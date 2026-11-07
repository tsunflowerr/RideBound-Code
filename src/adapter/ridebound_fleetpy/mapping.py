from __future__ import annotations

import hashlib
import json
import math
from os import PathLike
from decimal import Decimal, InvalidOperation, ROUND_FLOOR, ROUND_HALF_EVEN
from typing import Any, Callable, Iterable

from .errors import AdapterFailure


MAX_CANONICAL_INTEGER = 9_007_199_254_740_991


def _failure(code: str, path: str, detail: str) -> AdapterFailure:
    return AdapterFailure(code, path, detail)


def _typed_frame(value: Any, path: str = "$identity") -> bytes:
    """Injectively frame the supported FleetPy identity value vocabulary."""
    if value is None:
        return b"n"
    if isinstance(value, bool):
        raise _failure("RBWP7_IDENTITY_TYPE_INVALID", path, "bool is not an identity integer")
    if isinstance(value, int):
        encoded = str(value).encode("ascii")
        return b"i" + len(encoded).to_bytes(8, "big") + encoded
    if isinstance(value, str):
        encoded = value.encode("utf-8", errors="strict")
        return b"s" + len(encoded).to_bytes(8, "big") + encoded
    if isinstance(value, tuple):
        items = [_typed_frame(item, f"{path}[{index}]") for index, item in enumerate(value)]
        framed = bytearray(b"t" + len(items).to_bytes(8, "big"))
        for item in items:
            framed.extend(len(item).to_bytes(8, "big"))
            framed.extend(item)
        return bytes(framed)
    raise _failure(
        "RBWP7_IDENTITY_TYPE_INVALID",
        path,
        f"unsupported identity type {type(value).__name__}",
    )


class CanonicalIdRegistry:
    """Stable opaque IDs with collision detection and exact reverse lookup."""

    def __init__(
        self,
        namespace: str,
        digest: Callable[[bytes], str] | None = None,
    ) -> None:
        if not namespace or len(namespace.encode("ascii", errors="strict")) > 16:
            raise ValueError("namespace must contain 1..16 ASCII bytes")
        if not all(character.isalnum() or character == "-" for character in namespace):
            raise ValueError("namespace must be ASCII alphanumeric/hyphen")
        self._namespace = namespace
        self._digest = digest or (lambda value: hashlib.sha256(value).hexdigest())
        self._source_by_id: dict[str, bytes] = {}
        self._value_by_id: dict[str, Any] = {}

    def register(self, value: Any) -> str:
        framed = _typed_frame(value)
        opaque = f"{self._namespace}-{self._digest(framed)}"
        if len(opaque.encode("utf-8")) > 128:
            raise _failure("RBWP7_IDENTIFIER_OVERFLOW", "$identity", opaque)
        existing = self._source_by_id.get(opaque)
        if existing is not None and existing != framed:
            raise _failure("RBWP7_IDENTIFIER_COLLISION", "$identity", opaque)
        self._source_by_id[opaque] = framed
        self._value_by_id[opaque] = value
        return opaque

    def resolve(self, opaque: str) -> Any:
        try:
            return self._value_by_id[opaque]
        except KeyError as exc:
            raise _failure("RBWP7_IDENTIFIER_UNKNOWN", "$identity", opaque) from exc


def _decimal(value: Any, path: str) -> Decimal:
    if type(value).__module__.startswith("numpy") and hasattr(value, "item"):
        value = value.item()
    if isinstance(value, bool) or value is None:
        raise _failure("RBWP7_NUMBER_TYPE_INVALID", path, type(value).__name__)
    if isinstance(value, float) and not math.isfinite(value):
        raise _failure("RBWP7_NUMBER_NONFINITE", path, repr(value))
    if not isinstance(value, (int, float, str, Decimal)):
        raise _failure("RBWP7_NUMBER_TYPE_INVALID", path, type(value).__name__)
    try:
        parsed = Decimal(str(value))
    except (InvalidOperation, ValueError) as exc:
        raise _failure("RBWP7_NUMBER_INVALID", path, repr(value)) from exc
    if not parsed.is_finite():
        raise _failure("RBWP7_NUMBER_NONFINITE", path, repr(value))
    return parsed


