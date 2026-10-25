"""N1 table: strict fences (EO35, SHF) at fence tolerance 0 / 1 / 5 / 60 s vs the relaxed fence and Rule C (P5 plain rows).
Day value as in compare_p5.py. Reports served, visRev60, G_dispAny1 (guaranteed riders whose drop time dispatch changed,
both directions, >= 1 s; Shadow arms only) and the remaining gaps SHRC - SHF(tol) and EOHZ35 - EO35(tol).
Usage: python -I n1_table.py <p5 plain rows> <tol1000 rows> <tol5000 rows> <tol60000 rows> <out.tsv>
"""
import json
import sys

FIELDS = ("served", "visRev60", "G_dispAny1", "late60First")


def load(path):
    rows = {}
    for line in open(path, encoding="utf-8"):
        if line.strip():
            r = json.loads(line)
            rows[(r["cell"], r["config"], r["arm"])] = r
    return rows


def day_mean(rows, cfg, arm):
    days = sorted({k[0][:10] for k in rows})
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
                acc[f] += sum((r.get(f) or 0) for r in rs) / 3.0
        if ok:
            vals.append(acc)
    if not vals:
        return None
    return {f: sum(v[f] for v in vals) / len(vals) for f in FIELDS}, len(vals)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    base = load(sys.argv[1])
    tol = {0: base, 1: load(sys.argv[2]), 5: load(sys.argv[3]), 60: load(sys.argv[4])}
    out = ["config\tarm\ttol_s\tdays\tserved\tvisRev60\tG_dispAny1\tlate60First\tgap_served_vs_relaxed\tgap_visRev60_vs_relaxed"]
    for cfg, sh in (("INS", "72"), ("G2", "71")):
        for strict, relaxed in (("EO35", "EOHZ35"), ("SHF" + sh, "SHRC" + sh), ("SHF" + sh, "SHHZ" + sh)):
            ref = day_mean(base, cfg, relaxed)
            for t, rows in tol.items():
                m = day_mean(rows, cfg, strict)
                if m is None or ref is None:
                    continue
                (v, n), (rv, _) = m, ref
                out.append("\t".join([cfg, strict + " vs " + relaxed, str(t), str(n), "%.2f" % v["served"], "%.2f" % v["visRev60"],
                                      "%.2f" % v["G_dispAny1"], "%.2f" % v["late60First"],
                                      "%.2f" % (rv["served"] - v["served"]), "%.2f" % (rv["visRev60"] - v["visRev60"])]))
            out.append("\t".join([cfg, relaxed + " (reference)", "-", "", "%.2f" % ref[0]["served"], "%.2f" % ref[0]["visRev60"],
                                  "%.2f" % ref[0]["G_dispAny1"], "%.2f" % ref[0]["late60First"], "", ""]))
    with open(sys.argv[5], "x", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    print("\n".join(out))


if __name__ == "__main__":
    main()
