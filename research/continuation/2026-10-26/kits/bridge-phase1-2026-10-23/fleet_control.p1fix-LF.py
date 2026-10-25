from __future__ import annotations

import atexit
import hashlib
import math
from collections import Counter, defaultdict
from dataclasses import dataclass
from decimal import Decimal
from typing import Any, Iterable

from src.fleetctrl.FleetControlBase import FleetControlBase
from src.fleetctrl.planning.PlanRequest import PlanRequest
from src.fleetctrl.planning.VehiclePlan import (
    BoardingPlanStop,
    RoutingTargetPlanStop,
    VehiclePlan,
)
from src.misc.globals import G_DRIVING_STATUS, VRL_STATES
from src.simulation.Offers import TravellerOffer

from .errors import AdapterFailure
from .mapping import (
    FleetPyProtocolMapper,
    canonical_json_bytes,
    seconds_to_milliseconds,
)
from .protocol import (
    OrderedEventBuffer,
    ParsedDecision,
    ProtocolRoute,
    ProtocolStop,
    parse_decision,
)
from .relocation import infer_waiting_rider_relocations, route_riders
from .session import RideBoundProtocolSession, RideBoundSessionSettings


PHASE_LIFECYCLE = 30
PHASE_REACHED_STOP = 40
# Stop completion and its passenger transition form one ordered semantic stream.
# Giving both the same phase preserves the deterministic append order even when
# one FleetPy time step crosses several pickup/drop-off service legs.
PHASE_PASSENGER = PHASE_REACHED_STOP
PHASE_VEHICLE = 70


def _fail(code: str, path: str, detail: str) -> AdapterFailure:
    return AdapterFailure(code, path, detail)


@dataclass(frozen=True)
class _PassengerMarker:
    event_type: str
    raw_request: Any
    raw_vehicle: Any
    simulation_time: Any


@dataclass(frozen=True)
class _FinishedLeg:
    raw_position: Any
    is_driving: bool
    boarding_requests: tuple[Any, ...]
    alighting_requests: tuple[Any, ...]


