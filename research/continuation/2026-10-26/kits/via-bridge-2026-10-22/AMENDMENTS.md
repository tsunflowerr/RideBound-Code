# Manhattan bridge: amendments

## A1 — 2026-10-22 00:30 — comparator excluded process measurements from the summary comparison

**What happened.**
- The regress job (`REG-CF-U-d20181114-s10-r1-w08-1500`) finished with `status-pass` in 12.1 min. Its peak process-tree
  RSS was 889 MiB (Runner 784, Python 143).
- `compare_runs.py` (version 1) reported `framesIdentical 2311/2311`, `firstDifference null`, but
  `summaryIdentical false`. The only difference was `$.runs[0].resources.rssAfterBytes` (148,090,880 vs 148,455,424).
  The driver therefore wrote `REGRESS-FAILED.txt` and `regress-compare.json`. Both files are kept unchanged.
- The full summary diff (raw files):
  - `runs[0].resources` (rssBefore/After, system/user CPU, wall);
  - `semanticHash`, which includes the manifest hash and therefore the Runner `binarySha256`. This is expected for a
    new build (bundle `docs/wf9/review/REVIEW.md` B1), and it is masked by the comparator in both versions.

  Everything else was equal: label, repositoryInventorySha256 `0da63eaf…`, status, sourceScenarioContentSha256 and
  repeatCount.

**Defect.** Version 1 compared `runs[*].resources`, which holds measurements of the process: memory, CPU and wall
time. They differ between any two executions and are not behaviour. The gate's stated purpose is "the rebuilt Runner,
the rule patches with their keys absent, the fix and the GC cap change no decision" (`run_bridge.py` docstring).

**Change.** `compare_runs.py` now drops `runs[*].resources` before comparing summaries. Nothing else changed.

**Re-check of the comparator:**
- archive vs itself: identical;
- CF-U vs CF-C-30 (archive smoke): still differs (185/2245 frames, first difference at a decision);
- archive CF-U vs the regress job: identical, with 2311/2311 frames and the summary equal (`regress-compare-A1.json`).

**Verdict.** REGRESS-PASSED under A1. Same decisions as the archived Stage-1b smoke job.

**Side observation, not a gate.** With the buffered read, system CPU fell from 12,188 ms to 453 ms and wall time
from 983 s to 708 s. This matches the company's measurement.

## A2 — 2026-10-23 — Elmi budget patch v1.1 after the independent review (does not change the bridge Runner)

**Review.** An independent read-only review of `elmi-slack-budget-v1.patch` found no blocking issue. It confirmed:
- all four evaluator calls get the same anchor and limit;
- the rounding is identical to the emulator (`Math.round(alpha*S)`; `350/1000.0` is the same double as `0.35`);
- configs without the new keys behave and hash exactly as before;
- the tests catch both a missing per-rider limit and a re-measured limit.

**Should-fix 1.** Hard utilization (`HardVectorCandidateAssessor.cs:586-592`) reads only the fixed `HardLimit`. Under
stability-first C1 (or C2, or non-solver C1), a per-rider budget would therefore silently drop out of the
`worst-hard-utilization-ppm` ranking. Cost-first never adds that level (`SolverBackedFleetSelection.cs:344-347`), so
the bridge was not affected.

**Change (v1.1).**
- `Wp4RunnerConfiguration.Decode` now rejects `dropEtaBudgetSlackPermille` for the C1/C2 policy kinds unless
  `selectionObjectiveOrder` is `cost-first` (fail closed).
- 5 new tests:
  - no-worse recovery under a rider budget, identical to a fixed limit of 315 ms;
  - the two-sided floor with a rider deadline, 140 ms slack;
  - an onboard rider keeping the first-promise budget (2 cases);
  - the configuration guard.
- A corrected code comment on the 2^53 range.

`dotnet test RideBound.slnx` 1178/1178. Tree `290c698b…`, delta patch `elmi-slack-budget-v1.1-delta.patch` (sha256
`3ba1f0bc…`).

**Effect on the bridge.** None. The published `runner-via-elmi-v1` stays built from v1 (tree `3215515…`). The v1.1
guard is reached only when the order is not cost-first. All elmi-phase jobs use `wp4-tier3-v2-costfirst.json`.

