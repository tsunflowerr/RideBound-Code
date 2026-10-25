"""Derive the Tier-3 arm configurations and prove each differs from the base only where intended.

Same base and method as the Tier-2 generator (deadline/configs/make_configs.py):
base = benchmarks/configurations/wp14-c1-h6-reference-v1.json in the research worktree (drop-ETA
budget 30 s, decision-induced basis). Arms of T3.1 (gate family g at cap beta on the drop ETA):
  U          no research gate: drop_eta_total_ms hardLimit removed, nothing else changed
  C-<b>      conduct: decision-induced basis, drop hardLimit = b s          (C-30 == base bytes)
  V-<b>      outcome: customer-visible basis, drop hardLimit = b s
  Mplus-<b>  one-sided initial-promise deadline, slack b s, drop hardLimit removed
  M-<b>      two-sided initial-promise deadline, slack b s, drop hardLimit removed
for b in 30, 60, 120. policyId stays wp6-synthetic-policy-overlay-v1 (the preflight hard-codes it).
Writes only into this folder; refuses to overwrite a file whose content differs.
"""
import copy
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = Path(r"E:/Code/RideBound-deadline/benchmarks/configurations/wp14-c1-h6-reference-v1.json")
POLICY_ID = "wp6-synthetic-policy-overlay-v1"
CAPS = (30, 60, 120)


def leaves(value, prefix=""):
    if isinstance(value, dict):
        for key, sub in value.items():
            yield from leaves(sub, f"{prefix}.{key}")
    elif isinstance(value, list):
        if value and isinstance(value[0], dict) and "dimension" in value[0]:
            for sub in value:
                yield from leaves(sub, f"{prefix}[{sub['dimension']}]")
        elif value and isinstance(value[0], dict):
            for index, sub in enumerate(value):
                yield from leaves(sub, f"{prefix}[{index}]")
        else:
            yield prefix, json.dumps(value, sort_keys=True)
    else:
        yield prefix, json.dumps(value)


def diff(a, b):
    la, lb = dict(leaves(a)), dict(leaves(b))
    return sorted((k, la.get(k, "<absent>"), lb.get(k, "<absent>"))
                  for k in set(la) | set(lb) if la.get(k) != lb.get(k))


def write(name, data=None, raw=None):
    target = HERE / f"{name}.json"
    content = raw if raw is not None else (
        json.dumps(data, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    if target.exists() and target.read_bytes() != content:
        raise SystemExit(f"{target} exists with different content; refusing to overwrite")
    target.write_bytes(content)
    return hashlib.sha256(content).hexdigest()


base_bytes = BASE.read_bytes()
base = json.loads(base_bytes)
assert [p["policyId"] for p in base["policies"]] == [POLICY_ID]


def drop_limit(policy):
    found = [limit for limit in policy["limits"] if limit["dimension"] == "drop_eta_total_ms"]
    assert len(found) == 1 and found[0]["hardLimit"] == 30000
    return found[0]


def arm(basis=None, cap_s=None, slack_s=None, two_sided=False):
    config = copy.deepcopy(base)
    policy = config["policies"][0]
    limit = drop_limit(policy)
    if cap_s is None:
        del limit["hardLimit"]
    else:
        limit["hardLimit"] = cap_s * 1000
    if basis is not None:
        policy["budgetBasis"] = basis
    if slack_s is not None:
        policy["dropEtaDeadlineSlackMs"] = slack_s * 1000
        if two_sided:
            policy["dropEtaDeadlineTwoSided"] = True
    return config


arms = {"U": arm()}
for cap in CAPS:
    arms[f"C-{cap}"] = arm(cap_s=cap)
    arms[f"V-{cap}"] = arm(basis="customerVisible", cap_s=cap)
    arms[f"Mplus-{cap}"] = arm(slack_s=cap)
    arms[f"M-{cap}"] = arm(slack_s=cap, two_sided=True)

print("base sha256:", hashlib.sha256(base_bytes).hexdigest())
for name, data in arms.items():
    if name == "C-30":
        assert data == base
        print(f"{name}: {write(name, raw=base_bytes)} (byte copy of base)")
        continue
    print(f"{name}: {write(name, data)}")
    for key, old, new in diff(base, data):
        print(f"    {name} differs at {key}: {old} -> {new}")
