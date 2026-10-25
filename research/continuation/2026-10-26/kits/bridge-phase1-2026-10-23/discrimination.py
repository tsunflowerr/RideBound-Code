"""How different are the phase-1 arms from the arms already bridged? (prediction files only; no job output)

A new arm that predicts the same riders as a bridged arm would make its bridge test weak. Per pair, counts the riders
(cell, requestId) whose row differs in any of t0, p0, A, served, sumAbsC, nonzeroC, netC, maxAbsC, shared.
Usage: python -B discrimination.py
"""
import csv
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
BRIDGE = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
FIELDS = ("t0", "p0", "A", "served", "sumAbsC", "nonzeroC", "netC", "maxAbsC", "shared")


def load(path):
    rows = {}
    with open(path, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            rows[(row["role"], row["cell"], row["requestId"])] = tuple(row[f] for f in FIELDS)
    return rows


rows = load(HERE / "emulator-riders-p1.tsv")
for name in ("emulator-riders-2000.tsv", "emulator-riders-elmi-2000.tsv", "emulator-riders-g2-2000.tsv"):
    rows.update(load(BRIDGE / name))
PAIRS = [("EO35", "EOHZ35"), ("DLS-120", "DLHZ-120"), ("G2-EO35", "G2-EOHZ35"), ("G2-DLS-120", "G2-EOHZ35"),
         ("G2K3-U", "G2-U"), ("G2K3-RCE35", "G2-RCE35"), ("G2K3-EOHZ35", "G2-EOHZ35")]
for new, old in PAIRS:
    a = {(c, r): v for (role, c, r), v in rows.items() if role == new}
    b = {(c, r): v for (role, c, r), v in rows.items() if role == old}
    keys = set(a) | set(b)
    differ = sum(1 for k in keys if a.get(k) != b.get(k))
    cells = len({c for c, _ in keys if a.get((c, _)) != b.get((c, _))})
    print(f"{new:12s} vs {old:10s}: riders differ {differ}/{len(keys)} in {cells}/11 cells")
