# Stage 1b harness — fix report for Codex R28 (Claude, harness fixer; 2026-10-12/13)

Review fixed: `optimize/codex-debate/codex-work/R28-HARNESS-RECHECK-CODEX.md` (verdict NOT READY; N1–N8). Brief:
`BRIEF-HARNESS-FIX-R28.md` (owner decision on N1: behavioural-only Gate 0, explicitly disclosed; no .NET replay tool).
Paths are relative to this folder (`E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/`) unless stated;
`R` = `C:/Code/RideBound-costfirst/src/` (read only), `F` = `E:/Code/RideBound-deadline/simulators/fleetpy-ridebound/`
(read only).

**Only the pure self-test and the `plan` dry check were run.** No FleetPy job, no Runner or other .NET execution, no
build, no publish, no commit, no freeze, no seal. Nothing was written to `E:/Code/RideBound*` or `C:/Code/*`. New files
only: no existing file (originals, `-v2`, earlier logs and reports) was modified.

## 1. Files

| File | SHA-256 | Role |
|---|---|---|
| `compare_equivalence_stage1b-v3.py` | `6c2f21195af641d29f8bee1c8c0e372bbaba6a74e3eb11e9cd90c7ff57cd7d52` | comparator (N1, N2, N4, N6, N8) |
| `run_stage1b-v3.py` | `a5834eebf451c3da00f59fd67a15928ffe0f5ad05d4dcb31fd5d6e4a2d1f2ff2` | driver (N1 names, N3, N4, N5) |
| `test_compare_equivalence_stage1b-v3.py` | `1803d154e48e2de45cc731ba27cc8c11e9d47b68d1ad6e7cc1eebd031f8f144a` | self-test, `--target v3` or `--target v2` (N7 inside) |
| `STAGE1B-PLAN-DRAFT-v3.md` | `b8fbe35c20688331c8cc198279a5aab670ca588f73bfb6d800703363228fcaf7` | plan draft: N1 amendment, R28 §5 paragraph |
| `RUNNER-PROVENANCE-TEMPLATE-v3.md` | `f44f0a1dca5d01342b76d006fe6368888d9a3bd0dc814074b0a72bd449cde6f0` | receipt template; only the harness file names change to v3 |
| `selftest-2026-10-13-v3.log` | `928c5924dc8497b3864ec71f15fa7f3d2f9846bd48d01ce755f2164cdf630cde` | full v3 self-test |
| `selftest-2026-10-13-r28-on-v2.log` | `d372e58e91c1d3d29b96d442576483d5bb90f68ccc6e99c8f10560e5fb323531` | the new R28 controls run against the v2 files |
| `plan-dryrun-2026-10-13-v3.txt` | `87351809a85c0f31ea88fbe1f7c0bef78a533a7e576223be6a031398a319971a` | `plan gate0/smoke/main` with the v3 driver |
| v2 files (unchanged) | comparator `e469cac1…9ce0`, driver `f2399c4f…5e62a`, test `fe795e0e…3bce`, plan `9f6e439b…b3f2d4` | = R28:22-25 |
| originals (unchanged) | comparator `4b3384fa…e2e7`, driver `bce7e2ad…5615` | = R24:22-23 |

Both logs record the SHA-256 of every tested file in their header; the files on disk have exactly those hashes.

## 2. Results