**Known difference (not changed).**
- The emulator's EOC arm and `ELMI-OC-SPEC.md` use `floor(gamma*S/1000)`; RCE/EOHZ (emulator and RideBound) use
  `Math.round`. Example: S = 10, permille 350 gives 3 under EOC and 4 here. RideBound EOHZ35 is therefore compared
  only with emulator EOHZ35, never with EOC.
- `docs/18` and `docs/19` are not updated yet, because that needs the owner's commit decision.

## A3 — 2026-10-23 — elmi phase result; `a0_gate.py` extended; G2-off replay gate

**Elmi phase**
- Ran 00:50:54–01:17:47 (`run_elmi.cmd`, 2 workers): 22/22 jobs `status-pass`, about 2.4 min each.
- `bridge_compare.py --root …/elmi --emulator emulator-riders-elmi-2000.tsv --seal EMULATOR-PREDICTIONS-ELMI.sha256`:
  - 22/22 cell-arms exact and 1966/1966 riders identical (RCE35 11/11, EOHZ35 11/11);
  - files: `compare/elmi-22-2026-10-23-compare.json` (sha256 d6ad3427…), `-real-riders.tsv` (1af5312e…).
- **The rules bind.** Against U on the same 11 cells, RCE35 differs in 11/11 cells:
  - 873/1072 riders differ in served, sum|c|, p0, A or netC;
  - sum|c| falls from 68,302 s to 17,452 s;
  - served falls from 1011 to 982.

  EOHZ35 differs in 904/1080 riders; its served is 984. The exact match therefore tests the new code paths. These
  numbers are descriptive only (11 bridge cells, 2.0×). They are not a comparison of rules.

**`a0_gate.py` change**
- `ARMS` gains RCE35 and EOHZ35; `finished()` and the summary filter also read the elmi phase.
- Earlier invocations behave the same: the default `--phases` is unchanged, and the new arms and phase are only
  reached when the elmi phase is named.
- Previous version kept as `a0_gate.v1.py.bak`. New sha256 0c93a4bc….

**G2-off replay gate**
- Runner `runner-g2-v1`: G2 worktree tree 6b927ae1 = v1.1 + G2 phases A–F; manifest
  `g2-2026-10-23/runner-g2-v1.sha256.tsv`, tree 3f0e6294….
- Every finished job was replayed with its own configs, in which no G2 key is present:
  `a0_gate.py --runner C:/RideBoundData/research/runner-g2-v1 --tag g2off --phases regress,smoke,main,elmi`.
- Result: **80/80 jobs byte-identical, 59,330/59,330 frames** (1 regress, 2 smoke, 55 main, 22 elmi).
  `G2OFF-GATE-SUMMARY.json` sha256 fa702a90….
- So the G2 code, when off, changes no decision, promise, checkpoint or state hash of any job run so far.

## A4 — 2026-10-23 — G2 bridge: review fixes, frozen code, sealed predictions, driver

**Independent review of G2 A–F.** Read-only; 43/43 relocation tests pass. No blocking defect. The off-path is identical by
line-by-line reading, including the paths the 80-job replay does not exercise (B4 repair, origin-hold, exact-small,
stability-first, C2, rolling-cost, multiple-plan).

Fixed after the review (all inert without G2):
- **S1, evidence.**
  - With G2 the Runner writes `evidenceVersion` 1.3.0 and `generation.relocationLosses`; the relocation work units are
    already part of `consumedGenerationWorkUnits`.
  - `actual_fleetpy_medium_verify.py` accepts 1.3.0. It adds the relocation work, checks the rider accounting and the
    candidates' relocation fields, and verifies the portfolio when it is present.
  - 2 new verifier tests; 13/13 in that file.
  - The verifier is NOT part of a run's pass/fail (`actual_fleetpy_medium_preflight.py` only lists it as an artifact).
    G2 job evidence is checked with it after the pilot.
- **Minor fixes:**
  - M1: `SolverBackedRidePoolingPolicy.Decide` throws for G2 with any policy kind other than C1.
  - M5: the flag also rejects a vehicle lock in `freezeHorizonLocks`.
  - The config reader's error order is restored for non-G2 configs.
  - Added: a partner-independence test (DESIGN 3.3) and the matching config, lock and guard tests.

