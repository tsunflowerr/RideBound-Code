"""The 3 stopped G2 jobs (AMENDMENTS A7), reduced to the adapter step that failed.

A vehicle has just arrived at node 1 and is boarding rider A there. FleetPy holds that zero-duration boarding leg
locked for the rest of the step (Vehicles.py:226-228), and the Runner already counts A's pickup as executed. In the
same step the Runner sends a new route whose first stop is another pickup (rider W) at the same node: the G2 receiver
of the stopped jobs. Before the fix the adapter raised RBWP7_LOCKED_LEG_MISMATCH. The fix keeps FleetPy's own
in-progress stop (a copy of veh_plans[vid].list_plan_stops[0], locked, started_at set) in front of the Runner's route,
which is FleetPy's convention for re-planning a vehicle mid-boarding (VehiclePlan.update_plan step 2), and only when
the plan would otherwise fail that check.
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

A, W = 10, 11
NOW = 5


@unittest.skipUnless(FLEETPY_ROOT, "RIDEBOUND_FLEETPY_ROOT is required")
class InProgressServiceLegTests(unittest.TestCase):
    def _boarding_vehicle(self, duration_ms=0, locked=True, latest_pickup=None):
        """Vehicle 0 at node 1, boarding A there now; veh_plans[0] as FleetPy left it after the status update."""
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control.rq_dict = {A: _PlanRequest(A), W: _PlanRequest(W)}
        if latest_pickup is not None:
            control.rq_dict[A].t_pu_latest = latest_pickup
        vehicle = _Vehicle()
        vehicle.pos = (1, None, None)
        boarded = _Request(A)
        boarded.pu_time = NOW
        vehicle.pax = [boarded]
        vehicle.cl_start_time = NOW
        control.sim_vehicles = [vehicle]
        control.routing_engine = _Routing()
        control.begin_approach_buffer_time = 0
        control._active_charging_processes = {}
        control.rid_to_assigned_vid = {A: 0}
        pickup_a = ProtocolStop("pickup-a", 1, "pickup", A, duration_ms)
        drop_a = ProtocolStop("drop-a", 2, "dropOff", A, 0)
        head = control._fleetpy_stop(pickup_a, locked=locked)
        previous = VehiclePlan(vehicle, NOW, control.routing_engine, [head, control._fleetpy_stop(drop_a, locked=False)])
        previous.list_plan_stops[0].set_started_at(NOW)
        control.veh_plans = {0: previous}
        vehicle.assigned_route = control._build_VRLs(previous, vehicle, NOW)
        self.assertEqual(VRL_STATES.BOARDING, vehicle.assigned_route[0].status)
        self.assertEqual(locked, vehicle.assigned_route[0].locked)
        return control, vehicle, pickup_a, drop_a

    @staticmethod
    def _g2_route(pickup_a, drop_a):
        """The Runner's route after the move: A's pickup executed, W picked up at the same node next."""
        return ProtocolRoute(
            2,
            1,
            (pickup_a,),
            (
                ProtocolStop("pickup-w", 1, "pickup", W, 0),
                ProtocolStop("drop-a", 2, "dropOff", A, 0),
                ProtocolStop("drop-w", 2, "dropOff", W, 0),
            ),
        )

    def test_a_pickup_at_the_node_being_served_keeps_the_in_progress_stop_first(self) -> None:
        control, vehicle, pickup_a, drop_a = self._boarding_vehicle()

        plan = control._fleetpy_plan(0, self._g2_route(pickup_a, drop_a), NOW)

        self.assertEqual([A], plan.list_plan_stops[0].get_list_boarding_rids())
        self.assertTrue(plan.list_plan_stops[0].is_locked())
        self.assertEqual(NOW, plan.list_plan_stops[0].get_started_at())
        self.assertEqual([W], plan.list_plan_stops[1].get_list_boarding_rids())
        self.assertEqual(4, len(plan.list_plan_stops))
        self.assertTrue(plan.is_feasible())
        new_legs = control._build_VRLs(plan, vehicle, NOW)
        self.assertTrue(RideBoundFleetControl._leg_equivalent(vehicle.assigned_route[0], new_legs[0]))

        def apply(_self, veh_obj, _plan, _time, **_kwargs):
            veh_obj.assigned_route = control._build_VRLs(_plan, veh_obj, _time)

        with mock.patch.object(FleetControlBase, "assign_vehicle_plan", new=apply):
            control.assign_vehicle_plan(vehicle, plan, NOW)

    def test_without_the_fix_conditions_the_check_still_fails_closed(self) -> None:
        # A service leg with a real duration is not an instantaneous stop the Runner can have finished.
        control, vehicle, pickup_a, drop_a = self._boarding_vehicle(duration_ms=30_000)
        plan = control._fleetpy_plan(0, self._g2_route(pickup_a, drop_a), NOW)
        self.assertEqual([W], plan.list_plan_stops[0].get_list_boarding_rids())
        with mock.patch.object(FleetControlBase, "assign_vehicle_plan", new=lambda *a, **k: None):
            with self.assertRaises(AdapterFailure) as failure:
                control.assign_vehicle_plan(vehicle, plan, NOW)
        self.assertEqual("RBWP7_LOCKED_LEG_MISMATCH", failure.exception.code)

        # The Runner has not executed A's pickup: the stop being served is not finished on the Runner side.
        control, vehicle, pickup_a, drop_a = self._boarding_vehicle()
        unexecuted = ProtocolRoute(
            2,
            0,
            (),
            (ProtocolStop("pickup-w", 1, "pickup", W, 0), pickup_a, drop_a, ProtocolStop("drop-w", 2, "dropOff", W, 0)),
        )
        plan = control._fleetpy_plan(0, unexecuted, NOW)
        self.assertEqual([W], plan.list_plan_stops[0].get_list_boarding_rids())

    def test_a_late_boarding_at_the_served_stop_does_not_make_the_plan_infeasible(self) -> None:
        # As in the stopped jobs: A boards a few seconds after its latest pickup (recorded by the Runner,
        # ADR-076). The copy drops the started stop's latest-time bounds; FleetPy's stored stop keeps them.
        control, vehicle, pickup_a, drop_a = self._boarding_vehicle(latest_pickup=NOW - 3)

        plan = control._fleetpy_plan(0, self._g2_route(pickup_a, drop_a), NOW)

        self.assertTrue(plan.is_feasible())
        self.assertEqual([A], plan.list_plan_stops[0].get_list_boarding_rids())
        self.assertEqual({}, plan.list_plan_stops[0].latest_pickup_time_dict)
        self.assertEqual({A: NOW - 3}, control.veh_plans[0].list_plan_stops[0].latest_pickup_time_dict)
        new_legs = control._build_VRLs(plan, vehicle, NOW)
        self.assertTrue(RideBoundFleetControl._leg_equivalent(vehicle.assigned_route[0], new_legs[0]))

    def test_a_vehicle_without_a_locked_leg_gets_exactly_the_runner_route(self) -> None:
        control, vehicle, pickup_a, drop_a = self._boarding_vehicle(locked=False)

        plan = control._fleetpy_plan(0, self._g2_route(pickup_a, drop_a), NOW)

        self.assertEqual([W], plan.list_plan_stops[0].get_list_boarding_rids())
        self.assertEqual(3, len(plan.list_plan_stops))


if __name__ == "__main__":
    unittest.main()
