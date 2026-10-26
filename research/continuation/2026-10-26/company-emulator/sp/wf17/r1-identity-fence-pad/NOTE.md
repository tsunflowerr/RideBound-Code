# wf17 r1-identity-fence-pad: checks R1, R2, R3 (EXPLORATORY, POST-AUDIT)

Label: **wf17-exploratory-post-audit**. Nothing here is confirmatory. There is no Holm correction; p-values are descriptive.
Plan: `wf17/a0-plan/RUNPLAN.md`, sha256 `ef953c3b…2f12`; I verified it against `RUNPLAN-HASH.txt` before starting.
Deviations are listed in `DEVIATIONS.md`. Gates are listed in `work/GATES.md` (pre-run) and §1.2 (post-run). Code hashes are in
`CODE-HASH.txt`. Analysis hashes, written before each production run, are in `ANALYSIS-HASH.txt`. The full tables are
`results/r{1,2,3}_tables.txt` and `.json`.

Unit = day. Each test is an exact two-sided sign test, with ties dropped. Intervals are 95 % t-intervals over days. "n−/n+" counts days.
Y = share of served riders late by more than 60 s against the *shown first promise*. E = share early by more than 60 s.
visRev60 = riders with two opposite-sign visible changes of at least 60 s each.

## 0. Bottom line

Each verdict is the decision label given by the pre-stated rule (RUNPLAN plus the pre-run interpretations in `DEVIATIONS.md`).
Negative and inconclusive results are listed with the same weight as positive ones.

| Check | Conclusion tested | Verdict | Key number |
|---|---|---|---|
| R3 | C6 open loop: a modest forecast-specific gain over an equal-time pad exists | **HOLDS** | display − pad(k_w): −1.16 pp (F14, 12/14 days) and **−1.74 pp (F16 fresh, 15/16)**. Smaller than wf7's −2.88; the pad alone gives 73–80 % of the display gain |
| R3 | C6 closed loop: the wf14 system lateness gain is "mostly promise length" | **INCONCLUSIVE** (panels disagree: F14 WEAKENED, F16 INCONCLUSIVE). The "mostly length" reading is not supported | CL − equal-length pad: −2.84 pp (14/14) and −1.98 pp (15/16). w08 −7 to −8.5 pp; **w17 +2.7 pp: the forecast hurts the evening** |
| R3-X | C1 G2 reversal cut, on **fresh** days | **HOLDS** | visRev60 −12.6/day, **−38.5 %, 16/16 days** |
| R3-X | C2 small service cost | **HOLDS** | G2 served −2.6/day [−5.4, +0.2] |
| R3-X | C3 INS ≈ Elmi | **HOLDS** | INS served +2.3/day; reversals −8 % |
| R3-X | C4 the fence wins on Elmi's deadline yardstick | **HOLDS** (unfavourable to Rule C) | deadline breaches +4.5 (INS) / +5.2 (G2) per day, 16/16 and 15/16 days |
| R2 | C5 the SHRC > SHF gap is mostly a strict-fence rounding artefact | **INCONCLUSIVE** by rule. Substantively mostly true | 5-s tolerance removes 58 % (INS; ρ 0.416 vs threshold 0.40) and 82 % (G2) of the gap. In Elmi's flat world 1 s removes **all** of it |
| R2 | EO35 barely affected | **HOLDS** | +1.6 / +1.0 served/day at 5 s |
| R2 | SHHZ is a fair primary comparator | **"real in our model, not in Elmi's"** | SHF at 5 s still 16–20/day below SHHZ under traffic; equal to it in the flat world |
| R1 | C7 the effects do not depend on the identity / rounding mode | **HOLDS** | G2 reversal cut 41.0 % (plain) vs 43.4 % (adapter), 10/10 days each. Served DD +2.7 (G2), +3.5 (INS)/day |
| R1 | C5 note: does the strict-fence gap depend on the rounding mode? | **No** (not a finding) | SHF − SHHZ gap changes by 1 % (INS) and 3 % (G2) |

