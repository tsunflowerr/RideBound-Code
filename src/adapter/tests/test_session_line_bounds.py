from __future__ import annotations

import pathlib
import sys
import unittest


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

from ridebound_fleetpy import session  # noqa: E402
from ridebound_fleetpy.errors import AdapterFailure  # noqa: E402

MIB = 1024 * 1024


def settings(maximum_line_bytes: int | None) -> session.RideBoundSessionSettings:
    path = pathlib.Path("unused")
    return session.RideBoundSessionSettings(
        path, path, path, path, path, path, path,
        "run-1", "scenario-1", 1, "service", "policy", 60, 2, None,
        "policy-id", "policy-version", "a" * 64, "b" * 40, (path,),
        maximum_line_bytes=maximum_line_bytes,
    )


class FakeClient:
    def __init__(self, command, artifacts, **options) -> None:
        self.command = command
        self.options = options


def runner_bound(protocol: session.RideBoundProtocolSession) -> int:
    command = list(protocol._client.command)
    return int(command[command.index("--maximum-line-bytes") + 1])


class SessionLineBoundsTests(unittest.TestCase):
    """ADR-089 S3: one operator setting raises both frame bounds for a full-node Via run."""

    def test_the_setting_is_optional_and_can_only_raise_the_shipped_bound(self) -> None:
        self.assertIsNone(session._maximum_line_bytes({}))
        self.assertEqual(64 * MIB, session._maximum_line_bytes({"ridebound_maximum_line_bytes": 64 * MIB}))
        self.assertEqual(1024 * MIB, session._maximum_line_bytes({"ridebound_maximum_line_bytes": 1024 * MIB}))
        with self.assertRaises(AdapterFailure) as raised:
            session._maximum_line_bytes({"op_ridebound_maximum_line_bytes": 1024 * MIB})
        self.assertEqual("RBWP7_SETTING_PREFIX_REMOVED", raised.exception.code)
        for value in (True, "67108864", MIB, 64 * MIB - 1, 1024 * MIB + 1):
            with self.assertRaises(AdapterFailure) as raised:
                session._maximum_line_bytes({"ridebound_maximum_line_bytes": value})
            self.assertEqual("RBWP7_SETTING_INTEGER_INVALID", raised.exception.code)

    def test_the_setting_reaches_the_runner_and_both_client_bounds(self) -> None:
        protocol = session.RideBoundProtocolSession(settings(1024 * MIB), FakeClient)
        self.assertEqual(1024 * MIB, runner_bound(protocol))
        self.assertEqual(1024 * MIB, protocol._client.options["maximum_input_line_bytes"])
        self.assertEqual(1024 * MIB, protocol._client.options["maximum_output_line_bytes"])

    def test_without_the_setting_the_shipped_bounds_apply(self) -> None:
        protocol = session.RideBoundProtocolSession(settings(None), FakeClient)
        self.assertEqual(64 * MIB, runner_bound(protocol))
        self.assertEqual(64 * MIB, protocol._client.options["maximum_input_line_bytes"])
        self.assertEqual(64 * MIB, protocol._client.options["maximum_output_line_bytes"])

    def test_launcher_patches_still_apply_and_a_setting_never_lowers_them(self) -> None:
        # bridge_launcher_via.py raises the output bound by patching the module constant; a
        # client made later (restart) reads the constant at that time, as before.
        original = session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES
        try:
            plain = session.RideBoundProtocolSession(settings(None), FakeClient)
            raised = session.RideBoundProtocolSession(settings(128 * MIB), FakeClient)
            session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = 512 * MIB
            self.assertEqual(512 * MIB, plain._new_client().options["maximum_output_line_bytes"])
            self.assertEqual(512 * MIB, raised._new_client().options["maximum_output_line_bytes"])
            self.assertEqual(128 * MIB, raised._new_client().options["maximum_input_line_bytes"])
        finally:
            session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = original


if __name__ == "__main__":
    unittest.main()
