# wf20 / fullsys — Shadow + Rule C + closed-loop forecast together (PLAN, written before any production run)

Label of every row and table: `wf20-fullsys-exploratory-post-audit` = **thăm dò sau kiểm toán** (exploratory, post-audit).
Nothing here is confirmatory: F14 days have been simulated many times (wf14, wf15, wf17); F16 days were simulated in wf17 R3
(U/EO35/EOHZ35/RCE35/NCE35 in N/CL/CLF), never with Shadow arms. All hypotheses are written by the AI after the audit, with no
external timestamp. p-values are descriptive; no multiplicity correction. Bad results are reported with the same prominence as good ones.

`SP` = `E:/RideBoundData/company-intake/2026-10-23/sp`, `K` = `SP/wf20/fullsys`.

## 0. Question

What happens when Shadow allocation (Elmi 2026), Rule C (per-rider ledger Σ|c| ≤ B_r, c = p − e) and the closed-loop prev4
forecast (CL) run together? Never run before: Shadow arms ran only in mode N (wf15 confirm), CL ran only with U/EO35/EOHZ35/RCE35
(wf14 part S, wf17 R3).

## 1. Code (copied, never modified in place)

- `K/code` = copy of `SP/wf17/r1-identity-fence-pad/code` (sha256 of every copied file: `K/work/code-copied-sha256.txt`).
- Changed files (diff vs the wf17 copy is the only change; nothing in `runner/` changed; `engine17.js` sha256 `81d7b47e…` = the R3 engine):
  - `scripts/shcore.js`: `task.fc ∈ {N, CL}`. CL sets `opts.forecast = { prof, clipPickup: true }` with
    `prof = forecast.profile(day, w08 ? 32 : 68, 'prev4', SP/wf7/c-forecast/results/accuracy-index-2019.json, 15)`, i.e. exactly
    `wf17/.../scripts/r3drive.js` mode CL, on the same `opts` object that carries `opts.sscore` (Shadow score + frozen class).
    Row fields `mode` (= fc), `panel`, `sigR3` (= r3drive `sigOf`), label.
  - `scripts/run_sh.js`: `--fc`, `--panel` (both in the resume key and the row), `--prior <csv>`, at most 4 workers, label.
  - new `scripts/prep_prior_f16.js`: Shadow λ prior (preceding-hour zone counts; input data, no outcome) for the F16 days, which wf15
    never exported. Same SQL template / DuckDB / data / zone list as `wf15/scripts/prep_data.js`. **Already run (input data only):**
    g0a re-export of wf15's own 115 days reproduces `wf15/work/prior_prevhour.csv` byte-for-byte (sha256 `d40e2f3b…` PASS);
    g0b F16: 32/32 day-window keys, O = D totals, 0 zone mismatches over 96 cells (`K/work/prior_report.txt`).
- Shadow parameters **frozen, not recalibrated**: `SP/wf15/frozen/shadow-params.json` (x* = 72 % INS / 71 % G2, θ* INS w08 69.50 /
  w17 54.43, G2 w08 66.72 / w17 52.49, alphaS 0.15, stride 0), same as `SP/wf15/work/thresholds.json`. x* was calibrated on discovery
  days **in mode N** so that SHHZ served matches EO35. Under CL the scores are computed on forecast-stretched stop times and slack,
  so the realised guaranteed share can move; it is reported (`guaranteedShare %`) and **not** re-tuned. This is a known limitation:
  SHHZ_CL is "Elmi's frozen threshold under our forecast", not a re-calibrated Elmi.
- Execution smoke (before this plan): one F16 cell truncated to 15 min, CL, 10 runs, no error (`K/work/smoke/`); metrics not used.

## 2. Design

- World: wf14/wf17 base (P512, 62 vehicles, capacity 4, max ride 2000 ‰, dwell 0, pickup window 600 s, TLC-MP15, identity plain).
- Configs: INS `{}`; G2 `{ reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' }`.
- Arms (each in modes N and CL): SHF (x*), SHHZ (x*), SHRC (x*), EOHZ35, RCE35 (reference rows).
- Panels: F14 = 14 days × {w08, w17} × {r0, r1, r2} = 84 cells (`SP/wf14/scenarios/weekdays-P512`); F16 = 16 days × 2 × 3 = 96 cells
  (`SP/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512`).
