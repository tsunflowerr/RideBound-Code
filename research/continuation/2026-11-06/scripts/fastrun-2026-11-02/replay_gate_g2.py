"""GĐ2 gate (ADR-089): every recorded bridge job (the 228 of the G1 gate) replayed through the light-audit Runner
(`auditHash: light-v1`, solver evidence kept) must give every Runner frame equal to the recorded one once members
named *Hash are removed (replay_light.py), including the final checkpoint.

Usage: python -B replay_gate_g2.py [--families ins,g2,p1,r128] [--workers 2] [--runner DIR] [--out DIR]
"""
from __future__ import annotations

import argparse
import concurrent.futures
import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import replay_gate_g1 as g1  # noqa: E402  (same job list and configs)

PYTHON = g1.PYTHON


def replay(runner: str, out_dir: pathlib.Path, family: str, folder: pathlib.Path, policy: str, wp4: str) -> str:
    clash = family == "ins" and folder.parent.name == "main" and (g1.BASE / "smoke" / folder.name).exists()
    out = out_dir / (f"{family}-main-{folder.name}" if clash else f"{family}-{folder.name}")
    if (out / "result.json").exists():
        return f"{family}/{folder.name}: done before"
    command = [PYTHON, "-I", "-B", str(HERE / "replay_light.py"), "--runner", runner, "--policy-config", policy,
               "--wp4-config", wp4, "--transcript", str(folder / "transcript-00.ndjson"), "--out", str(out)]
    done = subprocess.run(command, capture_output=True, text=True)
    return f"{family}/{folder.name}: rc={done.returncode} {done.stdout.strip()[:400]}"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--families", default="ins,g2,p1,r128")
    parser.add_argument("--workers", type=int, default=2)
    parser.add_argument("--runner", default=r"E:/RideBoundData/research/runner-fastrun-g2")
    parser.add_argument("--out", default=str(HERE / "replay-g2"))
    args = parser.parse_args()
    out_dir = pathlib.Path(args.out)
    out_dir.mkdir(exist_ok=True)
    work = list(g1.jobs(set(args.families.split(","))))
    print(f"{len(work)} jobs to replay (light audit) through {args.runner}", flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for line in pool.map(lambda job: replay(args.runner, out_dir, *job), work):
            print(line, flush=True)
    rows = [json.loads(path.read_text(encoding="utf-8")) for path in sorted(out_dir.glob("*/result.json"))]
    summary = {"runner": args.runner, "jobs": len(rows), "equal": sum(bool(r.get("equal")) for r in rows),
               "frames": sum(r.get("framesCompared", 0) for r in rows),
               "framesEqualWithoutHashes": sum(r.get("framesEqualWithoutHashes", 0) for r in rows),
               "decisions": sum(r.get("decisions", 0) for r in rows),
               "runnerCpuSeconds": round(sum(r.get("runnerCpuSeconds") or 0 for r in rows), 1)}
    (out_dir / "SUMMARY.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"G2 gate: {summary['equal']}/{summary['jobs']} jobs equal without hash fields, "
          f"{summary['framesEqualWithoutHashes']}/{summary['frames']} frames", flush=True)
    return 0 if summary["equal"] == summary["jobs"] == len(work) else 1


if __name__ == "__main__":
    raise SystemExit(main())
