# wf17 r2-penalty-regime: checks R4 (G2 friction) and R5 (regime grid) (EXPLORATORY, POST-AUDIT)

Label: **wf17-exploratory-post-audit**. Nothing here is confirmatory. There is no Holm correction; p-values are descriptive.
All days used here (D60, D30) are wf14 confirmatory days that have been seen many times before (wf14, wf15, wf16 auditors).
Plan: `wf17/a0-plan/RUNPLAN.md`, sha256 `ef953c3b…2f12`, verified against `RUNPLAN-HASH.txt` before anything was copied or run.
Deviations: `DEVIATIONS.md`. Gates: `work/gates/GATES.md` and `GATES.ndjson`. Code hashes: `CODE-HASH.txt`. Analysis hashes, written
before each production run: `ANALYSIS-HASH.txt`. Full tables: `results/r4_tables.txt|json`, `results/r5_tables.txt|json`,
post-hoc table `results/r4_posthoc.txt|json`. Rows: `results/r4.ndjson` (3,600), `results/r5.ndjson` (2,520).

Unit = day = w08 + w17 at resample r0. Each test is an exact two-sided sign test, ties dropped. Intervals are 95 % t-intervals over days.
visRev60 = served riders with two consecutive opposite-sign visible drop-ETA changes of at least 60 s each. "Rule C" = RCE35; "fence" =
EOHZ35 (Elmi Ordinary Control with the Hoerl-Zwick floor, the fair primary comparator after the audit); NCE35 = net ledger with refunds.
"Rel." = 1 − Σ(RCE35)/Σ(EOHZ35). "Favour" = share of non-tied days on Rule C's side (RUNPLAN 2.1).

## 0. Bottom line

Each verdict is the label given by the pre-stated rule (RUNPLAN R4/R5 plus the pre-run interpretations in `DEVIATIONS.md`, 15:50 and
15:52). Unfavourable and null results are listed with the same weight as favourable ones.

