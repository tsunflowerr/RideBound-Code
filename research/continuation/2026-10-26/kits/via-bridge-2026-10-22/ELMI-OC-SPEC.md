# Elmi-OC: specification and mechanism check (emulator only)

Date: 2026-10-22. Status: mechanism check passed. It is **not** a result and not yet in the Runner.

## Source

- Elmi, Moylan, Alonso-Mora and Fielbaum (2026). *Improving ridepooling reliability using shareability shadows*.
  TR-C 192, 105897.
- Local extract: `C:/RideBoundContinuation/2026-10-17/elmi-comparator-preflight-v1/elmi-paper-extract.txt`. The
  2026-10-17 preparation spec is `ELMI-REPLICATION-SPEC.md` in the same folder.
- Definitions (extract lines 369-383):
  - waiting t_w = t_PU − t_r;
  - detour t_d = t_v − T_r;
  - delay t_D = t_w + t_d;
  - per-request maxima Ω_r (waiting) and Φ_r (delay).
- Residual slack after initial assignment (Eq. 2, lines 400-408): ω_r = Ω_r − t_w^i and φ_r = Φ_r − t_D^i.
- Ordinary Control (lines 947-951) gives every passenger the same fraction γ of the slack, from 0% to 100%. The new
  maxima are Ω' = t_w^i + γ ω_r and Φ' = t_D^i + γ φ_r, fixed at assignment (preparation spec F16).

## Adaptation to RideBound (named "Elmi-OC", not the paper's algorithm)

1. **Which constraint carries the slack.** RideBound has no total-delay bound. Its drop-side constraint is the ride
   time: drop ≤ pickup + maxRide (normalizer rule; 2.0× direct in the bridge). Elmi's slack is "the remaining capacity
   of the maxima the system enforces". So the drop slack at the first promise (pickup pk0, drop p0) is
   **pk0 + maxRide − p0**.
   - The alternative, Elmi's delay with an implied Φ = 600 s + maxRide − T, would use the latest possible pickup
     instead of the promised one. It is not used. A sensitivity check can run it later.
2. **Bound:** bound_r = p0 + floor(γ · (pk0 + maxRide − p0) / 1000), with γ in permille.
3. **Relaxed form under traffic:** admit when p ≤ max(bound_r, e), with e the keep-route projection, as DL-HZ does.
   Elmi's travel times are static; in RideBound's measured-traffic world e can pass the bound without any dispatch
   change, and a strict bound would then need a recovery rule. The relaxed form never prunes the keep route.
4. **Waiting part:** inert in insertion-only RideBound, because exact pickup locks forbid dispatch changes to a
   promised pickup ETA. It must be added when B4/G2 re-optimisation moves pickups.
5. **Not included:**
   - shadow control (Eq. 3-11: score, percentile threshold);
   - the paper's assignment objective (A.1-A.2);
   - rebalancing;
   - one new request per vehicle per epoch (spec F02-F07).

   Every arm runs on the same RideBound cost-first dispatcher, so the comparison is between rules, not systems.

## Code

- Emulator copy `C:/RideBoundContinuation/2026-10-22/emulator-wf5-eoc/engine.js`, arm code 8 (`EOC<gamma>`):
  - `admits` case 8;
  - `armKey` and `parseArm`;
  - `armOf` validates γ as an integer permille in [0, 1000].
- The original copy `emulator-wf5` (sealed bridge predictions) is unchanged.

## Checks (eoc-check-2026-10-22.json; 11 bridge cells, 2.0×)

| Check | Result |
|---|---|
| Existing emulator tests | 43/43 pass |
| Calibration vs archived runs (EOC unused) | 4083/4083 riders identical |
| EOC0 vs DLHZ0 (same rule p ≤ max(p0, e), different code paths) | 11/11 cells rider-identical |
| Every published drop revision satisfies p ≤ max(bound, e) | 0 violations / 144,515 revisions |
| EOC1000 vs U | 1011/1011 riders identical (the bound never binds here) |
| Served riders, γ = 0 / 0.25 / 0.5 / 0.75 / 1 | 925 / 979 / 985 / 1005 / 1011 |
| Σ\|c\| (s), same γ values | 32 / 8,718 / 26,011 / 47,341 / 68,302 |

Reading: the mechanism behaves as specified. These are 11 cells of one design: no comparison with RC/PS/DL-HZ is
claimed. That comparison belongs to the Via frontier step, at matched service.
