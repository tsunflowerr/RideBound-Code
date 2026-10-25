"""T3.6: signed v/e/p audit rows and an episode summary, rebuilt from one run transcript.

WHY THIS NEEDS NO PROTOCOL CHANGE
---------------------------------
`promisePublished` actions carry `p` and three ABSOLUTE deltas, not `e` (tang3/oracle/README.md), so the
signs of x = e - v and c = p - e cannot be recovered from actions alone. But the Runner's final
checkpoint holds the whole commitment ledger, and every revision entry stores `previousPromise` (v),
`exogenousProjection` (e) and `publishedPromise` (p) with exact integer ETAs. The signed audit row is
therefore rebuilt from the checkpoint, and two production records are used only as cross-checks:
  1. |e - v|, |p - e|, |p - v| must equal the entry's exogenous, decisionInduced and visible deltas;
  2. every ledger publication must appear exactly once as a `promisePublished` action of a decision
     that the adapter acknowledged (decisionApplied with the same hash), with the same p.
A run without a final checkpoint has no audit rows; it is reported as such, never skipped.

Totals per rider and coordinate come from the independent T3.2 oracle (tang3/oracle/accounting.py).
The pickup coordinate closes at the actual boarding (oracle M11): a revision published at or after
the boarding does not accumulate on it.

Usage: python -B audit_vep.py <transcript-00.ndjson> [--rows rows.tsv] [--json summary.json]
Outputs are created with mode "x" (never overwritten).
"""
from __future__ import annotations

import argparse
import base64
import collections
import hashlib
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "oracle"))

from accounting import AuditRow, accumulate  # noqa: E402

COORDINATES = (("pickup", "pickupEtaMs", "pickupEtaTotalMs"), ("drop", "dropEtaMs", "dropEtaTotalMs"))
E2_THRESHOLD_MS = 60_000


class EvidenceError(ValueError):
    """The transcript contradicts itself; the run's evidence is not usable as is."""


def read_frames(path: pathlib.Path):
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines()):
        record = json.loads(line)
        frame = base64.b64decode(record["frameBase64"])
        if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
            raise EvidenceError(f"line {number}: frame hash mismatch")
        yield record["direction"], json.loads(frame)


def load(path: pathlib.Path):
    decisions, acknowledged, final = {}, set(), None
    for direction, envelope in read_frames(path):
        kind = envelope.get("messageType")
        if direction == "runnerToAdapter" and kind == "decision":
            payload = envelope["payload"]
            decisions[payload["decisionHash"]] = payload
        elif direction == "adapterToRunner" and kind == "decisionApplied":
            acknowledged.add(envelope["payload"]["decisionHash"])
        elif direction == "runnerToAdapter" and kind == "checkpoint":
            final = envelope["payload"]["content"]["onlineState"]
    return decisions, acknowledged, final


def published_actions(decisions, acknowledged):
    """publicationId -> (promise, deltas) for every promisePublished of an acknowledged decision."""
    out = {}
    for decision_hash, payload in decisions.items():
        if decision_hash not in acknowledged:
            continue
        for action in payload.get("actions", []):
            if action.get("decisionType") != "promisePublished":
                continue
            value = action["payload"]
            if value["publicationId"] in out:
                raise EvidenceError(f"publication {value['publicationId']} published twice")
            out[value["publicationId"]] = value
    return out


