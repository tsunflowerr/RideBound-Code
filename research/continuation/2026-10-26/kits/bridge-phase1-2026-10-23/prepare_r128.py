"""Copy the 128-request bridge fixtures out of the worktree and derive their drivers (step 1.4).

Same rule as via-bridge-2026-10-22/prepare_bridge.py: driver = the bridge driver of the same cell with ONLY
sourceScenarioContentSha256 and sourceScenarioHash replaced (scenarioId unchanged, as the grid keeps it).
The W5 worlds of the bridge are reused unchanged (they depend on the day and window only).
Never overwrites: an existing destination must already be byte-identical. Writes sha256 manifests.
"""
import hashlib
import json
import os
import pathlib
import shutil
import sys

HERE = pathlib.Path(__file__).resolve().parent
NEW = pathlib.Path(r"C:/Code/RideBound-via-ref/benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/via-bridge-man-2000-r128-v1")
BRIDGE_DRIVERS = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/drivers")
OUT_FIXTURES = pathlib.Path(r"C:/RideBoundData/research/fixtures-via-bridge-r128-v1")
OUT_DRIVERS = HERE / "drivers-r128"


def sha(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    cells = sorted(p.name for p in NEW.iterdir())
    if len(cells) != 10:
        print(f"expected 10 cells, found {len(cells)}", file=sys.stderr)
        return 1
    OUT_FIXTURES.mkdir(parents=True, exist_ok=True)
    OUT_DRIVERS.mkdir(exist_ok=True)
    fixture_lines, driver_lines = [], []
    for cell in cells:
        src, dst = NEW / cell, OUT_FIXTURES / cell
        dst.mkdir(exist_ok=True)
        for item in sorted(src.iterdir()):
            target = dst / item.name
            if target.exists():
                if sha(target) != sha(item):
                    print(f"refused: {target} exists and differs", file=sys.stderr)
                    return 1
            else:
                shutil.copyfile(item, target)
            fixture_lines.append(f"{sha(target)} *{cell}/{item.name}")
        template = json.loads((BRIDGE_DRIVERS / f"{cell}.driver.json").read_text(encoding="utf-8"))
        content_sha = sha(dst / "scenario-content.json")
        report = json.loads((dst / "normalization-report.json").read_text(encoding="utf-8"))
        if report["scenarioContentSha256"] != content_sha:
            print(f"{cell}: report scenarioContentSha256 differs from the file", file=sys.stderr)
            return 1
        driver = dict(template, sourceScenarioContentSha256=content_sha, sourceScenarioHash=report["scenarioHash"])
        changed = sorted(k for k in driver if driver[k] != template[k])
        if changed != ["sourceScenarioContentSha256", "sourceScenarioHash"]:
            print(f"{cell}: unexpected driver changes {changed}", file=sys.stderr)
            return 1
        text = json.dumps(driver, indent=2) + "\n"
        out = OUT_DRIVERS / f"{cell}.driver.json"
        if out.exists():
            if out.read_text(encoding="utf-8") != text:
                print(f"refused: {out} exists and differs", file=sys.stderr)
                return 1
        else:
            with open(out, "x", encoding="utf-8", newline="\n") as handle:
                handle.write(text)
        driver_lines.append(f"{sha(out)} *{cell}.driver.json")
        print(f"{cell} content {content_sha[:12]} scenarioHash {report['scenarioHash'][:12]}")
    for name, lines in (("fixtures-via-bridge-r128-v1.sha256.txt", fixture_lines), ("drivers-r128.sha256.txt", driver_lines)):
        with open(HERE / name, "x", encoding="utf-8", newline="\n") as handle:
            handle.write("\n".join(lines) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
