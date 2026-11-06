"""Rented machine = this machine, job by job (ADR-089 GĐ5/GĐ6, 2026-11-04).

For every job folder under --runs that also exists under --reference (the evidence of the same job run on the home
machine from the same bundle), the transcript receipts must be identical: the same number of frames, and for every
frame the same ordinal, direction, length and SHA-256; the same SHA-256 of the uncompressed transcript; and the same
semanticHash in summary.json. (The .gz file hash is not compared: gzip headers carry the write time.)

Usage: python -I -B cloud_check.py --runs <runs dir> --reference E:/RideBoundData/final-v2/reference [--out <json>]
Exit 0 only if at least one job was compared and every compared job is identical.
"""
import argparse
import gzip
import json
import pathlib


def evidence(folder: pathlib.Path) -> dict:
    return json.loads(gzip.decompress((folder / "evidence.json.gz").read_bytes()))


def semantic(folder: pathlib.Path) -> str:
    return json.loads((folder / "summary.json").read_text(encoding="utf-8"))["semanticHash"]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--runs", required=True)
    parser.add_argument("--reference", required=True)
    parser.add_argument("--out")
    args = parser.parse_args()
    runs, reference = pathlib.Path(args.runs), pathlib.Path(args.reference)
    rows = []
    for folder in sorted(p for p in runs.iterdir() if (p / "evidence.json.gz").exists()):
        ref = reference / folder.name
        if not (ref / "evidence.json.gz").exists():
            continue
        a, b = evidence(folder)["transcript"], evidence(ref)["transcript"]
        first = next((i for i, (x, y) in enumerate(zip(a["frames"], b["frames"])) if x != y), None)
        same = (a["frameCount"] == b["frameCount"] and a["frames"] == b["frames"]
                and a["uncompressedSha256"] == b["uncompressedSha256"] and semantic(folder) == semantic(ref))
        rows.append({"job": folder.name, "identical": same, "frames": a["frameCount"],
                     "referenceFrames": b["frameCount"], "firstDifferentFrame": first,
                     "semanticEqual": semantic(folder) == semantic(ref)})
        print(json.dumps(rows[-1]), flush=True)
    report = {"compared": len(rows), "identical": sum(r["identical"] for r in rows), "jobs": rows}
    print(json.dumps({k: v for k, v in report.items() if k != "jobs"}))
    if args.out:
        out = pathlib.Path(args.out)
        if out.exists():
            raise SystemExit(f"refused: {out} exists")
        out.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return 0 if rows and report["identical"] == len(rows) else 1


if __name__ == "__main__":
    raise SystemExit(main())
