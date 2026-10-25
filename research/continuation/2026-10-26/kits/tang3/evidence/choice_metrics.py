"""T3.7 choice opportunities from one transcript (read-only), P6 / §17.2 steps 3 and 4.

From the solver execution evidence of every acknowledged decision:
  raw options of a vehicle  = generation.vehicleLosses[v].retainedCandidateCount (includes its no-op)
  gate prunes of a vehicle  = prunedCandidates of that vehicle whose code starts with COMMITMENT_
  eligible options          = raw options - gate prunes (a forced no-op kept by ADR-075 is not a prune)
A decision offers a RAW choice when some vehicle has >= 2 raw options, and a GATED choice when some
vehicle has >= 2 eligible options. Both are reported over decisions that had a pending request
(generation.totalPendingRequestCount > 0), because without one the only option is the no-op.
">= 2 options" is mostly "serve the new rider or not". The stricter MULTI-INSERTION share asks for
>= 3 options (the no-op plus at least two different insertions), i.e. a choice of HOW to serve,
which is what thesis chapter 4 measured (2.48 % of sets with >= 2 insertion positions).

Usage: python -B choice_metrics.py <transcript-00.ndjson>   (prints one JSON object)
"""
from __future__ import annotations

import base64
import collections
import hashlib
import json
import pathlib
import sys


def decisions(path: pathlib.Path):
    sent, acknowledged = {}, set()
    for line in path.read_text(encoding="utf-8").splitlines():
        record = json.loads(line)
        frame = base64.b64decode(record["frameBase64"])
        if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
            raise ValueError(f"{path}: frame hash mismatch")
        envelope = json.loads(frame)
        kind = envelope.get("messageType")
        if record["direction"] == "runnerToAdapter" and kind == "decision":
            sent.setdefault(envelope["payload"]["decisionHash"], envelope["payload"])
        elif record["direction"] == "adapterToRunner" and kind == "decisionApplied":
            if envelope["payload"]["decisionHash"] in sent:
                acknowledged.add(envelope["payload"]["decisionHash"])
    return [payload for key, payload in sent.items() if key in acknowledged]


def measure(path: pathlib.Path) -> dict:
    total = with_pending = raw_choice = gated_choice = without_evidence = 0
    raw_multi = gated_multi = 0
    vehicle_rows = vehicle_raw = vehicle_gated = vehicle_raw_multi = vehicle_gated_multi = 0
    gate_codes = collections.Counter()
    for payload in decisions(path):
        total += 1
        evidence = (payload.get("solver") or {}).get("executionEvidence")
        if not evidence:
            without_evidence += 1
            continue
        generation = evidence["generation"]
        if generation["totalPendingRequestCount"] == 0:
            continue
        with_pending += 1
        gate = collections.Counter()
        for prune in evidence.get("prunedCandidates", []):
            if prune["code"].startswith("COMMITMENT_"):
                gate[prune["vehicleId"]] += 1
                gate_codes[prune["code"]] += 1
        any_raw = any_gated = any_raw_multi = any_gated_multi = False
        for loss in generation["vehicleLosses"]:
            raw = loss["retainedCandidateCount"]
            eligible = raw - gate[loss["vehicleId"]]
            vehicle_rows += 1
            vehicle_raw += raw >= 2
            vehicle_gated += eligible >= 2
            vehicle_raw_multi += raw >= 3
            vehicle_gated_multi += eligible >= 3
            any_raw |= raw >= 2
            any_gated |= eligible >= 2
            any_raw_multi |= raw >= 3
            any_gated_multi |= eligible >= 3
        raw_choice += any_raw
        gated_choice += any_gated
        raw_multi += any_raw_multi
        gated_multi += any_gated_multi
    share = (lambda n, d: n / d if d else None)
    return {
        "transcript": str(path),
        "decisionsAcknowledged": total,
        "decisionsWithoutEvidence": without_evidence,
        "decisionsWithPending": with_pending,
        "rawChoiceShare": share(raw_choice, with_pending),
        "gatedChoiceShare": share(gated_choice, with_pending),
        "rawMultiInsertionShare": share(raw_multi, with_pending),
        "gatedMultiInsertionShare": share(gated_multi, with_pending),
        "vehicleDecisionsWithPending": vehicle_rows,
        "vehicleRawChoiceShare": share(vehicle_raw, vehicle_rows),
        "vehicleGatedChoiceShare": share(vehicle_gated, vehicle_rows),
        "vehicleRawMultiInsertionShare": share(vehicle_raw_multi, vehicle_rows),
        "vehicleGatedMultiInsertionShare": share(vehicle_gated_multi, vehicle_rows),
        "gatePrunes": dict(sorted(gate_codes.items())),
    }


if __name__ == "__main__":
    print(json.dumps(measure(pathlib.Path(sys.argv[1])), indent=1))
