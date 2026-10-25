# Stage 1b plan (DRAFT v3, not sealed): cost-first selector × drop-cap mechanism study

Status: draft v3 written 2026-10-13 by Claude (harness fixer), superseding `STAGE1B-PLAN-DRAFT-v2.md` and
`STAGE1B-PLAN-DRAFT.md` (both kept unchanged) after Codex R28 (`optimize/codex-debate/codex-work/R28-HARNESS-RECHECK-CODEX.md`,
N1–N8) and the owner decision on N1 recorded in `harness/BRIEF-HARNESS-FIX-R28.md`. Changes are listed in
`HARNESS-FIX-REPORT-R28.md`. To be copied to `STAGE1B-PLAN.md`, completed at the `<FILL>` marks after the runner is
published, and sealed in `STAGE1B-PLAN.sha256` **before any Stage-1b job**, Gate 0 included. Nothing in this file has
been run.

**Internal protocol record.** The thesis does not need to mention this plan, its hypothesis labels, thresholds or seal.
If results are used, they are reported as a measured mechanism comparison on the stated panel with its limits (§9).
Predictions are kept here only so that the analysis cannot be tuned after the fact (as in
`tang3/t38c120/C120-RESULTS.md`: "Internal record only; the thesis does not discuss predictions").

Anchors (all under `E:/Code/Report_INT3508/ridebound-scratchpad/`):
`R17` = `optimize/codex-debate/claude-work/R17-FINAL-VERDICT.md`; `R18` = `optimize/codex-debate/codex-work/R18-CONFIRM-CODEX.md`;
`R22` = `optimize/codex-debate/codex-work/R22-CODE-REVIEW-CODEX.md`; `R24` = `optimize/codex-debate/codex-work/R24-HARNESS-REVIEW-CODEX.md`;
`R28` = `optimize/codex-debate/codex-work/R28-HARNESS-RECHECK-CODEX.md`; `IR` = `tang3/stage1b/IMPLEMENTATION-REPORT.md`;
`C120R` = `tang3/t38c120/C120-RESULTS.md`; `R19C` = `optimize/codex-debate/codex-work/R19-CODEX.md`.

## 0. Authority, start rule and what was seen before

- Owner approved Stage 1b as an optional study (coordinator brief, 2026-10-11). The cost-first option is implemented,
  uncommitted, in `C:/Code/RideBound-costfirst` (ADR-078 Proposed; IR §3.2), reviewed by Codex R22: ACCEPT WITH CHANGES,
  no production edit requested (R22:3,252).
- Owner decision 2026-10-12 (recorded in `harness/BRIEF-HARNESS-FIX-R28.md:9-25`): Gate 0 is **behavioural-only and
  explicitly disclosed** (§3); no .NET replay tool will be written.
- Commit and publish need a separate owner approval (IR:262-267). The runner is not published at drafting time.
- **Start rule (R17:281-288; R18:222).** The optional study starts only after the A1 milestone, with the deadline
  confirmed. At least 3 weeks left: Stage 1 (1a then 1b). 1–3 weeks left: Stage 1a only (34 jobs, no code). Less than
  1 week: nothing. Recorded facts, to be filled from the owner's records before the seal (the harness builder has
  **not** established them; R24:138 and R28:199 note they are missing, not that the condition failed):
  - A1 milestone reached: `<FILL: date and where it is recorded>`
  - Confirmed deadline: `<FILL: date and who confirmed it>`
  - Time left when Stage 1b starts: `<FILL: weeks; must be at least 3 weeks>`, or the reference of an owner-authorized
    amendment of this rule: `<FILL or "none">`
  If the rule is not met and no amendment is recorded, Stage 1b does not start.
- Previewed before this plan: every archived SF arm on the 34 test cells (T3.8), Stage 1a SF-C120 (C120R), fixed-state
  selector replays (R18:313-317). No cost-first (CF) trajectory exists on any cell. This is an exploratory study with
  sealed predictions.

## 1. Design (R17:217-229 as corrected by R18:222-232)

