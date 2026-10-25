"""Stage 1b driver (plan: STAGE1B-PLAN.md, to be sealed in STAGE1B-PLAN.sha256 before any job). NOT RUN YET.

Phases (owner-approved study; R17-FINAL-VERDICT.md A2 as corrected by R18-CONFIRM-CODEX.md §2-3; Codex R22 §5-7):
  gate0 : OFF equivalence, 4 jobs. Arms U and C-30 with the UNCHANGED v2 WP4 (key absent) on the two cells of R22 §5
          (d20181116-s10-r1-w08, -w17), new Runner. Job labels are the archived T3.8 labels "<arm>-<cell>-t38", so
          the runId is the archived one (as the forecast gates did, optimize/run_forecast.py:93).
  smoke : ON integration smokes, 2 jobs. CF-U and CF-C-30 (WP4 v2 + "selectionObjectiveOrder": "cost-first") on one
          calibration cell, d20181114-s10-r1-w08, so no test cell is previewed and no main job is duplicated.
          Refused until GATE0-PASSED.txt is valid.
  main  : 68 jobs. CF-U and CF-C-30 on the 34 T3.8 test cells (split-t38-v1.json "test"). Refused until both
          GATE0-PASSED.txt and SMOKE-PASSED.txt are valid (both written by compare_equivalence_stage1b.py).
Everything else is the T3.8 command (run_t38.py:84-103): world wrapper v1.2, fixtures-panelA-v1, panel-A drivers and
worlds, master seed 7, one repeat, cwd E:/Code/RideBound-deadline (frozen harness root, HEAD 1ce15e9, inventory
0da63eaf... passed as --expected-repository-inventory-sha256).
Runner: C:/RideBoundData/research/runner-tier3-v3 (published later). Its expected tree is read from
runner-tier3-v3.sha256.tsv in this folder (written at publish time by runner_manifest.py, then sealed); every
mode that could start a job refuses until that manifest exists and the published tree matches it byte for byte.
Outputs: C:/RideBoundData/research/tier3-stage1b-v1/<phase>/<job>; logs: logs/<phase>/<job>.log (mode "x").
Safety as run_t38.py (:106-132): AC power only (waits up to 20 min, then defers), STOP file next to this script,
outputs never reused, a pending job whose log exists is reported, not retried, Windows kept awake; plus the disk gate
of optimize/run_forecast.py:50,105-106,126-127 (C: free - 51,200 MiB must leave 410 MiB per job).
`run` also refuses unless STAGE1B-PLAN.sha256 matches every sealed file and stage1b-input-freeze.sha256 matches every
frozen input.
Usage: python -B run_stage1b.py plan|report|freeze|run gate0|smoke|main
  plan   : dry check; lists jobs, states and every input problem; starts nothing, creates nothing
  freeze : writes stage1b-input-freeze.sha256 (mode "x"); post-approval, before sealing
"""
from __future__ import annotations

import concurrent.futures
import hashlib
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent.parent
for sub in ("pilot", "w07", "t38", "world"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(TANG3))
sys.path.insert(0, str(HERE))

import canonical_wp4 as cj  # noqa: E402
import compare_equivalence_stage1b as ce  # noqa: E402
import preflight_world  # noqa: E402
import preflight_world_v1_2 as v12  # noqa: E402
import run_pilot  # noqa: E402
import run_t38  # noqa: E402
import run_w07  # noqa: E402

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
GATE0_PASSED, GATE0_FAILED = HERE / "GATE0-PASSED.txt", HERE / "GATE0-FAILED.txt"
SMOKE_PASSED, SMOKE_FAILED = HERE / "SMOKE-PASSED.txt", HERE / "SMOKE-FAILED.txt"
SEALED_REQUIRED = ("STAGE1B-PLAN.md", "run_stage1b.py", "compare_equivalence_stage1b.py",
                   "test_compare_equivalence_stage1b.py", "selftest-2026-10-11-final.log", "canonical_wp4.py",
                   "make_wp4_costfirst.py", "make_wp4_costfirst.log", "wp4-tier3-v2-costfirst.json",
                   "runner-tier3-v3.sha256.tsv", "RUNNER-PROVENANCE-v3.md", "stage1b-input-freeze.sha256")
LOG_ROOT = HERE / "logs"
STOP_FILE = HERE / "STOP"
POWER_WAIT_S = 1200
FLOOR_MIB, NEED_MIB = 51_200, 410
PHASES = ("gate0", "smoke", "main")


