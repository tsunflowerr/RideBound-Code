"""Q4 (2026-11-06): for every served rider whose published drop-ETA sequence differs between the sealed prediction and
the real run (small cells, the big F14 cell excluded), is the first difference at an instant where one side published
two or more drop ETAs in the same millisecond? Reads only. Usage: python -I -B q4_same_instant.py <q34-details.json>"""
import collections
import csv
import gzip
import json
import pathlib
import sys

DET = json.load(open(sys.argv[1], encoding="utf-8"))
EVID = pathlib.Path(sys.argv[1]).parent
D = pathlib.Path("E:/RideBoundData/research")
TIMED = {"f14": D / "fullsys-f14-v2/predictions-timed/timed-adapter.tsv.gz",
         "f16": D / "fullsys-f16-v2/predictions-timed/timed-adapter.tsv.gz"}
RUNS = {"f14": EVID / "f14/runs/final", "f16": EVID / "f16/runs/final-f16"}
for panel, cells in DET.items():
    want = {(c["role"], c["cell"], r["requestId"]) for c in cells if c["cell"] != "d20191015-vf-r1-w08"
            for r in c["riders"] if r.get("servedEmulator") and "aligned" in r}
    emu = {}
    with gzip.open(TIMED[panel], "rt", encoding="utf-8", newline="") as h:
        for row in csv.DictReader(h, delimiter="\t"):
            k = (row["role"], row["cell"], row["requestId"])
            if k in want:
                emu[k] = [tuple(x) for x in json.loads(row["timedDropSequence"])]
    tally, examples = collections.Counter(), []
    by_cell = collections.defaultdict(set)
    for k in want:
        by_cell[(k[0], k[1])].add(k[2])
    for (role, cell), riders in sorted(by_cell.items()):
        ev = json.load(gzip.open(RUNS[panel] / f"{role}-{cell}" / "evidence.json.gz", "rt", encoding="utf-8"))
        real = collections.defaultdict(list)
        for t in ev["audit"]["table"]:
            if t[0] in riders and t[1] == "drop":
                real[t[0]].append((t[2], int(t[4]), int(t[7])))
        for r in riders:
            rs = [(pub, p) for _, pub, p in sorted(real[r])]
            es = emu[(role, cell, r)]
            n = min(len(rs), len(es))
            i = next((j for j in range(n) if rs[j] != es[j]), n)
            if i == n and len(rs) == len(es):
                tally["no difference in raw (pub, p) lists"] += 1
                continue
            pub = min(x[0] for x in (rs[i:i + 1] + es[i:i + 1]))
            same = sum(1 for x in rs if x[0] == pub) >= 2 or sum(1 for x in es if x[0] == pub) >= 2
            tally["first difference at a same-millisecond double publication" if same else "other"] += 1
            if not same and len(examples) < 5:
                examples.append((cell, role, r[:12], i, rs[max(0, i - 1):i + 2], es[max(0, i - 1):i + 2]))
    print(panel, dict(tally))
    for e in examples:
        print("   other:", e)
