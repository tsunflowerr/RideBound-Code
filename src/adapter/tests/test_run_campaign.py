"""run_campaign.py: the job lists of the final runs and the rule that skips jobs which passed before."""
import contextlib
import hashlib
import io
import json
import pathlib
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

import run_campaign  # noqa: E402

# The days of the two panels; every day has the cells r0, r1, r2 x w08, w17.
F14_DAYS = ("20190919", "20190924", "20191003", "20191007", "20191009", "20191015", "20191018", "20191024",
            "20191205", "20191209", "20191210", "20191213", "20191216", "20191218")
F16_DAYS = ("20190920", "20190926", "20190930", "20191002", "20191008", "20191010", "20191011", "20191016",
            "20191017", "20191023", "20191025", "20191028", "20191029", "20191204", "20191217", "20191219")
# sha256 of the final job lists as the old job-list scripts wrote them (JSON, indent 2, newline at the end),
# with the data folder written as <data>: 168 F14 jobs and 192 F16 jobs.
F14_FINAL_SHA256 = "e56dc0a75fd338df1504134fd3de7ec855d7d9129d28b663389fb0b184c3d6a6"
F16_FINAL_SHA256 = "94a99fd6fa8bb7680c1886bb555a448bbdf025cdeb9205d34a4f6cee2fac5dd6"


def cells(days):
    return [f"d{day}-vf-r{r}-w{w}" for day in days for r in range(3) for w in ("08", "17")]


def make_data(root, days, forecast_gate=False):
    """A panel data folder whose input files are empty."""
    files = ["job-configs-v2/commitment/SHRC71.json", "job-configs-v2/commitment/SHHZ71.json"]
    for cell in cells(days):
        files += [f"drivers-run/{cell}.driver.json", f"worlds/{cell}.world.json",
                  f"job-configs-v2/wp4/FS-G2-SHRC71-CL/{cell}.json", f"job-configs-v2/wp4/FS-G2-SHHZ71-N/{cell}.json"]
        files += [f"fixtures/{cell}/{name}.json" for name in run_campaign.FIXTURE_FILES]
    if forecast_gate:  # F14 only: the forecast gate arm exists for the two gate cells
        files += ["job-configs-v2/commitment/RCE35.json", "job-configs-v2/wp4/FS-G2-RCE35-CL/d20190919-vf-r0-w08.json",
                  "job-configs-v2/wp4/FS-G2-RCE35-CL/d20190919-vf-r0-w17.json"]
    for name in files:
        (root / name).parent.mkdir(parents=True, exist_ok=True)
        (root / name).write_text("{}\n", encoding="utf-8")
    return root


def list_sha256(jobs, root):
    text = (json.dumps(jobs, indent=2) + "\n").replace(root.as_posix(), "<data>")
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


class JobListTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        folder = tempfile.TemporaryDirectory()
        cls.addClassCleanup(folder.cleanup)
        cls.f14 = make_data(pathlib.Path(folder.name) / "f14", F14_DAYS, forecast_gate=True)
        cls.f16 = make_data(pathlib.Path(folder.name) / "f16", F16_DAYS)

    def hide(self, path):
        """Remove an input file for this test only."""
        hidden = path.with_name(path.name + ".hidden")
        path.rename(hidden)
        self.addCleanup(hidden.rename, path)

    def test_final_lists_equal_the_old_job_lists(self):
        f14 = run_campaign.make_jobs("f14", self.f14)
        f16 = run_campaign.make_jobs("f16", self.f16)
        self.assertEqual((len(f14), len(f16)), (168, 192))
        self.assertEqual(list_sha256(f14, self.f14), F14_FINAL_SHA256)
        self.assertEqual(list_sha256(f16, self.f16), F16_FINAL_SHA256)

    def test_one_job_in_full(self):
        root = self.f14.as_posix()
        cell = "d20190919-vf-r0-w17"
        job = run_campaign.make_jobs("f14", self.f14, "gate", ["FS-G2-SHHZ71-CL"])[1]
        self.assertEqual(job, {
            "name": f"FS-G2-SHHZ71-CL-{cell}", "label": f"FS-G2-SHHZ71-CL-{cell}-fin2", "role": "FS-G2-SHHZ71-CL",
            "config": f"{root}/job-configs-v2/commitment/SHHZ71.json",
            "wp4": f"{root}/job-configs-v2/wp4/FS-G2-SHRC71-CL/{cell}.json",
            "fixture": f"{root}/fixtures/{cell}", "driver": f"{root}/drivers-run/{cell}.driver.json",
            "world": f"{root}/worlds/{cell}.world.json", "transcript": "keep"})

    def test_gate_lists_and_transcripts(self):
        def names(panel, data, arms):
            return [job["name"] for job in run_campaign.make_jobs(panel, data, "gate", arms)]

        self.assertEqual(names("f16", self.f16, run_campaign.MAIN_ARMS), [
            "FS-G2-SHRC71-CL-d20190920-vf-r0-w08", "FS-G2-SHHZ71-N-d20190920-vf-r0-w08",
            "FS-G2-SHRC71-CL-d20190920-vf-r0-w17", "FS-G2-SHHZ71-N-d20190920-vf-r0-w17"])
        self.assertEqual(names("f14", self.f14, ["FS-G2-RCE35-CL"]), [
            "FS-G2-RCE35-CL-d20190919-vf-r0-w08", "FS-G2-RCE35-CL-d20190919-vf-r0-w17"])
        self.assertEqual(names("f14", self.f14, ["FS-G2-SHHZ71-CL", "FS-G2-SHRC71-N"]), [
            "FS-G2-SHHZ71-CL-d20190919-vf-r0-w08", "FS-G2-SHRC71-N-d20190919-vf-r0-w08",
            "FS-G2-SHHZ71-CL-d20190919-vf-r0-w17", "FS-G2-SHRC71-N-d20190919-vf-r0-w17"])
        kept = [job["name"] for job in run_campaign.make_jobs("f16", self.f16) if job["transcript"] == "keep"]
        self.assertEqual(kept, names("f16", self.f16, run_campaign.MAIN_ARMS))

    def test_dry_run_prints_the_command_and_environment(self):
        stdout = io.StringIO()
        with contextlib.redirect_stdout(stdout):
            code = run_campaign.main(["--panel", "f16", "--data", str(self.f16), "--out", "runs", "--logs", "logs",
                                      "--fleetpy-root", "fp", "--runner-root", "rr", "--dotnet", "dn",
                                      "--cells", "gate", "--dry-run"])
        lines = [json.loads(line) for line in stdout.getvalue().splitlines()]
        self.assertEqual((code, len(lines)), (0, 4))
        first = lines[0]
        self.assertEqual(first["env"], {"PYTHONDONTWRITEBYTECODE": "1", "PYTHONIOENCODING": "utf-8",
                                        "DOTNET_GCHeapHardLimit": "0x180000000"})
        argv = first["command"]
        self.assertEqual(argv[1:3], ["-B", str(ADAPTER_ROOT / "run_job.py")])
        options = dict(zip(argv[3::2], argv[4::2]))
        self.assertEqual(len(argv), 3 + 2 * len(options))  # no option given twice
        self.assertEqual(options, {
            "--label": "FS-G2-SHRC71-CL-d20190920-vf-r0-w08-f16", "--role": "FS-G2-SHRC71-CL",
            "--fleetpy-root": "fp", "--runner-root": "rr", "--dotnet": "dn",
            "--commitment-config": first["config"], "--wp4-config": first["wp4"],
            "--scenario": first["fixture"] + "/scenario-content.json",
            "--derivative-manifest": first["fixture"] + "/derivative-manifest.json",
            "--normalization-report": first["fixture"] + "/normalization-report.json",
            "--selection-frame": first["fixture"] + "/selection-frame.json",
            "--driver": first["driver"], "--world": first["world"], "--master-seed": "7", "--transcript": "keep",
            "--output": str(pathlib.Path("runs") / "FS-G2-SHRC71-CL-d20190920-vf-r0-w08")})

    def test_incomplete_data_and_unknown_arms_are_refused(self):
        with self.assertRaisesRegex(SystemExit, "missing .*RCE35.json"):
            run_campaign.make_jobs("f16", self.f16, "gate", ["FS-G2-RCE35-CL"])
        self.hide(self.f16 / "job-configs-v2/wp4/FS-G2-SHHZ71-N/d20191219-vf-r2-w17.json")
        with self.assertRaisesRegex(SystemExit, "missing .*d20191219-vf-r2-w17.json"):
            run_campaign.make_jobs("f16", self.f16)
        self.hide(self.f16 / "drivers-run/d20191219-vf-r2-w17.driver.json")
        with self.assertRaisesRegex(SystemExit, "expected 96 cells"):
            run_campaign.make_jobs("f16", self.f16)
        for arms in ("FS-G2-XX-CL", "FS-G2-SHRC71-CL,FS-G2-SHRC71-CL"):
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as caught:
                run_campaign.main(["--panel", "f14", "--data", str(self.f14), "--out", "o", "--logs", "l",
                                   "--fleetpy-root", "f", "--runner-root", "r", "--dotnet", "d", "--arms", arms])
            self.assertEqual(caught.exception.code, 2)


