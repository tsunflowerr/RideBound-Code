"""T3.5: run the repository medium preflight inside world v1 (OD-pair noise), not a scalar factor.

WHAT CHANGES AGAINST TIER 2 (deadline/preflight_drift_v2.py)
------------------------------------------------------------
Tier 2 multiplied every routing query by one scalar `_current_tt_factor`. Here every edge of the
96-node OD closure gets its own factor from world v1, keyed by (worldSeed, (from, to), bin,
component) and anchored so that bin 0 is exactly the fixture (world_v1.AnchoredWorld). The factor
is applied through FleetPy's own dynamic-network hook `NetworkBasic._set_edge_tt`
(NetworkBasic.py:280-287), which updates the edge and both adjacency tables; NetworkBasic keeps no
route cache (`_reset_internal_attributes_after_travel_time_update` and `_add_to_database` are
no-ops), so no stale result survives a bin change.

WORLD AND FORECAST ARE SEPARATE
-------------------------------
FleetPy moves vehicles in the world. The Runner never reads the world: it receives
`travelTimesUpdated` snapshots that the adapter builds from FleetPy's routing at the moment of the
batch (shortest-path costs over the current edge times), and it holds them flat into the future.
That is the current-condition forecast. `EdgeTimes.apply` reads the world only at the bin that
contains the current time, so a change to any later bin cannot change what the Runner sees now
(checked in test_preflight_world.py).

The noise is per edge; on the closure the direct edge is the OD pair, but once edges are perturbed
the triangle inequality can fail and FleetPy may route an OD pair over two edges. The effective OD
time is then the shortest path, which is also what the snapshot reports. This is an OD closure, not
a street network. FleetPy holds each edge time constant within a bin; world_v1.travel_time's FIFO
integration is the oracle's notion and is not what FleetPy executes.

The repository is not modified; it is hash-checked by the preflight.

Usage: identical to the real preflight, plus in the environment
    RB_WORLD_CONFIG   path to a world-v1 JSON (strict keys; its SHA-256 is printed)
"""

from __future__ import annotations

import csv
import hashlib
import importlib.util
import json
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

from world_v1 import BIN_S, AnchoredWorld, World, WorldConfig  # noqa: E402

REPO = pathlib.Path(r"C:/Code/RideBound-g2")  # G2 copy (2026-10-23): the only line that differs from tang3/world
TARGET = REPO / "simulators/fleetpy-ridebound/actual_fleetpy_medium_preflight.py"
FACTORS = pathlib.Path(
    r"E:/RideBoundData/wp6/extracted/sha256/d9/"
    r"d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e/"
    r"FleetPy_Manhattan/networks/Manhattan_2019_corrected"
)
ALLOWED_WINDOW_STARTS = (28800, 61200)
WINDOW_SECONDS = 7200
CONFIG_KEYS = {
    "worldVersion", "worldSeed", "sigma", "rho", "shockRate", "shockMean",
    "shockHalfLife", "shockWindow", "base",
}
# The base is either the measured Tier-2 day or explicit per-bin values (the last value is held).
MEASURED_BASE_KEYS = {"day", "exponent", "windowStart"}
EXPLICIT_BASE_KEYS = {"values"}


