"""World v1.3 wrapper: v1.2 plus (optional) forecast-aware travel times for the Runner only.

WHY (2026-10-06, forecast-aware promises)
-----------------------------------------
Under v1.2 the Runner promises with the travel times of the current 15-min bin, i.e. it assumes current traffic
persists. In the 07:00 ramp most of the later travel-time change is predictable from other days
(../../optimize/forecast-predictability-2026-10-06.txt). v1.3 lets the Runner see a forecast while FleetPy keeps
driving the measured times.

WHAT CHANGES
------------
* Window starts accepted for a measured base: 25200, 28800, 50400, 54000, 61200 (07, 08, 14, 15, 17 h), in this
  process only. The v1.2 guard (measured base day and window must equal the scenario's) is reused unchanged.
* If RB_FORECAST_CONFIG is NOT set: nothing else changes (the forecast-off control must reproduce v1.2 runs).
* If RB_FORECAST_CONFIG is set (JSON, keys exactly forecastVersion="forecast-v1", horizonBins K in 1..8,
  sourceDays = ISO dates, none equal to the scenario day): every arc of every travel snapshot sent to the Runner is
  the measured arc times r_b, where b = the 15-min bin of the snapshot's simulation time and
      r_b = (1/K) * sum_{j=0..K-1} m(b+j) / m(b),
  m(b) = mean over the source days of the measured citywide factor at windowStart + min(b, 8) * 900 s (the same
  file, column and hold-after-window rule as preflight_world.measured_base). K = 1 gives r = 1 exactly.
  The measured base of the world is uniform over edges, so scaling all arcs equally changes durations only, not
  shortest paths. FleetPy's own routing engine (vehicle movement, the metric-closure check) is not touched: only
  the Runner's copy of the snapshot (fleet_control._build_travel_snapshot -> mapper.travel_snapshot) is scaled,
  and its hash is computed on the scaled arcs.
* Log lines: "[world] version=v1.3 ...", the v1.2 guard line, "[forecast] ..." (config, profile, one line per
  scaled snapshot) and the v1.2 closing lines.
Usage: as preflight_world_v1_2.py (RB_WORLD_CONFIG required; RB_FORECAST_CONFIG optional).
"""

from __future__ import annotations

import csv
import datetime
import hashlib
import importlib.util
import json
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import preflight_world  # noqa: E402
import preflight_world_v1_1 as v11  # noqa: E402
import preflight_world_v1_2 as v12  # noqa: E402
from world_v1 import BIN_S  # noqa: E402

WINDOW_STARTS_V1_3 = (25200, 28800, 50400, 54000, 61200)
FORECAST_KEYS = {"forecastVersion", "horizonBins", "sourceDays"}
HOLD_BIN = 8                      # measured_base reads [start, start + 7200] and holds the last value


