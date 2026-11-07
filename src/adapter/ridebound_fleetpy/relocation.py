"""Waiting-rider relocation (G2) on the adapter side.

The Runner reports a move with no dedicated action: the donor's and the receiver's
``vehiclePlanUpdated`` plus one ``promisePublished`` whose projection names the receiver.
This module infers the move from one parsed decision and checks it fail-closed, before
FleetPy applies any plan. It is pure: FleetPy state comes in as plain mappings.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable, Mapping

from .errors import AdapterFailure

ACTIVE_RIDER_STATES = frozenset({"offered", "confirmed", "boarded"})


@dataclass(frozen=True)
class Relocation:
    raw_request: Any
    raw_donor: Any
    raw_receiver: Any


def route_riders(stops: Iterable[Any]) -> frozenset[Any]:
    """Raw request ids that still have a remaining stop on a route."""
    return frozenset(stop.raw_request for stop in stops if stop.raw_request is not None)


def infer_waiting_rider_relocations(
    riders_before: Mapping[Any, frozenset[Any]],
    riders_after: Mapping[Any, frozenset[Any]],
    assigned: Mapping[Any, Any],
    states: Mapping[Any, str],
    promised_vehicle: Mapping[Any, Any],
    newly_accepted: frozenset[Any],
) -> tuple[Relocation, ...]:
    """Return the decision's moves (at most one) or raise.

    ``riders_before`` / ``riders_after``: for every vehicle with a changed plan, the riders of
    its current route and of its new route. ``assigned``: FleetPy's rider -> vehicle index
    before the decision. ``promised_vehicle``: rider -> vehicle named by this decision's
    ``promisePublished``.

    A rider entering a planned vehicle other than its own is a move. It must be a confirmed
    waiting rider whose own vehicle also has a new plan without it, and whose new promise
    names the receiver. A rider that leaves a plan in an active state without entering
    another is dropped, which never happens without a move, so it fails closed.
    """
    moves = []
    for receiver in sorted(riders_after, key=repr):
        for rider in sorted(riders_after[receiver] - riders_before.get(receiver, frozenset()), key=repr):
            if rider in newly_accepted:
                continue
            donor = assigned.get(rider)
            if donor is None or donor == receiver:
                continue
            if (
                states.get(rider) != "confirmed"
                or donor not in riders_after
                or rider in riders_after[donor]
                or rider not in riders_before.get(donor, frozenset())
                or promised_vehicle.get(rider) != receiver
            ):
                raise AdapterFailure(
                    "RBWP7_RELOCATION_INVALID",
                    "$.decision.vehiclePlanUpdated",
                    repr((rider, donor, receiver, states.get(rider), promised_vehicle.get(rider))),
                )
            moves.append(Relocation(rider, donor, receiver))
    if len(moves) > 1:
        raise AdapterFailure("RBWP7_RELOCATION_COUNT", "$.decision.vehiclePlanUpdated", repr(moves))
    moved = {move.raw_request for move in moves}
    for vehicle in sorted(riders_after, key=repr):
        for rider in sorted(riders_before.get(vehicle, frozenset()) - riders_after[vehicle], key=repr):
            if rider not in moved and states.get(rider) in ACTIVE_RIDER_STATES:
                raise AdapterFailure(
                    "RBWP7_RIDER_DROPPED",
                    f"$.vehicles[{vehicle}].route",
                    repr((rider, states.get(rider))),
                )
    return tuple(moves)
