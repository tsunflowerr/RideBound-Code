"""World wrapper for the full-node Via cells (ADR-089, GĐ3): world v1.2 with a per-bin edge update that scales.

WHY
---
preflight_world.EdgeTimes.apply calls AnchoredWorld.factor for every edge at every bin change, and World.factor
computes the AR(1) noise and the shock history with about 50 SHA-256 draws per call even when sigma and shockRate are 0.
It also appends one (bin, bin) tuple per edge per bin. A full-node Via cell has about 680,000 edges and 16 bins:
about a minute of hashing and 11 million tuples per job.

WHAT CHANGES
------------
* Only noise-free worlds are accepted (sigma 0 and shockRate 0, as every Via world: convert_via_full.py). Then
  World.factor = base[b] * exp(0.0) * (1.0 + 0.0) = base[b] for every edge, bit for bit, and AnchoredWorld.factor is
  base[b] / base[0] for every edge. FastEdgeTimes computes that factor once per bin through the same
  AnchoredWorld.factor call, checks it bit for bit against the per-edge call on 100 edges spread over the edge list,
  and sets every edge to free_flow * factor exactly as EdgeTimes does. It records one read per bin.
* Everything else is preflight_world_v1_2.main (bin starts on the event clock, window-start check), run against the
  preflight of this checkout (preflight_world.TARGET in this folder).

Usage: identical to preflight_world_v1_2.py (RB_WORLD_CONFIG required).
"""

from __future__ import annotations

import importlib.util
import json
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import preflight_world  # noqa: E402  (this folder's copy: TARGET = the preflight of this checkout)
import preflight_world_v1_1 as v11  # noqa: E402
from world_v1 import BIN_S  # noqa: E402

WINDOW_STARTS = (25200, 28800, 61200)
SAMPLE_EDGES = 100


class FastEdgeTimes(preflight_world.EdgeTimes):
    """EdgeTimes for a noise-free world: one factor per bin, checked against the per-edge call on a sample."""

    def __init__(self, routing, world):
        super().__init__(routing, world)
        edges = list(self.free_flow)
        step = max(1, len(edges) // SAMPLE_EDGES)
        self._sample = edges[::step][:SAMPLE_EDGES]

    def apply(self, simulation_time: float) -> bool:
        b = int(simulation_time // BIN_S)
        if b == self.bin:
            return False
        self.bin = b
        self.reads.append((b, b))
        factor = self.world.factor(self._sample[0], b)
        for od in self._sample:
            per_edge = self.world.factor(od, b)
            if per_edge != factor:
                raise RuntimeError(f"edge factor {od} {per_edge!r} differs from {factor!r} in bin {b}")
        for (origin, destination), free_flow in self.free_flow.items():
            self.routing._set_edge_tt(origin, destination, free_flow * factor)
        self.routing._reset_internal_attributes_after_travel_time_update()
        return True


def main() -> int:
    path = os.environ.get("RB_WORLD_CONFIG")
    if not path:
        raise SystemExit("RB_WORLD_CONFIG is required")
    config = json.loads(pathlib.Path(path).read_bytes().decode("utf-8"))
    if config.get("sigma") != 0 or config.get("shockRate") != 0 or set(config.get("base", {})) != {"values"}:
        raise SystemExit("the full-node wrapper accepts only noise-free worlds with an explicit base")
    preflight_world.ALLOWED_WINDOW_STARTS = WINDOW_STARTS
    preflight_world.EdgeTimes = FastEdgeTimes
    world, config, digest = preflight_world.load_world(pathlib.Path(path))

    spec = importlib.util.spec_from_file_location("rb_preflight_world_full", preflight_world.TARGET)
    if spec is None or spec.loader is None:
        raise SystemExit(f"cannot load {preflight_world.TARGET}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)

    state = preflight_world.install(module, world)
    boundaries = v11.install_bin_boundaries(module)
    print(f"[world] version=full-v1 config={path} sha256={digest} seed={config['worldSeed']} target={preflight_world.TARGET}",
          flush=True)
    result = module.main()
    print(f"[world] bin changes applied: {state['changes']}; bin-start boundaries used: {boundaries['inserted']}",
          flush=True)
    return result


if __name__ == "__main__":
    raise SystemExit(main())
