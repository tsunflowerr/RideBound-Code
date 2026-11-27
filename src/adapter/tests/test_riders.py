"""riders.py on a small hand-made job: rider rows, drop-ETA sequences, job selection and refusals.

The expected rows and sequences are what the old evidence chain (bridge_riders.rows_for and
compare_drop_sequences.real_sequences, read through compare_via_full and evidence_reader) gives for the same job.
"""
import contextlib
import gzip
import hashlib
import io
import json
import pathlib
import sys
import tempfile
import unittest

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
# First alighting time per rider; rq-f has none. rq-g boards when rq-d alights: the two rides only touch, so neither
# is shared.
ALIGHTED = {"rq-a": 5000, "rq-b": 7000, "rq-d": 4000, "rq-g": 6000}
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


def job_evidence():
    """(audit table, ledger total of |c| over the drop ETAs, final online state, first alighting time per rider) of
    the job, as run_job.audit stores them: one row per ledger entry and coordinate, (requestId, coordinate, sequence,
    kind, publishedAtMs, v, e, p, x = e - v, c = p - e, z = p - v, closed), a pickup revision published at or after the
    boarding being closed."""
    requests = [{"requestId": "rq-e", "lifecycle": "rejected", "assignedVehicleId": None, "actualPickupTimeMs": None}]
    ledger, table = [], []
    for rider, (vehicle, lifecycle, boarded, promises) in RIDERS.items():
        requests.append({"requestId": rider, "lifecycle": lifecycle, "assignedVehicleId": vehicle,
                         "actualPickupTimeMs": boarded})
        entries, previous = [], None
        for sequence, (at, pickup, drop) in enumerate(promises):
            kind = "revision" if previous else "initialPromise"
            entries.append({"kind": kind, "publicationId": f"{rider}-{sequence}"})
            for coordinate, (e, p), before in (("pickup", pickup, previous and previous[0]),
                                               ("drop", drop, previous and previous[1])):
                v, e = (p, p) if previous is None else (before, e)
                closed = coordinate == "pickup" and boarded is not None and at >= boarded and previous is not None
                table.append((rider, coordinate, sequence, kind, at, v, e, p, e - v, p - e, p - v, int(closed)))
            previous = (pickup[1], drop[1])
        ledger.append({"requestId": rider, "entries": entries})
    total_c = sum(abs(row[9]) for row in table if row[1] == "drop")
    state = {"requests": requests, "commitmentLedger": ledger}
    return table, total_c, state, dict(ALIGHTED)


def write_evidence(folder, table, sum_drop_c, state, alighted):
    """evidence.json.gz and EVIDENCE.sha256 with the fields that riders.py reads."""
    evidence = {"format": "ridebound-job-evidence-v1", "job": folder.name, "role": ROLE,
                "audit": {"table": [list(row) for row in table], "summary": {"sumDropC": sum_drop_c}},
                "finalOnlineState": state, "events": {"boarded": {}, "alighted": alighted}}
    data = gzip.compress(canonical(evidence), mtime=0)
    (folder / "evidence.json.gz").write_bytes(data)
    (folder / "EVIDENCE.sha256").write_text(f"{hashlib.sha256(data).hexdigest()} *evidence.json.gz\n",
                                            encoding="utf-8")


def write_job(folder, status="pass", evidence=None):
    """A job folder with summary.json and the job's evidence, as the adapter writes them."""
    folder.mkdir(parents=True)
    (folder / "summary.json").write_text(json.dumps({"status": status}), encoding="utf-8")
    write_evidence(folder, *(evidence or job_evidence()))
    return folder


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

    def test_rows_and_sequences(self):
        table, total_c, _, _ = job_evidence()
        self.assertEqual(total_c, 450)
        job = write_job(self.tmp / "runs" / JOB)
        self.assertEqual(run([self.tmp / "runs"], self.tmp / "riders.tsv", "--sequences", self.tmp / "seq.tsv"), 0)
        self.assertEqual((self.tmp / "riders.tsv").read_bytes(), expected_bytes(riders.HEADER, EXPECTED_ROWS))
        self.assertEqual((self.tmp / "seq.tsv").read_bytes(),
                         expected_bytes(riders.SEQUENCES_HEADER, EXPECTED_SEQUENCES))
        # A transcript kept next to the evidence is not read.
        (job / "transcript-00.ndjson.gz").write_bytes(b"not read")
        run([self.tmp / "runs"], self.tmp / "again.tsv")
        self.assertEqual((self.tmp / "again.tsv").read_bytes(), (self.tmp / "riders.tsv").read_bytes())

    def test_job_selection(self):
        first, second = self.tmp / "first", self.tmp / "second"
        write_job(first / f"FS-G2-SHHZ71-N-{CELL}")
        write_job(first / JOB)
        write_job(first / "FS-G2-SHHZ71-N-d20190919-vf-r0-w17", status="fail")
        write_job(first / f"FS-G2-RCE35-CL-{CELL}")
        (first / "PROGRESS.json").write_text("{}", encoding="utf-8")
        # A broken job whose name was read under the first root: it is not read again.
        (write_job(second / JOB) / "evidence.json.gz").write_bytes(b"broken")
        write_job(second / f"{ROLE}-d20190919-vf-r0-w17")
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

    def refused(self, root, message):
        with self.assertRaises(SystemExit) as caught:
            run([root], root / "riders.tsv")
        self.assertIn(message, str(caught.exception.code))

    def test_evidence_refusals(self):
        table, total_c, state, alighted = job_evidence()

        def evidence_job(name, table, total_c):
            return write_job(self.tmp / name / JOB, evidence=(table, total_c, state, alighted))

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
        missing = evidence_job("missing", table, total_c)
        (missing / "evidence.json.gz").unlink()
        self.refused(missing.parent, "no evidence.json.gz")
        nocell = write_job(self.tmp / "nocell" / f"{ROLE}-d20190919-x")
        self.refused(nocell.parent, "no cell id")
        state["requests"][0]["lifecycle"] = 3  # rq-e's lifecycle, a number in the evidence
        self.refused(evidence_job("lifecycle", table, total_c).parent, "'rq-e' has the lifecycle 3, not a string")


if __name__ == "__main__":
    unittest.main()