class ResumeTests(unittest.TestCase):
    """The queue with run_job.py replaced by a stub that writes summary.json the way a job does."""

    @classmethod
    def setUpClass(cls):
        folder = tempfile.TemporaryDirectory()
        cls.addClassCleanup(folder.cleanup)
        cls.data = make_data(pathlib.Path(folder.name), F16_DAYS)

    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.root = pathlib.Path(folder.name)
        self.out = self.root / "runs"
        self.calls = []
        self.status = {}  # job name -> status its stub run writes; no entry: the job writes no summary

    def fake_run(self, argv, **options):
        output = pathlib.Path(argv[argv.index("--output") + 1])
        self.calls.append((output.name, options["env"]))
        if output.name in self.status:
            output.mkdir(parents=True)
            (output / "summary.json").write_text(json.dumps({"status": self.status[output.name]}), encoding="utf-8")
        return subprocess.CompletedProcess(argv, 0 if output.name in self.status else 3, stdout="job output\n")

    def campaign(self, logs):
        argv = ["--panel", "f16", "--data", str(self.data), "--out", str(self.out), "--logs", str(self.root / logs),
                "--fleetpy-root", "f", "--runner-root", "r", "--dotnet", "d", "--cells", "gate", "--workers", "2"]
        with mock.patch.object(run_campaign.subprocess, "run", self.fake_run), \
                contextlib.redirect_stdout(io.StringIO()):
            return run_campaign.main(argv)

    def test_passed_reads_only_a_summary_with_status_pass(self):
        folder = self.root / "job"
        self.assertFalse(run_campaign.passed(folder))
        folder.mkdir()
        for text, expected in (("{", False), ('{"status": "PASS"}', False), ('{"status": "failed"}', False),
                               ('{"status": "pass"}', True)):
            (folder / "summary.json").write_text(text, encoding="utf-8")
            self.assertIs(run_campaign.passed(folder), expected)

    def test_rerun_skips_passed_jobs_and_keeps_interrupted_folders(self):
        names = [job["name"] for job in run_campaign.make_jobs("f16", self.data, "gate")]
        (self.out / names[0]).mkdir(parents=True)
        (self.out / names[0] / "summary.json").write_text('{"status": "pass"}', encoding="utf-8")
        (self.out / names[1]).mkdir()
        (self.out / names[1] / "summary.json").write_text('{"status": "failed"}', encoding="utf-8")
        (self.out / f"{names[1]}.interrupted-1").mkdir()
        self.status = {names[1]: "pass", names[2]: "pass"}

        self.assertEqual(self.campaign("logs-1"), 1)  # names[3] writes no summary
        self.assertEqual(sorted(name for name, _ in self.calls), sorted(names[1:]))
        self.assertTrue(all(env["DOTNET_GCHeapHardLimit"] == "0x180000000" for _, env in self.calls))
        self.assertTrue((self.out / f"{names[1]}.interrupted-2" / "summary.json").is_file())
        self.assertTrue(run_campaign.passed(self.out / names[1]))
        logs = sorted((self.root / "logs-1").iterdir())
        self.assertEqual(len(logs), 3)
        self.assertTrue(all(log.read_text(encoding="utf-8").startswith("returncode=") for log in logs))

        self.calls.clear()
        self.status[names[3]] = "pass"
        self.assertEqual(self.campaign("logs-2"), 0)
        self.assertEqual([name for name, _ in self.calls], [names[3]])


if __name__ == "__main__":
    unittest.main()
