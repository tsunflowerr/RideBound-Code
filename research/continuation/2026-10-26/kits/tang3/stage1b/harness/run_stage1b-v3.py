"""Stage 1b driver, VERSION 3 (fixes Codex R28 N1, N3, N4, N5; optimize/codex-debate/codex-work/
R28-HARNESS-RECHECK-CODEX.md). v1 (run_stage1b.py) and v2 are kept unchanged. NOT RUN: no job has been started.

New in v3:
  N1  Gate 0 is behavioural-only (owner decision 2026-10-12): its verdict files are GATE0-BEHAVIOURAL-PASSED.txt /
      GATE0-BEHAVIOURAL-FAILED.txt (compare_equivalence_stage1b-v3.py, GATE0_DISCLOSURE).
  N3  Resolver closure: the freeze also records PATH, PATHEXT, SETUPTOOLS_USE_DISTUTILS and GIT_* (with the v2
      prefixes), requires PYTHONPATH / PYTHONHOME / PYTHONSTARTUP / PYTHONUSERBASE to be UNSET (redirects outside the
      declared trees), records the git executable that PATH resolves to and freezes the whole Git installation as a
      fifth external tree, and audits the interpreter's .pth files for paths outside the frozen trees. Observed input
      drift (before a job, after a phase) is a DURABLE stop: INPUT-DRIFT-STOP.txt (mode "x") is written and every
      later run / verdict is refused while it exists. After every phase (main included) the full freeze is verified
      again. Between those full checks the per-job check is a stat comparison (size and modification time) of every
      frozen file and tree file plus a scan for added tree files: it detects ordinary edits, not a same-size,
      same-mtime rewrite, and it does not authenticate the bytes a running job read.
  N4  Every input check is guarded: an exception becomes an input problem (and, in the comparator after the jobs
      started, a persisted failure verdict), never an escape.
  N5  AC power is rechecked inside the admission, after the queue and the stat/disk checks and immediately before
      the reservation and the launch; only one driver instance may run (an OS lock on run-instance.lock, released
      by the operating system when the process ends; reservations are per process).

Phases, jobs and the job command are exactly those of v1 (R17 A2 as corrected by R18 §2-3; Codex R22 §5-7):
  gate0 : 4 OFF jobs, arms U / C-30 x d20181116-s10-r1-w08 / -w17, archived labels "<arm>-<cell>-t38".
  smoke : 2 ON jobs, CF-U / CF-C-30 on the calibration cell d20181114-s10-r1-w08.
  main  : 68 ON jobs, CF-U / CF-C-30 on the 34 test cells of split-t38-v1.json.
  35 distinct cells in total (both Gate-0 cells are also test cells; R24:164).

New in v2:
  F2  stage1b-input-freeze.sha256 is verified as an EXACT, non-empty, duplicate-free inventory: every required file
      (the v1 targets plus the external-tree manifests), every external tree, every frozen environment variable and
      every required-absent path must be listed exactly once, and nothing else. The freeze now covers the external
      execution inputs (R24:80-86): the FleetPy tree (run_pilot.FLEETPY, without its .git/ metadata), the job
      interpreter's whole environment (run_pilot.PYTHON's prefix), the .NET host (dotnet.exe, host/fxr) and every
      installed Microsoft.NETCore.App (so the framework the framework-dependent Runner resolves cannot change), the
      environment variables that steer Python/.NET resolution or numerics (prefixes FROZEN_ENV_PREFIXES), and the
      absence of the interpreter's user site-packages. Each tree is frozen as a runner_manifest.py-format manifest
      (stage1b-external-<id>.sha256.tsv, mode "x") whose bytes are hashed in the freeze. The repository inventory
      check of every job (--expected-repository-inventory-sha256) is unchanged. There is NO archived baseline of
      these external trees from T3.8 (searched 2026-10-11); the freeze binds them from the freeze time on, it does
      not prove historical byte equality (disclosed in HARNESS-FIX-REPORT-R24.md).
      Verified in full before a phase and again by the comparator before any verdict; before every job a stat
      snapshot (size, mtime) of all frozen files and tree files is compared, and drift halts the phase.
  F3  Any *-FAILED.txt refuses every job-start path (run of any phase, and every job right before launch); a phase
      with its own verdict is closed (run gate0 after GATE0-PASSED, run smoke after SMOKE-PASSED). main also needs
      the integration inspection record SMOKE-INSPECTION.txt (R24 F4:114; see inspection_problems).
  F5  AC power only (local predicate; RB_ALLOW_BATTERY_MIN_PERCENT is refused, not honoured). STOP is checked on
      every pass of the power wait and again at admission. Admission right before launch, under one lock, rechecks
      STOP, the failure latches, the stat snapshot and disk space, and RESERVES 410 MiB per running job, so six
      workers cannot all spend the same free space: free - 51,200 MiB - reserved >= 410 MiB. 1,200-s deferral and
      keep-awake cleanup unchanged.
Usage: python -B run_stage1b-v3.py plan|report|freeze|run gate0|smoke|main
  plan   : dry check; lists jobs, states and every input problem; starts nothing, creates nothing
  freeze : writes the external-tree manifests and stage1b-input-freeze.sha256 (mode "x"); post-approval, before seal
"""
from __future__ import annotations

import concurrent.futures
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import threading
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent.parent
for sub in ("pilot", "w07", "t38", "world"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(TANG3))
sys.path.insert(0, str(HERE))


def _load(name: str, filename: str):
    if name in sys.modules:
        return sys.modules[name]
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


import canonical_wp4 as cj  # noqa: E402
import preflight_world  # noqa: E402
import preflight_world_v1_2 as v12  # noqa: E402
import run_pilot  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402

ce = _load("compare_equivalence_stage1b_v3", "compare_equivalence_stage1b-v3.py")

RUNNER = pathlib.Path(r"C:/RideBoundData/research/runner-tier3-v3")
RUNNER_MANIFEST = HERE / "runner-tier3-v3.sha256.tsv"
PROVENANCE = HERE / "RUNNER-PROVENANCE-v3.md"
OLD_RUNNER = run_w07.RUNNER                                   # runner-tier3-v2 (run_w07.py:40)
OLD_RUNNER_MANIFEST = TANG3 / "runner-tier3-v2.sha256.tsv"
OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-stage1b-v1")
REFERENCE_ROOT = run_t38.OUT_ROOT                             # C:/RideBoundData/research/tier3-t38-v1
WP4_OFF = run_w07.WP4                                         # tang3/configs/wp4-tier3-v2.json
WP4_ON = HERE / "wp4-tier3-v2-costfirst.json"
WP4_SHA256 = {"off": run_w07.WP4_SHA256,                      # 66e9b592...4518 (run_w07.py:43)
              "on": "37040657b8f6c16c8eb8430a208bfd4b155791668af0f1da49110f0f372300ae"}   # make_wp4_costfirst.log
