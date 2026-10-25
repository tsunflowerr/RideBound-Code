"""Checks for preflight_world_v1_3 (no simulation). Prints each check; exits 1 on any failure or a wrong check count.
Usage: python -B test_preflight_world_v1_3.py
"""
from __future__ import annotations

import json
import pathlib
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

import importlib.util  # noqa: E402

import preflight_world as pw  # noqa: E402  (its import puts the world folder first on sys.path)

# Load the v1.3 module that sits next to THIS file by path, so a mutated copy is really the one under test.
_spec = importlib.util.spec_from_file_location("v13_under_test", HERE / "preflight_world_v1_3.py")
v13 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(v13)

EXPECTED_CHECKS = 23
results = []


def check(name, ok, detail=""):
    results.append(bool(ok))
    print(f"{'PASS' if ok else 'FAIL'} {name} {detail}")


# 1. absolute profile agrees with measured_base (the world's own reader) for every day and window
pw.ALLOWED_WINDOW_STARTS = v13.WINDOW_STARTS_V1_3
worst = 0.0
for d in range(12, 19):
    day = f"2018-11-{d}"
    for start in v13.WINDOW_STARTS_V1_3:
        absolute = v13.absolute_profile(day, start)
        relative = pw.measured_base(day, 1.0, start, 24)
        worst = max(worst, max(abs(absolute[min(b, 8)] / absolute[0] - relative[b]) for b in range(24)))
check("absolute profile / start == measured_base, 7 days x 5 windows x 24 bins", worst < 1e-12, f"max diff {worst:.2e}")

# 2. ratio
prof = (2.0, 2.2, 2.6, 3.0, 3.1, 3.1, 3.2, 3.3, 3.5)
check("K=1 gives exactly 1 for every bin", all(v13.ratio(prof, b, 1) == 1.0 for b in range(30)))
check("K=2 at b=0 = (m0+m1)/(2 m0)", abs(v13.ratio(prof, 0, 2) - (2.0 + 2.2) / 4.0) < 1e-15)
check("K=3 at b=1 = (m1+m2+m3)/(3 m1)", abs(v13.ratio(prof, 1, 3) - (2.2 + 2.6 + 3.0) / 6.6) < 1e-15)
check("hold: ratio is 1 from bin 8 on for any K", all(v13.ratio(prof, b, k) == 1.0 for b in range(8, 20) for k in range(1, 9)))
check("bin 7, K=4 uses m(7), m(8), m(8), m(8)", abs(v13.ratio(prof, 7, 4) - (3.3 + 3 * 3.5) / (4 * 3.3)) < 1e-15)

# 3. forecast profile = mean of the source days
fp = v13.forecast_profile(["2018-11-12", "2018-11-13"], 25200)
a, b = v13.absolute_profile("2018-11-12", 25200), v13.absolute_profile("2018-11-13", 25200)
check("forecast profile is the mean of the source days", all(abs(fp[i] - (a[i] + b[i]) / 2) < 1e-15 for i in range(9)))


# 4. load_forecast validation
def load(cfg, base={"day": "2018-11-14", "exponent": 1, "windowStart": 25200}):
    with tempfile.TemporaryDirectory() as tmp:
        path = pathlib.Path(tmp) / "f.json"
        path.write_text(json.dumps(cfg), encoding="utf-8")
        try:
            return v13.load_forecast(path, base)
        except SystemExit as error:
            return f"refused: {error}"


good = {"forecastVersion": "forecast-v1", "horizonBins": 2, "sourceDays": ["2018-11-12", "2018-11-13"]}
ok = load(good)
check("valid config accepted", isinstance(ok, tuple) and ok[2] == fp)
check("scenario day in sourceDays refused (leakage)", "leakage" in str(load(dict(good, sourceDays=["2018-11-14", "2018-11-12"]))))
check("extra key refused", "refused" in str(load(dict(good, extra=1))))
check("horizonBins 0 refused", "refused" in str(load(dict(good, horizonBins=0))))
check("horizonBins 9 refused", "refused" in str(load(dict(good, horizonBins=9))))
check("horizonBins True refused", "refused" in str(load(dict(good, horizonBins=True))))
check("duplicate days refused", "refused" in str(load(dict(good, sourceDays=["2018-11-12", "2018-11-12"]))))
check("wrong version refused", "refused" in str(load(dict(good, forecastVersion="forecast-v2"))))
check("empty sourceDays refused", "refused" in str(load(dict(good, sourceDays=[]))))


# 5. install_forecast on a fake preflight module
class Mapper:
    def __init__(self):
        self.received = []

    def travel_snapshot(self, version, arcs):
        self.received.append((version, list(arcs)))
        return {"version": version, "arcs": list(arcs)}


class Control:
    def __init__(self):
        self._rb_mapper = Mapper()


class Module:
    def __init__(self):
        self.calls = []

    def _trigger_and_resolve(self, control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids):
        self.calls.append(simulation_time)
        return control._rb_mapper.travel_snapshot(len(self.calls), [("a", "b", 100.0), ("b", "a", 50.0)])


module, control = Module(), Control()
inner_mapper = control._rb_mapper
original = Module._trigger_and_resolve.__get__(module)
module._trigger_and_resolve = original
state = v13.install_forecast(module, prof, 2)
first = module._trigger_and_resolve(control, [], {}, 0.0, [], [])
r0 = (2.0 + 2.2) / 4.0
check("bin 0 arcs scaled by r_0", first["arcs"] == [("a", "b", 100.0 * r0), ("b", "a", 50.0 * r0)], str(first["arcs"]))
second = module._trigger_and_resolve(control, [], {}, 950.0, [], [])
r1 = (2.2 + 2.6) / 4.4
check("bin 1 (t=950 s) arcs scaled by r_1", second["arcs"] == [("a", "b", 100.0 * r1), ("b", "a", 50.0 * r1)])
check("original trigger called with the same times", module.calls == [0.0, 950.0])
check("mapper patched once, scaled count 2", state["patched"] and state["scaled"] == 2)
mid = module._trigger_and_resolve(control, [], {}, 1700.0, [], [])
check("t=1700 s is bin 1 (floor, not rounding)", mid["arcs"] == [("a", "b", 100.0 * r1), ("b", "a", 50.0 * r1)])
late = module._trigger_and_resolve(control, [], {}, 9000.0, [], [])
check("after the window (bin 10) arcs are unscaled", late["arcs"] == [("a", "b", 100.0), ("b", "a", 50.0)])

module2, control2 = Module(), Control()
module2._trigger_and_resolve = Module._trigger_and_resolve.__get__(module2)
v13.install_forecast(module2, prof, 1)
out = module2._trigger_and_resolve(control2, [], {}, 1800.0, [], [])
check("K=1 leaves arcs byte-equal", out["arcs"] == [("a", "b", 100.0), ("b", "a", 50.0)])

failures = results.count(False)
print(f"{len(results)} checks, {failures} failure(s)")
raise SystemExit(1 if failures or len(results) != EXPECTED_CHECKS else 0)
