"""Time-weighted ETA error next to the promise-stability metrics, per arm (ADR-089 GĐ4/GĐ6, owner's request 2026-11-04).

Definitions, fixed before any real job of the final comparison:
  For a served rider i: t0 = publication time of the initial promise, A = actual drop time, and the published drop ETA
  p(t) is a step function: p0 from t0, then p_k from the publication time of revision k (consecutive equal values do
  not change it). Revisions published at or after A are ignored (the Runner publishes one at the drop itself).
    E_i = (1 / (A - t0)) * integral_{t0}^{A} |p(t) - A| dt     time-weighted ETA error (ms; reported in minutes)
    I_i = |p0 - A|                                               error of the initial promise
  Stability metrics from the same sequence of p values (the report's rules, as reversal_counts.py and
  compare_drop_sequences.py): changes below 60 s ignored; reversals = consecutive kept changes of opposite sign;
  visRev60 = riders with at least one; TV5 = sum of |p - shown| for |p - shown| >= 5 s with shown updating.
  Per day (F14, 84 cells = 14 days x 2 windows x 3 samples): total / 42.
Sources of (pub, p): emulator = emulate_timed.js (tEval of the publishing decision); real = the drop rows of the audit
ledger table (compare_drop_sequences.py's reader) with A and served from compare_fullsys.py's real-riders TSV.

Usage: python -I -B eta_metrics.py emulator <timed.tsv> [--out <json>]
       python -I -B eta_metrics.py real --riders <real-riders.tsv> --root <runs> [--root ...] [--out <json>]
       python -I -B eta_metrics.py compare <sealed timed.tsv> --seal <seal file> --riders <real-riders.tsv>
                                   --root <runs> [--root ...] --out <json>
                                   rider by rider: the same (pub, p) sequence, t0 and A; then both summaries
       python -I -B eta_metrics.py selftest
"""
import argparse
import collections
import csv
import json
import pathlib
import statistics
import sys

MIN = 60_000


def tw_error(timed, t0, a):
    """timed: [(pub, p), ...] starting with (t0, p0), pub non-decreasing."""
    if not timed or timed[0][0] != t0 or a <= t0:
        raise ValueError(f"bad rider: t0 {t0}, A {a}, first {timed[:1]}")
    total = 0
    for k, (pub, p) in enumerate(timed):
        if pub >= a:
            break
        end = min(timed[k + 1][0], a) if k + 1 < len(timed) else a
        if end < pub:
            raise ValueError("publication times go back")
        total += abs(p - a) * (end - pub)
    return total / (a - t0)


def reversals(values, threshold=60_000):
    count, last = 0, 0
    for x, y in zip(values, values[1:]):
        z = y - x
        if abs(z) < threshold:
            continue
        sign = 1 if z > 0 else -1
        if last and sign != last:
            count += 1
        last = sign
    return count


def tv5(values):
    shown, total = values[0], 0
    for p in values[1:]:
        if abs(p - shown) >= 5_000:
            total += abs(p - shown)
            shown = p
    return total


def rider_metrics(timed, t0, a):
    kept = [(pub, p) for pub, p in timed if pub < a]
    values = [p for _, p in kept]
    rev = reversals(values)
    return {"E": tw_error(kept, t0, a), "I": abs(kept[0][1] - a), "rev": rev, "tv5": tv5(values)}


def pct(values, q):
    values = sorted(values)
    return values[min(len(values) - 1, int(q * len(values)))]


def summarize(per_role):
    out = {}
    for role, riders in sorted(per_role.items()):
        cells = len({cell for cell, _ in riders})
        e = [m["E"] / MIN for m in riders.values()]
        i = [m["I"] / MIN for m in riders.values()]
        s = {
            "cells": cells, "servedRiders": len(riders),
            "etaErrorMeanMin": round(statistics.fmean(e), 3), "etaErrorMedianMin": round(statistics.median(e), 3),
            "etaErrorP90Min": round(pct(e, 0.9), 3),
            "shareEtaError2Min": round(sum(x >= 2 for x in e) / len(e), 4),
            "shareEtaError5Min": round(sum(x >= 5 for x in e) / len(e), 4),
            "initialErrorMeanMin": round(statistics.fmean(i), 3),
            "visRev60": sum(m["rev"] > 0 for m in riders.values()),
            "reversalCount60": sum(m["rev"] for m in riders.values()),
            "tv5Hours": round(sum(m["tv5"] for m in riders.values()) / 3_600_000, 2),
        }
        if cells == 84:
            s["perDay"] = {k: round(s[k] / 42, 2) for k in ("visRev60", "reversalCount60", "tv5Hours")}
        out[role] = s
    return out


