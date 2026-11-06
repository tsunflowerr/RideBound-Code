"""Q5.2 (2026-11-06): the same job run on the rented Linux machine and on the home Windows machine, compared.
Both runs use commit 0eb9a6ca (coreCommitSha in initializeRun). Two jobs have a transcript on both sides:
FS-G2-SHHZ71-N and FS-G2-SHRC71-CL on d20190919-vf-r0-w08 (Windows: final-v1-local/runs-gates-v2, Linux: the F14 run).
  evidence: every field compared; transcript: frame by frame, byte-identical, or equal after removing keys ending in
  "Hash"/"Sha256" and coreCommitSha (compare_runs_commit_free.py's rule), or different.
Usage: python -I -B os_compare.py <linux runs/final> <windows runs-gates-v2> <out.json>"""
import base64
import gzip
import json
import pathlib
import re
import sys

LIN, WIN, OUT = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])
JOBS = ["FS-G2-SHHZ71-N-d20190919-vf-r0-w08", "FS-G2-SHRC71-CL-d20190919-vf-r0-w08"]
H = re.compile(r"(Hash|Sha256)$")


def strip(o):
    if isinstance(o, dict):
        return {k: strip(v) for k, v in o.items() if not H.search(k) and k != "coreCommitSha"}
    if isinstance(o, list):
        return [strip(v) for v in o]
    return o


def frames(p):
    with gzip.open(p, "rt", encoding="utf-8") as h:
        for line in h:
            f = json.loads(line)
            yield f["direction"], json.loads(base64.b64decode(f["frameBase64"]))


def diff_paths(a, b, path=""):
    if type(a) != type(b):
        return [path]
    if isinstance(a, dict):
        out = []
        for k in sorted(set(a) | set(b)):
            out += [path + "/" + k] if (k not in a or k not in b) else diff_paths(a[k], b[k], path + "/" + k)
        return out
    if isinstance(a, list):
        if len(a) != len(b):
            return [path + f"[len {len(a)} vs {len(b)}]"]
        return [path] if a != b else []
    return [path] if a != b else []


result = {}
for job in JOBS:
    ea = json.load(gzip.open(LIN / job / "evidence.json.gz", "rt", encoding="utf-8"))
    eb = json.load(gzip.open(WIN / job / "evidence.json.gz", "rt", encoding="utf-8"))
    n = same = stripped = other = 0
    commits = set()
    types_stripped = {}
    for (da, a), (db, b) in zip(frames(LIN / job / "transcript-00.ndjson.gz"), frames(WIN / job / "transcript-00.ndjson.gz")):
        n += 1
        if a.get("messageType") == "initializeRun":
            commits |= set(re.findall(r'"coreCommitSha": "([0-9a-f]+)"', json.dumps(a) + json.dumps(b)))
        if da == db and a == b:
            same += 1
        elif da == db and strip(a) == strip(b):
            stripped += 1
            t = a.get("messageType")
            types_stripped[t] = types_stripped.get(t, 0) + 1
        else:
            other += 1
    result[job] = {
        "evidenceDifferingPaths": diff_paths(ea, eb),
        "auditTableRows": [len(ea["audit"]["table"]), len(eb["audit"]["table"])],
        "auditTableEqual": ea["audit"]["table"] == eb["audit"]["table"],
        "eventsEqual": ea["events"] == eb["events"], "movesEqual": ea["moves"] == eb["moves"],
        "shadowAllocationsEqual": ea["shadowAllocations"] == eb["shadowAllocations"],
        "frames": n, "framesIdentical": same, "framesEqualWithoutHashKeys": stripped, "framesOther": other,
        "strippedByType": types_stripped, "coreCommitSha": sorted(commits)}
    print(job, json.dumps(result[job]))
with open(OUT, "x", encoding="utf-8", newline="\n") as h:
    json.dump(result, h, indent=1)
