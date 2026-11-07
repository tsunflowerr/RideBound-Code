from __future__ import annotations


class AdapterFailure(RuntimeError):
    """Fail-closed adapter error with a stable machine-readable code and path."""

    def __init__(self, code: str, path: str, detail: str) -> None:
        super().__init__(f"{code} at {path}: {detail}")
        self.code = code
        self.path = path
        self.detail = detail

