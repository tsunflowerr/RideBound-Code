# wf21/n7 — Does the closed-loop forecast give more than promising longer? (PLAN, written before any production run)

Label of every row, table and statement: `wf21-n7-exploratory-post-audit` = **thăm dò sau kiểm toán** (exploratory, post-audit).
- All 30 days were simulated before (F14 in wf14/wf15/wf17/wf20, F16 in wf17 R3 and wf20). The wf20 Shadow N/CL outcomes are known.
- Hypotheses and bands are written by the AI after the audit. There is no external timestamp (only PLAN-HASH.txt on this machine).
- p-values are descriptive. No multiplicity correction. Unfavourable results get the same prominence as favourable ones.

`SP` = `E:/RideBoundData/company-intake/2026-10-23/sp`, `K` = `SP/wf21/n7`.
Everything outside `K` is read only. No file is deleted. Outputs are append-only; nothing is overwritten.

## 0. Question (audit item §6-N7)

The owner's system is Shadow allocation + Rule C (SHRC) + the closed-loop prev4 forecast (CL). wf20 showed that CL cuts lateness
over a day (−32 to −38 per 1000 served under Shadow) but harms the 17:00 window (+17 to +21 per 1000). Two gaps remain:
1. **Length vs information.** CL promises longer in the morning (+22 to +29 s) and shorter in the evening (−10 to −19 s). No control
   has yet promised the *same length* *through dispatch*. wf17 R3 had only (a) a display-layer pad on N trajectories (does not touch
   dispatch) and (b) CLF, a flat drift through dispatch whose length matched CL only in w08 (within 25 %), not in w17.
