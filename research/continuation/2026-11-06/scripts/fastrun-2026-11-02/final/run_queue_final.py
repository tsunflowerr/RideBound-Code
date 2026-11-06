"""Resumable job queue for the final runs (ADR-089 GĐ5/GĐ6, 2026-11-04). run_queue.py (GĐ5) plus one step after every
passing job: finalize_job.py writes the job's evidence (everything the comparisons and metrics read), reads it back
through the same functions, and only then keeps or drops the transcript as the job's `transcript` field says
("keep" or "drop"; default keep). A job whose finalize step fails keeps its transcript and is reported.

Everything else is run_queue.py's: a job whose output holds a passing summary.json is skipped (its evidence is made if
missing); an output folder left without one is renamed <name>.interrupted-<k> and kept; STOP file, free disk and free RAM
floors at admission, one admission at a time; logs are never overwritten.

Job fields: name, label, role, config, wp4, fixture, driver, world [, transcript].
Usage: python -B run_queue_final.py --jobs <jobs.json> --out <runs> --logs <logs> --stop <STOP file>
                                    [--workers 1] [--disk-floor-gib 20] [--ram-floor-gib 7]
       (repository, Runner, .NET, FleetPy and tools are the bundle's fixed paths below)
"""
import argparse
import concurrent.futures
import ctypes
import datetime as dt
import json
import os
import pathlib
import shutil
import subprocess
import sys
import threading
import time

import psutil

ROOT = pathlib.Path(r"E:/RideBoundData/final-v2")
TOOLS = ROOT / "tools"
REPO = ROOT / "repo"
RUNNER = ROOT / "runner"
DOTNET = ROOT / "dotnet" / "dotnet.exe"
FLEETPY = pathlib.Path(r"E:/RideBoundData/wp7/FleetPy-1.0.2")
FINALIZE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02/fullsys/finalize_job.py")

JOB_ENV = {
    "PYTHONDONTWRITEBYTECODE": "1",
    "PYTHONIOENCODING": "utf-8",
    "RB_REALJOB_BUFFERED_READ": "1",
    "RB_REALJOB_TIMEOUT_S": "900",
    "DOTNET_GCHeapHardLimit": "0x180000000",
    "RB_VIA_MAX_OUTPUT_LINE_BYTES": str(2**30),
    "RB_VIA_MAX_INPUT_LINE_BYTES": str(2**30),
}
_admission = threading.Lock()


def now() -> str:
    return dt.datetime.now().isoformat(timespec="seconds")


def log(line: str) -> None:
    print(f"{now()} {line}", flush=True)


def passed(folder: pathlib.Path) -> bool:
    try:
        return json.loads((folder / "summary.json").read_text(encoding="utf-8")).get("status") == "pass"
    except (OSError, ValueError):
        return False


def command(job: dict, output: pathlib.Path, inventory_sha: str) -> list[str]:
    fixture = pathlib.Path(job["fixture"])
    return [sys.executable, "-B", str(TOOLS / "bridge_launcher_via_full.py"),
            str(TOOLS / "world-via-full" / "preflight_world_full.py"),
            "--label", job["label"], "--fleetpy-root", str(FLEETPY), "--runner-root", str(RUNNER),
            "--dotnet", str(DOTNET), "--commitment-config", job["config"], "--wp4-config", job["wp4"],
            "--scenario", str(fixture / "scenario-content.json"),
            "--derivative-manifest", str(fixture / "derivative-manifest.json"),
            "--normalization-report", str(fixture / "normalization-report.json"),
            "--selection-frame", str(fixture / "selection-frame.json"),
            "--driver", job["driver"], "--output", str(output), "--repeats", "1", "--master-seed", "7",
            "--expected-repository-inventory-sha256", inventory_sha]


