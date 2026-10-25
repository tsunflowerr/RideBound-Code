# Stage 1b harness — fix report for Codex R41, v2 (Claude, harness fixer; 2026-10-14)

Review answered: `optimize/codex-debate/codex-work/R41-HARNESS-RECHECK-V4-CODEX.md` (NOT READY; R41-1…R41-5 and §6).
Paths are relative to the harness folder `E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/` unless
stated; `F/` = `E:/Code/RideBound-deadline/simulators/fleetpy-ridebound/`, `A/` = `C:/RideBoundData/research/tier3-t38-v1/`
(both read only).

**Run:** only the pure self-test (three times, OK) and the `plan` dry check. No FleetPy job, no Runner or other
.NET execution, no build, publish, commit, freeze or seal; Codex and Antigravity were not launched. Nothing was written to
`E:/Code/RideBound*` or `C:/Code/*`. Only new files were created; no existing file was edited.

## 0. Why this report is "-v2", and what already existed

While my session was paused (session limit), another agent copied my v5 drafts into the harness and wrote
`HARNESS-FIX-REPORT-R41.md` (05:31, "Antigravity & Claude", SHA-256 e7a8dec3…8427). I keep it unchanged. It is
incomplete and partly inaccurate:

- It lists `selftest-2026-10-14-v5.log` (SHA-256 fa14a355…feb5) as the self-test log. That file is **byte-identical to
  my development console capture** (`scratchpad/r41tools/dev-v5.out`, run through a development shim): it is stdout
  in the console code page (one 0x97 byte, so not valid UTF-8; Codex R43 §6) with my shell's `exit=0` line appended,
  not the test's own UTF-8 log. It tested `run_stage1b-v5.py` **449c1d63…edb6**, while the delivered driver is
  **7d133819…5b76**. The only difference is one string literal at `run_stage1b-v5.py:857` (`plan section 13)` →
  `plan v5 section 12)`, a message text I corrected after the development run had started). Reconstruction:
  `run_stage1b-v5-devlog-snapshot.txt` = the delivered driver with that literal reverted; its SHA-256 is exactly
  449c1d630b474c2c55e8d5c6c6f1d36a14d6b50085ae4f3f7e43346a67f5edb6. This explains Codex R43-2 but does not repair it:
  the receipt for the delivered bytes is the new run `selftest-2026-10-14-v5-rerun.log` (§5).
- It says the `plan` dry check was run; no dry-run file existed. `plan-dryrun-2026-10-14-v5.txt` is new (§6).
- No R41-on-v4 reproduction log existed. `selftest-2026-10-14-r41-on-v4.log` is new (§5).
- **Codex has already reviewed v5 twice:** R43 (`codex-work/R43-HARNESS-RECHECK-V5-CODEX.md`, NOT READY: R43-1 P2,
  R43-2 P2, R43-3 P3) and R44 (`codex-work/R44-HARNESS-RECHECK-V5-CODEX.md`, NOT READY on one P2, R44-1 = R43-1;
  R44-3 retires R43-2 as a diagnostic-only difference, using the snapshot below; R44-2 = R43-3, optional P3). R44-1 is
  answered in round 6 (`HARNESS-FIX-REPORT-R44.md`, v6 files).

## 1. Files (round 5)

| File | Role | SHA-256 |
|---|---|---|
| `compare_equivalence_stage1b-v5.py` | comparator (R41-2, R41-3) | 690d1262e7a8b6f1f75a1c03b4d220c8866c4d22a37d24f9c32d4e16a2a80cd3 |
| `run_stage1b-v5.py` | driver (R41-1, R41-4) | 7d133819e0bea696d45e5a34a6f91f1569dac512d80935aae74d007503b85b76 |
| `test_compare_equivalence_stage1b-v5.py` | self-test; `--target v5` (A–E) or `--target v4` (E only) | 7735deee68eb06d5cfc36cf56b5b8d320e789e5357edd40212cbf4f83db72132 |
| `STAGE1B-PLAN-DRAFT-v5.md` | plan (R41-1, R41-5, §6, known limitations) | b3458932a57715ba8a373b62f43e42ab5e3051e4f46591782dbf1b6847e5b761 |
| `RUNNER-PROVENANCE-TEMPLATE-v5.md` | receipt template (R41-5) | 695f3c646f0881cce25da24e8b63d3c241b92bae476152d5273dc71cd3d92139 |
| `selftest-2026-10-14-r41-on-v4.log` | R41 controls on v4 (new) | 82c84fe4970728670148df8984f4cb744ab63112fa60f93adab3f959e29d418b |
| `selftest-2026-10-14-v5-rerun.log` | full suite on the delivered v5 bytes (new) | f29d2d8af6a52de9f286a3f9f4f301099480f52211227126a555e2c979f3b296 |
| `plan-dryrun-2026-10-14-v5.txt` | dry run, 3 phases (new) | 51320b5b56484f7199fb1a25bdaae96c3f362e7fe94a5223cb6d5fb8590f5728 |
| `run_stage1b-v5-devlog-snapshot.txt` | reconstructed driver tested by the dev log (new) | 449c1d630b474c2c55e8d5c6c6f1d36a14d6b50085ae4f3f7e43346a67f5edb6 |
| `selftest-2026-10-14-v5.log` | dev console capture (existing; not a valid receipt) | fa14a35581a93ab37e9e8172a6166e1340de3a1abcc9ee51e4a7503534c1feb5 |

