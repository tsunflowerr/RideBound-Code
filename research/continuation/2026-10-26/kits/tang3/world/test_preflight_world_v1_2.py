"""Tests for preflight_world_v1_2 (07:00 window start + measured-base guard), with fakes (no FleetPy).

main() is run end to end against a fake preflight module (preflight_world.TARGET pointed at a temporary file), so
the order "guard, then load the preflight" and the printed lines are checked without a simulator. Real fixtures
of the development panel are read (read-only) to check the time-zone conversion.
Run: python -B test_preflight_world_v1_2.py
"""
from __future__ import annotations

import contextlib
import csv
import io
import json
import os
import pathlib
import re
import sys
import tempfile

import preflight_world as pw
import preflight_world_v1_2 as v12
from world_v1 import BIN_S

failures: list[str] = []
checks_run = 0
EXPECTED_CHECKS = 24
FIXTURES = pathlib.Path(r"C:/Code/RideBound-noworse/benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/"
                        r"wp14-development-panel-v1")


def check(name: str, condition: bool) -> None:
    global checks_run
    checks_run += 1
    if not condition:
        failures.append(name)
        print("FAIL", name)


def raises_exit(function, *args) -> bool:
    try:
        function(*args)
    except SystemExit:
        return True
    return False


def scenario_file(folder: pathlib.Path, name: str, start_utc: str, zone: str = "America/New_York") -> pathlib.Path:
    path = folder / name
    path.write_text(json.dumps({"timeWindow": {"sourceTimezoneId": zone, "sourceWindowStartUtc": start_utc}}),
                    encoding="utf-8")
    return path


def world_file(folder: pathlib.Path, name: str, base) -> pathlib.Path:
    path = folder / name
    config = {"worldVersion": "world-v1", "worldSeed": 101, "rho": 0.7, "shockMean": 0.4, "shockHalfLife": 2.0,
              "shockWindow": 12, "sigma": 0.0, "shockRate": 0.0, "base": base}
    path.write_text(json.dumps(config), encoding="utf-8")
    return path


FAKE_PREFLIGHT = '''
def _trigger_and_resolve(control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids):
    return None

def _next_physical_boundary(vehicles, routing, simulation_time):
    return None

def main():
    print("[fake] preflight main ran", flush=True)
    return 0
'''


def run_main(world: pathlib.Path, argv: list[str]):
    out = io.StringIO()
    saved_argv, saved_env = sys.argv, os.environ.get("RB_WORLD_CONFIG")
    sys.argv = ["preflight_world_v1_2.py"] + argv
    os.environ["RB_WORLD_CONFIG"] = str(world)
    code = None
    try:
        with contextlib.redirect_stdout(out):
            code = v12.main()
    except SystemExit as stop:
        code = ("exit", str(stop))
    finally:
        sys.argv = saved_argv
        if saved_env is None:
            os.environ.pop("RB_WORLD_CONFIG", None)
        else:
            os.environ["RB_WORLD_CONFIG"] = saved_env
    return code, out.getvalue()


# 1. Importing v1.2 does not widen v1's window starts; v1 still refuses 07:00.
check("import leaves v1 window starts unchanged", pw.ALLOWED_WINDOW_STARTS == (28800, 61200))
check("v1 still refuses a 07:00 measured base", raises_exit(pw.measured_base, "2018-11-12", 1.0, 25200, 24))
check("v1.2 widens to exactly 07:00, 08:00 and 17:00", v12.WINDOW_STARTS_V1_2 == (25200, 28800, 61200))

# 2. Time-zone conversion on real fixtures and on synthetic scenarios.
check("real fixture 13/11 w08 -> (2018-11-13, 28800)",
      v12.scenario_window(FIXTURES / "d20181113-s10-r1-w08" / "scenario-content.json") == ("2018-11-13", 28800))
check("real fixture 12/11 w17 -> (2018-11-12, 61200)",
      v12.scenario_window(FIXTURES / "d20181112-s10-r1-w17" / "scenario-content.json") == ("2018-11-12", 61200))
