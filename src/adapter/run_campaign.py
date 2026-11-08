"""Run the F14 or F16 campaign: one run_job.py job per cell and arm, a few jobs at a time.

  python -B run_campaign.py --panel f14 --data <panel data> --out <runs> --logs <logs>
      --fleetpy-root <FleetPy 1.0.2> --runner-root <published Runner> --dotnet <dotnet>
      [--cells all|gate] [--arms A,B,...] [--workers N] [--dry-run]

The panel data folder holds drivers-run/<cell>.driver.json, fixtures/<cell>/, worlds/<cell>.world.json and
job-configs-v2/{commitment,wp4}/. Each job writes into <out>/<job name> and its log into <logs>. A rerun skips every
job whose summary.json says "pass"; a folder left without one is kept as <name>.interrupted-<k> and the job runs again.
--dry-run prints each job (fields, command and environment) as one JSON line and runs nothing.

Lists: all cells with the main arms (default) gives the 168 F14 or 192 F16 final jobs. Gate lists: --cells gate (main
arms), and for F14 --cells gate --arms FS-G2-SHHZ71-CL,FS-G2-SHRC71-N (attribution) or --arms FS-G2-RCE35-CL (forecast).
Gate jobs run into the same --out as the final list are skipped there, so they never run twice.
Workers: one job needs up to about 6.2 GB of RAM, so use --workers = min(CPU cores, (RAM in GB - 4) // 7).
"""
import argparse
import concurrent.futures
import datetime as dt
import json
import os
import pathlib
import subprocess
import sys
import time

JOB_SCRIPT = pathlib.Path(__file__).with_name("run_job.py")
# Environment of every job. The Runner inherits it from run_job.py, so its heap is capped at 6 GiB.
JOB_ENV = {"PYTHONDONTWRITEBYTECODE": "1", "PYTHONIOENCODING": "utf-8", "DOTNET_GCHeapHardLimit": "0x180000000"}
MASTER_SEED = "7"  # every final job used master seed 7
# panel: (number of cells, gate cells, label suffix). The label goes into the run id "wp7-medium-<label>" of the
# frames: these are legacy names, kept so that new frames stay byte-identical to the recorded runs.
PANELS = {"f14": (84, ("d20190919-vf-r0-w08", "d20190919-vf-r0-w17"), "fin2"),
          "f16": (96, ("d20190920-vf-r0-w08", "d20190920-vf-r0-w17"), "f16")}
# arm: (commitment config, WP4 config folder). SHRC71 = rule C, SHHZ71 = relaxed fence; the CL folder adds the
# forecast to the Shadow inputs, the N folder has Shadow only. RCE35-CL is the forecast gate arm.
ARMS = {"FS-G2-SHRC71-CL": ("SHRC71", "FS-G2-SHRC71-CL"),
        "FS-G2-SHHZ71-N": ("SHHZ71", "FS-G2-SHHZ71-N"),
        "FS-G2-SHHZ71-CL": ("SHHZ71", "FS-G2-SHRC71-CL"),
        "FS-G2-SHRC71-N": ("SHRC71", "FS-G2-SHHZ71-N"),
        "FS-G2-RCE35-CL": ("RCE35", "FS-G2-RCE35-CL")}
MAIN_ARMS = ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N")
FIXTURE_FILES = ("scenario-content", "derivative-manifest", "normalization-report", "selection-frame")


def log(line):
    print(f"{dt.datetime.now().isoformat(timespec='seconds')} {line}", flush=True)


def make_jobs(panel, data, cells="all", arms=MAIN_ARMS):
    """The job list: every cell (in name order) with every arm. Only the gate cells keep their transcripts."""
    count, gate, suffix = PANELS[panel]
    found = sorted(path.name[:-len(".driver.json")] for path in (data / "drivers-run").glob("*.driver.json"))
    if len(found) != count or not set(gate) <= set(found):
        raise SystemExit(f"{data}: expected {count} cells including {' and '.join(gate)}, found {len(found)}")
    jobs = []
    for cell in (gate if cells == "gate" else found):
        for arm in arms:
            commitment, wp4 = ARMS[arm]
            job = {"name": f"{arm}-{cell}", "label": f"{arm}-{cell}-{suffix}", "role": arm,
                   "config": (data / "job-configs-v2/commitment" / f"{commitment}.json").as_posix(),
                   "wp4": (data / "job-configs-v2/wp4" / wp4 / f"{cell}.json").as_posix(),
                   "fixture": (data / "fixtures" / cell).as_posix(),
                   "driver": (data / "drivers-run" / f"{cell}.driver.json").as_posix(),
                   "world": (data / "worlds" / f"{cell}.world.json").as_posix(),
                   "transcript": "keep" if cell in gate else "drop"}
            inputs = [job["config"], job["wp4"], job["driver"], job["world"]]
            for path in inputs + [f"{job['fixture']}/{name}.json" for name in FIXTURE_FILES]:
                if not os.path.isfile(path):
                    raise SystemExit(f"missing {path}")
            jobs.append(job)
    return jobs