**In one paragraph.**
- Under reassignment (G2), the surviving claim C1 replicates on 16 never-simulated days: −38.5 % reversals, 16/16 days. It is unchanged by
  the identity mode, and the service price (C2) stays small.
- Under insertion only (INS), RideBound's real mode, Rule C buys little: −8 to −10 % reversals, about 2 per 1,000 served. It pays about
  3 extra Elmi-deadline breaches per reversal avoided.
- The forecast matters more than the audit allowed:
  - Open loop, it beats an equal-length pad by about 1.2–1.7 pp.
  - Closed loop, it beats an equal-length pad in the morning by 7–8.5 pp, but it harms the evening.
  - In closed loop it also cuts visible reversals by 23–48 % at no service cost. Under INS that is about 4× Rule C's effect.
- The wf15 strict-fence service gap is mostly (G2) or largely (INS) millisecond rounding. In Elmi's own constant-time world it is all
  rounding.

---

## 1. What was run

### 1.1 Jobs (one heavy job at a time; ≤ 6 workers, except R1 adapter with 3; no other `node` job running; no suspend)

| Check | Cells | Runs | Wall | CPU | Peak RSS | Max heartbeat gap |
|---|---|---|---|---|---|---|
| R3 | F14 (84 cells, seen) + F16 (96 cells, **fresh**, generated here) | 3,060 | 11.1 min | 5,836 s | 963 MB | 10 s |
| R2a + R2b | D30 × 2 windows × r0 (60 cells) + flat copies of D10 (20 cells) | 1,440 + 360 | 11.5 min in total (12 invocations; first pass plus re-run, see D-R2) | 5,784 s | 1,046 MB | 10 s |
| R1 | D10 × 2 windows × r0 (20 cells) × {plain, adapter} | 640 | 1.5 min (plain, 6 workers) + 15.1 min (adapter, 3 workers) | 725 s + 3,399 s | 972 MB / **3,165 MB** (adapter, 3 workers) | 10 s |

There were no WALL_BUDGET errors and no failed simulation. The only failures were the R2 harness-assertion failures, D-R2 in
`DEVIATIONS.md`: the copied wf15 SHF invariant did not know the tolerance. I fixed the assertion, left the simulation untouched,
and re-ran only the failed tasks. A task that had passed reproduced field-for-field under the fixed code.

### 1.2 Gates (all PASS)

| Gate | Result |
|---|---|
| G-GEN (2019-02-11 r0: JSON, `.bin`, `.prov.json`) | 6/6 sha256-identical to wf14 |
| G-EXP (F16 export, per-day w08/w17 request counts vs `day_status.json`) | 32/32 equal |
| G-PAD (`pad.js` copy vs `pad_control.txt`) | byte-identical |
| G-ENG (`engine17.js` at defaults; 4 cells × 2 configs × 7 arms vs wf15 main) | 56/56 rows equal on every field except labels and timing |
| G-P2 (extra: P2 vs a5 V11/V12) | 24/24 equal |
| G-R3d (CLF with flatRatio = 1 vs N) | 8/8 rider-identical |
| R3: N rows on F14 vs wf14 main (U, EO35, EOHZ35, RCE35; G2 NCE35) | 756/756 equal |
| R3: CL RCE35 rows on F14 vs wf14 sys | 168/168 equal |
| R2a: τ = 0 rows vs wf15 main (SHF, SHHZ, SHRC, EO35, EOHZ35, RCE35) | 720/720 equal |
| R1: plain rows vs wf15 main (U, EO35, EOHZ35, RCE35, SH*) and wf14 main (G2 NCE35) | 300/300 equal |
| R1: INS NCE35 rider-identical to INS RCE35 (both identities) | 20/20 plain, 20/20 adapter |

---

## 2. R3: forecast vs an equal-time pad, plus a fresh-day replication (tests C6; R3-X tests C1–C4 on fresh days)

### 2.1 Open loop (wf7 B1 design): does the forecast beat an equal-length pad?

