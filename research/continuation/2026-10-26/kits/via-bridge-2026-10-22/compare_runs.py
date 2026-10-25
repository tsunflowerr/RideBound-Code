"""Behavioural comparison of two RideBound job outputs (same decisions?).

Normalisation (as wf6/realjob compare_transcripts.py, bundle docs/wf6/realjob/T.txt:124-128):
  - every transcript frame is base64-decoded and parsed;
  - keys `runId` and any key ending in Hash / Sha256 / hash / sha256 are dropped;
  - 64-hex substrings inside strings are masked (run-chained ids);
  - lists under `actions` and `publicationIds` are compared as multisets.
Build identity (binarySha256, coreCommitSha's effect on hashes) therefore cannot cause a
difference; any difference left is behavioural. This is weaker than a cross-replay (state
hashes are masked too) and is used only to show that a measurement-environment change does
not change decisions.

Usage: python -B compare_runs.py <run dir A> <run dir B> [--json out.json]
Exit 0 = identical, 1 = differ.
"""

from __future__ import annotations

import base64
import json
import lzma
import os
import re
import sys

HEX64 = re.compile(r"[0-9a-f]{64}")
MULTISET_KEYS = {"actions", "publicationIds"}


def drop_key(key: str) -> bool:
    return key == "runId" or key.endswith(("Hash", "Sha256", "hash", "sha256"))


def normalise(value, key: str = ""):
    if isinstance(value, dict):
        return {k: normalise(v, k) for k, v in value.items() if not drop_key(k)}
    if isinstance(value, list):
        items = [normalise(v) for v in value]
        if key in MULTISET_KEYS:
            items.sort(key=lambda item: json.dumps(item, sort_keys=True))
        return items
    if isinstance(value, str):
        return HEX64.sub("<hex64>", value)
    return value


def transcript_path(run_dir: str) -> str:
    for name in ("transcript-00.ndjson", "transcript-00.ndjson.xz"):
        path = os.path.join(run_dir, name)
        if os.path.exists(path):
            return path
    raise FileNotFoundError(f"no transcript in {run_dir}")


def frames(run_dir: str):
    path = transcript_path(run_dir)
    opener = lzma.open if path.endswith(".xz") else open
    with opener(path, "rt", encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            frame = json.loads(base64.b64decode(record["frameBase64"]))
            yield record["direction"], normalise(frame)


def first_difference(a, b, path="$"):
    if type(a) is not type(b):
        return path, repr(a)[:120], repr(b)[:120]
    if isinstance(a, dict):
        for key in sorted(set(a) | set(b)):
            if key not in a or key not in b:
                return f"{path}.{key}", "present" if key in a else "absent", "present" if key in b else "absent"
            found = first_difference(a[key], b[key], f"{path}.{key}")
            if found:
                return found
        return None
    if isinstance(a, list):
        if len(a) != len(b):
            return f"{path}[len]", len(a), len(b)
        for index, (x, y) in enumerate(zip(a, b)):
            found = first_difference(x, y, f"{path}[{index}]")
            if found:
                return found
        return None
    return None if a == b else (path, repr(a)[:120], repr(b)[:120])


def message_type(frame) -> str:
    if isinstance(frame, dict):
        return str(frame.get("type") or frame.get("messageType") or "?")
    return "?"


def main() -> int:
    args = sys.argv[1:]
    out = None
    if "--json" in args:
        index = args.index("--json")
        out = args[index + 1]
        del args[index:index + 2]
    if len(args) != 2:
        print(__doc__)
        return 2
    run_a, run_b = args
    total = equal = 0
    first = None
    left = list(frames(run_a))
    right = list(frames(run_b))
    for ordinal, ((dir_a, frame_a), (dir_b, frame_b)) in enumerate(zip(left, right)):
        total += 1
        if dir_a == dir_b and frame_a == frame_b:
            equal += 1
        elif first is None:
            where = first_difference(frame_a, frame_b) or ("direction", dir_a, dir_b)
            first = {"ordinal": ordinal, "direction": dir_a, "type": message_type(frame_a),
                     "path": where[0], "a": str(where[1]), "b": str(where[2])}
    summaries = []
    for run in (run_a, run_b):
        summary = json.load(open(os.path.join(run, "summary.json"), encoding="utf-8"))
        # Amendment A1 (2026-10-22): runs[*].resources holds process measurements (RSS, CPU, wall
        # time) that differ between any two executions; they are not behaviour and are not compared.
        for entry in summary.get("runs", []):
            if isinstance(entry, dict):
                entry.pop("resources", None)
        summaries.append(normalise(summary))
    result = {
        "runA": run_a, "runB": run_b,
        "framesA": len(left), "framesB": len(right),
        "framesCompared": total, "framesIdentical": equal,
        "firstDifference": first,
        "summaryIdentical": summaries[0] == summaries[1],
        "summaryDifference": None if summaries[0] == summaries[1] else first_difference(*summaries),
    }
    identical = len(left) == len(right) == equal and result["summaryIdentical"]
    result["identical"] = identical
    text = json.dumps(result, indent=2, default=str)
    print(text)
    if out:
        with open(out, "x", encoding="utf-8", newline="\n") as handle:
            handle.write(text + "\n")
    return 0 if identical else 1


if __name__ == "__main__":
    sys.exit(main())
