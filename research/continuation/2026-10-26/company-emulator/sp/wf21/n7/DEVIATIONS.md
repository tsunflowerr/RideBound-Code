# wf21/n7 DEVIATIONS (label wf21-n7-exploratory-post-audit)

Timestamped (UTC), in order. Plan: PLAN.md (sha256 in PLAN-HASH.txt, written 2026-10-26T07:45:59Z). Each entry says whether it was
written before or after the results it affects were read.

- D1 (2026-10-26T07:48:00Z), operational addition, written while the N/CL production job runs and before any production row was opened:
  `calloop.sh` (sha256 fe2ce22927d09558ab47e6eafe72c711ba1a03829f8940f975767d8cba806610) only repeats the RUNBOOK steps calnext/calrun14/calrun16 (or the PADc
  steps) until calib7.js writes the final flat file. No change of the calibration rule, tolerance or maximum (PLAN §2.1).
- D2 (2026-10-26T07:48:00Z), descriptive addition, same timing: `cidx/scripts/accuracy_c.js` (sha256 39ffe7f5ada5279802b3384ebfa7a154db21edea70b1ceaa68da68eb9230c3d5)
  computes the forecast accuracy (MAE15) of CL vs CLc vs naive with wf7's definition. PLAN §2.2 says it runs only after production;
  it is not used for any decision.
- D3 (2026-10-26T08:11:22Z), DEVIATION (calibration harness bug, promise length only; no outcome had been read): in `analysis/calib7.js` the
  "never repeat a tried r" guard compared the UNROUNDED proposal with the rounded tried values, so for G2|w17|SHRC the regula-falsi
  proposal 0.98030659 passed the guard and was then rounded to 0.980307, a value already tried. run_sh skipped it (resume), the try
  count did not grow, and `calloop.sh` stopped after 7 rounds without a final file (`logs/calib_pad.log`; flat_iter6.json and
  flat_iter7.json repeat flat_iter5.json and produced 0 runs). Cause of the bracket failure: L(r) is not monotone at the 1-s level
  (G2|w17|SHRC: L(0.980307) = 1617.72 > L(0.980763) = 1617.09). Fix: round before the guard (one line). Rule, tolerance (0.5 s)
  and the 6-try maximum are unchanged; 7 of 8 groups had already converged. New calib7.js sha256 967aa3c8fc367c4d0203b029ccfa7c38f30403edb71f43e65c6d339ecbfe842f.
  The loop is re-run with `calloop.sh PAD` (iteration files continue at k = 8; earlier iteration files are kept).
  calloop.sh: the try index list can now be given as KS (default unchanged) so iteration files are never overwritten; sha256 a7e521ae6540a8f39b31363fb064b102e62e8fcb833c0846f5ba5b25e0f929cd
- D4 (2026-10-26T08:13:22Z), record: PAD calibration ended by the 6-try rule. 7/8 groups converged (|gap| <= 0.49 s); G2|w17|SHRC did not (best r 0.979885, gap -0.60 s, PLAN §2.1: best r used, residual reported). Final file calib/flat_final.json (sha256 0b5c37874ddb96dfaf4ae85e8cd6b6b6f7410c3420aaa3c9db4a2a62823a3341). Only promise-length lines were read.
- D5 (2026-10-26T08:22:30Z), record: plgap.js printed PADC-NEEDED (|L_CLc - L_CL| up to 1.82 s, G2|w08|SHHZ; table in logs/prod_padclc.log), so PADc is calibrated and run as PLAN §2.1 says. Only promise-length lines were read.
- D6 (2026-10-26T08:37:36Z), DEVIATION (calibration harness, second guard bug; promise length only, no outcome read): for PADc G2|w08|SHRC the
  fallback proposal of the D3 guard (best r + (L* - L_best)/750) itself hit a tried value (1.030052), so `calloop.sh PADc` stalled
  (logs/calib_padc.log, flatc_iter6..8 = 0 runs). L(r) is again not monotone at the 0.5-s level (L(1.030052) = 1545.62 >
  L(1.030733) = 1545.38 > L*). Fix: the fallback is rounded and, while it equals a tried value, stepped by 0.0002 from the best
  point toward L*. Rule, tolerance and the 6-try maximum unchanged; 7 of 8 PADc groups had converged. New calib7.js sha256
  b501b27bd30d99fa5d826f959976f5b92ac36377ec099535254ebfee4aa32892. Loop resumed with KS = 9..12 (no file overwritten).
- D7 (2026-10-26T08:39:32Z), record: PADc calibration converged in all 8 groups (|gap| <= 0.5 s) after the D6 fix; calib/flatc_final.json sha256 a0eac581b6a470d51c50f39a864862fc1aade83de486066283dd9890ad35b37d. Note for the report: L(r) jumps by up to ~2 s for a 0.0002 change of r (dispatch is chaotic), so 'equal length' means equal within about +-0.5 s of a noisy quantity.
- D8 (2026-10-26T08:45:18Z), presentation addition AFTER the results were read: analysis/keys7.js (sha256 351227df166da0b76ce5bbe9d3a6c44254d4e25aa319ec69c4c47c86d58fe99e) writes results/key_numbers.txt, a compact extract of results/tables.json. No new statistic, no label change.
- D9 (2026-10-26T08:46:51Z), record: gates/GATES-ALL.txt assembles every gate output (no new check). In results/GATES-post.txt G2 and G3 read 'n/a' because the PAD(r=1) and old-index CLc rows live only in gates/gate_rows.ndjson; both passed there (24/24 each, gates/GATES-pre.txt). All jobs: 56 invocations, 8,466 runs (3,600 production, 4,770 calibration, 96 gate), 0 errors, wall 54 min, peak RSS 836 MB, max heartbeat gap 10 s.
- D10 (2026-10-26T08:48:11Z), cosmetic: the G2 line printed by gcheck7.js lists the skipped fields without 'variant', but the code (fixed before PLAN-HASH) does skip 'variant' (the PAD gate rows carry variant gate-r1). Context fact found after the results (not used for any decision): the emulator world's traffic factors in these cells equal the same Yellow month-polish index ratios (max |diff| 2.9e-4 on two checked cells, rounding), so world and forecast come from one series; CLc removes the month-polish link between them.
