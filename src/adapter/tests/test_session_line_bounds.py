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
        "policy-id", "policy-version", "a" * 64, "b" * 40,
        maximum_line_bytes=maximum_line_bytes,
    )


class FakeClient:
    def __init__(self, command, **options) -> None:
        self.command = command
        self.options = options


def runner_bound(protocol: session.RideBoundProtocolSession) -> int:
    command = list(protocol._client.command)
    return int(command[command.index("--maximum-line-bytes") + 1])


class SessionLineBoundsTests(unittest.TestCase):
    """ADR-089 S3: one required operator setting gives both frame bounds for a full-node Via run."""

    def test_the_setting_is_required_and_within_the_runner_range(self) -> None:
        self.assertEqual(64 * MIB, session._maximum_line_bytes({"ridebound_maximum_line_bytes": 64 * MIB}))
        self.assertEqual(1024 * MIB, session._maximum_line_bytes({"ridebound_maximum_line_bytes": 1024 * MIB}))
        with self.assertRaises(AdapterFailure) as raised:
            session._maximum_line_bytes({})
        self.assertEqual("RBWP7_SETTING_INTEGER_INVALID", raised.exception.code)
        for value in (True, "67108864", MIB, 64 * MIB - 1, 1024 * MIB + 1):
            with self.assertRaises(AdapterFailure) as raised:
                session._maximum_line_bytes({"ridebound_maximum_line_bytes": value})
            self.assertEqual("RBWP7_SETTING_INTEGER_INVALID", raised.exception.code)

    def test_the_setting_reaches_the_runner_and_both_client_bounds(self) -> None:
        protocol = session.RideBoundProtocolSession(settings(1024 * MIB), FakeClient)
        self.assertEqual(1024 * MIB, runner_bound(protocol))
        self.assertEqual(1024 * MIB, protocol._client.options["maximum_input_line_bytes"])
        self.assertEqual(1024 * MIB, protocol._client.options["maximum_output_line_bytes"])


class TravelSnapshotBoundTests(unittest.TestCase):
    """The travel snapshot bound is the network's node count; 0 or a missing key asked for the removed sparse mode."""

    def test_the_sparse_travel_snapshot_is_refused(self) -> None:
        key = "ridebound_complete_travel_snapshot_maximum_nodes"
        for values in ({}, {key: 0}):
            with self.subTest(values=values):
                with self.assertRaises(AdapterFailure) as raised:
                    session._snapshot_node_count(values)
                self.assertEqual("RBWP7_SPARSE_TRAVEL_SNAPSHOT_REMOVED", raised.exception.code)
                self.assertIn("removed; see commit ebca912", raised.exception.detail)
        self.assertEqual(2, session._snapshot_node_count({key: 2}))


if __name__ == "__main__":
    unittest.main()
