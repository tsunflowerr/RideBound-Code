"""End-to-end smoke of world v1.2 (preflight_world_v1_2.py) on the new 07:00 development cell d20181112-s10-r1-w07.

Same command as the task B driver (../w07/run_w07.command: runner-tier3-v2, WP4 v2, fixtures and drivers outside
the repositories, --expected-repository-inventory-sha256), arm C-30, with:
  v12-C-30-W5        the measured day 12/11 from 07:00 (../w07/worlds/W5-d20181112-w07.json). Check: status pass,
                     every bin applied at exactly bin * 900 s, the guard line names 2018-11-12 / 25200, and the
                     factor printed at bin 8 (09:00) equals f(09:00)/f(07:00) of the factor file (+47.8%) to 1e-4.
  v12-leak-a, -b     smoke worlds a/b of v1.1 (identical up to bin 3, different from bin 4), same label. Check: every
                     Runner frame before 3600 s byte-identical (the criterion of run_world_smoke.py).
  v12-guard-negative the 13/11 world on this 12/11 cell. Check: nonzero exit, the "does not match the scenario day"
                     message, and no output directory (the guard runs before the preflight is loaded).
Usage: python -B run_world_v12_smoke.py run | check <new-check-log>
Outputs: C:/RideBoundData/research/tier3-world-v12-smoke/<job>; never restarted; logs mode "x".
"""
from __future__ import annotations

import json
import os
import pathlib
import re
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
sys.path.insert(0, str(TANG3 / "pilot"))
sys.path.insert(0, str(TANG3 / "w07"))
sys.path.insert(0, str(HERE))

import run_pilot  # noqa: E402
import run_w07  # noqa: E402
import run_world_smoke  # noqa: E402
from world_v1 import BIN_S  # noqa: E402

OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-world-v12-smoke")
LOG_ROOT = HERE / "logs"
CELL = "d20181112-s10-r1-w07"
SPLIT_MS = 4 * BIN_S * 1000
WORLDS = TANG3 / "w07" / "worlds"
JOBS = {
    # job: (world config, label)
    "v12-C-30-W5": (WORLDS / "W5-d20181112-w07.json", "v12-C-30-W5"),
    "v12-leak-a": (HERE / "smoke" / "world-a.json", "v12-leak"),
    "v12-leak-b": (HERE / "smoke" / "world-b.json", "v12-leak"),
    "v12-guard-negative": (WORLDS / "W5-d20181113-w07.json", "v12-guard-negative"),
}
BIN_LINE = re.compile(r"^\[world\] t=\s*(?P<t>\d+)s bin=(?P<bin>\d+) edges=\d+ factor=\[(?P<lo>[\d.]+), (?P<hi>[\d.]+)\]$")


def command(job: str) -> list[str]:
    _, label = JOBS[job]
    base = run_w07.command(job, "C-30", CELL)
    base[base.index("--label") + 1] = label
    base[base.index("--output") + 1] = str(OUT_ROOT / job)
    return base


def run() -> int:
    if not run_pilot.may_start_job():
        print("not on AC power: nothing started")
        return 3
    problems = run_w07.check_inputs()
    if problems:
        raise SystemExit(f"input problems: {problems}")
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)
    run_pilot.keep_awake(True)
    processes = {}
    try:
        for job, (world, _) in JOBS.items():
            if (OUT_ROOT / job).exists() or (LOG_ROOT / f"{job}.log").exists():
                print(f"{job}: output or log exists, not started")
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


def log_text(job: str) -> str:
    return (LOG_ROOT / f"{job}.log").read_text(encoding="utf-8", errors="replace")


def switches(job: str) -> dict[int, tuple[int, str, str]]:
    return {int(m["bin"]): (int(m["t"]), m["lo"], m["hi"])
            for line in log_text(job).splitlines() if (m := BIN_LINE.match(line))}


def check(log_path: str) -> int:
    lines, ok = [], True
    say = lines.append
    for job in ("v12-C-30-W5", "v12-leak-a", "v12-leak-b"):
        summary = OUT_ROOT / job / "summary.json"
        status = json.loads(summary.read_text(encoding="utf-8")).get("status") if summary.exists() else None
        at = switches(job)
        exact = bool(at) and all(t == b * BIN_S for b, (t, _, _) in at.items())
        version = "[world] version=v1.2 config=" in log_text(job)
        say(f"{job}: status={status} bins={len(at)} allSwitchesAtBinStart={exact} versionLine={version}")
        ok &= status == "pass" and exact and version
    text = log_text("v12-C-30-W5")
    guard = "[world] measured base check: day=2018-11-12 windowStart=25200 matches the scenario" in text
    bins = switches("v12-C-30-W5")
    bin8 = bins.get(8)
    import preflight_world as pw
    saved = pw.ALLOWED_WINDOW_STARTS
    pw.ALLOWED_WINDOW_STARTS = (25200, 28800, 61200)
    try:
        expected = pw.measured_base("2018-11-12", 1.0, 25200, 24)[8]   # f(09:00) / f(07:00) from the factor file
    finally:
        pw.ALLOWED_WINDOW_STARTS = saved
    factor_ok = bin8 is not None and all(abs(float(value) - expected) <= 1e-4 for value in bin8[1:])
    say(f"v12-C-30-W5: guard line {guard}; bin 8 factor {bin8[1:] if bin8 else None} "
        f"(expected {expected:.4f}, i.e. +{expected - 1:.1%}) -> {factor_ok}")
    ok &= guard and factor_ok
    a = list(run_world_smoke.frames(OUT_ROOT / "v12-leak-a" / "transcript-00.ndjson"))
    b = list(run_world_smoke.frames(OUT_ROOT / "v12-leak-b" / "transcript-00.ndjson"))
    cut = lambda values: next(i for i, f in enumerate(values) if f[2] is not None and f[2] >= SPLIT_MS)  # noqa: E731
    cut_a, cut_b = cut(a), cut(b)
    late = [i for i, f in enumerate(a[cut_a:], start=cut_a) if f[2] is not None and f[2] < SPLIT_MS]
    identical = cut_a == cut_b and not late and [f[0] for f in a[:cut_a]] == [f[0] for f in b[:cut_b]]
    first = next((i for i, (x, y) in enumerate(zip(a, b)) if x[0] != y[0]), None)
    say(f"leak: frames {len(a)}/{len(b)}, prefix {cut_a}/{cut_b}, identical before {SPLIT_MS} ms: {identical}, "
        f"first differing frame {first} at {a[first][2] if first is not None else None} ms")
    leak_ok = identical and first is not None and (a[first][2] or 0) >= SPLIT_MS
    say("LEAKAGE CHECK " + ("PASS" if leak_ok else "FAIL"))
    ok &= leak_ok
    negative = log_text("v12-guard-negative")
    code_line = [line for line in negative.splitlines() if line.startswith("returncode=")]
    refused = (bool(code_line) and code_line[-1].split()[0] != "returncode=0"
               and "does not match the scenario day" in negative and not (OUT_ROOT / "v12-guard-negative").exists())
    say(f"v12-guard-negative: {code_line[-1] if code_line else 'no returncode'}; refused before the preflight: {refused}")
    ok &= refused
    say("V1.2 SMOKE " + ("PASS" if ok else "FAIL"))
    text = "\n".join(lines) + "\n"
    with open(log_path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "run":
        raise SystemExit(run())
    if len(sys.argv) == 3 and sys.argv[1] == "check":
        raise SystemExit(check(sys.argv[2]))
    print(__doc__)
    raise SystemExit(2)
