"""Independent recomputation of the Tier 3 episode table from a raw transcript.

Implemented from METRICS-SPEC.md (v1, 2026-09-25) only. Standard library only, Python 3.10.

Usage:
    python -B verify_independent.py <transcript-00.ndjson> <output.json>

The output file must not already exist (this script never overwrites).
"""

import base64
import hashlib
import json
import math
import os
import sys
from collections import Counter, OrderedDict

COORDS = (("pickup", "pickupEtaMs", "pickupEtaTotalMs"), ("drop", "dropEtaMs", "dropEtaTotalMs"))
E2_THRESHOLD_MS = 60000


def read_transcript(path):
    """Stream the transcript; keep only what the metrics need."""
    decisions = []          # list of dicts in frame order
    acked_hashes = []       # decisionHash of each decisionApplied frame, in order
    ack_ordinals = {}       # decisionHash -> first frame index of its ack
    last_checkpoint = None
    frames = 0
    sha_mismatch = []
    message_counts = Counter()
    with open(path, "r", encoding="utf-8") as fh:
        for idx, line in enumerate(fh):
            line = line.strip()
            if not line:
                continue
            frames += 1
            rec = json.loads(line)
            raw = base64.b64decode(rec["frameBase64"], validate=True)
            if hashlib.sha256(raw).hexdigest() != str(rec.get("frameSha256", "")).lower():
                sha_mismatch.append(idx)
            env = json.loads(raw.decode("utf-8"))
            direction = rec.get("direction")
            mtype = env.get("messageType")
            message_counts[f"{direction}:{mtype}"] += 1
            payload = env.get("payload") or {}
            if direction == "runnerToAdapter" and mtype == "decision":
                cert = payload.get("certificate")
                body = cert.get("body") if isinstance(cert, dict) else None
                normal = body.get("normalOperation") if isinstance(body, dict) else None
                pubs = []
                for action in payload.get("actions") or []:
                    if action.get("decisionType") != "promisePublished":
                        continue
                    ap = action.get("payload") or {}
                    promise = ap.get("promise") or {}
                    pubs.append({
                        "publicationId": ap.get("publicationId"),
                        "requestId": promise.get("requestId"),
                        "pickupEtaMs": promise.get("pickupEtaMs"),
                        "dropEtaMs": promise.get("dropEtaMs"),
                    })
                decisions.append({
                    "frameIndex": idx,
                    "decisionHash": payload.get("decisionHash"),
                    "normalOperation": normal,
                    "hasCertificateBody": isinstance(body, dict),
                    "publications": pubs,
                })
            elif direction == "adapterToRunner" and mtype == "decisionApplied":
                h = payload.get("decisionHash")
                acked_hashes.append(h)
                ack_ordinals.setdefault(h, idx)
            elif direction == "runnerToAdapter" and mtype == "checkpoint":
                last_checkpoint = env
    return {
        "frames": frames,
        "shaMismatchLines": sha_mismatch,
        "messageCounts": dict(sorted(message_counts.items())),
        "decisions": decisions,
        "ackedHashes": acked_hashes,
        "ackOrdinals": ack_ordinals,
        "lastCheckpoint": last_checkpoint,
    }


def eta(obj, key):
    value = obj[key]
    if not isinstance(value, int) or isinstance(value, bool):
        raise TypeError(f"non-integer ETA {key}={value!r}")
    return value


