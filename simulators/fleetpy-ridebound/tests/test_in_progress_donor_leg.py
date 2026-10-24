"""The stopped phase-1 G2 job (PHASE1-LOG P3), reduced to the adapter step that failed.

A G2 donor has just arrived at node 1, the drop node of its onboard rider A, and FleetPy has started that zero-duration
service leg and keeps it locked for the rest of the step (Vehicles.py:226-228). The event batch of that instant has no
vehicleReachedStop/passengerAlighted for it yet, so the Runner still holds A's drop as a remaining, unlocked stop. In
the same step the Runner moves a waiting rider W away from this vehicle, so it sends a new route that starts with that
same drop. FleetPy's locked leg and the first leg built from the Runner's route are equal in every field but `locked`,
and the adapter raised RBWP7_LOCKED_LEG_MISMATCH. The fix uses FleetPy's own started stop in place of the Runner's first
stop (the same stop), and only where the plan would otherwise fail that check.
"""

from __future__ import annotations

import os
import pathlib
import sys
import unittest
from unittest import mock

ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))
sys.path.insert(0, str(pathlib.Path(__file__).parent))
FLEETPY_ROOT = os.environ.get("RIDEBOUND_FLEETPY_ROOT")
if FLEETPY_ROOT:
    sys.path.insert(0, FLEETPY_ROOT)
    from src.fleetctrl.FleetControlBase import FleetControlBase
    from src.fleetctrl.planning.VehiclePlan import VehiclePlan
    from src.misc.globals import VRL_STATES

    from ridebound_fleetpy.fleet_control import RideBoundFleetControl
    from ridebound_fleetpy.protocol import ProtocolRoute, ProtocolStop
    from test_fleet_control import _PlanRequest, _Request, _Routing, _Vehicle

from ridebound_fleetpy.errors import AdapterFailure

A, B, W = 10, 12, 11
NOW = 5


