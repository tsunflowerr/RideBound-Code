#!/usr/bin/env bash
# wf21/n7 RUNBOOK (label wf21-n7-exploratory-post-audit = thăm dò sau kiểm toán). Commands exactly as run, in order.
# Usage: bash RUNBOOK.sh <step> [k]
#   steps: gate gatecheck n14 n16 cl14 cl16 calnext calrun14 calrun16 pad14 pad16 clc14 clc16 padccheck padcnext padcrun14 padcrun16
#          padc14 padc16 check analyze
set -u
SP="E:/RideBoundData/company-intake/2026-10-23/sp"
K="$SP/wf21/n7"
F16DIR="$SP/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512"
F16PRIOR="$SP/wf20/fullsys/work/prior_F16/prior_prevhour.csv"
F14DAYS="$SP/wf20/fullsys/work/days_F14.csv"
SH=(--frozen frozen/shadow-params.json --x star --arms SHRC,SHHZ --workers 4)
GC14=2019-10-03-w08-r0,2019-10-03-w17-r0,2019-12-18-w08-r1,2019-12-18-w17-r2
GC16=2019-09-20-w08-r0,2019-12-19-w17-r1
CIDX="$K/cidx/results/index-causal-pertarget.json"
OIDX="$K/cidx/results/index-old-pertarget.json"
ROWS="$K/results/rows.ndjson"
CAL="$K/calib/calib.ndjson"
P14=(--set confirm --days-file "$F14DAYS" --panel F14)
P16=(--scen "$F16DIR" --prior "$F16PRIOR" --panel F16)
cd "$K/code/scripts" || exit 9
case "$1" in
  gate)      G="$K/gates/gate_rows.ndjson"
             for m in N CL; do
               node run_sh.js "${P14[@]}" --cells "$GC14" --fc $m "${SH[@]}" --out "$G" || exit 1
               node run_sh.js "${P16[@]}" --cells "$GC16" --fc $m "${SH[@]}" --out "$G" || exit 1
             done
             node run_sh.js "${P14[@]}" --cells "$GC14" --fc PAD --flat "$K/gates/flat_r1.json" --variant gate-r1 "${SH[@]}" --out "$G" || exit 1
             node run_sh.js "${P16[@]}" --cells "$GC16" --fc PAD --flat "$K/gates/flat_r1.json" --variant gate-r1 "${SH[@]}" --out "$G" || exit 1
             node run_sh.js "${P14[@]}" --cells "$GC14" --fc CLc --fc-index "$OIDX" --variant gate-oldidx "${SH[@]}" --out "$G" || exit 1
             node run_sh.js "${P16[@]}" --cells "$GC16" --fc CLc --fc-index "$OIDX" --variant gate-oldidx "${SH[@]}" --out "$G" || exit 1 ;;
  gatecheck) node "$K/analysis/gcheck7.js" --rows "$K/gates/gate_rows.ndjson" --out "$K/gates/GATES-pre.txt" ;;
  n14)       node run_sh.js "${P14[@]}" --fc N  "${SH[@]}" --out "$ROWS" ;;
  n16)       node run_sh.js "${P16[@]}" --fc N  "${SH[@]}" --out "$ROWS" ;;
  cl14)      node run_sh.js "${P14[@]}" --fc CL "${SH[@]}" --out "$ROWS" ;;
  cl16)      node run_sh.js "${P16[@]}" --fc CL "${SH[@]}" --out "$ROWS" ;;
  # PAD calibration (promise length only): calnext k writes calib/flat_iter<k>.json (or calib/flat_final.json when done)
  calnext)   node "$K/analysis/calib7.js" --ref "$ROWS" --refMode CL --calib "$CAL" --variant calPAD --out "$K/calib/flat_iter$2.json" --final "$K/calib/flat_final.json" ;;
  calrun14)  node run_sh.js "${P14[@]}" --fc PAD --flat "$K/calib/flat_iter$2.json" --variant calPAD "${SH[@]}" --out "$CAL" ;;
  calrun16)  node run_sh.js "${P16[@]}" --fc PAD --flat "$K/calib/flat_iter$2.json" --variant calPAD "${SH[@]}" --out "$CAL" ;;
  pad14)     node run_sh.js "${P14[@]}" --fc PAD --flat "$K/calib/flat_final.json" "${SH[@]}" --out "$ROWS" ;;
  pad16)     node run_sh.js "${P16[@]}" --fc PAD --flat "$K/calib/flat_final.json" "${SH[@]}" --out "$ROWS" ;;
  clc14)     node run_sh.js "${P14[@]}" --fc CLc --fc-index "$CIDX" "${SH[@]}" --out "$ROWS" ;;
  clc16)     node run_sh.js "${P16[@]}" --fc CLc --fc-index "$CIDX" "${SH[@]}" --out "$ROWS" ;;
  # PADc rule (PLAN §3): only if some |L_CLc - L_CL| > 1.0 s (padccheck prints the table, reads promise length only)
  padccheck) node "$K/analysis/plgap.js" --rows "$ROWS" ;;
  padcnext)  node "$K/analysis/calib7.js" --ref "$ROWS" --refMode CLc --calib "$CAL" --variant calPADc --out "$K/calib/flatc_iter$2.json" --final "$K/calib/flatc_final.json" ;;
  padcrun14) node run_sh.js "${P14[@]}" --fc PAD --flat "$K/calib/flatc_iter$2.json" --variant calPADc "${SH[@]}" --out "$CAL" ;;
  padcrun16) node run_sh.js "${P16[@]}" --fc PAD --flat "$K/calib/flatc_iter$2.json" --variant calPADc "${SH[@]}" --out "$CAL" ;;
  padc14)    node run_sh.js "${P14[@]}" --fc PAD --flat "$K/calib/flatc_final.json" --variant PADc "${SH[@]}" --out "$ROWS" ;;
  padc16)    node run_sh.js "${P16[@]}" --fc PAD --flat "$K/calib/flatc_final.json" --variant PADc "${SH[@]}" --out "$ROWS" ;;
  check)     node "$K/analysis/gcheck7.js" --rows "$ROWS" --out "$K/results/GATES-post.txt" --post --det "$K/gates/gate_rows.ndjson" --calib "$CAL" ;;
  analyze)   if [ -f "$K/calib/flatc_final.json" ]; then FC=(--flatc "$K/calib/flatc_final.json"); else FC=(); fi
             node "$K/analysis/an7.js" --rows "$ROWS" --flat "$K/calib/flat_final.json" "${FC[@]}" --out "$K/results/tables" ;;
  *) echo "unknown step $1"; exit 9 ;;
esac