Primary contrast: INS RCE35, windows pooled. Each value is Δ in pp of Y, mean [95 % CI], with n−/n+ over days.

| Contrast | F14 (14 days, seen) | F16 (16 days, **fresh**) | Prediction |
|---|---|---|---|
| (a) display − naive | −5.93 [−6.53, −5.34], 14−/0+ | −6.38 [−7.04, −5.72], 16−/0+ | — |
| (b) pad(k_w) − naive | −4.77 [−5.34, −4.21], 14−/0+ | −4.64 [−5.10, −4.18], 16−/0+ | −3 to −6 ✓ |
| **(c) display − pad(k_w)** | **−1.16 [−1.89, −0.43], 12−/2+, p 0.013** | **−1.74 [−2.47, −1.00], 15−/1+, p 0.0005** | −1 to −3 ✓ |
| (c) w08 / w17 | −2.38 / +0.03 | −3.49 / −0.17 | w08 carries all of it, w17 ≈ 0 ✓ |
| (c) on E (early > 60 s) | −0.80 (13−/1+) | −0.94 (13−/3+) | display < pad ✓ |
| (c2) display − pad(k_cell) | −1.20 | −1.70 | — |

- k_w08 is 2.24 % (F14) and 2.37 % (F16) of the remaining promise time; k_w17 is 0.09–0.10 %. These match wf7's 2.32 % / 0.18 %.
- The pad alone takes 80 % (F14) and 73 % (F16) of the naive→display gain; wf7 found about 68 %.
- The same direction holds for every arm and config. (c) ranges from −0.87 to −1.96 pp, and 11–15 of 14/16 days are negative in every cell.
- **Label C6 open loop: HOLDS** for the primary contrast, and also for INS U, INS EOHZ35, G2 EOHZ35 and G2 RCE35.
  - G2 U is WEAKENED by the pre-stated rule: −0.87 and −0.88 pp, below 1.0 pp in both panels.
- Reading:
  - A forecast-specific gain exists. It is real, consistent across days, and **modest**: about 1.2–1.7 pp of 32 % late riders, roughly a
    fifth to a quarter of the display layer's gain. It is smaller than wf7's −2.88 pp.
  - The rest of the display gain is promise length, which a constant pad delivers just as well.
  - Not REVERSED and not STRENGTHENED.

### 2.2 Closed loop: is the wf14 system lateness gain "mostly promise length"?

Contrast (e): CL − padCL. padCL is a uniform pad that gives the N runs the same mean promise length as CL, per panel × window × config × arm.

| Contrast (INS RCE35) | F14 | F16 | Prediction |
|---|---|---|---|
| (d) CL − naive | −4.58 [−5.49, −3.68], 14−/0+ | −4.49 [−5.46, −3.51], 16−/0+ | large and negative in w08 ✓ |
| **(e) CL − padCL, pooled** | **−2.84 [−3.82, −1.87], 14−/0+** | **−1.98 [−2.81, −1.15], 15−/1+** | 0 ± 1.5 ✗ |
| (e) w08 | −8.53, 14−/0+ | −7.06, 16−/0+ | −2 to +1 ✗ |
| (e) w17 | +2.65, 2−/12+ | +2.67, 3−/13+ | +1 to +4 ✓ |
| (e) on E | −2.43, 13−/1+ | −2.97, 16−/0+ | — |
| (f) CLF − CL, w08 (CLF equal time) | +3.00, 1−/13+ | +2.88, 1−/15+ | — |
| (f) CLF − CL, w17 (CLF *not* equal time; descriptive) | +8.58 | +9.19 | — |

- Pooled levels of Y, F16 (F14 in brackets): naive 32.39 (32.30), padCL 29.88 (30.56), CL 27.95 (27.74).
  - Promise length explains about 56 % (38 %) of the CL gain over naive. The other 44 % (62 %) comes from the closed-loop forecast's shape
    and its dispatch.
  - CL also cuts early arrivals sharply: E is 1.7 % against 4.7 % for padCL.
