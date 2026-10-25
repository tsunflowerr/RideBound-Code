"""Write (or verify) a SHA-256 manifest of a published Runner directory.

Line format as runner-deadline-v1.sha256.tsv: `relative/path<TAB>sha256<TAB>bytes`, sorted by path
(ordinal, forward slashes). Header: `# tree-sha256 <hex> files <n>`, where the tree hash is the
SHA-256 of the UTF-8 body lines joined by "\n" (this definition is stated here because the Tier 2
file does not state its own).

Usage: python -B runner_manifest.py write|verify <runner_dir> <manifest.tsv>
"""
from __future__ import annotations

import hashlib
import pathlib
import sys


def body(root: pathlib.Path) -> list[str]:
    lines = []
    for path in sorted(root.rglob("*"), key=lambda p: p.relative_to(root).as_posix()):
        if path.is_file():
            data = path.read_bytes()
            lines.append(f"{path.relative_to(root).as_posix()}\t{hashlib.sha256(data).hexdigest()}\t{len(data)}")
    return lines


def main() -> int:
    if len(sys.argv) != 4 or sys.argv[1] not in ("write", "verify"):
        print(__doc__)
        return 2
    mode, root, manifest = sys.argv[1], pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3])
    lines = body(root)
    tree = hashlib.sha256("\n".join(lines).encode("utf-8")).hexdigest()
    text = f"# tree-sha256 {tree} files {len(lines)}\n" + "\n".join(lines) + "\n"
    if mode == "write":
        with open(manifest, "x", encoding="utf-8", newline="\n") as handle:  # never overwrite
            handle.write(text)
        print(f"wrote {manifest}: tree-sha256 {tree} files {len(lines)}")
        return 0
    same = manifest.read_text(encoding="utf-8") == text
    print(f"{'OK' if same else 'MISMATCH'} tree-sha256 {tree} files {len(lines)}")
    return 0 if same else 1


if __name__ == "__main__":
    raise SystemExit(main())