Unchanged reviewed v4 files (R41:20-28): compare 5954f455…8fed, run 5308a0b2…0044, test 8f685b22…7869, plan
0340a7a8…f009, template f44f0a1d…e6f0, R38 report dc5d7e1d…656e, v4 log 98270fb6…47da (all re-hashed 2026-10-14).

## 2. Findings → fix → controls → result

| R41 item | Fix (v5 file:line) | Controls (test v5) | v4 (r41-on-v4 log) | v5 (rerun log, all OK) |
|---|---|---|---|---|
| **R41-1 (P2)** analysis identity not retained | Binding written once (mode "x") at the first `run main` admission: `run_stage1b-v5.py:897-911` (text, `bind_analysis`), set before admission `:1350-1355`; checked against the binding, not the manifest itself `:926-966`; every main log anchors the binding `:1196-1198`, checked `:914-923`; held hash compared after the phase `:962-963`; drift classification `:1340-1343`; `analysis_problems` `:968-978`; manifest must list `STAGE1B-ANALYSIS-AMENDMENT.md` with `inventory-reviewed-by:` / `inventory-coverage: complete` `:844-890`; main stat snapshot includes manifest, binding, bound files `:981-991,1065-1068` | R41-1-a…h (`test:2574-2659`; fake world `:2497-2571`) | 1 OK / 7 BAD (log :11-18) | OK |
| **R41-2 (P2)** outcome composition | `outcome_problems` `compare_equivalence_stage1b-v5.py:534-575` (protocol rq- IDs unique/disjoint/= arrived; booked = accepted = boarded = alighted; no cancellation; initial publications = accepted; semantic req- lists disjoint, protocol sizes, cover requestCount — sizes only across namespaces); collected in `facts` `:911-913,955-973`; passed `:1043-1045`; applied `:635-639` | R41-2-a…f, R41-2-cal (`test:2661-2702`) | 1 OK / 5 BAD (log :20-25) | OK |
| **R41-3 (P3)** boolean repeat fields | `_same_int` for runs[0].repeat, repeatCount, repeat: `compare…-v5.py:599-603` | R41-3-a, R41-3-b (`test:2703-2710`) | 2 BAD (log :27-28) | OK |
| **R41-4 (P3)** taskkill exempted by name | Exception bound to `C:/Windows/System32/taskkill.exe`: `run_stage1b-v5.py:193-199`; `tool_location_problems` `:701-710`, used by `tool_problems` `:697` and `write_freeze` `:1252` | R41-4-a…c (`test:2712-2741`) | 1 OK / 2 BAD (log :29-31) | OK |
| **R41-5 (P3)** plan/template identities | Plan v5 title/status `:1-11`, receipt `:119-131`, seal list = `SEALED_REQUIRED` exactly `:323-332` (`run_stage1b-v5.py:220-224`); template v5 header `:1-12`, harness identities `:75`, no push `:37` | R41-5-a…g (`test:2743-2789`) | 1 OK / 6 BAD (log :32-38) | OK |
| **§6** launch facts | Corrected arithmetic and start rule `plan:47-54`; A1 sources with SHA-256 `:40-45`; approval incl. no push `:33-36`; analysis timing aligned `:249-258` with `:310-321` | R41-5-e, R41-5-f | BAD (log :36-37) | OK |

R41 §2 wording note: `HARNESS-FIX-REPORT-R38.md:29` should read "format and enclosing receipt" for
`checkpointBindingHash`; the v5 comparator docstring and plan §3.1 / §14 say so.

**Calibration of the R41-2 rules (read only, before coding):** on A/ U-w08, U-w17, C-30-w08, C-30-w17 and
Mplus-30-d20181114-s10-r3-w07, protocol accepted / rejected / arrived = 80/28/108, 94/14/108, 81/27/108, 87/21/108,
47/61/108; semantic list sizes equal those counts; booked = boarded = alighted = accepted; no cancellation; initial
publications = accepted. Every adopted rule holds on all five. Not adopted: the verifier's `travelSnapshotVersion == 1`
(F/actual_fleetpy_medium_verify.py:1158) — it is 10, 10, 10, 10, 8 on these runs.

## 3. Fixed versus documented (convergence rule)

- **Fixed:** R41-1 and R41-2 (P2); R41-3, R41-4 and R41-5 (P3, cheap); the §6 arithmetic, A1 sources and timing
  alignment. Codex R43 confirms R41-2, R41-3, R41-4 and R41-5 FIXED and R41-1 PARTLY (R43 §2).