| Phase | Arm | WP4 | Commitment config | Cells | Jobs |
|---|---|---|---|---|---:|
| Gate 0 (behavioural-only, §3) | OFF-U, OFF-C-30 (new runner, option OFF) | `tang3/configs/wp4-tier3-v2.json` **unchanged, key absent** (file 66e9b592…4518, canonical fb2813c4…cd42) | `U.json` (998a0a94…c273), `C-30.json` (d6124f3f…8beb) | `d20181116-s10-r1-w08`, `d20181116-s10-r1-w17` (R22:160-167) | 4 |
| ON smoke | CF-U, CF-C-30 | `harness/wp4-tier3-v2-costfirst.json` = v2 + exactly `"selectionObjectiveOrder": "cost-first"` (file 37040657…00ae, canonical 16dcb7e3…3140) | same two files | `d20181114-s10-r1-w08` (a calibration cell; see §1.2) | 2 |
| Main | CF-U, CF-C-30 | as smoke | same two files | the 34 test cells of `tang3/t38/split-t38-v1.json` "test" (b72ac0f7…03f1) | 68 |
| **Stage 1b total** | | | | **35 distinct cells** (both Gate-0 cells are also test cells; R24:164) | **74** |

Matched SF comparators are the archived T3.8 runs `C:/RideBoundData/research/tier3-t38-v1/{U,C-30}-<cell>` (SF-U, SF-C30),
run with runner-tier3-v2; they retain their recorded configuration, fixture and transcript identities, while equality of
their external execution software with Stage 1b's is unverified (§1.1). Stage 1a (SF-C120, 34 jobs) is complete
(C120R); Stage 1 = 108 jobs (R18:215).

**CF definition.** C1 hard-vector policy with ranking forced-reference-count (when active) → accepted-request-count →
operational-cost → candidate-id ranks; the worst-utilization level and the 10 revision levels are skipped. Hard
assessment, gates, locks, recovery (b) `no-worse-than-reference-v1`, generation/work/retention budgets, booking
confirmation trigger and late boarding `record-v1` are unchanged (R17:225; R18:225; IR:58-68; R22:68-96). Results read as
"cost-first ranking over the same retained portfolio" (IR:293-294; R22:94).

**Run identity.** Each job is identified by `initializeRun.policyConfigurationHash`: OFF-U `251b331e…c346`, OFF-C30
`a2f11a3a…5f1c` (archived, R22:126-130); CF-U `90b290d7…2df5`, CF-C30 `e6b33be5…6f76` (derived by Codex R22:236-238 and
independently reproduced by `harness/make_wp4_costfirst.log`). Objective diagnostics are supporting evidence only (R22:242).

### 1.1 Common settings and the freeze limitation (R28 §5)
World wrapper v1.2 (`tang3/world/preflight_world_v1_2.py`, df28b6cb…a262), measured day/window worlds
`tang3/calib/panelA/worlds/W5-<day>-<window>.json`, fixtures `C:/RideBoundData/research/fixtures-panelA-v1`, drivers
`tang3/calib/panelA/drivers`, master seed 7, one repeat, cwd `E:/Code/RideBound-deadline` (HEAD 1ce15e9, inventory
0da63eaf…), adapter and FleetPy 1.0.2 as recorded for T3.8, 6 workers. Every input file is frozen in
`stage1b-input-freeze.sha256` (v3 format, `run_stage1b-v3.py freeze`), which also freezes the execution software and
resolver scope: the FleetPy tree `E:/RideBoundData/wp7/FleetPy-1.0.2` (without `.git/`), the job interpreter's
environment `E:/RideBoundData/wp7/envs/fleetpy-1.0.2`, `dotnet.exe`, `host/fxr` and every installed
`Microsoft.NETCore.App`, the Git installation `C:/Program Files/Git` and the `git` executable that `PATH` resolves to,
the steering environment (`PATH`, `PATHEXT`, `SETUPTOOLS_USE_DISTUTILS`, `PYTHON*`, `DOTNET_*`, `COREHOST_*`, `COMPLUS_*`,
`OMP_*`, `MKL_*`, `OPENBLAS_*`, `RB_*`, `GIT_*`), the absence of the interpreter's user site-packages and of
`PYTHONPATH`/`PYTHONHOME`/`PYTHONSTARTUP`/`PYTHONUSERBASE`, and an audit that no `.pth` file of the interpreter adds a
path outside the frozen trees. The freeze is verified in full before every phase, after every phase (main included) and
by the comparator before any verdict. Between those full checks the driver compares, before each job, the size and
modification time of every frozen file and tree file and looks for added tree files: this detects ordinary edits, not a
same-size, same-modification-time rewrite, and it does not authenticate the bytes a running job read. Observed drift
writes `INPUT-DRIFT-STOP.txt`, a durable stop that refuses every later run and verdict until the owner reviews it.

