"""Assemble the final-run bundle (ADR-089 GĐ5/GĐ6, 2026-11-04): one directory tree at FIXED absolute paths on E:, so the
same command lines run on this machine and on a rented Windows machine where the bundle is unpacked and mapped to E:
(`subst E: <folder>`). Same OS, same paths, same Python, same .NET, same Runner: a job's frames must be identical on both
machines, and the first step on the rented machine checks that against this machine's receipts.

E:/RideBoundData/final-v2/ (root; must not exist)
  repo/     git-visible files of the development worktree E:/Code/RideBound-fastrun (GĐ4 Shadow + forecast in C#, the
            adapter's forecast fix, ADR-089) EXCEPT research/continuation/2026-10-26/company-emulator/ (company-machine
            material, never sent out) and one thesis manifest with logo paths; committed as ONE local export commit with
            a fixed identity and date (the adapter writes HEAD into every run manifest). Scanned for the company's name.
  runner/   `dotnet publish src/RideBound.Runner -c Release` FROM repo/ (the Runner is built from the exported source)
  dotnet/   the .NET host and the one Microsoft.NETCore.App runtime the Runner rolls forward to on this machine
  tools/    the job launcher and world wrapper (repository path = repo/), inventory.py, run_queue_final.py
  BUILD.json  what each part was made from, with SHA-256
Other files of the bundle stay at their own absolute paths (FleetPy, the Python environment, the F14 inputs, the network,
the sealed predictions, the comparison tools); pack_final.py lists and hashes all of them.

Usage: python -I -B assemble_final.py
"""
import hashlib
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys

HERE = pathlib.Path(__file__).resolve().parent
KIT = HERE.parent
ROOT = pathlib.Path(r"E:/RideBoundData/final-v2")
DEV = pathlib.Path(r"E:/Code/RideBound-fastrun")
EXCLUDED_PREFIX = "research/continuation/2026-10-26/company-emulator/"
EXCLUDED_FILES = {"research/continuation/2026-10-15/thesis-v5-deployment.json"}
# Published copy (2026-11-06): the company names are not written in this public repository; set RB_COMPANY_NAMES_REGEX
# to the original byte pattern before running (sha256 of the unredacted file: scripts/HELD-BACK.tsv).
COMPANY_NAMES = re.compile(os.environ["RB_COMPANY_NAMES_REGEX"].encode(), re.IGNORECASE)
DOTNET = pathlib.Path(r"C:/Program Files/dotnet")
PHASE1 = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad/bridge-phase1-2026-10-23")
G3A_TOOLS = pathlib.Path(r"E:/RideBoundData/research/via-full-v1/gate-g3a/tools")
IDENTITY = {"GIT_AUTHOR_NAME": "ridebound-export", "GIT_AUTHOR_EMAIL": "export@localhost",
            "GIT_COMMITTER_NAME": "ridebound-export", "GIT_COMMITTER_EMAIL": "export@localhost",
            "GIT_AUTHOR_DATE": "2026-11-04T06:00:00+00:00", "GIT_COMMITTER_DATE": "2026-11-04T06:00:00+00:00"}


def sha(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def git(repo: pathlib.Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(repo), "-c", "core.autocrlf=false", "-c", "core.filemode=false", *args],
                          check=True, stdout=subprocess.PIPE, text=True, encoding="utf-8",
                          env=dict(os.environ, **IDENTITY)).stdout.strip()


def replace_once(text: str, old: str, new: str) -> str:
    if text.count(old) != 1:
        raise SystemExit(f"expected exactly one occurrence of {old!r}")
    return text.replace(old, new)


