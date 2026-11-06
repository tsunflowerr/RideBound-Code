"""Answers to the company machine's Q3/Q4 (2026-11-06): what exactly differs between the sealed adapter predictions and
the real runs, rider by rider, in every cell-arm where some side of the comparison was not exact. Reads only.

Real side from each job's evidence.json.gz (audit.table rows [r, coord, seq, kind, pub, v, e, p, x, cc, z, closed]):
  values  = compare_drop_sequences.real_sequences: drop rows sorted, first must be initialPromise, the at-drop revision
            (last row, revision, pub == p) left out, consecutive repeats dropped;
  timed   = eta_metrics.from_real: (pub, p) with pub < A, consecutive equal p dropped, for riders served on the real side.
Emulator side: the sealed drop-sequences TSV (values) and the sealed timed TSV (timed).
Counts follow the report's conventions (sequences: riders served in the prediction that exist on the real side) and are
checked against the vast reports (sequences.json / eta.json "differing") cell by cell.
Usage: python -I -B q34_details.py <evidence root> <results dir> <out.json>
"""
import collections
import csv
import gzip
import json
import pathlib
import sys

ROOT, RESULTS, OUT = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])
D = pathlib.Path("E:/RideBoundData/research")
PANELS = {
    "f14": dict(runs=ROOT / "f14/runs/final", seq=D / "fullsys-f14-v2/predictions/fullsys-f14-drop-sequences.tsv",
                timed=D / "fullsys-f14-v2/predictions-timed/timed-adapter.tsv.gz",
                riders=D / "fullsys-f14-v2/runs-linux-2026-11-05/extracted/runs/report/riders-real-riders.tsv",
                pred=D / "fullsys-f14-v2/predictions/fullsys-f14-predictions.tsv"),
    "f16": dict(runs=ROOT / "f16/runs/final-f16", seq=D / "fullsys-f16-v2/predictions/fullsys-f16-drop-sequences.tsv",
                timed=D / "fullsys-f16-v2/predictions-timed/timed-adapter.tsv.gz",
                riders=D / "fullsys-f16-v2/runs-linux-2026-11-06/extracted/runs/report-f16/riders-real-riders.tsv",
                pred=D / "fullsys-f16-v2/predictions/fullsys-f16-predictions.tsv"),
}
ROLES = ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N")
WATCH = {("f14", "FS-G2-SHHZ71-N", "d20191015-vf-r1-w08", "rq-33085454a98f2e7b21eb951901717ac7d364a7bf8aa3925e389196749c6d7933"),
         ("f14", "FS-G2-SHHZ71-N", "d20191218-vf-r1-w08", "rq-88620aac12f686ef0fa9da1d162936ef6b2aebae0b38ff6531dc51bde9b39ec7")}


def dedupe(values):
    out = []
    for v in values:
        if not out or v != out[-1]:
            out.append(v)
    return out


def has_reversal(s, th=60_000):
    last = 0
    for a, b in zip(s, s[1:]):
        z = b - a
        if abs(z) < th:
            continue
        sign = 1 if z > 0 else -1
        if last and sign != last:
            return True
        last = sign
    return False


def reversals(s, th=60_000):
    count, last = 0, 0
    for a, b in zip(s, s[1:]):
        z = b - a
        if abs(z) < th:
            continue
        sign = 1 if z > 0 else -1
        if last and sign != last:
            count += 1
        last = sign
    return count


def tv5(s):
    shown, total = s[0], 0
    for p in s[1:]:
        if abs(p - shown) >= 5_000:
            total += abs(p - shown)
            shown = p
    return total


def tw_error(timed, t0, a):
    total = 0
    for k, (pub, p) in enumerate(timed):
        if pub >= a:
            break
        end = min(timed[k + 1][0], a) if k + 1 < len(timed) else a
        total += abs(p - a) * (end - pub)
    return total / (a - t0)


def read_tsv(path, roles, cells, value):
    out = {}
    opener = gzip.open if str(path).endswith(".gz") else open
    with opener(path, "rt", encoding="utf-8", newline="") as h:
        for row in csv.DictReader(h, delimiter="\t"):
            role = row.get("role") or row.get("arm")
            if role in roles and (role, row["cell"]) in cells:
                out[(role, row["cell"], row["requestId"])] = value(row)
    return out


