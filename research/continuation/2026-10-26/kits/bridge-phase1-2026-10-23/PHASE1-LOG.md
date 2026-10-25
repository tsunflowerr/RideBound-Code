# Bridge phase 1 — log

Owner approval: 2026-10-23, "Đồng ý" to run the company wf15 emulator taken from the intake and to start phase 1
(plan: `C:/RideBoundContinuation/2026-10-23/PHAN-TICH-GOI-CONG-TY-23-10.md` §6). Everything here is exploratory
implementation-fidelity work; it says nothing about which rule is better.

## P1 — 2026-10-23 ~22:30 — emulator copy, step 1.0, predictions, driver

**wf15 emulator copy.** `C:/RideBoundContinuation/2026-10-23/emulator-wf15/`:
- the intake's `sp/wf15/runner/*.js`, with `lib.js` replaced by the home copy used for the bridge (path constants only);
- `engine.js` sha256 `908a2961…`, equal to the company's frozen `codeSha256.engine`
  (`sp/wf15/frozen/shadow-params.json:54`);
- every other `.js` is byte-identical to the intake;
- manifest in `MANIFEST.sha256`.

Against the bridge emulator `emulator-wf5-eoc`, only `engine.js` and `lib.js` differ. wf15 adds four files:
`lambda.js`, `netfull.js`, `shadow.js`, `sscore.js`.

**Step 1.0: wf15 against the sealed bridge predictions.** `emulate_wf15_equiv.js` re-created the three sealed files with
the wf15 engine, using the same layout, row order and roles. All four outputs are byte-identical to the sealed files:

| File | Rows | sha256 |
|---|---|---|
| `emulator-riders-2000.tsv` (U, C-120, C-300, PS-300, DLHZ-120) | 4,972 | `06742da6…` |
| `emulator-riders-elmi-2000.tsv` (RCE35, EOHZ35) | 1,966 | `e42c7e2d…` |
| `emulator-riders-g2-2000.tsv` | 2,994 | `447b6e47…` |
| `emulator-riders-g2-2000-counts.tsv` | — | `1b08534f…` |

The 10 bridged arms therefore hold for the engine that produced the company's 2×3 table.

**Configs** (`make_configs_p1.py`; each derived by exact text edits that must match once; hashes in
`configs-p1.sha256.txt`):

| New config | Derived from | Change |
|---|---|---|
| `EO35` | `EOHZ35` | `dropEtaDeadlineRelaxed` removed |
| `DLS-120` | `DLHZ-120` | `dropEtaDeadlineRelaxed` removed |
| `G2-EO35` | `G2-EOHZ35` | `dropEtaDeadlineRelaxed` removed |
| `G2-DLS-120` | `G2-EO35` | fixed 120 s slack |
| `wp4-costfirst-forced` | `wp4-tier3-v2-costfirst` | `commitmentRecovery` `forced-reference-v1` |
| `wp4-g2-costfirst-forced` | `wp4-g2-costfirst` | `commitmentRecovery` `forced-reference-v1` |
| `wp4-g2-costfirst-k3` | `wp4-g2-costfirst` | `maximumTargetVehicles` 3 |

Why `forced-reference-v1`:
- it keeps a forced vehicle on its kept route (`Wp4RunnerConfiguration.cs:52-59`);
- this is the emulator's forced vehicle: only the NOOP plan (`engine.js:576`), and excluded from G2 (`:593`, `:598`);
- C# G2 also prunes forced donors and receivers (`HardVectorCandidateAssessor.cs:519-530`);
- the Runner's G2 checks do not restrict the recovery mode (`Wp4RunnerConfiguration.cs:408-437`);
- `no-worse-than-reference-v1`, used by every earlier bridge arm, would turn a strict deadline into the relaxed one
  (ADR-080).

**Predictions** (`emulate_p1.js`, wf15 engine). Totals over the 11 cells:

