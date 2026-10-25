"""G2 job audit: real moves, decision counts, and the 1.3.0 solver evidence of every decision.

For each job folder given:
  moves     = ledger revisions whose published vehicle differs from the keep-route (exogenous) vehicle,
              from the final checkpoint (one per moved rider and decision);
  decisions = Runner decisions in the transcript; with solver evidence, how many carried relocation losses;
  evidence  = every decision's solver evidence checked by actual_fleetpy_medium_verify._verify_audited_solver_evidence
              (G2 worktree copy), which knows evidence version 1.3.0.

Usage: python -I -B g2_moves.py <job folder> [...]
"""

from __future__ import annotations

import base64
import collections
import hashlib
import importlib.util
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
VERIFY = pathlib.Path(r"C:/Code/RideBound-g2/simulators/fleetpy-ridebound/actual_fleetpy_medium_verify.py")


def frames(path: pathlib.Path):
    """(direction, decoded envelope); each frame's SHA-256 is checked, as in audit_vep.read_frames."""
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines()):
        record = json.loads(line)
        frame = base64.b64decode(record["frameBase64"])
        if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
            raise SystemExit(f"{path} line {number}: frame hash mismatch")
        yield record["direction"], json.loads(frame)


def load_verifier():
    spec = importlib.util.spec_from_file_location("medium_verify_g2", VERIFY)
    module = importlib.util.module_from_spec(spec)
    sys.path.insert(0, str(VERIFY.parent))
    spec.loader.exec_module(module)
    return module


def audit(folder: pathlib.Path, verifier) -> dict:
    transcript = folder / "transcript-00.ndjson"
    final = None
    decisions = 0
    versions = collections.Counter()
    with_relocation = 0
    evidence_errors = []
    for direction, envelope in frames(transcript):
        kind = envelope.get("messageType")
        if direction == "runnerToAdapter" and kind == "decision":
            decisions += 1
            solver = envelope.get("payload", {}).get("solver")
            if isinstance(solver, dict) and isinstance(solver.get("executionEvidence"), dict):
                evidence = solver["executionEvidence"]
                versions[evidence.get("evidenceVersion")] += 1
                if "relocationLosses" in evidence.get("generation", {}):
                    with_relocation += 1
                try:
                    verifier._verify_audited_solver_evidence(solver, decisions)
                except RuntimeError as failure:
                    evidence_errors.append(f"decision {decisions}: {failure}")
        elif direction == "runnerToAdapter" and kind == "checkpoint":
            final = envelope["payload"]["content"]["onlineState"]
    moves = []
    if final is not None:
        for history in final["commitmentLedger"]:
            for entry in history["entries"]:
                if entry["kind"] == "initialPromise":
                    continue
                before = entry["exogenousProjection"]["vehicleId"]
                after = entry["publishedPromise"]["projection"]["vehicleId"]
                if before != after:
                    moves.append((history["requestId"], before, after, entry["publishedPromise"]["publishedAtMs"]))
    return {
        "job": folder.name,
        "finalCheckpoint": final is not None,
        "moves": len(moves),
        "movedRiders": len({move[0] for move in moves}),
        "decisions": decisions,
        "evidenceVersions": dict(versions),
        "decisionsWithRelocationLosses": with_relocation,
        "evidenceErrors": evidence_errors[:5],
        "evidenceErrorCount": len(evidence_errors),
        "firstMoves": moves[:5],
    }


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    verifier = load_verifier()
    for name in sys.argv[1:]:
        print(json.dumps(audit(pathlib.Path(name), verifier)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
