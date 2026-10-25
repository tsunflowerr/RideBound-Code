"""R46 extractor regressions. Run with the frozen interpreter and -B; all created artifacts stay on C.

This standalone supplement uses actual pinned helper source/factors read-only and synthetic study evidence.
No simulator jobs, original input edits, environment changes, cleanup, or analysis seals are performed.
"""
from __future__ import annotations

import csv
import hashlib
import importlib.util
import io
import json
import pathlib
import subprocess
import sys
import types
import unittest
import uuid
from unittest import mock

SCRIPT = pathlib.Path(__file__).resolve().parent.parent / "analysis-r46" / "extract_stage1b.py"
ARTIFACTS = pathlib.Path(__file__).resolve().parent / ("extract-r46-artifacts-" + uuid.uuid4().hex)
ARTIFACTS.mkdir()


def source_import():
    module = types.ModuleType("extract_r46_test")
    module.__file__ = str(SCRIPT)
    sys.modules[module.__name__] = module
    exec(compile(SCRIPT.read_bytes(), str(SCRIPT), "exec"), module.__dict__)
    return module


ex = source_import()


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def fixture(tag):
    root = ARTIFACTS / (tag + "-" + uuid.uuid4().hex)
    root.mkdir()
    runner = SCRIPT.parent / ex.RUNNER_V3_MANIFEST.name
    (root / runner.name).write_bytes(runner.read_bytes())
    identity = ex.runner_receipt_identity(runner.read_bytes())
    common = f"runnerDllSha256 {identity['dll']}\nrunnerTreeSha256 {identity['tree']}\n"
    (root / ex.STUDY_FILES[0]).write_bytes((common + "GATE0 BEHAVIOURAL PASSED\n").encode())
    smoke = (common + "SMOKE PASSED\n").encode()
    (root / ex.STUDY_FILES[1]).write_bytes(smoke)
    (root / ex.STUDY_FILES[2]).write_bytes(
        f"smokeVerdictSha256 {digest(smoke)}\ninspected-by: synthetic regression fixture\ninspection: accepted\n".encode())
    files = {"extract_stage1b.py": b"# synthetic bound extractor\n",
             "report_stage1b.py": b"# synthetic bound report\n",
             "STAGE1B-ANALYSIS-AMENDMENT.md": b"inventory-reviewed-by: synthetic regression fixture\ninventory-coverage: complete\n",
             "selftest-fixture.log": b"synthetic evidence\n"}
    for name, raw in files.items():
        (root / name).write_bytes(raw)
    names = sorted(list(files) + [runner.name])
    freeze = "".join(f"{digest((root / name).read_bytes())}  {name}\n" for name in names).encode()
    (root / ex.FREEZE.name).write_bytes(freeze)
    (root / ex.BINDING.name).write_bytes(f"manifest-sha256 {digest(freeze)}\n".encode() + freeze)
    return root


