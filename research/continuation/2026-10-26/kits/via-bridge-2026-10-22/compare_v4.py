"""Owner condition 3 for the v4 rerun (AMENDMENTS A8). Writes compare/v4-condition3-<date>.json and prints a summary.

(a) the 33 G2 jobs (g2v4/g2all) against the sealed emulator predictions (bridge_compare.py): 33/33 exact required;
(b) the 30 G2 jobs that already matched (g2v3/g2pilot 3 + g2v3/g2main 27) against their v4 reruns: compare_runs.py
    must report identical (same commit, same Runner build; 64-hex hashes masked);
(c) the 80 earlier insertion-only jobs (via-bridge-man-v1/{regress,smoke,main,elmi}) against their v4 reruns
    (g2v4/ins*): identical, or different only in frame 2's provenance fields under the A5 rule
    (run_g2.a5_provenance_only).
Any failure in (b) or (c) means a previously matching job changed: stop and report.

Usage: python -I -B compare_v4.py <date tag>
"""

from __future__ import annotations

import importlib.util
import json
import os
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
PYTHON = r"E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe"
BASE = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1")
V3 = BASE / "g2v3"
V4 = BASE / "g2v4"
INS = {"insregress": "regress", "inssmoke": "smoke", "insmain": "main", "inselmi": "elmi"}


def run_g2_module():
    spec = importlib.util.spec_from_file_location("run_g2_v4", HERE / "run_g2.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def compare(old: pathlib.Path, new: pathlib.Path, out: pathlib.Path) -> tuple[int, dict]:
    result = subprocess.run([PYTHON, "-B", str(HERE / "compare_runs.py"), str(old), str(new), "--json", str(out)],
                            capture_output=True, text=True, env=dict(os.environ, PYTHONIOENCODING="utf-8"))
    return result.returncode, json.loads(out.read_text(encoding="utf-8"))


def main() -> int:
    tag = sys.argv[1]
    outdir = HERE / "compare" / f"v4-{tag}"
    outdir.mkdir(parents=True, exist_ok=False)
    rg = run_g2_module()
    report = {"g2Emulator": None, "g2Previous": [], "ins": []}

    emulator = subprocess.run([PYTHON, "-I", "-B", str(HERE / "bridge_compare.py"), "--root", str(V4 / "g2all"),
                               "--emulator", "emulator-riders-g2-2000.tsv", "--seal", "EMULATOR-PREDICTIONS-G2.sha256",
                               str(outdir / "g2all")], capture_output=True, text=True, cwd=str(HERE),
                              env=dict(os.environ, PYTHONIOENCODING="utf-8"))
    report["g2Emulator"] = emulator.stdout.strip().splitlines()

    previous = [(V3 / "g2pilot" / name.name, name.name) for name in sorted((V3 / "g2pilot").iterdir())] + \
               [(V3 / "g2main" / name.name, name.name) for name in sorted((V3 / "g2main").iterdir())]
    for old, name in previous:
        if not (old / "summary.json").exists():
            continue                      # the 3 jobs that stopped in v3 have nothing to stay identical to
        new = V4 / "g2all" / name
        if not (new / "summary.json").exists():
            report["g2Previous"].append({"job": name, "identical": False, "why": "v4 job has no summary"})
            continue
        rc, data = compare(old, new, outdir / f"g2prev-{name}.json")
        report["g2Previous"].append({"job": name, "identical": rc == 0, "framesIdentical": data["framesIdentical"],
                                     "framesCompared": data["framesCompared"], "first": data["firstDifference"]})

    for phase, source in INS.items():
        for name, _arm, _cell in rg.jobs(phase):
            old, new = BASE / source / name, V4 / phase / name
            if not (new / "summary.json").exists():
                report["ins"].append({"job": f"{source}/{name}", "pass": False, "why": "v4 job has no summary"})
                continue
            rc, data = compare(old, new, outdir / f"ins-{source}-{name}.json")
            ok, why = (True, "identical") if rc == 0 else rg.a5_provenance_only(data, old, new)
            report["ins"].append({"job": f"{source}/{name}", "pass": ok, "why": why,
                                  "framesIdentical": data["framesIdentical"], "framesCompared": data["framesCompared"]})

    prev_ok = sum(r["identical"] for r in report["g2Previous"])
    ins_ok = sum(r["pass"] for r in report["ins"])
    report["summary"] = {
        "g2Emulator": report["g2Emulator"][0] if report["g2Emulator"] else "no output",
        "g2PreviousIdentical": f"{prev_ok}/{len(report['g2Previous'])}",
        "insIdenticalOrA5": f"{ins_ok}/{len(report['ins'])}",
        "insA5Only": sum(r["pass"] and r["why"] != "identical" for r in report["ins"]),
    }
    (outdir / "condition3.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report["summary"], indent=2))
    for line in report["g2Emulator"][1:]:
        print(line)
    for r in report["g2Previous"]:
        if not r["identical"]:
            print("G2 PREVIOUS CHANGED", r)
    for r in report["ins"]:
        if not r["pass"]:
            print("INS CHANGED", r)
    return 0


if __name__ == "__main__":
    sys.exit(main())
