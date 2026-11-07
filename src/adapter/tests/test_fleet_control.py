from __future__ import annotations

import inspect
import os
import pathlib
import sys
import unittest
from types import SimpleNamespace
from unittest import mock


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))
FLEETPY_ROOT = os.environ.get("RIDEBOUND_FLEETPY_ROOT")
if FLEETPY_ROOT:
    sys.path.insert(0, FLEETPY_ROOT)
    from src.fleetctrl.FleetControlBase import FleetControlBase
    from src.misc.globals import VRL_STATES
    from src.misc.init_modules import get_src_fleet_control_modules
    from src.simulation.Legs import VehicleRouteLeg

    from ridebound_fleetpy.fleet_control import (
        RideBoundFleetControl,
        _FinishedLeg,
        _PassengerMarker,
    )
    from ridebound_fleetpy.mapping import FleetPyProtocolMapper
    from ridebound_fleetpy.protocol import (
        OrderedEventBuffer,
        ProtocolRoute,
        ProtocolStop,
    )
else:
    FleetControlBase = object
    RideBoundFleetControl = None

from ridebound_fleetpy.errors import AdapterFailure


class _Request:
    def __init__(self, rid):
        self.rid = rid
        self.nr_pax = 1
        self.pu_time = None

    def get_rid_struct(self):
        return self.rid

    def get_rid(self):
        return self.rid


class _PlanRequest(_Request):
    def __init__(self, rid):
        super().__init__(rid)
        self.o_pos = (1, None, None)
        self.d_pos = (2, None, None)
        self.rq_time = 0
        self.t_pu_earliest = 0
        self.t_pu_latest = 100
        self.max_trip_time = 100
        self.t_do_latest = 200


class _Vehicle:
    def __init__(self):
        self.vid = 0
        self.pos = (0, None, None)
        self.soc = 1.0
        self.pax = []
        self.max_pax = 4
        self.max_parcels = 0
        self.assigned_route = []

    def get_nr_pax_without_currently_boarding(self):
        return 0

    def get_nr_parcels_without_currently_boarding(self):
        return 0

    def compute_soc_consumption(self, _distance):
        return 0

    def compute_soc_charging(self, _power, _duration):
        return 0


class _Routing:
    def __init__(self, node_count=3):
        self.node_count = node_count

    def get_number_network_nodes(self):
        return self.node_count

    @staticmethod
    def return_travel_costs_1to1(origin, destination):
        distance = abs(destination[0] - origin[0])
        return distance, distance, distance * 100


