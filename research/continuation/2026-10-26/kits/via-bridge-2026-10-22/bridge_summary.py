"""Final tables for the Manhattan bridge (descriptive; 11 cells / 7 days is NOT an inferential sample).

Inputs: the latest compare/*-compare.json (+ its real-riders TSV), A0-GATE-SUMMARY.json, logs/main/*.log first lines.
Per arm (main jobs, real RideBound): promised riders, served, riders with any dispatch change (k >= 1), with k >= 2,
sum |c| (s), served riders late > 60 s against their first drop promise (A - p0 > 60 s).
Per job resources: wall minutes and peak process-tree RSS (Runner + Python) from the driver's job log.

Usage: python -B bridge_summary.py <compare.json> <out.txt>
"""

from __future__ import annotations

import collections
import csv
import json
import pathlib
import re
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
ARMS = ("U", "C-120", "C-300", "PS-300", "DLHZ-120")
FIRST = re.compile(r"returncode=(-?\d+) seconds=(\d+) peakTreeRssMiB=(\d+)")


def main() -> int:
    compare_path, out_path = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    compare = json.loads(compare_path.read_text(encoding="utf-8"))
    a0 = json.loads((HERE / "A0-GATE-SUMMARY.json").read_text(encoding="utf-8"))
    riders = list(csv.DictReader(open(compare["realRidersTsv"], encoding="utf-8"), delimiter="\t"))

    lines = []
    lines.append("MANHATTAN BRIDGE - final tables")
    lines.append(f"compare file: {compare_path.name}; emulator predictions sha256 {compare['emulatorPredictionsSha256']}")
    lines.append("")
    lines.append("1. Real RideBound vs sealed emulator predictions (per rider: t0, p0, A, served, sum|c|, k, net c, max|c|, shared)")
    lines.append(f"   main jobs read {compare['finishedJobs']}; cell-arms exact {compare['cellArmsExact']}/{compare['cellArmsCompared']}; "
                 f"riders identical {compare['ridersIdentical']}/{compare['riders']}")
    for arm in ARMS:
        v = compare["byArm"][arm]
        lines.append(f"   {arm:9s} exact {v['exact']}/{v['cellArms']}")
    for r in compare["results"]:
        if not r["exact"]:
            lines.append(f"   NOT EXACT {r['arm']} {r['cell']}: {r['identical']}/{r['riders']}; first {json.dumps(r['firstDifference'])[:240]}")
    lines.append("")
    lines.append("2. A0.1 gate (incremental-hash Runner replays every recorded job byte for byte)")
    lines.append(f"   jobs {a0['identical']}/{a0['replayed']} identical; frames {a0['framesIdentical']}/{a0['framesTotal']}")
    cpus = [r["runnerCpuSeconds"] for r in a0["rows"] if r["runnerCpuSeconds"] is not None]
    if cpus:
        lines.append(f"   A0 Runner CPU per replay: median {statistics.median(cpus):.1f} s, max {max(cpus):.1f} s")
    lines.append("")
    lines.append("3. Job resources (driver logs; 2 jobs in parallel; 09:00-12:35 also shared the PC with the compression)")
    secs, peaks = [], []
    for log in sorted((HERE / "logs" / "main").glob("*.log")):
        m = FIRST.match(log.read_text(encoding="utf-8").splitlines()[0])
        if m and m.group(1) == "0":
            secs.append(int(m.group(2)) / 60)
            peaks.append(int(m.group(3)))
    if secs:
        lines.append(f"   jobs {len(secs)}; wall min median {statistics.median(secs):.1f} (min {min(secs):.1f}, max {max(secs):.1f}); "
                     f"peak tree RSS MiB median {statistics.median(peaks):.0f} (max {max(peaks)})")
    lines.append("")
    lines.append("4. Descriptive outcomes per arm (11 cells summed; NOT a test, 7 days only)")
    lines.append(f"   {'arm':9s} {'promised':>8s} {'served':>7s} {'k>=1':>6s} {'k>=2':>6s} {'sum|c| s':>9s} {'late>60s':>9s}")
    agg = collections.defaultdict(lambda: collections.Counter())
    for r in riders:
        a = agg[r["role"]]
        a["promised"] += 1
        served = r["served"] == "1"
        a["served"] += served
        k = int(r["nonzeroC"])
        a["k1"] += k >= 1
        a["k2"] += k >= 2
        a["S"] += int(r["sumAbsC"])
        if served and r["A"] not in ("", None) and int(r["A"]) - int(r["p0"]) > 60000:
            a["late60"] += 1
    for arm in ARMS:
        a = agg[arm]
        lines.append(f"   {arm:9s} {a['promised']:8d} {a['served']:7d} {a['k1']:6d} {a['k2']:6d} {a['S'] / 1000:9.0f} {a['late60']:9d}")
    text = "\n".join(lines) + "\n"
    with open(out_path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
