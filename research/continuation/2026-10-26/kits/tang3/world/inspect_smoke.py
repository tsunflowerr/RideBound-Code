"""Diagnose the T3.5 smoke comparison: where are the frames without simTimeMs, and is the whole
prefix before the first frame at or after the split identical? Read-only."""
from __future__ import annotations

import collections

import run_world_smoke as smoke

a = list(smoke.frames(smoke.OUT_ROOT / "world-a" / "transcript-00.ndjson"))
b = list(smoke.frames(smoke.OUT_ROOT / "world-b" / "transcript-00.ndjson"))
untimed = [(i, f[1], f[3]) for i, f in enumerate(a) if f[2] is None]
print("untimed frames in world-a (index, direction, type):",
      collections.Counter((d, t) for _, d, t in untimed))
print("first 3 untimed:", untimed[:3], "last 3 untimed:", untimed[-3:])

cut_a = next(i for i, f in enumerate(a) if f[2] is not None and f[2] >= smoke.SPLIT_MS)
cut_b = next(i for i, f in enumerate(b) if f[2] is not None and f[2] >= smoke.SPLIT_MS)
print(f"first frame at or after the split: world-a index {cut_a}, world-b index {cut_b}")
same_prefix = cut_a == cut_b and [f[0] for f in a[:cut_a]] == [f[0] for f in b[:cut_b]]
print(f"identical prefix before the first frame at or after the split: {same_prefix}")
late_early = [i for i, f in enumerate(a[cut_a:], start=cut_a) if f[2] is not None and f[2] < smoke.SPLIT_MS]
print(f"timed frames before the split that appear after the cut: {len(late_early)}")
first_diff = next(i for i, (x, y) in enumerate(zip(a, b)) if x[0] != y[0])
print(f"first differing frame index {first_diff}: {a[first_diff][1:]} / {b[first_diff][1:]}")
print("frames around the cut (index, direction, simTimeMs, type, same):")
for i in range(max(0, cut_a - 3), cut_a + 3):
    print(" ", i, a[i][1], a[i][2], a[i][3], a[i][0] == b[i][0])
