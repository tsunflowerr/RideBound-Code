"""Q5.2 (2026-11-06), third job: FS-G2-SHRC71-CL-d20190919-vf-r0-w17 ran at home on Windows from the final-v2 package
(only its evidence is kept: final-v2/reference) and on the rented Linux machine in the F14 run. Evidence compared field by
field (os_compare.py's diff_paths rule). Usage: python -I -B os_compare_evidence_only.py <linux job dir> <windows job dir> <out.json>"""
import gzip
import json
import pathlib
import sys

LIN, WIN, OUT = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])


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


ea = json.load(gzip.open(LIN / "evidence.json.gz", "rt", encoding="utf-8"))
eb = json.load(gzip.open(WIN / "evidence.json.gz", "rt", encoding="utf-8"))
res = {LIN.name: {"evidenceDifferingPaths": diff_paths(ea, eb),
                  "auditTableRows": [len(ea["audit"]["table"]), len(eb["audit"]["table"])],
                  "auditTableEqual": ea["audit"]["table"] == eb["audit"]["table"],
                  "eventsEqual": ea["events"] == eb["events"], "movesEqual": ea["moves"] == eb["moves"],
                  "shadowAllocationsEqual": ea["shadowAllocations"] == eb["shadowAllocations"],
                  "finalOnlineStateEqual": ea["finalOnlineState"] == eb["finalOnlineState"]}}
print(json.dumps(res))
with open(OUT, "x", encoding="utf-8", newline="\n") as h:
    json.dump(res, h, indent=1)
