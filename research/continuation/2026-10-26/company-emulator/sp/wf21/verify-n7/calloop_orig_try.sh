#!/usr/bin/env bash
# wf21/n7 calibration driver (PLAN §2.1): repeats RUNBOOK calnext/calrun until calib7.js writes the final flat file (max 6 tries).
# Usage: bash calloop.sh PAD|PADc     (PADc only when plgap.js printed PADC-NEEDED)
set -u
K="E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7"
cd "$K" || exit 9
if [ "$1" = "PAD" ]; then NEXT=calnext; R14=calrun14; R16=calrun16; FINAL="$K/calib/flat_final.json";
else NEXT=padcnext; R14=padcrun14; R16=padcrun16; FINAL="$K/calib/flatc_final.json"; fi
for k in 1 2 3 4 5 6 7; do
  echo "== $1 try $k $(date -u +%FT%TZ)"
  bash RUNBOOK.sh $NEXT $k || { echo "calib7 failed"; exit 1; }
  if [ -f "$FINAL" ]; then echo "final flat file written: $FINAL"; exit 0; fi
  bash RUNBOOK.sh $R14 $k || { echo "$R14 failed"; exit 1; }
  bash RUNBOOK.sh $R16 $k || { echo "$R16 failed"; exit 1; }
done
echo "no final file after 7 rounds"; exit 1
