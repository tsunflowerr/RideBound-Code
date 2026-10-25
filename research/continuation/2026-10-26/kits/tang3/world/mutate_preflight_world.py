"""Mutation check for preflight_world: each mutant must make test_preflight_world.py fail. Mutants
run in a temporary directory; the real file is never modified. Usage: python -B mutate_preflight_world.py"""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = (HERE / "preflight_world.py").read_text(encoding="utf-8")
MUTANTS = {
    "reads the next bin (leaks the future)": (
        "            factor = self.world.factor((origin, destination), b)",
        "            factor = self.world.factor((origin, destination), b + 1)"),
    "one scalar for every edge": (
        "            factor = self.world.factor((origin, destination), b)",
        "            factor = self.world.factor((0, 1), b)"),
    "bridge not marked dirty": (
        "            control._rb_travel_dirty = True",
        "            pass"),
    "stale arc cache kept": (
        "            control._rb_travel_arc_cache.clear()",
        "            pass"),
    "every trigger reapplies": (
        "        if b == self.bin:\n            return False",
        "        if False:\n            return False"),
    "unanchored world": (
        "    return AnchoredWorld(world), config, hashlib.sha256(raw).hexdigest()",
        "    return world, config, hashlib.sha256(raw).hexdigest()"),
    "unknown config key accepted": (
        "    if unknown or missing:",
        "    if missing:"),
}
survivors = []
for name, (old, new) in MUTANTS.items():
    if old not in SOURCE:
        print(f"MUTANT NOT APPLICABLE: {name}")
        survivors.append(name)
        continue
    with tempfile.TemporaryDirectory() as tmp:
        (Path(tmp) / "preflight_world.py").write_text(SOURCE.replace(old, new, 1), encoding="utf-8")
        for helper in ("world_v1.py", "test_preflight_world.py"):
            shutil.copy(HERE / helper, tmp)
        result = subprocess.run([sys.executable, "-B", "test_preflight_world.py"], cwd=tmp,
                                capture_output=True, text=True)
        killed = result.returncode != 0
        print(f"{'killed ' if killed else 'SURVIVED'}  {name}")
        if not killed:
            survivors.append(name)
print(f"{len(survivors)} surviving mutant(s)")
raise SystemExit(1 if survivors else 0)
