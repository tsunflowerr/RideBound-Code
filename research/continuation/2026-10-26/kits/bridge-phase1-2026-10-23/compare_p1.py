"""Phase-1 bridge comparison: real RideBound jobs vs the sealed phase-1 emulator predictions.

Same rule as via-bridge-2026-10-22/bridge_compare.py (per accepted rider: t0, p0, A, served, sumAbsC, nonzeroC, netC,
maxAbsC, shared, plus the accepted-rider set; a cell-arm is exact when both agree on everything), with the rider rows
extracted by the same module (bridge_riders.rows_for). Adds, for the G2 roles, the moves counted by plan membership
(moves_plan.py) against the emulator's relocation count of the same cell-arm.
The emulator files must match EMULATOR-PREDICTIONS-P1.sha256 (sealed before any phase-1 job).

Usage: python -B compare_p1.py <out-prefix> [--root <phase folder> ...]
Writes <prefix>-real-riders.tsv and <prefix>-compare.json (never overwrites).
"""
from __future__ import annotations

import csv
import hashlib
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
BRIDGE = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
sys.path.insert(0, str(BRIDGE))
sys.path.insert(0, str(HERE))
import bridge_riders  # noqa: E402
import moves_plan  # noqa: E402

OUT_ROOT = pathlib.Path("C:/RideBoundData/research/via-bridge-man-v1/p1")
EMULATOR = HERE / "emulator-riders-p1.tsv"
COUNTS = HERE / "emulator-riders-p1-counts.tsv"
SEAL = HERE / "EMULATOR-PREDICTIONS-P1.sha256"
FIELDS = ("t0", "p0", "A", "served", "sumAbsC", "nonzeroC", "netC", "maxAbsC", "shared")
ARMS = ("EO35", "DLS-120", "G2-EO35", "G2-DLS-120", "G2K3-U", "G2K3-RCE35", "G2K3-EOHZ35")


def norm(value: str) -> str:
    value = value.strip()
    if value in ("", "NaN", "nan", "undefined"):
        return ""
    return str(int(float(value))) if value.lstrip("-").replace(".", "", 1).isdigit() else value


def check_seal() -> None:
    sealed = {}
    for line in SEAL.read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#") and " *" in line:
            digest, name = line.split(" *", 1)
            sealed[pathlib.Path(name).name] = digest
    for path in (EMULATOR, COUNTS):
        if sealed.get(path.name) != hashlib.sha256(path.read_bytes()).hexdigest():
            raise SystemExit(f"{path.name} changed since sealing")


def main() -> int:
    args = sys.argv[1:]
    roots = []
    while "--root" in args:
        index = args.index("--root")
        roots.append(pathlib.Path(args[index + 1]))
        del args[index:index + 2]
    if len(args) != 1:
        print(__doc__)
        return 2
    roots = roots or [OUT_ROOT / "p1pilot", OUT_ROOT / "p1main"]
    prefix = pathlib.Path(args[0])
    check_seal()

    finished = []
    for root in roots:
        if not root.exists():
            continue
        for folder in sorted(root.iterdir()):
            summary = folder / "summary.json"
            if summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass":
                arm = next((a for a in sorted(ARMS, key=len, reverse=True) if folder.name.startswith(a + "-d")), None)
                if arm:
                    finished.append((arm, folder))
    real_tsv = prefix.with_name(prefix.name + "-real-riders.tsv")
    with open(real_tsv, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(bridge_riders.HEADER)
        for arm, folder in finished:
            for row in bridge_riders.rows_for(arm, folder):
                handle.write("\t".join(str(v) for v in row) + "\n")

    real, emu, relocations = {}, {}, {}
    with open(real_tsv, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            real.setdefault((row["role"], row["cell"]), {})[row["requestId"]] = row
    with open(EMULATOR, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            emu.setdefault((row["role"], row["cell"]), {})[row["requestId"]] = row
    with open(COUNTS, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            relocations[(row["role"], row["cell"])] = int(row["relocations"])
    folders = {(arm, bridge_riders.CELL.search(folder.name).group(1)): folder for arm, folder in finished}

    results = []
    for (role, cell), rr in sorted(real.items()):
        ee = emu.get((role, cell), {})
        only_real, only_emu = sorted(set(rr) - set(ee)), sorted(set(ee) - set(rr))
        same, first = 0, None
        for rq in sorted(set(rr) & set(ee), key=lambda q: (int(rr[q]["t0"]), q)):
            diffs = [f for f in FIELDS if norm(rr[rq][f]) != norm(ee[rq][f])]
            if diffs and first is None:
                first = {"requestId": rq, "t0": rr[rq]["t0"], "fields": {f: [rr[rq][f], ee[rq][f]] for f in diffs}}
            same += not diffs
        total = len(set(rr) | set(ee))
        entry = {"arm": role, "cell": cell, "riders": total, "identical": same,
                 "exact": same == total and not only_real and not only_emu,
                 "onlyReal": len(only_real), "onlyEmulator": len(only_emu), "firstDifference": first}
        if role.startswith("G2"):
            moved = moves_plan.count(folders[(role, cell)])
            entry.update({"movesReal": moved["moves"], "movesEmulator": relocations[(role, cell)],
                          "maxMovesPerDecision": moved["maxMovesPerDecision"]})
        results.append(entry)
    report = {
        "finishedJobs": len(finished), "cellArmsCompared": len(results),
        "cellArmsExact": sum(r["exact"] for r in results),
        "riders": sum(r["riders"] for r in results), "ridersIdentical": sum(r["identical"] for r in results),
        "movesEqual": sum(r.get("movesReal") == r.get("movesEmulator") for r in results if "movesReal" in r),
        "g2CellArms": sum("movesReal" in r for r in results),
        "byArm": {arm: {"cellArms": sum(r["arm"] == arm for r in results),
                        "exact": sum(r["arm"] == arm and r["exact"] for r in results)} for arm in ARMS},
        "realRidersTsv": str(real_tsv), "results": results,
    }
    out = prefix.with_name(prefix.name + "-compare.json")
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
        handle.write("\n")
    print(f"jobs {len(finished)}; cell-arms exact {report['cellArmsExact']}/{len(results)}; riders identical "
          f"{report['ridersIdentical']}/{report['riders']}; G2 moves equal {report['movesEqual']}/{report['g2CellArms']}")
    for arm, value in report["byArm"].items():
        print(f"  {arm:12s} exact {value['exact']}/{value['cellArms']}")
    for r in results:
        if not r["exact"] or r.get("movesReal") != r.get("movesEmulator") or r.get("maxMovesPerDecision", 0) > 1:
            print(f"  CHECK {r['arm']} {r['cell']}: {r['identical']}/{r['riders']} riders, onlyReal {r['onlyReal']}, "
                  f"onlyEmu {r['onlyEmulator']}, moves {r.get('movesReal')}/{r.get('movesEmulator')}, "
                  f"first {json.dumps(r['firstDifference'])[:300]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
