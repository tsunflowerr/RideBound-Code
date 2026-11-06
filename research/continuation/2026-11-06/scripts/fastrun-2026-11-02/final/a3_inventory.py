"""A3 of KE-HOACH-MAY-NHA.md (2026-11-04): what the final runs use that lives outside the repository. Read only.

1. Every *.json under the roots (third-party FleetPy and the Python environment skipped): the listed keys at any depth,
   non-empty `stopDistances`, every `policyId` value, the value "origin-hold" anywhere.
2. Commitment configs: a Shadow policy (dropEtaShadowAllocation) must have exactly one of dropEtaBudgetSlackPermille /
   dropEtaDeadlineSlackPermille, and at 1000 (ADR-090 fix 2).
3. WP4 configs with shadowAllocation: scheduleStrategy earliest-feasible, intervalMs and binMs >= 60 000; planningForecast
   binMs >= 60 000 (ADR-090 fixes 5-7).
4. Every *.py and *.ps1 under the roots: lines with the listed symbols.
Usage: python -I -B a3_inventory.py <out.json> <root> [<root> ...]
"""
import collections
import json
import pathlib
import re
import sys

KEYS = ("fixedFreeze", "repair", "multiplePlan", "maximumRidersPerVehicle", "stopServiceDurationMs", "freezeHorizon",
        "dropEtaDeadlineTwoSided", "solverExecutionEvidenceProfile")
SYMBOLS = ("ridebound_fleetpy", "restore(", "restart", "_repository_inventory_sha256", "wp14r_", "RIDEBOUND_STATE_HASH",
           "RB_REALJOB_BUFFERED_READ")
SKIP = ("FleetPy-1.0.2", "envs", "node_modules", "__pycache__")


def walk(value, path, found):
    if isinstance(value, dict):
        for key, item in value.items():
            if key in KEYS:
                found["keys"][key].append(path + "." + key)
            if key == "stopDistances" and item not in (None, [], {}):
                found["stopDistances"].append(path + "." + key)
            if key == "policyId" and isinstance(item, str):
                found["policyId"][item] += 1
            walk(item, path + "." + key, found)
    elif isinstance(value, list):
        for i, item in enumerate(value):
            walk(item, f"{path}[{i}]", found)
    elif value == "origin-hold":
        found["originHold"].append(path)


def main() -> int:
    out, roots = pathlib.Path(sys.argv[1]), [pathlib.Path(r) for r in sys.argv[2:]]
    if out.exists():
        raise SystemExit(f"refused: {out} exists")
    report = {"roots": [str(r) for r in roots], "jsonFiles": 0, "unreadableJson": [], "keys": {k: [] for k in KEYS},
              "stopDistancesNonEmpty": [], "policyId": {}, "originHold": [], "shadowCommitmentChecks": [],
              "shadowWp4Checks": {"files": 0, "problems": []}, "symbols": {s: [] for s in SYMBOLS}}
    policy_ids = collections.Counter()
    for root in roots:
        for path in sorted(root.rglob("*")):
            if not path.is_file() or any(part in SKIP for part in path.parts):
                continue
            if path.suffix == ".json":
                report["jsonFiles"] += 1
                try:
                    value = json.loads(path.read_text(encoding="utf-8"))
                except (ValueError, UnicodeDecodeError) as exc:
                    report["unreadableJson"].append(f"{path}: {type(exc).__name__}")
                    continue
                found = {"keys": collections.defaultdict(list), "stopDistances": [], "policyId": collections.Counter(),
                         "originHold": []}
                walk(value, "$", found)
                for key, where in found["keys"].items():
                    report["keys"][key] += [f"{path}: {w}" for w in where[:3]]
                report["stopDistancesNonEmpty"] += [f"{path}: {w}" for w in found["stopDistances"]]
                report["originHold"] += [f"{path}: {w}" for w in found["originHold"]]
                policy_ids.update(found["policyId"])
                if isinstance(value, dict) and isinstance(value.get("policies"), list):
                    for policy in value["policies"]:
                        if isinstance(policy, dict) and policy.get("dropEtaShadowAllocation"):
                            dims = {k: policy[k] for k in ("dropEtaBudgetSlackPermille", "dropEtaDeadlineSlackPermille")
                                    if k in policy}
                            report["shadowCommitmentChecks"].append(
                                {"file": str(path), "slackKeys": dims,
                                 "ok": len(dims) == 1 and list(dims.values())[0] == 1000})
                if isinstance(value, dict) and "shadowAllocation" in value:
                    report["shadowWp4Checks"]["files"] += 1
                    shadow = value["shadowAllocation"]
                    strategy = (value.get("candidateGeneration") or {}).get("scheduleStrategy")
                    problems = []
                    if strategy != "earliest-feasible":
                        problems.append(f"scheduleStrategy {strategy}")
                    for key in ("intervalMs", "binMs"):
                        if not isinstance(shadow.get(key), int) or shadow[key] < 60_000:
                            problems.append(f"shadowAllocation.{key} {shadow.get(key)}")
                    forecast = value.get("planningForecast")
                    if forecast is not None and (not isinstance(forecast.get("binMs"), int) or forecast["binMs"] < 60_000):
                        problems.append(f"planningForecast.binMs {forecast.get('binMs')}")
                    if problems:
                        report["shadowWp4Checks"]["problems"].append({"file": str(path), "problems": problems})
            elif path.suffix in (".py", ".ps1"):
                try:
                    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
                except OSError:
                    continue
                for number, line in enumerate(lines, 1):
                    for symbol in SYMBOLS:
                        if symbol in line:
                            report["symbols"][symbol].append(f"{path}:{number}")
    report["policyId"] = dict(policy_ids.most_common())
    out.write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({"jsonFiles": report["jsonFiles"], "unreadableJson": len(report["unreadableJson"]),
                      "keys": {k: len(v) for k, v in report["keys"].items()},
                      "stopDistancesNonEmpty": len(report["stopDistancesNonEmpty"]),
                      "originHold": len(report["originHold"]), "policyId": report["policyId"],
                      "shadowCommitmentChecks": report["shadowCommitmentChecks"],
                      "shadowWp4": {"files": report["shadowWp4Checks"]["files"],
                                    "problems": len(report["shadowWp4Checks"]["problems"])},
                      "symbols": {s: len(v) for s, v in report["symbols"].items()}}, indent=1, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
