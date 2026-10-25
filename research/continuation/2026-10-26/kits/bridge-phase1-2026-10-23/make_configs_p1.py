"""Phase-1 configs, each derived from a bridged config by exact text edits (each edit must match exactly once).

Strict arms (bridge step 1.1): the strict deadline of RideBound is the deadline without dropEtaDeadlineRelaxed plus the
recovery forced-reference-v1, which keeps a forced vehicle on its kept route (the emulator's forced vehicle).
  EO35        <- EOHZ35.json      minus dropEtaDeadlineRelaxed
  DLS-120     <- DLHZ-120.json    minus dropEtaDeadlineRelaxed
  G2-EO35     <- G2-EOHZ35.json   minus dropEtaDeadlineRelaxed
  G2-DLS-120  <- G2-EO35          with the fixed 120 s slack instead of the 35% share
  wp4-costfirst-forced.json     <- wp4-tier3-v2-costfirst.json  recovery forced-reference-v1
  wp4-g2-costfirst-forced.json  <- wp4-g2-costfirst.json         recovery forced-reference-v1
k-nearest receivers (bridge step 1.2):
  wp4-g2-costfirst-k3.json      <- wp4-g2-costfirst.json         waitingRiderRelocation {maximumTargetVehicles: 3}
Usage: python -B make_configs_p1.py   (writes configs/ next to this file; refuses to overwrite)
"""
import hashlib
import json
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
BRIDGE = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/configs")
WP4 = pathlib.Path("E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/wp4-tier3-v2-costfirst.json")
OUT = HERE / "configs"

RELAXED_LAST = (',\n      "dropEtaDeadlineRelaxed": true\n', "\n")      # last key of the policy object
RELAXED_MID = ('      "dropEtaDeadlineRelaxed": true,\n', "")         # followed by ratchetLocks (G2 configs)
FORCED = ('"commitmentRecovery": "no-worse-than-reference-v1"', '"commitmentRecovery": "forced-reference-v1"')
K3 = ('"waitingRiderRelocation": {}', '"waitingRiderRelocation": {\n      "maximumTargetVehicles": 3\n    }')
FIXED_120 = ('"dropEtaDeadlineSlackPermille": 350', '"dropEtaDeadlineSlackMs": 120000')

PLAN = [
    ("EO35.json", BRIDGE / "EOHZ35.json", [RELAXED_LAST]),
    ("DLS-120.json", BRIDGE / "DLHZ-120.json", [RELAXED_LAST]),
    ("G2-EO35.json", BRIDGE / "G2-EOHZ35.json", [RELAXED_MID]),
    ("G2-DLS-120.json", BRIDGE / "G2-EOHZ35.json", [RELAXED_MID, FIXED_120]),
    ("wp4-costfirst-forced.json", WP4, [FORCED]),
    ("wp4-g2-costfirst-forced.json", BRIDGE / "wp4-g2-costfirst.json", [FORCED]),
    ("wp4-g2-costfirst-k3.json", BRIDGE / "wp4-g2-costfirst.json", [K3]),
]

OUT.mkdir(exist_ok=True)
lines = []
for name, source, edits in PLAN:
    text = source.read_bytes().decode("utf-8")
    if "\r" in text:
        raise SystemExit(f"{source} has CR bytes")
    for old, new in edits:
        if text.count(old) != 1:
            raise SystemExit(f"{name}: edit {old!r} matches {text.count(old)} times in {source.name}")
        text = text.replace(old, new)
    json.loads(text)
    if "dropEtaDeadlineRelaxed" in text and name not in ("wp4-costfirst-forced.json",):
        raise SystemExit(f"{name}: still relaxed")
    target = OUT / name
    with open(target, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    digest = hashlib.sha256(target.read_bytes()).hexdigest()
    lines.append(f"{digest} *{name}")
    print(f"{name:32s} {digest[:12]} <- {source.name}")
with open(HERE / "configs-p1.sha256.txt", "x", encoding="utf-8", newline="\n") as handle:
    handle.write("\n".join(lines) + "\n")