class RunnerServiceBoundsSettingTests(unittest.TestCase):
    def test_late_boarding_key_is_read_exactly_as_the_runner_reads_it(self) -> None:
        from ridebound_fleetpy.session import runner_owns_service_bounds

        path = pathlib.Path("wp4.json")

        self.assertFalse(runner_owns_service_bounds({}, path))
        self.assertFalse(runner_owns_service_bounds({"lateBoarding": "reject"}, path))
        self.assertTrue(runner_owns_service_bounds({"lateBoarding": "record-v1"}, path))
        with self.assertRaises(AdapterFailure) as failure:
            runner_owns_service_bounds({"lateBoarding": "record-v2"}, path)
        self.assertEqual("RBWP7_WP4_LATE_BOARDING_INVALID", failure.exception.code)

    @unittest.skipUnless(FLEETPY_ROOT, "RIDEBOUND_FLEETPY_ROOT is required")
    def test_session_settings_carry_the_key_from_the_wp4_file(self) -> None:
        # The whole path from operator attributes to the settings field, with real files.
        import json
        import tempfile

        from ridebound_fleetpy.session import RideBoundSessionSettings

        repository = ADAPTER_ROOT.parents[1]

        def settings(wp4_extra):
            with tempfile.TemporaryDirectory() as directory:
                root = pathlib.Path(directory)
                runner = root / "runner"
                runner.mkdir()
                (runner / "RideBound.Runner.dll").write_bytes(b"stub")
                commitment = root / "commitment.json"
                commitment.write_text(
                    json.dumps({"policies": [{"policyId": "uniform-v1"}]}),
                    encoding="utf-8",
                )
                wp4 = root / "wp4.json"
                wp4.write_text(
                    json.dumps(
                        {
                            "policyId": "ridebound-hard-vector",
                            "policyVersion": "test-v1",
                            **wp4_extra,
                        }
                    ),
                    encoding="utf-8",
                )
                return RideBoundSessionSettings.from_attributes(
                    {
                        "ridebound_dotnet_path": sys.executable,
                        "ridebound_runner_root": str(runner),
                        "ridebound_commitment_config": str(commitment),
                        "ridebound_wp4_config": str(wp4),
                        "ridebound_fleetpy_root": FLEETPY_ROOT,
                        "ridebound_repository_root": str(repository),
                        "ridebound_commitment_policy_id": "uniform-v1",
                        "ridebound_run_id": "run-1",
                        "ridebound_scenario_id": "scenario-1",
                        "ridebound_master_seed": 7,
                        "ridebound_service_class": "standard",
                    }
                )

        self.assertFalse(settings({}).runner_service_bounds)
        self.assertFalse(settings({"lateBoarding": "reject"}).runner_service_bounds)
        self.assertTrue(settings({"lateBoarding": "record-v1"}).runner_service_bounds)
        with self.assertRaises(AdapterFailure) as failure:
            settings({"lateBoarding": "record-v2"})
        self.assertEqual("RBWP7_WP4_LATE_BOARDING_INVALID", failure.exception.code)
        # ADR-089 GĐ4
        self.assertFalse(settings({"lateBoarding": "record-v1"}).planning_forecast)
        self.assertTrue(
            settings({"lateBoarding": "record-v1", "planningForecast": {"profile": []}}).planning_forecast
        )

    def test_planning_forecast_key_needs_an_object_and_recorded_late_boarding(self) -> None:
        # ADR-089 GĐ4
        from ridebound_fleetpy.session import runner_plans_with_forecast

        path = pathlib.Path("wp4.json")
        recorded = {"lateBoarding": "record-v1"}

        self.assertFalse(runner_plans_with_forecast({}, path))
        self.assertFalse(runner_plans_with_forecast(recorded, path))
        self.assertTrue(runner_plans_with_forecast({**recorded, "planningForecast": {"binMs": 900000}}, path))
        with self.assertRaises(AdapterFailure) as invalid:
            runner_plans_with_forecast({**recorded, "planningForecast": []}, path)
        self.assertEqual("RBWP7_WP4_PLANNING_FORECAST_INVALID", invalid.exception.code)
        for late in ({}, {"lateBoarding": "reject"}):
            with self.subTest(late=late), self.assertRaises(AdapterFailure) as unrecorded:
                runner_plans_with_forecast({**late, "planningForecast": {}}, path)
            self.assertEqual(
                "RBWP7_WP4_PLANNING_FORECAST_NEEDS_RECORDED_LATE_BOARDING",
                unrecorded.exception.code,
            )