@unittest.skipUnless(FLEETPY_ROOT, "RIDEBOUND_FLEETPY_ROOT is required")
class InProgressDonorLegTests(unittest.TestCase):
    def _alighting_donor(self, duration_ms=0, locked=True):
        """Vehicle 0 at node 1 with A and B onboard, A alighting there now; W (waiting) was to be picked up at node 3.

        veh_plans[0] is as FleetPy left it after the status update: the drop of A first, locked, started now."""
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control.rq_dict = {A: _PlanRequest(A), B: _PlanRequest(B), W: _PlanRequest(W)}
        for rid, origin, destination in ((A, 0, 1), (B, 0, 2), (W, 3, 4)):
            control.rq_dict[rid].o_pos = (origin, None, None)
            control.rq_dict[rid].d_pos = (destination, None, None)
        vehicle = _Vehicle()
        vehicle.pos = (1, None, None)
        riders = []
        for rid in (A, B):
            onboard = _Request(rid)
            onboard.pu_time = 0
            riders.append(onboard)
        vehicle.pax = riders
        vehicle.cl_start_time = NOW
        control.sim_vehicles = [vehicle]
        control.routing_engine = _Routing(node_count=5)
        control.begin_approach_buffer_time = 0
        control._active_charging_processes = {}
        control.rid_to_assigned_vid = {A: 0, B: 0, W: 0}
        pickup_a = ProtocolStop("pickup-a", 0, "pickup", A, 0)
        pickup_b = ProtocolStop("pickup-b", 0, "pickup", B, 0)
        drop_a = ProtocolStop("drop-a", 1, "dropOff", A, duration_ms)
        drop_b = ProtocolStop("drop-b", 2, "dropOff", B, 0)
        pickup_w = ProtocolStop("pickup-w", 3, "pickup", W, 0)
        drop_w = ProtocolStop("drop-w", 4, "dropOff", W, 0)
        head = control._fleetpy_stop(drop_a, locked=locked)
        previous = VehiclePlan(vehicle, NOW, control.routing_engine, [
            head, control._fleetpy_stop(drop_b, locked=False), control._fleetpy_stop(pickup_w, locked=False),
            control._fleetpy_stop(drop_w, locked=False)])
        previous.list_plan_stops[0].set_started_at(NOW)
        control.veh_plans = {0: previous}
        vehicle.assigned_route = control._build_VRLs(previous, vehicle, NOW)
        self.assertEqual(VRL_STATES.BOARDING, vehicle.assigned_route[0].status)
        self.assertEqual(locked, vehicle.assigned_route[0].locked)
        return control, vehicle, (pickup_a, pickup_b, drop_a, drop_b)

    @staticmethod
    def _donor_route(pickup_a, pickup_b, drop_a, drop_b):
        """The Runner's route after W moved away: the pickups executed, A's drop still remaining (not yet reported)."""
        return ProtocolRoute(3, 2, (pickup_a, pickup_b), (drop_a, drop_b))

    def test_the_stop_being_served_but_not_yet_reported_is_fleetpys_started_stop(self) -> None:
        control, vehicle, stops = self._alighting_donor()

        plan = control._fleetpy_plan(0, self._donor_route(*stops), NOW)

        self.assertEqual(2, len(plan.list_plan_stops))           # the same stop, not a second one
        self.assertEqual([A], plan.list_plan_stops[0].get_list_alighting_rids())
        self.assertTrue(plan.list_plan_stops[0].is_locked())
        self.assertEqual(NOW, plan.list_plan_stops[0].get_started_at())
        self.assertEqual([B], plan.list_plan_stops[1].get_list_alighting_rids())
        self.assertTrue(plan.is_feasible())
        new_legs = control._build_VRLs(plan, vehicle, NOW)
        self.assertTrue(RideBoundFleetControl._leg_equivalent(vehicle.assigned_route[0], new_legs[0]))

        def apply(_self, veh_obj, _plan, _time, **_kwargs):
            veh_obj.assigned_route = control._build_VRLs(_plan, veh_obj, _time)

        with mock.patch.object(FleetControlBase, "assign_vehicle_plan", new=apply):
            control.assign_vehicle_plan(vehicle, plan, NOW)

    def test_a_different_first_stop_still_fails_closed(self) -> None:
        # The Runner's route starts with B's drop: not the stop being served, so no substitution.
        control, vehicle, (pickup_a, pickup_b, drop_a, drop_b) = self._alighting_donor()
        route = ProtocolRoute(3, 2, (pickup_a, pickup_b), (drop_b, drop_a))
        plan = control._fleetpy_plan(0, route, NOW)
        self.assertEqual([B], plan.list_plan_stops[0].get_list_alighting_rids())
        with mock.patch.object(FleetControlBase, "assign_vehicle_plan", new=lambda *a, **k: None):
            with self.assertRaises(AdapterFailure) as failure:
                control.assign_vehicle_plan(vehicle, plan, NOW)
        self.assertEqual("RBWP7_LOCKED_LEG_MISMATCH", failure.exception.code)

    def test_a_service_leg_with_a_duration_still_fails_closed(self) -> None:
        control, vehicle, stops = self._alighting_donor(duration_ms=30_000)
        plan = control._fleetpy_plan(0, self._donor_route(*stops), NOW)
        self.assertFalse(plan.list_plan_stops[0].is_locked())
        with mock.patch.object(FleetControlBase, "assign_vehicle_plan", new=lambda *a, **k: None):
            with self.assertRaises(AdapterFailure) as failure:
                control.assign_vehicle_plan(vehicle, plan, NOW)
        self.assertEqual("RBWP7_LOCKED_LEG_MISMATCH", failure.exception.code)

    def test_a_vehicle_without_a_locked_leg_gets_exactly_the_runner_route(self) -> None:
        control, vehicle, stops = self._alighting_donor(locked=False)
        plan = control._fleetpy_plan(0, self._donor_route(*stops), NOW)
        self.assertFalse(plan.list_plan_stops[0].is_locked())
        self.assertIsNone(plan.list_plan_stops[0].get_started_at())
        self.assertEqual(2, len(plan.list_plan_stops))


if __name__ == "__main__":
    unittest.main()