class StudyAuthentication(unittest.TestCase):
    def test_valid_exact_receipts_and_full_inventory(self):
        root = fixture("valid")
        state = ex.require_main_state(harness=root)
        self.assertEqual(state, ex.require_main_state(state, harness=root))
        self.assertEqual(429, state["runnerIdentity"]["files"])
        self.assertIn("selftest-fixture.log", state["bindingInventory"])

    def test_arbitrary_verdict_bytes_refused(self):
        for name in ex.STUDY_FILES:
            with self.subTest(name=name):
                root = fixture("arbitrary")
                (root / name).write_bytes(b"not a verdict\n")
                with self.assertRaises(ex.Refusal):
                    ex.require_main_state(harness=root)

    def test_wrong_runner_links_and_missing_terminators_refused(self):
        for name in ex.STUDY_FILES[:2]:
            for mutation in ("dll", "tree", "terminator"):
                with self.subTest(name=name, mutation=mutation):
                    root = fixture("build")
                    path = root / name
                    lines = path.read_text().splitlines()
                    if mutation == "terminator":
                        lines[-1] = "PASSED"
                    else:
                        key = "runnerDllSha256" if mutation == "dll" else "runnerTreeSha256"
                        lines = [key + " " + "0" * 64 if line.startswith(key) else line for line in lines]
                    path.write_bytes(("\n".join(lines) + "\n").encode())
                    with self.assertRaises(ex.Refusal):
                        ex.require_main_state(harness=root)

    def test_smoke_inspection_links_acceptance_and_inspector_refused(self):
        for mutation in ("smokeVerdictSha256", "inspection: accepted", "inspected-by: "):
            with self.subTest(mutation=mutation):
                root = fixture("inspection")
                path = root / ex.STUDY_FILES[2]
                lines = [line for line in path.read_text().splitlines() if not line.startswith(mutation)]
                path.write_bytes(("\n".join(lines) + "\n").encode())
                with self.assertRaises(ex.Refusal):
                    ex.require_main_state(harness=root)

    def test_valid_receipt_replacement_drift_refused(self):
        root = fixture("replacement")
        state = ex.require_main_state(harness=root)
        path = root / ex.STUDY_FILES[0]
        path.write_bytes(b"# replacement retaining correct verdict/build\n" + path.read_bytes())
        self.assertEqual([], ex.study_state(root)[1])
        with self.assertRaisesRegex(ex.Refusal, "retained extraction snapshot"):
            ex.require_main_state(state, harness=root)

    def test_current_failure_or_drift_stop_refused(self):
        for name in (ex.DRIFT_STOP, "GATE0-BEHAVIOURAL-FAILED.txt", "OTHER-FAILED.txt"):
            with self.subTest(name=name):
                root = fixture("stop")
                state = ex.require_main_state(harness=root)
                (root / name).write_bytes(b"synthetic stop\n")
                with self.assertRaisesRegex(ex.Refusal, "stop / failure"):
                    ex.require_main_state(state, harness=root)

    def test_latch_arriving_during_inventory_scan_refused(self):
        root = fixture("late-latch")
        original = ex.verified_bytes

        def add_stop(path, want):
            raw = original(path, want)
            if pathlib.Path(path).name == "selftest-fixture.log":
                (root / ex.DRIFT_STOP).write_bytes(b"arrived during validation\n")
            return raw

        with mock.patch.object(ex, "verified_bytes", side_effect=add_stop):
            with self.assertRaisesRegex(ex.Refusal, "appeared during validation"):
                ex.require_main_state(harness=root)

    def test_nonreader_bound_evidence_drift_refused(self):
        root = fixture("nonreader")
        state = ex.require_main_state(harness=root)
        (root / "selftest-fixture.log").write_bytes(b"modified selftest evidence\n")
        with self.assertRaisesRegex(ex.Refusal, "selftest-fixture.log"):
            ex.require_main_state(state, harness=root)

    def test_duplicate_or_malformed_binding_and_freeze_refused(self):
        for file, addition in ((ex.BINDING.name, b"malformed ignored before R46\n"),
                               (ex.BINDING.name, b"manifest-sha256 " + b"0" * 64 + b"\n"),
                               (ex.FREEZE.name, b"malformed ignored before R46\n")):
            with self.subTest(file=file, addition=addition):
                root = fixture("malformed")
                path = root / file
                path.write_bytes(path.read_bytes() + addition)
                with self.assertRaises(ex.Refusal):
                    ex.require_main_state(harness=root)

    def test_freeze_inventory_must_match_binding_exactly(self):
        root = fixture("inventory-equality")
        freeze = root / ex.FREEZE.name
        # Keep the manifest digest link valid while dropping one retained inventory line.
        raw = b"".join(line for line in freeze.read_bytes().splitlines(keepends=True) if b"selftest-fixture.log" not in line)
        freeze.write_bytes(raw)
        binding = root / ex.BINDING.name
        lines = binding.read_bytes().splitlines(keepends=True)
        lines[0] = f"manifest-sha256 {digest(raw)}\n".encode()
        binding.write_bytes(b"".join(lines))
        with self.assertRaisesRegex(ex.Refusal, "inventory differs"):
            ex.require_main_state(harness=root)