@unittest.skipUnless(FLEETPY_ROOT, "RIDEBOUND_FLEETPY_ROOT is required")
class FleetControlContractTests(unittest.TestCase):
    def test_dev_registration_loads_concrete_direct_subclass(self) -> None:
        registration = get_src_fleet_control_modules()["RideBoundFleetControl"]
        self.assertEqual(
            ("ridebound_fleetpy.fleet_control", "RideBoundFleetControl"),
            registration,
        )
        self.assertFalse(inspect.isabstract(RideBoundFleetControl))
        self.assertEqual(set(), RideBoundFleetControl.__abstractmethods__)
        self.assertIs(FleetControlBase, RideBoundFleetControl.__mro__[1])

    def test_protocol_route_advances_mutable_then_frozen_without_version_change(self) -> None:
        pickup = ProtocolStop("pickup", 1, "pickup", 10, 0)
        dropoff = ProtocolStop("drop", 2, "dropOff", 10, 0)
        initial = ProtocolRoute(7, 0, (), (pickup, dropoff))

        after_pickup = RideBoundFleetControl._advance_route(initial, pickup)
        after_dropoff = RideBoundFleetControl._advance_route(after_pickup, dropoff)

        self.assertEqual(7, after_pickup.plan_version)
        self.assertEqual(1, after_pickup.executed_stop_count)
        self.assertEqual((pickup,), after_pickup.frozen_prefix)
        self.assertEqual((dropoff,), after_pickup.mutable_suffix)
        self.assertEqual(2, after_dropoff.executed_stop_count)
        self.assertEqual((pickup, dropoff), after_dropoff.frozen_prefix)
        self.assertEqual((), after_dropoff.mutable_suffix)

    def test_travel_arc_cache_is_generation_scoped(self) -> None:
        class CountingRouting:
            def __init__(self):
                self.calls = 0

            def return_travel_costs_1to1(self, origin, destination):
                self.calls += 1
                distance = abs(destination[0] - origin[0])
                return distance, distance, distance * 100

        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_mapper = FleetPyProtocolMapper()
        control._rb_travel_version = 0
        control._rb_travel_dirty = True
        control._rb_travel_arc_cache = {}
        control._rb_closed = False
        control.routing_engine = CountingRouting()
        control._active_raw_nodes = mock.Mock(return_value={0, 1, 2})

        first = control._build_travel_snapshot()
        second = control._build_travel_snapshot()

        self.assertEqual(6, control.routing_engine.calls)
        self.assertEqual(first["arcs"], second["arcs"])
        self.assertEqual(1, first["version"])
        self.assertEqual(2, second["version"])

        control.inform_network_travel_time_update(1)
        third = control._build_travel_snapshot()

        self.assertEqual(12, control.routing_engine.calls)
        self.assertEqual(first["arcs"], third["arcs"])
        self.assertEqual(3, third["version"])

    def test_complete_snapshot_scope_is_exactly_bounded(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_settings = SimpleNamespace(
            complete_travel_snapshot_maximum_nodes=3
        )
        control._rb_graph_nodes = None
        control.routing_engine = _Routing(node_count=3)

        self.assertEqual({0, 1, 2}, control._active_raw_nodes())

        control._rb_graph_nodes = None
        control._rb_settings = SimpleNamespace(
            complete_travel_snapshot_maximum_nodes=2
        )
        with self.assertRaises(AdapterFailure) as failure:
            control._active_raw_nodes()
        self.assertEqual(
            "RBWP7_COMPLETE_TRAVEL_SNAPSHOT_BOUND_EXCEEDED",
            failure.exception.code,
        )

    def test_plan_stop_round_trip_preserves_membership_constraints_and_locks(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control.rq_dict = {10: _PlanRequest(10)}
        control.sim_vehicles = [_Vehicle()]
        control.routing_engine = _Routing()
        pickup = ProtocolStop("pickup", 1, "pickup", 10, 500)
        dropoff = ProtocolStop("drop", 2, "dropOff", 10, 750)
        route = ProtocolRoute(1, 0, (pickup,), (dropoff,))

        plan = control._fleetpy_plan(0, route, 0)

        self.assertTrue(plan.is_feasible())
        self.assertTrue(plan.is_structural_feasible())
        self.assertEqual([1, 2.5], plan.get_pax_info(10))
        self.assertEqual([10], plan.list_plan_stops[0].get_list_boarding_rids())
        self.assertEqual([10], plan.list_plan_stops[1].get_list_alighting_rids())
        self.assertTrue(plan.list_plan_stops[0].is_locked())
        self.assertFalse(plan.list_plan_stops[1].is_locked())
        self.assertEqual((0.5, None), plan.list_plan_stops[0].get_duration_and_earliest_departure())
        self.assertEqual((0.75, None), plan.list_plan_stops[1].get_duration_and_earliest_departure())

    @staticmethod
    def _plan_control(latest_pickup=100, record_late_boarding=False, planning_forecast=False):
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_settings = SimpleNamespace(
            runner_service_bounds=record_late_boarding,
            planning_forecast=planning_forecast,
        )
        request = _PlanRequest(10)
        request.t_pu_latest = latest_pickup
        control.rq_dict = {10: request}
        control.sim_vehicles = [_Vehicle()]
        control.routing_engine = _Routing()
        return control

    @staticmethod
    def _pickup_and_drop():
        return ProtocolRoute(
            1,
            0,
            (),
            (
                ProtocolStop("pickup", 1, "pickup", 10, 0),
                ProtocolStop("drop", 2, "dropOff", 10, 0),
            ),
        )

    def test_recording_late_boarding_drops_only_the_latest_arrival_bound(self) -> None:
        # ADR-076. The pickup window and the maximum trip stay in both modes; only the
        # latest arrival, which FleetPy anchors on the original window, is dropped.
        for record, expected_arrival in ((False, {10: 200}), (True, {})):
            with self.subTest(record_late_boarding=record):
                control = self._plan_control(record_late_boarding=record)

                plan = control._fleetpy_plan(0, self._pickup_and_drop(), 0)

                earliest, latest, _, _ = plan.list_plan_stops[0].get_boarding_time_constraint_dicts()
                _, _, max_trip, latest_arrival = (
                    plan.list_plan_stops[1].get_boarding_time_constraint_dicts()
                )
                self.assertEqual({10: 0}, earliest)
                self.assertEqual({10: 100}, latest)
                self.assertEqual({10: 100}, max_trip)
                self.assertEqual(expected_arrival, latest_arrival)

    def test_a_late_planned_pickup_is_still_vetoed_while_recording(self) -> None:
        # The Runner validates every changed plan without allowance, so a changed plan with a
        # late pickup means the Runner and FleetPy disagree on travel times. FleetPy keeps
        # catching it in both modes (latest pickup 0 s, reached at 1 s; FleetPy rounds a
        # latest pickup up to whole seconds, so 0.5 s would be on time).
        for record in (False, True):
            with self.subTest(record_late_boarding=record):
                control = self._plan_control(latest_pickup=0, record_late_boarding=record)

                with self.assertRaises(AdapterFailure) as failure:
                    control._fleetpy_plan(0, self._pickup_and_drop(), 0)
                self.assertEqual("RBWP7_FLEETPY_PLAN_INFEASIBLE", failure.exception.code)

    def test_a_late_boarded_drop_gets_the_boarding_plus_maximum_trip_bound(self) -> None:
        # The rider boarded at 240 s, after the latest pickup 100 s; maximum trip 100 s.
        # FleetPy anchors the latest arrival on the original window (100 + 100 = 200 s), so
        # by default a drop at 251 s is infeasible although the ride lasts 11 s. Recording
        # late boarding leaves the bound the Runner uses: boarding + maximum trip = 340 s,
        # which still rejects a drop at 346 s.
        def plan_at(simulation_time, record):
            control = self._plan_control(record_late_boarding=record)
            vehicle = control.sim_vehicles[0]
            onboard = _Request(10)
            onboard.pu_time = 240
            vehicle.pax = [onboard]
            vehicle.pos = (1, None, None)
            pickup = ProtocolStop("pickup", 1, "pickup", 10, 0)
            dropoff = ProtocolStop("drop", 2, "dropOff", 10, 0)
            return control._fleetpy_plan(
                0,
                ProtocolRoute(1, 1, (pickup,), (dropoff,)),
                simulation_time,
            )

        with self.assertRaises(AdapterFailure) as anchored:
            plan_at(250, record=False)
        self.assertEqual("RBWP7_FLEETPY_PLAN_INFEASIBLE", anchored.exception.code)
        plan = plan_at(250, record=True)
        self.assertTrue(plan.is_feasible())
        self.assertEqual([240, 251], plan.get_pax_info(10))
        with self.assertRaises(AdapterFailure) as over:
            plan_at(345, record=True)
        self.assertEqual("RBWP7_FLEETPY_PLAN_INFEASIBLE", over.exception.code)

    def test_under_a_planning_forecast_the_runner_owns_pickup_window_and_maximum_trip(self) -> None:
        # ADR-089 GĐ4 (forecast gate 2026-11-04: FleetPy priced a forecast-planned pickup
        # 0.88 s after its latest time at the current travel times). The plan that FleetPy
        # vetoes without a forecast (latest pickup 0 s, reached at 1 s) is accepted, and both
        # time bounds are empty; the earliest pickup hold stays.
        control = self._plan_control(latest_pickup=0, record_late_boarding=True, planning_forecast=True)

        plan = control._fleetpy_plan(0, self._pickup_and_drop(), 0)

        self.assertTrue(plan.is_feasible())
        earliest, latest, _, _ = plan.list_plan_stops[0].get_boarding_time_constraint_dicts()
        _, _, max_trip, latest_arrival = plan.list_plan_stops[1].get_boarding_time_constraint_dicts()
        self.assertEqual({10: 0}, earliest)
        self.assertEqual({}, latest)
        self.assertEqual({}, max_trip)
        self.assertEqual({}, latest_arrival)

    def test_a_planning_forecast_keeps_the_capacity_check(self) -> None:
        control = self._plan_control(record_late_boarding=True, planning_forecast=True)
        control.rq_dict[10].nr_pax = 5

        with self.assertRaises(AdapterFailure) as failure:
            control._fleetpy_plan(0, self._pickup_and_drop(), 0)
        self.assertEqual("RBWP7_FLEETPY_PLAN_INFEASIBLE", failure.exception.code)

    def test_recording_late_boarding_keeps_the_capacity_check(self) -> None:
        control = self._plan_control(record_late_boarding=True)
        control.rq_dict[10].nr_pax = 5
        route = ProtocolRoute(
            1,
            0,
            (),
            (
                ProtocolStop("pickup", 1, "pickup", 10, 0),
                ProtocolStop("drop", 2, "dropOff", 10, 0),
            ),
        )

        with self.assertRaises(AdapterFailure) as failure:
            control._fleetpy_plan(0, route, 0)
        self.assertEqual("RBWP7_FLEETPY_PLAN_INFEASIBLE", failure.exception.code)

    def test_locked_leg_equivalence_uses_request_identity_not_object_identity(self) -> None:
        old = VehicleRouteLeg(
            VRL_STATES.ROUTE,
            (1, None, None),
            {1: [_Request((7, "sub"))], -1: []},
            locked=True,
        )
        new = VehicleRouteLeg(
            VRL_STATES.ROUTE,
            (1, None, None),
            {1: [_PlanRequest((7, "sub"))], -1: []},
            locked=True,
        )
        changed = VehicleRouteLeg(
            VRL_STATES.ROUTE,
            (2, None, None),
            {1: [_PlanRequest((7, "sub"))], -1: []},
            locked=True,
        )

        self.assertTrue(RideBoundFleetControl._leg_equivalent(old, new))
        self.assertFalse(RideBoundFleetControl._leg_equivalent(old, changed))

    def test_assignment_never_delegates_force_and_rejects_locked_mismatch(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        vehicle = _Vehicle()
        control.rid_to_assigned_vid = {}
        expected = VehicleRouteLeg(
            VRL_STATES.ROUTE,
            (1, None, None),
            {1: [], -1: []},
            locked=True,
        )
        vehicle.assigned_route = [expected]
        plan = mock.Mock()
        plan.get_involved_request_ids.return_value = []
        control._build_VRLs = mock.Mock(return_value=[expected])

        def apply(_self, veh_obj, _plan, _time, **kwargs):
            self.assertFalse(kwargs["force_assign"])
            veh_obj.assigned_route = [expected]

        with mock.patch.object(FleetControlBase, "assign_vehicle_plan", new=apply):
            control.assign_vehicle_plan(vehicle, plan, 1)

        mismatch = VehicleRouteLeg(
            VRL_STATES.ROUTE,
            (2, None, None),
            {1: [], -1: []},
            locked=True,
        )
        control._build_VRLs.return_value = [mismatch]
        with self.assertRaises(AdapterFailure) as locked:
            control.assign_vehicle_plan(vehicle, plan, 2)
        self.assertEqual("RBWP7_LOCKED_LEG_MISMATCH", locked.exception.code)
        with self.assertRaises(AdapterFailure) as forced:
            control.assign_vehicle_plan(vehicle, plan, 2, force_assign=True)
        self.assertEqual("RBWP7_FORCE_ASSIGNMENT_PATH", forced.exception.code)

    def test_finished_leg_is_emitted_before_boarding_without_double_reach(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_mapper = FleetPyProtocolMapper()
        control._rb_mapper.vehicles.register(0)
        control._rb_mapper.requests.register(10)
        control._rb_mapper.node_id(1)
        control._rb_events = OrderedEventBuffer()
        pickup = ProtocolStop("pickup", 1, "pickup", 10, 0)
        control._rb_routes = {0: ProtocolRoute(1, 0, (), (pickup,))}
        control._rb_finished_legs = {
            0: [_FinishedLeg((1, None, None), True, (), ())]
        }
        control._rb_passenger_markers = [
            _PassengerMarker("passengerBoarded", 10, 0, 1)
        ]
        control._rb_semantic_vehicles = set()
        control.sim_time = 1

        control._materialize_callback_events()
        batch = control._rb_events.drain("run", "scenario", 1)

        self.assertEqual(
            ["vehicleReachedStop", "passengerBoarded", "timerTick"],
            [event["eventType"] for event in batch["payload"]["events"]],
        )
        self.assertEqual(1, control._rb_routes[0].executed_stop_count)

    def test_driving_arrival_does_not_complete_pickup_before_boarding(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_mapper = FleetPyProtocolMapper()
        control._rb_mapper.vehicles.register(0)
        control._rb_mapper.requests.register(10)
        control._rb_mapper.node_id(1)
        control._rb_events = OrderedEventBuffer()
        pickup = ProtocolStop("pickup", 1, "pickup", 10, 0)
        control._rb_routes = {0: ProtocolRoute(1, 0, (), (pickup,))}
        control._rb_finished_legs = {
            0: [_FinishedLeg((1, None, None), True, (), ())]
        }
        control._rb_passenger_markers = []
        control._rb_semantic_vehicles = set()
        control.sim_time = 1

        control._materialize_callback_events()
        batch = control._rb_events.drain("run", "scenario", 1)

        self.assertEqual(
            ["timerTick"],
            [event["eventType"] for event in batch["payload"]["events"]],
        )
        self.assertEqual(0, control._rb_routes[0].executed_stop_count)

    def test_large_step_interleaves_pickup_board_dropoff_alight(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_mapper = FleetPyProtocolMapper()
        control._rb_mapper.vehicles.register(0)
        control._rb_mapper.requests.register(10)
        control._rb_mapper.node_id(1)
        control._rb_mapper.node_id(2)
        control._rb_events = OrderedEventBuffer()
        pickup = ProtocolStop("pickup", 1, "pickup", 10, 0)
        dropoff = ProtocolStop("drop", 2, "dropOff", 10, 0)
        control._rb_routes = {
            0: ProtocolRoute(1, 0, (), (pickup, dropoff))
        }
        control._rb_finished_legs = {
            0: [
                _FinishedLeg((1, None, None), True, (), ()),
                _FinishedLeg((2, None, None), True, (), ()),
            ]
        }
        control._rb_passenger_markers = [
            _PassengerMarker("passengerBoarded", 10, 0, 3),
            _PassengerMarker("passengerAlighted", 10, 0, 3),
        ]
        control._rb_semantic_vehicles = set()
        control.sim_time = 3

        control._materialize_callback_events()
        batch = control._rb_events.drain("run", "scenario", 3)

        self.assertEqual(
            [
                "vehicleReachedStop",
                "passengerBoarded",
                "vehicleReachedStop",
                "passengerAlighted",
                "timerTick",
            ],
            [event["eventType"] for event in batch["payload"]["events"]],
        )
        self.assertEqual(2, control._rb_routes[0].executed_stop_count)

    def test_zero_distance_service_is_ordered_before_later_driving_leg(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_mapper = FleetPyProtocolMapper()
        control._rb_mapper.vehicles.register(0)
        control._rb_mapper.requests.register(10)
        control._rb_mapper.requests.register(11)
        control._rb_mapper.node_id(2)
        control._rb_mapper.node_id(81)
        control._rb_events = OrderedEventBuffer()
        pickup = ProtocolStop("pickup-10", 2, "pickup", 10, 0)
        dropoff = ProtocolStop("dropoff-11", 81, "dropOff", 11, 0)
        control._rb_routes = {
            0: ProtocolRoute(1, 0, (), (pickup, dropoff))
        }
        control._rb_finished_legs = {
            0: [
                _FinishedLeg((2, None, None), False, (10,), ()),
                _FinishedLeg((81, None, None), True, (), ()),
            ]
        }
        control._rb_passenger_markers = [
            _PassengerMarker("passengerBoarded", 10, 0, 3)
        ]
        control._rb_semantic_vehicles = set()
        control.sim_time = 3

        control._materialize_callback_events()
        batch = control._rb_events.drain("run", "scenario", 3)

        self.assertEqual(
            [
                "vehicleReachedStop",
                "passengerBoarded",
                "timerTick",
            ],
            [event["eventType"] for event in batch["payload"]["events"]],
        )
        self.assertEqual(1, control._rb_routes[0].executed_stop_count)
        self.assertEqual(81, control._rb_routes[0].remaining_stops[0].raw_node)

    def test_passenger_callback_cannot_be_retimestamped_to_coarse_batch_end(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_mapper = FleetPyProtocolMapper()
        control._rb_mapper.vehicles.register(0)
        control._rb_mapper.requests.register(10)
        control._rb_mapper.node_id(1)
        control._rb_events = OrderedEventBuffer()
        pickup = ProtocolStop("pickup", 1, "pickup", 10, 0)
        control._rb_routes = {0: ProtocolRoute(1, 0, (), (pickup,))}
        control._rb_finished_legs = {
            0: [_FinishedLeg((1, None, None), True, (), ())]
        }
        control._rb_passenger_markers = [
            _PassengerMarker("passengerBoarded", 10, 0, 2)
        ]
        control._rb_semantic_vehicles = set()
        control.sim_time = 3

        with self.assertRaises(AdapterFailure) as failure:
            control._materialize_callback_events()

        self.assertEqual(
            "RBWP7_CALLBACK_BATCH_TIME_MISMATCH",
            failure.exception.code,
        )

    def test_foreign_finished_service_leg_cannot_be_ignored(self) -> None:
        control = RideBoundFleetControl.__new__(RideBoundFleetControl)
        control._rb_mapper = FleetPyProtocolMapper()
        control._rb_mapper.vehicles.register(0)
        control._rb_mapper.requests.register(10)
        control._rb_mapper.requests.register(11)
        control._rb_mapper.node_id(1)
        control._rb_events = OrderedEventBuffer()
        pickup = ProtocolStop("pickup", 1, "pickup", 10, 0)
        control._rb_routes = {0: ProtocolRoute(1, 0, (), (pickup,))}
        control._rb_finished_legs = {
            0: [_FinishedLeg((1, None, None), False, (11,), ())]
        }
        control._rb_passenger_markers = []
        control._rb_semantic_vehicles = set()
        control.sim_time = 3

        with self.assertRaises(AdapterFailure) as failure:
            control._materialize_callback_events()

        self.assertEqual(
            "RBWP7_FINISHED_SERVICE_WITHOUT_ROUTE",
            failure.exception.code,
        )


if __name__ == "__main__":
    unittest.main()
