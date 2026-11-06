"""Make the Linux files of the final-run bundle (2026-11-04). The Windows bundle (final-v2: Runner, repository export,
inputs, sealed predictions, sealed tools) is reused unchanged; only what names Windows paths gets a Linux copy, with the
mirror root E:/ -> /rb/. Every replacement must hit exactly once; the run stops otherwise. Refuses to overwrite.

  research/fullsys-f14-v2/job-configs-linux/   commitment configs (copies) and WP4 configs whose only change is
                                               shadowAllocation.networkDirectory (E:/... -> /rb/...)
  research/fullsys-f14-v2/jobs-linux/          the three job lists with /rb paths and job-configs-linux
  final-v2-linux/tools/                        job launcher, world wrapper (repository = /rb/.../final-v2/repo),
                                               inventory.py, run_queue_linux.py (bundled Linux .NET, finalize_linux.py)
  fastrun-2026-11-02/final-linux/              final_report_linux.py, verify_bundle_linux.py (generated here; the other
                                               files of that folder are written by hand)
Usage: python -I -B make_linux_overlay.py
"""
import ast
import json
import pathlib
import re
import shutil

E = pathlib.Path("E:/")
DATA = E / "RideBoundData/research/fullsys-f14-v2"
WIN_TOOLS = E / "RideBoundData/final-v2/tools"
LINUX = E / "RideBoundData/final-v2-linux"
KIT = E / "Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02"
FINAL_LINUX = KIT / "final-linux"
NETWORK_WIN = "E:/RideBoundData/research/via-full-v1/shadow-gate/manhattan-network"
NETWORK_LINUX = "/rb/RideBoundData/research/via-full-v1/shadow-gate/manhattan-network"
WINDOWS_PATH = re.compile(r"\b[A-Za-z]:[/\\]")


def replace_once(text: str, old: str, new: str, where: str) -> str:
    if text.count(old) != 1:
        raise SystemExit(f"{where}: expected exactly one {old!r}, found {text.count(old)}")
    return text.replace(old, new)


