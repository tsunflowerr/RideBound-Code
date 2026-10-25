# Stage 1b harness — fix report for Codex R24 (Claude, harness fixer; 2026-10-11/12)

Review fixed: `optimize/codex-debate/codex-work/R24-HARNESS-REVIEW-CODEX.md` (verdict NOT READY; F1–F6). Earlier requirements:
`R22-CODE-REVIEW-CODEX.md` §5. All paths below are relative to this folder
(`E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/`) unless stated otherwise. `R` =
`C:/Code/RideBound-costfirst/src/` (read only), `F` = `E:/Code/RideBound-deadline/simulators/fleetpy-ridebound/` (read only).

**Nothing was run except pure self-tests and the `plan` dry check.** No FleetPy job, no Runner run/publish, no `dotnet`
command, no build, no commit, no `freeze`, no seal, no comparator `gate0`/`smoke` CLI (its `main()` was exercised only
in memory inside the self-test, with all file I/O replaced). The originals are unchanged (hashes in §1 equal R24:22-30).

## 1. Files

| File | SHA-256 | Status |
|---|---|---|
| `compare_equivalence_stage1b-v2.py` | `e469cac16271a3fbcf7332a8f22590fa4328023f303ac64d9d14dc414ff99ce0` | new — comparator (F1, F3, F4) |
| `run_stage1b-v2.py` | `f2399c4f28028e9344a139296a575f21f65d93199305b5bad083eeefe125e62a` | new — driver (F2, F3, F4 inspection gate, F5) |
| `test_compare_equivalence_stage1b-v2.py` | `fe795e0e1f501e467f04060b9a2e93eb6e5240912537cd70e10dffeb1e4d3bce` | new — self-test, `--target v2` or `--target original` |
| `STAGE1B-PLAN-DRAFT-v2.md` | `9f6e439b992e9c6bfc4dc8caf15cfab95c6722840001ee100bbe628974b3f2d4` | new — plan draft (F6 + v2 harness references) |
| `RUNNER-PROVENANCE-TEMPLATE-v2.md` | `6193e67011ca3b606eb65ff259c64dae9b0c62b8b219a4f941638cfa093ce8e4` | new — receipt template (R24 §3 notes) |
| `selftest-2026-10-12-r24-original.log` | `0de0be53616754e2b052cd76cdf63a475fb90fbb08fa14cc5d67860987e0a44b` | new — reproduction: R24 controls against the ORIGINAL files |
| `selftest-2026-10-12-v2.log` | `4cea513cfc6be5d3d4b39bb9719e8159599dada674a1e147f89b2c609db08034` | new — full v2 self-test |
| `plan-dryrun-2026-10-12-v2.txt` | `7c358a29ef4d48c83cd3c1237df0a8d6c0790899d6908d1e549c231c2b94d7bf` | new — `plan gate0/smoke/main` with the v2 driver |
| `compare_equivalence_stage1b.py`, `run_stage1b.py`, `test_compare_equivalence_stage1b.py`, `STAGE1B-PLAN-DRAFT.md` | `4b3384fa…e2e7`, `bce7e2ad…5615`, `689e4fe2…80f2`, `8b7338c0…ce1b` | unchanged originals (= R24:22-30) |

Both logs record the SHA-256 of every tested file in their header; the current files have exactly those hashes. No
`-v3` file was needed: after the coordinator's no-rewrite rule nothing was changed (the `-v2` modification times
2026-10-12 10:18–10:32 are my last edits, made before the logged runs started at 10:34:43; §6).

## 2. Results

