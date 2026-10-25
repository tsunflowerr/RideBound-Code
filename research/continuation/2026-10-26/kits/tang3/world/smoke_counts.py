"""Count what the Tier-3 mechanisms did in each smoke run, from the transcript (read-only):
non-normal certificates, forced-reference witnesses, and the final checkpoint's breaches and late
pickups."""
from __future__ import annotations

import base64
import collections
import json

import run_world_smoke as smoke


def walk(value, key):
    """Every value stored under `key` anywhere in a JSON tree."""
    if isinstance(value, dict):
        for k, v in value.items():
            if k == key:
                yield v
            yield from walk(v, key)
    elif isinstance(value, list):
        for item in value:
            yield from walk(item, key)


for world in smoke.WORLDS:
    path = smoke.OUT_ROOT / world / "transcript-00.ndjson"
    decisions = non_normal = 0
    stages = collections.Counter()
    final_state = None
    for line in path.read_text(encoding="utf-8").splitlines():
        record = json.loads(line)
        envelope = json.loads(base64.b64decode(record["frameBase64"]))
        kind = envelope.get("messageType")
        if record["direction"] == "runnerToAdapter" and kind == "decision":
            decisions += 1
            body = envelope["payload"].get("certificate", {}).get("body")
            if body and body.get("normalOperation") is False:
                non_normal += 1
                stages.update((w["stage"], w["code"]) for w in body["witnesses"])
        if record["direction"] == "runnerToAdapter" and kind == "checkpoint":
            final_state = envelope["payload"]
    ledgers = list(walk(final_state, "incidentLedger")) if final_state else []
    breaches = [b for ledger in ledgers for b in ledger.get("breaches", [])]
    kinds = collections.Counter(b.get("kind", "operationalIncident") for b in breaches)
    late = [p for ledger in ledgers for p in ledger.get("latePickups", [])]
    print(f"{world}: decisions={decisions} nonNormal={non_normal} witnesses={dict(stages)} "
          f"breaches={dict(kinds)} latePickups={len(late)}"
          + (f" maxLatenessMs={max(p['actualPickupMs'] - p['latestPickupMs'] for p in late)}" if late else ""))