def absolute_profile(day: str, window_start: int) -> tuple[float, ...]:
    """Measured citywide factor per bin 0..HOLD_BIN (absolute, not normalised), as measured_base reads it."""
    path = preflight_world.FACTORS / f"{day}_tt_factors.csv"
    if not path.exists():
        raise SystemExit(f"no measured factors for {day}: {path}")
    rows = []
    with path.open(encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            seconds = int(row["simulation_time"])
            if window_start <= seconds <= window_start + preflight_world.WINDOW_SECONDS:
                rows.append((seconds - window_start, float(row["travel_time_factor"])))
    rows.sort()
    if not rows or rows[0][0] != 0:
        raise SystemExit(f"{day}: no factor row exactly at the window start {window_start}")
    values = []
    for b in range(HOLD_BIN + 1):
        current = rows[0][1]
        for seconds, factor in rows:
            if b * BIN_S >= seconds:
                current = factor
            else:
                break
        values.append(current)
    return tuple(values)


def forecast_profile(source_days: list[str], window_start: int) -> tuple[float, ...]:
    profiles = [absolute_profile(day, window_start) for day in source_days]
    return tuple(sum(p[b] for p in profiles) / len(profiles) for b in range(HOLD_BIN + 1))


def ratio(profile: tuple[float, ...], b: int, k: int) -> float:
    if k == 1:
        return 1.0
    m = lambda i: profile[min(i, HOLD_BIN)]  # noqa: E731
    return sum(m(b + j) for j in range(k)) / (k * m(b))


def load_forecast(path: pathlib.Path, base: dict) -> tuple[dict, str, tuple[float, ...]]:
    raw = path.read_bytes()
    config = json.loads(raw.decode("utf-8"))
    if set(config) != FORECAST_KEYS:
        raise SystemExit(f"forecast config keys {sorted(config)} != {sorted(FORECAST_KEYS)}")
    if config["forecastVersion"] != "forecast-v1":
        raise SystemExit(f"unsupported forecastVersion {config['forecastVersion']!r}")
    k = config["horizonBins"]
    if isinstance(k, bool) or not isinstance(k, int) or not 1 <= k <= 8:
        raise SystemExit(f"horizonBins must be an integer in 1..8, got {k!r}")
    days = config["sourceDays"]
    if not isinstance(days, list) or not days or len(set(days)) != len(days):
        raise SystemExit("sourceDays must be a non-empty list of distinct dates")
    for day in days:
        datetime.date.fromisoformat(day)
    if base["day"] in days:
        raise SystemExit(f"sourceDays contains the scenario day {base['day']} (leakage)")
    profile = forecast_profile(days, int(base["windowStart"]))
    return config, hashlib.sha256(raw).hexdigest(), profile


def install_forecast(module, profile: tuple[float, ...], k: int) -> dict:
    """Scale the Runner's copy of every travel snapshot by r_b; FleetPy's routing engine is untouched."""
    original = module._trigger_and_resolve
    state = {"patched": False, "scaled": 0, "time": None}

    def patched(control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids):
        state["time"] = simulation_time
        if not state["patched"]:
            mapper = control._rb_mapper
            inner = mapper.travel_snapshot

            def scaled(version, arcs):
                b = int(state["time"] // BIN_S)
                r = ratio(profile, b, k)
                state["scaled"] += 1
                print(f"[forecast] t={state['time']:>6.0f}s bin={b} ratio={r:.6f}", flush=True)
                return inner(version, [(origin, destination, seconds * r) for origin, destination, seconds in arcs])

            mapper.travel_snapshot = scaled
            state["patched"] = True
        return original(control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids)

    module._trigger_and_resolve = patched
    return state


def main() -> int:
    path = os.environ.get("RB_WORLD_CONFIG")
    if not path:
        raise SystemExit("RB_WORLD_CONFIG is required")
    preflight_world.ALLOWED_WINDOW_STARTS = WINDOW_STARTS_V1_3
    config_json = json.loads(pathlib.Path(path).read_bytes().decode("utf-8"))
    checked = v12.check_measured_base(config_json, v12.argument(sys.argv[1:], "--scenario"))
    forecast = None
    forecast_path = os.environ.get("RB_FORECAST_CONFIG")
    if forecast_path:
        if checked is None:
            raise SystemExit("a forecast needs a measured-day world base")
        forecast = load_forecast(pathlib.Path(forecast_path), config_json["base"])
    world, config, digest = preflight_world.load_world(pathlib.Path(path))

    spec = importlib.util.spec_from_file_location("rb_preflight_world_v1_3", preflight_world.TARGET)
    if spec is None or spec.loader is None:
        raise SystemExit(f"cannot load {preflight_world.TARGET}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)

    state = preflight_world.install(module, world)
    boundaries = v11.install_bin_boundaries(module)
    fstate = install_forecast(module, forecast[2], forecast[0]["horizonBins"]) if forecast else None
    print(f"[world] version=v1.3 config={path} sha256={digest} seed={config['worldSeed']}", flush=True)
    print(f"[world] measured base check: {'not a measured base' if checked is None else f'day={checked[0]} windowStart={checked[1]} matches the scenario'}",
          flush=True)
    if forecast:
        print(f"[forecast] version=forecast-v1 config={forecast_path} sha256={forecast[1]} "
              f"horizonBins={forecast[0]['horizonBins']} sourceDays={','.join(forecast[0]['sourceDays'])} "
              f"profile={','.join(f'{v:.4f}' for v in forecast[2])}", flush=True)
    else:
        print("[forecast] off", flush=True)
    result = module.main()
    print(f"[world] bin changes applied: {state['changes']}; "
          f"bin-start boundaries used: {boundaries['inserted']}", flush=True)
    if fstate is not None:
        print(f"[forecast] snapshots scaled: {fstate['scaled']}", flush=True)
    return result


if __name__ == "__main__":
    raise SystemExit(main())
