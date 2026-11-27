"""Per-rider rows of finished RideBound jobs, read from each job's evidence file.

  python -B riders.py --root <runs> [--root <runs> ...] --out <riders.tsv> [--sequences <drop-sequences.tsv>]
      [--arms A,B,...]

A job is a folder <arm>-<cell> under a --root whose summary.json says "pass". The roots are read in the given order,
the folders of each root in name order; a job of an arm that is not listed is skipped, and a job name seen under an
earlier root is not read again. A job is read from evidence.json.gz, which must match EVIDENCE.sha256 (format
ridebound-job-evidence-v1). Every job of the final runs has it: the job's finalize step writes it, with the audit table
that run_job.audit rebuilds from the transcript, whether the transcript is kept or not.

--out gets one row per rider with a drop promise, sorted by requestId:
  lifecycle   the rider's state in the final checkpoint
  t0, p0      time and value of the rider's first published drop ETA (the initial promise)
  D0          always empty (the column keeps the layout of the emulator's rider files)
  A           sim time of the first event batch with the rider's passengerAlighted event
  served      1 if the rider is completed and alighted
  sumAbsC, nonzeroC, netC, maxAbsC
              over the rider's later drop revisions, with c = p - e (published ETA minus the kept-route ETA)
  shared      1 if another alighted rider was on board the same vehicle at the same time
--sequences gets each rider's published drop ETAs as a JSON list, in ledger order, without consecutive repeats and
without the revision that the Runner publishes at the drop itself (the last one, published at its own drop ETA).

A job is refused when its evidence disagrees with itself: the riders' sum of |c| must equal the ledger total, each
rider's ledger entries must add up to its sum, and every request's lifecycle must be a string.
"""
import argparse
import collections
import gzip
import hashlib
import json
import pathlib
import re

HEADER = "role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared\n"
SEQUENCES_HEADER = "role\tcell\trequestId\tdropSequence\n"
MAIN_ARMS = ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N")
CELL = re.compile(r"(d\d{8}-vf-r\d-w\d\d)")
EVIDENCE = "evidence.json.gz"


def finished_jobs(roots, arms):
    """(arm, folder) of every passing job, root by root and in name order; a job name is read once."""
    found, seen = [], set()
    for root in roots:
        if not root.exists():
            continue
        for folder in sorted(root.iterdir()):
            summary = folder / "summary.json"
            if summary.exists() and json.loads(summary.read_text(encoding="utf-8")).get("status") == "pass":
                arm = next((a for a in sorted(arms, key=len, reverse=True) if folder.name.startswith(a + "-d")), None)
                if arm and (arm, folder.name) not in seen:
                    seen.add((arm, folder.name))
                    found.append((arm, folder))
    return found


def read_job(folder):
    """(audit table, ledger total of |c| over the drop ETAs, final online state, first alighting time per rider), as
    the job's evidence file stores them."""
    if not (folder / EVIDENCE).exists():
        raise SystemExit(f"{folder}: no {EVIDENCE}")
    data = (folder / EVIDENCE).read_bytes()
    sealed = (folder / "EVIDENCE.sha256").read_text(encoding="utf-8").split()[0]
    if hashlib.sha256(data).hexdigest() != sealed:
        raise SystemExit(f"{folder}: {EVIDENCE} does not match EVIDENCE.sha256")
    evidence = json.loads(gzip.decompress(data))
    if evidence.get("format") != "ridebound-job-evidence-v1" or evidence.get("job") != folder.resolve().name:
        raise SystemExit(f"{folder}: not this job's evidence")
    audit = evidence["audit"]
    return audit["table"], audit["summary"]["sumDropC"], evidence["finalOnlineState"], evidence["events"]["alighted"]


