# Stage 1b harness v7 — changes from v6 (Claude, harness fixer; 2026-10-14)

Basis: Codex R45 rated harness v6 READY TO SEAL (`optimize/codex-debate/codex-work/R45-HARNESS-CHECK-V6-CODEX.md`). v7
makes only the coordinator's listed changes: (1) the owner's C: disk floor, (2) the §11 launch-environment wording,
(3) the §0 start-rule facts and the coordinator's runner-commit facts, (4) the optional R45-1 one-line fix. The version
cascade then requires all five harness files to get v7 names (the driver seals and loads its own version; the comparator
loads the driver; the test targets the version; the plan and the template name the files). **No v6 or older file was
edited.** The provenance receipt `RUNNER-PROVENANCE-v3.md` was not created or filled (the coordinator fills it).

**Run:** only the pure self-test and the `plan` dry check. No job, no .NET, no publish, commit, freeze or seal; Codex and
Antigravity were not launched; nothing was written to `E:/Code/RideBound*` or `C:/Code/*`. Read-only check of the
coordinator's commit facts: in `C:/Code/RideBound-costfirst`, `git -c core.autocrlf=false diff 761915c dce84ec |
sha256sum` = c48042af4e7cee2db01bdee5efb804dac703d80c8c657276fc5718816e1ad690; `dce84ec` is on `research/tier3-cost-first`
and in no remote branch; `harness/dotnet-test-costfirst-dce84ec-2026-10-14.log` has SHA-256
5b845e7bce2cb45fd80e7cef7b116b0041aafcb9bd87feb7251f4701ec54cc55, passed counts 10+191+138+108+82+14+135+208+147 = 1,033
and one failure, `StrictBagItBundleTests.Source_inventory_binds_dirty_worktree_files_not_only_base_commit` (log :65-66).

## 1. Summary per file

| File | Changes v6 → v7 |
|---|---|
| `run_stage1b-v7.py` | docstring "VERSION 7" + "New in v7"; `FLOOR_MIB, NEED_MIB = 46_080, 410` (was 51_200); R45-1: `anchor-exists-pending` counted with `log-exists-pending` as unfinished, so `run` returns 3, not 0; version names: loads `compare_equivalence_stage1b-v7.py`, `SEALED_REQUIRED` names the v7 driver / comparator / test and `selftest-2026-10-14-v7.log`, usage, lock, drift-receipt, binding and freeze headers, analysis-freeze message "plan v7 section 12" (32 changed lines in total) |
| `compare_equivalence_stage1b-v7.py` | only the driver it loads (`run_stage1b-v7.py`), its docstring, usage and report header; every comparison, report and outcome rule unchanged (19 changed lines) |
| `test_compare_equivalence_stage1b-v7.py` | targets v7 (default, sections A–G) and v6 (section G only); v7 added to the version branches; F6-7 log date; R38-P-c and R41-5-e check the v7 wording / arithmetic when the target is v7 (the owner changed those plan texts); new section G: R45-1-a/b/c, V7-a (floor), V7-b (deadline, 21/22 days, READY), V7-c (commit facts), V7-d (launch wording verbatim) (160 changed lines) |
| `STAGE1B-PLAN-DRAFT-v7.md` | title/status v7 and history; §0: READY TO SEAL by Codex R45 (for v6) replaces "Codex has not rated the harness READY"; commit `dce84ec…` (not pushed); deadline on or after 2026-11-05 (owner, 2026-10-14); launch 2026-10-14 or 2026-10-15 with 22 / 21 days; §2: runner-source commit, reviewed-diff equality, dotnet test log; §10: disk floor 46,080 MiB with the owner decision; §11: the coordinator's launch-environment wording verbatim (driver checks unchanged); §12: R45-1 exit status; v7 file names (§1.1, §2, §3, §4, §10, §13) |
| `RUNNER-PROVENANCE-TEMPLATE-v7.md` | title v7 and the v7 harness file names only |

## 2. Results

- `selftest-2026-10-14-v7.log` (`--target v7`): **333 OK, 0 BAD, 2 LIMIT**, exit 0, 1532 s; = the v6 count 326 + 7 new
  section-G controls. Header (log :2-6) equals the v7 hashes below. The two LIMITs are the accepted N1 and R38-1 cases.
- Section G against v6 (`--target v6`, no log file written): 1 OK (R45-1-c positive), 6 BAD (R45-1-a/b returned 0;
  floor 51,200; v6 plan lacks the v7 facts and wording), as expected.
- `plan-dryrun-2026-10-14-v7.txt`: exit 0 for gate0 / smoke / main (4 / 2 / 68 pending); "floor 46080 MiB + 410 MiB
  reserved per running job", C: free 54,507 MiB. The runner manifest now exists and the published tree verifies (the
  only runner problem left is the missing filled `RUNNER-PROVENANCE-v3.md`); the seal and input freeze do not exist yet;
  the agent shell's `GIT_EDITOR` is flagged (the launcher removes it). Harness listing unchanged apart from the new
  file; no output root, no `logs/`; worktree index mtime unchanged.

## 3. SHA-256 of every v7 file

| File | SHA-256 |
|---|---|
| `run_stage1b-v7.py` | 7495dd515154e81932ce61ea537f648739d940099b37425d0eb6b58f34e64811 |
| `compare_equivalence_stage1b-v7.py` | 8fb8c6a614db3cedd43488e4525e6492c6b7ee95c68ee435162a308eb75929da |
| `test_compare_equivalence_stage1b-v7.py` | 7d2267df3d25f0cea1bc03955f85069416e57a723a5e80f73036cd4c522c44e7 |
| `STAGE1B-PLAN-DRAFT-v7.md` | b6269a8c3d6b1d1763abafa4655f2f05cbfdf3d1d598ca4053d474ab7218dc44 |
| `RUNNER-PROVENANCE-TEMPLATE-v7.md` | 36d5aa398f524fb9393fb78c2a4cdee4dd051c16511285768db08e11ef8e283f |
| `selftest-2026-10-14-v7.log` | ce5a477aea535d95c05051308033ec773775e8b32571710b8943887da2dbacca |
| `plan-dryrun-2026-10-14-v7.txt` | b44db9cc134a2f204905126507c9af24fb2433e689bfaddf09b006c3e6fd2c90 |

## 4. Unified diffs v6 → v7 (`diff -u`, changed lines with 3 lines of context)

```diff
--- run_stage1b-v6.py
+++ run_stage1b-v7.py
@@ -1,5 +1,11 @@
-"""Stage 1b driver, VERSION 6 (fixes Codex R44-1; optimize/codex-debate/codex-work/R44-HARNESS-RECHECK-V5-CODEX.md).
-v1 (run_stage1b.py) .. v5 are kept unchanged. NOT RUN: no job has been started.
+"""Stage 1b driver, VERSION 7 (after Codex R45, READY TO SEAL for v6; optimize/codex-debate/codex-work/
+R45-HARNESS-CHECK-V6-CODEX.md). v1 (run_stage1b.py) .. v6 are kept unchanged. NOT RUN: no job has been started.
+
+New in v7:
+  Disk floor  owner decision 2026-10-14: the C: floor is lowered temporarily to 45 GiB for Stage 1b (FLOOR_MIB
+        46,080; NEED_MIB 410 per running job unchanged). C: had about 54 GB free; 74 jobs need about 6-7 GB.
+  R45-1 An anchor-only main job ("anchor-exists-pending", left for owner review) now counts as unfinished, so `run`
+        returns 3 (deferred), never 0, while such a job exists.
 
 New in v6:
   R44-1 Main output can no longer exist without its admitted analysis identity. Before a main child is launched,
@@ -102,7 +108,7 @@
       STOP, the failure latches, the stat snapshot and disk space, and RESERVES 410 MiB per running job, so six
       workers cannot all spend the same free space: free - 51,200 MiB - reserved >= 410 MiB. 1,200-s deferral and
       keep-awake cleanup unchanged.