class FinalOutputRefusals(unittest.TestCase):
    def extraction(self, mutation, final_scan=None):
        root = fixture("final-output")
        prefix = root / "result"
        real_require = ex.require_main_state
        role_sources = {role: role for role in ex.ROLES}

        class Pool:
            def __init__(self, **kwargs):
                pass

            def __enter__(self):
                return self

            def __exit__(self, *args):
                return False

            def map(self, reader, tasks):
                records = [([], {"source": source, "cell": cell}) for source, cell, spec in tasks]
                mutation(root)
                return records

        spec = {"job": "CF-U-fixture", "folder": str(root / "input"), "label": "L", "pin": None}
        final_calls = []

        def log_check(*args):
            final_calls.append(args)
            if final_scan is not None:
                final_scan(root, len(final_calls))
            return []

        with mock.patch.multiple(ex, load_cells=lambda: ["d20181114-s10-r1-w08"],
                                 expected_run=lambda *args: dict(spec), folder_problems=lambda *args: [],
                                 cf_log_problems=log_check, unexpected_main_entries=lambda *args: [],
                                 cross_run_problems=lambda *args: [], readers=lambda: {}, ProcessPoolExecutor=Pool,
                                 JOB_PYTHON=pathlib.Path(sys.executable),
                                 require_main_state=lambda expected=None: real_require(expected, harness=root)):
            with self.assertRaises(ex.Refusal):
                ex.extract("main", role_sources, str(prefix))
        self.assertFalse(pathlib.Path(str(prefix) + "-riders.tsv").exists())
        self.assertFalse(pathlib.Path(str(prefix) + "-runs.json").exists())

    def test_new_stop_during_workers_leaves_no_result(self):
        self.extraction(lambda root: (root / ex.DRIFT_STOP).write_bytes(b"new stop\n"))

    def test_new_stop_during_last_final_cf_log_scan_leaves_no_result(self):
        def stop_during_scan(root, count):
            if count == 2:       # both CF arms reach the final scan; initial scan skips the synthetic absent folders
                (root / ex.DRIFT_STOP).write_bytes(b"new stop during last final CF log scan\n")
        self.extraction(lambda root: None, final_scan=stop_during_scan)

    def test_wrong_interpreter_refuses_before_invalid_study_state(self):
        prefix = ARTIFACTS / "wrong-interpreter"
        with mock.patch.multiple(ex, load_cells=lambda: ["d20181114-s10-r1-w08"],
                                 JOB_PYTHON=ARTIFACTS / "not-current-python.exe"), \
                mock.patch.object(ex, "require_main_state", side_effect=AssertionError("study checked too early")):
            with self.assertRaisesRegex(ex.Refusal, "frozen job interpreter"):
                ex.extract("main", {role: role for role in ex.ROLES}, str(prefix))
        self.assertFalse(pathlib.Path(str(prefix) + "-riders.tsv").exists())
        self.assertFalse(pathlib.Path(str(prefix) + "-runs.json").exists())

    def test_replaced_valid_receipt_during_workers_leaves_no_result(self):
        def mutate(root):
            path = root / ex.STUDY_FILES[0]
            path.write_bytes(b"# valid but changed\n" + path.read_bytes())
        self.extraction(mutate)

    def test_full_bound_inventory_drift_during_workers_leaves_no_result(self):
        self.extraction(lambda root: (root / "selftest-fixture.log").write_bytes(b"changed\n"))

    def test_persistent_pinned_data_drift_during_workers_leaves_no_result(self):
        pin = ARTIFACTS / "synthetic-pinned-factor.csv"
        pin.write_bytes(b"verified data\n")
        with mock.patch.dict(ex.PINNED_DATA, {str(pin): digest(pin.read_bytes())}):
            self.extraction(lambda root: pin.write_bytes(b"persistent changed bytes\n"))


