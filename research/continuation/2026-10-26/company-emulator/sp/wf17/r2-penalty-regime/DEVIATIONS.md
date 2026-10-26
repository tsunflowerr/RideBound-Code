# wf17 r2-penalty-regime: deviations and pre-stated interpretations (wf17-exploratory-post-audit)

Checks: **R4** (G2 friction) and **R5** (regime grid) of `wf17/a0-plan/RUNPLAN.md`.
Plan sha256 `ef953c3b87ec8231f78878606afcfc56a47e5931b090138191b6c19dc9da2f12`, verified against `RUNPLAN-HASH.txt` at
2026-10-23T15:36+07:00, before any code was copied or anything was run in this key. Timestamps are +07:00. Each entry is written
before the results it affects are read, unless it says otherwise.

## Setup

- **2026-10-23T15:38 (record). Code source.** `engine17.js` is copied from `wf17/r1-identity-fence-pad/code/runner/engine17.js`, the first
  wf17 build (sha256 `81d7b47e…`, recorded there). A `diff` against `wf15/runner/engine.js` shows exactly the patches P1, P2 and P3 of
  RUNPLAN 1.1 and nothing else. `scenario.js`, `identity.js`, `normalizer_emul.js`, `lib.js` and `metrics_sh.js` are copied from wf15
  (hashes equal the RUNPLAN prefixes). `gen17.js` and `cmp_rows.js` are copied from the r1 key (gen17 passed G-GEN there); `lib17.js`
  (statistics helpers) is copied from the r1 key's `analysis/`.
- **2026-10-23T15:39 (DEVIATION: free RAM).** RUNPLAN 1.5 asks for >= 4 GB free RAM before each job. At 15:39 the machine had
  3.58 GB free of 15.75 GB, with no `node` process running. The r1 key measured 963-1,046 MB peak RSS for whole 6-worker G2 jobs of
  this engine (R3, R2). I re-check free RAM before each job and record it here; worker count does not change any result
  (each run is deterministic per task).
- **2026-10-23T15:41 (record). R5 cells.** `gen17.js --set weekdays --fmax 512:96,256:56 --resample 0`, default tags, on the D30 days
  (`work/days_D30.json`), `--data SP/wf5/data/nyc-via/fleetpy-manhattan` (the r1 key found the first RUNPLAN candidate lacks the zones
  file), `--work SP/wf6/via-many-days/work`, `--windows SP/wf14/work/via_windows.csv`, `--factors SP/wf6/via-many-days/work/factors.json`.
  Output `cells/d30/`. Gate (RUNPLAN R5): the 60 regenerated P512 cells are sha256-identical to wf14 for scenario JSON, matrix `.bin`
  and `.prov.json` (180/180). P256 cells: 60/60 have 256 requests, all contained in the same cell's P512 request set (nested), and
  56 written vehicles (cut to 42 at run time). R5 P512 variants read the wf14 originals (identical bytes); V-P256 reads `cells/d30/weekdays-P256`.
- **2026-10-23T15:47 (record). Pre-run gates, all PASS** (`work/gates/GATES.ndjson`, `GATES.md`): G-ENG-sub R4 T8P0 (4 cells x 5 arms) vs wf14
  main G2 r0, 20/20; G-P2 T8P60 / T8P300 vs a5 V11 / V12, 12/12 and 12/12; G-ENG-sub R5 B0 (4 cells, INS 2 arms, G2 4 arms) vs wf14 main
  r0, 24/24; R5 V-R15 / V-R25 vs a5 V1 / V2 on 2 cells, 8/8 and 8/8. These gate rows are reproductions of wf14/a5 rows (seen data).
- **2026-10-23T15:48 (record). Smoke test** (timing and errors only; outcome fields not read): R4 T16P0/T16P300 and R5 V-P256/V-D24/V-F68 on
  2019-02-05-w17-r0. 0 errors; 0.2-3.1 s per arm-run; peak RSS 0.3-0.6 GB with 4 workers.
- **2026-10-23T15:50 (pre-stated interpretation, before the R4 production run).**
  - "Days in favour" in the C1 / C8 WEAKENED rule is the RUNPLAN 2.1 definition: share of NON-TIED days on the benefit side. The share
    over all days is printed next to it.
  - The C1 / C8 REVERSED rule "dVisRev60 >= 0 on >= half of the days" counts ties as unfavourable, over all days of the contrast. The
    check-specific rule is used for the label; the RUNPLAN 2.3 general version (sign p < 0.05 in the opposite direction) is printed too.
  - **C2 FAILS** (mean dServed < -8/day with >= 2/3 of days negative) is reported in words as FAILS. In the holds / weakened / flips
    vocabulary of the summary it maps to **weakened** (the service price is larger than the stated 0-8/day, not a change of sign).
  - Robust (RUNPLAN 2.3) = HOLDS in >= 80 % of the pre-specified configs/variants (R4: >= 5 of 6; R5: >= 6 of 7) and REVERSED in none.
  - Descriptive extras, fixed now and given no label: per-window relative reductions; difference-in-differences of the RCE35 - EOHZ35
    effect across friction (pi vs pi = 0 at the same T; T16 vs T8 at the same pi), with sign tests on DD_d; the EOHZ35 - EO35 and
    NCE35 - EOHZ35 contrasts.
  - Synthetic-row smoke test of `analysis/r4_analyze.js` (`work/smoke/synth.js`, random numbers, not simulation output) ran before the
    hash; it exercised the exclusion log (one dropped cell). No R4 production output existed at that time.