-Usage: python -B run_stage1b-v6.py plan|report|freeze|run gate0|smoke|main
+Usage: python -B run_stage1b-v7.py plan|report|freeze|run gate0|smoke|main
   plan   : dry check; lists jobs, states and every input problem; starts nothing, creates nothing
   freeze : writes the external-tree manifests and stage1b-input-freeze.sha256 (mode "x"); post-approval, before seal
 """
@@ -146,7 +152,7 @@
 import run_t38  # noqa: E402
 import run_w07  # noqa: E402
 
-ce = _load("compare_equivalence_stage1b_v6", "compare_equivalence_stage1b-v6.py")
+ce = _load("compare_equivalence_stage1b_v7", "compare_equivalence_stage1b-v7.py")
 
 RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-tier3-v3")
 RUNNER_MANIFEST = HERE / "runner-tier3-v3.sha256.tsv"
@@ -181,7 +187,7 @@
 LOG_ROOT = HERE / "logs"
 STOP_FILE = HERE / "STOP"
 POWER_WAIT_S = 1200
-FLOOR_MIB, NEED_MIB = 51_200, 410
+FLOOR_MIB, NEED_MIB = 46_080, 410          # v7: owner decision 2026-10-14 (45 GiB, Stage 1b)
 PHASES = ("gate0", "smoke", "main")
 BATTERY_ENV = run_pilot.BATTERY_ENV                           # RB_ALLOW_BATTERY_MIN_PERCENT (run_pilot.py:41)
 
@@ -228,8 +234,8 @@
     return HERE / f"stage1b-external-{tree_id}.sha256.tsv"
 
 
-SEALED_REQUIRED = ("STAGE1B-PLAN.md", "run_stage1b-v6.py", "compare_equivalence_stage1b-v6.py",
-                   "test_compare_equivalence_stage1b-v6.py", "selftest-2026-10-14-v6.log", "canonical_wp4.py",
+SEALED_REQUIRED = ("STAGE1B-PLAN.md", "run_stage1b-v7.py", "compare_equivalence_stage1b-v7.py",
+                   "test_compare_equivalence_stage1b-v7.py", "selftest-2026-10-14-v7.log", "canonical_wp4.py",
                    "make_wp4_costfirst.py", "make_wp4_costfirst.log", "wp4-tier3-v2-costfirst.json",
                    "runner-tier3-v3.sha256.tsv", "RUNNER-PROVENANCE-v3.md", "stage1b-input-freeze.sha256") + tuple(
     external_manifest(tree_id).name for tree_id, _, _ in EXTERNAL_TREES)
@@ -865,7 +871,7 @@
     if text is None:
         if not ANALYSIS_FREEZE.exists():
             return [f"refused: {ANALYSIS_FREEZE.name} does not exist (freeze the main-phase analysis code by a hashed "
-                    f"amendment before any main job starts; plan v6 section 12)"]
+                    f"amendment before any main job starts; plan v7 section 12)"]
         text = ANALYSIS_FREEZE.read_text(encoding="utf-8")
     problems, listed = [], set()
     for number, line in enumerate(text.splitlines(), start=1):
@@ -908,7 +914,7 @@
 def analysis_binding_text(manifest_bytes: bytes, stamp: str) -> str:
     """R41-1: the retained identity: the manifest's SHA-256 and its inventory lines, copied verbatim."""
     inventory = [line for line in manifest_bytes.decode("utf-8").splitlines() if line and not line.startswith("#")]