2. **Causality.** The forecast index normalises each month by its own median polish (`SP/wf7/c-forecast/PREREG.md:17-18`, "one polish
   per month"; `SP/wf7/c-forecast-review/REVIEW.md:95-98`). A source day's factors therefore depend on later days of its month.

## 1. Material reused (read only)

- Runner: copy of `SP/wf20/fullsys/code` in `K/code` (sha256 of the copy: `K/work/code-copied-sha256.txt`; engine17.js `81d7b47e…`
  unchanged). Changed files: `scripts/shcore.js`, `scripts/run_sh.js` only (diff = the header comments; `PLAN-HASH.txt` lists hashes).
- Shadow parameters **frozen**: `SP/wf15/frozen/shadow-params.json` (x* = 72 % INS / 71 % G2, θ* per config × window). x* was
  calibrated on discovery days **in mode N** to match EO35 service. It is **not** re-tuned for CL, PAD or CLc. The realised guaranteed
  share is reported per mode.
- Cells: F14 = 14 days × {w08, w17} × {r0, r1, r2} (`SP/wf14/scenarios/weekdays-P512`, day list `SP/wf20/fullsys/work/days_F14.csv`),
  F16 = 16 days × 2 × 3 (`SP/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512`, λ prior `SP/wf20/fullsys/work/prior_F16/prior_prevhour.csv`).
- World: P512, 62 vehicles, capacity 4, max ride 2000 ‰, dwell 0, pickup window 600 s. Configs INS `{}`, G2 `{reopt: {g2: 99, g2Targets: 8}, lock: 'ratchet'}`.
- Arms: **SHRC** (owner's rule) and **SHHZ** (Elmi Shadow + Hörl–Zwick floor = competitor rule). EOHZ35/RCE35 are **not** run
  (not needed for any gate: every gate below uses SHRC/SHHZ rows).

## 2. Promise modes (all go through dispatch: the Runner's projection, feasibility, cost and publication use them)

| Mode | Definition |
|---|---|
| N | No forecast: the Runner holds the current traffic snapshot flat (exact RideBound). Re-run (see §3) |
| CL | Closed-loop prev4 forecast, current index `SP/wf7/c-forecast/results/accuracy-index-2019.json`, `opts.forecast = {prof, clipPickup: true}` exactly as wf20 (weight 1 in the current 15-min bin, prof[b]/prof[b0] after). Re-run (see §3) |
| **PAD** | Equal-length flat pad through dispatch = engine17 patch P3 (wf17 CLF): `opts.forecast = {prof, clipPickup: true, flatRatio: r}`. Weight 1 in the current bin, **constant r** in every later bin. The only difference from CL is that the day- and time-specific profile is replaced by one constant per config × window × arm. r is set by the matching rule below (r < 1 = uniform shortening, needed in w17 where CL shortens promises) |
| **CLc** | CL with a **causal** index (§2.2). Same engine path as CL |
| PADc | Only if needed (§2.1 rule): a second flat pad matched to CLc's length |

### 2.1 Matching rule for PAD (fixed now; reads promise length only)

- Promise length of a rider = p0 − request time (`p0 − arrival`), p0 = first published drop-off promise. New row fields
  `plSumArr_ms`, `plN` (all promised riders; promised = served in every wf20 row).
- Group g = config × window × arm (8 groups). L_g(mode) = Σ plSumArr_ms / Σ plN over the 90 cells of the window (30 days × 3 resamples),
  i.e. the rider-weighted mean over **all 30 days**.
- **r_g is chosen so that L_g(PAD) = L_g(CL)** (same arm, same config, same window), tolerance |gap| ≤ 0.5 s, by the deterministic
  root finder in `K/analysis/calib7.js`: first guess r = 1 + (L_CL − L_N)/750 s (prior slope from wf17 R3 dumps,
  `K/work/pl_slope_estimate.txt`), then secant / regula falsi on (1, L_N) and the tried points; at most 6 tries per group; final r =
  the tried value with the smallest |gap|. Calibration rows go to `K/calib/calib.ndjson` (variant `calPAD`); `calib7.js` keeps only
  identity and promise-length fields in memory. Production PAD rows are then run once with the final r (they must equal the
  calibration rows at that r: gate G9b).
- **Why match on all 30 days, not "calibrate on F14, test on F16":** the pad is a length-equalising device, not a forecast. The
  claim under test is "at the *same* mean promise length, does the forecast do better?". Matching on the very days of the contrast
  makes the pooled contrast exactly equal-length. Calibration reads no outcome, so there is no leakage of outcome information. The
  per-panel length gaps (F14, F16) are reported, so the reader can see how far the pooled match holds within each panel.
- **Why a flat drift and not a uniform inflation of every leg:** CL never changes the projection inside the current 15-min bin
  (weight 1 there). A pad that also inflates the current bin would differ from CL in mechanism as well as in information. With the
  flat drift, CL − PAD isolates the profile's information (its shape over the day and across days) at equal mean length. The A3
  display pad p0 + k(p0 − t0) cannot enter dispatch without a new engine patch, so it is not used.
- **PADc rule:** after CLc production, `plgap.js` compares L(CLc) with L(CL) per group. If every |L_CLc − L_CL| ≤ 1.0 s, CLc
  contrasts use PAD (the gap is reported). Otherwise a PADc pad is calibrated to L(CLc) by the same procedure (variant `calPADc`,
  production variant `PADc`) and CLc contrasts use PADc (CLc − PAD is still reported).

### 2.2 Causal index (CLc) — exact difference from the current index

Current (old) rule: F[s][b] = exp(e_med(s, b) − median of e_med over month(s)), with e_med from a median polish of log trip duration
(OD effect + day-bin effect, 4 alternations) fitted on the **whole calendar month** of s (`SP/wf7/c-forecast/scripts/tlc_factors.sql.tmpl`).
For a source day s in the same month as the target day d, the OD effects (and the ≥ 20-trip OD filter and the month median) use days
after d.

Causal rule (fixed now, before any CLc run or forecast-accuracy check): for target day d in month M, the reference month is R = M − 1
(entirely before d).
- Source day s in R: e(s, b) = R's own polish (identical to the old value; the polish of R uses only R).
- Source day s in M (s < d): e(s, b) = median over s's trips in bin b of (log duration − r_OD of R), OD pairs of R's reference only.
  Nothing is re-estimated with month-M data.
- F_d[s][b] = exp(e(s, b) − median of R's e_med): one constant for all sources of d. It cancels in the engine's ratios
  prof[b]/prof[b0]; the old rule used month(s)'s median, which weights sources of two months slightly differently.
- The source days (prev4: d − 7k, k = 1..4, school weekdays, ≥ 2 sources) are unchanged.
- Builder: `K/cidx/scripts/build_sql.js` (copies the original generated `run.sql` of each month, changes only the output directory,
  appends the causal scoring block), DuckDB v1.5.5 (`SP/tools/duckdb-v1.5.5-sd`, same binary as wf20 prep_prior), `build_index.js`.
- **Already done before this plan (input data only, no dispatch run):**
  - CI-0: the rebuilt months reproduce wf7's `bins.csv` columns day, bin, n, e_med, q25, q75, e_h0/e_h1, e_yellow/e_boro exactly;
    only e_mean and e_trim differ (≤ 2·10⁻¹³ relative, parallel summation order; not used by the index); `od_ref.csv` identical as a set.
  - CI-1: the old rule rebuilt from these outputs equals `accuracy-index-2019.json` (153 days × 96 bins, 0 mismatches, JSON string identical).
  - CI-2: per-target old-rule profiles equal the CL profiles for all 30 days × 2 windows (exact).
  - CI-3: causal file: every stored day < target; reference month ends before the target month; same source days as CL.
  - Descriptive (no traffic truth, no outcome): CLc vs CL forecast weights w(b) = m(b)/m(0), b = 1..8, differ by median 0.27 % (w08) /
    0.23 % (w17) at the largest horizon point, max 0.92 %. **6 target days have bit-identical profiles** (all sources in the previous
    month: 2019-10-02, 10-03, 10-07, 10-08, 12-04, 12-05), so CLc = CL there (gate G6). Forecast accuracy against the target day's own
    factors is computed only after production (descriptive).

### 2.3 Skipped: RN (recalibrated naive through dispatch)

The wf17 B3 "recalibrated naive" learns a per-15-min-bin correction of the naive promise from earlier days' **realised arrivals**.
Through dispatch it would need (i) a sequential day-by-day feedback loop over earlier simulated days of the same arm (our 30 days are
scattered over Sep–Dec and are not consecutive) and (ii) a new rule mapping an outcome-based correction into Runner travel weights.
Both are new design choices without precedent here, and a time-of-day profile learned from earlier weeks is what prev4 already is.
It is skipped; this is a limitation of the answer (the pad is a weaker baseline than a recalibrated naive).

## 3. Runs

- N and CL: all SHRC/SHHZ runs of wf20 are **re-run** (720 + 720), because the promise-length field does not exist in wf20 rows. Every
  existing field must equal the wf20 row exactly (gate G1 on all 1,440 rows), so the analysis effectively reuses the wf20 rows.
- PAD 720, CLc 720 (+ PADc 720 if the rule fires); calibration about 720 per try (resume skips groups already converged).
- 4 worker threads (`run_sh.js` caps at 4), one job at a time, wallBudget 900 s per run, no per-rider dumps.
- Order: gates → N, CL → calibration → PAD → CLc → PADc rule → post gates → analysis.

## 4. Metrics and statistics (`K/analysis/an7.js`; sha256 in PLAN-HASH.txt)

- Unit = day. X_d = Σ_{w08, w17} mean_{r0, r1, r2} X; per window X_{d,w} = mean over resamples; "/1000" = per 1000 served.
- Metrics (day, w08, w17): served; mean initial promise length PL (s); late60First/1000 (A > p0 + 60 s); **early60First/1000**
  (A < p0 − 60 s, new field); visRev60 per day; Pchg/1000 (= pDisp); breachDeadline60/1000; TV5 (s per served rider); guaranteed
  riders late > 60 s per day (G_late60First); ledger breaches/1000; guaranteed share (%); relocations.
- Paired by day: mean, SE, 95 % t-interval, median, n−/n+/ties, exact two-sided sign test (ties dropped); counts also relative.
  F14, F16 and pooled 30 days.
- Contrasts per config (INS, G2) and arm (SHRC, SHHZ): **CL − PAD**, **CLc − PAD** (or CLc − PADc), **CLc − CL**, **PAD − N**, CL − N, CLc − N.
- Owner's system, per config, exactly additive by day:
  SHRC_X − SHHZ_N = (Rule C part: SHRC_N − SHHZ_N) + (length part: SHRC_PAD − SHRC_N) + (information part: SHRC_X − SHRC_PAD),
  X ∈ {CL, CLc}; promise part = length + information. Whole system (descriptive): SHRC_G2_X − SHHZ_INS_N = G2 part (SHHZ_G2_N −
  SHHZ_INS_N) + Rule C part + length part + information part.

## 5. Predictions (written now; bands for every config × arm cell, pooled 30 days; per-panel labels too)

Evidence used (all already seen): wf20 REPORT-VI (Shadow CL − N: late/1000 day −32 to −38, w08 −87 to −98, w17 +17 to +21; visRev60
−25 % to −48 %; TV5 −16 to −20 s); wf17 R3 rows and dumps (non-Shadow EOHZ35/RCE35; `K/work/evidence_clf.txt`, `evidence_eqlen.txt`,
`pl_evidence_wf17_bypanel.txt`): CL − N promise length w08 +17 to +30 s, w17 −10 to −19 s; CLF (flat drift) − N late/1000 w08 −104 to
−112 at +17 to +25 s, w17 +119 to +132 at −29 to −38 s; a crude linear length-scaling of CLF to CL's length gives CL − equal-length
pad ≈ +2.5 to −29 per 1000 in w08 (median ≈ −6) and +1 to −37 in w17 (median ≈ −12), non-Shadow. Under Shadow the CL effects are
about 0.5–0.75 of the non-Shadow ones. The display-layer pad of wf17 (e) favoured CL by 7–8.5 pp in w08 and the pad by 2.7 pp in w17;
**through dispatch I expect a much smaller morning gain and the evening sign to turn** (the dispatcher over-commits when projections
are shortened uniformly).

Formal items: `K/analysis/predictions.json` (labelled mechanically by an7.js; rules in that file):

| ID | Contrast, metric, scope | Prediction (band, day share) | Flip (REVERSED) |
|---|---|---|---|
| P1 | CL − PAD late/1000 w08 | −20 to 0, ≥ 60 % days negative (small information gain in the morning) | > +3 with sign p < 0.05 |
| P2 | CL − PAD late/1000 w17 | −25 to 0, ≥ 60 % days negative (CL better than an equal shortening) | > +5 with sign p < 0.05 |
| P3 | CL − PAD late/1000 day | −20 to 0, ≥ 60 % days | > +3 with sign p < 0.05 |
| P4a/b/c | PAD − N late/1000 w08 / w17 / day | −105 to −70 (≥ 90 %) / +15 to +45 (≥ 80 % positive) / −45 to −20 (≥ 80 %) | w08 ≥ 0 / w17 ≤ 0 / day ≥ 0 (or < 50 % of days) |
| P5 | CL − PAD TV5 s, day | −18 to −5, ≥ 75 % days | ≥ 0 or < 50 % days |
| P6 | CL − PAD visRev60/day | −4 to +2 (the reversal cut is mostly length/direction) | outside with sign p < 0.05 |
| P7 | CL − PAD breachDeadline60/1000 day | −8 to 0, ≥ 60 % days | > +1 with sign p < 0.05 |
| P8a/b | CL − PAD served/day ; Pchg/1000 day | within ±3 ; within ±6 | outside with sign p < 0.05 |
| P9 | CL − PAD early60First/1000 day | −25 to 0, ≥ 60 % days | > +3 with sign p < 0.05 |
| P10 | CL − PAD guaranteed riders late/day | −4 to 0, ≥ 55 % days | > +0.5 with sign p < 0.05 |
| P11a–f | CLc − CL: late/1000 day, w08, w17 ; served ; visRev60 ; TV5 | within ±4, ±6, ±6 ; ±2 ; ±1.5 ; ±2 s | outside with sign p < 0.05 |
| P12a–c | CLc − PAD(c) late/1000 w08 / w17 / day | as P1 / P2 / P3 | as P1 / P2 / P3 |
| P13 | calibrated r (descriptive, not an outcome) | w08 1.025–1.045 ; w17 0.970–0.995 | — |
| P14 | (PAD − N)/(CL − N) visRev60 day (descriptive) | 0.5 to 1.4 (the pad reproduces most of the reversal cut) | — |

Labels: HOLDS / STRONGER / WEAKENED / REVERSED / INCONCLUSIVE, as defined in predictions.json (same scheme as wf20).

## 6. The answer rule (fixed now)

Primary endpoint late60First/1000; primary arm SHRC (owner's system), both configs; SHHZ reported alongside. For each scope
(day, w08, w17): **"forecast beats the equal-length pad"** if CL − PAD < 0 with pooled sign p < 0.05 **and** the mean is negative in
both F14 and F16; **"pad beats forecast"** if the mirror holds; otherwise **"no clear difference"**. The same rule for CLc − PAD(c)
answers "does it hold with a causal index". Secondary endpoints (TV5, visRev60, deadline breaches, early arrivals, served) are
classified the same way and reported next to it; they do not change the primary answer. A day-level "yes" with a window-level
"pad beats forecast" is reported as a mixed answer, never as a plain "yes".

## 7. Gates (exact, no tolerance; `K/analysis/gcheck7.js`; any pre-production failure = stop, DEVIATIONS.md, fix, re-gate)

Pre-production (`RUNBOOK.sh gate`, cells as wf20: F14 2019-10-03-w08-r0, 2019-10-03-w17-r0, 2019-12-18-w08-r1, 2019-12-18-w17-r2;
F16 2019-09-20-w08-r0, 2019-12-19-w17-r1; SHRC, SHHZ × INS, G2):
- G1 N and CL rows equal the wf20 rows on every wf20 field except label/ms/msScore, field set = wf20 + the new fields.
- G2 PAD with flatRatio 1 reproduces N (rider fingerprint sigR3 and every wf20 field except label/ms/msScore/mode/variant/sig/sigX/fc;
  the shown pickup ETA is clipped by design, as in wf17 G-R3d).
- G3 the CLc code path fed the **old-rule** per-target index reproduces CL on every field (proves CLc differs from CL only by the index).
- G4 SHRC ledger breaches 0 (and the in-run assertion Labs ≤ bSH); G5 x* = 72/71; G8 plN = promised, plServedN = served.
Post-production (`RUNBOOK.sh check`): G1 on all 1,440 N/CL rows; G4, G5, G8 on all rows; G6 CLc = CL on the 6 identical-profile
days (144 rows); G7 completeness (180 cells per mode × config × arm, no duplicates); G9a gate rows = production rows; G9b production
PAD rows = calibration rows at the final r.

## 8. Honesty rules

No arm, metric, day or contrast is dropped after results are seen. Calibration looks only at promise length. If calibration does not
converge within 6 tries, the best r is used and the residual gap is reported. Failed tasks are logged and only resumed (never
replaced). Every change after this plan goes to `K/DEVIATIONS.md` with a UTC timestamp. Known limits stated now: days already seen;
frozen x* (calibrated in N); emulator only (Shadow has no C# implementation); the pad is matched in mean length, not per day; RN not run.
