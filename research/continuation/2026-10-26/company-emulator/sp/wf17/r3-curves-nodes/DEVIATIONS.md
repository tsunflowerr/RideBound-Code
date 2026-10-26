# wf17 r3-curves-nodes: deviations and pre-stated interpretations (wf17-exploratory-post-audit)

Checks: **R6** (policy curves over alpha and Shadow x) and **R7** (alternative node draw) of `wf17/a0-plan/RUNPLAN.md`.
Plan sha256 `ef953c3b87ec8231f78878606afcfc56a47e5931b090138191b6c19dc9da2f12`, verified against `RUNPLAN-HASH.txt` at
2026-10-23T16:32+07:00, before any code was copied or anything was run in this key. Timestamps are +07:00. Each entry is written
before the results it affects are read, unless it says otherwise.

## Setup

- **2026-10-23T16:35 (record). Code source.** All runner and driver files are byte copies of the r1 key's files (shared `engine17.js`
  sha256 `81d7b47e…` = wf15 engine + P1 + P2 + P3, first built and diff-checked by `wf17/r1-identity-fence-pad`; `shcore.js` is the r1
  v2 copy `29287ab3…` with the tolerance-aware SHF invariant; R6/R7 use tolerance 0 everywhere, so v1 and v2 behave identically here).
  `gate17.js` is copied from the r2 key. `analysis/lib17.js` is copied from the r1 key. Hashes in `CODE-HASH.txt`.
- **2026-10-23T16:36 (record). Panels.** `work/panels.json`: D30 (even indices of sorted D60) and D10 (FNV-1a + mulberry32, seed
  `wf17-a0|D10|2026-10-23`) recomputed from `wf14/work/days.json`; both equal the lists printed in RUNPLAN 1.3 and the r1 key's panels.
- **2026-10-23T16:36 (record). theta(x*) check (RUNPLAN R6).** theta from `--ref wf15/work/s3/ref_U_causal.json` (sha256 `ba8dabc3…`, equal
  to `frozen/shadow-params.json` reference.refSha256) at x* (INS 72, G2 71) is bitwise equal to the frozen theta in all 4 config x window
  cells (`work/theta_check.json`). R6 therefore runs with `--ref` only (no `--frozen`), with `--alphaS 0.15 --stride 0` (the frozen values),
  so every theta of R6, x* included, comes from the reference as RUNPLAN asks.
- **2026-10-23T16:36 (DEVIATION: free RAM).** RUNPLAN 1.5 asks for >= 4 GB free RAM before each job. At 16:32 the machine had 3.38 GB free,
  with no `node` process running (the r1 and r2 keys logged the same condition). The r1 key measured 0.97-1.05 GB peak RSS for whole 6-worker
  plain-identity jobs of this driver. Free RAM is re-checked and recorded before each job; worker count does not change any result.
- **2026-10-23T16:39 (record). R6 pre-run gates, all PASS** (`work/gates/GATES.ndjson`; 2 cells 2019-02-11 w08/w17 r0, both configs, all 27 arms
  run as gate + timing smoke, `logs/r6gate.log`): EOHZ/RCE at alpha 10/20/50/100 = a5 V7-V10 rows (8 gates, 4/4 each); EOHZ35/RCE35/U = wf14
  main r0 (4/4 each); SHHZ/SHRC at x*, U, EOHZ35, RCE35 = wf15 main (all equal). These are reproductions of seen rows. Outcome fields of the
  other arms in the gate file were not read. Timing: 4 tasks x 27 arms in 42 s wall with 4 workers (~1.6 s CPU per arm-run), peak RSS 686 MB.
- **2026-10-23T16:39 (DEVIATION, gate helper).** The first a5 gate pass reported every row as mismatched on the single field `nReq`, which is a
  field written by the a5 driver (number of requests of the cell) and does not exist in the run_sh/metrics_sh rows. `code/scripts/gate17r.js`
  = the r2 key's `gate17.js` + `--ignore k` and `--nreq-check` (the reference `nReq` must equal my `requests`). With nReq checked that way,
  all 8 a5 gates pass. No metric field differed.
