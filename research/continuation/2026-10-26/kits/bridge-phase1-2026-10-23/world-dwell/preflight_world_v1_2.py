"""World v1.2 wrapper: v1.1 plus the 07:00 window start and a guard that a measured base matches the cell.

WHY (handoff 2026-09-29 §9 task B, real-only 07:00-09:00 cells)
---------------------------------------------------------------
* preflight_world.measured_base accepts only the window starts in ALLOWED_WINDOW_STARTS (28800 and 61200), and
  v1.1 inherits that. The 07:00-09:00 cells have sourceWindowStartSeconds 25200.
* run_pilot.world_file picks the measured-day file (W5) by window only, so a 12/11 file used for a 13/11 cell
  would silently apply the wrong day. v1.2 refuses that: when the world base is the measured day, its (day,
  windowStart) must equal the local date and start of the scenario's timeWindow (America/New_York).

WHAT CHANGES
------------
* preflight_world.ALLOWED_WINDOW_STARTS is widened to (25200, 28800, 61200) inside main(), in this process only.
  Importing this module changes nothing. preflight_world.py and preflight_world_v1_1.py are imported, not
  modified: the pilot ran on v1, the sweep and the no-worse runs on v1.1.
* The measured-base guard above, checked before the preflight is loaded.
* The version line says v1.2, and one extra line reports the guard.
Everything else is v1.1: per-edge factors from the anchored world, bin starts on the preflight's event clock,
the Runner sees current-condition snapshots only. As in v1 and v1.1, a measured base reads the factor file only
inside [windowStart, windowStart + 7200] and holds the last value afterwards (during the drain).

Usage: identical to preflight_world_v1_1.py (RB_WORLD_CONFIG required; the preflight's --scenario is read).
"""

from __future__ import annotations

import datetime
import importlib.util
import json
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import preflight_world  # noqa: E402
import preflight_world_v1_1 as v11  # noqa: E402

WINDOW_STARTS_V1_2 = (25200, 28800, 61200)
# The scenario windows are in November 2018, after daylight saving time ended on 2018-11-04, so New York is UTC-5.
# The fixed offset is only accepted inside the range where it is correct.
NEW_YORK_OFFSET = datetime.timedelta(hours=-5)
OFFSET_VALID = (datetime.date(2018, 11, 5), datetime.date(2018, 11, 30))


def scenario_window(scenario_path: pathlib.Path) -> tuple[str, int]:
    """(local date, seconds after local midnight) of the scenario's source window start."""
    content = json.loads(pathlib.Path(scenario_path).read_text(encoding="utf-8"))
    window = content["timeWindow"]
    if window.get("sourceTimezoneId") != "America/New_York":
        raise SystemExit(f"unsupported source time zone {window.get('sourceTimezoneId')!r}")
    text = window["sourceWindowStartUtc"]
    if not text.endswith("Z"):
        raise SystemExit(f"source window start is not UTC: {text!r}")
    start = datetime.datetime.strptime(text[:19], "%Y-%m-%dT%H:%M:%S")
    local = start + NEW_YORK_OFFSET
    if not OFFSET_VALID[0] <= local.date() <= OFFSET_VALID[1]:
        raise SystemExit(f"the fixed UTC-5 offset is not valid for {local.date()}")
    return local.date().isoformat(), local.hour * 3600 + local.minute * 60 + local.second


def argument(argv: list[str], name: str):
    """Value of `name` in an argparse-style command line (`--x v` or `--x=v`), or None."""
    for index, value in enumerate(argv):
        if value == name and index + 1 < len(argv):
            return argv[index + 1]
        if value.startswith(name + "="):
            return value.split("=", 1)[1]
    return None


def check_measured_base(config: dict, scenario_path) -> tuple[str, int] | None:
    """For a measured-day base, require (day, windowStart) == the scenario's window; None for other bases."""
    base = config.get("base")
    if not base or set(base) != preflight_world.MEASURED_BASE_KEYS:
        return None
    if scenario_path is None:
        raise SystemExit("a measured world base needs the preflight's --scenario to check its day and window")
    day, start = scenario_window(pathlib.Path(scenario_path))
    if base["day"] != day:
        raise SystemExit(f"world base day {base['day']} does not match the scenario day {day}")
    if int(base["windowStart"]) != start:
        raise SystemExit(f"world base windowStart {base['windowStart']} does not match the scenario start {start}")
    return day, start


def main() -> int:
    path = os.environ.get("RB_WORLD_CONFIG")
    if not path:
        raise SystemExit("RB_WORLD_CONFIG is required")
    preflight_world.ALLOWED_WINDOW_STARTS = WINDOW_STARTS_V1_2
    checked = check_measured_base(json.loads(pathlib.Path(path).read_bytes().decode("utf-8")),
                                  argument(sys.argv[1:], "--scenario"))
    world, config, digest = preflight_world.load_world(pathlib.Path(path))

    spec = importlib.util.spec_from_file_location("rb_preflight_world_v1_2", preflight_world.TARGET)
    if spec is None or spec.loader is None:
        raise SystemExit(f"cannot load {preflight_world.TARGET}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)

    state = preflight_world.install(module, world)
    boundaries = v11.install_bin_boundaries(module)
    print(f"[world] version=v1.2 config={path} sha256={digest} seed={config['worldSeed']}", flush=True)
    print(f"[world] measured base check: {'not a measured base' if checked is None else f'day={checked[0]} windowStart={checked[1]} matches the scenario'}",
          flush=True)
    result = module.main()
    print(f"[world] bin changes applied: {state['changes']}; "
          f"bin-start boundaries used: {boundaries['inserted']}", flush=True)
    return result


if __name__ == "__main__":
    raise SystemExit(main())