def measured_base(day: str, exponent: float, window_start: int, bins: int) -> tuple[float, ...]:
    """v0 base per bin: the Tier-2 measured Manhattan ratio to the window start, raised to k."""
    if window_start not in ALLOWED_WINDOW_STARTS:
        raise SystemExit(f"unsupported window start {window_start}")
    path = FACTORS / f"{day}_tt_factors.csv"
    if not path.exists():
        raise SystemExit(f"no measured factors for {day}: {path}")
    rows = []
    with path.open(encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            seconds = int(row["simulation_time"])
            if window_start <= seconds <= window_start + WINDOW_SECONDS:
                rows.append((seconds - window_start, float(row["travel_time_factor"])))
    rows.sort()
    if not rows or rows[0][0] != 0:
        raise SystemExit(f"{day}: no factor row exactly at the window start {window_start}")
    base = rows[0][1]
    values = []
    for b in range(bins):
        current = rows[0][1]
        for seconds, factor in rows:
            if b * BIN_S >= seconds:
                current = factor
            else:
                break
        values.append((current / base) ** exponent)
    return tuple(values)


def load_world(path: pathlib.Path) -> tuple[AnchoredWorld, dict, str]:
    raw = path.read_bytes()
    config = json.loads(raw.decode("utf-8"))
    unknown = set(config) - CONFIG_KEYS
    missing = CONFIG_KEYS - {"base"} - set(config)
    if unknown or missing:
        raise SystemExit(f"world config keys: unknown={sorted(unknown)} missing={sorted(missing)}")
    if config["worldVersion"] != "world-v1":
        raise SystemExit(f"unsupported worldVersion {config['worldVersion']!r}")
    base = ()
    if config.get("base") is not None:
        spec = config["base"]
        if set(spec) == MEASURED_BASE_KEYS:
            # 24 bins cover the 2 h window plus any drain the fixtures allow.
            base = measured_base(
                spec["day"], float(spec["exponent"]), int(spec["windowStart"]), 24)
        elif set(spec) == EXPLICIT_BASE_KEYS:
            base = tuple(float(value) for value in spec["values"])
            if not base or any(value <= 0 for value in base):
                raise SystemExit("explicit world base values must be positive and non-empty")
        else:
            raise SystemExit(
                f"world base keys must be {sorted(MEASURED_BASE_KEYS)} or {sorted(EXPLICIT_BASE_KEYS)}")
    world = World(
        WorldConfig(
            world_seed=int(config["worldSeed"]),
            sigma=float(config["sigma"]),
            rho=float(config["rho"]),
            shock_rate=float(config["shockRate"]),
            shock_mean=float(config["shockMean"]),
            shock_half_life=float(config["shockHalfLife"]),
            shock_window=int(config["shockWindow"]),
            base=base,
        )
    )
    return AnchoredWorld(world), config, hashlib.sha256(raw).hexdigest()


class EdgeTimes:
    """Sets every routing edge to free-flow time x world factor of the bin containing `now`."""

    def __init__(self, routing, world):
        self.routing = routing
        self.world = world
        # Free-flow times as loaded from the fixture, before any change.
        self.free_flow = {
            (node.node_index, other.node_index): edge.get_tt()
            for node in routing.nodes
            for other, edge in node.edges_to.items()
        }
        self.bin = None
        self.reads: list[tuple[int, int]] = []   # (bin read, bin of now), for leakage tests

    def apply(self, simulation_time: float) -> bool:
        b = int(simulation_time // BIN_S)
        if b == self.bin:
            return False
        self.bin = b
        for (origin, destination), free_flow in self.free_flow.items():
            self.reads.append((b, b))
            factor = self.world.factor((origin, destination), b)
            self.routing._set_edge_tt(origin, destination, free_flow * factor)
        self.routing._reset_internal_attributes_after_travel_time_update()
        return True

    def current_range(self) -> tuple[float, float]:
        factors = [
            edge.get_tt() / self.free_flow[(node.node_index, other.node_index)]
            for node in self.routing.nodes
            for other, edge in node.edges_to.items()
            if self.free_flow[(node.node_index, other.node_index)] > 0
        ]
        return min(factors), max(factors)


def install(module, world) -> dict:
    """Wrap the preflight's trigger so each new bin updates the edges and marks the bridge dirty."""
    original = module._trigger_and_resolve
    state = {"edges": None, "changes": 0}

    def patched(control, request_ids, request_db, simulation_time,
                confirmed_ids, rejected_ids):
        if state["edges"] is None:
            state["edges"] = EdgeTimes(control.routing_engine, world)
        if state["edges"].apply(simulation_time):
            control._rb_travel_arc_cache.clear()
            control._rb_travel_dirty = True
            state["changes"] += 1
            low, high = state["edges"].current_range()
            print(f"[world] t={simulation_time:>6.0f}s bin={state['edges'].bin} "
                  f"edges={len(state['edges'].free_flow)} factor=[{low:.4f}, {high:.4f}]",
                  flush=True)
        return original(control, request_ids, request_db, simulation_time,
                        confirmed_ids, rejected_ids)

    module._trigger_and_resolve = patched
    return state


def main() -> int:
    path = os.environ.get("RB_WORLD_CONFIG")
    if not path:
        raise SystemExit("RB_WORLD_CONFIG is required")
    world, config, digest = load_world(pathlib.Path(path))

    spec = importlib.util.spec_from_file_location("rb_preflight_world", TARGET)
    if spec is None or spec.loader is None:
        raise SystemExit(f"cannot load {TARGET}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)

    state = install(module, world)
    print(f"[world] config={path} sha256={digest} seed={config['worldSeed']}", flush=True)
    result = module.main()
    print(f"[world] bin changes applied: {state['changes']}", flush=True)
    return result


if __name__ == "__main__":
    raise SystemExit(main())