def per_rider(history, pickup_close_ms):
    """Return per-rider accumulators, chain check results, and cross-check 1 failures."""
    entries = history["entries"]
    acc = {c: {"C": 0, "X": 0, "V": 0, "openRevisions": 0, "closedRevisions": 0} for c, _, _ in COORDS}
    chain_failures = []
    chain_full_failures = []
    delta_failures = []
    kind_failures = []
    for t, entry in enumerate(entries):
        kind = entry.get("kind")
        if (t == 0 and kind != "initialPromise") or (t > 0 and kind != "revision"):
            kind_failures.append({"requestId": history["requestId"], "entryIndex": t, "kind": kind})
        p_obj = entry["publishedPromise"]["projection"]
        published_at = entry["publishedPromise"]["publishedAtMs"]
        if t == 0:
            continue  # v = e = p for the initial entry: all three differences are zero.
        v_obj = entry["previousPromise"]["projection"]
        e_obj = entry["exogenousProjection"]
        prev_p = entries[t - 1]["publishedPromise"]
        # Chain continuity (spec: v equals previous entry's p), checked through closed rows too.
        for _, key, _ in COORDS:
            if eta(v_obj, key) != eta(prev_p["projection"], key):
                chain_failures.append({"requestId": history["requestId"], "entryIndex": t, "field": key,
                                       "v": eta(v_obj, key), "previousP": eta(prev_p["projection"], key)})
        # Stricter supplementary check: whole previousPromise object equals previous publishedPromise.
        if entry["previousPromise"] != prev_p:
            chain_full_failures.append({"requestId": history["requestId"], "entryIndex": t})
        for coord, key, total_key in COORDS:
            p, v, e = eta(p_obj, key), eta(v_obj, key), eta(e_obj, key)
            x, c, z = e - v, p - e, p - v
            deltas = entry.get("deltas") or {}
            for name, value, basis in (("x", x, "exogenous"), ("c", c, "decisionInduced"), ("z", z, "visible")):
                recorded = (deltas.get(basis) or {}).get(total_key)
                if recorded != abs(value):
                    delta_failures.append({"requestId": history["requestId"], "entryIndex": t, "coordinate": coord,
                                           "quantity": name, "absComputed": abs(value),
                                           f"deltas.{basis}.{total_key}": recorded,
                                           "publishedAtMs": published_at, "pickupCloseMs": pickup_close_ms})
            closed = coord == "pickup" and pickup_close_ms is not None and published_at >= pickup_close_ms
            if closed:
                acc[coord]["closedRevisions"] += 1
                continue
            acc[coord]["openRevisions"] += 1
            acc[coord]["C"] += abs(c)
            acc[coord]["X"] += abs(x)
            acc[coord]["V"] += abs(z)
    return acc, chain_failures, chain_full_failures, delta_failures, kind_failures


def cvar95(values):
    """Sort ascending, mean of values from index floor(0.95 n) to the end (integer-exact index)."""
    n = len(values)
    if n == 0:
        return None, None, None
    ordered = sorted(values)
    start_exact = (95 * n) // 100
    start_float = math.floor(0.95 * n)
    tail = ordered[start_exact:]
    return sum(tail) / len(tail), start_exact, start_float