WP4_CANONICAL = {"off": "fb2813c4036d309490e12df77e37de1683b92b92a9ebdd71e16cad35d50ecd42",
                 "on": "16dcb7e3337763aab7889f7528365dced2aa49028db6dbf390f8b3dce79b3140"}
WRAPPER_SHA256 = "df28b6cb6adf2c94e5b539f137bda674b8498046d56fb0c5f1ab4ec1937ca262"   # W07-PLAN.sha256:6
ARMS = ("U", "C-30")
GATE_CELLS = ("d20181116-s10-r1-w08", "d20181116-s10-r1-w17")   # R22:160-167
SMOKE_CELL = "d20181114-s10-r1-w08"                             # calibration cell (split-t38-v1.json)
TEST_CELLS = tuple(run_t38.SPLIT["test"])
SEAL = HERE / "STAGE1B-PLAN.sha256"
PLAN = HERE / "STAGE1B-PLAN.md"
FREEZE = HERE / "stage1b-input-freeze.sha256"
GATE0_PASSED, GATE0_FAILED = HERE / ce.GATE0_PASS_FILE, HERE / ce.GATE0_FAIL_FILE   # N1: behavioural-only names
GATE0_WORD = ce.GATE0_WORD                                                            # "GATE0 BEHAVIOURAL"
SMOKE_PASSED, SMOKE_FAILED = HERE / "SMOKE-PASSED.txt", HERE / "SMOKE-FAILED.txt"
INSPECTION = HERE / "SMOKE-INSPECTION.txt"
DRIFT_STOP = HERE / "INPUT-DRIFT-STOP.txt"           # N3: durable stop receipt for observed input drift
INSTANCE_LOCK = HERE / "run-instance.lock"           # N5: one driver instance at a time
LOG_ROOT = HERE / "logs"
STOP_FILE = HERE / "STOP"
POWER_WAIT_S = 1200
FLOOR_MIB, NEED_MIB = 51_200, 410
PHASES = ("gate0", "smoke", "main")
BATTERY_ENV = run_pilot.BATTERY_ENV                           # RB_ALLOW_BATTERY_MIN_PERCENT (run_pilot.py:41)

# ---- F2: external execution inputs
DOTNET_ROOT_DIR = pathlib.Path(run_pilot.DOTNET).parent
EXTERNAL_TREES = (  # (id, root, excluded relative prefixes)
    ("fleetpy", pathlib.Path(run_pilot.FLEETPY), (".git/",)),       # git metadata changes on `git status`
    ("python-env", pathlib.Path(run_pilot.PYTHON).parent, ()),       # sys.prefix of the job interpreter (observed)
    ("dotnet-netcore-app", DOTNET_ROOT_DIR / "shared" / "Microsoft.NETCore.App", ()),
    ("dotnet-host-fxr", DOTNET_ROOT_DIR / "host" / "fxr", ()),
    ("git", pathlib.Path(r"C:/Program Files/Git"), ()),             # N3: git resolved through PATH (observed
)                                                                    # 2026-10-12: 9,571 files, git 2.55.0.windows.3)
FROZEN_ENV_PREFIXES = ("PYTHON", "DOTNET_", "COREHOST_", "COMPLUS_", "OMP_", "MKL_", "OPENBLAS_", "RB_", "GIT_")
FROZEN_ENV_NAMES = frozenset({"PATH", "PATHEXT", "SETUPTOOLS_USE_DISTUTILS"})   # N3: resolver policy; the .pth
                                                     # distutils-precedence.pth:1 reads SETUPTOOLS_USE_DISTUTILS
FORBIDDEN_ENV = ("PYTHONPATH", "PYTHONHOME", "PYTHONSTARTUP", "PYTHONUSERBASE")  # N3: redirects outside the trees
REQUIRED_TOOLS = ("git",)                            # N3: bare names the jobs / checks resolve through PATH
PYTHON_SITE = pathlib.Path(run_pilot.PYTHON).parent / "Lib" / "site-packages"
DRIVER_SET_ENV = frozenset({"PYTHONDONTWRITEBYTECODE", "PYTHONIOENCODING", "RB_WORLD_CONFIG", "RB_FORECAST_CONFIG",
                            BATTERY_ENV})
# site.getusersitepackages() of the job interpreter, observed 2026-10-11 (ENABLE_USER_SITE True; directory absent)
USER_SITE = pathlib.Path(os.environ.get("APPDATA", r"C:/Users/quang/AppData/Roaming")) / "Python" / "Python310" / \
    "site-packages"
ABSENT_REQUIRED = (USER_SITE,)


def external_manifest(tree_id: str) -> pathlib.Path:
    return HERE / f"stage1b-external-{tree_id}.sha256.tsv"


SEALED_REQUIRED = ("STAGE1B-PLAN.md", "run_stage1b-v3.py", "compare_equivalence_stage1b-v3.py",
                   "test_compare_equivalence_stage1b-v3.py", "selftest-2026-10-13-v3.log", "canonical_wp4.py",
                   "make_wp4_costfirst.py", "make_wp4_costfirst.log", "wp4-tier3-v2-costfirst.json",
                   "runner-tier3-v3.sha256.tsv", "RUNNER-PROVENANCE-v3.md", "stage1b-input-freeze.sha256") + tuple(
    external_manifest(tree_id).name for tree_id, _, _ in EXTERNAL_TREES)


def jobs(phase: str):
    """(job name, arm, cell, WP4 mode, label), cell by cell so a cell's arms finish close together."""
    if phase == "gate0":
        return [(f"{arm}-{cell}", arm, cell, "off", f"{arm}-{cell}-t38") for cell in GATE_CELLS for arm in ARMS]
    cells = (SMOKE_CELL,) if phase == "smoke" else TEST_CELLS
    return [(f"CF-{arm}-{cell}", arm, cell, "on", f"CF-{arm}-{cell}-s1b") for cell in cells for arm in ARMS]


