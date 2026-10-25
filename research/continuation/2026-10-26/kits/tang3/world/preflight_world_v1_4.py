"""World v1.4 wrapper: v1.3 plus sub-bin snapshot updates and a continuous, anchored forecast horizon.

WHY (2026-10-06, ../../optimize/EVIDENCE-ADDENDUM-1-2026-10-06.md)
-----------------------------------------------------------------
v1.3 scales the Runner's snapshot by one ratio per 15-min bin. FleetPy's measured world is piecewise constant per
bin, so legs driven inside the current bin looked slower than they were and ETAs oscillated (development cell:
earlier revisions 188 s -> 1748 s). v1.4 rebuilds the Runner's snapshot every U seconds and uses
    r(t) = (1/D) * integral_{t}^{t+D} g(tau) dtau,
    g(tau) = 1 inside the current bin b(t);  g(tau) = m(b(tau)) / m(b(t)) in later bins (hold from bin 8),
i.e. the current measured level for the rest of this bin, and the forecast GROWTH (mean of other same-kind days)
after it. Early in a bin r(t) is close to 1; it grows as the bin end approaches.

CONFIG (RB_FORECAST_CONFIG, JSON, keys exactly):
  forecastVersion "forecast-v2"; mode "forecast" or "naive" (naive: r = 1 but the SAME clock stops and snapshot
  rebuilds, so it is the comparator with an identical event clock); horizonSeconds D in [300, 3600];
  updateSeconds U, a divisor of 900 in [60, 900]; sourceDays (none equal to the scenario day).
Without RB_FORECAST_CONFIG, v1.4 behaves exactly as v1.3 with the forecast off (= v1.2).
Clock: besides v1.1's bin-start stops, the event clock also stops at every multiple of U, and the snapshot is
marked dirty at the first trigger of each U-slot (FleetPy's own edge times are not touched).
Usage: as preflight_world_v1_3.py.
"""

from __future__ import annotations

import hashlib
import importlib.util
import json
import math
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import preflight_world  # noqa: E402
import preflight_world_v1_1 as v11  # noqa: E402
import preflight_world_v1_2 as v12  # noqa: E402
import preflight_world_v1_3 as v13  # noqa: E402
from world_v1 import BIN_S  # noqa: E402

KEYS = {"forecastVersion", "mode", "horizonSeconds", "updateSeconds", "sourceDays"}