- **Documented, not fixed** (plan v5 §14 "Known limitations accepted before seal", `plan:334-360`, one-line reasons):
  middle-decision digests; `checkpointBindingHash` format only; forced-objective activation; size/mtime checks between
  full verifications; analysis binding before the first main log; analysis coverage is a manual review; taskkill OS
  dependencies; `SYSTEMROOT` resolver model; historical software equality; A1 / deadline facts.
- **Correction needed (R43-1):** the reason given for "analysis binding before the first main log" (`plan:348-350`:
  "no main output exists at that point") is **false**: `run_job` writes the log only after the child exits
  (`run_stage1b-v5.py:1192-1198`), so output can exist without an anchor. This is not a limitation to accept; it needs a
  v6 fix (an admission anchor written before launch).

## 4. Process notes

- Drafts were developed in my scratchpad and run through a shim that executes each draft with `__file__` set to its
  harness path; the harness copies are byte-identical to those drafts (hashes above).
- Codex's R41 probes were reproduced as tests: R41-1-a (consistent restamp during main), R41-2-a/b (empty lists with
  consistent receipts), R41-3-a/b (booleans; pair passed on v4), R41-4-a (E:/outside/taskkill.exe).

## 5. Self-test evidence

- `selftest-2026-10-14-r41-on-v4.log` (`--target v4`, harness test v5 against the reviewed v4 files; header :2-6 equals
  the R41 v4 hashes): **4 OK, 22 BAD, 0 LIMIT**, exit 1. The 4 OK are positives (R41-1-d, R41-2-f, R41-4-b, R41-5-c).
  Every BAD is an expected reproduction (missing fix in v4).
- `selftest-2026-10-14-v5-rerun.log` (`--target v5`, run from the harness folder on the delivered bytes; header hashes
  equal §1, log :2-8): **317 OK, 0 BAD, 2 LIMIT**, exit 0, 4773 s (log :337). Section E is log :308-336, all OK.
- `selftest-2026-10-14-v5.log` (existing dev capture, driver 449c1d63…): 317 OK, 0 BAD, 2 LIMIT. Superseded as a
  receipt by the rerun.
- The two LIMITs are the accepted N1 middle-decision digest and the R38-1 `checkpointBindingHash` cases.

## 6. Dry run

`plan-dryrun-2026-10-14-v5.txt` (`run_stage1b-v5.py plan gate0|smoke|main`, exit 0 each; 4 / 2 / 68 pending jobs; 35
distinct cells). Input problems are the expected missing prerequisites: runner manifest, seal, input freeze; for smoke
the Gate-0 pass verdict; for main also SMOKE-PASSED, SMOKE-INSPECTION and the analysis freeze. Tools: git →
`C:/Program Files/Git/mingw64/bin/git.exe` (Git Bash PATH order; inside the frozen Git tree), taskkill →
`C:/Windows/System32/taskkill.exe` (declared exception satisfied), both contexts agree. The agent shell shows
`GIT_EDITOR` set, which the driver refuses (the owner's fresh PowerShell is the intended launch shell, plan §11).
Checked afterwards: harness listing unchanged apart from the new file; `C:/RideBoundData/research/tier3-stage1b-v1`,
`runner-tier3-v3` and `harness/logs` absent; RideBound-deadline worktree index mtime unchanged (2026-09-29 13:57).

## 7. Owner facts recorded (plan v5 §0)

Deadline after 2026-11-03, exact date not given; A1 reached (chapter-4 part A v5, part B v10/v11; source files and
SHA-256 at `plan:40-45`); commit (worktree, no push) and runner-v3 publication approved once Codex rates the harness
READY. **Consequence:** with only "after 2026-11-03", a launch on 2026-10-15 or later has fewer than 21 days by the
conservative bound (2026-11-04 − 2026-10-15 = 20 days). Stage 1b cannot start unless the owner gives the exact deadline
(or a stronger bound) with deadline − launch ≥ 21 days.

## 8. Open before the seal

1. **R43-1 = R44-1 (P2):** write the main admission anchor (binding SHA) exclusively before each launch; refuse /
   persist loss for main output without an anchor; correct `plan:348-350`. Answered by round 6 (v6).
2. **R43-2 (retired by R44-3):** `SEALED_REQUIRED` of v5 names `selftest-2026-10-14-v5.log`, the dev capture; the v6
   driver names its own receipt generated on the final bytes.
3. **R43-3 = R44-2 (optional P3):** booking / boarding / alighting IDs are not checked for uniqueness before the set
   comparison; documented in plan v6 §14 (round 6 is scoped to R44-1).
4. Owner: exact deadline (§7); then commit, publish, provenance receipt, input freeze and seal at their authorized
   steps; analysis scripts, amendment record and analysis freeze before any main job.
