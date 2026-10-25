"""Driver for the Manhattan bridge (company plan step 1; owner approval 2026-10-21).

Phases (run in this order; each refuses to start until the previous one passed):
  regress  1 job.  CF-U at 1.5x on d20181114-s10-r1-w08 with EXACTLY the inputs of the archived
           Stage-1b smoke job (C:/RideBoundData/research/tier3-stage1b-v1/smoke/CF-U-...), but the new
           reference Runner (7435661 + ADR-080 rule patches), the measurement fix (bridge_launcher.py)
           and the 896 MiB GC cap. Pass = compare_runs.py identical to the archive: the rebuilt Runner,
           the rule patches with their keys absent, the fix and the GC cap change no decision.
  smoke    2 jobs. PS-300 and DLHZ-120 (the two new configs) on d20181114-s10-r1-w08 at 2.0x.
  main     55 jobs. 11 cells (w08 r1 12-18 Nov, w17 r1 12/13/14/18 Nov) x U, C-120, C-300, PS-300,
           DLHZ-120, 2.0x max ride, cost-first WP4, no-worse recovery, measured-day world W5, seed 7.

Rails (as tang3 drivers): one instance (PID lock), STOP file, AC power only (waits 20 min), keeps the
PC awake, RAM floor before each job, disk floors C >= 30 GiB / E >= 40 GiB, never overwrites an
output or a log, failed outputs are kept. Do not edit E:/Code/RideBound-deadline while jobs run
(the preflight checks the adapter repository inventory at start and end).

Usage: python -B run_bridge.py check|regress|smoke|main [--workers N]
"""

from __future__ import annotations

import argparse
import concurrent.futures
import ctypes
import datetime as dt
import hashlib
import json
import os
import pathlib
import shutil
import subprocess
import sys
import threading
import time

import psutil

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/tang3")
PYTHON = pathlib.Path(r"E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe")
FLEETPY = pathlib.Path(r"E:/RideBoundData/wp7/FleetPy-1.0.2")
DOTNET = pathlib.Path(r"C:/Program Files/dotnet/dotnet.exe")
ADAPTER_REPO = pathlib.Path(r"E:/Code/RideBound-deadline")
WRAPPER = TANG3 / "world" / "preflight_world_v1_2.py"
LAUNCHER = HERE / "bridge_launcher.py"
COMPARE = HERE / "compare_runs.py"
RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-via-ref-v1")
RUNNER_MANIFEST = HERE / "runner-via-ref-v1.sha256.tsv"
WP4 = TANG3 / "stage1b" / "harness" / "wp4-tier3-v2-costfirst.json"
WP4_SHA256 = "37040657b8f6c16c8eb8430a208bfd4b155791668af0f1da49110f0f372300ae"   # run_stage1b-v7.py:166
EXPECTED_INVENTORY = "0da63eaf2b30ab332fba265a6b2b6e5c36b3ee78950cbac6d6267e60f81e4cbe"  # run_w07.py:46
FIXTURES = pathlib.Path(r"C:/RideBoundData/research/fixtures-via-bridge-2000-v1")
OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1")
LOG_ROOT = HERE / "logs"
STOP_FILE = HERE / "STOP"
LOCK = HERE / "run-instance.lock"
PROGRESS = HERE / "PROGRESS.txt"

ARMS = ("U", "C-120", "C-300", "PS-300", "DLHZ-120")
# Phase "elmi" (added 2026-10-23): the Elmi-budget arms (wf14 formula, alpha 0.35) on the same 11 cells, with the
# Runner that adds the slack-proportional budget on top of A0.1 (patch elmi-slack-budget-v1.patch, tree 32155156...).
# Refused until ELMI-READY.txt exists (written after the independent review and the 58-job replay gate pass).
ELMI_ARMS = ("RCE35", "EOHZ35")
ELMI_RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-via-elmi-v1")
ELMI_RUNNER_MANIFEST = HERE / "runner-via-elmi-v1.sha256.tsv"
CELLS = tuple(f"d201811{d}-s10-r1-w08" for d in ("12", "13", "14", "15", "16", "17", "18")) + \
        tuple(f"d201811{d}-s10-r1-w17" for d in ("12", "13", "14", "18"))
SMOKE_CELL = "d20181114-s10-r1-w08"

