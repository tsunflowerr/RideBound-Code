#!/usr/bin/env bash
# After watch_f16_v2.sh has downloaded and sha256-checked the F16 archives: extract runs/report-f16 only (into a new folder),
# then the 30-day synthesis with both panels real. Reads the archives; deletes nothing; refuses existing outputs.
set -eu
PY=E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe
DEST=E:/RideBoundData/research/fullsys-f16-v2/runs-linux-2026-11-06
F14=E:/RideBoundData/research/fullsys-f14-v2
F16=E:/RideBoundData/research/fullsys-f16-v2
FIN=E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02/final
R14=$F14/runs-linux-2026-11-05/extracted/runs/report
res=$(ls "$DEST"/ridebound-f16-results-*.tar.gz)
X=$DEST/extracted
OUT=$F16/synth-real-30d-2026-11-06
# an existing folder is accepted only if empty (a first attempt on 06/11 08:16 created both and then failed in tar)
[ -n "$(ls -A "$X" 2>/dev/null)" ] && { echo "$X is not empty, not extracting again"; exit 1; }
[ -n "$(ls -A "$OUT" 2>/dev/null)" ] && { echo "$OUT is not empty"; exit 1; }
mkdir -p "$X" "$OUT"
# --force-local: GNU tar reads "E:/..." as host "E" otherwise
tar --force-local -xzf "$res" -C "$X" --no-same-owner runs/report-f16
ls -la "$X/runs/report-f16"
"$PY" -I -c 'import json, sys; r = json.load(open(sys.argv[1])); print("allExact:", r.get("allExact"), r.get("returnCodes"))' \
    "$X/runs/report-f16/FINAL-REPORT.json"
"$PY" -I -B -X utf8 "$FIN/synth_real_30d.py" --out "$OUT/synth-real-30d" \
    --panel F14 "$F14/predictions/fullsys-f14-predictions.tsv" "$F14/predictions/fullsys-f14-drop-sequences.tsv" \
                "$R14/sequences.json" "$R14/riders-real-riders.tsv" \
    --panel F16 "$F16/predictions/fullsys-f16-predictions.tsv" "$F16/predictions/fullsys-f16-drop-sequences.tsv" \
                "$X/runs/report-f16/sequences.json" "$X/runs/report-f16/riders-real-riders.tsv" \
    > "$OUT/synth-real-30d.stdout.txt" 2> "$OUT/synth-real-30d.stderr.txt"
echo "synth rc=$?"
ls -la "$OUT"
