"""WP4 configurations v2 for the final comparison (ADR-089 GĐ4, 2026-11-04): v1 (make_job_configs.js) plus
shadowAllocation.networkNodes, the cell's map from run node identifier to network node index.

Why: the first real Shadow job failed ("Node 'node-825512e8…' is not a network node"): run node identifiers are the
adapter's opaque ones, node- + SHA-256 of the FleetPy node (ridebound_fleetpy/mapping.py CanonicalIdRegistry), and the
FleetPy node of a cell is its local index i (verified on d20190919-vf-r0-w08: the 850 snapshot nodes are exactly
node_id(0..849), and every sampled arc time equals D[globalOf[i]][globalOf[j]] * kappa). The identifiers are therefore
made here with the adapter's own function, FleetPyProtocolMapper().node_id(i), and mapped to globalOf[i] of the sealed
shadow inputs (emulate_fullsys.js adapter). Nothing else of v1 changes; v1 files are kept.

Writes <data>/job-configs-v2/{commitment/*.json (copies), wp4/<arm>/<cell>.json}; refuses to overwrite.
Usage: python -I -B make_job_configs_v2.py <data root> <adapter package dir (simulators/fleetpy-ridebound)>
"""
import hashlib
import json
import pathlib
import shutil
import sys

data, adapter = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
sys.path.insert(0, str(adapter))
from ridebound_fleetpy.mapping import FleetPyProtocolMapper  # noqa: E402

old, new = data / "job-configs", data / "job-configs-v2"
if new.exists():
    raise SystemExit(f"refused: {new} exists")
inputs_dir = data / "predictions" / "shadow-inputs"
SHADOW_ARMS = ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N")


def write(path: pathlib.Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(value, indent=2) + "\n")


(new / "commitment").mkdir(parents=True)
for path in sorted((old / "commitment").glob("*.json")):
    shutil.copyfile(path, new / "commitment" / path.name)
for path in sorted((old / "wp4" / "FS-G2-RCE35-CL").glob("*.json")):   # forecast gate: no Shadow, copied as is
    target = new / "wp4" / "FS-G2-RCE35-CL" / path.name
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, target)

summary = {"cells": 0, "nodes": 0}
for path in sorted(inputs_dir.glob("*.json")):
    inputs = json.loads(path.read_text(encoding="utf-8"))
    cell, global_of = inputs["cell"], inputs["globalOf"]
    mapper = FleetPyProtocolMapper()
    nodes = {mapper.node_id(i): g for i, g in enumerate(global_of)}
    if len(nodes) != len(global_of) or len(set(global_of)) != len(global_of) or not all(0 <= g < 4411 for g in global_of):
        raise SystemExit(f"{cell}: node map is not one-to-one within the 4,411-node network")
    for arm in SHADOW_ARMS:
        value = json.loads((old / "wp4" / arm / f"{cell}.json").read_text(encoding="utf-8"))
        block = value["shadowAllocation"]
        if "networkNodes" in block:
            raise SystemExit(f"{arm}/{cell}: v1 already has networkNodes")
        block["networkNodes"] = nodes
        write(new / "wp4" / arm / f"{cell}.json", value)
    summary["cells"] += 1
    summary["nodes"] += len(nodes)
summary["sha256"] = {p.relative_to(new).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                     for p in sorted(new.rglob("*.json"))}
(new / "CONFIGS-V2.json").write_text(json.dumps(summary, indent=1), encoding="utf-8")
print(f"{summary['cells']} cells, {summary['nodes']} node ids; {len(summary['sha256'])} files")
