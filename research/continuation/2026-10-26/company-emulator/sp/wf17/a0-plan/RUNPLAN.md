# wf17 RUNPLAN: do the traded-for benefits hold up? (EXPLORATORY, POST-AUDIT)

Written 2026-10-23 by key `a0-plan`, BEFORE any wf17 simulation, cell generation or export. Nothing in this file has
been run yet. The hash of this file is in `RUNPLAN-HASH.txt` next to it.

`SP` = `E:/RideBoundData/company-intake/2026-10-23/sp`.

---

## 0. Status, scope, honesty rules

### 0.1 Status of every run in this plan
- Every run is **EXPLORATORY, POST-AUDIT (wf17)**. Every output row and every summary file must carry the label
  `wf17-exploratory-post-audit`.
- Nothing here is confirmatory. The confirmatory days (wf14, 60 days) have been seen many times, by wf14, wf15 and both
  wf16 auditors. Only the panel **F16** (§1.3) has never been simulated.
- No multiplicity correction (no Holm). p-values are descriptive. The plan reads **patterns across pre-specified
  variants**, not single p-values.

### 0.2 What simulation can and cannot answer
The user asks two things:
- **(a) Do the benefits hold up?** Robustness, magnitude and price. This plan answers it.
- **(b) Do they matter to riders?** Simulation cannot answer this. It can only express the benefit in absolute terms
  (per 1000 served riders, and as "exchange rates" against the riders lost and the deadline breaches added, §2.4). Whether
  a ≥60-s reversal matters to riders is a literature question (parallel wf17 research keys b1–b4) or a user-study question.
- **The "auditable per-rider commitment" is a definitional property.** The ledger bound Σ|c| ≤ B holds by construction,
  so no run can test it. What runs can test is how that promise fares on Elmi's yardstick (deadline breach), and every
  table must report it next to the reversal results.

### 0.3 Honesty rules
- Negative and null results get the same table, the same wording strength and the same position as positive ones.
- No arm, metric, day or variant is dropped after results are seen.
- A failed cell is logged and never replaced. A day missing any arm of a contrast is excluded from that contrast, and the
  exclusion is logged.
- Any deviation from this plan goes into `wf17/<key>/DEVIATIONS.md` with a timestamp, before the affected results are
  read where possible.
- **Analysis scripts are written and sha256-hashed** into `wf17/<key>/ANALYSIS-HASH.txt` before the check's first
  production run. Smoke tests and gates (§1.6) are allowed earlier. Results of a running job are not inspected to decide
  whether to extend it: there is no optional stopping, and no "add days if borderline".

### 0.4 Conclusions under test
They come from the audit: `SP/wf16/a5-steering/NOTE.md` §0, §4.8, §5; `SP/wf16/a3-code-emulator/NOTE.md` F1, F4, F6;
and the post-audit report.

