"""Step 1.4 grid, second attempt: the largest bridge cells the normalizer contract v1 allows.

The first attempt (grid-bridge-big216.json: 216 requests, 16 vehicles, 192 nodes) was refused by the normalizer:
"normalizer.configuration-invalid" (normalize-big216.log), because contract v1 bounds a cell at RequestTarget <= 128,
VehicleCount <= 32, MaximumNodeCount <= 96 (src/RideBound.Benchmarking/Normalization/FleetPyManhattanNormalizer.cs:322-328).
The company plan (Ke-Hoach-May-Nha 2.3) says: record the limit and the largest size that runs, and do not change the
core to pass it.

So: every field copied from via-bridge-2026-10-22/grid-bridge-2000.json, except requestTarget 128 (the bound),
maximumNodeCount 96 (the bound, unchanged) and vehicleCount 10:
- 128/10 = 12.8 requests per vehicle, close to the bridge's 108/8 = 13.5;
- 10 vehicles leave 9 other vehicles per donor, so the company's G2 setting of 8 nearest receivers binds.
Writes grid-bridge-r128.json next to this file (refuses to overwrite).
"""
import json
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/grid-bridge-2000.json")
grid = json.loads(SOURCE.read_text(encoding="utf-8"))
if grid["gridId"] != "via-bridge-man-2000-v1" or len(grid["cells"]) != 11:
    raise SystemExit("unexpected source grid")
grid["gridId"] = "via-bridge-man-2000-r128-v1"
for cell in grid["cells"]:
    if (cell["requestTarget"], cell["vehicleCount"], cell["maximumNodeCount"]) != (108, 8, 96):
        raise SystemExit(f"{cell['cellId']}: unexpected size")
    if cell["maximumRideTimePermille"] != 2000:
        raise SystemExit(f"{cell['cellId']}: not 2.0x")
    cell.update(requestTarget=128, vehicleCount=10)
with open(HERE / "grid-bridge-r128.json", "x", encoding="utf-8", newline="\n") as handle:
    json.dump(grid, handle, indent=2)
    handle.write("\n")
print("wrote grid-bridge-r128.json:", len(grid["cells"]), "cells")