- Runs: 180 cells × 2 configs × 2 modes × 5 arms = **3600**; 4 invocations (`RUNBOOK.sh f14n f14cl f16n f16cl`), one output
  `K/results/rows.ndjson`, 4 workers, wallBudget 900 s per run. No per-rider dumps.
- All 5 arms of a task share one causal score context; EOHZ35/RCE35 get score logging only (no effect on dispatch; g2 checks this
  against R3 rows that ran without scoring).

## 3. Gates (exact, no tolerance; `K/analysis/gcheck.js`)

Pre-production (`RUNBOOK.sh gate`, `gatecheck` → `K/gates/GATES-pre.txt`): cells F14 `2019-10-03-w08-r0, 2019-10-03-w17-r0,
2019-12-18-w08-r1, 2019-12-18-w17-r2` and F16 `2019-09-20-w08-r0, 2019-12-19-w17-r1`, all 5 arms × 2 configs × N and CL (120 runs).
The gate checker prints only match counts; Shadow-CL metric values of gate rows are not looked at before production.
- **g1**: SHF/SHHZ/SHRC mode-N rows on F14 equal `SP/wf15/results/confirm/main.ndjson` (same cell|config|arm) on every wf15 field
  except bookkeeping/timing (part, set, ms, msScore, engine, label, variant, fenceTolMs, audit, sig, sigX, diagTieBreaks). g1x (extra):
  the same for EOHZ35/RCE35 N.
- **g2**: EOHZ35/RCE35 rows in N and CL equal `SP/wf17/r1-identity-fence-pad/results/r3.ndjson` (same panel|cell|config|mode|arm,
  flatRatio null) on every R3 field except label, check, ms, sig, flatRatio; and `sigR3` = R3 `sig` (rider-level identity).
- **g3**: every SHRC row (N and CL) has breachLedger = 0, G_ledgerBreach = 0, A_ledgerBreach = 0; the in-run assertion
  `Labs ≤ bSH` for every rider (zero tolerance) must pass, otherwise the task fails and writes no row.
- x* check: every Shadow row uses 72 (INS) / 71 (G2).
Any pre-production gate failure = stop, log in DEVIATIONS.md, fix, re-gate. Post-production (`RUNBOOK.sh check` →
`K/results/GATES-post.txt`): g1 on all 504 F14 N Shadow rows, g2 on all 1440 EOHZ35/RCE35 rows, g3 on all 720 SHRC rows,
g4 completeness (84 / 96 rows per panel × config × mode × arm, no duplicates), g5 gate rows = production rows (determinism).
A post-production g1/g2/g3 failure is reported in DEVIATIONS.md and on top of the results; rows are never overwritten.

## 4. Metrics and analysis (`K/analysis/fsan.js`, sha256 in PLAN-HASH.txt)

- Unit = day. X_d = Σ_{w08,w17} mean_{r0,r1,r2} X; per window X_{d,w} = mean over resamples. A day missing any cell of an arm is
  dropped from contrasts with that arm (logged).
- Metrics (all per day, per window w08/w17, and per 1000 served): served; visRev60; late60First; Pchg = pDisp (dispatch push ≥ +5 s);
  breachDeadline60 (A > p0 + B_r + 60 s); TV5 (s per served rider; sumTV5 with a 5-s deadband); guaranteed riders late > 60 s
  (G_late60First, Shadow arms); diagnostics: guaranteedShare (%), breachLedger per 1000, relocations.
- Paired by day: mean Δ ± SE, 95 % t-interval, median, n−/n+/ties, exact two-sided sign test; counts also rel = ΣA/ΣB − 1.
  F14, F16 and pooled (30 days) reported separately.