| Run | Log | Result |
|---|---|---|
| Full suite, **v3** | `selftest-2026-10-13-v3.log` | `SELFTEST PASS`: **197 OK, 0 BAD, 1 LIMIT**, 939 s. A (v1/v2 controls on v3) 69 OK; B (R24 controls F1–F6) 79 OK; C (R28 controls N1–N8) 49 OK. The LIMIT is the disclosed N1 limit (§3 N1). |
| R28 controls on the **v2** files | `selftest-2026-10-13-r28-on-v2.log` | `SELFTEST FAIL` (as intended): **7 OK, 40 BAD**. Every finding control fails on v2 for its stated reason (N1 4 BAD, N2 9, N3 9, N4 4, N5 2, N6 10, N7 1, N8 1). The 7 OK are the positives (N2-i, N3-g, N4-e, N5-b, N6-j, N6-k, N8-b). |
| `plan` dry check, v3 driver | `plan-dryrun-2026-10-13-v3.txt` | gate0 4 / smoke 2 / main 68 jobs, all `pending`, exit 0 for every phase. Only the expected blockers: runner-v3 manifest missing, plan not sealed, freeze missing; smoke also `GATE0-BEHAVIOURAL-PASSED.txt`; main also `SMOKE-PASSED.txt` and `SMOKE-INSPECTION.txt`. Reported: 35 distinct cells; behavioural-only verdict names; five external trees present; `git` resolves to `C:/Program Files/Git/mingw64/bin/git.EXE` (inside the frozen Git tree); steering environment names `DOTNET_MULTILEVEL_LOOKUP, GIT_EDITOR, PATH, PATHEXT`; no forbidden redirect; `.pth` audit clean (1 line); user site-packages absent; no drift receipt; power (1, 100), AC-only True; C: free 62,698 MiB. Before and after: output root `C:/RideBoundData/research/tier3-stage1b-v1`, `logs/`, `run-instance.lock` and `INPUT-DRIFT-STOP.txt` absent; worktree index of `E:/Code/RideBound-deadline` unchanged (mtime 2026-09-29 13:57:44). |

## 3. Finding → test → fix → result

### N1 (P1) — middle-decision digests: Gate 0 becomes behavioural-only, explicitly disclosed (owner decision)
- **Decision** (`BRIEF-HARNESS-FIX-R28.md:9-25`): no .NET replay; Gate 0 = recomputable digests + exact behavioural
  equality + link and content checks for decisions 2..N, renamed and disclosed.
- **Tests**: N1-a verdict file names, N1-b disclosure in every in-memory Gate-0 verdict/report, N1-c plan states the
  amended criterion and why, N1-d plan adopts the R28 §5 paragraph; the limit itself is logged as `LIMIT` (N1-LIMIT: a
  consistently rewritten decision-336 digest is still accepted). On v2: N1-a…d BAD.
- **Fix**: comparator `GATE0_PASS_FILE/GATE0_FAIL_FILE/GATE0_WORD` :97-98 and `GATE0_DISCLOSURE` :99-105 (written into
  every Gate-0 header, `main` :1514-1599; pair line "BEHAVIOURALLY EQUIVALENT" :1050; smoke line notes the same limit
  :1376-1414); driver uses the same names :118-119 and checks the pass verdict with `GATE0_WORD`
  (`frozen_input_problems` :687-700). Docstrings state the amendment (comparator :1-22, driver :1-21). Self-test:
  P2v/P3v relabelled **P2n/P3n normalisation tests (validity-limited)**; `rebuild` docstring states the limit.
  Plan v3 §3 "Amendment" (Codex wording R28:56, plus "a protocol-invalid tape with identical actions, ledgers and frame
  content could therefore pass"; results exploratory), §0, §6 G0 row, §8, §9.
- **Result** (v3 log): N1-a…d OK; 1 LIMIT, as decided.

### N2 (P1) — duplicate JSON fields passed
- **Tests** (fresh frame SHA/length and bundle receipts): N2-a R28's counterexample (final `checkpointHash`
  duplicated with the same value), N2-b envelope field, N2-c nested decision field, N2-d field inside an action, N2-e
  transcript-record field, N2-f `summary.json` (status `fail` then `pass`), N2-g `bundle-manifest.json`, N2-h
  non-canonical frame encoding, N2-j smoke decoding (executionPath `none` then `validatedIncumbent`); positive N2-i
  canonical re-encoding of a frame. On v2: N2-a…h and N2-j PASS → 9 BAD; N2-i OK.