Stage 1b uses the execution software, resolved tools and steering environment recorded in the input freeze. They are fixed from freeze time and shared by all new OFF, smoke and main jobs. The archived SF-U/SF-C30 controls retain their recorded configuration, fixture and transcript identities. No archived byte-level baseline was located for their external FleetPy/Python/.NET execution environment, so historical software equality is unverified. Gate 0 requires valid evidence and exact behavioral reproduction on the four specified arm-cell pairs; a pass establishes compatibility on those pairs, not equivalence on every main-panel cell or absence of runtime confounding. CF-versus-archived-SF results are reported as exploratory model-experiment contrasts with this provenance limitation, alongside service composition, guards and cell/day patterns.

(Adopted from R28:176. Under the amended behavioural-only Gate 0 of §3, "valid evidence" means the recomputable digests
and the per-run link and content checks; the digests of decisions 2..N are not validated by the production .NET code.
This clarification does not waive known input, configuration or hash mismatches and does not add jobs, R28:178.)

### 1.2 Choices made by the harness builder (owner may change before the seal)
- Smoke cell `d20181114-s10-r1-w08`: a calibration cell (forced calibration group of the split), already used as a smoke
  cell in T3.5/T3.6 (`tang3/t38/T38-PLAN.md` §0). Using it previews no test cell and duplicates no main job.
- Gate-0 job labels are the archived labels `<arm>-<cell>-t38`, so `runId` equals the archived value (precedent:
  `optimize/run_forecast.py:93`). CF jobs use `CF-<arm>-<cell>-s1b`.
- `main` also requires `SMOKE-PASSED.txt` and the recorded smoke inspection `SMOKE-INSPECTION.txt` (§4).
- Only one driver instance runs at a time (an operating-system lock on `harness/run-instance.lock`).

## 2. Runner and provenance (R22 F3, §5.1, §7.2)

- Runner `C:/RideBoundData/research/runner-tier3-v3`, published from the owner-approved commit of
  `research/tier3-cost-first`: commit `<FILL>`, base `761915c`, reviewed diff `c48042af…d690`; tree `<FILL>` (`<FILL>`
  files), `RideBound.Runner.dll` `<FILL>`; manifest `harness/runner-tier3-v3.sha256.tsv` (`<FILL>` SHA-256); receipt
  `harness/RUNNER-PROVENANCE-v3.md` (from `RUNNER-PROVENANCE-TEMPLATE-v3.md`; every field filled — the driver refuses a
  receipt that still contains a template field; the recorded facts still need manual review, R28:207).
- The harness commit (`1ce15e9`, = `coreCommitSha` in every initializeRun) and inventory (`0da63eaf…`) are separate
  facts from the runner source. The transcripts' coreCommitSha is never presented as the runner source (R22:58).
- All 74 jobs use this one tree. `run_stage1b-v3.py` re-verifies it byte for byte before each phase; the Gate-0 and smoke
  verdict files record its DLL and tree hash and are refused for any other build.

## 3. Gate 0 (G0) — behavioural-only, amended (owner decision 2026-10-12; R28 N1)

**Amendment.** This criterion is amended from R22's full-validity criterion (R22:173,190,200-208; retained in R24). The
middle-decision digests (the `decisionHash` of decisions 2..N and the intermediate state hashes) are not validated by
the production .NET code: they hash the Runner's pre-batch online state (`RunnerSession.cs:768-799`), which a
transcript carries only at the final checkpoint, and no .NET replay validator will be built. As Codex states, exact
behaviour "is strong behavioral evidence, but a protocol-invalid tape can have identical actions, ledgers and normalized
frame content" (R28:56); a protocol-invalid tape with identical actions, ledgers and frame content could therefore pass.
A pass is therefore named `GATE0-BEHAVIOURAL-PASSED.txt` (failure `GATE0-BEHAVIOURAL-FAILED.txt`), every Gate-0 report
carries this disclosure, and all Stage-1b results are reported as exploratory.

Four pairs, each new OFF job against its archived reference (never U against C30):

| Reference | Frames | Transcript SHA-256 |
|---|---:|---|
| `U-d20181116-s10-r1-w08` | 2020 | c82d2796…1740 |
| `U-d20181116-s10-r1-w17` | 2284 | 6ec5242c…f2e1 |
| `C-30-d20181116-s10-r1-w08` | 2035 | 42ba5f1a…72b |
| `C-30-d20181116-s10-r1-w17` | 2146 | 371565dd…bbc |

