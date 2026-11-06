#!/usr/bin/env bash
# run_linux.sh step 9, by hand: step 8 stopped on allExact=false (2026-11-05). Same commands as run_linux.sh lines 168-173.
set -Eeuo pipefail
ROOT=/rb
stamp=$(date -u +%Y%m%dT%H%M%SZ)
results=ridebound-results-$stamp.tar.gz
transcripts=ridebound-transcripts-$stamp.tar
echo "[$(date +%FT%T)] start $stamp"
tar -czf "$ROOT/$results" -C "$ROOT" --exclude="transcript-*.ndjson.gz" runs
(cd "$ROOT" && find runs -name "transcript-*.ndjson.gz" -print0 | sort -z | tar -cf "$transcripts" --null -T -)
(cd "$ROOT" && sha256sum "$results" "$transcripts" > "ridebound-results-$stamp.sha256")
echo "[$(date +%FT%T)] done: $ROOT/$results $ROOT/$transcripts $ROOT/ridebound-results-$stamp.sha256"
