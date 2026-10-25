"""Driver for the Via-zone bridge: real RideBound jobs on the zone-level Via fixtures (owner order 2026-10-26).

Built from bridge-phase1-2026-10-23/run_p1.py (same rails, same job machinery); only constants, arms and phases differ.

Inputs (all hash-checked before a phase starts):
  fixtures/drivers/worlds  E:/RideBoundData/research/via-real-v1 (convert_via_zone.py; VIA-INPUTS.sha256)
  Runner                   C:/RideBoundData/research/runner-p1-dwell-v1 (published from research/p1-dwell 9ace339;
                           the dwell-off replay gate reproduced 228/228 recorded bridge jobs byte for byte, PHASE1-LOG P9)
  adapter                  C:/Code/RideBound-p1fix at 6ab03fc (the fix1 adapter; its Python is identical in p1-dwell),
                           through bridge_launcher_via.py and world-via/ (byte copies of the fix1 launcher and wrapper,
                           the launcher plus the output-line-limit patch (3))
  configs                  via-bridge-2026-10-22/configs: U, RCE35, EOHZ35 (insertion-only, WP4 tang3 cost-first
                           37040657...) and G2-U, G2-RCE35, G2-EOHZ35 (WP4 G2 cost-first k8, bridge-phase1 configs)
Arms: VIA-U, VIA-RCE35, VIA-EOHZ35, VIA-G2K8-U, VIA-G2K8-RCE35, VIA-G2K8-EOHZ35 on d20190213-vz-r0-w08 / -w17.
Phases:
  trial    1 job: VIA-RCE35-d20190213-vz-r0-w08. Needs VIA-READY.txt. RAN 2026-10-26 01:07-01:11 with the bridged WP4
           budgets and FAILED CLOSED (VIA-LOG.md A1): the decision-level generation budget of 10,000 work units is
           summed over all vehicles (SolverBackedRidePoolingPolicy.cs:520-524) and 62 vehicles exceed it
           (CandidateSelectionExecution.cs:99-112). Output and log kept; the phase is never run again.
  trialA1  the same job with the amendment-A1 WP4 configs (configs/wp4-via-*-a1.json: decision-level generation and
           validation budgets 5,000,000, solver budget 10,000,000 work units / 30 s deterministic; per-vehicle caps,
           100 candidates and 10,000 exploration units, unchanged). Needs VIA-READY-A1.txt.
  mainA1   the other 11 jobs with the A1 configs. Needs TRIAL-REVIEWED.txt (written after compare_via.py on trialA1).
Rails (as run_p1.py): one instance (PID lock), STOP file, AC power only (waits 20 min), keeps the PC awake, RAM floor
before each job, disk floors C >= 30 GiB / E >= 40 GiB, never overwrites an output or a log, failed outputs are kept.
Do not edit C:/Code/RideBound-p1fix while jobs run (the preflight checks its inventory at start and end).
Job environment differs from run_p1.py in two values (neither changes a decision):
  DOTNET_GCHeapHardLimit 0x80000000 (2 GiB instead of 896 MiB: 62 vehicles and 509 requests; the bridge regress
  phase showed the GC cap changes no decision), RB_VIA_MAX_OUTPUT_LINE_BYTES 512 MiB (launcher patch (3)).

Usage: python -B run_via.py check|trialA1|mainA1 [--workers N]
Disk floors are the bridge drivers' (C 30 GiB, E 40 GiB); every output and log of this driver goes to E: or the kit.
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
sys.path.insert(0, str(PHASE1))
import inventory  # noqa: E402  (= the preflight's repository inventory function)

BRIDGE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
TANG3 = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/tang3")
PYTHON = pathlib.Path(r"E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe")
FLEETPY = pathlib.Path(r"E:/RideBoundData/wp7/FleetPy-1.0.2")
DOTNET = pathlib.Path(r"C:/Program Files/dotnet/dotnet.exe")
ADAPTER_REPO = pathlib.Path(r"C:/Code/RideBound-p1fix")
WRAPPER = HERE / "world-via" / "preflight_world_v1_2.py"
LAUNCHER = HERE / "bridge_launcher_via.py"
RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-p1-dwell-v1")
RUNNER_MANIFEST = PHASE1 / "runner-p1-dwell-v1.sha256.tsv"
WP4_INS_BRIDGED = TANG3 / "stage1b" / "harness" / "wp4-tier3-v2-costfirst.json"
WP4_INS_SHA256 = "37040657b8f6c16c8eb8430a208bfd4b155791668af0f1da49110f0f372300ae"   # run_r128.py:28
WP4_K8_BRIDGED = PHASE1 / "configs" / "wp4-g2-costfirst-k8.json"
# Amendment A1 (VIA-LOG.md): the same files with only the four solverExecution budgets raised (VIA-WP4-A1.sha256).
WP4_INS_A1 = HERE / "configs" / "wp4-via-ins-a1.json"
WP4_K8_A1 = HERE / "configs" / "wp4-via-g2k8-a1.json"
A1_PHASES = ("trialA1", "mainA1")
# Amendment A3 (VIA-LOG.md): trialA1 ran all its decisions but the Runner failed on the final checkpoint near its 2 GiB
# GC heap cap; mainA1 was stopped. Phase runA3 = all 12 jobs, A1 WP4 configs, heap cap 6 GiB, launcher with the
# runner-stderr diagnostic (4), ordered so the main comparisons come first, admissions spaced by --stagger-seconds.
A3_PHASE = "runA3"
LAUNCHER_A3 = HERE / "bridge_launcher_via_a3.py"
A3_ORDER = ("VIA-RCE35", "VIA-EOHZ35", "VIA-G2K8-RCE35", "VIA-G2K8-EOHZ35", "VIA-U", "VIA-G2K8-U")
STAGGER_S = 0
EXPECTED_INVENTORY = (HERE / "ADAPTER-INVENTORY.txt").read_text(encoding="utf-8").split()[0]
DATA = pathlib.Path(r"E:/RideBoundData/research/via-real-v1")
OUT_ROOT = DATA / "runs"
LOG_ROOT = HERE / "logs"
STOP_FILE = HERE / "STOP"
LOCK = HERE / "run-via-instance.lock"
PROGRESS = HERE / "PROGRESS-VIA.txt"

ARMS = ("VIA-U", "VIA-RCE35", "VIA-EOHZ35", "VIA-G2K8-U", "VIA-G2K8-RCE35", "VIA-G2K8-EOHZ35")
CELLS = ("d20190213-vz-r0-w08", "d20190213-vz-r0-w17")
TRIAL = ("VIA-RCE35", "d20190213-vz-r0-w08")

POWER_WAIT_S = 1200
RAM_FLOOR = int(3.0 * 2**30)
RAM_WAIT_S = 1800
DISK_FLOORS = {"C:\\": 30 * 2**30, "E:\\": 40 * 2**30}
JOB_ENV = {
    "PYTHONDONTWRITEBYTECODE": "1",
    "PYTHONIOENCODING": "utf-8",
    "RB_REALJOB_BUFFERED_READ": "1",
    "RB_REALJOB_TIMEOUT_S": "900",
    "DOTNET_GCHeapHardLimit": "0x80000000",
    "RB_VIA_MAX_OUTPUT_LINE_BYTES": str(512 * 2**20),
}
JOB_ENV_A3 = {
    "PYTHONDONTWRITEBYTECODE": "1",
    "PYTHONIOENCODING": "utf-8",
    "RB_REALJOB_BUFFERED_READ": "1",
    "RB_REALJOB_TIMEOUT_S": "900",
    "DOTNET_GCHeapHardLimit": "0x180000000",
    "RB_VIA_MAX_OUTPUT_LINE_BYTES": str(512 * 2**20),
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
    if phase == A3_PHASE:
        return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in A3_ORDER]
    if phase in ("trial", "trialA1"):
        return [(f"{TRIAL[0]}-{TRIAL[1]}", TRIAL[0], TRIAL[1])]
    return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in ARMS if (arm, cell) != TRIAL]


def config_for(arm: str) -> pathlib.Path:
    name = arm.removeprefix("VIA-").replace("G2K8-", "G2-")
    return BRIDGE / "configs" / f"{name}.json"


def wp4_for(arm: str, phase: str) -> pathlib.Path:
    if phase in A1_PHASES or phase == A3_PHASE:
        return WP4_K8_A1 if "G2K8" in arm else WP4_INS_A1
    return WP4_K8_BRIDGED if "G2K8" in arm else WP4_INS_BRIDGED


def inputs(arm: str, cell: str) -> dict:
    return {
        "label": f"{arm}-{cell}-vzr",
        "config": config_for(arm),
        "fixture": DATA / "fixtures" / cell,
        "driver": DATA / "drivers" / f"{cell}.driver.json",
        "world": DATA / "worlds" / f"{cell}.world.json",
    }


def command(phase: str, name: str, arm: str, cell: str) -> list[str]:
    spec = inputs(arm, cell)
    fixture = spec["fixture"]
    return [
        str(PYTHON), "-B", str(LAUNCHER_A3 if phase == A3_PHASE else LAUNCHER), str(WRAPPER),
        "--label", spec["label"],
        "--fleetpy-root", str(FLEETPY),
        "--runner-root", str(RUNNER),
        "--dotnet", str(DOTNET),
        "--commitment-config", str(spec["config"]),
        "--wp4-config", str(wp4_for(arm, phase)),
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

def check_manifest(manifest: pathlib.Path, root: pathlib.Path, problems: list[str]) -> None:
    for line in manifest.read_text(encoding="utf-8").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        digest, rel = line.split(None, 1)
        path = root / rel.lstrip("*").removeprefix("./")
        if not path.exists() or sha256(path) != digest:
            problems.append(f"input changed or missing: {path}")


def check_inputs(phase: str) -> list[str]:
    problems: list[str] = []
    for path in (PYTHON, FLEETPY, DOTNET, WRAPPER, LAUNCHER, RUNNER, WP4_INS_BRIDGED, WP4_K8_BRIDGED,
                 WP4_INS_A1, WP4_K8_A1):
        if not path.exists():
            problems.append(f"missing {path}")
    if WP4_INS_BRIDGED.exists() and sha256(WP4_INS_BRIDGED) != WP4_INS_SHA256:
        problems.append("WP4 cost-first config SHA-256 mismatch")
    check_manifest(HERE / "VIA-WP4-A1.sha256", HERE / "configs", problems)
    verify = subprocess.run([str(PYTHON), "-B", str(TANG3 / "runner_manifest.py"), "verify", str(RUNNER),
                             str(RUNNER_MANIFEST)], capture_output=True, text=True)
    if verify.returncode != 0 or not verify.stdout.startswith("OK"):
        problems.append(f"runner manifest: {verify.stdout.strip()} {verify.stderr.strip()}")
    check_manifest(HERE / "VIA-CONFIGS.sha256", BRIDGE / "configs", problems)       # the six arm configs
    check_manifest(PHASE1 / "configs-r128.sha256.txt", PHASE1 / "configs", problems)  # includes wp4 k8
    check_manifest(HERE / "VIA-INPUTS.sha256", DATA, problems)                        # fixtures, drivers, worlds, predictions
    check_manifest(HERE / "VIA-KIT.sha256", HERE, problems)                           # converter, emulator, launcher, wrapper
    if phase == A3_PHASE:
        check_manifest(HERE / "VIA-KIT-A3.sha256", HERE, problems)                    # the A3 launcher
        if not (HERE / "VIA-READY-A3.txt").exists():
            problems.append("runA3 refused: VIA-READY-A3.txt does not exist")
    actual = inventory.repository_inventory_sha256(ADAPTER_REPO.resolve())
    if actual != EXPECTED_INVENTORY:
        problems.append(f"adapter repository inventory {actual} != expected {EXPECTED_INVENTORY}")
    for name, arm, cell in jobs(phase):
        spec = inputs(arm, cell)
        for key in ("config", "driver", "world"):
            if not spec[key].exists():
                problems.append(f"{name}: missing {key} {spec[key]}")
        if not wp4_for(arm, phase).exists():
            problems.append(f"{name}: missing wp4 {wp4_for(arm, phase)}")
        if not (spec["fixture"] / "scenario-content.json").exists():
            problems.append(f"{name}: missing fixture {spec['fixture']}")
    if phase in ("trial", "main"):
        problems.append(f"{phase} refused: superseded by amendment A1 (use trialA1 / mainA1)")
    if phase == "trialA1" and not (HERE / "VIA-READY-A1.txt").exists():
        problems.append("trialA1 refused: VIA-READY-A1.txt does not exist")
    # Amendment A2 (VIA-LOG.md): mainA1 may also start on MAINA1-EARLY-START.txt, written while trialA1 is still running.
    if phase == "mainA1" and not ((HERE / "TRIAL-REVIEWED.txt").exists() or (HERE / "MAINA1-EARLY-START.txt").exists()):
        problems.append("mainA1 refused: neither TRIAL-REVIEWED.txt nor MAINA1-EARLY-START.txt exists")
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
        env = dict(os.environ, **(JOB_ENV_A3 if phase == A3_PHASE else JOB_ENV),
                   RB_WORLD_CONFIG=str(inputs(arm, cell)["world"]))
        started = time.time()
        log(f"start {phase}/{name}")
        process = subprocess.Popen(command(phase, name, arm, cell), cwd=str(ADAPTER_REPO), env=env,
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                                   encoding="utf-8", errors="replace")
        time.sleep(max(5, STAGGER_S))       # let the process tree appear (A3: and space the final checkpoints)
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


def acquire_lock(phase: str) -> bool:
    # A2: one instance per phase (trialA1 keeps the original lock file), so mainA1 can run beside trialA1. Admission of
    # each job still checks AC power, disk floors and the RAM floor.
    lock = LOCK if phase == "trialA1" else HERE / f"run-via-instance-{phase}.lock"
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
    parser.add_argument("phase", choices=("check", "trialA1", "mainA1", A3_PHASE))
    parser.add_argument("--workers", type=int, default=1)
    parser.add_argument("--stagger-seconds", type=int, default=0)
    args = parser.parse_args()
    global STAGGER_S
    STAGGER_S = args.stagger_seconds
    phase = args.phase
    if phase == "check":
        for p in (*A1_PHASES, A3_PHASE):
            problems = check_inputs(p)
            log(f"check {p}: {'OK' if not problems else problems}")
        log("disk: " + ("OK" if not disk_problems() else "; ".join(disk_problems())))
        log(f"RAM available {psutil.virtual_memory().available / 2**30:.1f} GiB; AC power {on_ac()}")
        return 0
    if not acquire_lock(phase):
        log(f"refused: another run_via.py instance of {phase} is running")
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
