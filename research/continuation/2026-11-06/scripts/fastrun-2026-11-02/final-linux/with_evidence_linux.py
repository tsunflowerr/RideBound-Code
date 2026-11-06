"""Linux entry point of fullsys/with_evidence.py (2026-11-04): sets the Linux import folders, then runs the unchanged
with_evidence.py as __main__ (which imports the named tool as a module and calls its main()).
Usage: python -I -B with_evidence_linux.py <tool.py> [tool arguments ...]
"""
import pathlib
import runpy
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import linux_paths  # noqa: E402

linux_paths.install()
target = pathlib.Path(linux_paths.KIT) / "fullsys" / "with_evidence.py"
sys.argv = [str(target), *sys.argv[1:]]
runpy.run_path(str(target), run_name="__main__")
