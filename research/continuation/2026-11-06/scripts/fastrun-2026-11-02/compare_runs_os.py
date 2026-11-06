"""Compare the same jobs run in two places (ADR-089, GĐ5 gate): Linux container vs Windows, or export vs run worktree.

Per job (a folder name present under both roots):
  summary.json   status, semanticHash, repositoryInventorySha256
  run-00.json    every field of "semantic" (resources are timings and are not compared)
  transcript     decompressed frame lines compared byte for byte (gzip bytes are not compared: zlib builds differ);
                 reports the line count, the number of differing lines and the first one (ordinal, message type)
"Identical" = same status, same semanticHash, same semantic fields, and zero differing transcript lines.

Usage: python -I compare_runs_os.py <rootA> <rootB> [--names a,b,...] [--out report.json]
"""
import argparse
import base64
import gzip
import itertools
import json
import pathlib


def open_transcript(folder: pathlib.Path):
    gz = folder / "transcript-00.ndjson.gz"
    return gzip.open(gz, "rb") if gz.exists() else open(folder / "transcript-00.ndjson", "rb")


def message_type(line: bytes) -> str:
    try:
        frame = json.loads(base64.b64decode(json.loads(line)["frameBase64"]))
        return frame.get("messageType", "?")
    except (ValueError, KeyError, TypeError):
        return "?"


def compare_job(a: pathlib.Path, b: pathlib.Path) -> dict:
    sa = json.loads((a / "summary.json").read_text(encoding="utf-8"))
    sb = json.loads((b / "summary.json").read_text(encoding="utf-8"))
    ra = json.loads((a / "run-00.json").read_text(encoding="utf-8"))["semantic"]
    rb = json.loads((b / "run-00.json").read_text(encoding="utf-8"))["semantic"]
    lines = differing = 0
    first = None
    with open_transcript(a) as ta, open_transcript(b) as tb:
        for la, lb in itertools.zip_longest(ta, tb):
            lines += 1
            if la != lb:
                differing += 1
                if first is None:
                    first = {"line": lines, "typeA": message_type(la) if la else "<end>",
                             "typeB": message_type(lb) if lb else "<end>"}
    report = {
        "statusA": sa.get("status"), "statusB": sb.get("status"),
        "semanticHashEqual": sa.get("semanticHash") == sb.get("semanticHash"),
        "inventoryEqual": sa.get("repositoryInventorySha256") == sb.get("repositoryInventorySha256"),
        "semanticFieldsDiffering": sorted(k for k in set(ra) | set(rb) if ra.get(k) != rb.get(k)),
        "acceptedA": len(ra.get("acceptedRequestIds", [])), "acceptedB": len(rb.get("acceptedRequestIds", [])),
        "transcriptLines": lines, "transcriptLinesDiffering": differing, "firstDifference": first,
    }
    report["identical"] = (report["statusA"] == report["statusB"] == "pass" and report["semanticHashEqual"]
                           and not report["semanticFieldsDiffering"] and differing == 0)
    return report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root_a")
    parser.add_argument("root_b")
    parser.add_argument("--names")
    parser.add_argument("--out")
    args = parser.parse_args()
    root_a, root_b = pathlib.Path(args.root_a), pathlib.Path(args.root_b)
    if args.names:
        names = args.names.split(",")
    else:
        names = sorted(p.name for p in root_a.iterdir() if (p / "summary.json").exists() and (root_b / p.name / "summary.json").exists())
    results = {}
    for name in names:
        results[name] = compare_job(root_a / name, root_b / name)
        print(name, json.dumps(results[name]), flush=True)
    verdict = {"rootA": str(root_a), "rootB": str(root_b), "jobs": len(results),
               "identical": sum(r["identical"] for r in results.values()), "results": results}
    print(f"identical {verdict['identical']}/{verdict['jobs']}")
    if args.out:
        out = pathlib.Path(args.out)
        if out.exists():
            raise SystemExit(f"refused: {out} exists")
        out.write_text(json.dumps(verdict, indent=2), encoding="utf-8")
    return 0 if results and verdict["identical"] == verdict["jobs"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
