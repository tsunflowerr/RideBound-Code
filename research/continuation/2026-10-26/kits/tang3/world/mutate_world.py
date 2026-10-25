"""Mutation check for world_v1: each mutant must make test_world_v1.py fail. Mutants run in a
temporary directory; the real file is never modified. Usage: python -B mutate_world.py"""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = (HERE / "world_v1.py").read_text(encoding="utf-8")
MUTANTS = {
    "forecast peeks one bin ahead": ("        b = int(now_s // BIN_S)\n        self.reads.append(b)",
                                     "        b = int(now_s // BIN_S) + 1\n        self.reads.append(b)"),
    "travel time ignores bin boundaries (breaks FIFO)": (
        "        remaining, t = float(free_flow_s), float(depart_s)",
        "        return free_flow_s * self.factor(od, int(depart_s // BIN_S))\n"
        "        remaining, t = float(free_flow_s), float(depart_s)"),
    "global RNG instead of keyed": ("    digest = hashlib.sha256(repr(key).encode(\"utf-8\")).digest()",
                                    "    import random\n    digest = random.randbytes(32)"),
    "seed not in key": ("_normal(c.world_seed, od, k, \"ar\")", "_normal(od, k, \"ar\")"),
    "one-sided noise": ("value = c.rho * value + c.sigma * _normal(c.world_seed, od, k, \"ar\")",
                        "value = c.rho * value + c.sigma * abs(_normal(c.world_seed, od, k, \"ar\"))"),
    "base ignored": ("        return base * math.exp", "        return math.exp"),
}
survivors = []
for name, (old, new) in MUTANTS.items():
    if old not in SOURCE:
        print(f"MUTANT NOT APPLICABLE: {name}")
        survivors.append(name)
        continue
    with tempfile.TemporaryDirectory() as tmp:
        (Path(tmp) / "world_v1.py").write_text(SOURCE.replace(old, new, 1), encoding="utf-8")
        shutil.copy(HERE / "test_world_v1.py", tmp)
        result = subprocess.run([sys.executable, "-B", "test_world_v1.py"], cwd=tmp,
                                capture_output=True, text=True)
        killed = result.returncode != 0
        print(f"{'killed ' if killed else 'SURVIVED'}  {name}")
        if not killed:
            survivors.append(name)
print(f"{len(survivors)} surviving mutant(s)")
raise SystemExit(1 if survivors else 0)
