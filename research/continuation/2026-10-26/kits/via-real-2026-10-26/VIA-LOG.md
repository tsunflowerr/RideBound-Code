# Via-zone bridge: real RideBound on Via data at Via scale (log)

Owner order 2026-10-26 ~00:45: "Chuẩn bị mọi thứ cho việc chạy bộ dữ liệu thật trên Via sau đó chạy 1 vài job ... tầm
1h15". Exploratory, post-audit. Not a confirmation. Times are local (UTC+7) unless marked Z.

## V1 — 00:50–01:07 — why zone level; converter; predictions sealed

- A real job materializes a complete directed graph over the scenario nodes and checks every arc with FleetPy's router
  before the first event (`actual_fleetpy_medium_preflight.py:113-155, :437-447`); each traffic bin sends a complete
  snapshot of all node pairs. Company Via cells have 824–858 nodes (≈680,000 arcs): not runnable in practice.
- TLC Via trips are recorded by taxi zone only; the company placed each trip end on a FleetPy node sampled inside the zone.
  `convert_via_zone.py` maps every node to one representative node per zone (zone medoid). Rules are in its docstring and
  were fixed before any prediction or job.
- Cells (fixed before predictions): the first P5 day, resample r0, both windows.

  | cell | source nodes | zones = nodes | arcs | closed matrix entries | requests | dropped intra-zone | vehicles |
  |---|---|---|---|---|---|---|---|
  | d20190213-vz-r0-w08 | 824 | 60 | 3,540 | 542 | 509 | 3 | 62 |
  | d20190213-vz-r0-w17 | 830 | 58 | 3,306 | 334 | 508 | 4 | 62 |

  The company matrix restricted to the representatives is not a shortest-path metric: the integer Floyd–Warshall closure
  made 542 / 334 updates, which shortened **537 / 331 distinct arcs** (erratum R1-4: the table column counts updates; the
  arc counts were recomputed against the source matrix). Both sides use the closed matrix.
  *Erratum 26/10 (ADR-086):* every shortened arc changed by only **1–2 ms** (max 2 ms, none ≥ 1,000 ms): rounding
  residue. The company matrix passes the company's triangle check, which counts a violation only at ≥ 1 s
  (`sp/wf15/runner/scenario.js:245-248`). The closure was needed only because the RideBound job checks every arc against
  FleetPy's shortest path to the millisecond. "Not a shortest-path metric" above means this ms-level residue, nothing more.
- Emulator predictions (`emulate_via.js`, wf15 engine copy of bridge phase 1): 12 cell-arms, 8 s in all, ≈495 decisions
  per job, served 362–464, G2 relocations 6–33 per job. Sealed in `VIA-PREDICTIONS.sha256` at 2026-10-25T18:06:24Z, before
  any RideBound job.
- Runner `runner-p1-dwell-v1` (dwell-off replay gate 228/228). Adapter `C:/Code/RideBound-p1fix` 6ab03fc: its
  `fleet_control.py` is byte-identical to the fix1 file (sha256 490db8b6…); the inventory changed from FIX1 4cd1c77b to
  4f4dacd3 only because docs/18-19 and the commit were added (`git diff e1c6f2b 6ab03fc`: docs/18, docs/19,
  fleet_control.py +62, the new test).
- Launcher `bridge_launcher_via.py` = fix1 launcher + process-local patch (3): Runner output line limit 64 MiB → 512 MiB.
  World wrapper `world-via/` = byte copies of `world-fix1/`; the explicit world base carries the cell's 16 TLC-MP15 factors.
- `run_via.py check`: all inputs OK. `VIA-READY.txt` at 18:07:09Z.

## V2 — 01:07–01:11 — trial job failed closed: decision-level work budget

- `VIA-RCE35-d20190213-vz-r0-w08` (insertion-only, bridged WP4 `wp4-tier3-v2-costfirst.json`): rc 1 after 4.1 min,
  peak 362 MiB, simulated time in bin 1 (900–1800 s). Runner answer:
  `INTERNAL_ERROR: Pre-solve counters must fit their stage budgets and candidate loss requires a stable digest.`
  Output kept: `E:/RideBoundData/research/via-real-v1/runs/trial/VIA-RCE35-d20190213-vz-r0-w08/` (failure-00.json,
  transcript 170 MiB), log `logs/trial/`.
- Cause (read in the code): the decision-level generation work is the SUM of every vehicle's exploration work plus G2
  relocation work (`SolverBackedRidePoolingPolicy.cs:520-524`); `CandidateSelectionPreSolveAccounting.Create` refuses any
  sum above `maximumGenerationWorkUnits` (`CandidateSelectionExecution.cs:99-112`). The bridged value 10,000 equals the
  PER-VEHICLE exploration cap, so it holds for 8–10 vehicles (232,103 bridge decisions never reached it) but not for 62.
