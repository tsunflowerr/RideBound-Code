"""Per-rider rows of finished RideBound jobs, read from each job's transcript or evidence file.

  python -B riders.py --root <runs> [--root <runs> ...] --out <riders.tsv> [--sequences <drop-sequences.tsv>]
      [--arms A,B,...]

A job is a folder <arm>-<cell> under a --root whose summary.json says "pass". The roots are read in the given order,
the folders of each root in name order; a job of an arm that is not listed is skipped, and a job name seen under an
earlier root is not read again. A job is read from transcript-00.ndjson.gz when it has one, otherwise from
evidence.json.gz, which must match EVIDENCE.sha256 (format ridebound-job-evidence-v1). These are the files a job of the
final runs leaves: the adapter's recorder writes the gzip transcript and the job's finalize step writes the evidence.

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

A job is refused when its records disagree. From a transcript: a frame whose SHA-256 differs, a ledger entry that is
not a promisePublished action of an acknowledged decision with the same ETAs, an acknowledged publication that is not
in the ledger or is published twice, deltas that are not |e - v|, |p - e| and |p - v|, or ETAs that are not canonical
integers or do not form one chain per rider and coordinate (the initial promise first, then v = the previous p).
Always: the riders' sum of |c| must equal the ledger total, and each rider's ledger entries must add up to its sum.

Also refused, because the final runs never produced them: a job folder with an uncompressed transcript-00.ndjson, a
transcript record whose frameLengthBytes is not the length of its frame, and a request whose lifecycle is not a
string. RB_PREFER_EVIDENCE=1, the old reader's switch to read the evidence next to a kept transcript, is refused too.
"""
import argparse
import base64
import collections
import gzip
import hashlib
import json
import os
import pathlib
import re

HEADER = "role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared\n"
SEQUENCES_HEADER = "role\tcell\trequestId\tdropSequence\n"
MAIN_ARMS = ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N")
CELL = re.compile(r"(d\d{8}-vf-r\d-w\d\d)")
TRANSCRIPT = "transcript-00.ndjson.gz"
EVIDENCE = "evidence.json.gz"
REMOVED = "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)"
# (coordinate, its ETA in a promise, its field in the ledger deltas)
COORDINATES = (("pickup", "pickupEtaMs", "pickupEtaTotalMs"), ("drop", "dropEtaMs", "dropEtaTotalMs"))
MAX_CANONICAL = 2**53 - 1
# In a frame over 16 MiB every travel snapshot's arcs list is emptied before parsing: nothing here reads arcs, and a
# full-node job sends about 16 snapshots of about 140 MB each.
BIG_FRAME = 16 * 2**20
ARCS = re.compile(rb'"arcs":\[.*?\],"snapshotHash"', re.DOTALL)


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
    """(audit table, ledger total of |c| over the drop ETAs, final online state, first alighting time per rider)."""
    if (folder / "transcript-00.ndjson").exists():
        raise SystemExit(f"{folder}: reading an uncompressed transcript-00.ndjson: {REMOVED}")
    if (folder / TRANSCRIPT).exists():
        return read_transcript(folder / TRANSCRIPT)
    if (folder / EVIDENCE).exists():
        return read_evidence(folder)
    raise SystemExit(f"{folder}: no {TRANSCRIPT} and no {EVIDENCE}")


def read_evidence(folder):
    """The values that the job's evidence file stores."""
    data = (folder / EVIDENCE).read_bytes()
    sealed = (folder / "EVIDENCE.sha256").read_text(encoding="utf-8").split()[0]
    if hashlib.sha256(data).hexdigest() != sealed:
        raise SystemExit(f"{folder}: {EVIDENCE} does not match EVIDENCE.sha256")
    evidence = json.loads(gzip.decompress(data))
    if evidence.get("format") != "ridebound-job-evidence-v1" or evidence.get("job") != folder.resolve().name:
        raise SystemExit(f"{folder}: not this job's evidence")
    audit = evidence["audit"]
    return audit["table"], audit["summary"]["sumDropC"], evidence["finalOnlineState"], evidence["events"]["alighted"]


