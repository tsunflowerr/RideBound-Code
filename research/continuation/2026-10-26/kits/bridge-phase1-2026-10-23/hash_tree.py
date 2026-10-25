"""sha256 and size of every file under the given roots, as a sorted TSV (path, bytes, sha256).

Used around the NTFS transparent compression of tonight's job outputs (PHASE1-LOG P8): the listing before and after
must be identical, which shows that compression changed no byte.
Usage: python -B hash_tree.py <out.tsv> <root> [<root> ...]
"""
import hashlib
import pathlib
import sys

out = pathlib.Path(sys.argv[1])
rows = []
for root in map(pathlib.Path, sys.argv[2:]):
    for path in sorted(p for p in root.rglob("*") if p.is_file()):
        digest = hashlib.sha256()
        with open(path, "rb") as handle:
            for block in iter(lambda: handle.read(1 << 22), b""):
                digest.update(block)
        rows.append(f"{path.as_posix()}\t{path.stat().st_size}\t{digest.hexdigest()}")
with open(out, "x", encoding="utf-8", newline="\n") as handle:
    handle.write("\n".join(rows) + "\n")
print(f"{len(rows)} files, {sum(int(r.split(chr(9))[1]) for r in rows) / 2**30:.2f} GiB -> {out}")
