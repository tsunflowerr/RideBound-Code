"""riders.py on a small hand-made job: rider rows, drop-ETA sequences, job selection and refusals.

The expected rows and sequences are what the old evidence chain (bridge_riders.rows_for and
compare_drop_sequences.real_sequences, read through compare_via_full and evidence_reader) gives for the same job.
"""
import base64
import collections
import contextlib
import gzip
import hashlib
import io
import json
import os
import pathlib
import sys
import tempfile
import unittest
from unittest import mock

ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

import riders  # noqa: E402

ROLE, CELL = "FS-G2-SHRC71-CL", "d20190919-vf-r0-w08"
JOB = f"{ROLE}-{CELL}"
# requestId: (vehicle, lifecycle, boarding time, promises). A promise is (published at, pickup (e, p), drop (e, p));
# the first one is the initial promise (its e is not used), the others are revisions from the promise before.
RIDERS = {
    "rq-a": ("v1", "completed", 1000, [(100, (900, 900), (4800, 4800)), (600, (950, 950), (4900, 5100)),
                                       (2000, (1000, 1000), (5000, 4950)), (5000, (1000, 1000), (5000, 5000))]),
    "rq-b": ("v1", "completed", 3000, [(200, (2800, 2800), (6900, 6900)), (2500, (2900, 2900), (6900, 6900)),
                                       (7000, (3000, 3000), (7000, 7000))]),
    "rq-c": ("v2", "cancelled", None, [(300, (1500, 1500), (4000, 4000)), (800, (1600, 1550), (4300, 4100))]),
    "rq-d": ("v2", "completed", 2000, [(400, (1900, 1900), (3900, 3900)), (4000, (2000, 2000), (4000, 4000))]),
    "rq-f": ("v2", "completed", 2600, [(500, (2500, 2500), (6000, 6000))]),
    "rq-g": ("v2", "completed", 4000, [(700, (3900, 3900), (6100, 6100))]),
}
# (sim time of the event batch, requestId) of every passengerAlighted event; rq-f has none. rq-g boards when rq-d
# alights: the two rides only touch, so neither is shared.
ALIGHTED = [(4000, "rq-d"), (4500, "rq-d"), (5000, "rq-a"), (7000, "rq-b"), (6000, "rq-g")]
EXPECTED_ROWS = [
    "rq-a\tcompleted\t100\t4800\t\t5000\t1\t250\t2\t150\t200\t1",
    "rq-b\tcompleted\t200\t6900\t\t7000\t1\t0\t0\t0\t0\t1",
    "rq-c\tcancelled\t300\t4000\t\t\t0\t200\t1\t-200\t200\t0",
    "rq-d\tcompleted\t400\t3900\t\t4000\t1\t0\t0\t0\t0\t0",
    "rq-f\tcompleted\t500\t6000\t\t\t0\t0\t0\t0\t0\t0",
    "rq-g\tcompleted\t700\t6100\t\t6000\t1\t0\t0\t0\t0\t0",
]
EXPECTED_SEQUENCES = ["rq-a\t[4800,5100,4950]", "rq-b\t[6900]", "rq-c\t[4000,4100]", "rq-d\t[3900]", "rq-f\t[6000]",
                      "rq-g\t[6100]"]


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def deltas(v, e, p):
    """The ledger deltas |e - v|, |p - e| and |p - v| of both ETAs."""
    return {name: {"pickupEtaTotalMs": abs(b["pickupEtaMs"] - a["pickupEtaMs"]),
                   "dropEtaTotalMs": abs(b["dropEtaMs"] - a["dropEtaMs"])}
            for name, a, b in (("exogenous", v, e), ("decisionInduced", e, p), ("visible", v, p))}