Criterion (implemented by `harness/compare_equivalence_stage1b-v3.py`; its controls pass in
`harness/selftest-2026-10-13-v3.log`):
1. Preconditions per run: status pass / succeeded; bundle files match `bundle-manifest.json`; every transcript record and
   frame parsed strictly (a duplicate JSON field anywhere, a non-integer number or a non-canonical encoding fails;
   R28 N2); every frame SHA-256, length, direction and contiguous ordinal from 1; final Runner checkpoint; decision
   chain, ACK links, state continuity and certificate publication links valid; `audit_vep` cross-checks pass (R22:173).
   Recomputable digests: `initialized.manifestHash`, the initial state hash, the decision-1 `decisionHash`, every
   checkpoint's `checkpointHash` and the final checkpoint's `stateHash` are recomputed with the Runner's formulas and
   must match; the final checkpoint must close the transcript (epoch, event sequence, time). Decisions 2..N: link and
   content checks only (amendment above).
2. Initialization: `policyConfigurationHash` equals the archived arm binding recomputed from the actual config bytes;
   scenario/graph/travel-time snapshot hashes, coreCommitSha (1ce15e9), versions, seed, units and capabilities equal;
   `binarySha256` is the **only** permitted difference, after the old value verifies against runner-tier3-v2
   (a631038b…, tree ac251ef3…) and the new value against runner-tier3-v3; all four new values equal (R22:183-196).
   Explicit `stability-first` in the OFF WP4 is a failure (R22:50,232).
3. Exactly equal after the narrow normalization of R22:188-194: ordered `fleet_decisions`; every signed v/e/p row;
   served set, lifecycle, boarding and alight times; every decoded frame in order; final online state, commitment
   ledger and incident ledger; run-00 semantic block (R22:200-206). No epsilon, no prefix pass (R22:208).
4. Any failing, missing or malformed pair, and any frozen input that cannot be validated after the jobs started:
   `GATE0-BEHAVIOURAL-FAILED.txt` (written once, before the report, recording the frozen expected identities when live
   ones are unreadable), report the first difference, **stop Stage 1b**: every later `run` and every comparator verdict
   is refused while a `*-FAILED.txt` or `INPUT-DRIFT-STOP.txt` exists. No re-run of controls without owner approval
   (R17:259; R18:230).
5. Time box: Gate 0 must pass within 5 working days of starting 1b, and the Stage-1 analysis must finish within 10
   working days; otherwise drop the rest and report it as future work (R17:285-286).

## 4. ON smoke (R22 F4; R24 F4; R28 N6)

Both smokes must: end `status: pass` with a valid audit and the recomputable digests of §3.1 (decisions 2..N: links and
content only); carry the CF binding of their arm, the verified runner-v3 DLL and coreCommitSha 1ce15e9; and in **every**
decision carry the execution evidence that the ON configuration emits (`"emitSolverExecutionEvidence": true`): solver
status `completed`, evidence version 1.1.0, complete generation and selection evidence, omitted-candidate and
generation-work accounting that add up, counters within the frozen ON budgets, no saturated omission count, a validated
publishing path (`validatedIncumbent`, or a `safeNoOp` / `greedySingleRequest` fallback with its detail code),
consistent primary/final statuses. Objective bounds: an **optimal** list is complete — forced-reference-count when a
forced certificate witness shows the level was active, accepted-request-count, operational-cost, and one candidate-id
rank per vehicle of the generation evidence in ordinal vehicle order; prefixes only for unproven (feasible) solves;
absence only with a supported reason (no-incumbent primary, fallback final); every bound a canonical non-negative
integer with the objective's sense direction, exact gap arithmetic and proof flag. No `worst-hard-utilization-ppm`, no
`revision:*` level. For CF-C30 no rider has drop Σ|c| > 30 s. The smoke report prints solver statuses, execution paths,
omission and work sums, and per level the incumbent and best-bound sums, for inspection
(`compare_equivalence_stage1b-v3.py smoke`). Failure: integration defect, stop.

Before `main`, the person who read the smoke report records the integration inspection in `harness/SMOKE-INSPECTION.txt`
(lines `smokeVerdictSha256 <SHA-256 of SMOKE-PASSED.txt>`, `inspected-by: <names>`, `inspection: accepted`); the driver
refuses `main` without it.

## 5. Primary endpoint (R18:236-247; replaces the "DiD" wording of R17:263)