def seq_diff(e, r):
    import difflib
    n = min(len(e), len(r))
    first = next((i for i in range(n) if e[i] != r[i]), n)
    common = [abs(e[i] - r[i]) for i in range(n)]
    # alignment: values the real side has in addition (insert), lacks (delete), or has with another value (replace)
    ops = collections.Counter()
    replaced = []
    for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, e, r, autojunk=False).get_opcodes():
        if tag == "insert":
            ops["realExtra"] += j2 - j1
        elif tag == "delete":
            ops["realMissing"] += i2 - i1
        elif tag == "replace":
            k = min(i2 - i1, j2 - j1)
            replaced += [abs(e[i1 + t] - r[j1 + t]) for t in range(k)]
            ops["replaced"] += k
            ops["realExtra"] += max(0, (j2 - j1) - (i2 - i1))
            ops["realMissing"] += max(0, (i2 - i1) - (j2 - j1))
    return {"lenEmulator": len(e), "lenReal": len(r), "firstIndex": first,
            "maxAbsDiffMs": max(common) if common else None, "sameLength": len(e) == len(r),
            "aligned": dict(ops), "maxReplacedDiffMs": max(replaced) if replaced else 0}


report = {}
for panel, P in PANELS.items():
    folders = sorted(p for p in P["runs"].iterdir() if (p / "evidence.json.gz").exists())
    cells = set()
    for f in folders:
        role = next(r for r in ROLES if f.name.startswith(r + "-d"))
        cells.add((role, f.name[len(role) + 1:]))
    emu_vals = read_tsv(P["seq"], ROLES, cells, lambda r: (r["served"] == "1", json.loads(r["dropSequence"])))
    emu_timed = read_tsv(P["timed"], ROLES, cells,
                         lambda r: (r["served"] == "1", [tuple(x) for x in json.loads(r["timedDropSequence"])],
                                    int(r["t0"]), int(r["A"])))
    truth = read_tsv(P["riders"], ROLES, cells, lambda r: r)
    pred = read_tsv(P["pred"], ROLES, cells, lambda r: r)
    vast = {name: {(c.get("role") or c.get("arm"), c["cell"]): c
                   for c in (json.load(open(RESULTS / f"{panel}-report/{name}.json")).get("results")
                             or json.load(open(RESULTS / f"{panel}-report/{name}.json")).get("rows"))}
            for name in ("sequences", "eta")}
    out_cells = []
    for folder in folders:
        role = next(r for r in ROLES if folder.name.startswith(r + "-d"))
        cell = folder.name[len(role) + 1:]
        ev = json.load(gzip.open(folder / "evidence.json.gz", "rt", encoding="utf-8"))
        rows = collections.defaultdict(list)
        allrows = collections.defaultdict(list)
        for r, coord, seq, kind, pub, v, e, p, x, cc, z, closed in ev["audit"]["table"]:
            allrows[r].append((coord, seq, kind, int(pub), int(p), cc))
            if coord == "drop":
                rows[r].append((seq, kind, int(pub), int(p)))
        real_vals, real_timed = {}, {}
        for r, entries in rows.items():
            entries.sort()
            if entries[0][1] != "initialPromise":
                raise SystemExit(f"{folder.name} {r}: no initial promise first")
            ent = entries[:-1] if len(entries) > 1 and entries[-1][1] == "revision" and entries[-1][2] == entries[-1][3] else entries
            real_vals[r] = dedupe([p for _, _, _, p in ent])
            t = truth.get((role, cell, r))
            if t is not None and t["served"] == "1":
                a, timed = int(t["A"]), []
                for _, _, pub, p in entries:
                    if pub < a and (not timed or p != timed[-1][1]):
                        timed.append((pub, p))
                real_timed[r] = (timed, int(t["t0"]), a)
        # values (report convention: served in the prediction, present on the real side)
        emu_all = {r: (sv, s) for (ro, c, r), (sv, s) in emu_vals.items() if ro == role and c == cell}
        emu_served = {r: s for r, (sv, s) in emu_all.items() if sv}
        diffs, eff = [], collections.Counter()
        for r, s in emu_served.items():
            if r not in real_vals:
                eff["emulatorServedMissingOnReal"] += 1
                continue
            rv = real_vals[r]
            eff["visRev60Emulator"] += has_reversal(s); eff["visRev60Real"] += has_reversal(rv)
            eff["reversalsEmulator"] += reversals(s); eff["reversalsReal"] += reversals(rv)
            eff["tv5Emulator"] += tv5(s); eff["tv5Real"] += tv5(rv)
        # compare_drop_sequences.py's own count: every rider of either side, served or not
        for r in sorted(set(real_vals) | set(emu_all)):
            s, rv = emu_all.get(r, (None, None))[1], real_vals.get(r)
            if s != rv:
                d = dict(requestId=r, servedEmulator=emu_all.get(r, (None,))[0], onReal=r in real_vals, onEmulator=r in emu_all)
                if s is not None and rv is not None:
                    d.update(seq_diff(s, rv))
                diffs.append(d)
        # all riders served on the real side (Q3.3)
        real_served = [r for (ro, c, r), t in truth.items() if ro == role and c == cell and t["served"] == "1"]
        eff["realServed"] = len(real_served)
        eff["visRev60RealAllRealServed"] = sum(has_reversal(real_vals[r]) for r in real_served if r in real_vals)
        eff["reversalsRealAllRealServed"] = sum(reversals(real_vals[r]) for r in real_served if r in real_vals)
        # timed
        emu_t = {r: v for (ro, c, r), v in emu_timed.items() if ro == role and c == cell and v[0]}
        tdiff, emean, rmean, both, perr = collections.Counter(), [], [], 0, []
        for r in sorted(set(emu_t) | set(real_timed)):
            ev_, rv_ = emu_t.get(r), real_timed.get(r)
            if ev_ is None or rv_ is None:
                tdiff["onlyOneSide"] += 1
                continue
            _, et, et0, ea = ev_
            rt, rt0, ra = rv_
            ke = [(pub, p) for pub, p in et if pub < ea]
            Ee, Er = tw_error(ke, et0, ea), tw_error(rt, rt0, ra)
            emean.append(Ee); rmean.append(Er); both += 1
            if (ke, et0, ea) != (rt, rt0, ra):
                if [p for _, p in ke] == [p for _, p in rt] and (et0, ea) == (rt0, ra):
                    tdiff["publicationTimesOnly"] += 1
                else:
                    tdiff["valuesOrT0OrA"] += 1
                perr.append(abs(Ee - Er) / 60_000)
        vs, ve = vast["sequences"].get((role, cell), {}), vast["eta"].get((role, cell), {})
        rec = {"panel": panel, "role": role, "cell": cell, "sequenceDiffers": len(diffs),
               "vastSequenceDiffers": vs.get("differing"), "effects": dict(eff),
               "timedDiffers": dict(tdiff), "vastTimedDiffers": ve.get("differing"),
               "etaMeanMinEmulator": round(sum(emean) / len(emean) / 60_000, 5) if emean else None,
               "etaMeanMinReal": round(sum(rmean) / len(rmean) / 60_000, 5) if rmean else None,
               "etaMaxRiderAbsDiffMin": round(max(perr), 4) if perr else 0.0,
               "riders": diffs}
        if vs.get("differing") is not None and vs.get("differing") != len(diffs):
            raise SystemExit(f"{panel} {role} {cell}: {len(diffs)} sequence differences, vast report {vs.get('differing')}")
        for key in WATCH:
            if key[:3] == (panel, role, cell):
                r = key[3]
                rec.setdefault("watched", {})[r] = {
                    "realRows": sorted(allrows[r], key=lambda x: (x[0], x[1])),
                    "realFields": {k: truth[(role, cell, r)][k] for k in ("t0", "p0", "A", "served", "sumAbsC", "nonzeroC", "netC", "maxAbsC")}
                    if (role, cell, r) in truth else None,
                    "predictedFields": {k: pred[(role, cell, r)][k] for k in ("t0", "p0", "A", "served", "sumAbsC", "nonzeroC", "netC", "maxAbsC")}
                    if (role, cell, r) in pred else None,
                    "emulatorTimed": emu_t.get(r, (None, None))[1], "realTimed": real_timed.get(r, (None,))[0]}
        out_cells.append(rec)
    report[panel] = out_cells
with open(OUT, "x", encoding="utf-8", newline="\n") as h:
    json.dump(report, h, indent=1)
for panel, cells in report.items():
    tot = collections.Counter()
    for c in cells:
        big = c["cell"] == "d20191015-vf-r1-w08" and c["role"] == "FS-G2-SHHZ71-N"
        for k, v in c["effects"].items():
            tot[("big " if big else "") + k] += v
        tot[("big " if big else "") + "sequenceDiffers"] += c["sequenceDiffers"]
        tot[("big " if big else "") + "seqCells"] += c["sequenceDiffers"] > 0
        for r in c["riders"]:
            kind = ("onlyOneSide" if "sameLength" not in r else "sameLength" if r["sameLength"] else "lengthDiffers")
            tot[("big " if big else "") + kind] += 1
            tot[("big " if big else "") + ("served" if r["servedEmulator"] else "notServedInPrediction")] += 1
        for k, v in c["timedDiffers"].items():
            tot[("big " if big else "") + "timed " + k] += v
    print(panel, dict(sorted(tot.items())))
print("wrote", OUT)