def job_frames():
    """(direction, envelope) of the job in transcript order: one decision per publication time, each acknowledged,
    one more decision that is never acknowledged, the event batches, an early checkpoint and the final one."""
    requests = [{"requestId": "rq-e", "lifecycle": "rejected", "assignedVehicleId": None, "actualPickupTimeMs": None}]
    ledger, published, batches = [], collections.defaultdict(list), collections.defaultdict(list)
    for rider, (vehicle, lifecycle, boarded, promises) in RIDERS.items():
        requests.append({"requestId": rider, "lifecycle": lifecycle, "assignedVehicleId": vehicle,
                         "actualPickupTimeMs": boarded})
        if boarded is not None:
            batches[boarded].append({"eventType": "passengerBoarded", "payload": {"requestId": rider}})
        entries, previous = [], None
        for number, (at, (pickup_e, pickup_p), (drop_e, drop_p)) in enumerate(promises):
            promise = {"pickupEtaMs": pickup_p, "dropEtaMs": drop_p, "vehicleId": vehicle}
            entry = {"kind": "revision" if previous else "initialPromise", "publicationId": f"{rider}-{number}",
                     "publishedPromise": {"publishedAtMs": at, "projection": promise}}
            if previous:
                kept = {"pickupEtaMs": pickup_e, "dropEtaMs": drop_e, "vehicleId": vehicle}
                entry.update(previousPromise={"projection": dict(previous)}, exogenousProjection=kept,
                             deltas=deltas(previous, kept, promise))
            entries.append(entry)
            published[at].append({"publicationId": entry["publicationId"], "promise": dict(promise)})
            previous = promise
        ledger.append({"requestId": rider, "entries": entries})
    for at, rider in ALIGHTED:
        batches[at].append({"eventType": "passengerAlighted", "payload": {"requestId": rider}})
    batches[2000].append({"eventType": "travelTimesUpdated", "payload": {"snapshot": {"arcs": [[0, 1, 5]]}}})
    timed = [(0, 0, "adapterToRunner", {"messageType": "hello", "payload": {}})]
    for at, events in batches.items():
        timed.append((at, 0, "adapterToRunner", {"messageType": "eventBatch", "payload": {"events": events}}))
    for at, promises in published.items():
        actions = [{"decisionType": "promisePublished", "payload": promise} for promise in promises]
        timed.append((at, 1, "runnerToAdapter", {"messageType": "decision",
                                                 "payload": {"decisionHash": f"decision-{at}", "actions": actions}}))
        timed.append((at, 2, "adapterToRunner", {"messageType": "decisionApplied",
                                                 "payload": {"decisionHash": f"decision-{at}"}}))
    ghost = {"publicationId": "ghost-0", "promise": {"pickupEtaMs": 1, "dropEtaMs": 2, "vehicleId": "v1"}}
    unacknowledged = {"decisionHash": "decision-unacknowledged",
                      "actions": [{"decisionType": "promisePublished", "payload": ghost}]}
    timed.append((150, 1, "runnerToAdapter", {"messageType": "decision", "payload": unacknowledged}))
    incidents = {"breaches": [], "latePickups": []}
    early = {"requests": [], "commitmentLedger": [], "incidentLedger": incidents}
    final = {"requests": requests, "commitmentLedger": ledger, "incidentLedger": incidents}
    for at, state in ((3500, early), (8000, final)):
        timed.append((at, 3, "runnerToAdapter", {"messageType": "checkpoint",
                                                 "payload": {"content": {"onlineState": state}}}))
    frames = []
    for at, _, direction, envelope in sorted(timed, key=lambda item: item[:2]):
        frames.append((direction, dict(envelope, simTimeMs=at)))
    return frames


def final_state(frames):
    checkpoints = [envelope for _, envelope in frames if envelope["messageType"] == "checkpoint"]
    return checkpoints[-1]["payload"]["content"]["onlineState"]


def write_job(folder, frames, status="pass"):
    """A job folder with summary.json and transcript-00.ndjson.gz as the adapter writes them."""
    folder.mkdir(parents=True)
    (folder / "summary.json").write_text(json.dumps({"status": status}), encoding="utf-8")
    with gzip.open(folder / "transcript-00.ndjson.gz", "wb") as handle:
        for ordinal, (direction, envelope) in enumerate(frames, 1):
            frame = canonical(envelope)
            record = {"schemaVersion": "1.0.0", "ordinal": ordinal, "direction": direction,
                      "frameLengthBytes": len(frame), "frameSha256": hashlib.sha256(frame).hexdigest(),
                      "frameBase64": base64.b64encode(frame).decode("ascii")}
            handle.write(canonical(record) + b"\n")
    return folder


