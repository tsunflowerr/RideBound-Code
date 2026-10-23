from __future__ import annotations

import hashlib
import json
import pathlib
import subprocess
from dataclasses import dataclass
from typing import Any, Callable, Iterable, Mapping

from .errors import AdapterFailure
from .mapping import canonical_json_bytes, wp4_policy_binding_hash
from .runner_client import RunnerClient
from .transcript import ProtocolTranscriptRecorder


FLEETPY_VERSION = "1.0.2"
FLEETPY_COMMIT = "053aa9d4fcfde91c5d303435d5748f9206c071b0"
ADAPTER_ID = "fleetpy-ridebound"
ADAPTER_VERSION = "1.0.0"
# The declared valid range for the Runner frame bound is 1 MiB..64 MiB
# (ADR-039). This is a resource guard, not a correctness invariant: a denser
# scenario legitimately produces a larger full-state checkpoint, and exceeding
# the bound is a typed fail-closed error rather than silent truncation. A
# contended peak-window grid with long per-vehicle routes overran 16 MiB, so the
# adapter now requests the declared ceiling and the observed frame sizes are
# recorded so the headroom stays visible.
RUNNER_MAXIMUM_INPUT_LINE_BYTES = 64 * 1024 * 1024
RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = 64 * 1024 * 1024


def _fail(code: str, path: str, detail: str) -> AdapterFailure:
    return AdapterFailure(code, path, detail)


def _required_text(values: Mapping[str, Any], key: str) -> str:
    value = _setting(values, key)
    if not isinstance(value, str) or not value:
        raise _fail("RBWP7_SETTING_REQUIRED", f"$.operator.{key}", repr(value))
    return value


def _setting(values: Mapping[str, Any], key: str, default: Any = None) -> Any:
    direct = values.get(key, default)
    prefixed_key = f"op_{key}"
    prefixed = values.get(prefixed_key, default)
    if key in values and prefixed_key in values and direct != prefixed:
        raise _fail(
            "RBWP7_SETTING_CONFLICT",
            f"$.operator.{key}",
            f"direct={direct!r}; prefixed={prefixed!r}",
        )
    return direct if key in values else prefixed


def _required_file(values: Mapping[str, Any], key: str) -> pathlib.Path:
    path = pathlib.Path(_required_text(values, key)).expanduser().resolve()
    if not path.is_file():
        raise _fail("RBWP7_SETTING_FILE_MISSING", f"$.operator.{key}", str(path))
    return path


def _required_directory(values: Mapping[str, Any], key: str) -> pathlib.Path:
    path = pathlib.Path(_required_text(values, key)).expanduser().resolve()
    if not path.is_dir():
        raise _fail("RBWP7_SETTING_DIRECTORY_MISSING", f"$.operator.{key}", str(path))
    return path


def _positive_integer(values: Mapping[str, Any], key: str) -> int:
    value = _setting(values, key)
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise _fail("RBWP7_SETTING_INTEGER_INVALID", f"$.operator.{key}", repr(value))
    return value


def _nonnegative_integer(values: Mapping[str, Any], key: str, default: int = 0) -> int:
    value = _setting(values, key, default)
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise _fail("RBWP7_SETTING_INTEGER_INVALID", f"$.operator.{key}", repr(value))
    return value


def _strict_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate field {key!r}")
        result[key] = value
    return result


def _read_json(path: pathlib.Path) -> dict[str, Any]:
    try:
        with path.open("r", encoding="utf-8", newline="") as source:
            value = json.load(source, object_pairs_hook=_strict_object)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as exc:
        raise _fail("RBWP7_SETTING_JSON_INVALID", str(path), str(exc)) from exc
    if not isinstance(value, dict):
        raise _fail("RBWP7_SETTING_JSON_INVALID", str(path), type(value).__name__)
    return value


def _require_declared_commitment_policy(
    commitment: Mapping[str, Any],
    policy_id: str,
    path: pathlib.Path,
) -> None:
    policies = commitment.get("policies")
    if not isinstance(policies, list) or not policies:
        raise _fail("RBWP7_COMMITMENT_CONFIG_INVALID", str(path), "$.policies")
    declared: set[str] = set()
    for index, policy in enumerate(policies):
        value = policy.get("policyId") if isinstance(policy, dict) else None
        if not isinstance(value, str) or not value or value in declared:
            raise _fail(
                "RBWP7_COMMITMENT_CONFIG_INVALID",
                str(path),
                f"$.policies[{index}].policyId={value!r}",
            )
        declared.add(value)
    if policy_id not in declared:
        raise _fail(
            "RBWP7_COMMITMENT_POLICY_UNDECLARED",
            "$.operator.ridebound_commitment_policy_id",
            f"policy={policy_id!r}; declared={sorted(declared)!r}",
        )


