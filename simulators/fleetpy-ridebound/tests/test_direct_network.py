"""ADR-089 (GĐ3): CompleteGraphDirectNetwork answers every routing query exactly as NetworkBasic does on a complete graph
whose direct edges are strictly shortest (every two-arc path is longer), for node and mid-edge positions."""

from __future__ import annotations

import csv
import os
import pathlib
import random
import sys
import tempfile
import unittest

ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))
FLEETPY_ROOT = os.environ.get("RIDEBOUND_FLEETPY_ROOT")
if FLEETPY_ROOT:
    sys.path.insert(0, FLEETPY_ROOT)
    from src.routing.NetworkBasic import NetworkBasic

    from ridebound_fleetpy.direct_network import CompleteGraphDirectNetwork


def _materialize(root: pathlib.Path, times: list[list[float]]) -> None:
    """The layout actual_fleetpy_medium_preflight._materialize_network writes."""
    base = root / "base"
    base.mkdir(parents=True)
    with (base / "nodes.csv").open("x", encoding="utf-8", newline="") as target:
        writer = csv.writer(target, lineterminator="\n")
        writer.writerow(["node_index", "is_stop_only", "pos_x", "pos_y"])
        for index in range(len(times)):
            writer.writerow([index, "True", index * 1000, 0])
    with (base / "edges.csv").open("x", encoding="utf-8", newline="") as target:
        writer = csv.writer(target, lineterminator="\n")
        writer.writerow(["from_node", "to_node", "distance", "travel_time", "source_edge_id"])
        ordinal = 0
        for origin, row in enumerate(times):
            for destination, seconds in enumerate(row):
                if origin != destination:
                    writer.writerow([origin, destination, seconds * 10, seconds, ordinal])
                    ordinal += 1
    (base / "crs.info").write_text("EPSG:32632\n", encoding="ascii")


@unittest.skipUnless(FLEETPY_ROOT, "RIDEBOUND_FLEETPY_ROOT is required")
class DirectNetworkTests(unittest.TestCase):
    def _pair(self, seed: int, size: int):
        random.seed(seed)
        # Every arc lies in [100, 190) seconds, so any two-arc path (>= 200 s) is strictly longer than the direct arc.
        times = [[0.0 if i == j else round(random.uniform(100.0, 190.0), 3) for j in range(size)] for i in range(size)]
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        _materialize(pathlib.Path(temporary.name) / "basic", times)
        _materialize(pathlib.Path(temporary.name) / "direct", times)
        return (NetworkBasic(str(pathlib.Path(temporary.name) / "basic")),
                CompleteGraphDirectNetwork(str(pathlib.Path(temporary.name) / "direct")))

    @staticmethod
    def _positions(size: int, count: int):
        positions = []
        for _ in range(count):
            origin = random.randrange(size)
            if random.random() < 0.5:
                positions.append((origin, None, None))
            else:
                other = random.choice([node for node in range(size) if node != origin])
                positions.append((origin, other, round(random.random(), 3)))
        return positions

    def assertCostsEqual(self, left, right):
        self.assertEqual(len(left), len(right))
        for a, b in zip(left, right):
            if isinstance(a, (int, float)):
                self.assertAlmostEqual(a, b, places=9)
            else:
                self.assertEqual(a, b)

    def test_one_to_one_costs_and_routes(self):
        compared = 0
        for seed in range(1, 6):
            basic, direct = self._pair(seed, 9)
            for origin in self._positions(9, 40):
                for destination in self._positions(9, 8):
                    self.assertCostsEqual(basic.return_travel_costs_1to1(origin, destination),
                                          direct.return_travel_costs_1to1(origin, destination))
                    self.assertEqual(basic.return_best_route_1to1(origin, destination),
                                     direct.return_best_route_1to1(origin, destination))
                    compared += 1
        self.assertGreater(compared, 1000)

    def test_many_to_one_and_one_to_many_with_limits(self):
        for seed in range(11, 16):
            basic, direct = self._pair(seed, 10)
            for trial in range(40):
                origins = self._positions(10, 6)
                destination = self._positions(10, 1)[0]
                if trial % 4 == 0:
                    # the start node among the targets: Router may stop before its first step
                    origins = [(destination[0], None, None)] + origins
                for max_routes, max_cost in ((None, None), (1, None), (2, None), (None, 300.0), (3, 150.0),
                                             (None, -1.0)):
                    left = basic.return_travel_costs_Xto1(origins, destination, max_routes, max_cost)
                    right = direct.return_travel_costs_Xto1(origins, destination, max_routes, max_cost)
                    self.assertEqual(len(left), len(right))
                    for a, b in zip(left, right):
                        self.assertCostsEqual(a, b)
                    left = basic.return_travel_costs_1toX(destination, origins, max_routes, max_cost)
                    right = direct.return_travel_costs_1toX(destination, origins, max_routes, max_cost)
                    self.assertEqual(len(left), len(right))
                    for a, b in zip(left, right):
                        self.assertCostsEqual(a, b)
                for max_cost in (None, 300.0, -1.0):
                    self.assertEqual(basic.return_best_route_Xto1(origins, destination, max_cost),
                                     direct.return_best_route_Xto1(origins, destination, max_cost))
                    self.assertEqual(basic.return_best_route_1toX(destination, origins, max_cost),
                                     direct.return_best_route_1toX(destination, origins, max_cost))

    def test_travel_cost_matrix_and_edge_updates(self):
        basic, direct = self._pair(21, 8)
        for network in (basic, direct):
            network._set_edge_tt(0, 1, 500.0)
            network._reset_internal_attributes_after_travel_time_update()
        positions = self._positions(8, 10)
        left = basic.return_travel_cost_matrix(positions)
        right = direct.return_travel_cost_matrix(positions)
        self.assertEqual(set(left), set(right))
        for key in left:
            # After the update 0->1 is no longer a shortest path: NetworkBasic routes around it, the direct network
            # does not. The complete-graph direct network is only valid on a closure, which the preflight checks.
            if key[0][0] == 0 and (key[0][1] in (None, 1)) and key[1][0] == 1:
                continue
            self.assertCostsEqual(left[key], right[key])


if __name__ == "__main__":
    unittest.main()
