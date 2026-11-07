from __future__ import annotations

import math
import pathlib
import random
import sys
import unittest
from dataclasses import dataclass
from decimal import Decimal


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

from ridebound_fleetpy.errors import AdapterFailure
from ridebound_fleetpy.mapping import (
    MAX_CANONICAL_INTEGER,
    CanonicalIdRegistry,
    FleetPyProtocolMapper,
    canonical_json_bytes,
    canonical_json_file_hash,
    seconds_to_milliseconds,
    wp4_policy_binding_hash,
)


@dataclass
class _PlanRequest:
    rid: object = (7, "sub")
    o_pos: tuple = (10, None, None)
    d_pos: tuple = (20, None, None)
    rq_time: object = Decimal("1.0005")
    t_pu_earliest: object = Decimal("1.0015")
    t_pu_latest: object = Decimal("2.0005")
    max_trip_time: object = Decimal("30.0005")
    nr_pax: int = 2

    def get_rid_struct(self):
        return self.rid


class CanonicalIdentityTests(unittest.TestCase):
    def test_type_and_length_framing_distinguishes_adversarial_values(self) -> None:
        registry = CanonicalIdRegistry("id")
        values = [1, "1", (1,), ("1",), (1, 23), (12, 3), None, (None,)]
        mapped = [registry.register(value) for value in values]
        self.assertEqual(len(values), len(set(mapped)))
        self.assertEqual(mapped, [registry.register(value) for value in values])
        self.assertEqual(values, [registry.resolve(value) for value in mapped])

    def test_digest_collision_fails_closed(self) -> None:
        registry = CanonicalIdRegistry("id", digest=lambda _value: "0" * 64)
        registry.register(1)
        with self.assertRaises(AdapterFailure) as raised:
            registry.register("1")
        self.assertEqual("RBWP7_IDENTIFIER_COLLISION", raised.exception.code)

    def test_mutable_or_ambiguous_identity_types_are_rejected(self) -> None:
        registry = CanonicalIdRegistry("id")
        for value in [True, 1.0, [1], {"x": 1}, b"1"]:
            with self.subTest(value=value), self.assertRaises(AdapterFailure) as raised:
                registry.register(value)
            self.assertEqual("RBWP7_IDENTITY_TYPE_INVALID", raised.exception.code)

    def test_seeded_identity_permutations_are_stable_and_unique(self) -> None:
        rng = random.Random(2_607_003)
        values = [(rng.randint(-10_000, 10_000), f"r-{index}") for index in range(256)]
        first = CanonicalIdRegistry("rq")
        second = CanonicalIdRegistry("rq")
        first_ids = {value: first.register(value) for value in values}
        rng.shuffle(values)
        second_ids = {value: second.register(value) for value in values}
        self.assertEqual(first_ids, second_ids)
        self.assertEqual(256, len(set(first_ids.values())))