def _sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
    except OSError as exc:
        raise _fail("RBWP7_ARTIFACT_READ_FAILED", str(path), str(exc)) from exc
    return digest.hexdigest()


def _git_commit(repository: pathlib.Path) -> str:
    try:
        completed = subprocess.run(
            ["git", "-C", str(repository), "rev-parse", "HEAD"],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="ascii",
            timeout=10,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        raise _fail("RBWP7_CORE_COMMIT_UNAVAILABLE", str(repository), str(exc)) from exc
    commit = completed.stdout.strip()
    if len(commit) != 40 or any(character not in "0123456789abcdef" for character in commit):
        raise _fail("RBWP7_CORE_COMMIT_INVALID", str(repository), commit)
    return commit


def runner_owns_service_bounds(wp4_value: Mapping[str, Any], path: pathlib.Path) -> bool:
    """ADR-076: read the WP4 `lateBoarding` key exactly as the Runner does.

    Absent or `reject` keeps every FleetPy service-window check; `record-v1` makes the
    Runner record late boardings and drops FleetPy's latest arrival, the only bound anchored
    on the original pickup window. Any other value fails before the Runner starts.
    """
    late_boarding = wp4_value.get("lateBoarding", "reject")
    if late_boarding not in ("reject", "record-v1"):
        raise _fail("RBWP7_WP4_LATE_BOARDING_INVALID", str(path), repr(late_boarding))
    return late_boarding == "record-v1"


def runner_relocates_waiting_riders(wp4_value: Mapping[str, Any], path: pathlib.Path) -> bool:
    """G2: true when the WP4 `candidateGeneration` declares `waitingRiderRelocation`.

    Absent keeps every earlier adapter path unchanged. The Runner validates the object's
    contents; any value other than an object fails here before the Runner starts.
    """
    generation = wp4_value.get("candidateGeneration")
    if not isinstance(generation, Mapping) or "waitingRiderRelocation" not in generation:
        return False
    if not isinstance(generation["waitingRiderRelocation"], Mapping):
        raise _fail(
            "RBWP7_WP4_RELOCATION_INVALID",
            str(path),
            repr(generation["waitingRiderRelocation"]),
        )
    return True


@dataclass(frozen=True)
class RideBoundSessionSettings:
    dotnet: pathlib.Path
    runner_root: pathlib.Path
    runner_dll: pathlib.Path
    commitment_config: pathlib.Path
    wp4_config: pathlib.Path
    fleetpy_root: pathlib.Path
    repository_root: pathlib.Path
    run_id: str
    scenario_id: str
    master_seed: int
    service_class: str
    commitment_policy_id: str
    timeout_seconds: int
    complete_travel_snapshot_maximum_nodes: int
    transcript_path: pathlib.Path | None
    policy_id: str
    policy_version: str
    policy_binding_hash: str
    core_commit: str
    artifact_paths: tuple[pathlib.Path, ...]
    # ADR-076 (exploratory): true when the WP4 configuration declares
    # `lateBoarding: record-v1`. The Runner then records late boardings, and the adapter
    # drops the one FleetPy bound anchored on the original window (the latest arrival).
    runner_service_bounds: bool = False
    # G2 (exploratory): true when the WP4 configuration declares
    # `candidateGeneration.waitingRiderRelocation`. The adapter then infers and checks the one
    # waiting rider a decision may move between vehicles (relocation.py).
    waiting_rider_relocation: bool = False

    @classmethod
    def from_attributes(
        cls,
        attributes: Mapping[str, Any],
    ) -> "RideBoundSessionSettings":
        dotnet = _required_file(attributes, "ridebound_dotnet_path")
        runner_root = _required_directory(attributes, "ridebound_runner_root")
        runner_dll = runner_root / "RideBound.Runner.dll"
        if not runner_dll.is_file():
            raise _fail("RBWP7_RUNNER_DLL_MISSING", "$.operator.ridebound_runner_root", str(runner_dll))
        commitment = _required_file(attributes, "ridebound_commitment_config")
        wp4 = _required_file(attributes, "ridebound_wp4_config")
        fleetpy_root = _required_directory(attributes, "ridebound_fleetpy_root")
        repository = _required_directory(attributes, "ridebound_repository_root")
        commitment_value = _read_json(commitment)
        wp4_value = _read_json(wp4)
        policy_id = wp4_value.get("policyId")
        policy_version = wp4_value.get("policyVersion")
        if not isinstance(policy_id, str) or not policy_id:
            raise _fail("RBWP7_WP4_POLICY_INVALID", str(wp4), repr(policy_id))
        if not isinstance(policy_version, str) or not policy_version:
            raise _fail("RBWP7_WP4_POLICY_INVALID", str(wp4), repr(policy_version))
        runner_service_bounds = runner_owns_service_bounds(wp4_value, wp4)
        waiting_rider_relocation = runner_relocates_waiting_riders(wp4_value, wp4)
        commitment_policy_id = _required_text(
            attributes,
            "ridebound_commitment_policy_id",
        )
        _require_declared_commitment_policy(
            commitment_value,
            commitment_policy_id,
            commitment,
        )
        timeout = _setting(attributes, "ridebound_timeout_seconds", 60)
        if isinstance(timeout, bool) or not isinstance(timeout, int) or timeout < 1:
            raise _fail("RBWP7_SETTING_INTEGER_INVALID", "$.operator.ridebound_timeout_seconds", repr(timeout))
        transcript_value = _setting(attributes, "ridebound_transcript_path")
        if transcript_value is None:
            transcript_path = None
        elif not isinstance(transcript_value, str) or not transcript_value:
            raise _fail(
                "RBWP7_SETTING_PATH_INVALID",
                "$.operator.ridebound_transcript_path",
                repr(transcript_value),
            )
        else:
            transcript_path = pathlib.Path(transcript_value).resolve()
            if not transcript_path.parent.is_dir() or transcript_path.exists():
                raise _fail(
                    "RBWP7_SETTING_OUTPUT_PATH_INVALID",
                    "$.operator.ridebound_transcript_path",
                    str(transcript_path),
                )
        adapter_root = pathlib.Path(__file__).parents[1]
        fleetpy_artifacts = (
            fleetpy_root / "LICENSE",
            fleetpy_root / "environment.yml",
            fleetpy_root / "src" / "fleetctrl" / "FleetControlBase.py",
            fleetpy_root / "src" / "routing" / "NetworkBase.py",
            fleetpy_root / "src" / "simulation" / "Vehicles.py",
            fleetpy_root / "src" / "misc" / "init_modules.py",
        )
        adapter_artifacts = (
            adapter_root / "environment.lock.yml",
            adapter_root / "capability-matrix.json",
            *sorted((adapter_root / "ridebound_fleetpy").glob("*.py")),
        )
        scenario_root_value = _setting(attributes, "ridebound_scenario_root")
        if scenario_root_value is None:
            scenario_artifacts: tuple[pathlib.Path, ...] = ()
        else:
            scenario_root = pathlib.Path(str(scenario_root_value)).resolve()
            if not scenario_root.is_dir():
                raise _fail(
                    "RBWP7_SETTING_DIRECTORY_MISSING",
                    "$.operator.ridebound_scenario_root",
                    str(scenario_root),
                )
            scenario_artifacts = tuple(
                sorted(path for path in scenario_root.rglob("*") if path.is_file())
            )
            if not scenario_artifacts:
                raise _fail(
                    "RBWP7_SCENARIO_ARTIFACTS_EMPTY",
                    "$.operator.ridebound_scenario_root",
                    str(scenario_root),
                )
        additional_value = _setting(attributes, "ridebound_additional_artifacts", ())
        if isinstance(additional_value, str):
            additional_values = (additional_value,)
        elif isinstance(additional_value, (list, tuple)) and all(
            isinstance(value, str) and value for value in additional_value
        ):
            additional_values = tuple(additional_value)
        else:
            raise _fail(
                "RBWP7_SETTING_ARTIFACT_LIST_INVALID",
                "$.operator.ridebound_additional_artifacts",
                repr(additional_value),
            )
        additional_artifacts = tuple(
            pathlib.Path(value).resolve() for value in additional_values
        )
        published = tuple(sorted(path for path in runner_root.iterdir() if path.is_file()))
        artifacts = (
            dotnet,
            commitment,
            wp4,
            *published,
            *fleetpy_artifacts,
            *adapter_artifacts,
            *scenario_artifacts,
            *additional_artifacts,
        )
        artifacts = tuple(
            dict.fromkeys(path.resolve() for path in artifacts)
        )
        missing = [str(path) for path in artifacts if not path.is_file()]
        if missing:
            raise _fail("RBWP7_ARTIFACT_MISSING", "$.artifacts", repr(missing))
        return cls(
            dotnet,
            runner_root,
            runner_dll,
            commitment,
            wp4,
            fleetpy_root,
            repository,
            _required_text(attributes, "ridebound_run_id"),
            _required_text(attributes, "ridebound_scenario_id"),
            _positive_integer(attributes, "ridebound_master_seed"),
            _required_text(attributes, "ridebound_service_class"),
            commitment_policy_id,
            timeout,
            _nonnegative_integer(
                attributes,
                "ridebound_complete_travel_snapshot_maximum_nodes",
            ),
            transcript_path,
            policy_id,
            policy_version,
            wp4_policy_binding_hash(commitment, wp4),
            _git_commit(repository),
            tuple(path.resolve() for path in artifacts),
            runner_service_bounds,
            waiting_rider_relocation,
        )


class RideBoundProtocolSession:
    """Owns one exact long-lived Runner and its initialize/ACK boundary."""

    def __init__(
        self,
        settings: RideBoundSessionSettings,
        client_factory: Callable[..., RunnerClient] = RunnerClient,
    ) -> None:
        self.settings = settings
        command = [
            str(settings.dotnet),
            str(settings.runner_dll),
            "--mode",
            "commitment",
            "--policy-config",
            str(settings.commitment_config),
            "--wp4-config",
            str(settings.wp4_config),
            "--solver-seed-source",
            "manifest-master-seed",
            "--maximum-line-bytes",
            str(RUNNER_MAXIMUM_INPUT_LINE_BYTES),
        ]
        self._command = tuple(command)
        self._client_factory = client_factory
        self._transcript = (
            ProtocolTranscriptRecorder(settings.transcript_path)
            if settings.transcript_path is not None
            else None
        )
        self._client = self._new_client()
        self._initialized = False
        self._closed = False
        self._manifest_hash: str | None = None
        self._initialize_envelope: dict[str, Any] | None = None
        self._last_checkpoint: dict[str, Any] | None = None

    def _new_client(self):
        return self._client_factory(
            self._command,
            self.settings.artifact_paths,
            timeout_seconds=self.settings.timeout_seconds,
            maximum_input_line_bytes=RUNNER_MAXIMUM_INPUT_LINE_BYTES,
            maximum_output_line_bytes=RUNNER_MAXIMUM_OUTPUT_LINE_BYTES,
            transcript_writer=(
                self._transcript.record
                if self._transcript is not None
                else None
            ),
        )

    @property
    def manifest_hash(self) -> str | None:
        return self._manifest_hash

    @property
    def artifact_receipts(self) -> dict[str, list[dict[str, str]]]:
        return self._client.artifact_receipts

    def initialize(
        self,
        snapshot: dict[str, Any],
        node_ids: Iterable[str],
        request_ids: Iterable[str],
        vehicle_ids: Iterable[str],
    ) -> dict[str, Any]:
        if self._initialized or self._closed:
            raise _fail("RBWP7_SESSION_STATE_INVALID", "$.session", "initialize")
        nodes = sorted(set(node_ids))
        requests = sorted(set(request_ids))
        vehicles = sorted(set(vehicle_ids))
        if not nodes or not vehicles:
            raise _fail("RBWP7_INITIAL_STATE_EMPTY", "$.initialize", repr((nodes, vehicles)))
        scenario_content = {
            "scenarioId": self.settings.scenario_id,
            "nodes": nodes,
            "requestIds": requests,
            "vehicleIds": vehicles,
            "travelSnapshotHash": snapshot["snapshotHash"],
        }
        scenario_hash = hashlib.sha256(
            b"RideBound.Wp7Scenario.v1\0" + canonical_json_bytes(scenario_content)
        ).hexdigest()
        graph_hash = hashlib.sha256(
            b"RideBound.Wp7Graph.v1\0" + canonical_json_bytes({"nodes": nodes})
        ).hexdigest()
        initialize = {
            "schemaVersion": "1.0.0",
            "messageType": "initializeRun",
            "runId": self.settings.run_id,
            "scenarioId": self.settings.scenario_id,
            "payload": {
                "manifest": {
                    "protocolVersion": "1.0.0",
                    "masterSeed": self.settings.master_seed,
                    "policyId": self.settings.policy_id,
                    "policyVersion": self.settings.policy_version,
                    "policyConfigurationHash": self.settings.policy_binding_hash,
                    "scenarioContentHash": scenario_hash,
                    "graphSnapshotHash": graph_hash,
                    "travelTimeSnapshotHash": snapshot["snapshotHash"],
                    "costUnitId": "abstract-generalized-cost-v1",
                    "sourceUnitConversions": [
                        {
                            "quantity": "time",
                            "sourceUnit": "second",
                            "canonicalUnit": "millisecond",
                            "roundingRule": "roundTiesToEven",
                        },
                        {
                            "quantity": "distance",
                            "sourceUnit": "meter",
                            "canonicalUnit": "millimeter",
                            "roundingRule": "roundTiesToEven",
                        },
                    ],
                    "capabilitySelection": {
                        "status": "accepted",
                        "positionModel": "directedEdgeProgress",
                        "capabilities": [
                            "dynamicTravelTimes",
                            "exactEventOrdering",
                            "oldPlanProjection",
                        ],
                        "maxFleetSize": 5000,
                        "maxRequestCount": 100000,
                    },
                    "adapter": {
                        "adapterId": ADAPTER_ID,
                        "adapterVersion": ADAPTER_VERSION,
                    },
                    "simulator": {
                        "simulatorId": "fleetpy",
                        "simulatorVersion": FLEETPY_VERSION,
                        "upstreamCommitSha": FLEETPY_COMMIT,
                    },
                    "coreCommitSha": self.settings.core_commit,
                    "binarySha256": _sha256(self.settings.runner_dll),
                }
            },
        }
        self._client.start()
        self._client.negotiate(_hello())
        initialized = self._client.initialize(initialize)
        self._manifest_hash = initialized["payload"]["manifestHash"]
        self._initialize_envelope = initialize
        self._initialized = True
        return initialized

    def decide(self, event_batch: dict[str, Any]) -> dict[str, Any]:
        if not self._initialized or self._closed:
            raise _fail("RBWP7_SESSION_STATE_INVALID", "$.session", "decide")
        return self._client.decide(event_batch)

    def acknowledge(self, decision: dict[str, Any]) -> None:
        try:
            decision_hash = decision["payload"]["decisionHash"]
        except (KeyError, TypeError) as exc:
            raise _fail("RBWP7_DECISION_HASH_MISSING", "$.decision", str(exc)) from exc
        self._client.acknowledge(decision_hash)

    def checkpoint(self) -> dict[str, Any]:
        if not self._initialized or self._closed:
            raise _fail("RBWP7_SESSION_STATE_INVALID", "$.session", "checkpoint")
        checkpoint = self._client.request_checkpoint()
        self._last_checkpoint = checkpoint
        return checkpoint

    def restart(self, checkpoint: dict[str, Any]) -> dict[str, Any]:
        if (
            not self._initialized
            or self._closed
            or self._initialize_envelope is None
            or self._client.state != "initialized"
        ):
            raise _fail("RBWP7_RESTART_UNSAFE", "$.session", self._client.state)
        try:
            payload = checkpoint["payload"]
            expected_hash = payload["checkpointHash"]
        except (KeyError, TypeError) as exc:
            raise _fail("RBWP7_CHECKPOINT_INVALID", "$.checkpoint", str(exc)) from exc
        if checkpoint is not self._last_checkpoint:
            raise _fail(
                "RBWP7_CHECKPOINT_NOT_LATEST",
                "$.checkpoint.checkpointHash",
                repr(expected_hash),
            )

        self._client.shutdown()
        replacement = self._new_client()
        try:
            replacement.start()
            replacement.negotiate(_hello())
            initialized = replacement.initialize(self._initialize_envelope)
            if initialized["payload"].get("manifestHash") != self._manifest_hash:
                raise _fail(
                    "RBWP7_RESTART_MANIFEST_MISMATCH",
                    "$.initialized.manifestHash",
                    repr(initialized["payload"].get("manifestHash")),
                )
            restored = replacement.restore(payload)
        except BaseException:
            replacement.shutdown()
            raise
        self._client = replacement
        return restored

    def close(self) -> None:
        if self._closed:
            return
        try:
            self._client.shutdown()
        finally:
            try:
                if self._transcript is not None:
                    self._transcript.close()
            finally:
                self._closed = True


def _hello() -> dict[str, Any]:
    return {
        "schemaVersion": "1.0.0",
        "messageType": "hello",
        "payload": {
            "adapterId": ADAPTER_ID,
            "adapterVersion": ADAPTER_VERSION,
            "supportedSchemaVersions": ["1.0.0"],
            "positionModel": "directedEdgeProgress",
            "capabilities": [
                "oldPlanProjection",
                "cancellations",
                "exactEventOrdering",
                "dynamicTravelTimes",
            ],
            "maxFleetSize": 5000,
            "maxRequestCount": 100000,
        },
    }