- Contrasts (per config): **MAIN SHRC_CL − SHHZ_CL**; SHRC_N − SHHZ_N; SHRC_CL − SHF_CL; SH*_CL − SH*_N (SHF, SHHZ, SHRC);
  reference EOHZ35_CL − EOHZ35_N, RCE35_CL − RCE35_N, RCE35_CL − EOHZ35_CL, RCE35_N − EOHZ35_N; SHHZ_CL − EOHZ35_CL,
  SHRC_CL − RCE35_CL; DiD (SHRC−SHHZ)_CL − (SHRC−SHHZ)_N and (SHRC−SHHZ)_CL − (RCE35−EOHZ35)_CL.
- Whole system (wf13 §3 layout): (a) Elmi as in the paper SHHZ + INS + N; (b) our full system SHRC + G2 + CL; (c) fair control
  SHHZ + G2 + CL; (d) RCE35 + G2 + CL (no Shadow); (e, extra) EOHZ35 + G2 + CL. Levels for all; contrasts b−a, b−c, b−d, d−a, c−a.

## 5. Predictions (from existing evidence; written before production)

Evidence file: `K/work/evidence/evidence.txt` (fsan.js on wf15 main rows restricted to F14 + wf17 r3 rows; the 672 EOHZ35/RCE35 N
rows present in both files are identical on every count field). Key evidence (pooled unless F14 only):
- wf15 F14, mode N, SHRC − SHHZ: G2 served −9.79/day (14/14 days lower), visRev60 −15.05/day (−32.6 %, 14/14), Pchg −5.7/1000
  (12/14 lower), late60First +2.7/1000 (9/14 higher), TV5 −19.9 s, G_late60First −1.1/day, relocations −75.6 %;
  INS served −5.0 (12−/1+), visRev60 −1.0 (−3.7 %), late60First +1.2/1000, Pchg −1.3/1000, TV5 +0.1 s.
- wf17 R3 CL − N (EOHZ35, RCE35; F14 and F16 alike): late60First −44 to −46/1000 per day (30/30 days), w08 −131 to −139/1000,
  **w17 +37 to +43/1000 (30/30 days worse)**; visRev60 −26 % to −44 %; TV5 −18 s; breachDeadline60 −10 to −17/1000; Pchg ±1/1000;
  served |Δ| ≤ 2/day. DiD (RCE35−EOHZ35)_CL − (…)_N: visRev60 +0.8 (G2) / +1.2 (INS)/day, breachDeadline60 −7.0 / −5.9 per 1000.
- wf17 report §2.6 R6 (D30, r0, at matched service): SHRC vs SHHZ under G2 reversals −14 % to −28 %, no INS gain, Pchg worse
  +0.6 to +3.7 pp, late60First worse, served lower 1.6–14.5/day. Note: R6's Pchg/late statements are at matched service; this design
  compares at the same x*, where the F14 N evidence above has Pchg slightly **lower** for SHRC. I predict from the same-x* evidence.

Notation: band = predicted range of the pooled mean; "days" = share of non-tied days on the predicted side.

