"""Tests for preflight_world_v1_1 (bin starts on the event clock), with fakes (no FleetPy).

A small event loop mirrors the medium preflight's clock (actual_fleetpy_medium_preflight.py:583-637): next time
= min(next arrival, physical boundary, drain timer), then trigger. Two fake "arms" differ only in their physical
boundaries. With v1 the bin switch lags and differs by arm; with v1.1 every arm switches exactly at bin * BIN_S.
Run: python -B test_preflight_world_v1_1.py
"""
from __future__ import annotations

import types

import preflight_world as pw
import preflight_world_v1_1 as v11
from world_v1 import BIN_S, AnchoredWorld, World, WorldConfig

# Not imported from test_preflight_world: that module runs its checks and raises SystemExit on import.
failures: list[str] = []
checks_run = 0


def check(name: str, condition: bool) -> None:
    global checks_run
    checks_run += 1
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
    """Closure over three nodes with the NetworkBasic methods preflight_world uses."""

    def __init__(self):
        self.nodes = [Node(i) for i in range(3)]
        for node in self.nodes:
            for other in self.nodes:
                if other is not node:
                    node.edges_to[other] = Edge(60.0 * (1 + abs(node.node_index - other.node_index)))

    def _set_edge_tt(self, origin, destination, value):
        self.nodes[origin].edges_to[self.nodes[destination]].set_tt(value)

    def _reset_internal_attributes_after_travel_time_update(self):
        pass


world = AnchoredWorld(World(WorldConfig(world_seed=7, sigma=0.2)))


# next_bin_start is strictly after now.
check("next bin after 0", v11.next_bin_start(0) == BIN_S)
check("next bin just before a start", v11.next_bin_start(BIN_S - 0.5) == BIN_S)
check("next bin exactly at a start", v11.next_bin_start(BIN_S) == 2 * BIN_S)
check("next bin is always later", all(v11.next_bin_start(t) > t for t in (0, 1, 899.999, 900, 1799, 5000.5)))

# The wrapper keeps an earlier physical boundary and inserts the bin start otherwise.
module = types.SimpleNamespace(_next_physical_boundary=lambda vehicles, routing, t: {0: 300, 400: None}.get(t, t + 2000))
state = v11.install_bin_boundaries(module)
check("earlier physical boundary kept", module._next_physical_boundary(None, None, 0) == 300)
check("no physical boundary -> bin start", module._next_physical_boundary(None, None, 400) == BIN_S)
check("later physical boundary -> bin start", module._next_physical_boundary(None, None, 1000) == 2 * BIN_S)
check("inserted boundaries counted", state["inserted"] == 2)


def run(period: float, use_v11: bool, end: float = 3700):
    """Switch times (bin -> t) of one fake arm whose vehicles hit a boundary every `period` seconds."""
    module = types.SimpleNamespace(
        _trigger_and_resolve=lambda *args: None,
        _next_physical_boundary=lambda vehicles, routing, t: (int(t // period) + 1) * period)
    world_state = pw.install(module, world)
    if use_v11:
        v11.install_bin_boundaries(module)
    control = types.SimpleNamespace(routing_engine=Routing(), _rb_travel_arc_cache={}, _rb_travel_dirty=False)
    arrivals = [2950]
    switches = {}
    now = 0
    module._trigger_and_resolve(control, set(), {}, now, set(), set())
    switches[world_state["edges"].bin] = now
    while True:
        physical = module._next_physical_boundary(None, None, now)
        if physical is not None and physical <= now:
            raise RuntimeError("non-positive physical boundary")
        nxt = min(value for value in (arrivals[0] if arrivals else None, physical) if value is not None)
        if nxt > end:
            return switches
        if arrivals and nxt == arrivals[0]:
            arrivals.pop(0)
        before = world_state["edges"].bin
        module._trigger_and_resolve(control, set(), {}, nxt, set(), set())
        if world_state["edges"].bin != before:
            switches[world_state["edges"].bin] = nxt
        now = nxt


old_a, old_b = run(700, False), run(1100, False)
check("v1 reproduces the pilot finding (lag)", any(old_a[b] > b * BIN_S for b in (1, 2, 3)))
check("v1 reproduces the pilot finding (arm dependent)", old_a != old_b)
new_a, new_b = run(700, True), run(1100, True)
check("v1.1 switches exactly at bin starts", all(new_a[b] == b * BIN_S for b in new_a))
check("v1.1 switch times are arm independent", new_a == new_b)
# v1 can miss a bin entirely when no event falls between its start and the end (bin 4 = 3600 s here).
check("v1.1 reaches every bin before the end", sorted(new_a) == [0, 1, 2, 3, 4])
check("v1 bins are a prefix of the v1.1 bins", set(old_a) <= set(new_a) and set(old_b) <= set(new_b))

print(f"v1.1: {checks_run} checks, {len(failures)} failure(s)")
raise SystemExit(1 if failures or checks_run < 14 else 0)  # 14 checks: a silent early exit must fail