def analyse(path):
    data = read_transcript(path)
    out = OrderedDict()
    out["transcript"] = path
    out["transcriptSha256"] = file_sha256(path)
    out["frames"] = data["frames"]
    out["frameSha256Mismatches"] = len(data["shaMismatchLines"])
    out["messageCounts"] = data["messageCounts"]

    # ---- decisions / acknowledgements ----
    decisions = data["decisions"]
    acked_set = set(data["ackedHashes"])
    decision_hashes = [d["decisionHash"] for d in decisions]
    acked_decisions = []
    seen = set()
    for d in decisions:
        h = d["decisionHash"]
        if h in acked_set and h not in seen:
            seen.add(h)
            acked_decisions.append(d)
    ack_before_decision = [d["decisionHash"] for d in acked_decisions
                           if data["ackOrdinals"][d["decisionHash"]] < d["frameIndex"]]
    out["decisionFrames"] = len(decisions)
    out["distinctDecisionHashes"] = len(set(decision_hashes))
    out["decisionAppliedFrames"] = len(data["ackedHashes"])
    out["acksWithoutDecision"] = len(acked_set - set(decision_hashes))
    out["acksPrecedingTheirDecision"] = len(ack_before_decision)

    final = data["lastCheckpoint"]
    state = None
    if final is not None:
        state = ((final.get("payload") or {}).get("content") or {}).get("onlineState")
    out["finalCheckpoint"] = state is not None

    table = OrderedDict()
    table["finalCheckpoint"] = state is not None
    table["decisionsAcknowledged"] = len(acked_decisions)
    table["nonNormalCertificates"] = sum(1 for d in acked_decisions if d["normalOperation"] is False)
    out["acknowledgedDecisionsWithoutCertificateBody"] = sum(1 for d in acked_decisions if not d["hasCertificateBody"])

    cross = OrderedDict()
    if state is None:
        out["episodeTable"] = table
        out["crossChecks"] = cross
        return out

    requests = state.get("requests") or []
    lifecycle = Counter(r.get("lifecycle") for r in requests)
    table["requests"] = len(requests)
    table["lifecycle"] = dict(sorted(lifecycle.items()))
    table["E1ServedShare"] = (lifecycle.get("completed", 0) / len(requests)) if requests else None

    by_id = {r["requestId"]: r for r in requests}
    ledger = state.get("commitmentLedger") or []
    table["promisedRiders"] = len(ledger)

    drop_v = []
    sums = Counter()
    chain_failures, chain_full_failures, delta_failures, kind_failures = [], [], [], []
    riders_without_request = 0
    riders_never_boarded = 0
    open_closed = Counter()
    for history in ledger:
        req = by_id.get(history["requestId"])
        if req is None:
            riders_without_request += 1
        close_ms = req.get("actualPickupTimeMs") if req else None
        if close_ms is None:
            riders_never_boarded += 1
        acc, cf, cff, df, kf = per_rider(history, close_ms)
        chain_failures += cf
        chain_full_failures += cff
        delta_failures += df
        kind_failures += kf
        drop_v.append(acc["drop"]["V"])
        for coord in ("pickup", "drop"):
            for q in ("C", "X", "V"):
                sums[f"{coord}{q}"] += acc[coord][q]
            open_closed[f"{coord}Open"] += acc[coord]["openRevisions"]
            open_closed[f"{coord}Closed"] += acc[coord]["closedRevisions"]

    n = len(drop_v)
    table["E2ShareDropVAbove60s"] = (sum(1 for v in drop_v if v > E2_THRESHOLD_MS) / n) if n else None
    cv, start_exact, start_float = cvar95(drop_v)
    table["dropVCvar95Ms"] = cv
    table["sumDropC"] = sums["dropC"]
    table["sumDropX"] = sums["dropX"]
    table["sumDropV"] = sums["dropV"]

    incident = state.get("incidentLedger") or {}
    breaches = incident.get("breaches") or []
    table["breaches"] = dict(sorted(Counter(b.get("kind", "operationalIncident") for b in breaches).items()))
    late = incident.get("latePickups") or []
    table["latePickups"] = len(late)
    table["latePickupMaxMs"] = max((lp["actualPickupMs"] - lp["latestPickupMs"] for lp in late), default=0)

    supp = OrderedDict()
    supp["sumPickupC_open"] = sums["pickupC"]
    supp["sumPickupX_open"] = sums["pickupX"]
    supp["sumPickupV_open"] = sums["pickupV"]
    supp["revisionCounts"] = dict(open_closed)
    supp["cvarTailStartIndexExact"] = start_exact
    supp["cvarTailStartIndexFloat"] = start_float
    supp["cvarTailLength"] = (n - start_exact) if n else 0
    supp["cvarTailLengthCeil5pct"] = math.ceil(0.05 * n) if n else 0
    supp["ridersWithDropVAbove60s"] = sum(1 for v in drop_v if v > E2_THRESHOLD_MS)
    supp["ridersWithDropVEqual60s"] = sum(1 for v in drop_v if v == E2_THRESHOLD_MS)
    supp["ledgerRidersWithoutRequest"] = riders_without_request
    supp["ledgerRidersWithoutActualPickup"] = riders_never_boarded
    supp["breachesMissingKind"] = sum(1 for b in breaches if "kind" not in b)
    supp["latePickupsKeyPresent"] = "latePickups" in incident
    supp["entryKindViolations"] = len(kind_failures)
    supp["firstEntryKindViolation"] = kind_failures[0] if kind_failures else None
    out["supplementary"] = supp

    # Chain continuity (spec section "Per-rider promise history").
    cross["chainContinuityEta"] = {"pass": not chain_failures, "failures": len(chain_failures),
                                   "firstCounterexample": chain_failures[0] if chain_failures else None}
    cross["chainContinuityFullPreviousPromise_supplementary"] = {
        "pass": not chain_full_failures, "failures": len(chain_full_failures),
        "firstCounterexample": chain_full_failures[0] if chain_full_failures else None}

    # Cross-check 1.
    drop_fail = [f for f in delta_failures if f["coordinate"] == "drop"]
    pick_fail = [f for f in delta_failures if f["coordinate"] == "pickup"]
    cross["check1_deltaTotals"] = {
        "pass": not delta_failures,
        "revisionsChecked": sum(len(h["entries"]) - 1 for h in ledger),
        "dropFailures": len(drop_fail), "pickupFailures": len(pick_fail),
        "firstCounterexample": delta_failures[0] if delta_failures else None}

    # Cross-check 2.
    acked_pubs = {}
    acked_pub_count = Counter()
    for d in acked_decisions:
        for pub in d["publications"]:
            acked_pub_count[pub["publicationId"]] += 1
            acked_pubs.setdefault(pub["publicationId"], pub)
    unacked_pub_ids = set()
    for d in decisions:
        if d["decisionHash"] not in acked_set:
            for pub in d["publications"]:
                unacked_pub_ids.add(pub["publicationId"])
    ledger_ids = []
    c2_fail = []
    for history in ledger:
        for t, entry in enumerate(history["entries"]):
            pid = entry.get("publicationId")
            ledger_ids.append(pid)
            cnt = acked_pub_count.get(pid, 0)
            if cnt != 1:
                c2_fail.append({"type": "ledgerPublicationActionCount", "publicationId": pid,
                                "requestId": history["requestId"], "entryIndex": t, "count": cnt})
                if cnt == 0:
                    continue
            pub = acked_pubs[pid]
            proj = entry["publishedPromise"]["projection"]
            if pub["pickupEtaMs"] != proj["pickupEtaMs"] or pub["dropEtaMs"] != proj["dropEtaMs"]:
                c2_fail.append({"type": "etaMismatch", "publicationId": pid, "requestId": history["requestId"],
                                "entryIndex": t, "action": [pub["pickupEtaMs"], pub["dropEtaMs"]],
                                "ledger": [proj["pickupEtaMs"], proj["dropEtaMs"]]})
            if pub["requestId"] != history["requestId"]:
                c2_fail.append({"type": "requestIdMismatch_supplementary", "publicationId": pid,
                                "ledgerRequestId": history["requestId"], "actionRequestId": pub["requestId"]})
    ledger_id_counts = Counter(ledger_ids)
    dup_ledger = [pid for pid, c in ledger_id_counts.items() if c > 1]
    for pid in dup_ledger:
        c2_fail.append({"type": "duplicateLedgerPublicationId", "publicationId": pid, "count": ledger_id_counts[pid]})
    missing = [pid for pid in acked_pub_count if pid not in ledger_id_counts]
    for pid in missing:
        c2_fail.append({"type": "acknowledgedPublicationMissingFromLedger", "publicationId": pid})
    cross["check2_publications"] = {
        "pass": not c2_fail,
        "ledgerEntries": len(ledger_ids),
        "acknowledgedPromisePublishedActions": sum(acked_pub_count.values()),
        "distinctAcknowledgedPublicationIds": len(acked_pub_count),
        "acknowledgedPublicationsMissingFromLedger": len(missing),
        "publicationsInUnacknowledgedDecisions": len(unacked_pub_ids),
        "failures": len(c2_fail),
        "firstCounterexample": c2_fail[0] if c2_fail else None}

    out["episodeTable"] = table
    out["crossChecks"] = cross
    return out


def file_sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    src, dst = sys.argv[1], sys.argv[2]
    if os.path.exists(dst):
        print(f"refusing to overwrite existing file: {dst}")
        return 3
    result = analyse(src)
    with open(dst, "x", encoding="utf-8", newline="\n") as fh:
        json.dump(result, fh, indent=2, ensure_ascii=False)
        fh.write("\n")
    print(json.dumps({"episodeTable": result["episodeTable"], "crossChecks": result["crossChecks"],
                      "supplementary": result.get("supplementary")}, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
