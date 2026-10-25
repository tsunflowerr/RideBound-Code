"""Copy the October research kits (scripts, configs, small results) into the push worktree (owner order 2026-10-26).

Owner: "commit mọi thứ bao gồm code C#, script ... và những thứ cần thiết không quá nặng mà cần thiết nhất lên github
project RideBound-Code". The C# code is already on the branch (research/p1-dwell ancestry). This copies, never moves:
  * kept: code (.py .js .mjs .sql .sh .ps1), text (.md .txt .tsv .csv .json .yaml .yml .sha256 .patch) up to 3 MiB;
  * skipped: *.log, *.bak, *.lock, STOP, PROGRESS* (live state), folders logs*, __pycache__, .git, node_modules,
    and every file over 3 MiB (run outputs stay in E:/RideBoundData; rebuild them with the scripts).
The company emulator (wf15 runner, emulator copies) is NOT copied: it came from the company machine and is not in these
kits; the scripts reference it by path. Each copied text file is scanned for e-mail addresses and secret-like strings;
any hit stops the copy. Writes COPY-MANIFEST.tsv (path, bytes, sha256, source). Never overwrites.

Usage: python -I copy_kits.py <destination folder>
"""
from __future__ import annotations

import hashlib
import pathlib
import re
import shutil
import sys

SRC = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad")
KITS = [
    "via-bridge-2026-10-22",
    "g2-2026-10-23",
    "bridge-phase1-2026-10-23",
    "p5-via-adapter-2026-10-25",
    "p7-tlc-2026-10-25",
    "identity-2026-10-25",
    "breakeven-2026-10-26",
    "via-real-2026-10-26",
]
# tang3: only what the October kits import or read (runner manifest check, evidence extractor, world wrappers,
# WP4 harness configs, panel-A regress inputs); its run outputs (t38, w07x, vm, stage1b results) stay local.
TANG3_PARTS = ["runner_manifest.py", "inspect_checkpoint.py", "configs", "evidence", "world", "calib",
               "stage1b/harness", "runner-tier3-v1.sha256.tsv", "runner-tier3-v2.sha256.tsv"]
# Parts copied without their sub-folders (stage1b/harness keeps its configs and scripts at the top level; the
# analysis-* sub-folders are self-test outputs, about 30 MiB).
TANG3_SHALLOW = {"stage1b/harness"}
CODE = {".py", ".js", ".mjs", ".sql", ".sh", ".ps1"}
TEXT = {".md", ".txt", ".tsv", ".csv", ".json", ".yaml", ".yml", ".sha256", ".patch"}
MAX_BYTES = 3 * 2**20
SKIP_DIRS = {"__pycache__", ".git", "node_modules"}
SKIP_NAMES = {"STOP"}
# Local part must start with a letter or digit, so a Python decorator line in a patch ("+@unittest.skipUnless") is not
# read as an address (a decorator has nothing before "@"). Top-level domain of 2-6 letters, any case (review R1 #8).
EMAIL = re.compile(rb"(?<![\w.+-])[A-Za-z0-9][A-Za-z0-9._%+-]*@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,6}\b")
SECRET = re.compile(rb"(?i)(ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}"
                    rb"|xox[bpas]-[A-Za-z0-9-]{10,}|bearer\s+[A-Za-z0-9._-]{20,}|password\s*[:=]|api[_-]?key\s*[:=]"
                    rb"|BEGIN [A-Z ]*PRIVATE KEY)")
# Addresses that are public citations or protocol placeholders, not personal data.
EMAIL_ALLOWED = {b"noreply@anthropic.com"}


def wanted(path: pathlib.Path) -> bool:
    """path is absolute; the folder test looks only at the part below SRC."""
    relative = path.relative_to(SRC)
    if any(part in SKIP_DIRS or part.lower().startswith("logs") for part in relative.parts[:-1]):
        return False
    name = path.name
    if name in SKIP_NAMES or name.startswith("PROGRESS") or name.endswith((".log", ".bak", ".lock")) or ".bak" in name:
        return False
    if path.suffix.lower() not in CODE | TEXT:
        return False
    return path.stat().st_size <= MAX_BYTES


def main(argv: list[str]) -> int:
    if len(argv) != 1:
        print(__doc__)
        return 2
    dest = pathlib.Path(argv[0])
    if dest.exists():
        raise SystemExit(f"refused: {dest} exists")
    sources: list[tuple[pathlib.Path, pathlib.Path]] = []
    for kit in KITS:
        root = SRC / kit
        for path in sorted(p for p in root.rglob("*") if p.is_file()):
            if wanted(path):
                sources.append((path, dest / path.relative_to(SRC)))
    for part in TANG3_PARTS:
        root = SRC / "tang3" / part
        if root.is_file():
            files = [root]
        elif part in TANG3_SHALLOW:
            files = sorted(p for p in root.iterdir() if p.is_file())
        else:
            files = sorted(p for p in root.rglob("*") if p.is_file())
        for path in files:
            if wanted(path):
                sources.append((path, dest / path.relative_to(SRC)))
    problems = []
    for path, _ in sources:
        data = path.read_bytes()
        for hit in set(EMAIL.findall(data)) - EMAIL_ALLOWED:
            problems.append(f"e-mail {hit.decode(errors='replace')} in {path}")
        for hit in set(SECRET.findall(data)):
            problems.append(f"secret-like {hit[:20]!r} in {path}")
    if problems:
        print("\n".join(problems))
        raise SystemExit("refused: personal or secret-like strings found; nothing copied")
    rows = ["path\tbytes\tsha256\tsource"]
    total = 0
    for path, target in sources:
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            raise SystemExit(f"refused: {target} exists")
        shutil.copyfile(path, target)
        data = target.read_bytes()
        if data != path.read_bytes():
            raise SystemExit(f"copy differs: {target}")
        total += len(data)
        rows.append(f"{target.relative_to(dest).as_posix()}\t{len(data)}\t{hashlib.sha256(data).hexdigest()}\t{path.as_posix()}")
    (dest / "COPY-MANIFEST.tsv").write_text("\n".join(rows) + "\n", encoding="utf-8", newline="\n")
    print(f"copied {len(sources)} files, {total / 2**20:.1f} MiB, into {dest}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