def jobs(phase: str):
    """(job name, arm, cell, WP4 mode, label), cell by cell so a cell's arms finish close together."""
    if phase == "gate0":
        return [(f"{arm}-{cell}", arm, cell, "off", f"{arm}-{cell}-t38") for cell in GATE_CELLS for arm in ARMS]
    cells = (SMOKE_CELL,) if phase == "smoke" else TEST_CELLS
    return [(f"CF-{arm}-{cell}", arm, cell, "on", f"CF-{arm}-{cell}-s1b") for cell in cells for arm in ARMS]


def sha256_file(path: pathlib.Path) -> str:
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
        return "status-" + json.loads(summary.read_text(encoding="utf-8")).get("status", "unknown")
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
    elif identity.get("ok"):
        text = PROVENANCE.read_text(encoding="utf-8")
        if identity["tree"] not in text or identity["dll"] not in text:
            problems.append(f"{PROVENANCE.name} does not record this tree hash and DLL SHA-256")
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
        if cj.canonical_sha256(raw) != WP4_CANONICAL[mode]:
            problems.append(f"WP4 {mode} canonical SHA-256 mismatch")
        for arm in ARMS:
            commitment = run_pilot.CONFIGS / f"{arm}.json"
            if commitment.exists() and cj.binding(commitment.read_bytes(), raw) != ce.BINDINGS[(mode, arm)]:
                problems.append(f"binding {mode}/{arm} differs from {ce.BINDINGS[(mode, arm)][:8]}...")
    if WP4_OFF.exists() and WP4_ON.exists():
        on = cj.parse_strict(WP4_ON.read_bytes())
        if on.pop("selectionObjectiveOrder", None) != "cost-first" or \
                cj.canonical_bytes_of_value(on) != cj.canonical_bytes(WP4_OFF.read_bytes()):
            problems.append("WP4 ON is not exactly v2 + selectionObjectiveOrder=cost-first")
        if "selectionObjectiveOrder" in cj.parse_strict(WP4_OFF.read_bytes()):
            problems.append("WP4 OFF declares selectionObjectiveOrder (it must be absent, R22:50,232)")
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
            declared = json.loads(driver.read_text(encoding="utf-8"))["sourceScenarioContentSha256"]
            if sha256_file(fixture / "scenario-content.json") != declared:
                problems.append(f"{cell}: fixture scenario SHA-256 differs from the driver")
            try:
                config = json.loads(world.read_text(encoding="utf-8"))
                checked = v12.check_measured_base(config, fixture / "scenario-content.json")
                base = config["base"]
                factors = preflight_world.measured_base(base["day"], 1.0, int(base["windowStart"]), 24)
                if checked != (f"2018-11-{cell[7:9]}", run_t38.window_start(cell)) or len(factors) != 24:
                    problems.append(f"{cell}: world check {checked}, {len(factors)} factors")
            except SystemExit as error:
                problems.append(f"{cell}: world refused: {error}")
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
    return targets


def freeze_problems() -> list[str]:
    if not FREEZE.exists():
        return [f"{FREEZE.name} does not exist (run `freeze` after approval, before sealing)"]
    problems = []
    for line in FREEZE.read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        digest, path = line.split("  ", 1)
        target = pathlib.Path(path)
        if not target.exists() or sha256_file(target) != digest:
            problems.append(f"frozen input changed or missing: {path}")
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
        listed.add(name)
        target = HERE / name
        if not target.exists() or sha256_file(target) != digest:
            problems.append(f"sealed file changed or missing: {name}")
    missing = [name for name in SEALED_REQUIRED if name not in listed]
    if missing:
        problems.append(f"seal does not cover {missing}")
    return problems


def verdict_problems(path: pathlib.Path, failed: pathlib.Path, word: str, identity: dict) -> list[str]:
    if failed.exists():
        return [f"refused: {failed.name} exists; Stage 1b stops (R22:208, R17 G0)"]
    if not path.exists():
        return [f"refused: {path.name} does not exist"]
    text = path.read_text(encoding="utf-8").splitlines()
    problems = []
    if not text or text[-1] != f"{word} PASSED":
        problems.append(f"{path.name} does not end with '{word} PASSED'")
    if identity.get("ok") and (f"runnerDllSha256 {identity['dll']}" not in text
                               or f"runnerTreeSha256 {identity['tree']}" not in text):
        problems.append(f"{path.name} was written for another Runner build")
    return problems


