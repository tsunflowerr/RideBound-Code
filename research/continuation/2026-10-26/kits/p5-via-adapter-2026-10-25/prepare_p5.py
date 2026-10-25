"""P5 (audit 23/10 §6-P5): pick the 10-day slice by a fixed seed and build the two cell sets.

Day rule (fixed before any P5 run): rank the 60 wf14 confirm days (sp/wf15/work/days_confirm.csv) by
sha256("p5-adapter-v1|" + day) ascending and keep the first 10; every day keeps both windows and all 3 resamples (60 cells).

Cell sets (only these two string fields differ; everything else is byte-identical to the intake cell):
  plain   : the intake cell file, byte copy               identity.mode "plain", travel.snapshotRounding "round"
  adapter : identity.mode "ridebound-fleetpy-adapter", travel.snapshotRounding "fleetpy-seconds-half-even"
Matrices are byte copies into cells/matrices (both sets reference ../matrices/<name>.bin).
Usage: python -I prepare_p5.py   (refuses if the cells folder exists)
"""
import csv
import hashlib
import json
import os
import shutil
import sys

SRC = "E:/RideBoundData/company-intake/2026-10-23/sp/wf14/scenarios"
ROOT = "E:/RideBoundData/research/p5-via-adapter-v1"
DAYS_CSV = ROOT + "/sp/wf15/work/days_confirm.csv"
SEED = "p5-adapter-v1|"
PLAIN_ID, ADAPT_ID = '"identity":{"mode":"plain"}', '"identity":{"mode":"ridebound-fleetpy-adapter"}'
PLAIN_RD, ADAPT_RD = '"snapshotRounding":"round"', '"snapshotRounding":"fleetpy-seconds-half-even"'


def sha(b):
    return hashlib.sha256(b).hexdigest()


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    out = ROOT + "/cells"
    if os.path.exists(out):
        raise SystemExit("refused: " + out + " exists")
    days = [l.strip() for l in open(DAYS_CSV, encoding="utf-8") if len(l.strip()) == 10 and l.strip()[4] == "-"]
    if len(days) != 60 or len(set(days)) != 60:
        raise SystemExit("expected 60 distinct confirm days, got %d" % len(days))
    ranked = sorted(days, key=lambda d: sha((SEED + d).encode()))
    pick = sorted(ranked[:10])
    os.makedirs(out + "/plain")
    os.makedirs(out + "/adapter")
    os.makedirs(out + "/matrices")
    rows = []
    for day in pick:
        for win in ("w08", "w17"):
            for rs in ("r0", "r1", "r2"):
                cell = "%s-%s-%s" % (day, win, rs)
                src = "%s/weekdays-P512/%s.json" % (SRC, cell)
                raw = open(src, "rb").read()
                text = raw.decode("utf-8")
                for s in (PLAIN_ID, PLAIN_RD):
                    if text.count(s) != 1:
                        raise SystemExit("%s: expected exactly one %s, found %d" % (cell, s, text.count(s)))
                adapted = text.replace(PLAIN_ID, ADAPT_ID).replace(PLAIN_RD, ADAPT_RD).encode("utf-8")
                j = json.loads(text)
                mat = j["travel"]["base"]["matrixFile"]
                if not mat.startswith("../matrices/"):
                    raise SystemExit(cell + ": unexpected matrixFile " + mat)
                mname = mat.split("/")[-1]
                msrc = "%s/matrices/%s" % (SRC, mname)
                shutil.copyfile(msrc, out + "/matrices/" + mname)
                open(out + "/plain/" + cell + ".json", "xb").write(raw)
                open(out + "/adapter/" + cell + ".json", "xb").write(adapted)
                mbytes = open(out + "/matrices/" + mname, "rb").read()
                prov = json.load(open("%s/provenance/%s.prov.json" % (SRC, mname[:-4]), encoding="utf-8"))
                if sha(mbytes) != sha(open(msrc, "rb").read()) or prov["travel"]["matrixSha256"] != sha(mbytes):
                    raise SystemExit(cell + ": matrix copy or provenance matrixSha256 mismatch")
                rows.append((cell, sha(raw), sha(adapted), mname, sha(mbytes)))
    with open(ROOT + "/P5-DAYS.txt", "x", encoding="utf-8") as f:
        f.write("# rule: sorted(days_confirm, key=sha256('p5-adapter-v1|'+day))[:10], then sorted by date\n")
        f.write("\n".join(pick) + "\n")
    with open(ROOT + "/P5-CELLS.tsv", "x", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter="\t", lineterminator="\n")
        w.writerow(["cell", "plainSha256", "adapterSha256", "matrix", "matrixSha256"])
        w.writerows(rows)
    print("days:", " ".join(pick))
    print("cells:", len(rows))


if __name__ == "__main__":
    main()
