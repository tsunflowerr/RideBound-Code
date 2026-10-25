"""Build the Manhattan bridge grids (11 cells) from the exact source grids.

Cells: w08 r1 on 12-18 Nov 2018 (7) + w17 r1 on 12/13/14/18 Nov (4) = the
company plan's MAN-RB bridge (WOR08 7 days + IMP17 4 days, sample r1).
Every field is copied unchanged from the grid that produced the archived
fixture; only maximumRideTimePermille changes (scenarioId kept, so the
vehicle start nodes and the request sample stay identical).

Writes:
  grid-bridge-check-1500.json  - permille 1500, must reproduce the archived
                                 fixtures byte for byte (check of this script)
  grid-bridge-2000.json        - permille 2000, the bridge
"""

import json
import os
import sys

REPO = r"C:\Code\RideBound-via"
SCRATCH = r"E:\Code\Report_INT3508\ridebound-scratchpad"
HERE = os.path.dirname(os.path.abspath(__file__))

SOURCES = [
    (os.path.join(REPO, r"benchmarks\scenarios\wp14-development\grid-v1.json"),
     ["d20181112-s10-r1-w08", "d20181113-s10-r1-w08",
      "d20181112-s10-r1-w17", "d20181113-s10-r1-w17"]),
    (os.path.join(REPO, r"benchmarks\scenarios\wp9-confirmatory\grid-v2.json"),
     ["d20181114-s10-r1", "d20181115-s10-r1", "d20181116-s10-r1",
      "d20181117-s10-r1", "d20181118-s10-r1"]),
    (os.path.join(SCRATCH, r"tang3\calib\panelA\grid-panel-a-extra-v1.json"),
     ["d20181114-s10-r1-w17", "d20181118-s10-r1-w17"]),
]


def bridge_id(cell_id: str, start: int) -> str:
    """Uniform bridge cell id: dYYYYMMDD-s10-r1-wHH."""
    if cell_id.endswith(("-w08", "-w17")):
        return cell_id
    return f"{cell_id}-w{start // 3600:02d}"


def main() -> int:
    cells = []
    for path, wanted in SOURCES:
        with open(path, encoding="utf-8") as handle:
            grid = json.load(handle)
        by_id = {cell["cellId"]: cell for cell in grid["cells"]}
        for cell_id in wanted:
            if cell_id not in by_id:
                print(f"missing {cell_id} in {path}", file=sys.stderr)
                return 1
            cell = dict(by_id[cell_id])
            cell["_source"] = os.path.basename(os.path.dirname(path)) + "/" + os.path.basename(path)
            cells.append(cell)
    if len(cells) != 11:
        print(f"expected 11 cells, got {len(cells)}", file=sys.stderr)
        return 1

    for permille, grid_id, name in [
        (1500, "via-bridge-check-1500-v1", "grid-bridge-check-1500.json"),
        (2000, "via-bridge-man-2000-v1", "grid-bridge-2000.json"),
    ]:
        out_cells = []
        for cell in cells:
            copy = {k: v for k, v in cell.items() if k != "_source"}
            if copy["maximumRideTimePermille"] != 1500:
                print(f"unexpected source permille in {copy['cellId']}", file=sys.stderr)
                return 1
            copy["cellId"] = bridge_id(cell["cellId"], cell["sourceWindowStartSeconds"])
            copy["maximumRideTimePermille"] = permille
            out_cells.append(copy)
        out_cells.sort(key=lambda c: c["cellId"])
        with open(os.path.join(HERE, name), "w", encoding="utf-8", newline="\n") as handle:
            json.dump({"gridId": grid_id, "cells": out_cells}, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
        print(name, [c["cellId"] + " <- " + c["scenarioId"] for c in out_cells])

    with open(os.path.join(HERE, "grid-sources.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump([{"cellId": bridge_id(c["cellId"], c["sourceWindowStartSeconds"]),
                    "source": c["_source"], "sourceCellId": c["cellId"],
                    "scenarioId": c["scenarioId"]} for c in cells], handle, indent=2)
        handle.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
