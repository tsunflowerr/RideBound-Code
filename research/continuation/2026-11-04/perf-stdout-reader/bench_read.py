"""Times RunnerClient's stdout reader on identical child output, for one adapter tree.

Usage: bench_read.py <adapter_root> <line_bytes> <count>
Starts RunnerClient (its real start() and _read_stdout thread) on emit_lines.py, takes every
line from the client's internal queue, and prints one JSON result: wall seconds from start()
to EOF, the sha256 of the concatenated received lines, line count and the process peak
working set. Nothing of the protocol layer runs, so this isolates the reading path.
"""
import ctypes
import hashlib
import json
import pathlib
import sys
import time

root, size, count = pathlib.Path(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])
sys.path.insert(0, str(root))
from ridebound_fleetpy.runner_client import RunnerClient, _EOF  # noqa: E402

emitter = pathlib.Path(__file__).with_name("emit_lines.py")
client = RunnerClient(
    [sys.executable, str(emitter), str(size), str(count)],
    [emitter],
    timeout_seconds=3600,
    maximum_line_bytes=max(256, size + 16),
)
digest = hashlib.sha256()
lines = 0
started = time.perf_counter()
client.start()
while True:
    item = client._stdout.get()
    if item is _EOF:
        break
    if isinstance(item, BaseException):
        raise item
    digest.update(item)
    lines += 1
elapsed = time.perf_counter() - started
client._process.wait()


class Counters(ctypes.Structure):
    _fields_ = [("cb", ctypes.c_ulong), ("PageFaultCount", ctypes.c_ulong),
                ("PeakWorkingSetSize", ctypes.c_size_t), ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t), ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t), ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t), ("PeakPagefileUsage", ctypes.c_size_t)]


counters = Counters()
counters.cb = ctypes.sizeof(Counters)
ctypes.windll.kernel32.GetCurrentProcess.restype = ctypes.c_void_p
ctypes.windll.psapi.GetProcessMemoryInfo.argtypes = [ctypes.c_void_p, ctypes.POINTER(Counters), ctypes.c_ulong]
if not ctypes.windll.psapi.GetProcessMemoryInfo(
        ctypes.windll.kernel32.GetCurrentProcess(), ctypes.byref(counters), counters.cb):
    raise OSError("GetProcessMemoryInfo failed")
print(json.dumps({
    "root": str(root), "lineBytes": size, "count": count, "lines": lines,
    "seconds": round(elapsed, 3), "sha256": digest.hexdigest(),
    "peakWorkingSetMiB": round(counters.PeakWorkingSetSize / 1048576, 1),
}))
