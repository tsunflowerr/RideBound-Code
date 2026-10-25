"""Driver for bridge step 1.4: the 10 bridge cells at 128 requests and 10 vehicles (normalizer contract v1 bound).

Reuses run_p1.py (same rails, same Runner runner-g2-v3, same G2 worktree adapter and inventory, and the same instance
lock run-p1-instance.lock, so it can never run at the same time as a run_p1.py phase). Only the jobs differ:
  R128-U, R128-RCE35            bridge configs U, RCE35 with the bridge WP4 (cost-first, no-worse recovery);
  R128-G2K8-U, R128-G2K8-RCE35  bridge configs G2-U, G2-RCE35 with WP4 G2 cost-first and maximumTargetVehicles 8.
Fixtures C:/RideBoundData/research/fixtures-via-bridge-r128-v1; drivers drivers-r128/ (prepare_r128.py); bridge worlds.
v2 (PHASE1-LOG P4): drivers-r128-v2 (declared cardinality 128/10); the v1 pilot failed in the preflight on the
108/8 declaration before any simulation (logs/r128pilot kept). Phases:
  r128v2pilot  4 jobs on d20181114-s10-r1-w08. Needs R128-READY-v2.txt.
  r128v2main   36 jobs, the other 9 cells. Needs R128-PILOT-REVIEWED-v2.txt.
Usage: python -B run_r128.py check|r128v2pilot|r128v2main [--workers N]
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

FIXTURES = pathlib.Path(r"C:/RideBoundData/research/fixtures-via-bridge-r128-v1")
WP4_INS = base.TANG3 / "stage1b" / "harness" / "wp4-tier3-v2-costfirst.json"
WP4_INS_SHA256 = "37040657b8f6c16c8eb8430a208bfd4b155791668af0f1da49110f0f372300ae"   # run_g2.py:84
ARMS = ("R128-U", "R128-RCE35", "R128-G2K8-U", "R128-G2K8-RCE35")
CELLS = tuple(sorted(p.name for p in FIXTURES.iterdir())) if FIXTURES.exists() else ()


def jobs(phase: str):
    if phase == "r128v2pilot":
        return [(f"{arm}-{base.PILOT_CELL}", arm, base.PILOT_CELL) for arm in ARMS]
    return [(f"{arm}-{cell}", arm, cell) for cell in CELLS if cell != base.PILOT_CELL for arm in ARMS]


def config_for(arm: str) -> pathlib.Path:
    name = {"R128-U": "U", "R128-RCE35": "RCE35", "R128-G2K8-U": "G2-U", "R128-G2K8-RCE35": "G2-RCE35"}[arm]
    return base.BRIDGE / "configs" / f"{name}.json"


def wp4_for(arm: str) -> pathlib.Path:
    return HERE / "configs" / "wp4-g2-costfirst-k8.json" if "G2K8" in arm else WP4_INS


def inputs(arm: str, cell: str) -> dict:
    return {
        "label": f"{arm}-{cell}-vbr",
        "config": config_for(arm),
        "fixture": FIXTURES / cell,
        "driver": HERE / "drivers-r128-v2" / f"{cell}.driver.json",
        "world": base.BRIDGE / "worlds" / f"W5-{cell[:9]}-{cell[-3:]}.json",
    }


def check_inputs(phase: str) -> list[str]:
    problems: list[str] = []
    for path in (base.PYTHON, base.FLEETPY, base.DOTNET, base.WRAPPER, base.LAUNCHER, base.RUNNER, WP4_INS):
        if not path.exists():
            problems.append(f"missing {path}")
    if WP4_INS.exists() and base.sha256(WP4_INS) != WP4_INS_SHA256:
        problems.append("WP4 cost-first config SHA-256 mismatch")
    verify = subprocess.run([str(base.PYTHON), "-B", str(base.TANG3 / "runner_manifest.py"), "verify", str(base.RUNNER),
                             str(base.RUNNER_MANIFEST)], capture_output=True, text=True)
    if verify.returncode != 0 or not verify.stdout.startswith("OK"):
        problems.append(f"runner manifest: {verify.stdout.strip()} {verify.stderr.strip()}")
    for manifest, root in ((base.BRIDGE / "configs-worlds.sha256.txt", base.BRIDGE),
                           (base.BRIDGE / "configs-g2.sha256.txt", base.BRIDGE / "configs"),
                           (HERE / "configs-r128.sha256.txt", HERE / "configs"),
                           (base.G2_KIT / "world-g2.sha256.txt", base.G2_KIT / "world"),
                           (HERE / "drivers-r128-v2.sha256.txt", HERE / "drivers-r128-v2"),
                           (HERE / "fixtures-via-bridge-r128-v1.sha256.txt", FIXTURES)):
        for line in manifest.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            digest, rel = line.split(None, 1)
            path = root / rel.lstrip("*").removeprefix("./")
            if not path.exists() or base.sha256(path) != digest:
                problems.append(f"input changed or missing: {path}")
    actual = base.inventory.repository_inventory_sha256(base.ADAPTER_REPO.resolve())
    if actual != base.EXPECTED_INVENTORY:
        problems.append(f"adapter repository inventory {actual} != expected {base.EXPECTED_INVENTORY}")
    if len(CELLS) != 10:
        problems.append(f"expected 10 fixture cells, found {len(CELLS)}")
    for name, arm, cell in jobs(phase):
        spec = inputs(arm, cell)
        for key in ("config", "driver", "world"):
            if not spec[key].exists():
                problems.append(f"{name}: missing {key} {spec[key]}")
        if not wp4_for(arm).exists():
            problems.append(f"{name}: missing wp4 {wp4_for(arm)}")
    if phase == "r128v2pilot" and not (HERE / "R128-READY-v2.txt").exists():
        problems.append("r128v2pilot refused: R128-READY-v2.txt does not exist")
    if phase == "r128v2main" and not (HERE / "R128-PILOT-REVIEWED-v2.txt").exists():
        problems.append("r128v2main refused: R128-PILOT-REVIEWED-v2.txt does not exist")
    return problems


# run_p1's job machinery reads these module globals at call time.
base.jobs, base.config_for, base.wp4_for, base.inputs = jobs, config_for, wp4_for, inputs
base.PROGRESS = HERE / "PROGRESS-R128.txt"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "r128v2pilot", "r128v2main"))
    parser.add_argument("--workers", type=int, default=3)
    args = parser.parse_args()
    phase = args.phase
    if phase == "check":
        for p in ("r128v2pilot", "r128v2main"):
            problems = check_inputs(p)
            base.log(f"check {p}: {'OK' if not problems else problems}")
        return 0
    if not base.acquire_lock():
        base.log("refused: a run_p1.py or run_r128.py instance is running")
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
