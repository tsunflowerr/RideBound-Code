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

Via-zone copy (2026-10-26) of bridge_launcher_fix1.py: the same adapter (C:/Code/RideBound-p1fix) and fixes (1)-(2),
plus one more process-local patch, also with no repository file change:
  (3) RB_VIA_MAX_OUTPUT_LINE_BYTES=<n> -> session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = n (shipped 64 MiB,
      session.py:28, read at call time in session.py:413). At 108 requests the final checkpoint line was already
      17.1 MiB; a 509-request, 62-vehicle job may exceed 64 MiB at the very end. The limit only refuses an over-long
      Runner line; it does not change any message or decision. The Runner's input limit (session.py:27, passed on
      the Runner command line at :391) is NOT changed.

Amendment A3 copy (2026-10-26 02:50): bridge_launcher_via.py plus one diagnostic patch, also process-local:
  (4) RunnerClient._fail first writes the Runner's captured stderr (RunnerClient.diagnostics, runner_client.py:118-121;
      up to 1 MiB) to this process's stderr, which the job driver keeps in the job log. RunnerHost answers every
      unexpected exception with the same INTERNAL_ERROR text and writes only the exception type to stderr
      (RunnerHost.cs:96-106); trialA1 lost that line. Nothing else changes: the failure is raised exactly as before.

Usage: python -B bridge_launcher_via_a3.py <world wrapper .py> <wrapper arguments...>
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
    line_limit = int(os.environ.get("RB_VIA_MAX_OUTPUT_LINE_BYTES", "0") or 0)
    if timeout <= 0 and not buffered and line_limit <= 0:
        return applied
    sys.path.insert(0, ADAPTER_ROOT)
    try:
        from ridebound_fleetpy import runner_client as rc
        from ridebound_fleetpy import session as ss
    finally:
        sys.path.remove(ADAPTER_ROOT)

    if line_limit > 0:
        if line_limit < ss.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES:
            raise SystemExit("RB_VIA_MAX_OUTPUT_LINE_BYTES may only raise the shipped limit")
        ss.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = line_limit
        applied.append(f"output-line-limit={line_limit}")

    original_fail = rc.RunnerClient._fail

    def _fail(self, code: str, detail: str) -> None:
        try:
            text = self.diagnostics
        except BaseException as exc:  # never mask the real failure
            text = f"<diagnostics unavailable: {type(exc).__name__}>"
        print(f"[runner-stderr at failure {code}]\n{text}\n[end runner-stderr]", file=sys.stderr, flush=True)
        original_fail(self, code, detail)

    rc.RunnerClient._fail = _fail
    applied.append("runner-stderr-on-failure")

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
