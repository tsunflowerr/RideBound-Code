"""Repository inventory exactly as the run preflight computes it.

Copied verbatim (logic) from actual_fleetpy_medium_preflight.py:726-781 (_git_head_commit,
_repository_inventory_sha256) in C:/Code/RideBound-g2, so a phase-1 driver can freeze the value the preflight will
check. Self-check: E:/Code/RideBound-deadline must give 0da63eaf... (every earlier bridge job).

Usage: python -B inventory.py <repository> [<repository> ...]
"""
import hashlib
import os
import pathlib
import subprocess
import sys


def git_head_commit(repository):
    completed = subprocess.run(["git", "-C", str(repository), "rev-parse", "HEAD"], check=True,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="ascii", timeout=10)
    return completed.stdout.strip()


def repository_inventory_sha256(repository):
    head = git_head_commit(repository).encode("ascii")
    completed = subprocess.run(["git", "-C", str(repository), "ls-files", "-z", "--cached", "--others",
                                "--exclude-standard"], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               timeout=30)
    relative_paths = sorted(value for value in completed.stdout.split(b"\0") if value)
    if len(relative_paths) != len(set(relative_paths)):
        raise RuntimeError("repository inventory contains duplicate paths")
    digest = hashlib.sha256(b"RideBound.Wp9RepositoryInventory.v1\0")
    digest.update(len(head).to_bytes(8, "big"))
    digest.update(head)
    for relative_bytes in relative_paths:
        relative = pathlib.Path(os.fsdecode(relative_bytes))
        path = (repository / relative).resolve()
        path.relative_to(repository)
        if not path.is_file():
            raise RuntimeError(f"repository inventory file is missing: {relative}")
        digest.update(len(relative_bytes).to_bytes(8, "big"))
        digest.update(relative_bytes)
        digest.update(path.stat().st_size.to_bytes(8, "big"))
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
    return digest.hexdigest()


if __name__ == "__main__":
    for argument in sys.argv[1:]:
        repository = pathlib.Path(argument).resolve()
        print(repository_inventory_sha256(repository), git_head_commit(repository), repository)
