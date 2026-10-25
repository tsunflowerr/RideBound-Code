"""WP4 v2 for the no-worse runs (SWEEP-AMENDMENTS.md A1): wp4-tier3-v1.json with exactly one value changed,
`commitmentRecovery: forced-reference-v1` -> `no-worse-than-reference-v1`. Byte-level replace of that one line,
so formatting is identical; the script proves that the parsed JSON differs in that key only.
Writes wp4-tier3-v2.json with mode "x". Usage: python -B make_wp4_v2.py
"""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
source = (HERE / "wp4-tier3-v1.json").read_bytes()
old = b'"commitmentRecovery": "forced-reference-v1"'
new = b'"commitmentRecovery": "no-worse-than-reference-v1"'
assert source.count(old) == 1, "expected exactly one commitmentRecovery line"
target = source.replace(old, new)
a, b = json.loads(source), json.loads(target)
changed = sorted(k for k in set(a) | set(b) if a.get(k) != b.get(k))
assert changed == ["commitmentRecovery"], changed
with open(HERE / "wp4-tier3-v2.json", "xb") as handle:
    handle.write(target)
print("wp4-tier3-v1.json", hashlib.sha256(source).hexdigest())
print("wp4-tier3-v2.json", hashlib.sha256(target).hexdigest())
print("differs only at:", changed, a["commitmentRecovery"], "->", b["commitmentRecovery"])
