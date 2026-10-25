"""Read-only: show the structure of the final Runner checkpoint in a transcript (keys, entry kinds,
and one revision entry), to see whether signed v/e/p can be rebuilt from it."""
from __future__ import annotations

import base64
import collections
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
final = None
for line in path.read_text(encoding="utf-8").splitlines():
    record = json.loads(line)
    envelope = json.loads(base64.b64decode(record["frameBase64"]))
    if record["direction"] == "runnerToAdapter" and envelope.get("messageType") == "checkpoint":
        final = envelope["payload"]


def keys(value, depth=0, limit=3):
    if isinstance(value, dict) and depth < limit:
        return {k: keys(v, depth + 1, limit) for k, v in value.items()}
    if isinstance(value, list):
        return [f"list[{len(value)}]", keys(value[0], depth + 1, limit) if value else None]
    return type(value).__name__


print(json.dumps(keys(final, limit=4), indent=1)[:3000])
state = final
for part in ("content", "onlineState"):
    if isinstance(state, dict) and part in state:
        state = state[part]
ledger = state["commitmentLedger"]
histories = ledger["histories"] if isinstance(ledger, dict) else ledger
entries = [entry for history in histories for entry in history["entries"]]
print("histories", len(histories), "entries", len(entries),
      "kinds", collections.Counter(entry.get("kind") for entry in entries))
revision = next(entry for entry in entries if entry.get("kind") != "initialPromise")
print(json.dumps(revision, indent=1)[:2500])
