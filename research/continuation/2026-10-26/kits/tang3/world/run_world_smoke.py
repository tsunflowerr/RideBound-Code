"""T3.5 end-to-end leakage check: the same cell, arm and label in two worlds that are identical up
to bin 3 and differ from bin 4 (t >= 3600 s). Every frame the Runner exchanges before 3600 s must be
byte-identical between the two runs; after it the runs may diverge.

Usage: python -B run_world_smoke.py run      # starts the two jobs (AC power only), in parallel
       python -B run_world_smoke.py compare  # compares the two transcripts

Outputs go under E:/RideBoundData/research/tier3-world-smoke-v1/<world>; a job whose output exists
is never started again, and every log is written with mode "x" (never overwritten).
"""
from __future__ import annotations

import base64
import ctypes
import hashlib
import json
import os
import pathlib
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
REPO = pathlib.Path(r"E:/Code/RideBound-deadline")
PYTHON = pathlib.Path(r"E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe")
FLEETPY = pathlib.Path(r"E:/RideBoundData/wp7/FleetPy-1.0.2")
RUNNER = pathlib.Path(r"E:/RideBoundData/research/runner-tier3-v1")
DOTNET = pathlib.Path(r"C:/Program Files/dotnet/dotnet.exe")
OUT_ROOT = pathlib.Path(r"E:/RideBoundData/research/tier3-world-smoke-v1")
LOG_ROOT = HERE / "logs"
COMMITMENT = TANG3.parent / "deadline" / "configs" / "conduct-30.json"
WP4 = TANG3 / "configs" / "wp4-tier3-v1.json"
FIXTURES = REPO / "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/wp9-confirmatory-fixed-panel-v2"
DRIVERS = REPO / "benchmarks/scenarios/wp9-confirmatory"
CELL = "d20181114-s10-r1"
LABEL = "t35-leak"          # same label => same run id in both worlds
WORLDS = {"world-a": HERE / "smoke" / "world-a.json", "world-b": HERE / "smoke" / "world-b.json"}
SPLIT_MS = 4 * 900 * 1000   # first bin where the two worlds differ


def on_ac_power() -> bool:
    class SystemPowerStatus(ctypes.Structure):
        _fields_ = [("ACLineStatus", ctypes.c_ubyte), ("BatteryFlag", ctypes.c_ubyte),
                    ("BatteryLifePercent", ctypes.c_ubyte), ("SystemStatusFlag", ctypes.c_ubyte),
                    ("BatteryLifeTime", ctypes.c_ulong), ("BatteryFullLifeTime", ctypes.c_ulong)]

    status = SystemPowerStatus()
    return bool(ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(status))) \
        and status.ACLineStatus == 1


def command(world: str) -> list[str]:
    fixture = FIXTURES / CELL
    return [
        str(PYTHON), "-B", str(HERE / "preflight_world.py"),
        "--label", LABEL,
        "--fleetpy-root", str(FLEETPY),
        "--runner-root", str(RUNNER),
        "--dotnet", str(DOTNET),
        "--commitment-config", str(COMMITMENT),
        "--wp4-config", str(WP4),
        "--scenario", str(fixture / "scenario-content.json"),
        "--derivative-manifest", str(fixture / "derivative-manifest.json"),
        "--normalization-report", str(fixture / "normalization-report.json"),
        "--selection-frame", str(fixture / "selection-frame.json"),
        "--driver", str(DRIVERS / f"{CELL}.driver.json"),
        "--output", str(OUT_ROOT / world),
        "--repeats", "1",
        "--master-seed", "7",
    ]


def run() -> int:
    if not on_ac_power():
        print("not on AC power: nothing started")
        return 3
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)
    processes = {}
    for world, config in WORLDS.items():
        if (OUT_ROOT / world).exists():
            print(f"{world}: output exists, not started")
            continue
        env = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONIOENCODING="utf-8",
                   RB_WORLD_CONFIG=str(config))
        log = open(LOG_ROOT / f"{world}.log", "x", encoding="utf-8", newline="\n")
        processes[world] = (subprocess.Popen(command(world), cwd=str(REPO), env=env,
                                             stdout=log, stderr=subprocess.STDOUT), log, time.time())
    for world, (process, log, started) in processes.items():
        code = process.wait()
        log.write(f"\nreturncode={code} seconds={time.time() - started:.0f}\n")
        log.close()
        print(f"{world}: returncode={code} seconds={time.time() - started:.0f}")
    return 0


def frames(path: pathlib.Path):
    """(sha256, direction, simTimeMs or None, messageType) per transcript line."""
    for line in path.read_text(encoding="utf-8").splitlines():
        record = json.loads(line)
        frame = base64.b64decode(record["frameBase64"])
        if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
            raise SystemExit(f"{path}: frame hash mismatch")
        envelope = json.loads(frame)
        yield record["frameSha256"], record["direction"], envelope.get("simTimeMs"), envelope.get("messageType")


def compare() -> int:
    # The prefix before the split is every frame before the first timed frame at or after it.
    # (A first version filtered on simTimeMs and counted the untimed final checkpoint/shutdown
    # frames as "before", which reported FAIL on identical prefixes; see inspect_smoke.py.)
    a = list(frames(OUT_ROOT / "world-a" / "transcript-00.ndjson"))
    b = list(frames(OUT_ROOT / "world-b" / "transcript-00.ndjson"))

    def cut(values):
        return next(i for i, f in enumerate(values) if f[2] is not None and f[2] >= SPLIT_MS)

    cut_a, cut_b = cut(a), cut(b)
    late = [i for i, f in enumerate(a[cut_a:], start=cut_a) if f[2] is not None and f[2] < SPLIT_MS]
    first_diff = next((i for i, (x, y) in enumerate(zip(a, b)) if x[0] != y[0]), None)
    print(f"frames: world-a {len(a)}, world-b {len(b)}; prefix before {SPLIT_MS} ms: "
          f"{cut_a} vs {cut_b} frames")
    identical_before = (cut_a == cut_b and not late
                        and [f[0] for f in a[:cut_a]] == [f[0] for f in b[:cut_b]])
    print(f"identical before the split: {identical_before}")
    if first_diff is None:
        print("no differing frame at all (the future change had no effect: check the world)")
        return 1
    print(f"first differing frame: index {first_diff}, simTimeMs {a[first_diff][2]} / "
          f"{b[first_diff][2]}, type {a[first_diff][3]} / {b[first_diff][3]}")
    ok = identical_before and (a[first_diff][2] or 0) >= SPLIT_MS
    print("LEAKAGE CHECK " + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in ("run", "compare"):
        print(__doc__)
        raise SystemExit(2)
    raise SystemExit(run() if sys.argv[1] == "run" else compare())
