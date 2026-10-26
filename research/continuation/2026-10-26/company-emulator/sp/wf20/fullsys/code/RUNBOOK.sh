#!/usr/bin/env bash
# wf17 r1-identity-fence-pad RUNBOOK (wf17-exploratory-post-audit). The commands as run, in order. One heavy job at a time.
# Usage: bash RUNBOOK.sh <step>   steps: r2a0 r2a1 r2a5 r2a60 r2b0 r2b1 r2b5 r1smoke r1plain r1adapter
set -u
SP="E:/RideBoundData/company-intake/2026-10-23/sp"
K="$SP/wf17/r1-identity-fence-pad"
cd "$K/code/scripts" || exit 9
D30=$(cat "$K/work/cells_D30_r0.txt"); D10=$(cat "$K/work/cells_D10_r0.txt")
SH=(--frozen frozen/shadow-params.json --x star --workers 6)
case "$1" in
  # ---- R3 (already run): node r3drive.js --out $K/results/r3.ndjson --dump $K/dumps/r3.dump.ndjson --rw $K/work/r3/rw.json --panels F14,F16 --workers 6
  r2a0)  node run_sh.js --set confirm --cells "$D30" "${SH[@]}" --arms SHF,SHHZ,SHRC,EO35,EOHZ35,RCE35 --audit --fence-tol 0     --variant r2a --out "$K/results/r2a.ndjson" ;;
  r2a1)  node run_sh.js --set confirm --cells "$D30" "${SH[@]}" --arms SHF,EO35                        --audit --fence-tol 1000  --variant r2a --out "$K/results/r2a.ndjson" ;;
  r2a5)  node run_sh.js --set confirm --cells "$D30" "${SH[@]}" --arms SHF,EO35                        --audit --fence-tol 5000  --variant r2a --out "$K/results/r2a.ndjson" ;;
  r2a60) node run_sh.js --set confirm --cells "$D30" "${SH[@]}" --arms SHF,EO35                        --audit --fence-tol 60000 --variant r2a --out "$K/results/r2a.ndjson" ;;
  r2b0)  node run_sh.js --scen "$K/cells/flat" --cells "$D10" "${SH[@]}" --arms SHF,SHHZ,SHRC,EO35,EOHZ35,RCE35 --audit --fence-tol 0    --variant r2b-flat --out "$K/results/r2b.ndjson" ;;
  r2b1)  node run_sh.js --scen "$K/cells/flat" --cells "$D10" "${SH[@]}" --arms SHF                           --audit --fence-tol 1000 --variant r2b-flat --out "$K/results/r2b.ndjson" ;;
  r2b5)  node run_sh.js --scen "$K/cells/flat" --cells "$D10" "${SH[@]}" --arms SHF,EO35                      --audit --fence-tol 5000 --variant r2b-flat --out "$K/results/r2b.ndjson" ;;
  r1smoke)   node run_sh.js --scen "$K/cells/adapter" --cells 2019-02-11-w08-r0 --configs G2 --arms RCE35 --score causal "${SH[@]}" --variant adapter-smoke --out "$K/work/smoke/r1s.ndjson" ;;
  r1plain)   node run_sh.js --set confirm --cells "$D10" "${SH[@]}" --arms U,EO35,EOHZ35,RCE35,NCE35,SHF,SHHZ,SHRC --variant plain   --out "$K/results/r1.ndjson" ;;
  r1adapter) node run_sh.js --scen "$K/cells/adapter" --cells "$D10" "${SH[@]}" --workers 3 --arms U,EO35,EOHZ35,RCE35,NCE35,SHF,SHHZ,SHRC --variant adapter --out "$K/results/r1.ndjson" ;;
  *) echo "unknown step $1"; exit 9 ;;
esac