def rider_rows(role, cell, folder, table, total_c, state, alighted):
    """The --out rows of one job, sorted by requestId."""
    first = {}  # requestId -> (t0, p0)
    sum_c, nonzero_c, net_c, max_c = (collections.defaultdict(int) for _ in range(4))
    entry_c = {}  # (requestId, sequence) -> c of the drop ETA (0 for the initial promise)
    for rider, coordinate, sequence, kind, published_at, v, e, p, x, c, z, closed in table:
        if coordinate != "drop":
            continue
        entry_c[(rider, sequence)] = 0 if kind == "initialPromise" else c
        if kind == "initialPromise":
            if rider in first:
                raise SystemExit(f"{folder}: {rider} has two drop initialPromise rows")
            first[rider] = (published_at, p)
            continue
        sum_c[rider] += abs(c)
        nonzero_c[rider] += c != 0
        net_c[rider] += c
        max_c[rider] = max(max_c[rider], abs(c))
    if sum(sum_c.values()) != total_c:
        raise SystemExit(f"{folder}: per-rider sum |c| != ledger total {total_c}")
    for history in state["commitmentLedger"]:
        rider = history["requestId"]
        spent = sum(abs(entry_c[(rider, sequence)]) for sequence, _ in enumerate(history["entries"]))
        if rider in first and spent != sum_c[rider]:
            raise SystemExit(f"{folder}: {rider} ledger running sum {spent} != {sum_c[rider]}")
    lifecycle = {}
    for request in state["requests"]:
        if not isinstance(request["lifecycle"], str):
            raise SystemExit(f"{folder}: request {request['requestId']!r} has the lifecycle {request['lifecycle']!r}, "
                             "not a string")
        lifecycle[request["requestId"]] = request["lifecycle"]
    served = {rider for rider, value in lifecycle.items() if value == "completed"} & set(alighted)
    on_board = collections.defaultdict(list)  # vehicle -> (boarding time, alighting time, requestId)
    for request in state["requests"]:
        if request["requestId"] in alighted:
            on_board[request["assignedVehicleId"]].append(
                (request["actualPickupTimeMs"], alighted[request["requestId"]], request["requestId"]))
    shared = set()
    for legs in on_board.values():
        for start, end, rider in legs:
            if any(other != rider and other_start < end and start < other_end
                   for other_start, other_end, other in legs):
                shared.add(rider)
    rows = []
    for rider in sorted(first):
        t0, p0 = first[rider]
        rows.append([role, cell, rider, lifecycle.get(rider, ""), t0, p0, "", alighted.get(rider, ""),
                     int(rider in served), sum_c[rider], nonzero_c[rider], net_c[rider], max_c[rider],
                     int(rider in shared)])
    return rows


def drop_sequences(folder, table):
    """The --sequences values of one job: requestId -> published drop ETAs."""
    entries = collections.defaultdict(list)
    for rider, coordinate, sequence, kind, published_at, v, e, p, *_ in table:
        if coordinate == "drop":
            entries[rider].append((sequence, kind, published_at, p))
    sequences = {}
    for rider, values in entries.items():
        values.sort()
        if values[0][1] != "initialPromise":
            raise SystemExit(f"{folder}: {rider} does not start with its initial promise")
        # The Runner's last revision at the drop itself (published at the drop time, p = that time) is left out.
        if len(values) > 1 and values[-1][1] == "revision" and values[-1][2] == values[-1][3]:
            values = values[:-1]
        sequence = []
        for value in values:
            if not sequence or value[3] != sequence[-1]:
                sequence.append(value[3])
        sequences[rider] = sequence
    return sequences


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", action="append", required=True, type=pathlib.Path, help="a folder of job folders")
    parser.add_argument("--out", required=True, type=pathlib.Path, help="the rider TSV to write (must not exist)")
    parser.add_argument("--sequences", type=pathlib.Path, help="also write the drop-ETA sequences (must not exist)")
    parser.add_argument("--arms", default=",".join(MAIN_ARMS), help="comma-separated arms to read")
    args = parser.parse_args(argv)
    for path in (args.out, args.sequences):
        if path is not None and path.exists():
            parser.error(f"{path} exists")
    jobs = finished_jobs(args.root, tuple(args.arms.split(",")))
    count, sequence_lines = 0, []
    with open(args.out, "x", encoding="utf-8", newline="\n") as out:
        out.write(HEADER)
        for arm, folder in jobs:
            match = CELL.search(folder.name)
            if not match:
                raise SystemExit(f"no cell id in {folder.name}")
            table, total_c, state, alighted = read_job(folder)
            for row in rider_rows(arm, match.group(1), folder, table, total_c, state, alighted):
                out.write("\t".join(str(value) for value in row) + "\n")
                count += 1
            if args.sequences:
                sequences = drop_sequences(folder, table)
                for rider in sorted(sequences):
                    sequence = json.dumps(sequences[rider], separators=(",", ":"))
                    sequence_lines.append(f"{arm}\t{match.group(1)}\t{rider}\t{sequence}\n")
            print(f"read {folder.name}", flush=True)
    if args.sequences:
        with open(args.sequences, "x", encoding="utf-8", newline="\n") as handle:
            handle.write(SEQUENCES_HEADER)
            handle.writelines(sequence_lines)
    print(f"wrote {count} rider rows from {len(jobs)} jobs")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
