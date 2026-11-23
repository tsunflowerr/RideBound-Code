#!/usr/bin/env bash
# Quick check of the fast simulator on the two sample cells (2019-09-19, 08:00 and 17:00, sample r0):
#   1. a small data root: the road network (downloaded once from Zenodo), the frozen parameters, the two cells;
#   2. the fixtures, made from the cells with the main program's converter (src/adapter/convert_via_full_v2.py);
#   3. the three drivers of Chapter 5 on both cells (36 cell-arm rows: the rules of Tables 5.4-5.7, the forecast arms of
#      Tables 5.8-5.10, the 300 s penalty of Table 5.11);
#   4. every row's signature compared with expected.tsv.
# Needs Node 20, Python 3 with numpy, and internet for step 1. About 5-10 minutes.
# usage: bash src/fastsim/sample/kiem_nhanh.sh <new work folder>
set -euo pipefail
WORK="${1:?usage: bash kiem_nhanh.sh <new work folder>}"
[ -e "$WORK" ] && { echo "refused: $WORK exists"; exit 1; }
HERE="$(cd "$(dirname "$0")" && pwd)"
FS="$(cd "$HERE/.." && pwd)"
SRC="$(cd "$FS/.." && pwd)"
PY="${PY:-python}"
mkdir -p "$WORK"
WORK="$(cd "$WORK" && (pwd -W 2>/dev/null || pwd))"      # a path Node understands, on Windows too
CELLS="2019-09-19-w08-r0 2019-09-19-w17-r0"

echo "== 1. data root"
export RB_DATA="$WORK/data"
node "$SRC/datagen/fetch_network.js" "$RB_DATA"
cp -r "$FS/data/." "$RB_DATA/"
cp -r "$HERE/wf14" "$RB_DATA/"

echo "== 2. fixtures"
for c in $CELLS; do
  "$PY" -B "$SRC/adapter/convert_via_full_v2.py" "$RB_DATA/wf14/scenarios/weekdays-P512/$c.json" "$WORK/fixtures" \
    "d20190919-vf-r0-${c:11:3}" > "$WORK/convert-$c.json"
done
printf '%s\n' $CELLS > "$WORK/cells.txt"

echo "== 3. fast simulator"
cd "$FS/adapter60"
node emulate_adapter.js --cells "$WORK/cells.txt" --roots "$WORK/fixtures" --out "$WORK/rows-adapter" \
  --arms "INS:U:N,INS:EO35:N,INS:EOHZ35:N,INS:RCE35:N,G2:U:N,G2:EO35:N,G2:EOHZ35:N,G2:RCE35:N,G2:SHHZ71:N,G2:SHRC71:N,G2:NCE35:N,G2:OCE35:N,G2:SHRC71:CL,G2:SHRC71:CLc"
node emulate_adapter_pp.js --cells "$WORK/cells.txt" --roots "$WORK/fixtures" --out "$WORK/rows-pp" \
  --arms "G2:EOHZ35:N,G2:RCE35:N" --g2pen 300000
node emulate_adapter_eta.js --cells "$WORK/cells.txt" --roots "$WORK/fixtures" --out "$WORK/rows-eta" \
  --arms "G2:SHRC71:PAD,G2:SHHZ71:CL" --flat "$(pwd -W 2>/dev/null || pwd)/flat_iter5.json"

echo "== 4. compare with expected.tsv"
node "$HERE/check_sample.js" "$HERE/expected.tsv" emulate_adapter "$WORK/rows-adapter" emulate_adapter_pp "$WORK/rows-pp" \
  emulate_adapter_eta "$WORK/rows-eta"