- **Finding (to report):** with the bridged/default WP4 budgets, RideBound cannot run a Via-scale cell; it stops by design
  (fail closed) instead of silently dropping candidates.

## A1 — 01:13 — amendment before any rerun: Via WP4 budgets

- New configs `configs/wp4-via-ins-a1.json`, `configs/wp4-via-g2k8-a1.json` (`VIA-WP4-A1.sha256`): the bridged files with
  only four `solverExecution` values changed (diff = 4 lines each):
  `maximumGenerationWorkUnits` and `maximumValidationWorkUnits` 10,000 → 5,000,000; `maximumSolverWorkUnits`
  100,000 → 10,000,000; `maximumSolverDeterministicTimeMicros` 1,000,000 → 30,000,000.
- Unchanged: per-vehicle caps (100 candidates, 10,000 exploration units, 2 new requests), every rule, the predictions.
- Why this keeps the test meaningful: the emulator has no work budgets (exhaustive). The bridge matched because no budget
  ever bound. Raising only the decision-level sums keeps RideBound exhaustive where the per-vehicle caps do not bind; if a
  per-vehicle cap or the solver limit binds, the rider comparison will show it (and the solver evidence will say so).
- `run_via.py`: phases `trial`/`main` retired (refused); new phases `trialA1` (the same job) and `mainA1` (11 jobs) use the
  A1 configs; outputs under `runs/trialA1`, `runs/mainA1`. `VIA-READY-A1.txt` 18:14:35Z; trialA1 started 01:14:41.
- trialA1 passed the point where the first trial stopped (transcript 213 MB > 170 MiB at 01:19) and kept running.

## A2 — 01:45 — amendment: mainA1 starts before the trial comparison

- Measured speed: trialA1 reached simulated 4,007 s after 28 min (01:42), runner ≈ 1 core, 664 MiB. Expected ≈ 85 min per
  insertion-only job; the G2 jobs do about 3× the vehicle evaluations in the emulator (diagnostic `vehDecisions`), so
  they will be slower. Waiting for the trial comparison first would end the 11 main jobs after the owner leaves.
- Decision: start mainA1 now with 3 workers, before `TRIAL-REVIEWED.txt`, on a new gate file `MAINA1-EARLY-START.txt`.
  `run_via.py` keeps one instance per phase (separate lock file for mainA1); every admission still checks AC power,
  disk floors and the 3 GiB RAM floor.
- Why the gate's purpose still holds: the predictions were sealed before any job, so starting early cannot bias them;
  trialA1 has run 28 min without error past the point where the first trial stopped; the independent review found no
  blocker and the cap diagnostic shows no per-vehicle cap binding. Cost if the trial fails late: machine time only;
  every output is kept and labelled.
- The comparison is run on the trial first; main results are read only after it.

## V3 — 02:42 — trialA1: every decision ran, the final checkpoint failed

- `VIA-RCE35-d20190213-vz-r0-w08` (A1): rc 1 after 87.6 min (5,256 s); peak tree 1,712 MiB (dotnet 1,568 MiB, under the
  2 GiB GC heap cap of `run_via.py` JOB_ENV). The simulation went through the horizon and the drain (world bins to
  t = 9,000 s; transcript 1,011 MiB, last frame ordinal 8,781). The last two frames: adapter `checkpoint` request (ordinal 8780),
  Runner `error` INTERNAL_ERROR "Runner could not process the message safely." (8781).
- `RunnerHost.cs:96-106` answers every unexpected exception with that text and writes only the exception type to its
  stderr, which the adapter keeps in memory and did not save. Most likely cause: the managed heap needed for the final
  checkpoint exceeded the 2 GiB cap (the checkpoint was already 17.1 MiB at 108 requests). Not proven.
- Consequence: the rider comparison needs the final checkpoint (`bridge_riders.py:57-60`, the ledger is rebuilt from it),
  so trialA1 cannot be compared. Output kept (`runs/trialA1/`, failure-00.json, transcript).

## A3 — 02:44–02:55 — amendment: heap cap 6 GiB, stderr diagnostic, one ordered phase

- mainA1 (started under A2 with the same 2 GiB cap) was stopped at 02:44 so that its jobs would not fail the same way
  after 1.5–3 h: the three running job trees were killed (rc 15 after 60 min); the driver had admitted two more jobs,
  which were killed within 15 s, and a STOP file stopped further admissions (moved to `STOP.used-A3` afterwards).
  Every output folder and log is kept (`runs/mainA1/`, `logs/mainA1/`); none has a summary.
