#!/usr/bin/env bash
# Convert the 96 F16 company cells into full-node fixtures (convert_via_full_v2.py, unchanged) and make the run drivers
# (make_run_drivers.py, complete-graph-direct-v1 + gzip-v1, as the F14 run drivers). Stops at the first failure.
set -u
PY=E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe
S=E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02
CELLS=E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512
ROOT=E:/RideBoundData/research/fullsys-f16-v2
LOG=$ROOT/logs/convert-f16.log
mkdir -p "$ROOT/logs"
n=0
for f in "$CELLS"/*.json; do
  b=$(basename "$f" .json)                        # 2019-09-20-w08-r0
  day=${b:0:10}; win=${b:11:3}; rs=${b:15:2}
  vf="d${day//-/}-vf-${rs}-${win}"                # d20190920-vf-r0-w08
  "$PY" -I -B "$S/convert_via_full_v2.py" "$f" "$ROOT" "$vf" >> "$LOG" 2>&1 || { echo "convert failed: $b"; exit 1; }
  "$PY" -I -B "$S/make_run_drivers.py" write "$ROOT/drivers/$vf.driver.json" "$ROOT/drivers-run/$vf.driver.json" complete-graph-direct-v1 gzip-v1 >> "$LOG" 2>&1 || { echo "driver failed: $vf"; exit 1; }
  n=$((n + 1))
done
echo "converted $n cells"
