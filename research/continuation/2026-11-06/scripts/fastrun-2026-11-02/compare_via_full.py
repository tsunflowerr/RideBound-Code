"""Full-node Via comparison (ADR-089, GĐ3): real RideBound jobs on the vf cells vs the sealed emulator predictions.

The rider rule is via-real-2026-10-26/compare_via.py's, unchanged: per accepted rider t0, p0, A, served, sumAbsC,
nonzeroC, netC, maxAbsC, shared, plus the accepted-rider set (via-bridge-2026-10-22/bridge_riders.rows_for); a cell-arm
is exact when both sides agree on everything; G2 moves counted by plan membership (moves_plan.count) are compared with
the emulator's relocation count. Differences, all in this process only:
* arms include EO35 and G2K8-EO35; the cell pattern matches vf cells; predictions and seal are the vf ones;
* transcripts may be gzip (transcript-00.ndjson.gz). The three readers the helpers use (audit_vep.read_frames,
  g2_moves.frames, the open() in bridge_riders.event_times) are replaced by line-streaming readers that still check
  every frame's SHA-256. In a frame over 16 MiB every "arcs" array of a travel snapshot (the travelTimesUpdated
  snapshot, the checkpoint's travelTimes) is replaced by [] before parsing: no helper reads arcs, and a full-node job
  has about 16 snapshots of about 140 MB each.

Usage: python -I -B compare_via_full.py <out-prefix> [--root <phase folder> ...]  (default: runs/main16 and runs/smoke)
Writes <prefix>-real-riders.tsv and <prefix>-compare.json (never overwrites).
"""
from __future__ import annotations

import base64
import builtins
import csv
import gzip
import hashlib
import json
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
BRIDGE = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
PHASE1 = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/bridge-phase1-2026-10-23")
sys.path.insert(0, str(BRIDGE))
sys.path.insert(0, str(PHASE1))
sys.path.insert(0, r"E:\Code\Report_INT3508\ridebound-scratchpad\tang3\evidence")   # as bridge_riders.py:25
import audit_vep  # noqa: E402
import bridge_riders  # noqa: E402
import g2_moves  # noqa: E402
import moves_plan  # noqa: E402

DATA = pathlib.Path("E:/RideBoundData/research/via-full-v1")
OUT_ROOT = DATA / "runs"
EMULATOR = DATA / "predictions" / "emulator-riders-via-full.tsv"
COUNTS = DATA / "predictions" / "emulator-riders-via-full-counts.tsv"
SEAL = HERE / "VIA-FULL-PREDICTIONS.sha256"
FIELDS = ("t0", "p0", "A", "served", "sumAbsC", "nonzeroC", "netC", "maxAbsC", "shared")
ARMS = ("VIA-U", "VIA-RCE35", "VIA-EOHZ35", "VIA-EO35", "VIA-G2K8-U", "VIA-G2K8-RCE35", "VIA-G2K8-EOHZ35",
        "VIA-G2K8-EO35")
BIG_FRAME = 16 * 2**20
ARCS = re.compile(rb'"arcs":\[.*?\],"snapshotHash"', re.DOTALL)


def resolve(path: pathlib.Path) -> pathlib.Path:
    path = pathlib.Path(path)
    gz = path.with_name(path.name + ".gz")
    return gz if not path.exists() and gz.exists() else path


def open_text(path):
    path = resolve(path)
    return gzip.open(path, "rt", encoding="utf-8", newline="") if path.suffix == ".gz" else builtins.open(
        path, encoding="utf-8", newline="")


def without_arcs(frame: bytes) -> bytes:
    return ARCS.sub(b'"arcs":[],"snapshotHash"', frame) if len(frame) > BIG_FRAME else frame


def stream_frames(error):
    def frames(path):
        with open_text(path) as handle:
            for number, line in enumerate(handle):
                record = json.loads(line)
                frame = base64.b64decode(record["frameBase64"])
                if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
                    raise error(f"{path} line {number}: frame hash mismatch")
                yield record["direction"], json.loads(without_arcs(frame))
    return frames


def gz_open(file, *args, **kwargs):
    if str(file).endswith("transcript-00.ndjson"):
        return _stripped_lines(file)
    return builtins.open(file, *args, **kwargs)


class _stripped_lines:
    def __init__(self, path):
        self._handle = open_text(path)

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self._handle.close()

    def __iter__(self):
        for line in self._handle:
            record = json.loads(line)
            if record.get("frameLengthBytes", 0) > BIG_FRAME:
                frame = base64.b64decode(record["frameBase64"])
                record["frameBase64"] = base64.b64encode(without_arcs(frame)).decode("ascii")
                line = json.dumps(record)
            yield line


audit_vep.read_frames = stream_frames(audit_vep.EvidenceError)
g2_moves.frames = stream_frames(SystemExit)
bridge_riders.open = gz_open
bridge_riders.CELL = re.compile(r"(d\d{8}-vf-r\d-w\d\d)")


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
            sealed[name] = digest
    for path, name in ((EMULATOR, "predictions/" + EMULATOR.name), (COUNTS, "predictions/" + COUNTS.name)):
        if sealed.get(name) != hashlib.sha256(path.read_bytes()).hexdigest():
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
    roots = roots or [OUT_ROOT / "main16", OUT_ROOT / "smoke"]
    prefix = pathlib.Path(args[0])
    check_seal()

    finished, seen = [], set()
    for root in roots:
        if not root.exists():
            continue
        for folder in sorted(root.iterdir()):
            summary = folder / "summary.json"
            if summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass":
                arm = next((a for a in sorted(ARMS, key=len, reverse=True) if folder.name.startswith(a + "-d")), None)
                if arm and (arm, folder.name) not in seen:
                    seen.add((arm, folder.name))
                    finished.append((arm, folder))
    real_tsv = prefix.with_name(prefix.name + "-real-riders.tsv")
    with builtins.open(real_tsv, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(bridge_riders.HEADER)
        for arm, folder in finished:
            for row in bridge_riders.rows_for(arm, folder):
                handle.write("\t".join(str(v) for v in row) + "\n")
            print(f"read {folder.name}", flush=True)

    real, emu, relocations = {}, {}, {}
    with builtins.open(real_tsv, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            real.setdefault((row["role"], row["cell"]), {})[row["requestId"]] = row
    with builtins.open(EMULATOR, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            emu.setdefault((row["role"], row["cell"]), {})[row["requestId"]] = row
    with builtins.open(COUNTS, encoding="utf-8", newline="") as handle:
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
        if "G2" in role:
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
    with builtins.open(out, "x", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=2)
        handle.write("\n")
    print(f"jobs {len(finished)}; cell-arms exact {report['cellArmsExact']}/{len(results)}; riders identical "
          f"{report['ridersIdentical']}/{report['riders']}; G2 moves equal {report['movesEqual']}/{report['g2CellArms']}")
    for result in results:
        if not result["exact"]:
            print("  not exact:", json.dumps(result))
    return 0


if __name__ == "__main__":
    sys.exit(main())
