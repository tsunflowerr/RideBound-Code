from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable

from .errors import AdapterFailure
from .mapping import MAX_CANONICAL_INTEGER, FleetPyProtocolMapper, seconds_to_milliseconds


def _fail(code: str, path: str, detail: str) -> AdapterFailure:
    return AdapterFailure(code, path, detail)


def _exact_object(value: Any, fields: set[str], path: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise _fail("RBWP7_PROTOCOL_OBJECT_INVALID", path, type(value).__name__)
    actual = set(value)
    if actual != fields:
        raise _fail(
            "RBWP7_PROTOCOL_FIELDS_INVALID",
            path,
            f"missing={sorted(fields - actual)!r}; unknown={sorted(actual - fields)!r}",
        )
    return value


def _integer(value: Any, path: str, *, minimum: int = 0) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise _fail("RBWP7_PROTOCOL_INTEGER_INVALID", path, type(value).__name__)
    if value < minimum or value > MAX_CANONICAL_INTEGER:
        raise _fail("RBWP7_PROTOCOL_INTEGER_RANGE", path, str(value))
    return value


def _text(value: Any, path: str) -> str:
    if not isinstance(value, str) or not value:
        raise _fail("RBWP7_PROTOCOL_TEXT_INVALID", path, repr(value))
    return value


@dataclass(frozen=True)
class ProtocolStop:
    stop_id: str
    raw_node: Any
    kind: str
    raw_request: Any | None
    service_duration_ms: int


@dataclass(frozen=True)
class ProtocolRoute:
    plan_version: int
    executed_stop_count: int
    frozen_prefix: tuple[ProtocolStop, ...]
    mutable_suffix: tuple[ProtocolStop, ...]

    @property
    def remaining_stops(self) -> tuple[ProtocolStop, ...]:
        return self.frozen_prefix[self.executed_stop_count :] + self.mutable_suffix


@dataclass(frozen=True)
class AcceptedAction:
    request_id: str
    raw_request: Any
    vehicle_id: str
    raw_vehicle: Any
    candidate_id: str


@dataclass(frozen=True)
class OutcomeAction:
    decision_type: str
    request_id: str
    raw_request: Any
    reason_code: str


@dataclass(frozen=True)
class PlanAction:
    vehicle_id: str
    raw_vehicle: Any
    candidate_id: str
    route: ProtocolRoute


@dataclass(frozen=True)
class PromiseAction:
    request_id: str
    raw_request: Any
    publication_id: str
    promise_version: int
    reason_code: str
    source_event_sequence: int
    # The projection's vehicle as sent, kept only for the G2 move check; it is never mapped
    # or validated here, so decisions without G2 parse exactly as before.
    vehicle_id: str | None = None


@dataclass(frozen=True)
class ParsedDecision:
    accepted: tuple[AcceptedAction, ...]
    outcomes: tuple[OutcomeAction, ...]
    plans: tuple[PlanAction, ...]
    promises: tuple[PromiseAction, ...]


class OrderedEventBuffer:
    """Buffers FleetPy callbacks and emits gapless, phase-ordered event batches."""

    def __init__(self) -> None:
        self._events: list[tuple[int, int, str, dict[str, Any]]] = []
        self._ordinal = 0
        self._next_event_sequence = 1
        self._next_epoch = 1
        self._last_callback_time_ms = 0
        self._last_batch_time_ms = 0
        self._dedupe: set[str] = set()

    @property
    def next_event_sequence(self) -> int:
        return self._next_event_sequence

    @property
    def next_epoch(self) -> int:
        return self._next_epoch

    @property
    def has_pending_events(self) -> bool:
        return bool(self._events)

    def append(
        self,
        simulation_time: Any,
        phase: int,
        event_type: str,
        payload: dict[str, Any],
        *,
        dedupe_key: str | None = None,
    ) -> None:
        time_ms = seconds_to_milliseconds(simulation_time, "$callback.simulationTime")
        if time_ms < self._last_callback_time_ms or time_ms < self._last_batch_time_ms:
            raise _fail(
                "RBWP7_CALLBACK_TIME_REGRESSION",
                "$callback.simulationTime",
                f"actual={time_ms}; prior={max(self._last_callback_time_ms, self._last_batch_time_ms)}",
            )
        if not isinstance(phase, int) or isinstance(phase, bool) or phase < 0:
            raise _fail("RBWP7_CALLBACK_PHASE_INVALID", "$callback.phase", repr(phase))
        _text(event_type, "$callback.eventType")
        if not isinstance(payload, dict):
            raise _fail("RBWP7_CALLBACK_PAYLOAD_INVALID", "$callback.payload", type(payload).__name__)
        if dedupe_key is not None:
            if dedupe_key in self._dedupe:
                raise _fail("RBWP7_CALLBACK_DUPLICATE", "$callback", dedupe_key)
            self._dedupe.add(dedupe_key)
        self._last_callback_time_ms = time_ms
        self._events.append((phase, self._ordinal, event_type, payload))
        self._ordinal += 1

    def drain(
        self,
        run_id: str,
        scenario_id: str,
        simulation_time: Any,
        *,
        prefix: Iterable[tuple[str, dict[str, Any]]] = (),
        append_timer_tick: bool = True,
    ) -> dict[str, Any]:
        time_ms = seconds_to_milliseconds(simulation_time, "$batch.simulationTime")
        if time_ms < self._last_callback_time_ms or time_ms < self._last_batch_time_ms:
            raise _fail(
                "RBWP7_BATCH_TIME_REGRESSION",
                "$batch.simulationTime",
                f"actual={time_ms}; prior={max(self._last_callback_time_ms, self._last_batch_time_ms)}",
            )
        ordered = list(prefix)
        ordered.extend(
            (event_type, payload)
            for _, _, event_type, payload in sorted(self._events)
        )
        if append_timer_tick:
            ordered.append(("timerTick", {}))
        events: list[dict[str, Any]] = []
        for event_type, payload in ordered:
            events.append(
                {
                    "eventSeq": self._next_event_sequence,
                    "eventType": event_type,
                    "payload": payload,
                }
            )
            self._next_event_sequence += 1
        batch = {
            "schemaVersion": "1.0.0",
            "messageType": "eventBatch",
            "runId": _text(run_id, "$batch.runId"),
            "scenarioId": _text(scenario_id, "$batch.scenarioId"),
            "epochId": self._next_epoch,
            "simTimeMs": time_ms,
            "payload": {"events": events},
        }
        self._next_epoch += 1
        self._last_batch_time_ms = time_ms
        self._events.clear()
        self._dedupe.clear()
        return batch


def parse_decision(
    decision: dict[str, Any],
    mapper: FleetPyProtocolMapper,
) -> ParsedDecision:
    try:
        raw_actions = decision["payload"]["actions"]
    except (KeyError, TypeError) as exc:
        raise _fail("RBWP7_DECISION_ACTIONS_MISSING", "$.decision.payload", str(exc)) from exc
    if not isinstance(raw_actions, list):
        raise _fail("RBWP7_DECISION_ACTIONS_INVALID", "$.decision.payload.actions", type(raw_actions).__name__)

    accepted: list[AcceptedAction] = []
    outcomes: list[OutcomeAction] = []
    plans: list[PlanAction] = []
    promises: list[PromiseAction] = []
    request_decisions: set[str] = set()
    planned_vehicles: set[str] = set()
    publication_ids: set[str] = set()

    for index, raw_action in enumerate(raw_actions):
        path = f"$.decision.payload.actions[{index}]"
        action = _exact_object(raw_action, {"decisionType", "payload"}, path)
        decision_type = _text(action["decisionType"], f"{path}.decisionType")
        payload = action["payload"]
        if decision_type == "requestAccepted":
            value = _exact_object(
                payload,
                {"requestId", "vehicleId", "candidateId"},
                f"{path}.payload",
            )
            request_id = _text(value["requestId"], f"{path}.payload.requestId")
            vehicle_id = _text(value["vehicleId"], f"{path}.payload.vehicleId")
            _unique(request_decisions, request_id, f"{path}.payload.requestId")
            accepted.append(
                AcceptedAction(
                    request_id,
                    mapper.raw_request(request_id),
                    vehicle_id,
                    mapper.raw_vehicle(vehicle_id),
                    _text(value["candidateId"], f"{path}.payload.candidateId"),
                )
            )
        elif decision_type in {"requestRejected", "requestDeferred"}:
            value = _exact_object(
                payload,
                {"requestId", "reasonCode"},
                f"{path}.payload",
            )
            request_id = _text(value["requestId"], f"{path}.payload.requestId")
            _unique(request_decisions, request_id, f"{path}.payload.requestId")
            outcomes.append(
                OutcomeAction(
                    decision_type,
                    request_id,
                    mapper.raw_request(request_id),
                    _text(value["reasonCode"], f"{path}.payload.reasonCode"),
                )
            )
        elif decision_type == "vehiclePlanUpdated":
            value = _exact_object(
                payload,
                {"vehicleId", "candidateId", "route"},
                f"{path}.payload",
            )
            vehicle_id = _text(value["vehicleId"], f"{path}.payload.vehicleId")
            _unique(planned_vehicles, vehicle_id, f"{path}.payload.vehicleId")
            plans.append(
                PlanAction(
                    vehicle_id,
                    mapper.raw_vehicle(vehicle_id),
                    _text(value["candidateId"], f"{path}.payload.candidateId"),
                    _route(value["route"], mapper, f"{path}.payload.route"),
                )
            )
        elif decision_type == "promisePublished":
            value = _exact_object(
                payload,
                {
                    "publicationId",
                    "promiseVersion",
                    "reasonCode",
                    "sourceEventSeq",
                    "promise",
                    "exogenousDelta",
                    "decisionDelta",
                    "visibleDelta",
                    "budgetBefore",
                    "budgetAfter",
                },
                f"{path}.payload",
            )
            promise = value["promise"]
            if not isinstance(promise, dict):
                raise _fail("RBWP7_PROMISE_INVALID", f"{path}.payload.promise", type(promise).__name__)
            request_id = _text(promise.get("requestId"), f"{path}.payload.promise.requestId")
            publication_id = _text(value["publicationId"], f"{path}.payload.publicationId")
            _unique(publication_ids, publication_id, f"{path}.payload.publicationId")
            for vector_name in (
                "exogenousDelta",
                "decisionDelta",
                "visibleDelta",
                "budgetBefore",
                "budgetAfter",
            ):
                _validate_vector(value[vector_name], f"{path}.payload.{vector_name}")
            promises.append(
                PromiseAction(
                    request_id,
                    mapper.raw_request(request_id),
                    publication_id,
                    _integer(value["promiseVersion"], f"{path}.payload.promiseVersion", minimum=1),
                    _text(value["reasonCode"], f"{path}.payload.reasonCode"),
                    _integer(value["sourceEventSeq"], f"{path}.payload.sourceEventSeq", minimum=1),
                    promise.get("vehicleId") if isinstance(promise.get("vehicleId"), str) else None,
                )
            )
        elif decision_type in {"offerProposed", "commitmentBreachDeclared"}:
            raise _fail("RBWP7_DECISION_TYPE_UNSUPPORTED", f"{path}.decisionType", decision_type)
        else:
            raise _fail("RBWP7_DECISION_TYPE_UNKNOWN", f"{path}.decisionType", decision_type)

    return ParsedDecision(tuple(accepted), tuple(outcomes), tuple(plans), tuple(promises))


def _route(value: Any, mapper: FleetPyProtocolMapper, path: str) -> ProtocolRoute:
    route = _exact_object(
        value,
        {"planVersion", "executedStopCount", "frozenPrefix", "mutableSuffix"},
        path,
    )
    frozen = _stops(route["frozenPrefix"], mapper, f"{path}.frozenPrefix")
    mutable = _stops(route["mutableSuffix"], mapper, f"{path}.mutableSuffix")
    executed = _integer(route["executedStopCount"], f"{path}.executedStopCount")
    if executed > len(frozen):
        raise _fail("RBWP7_ROUTE_PROGRESS_INVALID", f"{path}.executedStopCount", str(executed))
    ids = [stop.stop_id for stop in (*frozen, *mutable)]
    if len(ids) != len(set(ids)):
        raise _fail("RBWP7_ROUTE_STOP_DUPLICATE", path, repr(ids))
    return ProtocolRoute(
        _integer(route["planVersion"], f"{path}.planVersion"),
        executed,
        frozen,
        mutable,
    )


def _stops(
    value: Any,
    mapper: FleetPyProtocolMapper,
    path: str,
) -> tuple[ProtocolStop, ...]:
    if not isinstance(value, list):
        raise _fail("RBWP7_ROUTE_STOPS_INVALID", path, type(value).__name__)
    result: list[ProtocolStop] = []
    for index, raw in enumerate(value):
        stop_path = f"{path}[{index}]"
        stop = _exact_object(
            raw,
            {"stopId", "nodeId", "kind", "requestId", "serviceDurationMs"},
            stop_path,
        )
        kind = _text(stop["kind"], f"{stop_path}.kind")
        if kind not in {"waypoint", "pickup", "dropOff"}:
            raise _fail("RBWP7_ROUTE_STOP_KIND_INVALID", f"{stop_path}.kind", kind)
        request_id = stop["requestId"]
        if kind == "waypoint":
            if request_id is not None:
                raise _fail("RBWP7_ROUTE_WAYPOINT_REQUEST", f"{stop_path}.requestId", repr(request_id))
            raw_request = None
        else:
            request_id = _text(request_id, f"{stop_path}.requestId")
            raw_request = mapper.raw_request(request_id)
        result.append(
            ProtocolStop(
                _text(stop["stopId"], f"{stop_path}.stopId"),
                mapper.raw_node(_text(stop["nodeId"], f"{stop_path}.nodeId")),
                kind,
                raw_request,
                _integer(stop["serviceDurationMs"], f"{stop_path}.serviceDurationMs"),
            )
        )
    return tuple(result)


def _unique(values: set[str], value: str, path: str) -> None:
    if value in values:
        raise _fail("RBWP7_DECISION_DUPLICATE", path, value)
    values.add(value)


def _validate_vector(value: Any, path: str) -> None:
    vector = _exact_object(
        value,
        {
            "pickupEtaTotalMs",
            "dropEtaTotalMs",
            "materialEtaRevisionCount",
            "vehicleSwitchCount",
            "pickupStopRelocationMm",
            "pickupStopSwitchCount",
            "dropStopRelocationMm",
            "dropStopSwitchCount",
            "incumbentOrderInversionCount",
            "prePickupInsertedStopCount",
        },
        path,
    )
    for field, item in vector.items():
        _integer(item, f"{path}.{field}")
