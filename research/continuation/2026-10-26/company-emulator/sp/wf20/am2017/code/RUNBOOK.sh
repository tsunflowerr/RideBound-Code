#!/usr/bin/env bash
# wf20/am2017 RUNBOOK (thăm dò sau kiểm toán). Commands as run, in order. Usage: bash RUNBOOK.sh <step>   steps: gates prod postgate analyze
set -u
SP="E:/RideBoundData/company-intake/2026-10-23/sp"
K="$SP/wf20/am2017"
cd "$K/code/scripts" || exit 9
G="$K/work/gates"; LOG="$G/GATES.ndjson"; W14="$SP/wf14/results/main.ndjson"; R5="$SP/wf17/r2-penalty-regime/results/r5.ndjson"
case "$1" in
  gates)
    C4="2019-02-11-w08-r0,2019-02-11-w17-r0,2019-10-03-w08-r0,2019-10-03-w17-r0"
    C2="2019-02-05-w08-r0,2019-02-05-w17-r0"
    node drive20.js --check AM --out "$G/g1.ndjson" --workers 4 --variants F62 --cells "$C4"
    node drive20.js --check AM --out "$G/g2.ndjson" --workers 4 --variants GF56,GF68 --cells "$C2" --configs G2 --arms U,EOHZ35,RCE35
    node drive20.js --check AM --out "$G/g2.ndjson" --workers 4 --variants GF56,GF68 --cells "$C2" --configs INS --arms EOHZ35,RCE35
    node gate17.js --name "G1 F62 (4 cells x 8 arm-configs) vs wf14 main r0" --mine "$G/g1.ndjson" --filter-mine variant=F62 --ref "$W14" --filter-ref part=M,mode=N,rs=r0 --expect 32 --log "$LOG"
    node gate17.js --name "G2 GF56 vs R5 V-F56" --mine "$G/g2.ndjson" --filter-mine variant=GF56 --ref "$R5" --filter-ref variant=V-F56 --expect 10 --log "$LOG"
    node gate17.js --name "G2 GF68 vs R5 V-F68" --mine "$G/g2.ndjson" --filter-mine variant=GF68 --ref "$R5" --filter-ref variant=V-F68 --expect 10 --log "$LOG"
    ;;
  prod) node drive20.js --check AM --out "$K/results/am.ndjson" --workers 4 ;;
  postgate)
    node gate17.js --name "G3 all F62 production rows vs wf14 main r0" --mine "$K/results/am.ndjson" --filter-mine variant=F62 --ref "$W14" --filter-ref part=M,mode=N,rs=r0 --expect 480 --log "$LOG"
    ;;
  analyze) cd "$K/analysis" && node am_analyze.js "$K/results/am.ndjson" "$K/results/am_tables.txt" ;;
  *) echo "unknown step $1"; exit 9 ;;
esac