def frozen_input_problems(phase: str, for_comparator: bool = False) -> list[str]:
    cells = sorted({cell for _, _, cell, _, _ in jobs(phase)})
    runner, identity = runner_problems()
    problems = runner + config_problems() + harness_problems() + cell_problems(cells)
    if phase == "gate0":
        problems += reference_problems()
    problems += seal_problems() + freeze_problems()
    if not for_comparator and phase in ("smoke", "main"):
        problems += verdict_problems(GATE0_PASSED, GATE0_FAILED, "GATE0", identity)
    if not for_comparator and phase == "main":
        problems += verdict_problems(SMOKE_PASSED, SMOKE_FAILED, "SMOKE", identity)
    return problems


# ---------------------------------------------------------------- jobs

def free_mib() -> int:
    return shutil.disk_usage("C:\\").free // (1024 * 1024)


def wait_for_power() -> bool:
    deadline = time.time() + POWER_WAIT_S
    while not run_pilot.may_start_job():
        if time.time() > deadline or STOP_FILE.exists():
            return False
        time.sleep(30)
    return True


def run_job(phase: str, name: str, arm: str, cell: str, mode: str, label: str):
    if state_of(phase, name) != "pending":
        return name, state_of(phase, name)
    log = LOG_ROOT / phase / f"{name}.log"
    if log.exists():
        return name, "log-exists-pending"
    if STOP_FILE.exists():
        return name, "deferred-stop"
    if free_mib() - FLOOR_MIB < NEED_MIB:
        return name, f"deferred-disk ({free_mib()} MiB free)"
    if not wait_for_power():
        return name, "deferred-on-battery"
    env = dict(os.environ, PYTHONDONTWRITEBYTECODE="1", PYTHONIOENCODING="utf-8",
               RB_WORLD_CONFIG=str(run_t38.world_file(cell)))
    env.pop("RB_FORECAST_CONFIG", None)
    started = time.time()
    completed = subprocess.run(command(phase, name, arm, cell, mode, label), cwd=str(run_pilot.REPO), check=False,
                               capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
    with open(log, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(f"returncode={completed.returncode} seconds={time.time() - started:.0f}\n"
                     + completed.stdout + completed.stderr)
    return name, state_of(phase, name)


def write_freeze() -> int:
    problems = config_problems() + cell_problems(sorted(set(TEST_CELLS) | {SMOKE_CELL})) + reference_problems()
    if problems:
        raise SystemExit(f"freeze refused, input problems: {problems}")
    lines = [f"# stage1b input freeze, written {time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}; "
             f"SHA-256 and absolute path of every input the jobs and the comparator read"]
    for target in freeze_targets():
        lines.append(f"{sha256_file(target)}  {target.as_posix()}")
    with open(FREEZE, "x", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print(f"wrote {FREEZE.name}: {len(lines) - 1} files")
    return 0


def main() -> int:
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
            print(f"C: free {free_mib()} MiB; floor {FLOOR_MIB} MiB + {NEED_MIB} MiB per job")
            print("example:", " ".join(command(phase, *items[0])))
            print("example env: RB_WORLD_CONFIG=" + str(run_t38.world_file(items[0][2])), "cwd", run_pilot.REPO)
        return 0
    if mode == "freeze":
        return write_freeze()
    problems = frozen_input_problems(phase)
    if problems:
        raise SystemExit(f"input problems: {problems}")
    (OUT_ROOT / phase).mkdir(parents=True, exist_ok=True)
    (LOG_ROOT / phase).mkdir(parents=True, exist_ok=True)
    run_pilot.keep_awake(True)
    started, deferred = time.time(), 0
    try:
        with concurrent.futures.ThreadPoolExecutor(run_pilot.WORKERS) as pool:
            futures = [pool.submit(run_job, phase, *item) for item in items]
            for done, future in enumerate(concurrent.futures.as_completed(futures), start=1):
                name, label = future.result()
                deferred += label.startswith("deferred") or label == "log-exists-pending"
                print(f"[{time.time() - started:7.0f}s] {name:<34} {label:<24} ({done}/{len(items)})", flush=True)
    finally:
        run_pilot.keep_awake(False)
    return 3 if deferred else 0


if __name__ == "__main__":
    raise SystemExit(main())
