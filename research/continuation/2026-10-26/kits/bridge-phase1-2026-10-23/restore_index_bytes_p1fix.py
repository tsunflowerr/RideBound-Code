"""Make the research worktree byte-identical to the committed blobs.

Why: `git worktree add` on this machine checks out with the system-level
core.autocrlf=true, and `.gitattributes` only pins eol=lf for some extensions.
So *.ndjson, *.hex, *.py, ... land as CRLF in the new worktree, while the
committed blobs (and the main working tree the 908/908 baseline was recorded on)
are LF. Golden-byte tests then fail for an environmental reason.

This script only touches C:/Code/RideBound-p1fix. For every tracked path it
writes exactly the index blob bytes, then re-hashes the file without filters and
checks it equals the blob id. Nothing is deleted.

Usage: python restore_index_bytes.py [--check]
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

WORKTREE = Path(r"C:/Code/RideBound-p1fix")


def git(*args: str, data: bytes | None = None) -> bytes:
    return subprocess.run(
        ["git", "-C", str(WORKTREE), *args],
        input=data, check=True, capture_output=True,
    ).stdout


def index_entries() -> list[tuple[str, str, str]]:
    raw = git("ls-files", "-s", "-z")
    out = []
    for record in raw.split(b"\0"):
        if not record:
            continue
        meta, path = record.split(b"\t", 1)
        mode, sha, stage = meta.decode().split()
        out.append((mode, sha, path.decode("utf-8")))
    return out


def main() -> int:
    check_only = "--check" in sys.argv
    entries = [e for e in index_entries() if e[0] in ("100644", "100755")]
    shas = "\n".join(sha for _, sha, _ in entries).encode() + b"\n"
    # Batch-read every blob in one process.
    proc = subprocess.run(
        ["git", "-C", str(WORKTREE), "cat-file", "--batch"],
        input=shas, check=True, capture_output=True,
    )
    stream = proc.stdout
    pos = 0
    blobs: dict[str, bytes] = {}
    for _, sha, _ in entries:
        header_end = stream.index(b"\n", pos)
        header = stream[pos:header_end].decode().split()
        assert header[0] == sha and header[1] == "blob", header
        size = int(header[2])
        start = header_end + 1
        blobs[sha] = stream[start:start + size]
        pos = start + size + 1

    rewritten = mismatched = 0
    for _, sha, rel in entries:
        target = WORKTREE / rel
        want = blobs[sha]
        have = target.read_bytes()
        if have != want:
            mismatched += 1
            if not check_only:
                target.write_bytes(want)
                rewritten += 1

    # Independent verification: hash the files as they are on disk, no filters.
    paths = "\n".join(rel for _, _, rel in entries).encode() + b"\n"
    hashed = git("hash-object", "--no-filters", "--stdin-paths", data=paths).decode().split()
    bad = [rel for (_, sha, rel), got in zip(entries, hashed) if sha != got]
    print(f"tracked={len(entries)} differing_before={mismatched} "
          f"rewritten={rewritten} hash_mismatch_after={len(bad)}")
    for rel in bad[:20]:
        print("  still differs:", rel)
    return 0 if (check_only or not bad) else 1


if __name__ == "__main__":
    raise SystemExit(main())