- **In w17 the closed-loop forecast is harmful.** In both panels, CL's Y in w17 (29.9–30.7 %) is higher than naive's (25.7–26.4 %).
  The forecast expects the evening to ease, shortens promises (kCL_w17 = −0.6 to −1.1 %), and more riders end up late. wf7 B2 predicted this.
- **Label C6 closed loop: INCONCLUSIVE** (panels disagree).
  - F14 is WEAKENED: ≤ −2 pp with ≥ 75 % of days negative, so the closed-loop forecast beats an equal-length pad.
  - F16 is INCONCLUSIVE: −1.98 pp misses the −2 pp threshold by 0.02 pp, with 15/16 days negative.
  - Plain reading: the claim "the wf14 system gain is mostly promise length" is **not supported** in either panel. The direction is the
    same across all 4 config × arm cells and both panels (pooled −0.91 to −2.84 pp).
  - It comes entirely from w08, where CL beats an equal-length pad by 4–8.5 pp. In w17 CL is worse than the pad.
- **CLF**: closed loop with only the window's average drift r_w, no day-specific shape.
  - In w08 (equal time) it is 2.4–3.0 pp worse than CL in every config × arm and both panels. The prev4 forecast's day-specific shape
    carries real information.
  - In w17, CLF shortens promises more than CL (not equal time), so that contrast is descriptive only.
- **Side finding (the pre-specified row metrics of (d)):**
  - The closed-loop forecast **itself cuts visible reversals**: visRev60 CL − N is −6.1 to −9.9 per day, which is −23 % to −48 %, with
    12–13 of 14/16 days in favour.
  - It also cuts TV5 churn by about 18 s per served rider (16/16 days), at no service cost (|Δserved| ≤ 2/day).
  - Under INS this is about 4× what Rule C achieves (§2.3).

### 2.3 R3-X: fresh-day replication of C1–C4 (F16, never simulated before; N rows; RCE35 vs EOHZ35)

| Claim | Metric | F16 (fresh) | F14 (seen) | Prediction | Label (F16) |
|---|---|---|---|---|---|
| C1 | G2 visRev60 | −12.58/day [−14.77, −10.40], **−38.5 %**, 16−/0+ | −13.50/day, −40.9 %, 14−/0+ | −30 to −50 %, ≥ 13/16 ✓ | **HOLDS** |
| C1 | G2 visRev60 vs NCE35 / vs EO35 | −37.2 % (16/16) / −37.8 % (16/16) | −39.8 % / −40.6 % | same direction ✓ | — |
| C1 | G2 TV5 per served rider | −13.5 s (16/16) | −13.0 s (14/14) | — | — |
| C1 | G2 relocations / switched riders | −44 % / −44 % (16/16) | −49 % / −49 % | — | — |
| C2 | G2 served | −2.63/day [−5.43, +0.18], 11−/5+ | −2.86/day, 9−/5+ | −1 to −5 ✓ | **HOLDS** |
| C2 | G2 served vs NCE35 | −4.17/day, 12−/3+ | −5.74/day, 13−/1+ | about −5 ✓ | — |
| C3 | INS served | +2.25/day [+0.15, +4.35], 5−/10+ | −0.50/day | ±4 ✓ | **HOLDS** |
| C3 | INS visRev60 | −1.48/day, −8.4 %, 12−/3+ | −1.79/day, −9.5 % | 0 to −15 % ✓ | — |
| C4 | INS breachDeadline60 | **+4.54/day, 0−/16+** | +4.90/day, 0−/14+ | +2 to +7 ✓ | **HOLDS**: the fence is better on Elmi's yardstick |
| C4 | G2 breachDeadline60 | **+5.17/day, 1−/15+** | +5.76/day, 0−/14+ | +2 to +7 ✓ | **HOLDS** |
| — | Pchg = P(dispatch change ≥ 5 s), G2 / INS | −0.56 pp / +0.27 pp | −0.31 / +0.20 | — | — |

