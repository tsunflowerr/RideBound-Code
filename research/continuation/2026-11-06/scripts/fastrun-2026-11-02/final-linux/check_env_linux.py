"""Checks the rented Linux machine after unpacking, before any job (2026-11-04), and records its environment.
Exit 0 only if every check holds:
  - Python is the bundled 3.10.22 and every pin of requirements-linux.txt is installed at exactly that version;
  - the .NET runtime is Microsoft.NETCore.App 10.0.9 (the home machine's runtime);
  - libicu is loadable (else .NET would fall back to invariant globalization, unlike the home machine);
  - the repository's git HEAD is the export commit and its inventory equals the home machine's value
    (inventory.py over E:/RideBoundData/final-v2/repo on 2026-11-04: same files, same bytes after unpacking).
Writes <out>/ENVIRONMENT.json (machine, OS, CPU, memory and cgroup limits, versions, installed packages).
Usage: python -I -B check_env_linux.py <out folder>
"""
import ctypes.util
import importlib.metadata
import json
import os
import pathlib
import platform
import re
import subprocess
import sys

ROOT = pathlib.Path("/rb")
LINUX = ROOT / "RideBoundData/final-v2-linux"
REPO = ROOT / "RideBoundData/final-v2/repo"
DOTNET = LINUX / "dotnet/runtime/dotnet"
REQUIREMENTS = LINUX / "python/requirements-linux.txt"
HEAD = "0eb9a6ca7b61bb1ade653bdc178fae5a1f102681"
INVENTORY = "271166f4101b98ca0d4366502122eea62072a06ce51cc2358a7d5bb335ae2316"
PYTHON = (3, 10, 22)
NETCORE = "Microsoft.NETCore.App 10.0.9"


def text(command: list) -> str:
    try:
        return subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, timeout=60,
                              errors="replace").stdout.strip()
    except (OSError, subprocess.SubprocessError) as error:
        return f"unavailable: {error}"


def read(path: str) -> str:
    try:
        return pathlib.Path(path).read_text(encoding="utf-8", errors="replace").strip()
    except OSError:
        return "unavailable"


def main() -> int:
    out = pathlib.Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    problems = []
    if sys.version_info[:3] != PYTHON:
        problems.append(f"python {platform.python_version()} is not {'.'.join(map(str, PYTHON))}")
    pins = {}
    for line in REQUIREMENTS.read_text(encoding="utf-8").splitlines():
        line = line.split("#", 1)[0].strip()
        if line:
            name, version = line.split("==")
            pins[name] = version
    for name, version in pins.items():
        try:
            installed = importlib.metadata.version(name)
        except importlib.metadata.PackageNotFoundError:
            installed = None
        if installed != version:
            problems.append(f"package {name}: installed {installed}, pinned {version}")
    runtimes = text([str(DOTNET), "--list-runtimes"])
    if not any(line.startswith(NETCORE + " ") for line in runtimes.splitlines()):
        problems.append(f".NET runtime {NETCORE} missing: {runtimes!r}")
    icu = ctypes.util.find_library("icuuc")
    if not icu:
        problems.append("libicu (libicuuc) not found: install the distribution's libicu package")
    head = text(["git", "-C", str(REPO), "rev-parse", "HEAD"])
    if head != HEAD:
        problems.append(f"repository HEAD {head!r} is not {HEAD} (if git reports dubious ownership, run the script as "
                        "the user who unpacked the bundle)")
    sys.path.insert(0, str(LINUX / "tools"))
    import inventory  # noqa: E402

    try:
        inventory_sha = inventory.repository_inventory_sha256(REPO.resolve())
    except Exception as error:  # noqa: BLE001 - recorded and reported as a failed check
        inventory_sha = f"failed: {error!r}"
    if inventory_sha != INVENTORY:
        problems.append(f"repository inventory {inventory_sha} is not the home value {INVENTORY}")
    meminfo = dict(re.findall(r"^(\w+):\s+(\d+) kB", read("/proc/meminfo"), re.M))
    record = {
        "checks": {"pass": not problems, "problems": problems},
        "machine": {"uname": platform.uname()._asdict(), "osRelease": read("/etc/os-release"),
                    "glibc": platform.libc_ver(), "cpuModel": sorted(set(re.findall(r"^model name\s*:\s*(.+)$",
                                                                                    read("/proc/cpuinfo"), re.M))),
                    "logicalCpus": os.cpu_count(), "affinityCpus": len(os.sched_getaffinity(0)),
                    "memTotalKiB": int(meminfo.get("MemTotal", 0)),
                    "cgroupMemoryMax": read("/sys/fs/cgroup/memory.max"),
                    "cgroupV1MemoryLimit": read("/sys/fs/cgroup/memory/memory.limit_in_bytes"),
                    "cgroupCpuMax": read("/sys/fs/cgroup/cpu.max"), "timezoneEnv": os.environ.get("TZ"),
                    "lang": os.environ.get("LANG"), "lcAll": os.environ.get("LC_ALL")},
        "python": {"version": platform.python_version(), "executable": sys.executable,
                   "packages": sorted(f"{d.metadata['Name']}=={d.version}" for d in importlib.metadata.distributions())},
        "dotnet": {"runtimes": runtimes, "info": text([str(DOTNET), "--info"])},
        "libicu": icu,
        "git": text(["git", "--version"]),
        "repository": {"head": head, "inventorySha256": inventory_sha},
    }
    (out / "ENVIRONMENT.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
    for problem in problems:
        print("PROBLEM:", problem)
    print(json.dumps({"pass": not problems, "python": platform.python_version(), "head": head,
                      "inventory": inventory_sha, "libicu": icu, "cpus": os.cpu_count(),
                      "memTotalGiB": round(int(meminfo.get("MemTotal", 0)) / 2**20, 1)}))
    return 0 if not problems else 1


if __name__ == "__main__":
    raise SystemExit(main())
