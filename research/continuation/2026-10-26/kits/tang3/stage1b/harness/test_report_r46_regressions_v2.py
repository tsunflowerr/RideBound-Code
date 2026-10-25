"""Independent R46 report failure scenarios. Python -B; writes only fresh C: continuation fixtures.

No archived run output is read or changed. The shared checker is exercised from its real authenticated source bytes.
"""
from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import io
import json
import marshal
import pathlib
import struct
import sys
import tempfile
import types
import unittest

sys.dont_write_bytecode = True
BASE = pathlib.Path(r"C:/RideBoundContinuation/2026-10-15")
SOURCE = BASE / "analysis-r46"
WORK = pathlib.Path(tempfile.mkdtemp(prefix="report-r46-", dir=BASE / "reviews"))


def module(path, name):
    result = types.ModuleType(name)
    result.__file__ = str(path)
    exec(compile(path.read_bytes(), str(path), "exec"), result.__dict__)
    return result


rs = module(SOURCE / "report_stage1b.py", "report_r46_test_subject")
ex = module(SOURCE / "extract_stage1b.py", "extract_r46_test_fixture")


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def metadata(mode="dryrun"):
    return {"mode": mode, "dryRun": mode == "dryrun", "roleSources":
            {r: r for r in rs.ROLES} if mode == "main" else
            {"CF-U": "SF-U", "CF-C30": "SF-C30", "SF-U": "SF-U", "SF-C30": "SF-C30"}}


def fixture(name):
    harness = WORK / name
    harness.mkdir()
    for filename in ("extract_stage1b.py", "report_stage1b.py", "runner-tier3-v3.sha256.tsv"):
        (harness / filename).write_bytes((SOURCE / filename).read_bytes())
    identity = ex.runner_receipt_identity((harness / "runner-tier3-v3.sha256.tsv").read_bytes())
    gate = (f"runnerDllSha256 {identity['dll']}\nrunnerTreeSha256 {identity['tree']}\nGATE0 BEHAVIOURAL PASSED\n")
    smoke = (f"runnerDllSha256 {identity['dll']}\nrunnerTreeSha256 {identity['tree']}\nSMOKE PASSED\n")
    (harness / "GATE0-BEHAVIOURAL-PASSED.txt").write_text(gate, encoding="utf-8")
    (harness / "SMOKE-PASSED.txt").write_text(smoke, encoding="utf-8")
    (harness / "SMOKE-INSPECTION.txt").write_text(
        f"smokeVerdictSha256 {sha((harness / 'SMOKE-PASSED.txt').read_bytes())}\n"
        "inspected-by: synthetic regression fixture\ninspection: accepted\n", encoding="utf-8")
    (harness / "STAGE1B-ANALYSIS-AMENDMENT.md").write_text(
        "inventory-reviewed-by: synthetic regression fixture\ninventory-coverage: complete\n", encoding="utf-8")
    (harness / "analysis-extra-test.log").write_text("bound evidence\n", encoding="utf-8")
    names = ("extract_stage1b.py", "report_stage1b.py", "runner-tier3-v3.sha256.tsv",
             "STAGE1B-ANALYSIS-AMENDMENT.md", "analysis-extra-test.log")
    inventory = "".join(f"{sha((harness / n).read_bytes())}  {n}\n" for n in names)
    (harness / "stage1b-analysis-freeze.sha256").write_text(inventory, encoding="utf-8")
    (harness / "stage1b-analysis-binding.txt").write_text(
        f"manifest-sha256 {sha((harness / 'stage1b-analysis-freeze.sha256').read_bytes())}\n{inventory}",
        encoding="utf-8")
    state = ex.require_main_state(harness=harness)
    doc = {**metadata("main"), "studyState": state, "extractor":
           {"file": "extract_stage1b.py", "sha256": sha((harness / "extract_stage1b.py").read_bytes())}}
    return harness, doc


def rider(cell, rid, late=False):
    return {"cell": cell, "id": rid, "life": "completed", "t0": 0, "p0": 600_000, "D0": 600_000.0,
            "A": 660_001 if late else 600_000, "served": True, "S": 0, "k": 0, "net": 0, "maxc": 0,
            "shared": False}