| Role | Engine options | Relocations | Forced-vehicle decisions |
|---|---|---|---|
| EO35 | `{EO, alpha 0.35}` | — | 110 |
| DLS-120 | `{DLS, B 120 s}` | — | 35 |
| G2-EO35 | G2 (every vehicle a target) + `{EO, alpha 0.35}` | 61 | 534 |
| G2-DLS-120 | G2 (every vehicle a target) + `{DLS, B 120 s}` | 64 | 203 |
| G2K3-U | `g2Targets` 3 (< 7 other vehicles) + U | 63 | — |
| G2K3-RCE35 | `g2Targets` 3 + RCE35 | 19 | — |
| G2K3-EOHZ35 | `g2Targets` 3 + EOHZ35 | 53 | — |

For comparison, unlimited G2 gives 54 / 26 / 62 relocations for U / RCE35 / EOHZ35.

**Discrimination** (`discrimination.txt`): the new arms are not copies of the bridged ones.

| New arm vs bridged arm | Riders that differ | Cells |
|---|---|---|
| EO35 vs EOHZ35 | 207 | 5/11 |
| DLS-120 vs DLHZ-120 | 91 | 3/11 |
| G2-EO35 vs G2-EOHZ35 | 228 | 6/11 |
| G2-DLS-120 vs G2-EOHZ35 | 789 | 11/11 |
| G2K3-U vs G2-U | 166 | 3/11 |
| G2K3-RCE35 vs G2-RCE35 | 93 | 4/11 |
| G2K3-EOHZ35 vs G2-EOHZ35 | 98 | 5/11 |

**Sealed** in `EMULATOR-PREDICTIONS-P1.sha256` at 2026-10-23T15:25:15Z. At that time `via-bridge-man-v1/p1` did not
exist: 0 phase-1 jobs.

**Move counter.** `moves_plan.py` counts moves by plan membership, i.e. the vehicle whose route holds a rider's pickup.

Self-check on the 33 v4 G2 jobs: 33/33 equal to the sealed emulator counts, 142 moves, at most 1 per decision. This
reproduces AMENDMENTS A9. Before this, A9's plan-membership count was an ad hoc computation with no saved script; that
gap is now closed.

**Driver** `run_p1.py`:
- the rails of `run_g2.py` v4;
- `runner-g2-v3` and the G2 worktree adapter through `bridge_launcher_g2.py`;
- the adapter inventory `36d70c9c…` (`P1-INVENTORY.txt`) at HEAD `937bb31` (clean).

`inventory.py` copies the preflight's function. Self-check: `E:/Code/RideBound-deadline` gives `0da63eaf…`.

`run_p1.py check`: all inputs pass. AC power; RAM available 3.9 GiB, so 2 workers.

**Comparison** `compare_p1.py`:
- the per-rider rule of `bridge_compare.py`, with the same `bridge_riders` extraction;
- plus the G2 moves against the emulator relocations.

## P2 — 2026-10-23 ~22:50 — pilot passed; step 1.3 needs a C# change; step 1.4 hits the contract bound

**Pilot** (p1pilot, 7 jobs on d20181114-s10-r1-w08, 22:31–22:43, all status-pass, about 3.3 min each):
- compare_p1.py: 7/7 cell-arms exact, 653/653 riders identical;
- G2 moves equal to the emulator 5/5, at most 1 per decision;
- evidence 1.3.0 on every decision of the 5 G2 jobs, 0 verifier errors.

The cell exercises the new paths. Emulator forced-vehicle decisions: EO35 24, DLS-120 12, G2-EO35 102, G2-DLS-120 101.
k3 relocations: 6 / 2 / 3. `P1-PILOT-REVIEWED.txt` written; p1main (70 jobs, 3 workers) started about 22:45.

