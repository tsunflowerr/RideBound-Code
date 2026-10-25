"""Step 1.4 grid: the 11 bridge cells at twice the size (216 requests, 16 vehicles, at most 192 nodes).

Every field is copied from via-bridge-2026-10-22/grid-bridge-2000.json (2.0x, the same demand sample, window, factor,
labels and scenarioId); only requestTarget, vehicleCount and maximumNodeCount are doubled, and the gridId changes.
The ratio of requests to vehicles and to nodes stays as in the bridge cells (108/8/96).
Writes grid-bridge-big216.json next to this file (refuses to overwrite).
"""
import json
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/grid-bridge-2000.json")
grid = json.loads(SOURCE.read_text(encoding="utf-8"))
if grid["gridId"] != "via-bridge-man-2000-v1" or len(grid["cells"]) != 11:
    raise SystemExit("unexpected source grid")
grid["gridId"] = "via-bridge-man-2000-big216-v1"
for cell in grid["cells"]:
    if (cell["requestTarget"], cell["vehicleCount"], cell["maximumNodeCount"]) != (108, 8, 96):
        raise SystemExit(f"{cell['cellId']}: unexpected size")
    if cell["maximumRideTimePermille"] != 2000:
        raise SystemExit(f"{cell['cellId']}: not 2.0x")
    cell.update(requestTarget=216, vehicleCount=16, maximumNodeCount=192)
with open(HERE / "grid-bridge-big216.json", "x", encoding="utf-8", newline="\n") as handle:
    json.dump(grid, handle, indent=2)
    handle.write("\n")
print("wrote grid-bridge-big216.json:", len(grid["cells"]), "cells")
