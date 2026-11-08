"""Run drivers (ADR-089): a sealed driver plus routingEngine and transcriptCompression, nothing else changed.

Written down after the fact: via-full-v1/drivers-run/ was made by an inline command with this logic; check mode shows
that this script reproduces those two files byte for byte. Also makes the G3a drivers (same cell, both routing engines).

Usage: python -I make_run_drivers.py write <sealed driver> <out driver> <routingEngine> [<transcriptCompression>]
       python -I make_run_drivers.py check <sealed driver> <existing run driver> <routingEngine> [<compression>]
"""
import json
import pathlib
import sys

ENGINES = ("network-basic-v1", "complete-graph-direct-v1")
COMPRESSIONS = ("none", "gzip-v1")


def render(sealed: pathlib.Path, engine: str, compression: str) -> bytes:
    if engine not in ENGINES or compression not in COMPRESSIONS:
        raise SystemExit(f"unknown routingEngine or transcriptCompression: {engine} {compression}")
    driver = json.loads(sealed.read_text(encoding="utf-8"))
    if "routingEngine" in driver or "transcriptCompression" in driver:
        raise SystemExit(f"{sealed} already has run keys")
    driver["routingEngine"] = engine
    driver["transcriptCompression"] = compression
    return (json.dumps(driver, indent=2) + "\n").encode("utf-8")


def main() -> int:
    mode, sealed, target, engine = sys.argv[1], pathlib.Path(sys.argv[2]), pathlib.Path(sys.argv[3]), sys.argv[4]
    compression = sys.argv[5] if len(sys.argv) > 5 else "gzip-v1"
    data = render(sealed, engine, compression)
    if mode == "check":
        same = target.read_bytes() == data
        print(f"{target}: {'reproduced byte for byte' if same else 'DIFFERS'}")
        return 0 if same else 1
    if mode != "write" or target.exists():
        raise SystemExit(f"refused: mode {mode} or {target} exists")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    print(f"{target}: written")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
