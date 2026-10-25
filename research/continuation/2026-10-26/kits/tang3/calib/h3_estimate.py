"""Planning estimate for T3.8 / H3 on DEVELOPMENT outputs only (2026-09-30). Not a calibration, not a result.

Per episode j (one run of C-30) the H3 score of the plan (KE-HOACH §8.3) is S_j = max over promised riders of X_r,
X_r = sum |x| over the rider's drop revisions (x = e - v, the change seen when the route is kept), because
V_r <= C_r + X_r and C_r <= beta by construction. The T3.1 usefulness proposal is "half the 90th percentile of V_r
under U on the development set" (V_r = sum |z| over drop revisions). This script prints both per episode.
Episodes: the 8 cells 07:00-09:00 (task B) and the two measured-day cells of the sweep (W5, 08:00-10:00 and 17:00-19:00).
Usage: python -B h3_estimate.py <new-log>   (mode "x")
"""
from __future__ import annotations

import pathlib
import statistics
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent
for sub in ("sweep", "evidence", "w07"):
    sys.path.insert(0, str(TANG3 / sub))
import audit_vep  # noqa: E402
import run_sweep  # noqa: E402
import run_w07  # noqa: E402


def per_rider(transcript: pathlib.Path):
    table, summary = audit_vep.audit(transcript)
    x, z = {}, {}
    for request, coordinate, _, kind, _, _, _, _, xv, _, zv, closed in table:
        if coordinate != "drop":
            continue
        x.setdefault(request, 0)
        z.setdefault(request, 0)
        if kind != "initialPromise" and not closed:
            x[request] += abs(xv)
            z[request] += abs(zv)
    if sum(z.values()) != summary["sumDropV"]:
        raise SystemExit(f"{transcript}: V does not match the oracle")
    return x, z


def p90(values):
    ordered = sorted(values)
    return ordered[max(0, -(-9 * len(ordered) // 10) - 1)]  # nearest-rank 90th percentile


def main(log_path: str) -> int:
    episodes = [(cell, run_w07.OUT_ROOT / f"C-30-{cell}", run_w07.OUT_ROOT / f"U-{cell}") for cell in run_w07.CELLS]
    for cell in ("d20181112-s10-r1-w08", "d20181112-s10-r1-w17"):
        episodes.append((f"W5-{cell}", run_sweep.OUT_ROOT / run_sweep.effective(f"C-30-W5-{cell}"),
                         run_sweep.OUT_ROOT / run_sweep.effective(f"U-W5-{cell}")))
    lines = ["# h3_estimate.py (planning estimate on development outputs; not a calibration)",
             "episode | C-30: S_j = max_r X_r (s), riders with X_r > 60 s | U: p90 of V_r (s), half of it (s)"]
    scores, halves = [], []
    for name, c_dir, u_dir in episodes:
        x, _ = per_rider(c_dir / "transcript-00.ndjson")
        _, zu = per_rider(u_dir / "transcript-00.ndjson")
        s = max(x.values()) / 1000
        half = p90(list(zu.values())) / 2000
        scores.append((name, s))
        halves.append(half)
        lines.append(f"{name} | {s:7.1f}  {sum(1 for v in x.values() if v > 60000):>3}/{len(x)} | "
                     f"{p90(list(zu.values())) / 1000:7.1f}  {half:6.1f}")
    w07 = [s for n, s in scores if n.endswith("w07")]
    lines.append(f"07-09h episodes: S_j min {min(w07):.1f} median {statistics.median(w07):.1f} max {max(w07):.1f} s")
    lines.append(f"after-peak episodes (W5 08-10, 17-19): S_j {[round(s, 1) for n, s in scores if not n.endswith('w07')]} s")
    lines.append(f"usefulness proposal (half p90 of V_r under U): median over episodes {statistics.median(halves):.1f} s, "
                 f"range {min(halves):.1f}-{max(halves):.1f} s")
    text = "\n".join(lines) + "\n"
    with open(log_path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    raise SystemExit(main(sys.argv[1]))