def panel(mode="dryrun"):
    cells = [f"d201811{14 + i % 5:02d}-s10-r{i+1}-{'w08' if i < 25 else 'w07'}" for i in range(34)]
    riders, runs = {r: [] for r in rs.ROLES}, {}
    for role in rs.ROLES:
        for cell in cells:
            riders[role] += [rider(cell, "r1", late=role.endswith("U")), rider(cell, "r2")]
            runs[(role, cell)] = {
                "role": role, "source": metadata(mode)["roleSources"][role], "cell": cell,
                "requests": 108, "served": 2, "promisedRiders": 2,
                "lifecycle": {"completed": 2, "rejected": 106}, "completedWithoutAlight": 0,
                "alightedNotCompleted": 0, "alightedWithoutPromise": 0, "forecastActive": False,
                "pruneClassesOwn": {}, "pruneClassesJudge": {}, "decisionsAcknowledged": 1,
                "judgeAckedDecisions": 1, "dropWitnessLimits": {}, "ledgerBudgetMismatches": [],
                "summaryStatus": "pass", "run00Status": "succeeded", "manifest":
                {"policyConfigurationHash": "a" * 64, "binarySha256": "b" * 64, "coreCommitSha": "c" * 40,
                 "scenarioContentHash": cell}, "repositoryInventorySha256": "d" * 64,
                "requestIdsSha256": cell, "breaches": {}, "breachRecordsToExplain": [],
                "nonNormalDecisionHashes": [], "judgeDecisionsWithCumulativeOnly": 0,
                "judgeRequestsWithCumulativeOnly": 0, "judgeOfWhichFinallyRejected": 0,
                "ridersWithForcedBreach": 0, "latePickups": 0, "cumulativeOnlyOwn": []}
    doc = {**metadata(mode), "extractor": {"file": "synthetic", "sha256": "0" * 64},
           "python": {"executable": "synthetic", "version": "3.11"}, "ridersTsv": {"file": "synthetic"},
           "studyState": {"latches": []}, "cells": cells}
    return doc, riders, runs, cells


def analyse(doc, riders, runs, cells):
    return rs.analyse(doc, riders, runs, cells, baselines={("SF-U", "served"): 68}, sf_w0817=25)