**Step 1.3 (dwell 24 s) is not a configuration change.** RideBound C# creates every inserted pickup and drop stop with
`new Duration(0)` (`InsertionCandidateGenerator.cs:801-812`). The Runner therefore always plans 0 s service at its own
stops. Setting 24 s in FleetPy (`op_const_boarding_time`, preflight:241) and in the emulator (`cfg.dwell`) would make the
Runner's projections wrong by construction.

A real test needs an opt-in C# option for the service duration of inserted stops, with tests and a new Runner. That is
AI-written core code, which the audit says the student must review. **Not started. Waiting for the owner.**

**Step 1.4 (larger cells): contract bound.**
- The first grid (216 requests, 16 vehicles, 192 nodes; `grid-bridge-big216.json`) was refused:
  `normalizer.configuration-invalid` (`normalize-big216.log`).
- The normalizer contract v1 allows `RequestTarget <= 128`, `VehicleCount <= 32` and `MaximumNodeCount <= 96`
  (`FleetPyManhattanNormalizer.cs:322-328`). The adapter's own limits are 5,000 vehicles and 100,000 requests
  (`session.py:494`).
- Per the company plan 2.3, I record the limit and run the largest size the contract allows. No core change.
- `grid-bridge-r128.json`: 128 requests, 10 vehicles, 96 nodes; 12.8 requests per vehicle against 108/8 = 13.5.
- 10 of 11 cells normalize. d20181118-s10-r1-w08 is refused: `source.node-cap-prevents-target` (96 nodes cannot carry
  128 eligible requests). d20181118-w17 was normalized alone (`grid-bridge-r128-last.json`), because the tool stops at
  the first failure.
- Fixtures: `C:/RideBoundData/research/fixtures-via-bridge-r128-v1` (70 files).
  Drivers: `drivers-r128/`. Both by `prepare_r128.py`, with manifests.
- The emulator (wf15) loads the cells as 128 requests and 10 vehicles.
- Arms: R128-U and R128-RCE35 (bridge configs and WP4); R128-G2K8-U and R128-G2K8-RCE35 (bridge G2 configs, WP4
  `maximumTargetVehicles` 8, the company setting).
- **Discrimination, faithfully:** with 10 vehicles the cap of 8 changes **0** riders against unlimited G2 (all 10
  cells; `discrimination-r128.txt`). The ninth vehicle is never the chosen receiver. The k-nearest rule is tested by
  step 1.2 (k = 3); step 1.4 tests fidelity at 128 requests only.
- Predictions sealed in `EMULATOR-PREDICTIONS-R128.sha256` at 2026-10-23T15:49:52Z, with 0 step-1.4 jobs.
- Driver `run_r128.py` reuses `run_p1.py` and its instance lock, so it cannot run beside p1main.
- Comparison `compare_r128.py` is compare_p1.py with 6 exact edits: constants and the G2 role test.

## P3 — 2026-10-23 ~23:55 — p1main: 69/70 pass; one stop at a donor's locked service leg (not fixed; owner decides)

**p1main** (70 jobs, 22:45–23:46, 3 workers): 69 status-pass, 1 stopped. compare_p1.py over p1pilot + p1main
(`compare/p1all-2026-10-23-compare.json`):
- **76/76 completed cell-arms exact, 6,806/6,806 riders identical;**
- G2 moves equal to the emulator 54/54, at most 1 per decision.

| Arm | Exact |
|---|---|
| EO35 | 11/11 |
| DLS-120 | 11/11 |
| G2-EO35 | 11/11 |
| G2-DLS-120 | 11/11 |
| G2K3-U | 10/10 + 1 stopped |
| G2K3-RCE35 | 11/11 |
| G2K3-EOHZ35 | 11/11 |

**The stopped job:** `p1main/G2K3-U-d20181112-s10-r1-w08`. It failed at 4,191.283 s with
`RBWP7_LOCKED_LEG_MISMATCH at $.vehicles[3].activeLeg`. The output (`failure-00.json`, transcript) and the log are kept.

