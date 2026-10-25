"""Driver for bridge step 1.3: dwell 24 s at every inserted pickup and drop (owner approval PHASE1-LOG P5).

Reuses run_p1.py (same rails and instance lock) with: Runner runner-p1-dwell-v1 (C# option stopServiceDurationMs), the
adapter of C:/Code/RideBound-p1dwell (bridge_launcher_dwell.py, world-dwell/), WP4 configs with stopServiceDurationMs
24000 (insertion-only and G2), the bridge commitment configs, fixtures, drivers and worlds.
Cells (fixed before any run, as emulate_dwell.js): d20181112-w08, d20181113-w08, d20181112-w17, d20181113-w17.
Arms: D24-U, D24-RCE35, D24-EOHZ35 (insertion-only); D24-G2-U, D24-G2-RCE35, D24-G2-EOHZ35 (G2, every vehicle).
Phases:
  dwellpilot  6 jobs on d20181112-s10-r1-w08. Needs DWELL-READY.txt.
  dwellmain   18 jobs on the other 3 cells. Needs DWELL-PILOT-REVIEWED.txt.
Usage: python -B run_dwell.py check|dwellpilot|dwellmain [--workers N]
"""
from __future__ import annotations

import argparse
import concurrent.futures
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import run_p1 as base  # noqa: E402

DWELL_REPO = pathlib.Path(r"C:/Code/RideBound-p1dwell")
CELLS = ("d20181112-s10-r1-w08", "d20181113-s10-r1-w08", "d20181112-s10-r1-w17", "d20181113-s10-r1-w17")
PILOT = CELLS[0]
ARMS = ("D24-U", "D24-RCE35", "D24-EOHZ35", "D24-G2-U", "D24-G2-RCE35", "D24-G2-EOHZ35")


def jobs(phase: str):
    cells = (PILOT,) if phase == "dwellpilot" else CELLS[1:]
    return [(f"{arm}-{cell}", arm, cell) for cell in cells for arm in ARMS]


def config_for(arm: str) -> pathlib.Path:
    return base.BRIDGE / "configs" / f"{arm.removeprefix('D24-')}.json"


def wp4_for(arm: str) -> pathlib.Path:
    name = "wp4-g2-costfirst-dwell24.json" if arm.startswith("D24-G2-") else "wp4-costfirst-dwell24.json"
    return HERE / "configs" / name


def inputs(arm: str, cell: str) -> dict:
    return {
        "label": f"{arm}-{cell}-vbr",
        "config": config_for(arm),
        "fixture": base.FIXTURES / cell,
        "driver": base.BRIDGE / "drivers" / f"{cell}.driver.json",
        "world": base.BRIDGE / "worlds" / f"W5-{cell[:9]}-{cell[-3:]}.json",
    }


def check_inputs(phase: str) -> list[str]:
    problems: list[str] = []
    for path in (base.PYTHON, base.FLEETPY, base.DOTNET, base.WRAPPER, base.LAUNCHER, base.RUNNER):
        if not path.exists():
            problems.append(f"missing {path}")
    verify = subprocess.run([str(base.PYTHON), "-B", str(base.TANG3 / "runner_manifest.py"), "verify", str(base.RUNNER),
                             str(base.RUNNER_MANIFEST)], capture_output=True, text=True)
    if verify.returncode != 0 or not verify.stdout.startswith("OK"):
        problems.append(f"runner manifest: {verify.stdout.strip()} {verify.stderr.strip()}")
    for manifest, root in ((base.BRIDGE / "configs-worlds.sha256.txt", base.BRIDGE),
                           (base.BRIDGE / "configs-elmi.sha256.txt", base.BRIDGE / "configs"),
                           (base.BRIDGE / "configs-g2.sha256.txt", base.BRIDGE / "configs"),
                           (HERE / "configs-dwell.sha256.txt", HERE / "configs"),
                           (HERE / "world-dwell.sha256.txt", HERE / "world-dwell"),
                           (base.BRIDGE / "drivers.sha256.txt", base.BRIDGE / "drivers"),
                           (base.BRIDGE / "fixtures-via-bridge-2000-v1.sha256.txt", base.FIXTURES)):
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            digest, rel = line.split(None, 1)
            path = root / rel.lstrip("*").removeprefix("./")
            if not path.exists() or base.sha256(path) != digest:
                problems.append(f"input changed or missing: {path}")
    actual = base.inventory.repository_inventory_sha256(DWELL_REPO.resolve())
    if actual != base.EXPECTED_INVENTORY:
        problems.append(f"dwell repository inventory {actual} != expected {base.EXPECTED_INVENTORY}")
    for name, arm, cell in jobs(phase):
        spec = inputs(arm, cell)
        for key in ("config", "driver", "world"):
            if not spec[key].exists():
                problems.append(f"{name}: missing {key} {spec[key]}")
        if not wp4_for(arm).exists():
            problems.append(f"{name}: missing wp4 {wp4_for(arm)}")
    if phase == "dwellpilot" and not (HERE / "DWELL-READY.txt").exists():
        problems.append("dwellpilot refused: DWELL-READY.txt does not exist")
    if phase == "dwellmain" and not (HERE / "DWELL-PILOT-REVIEWED.txt").exists():
        problems.append("dwellmain refused: DWELL-PILOT-REVIEWED.txt does not exist")
    return problems


# run_p1's job machinery reads these module globals at call time.
base.RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-p1-dwell-v1")
base.RUNNER_MANIFEST = HERE / "runner-p1-dwell-v1.sha256.tsv"
base.LAUNCHER = HERE / "bridge_launcher_dwell.py"
base.WRAPPER = HERE / "world-dwell" / "preflight_world_v1_2.py"
base.ADAPTER_REPO = DWELL_REPO
base.EXPECTED_INVENTORY = (HERE / "DWELL-INVENTORY.txt").read_text(encoding="utf-8").split()[0]
base.PROGRESS = HERE / "PROGRESS-DWELL.txt"
base.OUT_ROOT = pathlib.Path(r"E:/RideBoundData/research/via-bridge-man-v1/p1")   # E:, not C: (PHASE1-LOG P8)
base.jobs, base.config_for, base.wp4_for, base.inputs = jobs, config_for, wp4_for, inputs


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "dwellpilot", "dwellmain"))
    parser.add_argument("--workers", type=int, default=3)
    args = parser.parse_args()
    phase = args.phase
    if phase == "check":
        for p in ("dwellpilot", "dwellmain"):
            problems = check_inputs(p)
            base.log(f"check {p} ({len(jobs(p))} jobs): {'OK' if not problems else problems}")
        return 0
    if not base.acquire_lock():
        base.log("refused: a run_p1/run_r128/run_fix1/run_dwell instance is running")
        return 3
    problems = check_inputs(phase)
    if problems:
        base.log("refused: " + "; ".join(problems))
        return 4
    base.keep_awake()
    (base.LOG_ROOT / phase).mkdir(parents=True, exist_ok=True)
    (base.OUT_ROOT / phase).mkdir(parents=True, exist_ok=True)
    results: dict[str, dict] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(base.run_job, phase, *job) for job in jobs(phase)]
        for future in concurrent.futures.as_completed(futures):
            name, state, info = future.result()
            results[name] = {"state": state, **(info or {})}
            base.write_progress(phase, results)
    base.write_progress(phase, results)
    base.log(f"phase {phase} finished")
    return 0


if __name__ == "__main__":
    sys.exit(main())
