"""Mutation check for test_preflight_world_v1_3.py: each mutant of preflight_world_v1_3.py must make the test fail.
Copies the test and the mutated module into a temporary folder (this folder stays untouched).
Usage: python -B mutate_preflight_world_v1_3.py
"""
from __future__ import annotations

import os
import pathlib
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = (HERE / "preflight_world_v1_3.py").read_text(encoding="utf-8")
TEST = (HERE / "test_preflight_world_v1_3.py").read_text(encoding="utf-8")
MUTANTS = {
    "ratio looks one bin ahead": ("sum(m(b + j) for j in range(k))", "sum(m(b + j + 1) for j in range(k))"),
    "hold from bin 7": ("HOLD_BIN = 8 ", "HOLD_BIN = 7 "),
    "no leakage guard": ('    if base["day"] in days:\n', '    if False:\n'),
    "no K=1 shortcut, divide by k*m(b+1)": ("    if k == 1:\n        return 1.0\n", ""),
    "snapshot not scaled": ("return inner(version, [(origin, destination, seconds * r) for origin, destination, seconds in arcs])",
                            "return inner(version, arcs)"),
    "bin by rounding": ('b = int(state["time"] // BIN_S)', 'b = int(round(state["time"] / BIN_S))'),
    "profile from first day only": ("return tuple(sum(p[b] for p in profiles) / len(profiles) for b in range(HOLD_BIN + 1))",
                                    "return tuple(profiles[0][b] for b in range(HOLD_BIN + 1))"),
    "K range 1..9": ("not 1 <= k <= 8", "not 1 <= k <= 9"),
}
SECOND = {"no K=1 shortcut, divide by k*m(b+1)": ("/ (k * m(b))", "/ (k * m(b + 1))")}


def main() -> int:
    killed = 0
    for name, (old, new) in MUTANTS.items():
        assert SOURCE.count(old) == 1, name
        mutated = SOURCE.replace(old, new)
        if name in SECOND:
            a, b = SECOND[name]
            assert mutated.count(a) == 1, name
            mutated = mutated.replace(a, b)
        with tempfile.TemporaryDirectory() as tmp:
            (pathlib.Path(tmp) / "preflight_world_v1_3.py").write_text(mutated, encoding="utf-8")
            (pathlib.Path(tmp) / "test_preflight_world_v1_3.py").write_text(TEST, encoding="utf-8")
            env = dict(os.environ, PYTHONPATH=str(HERE), PYTHONIOENCODING="utf-8", PYTHONDONTWRITEBYTECODE="1")
            done = subprocess.run([sys.executable, "-B", str(pathlib.Path(tmp) / "test_preflight_world_v1_3.py")],
                                  capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
        dead = done.returncode != 0
        killed += dead
        last = (done.stdout.strip().splitlines() or ["(no output)"])[-1]
        print(f"{'KILLED' if dead else 'SURVIVED'}: {name} -> {last}")
    print(f"{killed}/{len(MUTANTS)} mutants killed")
    return 0 if killed == len(MUTANTS) else 1


if __name__ == "__main__":
    raise SystemExit(main())
