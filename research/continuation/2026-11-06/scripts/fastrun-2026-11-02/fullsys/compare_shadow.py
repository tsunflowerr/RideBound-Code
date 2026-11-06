"""Per-rider Shadow allocation of RideBound runs vs the sealed emulator predictions (ADR-089 GĐ4 Shadow gate, 2026-11-04).

Real:     the shadowAllocations array of the LAST checkpoint frame in transcript-00.ndjson.gz
          (OnlineStateCanonicalWriter.WriteShadowAllocations: requestId, class "guaranteed"/"allowed", score = the
          double's round-trip string). Read without parsing the whole checkpoint: the frame is base64-decoded from the
          line bytes, its SHA-256 checked against the record, and only the array is parsed.
Emulator: sS and aSH of the sealed rider rows (emulate_fullsys.js adapter / emulate_timed.js attr-predictions.tsv);
          the predictions file must be listed in the seal file.
Exact per cell-arm = the same riders carry an allocation on both sides, every class equals (guaranteed <-> aSH 0,
allowed <-> aSH 1) and every score is the same double.

Usage: python -I -B compare_shadow.py <predictions.tsv> <seal file> <out.json> --root <runs> [--root <runs> ...]
"""
import base64
import collections
import csv
import gzip
import hashlib
import json
import pathlib
import sys

KEY_B64 = b'"frameBase64":"'


def field(line: bytes, name: bytes) -> bytes:
    i = line.index(b'"' + name + b'":') + len(name) + 3
    j = i
    while line[j:j + 1] not in (b",", b"}"):
        j += 1
    return line[i:j]


def last_allocations(transcript: pathlib.Path):
    found = None
    with gzip.open(transcript, "rb") as handle:
        for line in handle:
            if not line.startswith(b'{"direction":"runnerToAdapter"'):   # canonical JSON: keys sorted
                continue
            start = line.index(KEY_B64) + len(KEY_B64)
            end = line.index(b'"', start)
            frame = base64.b64decode(line[start:end])
            if b'"messageType":"checkpoint"' not in frame[:400]:
                continue
            if hashlib.sha256(frame).hexdigest().encode() != field(line, b"frameSha256").strip(b'"'):
                raise SystemExit(f"{transcript}: checkpoint frame hash mismatch")
            k = frame.rfind(b'"shadowAllocations":[')
            if k < 0:
                found = {}
                continue
            k += len(b'"shadowAllocations":')
            found = {a["requestId"]: (a["class"], a["score"]) for a in json.loads(frame[k:frame.index(b"]", k) + 1])}
    return found


def main() -> int:
    args = sys.argv[1:]
    roots = []
    while "--root" in args:
        i = args.index("--root")
        roots.append(pathlib.Path(args[i + 1]))
        del args[i:i + 2]
    predictions, seal, out = (pathlib.Path(a) for a in args)
    if out.exists():
        raise SystemExit(f"refused: {out} exists")
    digest = hashlib.sha256(predictions.read_bytes()).hexdigest()
    if not any(l.startswith(digest) and l.endswith(predictions.name) for l in seal.read_text(encoding="utf-8").splitlines()):
        raise SystemExit(f"{predictions.name} is not in {seal.name}")
    emulator = collections.defaultdict(dict)
    with open(predictions, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            if row["aSH"] not in ("", "undefined", "NaN"):
                emulator[(row["role"], row["cell"])][row["requestId"]] = (int(row["aSH"]), float(row["sS"]))
    results = []
    for root in roots:
        for folder in sorted(p for p in root.iterdir() if (p / "summary.json").exists()):
            if json.loads((folder / "summary.json").read_text(encoding="utf-8")).get("status") != "pass":
                continue
            key = next((k for k in emulator if folder.name == f"{k[0]}-{k[1]}"), None)
            if key is None:
                continue
            real = last_allocations(folder / "transcript-00.ndjson.gz")
            if real is None:
                raise SystemExit(f"{folder}: no checkpoint")
            emu = emulator[key]
            cls = {"guaranteed": 0, "allowed": 1}
            missing = sorted(set(emu) ^ set(real))
            classDiff = sorted(r for r in set(emu) & set(real) if cls[real[r][0]] != emu[r][0])
            scoreDiff = sorted(r for r in set(emu) & set(real) if float(real[r][1]) != emu[r][1])
            result = {"role": key[0], "cell": key[1], "ridersEmulator": len(emu), "ridersReal": len(real),
                      "notOnBothSides": len(missing), "classDiffers": len(classDiff), "scoreDiffers": len(scoreDiff),
                      "guaranteedReal": sum(c == "guaranteed" for c, _ in real.values()),
                      "guaranteedEmulator": sum(a == 0 for a, _ in emu.values()),
                      "first": (missing + classDiff + scoreDiff)[:3],
                      "exact": not (missing or classDiff or scoreDiff)}
            results.append(result)
            print(json.dumps(result), flush=True)
    summary = {"cellArms": len(results), "exact": sum(r["exact"] for r in results),
               "riders": sum(r["ridersEmulator"] for r in results), "results": results}
    out.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in summary.items() if k != "results"}))
    return 0 if results and summary["exact"] == summary["cellArms"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