-    return "\n".join([f"# stage1b analysis binding, written {stamp} by run_stage1b-v6.py at the first `run main` "
+    return "\n".join([f"# stage1b analysis binding, written {stamp} by run_stage1b-v7.py at the first `run main` "
                       f"admission (R41-1); never rewritten", f"manifest-sha256 {hashlib.sha256(manifest_bytes).hexdigest()}"]
                      + inventory) + "\n"
 
@@ -1076,7 +1082,7 @@
 def record_input_drift(phase: str, where: str, problems: list[str]) -> None:
     """N3: observed input drift is a durable global stop, not a transient deferral."""
     stamp = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
-    write_receipt(DRIFT_STOP, [f"# INPUT DRIFT STOP written {stamp} by run_stage1b-v6.py ({phase}, {where})",
+    write_receipt(DRIFT_STOP, [f"# INPUT DRIFT STOP written {stamp} by run_stage1b-v7.py ({phase}, {where})",
                                "Frozen inputs changed while Stage 1b was running; every later run and verdict is "
                                "refused. Owner review required (R28 N3)."] + [f"- {p}" for p in problems[:50]])
 
@@ -1206,7 +1212,7 @@
         locker(handle)
     except OSError as error:
         handle.close()
-        raise SystemExit(f"refused: another run_stage1b-v6.py instance holds {INSTANCE_LOCK.name} ({error})")
+        raise SystemExit(f"refused: another run_stage1b-v7.py instance holds {INSTANCE_LOCK.name} ({error})")
     _INSTANCE_HANDLE = handle
 
 
@@ -1312,7 +1318,7 @@
         with open(external_manifest(tree_id), "x", encoding="utf-8", newline="\n") as handle:
             handle.write(text)
         tree_lines.append(f"tree {tree_id} {text.split()[2]} {len(lines)} {pathlib.Path(root).as_posix()}")
-    lines = [f"# stage1b input freeze v6, written {stamp}; SHA-256 and absolute path of every input the jobs and "
+    lines = [f"# stage1b input freeze v7, written {stamp}; SHA-256 and absolute path of every input the jobs and "
              f"the comparator read, the external execution trees, the resolved tools, the steering environment and "
              f"required-absent paths"]
     for target in freeze_targets():
@@ -1414,7 +1420,7 @@
             try:
                 for done, future in enumerate(concurrent.futures.as_completed(futures), start=1):
                     name, label = future.result()
-                    deferred += label.startswith(("deferred", "stopped")) or label == "log-exists-pending"
+                    deferred += label.startswith(("deferred", "stopped")) or label in ("log-exists-pending", "anchor-exists-pending")
                     print(f"[{time.time() - started:7.0f}s] {name:<34} {label:<24} ({done}/{len(items)})", flush=True)
             except BaseException:
                 for future in futures:                   # v4: no further launch after a worker / launch error
--- compare_equivalence_stage1b-v6.py
+++ compare_equivalence_stage1b-v7.py
@@ -1,7 +1,6 @@
-"""Stage-1b Gate-0 comparator, VERSION 6. The only change from v5 is the driver it loads for readiness, latches and
-frozen-input checks: run_stage1b-v6.py (Codex R44-1 was fixed in the driver; the v5 comparator would load the v5 driver,
-whose seal list names the v5 files). Every comparison, report and outcome rule is the v5 code, byte for byte. v1 .. v5
-are kept unchanged.
+"""Stage-1b Gate-0 comparator, VERSION 7. As in v6, the only change from v5 is the driver it loads for readiness,
+latches and frozen-input checks: now run_stage1b-v7.py (version cascade of harness v7: owner disk floor, R45-1). Every
+comparison, report and outcome rule is the v5 code, byte for byte. v1 .. v6 are kept unchanged.
 
 New in v5:
   R41-2 Per-run outcome conservation (report_problems, `outcomes`): protocol requestAccepted / requestRejected action
@@ -90,10 +89,10 @@
       prints objective bounds and work values. The configuration identity stays the verified ON binding (R22:242).
 
 Usage (the report name is a bare file name; it is written into this folder):
-  python -B compare_equivalence_stage1b-v6.py pair <ref-run> <new-run> <U|C-30> pair-<tag>.txt
-  python -B compare_equivalence_stage1b-v6.py gate0 gate0-compare-<tag>.txt
+  python -B compare_equivalence_stage1b-v7.py pair <ref-run> <new-run> <U|C-30> pair-<tag>.txt
+  python -B compare_equivalence_stage1b-v7.py gate0 gate0-compare-<tag>.txt
         -> GATE0-BEHAVIOURAL-PASSED.txt / GATE0-BEHAVIOURAL-FAILED.txt
-  python -B compare_equivalence_stage1b-v6.py smoke smoke-check-<tag>.txt    -> SMOKE-PASSED.txt / SMOKE-FAILED.txt
+  python -B compare_equivalence_stage1b-v7.py smoke smoke-check-<tag>.txt    -> SMOKE-PASSED.txt / SMOKE-FAILED.txt
 `pair` is exploratory and never a Gate-0 verdict. Every output is created with mode "x" (never overwritten).
 """
 from __future__ import annotations
@@ -120,8 +119,8 @@
 import runner_manifest  # noqa: E402
 from pilot_analyze import fleet_decisions  # noqa: E402
 
-DRIVER_FILE = "run_stage1b-v6.py"
-DRIVER_MODULE = "run_stage1b_v6"
+DRIVER_FILE = "run_stage1b-v7.py"
+DRIVER_MODULE = "run_stage1b_v7"
 DRIVER = None            # the loaded driver module (tests may replace it)
 GATE0_PASS_FILE, GATE0_FAIL_FILE = "GATE0-BEHAVIOURAL-PASSED.txt", "GATE0-BEHAVIOURAL-FAILED.txt"
 GATE0_WORD = "GATE0 BEHAVIOURAL"
@@ -1807,7 +1806,7 @@
     s = driver()
 
     def header_for(old_identity, new_identity):
-        return [f"# compare_equivalence_stage1b-v6.py {' '.join(argv)}",
+        return [f"# compare_equivalence_stage1b-v7.py {' '.join(argv)}",
                 f"old runner: ok {old_identity['ok']} tree {old_identity['tree']} dll {old_identity['dll']} "
                 f"{old_identity['problems']}",
                 f"new runner: ok {new_identity['ok']} tree {new_identity['tree']} dll {new_identity['dll']} "
--- test_compare_equivalence_stage1b-v6.py
+++ test_compare_equivalence_stage1b-v7.py
@@ -1,10 +1,11 @@
-"""Self-test v6 for the Stage-1b harness (Codex R44 fix). Pure, in-memory controls plus read-only archived inputs;
-nothing is simulated, built, published or written except the optional log (mode "x").
+"""Self-test v7 for the Stage-1b harness (v7: owner disk floor, launch environment, start-rule facts, R45-1). Pure,
+in-memory controls plus read-only archived inputs; nothing is simulated, built, published or written except the
+optional log (mode "x").
 
-  python -B test_compare_equivalence_stage1b-v6.py [--target v6|v5] [<out-log>]
+  python -B test_compare_equivalence_stage1b-v7.py [--target v7|v6] [<out-log>]
 
---target v6 (default) runs
-  A. every control of the v1/v2 self-tests against the v6 comparator (= v5 comparison code, v6 driver binding).
+--target v7 (default) runs
+  A. every control of the v1/v2 self-tests against the v7 comparator (= v5 comparison code, v7 driver binding).
      P2n / P3n are NORMALISATION tests with validity-limited fixtures (R28 N1): the transcript-verifiable digests are
      recomputed by code independent of the comparator, the digests of decisions 2..N are not rebuilt. A fixture built
      from a changed transcript also gets its run report receipts refreshed (refresh_report, R38-1).
@@ -13,9 +14,10 @@
   D. the R38 finding controls (R38-1 .. R38-5 and the section-6 tool / launch-environment / analysis-timing items).
   E. the R41 finding controls (R41-1 .. R41-5 and the section-6 launch-date arithmetic).
   F. the R44 finding controls (R44-1: main output without its admitted analysis identity).
---target v5 runs only F against the reviewed v5 files (compare_equivalence_stage1b-v5.py, run_stage1b-v5.py,
-STAGE1B-PLAN-DRAFT-v5.md, RUNNER-PROVENANCE-TEMPLATE-v5.md): every R44 counterexample is expected to show BAD there
-(the reproduction log); positives are expected OK.
+  G. the v7 controls (R45-1 exit status of an anchor-only job; owner disk floor; v7 plan facts).
+--target v6 runs only G against the v6 files (expected BAD there; positives OK).
+Plan-text controls whose wording changed by owner decision in v7 (R38-P-c launch environment, R41-5-e start-rule
+arithmetic) check the v7 wording when the target is v7.
 Lines: OK = expected result; BAD = unexpected; LIMIT = a disclosed limit: (1) a consistently rewritten decision 2..N
 digest is not detected (behavioural-only Gate 0, owner decision 2026-10-12); (2) a well-formed but false
 checkpointBindingHash with a consistent semanticHash is not detected (its preimage contains adapter state that a
@@ -468,14 +470,23 @@
             gate_pass=ce.GATE0_PASS_FILE, gate_fail=ce.GATE0_FAIL_FILE,
             files=("compare_equivalence_stage1b-v5.py", "run_stage1b-v5.py", "STAGE1B-PLAN-DRAFT-v5.md",
                    "RUNNER-PROVENANCE-TEMPLATE-v5.md"))
-    s = load("run_stage1b_v6", "run_stage1b-v6.py")
-    ce = sys.modules["compare_equivalence_stage1b_v6"]
+    if version == "v6":
+        s = load("run_stage1b_v6", "run_stage1b-v6.py")
+        ce = sys.modules["compare_equivalence_stage1b_v6"]
+        ce.DRIVER = s
+        return types.SimpleNamespace(
+            version=version, ce=ce, s=s, plan=source("STAGE1B-PLAN-DRAFT-v6.md"), gate_word=ce.GATE0_WORD,
+            gate_pass=ce.GATE0_PASS_FILE, gate_fail=ce.GATE0_FAIL_FILE,
+            files=("compare_equivalence_stage1b-v6.py", "run_stage1b-v6.py", "STAGE1B-PLAN-DRAFT-v6.md",
+                   "RUNNER-PROVENANCE-TEMPLATE-v6.md"))
+    s = load("run_stage1b_v7", "run_stage1b-v7.py")
+    ce = sys.modules["compare_equivalence_stage1b_v7"]
     ce.DRIVER = s
     return types.SimpleNamespace(
-        version=version, ce=ce, s=s, plan=source("STAGE1B-PLAN-DRAFT-v6.md"), gate_word=ce.GATE0_WORD,
+        version=version, ce=ce, s=s, plan=source("STAGE1B-PLAN-DRAFT-v7.md"), gate_word=ce.GATE0_WORD,
         gate_pass=ce.GATE0_PASS_FILE, gate_fail=ce.GATE0_FAIL_FILE,
-        files=("compare_equivalence_stage1b-v6.py", "run_stage1b-v6.py", "STAGE1B-PLAN-DRAFT-v6.md",
-               "RUNNER-PROVENANCE-TEMPLATE-v6.md"))
+        files=("compare_equivalence_stage1b-v7.py", "run_stage1b-v7.py", "STAGE1B-PLAN-DRAFT-v7.md",
+               "RUNNER-PROVENANCE-TEMPLATE-v7.md"))
 
 
 class Ctx:
@@ -603,7 +614,7 @@
     return change
 
 
-# ---------------------------------------------------------------- section A: v1/v2 controls on the v6 comparator
+# ---------------------------------------------------------------- section A: v1/v2 controls on the v7 comparator
 
 def section_a(c: Ctx) -> None:
     ce = c.T.ce
@@ -897,13 +908,13 @@
         self.files = {"X:/s/run_pilot.py": b"pilot", "X:/s/fixture.json": b"fixture", "X:/s/dotnet.exe": b"host"}
         self.tree_data = {"X:/env": {"python.exe": b"py", "Lib/os.py": b"os", "Lib/site-packages/x.py": b"x"}}
         self.tree_ids = {"python-env": "X:/env"}
-        if T.version in ("v3", "v4", "v5", "v6"):
+        if T.version in ("v3", "v4", "v5", "v6", "v7"):
             self.tree_data["X:/tools/git"] = {"cmd/git.exe": b"git launcher", "mingw64/bin/git.exe": b"git"}
             self.tree_ids["git"] = "X:/tools/git"
         self.environ = {"DOTNET_MULTILEVEL_LOOKUP": "0", "PATH": "X:/tools/git/cmd;C:/Windows"}
         self.git_at = "X:/tools/git/cmd/git.exe"
         self.git_driver_at = None                         # v4: None = the driver context resolves like the jobs
-        self.taskkill_at = ("C:/Windows/System32/taskkill.exe" if T.version in ("v5", "v6")     # R41-4 path-scoped
+        self.taskkill_at = ("C:/Windows/System32/taskkill.exe" if T.version in ("v5", "v6", "v7")     # R41-4 path-scoped
                             else "X:/win/System32/taskkill.exe")
         self.tool_bytes = {self.taskkill_at: b"taskkill"}
         self.omit_taskkill = False
@@ -963,7 +974,7 @@
             lines.append(f"tree {tree_id} {sha(chr(10).join(body).encode('utf-8'))} {len(body)} {root}")
         if self.T.version == "v3":
             lines.append(f"tool git {self.git_at}")
-        elif self.T.version in ("v4", "v5", "v6"):
+        elif self.T.version in ("v4", "v5", "v6", "v7"):
             lines.append(f"tool git {self.sha(self.git_at)} {self.git_at}")
             if not self.omit_taskkill:
                 lines.append(f"tool taskkill {self.sha(self.taskkill_at)} {self.taskkill_at}")
@@ -978,7 +989,7 @@
                       read=lambda p: self.files[pathlib.Path(p).as_posix()].decode(), body=self.body)
         if T.version == "v3":
             kwargs.update(which=self.which, pth=lambda: list(self.pth), site=pathlib.Path("X:/env/Lib/site-packages"))
-        elif T.version in ("v4", "v5", "v6"):
+        elif T.version in ("v4", "v5", "v6", "v7"):
             kwargs.update(resolve=self.resolve, pth=lambda: list(self.pth), site=pathlib.Path("X:/env/Lib/site-packages"))
         return T.s.freeze_problems(text, **kwargs)
 
@@ -1516,7 +1527,7 @@
     v = T.version
     check(f"F6-7 the {v} seal list names the {v} files and the external-tree manifests",
           {f"run_stage1b-{v}.py", f"compare_equivalence_stage1b-{v}.py", f"test_compare_equivalence_stage1b-{v}.py",
-           f"selftest-{ {'v5': '2026-10-14', 'v6': '2026-10-14'}.get(v, '2026-10-13') }-{v}.log"} <= set(s.SEALED_REQUIRED)
+           f"selftest-{ {'v5': '2026-10-14', 'v6': '2026-10-14', 'v7': '2026-10-14'}.get(v, '2026-10-13') }-{v}.log"} <= set(s.SEALED_REQUIRED)
           and all(s.external_manifest(i).name in s.SEALED_REQUIRED for i, _, _ in s.EXTERNAL_TREES),
           str(s.SEALED_REQUIRED))
 
@@ -1689,7 +1700,7 @@
         audit = s.pth_redirect_problems(s.pth_lines(), s.PYTHON_SITE, tree_roots)
         check("N3-l read-only: the job interpreter's .pth files add no path outside the frozen trees", not audit,
               f"{len(s.pth_lines())} .pth line(s); {audit}")
-    elif T.version in ("v4", "v5", "v6"):
+    elif T.version in ("v4", "v5", "v6", "v7"):
         resolved = s.tool_resolution("git", "job")
         tree_roots = [pathlib.Path(root).as_posix() for _, root, _ in s.EXTERNAL_TREES]
         check("N3-k read-only: git resolves inside the frozen Git tree in this environment (v4: CreateProcess order, "
@@ -1865,7 +1876,7 @@
     T = c.T
     failing = {"label": "runner-tier3-v2", "ok": False, "tree": None, "files": None, "dll": None,
                "problems": ["runner-tier3-v2: runner directory differs from its manifest (injected)"]}
-    if T.version in ("v3", "v4", "v5", "v6"):
+    if T.version in ("v3", "v4", "v5", "v6", "v7"):
         reports = []
         with patched(T.ce, runner_identity_from_disk=lambda *a: dict(failing)):
             Ctx(T, report=lambda name, ok, detail="": reports.append((name, ok)))
@@ -2357,7 +2368,7 @@
     check("R38-T-e NoDefaultCurrentDirectoryInExePath is a frozen steering name (observed set in the agent shells on "
           "2026-10-13)", "NODEFAULTCURRENTDIRECTORYINEXEPATH" in getattr(s, "FROZEN_ENV_NAMES", ()),
           str(sorted(getattr(s, "FROZEN_ENV_NAMES", ()))))
-    if T.version not in ("v4", "v5", "v6"):
+    if T.version not in ("v4", "v5", "v6", "v7"):
         say("   R38-T-f..m not applicable: this driver has no taskkill binding or CreateProcess resolver")
         return
 
@@ -2450,9 +2461,17 @@
           T.plan.name)
     check("R38-P-b the plan records the three-week calculation and its start-date consequence",
           "2026-10-14" in flat and "21 days" in flat and "do not start" in flat, T.plan.name)
-    check("R38-P-c the plan settles the launch environment (one fresh Windows PowerShell; GIT_* unset; the same "
-          "recorded environment for freeze, run and verdict commands)", "Windows PowerShell" in flat
-          and "GIT_* unset" in flat and "launch environment" in flat, T.plan.name)
+    if T.version == "v7":                # v7: owner-approved launcher wording (HARNESS-V7-CHANGES.md)
+        check("R38-P-c the plan settles the launch environment (fresh `powershell.exe -NoProfile` through a launcher "
+              "that removes GIT_* and NoDefaultCurrentDirectoryInExePath; the same launcher and environment for freeze, "
+              "run and verdict commands)", "Windows PowerShell process (`powershell.exe -NoProfile`)" in flat
+              and "removes every `GIT_*` variable and `NoDefaultCurrentDirectoryInExePath`" in flat
+              and "The same launcher and the same environment are used for freeze, run and verdict commands" in flat,
+              T.plan.name)
+    else:
+        check("R38-P-c the plan settles the launch environment (one fresh Windows PowerShell; GIT_* unset; the same "
+              "recorded environment for freeze, run and verdict commands)", "Windows PowerShell" in flat
+              and "GIT_* unset" in flat and "launch environment" in flat, T.plan.name)
     check("R38-P-d the plan declares the taskkill OS cleanup exception and the CreateProcess tool resolution",
           "taskkill" in flat and "OS cleanup exception" in flat and "CreateProcess" in flat, T.plan.name)
     check("R38-P-e the plan makes the analysis-code timing explicit and mechanical (stage1b-analysis-freeze.sha256 "
@@ -2797,13 +2816,22 @@
           and all(n in template for n in code_names) and not t_stale,
           f"title {t_first[:70]!r}; stale {t_stale}")
     flat = " ".join(text.split())
-    d14 = (datetime.date(2026, 11, 4) - datetime.date(2026, 10, 14)).days
-    d15 = (datetime.date(2026, 11, 4) - datetime.date(2026, 10, 15)).days
-    d03 = (datetime.date(2026, 11, 3) - datetime.date(2026, 10, 14)).days
-    check(f"R41-5-e the plan's three-week arithmetic is correct ({d14} / {d15} / {d03} days) and its start consequence "
-          f"is stated", f"launch 2026-10-14 → {d14} days" in flat and f"launch 2026-10-15 → {d15} days" in flat
-          and f"2026-10-14 → 2026-11-03 is only {d03} days" in flat and "do not start Stage 1b" in flat
-          and "more than 21 days remain" not in flat, T.plan.name)
+    if v == "v7":                        # v7: owner fact 2026-10-14, deadline on or after 2026-11-05
+        e15 = (datetime.date(2026, 11, 5) - datetime.date(2026, 10, 15)).days
+        e14 = (datetime.date(2026, 11, 5) - datetime.date(2026, 10, 14)).days
+        e16 = (datetime.date(2026, 11, 5) - datetime.date(2026, 10, 16)).days
+        check(f"R41-5-e the plan's three-week arithmetic is correct ({e15} / {e14} / {e16} days) and its start "
+              f"consequence is stated", f"2026-11-05 − 2026-10-15 = {e15} days" in flat
+              and f"2026-11-05 − 2026-10-14 = {e14} days" in flat and f"2026-11-05 − 2026-10-16 = {e16} days" in flat
+              and "do not start Stage 1b" in flat and "more than 21 days remain" not in flat, T.plan.name)
+    else:
+        d14 = (datetime.date(2026, 11, 4) - datetime.date(2026, 10, 14)).days
+        d15 = (datetime.date(2026, 11, 4) - datetime.date(2026, 10, 15)).days
+        d03 = (datetime.date(2026, 11, 3) - datetime.date(2026, 10, 14)).days
+        check(f"R41-5-e the plan's three-week arithmetic is correct ({d14} / {d15} / {d03} days) and its start "
+              f"consequence is stated", f"launch 2026-10-14 → {d14} days" in flat
+              and f"launch 2026-10-15 → {d15} days" in flat and f"2026-10-14 → 2026-11-03 is only {d03} days" in flat
+              and "do not start Stage 1b" in flat and "more than 21 days remain" not in flat, T.plan.name)
     check("R41-5-f the plan attaches the A1 milestone source records (chapter-4 drafts with SHA-256)",
           "08-chuong4-phanA-v5-v3.tex" in flat and "09-chuong4-phanB-v11-v2.tex" in flat and "b2978f74" in flat,
           T.plan.name)
@@ -2914,25 +2942,75 @@
           and "Admission anchor (R44-1)" in flat and "Lifecycle multiplicity (R44-2" in flat, T.plan.name)
 
 
+# ================================================================ section G: v7 controls
+
+def v7_controls(c: Ctx) -> None:
+    import datetime
+    T, s = c.T, c.T.s
+    names = [job[0] for job in s.jobs("main")]
+    # R45-1 (Codex R45 probe): anchors for every main job, the first job anchor-only, the other 67 complete
+    world = AnalysisWorld()
+    held = world.write_binding()
+    st = analysis_main(c, world, anchors={name: f"analysis-binding {held}" for name in names}, completed=names[1:],
+                       run_job="real")
+    check("R45-1-a Codex: one anchor-only main job (owner review) and 67 complete outputs: `run main` returns 3, not 0",
+          st.get("return") == 3 and st["receipt"] is None, describe_main(st))
+    world = AnalysisWorld()
+    held = world.write_binding()
+    st = analysis_main(c, world, anchors={name: f"analysis-binding {held}" for name in names}, run_job="real")
+    check("R45-1-b Codex: all 68 main jobs anchor-only: `run main` returns 3, not 0", st.get("return") == 3
+          and st["receipt"] is None, describe_main(st))
+    world = AnalysisWorld()
+    held = world.write_binding()
+    st = analysis_main(c, world, anchors={name: f"analysis-binding {held}" for name in names}, completed=names,
+                       run_job="real")
+    check("R45-1-c all 68 main jobs complete with their anchors: `run main` returns 0 [positive]",
+          st.get("return") == 0 and st["receipt"] is None, describe_main(st))
+    # owner disk floor (2026-10-14)
+    flat = " ".join(T.plan.read_text(encoding="utf-8").split())
+    check("V7-a the C: disk floor is the owner's 45 GiB (FLOOR_MIB 46,080; NEED_MIB 410 unchanged) and the plan records "
+          "the decision", s.FLOOR_MIB == 46_080 and s.NEED_MIB == 410 and "C: disk floor 46,080 MiB (45 GiB; owner "
+          "decision 2026-10-14" in flat, f"FLOOR_MIB {s.FLOOR_MIB}, NEED_MIB {s.NEED_MIB}")
+    # start-rule facts and READY status (owner and coordinator facts of 2026-10-14)
+    e15 = (datetime.date(2026, 11, 5) - datetime.date(2026, 10, 15)).days
+    e14 = (datetime.date(2026, 11, 5) - datetime.date(2026, 10, 14)).days
+    check("V7-b the plan records the deadline on or after 2026-11-05, the launch dates with 21 / 22 days and the "
+          "READY TO SEAL rating, and no longer says the harness is not READY",
+          "on or after 2026-11-05" in flat and f"2026-11-05 − 2026-10-15 = {e15} days" in flat and e15 == 21
+          and f"2026-11-05 − 2026-10-14 = {e14} days" in flat and e14 == 22
+          and "rated READY TO SEAL by Codex R45 on 2026-10-14" in flat
+          and "Codex has not rated the harness READY" not in flat and "The harness is not READY" not in flat,
+          T.plan.name)
+    check("V7-c the plan records the runner-source commit dce84ec (not pushed), its reviewed-diff hash and the dotnet "
+          "test log", "dce84ec4e51b9b7489402d81bb0dda28afbda0a0" in flat and "not pushed" in flat
+          and "c48042af…d690, equal to the reviewed diff" in flat
+          and "dotnet-test-costfirst-dce84ec-2026-10-14.log" in flat and "5b845e7b…cc55" in flat, T.plan.name)
+    check("V7-d the plan carries the owner-approved launch-environment wording verbatim",
+          "A fresh Windows PowerShell process (`powershell.exe -NoProfile`), started either by the owner or by the "
+          "coordinator through a launcher script that removes every `GIT_*` variable and "
+          "`NoDefaultCurrentDirectoryInExePath` before starting it. The same launcher and the same environment are "
+          "used for freeze, run and verdict commands." in flat, T.plan.name)
+
+
 # ---------------------------------------------------------------- main
 
 def main(argv: list[str]) -> int:
-    version = "v6"
+    version = "v7"
     if argv[:1] == ["--target"]:
         version, argv = argv[1], argv[2:]
-    if version not in ("v6", "v5"):
+    if version not in ("v7", "v6"):
         print(__doc__)
         return 2
     started = time.time()
     T = targets(version)
-    say(f"# test_compare_equivalence_stage1b-v6.py --target {version} -- in-memory controls; archived inputs read only")
-    for name in T.files + ("test_compare_equivalence_stage1b-v6.py", "canonical_wp4.py", "wp4-tier3-v2-costfirst.json"):
+    say(f"# test_compare_equivalence_stage1b-v7.py --target {version} -- in-memory controls; archived inputs read only")
+    for name in T.files + ("test_compare_equivalence_stage1b-v7.py", "canonical_wp4.py", "wp4-tier3-v2-costfirst.json"):
         say(f"#   tested file {name} sha256 {sha(source(name).read_bytes())}")
     say(f"#   fixture digest helper actual_fleetpy_medium_verify.py sha256 "
         f"{sha((ADAPTER / 'actual_fleetpy_medium_verify.py').read_bytes())} (frozen harness, read only)")
     c = Ctx(T)
-    if version == "v6":
-        say("## A. v1/v2 controls on the v6 comparator")
+    if version == "v7":
+        say("## A. v1/v2 controls on the v7 comparator")
         section_a(c)
         say("## B. R24 finding controls")
         for part in (f1, f2, f3_driver, f5, f3_comparator, f4, f6):
@@ -2946,8 +3024,10 @@
         say("## E. R41 finding controls")
         for part in (r41_1, r41_2_3, r41_4, r41_5):
             part(c)
-    say("## F. R44 finding controls")
-    r44_1(c)
+        say("## F. R44 finding controls")
+        r44_1(c)
+    say("## G. v7 controls (R45-1, owner disk floor, v7 plan facts)")
+    v7_controls(c)
     say(f"SELFTEST {'PASS' if not FAILURES else 'FAIL'} (--target {version}): {COUNTS['OK']} OK, {COUNTS['BAD']} BAD, "
         f"{COUNTS['LIMIT']} LIMIT; unexpected {FAILURES}; limits {LIMITS}; {time.time() - started:.0f} s")
     if argv:
--- STAGE1B-PLAN-DRAFT-v6.md
+++ STAGE1B-PLAN-DRAFT-v7.md
@@ -1,14 +1,15 @@
-# Stage 1b plan (DRAFT v6, not sealed): cost-first selector × drop-cap mechanism study
+# Stage 1b plan (DRAFT v7, not sealed): cost-first selector × drop-cap mechanism study
 
-Status: draft v6 written 2026-10-14 by Claude (harness fixer) after Codex R44
-(`optimize/codex-debate/codex-work/R44-HARNESS-RECHECK-V5-CODEX.md`, one P2: R44-1). It supersedes the earlier drafts,
-all kept unchanged: `STAGE1B-PLAN-DRAFT.md` (v1), `-v2` (after R24, `HARNESS-FIX-REPORT-R24.md`), `-v3` (after R28 and
-the owner decision on N1, `HARNESS-FIX-REPORT-R28.md`), `-v4` (after R38, `HARNESS-FIX-REPORT-R38.md`; its title line
-still said "v3", Codex R41-5), `-v5` (after R41, `HARNESS-FIX-REPORT-R41.md` and `HARNESS-FIX-REPORT-R41-v2.md`). The v6
-changes are §12 (admission anchor), §14 (corrected analysis limitation; lifecycle multiplicity) and the v6 file names;
-see `HARNESS-FIX-REPORT-R44.md`. The harness this plan names is **v6**: `run_stage1b-v6.py`,
-`compare_equivalence_stage1b-v6.py`, `test_compare_equivalence_stage1b-v6.py`, self-test log
-`selftest-2026-10-14-v6.log`, receipt template `RUNNER-PROVENANCE-TEMPLATE-v6.md`. To be copied to
+Status: draft v7 written 2026-10-14 by Claude (harness fixer) after Codex R45
+(`optimize/codex-debate/codex-work/R45-HARNESS-CHECK-V6-CODEX.md`: READY TO SEAL for v6). It supersedes the earlier
+drafts, all kept unchanged: `STAGE1B-PLAN-DRAFT.md` (v1), `-v2` (after R24, `HARNESS-FIX-REPORT-R24.md`), `-v3` (after
+R28 and the owner decision on N1, `HARNESS-FIX-REPORT-R28.md`), `-v4` (after R38, `HARNESS-FIX-REPORT-R38.md`; its title
+line still said "v3", Codex R41-5), `-v5` (after R41, `HARNESS-FIX-REPORT-R41.md` and `HARNESS-FIX-REPORT-R41-v2.md`),
+`-v6` (after R44, `HARNESS-FIX-REPORT-R44.md`). The v7 changes are the owner's disk floor (§10), the launch environment
+(§11), the start-rule facts and the runner commit (§0, §2), R45-1 (exit status) and the v7 file names; see
+`HARNESS-V7-CHANGES.md`. The harness this plan names is **v7**: `run_stage1b-v7.py`,
+`compare_equivalence_stage1b-v7.py`, `test_compare_equivalence_stage1b-v7.py`, self-test log
+`selftest-2026-10-14-v7.log`, receipt template `RUNNER-PROVENANCE-TEMPLATE-v7.md`. To be copied to
 `STAGE1B-PLAN.md`, completed at the `<FILL>` marks after the runner is published, and sealed in `STAGE1B-PLAN.sha256`
 **before any Stage-1b job**, Gate 0 included. Nothing in this file has been run.
 
@@ -35,7 +36,10 @@
 - **Commit and publish approval.** The owner approved committing the cost-first branch (in its worktree, **no push**)
   and publishing runner v3 once Codex rates the harness READY (recorded by the coordinator on 2026-10-13 from the
   owner's answer; LEDGER "Owner decisions at 14:00", lines 493-497 at LEDGER SHA-256 8560d7db…0cc9d read 2026-10-14;
-  R41 §6). The runner is not published at drafting time; Codex has not rated the harness READY (R41: NOT READY).
+  R41 §6). **The harness was rated READY TO SEAL by Codex R45 on 2026-10-14** (`codex-work/R45-HARNESS-CHECK-V6-CODEX.md`,
+  for harness v6; v7 adds only the changes listed in `HARNESS-V7-CHANGES.md`). The coordinator committed
+  `dce84ec4e51b9b7489402d81bb0dda28afbda0a0` on `research/tier3-cost-first` (2026-10-14 10:58, not pushed); §2. The
+  runner is not published at drafting time.
 - **Start rule (R17:281-288; R18:222).** The optional study starts only after the A1 milestone, with the deadline
   confirmed. At least 3 weeks left: Stage 1 (1a then 1b). 1–3 weeks left: Stage 1a only (34 jobs, no code). Less than
   1 week: nothing.
@@ -45,15 +49,14 @@
     b2978f74…306b) and `optimize/codex-debate/codex-work/r39-ch4b-v11/09-chuong4-phanB-v11-v2.tex` (2026-10-13 23:58,
     SHA-256 5d4ac193…02c1); the part-B v10 check is `codex-work/r37-ch4b-verify/R37-CH4B-V10-VERIFY-CODEX.md`. These
     files show that the drafts exist; the milestone judgement itself is the owner's.
-  - **Confirmed deadline:** after 2026-11-03 (the exact date was not given; confirmed by the owner on 2026-10-13).
-  - **Three-week calculation (corrected after R41 §6).** Convention: calendar days, local dates (UTC+7), days left =
-    deadline date − launch date. "After 2026-11-03" means a deadline date of 2026-11-04 or later, so the conservative
-    lower bound is: launch 2026-10-14 → 21 days (exactly three weeks); launch 2026-10-15 → 20 days; any later launch →
-    fewer. 2026-10-14 → 2026-11-03 is only 20 days. The harness is not READY on 2026-10-14, so any launch is on
-    2026-10-15 or later and the recorded fact alone gives fewer than 21 days. **Therefore: before the launch, record the
-    intended launch date and the exact deadline (or a stronger lower bound) with its source; if deadline − launch is
-    fewer than 21 days, do not start Stage 1b** (1–3 weeks: Stage 1a only, already complete). No amendment of this rule
-    is inferred.
+  - **Confirmed deadline:** on or after 2026-11-05 (confirmed by the owner on 2026-10-14, recorded by the coordinator;
+    the exact date was not given). Earlier record: after 2026-11-03 (owner, 2026-10-13).
+  - **Three-week calculation.** Convention: calendar days, local dates (UTC+7), days left = deadline date − launch date,
+    with the conservative deadline 2026-11-05. The intended launch date of the first Stage-1b job (Gate 0) is 2026-10-14
+    or 2026-10-15: 2026-11-05 − 2026-10-15 = 21 days and 2026-11-05 − 2026-10-14 = 22 days, so the three-week rule is
+    met for either date. A later launch (2026-11-05 − 2026-10-16 = 20 days) does not meet it from this fact alone:
+    **if deadline − launch is fewer than 21 days, do not start Stage 1b** (1–3 weeks: Stage 1a only, already complete)
+    unless a later confirmed deadline is recorded first. No amendment of this rule is inferred.
 - Previewed before this plan: every archived SF arm on the 34 test cells (T3.8), Stage 1a SF-C120 (C120R), fixed-state
   selector replays (R18:313-317). No cost-first (CF) trajectory exists on any cell. This is an exploratory study with
   sealed predictions.
@@ -87,7 +90,7 @@
 `tang3/calib/panelA/worlds/W5-<day>-<window>.json`, fixtures `C:/RideBoundData/research/fixtures-panelA-v1`, drivers
 `tang3/calib/panelA/drivers`, master seed 7, one repeat, cwd `E:/Code/RideBound-deadline` (HEAD 1ce15e9, inventory
 0da63eaf…), adapter and FleetPy 1.0.2 as recorded for T3.8, 6 workers. Every input file is frozen in
-`stage1b-input-freeze.sha256` (v6 format, `run_stage1b-v6.py freeze`), which also freezes the execution software and
+`stage1b-input-freeze.sha256` (v7 format, `run_stage1b-v7.py freeze`), which also freezes the execution software and
 resolver scope: the FleetPy tree `E:/RideBoundData/wp7/FleetPy-1.0.2` (without `.git/`), the job interpreter's
 environment `E:/RideBoundData/wp7/envs/fleetpy-1.0.2`, `dotnet.exe`, `host/fxr` and every installed
 `Microsoft.NETCore.App`, the Git installation `C:/Program Files/Git`, the two bare-name tools of the audited call graph
@@ -121,15 +124,20 @@
 ## 2. Runner and provenance (R22 F3, §5.1, §7.2)
 
 - Runner `C:/RideBoundData/research/runner-tier3-v3`, published from the owner-approved commit of
-  `research/tier3-cost-first`: commit `<FILL>`, base `761915c`, reviewed diff `c48042af…d690`; tree `<FILL>` (`<FILL>`
+  `research/tier3-cost-first`: commit `dce84ec4e51b9b7489402d81bb0dda28afbda0a0` (committed by the coordinator on
+  2026-10-14, not pushed; `git -c core.autocrlf=false diff 761915c dce84ec | sha256sum` = c48042af…d690, equal to the
+  reviewed diff; `dotnet test` at the commit: 1,033 passed plus the 1 known clean-tree failure
+  `StrictBagItBundleTests.Source_inventory_binds_dirty_worktree_files_not_only_base_commit`, log
+  `harness/dotnet-test-costfirst-dce84ec-2026-10-14.log`, SHA-256 5b845e7b…cc55), base `761915c`, reviewed diff
+  `c48042af…d690`; tree `<FILL>` (`<FILL>`
   files), `RideBound.Runner.dll` `<FILL>`; manifest `harness/runner-tier3-v3.sha256.tsv` (`<FILL>` SHA-256); receipt
-  `harness/RUNNER-PROVENANCE-v3.md`, filled from `RUNNER-PROVENANCE-TEMPLATE-v6.md`. **One receipt file name:** the
-  `-v3` names the runner build, not the harness version; it is the file `run_stage1b-v6.py` reads (`PROVENANCE`) and the
+  `harness/RUNNER-PROVENANCE-v3.md`, filled from `RUNNER-PROVENANCE-TEMPLATE-v7.md`. **One receipt file name:** the
+  `-v3` names the runner build, not the harness version; it is the file `run_stage1b-v7.py` reads (`PROVENANCE`) and the
   file in the seal list (§13). Every field must be filled (the driver refuses a receipt that still contains a template
   field; the recorded facts still need manual review, R28:207).
 - The harness commit (`1ce15e9`, = `coreCommitSha` in every initializeRun) and inventory (`0da63eaf…`) are separate
   facts from the runner source. The transcripts' coreCommitSha is never presented as the runner source (R22:58).
-- All 74 jobs use this one tree. `run_stage1b-v6.py` re-verifies it byte for byte before each phase; the Gate-0 and smoke
+- All 74 jobs use this one tree. `run_stage1b-v7.py` re-verifies it byte for byte before each phase; the Gate-0 and smoke
   verdict files record its DLL and tree hash and are refused for any other build.
 
 ## 3. Gate 0 (G0) — behavioural-only, amended (owner decision 2026-10-12; R28 N1)
@@ -152,8 +160,8 @@
 | `C-30-d20181116-s10-r1-w08` | 2035 | 42ba5f1a…72b |
 | `C-30-d20181116-s10-r1-w17` | 2146 | 371565dd…bbc |
 
-Criterion (implemented by `harness/compare_equivalence_stage1b-v6.py`; its controls pass in
-`harness/selftest-2026-10-14-v6.log`):
+Criterion (implemented by `harness/compare_equivalence_stage1b-v7.py`; its controls pass in
+`harness/selftest-2026-10-14-v7.log`):
 1. Preconditions per run: status pass / succeeded; bundle files match `bundle-manifest.json`; every transcript record and
    frame parsed strictly (a duplicate JSON field anywhere, a non-integer number or a non-canonical encoding fails;
    R28 N2); every frame SHA-256, length, direction and contiguous ordinal from 1; final Runner checkpoint; decision
@@ -200,7 +208,7 @@
 integer with the objective's sense direction, exact gap arithmetic and proof flag. No `worst-hard-utilization-ppm`, no
 `revision:*` level. For CF-C30 no rider has drop Σ|c| > 30 s. The smoke report prints solver statuses, execution paths,
 omission and work sums, and per level the incumbent and best-bound sums, for inspection
-(`compare_equivalence_stage1b-v6.py smoke`). Failure: integration defect, stop.
+(`compare_equivalence_stage1b-v7.py smoke`). Failure: integration defect, stop.
 
 Before `main`, the person who read the smoke report records the integration inspection in `harness/SMOKE-INSPECTION.txt`
 (lines `smokeVerdictSha256 <SHA-256 of SMOKE-PASSED.txt>`, `inspected-by: <names>`, `inspection: accepted`); the driver
@@ -281,14 +289,15 @@
 
 ## 10. Execution order and safety
 
-`run_stage1b-v6.py` phases `gate0` → (`compare_equivalence_stage1b-v6.py gate0 gate0-compare-<tag>.txt` →
+`run_stage1b-v7.py` phases `gate0` → (`compare_equivalence_stage1b-v7.py gate0 gate0-compare-<tag>.txt` →
 `GATE0-BEHAVIOURAL-PASSED.txt`) → `smoke` → (`… smoke smoke-check-<tag>.txt` → `SMOKE-PASSED.txt`; then
 `SMOKE-INSPECTION.txt`) → analysis freeze (§12) → `main`. AC power only (the battery override is refused; AC is
 rechecked inside the admission, after the queue and the stat checks and immediately before each launch), STOP checked
 during the power wait and at admission (and again after the admission's stat scan), keep-awake, outputs never reused,
 logs mode "x", one driver instance at a time, admission right before each launch (STOP, failure latches, input-drift
-stop, frozen-input stat drift, disk, AC, then STOP and latches again), C: disk floor 51,200 MiB with 410 MiB reserved
-per running job, full freeze verification before and after every phase (also after a worker exception). A
+stop, frozen-input stat drift, disk, AC, then STOP and latches again), C: disk floor 46,080 MiB (45 GiB; owner decision
+2026-10-14: lowered temporarily from 51,200 MiB for Stage 1b, because C: had about 54 GB free and the 74 jobs need about
+6–7 GB, so the 50 GiB floor would stop the run partway) with 410 MiB reserved per running job, full freeze verification before and after every phase (also after a worker exception). A
 `*-FAILED.txt` or `INPUT-DRIFT-STOP.txt` refuses every later start and verdict; a phase with its own pass verdict is
 closed. Outputs `C:/RideBoundData/research/tier3-stage1b-v1/<phase>/<job>`. Estimated compute: historical ~15 min/job at
 6 parallel (R17:232; archived SF-U w08 run 880 s wall); CF runtime on T3.8 unconfirmed (R17:233-234). Disk: archived run
@@ -296,11 +305,11 @@
 
 ## 11. Settled launch environment and tool resolution (R38 §6; R41-4)
 
-- **Launch environment:** one fresh Windows PowerShell session opened by the owner (not an agent, IDE or Git Bash
-  terminal: on 2026-10-13 the agent shells set GIT_EDITOR, GIT_TERMINAL_PROMPT, GIT_ASKPASS and
-  NoDefaultCurrentDirectoryInExePath, and Git Bash puts `/mingw64/bin` first on PATH), with GIT_* unset; the same
-  recorded environment for freeze, run and verdict commands. The driver refuses any GIT_* variable and compares the
-  frozen steering names and values at every run and verdict; a fresh-shell wording alone does not replace that check.
+- **Launch environment:** A fresh Windows PowerShell process (`powershell.exe -NoProfile`), started either by the
+  owner or by the coordinator through a launcher script that removes every `GIT_*` variable and
+  `NoDefaultCurrentDirectoryInExePath` before starting it. The same launcher and the same environment are used for
+  freeze, run and verdict commands. The driver refuses any GIT_* variable and compares the frozen steering names and
+  values at every run and verdict; a fresh-shell wording alone does not replace that check.
 - **Tool resolution:** tools are resolved in CreateProcess order (application directory, current directory, System32
   before PATH; '.exe' appended), in the job context (job interpreter directory, job cwd `E:/Code/RideBound-deadline`)
   and the driver context, which must agree. `git` must resolve inside the frozen Git tree. Bare `taskkill` is declared
@@ -327,12 +336,13 @@
   job log is written (the log is written only after the child returns). On every check against the binding, a main job
   with an output directory or a log but no anchor, or with an anchor naming another binding, refuses the run and is
   persisted as `INPUT-DRIFT-STOP.txt`. So a restart after main results exist cannot adopt a replaced analysis. An anchor
-  without output (a launch that left nothing) is not relaunched automatically; the owner reviews it.
+  without output (a launch that left nothing) is not relaunched automatically; the owner reviews it, and `run` returns
+  3 (unfinished), not 0, while such a job exists (R45-1).
 
 ## 13. Sealed files
 
-Sealed files (`run_stage1b-v6.py` SEALED_REQUIRED, exactly): `STAGE1B-PLAN.md` (this plan), `run_stage1b-v6.py`,
-`compare_equivalence_stage1b-v6.py`, `test_compare_equivalence_stage1b-v6.py`, `selftest-2026-10-14-v6.log`,
+Sealed files (`run_stage1b-v7.py` SEALED_REQUIRED, exactly): `STAGE1B-PLAN.md` (this plan), `run_stage1b-v7.py`,
+`compare_equivalence_stage1b-v7.py`, `test_compare_equivalence_stage1b-v7.py`, `selftest-2026-10-14-v7.log`,
 `canonical_wp4.py`, `make_wp4_costfirst.py`, `make_wp4_costfirst.log`, `wp4-tier3-v2-costfirst.json`,
 `runner-tier3-v3.sha256.tsv`, `RUNNER-PROVENANCE-v3.md`, `stage1b-input-freeze.sha256`, and the five external-tree
 manifests `stage1b-external-fleetpy.sha256.tsv`, `stage1b-external-python-env.sha256.tsv`,
--- RUNNER-PROVENANCE-TEMPLATE-v6.md
+++ RUNNER-PROVENANCE-TEMPLATE-v7.md
@@ -1,14 +1,14 @@
-# Runner provenance receipt — TEMPLATE v6 (copy to `RUNNER-PROVENANCE-v3.md` at publish time; never edit this file)
+# Runner provenance receipt — TEMPLATE v7 (copy to `RUNNER-PROVENANCE-v3.md` at publish time; never edit this file)
 
-v6 of `RUNNER-PROVENANCE-TEMPLATE.md` (v1–v5 kept unchanged; v6 changes only the harness file names to the v6 files,
-Codex R44; the v4 template was a byte copy of v3, Codex R41-5).
+v7 of `RUNNER-PROVENANCE-TEMPLATE.md` (v1–v6 kept unchanged; v7, like v6, changes only the harness file names, now to
+the v7 files; the v4 template was a byte copy of v3, Codex R41-5).
 **One receipt file name:** the filled copy is `harness/RUNNER-PROVENANCE-v3.md`. The `-v3` names the **runner build**
-(`runner-tier3-v3`), not the harness version; it is the file the v6 driver reads (`run_stage1b-v6.py` `PROVENANCE`) and
-the file listed in the seal (`SEALED_REQUIRED`, plan v6 §13). The harness identities to record in §D are the **v6**
-files: `run_stage1b-v6.py`, `compare_equivalence_stage1b-v6.py`, `test_compare_equivalence_stage1b-v6.py`,
-`selftest-2026-10-14-v6.log`. Earlier changes (Codex R24 §3, `R24-HARNESS-REVIEW-CODEX.md:197,201`): the publish
+(`runner-tier3-v3`), not the harness version; it is the file the v7 driver reads (`run_stage1b-v7.py` `PROVENANCE`) and
+the file listed in the seal (`SEALED_REQUIRED`, plan v7 §13). The harness identities to record in §D are the **v7**
+files: `run_stage1b-v7.py`, `compare_equivalence_stage1b-v7.py`, `test_compare_equivalence_stage1b-v7.py`,
+`selftest-2026-10-14-v7.log`. Earlier changes (Codex R24 §3, `R24-HARNESS-REVIEW-CODEX.md:197,201`): the publish
 snippet in §E starts with `Set-Location` so the SDK diagnostics run in the source worktree (global.json applies);
-`run_stage1b-v6.py` refuses a receipt that still contains any backticked template field such as `` `<...>` `` (an
+`run_stage1b-v7.py` refuses a receipt that still contains any backticked template field such as `` `<...>` `` (an
 automated check of completeness only — the recorded facts still need manual review before the seal, R24:197). Delete
 this header paragraph from the filled copy: its example field would itself be refused as unfilled.
 
@@ -17,7 +17,7 @@
 Stage-1b job is the HEAD of the **harness** root (`E:/Code/RideBound-deadline`, adapter `session.py:140-156,339-340`), not
 the source of the Runner DLL. The DLL identity in `initializeRun.payload.manifest.binarySha256` is the SHA-256 of
 `<runner root>/RideBound.Runner.dll` (`session.py:482-483`). This receipt binds that DLL and its full tree to the reviewed
-source. `run_stage1b-v6.py` refuses to run unless the filled copy `RUNNER-PROVENANCE-v3.md` exists, contains the tree hash and the DLL SHA-256, and
+source. `run_stage1b-v7.py` refuses to run unless the filled copy `RUNNER-PROVENANCE-v3.md` exists, contains the tree hash and the DLL SHA-256, and
 has no unfilled template field.
 
 Fill every `<...>`. Record command output verbatim (or as a log file with its SHA-256). If a value differs from an
@@ -45,7 +45,7 @@
 | SDK pin | `global.json`: `10.0.301`, `rollForward: latestPatch` | `<...>` |
 | `dotnet --version` (run in the worktree) | `10.0.3xx` | `<...>` |
 | `dotnet --list-sdks`, `dotnet --list-runtimes` | record both; the published runner is framework-dependent `net10.0` (`runtimeconfig.json` of v2: `Microsoft.NETCore.App 10.0.0`); installed on 2026-10-11: `Microsoft.NETCore.App 10.0.9`, `host/fxr 10.0.9` (read-only listing) | `<...>` |
-| Host | `dotnet` used by the jobs: `C:/Program Files/dotnet/dotnet.exe` (`run_pilot.py:31`); its SHA-256 and the `host/fxr` and `shared/Microsoft.NETCore.App` trees are frozen by `run_stage1b-v6.py freeze` | `<...>` |
+| Host | `dotnet` used by the jobs: `C:/Program Files/dotnet/dotnet.exe` (`run_pilot.py:31`); its SHA-256 and the `host/fxr` and `shared/Microsoft.NETCore.App` trees are frozen by `run_stage1b-v7.py freeze` | `<...>` |
 | Output root did not exist before publish | `Test-Path C:/RideBoundData/research/runner-tier3-v3` → `False` | `<...>` |
 | Publish command (exact) | see §E | `<...>` |
 | Publish console log | file name + SHA-256 | `<...>` |
@@ -70,10 +70,10 @@
 | Field | Value (frozen, R22:177) | Recorded check |
 |---|---|---|
 | Harness root (cwd of every job) | `E:/Code/RideBound-deadline`, never modified | `<...>` |
-| Harness commit = `coreCommitSha` in every initializeRun | `1ce15e92d2c1459d4254bbbcd581529197257fb2` (`git -C E:/Code/RideBound-deadline rev-parse HEAD`; `run_stage1b-v6.py plan` checks it) | `<...>` |
+| Harness commit = `coreCommitSha` in every initializeRun | `1ce15e92d2c1459d4254bbbcd581529197257fb2` (`git -C E:/Code/RideBound-deadline rev-parse HEAD`; `run_stage1b-v7.py plan` checks it) | `<...>` |
 | Repository inventory | `0da63eaf2b30ab332fba265a6b2b6e5c36b3ee78950cbac6d6267e60f81e4cbe` (`run_w07.py:46`, passed as `--expected-repository-inventory-sha256`) | `<...>` |
 | World wrapper | `tang3/world/preflight_world_v1_2.py` SHA-256 `df28b6cb…a262` (`W07-PLAN.sha256:6`) | `<...>` |
-| Driver / comparator / self-test | SHA-256 of `run_stage1b-v6.py`, `compare_equivalence_stage1b-v6.py`, `test_compare_equivalence_stage1b-v6.py` and `selftest-2026-10-14-v6.log` (all four also in `STAGE1B-PLAN.sha256`) | `<...>` |
+| Driver / comparator / self-test | SHA-256 of `run_stage1b-v7.py`, `compare_equivalence_stage1b-v7.py`, `test_compare_equivalence_stage1b-v7.py` and `selftest-2026-10-14-v7.log` (all four also in `STAGE1B-PLAN.sha256`) | `<...>` |
 
 Statement to keep verbatim in the filled receipt: *"coreCommitSha `1ce15e9…` in the Stage-1b transcripts identifies the
 harness root. The Runner DLL `<dll>` (tree `<tree>`) was published from `<runner-source commit>` on
```
