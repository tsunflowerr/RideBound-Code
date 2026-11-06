"""Linux entry point of fullsys/finalize_job.py (2026-11-04): sets the Linux import folders, then runs the unchanged
finalize_job.py as __main__ with the same arguments.
Usage: python -I -B finalize_linux.py <job folder> <role> [--transcript keep|drop]
"""
import pathlib
import runpy
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import linux_paths  # noqa: E402

linux_paths.install()
target = pathlib.Path(linux_paths.KIT) / "fullsys" / "finalize_job.py"
sys.argv = [str(target), *sys.argv[1:]]
runpy.run_path(str(target), run_name="__main__")
