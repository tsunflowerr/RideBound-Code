"""Compare the DLS-120-NW jobs (student's relaxed-deadline form) with the emulator DLHZ-120 rows sealed on 21/10.

Same rule as compare_p1.py: per accepted rider t0, p0, A, served, sumAbsC, nonzeroC, netC, maxAbsC, shared, plus the
accepted-rider set; a cell is exact when everything agrees. Also compares with the real DLHZ-120 rider rows of the
22/10 bridge (via-bridge-2026-10-22/compare/*-real-riders.tsv that contain role DLHZ-120).
Usage: python -B compare_nw.py <out-prefix>   (writes <prefix>-real-riders.tsv and <prefix>-compare.json, never overwrites)
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
import bridge_riders  # noqa: E402

ROOT = pathlib.Path("E:/RideBoundData/research/via-bridge-man-v1-e/nw")
EMULATOR = BRIDGE / "emulator-riders-2000.tsv"
SEALED = "06742da62c45af96c1310b85a811088fbaaa7ad9674f2e068557592a8a13d17a"
FIELDS = ("t0", "p0", "A", "served", "sumAbsC", "nonzeroC", "netC", "maxAbsC", "shared")
ARM = "DLS-120-NW"


def norm(value: str) -> str:
    value = (value or "").strip()
    if value in ("", "NaN", "nan", "undefined"):
        return ""
    return str(int(float(value))) if value.lstrip("-").replace(".", "", 1).isdigit() else value


def load(path, role):
    out = {}
    with open(path, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            if row["role"] == role:
                out.setdefault(row["cell"], {})[row["requestId"]] = row
    return out


def compare(rr, ee):
    only_real, only_other = set(rr) - set(ee), set(ee) - set(rr)
    same, first = 0, None
    for rq in sorted(set(rr) & set(ee), key=lambda q: (int(rr[q]["t0"]), q)):
        diffs = [f for f in FIELDS if norm(rr[rq][f]) != norm(ee[rq][f])]
        if diffs and first is None:
            first = {"requestId": rq, "fields": {f: [rr[rq][f], ee[rq][f]] for f in diffs}}
        same += not diffs
    total = len(set(rr) | set(ee))
    return {"riders": total, "identical": same, "exact": same == total and not only_real and not only_other,
            "onlyReal": len(only_real), "onlyOther": len(only_other), "firstDifference": first}


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    prefix = pathlib.Path(sys.argv[1])
    if hashlib.sha256(EMULATOR.read_bytes()).hexdigest() != SEALED:
        raise SystemExit("emulator-riders-2000.tsv changed since sealing")
    finished = []
    for folder in sorted(ROOT.iterdir()) if ROOT.exists() else []:
        summary = folder / "summary.json"
        if summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass":
            finished.append(folder)
    real_tsv = prefix.with_name(prefix.name + "-real-riders.tsv")
    with open(real_tsv, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(bridge_riders.HEADER)
        for folder in finished:
            for row in bridge_riders.rows_for(ARM, folder):
                handle.write("\t".join(str(v) for v in row) + "\n")
    real = load(real_tsv, ARM)
    emu = load(EMULATOR, "DLHZ-120")
    dlhz_real = {}
    for path in sorted((BRIDGE / "compare").glob("*-real-riders.tsv")):
        for cell, rows in load(path, "DLHZ-120").items():
            dlhz_real.setdefault(cell, rows)
    results = []
    for cell in sorted(real):
        entry = {"cell": cell, "vsEmulatorDLHZ120": compare(real[cell], emu.get(cell, {}))}
        if cell in dlhz_real:
            entry["vsRealDLHZ120"] = compare(real[cell], dlhz_real[cell])
        results.append(entry)
    report = {"finishedJobs": len(finished), "cells": len(results),
              "exactVsEmulator": sum(r["vsEmulatorDLHZ120"]["exact"] for r in results),
              "exactVsRealDLHZ120": sum(r.get("vsRealDLHZ120", {}).get("exact", False) for r in results),
              "ridersVsEmulator": [sum(r["vsEmulatorDLHZ120"]["identical"] for r in results),
                                   sum(r["vsEmulatorDLHZ120"]["riders"] for r in results)],
              "results": results}
    with open(prefix.with_name(prefix.name + "-compare.json"), "x", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
        handle.write("\n")
    print(f"jobs {len(finished)}; exact vs emulator DLHZ-120 {report['exactVsEmulator']}/{len(results)} "
          f"(riders {report['ridersVsEmulator'][0]}/{report['ridersVsEmulator'][1]}); exact vs real DLHZ-120 "
          f"{report['exactVsRealDLHZ120']}/{len(results)}")
    for r in results:
        if not r["vsEmulatorDLHZ120"]["exact"]:
            print(f"  CHECK {r['cell']}: {json.dumps(r['vsEmulatorDLHZ120'])[:300]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
