"""replay.py on transcripts recorded here through the real session and client against the fake Runner."""
from __future__ import annotations

import os
import pathlib
import shutil
import sys
import tempfile
import unittest


ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

import replay  # noqa: E402
from ridebound_fleetpy import session  # noqa: E402

FAKE = pathlib.Path(__file__).parent / "fixtures" / "fake_runner.py"
# An embedded interpreter (LibreOffice's, for one) reports a sys.executable that is not a file.
PYTHON = sys.executable if os.path.isfile(sys.executable) else os.path.join(sys.prefix, "bin", "python.exe")
replay.PYTHON = PYTHON


def settings(folder, transcript, runner, commit="b" * 40):
    unused = folder / "unused"
    return session.RideBoundSessionSettings(
        dotnet=pathlib.Path(PYTHON), runner_root=folder, runner_dll=runner, commitment_config=unused,
        wp4_config=unused, fleetpy_root=unused, repository_root=unused, run_id="run-1", scenario_id="scenario-1",
        master_seed=1, service_class="service", commitment_policy_id="policy", timeout_seconds=30,
        complete_travel_snapshot_maximum_nodes=2, transcript_path=transcript, policy_id="policy-id",
        policy_version="policy-version", policy_binding_hash="a" * 64, core_commit=commit, artifact_paths=(runner,),
    )


def job(settings, last_time=2000):
    """A small adapter run: hello, initialize, two epochs with ACKs and a checkpoint."""
    protocol = session.RideBoundProtocolSession(settings)
    try:
        protocol.initialize({"snapshotHash": "c" * 64}, ["n-1", "n-2"], ["r-1"], ["v-1"])
        for epoch, time_ms in ((1, 1000), (2, last_time)):
            batch = {"schemaVersion": "1.0.0", "messageType": "eventBatch", "runId": "run-1",
                     "scenarioId": "scenario-1", "epochId": epoch, "simTimeMs": time_ms, "payload": {"events": []}}
            protocol.acknowledge(protocol.decide(batch))
        protocol.checkpoint()
    finally:
        protocol.close()


class ReplayTests(unittest.TestCase):
    def setUp(self):
        self.folder = pathlib.Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.folder, True)
        self.rebuilt = self.folder / "fake_runner_rebuilt.py"  # another "build": binarySha256 differs
        self.rebuilt.write_bytes(FAKE.read_bytes() + b"# rebuilt\n")

    def record(self, name, **options):
        transcript = self.folder / name
        job(settings(self.folder, transcript, FAKE), **options)
        return transcript

    def test_runner_mode_replays_plain_and_gzip_transcripts(self):
        for name in ("plain.ndjson", "zipped.ndjson.gz"):
            with self.subTest(name):
                result = replay.replay_runner([PYTHON, str(FAKE)], self.record(name))
                self.assertTrue(result["identical"], result)
                self.assertEqual((result["framesSent"], result["framesCompared"], result["framesIdentical"]),
                                 (8, 5, 5))
                self.assertEqual(result["runnerExitCode"], 0)

    def test_runner_mode_stops_at_the_first_differing_runner_frame(self):
        result = replay.replay_runner([PYTHON, str(FAKE), "node-only"], self.record("base.ndjson"))
        self.assertFalse(result["identical"])
        difference = result["firstDifference"]
        self.assertEqual((difference["ordinal"], difference["messageType"]), (2, "helloAck"))
        self.assertIn("directedEdgeProgress", difference["recorded"])
        self.assertIn("nodeOnly", difference["replayed"])
        self.assertEqual(result["framesCompared"], 1)

    def test_adapter_mode_rebinds_only_the_build_identity(self):
        recorded = self.record("base.ndjson.gz")
        again = self.folder / "again.ndjson"
        result = replay.run_adapter(recorded, lambda: job(settings(self.folder, again, self.rebuilt, "d" * 40)))
        self.assertTrue(result["identical"], result)
        self.assertTrue(result["initializeRunRebound"])
        self.assertEqual((result["framesChecked"], result["job"]), (13, "returned"))
        # The replayed run's own transcript differs from the recording only in the initializeRun frame.
        compared = replay.compare_transcripts(recorded, again)
        self.assertEqual((compared["lines"], compared["differing"], compared["strippedByMessageType"]),
                         (13, 0, {"initializeRun": 1}))

    def test_adapter_mode_reports_the_first_differing_adapter_frame(self):
        recorded = self.record("base.ndjson")
        result = replay.run_adapter(recorded, lambda: job(settings(self.folder, None, FAKE), last_time=2500))
        self.assertFalse(result["identical"])
        self.assertFalse(result["initializeRunRebound"])
        self.assertEqual((result["firstDifference"]["ordinal"], result["firstDifference"]["messageType"]),
                         (8, "eventBatch"))
        self.assertTrue(result["job"].startswith("ReplayDifference"), result["job"])

    def test_adapter_mode_fails_a_job_that_ends_early(self):
        result = replay.run_adapter(self.record("base.ndjson"), lambda: None)
        self.assertFalse(result["identical"])
        self.assertEqual(result["firstDifference"]["reason"], "the job ended before the transcript")

    def test_compare_mode_ignores_hashes_and_build_identity_only(self):
        recorded = self.record("a.ndjson")
        job(settings(self.folder, self.folder / "b.ndjson.gz", self.rebuilt, "d" * 40))
        job(settings(self.folder, self.folder / "c.ndjson", FAKE), last_time=2500)
        same = replay.compare_transcripts(recorded, self.folder / "b.ndjson.gz")
        self.assertEqual((same["sameBytes"], same["sameAfterStrip"], same["differing"]), (12, 1, 0))
        other = replay.compare_transcripts(recorded, self.folder / "c.ndjson")
        self.assertEqual((other["differing"], other["firstDifferingLine"]), (3, 8))  # eventBatch, decision, ACK

    def test_compare_cli_pairs_job_folders_under_two_roots(self):
        for root, options in (("old", {}), ("new", {"commit": "d" * 40})):
            (self.folder / root / "job-1").mkdir(parents=True)
            (self.folder / root / "job-1" / "summary.json").write_text("{}")
            transcript = self.folder / root / "job-1" / "transcript-00.ndjson.gz"
            job(settings(self.folder, transcript, FAKE, **options))
        out = self.folder / "report.json"
        self.assertEqual(replay.main(["compare", str(self.folder / "old"), str(self.folder / "new"),
                                      "--out", str(out)]), 0)
        self.assertIn('"differing": 0', out.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