class CanonicalUnitTests(unittest.TestCase):
    def test_seconds_use_decimal_round_ties_to_even(self) -> None:
        cases = {
            "0.0005": 0,
            "0.0015": 2,
            "1.2345": 1234,
            "1.2355": 1236,
            Decimal("2.0005"): 2000,
        }
        for source, expected in cases.items():
            with self.subTest(source=source):
                self.assertEqual(expected, seconds_to_milliseconds(source))

    def test_pinned_fleetpy_numpy_scalars_are_normalized_explicitly(self) -> None:
        import numpy

        self.assertEqual(1500, seconds_to_milliseconds(numpy.float64(1.5)))
        self.assertEqual(2000, seconds_to_milliseconds(numpy.int64(2)))
        with self.assertRaises(AdapterFailure):
            seconds_to_milliseconds(numpy.bool_(True))

    def test_time_rejects_nonfinite_negative_and_overflow(self) -> None:
        invalid = [math.nan, math.inf, -0.001, str(MAX_CANONICAL_INTEGER)]
        for source in invalid:
            with self.subTest(source=source), self.assertRaises(AdapterFailure):
                seconds_to_milliseconds(source)

    def test_canonical_json_is_permutation_stable_utf8_lf_free(self) -> None:
        left = {"z": [3, 2, 1], "a": {"é": "值", "b": True}}
        right = {"a": {"b": True, "é": "值"}, "z": [3, 2, 1]}
        self.assertEqual(canonical_json_bytes(left), canonical_json_bytes(right))
        self.assertNotIn(b"\n", canonical_json_bytes(left))
        self.assertIn("值".encode("utf-8"), canonical_json_bytes(left))

    def test_canonical_json_rejects_float_and_non_string_key(self) -> None:
        for source in [{"x": 1.0}, {1: "x"}, {"x": b"raw"}]:
            with self.subTest(source=source), self.assertRaises(AdapterFailure):
                canonical_json_bytes(source)

    def test_cross_language_configuration_hash_and_binding_are_stable(self) -> None:
        repository = ADAPTER_ROOT.parents[1]
        commitment = repository / "src" / "core" / "benchmarks" / "configurations" / "wp3-boundary-test-v1.json"
        wp4 = repository / "src" / "core" / "benchmarks" / "configurations" / "wp7-fleetpy-rolling-cost-v1.json"
        self.assertEqual(
            "d1be06163dd38de567e4489100acd05b74c41cc454300f7b7286b459355e928f",
            canonical_json_file_hash(commitment),
        )
        binding = wp4_policy_binding_hash(commitment, wp4)
        self.assertEqual(64, len(binding))
        self.assertNotEqual(canonical_json_file_hash(commitment), binding)
        self.assertNotEqual(canonical_json_file_hash(wp4), binding)


class PositionAndRequestMappingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.mapper = FleetPyProtocolMapper()

    def test_edge_progress_floors_so_the_vehicle_is_never_placed_ahead(self) -> None:
        # Permille is 1/1000 of an edge, so rounding to nearest can put the
        # vehicle ahead of where it actually is and make every downstream ETA
        # optimistic. Flooring keeps RideBound behind the true position.
        edge = self.mapper.position((10, 20, Decimal("0.0015")))
        almost_end = self.mapper.position((10, 20, Decimal("0.9995")))
        end = self.mapper.position((10, 20, Decimal("1")))
        self.assertEqual("edgeProgress", edge["kind"])
        self.assertEqual(1, edge["progressPermille"])
        self.assertEqual("edgeProgress", almost_end["kind"])
        self.assertEqual(999, almost_end["progressPermille"])
        self.assertEqual(self.mapper.node_id(20), end["nodeId"])
        self.assertNotEqual(edge["fromNodeId"], edge["toNodeId"])

    def test_an_entered_edge_is_never_reported_as_the_node_behind_it(self) -> None:
        """Flooring must stay inside the edge, never step back off it.

        A vehicle a fraction of a permille along 86->8 used to be reported as
        standing on node 86. On a directed network that is not a conservative
        approximation, it is a different place: the vehicle cannot un-enter
        the edge. On w14-d20181112-s10-r1-w17-b1-ref-s7 the core costed 86->39
        at 574.616 s from node 86 while the true cost from the vehicle's
        position was 792.244 s - returning to node 86 alone costs 1352.586 s.
        The 217.628 s gap turned 25.384 s of believed slack into a 192.244 s
        pickup-window violation and halted the matrix.
        """
        node = self.mapper.position((10, None, None))
        just_entered = self.mapper.position((10, 20, Decimal("0.000126")))
        self.assertEqual("node", node["kind"])
        self.assertEqual("edgeProgress", just_entered["kind"])
        self.assertEqual(self.mapper.node_id(10), just_entered["fromNodeId"])
        self.assertEqual(self.mapper.node_id(20), just_entered["toNodeId"])
        # One permille is the smallest step the wire can carry, so it is the
        # most conservative point the contract can express for an entered edge.
        self.assertEqual(1, just_entered["progressPermille"])

    def test_standing_on_the_from_node_is_still_a_node(self) -> None:
        # Exactly zero progress means the vehicle has not entered the edge yet
        # and is free to leave the node in any direction.
        at_node = self.mapper.position((10, 20, Decimal("0")))
        self.assertEqual("node", at_node["kind"])
        self.assertEqual(self.mapper.node_id(10), at_node["nodeId"])

    def test_every_entered_edge_stays_within_the_wire_contract(self) -> None:
        # The core rejects any permille outside 1..999, so a clamp that fell
        # to 0 or ran to 1000 would fail the conversation rather than the plan.
        for numerator in ("0.0000001", "0.0004", "0.5", "0.9994", "0.999999"):
            with self.subTest(progress=numerator):
                mapped = self.mapper.position((10, 20, Decimal(numerator)))
                self.assertEqual("edgeProgress", mapped["kind"])
                self.assertGreaterEqual(mapped["progressPermille"], 1)
                self.assertLessEqual(mapped["progressPermille"], 999)

    def test_position_rejects_partial_reverse_self_and_invalid_fraction(self) -> None:
        invalid = [
            (10, None, 0.5),
            (10, 10, 0.5),
            (10, 20, -0.1),
            (10, 20, 1.1),
            (10, 20, math.nan),
            [10, 20, 0.5],
        ]
        for position in invalid:
            with self.subTest(position=position), self.assertRaises(AdapterFailure):
                self.mapper.position(position)

    def test_plan_request_maps_exact_fleetpy_constraint_fields(self) -> None:
        mapped = self.mapper.request(_PlanRequest(), "standard", "uniform-v1")
        self.assertEqual(1000, mapped["arrivalTimeMs"])
        self.assertEqual(1002, mapped["earliestPickupMs"])
        self.assertEqual(2000, mapped["latestPickupMs"])
        self.assertEqual(30000, mapped["maxRideTimeMs"])
        self.assertEqual(2, mapped["partySize"])
        self.assertEqual((7, "sub"), self.mapper.requests.resolve(mapped["requestId"]))

    def test_request_rejects_edge_stop_bad_order_party_and_zero_trip(self) -> None:
        mutations = [
            _PlanRequest(o_pos=(10, 11, 0.2)),
            _PlanRequest(t_pu_earliest=3, t_pu_latest=2),
            _PlanRequest(nr_pax=True),
            _PlanRequest(d_pos=(10, None, None)),
            _PlanRequest(max_trip_time=0),
        ]
        for request in mutations:
            with self.subTest(request=request), self.assertRaises(AdapterFailure):
                self.mapper.request(request, "standard", "uniform-v1")


class TravelSnapshotMappingTests(unittest.TestCase):
    def test_directed_sparse_snapshot_is_permutation_deterministic(self) -> None:
        arcs = [(10, 20, "1.0005"), (20, 10, "2.0015"), (20, 30, 0)]
        left = FleetPyProtocolMapper().travel_snapshot(1, arcs)
        right = FleetPyProtocolMapper().travel_snapshot(1, reversed(arcs))
        self.assertEqual(left, right)
        self.assertEqual(3, len(left["arcs"]))
        self.assertEqual([0, 1000, 2002], sorted(arc["travelTimeMs"] for arc in left["arcs"]))

    def test_missing_reverse_arc_is_not_invented(self) -> None:
        snapshot = FleetPyProtocolMapper().travel_snapshot(1, [(10, 20, 1)])
        self.assertEqual(1, len(snapshot["arcs"]))

    def test_duplicate_self_empty_and_malformed_arcs_fail_closed(self) -> None:
        mutations = [
            [(10, 20, 1), (10, 20, 2)],
            [(10, 10, 1)],
            [],
            [(10, 20)],
            [(10, 20, math.inf)],
        ]
        for arcs in mutations:
            with self.subTest(arcs=arcs), self.assertRaises(AdapterFailure):
                FleetPyProtocolMapper().travel_snapshot(1, arcs)


if __name__ == "__main__":
    unittest.main()