- Without the NYC-DOE non-school target days (09-30 in F16, 10-09 in F14), every label is unchanged.
- **"Does it matter" exchange rates (RUNPLAN 2.4), F16:**
  - **G2:**
    - 17.7 visible reversals avoided per 1,000 served riders;
    - 4.8 reversals avoided per rider not served;
    - 2.4 reversals avoided per extra Elmi-deadline breach;
    - Δlate60First +0.6/day, which is noise (8−/8+).
  - **G2 vs NCE35:** 16.7 per 1,000 served; 2.9 per rider not served; no deadline price (Δ = 0.00/day; the script prints a
    floating-point artefact here, see `DEVIATIONS.md`).
  - **INS:** 2.1 reversals avoided per 1,000 served; no service price; but **0.33 reversals avoided per extra deadline breach**. That is
    about 3 extra Elmi-deadline breaches for each reversal avoided.
- Breach rates: RCE35 1.9 % against EOHZ35 1.2–1.3 % of served riders.

---

## 3. R2: is the strict-fence result a rounding artefact? Tolerance 0 / 1 / 5 / 60 s (tests C5)

### 3.1 Real traffic (R2a; D30, 30 days; causal scores, frozen x*)

| | INS (x* = 72) | G2 (x* = 71) | Prediction (a3 discovery) |
|---|---|---|---|
| Gap SHRC − SHF at τ = 0 (served/day) | 34.2 [29.2, 39.2], 30/30 | 29.2 [24.9, 33.4], 30/30 | wf15: +34.4 / +28.9 ✓ |
| ρ(1 s) = Gap(1 s) / Gap(0) | 0.594 | 0.345 | 0.65 / 0.62 |
| **ρ(5 s)** | **0.416** (Gap 14.2/day, 24/30 days > 0) | **0.184** (Gap 5.4/day, 23/30) | 0.37 / 0.62 |
| ρ(60 s) | −0.456 (SHF serves 15.6/day more than SHRC) | −0.714 (+20.8/day) | ≤ 0 ✓ |
| ρ_HZ(5 s), SHHZ version | 0.495 | 0.409 | — |
| SHF(5 s) − SHHZ, served/day | **−19.5**, 29/30 | **−16.5**, 28/30 | fair comparator if within ±3 |
| EO35(τ) − EO35(0), served/day, τ = 1 / 5 / 60 s | +0.17 / +1.57 / +12.5 | +0.57 / +1.00 / +9.3 | ±1.5 for every τ (60 s ✗) |
| SHF forced vehicle-decisions/day at τ = 0, share ≤ 1 s / 1–5 s / 5–60 s / > 60 s | 23,395: 49 / 7 / 36 / 7 % | 65,477: 49 / 7 / 35 / 8 % | a3: 41–42 % ≤ 1 s |
| EO35 forced/day at τ = 0, share ≤ 1 s | 2,571: 1.3 % | 7,296: 1.6 % | "barely affected" ✓ |
| Guaranteed riders moved (G_dispAny1): SHF at 0 / 5 / 60 s; SHHZ; SHRC | 0.5 / 1.5 / 8.9 %; 0.7 %; 0 % | 4.4 / 7.3 / 13.6 %; 6.0 %; 0 % | — |
| Guaranteed riders late > 60 s vs p0: SHF at 0 / 60 s; SHHZ; SHRC | 3.5 / 5.4 %; 4.1 %; 4.1 % | 3.6 / 5.6 %; 4.1 %; 4.2 % | — |

### 3.2 Elmi's constant-time world (R2b; flat copies of D10, 10 days)

| served/day vs SHHZ | INS | G2 |
|---|---|---|
| SHF τ = 0 | **−51.3**, 10/10 | **−46.8**, 10/10 |
| SHF τ = 1 s | +2.6 | +2.0 |
| SHF τ = 5 s | +2.6 | +4.3 |
| SHRC | −6.2 (9/10) | −4.2 (6/10) |
| EO35 τ = 0, and RCE35, vs EOHZ35 | 0.0 and −0.1 | +0.1 and −2.7 |

