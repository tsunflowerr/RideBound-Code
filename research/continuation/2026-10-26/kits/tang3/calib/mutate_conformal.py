"""Mutation check for conformal.py: each mutant must make test_conformal.py fail. Mutants run in a temporary directory;
the real file is never modified. Usage: python -B mutate_conformal.py"""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = (HERE / "conformal.py").read_text(encoding="utf-8")
MUTANTS = {
    "rank rounds down (floor instead of ceil)": (
        "    return ((n + 1) * (alpha_den - alpha_num) + alpha_den - 1) // alpha_den",
        "    return ((n + 1) * (alpha_den - alpha_num)) // alpha_den"),
    "rank uses n instead of n + 1": (
        "    return ((n + 1) * (alpha_den - alpha_num) + alpha_den - 1) // alpha_den",
        "    return (n * (alpha_den - alpha_num) + alpha_den - 1) // alpha_den"),
    "rank by floating point": (
        "    return ((n + 1) * (alpha_den - alpha_num) + alpha_den - 1) // alpha_den",
        "    return int(__import__('math').ceil((n + 1) * (1 - alpha_num / alpha_den)))"),
    "infinite encoded as a large number": (
        "        return Threshold(n, alpha_num, alpha_den, k, None, True,\n                         f\"k = {k} > n = {n}",
        "        return Threshold(n, alpha_num, alpha_den, k, 10 ** 18, False,\n                         f\"k = {k} > n = {n}"),
    "k-th smallest off by one": (
        "finite[k - 1], False",
        "finite[k - 2], False"),
    "interpolating quantile": (
        "    return Threshold(n, alpha_num, alpha_den, k, finite[k - 1], False,",
        "    import numpy as np\n    return Threshold(n, alpha_num, alpha_den, k, int(np.quantile(finite, 1 - alpha_num / alpha_den)), False,"),
    "unobserved scores dropped instead of ranked": (
        "    if k > len(finite):   # the k-th smallest is an unobserved (infinite) score",
        "    if False:"),
    "overlap between calibration and test allowed": (
        "    if overlap:\n        raise ValueError",
        "    if False:\n        raise ValueError"),
    "missing score allowed": (
        "            if score is None:\n                raise ValueError",
        "            if False:\n                raise ValueError"),
    "negative scores allowed": (
        "or score < 0):",
        "or score < -10 ** 9):"),
    "covered uses < instead of <=": (
        "    return score <= threshold_.gamma_ms",
        "    return score < threshold_.gamma_ms"),
}
survivors = []
for name, (old, new) in MUTANTS.items():
    if old not in SOURCE:
        print(f"MUTANT NOT APPLICABLE: {name}")
        survivors.append(name)
        continue
    with tempfile.TemporaryDirectory() as tmp:
        (Path(tmp) / "conformal.py").write_text(SOURCE.replace(old, new, 1), encoding="utf-8")
        shutil.copy(HERE / "test_conformal.py", tmp)
        result = subprocess.run([sys.executable, "-B", "test_conformal.py"], cwd=tmp, capture_output=True, text=True)
        killed = result.returncode != 0
        print(f"{'killed ' if killed else 'SURVIVED'}  {name}")
        if not killed:
            survivors.append(name)
print(f"{len(survivors)} surviving mutant(s)")
raise SystemExit(1 if survivors else 0)
