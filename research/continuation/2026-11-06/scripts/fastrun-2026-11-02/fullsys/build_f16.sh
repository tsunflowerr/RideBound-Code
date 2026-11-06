#!/usr/bin/env bash
# PLAN-F16.md steps 3-4 and the pack, in order; stops at the first failure. Every output refuses to overwrite.
set -u
PY=E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe
K=E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02
R=E:/RideBoundData/research/fullsys-f16-v2
L=$R/logs/build-f16.log
log() { echo "[$(date -u +%FT%TZ)] $*" | tee -a "$L"; }
run() { local name=$1; shift; log "start $name"; "$@" > "$R/logs/$name.log" 2>&1; local rc=$?; log "end   $name rc=$rc"; tail -2 "$R/logs/$name.log" | tee -a "$L"; [ $rc -eq 0 ] || { log "STOP at $name"; exit 1; }; }
cd "$K/fullsys" || exit 9
mkdir -p "$R/predictions" "$R/predictions-timed"
run adapter-predictions node emulate_fullsys_f16.js adapter "$R" "$R/predictions/fullsys-f16-predictions.tsv"
run seal-predictions "$PY" -I -B seal_f16.py predictions
run sequences node emulate_sequences_f16.js "$R" "$R/predictions/fullsys-f16-predictions.tsv" "$R/predictions/fullsys-f16-drop-sequences.tsv"
run seal-sequences "$PY" -I -B seal_f16.py sequences
run job-configs node make_job_configs.js fullsys "$R"
run job-configs-v2 "$PY" -I -B make_job_configs_v2.py "$R" E:/RideBoundData/final-v2/repo/simulators/fleetpy-ridebound
run jobs-linux "$PY" -I -B "$K/final/make_f16_jobs_linux.py"
run timed env TIMED_GZIP=1 node emulate_timed2_f16.js adapter "$R" "$R/predictions-timed"
run seal-timed "$PY" -I -B seal_f16.py timed
run pack "$PY" -I -B "$K/final-linux/pack_f16.py"
log "done"
