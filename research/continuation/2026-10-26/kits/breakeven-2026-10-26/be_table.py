"""Break-even tables (BREAKEVEN-PLAN.md sections 5-8). Run only after be_gate.py printed GATE PASS.
Day value = sum over the two windows of the mean over the three resamples; curves = mean over the 10 days.
Comparisons per config: matched service (RCE interpolated in alpha to the comparator's served; first bracketing segment
from low alpha; outside the RCE grid -> not computed) against EOHZ (main) and EO; same alpha against EOHZ and EO.
Decision rule: rule C preferred iff w*dR + dL < 0, w >= 0 = harm of one reversal in units of one Ld event.
Bootstrap: paired over days, 10,000 replicates, mulberry32 seed 20261026, interpolation redone in every replicate.
Usage: python -I be_table.py <rows-be.ndjson> <out BREAKEVEN-TABLE.tsv> <out BREAKEVEN-CURVES.tsv> <out BREAKEVEN-SUMMARY.json>
"""
import json
import math
import sys

import numpy as np

CONFIGS = ("INS", "G2")
FAMS = ("EO", "EOHZ", "RCE")
ALPHAS = (0, 10, 20, 35, 50, 75, 100)
REPORT_ALPHAS = (10, 20, 35, 50, 75, 100)
METRICS = ("S", "R", "Ld", "La", "Ld120", "Lt60", "sumNet", "sumDA", "pDisp")
IS, IR, ILD, ILA = 0, 1, 2, 3
W_GRID = (0.25, 0.5, 1.0, 2.0, 4.0)
NBOOT = 10000
SEED = 20261026
M32 = 0xFFFFFFFF


def mulberry32(seed):
    state = [seed & M32]

    def rnd():
        a = (state[0] + 0x6D2B79F5) & M32
        state[0] = a
        t = ((a ^ (a >> 15)) * (1 | a)) & M32
        t = ((t + (((t ^ (t >> 7)) * (61 | t)) & M32)) & M32) ^ t
        return ((t ^ (t >> 14)) & M32) / 4294967296.0

    return rnd


def metric_values(r):
    be = r["be"]
    return (r["served"], r["visRev60"], be["Ld60"], r["late60First"], be["Ld120"], be["Lt60"], be["sumNet"] / 60000.0,
            be["sumDA"] / 60000.0, r["pDisp"])


def load_days(path):
    rows = {}
    for line in open(path, encoding="utf-8"):
        if line.strip():
            r = json.loads(line)
            rows[(r["cell"], r["config"], r["arm"])] = r
    days = sorted({k[0][:10] for k in rows})
    D = np.zeros((len(days), len(CONFIGS), len(FAMS), len(ALPHAS), len(METRICS)))
    for di, d in enumerate(days):
        for ci, cfg in enumerate(CONFIGS):
            for fi, fam in enumerate(FAMS):
                for ai, a in enumerate(ALPHAS):
                    arm = f"{fam}{a}"
                    acc = np.zeros(len(METRICS))
                    for win in ("w08", "w17"):
                        vals = []
                        for k in range(3):
                            key = (f"{d}-{win}-r{k}", cfg, arm)
                            if key not in rows:
                                raise SystemExit(f"missing row {key}")
                            vals.append(metric_values(rows[key]))
                        acc += np.mean(np.array(vals, dtype=float), axis=0)
                    D[di, ci, fi, ai] = acc
    return days, D


def decide(dR, dL):
    if dR == 0 and dL == 0:
        return "tie", None
    if dR <= 0 and dL <= 0:
        return "C-always", 0.0
    if dR >= 0 and dL >= 0:
        return "E-always", math.inf
    if dR < 0 < dL:
        return "C-if-w-above", dL / (-dR)
    return "C-if-w-below", (-dL) / dR


def matched(curves, ci, fe, ae):
    """RCE interpolated so that its served equals the comparator's served at alpha index ae. None if outside the grid."""
    target = curves[ci, fe, ae, IS]
    rce = curves[ci, FAMS.index("RCE")]
    for i in range(len(ALPHAS) - 1):
        s0, s1 = rce[i, IS], rce[i + 1, IS]
        if s0 <= target <= s1 and s1 > s0:
            f = (target - s0) / (s1 - s0)
            return ALPHAS[i] + f * (ALPHAS[i + 1] - ALPHAS[i]), rce[i] + f * (rce[i + 1] - rce[i])
        if s0 == target == s1:
            return float(ALPHAS[i]), rce[i].copy()
    return None