| ID | Conclusion as currently stated | Source |
|---|---|---|
| C1 | Under reassignment (G2), RCE35 cuts ≥60-s reversals (visRev60) by 30–56 % vs the fence at the same per-rider budget, in every variant tried; visible churn (TV5) also falls | a5 §4.8, §5 |
| C2 | ... at a service cost of 0–8 riders/day (≤ 1 %); every G2 point estimate is negative | a5 §4.5, §5 |
| C3 | Under insertion-only (INS, RideBound's real mode) Rule C ≈ Elmi: reversals −3…−12 %, service ±4/day | a5 §4.8 |
| C4 | On Elmi's deadline yardstick the fence is better: breach 1.0–1.1 % vs 1.75–1.78 % | a5 §4.2 |
| C5 | wf15 "SHRC serves +29–34/day more than SHF" is mostly a strict-fence rounding artefact; EOHZ35/SHHZ are the fair comparators; EO35 is barely affected | a3 F1 |
| C6 | The wf14 system gain is the forecast. Its forecast-specific gain over an equal-time pad is modest: −2.88 pp in wf7 (Nov 2019, open loop); no pad control exists in wf14 | a5 §4.1; wf7 review B1 |
| C7 | The emulator results do not depend on the plain identity mode (B3) or on the node draw | a3 §1.4 (chaotic noise, same for every arm) — untested on effects |
| C8 | The direction of C1 does not depend on the regime selected; its size does (density ×7, relocation penalty ×1.8) | a5 §5 |
| C9 | (new, no prior claim) Over the whole α / x policy range, Rule C's operating curve lies below the fence's curve on reversals at matched service, without paying in P(change) | — |

---

## 1. Common setup

### 1.1 Code (copied, never modified in place)
Each check copies what it needs into `SP/wf17/<key>/code/` and records the sha256 of every copied or patched file in
`wf17/<key>/CODE-HASH.txt`. Sources and current hashes (verified 2026-10-23):

| File | sha256 (prefix) |
|---|---|
| `SP/wf15/runner/engine.js` (wf15 engine; ≡ wf13 engine in default mode, a3 T1d 2880/2880) | `908a2961…` |
| `SP/wf15/runner/scenario.js` (= wf13 copy, hash pinned in wf14 PREREG-HASH) | `d27d0bee…` |
| `SP/wf15/scripts/metrics_sh.js` (superset of wf14 `metrics()`; byte-identical for non-Shadow arms) | `f1ee54cc…` |
| `SP/wf15/scripts/run_sh.js`, `shcore.js` (+ `shlib.js`, `pool.js`, `shworker.js`, runner `sscore.js`, `lambda.js`, `netfull.js`) | `4a2e5c57…`, `ba2b9c78…` |
| `SP/wf14/scripts/gen_cells_rs.js` | `19fc7d24…` |
| `SP/wf13/scripts/forecast.js` | `bcd21f2d…` |
| `SP/wf7/c-forecast-review/scripts/pad.js` | `f183c534…` |
| `SP/wf16/a3-code-emulator/engine_tol.js` (source of patch P1) | `e38f3dd3…` |
| `SP/wf16/a5-steering/runner_copy/engine.js` (source of patch P2) | `b1fb6a08…` |

**Engine copy `engine17.js` = wf15 engine + three opt-in patches.** All defaults reproduce wf15. Every check that needs
a patched engine applies **all three** patches, so every copy has the same hash; the first check to build it records
that hash.
- **P1, fence tolerance** (a3's `engine_tol.js` edits, ported verbatim):
  - module variable `TOL`, set from `opts.fenceTolMs || 0` at the start of `simulate`;
  - `+ TOL` in admission cases 4 (DLS), 8 (EO) and 13 (SHF);
  - `+ TOL` in the forced-vehicle test (`const forced = …`, wf15 line 468);
  - optional `opts.audit` exceedance histogram (≤1 s / ≤5 s / ≤60 s / >60 s);
  - the `require` of `identity.js` stays relative.
- **P2, G2 friction** (a5 semantics, ported to the wf15 line numbers):
  - `+ (ro.g2PenaltyMs || 0)` on `const cost0 = …` (wf15 line 608);
  - `&& r.switches < (ro.g2MaxSwitches === undefined ? Infinity : ro.g2MaxSwitches)` in the G2 eligibility filter
    (wf15 line 520).
- **P3, closed-loop flat drift (R3 secondary arm CLF):** in `legF` (wf15 line 155),
  `const wt = b === fb0 ? 1 : (FC.flatRatio !== undefined ? FC.flatRatio : fprof[Math.min(b, fH)] / fl0);`.
  With `flatRatio = 1` the projection is `time + w`, so CLF(1) must be rider-identical to mode N (gate G-R3d).

**Metrics.** Every check uses `metrics_sh.metrics()` unchanged. For pDispUp1/pDispAny1 and the Shadow guaranteed-rider
fields the code is in `metrics_sh.js`. Any new quantity (early60 vs a shown promise, exchange rates) is computed in the
check's own analysis script, from per-rider dumps or row fields. It is never added to `metrics()`.

**Drivers.**
- Non-Shadow jobs (R3, R4, R5): a generic worker-pool driver modelled on `SP/wf16/a5-steering/scripts/robustness.js`
  (resumable on a full task key; `maxOldGenerationSizeMb` 600).
- Jobs with Shadow arms (R1, R2, R6, R7): a copy of the wf15 `run_sh.js` stack, extended only by:
  - `--fence-tol <ms>`: applies to EO*/SHF*/DLS* arms; it is part of the resume key and of the output row;
  - `--scen <dir>`: already supported.

  Shadow scores are causal, with `alphaS` 0.15 and stride 0 from `SP/wf15/frozen/shadow-params.json`. θ(x) for x ≠ x*
  comes from the discovery reference `SP/wf15/work/s3/ref_U_causal.json` (`--ref`), exactly as `SP/wf15/results/xsearch.txt`.

### 1.2 Configurations
- **Base world** (wf14 PREREG §1):
  - P512, 62 vehicles (cells hold 96; runs cut with `nVeh`), capacity 4;
  - max ride 2000 ‰ (ST), dwell 0, pickup window 600 s;
  - TLC-MP15 traffic, identity `plain`, snapshot rounding `round`.
- **INS:** `{}`.
- **G2:** `{ reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' }`. R4 varies `g2Targets` and `g2PenaltyMs`.
- **Arms** (`engine.js` `admits`): U; EO{α} (strict fence with forced vehicles); EOHZ{α} (fence with the Hoerl-Zwick floor);
  RCE{α} (Rule C ledger Σ|c| ≤ B_r); NCE{α} (net ledger with refunds); B_r = round(α·slk_r).
  Shadow: SHF{x}, SHHZ{x}, SHRC{x}, with x* = 72 (INS) / 71 (G2).
- **Note:** under INS, c ≥ 0, so NCE ≡ RCE. INS NCE35 runs only as a sanity gate.

### 1.3 Day panels (all computed before any run)
- **D60:** the 60 wf14 confirmatory weekdays (`SP/wf14/work/days.json`). Seen.
- **D30:** every second day of D60 in sorted order (index 0, 2, …). This is exactly the a5 robustness set, so a5 rows
  are reusable as gates. Seen.
  `2019-02-05 02-11 02-15 03-01 03-11 03-13 04-03 04-08 04-23 05-01 05-09 05-13 06-04 06-17 06-21 07-03 07-12 07-22 08-14 08-26 08-29 09-04 09-13 09-19 10-03 10-09 10-18 12-05 12-10 12-16`
- **D10:** seeded draw from D60, used by R1, R7 and R2b. Seen.
  - Method: FNV-1a + mulberry32, as wf14 `select_days.js`. Seed `wf17-a0|D10|2026-10-23`; Fisher–Yates shuffle of the
    sorted 60 days; first 10 kept, then sorted.
  - Result: `2019-02-11 03-11 05-09 06-17 06-26 07-12 10-03 10-07 10-15 12-18`.
- **F14:** the 14 forecast-eligible confirmatory days (`forecast.profile(day, s0, 'prev4', index)` non-null for both
  windows). These are exactly the wf14 system days. Seen.
  `2019-09-19 09-24 10-03 10-07 10-09 10-15 10-18 10-24 12-05 12-09 12-10 12-13 12-16 12-18`
- **F16:** the forecast-eligible weekdays among the 91 AVAILABLE, dqFail = false days that wf14 did not draw
  (`SP/wf12/r5-data-days/work/day_status.json` minus `SP/wf14/work/days.json`). **Never simulated; the only fresh panel.**
  `2019-09-20 09-26 09-30 10-02 10-08 10-10 10-11 10-16 10-17 10-23 10-25 10-28 10-29 12-04 12-17 12-19`
  - Unused August days are not eligible: prev4 needs ≥ 2 source days on or after 2019-09-05 (`forecast.js` `isSourceOk`).
  - Sensitivity: drop target days that are NYC-DOE non-school days in `forecast.js` EXCL (09-30 in F16, 10-09 in F14).
- **Resamples:** r0 only, except R3, which uses r0, r1, r2 (the wf14 day definition).

### 1.4 Cell generation (only where existing cells do not suffice)
Generator copy `gen17.js` = `gen_cells_rs.js` plus two options:
- `--seedtag` (default `wf14-confirm`) in the sample and fleet seeds;
- `--nodetag` (default `via-many-days`) in the node seed.

With defaults it must reproduce wf14 cells byte for byte (gate G-GEN).

Inputs:
- `--windows`: `SP/wf14/work/via_windows.csv` (covers D60); for F16, a new export (below);
- `--factors`: `SP/wf6/via-many-days/work/factors.json` (364 days, includes all F16 days);
- `--data`: the FleetPy Manhattan directory whose edges/nodes sha256 equals `provenance.sources.fleetpyNetwork` of a wf14
  cell (candidates: `SP/wf6/data/manhattan-fleetpy/`, `SP/wf5/data/nyc-via/fleetpy-manhattan/`);
- `--work`: `SP/wf6/via-many-days/work` (`ff_apsp_u16.bin`);
- `--set weekdays`, `--resample k`, `--fmax` as stated per check.

Output always goes under `SP/wf17/<key>/cells/`.

**F16 export.** `SP/wf6/via-many-days/scripts/export_windows.sql.tmpl` with:
- `__DATA__` = `SP/wf6/data/via-many-days`;
- `__DAYS__` = a days.csv of F16;
- `__OUT__` = `wf17/<key>/work`;
- DuckDB CLI `SP/tools/duckdb-v1.5.5-st/duckdb.exe`.

### 1.5 Resources
- At most 8 worker threads (default 6, as a5 and wf6), and one heavy job at a time machine-wide. Before each job, check
  that no other `node` simulation is running and that free RAM is ≥ 4 GB.
- `wallBudgetMs` is 900,000 per run.
- Record wall time, CPU time and peak RSS per job. If the machine suspends during a job (monitor gap > 120 s or any
  WALL_BUDGET error), the whole job is re-run into a separate file and compared record by record (the wf6 incident rule).

### 1.6 Gates (run before each check's production job; failure = stop that check and log it)
| Gate | Requirement |
|---|---|
| G-ENG | `engine17.js` with all knobs at default reproduces, on every metric field except timing, the `SP/wf15/results/confirm/main.ndjson` rows of 4 cells (2019-02-11 and 2019-10-03 × w08/w17 × r0). Arms U, EO35, EOHZ35, RCE35, SHF/SHHZ/SHRC at x*, in INS and G2. |
| G-P2 | With `g2PenaltyMs` 60000 / 300000 (targets 8), EO35/EOHZ35/RCE35 G2 rows on 2 D30 days equal a5 V11/V12 rows (`SP/wf16/a5-steering/results/robust.ndjson`) on all shared fields. |
| G-GEN | `gen17.js` with default tags regenerates 2019-02-11 w08/w17 r0. Scenario JSON, matrix `.bin` and `.prov.json` must be sha256-identical to `SP/wf14/scenarios/…`. |
| G-EXP | F16 export: per-day w08/w17 request counts equal `w08`/`w17` in `day_status.json`. If they differ, the definition difference is explained before any cell is generated. |
| G-PAD | The `pad.js` copy run on `SP/wf7/c-forecast-review/out/rerun_all_WY.json` reproduces the RC120 and U lines of `SP/wf7/c-forecast-review/out/pad_control.txt` exactly. |

---

## 2. Common analysis (all checks)

### 2.1 Unit, tests and effect sizes
- **Unit = day.** X_d = Σ over windows {w08, w17} of the mean over resamples of X.
  - r0-only checks: X_d = X_{w08,r0} + X_{w17,r0}.
  - R3: mean of r0, r1, r2, then summed over the two windows (as wf14 PREREG §3).
- **Paired difference** Δ_d = X_d(arm A) − X_d(arm B), computed on the same cells.
- **Test:** exact two-sided sign test (binomial ½, ties dropped). Report n+ / n− / ties and p.
- **Effect size**, for every contrast:
  - mean Δ with SE and a 95 % t-interval; median Δ;
  - for counts, relative change 1 − ΣX(A)/ΣX(B);
  - per-1000-served rates;
  - "days in favour" = share of non-tied days on the benefit side.
- **Share metrics** (late, early, P(change)) are per-day shares of that day's served riders. Differences are in pp, as
  in wf7 `pad.js`.
- **Diff-in-diff** for variant checks (R1, R7): DD_d = Δ_d(variant) − Δ_d(base), with a sign test on DD_d and its mean
  ± SE.

### 2.2 Fixed metric list (every table reports all of them)
| Metric | Definition |
|---|---|
| served | served riders |
| visRev60 | riders with two consecutive opposite-sign visible changes, each ≥ 60 s |
| sumTV5 / served | visible drop-ETA total variation with a 5-s deadband, in s per served rider |
| Pchg = pDisp / served | share of riders with ≥ 1 dispatch change c ≥ +5 s; the wf15 H4 "P(đổi do điều phối)" |
| pDispAny1 / served | share with \|c\| ≥ 1 s |
| late60First / served | lateness > 60 s vs the first promise |
| breachDeadline60 / served | A > p0 + B_r + 60 s (Elmi's yardstick) |
| breachLedger / served | **BY-CONSTRUCTION for RC arms**; reported, never counted as a result |
| relocations, switchedRiders | G2 only |

### 2.3 Decision labels
Each conclusion gets one label per check, by the numeric rules stated in that check:

| Label | Meaning |
|---|---|
| **HOLDS** | — |
| **WEAKENED** | effect smaller than stated, or lost day-consistency |
| **REVERSED** | opposite sign on ≥ half of the days, with a two-sided sign test p < 0.05 in that direction |
| **INCONCLUSIVE** | none of the above; reported as such, not as "no effect" |

A conclusion is called **robust** only if it HOLDS in ≥ 80 % of the pre-specified variants where it is tested and is
REVERSED in none.

### 2.4 "Does it matter" quantities (descriptive, no threshold)
All are computed for RCE35 vs EOHZ35, and also vs NCE35 where available:
- **ARR** = reversals avoided per 1000 served = −ΔvisRev60 / served(EOHZ35) × 1000.
- **ER_served** = reversals avoided per rider not served = ΔvisRev60 / Δserved. Only when Δserved < 0; otherwise
  "no service price".
- **ER_deadline** = reversals avoided per extra Elmi-deadline breach = −ΔvisRev60 / ΔbreachDeadline60. Only when Δ > 0.
- **ER_late** = reversals avoided per extra rider late > 60 s vs the first promise.

These are the numbers the research keys (b1–b4) need to weigh against evidence on how much riders mind ETA reversals.

### 2.5 Output layout (per check key)
- `cells/` (if generated), `results/*.ndjson` (one row per run with all knobs as fields), `dumps/` (R3 per-rider);
- `analysis/*.txt` (pre-hashed scripts);
- `NOTE.md`, `DEVIATIONS.md`, `CODE-HASH.txt`, `ANALYSIS-HASH.txt`.

Suggested keys, or as the orchestrator assigns: `r1-identity`, `r2-fence-tol`, `r3-forecast-pad`, `r4-g2-friction`,
`r5-regime`, `r6-curves`, `r7-nodes`.

---

## 3. Checks

### R1 — B3 identity: plain/round vs ridebound-fleetpy-adapter + fleetpy-seconds-half-even

**Tests:** C7, and indirectly C1, C3 and C5.

**Cells:** D10 × {w08, w17} × r0 = 20 cells.

**Variant A (adapter):** each wf14 cell JSON is transformed. Exactly four keys change; a JSON diff must show nothing else:
- `identity.mode` → `ridebound-fleetpy-adapter`;
- `travel.snapshotRounding` → `fleetpy-seconds-half-even`;
- `travel.base.matrixFile` and `provenance.provenanceFile` → absolute paths of the wf14 originals (read only).

Requests, nodes, matrices, fleet and traffic factors are unchanged, so this is equivalent to regenerating the cells with
the adapter identity. `scenario.js` validation must pass (uniform profile + half-even rounding). Output:
`wf17/<key>/cells/adapter/`.

**Variant P (plain):** the original wf14 files, re-run in the same job.

**Arms**, causal scores, frozen x*, base world:
- INS: U, EO35, EOHZ35, RCE35, NCE35 (sanity), SHF72, SHHZ72, SHRC72;
- G2: U, EO35, EOHZ35, RCE35, NCE35, SHF71, SHHZ71, SHRC71.

**Runs:** 20 cells × 2 identities × 2 configs × 8 arms = 640.

**Smoke test** (before production): one adapter cell, G2, RCE35. The adapter snapshot hash serializes about 685k arcs per
bin. If one arm-run takes > 300 s or a worker exceeds its heap, the arm set is cut to {EOHZ35, RCE35, NCE35, SHHZ, SHRC}
and the cut is logged as a deviation.

**Gates:**
- P rows equal the wf15 main rows (U, EO35, EOHZ35, RCE35, SH*) and the wf14 main rows (G2 NCE35) for these 20 cells,
  on every metric field.
- INS NCE35 is rider-identical to INS RCE35 in both identities.

**Metrics:**
- Primary:
  - G2 visRev60 and served, RCE35 − EOHZ35 and RCE35 − NCE35;
  - INS served and visRev60, RCE35 − EOHZ35.
- Secondary:
  - SHRC − SHHZ served;
  - SHRC − SHF served;
  - SHF forcedVehDecisions;
  - everything in §2.2.
- Descriptive chaos: per cell and arm, |served_A − served_P|, and the number of rider-identical cells.

**Analysis:** relative reduction and Δ in each identity on the same 10 days; DD per §2.1.

**Prediction:**
- Per-cell chaos appears: served differs between identities by a few riders per cell, and few or no cells are
  rider-identical (a3 §1.4: Manhattan −1…+4 per cell, 0/20 identical).
- Effects carry over:
  - the G2 visRev60 relative reduction (RCE35 vs EOHZ35) in A is within ±10 pp of P;
  - the mean DD of served is within ±3/day for G2 and INS;
  - the INS reversal change stays small (≤ 25 % relative).
- The SHF − SHHZ gap may change under half-even rounding. No direction is predicted.

**Flip criteria:**
- C7 is **WEAKENED** if:
  - |relative reduction_A − relative reduction_P| > 15 pp; or
  - |mean DD served| > 5/day with ≥ 8/10 days of the same DD sign.
- C7 is **REVERSED** (for C1) if, in A, G2 ΔvisRev60 (RCE35 − EOHZ35) ≥ 0 on ≥ 5/10 days.
- **C5 note:** if the SHF − SHHZ served gap changes by > 50 % between identities, the rounding artefact depends on the
  rounding mode. This is reported as a finding about C5.

### R2 — strict-fence rounding artefact: tolerance 0 / 1 / 5 / 60 s

**Tests:** C5. It extends a3 F1, which ran random allocation (π = 0.68) on 8 discovery cells, to causal scores at the
frozen x* on confirmatory cells.

**Engine:** `engine17.js` (P1); driver `run_sh` copy with `--fence-tol`.

**R2a, real traffic.** D30 × {w08, w17} × r0 = 60 cells, base world.

| Config | Tolerance-varied arms, each at τ ∈ {0, 1000, 5000, 60000} ms | Arms with no τ |
|---|---|---|
| INS | SHF72, EO35 | SHHZ72, SHRC72, EOHZ35, RCE35 |
| G2 | SHF71, EO35 | SHHZ71, SHRC71, EOHZ35, RCE35 |

`audit: true` is on for the SHF/EO runs. Runs: 60 × 2 × 12 = 1440.

**R2b, Elmi's constant-time world** (as a3 `fence_flat.js` and a5 V6).
- Cells: D10 × {w08, w17} × r0 = 20 cells. File copies set every `travel.profile.factors[b]` to `factors[0]`; matrix and
  provenance are referenced by absolute path. The Shadow scorer then also sees the flat profile.
- Arms per config: SHF{x*} at τ {0, 1000, 5000}; SHHZ{x*}; SHRC{x*}; EO35 at τ {0, 5000}; EOHZ35; RCE35.
- Runs: 20 × 2 × 9 = 360.

**Gate:** τ = 0 rows of R2a equal the wf15 main rows for the 60 cells (SHF, SHHZ, SHRC, EO35, EOHZ35, RCE35) on every
metric field.

**Metrics:**
- Primary: served.
- Also:
  - forcedVehDecisions with its exceedance histogram;
  - for Shadow arms: G_n, G_dispAny1, G_dispUp5, G_late60First (guaranteed riders late vs p0, Elmi's sense);
  - everything in §2.2.

**Quantities:**
- Gap(τ) = served(SHRC) − served(SHF_τ) per day.
- Remaining share ρ(τ) = mean Gap(τ) / mean Gap(0).
- The same with SHHZ in place of SHRC.
- EO effect: served(EO35_τ) − served(EO35_0).

**Prediction** (a3 discovery numbers):
- INS: ρ(1 s) ≈ 0.65, ρ(5 s) ≈ 0.37.
- G2: ρ(1 s) ≈ ρ(5 s) ≈ 0.62.
- ρ(60 s) ≤ 0: SHF with a 60-s tolerance serves at least as many riders as SHRC.
- EO35: |Δserved(τ)| ≤ 1.5/day for every τ.
- R2b: SHF(τ = 5 s) within ±2/day of SHHZ, and SHF(τ = 0) clearly below (a3: up to 37/cell).

**Flip criteria:**
- C5 "mostly artefact":
  - **HOLDS** if ρ(5 s) ≤ 0.4 in INS and ≤ 0.7 in G2;
  - **WEAKENED** if ρ(5 s) > 0.6 in both configs;
  - **REVERSED** if ρ(5 s) ≥ 0.9 in both configs **and** R2b shows SHF(τ = 0) within 2/day of SHHZ.
- "EO35 barely affected, wf14 EO35 results stand" is **WEAKENED** if |served(EO35_5s) − served(EO35_0)| > 3/day with
  ≥ 2/3 of days on one side.
- "SHHZ is a fair primary comparator" is supported if SHF(τ = 5 s) serves within ±3/day of SHHZ. If SHF(τ = 5 s) is still
  > 8/day below SHHZ, the remaining loss belongs to our forced-vehicle formalization under traffic. That is reported as
  "real in our model, not in Elmi's".

### R3 — forecast vs equal-time pad, plus a fresh-day replication

**Tests:** C6. A by-product, R3-X, replicates C1–C4 on F16.

**Panels:** F14 (seen; 14 days × 2 windows × r0/r1/r2 = 84 cells, from wf14) and F16 (fresh; 16 × 2 × 3 = 96 cells).

**F16 cells:**
1. Export (§1.4), then gate G-EXP.
2. `gen17.js --seedtag wf17-fresh --fmax 512:96 --resample k` for k = 0, 1, 2 (new sample and fleet seeds; node seed
   unchanged).

**Forecast:** `forecast.js` `profile(day, s0 = 32 | 68, 'prev4', index, 15)` with index
`SP/wf7/c-forecast/results/accuracy-index-2019.json`. Closed loop: `opts.forecast = { prof, clipPickup: true }`, as wf14.

**Runs per cell**, configs INS and G2:

| Mode | INS arms | G2 arms |
|---|---|---|
| N (naive) | U, EO35, EOHZ35, RCE35 | U, EO35, EOHZ35, RCE35, NCE35 |
| CL (closed-loop prev4) | EOHZ35, RCE35 | EOHZ35, RCE35 |
| CLF (secondary, P3) | EOHZ35, RCE35 | EOHZ35, RCE35 |

- CLF uses `flatRatio = r_w`. r_w is computed per panel × window, outcome-free, from the panel's own prev4 profiles:
  r_w = exp(mean over days d, b0 ∈ {0..7}, b ∈ {b0+1..b0+4} of ln(prof_d[b] / prof_d[b0])).
  It uses no outcomes and no test-day traffic. It keeps the average drift of the window but drops the day-specific shape
  of the forecast.
- Per-rider dumps (served riders: t0, p0, A, pLast) for N {U, EOHZ35, RCE35}, CL and CLF.
- Runs: 180 cells × 17 = 3060.

**Promise variants, computed post hoc on the N runs.** Every k uses promises only (wf7 definition) and is computed per
panel × window × config × arm:

| Variant | Promise | Note |
|---|---|---|
| naive | p0 | — |
| display | D0 = `forecast.display(t0, p0, prof)` | open-loop, one-sided |
| pad(k_w) | p0 + k_w (p0 − t0) | k_w = Σ(D0 − p0) / Σ(p0 − t0); this is exactly the wf7 B1 pad |
| pad(k_cell) | as pad(k_w), with k per cell | secondary |
| padCL | p0 + k^CL_w (p0 − t0) | k^CL_w = mean_CL(p0 − t0) / mean_N(p0 − t0) − 1; may be negative in w17. The equal-mean-length uniform pad of the closed loop |

**Metrics** (shares of served, pp; per window and pooled):
- Y = late > 60 s vs the shown first promise;
- E = early > 60 s vs the shown first promise;
- served; visRev60; sumTV5 / served; late60Last.
- For CLF: achieved mean (p0 − t0) relative to CL. If it differs by > 25 % of CL's extension, CLF is labelled "not equal
  time" and is reported only descriptively.

**Contrasts** (per config × arm × panel, and per window):

| Label | Contrast | Role |
|---|---|---|
| (a) | display − naive | — |
| (b) | pad(k_w) − naive | — |
| **(c)** | **display − pad(k_w)** | **primary open-loop** (= wf7 B1) |
| (d) | CL − naive | served differs; lateness vs each run's own first promise |
| **(e)** | **CL − padCL** | **primary closed-loop** |
| (f) | CLF − CL | secondary |

**Gates:**
- G-PAD.
- N rows on F14 equal the wf14 main rows (U, EO35, EOHZ35, RCE35; G2 NCE35) for all 84 cells.
- CL RCE35 rows equal the wf14 sys rows (84 cells × 2 configs).
- CLF with `flatRatio = 1` is rider-identical to N on 2 cells (G-R3d).
- The F16 generator gate G-GEN passes for the default seedtag.

**Prediction:**
- **(c)**, INS RCE35, pooled:
  - −1.0 to −3.0 pp (wf7: −2.88 pp, RC120 1.5x Nov);
  - ≥ 10/14 days negative on F14 and ≥ 11/16 on F16;
  - essentially all of it in w08; w17 ≈ 0, because display is one-sided and k_w17 ≈ 0.2 %.
  - E (early arrivals): display < pad.
  - G2 and EOHZ35: the same direction.
- **(b):** −3 to −6 pp, i.e. the pad alone gets most of the naive→display gain (wf7: about 68 %).
- **(e):** w08 between −2 and +1 pp; w17 between +1 and +4 pp, because the closed loop raises w17 lateness (wf7 B2);
  pooled 0 ± 1.5 pp.
- **(d):** large and negative in w08. a5: the forecast-only part of wf14 H5 = −29.45 late/day.

**Flip criteria:**
- C6, open loop, "a modest forecast-specific gain exists":
  - **REVERSED** if mean (c) ≥ 0 or < 60 % of days are negative, in both panels;
  - **STRENGTHENED** if (c) ≤ −4 pp with ≥ 80 % of days negative in both panels.
- C6, closed loop, "the wf14 system lateness gain is mostly promise length":
  - **HOLDS** if |mean (e)| ≤ 1.5 pp pooled in both panels;
  - **WEAKENED** if (e) ≤ −2 pp with ≥ 75 % of days negative (the closed-loop forecast beats an equal-length pad);
  - the claim is **strengthened** (the forecast is worse than padding) if (e) ≥ +1.5 pp with ≥ 75 % of days positive.

**R3-X, fresh-day replication.** From the F16 N rows; these are the only never-seen data in wf17.

| Contrast | Prediction (wf14/wf15 confirm) | Flip criterion |
|---|---|---|
| G2 visRev60, RCE35 − EOHZ35 | −30 to −50 %, ≥ 13/16 days | C1 **WEAKENED** if < 20 % or < 11/16 days in favour; **REVERSED** if ≥ 8/16 days unfavourable |
| G2 visRev60, RCE35 − NCE35 and RCE35 − EO35 | same direction as above | — |
| G2 served, RCE35 − EOHZ35 | −1 to −5/day | C2 **WEAKENED** if mean < −8/day with ≥ 11/16 days negative |
| INS served and visRev60, RCE35 − EOHZ35 | served within ±4/day; visRev60 −0 to −15 % | C3 **WEAKENED** if \|Δserved\| > 8/day with ≥ 11/16 days on one side |
| breachDeadline60, RCE35 − EOHZ35 | +2 to +7/day, INS and G2 | C4 **REVERSED** if the difference is ≤ 0 on ≥ 8/16 days |

### R4 — G2 friction: relocation penalty 0 / 60 / 300 s × g2Targets 8 / 16

**Tests:** C1 and C2 under realistic relocation friction. This is the most important check: C1 is the surviving claim.

**Cells:** D60 × {w08, w17} × r0 = 120 cells, base world.

**Engine:** `engine17.js` (P2).

**Configs:** G2 with `reopt: { g2: 99, g2Targets: T, g2PenaltyMs: π }`, `lock: 'ratchet'`, for T ∈ {8, 16} and
π ∈ {0, 60000, 300000}: 6 configs.

**Arms:** U, EO35, EOHZ35, RCE35, NCE35.

**Runs:** 120 × 6 × 5 = 3600.

**Gates:**
- (T 8, π 0) rows equal the wf14 main G2 r0 rows for all 5 arms on all 120 cells.
- G-P2.

**Metrics:**
- Primary: visRev60 and served for RCE35 − EOHZ35 and RCE35 − NCE35.
- Secondary: RCE35 − EO35; relative reductions; sumTV5 / served; Pchg; late60First; breachDeadline60; relocations;
  switchedRiders; §2.4 exchange rates.

**Prediction:**

| Config | RCE35 vs EOHZ35, visRev60 | Served vs EOHZ35 | Source |
|---|---|---|---|
| T8, π0 | −16 to −18/day (−40 to −44 %), 60/60 days | −1.3 to −4.8/day | a5 V0; wf14 H1 |
| T8, π60 | −15/day (≈ −39 %) | ≈ −4/day | a5 V11 |
| T8, π300 | −10/day (≈ −30 %) | ≈ −6.5/day (a5: 7/23 days) | a5 V12 |
| T16, all π | no prior data. Larger absolute cut than T8 at the same π, relative 35–50 %; shrinks with π as at T8 | −2 to −8/day | — |

- vs NCE35: visRev60 about −40 %, served about −5/day (wf14 secondary −5.2/day).

**Flip criteria**, per config:
- C1 is **WEAKENED** if the relative reduction vs EOHZ35 is < 20 % or < 75 % of days are in favour.
- C1 is **REVERSED** if ΔvisRev60 ≥ 0 on ≥ half of the days.
- C2 ("small service cost, ≤ 1 %") **FAILS** in a config if the mean Δserved vs EOHZ35 is below −8/day (about −1 % of
  ~740) with ≥ 2/3 of days negative.
- The π = 300 s configs decide whether C2 survives under realistic friction. If C2 fails at π = 300 s for both T, the
  thesis must state the service price as "about 1 % under realistic relocation friction".

### R5 — regime grid

**Tests:** C8, and C1/C2/C3 per regime.

**Cells:** D30 × {w08, w17} × r0 = 60 per variant.

**P256 cells:** `gen17.js --fmax 512:96,256:56 --resample 0` for the D30 days, with default tags. P256 = the first 256 of
the same shuffle as P512 (nested; wf6 design), with 42 vehicles (the wf6 calibrated P256 fleet, 79.2 % U-served rule).
- **Gate:** the regenerated P512 files are byte-identical to wf14.
- **Note:** a5 V5 used a hash subsample with 31 vehicles. This is a different design, so it is not a gate.

**Variants** (one factor at a time from B0 = P512, 62 vehicles, 2.0x, dwell 0):

| Variant | Change |
|---|---|
| B0 | base |
| V-P256 | P256 cells, 42 vehicles |
| V-F56 | 56 vehicles |
| V-F68 | 68 vehicles |
| V-R15 | max ride 1500 ‰ |
| V-R25 | max ride 2500 ‰ |
| V-D24 | dwell 24 s per stop |

Fleets are nested: the first nVeh by id.

**Arms:** G2: U, EOHZ35, RCE35, NCE35. INS: EOHZ35, RCE35.

**Runs:** 7 × 60 × 6 = 2520.

**Gates:**
- B0 rows equal the wf14 r0 rows.
- V-R15 and V-R25 EOHZ35/RCE35 rows equal a5 V1/V2 rows.

**Metrics:** §2.2 and §2.4. Primary: G2 visRev60 and served, RCE35 − EOHZ35 and RCE35 − NCE35; INS served and visRev60.
Rates per 1000 served are always reported, because density changes the scale.

**Prediction** (a5 V0–V5):

| Variant | G2 relative reversal reduction (RCE35 vs EOHZ35) | Note |
|---|---|---|
| V-R15 | ≈ 40 % (absolute ≈ −7/day) | — |
| V-R25 | ≈ 39 % (≈ −26/day) | — |
| V-F56 | ≈ 36–42 % | — |
| V-F68 | ≈ 42–47 % | — |
| V-P256 | 25–40 %, absolute −3 to −6/day (smaller per day; similar per 1000 served) | — |
| V-D24 | 30–45 % | no prior data |

- G2 served vs EOHZ35: between 0 and −5/day in every variant.
- INS:
  - |Δserved| ≤ 4/day;
  - ΔvisRev60 between −0.2 and −2.5/day (relative 0 to −25 %);
  - ΔbreachDeadline60 > 0 (Rule C worse on Elmi's yardstick) in every variant.

**Flip criteria:**
- C8 **HOLDS** if the G2 relative reduction lies in [20 %, 60 %] with ≥ 75 % of days in favour in all 7 variants.
- C8 is **WEAKENED** if any variant falls below 20 % or below 75 % of days in favour. The variant is named, and "regime
  dependent" goes into the conclusion.
- C8 is **REVERSED** if any variant has ΔvisRev60 ≥ 0 on ≥ half of the days.
- C2: as R4, per variant.
- C3 is **WEAKENED** if any variant has |INS Δserved| > 8/day with ≥ 2/3 of days on one side.
- C4 is **REVERSED** in a variant if INS ΔbreachDeadline60 ≤ 0 on ≥ half of the days.

### R6 — policy curves (α and Shadow x)

**Tests:** C9. This is new: the thesis so far compares single points at α = 35 % and x*.

**Cells:** D30 × {w08, w17} × r0 = 60 cells, base world, INS and G2.

**Arms** per config:
- EOHZ{0, 10, 20, 35, 50, 75, 100};
- RCE{0, 10, 20, 35, 50, 75, 100};
- SHHZ{50, 60, 70, 80, 90, x*};
- SHRC{50, 60, 70, 80, 90, x*};
- U.

θ(x) for every x, including x*, comes from `--ref SP/wf15/work/s3/ref_U_causal.json`. It must equal the frozen θ at x*,
and that equality is checked. RCE0 is the frozen ledger (no dispatch change); EOHZ0 is p ≤ max(p0, e).

**Runs:** 60 × 2 × 27 = 3240.

**Gates:**
- EOHZ/RCE at α ∈ {10, 20, 50, 100} equal a5 V7–V10 rows, and at α = 35 equal the wf14 rows.
- SHHZ/SHRC at x* equal the wf15 main rows.

**Curves:** Y against served for Y ∈ {Pchg (primary), visRev60 per 1000 served, breachDeadline60 per 1000 served}; also
sumTV5 per served and pDispAny1.

**Frontier comparison, primary, per day:**
1. Sort the EOHZ points by served (ties averaged) and interpolate Y linearly.
2. For each RCE α point, compute Δ_d(α) = Y_RCE(α) − Y_EOHZ(served_RCE(α)).
3. Points outside that day's EOHZ served range are excluded and counted.
4. Sign test and mean ± SE across days for each α.

The same is done for SHRC vs SHHZ (over x) and for SHHZ vs EOHZ (Elmi's own claim that Shadow dominates Ordinary).

**Frontier comparison, secondary, pooled:** curves of the 30-day means, and the vertical gap at matched service with a
day-bootstrap 95 % CI (10,000 resamples, seed `wf17-r6`).

**Prediction:**

| Config | Comparison | Predicted gap at matched service |
|---|---|---|
| G2 | RCE vs EOHZ, visRev60 | 30–55 % lower for α ≥ 10 |
| G2 | RCE vs EOHZ, breachDeadline60 | higher, +0.3 to +1 pp |
| G2 | RCE vs EOHZ, Pchg | within ±1.5 pp |
| INS | RCE vs EOHZ, reversals | 0 to −25 % |
| INS | RCE vs EOHZ, Pchg | within ±1 pp |
| INS | RCE vs EOHZ, deadline breach | higher |
| both | SHHZ vs EOHZ, Pchg | lower (wf15: −10.9 pp INS at x*) |
| both | SHRC vs SHHZ, reversals and Pchg | lower |

At equal x, SHRC serves less than SHHZ (wf15 S10: −9.5/day G2).

**Flip criteria:**
- C9 (G2 reversals) **HOLDS** if the gap is ≤ −20 % at ≥ 4 of the 6 points with α > 0, each with ≥ 75 % of days in favour.
- C9 is **WEAKENED** if the gap lies within ±10 % at ≥ half of those points.
- C9 is **REVERSED** if RCE is above EOHZ (more reversals) at ≥ half of those points.
- "Rule C costs nothing in Elmi's P(change)" is **WEAKENED** if RCE's Pchg exceeds EOHZ's by > 2 pp at matched service at
  ≥ half of the points.
- Elmi's Shadow > Ordinary on Pchg is reported as a replication or a non-replication of Elmi. It is not a Rule C result.

### R7 — node placement (alternative node draw)

**Tests:** C7. The task cites Elmi et al. 2026 footnote 12 for node-placement sensitivity; this plan did not re-read it.

**Cells:** `gen17.js --nodetag wf17-r7-alt1 --fmax 512:96 --resample 0` for D10, giving 20 cells.
- The request sample stays the same, because the sample seed does not change.
- Origin and destination nodes are redrawn within the same taxi zones. The fleet nodes follow the new origins, and the
  matrices are rebuilt.
- **Gate:** with the default nodetag the same command reproduces the 20 wf14 D10 r0 cells byte for byte (G-GEN on 20
  cells).
- **Sanity:** the median direct time per cell changes by less than 10 %. If not, it is reported and the analysis still runs.

**Arms:** as R1 (8 per config, Shadow causal at the frozen x*).

**Runs:** 20 × 2 × 8 = 320. The baseline is R1's plain rows on the same 20 cells (reused); if R1 has not run, the
baseline is re-run (+320).

**Metrics, analysis, prediction and flip:** as R1, with "alt nodes" in place of "adapter identity".
- **Prediction:** per-cell levels move (served ± several riders per cell), and effects carry over within ±10 pp
  (relative reversal reduction) and ±3/day (served DD).
- **Flip:** C7 **WEAKENED** at > 15 pp or > 5/day with ≥ 8/10 days of the same DD sign; **REVERSED** for C1 if G2
  ΔvisRev60 ≥ 0 on ≥ 5/10 days.

---

## 4. Order, size, time

| Order | Check | Generation | Runs | Est. wall (6 workers; INS ≈ 0.8 s, G2 ≈ 1.6 s per run, wf15 timings) |
|---|---|---|---|---|
| 0 | Gates G-ENG, G-P2, G-GEN, G-PAD | 2 cells | ≈ 60 | 5 min |
| 1 | R4 G2 friction | — | 3600 | 20–30 min (T16 slower) |
| 2 | R3 forecast / pad + R3-X | F16 export + 96 cells | 3060 | 15 min + 15 min generation |
| 3 | R6 curves | — | 3240 | 15 min |
| 4 | R5 regime grid | P256 for D30 | 2520 | 12 min + 5 min |
| 5 | R2 fence tolerance | R2b flat copies | 1800 | 8 min |
| 6 | R1 identity | adapter copies | 640 | 10–30 min (snapshot hashing) |
| 7 | R7 node placement | 20 cells | 320 | 5 min + 5 min |

Total ≈ 15,200 runs, about 1.5–2 h wall. All jobs are sequential, each with ≤ 8 workers.

---

## 5. Not covered by this plan (stated so that nobody reads silence as support)
- **BT ride-limit semantics.** The survey rated it "Cao"; it was never run.
- **Other windows and other cities.** Elmi's 13–15 h window; FleetPy Chicago.
- **Stronger Elmi variants:**
  - a penalty for fence violation instead of forced vehicles;
  - Elmi with a relocation cost.
- **Anything on the real RideBound C#.** G2 cannot exist there (vehicle lock, a3 D1), and per-rider budgets and the
  forecast do not exist there (a3 D4, D7).
- **Rider welfare and perceived value of fewer reversals:** research keys b1–b4.
- **The confirmatory statistics of wf14/wf15.** They stay as registered. wf17 adds exploratory context and does not
  re-test them.