REGRESS = {
    "archive": pathlib.Path(r"C:/RideBoundData/research/tier3-stage1b-v1/smoke/CF-U-d20181114-s10-r1-w08"),
    "label": "CF-U-d20181114-s10-r1-w08-s1b",
    "config": TANG3 / "configs" / "U.json",
    "fixture": pathlib.Path(r"C:/RideBoundData/research/fixtures-panelA-v1/d20181114-s10-r1-w08"),
    "driver": TANG3 / "calib" / "panelA" / "drivers" / "d20181114-s10-r1-w08.driver.json",
    "world": TANG3 / "calib" / "panelA" / "worlds" / "W5-d20181114-w08.json",
}

POWER_WAIT_S = 1200
RAM_FLOOR = int(1.8 * 2**30)
RAM_WAIT_S = 1800
DISK_FLOORS = {"C:\\": 30 * 2**30, "E:\\": 40 * 2**30}
JOB_ENV = {
    "PYTHONDONTWRITEBYTECODE": "1",
    "PYTHONIOENCODING": "utf-8",
    "RB_REALJOB_BUFFERED_READ": "1",
    "RB_REALJOB_TIMEOUT_S": "900",
    "DOTNET_GCHeapHardLimit": "0x38000000",
}


class _PowerStatus(ctypes.Structure):
    _fields_ = [("ACLineStatus", ctypes.c_byte), ("BatteryFlag", ctypes.c_byte),
                ("BatteryLifePercent", ctypes.c_byte), ("SystemStatusFlag", ctypes.c_byte),
                ("BatteryLifeTime", ctypes.c_ulong), ("BatteryFullLifeTime", ctypes.c_ulong)]


def on_ac() -> bool:
    status = _PowerStatus()
    return bool(ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(status))) and status.ACLineStatus == 1


def keep_awake() -> None:
    ctypes.windll.kernel32.SetThreadExecutionState(0x80000000 | 0x00000001)


def now() -> str:
    return dt.datetime.now().isoformat(timespec="seconds")


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def log(line: str) -> None:
    print(f"{now()} {line}", flush=True)


# ------------------------------------------------------------------ jobs

def jobs(phase: str):
    if phase == "regress":
        return [("REG-CF-U-d20181114-s10-r1-w08-1500", "U", SMOKE_CELL)]
    if phase == "smoke":
        return [(f"{arm}-{SMOKE_CELL}", arm, SMOKE_CELL) for arm in ("PS-300", "DLHZ-120")]
    if phase == "elmi":
        return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in ELMI_ARMS]
    return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in ARMS]


def runner_for(phase: str) -> tuple[pathlib.Path, pathlib.Path]:
    return (ELMI_RUNNER, ELMI_RUNNER_MANIFEST) if phase == "elmi" else (RUNNER, RUNNER_MANIFEST)


def inputs(phase: str, arm: str, cell: str) -> dict:
    if phase == "regress":
        return {k: REGRESS[k] for k in ("label", "config", "fixture", "driver", "world")}
    return {
        "label": f"{arm}-{cell}-vbr",
        "config": HERE / "configs" / f"{arm}.json",
        "fixture": FIXTURES / cell,
        "driver": HERE / "drivers" / f"{cell}.driver.json",
        "world": HERE / "worlds" / f"W5-{cell[:9]}-{cell[-3:]}.json",
    }


def command(phase: str, name: str, arm: str, cell: str) -> list[str]:
    spec = inputs(phase, arm, cell)
    fixture = spec["fixture"]
    return [
        str(PYTHON), "-B", str(LAUNCHER), str(WRAPPER),
        "--label", spec["label"],
        "--fleetpy-root", str(FLEETPY),
        "--runner-root", str(runner_for(phase)[0]),
        "--dotnet", str(DOTNET),
        "--commitment-config", str(spec["config"]),
        "--wp4-config", str(WP4),
        "--scenario", str(fixture / "scenario-content.json"),
        "--derivative-manifest", str(fixture / "derivative-manifest.json"),
        "--normalization-report", str(fixture / "normalization-report.json"),
        "--selection-frame", str(fixture / "selection-frame.json"),
        "--driver", str(spec["driver"]),
        "--output", str(OUT_ROOT / phase / name),
        "--repeats", "1",
        "--master-seed", "7",
        "--expected-repository-inventory-sha256", EXPECTED_INVENTORY,
    ]


def state_of(phase: str, name: str) -> str:
    output = OUT_ROOT / phase / name
    if not output.exists():
        return "pending"
    summary = output / "summary.json"
    if summary.exists():
        try:
            return "status-" + str(json.loads(summary.read_text(encoding="utf-8")).get("status", "unknown"))
        except (ValueError, OSError):
            return "status-unreadable"
    return "exists-no-summary"


# ------------------------------------------------------------------ checks