def compare(curves, ci, comp, fe_name, a):
    fe, ae = FAMS.index(fe_name), ALPHAS.index(a)
    E = curves[ci, fe, ae]
    if comp == "matched":
        m = matched(curves, ci, fe, ae)
        if m is None:
            return None
        alpha_c, C = m
    else:
        alpha_c, C = float(a), curves[ci, FAMS.index("RCE"), ae]
    return alpha_c, E, C


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    days, D = load_days(sys.argv[1])
    if len(days) != 10:
        raise SystemExit(f"expected 10 days, got {len(days)}")
    curves = D.mean(axis=0)
    # monotonicity of served in alpha (plan 6b)
    mono = {}
    for ci, cfg in enumerate(CONFIGS):
        for fi, fam in enumerate(FAMS):
            s = curves[ci, fi, :, IS]
            mono[f"{cfg}-{fam}"] = bool(np.all(np.diff(s) > 0))
    specs = [(cfg, comp, fe, a) for cfg in CONFIGS for comp, fe in (("matched", "EOHZ"), ("matched", "EO"),
                                                                       ("sameAlpha", "EOHZ"), ("sameAlpha", "EO"))
             for a in REPORT_ALPHAS]
    point = {}
    for spec in specs:
        cfg, comp, fe, a = spec
        point[spec] = compare(curves, CONFIGS.index(cfg), comp, fe, a)
    # bootstrap
    rnd = mulberry32(SEED)
    n = len(days)
    boot = {spec: {"cases": {}, "wstar": [], "excluded": 0, "outside": 0, "pref": [0] * len(W_GRID),
                   "casesLa": {}} for spec in specs}
    for _ in range(NBOOT):
        idx = [int(rnd() * n) for _ in range(n)]
        cb = D[idx].mean(axis=0)
        for spec in specs:
            cfg, comp, fe, a = spec
            res = compare(cb, CONFIGS.index(cfg), comp, fe, a)
            b = boot[spec]
            if res is None:
                b["outside"] += 1
                continue
            _, E, C = res
            dR, dL, dLa = C[IR] - E[IR], C[ILD] - E[ILD], C[ILA] - E[ILA]
            case, w = decide(dR, dL)
            b["cases"][case] = b["cases"].get(case, 0) + 1
            caseLa, _ = decide(dR, dLa)
            b["casesLa"][caseLa] = b["casesLa"].get(caseLa, 0) + 1
            if case in ("C-always", "E-always", "C-if-w-above"):
                b["wstar"].append(w)
            else:
                b["excluded"] += 1
            for wi, wv in enumerate(W_GRID):
                if wv * dR + dL < 0:
                    b["pref"][wi] += 1
    head = ["config", "comparison", "comparator", "alphaE", "alphaC", "S_E", "S_C", "dS", "R_E", "R_C", "dR", "Ld_E", "Ld_C",
            "dLd", "La_E", "La_C", "dLa", "caseLd", "wstarLd", "caseLa", "wstarLa", "boot_cases_Ld", "boot_wstarLd_p2.5",
            "boot_wstarLd_p50", "boot_wstarLd_p97.5", "boot_excluded", "boot_outside_grid"] + \
           [f"boot_prefC_w{w:g}" for w in W_GRID] + ["dLd120", "dLt60", "dNet_min", "dDA_min"]
    lines = ["\t".join(head)]
    summary = {"days": days, "nboot": NBOOT, "seed": SEED, "servedMonotoneInAlpha": mono, "rows": []}
    fmt = lambda x: "inf" if x == math.inf else ("" if x is None else f"{x:.3f}")
    for spec in specs:
        cfg, comp, fe, a = spec
        res = point[spec]
        b = boot[spec]
        valid = NBOOT - b["outside"]
        ws = np.array(b["wstar"], dtype=float)
        pct = [float(np.percentile(ws, q, method="inverted_cdf")) if len(ws) else None for q in (2.5, 50, 97.5)]
        cases = {k: round(v / NBOOT, 4) for k, v in sorted(b["cases"].items())}
        pref = [round(p / NBOOT, 4) for p in b["pref"]]
        if res is None:
            row = [cfg, comp, fe, str(a), "outside-grid"] + [""] * 16 + [json.dumps(cases)] + [fmt(p) for p in pct] + \
                  [str(b["excluded"]), str(b["outside"])] + [f"{p:.4f}" for p in pref] + [""] * 4
            lines.append("\t".join(row))
            summary["rows"].append({"spec": spec, "point": None, "bootCases": cases, "bootWstar": pct, "prefC": pref})
            continue
        alpha_c, E, C = res
        dS, dR, dL, dLa = C[IS] - E[IS], C[IR] - E[IR], C[ILD] - E[ILD], C[ILA] - E[ILA]
        case, w = decide(dR, dL)
        caseLa, wLa = decide(dR, dLa)
        row = [cfg, comp, fe, str(a), f"{alpha_c:.2f}", f"{E[IS]:.2f}", f"{C[IS]:.2f}", f"{dS:.2f}", f"{E[IR]:.2f}",
               f"{C[IR]:.2f}", f"{dR:.2f}", f"{E[ILD]:.2f}", f"{C[ILD]:.2f}", f"{dL:.2f}", f"{E[ILA]:.2f}", f"{C[ILA]:.2f}",
               f"{dLa:.2f}", case, fmt(w), caseLa, fmt(wLa), json.dumps(cases)] + [fmt(p) for p in pct] + \
              [str(b["excluded"]), str(b["outside"])] + [f"{p:.4f}" for p in pref] + \
              [f"{C[4] - E[4]:.2f}", f"{C[5] - E[5]:.2f}", f"{C[6] - E[6]:.2f}", f"{C[7] - E[7]:.2f}"]
        lines.append("\t".join(row))
        summary["rows"].append({"spec": spec, "alphaC": alpha_c, "dS": dS, "dR": dR, "dLd": dL, "dLa": dLa, "caseLd": case,
                                "wstarLd": w, "caseLa": caseLa, "wstarLa": wLa, "bootCases": cases,
                                "bootCasesLa": {k: round(v / NBOOT, 4) for k, v in sorted(b["casesLa"].items())},
                                "bootWstar": pct, "bootExcluded": b["excluded"], "bootOutside": b["outside"],
                                "prefC": dict(zip([str(x) for x in W_GRID], pref)), "validReplicates": valid,
                                "dLd120": C[4] - E[4], "dLt60": C[5] - E[5], "dNet_min": C[6] - E[6],
                                "dDA_min": C[7] - E[7]})
    with open(sys.argv[2], "x", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    # curves (descriptive)
    ch = ["config", "family", "alpha", "S", "R", "Ld", "La", "Ld120", "Lt60", "meanNetPerServed_min", "meanDAPerServed_min",
          "pDispPct"]
    cl = ["\t".join(ch)]
    for ci, cfg in enumerate(CONFIGS):
        for fi, fam in enumerate(FAMS):
            for ai, a in enumerate(ALPHAS):
                v = curves[ci, fi, ai]
                pdisp = float(np.mean(100.0 * D[:, ci, fi, ai, 8] / D[:, ci, fi, ai, 0]))
                cl.append("\t".join([cfg, fam, str(a)] + [f"{v[k]:.2f}" for k in range(6)] +
                                    [f"{v[6] / v[0]:.3f}", f"{v[7] / v[0]:.3f}", f"{pdisp:.3f}"]))
    with open(sys.argv[3], "x", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(cl) + "\n")
    with open(sys.argv[4], "x", encoding="utf-8", newline="\n") as f:
        json.dump(summary, f, ensure_ascii=False, indent=1, default=lambda x: None if x == math.inf else x)
        f.write("\n")
    print("served monotone in alpha:", mono)
    print("\n".join(lines))
    print("\n".join(cl))


if __name__ == "__main__":
    main()
