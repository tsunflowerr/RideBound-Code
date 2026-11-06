"""Pack the F16 overlay for the rented Linux machine (PLAN-F16.md, 2026-11-05). Same form as pack_linux.py: entries relative
to E:/ (unpacked under /rb), regular files only, owner root, sorted, mtime-0 gzip; refuses to overwrite.

Holds only what the F16 run adds to the F14 bundle already unpacked on that machine: the F16 fixtures, worlds, drivers,
Linux configurations and job lists, the sealed predictions (rider rows and counts, Shadow inputs, drop sequences, timed
sequences), the plan and the three seal files, and run_f16_linux.sh. Writes into E:/RideBoundData/final-f16-linux-pack/:
  ridebound-f16-linux.tar.gz, MANIFEST-f16.tsv (path, bytes, sha256; verify_bundle_linux.py), run_f16_linux.sh,
  SHA256SUMS-F16 (the three files to upload besides SHA256SUMS-F16 itself)
Usage: python -I -B pack_f16.py
"""
import csv
import gzip
import hashlib
import pathlib
import shutil
import tarfile

E = pathlib.Path("E:/")
OUT = E / "RideBoundData/final-f16-linux-pack"
DATA = E / "RideBoundData/research/fullsys-f16-v2"
KIT = E / "Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02"
FOLDERS = [DATA / name for name in ("fixtures", "worlds", "drivers", "drivers-run", "job-configs-linux", "jobs-linux",
                                    "predictions", "predictions-timed")]
SINGLE = [DATA / "PLAN-F16.md", DATA / "PLAN-F16.sha256",
          KIT / "fullsys/FULLSYS-F16-PREDICTIONS.sha256", KIT / "fullsys/FULLSYS-F16-SEQUENCES.sha256",
          KIT / "fullsys/FULLSYS-F16-TIMED.sha256", KIT / "final-linux/run_f16_linux.sh"]


def sha256(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        for chunk in iter(lambda: source.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    if OUT.exists():
        raise SystemExit(f"refused: {OUT} exists")
    files = list(SINGLE)
    for folder in FOLDERS:
        if not folder.is_dir():
            raise SystemExit(f"missing folder {folder}")
        for path in folder.rglob("*"):
            if path.is_symlink():
                raise SystemExit(f"refused: symbolic link {path}")
            if path.is_file() and "__pycache__" not in path.parts:
                files.append(path)
    for path in files:
        if not path.is_file():
            raise SystemExit(f"missing {path}")
    files = sorted(set(files), key=lambda p: p.relative_to(E).as_posix())
    for path in files:
        if path.suffix == ".sh" and path.name != "PLAN-F16.sha256" and b"\r\n" in path.read_bytes():
            raise SystemExit(f"refused: CRLF line ends in {path}")
    OUT.mkdir(parents=True)
    rows = []
    overlay = OUT / "ridebound-f16-linux.tar.gz"
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
    with open(OUT / "MANIFEST-f16.tsv", "x", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(("path", "bytes", "sha256"))
        writer.writerows(rows)
    shutil.copyfile(KIT / "final-linux/run_f16_linux.sh", OUT / "run_f16_linux.sh")
    sums = [(sha256(OUT / name), name) for name in ("ridebound-f16-linux.tar.gz", "MANIFEST-f16.tsv", "run_f16_linux.sh")]
    with open(OUT / "SHA256SUMS-F16", "x", encoding="ascii", newline="\n") as handle:
        for digest, name in sums:
            handle.write(f"{digest}  {name}\n")
    print(f"{len(rows)} files, {sum(r[1] for r in rows) / 2**20:.1f} MiB -> {overlay} "
          f"({overlay.stat().st_size / 2**20:.1f} MiB)")
    for digest, name in sums:
        print(f"{digest}  {name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
