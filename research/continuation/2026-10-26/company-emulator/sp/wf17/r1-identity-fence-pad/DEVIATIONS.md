# wf17 r1-identity-fence-pad: deviations and interpretation notes (wf17-exploratory-post-audit)

Plan: `wf17/a0-plan/RUNPLAN.md`, sha256 `ef953c3b87ec8231f78878606afcfc56a47e5931b090138191b6c19dc9da2f12`. I verified it against
`RUNPLAN-HASH.txt` at 2026-10-23T14:30+07:00, before I wrote any code or ran anything. Each entry below has a timestamp (+07:00). Each
entry was written before the results it affects were read, unless the entry says otherwise.

## Setup (before any production run)

- **2026-10-23T14:36 (record, not a deviation). Choice of `--data` for the generator.**
  - `gen17.js` uses `--data SP/wf5/data/nyc-via/fleetpy-manhattan`, the second candidate listed in RUNPLAN 1.4.
  - The first candidate, `SP/wf6/data/manhattan-fleetpy`, has the same edges/nodes sha256 but no
    `FleetPy_Manhattan__zones__...general_info.csv`, so the generator stops (ENOENT).
  - The edges/nodes sha256 of the second candidate equal `provenance.sources.fleetpyNetwork` of the wf14 cells.
- **2026-10-23T14:40 (extra gate).** I also ran G-P2, the gate for the R4 friction patch. I did this because this key is the first to build
  `engine17.js`. It passed, 24/24 rows equal to the a5 V11/V12 rows (`work/gates/gp2.ndjson`).
- **2026-10-23T14:43 (definition). "Rider-identical" in G-R3d.**
  - The run fingerprint covers these per-rider fields: [rq, served, veh, t0, p0, pk0, A, pickup, revs]. It leaves out revs[i][9], the
    *shown* pickup ETA.
  - Reason: `clipPickup: true`, which CL and CLF use by design (as wf14 part S), changes only that shown value, and N runs have no clipping.
  - With this definition G-R3d passed 8/8. CLF used flatRatio 1 on 2019-09-19 w08/w17 r0, INS and G2, arms EOHZ35 and RCE35. The CLF runs
    had up to 333 clipped revisions.
- **2026-10-23T14:45 (smoke).** I smoke-tested the R3 driver and the R3 analysis on 2 seen F14 days (2019-09-19, 2019-12-05; 72 tasks;
  `work/smoke/`).
  - No F16 (fresh) cell was simulated in the smoke test.
  - After the smoke test I changed the analysis script in one way only: when a panel has no data, its label is now INCONCLUSIVE, where it
    used to treat NaN as a pass. No threshold or metric changed.
- **2026-10-23T14:48 (interpretation, pre-stated before the R3 production run). R3 decision labels.** RUNPLAN R3 gives only part of the
  rules, so I fixed the rest in the header of `analysis/r3_analyze.js`:
  - **Primary contrast** = INS RCE35, windows pooled, as in the R3 prediction. Labels for the other config × arm cells are descriptive.
  - **C6 open loop:** the plan defines only REVERSED and STRENGTHENED. I added:
    - HOLDS if mean (c) < 0 and ≥ 60 % of days are negative in both panels, and |mean| ≥ 1.0 pp in at least one panel. The 1.0 pp is the
      lower end of the predicted range −1 to −3 pp.
    - WEAKENED otherwise, that is, one panel fails, or |mean| < 1.0 pp in both panels. This is "effect smaller than stated / lost day
      consistency" in RUNPLAN 2.3.
  - **C6 closed loop:** the label is given per panel. The overall label is the common label when F14 and F16 agree; otherwise it is
    INCONCLUSIVE, and both panel labels are reported.
  - **R3-X day thresholds** are 11/16 and 8/16. For the 14-day F14 panel (seen, reported for comparison only) they scale to
    ceil(11·14/16) = 10 and ceil(8·14/16) = 7.
    - "Days in favour" counts strict improvements; ties do not count as in favour.
    - The C4 rule "difference ≤ 0" counts both ties and negative days.

- **Run order.** Within this key the order is R3 → R2 → R1, the same relative order as RUNPLAN 4.

## R3 (after the production run)

- **2026-10-23T15:00 (record).** The R3 production run finished: 1080 tasks, 3060 rows, 0 errors. Wall time was 11.1 min, CPU about
  5,836 s, peak RSS 963 MB, and the largest heartbeat gap was 10 s, so there was no suspend.
  - Post-run gates passed: the F14 N rows equal the wf14 main rows (756/756), and the F14 CL RCE35 rows equal the wf14 sys rows (168/168).
