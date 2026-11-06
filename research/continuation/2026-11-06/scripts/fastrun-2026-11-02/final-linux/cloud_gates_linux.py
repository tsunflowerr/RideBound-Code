"""Gates on the rented LINUX machine before the 168 final jobs (2026-11-04). Exit 0 only if all pass.

The Windows bundle's gate A (frames identical to this project's home runs) cannot hold on Linux: the run manifests carry
the Linux paths of .NET, the Runner and the configurations, so frame hashes differ by construction. What must hold is
what the thesis uses, rider by rider, against the predictions sealed before any job:
  B  the 4 check jobs (2 gate cells x the 2 main arms) equal the sealed predictions on all four aspects: rider fields,
     published drop-ETA sequences, Shadow classes and scores, (publication time, ETA) sequences (final_report_linux.py).
     The home machine (Windows) ran 3 of these 4 jobs from the same Runner and matched the same predictions exactly,
     so B passing means Linux = Windows rider by rider.
  C  the 4 attribution jobs (FS-G2-SHHZ71-CL, FS-G2-SHRC71-N x 2 gate cells) equal their sealed predictions
     (FULLSYS-F14-TIMED-GATE.sha256): rider fields, Shadow classes and scores, (publication time, ETA) sequences.
Usage: python -I -B cloud_gates_linux.py --check-runs /rb/runs/final --attr-runs /rb/runs/gates --out /rb/runs/gates-report
"""
import argparse
import json
import os
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
KIT = pathlib.Path("/rb/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02")
DATA = pathlib.Path("/rb/RideBoundData/research/fullsys-f14-v2")
GATE = DATA / "predictions-timed-gate"
GATE_SEAL = KIT / "fullsys" / "FULLSYS-F14-TIMED-GATE.sha256"
WRAP = HERE / "with_evidence_linux.py"
ENV = dict(os.environ, RB_PREFER_EVIDENCE="1")


def run(out: pathlib.Path, name: str, args: list) -> int:
    done = subprocess.run([sys.executable, "-I", "-B", *map(str, args)], stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace", env=ENV)
    (out / f"{name}.log").write_text(f"returncode={done.returncode}\n{done.stdout}", encoding="utf-8")
    print(f"{name}: returncode {done.returncode}", flush=True)
    return done.returncode


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--check-runs", required=True)
    p.add_argument("--attr-runs", required=True)
    p.add_argument("--out", required=True)
    a = p.parse_args()
    out = pathlib.Path(a.out)
    out.mkdir(parents=True)
    codes = {
        "B-main-arms": run(out, "B-main-arms", [HERE / "final_report_linux.py", "--runs", a.check_runs,
                                                "--out", out / "B-main"]),
        "C1-attr-riders": run(out, "C1-attr-riders", [
            WRAP, "compare_fullsys.py", DATA, GATE / "attr-predictions.tsv", GATE_SEAL,
            "FS-G2-SHHZ71-CL,FS-G2-SHRC71-N", out / "C-attr", "--root", a.attr_runs]),
        "C2-attr-shadow": run(out, "C2-attr-shadow", [
            WRAP, "compare_shadow.py", GATE / "attr-predictions.tsv", GATE_SEAL, out / "C-attr-shadow.json",
            "--root", a.attr_runs]),
    }
    codes["C3-attr-eta"] = run(out, "C3-attr-eta", [
        WRAP, "eta_metrics.py", "compare", GATE / "timed-adapter.tsv", "--seal", GATE_SEAL,
        "--riders", out / "C-attr-real-riders.tsv", "--root", a.attr_runs, "--out", out / "C-attr-eta.json"])

    def load(name):
        path = out / name
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else None

    main_arms, attr = load("B-main/FINAL-REPORT.json"), load("C-attr-compare.json")
    shadow, eta = load("C-attr-shadow.json") or {}, load("C-attr-eta.json") or {}
    report = {"returnCodes": codes, "mainArms": main_arms,
              "attribution": attr and {k: attr[k] for k in ("cellArmsCompared", "cellArmsExact", "riders",
                                                            "ridersIdentical", "movesEqual", "g2CellArms")},
              "attributionShadow": {k: shadow.get(k) for k in ("cellArms", "exact")},
              "attributionEta": {k: eta.get(k) for k in ("cellArms", "exact")}}
    ok = (all(c == 0 for c in codes.values())
          and main_arms and main_arms["allExact"] and main_arms["riders"]["finishedJobs"] == 4
          and attr and attr["cellArmsCompared"] == 4 and attr["cellArmsExact"] == 4
          and shadow.get("cellArms") == 4 and shadow.get("exact") == 4
          and eta.get("cellArms") == 4 and eta.get("exact") == 4)
    report["pass"] = bool(ok)
    (out / "GATES-REPORT.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"pass": report["pass"], "returnCodes": codes}))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