def finalize(job: dict, output: pathlib.Path, logs: pathlib.Path) -> str:
    if (output / "evidence.json.gz").exists():
        return "evidence-present"
    policy = job.get("transcript", "keep")
    if policy not in ("keep", "drop"):
        return f"finalize-bad-policy-{policy}"
    started = time.time()
    done = subprocess.run([sys.executable, "-I", "-B", str(FINALIZE), str(output), job["role"], "--transcript", policy],
                          stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8",
                          errors="replace")
    path = logs / f"{job['name']}.finalize.{dt.datetime.now():%Y%m%dT%H%M%S}.log"
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(f"returncode={done.returncode} seconds={time.time() - started:.0f}\n{done.stdout}")
    return f"evidence-{policy}" if done.returncode == 0 else f"finalize-failed-rc{done.returncode}"


def run(job: dict, args, inventory_sha: str):
    output = pathlib.Path(args.out) / job["name"]
    logs = pathlib.Path(args.logs)
    if not job["name"].startswith(job["role"] + "-d"):
        return job["name"], "bad-job-role"
    with _admission:
        if passed(output):
            return job["name"], "done-before+" + finalize(job, output, logs)
        if pathlib.Path(args.stop).exists():
            return job["name"], "deferred-stop"
        if output.exists():
            k = 1
            while output.with_name(f"{output.name}.interrupted-{k}").exists():
                k += 1
            output.rename(output.with_name(f"{output.name}.interrupted-{k}"))
            log(f"kept an interrupted output as {output.name}.interrupted-{k}")
        free = shutil.disk_usage(args.out).free / 2**30
        if free < args.disk_floor_gib:
            return job["name"], f"deferred-disk-{free:.1f}GiB"
        deadline = time.time() + 1800
        while psutil.virtual_memory().available < args.ram_floor_gib * 2**30:
            if time.time() > deadline or pathlib.Path(args.stop).exists():
                return job["name"], "deferred-low-ram"
            time.sleep(30)
        env = dict(os.environ, **JOB_ENV, RB_WORLD_CONFIG=job["world"])
        log(f"start {job['name']}")
        started = time.time()
        process = subprocess.Popen(command(job, output, inventory_sha), cwd=str(REPO), env=env,
                                   stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8",
                                   errors="replace")
        time.sleep(5)
    text, _ = process.communicate()
    seconds = time.time() - started
    path = logs / f"{job['name']}.{dt.datetime.now():%Y%m%dT%H%M%S}.log"
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(f"returncode={process.returncode} seconds={seconds:.0f}\n{text}")
    if not passed(output):
        log(f"end   {job['name']} {seconds / 60:.1f} min -> failed-rc{process.returncode}")
        return job["name"], f"failed-rc{process.returncode}"
    state = "status-pass+" + finalize(job, output, logs)
    log(f"end   {job['name']} {seconds / 60:.1f} min -> {state}")
    return job["name"], state


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--jobs", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--logs", required=True)
    parser.add_argument("--stop", required=True)
    parser.add_argument("--workers", type=int, default=1)
    parser.add_argument("--disk-floor-gib", type=float, default=20.0)
    parser.add_argument("--ram-floor-gib", type=float, default=7.0)
    args = parser.parse_args()
    sys.path.insert(0, str(TOOLS))
    import inventory

    if os.name == "nt":  # keep the host awake
        ctypes.windll.kernel32.SetThreadExecutionState(0x80000000 | 0x00000001)
    pathlib.Path(args.out).mkdir(parents=True, exist_ok=True)
    pathlib.Path(args.logs).mkdir(parents=True, exist_ok=True)
    jobs = json.loads(pathlib.Path(args.jobs).read_text(encoding="utf-8"))
    inventory_sha = inventory.repository_inventory_sha256(REPO.resolve())
    log(f"{len(jobs)} jobs; HEAD {inventory.git_head_commit(REPO)}; repository inventory {inventory_sha}")
    results = {}
    progress = pathlib.Path(args.out, f"PROGRESS-{pathlib.Path(args.jobs).stem}.json")
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for name, state in pool.map(lambda job: run(job, args, inventory_sha), jobs):
            results[name] = state
            progress.write_text(json.dumps(results, indent=2), encoding="utf-8")
    log("finished: " + json.dumps(results))
    ok = all(state.startswith(("status-pass+evidence", "done-before+evidence")) for state in results.values())
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
