# verify-fullsys PLAN (label: thăm dò sau kiểm toán / exploratory, post-audit)

Scope: independent check of `wf20/fullsys` (no simulation is run; analysis of existing rows only).
Inputs (read-only): `wf20/fullsys/results/rows.ndjson` (3600 rows), `wf20/fullsys/gates/gate_rows.ndjson` (120),
`wf15/results/confirm/main.ndjson`, `wf17/r1-identity-fence-pad/results/r3.ndjson`, `wf15/frozen/shadow-params.json`,
`wf20/fullsys/work/days_F14.csv`, `days_F16.csv`, producer tables `wf20/fullsys/results/tables.json` (only for the final comparison).

Own code: `verify-fullsys/scripts/indep.js` (written from scratch; does not import or copy fsan.js / gcheck.js / labels.js).

Checks
1. Gates, re-done independently: label on every row; x* (arm suffix, row.x, row.theta = frozen θ*[config][window]); completeness
   (cell set = days × {w08,w17} × {r0,r1,r2} per panel × config × mode × arm, no duplicates); g1 (F14 N rows vs wf15 main) and
   g2 (EOHZ35/RCE35 N+CL vs wf17 R3, flatRatio null, sigR3 = sig) compared on the UNION of fields, listing every field that differs
   (so the producer's exclusion list is made visible); g3 (SHRC breachLedger, G_ledgerBreach, A_ledgerBreach = 0); g5 (gate rows =
   production rows on every field except ms/msScore); forecast really active in Shadow CL rows (CL vs N rows differ, clip counters).
2. Tables recomputed from raw rows: unit = day, X_d = Σ_w mean_rs X; per 1000 = 1000·X_d/served_d; TV5 = sumTV5/1000/served.
   Paired by day: mean, SE, 95 % t-CI (own t quantile by numeric integration), exact sign test (BigInt binomial), rel = ΣA/ΣB − 1.
   Contrasts: MAIN SHRC_CL − SHHZ_CL (INS, G2; day/w08/w17; F14/F16/pooled), SHRC_N − SHHZ_N, DiD vs N, DiD vs (RCE35−EOHZ35)_CL,
   SH*_CL − SH*_N, EOHZ35/RCE35 CL − N, SYS levels a–e and b−a, b−c, b−d, d−a, c−a, SHRC_CL − SHF_CL.
3. Prediction labels P1–P26 re-applied from PLAN §5 text by my own reading.

Prediction (verification): every recomputed mean/SE/CI/median/counts/p/rel/levels equals the producer's tables.json within 1e-9
(absolute, or relative for p); every gate passes; my labels equal the producer's.
Flip: any recomputed value differs by more than 1e-9, any gate fails, or any label differs → reported as a finding in REPORT-VI.md.
