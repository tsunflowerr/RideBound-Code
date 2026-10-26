# wf17 r3-curves-nodes: checks R6 (policy curves) and R7 (node placement) — EXPLORATORY, POST-AUDIT

Label: **wf17-exploratory-post-audit**. Nothing here is confirmatory; there is no Holm correction and p-values are descriptive.
Plan: `wf17/a0-plan/RUNPLAN.md`, sha256 `ef953c3b…2f12`, verified against `RUNPLAN-HASH.txt` before anything was copied or run.
Deviations (with timestamps) are in `DEVIATIONS.md`; code hashes in `CODE-HASH.txt`; analysis hashes (written before both production
runs) in `ANALYSIS-HASH.txt`. Full tables: `results/r6_tables.txt` / `.json`, `results/r7_tables.txt` / `.json`.

Unit = day (w08 + w17, r0). Every test is an exact two-sided sign test over days with ties dropped; intervals are 95 % t-intervals.
"In favour" = share of non-tied days on the benefit side. Rates are per 1000 served riders of that day.
visRev60 = riders with two consecutive opposite-sign visible changes of ≥ 60 s; Pchg = share of riders with ≥ 1 dispatch change
c ≥ +5 s (Elmi's P(change)); late60First = arrival > 60 s after the *first* promise; breachDeadline60 = arrival > p0 + own budget + 60 s
(Elmi's yardstick; the budget moves with α / x, and guaranteed Shadow riders have budget 0).

---

## 0. Bottom line

Each verdict is the label from the pre-stated rule (RUNPLAN R6/R7 plus the interpretations written in `DEVIATIONS.md` before the runs).
Unfavourable and null results are listed with the same weight as favourable ones.

| Check | Conclusion tested | Verdict | Key numbers |
|---|---|---|---|
| R6 | **C9, G2 reversals:** at *matched service*, over the whole α range, Rule C (RCE) has fewer reversals than the fence (EOHZ) | **HOLDS** | 6/6 points with α > 0: −27 % to −55 %, 90–100 % of days in favour (pooled bootstrap −27 % [−32, −22] to −54 % [−61, −46]) |
| R6 | C9, "without paying in P(change)" (G2 / INS) | **HOLDS / HOLDS** | ΔPchg at matched service −0.3 … +1.2 pp (G2), −0.7 … +0.8 pp (INS); no point above +2 pp |
| R6 | C9 overall (G2) | **HOLDS** | — |
| R6 | C9 reversals under INS (secondary, same rule) | **WEAKENED** | −27 % at α 10, −14 % at α 20, −10 % at α 35, then −4 %, −5 %, −0.3 % (3 points within ±10 %) |
| R6 | **Price not in C9, reported with equal weight:** Elmi's deadline yardstick at matched service | **Fence better** (unfavourable to Rule C) | +5.7 to +9.3 breaches per 1000 served at α 20–75, on 26–30 of the 27–30 included days, both configs; ≈ 0 at α 10 and α 100 |
| R6 | **Price not in C9:** lateness vs the first promise at matched service, G2 | **Rule C worse at 4 of 6 points** | +11 to +18 per 1000 served at α 20, 35, 50, 100 (day-consistent only at α 50, p 0.016, and α 100, p 5e-5); INS: Rule C better at α 10–35 |
| R6 | Elmi's own claim: Shadow (SHHZ) beats Ordinary (EOHZ) on P(change) at matched service | **REPLICATED** (both configs) | −6 to −11 pp, 30/30 days at every x. **But** SHHZ has **+20 % to +91 % more reversals** and +31 % to +106 % more TV5 churn than EOHZ at the same service |
| R6 | Prediction "SHRC lower than SHHZ on reversals and Pchg" | **Half-true** | G2 reversals −14 % to −28 % (5/5 points); INS reversals no gain (0/5); Pchg **worse** by +0.6 to +3.7 pp in both configs (0/5) |
| R7 | **C7:** the effects do not depend on the node draw | **HOLDS** | G2 reversal cut 41.0 % (wf14 nodes) vs 39.4 % (alt nodes), 10/10 days each; served DD +1.8 (G2), +4.6 (INS, 7/10 days, under the 5/day flip line) |
| R7 | C5 note: does the strict-fence gap depend on node placement? | **No** (not a finding) | SHF − SHHZ served gap changes by 21 % (INS) and 7 % (G2); threshold 50 % |

**In one paragraph.**
- The surviving result C1 is **not an artefact of the α = 35 % point**. Under reassignment (G2), Rule C's operating curve lies below the
  fence's curve on reversals over the whole α range when the two are compared **at the same number of served riders**, which removes
  the service price from the comparison. It does not cost Elmi's P(change).
- The **reversal benefit is paid for in two other currencies**, which the thesis must state next to it:
  - Elmi's deadline yardstick: about 0.3–0.46 extra breaches per reversal avoided at α 20–75 (G2), and about 2–5 breaches per
    reversal avoided under INS.
  - Under G2 at matched service, about 1 extra rider late by more than 60 s against the first promise per 1–2 reversals avoided
    (α 20–50, α 100). At *equal* α this price is invisible (Rule C is −0.9 to −6.6 late riders/day there at α 20–100, +0.7 at α 10); it appears only once service is
    matched, because the fence then runs at a lower α.
- Under insertion only (INS), RideBound's real mode, the curve result **weakens to almost nothing above α ≈ 35 %**: the reversal gap
  is −27 % at α 10 and within ±5 % from α 50 upwards, while the deadline price stays. This agrees with C3.
- Elmi's Shadow result replicates on its own metric, but in our model **fewer riders changed is not less churn**: at matched
  service Shadow concentrates more reversals on the riders it lets move.
- A completely different node draw (96–99 % of request nodes moved) moves per-cell service by 7–10 riders on average, yet the effects
  stay within 2 pp (reversal cut) and 5/day (service). C7 holds for nodes as it held for the identity mode (R1).

---

## 1. What was run

### 1.1 Jobs (one heavy job at a time, ≤ 6 workers, no other `node` job running, no suspend)

| Check | Cells | Arms per config | Runs | Wall | Peak RSS | Max heartbeat gap |
|---|---|---|---|---|---|---|
| R6 | D30 × {w08, w17} × r0 = 60 cells, INS + G2 | U; EOHZ{0,10,20,35,50,75,100}; RCE{same}; SHHZ{50,60,70,80,90,x*}; SHRC{same} (27) | 3,240 | 11.1 min | 1,174 MB | 10 s |
| R7 | D10 alt-node cells (20), INS + G2 | U, EO35, EOHZ35, RCE35, NCE35, SHF/SHHZ/SHRC at x* (8) | 320 (+ 320 R1 plain rows reused) | 1.2 min | 999 MB | 10 s |

- R6 Shadow thresholds: every θ(x) from `--ref wf15/work/s3/ref_U_causal.json`; θ(x*) from the reference is bitwise equal to the frozen θ
  in all 4 config × window cells (`work/theta_check.json`). Causal scores, αS 0.15, stride 0 (frozen values).
- R7 cells: `gen17.js --nodetag wf17-r7-alt1 --fmax 512:96 --resample 0` on D10. Same requests, arrival times, zones, κ and fleet size
  as wf14; 96.1–99.2 % of origin and 96.7–99.2 % of destination nodes moved; fleet start nodes redrawn. Sanity: median direct time per
  cell changes −3.9 % … +4.4 % (all 20 under 10 %). R7 Shadow arms use the frozen θ at x* (as R1).
- Free RAM was below the RUNPLAN 4 GB at the R6 start (3.25 GB; logged as a deviation); 7.4 GB at R7.

### 1.2 Gates (all PASS)

| Gate | Result |
|---|---|
| G-GEN on all 20 D10 cells (default tags reproduce wf14 scenario JSON, matrix .bin, .prov.json) | 20/20 sha256-identical |
| R6 pre-run (2 cells, 27 arms): EOHZ/RCE α 10/20/50/100 = a5 V7–V10; EOHZ35/RCE35/U = wf14 main; SHHZ/SHRC x*, U, EOHZ35, RCE35 = wf15 main | all equal |
| R6 post-run: EOHZ/RCE α 10/20/50/100 = a5 V7–V10 (60 cells × 2 configs) | 960/960 (via `gate17r.js`; the in-script a5 gate had an arm-name bug, see DEVIATIONS) |
| R6 post-run: EOHZ35/RCE35/U = wf14 main r0 | 360/360 |
| R6 post-run: SHHZ/SHRC at x*, U, EOHZ35, RCE35 = wf15 main | 600/600 |
| R6 row count | 3,240 rows, 3,240 unique (cell, config, arm) |
| R7 baseline (R1 plain rows) = wf15 main / wf14 main | 300/300 |
| R7 INS NCE35 rider-identical to INS RCE35 (both node draws) | 20/20 and 20/20 |

The a5 gate field `nReq` (an a5 driver field) was checked equal to my `requests` instead of being compared by name (DEVIATIONS 16:39).

---

## 2. R6 — policy curves over α and Shadow x (tests C9; new)

### 2.1 The curves (30-day means; plots in `plots/`)

G2 (reassignment):

| Arm | served/day | Pchg (pp) | visRev60 /1000 | TV5/served (s) | breachDL60 /1000 | late60First /1000 |
|---|---|---|---|---|---|---|
| U | 811.3 | 47.8 | 116.1 | 344.1 | (no budget) | 468.3 |
| EOHZ 0 / 10 / 20 / 35 / 50 / 75 / 100 | 630 / 673 / 706 / 742 / 766 / 795 / 811 | 1.1 / 14.0 / 23.8 / 32.9 / 38.7 / 44.3 / 47.7 | 4.9 / 20.0 / 35.2 / 56.8 / 72.1 / 96.3 / 115.7 | 38 / 55 / 83 / 133 / 187 / 268 / 344 | 85.8 / 16.0 / 13.1 / 9.5 / 7.0 / 5.1 / 4.5 | 86 / 137 / 233 / 323 / 378 / 436 / 468 |
| RCE 0 / 10 / 20 / 35 / 50 / 75 / 100 | 620 / 671 / 702 / 737 / 760 / 790 / 803 | 0.0 / 13.5 / 23.2 / 32.2 / 38.0 / 43.4 / 46.3 | 1.0 / 8.9 / 20.1 / 33.6 / 45.1 / 63.2 / 78.0 | 33 / 47 / 72 / 116 / 165 / 240 / 300 | 86.5 / 20.7 / 19.4 / 17.7 / 16.3 / 15.1 / 5.3 | 86 / 138 / 233 / 324 / 379 / 434 / 464 |
| SHHZ 50 / 60 / 70 / 71 / 80 / 90 | 779 / 765 / 743 / 740 / 713 / 675 | 33.5 / 28.7 / 22.7 / 22.0 / 16.1 / 8.3 | 99.8 / 91.7 / 77.8 / 75.8 / 60.9 / 36.9 | 294 / 270 / 232 / 226 / 190 / 119 | 19.7 / 23.2 / 29.1 / 30.4 / 42.2 / 59.7 | 342 / 300 / 249 / 243 / 198 / 136 |
| SHRC 50 / 60 / 70 / 71 / 80 / 90 | 770 / 751 / 733 / 729 / 701 / 665 | 33.0 / 27.8 / 22.1 / 21.5 / 15.1 / 7.5 | 66.6 / 61.0 / 54.2 / 52.7 / 45.6 / 28.9 | 264 / 243 / 212 / 210 / 170 / 110 | 18.5 / 23.2 / 29.6 / 30.8 / 41.0 / 58.5 | 351 / 304 / 258 / 252 / 200 / 138 |

INS (insertion only) shows the same layout (`results/r6_tables.txt`); the RCE and EOHZ curves nearly coincide there except on reversals
at small α and on deadline breaches.

### 2.2 Primary frontier: RCE(α) against the EOHZ curve at the same served/day (per day, then across 30 days)

Δ = RCE − EOHZ interpolated at RCE's served on the same day. "Incl" = days inside that day's EOHZ served range.

**G2**

| Point | Incl | ΔvisRev60 /1000 (rel. gap) | In favour | ΔPchg (pp) | ΔbreachDL60 /1000 | Δlate60First /1000 | ΔTV5/served (s) |
|---|---|---|---|---|---|---|---|
| RCE10 | 30 | −10.7 (**−54.5 %**) | 97 % (29/1) | +0.13 | −2.1 (13/17, p 0.58) | −0.4 | −8.8 (−15.9 %) |
| RCE20 | 30 | −12.3 (**−38.0 %**) | 90 % (27/3) | +0.96 | **+5.7** (26/30 days) | **+14.6** (20/30, p 0.099) | −7.8 (−9.7 %) |
| RCE35 | 30 | −21.1 (**−38.6 %**) | 100 % | +0.34 | **+7.7** (29/30) | **+10.9** (19/30, p 0.20) | −15.2 (−11.6 %) |
| RCE50 | 30 | −23.5 (**−34.2 %**) | 100 % | +1.23 | **+8.8** (30/30) | **+17.8** (22/30, p 0.016) | −9.3 (−5.3 %, n.s.) |
| RCE75 | 29 | −31.7 (**−34.0 %**) | 100 % | −0.30 | **+9.3** (28/29) | +5.3 (15/29) | −26.0 (−9.8 %) |
| RCE100 | 27 | −29.0 (**−27.4 %**) | 100 % | +0.48 | +0.1 (16/27, p 0.44) | **+15.1** (24/27, p 5e-5) | −4.7 (−1.6 %, n.s.) |

**INS**

| Point | Incl | ΔvisRev60 /1000 (rel. gap) | In favour | ΔPchg (pp) | ΔbreachDL60 /1000 | Δlate60First /1000 | ΔTV5/served (s) |
|---|---|---|---|---|---|---|---|
| RCE10 | 30 | −3.2 (−26.9 %) | 87 % | −0.19 | +0.4 (n.s.) | −6.0 | −1.8 (−3.7 %) |
| RCE20 | 30 | −3.1 (−14.2 %) | 77 % | −0.47 | **+5.7** (30/30) | −8.3 | −4.7 (−6.2 %) |
| RCE35 | 30 | −3.4 (−10.4 %) | 80 % | −0.65 | **+7.4** (29/30) | −12.5 (22/30, p 0.016) | −7.6 (−6.2 %) |
| RCE50 | 30 | −1.5 (−3.9 %) | 53 % | +0.58 | **+7.9** (30/30) | +2.3 | −1.0 (−0.6 %) |
| RCE75 | 27 | −2.6 (−5.2 %) | 77 % | −0.02 | **+8.9** (27/27) | −2.9 | −2.7 (−1.1 %) |
| RCE100 | 14 (16 above range) | −0.2 (−0.3 %) | 54 % | +0.76 | +0.2 | +8.7 | +12.8 (+4.6 %) |

The α = 0 points (RCE0 = frozen ledger; EOHZ0 = p ≤ max(p0, e)) fall below the EOHZ range on 23–27 of 30 days and are not evaluable.

### 2.3 Labels (pre-stated rules, `DEVIATIONS.md` 16:45)

- **C9 G2 reversals: HOLDS.** 6/6 points have a relative gap ≤ −20 % with ≥ 75 % of days in favour (rule: ≥ 4). No point within ±10 %,
  none above zero.
- **C9 G2 P(change): HOLDS.** No point above +2 pp (largest +1.23 pp at α 50).
- **C9 overall (G2): HOLDS.**
- **C9 INS reversals (secondary): WEAKENED.** Three points (α 50, 75, 100) are within ±10 %. Only α 10 is below −20 %. C3 predicted this.
- **C9 INS P(change): HOLDS** (largest +0.76 pp).
- Not part of C9, and unfavourable to Rule C: **the deadline breach at matched service is higher** at every point except G2 α 10 (−2.1 /1000, n.s.)
  (INS 6/6, G2 5/6 positive means; day-consistent at α 20–75; ≈ 0 at α 10 and 100). Under G2 the **lateness vs the first promise is higher at matched service** at α 20, 35, 50, 100.

### 2.4 Secondary check: pooled 30-day-mean curves with a day bootstrap (10,000 resamples, seed `wf17-r6`)

| | α 10 | α 20 | α 35 | α 50 | α 75 | α 100 |
|---|---|---|---|---|---|---|
| G2 visRev60 rel. gap [95 % CI] | −53.5 % [−60.8, −46.2] | −39.3 % [−47.4, −31.6] | −37.7 % [−43.3, −32.9] | −33.5 % [−39.5, −27.8] | −31.7 % [−36.6, −27.5] | −26.5 % [−31.5, −21.8] |
| G2 ΔPchg (pp) | +0.24 [−0.70, 1.10] | +0.80 [−0.28, 1.92] | +0.58 [−0.26, 1.39] | +0.96 [−0.11, 1.99] | −0.04 [−0.88, 0.81] | +0.31 [−0.36, 1.14] |
| G2 ΔbreachDL60 /1000 | +0.7 [−5.1, 5.5] | +5.9 [4.6, 7.2] | +7.7 [6.2, 9.2] | +8.6 [6.9, 10.4] | +9.7 [8.3, 11.1] | +0.6 [−0.7, 1.7] |
| INS visRev60 rel. gap | −27.6 % [−36.0, −19.6] | −15.3 % [−21.8, −7.8] | −10.1 % [−14.0, −5.7] | −4.8 % [−8.3, −1.2] | −4.0 % [−8.7, 0.1] | NA (RCE100 serves more than EOHZ100 in 81 % of resamples) |

The pooled view agrees with the per-day view in sign and size at every point.

### 2.5 Shadow allocation: Elmi's own claim, and Rule C inside Shadow

**SHHZ (Shadow) vs EOHZ (Ordinary) at matched service: Elmi's P(change) result REPLICATES, but reversals go the other way.**

| Config | ΔPchg (pp), x 50 … 90 | ΔvisRev60 /1000 (rel.) | ΔTV5/served (rel.) | Δlate60First (rel.) | ΔbreachDL60 /1000 |
|---|---|---|---|---|---|
| G2 | −6.2 … −10.6 (−18 % … −43 %), 30/30 days at every x | **+16 … +20 (+20 % … +79 %)**, 27–30/30 days against | **+31 % … +106 %** | −8 % … −24 % | +13 … +39 (guaranteed riders have budget 0) |
| INS | −6.6 … −10.7 (−17 % … −47 %), 29–30/30 | **+8.7 … +15.5 (+20 % … +91 %)**, 24–30/30 against | **+36 % … +103 %** | −12 % … −24 % | +13 … +39 |

Reading: Shadow protects most riders completely (no change), so fewer riders see any change and fewer arrive late against their first
promise. The riders it allows to move absorb more back-and-forth. "Fewer riders changed" (Elmi's metric) and "less churn" are
different objectives in our model.

**SHRC vs SHHZ at matched service (prediction: lower on both reversals and Pchg): half-true.**
- G2 reversals: −7.6 … −25.9 /1000 (−14 % … −28 %), 80–100 % of days, at 5 evaluable points.
- INS reversals: no gain (−2.3 % … +4.7 %; 0/5 points lower with ≥ 75 % of days).
- Pchg: **worse** in both configs, +1.3 … +3.7 pp (G2) and +0.6 … +2.2 pp (INS).
- late60First: worse in G2 (+10 % … +15 %) and INS (+3.5 % … +6.6 %). Deadline breaches: better (−3.6 … −6.9 /1000 G2; −1.1 … −3.1 INS).
- At equal x, SHRC serves less than SHHZ at every x: −1.6 … −6.1 /day (INS), −8.9 … −14.5 /day (G2; wf15 S10: −9.5 at x*).
- SHRC90 lies below the SHHZ served range on 22 (INS) and 28 (G2) of 30 days and is not evaluable.

### 2.6 "Does it matter" quantities (RUNPLAN 2.4)

**At equal α (pre-specified; RCE(α) − EOHZ(α), per day):**

| α | G2 Δserved | G2 ΔvisRev60 (rel.) | G2 ΔbreachDL60 | ARR (/1000 served) | reversals avoided per rider lost | per extra deadline breach | INS Δserved | INS ΔvisRev60 (rel.) | INS ΔbreachDL60 |
|---|---|---|---|---|---|---|---|---|---|
| 10 | −2.4 | −7.5 (−56 %) | +3.1 | 11.2 | 3.1 | 2.4 | 0.0 | −2.2 (−28 %) | +2.8 |
| 20 | −4.6 | −10.9 (−44 %) | +4.4 | 15.4 | 2.4 | 2.5 | +2.7 | −1.7 (−12 %) | +3.9 |
| 35 | −4.8 | −17.4 (−41 %) | +6.1 | 23.5 | 3.7 | 2.9 | +1.8 | −2.0 (−8.5 %) | +5.1 |
| 50 | −6.7 | −21.1 (−38 %) | +7.0 | 27.6 | 3.2 | 3.0 | −0.2 | −1.5 (−5.0 %) | +6.3 |
| 75 | −4.4 | −26.7 (−35 %) | +7.9 | 33.6 | 6.1 | 3.4 | +0.7 | −1.3 (−3.3 %) | +7.2 |
| 100 | −7.8 (24/29 days) | −31.4 (−34 %) | +0.7 | 38.8 | 4.0 | 47 (Δ only +0.7/day, n.s.) | +1.0 | −0.6 (−1.2 %) | +0.1 |

- G2 service price at equal α stays inside the C2 band (0 to −8/day) at every α, but reaches −7.8/day at α 100 (as a5 V10).
- INS at α 35: 2.0 reversals avoided per day for 5.1 extra deadline breaches per day, i.e. **about 2.6 breaches per reversal avoided**.
- At equal α, late60First is not worse for Rule C (G2 +0.7 … −6.6/day, none day-consistent against Rule C; INS −3.7 … +1.8/day).

**At matched service (POST HOC arithmetic on the printed frontier means, `results/r6_posthoc_rates.txt`; no test):**

| G2 point | Reversals avoided /1000 | Extra deadline breaches /1000 | Extra late60First /1000 | Reversals avoided per extra breach | per extra late rider |
|---|---|---|---|---|---|
| RCE10 | 10.7 | −2.1 | −0.4 | no price | no price |
| RCE20 | 12.3 | 5.7 | 14.6 | 2.2 | **0.8** |
| RCE35 | 21.1 | 7.7 | 10.9 | 2.8 | **1.9** |
| RCE50 | 23.5 | 8.8 | 17.8 | 2.7 | **1.3** |
| RCE75 | 31.7 | 9.3 | 5.3 | 3.4 | 5.9 |
| RCE100 | 29.0 | 0.1 | 15.1 | ≈ no price | **1.9** |

Under INS, the matched-service exchange is 0.2–0.5 reversals avoided per extra deadline breach at α 20–75 (2–5 breaches per reversal),
with no lateness price at α 10–35.

What these numbers mean, and what they do not: under G2 at α 35 and matched service, about 21 of every 1000 served riders (about 15 a
day) are spared a ≥ 60-s reversal. In exchange, about 8 more per 1000 miss Elmi's deadline and about 11 more per 1000 arrive more than
60 s after their first promise. Whether a reversal is worth more to a rider than a minute of lateness against the first promise is a
literature or user-study question (wf17 research keys b1–b4). The simulation cannot answer it.

### 2.7 RUNPLAN R6 predictions (points meeting / evaluable)

| Prediction | Met |
|---|---|
| G2 RCE vs EOHZ reversals 30–55 % lower (α ≥ 10) | 5/6 (α 100: −27 %) |
| G2 deadline breach higher, +0.3 to +1 pp | 4/6 (α 10 and 100 ≈ 0) |
| G2 Pchg within ±1.5 pp | 6/6 |
| INS reversals 0 to −25 % | 5/6 (α 10: −27 %) |
| INS Pchg within ±1 pp | 6/6 |
| INS deadline breach higher | 6/6 positive means (α 10 and 100 not day-consistent) |
| SHHZ vs EOHZ Pchg lower (INS / G2) | 6/6 / 6/6 |
| SHRC vs SHHZ reversals lower (INS / G2) | 2/5 / 5/5 |
| SHRC vs SHHZ Pchg lower (INS / G2) | **0/5 / 0/5** (failed) |
| At equal x, SHRC serves less than SHHZ (INS / G2) | 6/6 / 6/6 |

### 2.8 Plot files

`plots/r6_G2_primary.svg`, `plots/r6_INS_primary.svg` (Pchg, visRev60 /1000, breachDeadline60 /1000 against served/day),
`plots/r6_*_secondary.svg` (TV5/served, pDispAny1), PNG renders of the four, and `plots/r6_ascii.txt` (character plots).
Blue = fence (EOHZ, SHHZ); orange = Rule C ledger (RCE, SHRC); solid + circle = uniform α; dashed + square = Shadow x; grey diamond = U.

---

## 3. R7 — alternative node draw (tests C7)

D10 × {w08, w17} × r0. P = wf14 node draw (the R1 plain rows, reused byte for byte); A = `--nodetag wf17-r7-alt1`.

| Contrast (per day) | wf14 nodes (P) | Alt nodes (A) | DD = Δ_A − Δ_P, mean (SE); n−/n+/ties |
|---|---|---|---|
| G2 visRev60, RCE35 − EOHZ35 | −16.2, **−41.0 %**, 10/10 days | −16.0, **−39.4 %**, 10/10 days | +0.2 (1.69); 4/3/3 |
| G2 served, RCE35 − EOHZ35 | −1.5 | +0.3 | +1.8 (3.41); 5/5/0 |
| G2 visRev60, RCE35 − NCE35 | −15.8, −40.4 % | −14.0, −36.3 % | +1.8 (3.08) |
| G2 served, RCE35 − NCE35 | −5.2 | −3.0 | +2.2 (3.83) |
| G2 breachDeadline60, RCE35 − EOHZ35 | +5.3 (10/10) | +6.7 (10/10) | +1.4 |
| G2 TV5/served, RCE35 − EOHZ35 | −15.9 s | −16.1 s | −0.2 |
| INS served, RCE35 − EOHZ35 | −1.4 | +3.2 | **+4.6 (2.23)**; 3/7/0, p 0.34 |
| INS visRev60, RCE35 − EOHZ35 | −2.1, −10.0 % | −1.5, −6.8 % | +0.6 |
| INS breachDeadline60, RCE35 − EOHZ35 | +4.1 | +5.4 (10/10) | +1.3 |
| INS Pchg, RCE35 − EOHZ35 | −0.27 pp | +0.45 pp (9/10 days) | +0.73 pp |
| SHRC − SHHZ served (INS / G2) | −6.4 / −6.1 | −5.7 / −5.2 | +0.7 / +0.9 |
| SHF − SHHZ served (INS / G2) | −42.8 / −36.7 | −33.8 / −34.2 | change 21 % / 7 % |
| SHHZ − EOHZ35 visRev60 (INS / G2) | +12.0 / +13.7 | +11.3 / +15.8 | −0.7 / +2.1 |

- **Label C7: HOLDS.** The G2 relative reversal cut moves by 1.6 pp (prediction ±10 pp, flip 15 pp). No alt-node day has
  ΔvisRev60 ≥ 0 (REVERSED needs ≥ 5/10). |mean DD served| is 1.8 (G2) and 4.6 (INS), under the 5/day flip line, and the INS DD has
  7/10 days of one sign (flip needs ≥ 8/10).
- **Prediction miss, reported:** the INS served DD (+4.6/day) exceeds the predicted ±3/day, as in R1 (+3.5/day with the adapter
  identity). In both re-draws Rule C looks a few riders/day better under INS than on the plain D10 cells, where it was −1.4/day.
  The R6 30-day value at α 35 is +1.8/day. The INS service difference between RCE35 and EOHZ35 is noise of a few riders per day around
  zero, not a stable sign.
- **Chaos per cell is larger than for the identity mode**: mean |served_A − served_P| is 6.9–10.4 per cell (max 44, G2 SHF71);
  0/20 cells are rider-identical (expected: almost every node moved). Effects at the day level are still stable.
- **C5 note:** the strict-fence gap SHF − SHHZ shrinks by 21 % (INS) and 7 % (G2) with the new nodes; under the 50 % finding line.
  SHF forced vehicle-decisions per day barely move (22,252 → 21,847 INS; 62,614 → 61,623 G2).

---

## 4. What R6 and R7 say about "do the traded-for benefits hold up, and do they matter?"

1. **Hold up (G2):** yes, and more strongly than the single-point evidence. The reversal benefit of the no-refund ledger survives
   the fairest comparison available here (matched service, whole α range, 30 days; 27 %–55 %, 90–100 % of days), the pooled bootstrap,
   and a complete node re-draw (R7). It does not cost Elmi's P(change).
2. **Hold up (INS, RideBound's real mode):** only weakly, and only at small budgets: −27 % at α 10, about −10 % at α 35, about 0 from
   α 50 upwards. The deadline price is there at every α from 20 to 75.
3. **Price, stated plainly:** Rule C pays on Elmi's deadline yardstick at every α except the extremes, in both modes. Under G2 at
   matched service, it also pays in lateness against the first promise (about 1 extra late rider per 1–2 reversals avoided at α 20–50).
   That lateness price does not appear in the equal-α comparisons used so far in the thesis; it needs to be reported.
4. **Shadow:** Elmi's P(change) gain replicates, but Shadow raises reversals and churn in our model. A thesis that adopts Elmi's
   P(change) as the yardstick would rank Shadow above Rule C; one that uses reversals ranks it below. The choice of yardstick decides
   the ranking, so the thesis has to argue for it with rider evidence, not with simulation.
5. **Matter to riders:** not answerable by simulation. §2.6 gives the exchange rates (reversals avoided per rider lost, per deadline
   breach, per late rider) that the research keys need.

## 5. Limits (stated so that silence is not read as support)

- All 30 R6 days and all 10 R7 days are **seen** (wf14/wf15/wf16). R6 and R7 replicate nothing on fresh data; the fresh-day replication
  of C1–C4 is R3-X in the r1 key.
- One density / fleet / ride-limit regime (base world), r0 only, w08 + w17 only.
- The α grid has gaps of 10–25 pp; matched-service values are linear interpolations between grid points on each day.
- breachDeadline60 is measured against each arm's own budget, so along a curve the yardstick changes; at α = 0 it equals late60First.
- The "evaluable ≥ 10 days" rule, the relative-gap definition and the Pchg / Elmi labels were fixed by this key before the runs
  (RUNPLAN gave only part of the rules).
- INS RCE100 is inside the EOHZ range on only 14/30 days (RCE100 serves slightly more than EOHZ100); SHRC90 is outside the SHHZ range on
  22–28 days. These points carry little weight.
- R7 tests one alternative node draw, not a distribution of draws. Elmi et al. 2026 footnote 12 was not re-read (RUNPLAN R7 says so too).
- Nothing here touches the real RideBound C# (no G2 there; no per-rider budget).

## 6. Files

- Plan check, rules, deviations: `DEVIATIONS.md`; code: `code/` (`RUNBOOK.sh` = commands as run), `CODE-HASH.txt`; analysis:
  `analysis/` (`r6_analyze.js`, `r6_plot.js`, `r7_analyze.js`, `r7_cells_check.js`, hashed in `ANALYSIS-HASH.txt` before the runs;
  `r6_posthoc_rates.js` is post hoc).
- Results: `results/r6.ndjson` (3,240 rows), `results/r6_tables.{txt,json}`, `results/r6_posthoc_rates.txt`, `results/r7.ndjson` (320 rows),
  `results/r7_baseline_plain.ndjson` (R1 plain rows), `results/r7_tables.{txt,json}`, run footers in `results/*.runs.ndjson`.
- Plots: `plots/`. Cells: `cells/ggen/` (G-GEN), `cells/alt1/` (R7 alt nodes). Checks: `work/theta_check.json`, `work/r7_cells_check.json`,
  `work/gates/GATES.ndjson`. Logs: `logs/`.