- Changes (nothing in RideBound, the adapter, the configs or the predictions):
  - `DOTNET_GCHeapHardLimit` 0x80000000 (2 GiB) → 0x180000000 (6 GiB) for the new phase (the bridge regress phase showed
    the GC cap changes no decision);
  - launcher `bridge_launcher_via_a3.py` = `bridge_launcher_via.py` + patch (4): on any Runner failure, print the Runner's
    captured stderr to the job log (diagnostic only; `VIA-KIT-A3.sha256`);
  - phase `runA3` runs all 12 jobs with the A1 WP4 configs, ordered RCE35, EOHZ35, G2K8-RCE35, G2K8-EOHZ35, U, G2K8-U
    (w08 first, then w17), 3 workers, admissions spaced 600 s so that final checkpoints do not coincide (commit limit
    20.5 GB, 9.7 GB free at 02:47).
- The trial rerun is the first runA3 job; the comparison reads the trial first.

## V4 — 04:08–07:50 — runA3: 3 of 7 finished jobs reached a final checkpoint; the others ran out of memory there

| job | rc | minutes | peak tree MiB (dotnet / python) | final checkpoint |
|---|---|---|---|---|
| VIA-RCE35 w08 | 1 | 81.8 | 4,833 (4,689 / 146) | Runner `OutOfMemoryException` |
| VIA-EOHZ35 w08 | 0 | 81.9 | 6,467 (5,268 / 2,370) | pass |
| VIA-G2K8-RCE35 w08 | 1 | 81.4 | 5,781 (5,637 / 165) | Runner `OutOfMemoryException` |
| VIA-G2K8-EOHZ35 w08 | 0 | 89.1 | 5,828 (5,397 / 2,499) | pass |
| VIA-U w08 | 1 | 117.4 | 5,248 (5,092 / 171) | Runner `OutOfMemoryException` |
| VIA-G2K8-U w08 | 0 | 110.6 | 7,155 (5,839 / 3,267) | pass |
| VIA-RCE35 w17 | 1 | 91.4 | 5,435 (5,285 / 151) | Runner `OutOfMemoryException` |

- The A3 diagnostic (4) shows the cause in every failed job's log: `Runner internal error: OutOfMemoryException`, on the
  final `checkpoint` request, after every decision. The failure is not about decisions; the rider comparison simply has
  no ledger for those jobs (`bridge_riders.py:57-60`).
- The passed checkpoints are single protocol lines of several hundred MiB (node cannot even hold the tail as one string;
  the adapter process grows to 2.4–3.3 GiB while receiving one). The 6 GiB GC cap is not always enough to build it.
  Which arms fail is not explained yet (RCE35 failed in both windows, EOHZ35 passed in w08).
- EOHZ35 w17, G2K8-RCE35 w17 and G2K8-EOHZ35 w17 were still running at 07:50.
- 07:47 the comparison of the 3 passed jobs started; at 07:50 it was SUSPENDED (not killed) at 5.8 GiB because free
  commit fell to 3.1 GB while a runner was about to build its checkpoint; resumed after that job ended.

## V5 — 07:47–07:54 — rider comparison: 3/3 exact, 1,127/1,127 riders

`compare_via.py compare/runA3-0746` (roots: runs/runA3; the 3 jobs that had passed when it started; seal checked):

| cell-arm | riders | identical | G2 moves real / emulator |
|---|---|---|---|
| VIA-EOHZ35 d20190213-vz-r0-w08 | 370 | 370 | — |
| VIA-G2K8-EOHZ35 d20190213-vz-r0-w08 | 377 | 377 | 17 / 17 (max 1 per decision) |
| VIA-G2K8-U d20190213-vz-r0-w08 | 380 | 380 | 27 / 27 (max 1 per decision) |

- Real RideBound equals the sealed emulator predictions rider for rider at Via scale (509 requests, 62 vehicles, 60
  nodes), including G2 with 8 receivers out of 62 vehicles, which the small bridge never tested.
- **Not covered:** rule C (RCE35). Every RCE35 job (w08 INS, w08 G2, w17 INS) ran all its decisions but ran out of memory
  on the final checkpoint, so its ledger cannot be read; nothing is known yet about RideBound = emulator for rule C at
  this scale. U (insertion-only) w08 also failed the same way.
- VIA-EOHZ35 w17 passed at 07:51 (after this comparison started); it is compared in a later run.

## V6 — 08:47–09:05 — runA3 finished; final comparison 6/6 exact, 2,407/2,407 riders

- Last jobs: VIA-EOHZ35 w17 pass (95.3 min, peak 5,742 MiB), VIA-G2K8-RCE35 w17 pass (105.7 min, 7,510 MiB),
  VIA-G2K8-EOHZ35 w17 pass (98.1 min, 6,683 MiB).
