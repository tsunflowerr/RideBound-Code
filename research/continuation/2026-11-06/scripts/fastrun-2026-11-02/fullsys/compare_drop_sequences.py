"""Per-rider published drop-ETA sequences of RideBound runs vs the sealed emulator sequences (ADR-089 GĐ4/GĐ6, 2026-11-04).

The report's headline metrics are the promise reversal (visRev60) and TV5 (metrics_sh.js:47-60). Both depend only on each
served rider's published drop-ETA sequence, so the gate compares that sequence rider by rider:
  real      the drop rows of the audit ledger table (tang3/evidence/audit_vep.audit, the reader compare_via_full.py patches
            for gzip transcripts): p of the initial promise, then p of every later drop entry in ledger order;
  emulator  fullsys-f14-drop-sequences.tsv (emulate_sequences.js; sealed);
  both      consecutive repeats dropped. rev60 and tv5 are recomputed from the sequence with the report's rules and summed
            over served riders per cell-arm.
Exact = same rider set and the same sequence for every rider.

Usage: python -I -B compare_drop_sequences.py <drop-sequences.tsv> <seal file> <out.json> --root <runs> [--root <runs> ...]
"""
import collections
import csv
import hashlib
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
import compare_via_full as C  # noqa: E402  (patches audit_vep.read_frames for gzip and big frames)

audit_vep = C.audit_vep


def dedupe(values):
    out = []
    for value in values:
        if not out or value != out[-1]:
            out.append(value)
    return out


def has_reversal(sequence, threshold=60_000):   # metrics_sh.hasReversal over z = consecutive changes
    last = 0
    for a, b in zip(sequence, sequence[1:]):
        z = b - a
        if abs(z) < threshold:
            continue
        sign = 1 if z > 0 else -1
        if last != 0 and sign != last:
            return True
        last = sign
    return False


def tv5(sequence):   # metrics_sh.js:58-59 with shown = p0
    shown, total = sequence[0], 0
    for p in sequence[1:]:
        if abs(p - shown) >= 5_000:
            total += abs(p - shown)
            shown = p
    return total


def real_sequences(folder: pathlib.Path):
    """Published drop ETAs per rider. The Runner also publishes a last revision at the drop itself (published at the
    drop time, p = that time); the emulator records no revision once the rider is dropped, so that entry is left out.
    Found by the tool test of 2026-11-04 (fullsys/tool-test-final-entry.txt): with it left out, 671/671 riders of two
    G3b jobs have identical sequences and TV5."""
    transcript = folder / "transcript-00.ndjson.gz"
    table, _ = audit_vep.audit(transcript)
    if table is None:
        raise SystemExit(f"{folder}: no final checkpoint")
    rows = collections.defaultdict(list)
    for r, coord, seq, kind, pub, v, e, p, x, cc, z, closed in table:
        if coord == "drop":
            rows[r].append((seq, kind, pub, p))
    out, at_drop = {}, 0
    for r, entries in rows.items():
        entries.sort()
        if entries[0][1] != "initialPromise":
            raise SystemExit(f"{folder}: {r} does not start with its initial promise")
        if len(entries) > 1 and entries[-1][1] == "revision" and entries[-1][2] == entries[-1][3]:
            entries = entries[:-1]
            at_drop += 1
        out[r] = dedupe([p for _, _, _, p in entries])
    return out, at_drop


def main() -> int:
    args = sys.argv[1:]
    roots = []
    while "--root" in args:
        i = args.index("--root")
        roots.append(pathlib.Path(args[i + 1]))
        del args[i:i + 2]
    expected_file, seal_file, out_file = (pathlib.Path(a) for a in args)
    if out_file.exists():
        raise SystemExit(f"refused: {out_file} exists")
    digest = hashlib.sha256(expected_file.read_bytes()).hexdigest()
    sealed = [l for l in seal_file.read_text(encoding="utf-8").splitlines() if l.endswith("*predictions/" + expected_file.name)]
    if not sealed or not sealed[0].startswith(digest):
        raise SystemExit(f"{expected_file.name} is not the sealed file")
    emulator = collections.defaultdict(dict)
    with open(expected_file, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            emulator[(row["role"], row["cell"])][row["requestId"]] = (int(row["served"]), json.loads(row["dropSequence"]))
    results = []
    for root in roots:
        for folder in sorted(p for p in root.iterdir() if (p / "summary.json").exists()):
            if json.loads((folder / "summary.json").read_text(encoding="utf-8")).get("status") != "pass":
                continue
            key = next((k for k in emulator if folder.name == f"{k[0]}-{k[1]}"), None)
            if key is None:
                continue
            (real, at_drop), emu = real_sequences(folder), emulator[key]
            differing = sorted(r for r in set(real) | set(emu) if real.get(r) != (emu.get(r) or (None, None))[1])
            served = [r for r, (s, _) in emu.items() if s]
            result = {
                "role": key[0], "cell": key[1], "riders": len(emu), "ridersReal": len(real),
                "sequencesIdentical": len(emu) - len(differing) if not set(real) ^ set(emu) else None,
                "differing": len(differing), "firstDifferences": differing[:3], "atDropRevisionsLeftOut": at_drop,
                "visRev60Emulator": sum(has_reversal(emu[r][1]) for r in served),
                "visRev60Real": sum(has_reversal(real[r]) for r in served if r in real),
                "tv5Emulator": sum(tv5(emu[r][1]) for r in served),
                "tv5Real": sum(tv5(real[r]) for r in served if r in real),
                "exact": not differing,
            }
            results.append(result)
            print(json.dumps(result), flush=True)
    summary = {"cellArms": len(results), "exact": sum(r["exact"] for r in results),
               "riders": sum(r["riders"] for r in results), "differing": sum(r["differing"] for r in results),
               "visRev60Emulator": sum(r["visRev60Emulator"] for r in results), "visRev60Real": sum(r["visRev60Real"] for r in results),
               "tv5Emulator": sum(r["tv5Emulator"] for r in results), "tv5Real": sum(r["tv5Real"] for r in results),
               "results": results}
    out_file.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in summary.items() if k != "results"}))
    return 0 if results and summary["exact"] == summary["cellArms"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