def export_repo(repo: pathlib.Path) -> dict:
    listed = subprocess.run(["git", "-C", str(DEV), "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
                            check=True, stdout=subprocess.PIPE).stdout.split(b"\0")
    paths = sorted(p.decode("utf-8") for p in listed if p)
    kept = [p for p in paths if not p.startswith(EXCLUDED_PREFIX) and p not in EXCLUDED_FILES]
    hits = []
    for relative in kept:
        source = DEV / relative
        target = repo / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        if COMPANY_NAMES.search(target.read_bytes()):
            hits.append(relative)
    if hits:
        raise SystemExit(f"company name found in exported files: {hits[:10]}")
    git(repo, "init", "-q")
    git(repo, "config", "core.autocrlf", "false")
    git(repo, "config", "core.filemode", "false")
    git(repo, "add", "-A", "-f")
    git(repo, "commit", "-q", "-m", "RideBound export for the final runs (ADR-089 GD5/GD6); see BUILD.json")
    tracked = sorted(p for p in git(repo, "ls-files", "-z").split("\0") if p)
    if tracked != kept:
        raise SystemExit("export commit does not hold exactly the exported files")
    return {"developmentWorktree": str(DEV), "baseCommit": git(DEV, "rev-parse", "HEAD"),
            "excludedPrefix": EXCLUDED_PREFIX, "excludedFilesListed": sorted(EXCLUDED_FILES),
            "gitVisibleFiles": len(paths), "exportedFiles": len(kept), "companyNameScan": "no hits",
            "exportCommit": git(repo, "rev-parse", "HEAD"), "exportIdentity": IDENTITY}


def publish_runner(repo: pathlib.Path, out: pathlib.Path) -> dict:
    sys.path.insert(0, str(PHASE1))
    import inventory
    before = inventory.repository_inventory_sha256(repo)
    subprocess.run([str(DOTNET / "dotnet.exe"), "publish", str(repo / "src" / "RideBound.Runner"), "-c", "Release",
                    "-o", str(out), "-m:1", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-v:q"], check=True)
    after = inventory.repository_inventory_sha256(repo)
    if before != after:
        raise SystemExit("publishing changed the repository inventory (bin/obj not ignored?)")
    runtime = json.loads((out / "RideBound.Runner.runtimeconfig.json").read_text(encoding="utf-8"))
    return {"command": "dotnet publish src/RideBound.Runner -c Release -m:1 -nodeReuse:false "
                       "-p:UseSharedCompilation=false", "runtimeconfig": runtime,
            "repositoryInventory": after, "files": {p.name: sha(p) for p in sorted(out.iterdir()) if p.is_file()}}


def copy_dotnet(out: pathlib.Path, framework_version: str) -> dict:
    major_minor = ".".join(framework_version.split(".")[:2]) + "."
    shared = DOTNET / "shared" / "Microsoft.NETCore.App"
    versions = sorted((p.name for p in shared.iterdir() if p.name.startswith(major_minor)),
                      key=lambda v: tuple(int(x) for x in v.split("-")[0].split(".")))
    if not versions:
        raise SystemExit(f"no Microsoft.NETCore.App {major_minor}x on this machine")
    chosen = versions[-1]   # what the default roll-forward (latest patch) picks here
    shutil.copy2(DOTNET / "dotnet.exe", out / "dotnet.exe")
    for name in ("LICENSE.txt", "ThirdPartyNotices.txt"):
        if (DOTNET / name).exists():
            shutil.copy2(DOTNET / name, out / name)
    shutil.copytree(DOTNET / "host", out / "host")
    shutil.copytree(shared / chosen, out / "shared" / "Microsoft.NETCore.App" / chosen)
    return {"source": str(DOTNET), "microsoftNetCoreApp": chosen, "dotnetExeSha256": sha(out / "dotnet.exe")}


def write_tools(tools: pathlib.Path, repo: pathlib.Path) -> dict:
    (tools / "world-via-full").mkdir(parents=True)
    launcher = (G3A_TOOLS / "bridge_launcher_via_full.py").read_text(encoding="utf-8")
    launcher = replace_once(launcher, r'ADAPTER_ROOT = r"E:\Code\RideBound-fastrun-run\simulators\fleetpy-ridebound"',
                            f'ADAPTER_ROOT = r"{repo.as_posix()}/simulators/fleetpy-ridebound"')
    (tools / "bridge_launcher_via_full.py").write_text(launcher, encoding="utf-8", newline="\n")
    world = (G3A_TOOLS / "world-via-full" / "preflight_world.py").read_text(encoding="utf-8")
    world = replace_once(world, 'REPO = pathlib.Path(r"E:/Code/RideBound-fastrun-run")',
                         f'REPO = pathlib.Path(r"{repo.as_posix()}")')
    (tools / "world-via-full" / "preflight_world.py").write_text(world, encoding="utf-8", newline="\n")
    for path in sorted((G3A_TOOLS / "world-via-full").glob("*.py")):
        if path.name != "preflight_world.py":
            shutil.copyfile(path, tools / "world-via-full" / path.name)
    shutil.copyfile(G3A_TOOLS / "inventory.py", tools / "inventory.py")
    shutil.copyfile(HERE / "run_queue_final.py", tools / "run_queue_final.py")
    return {p.relative_to(tools).as_posix(): sha(p) for p in sorted(tools.rglob("*.py"))}


def main() -> int:
    if ROOT.exists():
        raise SystemExit(f"refused: {ROOT} exists")
    ROOT.mkdir(parents=True)
    build = {"root": str(ROOT)}
    build["repo"] = export_repo(ROOT / "repo")
    build["runner"] = publish_runner(ROOT / "repo", ROOT / "runner")
    (ROOT / "dotnet").mkdir()
    build["dotnet"] = copy_dotnet(ROOT / "dotnet", build["runner"]["runtimeconfig"]["runtimeOptions"]["framework"]["version"])
    build["tools"] = write_tools(ROOT / "tools", ROOT / "repo")
    (ROOT / "BUILD.json").write_text(json.dumps(build, indent=2), encoding="utf-8")
    print(json.dumps({k: build[k] for k in ("repo",)}, indent=1))
    print("runner", build["runner"]["files"].get("RideBound.Runner.dll"), "dotnet", build["dotnet"]["microsoftNetCoreApp"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
