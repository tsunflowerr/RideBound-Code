"""World v1.1 wrapper: world v1 (preflight_world.py) plus bin starts on the preflight's event clock.

WHY (found in the T3.7 pilot, tang3/pilot/pilot_worlds.py)
----------------------------------------------------------
preflight_world.install applies a new bin at the first `_trigger_and_resolve` at or after the bin start. The
medium preflight triggers only at request arrivals, vehicle physical boundaries and drain timers
(actual_fleetpy_medium_preflight.py:583-634), so vehicles kept moving on the previous bin's edge times until the
next event: up to 265 s late in the pilot (149 s on bins every arm reached), and the switch time differed across
arms by up to 131 s, because physical boundaries depend on the arm's routes. Tier 2 (deadline/preflight_drift_v2.py) had the same mechanism:
lag up to 210 s, across families up to 70 s (tang3/pilot/tier2_switch_lag.py).

WHAT CHANGES
------------
`_next_physical_boundary` is wrapped to return min(original, next bin start strictly after now). The event clock
therefore stops at every bin start, `_move_vehicles` ends there, and the trigger at that instant applies the new
bin. The switch time becomes `bin * BIN_S` in every arm. Consequence: one extra trigger (and batch to the Runner)
per bin start when no other event falls on it; that trigger is arm independent. Drain-step accounting is unchanged
(only timer boundaries count as drain steps).

Nothing else differs from preflight_world.py, which is imported, not copied, and stays untouched because the
T3.7 pilot ran on it. The repository is not modified.

Usage: identical to preflight_world.py (RB_WORLD_CONFIG required).
"""

from __future__ import annotations

import importlib.util
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import preflight_world  # noqa: E402
from world_v1 import BIN_S  # noqa: E402


def next_bin_start(simulation_time: float) -> int:
    """The first bin start strictly after `simulation_time`."""
    return (int(simulation_time // BIN_S) + 1) * BIN_S


def install_bin_boundaries(module) -> dict:
    """Wrap the preflight's physical-boundary function so the event clock stops at each bin start."""
    original = module._next_physical_boundary
    state = {"inserted": 0}

    def patched(vehicles, routing, simulation_time):
        value = original(vehicles, routing, simulation_time)
        boundary = next_bin_start(simulation_time)
        if value is None or boundary < value:
            state["inserted"] += 1
            return boundary
        return value

    module._next_physical_boundary = patched
    return state


def main() -> int:
    path = os.environ.get("RB_WORLD_CONFIG")
    if not path:
        raise SystemExit("RB_WORLD_CONFIG is required")
    world, config, digest = preflight_world.load_world(pathlib.Path(path))

    spec = importlib.util.spec_from_file_location("rb_preflight_world_v1_1", preflight_world.TARGET)
    if spec is None or spec.loader is None:
        raise SystemExit(f"cannot load {preflight_world.TARGET}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)

    state = preflight_world.install(module, world)
    boundaries = install_bin_boundaries(module)
    print(f"[world] version=v1.1 config={path} sha256={digest} seed={config['worldSeed']}", flush=True)
    result = module.main()
    print(f"[world] bin changes applied: {state['changes']}; "
          f"bin-start boundaries used: {boundaries['inserted']}", flush=True)
    return result


if __name__ == "__main__":
    raise SystemExit(main())