- runA3 ran 10 of its 12 jobs (`PROGRESS-VIA.txt`): **6 passed**; **4 failed** on the final checkpoint with Runner
  `OutOfMemoryException` (RCE35 w08, G2K8-RCE35 w08, U w08, RCE35 w17); **2 were never started**: VIA-U w17 and
  VIA-G2K8-U w17 were deferred by the disk rail ("C:\ free 29.8 GiB < floor 30 GiB"). All outputs go to E:; C: dipped
  during the run (page file peak 3.3 GB; 40 leftover FleetPy temp folders total only 565 KiB) and was back to 33.7 GiB
  at 09:00. Nothing was deleted.
- `compare_via.py compare/runA3-final` (all passed runA3 jobs; seal checked; 11.8 min):

| cell-arm | riders identical | G2 moves real / emulator |
|---|---|---|
| VIA-EOHZ35 w08 | 370 / 370 | — |
| VIA-EOHZ35 w17 | 425 / 425 | — |
| VIA-G2K8-EOHZ35 w08 | 377 / 377 | 17 / 17 |
| VIA-G2K8-EOHZ35 w17 | 425 / 425 | 19 / 19 |
| VIA-G2K8-RCE35 w17 | 430 / 430 | 8 / 8 |
| VIA-G2K8-U w08 | 380 / 380 | 27 / 27 |

- **6/6 cell-arms exact, 2,407/2,407 riders, 4/4 G2 move counts equal, at most 1 move per decision.**
- Coverage: Elmi relaxed fence (insertion-only and G2, both windows), rule C with G2 (w17), unconstrained with G2 (w08).
  Not covered: rule C insertion-only (w08 and w17 failed on the checkpoint), rule C with G2 in w08 (failed), U
  insertion-only (w08 failed, w17 not started), G2-U w17 (not started), and Shadow (not in RideBound).

## V7 — 26/10 afternoon — company-machine feedback (ADR-086)

- The company machine rebuilt the 6/6 comparison from this kit's TSV files and got the same result.
- Wording limit: the result is about the zone-level Via cells (`vz`), one day (2019-02-13). The company P512 cells
  themselves are not bridged.
- The A1 configs (`configs/wp4-via-*-a1.json`) carry `commitmentRecovery: no-worse-than-reference-v1`. They were used only
  for U, RCE35 and EOHZ35. Do NOT use them for EO35 (strict fence) at Via scale: EO35 needs `forced-reference-v1`, as in
  bridge phase 1 (`bridge-phase1-2026-10-23/configs/wp4-*-forced.json`).

## R1 — 01:20–01:30 — independent review (fresh-context agent, read-only) and follow-ups

- **No blocker.** The reviewer rebuilt both fixtures from the source cell and its int32 matrix and matched medoids, the
  closed matrix (0 arc mismatches), requests, max ride, payload hashes, fleet, sort orders and the driver hash; checked
  that `emulate_via.js` differs from `fixtures.js prepareFixtureCell` only in the traffic profile, which equals
  `World.factor`/`AnchoredWorld.factor` (sigma 0, bin 0 → 1, held after the last bin), and that the launcher patch (3)
  takes effect (`session.py:413`; the trial log shows `output-line-limit=536870912`).
- Findings and what was done:
  1. `compare_via.py` default roots pointed at the retired `trial`/`main` folders → fixed to `trialA1`/`mainA1`.
  2. The emulator does not cut at the per-vehicle exploration cap (diagnostic counter only, `engine.js:500-502`).
     Unsealed diagnostic `emulate_via_diag.js` → `predictions-diag/emulator-caps-diag.tsv`: in all 12 cell-arms
     `workCapHits` 0, `poolCapHits` 0, largest pool 32 (< 100); served counts equal the sealed predictions. So a later
     mismatch cannot be explained by those caps. Solver statuses will be counted after the jobs
     (`bridge-phase1-2026-10-23/count_solver_evidence.py`).
  3. `emulate_via.js` writes `d.forcedVehDecisions`, but the engine field is `forcedVehicleDecisions`: that column is blank
     in the sealed counts file. Not read by the comparison; the sealed file is left as is (the diagnostic file has it: 0).
  4. Closure counts: erratum above (537 / 331 arcs).
  5. The Runner gives the same message for three conditions (generation budget, validation budget, loss without digest;
     `CandidateSelectionExecution.cs:99-112`). A1 raised both budgets; which one was exceeded is not shown.
  6. `convert_via_zone.py` does not check the cell name's resample against the source id, and its daylight-saving check
     is written for 2019 only. Both cells were checked by hand (r0, February). Sealed file left as is.
  7. Usage line of `run_via.py` fixed; disk floors stated (all outputs on E:).
  8. `copy_kits.py` e-mail scan now accepts upper-case top-level domains; token patterns added. Dry run: 2,036 files,
     19.0 MiB, no hit.
  9. Citation erratum: the explicit world base is read at `preflight_world.py:112-115` and its keys are defined at
     :63-64, not :106-109 and :59-60 as the docstrings of `convert_via_zone.py` and `emulate_via.js` say (sealed files
     left as is).
