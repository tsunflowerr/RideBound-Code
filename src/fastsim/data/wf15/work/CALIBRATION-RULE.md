# wf15 calibration rule (written 2026-10-22 17:24 +07:00, BEFORE any S3/S4 run and before any reliability metric of a Shadow arm at a calibrated threshold was seen)

Scope: discovery cells only (55 days x {w08, w17} = 110 cells; `--set discovery`, days 2019-10-22 and 2019-12-06 excluded).
No run on the confirmatory cells (`wf14/scenarios/weekdays-P512`) is part of this step.

## 1. Score-validity gate G-V (SPEC 5.3, 8.3)

- Reference runs: arm U with score logging (`--log`), INS and G2, every promised rider dumped. EO100 (Ordinary Control with
  100 % slack) is run with logging in the same invocation as a second Fig. 9 analogue; it is NOT used for the reference
  distribution (SPEC 5.2 says U).
- Gate, per dispatcher (pooled windows): Spearman rho(S_r, nDisp5) > 0 with the day-cluster bootstrap 95 % CI excluding 0,
  AND P(dispUp5) of the top score quintile minus the bottom quintile > 0 with its CI excluding 0.
- If either config fails: stop and report; no tuning of the score.

## 2. Thresholds

- theta_{c,w}(x) = type-1 empirical quantile s_(ceil(x M / 100)) of the U-run scores of config c and window w (SPEC 5.2).
- Coarse grid x in {0, 10, 20, 30, 40, 50, 60, 70, 80, 85, 90, 100} % (85 added to the SPEC grid because the task lists it).
- Fine grid: 1-% steps inside the coarse interval(s) adjacent to the coarse argmin where the served gap changes sign
  (`calib_theta.js search`, unchanged code).

## 3. Choice of x* (Elmi's horizontal alignment, made prospective)

- Matched quantity: mean served riders per discovery cell (110 cells) of **SHHZ_x** versus **EO35** of the same dispatcher
  (EO35 recomputed by the wf15 engine and checked cell by cell against wf13 partA).
- x*_c = argmin_x |served(SHHZ_x) - served(EO35)|, ties to the smaller x; one x* per dispatcher, pooled over windows.
- Why SHHZ and not SHF or SHRC: SPEC 5.3 (in Elmi's constant-time world the strict fence and the HZ floor coincide; in our
  traffic world the strict fence at B = 0 forces vehicles for a traffic-only reason, and Rule C is the factor under test).
  SHF and SHRC use the same theta* without recalibration, so the admission factor is compared at a fixed allocation.
- The rule uses ONLY served. Reliability metrics (P(dispatch change), reversals, breaches, guaranteed-rider changes) play
  no role in the choice of x*.
- SHF and SHRC are run afterwards on the task grid x in {50, 60, 70, 80, 85, 90} and at x*, for description only (served
  curve of all three fences). The x* that a served match on SHF or SHRC would have given is reported as a sensitivity and
  never replaces x*.

## 4. Freeze

- `calib_theta.js search ... --freeze` writes `frozen/shadow-params.json` (+ `frozen/FREEZE-HASH.txt`); it refuses to
  overwrite.
- `work/thresholds.json` (the file named by the task) is written from the frozen file plus the full theta table of the
  grid; the confirmatory driver reads theta* from `frozen/shadow-params.json`, and the two must agree (checked).