- **Fix** (comparator): `DuplicateField` / `_strict_pairs` / `strict_loads` :196-224 (object_pairs_hook rejecting
  duplicates; protocol content also rejects non-integers and NaN/Infinity, as F/actual_fleetpy_medium_verify.py:30-49);
  `decode_frame` / `decode_record` :225-246 require canonical bytes (frozen transcript contract,
  F/actual_fleetpy_medium_verify.py:839-846,879-883); used by `Run.__init__` for bundle/summary/run-00 :548-550,
  `Run.frame` :591-593, the per-frame loop of `facts` :770-787 and `objective_level_problems` :1301-1366. Driver:
  `state_of` :181-194, `cell_problems` :293-329 parse strictly; WP4 files were already strict (`canonical_wp4.parse_strict`).
- **Calibration**: all 8,485 records and frames of the four Gate-0 references are canonical (read-only probe; R3 in
  the v3 log passes with strict decoding).
- **Result**: N2-a…j OK (N2-a fails because the duplicated field makes the final checkpoint frame undecodable).

### N3 (P2) — resolver closure and durable drift
- **Tests**: freeze world (in memory) N3-a `PATH` changed, N3-b `SETUPTOOLS_USE_DISTUTILS` set, N3-c `GIT_DIR` set,
  N3-d `PYTHONPATH` recorded consistently but still refused, N3-e `git` resolving elsewhere, N3-f `.pth` path outside the
  frozen trees, N3-g complete positive; N3-h drift seen at admission is still refused by a NEW driver invocation after the
  bytes are restored; N3-i post-phase full verification (main included) writes the stop receipt; N3-j declared resolver
  scope; read-only N3-k (`git` resolves inside the Git tree here) and N3-l (real `.pth` audit clean). On v2: 9 BAD,
  N3-g OK (N3-k/l run on v3 only).
- **Fix** (driver): Git installation as a fifth external tree :133-139; `FROZEN_ENV_PREFIXES` + `GIT_` :140,
  `FROZEN_ENV_NAMES` (PATH, PATHEXT, SETUPTOOLS_USE_DISTUTILS) :141, `FORBIDDEN_ENV` :143, `REQUIRED_TOOLS` :144;
  `frozen_env` :379-387, `forbidden_env` :388-395, `resolve_tool` :396-404, `pth_lines`/`_inside`/
  `pth_redirect_problems` :405-445; `freeze_problems` :446-571 adds `tool` lines (:443, :553-558), forbidden redirects
  (:549) and the `.pth` audit (:560); `write_freeze` :883-918 records the resolved tools and refuses forbidden
  redirects, tools outside the trees and `.pth` redirects. Durable stop: `DRIFT_STOP` :122, `write_receipt` /
  `record_input_drift` :701-720, written by `Admission.admit` on stat drift (:801) and by `post_phase_problems`
  :874-882 (called after every phase, `main` :984); `latch_problems` refuses while it exists (:602). The stat-based
  between-check limitation is described precisely in the driver docstring (:12-16) and plan §1.1.
- **Result**: N3-a…l OK.

