from __future__ import annotations

import base64
import gzip
import hashlib
import pathlib
import threading

from .errors import AdapterFailure
from .mapping import canonical_json_bytes


class ProtocolTranscriptRecorder:
    """Append-only exact-byte transcript shared across Runner restarts.

    ADR-089: a path ending in .gz is written gzip-compressed, with the same records. A full-node Via job sends about
    16 travel snapshots of about 140 MB, so its plain transcript would be about 3 GB.
    """

    def __init__(self, path: pathlib.Path) -> None:
        self.path = path.resolve()
        try:
            self._target = (
                gzip.open(self.path, "xb", compresslevel=6)
                if self.path.suffix == ".gz"
                else self.path.open("xb")
            )
        except OSError as exc:
            raise AdapterFailure(
                "RBWP7_TRANSCRIPT_OPEN_FAILED",
                "$.operator.ridebound_transcript_path",
                str(exc),
            ) from exc
        self._ordinal = 0
        self._lock = threading.Lock()
        self._closed = False

    def record(self, direction: str, frame: bytes) -> None:
        if direction not in {"adapterToRunner", "runnerToAdapter"}:
            raise ValueError("unknown transcript direction")
        if not isinstance(frame, bytes) or not frame:
            raise ValueError("transcript frame must be non-empty bytes")
        with self._lock:
            if self._closed:
                raise AdapterFailure(
                    "RBWP7_TRANSCRIPT_STATE_INVALID",
                    "$.transcript",
                    "closed",
                )
            self._ordinal += 1
            record = {
                "schemaVersion": "1.0.0",
                "ordinal": self._ordinal,
                "direction": direction,
                "frameLengthBytes": len(frame),
                "frameSha256": hashlib.sha256(frame).hexdigest(),
                "frameBase64": base64.b64encode(frame).decode("ascii"),
            }
            try:
                self._target.write(canonical_json_bytes(record) + b"\n")
                self._target.flush()
            except OSError as exc:
                raise AdapterFailure(
                    "RBWP7_TRANSCRIPT_WRITE_FAILED",
                    "$.transcript",
                    str(exc),
                ) from exc

    def close(self) -> None:
        with self._lock:
            if self._closed:
                return
            try:
                self._target.flush()
                self._target.close()
            except OSError as exc:
                raise AdapterFailure(
                    "RBWP7_TRANSCRIPT_CLOSE_FAILED",
                    "$.transcript",
                    str(exc),
                ) from exc
            finally:
                self._closed = True