Definitions (as `optimize/codex-debate/claude-work/r21-stage1a/report_stage1a.py:4-9,63-69`): pooled over the 34 test
cells, 3,672 requests per arm; served = lifecycle `completed` with a passengerAlighted event; A = alight time
(`tang3/sweep/sweep_analyze.py:40-57`); p0 = first drop promise; naive late60 L = #served with A − p0 > 60 s;
timely60 T = served − L.

    I_L = (L_CF,U − L_CF,C30) − (L_SF,U − L_SF,C30) = (L_CF,U − L_CF,C30) − (203 − 76) = (L_CF,U − L_CF,C30) − 127
    I_L = I_served − I_timely60,  I_served = (S_CF,U − S_CF,C30) − (2434 − 2338),  I_timely60 = (T_CF,U − T_CF,C30) − (2231 − 2262)

(SF values: R18 rows 26-28, `R18:82-84`; identity: R18:243-245.) L counts late **completed** rider-runs; rejected
requests are absent and served sets can change, so I_L is reported **with** I_served and I_timely60, the full 2×2 of
timely / late / rejected counts, served-set overlap diagnostics, and per-cell and per-day paired signs (R18:247).
Common-served strata are post-treatment populations, not randomized estimands (R18:247). Five days are not 34
independent replicates; uncertainty with 5 day clusters is fragile, report the observed paired pattern (R18:251).

## 6. Sealed predictions (R17:259-266, interpretations corrected by R18:197-203)

Reference numbers: SF-U / SF-C30 served 2,434 / 2,338; naive late60 203 / 76; forecast (causal-no15) late60 137 / 10;
forecast timely60 per request 2,297 / 2,328; drop-exposed riders 174 / 33; shared completions 555 / 313
(R18 rows 26-28, 33, 37; C120R:15-25).

| ID | Prediction | If it fails (R18 wording) |
|---|---|---|
| G0 | §3 (behavioural-only, amended) passes on all 4 pairs | Stop (R17:259). |
| H1 | CF-C30: every rider drop Σ|c| ≤ 30 s; 0 overshoots; every forced / no-worse record explained (R17:260) | Implementation defect, stop. |
| H2 | CF-U drop-exposed riders ≥ 261 (1.5 × 174) **and** naive late60 ≥ 243 (203 + 40) (R17:261) | The chosen materiality prediction failed. It does **not** show selector invariance, absence of a confounder, or small effects on every metric (R18:199). No Stage 2. |
| H3 | CF-C30 naive late60 ≤ 4.25 % of served **and** forecast late60 ≤ 1.0 % of served (SF-C30: 3.25 %, 0.43 %) (R17:262) | Passing shows those fractions were retained under the changed selector; it does **not** prove "the cap alone suffices" or physical-arrival guarantees (R18:200). Failing means the changed policy missed a predeclared aggregate reliability threshold; report the service composition and trajectory evidence before attributing the difference to the selector. (R19C:178; R24:140) |
| **H4 (primary)** | I_L ≥ +40 pooled, and per-cell interaction > 0 in ≥ 15 of the 25 w08/w17 test cells (SF: U > C30 late60 in 25/25 of them, R18 row 36) (R17:263) | I_L < 40 is **not** evidence of objective independence; a negative I_L can itself be an interaction; a small nonzero interaction still misses +40. Report the estimate, per-cell/day contrasts and uncertainty, and name the missed target (R18:201). |
| H5 (guard) | Forecast timely60 per request CF-C30 − CF-U ≥ +31 (SF: 2,328 − 2,297) (R17:264) | Reported next to H4; not a second primary. |
| H6 (key secondary) | CF-C30 shared completions ≥ **345** = ceil(1.10 × 313), with CF-C30 served ≥ **2,315** = ceil(0.99 × 2,338) (R17:265; exact 313 from C120R:25; guard R19C:179) | A smaller gain is not "no pooling gain"; report the value (R18:202). The 99 % service guard stays even if H6 fails (R18:249). |
| H7 (Stage-2 gate) | On CF-U trajectories ≥ 10 completed riders cumulative-only at B = 120 s (every nonzero drop move ≤ 120 s, sum > 120 s) (R17:266) | < 10: do not build the per-step arm. This is a resource-allocation stop rule, not a bound on witnesses or proof that paths cannot diverge (R18:203). |

## 7. Guards for every arm (R17:270-278; R18:249)

Report: served; timely60 / timely120 per request (naive and frozen causal-no15 forecast, `optimize/display_layer.py`, no
retuning); late60 / late120 among served; early60; mean quote extension; median initial promised ride time; absolute
shared completions; exposed and repeated riders; budget prunings; cumulative-only records and decisions; forced and
no-worse counts; per-cell and per-day paired signs. An arrival gain with a median initial promise more than 30 s longer
is not claimed (R17:275). No forecast retuning; padded U rows are descriptive references only; no new B or load values
searched after a null and called confirmation (R17:276-278).