- With constant travel times every SHF forcing is a rounding excess: 96 % are ≤ 1 s and the rest are ≤ 5 s.
- A 1-s tolerance removes the whole loss. In Elmi's world, the strict fence's service gap is **entirely** an artefact of the
  millisecond projection.
- Descriptive, not pre-specified:
  - In the flat world under G2, RCE35 vs EOHZ35 visRev60 is 1.8 against 21.2 per day (−92 %), at −2.7 served/day.
  - Both arms have 0 deadline breaches, because there is no exogenous drift.
  - Under INS the flat world has no reversals at all.

### 3.3 Labels

- **C5 "SHRC > SHF is mostly a strict-fence rounding artefact": INCONCLUSIVE** under the pre-stated rule.
  - HOLDS needs ρ(5 s) ≤ 0.40 for INS and ≤ 0.70 for G2. INS is 0.416, just above the threshold; G2 is 0.184, well inside.
  - WEAKENED and REVERSED do not apply.
  - Plain reading: a 5-s tolerance removes 58 % (INS) and 82 % (G2) of the wf15 SHRC − SHF gap. In the flat world, 1 s removes all of it.
    The audit's claim is essentially right, with a slightly smaller share for INS.
  - What remains is real *in our model*: SHF at 5 s is still 14.2 (INS) and 5.4 (G2) riders/day below SHRC.
- **"EO35 barely affected; the wf14 EO35 results stand": HOLDS** in both configs (+1.57 and +1.00/day at 5 s; no side has 2/3 of the
  days).
- **"SHHZ is a fair primary comparator": REAL IN OUR MODEL, NOT IN ELMI'S.**
  - SHF at 5 s is still 16–20 riders/day below SHHZ under real traffic. The remaining forcings are 5–60 s and > 60 s exceedances from
    traffic drift, meeting our forced-vehicle formalisation.
  - In the flat world SHF at 1–5 s equals SHHZ within +2.0 to +4.3/day.
  - So SHHZ is the right comparator for "Elmi's concept". The strict-fence numbers measure our implementation of a hard fence under
    traffic, not Elmi's design.

---

## 4. R1: identity mode, plain/round vs ridebound-fleetpy-adapter + fleetpy-seconds-half-even (tests C7; indirectly C1, C3, C5)

D10 (10 seen days) × {w08, w17} × r0.
- The adapter cells differ from wf14 in exactly 4 JSON keys: identity mode, snapshot rounding, and 2 absolute paths (`cells/adapter/_diff_report.json`).
- 8 arms per config; Shadow arms use causal scores at the frozen x*.
- Smoke test: 18.4 s per arm-run, so the full arm set was kept.

| Contrast (per day = w08 + w17) | Plain (P) | Adapter (A) | DD = Δ_A − Δ_P, mean (SE); n−/n+/ties |
|---|---|---|---|
| G2 visRev60, RCE35 − EOHZ35 | −16.2/day, **−41.0 %**, 10−/0+ | −17.2/day, **−43.4 %**, 10−/0+ | −1.0 (0.71); 5/2/3 |
| G2 served, RCE35 − EOHZ35 | −1.5 | +1.2 | **+2.7 (1.16)**; 2/6/2 |
| G2 visRev60, RCE35 − NCE35 | −15.8, −40.4 % | −14.7, −39.6 % | +1.1 (0.77) |
| G2 served, RCE35 − NCE35 | −5.2 | −1.6 | **+3.6 (1.13); 1/8/1, sign p 0.039** |
| G2 breachDeadline60, RCE35 − EOHZ35 | +5.3, 10/10 | +5.7, 10/10 | +0.4 |
| G2 TV5 per served, RCE35 − EOHZ35 | −15.9 s | −16.2 s | −0.2 |
| INS served, RCE35 − EOHZ35 | −1.4 | +2.1 | **+3.5 (1.28)**; 2/8/0, p 0.11 |
| INS visRev60, RCE35 − EOHZ35 | −2.1, −10.0 % | −2.1, −10.1 % | 0.0 |
| INS breachDeadline60, RCE35 − EOHZ35 | +4.1 | +4.0 | −0.1 |
| SHRC − SHHZ served (INS / G2) | −6.4 / −6.1 | −6.4 / −6.3 | 0.0 / −0.2 |
| SHRC − SHF served (INS / G2) | +36.4 / +30.6 | +36.0 / +31.5 | −0.4 / +0.9 |
| SHF − SHHZ served (INS / G2) | −42.8 / −36.7 | −42.4 / −37.8 | change +1 % / −3 % |
| SHF forced vehicle-decisions/day (INS / G2) | 22,252 / 62,614 | 22,185 / 62,503 | — |

