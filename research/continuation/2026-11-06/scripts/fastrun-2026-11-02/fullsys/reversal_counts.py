"""Number of ETA reversals (owner's request 2026-11-04), next to the report's visRev60.

Per served rider, from the published drop-ETA sequence (consecutive repeats dropped; the Runner's at-drop revision left
out, as compare_drop_sequences.py does): keep the changes z of at least 60 s (metrics_sh.hasReversal's threshold); the
number of reversals is the number of consecutive kept changes with opposite signs. visRev60 counts riders with at least
one; reversalCount60 counts the reversals themselves. Per day = sum over a day's two windows of the mean over its three
samples (F14: total / 42), the report's day unit.

Usage: python -I -B reversal_counts.py emulator <drop-sequences.tsv> [--out <out.json>]
       python -I -B reversal_counts.py real <drop-sequences.tsv for the served flags> --root <runs> [...] [--out <out.json>]
"""
import argparse
import collections
import csv
import json
import pathlib
import sys

THRESHOLD = 60_000


def reversals(sequence, threshold=THRESHOLD):
    count, last = 0, 0
    for a, b in zip(sequence, sequence[1:]):
        z = b - a
        if abs(z) < threshold:
            continue
        sign = 1 if z > 0 else -1
        if last != 0 and sign != last:
            count += 1
        last = sign
    return count


def summarize(per_role):
    out = {}
    for role, counts in sorted(per_role.items()):
        values = list(counts.values())
        histogram = collections.Counter(min(c, 3) for c in values)
        cells = len({cell for cell, _ in counts.keys()})
        out[role] = {
            "servedRiders": len(values),
            "visRev60": sum(c > 0 for c in values),
            "reversalCount60": sum(values),
            "ridersWith": {"0": histogram[0], "1": histogram[1], "2": histogram[2], "3+": histogram[3]},
            "maxPerRider": max(values) if values else 0,
            "cells": cells,
        }
        if cells == 84:   # F14: 14 days x 2 windows x 3 samples
            out[role]["perDay"] = {"visRev60": round(out[role]["visRev60"] / 42, 2),
                                   "reversalCount60": round(out[role]["reversalCount60"] / 42, 2)}
    return out


def read_sequences(path):
    per_role = collections.defaultdict(dict)
    with open(path, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            if row["served"] == "1":
                per_role[row["role"]][(row["cell"], row["requestId"])] = json.loads(row["dropSequence"])
    return per_role


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("emulator", "real"))
    parser.add_argument("sequences")
    parser.add_argument("--root", action="append", default=[])
    parser.add_argument("--out")
    args = parser.parse_args()
    mode = args.mode
    sequences = read_sequences(args.sequences)
    if mode == "emulator":
        per_role = {role: {k: reversals(s) for k, s in riders.items()} for role, riders in sequences.items()}
    elif mode == "real":
        sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
        import compare_drop_sequences as D
        per_role = collections.defaultdict(dict)
        for root in map(pathlib.Path, args.root):
            for folder in sorted(p for p in root.iterdir() if (p / "summary.json").exists()):
                role = next((r for r in sequences if folder.name.startswith(r + "-d")), None)
                if role is None:
                    continue
                cell = folder.name[len(role) + 1:]
                real, _ = D.real_sequences(folder)
                for (c, rider), _seq in sequences[role].items():
                    if c == cell and rider in real:
                        per_role[role][(cell, rider)] = reversals(real[rider])
    else:
        raise SystemExit(__doc__)
    summary = summarize(per_role)
    print(json.dumps(summary, indent=1))
    if args.out:
        out = pathlib.Path(args.out)
        if out.exists():
            raise SystemExit(f"refused: {out} exists")
        out.write_text(json.dumps(summary, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
