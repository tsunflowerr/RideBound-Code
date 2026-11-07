from __future__ import annotations

import pathlib
import sys
import unittest
from dataclasses import dataclass


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

from ridebound_fleetpy.errors import AdapterFailure
from ridebound_fleetpy.relocation import (
    Relocation,
    infer_waiting_rider_relocations,
    route_riders,
)
from ridebound_fleetpy.session import runner_relocates_waiting_riders

A, B, C = "veh-a", "veh-b", "veh-c"
W, X, NEW = "rider-w", "rider-x", "rider-new"


@dataclass(frozen=True)
class _Stop:
    raw_request: object


def _infer(
    before,
    after,
    assigned=None,
    states=None,
    promised=None,
    accepted=frozenset(),
):
    return infer_waiting_rider_relocations(
        {vehicle: frozenset(riders) for vehicle, riders in before.items()},
        {vehicle: frozenset(riders) for vehicle, riders in after.items()},
        assigned if assigned is not None else {W: A, X: A},
        states if states is not None else {W: "confirmed", X: "confirmed"},
        promised if promised is not None else {W: B},
        frozenset(accepted),
    )


class WaitingRiderRelocationInferenceTests(unittest.TestCase):
    def test_a_confirmed_rider_moving_from_donor_to_receiver_is_one_move(self) -> None:
        moves = _infer({A: {W, X}, B: set()}, {A: {X}, B: {W}})

        self.assertEqual((Relocation(W, A, B),), moves)

    def test_no_move_without_a_vehicle_change(self) -> None:
        self.assertEqual((), _infer({A: {W, X}}, {A: {X, W}}))
        # A newly accepted request entering a plan is an acceptance, not a move.
        self.assertEqual(
            (),
            _infer({A: {W}}, {A: {W, NEW}}, assigned={W: A}, accepted={NEW}),
        )

    def test_an_offered_or_boarded_rider_never_moves(self) -> None:
        for state in ("offered", "boarded", "pending", "cancel-pending"):
            with self.subTest(state=state):
                with self.assertRaises(AdapterFailure) as failure:
                    _infer({A: {W}, B: set()}, {A: set(), B: {W}}, states={W: state})
                self.assertEqual("RBWP7_RELOCATION_INVALID", failure.exception.code)

    def test_a_move_needs_the_donor_plan_without_the_rider_and_a_matching_promise(self) -> None:
        cases = {
            "donor plan unchanged": ({A: {W}, B: set()}, {B: {W}}, {W: B}),
            "donor keeps the rider": ({A: {W}, B: set()}, {A: {W}, B: {W}}, {W: B}),
            "no promise": ({A: {W}, B: set()}, {A: set(), B: {W}}, {}),
            "promise names the donor": ({A: {W}, B: set()}, {A: set(), B: {W}}, {W: A}),
        }
        for name, (before, after, promised) in cases.items():
            with self.subTest(name):
                with self.assertRaises(AdapterFailure) as failure:
                    _infer(before, after, assigned={W: A}, states={W: "confirmed"}, promised=promised)
                self.assertEqual("RBWP7_RELOCATION_INVALID", failure.exception.code)

    def test_at_most_one_rider_moves(self) -> None:
        with self.assertRaises(AdapterFailure) as failure:
            _infer(
                {A: {W, X}, B: set(), C: set()},
                {A: set(), B: {W}, C: {X}},
                promised={W: B, X: C},
            )
        self.assertEqual("RBWP7_RELOCATION_COUNT", failure.exception.code)

    def test_an_active_rider_dropped_without_a_move_fails_closed(self) -> None:
        for state in ("offered", "confirmed", "boarded"):
            with self.subTest(state=state):
                with self.assertRaises(AdapterFailure) as failure:
                    _infer({A: {W, X}}, {A: {X}}, states={W: state, X: "confirmed"}, promised={})
                self.assertEqual("RBWP7_RIDER_DROPPED", failure.exception.code)
        # A cancelled or completed rider leaves its plan legitimately.
        for state in ("cancel-pending", "completed", "closed"):
            with self.subTest(state=state):
                self.assertEqual((), _infer({A: {W, X}}, {A: {X}}, states={W: state, X: "confirmed"}, promised={}))

    def test_route_riders_ignores_waypoints(self) -> None:
        self.assertEqual(
            frozenset({W, X}),
            route_riders([_Stop(W), _Stop(None), _Stop(X), _Stop(W)]),
        )


class WaitingRiderRelocationSettingTests(unittest.TestCase):
    def test_the_wp4_key_turns_the_check_on_only_when_declared(self) -> None:
        path = pathlib.Path("wp4.json")

        self.assertFalse(runner_relocates_waiting_riders({}, path))
        self.assertFalse(runner_relocates_waiting_riders({"candidateGeneration": {}}, path))
        self.assertTrue(
            runner_relocates_waiting_riders(
                {"candidateGeneration": {"waitingRiderRelocation": {}}},
                path,
            )
        )
        self.assertTrue(
            runner_relocates_waiting_riders(
                {"candidateGeneration": {"waitingRiderRelocation": {"maximumTargetVehicles": 8}}},
                path,
            )
        )
        with self.assertRaises(AdapterFailure) as failure:
            runner_relocates_waiting_riders(
                {"candidateGeneration": {"waitingRiderRelocation": True}},
                path,
            )
        self.assertEqual("RBWP7_WP4_RELOCATION_INVALID", failure.exception.code)


if __name__ == "__main__":
    unittest.main()