- **Chaos per cell**, as predicted:
  - |served_A − served_P| averages 1.0–2.9 per cell, with a maximum of 14 (G2 SHRC71) and 12 (INS U).
  - **0–3 of 20 cells are rider-identical**, depending on the arm. a3 found −1..+4 per cell and 0/20.
  - Identity tie-breaks per run: about 3 under INS, 12–26 under G2.
- **Label C7: HOLDS.**
  - The G2 relative reversal reduction moves by +2.4 pp (prediction ±10 pp; flip threshold 15 pp).
  - No adapter day has ΔvisRev60 ≥ 0 (REVERSED needs ≥ 5/10).
  - |mean DD served| is 2.7 (G2) and 3.5 (INS). Both are below the 5/day flip threshold.
  - INS slightly exceeds the *predicted* ±3/day, with 8/10 days positive, p 0.11. In the adapter identity Rule C looks about 3.5 riders/day
    better relative to EOHZ35 than in plain. The sign of the INS service difference flips (−1.4 → +2.1), but both values lie inside the
    C3 band of ±4/day.
  - The INS reversal change stays at about 10 % in both identities (≤ 25 % ✓).
  - Not pre-specified as a flip criterion: the G2 service gap **RCE35 − NCE35** shrinks from −5.2 to −1.6/day (DD +3.6, 8/9 non-tied
    days, p 0.039). The service price of Rule C *against the net ledger* is therefore identity-sensitive at the level of a few riders per day.
- **C5 note:** the strict-fence service gap does **not** depend on the rounding mode (SHF − SHHZ changes by +1 % / −3 %).
  - With R2b (all flat-world forcings are ≤ 5 s), this is consistent with a3's attribution of the artefact to the vehicle-position
    quantization: the permille floor and the ceil of the remaining arc in `startOf`. It does not come from the snapshot rounding.

---

## 5. What these three checks say about "do the traded-for benefits hold up, and do they matter?"

This section covers the simulation only. Whether riders value these differences is for the literature keys b1–b4.

1. **Fewer reversals under reassignment (C1): the benefit holds up.**
   - It replicates on fresh days: F16 −38.5 %, 16/16.
   - It survives the identity / rounding mode: 41 % vs 43 %, 10/10 each.
   - It is about the same size against the net ledger and against the strict fence (−37 % to −38 %).
   - It comes with −13.5 s of TV5 churn per served rider and 44 % fewer relocations.
   - **Size in absolute terms:** about 18 fewer riders per 1,000 served see a ≥ 60-s reversal. The price per day is about 2.6 riders not
     served (≈ 4.8 reversals avoided per lost rider) and about 5 extra Elmi-deadline breaches (≈ 2.4 reversals avoided per extra
     breach). Lateness against the first promise does not change.
2. **Insertion only, RideBound's actual mode (C3, C4): the reversal benefit is small and is paid on Elmi's yardstick.**
   - Rule C gives −8 to −10 % reversals, which is about 2 per 1,000 served, at no service price.
   - It gives about 3 extra deadline breaches (A > p0 + B + 60 s) per reversal avoided: 1.9 % vs 1.2–1.3 % of served riders.
   - Under INS the honest case for Rule C is the auditable per-rider ledger, which holds by construction. It is not fewer reversals.
