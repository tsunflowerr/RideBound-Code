"""Per-rider bridge comparison: real RideBound main jobs vs the sealed emulator predictions.

Fields compared per accepted rider (the emulator calibration rule, calibrate.js:41): t0, p0, A, served, sumAbsC,
nonzeroC, netC, maxAbsC, shared; plus the accepted-rider set of each cell-arm. A cell-arm is exact when both sides
accept the same riders and every field is equal. Only finished main jobs with summary status "pass" are read.
The emulator file must match EMULATOR-PREDICTIONS.sha256 (written before any main job had finished).

Usage: python -B bridge_compare.py <out-prefix>     writes <prefix>-real-riders.tsv and <prefix>-compare.json
"""

from __future__ import annotations

import csv
import hashlib
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import bridge_riders  # noqa: E402

MAIN = pathlib.Path(r"C:/RideBoundData/research/via-bridge-man-v1/main")
EMULATOR = HERE / "emulator-riders-2000.tsv"
SEAL = HERE / "EMULATOR-PREDICTIONS.sha256"
FIELDS = ("t0", "p0", "A", "served", "sumAbsC", "nonzeroC", "netC", "maxAbsC", "shared")
ARMS = ("U", "C-120", "C-300", "PS-300", "DLHZ-120", "RCE35", "EOHZ35", "G2-U", "G2-RCE35", "G2-EOHZ35")


def norm(value: str) -> str:
    value = value.strip()
    if value in ("", "NaN", "nan", "undefined"):
        return ""
    return str(int(float(value))) if value.lstrip("-").replace(".", "", 1).isdigit() else value


def main() -> int:
    args = sys.argv[1:]
    root, emulator, seal = MAIN, EMULATOR, SEAL

    def take(flag, default):
        if flag in args:
            index = args.index(flag)
            value = pathlib.Path(args[index + 1])
            del args[index:index + 2]
            return value
        return default

    root = take("--root", root)            # e.g. the smoke or elmi folder
    emulator = take("--emulator", emulator)  # e.g. emulator-riders-elmi-2000.tsv
    seal = take("--seal", seal)            # e.g. EMULATOR-PREDICTIONS-ELMI.sha256
    if len(args) != 1:
        print(__doc__)
        return 2
    prefix = pathlib.Path(args[0])
    sealed = [line.split()[0] for line in seal.read_text(encoding="utf-8").splitlines()
              if line and not line.startswith("#") and line.rstrip().endswith(emulator.name)]
    actual = hashlib.sha256(emulator.read_bytes()).hexdigest()
    if sealed != [actual]:
        raise SystemExit(f"emulator predictions changed since sealing: {actual}")

    finished = []
    for folder in sorted(root.iterdir()):
        summary = folder / "summary.json"
        if summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass":
            arm = next((a for a in sorted(ARMS, key=len, reverse=True) if folder.name.startswith(a + "-d")), None)
            if arm:
                finished.append((arm, folder))
    real_rows = []
    for arm, folder in finished:
        real_rows.extend(bridge_riders.rows_for(arm, folder))
    real_tsv = prefix.with_name(prefix.name + "-real-riders.tsv")
    with open(real_tsv, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(bridge_riders.HEADER)
        for row in real_rows:
            handle.write("\t".join(str(v) for v in row) + "\n")

    real = {}
    for row in csv.DictReader(open(real_tsv, encoding="utf-8"), delimiter="\t"):
        real.setdefault((row["role"], row["cell"]), {})[row["requestId"]] = row
    emu = {}
    for row in csv.DictReader(open(emulator, encoding="utf-8"), delimiter="\t"):
        emu.setdefault((row["role"], row["cell"]), {})[row["requestId"]] = row

    results = []
    riders_total = riders_same = 0
    for (role, cell), rr in sorted(real.items()):
        ee = emu.get((role, cell), {})
        only_real = sorted(set(rr) - set(ee))
        only_emu = sorted(set(ee) - set(rr))
        same = 0
        first = None
        for rq in sorted(set(rr) & set(ee), key=lambda q: (int(rr[q]["t0"]), q)):
            diffs = [f for f in FIELDS if norm(rr[rq][f]) != norm(ee[rq][f])]
            if diffs:
                if first is None:
                    first = {"requestId": rq, "t0": rr[rq]["t0"],
                             "fields": {f: [rr[rq][f], ee[rq][f]] for f in diffs}}
            else:
                same += 1
        total = len(set(rr) | set(ee))
        riders_total += total
        riders_same += same
        results.append({"arm": role, "cell": cell, "riders": total, "identical": same,
                        "exact": same == total and not only_real and not only_emu,
                        "onlyReal": len(only_real), "onlyEmulator": len(only_emu),
                        "servedReal": sum(norm(r["served"]) == "1" for r in rr.values()),
                        "servedEmulator": sum(norm(r["served"]) == "1" for r in ee.values()),
                        "firstDifference": first})
    report = {
        "finishedJobs": len(finished), "cellArmsCompared": len(results),
        "cellArmsExact": sum(r["exact"] for r in results),
        "riders": riders_total, "ridersIdentical": riders_same,
        "emulatorPredictionsSha256": actual, "realRidersTsv": str(real_tsv),
        "byArm": {arm: {"cellArms": sum(r["arm"] == arm for r in results),
                        "exact": sum(r["arm"] == arm and r["exact"] for r in results)} for arm in ARMS},
        "results": results,
    }
    out = prefix.with_name(prefix.name + "-compare.json")
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
        handle.write("\n")
    print(f"jobs {len(finished)}; cell-arms exact {report['cellArmsExact']}/{len(results)}; "
          f"riders identical {riders_same}/{riders_total}")
    for arm, v in report["byArm"].items():
        print(f"  {arm:9s} exact {v['exact']}/{v['cellArms']}")
    for r in results:
        if not r["exact"]:
            print(f"  NOT EXACT {r['arm']} {r['cell']}: {r['identical']}/{r['riders']} riders, "
                  f"onlyReal {r['onlyReal']}, onlyEmu {r['onlyEmulator']}, first {json.dumps(r['firstDifference'])[:300]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
