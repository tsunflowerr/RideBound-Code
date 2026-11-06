"""Pack the Linux overlay of the final-run bundle (2026-11-04). Refuses to overwrite.

The overlay holds only what the Linux run adds to the unchanged Windows bundle (ridebound-final-v2.zip): bundled Python
3.10.22 (archive, offline wheels, pins), the .NET 10.0.9 Linux runtime archive, the Linux job tools, the Linux job
configurations and job lists, and the final-linux scripts. Entries are relative to E:/ (unpacked under /rb on Linux),
regular files only, owner root, sorted. Writes into E:/RideBoundData/final-v2-linux-pack/:
  ridebound-final-v2-linux.tar.gz   the overlay
  MANIFEST-linux.tsv                path, bytes, sha256 of every overlay file (as MANIFEST.tsv; verify_bundle_linux.py)
  run_linux.sh, RUN-LINUX.md        the run script and the instructions (also inside the overlay)
  SHA256SUMS                        the files to upload, including ../final-v2-pack/{ridebound-final-v2.zip,MANIFEST.tsv}
Usage: python -I -B pack_linux.py
"""
import csv
import gzip
import hashlib
import pathlib
import shutil
import tarfile

E = pathlib.Path("E:/")
OUT = E / "RideBoundData/final-v2-linux-pack"
BASE = E / "RideBoundData/final-v2-pack"
FINAL_LINUX = E / "Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02/final-linux"
FOLDERS = [
    E / "RideBoundData/final-v2-linux/python",
    E / "RideBoundData/final-v2-linux/dotnet",
    E / "RideBoundData/final-v2-linux/tools",
    E / "RideBoundData/research/fullsys-f14-v2/job-configs-linux",
    E / "RideBoundData/research/fullsys-f14-v2/jobs-linux",
    FINAL_LINUX,
]
SKIP_PARTS = {"__pycache__", "runtime"}  # runtime/: unpacked on the rented machine, never shipped


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        for chunk in iter(lambda: source.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    if OUT.exists():
        raise SystemExit(f"refused: {OUT} exists")
    files = []
    for folder in FOLDERS:
        for path in folder.rglob("*"):
            if path.is_symlink():
                raise SystemExit(f"refused: symbolic link {path}")
            if path.is_file() and not SKIP_PARTS.intersection(path.relative_to(folder).parts):
                files.append(path)
    files = sorted(set(files), key=lambda p: p.relative_to(E).as_posix())
    shell = [p for p in files if p.suffix == ".sh"]
    for path in shell:
        if b"\r\n" in path.read_bytes():
            raise SystemExit(f"refused: CRLF line ends in {path}")
    OUT.mkdir(parents=True)
    rows = []
    overlay = OUT / "ridebound-final-v2-linux.tar.gz"
    with open(overlay, "xb") as raw, gzip.GzipFile(fileobj=raw, mode="wb", compresslevel=6, mtime=0) as zipped, \
            tarfile.open(fileobj=zipped, mode="w", format=tarfile.PAX_FORMAT) as tar:
        for path in files:
            name = path.relative_to(E).as_posix()
            info = tar.gettarinfo(str(path), arcname=name)
            info.uid = info.gid = 0
            info.uname = info.gname = "root"
            info.mode = 0o755 if path.suffix == ".sh" else 0o644
            with open(path, "rb") as source:
                tar.addfile(info, source)
            rows.append((name, path.stat().st_size, sha256(path)))
    with open(OUT / "MANIFEST-linux.tsv", "x", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(("path", "bytes", "sha256"))
        writer.writerows(rows)
    for name in ("run_linux.sh", "RUN-LINUX.md"):
        shutil.copyfile(FINAL_LINUX / name, OUT / name)
    sums = [(sha256(BASE / name), name) for name in ("ridebound-final-v2.zip", "MANIFEST.tsv")]
    sums += [(sha256(OUT / name), name) for name in ("ridebound-final-v2-linux.tar.gz", "MANIFEST-linux.tsv",
                                                      "run_linux.sh")]
    with open(OUT / "SHA256SUMS", "x", encoding="ascii", newline="\n") as handle:
        for digest, name in sums:
            handle.write(f"{digest}  {name}\n")
    print(f"{len(rows)} files, {sum(r[1] for r in rows) / 2**20:.1f} MiB -> {overlay} "
          f"({overlay.stat().st_size / 2**20:.1f} MiB)")
    for digest, name in sums:
        print(f"{digest}  {name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
