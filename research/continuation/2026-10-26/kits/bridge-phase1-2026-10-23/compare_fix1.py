"""Owner condition 3 for the donor-side fix: verdict over the fix1 reruns. Writes compare/fix1-<tag>/condition3.json.

(a) every fix1 job whose earlier run completed: compare_runs.py (64-hex hashes masked) must report identical, or a
    difference only in frame 2's provenance fields under the A5 rule (run_g2.a5_provenance_only);
(b) every fix1 job against the sealed emulator predictions of its family (bridge_compare.py / compare_p1.py /
    compare_r128.py, each with its own seal check); in particular the job that stopped in p1main must now be exact.
Any job that matched before and changed means: stop and report.
Usage: python -B compare_fix1.py <tag>
"""
from __future__ import annotations

import importlib.util
import json
import os
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
BRIDGE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
PYTHON = r"E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe"
FIX_ROOTS = (pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1/fix1"),   # the first 6 fix1bridge jobs
             pathlib.Path(r"E:/RideBoundData/research/via-bridge-man-v1/fix1"))   # every later job (PHASE1-LOG P8)
ENV = dict(os.environ, PYTHONIOENCODING="utf-8")


def load(name: str, path: pathlib.Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> int:
    tag = sys.argv[1]
    outdir = HERE / "compare" / f"fix1-{tag}"
    outdir.mkdir(parents=True, exist_ok=False)
    run_g2 = load("run_g2_v4", BRIDGE / "run_g2.py")
    sys.path.insert(0, str(HERE))
    fix = load("run_fix1", HERE / "run_fix1.py")
    report = {"previous": [], "emulator": {}}
    for name, spec in sorted(fix.SPECS.items()):
        new = next((root / spec["phase"] / name for root in FIX_ROOTS if (root / spec["phase"] / name).exists()),
                   FIX_ROOTS[-1] / spec["phase"] / name)
        old = spec["previous"]
        if not (new / "summary.json").exists():
            report["previous"].append({"job": name, "pass": False, "why": "fix1 job has no summary"})
            continue
        if not (old / "summary.json").exists():
            report["previous"].append({"job": name, "pass": None, "why": "earlier run stopped; judged by (b) only"})
            continue
        out = outdir / f"prev-{name}.json"
        result = subprocess.run([PYTHON, "-B", str(BRIDGE / "compare_runs.py"), str(old), str(new), "--json", str(out)],
                                capture_output=True, text=True, env=ENV)
        data = json.loads(out.read_text(encoding="utf-8"))
        ok, why = (True, "identical") if result.returncode == 0 else run_g2.a5_provenance_only(data, old, new)
        report["previous"].append({"job": name, "pass": ok, "why": why, "framesIdentical": data["framesIdentical"],
                                   "framesCompared": data["framesCompared"]})
    def roots(phase: str) -> list[str]:
        return [arg for root in FIX_ROOTS if (root / phase).exists() for arg in ("--root", str(root / phase))]

    # bridge_compare.py takes one --root, so the bridge family is compared once per drive (C: holds the first 6 jobs).
    bridge_runs = [(f"bridge-{root.drive[0]}", [PYTHON, "-I", "-B", str(BRIDGE / "bridge_compare.py"), "--root",
                    str(root / "fix1bridge"), "--emulator", "emulator-riders-g2-2000.tsv", "--seal",
                    "EMULATOR-PREDICTIONS-G2.sha256", str(outdir / f"bridge-{root.drive[0]}")])
                   for root in FIX_ROOTS if (root / "fix1bridge").exists()]
    for family, command in (
            *bridge_runs,
            ("p1", [PYTHON, "-B", str(HERE / "compare_p1.py"), str(outdir / "p1"), *roots("fix1p1")]),
            ("r128", [PYTHON, "-B", str(HERE / "compare_r128.py"), str(outdir / "r128"), *roots("fix1r128")])):
        cwd = BRIDGE if family.startswith("bridge") else HERE
        result = subprocess.run(command, capture_output=True, text=True, cwd=str(cwd), env=ENV)
        report["emulator"][family] = (result.stdout + result.stderr).strip().splitlines()
    judged = [r for r in report["previous"] if r["pass"] is not None]
    report["summary"] = {
        "previousIdenticalOrA5": f"{sum(r['pass'] for r in judged)}/{len(judged)}",
        "a5Only": sum(r["pass"] and r["why"] != "identical" for r in judged),
        "earlierStopped": [r["job"] for r in report["previous"] if r["pass"] is None],
        "emulator": {k: v[0] if v else "no output" for k, v in report["emulator"].items()},
    }
    (outdir / "condition3.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report["summary"], indent=2))
    for family, lines in report["emulator"].items():
        for line in lines[1:]:
            if "CHECK" in line or "NOT EXACT" in line:
                print(family, line)
    for r in report["previous"]:
        if r["pass"] is False:
            print("CHANGED OR MISSING", r)
    return 0


if __name__ == "__main__":
    sys.exit(main())
