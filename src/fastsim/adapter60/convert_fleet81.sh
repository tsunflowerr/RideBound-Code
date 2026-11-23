#!/usr/bin/env bash
# A4: 60 ô của khảo sát số xe (30 ngày, r0) với bộ chuyển giống hệt, chỉ khác NVEH = 81. Ghi vào root riêng.
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
PY="${PY:-python}"
CONV="$HERE/convert_via_full_v2_nveh81.py"
SRC="${RB_DATA:?set RB_DATA}/wf14/scenarios/weekdays-P512"
ROOT="${RB_FIXTURE_OUT:?set RB_FIXTURE_OUT to a new fixture root}"
mkdir -p "$ROOT/logs"
one() {
  b="$1"; day=${b:0:10}; win=${b:11:3}; rs=${b:15:2}; vf="d${day//-/}-vf-${rs}-${win}"
  if [ -d "$ROOT/fixtures/$vf" ]; then echo "skip $vf"; return 0; fi
  "$PY" -I -B "$CONV" "$SRC/$b.json" "$ROOT" "$vf" > "$ROOT/logs/convert-$vf.json" 2> "$ROOT/logs/convert-$vf.err" && echo "ok $vf" || echo "FAIL $vf"
}
export -f one
export PY CONV SRC ROOT
tr -d '\r' < "$HERE/cells-fleet30.txt" | xargs -P 2 -I{} bash -c 'one "$@"' _ {}
echo "fixtures: $(ls "$ROOT/fixtures" | wc -l)"