- **RideBound's decision equals the emulator's.**
  - The emulator trace (`trace_p1.js d20181112-s10-r1-w08 U 0 3`, diagnostic) moves rq-3713b75 from veh-003 to veh-007
    at 4,191,283 ms.
  - The transcript shows the same move in the last decision.
  - The 4 moves the job made (1,651,000; 1,767,000; 3,517,000; 4,191,283 ms) are the emulator's first 4 moves: same
    riders, same donors, same receivers (vehicle index = `identity.vehicleId(i)`).
- **The cause is the donor-side twin of A7.**
  - Vehicle 3 (veh-67cc…) is the G2 **donor**. At the decision instant it has just arrived at node 49, the drop node
    of an onboard rider (rq-f13a30a; FleetPy `req-160a…`).
  - FleetPy has started that zero-duration service leg and keeps it locked for the step (`Vehicles.py:226-228`).
  - The event batch at 4,191,283 has no `vehicleReachedStop`/`passengerAlighted` for it, so the Runner still has the
    drop as a remaining, unlocked stop. Its new route for the donor (the moved rider removed) starts with that same drop.
- **Diagnostic run, not evidence.**
  - Setup: `diag_launcher_p1.py` = `bridge_launcher_g2.py` plus a dump at the failure; no repository change.
    Output in `C:/RideBoundData/research/g2-devsmoke/p1-diag/run2`, dump `locked-leg-dump-run2.json`.
  - It reproduces the same failure at the same time and vehicle.
  - FleetPy's locked leg and the Runner's first leg have the same `_leg_signature` in every field (status BOARDING,
    node 49, alighting `req-160a…`, duration 0, earliest start/end) **except `locked`** (True vs False).
  - FleetPy's stored head is that started stop: locked, `started_at` 4191.283, the same rider.
- **Why the A8 fix does not act.**
  - It keeps FleetPy's started stop only when the stop's riders are already executed in the Runner's route
    (`fleet_control.py:1019-1027`). Here the Runner has not yet been told.
  - The first diagnostic attempt (run1) failed in my launcher (`No module named 'src'`) before any simulation, so it
    produced no job output. Fixed by putting the FleetPy root on the path for the import.
- **Not fixed. Options for the owner** (the same class as A7/A8, a connection-layer gap; no RideBound or emulator
  change in A):
  - **A:** when FleetPy's current locked zero-duration service leg equals the Runner's first leg in every field except
    `locked`, and FleetPy's stored head is that started stop, use FleetPy's started stop in place of the Runner's first
    stop. It is the same stop; the Runner receives the reached/alighted events in the next batch. The branch would
    act only where the old code fails closed. Test first (reproduce), then rerun every G2 job and require the earlier
    matching jobs to stay byte-identical, as in A8.
  - **B:** report as a limit (1 of 77 phase-1 cell-arms).

## P4 — 2026-10-24 ~00:00 — step 1.4 pilot v1 failed in the preflight (my driver rule); drivers v2

The first step-1.4 pilot (`run_r128.py r128pilot`, 23:58) failed in all 4 jobs within about 6 s, with no simulation and
no output folder:

> source cardinality drift: driver declared {requestCount 108, vehicleCount 8, ...}, scenario has {128, 10, ...}
> (`actual_fleetpy_medium_preflight.py:812-823`)

**My error.** The `prepare_bridge.py` rule (replace only the two source hashes) assumes cells of the bridge size, and
the driver also declares the cell's cardinality. The logs stay in `logs/r128pilot/`.

The monitor thread also printed a psutil `NoSuchProcess` traceback, because the process ended before the first sample.
This is cosmetic: the job state was read from the output folder.

**Fix.**
- `prepare_r128_v2.py` builds `drivers-r128-v2/`: the sealed v1 drivers with `expectedRequestCount` 128 and
  `expectedVehicleCount` 10. Both values are checked against each scenario's own `validationSummary`; nodes 96 and
  arcs 9,120 are unchanged and checked. Manifest: `drivers-r128-v2.sha256.txt`.
