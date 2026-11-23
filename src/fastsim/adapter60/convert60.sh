#!/usr/bin/env bash
# Chuyển 276 ô của 60 ngày chính sang dạng mạng nút đầy đủ bằng đúng bộ chuyển convert_via_full_v2.py
# (bộ đã dùng cho 30 ngày chạy RideBound đầy đủ). Không sửa bộ chuyển. Không ghi đè (bộ chuyển tự từ chối).
# Usage: bash convert60.sh   (chạy 6 tiến trình song song)
set -u
PY="${PY:-python}"
CONV="$HERE/../../adapter/convert_via_full_v2.py"
SRC="${RB_DATA:?set RB_DATA}/wf14/scenarios/weekdays-P512"
ROOT="${RB_FIXTURE_OUT:?set RB_FIXTURE_OUT to a new fixture root}"
HERE="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$ROOT/logs"
one() {
  b="$1"                                   # 2019-02-05-w08-r0
  day=${b:0:10}; win=${b:11:3}; rs=${b:15:2}
  vf="d${day//-/}-vf-${rs}-${win}"         # d20190205-vf-r0-w08
  if [ -d "$ROOT/fixtures/$vf" ]; then echo "skip $vf"; return 0; fi
  "$PY" -I -B "$CONV" "$SRC/$b.json" "$ROOT" "$vf" > "$ROOT/logs/convert-$vf.json" 2> "$ROOT/logs/convert-$vf.err" \
    && echo "ok $vf" || echo "FAIL $vf"
}
export -f one
export PY CONV SRC ROOT
tr -d '\r' < "$HERE/cells-60-convert.txt" | xargs -P 6 -I{} bash -c 'one "$@"' _ {}
echo "fixtures: $(ls "$ROOT/fixtures" | wc -l)"
