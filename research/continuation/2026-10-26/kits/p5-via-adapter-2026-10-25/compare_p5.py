"""P5 analysis (written and sealed BEFORE any P5 run). Read-only on its inputs.

Gate 0 (setup validity): every home plain row equals the company confirm row of the same (cell, config, arm) on every
field except the run bookkeeping fields IGNORE. PASS iff all 840 rows (60 cells x 2 configs x 7 arms) match.

Main comparison (audit §6-P5): plain + round vs adapter + half-even on the same 60 cells.
  day value  = sum over the two windows of the mean over the three resamples (company PREREG-SHADOW §4);
               a day enters a contrast only if both arms have all 6 cells in that mode;
  ratio      = 100 * day sum(count) / day sum(served) (pDispPct);
  contrast   = mean over days of (arm a - arm b);
  verdict    = FLIP if mean_plain * mean_adapter < 0; ZERO if exactly one of the two is 0; SAME-SIGN otherwise.
               Audit rule: a FLIP drops that conclusion; SAME-SIGN reports both magnitudes.
Usage: python -I compare_p5.py <company confirm.ndjson> <home plain rows> <home adapter rows> <out dir>
"""
import json
import math
import os
import sys

IGNORE = {"part", "set", "ms", "msScore"}
ARMS = {"INS": ["U", "EO35", "EOHZ35", "RCE35", "SHF72", "SHHZ72", "SHRC72"],
        "G2": ["U", "EO35", "EOHZ35", "RCE35", "SHF71", "SHHZ71", "SHRC71"]}
SH = {"INS": "72", "G2": "71"}
CONTRASTS = [("RCE35", "EOHZ35"), ("RCE35", "EO35"), ("EOHZ35", "EO35"), ("SHRC", "SHHZ"), ("SHRC", "SHF"),
             ("SHHZ", "EOHZ35"), ("RCE35", "U")]
METRICS = ["served", "visRev60", "pDispPct", "late60First", "TV5min", "breachLedger"]


def arm_name(a, cfg):
    return a + SH[cfg] if a in ("SHRC", "SHHZ", "SHF") else a


def load(path):
    rows = {}
    for line in open(path, encoding="utf-8"):
        if not line.strip():
            continue
        r = json.loads(line)
        k = (r["cell"], r["config"], r["arm"])
        if k in rows and rows[k] != r:
            raise SystemExit("duplicate key with different content: %s in %s" % (k, path))
        rows[k] = r
    return rows


def strip(r):
    return {k: v for k, v in r.items() if k not in IGNORE}


def day_values(rows, cfg, arm, cells):
    """day -> dict of summed resample means for the count fields; None if a cell is missing."""
    by_day = {}
    for cell in cells:
        day, win, rs = cell[:10], cell[11:14], cell[15:]
        by_day.setdefault(day, {}).setdefault(win, []).append(rows.get((cell, cfg, arm)))
    out = {}
    for day, wins in by_day.items():
        if sorted(wins) != ["w08", "w17"] or any(len(v) != 3 or None in v for v in wins.values()):
            out[day] = None
            continue
        acc = {}
        for f in ("served", "visRev60", "pDisp", "late60First", "sumTV5", "breachLedger"):
            acc[f] = sum(sum(r[f] for r in v) / 3.0 for v in wins.values())
        out[day] = acc
    return out


