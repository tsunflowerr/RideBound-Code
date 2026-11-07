from __future__ import annotations

import copy
import pathlib
import sys
import unittest


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

from ridebound_fleetpy.errors import AdapterFailure
from ridebound_fleetpy.mapping import FleetPyProtocolMapper
from ridebound_fleetpy.protocol import OrderedEventBuffer, parse_decision


def _mapper() -> tuple[FleetPyProtocolMapper, str, str, str, str]:
    mapper = FleetPyProtocolMapper()
    request_id = mapper.requests.register(("traveler", 1))
    vehicle_id = mapper.vehicles.register((0, 7))
    origin_id = mapper.node_id(11)
    destination_id = mapper.node_id(12)
    return mapper, request_id, vehicle_id, origin_id, destination_id


def _decision() -> tuple[dict, FleetPyProtocolMapper]:
    mapper, request_id, vehicle_id, origin_id, destination_id = _mapper()
    zero_vector = {
        "pickupEtaTotalMs": 0,
        "dropEtaTotalMs": 0,
        "materialEtaRevisionCount": 0,
        "vehicleSwitchCount": 0,
        "pickupStopRelocationMm": 0,
        "pickupStopSwitchCount": 0,
        "dropStopRelocationMm": 0,
        "dropStopSwitchCount": 0,
        "incumbentOrderInversionCount": 0,
        "prePickupInsertedStopCount": 0,
    }
    route = {
        "planVersion": 1,
        "executedStopCount": 0,
        "frozenPrefix": [],
        "mutableSuffix": [
            {
                "stopId": "pickup-stop",
                "nodeId": origin_id,
                "kind": "pickup",
                "requestId": request_id,
                "serviceDurationMs": 500,
            },
            {
                "stopId": "drop-stop",
                "nodeId": destination_id,
                "kind": "dropOff",
                "requestId": request_id,
                "serviceDurationMs": 500,
            },
        ],
    }
    actions = [
        {
            "decisionType": "requestAccepted",
            "payload": {
                "requestId": request_id,
                "vehicleId": vehicle_id,
                "candidateId": "candidate-1",
            },
        },
        {
            "decisionType": "vehiclePlanUpdated",
            "payload": {
                "vehicleId": vehicle_id,
                "candidateId": "candidate-1",
                "route": route,
            },
        },
        {
            "decisionType": "promisePublished",
            "payload": {
                "publicationId": "publication-1",
                "promiseVersion": 1,
                "reasonCode": "INITIAL_BOOKING_CONFIRMATION",
                "sourceEventSeq": 4,
                "promise": {"requestId": request_id},
                "exogenousDelta": zero_vector,
                "decisionDelta": zero_vector,
                "visibleDelta": zero_vector,
                "budgetBefore": zero_vector,
                "budgetAfter": zero_vector,
            },
        },
    ]
    return {"payload": {"actions": actions}}, mapper


class OrderedEventBufferTests(unittest.TestCase):
    def test_orders_semantic_phases_and_assigns_gapless_sequences(self) -> None:
        buffer = OrderedEventBuffer()
        buffer.append(1, 40, "passengerBoarded", {"requestId": "r"})
        buffer.append(1, 20, "vehicleReachedStop", {"stopId": "s"})
        first = buffer.drain(
            "run",
            "scenario",
            1,
            prefix=[("travelTimesUpdated", {"snapshot": {}})],
        )
        second = buffer.drain("run", "scenario", 2)

        self.assertEqual(1, first["epochId"])
        self.assertEqual(
            [
                "travelTimesUpdated",
                "vehicleReachedStop",
                "passengerBoarded",
                "timerTick",
            ],
            [event["eventType"] for event in first["payload"]["events"]],
        )
        self.assertEqual(
            [1, 2, 3, 4],
            [event["eventSeq"] for event in first["payload"]["events"]],
        )
        self.assertEqual(2, second["epochId"])
        self.assertEqual(
            [{"eventSeq": 5, "eventType": "timerTick", "payload": {}}],
            second["payload"]["events"],
        )

    def test_rejects_duplicate_and_time_regression(self) -> None:
        buffer = OrderedEventBuffer()
        buffer.append(2, 10, "requestArrived", {}, dedupe_key="request:1")
        with self.assertRaises(AdapterFailure) as duplicate:
            buffer.append(2, 10, "requestArrived", {}, dedupe_key="request:1")
        self.assertEqual("RBWP7_CALLBACK_DUPLICATE", duplicate.exception.code)
        with self.assertRaises(AdapterFailure) as regression:
            buffer.append(1, 10, "timerTick", {})
        self.assertEqual("RBWP7_CALLBACK_TIME_REGRESSION", regression.exception.code)


class DecisionParserTests(unittest.TestCase):
    def test_resolves_ids_and_preserves_route_membership(self) -> None:
        decision, mapper = _decision()
        parsed = parse_decision(decision, mapper)

        accepted = parsed.accepted[0]
        self.assertEqual(("traveler", 1), accepted.raw_request)
        self.assertEqual((0, 7), accepted.raw_vehicle)
        self.assertEqual(11, parsed.plans[0].route.remaining_stops[0].raw_node)
        self.assertEqual(
            ("traveler", 1),
            parsed.plans[0].route.remaining_stops[1].raw_request,
        )
        self.assertEqual(
            "INITIAL_BOOKING_CONFIRMATION",
            parsed.promises[0].reason_code,
        )

    def test_mutations_fail_closed(self) -> None:
        mutations = [
            (
                lambda value: value["payload"]["actions"].append(
                    copy.deepcopy(value["payload"]["actions"][0])
                ),
                "RBWP7_DECISION_DUPLICATE",
            ),
            (
                lambda value: value["payload"]["actions"][1]["payload"][
                    "route"
                ].update({"executedStopCount": 1}),
                "RBWP7_ROUTE_PROGRESS_INVALID",
            ),
            (
                lambda value: value["payload"]["actions"][0].update(
                    {"decisionType": "nativeFleetPyOptimization"}
                ),
                "RBWP7_DECISION_TYPE_UNKNOWN",
            ),
            (
                lambda value: value["payload"]["actions"][1]["payload"][
                    "route"
                ]["mutableSuffix"][0].update({"nodeId": "node-unknown"}),
                "RBWP7_IDENTIFIER_UNKNOWN",
            ),
        ]
        for mutate, expected_code in mutations:
            with self.subTest(expected_code=expected_code):
                decision, mapper = _decision()
                mutated = copy.deepcopy(decision)
                mutate(mutated)
                with self.assertRaises(AdapterFailure) as raised:
                    parse_decision(mutated, mapper)
                self.assertEqual(expected_code, raised.exception.code)


if __name__ == "__main__":
    unittest.main()
