"""Stage 1b analysis, step 2 of 2: tables, primary endpoint, guards and predictions from extract_stage1b.py outputs.

Adapted copy of optimize/codex-debate/claude-work/r21-stage1a/report_stage1a.py (Stage 1a). Reads <prefix>-riders.tsv
and <prefix>-runs.json only for outcomes (no transcript/reader import). Main mode authenticates and executes the bound
extractor's source bytes for the shared current-state checker; its runtime dependencies are standard library only.
Plan: sealed STAGE1B-PLAN.md
sections 5 (primary endpoint), 6 (sealed predictions; an internal record only), 7 (guards for every arm), 8 (analysis).
Written 2026-10-14 before any Stage-1b output existed.

Definitions (report_stage1a.py:4-10,56-70 and plan section 5; roles CF-U, CF-C30, SF-U, SF-C30):
  requests  = 108 per run, 3,672 per role (34 cells); accepted = riders with a drop initialPromise (rider rows);
  served S  = lifecycle "completed" AND a passengerAlighted event (extractor column `served`);
  p0 = FIRST drop promise, t0 = its publication time, A = alight time (sweep_analyze.py:40-57);
  naive late_k L = #served with A - p0 > k s (k = 60, 120, 300); timely_k T = S - late_k (A - p0 <= k s);
  early60 = #served with A - p0 < -60 s;
  forecast quote (frozen causal-no15, D0 = display_layer.display(t0, p0, profile); naive in inactive cells; no
  retuning): fc_late_k = A - D0 > k s, fc_timely_k = S - fc_late_k, fc_early60 = A - D0 < -60 s;
  mean quote extension = mean (D0 - p0) over served riders;
  initial promise horizon = p0 - t0 ("median initial promised ride time" of plan section 7; R16-CODEX.md:161 "initial
  quote horizon"), median = nearest rank (budget_use_check.q), over accepted riders (and, as a check, over served);
  dispatch use over ACCEPTED riders: S_r = sum |c| of later drop revisions, exposed = S_r > 0, repeated = >= 2 nonzero
  c, overshoot (C30 roles) = S_r > 30 s; H7 screen = served riders with max |c| <= 120 s < S_r
  (cumulative_vs_perinsertion.py:72-77 "cumulative-ONLY", here over served riders as plan section 6 says);
  shared completion = served rider with another rider on board the same vehicle at an overlapping time;
  main evidence with completed-without-alight anomalies is refused, making the completed/served H7 cohorts identical.
Primary endpoint (plan section 5; R18:236-247):
  I_L = (L_CF,U - L_CF,C30) - (L_SF,U - L_SF,C30);  I_served and I_timely60 likewise;  I_L = I_served - I_timely60
  (checked exactly, pooled, per cell, per window and per day). The SF values are recomputed from the archived runs and
  must equal the plan's constants (2,434 / 2,338 served, 203 / 76 late60, 2,231 / 2,262 timely60) first; any baseline
  mismatch stops the report before the CF tables (as report_stage1a.py:111-122).
Cell / day handling (plan section 5; R18:247,251): pooled counts; per-cell interactions and sign counts over the 34 cells,
  over the 25 w08/w17 cells (H4) and the 9 w07 cells; per-window and per-day (5 days) pooled interactions and their
  signs; leave-one-day-out I_L. No interval is computed: five day clusters are not 34 independent replicates.
Every number is printed with its count and denominator.

Usage: python -B report_stage1b.py <prefix> <out.txt>   (out opened with mode "x"; also printed)
  main-mode extraction: current prerequisites, latches, runner identity and full retained analysis inventory are
  rechecked before analysis and immediately before output by the bound extractor's shared checker. Strict mode,
  dryRun and source metadata are required. Every dry-run section labels its "CF" rows as allowed archived stand-ins.
Exit status: 0 report written; 1 written but the SF baselines did not reproduce (stopped before CF tables) or the
  identity/accounting validity failed; 2 refused (nothing written). H3 and promise guards with undefined quantities
  are NOT EVALUABLE, never PASS/met. Ledger arithmetic defects fail accounting validity separately from H1 recovery.
"""
from __future__ import annotations

import collections
import hashlib
import json
import math
import os
import pathlib
import re
import statistics
import sys
import types

sys.dont_write_bytecode = True

HERE = pathlib.Path(__file__).resolve().parent
BINDING = HERE / "stage1b-analysis-binding.txt"
OUTPUT_FORBIDDEN = (pathlib.Path(r"C:/RideBoundData"), pathlib.Path(r"E:/RideBoundData"))
ROLES = ("CF-U", "CF-C30", "SF-U", "SF-C30")
STANDINS = ("SF-U", "SF-C30", "SF-C120")
CELL_OF_ROLE = {"SF-U": ("SF", "U"), "SF-C30": ("SF", "C30"), "CF-U": ("CF", "U"), "CF-C30": ("CF", "C30")}
CAP_MS = {"CF-U": None, "CF-C30": 30_000, "SF-U": None, "SF-C30": 30_000}
REQUESTS_PER_RUN, CELLS_EXPECTED = 108, 34
WINDOWS, H4_WINDOWS, H4_CELLS = ("w07", "w08", "w17"), ("w08", "w17"), 25
H7_B_MS = 120_000
HEADER = ["role", "cell", "requestId", "lifecycle", "t0", "p0", "D0", "A", "served", "sumAbsC", "nonzeroC", "netC",
          "maxAbsC", "shared"]
ADDITIVE = ("requests", "accepted", "served", "late60", "late120", "late300", "fc_late60", "fc_late120", "fc_late300",
            "early60", "fc_early60", "timely60", "timely120", "fc_timely60", "fc_timely120", "shared", "exposed",
            "repeated", "h7_served", "h7_accepted")
PLAN_SF = {  # plan section 5 (R18 rows 26-28) and section 6 (R18 rows 33, 37; C120R:15-25)
    ("SF-U", "served"): 2434, ("SF-C30", "served"): 2338, ("SF-U", "late60"): 203, ("SF-C30", "late60"): 76,
    ("SF-U", "timely60"): 2231, ("SF-C30", "timely60"): 2262,
}
BASELINES = {**PLAN_SF, **{
    ("SF-U", "late120"): 102, ("SF-C30", "late120"): 11,           # stage1a-report-2026-10-10.txt:13-14
    ("SF-U", "timely120"): 2332, ("SF-C30", "timely120"): 2327,    # :17-18
    ("SF-U", "fc_late60"): 137, ("SF-C30", "fc_late60"): 10,       # plan section 6; :19-20
    ("SF-U", "fc_timely60"): 2297, ("SF-C30", "fc_timely60"): 2328,  # plan section 6; :21-22
    ("SF-U", "exposed"): 174, ("SF-C30", "exposed"): 33,           # plan section 6 (R18 row 37)
    ("SF-U", "shared"): 555, ("SF-C30", "shared"): 313,            # plan section 6 (C120R:25)
    ("SF-U", "h7_served"): 4,                                      # R17:525 / CVP:17 (U, B = 120 s)
}}
SF_W0817_U_GT_C30 = 25     # plan H4: SF U > C30 late60 in 25/25 w08/w17 cells (R18 row 36)