class ReportRegressions(unittest.TestCase):
    def setUp(self):
        self.old_binding = rs.BINDING

    def tearDown(self):
        rs.BINDING = self.old_binding

    def test_strict_mode_and_flag_pairs(self):
        for mode, flag in (("dryrun", False), ("main", True), ("main-typo", False), ("main", 0),
                           ("dryrun", 1), (None, True), ("dryrun", "true")):
            with self.subTest(mode=mode, flag=flag):
                doc = metadata()
                doc.update(mode=mode, dryRun=flag)
                with self.assertRaises(rs.Refusal):
                    rs.require_metadata(doc)

    def test_exact_role_sources(self):
        for mode, key, value in (("main", "CF-U", "SF-U"), ("dryrun", "CF-U", "CF-U"),
                                 ("dryrun", "SF-C30", "SF-C120"), ("dryrun", "CF-C30", None)):
            with self.subTest(mode=mode, key=key):
                doc = metadata(mode)
                doc["roleSources"][key] = value
                with self.assertRaises(rs.Refusal):
                    rs.require_metadata(doc)
        for a in rs.STANDINS:
            for b in rs.STANDINS:
                doc = metadata()
                doc["roleSources"].update({"CF-U": a, "CF-C30": b})
                self.assertEqual(rs.metadata_problems(doc), [])
        doc = metadata()
        doc["roleSources"]["other"] = "SF-U"
        with self.assertRaises(rs.Refusal):
            rs.require_metadata(doc)

    def test_in_memory_analyse_cannot_remove_dryrun_warning(self):
        doc, riders, runs, cells = panel()
        doc["dryRun"] = False
        with self.assertRaises(rs.Refusal):
            analyse(doc, riders, runs, cells)

    def test_load_checks_metadata_before_riders_read(self):
        prefix = WORK / "bad-mode"
        (WORK / "bad-mode-runs.json").write_text(json.dumps({"schema": "stage1b-extract-v1", **metadata(),
                                                              "dryRun": False}), encoding="utf-8")
        with self.assertRaisesRegex(rs.Refusal, "mode/dryRun"):
            rs.load(str(prefix))

    def test_main_and_dryrun_reject_conflicting_recorded_sources(self):
        for mode, wrong in (("main", "SF-U"), ("dryrun", "CF-U")):
            with self.subTest(mode=mode):
                doc, riders, runs, cells = panel(mode)
                runs[("CF-U", cells[0])]["source"] = wrong
                with self.assertRaisesRegex(rs.Refusal, "does not equal roleSources"):
                    analyse(doc, riders, runs, cells)
                prefix = WORK / f"source-conflict-{mode}"
                raw_doc = {**doc, "schema": "stage1b-extract-v1", "runs": list(runs.values())}
                pathlib.Path(f"{prefix}-runs.json").write_text(json.dumps(raw_doc), encoding="utf-8")
                with self.assertRaisesRegex(rs.Refusal, "does not equal roleSources"):
                    rs.load(str(prefix))

    def test_missing_and_nonstring_run_source_or_role_are_refused(self):
        for key, value in (("source", None), ("source", 1), ("source", False), ("role", None), ("role", 1)):
            with self.subTest(key=key, value=value):
                doc, riders, runs, cells = panel()
                runs[("CF-U", cells[0])][key] = value
                with self.assertRaises(rs.Refusal):
                    analyse(doc, riders, runs, cells)
        doc, riders, runs, cells = panel()
        del runs[("CF-U", cells[0])]["source"]
        with self.assertRaises(rs.Refusal):
            analyse(doc, riders, runs, cells)

    def test_zero_denominator_and_empty_promise_guard(self):
        doc, riders, runs, cells = panel()
        riders["CF-C30"] = []
        for cell in cells:
            runs[("CF-C30", cell)].update(served=0, promisedRiders=0, lifecycle={"rejected": 108})
        lines, result = analyse(doc, riders, runs, cells)
        text = "\n".join(lines)
        self.assertIsNone(result["predictions"]["H3"])
        self.assertIn("H3  NOT EVALUABLE", text)
        self.assertFalse(result["predictions"]["H6"])
        for pair, (_, longer) in result["promise_guard"].items():
            if "CF-C30" in pair:
                self.assertIsNone(longer)
                line = next(x for x in lines if x.startswith(f"- {pair[0]} vs {pair[1]}: median horizon"))
                self.assertIn("NOT EVALUABLE", line)
                self.assertNotIn("guard met", line)

    def test_nonfinite_median_never_meets_guard(self):
        old = rs.q
        try:
            for nonfinite in (float("nan"), float("inf"), float("-inf")):
                rs.q = lambda xs, p: nonfinite
                lines, result = analyse(*panel())
                self.assertTrue(all(longer is None for _, longer in result["promise_guard"].values()))
                self.assertEqual(sum("NOT EVALUABLE" in x for x in lines), 4)
        finally:
            rs.q = old

    def test_ledger_mismatch_is_accounting_failure(self):
        doc, riders, runs, cells = panel()
        runs[("CF-C30", cells[0])]["ledgerBudgetMismatches"] = [("r1", 1, "budgetAfter", 0, 20_000)]
        lines, result = analyse(doc, riders, runs, cells)
        self.assertEqual(result["stopped"], "accounting-validity")
        self.assertFalse(result["accounting_validity"])
        self.assertIn("ACCOUNTING VALIDITY FAILED -> STOP", "\n".join(lines))
        self.assertNotIn("predictions", result)

    def test_h1_accounting_is_separate_from_original_cap_and_explanation_threshold(self):
        M = {"CF-C30": {"late60": 0, "served": 2400, "fc_late60": 0, "fc_timely60": 2331, "shared": 345,
                         "requests": 3672},
             "CF-U": {"exposed": 261, "accepted": 2500, "late60": 243, "served": 2500, "fc_timely60": 2300,
                      "h7_served": 10, "requests": 3672},
             "SF-C30": {"late60": 76, "served": 2338, "fc_late60": 10}, "SF-U": {"h7_served": 4}}
        D = {"CF-C30": {"over": 0, "n": 2400, "max_s": 20.0}}
        P = {"CF-C30": {"nonnormal": [], "explain": [], "budget": [("c", "r", 1, "budgetAfter", 0, 20_000)]}}
        lines, v = rs.predictions(M, D, P, 40, 0, 0, 15, 25, {"latches": []}, True)
        self.assertEqual(v["H1"], "PASS")
        self.assertFalse(v["accountingValidity"])
        self.assertIn("ACCOUNTING VALIDITY FAIL", "\n".join(lines))
        P["CF-C30"]["budget"] = []
        P["CF-C30"]["nonnormal"] = [("c", "h")]
        _, v = rs.predictions(M, D, P, 40, 0, 0, 15, 25, {"latches": []}, True)
        self.assertEqual(v["H1"], "PENDING")
        D["CF-C30"]["over"] = 1
        _, v = rs.predictions(M, D, P, 40, 0, 0, 15, 25, {"latches": []}, True)
        self.assertEqual(v["H1"], "FAIL")

    def test_main_h7_anomaly_refuses_but_dryrun_retains_diagnostic(self):
        doc, riders, runs, cells = panel("main")
        runs[("CF-U", cells[0])]["completedWithoutAlight"] = 1
        with self.assertRaisesRegex(rs.Refusal, "completed riders without alight"):
            analyse(doc, riders, runs, cells)
        doc.update(metadata())
        for run in runs.values():
            run["source"] = doc["roleSources"][run["role"]]
        lines, result = analyse(doc, riders, runs, cells)
        self.assertNotIn("stopped", result)
        self.assertIn("completed without alight 1", "\n".join(lines))

    def test_nearest_rank_accepted_horizon_convention_unchanged(self):
        rows = [rider("c", str(i)) for i in range(4)]
        for row, horizon in zip(rows, (100, 300, 200, 400)):
            row.update(p0=horizon, t0=0)
        self.assertEqual(rs.outcome(rows, 4)["horizon_med_acc_ms"], 200)

    def test_genuine_bound_checker_accepts_complete_fixture(self):
        harness, doc = fixture("valid")
        rs.BINDING = harness / rs.BINDING.name
        self.assertEqual(rs.binding_problems(doc), [])

    def test_stop_receipts_after_extraction_are_currently_enforced(self):
        for i, name in enumerate(("INPUT-DRIFT-STOP.txt", "GATE0-BEHAVIOURAL-FAILED.txt", "OTHER-FAILED.txt")):
            harness, doc = fixture(f"latch-{i}")
            rs.BINDING = harness / rs.BINDING.name
            (harness / name).write_text("synthetic stop\n", encoding="utf-8")
            self.assertIn("stop / failure", "\n".join(rs.binding_problems(doc)))

    def test_changed_prerequisites_or_any_inventory_file_refuse(self):
        for i, name in enumerate(("GATE0-BEHAVIOURAL-PASSED.txt", "SMOKE-PASSED.txt", "SMOKE-INSPECTION.txt",
                                   "STAGE1B-ANALYSIS-AMENDMENT.md", "analysis-extra-test.log")):
            harness, doc = fixture(f"changed-{i}")
            rs.BINDING = harness / rs.BINDING.name
            path = harness / name
            path.write_bytes(path.read_bytes() + b"changed\n")
            self.assertTrue(rs.binding_problems(doc), name)

    def test_plausible_receipt_name_with_invalid_content_never_passes(self):
        harness, doc = fixture("bogus-receipt")
        rs.BINDING = harness / rs.BINDING.name
        path = harness / "GATE0-BEHAVIOURAL-PASSED.txt"
        path.write_text("a file exists\n", encoding="utf-8")
        doc["studyState"] = ex.study_state(harness)[0]
        self.assertTrue(rs.binding_problems(doc))

    def test_extractor_cache_cannot_replace_authenticated_checker(self):
        harness, doc = fixture("cache")
        rs.BINDING = harness / rs.BINDING.name
        marker = harness / "cache-code-executed.txt"
        payload = f"from pathlib import Path\nPath({str(marker)!r}).write_text('BAD')\n"
        code = compile(payload, str(harness / "extract_stage1b.py"), "exec")
        stat = (harness / "extract_stage1b.py").stat()
        raw = importlib.util.MAGIC_NUMBER + struct.pack("<III", 0, int(stat.st_mtime), stat.st_size) + marshal.dumps(code)
        cache = pathlib.Path(importlib.util.cache_from_source(str(harness / "extract_stage1b.py")))
        cache.parent.mkdir()
        cache.write_bytes(raw)
        self.assertEqual(rs.binding_problems(doc), [])
        self.assertFalse(marker.exists())

    def test_final_check_refuses_stop_appearing_during_analysis_without_output(self):
        harness, doc = fixture("before-output")
        rs.BINDING = harness / rs.BINDING.name
        old_load, old_analyse = rs.load, rs.analyse
        out = harness / "report.txt"
        try:
            rs.load = lambda prefix: (doc, {}, {}, [])
            def intermediate(*args):
                (harness / "INPUT-DRIFT-STOP.txt").write_text("appeared during reporting\n", encoding="utf-8")
                return ["SHOULD NOT BE WRITTEN"], {}
            rs.analyse = intermediate
            with contextlib.redirect_stdout(io.StringIO()) as captured:
                code = rs.main(["report_stage1b.py", "unused", str(out)])
            self.assertEqual(code, 2)
            self.assertIn("before report output", captured.getvalue())
            self.assertFalse(out.exists())
        finally:
            rs.load, rs.analyse = old_load, old_analyse


if __name__ == "__main__":
    print(f"Fixture directory preserved: {WORK}")
    print(f"report SHA-256: {sha((SOURCE / 'report_stage1b.py').read_bytes())}")
    print(f"extractor SHA-256: {sha((SOURCE / 'extract_stage1b.py').read_bytes())}")
    unittest.main(verbosity=2)
