"""Audit 23/10 §6-P4(a) supplement (owner order 2026-10-25: carry out the whole plan).

The relaxed drop deadline in the STUDENT'S own form: overlay configs/DLS-120.json (dropEtaDeadlineSlackMs 120000, no
dropEtaDeadlineRelaxed flag) with the 22/10 bridge WP4 (commitmentRecovery no-worse-than-reference-v1). Every other input
is the 22/10 DLHZ-120 job's: runner-via-ref-v1, bridge_launcher.py (adapter E:/Code/RideBound-deadline), world W5,
drivers, fixtures, seed 7. DLS-120.json differs from the bridged DLHZ-120.json only by the missing flag.

Prediction, fixed before the run (NW-PREDICTION.txt): rider-identical to the emulator DLHZ-120 rows sealed on 21/10
(via-bridge-2026-10-22/EMULATOR-PREDICTIONS.sha256), i.e. the student's no-worse form is the HZ fence p <= max(p0 + B, e).
Phase nw: 11 jobs, outputs under E:/RideBoundData/research/via-bridge-man-v1-e/nw/. Needs NW-READY.txt.
Same rails as run_bridge.py (AC only, RAM floor, disk floors, STOP file, never overwrites, failed outputs kept).
Usage: python -B run_nw.py check|nw [--workers N]
"""
from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
BRIDGE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22")
sys.path.insert(0, str(BRIDGE))
import run_bridge as base  # noqa: E402

ARM = "DLS-120-NW"
CONFIG = HERE / "configs" / "DLS-120.json"
CONFIG_SHA256 = hashlib.sha256(CONFIG.read_bytes()).hexdigest()
base.OUT_ROOT = pathlib.Path(r"E:/RideBoundData/research/via-bridge-man-v1-e")
base.LOG_ROOT = HERE / "logs-nw"
base.STOP_FILE = HERE / "STOP"
base.LOCK = HERE / "run-nw-instance.lock"
base.PROGRESS = HERE / "PROGRESS-NW.txt"
_inputs = base.inputs


def jobs(_phase: str):
    return [(f"{ARM}-{cell}", ARM, cell) for cell in base.CELLS]


def inputs(_phase: str, arm: str, cell: str) -> dict:
    spec = _inputs("main", "DLHZ-120", cell)
    spec["label"] = f"{arm}-{cell}-vbr"
    spec["config"] = CONFIG
    return spec


base.jobs = jobs
base.inputs = inputs


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phase", choices=("check", "nw"))
    parser.add_argument("--workers", type=int, default=2)
    args = parser.parse_args()
    problems = base.check_inputs("nw")
    if CONFIG_SHA256 != (HERE / "NW-PREDICTION.txt").read_text(encoding="utf-8").split("config sha256 ")[1].split()[0]:
        problems.append("DLS-120.json changed since NW-PREDICTION.txt")
    if args.phase == "check":
        base.log(f"check nw: {'OK' if not problems else problems}; disk {base.disk_problems() or 'OK'}; "
                 f"RAM {base.psutil.virtual_memory().available / 2**30:.1f} GiB; AC {base.on_ac()}")
        return 0
    if not (HERE / "NW-READY.txt").exists():
        problems.append("refused: NW-READY.txt does not exist")
    if problems:
        base.log("refused: " + "; ".join(problems))
        return 4
    if not base.acquire_lock():
        base.log("refused: another run_nw.py instance is running")
        return 3
    base.keep_awake()
    (base.LOG_ROOT / "nw").mkdir(parents=True, exist_ok=True)
    (base.OUT_ROOT / "nw").mkdir(parents=True, exist_ok=True)
    results: dict[str, dict] = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(base.run_job, "nw", *job) for job in jobs("nw")]
        for future in concurrent.futures.as_completed(futures):
            name, state, info = future.result()
            results[name] = {"state": state, **(info or {})}
            base.write_progress("nw", results)
    base.write_progress("nw", results)
    base.log("phase nw finished")
    return 0


if __name__ == "__main__":
    sys.exit(main())
