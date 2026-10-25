"""Owner condition 3 for the donor-side fix (PHASE1-LOG P5/P6): rerun every G2 job with the fixed adapter.

Every G2 job run so far is rerun with exactly its original inputs (commitment config, WP4, fixture, driver, world,
label, seed 7, Runner runner-g2-v3); only the adapter differs: C:/Code/RideBound-p1fix (fleet_control.py +62 lines),
through bridge_launcher_fix1.py and world-fix1/ (each one line different from the G2 copies).
Phases (outputs under C:/RideBoundData/research/via-bridge-man-v1/fix1/<phase>):
  fix1bridge  33 jobs = g2v4/g2all            (G2-U, G2-RCE35, G2-EOHZ35 x 11 bridge cells)
  fix1p1      55 jobs = p1pilot + p1main G2   (G2-EO35, G2-DLS-120, G2K3-U, G2K3-RCE35, G2K3-EOHZ35 x 11)
  fix1r128    20 jobs = r128v2pilot + main G2 (R128-G2K8-U, R128-G2K8-RCE35 x 10 cells of 128 requests)
Each phase needs FIX1-READY.txt. Same rails and the same instance lock as run_p1.py.
Usage: python -B run_fix1.py check|fix1bridge|fix1p1|fix1r128 [--workers N]
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

BASE = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1")
FIX_REPO = pathlib.Path(r"C:/Code/RideBound-p1fix")
R128_FIXTURES = pathlib.Path(r"C:/RideBoundData/research/fixtures-via-bridge-r128-v1")
WP4_K8 = HERE / "configs" / "wp4-g2-costfirst-k8.json"
BRIDGE_G2 = ("G2-U", "G2-RCE35", "G2-EOHZ35")
P1_G2 = ("G2-EO35", "G2-DLS-120", "G2K3-U", "G2K3-RCE35", "G2K3-EOHZ35")
R128_G2 = ("R128-G2K8-U", "R128-G2K8-RCE35")
R128_CONFIG = {"R128-G2K8-U": "G2-U", "R128-G2K8-RCE35": "G2-RCE35"}


def _specs() -> dict[str, dict]:
    """job name -> inputs, phase and the earlier run it repeats."""
    specs: dict[str, dict] = {}
    for cell in base.CELLS:
        world = base.BRIDGE / "worlds" / f"W5-{cell[:9]}-{cell[-3:]}.json"
        driver = base.BRIDGE / "drivers" / f"{cell}.driver.json"
        fixture = base.FIXTURES / cell
        for arm in BRIDGE_G2:
            specs[f"{arm}-{cell}"] = dict(phase="fix1bridge", config=base.BRIDGE / "configs" / f"{arm}.json",
                                          wp4=base.BRIDGE / "configs" / "wp4-g2-costfirst.json", fixture=fixture,
                                          driver=driver, world=world, previous=BASE / "g2v4" / "g2all" / f"{arm}-{cell}")
        for arm in P1_G2:
            source = "p1pilot" if cell == base.PILOT_CELL else "p1main"
            specs[f"{arm}-{cell}"] = dict(phase="fix1p1", config=base.config_for(arm), wp4=base.wp4_for(arm),
                                          fixture=fixture, driver=driver, world=world,
                                          previous=BASE / "p1" / source / f"{arm}-{cell}")
    for cell in sorted(p.name for p in R128_FIXTURES.iterdir()):
        source = "r128v2pilot" if cell == base.PILOT_CELL else "r128v2main"
        for arm in R128_G2:
            specs[f"{arm}-{cell}"] = dict(
                phase="fix1r128", config=base.BRIDGE / "configs" / f"{R128_CONFIG[arm]}.json", wp4=WP4_K8,
                fixture=R128_FIXTURES / cell, driver=HERE / "drivers-r128-v2" / f"{cell}.driver.json",
                world=base.BRIDGE / "worlds" / f"W5-{cell[:9]}-{cell[-3:]}.json",
                previous=BASE / "p1" / source / f"{arm}-{cell}")
    return specs


SPECS = _specs()


def jobs(phase: str):
    return [(name, name, "") for name, spec in SPECS.items() if spec["phase"] == phase]


def inputs(arm: str, _cell: str) -> dict:
    spec = SPECS[arm]
    return {"label": f"{arm}-vbr", **{k: spec[k] for k in ("config", "fixture", "driver", "world")}}


def wp4_for(arm: str) -> pathlib.Path:
    return SPECS[arm]["wp4"]


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
                           (base.BRIDGE / "configs-g2.sha256.txt", base.BRIDGE / "configs"),
                           (HERE / "configs-p1.sha256.txt", HERE / "configs"),
                           (HERE / "configs-r128.sha256.txt", HERE / "configs"),
                           (HERE / "world-fix1.sha256.txt", HERE / "world-fix1"),
                           (base.BRIDGE / "drivers.sha256.txt", base.BRIDGE / "drivers"),
                           (HERE / "drivers-r128-v2.sha256.txt", HERE / "drivers-r128-v2"),
                           (base.BRIDGE / "fixtures-via-bridge-2000-v1.sha256.txt", base.FIXTURES),
                           (HERE / "fixtures-via-bridge-r128-v1.sha256.txt", R128_FIXTURES)):
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            digest, rel = line.split(None, 1)
            path = root / rel.lstrip("*").removeprefix("./")
            if not path.exists() or base.sha256(path) != digest:
                problems.append(f"input changed or missing: {path}")
    actual = base.inventory.repository_inventory_sha256(FIX_REPO.resolve())
    if actual != base.EXPECTED_INVENTORY:
        problems.append(f"fix repository inventory {actual} != expected {base.EXPECTED_INVENTORY}")
    for name, _, _ in jobs(phase):
        spec = SPECS[name]
        for key in ("config", "wp4", "driver", "world"):
            if not spec[key].exists():
                problems.append(f"{name}: missing {key} {spec[key]}")
        if not (spec["fixture"] / "scenario-content.json").exists():
            problems.append(f"{name}: missing fixture {spec['fixture']}")
        if not spec["previous"].exists():
            problems.append(f"{name}: missing earlier run {spec['previous']}")
    if not (HERE / "FIX1-READY.txt").exists():
        problems.append(f"{phase} refused: FIX1-READY.txt does not exist")
    return problems


# run_p1's job machinery reads these module globals at call time.
base.LAUNCHER = HERE / "bridge_launcher_fix1.py"
base.WRAPPER = HERE / "world-fix1" / "preflight_world_v1_2.py"
base.ADAPTER_REPO = FIX_REPO
base.EXPECTED_INVENTORY = (HERE / "FIX1-INVENTORY.txt").read_text(encoding="utf-8").split()[0]
# PHASE1-LOG P8: C: reached its 30 GiB floor after the first 6 fix1bridge jobs; every later output goes to E:. A job
# already finished on C: (FIX_C) is not run again.
FIX_C = BASE / "fix1"
base.OUT_ROOT = pathlib.Path(r"E:/RideBoundData/research/via-bridge-man-v1/fix1")
_state_on_e = base.state_of


def state_of(phase: str, name: str) -> str:
    if (FIX_C / phase / name).exists():
        summary = FIX_C / phase / name / "summary.json"
        return "done-on-C:" + ("status-pass" if summary.exists() else "no-summary")
    return _state_on_e(phase, name)


base.state_of = state_of
base.LOG_ROOT = HERE / "logs-fix1"
base.PROGRESS = HERE / "PROGRESS-FIX1.txt"
base.jobs, base.inputs, base.wp4_for = jobs, inputs, wp4_for


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "fix1bridge", "fix1p1", "fix1r128"))
    parser.add_argument("--workers", type=int, default=3)
    args = parser.parse_args()
    phase = args.phase
    if phase == "check":
        for p in ("fix1bridge", "fix1p1", "fix1r128"):
            problems = check_inputs(p)
            base.log(f"check {p} ({len(jobs(p))} jobs): {'OK' if not problems else problems}")
        return 0
    if not base.acquire_lock():
        base.log("refused: a run_p1/run_r128/run_fix1 instance is running")
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
