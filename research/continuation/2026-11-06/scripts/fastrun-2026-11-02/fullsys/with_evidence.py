"""Run one of the comparison tools with evidence_reader installed (ADR-089 GĐ5/GĐ6, 2026-11-04), so job folders whose
transcript was dropped are read from their evidence.json.gz. The tool itself is not changed (several are sealed).

Usage: python -I -B with_evidence.py <tool.py> [tool arguments ...]
       tools: compare_fullsys.py, compare_drop_sequences.py, compare_shadow.py, eta_metrics.py, reversal_counts.py
"""
import importlib
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE))
import evidence_reader  # noqa: E402

TOOLS = ("compare_fullsys.py", "compare_drop_sequences.py", "compare_shadow.py", "eta_metrics.py", "reversal_counts.py")
if len(sys.argv) < 2 or sys.argv[1] not in TOOLS:
    raise SystemExit(__doc__)
evidence_reader.install()
# imported as a module (not run as a script) so the wrapped readers are the ones its main() calls
module = importlib.import_module(sys.argv[1][:-3])
sys.argv = [str(HERE / sys.argv[1]), *sys.argv[2:]]
raise SystemExit(module.main())
