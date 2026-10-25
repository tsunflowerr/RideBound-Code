"""Mutation check for preflight_world_v1_2: each mutant must make test_preflight_world_v1_2.py fail. Mutants run in
a temporary directory; the real file is never modified. Usage: python -B mutate_preflight_world_v1_2.py"""
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = (HERE / "preflight_world_v1_2.py").read_text(encoding="utf-8")
MUTANTS = {
    "day not checked": (
        "    if base[\"day\"] != day:",
        "    if False:"),
    "window start not checked": (
        "    if int(base[\"windowStart\"]) != start:",
        "    if False:"),
    "07:00 not allowed": (
        "WINDOW_STARTS_V1_2 = (25200, 28800, 61200)",
        "WINDOW_STARTS_V1_2 = (28800, 61200)"),
    "wrong New York offset (UTC-4)": (
        "NEW_YORK_OFFSET = datetime.timedelta(hours=-5)",
        "NEW_YORK_OFFSET = datetime.timedelta(hours=-4)"),
    "measured base accepted without a scenario": (
        "    if scenario_path is None:\n        raise SystemExit(",
        "    if scenario_path is None:\n        return None\n        raise SystemExit("),
    "guard after the preflight is loaded": (
        "    checked = check_measured_base(",
        "    checked = None and check_measured_base("),
    "version line still says v1.1": (
        "[world] version=v1.2 config=",
        "[world] version=v1.1 config="),
    "time-zone check dropped": (
        "    if window.get(\"sourceTimezoneId\") != \"America/New_York\":",
        "    if False:"),
    "daylight-saving range not enforced": (
        "    if not OFFSET_VALID[0] <= local.date() <= OFFSET_VALID[1]:",
        "    if False:"),
}
survivors = []
for name, (old, new) in MUTANTS.items():
    if old not in SOURCE:
        print(f"MUTANT NOT APPLICABLE: {name}")
        survivors.append(name)
        continue
    with tempfile.TemporaryDirectory() as tmp:
        (Path(tmp) / "preflight_world_v1_2.py").write_text(SOURCE.replace(old, new, 1), encoding="utf-8")
        for helper in ("world_v1.py", "preflight_world.py", "preflight_world_v1_1.py", "test_preflight_world_v1_2.py"):
            shutil.copy(HERE / helper, tmp)
        result = subprocess.run([sys.executable, "-B", "test_preflight_world_v1_2.py"], cwd=tmp,
                                capture_output=True, text=True)
        killed = result.returncode != 0
        print(f"{'killed ' if killed else 'SURVIVED'}  {name}")
        if not killed:
            survivors.append(name)
print(f"{len(survivors)} surviving mutant(s)")
raise SystemExit(1 if survivors else 0)