class RideBoundFleetControl(FleetControlBase):
    """Mechanical FleetPy 1.0.2 adapter backed only by the external Runner."""

    @property
    def _rb_runner_service_bounds(self) -> bool:
        """ADR-076: true when the WP4 configuration declares `lateBoarding: record-v1`."""
        settings = getattr(self, "_rb_settings", None)
        return bool(getattr(settings, "runner_service_bounds", False))

    def __init__(
        self,
        op_id,
        operator_attributes,
        list_vehicles,
        routing_engine,
        zone_system,
        scenario_parameters,
        dir_names,
        op_charge_depot_infra=None,
        list_pub_charging_infra=[],
    ):
        super().__init__(
            op_id,
            operator_attributes,
            list_vehicles,
            routing_engine,
            zone_system,
            scenario_parameters,
            dir_names,
            op_charge_depot_infra,
            list_pub_charging_infra,
        )
        self.vr_ctrl_f = lambda *_args, **_kwargs: 0.0
        self._rb_settings = RideBoundSessionSettings.from_attributes(
            operator_attributes
        )
        self._rb_mapper = FleetPyProtocolMapper()
        self._rb_session = self._create_protocol_session(self._rb_settings)
        self._rb_events = OrderedEventBuffer()
        self._rb_states: dict[Any, str] = {}
        self._rb_routes: dict[Any, ProtocolRoute] = {
            vehicle.vid: ProtocolRoute(0, 0, (), ())
            for vehicle in self.sim_vehicles
        }
        self._rb_finished_legs: dict[Any, list[_FinishedLeg]] = defaultdict(list)
        self._rb_passenger_markers: list[_PassengerMarker] = []
        self._rb_dirty_vehicles: set[Any] = set()
        self._rb_semantic_vehicles: set[Any] = set()
        self._rb_terminal_after_ack: set[Any] = set()
        self._rb_travel_version = 0
        self._rb_travel_dirty = True
        self._rb_initialized = False
        self._rb_closed = False
        self._rb_publications: list[dict[str, Any]] = []
        self._rb_last_checkpoint: dict[str, Any] | None = None
        self._rb_checkpoint_binding_hash: str | None = None
        self._rb_graph_nodes: tuple[Any, ...] | None = None
        self._rb_travel_arc_cache: dict[tuple[Any, Any], Any] = {}
        for vehicle in self.sim_vehicles:
            self._rb_mapper.vehicles.register(vehicle.vid)
        atexit.register(self.close)

    @staticmethod
    def _create_protocol_session(settings):
        return RideBoundProtocolSession(settings)

    def close(self):
        if self._rb_closed:
            return
        self._rb_session.close()
        self._rb_closed = True

    def restart_runner(self):
        if (
            self._rb_events.has_pending_events
            or self._rb_finished_legs
            or self._rb_passenger_markers
            or self._rb_dirty_vehicles
            or self._rb_semantic_vehicles
            or self._rb_terminal_after_ack
        ):
            raise _fail("RBWP7_RESTART_UNSAFE", "$.adapter", "callback boundary")
        self.checkpoint_runner()
        assert self._rb_last_checkpoint is not None
        assert self._rb_checkpoint_binding_hash is not None
        before = self._checkpoint_binding_hash(self._rb_last_checkpoint)
        if before != self._rb_checkpoint_binding_hash:
            raise _fail(
                "RBWP7_ADAPTER_CHECKPOINT_DRIFT",
                "$.adapter.checkpointBindingHash",
                f"expected={self._rb_checkpoint_binding_hash}; actual={before}",
            )
        restored = self._rb_session.restart(self._rb_last_checkpoint)
        after = self._checkpoint_binding_hash(self._rb_last_checkpoint)
        if after != before:
            raise _fail(
                "RBWP7_ADAPTER_RESTART_DRIFT",
                "$.adapter.checkpointBindingHash",
                f"before={before}; after={after}",
            )
        return restored

    def checkpoint_runner(self):
        if (
            self._rb_closed
            or self._rb_events.has_pending_events
            or self._rb_finished_legs
            or self._rb_passenger_markers
            or self._rb_dirty_vehicles
            or self._rb_semantic_vehicles
            or self._rb_terminal_after_ack
        ):
            raise _fail("RBWP7_CHECKPOINT_UNSAFE", "$.adapter", "callback boundary")
        checkpoint = self._rb_session.checkpoint()
        self._record_checkpoint(checkpoint)
        return checkpoint

    def receive_status_update(
        self,
        vid,
        simulation_time,
        list_finished_VRL,
        force_update=True,
    ):
        self._require_vehicle(vid)
        for leg in list_finished_VRL:
            self._rb_finished_legs[vid].append(
                _FinishedLeg(
                    leg.destination_pos,
                    leg.status in G_DRIVING_STATUS,
                    tuple(
                        self._raw_request_id(value)
                        for value in leg.rq_dict.get(1, [])
                    ),
                    tuple(
                        self._raw_request_id(value)
                        for value in leg.rq_dict.get(-1, [])
                    ),
                )
            )
        super().receive_status_update(
            vid,
            simulation_time,
            list_finished_VRL,
            force_update=force_update,
        )
        self._rb_dirty_vehicles.add(vid)

    def user_request(self, rq, simulation_time):
        raw_request = rq.get_rid_struct()
        if raw_request in self._rb_states or raw_request in self.rq_dict:
            raise _fail("RBWP7_REQUEST_CALLBACK_DUPLICATE", "$.callback.request", repr(raw_request))
        prq = PlanRequest(
            rq,
            self.routing_engine,
            min_wait_time=self.min_wait_time,
            max_wait_time=self.max_wait_time,
            max_detour_time_factor=self.max_dtf,
            max_constant_detour_time=self.max_cdt,
            add_constant_detour_time=self.add_cdt,
            min_detour_time_window=self.min_dtw,
            boarding_time=self.const_bt,
        )
        self.rq_dict[raw_request] = prq
        self._rb_states[raw_request] = "pending"
        request = self._rb_mapper.request(
            prq,
            self._rb_settings.service_class,
            self._rb_settings.commitment_policy_id,
        )
        self._rb_events.append(
            simulation_time,
            PHASE_LIFECYCLE,
            "requestArrived",
            {"request": request},
            dedupe_key=f"request:{request['requestId']}",
        )
        if self._rb_settings.complete_travel_snapshot_maximum_nodes == 0:
            self._rb_travel_dirty = True

    def user_confirms_booking(self, rid, simulation_time):
        self._require_request_state(rid, "offered")
        super().user_confirms_booking(rid, simulation_time)
        request_id = self._rb_mapper.requests.register(rid)
        self._rb_events.append(
            simulation_time,
            PHASE_LIFECYCLE,
            "bookingConfirmed",
            {"requestId": request_id},
            dedupe_key=f"booking:{request_id}",
        )
        self._rb_states[rid] = "confirmed"

    def user_cancels_request(self, rid, simulation_time):
        state = self._rb_states.get(rid)
        if state is None:
            raise _fail("RBWP7_REQUEST_CALLBACK_UNKNOWN", "$.callback.cancel", repr(rid))
        if state == "rejected":
            self.rq_dict.pop(rid, None)
            self._rb_states[rid] = "closed"
            return
        if state in {"cancel-pending", "completed", "closed"}:
            raise _fail("RBWP7_CANCELLATION_DUPLICATE", "$.callback.cancel", repr(rid))
        event_type = {
            "pending": "requestCancelledBeforeAcceptance",
            "deferred": "requestCancelledBeforeAcceptance",
            "offered": "offerDeclined",
            "confirmed": "requestCancelledAfterAcceptance",
        }.get(state)
        if event_type is None:
            raise _fail("RBWP7_CANCELLATION_STATE_INVALID", "$.callback.cancel", state)
        request_id = self._rb_mapper.requests.register(rid)
        self._rb_events.append(
            simulation_time,
            PHASE_LIFECYCLE,
            event_type,
            {"requestId": request_id},
            dedupe_key=f"cancel:{request_id}",
        )
        self._rb_states[rid] = "cancel-pending"
        self._rb_terminal_after_ack.add(rid)

    def _create_user_offer(
        self,
        prq,
        simulation_time,
        assigned_vehicle_plan=None,
        offer_dict_without_plan=None,
    ):
        if assigned_vehicle_plan is None:
            return self._create_rejection(prq, simulation_time)
        rid = prq.get_rid_struct()
        pax_info = assigned_vehicle_plan.get_pax_info(rid)
        if pax_info is None or len(pax_info) != 2:
            raise _fail("RBWP7_OFFER_PLAN_MEMBERSHIP", "$.offer.plan", repr((rid, pax_info)))
        pickup, dropoff = pax_info
        if pickup < prq.rq_time or dropoff < pickup:
            raise _fail("RBWP7_OFFER_TIME_INVALID", "$.offer.plan", repr(pax_info))
        extras = {} if offer_dict_without_plan is None else dict(offer_dict_without_plan)
        extras["ridebound_candidate_source"] = "external-runner"
        offer = TravellerOffer(
            prq.get_rid(),
            self.op_id,
            pickup - prq.rq_time,
            dropoff - pickup,
            self._compute_fare(simulation_time, prq, assigned_vehicle_plan),
            extras,
        )
        prq.set_service_offered(offer)
        return offer

    def change_prq_time_constraints(self, sim_time, rid, new_lpt, new_ept=None):
        if rid not in self.rq_dict:
            raise _fail("RBWP7_REQUEST_CALLBACK_UNKNOWN", "$.callback.constraints", repr(rid))
        prq = self.rq_dict[rid]
        prq.set_new_pickup_time_constraint(new_lpt, new_ept)
        vid = self.rid_to_assigned_vid.get(rid)
        if vid is not None:
            feasible = self.veh_plans[vid].update_prq_hard_constraints(
                self.sim_vehicles[vid],
                sim_time,
                self.routing_engine,
                prq,
                new_lpt,
                new_ept=new_ept,
                keep_feasible=True,
            )
            if not feasible:
                raise _fail("RBWP7_CONSTRAINT_UPDATE_INFEASIBLE", "$.callback.constraints", repr(rid))

    def acknowledge_boarding(self, rid, vid, simulation_time):
        self._require_request_state(rid, "confirmed")
        self._require_assigned_vehicle(rid, vid)
        super().acknowledge_boarding(rid, vid, simulation_time)
        self._rb_passenger_markers.append(
            _PassengerMarker("passengerBoarded", rid, vid, simulation_time)
        )
        self._rb_states[rid] = "boarded"
        self._rb_dirty_vehicles.add(vid)

    def acknowledge_alighting(self, rid, vid, simulation_time):
        self._require_request_state(rid, "boarded")
        self._require_assigned_vehicle(rid, vid)
        self._rb_passenger_markers.append(
            _PassengerMarker("passengerAlighted", rid, vid, simulation_time)
        )
        self._rb_states[rid] = "completed"
        self._rb_terminal_after_ack.add(rid)
        self._rb_dirty_vehicles.add(vid)

    def assign_vehicle_plan(
        self,
        veh_obj,
        vehicle_plan,
        sim_time,
        force_assign=False,
        assigned_charging_task=None,
        add_arg=None,
    ):
        if force_assign:
            raise _fail("RBWP7_FORCE_ASSIGNMENT_PATH", "$.plan.forceAssign", "true")
        new_legs = self._build_VRLs(vehicle_plan, veh_obj, sim_time)
        if veh_obj.assigned_route and veh_obj.assigned_route[0].locked:
            if not new_legs or not self._leg_equivalent(
                veh_obj.assigned_route[0], new_legs[0]
            ):
                raise _fail(
                    "RBWP7_LOCKED_LEG_MISMATCH",
                    f"$.vehicles[{veh_obj.vid}].activeLeg",
                    "replacement is not exact-equivalent",
                )
        old_rids = set(self.rid_to_assigned_vid)
        super().assign_vehicle_plan(
            veh_obj,
            vehicle_plan,
            sim_time,
            force_assign=False,
            assigned_charging_task=assigned_charging_task,
            add_arg=add_arg,
        )
        assigned_now = set(vehicle_plan.get_involved_request_ids())
        for rid in old_rids - assigned_now:
            if self.rid_to_assigned_vid.get(rid) == veh_obj.vid:
                del self.rid_to_assigned_vid[rid]
        actual_signatures = [self._leg_signature(leg) for leg in veh_obj.assigned_route]
        expected_signatures = [self._leg_signature(leg) for leg in new_legs]
        if actual_signatures != expected_signatures:
            raise _fail("RBWP7_PLAN_POST_ASSIGN_MISMATCH", f"$.vehicles[{veh_obj.vid}]", repr(actual_signatures))

    def _call_time_trigger_request_batch(self, simulation_time):
        if self._rb_closed:
            raise _fail("RBWP7_SESSION_STATE_INVALID", "$.session", "closed")
        self.sim_time = simulation_time
        self._materialize_callback_events()
        prefix: list[tuple[str, dict[str, Any]]] = []
        snapshot = None
        if self._rb_travel_dirty or not self._rb_initialized:
            snapshot = self._build_travel_snapshot()
            prefix.append(("travelTimesUpdated", {"snapshot": snapshot}))
        if not self._rb_initialized:
            for vehicle in self.sim_vehicles:
                self._rb_dirty_vehicles.add(vehicle.vid)
        observation_vehicles = self._rb_dirty_vehicles & self._rb_semantic_vehicles
        for vid in sorted(self._rb_dirty_vehicles - observation_vehicles, key=repr):
            self._rb_events.append(
                simulation_time,
                PHASE_VEHICLE,
                "vehicleAdvanced",
                {"vehicle": self._vehicle_snapshot(vid)},
                dedupe_key=f"vehicle:{self._rb_mapper.vehicles.register(vid)}",
            )
        if not self._rb_initialized:
            assert snapshot is not None
            self._initialize_session(snapshot)
        batch = self._rb_events.drain(
            self._rb_settings.run_id,
            self._rb_settings.scenario_id,
            simulation_time,
            prefix=prefix,
        )
        decision = self._rb_session.decide(batch)
        parsed = parse_decision(decision, self._rb_mapper)
        self._apply_decision(parsed, simulation_time)
        self._rb_session.acknowledge(decision)
        if observation_vehicles:
            for vid in sorted(observation_vehicles, key=repr):
                self._rb_events.append(
                    simulation_time,
                    PHASE_VEHICLE,
                    "vehicleAdvanced",
                    {"vehicle": self._vehicle_snapshot(vid)},
                    dedupe_key=f"vehicle:{self._rb_mapper.vehicles.register(vid)}",
                )
            observation_batch = self._rb_events.drain(
                self._rb_settings.run_id,
                self._rb_settings.scenario_id,
                simulation_time,
                append_timer_tick=False,
            )
            observation_decision = self._rb_session.decide(observation_batch)
            observation_parsed = parse_decision(
                observation_decision,
                self._rb_mapper,
            )
            self._apply_decision(observation_parsed, simulation_time)
            self._rb_session.acknowledge(observation_decision)
        self._rb_dirty_vehicles.clear()
        self._rb_semantic_vehicles.clear()
        self._finalize_terminal_requests()

    def lock_current_vehicle_plan(self, vid):
        self._require_vehicle(vid)
        route = self._rb_routes[vid]
        if route.mutable_suffix:
            raise _fail(
                "RBWP7_EXTERNAL_PLAN_LOCK_UNSUPPORTED",
                f"$.vehicles[{vid}].route",
                "core-owned mutable suffix cannot be frozen externally",
            )
        super().lock_current_vehicle_plan(vid)

    def inform_network_travel_time_update(self, simulation_time):
        if self._rb_closed:
            raise _fail("RBWP7_SESSION_STATE_INVALID", "$.session", "closed")
        self._rb_travel_arc_cache.clear()
        self._rb_travel_dirty = True

    def _lock_vid_rid_pickup(self, sim_time, vid, rid):
        route = self._rb_routes.get(vid)
        if route is None or any(
            stop.raw_request == rid and stop.kind == "pickup"
            for stop in route.mutable_suffix
        ):
            raise _fail(
                "RBWP7_EXTERNAL_PICKUP_LOCK_UNSUPPORTED",
                f"$.vehicles[{vid}].requests[{rid}]",
                "lock must originate in the Runner route",
            )
        super()._lock_vid_rid_pickup(sim_time, vid, rid)

    def _prq_from_reservation_to_immediate(self, rid, sim_time):
        if rid not in self.rq_dict:
            raise _fail("RBWP7_REQUEST_CALLBACK_UNKNOWN", "$.reservation", repr(rid))
        self.rq_dict[rid].set_reservation_flag(False)

    def _initialize_session(self, snapshot):
        graph_nodes = self._all_graph_nodes()
        node_ids = [self._rb_mapper.node_id(node) for node in graph_nodes]
        request_ids = [
            self._rb_mapper.requests.register(rid)
            for rid, state in self._rb_states.items()
            if state != "closed"
        ]
        vehicle_ids = [
            self._rb_mapper.vehicles.register(vehicle.vid)
            for vehicle in self.sim_vehicles
        ]
        self._rb_session.initialize(snapshot, node_ids, request_ids, vehicle_ids)
        self._rb_initialized = True

    def _all_graph_nodes(self):
        if self._rb_graph_nodes is None:
            count = self.routing_engine.get_number_network_nodes()
            if isinstance(count, bool) or not isinstance(count, int) or count < 2:
                raise _fail("RBWP7_GRAPH_NODE_COUNT_INVALID", "$.network.nodes", repr(count))
            self._rb_graph_nodes = tuple(range(count))
        return self._rb_graph_nodes

    def _active_raw_nodes(self) -> set[Any]:
        maximum = self._rb_settings.complete_travel_snapshot_maximum_nodes
        if maximum > 0:
            graph_nodes = self._all_graph_nodes()
            if len(graph_nodes) > maximum:
                raise _fail(
                    "RBWP7_COMPLETE_TRAVEL_SNAPSHOT_BOUND_EXCEEDED",
                    "$.network.nodes",
                    f"actual={len(graph_nodes)}; maximum={maximum}",
                )
            return set(graph_nodes)

        nodes: set[Any] = set()
        for vehicle in self.sim_vehicles:
            self._collect_position_nodes(vehicle.pos, nodes)
        for prq in self.rq_dict.values():
            self._collect_position_nodes(prq.o_pos, nodes)
            self._collect_position_nodes(prq.d_pos, nodes)
        for route in self._rb_routes.values():
            nodes.update(stop.raw_node for stop in route.remaining_stops)
        if len(nodes) < 2:
            for node in self._all_graph_nodes():
                nodes.add(node)
                if len(nodes) == 2:
                    break
        return nodes

    @staticmethod
    def _collect_position_nodes(position, nodes):
        if not isinstance(position, tuple) or len(position) != 3:
            raise _fail("RBWP7_POSITION_SHAPE_INVALID", "$.vehicle.position", repr(position))
        nodes.add(position[0])
        if position[1] is not None:
            nodes.add(position[1])

    def _build_travel_snapshot(self):
        nodes = sorted(self._active_raw_nodes(), key=repr)
        arcs = []
        for origin in nodes:
            for destination in nodes:
                if origin == destination:
                    continue
                key = (origin, destination)
                if key not in self._rb_travel_arc_cache:
                    try:
                        costs = self.routing_engine.return_travel_costs_1to1(
                            (origin, None, None),
                            (destination, None, None),
                        )
                        self._rb_travel_arc_cache[key] = costs[1]
                    except (ArithmeticError, IndexError, KeyError, TypeError, ValueError) as exc:
                        raise _fail(
                            "RBWP7_TRAVEL_ARC_UNAVAILABLE",
                            "$.network.travel",
                            f"{origin!r}->{destination!r}: {exc}",
                        ) from exc
                travel_time = self._rb_travel_arc_cache[key]
                if isinstance(travel_time, bool) or not isinstance(travel_time, (int, float, Decimal)):
                    raise _fail("RBWP7_TRAVEL_TIME_INVALID", "$.network.travel", repr(travel_time))
                if isinstance(travel_time, float) and not math.isfinite(travel_time):
                    raise _fail("RBWP7_TRAVEL_TIME_INVALID", "$.network.travel", repr(travel_time))
                arcs.append((origin, destination, travel_time))
        self._rb_travel_version += 1
        snapshot = self._rb_mapper.travel_snapshot(self._rb_travel_version, arcs)
        self._rb_travel_dirty = False
        return snapshot

    def _materialize_callback_events(self):
        markers_by_vehicle: dict[Any, list[_PassengerMarker]] = defaultdict(list)
        for marker in self._rb_passenger_markers:
            markers_by_vehicle[marker.raw_vehicle].append(marker)

        vehicles = set(self._rb_finished_legs) | set(markers_by_vehicle)
        for vid in sorted(vehicles, key=repr):
            markers = markers_by_vehicle[vid]
            finished_legs = self._rb_finished_legs.get(vid, ())
            for leg_index, finished_leg in enumerate(finished_legs):
                route = self._rb_routes[vid]
                raw_position = (
                    finished_leg.raw_position[0]
                    if isinstance(finished_leg.raw_position, tuple)
                    and len(finished_leg.raw_position) == 3
                    else finished_leg.raw_position
                )
                if finished_leg.is_driving:
                    if not route.remaining_stops:
                        raise _fail(
                            "RBWP7_FINISHED_LEG_WITHOUT_ROUTE",
                            f"$.vehicles[{vid}].finishedLegs[{leg_index}]",
                            repr(finished_leg),
                        )
                    expected = route.remaining_stops[0]
                    if expected.raw_node != raw_position:
                        physical = [
                            leg.destination_pos
                            for leg in self._require_vehicle(vid).assigned_route
                        ]
                        raise _fail(
                            "RBWP7_REACHED_STOP_ORDER",
                            f"$.vehicles[{vid}].finishedLegs[{leg_index}]",
                            f"time={self.sim_time!r}; expected={expected.raw_node!r}; "
                            f"actual={raw_position!r}; "
                            f"protocolRemaining="
                            f"{[stop.raw_node for stop in route.remaining_stops]!r}; "
                            f"finished={list(finished_legs)!r}; "
                            f"physicalRemaining={physical!r}",
                        )

                    # Finishing a driving VRL only means that the vehicle is at
                    # the node. A passenger stop is complete only when FleetPy
                    # also reports the corresponding boarding/alighting
                    # callback. A routing waypoint needs no passenger callback.
                    if expected.raw_request is None:
                        self._emit_reached(vid, expected, self.sim_time)
                    else:
                        marker_index = self._matching_marker_index(markers, expected)
                        if marker_index is not None:
                            marker = markers.pop(marker_index)
                            self._require_marker_batch_time(marker)
                            self._emit_reached(vid, expected, self.sim_time)
                            self._append_passenger(marker)
                    continue

                passenger_kinds = {
                    *(('pickup', rid) for rid in finished_leg.boarding_requests),
                    *(('dropOff', rid) for rid in finished_leg.alighting_requests),
                }
                if passenger_kinds:
                    # A zero-distance passenger stop appears only as a service
                    # VRL. Preserve FleetPy's completed-VRL order so that such a
                    # stop is consumed before a later driving destination in
                    # the same coarse simulation step.
                    while self._rb_routes[vid].remaining_stops:
                        expected = self._rb_routes[vid].remaining_stops[0]
                        if (expected.kind, expected.raw_request) not in passenger_kinds:
                            break
                        if expected.raw_node != raw_position:
                            raise _fail(
                                "RBWP7_PASSENGER_STOP_POSITION",
                                f"$.vehicles[{vid}].finishedLegs[{leg_index}]",
                                f"expected={expected.raw_node!r}; actual={raw_position!r}",
                            )
                        marker_index = self._matching_marker_index(markers, expected)
                        if marker_index is None:
                            raise _fail(
                                "RBWP7_PASSENGER_CALLBACK_MISSING",
                                f"$.vehicles[{vid}].finishedLegs[{leg_index}]",
                                f"request={expected.raw_request!r}; kind={expected.kind}",
                            )
                        marker = markers.pop(marker_index)
                        self._require_marker_batch_time(marker)
                        self._emit_reached(vid, expected, self.sim_time)
                        self._append_passenger(marker)
                    executed = self._rb_routes[vid].frozen_prefix[
                        : self._rb_routes[vid].executed_stop_count
                    ]
                    unaccounted = sorted(
                        (kind, repr(rid))
                        for kind, rid in passenger_kinds
                        if not any(
                            stop.kind == kind
                            and stop.raw_request == rid
                            and stop.raw_node == raw_position
                            for stop in executed
                        )
                    )
                    if unaccounted:
                        raise _fail(
                            "RBWP7_FINISHED_SERVICE_WITHOUT_ROUTE",
                            f"$.vehicles[{vid}].finishedLegs[{leg_index}]",
                            repr(unaccounted),
                        )
                    continue

                # A zero-distance routing waypoint can likewise have no driving
                # VRL. Completion of its non-passenger service leg is the only
                # physical proof that the waypoint was reached.
                if (
                    route.remaining_stops
                    and route.remaining_stops[0].raw_request is None
                    and route.remaining_stops[0].raw_node == raw_position
                ):
                    self._emit_reached(
                        vid,
                        route.remaining_stops[0],
                        self.sim_time,
                    )

            # A passenger callback is emitted when its service leg starts. If
            # the service extends beyond this FleetPy step, the VRL is not yet
            # in finished_legs; the callback itself still proves exact arrival.
            for marker in markers:
                expected_kind = (
                    "pickup" if marker.event_type == "passengerBoarded" else "dropOff"
                )
                route = self._rb_routes[vid]
                if (
                    not route.remaining_stops
                    or route.remaining_stops[0].raw_request != marker.raw_request
                    or route.remaining_stops[0].kind != expected_kind
                ):
                    raise _fail(
                        "RBWP7_PASSENGER_STOP_ORDER",
                        f"$.vehicles[{vid}].route",
                        f"request={marker.raw_request!r}; kind={expected_kind}",
                    )
                self._require_marker_batch_time(marker)
                self._emit_reached(vid, route.remaining_stops[0], self.sim_time)
                self._append_passenger(marker)

        self._rb_finished_legs.clear()
        self._rb_passenger_markers.clear()

    @staticmethod
    def _matching_marker_index(markers, stop):
        expected_type = (
            "passengerBoarded" if stop.kind == "pickup" else "passengerAlighted"
        )
        for index, marker in enumerate(markers):
            if (
                marker.raw_request == stop.raw_request
                and marker.event_type == expected_type
            ):
                return index
        return None

    def _append_passenger(self, marker):
        request_id = self._rb_mapper.requests.register(marker.raw_request)
        self._rb_events.append(
            marker.simulation_time,
            PHASE_PASSENGER,
            marker.event_type,
            {
                "vehicleId": self._rb_mapper.vehicles.register(marker.raw_vehicle),
                "requestId": request_id,
                "planVersion": self._rb_routes[marker.raw_vehicle].plan_version,
            },
            dedupe_key=f"{marker.event_type}:{request_id}",
        )
        self._rb_semantic_vehicles.add(marker.raw_vehicle)

    def _require_marker_batch_time(self, marker):
        marker_ms = seconds_to_milliseconds(
            marker.simulation_time,
            "$.callback.passenger.simulationTime",
        )
        batch_ms = seconds_to_milliseconds(
            self.sim_time,
            "$.callback.batchSimulationTime",
        )
        if marker_ms != batch_ms:
            raise _fail(
                "RBWP7_CALLBACK_BATCH_TIME_MISMATCH",
                "$.callback.passenger.simulationTime",
                f"event={marker_ms}; batch={batch_ms}",
            )

    def _emit_reached(self, vid, stop, simulation_time):
        route = self._rb_routes[vid]
        self._rb_events.append(
            simulation_time,
            PHASE_REACHED_STOP,
            "vehicleReachedStop",
            {
                "vehicleId": self._rb_mapper.vehicles.register(vid),
                "stopId": stop.stop_id,
                "planVersion": route.plan_version,
                "position": {
                    "kind": "node",
                    "nodeId": self._rb_mapper.node_id(stop.raw_node),
                },
            },
            dedupe_key=f"reached:{self._rb_mapper.vehicles.register(vid)}:{route.plan_version}:{stop.stop_id}",
        )
        self._rb_semantic_vehicles.add(vid)
        self._rb_routes[vid] = self._advance_route(route, stop)

    @staticmethod
    def _advance_route(route, stop):
        if route.executed_stop_count < len(route.frozen_prefix):
            expected = route.frozen_prefix[route.executed_stop_count]
            if expected != stop:
                raise _fail("RBWP7_REACHED_STOP_ORDER", "$.route.frozenPrefix", stop.stop_id)
            return ProtocolRoute(
                route.plan_version,
                route.executed_stop_count + 1,
                route.frozen_prefix,
                route.mutable_suffix,
            )
        if not route.mutable_suffix or route.mutable_suffix[0] != stop:
            raise _fail("RBWP7_REACHED_STOP_ORDER", "$.route.mutableSuffix", stop.stop_id)
        return ProtocolRoute(
            route.plan_version,
            route.executed_stop_count + 1,
            route.frozen_prefix + (stop,),
            route.mutable_suffix[1:],
        )

    def _vehicle_snapshot(self, vid):
        vehicle = self._require_vehicle(vid)
        onboard_raw = [request.get_rid_struct() for request in vehicle.pax]
        onboard_ids = sorted(self._rb_mapper.requests.register(rid) for rid in onboard_raw)
        accepted_raw = [
            rid
            for rid, assigned_vid in self.rid_to_assigned_vid.items()
            if assigned_vid == vid
            and self._rb_states.get(rid) in {"offered", "confirmed", "boarded"}
        ]
        occupied = sum(request.nr_pax for request in vehicle.pax)
        if occupied < 0 or occupied > vehicle.max_pax:
            raise _fail("RBWP7_VEHICLE_LOAD_INVALID", f"$.vehicles[{vid}]", str(occupied))
        return {
            "vehicleId": self._rb_mapper.vehicles.register(vid),
            "capacity": vehicle.max_pax,
            "occupiedSeats": occupied,
            "position": self._rb_mapper.position(vehicle.pos),
            "onboardRequestIds": onboard_ids,
            "acceptedRequestIds": sorted(
                self._rb_mapper.requests.register(rid) for rid in accepted_raw
            ),
            "route": self._route_contract(self._rb_routes[vid]),
        }

    def _route_contract(self, route):
        return {
            "planVersion": route.plan_version,
            "executedStopCount": route.executed_stop_count,
            "frozenPrefix": [self._stop_contract(stop) for stop in route.frozen_prefix],
            "mutableSuffix": [self._stop_contract(stop) for stop in route.mutable_suffix],
        }

    def _stop_contract(self, stop):
        return {
            "stopId": stop.stop_id,
            "nodeId": self._rb_mapper.node_id(stop.raw_node),
            "kind": stop.kind,
            "requestId": None
            if stop.raw_request is None
            else self._rb_mapper.requests.register(stop.raw_request),
            "serviceDurationMs": stop.service_duration_ms,
        }

    def _apply_decision(self, parsed: ParsedDecision, simulation_time):
        accepted_by_request = {action.raw_request: action for action in parsed.accepted}
        prepared_plans = []
        for action in parsed.plans:
            current = self._rb_routes[action.raw_vehicle]
            if action.route == current:
                # Runner echoes event-induced progress so the decision is a
                # complete state transition. FleetPy already executed this exact
                # route prefix; assigning it again would duplicate service.
                continue
            if (
                action.route.executed_stop_count != current.executed_stop_count
                or action.route.frozen_prefix != current.frozen_prefix
                or action.route.plan_version != current.plan_version + 1
            ):
                raise _fail(
                    "RBWP7_PLAN_CORE_BOUNDARY_MISMATCH",
                    f"$.vehicles[{action.raw_vehicle}].route",
                    "version/progress/frozen prefix changed outside the core boundary",
                )
            plan = self._fleetpy_plan(action.raw_vehicle, action.route, simulation_time)
            prepared_plans.append((action, plan))
        for action in parsed.accepted:
            self._require_request_state(action.raw_request, "pending", "deferred")
            if action.raw_vehicle not in self._rb_routes:
                raise _fail("RBWP7_ACCEPTED_VEHICLE_UNKNOWN", "$.decision.accepted", repr(action.raw_vehicle))
        for action in parsed.promises:
            if self._rb_states.get(action.raw_request) not in {"confirmed", "boarded", "completed"}:
                raise _fail("RBWP7_PROMISE_BEFORE_CONFIRMATION", "$.decision.promise", repr(action.raw_request))
            self._rb_publications.append(
                {
                    "requestId": action.request_id,
                    "publicationId": action.publication_id,
                    "promiseVersion": action.promise_version,
                    "reasonCode": action.reason_code,
                    "sourceEventSeq": action.source_event_sequence,
                }
            )
        relocations = ()
        if getattr(self._rb_settings, "waiting_rider_relocation", False):
            # G2: infer and check the one waiting rider this decision may move, before
            # FleetPy applies any plan (relocation.py). Off, nothing here runs.
            relocations = infer_waiting_rider_relocations(
                {
                    action.raw_vehicle: route_riders(self._rb_routes[action.raw_vehicle].remaining_stops)
                    for action, _ in prepared_plans
                },
                {action.raw_vehicle: route_riders(action.route.remaining_stops) for action, _ in prepared_plans},
                dict(self.rid_to_assigned_vid),
                dict(self._rb_states),
                {
                    action.raw_request: self._rb_mapper.raw_vehicle(action.vehicle_id)
                    for action in parsed.promises
                    if action.vehicle_id is not None
                },
                frozenset(accepted_by_request),
            )
        plans_by_vehicle = {}
        for action, plan in prepared_plans:
            plans_by_vehicle[action.raw_vehicle] = plan
            self.assign_vehicle_plan(
                self._require_vehicle(action.raw_vehicle),
                plan,
                simulation_time,
                force_assign=False,
            )
            self._rb_routes[action.raw_vehicle] = action.route
        for action in parsed.accepted:
            plan = plans_by_vehicle.get(action.raw_vehicle)
            if plan is None or action.raw_request not in plan.get_involved_request_ids():
                raise _fail("RBWP7_ACCEPTANCE_PLAN_MISMATCH", "$.decision.accepted", repr(action.raw_request))
            self._rb_states[action.raw_request] = "offered"
            self._create_user_offer(
                self.rq_dict[action.raw_request],
                simulation_time,
                plan,
            )
        for outcome in parsed.outcomes:
            state = self._rb_states.get(outcome.raw_request)
            if outcome.decision_type == "requestRejected":
                if state not in {"pending", "deferred"}:
                    raise _fail("RBWP7_REJECTION_STATE_INVALID", "$.decision.outcome", repr((outcome.raw_request, state)))
                self._create_rejection(self.rq_dict[outcome.raw_request], simulation_time)
                self._rb_states[outcome.raw_request] = "rejected"
            else:
                if state not in {"pending", "deferred"}:
                    raise _fail("RBWP7_DEFER_STATE_INVALID", "$.decision.outcome", repr((outcome.raw_request, state)))
                self._rb_states[outcome.raw_request] = "deferred"
        for rid, acceptance in accepted_by_request.items():
            if self.rid_to_assigned_vid.get(rid) != acceptance.raw_vehicle:
                raise _fail("RBWP7_ASSIGNMENT_INDEX_MISMATCH", "$.decision.accepted", repr(rid))
        for move in relocations:
            if self.rid_to_assigned_vid.get(move.raw_request) != move.raw_receiver:
                raise _fail("RBWP7_RELOCATION_INDEX_MISMATCH", "$.decision.vehiclePlanUpdated", repr(move))

    def _fleetpy_plan(self, vid, route, simulation_time):
        plan = VehiclePlan(
            self._require_vehicle(vid),
            simulation_time,
            self.routing_engine,
            self._fleetpy_stops(route),
        )
        in_progress = self._in_progress_service_stop(vid, route, plan, simulation_time)
        if in_progress is not None:
            plan = VehiclePlan(
                self._require_vehicle(vid),
                simulation_time,
                self.routing_engine,
                [in_progress, *self._fleetpy_stops(route)],
            )
        else:
            started = self._started_first_stop(vid, plan, simulation_time)
            if started is not None:
                plan = VehiclePlan(
                    self._require_vehicle(vid),
                    simulation_time,
                    self.routing_engine,
                    [started, *self._fleetpy_stops(route)[1:]],
                )
        if not plan.is_feasible() or not plan.is_structural_feasible():
            raise _fail("RBWP7_FLEETPY_PLAN_INFEASIBLE", f"$.vehicles[{vid}].route", str(plan))
        return plan

    def _fleetpy_stops(self, route):
        stops = []
        unexecuted_frozen = route.frozen_prefix[route.executed_stop_count :]
        for stop in unexecuted_frozen:
            stops.append(self._fleetpy_stop(stop, locked=True))
        for stop in route.mutable_suffix:
            stops.append(self._fleetpy_stop(stop, locked=False))
        return stops

    def _in_progress_service_stop(self, vid, route, plan, simulation_time):
        """FleetPy's own in-progress stop to keep in front of the Runner's route, or None.

        With 0 s dwell a vehicle that has just arrived serves the stop in a zero-duration leg that FleetPy
        keeps locked for the rest of the step (Vehicles.py:226-228). The Runner already counts the stop as
        executed. A new route decided in that step (for example a G2 receiver picking up at the same node)
        then has a different first leg, and FleetPy refuses to replace a locked leg (Vehicles.py:351-366).
        FleetPy's own convention for re-planning such a vehicle is to keep the started stop first, locked,
        with its start time (VehiclePlan.update_plan, step 2). This returns a copy of that stop only when all
        of the following hold; otherwise the plan is exactly the Runner's route, as before:
          - the current leg is a locked BOARDING leg of zero duration;
          - the Runner's route would not already start with an equivalent leg (no conflict, no change);
          - FleetPy's stored plan starts with that started stop (same node, riders, lock, start time);
          - every rider boarding there has its pickup, and every rider alighting there its drop, executed in
            the Runner's route and not remaining.
        """
        vehicle = self._require_vehicle(vid)
        if not vehicle.assigned_route:
            return None
        current = vehicle.assigned_route[0]
        if not current.locked or current.status != VRL_STATES.BOARDING or current.duration != 0:
            return None
        new_legs = self._build_VRLs(plan, vehicle, simulation_time)
        if new_legs and self._leg_equivalent(current, new_legs[0]):
            return None
        stored = getattr(self, "veh_plans", {}).get(vid)
        if stored is None or not stored.list_plan_stops:
            return None
        head = stored.list_plan_stops[0]
        boarding = {request.get_rid_struct() for request in current.rq_dict.get(1, [])}
        alighting = {request.get_rid_struct() for request in current.rq_dict.get(-1, [])}
        if (
            head.get_pos() != current.destination_pos
            or not head.is_locked()
            or head.get_started_at() is None
            or set(head.get_list_boarding_rids()) != boarding
            or set(head.get_list_alighting_rids()) != alighting
        ):
            return None
        executed = route.frozen_prefix[: route.executed_stop_count]
        remaining = route.remaining_stops

        def finished(rid, kind):
            return any(stop.raw_request == rid and stop.kind == kind for stop in executed) and not any(
                stop.raw_request == rid and stop.kind == kind for stop in remaining
            )

        if not all(finished(rid, "pickup") for rid in boarding) or not all(
            finished(rid, "dropOff") for rid in alighting
        ):
            return None
        # The stop is being served now, so its latest-time bounds are history: a late boarding there is already
        # recorded by the Runner (ADR-076). Kept on the copy, they would make the plan infeasible. The earliest
        # times stay, so the leg built from the copy is the same locked leg. FleetPy's stored stop is unchanged.
        in_progress = head.copy()
        in_progress.latest_pickup_time_dict = {}
        in_progress.latest_arrival_time_dict = {}
        in_progress.max_trip_time_dict = {}
        return in_progress

    def _started_first_stop(self, vid, plan, simulation_time):
        """FleetPy's own started stop to use in place of the Runner's first stop, or None.

        The twin of _in_progress_service_stop for a stop the Runner does not yet count as executed. A vehicle that has
        just arrived serves the stop in a zero-duration leg that FleetPy keeps locked for the rest of the step, but the
        event batch of that instant may not report it yet, so the Runner still holds the stop as remaining and
        unlocked. A new route decided in that step that starts with that same stop (for example a G2 donor giving a
        waiting rider away) then builds a first leg that differs from FleetPy's only in `locked`, and FleetPy refuses
        to replace a locked leg (Vehicles.py:351-366). This returns a copy of FleetPy's started stop, to take the place
        of the Runner's first stop (it is the same stop), only when all of the following hold; otherwise the plan is
        exactly the Runner's route, as before:
          - the current leg is a locked BOARDING leg of zero duration;
          - the first leg built from the Runner's route is unlocked, not equivalent to the current leg, and equal to it
            in every other field of the leg signature (status, node, boarding and alighting riders, duration, earliest
            start and end);
          - FleetPy's stored plan starts with that started stop (same node, riders, lock, start time).
        """
        vehicle = self._require_vehicle(vid)
        if not vehicle.assigned_route:
            return None
        current = vehicle.assigned_route[0]
        if not current.locked or current.status != VRL_STATES.BOARDING or current.duration != 0:
            return None
        new_legs = self._build_VRLs(plan, vehicle, simulation_time)
        if not new_legs or new_legs[0].locked or self._leg_equivalent(current, new_legs[0]):
            return None
        current_signature, first_signature = self._leg_signature(current), self._leg_signature(new_legs[0])
        lock_field = 4
        if (current_signature[:lock_field] + current_signature[lock_field + 1:]
                != first_signature[:lock_field] + first_signature[lock_field + 1:]):
            return None
        stored = getattr(self, "veh_plans", {}).get(vid)
        if stored is None or not stored.list_plan_stops:
            return None
        head = stored.list_plan_stops[0]
        boarding = {request.get_rid_struct() for request in current.rq_dict.get(1, [])}
        alighting = {request.get_rid_struct() for request in current.rq_dict.get(-1, [])}
        if (
            head.get_pos() != current.destination_pos
            or not head.is_locked()
            or head.get_started_at() is None
            or set(head.get_list_boarding_rids()) != boarding
            or set(head.get_list_alighting_rids()) != alighting
        ):
            return None
        # As in _in_progress_service_stop: the stop is being served now, so its latest-time bounds are history (a late
        # service is recorded by the Runner, ADR-076). The earliest times stay, so the leg is the same locked leg.
        started = head.copy()
        started.latest_pickup_time_dict = {}
        started.latest_arrival_time_dict = {}
        started.max_trip_time_dict = {}
        return started

    def _fleetpy_stop(self, stop: ProtocolStop, locked: bool):
        position = (stop.raw_node, None, None)
        duration = float(Decimal(stop.service_duration_ms) / Decimal(1000))
        if stop.kind == "waypoint":
            return RoutingTargetPlanStop(position, duration=duration, locked=locked)
        if stop.raw_request not in self.rq_dict:
            raise _fail("RBWP7_PLAN_REQUEST_UNKNOWN", "$.route.stop.requestId", repr(stop.raw_request))
        prq = self.rq_dict[stop.raw_request]
        if stop.kind == "pickup":
            if prq.o_pos != position:
                raise _fail("RBWP7_PICKUP_NODE_MISMATCH", "$.route.stop.nodeId", repr(stop.raw_request))
            return BoardingPlanStop(
                position,
                boarding_dict={1: [stop.raw_request], -1: []},
                earliest_pickup_time_dict={stop.raw_request: prq.t_pu_earliest},
                latest_pickup_time_dict={stop.raw_request: prq.t_pu_latest},
                change_nr_pax=prq.nr_pax,
                duration=duration,
                locked=locked,
            )
        if stop.kind == "dropOff":
            if prq.d_pos != position:
                raise _fail("RBWP7_DROPOFF_NODE_MISMATCH", "$.route.stop.nodeId", repr(stop.raw_request))
            # ADR-076: FleetPy's latest arrival is `t_pu_latest + max_trip_time`, anchored on
            # the original window; the Runner bounds a boarded rider by the actual boarding
            # plus the maximum ride, which `max_trip_time_dict` already checks from the real
            # boarding time. The two agree for every on-time rider; only a late boarding makes
            # the anchored one stricter. When the Runner records late boardings the anchored
            # bound is dropped, so the effective bound is boarding + maximum trip. The pickup
            # window and the maximum trip stay as an independent cross-check: the Runner
            # validates every changed candidate without allowance, but after an in-batch
            # cancellation the relieved no-op (ADR-047) is re-sent too, and these checks can
            # then reject it; that path predates ADR-076.
            latest_arrival = (
                {} if self._rb_runner_service_bounds else {stop.raw_request: prq.t_do_latest}
            )
            return BoardingPlanStop(
                position,
                boarding_dict={1: [], -1: [stop.raw_request]},
                max_trip_time_dict={stop.raw_request: prq.max_trip_time},
                latest_arrival_time_dict=latest_arrival,
                change_nr_pax=-prq.nr_pax,
                duration=duration,
                locked=locked,
            )
        raise _fail("RBWP7_ROUTE_STOP_KIND_INVALID", "$.route.stop.kind", stop.kind)

    def _finalize_terminal_requests(self):
        for rid in sorted(self._rb_terminal_after_ack, key=repr):
            self.rq_dict.pop(rid, None)
            self.rid_to_assigned_vid.pop(rid, None)
            self._rb_states[rid] = "closed"
        self._rb_terminal_after_ack.clear()

    def _record_checkpoint(self, checkpoint):
        self._rb_last_checkpoint = checkpoint
        self._rb_checkpoint_binding_hash = self._checkpoint_binding_hash(checkpoint)

    def _checkpoint_binding_hash(self, checkpoint):
        try:
            checkpoint_hash = checkpoint["payload"]["checkpointHash"]
        except (KeyError, TypeError) as exc:
            raise _fail("RBWP7_CHECKPOINT_INVALID", "$.checkpoint", str(exc)) from exc
        adapter_state = {
            "nextEventSeq": self._rb_events.next_event_sequence,
            "nextEpoch": self._rb_events.next_epoch,
            "travelVersion": self._rb_travel_version,
            "travelDirty": self._rb_travel_dirty,
            "requestStates": [
                [self._rb_mapper.requests.register(rid), state]
                for rid, state in sorted(
                    self._rb_states.items(),
                    key=lambda item: self._rb_mapper.requests.register(item[0]),
                )
            ],
            "vehicles": [
                {
                    "protocol": self._vehicle_snapshot(vehicle.vid),
                    "physicalLegs": [
                        self._physical_leg_contract(leg)
                        for leg in vehicle.assigned_route
                    ],
                }
                for vehicle in sorted(self.sim_vehicles, key=lambda item: repr(item.vid))
            ],
            "publications": list(self._rb_publications),
        }
        adapter_hash = hashlib.sha256(
            b"RideBound.FleetPyAdapterState.v1\0"
            + canonical_json_bytes(adapter_state)
        ).digest()
        try:
            runner_hash = bytes.fromhex(checkpoint_hash)
        except (TypeError, ValueError) as exc:
            raise _fail(
                "RBWP7_CHECKPOINT_INVALID",
                "$.checkpoint.checkpointHash",
                repr(checkpoint_hash),
            ) from exc
        if len(runner_hash) != 32:
            raise _fail(
                "RBWP7_CHECKPOINT_INVALID",
                "$.checkpoint.checkpointHash",
                repr(checkpoint_hash),
            )
        return hashlib.sha256(
            b"RideBound.FleetPyCheckpointBinding.v1\0"
            + runner_hash
            + adapter_hash
        ).hexdigest()

    def _physical_leg_contract(self, leg):
        def request_ids(values):
            result = []
            for value in values:
                raw = (
                    value.get_rid_struct()
                    if hasattr(value, "get_rid_struct")
                    else value.get_rid()
                    if hasattr(value, "get_rid")
                    else value
                )
                result.append(self._rb_mapper.requests.register(raw))
            return sorted(result)

        def optional_time(value, path):
            # FleetPy VehicleRouteLeg uses the exact -1000 sentinel for an
            # unspecified earliest bound. It is representation metadata, not a
            # negative simulation timestamp.
            if type(value).__module__.startswith("numpy") and hasattr(value, "item"):
                value = value.item()
            return (
                None
                if value is None or value in {-1, -1000, -100_000_000}
                else seconds_to_milliseconds(value, path)
            )

        return {
            "status": leg.status.name,
            "destination": self._rb_mapper.position(leg.destination_pos),
            "boardingRequestIds": request_ids(leg.rq_dict.get(1, [])),
            "alightingRequestIds": request_ids(leg.rq_dict.get(-1, [])),
            "locked": bool(leg.locked),
            "durationMs": optional_time(leg.duration, "$.physicalLeg.duration"),
            "earliestStartMs": optional_time(
                leg.earliest_start_time,
                "$.physicalLeg.earliestStartTime",
            ),
            "earliestEndMs": optional_time(
                leg.earliest_end_time,
                "$.physicalLeg.earliestEndTime",
            ),
        }

    def _require_vehicle(self, vid):
        if isinstance(vid, bool) or not isinstance(vid, int) or vid < 0 or vid >= len(self.sim_vehicles):
            raise _fail("RBWP7_VEHICLE_UNKNOWN", "$.vehicle", repr(vid))
        vehicle = self.sim_vehicles[vid]
        if vehicle.vid != vid:
            raise _fail("RBWP7_VEHICLE_INDEX_DRIFT", "$.vehicle", repr((vid, vehicle.vid)))
        return vehicle

    def _require_request_state(self, rid, *expected):
        actual = self._rb_states.get(rid)
        if actual not in expected:
            raise _fail(
                "RBWP7_REQUEST_STATE_INVALID",
                "$.request.lifecycle",
                f"request={rid!r}; actual={actual!r}; expected={expected!r}",
            )

    def _require_assigned_vehicle(self, rid, vid):
        self._require_vehicle(vid)
        if self.rid_to_assigned_vid.get(rid) != vid:
            raise _fail("RBWP7_REQUEST_VEHICLE_MISMATCH", "$.request.vehicle", repr((rid, vid)))

    @staticmethod
    def _raw_request_id(value):
        if hasattr(value, "get_rid_struct"):
            return value.get_rid_struct()
        if hasattr(value, "get_rid"):
            return value.get_rid()
        return value

    @staticmethod
    def _rid_values(values: Iterable[Any]):
        return Counter(RideBoundFleetControl._raw_request_id(value) for value in values)

    @classmethod
    def _leg_signature(cls, leg):
        return (
            leg.status,
            leg.destination_pos,
            cls._rid_values(leg.rq_dict.get(1, [])),
            cls._rid_values(leg.rq_dict.get(-1, [])),
            leg.locked,
            leg.duration,
            leg.earliest_start_time,
            leg.earliest_end_time,
        )

    @classmethod
    def _leg_equivalent(cls, left, right):
        return cls._leg_signature(left) == cls._leg_signature(right)
