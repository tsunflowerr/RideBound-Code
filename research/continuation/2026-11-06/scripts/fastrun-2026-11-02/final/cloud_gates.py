"""Gates on the rented machine before the 168 final jobs (ADR-089 GĐ5/GĐ6, 2026-11-04). Exit 0 only if all pass.

  A  cloud = home: the check jobs whose home reference exists (E:/RideBoundData/final-v2/reference) have the same frames,
     frame by frame (cloud_check.py).
  B  the 4 check jobs (2 gate cells x 2 main arms) equal the sealed predictions on all four aspects: rider fields,
     published drop-ETA sequences, Shadow classes and scores, (publication time, ETA) sequences (final_report.py).
  C  the 4 attribution jobs (FS-G2-SHHZ71-CL, FS-G2-SHRC71-N x 2 gate cells) equal their sealed predictions
     (FULLSYS-F14-TIMED-GATE.sha256): rider fields, Shadow classes and scores, (publication time, ETA) sequences.
     These were not run at home: disk E was at its floor on 2026-11-04.
Every comparison reads the jobs' evidence (with_evidence.py); the tools and the sealed files are the ones in the bundle.

Usage: python -I -B cloud_gates.py --check-runs E:/runs/final --attr-runs E:/runs/gates --out E:/runs/gates-report
"""
import argparse
import json
import os
import pathlib
import subprocess
import sys

KIT = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02")
DATA = pathlib.Path(r"E:/RideBoundData/research/fullsys-f14-v2")
GATE = DATA / "predictions-timed-gate"
GATE_SEAL = KIT / "fullsys" / "FULLSYS-F14-TIMED-GATE.sha256"
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
    wrap = KIT / "fullsys" / "with_evidence.py"
    codes = {
        "A-cloud-equals-home": run(out, "A-cloud-equals-home", [
            KIT / "final" / "cloud_check.py", "--runs", a.check_runs,
            "--reference", r"E:/RideBoundData/final-v2/reference", "--out", out / "A-cloud-check.json"]),
        "B-main-arms": run(out, "B-main-arms", [
            KIT / "final" / "final_report.py", "--runs", a.check_runs, "--out", out / "B-main"]),
        "C1-attr-riders": run(out, "C1-attr-riders", [
            wrap, "compare_fullsys.py", DATA, GATE / "attr-predictions.tsv", GATE_SEAL,
            "FS-G2-SHHZ71-CL,FS-G2-SHRC71-N", out / "C-attr", "--root", a.attr_runs]),
        "C2-attr-shadow": run(out, "C2-attr-shadow", [
            wrap, "compare_shadow.py", GATE / "attr-predictions.tsv", GATE_SEAL, out / "C-attr-shadow.json",
            "--root", a.attr_runs]),
    }
    codes["C3-attr-eta"] = run(out, "C3-attr-eta", [
        wrap, "eta_metrics.py", "compare", GATE / "timed-adapter.tsv", "--seal", GATE_SEAL,
        "--riders", out / "C-attr-real-riders.tsv", "--root", a.attr_runs, "--out", out / "C-attr-eta.json"])

    def load(name):
        path = out / name
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else None

    attr = load("C-attr-compare.json")
    report = {"returnCodes": codes,
              "cloudCheck": load("A-cloud-check.json"),
              "mainArms": load("B-main/FINAL-REPORT.json"),
              "attribution": attr and {k: attr[k] for k in ("cellArmsCompared", "cellArmsExact", "riders",
                                                            "ridersIdentical", "movesEqual", "g2CellArms")},
              "attributionShadow": (load("C-attr-shadow.json") or {}).get("exact"),
              "attributionEta": (load("C-attr-eta.json") or {}).get("exact")}
    ok = (all(c == 0 for c in codes.values())
          and report["mainArms"] and report["mainArms"]["allExact"] and report["mainArms"]["riders"]["finishedJobs"] == 4
          and report["cloudCheck"] and report["cloudCheck"]["compared"] >= 3
          and report["cloudCheck"]["identical"] == report["cloudCheck"]["compared"]
          and attr and attr["cellArmsCompared"] == 4 and attr["cellArmsExact"] == 4)
    report["pass"] = bool(ok)
    (out / "GATES-REPORT.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"pass": report["pass"], "returnCodes": codes}))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