- **2026-10-23T15:51 (record). Free RAM before R4.** While I waited (`logs/memwait1.log`), a corporate endpoint agent (`fcagte`) held up to
  5.7 GB and free RAM dipped to 0.27 GB; it released memory within a minute. At the R4 start (15:52) free RAM was 7.28 GB and no other
  `node` process was running, so RUNPLAN 1.5 was met for R4 with 6 workers.
- **2026-10-23T15:52 (pre-stated interpretation, before the R5 production run and before any R4 result was read).** R5 labels exactly as
  in the header of `analysis/r5_analyze.js`:
  - C1 per variant uses the R4 C1 rule; C8 overall: REVERSED > WEAKENED > HOLDS; a variant above 60 % with none below the thresholds gives
    INCONCLUSIVE ("direction holds, size above the stated range").
  - C3 "on one side" = days whose INS dServed has the sign of the mean dServed. C4 "difference <= 0" counts ties and negative days.
  - The C2 threshold (-8/day) is applied literally in V-P256 too, although P256 serves about half as many riders; the % of EOHZ35 served is
    printed next to it.
  - DD vs B0 is descriptive. Synthetic-row smoke test of `r5_analyze.js` (random numbers) ran before the hash.
- **2026-10-23T16:13 (record). R4 job finished**: 720 tasks, 3,600 rows, 0 errors; wall 20.7 min, CPU 9,231 s, peak RSS 835 MB, largest
  heartbeat gap 10.1 s (no suspend). No R4 result had been read at this point.
- **2026-10-23T16:16 (DEVIATION: free RAM before R5).** Free RAM stayed at 3.05-3.65 GB for 3 minutes of polling (no `node` process
  running), below the RUNPLAN 1.5 threshold of 4 GB. R4 measured 835 MB peak RSS with 6 workers, and R5 runs are smaller or equal in size,
  so I started R5 with 6 workers anyway. The worker count does not change any result.

## After the R4 results were read

- **2026-10-23T16:17 (record).** R4 post-run gates PASS: T8P0 rows equal wf14 main G2 r0 (600/600); extra: T8P60 / T8P300 EO35/EOHZ35/RCE35
  rows on the D30 cells equal a5 V11 / V12 (180/180 each). `analysis/r4_analyze.js` ran unchanged (hash as in ANALYSIS-HASH); log empty
  (no missing cell, no excluded day).
- **2026-10-23T16:18 (POST-HOC addition, decided AFTER reading the R4 tables; descriptive only, no label).** The R4 levels show that the
  relocation penalty by itself lowers the fence's reversals (EOHZ35 39.7 -> 30.4/day from pi 0 to 300 s) while Rule C's level barely moves
  (23.1 -> 21.7). That raises a question RUNPLAN did not pre-specify: how much of Rule C's G2 reversal benefit could a plain relocation
  penalty buy, and at what service price? `analysis/posthoc_r4.js` computes paired day contrasts ACROSS configs on the same cells
  (EOHZ35 at pi vs EOHZ35 at pi 0; RCE35 at pi vs EOHZ35 at pi 0; U at pi vs U at pi 0). It is reported in NOTE.md as post hoc, after
  the pre-specified results, and changes no label.

## R5

- **2026-10-23T16:26 (record). R5 job finished**: 840 tasks, 2,520 rows, 0 errors; wall 9.5 min, CPU 4,408 s, peak RSS 909 MB, largest
  heartbeat gap 10.0 s (no suspend).
- **2026-10-23T16:27 (record).** R5 post-run gates PASS: B0 rows equal wf14 main r0 (360/360); V-R15 / V-R25 EOHZ35/RCE35 rows (INS + G2)
  equal a5 V1 / V2 (240/240 each); extra: B0 G2 rows equal the R4 T8P0 rows on the same D30 cells (240/240). `analysis/r5_analyze.js`
  ran unchanged (hash as in ANALYSIS-HASH); log empty (no missing cell, no excluded day).
- No other deviation. No arm, metric, day or variant was dropped; no failed cell; no re-run.
- **2026-10-23T16:28 (POST-HOC, record).** Added the count metrics breachDeadline60 and late60First to `analysis/posthoc_r4.js` so that the penalty-only exchange rates can be computed from saved output; the other lines of `results/r4_posthoc.txt` are unchanged.
