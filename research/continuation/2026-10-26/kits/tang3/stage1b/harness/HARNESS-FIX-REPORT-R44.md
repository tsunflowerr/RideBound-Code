# Stage 1b harness — fix report for Codex R44 (round 6; Claude, harness fixer; 2026-10-14)

Review answered: `optimize/codex-debate/codex-work/R44-HARNESS-RECHECK-V5-CODEX.md` (NOT READY on one P2, R44-1).
Paths are relative to the harness folder `E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/` unless
stated; `F/` = `E:/Code/RideBound-deadline/simulators/fleetpy-ridebound/` (read only).

**Run:** only the pure self-test (twice) and the `plan` dry check. No FleetPy job, no Runner or other .NET execution,
no build, publish, commit, freeze or seal; Codex and Antigravity were not launched. Nothing was written to
`E:/Code/RideBound*` or `C:/Code/*`. Only new files were created; v5 and older files are unchanged.

## 1. The finding

R44-1 (P2): v5 anchored the analysis binding across restarts only through the main job log, and `run_job` writes that
log only after the child returns (`run_stage1b-v5.py:1192-1198`), while the producer writes its output before it
returns (F/actual_fleetpy_medium_preflight.py:848,723,905-906). After a stop between those two moments, a main output
exists with no anchor; a restart then accepted analysis code, manifest and binding replaced together (R44 probe:
late60 20 → 0). The plan's limitation (`STAGE1B-PLAN-DRAFT-v5.md:348-350`) wrongly said no main output exists there.

## 2. Fix (simplest robust option: anchor before launch, refuse output without anchor)

`run_stage1b-v6.py`:
- `write_admission_anchor` (:984-993): before a main child is launched, exclusively creates `logs/main/<job>.admitted`
  (mode "x"), writes `analysis-binding <held binding SHA-256>`, flushes and fsyncs. Called in `run_job` before the
  child (:1234-1238); if it cannot be created (or no binding is held), the child is **not launched**
  (`stopped-no-admission-anchor`). An anchor left without output is not relaunched automatically
  (`anchor-exists-pending`, :1219-1220), for owner review.
- `main_anchor_problems` (:996-1009), used by every check against the binding (:975): each main job with an output
  directory or a log must have an anchor naming the current binding; an output without anchor, or an anchor naming
  another binding, is a problem. At start-up (resume) these problems are classified as loss after start
  (:1392-1397: refusal **and** durable `INPUT-DRIFT-STOP.txt`); after the phase they are persisted by
  `post_phase_problems`. The v5 checks stay: binding vs manifest and files, held SHA, log first lines (:925-934).
- Names: docstring v6 (:1-13); comparator v6 (:149); `SEALED_REQUIRED` names the v6 files and
  `selftest-2026-10-14-v6.log` (:231-235).

`compare_equivalence_stage1b-v6.py`: **only** the driver it loads (`DRIVER_FILE` / `DRIVER_MODULE` :123-124), its
docstring, usage and report header. Reason: the v5 comparator loads `run_stage1b-v5.py`, whose seal list names the v5
files, so the Gate-0 / smoke verdicts would check a different seal list than the v6 driver. Every comparison, report
and outcome rule is the v5 code byte for byte (`diff` shows only those five places).

`STAGE1B-PLAN-DRAFT-v6.md`: status and v6 names (:1-13 and every harness file reference); §12 new "Admission anchor
(R44-1)" paragraph (:324-331); §14 corrected entry "Analysis binding before the first admission anchor" (:357-361:
the remaining window has no started main job and no output, because the anchor precedes launch; it states that v5's
reason was false), new entries "Deliberate coordinated rewrite of every local receipt" (:362-365) and "Lifecycle
multiplicity (R44-2, optional P3)" (:366-369); §13 seal list = v6 `SEALED_REQUIRED`.

`RUNNER-PROVENANCE-TEMPLATE-v6.md`: harness file names v6 only (receipt name unchanged: `RUNNER-PROVENANCE-v3.md`).

`test_compare_equivalence_stage1b-v6.py`: `--target v6` (sections A–F) or `--target v5` (F only). New section F
(`r44_1` :2854-2913, `run_job_world` :2816-2851); `analysis_main` gains admission anchors and complete outputs
(:2538-2587); R41-1-b/c/d now include the admission anchor next to the log (:2619,2631,2640); the fake writer has
`flush` for the anchor write.

**Unchanged files keep their v5 names:** none of the five v5 files was changed in place; all five have v6 successors
because each one names, loads or seals the harness version (driver; comparator's driver binding; test targets; plan
and template file names). No other harness file changed.

## 3. Controls (section F) and results