def write_evidence(folder, table, sum_drop_c, state, alighted):
    """evidence.json.gz and EVIDENCE.sha256 with the fields that riders.py reads."""
    evidence = {"format": "ridebound-job-evidence-v1", "job": folder.name, "role": ROLE,
                "audit": {"table": [list(row) for row in table], "summary": {"sumDropC": sum_drop_c}},
                "finalOnlineState": state, "events": {"boarded": {}, "alighted": alighted}}
    data = gzip.compress(canonical(evidence), mtime=0)
    (folder / "evidence.json.gz").write_bytes(data)
    (folder / "EVIDENCE.sha256").write_text(f"{hashlib.sha256(data).hexdigest()} *evidence.json.gz\n",
                                            encoding="utf-8")


def edit_record(job, number, edit):
    """Change record <number> of the job's transcript with edit(record)."""
    path = job / "transcript-00.ndjson.gz"
    lines = gzip.decompress(path.read_bytes()).splitlines(keepends=True)
    record = json.loads(lines[number])
    edit(record)
    lines[number] = canonical(record) + b"\n"
    path.write_bytes(gzip.compress(b"".join(lines)))


def run(roots, out, *extra):
    argv = [item for root in roots for item in ("--root", str(root))] + ["--out", str(out)] + [str(x) for x in extra]
    with contextlib.redirect_stdout(io.StringIO()):
        return riders.main(argv)


def expected_bytes(header, lines):
    """The file riders.py must write: the header, then the job's lines with its role and cell in front."""
    return (header + "".join(f"{ROLE}\t{CELL}\t{line}\n" for line in lines)).encode("utf-8")


class RiderRowsTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.tmp = pathlib.Path(folder.name)

    def test_rows_and_sequences_from_a_transcript(self):
        write_job(self.tmp / "runs" / JOB, job_frames())
        self.assertEqual(run([self.tmp / "runs"], self.tmp / "riders.tsv", "--sequences", self.tmp / "seq.tsv"), 0)
        self.assertEqual((self.tmp / "riders.tsv").read_bytes(), expected_bytes(riders.HEADER, EXPECTED_ROWS))
        self.assertEqual((self.tmp / "seq.tsv").read_bytes(),
                         expected_bytes(riders.SEQUENCES_HEADER, EXPECTED_SEQUENCES))

    def test_evidence_gives_the_same_rows(self):
        job = write_job(self.tmp / "runs" / JOB, job_frames())
        table, total_c, state, alighted = riders.read_transcript(job / "transcript-00.ndjson.gz")
        self.assertEqual(total_c, 450)
        (job / "transcript-00.ndjson.gz").unlink()
        write_evidence(job, table, total_c, state, alighted)
        run([self.tmp / "runs"], self.tmp / "riders.tsv", "--sequences", self.tmp / "seq.tsv")
        self.assertEqual((self.tmp / "riders.tsv").read_bytes(), expected_bytes(riders.HEADER, EXPECTED_ROWS))
        self.assertEqual((self.tmp / "seq.tsv").read_bytes(),
                         expected_bytes(riders.SEQUENCES_HEADER, EXPECTED_SEQUENCES))
        # A transcript next to the evidence is read instead of it.
        write_job(self.tmp / "other" / JOB, job_frames())
        (self.tmp / "other" / JOB / "evidence.json.gz").write_bytes(b"not read")
        run([self.tmp / "other"], self.tmp / "other.tsv")
        self.assertEqual((self.tmp / "other.tsv").read_bytes(), (self.tmp / "riders.tsv").read_bytes())

    def test_job_selection(self):
        first, second = self.tmp / "first", self.tmp / "second"
        write_job(first / f"FS-G2-SHHZ71-N-{CELL}", job_frames())
        write_job(first / JOB, job_frames())
        write_job(first / "FS-G2-SHHZ71-N-d20190919-vf-r0-w17", job_frames(), status="fail")
        write_job(first / f"FS-G2-RCE35-CL-{CELL}", job_frames())
        (first / "PROGRESS.json").write_text("{}", encoding="utf-8")
        # A broken job whose name was read under the first root: it is not read again.
        (write_job(second / JOB, job_frames()) / "transcript-00.ndjson.gz").write_bytes(b"broken")
        write_job(second / f"{ROLE}-d20190919-vf-r0-w17", job_frames())
        run([first, self.tmp / "missing", second], self.tmp / "riders.tsv")
        keys = [line.split("\t")[:2] for line in (self.tmp / "riders.tsv").read_text(encoding="utf-8").splitlines()]
        riders_per_job = len(EXPECTED_ROWS)
        self.assertEqual(keys[1:], [["FS-G2-SHHZ71-N", CELL]] * riders_per_job + [[ROLE, CELL]] * riders_per_job
                         + [[ROLE, "d20190919-vf-r0-w17"]] * riders_per_job)
        run([first], self.tmp / "rce.tsv", "--arms", "FS-G2-RCE35-CL")
        self.assertEqual(len((self.tmp / "rce.tsv").read_text(encoding="utf-8").splitlines()), 1 + riders_per_job)
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as refused:
            run([first], self.tmp / "rce.tsv")
        self.assertEqual(refused.exception.code, 2)
        # The old reader's switch RB_PREFER_EVIDENCE=1 is refused; any other value changes nothing.
        error = io.StringIO()
        with mock.patch.dict(os.environ, {"RB_PREFER_EVIDENCE": "1"}), contextlib.redirect_stderr(error), \
                self.assertRaises(SystemExit) as refused:
            run([first], self.tmp / "prefer.tsv")
        self.assertEqual(refused.exception.code, 2)
        self.assertIn("RB_PREFER_EVIDENCE=1", error.getvalue())
        self.assertIn("removed; see commit ebca912", error.getvalue())
        with mock.patch.dict(os.environ, {"RB_PREFER_EVIDENCE": "0"}):
            run([first], self.tmp / "prefer.tsv", "--arms", "FS-G2-RCE35-CL")
        self.assertEqual((self.tmp / "prefer.tsv").read_bytes(), (self.tmp / "rce.tsv").read_bytes())

    def refused(self, root, message):
        with self.assertRaises(SystemExit) as caught:
            run([root], root / "riders.tsv")
        self.assertIn(message, str(caught.exception.code))

    def test_transcript_refusals(self):
        def entry(frames, rider, number):
            ledger = final_state(frames)["commitmentLedger"]
            return next(history for history in ledger if history["requestId"] == rider)["entries"][number]

        def bad_delta(frames):
            entry(frames, "rq-a", 1)["deltas"]["decisionInduced"]["dropEtaTotalMs"] += 1

        def broken_chain(frames):  # rq-a's second revision starts from 5000 instead of the published 5100
            item = entry(frames, "rq-a", 2)
            item["previousPromise"]["projection"]["dropEtaMs"] = 5000
            item["deltas"] = deltas(item["previousPromise"]["projection"], item["exogenousProjection"],
                                    item["publishedPromise"]["projection"])

        def float_eta(frames):  # the same non-integer drop ETA in the decision and in the ledger
            decision = next(envelope for _, envelope in frames if envelope["simTimeMs"] == 500
                            and envelope["messageType"] == "decision")
            decision["payload"]["actions"][0]["payload"]["promise"]["dropEtaMs"] = 6000.0
            entry(frames, "rq-f", 0)["publishedPromise"]["projection"]["dropEtaMs"] = 6000.0

        def unacknowledged(frames):
            frames[:] = [frame for frame in frames if frame[1]["messageType"] != "decisionApplied"
                         or frame[1]["payload"]["decisionHash"] != "decision-600"]

        def not_in_ledger(frames):
            final_state(frames)["commitmentLedger"][0]["entries"].pop()

        def other_p(frames):
            entry(frames, "rq-c", 1)["publishedPromise"]["projection"]["dropEtaMs"] += 1

        def no_checkpoint(frames):
            frames[:] = [frame for frame in frames if frame[1]["messageType"] != "checkpoint"]

        def lifecycle_null(frames):  # every lifecycle must be a string, a rider's or not
            next(request for request in final_state(frames)["requests"] if request["requestId"] == "rq-e")[
                "lifecycle"] = None

        cases = [(bad_delta, "|decisionInduced| ledger=201 recomputed=200"), (broken_chain, "breaks the chain"),
                 (float_eta, "is not a canonical integer"), (unacknowledged, "has no acknowledged action"),
                 (not_in_ledger, "not in the final ledger"), (other_p, "action p differs from ledger p"),
                 (no_checkpoint, "no final checkpoint"),
                 (lifecycle_null, "'rq-e' has the lifecycle None, not a string")]
        for edit, message in cases:
            with self.subTest(edit.__name__):
                frames = job_frames()
                edit(frames)
                write_job(self.tmp / edit.__name__ / JOB, frames)
                self.refused(self.tmp / edit.__name__, message)
        record_cases = [("frame hash", lambda record: record.update(frameSha256="0" + record["frameSha256"]),
                         "line 3: frame hash mismatch"),
                        ("frame length", lambda record: record.update(frameLengthBytes=1),
                         "line 3: frameLengthBytes 1 is not the frame's length"),
                        ("no frame length", lambda record: record.pop("frameLengthBytes"),
                         "line 3: frameLengthBytes None is not the frame's length")]
        for name, edit, message in record_cases:
            with self.subTest(name):
                edit_record(write_job(self.tmp / name / JOB, job_frames()), 3, edit)
                self.refused(self.tmp / name, message)
        with self.subTest("uncompressed transcript"):
            job = write_job(self.tmp / "plain" / JOB, job_frames())
            (job / "transcript-00.ndjson").write_bytes(gzip.decompress((job / "transcript-00.ndjson.gz").read_bytes()))
            self.refused(self.tmp / "plain", "reading an uncompressed transcript-00.ndjson: removed; see commit")
        with self.subTest("no cell id"):
            write_job(self.tmp / "nocell" / f"{ROLE}-d20190919-x", job_frames())
            self.refused(self.tmp / "nocell", "no cell id")

    def test_evidence_refusals(self):
        source = write_job(self.tmp / "source" / JOB, job_frames())
        table, total_c, state, alighted = riders.read_transcript(source / "transcript-00.ndjson.gz")

        def evidence_job(name, table, total_c):
            folder = self.tmp / name / JOB
            folder.mkdir(parents=True)
            (folder / "summary.json").write_text('{"status": "pass"}', encoding="utf-8")
            write_evidence(folder, table, total_c, state, alighted)
            return folder

        other = evidence_job("other", table, total_c)
        data = gzip.compress(gzip.decompress((other / "evidence.json.gz").read_bytes()).replace(
            CELL.encode(), b"d20190919-vf-r1-w08"), mtime=0)
        (other / "evidence.json.gz").write_bytes(data)
        (other / "EVIDENCE.sha256").write_text(hashlib.sha256(data).hexdigest(), encoding="utf-8")
        self.refused(other.parent, "not this job's evidence")
        self.refused(evidence_job("total", table, total_c + 1).parent, "per-rider sum |c| != ledger total 451")
        # One more drop revision of rq-a in the table only: its ledger entries no longer add up to its sum of |c|.
        extra = [list(row) for row in table] + [["rq-a", "drop", 9, "revision", 9000, 0, 0, 7, 0, 7, 7, 0]]
        self.refused(evidence_job("running", extra, total_c + 7).parent, "rq-a ledger running sum 250 != 257")
        tampered = evidence_job("tampered", table, total_c)
        (tampered / "EVIDENCE.sha256").write_text("0" * 64, encoding="utf-8")
        self.refused(tampered.parent, "does not match EVIDENCE.sha256")
        state["requests"][0]["lifecycle"] = 3  # rq-e's lifecycle, a number in the evidence
        self.refused(evidence_job("lifecycle", table, total_c).parent, "'rq-e' has the lifecycle 3, not a string")


if __name__ == "__main__":
    unittest.main()
