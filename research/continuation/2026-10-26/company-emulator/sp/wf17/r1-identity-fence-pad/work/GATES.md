# Gate log, wf17 r1-identity-fence-pad (wf17-exploratory-post-audit)

| Time (+07:00) | Gate | What was compared | Result | File |
|---|---|---|---|---|
| 14:31 | Plan hash | sha256 of RUNPLAN.md vs RUNPLAN-HASH.txt | PASS (`ef953c3b…`) | — |
| 14:33 | Panels | D30, D10, F14, F16 recomputed (FNV-1a + mulberry32 seed `wf17-a0\|D10\|2026-10-23`; prev4 eligibility; 91 unused AVAILABLE days) vs the lists printed in RUNPLAN 1.3 | PASS (4/4 identical) | `work/panels.json` |
| 14:37 | G-GEN | `gen17.js` with default tags regenerates 2019-02-11 w08/w17 r0: scenario JSON, matrix `.bin`, `.prov.json` | PASS (6/6 sha256-identical to wf14) | `cells/ggen/` |
| 14:38 | G-EXP | F16 export per-day w08/w17 request counts vs `day_status.json` | PASS (32/32 equal; 134,641 rows) | `work/f16/` |
| 14:38 | G-PAD | `pad.js` copy on `rerun_all_WY.json` vs `pad_control.txt` | PASS (byte-identical output) | `work/gates/gpad_out.txt` |
| 14:39 | G-ENG | `engine17.js`, all knobs at default, via the `run_sh` copy: 4 cells (2019-02-11, 2019-10-03 × w08/w17 × r0) × {INS, G2} × {U, EO35, EOHZ35, RCE35, SHF, SHHZ, SHRC at x*} vs `wf15/results/confirm/main.ndjson`, every field except labels/timing | PASS (56/56 equal) | `work/gates/geng.ndjson` |
| 14:40 | G-P2 (R4's gate; extra) | engine17 P2, π 60/300 s, T 8, EO35/EOHZ35/RCE35, 2019-02-05 and 2019-02-11 × w08/w17 vs a5 V11/V12 | PASS (24/24 equal) | `work/gates/gp2.ndjson` |
| 14:43 | G-R3d | CLF with flatRatio = 1 vs N, 2019-09-19 w08/w17 r0, INS/G2, EOHZ35/RCE35 (fingerprint without the shown pickup ETA) | PASS (8/8 rider-identical) | `work/gates/gr3d.ndjson` |
| 14:43 | (pre-check) | N rows of those 2 cells vs wf14 main | 18/18 equal | same |
| 15:00 | R3 post-run | F14 N rows vs wf14 main (U, EO35, EOHZ35, RCE35; G2 NCE35) | PASS 756/756 | `results/r3.ndjson` |
| 15:00 | R3 post-run | F14 CL RCE35 rows vs wf14 sys | PASS 168/168 | same |
| 15:15 | R2a post-run | τ = 0 rows vs wf15 main: strict arms (SHF, EO35) and other arms (SHHZ, SHRC, EOHZ35, RCE35) | PASS 240/240 and 480/480 | `results/r2a.ndjson` |
| 15:18 | R1 post-run | plain rows vs wf15 main (U, EO35, EOHZ35, RCE35, SH* at x*) and wf14 main (G2 NCE35) | PASS 300/300 | `results/r1.ndjson` |
| 15:34 | R1 post-run | INS NCE35 vs INS RCE35, rider fingerprint | PASS 20/20 plain, 20/20 adapter | same |