## 8. Analysis, review and amendments

- Analysis code: adapted copies of `optimize/codex-debate/claude-work/r21-stage1a/{extract,report}_stage1a.py` with the
  CF arms, plus the I_L / I_served / I_timely60 identity check; frozen (SHA-256 written as an amendment) **before** the
  first main output is opened. Codex recomputes independently, as for Stage 1a. These scripts do not exist yet (R28:46).
- Amendments go to `STAGE1B-AMENDMENTS.md`, written after the seal, never editing the sealed plan.
- Every outcome is reported, including failed predictions; no re-run or retuning after a result. All results are
  reported as exploratory (§3 amendment, §1.1 limitation).
- Time box: Stage-1 analysis finished within 10 working days (R17:285-286); otherwise drop the rest and report it as
  future work.

## 9. Stage 2 conditions (R17:227-229,287-288; R18:226-232)

Stage 2 (CF-C120 34 + CF-per-step-120 34 + 2 ON smokes + 2 OFF re-checks on the second build = 72 jobs) only if H2 **and**
H7 pass and at least 2 weeks remain after Stage 1. All Stage-2 arms use the same second published binary; only the
budget mode changes for the direct cumulative/per-step comparison, and manifests are compared explicitly when stages
are combined (R18:228). The two frozen OFF rechecks on that second build exercise the changed 120-s path against
available controls (R18:226). Per-step semantics must be aligned across assessor, validator, certificates and breach
records; arms, cells and reference artifacts frozen before building; additional validation jobs, if needed, are
disclosed before approval (R18:226). Without Stage 2 no empirical cumulative-versus-per-step outcome claim may be made
(R18:232). No Stage-2 job is authorized by this plan.

What may be said afterwards (R17:299-309): on 34 previewed cells over 5 days, one simulator and city-wide traffic
factors, whether the 30-s cap kept its reliability when the selector stopped ranking stability (H3/H4), whether CF under
the cap changed pooling (H6). Not: generality, realised cost or profit savings (operational cost is a repeated plan
cost), "C-120 is optimal", "accumulation matters" from witnesses, independence of the 34 cells, protocol validity of
the middle decisions (§3).

## 10. Execution order and safety

`run_stage1b-v3.py` phases `gate0` → (`compare_equivalence_stage1b-v3.py gate0 gate0-compare-<tag>.txt` →
`GATE0-BEHAVIOURAL-PASSED.txt`) → `smoke` → (`… smoke smoke-check-<tag>.txt` → `SMOKE-PASSED.txt`; then
`SMOKE-INSPECTION.txt`) → `main`. AC power only (the battery override is refused; AC is rechecked inside the admission,
after the queue and the stat checks and immediately before each launch), STOP checked during the power wait and at
admission, keep-awake, outputs never reused, logs mode "x", one driver instance at a time, admission right before each
launch (STOP, failure latches, input-drift stop, frozen-input stat drift, disk, AC), C: disk floor 51,200 MiB with 410
MiB reserved per running job, full freeze verification before and after every phase. A `*-FAILED.txt` or
`INPUT-DRIFT-STOP.txt` refuses every later start and verdict; a phase with its own pass verdict is closed. Outputs
`C:/RideBoundData/research/tier3-stage1b-v1/<phase>/<job>`. Estimated compute: historical ~15 min/job at 6 parallel
(R17:232; archived SF-U w08 run 880 s wall); CF runtime on T3.8 unconfirmed (R17:233-234). Disk: archived run folders are
82–96 MB, so 74 jobs ≈ 6.0–7.1 GB (an estimate, R24:128).

Sealed files (`run_stage1b-v3.py` SEALED_REQUIRED): this plan, `run_stage1b-v3.py`, `compare_equivalence_stage1b-v3.py`,
`test_compare_equivalence_stage1b-v3.py`, `selftest-2026-10-13-v3.log`, `canonical_wp4.py`, `make_wp4_costfirst.py`,
`make_wp4_costfirst.log`, `wp4-tier3-v2-costfirst.json`, `runner-tier3-v3.sha256.tsv`, `RUNNER-PROVENANCE-v3.md`,
`stage1b-input-freeze.sha256`, and the five external-tree manifests `stage1b-external-{fleetpy,python-env,
dotnet-netcore-app,dotnet-host-fxr,git}.sha256.tsv`.