| Control | What | v5 (`selftest-2026-10-14-r44-on-v5.log`) | v6 (`selftest-2026-10-14-v6.log`) |
|---|---|---|---|
| R44-1-a | Codex: main output, original binding, no log, no anchor → refuse + drift | BAD (return 3, admitted; log :11) | OK (log :338) |
| R44-1-b | Codex: analysis, manifest and binding replaced after output, no log → refuse + drift | BAD (admitted; :12) | OK (log :339) |
| R44-1-c | same replacement, original admission anchor, no log → refuse + drift | BAD (admitted; :13) | OK (log :340) |
| R44-1-d | unchanged analysis, output with matching anchor, no log → admitted [positive] | OK (:14) | OK (log :341) |
| R44-1-e | Codex control: replaced analysis, original log anchor → refuse [positive] | OK (:15) | OK (log :342) |
| R44-1-f | Codex strengthened: all 68 outputs complete, no anchors/logs, analysis replaced → refuse | BAD (return 0; :16) | OK (log :343) |
| R44-1-g | actual `run_job`: anchor with the held SHA exists before the child is launched | BAD (launched, no anchor; :17) | OK (log :344) |
| R44-1-h | anchor cannot be created → child not launched | BAD (launched; :18) | OK (log :345) |
| R44-1-i | plan: false reason removed, anchor described, lifecycle limit listed | BAD (:19) | OK (log :346) |

`selftest-2026-10-14-r44-on-v5.log`: **2 OK, 7 BAD, 0 LIMIT**, exit 1; header :2-5 equals the reviewed v5 hashes.
`selftest-2026-10-14-v6.log`: **326 OK, 0 BAD, 2 LIMIT**, exit 0, 1730 s (log :347); header :2-6 equals the v6 hashes in §6; sections A–E (317 controls) all OK, section F (:337-346) all OK. The two LIMITs are the accepted N1 and R38-1 cases.

## 4. Dry run

`plan-dryrun-2026-10-14-v6.txt` (`run_stage1b-v6.py plan gate0|smoke|main`, exit 0 each; 4 / 2 / 68 pending). Input
problems are the expected missing prerequisites (runner manifest, seal, input freeze; Gate-0 / smoke verdicts, smoke
inspection and analysis freeze for later phases); taskkill resolves to the declared System32 path; `GIT_EDITOR` of the
agent shell is flagged. Harness listing unchanged apart from the new file; no output root, no `logs/`; worktree index
mtime unchanged (2026-09-29 13:57).

## 5. Convergence rule

- **Fixed:** R44-1 (P2).
- **Documented (plan v6 §14):** R44-2 lifecycle multiplicity (optional P3; this round was scoped to R44-1; Gate 0 still
  fails any changed event through exact frame comparison); the remaining pre-first-anchor window (no main output can
  exist there); a deliberate coordinated rewrite of every local receipt.
- **R44-3:** the v5 dev-capture log is explained by `run_stage1b-v5-devlog-snapshot.txt`; the receipt for the v5 bytes is
  `selftest-2026-10-14-v5-rerun.log` (317 OK, 0 BAD, 2 LIMIT), and v6 has its own receipt above.

## 6. SHA-256 of the round-6 files

| File | SHA-256 |
|---|---|
| `compare_equivalence_stage1b-v6.py` | 4d36c470b654e3bf5a612a05125285dc709c8bd8d01b5e54798ec52fbb56862a |
| `run_stage1b-v6.py` | 42121e5fe9156a646360b143b9c03bc286700da9db2349df5416e698324c5deb |
| `test_compare_equivalence_stage1b-v6.py` | 5d863b50613b88949bba2ce5170e71005382708efc913b65ce265ea8205d3676 |
| `STAGE1B-PLAN-DRAFT-v6.md` | ca05821242971cc3a3fa51960d1c787e73fa395c29d1703150b13773dbfcff4a |
| `RUNNER-PROVENANCE-TEMPLATE-v6.md` | bad867c25be9ef3e391b60e144fdb7150c21c381631b6c21ac8970a5c5779781 |
| `selftest-2026-10-14-r44-on-v5.log` | b3e50e3c310cb259fc1ab94df47c39f55cd70a08083179e7244437b32a8404e9 |
| `selftest-2026-10-14-v6.log` | 94e82b14b3cd660633572fe7bc5d5cdb320bf50031f5e4a17732f3c9fb3e724f |
| `plan-dryrun-2026-10-14-v6.txt` | 7b819b38883ed30235ecce8d022a0e129ee5435ec7861d17a66ab861fe5b4be6 |

## 7. Still open before the seal (not harness code)

Owner: the exact deadline (with only "after 2026-11-03", any launch from 2026-10-15 has fewer than 21 days by the
conservative bound, plan §0); then commit (no push), publish, provenance receipt, input freeze and seal at their
authorized steps; the analysis scripts, `STAGE1B-ANALYSIS-AMENDMENT.md` and `stage1b-analysis-freeze.sha256` before any
main job; launch from a fresh Windows PowerShell with GIT_* unset.
