"""Driver for the G2 bridge (waiting-rider relocation; owner "bắt đầu bước 1,2", 2026-10-22).
Built from run_bridge.py (same rails); only constants, phases and verdict files differ.

Phases:
  g2regress  2 jobs. U and RCE35 (G2 OFF: the existing configs and WP4) on d20181114-s10-r1-w08, with the G2
             Runner (runner-g2-v2) and the G2 worktree adapter (C:/Code/RideBound-g2). Pass = compare_runs.py
             identical to main/U-... and elmi/RCE35-... (the adapter changes and the G2 code change nothing).
  g2pilot    3 jobs. G2-U, G2-RCE35, G2-EOHZ35 on d20181114-s10-r1-w08 (G2 ON). Needs G2REGRESS-PASSED.txt.
             Compared with the sealed emulator (EMULATOR-PREDICTIONS-G2.sha256) before g2main.
  g2main     30 jobs. The other 10 cells x 3 G2 arms. Needs G2-PILOT-REVIEWED.txt (written by hand after the
             pilot comparison).

Original docstring of run_bridge.py follows.

Driver for the Manhattan bridge (company plan step 1; owner approval 2026-10-21).

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
ADAPTER_REPO = pathlib.Path(r"C:/Code/RideBound-g2")
G2_KIT = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/g2-2026-10-23")
WRAPPER = G2_KIT / "world" / "preflight_world_v1_2.py"   # copy of tang3/world with REPO = the G2 worktree
# v4 (AMENDMENTS A8): bridge_launcher.py imports the adapter package from E:/Code/RideBound-deadline, which made every
# earlier G2 job run that adapter. The G2 copy differs only in that path.
LAUNCHER = HERE / "bridge_launcher_g2.py"
COMPARE = HERE / "compare_runs.py"
# v3 (2026-10-23, AMENDMENTS A6): runner-g2-v3 = v2 + the Contracts evidence fix; every output, log and gate
# file of this version has its own name, so the failed v2 pilot and the v2 regress stay as recorded.
VERSION = "v4"
RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-g2-v3")   # unchanged in v4: no RideBound change
RUNNER_MANIFEST = G2_KIT / "runner-g2-v3.sha256.tsv"
WP4 = TANG3 / "stage1b" / "harness" / "wp4-tier3-v2-costfirst.json"
WP4_SHA256 = "37040657b8f6c16c8eb8430a208bfd4b155791668af0f1da49110f0f372300ae"   # run_stage1b-v7.py:166
# The G2 worktree inventory, frozen in G2-INVENTORY.txt when the code was frozen for the bridge.
EXPECTED_INVENTORY = (G2_KIT / f"G2-INVENTORY-{VERSION}.txt").read_text(encoding="utf-8").split()[0]
WP4_G2 = HERE / "configs" / "wp4-g2-costfirst.json"
FIXTURES = pathlib.Path(r"C:/RideBoundData/research/fixtures-via-bridge-2000-v1")
BASE_ROOT = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1")   # archived main/elmi jobs
OUT_ROOT = BASE_ROOT / f"g2{VERSION}"
LOG_ROOT = HERE / "logs" / f"g2{VERSION}"
STOP_FILE = HERE / "STOP"
LOCK = HERE / "run-g2-instance.lock"
PROGRESS = HERE / f"PROGRESS-G2-{VERSION}.txt"

ARMS = ("U", "C-120", "C-300", "PS-300", "DLHZ-120")
# Phase "elmi" (added 2026-10-23): the Elmi-budget arms (wf14 formula, alpha 0.35) on the same 11 cells, with the
# Runner that adds the slack-proportional budget on top of A0.1 (patch elmi-slack-budget-v1.patch, tree 32155156...).
# Refused until ELMI-READY.txt exists (written after the independent review and the 58-job replay gate pass).
G2_ARMS = ("G2-U", "G2-RCE35", "G2-EOHZ35")
PILOT_CELL = "d20181114-s10-r1-w08"
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

INS_ARMS = ("U", "C-120", "C-300", "PS-300", "DLHZ-120")
INS_ELMI_ARMS = ("RCE35", "EOHZ35")
# v4 rerun phase -> the earlier phase whose jobs it repeats with the same inputs (for comparison).
INS_SOURCE = {"insregress": "regress", "inssmoke": "smoke", "insmain": "main", "inselmi": "elmi"}


def jobs(phase: str):
    if phase == "g2all":
        return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in G2_ARMS]
    if phase == "insregress":
        return [("REG-CF-U-d20181114-s10-r1-w08-1500", "U", SMOKE_CELL)]
    if phase == "inssmoke":
        return [(f"{arm}-{SMOKE_CELL}", arm, SMOKE_CELL) for arm in ("PS-300", "DLHZ-120")]
    if phase == "insmain":
        return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in INS_ARMS]
    if phase == "inselmi":
        return [(f"{arm}-{cell}", arm, cell) for cell in CELLS for arm in INS_ELMI_ARMS]
    if phase == "g2regress":
        return [(f"{arm}-{PILOT_CELL}", arm, PILOT_CELL) for arm in ("U", "RCE35")]
    if phase == "g2pilot":
        return [(f"{arm}-{PILOT_CELL}", arm, PILOT_CELL) for arm in G2_ARMS]
    return [(f"{arm}-{cell}", arm, cell) for cell in CELLS if cell != PILOT_CELL for arm in G2_ARMS]


def runner_for(phase: str) -> tuple[pathlib.Path, pathlib.Path]:
    return (RUNNER, RUNNER_MANIFEST)


def wp4_for(arm: str) -> pathlib.Path:
    return WP4_G2 if arm.startswith("G2-") else WP4


def inputs(phase: str, arm: str, cell: str) -> dict:
    if phase == "insregress":
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
    runner, runner_manifest = runner_for(phase)
    for path in (PYTHON, FLEETPY, DOTNET, WRAPPER, LAUNCHER, COMPARE, runner, WP4, WP4_G2):
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
                           (HERE / "configs-g2.sha256.txt", HERE / "configs"),
                           (G2_KIT / "world-g2.sha256.txt", G2_KIT / "world"),
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
    if phase in ("g2all", *INS_SOURCE) and not (G2_KIT / f"G2-READY-{VERSION}.txt").exists():
        problems.append(f"{phase} refused: G2-READY-{VERSION}.txt does not exist")
    if phase == "g2regress" and not (G2_KIT / f"G2-READY-{VERSION}.txt").exists():
        problems.append(f"g2regress refused: G2-READY-{VERSION}.txt does not exist (review + G2-off replay gate first)")
    if phase == "g2pilot" and not (HERE / f"G2REGRESS-PASSED-{VERSION}.txt").exists():
        problems.append(f"g2pilot refused: G2REGRESS-PASSED-{VERSION}.txt does not exist")
    if phase == "g2main" and not (HERE / f"G2-PILOT-REVIEWED-{VERSION}.txt").exists():
        problems.append(f"g2main refused: G2-PILOT-REVIEWED-{VERSION}.txt does not exist")
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


A5_ALLOWED = {"$.payload.manifest.binarySha256", "$.payload.manifest.coreCommitSha"}


def _frame(path: pathlib.Path, ordinal: int):
    import base64
    with open(path, encoding="utf-8") as handle:
        for index, line in enumerate(handle):
            if index == ordinal:
                record = json.loads(line)
                frame = base64.b64decode(record["frameBase64"])
                if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
                    raise RuntimeError(f"{path}: frame {ordinal} hash mismatch")
                return json.loads(frame)
    raise RuntimeError(f"{path}: no frame {ordinal}")


def _paths(a, b, path="$"):
    if isinstance(a, dict) and isinstance(b, dict):
        out = []
        for key in sorted(set(a) | set(b)):
            out += [f"{path}.{key}"] if key not in a or key not in b else _paths(a[key], b[key], f"{path}.{key}")
        return out
    if isinstance(a, list) and isinstance(b, list) and len(a) == len(b):
        return [p for i, (x, y) in enumerate(zip(a, b)) for p in _paths(x, y, f"{path}[{i}]")]
    return [] if a == b else [path]


def a5_provenance_only(report: dict, archive: pathlib.Path, run: pathlib.Path) -> tuple[bool, str]:
    """AMENDMENTS A5: the only allowed difference is frame 2 (initializeRun), and inside it only the Runner build
    hash and the adapter repository commit. Every other frame identical (64-hex masked) and the summary identical."""
    if not report.get("summaryIdentical") or report.get("framesA") != report.get("framesB"):
        return False, "summary or frame count differs"
    if report.get("framesCompared", 0) - report.get("framesIdentical", 0) != 1:
        return False, "more than one frame differs"
    first = report.get("firstDifference") or {}
    if first.get("ordinal") != 2 or first.get("type") != "initializeRun":
        return False, f"the differing frame is not frame 2 initializeRun: {first}"
    diff = _paths(_frame(archive / "transcript-00.ndjson", 2), _frame(run / "transcript-00.ndjson", 2))
    if not diff or not set(diff) <= A5_ALLOWED:
        return False, f"frame 2 differs outside the provenance fields: {diff}"
    return True, f"A5: only frame 2 differs, at {sorted(diff)}"


def after_g2regress() -> None:
    """Each G2-off job against the job of the same arm and cell from the earlier phases."""
    lines, passed = [], True
    for name, arm, cell in jobs("g2regress"):
        archive = BASE_ROOT / ("elmi" if arm == "RCE35" else "main") / f"{arm}-{cell}"
        out = HERE / f"g2regress-{VERSION}-compare-{arm}.json"
        if out.exists():
            log(f"{out.name} exists; not re-comparing")
            passed = False
            continue
        run = OUT_ROOT / "g2regress" / name
        result = subprocess.run([str(PYTHON), "-B", str(COMPARE), str(archive), str(run), "--json", str(out)],
                                capture_output=True, text=True, env=dict(os.environ, PYTHONIOENCODING="utf-8"))
        if result.returncode == 0:
            ok, why = True, "identical"
        else:
            ok, why = a5_provenance_only(json.loads(out.read_text(encoding="utf-8")), archive, run)
        passed &= ok
        lines.append(f"{arm}: compare_runs rc={result.returncode}; verdict {'pass' if ok else 'FAIL'} ({why})\n{result.stdout}")
    verdict = f"G2REGRESS-PASSED-{VERSION}.txt" if passed else f"G2REGRESS-FAILED-{VERSION}.txt"
    (HERE / verdict).write_text(f"{now()}\n" + "\n".join(lines), encoding="utf-8")
    log(f"g2regress verdict: {verdict}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "g2all", "insregress", "inssmoke", "insmain", "inselmi",
                                          "g2regress", "g2pilot", "g2main"))
    parser.add_argument("--workers", type=int, default=2)
    args = parser.parse_args()
    phase = args.phase
    if phase == "check":
        for p in ("g2all", "insregress", "inssmoke", "insmain", "inselmi"):
            problems = check_inputs(p)
            log(f"check {p}: {'OK' if not problems else problems}")
        log("disk: " + ("OK" if not disk_problems() else "; ".join(disk_problems())))
        log(f"RAM available {psutil.virtual_memory().available / 2**30:.1f} GiB; AC power {on_ac()}")
        return 0
    if not acquire_lock():
        log("refused: another run_g2.py instance is running")
        return 3
    problems = check_inputs(phase)
    if problems:
        log("refused: " + "; ".join(problems))
        return 4
    keep_awake()
    (LOG_ROOT / phase).mkdir(parents=True, exist_ok=True)
    (OUT_ROOT / phase).mkdir(parents=True, exist_ok=True)
    workers = args.workers
    results: dict[str, dict] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        futures = [pool.submit(run_job, phase, *job) for job in jobs(phase)]
        for future in concurrent.futures.as_completed(futures):
            name, state, info = future.result()
            results[name] = {"state": state, **(info or {})}
            write_progress(phase, results)
    if phase == "g2regress":
        after_g2regress()
    write_progress(phase, results)
    log(f"phase {phase} finished")
    return 0


if __name__ == "__main__":
    sys.exit(main())
