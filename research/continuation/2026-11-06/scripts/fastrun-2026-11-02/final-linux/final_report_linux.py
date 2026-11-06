"""All comparisons and metrics of the final runs in one command (ADR-089 GĐ5/GĐ6, 2026-11-04). Every tool runs through
with_evidence.py, so job folders whose transcript was dropped are read from their evidence; the tools themselves (and
the sealed prediction files they check) are unchanged.

  1 riders     compare_fullsys.py: t0, p0, A, served, sum|c|, #c, net c, max|c|, shared, accepted set, G2 moves
  2 sequences  compare_drop_sequences.py: every served rider's published drop-ETA sequence; visRev60 and TV5
  3 shadow     compare_shadow.py: class and score of every rider (Shadow arms)
  4 eta        eta_metrics.py compare: (publication time, ETA) sequences, t0, A; time-weighted ETA error, reversals
  5 reversals  reversal_counts.py real: reversal counts per arm (same rule as the report's visRev60)
Writes <out>/ (must not exist): one file per step, the tools' own logs, and FINAL-REPORT.json.

Usage: python -I -B final_report.py --runs <runs> [--runs ...] --out <dir> --arms FS-G2-SHRC71-CL,FS-G2-SHHZ71-N
           --predictions <rows.tsv> --seal <seal> --sequences <drop-sequences.tsv> --sequences-seal <seal>
           --timed <timed.tsv> --timed-seal <seal>
Defaults are the 168-job final comparison.
"""
import argparse
import json
import pathlib
import subprocess
import sys

KIT = pathlib.Path("/rb/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02")
DATA = pathlib.Path("/rb/RideBoundData/research/fullsys-f14-v2")
WRAP = KIT / "final-linux" / "with_evidence_linux.py"


def tool(out: pathlib.Path, name: str, *args: str) -> int:
    done = subprocess.run([sys.executable, "-I", "-B", str(WRAP), *args], stdout=subprocess.PIPE,
                          stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    (out / f"{name}.log").write_text(f"returncode={done.returncode}\n{done.stdout}", encoding="utf-8")
    print(f"{name}: returncode {done.returncode}", flush=True)
    return done.returncode


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--runs", action="append", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--arms", default="FS-G2-SHRC71-CL,FS-G2-SHHZ71-N")
    p.add_argument("--predictions", default=str(DATA / "predictions/fullsys-f14-predictions.tsv"))
    p.add_argument("--seal", default=str(KIT / "fullsys/FULLSYS-F14-PREDICTIONS.sha256"))
    p.add_argument("--sequences", default=str(DATA / "predictions/fullsys-f14-drop-sequences.tsv"))
    p.add_argument("--sequences-seal", default=str(KIT / "fullsys/FULLSYS-F14-SEQUENCES.sha256"))
    p.add_argument("--timed", default=str(DATA / "predictions-timed/timed-adapter.tsv.gz"))
    p.add_argument("--timed-seal", default=str(KIT / "fullsys/FULLSYS-F14-TIMED.sha256"))
    a = p.parse_args()
    out = pathlib.Path(a.out)
    out.mkdir(parents=True)
    roots = [x for r in a.runs for x in ("--root", r)]
    codes = {
        "1-riders": tool(out, "1-riders", "compare_fullsys.py", str(DATA), a.predictions, a.seal, a.arms,
                         str(out / "riders"), *roots),
        "2-sequences": tool(out, "2-sequences", "compare_drop_sequences.py", a.sequences, a.sequences_seal,
                            str(out / "sequences.json"), *roots),
        "3-shadow": tool(out, "3-shadow", "compare_shadow.py", a.predictions, a.seal, str(out / "shadow.json"), *roots),
    }
    codes["4-eta"] = tool(out, "4-eta", "eta_metrics.py", "compare", a.timed, "--seal", a.timed_seal,
                          "--riders", str(out / "riders-real-riders.tsv"), *roots, "--out", str(out / "eta.json"))
    codes["5-reversals"] = tool(out, "5-reversals", "reversal_counts.py", "real", a.sequences, *roots,
                                "--out", str(out / "reversals.json"))

    def load(name):
        path = out / name
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else None

    riders, sequences, shadow, eta = (load(n) for n in ("riders-compare.json", "sequences.json", "shadow.json",
                                                        "eta.json"))
    report = {
        "returnCodes": codes,
        "riders": riders and {k: riders[k] for k in ("finishedJobs", "cellArmsCompared", "cellArmsExact", "riders",
                                                      "ridersIdentical", "movesEqual", "g2CellArms", "byArm")},
        "sequences": sequences and {k: v for k, v in sequences.items() if k != "results"},
        "shadow": shadow and {k: v for k, v in shadow.items() if k != "results"},
        "eta": eta and {"cellArms": eta["cellArms"], "exact": eta["exact"], "real": eta["real"],
                        "emulator": eta["emulator"]},
        "reversals": load("reversals.json"),
    }
    exact = (riders and riders["cellArmsExact"] == riders["cellArmsCompared"] > 0
             and sequences and sequences["exact"] == sequences["cellArms"]
             and shadow and shadow["exact"] == shadow["cellArms"]
             and eta and eta["exact"] == eta["cellArms"])
    report["allExact"] = bool(exact)
    (out / "FINAL-REPORT.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"allExact": report["allExact"], "returnCodes": codes}))
    return 0 if exact and all(c == 0 for c in codes.values()) else 1


if __name__ == "__main__":
    raise SystemExit(main())
