#!/usr/bin/env bash
# wf17 r3-curves-nodes RUNBOOK (wf17-exploratory-post-audit). Commands as run, in order. One heavy job at a time.
# Usage: bash RUNBOOK.sh <step> [workers]   steps: r6gate r6 r7ggen r7gen r7 r7smoke
set -u
SP="E:/RideBoundData/company-intake/2026-10-23/sp"
K="$SP/wf17/r3-curves-nodes"
NW="${2:-6}"
cd "$K/code/scripts" || exit 9
D30=$(cat "$K/work/cells_D30_r0.txt"); D10=$(cat "$K/work/cells_D10_r0.txt")
AI=$(cat "$K/work/arms_INS.txt"); AG=$(cat "$K/work/arms_G2.txt")
# R6: every theta from the discovery reference (theta(x*) bitwise = frozen, work/theta_check.json); alphaS / stride = frozen values
R6=(--ref work/s3/ref_U_causal.json --alphaS 0.15 --stride 0 --score causal --arms-INS "$AI" --arms-G2 "$AG" --variant r6)
GEN=(--windows "$SP/wf14/work/via_windows.csv" --factors "$SP/wf6/via-many-days/work/factors.json" --data "$SP/wf5/data/nyc-via/fleetpy-manhattan" --work "$SP/wf6/via-many-days/work" --set weekdays --resample 0 --fmax 512:96)
case "$1" in
  r6gate) node run_sh.js --set confirm --cells 2019-02-11-w08-r0,2019-02-11-w17-r0 "${R6[@]}" --workers "$NW" --out "$K/work/gates/g_r6.ndjson" ;;
  r6)     node run_sh.js --set confirm --cells "$D30" "${R6[@]}" --workers "$NW" --out "$K/results/r6.ndjson" ;;
  r7ggen) node gen17.js --days "$K/work/days_D10.json" "${GEN[@]}" --out "$K/cells/ggen" ;;
  r7gen)  node gen17.js --days "$K/work/days_D10.json" "${GEN[@]}" --nodetag wf17-r7-alt1 --out "$K/cells/alt1" ;;
  r7smoke) node run_sh.js --scen "$K/cells/alt1/weekdays-P512" --cells 2019-02-11-w08-r0 --configs G2 --arms RCE35 --frozen frozen/shadow-params.json --x star --score causal --variant altnodes-smoke --workers 1 --out "$K/work/smoke/r7s.ndjson" ;;
  r7)     node run_sh.js --scen "$K/cells/alt1/weekdays-P512" --cells "$D10" --frozen frozen/shadow-params.json --x star --arms U,EO35,EOHZ35,RCE35,NCE35,SHF,SHHZ,SHRC --variant altnodes --workers "$NW" --out "$K/results/r7.ndjson" ;;
  *) echo "unknown step $1"; exit 9 ;;
esac
