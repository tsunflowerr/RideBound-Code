"""Wait until no process has any of the given markers in its command line (three idle checks a minute apart), then run
the command. Used to start a job queue only after a heavier one has finished on this 15.7 GiB machine.

Usage: python -I wait_idle.py <marker[,marker...]> <command...>
"""
import subprocess
import sys
import time

import psutil


def busy(markers: list[str]) -> bool:
    # This process and the shells that started it carry the markers in their own command lines.
    me = psutil.Process()
    own = {me.pid, *(p.pid for p in me.parents())}
    for process in psutil.process_iter(["cmdline"]):
        try:
            if process.pid in own:
                continue
            if any(m in part for part in process.info["cmdline"] or [] for m in markers):
                return True
        except psutil.Error:
            pass
    return False


markers = sys.argv[1].split(",")
idle = 0
while idle < 3:
    idle = 0 if busy(markers) else idle + 1
    time.sleep(60)
print(time.strftime("%Y-%m-%dT%H:%M:%S"), f"idle ({', '.join(markers)}); starting", flush=True)
sys.exit(subprocess.call(sys.argv[2:]))