class ExecutedAndConsumedBytes(unittest.TestCase):
    def child(self, code):
        source = ARTIFACTS / ("probe-" + uuid.uuid4().hex + ".py")
        source.write_bytes(code.encode())
        completed = subprocess.run([sys.executable, "-B", str(source)], capture_output=True, text=True,
                                   encoding="utf-8", cwd=str(SCRIPT.parent))
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        print(completed.stdout.strip())

    def test_all_ten_valid_poisoned_caches_bypassed_and_forecast_unchanged(self):
        scratch = ARTIFACTS / "source-copies"
        for relative, want in ex.PINNED_MODULES.values():
            path = scratch / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(ex.verified_bytes(ex.SCRATCH / relative, want))
        head = f"""import sys, pathlib, types, importlib.util, importlib.machinery, importlib._bootstrap_external as be, io, csv, json
from unittest import mock
source = pathlib.Path({str(SCRIPT)!r})
ex = types.ModuleType('fresh_extractor')
ex.__file__ = str(source)
sys.modules[ex.__name__] = ex
exec(compile(source.read_bytes(),str(source),'exec'),ex.__dict__)
ex.SCRATCH = pathlib.Path({str(scratch)!r})
ex.READER_PATHS = tuple(ex.SCRATCH / p for p in ('tang3/evidence','optimize','optimize/codex-debate/claude-work/r13-final-judge'))
valid = 0
for name,(relative,want) in ex.PINNED_MODULES.items():
    path = ex.SCRATCH / relative
    stat = path.stat()
    poison = 'poisoned-cache-' + name
    code = compile(path.read_bytes() + ('\\nCACHE_POISON = ' + repr(poison) + '\\n').encode(),str(path),'exec')
    cache = pathlib.Path(importlib.util.cache_from_source(str(path)))
    cache.parent.mkdir(parents=True,exist_ok=True)
    cache.write_bytes(be._code_to_timestamp_pyc(code,int(stat.st_mtime),stat.st_size))
    assert poison in importlib.machinery.SourceFileLoader(name,str(path)).get_code(name).co_consts
    valid += 1
assert valid == 10
R = ex.readers()
assert len(R['verifiedSources']) == 10
assert all(not hasattr(sys.modules[name],'CACHE_POISON') for name in ex.PINNED_MODULES)
dl = R['display_layer']
before = dl.display(0,1800000,dl.profile('2018-11-14','w08','causal-no15'))
assert before == 1847580.4741262272, before
v13 = sys.modules['preflight_world_v1_3']
frozen_source = v13.preflight_world
original_open = pathlib.Path.open
disk_reads = []
def transient_open(path,*args,**kwargs):
    if ex.norm(path).startswith(ex.norm(ex.FACTORS)+__import__('os').sep):
        disk_reads.append(str(path))
        raw = R['verifiedData'][ex.norm(path)]
        rows = list(csv.DictReader(io.StringIO(raw.decode())))
        for row in rows:
            row['travel_time_factor'] = str(float(row['travel_time_factor']) * (10 if int(row['simulation_time']) >= 29700 else 1))
        output = io.StringIO()
        writer = csv.DictWriter(output,fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
        return io.StringIO(output.getvalue())
    return original_open(path,*args,**kwargs)
with mock.patch.object(pathlib.Path,'open',transient_open):
    v13.preflight_world = frozen_source.original
    vulnerable = dl.display(0,1800000,dl.profile('2018-11-14','w08','causal-no15'))
    assert vulnerable != before
    assert len(disk_reads) == 2
    v13.preflight_world = frozen_source
    disk_reads.clear()
    after = dl.display(0,1800000,dl.profile('2018-11-14','w08','causal-no15'))
    assert after == before
    assert disk_reads == []
    assert ex.readers() is R
print(json.dumps({{'valid_poisoned_caches':valid,'verified_source_modules':len(R['verifiedSources']),
                   'D0_ms':before,'transient_vulnerable_D0_ms':vulnerable,'frozen_D0_ms':after,
                   'disk_factor_reads_after_freezing':len(disk_reads)}}))
"""
        self.child(head)

    def test_already_loaded_pinned_module_cannot_claim_verified_execution(self):
        self.child(f"""import sys,pathlib,types
source=pathlib.Path({str(SCRIPT)!r})
ex=types.ModuleType('fresh_extractor'); ex.__file__=str(source)
exec(compile(source.read_bytes(),str(source),'exec'),ex.__dict__)
fake=types.ModuleType('display_layer');fake.__file__=str(ex.SCRATCH/ex.PINNED_MODULES['display_layer'][0])
sys.modules['display_layer']=fake
try:
    ex.readers()
except ex.Refusal as error:
    assert 'already loaded' in str(error)
    print('preloaded unverified module refused')
else:
    raise AssertionError('preloaded module accepted')
""")

    def test_source_snapshot_execution_ignores_later_path_changes(self):
        path = ARTIFACTS / "verified-snapshot.py"
        raw = b"VALUE = 7\n"
        path.write_bytes(raw)
        retained = ex.verified_bytes(path, digest(raw))
        loader = ex._VerifiedSourceLoader(path, retained, {})
        path.write_bytes(b"VALUE = 999\n")
        module = types.ModuleType("snapshot")
        loader.exec_module(module)
        self.assertEqual(7, module.VALUE)
        with self.assertRaises(ex.Refusal):
            ex.verified_bytes(path, digest(raw))


if __name__ == "__main__":
    if not sys.dont_write_bytecode:
        raise SystemExit("Run with -B; do not create interpreter cache files")
    print("artifacts:", ARTIFACTS)
    unittest.main(verbosity=2)
