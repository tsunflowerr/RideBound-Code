"""Check an unpacked final-run bundle against MANIFEST.tsv (ADR-089 GĐ5/GĐ6, 2026-11-04): every listed file exists
under E:/ with the listed size and SHA-256. Standard library only.

Usage: python -I -B verify_bundle.py <MANIFEST.tsv>
"""
import csv
import hashlib
import pathlib
import sys


def main() -> int:
    manifest = pathlib.Path(sys.argv[1])
    bad, n, total = [], 0, 0
    with open(manifest, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            path = pathlib.Path("/rb/") / row["path"]
            n += 1
            if not path.is_file() or path.stat().st_size != int(row["bytes"]):
                bad.append(("missing-or-size", row["path"]))
                continue
            digest = hashlib.sha256()
            with open(path, "rb") as source:
                for chunk in iter(lambda: source.read(1 << 20), b""):
                    digest.update(chunk)
            if digest.hexdigest() != row["sha256"]:
                bad.append(("sha256", row["path"]))
            total += int(row["bytes"])
    print(f"{n} files, {total / 2**30:.2f} GiB checked; {len(bad)} problems")
    for kind, path in bad[:20]:
        print(f"  {kind}: {path}")
    return 0 if not bad and n else 1


if __name__ == "__main__":
    raise SystemExit(main())