def from_emulator(path):
    """role -> {(cell, rider): (timed, t0, A)} for served riders."""
    per_role = collections.defaultdict(dict)
    import gzip   # 2026-11-04: the 84-cell export is timed-adapter.tsv.gz (same rows)
    opener = gzip.open if str(path).endswith(".gz") else open
    with opener(path, "rt", encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            if row["served"] != "1":
                continue
            timed = [tuple(x) for x in json.loads(row["timedDropSequence"])]
            per_role[row["role"]][(row["cell"], row["requestId"])] = (timed, int(row["t0"]), int(row["A"]))
    return per_role


def metrics_of(sequences):
    return {role: {k: rider_metrics(*v) for k, v in riders.items()} for role, riders in sequences.items()}


def from_real(riders_tsv, roots):
    here = pathlib.Path(__file__).resolve().parent
    sys.path.insert(0, str(here))
    import compare_drop_sequences as D   # its reader patches audit_vep for gzip and big frames
    truth = {}
    with open(riders_tsv, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            truth[(row["role"], row["cell"], row["requestId"])] = row
    roles = sorted({k[0] for k in truth}, key=len, reverse=True)
    per_role = collections.defaultdict(dict)
    for root in map(pathlib.Path, roots):
        for folder in sorted(p for p in root.iterdir() if (p / "summary.json").exists()):
            if json.loads((folder / "summary.json").read_text(encoding="utf-8")).get("status") != "pass":
                continue
            role = next((r for r in roles if folder.name.startswith(r + "-d")), None)
            if role is None:
                continue
            cell = folder.name[len(role) + 1:]
            table, _ = D.audit_vep.audit(folder / "transcript-00.ndjson.gz")
            rows = collections.defaultdict(list)
            for r, coord, seq, kind, pub, v, e, p, x, cc, z, closed in table:
                if coord == "drop":
                    rows[r].append((seq, kind, int(pub), int(p)))
            for rider, entries in rows.items():
                t = truth.get((role, cell, rider))
                if t is None or t["served"] != "1":
                    continue
                entries.sort()
                if entries[0][1] != "initialPromise" or entries[0][2] != int(t["t0"]):
                    raise SystemExit(f"{folder.name} {rider}: initial promise does not match t0")
                a = int(t["A"])
                timed = []
                for _, _, pub, p in entries:
                    if pub < a and (not timed or p != timed[-1][1]):   # the at-drop revision (pub = A) left out
                        timed.append((pub, p))
                per_role[role][(cell, rider)] = (timed, int(t["t0"]), a)
    return per_role


def compare(emulator, real):
    out = []
    for role in sorted(set(emulator) | set(real)):
        cells = sorted({c for c, _ in emulator.get(role, {})} | {c for c, _ in real.get(role, {})})
        for cell in cells:
            emu = {r: v for (c, r), v in emulator.get(role, {}).items() if c == cell}
            rea = {r: v for (c, r), v in real.get(role, {}).items() if c == cell}
            if not rea:
                continue   # cell-arm not run
            differing = sorted(r for r in set(emu) | set(rea) if emu.get(r) != rea.get(r))
            out.append({"role": role, "cell": cell, "servedEmulator": len(emu), "servedReal": len(rea),
                        "differing": len(differing), "first": differing[:3], "exact": not differing})
    return out


def selftest():
    # owner's example (ChatGPT): t0 10:00, A 10:35; A-case p0 10:30 then 10:35 from 10:05 -> 5*5/35 = 0.714 min
    m = 60_000
    a = rider_metrics([(0, 30 * m), (5 * m, 35 * m)], 0, 35 * m)
    b = rider_metrics([(0, 30 * m), (30 * m, 35 * m)], 0, 35 * m)
    assert abs(a["E"] / m - 25 / 35) < 1e-12 and abs(b["E"] / m - 150 / 35) < 1e-12, (a, b)
    assert a["rev"] == 0 and a["tv5"] == 5 * m and a["I"] == 5 * m
    c = rider_metrics([(0, 30 * m), (2 * m, 33 * m), (4 * m, 31 * m), (6 * m, 34 * m), (40 * m, 40 * m)], 0, 35 * m)
    assert c["rev"] == 2 and abs(c["E"] - (5 * m * 2 * m + 2 * m * 2 * m + 4 * m * 2 * m + 1 * m * 29 * m) / (35 * m)) < 1e-6, c
    print("selftest ok: example A", round(a["E"] / m, 2), "min, example B", round(b["E"] / m, 2), "min")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("emulator", "real", "compare", "selftest"))
    parser.add_argument("timed", nargs="?")
    parser.add_argument("--riders")
    parser.add_argument("--seal")
    parser.add_argument("--root", action="append", default=[])
    parser.add_argument("--out")
    args = parser.parse_args()
    if args.mode == "selftest":
        selftest()
        return 0
    if args.mode == "compare":
        import hashlib
        digest = hashlib.sha256(pathlib.Path(args.timed).read_bytes()).hexdigest()
        if not any(l.startswith(digest) for l in pathlib.Path(args.seal).read_text(encoding="utf-8").splitlines()):
            raise SystemExit(f"{args.timed} is not in {args.seal}")
        out = pathlib.Path(args.out)
        if out.exists():
            raise SystemExit(f"refused: {out} exists")
        emulator, real = from_emulator(args.timed), from_real(args.riders, args.root)
        rows = compare(emulator, real)
        ran = {(r["role"], r["cell"]) for r in rows}
        emulator = {role: {k: v for k, v in riders.items() if (role, k[0]) in ran} for role, riders in emulator.items()}
        # 2026-11-04 fix after the seal: arms with no job yet are left out of the summary (an empty arm divided by 0).
        # The rider-by-rider comparison above is unchanged.
        emulator = {role: riders for role, riders in emulator.items() if riders}
        summary = {"cellArms": len(rows), "exact": sum(r["exact"] for r in rows), "rows": rows,
                   "emulator": summarize(metrics_of(emulator)), "real": summarize(metrics_of(real))}
        print(json.dumps(summary, indent=1))
        out.write_text(json.dumps(summary, indent=1), encoding="utf-8")
        return 0 if rows and summary["exact"] == len(rows) else 1
    per_role = from_emulator(args.timed) if args.mode == "emulator" else from_real(args.riders, args.root)
    summary = summarize(metrics_of(per_role))
    print(json.dumps(summary, indent=1))
    if args.out:
        out = pathlib.Path(args.out)
        if out.exists():
            raise SystemExit(f"refused: {out} exists")
        out.write_text(json.dumps(summary, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
