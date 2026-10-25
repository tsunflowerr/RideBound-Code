"""Dwell-off replay gate: runner-p1-dwell-v1 (the C# stopServiceDurationMs option, absent) must reproduce every
Runner->adapter frame of every recorded bridge job byte for byte (via-bridge-2026-10-22/replay_runner.py), each job with
its own commitment config and WP4 config:
  ins     regress 1, smoke 2, main 55, elmi 22 (as a0_gate.py; WP4 tier-3 cost-first)
  g2      g2v4/g2all 33 (bridge G2 configs, wp4-g2-costfirst)
  p1      p1pilot + p1main, the 76 completed jobs (configs and WP4 as run_p1.py)
  r128    r128v2pilot + r128v2main, the 40 jobs (configs and WP4 as run_r128.py)
Results: replay-dwelloff/<family>-<job>.json (existing results are skipped, so it can be resumed); summary
DWELLOFF-GATE-SUMMARY.json. Two replays run at a time.
Usage: python -B replay_gate_dwell.py [--families ins,g2,p1,r128] [--workers 2]
"""
from __future__ import annotations

import argparse
import concurrent.futures
import importlib.util
import json
import os
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
BRIDGE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
PYTHON = r"E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe"
RUNNER = r"C:/RideBoundData/research/runner-p1-dwell-v1"
BASE = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1")
OUT = HERE / "replay-dwelloff"


def load(name: str, path: pathlib.Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def passed(folder: pathlib.Path) -> bool:
    summary = folder / "summary.json"
    return summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass"


def jobs(families: set[str]):
    """(family, folder, policy config, wp4 config) for every completed recorded job."""
    sys.path.insert(0, str(HERE))
    a0 = load("a0_gate", BRIDGE / "a0_gate.py")
    p1 = load("run_p1_for_gate", HERE / "run_p1.py")
    r128 = load("run_r128_for_gate", HERE / "run_r128.py")
    if "ins" in families:
        for phase in ("regress", "smoke", "main", "elmi"):
            for folder in sorted((BASE / phase).iterdir()):
                if passed(folder):
                    yield "ins", folder, a0.config_for(phase, folder.name), a0.WP4
    if "g2" in families:
        for folder in sorted((BASE / "g2v4" / "g2all").iterdir()):
            if passed(folder):
                arm = folder.name.split("-d2018")[0]
                yield "g2", folder, str(BRIDGE / "configs" / f"{arm}.json"), str(BRIDGE / "configs" / "wp4-g2-costfirst.json")
    if "p1" in families:
        for phase in ("p1pilot", "p1main"):
            for folder in sorted((BASE / "p1" / phase).iterdir()):
                if passed(folder):
                    arm = folder.name.split("-d2018")[0]
                    yield "p1", folder, str(p1.config_for(arm)), str(p1.wp4_for(arm))
    if "r128" in families:
        for phase in ("r128v2pilot", "r128v2main"):
            for folder in sorted((BASE / "p1" / phase).iterdir()):
                if passed(folder):
                    arm = folder.name.split("-d2018")[0]
                    yield "r128", folder, str(r128.config_for(arm)), str(r128.wp4_for(arm))


def replay(family: str, folder: pathlib.Path, policy: str, wp4: str) -> str:
    # v2 (PHASE1-LOG P9): smoke and main both hold PS-300 and DLHZ-120 on d20181114-s10-r1-w08. v1 named results by
    # folder only, so the two main jobs were skipped as already done. The 78 v1 names stay; such a main job gets
    # its phase in the name.
    clash = family == "ins" and folder.parent.name == "main" and (BASE / "smoke" / folder.name).exists()
    out = OUT / (f"{family}-main-{folder.name}.json" if clash else f"{family}-{folder.name}.json")
    if out.exists():
        return f"{family}/{folder.name}: done before"
    command = [PYTHON, "-I", "-B", str(BRIDGE / "replay_runner.py"), "--runner", RUNNER, "--policy-config", policy,
               "--wp4-config", wp4, "--transcript", str(folder / "transcript-00.ndjson"), "--out", str(out)]
    result = subprocess.run(command, capture_output=True, text=True, env=dict(os.environ, PYTHONIOENCODING="utf-8"))
    return f"{family}/{folder.name}: rc={result.returncode} {result.stdout.strip()[:160]}"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--families", default="ins,g2,p1,r128")
    parser.add_argument("--workers", type=int, default=2)
    args = parser.parse_args()
    OUT.mkdir(exist_ok=True)
    work = list(jobs(set(args.families.split(","))))
    print(f"{len(work)} jobs to replay through {RUNNER}", flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for line in pool.map(lambda job: replay(*job), work):
            print(line, flush=True)
    rows = []
    for path in sorted(OUT.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        rows.append({"file": path.name, "identical": data["identical"], "frames": data["framesCompared"],
                     "framesIdentical": data["framesIdentical"], "firstDifference": data["firstDifference"]})
    summary = {"runner": RUNNER, "replayed": len(rows), "identical": sum(r["identical"] for r in rows),
               "framesTotal": sum(r["frames"] for r in rows), "framesIdentical": sum(r["framesIdentical"] for r in rows),
               "byFamily": {f: {"jobs": sum(r["file"].startswith(f + "-") for r in rows),
                                "identical": sum(r["file"].startswith(f + "-") and r["identical"] for r in rows)}
                            for f in ("ins", "g2", "p1", "r128")},
               "notIdentical": [r for r in rows if not r["identical"]], "rows": rows}
    (HERE / "DWELLOFF-GATE-SUMMARY.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"dwell-off gate: {summary['identical']}/{summary['replayed']} jobs byte-identical, "
          f"{summary['framesIdentical']}/{summary['framesTotal']} frames; {summary['byFamily']}")
    return 0 if not summary["notIdentical"] else 1


if __name__ == "__main__":
    sys.exit(main())