`dotnet test RideBound.slnx` 1224/1224. Adapter Python suite: 451 tests, 6 failures, the same 6 fail on the non-G2 via
worktree (frozen-receipt checks). Relocation module tests 8/8.

**Not covered yet (S2).** No Runner-level test drives a real move through `RunnerSession`, the action frame, a checkpoint
and restore, and the adapter round trip. The g2pilot jobs are the first end-to-end exercise. A pass there is evidence,
but it is not a substitute for unit tests of checkpoint and restore after a move.

**Frozen code.**
- G2 worktree `C:/Code/RideBound-g2`: staged tree 5fedf3bb… = v1.1 + G2 A–G + review fixes. Patch
  `g2-2026-10-23/g2-A-G-v2.patch` (sha256 e3f1b9cd…), 41 files, +3907/−58.
- Runner `C:/RideBoundData/research/runner-g2-v2`: manifest `g2-2026-10-23/runner-g2-v2.sha256.tsv`, tree b859b893….
- Adapter-repository inventory 6f7411cb… (`g2-2026-10-23/G2-INVENTORY.txt`). It is computed with the run preflight's
  own function, which reproduces 0da63eaf… for `E:/Code/RideBound-deadline`.
- A second G2-off replay gate with runner-g2-v2 (tag g2offv2, all 80 jobs) runs before `G2-READY.txt`.

**Sealed predictions.** `bridge_emulate_g2.js` (emulator-wf5-eoc, G2 = `{reopt: {g2: 99}, lock: 'ratchet'}`) on the 11
cells × G2-U, G2-RCE35, G2-EOHZ35:
- 2994 rider rows; 0 invariant violations;
- relocations per cell-arm 0–16 (G2-U 54, G2-RCE35 26, G2-EOHZ35 62 in total).

Sealed in `EMULATOR-PREDICTIONS-G2.sha256` at 2026-10-22T22:11Z (05:11 local), before any G2 Runner job.

**G2 configs.** `configs/G2-*.json` = U, RCE35 and EOHZ35 with these changes:
- `vehicle_switch_count` hardLimit removed (the emulator has no switch limit);
- `finalConfirmationLocks` ["pickupStop", "pickupEta"];
- `ratchetLocks` ["pickupEta"];
- `waitingRiderRelocation` true.

`configs/wp4-g2-costfirst.json` = the cost-first WP4 plus `candidateGeneration.waitingRiderRelocation: {}`.
Hashes are in `configs-g2.sha256.txt`.

**Wrapper and driver.**
- `g2-2026-10-23/world/` is a copy of `tang3/world` (preflight_world*.py, world_v1.py). Only `REPO` differs (the G2
  worktree). Hashes are in `world-g2.sha256.txt`.
- `run_g2.py` is generated from `run_bridge.py`: same rails, and phases g2regress → g2pilot → g2main, each gated:
  - g2regress needs `G2-READY.txt`;
  - g2pilot needs `G2REGRESS-PASSED.txt`;
  - g2main needs `G2-PILOT-REVIEWED.txt`, written by hand after the pilot is compared with the sealed emulator.
- g2regress runs U and RCE35 with G2 OFF through the G2 adapter and Runner. They are compared by `compare_runs.py`
  with main/U and elmi/RCE35 of the same cell, which tests the adapter changes end to end.

**Known risk, to be measured in the pilot.** The adapter asks for a second ("observation") decision in the same time
trigger when vehicles reported semantic events (`fleet_control.py`, `_call_time_trigger_request_batch`). If RideBound
relocates in decisions the emulator does not take, the G2 arms diverge from the emulator even though the code is right.
Divergence on the frozen prefix (RideBound moves only riders whose pickup is in the mutable suffix) is the other
listed risk.

## A5 — 2026-10-23 06:25 — g2regress: the adapter repository commit is provenance

**What happened.** g2regress ran 06:19–06:22. Both jobs (U and RCE35 on d20181114-s10-r1-w08, G2 OFF, runner-g2-v2,
adapter from the G2 worktree) ended `status-pass`. `run_g2.py` then wrote `G2REGRESS-FAILED.txt`:
- U: 2382/2383 frames identical, summary identical;
- RCE35: 2265/2266 frames identical, summary identical.

