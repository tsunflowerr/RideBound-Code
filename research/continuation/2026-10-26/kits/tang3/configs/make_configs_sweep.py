"""Sweep arms for the C vs M⁺ comparison (T3 sweep): C-<x> and Mplus-<x> for x in 30, 60, 120, 300, 600, 900 s, plus U.

Same base and the same derivation as make_configs_t3.py (imported, not copied):
  C-<x>      decision-induced basis, drop_eta_total_ms hardLimit = x s, no deadline   (C-30 == base bytes)
  Mplus-<x>  one-sided initial-promise deadline dropEtaDeadlineSlackMs = x s, drop hardLimit removed
  U          drop hardLimit removed, nothing else
Every other field (structural limits, locks, phases, policyId) is the base's, for both families.
Existing files (30/60/120 and U, written by make_configs_t3.py) must be byte-identical to a regeneration;
new files (300/600/900) are created, never overwritten with different content. The diff of every arm against
the base is printed, and so is the diff between C-<x> and Mplus-<x>, which must be exactly the two intended keys.
Usage: python -B make_configs_sweep.py  > make_configs_sweep.log   (run once)
"""
import contextlib
import hashlib
import io
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
LEVELS = (30, 60, 120, 300, 600, 900)

# make_configs_t3 writes its 13 arms at import; with unchanged inputs that is a byte-identical no-op (it refuses
# to overwrite different content). Its printout is captured so the log shows only this script's lines.
with contextlib.redirect_stdout(io.StringIO()):
    import make_configs_t3 as t3  # noqa: E402

arms = {"U": t3.arm()}
for x in LEVELS:
    arms[f"C-{x}"] = t3.arm(cap_s=x)
    arms[f"Mplus-{x}"] = t3.arm(slack_s=x)

print("base sha256:", hashlib.sha256(t3.base_bytes).hexdigest())
problems = []
for name, data in arms.items():
    if name == "C-30":
        assert data == t3.base
        digest = t3.write(name, raw=t3.base_bytes)
    else:
        digest = t3.write(name, data)
    print(f"{name}: {digest}")
    for key, old, new in t3.diff(t3.base, data):
        print(f"    {name} differs from base at {key}: {old} -> {new}")
for x in LEVELS:
    pair = t3.diff(arms[f"C-{x}"], arms[f"Mplus-{x}"])
    keys = sorted(key for key, _, _ in pair)
    expected = [".policies[0].dropEtaDeadlineSlackMs", ".policies[0].limits[drop_eta_total_ms].hardLimit"]
    print(f"C-{x} vs Mplus-{x}: {[(k, a, b) for k, a, b in pair]}")
    if keys != expected:
        problems.append(f"C-{x} vs Mplus-{x} differ at {keys}, expected {expected}")
print("problems:", problems or "none")
sys.exit(1 if problems else 0)
