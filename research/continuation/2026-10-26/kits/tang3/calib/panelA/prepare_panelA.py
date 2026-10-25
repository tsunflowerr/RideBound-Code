"""T3.8/T3.9 preparation: the 49 panel A episodes (days 14-18/11 x samples r1..r4 x windows 07, 08, 17, minus the 11
cells the normalizer could not build under the fixed retry rule of normalize_panelA.py), read-only on the repositories.

- 08:00-10:00: the 20 frozen panel A fixtures (wp9-confirmatory-fixed-panel-v2) and their drivers, used unchanged.
- 07:00-09:00 and 17:00-19:00: the 29 cells written by the normalizer into C:/Code/RideBound-noworse for grid
  tier3-panel-a-extra-v1 (normalize-per-cell-2026-10-01.log; 11 dropped cells listed in DROPPED, each driver = the 08:00-10:00 driver of the same (day, sample) with only scenarioId,
  sourceScenarioContentSha256 and sourceScenarioHash changed). A dropped cell must have no fixture folder; every
  other cell must have one.
Checks per cell: 108 requests, 8 vehicles, 96 nodes, 9120 arcs, 108 selected rows, normalizerSourceSha256 34517a83...,
scenarioId, local day and window start (the v1.2 guard's conversion), report scenarioContentSha256 = file SHA-256, and
driver hash = fixture hash. Copies fixtures to FIXTURE_ROOT and drivers to ./drivers (never overwrite). Reports the
selected source rows shared by windows of the same (day, sample) (same demand file).
Usage: python -B prepare_panelA.py <new-log>
"""
from __future__ import annotations

import hashlib
import json
import pathlib
import shutil
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parents[1] / "world"))
import preflight_world_v1_2 as v12  # noqa: E402

REPO = pathlib.Path(r"C:/Code/RideBound-noworse")
FIXTURES = REPO / "benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1"
W08_FIXTURES = FIXTURES / "wp9-confirmatory-fixed-panel-v2"
W08_DRIVERS = REPO / "benchmarks/scenarios/wp9-confirmatory"
GRID = json.loads((HERE / "grid-panel-a-extra-v1.json").read_text(encoding="utf-8"))
NEW_FIXTURES = FIXTURES / GRID["gridId"]
FIXTURE_ROOT = pathlib.Path(r"C:/RideBoundData/research/fixtures-panelA-v1")
DRIVER_ROOT = HERE / "drivers"
NORMALIZER_SOURCE = "34517a832014c78a572282e01795fc243896db088249fd6dade70beed2aa4d90"
DAYS = ("14", "15", "16", "17", "18")
WINDOWS = {"w07": 25200, "w08": 28800, "w17": 61200}
DROPPED = frozenset(  # normalize-per-cell-2026-10-01.log: node cap reached with the base label and -retry1..3
    [f"d20181115-s10-r{r}-w17" for r in (1, 2, 3, 4)]
    + [f"d20181117-s10-r{r}-w07" for r in (1, 2, 4)]
    + [f"d20181118-s10-r{r}-w07" for r in (1, 2, 3, 4)])


def cells():
    for day in DAYS:
        for r in (1, 2, 3, 4):
            for window in WINDOWS:
                if f"d201811{day}-s10-r{r}-{window}" not in DROPPED:
                    yield f"d201811{day}-s10-r{r}", window


def selected(frame: pathlib.Path) -> set[int]:
    return {row["sourceRecordOrdinal"] for row in json.loads(frame.read_text(encoding="utf-8")) if row["disposition"] == "selected"}