def check_inputs(phase: str) -> list[str]:
    problems: list[str] = []
    runner, runner_manifest = runner_for(phase)
    for path in (PYTHON, FLEETPY, DOTNET, WRAPPER, LAUNCHER, COMPARE, runner, WP4):
        if not path.exists():
            problems.append(f"missing {path}")
    if WP4.exists() and sha256(WP4) != WP4_SHA256:
        problems.append("WP4 cost-first config SHA-256 mismatch")
    verify = subprocess.run([str(PYTHON), "-B", str(TANG3 / "runner_manifest.py"), "verify", str(runner),
                             str(runner_manifest)], capture_output=True, text=True)
    if verify.returncode != 0 or not verify.stdout.startswith("OK"):
        problems.append(f"runner manifest: {verify.stdout.strip()} {verify.stderr.strip()}")
    for manifest, root in ((HERE / "configs-worlds.sha256.txt", HERE),
                           (HERE / "configs-elmi.sha256.txt", HERE / "configs"),
                           (HERE / "drivers.sha256.txt", HERE / "drivers"),
                           (HERE / "fixtures-via-bridge-2000-v1.sha256.txt", FIXTURES)):
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            digest, rel = line.split(None, 1)
            rel = rel.lstrip("*").removeprefix("./")
            path = root / rel
            if not path.exists() or sha256(path) != digest:
                problems.append(f"input changed or missing: {path}")
    for name, arm, cell in jobs(phase):
        spec = inputs(phase, arm, cell)
        for key in ("config", "driver", "world"):
            if not spec[key].exists():
                problems.append(f"{name}: missing {key} {spec[key]}")
        if not (spec["fixture"] / "scenario-content.json").exists():
            problems.append(f"{name}: missing fixture {spec['fixture']}")
    if phase == "smoke" and not (HERE / "REGRESS-PASSED.txt").exists():
        problems.append("smoke refused: REGRESS-PASSED.txt does not exist")
    if phase == "main" and not (HERE / "SMOKE-PASSED.txt").exists():
        problems.append("main refused: SMOKE-PASSED.txt does not exist")
    if phase == "elmi" and not (HERE / "ELMI-READY.txt").exists():
        problems.append("elmi refused: ELMI-READY.txt does not exist (review + replay gate first)")
    return problems


def disk_problems() -> list[str]:
    return [f"{drive} free {shutil.disk_usage(drive).free / 2**30:.1f} GiB < floor {floor / 2**30:.0f} GiB"
            for drive, floor in DISK_FLOORS.items() if shutil.disk_usage(drive).free < floor]


def wait_until(predicate, seconds: int) -> bool:
    deadline = time.time() + seconds
    while not predicate():
        if time.time() > deadline or STOP_FILE.exists():
            return False
        time.sleep(30)
    return True


# ------------------------------------------------------------------ run

_start_lock = threading.Lock()


