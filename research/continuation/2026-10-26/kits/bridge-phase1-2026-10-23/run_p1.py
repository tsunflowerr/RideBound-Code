"""Driver for bridge phase 1 (owner approval 2026-10-23: "Đồng ý" to run the company wf15 emulator and start phase 1).
Built from via-bridge-2026-10-22/run_g2.py v4 (same rails); only constants, arms, phases and gate files differ.

Arms (11 Manhattan bridge cells, 2.0x, cost-first, measured-day world W5, seed 7, runner-g2-v3, G2 worktree adapter):
  1.1 strict deadlines, recovery forced-reference-v1 (a forced vehicle keeps its route):
      EO35, DLS-120 (insertion-only) and G2-EO35, G2-DLS-120 (G2, every other vehicle a receiver);
  1.2 G2 with at most 3 receiver vehicles (maximumTargetVehicles 3): G2K3-U, G2K3-RCE35, G2K3-EOHZ35
      (the policy configs of the bridged G2-U, G2-RCE35, G2-EOHZ35).
Phases:
  p1pilot  7 jobs, the 7 arms on d20181114-s10-r1-w08. Needs P1-READY.txt.
  p1main   70 jobs, the other 10 cells x 7 arms. Needs P1-PILOT-REVIEWED.txt (written after compare_p1.py on the pilot).

Rails (as run_g2.py): one instance (PID lock), STOP file, AC power only (waits 20 min), keeps the PC awake, RAM floor
before each job, disk floors C >= 30 GiB / E >= 40 GiB, never overwrites an output or a log, failed outputs are kept.
Do not edit C:/Code/RideBound-g2 while jobs run (the preflight checks its inventory at start and end).

Usage: python -B run_p1.py check|p1pilot|p1main [--workers N]
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
sys.path.insert(0, str(HERE))
import inventory  # noqa: E402

BRIDGE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
TANG3 = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/tang3")
PYTHON = pathlib.Path(r"E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe")
FLEETPY = pathlib.Path(r"E:/RideBoundData/wp7/FleetPy-1.0.2")
DOTNET = pathlib.Path(r"C:/Program Files/dotnet/dotnet.exe")
ADAPTER_REPO = pathlib.Path(r"C:/Code/RideBound-g2")
G2_KIT = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/g2-2026-10-23")
WRAPPER = G2_KIT / "world" / "preflight_world_v1_2.py"
LAUNCHER = BRIDGE / "bridge_launcher_g2.py"          # adapter package from the G2 worktree (AMENDMENTS A8)
RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-g2-v3")
RUNNER_MANIFEST = G2_KIT / "runner-g2-v3.sha256.tsv"
# Inventory of C:/Code/RideBound-g2 at HEAD 937bb31 (clean), computed by inventory.py (= the preflight's function).
EXPECTED_INVENTORY = (HERE / "P1-INVENTORY.txt").read_text(encoding="utf-8").split()[0]
FIXTURES = pathlib.Path(r"C:/RideBoundData/research/fixtures-via-bridge-2000-v1")
OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1/p1")
LOG_ROOT = HERE / "logs"
STOP_FILE = HERE / "STOP"
LOCK = HERE / "run-p1-instance.lock"
PROGRESS = HERE / "PROGRESS-P1.txt"

ARMS = ("EO35", "DLS-120", "G2-EO35", "G2-DLS-120", "G2K3-U", "G2K3-RCE35", "G2K3-EOHZ35")
PILOT_CELL = "d20181114-s10-r1-w08"
CELLS = tuple(f"d201811{d}-s10-r1-w08" for d in ("12", "13", "14", "15", "16", "17", "18")) + \
        tuple(f"d201811{d}-s10-r1-w17" for d in ("12", "13", "14", "18"))

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
    if phase == "p1pilot":
        return [(f"{arm}-{PILOT_CELL}", arm, PILOT_CELL) for arm in ARMS]
    return [(f"{arm}-{cell}", arm, cell) for cell in CELLS if cell != PILOT_CELL for arm in ARMS]


def config_for(arm: str) -> pathlib.Path:
    if arm.startswith("G2K3-"):
        return BRIDGE / "configs" / f"G2-{arm.removeprefix('G2K3-')}.json"
    return HERE / "configs" / f"{arm}.json"


def wp4_for(arm: str) -> pathlib.Path:
    if arm.startswith("G2K3-"):
        return HERE / "configs" / "wp4-g2-costfirst-k3.json"
    if arm.startswith("G2-"):
        return HERE / "configs" / "wp4-g2-costfirst-forced.json"
    return HERE / "configs" / "wp4-costfirst-forced.json"


def inputs(arm: str, cell: str) -> dict:
    return {
        "label": f"{arm}-{cell}-vbr",
        "config": config_for(arm),
        "fixture": FIXTURES / cell,
        "driver": BRIDGE / "drivers" / f"{cell}.driver.json",
        "world": BRIDGE / "worlds" / f"W5-{cell[:9]}-{cell[-3:]}.json",
    }


def command(phase: str, name: str, arm: str, cell: str) -> list[str]:
    spec = inputs(arm, cell)
    fixture = spec["fixture"]
    return [
        str(PYTHON), "-B", str(LAUNCHER), str(WRAPPER),
        "--label", spec["label"],
        "--fleetpy-root", str(FLEETPY),
        "--runner-root", str(RUNNER),
        "--dotnet", str(DOTNET),
        "--commitment-config", str(spec["config"]),
        "--wp4-config", str(wp4_for(arm)),
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
    for path in (PYTHON, FLEETPY, DOTNET, WRAPPER, LAUNCHER, RUNNER):
        if not path.exists():
            problems.append(f"missing {path}")
    verify = subprocess.run([str(PYTHON), "-B", str(TANG3 / "runner_manifest.py"), "verify", str(RUNNER),
                             str(RUNNER_MANIFEST)], capture_output=True, text=True)
    if verify.returncode != 0 or not verify.stdout.startswith("OK"):
        problems.append(f"runner manifest: {verify.stdout.strip()} {verify.stderr.strip()}")
    for manifest, root in ((BRIDGE / "configs-worlds.sha256.txt", BRIDGE),
                           (BRIDGE / "configs-g2.sha256.txt", BRIDGE / "configs"),
                           (HERE / "configs-p1.sha256.txt", HERE / "configs"),
                           (G2_KIT / "world-g2.sha256.txt", G2_KIT / "world"),
                           (BRIDGE / "drivers.sha256.txt", BRIDGE / "drivers"),
                           (BRIDGE / "fixtures-via-bridge-2000-v1.sha256.txt", FIXTURES)):
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            digest, rel = line.split(None, 1)
            path = root / rel.lstrip("*").removeprefix("./")
            if not path.exists() or sha256(path) != digest:
                problems.append(f"input changed or missing: {path}")
    actual = inventory.repository_inventory_sha256(ADAPTER_REPO.resolve())
    if actual != EXPECTED_INVENTORY:
        problems.append(f"adapter repository inventory {actual} != expected {EXPECTED_INVENTORY}")
    for name, arm, cell in jobs(phase):
        spec = inputs(arm, cell)
        for key in ("config", "driver", "world"):
            if not spec[key].exists():
                problems.append(f"{name}: missing {key} {spec[key]}")
        if not wp4_for(arm).exists():
            problems.append(f"{name}: missing wp4 {wp4_for(arm)}")
        if not (spec["fixture"] / "scenario-content.json").exists():
            problems.append(f"{name}: missing fixture {spec['fixture']}")
    if phase == "p1pilot" and not (HERE / "P1-READY.txt").exists():
        problems.append("p1pilot refused: P1-READY.txt does not exist")
    if phase == "p1main" and not (HERE / "P1-PILOT-REVIEWED.txt").exists():
        problems.append("p1main refused: P1-PILOT-REVIEWED.txt does not exist")
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
        env = dict(os.environ, **JOB_ENV, RB_WORLD_CONFIG=str(inputs(arm, cell)["world"]))
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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "p1pilot", "p1main"))
    parser.add_argument("--workers", type=int, default=3)
    args = parser.parse_args()
    phase = args.phase
    if phase == "check":
        for p in ("p1pilot", "p1main"):
            problems = check_inputs(p)
            log(f"check {p}: {'OK' if not problems else problems}")
        log("disk: " + ("OK" if not disk_problems() else "; ".join(disk_problems())))
        log(f"RAM available {psutil.virtual_memory().available / 2**30:.1f} GiB; AC power {on_ac()}")
        return 0
    if not acquire_lock():
        log("refused: another run_p1.py instance is running")
        return 3
    problems = check_inputs(phase)
    if problems:
        log("refused: " + "; ".join(problems))
        return 4
    keep_awake()
    (LOG_ROOT / phase).mkdir(parents=True, exist_ok=True)
    (OUT_ROOT / phase).mkdir(parents=True, exist_ok=True)
    results: dict[str, dict] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(run_job, phase, *job) for job in jobs(phase)]
        for future in concurrent.futures.as_completed(futures):
            name, state, info = future.result()
            results[name] = {"state": state, **(info or {})}
            write_progress(phase, results)
    write_progress(phase, results)
    log(f"phase {phase} finished")
    return 0


if __name__ == "__main__":
    sys.exit(main())
