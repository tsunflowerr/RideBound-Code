"""A0 gate: cross-replay every finished bridge job (regress, smoke, main) through the A0 Runner.

Pass rule (bundle docs/wf9/review/REVIEW.md B1 fix): every Runner->adapter frame of the recorded job is reproduced
BYTE FOR BYTE by runner-via-a0-v1 (7435661 + rule patches + incremental-hash patches), state hashes and checkpoint
included. Jobs already replayed (replay/a0-<phase>-<job>.json exists) are skipped, so the script can be re-run as
more main jobs finish. Writes A0-GATE-SUMMARY.json (mode "w": a running summary, regenerated from the result files).

Usage: python -B a0_gate.py [--limit N]
"""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
PYTHON = r"E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe"
OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1")
A0_RUNNER = r"C:/RideBoundData/research/runner-via-a0-v1"
WP4 = r"E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/wp4-tier3-v2-costfirst.json"
REGRESS_CONFIG = r"E:/Code/Report_INT3508/ridebound-scratchpad/tang3/configs/U.json"
ARMS = ("DLHZ-120", "PS-300", "C-120", "C-300", "U", "RCE35", "EOHZ35")  # elmi arms added 2026-10-23 (A3)


def config_for(phase: str, job: str) -> str:
    if phase == "regress":
        return REGRESS_CONFIG
    arm = next(a for a in ARMS if job.startswith(a + "-d"))
    return str(HERE / "configs" / f"{arm}.json")


def finished():
    for phase in ("regress", "smoke", "main", "elmi"):
        root = OUT_ROOT / phase
        if not root.exists():
            continue
        for folder in sorted(root.iterdir()):
            summary = folder / "summary.json"
            if summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass":
                yield phase, folder


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--runner", default=A0_RUNNER, help="Runner build to replay through")
    parser.add_argument("--tag", default="a0", help="result file prefix and summary name")
    parser.add_argument("--phases", default="regress,smoke,main")
    args = parser.parse_args()
    tag = args.tag
    phases = tuple(args.phases.split(","))
    done = 0
    for phase, folder in finished():
        if phase not in phases:
            continue
        out = HERE / "replay" / f"{tag}-{phase}-{folder.name}.json"
        if out.exists():
            continue
        if args.limit and done >= args.limit:
            break
        command = [PYTHON, "-I", "-B", str(HERE / "replay_runner.py"), "--runner", args.runner,
                   "--policy-config", config_for(phase, folder.name), "--wp4-config", WP4,
                   "--transcript", str(folder / "transcript-00.ndjson"), "--out", str(out)]
        result = subprocess.run(command, capture_output=True, text=True, env=dict(os.environ, PYTHONIOENCODING="utf-8"))
        print(f"{phase}/{folder.name}: rc={result.returncode} {result.stdout.strip()[:200]}", flush=True)
        done += 1

    rows = []
    for path in sorted((HERE / "replay").glob(f"{tag}-*.json")):
        if path.name.startswith((f"{tag}-on-", f"{tag}-selfcheck-")) \
                or not path.name.startswith(tuple(f"{tag}-{p}-" for p in ("regress", "smoke", "main", "elmi"))):
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        rows.append({"file": path.name, "identical": data["identical"], "frames": data["framesCompared"],
                     "framesIdentical": data["framesIdentical"], "runnerCpuSeconds": data["runnerCpuSeconds"],
                     "firstDifference": data["firstDifference"]})
    summary = {"replayed": len(rows), "identical": sum(r["identical"] for r in rows),
               "framesTotal": sum(r["frames"] for r in rows), "framesIdentical": sum(r["framesIdentical"] for r in rows),
               "notIdentical": [r for r in rows if not r["identical"]], "rows": rows}
    summary["runner"] = args.runner
    name = "A0-GATE-SUMMARY.json" if tag == "a0" else f"{tag.upper()}-GATE-SUMMARY.json"
    (HERE / name).write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(f"{tag} gate so far: {summary['identical']}/{summary['replayed']} jobs byte-identical, "
          f"{summary['framesIdentical']}/{summary['framesTotal']} frames")
    return 0 if not summary["notIdentical"] else 1


if __name__ == "__main__":
    sys.exit(main())
