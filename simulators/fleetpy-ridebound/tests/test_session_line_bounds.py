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
        "run-1", "scenario-1", 1, "service", "policy", 60, 0, None,
        "policy-id", "policy-version", "a" * 64, "b" * 40, (path,),
        maximum_line_bytes=maximum_line_bytes,
    )


class FakeClient:
    def __init__(self, command, artifacts, **options) -> None:
        self.command = command
        self.options = options


class SessionLineBoundsTests(unittest.TestCase):
    """ADR-089 S3: one operator setting raises both frame bounds for a full-node Via run."""

    def test_the_setting_is_optional_and_bounded_like_the_runner(self) -> None:
        self.assertIsNone(session._maximum_line_bytes({}))
        self.assertEqual(MIB, session._maximum_line_bytes({"ridebound_maximum_line_bytes": MIB}))
        self.assertEqual(1024 * MIB, session._maximum_line_bytes({"op_ridebound_maximum_line_bytes": 1024 * MIB}))
        for value in (True, "67108864", MIB - 1, 1024 * MIB + 1):
            with self.assertRaises(AdapterFailure) as raised:
                session._maximum_line_bytes({"ridebound_maximum_line_bytes": value})
            self.assertEqual("RBWP7_SETTING_INTEGER_INVALID", raised.exception.code)

    def test_the_setting_reaches_the_runner_and_both_client_bounds(self) -> None:
        protocol = session.RideBoundProtocolSession(settings(1024 * MIB), FakeClient)
        command = list(protocol._client.command)
        self.assertEqual(str(1024 * MIB), command[command.index("--maximum-line-bytes") + 1])
        self.assertEqual(1024 * MIB, protocol._client.options["maximum_input_line_bytes"])
        self.assertEqual(1024 * MIB, protocol._client.options["maximum_output_line_bytes"])

    def test_without_the_setting_the_shipped_bounds_and_launcher_patches_apply(self) -> None:
        protocol = session.RideBoundProtocolSession(settings(None), FakeClient)
        command = list(protocol._client.command)
        self.assertEqual(str(64 * MIB), command[command.index("--maximum-line-bytes") + 1])
        self.assertEqual(64 * MIB, protocol._client.options["maximum_output_line_bytes"])

        # bridge_launcher_via.py raises the output bound by patching the module constant.
        original = session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES
        session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = 512 * MIB
        try:
            patched = session.RideBoundProtocolSession(settings(None), FakeClient)
            self.assertEqual(512 * MIB, patched._client.options["maximum_output_line_bytes"])
            self.assertEqual(64 * MIB, patched._client.options["maximum_input_line_bytes"])
        finally:
            session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = original


if __name__ == "__main__":
    unittest.main()
