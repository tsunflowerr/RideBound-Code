#!/usr/bin/env bash
# Reproducibility spot-check of the fast simulator (synchronised/adapter mode): for every family of results used in
# Chapter 5, re-run ONE cell with the same script and arms that produced the stored rows, write to a scratch folder
# (never into the stored results), and compare the rows' `sig` with the stored rows. Runs one process at a time.
# usage: bash repro_spotcheck.sh <scratch-out-dir>
set -u
OUTROOT="${1:?scratch dir}"
HERE="$(cd "$(dirname "$0")/../adapter60" && pwd)"
STORE="${RB_STORE:?set RB_STORE to the folder of the stored result rows}"
ROOTS="${RB_ROOTS:?set RB_ROOTS to the fixture roots, separated by ;}"
FLEET_ROOTS="${RB_FLEET81_ROOT:?set RB_FLEET81_ROOT}"
mkdir -p "$OUTROOT"
cd "$HERE" || exit 1
first() { head -1 "$1"; }
run() { # name script cellsfile arms storedname roots extra...
  local name="$1" script="$2" cellsf="$3" arms="$4" stored="$5" roots="$6"; shift 6
  local cell; cell=$(first "$cellsf"); echo "$cell" > "$OUTROOT/cell-$name.txt"
  rm -rf "$OUTROOT/$name"; mkdir -p "$OUTROOT/$name"
  echo "== $name: $script cell=$cell arms=$arms $*"
  node --max-old-space-size=2500 "$script" --cells "$OUTROOT/cell-$name.txt" --arms "$arms" --roots "$roots" --out "$OUTROOT/$name" "$@" > "$OUTROOT/$name/log.txt" 2>&1
  node "$OUTROOT/compare_sig.js" "$OUTROOT/$name" "$STORE/$stored" "$name"
}
cp "$(dirname "$0")/compare_sig.js" "$OUTROOT/compare_sig.js"
run main60 emulate_adapter.js cells-60.txt "G2:SHHZ71:N,G2:SHRC71:N,G2:RCE35:N,INS:U:N" main60 "$ROOTS"
run fc30   emulate_adapter.js cells-30.txt "G2:SHRC71:CL,G2:SHRC71:CLc" fc30 "$ROOTS"
run alpha10 emulate_adapter.js cells-alpha10.txt "G2:EOHZ0:N,G2:RCE100:N" alpha10 "$ROOTS"
run fleet20 emulate_adapter.js cells-fleet30.txt "G2:EOHZ35:N,G2:RCE35:N" fleet-20 "$FLEET_ROOTS" --nveh 20
run pen300 emulate_adapter_pp.js cells-pen60.txt "G2:EOHZ35:N,G2:RCE35:N" pen300 "$ROOTS" --g2pen 300000
run eta30  emulate_adapter_eta.js cells-30.txt "G2:SHRC71:PAD,G2:SHHZ71:N" eta30 "$ROOTS" --flat "$HERE/flat_iter5.json"
echo "SPOTCHECK DONE"
