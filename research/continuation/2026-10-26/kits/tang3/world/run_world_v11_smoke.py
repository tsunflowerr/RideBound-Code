"""End-to-end smoke of world v1.1 (preflight_world_v1_1.py) on the DEVELOPMENT cell d20181112-s10-r1-w08.

Four jobs, in parallel, AC power only:
  v11-C-30-W4, v11-V-30-W4   world W4 (OD noise), arms C-30 and V-30. Check: status pass, every bin applied at
                             exactly bin * 900 s, and the same switch times in both arms (the pilot's v1 runs of
                             this cell differed by up to 37 s between C-30 and V-30 in W4).
  v11-leak-a, v11-leak-b     arm C-30, smoke worlds a/b (identical up to bin 3, different from bin 4), same label.
                             Check: every Runner frame before 3600 s byte-identical (as run_world_smoke.py).
The T3.5 smoke used d20181114-s10-r1 (Panel A); this one deliberately does not.

Usage: python -B run_world_v11_smoke.py run | check
Outputs: E:/RideBoundData/research/tier3-world-v11-smoke/<job>; an existing output is never started again; logs
are opened with mode "x".
"""
from __future__ import annotations

import os
import pathlib
import re
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
sys.path.insert(0, str(TANG3 / "pilot"))
sys.path.insert(0, str(HERE))

import run_pilot  # noqa: E402  (paths, power check, command layout)
import run_world_smoke  # noqa: E402  (frames(), the prefix criterion)
from world_v1 import BIN_S  # noqa: E402

OUT_ROOT = pathlib.Path(r"E:/RideBoundData/research/tier3-world-v11-smoke")
LOG_ROOT = HERE / "logs"
PREFLIGHT = HERE / "preflight_world_v1_1.py"
CELL = "d20181112-s10-r1-w08"
SPLIT_MS = 4 * BIN_S * 1000
JOBS = {
    # job: (arm, world config, label)
    "v11-C-30-W4": ("C-30", run_pilot.world_file("W4", CELL), "v11-C-30-W4"),
    "v11-V-30-W4": ("V-30", run_pilot.world_file("W4", CELL), "v11-V-30-W4"),
    "v11-leak-a": ("C-30", HERE / "smoke" / "world-a.json", "v11-leak"),
    "v11-leak-b": ("C-30", HERE / "smoke" / "world-b.json", "v11-leak"),
}
BIN_LINE = re.compile(r"^\[world\] t=\s*(?P<t>\d+)s bin=(?P<bin>\d+) ")


def command(job: str) -> list[str]:
    arm, _, label = JOBS[job]
    base = run_pilot.command(label, arm, CELL)
    base[2] = str(PREFLIGHT)                      # the wrapper, instead of preflight_world.py
    base[base.index("--output") + 1] = str(OUT_ROOT / job)
    return base


def run() -> int:
    if not run_pilot.may_start_job():
        print("not on AC power: nothing started")
        return 3
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)
    run_pilot.keep_awake(True)
    processes = {}
    try:
        for job, (_, world, _) in JOBS.items():
            if (OUT_ROOT / job).exists():
                print(f"{job}: output exists, not started")
                continue
            env = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONIOENCODING="utf-8", RB_WORLD_CONFIG=str(world))
            log = open(LOG_ROOT / f"{job}.log", "x", encoding="utf-8", newline="\n")
            processes[job] = (subprocess.Popen(command(job), cwd=str(run_pilot.REPO), env=env,
                                               stdout=log, stderr=subprocess.STDOUT), log, time.time())
        for job, (process, log, started) in processes.items():
            code = process.wait()
            log.write(f"\nreturncode={code} seconds={time.time() - started:.0f}\n")
            log.close()
            print(f"{job}: returncode={code} seconds={time.time() - started:.0f}", flush=True)
    finally:
        run_pilot.keep_awake(False)
    return 0


def switches(job: str) -> dict[int, int]:
    out = {}
    for line in (LOG_ROOT / f"{job}.log").read_text(encoding="utf-8", errors="replace").splitlines():
        if match := BIN_LINE.match(line):
            out[int(match["bin"])] = int(match["t"])
    return out


def check() -> int:
    ok = True
    for job in JOBS:
        summary = OUT_ROOT / job / "summary.json"
        status = run_pilot.json.loads(summary.read_text(encoding="utf-8")).get("status") if summary.exists() else None
        at = switches(job)
        exact = bool(at) and all(t == b * BIN_S for b, t in at.items())
        print(f"{job}: status={status} bins={len(at)} allSwitchesAtBinStart={exact}")
        ok &= status == "pass" and exact
    c, v = switches("v11-C-30-W4"), switches("v11-V-30-W4")
    common = sorted(set(c) & set(v))
    same = all(c[b] == v[b] for b in common)
    print(f"C-30 vs V-30 (W4): common bins {len(common)}, identical switch times: {same}")
    ok &= same and len(common) >= 8
    a = list(run_world_smoke.frames(OUT_ROOT / "v11-leak-a" / "transcript-00.ndjson"))
    b = list(run_world_smoke.frames(OUT_ROOT / "v11-leak-b" / "transcript-00.ndjson"))
    cut = lambda values: next(i for i, f in enumerate(values) if f[2] is not None and f[2] >= SPLIT_MS)  # noqa: E731
    cut_a, cut_b = cut(a), cut(b)
    late = [i for i, f in enumerate(a[cut_a:], start=cut_a) if f[2] is not None and f[2] < SPLIT_MS]
    identical = cut_a == cut_b and not late and [f[0] for f in a[:cut_a]] == [f[0] for f in b[:cut_b]]
    first = next((i for i, (x, y) in enumerate(zip(a, b)) if x[0] != y[0]), None)
    print(f"leak: frames {len(a)}/{len(b)}, prefix {cut_a}/{cut_b}, identical before {SPLIT_MS} ms: {identical}, "
          f"first differing frame {first} at {a[first][2] if first is not None else None} ms")
    leak_ok = identical and first is not None and (a[first][2] or 0) >= SPLIT_MS
    print("LEAKAGE CHECK " + ("PASS" if leak_ok else "FAIL"))
    ok &= leak_ok
    print("V1.1 SMOKE " + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in ("run", "check"):
        print(__doc__)
        raise SystemExit(2)
    raise SystemExit(run() if sys.argv[1] == "run" else check())