- **2026-10-23T16:45 (pre-stated interpretation, before the R6 production run). R6 rules**, exactly as in the header of `analysis/r6_analyze.js`:
  - Unit = day = w08 + w17; share / rate metrics are per-day shares of that day's served riders; curves are 30-day means.
  - **The U arm is a reference point only**; the EOHZ curve is its 7 alpha points (RUNPLAN "sort the EOHZ points").
  - **Evaluable point:** >= 10 of 30 days inside the comparator's served range on that day. Excluded days are counted (below / above).
  - **Relative gap** = (sum over included days of Y_target) / (sum of the interpolated comparator Y) - 1.
  - **C9 reversals** (G2 primary; INS same rule, SECONDARY), 6 points alpha > 0: REVERSED if >= 3 points have mean Delta > 0; else
    WEAKENED if >= 3 points have |relative gap| <= 10 %; else HOLDS if >= 4 points have relative gap <= -20 % with >= 75 % of non-tied
    included days in favour; else INCONCLUSIVE. alpha = 0 points (RCE0 frozen ledger, EOHZ0) are reported, not labelled.
    Note: a gap between -10 % and -20 % at most points gives INCONCLUSIVE by these numeric rules (RUNPLAN 2.3: "none of the above");
    NOTE.md will then say in words whether the effect is smaller than stated.
  - **C9 P(change)** (RUNPLAN gives only WEAKENED): WEAKENED if >= 3 of the 6 points have mean Delta Pchg > +2 pp; else HOLDS if >= 4 points
    are evaluable; else INCONCLUSIVE. **C9 overall (G2)** = REVERSED if the G2 reversal label is REVERSED; WEAKENED if the G2 reversal or
    G2 Pchg label is WEAKENED; HOLDS if both HOLD; else INCONCLUSIVE.
  - **Elmi replication** (SHHZ vs EOHZ on Pchg; not a Rule C result): REPLICATED if more than half of the evaluable SHHZ points have mean
    Delta < 0 with >= 75 % of days in favour; NOT REPLICATED if more than half have mean Delta >= 0; else MIXED; INCONCLUSIVE if < 3 evaluable.
    The same counting is printed for SHRC vs SHHZ (reversals, Pchg) and RCE vs EOHZ deadline breach (descriptive prediction checks).
  - **breachDeadline60 caveat:** each arm is measured against its own budget (alpha or x), so the yardstick moves along the curve; U has no
    budget (breach 0 by definition). breachLedger is BY-CONSTRUCTION for RC arms and is never a result.
  - Bootstrap: 10,000 day resamples, mulberry32(FNV-1a('wf17-r6')), one index draw per resample shared across comparisons of a config.
  - Vocabulary for the summary: REVERSED -> flips; WEAKENED -> weakened; HOLDS -> holds; INCONCLUSIVE -> inconclusive.
  - Synthetic-row smoke tests of `r6_analyze.js` and `r6_plot.js` (`work/smoke/synth_r6.js`, random numbers, not simulation output) ran
    before the hash; they exercised the missing-window log and the bootstrap day drop. No R6 production output existed at that time.
- **2026-10-23T16:45 (pre-stated, before the R7 cell generation and production run). R7.**
  - Baseline = the R1 plain rows (RUNPLAN R7 "reused"): the 320 lines with `"variant":"plain"` of `wf17/r1-identity-fence-pad/results/r1.ndjson`
    (sha256 of that file `cadbf681…`) copied verbatim to `results/r7_baseline_plain.ndjson` (sha256 `fa4d7d1a…`). They were run with the
    frozen theta at x* (`--frozen --x star`); the R7 alt-node runs use the same flags.
  - `analysis/r7_analyze.js` = the r1 key's `r1_analyze.js` with the identity variants replaced by the node draws (plain vs `altnodes`);
    every rule, threshold and printed quantity is unchanged (RUNPLAN R7: "as R1, with 'alt nodes' in place of 'adapter identity'").
    Smoke-tested on the baseline rows plus a synthetic copy (served +-1, visRev60 +1; not simulation output).
  - `analysis/r7_cells_check.js`: G-GEN on the 20 D10 cells (default tags, byte-identical to wf14), alt-cell checks (same request ids,
    arrivals, zones, kappa, fleet 96; scenario validation) and the sanity rule (|median direct time change| < 10 % per cell).
  - The copied `gen17.js` keeps the r1 key's `derivedBy` provenance string (it names the r1 path); the file is byte-identical to the r1
    copy (sha256 `a7351933…`), so the default-tag G-GEN reproduces wf14 bytes. For non-default tags the string names the r1 copy; this is a
    provenance text only.