def read_transcript(path):
    """One pass over the transcript, then the audit table rebuilt from its last checkpoint."""
    decisions, acknowledged, state, alighted = {}, set(), None, {}
    with gzip.open(path, "rt", encoding="utf-8", newline="") as handle:
        for number, line in enumerate(handle):
            record = json.loads(line)
            frame = base64.b64decode(record["frameBase64"])
            if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
                raise SystemExit(f"{path} line {number}: frame hash mismatch")
            stated = record.get("frameLengthBytes")
            if stated != len(frame):
                raise SystemExit(f"{path} line {number}: frameLengthBytes {stated!r} is not the frame's length "
                                 f"{len(frame)}")
            if len(frame) > BIG_FRAME:
                frame = ARCS.sub(b'"arcs":[],"snapshotHash"', frame)
            envelope = json.loads(frame)
            route = (record["direction"], envelope.get("messageType"))
            if route == ("runnerToAdapter", "decision"):
                decisions[envelope["payload"]["decisionHash"]] = envelope["payload"]
            elif route == ("adapterToRunner", "decisionApplied"):
                acknowledged.add(envelope["payload"]["decisionHash"])
            elif route == ("runnerToAdapter", "checkpoint"):
                state = envelope["payload"]["content"]["onlineState"]
            elif route == ("adapterToRunner", "eventBatch"):
                for event in envelope["payload"].get("events", []):
                    if event.get("eventType") == "passengerAlighted":
                        alighted.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
    if state is None:
        raise SystemExit(f"{path.parent}: no final checkpoint")
    actions = {}  # publicationId -> promisePublished payload of an acknowledged decision
    for decision_hash, payload in decisions.items():
        if decision_hash not in acknowledged:
            continue
        for action in payload.get("actions", []):
            if action.get("decisionType") == "promisePublished":
                value = action["payload"]
                if value["publicationId"] in actions:
                    raise SystemExit(f"{path}: publication {value['publicationId']} published twice")
                actions[value["publicationId"]] = value
    table = audit_table(state, actions)
    check_chains(table)
    return table, sum(abs(row[9]) for row in table if row[1] == "drop"), state, alighted


def audit_table(state, actions):
    """One row per ledger entry and coordinate, the table an evidence file stores:
    (requestId, coordinate, sequence, kind, publishedAtMs, v, e, p, x = e - v, c = p - e, z = p - v, closed),
    with v the promise in force before the entry, e the kept-route ETA and p the published ETA. A pickup revision
    published at or after the boarding is closed."""
    requests = {request["requestId"]: request for request in state["requests"]}
    table, in_ledger = [], set()
    for history in state["commitmentLedger"]:
        rider = history["requestId"]
        boarded = requests.get(rider, {}).get("actualPickupTimeMs")
        for sequence, entry in enumerate(history["entries"]):
            published = entry["publishedPromise"]
            in_ledger.add(entry["publicationId"])
            action = actions.get(entry["publicationId"])
            if action is None:
                raise SystemExit(f"{rider}: ledger publication {entry['publicationId']} has no acknowledged action")
            initial = entry["kind"] == "initialPromise"
            for coordinate, eta, dimension in COORDINATES:
                p = published["projection"][eta]
                if action["promise"][eta] != p:
                    raise SystemExit(f"{rider}/{coordinate}: action p differs from ledger p")
                if initial:
                    v = e = p
                else:
                    v, e = entry["previousPromise"]["projection"][eta], entry["exogenousProjection"][eta]
                    for name, value in (("exogenous", abs(e - v)), ("decisionInduced", abs(p - e)),
                                        ("visible", abs(p - v))):
                        if entry["deltas"][name][dimension] != value:
                            raise SystemExit(f"{rider}/{coordinate}#{sequence}: |{name}| ledger="
                                             f"{entry['deltas'][name][dimension]} recomputed={value}")
                closed = (coordinate == "pickup" and boarded is not None
                          and published["publishedAtMs"] >= boarded and not initial)
                table.append((rider, coordinate, sequence, entry["kind"], published["publishedAtMs"],
                              v, e, p, e - v, p - e, p - v, int(closed)))
    missing = set(actions) - in_ledger
    if missing:
        raise SystemExit(f"{len(missing)} acknowledged publications are not in the final ledger")
    return table


def check_chains(table):
    """Every ETA is a canonical integer, and each rider's rows per coordinate form one chain: the initial promise
    first, then every revision starts from the promise in force (v = the previous p)."""
    previous = {}  # (requestId, coordinate) -> (sequence, p) of the row before
    for rider, coordinate, sequence, kind, _, v, e, p, *_ in sorted(table, key=lambda row: row[:3]):
        for value in (v, e, p):
            if not isinstance(value, int) or isinstance(value, bool) or not 0 <= value <= MAX_CANONICAL:
                raise SystemExit(f"{rider}/{coordinate}#{sequence}: ETA {value!r} is not a canonical integer")
        key = (rider, coordinate)
        if key not in previous:
            if kind != "initialPromise":
                raise SystemExit(f"{rider}/{coordinate}#{sequence}: the first entry is not the initial promise")
        elif kind == "initialPromise" or sequence <= previous[key][0] or v != previous[key][1]:
            raise SystemExit(f"{rider}/{coordinate}#{sequence} breaks the chain: {kind}, v={v}, previous p="
                             f"{previous[key][1]}")
        previous[key] = (sequence, p)


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
    if os.environ.get("RB_PREFER_EVIDENCE") == "1":
        parser.error(f"RB_PREFER_EVIDENCE=1 (read the evidence next to a kept transcript): {REMOVED}")
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
