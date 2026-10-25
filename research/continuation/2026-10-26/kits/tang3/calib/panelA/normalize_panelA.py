"""Generate the 40 extra panel A cells one at a time (the normalizer stops the whole grid at the first failing cell).

Rule, fixed before any simulation on these cells: a cell whose HMAC-ranked selection cannot reach 108 requests within
the 96-node cap ("source.node-cap-prevents-target") is retried with the selection and pseudonymization labels suffixed
"-retry1", then "-retry2", then "-retry3", in that order; if all fail, the cell is dropped and reported. No other
failure is retried. The label only changes which source rows are sampled; no outcome of any run is involved.
Each attempt uses a one-cell grid manifest (same gridId, so fixtures land in the same folder) written to
./single-grids/ with exclusive create; a cell whose fixture folder already exists is skipped.
Usage: python -B normalize_panelA.py <new-log>
"""
from __future__ import annotations

import json
import pathlib
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
REPO = pathlib.Path(r"C:/Code/RideBound-noworse")
GRID = json.loads((HERE / "grid-panel-a-extra-v1.json").read_text(encoding="utf-8"))
OUT = REPO / "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1" / GRID["gridId"]
SINGLE = HERE / "single-grids"
COMMAND = ["dotnet", "run", "--project", "tools/RideBound.Wp6Normalize", "-c", "Release", "--", "normalize",
           "--cache", "E:/RideBoundData/wp6", "--repository", str(REPO), "--accept-license", "CC-BY-4.0", "--grid"]


def attempt(cell: dict, suffix: str, log) -> str:
    variant = dict(cell)
    if suffix:
        variant["selectionLabel"] = cell["selectionLabel"] + suffix
        variant["pseudonymizationLabel"] = cell["pseudonymizationLabel"] + suffix
    path = SINGLE / f"{cell['cellId']}{suffix or ''}.json"
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        json.dump({"gridId": GRID["gridId"], "cells": [variant]}, handle, indent=2)
        handle.write("\n")
    completed = subprocess.run(COMMAND + [str(path)], cwd=str(REPO), capture_output=True, text=True, encoding="utf-8",
                               errors="replace", timeout=900)
    text = (completed.stdout + completed.stderr).strip()
    log.write(f"== {cell['cellId']} label{suffix or ' (base)'} rc={completed.returncode}\n{text[-1500:]}\n")
    log.flush()
    if completed.returncode == 0 and '"requestCount":108' in text:
        return "ok"
    if "source.node-cap-prevents-target" in text:
        return "node-cap"
    return f"failed rc={completed.returncode}"


def main(log_path: str) -> int:
    SINGLE.mkdir(exist_ok=True)
    summary = []
    with open(log_path, "x", encoding="utf-8", newline="\n") as log:
        for cell in GRID["cells"]:
            if (OUT / cell["cellId"]).exists():
                summary.append(f"{cell['cellId']}: fixture exists (base label), skipped")
                continue
            outcome, used = None, None
            for suffix in ("", "-retry1", "-retry2", "-retry3"):
                outcome = attempt(cell, suffix, log)
                used = suffix or "base"
                if outcome != "node-cap":
                    break
            summary.append(f"{cell['cellId']}: {outcome} with label {used}")
            print(summary[-1], flush=True)
        log.write("== summary\n" + "\n".join(summary) + "\n")
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1]))
