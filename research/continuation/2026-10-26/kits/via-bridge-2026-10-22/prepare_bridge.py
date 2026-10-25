"""Copy the 2.0x bridge fixtures out of the worktree and derive their drivers.

Driver rule (same as tang3/calib/panelA/prepare_panelA.py:97-101): driver = the archived
driver of the same cell with ONLY sourceScenarioContentSha256 and sourceScenarioHash
replaced; scenarioId is unchanged here because the grid keeps it. The template must
match the 1.5x check fixture (proves the template is the right one).
Never overwrites: an existing destination must already be byte-identical.
"""

import hashlib
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
FIX = r"C:\Code\RideBound-via-ref\benchmarks\fixtures\wp6\public\fleetpy-manhattan-v1"
NEW = os.path.join(FIX, "via-bridge-man-2000-v1")
CHECK = os.path.join(FIX, "via-bridge-check-1500-v1")
OUT_FIXTURES = r"C:\RideBoundData\research\fixtures-via-bridge-2000-v1"
OUT_DRIVERS = os.path.join(HERE, "drivers")
WP14_DRIVERS = r"C:\Code\RideBound-via-ref\benchmarks\scenarios\wp14-development"
PANEL_DRIVERS = r"E:\Code\Report_INT3508\ridebound-scratchpad\tang3\calib\panelA\drivers"


def sha_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha(path: str) -> str:
    return sha_bytes(open(path, "rb").read())


def template_for(cell: str) -> str:
    if cell.startswith(("d20181112", "d20181113")):
        return os.path.join(WP14_DRIVERS, f"{cell}.driver.json")
    return os.path.join(PANEL_DRIVERS, f"{cell}.driver.json")


def main() -> int:
    os.makedirs(OUT_FIXTURES, exist_ok=True)
    os.makedirs(OUT_DRIVERS, exist_ok=True)
    cells = sorted(os.listdir(NEW))
    if len(cells) != 11:
        print(f"expected 11 cells, found {len(cells)}", file=sys.stderr)
        return 1
    for cell in cells:
        src = os.path.join(NEW, cell)
        dst = os.path.join(OUT_FIXTURES, cell)
        os.makedirs(dst, exist_ok=True)
        for name in sorted(os.listdir(src)):
            target = os.path.join(dst, name)
            if os.path.exists(target):
                if sha(target) != sha(os.path.join(src, name)):
                    print(f"refused: {target} exists and differs", file=sys.stderr)
                    return 1
            else:
                shutil.copyfile(os.path.join(src, name), target)

        template_path = template_for(cell)
        template = json.load(open(template_path, encoding="utf-8"))
        check_content = os.path.join(CHECK, cell, "scenario-content.json")
        check_report = json.load(open(os.path.join(CHECK, cell, "normalization-report.json"), encoding="utf-8"))
        if template["sourceScenarioContentSha256"] != sha(check_content) \
                or template["sourceScenarioHash"] != check_report["scenarioHash"]:
            print(f"{cell}: template driver does not match the 1.5x fixture", file=sys.stderr)
            return 1
        content_sha = sha(os.path.join(dst, "scenario-content.json"))
        report = json.load(open(os.path.join(dst, "normalization-report.json"), encoding="utf-8"))
        if report["scenarioContentSha256"] != content_sha:
            print(f"{cell}: report scenarioContentSha256 differs from the file", file=sys.stderr)
            return 1
        driver = dict(template, sourceScenarioContentSha256=content_sha, sourceScenarioHash=report["scenarioHash"])
        changed = sorted(k for k in driver if driver[k] != template[k])
        if changed != ["sourceScenarioContentSha256", "sourceScenarioHash"]:
            print(f"{cell}: unexpected driver changes {changed}", file=sys.stderr)
            return 1
        text = json.dumps(driver, indent=2) + "\n"
        out = os.path.join(OUT_DRIVERS, f"{cell}.driver.json")
        if os.path.exists(out):
            if open(out, encoding="utf-8").read() != text:
                print(f"refused: {out} exists and differs", file=sys.stderr)
                return 1
        else:
            with open(out, "x", encoding="utf-8", newline="\n") as handle:
                handle.write(text)
        print(f"{cell} content {content_sha[:12]} scenarioHash {report['scenarioHash'][:12]} "
              f"template {os.path.relpath(template_path, os.path.dirname(os.path.dirname(template_path)))}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
