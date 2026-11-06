"""Write bridge_launcher_via_full.py from via-real-2026-10-26/bridge_launcher_via_a3.py with three exact replacements."""
import pathlib

SOURCE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/via-real-2026-10-26/bridge_launcher_via_a3.py")
TARGET = pathlib.Path(__file__).with_name("bridge_launcher_via_full.py")
text = SOURCE.read_bytes().decode("utf-8")


def sub(old: str, new: str) -> None:
    global text
    if text.count(old) != 1:
        raise SystemExit(f"expected exactly one occurrence of: {old!r}")
    text = text.replace(old, new)


sub("Usage: python -B bridge_launcher_via_a3.py",
    "Full-node copy (ADR-089, 2026-11-03) of bridge_launcher_via_a3.py: the adapter of the run worktree\n"
    "E:/Code/RideBound-fastrun-run and one more process-local patch, with no repository file change:\n"
    "  (5) RB_VIA_MAX_INPUT_LINE_BYTES=<n> -> session.RUNNER_MAXIMUM_INPUT_LINE_BYTES = n (shipped 64 MiB, session.py:27,\n"
    "      passed on the Runner command line). A full-node travel snapshot is about 140 MB; the Runner accepts up to 1 GiB\n"
    "      (NdjsonReader.MaximumConfigurableLineBytes). It only raises the limit; no message or decision changes.\n\n"
    "Usage: python -B bridge_launcher_via_full.py")
sub(r'ADAPTER_ROOT = r"C:\Code\RideBound-p1fix\simulators\fleetpy-ridebound"  # fix1 copy (2026-10-24): the only change',
    r'ADAPTER_ROOT = r"E:\Code\RideBound-fastrun-run\simulators\fleetpy-ridebound"  # ADR-089 run worktree')
sub('    line_limit = int(os.environ.get("RB_VIA_MAX_OUTPUT_LINE_BYTES", "0") or 0)\n'
    '    if timeout <= 0 and not buffered and line_limit <= 0:',
    '    line_limit = int(os.environ.get("RB_VIA_MAX_OUTPUT_LINE_BYTES", "0") or 0)\n'
    '    input_limit = int(os.environ.get("RB_VIA_MAX_INPUT_LINE_BYTES", "0") or 0)\n'
    '    if timeout <= 0 and not buffered and line_limit <= 0 and input_limit <= 0:')
sub('        ss.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = line_limit\n'
    '        applied.append(f"output-line-limit={line_limit}")\n',
    '        ss.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES = line_limit\n'
    '        applied.append(f"output-line-limit={line_limit}")\n\n'
    '    if input_limit > 0:\n'
    '        if input_limit < ss.RUNNER_MAXIMUM_INPUT_LINE_BYTES:\n'
    '            raise SystemExit("RB_VIA_MAX_INPUT_LINE_BYTES may only raise the shipped limit")\n'
    '        ss.RUNNER_MAXIMUM_INPUT_LINE_BYTES = input_limit\n'
    '        applied.append(f"input-line-limit={input_limit}")\n')
with open(TARGET, "x", encoding="utf-8", newline="") as handle:
    handle.write(text)
print(f"wrote {TARGET}")
