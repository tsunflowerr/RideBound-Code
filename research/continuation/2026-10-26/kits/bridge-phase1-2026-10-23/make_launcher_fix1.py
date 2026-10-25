"""bridge_launcher_fix1.py = bridge_launcher_g2.py with ADAPTER_ROOT = the fix worktree (one exact edit)."""
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
SOURCE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/bridge_launcher_g2.py")
text = SOURCE.read_bytes().decode("utf-8")
old = 'ADAPTER_ROOT = r"C:\\Code\\RideBound-g2\\simulators\\fleetpy-ridebound"'
new = 'ADAPTER_ROOT = r"C:\\Code\\RideBound-p1fix\\simulators\\fleetpy-ridebound"  # fix1 copy (2026-10-24): the only change'
if text.count(old) != 1:
    raise SystemExit("pattern not found exactly once")
with open(HERE / "bridge_launcher_fix1.py", "x", encoding="utf-8", newline="") as handle:
    handle.write(text.replace(old, new))
print("ok")