| Check | Conclusion tested | Verdict | Key number |
|---|---|---|---|
| R4 | C1: under G2, Rule C cuts ≥ 60-s reversals vs the fence, under realistic relocation friction | **HOLDS in 6/6 configs (robust)** | −41.8 % (−16.6/day, 60/60 days) at π 0 → **−28.6 % (−8.7/day, 58/60)** at π 300 s; T16 ≈ T8 |
| R4 | C2: the service price is small (≤ 8/day, ≤ 1 %) | **HOLDS in 6/6 configs (robust)**; the π = 300 s rule does not trigger | −4.8 to −6.3 served/day (0.65–0.86 %), 38–45 of 60 days negative. **Every point estimate is a loss**, and the CIs exclude 0 |
| R4 | (descriptive) exchange rate under friction | **Gets worse by about half** | per 1,000 served: 22.6 reversals avoided at π 0 vs 11.8 at π 300 s. Per rider not served: 3.4 → 1.6. Per extra Elmi-deadline breach: 2.9 → 1.7 |
| R4 | (descriptive) is g2Targets 16 different from 8? | **No** | DD of the Rule C effect, T16 − T8: +0.3, −0.2, +0.2 reversals/day (26–42 of 60 days tied) |
| R4 post hoc | (not pre-specified) how much of the cut does a plain relocation penalty buy? | **no label (post hoc)** | Fence + 300-s penalty vs fence: **−9.3 reversals/day (−23.5 %) at −0.7 served/day (n.s.)**, which is 56 % of Rule C's cut at about 1/7 of its service price. Its own price is +2.6/day riders late > 60 s vs the first promise |
| R5 | C8: the direction of C1 does not depend on the regime; its size does | **HOLDS** (all 7 variants within 20–60 %, ≥ 75 % of days) | G2 rel. 28.0 % (P256) to 41.4 % (B0); 86–100 % of days |
| R5 | C1 per regime | **HOLDS 7/7 (robust)** | absolute −3.2/day (P256) to −26.9/day (ride 2.5×) |
| R5 | C2 per regime | **HOLDS 7/7 (robust)** | G2 served −0.7 to −4.8/day |
| R5 | C3: under INS (RideBound's real mode) Rule C ≈ Elmi | **HOLDS 7/7** | INS served −0.7 to +1.8/day (no CI excludes 0); **reversals −1.8 to −12.3 %, only −0.1 to −2.4/day**; at P256 the cut is null (−0.13/day, p 0.51) |
| R5 | C4: the fence wins on Elmi's deadline yardstick (unfavourable to Rule C) | **HOLDS 7/7**, i.e. Rule C is worse in every regime | INS: +1.9 to +7.2 extra deadline breaches/day, 26–30 of 30 days. G2: +1.9 to +6.8/day |
| R5 | (prediction, no label) P256 effect "similar per 1,000 served" | **Prediction failed** | G2 −8.3 per 1,000 served at P256 vs −23.2 at B0: the effect per rider is about 1/3 at half density |

**In one paragraph.**
- The surviving claim C1 (under reassignment, the no-refund ledger cuts promise reversals vs the fence) survives every friction setting and
  every regime tried here. Its direction is never in doubt: at least 86 % of days favour it, and 28–42 % fewer reversals.
- Its size is not fixed. A 300-s relocation penalty halves the absolute cut (−16.6 → −8.7 per day). Low density (P256) cuts the
  per-1,000-served benefit to about a third. A shorter ride limit (1.5×) cuts it by more than half.
- The price stays small in riders (≤ 0.9 % of served), but it is always a loss, and the exchange rate worsens with friction. At
  π = 300 s Rule C buys about 1.6 reversals avoided per rider not served and 1.7 per extra Elmi-deadline breach.
- **Post hoc, and not pre-specified:** a plain 300-s relocation penalty on the fence buys more than half of Rule C's G2 reversal cut at
  almost no service cost. Rule C on top of that penalty still cuts a further 28.6 % (−8.7/day) for −5.4 riders/day.
- Under insertion only (RideBound's real mode) Rule C buys very little: −0.1 to −2.4 reversals/day, about 0.3–3.2 per 1,000 served. It
  pays 1.9–7.2 extra Elmi-deadline breaches per day in every regime, that is, **about 2 to 14 extra breaches per reversal avoided**.

---

## 1. What was run

### 1.1 Jobs (one heavy job at a time; 6 workers; no other `node` job running; no suspend)

| Check | Cells | Runs | Wall | CPU | Peak RSS | Max heartbeat gap | Errors |
|---|---|---|---|---|---|---|---|
| R4 | D60 × {w08, w17} × r0 = 120 cells (wf14 P512) × 6 configs × 5 arms | 3,600 | 20.7 min | 9,231 s | 835 MB | 10.1 s | 0 |
| R5 | D30 × {w08, w17} × r0 = 60 cells × 7 variants × (G2 4 + INS 2 arms) | 2,520 | 9.5 min | 4,408 s | 909 MB | 10.0 s | 0 |

- **R4 configs:** G2 `{ reopt: { g2: 99, g2Targets: T, g2PenaltyMs: π }, lock: 'ratchet' }` for T ∈ {8, 16} and π ∈ {0, 60, 300} s.
  Arms U, EO35, EOHZ35, RCE35, NCE35.
- **R5 variants**, one factor at a time from B0 (P512, 62 vehicles, ride limit 2.0×, dwell 0):
  - V-P256: P256 cells, 42 vehicles;
  - V-F56 and V-F68: 56 and 68 vehicles;
  - V-R15 and V-R25: ride limit 1.5× and 2.5×;
  - V-D24: dwell 24 s per stop.
  - Arms: G2 U, EOHZ35, RCE35, NCE35; INS EOHZ35, RCE35.
- **P256 cells** were generated with `gen17.js --fmax 512:96,256:56 --resample 0` and default tags. They are nested in P512: 60/60 cells,
  and every P256 request is in the same cell's P512 set.
- **Deviation (RAM):** free RAM was 3.1–3.6 GB before R5, below the plan's 4 GB. Measured peak RSS was < 1 GB, and the worker count does
  not change any result. The other deviations are in `DEVIATIONS.md`.

### 1.2 Gates (all PASS)

| Gate | Result |
|---|---|
| Plan hash; panels D60/D30 vs RUNPLAN 1.3 | PASS |
| R5 generator gate: regenerated P512 (JSON, `.bin`, `.prov.json`) vs wf14, D30 | 180/180 sha256-identical |
| Pre-run G-ENG-sub (R4 T8P0, 4 cells × 5 arms vs wf14 main) / G-P2 (π 60 / 300 s vs a5 V11 / V12) | 20/20; 12/12, 12/12 |
| Pre-run R5 B0 (4 cells vs wf14 main) / V-R15, V-R25 (2 cells vs a5 V1 / V2) | 24/24; 8/8, 8/8 |
| **R4 post-run:** T8P0 rows vs wf14 main G2 r0, all 5 arms × 120 cells | **600/600** |
| R4 post-run (extra): T8P60 / T8P300 EO35/EOHZ35/RCE35 on D30 vs a5 V11 / V12 | 180/180, 180/180 |
| **R5 post-run:** B0 rows vs wf14 main r0 (6 arm-configs × 60 cells) | **360/360** |
| **R5 post-run:** V-R15 / V-R25 EOHZ35/RCE35 (INS + G2) vs a5 V1 / V2 | **240/240, 240/240** |
| R5 post-run (extra): B0 G2 rows vs R4 T8P0 rows on the same cells | 240/240 |

---

## 2. R4: G2 friction (tests C1, C2). D60, 60 days

### 2.1 Primary contrasts and labels

| Config (T, π) | Rel. visRev60 vs fence | ΔvisRev60/day [95 % CI] | Favour | **C1** | Δserved/day [95 % CI] | Days with Δserved < 0 | **C2** | Rel. vs NCE35 | Δserved vs NCE35 |
|---|---|---|---|---|---|---|---|---|---|
| 8, 0 | 41.8 % | −16.57 [−18.37, −14.76] | 100 % (60−/0+) | HOLDS | −4.92 [−7.51, −2.32] | 41/60 | HOLDS | 41.1 % | −7.18 |
| 8, 60 s | 39.1 % | −14.32 [−15.99, −12.64] | 100 % | HOLDS | −5.27 [−8.36, −2.17] | 42/60 | HOLDS | 37.1 % | −7.47 |
| 8, 300 s | **28.6 %** | **−8.67 [−9.98, −7.36]** | 97 % (58−/2+) | HOLDS | −5.42 [−7.85, −2.99] | 40/60 | HOLDS | 25.7 % | −5.88 |
| 16, 0 | 41.1 % | −16.27 [−18.06, −14.47] | 100 % | HOLDS | −5.75 [−8.46, −3.04] | 41/60 | HOLDS | 41.2 % | −7.02 |
| 16, 60 s | 39.1 % | −14.50 [−16.24, −12.76] | 100 % | HOLDS | −6.28 [−9.48, −3.09] | 45/60 | HOLDS | 37.4 % | **−8.20** |
| 16, 300 s | **28.1 %** | **−8.52 [−9.92, −7.12]** | 93 % (56−/4+) | HOLDS | −4.78 [−7.34, −2.22] | 38/60 | HOLDS | 25.8 % | −5.83 |

- **C1 robust:** HOLDS in 6/6 configs and REVERSED in none. **C2 robust:** HOLDS in 6/6. The π = 300 s rule ("C2 fails at 300 s for both
  T → state the price as about 1 %") does not trigger.
- **Unfavourable details, reported with the same weight.**
  - The service price is a loss in all 6 configs: every CI lies below 0. It is 0.65–0.86 % of the fence's served riders, a little larger
    than the predicted −1.3 to −4.8/day at T8/π0.
  - Against NCE35 (refund ledger) the price is larger: −5.8 to −8.2/day, i.e. 0.8–1.1 %. At T16/π60 it is −8.20/day [−10.86, −5.54] with
    47/60 days negative. That would meet the C2 "fails" threshold if C2 were measured against NCE35; it is measured against the fence,
    so this changes no label.
  - NCE35 itself cuts almost no reversals vs the fence (−0.5 to −1.2/day). The benefit therefore comes from the no-refund ledger, not
    from having a ledger at all.

### 2.2 Secondary metrics, RCE35 − EOHZ35 (mean per day or pp; n−/n+ of 60 days)

| Config | TV5 (s per served) | Pchg (pp) | pDispAny1 (pp) | late60First (pp) | **breachDeadline60 (per day)** | relocations (per day, rel.) |
|---|---|---|---|---|---|---|
| 8, 0 | −15.5 (60−/0+) | −0.53 (39−/21+) | −1.10 | +0.26 (21−/39+) | **+5.77 (1−/56+)** | −15.8 (−52 %) |
| 8, 60 s | −13.8 (60−/0+) | −0.52 (41−/19+) | −1.15 | +0.12 | **+5.67 (1−/57+)** | −13.9 (−55 %) |
| 8, 300 s | −8.4 (56−/4+) | −0.27 (33−/27+) | −0.63 | −0.10 | **+5.17 (1−/54+)** | −7.7 (−55 %) |
| 16, 0 | −15.7 | −0.57 | — | +0.26 | **+5.68** | −15.9 (−51 %) |
| 16, 60 s | −13.9 | −0.61 | — | +0.17 | **+5.63** | −14.0 (−54 %) |
| 16, 300 s | −8.7 (55−/5+) | −0.35 | — | −0.16 | **+5.15 (0−/56+)** | −7.8 (−53 %) |

- Rule C does not pay in Elmi's P(change). Pchg is 0.3–0.6 pp *lower* than the fence's.
- Rule C pays on Elmi's deadline yardstick in every config: about +0.7–0.8 pp of served riders breach p0 + B_r + 60 s, on 54–57 of 60 days.
- Rule C roughly halves G2 relocations in every config. The reversal cut and the suppressed relocations go together. Under G2 the ledger
  mainly works by refusing relocations that would move a promise.
- **By window** (descriptive): the relative cut is much larger in w08 (57–70 %) than in w17 (20–30 %). In absolute terms the two windows
  are similar at π 0 (about −8.4 and −8.1/day). At π 300 s they are −3.7 (w08) and −4.9/day (w17).
- Levels: the ledger-less arm U has 89 reversals/day (111 per 1,000 served). Any budgeted arm has about 40 (54/1,000), and Rule C 23
  (32/1,000). The budget itself does most of the work; the ledger type adds the C1 cut on top.

### 2.3 Friction: difference-in-differences of the Rule C effect (descriptive; DD_d = Δ_d(config) − Δ_d(reference))

| Comparison | DD visRev60/day | DD served/day | DD breachDeadline60/day |
|---|---|---|---|
| T8: π 60 vs π 0 | +2.25 [1.10, 3.40], 36+/16− | −0.35 (n.s.) | −0.10 |
| T8: π 300 vs π 0 | **+7.90 [6.42, 9.38], 55+/4−** | −0.50 (n.s.) | −0.60 |
| T16: π 300 vs π 0 | **+7.75 [6.38, 9.12], 55+/5−** | +0.97 (n.s.) | −0.53 |
| T16 vs T8 at π 0 / 60 / 300 | +0.30 / −0.18 / +0.15 (26–42 ties) | −0.83 / −1.02 / +0.63 (n.s.) | ≈ 0 |

- Friction removes about half of the reversal benefit (+7.8 to +7.9/day of the −16.3 to −16.6) and leaves the service price unchanged.
- Doubling the G2 target set (8 → 16) changes nothing measurable.

### 2.4 Predictions (RUNPLAN R4) vs observed

| Config | Predicted | Observed | Met? |
|---|---|---|---|
| T8, π 0 | −16 to −18/day (−40 to −44 %), 60/60; served −1.3 to −4.8 | −16.6 (−41.8 %), 60/60; served −4.92 | yes / served slightly worse than predicted |
| T8, π 60 | ≈ −15 (≈ −39 %); served ≈ −4 | −14.3 (−39.1 %); served −5.27 | yes / served worse |
| T8, π 300 | ≈ −10 (≈ −30 %); served ≈ −6.5 | −8.7 (−28.6 %); served −5.42 | roughly (cut smaller); served better |
| T16 | larger absolute cut than T8 at the same π; rel. 35–50 %; served −2 to −8 | same as T8 (DD ≈ 0); 41 / 39 / **28 %**; served −4.8 to −6.3 | **no** (no T16 gain; 28 % at π 300 is below 35 %); served yes |
| vs NCE35 | ≈ −40 %; served ≈ −5 | −41 % at π 0, shrinking to −26 % at π 300; served −5.8 to −8.2 | partly (cut shrinks with π; price larger) |

### 2.5 POST HOC (decided after reading 2.1–2.3; descriptive, no label): could a plain relocation penalty do the job?

The R4 levels show that the 300-s penalty itself lowers the fence's reversals (39.7 → 30.4/day), while Rule C's level hardly moves
(23.1 → 21.7). The contrasts below are paired over the same 60 days but taken across configs (`results/r4_posthoc.txt`).

| Contrast (T8) | ΔvisRev60/day | Δserved/day | ΔbreachDeadline60/day | Δlate60First/day | Δrelocations/day |
|---|---|---|---|---|---|
| Fence + 300-s penalty − fence (π 0) | **−9.30 [−10.77, −7.83] (−23.5 %), 54−/5+** | **−0.68 [−3.46, 2.10] (n.s.)** | +0.92 [0.38, 1.46] | **+2.57 [0.19, 4.95], 40+/19−** | −16.0 (−53 %) |
| Rule C (π 0) − fence (π 0) | −16.57 (−41.8 %) | −4.92 | +5.77 | +0.35 (n.s.) | −15.8 |
| Rule C + 300-s penalty − fence (π 0) | −17.97 (−45.3 %) | −6.10 | +6.08 | +0.15 (n.s.) | −23.7 (−79 %) |
| Rule C + penalty − fence + penalty (marginal value of Rule C under friction) | −8.67 (−28.6 %) | −5.42 | +5.17 | −2.42 (n.s.) | −7.7 |

T16 gives the same picture: the penalty alone gives −9.27/day with served −1.58 (n.s.), deadline breaches +0.78/day, and late60First
+2.17/day.

- **Exchange rates.**
  - The penalty alone avoids about 13.7 reversals per rider not served (Rule C: 3.4) and about 10 per extra deadline breach (Rule C: 2.9).
  - It costs one extra rider late by more than 60 s against the first promise per 3.6 reversals avoided. Rule C pays essentially nothing
    on that metric.
- **Reading.** Under G2, a substantial part of the reversal problem is relocation churn, and a relocation penalty is a cheaper first lever
  than Rule C. Rule C keeps a real marginal benefit on top of it: −8.7/day, 58/60 days, for −5.4 riders/day.
- **Limits of this comparison.**
  - It is post hoc.
  - π was designed as "friction", not as a tuned policy knob.
  - No penalty value other than 60 and 300 s was tried.
  - This should be treated as a hypothesis for the thesis discussion, not as a result.

---

## 3. R5: regime grid (tests C8, and C1–C4 per regime). D30, 30 days

### 3.1 G2 (reassignment): Rule C vs fence

| Variant | Fence served/day | Rel. visRev60 | ΔvisRev60/day [CI] | Per 1,000 served | Favour (n−/n+/ties) | **C1** | Δserved/day [CI] | Days < 0 | **C2** | Rel. vs NCE35 | Δserved vs NCE35 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| B0 | 742.0 | 41.4 % | −17.43 [−20.27, −14.59] | −23.2 | 100 % (30/0/0) | HOLDS | −4.77 [−8.56, −0.97] | 20/30 | HOLDS | 41.6 % | −6.77 |
| V-P256 (42 veh) | 385.5 | **28.0 %** | **−3.23 [−4.46, −2.00]** | **−8.3** | 86 % (25/4/1) | HOLDS | −0.67 [−2.69, 1.35] | 17/30 | HOLDS | 26.0 % | −0.73 |
| V-F56 | 689.7 | 39.4 % | −15.17 [−18.08, −12.25] | −21.7 | 100 % | HOLDS | −3.37 [−6.77, 0.04] | 18/30 | HOLDS | 37.4 % | −5.17 |
| V-F68 | 782.0 | 41.2 % | −18.17 [−21.14, −15.20] | −22.9 | 100 % (29/0/1) | HOLDS | −4.83 [−8.50, −1.17] | 20/30 | HOLDS | 39.8 % | −4.17 |
| V-R15 | 679.5 | 41.2 % | −7.10 [−8.84, −5.36] | −10.2 | 97 % (28/1/1) | HOLDS | −1.67 [−4.29, 0.96] | 18/30 | HOLDS | 37.2 % | −3.17 |
| V-R25 | 781.9 | 40.0 % | −26.90 [−29.53, −24.27] | −34.0 | 100 % | HOLDS | −4.67 [−9.81, 0.48] | 19/30 | HOLDS | 37.1 % | −6.20 |
| V-D24 | 707.9 | 39.7 % | −15.57 [−17.80, −13.33] | −21.7 | 100 % | HOLDS | −2.43 [−6.76, 1.89] | 21/30 | HOLDS | 36.7 % | −4.00 |

- **C8 = HOLDS.** Every variant lies within [20 %, 60 %] with ≥ 75 % of days in favour; none is weakened or reversed.
- **C1 robust** (7/7). **C2 robust** (7/7; the service price is −0.7 to −4.8/day, and every point estimate is a loss).
- **Size is regime-dependent.**
  - Density: DD of P256 vs B0 is +14.2/day [11.1, 17.3], 30/30 days; per 1,000 served it is +15.0 [10.0, 19.9]. The "similar per 1,000
    served" prediction fails: the per-rider benefit at half density is about a third.
  - Ride limit: 1.5× gives −7.1/day, against −26.9/day at 2.5×.
  - Fleet size and dwell do not change the effect measurably.
- G2 Elmi's yardstick: Rule C has +1.9 to +6.8 more deadline breaches per day in every variant (26–30 of 30 days). Pchg is 0.3–0.6 pp lower.

### 3.2 INS (insertion only, RideBound's real mode): Rule C vs fence

| Variant | ΔvisRev60/day (rel.), n−/n+/ties, p | Per 1,000 served | Δserved/day [CI] | **C3** | **ΔbreachDeadline60/day**, days ≤ 0 | **C4** | Reversals avoided per extra deadline breach |
|---|---|---|---|---|---|---|---|
| B0 | −2.00 (8.5 %), 21/3/6, p 0.0003 | −2.8 | +1.80 [−1.54, 5.14] | HOLDS | **+5.10**, 2/30 | HOLDS | 0.39 |
| V-P256 | **−0.13 (1.8 %), 6/3/21, p 0.51** | −0.4 | +1.13 [−0.06, 2.33] | HOLDS | **+1.90**, 4/30 | HOLDS | **0.07** |
| V-F56 | −1.57 (6.8 %), 22/7/1 | −2.4 | +1.73 [−0.81, 4.28] | HOLDS | **+5.30**, 1/30 | HOLDS | 0.30 |
| V-F68 | −2.43 (10.1 %), 23/6/1 | −3.2 | +1.10 [−1.35, 3.55] | HOLDS | **+5.07**, 0/30 | HOLDS | 0.48 |
| V-R15 | −1.27 (12.3 %), 19/5/6 | −1.8 | −0.70 [−2.66, 1.26] | HOLDS | **+2.63**, 2/30 | HOLDS | 0.48 |
| V-R25 | −1.83 (5.2 %), 20/7/3 | −2.4 | +1.27 [−1.60, 4.13] | HOLDS | **+7.20**, 0/30 | HOLDS | 0.25 |
| V-D24 | −1.60 (7.5 %), 21/5/4 | −2.3 | +0.90 [−1.11, 2.91] | HOLDS | **+5.57**, 0/30 | HOLDS | 0.29 |

- **C3 HOLDS** in 7/7: Rule C ≈ Elmi on service under INS.
  - Its reversal benefit is small: 0.4–3.2 per 1,000 served. At low density it is null.
- **C4 HOLDS** in 7/7. This is **unfavourable to Rule C**: on Elmi's own yardstick Rule C is worse in every regime.
  - Under INS it costs 2.1 to 14 extra deadline breaches for each reversal it avoids.
  - TV5 and Pchg under INS: no consistent difference (|ΔTV5| ≤ 1.4 s per served; Pchg −0.1 to +0.5 pp).

### 3.3 Predictions (RUNPLAN R5) vs observed

| Variant | Predicted G2 rel. | Observed | Met? |
|---|---|---|---|
| V-R15 | ≈ 40 % (≈ −7/day) | 41.2 % (−7.1) | yes |
| V-R25 | ≈ 39 % (≈ −26/day) | 40.0 % (−26.9) | yes |
| V-F56 | 36–42 % | 39.4 % | yes |
| V-F68 | 42–47 % | 41.2 % | just below |
| V-P256 | 25–40 %, −3 to −6/day, similar per 1,000 served | 28.0 %, −3.2/day, **−8.3 vs −23.2 per 1,000** | rel./abs. yes; **per 1,000 no** |
| V-D24 | 30–45 % | 39.7 % | yes |
| all | G2 served 0 to −5/day; INS \|Δserved\| ≤ 4; INS ΔvisRev60 −0.2 to −2.5; INS Δbreach > 0 | −0.7 to −4.8; −0.7 to +1.8; −0.13 to −2.43; +1.9 to +7.2 | yes, except P256 INS reversals (−0.13, smaller than predicted) |

---

## 4. "Does it matter" quantities (RUNPLAN 2.4; RCE35 vs EOHZ35; descriptive, no threshold)

| Setting | Reversals avoided per 1,000 served (ARR) | Per rider not served (ER_served) | Per extra Elmi-deadline breach (ER_deadline) | Per extra rider late > 60 s vs first promise (ER_late) |
|---|---|---|---|---|
| G2, T8 π 0 (D60) | 22.6 | 3.4 | 2.9 | 47 (late60First +0.35/day, n.s.) |
| G2, T8 π 60 s | 19.5 | 2.7 | 2.5 | no lateness price |
| G2, T8 π 300 s | **11.8** | **1.6** | **1.7** | no lateness price |
| G2, T16 π 300 s | 11.6 | 1.8 | 1.7 | no lateness price |
| G2, B0 (D30) | 23.5 | 3.7 | 2.9 | no lateness price |
| G2, P256 | 8.4 | 4.9 | 1.7 | no lateness price |
| G2, ride 1.5× | 10.4 | 4.3 | 2.3 | no lateness price |
| G2, ride 2.5× | 34.4 | 5.8 | 3.9 | no lateness price |
| **INS, B0** | **2.7** | no service price (+1.8/day) | **0.39** | no lateness price |
| **INS, P256** | **0.35** | no service price | **0.07** | no lateness price |
| (post hoc) fence + 300-s penalty vs fence, G2 T8 | 12.7 | ≈ 13.7 | ≈ 10 | ≈ 3.6 |

Simulation can only give these rates. Whether 12–23 fewer reversals per 1,000 riders (G2), or 0.4–3 (INS), is worth about one extra
Elmi-deadline breach per 1.7–2.9 reversals (G2), or 2–14 breaches per reversal (INS), depends on how riders weigh a promise reversal against
a late arrival beyond the deadline. That is a question for the literature keys b1–b4 or a user study. This check does not answer it.

---

## 5. Caveats

- All results come from the emulator (wf15 engine plus the opt-in P2 friction knob), on Manhattan Via 2019 days. The days were already
  seen (wf14 confirmatory D60/D30), only r0 was used, and the runs are exploratory.
- G2 (reassignment after the promise) does not exist in the real RideBound (vehicle lock, a3 D1). C1, C2 and C8 describe a possible
  system, not the deployed one. In the deployed mode (INS) the benefit is small and the deadline price is consistent.
- π is an additive cost in the G2 objective (ms of vehicle time). It is not calibrated to any observed relocation friction; 300 s is
  "large" by assumption.
- visRev60 counts riders, not reversal events, and uses a 60-s threshold. Smaller jitter is outside this metric (TV5 covers it in part).
- breachLedger = 0 for Rule C holds by construction and is not counted as a result. The tables report it only for completeness.
- The R4 post-hoc comparison (2.5) was not pre-specified; it is labelled post hoc and changes no verdict.
