"""Tests for world_v1 (T3.5 acceptance: determinism, query-order independence, no leakage, FIFO,
positivity, two-sidedness, shock recovery). Run: python -B test_world_v1.py"""
from __future__ import annotations

import itertools

from world_v1 import BIN_S, AnchoredWorld, CurrentConditionForecast, World, WorldConfig

failures: list[str] = []


def check(name: str, condition: bool) -> None:
    if not condition:
        failures.append(name)
        print("FAIL", name)


ods = [("n1", "n2"), ("n2", "n1"), ("n3", "n7")]
world = World(WorldConfig(world_seed=7))

# Determinism and query-order independence: same key -> same value, whatever was asked before.
a = [world.factor(od, b) for od in ods for b in range(8)]
fresh = World(WorldConfig(world_seed=7))
for od, b in reversed(list(itertools.product(ods, range(8)))):
    fresh.factor(od, b)
b_values = [fresh.factor(od, b) for od in ods for b in range(8)]
check("deterministic and order independent", a == b_values)

# Different seeds give different worlds; different OD pairs are not copies of each other.
other = World(WorldConfig(world_seed=8))
check("seed matters", any(world.factor(ods[0], b) != other.factor(ods[0], b) for b in range(8)))
check("pairs differ", [world.factor(ods[0], b) for b in range(8)] != [world.factor(ods[1], b) for b in range(8)])

# Positivity.
check("positive", all(world.factor(od, b) > 0 for od in ods for b in range(96)))

# Two-sidedness: over many pairs and bins the log-noise-only factor goes both above and below 1.
calm = World(WorldConfig(world_seed=11, shock_rate=0.0))
values = [calm.factor((f"o{i}", f"d{i}"), b) for i in range(40) for b in range(8)]
check("two-sided", min(values) < 1.0 < max(values))

# Shock recovery: with shocks only, a started shock decays by the half-life.
shocky = World(WorldConfig(world_seed=3, sigma=0.0, shock_rate=1.0, shock_mean=0.4, shock_window=0))
# shock_window=0: only the shock of the current bin counts, so f = 1 + magnitude of this bin.
check("shock raises the factor", all(shocky.factor(("a", "b"), b) >= 1.0 for b in range(10)))
decay = World(WorldConfig(world_seed=3, sigma=0.0, shock_rate=0.0, shock_window=12))
check("no shocks, no noise -> factor 1", all(abs(decay.factor(("a", "b"), b) - 1.0) < 1e-12 for b in range(10)))
one = World(WorldConfig(world_seed=5, sigma=0.0, shock_rate=0.3, shock_half_life=2.0))
series = [one.factor(("x", "y"), b) for b in range(40)]
peaks = [i for i in range(1, 39) if series[i] > series[i - 1] and series[i] >= series[i + 1]]
check("shocks recover (some later bin is lower than a peak)",
      any(series[p + 2] < series[p] for p in peaks if p + 2 < 40))

# Base (v0) component multiplies in.
based = World(WorldConfig(world_seed=7, base=(1.0, 1.2, 1.5)))
check("base applied", abs(based.factor(ods[0], 2) / world.factor(ods[0], 2) - 1.5) < 1e-12)
check("base held after last bin", abs(based.factor(ods[0], 5) / world.factor(ods[0], 5) - 1.5) < 1e-12)

# FIFO: departing later never arrives earlier, including across bin boundaries.
fifo_ok = True
for od in ods:
    for depart in range(0, 4 * BIN_S, 37):
        early = depart + world.travel_time(od, depart, 600)
        later = (depart + 37) + world.travel_time(od, depart + 37, 600)
        fifo_ok &= later >= early - 1e-9
check("FIFO", fifo_ok)

# Travel time with factor 1 equals free flow; zero free-flow gives zero.
check("free flow", abs(decay.travel_time(("a", "b"), 100.0, 600.0) - 600.0) < 1e-9)
check("zero distance", world.travel_time(ods[0], 100.0, 0.0) == 0.0)

# No leakage: changing the FUTURE of the world does not change a forecast issued now, and the
# forecast only reads bins that have started.
forecast = CurrentConditionForecast(world)
now = 2 * BIN_S + 10
value = forecast.travel_time(ods[0], now, 600)
future_changed = World(WorldConfig(world_seed=7, base=(1.0, 1.0, 1.0, 9.0, 9.0)))
check("forecast ignores the future",
      abs(CurrentConditionForecast(future_changed).travel_time(ods[0], now, 600) - value) < 1e-9)
check("forecast reads only started bins", all(b <= int(now // BIN_S) for b in forecast.reads))

# Anchored world: exactly 1 at bin 0 (the run starts on the fixture's travel times), the ratio to
# bin 0 afterwards, and still independent of the future.
anchored = AnchoredWorld(world)
check("anchored bin 0 is exactly 1", all(anchored.factor(od, 0) == 1.0 for od in ods))
check("anchored is the ratio to bin 0",
      all(abs(anchored.factor(od, b) - world.factor(od, b) / world.factor(od, 0)) < 1e-12
          for od in ods for b in range(1, 8)))
check("anchored ignores the future",
      AnchoredWorld(future_changed).factor(ods[0], 2) == AnchoredWorld(
          World(WorldConfig(world_seed=7, base=(1.0, 1.0, 1.0)))).factor(ods[0], 2))

print(f"{len(failures)} failure(s)")
raise SystemExit(1 if failures else 0)
