from __future__ import annotations

import pathlib
import sys
import tempfile
import unittest


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

from ridebound_fleetpy.errors import AdapterFailure
from ridebound_fleetpy.runner_client import RunnerClient
from ridebound_fleetpy.session import _require_declared_commitment_policy
from ridebound_fleetpy.transcript import ProtocolTranscriptRecorder
from actual_fleetpy_medium_verify import decode_transcript, verify_bundle


FAKE = pathlib.Path(__file__).parent / "fixtures" / "fake_runner.py"
HASH = "a" * 64


def hello():
    return {
        "schemaVersion": "1.0.0",
        "messageType": "hello",
        "payload": {},
    }


def initialize():
    return {
        "schemaVersion": "1.0.0",
        "messageType": "initializeRun",
        "runId": "run-1",
        "scenarioId": "scenario-1",
        "payload": {},
    }


def event_batch():
    return {
        "schemaVersion": "1.0.0",
        "messageType": "eventBatch",
        "runId": "run-1",
        "scenarioId": "scenario-1",
        "epochId": 1,
        "simTimeMs": 1000,
        "payload": {"events": []},
    }


class RunnerClientTests(unittest.TestCase):
    def make_client(self, mode="normal", **options):
        return RunnerClient(
            [sys.executable, str(FAKE), mode],
            [sys.executable, FAKE],
            timeout_seconds=options.pop("timeout_seconds", 1),
            shutdown_timeout_seconds=options.pop("shutdown_timeout_seconds", 1),
            **options,
        )

    def test_full_lifecycle_is_hash_bound_and_checkpointed(self) -> None:
        client = self.make_client()
        client.start()
        client.negotiate(hello())
        client.initialize(initialize())
        decision = client.decide(event_batch())
        checkpoint = client.acknowledge_and_checkpoint(decision["payload"]["decisionHash"])
        self.assertEqual("b" * 64, checkpoint["payload"]["checkpointHash"])
        client.shutdown()
        self.assertEqual("closed", client.state)
        self.assertEqual(client.artifact_receipts["before"], client.artifact_receipts["after"])

    def test_node_only_capability_can_be_selected_explicitly(self) -> None:
        client = self.make_client("node-only", expected_position_model="nodeOnly")
        try:
            client.start()
            acknowledgement = client.negotiate(hello())
            selection = acknowledgement["payload"]["capabilitySelection"]
            self.assertEqual("nodeOnly", selection["positionModel"])
        finally:
            client.shutdown()

    def test_position_model_mismatch_remains_fail_closed(self) -> None:
        client = self.make_client(expected_position_model="nodeOnly")
        try:
            client.start()
            with self.assertRaises(AdapterFailure) as raised:
                client.negotiate(hello())
            self.assertEqual("RBWP7_RUNNER_POSITION_DOWNGRADE", raised.exception.code)
        finally:
            client.shutdown()

    def test_adapter_policy_setting_must_be_declared_by_commitment_config(self) -> None:
        config = {"policies": [{"policyId": "declared-policy"}]}
        _require_declared_commitment_policy(
            config,
            "declared-policy",
            pathlib.Path("commitment.json"),
        )
        with self.assertRaises(AdapterFailure) as raised:
            _require_declared_commitment_policy(
                config,
                "wrong-policy",
                pathlib.Path("commitment.json"),
            )
        self.assertEqual("RBWP7_COMMITMENT_POLICY_UNDECLARED", raised.exception.code)

    def test_local_ack_hash_mismatch_never_reaches_runner(self) -> None:
        client = self.make_client()
        try:
            client.start()
            client.negotiate(hello())
            client.initialize(initialize())
            client.decide(event_batch())
            with self.assertRaises(AdapterFailure) as raised:
                client.acknowledge_and_checkpoint("f" * 64)
            self.assertEqual("RBWP7_RUNNER_ACK_HASH_LOCAL_MISMATCH", raised.exception.code)
        finally:
            client.shutdown()

    def test_ack_without_checkpoint_is_confirmed_by_next_response(self) -> None:
        client = self.make_client()
        client.start()
        client.negotiate(hello())
        client.initialize(initialize())
        decision = client.decide(event_batch())
        client.acknowledge(decision["payload"]["decisionHash"])
        self.assertEqual("initialized", client.state)
        checkpoint = client.request_checkpoint()
        self.assertEqual("b" * 64, checkpoint["payload"]["checkpointHash"])
        client.shutdown()

    def test_input_and_output_bounds_are_independent(self) -> None:
        client = self.make_client(
            "oversized",
            maximum_input_line_bytes=2048,
            maximum_output_line_bytes=256,
        )
        try:
            client.start()
            with self.assertRaises(AdapterFailure) as raised:
                client.negotiate(hello())
            self.assertEqual("RBWP7_RUNNER_OUTPUT_TOO_LARGE", raised.exception.code)
        finally:
            client.shutdown()

    def test_transcript_preserves_exact_frames_and_detects_mutation(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "transcript.ndjson"
            recorder = ProtocolTranscriptRecorder(path)
            client = self.make_client(transcript_writer=recorder.record)
            client.start()
            client.negotiate(hello())
            client.initialize(initialize())
            decision = client.decide(event_batch())
            client.acknowledge(decision["payload"]["decisionHash"])
            client.request_checkpoint()
            client.shutdown()
            recorder.close()

            decoded = decode_transcript(path)
            self.assertEqual("hello", decoded[0][1]["messageType"])
            self.assertEqual("shutdown", decoded[-1][1]["messageType"])
            self.assertEqual(
                [
                    "hello",
                    "helloAck",
                    "initializeRun",
                    "initialized",
                    "eventBatch",
                    "decision",
                    "decisionApplied",
                    "checkpoint",
                    "checkpoint",
                    "shutdown",
                ],
                [envelope["messageType"] for _, envelope in decoded],
            )

            mutated = bytearray(path.read_bytes())
            marker = mutated.index(b'"frameSha256":"') + len(b'"frameSha256":"')
            mutated[marker] = ord("f") if mutated[marker] != ord("f") else ord("e")
            path.write_bytes(mutated)
            with self.assertRaisesRegex(RuntimeError, "frame length/hash mismatch"):
                decode_transcript(path)

    def test_bundle_manifest_rejects_an_unlisted_file(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "bundle-manifest.json").write_text(
                '{"bundleType":"ridebound-wp7-actual-fleetpy-medium-v1",'
                '"files":[],"schemaVersion":"1.0.0"}\n',
                encoding="utf-8",
                newline="\n",
            )
            (root / "unlisted.txt").write_text(
                "mutation\n",
                encoding="utf-8",
                newline="\n",
            )

            with self.assertRaisesRegex(RuntimeError, "bundle inventory differs"):
                verify_bundle(root)

    def test_restore_requires_initialized_state(self) -> None:
        client = self.make_client()
        try:
            client.start()
            with self.assertRaises(AdapterFailure) as raised:
                client.restore({})
            self.assertEqual("RBWP7_RUNNER_CLIENT_STATE_INVALID", raised.exception.code)
        finally:
            client.shutdown()

    def test_restore_requires_exact_checkpoint_hash_echo(self) -> None:
        client = self.make_client()
        try:
            client.start()
            client.negotiate(hello())
            client.initialize(initialize())
            with self.assertRaises(AdapterFailure) as raised:
                client.restore({"checkpointHash": "c" * 64})
            self.assertEqual(
                "RBWP7_RUNNER_RESTORE_HASH_MISMATCH",
                raised.exception.code,
            )
        finally:
            client.shutdown()

    def test_adversarial_stdout_and_lifecycle_fail_closed(self) -> None:
        expected = {
            "partial": "RBWP7_RUNNER_INCOMPLETE_FRAME",
            "malformed": "RBWP7_RUNNER_OUTPUT_MALFORMED",
            "duplicate": "RBWP7_RUNNER_OUTPUT_MALFORMED",
            "oversized": "RBWP7_RUNNER_OUTPUT_TOO_LARGE",
            "error": "RBWP7_RUNNER_PROTOCOL_ERROR",
            "extra": "RBWP7_RUNNER_RESPONSE_TYPE_MISMATCH",
            "crash": "RBWP7_RUNNER_EOF",
        }
        for mode, code in expected.items():
            with self.subTest(mode=mode):
                client = self.make_client(
                    mode, maximum_line_bytes=256 if mode == "oversized" else 512
                )
                try:
                    client.start()
                    if mode == "extra":
                        client.negotiate(hello())
                        with self.assertRaises(AdapterFailure) as raised:
                            client.initialize(initialize())
                    else:
                        with self.assertRaises(AdapterFailure) as raised:
                            client.negotiate(hello())
                    self.assertEqual(code, raised.exception.code)
                finally:
                    client.shutdown()

    def test_timeout_and_stderr_overflow_are_typed(self) -> None:
        cases = [
            ("timeout", "RBWP7_RUNNER_RESPONSE_TIMEOUT", {"timeout_seconds": 0.05}),
            ("stderr", "RBWP7_RUNNER_STDERR_OVERFLOW", {"maximum_stderr_bytes": 64}),
        ]
        for mode, code, options in cases:
            with self.subTest(mode=mode):
                client = self.make_client(mode, **options)
                try:
                    client.start()
                    with self.assertRaises(AdapterFailure) as raised:
                        client.negotiate(hello())
                    self.assertEqual(code, raised.exception.code)
                finally:
                    client.shutdown()

    def test_missing_or_mutated_artifact_fails_pre_or_postflight(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            artifact = pathlib.Path(directory) / "artifact.txt"
            artifact.write_text("before", encoding="utf-8")
            missing = RunnerClient([sys.executable, str(FAKE)], [artifact.with_suffix(".missing")])
            with self.assertRaises(AdapterFailure) as raised:
                missing.start()
            self.assertEqual("RBWP7_RUNNER_ARTIFACT_MISSING", raised.exception.code)

            client = RunnerClient(
                [sys.executable, str(FAKE)],
                [sys.executable, FAKE, artifact],
                timeout_seconds=1,
                shutdown_timeout_seconds=1,
            )
            client.start()
            client.negotiate(hello())
            artifact.write_text("after", encoding="utf-8")
            with self.assertRaises(AdapterFailure) as raised:
                client.shutdown()
            self.assertEqual("RBWP7_RUNNER_ARTIFACT_DRIFT", raised.exception.code)


if __name__ == "__main__":
    unittest.main()
