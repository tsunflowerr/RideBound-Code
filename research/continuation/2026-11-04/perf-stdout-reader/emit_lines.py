"""Child process for the stdout-reading benchmark: writes deterministic NDJSON-like lines.

Usage: emit_lines.py <line_bytes> <count>
Each line is <line_bytes> bytes of seeded pseudo-random JSON-safe text followed by LF.
The same arguments always produce the same bytes.
"""
import random
import sys

size, count = int(sys.argv[1]), int(sys.argv[2])
rng = random.Random(20261104)
alphabet = b'abcdefghijklmnopqrstuvwxyz0123456789{}[]:,"'
block = bytes(rng.choice(alphabet) for _ in range(1 << 20))
out = sys.stdout.buffer
for index in range(count):
    prefix = str(index).encode() + b":"
    body = (block * (size // len(block) + 1))[: size - len(prefix)]
    out.write(prefix + body + b"\n")
out.flush()
