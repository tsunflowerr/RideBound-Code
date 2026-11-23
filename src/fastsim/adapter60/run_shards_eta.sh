#!/usr/bin/env bash
# Chạy emulate_adapter_eta.js thành nhiều tiến trình song song rồi đợi tất cả xong.
# Usage: bash run_shards.sh <tên lượt> <tệp ô> <danh sách nhánh> <số tiến trình> [--riders]
set -u
NAME="$1"; CELLS="$2"; ARMS="$3"; N="$4"; EXTRA="${5:-}"
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="${RB_RESULTS:?set RB_RESULTS to a new results folder}/$NAME"
ROOTS="${ROOTS_OVERRIDE:-${RB_ROOTS:?set RB_ROOTS to the fixture roots, separated by ;}}"
mkdir -p "$OUT"
cd "$HERE" || exit 1
for i in $(seq 0 $((N - 1))); do
  node --max-old-space-size=3000 emulate_adapter_eta.js --cells "$CELLS" --arms "$ARMS" --roots "$ROOTS" --out "$OUT" --shard "$i/$N" $EXTRA > "$OUT/log-$i.txt" 2>&1 &
done
fail=0
for job in $(jobs -p); do wait "$job" || fail=$((fail + 1)); done
echo "$NAME: $N shards, failed $fail"
cat "$OUT"/rows-*.ndjson 2>/dev/null | wc -l