3. **The forecast (C6) is worth more than the audit allowed, with one important negative.**
   - Open loop, it adds a modest, consistent gain over an equal-length pad: 1.2–1.7 pp of late riders.
   - Closed loop, it beats an equal-length pad by 2–3 pp pooled and 7–8.5 pp in the morning peak. It also cuts early arrivals by about
     two thirds (E 1.4–1.7 % against 5.4–5.6 % naive, INS RCE35).
   - It reduces visible reversals by 23–48 % and TV5 by 18 s per rider, at no service cost.
   - **But in the evening window it makes lateness worse**: +4 pp against naive promises and +2.7 pp against an equal-length pad, in both
     panels. A thesis claim about the forecast has to be stated per window, or the w17 behaviour fixed (for example, never shorten a promise
     below the naive projection).
4. **Shadow vs Rule C comparisons (C5):** the wf15 "SHRC serves more than SHF" headline should not be used.
   - It is mostly the millisecond fence (58–82 % at 5 s), and in Elmi's world it is all of it.
   - SHHZ is the comparator that represents Elmi's concept. SHRC serves **fewer** riders than SHHZ in every set run here:
     - R1 (D10): −6.1 to −6.4/day;
     - R2a (D30): −5.3 (INS) and −11.1 (G2);
     - R2b (flat): −4.2 to −6.2.
5. **Identity mode (C7) does not change any conclusion.**
   - Per-cell outcomes are chaotic: 0–3/20 cells are rider-identical.
   - The DD on service for INS (+3.5/day) and for G2 RCE35 − NCE35 (+3.6/day) is large enough that differences of ±3–4 riders/day between
     arms should not be read as meaningful in either direction.

---

## 6. Limits (stated so that silence is not read as support)

- Every number is from the emulator on NYC Via 2019, P512, 62 vehicles, 2.0x, dwell 0. None of it is from the real RideBound C#. G2 does not
  exist in RideBound.
- F14, D10 and D30 are days that have been seen many times. **Only F16 is fresh**, and R3 is the only check here that uses it.
- p-values are descriptive. Several labels sit within a few hundredths of their thresholds:
  - ρ_INS(5 s) = 0.416 against 0.40;
  - (e) on F16 = −1.98 pp against −2.00.
  I report the labels exactly as the pre-stated rules give them.
- **Whether riders care about a ≥ 60-s reversal cannot be answered by simulation.** The exchange rates in §2.3 are the input that the
  literature keys b1–b4 need.
- **padCL** matches the *mean* promise length per panel × window. It does not match each rider's promise length, and it is applied to the
  N runs, whose trajectories and served sets differ slightly from CL's (|Δserved| ≤ 2/day).
- In w17, CLF is not equal-time, so the w17 and pooled CLF numbers are descriptive.

## 7. Files

All paths are under `SP/wf17/r1-identity-fence-pad/`:
- results: `results/r3.ndjson`, `dumps/r3.dump.ndjson`, `results/r2a.ndjson`, `results/r2b.ndjson`, `results/r1.ndjson`, plus `*.runs.ndjson`
  (job manifests and footers) and `*.errors.ndjson` (R2 first pass);
- tables: `results/r3_tables.txt|json`, `results/r2_tables.txt|json`, `results/r1_tables.txt|json`;
- analysis: `analysis/lib17.js`, `analysis/r{1,2,3}_analyze.js`, hashed in `ANALYSIS-HASH.txt`;
- code: `code/runner/engine17.js` (sha256 `81d7b47e…`, the first wf17 build, P1+P2+P3), `code/scripts/*`, `code/RUNBOOK.sh`;
- cells: F16 (96, fresh) in `cells/f16/`; R1 adapter copies in `cells/adapter/`; R2b flat copies in `cells/flat/`; G-GEN in `cells/ggen/`.
  Each copy directory has a `_diff_report.json`.