def sha256_file(path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def state_of(phase: str, name: str) -> str:
    output = OUT_ROOT / phase / name
    if not output.exists():
        return "pending"
    summary = output / "summary.json"
    if summary.exists():
        try:
            return "status-" + str(ce.strict_loads(summary.read_text(encoding="utf-8"), protocol=False)
                                   .get("status", "unknown"))
        except (ValueError, AttributeError, OSError):
            return "status-unreadable"
    return "exists-no-summary"


def command(phase: str, name: str, arm: str, cell: str, mode: str, label: str) -> list[str]:
    fixture = run_t38.FIXTURE_ROOT / cell
    return [
        str(run_pilot.PYTHON), "-B", str(run_w07.PREFLIGHT),
        "--label", label,
        "--fleetpy-root", str(run_pilot.FLEETPY),
        "--runner-root", str(RUNNER),
        "--dotnet", str(run_pilot.DOTNET),
        "--commitment-config", str(run_pilot.CONFIGS / f"{arm}.json"),
        "--wp4-config", str(WP4_OFF if mode == "off" else WP4_ON),
        "--scenario", str(fixture / "scenario-content.json"),
        "--derivative-manifest", str(fixture / "derivative-manifest.json"),
        "--normalization-report", str(fixture / "normalization-report.json"),
        "--selection-frame", str(fixture / "selection-frame.json"),
        "--driver", str(run_t38.DRIVER_ROOT / f"{cell}.driver.json"),
        "--output", str(OUT_ROOT / phase / name),
        "--repeats", "1",
        "--master-seed", "7",
        "--expected-repository-inventory-sha256", run_w07.EXPECTED_INVENTORY,
    ]


# ---------------------------------------------------------------- frozen inputs

def runner_problems() -> tuple[list[str], dict]:
    if not RUNNER_MANIFEST.exists():
        return [f"refused: {RUNNER_MANIFEST.name} does not exist (publish the Runner and write its manifest first)"], {}
    identity = ce.runner_identity_from_disk(RUNNER, RUNNER_MANIFEST, "runner-tier3-v3")
    problems = list(identity["problems"])
    if identity.get("dll") == ce.OLD_RUNNER_DLL or identity.get("tree") == ce.OLD_RUNNER_TREE:
        problems.append("runner-tier3-v3 has the archived v2 DLL or tree; it is not the cost-first build")
    if RUNNER.resolve() == pathlib.Path(OLD_RUNNER).resolve():
        problems.append("RUNNER points at runner-tier3-v2")
    if not PROVENANCE.exists():
        problems.append(f"{PROVENANCE.name} (filled runner provenance receipt) does not exist")
    else:
        text = PROVENANCE.read_text(encoding="utf-8")
        if identity.get("ok") and (identity["tree"] not in text or identity["dll"] not in text):
            problems.append(f"{PROVENANCE.name} does not record this tree hash and DLL SHA-256")
        unfilled = re.findall(r"`<[^`\n]*>`", text)
        if unfilled:
            problems.append(f"{PROVENANCE.name} still has {len(unfilled)} unfilled template field(s), e.g. {unfilled[0]}")
    return problems, identity


def config_problems() -> list[str]:
    problems = []
    for arm in ARMS:
        path = run_pilot.CONFIGS / f"{arm}.json"
        if not path.exists() or sha256_file(path) != run_t38.CONFIG_SHA256[arm]:
            problems.append(f"commitment config {arm} missing or SHA-256 mismatch")
    for mode, path in (("off", WP4_OFF), ("on", WP4_ON)):
        if not path.exists():
            problems.append(f"WP4 {mode} missing: {path}")
            continue
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != WP4_SHA256[mode]:
            problems.append(f"WP4 {mode} file SHA-256 mismatch")
        try:                                                  # N4: a malformed config is a problem, not an escape
            if cj.canonical_sha256(raw) != WP4_CANONICAL[mode]:
                problems.append(f"WP4 {mode} canonical SHA-256 mismatch")
            for arm in ARMS:
                commitment = run_pilot.CONFIGS / f"{arm}.json"
                if commitment.exists() and cj.binding(commitment.read_bytes(), raw) != ce.BINDINGS[(mode, arm)]:
                    problems.append(f"binding {mode}/{arm} differs from {ce.BINDINGS[(mode, arm)][:8]}...")
        except (cj.CanonicalJsonError, ValueError, UnicodeDecodeError) as error:
            problems.append(f"WP4 {mode} is not valid canonical JSON ({type(error).__name__}: {error})")
    if WP4_OFF.exists() and WP4_ON.exists():
        try:
            on = cj.parse_strict(WP4_ON.read_bytes())
            if on.pop("selectionObjectiveOrder", None) != "cost-first" or \
                    cj.canonical_bytes_of_value(on) != cj.canonical_bytes(WP4_OFF.read_bytes()):
                problems.append("WP4 ON is not exactly v2 + selectionObjectiveOrder=cost-first")
            if "selectionObjectiveOrder" in cj.parse_strict(WP4_OFF.read_bytes()):
                problems.append("WP4 OFF declares selectionObjectiveOrder (it must be absent, R22:50,232)")
        except (cj.CanonicalJsonError, ValueError, UnicodeDecodeError, AttributeError) as error:
            problems.append(f"WP4 ON/OFF comparison impossible ({type(error).__name__}: {error})")
    return problems


def harness_problems() -> list[str]:
    problems = []
    if not run_w07.PREFLIGHT.exists() or sha256_file(run_w07.PREFLIGHT) != WRAPPER_SHA256:
        problems.append("world wrapper v1.2 missing or SHA-256 differs from W07-PLAN.sha256")
    if run_w07.EXPECTED_INVENTORY != ce.HARNESS_INVENTORY:
        problems.append("expected repository inventory is not 0da63eaf...")
    if sha256_file(run_t38.HERE / "split-t38-v1.json") != run_t38.SPLIT_SHA256:
        problems.append("split manifest SHA-256 mismatch")
    try:
        head = subprocess.run(["git", "-C", str(run_pilot.REPO), "rev-parse", "HEAD"], capture_output=True,
                              text=True, encoding="ascii", timeout=10, check=True).stdout.strip()
        if head != ce.HARNESS_COMMIT:
            problems.append(f"harness root {run_pilot.REPO} HEAD {head} != {ce.HARNESS_COMMIT[:7]} (coreCommitSha)")
    except (OSError, subprocess.SubprocessError) as error:
        problems.append(f"cannot read the harness root HEAD: {error}")
    return problems


def cell_problems(cells) -> list[str]:
    """run_t38.py:146-167 for the given cells: inputs exist, fixture matches the driver, world wrapper accepts it."""
    problems = []
    saved = preflight_world.ALLOWED_WINDOW_STARTS
    preflight_world.ALLOWED_WINDOW_STARTS = v12.WINDOW_STARTS_V1_2
    try:
        for cell in cells:
            fixture = run_t38.FIXTURE_ROOT / cell
            driver = run_t38.DRIVER_ROOT / f"{cell}.driver.json"
            world = run_t38.world_file(cell)
            if not (fixture.exists() and driver.exists() and world.exists()):
                problems.append(f"missing fixture, driver or world for {cell}")
                continue
            try:
                declared = ce.strict_loads(driver.read_text(encoding="utf-8"),
                                           protocol=False)["sourceScenarioContentSha256"]
            except (ValueError, KeyError, TypeError) as error:
                problems.append(f"{cell}: driver file unreadable ({type(error).__name__}: {error})")
                continue
            if sha256_file(fixture / "scenario-content.json") != declared:
                problems.append(f"{cell}: fixture scenario SHA-256 differs from the driver")
            try:
                config = ce.strict_loads(world.read_text(encoding="utf-8"), protocol=False)
                checked = v12.check_measured_base(config, fixture / "scenario-content.json")
                base = config["base"]
                factors = preflight_world.measured_base(base["day"], 1.0, int(base["windowStart"]), 24)
                if checked != (f"2018-11-{cell[7:9]}", run_t38.window_start(cell)) or len(factors) != 24:
                    problems.append(f"{cell}: world check {checked}, {len(factors)} factors")
            except SystemExit as error:
                problems.append(f"{cell}: world refused: {error}")
            except (ValueError, KeyError, TypeError) as error:
                problems.append(f"{cell}: world file unreadable ({type(error).__name__}: {error})")
    finally:
        preflight_world.ALLOWED_WINDOW_STARTS = saved
    return problems


def reference_problems() -> list[str]:
    problems = []
    for name, (want, frames) in ce.REFERENCE_TRANSCRIPTS.items():
        path = REFERENCE_ROOT / name / "transcript-00.ndjson"
        if not path.exists() or sha256_file(path) != want:
            problems.append(f"Gate-0 reference {name} missing or not the R22-verified transcript")
    return problems


def freeze_targets() -> list[pathlib.Path]:
    """v1 targets (unchanged) + the external-tree manifests."""
    cells = sorted(set(TEST_CELLS) | {SMOKE_CELL})
    days = sorted({f"2018-11-{cell[7:9]}" for cell in cells})
    targets = [TANG3 / p for p in (
        "pilot/run_pilot.py", "w07/run_w07.py", "t38/run_t38.py", "t38/split-t38-v1.json", "runner_manifest.py",
        "world/preflight_world_v1_2.py", "world/preflight_world_v1_1.py", "world/preflight_world.py",
        "world/world_v1.py", "configs/U.json", "configs/C-30.json", "configs/wp4-tier3-v2.json",
        "evidence/audit_vep.py", "evidence/choice_metrics.py", "oracle/accounting.py", "pilot/pilot_analyze.py",
        "pilot/pilot_frozen.py", "sweep/sweep_analyze.py", "sweep/run_sweep.py")]
    targets += [WP4_ON, pathlib.Path(run_pilot.DOTNET)]
    targets += [preflight_world.FACTORS / f"{day}_tt_factors.csv" for day in days]
    targets += sorted({run_t38.world_file(cell) for cell in cells})
    targets += [run_t38.DRIVER_ROOT / f"{cell}.driver.json" for cell in cells]
    for cell in cells:
        targets += sorted(p for p in (run_t38.FIXTURE_ROOT / cell).rglob("*") if p.is_file())
    for name in ce.REFERENCE_TRANSCRIPTS:
        targets += [REFERENCE_ROOT / name / f for f in ("bundle-manifest.json",) + ce.BUNDLE_FILES]
    targets += [external_manifest(tree_id) for tree_id, _, _ in EXTERNAL_TREES]
    return targets


def tree_files(root: pathlib.Path, excludes=()) -> list[pathlib.Path]:
    out = []
    for directory, dirnames, filenames in os.walk(root):
        dirnames.sort()
        for filename in filenames:
            path = pathlib.Path(directory) / filename
            relative = path.relative_to(root).as_posix()
            if not any(relative.startswith(prefix) for prefix in excludes):
                out.append(path)
    return sorted(out, key=lambda p: p.relative_to(root).as_posix())


def tree_body(root: pathlib.Path, excludes=()) -> list[str]:
    """runner_manifest.py:17-23 line format (relative path, SHA-256, bytes), sorted, minus excluded prefixes."""
    return [f"{p.relative_to(root).as_posix()}\t{sha256_file(p)}\t{p.stat().st_size}"
            for p in tree_files(root, excludes)]


def frozen_env(environ=None) -> dict[str, str]:
    """The steering environment recorded in the freeze: the v2 prefixes plus GIT_* and the resolver names PATH,
    PATHEXT, SETUPTOOLS_USE_DISTUTILS (R28 N3), minus the variables the driver sets itself."""
    environ = os.environ if environ is None else environ
    return {name.upper(): value for name, value in environ.items()
            if (name.upper().startswith(FROZEN_ENV_PREFIXES) or name.upper() in FROZEN_ENV_NAMES)
            and name.upper() not in DRIVER_SET_ENV}


def forbidden_env(environ=None) -> list[str]:
    """N3: interpreter redirects that point outside the declared trees must be unset (their targets are not frozen)."""
    environ = os.environ if environ is None else environ
    upper = {name.upper() for name in environ}
    return [f"{name} is set; it redirects the job interpreter outside the frozen trees and must be unset"
            for name in FORBIDDEN_ENV if name in upper]


def resolve_tool(name: str, environ=None, which=None) -> str | None:
    """The executable a bare command name resolves to under the given PATH/PATHEXT (as the jobs and checks call it)."""
    environ = os.environ if environ is None else environ
    which = shutil.which if which is None else which
    path = {k.upper(): v for k, v in environ.items()}.get("PATH")
    found = which(name, path=path)
    return pathlib.Path(found).as_posix() if found else None


def pth_lines(site: pathlib.Path = None) -> list[tuple[str, str]]:
    """Every non-comment line of the interpreter's .pth files (site.py processes them at start-up)."""
    site = PYTHON_SITE if site is None else site
    out = []
    for pth in sorted(site.glob("*.pth")):
        for line in pth.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.strip() and not line.startswith("#"):
                out.append((pth.name, line))
    return out


def _inside(path: str, roots) -> bool:
    candidate = pathlib.PurePosixPath(pathlib.Path(path).as_posix().lower())
    for root in roots:
        base = pathlib.PurePosixPath(pathlib.Path(root).as_posix().lower())
        if candidate == base or base in candidate.parents:
            return True
    return False


def pth_redirect_problems(lines, site, roots) -> list[str]:
    """N3: a .pth path line adds a sys.path entry; it must stay inside the frozen trees. Import lines run code that
    lives in the frozen environment tree (their steering variable SETUPTOOLS_USE_DISTUTILS is frozen)."""
    problems = []
    for name, line in lines:
        if line.startswith(("import ", "import\t")):
            continue
        target = pathlib.Path(line.strip())
        target = target if target.is_absolute() else pathlib.Path(site) / target
        if not _inside(target.as_posix(), roots):
            problems.append(f"{name}: path entry {line.strip()!r} lies outside the frozen trees")
    return problems


FILE_LINE = re.compile(r"^([0-9a-f]{64})  (\S.*)$")
TREE_LINE = re.compile(r"^tree (\S+) ([0-9a-f]{64}) (\d+) (\S.*)$")
ENV_LINE = re.compile(r"^env ([^=\s]+)=(.*)$")
ABSENT_LINE = re.compile(r"^absent (\S.*)$")
TOOL_LINE = re.compile(r"^tool (\S+) (\S.*)$")


def freeze_problems(text: str | None = None, *, targets=None, trees=None, environ=None, absent=None,
                    sha=sha256_file, exists=lambda p: pathlib.Path(p).exists(), read=None, body=None,
                    which=None, pth=None, site=None) -> list[str]:
    """F2 + R28 N3/N4: exact verification of the freeze. Every dependency is injectable so the self-test runs in
    memory; an unreadable input is a problem, never an exception."""
    if text is None:
        if not FREEZE.exists():
            return [f"{FREEZE.name} does not exist (run `freeze` after approval, before sealing)"]
        text = FREEZE.read_text(encoding="utf-8")
    targets = freeze_targets() if targets is None else targets
    trees = EXTERNAL_TREES if trees is None else trees
    absent = ABSENT_REQUIRED if absent is None else absent
    read = (lambda p: pathlib.Path(p).read_text(encoding="utf-8")) if read is None else read
    body = tree_body if body is None else body
    site = PYTHON_SITE if site is None else site
    pth = (lambda: pth_lines(site)) if pth is None else pth
    problems, files, tree_lines, env_lines, absent_lines, tool_lines = [], {}, {}, {}, set(), {}
    for number, line in enumerate(text.splitlines(), start=1):
        if not line or line.startswith("#"):
            continue
        match = FILE_LINE.match(line)
        if match:
            if match.group(2) in files:
                problems.append(f"freeze line {number}: duplicate file entry {match.group(2)}")
            files[match.group(2)] = match.group(1)
            continue
        match = TREE_LINE.match(line)
        if match:
            if match.group(1) in tree_lines:
                problems.append(f"freeze line {number}: duplicate tree entry {match.group(1)}")
            tree_lines[match.group(1)] = (match.group(2), int(match.group(3)), match.group(4))
            continue
        match = ENV_LINE.match(line)
        if match:
            if match.group(1) in env_lines:
                problems.append(f"freeze line {number}: duplicate env entry {match.group(1)}")
            env_lines[match.group(1)] = match.group(2)
            continue
        match = ABSENT_LINE.match(line)
        if match:
            if match.group(1) in absent_lines:
                problems.append(f"freeze line {number}: duplicate absent entry {match.group(1)}")
            absent_lines.add(match.group(1))
            continue
        match = TOOL_LINE.match(line)
        if match:
            if match.group(1) in tool_lines:
                problems.append(f"freeze line {number}: duplicate tool entry {match.group(1)}")
            tool_lines[match.group(1)] = match.group(2)
            continue
        problems.append(f"freeze line {number}: malformed: {line[:80]!r}")
    expected = {pathlib.Path(p).as_posix() for p in targets}
    if not expected or not files:
        problems.append("freeze lists no file or the required file inventory is empty")
    missing, extra = sorted(expected - set(files)), sorted(set(files) - expected)
    if missing:
        problems.append(f"freeze does not cover {len(missing)} required file(s), e.g. {missing[:3]}")
    if extra:
        problems.append(f"freeze lists {len(extra)} file(s) outside the required inventory, e.g. {extra[:3]}")
    for path in sorted(expected & set(files)):
        try:
            if not exists(path) or sha(path) != files[path]:
                problems.append(f"frozen input changed or missing: {path}")
        except (OSError, ValueError) as error:
            problems.append(f"frozen input unreadable: {path} ({type(error).__name__}: {error})")
    want_trees = {tree_id: (pathlib.Path(root).as_posix(), excludes) for tree_id, root, excludes in trees}
    if set(tree_lines) != set(want_trees):
        problems.append(f"freeze trees {sorted(tree_lines)} != required external trees {sorted(want_trees)}")
    for tree_id in sorted(set(tree_lines) & set(want_trees)):
        tree_hash, count, root = tree_lines[tree_id]
        want_root, excludes = want_trees[tree_id]
        if root != want_root:
            problems.append(f"external tree {tree_id}: root {root} != {want_root}")
            continue
        manifest = external_manifest(tree_id)
        try:
            manifest_text = read(manifest)
        except (OSError, ValueError, KeyError):
            problems.append(f"external tree {tree_id}: manifest {manifest.name} unreadable")
            continue
        if manifest_text.split("\n", 1)[0] != f"# tree-sha256 {tree_hash} files {count}":
            problems.append(f"external tree {tree_id}: manifest header differs from the freeze line")
        if not exists(root):
            problems.append(f"external tree {tree_id}: root {root} missing")
            continue
        try:
            live = body(pathlib.Path(root), excludes)
        except (OSError, ValueError) as error:
            problems.append(f"external tree {tree_id}: unreadable ({type(error).__name__}: {error})")
            continue
        if ce.tree_text(live) != manifest_text:
            frozen = manifest_text.split("\n")[1:-1]
            changed = sorted(set(frozen) ^ set(live))
            problems.append(f"external tree {tree_id} differs from its frozen manifest ({len(changed)} differing "
                            f"line(s), e.g. {[c.split(chr(9))[0] for c in changed[:3]]})")
    current = frozen_env(environ)
    for name in sorted(set(current) | set(env_lines)):
        if name not in env_lines:
            problems.append(f"environment variable {name} is set now but was not set at freeze time")
        elif name not in current:
            problems.append(f"environment variable {name} was frozen but is not set now")
        elif current[name] != env_lines[name]:
            problems.append(f"environment variable {name} changed since the freeze")
    problems += forbidden_env(environ)
    if set(tool_lines) != set(REQUIRED_TOOLS):
        problems.append(f"freeze tools {sorted(tool_lines)} != required resolved tools {list(REQUIRED_TOOLS)}")
    roots = [pathlib.Path(root).as_posix() for _, root, _ in trees]
    for name in sorted(set(tool_lines) & set(REQUIRED_TOOLS)):
        resolved = resolve_tool(name, environ, which)
        if resolved != tool_lines[name]:
            problems.append(f"tool {name} now resolves to {resolved}, frozen {tool_lines[name]}")
        elif not _inside(resolved, roots):
            problems.append(f"tool {name} resolves outside the frozen trees: {resolved}")
    try:
        problems += pth_redirect_problems(pth(), site, roots)
    except (OSError, ValueError) as error:
        problems.append(f".pth audit impossible ({type(error).__name__}: {error})")
    want_absent = {pathlib.Path(p).as_posix() for p in absent}
    if absent_lines != want_absent:
        problems.append(f"freeze absent paths {sorted(absent_lines)} != required {sorted(want_absent)}")
    for path in sorted(want_absent):
        if exists(path):
            problems.append(f"required-absent path now exists (would change imports): {path}")
    return problems


def seal_problems() -> list[str]:
    if not SEAL.exists():
        return [f"refused: {SEAL.name} does not exist (the plan is not sealed)"]
    problems, listed = [], set()
    for line in SEAL.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        match = re.match(r"^([0-9a-f]{64}) [ *]?(\S+)", line)
        if not match:
            problems.append(f"seal line malformed: {line[:60]}")
            continue
        digest, name = match.group(1), match.group(2).lstrip("*")
        if name in listed:
            problems.append(f"seal lists {name} twice")
        listed.add(name)
        target = HERE / name
        if not target.exists() or sha256_file(target) != digest:
            problems.append(f"sealed file changed or missing: {name}")
    missing = [name for name in SEALED_REQUIRED if name not in listed]
    if missing:
        problems.append(f"seal does not cover {missing}")
    return problems


def latch_problems(phase: str, for_comparator: bool = False) -> list[str]:
    """F3 + R28 N3: any failure verdict or the input-drift stop receipt stops every job-start path and every verdict;
    a phase with its own pass verdict is closed. Used by `run`, by every job right before launch and by the
    comparator."""
    problems = [f"refused: {failed.name} exists; Stage 1b stops (R22:208, R17 G0)"
                for failed in (GATE0_FAILED, SMOKE_FAILED) if failed.exists()]
    if DRIFT_STOP.exists():
        problems.append(f"refused: {DRIFT_STOP.name} exists; frozen inputs drifted, Stage 1b stops (R28 N3)")
    own = {"gate0": GATE0_PASSED, "smoke": SMOKE_PASSED}.get(phase)
    if own is not None and own.exists():
        problems.append(f"refused: {own.name} exists; phase {phase} is closed")
    return problems


def verdict_problems(path, failed, word: str, identity: dict) -> list[str]:
    if failed.exists():
        return [f"refused: {failed.name} exists; Stage 1b stops (R22:208, R17 G0)"]
    if not path.exists():
        return [f"refused: {path.name} does not exist"]
    text = path.read_text(encoding="utf-8").splitlines()
    problems = []
    if not text or text[-1] != f"{word} PASSED":
        problems.append(f"{path.name} does not end with '{word} PASSED'")
    if not identity.get("ok") or f"runnerDllSha256 {identity['dll']}" not in text \
            or f"runnerTreeSha256 {identity['tree']}" not in text:
        problems.append(f"{path.name} was not written for this verified Runner build")
    return problems


def inspection_problems() -> list[str]:
    """F4 (R24:114): main needs the recorded integration inspection of the smoke report: SMOKE-INSPECTION.txt,
    written by the person who read the smoke report, with the lines `smokeVerdictSha256 <SHA-256 of
    SMOKE-PASSED.txt>`, `inspected-by: <names>` and `inspection: accepted`."""
    if not INSPECTION.exists():
        return [f"refused: {INSPECTION.name} does not exist (record the smoke integration inspection first)"]
    lines = INSPECTION.read_text(encoding="utf-8").splitlines()
    problems = []
    if not SMOKE_PASSED.exists() or f"smokeVerdictSha256 {sha256_file(SMOKE_PASSED)}" not in lines:
        problems.append(f"{INSPECTION.name} does not name the SHA-256 of {SMOKE_PASSED.name}")
    if "inspection: accepted" not in lines:
        problems.append(f"{INSPECTION.name} does not record 'inspection: accepted'")
    if not any(line.startswith("inspected-by: ") and line[len("inspected-by: "):].strip() for line in lines):
        problems.append(f"{INSPECTION.name} does not name the inspector ('inspected-by: ...')")
    return problems


def environment_problems(environ=None) -> list[str]:
    environ = os.environ if environ is None else environ
    if environ.get(BATTERY_ENV) is not None:
        return [f"refused: {BATTERY_ENV} is set; Stage 1b runs on AC power only (plan section 10) - unset it"]
    return []


def readiness_problems(phase: str) -> list[str]:
    """Not ready = not a verdict: unpublished runner, unsealed plan or no freeze (comparator writes nothing)."""
    problems = []
    if not RUNNER_MANIFEST.exists():
        problems.append(f"{RUNNER_MANIFEST.name} does not exist")
    if not SEAL.exists():
        problems.append(f"{SEAL.name} does not exist")
    if not FREEZE.exists():
        problems.append(f"{FREEZE.name} does not exist")
    return problems


def _guard(label: str, check, default=()):
    """N4: an input check that raises becomes an input problem."""
    try:
        return check()
    except Exception as error:
        return list(default) + [f"{label} could not be validated ({type(error).__name__}: {error})"]


def input_integrity_problems(phase: str) -> tuple[list[str], dict]:
    """Every frozen-input check of a phase (no latches, no verdicts); guarded (N4)."""
    cells = sorted({cell for _, _, cell, _, _ in jobs(phase)})
    try:
        runner, identity = runner_problems()
    except Exception as error:
        runner, identity = [f"runner identity could not be validated ({type(error).__name__}: {error})"], {}
    problems = list(runner)
    problems += _guard("WP4/commitment configs", config_problems)
    problems += _guard("harness root", harness_problems)
    problems += _guard("cell inputs", lambda: cell_problems(cells))
    if phase == "gate0":
        problems += _guard("Gate-0 references", reference_problems)
    problems += _guard("plan seal", seal_problems)
    problems += _guard("input freeze", freeze_problems)
    return problems, identity


def frozen_input_problems(phase: str, for_comparator: bool = False) -> list[str]:
    problems, identity = input_integrity_problems(phase)
    if not for_comparator:
        problems += latch_problems(phase) + environment_problems()
        if phase in ("smoke", "main"):
            problems += _guard("Gate-0 verdict", lambda: verdict_problems(GATE0_PASSED, GATE0_FAILED, GATE0_WORD,
                                                                          identity))
        if phase == "main":
            problems += _guard("smoke verdict", lambda: verdict_problems(SMOKE_PASSED, SMOKE_FAILED, "SMOKE",
                                                                        identity))
            problems += _guard("smoke inspection", inspection_problems)
    return problems


def write_receipt(path, lines: list[str]) -> bool:
    """Exclusive write of a stop receipt; False if it already exists (the stop is already latched)."""
    try:
        with open(path, "x", encoding="utf-8", newline="\n") as handle:
            handle.write("\n".join(lines) + "\n")
        return True
    except FileExistsError:
        return False


def record_input_drift(phase: str, where: str, problems: list[str]) -> None:
    """N3: observed input drift is a durable global stop, not a transient deferral."""
    stamp = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    write_receipt(DRIFT_STOP, [f"# INPUT DRIFT STOP written {stamp} by run_stage1b-v3.py ({phase}, {where})",
                               "Frozen inputs changed while Stage 1b was running; every later run and verdict is "
                               "refused. Owner review required (R28 N3)."] + [f"- {p}" for p in problems[:50]])


# ---------------------------------------------------------------- jobs (F5, R28 N5)

def free_mib() -> int:
    return shutil.disk_usage("C:\\").free // (1024 * 1024)


def ac_only() -> bool:
    """Strict local AC predicate: ignores the inherited battery override (run_pilot.py:73-81)."""
    status = run_pilot.power_status()
    return status is not None and status[0] == 1


def wait_for_power() -> bool:
    """True only on AC power with STOP absent at return; STOP is checked on every pass (R24:124)."""
    deadline = time.time() + POWER_WAIT_S
    while True:
        if STOP_FILE.exists():
            return False
        if ac_only():
            return not STOP_FILE.exists()
        if time.time() > deadline:
            return False
        time.sleep(30)


def frozen_paths() -> list[pathlib.Path]:
    paths = [pathlib.Path(p) for p in freeze_targets()] + [SEAL, FREEZE, RUNNER_MANIFEST]
    for tree_id, root, excludes in EXTERNAL_TREES:
        paths += tree_files(root, excludes)
    if RUNNER.is_dir():
        paths += tree_files(RUNNER)
    return paths


def stat_snapshot(paths) -> dict:
    snapshot = {}
    for path in paths:
        try:
            info = os.stat(path)
            snapshot[str(path)] = (info.st_size, info.st_mtime_ns)
        except OSError:
            snapshot[str(path)] = None
    return snapshot


def stat_drift(snapshot: dict) -> list[str]:
    """Metadata check between full verifications: size/mtime of every frozen file and tree file, added tree files,
    required-absent paths. It detects ordinary edits; it does not authenticate bytes (R28 N3)."""
    current = stat_snapshot(pathlib.Path(p) for p in snapshot)
    drift = [path for path, value in snapshot.items() if current.get(path) != value]
    drift += [pathlib.Path(p).as_posix() + " (now exists)" for p in ABSENT_REQUIRED if pathlib.Path(p).exists()]
    for tree_id, root, excludes in EXTERNAL_TREES:      # a file added to an external tree after the snapshot
        if pathlib.Path(root).is_dir():
            added = [str(p) for p in tree_files(root, excludes) if str(p) not in snapshot]
            drift += [f"{p} (added)" for p in added[:3]]
    return drift


class Admission:
    """F5 + R28 N3/N5: one lock; STOP, latches (incl. the drift stop), frozen-input stat drift, disk and finally AC
    power are rechecked immediately before launch, and 410 MiB is reserved per running job. Observed drift writes
    the durable INPUT-DRIFT-STOP.txt."""

    def __init__(self, phase: str, snapshot: dict | None):
        self.phase, self.snapshot = phase, snapshot
        self.lock = threading.Lock()
        self.reserved = 0
        self.halted = None

    def admit(self, name: str) -> str | None:
        with self.lock:
            if self.halted:
                return f"deferred-halted ({self.halted})"
            if STOP_FILE.exists():
                return "deferred-stop"
            latch = latch_problems(self.phase)
            if latch:
                self.halted = latch[0]
                return "deferred-latch"
            drift = stat_drift(self.snapshot) if self.snapshot is not None else []
            if drift:
                self.halted = f"frozen input drift: {drift[0]}"
                record_input_drift(self.phase, f"before job {name}", drift)
                return "stopped-input-drift"
            free = free_mib()
            if free - FLOOR_MIB - self.reserved < NEED_MIB:
                return f"deferred-disk ({free} MiB free, {self.reserved} MiB reserved)"
            if not ac_only():                    # N5: after the queue and the long stat scan, before the launch
                return "deferred-on-battery (AC lost before launch)"
            self.reserved += NEED_MIB
            return None

    def release(self) -> None:
        with self.lock:
            self.reserved -= NEED_MIB


ADMISSION: Admission | None = None
_INSTANCE_HANDLE = None


def _os_lock(handle) -> None:
    """Non-blocking exclusive lock of the first byte; the OS releases it when the process ends."""
    import msvcrt
    handle.seek(0)
    msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)


