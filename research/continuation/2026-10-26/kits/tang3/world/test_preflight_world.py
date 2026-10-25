"""Tests for preflight_world (T3.5 integration), with a fake routing network (no FleetPy).

Acceptance of T3.5: changing the future while keeping the past leaves what the Runner is given
unchanged; query order does not change the world (test_world_v1.py). Run: python -B test_preflight_world.py
"""
from __future__ import annotations

import json
import pathlib
import tempfile
import types

import preflight_world as pw
from world_v1 import BIN_S, AnchoredWorld, World, WorldConfig

failures: list[str] = []


def check(name: str, condition: bool) -> None:
    if not condition:
        failures.append(name)
        print("FAIL", name)


class Edge:
    def __init__(self, tt):
        self.tt = tt

    def get_tt(self):
        return self.tt

    def set_tt(self, value):
        self.tt = value


class Node:
    def __init__(self, index):
        self.node_index = index
        self.edges_to = {}


class Routing:
    """Closure over three nodes with the NetworkBasic methods the tool uses."""

    def __init__(self):
        self.nodes = [Node(i) for i in range(3)]
        for node in self.nodes:
            for other in self.nodes:
                if other is not node:
                    node.edges_to[other] = Edge(60.0 * (1 + abs(node.node_index - other.node_index)))
        self.resets = 0

    def _set_edge_tt(self, origin, destination, value):
        self.nodes[origin].edges_to[self.nodes[destination]].set_tt(value)

    def _reset_internal_attributes_after_travel_time_update(self):
        self.resets += 1

    def times(self):
        return {(n.node_index, o.node_index): e.get_tt() for n in self.nodes for o, e in n.edges_to.items()}


class FutureOverride:
    """World B: identical to A up to bin k, a different world afterwards."""

    def __init__(self, world, k, factor=3.0):
        self.world, self.k, self.value = world, k, factor

    def factor(self, od, b):
        return self.world.factor(od, b) if b <= self.k else self.value


world = AnchoredWorld(World(WorldConfig(world_seed=7, sigma=0.2)))

# Bin 0 is exactly the fixture.
routing = Routing()
free = routing.times()
edges = pw.EdgeTimes(routing, world)
check("bin 0 applies", edges.apply(0))
check("bin 0 is the fixture exactly", routing.times() == free)
check("same bin does nothing", not edges.apply(BIN_S - 1))

# Per-edge factors, not one scalar.
edges.apply(BIN_S)
ratios = {od: routing.times()[od] / free[od] for od in free}
check("edges get different factors", len({round(value, 12) for value in ratios.values()}) > 1)
check("factor matches the anchored world",
      all(abs(ratios[od] - world.factor(od, 1)) < 1e-12 for od in free))
check("reads only the bin of now", all(read == now for read, now in edges.reads))

# No leakage: changing bins after k never changes edge times before (k + 1) * BIN_S.
k = 2
times = [0, 450, BIN_S, BIN_S + 7, 2 * BIN_S, 3 * BIN_S - 1]
later = [3 * BIN_S, 4 * BIN_S]
routing_a, routing_b = Routing(), Routing()
edges_a = pw.EdgeTimes(routing_a, world)
edges_b = pw.EdgeTimes(routing_b, FutureOverride(world, k))
same_past = True
for t in times:
    edges_a.apply(t)
    edges_b.apply(t)
    same_past &= routing_a.times() == routing_b.times()
check("future change leaves the past unchanged", same_past)
edges_a.apply(later[0])
edges_b.apply(later[0])
check("the override is real (future differs)", routing_a.times() != routing_b.times())

# The installed wrapper: new bin -> apply, clear the bridge cache, mark dirty; call the original.
calls = []
module = types.SimpleNamespace(_trigger_and_resolve=lambda *args: calls.append(args[3]))
state = pw.install(module, world)
control = types.SimpleNamespace(
    routing_engine=Routing(), _rb_travel_arc_cache={"x": 1}, _rb_travel_dirty=False)
module._trigger_and_resolve(control, set(), {}, 0, set(), set())
check("first call marks the snapshot dirty", control._rb_travel_dirty and not control._rb_travel_arc_cache)
control._rb_travel_dirty = False
control._rb_travel_arc_cache["x"] = 1
module._trigger_and_resolve(control, set(), {}, 100, set(), set())
check("same bin keeps the cache and the flag", not control._rb_travel_dirty and control._rb_travel_arc_cache)
module._trigger_and_resolve(control, set(), {}, BIN_S, set(), set())
check("new bin marks dirty again", control._rb_travel_dirty and not control._rb_travel_arc_cache)
check("original trigger always runs", calls == [0, 100, BIN_S])
check("two bin changes counted", state["changes"] == 2)

# Strict world config.
good = {
    "worldVersion": "world-v1", "worldSeed": 7, "sigma": 0.08, "rho": 0.7, "shockRate": 0.02,
    "shockMean": 0.4, "shockHalfLife": 2.0, "shockWindow": 12, "base": None,
}
with tempfile.TemporaryDirectory() as directory:
    path = pathlib.Path(directory) / "world.json"
    path.write_text(json.dumps(good), encoding="utf-8")
    loaded, config, digest = pw.load_world(path)
    check("config loads and hashes", config["worldSeed"] == 7 and len(digest) == 64)
    check("loaded world is anchored", loaded.factor(("a", "b"), 0) == 1.0)
    # Explicit per-bin base: equal worlds up to the change, different after it.
    path.write_text(json.dumps({**good, "base": {"values": [1.0, 1.0, 1.0, 1.0]}}), encoding="utf-8")
    flat, _, _ = pw.load_world(path)
    path.write_text(json.dumps({**good, "base": {"values": [1.0, 1.0, 1.0, 1.0, 1.5]}}), encoding="utf-8")
    raised, _, _ = pw.load_world(path)
    check("explicit base keeps the past", all(
        flat.factor(("a", "b"), b) == raised.factor(("a", "b"), b) for b in range(4)))
    check("explicit base changes the future",
          abs(raised.factor(("a", "b"), 4) / flat.factor(("a", "b"), 4) - 1.5) < 1e-12)
    for bad in ({**good, "extra": 1}, {**good, "worldVersion": "world-v2"},
                {key: value for key, value in good.items() if key != "sigma"},
                {**good, "base": {"values": []}}, {**good, "base": {"values": [1.0, 0.0]}},
                {**good, "base": {"day": "2018-11-14"}}):
        path.write_text(json.dumps(bad), encoding="utf-8")
        try:
            pw.load_world(path)
            check(f"rejects {json.dumps(bad, sort_keys=True)}", False)
        except SystemExit:
            pass

print(f"{len(failures)} failure(s)")
raise SystemExit(1 if failures else 0)
