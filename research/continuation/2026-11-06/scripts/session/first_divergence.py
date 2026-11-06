"""Earliest publication where the real run and the sealed adapter prediction differ, for one cell-arm.
Real: evidence.json.gz audit.table drop rows (eta_metrics.from_real's form: dedupe, pub < A).
Emulator: timed-adapter.tsv.gz rows. Usage: python -I -B first_divergence.py <evidence.json.gz> <timed.tsv.gz> <role> <cell> <riders-real.tsv>
"""
import collections
import csv
import gzip
import json
import sys

evidence, timed_path, role, cell, riders_tsv = sys.argv[1:]
truth = {}
with open(riders_tsv, encoding="utf-8", newline="") as h:
    for row in csv.DictReader(h, delimiter="\t"):
        if row["role"] == role and row["cell"] == cell:
            truth[row["requestId"]] = row
ev = json.load(gzip.open(evidence, "rt", encoding="utf-8"))
rows = collections.defaultdict(list)
for r, coord, seq, kind, pub, v, e, p, x, cc, z, closed in ev["audit"]["table"]:
    if coord == "drop":
        rows[r].append((seq, kind, int(pub), int(p)))
real = {}
for rider, entries in rows.items():
    t = truth.get(rider)
    if t is None or t["served"] != "1":
        continue
    entries.sort()
    a = int(t["A"])
    timed = []
    for _, _, pub, p in entries:
        if pub < a and (not timed or p != timed[-1][1]):
            timed.append((pub, p))
    real[rider] = (timed, int(t["t0"]), a)
emu = {}
with gzip.open(timed_path, "rt", encoding="utf-8", newline="") as h:
    for row in csv.DictReader(h, delimiter="\t"):
        if row["role"] == role and row["cell"] == cell and row["served"] == "1":
            emu[row["requestId"]] = ([tuple(x) for x in json.loads(row["timedDropSequence"])], int(row["t0"]), int(row["A"]))
print(f"served real {len(real)}, emulator {len(emu)}; only real {len(set(real) - set(emu))}, only emulator {len(set(emu) - set(real))}")
firsts = []
for r in set(real) | set(emu):
    a, b = real.get(r), emu.get(r)
    if a == b:
        continue
    if a is None or b is None:
        t0 = (a or b)[1]
        firsts.append((t0, r, "served on one side only", a is not None))
        continue
    sa, sb = a[0], b[0]
    k = 0
    while k < min(len(sa), len(sb)) and sa[k] == sb[k]:
        k += 1
    if k < min(len(sa), len(sb)):
        t = min(sa[k][0], sb[k][0])
        firsts.append((t, r, f"revision {k}: real {sa[k]} emulator {sb[k]}", None))
    else:
        t = (sa[k][0] if k < len(sa) else sb[k][0]) if k < max(len(sa), len(sb)) else min(a[2], b[2])
        firsts.append((t, r, f"length real {len(sa)} emulator {len(sb)}; A real {a[2]} emulator {b[2]}", None))
firsts.sort()
print(f"riders differing {len(firsts)}")
for f in firsts[:12]:
    print(f)