- **2026-10-23T15:02 (display artefact in the pre-hashed script; not fixed, reported).** For F16 G2 RCE35 vs NCE35,
  `rates()` prints `ER_deadline` = 1.07e17.
  - Cause: the mean ΔbreachDeadline60 is 0 up to floating-point error (+2e-16), so the `> 0` test passes.
  - NOTE.md reports this as "no deadline price (Δ = 0.00/day)". The script is left unchanged, so its hash stays valid.
- **2026-10-23T15:02 (record).** CLF counts as "equal time" in w08, where |ext_CLF − ext_CL| ≤ 25 % of ext_CL in all 8 panel × config × arm
  cells. It is NOT equal time in w17 (8/8): the flat ratio r_w17 ≈ 0.958 shortens promises about twice as much as CL does.
  - Per RUNPLAN R3, the w17 and pooled CLF contrasts are therefore descriptive only. The w08 CLF − CL contrast is the interpretable one.

## R2

- **2026-10-23T15:08 (DEVIATION: harness bug fixed; failed tasks re-run).** In the first R2 pass, every tolerance τ > 0 invocation lost most of its
  tasks (`logs/r2_prod.log`, `results/r2*.ndjson.errors.ndjson`). Errors per invocation:

  | Invocation | Failed tasks |
  |---|---|
  | r2a τ 1 s | 113/120 |
  | r2a τ 5 s | 120/120 |
  | r2a τ 60 s | 120/120 |
  | r2b τ 1 s | 36/40 |
  | r2b τ 5 s | 39/40 |

  - **Cause.** The copied wf15 T3 invariant in `shcore.js` asserts `p <= p0 + bSH || c == 0` for SHF. It does not know the P1
    tolerance, so a legal admission inside the tolerance band raised "SHF admission violated". The run then aborted. No
    simulation error was involved.
  - **Fix.** The assertion is now `p <= p0 + bSH + τ || c == 0` for strict-fence runs.
    - The simulation code is unchanged.
    - The original copy is kept as `work/shcore.v1.js`.
    - New `shcore.js` sha256: `29287ab3…`.
  - **Check.** I re-ran one task that had passed under the old code (2019-09-13-w08-r0 INS, τ 1 s) with the fixed code. Both rows were
    identical on every field (`work/smoke/r2_recheck.ndjson`).
  - **Re-run.** I re-run only the failed tasks, with resume and the same commands.
    - This departs from the rule "a failed cell is never replaced".
    - Justification: the failure is a deterministic assertion bug that fires on almost every τ > 0 task, whatever the outcome. Re-running
      therefore selects nothing on outcomes.
    - I had not read any τ > 0 result before this decision. The 7 + 4 + 1 rows that succeeded were not inspected.

## R1

- **2026-10-23T15:17 (record: smoke test and resources).**
  - Smoke test: one adapter cell, 2019-02-11-w08-r0, G2, RCE35, with scores on as in production. It took 18.4 s for one arm-run,
    adapter snapshot hashing included. Peak RSS was 1,265 MB with one worker, against 1,046 MB for a whole 6-worker R2 job.
  - Neither cut criterion was met (> 300 s per arm-run, or a heap overflow), so **the full 8-arm set is kept.**
  - Free RAM was 3.5 GB, so the adapter invocation runs with **3 workers** instead of 6. Each adapter worker builds a JSON string of about
    117 MB per snapshot bin.
  - The worker count does not change any result; every run is deterministic per task.
  - The plain invocation uses 6 workers.
- **2026-10-23T15:17 (record).** P (plain) and A (adapter) are two invocations run back to back into the same `results/r1.ndjson`. The
  identity is part of the resume key (`variant`).
- **2026-10-23T15:17 (record).** The R1 analysis was smoke-tested on synthetic rows: the G-ENG rows duplicated as `plain` and
  `adapter`, with served +1 in the copies (`work/smoke/r1_fake.ndjson`). This tested the code only. No R1 outcome was read.
- **2026-10-23T15:34 (record).** R1 finished: 640 rows and 0 errors.
  - Plain: 1.5 min wall, 6 workers, 972 MB peak RSS.
  - Adapter: 15.1 min wall, 3 workers, **3,165 MB peak RSS**. With 6 workers the adapter job would very likely have exceeded the free RAM.
  - The largest heartbeat gap was 10 s.
  - Post-run gates passed: plain rows equal wf15/wf14 (300/300), and INS NCE35 is rider-identical to RCE35 in both identities (20/20 each).
- **2026-10-23T15:34 (note on analysis output).** Two quirks in the pre-hashed scripts, both left as they are; NOTE.md reports the
  correct readings.
  - For share metrics, the DD sign tests in `r1_analyze.js` treat floating-point differences above 1e-9 as non-ties. They are reported
    but not used in any label.
  - The `fmtC` lines for share metrics print the A/B levels as fractions, not percentages. The mean Δ is in pp.