def audit(path: pathlib.Path):
    decisions, acknowledged, state = load(path)
    if state is None:
        return None, {"transcript": str(path), "finalCheckpoint": False}
    requests = {request["requestId"]: request for request in state["requests"]}
    actions = published_actions(decisions, acknowledged)
    rows, table = [], []
    seen_publications = set()
    for history in state["commitmentLedger"]:
        request_id = history["requestId"]
        boarded = requests.get(request_id, {}).get("actualPickupTimeMs")
        for sequence, entry in enumerate(history["entries"]):
            published = entry["publishedPromise"]
            publication = entry["publicationId"]
            seen_publications.add(publication)
            action = actions.get(publication)
            if action is None:
                raise EvidenceError(f"{request_id}: ledger publication {publication} has no acknowledged action")
            for coordinate, eta, dimension in COORDINATES:
                p = published["projection"][eta]
                if action["promise"][eta] != p:
                    raise EvidenceError(f"{request_id}/{coordinate}: action p differs from ledger p")
                if entry["kind"] == "initialPromise":
                    v = e = p
                else:
                    v = entry["previousPromise"]["projection"][eta]
                    e = entry["exogenousProjection"][eta]
                    deltas = entry["deltas"]
                    for name, value in (("exogenous", abs(e - v)), ("decisionInduced", abs(p - e)),
                                        ("visible", abs(p - v))):
                        if deltas[name][dimension] != value:
                            raise EvidenceError(
                                f"{request_id}/{coordinate}#{sequence}: |{name}| ledger="
                                f"{deltas[name][dimension]} recomputed={value}")
                closed = (coordinate == "pickup" and boarded is not None
                          and published["publishedAtMs"] >= boarded and entry["kind"] != "initialPromise")
                rows.append(AuditRow(request_id, coordinate, sequence, v, e, p,
                                     initial=entry["kind"] == "initialPromise", closed=closed))
                table.append((request_id, coordinate, sequence, entry["kind"], published["publishedAtMs"],
                              v, e, p, e - v, p - e, p - v, int(closed)))
    missing = set(actions) - seen_publications
    if missing:
        raise EvidenceError(f"{len(missing)} acknowledged publications are not in the final ledger")
    totals = accumulate(rows)
    return table, summarize(path, state, totals, decisions, acknowledged)


def summarize(path, state, totals, decisions, acknowledged):
    lifecycle = collections.Counter(request["lifecycle"] for request in state["requests"])
    incidents = state["incidentLedger"]
    breach_kinds = collections.Counter(b.get("kind", "operationalIncident") for b in incidents["breaches"])
    late = incidents.get("latePickups", [])
    non_normal = sum(
        1 for payload in decisions.values()
        if payload["decisionHash"] in acknowledged
        and (payload.get("certificate", {}).get("body") or {}).get("normalOperation") is False)
    drop_v = sorted(t.V for (request, coordinate), t in totals.items() if coordinate == "drop")
    # METRICS-SPEC v1.1 item 1: exact integer tail index, no floating point.
    tail = drop_v[(95 * len(drop_v)) // 100:] if drop_v else []
    requests = len(state["requests"])
    served = lifecycle.get("completed", 0)
    return {
        "transcript": str(path),
        "finalCheckpoint": True,
        "requests": requests,
        "lifecycle": dict(sorted(lifecycle.items())),
        "E1ServedShare": served / requests if requests else None,
        "promisedRiders": len(drop_v),
        "E2ShareDropVAbove60s": (sum(1 for value in drop_v if value > E2_THRESHOLD_MS) / len(drop_v)
                                 if drop_v else None),
        "dropVCvar95Ms": sum(tail) / len(tail) if tail else None,
        "sumDropC": sum(t.C for (r, c), t in totals.items() if c == "drop"),
        "sumDropX": sum(t.X for (r, c), t in totals.items() if c == "drop"),
        "sumDropV": sum(drop_v),
        "sumPickupC": sum(t.C for (r, c), t in totals.items() if c == "pickup"),
        "sumPickupX": sum(t.X for (r, c), t in totals.items() if c == "pickup"),
        "sumPickupV": sum(t.V for (r, c), t in totals.items() if c == "pickup"),
        "decisionsAcknowledged": len(acknowledged & set(decisions)),
        "nonNormalCertificates": non_normal,
        "breaches": dict(sorted(breach_kinds.items())),
        # Recovery burden (T3.7): riders with at least one forced-reference breach.
        "ridersWithForcedBreach": len({
            b["requestId"] for b in incidents["breaches"] if b.get("kind") == "forcedReference"}),
        "latePickups": len(late),
        "latePickupMaxMs": max((p["actualPickupMs"] - p["latestPickupMs"] for p in late), default=0),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("transcript", type=pathlib.Path)
    parser.add_argument("--rows", type=pathlib.Path)
    parser.add_argument("--json", type=pathlib.Path)
    arguments = parser.parse_args()
    table, summary = audit(arguments.transcript)
    print(json.dumps(summary, indent=1))
    if arguments.rows and table is not None:
        with open(arguments.rows, "x", encoding="utf-8", newline="\n") as handle:
            handle.write("requestId\tcoordinate\tsequence\tkind\tpublishedAtMs\tv\te\tp\tx\tc\tz\tclosed\n")
            for row in table:
                handle.write("\t".join(str(value) for value in row) + "\n")
    if arguments.json:
        with open(arguments.json, "x", encoding="utf-8", newline="\n") as handle:
            json.dump(summary, handle, indent=1, sort_keys=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