def command(job, args):
    """The run_job.py command line of one job."""
    fixture = job["fixture"]
    return [sys.executable, "-B", str(JOB_SCRIPT), "--label", job["label"], "--role", job["role"],
            "--fleetpy-root", args.fleetpy_root, "--runner-root", args.runner_root, "--dotnet", args.dotnet,
            "--commitment-config", job["config"], "--wp4-config", job["wp4"],
            "--scenario", f"{fixture}/scenario-content.json",
            "--derivative-manifest", f"{fixture}/derivative-manifest.json",
            "--normalization-report", f"{fixture}/normalization-report.json",
            "--selection-frame", f"{fixture}/selection-frame.json",
            "--driver", job["driver"], "--world", job["world"], "--master-seed", MASTER_SEED,
            "--transcript", job["transcript"], "--output", str(pathlib.Path(args.out) / job["name"])]


def passed(folder):
    """True if the folder holds a summary.json whose status is "pass"."""
    try:
        return json.loads((folder / "summary.json").read_text(encoding="utf-8")).get("status") == "pass"
    except (OSError, ValueError):
        return False


def run(job, args):
    """Run one job unless it passed before, and return its state."""
    output = pathlib.Path(args.out) / job["name"]
    if passed(output):
        return "done-before"
    if output.exists():  # left by an interrupted or failed run: keep it, and run again in a fresh folder
        k = 1
        while output.with_name(f"{output.name}.interrupted-{k}").exists():
            k += 1
        output.rename(output.with_name(f"{output.name}.interrupted-{k}"))
        log(f"kept an interrupted output as {output.name}.interrupted-{k}")
    log(f"start {job['name']}")
    started = time.time()
    done = subprocess.run(command(job, args), env=dict(os.environ, **JOB_ENV), stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    seconds = time.time() - started
    path = pathlib.Path(args.logs) / f"{job['name']}.{dt.datetime.now():%Y%m%dT%H%M%S}.log"
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(f"returncode={done.returncode} seconds={seconds:.0f}\n{done.stdout}")
    state = "pass" if passed(output) else f"failed-rc{done.returncode}"
    log(f"end   {job['name']} {seconds / 60:.1f} min -> {state}")
    return state


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--panel", required=True, choices=sorted(PANELS))
    parser.add_argument("--data", required=True, type=pathlib.Path, help="the panel's data folder")
    parser.add_argument("--out", required=True, help="one output folder per job is made here")
    parser.add_argument("--logs", required=True)
    parser.add_argument("--fleetpy-root", required=True)
    parser.add_argument("--runner-root", required=True, help="the folder that holds RideBound.Runner.dll")
    parser.add_argument("--dotnet", required=True)
    parser.add_argument("--cells", choices=("all", "gate"), default="all")
    parser.add_argument("--arms", default=",".join(MAIN_ARMS), help="comma-separated, from " + ", ".join(ARMS))
    parser.add_argument("--workers", type=int, default=1, help="min(CPU cores, (RAM in GB - 4) // 7)")
    parser.add_argument("--dry-run", action="store_true", help="print the jobs as JSON lines and run nothing")
    args = parser.parse_args(argv)
    arms = args.arms.split(",")
    if any(arm not in ARMS for arm in arms) or len(set(arms)) < len(arms):
        parser.error(f"--arms {args.arms}: each arm must be one of {', '.join(ARMS)}, at most once")
    jobs = make_jobs(args.panel, args.data, args.cells, arms)
    if args.dry_run:
        for job in jobs:
            print(json.dumps({**job, "command": command(job, args), "env": JOB_ENV}))
        return 0
    for folder in (args.out, args.logs):
        pathlib.Path(folder).mkdir(parents=True, exist_ok=True)
    log(f"{len(jobs)} jobs, {args.workers} workers")
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        states = list(pool.map(lambda job: run(job, args), jobs))
    failed = [job["name"] for job, state in zip(jobs, states) if state not in ("pass", "done-before")]
    log(f"finished: {len(jobs) - len(failed)} passed, {len(failed)} failed {failed}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
