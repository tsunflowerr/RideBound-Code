"""Read a job's evidence.json.gz (finalize_job.py) in place of its transcript (ADR-089 GĐ5/GĐ6, 2026-11-04).

install() wraps the five readers the comparison tools use, so the tools themselves stay unchanged:
  audit_vep.audit(transcript), audit_vep.load(transcript), bridge_riders.event_times(transcript),
  moves_plan.count(folder), compare_shadow.last_allocations(transcript)
A job folder is read from its evidence when it has no transcript-00.ndjson(.gz) but has evidence.json.gz whose SHA-256
matches EVIDENCE.sha256; otherwise the original reader runs on the transcript. only_evidence(folder) forces the evidence
for one folder (finalize_job.py's read-back check). Import after compare_via_full (it patches the same modules).
"""
import contextlib
import gzip
import hashlib
import json
import os
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE))
import compare_via_full as C  # noqa: E402
import compare_shadow  # noqa: E402

sys.path.insert(0, str(C.PHASE1))
import moves_plan  # noqa: E402

audit_vep, bridge_riders = C.audit_vep, C.bridge_riders
EVIDENCE = "evidence.json.gz"
_forced: set = set()
_cache: dict = {}
_installed = False


def _folder(path) -> pathlib.Path:
    path = pathlib.Path(path)
    return (path.parent if path.name.startswith("transcript-00") else path).resolve()


def _from_evidence(folder: pathlib.Path) -> bool:
    if folder in _forced:
        return True
    # RB_PREFER_EVIDENCE=1: read the evidence whenever it exists, even next to a kept transcript (finalize_job.py has
    # already checked that the evidence gives the same values as the transcript).
    if os.environ.get("RB_PREFER_EVIDENCE") == "1" and (folder / EVIDENCE).exists():
        return True
    has_transcript = (folder / "transcript-00.ndjson.gz").exists() or (folder / "transcript-00.ndjson").exists()
    return not has_transcript and (folder / EVIDENCE).exists()


def load_evidence(folder: pathlib.Path) -> dict:
    folder = folder.resolve()
    if folder not in _cache:
        data = (folder / EVIDENCE).read_bytes()
        sealed = (folder / "EVIDENCE.sha256").read_text(encoding="utf-8").split()[0]
        if hashlib.sha256(data).hexdigest() != sealed:
            raise SystemExit(f"{folder}: evidence.json.gz does not match EVIDENCE.sha256")
        value = json.loads(gzip.decompress(data))
        if value.get("format") != "ridebound-job-evidence-v1" or value.get("job") != folder.name:
            raise SystemExit(f"{folder}: not this job's evidence")
        _cache.clear()
        _cache[folder] = value
    return _cache[folder]


def install() -> None:
    global _installed
    if _installed:
        return
    original = {
        "audit": audit_vep.audit, "load": audit_vep.load, "event_times": bridge_riders.event_times,
        "count": moves_plan.count, "shadow": compare_shadow.last_allocations,
    }

    def audit(path):
        folder = _folder(path)
        if not _from_evidence(folder):
            return original["audit"](path)
        e = load_evidence(folder)["audit"]
        return [tuple(row) for row in e["table"]], e["summary"]

    def load(path):
        folder = _folder(path)
        if not _from_evidence(folder):
            return original["load"](path)
        return None, None, load_evidence(folder)["finalOnlineState"]

    def event_times(path):
        folder = _folder(path)
        if not _from_evidence(folder):
            return original["event_times"](path)
        e = load_evidence(folder)["events"]
        return e["boarded"], e["alighted"]

    def count(folder):
        resolved = _folder(folder)
        if not _from_evidence(resolved):
            return original["count"](folder)
        return load_evidence(resolved)["moves"]

    def shadow(path):
        folder = _folder(path)
        if not _from_evidence(folder):
            return original["shadow"](path)
        return load_evidence(folder)["shadowAllocations"]

    audit_vep.audit, audit_vep.load, bridge_riders.event_times = audit, load, event_times
    moves_plan.count, compare_shadow.last_allocations = count, shadow
    _installed = True


@contextlib.contextmanager
def only_evidence(folder: pathlib.Path):
    install()
    folder = pathlib.Path(folder).resolve()
    _forced.add(folder)
    try:
        yield
    finally:
        _forced.discard(folder)
        _cache.clear()
