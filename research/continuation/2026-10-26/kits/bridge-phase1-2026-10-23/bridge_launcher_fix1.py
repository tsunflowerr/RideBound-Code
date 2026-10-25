"""Job launcher for the Manhattan bridge: the measurement fix of wf6/realjob, then the world wrapper.

Re-implements the company measurement fix (bundle docs/wf6/realjob/T.txt section 3.3) as a
monkey-patch in this process only; no repository file changes:
  (1) RB_REALJOB_TIMEOUT_S=900  -> RunnerClient waits up to 900 s per Runner response
      (the adapter's own setting stays 60, so the operator configuration is unchanged);
  (2) RB_REALJOB_BUFFERED_READ=1 -> RunnerClient._read_stdout reads the Runner's stdout
      through io.BufferedReader(buffer_size=1 MiB) and calls readline(limit + 2) on it:
      same lines, same limit, same queue, same EOF sentinel. As shipped the pipe is
      unbuffered (runner_client.py:138, bufsize=0), i.e. one ReadFile per byte, and a
      ~15-20 MiB final checkpoint can exceed the 60 s wait on a loaded machine.
The company showed identical decisions with and without the fix (2321/2321 frames).

Usage: python -B bridge_launcher.py <world wrapper .py> <wrapper arguments...>
"""

from __future__ import annotations

import io
import os
import runpy
import sys

# G2 copy (2026-10-23, AMENDMENTS A8): the adapter of the G2 worktree. bridge_launcher.py imports the package from
# E:/Code/RideBound-deadline, and that cached import made every job run that adapter.
ADAPTER_ROOT = r"C:\Code\RideBound-p1fix\simulators\fleetpy-ridebound"  # fix1 copy (2026-10-24): the only change


def install_fix() -> list[str]:
    applied: list[str] = []
    timeout = float(os.environ.get("RB_REALJOB_TIMEOUT_S", "0") or 0)
    buffered = os.environ.get("RB_REALJOB_BUFFERED_READ") == "1"
    if timeout <= 0 and not buffered:
        return applied
    sys.path.insert(0, ADAPTER_ROOT)
    try:
        from ridebound_fleetpy import runner_client as rc
    finally:
        sys.path.remove(ADAPTER_ROOT)

    if timeout > 0:
        original_init = rc.RunnerClient.__init__

        def __init__(self, *args, **kwargs):
            original_init(self, *args, **kwargs)
            self._timeout = timeout

        rc.RunnerClient.__init__ = __init__
        applied.append(f"timeout={timeout:g}s")

    if buffered:
        eof = rc._EOF

        def _read_stdout(self) -> None:
            process = self._process
            assert process is not None and process.stdout is not None
            reader = io.BufferedReader(process.stdout, buffer_size=1 << 20)
            try:
                while True:
                    line = reader.readline(self._maximum_output_line_bytes + 2)
                    if not line:
                        self._stdout.put(eof)
                        return
                    self._stdout.put(line)
            except BaseException as exc:  # same contract as the original reader
                self._stdout.put(exc)

        rc.RunnerClient._read_stdout = _read_stdout
        applied.append("buffered-read=1MiB")
    return applied


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit("usage: bridge_launcher.py <wrapper.py> <args...>")
    applied = install_fix()
    print("bridge_launcher: " + (", ".join(applied) if applied else "no fix (as shipped)"), file=sys.stderr, flush=True)
    wrapper = sys.argv[1]
    sys.argv = sys.argv[1:]
    runpy.run_path(wrapper, run_name="__main__")


if __name__ == "__main__":
    main()
