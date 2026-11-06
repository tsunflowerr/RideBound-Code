"""Decode a RideBound transcript and print the frames in a simulation-time window (read only).
Usage: python -I -B transcript_window.py <transcript.ndjson.gz> <t_from_ms> <t_to_ms> [--types]
The simulation time of a frame is taken from the first of these payload keys found (recursively, top level first):
simulationTimeMs, nowMs, timeMs, atMs."""
import base64
import collections
import gzip
import json
import sys

path, t_from, t_to = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
types_only = "--types" in sys.argv
KEYS = ("simulationTimeMs", "nowMs", "timeMs", "atMs", "tEvalMs", "decisionTimeMs")


def find_time(obj, depth=0):
    if isinstance(obj, dict):
        for k in KEYS:
            if k in obj and isinstance(obj[k], int):
                return obj[k]
        if depth < 3:
            for v in obj.values():
                t = find_time(v, depth + 1)
                if t is not None:
                    return t
    return None


counts = collections.Counter()
examples = {}
last_t = None
with gzip.open(path, "rt", encoding="utf-8") as handle:
    for line in handle:
        f = json.loads(line)
        msg = json.loads(base64.b64decode(f["frameBase64"]))
        mtype = msg.get("messageType")
        t = find_time(msg.get("payload", {}))
        if t is not None:
            last_t = t
        counts[(f["direction"], mtype)] += 1
        if types_only:
            if (f["direction"], mtype) not in examples:
                examples[(f["direction"], mtype)] = json.dumps(msg)[:700]
            continue
        if last_t is not None and t_from <= last_t <= t_to:
            print(f"#{f['ordinal']} {f['direction']} {mtype} t={t} (last {last_t}) {json.dumps(msg.get('payload'))[:3000]}")
        if last_t is not None and last_t > t_to:
            break
if types_only:
    for k, v in counts.items():
        print(k, v)
        print("   ", examples[k])
