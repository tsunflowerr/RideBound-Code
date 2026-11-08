"""T3.5 world v1: OD-pair travel-time noise with shared, keyed random numbers (Tier-3 decision 7a).

The world is a deterministic function of (worldSeed, odPair, timeBin, component): every random
number is derived by hashing that key, so the value an arm sees never depends on how many times or
in which order any arm queried the world (no global RNG is consumed). The same world seed therefore
gives every arm the same traffic.

Travel-time factor of an OD pair in bin b (length BIN_S seconds):
    f(od, b) = base(b) * exp(L(od, b)) * (1 + S(od, b))
  base(b)  : optional measured Manhattan factor ratio (v0, Tier 2), 1.0 when absent
  L(od, b) : two-sided mean-reverting log noise, AR(1): L_b = rho * L_{b-1} + sigma * Z(od, b, "ar")
  S(od, b) : shocks; a shock starts in bin s with probability `shock_rate`, magnitude
             Exp(mean `shock_mean`) and recovers exponentially with half-life `shock_half_life` bins
Positivity holds by construction (exp > 0, shocks >= 0).

Travel time integrates speed = 1 / f over time, so a later departure never arrives earlier (FIFO).
The forecast provider sees only bins that have started by `now` and extends the current bin's factor
(current-condition forecast); it never reads future bins of the world.
"""
from __future__ import annotations

import hashlib
import math
import struct
from dataclasses import dataclass

BIN_S = 900


def _uniform(*key) -> float:
    """A uniform in (0, 1) from a hash of the key; never 0 or 1."""
    digest = hashlib.sha256(repr(key).encode("utf-8")).digest()
    value = struct.unpack(">Q", digest[:8])[0]
    return (value + 0.5) / 2 ** 64


def _normal(*key) -> float:
    """Standard normal by Box-Muller from two keyed uniforms."""
    u1, u2 = _uniform(*key, "u1"), _uniform(*key, "u2")
    return math.sqrt(-2.0 * math.log(u1)) * math.cos(2.0 * math.pi * u2)


@dataclass(frozen=True)
class WorldConfig:
    world_seed: int
    sigma: float = 0.08            # stationary log-noise scale per bin innovation
    rho: float = 0.7               # AR(1) persistence between bins
    shock_rate: float = 0.02       # probability that a shock starts in a bin, per OD pair
    shock_mean: float = 0.4        # mean relative magnitude of a shock
    shock_half_life: float = 2.0   # bins
    shock_window: int = 12         # bins of history that can still contribute a shock
    base: tuple[float, ...] = ()   # optional v0 base ratios per bin (Tier-2 measured factors)


class World:
    def __init__(self, config: WorldConfig):
        self.config = config

    def _log_noise(self, od: tuple[str, str], b: int) -> float:
        # Unrolled AR(1) from bin 0 so the value is a pure function of the key (no state).
        c = self.config
        value = 0.0
        for k in range(b + 1):
            value = c.rho * value + c.sigma * _normal(c.world_seed, od, k, "ar")
        return value

    def _shock(self, od: tuple[str, str], b: int) -> float:
        c = self.config
        total = 0.0
        for start in range(max(0, b - c.shock_window), b + 1):
            if _uniform(c.world_seed, od, start, "shock-start") < c.shock_rate:
                magnitude = -c.shock_mean * math.log(_uniform(c.world_seed, od, start, "shock-size"))
                total += magnitude * 0.5 ** ((b - start) / c.shock_half_life)
        return total

    def factor(self, od: tuple[str, str], b: int) -> float:
        if b < 0:
            raise ValueError("bins start at 0")
        base = self.config.base[b] if b < len(self.config.base) else (
            self.config.base[-1] if self.config.base else 1.0)
        return base * math.exp(self._log_noise(od, b)) * (1.0 + self._shock(od, b))

    def travel_time(self, od: tuple[str, str], depart_s: float, free_flow_s: float) -> float:
        """Seconds to travel `od` when departing at `depart_s`: integrate speed 1/f over bins until
        the free-flow travel time is used up."""
        if free_flow_s < 0 or depart_s < 0:
            raise ValueError("negative time")
        remaining, t = float(free_flow_s), float(depart_s)
        while remaining > 1e-12:
            b = int(t // BIN_S)
            f = self.factor(od, b)
            bin_end = (b + 1) * BIN_S
            capacity = (bin_end - t) / f        # free-flow seconds coverable before the bin ends
            if capacity >= remaining:
                t += remaining * f
                remaining = 0.0
            else:
                remaining -= capacity
                t = bin_end
        return t - depart_s


class AnchoredWorld:
    """The world as a run starting at bin 0 sees it: each OD factor divided by its bin-0 value.

    The run then starts on exactly the fixture's travel times (the preflight asserts that at
    initialisation, and the first snapshot must match the manifest hash), and all drift happens
    after the start. Bin 0 is always in the past of every later bin, so this stays a pure function
    of the key and reads no future bin.
    """

    def __init__(self, world: World):
        self.world = world

    def factor(self, od: tuple[str, str], b: int) -> float:
        if b < 0:
            raise ValueError("bins start at 0")
        if b == 0:
            return 1.0
        return self.world.factor(od, b) / self.world.factor(od, 0)


class CurrentConditionForecast:
    """Forecast issued at `now`: the factor of the bin containing `now`, held flat for the future.
    It reads the world only at bins that have already started."""

    def __init__(self, world: World):
        self.world = world
        self.reads: list[int] = []   # bins read, for leakage tests

    def factor(self, od: tuple[str, str], now_s: float) -> float:
        b = int(now_s // BIN_S)
        self.reads.append(b)
        return self.world.factor(od, b)

    def travel_time(self, od: tuple[str, str], now_s: float, free_flow_s: float) -> float:
        return free_flow_s * self.factor(od, now_s)
