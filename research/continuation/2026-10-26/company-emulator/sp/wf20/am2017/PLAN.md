# wf20/am2017 PLAN: anchor the 512-requests / 62-vehicles regime on Alonso-Mora 2017 and Fielbaum & Alonso-Mora 2020

**Label: thăm dò sau kiểm toán (exploratory, post-audit).** All 30 days (D30) were already seen in wf14, wf15, wf16 and wf17. Nothing here
is confirmatory. No Holm correction; p-values are descriptive.

`SP` = `E:/RideBoundData/company-intake/2026-10-23/sp`,
`ME` = `SP/wf20/am2017`. Everything new is written under `ME`. All other folders are read only.

Owner's concern: a committee may ask why the thesis uses 512 requests per 2 h and 62 vehicles. Idea: anchor the regime on Alonso-Mora et
al. 2017 (AM17) and run a small proportional version. This plan (i) records where 512/62 came from and how they compare with AM17 and FA20
(Step 1, facts, no simulation), (ii) fixes, before any run, a rule-based set of fleet sizes for the same 512 requests (Step 2), and
(iii) states the predictions and flip rules for the wf17 conclusions C1-C4 at each fleet point (Step 3 is the run itself).

---

## 1. Step 1 facts (no runs). Script `ME/scripts/facts.js` + `ME/scripts/profile.sql` -> `ME/facts/facts.json`

### 1.1 The literature regimes

| Item | AM17 (PNAS 114:462) | FA20 (TR-C 121:102831) |
|---|---|---|
| Area / data | Manhattan, taxi trips with origin and destination in Manhattan, 5-11 May 2013; request time = pickup time; network 4,092 nodes / 9,453 edges, daily-mean static travel times (`SP/wf12/r2-alonso-mora/dl_pnas/am2017.txt:69`) | Manhattan, 15/01/2013 13:00-15:00, "a subset of the publicly available dataset", 4,091 nodes / 9,453 edges (`SP/dl_fa2020/fa2020.txt:356`) |
| Requests | 382,779 (Sun) to 460,700 (Fri) per day; running pool up to 2,000 (am2017.txt:70); "about 3 million requests per week" (`.../dl_pnas/si.txt:604`) -> 428,571/day average, 35,714 per 2 h over 24 h | 19,610 requests in the 2 h (groups > 4 removed) (fa2020.txt:358) |
| Fleet x capacity | m in {1,000, 2,000, 3,000} x capacity {1, 2, 4, 10} (am2017.txt:71); SI nominal case 2,000 x 4, Ω = 5 min (si.txt:609-610, 620) | 2,000 x 4 "as in Alonso-Mora et al. (2017)" (fa2020.txt:358) |
| Wait / delay limits | Ω in {120, 300, 420} s; Δ = 2Ω and Δ includes waiting (am2017.txt:71); constraint text am2017.txt:37 | max wait 5 min, max delay 10 min, "taken from Alonso-Mora et al. (2017)" (`SP/wf12/r2-alonso-mora/fa2020_layout.txt:1086-1087`) |
| Service | 2,000 x 4: 91.1 % (Ω 300), 93.7 % (Ω 420); 3,000 x 4: 97.0 % / 97.9 %; 1,000 x 4: 58.1 % / 60.3 % (si.txt:718-719, 731-732, 744-745); headline 3,000 x 2/4 -> 94/98 % (am2017.txt:30) | rejection 34.5 % (served 65.5 %) (fa2020_layout.txt:574); text says 66.5 % (fa2020.txt:359) |
| Algorithm | batch ILP over RV/RTV graphs, 30-s batches, reassignment before pickup, rebalancing of idle vehicles (+~20 % service, am2017.txt:71) | AM17 model, 1-min batches, rejection weight 40 (fa2020_layout.txt:1093) |

### 1.2 Where the thesis numbers came from (company data)

- **512 requests / 2 h.** Round 5 chose two densities: P128 = 128 requests / 2 h (3 % of Via; RideBound's normalizer cap
  `requestTarget <= 128`) and P512 = 4 x P128 = 512 / 2 h (12 % of Via) (`SP/wf5/ds-nyc-via/T.txt:14-15, 72-75`). P512 was kept because only
  the dense regime makes the memory and slack-ownership effects visible (wf5 T.txt:72-80). Each wf6/wf14 cell is the first 512 of one seeded
  shuffle of the window's eligible Via requests (`SP/wf6/via-many-days/T.txt:140-143`). **512 is therefore 4 x RideBound's request cap, not
  a literature value.**
