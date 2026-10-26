#!/usr/bin/env bash
# wf17 r2-penalty-regime RUNBOOK (wf17-exploratory-post-audit). Commands as run, in order. One heavy job at a time.
# Usage: bash RUNBOOK.sh <step> [workers]   steps: gates r4 r5
set -u
SP="E:/RideBoundData/company-intake/2026-10-23/sp"
K="$SP/wf17/r2-penalty-regime"
NW="${2:-6}"
cd "$K/code/scripts" || exit 9
G="$K/work/gates"; LOG="$G/GATES.ndjson"; W14="$SP/wf14/results/main.ndjson"; A5="$SP/wf16/a5-steering/results/robust.ndjson"
AM="EO=EO35,EOHZ=EOHZ35,RCE=RCE35"
case "$1" in
  gates)
    C4="2019-02-11-w08-r0,2019-02-11-w17-r0,2019-10-03-w08-r0,2019-10-03-w17-r0"
    C2="2019-02-05-w08-r0,2019-02-05-w17-r0,2019-02-11-w08-r0,2019-02-11-w17-r0"
    node drive17.js --check R4 --out "$G/g_r4.ndjson" --workers "$NW" --variants T8P0 --cells "$C4"
    node drive17.js --check R4 --out "$G/g_r4.ndjson" --workers "$NW" --variants T8P60,T8P300 --cells "$C2" --arms EO35,EOHZ35,RCE35
    node drive17.js --check R5 --out "$G/g_r5.ndjson" --workers "$NW" --variants B0 --cells "$C4"
    node drive17.js --check R5 --out "$G/g_r5.ndjson" --workers "$NW" --variants V-R15,V-R25 --cells 2019-02-05-w08-r0,2019-02-05-w17-r0 --arms EOHZ35,RCE35
    node gate17.js --name "G-ENG-sub R4 T8P0 vs wf14 main G2 r0" --mine "$G/g_r4.ndjson" --filter-mine variant=T8P0 --ref "$W14" --filter-ref part=M,mode=N,rs=r0,config=G2 --expect 20 --log "$LOG"
    node gate17.js --name "G-P2 T8P60 vs a5 V11" --mine "$G/g_r4.ndjson" --filter-mine variant=T8P60 --ref "$A5" --filter-ref variant=V11_g2pen60,config=G2 --map-arm-ref "$AM" --expect 12 --log "$LOG"
    node gate17.js --name "G-P2 T8P300 vs a5 V12" --mine "$G/g_r4.ndjson" --filter-mine variant=T8P300 --ref "$A5" --filter-ref variant=V12_g2pen300,config=G2 --map-arm-ref "$AM" --expect 12 --log "$LOG"
    node gate17.js --name "G-ENG-sub R5 B0 vs wf14 main r0" --mine "$G/g_r5.ndjson" --filter-mine variant=B0 --ref "$W14" --filter-ref part=M,mode=N,rs=r0 --expect 24 --log "$LOG"
    node gate17.js --name "R5 V-R15 vs a5 V1 (2 cells)" --mine "$G/g_r5.ndjson" --filter-mine variant=V-R15 --ref "$A5" --filter-ref variant=V1_ride15 --map-arm-ref "$AM" --expect 8 --log "$LOG"
    node gate17.js --name "R5 V-R25 vs a5 V2 (2 cells)" --mine "$G/g_r5.ndjson" --filter-mine variant=V-R25 --ref "$A5" --filter-ref variant=V2_ride25 --map-arm-ref "$AM" --expect 8 --log "$LOG"
    ;;
  smoke)
    # timing / error smoke only (outcomes not read): slowest R4 config and the two new R5 regimes on one cell
    node drive17.js --check R4 --out "$K/work/smoke/s_r4.ndjson" --workers "$NW" --variants T16P0,T16P300 --cells 2019-02-05-w17-r0
    node drive17.js --check R5 --out "$K/work/smoke/s_r5.ndjson" --workers "$NW" --variants V-P256,V-D24,V-F68 --cells 2019-02-05-w17-r0
    ;;
  r4) node drive17.js --check R4 --out "$K/results/r4.ndjson" --workers "$NW" ;;
  r5) node drive17.js --check R5 --out "$K/results/r5.ndjson" --workers "$NW" ;;
  *) echo "unknown step $1"; exit 9 ;;
esac
