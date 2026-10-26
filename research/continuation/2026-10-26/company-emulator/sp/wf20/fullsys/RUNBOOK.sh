#!/usr/bin/env bash
# wf20/fullsys RUNBOOK (label wf20-fullsys-exploratory-post-audit = thăm dò sau kiểm toán). Commands exactly as run, in order.
# Usage: bash RUNBOOK.sh <step>   steps: prior gate gatecheck f14n f14cl f16n f16cl check analyze
set -u
SP="E:/RideBoundData/company-intake/2026-10-23/sp"
K="$SP/wf20/fullsys"
F16DIR="$SP/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512"
F16PRIOR="$K/work/prior_F16/prior_prevhour.csv"
F14DAYS="$K/work/days_F14.csv"
SH=(--frozen frozen/shadow-params.json --x star --arms SHF,SHHZ,SHRC,EOHZ35,RCE35 --workers 4)
GC14=2019-10-03-w08-r0,2019-10-03-w17-r0,2019-12-18-w08-r1,2019-12-18-w17-r2
GC16=2019-09-20-w08-r0,2019-12-19-w17-r1
cd "$K/code/scripts" || exit 9
case "$1" in
  prior)     node prep_prior_f16.js ;;
  gate)      for m in N CL; do
               node run_sh.js --set confirm --days-file "$F14DAYS" --cells "$GC14" --panel F14 --fc $m "${SH[@]}" --out "$K/gates/gate_rows.ndjson" || exit 1
               node run_sh.js --scen "$F16DIR" --cells "$GC16" --prior "$F16PRIOR" --panel F16 --fc $m "${SH[@]}" --out "$K/gates/gate_rows.ndjson" || exit 1
             done ;;
  gatecheck) node "$K/analysis/gcheck.js" --rows "$K/gates/gate_rows.ndjson" --out "$K/gates/GATES-pre.txt" ;;
  f14n)      node run_sh.js --set confirm --days-file "$F14DAYS" --panel F14 --fc N  "${SH[@]}" --out "$K/results/rows.ndjson" ;;
  f14cl)     node run_sh.js --set confirm --days-file "$F14DAYS" --panel F14 --fc CL "${SH[@]}" --out "$K/results/rows.ndjson" ;;
  f16n)      node run_sh.js --scen "$F16DIR" --prior "$F16PRIOR" --panel F16 --fc N  "${SH[@]}" --out "$K/results/rows.ndjson" ;;
  f16cl)     node run_sh.js --scen "$F16DIR" --prior "$F16PRIOR" --panel F16 --fc CL "${SH[@]}" --out "$K/results/rows.ndjson" ;;
  check)     node "$K/analysis/gcheck.js" --rows "$K/results/rows.ndjson" --out "$K/results/GATES-post.txt" --expect 1 --det "$K/gates/gate_rows.ndjson" ;;
  analyze)   node "$K/analysis/fsan.js" --rows "$K/results/rows.ndjson" --spec production --out "$K/results/tables" ;;
  *) echo "unknown step $1"; exit 9 ;;
esac
