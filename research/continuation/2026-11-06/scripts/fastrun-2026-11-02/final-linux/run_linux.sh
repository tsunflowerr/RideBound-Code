#!/usr/bin/env bash
# RideBound final runs on a rented Linux machine (ADR-089 GD5/GD6; Linux bundle 2026-11-04).
# x86_64 Linux with glibc (Ubuntu 22.04 tested target). Run as root, or as a user with sudo, from the folder that holds
# this script and ridebound-final-v2.zip, MANIFEST.tsv, ridebound-final-v2-linux.tar.gz, MANIFEST-linux.tsv, SHA256SUMS:
#   tmux new -s rb
#   bash run_linux.sh 2>&1 | tee -a run_linux.log
# Rerunning resumes: finished steps and finished jobs are skipped. To stop admitting new jobs: touch /rb/runs/STOP.
# Optional settings (environment): WORKERS (default: from RAM and CPUs), RAM_FLOOR_GIB (6.5), DISK_FLOOR_GIB (4).
# Steps, each stops the script on failure:
#   0 prerequisites   1 upload checksums   2 unpack to /rb   3 Python, packages, .NET   4 verify every file (SHA-256)
#   5 machine checks (versions, git HEAD, repository inventory = home value)
#   6 gates: 4 main-arm jobs + 4 attribution jobs, all equal to the sealed predictions rider by rider
#   7 the 168 final jobs   8 comparisons and metrics   9 results archive
set -Eeuo pipefail
trap 'echo "STOPPED at line $LINENO (exit $?). Nothing is deleted; fix the cause and rerun the same command." >&2' ERR

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ROOT=/rb
LX=$ROOT/RideBoundData/final-v2-linux
FL=$ROOT/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02/final-linux
JOBS=$ROOT/RideBoundData/research/fullsys-f14-v2/jobs-linux
PY=$LX/python/runtime/bin/python3.10
DOTNET=$LX/dotnet/runtime/dotnet
RUNS=$ROOT/runs
RAM_FLOOR_GIB=${RAM_FLOOR_GIB:-6.5}
DISK_FLOOR_GIB=${DISK_FLOOR_GIB:-4}
PYTHON_TGZ=cpython-3.10.22+20261009-x86_64-unknown-linux-gnu-install_only.tar.gz
DOTNET_TGZ=dotnet-runtime-10.0.9-linux-x64.tar.gz

export LANG=C.UTF-8 LC_ALL=C.UTF-8 TZ=ICT-7   # TZ: the home machine's UTC+7 (only log times use local time)
unset PYTHONPATH PYTHONHOME PYTHONSTARTUP DOTNET_ROOT

step() { echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"; }
die() { echo "STOP: $*" >&2; exit 1; }
as_root() { if [ "$(id -u)" -eq 0 ]; then "$@"; else sudo "$@"; fi; }
next_free() { local k=1; while [ -e "$1.interrupted-$k" ]; do k=$((k + 1)); done; echo "$1.interrupted-$k"; }

step "0 prerequisites"
[ "$(uname -m)" = x86_64 ] || die "x86_64 Linux is required, this machine is $(uname -m)"
missing=()
for tool in git tar gzip sha256sum sha512sum ldconfig awk; do command -v "$tool" >/dev/null || missing+=("$tool"); done
ldconfig -p 2>/dev/null | grep -q 'libicuuc\.so\.' || missing+=(libicu)
if [ ${#missing[@]} -gt 0 ]; then
    step "  installing: ${missing[*]}"
    command -v apt-get >/dev/null || die "missing ${missing[*]}; install them with the distribution's package manager"
    as_root apt-get update -q
    icu=$(apt-cache search --names-only '^libicu[0-9]+$' | cut -d' ' -f1 | sort -V | tail -1)
    as_root env DEBIAN_FRONTEND=noninteractive apt-get install -y -q git tar gzip coreutils libc-bin gawk ${icu:+"$icu"}
    ldconfig -p | grep -q 'libicuuc\.so\.' || die "libicu is still missing after apt-get"
fi
step "  $(ldd --version 2>&1 | head -1); $(git --version); CPUs $(nproc); $(awk '/MemTotal/{printf "RAM %.1f GiB", $2/2^20}' /proc/meminfo)"

if [ ! -f "$LX/.unpacked" ]; then
    step "1 upload checksums"
    (cd "$HERE" && sha256sum -c SHA256SUMS) || die "an uploaded file is damaged or missing (see above); upload it again"

    step "2 unpack to $ROOT"
    [ -L "$ROOT" ] && die "$ROOT is a symbolic link; it must be a real folder (paths are compared after resolving)"
    if [ ! -d "$ROOT" ]; then as_root mkdir -p "$ROOT"; as_root chown "$(id -u):$(id -g)" "$ROOT"; fi
    # Unpacked: base 6.8 GiB + Python, packages and .NET ~1.2 GiB. Runs later: ~6 GiB kept + ~0.8 GiB per running job,
    # and the results archives ~6 GiB more.
    free=$(df -BG --output=avail "$ROOT" | tail -1 | tr -dc 0-9)
    [ "$free" -ge 10 ] || die "$ROOT has ${free} GiB free; unpacking needs 10 GiB (whole run: 22 GiB + 1 GiB per worker)"
    tar -xzf "$HERE/ridebound-final-v2-linux.tar.gz" -C "$ROOT" --no-same-owner
    # tr: dotnet-runtime.sha512 was written on Windows with CRLF line ends (found on the first rented run, 2026-11-04);
    # the digest lines themselves are unchanged
    (cd "$LX/python" && tr -d '\r' < python.sha256 | sha256sum -c -) || die "Python archive checksum"
    (cd "$LX/dotnet" && tr -d '\r' < dotnet-runtime.sha512 | sha512sum -c -) || die ".NET archive checksum"
    mkdir -p "$LX/python/runtime" "$LX/dotnet/runtime"
    tar -xzf "$LX/python/$PYTHON_TGZ" -C "$LX/python/runtime" --strip-components=1 --no-same-owner
    tar -xzf "$LX/dotnet/$DOTNET_TGZ" -C "$LX/dotnet/runtime" --no-same-owner
    # Python's zipfile, not unzip: it reads the UTF-8 flag of the one file name with Vietnamese letters.
    step "  extracting ridebound-final-v2.zip (66,545 files, 6.8 GiB)"
    "$PY" -I -B -c 'import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' \
        "$HERE/ridebound-final-v2.zip" "$ROOT"
    touch "$LX/.unpacked"
fi

step "3 Python packages (offline wheels) and .NET"
"$PY" -m pip install -q --no-index --disable-pip-version-check --no-warn-script-location \
    --find-links "$LX/python/wheels" -r "$LX/python/requirements-linux.txt"
"$DOTNET" --list-runtimes

step "4 verify every file (SHA-256)"
"$PY" -I -B "$FL/verify_bundle_linux.py" "$HERE/MANIFEST.tsv" || die "base bundle differs from MANIFEST.tsv"
"$PY" -I -B "$FL/verify_bundle_linux.py" "$HERE/MANIFEST-linux.tsv" || die "Linux overlay differs from MANIFEST-linux.tsv"

step "5 machine checks"
mkdir -p "$RUNS/final" "$RUNS/gates" "$RUNS/logs"
"$PY" -I -B "$FL/check_env_linux.py" "$RUNS" || die "machine checks failed (see $RUNS/ENVIRONMENT.json)"

mem_kib=$(awk '/MemTotal/{print $2}' /proc/meminfo)
limit=$(cat /sys/fs/cgroup/memory.max 2>/dev/null || cat /sys/fs/cgroup/memory/memory.limit_in_bytes 2>/dev/null || echo max)
if [[ "$limit" =~ ^[0-9]+$ ]] && [ $((limit / 1024)) -lt "$mem_kib" ]; then mem_kib=$((limit / 1024)); fi
cpus=$(nproc)
if [ -r /sys/fs/cgroup/cpu.max ]; then
    read -r quota period < /sys/fs/cgroup/cpu.max
    if [ "$quota" != max ]; then c=$(((quota + period - 1) / period)); [ "$c" -lt "$cpus" ] && cpus=$c; fi
fi
# One job peaks at about 6.2 GB (Runner and adapter, measured at home); 7 GiB per job plus 4 GiB for the system.
auto=$(((mem_kib / 1048576 - 4) / 7))
[ "$auto" -gt "$cpus" ] && auto=$cpus
# A container (e.g. a vast.ai offer showing "12.0/96 CPU, 64/516 GB") may see the whole host when the host does not set
# hard cgroup limits, so the automatic value could exceed the rented share: there WORKERS must be given.
in_container=0
[ -f /.dockerenv ] && in_container=1
command -v systemd-detect-virt >/dev/null && systemd-detect-virt -cq && in_container=1
if [ "$in_container" -eq 1 ] && [ -z "${WORKERS:-}" ]; then
    die "container: set WORKERS = min(rented CPU cores, (rented RAM GB - 4) / 7), e.g. 12 CPU + 64 GB -> WORKERS=8 bash run_linux.sh (this machine sees $cpus CPUs, $((mem_kib / 1048576)) GiB)"
fi
WORKERS=${WORKERS:-$auto}
[ "$WORKERS" -ge 1 ] || die "not enough RAM for one job (need about 11 GiB)"
free=$(df -BG --output=avail "$ROOT" | tail -1 | tr -dc 0-9)
step "  workers $WORKERS (RAM limit $((mem_kib / 1048576)) GiB, CPUs $cpus, disk free ${free} GiB)"
if [ ! -f "$RUNS/report/FINAL-REPORT.json" ] && [ "$free" -lt $((12 + WORKERS)) ]; then
    step "  WARNING: ${free} GiB free; runs and archives need about $((12 + WORKERS)) GiB (jobs wait at ${DISK_FLOOR_GIB} GiB)"
fi

queue() {  # queue <job list> <output folder> <workers>
    "$PY" -B "$LX/tools/run_queue_linux.py" --jobs "$JOBS/$1" --out "$2" --logs "$RUNS/logs" --stop "$RUNS/STOP" \
        --workers "$3" --ram-floor-gib "$RAM_FLOOR_GIB" --disk-floor-gib "$DISK_FLOOR_GIB"
}

step "6 gates (8 jobs; a difference means STOP and report, do not run the final jobs)"
report=$RUNS/gates-report
if [ -f "$report/GATES-REPORT.json" ]; then
    passed=$("$PY" -I -c 'import json, sys; print(json.load(open(sys.argv[1]))["pass"])' "$report/GATES-REPORT.json")
    [ "$passed" = True ] || die "the gates failed earlier ($report/GATES-REPORT.json): report it, do not run the final jobs"
    step "  gates passed earlier"
else
    if [ -e "$report" ]; then moved=$(next_free "$report"); mv "$report" "$moved"; step "  kept an interrupted gate report as $moved"; fi
    gate_workers=$((WORKERS < 4 ? WORKERS : 4))
    # "|| exit": a failing background queue returns its code to wait below without firing the ERR trap
    { queue jobs-cloud-check.json "$RUNS/final" "$gate_workers" || exit $?; } & main_pid=$!
    if [ "$WORKERS" -ge 8 ]; then
        { queue jobs-gate-attr.json "$RUNS/gates" 4 || exit $?; } & attr_pid=$!
        main_rc=0; wait "$main_pid" || main_rc=$?
        attr_rc=0; wait "$attr_pid" || attr_rc=$?
    else
        main_rc=0; wait "$main_pid" || main_rc=$?
        attr_rc=0; queue jobs-gate-attr.json "$RUNS/gates" "$gate_workers" || attr_rc=$?
    fi
    [ "$main_rc" -eq 0 ] || die "main-arm gate jobs (see $RUNS/final/PROGRESS-jobs-cloud-check.json and $RUNS/logs)"
    [ "$attr_rc" -eq 0 ] || die "attribution gate jobs (see $RUNS/gates/PROGRESS-jobs-gate-attr.json and $RUNS/logs)"
    "$PY" -I -B "$FL/cloud_gates_linux.py" --check-runs "$RUNS/final" --attr-runs "$RUNS/gates" --out "$report" \
        || die "GATES FAILED (see $report): report it, do not run the final jobs"
fi

step "7 final jobs (168; finished ones are skipped)"
queue jobs-final.json "$RUNS/final" "$WORKERS" \
    || die "some final jobs did not finish (see $RUNS/final/PROGRESS-jobs-final.json); rerun to retry them"

step "8 comparisons and metrics"
if [ ! -f "$RUNS/report/FINAL-REPORT.json" ]; then
    if [ -e "$RUNS/report" ]; then moved=$(next_free "$RUNS/report"); mv "$RUNS/report" "$moved"; fi
    "$PY" -I -B "$FL/final_report_linux.py" --runs "$RUNS/final" --out "$RUNS/report" \
        || die "final report (see $RUNS/report/*.log)"
fi
"$PY" -I -c 'import json, sys; r = json.load(open(sys.argv[1])); print("allExact:", r.get("allExact"))' \
    "$RUNS/report/FINAL-REPORT.json"

step "9 results archives"
# results: everything but the 4 kept transcripts (~2.5 GB: evidence, logs, gates, report, environment);
# transcripts: the 4 kept transcripts of the gate cells (~3 GB, already gzip).
used=$(du -s -BG "$RUNS" | cut -f1 | tr -dc 0-9)
free=$(df -BG --output=avail "$ROOT" | tail -1 | tr -dc 0-9)
[ "$free" -gt $((used + 1)) ] || die "${free} GiB free is too little to archive ${used} GiB; copy $RUNS directly (rsync)"
stamp=$(date -u +%Y%m%dT%H%M%SZ)
results=ridebound-results-$stamp.tar.gz
transcripts=ridebound-transcripts-$stamp.tar
tar -czf "$ROOT/$results" -C "$ROOT" --exclude='transcript-*.ndjson.gz' runs
(cd "$ROOT" && find runs -name 'transcript-*.ndjson.gz' -print0 | sort -z | tar -cf "$transcripts" --null -T -)
(cd "$ROOT" && sha256sum "$results" "$transcripts" > "ridebound-results-$stamp.sha256")
step "done: download $ROOT/$results first, then $ROOT/$transcripts and $ROOT/ridebound-results-$stamp.sha256"
step "      e.g. scp -P <port> root@<host>:$ROOT/$results ."