- **Sampling fraction (measured here, facts.json).** Eligible Via (HV0004) Manhattan-internal requests per cell window, D30:
  median 4,522 (w08) / 4,702 (w17), range 2,610-6,249, so 512 = **11.1 %** of the window (8.2-19.6 %). D60: median 4,539 / 4,646
  (fraction 11.1 %, 7.9-20.2 %). `day_window_counts.csv` (all HV0004 Manhattan-internal, before the node filter) gives D60 medians
  4,542 / 4,649.
- **62 vehicles (P512) / 42 (P256).** Pre-registered service rule: smallest grid fleet whose mean U served share (INS, max ride 2.5x,
  dwell 0) on 4 calibration cells (2019-11-05/19 x w08/w17, not in the panel) reaches **79.2 %** = the served level of RideBound's own
  Manhattan panel A (wf5 T.txt:155-157; wf6 T.txt:146-149). P512: 58 -> 78.3 %, 62 -> 82.3 % (`SP/wf6/via-many-days/work/fleet_cal_P512.txt:10-11`);
  P256: 40 -> 78.1 %, 42 -> 81.1 %. **62 is a service-level calibration, not a literature value.** On D30 at the thesis ride limit 2.0x,
  U serves 78.5 % (INS) / 79.2 % (G2) (wf14 main r0).
- **Service limits in the cells** (all 120 D60 r0 cells, facts.json): pickup window 600 s (max wait 10 min after the request), decision timer
  600 s, capacity 4, 96 vehicles written (cut to nVeh at run time, prefix), file maxRidePermille 1500 overridden to **2000 (ride <= 2.0 x
  direct)** at run time, dwell 0. Compared with AM17/FA20 (wait <= 5 min, total delay incl. wait <= 10 min) the thesis limits are LOOSER:
  wait 10 min, and ride time up to 2 x direct (median model direct 942 s on D30, so up to ~16 extra minutes in the vehicle).

### 1.3 Load ratios (requests per vehicle per 2 h)

| Regime | Requests / 2 h | Fleet | Req / veh / 2 h |
|---|---|---|---|
| AM17, 24-h average, m = 1,000 / 2,000 / 3,000 | 35,714 | 1,000 / 2,000 / 3,000 | 35.7 / **17.9** / **11.9** |
| AM17, Sunday ... Friday, m = 2,000 | 31,898 ... 38,392 | 2,000 | 15.9 ... 19.2 |
| AM17 in OUR windows (08-10, 17-19), PROXY | 51,104 | 2,000 / 3,000 | **25.6** / 17.0 |
| FA20 (13-15 h) | 19,610 | 2,000 | **9.8** |
| Thesis P512 | 512 | 62 | **8.26** |
| Thesis P256 | 256 | 42 | 6.10 |
| RideBound native Manhattan panel A (round 5) | 108 | 8 | 13.5 (wf5 T.txt:540) |

- **AM17 peak ratio is NOT derivable from AM17 itself** (no hourly counts; only the 2,000-request pool, am2017.txt:70). The proxy uses the
  2019 weekday hourly profile of Manhattan-internal Yellow taxi trips (RatecodeID 1, PU zone <> DO zone; 230 weekdays, holidays removed;
  `ME/facts/hourly_weekday_2019.csv`): 08-10 = 10.83 % of the day, 17-19 = 13.02 %, 13-15 = 10.58 %; mean of our two windows 11.93 %, i.e.
  1.43 x the 24-h average 2-h volume. Applied to AM17's 428,571/day. Caveats: 2019 shape for 2013 data; week-average day.
- The same proxy puts AM17's 13-15 h at ~45,400 requests, so FA20's 19,610 is ~43 % of it, consistent with FA20's word "subset"
  (fa2020.txt:356). The reason is not stated (UNVERIFIED).
