"""G2 moves counted by plan membership, from a job transcript.

After every Runner decision, the vehicle whose route (frozen prefix + mutable suffix) holds a request's pickup stop is
that request's owner. A move is a request whose owner exists before and after a decision and differs. This is the count
that AMENDMENTS A7/A9 call "plan membership"; the ledger-based g2_moves.py misses a rider confirmed and moved in the same
decision. Frames are read with g2_moves.frames (each frame's SHA-256 checked).

Self-check: --check-v4 compares the 33 v4 G2 jobs with the sealed emulator counts (A9 reports 33/33, 142 moves).
Usage: python -B moves_plan.py <job folder> [...]        one JSON line per job
       python -B moves_plan.py --check-v4
"""
import csv
import json
import pathlib
import sys

BRIDGE = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
sys.path.insert(0, str(BRIDGE))
import g2_moves  # noqa: E402


def count(folder: pathlib.Path) -> dict:
    routes = {}                      # vehicleId -> route (last vehiclePlanUpdated)
    owner = {}                       # requestId -> vehicleId holding its pickup
    moves, per_decision_max, decisions = [], 0, 0
    for direction, envelope in g2_moves.frames(folder / "transcript-00.ndjson"):
        if direction != "runnerToAdapter" or envelope.get("messageType") != "decision":
            continue
        decisions += 1
        for action in envelope["payload"]["actions"]:
            if action["decisionType"] == "vehiclePlanUpdated":
                routes[action["payload"]["vehicleId"]] = action["payload"]["route"]
        after = {}
        for vehicle, route in routes.items():
            for stop in route["frozenPrefix"] + route["mutableSuffix"]:
                if stop["kind"] == "pickup":
                    if stop["requestId"] in after:
                        raise SystemExit(f"{folder.name}: pickup of {stop['requestId']} on two vehicles")
                    after[stop["requestId"]] = vehicle
        here = [(rq, owner[rq], vehicle, envelope["simTimeMs"]) for rq, vehicle in after.items()
                if rq in owner and owner[rq] != vehicle]
        moves.extend(here)
        per_decision_max = max(per_decision_max, len(here))
        owner = after
    return {"job": folder.name, "moves": len(moves), "maxMovesPerDecision": per_decision_max,
            "decisions": decisions, "firstMoves": moves[:3]}


def check_v4() -> int:
    emulator = {}
    with open(BRIDGE / "emulator-riders-g2-2000-counts.tsv", encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            emulator[f"{row['role']}-{row['cell']}"] = int(row["relocations"])
    root = pathlib.Path("C:/RideBoundData/research/via-bridge-man-v1/g2v4/g2all")
    equal = total = 0
    for folder in sorted(root.iterdir()):
        result = count(folder)
        expected = emulator[folder.name]
        equal += result["moves"] == expected
        total += result["moves"]
        if result["moves"] != expected or result["maxMovesPerDecision"] > 1:
            print("MISMATCH", folder.name, result["moves"], expected, result["maxMovesPerDecision"])
    print(f"v4 g2all: {equal}/{len(emulator)} jobs equal to the emulator; {total} moves")
    return 0 if equal == len(emulator) else 1


if __name__ == "__main__":
    if sys.argv[1:] == ["--check-v4"]:
        sys.exit(check_v4())
    for name in sys.argv[1:]:
        print(json.dumps(count(pathlib.Path(name))))