- The first run of the script stopped on a wrong field source (the normalization report has no counts) after creating
  the empty folder. The script now accepts only an empty folder.
- `run_r128.py` (v1 kept as `run_r128.v1.py.bak`) uses the v2 drivers, phases `r128v2pilot`/`r128v2main` and gate
  files `-v2`.
- The sealed emulator predictions are unaffected: the emulator reads only the fixtures.

## P5 — 2026-10-24 ~00:20 — owner decision

Owner, verbatim: "Đồng ý theo ý bạn đi làm theo hướng công ty đi nếu có phải sửa code C# cx k sao."

This approves both proposals of P3 and P2:
1. **Fix A for the donor-side locked service leg**, with the A8 conditions:
   - dwell 0 confirmed;
   - a reproducing test written before the fix;
   - every G2 job rerun, with the earlier matching jobs required byte-identical (A5 provenance rule) and the stopped
     job required exact against the sealed emulator; stop and report if a matching job changes;
   - the full record kept.
2. **Step 1.3:** an opt-in C# option for the service duration of inserted stops, off by default. It is AI-written, and
   the student reviews it before it counts as evidence.

All code changes go to a new worktree: `C:/Code/RideBound-g2` is in use by r128v2main until about 00:50.

## P6 — 2026-10-24 ~00:50 — donor-side fix (conditions 1–2 met), dwell option in C#, predictions sealed

**Worktrees** (both from `research/g2-core` 937bb31; `C:/Code/RideBound-g2` untouched, inventory still `36d70c9c…`):
- `C:/Code/RideBound-p1fix` (branch `research/p1-fix`): the adapter fix only, frozen for the fix1 reruns.
- `C:/Code/RideBound-p1dwell` (branch `research/p1-dwell`): the same adapter fix plus the C# dwell option.

**New worktrees get CRLF** (memory trap). `git worktree add` wrote 221 text files with CRLF (`i/lf w/crlf`), which
broke the WP13 frozen-source tests in my first suite run: 430 tests, 3 import errors
(`adapter-suite-p1fix.txt`, kept).

`restore_index_bytes_p1fix.py` / `_p1dwell.py` (copies of `deadline/restore_index_bytes.py`, only the path changed)
rewrote the exact index bytes. Afterwards `--check` reports 4,228 tracked files with exactly 1 differing:
`fleet_control.py`, the fix.

The fix was saved first as LF (`fleet_control.p1fix-LF.py`: +62 lines, 0 removed against the index blob) and put back
after the restore.

**Condition 1 (dwell 0).**
- The stopped job's transcripts have serviceDurationMs 0 on every stop.
- The FleetPy boarding time is 0 (preflight:241).
- The emulator runs with dwell 0 (no `cfg.dwell` in `emulate_p1.js`).

**Condition 2 (reproduce before fixing).**
- Job level: diagnostic run2 (P3) reproduces the same failure at the same time on the same vehicle.
- Unit test `simulators/fleetpy-ridebound/tests/test_in_progress_donor_leg.py`, 4 tests:
  - main case: the donor alights A at node 1 while the Runner still holds A's drop as remaining;
  - three fail-closed edges: a different first stop; a leg with duration; no locked leg.
- Results, in order:

