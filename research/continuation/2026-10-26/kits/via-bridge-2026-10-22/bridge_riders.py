"""Per-rider rows from RideBound run folders, in the riders.tsv layout the emulator compares against.

The rider rules are copied from the company's extract_stage1b.py (bundle code/step0/src/extract_stage1b.py:60-67 and
:897-971), without its Stage-1b refusal and pin machinery:
  t0, p0  = publishedAtMs and p of the rider's drop initialPromise (ledger table of tang3/evidence/audit_vep.audit);
  A       = simTimeMs of the first adapter batch with the rider's passengerAlighted event (sweep_analyze.py:40-57);
  served  = lifecycle "completed" AND A present;
  sumAbsC = sum |c| over later drop revisions; nonzeroC = count c != 0; netC = sum c; maxAbsC = max |c| (0 if none);
  shared  = another alighted rider on the same vehicle with an overlapping on-board interval [actualPickupTimeMs, A).
D0 is left empty (the emulator calibration does not compare it). Cross-checks that refuse a run: per-rider sum |c| must
equal the audit's sumDropC, and the Runner's ledger running drop budget must equal sum |c| per rider.

Usage: python -B bridge_riders.py <out.tsv> <role>=<run dir> [<role>=<run dir> ...]
       the cell id is taken from the run folder name (the trailing dYYYYMMDD-s10-rN-wHH).
"""

from __future__ import annotations

import base64
import collections
import json
import pathlib
import re
import sys

sys.path.insert(0, r"E:\Code\Report_INT3508\ridebound-scratchpad\tang3\evidence")
import audit_vep  # noqa: E402

CELL = re.compile(r"(d\d{8}-s10-r\d-w\d\d)")
HEADER = "role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared\n"


def event_times(transcript: pathlib.Path):
    """Verbatim rule of tang3/sweep/sweep_analyze.py:40-57."""
    boarded, alighted = {}, {}
    with open(transcript, encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            if record["direction"] != "adapterToRunner":
                continue
            envelope = json.loads(base64.b64decode(record["frameBase64"]))
            if envelope.get("messageType") != "eventBatch":
                continue
            for event in envelope["payload"].get("events", []):
                kind = event.get("eventType")
                if kind == "passengerBoarded":
                    boarded.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
                elif kind == "passengerAlighted":
                    alighted.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
    return boarded, alighted


def rows_for(role: str, folder: pathlib.Path) -> list[list]:
    match = CELL.search(folder.name)
    if not match:
        raise SystemExit(f"no cell id in {folder.name}")
    cell = match.group(1)
    transcript = folder / "transcript-00.ndjson"
    _, _, state = audit_vep.load(transcript)
    table, summary = audit_vep.audit(transcript)
    if table is None:
        raise SystemExit(f"{folder}: no final checkpoint")
    _, alighted = event_times(transcript)
    first = {}
    s_drop, k_drop, net, max_c = (collections.defaultdict(int) for _ in range(4))
    c_by_seq = {}
    for r, coord, seq, kind, pub, v, e, p, x, cc, z, closed in table:
        if coord != "drop":
            continue
        c_by_seq[(r, seq)] = 0 if kind == "initialPromise" else cc
        if kind == "initialPromise":
            if r in first:
                raise SystemExit(f"{folder}: {r} has two drop initialPromise rows")
            first[r] = (pub, p)
            continue
        s_drop[r] += abs(cc)
        k_drop[r] += cc != 0
        net[r] += cc
        max_c[r] = max(max_c[r], abs(cc))
    if sum(s_drop.values()) != summary["sumDropC"]:
        raise SystemExit(f"{folder}: per-rider sum |c| != audit sumDropC")
    for history in state["commitmentLedger"]:
        r, spent = history["requestId"], 0
        for seq, _entry in enumerate(history["entries"]):
            spent += abs(c_by_seq[(r, seq)])
        if r in first and spent != s_drop[r]:
            raise SystemExit(f"{folder}: {r} ledger running sum {spent} != {s_drop[r]}")
    reqs = state["requests"]
    life = {q["requestId"]: q["lifecycle"] for q in reqs}
    completed = {r for r, value in life.items() if value == "completed"}
    served = completed & set(alighted)
    legs = collections.defaultdict(list)
    for q in reqs:
        if q["requestId"] in alighted:
            legs[q["assignedVehicleId"]].append((q["actualPickupTimeMs"], alighted[q["requestId"]], q["requestId"]))
    shared = set()
    for rs in legs.values():
        for a0, a1, rid in rs:
            if any(o != rid and b0 < a1 and a0 < b1 for b0, b1, o in rs):
                shared.add(rid)
    rows = []
    for r in sorted(first):
        t0, p0 = first[r]
        rows.append([role, cell, r, life.get(r, ""), t0, p0, "", alighted.get(r, ""), int(r in served), s_drop[r],
                     k_drop[r], net[r], max_c[r], int(r in shared)])
    return rows


def main() -> int:
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    out = pathlib.Path(sys.argv[1])
    lines = [HEADER]
    for spec in sys.argv[2:]:
        role, folder = spec.split("=", 1)
        for row in rows_for(role, pathlib.Path(folder)):
            lines.append("\t".join(str(v) for v in row) + "\n")
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        handle.writelines(lines)
    print(f"wrote {out}: {len(lines) - 1} rider rows")
    return 0


if __name__ == "__main__":
    sys.exit(main())
