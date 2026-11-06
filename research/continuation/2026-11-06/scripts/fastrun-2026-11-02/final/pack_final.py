"""Pack the final-run bundle (ADR-089 GĐ5/GĐ6, 2026-11-04) as one zip whose entries are paths relative to E:/, plus
MANIFEST.tsv (relative path, bytes, SHA-256 of every file). On the rented machine the zip is unpacked into a folder
that becomes E: (`subst E: <folder>`), so every absolute path in the configs and tools is the same as here.

What goes in (nothing else; no company-machine material, no emulator code, no run outputs except the reference evidence):
  final-v2/{BUILD.json, tools/, runner/, dotnet/, reference/} and repo/ = its tracked files + its .git
  FleetPy 1.0.2 (without __pycache__) and the Python environment fleetpy-1.0.2
  F14 inputs (fixtures, worlds, drivers, run drivers, job configs, job lists), sealed predictions and seal files
  the Shadow network (D.u16, network.json)
  the comparison/evidence tools at their own paths (TOOL_FILES)
Usage: python -I -B pack_final.py <out dir>      (refuses if the zip or manifest exists)
"""
import hashlib
import os
import re
import pathlib
import subprocess
import sys
import zipfile

E = pathlib.Path("E:/")
FINAL = E / "RideBoundData/final-v2"
DATA = E / "RideBoundData/research/fullsys-f14-v2"
SCRATCH = E / "Code/Report_INT3508/ridebound-scratchpad"
KIT = SCRATCH / "fastrun-2026-11-02"
TREES = [  # (folder, skip __pycache__)
    (FINAL / "tools", True), (FINAL / "runner", False), (FINAL / "dotnet", False), (FINAL / "reference", False),
    (FINAL / "repo" / ".git", False),
    (E / "RideBoundData/wp7/FleetPy-1.0.2", True), (E / "RideBoundData/wp7/envs/fleetpy-1.0.2", False),
    (DATA / "fixtures", False), (DATA / "worlds", False), (DATA / "drivers", False), (DATA / "drivers-run", False),
    (DATA / "job-configs-v2", False), (DATA / "jobs-bundle-v2", False), (DATA / "predictions", False),
    (DATA / "predictions-forecast-gate", False), (DATA / "predictions-timed-gate", False),
    (DATA / "predictions-timed", False),
    (E / "RideBoundData/research/via-full-v1/shadow-gate/manhattan-network", False),
    (KIT / "final", True),
]
# home-side build scripts: not needed on the rented machine (they also hold the name-scan pattern itself)
NOT_SHIPPED = {KIT / "final" / "assemble_final.py", KIT / "final" / "pack_final.py"}
TOOL_FILES = [
    FINAL / "BUILD.json",
    KIT / "compare_via_full.py",
    KIT / "fullsys/compare_fullsys.py", KIT / "fullsys/compare_drop_sequences.py", KIT / "fullsys/compare_shadow.py",
    KIT / "fullsys/eta_metrics.py", KIT / "fullsys/reversal_counts.py", KIT / "fullsys/finalize_job.py",
    KIT / "fullsys/evidence_reader.py", KIT / "fullsys/with_evidence.py",
    KIT / "fullsys/FULLSYS-F14-PREDICTIONS.sha256", KIT / "fullsys/FULLSYS-F14-SEQUENCES.sha256",
    KIT / "fullsys/FORECAST-GATE-PREDICTIONS.sha256", KIT / "fullsys/FULLSYS-F14-TIMED-GATE.sha256",
    KIT / "fullsys/FULLSYS-F14-TIMED.sha256", KIT / "fullsys/FULLSYS-F14-TIMED-GATE.addendum.txt",
    SCRATCH / "tang3/evidence/audit_vep.py", SCRATCH / "tang3/oracle/accounting.py",
    SCRATCH / "via-bridge-2026-10-22/bridge_riders.py", SCRATCH / "via-bridge-2026-10-22/g2_moves.py",
    SCRATCH / "bridge-phase1-2026-10-23/moves_plan.py", SCRATCH / "bridge-phase1-2026-10-23/inventory.py",
]
FORBIDDEN = ("company-intake", "company-emulator", "/sp/")
# Published copy (2026-11-06): the company names are not written in this public repository; set RB_COMPANY_NAMES_REGEX
# to the original byte pattern before running (sha256 of the unredacted file: scripts/HELD-BACK.tsv).
COMPANY_NAMES = re.compile(os.environ["RB_COMPANY_NAMES_REGEX"].encode(), re.IGNORECASE)


def company_name_hits(listing: list) -> list:
    """Every file of the bundle is scanned for the company's name (streamed, overlapping 64-byte windows)."""
    hits = []
    for path in listing:
        with open(path, "rb") as handle:
            tail = b""
            for chunk in iter(lambda: handle.read(1 << 22), b""):
                if COMPANY_NAMES.search(tail + chunk):
                    hits.append(path)
                    break
                tail = chunk[-64:]
    return hits


def sha(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def files() -> list:
    out = []
    tracked = subprocess.run(["git", "-C", str(FINAL / "repo"), "ls-files", "-z"], check=True,
                             stdout=subprocess.PIPE).stdout.split(b"\0")
    out += [FINAL / "repo" / os.fsdecode(p) for p in tracked if p]
    for folder, skip_cache in TREES:
        if not folder.exists():
            print(f"(absent, skipped) {folder}")
            continue
        for path in sorted(folder.rglob("*")):
            if path.is_file() and not (skip_cache and "__pycache__" in path.parts):
                out.append(path)
    out += [p for p in TOOL_FILES if p.exists()]
    out = sorted(set(out) - NOT_SHIPPED)
    bad = [p for p in out if any(f in p.as_posix() for f in FORBIDDEN)]
    if bad:
        raise SystemExit(f"forbidden paths in the bundle: {bad[:5]}")
    return out


def main() -> int:
    if sys.argv[1] == "--dry-run":   # list, size and scan only; nothing is written
        listing = files()
        hits = company_name_hits(listing)
        total = sum(p.stat().st_size for p in listing)
        print(f"{len(listing)} files, {total / 2**30:.2f} GiB unpacked; company-name hits: {len(hits)}")
        for path in hits[:20]:
            print("  hit:", path)
        return 0 if not hits else 1
    out_dir = pathlib.Path(sys.argv[1])
    out_dir.mkdir(parents=True, exist_ok=True)
    archive, manifest = out_dir / "ridebound-final-v2.zip", out_dir / "MANIFEST.tsv"
    for path in (archive, manifest):
        if path.exists():
            raise SystemExit(f"refused: {path} exists")
    listing = files()
    hits = company_name_hits(listing)
    if hits:
        raise SystemExit(f"company name found in {len(hits)} bundle files, e.g. {hits[:5]}")
    total = 0
    with open(manifest, "x", encoding="utf-8", newline="\n") as m, \
            zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as z:
        m.write("path\tbytes\tsha256\n")
        for path in listing:
            relative = path.relative_to(E).as_posix()
            size = path.stat().st_size
            m.write(f"{relative}\t{size}\t{sha(path)}\n")
            z.write(path, relative)
            total += size
    print(f"{len(listing)} files, {total / 2**30:.2f} GiB unpacked, zip {archive.stat().st_size / 2**30:.2f} GiB")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
