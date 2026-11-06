"""Driver for the full-node Via cells (ADR-089, GĐ3): real RideBound jobs (FleetPy + adapter + C# Runner) on vf cells.

Built from via-real-2026-10-26/run_via.py (same rails: one instance per phase, STOP file, AC power, keep-awake, RAM and
disk floors, one admission at a time, never overwrite an output or a log, failed outputs kept). Differences:
  inputs     E:/RideBoundData/research/via-full-v1 (convert_via_full.py; sealed in VIA-FULL-PREDICTIONS.sha256), with
             drivers-run/<cell>.driver.json = the sealed driver plus routingEngine complete-graph-direct-v1 and
             transcriptCompression gzip-v1 (make_run_drivers.py)
  Runner     E:/RideBoundData/research/runner-fastrun-final1 (this branch, Release)
  adapter    the run worktree E:/Code/RideBound-fastrun-run (59c4ee8 + this branch's changes, copied byte for byte),
             through bridge_launcher_via_full.py and world-via-full/preflight_world_full.py
  configs    arm policy configs as bridged (via-bridge-2026-10-22/configs: U, RCE35, EOHZ35, G2-*;
             bridge-phase1-2026-10-23/configs: EO35, G2-EO35) with the fast WP4 configs (configs/: A1 budgets,
             auditHash light-v1, solver evidence off; EO35 arms forced-reference-v1)
  env        heap cap 6 GiB; Runner input and output line limits 1 GiB
Phases: smoke = VIA-EOHZ35 on the w08 cell; main16 = the 16 jobs (8 arms x 2 cells), smoke job included if passed.

Usage: python -B run_via_full.py check|smoke|main16 [--workers N]
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
PHASE1 = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/bridge-phase1-2026-10-23")
BRIDGE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
sys.path.insert(0, str(PHASE1))
import inventory  # noqa: E402

PYTHON = pathlib.Path(r"E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe")
FLEETPY = pathlib.Path(r"E:/RideBoundData/wp7/FleetPy-1.0.2")
DOTNET = pathlib.Path(r"C:/Program Files/dotnet/dotnet.exe")
ADAPTER_REPO = pathlib.Path(r"E:/Code/RideBound-fastrun-run")
WRAPPER = HERE / "world-via-full" / "preflight_world_full.py"
LAUNCHER = HERE / "bridge_launcher_via_full.py"
RUNNER = pathlib.Path(r"E:/RideBoundData/research/runner-fastrun-final1")
DATA = pathlib.Path(r"E:/RideBoundData/research/via-full-v1")
OUT_ROOT = DATA / "runs"
LOG_ROOT = HERE / "logs-full"
STOP_FILE = HERE / "STOP"
SEAL = HERE / "VIA-FULL-PREDICTIONS.sha256"

ARMS = ("VIA-U", "VIA-RCE35", "VIA-EOHZ35", "VIA-EO35", "VIA-G2K8-U", "VIA-G2K8-RCE35", "VIA-G2K8-EOHZ35",
        "VIA-G2K8-EO35")
CELLS = ("d20190213-vf-r0-w08", "d20190213-vf-r0-w17")
SMOKE = ("VIA-EOHZ35", "d20190213-vf-r0-w08")

POWER_WAIT_S = 1200
RAM_FLOOR = int(3.0 * 2**30)
RAM_WAIT_S = 1800
DISK_FLOORS = {"C:\\": 30 * 2**30, "E:\\": 40 * 2**30}
JOB_ENV = {
    "PYTHONDONTWRITEBYTECODE": "1",
    "PYTHONIOENCODING": "utf-8",
    "RB_REALJOB_BUFFERED_READ": "1",
    "RB_REALJOB_TIMEOUT_S": "900",
    "DOTNET_GCHeapHardLimit": "0x180000000",
    "RB_VIA_MAX_OUTPUT_LINE_BYTES": str(2**30),
    "RB_VIA_MAX_INPUT_LINE_BYTES": str(2**30),
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


def jobs(phase: str):
    if phase == "smoke":
        return [(f"{SMOKE[0]}-{SMOKE[1]}", *SMOKE)]
    # The smoke job is not run again once it passed: compare_via_full.py reads it from runs/smoke.
    smoke_name = f"{SMOKE[0]}-{SMOKE[1]}"
    skip = {smoke_name} if state_of("smoke", smoke_name) == "status-pass" else set()
    return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in ARMS if f"{arm}-{cell}" not in skip]


def config_for(arm: str) -> pathlib.Path:
    name = arm.removeprefix("VIA-").replace("G2K8-", "G2-")
    root = PHASE1 / "configs" if name.endswith("EO35") and "EOHZ" not in name else BRIDGE / "configs"
    return root / f"{name}.json"


def wp4_for(arm: str) -> pathlib.Path:
    g2 = "G2K8" in arm
    forced = arm.endswith("-EO35")
    return HERE / "configs" / f"wp4-via-{'g2k8' if g2 else 'ins'}-a1{'-forced' if forced else ''}-fast.json"


def inputs(arm: str, cell: str) -> dict:
    return {"label": f"{arm}-{cell}-vfr", "config": config_for(arm), "wp4": wp4_for(arm),
            "fixture": DATA / "fixtures" / cell, "driver": DATA / "drivers-run" / f"{cell}.driver.json",
            "world": DATA / "worlds" / f"{cell}.world.json"}


def command(phase: str, name: str, arm: str, cell: str, expected_inventory: str) -> list[str]:
    spec = inputs(arm, cell)
    fixture = spec["fixture"]
    return [
        str(PYTHON), "-B", str(LAUNCHER), str(WRAPPER),
        "--label", spec["label"],
        "--fleetpy-root", str(FLEETPY),
        "--runner-root", str(RUNNER),
        "--dotnet", str(DOTNET),
        "--commitment-config", str(spec["config"]),
        "--wp4-config", str(spec["wp4"]),
        "--scenario", str(fixture / "scenario-content.json"),
        "--derivative-manifest", str(fixture / "derivative-manifest.json"),
        "--normalization-report", str(fixture / "normalization-report.json"),
        "--selection-frame", str(fixture / "selection-frame.json"),
        "--driver", str(spec["driver"]),
        "--output", str(OUT_ROOT / phase / name),
        "--repeats", "1",
        "--master-seed", "7",
        "--expected-repository-inventory-sha256", expected_inventory,
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


def check_seal(problems: list[str]) -> None:
    roots = {"predictions/": DATA, "fixtures/": DATA, "drivers/": DATA, "worlds/": DATA}
    for line in SEAL.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        digest, rel = line.split(None, 1)
        rel = rel.lstrip("*")
        root = next((r for prefix, r in roots.items() if rel.startswith(prefix)), HERE)
        path = root / rel
        if not path.exists() or sha256(path) != digest:
            problems.append(f"sealed input changed or missing: {path}")


def check_inputs(phase: str) -> tuple[list[str], str]:
    problems: list[str] = []
    for path in (PYTHON, FLEETPY, DOTNET, WRAPPER, LAUNCHER, RUNNER, ADAPTER_REPO):
        if not path.exists():
            problems.append(f"missing {path}")
    check_seal(problems)
    for name, arm, cell in jobs(phase):
        spec = inputs(arm, cell)
        for key in ("config", "wp4", "driver", "world"):
            if not spec[key].exists():
                problems.append(f"{name}: missing {key} {spec[key]}")
        if not (spec["fixture"] / "scenario-content.json").exists():
            problems.append(f"{name}: missing fixture {spec['fixture']}")
    actual = inventory.repository_inventory_sha256(ADAPTER_REPO.resolve())
    return problems, actual


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


_start_lock = threading.Lock()


def run_job(phase: str, name: str, arm: str, cell: str, expected_inventory: str):
    with _start_lock:
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
        process = subprocess.Popen(command(phase, name, arm, cell, expected_inventory), cwd=str(ADAPTER_REPO),
                                   env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                                   encoding="utf-8", errors="replace")
        time.sleep(5)
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


def acquire_lock(phase: str) -> bool:
    lock = HERE / f"run-via-full-{phase}.lock"
    if lock.exists():
        try:
            pid = int(lock.read_text(encoding="utf-8").split()[0])
            if psutil.pid_exists(pid) and "python" in psutil.Process(pid).name().lower():
                return False
        except (ValueError, IndexError, psutil.Error):
            pass
    lock.write_text(f"{os.getpid()} {now()}\n", encoding="utf-8")
    return True


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "smoke", "main16"))
    parser.add_argument("--workers", type=int, default=1)
    args = parser.parse_args()
    phase = args.phase
    if phase == "check":
        for p in ("smoke", "main16"):
            problems, inventory_sha = check_inputs(p)
            log(f"check {p}: {'OK' if not problems else problems}; adapter inventory {inventory_sha}")
        log("disk: " + ("OK" if not disk_problems() else "; ".join(disk_problems())))
        log(f"RAM available {psutil.virtual_memory().available / 2**30:.1f} GiB; AC power {on_ac()}")
        return 0
    if not acquire_lock(phase):
        log(f"refused: another run_via_full.py instance of {phase} is running")
        return 3
    problems, inventory_sha = check_inputs(phase)
    if problems:
        log("refused: " + "; ".join(problems))
        return 4
    log(f"adapter inventory {inventory_sha} (checked again by every job at start and end)")
    keep_awake()
    (LOG_ROOT / phase).mkdir(parents=True, exist_ok=True)
    (OUT_ROOT / phase).mkdir(parents=True, exist_ok=True)
    results: dict[str, dict] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(run_job, phase, *job, inventory_sha) for job in jobs(phase)]
        for future in concurrent.futures.as_completed(futures):
            name, state, info = future.result()
            results[name] = {"state": state, **(info or {})}
            (HERE / f"PROGRESS-VIA-FULL-{phase}.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    log(f"phase {phase} finished: " + json.dumps({k: v["state"] for k, v in results.items()}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
