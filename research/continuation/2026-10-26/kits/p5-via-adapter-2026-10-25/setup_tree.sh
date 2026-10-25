#!/usr/bin/env bash
# P5 (audit 23/10 §6-P5): build a home copy of the company's wf15 Shadow pipeline so it can run on the 360 wf14 cells.
# Copies are byte copies from the intake (read-only); no company file is edited. The FleetPy Manhattan network comes from
# the home Zenodo extraction and is checked against the cell provenance hashes; network.js / nyclib.js come from the
# 20/10 NYC-Via bundle and are checked against the company hash list (wf15/PREREG-SHADOW-HASH.txt).
# Usage: setup_tree.sh   (refuses if the target exists)
set -euo pipefail
SRC=/e/RideBoundData/company-intake/2026-10-23
ROOT=/e/RideBoundData/research/p5-via-adapter-v1
ZEN=/e/RideBoundData/wp6/extracted/sha256/d9/d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636482e2c599e/FleetPy_Manhattan
[[ -e "$ROOT" ]] && { echo "refused: $ROOT exists"; exit 3; }
SP=$ROOT/sp
mkdir -p "$SP/wf15/work" "$SP/wf15/results" "$SP/wf5/ds-nyc-via/scripts" "$SP/wf5/data/nyc-via/fleetpy-manhattan" \
  "$SP/wf6/via-many-days/work"
cp -r "$SRC/sp/wf15/runner" "$SRC/sp/wf15/scripts" "$SRC/sp/wf15/frozen" "$SP/wf15/"
cp "$SRC/sp/wf15/PREREG-SHADOW.md" "$SRC/sp/wf15/PREREG-SHADOW-HASH.txt" "$SP/wf15/"
for f in prior_prevhour.csv days_all.csv days_confirm.csv days_discovery.csv zones.csv thresholds.json CALIBRATION-RULE.md; do
  cp "$SRC/sp/wf15/work/$f" "$SP/wf15/work/"
done
cp "$SRC/sp/wf15/results/EXPLORATORY.md" "$SRC/sp/wf15/results/alpha_calibration.json" "$SP/wf15/results/"
cp "$SRC/nyc-via-bundle-20-10/nyc-via/scripts/network.js" "$SRC/nyc-via-bundle-20-10/nyc-via/scripts/nyclib.js" \
  "$SP/wf5/ds-nyc-via/scripts/"
D=$SP/wf5/data/nyc-via/fleetpy-manhattan; P=FleetPy_Manhattan__
cp "$ZEN/networks/Manhattan_2019_corrected/base/nodes.csv" "$D/${P}networks__Manhattan_2019_corrected__base__nodes.csv"
cp "$ZEN/networks/Manhattan_2019_corrected/base/edges.csv" "$D/${P}networks__Manhattan_2019_corrected__base__edges.csv"
cp "$ZEN/zones/Manhattan_Taxi_Zones/Manhattan_2019_corrected/node_zone_info.csv" \
  "$D/${P}zones__Manhattan_Taxi_Zones__Manhattan_2019_corrected__node_zone_info.csv"
cp "$ZEN/zones/Manhattan_Taxi_Zones/general_info.csv" "$D/${P}zones__Manhattan_Taxi_Zones__general_info.csv"

echo "== network vs cell provenance (gen_cells_rs.js edgesSha256 / nodesSha256)"
echo "2d5eeacbee9df83cc9bd5c40e8de0359582673a8f09eac3095054972a8332674 *$D/${P}networks__Manhattan_2019_corrected__base__edges.csv
ccc68c06bd87901ece1c0518878a891aa30cdabf661f98cb35dc20a5f26901fe *$D/${P}networks__Manhattan_2019_corrected__base__nodes.csv" | sha256sum -c
echo "== company hash list (files present in this copy)"
cd "$SP/wf15"
grep -E '^[0-9a-f]{64} \*' PREREG-SHADOW-HASH.txt | grep -v ' \*-$' | while read -r h f; do
  f=${f#\*}; if [[ -e "$f" ]]; then echo "$h *$f"; fi
done | sha256sum -c
echo "== not present in this copy (expected: s3 reference, wf13 engine, wf14 results, APSP cache before first run)"
grep -E '^[0-9a-f]{64} \*' PREREG-SHADOW-HASH.txt | grep -v ' \*-$' | while read -r h f; do f=${f#\*}; [[ -e "$f" ]] || echo "absent: $f"; done