| ID | Quantity (pooled 30 days unless stated) | Prediction | Flip criterion (REVERSED) |
|---|---|---|---|
| P1 | MAIN G2 SHRC_CL − SHHZ_CL visRev60/day | −10 to −16 (rel −25 % to −40 %), ≥ 80 % days | mean ≥ 0, or ≥ half of days ≥ 0 with sign p < 0.05 |
| P2 | MAIN G2 served/day | −6 to −14, ≥ 70 % days | mean ≥ 0 with ≥ half of days positive |
| P3 | MAIN G2 late60First/1000 | −3 to +6 (no gain; slightly worse) | ≤ −6 with sign p < 0.05 (Rule C clearly better) |
| P4 | MAIN G2 Pchg/1000 | −10 to +3 | ≥ +5 with sign p < 0.05 |
| P5 | MAIN G2 TV5 s/served | −10 to −22 | ≥ 0 |
| P6 | MAIN G2 breachDeadline60/1000 | −3 to +2 | outside band with sign p < 0.05 |
| P7 | MAIN G2 G_late60First/day | −2.5 to +1 | ≥ +2 with sign p < 0.05 |
| P8 | MAIN INS SHRC_CL − SHHZ_CL visRev60/day | −2.5 to +0.5 (|rel| ≤ 10 %) | ≤ −20 % with ≥ 75 % days (an INS gain appears) |
| P9 | MAIN INS served/day | −2 to −8 | ≥ +2 with sign p < 0.05 |
| P10 | MAIN INS late60First/1000, Pchg/1000, TV5 | each within ±4 (TV5 ±3 s) | outside with sign p < 0.05 |
| P11 | FC SH*_CL − SH*_N late60First/1000 day (each of SHF/SHHZ/SHRC, INS and G2) | −25 to −50, ≥ 80 % days | mean ≥ 0 or < 60 % days negative |
| P12 | FC SH* late60First/1000 w08 | −80 to −140 | ≥ 0 |
| P13 | FC SH* late60First/1000 **w17** | **+20 to +45 (CL hurts the evening)**, ≥ 75 % days | ≤ 0 or < 50 % days positive |
| P14 | FC SH* visRev60/day | rel −20 % to −45 %, ≥ 75 % days | ≥ 0 |
| P15 | FC SH* TV5 s/served | −10 to −25 | ≥ 0 |
| P16 | FC SH* breachDeadline60/1000 | negative, rel −30 % to −80 % | ≥ 0 |
| P17 | FC SH* Pchg/1000 ; served/day | |Δ| ≤ 5 ; |Δ| ≤ 4 | outside with sign p < 0.05 |
| P18 | FC SH* guaranteedShare (pp) | |Δ| ≤ 3 pp; w08 up, w17 down (uncertain) | |Δ| > 5 pp in either window = calibration caveat (not a flip) |
| P19 | DiD G2 (SHRC−SHHZ)_CL − (SHRC−SHHZ)_N visRev60/day | 0 to +3 (forecast slightly shrinks Rule C's cut) | ≤ −3 with sign p < 0.05 |
| P20 | SYS b − a (full system − Elmi paper) served/day | −6 to +6 | — (two-sided band) |
| P21 | SYS b − a visRev60/day | −2 to −10 | ≥ 0 |
| P22 | SYS b − a late60First/1000 day ; w17 | day −15 to −45 ; w17 positive (+15 to +50) | day ≥ 0 ; w17 ≤ 0 |
| P23 | SYS b − a Pchg/1000 ; TV5 s | −5 to +12 ; −1 to −20 | outside with sign p < 0.05 ; ≥ 0 |
| P24 | SYS b − c = P1–P7 (G2 MAIN) | as P1–P7 | as P1–P7 |
| P25 | SYS b − d (what Shadow adds to RCE35+G2+CL) | served −4 to −15; visRev60 +6 to +16; late60First/1000 −40 to −80; Pchg/1000 −80 to −115; TV5 +70 to +110 s; breachDeadline60/1000 +5 to +30 | any of the listed signs reversed with sign p < 0.05 |
| P26 | SHRC_CL − SHF_CL served/day | +25 to +40 (the wf15 strict-fence rounding gap persists) | ≤ +10 |
| P27 | g1/g2/g3 | all PASS | any mismatch |

Labels per prediction: **HOLDS** (pooled mean has the predicted sign/inside the band and day-consistency met), **STRONGER**
(right sign, beyond the band away from 0), **WEAKENED** (right sign, band missed toward 0 or day-consistency missed),
**REVERSED** (flip criterion met), **INCONCLUSIVE** (otherwise). For two-sided "≈ 0" bands: HOLDS if inside the band, else the
flip criterion decides. F14 and F16 are labelled separately as well; F14 mode-N Shadow rows are reproductions of wf15 (known).

## 6. Honesty rules

No arm, metric, day or contrast is dropped after results are seen. Failed tasks are logged (`rows.ndjson.errors.ndjson`) and
re-run only by the resume rule (never replaced by other cells); any deviation goes to `K/DEVIATIONS.md` with a timestamp.
Outputs are never overwritten (the drivers append; analysis outputs get new names if re-run). The whole-system comparison mixes
three changes (Shadow, G2, CL) and is descriptive.