- **Trip length matters.** Via trips are longer than taxi trips. 2019 weekday, our windows pooled by volume (facts.json):
  mean actual duration Via 1,265 s vs Yellow 817 s -> **kappa = 1.550**; medians 1.61; median miles 1.45. Via trip_time includes pooling
  detours of matched rides, so 1.45-1.55 is the honest range. In taxi-trip equivalents P512 = 8.26 x 1.55 = **12.8 req / veh / 2 h**,
  between AM17's 3,000-fleet (11.9) and 2,000-fleet (17.9) averages; P256 = 9.4.
- Vehicle-time load (requests x mean trip time / fleet time): P512 on model direct times 1.24 (D30); on actual durations 1.45; AM17
  2,000 x 4 average ~2.0 and in our windows ~2.9 (on 2019 Yellow mean 817 s); AM17 3,000 average ~1.35; FA20 ~1.15. Proxy numbers.

### 1.4 Density caveat (pooling depends on density, not only on the ratio)

All three studies use the same island, so density ratios do not depend on the area figure. Area: 58.0 km^2 (round 5 taxi-zone area,
implied by 4.41 req/km^2/h for 256 req/h, wf5 T.txt:540); FleetPy node coverage 52.8 km^2 (250-m grid) to 65.7 km^2 (convex hull)
(facts.json).

| Regime | Vehicles / km^2 | Requests / km^2 / h |
|---|---|---|
| AM17 2,000 x 4 (24-h avg / our windows proxy) | 34.5 | 308 / 441 |
| AM17 3,000 x 4 | 51.7 | 308 / 441 |
| FA20 2,000 x 4 | 34.5 | 169 |
| Thesis P512, 62 veh | 1.07 | 4.41 |
| Thesis P512 at 29 veh (AM17-proportional) | 0.50 | 4.41 |
| Thesis P256, 42 veh | 0.72 | 2.21 |

**Proportional scaling keeps requests per vehicle but cuts request density 70 x (AM17 average) or 38 x (FA20) and vehicle density 32 x.**
Shareability (how many trips can be pooled) rises with request density (Santi et al. 2014 PNAS; Tachet et al. 2017 Sci Rep; SECONDHAND,
not re-read here). AM17's own robustness test changes demand x0.5 / x2 at a fixed 2,000 fleet (si.txt:603-610), which changes density and
load together; it is not a scaling test. A small proportional version is therefore **not** a miniature AM17: fewer vehicles at the same
512 requests mainly means less supply per request, at a density far below AM17's. wf17 R5 showed density matters: P256 (half the requests,
42 vehicles) cut the G2 effect per 1,000 served to about 1/3 (−8.3 vs −23.2), while 56/68 vehicles at P512 changed nothing measurable
(`SP/wf17/r2-penalty-regime/NOTE.md`, section 3.1).

---

## 2. Step 2: sealed fleet points for 512 requests / 2 h (rule fixed before any run)

All other parameters are exactly the wf14 cells / wf17 R5 B0 (P512 cells `SP/wf14/scenarios/weekdays-P512/*-r0.json`, max ride 2000 permille,
dwell 0, capacity 4, pickup window 600 s, decision timer 600 s; fleet = first nVeh of the 96 written vehicles, the R5 V-F56/V-F68 override).

| Point | nVeh | Rule | Value |
|---|---|---|---|
| **F_AM** (primary AM anchor) | **29** | round(512 / AM17 24-h average ratio at the SI nominal fleet 2,000 x 4) = round(512 / 17.857) | 28.67 -> 29 (Sun/Fri bounds 32 / 27) |
| **F_AM3k** | **43** | round(512 / AM17 average ratio at the headline fleet 3,000 x 4) = round(512 / 11.905) | 43.0 -> 43 |
| **F_AMpk** (proxy) | **20** | round(512 / AM17 ratio in our windows, 2,000 x 4, 2019 Yellow profile proxy) = round(512 / 25.552) | 20.04 -> 20 |
| **F_FA** (primary FA anchor) | **52** | round(512 / FA20 ratio) = round(512 / 9.805) | 52.2 -> 52 |
| F62 (reference) | 62 | thesis P512 = wf14 / R5 B0 (reproduction, also the gate) | 62 |
| F_AM3k_t (secondary) | 67 | round(512 x kappa / 11.905), kappa = 1.550 (Via/Yellow mean duration, section 1.3) | 66.7 -> 67 |
| F_FA_t (secondary) | 81 | round(512 x kappa / 9.805) | 80.9 -> 81 |

