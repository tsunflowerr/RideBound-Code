"""Mutation check for test_preflight_world_v1_4.py: each mutant of preflight_world_v1_4.py must make the test fail.
Copies the test and the mutated module into a temporary folder (this folder stays untouched).
Usage: python -B mutate_preflight_world_v1_4.py
"""
from __future__ import annotations

import os
import pathlib
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = (HERE / "preflight_world_v1_4.py").read_text(encoding="utf-8")
TEST = (HERE / "test_preflight_world_v1_4.py").read_text(encoding="utf-8")
MUTANTS = {
    "no growth after the current bin": ("weight = 1.0 if b == b0 else m(b) / m(b0)", "weight = 1.0"),
    "growth applied in the current bin too": ("weight = 1.0 if b == b0 else m(b) / m(b0)", "weight = m(b + 1) / m(b0)"),
    "hold ignored (no min with HOLD_BIN)": ("profile[min(b, v13.HOLD_BIN)]", "profile[min(b, len(profile) - 1)] * (1.01 if b > v13.HOLD_BIN else 1.0)"),
    "dirty set at every trigger": ("        if slot != state[\"slot\"]:\n", "        if True:\n"),
    "naive mode scales": ("r = 1.0 if naive else continuous_ratio(profile, state[\"time\"], d)", "r = continuous_ratio(profile, state[\"time\"], d)"),
    "slot boundary off by one slot": ("boundary = (math.floor(simulation_time / update_seconds) + 1) * update_seconds",
                                     "boundary = (math.floor(simulation_time / update_seconds) + 2) * update_seconds"),
    "leakage guard removed": ('    if base["day"] in days:\n', '    if False:\n'),
    "U divisor check removed": ("or BIN_S % u", ""),
}
SECOND = {}


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
            (pathlib.Path(tmp) / "preflight_world_v1_4.py").write_text(mutated, encoding="utf-8")
            (pathlib.Path(tmp) / "test_preflight_world_v1_4.py").write_text(TEST, encoding="utf-8")
            env = dict(os.environ, PYTHONPATH=str(HERE), PYTHONIOENCODING="utf-8", PYTHONDONTWRITEBYTECODE="1")
            done = subprocess.run([sys.executable, "-B", str(pathlib.Path(tmp) / "test_preflight_world_v1_4.py")],
                                  capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
        dead = done.returncode != 0
        killed += dead
        last = (done.stdout.strip().splitlines() or ["(no output)"])[-1]
        print(f"{'KILLED' if dead else 'SURVIVED'}: {name} -> {last}")
    print(f"{killed}/{len(MUTANTS)} mutants killed")
    return 0 if killed == len(MUTANTS) else 1


if __name__ == "__main__":
    raise SystemExit(main())
