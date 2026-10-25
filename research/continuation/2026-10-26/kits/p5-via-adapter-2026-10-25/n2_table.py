"""N2 alpha curve table (exploratory, after the audit). Same day value as compare_p5.py: sum over the two windows of the
mean over the three resamples; pDispPct = 100 * day sum(pDisp) / day sum(served). Reports the mean over the 10 days.
alpha 35 rows come from the P5 plain rows (same cells, same runner, same mode).
Usage: python -I n2_table.py <n2 rows> <p5 plain rows> <out.tsv>
"""
import json
import sys

FIELDS = ("served", "visRev60", "pDisp", "late60First", "sumTV5", "breachLedger")


def load(paths):
    rows = {}
    for p in paths:
        for line in open(p, encoding="utf-8"):
            if line.strip():
                r = json.loads(line)
                rows[(r["cell"], r["config"], r["arm"])] = r
    return rows


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    rows = load(sys.argv[1:3])
    cells = sorted({k[0] for k in rows})
    days = sorted({c[:10] for c in cells})
    out = ["config\tfamily\talpha\tdays\tserved\tvisRev60\tpDispPct\tlate60First\tTV5min\tbreachLedger"]
    for cfg in ("INS", "G2"):
        for fam in ("EO", "EOHZ", "RCE"):
            for a in (0, 10, 20, 35, 50, 75, 100):
                arm = "%s%d" % (fam, a)
                vals = []
                for d in days:
                    acc = dict.fromkeys(FIELDS, 0.0)
                    ok = True
                    for win in ("w08", "w17"):
                        rs = [rows.get(("%s-%s-r%d" % (d, win, i), cfg, arm)) for i in range(3)]
                        if None in rs:
                            ok = False
                            break
                        for f in FIELDS:
                            acc[f] += sum(r[f] for r in rs) / 3.0
                    if ok:
                        vals.append(acc)
                if not vals:
                    continue
                n = len(vals)
                mean = lambda f: sum(v[f] for v in vals) / n
                pdisp = sum(100.0 * v["pDisp"] / v["served"] for v in vals) / n
                out.append("\t".join([cfg, fam, str(a), str(n), "%.2f" % mean("served"), "%.2f" % mean("visRev60"),
                                      "%.3f" % pdisp, "%.2f" % mean("late60First"), "%.1f" % (mean("sumTV5") / 60000.0),
                                      "%.2f" % mean("breachLedger")]))
    with open(sys.argv[3], "x", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    print("\n".join(out))


if __name__ == "__main__":
    main()
