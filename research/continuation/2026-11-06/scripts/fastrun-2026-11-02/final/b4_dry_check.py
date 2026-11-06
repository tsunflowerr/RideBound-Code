"""B4 of KE-HOACH-MAY-NHA.md (2026-11-04): check all 168 final jobs WITHOUT running them. Read only.

Per job of jobs-bundle-v2/jobs-final.json:
  1 files: commitment config, WP4 config, run driver, world, and the fixture files exist;
  2 hashes: each of them has the SHA-256 that MANIFEST.tsv of the bundle lists (so the rented machine gets these bytes);
  3 Runner: the bundle's Runner (final-v2/dotnet + final-v2/runner) loads the (commitment, WP4) pair and exits 0 on an
    empty input; a configuration error exits 64 (checked on a broken copy before this run);
Per cell:
  4 networkNodes: the map has one entry per fixture node; its keys are the adapter's own node ids
    (FleetPyProtocolMapper().node_id(i), i = local node index) and node i maps to the network index written in the
    fixture's i-th node id (node-%06d, fixture nodes sorted as the emulator's loadCell sorts them);
  5 Shadow inputs: kappa, factors, threshold, alpha, phi, sampled, priors, interval and pickup window equal the sealed
    shadow inputs (predictions/shadow-inputs/<cell>.json); the CL arm's forecast profile equals the sealed profile.
Usage: python -I -B b4_dry_check.py <out.json>
"""
import csv
import hashlib
import json
import pathlib
import subprocess
import sys

E = pathlib.Path("E:/")
DATA = E / "RideBoundData/research/fullsys-f14-v2"
FINAL = E / "RideBoundData/final-v2"
MANIFEST = E / "RideBoundData/final-v2-pack/MANIFEST.tsv"
JOBS = DATA / "jobs-bundle-v2/jobs-final.json"
sys.path.insert(0, str(FINAL / "repo/simulators/fleetpy-ridebound"))
from ridebound_fleetpy.mapping import FleetPyProtocolMapper  # noqa: E402

FIXTURE_FILES = ("scenario-content.json", "derivative-manifest.json", "normalization-report.json",
                 "selection-frame.json", "normalizer-configuration.json")


def sha(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    out = pathlib.Path(sys.argv[1])
    if out.exists():
        raise SystemExit(f"refused: {out} exists")
    manifest = {r["path"]: r["sha256"] for r in csv.DictReader(open(MANIFEST, encoding="utf-8", newline=""),
                                                              delimiter="\t")}
    jobs = json.loads(JOBS.read_text(encoding="utf-8"))
    problems, checked_files, runner_ok, cells = [], set(), 0, {}
    names = [j["name"] for j in jobs]
    if len(jobs) != 168 or len(set(names)) != 168:
        problems.append(f"job list: {len(jobs)} jobs, {len(set(names))} unique")
    for job in jobs:
        cell = pathlib.Path(job["fixture"]).name
        cells.setdefault(cell, set()).add(job["role"])
        paths = [pathlib.Path(job[k]) for k in ("config", "wp4", "driver", "world")]
        paths += [pathlib.Path(job["fixture"]) / f for f in FIXTURE_FILES]
        for path in paths:
            if not path.is_file():
                problems.append(f"{job['name']}: missing {path}")
                continue
            if path in checked_files:
                continue
            relative = path.relative_to(E).as_posix()
            if manifest.get(relative) != sha(path):
                problems.append(f"{job['name']}: {relative} not in MANIFEST.tsv with this SHA-256")
            checked_files.add(path)
        done = subprocess.run([str(FINAL / "dotnet/dotnet.exe"), str(FINAL / "runner/RideBound.Runner.dll"),
                               "--mode", "commitment", "--policy-config", job["config"], "--wp4-config", job["wp4"],
                               "--solver-seed-source", "manifest-master-seed", "--maximum-line-bytes", str(2**30)],
                              stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        if done.returncode != 0 or done.stderr.strip() or done.stdout.strip():
            problems.append(f"{job['name']}: Runner exit {done.returncode}: {done.stderr.strip()[:300]}")
        else:
            runner_ok += 1
    for cell, roles in sorted(cells.items()):
        if roles != {"FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N"}:
            problems.append(f"{cell}: arms {sorted(roles)}")
        scenario = json.loads((DATA / "fixtures" / cell / "scenario-content.json").read_text(encoding="utf-8"))
        arcs = scenario["travelSnapshots"][0]["arcs"]
        fixture_ids = sorted({a["fromNodeId"] for a in arcs} | {a["toNodeId"] for a in arcs})
        mapper = FleetPyProtocolMapper()
        expected = {mapper.node_id(i): int(node[len("node-"):]) for i, node in enumerate(fixture_ids)}
        sealed = json.loads((DATA / "predictions/shadow-inputs" / f"{cell}.json").read_text(encoding="utf-8"))
        for role in ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N"):
            wp4 = json.loads((DATA / "job-configs-v2/wp4" / role / f"{cell}.json").read_text(encoding="utf-8"))
            shadow = wp4["shadowAllocation"]
            if shadow["networkNodes"] != expected:
                problems.append(f"{cell} {role}: networkNodes differ from the fixture nodes "
                                f"({len(shadow['networkNodes'])} vs {len(expected)})")
            pairs = {"kappa": (float(shadow["kappa"]), sealed["kappa"]),
                     "threshold": (float(shadow["threshold"]), sealed["theta"]),
                     "alpha": (float(shadow["alpha"]), sealed["alphaS"]),
                     "phi": (float(shadow["phi"]), sealed["phi"]),
                     "sampled": (shadow["sampled"], sealed["sampled"]),
                     "intervalMs": (shadow["intervalMs"], sealed["intervalMs"]),
                     "pickupWindowMs": (shadow["pickupWindowMs"], sealed["omegaPms"]),
                     "strideMs": (shadow["strideMs"], sealed["strideMs"]),
                     "factors": ([float(x) for x in shadow["factors"]], sealed["factors"]),
                     "priorOrigin": (shadow["priorOrigin"], sealed["prior"]["O"]),
                     "priorDestination": (shadow["priorDestination"], sealed["prior"]["D"])}
            for name, (have, want) in pairs.items():
                if have != want:
                    problems.append(f"{cell} {role}: {name} differs from the sealed shadow inputs")
            forecast = wp4.get("planningForecast")
            if role.endswith("-CL"):
                if forecast is None or [float(x) for x in forecast["profile"]] != sealed["forecast"]["profile"] \
                        or forecast["binMs"] != sealed["forecast"]["binMs"]:
                    problems.append(f"{cell} {role}: forecast profile differs from the sealed one")
            elif forecast is not None:
                problems.append(f"{cell} {role}: the no-forecast arm has a planningForecast")
    report = {"jobs": len(jobs), "cells": len(cells), "filesHashed": len(checked_files), "runnerLoadsOk": runner_ok,
              "problems": problems, "pass": not problems}
    out.write_text(json.dumps(report, indent=1), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "problems"}), f"problems: {len(problems)}")
    for problem in problems[:20]:
        print("  ", problem)
    return 0 if not problems else 1


if __name__ == "__main__":
    raise SystemExit(main())