def run_job(phase: str, name: str, arm: str, cell: str):
    with _start_lock:                      # one admission at a time, so two jobs never both see the same free RAM
        state = state_of(phase, name)
        if state != "pending":
            return name, state, None
        log_path = LOG_ROOT / phase / f"{name}.log"
        if log_path.exists():
            return name, "log-exists-pending", None
        if STOP_FILE.exists():
            return name, "deferred-stop", None
        if not wait_until(on_ac, POWER_WAIT_S):
            return name, "deferred-on-battery", None
        if disk_problems():
            return name, "deferred-disk: " + "; ".join(disk_problems()), None
        if not wait_until(lambda: psutil.virtual_memory().available >= RAM_FLOOR, RAM_WAIT_S):
            return name, "deferred-low-ram", None
        env = dict(os.environ, **JOB_ENV, RB_WORLD_CONFIG=str(inputs(phase, arm, cell)["world"]))
        started = time.time()
        log(f"start {phase}/{name}")
        process = subprocess.Popen(command(phase, name, arm, cell), cwd=str(ADAPTER_REPO), env=env,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                                   encoding="utf-8", errors="replace")
        time.sleep(5)                       # let the process tree appear before the next admission
    peak = {"rss": 0, "byName": {}}

    def monitor():
        root = psutil.Process(process.pid)
        while process.poll() is None:
            try:
                tree = [root] + root.children(recursive=True)
                total = 0
                for proc in tree:
                    try:
                        rss = proc.memory_info().rss
                        total += rss
                        peak["byName"][proc.name()] = max(peak["byName"].get(proc.name(), 0), rss)
                    except psutil.Error:
                        pass
                peak["rss"] = max(peak["rss"], total)
            except psutil.Error:
                pass
            time.sleep(2)

    watcher = threading.Thread(target=monitor, daemon=True)
    watcher.start()
    stdout, stderr = process.communicate()
    watcher.join(timeout=5)
    seconds = time.time() - started
    log_path.parent.mkdir(parents=True, exist_ok=True)
    with open(log_path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(f"returncode={process.returncode} seconds={seconds:.0f} peakTreeRssMiB={peak['rss'] / 2**20:.0f} "
                     f"peakByName={json.dumps({k: round(v / 2**20) for k, v in peak['byName'].items()})}\n")
        handle.write(stdout + stderr)
    state = state_of(phase, name)
    log(f"end   {phase}/{name} rc={process.returncode} {seconds / 60:.1f} min peak {peak['rss'] / 2**20:.0f} MiB -> {state}")
    return name, state, {"seconds": round(seconds), "peakTreeRssMiB": round(peak["rss"] / 2**20)}


def write_progress(phase: str, results: dict) -> None:
    counts: dict[str, int] = {}
    for name, _, _ in jobs(phase):
        state = state_of(phase, name)
        counts[state] = counts.get(state, 0) + 1
    text = f"{now()} phase={phase} " + " ".join(f"{k}={v}" for k, v in sorted(counts.items())) + "\n"
    for name, info in results.items():
        text += f"{name} {json.dumps(info)}\n"
    PROGRESS.write_text(text, encoding="utf-8")


def acquire_lock() -> bool:
    if LOCK.exists():
        try:
            pid = int(LOCK.read_text(encoding="utf-8").split()[0])
            if psutil.pid_exists(pid) and "python" in psutil.Process(pid).name().lower():
                return False
        except (ValueError, IndexError, psutil.Error):
            pass
    LOCK.write_text(f"{os.getpid()} {now()}\n", encoding="utf-8")
    return True


def after_regress() -> None:
    out = HERE / "regress-compare.json"
    if out.exists():
        log(f"{out.name} exists; not re-comparing")
        return
    result = subprocess.run([str(PYTHON), "-B", str(COMPARE), str(REGRESS["archive"]),
                             str(OUT_ROOT / "regress" / jobs("regress")[0][0]), "--json", str(out)],
                            capture_output=True, text=True, env=dict(os.environ, PYTHONIOENCODING="utf-8"))
    verdict = "REGRESS-PASSED.txt" if result.returncode == 0 else "REGRESS-FAILED.txt"
    (HERE / verdict).write_text(f"{now()} compare_runs rc={result.returncode}\n{result.stdout}", encoding="utf-8")
    log(f"regress verdict: {verdict}")


def after_smoke() -> None:
    states = [state_of("smoke", name) for name, _, _ in jobs("smoke")]
    verdict = "SMOKE-PASSED.txt" if all(s == "status-pass" for s in states) else "SMOKE-FAILED.txt"
    (HERE / verdict).write_text(f"{now()} states {states}\n", encoding="utf-8")
    log(f"smoke verdict: {verdict}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "regress", "smoke", "main", "elmi"))
    parser.add_argument("--workers", type=int, default=2)
    args = parser.parse_args()
    phase = args.phase
    if phase == "check":
        for p in ("regress", "smoke", "main", "elmi"):
            problems = check_inputs(p)
            log(f"check {p}: {'OK' if not problems else problems}")
        log("disk: " + ("OK" if not disk_problems() else "; ".join(disk_problems())))
        log(f"RAM available {psutil.virtual_memory().available / 2**30:.1f} GiB; AC power {on_ac()}")
        return 0
    if not acquire_lock():
        log("refused: another run_bridge.py instance is running")
        return 3
    problems = check_inputs(phase)
    if problems:
        log("refused: " + "; ".join(problems))
        return 4
    keep_awake()
    (LOG_ROOT / phase).mkdir(parents=True, exist_ok=True)
    (OUT_ROOT / phase).mkdir(parents=True, exist_ok=True)
    workers = 1 if phase == "regress" else args.workers
    results: dict[str, dict] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        futures = [pool.submit(run_job, phase, *job) for job in jobs(phase)]
        for future in concurrent.futures.as_completed(futures):
            name, state, info = future.result()
            results[name] = {"state": state, **(info or {})}
            write_progress(phase, results)
    if phase == "regress":
        after_regress()
    elif phase == "smoke":
        after_smoke()
    write_progress(phase, results)
    log(f"phase {phase} finished")
    return 0


if __name__ == "__main__":
    sys.exit(main())
