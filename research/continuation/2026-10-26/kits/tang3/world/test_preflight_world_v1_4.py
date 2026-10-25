"""Checks for preflight_world_v1_4 (no simulation). Exits 1 on any failure or a wrong check count.
Usage: python -B test_preflight_world_v1_4.py
"""
from __future__ import annotations

import importlib.util
import json
import pathlib
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import preflight_world as pw  # noqa: E402,F401  (puts the world folder first on sys.path)

_spec = importlib.util.spec_from_file_location("v14_under_test", HERE / "preflight_world_v1_4.py")
v14 = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(v14)

EXPECTED_CHECKS = 21
results = []


def check(name, ok, detail=""):
    results.append(bool(ok))
    print(f"{'PASS' if ok else 'FAIL'} {name} {detail}")


prof = (2.0, 2.2, 2.6, 3.0, 3.1, 3.1, 3.2, 3.3, 3.5)
close = lambda a, b: abs(a - b) < 1e-12  # noqa: E731
check("bin start, D=900: whole horizon in the current bin -> 1", v14.continuous_ratio(prof, 0.0, 900) == 1.0)
check("bin start, D=600 -> 1", v14.continuous_ratio(prof, 900.0, 600) == 1.0)
check("t=600, D=900: (300 + 600 m1/m0)/900", close(v14.continuous_ratio(prof, 600.0, 900), (300 + 600 * 2.2 / 2.0) / 900))
check("t=1000, D=1800: (800 + 900 m2/m1 + 100 m3/m1)/1800",
      close(v14.continuous_ratio(prof, 1000.0, 1800), (800 + 900 * 2.6 / 2.2 + 100 * 3.0 / 2.2) / 1800))
check("hold: t=7000 (bin 7), D=900 uses m8/m7 after 7200", close(v14.continuous_ratio(prof, 7000.0, 900), (200 + 700 * 3.5 / 3.3) / 900))
check("after the window (bin 9) -> 1", v14.continuous_ratio(prof, 8200.0, 1800) == 1.0)
check("bin 8 -> 9 crossing stays 1 (held after the window)", v14.continuous_ratio(prof, 7300.0, 900) == 1.0)
check("ratio grows within a bin as the bin end approaches",
      v14.continuous_ratio(prof, 0.0, 900) < v14.continuous_ratio(prof, 300.0, 900) < v14.continuous_ratio(prof, 600.0, 900))


def load(cfg, base={"day": "2018-11-14", "exponent": 1, "windowStart": 25200}):
    with tempfile.TemporaryDirectory() as tmp:
        path = pathlib.Path(tmp) / "f.json"
        path.write_text(json.dumps(cfg), encoding="utf-8")
        try:
            return v14.load(path, base)
        except SystemExit as error:
            return f"refused: {error}"


good = {"forecastVersion": "forecast-v2", "mode": "forecast", "horizonSeconds": 900, "updateSeconds": 300,
        "sourceDays": ["2018-11-12", "2018-11-13"]}
check("valid config accepted", isinstance(load(good), tuple))
check("leakage refused", "leakage" in str(load(dict(good, sourceDays=["2018-11-14"]))))
check("updateSeconds not dividing 900 refused", "refused" in str(load(dict(good, updateSeconds=400))))
check("horizonSeconds 200 refused", "refused" in str(load(dict(good, horizonSeconds=200))))
check("unknown mode refused", "refused" in str(load(dict(good, mode="oracle"))))
check("v1 version refused", "refused" in str(load(dict(good, forecastVersion="forecast-v1"))))


class Module:
    def __init__(self):
        self.calls = []

    def _next_physical_boundary(self, vehicles, routing, simulation_time):
        return 5000.0

    def _trigger_and_resolve(self, control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids):
        self.calls.append((simulation_time, control._rb_travel_dirty))
        control._rb_travel_dirty = False
        return control._rb_mapper.travel_snapshot(len(self.calls), [("a", "b", 100.0)])


class Mapper:
    def travel_snapshot(self, version, arcs):
        return list(arcs)


class Control:
    def __init__(self):
        self._rb_mapper, self._rb_travel_dirty = Mapper(), False


m = Module()
m._next_physical_boundary = Module._next_physical_boundary.__get__(m)
v14.install_slots(m, 300)
check("slot boundary before the original boundary is used", m._next_physical_boundary(None, None, 610.0) == 900.0)
check("original boundary kept when earlier", m._next_physical_boundary(None, None, 4950.0) == 5000.0)

m2, c2 = Module(), Control()
m2._trigger_and_resolve = Module._trigger_and_resolve.__get__(m2)
v14.install_forecast(m2, prof, dict(good, mode="forecast"))
a = m2._trigger_and_resolve(c2, [], {}, 0.0, [], [])
b = m2._trigger_and_resolve(c2, [], {}, 100.0, [], [])
cc = m2._trigger_and_resolve(c2, [], {}, 600.0, [], [])
check("dirty set at the first trigger of a new slot, not within the slot", [d for _, d in m2.calls] == [True, False, True])
check("t=0 (bin start, D=900): unscaled", a == [("a", "b", 100.0)])
check("t=600: scaled by the continuous ratio", close(cc[0][2], 100.0 * (300 + 600 * 2.2 / 2.0) / 900))

m3, c3 = Module(), Control()
m3._trigger_and_resolve = Module._trigger_and_resolve.__get__(m3)
v14.install_forecast(m3, prof, dict(good, mode="naive"))
n = m3._trigger_and_resolve(c3, [], {}, 600.0, [], [])
m3._trigger_and_resolve(c3, [], {}, 900.0, [], [])
check("naive mode: unscaled arcs", n == [("a", "b", 100.0)])
check("naive mode: same slot clock (dirty at each new slot)", [d for _, d in m3.calls] == [True, True])

failures = results.count(False)
print(f"{len(results)} checks, {failures} failure(s)")
raise SystemExit(1 if failures or len(results) != EXPECTED_CHECKS else 0)