- **2026-10-23T16:45 (DEVIATION: free RAM before R6).** Free RAM 3.25 GB (< 4 GB), no other `node` process running. The R6 gate run measured 686 MB peak RSS with 4 workers and the r1 key ~1.0 GB with 6 workers for this driver, so R6 starts with 6 workers. Worker count does not change any result.
- **2026-10-23T16:57 (record). R6 job finished**: 120 tasks, 3240 rows, 0 errors; wall 11.1 min, CPU 4,913 s user + 76 s sys, peak RSS 1,174 MB, largest heartbeat gap 10.0 s (no suspend). No R6 result had been read at this point. `analysis/r6_analyze.js` and `analysis/r6_plot.js` are run next, unchanged (hashes as in ANALYSIS-HASH).
- **2026-10-23T16:57 (DEVIATION: post-run gate code bug; read after the R6 analysis ran).** The a5 gate inside the pre-hashed `r6_analyze.js` reports 0/960 equal because it compares the reference field `arm` ('EOHZ'/'RCE', a5 naming) with my arm key ('EOHZ10' etc.) instead of mapping it (the pre-run gate used `--map-arm-ref`). The script is left unchanged (hash valid). The a5 post-run gate is re-done with `code/scripts/gate17r.js` (arm mapped, nReq checked), one call per arm, results in `work/gates/GATES.ndjson`. The other post-run gates (wf14 360/360, wf15 600/600, row count 3240/3240 unique) passed inside the script.
- **2026-10-23T17:00 (record). R7 cells** (`work/r7_cells_check.json`; generation logs `logs/r7ggen.log`, `logs/r7gen.log`, 4 s each):
  G-GEN on all 20 D10 cells PASS (default tags: scenario JSON, matrix .bin and .prov.json sha256-identical to wf14, 20/20). Alt cells
  (`--nodetag wf17-r7-alt1`): scenario validation passes, same request ids, arrival times, zones, kappa and 96 fleet slots as wf14;
  96.1-99.2 % of origin nodes and 96.7-99.2 % of destination nodes changed; fleet start nodes are redrawn with the new origins.
  Sanity: median direct time changes by -3.9 % .. +4.4 % per cell (all 20 < 10 %).
- **2026-10-23T17:00 (record). R7 smoke** (one alt cell, 2019-02-11-w08-r0, G2 RCE35, frozen x*): 0 errors, 1.3 s per arm-run,
  peak RSS 286 MB with 1 worker. Outcome fields were not read. Free RAM 7.4 GB, no other `node` process: RUNPLAN 1.5 met for R7.
- **2026-10-23T17:01 (record). R7 job finished**: 40 tasks, 320 rows, 0 errors; wall 1.2 min, CPU 562 s user + 9 s sys, peak RSS 999 MB, largest heartbeat gap 10.0 s (no suspend). No R7 result had been read at this point. `analysis/r7_analyze.js` is run next, unchanged (hash as in ANALYSIS-HASH).

## After the results were read

- **2026-10-23T17:03 (record).** R6 analysis ran unchanged; labels as printed in `results/r6_tables.txt`. Post-run gates: wf14 main r0
  360/360, wf15 main 600/600, row count 3240/3240 unique (inside the script); a5 V7-V10 960/960 via `gate17r.js` (see the gate-bug entry above).
  R7 analysis ran unchanged; gates: plain rows = wf15/wf14 300/300, INS NCE35 rider-identical to RCE35 20/20 in both node draws.
  The R7 script's console summary printed nothing (its line filter expects single-line strings; the tables file is complete). Cosmetic only.
- **2026-10-23T17:03 (POST-HOC, descriptive only, decided AFTER reading the R6 tables).** `analysis/r6_posthoc_rates.js`
  (sha256 `3b74f78e…`) divides the printed per-day frontier means (RCE vs EOHZ at matched service) to give RUNPLAN 2.4-style exchange rates
  at MATCHED service (reversals avoided per extra deadline breach, per extra rider late > 60 s vs the first promise). Reason: the
  pre-specified 2.4 rates are at equal alpha, and the matched-service frontier shows a lateness price under G2 that the equal-alpha rates
  do not show. No new test, no label. Output `results/r6_posthoc_rates.txt`.
- **2026-10-23T17:03 (record).** PNG renders of the SVG plots (`plots/*.png`) were made with headless Chrome for viewing; the SVGs
  are the plot files written by the pre-hashed `r6_plot.js`.