Both files are kept unchanged.

**The one differing frame.** In both jobs it is frame 2 (`initializeRun`). A full diff of that frame shows exactly two
differences, both in `$.payload.manifest`:
- `binarySha256`: the Runner build. This is a 64-hex value that the comparator masks elsewhere.
- `coreCommitSha`: `1ce15e92…` in the archived jobs (adapter repository `E:/Code/RideBound-deadline`) versus `7435661f…`
  (the G2 worktree's HEAD). It is a 40-hex value, which the comparator does not mask.

Every other byte of every frame is identical, with 64-hex hashes masked: all decisions, routes, acceptances and
promises. So are the summaries.

**Reading.** This phase runs the adapter from a different repository on purpose; that is its precondition, and it is
listed in the `run_g2.py` docstring. The commit difference is provenance by construction, in the same sense as A1's
process measurements. The phase's stated question is whether the adapter changes and the G2 code, when off, change any
behaviour, and the answer is no.

**Change.** None to the comparator. `G2REGRESS-PASSED.txt` was written by hand under A5, citing these two diffs.

## A6 — 2026-10-23 07:00 — v2 pilot failed closed; Contracts fix; dev smoke; runner-g2-v3; driver v3

**What happened.** The v2 pilot (06:23, `runner-g2-v2`) failed in all 3 jobs (G2-U, G2-RCE35, G2-EOHZ35) after about 20
s, at the first decision:
- The Runner answered `INTERNAL_ERROR` ("could not process the message safely") and ended the session. Outputs kept:
  `via-bridge-man-v1/g2pilot/*` (failure-00.json and transcript) and `logs/g2pilot/*`.
- Root cause, from the full exception of a debug build (`git archive` of the frozen tree into the session scratchpad;
  only `RunnerHost.cs` there prints `exception.ToString()`; the G2 worktree was not touched): `ArgumentException` in
  `RideBound.Contracts.Protocol.DecisionPayloadCodec.ValidatePayloadForEncoding` (`DecisionMessages.cs:580`).
- That validator accepted only evidence versions 1.0.0–1.2.0. The review fix S1 (A4) made the Runner write 1.3.0 with
  G2, so the Runner's own contract check refused to send it. The protocol's fail-closed design worked as intended.
- So the v2 G2 state was never published. The defect was mine: S1 was implemented in the Runner and the Python verifier
  but not in the Contracts codec.

**Fix (Contracts only).**
- `DecisionMessages.cs`: accept 1.3.0. Its portfolio is optional and validated when present. The candidate fields
  `relocatedRequestId` and `relocationRole` (`donor`|`receiver`, both or neither) are allowed only in 1.3.0 portfolios.
- New test `G2_evidence_v13_allows_relocation_fields_only_in_its_own_version`.
- `dotnet test RideBound.slnx` 1225/1225.
- Tree 6d071153… (= v2 tree 5fedf3bb + `DecisionMessages.cs` + its test). Patch `g2-2026-10-23/g2-A-G-v3.patch`
  (sha256 23ef453b…). `runner-g2-v3`, tree ff7b09a3…. Inventory 90957b38… in `G2-INVENTORY-v3.txt`.

**Dev smoke (diagnostic, NOT evidence).**
- A scratch Runner built from the same source ran G2-U, G2-RCE35 and G2-EOHZ35 on d20181114-s10-r1-w08 through the same
  launcher, outside the gated driver (`C:/RideBoundData/research/g2-devsmoke/dev1`).
- All 3 `status-pass`.
- Real moves 4, 6 and 10: the same counts as the sealed emulator (4, 6, 10).
- 2334 decisions, all with evidence 1.3.0, and 0 failures under the 1.3.0 verifier.
- `bridge_compare.py` against the sealed G2 predictions: 3/3 cell-arms exact, 286/286 riders identical.
- This is why the formal v3 pilot is expected to pass. The formal run repeats it with the published runner-g2-v3 and
  the gates.

**Driver v3.** `run_g2.py` is now versioned (VERSION = "v3"; the previous file is kept as `run_g2.v2.py.bak`):
- runner-g2-v3 and `G2-INVENTORY-v3.txt`;
- outputs `via-bridge-man-v1/g2v3/<phase>` and logs `logs/g2v3/<phase>`;
- gate files `G2-READY-v3.txt`, `G2REGRESS-PASSED-v3.txt` and `G2-PILOT-REVIEWED-v3.txt`, so no v2 file or folder is
  reused or overwritten;
- the archived comparison jobs are still read from `via-bridge-man-v1/main` and `/elmi`.

The A5 rule is now automatic in the g2regress verdict (`a5_provenance_only`). A comparison that is not identical passes
only if:
- exactly one frame differs, and it is frame 2 (`initializeRun`);
- the full diff of that frame lies within {`manifest.binarySha256`, `manifest.coreCommitSha`};
- the summaries are identical.

Checked:
- on the two v2 regress jobs: both pass;
- on a crafted report whose difference is in another frame: fails;
- on mismatched U/RCE35 frames (diff includes `policyConfigurationHash` and `runId`): fails.

**Gate before G2-READY-v3.** The G2-off replay of all 80 jobs through runner-g2-v3 (tag g2offv3).

## A7 — 2026-10-23 08:15 — G2 bridge v3 results; 3 jobs stopped at an emulator move FleetPy cannot execute

**Gates and pilot.**
- g2offv3: 80/80 jobs byte-identical, 59,330/59,330 frames (G2OFFV3-GATE-SUMMARY.json, dbac0bbe…).
- g2regress v3: `G2REGRESS-PASSED-v3.txt`; both jobs pass under the automatic A5 rule.
- g2pilot v3: 3/3 `status-pass`. Against the sealed emulator: 3/3 cell-arms exact, 286/286 riders, moves 4/6/10 equal
  to the emulator, 2334 decisions with 1.3.0 evidence and 0 verifier failures (`G2-PILOT-REVIEWED-v3.txt`).

**g2main v3** (07:35–08:00, 3 workers): 27/30 `status-pass`.
- `bridge_compare.py`: 27/27 cell-arms exact, 2439/2439 riders identical (G2-U 9/9, G2-RCE35 9/9, G2-EOHZ35 9/9)
  (`compare/g2v3-main27-2026-10-23-compare.json`, sha256 75e6a8a6…).
- With the pilot: **30/30 completed G2 cell-arms exact, 2725/2725 riders identical** to the predictions sealed before
  any G2 job.
- Moves:
  - The ledger-based counter (`g2_moves.py`) gives 107 moves in the 27 jobs (G2-U 45, G2-RCE35 18, G2-EOHZ35 44).
  - It equals the emulator's relocation count in 25 of 27 jobs.
  - In the other 2 (G2-EOHZ35 16/11 w08: 4 vs 5; G2-U 18/11 w08: 2 vs 3) the counter misses a rider that was
    confirmed and moved in the same decision; its first promise opens on the receiver (`initialPromise`, which the
    counter skips).
  - Counted by plan membership (a rider's pickup in another vehicle's plan), both jobs give 5 and 3, equal to the
    emulator.
  - At most 1 move per decision, checked per decision in those jobs (two moves at the same sim time come from two
    consecutive decisions: the main and the observation decision).
- Evidence: 19,913 decisions, 0 failures under the 1.3.0 verifier (`compare/g2v3-main27-2026-10-23-moves.jsonl`,
  sha256 3be66181…).

**The 3 stopped jobs.**
- Jobs: G2-EOHZ35 12/11 w08 at t=4,739,187 ms; G2-U and G2-RCE35 16/11 w08 at t=3,017,769 ms.
- All three end with the adapter's `RBWP7_LOCKED_LEG_MISMATCH at $.vehicles[i].activeLeg: replacement is not
  exact-equivalent` (`fleet_control.py:369-376`). Outputs and failure records are kept in `g2v3/g2main/*`.
- In each case RideBound's decision is **exactly the emulator's move** at that instant: the same rider and the same
  receiver (emulator trace, `scratchpad/g2/trace_g2.js`):
  - 16/11: rq-81e4f55… veh-006 → veh-002 at 3,017,769;
  - 12/11: rq-f992d21… veh-003 → veh-007 at 4,739,187.
- The receiver has just arrived at the moved rider's pickup node, and the decision is triggered by that arrival (the
  times are off the 60 s grid). It is boarding another rider there: `vehicleReachedStop` plus `passengerBoarded` in the
  same batch.
- The move inserts the moved rider's pickup at that node as the receiver's next stop (pickup ETA = now), which is the
  cheapest possible receiver. FleetPy keeps the in-progress boarding leg locked during that step (`Vehicles.py:226-228`,
  `G_LOCK_DURATION_STATUS`), so no plan with a different first leg can be assigned, and the adapter fails closed.

**Reading.**
- The RideBound C# G2 equals the emulator's G2 in every decision observed: 30 complete cell-arms, plus 3 cell-arms up to
  the stopping decision.
- The emulator, however, contains a move that FleetPy cannot carry out at that instant: picking up at the node where the
  receiver is boarding in the same step. This is a modelling gap between the emulator (no step-level lock; 0 s dwell)
  and FleetPy. It is not a RideBound coding error. It reached 3 of 33 G2 cell-arms on this panel.
- Without G2 the same conflict can arise in principle (a new request at the node where a vehicle is boarding, decided
  in the same step). It did not occur in the 77 earlier bridge jobs.

**Not decided (owner).**
- (A) Adapter: when FleetPy's current leg is a locked zero-duration stop the Runner already counts as executed, keep that
  leg first and append the Runner's route. The emulator's move becomes executable, and neither RideBound nor the
  emulator changes. This needs a careful FleetPy study and tests.
- (B) RideBound and the emulator both forbid a G2 receiver whose next stop would be inserted at its current node in the
  arrival step. That means a new emulator rule, re-sealing and a rerun.
- (C) Report as is: 30/30 exact, plus 3 stopped with the cause above.

## A8 — 2026-10-23 — owner chose (A) with 4 conditions; a launcher error found; adapter fix; v4 full rerun

**Owner decision (verbatim, 2026-10-23).** "Chọn (A), với điều kiện:
(1) xác nhận dwell/boarding = 0 trong cả FleetPy và emulator cho các job này; nếu khác 0 thì dừng và báo, không sửa;
(2) viết test tái hiện 3 ca trước khi sửa;
(3) sau khi sửa, chạy lại cả 33 job G2 và toàn bộ job INS đã chạy, yêu cầu các job đã khớp vẫn byte-identical và 33/33
G2 khớp tuyệt đối với emulator, nếu có job nào đã khớp bị đổi thì dừng và báo;
(4) ghi vào nhật ký: lần đầu 30/33, nguyên nhân, nội dung sửa adapter, test, kết quả sau sửa — không đổi RideBound, không
đổi emulator."

**First result (A7).** 30/33 G2 cell-arms complete and exact, 2725/2725 riders. 3 jobs stopped with
`RBWP7_LOCKED_LEG_MISMATCH`:
- G2-U and G2-RCE35 on 16/11 w08 at t=3,017,769 ms;
- G2-EOHZ35 on 12/11 w08 at t=4,739,187 ms.

**Cause.**
- Each of the 3 decisions is exactly the emulator's move.
- The receiver has just arrived at the moved rider's pickup node and is serving another rider there. With 0 s dwell
  FleetPy keeps that zero-duration service leg locked for the rest of the step (`Vehicles.py:226-228`), and it refuses
  any plan whose first leg differs (`Vehicles.py:351-366`; the adapter checks this first, `fleet_control.py:369-376`).
- The Runner already counts that stop as executed (`vehicleReachedStop` and `passengerBoarded` in the same batch).
- In all 3 cases the rider being served boards a few seconds after its latest pickup (3017.77 vs 3011 s; 4739.19 vs
  4736 s). The Runner records this as a late boarding (ADR-076 record-v1).

**Condition 1 (dwell = 0): met.**
- FleetPy: `op_const_boarding_time: 0` (`actual_fleetpy_medium_preflight.py:241`); stop duration =
  `serviceDurationMs` (`fleet_control.py:971`); all stops in the 3 transcripts have `serviceDurationMs` 0 (12,754,
  3,962 and 3,956 stops).
- Emulator: `dwell` 0 for d20181112 and d20181116 (`prepareFixtureCell`, `fixtures.js:19`; `bridge_emulate_g2.js` sets
  none).

**Condition 2 (reproduce before fixing): met.**
- The 3 jobs rerun with the unchanged adapter (`C:/RideBoundData/research/g2-devsmoke/repro-unfixed`) fail
  identically: same error, same vehicle, same frame count (676, 676, 1333).
- Unit test `simulators/fleetpy-ridebound/tests/test_in_progress_service_leg.py`:
  - before the fix: FAILED (`g2-2026-10-23/adapter-fix-tests-BEFORE.txt`);
  - after: 4/4 (`adapter-fix-tests-AFTER.txt`, then 4 tests including the late-boarding case).
- The late-boarding test fails with `RBWP7_FLEETPY_PLAN_INFEASIBLE` against the first fix attempt
  (`adapter-fix-late-test-against-attempt1.txt`).

**A launcher error, found while testing the fix (correction of A4–A7).**
- `bridge_launcher.py:24-37` imports the package `ridebound_fleetpy` from `E:/Code/RideBound-deadline` to patch the
  buffered read, and the cached import made every job load that adapter.
- So every G2 job before v4 (v2 pilot, v3 regress, pilot and main, dev smokes) ran the Runner from the G2 worktree but
  the **adapter of `RideBound-deadline`** (inventory 0da63eaf, never edited). Shown by the tracebacks:
  `E:\Code\RideBound-deadline\…\fleet_control.py`.
- Corrections:
  - The G2 adapter code (phase G: `relocation.py`, the session flag, the promise vehicle id) had not run in any job
    before v4.
  - A4–A6 say g2regress "tests the adapter changes end to end"; that is wrong. It tested the G2 Runner with G2 off, on
    the old adapter.
  - The A7 numbers (30/30 exact, 2725/2725 riders) stand as measured, but with the old adapter.
- Fix: `bridge_launcher_g2.py` is a copy that differs only in `ADAPTER_ROOT` (the G2 worktree). `run_g2.py` v4 uses it.
  The original launcher is unchanged.

**Adapter fix (no change to RideBound, none to the emulator).** `fleet_control.py`: `_fleetpy_plan` now calls
`_in_progress_service_stop`. When all of the following hold, it puts a copy of FleetPy's own started stop
(`veh_plans[vid].list_plan_stops[0]`, locked, `started_at` set) in front of the Runner's route:
- the current leg is a locked BOARDING leg of zero duration;
- the Runner's route would not already start with an equivalent leg;
- FleetPy's stored plan starts with that stop, with the same node, riders, lock and start time;
- the stop's pickups and drops are executed in the Runner's route and not remaining.

This is FleetPy's convention for re-planning a vehicle mid-service (`VehiclePlan.update_plan`, step 2). The copy drops
the stop's latest-time bounds (latest pickup, latest arrival, maximum trip), because the stop is being served now and a
late boarding there is already recorded by the Runner. Its earliest times stay, so the leg is the same locked leg.
FleetPy's stored stop is unchanged.

In every other case the plan is exactly the Runner's route, as before. The fix can only act where the old code failed
closed.

Tree 0b930c4f…, which differs from v3 only in:
- `fleet_control.py` and its test;
- `tests/RideBound.Runner.Tests/Online/WaitingRiderRelocationCheckpointTests.cs` (review gap S2: a state after a move
  checkpoints and restores exactly, and a forged move-back is refused; 2 tests).

Patches `g2-2026-10-23/g2-A-G-v4.patch` (sha256 624f9d08…) and `g2-v3-to-v4-adapter-fix.patch` (sha256 d7afd095…).
Runner `runner-g2-v3` unchanged. Tests: dotnet 1227/1227; adapter suite 456 (the same 6 known failures). Inventory
c2213308… (`G2-INVENTORY-v4.txt`).

**Dev smoke with the real G2 adapter (diagnostic).**
- The 3 cases pass: 3/3 cell-arms exact, 269/269 riders.
- The pilot cell passes: 3/3 exact, 286/286 riders.

**Condition 3, run.**
- `run_g2v4.cmd` started 11:45:41: g2all (33), insregress (1), inssmoke (2), insmain (55), inselmi (22), with
  runner-g2-v3 and the G2 adapter.
- Check `compare_v4.py`:
  - (a) 33/33 against the sealed emulator;
  - (b) the 30 earlier-exact G2 jobs identical to their v3 runs;
  - (c) the 80 insertion-only jobs identical to the archived jobs, or different only under A5.
- Results: see A9.

## A9 — 2026-10-23 13:40 — v4 results: condition 3 met

**Runs.** `run_g2v4.cmd`, 11:45:41 to about 13:36, runner-g2-v3 with the G2 worktree adapter (inventory c2213308…)
through `bridge_launcher_g2.py`. 113/113 jobs `status-pass`:
- g2all 33/33;
- insregress 1/1, inssmoke 2/2, insmain 55/55, inselmi 22/22.

Outputs are in `C:/RideBoundData/research/via-bridge-man-v1/g2v4/`.

**Condition 3 (`compare_v4.py 2026-10-23` → `compare/v4-2026-10-23/condition3.json`, sha256 69553a28…):**
- (a) G2 against the emulator predictions sealed 2026-10-22T22:11Z: **33/33 cell-arms exact, 2994/2994 riders
  identical** (G2-U 11/11, G2-RCE35 11/11, G2-EOHZ35 11/11). This covers every rider row of the sealed predictions.
  `compare/v4-2026-10-23/g2all-compare.json` (sha256 875fef29…).
- (b) The 30 G2 jobs that were exact in v3 (old adapter) against their v4 reruns (G2 adapter): **30/30 identical,
  66,951/66,951 frames** (`compare_runs.py` rc 0; 64-hex hashes masked, so the inventory hash is masked). The adapter
  fix and the G2 adapter change nothing in them.
- (c) The 80 earlier insertion-only jobs (regress 1, smoke 2, main 55, elmi 22) against their v4 reruns: **80/80 pass,
  177,750/177,830 frames identical**.
  - In every job exactly one frame differs: frame 2 (`initializeRun`), only at `manifest.binarySha256` and
    `manifest.coreCommitSha` (A5).
  - These are provenance by construction: the archived jobs used the adapter repository `E:/Code/RideBound-deadline`
    (commit 1ce15e92) and the via-ref or elmi Runner builds; the reruns used the G2 worktree (7435661) and
    runner-g2-v3.
  - Every decision, route, acceptance and promise is identical.

**Moves and evidence (33 G2 jobs).**
- Moves counted by plan membership: G2-U 54, G2-RCE35 26, G2-EOHZ35 62, total 142. That equals the emulator's
  relocation count **in every one of the 33 jobs**, with at most 1 move per decision.
- 24,439 decisions, all evidence 1.3.0, 0 failures under the 1.3.0 verifier
  (`compare/g2v4-g2all-2026-10-23-moves.jsonl`, sha256 fbf76326…).

**Condition 4 (the record, as asked).**
- **First result:** 30/33 (A7). 30 exact, 2725/2725 riders; 3 stopped with `RBWP7_LOCKED_LEG_MISMATCH`.
- **Cause:** the emulator's move picks the rider up at the node where the receiver is serving another rider in the
  same step. With 0 s dwell FleetPy keeps that zero-duration service leg locked, and no plan with another first leg is
  accepted. In all 3 the rider being served boarded late (recorded by the Runner). Dwell is 0 on both sides
  (condition 1).
- **Adapter fix:** `fleet_control.py` `_fleetpy_plan` / `_in_progress_service_stop`, as described in A8.
- **Tests:**
  - `test_in_progress_service_leg.py`, 4 tests: failed before, pass after;
  - the late-boarding case fails against the first attempt;
  - 3 reproduction runs of the unchanged adapter fail identically.
- **Result after the fix:** 33/33 G2 exact against the emulator (2994/2994 riders, 142/142 moves); every earlier
  matching job unchanged (30/30 G2 frame-identical; 80/80 INS identical except the two provenance fields).
- **RideBound unchanged** (runner-g2-v3; tree 0b930c4f differs from v3 only in the adapter, its test and one C# test
  file). **Emulator unchanged** (engine.js 4d2aefa2…; predictions sealed 2026-10-22T22:11Z, untouched).
- Also recorded: the launcher error of A8. Before v4, every G2 job ran the adapter of `RideBound-deadline`, so the A7
  numbers were measured with the old adapter. The v4 numbers above use the G2 adapter, and (b) shows that the two
  adapters give identical frames on the 30 jobs that completed with both.