def seconds_to_milliseconds(value: Any, path: str = "$time") -> int:
    milliseconds = (_decimal(value, path) * Decimal(1000)).quantize(
        Decimal(1), rounding=ROUND_HALF_EVEN
    )
    result = int(milliseconds)
    if result < 0 or result > MAX_CANONICAL_INTEGER:
        raise _failure("RBWP7_TIME_RANGE_INVALID", path, str(result))
    return result


def _validate_canonical(value: Any, path: str) -> None:
    if value is None or isinstance(value, (str, bool)):
        return
    if isinstance(value, int) and not isinstance(value, bool):
        if value < -MAX_CANONICAL_INTEGER or value > MAX_CANONICAL_INTEGER:
            raise _failure("RBWP7_CANONICAL_INTEGER_RANGE", path, str(value))
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_canonical(item, f"{path}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise _failure("RBWP7_CANONICAL_KEY_TYPE", path, type(key).__name__)
            _validate_canonical(item, f"{path}.{key}")
        return
    raise _failure("RBWP7_CANONICAL_TYPE_INVALID", path, type(value).__name__)


def canonical_json_bytes(value: Any) -> bytes:
    _validate_canonical(value, "$")
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def canonical_json_file_hash(path: str | PathLike[str]) -> str:
    try:
        with open(path, "r", encoding="utf-8", newline="") as source:
            value = json.load(source, object_pairs_hook=_strict_json_object)
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as exc:
        raise _failure("RBWP7_CONFIGURATION_INVALID", "$configuration", str(exc)) from exc
    return hashlib.sha256(canonical_json_bytes(value)).hexdigest()


def wp4_policy_binding_hash(
    commitment_path: str | PathLike[str],
    wp4_path: str | PathLike[str],
) -> str:
    commitment = bytes.fromhex(canonical_json_file_hash(commitment_path))
    wp4 = bytes.fromhex(canonical_json_file_hash(wp4_path))
    return hashlib.sha256(
        b"RideBound.Wp4RunnerConfigurationBinding.v1\0" + commitment + wp4
    ).hexdigest()


def _strict_json_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def _positive_integer(value: Any, path: str) -> int:
    if type(value).__module__.startswith("numpy") and hasattr(value, "item"):
        value = value.item()
    if isinstance(value, bool) or not isinstance(value, int):
        raise _failure("RBWP7_INTEGER_TYPE_INVALID", path, type(value).__name__)
    if value < 1 or value > MAX_CANONICAL_INTEGER:
        raise _failure("RBWP7_INTEGER_RANGE_INVALID", path, str(value))
    return value


class FleetPyProtocolMapper:
    """Strict, simulator-only mapping into RideBound v1 JSON values."""

    def __init__(self) -> None:
        self.requests = CanonicalIdRegistry("rq")
        self.vehicles = CanonicalIdRegistry("veh")
        self.nodes = CanonicalIdRegistry("node")
        self.edges = CanonicalIdRegistry("edge")
        self.stops = CanonicalIdRegistry("stop")

    def node_id(self, fleetpy_node: Any) -> str:
        if isinstance(fleetpy_node, bool) or not isinstance(fleetpy_node, (int, str)):
            raise _failure(
                "RBWP7_NODE_ID_TYPE_INVALID", "$position.node", type(fleetpy_node).__name__
            )
        return self.nodes.register(fleetpy_node)

    def raw_node(self, node_id: str) -> Any:
        return self.nodes.resolve(node_id)

    def raw_request(self, request_id: str) -> Any:
        return self.requests.resolve(request_id)

    def raw_vehicle(self, vehicle_id: str) -> Any:
        return self.vehicles.resolve(vehicle_id)

    def position(self, fleetpy_position: Any, path: str = "$position") -> dict[str, Any]:
        if not isinstance(fleetpy_position, tuple) or len(fleetpy_position) != 3:
            raise _failure("RBWP7_POSITION_SHAPE_INVALID", path, repr(fleetpy_position))
        start, end, relative = fleetpy_position
        start_id = self.node_id(start)
        if end is None and relative is None:
            return {"kind": "node", "nodeId": start_id}
        if end is None or relative is None:
            raise _failure("RBWP7_POSITION_PARTIAL_EDGE", path, repr(fleetpy_position))
        end_id = self.node_id(end)
        if start_id == end_id:
            raise _failure("RBWP7_POSITION_SELF_EDGE", path, repr(fleetpy_position))
        progress = _decimal(relative, f"{path}.relativeProgress")
        if progress < 0 or progress > 1:
            raise _failure("RBWP7_EDGE_PROGRESS_RANGE_INVALID", path, str(progress))
        # Floor, never nearest. The wire carries edge progress at 1/1000 of an
        # edge, so any rounding is a real position error: half a permille of a
        # 130 s edge is 65 ms. Rounding to nearest can place the vehicle ahead
        # of where it is, which makes every downstream ETA optimistic and lets
        # RideBound propose a plan FleetPy's own feasibility check rejects — one
        # Panel B job died that way, over its pickup window by 13 ms. Flooring
        # keeps RideBound behind the true position, and the validator already
        # rounds the remaining edge time up, so the pair is conservative on both
        # halves. Flooring is only ever applied within the edge the vehicle
        # has already entered - it must never move the vehicle back onto the
        # node behind it, which is a different place on a directed network.
        permille = int((progress * Decimal(1000)).quantize(Decimal(1), rounding=ROUND_FLOOR))
        if progress == 0:
            # Still standing on the from-node: not yet committed to the edge.
            return {"kind": "node", "nodeId": start_id}
        if permille >= 1000:
            return {"kind": "node", "nodeId": end_id}
        # Once the vehicle has entered the edge it cannot un-enter it, so the
        # from-node is not a conservative stand-in - it is unreachable except
        # the long way round. Measured on w14-d20181112-s10-r1-w17-b1-ref-s7:
        # a vehicle 0.126 permille along 86->8 was reported as standing on
        # node 86, so the core costed 86->39 at 574.616 s while the true cost
        # from its position was 792.244 s; returning to node 86 alone costs
        # 1352.586 s. The 217.628 s gap turned 25.384 s of believed slack into
        # a 192.244 s pickup-window violation that FleetPy rejected outright.
        # The wire cannot carry zero progress, so clamp to the smallest
        # representable step: one permille is the most conservative point the
        # contract can express, and it keeps the commitment to the edge.
        permille = max(permille, 1)
        return {
            "kind": "edgeProgress",
            "fromNodeId": start_id,
            "toNodeId": end_id,
            "edgeId": self.edges.register((start, end)),
            "progressPermille": permille,
        }

    def request(self, plan_request: Any, service_class: str, policy_id: str) -> dict[str, Any]:
        try:
            raw_request_id = plan_request.get_rid_struct()
            origin = plan_request.o_pos
            destination = plan_request.d_pos
            arrival = plan_request.rq_time
            earliest = plan_request.t_pu_earliest
            latest = plan_request.t_pu_latest
            max_ride = plan_request.max_trip_time
            party_size = plan_request.nr_pax
        except AttributeError as exc:
            raise _failure("RBWP7_REQUEST_CONTRACT_MISSING", "$request", str(exc)) from exc
        origin_id = self._request_node(origin, "$request.origin")
        destination_id = self._request_node(destination, "$request.destination")
        if origin_id == destination_id:
            raise _failure("RBWP7_REQUEST_ZERO_LENGTH", "$request", origin_id)
        arrival_ms = seconds_to_milliseconds(arrival, "$request.arrivalTime")
        earliest_ms = seconds_to_milliseconds(earliest, "$request.earliestPickup")
        latest_ms = seconds_to_milliseconds(latest, "$request.latestPickup")
        max_ride_ms = seconds_to_milliseconds(max_ride, "$request.maxRideTime")
        if not arrival_ms <= earliest_ms <= latest_ms:
            raise _failure(
                "RBWP7_REQUEST_TIME_ORDER_INVALID",
                "$request",
                f"{arrival_ms}<={earliest_ms}<={latest_ms}",
            )
        if max_ride_ms < 1:
            raise _failure("RBWP7_REQUEST_MAX_RIDE_INVALID", "$request.maxRideTime", str(max_ride_ms))
        if not isinstance(service_class, str) or not service_class:
            raise _failure("RBWP7_SERVICE_CLASS_INVALID", "$request.serviceClass", repr(service_class))
        if not isinstance(policy_id, str) or not policy_id:
            raise _failure("RBWP7_POLICY_ID_INVALID", "$request.commitmentPolicyId", repr(policy_id))
        return {
            "requestId": self.requests.register(raw_request_id),
            "arrivalTimeMs": arrival_ms,
            "originNodeId": origin_id,
            "destinationNodeId": destination_id,
            "earliestPickupMs": earliest_ms,
            "latestPickupMs": latest_ms,
            "maxRideTimeMs": max_ride_ms,
            "partySize": _positive_integer(party_size, "$request.partySize"),
            "serviceClass": service_class,
            "commitmentPolicyId": policy_id,
        }

    def travel_snapshot(
        self,
        version: int,
        fleetpy_arcs: Iterable[tuple[Any, Any, Any]],
    ) -> dict[str, Any]:
        version = _positive_integer(version, "$snapshot.version")
        by_pair: dict[tuple[str, str], dict[str, Any]] = {}
        for index, item in enumerate(fleetpy_arcs):
            if not isinstance(item, tuple) or len(item) != 3:
                raise _failure("RBWP7_TRAVEL_ARC_SHAPE_INVALID", f"$snapshot.arcs[{index}]", repr(item))
            raw_from, raw_to, seconds = item
            from_id = self.node_id(raw_from)
            to_id = self.node_id(raw_to)
            pair = (from_id, to_id)
            if from_id == to_id:
                raise _failure("RBWP7_TRAVEL_SELF_ARC", f"$snapshot.arcs[{index}]", from_id)
            if pair in by_pair:
                raise _failure("RBWP7_TRAVEL_ARC_DUPLICATE", f"$snapshot.arcs[{index}]", repr(pair))
            by_pair[pair] = {
                "fromNodeId": from_id,
                "toNodeId": to_id,
                "travelTimeMs": seconds_to_milliseconds(seconds, f"$snapshot.arcs[{index}].time"),
            }
        if not by_pair:
            raise _failure("RBWP7_TRAVEL_SNAPSHOT_EMPTY", "$snapshot.arcs", "no directed arcs")
        arcs = [by_pair[pair] for pair in sorted(by_pair)]
        content = {"version": version, "arcs": arcs}
        snapshot_hash = hashlib.sha256(
            b"RideBound.FleetPyTravelSnapshot.v1\0" + canonical_json_bytes(content)
        ).hexdigest()
        return {"version": version, "snapshotHash": snapshot_hash, "arcs": arcs}

    def stop_id(self, request_id: str, kind: str) -> str:
        if kind not in {"pickup", "dropOff"}:
            raise _failure("RBWP7_STOP_KIND_INVALID", "$stop.kind", repr(kind))
        raw_request = self.requests.resolve(request_id)
        return self.stops.register((raw_request, kind))

    def _request_node(self, position: Any, path: str) -> str:
        if not isinstance(position, tuple) or len(position) != 3:
            raise _failure("RBWP7_REQUEST_POSITION_SHAPE", path, repr(position))
        if position[1] is not None or position[2] is not None:
            raise _failure("RBWP7_REQUEST_POSITION_NOT_NODE", path, repr(position))
        return self.node_id(position[0])
