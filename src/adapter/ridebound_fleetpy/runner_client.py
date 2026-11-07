from __future__ import annotations

import hashlib
import io
import json
import os
import pathlib
import queue
import re
import signal
import subprocess
import threading
import time
from dataclasses import dataclass
from typing import Any, Callable, Iterable

from .errors import AdapterFailure
from .mapping import canonical_json_bytes


_HASH = re.compile(r"^[0-9a-f]{64}$")
_EOF = object()
# The Runner's helloAck must select this position model and offer at least these capabilities.
_POSITION_MODEL = "directedEdgeProgress"
_REQUIRED_CAPABILITIES = frozenset({"exactEventOrdering", "dynamicTravelTimes", "oldPlanProjection"})


@dataclass(frozen=True)
class ArtifactReceipt:
    path: str
    sha256: str


class RunnerClient:
    """Strict long-lived RideBound NDJSON process client.

    ACKs are locally hash-bound and ordered on the same stdin stream.  A following
    response-bearing request (the next decision or an explicit checkpoint) proves
    that the Runner consumed the ACK.  Shutdown drains stdout, so an error emitted
    for the final ACK cannot be silently lost.  Checkpoints are therefore requested
    only at evidence boundaries rather than serializing the complete online
    state after every epoch.
    """

    def __init__(
        self,
        command: Iterable[str],
        artifact_paths: Iterable[str | pathlib.Path],
        *,
        timeout_seconds: float = 30.0,
        shutdown_timeout_seconds: float = 10.0,
        maximum_input_line_bytes: int = 64 * 1024 * 1024,
        maximum_output_line_bytes: int = 64 * 1024 * 1024,
        maximum_stderr_bytes: int = 1024 * 1024,
        transcript_writer: Callable[[str, bytes], None] | None = None,
    ) -> None:
        self._command = tuple(str(part) for part in command)
        if not self._command:
            raise ValueError("command cannot be empty")
        self._artifact_paths = tuple(pathlib.Path(path).resolve() for path in artifact_paths)
        if not self._artifact_paths:
            raise ValueError("at least one artifact path is required")
        if timeout_seconds <= 0 or shutdown_timeout_seconds <= 0:
            raise ValueError("timeouts must be positive")
        if maximum_input_line_bytes < 256 or maximum_output_line_bytes < 256 or maximum_stderr_bytes < 1:
            raise ValueError("invalid stream bounds")
        self._timeout = timeout_seconds
        self._shutdown_timeout = shutdown_timeout_seconds
        self._maximum_input_line_bytes = maximum_input_line_bytes
        self._maximum_output_line_bytes = maximum_output_line_bytes
        self._maximum_stderr_bytes = maximum_stderr_bytes
        self._transcript_writer = transcript_writer
        self._process: subprocess.Popen[bytes] | None = None
        self._stdout: queue.Queue[bytes | object | BaseException] = queue.Queue()
        self._stderr = bytearray()
        self._stderr_lock = threading.Lock()
        self._stderr_overflow = threading.Event()
        self._stdout_thread: threading.Thread | None = None
        self._stderr_thread: threading.Thread | None = None
        self._state = "created"
        self._run_id: str | None = None
        self._scenario_id: str | None = None
        self._pending: dict[str, Any] | None = None
        self._receipts_before: tuple[ArtifactReceipt, ...] = ()
        self._receipts_after: tuple[ArtifactReceipt, ...] = ()

    @property
    def state(self) -> str:
        return self._state

    @property
    def artifact_receipts(self) -> dict[str, list[dict[str, str]]]:
        return {
            "before": [receipt.__dict__.copy() for receipt in self._receipts_before],
            "after": [receipt.__dict__.copy() for receipt in self._receipts_after],
        }

    @property
    def diagnostics(self) -> str:
        with self._stderr_lock:
            return bytes(self._stderr).decode("utf-8", errors="replace")

    def start(self) -> None:
        self._require_state("created")
        self._receipts_before = self._hash_artifacts()
        creationflags = 0
        popen_arguments: dict[str, Any] = {}
        if os.name == "nt":
            creationflags = subprocess.CREATE_NEW_PROCESS_GROUP
        else:
            popen_arguments["start_new_session"] = True
        try:
            process = subprocess.Popen(
                self._command,
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                bufsize=0,
                creationflags=creationflags,
                **popen_arguments,
            )
        except OSError as exc:
            self._state = "failed"
            raise AdapterFailure("RBWP7_RUNNER_START_FAILED", "$runner", str(exc)) from exc
        self._process = process
        self._stdout_thread = threading.Thread(
            target=self._read_stdout, name="ridebound-stdout", daemon=True
        )
        self._stderr_thread = threading.Thread(
            target=self._read_stderr, name="ridebound-stderr", daemon=True
        )
        self._stdout_thread.start()
        self._stderr_thread.start()
        self._state = "new"

    def negotiate(self, hello: dict[str, Any]) -> dict[str, Any]:
        self._require_state("new")
        self._require_outgoing_type(hello, "hello")
        self._write(hello)
        acknowledgement = self._read_expected("helloAck")
        selection = acknowledgement["payload"].get("capabilitySelection")
        if not isinstance(selection, dict):
            self._fail("RBWP7_RUNNER_ACK_INVALID", "missing capabilitySelection")
        if selection.get("status") != "accepted":
            self._fail("RBWP7_RUNNER_CAPABILITY_DOWNGRADE", repr(selection))
        if selection.get("positionModel") != _POSITION_MODEL:
            self._fail("RBWP7_RUNNER_POSITION_DOWNGRADE", repr(selection))
        capabilities = selection.get("capabilities")
        if not isinstance(capabilities, list) or not _REQUIRED_CAPABILITIES.issubset(
            set(capabilities)
        ):
            self._fail("RBWP7_RUNNER_CAPABILITY_MISSING", repr(capabilities))
        self._state = "negotiated"
        return acknowledgement

    def initialize(self, initialize_run: dict[str, Any]) -> dict[str, Any]:
        self._require_state("negotiated")
        self._require_outgoing_type(initialize_run, "initializeRun")
        run_id, scenario_id = self._require_run_context(initialize_run)
        self._write(initialize_run)
        initialized = self._read_expected("initialized", run_id, scenario_id)
        self._run_id = run_id
        self._scenario_id = scenario_id
        self._state = "initialized"
        return initialized

    def decide(self, event_batch: dict[str, Any]) -> dict[str, Any]:
        self._require_state("initialized")
        self._require_outgoing_type(event_batch, "eventBatch")
        run_id, scenario_id = self._require_run_context(event_batch, require_epoch=True)
        if run_id != self._run_id or scenario_id != self._scenario_id:
            self._fail("RBWP7_RUNNER_IDENTITY_MISMATCH", f"{run_id}/{scenario_id}")
        self._write(event_batch)
        decision = self._read_expected(
            "decision",
            run_id,
            scenario_id,
            event_batch["epochId"],
            event_batch["simTimeMs"],
        )
        decision_hash = decision["payload"].get("decisionHash")
        if not isinstance(decision_hash, str) or _HASH.fullmatch(decision_hash) is None:
            self._fail("RBWP7_RUNNER_DECISION_HASH_INVALID", repr(decision_hash))
        self._pending = decision
        self._state = "pending"
        return decision

    def acknowledge(self, decision_hash: str) -> None:
        self._require_state("pending")
        pending = self._pending
        assert pending is not None
        actual = pending["payload"]["decisionHash"]
        if decision_hash != actual:
            self._fail("RBWP7_RUNNER_ACK_HASH_LOCAL_MISMATCH", decision_hash)
        acknowledgement = {
            "schemaVersion": "1.0.0",
            "messageType": "decisionApplied",
            "runId": self._run_id,
            "scenarioId": self._scenario_id,
            "epochId": pending["epochId"],
            "simTimeMs": pending["simTimeMs"],
            "payload": {"decisionHash": decision_hash},
        }
        self._write(acknowledgement)
        self._pending = None
        self._state = "initialized"

    def request_checkpoint(self) -> dict[str, Any]:
        self._require_state("initialized")
        self._write(self._run_envelope("checkpoint", {}))
        checkpoint = self._read_expected("checkpoint", self._run_id, self._scenario_id)
        checkpoint_hash = checkpoint["payload"].get("checkpointHash")
        if not isinstance(checkpoint_hash, str) or _HASH.fullmatch(checkpoint_hash) is None:
            self._fail("RBWP7_RUNNER_CHECKPOINT_HASH_INVALID", repr(checkpoint_hash))
        return checkpoint

    def shutdown(self) -> None:
        if self._state == "closed":
            return
        process = self._process
        if process is None:
            self._state = "closed"
            return
        if process.poll() is None:
            try:
                if self._state not in {"failed", "created"}:
                    self._write({"schemaVersion": "1.0.0", "messageType": "shutdown", "payload": {}})
                if process.stdin is not None:
                    process.stdin.close()
                process.wait(timeout=self._shutdown_timeout)
            except (AdapterFailure, BrokenPipeError, subprocess.TimeoutExpired):
                self._terminate_tree()
        if process.poll() is None:
            self._terminate_tree()
        return_code = process.wait(timeout=self._shutdown_timeout)
        if self._stdout_thread is not None:
            self._stdout_thread.join(timeout=self._shutdown_timeout)
        if self._stderr_thread is not None:
            self._stderr_thread.join(timeout=self._shutdown_timeout)
        for stream in (process.stdin, process.stdout, process.stderr):
            if stream is not None and not stream.closed:
                stream.close()
        if self._state != "failed":
            self._assert_no_extra_stdout()
            if self._stderr_overflow.is_set():
                self._state = "failed"
                raise AdapterFailure(
                    "RBWP7_RUNNER_STDERR_OVERFLOW", "$runner", str(self._maximum_stderr_bytes)
                )
        self._receipts_after = self._hash_artifacts()
        if self._receipts_after != self._receipts_before:
            self._state = "failed"
            raise AdapterFailure("RBWP7_RUNNER_ARTIFACT_DRIFT", "$runner.artifacts", "pre/post hash differs")
        if return_code != 0 and self._state != "failed":
            self._state = "failed"
            raise AdapterFailure("RBWP7_RUNNER_EXIT_NONZERO", "$runner", str(return_code))
        self._state = "closed"

    def __enter__(self) -> "RunnerClient":
        self.start()
        return self

    def __exit__(self, _type: Any, _value: Any, _traceback: Any) -> None:
        self.shutdown()

    def _read_stdout(self) -> None:
        process = self._process
        assert process is not None and process.stdout is not None
        # The pipes are unbuffered (bufsize=0), and readline on a raw pipe makes one system call
        # per byte. A buffer over process.stdout itself reads 1 MiB at a time and returns the same
        # lines; once shutdown closes process.stdout, the next read fails as before.
        stdout = io.BufferedReader(process.stdout, buffer_size=1024 * 1024)
        try:
            while True:
                line = stdout.readline(self._maximum_output_line_bytes + 2)
                if not line:
                    self._stdout.put(_EOF)
                    return
                self._stdout.put(line)
        except BaseException as exc:
            self._stdout.put(exc)

    def _read_stderr(self) -> None:
        process = self._process
        assert process is not None and process.stderr is not None
        while True:
            chunk = process.stderr.read(4096)
            if not chunk:
                return
            with self._stderr_lock:
                remaining = self._maximum_stderr_bytes - len(self._stderr)
                if remaining > 0:
                    self._stderr.extend(chunk[:remaining])
                if len(chunk) > remaining:
                    self._stderr_overflow.set()

    def _write(self, envelope: dict[str, Any]) -> None:
        self._check_process()
        process = self._process
        assert process is not None and process.stdin is not None
        encoded = canonical_json_bytes(envelope)
        if len(encoded) > self._maximum_input_line_bytes:
            self._fail("RBWP7_RUNNER_INPUT_TOO_LARGE", str(len(encoded)))
        if self._transcript_writer is not None:
            self._transcript_writer("adapterToRunner", encoded)
        try:
            process.stdin.write(encoded + b"\n")
            process.stdin.flush()
        except (BrokenPipeError, OSError) as exc:
            self._fail("RBWP7_RUNNER_STDIN_CLOSED", str(exc))

    def _read_expected(
        self,
        message_type: str,
        run_id: str | None = None,
        scenario_id: str | None = None,
        epoch_id: int | None = None,
        sim_time_ms: int | None = None,
    ) -> dict[str, Any]:
        self._check_process(allow_exited=True)
        if self._stderr_overflow.is_set():
            self._fail("RBWP7_RUNNER_STDERR_OVERFLOW", str(self._maximum_stderr_bytes))
        deadline = time.monotonic() + self._timeout
        while True:
            if self._stderr_overflow.is_set():
                self._fail("RBWP7_RUNNER_STDERR_OVERFLOW", str(self._maximum_stderr_bytes))
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                self._fail("RBWP7_RUNNER_RESPONSE_TIMEOUT", message_type)
            try:
                item = self._stdout.get(timeout=min(remaining, 0.05))
                break
            except queue.Empty:
                continue
        if item is _EOF:
            self._fail("RBWP7_RUNNER_EOF", message_type)
        if isinstance(item, BaseException):
            self._fail("RBWP7_RUNNER_STDOUT_READ_FAILED", str(item))
        assert isinstance(item, bytes)
        transcript_frame = item[:-1] if item.endswith(b"\n") else item
        if self._transcript_writer is not None and transcript_frame:
            self._transcript_writer("runnerToAdapter", transcript_frame)
        if len(item) > self._maximum_output_line_bytes + 1:
            self._fail("RBWP7_RUNNER_OUTPUT_TOO_LARGE", str(len(item)))
        if not item.endswith(b"\n"):
            self._fail("RBWP7_RUNNER_INCOMPLETE_FRAME", repr(item[-32:]))
        frame = transcript_frame  # item[:-1]; a final checkpoint is up to ~0.8 GB, so no second copy
        if frame.endswith(b"\r"):
            self._fail("RBWP7_RUNNER_CRLF_FORBIDDEN", "stdout must use LF framing")
        try:
            decoded = frame.decode("utf-8", errors="strict")
            envelope = json.loads(
                decoded,
                object_pairs_hook=self._strict_object,
                parse_float=lambda text: self._reject_json_number(text),
                parse_constant=lambda text: self._reject_json_number(text),
            )
        except (UnicodeDecodeError, json.JSONDecodeError, ValueError) as exc:
            self._fail("RBWP7_RUNNER_OUTPUT_MALFORMED", str(exc))
        self._validate_envelope(envelope)
        if envelope["messageType"] == "error":
            payload = envelope["payload"]
            self._fail(
                "RBWP7_RUNNER_PROTOCOL_ERROR",
                f"{payload.get('code', 'UNKNOWN')}: {payload.get('message', '')}",
            )
        expected_context = {
            "messageType": message_type,
            "runId": run_id,
            "scenarioId": scenario_id,
            "epochId": epoch_id,
            "simTimeMs": sim_time_ms,
        }
        if envelope.get("messageType") != message_type:
            self._fail("RBWP7_RUNNER_RESPONSE_TYPE_MISMATCH", repr(envelope.get("messageType")))
        for field, expected in expected_context.items():
            if expected is not None and envelope.get(field) != expected:
                self._fail(
                    "RBWP7_RUNNER_RESPONSE_CONTEXT_MISMATCH",
                    f"{field}: actual={envelope.get(field)!r}; expected={expected!r}",
                )
        return envelope

    def _validate_envelope(self, envelope: Any) -> None:
        if not isinstance(envelope, dict):
            self._fail("RBWP7_RUNNER_ENVELOPE_INVALID", type(envelope).__name__)
        allowed = {"schemaVersion", "messageType", "runId", "scenarioId", "epochId", "simTimeMs", "payload"}
        unknown = set(envelope) - allowed
        if unknown:
            self._fail("RBWP7_RUNNER_ENVELOPE_UNKNOWN_FIELD", repr(sorted(unknown)))
        if envelope.get("schemaVersion") != "1.0.0":
            self._fail("RBWP7_RUNNER_SCHEMA_VERSION_MISMATCH", repr(envelope.get("schemaVersion")))
        if not isinstance(envelope.get("messageType"), str) or not isinstance(envelope.get("payload"), dict):
            self._fail("RBWP7_RUNNER_ENVELOPE_INVALID", repr(envelope))

    def _require_outgoing_type(self, envelope: dict[str, Any], expected: str) -> None:
        self._validate_envelope(envelope)
        if envelope["messageType"] != expected:
            self._fail("RBWP7_RUNNER_REQUEST_TYPE_INVALID", repr(envelope["messageType"]))

    def _require_run_context(
        self, envelope: dict[str, Any], require_epoch: bool = False
    ) -> tuple[str, str]:
        run_id = envelope.get("runId")
        scenario_id = envelope.get("scenarioId")
        if not isinstance(run_id, str) or not run_id or not isinstance(scenario_id, str) or not scenario_id:
            self._fail("RBWP7_RUNNER_CONTEXT_MISSING", repr((run_id, scenario_id)))
        if require_epoch and (
            isinstance(envelope.get("epochId"), bool)
            or not isinstance(envelope.get("epochId"), int)
            or envelope["epochId"] < 1
            or isinstance(envelope.get("simTimeMs"), bool)
            or not isinstance(envelope.get("simTimeMs"), int)
            or envelope["simTimeMs"] < 0
        ):
            self._fail("RBWP7_RUNNER_EPOCH_CONTEXT_INVALID", repr(envelope))
        return run_id, scenario_id

    def _run_envelope(self, message_type: str, payload: dict[str, Any]) -> dict[str, Any]:
        return {
            "schemaVersion": "1.0.0",
            "messageType": message_type,
            "runId": self._run_id,
            "scenarioId": self._scenario_id,
            "payload": payload,
        }

    def _require_state(self, expected: str) -> None:
        if self._state != expected:
            self._fail("RBWP7_RUNNER_CLIENT_STATE_INVALID", f"actual={self._state}; expected={expected}")

    def _check_process(self, allow_exited: bool = False) -> None:
        process = self._process
        if process is None:
            self._fail("RBWP7_RUNNER_NOT_STARTED", "process is absent")
        return_code = process.poll()
        if return_code is not None and not allow_exited:
            self._fail("RBWP7_RUNNER_EXITED", str(return_code))
        if self._stderr_overflow.is_set():
            self._fail("RBWP7_RUNNER_STDERR_OVERFLOW", str(self._maximum_stderr_bytes))

    def _hash_artifacts(self) -> tuple[ArtifactReceipt, ...]:
        receipts: list[ArtifactReceipt] = []
        for path in sorted(self._artifact_paths, key=lambda item: str(item).casefold()):
            if not path.is_file():
                raise AdapterFailure("RBWP7_RUNNER_ARTIFACT_MISSING", "$runner.artifacts", str(path))
            digest = hashlib.sha256()
            with path.open("rb") as source:
                for chunk in iter(lambda: source.read(1024 * 1024), b""):
                    digest.update(chunk)
            receipts.append(ArtifactReceipt(str(path), digest.hexdigest()))
        return tuple(receipts)

    def _assert_no_extra_stdout(self) -> None:
        saw_eof = False
        while True:
            try:
                item = self._stdout.get(timeout=self._shutdown_timeout)
            except queue.Empty:
                raise AdapterFailure(
                    "RBWP7_RUNNER_STDOUT_DID_NOT_CLOSE", "$runner", "missing EOF"
                )
            if item is _EOF:
                saw_eof = True
                break
            if isinstance(item, BaseException):
                raise AdapterFailure(
                    "RBWP7_RUNNER_STDOUT_READ_FAILED", "$runner", str(item)
                )
            if isinstance(item, bytes) and self._transcript_writer is not None:
                frame = item[:-1] if item.endswith(b"\n") else item
                if frame:
                    self._transcript_writer("runnerToAdapter", frame)
            raise AdapterFailure(
                "RBWP7_RUNNER_EXTRA_OUTPUT", "$runner", "unexpected frame after shutdown"
            )
        if not saw_eof:
            raise AdapterFailure("RBWP7_RUNNER_STDOUT_DID_NOT_CLOSE", "$runner", "missing EOF")

    @staticmethod
    def _strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"duplicate JSON field {key!r}")
            result[key] = value
        return result

    @staticmethod
    def _reject_json_number(text: str) -> Any:
        raise ValueError(f"non-integer JSON number {text!r}")

    def _terminate_tree(self) -> None:
        process = self._process
        if process is None or process.poll() is not None:
            return
        if os.name == "nt":
            subprocess.run(
                ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                check=False,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
        else:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass

    def _fail(self, code: str, detail: str) -> None:
        self._state = "failed"
        raise AdapterFailure(code, "$runner", detail)
