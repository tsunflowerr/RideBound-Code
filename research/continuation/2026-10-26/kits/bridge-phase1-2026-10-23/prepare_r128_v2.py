"""Step 1.4 drivers v2: the v1 drivers (drivers-r128/, sealed) with the declared cardinality of the 128-request cells.

The first pilot (r128pilot, 4 jobs) failed in the preflight before any simulation: "source cardinality drift: driver
declared requestCount 108, vehicleCount 8; scenario has 128, 10" (actual_fleetpy_medium_preflight.py:812-823). The
prepare_bridge.py rule (replace only the two source hashes) assumed cells of the bridge size. v2 also sets
expectedRequestCount and expectedVehicleCount, each checked against the cell's own normalization report; node and arc
counts (96, 9120) are unchanged and checked too. Writes drivers-r128-v2/ and drivers-r128-v2.sha256.txt; never overwrites.
"""
import hashlib
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
FIXTURES = pathlib.Path(r"C:/RideBoundData/research/fixtures-via-bridge-r128-v1")
OLD, NEW = HERE / "drivers-r128", HERE / "drivers-r128-v2"


def main() -> int:
    NEW.mkdir(exist_ok=True)              # a first run of this script stopped after creating the folder, empty
    if any(NEW.iterdir()):
        print(f"refused: {NEW} is not empty", file=sys.stderr)
        return 1
    lines = []
    for source in sorted(OLD.glob("*.driver.json")):
        cell = source.name.removesuffix(".driver.json")
        driver = json.loads(source.read_text(encoding="utf-8"))
        # the counts the preflight checks (actual_fleetpy_medium_preflight.py:818) come from the scenario itself
        scenario = json.loads((FIXTURES / cell / "scenario-content.json").read_text(encoding="utf-8"))
        counts = scenario["validationSummary"]
        want = {"expectedRequestCount": counts["requestCount"], "expectedVehicleCount": counts["vehicleCount"]}
        if (counts["requestCount"], counts["vehicleCount"], counts["nodeCount"], counts["directedArcCount"]) != (128, 10, 96, 9120):
            print(f"{cell}: unexpected cell size {counts}", file=sys.stderr)
            return 1
        if (driver["expectedNodeCount"], driver["expectedDirectedArcCount"]) != (96, 9120):
            print(f"{cell}: unexpected node/arc declaration", file=sys.stderr)
            return 1
        updated = dict(driver, **want)
        changed = sorted(k for k in updated if updated[k] != driver[k])
        if changed != ["expectedRequestCount", "expectedVehicleCount"]:
            print(f"{cell}: unexpected changes {changed}", file=sys.stderr)
            return 1
        target = NEW / source.name
        with open(target, "x", encoding="utf-8", newline="\n") as handle:
            handle.write(json.dumps(updated, indent=2) + "\n")
        lines.append(f"{hashlib.sha256(target.read_bytes()).hexdigest()} *{source.name}")
        print(f"{cell}: expectedRequestCount 128, expectedVehicleCount 10")
    with open(HERE / "drivers-r128-v2.sha256.txt", "x", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
