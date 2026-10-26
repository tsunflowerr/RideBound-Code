# Gate log, wf17 r2-penalty-regime (wf17-exploratory-post-audit)

| Time (+07:00) | Gate | What was compared | Result | File |
|---|---|---|---|---|
| 15:36 | Plan hash | sha256 of RUNPLAN.md vs RUNPLAN-HASH.txt | PASS (`ef953c3b…`) | — |
| 15:40 | Panels | D60 (wf14 days.json, 60 days) and D30 (even indices) vs the D30 list printed in RUNPLAN 1.3 | PASS | `work/panels.json` |
| 15:44 | R5 generator gate | `gen17.js` default tags, `--fmax 512:96,256:56 --resample 0`, D30: regenerated P512 scenario JSON, `.bin`, `.prov.json` vs wf14 | PASS 180/180 sha256-identical | `work/gates/ggen_d30.txt` |
| 15:44 | P256 nesting | 60 P256 cells: 256 requests each, all in the same cell's P512 set; 56 written vehicles | PASS 60/60 | same |
| 15:47 | G-ENG-sub (R4) | drive17 R4 T8P0, 4 cells (2019-02-11, 2019-10-03 × w08/w17 × r0) × 5 arms vs wf14 main G2 r0, every reference field except labels/timing | PASS 20/20 | `work/gates/g_r4.ndjson`, `GATES.ndjson` |
| 15:47 | G-P2 | drive17 R4 T8P60 / T8P300 (T 8), EO35/EOHZ35/RCE35, 2019-02-05 and 2019-02-11 × w08/w17 vs a5 V11 / V12 | PASS 12/12, 12/12 | same |
| 15:47 | G-ENG-sub (R5) | drive17 R5 B0, same 4 cells, INS {EOHZ35, RCE35} + G2 {U, EOHZ35, RCE35, NCE35} vs wf14 main r0 | PASS 24/24 | `work/gates/g_r5.ndjson` |
| 15:47 | R5 V-R15/V-R25 (pre-run, 2 cells) | EOHZ35/RCE35, INS + G2, 2019-02-05 × w08/w17 vs a5 V1 / V2 | PASS 8/8, 8/8 | same |
| 16:17 | R4 post-run | T8P0 rows (5 arms × 120 cells) vs wf14 main G2 r0 | PASS 600/600 | `results/r4.ndjson` |
| 16:17 | R4 post-run (extra) | T8P60 / T8P300 EO35/EOHZ35/RCE35 rows on the D30 cells vs a5 V11 / V12 | PASS 180/180, 180/180 | same |
| 16:27 | R5 post-run | B0 rows (G2 4 arms + INS 2 arms × 60 cells) vs wf14 main r0 | PASS 360/360 | `results/r5.ndjson` |
| 16:27 | R5 post-run | V-R15 / V-R25 EOHZ35/RCE35 rows (INS + G2, 60 cells) vs a5 V1 / V2 | PASS 240/240, 240/240 | same |
| 16:27 | R5 post-run (extra) | B0 G2 rows vs R4 T8P0 rows on the same D30 cells | PASS 240/240 | same |