| Run | Log | Result |
|---|---|---|
| R24 controls vs the **original** files | `selftest-2026-10-12-r24-original.log` | `SELFTEST FAIL`: **16 OK, 49 BAD** — every R24 finding reproduced (F1 7 BAD, F2 9, F3 13, F4 10, F5 5, F6 5). The 16 OK are positives (valid fixtures pass, admitted when admissible) and behaviour v1 already had right (F3-2, F3-5, F4-7, F4-8, F4-9). |
| Full suite vs **v2** | `selftest-2026-10-12-v2.log` | `SELFTEST PASS`: **148 OK, 0 BAD, 1 GAP** — A (all v1 controls + rebuilt variants) 69 OK; B (R24 controls) F1 17 OK + 1 GAP, F2 12, F3 18, F4 19, F5 6, F6 7. Wall time 19,329 s is inflated by a network/session outage during the run; the uninterrupted debug run of the same suite took 723 s. |
| `plan` dry check, v2 driver | `plan-dryrun-2026-10-12-v2.txt` | gate0 4 / smoke 2 / main 68 jobs, all `pending`, exit 0. Only the expected blockers: runner-v3 manifest missing, plan not sealed, freeze missing; smoke also `GATE0-PASSED.txt`; main also `SMOKE-PASSED.txt` and `SMOKE-INSPECTION.txt`. All other checks pass (configs, bindings, wrapper, harness HEAD, 35 distinct cells, references). External trees exist; frozen environment `{}`; user site-packages absent; power (1, 100), AC-only predicate True, battery override unset; C: free 60,926 MiB. The output root `C:/RideBoundData/research/tier3-stage1b-v1` and `logs/` were absent before and after. |

The GAP is a documented limit, not a pass (F1 row and §4.1).

## 3. Finding → test → fix → result

### F1 (P1) — invalid digests accepted by the Gate-0 comparator
- **Reproduced** (original log): F1-a (R24's counterexample: valid DLL change, wrong final `checkpointHash`, fresh
  transport receipts) PASSES; also F1-b decision-1 digest consistently rewritten, F1-c the v1 P2 fixture (stale
  `manifestHash`), F1-d `manifestHash` rewritten consistently, F1-e initial state hash, F1-f final state hash, F1-g the
  v1 P3 fixture (arbitrary digests): all PASS on v1 (7 BAD).
- **Fix** (`compare_equivalence_stage1b-v2.py`): per-run digest validity before any cross-run comparison,
  `digest_problems` :319-404, called from `facts` :774-780; formulas mirrored from the Runner (C# lines cited in the
  docstring :11-31): `manifest_hash` :254-270 (R `Contracts/Serialization/ProtocolHash.cs:10-29`,
  `Contracts/Protocol/InitializeRunMessages.cs:109-126,350-402`, `HelloMessages.cs:109-117,274-287,463-486`),
  `initial_online_state`/`online_state_hash` :271-284 (`Runner/Protocol/RunnerSession.cs:337-361`,
  `Application/State/OnlineState.cs:35-58`, `Runner/Online/OnlineStateCanonicalizer.cs:19-50,131-140,656-672`),
  `decision_hash`/`decision_input_bytes`/`decision_projection_bytes` :285-312 (`ProtocolHash.cs:31-67`,
  `RunnerSession.cs:532-540,768-800`, `Contracts/Protocol/DecisionMessages.cs:457-527`), `checkpoint_hash` :313-318
  (`Contracts/Protocol/CheckpointMessages.cs:32-45,129-159`), plus final-checkpoint closure (epoch, event sequence,
  time; as `F/actual_fleetpy_medium_verify.py:1133-1146`). A fast exact canonical writer `canonical_bytes` :204-238
  (validating walk + C-level `json.dumps`, falls back to the C# mirror `canonical_wp4` whenever UTF-16 ordinal key order
  could differ). Raw cross-run digest equality stays waived (R24:66).