def main(log_path: str) -> int:
    lines, problems = [], []
    grid = {c["cellId"]: c for c in GRID["cells"]}
    DRIVER_ROOT.mkdir(exist_ok=True)
    for cell_id in sorted(DROPPED):
        if (NEW_FIXTURES / cell_id).exists():
            problems.append(f"{cell_id}: listed as dropped but its fixture folder exists")
    built = sorted(path.name for path in NEW_FIXTURES.iterdir() if path.is_dir())
    expected_built = sorted(f"{b}-{w}" for b, w in cells() if w != "w08")
    if built != expected_built:
        problems.append(f"fixture folders {built} differ from the expected {expected_built}")
    rows_by_base = {}
    for base, window in cells():
        cell_id = f"{base}-{window}"
        source = W08_FIXTURES / base if window == "w08" else NEW_FIXTURES / cell_id
        scenario_path = source / "scenario-content.json"
        scenario = json.loads(scenario_path.read_text(encoding="utf-8"))
        report = json.loads((source / "normalization-report.json").read_text(encoding="utf-8"))
        summary = scenario["validationSummary"]
        day, start = v12.scenario_window(scenario_path)
        content_sha = hashlib.sha256(scenario_path.read_bytes()).hexdigest()
        template_path = W08_DRIVERS / f"{base}.driver.json"
        template = json.loads(template_path.read_text(encoding="utf-8"))
        expected_id = template["scenarioId"] if window == "w08" else grid[cell_id]["scenarioId"]
        facts = {"requests": summary["requestCount"], "vehicles": summary["vehicleCount"], "nodes": summary["nodeCount"],
                 "arcs": summary["directedArcCount"], "selected": report["selectedRecordCount"],
                 "normalizerSource": scenario["normalizerSourceSha256"], "scenarioId": scenario["scenarioId"],
                 "day": day, "start": start}
        expected = {"requests": 108, "vehicles": 8, "nodes": 96, "arcs": 9120, "selected": 108,
                    "normalizerSource": NORMALIZER_SOURCE, "scenarioId": expected_id,
                    "day": f"2018-11-{base[7:9]}", "start": WINDOWS[window]}
        for key, value in expected.items():
            if facts[key] != value:
                problems.append(f"{cell_id}: {key} = {facts[key]!r}, expected {value!r}")
        if report["scenarioContentSha256"] != content_sha:
            problems.append(f"{cell_id}: report scenarioContentSha256 differs from the file")
        if window == "w08":
            driver = template
            if driver["sourceScenarioContentSha256"] != content_sha:
                problems.append(f"{cell_id}: frozen driver hash differs from the frozen fixture")
        else:
            driver = dict(template, scenarioId=expected_id, sourceScenarioContentSha256=content_sha,
                          sourceScenarioHash=report["scenarioHash"])
            changed = sorted(k for k in driver if driver[k] != template[k])
            if changed != ["scenarioId", "sourceScenarioContentSha256", "sourceScenarioHash"]:
                problems.append(f"{cell_id}: driver changes {changed}")
        target = FIXTURE_ROOT / cell_id
        if target.exists():
            problems.append(f"{cell_id}: fixture copy exists at {target}; not overwritten")
        else:
            shutil.copytree(source, target)
            for path in sorted(source.iterdir()):
                if path.read_bytes() != (target / path.name).read_bytes():
                    problems.append(f"{cell_id}: copy of {path.name} differs")
        with open(DRIVER_ROOT / f"{cell_id}.driver.json", "x", encoding="utf-8", newline="\n") as handle:
            if window == "w08":
                handle.write(template_path.read_text(encoding="utf-8"))
            else:
                json.dump(driver, handle, indent=2)
                handle.write("\n")
        rows_by_base.setdefault(base, {})[window] = selected(source / "selection-frame.json")
        lines.append(f"{cell_id}: {facts} scenarioContentSha256={content_sha}")
    for base, rows in sorted(rows_by_base.items()):
        pairs = [(a, b) for a, b in (("w07", "w08"), ("w17", "w08"), ("w07", "w17")) if a in rows and b in rows]
        lines.append(f"overlap {base}: " + ", ".join(f"{a}&{b} {len(rows[a] & rows[b])}" for a, b in pairs)
                     + " of 108 selected source rows")
    lines.append(f"episodes: {sum(len(rows) for rows in rows_by_base.values())}")
    lines.append(f"problems: {len(problems)}")
    lines.extend("  " + p for p in problems)
    text = "\n".join(lines) + "\n"
    with open(log_path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 1 if problems else 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1]))