class Refusal(Exception):
    pass


def q(xs, p):
    """Verbatim copy of optimize/codex-debate/claude-work/budget_use_check.py:31-37 (nearest-rank percentile)."""
    if not xs:
        return float("nan")
    xs = sorted(xs)
    k = max(1, min(len(xs), int(-(-p * len(xs) // 1))))
    return xs[k - 1]


def nd(n, d, digits=2) -> str:
    """A count with its denominator."""
    return f"{n} / {d} ({100 * n / d:.{digits}f}%)" if d else f"{n} / {d} (n/a)"


def sha256_file(path) -> str:
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


def norm(path) -> str:
    return os.path.normcase(os.path.abspath(os.fspath(path)))


def signs(values) -> tuple[int, int, int]:
    values = list(values)
    return sum(v > 0 for v in values), sum(v == 0 for v in values), sum(v < 0 for v in values)


def sign_text(values, unit) -> str:
    pos, zero, neg = signs(values)
    return f"> 0 in {pos}, = 0 in {zero}, < 0 in {neg} of {pos + zero + neg} {unit}"


def interaction(v: dict) -> int:
    return (v["CF-U"] - v["CF-C30"]) - (v["SF-U"] - v["SF-C30"])


# ------------------------------------------------------------------------------------------ inputs

def metadata_problems(doc: dict) -> list[str]:
    """Accept the two real extractor modes and their exact role mappings, with strict flag types."""
    mode, dry, sources = doc.get("mode"), doc.get("dryRun"), doc.get("roleSources")
    problems = []
    if type(mode) is not str or type(dry) is not bool or (mode, dry) not in (("main", False), ("dryrun", True)):
        problems.append("mode/dryRun must be exactly ('main', false) or ('dryrun', true)")
    if type(sources) is not dict or set(sources) != set(ROLES) or any(type(v) is not str for v in sources.values()):
        problems.append("roleSources must map exactly the four role names to string sources")
    elif mode == "main" and sources != {role: role for role in ROLES}:
        problems.append("main roleSources must name the corresponding real CF/SF source for every role")
    elif mode == "dryrun" and (sources["SF-U"] != "SF-U" or sources["SF-C30"] != "SF-C30"
                               or any(sources[r] not in STANDINS for r in ("CF-U", "CF-C30"))):
        problems.append(f"dryrun SF sources must remain SF-U/SF-C30 and each CF stand-in must be in {STANDINS}")
    return problems


def require_metadata(doc: dict):
    problems = metadata_problems(doc)
    if problems:
        raise Refusal("; ".join(problems))


def require_run_sources(doc: dict, records):
    """Every recorded source must agree with its validated role mapping, including dry-run stand-ins."""
    for run in records:
        if type(run) is not dict or type(run.get("role")) is not str or run["role"] not in ROLES:
            raise Refusal("each run must be an object with a string role from the four allowed roles")
        role, source = run["role"], run.get("source")
        if type(source) is not str or source != doc["roleSources"][role]:
            raise Refusal(f"run {role} {run.get('cell')}: source {source!r} does not equal "
                          f"roleSources[{role!r}] {doc['roleSources'][role]!r}")


def load(prefix: str):
    doc = json.loads(pathlib.Path(f"{prefix}-runs.json").read_text(encoding="utf-8"))
    if doc.get("schema") != "stage1b-extract-v1":
        raise Refusal(f"{prefix}-runs.json is not a stage1b-extract-v1 document")
    require_metadata(doc)
    if type(doc.get("runs")) is not list:
        raise Refusal("runs.json must hold a list of run records")
    require_run_sources(doc, doc["runs"])
    raw = pathlib.Path(f"{prefix}-riders.tsv").read_bytes()
    if hashlib.sha256(raw).hexdigest() != doc["ridersTsv"]["sha256"]:
        raise Refusal(f"{prefix}-riders.tsv is not the file recorded in {prefix}-runs.json (SHA-256 differs)")
    lines = raw.decode("utf-8").split("\n")
    if lines[0].split("\t") != HEADER:
        raise Refusal("riders.tsv header is not the stage1b-extract-v1 header")
    riders = {role: [] for role in ROLES}
    for line in lines[1:]:
        if not line:
            continue
        r = dict(zip(HEADER, line.split("\t")))
        if r["role"] not in riders:
            raise Refusal(f"unknown role {r['role']!r} in riders.tsv")
        rec = {"cell": r["cell"], "id": r["requestId"], "life": r["lifecycle"], "t0": int(r["t0"]), "p0": int(r["p0"]),
               "D0": float(r["D0"]), "A": int(r["A"]) if r["A"] else None, "served": r["served"] == "1",
               "S": int(r["sumAbsC"]), "k": int(r["nonzeroC"]), "net": int(r["netC"]), "maxc": int(r["maxAbsC"]),
               "shared": r["shared"] == "1"}
        if rec["served"] and rec["A"] is None:
            raise Refusal(f"{r['role']} {r['cell']} {r['requestId']}: served without an alight time")
        riders[r["role"]].append(rec)
    if sum(len(v) for v in riders.values()) != doc["ridersTsv"]["rows"]:
        raise Refusal("riders.tsv row count differs from runs.json")
    cells = doc["cells"]
    runs = {}
    for rec in doc["runs"]:
        key = (rec["role"], rec["cell"])
        if key in runs:
            raise Refusal(f"run {key} listed twice")
        runs[key] = rec
    if len(cells) != CELLS_EXPECTED or set(runs) != {(role, c) for role in ROLES for c in cells}:
        raise Refusal("runs.json does not hold exactly the 4 roles x 34 cells")
    return doc, riders, runs, cells


def binding_problems(doc: dict) -> list[str]:
    """Authenticate the shared checker source, then enforce every current prerequisite and retained file.

    The only project dependency is the bound extractor. Compile the exact SHA-256 checked source bytes rather than
    importing its name; an adjacent or cached .pyc cannot replace the checker. Both commands use the same state API.
    """
    try:
        if not BINDING.is_file():
            raise Refusal(f"{BINDING.name} does not exist")
        binding_bytes = BINDING.read_bytes()
        if doc.get("studyState", {}).get("bindingSha256") != hashlib.sha256(binding_bytes).hexdigest():
            raise Refusal(f"{BINDING.name} changed since the extraction")
        extractor = doc.get("extractor", {})
        if extractor.get("file") != "extract_stage1b.py":
            raise Refusal("main extraction must name the bound extract_stage1b.py source")
        pins = collections.defaultdict(list)
        for line in binding_bytes.decode("utf-8").splitlines():
            match = re.fullmatch(r"([0-9a-f]{64})  ([A-Za-z0-9][A-Za-z0-9._-]*)", line)
            if match:
                pins[match.group(2)].append(match.group(1))
        report_name = pathlib.Path(__file__).name
        if pins[report_name] != [sha256_file(__file__)]:
            raise Refusal(f"{report_name} is not uniquely listed in {BINDING.name} with its current SHA-256")
        if pins["extract_stage1b.py"] != [extractor.get("sha256")]:
            raise Refusal(f"the extraction was made by an extractor not uniquely bound in {BINDING.name}")
        source_path = BINDING.parent / "extract_stage1b.py"
        source = source_path.read_bytes()
        if hashlib.sha256(source).hexdigest() != extractor.get("sha256"):
            raise Refusal("bound extract_stage1b.py source bytes changed since extraction")
        shared = types.ModuleType("_stage1b_bound_extractor_checker")
        shared.__file__ = str(source_path.resolve())
        exec(compile(source, shared.__file__, "exec", dont_inherit=True), shared.__dict__)
        try:
            shared.require_main_state(expected=doc["studyState"], harness=BINDING.parent)
        except shared.Refusal as error:
            raise Refusal(str(error)) from error
        return []
    except (Refusal, OSError, UnicodeError, KeyError, AttributeError, ValueError, SyntaxError) as error:
        return [str(error)]


# ------------------------------------------------------------------------------------------ metrics

def outcome(rs: list[dict], requests: int) -> dict:
    """report_stage1a.py:56-70 on the plan's served flag, plus the guard and prediction counts."""
    served = [r for r in rs if r["served"]]
    n = len(served)
    m = {"requests": requests, "accepted": len(rs), "served": n}
    for k in (60, 120, 300):
        m[f"late{k}"] = sum(1 for r in served if r["A"] - r["p0"] > k * 1000)
        m[f"fc_late{k}"] = sum(1 for r in served if r["A"] - r["D0"] > k * 1000)
    m["early60"] = sum(1 for r in served if r["A"] - r["p0"] < -60_000)
    m["fc_early60"] = sum(1 for r in served if r["A"] - r["D0"] < -60_000)
    for k in (60, 120):
        m[f"timely{k}"] = n - m[f"late{k}"]
        m[f"fc_timely{k}"] = n - m[f"fc_late{k}"]
    m["fc_ext_s"] = statistics.mean(r["D0"] - r["p0"] for r in served) / 1000 if n else float("nan")
    m["shared"] = sum(1 for r in served if r["shared"])
    m["exposed"] = sum(1 for r in rs if r["S"] > 0)
    m["repeated"] = sum(1 for r in rs if r["k"] >= 2)
    m["h7_served"] = sum(1 for r in served if r["maxc"] <= H7_B_MS < r["S"])
    m["h7_accepted"] = sum(1 for r in rs if r["maxc"] <= H7_B_MS < r["S"])
    m["horizon_med_acc_ms"] = q([r["p0"] - r["t0"] for r in rs], 0.5)
    m["horizon_med_served_ms"] = q([r["p0"] - r["t0"] for r in served], 0.5)
    return m


def add(ms: list[dict]) -> dict:
    return {key: sum(m[key] for m in ms) for key in ADDITIVE}


# ------------------------------------------------------------------------------------------ the report

def analyse(doc, riders, runs, cells, baselines=None, sf_w0817=None):
    """All sections. baselines / sf_w0817 default to the plan values; only the synthetic self-test injects others."""
    require_metadata(doc)
    require_run_sources(doc, runs.values())
    if doc["mode"] == "main":
        anomalies = [(role, cell) for (role, cell), run in runs.items() if run["completedWithoutAlight"]]
        anomalies += [(role, r["cell"], r["id"]) for role in ROLES for r in riders[role]
                      if r["life"] == "completed" and (not r["served"] or r["A"] is None)]
        if anomalies:
            raise Refusal(f"main evidence has completed riders without alight; completed and served H7 cohorts must "
                          f"coincide: {anomalies[:20]}")
    baselines = BASELINES if baselines is None else baselines
    sf_w0817 = SF_W0817_U_GT_C30 if sf_w0817 is None else sf_w0817
    L, R = [], {"dryRun": doc["dryRun"]}
    say = L.append
    tag = "[DRY RUN: 'CF' rows are archived stand-ins, NOT Stage-1b results] " if doc["dryRun"] else ""
    by_cell = {(role, c): [] for role in ROLES for c in cells}
    for role in ROLES:
        for r in riders[role]:
            by_cell[(role, r["cell"])].append(r)
    req = {role: sum(runs[(role, c)]["requests"] for c in cells) for role in ROLES}
    M = {role: outcome(riders[role], req[role]) for role in ROLES}
    C = {(role, c): outcome(by_cell[(role, c)], runs[(role, c)]["requests"]) for role in ROLES for c in cells}
    days = sorted({c[:9] for c in cells})
    cells_of = {**{d: [c for c in cells if c[:9] == d] for d in days}, **{w: [c for c in cells if c[-3:] == w]
                                                                           for w in WINDOWS}}
    say(f"# report_stage1b.py ({sha256_file(__file__)[:16]}...) on {doc['ridersTsv']['file']} "
        f"(extractor {doc['extractor']['file']} {doc['extractor']['sha256'][:16]}...); mode {doc['mode']}")
    if doc["dryRun"]:
        say(f"# DRY RUN / PLUMBING CHECK. Role sources: {doc['roleSources']}. Nothing below is a Stage-1b result.")
    say(f"# roles {ROLES}; {len(cells)} test cells; requests per role {[req[r] for r in ROLES]}; "
        f"interpreter {doc['python']['executable']} ({doc['python']['version'].split()[0]})")
    say("# All Stage-1b results are exploratory (plan section 3 amendment, section 1.1 limitation); the predictions are "
        "an internal record (plan section 6).")
    # ---------------------------------------------------------------- 0. validity
    say(f"\n## 0. {tag}Input validity per role (extractor refusals already passed; these are the recorded facts)")
    ok = True
    accounting = [(role, c, *item) for role in ROLES for c in cells
                  for item in runs[(role, c)]["ledgerBudgetMismatches"]]
    R["accounting_validity"] = not accounting
    for role in ROLES:
        rs = [runs[(role, c)] for c in cells]
        life = collections.Counter()
        limits = collections.Counter()
        for r in rs:
            life.update(r["lifecycle"])
            limits.update({int(k): v for k, v in r["dropWitnessLimits"].items()})
        served_rows = sum(1 for x in riders[role] if x["served"])
        served_runs = sum(r["served"] for r in rs)
        own_eq = all(r["pruneClassesOwn"] == r["pruneClassesJudge"] for r in rs)
        ack_eq = all(r["decisionsAcknowledged"] == r["judgeAckedDecisions"] for r in rs)
        ok &= served_rows == served_runs and own_eq and ack_eq
        say(f"- {role} (source {doc['roleSources'][role]}): runs {len(rs)}; summary status "
            f"{dict(collections.Counter(r['summaryStatus'] for r in rs))}; run-00 status "
            f"{dict(collections.Counter(r['run00Status'] for r in rs))}; policy binding "
            f"{sorted({r['manifest']['policyConfigurationHash'][:8] for r in rs})}; Runner DLL "
            f"{sorted({r['manifest']['binarySha256'][:8] for r in rs})}; core "
            f"{sorted({r['manifest']['coreCommitSha'][:7] for r in rs})}; inventory "
            f"{sorted({r['repositoryInventorySha256'][:8] for r in rs})}; requests {req[role]} "
            f"(= {len(rs)} x {REQUESTS_PER_RUN}: {req[role] == len(rs) * REQUESTS_PER_RUN}); lifecycle "
            f"{dict(sorted(life.items()))}; accepted riders (rider rows) {len(riders[role])} vs audit promised riders "
            f"{sum(r['promisedRiders'] for r in rs)}; served (completed and alighted) rows {served_rows} = runs "
            f"{served_runs}: {served_rows == served_runs}; completed without alight "
            f"{sum(r['completedWithoutAlight'] for r in rs)}; alighted not completed "
            f"{sum(r['alightedNotCompleted'] for r in rs)}; alighted without promise "
            f"{sum(r['alightedWithoutPromise'] for r in rs)}; forecast-active cells "
            f"{sum(r['forecastActive'] for r in rs)} of {len(rs)}; prune classes own == judge in every run: {own_eq}; "
            f"acknowledged decisions audit == judge in every run: {ack_eq}; drop-witness limit values "
            f"{dict(sorted(limits.items()))}; ledger budget mismatches "
            f"{sum(len(r['ledgerBudgetMismatches']) for r in rs)}")
    same = sum(1 for c in cells if len({(runs[(role, c)]["manifest"]["scenarioContentHash"],
                                          runs[(role, c)]["requestIdsSha256"]) for role in ROLES}) == 1)
    say(f"- per cell, equal scenario hash and request-id set across the four roles: {same} / {len(cells)} cells")
    say(f"- study state recorded by the extractor: latches {doc['studyState'].get('latches')}; "
        + "; ".join(f"{k} {(v or 'absent')[:16]}" for k, v in doc["studyState"].items()
                    if k.endswith(".txt") or k == "bindingSha256"))
    say(f"- ledger accounting validity: {'FAIL' if accounting else 'PASS'}; mismatched fields {len(accounting)} "
        "(a separate consistency condition; never a recovery explanation or an added H1 threshold)")
    if accounting:
        for item in accounting[:200]:
            say(f"    accounting defect: {item}")
        if len(accounting) > 200:
            say(f"    ... {len(accounting) - 200} more mismatched fields (all are in runs.json)")
        say("ACCOUNTING VALIDITY FAILED -> STOP (recovery explanations cannot waive a ledger arithmetic mismatch)")
        R["stopped"] = "accounting-validity"
        return L, R
    if not ok or same != len(cells):
        say("INPUT VALIDITY FAILED -> STOP")
        R["stopped"] = "validity"
        return L, R
    # ---------------------------------------------------------------- 1. baselines
    say("\n## 1. SF baseline reproduction (archived T3.8 SF-U and SF-C30; plan sections 5-6 and Stage 1a values)")
    ok = True
    for (role, key), want in baselines.items():
        got = M[role][key]
        ok &= got == want
        say(f"- {role:<6} {key:<12} expected {want:5d} got {got:5d}  {'OK' if got == want else 'MISMATCH'}")
    w0817 = [c for c in cells if c[-3:] in H4_WINDOWS]
    sf_gt = sum(1 for c in w0817 if C[("SF-U", c)]["late60"] > C[("SF-C30", c)]["late60"])
    ok &= sf_gt == sf_w0817 and len(w0817) == H4_CELLS
    say(f"- SF-U late60 > SF-C30 late60 in {sf_gt} of {len(w0817)} w08/w17 cells (expected {sf_w0817} of "
        f"{H4_CELLS})  {'OK' if sf_gt == sf_w0817 and len(w0817) == H4_CELLS else 'MISMATCH'}")
    say(f"BASELINES {'ALL REPRODUCED' if ok else 'NOT REPRODUCED -> STOP (no CF table is printed)'}")
    R["baselines"] = ok
    if not ok:
        R["stopped"] = "baselines"
        return L, R
    # ---------------------------------------------------------------- 2. primary endpoint
    say(f"\n## 2. {tag}Primary endpoint (plan section 5): naive late60 interaction, pooled over {len(cells)} cells")
    say("role   | served S (per request)  | late60 L (per served)   | timely60 T = S - L (per request)")
    for role in ROLES:
        m = M[role]
        say(f"{role:<6} | {nd(m['served'], m['requests']):<23} | {nd(m['late60'], m['served']):<23} | "
            f"{nd(m['timely60'], m['requests'])}")
    S = {r: M[r]["served"] for r in ROLES}
    Lt = {r: M[r]["late60"] for r in ROLES}
    T = {r: M[r]["timely60"] for r in ROLES}
    I_L, I_S, I_T = interaction(Lt), interaction(S), interaction(T)
    say(f"SF contrast  L_SF,U - L_SF,C30 = {Lt['SF-U']} - {Lt['SF-C30']} = {Lt['SF-U'] - Lt['SF-C30']} "
        f"(plan: 203 - 76 = 127)")
    say(f"CF contrast  L_CF,U - L_CF,C30 = {Lt['CF-U']} - {Lt['CF-C30']} = {Lt['CF-U'] - Lt['CF-C30']}")
    say(f"I_L        = ({Lt['CF-U']} - {Lt['CF-C30']}) - ({Lt['SF-U']} - {Lt['SF-C30']}) = {I_L:+d} rider-runs")
    say(f"I_served   = ({S['CF-U']} - {S['CF-C30']}) - ({S['SF-U']} - {S['SF-C30']}) = {I_S:+d}")
    say(f"I_timely60 = ({T['CF-U']} - {T['CF-C30']}) - ({T['SF-U']} - {T['SF-C30']}) = {I_T:+d}")
    identity = I_L == I_S - I_T
    say(f"identity   I_served - I_timely60 = {I_S:+d} - ({I_T:+d}) = {I_S - I_T:+d} {'==' if identity else '!='} "
        f"I_L = {I_L:+d}: {'HOLDS' if identity else 'FAILS -> STOP'}")
    R.update(I_L=I_L, I_served=I_S, I_timely60=I_T, identity=identity)
    say("L counts late COMPLETED rider-runs; rejected requests are absent and the served sets differ, so I_L is read "
        "together with I_served, I_timely60 and the 2x2 below (plan section 5; R18:247).")
    rej = {role: sum(runs[(role, c)]["lifecycle"].get("rejected", 0) for c in cells) for role in ROLES}
    notserved = {role: M[role]["requests"] - M[role]["served"] for role in ROLES}
    say("\nFull 2x2 (selector x cap) of the three request outcomes, counts over the requests of each role:")
    grids = {}
    for name, val in (("timely60 (served, A - p0 <= 60 s)", T), ("late60 (served, A - p0 > 60 s)", Lt),
                      ("not served (rejected or other lifecycle)", notserved)):
        say(f"  {name}")
        say(f"    {'':3} | {'U':<22} | {'C30':<22} | U - C30")
        for sel in ("SF", "CF"):
            u, c = val[f"{sel}-U"], val[f"{sel}-C30"]
            say(f"    {sel:3} | {nd(u, req[sel + '-U']):<22} | {nd(c, req[sel + '-C30']):<22} | {u - c:+d}")
        grids[name] = interaction(val)
        say(f"    CF - SF: U {val['CF-U'] - val['SF-U']:+d}, C30 {val['CF-C30'] - val['SF-C30']:+d}; interaction "
            f"= {grids[name]:+d}")
    total = sum(grids.values())
    say(f"  interactions timely {grids['timely60 (served, A - p0 <= 60 s)']:+d} + late "
        f"{grids['late60 (served, A - p0 > 60 s)']:+d} + not served "
        f"{grids['not served (rejected or other lifecycle)']:+d} = {total:+d} (0 when every role has the same requests)")
    say("  not served = rejected + other: " + "; ".join(
        f"{role} {notserved[role]} = {rej[role]} rejected + {notserved[role] - rej[role]} other" for role in ROLES))
    R["interaction_sum"] = total
    # ---------------------------------------------------------------- 3. served-set overlap
    say(f"\n## 3. {tag}Served-set overlap ((cell, requestId) of served riders; post-treatment populations, "
        f"descriptive only, R18:247)")
    sets = {role: {(r["cell"], r["id"]): r for r in riders[role] if r["served"]} for role in ROLES}
    late = lambda r: r["A"] - r["p0"] > 60_000  # noqa: E731
    R["overlap"] = {}
    for a, b in (("CF-U", "SF-U"), ("CF-C30", "SF-C30"), ("CF-U", "CF-C30"), ("SF-U", "SF-C30")):
        common = sets[a].keys() & sets[b].keys()
        only_a, only_b = sets[a].keys() - sets[b].keys(), sets[b].keys() - sets[a].keys()
        R["overlap"][(a, b)] = (len(common), len(only_a), len(only_b))
        say(f"- {a} vs {b}: common {len(common)}; only {a} {len(only_a)}; only {b} {len(only_b)}. late60 among common: "
            f"{a} {nd(sum(late(sets[a][k]) for k in common), len(common))}, "
            f"{b} {nd(sum(late(sets[b][k]) for k in common), len(common))}; among only-{a} "
            f"{nd(sum(late(sets[a][k]) for k in only_a), len(only_a))}; among only-{b} "
            f"{nd(sum(late(sets[b][k]) for k in only_b), len(only_b))}")
    # ---------------------------------------------------------------- 4. cells, windows, days
    say(f"\n## 4. {tag}Per cell, per window and per day (paired: every role ran the same 108 requests per cell)")
    say("cell                 | S: CF-U CF-C30 SF-U SF-C30 | L: CF-U CF-C30 SF-U SF-C30 | I_served I_timely60 I_L")
    per_cell = {}
    for c in cells:
        s = {r: C[(r, c)]["served"] for r in ROLES}
        lt = {r: C[(r, c)]["late60"] for r in ROLES}
        t = {r: C[(r, c)]["timely60"] for r in ROLES}
        per_cell[c] = (interaction(s), interaction(t), interaction(lt))
        if per_cell[c][2] != per_cell[c][0] - per_cell[c][1]:
            R["identity"] = identity = False
        say(f"{c} | {s['CF-U']:4d} {s['CF-C30']:6d} {s['SF-U']:4d} {s['SF-C30']:6d} | {lt['CF-U']:4d} "
            f"{lt['CF-C30']:6d} {lt['SF-U']:4d} {lt['SF-C30']:6d} | {per_cell[c][0]:+8d} {per_cell[c][1]:+10d} "
            f"{per_cell[c][2]:+3d}")
    say(f"identity I_L,c = I_served,c - I_timely60,c holds in "
        f"{sum(v[2] == v[0] - v[1] for v in per_cell.values())} of {len(cells)} cells")
    w07 = [c for c in cells if c[-3:] == "w07"]
    for label, idx in (("I_L,c", 2), ("I_served,c", 0), ("I_timely60,c", 1)):
        say(f"- {label}: {sign_text((per_cell[c][idx] for c in cells), 'cells')}; w08/w17: "
            f"{sign_text((per_cell[c][idx] for c in w0817), 'cells')}; w07: "
            f"{sign_text((per_cell[c][idx] for c in w07), 'cells')}")
    R["cells_IL_pos_w0817"] = signs(per_cell[c][2] for c in w0817)[0]
    R["cells_IL_signs"] = signs(per_cell[c][2] for c in cells)
    for group, kind in ((WINDOWS, "window"), (days, "day")):
        vals = {}
        for g in group:
            agg = {role: add([C[(role, c)] for c in cells_of[g]]) for role in ROLES}
            i = tuple(interaction({r: agg[r][key] for r in ROLES}) for key in ("served", "timely60", "late60"))
            vals[g] = i
            say(f"- {kind} {g} ({len(cells_of[g])} cells, {agg['CF-U']['requests']} requests per role): served "
                f"{[agg[r]['served'] for r in ROLES]}, late60 {[agg[r]['late60'] for r in ROLES]} (CF-U, CF-C30, SF-U, "
                f"SF-C30); I_served {i[0]:+d}, I_timely60 {i[1]:+d}, I_L {i[2]:+d} (identity "
                f"{'holds' if i[2] == i[0] - i[1] else 'FAILS'})")
            if i[2] != i[0] - i[1]:
                R["identity"] = identity = False
        if kind == "day":
            R["days_IL_signs"] = signs(v[2] for v in vals.values())
            say(f"- I_L per day: {sign_text((v[2] for v in vals.values()), 'days')}; I_served per day: "
                f"{sign_text((v[0] for v in vals.values()), 'days')}; I_timely60 per day: "
                f"{sign_text((v[1] for v in vals.values()), 'days')}")
    lodo = {}
    for d in days:
        keep = [c for c in cells if c[:9] != d]
        lodo[d] = interaction({r: sum(C[(r, c)]["late60"] for c in keep) for r in ROLES})
    say(f"- leave-one-day-out I_L (descriptive, not an interval): " + ", ".join(f"without {d} {v:+d}"
                                                                          for d, v in lodo.items())
        + f"; range [{min(lodo.values()):+d}, {max(lodo.values()):+d}]")
    R["lodo"] = lodo
    say("  Five days are not 34 independent replicates; no confidence interval is computed (plan section 5; R18:251).")
    # ---------------------------------------------------------------- 5. guards
    say(f"\n## 5. {tag}Guards for every role (plan section 7)")
    say("5a. Outcomes vs the FIRST drop promise, naive quote (served riders; per-request shares over the role's requests)")
    say("role   | served                 | late>60               | late>120             | late>300           | "
        "early>60           | timely<=60 per request | timely<=120 per request")
    for role in ROLES:
        m, n = M[role], M[role]["served"]
        say(f"{role:<6} | {nd(n, m['requests'])} | {nd(m['late60'], n)} | {nd(m['late120'], n)} | "
            f"{nd(m['late300'], n)} | {nd(m['early60'], n)} | {nd(m['timely60'], m['requests'])} | "
            f"{nd(m['timely120'], m['requests'])}")
    say("5b. Forecast quote (frozen causal-no15, D0 = display(t0, p0); naive in inactive cells; no retuning)")
    say("role   | fc late>60          | fc late>120        | fc late>300        | fc early>60        | "
        "fc timely<=60 per request | fc timely<=120 per request | mean quote extension (over served)")
    for role in ROLES:
        m, n = M[role], M[role]["served"]
        say(f"{role:<6} | {nd(m['fc_late60'], n)} | {nd(m['fc_late120'], n)} | {nd(m['fc_late300'], n)} | "
            f"{nd(m['fc_early60'], n)} | {nd(m['fc_timely60'], m['requests'])} | "
            f"{nd(m['fc_timely120'], m['requests'])} | {m['fc_ext_s']:.2f} s over {n} served")
    say("5c. Initial promise horizon p0 - t0 (nearest-rank median) and the 30-s promise guard (an arrival gain with a "
        "median initial promise more than 30 s longer is not claimed; R17:275)")
    for role in ROLES:
        m = M[role]
        say(f"- {role}: median {m['horizon_med_acc_ms'] / 1000:.3f} s over {m['accepted']} accepted riders; "
            f"{m['horizon_med_served_ms'] / 1000:.3f} s over {m['served']} served riders")
    R["promise_guard"] = {}
    for a, b in (("CF-C30", "SF-C30"), ("CF-U", "SF-U"), ("CF-C30", "CF-U"), ("SF-C30", "SF-U")):
        dmed = M[a]["horizon_med_acc_ms"] - M[b]["horizon_med_acc_ms"]
        evaluable = M[a]["accepted"] > 0 and M[b]["accepted"] > 0 and math.isfinite(dmed)
        longer = dmed > 30_000 if evaluable else None
        R["promise_guard"][(a, b)] = (dmed, longer)
        say(f"- {a} vs {b}: median horizon difference {dmed / 1000:+.3f} s; late60 {M[a]['late60'] - M[b]['late60']:+d}, "
            f"timely60 {M[a]['timely60'] - M[b]['timely60']:+d}, fc timely60 "
            f"{M[a]['fc_timely60'] - M[b]['fc_timely60']:+d}, timely120 {M[a]['timely120'] - M[b]['timely120']:+d}: "
            + ("NOT EVALUABLE: an accepted cohort is empty or the median difference is nonfinite; "
               "the promise guard cannot support an arrival-gain claim" if not evaluable else
               f"GUARD: median promise more than 30 s longer -> no arrival gain of {a} over {b} is claimed"
               if longer else "guard met (median promise not more than 30 s longer)"))
    say("5d. Dispatch changes of the drop promise (ACCEPTED riders; S_r = sum |c| of later drop revisions)")
    D = {}
    for role in ROLES:
        rs, m = riders[role], M[role]
        n = len(rs)
        exp = [r["S"] / 1000 for r in rs if r["S"] > 0]
        exp_served = sum(1 for r in rs if r["S"] > 0 and r["served"])
        kmax = max((r["k"] for r in rs), default=0)
        neg = sum(1 for r in rs if r["S"] > 0 and r["net"] < 0)
        line = (f"- {role}: drop-exposed {nd(m['exposed'], n)} (served among them {exp_served}); repeated (>= 2 nonzero "
                f"c) {nd(m['repeated'], n)} (max k {kmax}); S_r among exposed: mean "
                f"{statistics.mean(exp) if exp else float('nan'):.1f} s, p50 {q(exp, 0.5):.1f}, p90 {q(exp, 0.9):.1f}, "
                f"max {max(exp) if exp else float('nan'):.1f} (n = {len(exp)}); exposed with net signed c < 0: {neg}; "
                f"H7 screen max|c| <= 120 s < S_r: {nd(m['h7_served'], m['served'])} served, "
                f"{nd(m['h7_accepted'], n)} accepted")
        b = CAP_MS[role]
        d = {"n": n, "max_s": max(exp) if exp else 0.0}
        if b:
            d.update(over=sum(1 for r in rs if r["S"] > b), over_tol=sum(1 for r in rs if r["S"] > b + 1),
                     ge90=sum(1 for r in rs if r["S"] >= 0.9 * b), ge50=sum(1 for r in rs if r["S"] >= 0.5 * b))
            line += (f"; S_r >= 0.9 B ({0.9 * b / 1000:.0f} s): {nd(d['ge90'], n)}; S_r >= 0.5 B: {nd(d['ge50'], n)}; "
                     f"overshoots S_r > B: {nd(d['over'], n)} (S_r > B + 1 ms: {d['over_tol']})")
        else:
            line += "; (no budget) " + ", ".join(f"S_r {op} {s} s: {nd(sum(1 for r in rs if f(r['S'])), n)}" for op, s, f in (
                (">=", 27, lambda v: v >= 27_000), (">", 30, lambda v: v > 30_000), (">=", 108, lambda v: v >= 108_000),
                (">", 120, lambda v: v > 120_000)))
        D[role] = d
        say(line)
    say("5e. Budget prune records (acknowledged decisions; recorded first-failing witness only), forced and no-worse "
        "records (non-normal certificates; incident-ledger breaches other than exogenousServiceQuality)")
    P = {}
    for role in ROLES:
        rs = [runs[(role, c)] for c in cells]
        cls = collections.Counter()
        breaches = collections.Counter()
        for r in rs:
            cls.update(r["pruneClassesJudge"])
            breaches.update(r["breaches"])
        explain = [(r["cell"], rid, kind) for r in rs for rid, kind in r["breachRecordsToExplain"]]
        nonnormal = [(r["cell"], h) for r in rs for h in r["nonNormalDecisionHashes"]]
        budget = [(r["cell"], *x) for r in rs for x in r["ledgerBudgetMismatches"]]
        acked = sum(r["decisionsAcknowledged"] for r in rs)
        P[role] = {"cls": cls, "nonnormal": nonnormal, "explain": explain, "budget": budget}
        runs_cum = [r["cell"] for r in rs if r["pruneClassesJudge"].get("cumulative-only")]
        say(f"- {role}: budget-pruned candidates {sum(cls.values())} by class {dict(sorted(cls.items()))}; cumulative-only "
            f"records {cls.get('cumulative-only', 0)} in runs {runs_cum}; decisions with >= 1 cumulative-only "
            f"{sum(r['judgeDecisionsWithCumulativeOnly'] for r in rs)} of {acked} acknowledged; distinct new requests in "
            f"them {sum(r['judgeRequestsWithCumulativeOnly'] for r in rs)}, finally rejected "
            f"{sum(r['judgeOfWhichFinallyRejected'] for r in rs)}; non-normal (forced) decisions {nd(len(nonnormal), acked)}; "
            f"breach entries by kind {dict(sorted(breaches.items()))}; forced / no-worse breach records "
            f"{len(explain)}; riders with forced-reference breach {sum(r['ridersWithForcedBreach'] for r in rs)}; "
            f"ledger budget mismatches {len(budget)}; late pickups {sum(r['latePickups'] for r in rs)}")
        for r in rs:
            for det in r["cumulativeOnlyOwn"]:
                say(f"    cumulative-only record: {role} {r['cell']} new {det['newRequestIds']} witness "
                    f"(rider, before_ms, delta_ms, after_ms, limit_ms) {det['witness']}")
    say("5f. Shared completions (served rider with another rider on board the same vehicle at an overlapping time)")
    for role in ROLES:
        m = M[role]
        say(f"- {role}: shared completions {nd(m['shared'], m['served'])} of served; per request "
            f"{nd(m['shared'], m['requests'])}")
    say("5g. Paired signs of guard metrics (A - B per cell over 34 cells and per day over 5 days; pooled difference)")
    for a, b in (("CF-U", "SF-U"), ("CF-C30", "SF-C30"), ("CF-C30", "CF-U")):
        for key in ("served", "late60", "timely60", "timely120", "fc_timely60", "shared", "exposed"):
            per_c = [C[(a, c)][key] - C[(b, c)][key] for c in cells]
            per_d = [sum(C[(a, c)][key] - C[(b, c)][key] for c in cells_of[d]) for d in days]
            say(f"- {a} - {b} {key}: pooled {M[a][key] - M[b][key]:+d}; cells {sign_text(per_c, 'cells')}; days "
                f"{sign_text(per_d, 'days')}")
    # ---------------------------------------------------------------- 6. predictions
    say(f"\n## 6. {tag}Sealed predictions (plan section 6; internal record), evaluated as written")
    lines, verdicts = predictions(M, D, P, I_L, I_S, I_T, R["cells_IL_pos_w0817"], len(w0817), doc["studyState"],
                                  doc["dryRun"])
    L.extend(lines)
    R["predictions"] = verdicts
    R["M"] = M
    R["D"] = D
    if not R["identity"]:
        say("\nIDENTITY FAILED somewhere above -> the report is not usable")
        R["stopped"] = "identity"
    return L, R


def predictions(M, D, P, I_L, I_S, I_T, pos, n_w0817, study, dry_run):
    """Plan section 6, evaluated as written (pure function; thresholds tested at their boundaries by the self-test).
    M: outcome() per role; D: dispatch facts per role (CF-C30 needs over, n, max_s); P: per role nonnormal / explain /
    budget record lists; pos: w08/w17 cells with I_L,c > 0 out of n_w0817; study: extractor study state."""
    L = []
    say = L.append
    if dry_run:
        g0 = None
        say("G0  not evaluated (dry run; no Gate-0 verdict is read)")
    else:
        g0 = bool(study.get("GATE0-BEHAVIOURAL-PASSED.txt")) and not study.get("latches")
        say(f"G0  {'PASS' if g0 else 'FAIL'}: GATE0-BEHAVIOURAL-PASSED.txt SHA-256 "
            f"{study.get('GATE0-BEHAVIOURAL-PASSED.txt')}; stop / failure receipts {study.get('latches')} "
            f"(behavioural-only, amended; plan section 3)")
    m30, mu = M["CF-C30"], M["CF-U"]
    records = P["CF-C30"]["nonnormal"] + P["CF-C30"]["explain"]
    accounting = P["CF-C30"]["budget"]
    h1 = "FAIL" if D["CF-C30"]["over"] > 0 else "PASS" if not records else "PENDING"
    say(f"H1  {h1}: CF-C30 riders with S_r > 30 s {nd(D['CF-C30']['over'], D['CF-C30']['n'])} (needs 0; max S_r "
        f"{D['CF-C30']['max_s']:.1f} s); non-normal decisions to explain {len(P['CF-C30']['nonnormal'])}; "
        f"forced / no-worse breach entries to explain {len(P['CF-C30']['explain'])}; these are distinct record units"
        + (" -> each record below needs a written explanation before H1 can pass"
                                             if h1 == "PENDING" else ""))
    for item in records[:200]:
        say(f"    to explain: {item}")
    if len(records) > 200:
        say(f"    ... {len(records) - 200} more records (all are in the runs.json file)")
    if h1 == "FAIL":
        say("    plan: implementation defect, stop.")
    if accounting:
        say(f"ACCOUNTING VALIDITY FAIL: {len(accounting)} ledger arithmetic mismatches -> implementation assessment "
            "fails independently of the H1 cap/explanation screen; recovery explanations cannot waive this defect.")
        for item in accounting[:200]:
            say(f"    accounting defect: {item}")
    h2 = mu["exposed"] >= 261 and mu["late60"] >= 243
    say(f"H2  {'PASS' if h2 else 'FAIL'}: CF-U drop-exposed {nd(mu['exposed'], mu['accepted'])} accepted (needs >= 261 = "
        f"1.5 x 174) and naive late60 {nd(mu['late60'], mu['served'])} served (needs >= 243 = 203 + 40)")
    if not h2:
        say("    plan: the chosen materiality prediction failed; this does NOT show selector invariance, absence of a "
            "confounder, or small effects on every metric (R18:199). No Stage 2.")
    h3a = m30["late60"] * 10_000 <= 425 * m30["served"] if m30["served"] > 0 else None
    h3b = m30["fc_late60"] * 100 <= m30["served"] if m30["served"] > 0 else None
    h3 = h3a and h3b
    say(f"H3  {'NOT EVALUABLE' if h3 is None else 'PASS' if h3 else 'FAIL'}: CF-C30 naive late60 "
        f"{nd(m30['late60'], m30['served'])} (needs <= "
        f"4.25 %: {h3a}) and forecast late60 {nd(m30['fc_late60'], m30['served'])} (needs <= 1.0 %: {h3b}); SF-C30 "
        f"{nd(M['SF-C30']['late60'], M['SF-C30']['served'])}, {nd(M['SF-C30']['fc_late60'], M['SF-C30']['served'])}")
    if h3 is None:
        say("    zero served riders: reliability fractions have no denominator; retention cannot be evaluated.")
    elif not h3:
        say("    plan: the changed policy missed a predeclared aggregate reliability threshold; report the service "
            "composition and trajectory evidence before attributing the difference to the selector (R19C:178; R24:140).")
    h4 = I_L >= 40 and pos >= 15
    say(f"H4  {'PASS' if h4 else 'FAIL'} (primary): I_L = {I_L:+d} (needs >= +40: {I_L >= 40}) and per-cell I_L,c > 0 in "
        f"{pos} of {n_w0817} w08/w17 cells (needs >= 15: {pos >= 15}); I_served {I_S:+d}, I_timely60 {I_T:+d}")
    if not h4:
        missed = [x for x, bad in (("I_L >= +40", I_L < 40), (">= 15 of 25 cells", pos < 15)) if bad]
        say(f"    missed target(s): {missed}. plan: I_L < 40 is not evidence of objective independence; a negative I_L "
            f"can itself be an interaction; a small nonzero interaction still misses +40 (R18:201).")
    d5 = m30["fc_timely60"] - mu["fc_timely60"]
    h5 = d5 >= 31
    say(f"H5  {'PASS' if h5 else 'FAIL'} (guard): forecast timely60 per request CF-C30 - CF-U = {m30['fc_timely60']} - "
        f"{mu['fc_timely60']} = {d5:+d} over {m30['requests']} requests (needs >= +31; SF 2328 - 2297); reported next "
        f"to H4, not a second primary")
    h6 = m30["shared"] >= 345 and m30["served"] >= 2315
    say(f"H6  {'PASS' if h6 else 'FAIL'} (key secondary): CF-C30 shared completions {nd(m30['shared'], m30['served'])} "
        f"served (needs >= 345 = ceil(1.10 x 313)) with served {nd(m30['served'], m30['requests'])} (needs >= 2315 = "
        f"ceil(0.99 x 2338)); the 99 % service guard is reported either way: {m30['served'] >= 2315}")
    h7 = mu["h7_served"] >= 10
    say(f"H7  {'PASS' if h7 else 'FAIL'} (Stage-2 gate): CF-U completed and alighted riders with every nonzero drop move "
        "<= 120 s and "
        f"sum > 120 s: {nd(mu['h7_served'], mu['served'])} (needs >= 10; SF-U {M['SF-U']['h7_served']})")
    if not h7:
        say("    plan: do not build the per-step arm; a resource-allocation stop rule, not a bound on witnesses (R18:203).")
    say(f"Stage-2 precondition H2 and H7 (plan section 9; time condition not evaluated here): {h2 and h7}")
    verdicts = {"G0": g0, "H1": h1, "H2": h2, "H3": h3, "H4": h4, "H5": h5, "H6": h6, "H7": h7}
    if accounting:
        verdicts["accountingValidity"] = False
    return L, verdicts


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        raise SystemExit(__doc__)
    prefix, out = argv[1], pathlib.Path(argv[2])
    try:
        if out.exists():
            raise Refusal(f"{out} exists (mode 'x')")
        if any(norm(out).startswith(norm(root) + os.sep) for root in OUTPUT_FORBIDDEN):
            raise Refusal(f"{out} lies under an input root")
        doc, riders, runs, cells = load(prefix)
        if doc["mode"] == "main":
            problems = binding_problems(doc)
            if problems:
                raise Refusal("; ".join(problems))
        lines, results = analyse(doc, riders, runs, cells)
        if doc["mode"] == "main":
            problems = binding_problems(doc)
            if problems:
                raise Refusal("study state changed or became invalid before report output: " + "; ".join(problems))
    except Refusal as error:
        print(f"REFUSED; nothing written: {error}", flush=True)
        return 2
    text = "\n".join(lines) + "\n"
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    sys.stdout.write(text)
    return 1 if results.get("stopped") else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
