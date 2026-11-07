from __future__ import annotations

import json
import sys
import time


MODE = sys.argv[1] if len(sys.argv) > 1 else "normal"
HASH = "a" * 64
CHECKPOINT_HASH = "b" * 64


def emit(value):
    encoded = (
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")
    sys.stdout.buffer.write(encoded)
    sys.stdout.buffer.flush()


def response(message_type, payload, source=None):
    value = {"schemaVersion": "1.0.0", "messageType": message_type}
    if source is not None:
        for field in ["runId", "scenarioId", "epochId", "simTimeMs"]:
            if field in source and (message_type in {"decision"} or field in {"runId", "scenarioId"}):
                value[field] = source[field]
    value["payload"] = payload
    return value


for line in sys.stdin:
    incoming = json.loads(line)
    kind = incoming["messageType"]
    if kind == "hello":
        if MODE == "partial":
            sys.stdout.buffer.write(b'{"schemaVersion":"1.0.0"')
            sys.stdout.buffer.flush()
            raise SystemExit(0)
        if MODE == "malformed":
            sys.stdout.buffer.write(b"{broken}\n")
            sys.stdout.buffer.flush()
            continue
        if MODE == "duplicate":
            sys.stdout.buffer.write(b'{"schemaVersion":"1.0.0","messageType":"helloAck","messageType":"helloAck","payload":{}}\n')
            sys.stdout.buffer.flush()
            continue
        if MODE == "oversized":
            sys.stdout.buffer.write(b"x" * 1024 + b"\n")
            sys.stdout.buffer.flush()
            continue
        if MODE == "stderr":
            sys.stderr.write("e" * 4096)
            sys.stderr.flush()
            time.sleep(1)
            continue
        if MODE == "timeout":
            time.sleep(5)
            continue
        if MODE == "crash":
            raise SystemExit(7)
        if MODE == "error":
            emit(response("error", {"code": "HASH_MISMATCH", "message": "test"}))
            continue
        ack = response(
            "helloAck",
            {
                "selectedSchemaVersion": "1.0.0",
                "capabilitySelection": {
                    "status": "accepted",
                    "positionModel": "nodeOnly" if MODE == "node-only" else "directedEdgeProgress",
                    "capabilities": [
                        "dynamicTravelTimes",
                        "exactEventOrdering",
                        "oldPlanProjection",
                    ],
                    "maxFleetSize": 100,
                    "maxRequestCount": 1000,
                },
            },
        )
        emit(ack)
        if MODE == "extra":
            emit(ack)
    elif kind == "initializeRun":
        emit(response("initialized", {"manifestHash": HASH, "initialState": {}}, incoming))
    elif kind == "eventBatch":
        emit(response("decision", {"decisionHash": HASH, "actions": []}, incoming))
    elif kind == "decisionApplied":
        pass
    elif kind == "checkpoint":
        emit(response("checkpoint", {"checkpointHash": CHECKPOINT_HASH}, incoming))
    elif kind == "restore":
        emit(response("restore", {"status": "restored", "checkpointHash": CHECKPOINT_HASH}, incoming))
    elif kind == "shutdown":
        raise SystemExit(0)
