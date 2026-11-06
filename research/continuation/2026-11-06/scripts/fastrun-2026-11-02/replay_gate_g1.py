"""GĐ1 replay gate (ADR-089): the fast-runner build must reproduce every Runner->adapter frame of every recorded
bridge job byte for byte, exactly as runner-p1-dwell-v1 did in PHASE1-LOG P9 (228/228 jobs, 176,074 frames).

Copy of bridge-phase1-2026-10-23/replay_gate_dwell.py v2 with three constants changed (RUNNER, OUT, the phase-1 kit
path); job selection, configs and the per-job replay (via-bridge-2026-10-22/replay_runner.py) are unchanged.
The recorded transcripts are read through C:/RideBoundData/research/via-bridge-man-v1, now a junction to E: (moved
2026-11-02, every file SHA-256 verified).
Usage: python -B replay_gate_g1.py [--families ins,g2,p1,r128] [--workers 2] [--runner DIR] [--out DIR]
"""
from __future__ import annotations

import argparse
import concurrent.futures
import importlib.util
import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
PHASE1 = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/bridge-phase1-2026-10-23")
BRIDGE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
PYTHON = r"E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe"
BASE = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1")


def load(name: str, path: pathlib.Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def passed(folder: pathlib.Path) -> bool:
    summary = folder / "summary.json"
    return summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass"


def jobs(families: set[str]):
    """(family, folder, policy config, wp4 config) for every completed recorded job (as replay_gate_dwell.py)."""
    sys.path.insert(0, str(PHASE1))
    a0 = load("a0_gate", BRIDGE / "a0_gate.py")
    p1 = load("run_p1_for_gate", PHASE1 / "run_p1.py")
    r128 = load("run_r128_for_gate", PHASE1 / "run_r128.py")
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


def replay(runner: str, out_dir: pathlib.Path, family: str, folder: pathlib.Path, policy: str, wp4: str) -> str:
    clash = family == "ins" and folder.parent.name == "main" and (BASE / "smoke" / folder.name).exists()
    out = out_dir / (f"{family}-main-{folder.name}.json" if clash else f"{family}-{folder.name}.json")
    if out.exists():
        return f"{family}/{folder.name}: done before"
    transcript = folder / "transcript-00.ndjson"
    command = [PYTHON, "-I", "-B", str(BRIDGE / "replay_runner.py"), "--runner", runner, "--policy-config", policy,
               "--wp4-config", wp4, "--transcript", str(transcript), "--out", str(out)]
    done = subprocess.run(command, capture_output=True, text=True)
    result = json.loads(out.read_text(encoding="utf-8")) if out.exists() else {}
    keep = {k: result.get(k) for k in ("framesCompared", "framesIdentical", "identical", "runnerCpuSeconds",
                                       "wallSeconds", "runnerPeakRssMiB", "firstDifference")}
    return f"{family}/{folder.name}: rc={done.returncode} {json.dumps(keep)}"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--families", default="ins,g2,p1,r128")
    parser.add_argument("--workers", type=int, default=2)
    parser.add_argument("--runner", default=r"E:/RideBoundData/research/runner-fastrun-g1")
    parser.add_argument("--out", default=str(HERE / "replay-g1"))
    args = parser.parse_args()
    out_dir = pathlib.Path(args.out)
    out_dir.mkdir(exist_ok=True)
    work = list(jobs(set(args.families.split(","))))
    print(f"{len(work)} jobs to replay through {args.runner}", flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        for line in pool.map(lambda job: replay(args.runner, out_dir, *job), work):
            print(line, flush=True)
    rows = [json.loads(path.read_text(encoding="utf-8")) for path in sorted(out_dir.glob("*.json"))
            if path.name != "SUMMARY.json"]
    families: dict[str, dict[str, int]] = {}
    for path in sorted(out_dir.glob("*.json")):
        if path.name == "SUMMARY.json":
            continue
        family = path.name.split("-")[0]
        row = json.loads(path.read_text(encoding="utf-8"))
        entry = families.setdefault(family, {"jobs": 0, "identical": 0})
        entry["jobs"] += 1
        entry["identical"] += int(bool(row.get("identical")))
    summary = {"runner": args.runner, "replayed": len(rows), "identical": sum(bool(r.get("identical")) for r in rows),
               "frames": sum(r.get("framesCompared", 0) for r in rows),
               "framesIdentical": sum(r.get("framesIdentical", 0) for r in rows),
               "runnerCpuSeconds": round(sum(r.get("runnerCpuSeconds") or 0 for r in rows), 1),
               "families": families}
    (out_dir / "SUMMARY.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"G1 gate: {summary['identical']}/{summary['replayed']} jobs byte-identical, "
          f"{summary['framesIdentical']}/{summary['frames']} frames; {families}", flush=True)
    return 0 if summary["identical"] == summary["replayed"] == len(work) else 1


if __name__ == "__main__":
    raise SystemExit(main())
