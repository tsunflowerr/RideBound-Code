#!/usr/bin/env bash
# RideBound F16 runs on the rented Linux machine (PLAN-F16.md, 2026-11-05), AFTER run_linux.sh (F14) has finished on the
# same machine: the base bundle, Python, .NET and the job tools under /rb are reused unchanged. Run from the folder that
# holds this script, ridebound-f16-linux.tar.gz, MANIFEST-f16.tsv and SHA256SUMS-F16:
#   tmux new -s f16
#   bash run_f16_linux.sh 2>&1 | tee -a run_f16_linux.log
# Rerunning resumes (finished steps and jobs are skipped). To stop admitting new jobs: touch /rb/runs/STOP-F16.
# Steps: 1 upload checksums  2 unpack the F16 overlay (never over an existing file)  3 verify every overlay file
#        4 gate: 4 jobs (2 cells x 2 arms) equal to the sealed predictions on all four aspects, else STOP
#        5 the 192 F16 jobs  6 comparisons and metrics  7 results archives
set -Eeuo pipefail
trap 'echo "STOPPED at line $LINENO (exit $?). Nothing is deleted; fix the cause and rerun the same command." >&2' ERR

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ROOT=/rb
LX=$ROOT/RideBoundData/final-v2-linux
KIT=$ROOT/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02
FL=$KIT/final-linux
DATA=$ROOT/RideBoundData/research/fullsys-f16-v2
JOBS=$DATA/jobs-linux
PY=$LX/python/runtime/bin/python3.10
RUNS=$ROOT/runs
OUT=$RUNS/final-f16
LOGS=$RUNS/logs-f16
RAM_FLOOR_GIB=${RAM_FLOOR_GIB:-6.5}
DISK_FLOOR_GIB=${DISK_FLOOR_GIB:-4}
export LANG=C.UTF-8 LC_ALL=C.UTF-8 TZ=ICT-7
unset PYTHONPATH PYTHONHOME PYTHONSTARTUP DOTNET_ROOT

step() { echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"; }
die() { echo "STOP: $*" >&2; exit 1; }
next_free() { local k=1; while [ -e "$1.interrupted-$k" ]; do k=$((k + 1)); done; echo "$1.interrupted-$k"; }
report_args() {
    echo --predictions "$DATA/predictions/fullsys-f16-predictions.tsv" --seal "$KIT/fullsys/FULLSYS-F16-PREDICTIONS.sha256" \
         --sequences "$DATA/predictions/fullsys-f16-drop-sequences.tsv" --sequences-seal "$KIT/fullsys/FULLSYS-F16-SEQUENCES.sha256" \
         --timed "$DATA/predictions-timed/timed-adapter.tsv.gz" --timed-seal "$KIT/fullsys/FULLSYS-F16-TIMED.sha256"
}

[ -f "$LX/.unpacked" ] || die "the F14 bundle is not unpacked under $ROOT; run run_linux.sh first"
[ -f "$RUNS/report/FINAL-REPORT.json" ] || die "the F14 run has not finished its report ($RUNS/report/FINAL-REPORT.json)"
if pgrep -fx "bash run_linux.sh" >/dev/null; then die "run_linux.sh (F14) is still running"; fi

if [ ! -f "$DATA/.unpacked" ]; then
    step "1 upload checksums"
    (cd "$HERE" && sha256sum -c SHA256SUMS-F16) || die "an uploaded file is damaged or missing; upload it again"
    step "2 unpack the F16 overlay to $ROOT"
    tar -xzf "$HERE/ridebound-f16-linux.tar.gz" -C "$ROOT" --no-same-owner --keep-old-files \
        || die "unpacking failed (an existing file is never replaced)"
    touch "$DATA/.unpacked"
fi
step "3 verify every overlay file (SHA-256)"
"$PY" -I -B "$FL/verify_bundle_linux.py" "$HERE/MANIFEST-f16.tsv" || die "F16 overlay differs from MANIFEST-f16.tsv"

mem_kib=$(awk '/MemTotal/{print $2}' /proc/meminfo)
cpus=$(nproc)
auto=$(((mem_kib / 1048576 - 4) / 7))
[ "$auto" -gt "$cpus" ] && auto=$cpus
WORKERS=${WORKERS:-$auto}
[ "$WORKERS" -ge 1 ] || die "not enough RAM for one job"
mkdir -p "$OUT" "$LOGS"
step "  workers $WORKERS"
queue() {  # queue <job list> <workers>
    "$PY" -B "$LX/tools/run_queue_linux.py" --jobs "$JOBS/$1" --out "$OUT" --logs "$LOGS" --stop "$RUNS/STOP-F16" \
        --workers "$2" --ram-floor-gib "$RAM_FLOOR_GIB" --disk-floor-gib "$DISK_FLOOR_GIB"
}

step "4 gate (4 jobs; any difference means STOP and report)"
gate=$RUNS/gate-f16
if [ -f "$gate/FINAL-REPORT.json" ]; then
    ok=$("$PY" -I -c 'import json,sys; r=json.load(open(sys.argv[1])); print(bool(r["allExact"]) and r["riders"]["finishedJobs"] == 4)' "$gate/FINAL-REPORT.json")
    [ "$ok" = True ] || die "the F16 gate failed earlier ($gate/FINAL-REPORT.json): report it, do not run the F16 jobs"
    step "  gate passed earlier"
else
    if [ -e "$gate" ]; then moved=$(next_free "$gate"); mv "$gate" "$moved"; step "  kept an interrupted gate report as $moved"; fi
    queue jobs-gate-f16.json $((WORKERS < 4 ? WORKERS : 4)) || die "gate jobs did not finish (see $OUT/PROGRESS-jobs-gate-f16.json, $LOGS)"
    # shellcheck disable=SC2046
    "$PY" -I -B "$FL/final_report_linux.py" --runs "$OUT" --out "$gate" $(report_args) || true
    ok=$("$PY" -I -c 'import json,sys; r=json.load(open(sys.argv[1])); print(bool(r["allExact"]) and r["riders"]["finishedJobs"] == 4)' "$gate/FINAL-REPORT.json")
    [ "$ok" = True ] || die "F16 GATE FAILED (see $gate): report it, do not run the F16 jobs"
    step "  gate passed: 4 jobs equal to the sealed predictions on all four aspects"
fi

step "5 F16 jobs (192; finished ones are skipped)"
queue jobs-final-f16.json "$WORKERS" || die "some F16 jobs did not finish (see $OUT/PROGRESS-jobs-final-f16.json); rerun to retry them"

step "6 comparisons and metrics"
rep=$RUNS/report-f16
if [ ! -f "$rep/FINAL-REPORT.json" ]; then
    if [ -e "$rep" ]; then moved=$(next_free "$rep"); mv "$rep" "$moved"; fi
    # shellcheck disable=SC2046
    # A difference from the sealed predictions is a result to report, not a reason to skip the archives (the F14 run
    # stopped here on 2026-11-05 with allExact false and its archives had to be made by hand).
    "$PY" -I -B "$FL/final_report_linux.py" --runs "$OUT" --out "$rep" $(report_args) \
        || step "  NOT allExact or a comparison failed: report it (see $rep/*.log); archiving anyway"
fi
[ -f "$rep/FINAL-REPORT.json" ] || die "F16 report was not written (see $rep/*.log)"
"$PY" -I -c 'import json, sys; r = json.load(open(sys.argv[1])); print("allExact:", r.get("allExact"), r.get("returnCodes"))' "$rep/FINAL-REPORT.json"

step "7 results archives"
stamp=$(date -u +%Y%m%dT%H%M%SZ)
results=ridebound-f16-results-$stamp.tar.gz
transcripts=ridebound-f16-transcripts-$stamp.tar
tar -czf "$ROOT/$results" -C "$ROOT" --exclude='transcript-*.ndjson.gz' runs/final-f16 runs/logs-f16 runs/gate-f16 runs/report-f16
(cd "$ROOT" && find runs/final-f16 -name 'transcript-*.ndjson.gz' -print0 | sort -z | tar -cf "$transcripts" --null -T -)
(cd "$ROOT" && sha256sum "$results" "$transcripts" > "ridebound-f16-results-$stamp.sha256")
step "done: download $ROOT/$results, $ROOT/$transcripts and $ROOT/ridebound-f16-results-$stamp.sha256"