def metric(acc, m):
    if m == "pDispPct":
        return 100.0 * acc["pDisp"] / acc["served"]
    if m == "TV5min":
        return acc["sumTV5"] / 60000.0
    return acc[m]


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    company, plain, adapter, outdir = sys.argv[1:5]
    os.makedirs(outdir, exist_ok=True)
    C, P, A = load(company), load(plain), load(adapter)
    cells = sorted({k[0] for k in P})
    lines = []
    out = {"cells": len(cells), "gate0": {}, "identicalRows": {}, "contrasts": []}
    # gate 0
    n_ok, bad = 0, []
    for cell in cells:
        for cfg, arms in ARMS.items():
            for arm in arms:
                k = (cell, cfg, arm)
                if k not in P or k not in C:
                    bad.append((k, "missing"))
                    continue
                a, b = strip(P[k]), strip(C[k])
                if a == b:
                    n_ok += 1
                else:
                    diff = sorted(f for f in set(a) | set(b) if a.get(f) != b.get(f))
                    bad.append((k, diff[:8]))
    total = len(cells) * 14
    out["gate0"] = {"rows": total, "identical": n_ok, "pass": n_ok == total == 840, "firstMismatches": [str(x) for x in bad[:20]]}
    lines.append("GATE 0 home plain vs company confirm rows: %d/%d identical (ignoring %s) -> %s"
                 % (n_ok, total, sorted(IGNORE), "PASS" if out["gate0"]["pass"] else "FAIL"))
    for x in bad[:20]:
        lines.append("  mismatch %s" % (x,))
    # rows identical between modes (outcome fields only)
    for cfg, arms in ARMS.items():
        for arm in arms:
            same = sum(1 for cell in cells if (cell, cfg, arm) in P and (cell, cfg, arm) in A
                       and strip(P[(cell, cfg, arm)]) == strip(A[(cell, cfg, arm)]))
            out["identicalRows"]["%s %s" % (cfg, arm)] = same
    lines.append("")
    lines.append("Rows identical between plain and adapter (of %d cells):" % len(cells))
    for k, v in out["identicalRows"].items():
        lines.append("  %-12s %d" % (k, v))
    # contrasts
    lines.append("")
    lines.append("CONTRASTS (mean over days of a - b; day value = sum of windows of resample means; pDispPct = % of served)")
    lines.append("%-4s %-16s %-12s %10s %10s  %s" % ("cfg", "a - b", "metric", "plain", "adapter", "verdict"))
    for cfg in ARMS:
        for a0, b0 in CONTRASTS:
            a, b = arm_name(a0, cfg), arm_name(b0, cfg)
            res = {}
            for mode, R in (("plain", P), ("adapter", A)):
                da, db = day_values(R, cfg, a, cells), day_values(R, cfg, b, cells)
                days = sorted(d for d in da if da[d] is not None and db.get(d) is not None)
                res[mode] = {m: (sum(metric(da[d], m) - metric(db[d], m) for d in days) / len(days) if days else math.nan,
                                 len(days)) for m in METRICS}
            for m in METRICS:
                vp, np_ = res["plain"][m]
                va, na = res["adapter"][m]
                if vp * va < 0:
                    verdict = "FLIP"
                elif (vp == 0) != (va == 0):
                    verdict = "ZERO"
                else:
                    verdict = "SAME-SIGN"
                out["contrasts"].append({"config": cfg, "a": a, "b": b, "metric": m, "plain": vp, "adapter": va,
                                         "daysPlain": np_, "daysAdapter": na, "verdict": verdict})
                lines.append("%-4s %-16s %-12s %10.3f %10.3f  %s (days %d/%d)" % (cfg, a + " - " + b, m, vp, va, verdict, np_, na))
    flips = [c for c in out["contrasts"] if c["verdict"] != "SAME-SIGN"]
    lines.append("")
    lines.append("FLIP or ZERO: %d of %d contrast-metric pairs" % (len(flips), len(out["contrasts"])))
    for c in flips:
        lines.append("  %s %s - %s %s: plain %.3f adapter %.3f %s" % (c["config"], c["a"], c["b"], c["metric"], c["plain"], c["adapter"], c["verdict"]))
    with open(os.path.join(outdir, "P5-REPORT.txt"), "x", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    with open(os.path.join(outdir, "P5-RESULTS.json"), "x", encoding="utf-8") as f:
        json.dump(out, f, indent=1)
    print("\n".join(lines[:3]))
    print("FLIP or ZERO: %d of %d" % (len(flips), len(out["contrasts"])))


if __name__ == "__main__":
    main()