### N4 (P2) — frozen-input exceptions skipped the verdict
- **Tests**: N4-a injected `ValueError` from the frozen-input check after the jobs started (R28's probe), N4-b
  unreadable new runner tree after start (receipt must carry the frozen expected identity), N4-c malformed frozen WP4,
  N4-d unreadable frozen file, positive N4-e unstarted phase still refused without a verdict. On v2: N4-a…d BAD, N4-e OK.
- **Fix**: comparator `guarded_identity` :1497-1513 (failed identity + frozen expectation from the manifest header);
  `main` :1514-1599 establishes latch / readiness / started first, then guards identities and the frozen-input check
  (:1562-1575) and still writes the verdict before the report (:1592-1593). Driver: `config_problems` :240-274 turns
  malformed configs into problems (:250-269), `freeze_problems` turns unreadable files/trees into problems (:510),
  `_guard` / `input_integrity_problems` / `frozen_input_problems` :661-700 guard every check group.
- **Result**: N4-a…e OK.

### N5 (P2) — AC not rechecked after the admission queue
- **Tests**: N5-a AC at the power wait, lost before admission → no launch; positive N5-b; N5-c a second driver
  instance refused. On v2: N5-a and N5-c BAD, N5-b OK.
- **Fix** (driver): `Admission.admit` :777-819 rechecks `ac_only()` after STOP, latches, stat drift and disk,
  immediately before the reservation and the launch (:806); `acquire_instance_lock` / `_os_lock` :820-839 (non-blocking
  OS lock on `run-instance.lock`, released by the operating system at process end), taken by `run` (:966).
- **Result**: N5-a…c OK.

### N6 (P2) — optimal smoke evidence could omit tie levels / carry invalid bounds
- **Tests**: N6-a R28 probe (optimal lists keep only accepted count and cost), N6-b R28 probe (cost −1), N6-c ranks out
  of ordinal order, N6-d rank for a vehicle absent from the generation evidence, N6-e boolean `levelIndex`, N6-f
  inconsistent gap arithmetic, N6-g best bound on the wrong side, N6-h forced certificate witness without the forced
  level, N6-i solver work above the frozen budget; positives N6-j consistent unproven prefix, N6-k calibration on
  archived `Mplus-30-d20181114-s10-r3-w07` (442 decisions with forced levels), N6-l budgets equal the frozen ON WP4. On
  v2: 10 BAD, N6-j/k OK. The R24 fixtures F4-15/F4-17 now rebuild their numeric invariants (`unprove`).
- **Fix** (comparator): `MAXIMIZE_LEVELS` :179 (only accepted count is Maximize, R/RideBound.Algorithms/Policies/
  SolverBackedFleetSelection.cs:329; others MinSum/MinMaximum :556-574), `FORCED_STAGES` :181
  (R/RideBound.Runner/Protocol/RunnerSession.cs:1104), `BUDGETS` :183-185; `decision_evidence` :1085-1275: expected
  ranks from the generation's vehicles in ordinal order (:1125; SolverBackedFleetSelection.cs:236-238,402-416), rank
  prefix and forced-level checks (:1187-1198), complete optimal list (:1246-1257), forced incumbent ≥ 1, counters within
  budgets; `bound_problems` :1276-1300 mirrors `ObjectiveSolveBound.Create` (R/RideBound.Application/Optimization/
  CandidateSelectionSolver.cs:110-150): non-negative canonical integers, sense side, gap, denominator max(1, incumbent),
  proof flag.
- **Evidence for the rules** (read-only probes): ranks == sorted `vehicleLosses` vehicles in all 2,819 Gate-0
  decisions; forced level present exactly when a certificate witness has stage `forcedReference`/`forcedNoWorse` (Mplus
  samples: 321/321 and 525/525 with, 0/121 and 0/1,292 without); every bound non-negative and denominator rule holds on
  59,023 Gate-0 bounds.
- **Result**: N6-a…l OK; F4-19 calibration (2,819 Gate-0 decisions, CF order disabled) still 0 problems.

### N7 (P3) — self-test fallback hid a failed live tree verification
- **Test**: N7-a constructs the self-test context with an injected failing live identity; the failure must be
  reported BAD. On v2 (the v2 test's own `Ctx`): nothing reported → BAD.
- **Fix** (test): `Ctx` keeps `live_old_id`, reports `R1-live` BAD before any manifest-only identity, and R1 checks the
  live identity (test file `Ctx`, `section_a` R1).
- **Result**: N7-a OK; R1 (live tree 429 files, ac251ef3…) OK.

### N8 (P3) — Unicode fallback skipped the integer-range check
- **Tests**: N8-a key U+E000 with 2^53 must be rejected; positive N8-b. On v2: N8-a BAD.
- **Fix**: `canonical_bytes` :292-331 finishes the whole validating walk, then chooses the writer (`needs_ordinal`
  :301,322,326).
- **Result**: N8-a, N8-b OK; F1-R5 (fast writer = C# mirror) OK.

## 4. Plan and provenance (R28 §5, §7)
- `STAGE1B-PLAN-DRAFT-v3.md` §1.1 adopts the R28 §5 paragraph **verbatim** (R28:176), followed by one clarification that
  under the amended Gate 0 "valid evidence" means the recomputable digests and the per-run link/content checks; the
  unqualified "same inputs / unchanged from T3.8" wording is replaced (§1, §1.1). §3 records the amendment; §4 the N6
  smoke rules; §10 the safety changes and the v3 sealed list (five external-tree manifests).
- F6 text from R24 is retained (start rule with `<FILL>` facts, time-box consequence, Stage-2 sentences, H3 wording).
- `RUNNER-PROVENANCE-TEMPLATE-v3.md` differs from v2 only in the harness file names; recorded facts still need manual
  review before the seal (R28:207).

## 5. Open items and limits (not fixed, with reason)
1. **N1 limit (accepted by the owner):** the digests of decisions 2..N are not validated by production code; a
   protocol-invalid tape with identical behaviour could pass. Disclosed in every Gate-0 verdict, the plan and the log
   (`LIMIT`). Results are exploratory.
2. **N3 residuals:** only `git` was identified as a bare-name tool (driver `harness_problems`; the preflight's repository
   inventory, F/actual_fleetpy_medium_preflight.py:727-752); other PATH resolutions were not audited. The OS and system
   DLLs are not frozen (not required, R28:90). The steering environment depends on the shell (this shell sets
   `GIT_EDITOR`), so `freeze` and `run` must be done from the same shell setup. Per-job checks are stat-based; bytes
   read by a running job are not authenticated. No historical byte baseline exists (R28 §5 accepted as a limitation).
3. **N5:** the lock file `run-instance.lock` will remain (empty) after a run; it is never deleted. The instant between
   the last AC check and process creation remains.
4. **N6:** forced activation is required only when a certificate witness proves it; evidence 1.1.0 has no other marker
   of an active-but-unselected forced level.
5. Still open from R24/R28: the `<FILL>` start-rule facts; manual review of the provenance receipt; the main-phase
   analysis scripts (R28:46); invoking the comparator only after `run` returned 0; a new Codex review before any seal.

## 6. Process record
- The -v3 files were developed in the session scratchpad
  (`C:/Users/quang/AppData/Local/Temp/claude/e--Code-RideBound/4f238113-dd3b-4bd1-b13f-53613b57f0e6/scratchpad/r28/`)
  and run there through a shim (`devrun.py`, not copied) that loads each draft under its future harness path. Two
  test-fixture defects were fixed during development (N2-b assumed `messageType` was the first canonical field; N2-d
  used a decision without actions). Development runs: v2 target 7 OK / 40 BAD; v3 target 197 OK / 0 BAD / 1 LIMIT
  (998 s). The five files were then copied into this folder by exclusive creation on 2026-10-13 13:39:41,
  byte-identical, and never modified afterwards. The logged runs used those copies (r28-on-v2 written 13:42, exit 1;
  v3 exit 0, 939 s).
- Read-only actions outside this folder: C# sources in `R/` (CandidateSelectionSolver.cs, SolverBackedFleetSelection.cs,
  RunnerSession.cs); probes of archived runs in `C:/RideBoundData/research/tier3-t38-v1` (canonical encoding, ranks,
  forced levels); the job interpreter's `.pth` file and `sys.path` (`python -I -c`), `which`/`where.exe git`,
  `git --version`, the size of `C:/Program Files/Git`; the dry run's `git rev-parse HEAD` in
  `E:/Code/RideBound-deadline` (index mtime unchanged); `date`. Bytecode writing was disabled (`-B`,
  `PYTHONDONTWRITEBYTECODE=1`).