def acquire_instance_lock(opener=open, locker=None) -> None:
    """N5: reservations are per process, so only one `run` may be active at a time (any phase)."""
    global _INSTANCE_HANDLE
    locker = _os_lock if locker is None else locker
    handle = opener(INSTANCE_LOCK, "a+b")
    try:
        locker(handle)
    except OSError as error:
        handle.close()
        raise SystemExit(f"refused: another run_stage1b-v3.py instance holds {INSTANCE_LOCK.name} ({error})")
    _INSTANCE_HANDLE = handle


def run_job(phase: str, name: str, arm: str, cell: str, mode: str, label: str):
    if state_of(phase, name) != "pending":
        return name, state_of(phase, name)
    log = LOG_ROOT / phase / f"{name}.log"
    if log.exists():
        return name, "log-exists-pending"
    if STOP_FILE.exists():
        return name, "deferred-stop"
    if latch_problems(phase):
        return name, "deferred-latch"
    if free_mib() - FLOOR_MIB < NEED_MIB:
        return name, f"deferred-disk ({free_mib()} MiB free)"
    if not wait_for_power():
        return name, "deferred-stop" if STOP_FILE.exists() else "deferred-on-battery"
    admission = ADMISSION if ADMISSION is not None else Admission(phase, None)
    refusal = admission.admit(name)          # immediately before launch (R24:130; R28 N5)
    if refusal:
        return name, refusal
    try:
        env = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONIOENCODING="utf-8",
                   RB_WORLD_CONFIG=str(run_t38.world_file(cell)))
        env.pop("RB_FORECAST_CONFIG", None)
        started = time.time()
        completed = subprocess.run(command(phase, name, arm, cell, mode, label), cwd=str(run_pilot.REPO),
                                   check=False, capture_output=True, text=True, encoding="utf-8", errors="replace",
                                   env=env)
        with open(log, "x", encoding="utf-8", newline="\n") as handle:
            handle.write(f"returncode={completed.returncode} seconds={time.time() - started:.0f}\n"
                         + completed.stdout + completed.stderr)
    finally:
        admission.release()
    return name, state_of(phase, name)


