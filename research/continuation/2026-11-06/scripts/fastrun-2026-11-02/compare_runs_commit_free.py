"""Compare the same jobs run from two repository commits (ADR-089, GĐ5): export commit vs run worktree.

The adapter writes git HEAD into the run manifest (coreCommitSha), and every hash chained from the manifest differs.
Lines that are byte-identical pass as they are. A differing line passes only when its outer fields (direction,
ordinal, schemaVersion, frameLengthBytes) are equal and its decoded frame is equal after coreCommitSha is removed and
every key ending in "Hash" or "Sha256" is removed (recursively). Anything else is reported.

Usage: python -I compare_runs_commit_free.py <rootA> <rootB> [--out report.json]
"""
import argparse
import base64
import gzip
import itertools
import json
import pathlib

OUTER = ("direction", "ordinal", "schemaVersion", "frameLengthBytes")


def strip(value):
    if isinstance(value, dict):
        return {k: strip(v) for k, v in value.items()
                if k != "coreCommitSha" and not k.endswith("Hash") and not k.endswith("Sha256")}
    if isinstance(value, list):
        return [strip(v) for v in value]
    return value


def open_transcript(folder: pathlib.Path):
    gz = folder / "transcript-00.ndjson.gz"
    return gzip.open(gz, "rb") if gz.exists() else open(folder / "transcript-00.ndjson", "rb")


def compare(a: pathlib.Path, b: pathlib.Path) -> dict:
    lines = same_bytes = same_after_strip = 0
    removed = {}
    first = None
    with open_transcript(a) as ta, open_transcript(b) as tb:
        for la, lb in itertools.zip_longest(ta, tb):
            lines += 1
            if la == lb:
                same_bytes += 1
                continue
            ok = la is not None and lb is not None
            if ok:
                oa, ob = json.loads(la), json.loads(lb)
                ok = all(oa.get(k) == ob.get(k) for k in OUTER)
                if ok:
                    fa = json.loads(base64.b64decode(oa["frameBase64"]))
                    fb = json.loads(base64.b64decode(ob["frameBase64"]))
                    ok = strip(fa) == strip(fb)
                    kind = fa.get("messageType", "?")
                    removed[kind] = removed.get(kind, 0) + 1
            if ok:
                same_after_strip += 1
            elif first is None:
                first = lines
    return {"lines": lines, "sameBytes": same_bytes, "sameAfterStrip": same_after_strip,
            "differing": lines - same_bytes - same_after_strip, "firstDifferingLine": first,
            "strippedByMessageType": removed}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root_a")
    parser.add_argument("root_b")
    parser.add_argument("--out")
    args = parser.parse_args()
    root_a, root_b = pathlib.Path(args.root_a), pathlib.Path(args.root_b)
    names = sorted(p.name for p in root_b.iterdir() if (p / "summary.json").exists() and (root_a / p.name / "summary.json").exists())
    results = {}
    for name in names:
        results[name] = compare(root_a / name, root_b / name)
        print(name, json.dumps(results[name]), flush=True)
    clean = sum(r["differing"] == 0 for r in results.values())
    print(f"commit-free identical {clean}/{len(results)}")
    if args.out:
        out = pathlib.Path(args.out)
        if out.exists():
            raise SystemExit(f"refused: {out} exists")
        out.write_text(json.dumps({"rootA": str(root_a), "rootB": str(root_b), "results": results}, indent=2), encoding="utf-8")
    return 0 if results and clean == len(results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