- **Valid positives** (R24:66): the v1 P2/P3 fixtures were protocol-invalid; they are kept as negatives (F1-c, F1-g)
  and replaced by **P2v/P3v**, whose verifiable digests are recomputed by code independent of the comparator (the
  adapter verifier's `_framed_hash/_manifest_hash/_checkpoint_hash`, `F/actual_fleetpy_medium_verify.py:62-113`, and
  the test's own state/decision hash, test file `rebuild`).
- **Result** (v2 log): F1-R4 — on all four Gate-0 references the comparator formulas reproduce every archived
  `manifestHash`, initial state hash, decision-1 hash, `checkpointHash` and final state hash, and agree with the adapter
  verifier and the fixture builder; F1-R5 — the fast writer equals the C# mirror on the real final state, checkpoint
  content, decision-1 input/projection, manifest, escape/astral/extreme-integer samples and the ordinal fallback, and
  rejects null/float/out-of-range/lone surrogate; F1-a…g FAIL as required; P2v, P3v PASS. Section A: R3 shows all four
  references content-verified; the init-edit controls got rebuilt variants (N1r, N2r, N3r, N3br, N4-6r, N8r, N11r, N14r)
  that fail for the intended reason with no digest problem masking it.
- **Not fixed — GAP**: `decisionHash` of decisions 2..N and the intermediate state hashes (§4.1).

### F2 (P1) — freeze accepts an empty/incomplete inventory and omits external inputs
- **Reproduced**: header-only freeze → `[]` (F2-1, = R24:74-76); duplicate, missing and extra entries → `[]`
  (F2-2/3/4); malformed line → `ValueError` escapes (F2-5); Python-environment change, new `PYTHONPATH`, user
  site-packages appearing → `[]` (F2-7/8/9); no FleetPy / interpreter environment / .NET runtime in the freeze (F2-10).
- **Fix** (`run_stage1b-v2.py`): `freeze_problems` :350-444 verifies an exact, non-empty, duplicate-free inventory:
  file lines must equal `freeze_targets()` (:298-319, v1 targets + the external-tree manifests at :316), every external
  tree listed once with its root, its manifest header equal to the freeze line and the live tree equal to the manifest,
  the frozen environment equal to the current one, and the required-absent paths absent; malformed lines are problems.
  External inputs (:111-124): `EXTERNAL_TREES` = FleetPy root (without `.git/`), the job interpreter's prefix
  `E:/RideBoundData/wp7/envs/fleetpy-1.0.2`, `C:/Program Files/dotnet/shared/Microsoft.NETCore.App` (every installed
  version, so the framework the framework-dependent runner resolves cannot change) and `host/fxr` (`dotnet.exe` stays
  a file target); `FROZEN_ENV_PREFIXES` PYTHON*, DOTNET_*, COREHOST_*, COMPLUS_*, OMP_*, MKL_*, OPENBLAS_*, RB_* (minus
  the variables the driver sets itself); `ABSENT_REQUIRED` = the interpreter's user site-packages. Tree manifests use the
  `runner_manifest.py` format (`tree_files`/`tree_body` :320-337); `write_freeze` :675-702 writes them mode "x" before the
  freeze. Verification: full before a phase (`frozen_input_problems` :531-548) and again by the comparator before any
  verdict (`compare…-v2.py` main :1360); before every job a stat snapshot check (`frozen_paths`/`stat_snapshot`/
  `stat_drift` :572-602, used in `Admission.admit` :603-640) halts the phase on drift. The repository inventory check of
  every job is unchanged.
- **Result**: F2-1…F2-10 OK; F2-6 positive passes; F2-11 (read only) the tree walker equals `runner_manifest.body` on the
  real FleetPy tree (750 files without `.git/`) and NETCore.App (189 files).
- **Not fixed — no historical baseline** (§4.2).

### F3 (P2) — failure latches incomplete; malformed started runs escape
- **Reproduced**: `run gate0` admitted with `GATE0-FAILED.txt` (F3-1), `run smoke`/`run gate0` with `SMOKE-FAILED.txt`
  (F3-3/4), closed phases re-runnable (F3-6/7); a job launched after a failure appears (F3-10); malformed
  `summary.json` → `SystemExit` escapes, no verdict (F3-11); report written before the verdict (F3-12); reserved verdict
  name and a path outside the folder accepted as report (F3-14/15); gate0 judged while `SMOKE-FAILED.txt` exists
  (F3-16); malformed decision payload / checkpoint without content raise (F3-17/18).
- **Fix**: driver `latch_problems` :469-479 (any `*-FAILED.txt` refuses every phase; a phase with its own pass verdict is
  closed), used by `frozen_input_problems` :531-548, by `run_job` right before launch :641-674 and by the admission
  :603-640; comparator: `MalformedRun` :151-154 and `Run` :445-502 (unreadable bundle = exception subclass), `facts`
  :624-849 turns malformed content into problems, `evaluate_gate0`/`evaluate_smoke` :1261-1318 record any evaluation
  error as a failed job; `report_path_problems` :1243-1256 (bare new name with the phase prefix, never a verdict name)
  checked first in `main` :1319-1377; `main` refuses while any latch exists (:1351), keeps "not ready → no verdict" for
  unpublished/unsealed/unfrozen/unstarted (:1354-1359, driver `readiness_problems` :519-530), treats input drift after
  the jobs started as FAILED (:1360 → `INPUT/FREEZE FAIL`), and writes the verdict first, then the report (:1370-1371).
- **Result**: F3-1…F3-18 OK (F3-8/9/13 positives).

### F4 (P2) — smoke can waive the execution evidence
- **Reproduced**: decisions with no execution evidence (including R24's `feasible` probe), no selection, a wrong
  omission sum, a saturated count, `executionPath none`, an optimal solve without bounds and a fallback with the wrong
  detail code all give zero problems on v1 (F4-4/5/6/10/11/12/13/16); v1 prints only list lengths (F4-18); `main` is
  admitted without an inspection record (F4-1).
- **Fix** (`compare_equivalence_stage1b-v2.py`): `decision_evidence` :988-1123 requires solver status `completed`,
  evidence version 1.1.0 (constants :129-148; every one of 111,766 archived T3.8 decisions in 187 run folders has this
  shape — read-only survey in this session), exact generation/selection fields, generation and omission accounting
  (`omittedCandidateCount` = Σ omissions, generation work = Σ exploration, digest/count pairing, no saturation — rules of
  `F/actual_fleetpy_medium_verify.py:202-306,404-446`), a validated publishing path, path/status consistency
  (`R/Application/Optimization/CandidateSelectionExecution.cs:246-397`), bounds present unless the absence has a
  supported reason (no-incumbent primary, fallback final with its detail code, unproven prefix of a feasible solve;
  `CandidateSelectionSolver.cs:216-226`), every level of an optimal solve proven, and the cost-first order
  (`cf_order_problem` :967-987); `objective_level_problems` :1124-1189 aggregates statuses, paths, work/omission sums and
  per-level incumbent/best-bound sums, printed by `smoke_report` :1199-1236. Driver `inspection_problems` :495-511: `main`
  requires `SMOKE-INSPECTION.txt` naming the SHA-256 of `SMOKE-PASSED.txt`, the inspector and `inspection: accepted`.
  The configuration identity remains the verified ON binding (R22:242).
- **Result**: F4-1…F4-19 OK, including legitimate positives F4-14 (safeNoOp after no incumbent), F4-15 (greedy
  fallback after a rejected incumbent), F4-17 (feasible unproven prefix), and the calibration F4-19: every rule except
  the cost-first order holds on all 2,819 archived Gate-0 decisions (no false rejection of genuine evidence).

### F5 (P2) — AC-only, STOP and disk admission
- **Reproduced**: battery with `RB_ALLOW_BATTERY_MIN_PERCENT=50` admitted (F5-1/2); STOP created during the power wait
  ignored when AC returns (F5-3); disk not rechecked after the wait (F5-4); three overlapping starts all admitted with
  room for two allowances (F5-5).
- **Fix** (`run_stage1b-v2.py`): `environment_problems` :512-518 refuses the battery override; `ac_only` :553-558 strict
  AC; `wait_for_power` :559-571 checks STOP on every pass and at return; `Admission` :603-640 rechecks STOP, latches,
  stat drift and disk under one lock immediately before launch and reserves 410 MiB per running job
  (free − 51,200 − reserved ≥ 410), released in `run_job` :641-674 `finally`; 1,200-s deferral and keep-awake cleanup
  unchanged (`main` :703-760).
- **Result**: F5-1…F5-6 OK (F5-6: reservation released after a job).

### F6 (P3) — plan text
- **Reproduced**: the v1 draft lacks the A1/deadline/≥3-weeks start rule, the "drop the rest … future work"
  consequence, "same second published binary", "exercise the changed 120-s path against available controls", and has
  the old H3 sentence (F6-1…5 BAD).
- **Fix** (`STAGE1B-PLAN-DRAFT-v2.md`): §0 start rule (R17:281-288; R18:222) with `<FILL>` fields for the A1 milestone,
  confirmed deadline and time left (or an amendment); §3.5 and §8 time-box consequence (R17:285-286); §9 both Stage-2
  sentences (R18:226,228); §6 H3 failure text replaced verbatim (R24:140). Also: §1 notes 35 distinct cells (R24:164,
  correcting `HARNESS-REPORT.md:98`), §1.1/§3/§4/§10 describe the v2 checks, sealed list names the v2 files and the four
  external-tree manifests. Driver `runner_problems` :191-211 now also refuses a receipt with unfilled template fields
  (R24:197); `RUNNER-PROVENANCE-TEMPLATE-v2.md` starts the publish snippet with `Set-Location` (R24:201).
- **Result**: F6-1…F6-7 OK.

## 4. Not fixed, or fixed only partly (with reason)

1. **F1 — decisions 2..N.** Their `decisionHash` (and the intermediate state hashes) hash the Runner's pre-batch online
   state (`RunnerSession.cs:787-794`), which a transcript contains only at the final checkpoint. Recomputing it would
   mean reimplementing the Runner's reducer in Python, which `AGENTS.md` forbids. The v2 comparator verifies both ends
   of the chain by content (manifest, initial state, decision 1, final state, checkpoint) and keeps the per-run link
   checks and the full normalized content equality for the middle; the self-test logs this as **GAP** (F1-GAP: a
   consistent rewrite of decision 336's digest is still accepted). A complete fix would be a replay validator that feeds
   the new run's adapter frames to the frozen v3 Runner and requires byte-identical Runner frames — it runs .NET, so it
   needs an owner decision and was not built. An observation for Codex/owner, not adopted (R24:66 keeps raw equality
   waived) and not tested: with the archived Gate-0 labels the `runId` is identical, and publication/breach IDs derive
   from the batch and run, not the binary (`R/Runner/Protocol/RunnerSession.cs:845`;
   `R/Application/Commitments/CommitmentDecisionValidator.cs:1138-1146`;
   `R/Runner/Online/ExogenousServiceQualityBreachBridge.cs:203-224`), so an equivalent rebuilt run should reproduce the
   reference's online-state hashes byte for byte; requiring that would be a stronger Gate-0 contract.
2. **F2 — no historical baseline.** No archived byte-level record of the FleetPy tree, Python environment or .NET runtime
   used by T3.8 was found (searched the scratchpad and `docs/18`; bundles store only `artifactReceiptsEqual`). The freeze
   binds these trees from freeze time on; Gate 0 is the behavioural check. Read-only facts (not proof of equality):
   FleetPy checkout HEAD `053aa9d4…` = `F/ridebound_fleetpy/session.py:17`; installed `Microsoft.NETCore.App` and
   `host/fxr` are 10.0.9 only; `gurobipy` in the environment is dated 2026-09-08, before the T3.8 runs (2026-10-02).
   Also: `PATH` is not frozen; the frozen environment depends on the shell (one of my shells listed
   `DOTNET_MULTILEVEL_LOOKUP=0`, the dry run listed none), so `freeze` and `run` must use the same shell setup; per-job
   checks are stat-based and a same-size, same-mtime change is caught only by the comparator's full re-hash (→ FAILED);
   the user-site path is the one observed for this interpreter (`Python310`).
3. **F3** — invoking the comparator while jobs are still running judges the unfinished runs as failures (v1 behaviour
   kept, R22:208); operator rule: call it only after `run` returned 0. No driver lock was added.
4. **F4** — the inspection record is a human attestation; the driver checks its form and hash only. Evidence version is
   pinned to 1.1.0; a different version stops the smoke by design.
5. **F5** — the instant between admission and process launch remains (R24:124); 410 MiB/job is an estimate, not a
   measured CF output size (R24:128).
6. **F6** — A1 milestone, deadline and time left are `<FILL>`: I have not established them (R24:138).
7. Still open from R24: main-phase analysis scripts not written (R24:193); provenance facts need manual review
   (R24:197); a fresh Codex review of the v2 harness before any seal.

## 5. How to reproduce

`PY = E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe`, run in this folder with `PYTHONDONTWRITEBYTECODE=1`:
- `PY -B test_compare_equivalence_stage1b-v2.py --target original <new-log>` → expected `SELFTEST FAIL`, 16 OK / 49 BAD.
- `PY -B test_compare_equivalence_stage1b-v2.py --target v2 <new-log>` → expected `SELFTEST PASS`, 148 OK / 0 BAD / 1 GAP.
- `PY -B run_stage1b-v2.py plan gate0|smoke|main` → no job, nothing created.

## 6. Process record (what happened, including mistakes)

- 2026-10-11: v2 files written; first v2 debug run (console only) was stopped by the session limit after Section A
  control N7 (all OK so far) and was slow because the digest checks canonicalized multi-MB states character by character;
  the fast exact writer (`canonical_bytes`) was added and is checked by F1-R5.
- 2026-10-12, second debug run (console only): 147 OK, **1 BAD**, 1 GAP. The BAD was a test defect: the F1-R5 fallback
  sample (keys `\ue000k`, `z😀`) orders identically by code point and by UTF-16, so it could not discriminate; replaced by
  U+E000 vs U+1F600. Not a comparator defect.
- Two editing mishaps, repaired before the logged runs: the Edit tool turned some `\u` escapes into literal characters
  (comparator regex at :201 restored to `\ue000`, pattern verified identical), and a shell heredoc collapsed doubled
  backslashes in three test lines (raw control characters, a syntax error) — rewritten with a backslash-free script.
  The final Python files are ASCII except `§` in docstrings/comments (comparator :4, :97, :130; driver :4) and in one
  refusal message (driver :515).
- Last `-v2` edits at 10:18–10:32 (log-name date, fast-writer wiring, F1-R5, escape repairs); logged runs started 10:34:43;
  no file was changed afterwards.
- Read-only actions outside this folder: C# sources in `C:/Code/RideBound-costfirst`; `F/` sources and the import of
  `actual_fleetpy_medium_verify.py` (with `-B`; it was also run read-only on the four references, where its whole-bundle
  check fails on T3.8 terminal counters, so only its hash helpers are used); `git rev-parse HEAD` in
  `E:/Code/RideBound-deadline` (driver check), plus once a path-limited `git status --porcelain` there and
  `git rev-parse`/`git status --porcelain --ignored`/`git log -1` in the FleetPy checkout. Their index files were not
  rewritten (mtimes 2026-09-29 13:57:44 and 2026-08-13, checked before and after the dry run). Read-only probes of the
  archived runs (digest reproduction, evidence survey), hashing timings of the external trees (Python environment:
  57,750 files, 18.8 s), `GetSystemPowerStatus`, free-space reads. No `__pycache__` or other file was created under
  `tang3/` or `F/`.