with tempfile.TemporaryDirectory() as tmp:
    folder = pathlib.Path(tmp)
    w07 = scenario_file(folder, "w07.json", "2018-11-12T12:00:00.0000000Z")
    check("12:00Z on 12/11 -> 07:00 local = 25200", v12.scenario_window(w07) == ("2018-11-12", 25200))
    check("04:30Z on 13/11 -> previous local day 23:30",
          v12.scenario_window(scenario_file(folder, "late.json", "2018-11-13T04:30:00Z")) == ("2018-11-12", 84600))
    check("other time zone refused",
          raises_exit(v12.scenario_window, scenario_file(folder, "tz.json", "2018-11-12T12:00:00Z", "Europe/Berlin")))
    check("non-UTC start refused",
          raises_exit(v12.scenario_window, scenario_file(folder, "nz.json", "2018-11-12T12:00:00+01:00")))
    check("date outside the UTC-5 range refused (daylight saving time)",
          raises_exit(v12.scenario_window, scenario_file(folder, "dst.json", "2018-11-02T12:00:00Z")))

    # 3. The guard.
    measured = {"day": "2018-11-12", "exponent": 1, "windowStart": 25200}
    check("matching day and window pass", v12.check_measured_base({"base": measured}, w07) == ("2018-11-12", 25200))
    check("day mismatch refused", raises_exit(
        v12.check_measured_base, {"base": dict(measured, day="2018-11-13")}, w07))
    check("window mismatch refused", raises_exit(
        v12.check_measured_base, {"base": dict(measured, windowStart=28800)}, w07))
    check("measured base without --scenario refused", raises_exit(v12.check_measured_base, {"base": measured}, None))
    check("explicit base needs no scenario", v12.check_measured_base({"base": {"values": [1.0, 1.6]}}, None) is None)
    check("argument parsing: both argparse forms",
          v12.argument(["--scenario", "a"], "--scenario") == "a" and v12.argument(["--scenario=b"], "--scenario") == "b"
          and v12.argument(["--scenarios", "c"], "--scenario") is None)

    # 4. End to end against a fake preflight.
    fake = folder / "fake_preflight.py"
    fake.write_text(FAKE_PREFLIGHT, encoding="utf-8")
    saved_target, saved_starts = pw.TARGET, pw.ALLOWED_WINDOW_STARTS
    pw.TARGET = fake
    try:
        good = world_file(folder, "W5-d20181112-w07.json", measured)
        code, text = run_main(good, ["--scenario", str(w07), "--output", "x"])
        check("main returns the preflight result", code == 0)
        check("version line is v1.2 and parseable", re.search(
            r"^\[world\] version=v1\.2 config=.+ sha256=[0-9a-f]{64} seed=101$", text, re.M) is not None)
        check("guard line names the checked day and window",
              "[world] measured base check: day=2018-11-12 windowStart=25200 matches the scenario" in text)
        check("fake preflight ran after the guard", "[fake] preflight main ran" in text
              and text.index("measured base check") < text.index("[fake] preflight main ran"))
        pw.ALLOWED_WINDOW_STARTS = saved_starts
        bad = world_file(folder, "W5-d20181112-w07-for-13.json", measured)
        w07_13 = scenario_file(folder, "w07-13.json", "2018-11-13T12:00:00Z")
        code, text = run_main(bad, ["--scenario", str(w07_13)])
        check("wrong day stops before the preflight is loaded",
              isinstance(code, tuple) and "does not match the scenario day" in code[1]
              and "[fake] preflight main ran" not in text)
    finally:
        pw.TARGET, pw.ALLOWED_WINDOW_STARTS = saved_target, saved_starts

# 5. The 07:00 measured base itself equals the factor-file ratios, and holds the last in-window value afterwards.
pw.ALLOWED_WINDOW_STARTS = v12.WINDOW_STARTS_V1_2
try:
    base = pw.measured_base("2018-11-12", 1.0, 25200, 24)
finally:
    pw.ALLOWED_WINDOW_STARTS = (28800, 61200)
rows = {int(r["simulation_time"]): float(r["travel_time_factor"])
        for r in csv.DictReader(open(pw.FACTORS / "2018-11-12_tt_factors.csv", encoding="utf-8"))}
expected = [rows[25200 + b * BIN_S] / rows[25200] for b in range(9)]
check("bins 0..8 are the 07:00..09:00 ratios", all(abs(a - b) < 1e-12 for a, b in zip(base[:9], expected)))
check("bin 8 (09:00) is +47.8% on 12/11 (traffic-windows log)", round(base[8] - 1, 3) == 0.478)
check("after 09:00 the last in-window value is held", all(value == base[8] for value in base[9:]) and len(base) == 24)

print(f"v1.2: {checks_run} checks, {len(failures)} failure(s)")
raise SystemExit(1 if failures or checks_run < EXPECTED_CHECKS else 0)  # a silent early exit must fail
