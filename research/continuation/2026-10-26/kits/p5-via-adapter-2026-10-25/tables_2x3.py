"""Independent recomputation (home script, not company code) of the main Via tables: wf14 (U, EO35, EOHZ35, RCE35) and
wf15 (Shadow) confirm rows, 60 days, mode N. Day value = sum over the two windows of the mean over the three resamples;
ratios = 100 * day sum(count) / day sum(served). Contrast = mean over days of (a - b) with a two-sided 95% t interval over
days and the count of days a < b / a > b. Used to cross-check the audit's numbers (KT:632) and to build the thesis 2x3 table.
Usage: python -I tables_2x3.py <wf14 main.ndjson> <wf15 confirm.ndjson> <out.txt>
"""
import json
import math
import sys

COUNTS = ("served", "visRev60", "pDisp", "late60First", "breachDeadline60", "breachLedger", "sumTV5")
T975 = {59: 2.000995, 58: 2.001717, 57: 2.002465}


def load(paths):
    rows = {}
    for p in paths:
        for line in open(p, encoding="utf-8"):
            if line.strip():
                r = json.loads(line)
                if r.get("mode", "N") != "N":
                    continue
                rows[(r["cell"], r["config"], r["arm"])] = r
    return rows


def day_values(rows, cfg, arm):
    by = {}
    for (cell, c, a), r in rows.items():
        if c == cfg and a == arm:
            by.setdefault(cell[:10], {}).setdefault(cell[11:14], []).append(r)
    out = {}
    for day, wins in by.items():
        if sorted(wins) != ["w08", "w17"] or any(len(v) != 3 for v in wins.values()):
            continue
        out[day] = {f: sum(sum(r.get(f) or 0 for r in v) / 3.0 for v in wins.values()) for f in COUNTS}
    return out


def metric(acc, m):
    if m.endswith("Pct"):
        return 100.0 * acc[m[:-3]] / acc["served"]
    if m == "TV5min":
        return acc["sumTV5"] / 60000.0
    return acc[m]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    rows = load(sys.argv[1:3])
    lines = []
    metrics = ["served", "visRev60", "pDispPct", "late60First", "breachDeadline60Pct", "TV5min"]
    for cfg, sh in (("INS", "72"), ("G2", "71")):
        arms = ["U", "EO35", "EOHZ35", "RCE35", "SHF" + sh, "SHHZ" + sh, "SHRC" + sh]
        dv = {a: day_values(rows, cfg, a) for a in arms}
        lines.append("== %s: mean day value over days (n days)" % cfg)
        lines.append("%-8s %5s " % ("arm", "days") + " ".join("%12s" % m for m in metrics))
        for a in arms:
            d = dv[a]
            if not d:
                continue
            lines.append("%-8s %5d " % (a, len(d)) + " ".join("%12.3f" % (sum(metric(v, m) for v in d.values()) / len(d)) for m in metrics))
        lines.append("-- contrasts: mean diff [95%% t CI] (days a<b / a>b)")
        for a, b in (("RCE35", "EOHZ35"), ("RCE35", "EO35"), ("EOHZ35", "EO35"), ("SHRC" + sh, "SHHZ" + sh),
                     ("SHRC" + sh, "SHF" + sh), ("SHHZ" + sh, "EOHZ35")):
            days = sorted(set(dv[a]) & set(dv[b]))
            if not days:
                continue
            for m in ("served", "visRev60", "pDispPct", "late60First", "breachDeadline60Pct"):
                diffs = [metric(dv[a][d], m) - metric(dv[b][d], m) for d in days]
                n = len(diffs)
                mean = sum(diffs) / n
                sd = math.sqrt(sum((x - mean) ** 2 for x in diffs) / (n - 1)) if n > 1 else float("nan")
                h = T975.get(n - 1, 2.0) * sd / math.sqrt(n)
                lines.append("%-6s %-16s %-20s %9.3f [%8.3f, %8.3f]  (%d/%d) n=%d" % (
                    cfg, a + "-" + b, m, mean, mean - h, mean + h, sum(x < 0 for x in diffs), sum(x > 0 for x in diffs), n))
        lines.append("")
    with open(sys.argv[3], "x", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
