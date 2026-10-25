"""Mutation check for preflight_world_v1_1: each mutant must make test_preflight_world_v1_1.py fail. Mutants run in
a temporary directory; the real file is never modified. Usage: python -B mutate_preflight_world_v1_1.py"""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = (HERE / "preflight_world_v1_1.py").read_text(encoding="utf-8")
MUTANTS = {
    "bin start never inserted": (
        "        if value is None or boundary < value:",
        "        if False:"),
    "later bin start replaces an earlier physical boundary": (
        "        if value is None or boundary < value:",
        "        if value is None or boundary > value:"),
    "bin start not strictly after now": (
        "    return (int(simulation_time // BIN_S) + 1) * BIN_S",
        "    return int(simulation_time // BIN_S) * BIN_S"),
    "bin start one bin late": (
        "    return (int(simulation_time // BIN_S) + 1) * BIN_S",
        "    return (int(simulation_time // BIN_S) + 2) * BIN_S"),
    "physical boundary dropped when present": (
        "        return value\n",
        "        return boundary\n"),
}
survivors = []
for name, (old, new) in MUTANTS.items():
    if old not in SOURCE:
        print(f"MUTANT NOT APPLICABLE: {name}")
        survivors.append(name)
        continue
    with tempfile.TemporaryDirectory() as tmp:
        (Path(tmp) / "preflight_world_v1_1.py").write_text(SOURCE.replace(old, new, 1), encoding="utf-8")
        for helper in ("world_v1.py", "preflight_world.py", "test_preflight_world_v1_1.py"):
            shutil.copy(HERE / helper, tmp)
        result = subprocess.run([sys.executable, "-B", "test_preflight_world_v1_1.py"], cwd=tmp,
                                capture_output=True, text=True)
        killed = result.returncode != 0
        print(f"{'killed ' if killed else 'SURVIVED'}  {name}")
        if not killed:
            survivors.append(name)
print(f"{len(survivors)} surviving mutant(s)")
raise SystemExit(1 if survivors else 0)