- Not run, by rule: AM17 m = 1,000 (round(512 / 35.7) = 14), AM17's own under-supplied corner (58 % served even in AM17); the
  kappa-adjusted AM17 2,000 point (44) is within one vehicle of F_AM3k (43) and is represented by it.
- kappa sensitivity (descriptive, not run): kappa 1.4 / 1.5 / 1.6 gives AM3k_t 60 / 65 / 69 and FA_t 73 / 78 / 84.

---

## 3. Design (Step 3)

- **Panel:** D30 x {w08, w17} x r0 = 60 cells (D30 = every second day of wf14 D60, list in `SP/wf17/a0-plan/RUNPLAN.md` 1.3;
  `ME/work/panels.json` is a byte copy of `SP/wf17/r2-penalty-regime/work/panels.json`, sha256 9c911f55...).
- **Points:** F20, F29, F43, F52, F62, F67, F81 (7).
- **Configs and arms:** INS (insertion only, RideBound's real mode) and G2 (`{ reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' }`,
  move one waiting rider to one of 8 nearest vehicles), each with arms U, EO35, EOHZ35, RCE35.
- **Runs:** 7 x 60 x 8 = 3,360 arm-runs (840 tasks). One job. **4 worker threads** (wf20 rule). wall budget 900 s per arm-run.
- **Code** (`ME/code/`): runner files and `metrics_sh.js`, `gate17.js` are byte copies of `SP/wf17/r2-penalty-regime/code/` (sha256 in
  PLAN-HASH.txt). `scripts/drive20.js` = `drive17.js` + one new check `AM` (fleet points above, 4-worker cap, label) and nothing else.
- **Output:** `ME/results/am.ndjson` (+ `.runs.ndjson`, `.errors.ndjson`). Never overwritten; the driver is resumable on
  check|variant|cell|config|arm.
- **Analysis:** `ME/analysis/am_analyze.js` (+ `agg17.js`, `lib17.js` byte copies), hashed in PLAN-HASH.txt before production.

## 4. Metrics, contrasts and flip rules

### 4.1 Metrics (per day = w08 + w17)
served; **U service rate** = served / requests (%), per arm and config; visRev60 (riders with two consecutive opposite-sign visible drop-ETA
changes >= 60 s) per day and **per 1,000 served**; late60First (count and % of served); breachDeadline60 (count and %); **Pchg** (pDisp, % of
served); TV5, pDispAny1, relocations (descriptive).

### 4.2 Labels per point (= wf17 R5 rules; primary contrast RCE35 − EOHZ35)
- **C1** (G2 visRev60): REVERSED if ΔvisRev60 >= 0 on >= half of the days; WEAKENED if relative reduction < 20 % or < 75 % of non-tied days
  in favour; else HOLDS.
- **C2** (G2 served): FAILS if mean Δserved < −8/day with >= 2/3 of days negative (threshold literal, % of EOHZ35 printed); else HOLDS.
- **C3** (INS served): WEAKENED if |mean Δserved| > 8/day with >= 2/3 of days on the side of the mean; else HOLDS.
- **C4** (INS breachDeadline60; unfavourable to Rule C when it holds): REVERSED if Δ <= 0 on >= half of the days; else HOLDS.
- **C8'** ("the C1 direction does not depend on the fleet anchor"): REVERSED if any point is REVERSED; else WEAKENED if any point has
  rel < 20 % or favour < 75 %; else HOLDS if every point has rel in [20 %, 60 %]; else INCONCLUSIVE.
- Robust = HOLDS at >= 6 of 7 points and REVERSED at none.
- **Thesis statement rule (owner's question).** "The conclusions do not depend on the 512/62 choice within the literature anchors" may be
  written only if C1, C2, C3 and C4 have the same label at F29, F43 and F52 as at F62. Otherwise the thesis must name the point(s) where a
  label changes and say the conclusion depends on supply.

### 4.3 Descriptive (no label)
- RCE35 − EO35 and EOHZ35 − EO35, INS and G2; exchange rates; DD vs F62 (DD_d = Δ_d(point) − Δ_d(F62)).
- U service rate per point vs AM17/FA20 reported service (section 1.1).
- **H-req vs H-veh at F29 and F20.** F29 has fewer vehicles (29) than P256 (42) but P512's requests. If the G2 per-1,000 cut (RCE35 − EOHZ35)
  has magnitude >= 15 -> "consistent with H-req" (request density drives the per-rider effect); <= 10 -> "consistent with H-veh" (vehicle
  count drives it, like P256's −8.3); in between -> inconclusive.

## 5. Numeric predictions (written before any run; F62 values are known wf14/R5 numbers)

| Point | INS U service | G2 U service | G2 EOHZ35 served/day | C1 rel | G2 Δvis per 1,000 | G2 Δvis/day | G2 Δserved/day | INS Δserved/day | INS Δbreach/day | Labels |
|---|---|---|---|---|---|---|---|---|---|---|
| F20 | 28-40 % | 30-42 % | 270-400 | 25-50 % | −10 to −30 | −3 to −11 | 0 to −5 | within ±4 | +0.5 to +4 | C1 HOLDS (low confidence), C2/C3 HOLDS, C4 HOLDS |
| F29 | 40-52 % | 42-54 % | 380-520 | 30-50 % | −12 to −30 | −5 to −15 | 0 to −5 | within ±4 | +1 to +5 | all as F62 |
| F43 | 56-66 % | 58-68 % | 530-640 | 33-48 % | −16 to −28 | −9 to −17 | 0 to −6 | within ±4 | +2 to +6 | all as F62 |
| F52 | 66-73 % | 68-75 % | 620-700 | 35-45 % | −18 to −26 | −11 to −18 | 0 to −6 | within ±4 | +3 to +6 | all as F62 |
| F62 (known) | 78.5 % | 79.2 % | 742.0 | 41.4 % | −23.2 | −17.43 | −4.77 | +1.80 | +5.10 | C1-C4 HOLDS |
| F67 | 80-84 % | 81-85 % | 760-800 | 38-46 % | −20 to −26 | −15 to −21 | 0 to −7 | within ±4 | +4 to +6.5 | all as F62 |
| F81 | 86-92 % | 87-93 % | 820-880 | 36-48 % | −18 to −28 | −15 to −24 | 0 to −7 | within ±4 | +3.5 to +7 | all as F62 |

- C8': HOLDS (predicted); the weakest point is F20.
- H reading: "consistent with H-req" at F29 (my probability ~0.6).
- Prior basis: R5 (56/68 vehicles did not change the effect; P256 cut it to ~1/3 per 1,000), served per vehicle 13.1-13.7/day at 56-68
  (rising as the fleet shrinks), and C4 held in all 7 R5 variants.
- Service vs literature (prediction): at F29 our U service (40-54 %) is far below AM17's 91.1 % at 2,000 x 4 (Ω 300), i.e. a proportional
  fleet does not reproduce AM17's service level (density 70 x lower, kappa 1.55, insertion-only without rebalancing, at most 2 new requests
  per vehicle per batch). At F52 it is near FA20's 65.5 % served.

## 6. Gates

- **G0 (before gates):** code copies sha256-identical to `SP/wf17/r2-penalty-regime/code/` (recorded in PLAN-HASH.txt).
- **G1 (pre-run):** F62 on 4 D30 cells (2019-02-11 and 2019-10-03, w08 + w17) x 8 arm-configs vs wf14 main r0 (`SP/wf14/results/main.ndjson`,
  part M, mode N): 32/32 equal on every non-timing field (gate17.js).
- **G2 (pre-run, fleet-override path):** GF56 and GF68 on 2019-02-05 w08 + w17, G2 {U, EOHZ35, RCE35} and INS {EOHZ35, RCE35}, vs wf17 R5
  V-F56 / V-F68 rows (`SP/wf17/r2-penalty-regime/results/r5.ndjson`): 10/10 each, incl. the trajectory signature `sig`.
- **G3 (post-run):** all 480 F62 production rows vs wf14 main r0: 480/480.
- Any gate failure stops the job before production (G1, G2) or blocks the analysis (G3) until explained in DEVIATIONS.md.

## 7. Deviations, honesty

- Every deviation goes to `ME/DEVIATIONS.md` with a timestamp, written before the affected results are read where possible.
- Outputs are never overwritten. Bad and null results are reported with the same prominence as good ones.
- What this cannot show: anything about real RideBound (.NET); unseen days; AM17's own algorithm (ILP + rebalancing); AM17 density.