def write_new(path: pathlib.Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def main() -> int:
    made = []
    # 1. configurations
    out = DATA / "job-configs-linux"
    if out.exists():
        raise SystemExit(f"refused: {out} exists")
    for path in sorted((DATA / "job-configs-v2/commitment").glob("*.json")):
        target = out / "commitment" / path.name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, target)
        made.append(target)
    for arm in ("FS-G2-SHRC71-CL", "FS-G2-SHHZ71-N"):
        for path in sorted((DATA / "job-configs-v2/wp4" / arm).glob("*.json")):
            text = path.read_text(encoding="utf-8")
            text = replace_once(text, f'"networkDirectory": "{NETWORK_WIN}"', f'"networkDirectory": "{NETWORK_LINUX}"',
                                str(path))
            before, after = json.loads(path.read_text(encoding="utf-8")), json.loads(text)
            before["shadowAllocation"]["networkDirectory"] = NETWORK_LINUX
            if before != after:
                raise SystemExit(f"{path}: more than networkDirectory changed")
            target = out / "wp4" / arm / path.name
            write_new(target, text)
            made.append(target)
    # 2. job lists
    jobs_out = DATA / "jobs-linux"
    if jobs_out.exists():
        raise SystemExit(f"refused: {jobs_out} exists")
    for name in ("jobs-final.json", "jobs-cloud-check.json", "jobs-gate-attr.json"):
        text = (DATA / "jobs-bundle-v2" / name).read_text(encoding="utf-8")
        text = text.replace("E:/RideBoundData/research/fullsys-f14-v2/job-configs-v2/",
                            "/rb/RideBoundData/research/fullsys-f14-v2/job-configs-linux/")
        text = text.replace('"E:/', '"/rb/')
        jobs = json.loads(text)
        if any(WINDOWS_PATH.search(str(v)) for job in jobs for v in job.values()):
            raise SystemExit(f"{name}: a Windows path is left")
        write_new(jobs_out / name, text)
        made.append(jobs_out / name)
    # 3. job tools
    tools = LINUX / "tools"
    if tools.exists():
        raise SystemExit(f"refused: {tools} exists")
    launcher = (WIN_TOOLS / "bridge_launcher_via_full.py").read_text(encoding="utf-8")
    launcher = replace_once(launcher, 'ADAPTER_ROOT = r"E:/RideBoundData/final-v2/repo/simulators/fleetpy-ridebound"',
                            'ADAPTER_ROOT = r"/rb/RideBoundData/final-v2/repo/simulators/fleetpy-ridebound"', "launcher")
    write_new(tools / "bridge_launcher_via_full.py", launcher)
    world = (WIN_TOOLS / "world-via-full/preflight_world.py").read_text(encoding="utf-8")
    world = replace_once(world, 'REPO = pathlib.Path(r"E:/RideBoundData/final-v2/repo")',
                         'REPO = pathlib.Path(r"/rb/RideBoundData/final-v2/repo")', "preflight_world")
    world = replace_once(world, 'r"E:/RideBoundData/wp6/extracted/sha256/d9/"',
                         'r"/rb/RideBoundData/wp6/extracted/sha256/d9/"', "preflight_world")
    write_new(tools / "world-via-full/preflight_world.py", world)
    for path in sorted((WIN_TOOLS / "world-via-full").glob("*.py")):
        if path.name != "preflight_world.py":
            write_new(tools / "world-via-full" / path.name, path.read_text(encoding="utf-8"))
    write_new(tools / "inventory.py", (WIN_TOOLS / "inventory.py").read_text(encoding="utf-8"))
    queue = (KIT / "final/run_queue_final.py").read_text(encoding="utf-8")
    for old, new in (
            ('ROOT = pathlib.Path(r"E:/RideBoundData/final-v2")', 'ROOT = pathlib.Path("/rb/RideBoundData/final-v2")'),
            ('TOOLS = ROOT / "tools"', 'TOOLS = pathlib.Path("/rb/RideBoundData/final-v2-linux/tools")'),
            ('DOTNET = ROOT / "dotnet" / "dotnet.exe"',
             'DOTNET = pathlib.Path("/rb/RideBoundData/final-v2-linux/dotnet/runtime/dotnet")'),
            ('FLEETPY = pathlib.Path(r"E:/RideBoundData/wp7/FleetPy-1.0.2")',
             'FLEETPY = pathlib.Path("/rb/RideBoundData/wp7/FleetPy-1.0.2")'),
            ('FINALIZE = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02/fullsys/'
             'finalize_job.py")',
             'FINALIZE = pathlib.Path("/rb/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02/final-linux/'
             'finalize_linux.py")')):
        queue = replace_once(queue, old, new, "run_queue")
    queue = queue.replace('"""Resumable job queue for the final runs',
                          '"""LINUX copy (make_linux_overlay.py, 2026-11-04) of run_queue_final.py. Resumable job queue for '
                          'the final runs', 1)
    write_new(tools / "run_queue_linux.py", queue)
    made += sorted(p for p in tools.rglob("*.py"))
    # 4. report and bundle check
    report = (KIT / "final/final_report.py").read_text(encoding="utf-8")
    for old, new in (
            ('KIT = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02")',
             'KIT = pathlib.Path("/rb/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02")'),
            ('DATA = pathlib.Path(r"E:/RideBoundData/research/fullsys-f14-v2")',
             'DATA = pathlib.Path("/rb/RideBoundData/research/fullsys-f14-v2")'),
            ('WRAP = KIT / "fullsys" / "with_evidence.py"', 'WRAP = KIT / "final-linux" / "with_evidence_linux.py"')):
        report = replace_once(report, old, new, "final_report")
    write_new(FINAL_LINUX / "final_report_linux.py", report)
    verify = (KIT / "final/verify_bundle.py").read_text(encoding="utf-8")
    verify = replace_once(verify, 'path = pathlib.Path("E:/") / row["path"]', 'path = pathlib.Path("/rb/") / row["path"]',
                          "verify_bundle")
    write_new(FINAL_LINUX / "verify_bundle_linux.py", verify)
    made += [FINAL_LINUX / "final_report_linux.py", FINAL_LINUX / "verify_bundle_linux.py"]
    # 5. no Windows path may be left in a string constant of the generated Python files (docstrings excluded; the
    #    first version of this check matched lines and stopped on a docstring line of the launcher, 2026-11-04)
    for path in made:
        if path.suffix == ".py":
            tree = ast.parse(path.read_text(encoding="utf-8"))
            docs = {id(node.body[0].value) for node in ast.walk(tree)
                    if isinstance(node, (ast.Module, ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef))
                    and node.body and isinstance(node.body[0], ast.Expr)
                    and isinstance(node.body[0].value, ast.Constant)}
            for node in ast.walk(tree):
                if isinstance(node, ast.Constant) and isinstance(node.value, str) and id(node) not in docs \
                        and WINDOWS_PATH.search(node.value):
                    raise SystemExit(f"{path}:{node.lineno}: Windows path in code: {node.value[:100]}")
    print(f"{len(made)} files made")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