def post_phase_problems(phase: str) -> list[str]:
    """N3: after every phase (main included) the full frozen-input verification runs again; drift writes the durable
    stop receipt."""
    problems, _ = input_integrity_problems(phase)
    if problems:
        record_input_drift(phase, "after the phase", problems)
    return problems


def write_freeze() -> int:
    problems = config_problems() + cell_problems(sorted(set(TEST_CELLS) | {SMOKE_CELL})) + reference_problems()
    problems += [f"refused: {p.name} exists (freeze before any verdict)" for p in
                 (GATE0_PASSED, GATE0_FAILED, SMOKE_PASSED, SMOKE_FAILED, DRIFT_STOP) if p.exists()]
    problems += [f"required-absent path exists: {p}" for p in ABSENT_REQUIRED if pathlib.Path(p).exists()]
    problems += forbidden_env()
    roots = [pathlib.Path(root).as_posix() for _, root, _ in EXTERNAL_TREES]
    tools = {name: resolve_tool(name) for name in REQUIRED_TOOLS}
    problems += [f"tool {name} does not resolve inside the frozen trees: {path}" for name, path in tools.items()
                 if path is None or not _inside(path, roots)]
    problems += pth_redirect_problems(pth_lines(), PYTHON_SITE, roots)
    if problems:
        raise SystemExit(f"freeze refused, input problems: {problems}")
    stamp = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    tree_lines = []
    for tree_id, root, excludes in EXTERNAL_TREES:
        lines = tree_body(root, excludes)
        text = ce.tree_text(lines)
        with open(external_manifest(tree_id), "x", encoding="utf-8", newline="\n") as handle:
            handle.write(text)
        tree_lines.append(f"tree {tree_id} {text.split()[2]} {len(lines)} {pathlib.Path(root).as_posix()}")
    lines = [f"# stage1b input freeze v3, written {stamp}; SHA-256 and absolute path of every input the jobs and "
             f"the comparator read, the external execution trees, the resolved tools, the steering environment and "
             f"required-absent paths"]
    for target in freeze_targets():
        lines.append(f"{sha256_file(target)}  {pathlib.Path(target).as_posix()}")
    lines += tree_lines
    lines += [f"tool {name} {path}" for name, path in sorted(tools.items())]
    lines += [f"env {name}={value}" for name, value in sorted(frozen_env().items())]
    lines += [f"absent {pathlib.Path(p).as_posix()}" for p in ABSENT_REQUIRED]
    with open(FREEZE, "x", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print(f"wrote {FREEZE.name}: {len(freeze_targets())} files, {len(tree_lines)} trees, {len(tools)} tools")
    return 0


def main() -> int:
    global ADMISSION
    if len(sys.argv) != 3 or sys.argv[1] not in ("plan", "report", "freeze", "run") or sys.argv[2] not in PHASES:
        print(__doc__)
        return 2
    mode, phase = sys.argv[1], sys.argv[2]
    items = jobs(phase)
    if mode in ("plan", "report"):
        for name, *_ in items:
            print(f"{name}\t{state_of(phase, name)}")
        states = [state_of(phase, name) for name, *_ in items]
        print(len(items), {state: states.count(state) for state in sorted(set(states))})
        if mode == "plan":
            problems = frozen_input_problems(phase)
            print("input problems:", "none" if not problems else "")
            for problem in problems:
                print("  -", problem)
            all_cells = {cell for p in PHASES for _, _, cell, _, _ in jobs(p)}
            print(f"distinct cells across all phases: {len(all_cells)}; this phase: "
                  f"{len({cell for _, _, cell, _, _ in items})}")
            print(f"Gate-0 verdict files (behavioural-only, R28 N1): {GATE0_PASSED.name} / {GATE0_FAILED.name}")
            print("external trees frozen by `freeze` (verified before a phase, after it, and by the comparator):")
            for tree_id, root, excludes in EXTERNAL_TREES:
                print(f"  {tree_id}: {pathlib.Path(root).as_posix()} exists {pathlib.Path(root).is_dir()} "
                      f"excludes {list(excludes)}")
            roots = [pathlib.Path(root).as_posix() for _, root, _ in EXTERNAL_TREES]
            for name in REQUIRED_TOOLS:
                path = resolve_tool(name)
                print(f"resolved tool {name}: {path} inside frozen trees {path is not None and _inside(path, roots)}")
            env = frozen_env()
            print(f"steering environment now (names): {sorted(env)}; PATH entries: "
                  f"{len(env.get('PATH', '').split(os.pathsep)) if 'PATH' in env else 0}")
            print(f"forbidden redirects set: {forbidden_env() or 'none'}")
            print(f".pth redirect audit: {pth_redirect_problems(pth_lines(), PYTHON_SITE, roots) or 'none'} "
                  f"({len(pth_lines())} .pth line(s))")
            for path in ABSENT_REQUIRED:
                print(f"required-absent: {pathlib.Path(path).as_posix()} exists {pathlib.Path(path).exists()}")
            print(f"drift stop receipt {DRIFT_STOP.name} exists: {DRIFT_STOP.exists()}")
            status = run_pilot.power_status()
            print(f"power status (ACLineStatus, percent): {status}; AC-only predicate {ac_only()}; "
                  f"{BATTERY_ENV} set: {os.environ.get(BATTERY_ENV) is not None}")
            print(f"C: free {free_mib()} MiB; floor {FLOOR_MIB} MiB + {NEED_MIB} MiB reserved per running job")
            print("example:", " ".join(command(phase, *items[0])))
            print("example env: RB_WORLD_CONFIG=" + str(run_t38.world_file(items[0][2])), "cwd", run_pilot.REPO)
        return 0
    if mode == "freeze":
        return write_freeze()
    acquire_instance_lock()
    problems = frozen_input_problems(phase)
    if problems:
        raise SystemExit(f"input problems: {problems}")
    ADMISSION = Admission(phase, stat_snapshot(frozen_paths()))
    (OUT_ROOT / phase).mkdir(parents=True, exist_ok=True)
    (LOG_ROOT / phase).mkdir(parents=True, exist_ok=True)
    run_pilot.keep_awake(True)
    started, deferred = time.time(), 0
    try:
        with concurrent.futures.ThreadPoolExecutor(run_pilot.WORKERS) as pool:
            futures = [pool.submit(run_job, phase, *item) for item in items]
            for done, future in enumerate(concurrent.futures.as_completed(futures), start=1):
                name, label = future.result()
                deferred += label.startswith(("deferred", "stopped")) or label == "log-exists-pending"
                print(f"[{time.time() - started:7.0f}s] {name:<34} {label:<24} ({done}/{len(items)})", flush=True)
    finally:
        run_pilot.keep_awake(False)
    post = post_phase_problems(phase)
    if post:
        print(f"POST-PHASE INPUT DRIFT ({len(post)} problem(s)); {DRIFT_STOP.name} written; Stage 1b stops:")
        for problem in post[:20]:
            print("  -", problem)
        return 4
    return 3 if deferred else 0


if __name__ == "__main__":
    raise SystemExit(main())