| File | Code | Result |
|---|---|---|
| `donor-fix-tests-BEFORE.txt` | unfixed | 4 errors from **my fixture** (`RBWP7_DROPOFF_NODE_MISMATCH`: the helper's fixed nodes); kept |
| `donor-fix-tests-BEFORE-2.txt` | unfixed | **main case FAILS** (first stop not locked); 3 edges pass |
| `donor-fix-tests-AFTER.txt` | **still unfixed** | My Edit calls were refused because the file had not been Read in that worktree. Kept; it is not an AFTER result |
| `donor-fix-tests-AFTER-2.txt` | fixed | 8/8 pass (4 new + the 4 A8 tests) |

**Fix** (`fleet_control.py`, +62 lines). `_started_first_stop` is the twin of `_in_progress_service_stop` and is
called only when A8's branch returns None. It returns FleetPy's started stop, used **in place of** the Runner's first
stop (the same stop, so it is not duplicated), only when all of the following hold:
- the current leg is a locked zero-duration BOARDING leg;
- the Runner's first leg is unlocked, not equivalent, and equal in every other signature field;
- the stored head is that started stop.

Otherwise the plan is exactly the Runner's route. The branch acts only where `assign_vehicle_plan` would have raised.

**Adapter suite** (byte-clean worktree, `adapter-suite-p1fix-2.txt`): 461 tests, with exactly the 6 known failures.

**Correction of my earlier record.** A8/A9 (and ADR-081) say "adapter suite 456". That count was taken before the 4th
A8 test (late boarding) existed: in the session transcript the run reporting 456 comes before the first appearance of
that test. v4's suite is 457, and 457 + 4 = 461 now.

**Diagnostic smoke with the fixed adapter** (not evidence; `g2-devsmoke/p1-diag/fixsmoke`): the stopped cell-arm
completes and is exact against the sealed emulator, 102/102 riders, moves 11 = 11.

**fix1 rerun prepared** (condition 3):
- `run_fix1.py`: 108 G2 jobs = 33 bridge + 55 phase-1 + 20 step-1.4, each with its original inputs.
- Adapter `C:/Code/RideBound-p1fix`, inventory `4cd1c77b…` (`FIX1-INVENTORY.txt`).
- `bridge_launcher_fix1.py` and `world-fix1/`, each one line different.
- Verdict: `compare_fix1.py`.
- Starts after r128v2main.

**Dwell option** (owner approval P5; AI-written, the student reviews it).
- `CandidateGenerationOptions(…, long stopServiceDurationMs = 0)` → `StopServiceDuration`. Negative values throw;
  `WithRepairRequestCap` keeps it.
- `InsertionCandidateGenerator.Expand` uses it for the inserted pickup and drop instead of `new Duration(0)` (lines
  801-812 before).
- `Wp4RunnerConfiguration`: optional `candidateGeneration.stopServiceDurationMs`, an integer from 0 to 3,600,000.
  Absent means 0: the archived tier-3 content hash `fb2813c4…` is unchanged.
- The adapter already passes each stop's `serviceDurationMs` to FleetPy (`fleet_control.py:1104`). The verifier and
  the protocol only require an integer.
- Tests: `StopServiceDurationTests` (2) and `Stop_service_duration_is_optional_bounded_and_off_by_default` (1).
  - A fraction is refused by canonical JSON first (`CanonicalJsonException` at the same path). The test asserts that.
- Full `dotnet test` is running.

**Step-1.3 predictions** (`emulate_dwell.js`, wf15 engine with `cfg.dwell` 24,000):
- 4 cells fixed before any run: the first two w08 and the first two w17 by name;
- arms: D24-U, D24-RCE35, D24-EOHZ35, D24-G2-U, D24-G2-RCE35, D24-G2-EOHZ35;
- every rider differs from dwell 0 (`discrimination-dwell.txt`);
- sealed `EMULATOR-PREDICTIONS-DWELL.sha256` at 2026-10-23T17:41:50Z, with 0 step-1.3 jobs.

WP4 configs `wp4-costfirst-dwell24`, `wp4-g2-costfirst-dwell24` (`configs-dwell.sha256.txt`). `world-dwell/` and
`bridge_launcher_dwell.py` point to p1dwell.

## P7 — 2026-10-24 01:00 — step 1.4 result; fix1 reruns started; dwell-off replay gate running

**Step 1.4**:
- runs: r128v2pilot (4 jobs) + r128v2main (36), 00:00–00:53;
- compare_r128.py (`compare/r128v2all-2026-10-24-compare.json`): **39/39 completed cell-arms exact, 4,492/4,492
  riders identical**; G2 moves equal 19/19;
- by arm: R128-U 10/10, R128-RCE35 10/10, R128-G2K8-U 9/9, R128-G2K8-RCE35 10/10.

One job stopped: `R128-G2K8-U-d20181113-s10-r1-w17`, with `RBWP7_LOCKED_LEG_MISMATCH at $.vehicles[9].activeLeg`
after 72 s, on the **old** adapter (RideBound-g2). Output kept. It is one of the 20 step-1.4 G2 jobs in fix1r128:
- if the fixed adapter completes it exactly, it is the same donor-side case;
- otherwise it is a new variant, to be diagnosed separately.

**fix1** (condition 3) started 01:00:
- `run_fix1.py` fix1bridge → fix1p1 → fix1r128, 3 workers;
- `run_fix1.py check` OK for all 108 jobs;
- verdict to come from `compare_fix1.py`.

**Dwell-off replay gate** (`replay_gate_dwell.py`, runner-p1-dwell-v1, 2 workers):
- started 00:46 on ins + g2 + p1 = 189 recorded jobs;
- the 40 step-1.4 jobs follow after it.

## P8 — 2026-10-24 ~01:25 — C: reached its 30 GiB floor; transparent compression, no deletion; new outputs on E:

**What happened.** The first fix1 run (01:00) ran 6 fix1bridge jobs (all status-pass). Then the driver deferred every
other job, as the rail is meant to: "C:\ free 29.6 GiB < floor 30 GiB". fix1p1 and fix1r128 started 0 jobs. No output
was damaged.

**Why.** Tonight's job outputs on C: grew to 15 GB (`via-bridge-man-v1/p1`) + 0.9 GB (`fix1`).

**My lapse:** the memory file sets my working threshold for C: at ≥ 50 GB (the owner's floor is 30 GiB). I did not
stop at 50.

**Action: no file deleted, no path changed.**
1. `hash_tree.py` hashed every file under `via-bridge-man-v1/p1` and `fix1`: 488 files, 15.42 GiB
   (`compress-before-2026-10-24.tsv`).
2. NTFS transparent compression: `compact /c /s:<dir> /i /q`. Content and paths unchanged, reversible with
   `compact /u`.

   | Folder | Ratio | Before → after |
   |---|---|---|
   | p1 | 1.7 | 15.59 → 9.06 GB |
   | fix1 | 1.8 | 0.97 → 0.55 GB |

   C: free went from 29.6 to 36.1 GiB.
3. Rehash: `compress-after-2026-10-24.tsv` is **byte-identical** to the listing before (`cmp`). Every size and sha256 is
   unchanged.
4. Every later output goes to **E:** (142 GiB free, floor 40):
   - `run_fix1.py` OUT_ROOT is `E:/RideBoundData/research/via-bridge-man-v1/fix1`. A job already finished on C: is
     reported as `done-on-C:` and not rerun. The earlier file is `run_fix1.v1.py.bak`.
   - `compare_fix1.py` reads both roots; bridge_compare runs once per drive. The earlier file is
     `compare_fix1.v1.py.bak`.
   - `run_dwell.py` OUT_ROOT is `E:/.../via-bridge-man-v1/p1`.
5. fix1 resumed at about 01:27 (`driver-fix1*-2.log`).

The xz scheme of 2026-10-22 (`archive-compress-2026-10-22`) was not used. It replaces `transcript-00.ndjson` with
`.xz`, which the comparison and replay tools cannot read, and it had its own owner approval for the archive.

## P9 — 2026-10-24 03:40–12:15 — condition 3 met; dwell-off gate complete; step 1.3 blocked by a core modelling gap

**Dwell-off replay gate.** `replay_gate_dwell.py`, runner-p1-dwell-v1 with `stopServiceDurationMs` absent:
**228/228 recorded jobs byte-identical, 176,074/176,074 frames** (ins 80, g2 33, p1 76, r128 39;
`DWELLOFF-GATE-SUMMARY.json`).

*My error, found and fixed.* v1 named results by folder only. Smoke and main both hold PS-300 and DLHZ-120 on
d20181114-w08, so the 2 main jobs were skipped as "done before": the first summary showed 187/187, with 78 ins jobs.
v2 (`replay_gate_dwell.v1.py.bak` kept) names such a main job `ins-main-…`. Both were replayed and are identical.

**fix1 (owner condition 3).** 108 jobs, 108 status-pass:
- 6 on C:, then 102 on E: after P8, 01:27–03:26;
- `compare_fix1.py 2026-10-24` → `compare/fix1-2026-10-24/condition3.json`.

Results:
- **(a) 106/106 earlier-completed jobs identical:**
  - 73 frame-identical: all p1 and r128 G2 jobs, same commit and Runner;
  - 33 bridge G2 jobs differ only in frame 2 at `manifest.coreCommitSha` / `binarySha256`. A5 applies: the v4 runs
    had HEAD 7435661, fix1 has 937bb31.
- **(b) every fix1 job exact against its sealed emulator predictions:**

  | Family | Cell-arms | Riders | G2 moves |
  |---|---|---|---|
  | bridge | 33/33 | 2,994/2,994 (604 + 2,390) | — |
  | p1 | 55/55 | 4,948/4,948 | 55/55 |
  | r128 | 20/20 | 2,310/2,310 | 20/20 |

- **The two earlier-stopped jobs are now complete and exact:**
  - `G2K3-U-d20181112-s10-r1-w08` (P3);
  - `R128-G2K8-U-d20181113-s10-r1-w17` (P7, vehicle 9, old adapter). The same fix covers it, so it belongs to the same
    donor-side family.
- No earlier matching job changed. **Owner condition 3 is met.**

**Step 1.3 (dwell 24 s): blocked, not fixed, outputs kept.** `run_dwell.py dwellpilot`, 03:37–03:39: all 6 jobs failed
after about 40 s with `RBWP7_LOCKED_LEG_MISMATCH at $.vehicles[1]`. Those 6 include the insertion-only arms, so G2 is
not the cause. Outputs: `E:/RideBoundData/research/via-bridge-man-v1/p1/dwellpilot/`.

Diagnostic (not evidence; `diag_launcher_dwell.py`, `E:/RideBoundData/research/g2-devsmoke/dwell-diag/`):
- Vehicle 1 started boarding `req-e8f0…` at node 94 at 1,222.38 s. FleetPy holds that 24 s leg locked until
  1,246.38 s (duration 24.0).
- At 1,235 s a new request arrives. The Runner already counts the pickup executed at arrival, so its new route starts
  by driving to node 42 at once.

Cause, in the core:
- `RouteScheduleProjector` (`RideBound.Application/Scheduling/RouteScheduleProjector.cs:62-128`) starts every schedule
  at the evaluation time from the vehicle's position. No vehicle state says "busy serving until T".
- The emulator has exactly that (`engine.js:165`, `busyUntil = t + dwell`).
- The A8 and donor-side fixes act only on zero-duration legs, by design.

So with dwell > 0, RideBound would plan departures up to 24 s too early, and FleetPy refuses the replacement.

A real test of dwell > 0 needs:
1. a vehicle "available-from" time in the Domain;
2. the projector to start from it;
3. the adapter to report the end of the current service, which is a protocol/event field (a contract change);
4. the A8 keep-the-started-stop rule extended to service legs with a duration;
5. tests and a new Runner, then this bridge.

The C# option `stopServiceDurationMs` alone is necessary but not sufficient. It stays in p1dwell, inert when absent
(228/228 above). **Waiting for the owner.**