def continuous_ratio(profile: tuple[float, ...], t: float, d: float) -> float:
    """(1/D) * integral over [t, t+D] of g; g = 1 in the current bin, m(b)/m(b0) after it (bins held from 8)."""
    b0 = int(t // BIN_S)
    m = lambda b: profile[min(b, v13.HOLD_BIN)]  # noqa: E731
    total, cursor, end = 0.0, t, t + d
    while cursor < end:
        b = int(cursor // BIN_S)
        segment_end = min(end, (b + 1) * BIN_S)
        weight = 1.0 if b == b0 else m(b) / m(b0)
        total += (segment_end - cursor) * weight
        cursor = segment_end
    return total / d


def load(path: pathlib.Path, base: dict):
    raw = path.read_bytes()
    config = json.loads(raw.decode("utf-8"))
    if set(config) != KEYS:
        raise SystemExit(f"forecast config keys {sorted(config)} != {sorted(KEYS)}")
    if config["forecastVersion"] != "forecast-v2":
        raise SystemExit(f"unsupported forecastVersion {config['forecastVersion']!r}")
    if config["mode"] not in ("forecast", "naive"):
        raise SystemExit(f"mode must be forecast or naive, got {config['mode']!r}")
    d, u = config["horizonSeconds"], config["updateSeconds"]
    if isinstance(d, bool) or not isinstance(d, int) or not 300 <= d <= 3600:
        raise SystemExit(f"horizonSeconds must be an integer in 300..3600, got {d!r}")
    if isinstance(u, bool) or not isinstance(u, int) or not 60 <= u <= 900 or BIN_S % u:
        raise SystemExit(f"updateSeconds must divide 900 and be in 60..900, got {u!r}")
    days = config["sourceDays"]
    if not isinstance(days, list) or not days or len(set(days)) != len(days):
        raise SystemExit("sourceDays must be a non-empty list of distinct dates")
    if base["day"] in days:
        raise SystemExit(f"sourceDays contains the scenario day {base['day']} (leakage)")
    profile = v13.forecast_profile(days, int(base["windowStart"]))
    return config, hashlib.sha256(raw).hexdigest(), profile


def install_slots(module, update_seconds: int) -> dict:
    """Stop the event clock at every multiple of U (in addition to the existing boundaries)."""
    original = module._next_physical_boundary
    state = {"inserted": 0}

    def patched(vehicles, routing, simulation_time):
        value = original(vehicles, routing, simulation_time)
        boundary = (math.floor(simulation_time / update_seconds) + 1) * update_seconds
        if value is None or boundary < value:
            state["inserted"] += 1
            return boundary
        return value

    module._next_physical_boundary = patched
    return state


def install_forecast(module, profile, config) -> dict:
    """Rebuild the Runner snapshot at the first trigger of every U-slot; scale it by r(t) (or 1 in naive mode)."""
    original = module._trigger_and_resolve
    naive = config["mode"] == "naive"
    d, u = config["horizonSeconds"], config["updateSeconds"]
    state = {"patched": False, "scaled": 0, "time": None, "slot": None}

    def patched(control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids):
        state["time"] = simulation_time
        slot = int(simulation_time // u)
        if slot != state["slot"]:
            state["slot"] = slot
            control._rb_travel_dirty = True
        if not state["patched"]:
            mapper = control._rb_mapper
            inner = mapper.travel_snapshot

            def scaled(version, arcs):
                r = 1.0 if naive else continuous_ratio(profile, state["time"], d)
                state["scaled"] += 1
                print(f"[forecast] t={state['time']:>7.1f}s bin={int(state['time'] // BIN_S)} ratio={r:.6f}", flush=True)
                if r == 1.0:
                    return inner(version, arcs)
                return inner(version, [(o, dst, s * r) for o, dst, s in arcs])

            mapper.travel_snapshot = scaled
            state["patched"] = True
        return original(control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids)

    module._trigger_and_resolve = patched
    return state


def main() -> int:
    path = os.environ.get("RB_WORLD_CONFIG")
    if not path:
        raise SystemExit("RB_WORLD_CONFIG is required")
    preflight_world.ALLOWED_WINDOW_STARTS = v13.WINDOW_STARTS_V1_3
    config_json = json.loads(pathlib.Path(path).read_bytes().decode("utf-8"))
    checked = v12.check_measured_base(config_json, v12.argument(sys.argv[1:], "--scenario"))
    forecast = None
    forecast_path = os.environ.get("RB_FORECAST_CONFIG")
    if forecast_path:
        if checked is None:
            raise SystemExit("a forecast needs a measured-day world base")
        forecast = load(pathlib.Path(forecast_path), config_json["base"])
    world, config, digest = preflight_world.load_world(pathlib.Path(path))

    spec = importlib.util.spec_from_file_location("rb_preflight_world_v1_4", preflight_world.TARGET)
    if spec is None or spec.loader is None:
        raise SystemExit(f"cannot load {preflight_world.TARGET}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)

    state = preflight_world.install(module, world)
    boundaries = v11.install_bin_boundaries(module)
    slots = install_slots(module, forecast[0]["updateSeconds"]) if forecast else None
    fstate = install_forecast(module, forecast[2], forecast[0]) if forecast else None
    print(f"[world] version=v1.4 config={path} sha256={digest} seed={config['worldSeed']}", flush=True)
    print(f"[world] measured base check: {'not a measured base' if checked is None else f'day={checked[0]} windowStart={checked[1]} matches the scenario'}",
          flush=True)
    if forecast:
        c = forecast[0]
        print(f"[forecast] version=forecast-v2 mode={c['mode']} config={forecast_path} sha256={forecast[1]} "
              f"horizonSeconds={c['horizonSeconds']} updateSeconds={c['updateSeconds']} sourceDays={','.join(c['sourceDays'])} "
              f"profile={','.join(f'{v:.4f}' for v in forecast[2])}", flush=True)
    else:
        print("[forecast] off", flush=True)
    result = module.main()
    print(f"[world] bin changes applied: {state['changes']}; "
          f"bin-start boundaries used: {boundaries['inserted']}", flush=True)
    if fstate is not None:
        print(f"[forecast] snapshots built: {fstate['scaled']}; slot boundaries used: {slots['inserted']}", flush=True)
    return result


if __name__ == "__main__":
    raise SystemExit(main())
