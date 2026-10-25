"""Self-test of the Stage 1b analysis scripts extract_stage1b.py and report_stage1b.py (read-only on every input).

Part A, synthetic (known answers built by hand; no archived outcome is needed):
  A1 report_stage1b.outcome(): every definition at its boundary (late > 60 s strict, early < -60 s strict, timely = S - L,
     forecast quote, served flag, exposed, repeated, H7 rule max|c| <= 120 s < S, nearest-rank median horizon).
  A2 the identity I_L = I_served - I_timely60 for 2,000 random 2x2 count sets (T = S - L per role).
  A3 report_stage1b.predictions(): G0 and H1-H7 at both sides of every threshold.
  A4 report_stage1b.analyse() end to end on a synthetic 4-role x 34-cell data set with known pooled, per-cell, per-day and
     leave-one-day-out interactions; the baseline STOP and the validity STOP.
  A5 extractor gate on synthetic run folders, logs, anchors and harness files (written only in the work folder):
     missing / failed / foreign runs, log returncode, analysis binding in log and anchor, unexpected main entries,
     study-state latches and binding inventory.
  A6 extractor guards: bundle verification and pins, read-window closure, identity checks, pin_problems, output paths.
  A7 the verbatim copies equal their originals: event_times (3 real transcripts), sf_effective (68 SF names),
     day_of (34 cells), q (random lists).
  A8 report binding check on a synthetic binding file; the CF layout, labels, log / anchor formats and verdict names
     equal those in run_stage1b-v7.py and compare_equivalence_stage1b-v7.py (static source check); the policy
     bindings equal make_wp4_costfirst.log; the CF job mapping of expected_run().
  A9 in fresh subprocesses: readers() refuses an unpinned scratchpad module and a moved factor folder; the live read
     window records and flags an unpinned read and a write.
Part B, plumbing dry runs on ARCHIVED data through the command lines (stand-ins for the CF roles; known answers):
  B0 `extract_stage1b.py main` refuses now (no Stage-1b output exists; 68 missing) and writes nothing.
  B1 dryrun SF-U SF-C30 (SF as both selectors): I_L = I_served = I_timely60 = 0 pooled, per cell, per day; CF = SF on
     every metric; SF rider rows equal the Stage 1a extraction (r21-stage1a/stage1a-2026-10-10-riders.tsv).
  B2 dryrun SF-C30 SF-U (swapped): I_L = -254, I_served = -192, I_timely60 = +62; I_L,c = -2 (L_SF,U,c - L_SF,C30,c).
  B3 dryrun SF-U SF-C120: I_L = -56, I_served = -49, I_timely60 = +7; the C-120 stand-in reproduces Stage 1a
     (C120-RESULTS.md); per-run numbers equal Codex's independent R19-stage1a-analysis.json (U, C-30, C-120).
  B4 refusals: re-extraction to an existing prefix, report on a tampered riders.tsv, report to an existing file, a
     main-mode extraction without the analysis binding.
Usage: python -B test_analysis_stage1b.py <log file> <work folder>   (both new, inside this harness folder; mode "x")
Exit status 0 when every check passes.
"""
from __future__ import annotations

import collections
import hashlib
import json
import math
import os
import pathlib
import random
import shutil
import subprocess
import sys
import time

sys.dont_write_bytecode = True
HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import extract_stage1b as ex  # noqa: E402
import report_stage1b as rs  # noqa: E402

PY = sys.executable
SCRATCH = ex.SCRATCH
STAGE1A = SCRATCH / "optimize" / "codex-debate" / "claude-work" / "r21-stage1a" / "stage1a-2026-10-10-riders.tsv"
CODEX_R19 = SCRATCH / "optimize" / "codex-debate" / "codex-work" / "R19-stage1a-analysis.json"
LOG = None
COUNTS = collections.Counter()


def log(text=""):
    sys.stdout.write(text + "\n")
    sys.stdout.flush()
    LOG.write(text + "\n")
    LOG.flush()


def check(name, ok, detail=""):
    COUNTS["pass" if ok else "fail"] += 1
    detail = " | ".join(part.strip() for part in str(detail).splitlines() if part.strip())
    log(f"  [{'PASS' if ok else 'FAIL'}] {name}" + (f" -- {detail}" if detail else ""))
    return ok


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# ============================================================================== Part A

def rider(cell="d20181116-s10-r1-w08", rid="r", served=True, t0=0, p0=600_000, d0=None, a=600_000, s=0, k=0, net=0,
          maxc=0, shared=False):
    return {"cell": cell, "id": rid, "life": "completed" if served else "rejected", "t0": t0, "p0": p0,
            "D0": float(p0 if d0 is None else d0), "A": a if served else None, "served": served, "S": s, "k": k,
            "net": net, "maxc": maxc, "shared": shared}


def part_a1():
    log("A1 outcome() definitions at their boundaries")
    rs_ = [rider(rid="exact60", a=660_000),                       # A - p0 = 60 000: timely, not late
           rider(rid="late60", a=660_001),                        # 60 001: late60
           rider(rid="early-edge", a=540_000),                    # -60 000: not early
           rider(rid="early", a=539_999),                         # -60 001: early
           rider(rid="notserved", served=False, s=5_000, k=1, maxc=5_000),
           rider(rid="fc", a=660_001, d0=600_000.5),              # A - D0 = 60 000.5 > 60 000: fc late; naive late
           rider(rid="late120edge", a=720_000),                   # 120 000: late60, not late120
           rider(rid="late300", a=900_001, s=130_000, k=2, net=-10, maxc=120_000, shared=True),   # H7 counts
           rider(rid="h7max", s=200_000, k=2, maxc=120_001),     # max |c| > 120 s: not H7
           rider(rid="h7sum", s=120_000, k=2, maxc=60_000),      # S = 120 s: not > 120 s
           rider(rid="h7acc", served=False, s=150_000, k=2, maxc=100_000)]   # H7 accepted only
    m = rs.outcome(rs_, 20)
    want = {"requests": 20, "accepted": 11, "served": 9, "late60": 4, "late120": 1, "late300": 1, "fc_late60": 4,
            "early60": 1, "timely60": 5, "timely120": 8, "fc_timely60": 5, "shared": 1, "exposed": 5, "repeated": 4,
            "h7_served": 1, "h7_accepted": 2}
    for key, value in want.items():
        check(f"outcome {key} = {value}", m[key] == value, f"got {m[key]}")
    check("mean quote extension over served = 0.5 ms / 9 riders", abs(m["fc_ext_s"] - 0.5 / 9 / 1000) < 1e-15,
          f"got {m['fc_ext_s']!r}")
    horizons = [rider(rid=str(i), t0=0, p0=p) for i, p in enumerate((100, 300, 200, 400))]
    check("nearest-rank median horizon of 100, 200, 300, 400 ms is 200 ms (lower middle)",
          rs.outcome(horizons, 4)["horizon_med_acc_ms"] == 200)


def part_a2():
    log("A2 identity I_L = I_served - I_timely60 (2,000 random count sets)")
    rng, bad = random.Random(20261014), 0
    for _ in range(2000):
        S = {r: rng.randint(0, 3672) for r in rs.ROLES}
        L = {r: rng.randint(0, S[r]) for r in rs.ROLES}
        T = {r: S[r] - L[r] for r in rs.ROLES}
        bad += rs.interaction(L) != rs.interaction(S) - rs.interaction(T)
    check("identity holds in 2000 / 2000 random sets", bad == 0, f"{bad} failures")
    v = {"CF-U": 300, "CF-C30": 100, "SF-U": 203, "SF-C30": 76}
    check("interaction((300 - 100) - (203 - 76)) = +73", rs.interaction(v) == 73)


def base_prediction_inputs():
    M = {"CF-C30": {"late60": 0, "served": 2400, "fc_late60": 0, "fc_timely60": 2331, "shared": 345, "requests": 3672},
         "CF-U": {"exposed": 261, "accepted": 2500, "late60": 243, "served": 2500, "fc_timely60": 2300, "h7_served": 10,
                  "requests": 3672},
         "SF-C30": {"late60": 76, "served": 2338, "fc_late60": 10}, "SF-U": {"h7_served": 4}}
    D = {"CF-C30": {"over": 0, "n": 2400, "max_s": 29.0}}
    P = {"CF-C30": {"nonnormal": [], "explain": [], "budget": []}}
    return M, D, P


def predict(M, D, P, I_L=40, pos=15, study=None, dry=False):
    _, v = rs.predictions(M, D, P, I_L, 0, 0, pos, 25, study or {"latches": []}, dry)
    return v


def part_a3():
    log("A3 predictions() at both sides of every threshold")
    M, D, P = base_prediction_inputs()
    v = predict(M, D, P)
    check("all-at-threshold inputs: H1-H7 pass", [v[h] for h in ("H1", "H2", "H3", "H4", "H5", "H6", "H7")]
          == ["PASS", True, True, True, True, True, True], str(v))
    cases = [
        ("H1 overshoot 1 -> FAIL", ("H1", "FAIL"), lambda M, D, P: D["CF-C30"].update(over=1)),
        ("H1 one non-normal decision -> PENDING", ("H1", "PENDING"),
         lambda M, D, P: P["CF-C30"]["nonnormal"].append(("c", "h"))),
        ("H1 one forced breach record -> PENDING", ("H1", "PENDING"),
         lambda M, D, P: P["CF-C30"]["explain"].append(("c", "r", "forcedReference"))),
        ("H1 one ledger budget mismatch -> PENDING", ("H1", "PENDING"),
         lambda M, D, P: P["CF-C30"]["budget"].append(("c", "r", 1, "budgetAfter", 1, 2))),
        ("H2 exposed 260 -> FAIL", ("H2", False), lambda M, D, P: M["CF-U"].update(exposed=260)),
        ("H2 late60 242 -> FAIL", ("H2", False), lambda M, D, P: M["CF-U"].update(late60=242)),
        ("H3 late60 85 / 2000 = 4.25 % -> PASS", ("H3", True), lambda M, D, P: M["CF-C30"].update(served=2000, late60=85)),
        ("H3 late60 86 / 2000 > 4.25 % -> FAIL", ("H3", False), lambda M, D, P: M["CF-C30"].update(served=2000, late60=86)),
        ("H3 fc late60 20 / 2000 = 1.0 % -> PASS", ("H3", True),
         lambda M, D, P: M["CF-C30"].update(served=2000, fc_late60=20)),
        ("H3 fc late60 21 / 2000 > 1.0 % -> FAIL", ("H3", False),
         lambda M, D, P: M["CF-C30"].update(served=2000, fc_late60=21)),
        ("H5 difference +30 -> FAIL", ("H5", False), lambda M, D, P: M["CF-C30"].update(fc_timely60=2330)),
        ("H6 shared 344 -> FAIL", ("H6", False), lambda M, D, P: M["CF-C30"].update(shared=344)),
        ("H6 served 2314 -> FAIL", ("H6", False), lambda M, D, P: M["CF-C30"].update(served=2314)),
        ("H6 served 2315 -> PASS", ("H6", True), lambda M, D, P: M["CF-C30"].update(served=2315)),
        ("H7 9 riders -> FAIL", ("H7", False), lambda M, D, P: M["CF-U"].update(h7_served=9)),
    ]
    for name, (key, want), mutate in cases:
        M, D, P = base_prediction_inputs()
        mutate(M, D, P)
        got = predict(M, D, P)[key]
        check(name, got == want, f"got {got}")
    M, D, P = base_prediction_inputs()
    check("H4 I_L 39 with 15 cells -> FAIL", predict(M, D, P, I_L=39)["H4"] is False)
    check("H4 I_L 40 with 14 cells -> FAIL", predict(M, D, P, pos=14)["H4"] is False)
    check("G0 dry run -> not evaluated (None)", predict(M, D, P, dry=True)["G0"] is None)
    check("G0 main with the pass file and no latch -> PASS",
          predict(M, D, P, study={"GATE0-BEHAVIOURAL-PASSED.txt": "ab", "latches": []})["G0"] is True)
    check("G0 main with a latch -> FAIL",
          predict(M, D, P, study={"GATE0-BEHAVIOURAL-PASSED.txt": "ab", "latches": ["SMOKE-FAILED.txt"]})["G0"] is False)


def synthetic_data(cells, chosen):
    """SF-U (70 served, 6 late), SF-C30 (66, 2), CF-C30 (66, 2) everywhere; CF-U (72, 9) in `chosen`, else (70, 6).
    So I_L,c = +3, I_served,c = +2, I_timely60,c = -1 in the chosen cells and 0 elsewhere."""
    riders = {role: [] for role in rs.ROLES}
    runs = {}
    for role in rs.ROLES:
        for c in cells:
            s, late = {"SF-U": (70, 6), "SF-C30": (66, 2), "CF-C30": (66, 2)}.get(role, (72, 9) if c in chosen else (70, 6))
            for i in range(s):
                riders[role].append(rider(cell=c, rid=f"q{i}", a=661_000 if i < late else 600_000))
            runs[(role, c)] = {
                "role": role, "cell": c, "requests": 108, "lifecycle": {"completed": s, "rejected": 108 - s},
                "promisedRiders": s, "served": s, "completedWithoutAlight": 0, "alightedNotCompleted": 0,
                "alightedWithoutPromise": 0, "forecastActive": False, "pruneClassesOwn": {}, "pruneClassesJudge": {},
                "decisionsAcknowledged": 100, "judgeAckedDecisions": 100, "dropWitnessLimits": {},
                "ledgerBudgetMismatches": [], "summaryStatus": "pass", "run00Status": "succeeded",
                "manifest": {"policyConfigurationHash": "p" * 64, "binarySha256": "b" * 64, "coreCommitSha": "c" * 40,
                             "scenarioContentHash": f"s-{c}"},
                "repositoryInventorySha256": "i" * 64, "requestIdsSha256": f"r-{c}", "breaches": {},
                "breachRecordsToExplain": [], "nonNormalDecisionHashes": [], "judgeDecisionsWithCumulativeOnly": 0,
                "judgeRequestsWithCumulativeOnly": 0, "judgeOfWhichFinallyRejected": 0, "ridersWithForcedBreach": 0,
                "latePickups": 0, "cumulativeOnlyOwn": []}
    doc = {"dryRun": True, "mode": "dryrun", "roleSources": {r: r for r in rs.ROLES},
           "extractor": {"file": "synthetic", "sha256": "0" * 64}, "python": {"executable": "synthetic", "version": "x"},
           "ridersTsv": {"file": "synthetic", "sha256": "", "rows": sum(len(v) for v in riders.values())},
           "studyState": {"latches": []}, "cells": cells}
    return doc, riders, runs


SYNTH_BASELINES = {("SF-U", "served"): 2380, ("SF-C30", "served"): 2244, ("SF-U", "late60"): 204,
                   ("SF-C30", "late60"): 68, ("SF-U", "timely60"): 2176, ("SF-C30", "timely60"): 2176,
                   ("SF-U", "fc_late60"): 204, ("SF-C30", "fc_late60"): 68, ("SF-U", "late120"): 0,
                   ("SF-U", "exposed"): 0, ("SF-U", "shared"): 0, ("SF-U", "h7_served"): 0}


def part_a4(cells):
    log("A4 analyse() end to end on synthetic data with known answers")
    w0817 = [c for c in cells if c[-3:] in ("w08", "w17")]
    for n_chosen, h4 in ((15, True), (14, False)):
        chosen = set(w0817[:n_chosen])
        doc, riders, runs = synthetic_data(cells, chosen)
        lines, R = rs.analyse(doc, riders, runs, cells, baselines=SYNTH_BASELINES, sf_w0817=25)
        text = "\n".join(lines)
        check(f"[{n_chosen} chosen] not stopped, baselines reproduced", "stopped" not in R and R["baselines"],
              str(R.get("stopped")))
        check(f"[{n_chosen} chosen] I_L = {3 * n_chosen}, I_served = {2 * n_chosen}, I_timely60 = {-n_chosen}",
              (R["I_L"], R["I_served"], R["I_timely60"]) == (3 * n_chosen, 2 * n_chosen, -n_chosen),
              f"got {(R['I_L'], R['I_served'], R['I_timely60'])}")
        check(f"[{n_chosen} chosen] identity holds pooled, per cell, per window and per day", R["identity"] is True)
        check(f"[{n_chosen} chosen] 2x2 interactions sum to 0", R["interaction_sum"] == 0)
        check(f"[{n_chosen} chosen] cell signs (> 0, = 0, < 0) = ({n_chosen}, {34 - n_chosen}, 0)",
              R["cells_IL_signs"] == (n_chosen, 34 - n_chosen, 0), str(R["cells_IL_signs"]))
        check(f"[{n_chosen} chosen] w08/w17 cells with I_L,c > 0 = {n_chosen}", R["cells_IL_pos_w0817"] == n_chosen)
        per_day = collections.Counter(c[:9] for c in chosen)
        want_lodo = {d: 3 * n_chosen - 3 * per_day[d] for d in sorted({c[:9] for c in cells})}
        check(f"[{n_chosen} chosen] leave-one-day-out I_L = {want_lodo}", R["lodo"] == want_lodo, str(R["lodo"]))
        want_days = (sum(1 for d in want_lodo if per_day[d]), sum(1 for d in want_lodo if not per_day[d]), 0)
        check(f"[{n_chosen} chosen] day signs = {want_days}", R["days_IL_signs"] == want_days, str(R["days_IL_signs"]))
        check(f"[{n_chosen} chosen] H4 = {h4}", R["predictions"]["H4"] is h4)
        check(f"[{n_chosen} chosen] the I_L line shows its arithmetic",
              f"I_L        = (" in text and f"= {3 * n_chosen:+d} rider-runs" in text)
    doc, riders, runs = synthetic_data(cells, set())
    bad = dict(SYNTH_BASELINES)
    bad[("SF-U", "served")] = 2381
    lines, R = rs.analyse(doc, riders, runs, cells, baselines=bad, sf_w0817=25)
    check("a baseline mismatch stops before the CF tables", R.get("stopped") == "baselines"
          and not any(line.startswith("\n## 2.") for line in lines))
    lines, R = rs.analyse(doc, riders, runs, cells)          # the plan's own baselines cannot match synthetic data
    check("the plan baselines do not reproduce on synthetic data -> stop", R.get("stopped") == "baselines")
    doc, riders, runs = synthetic_data(cells, set())
    runs[("CF-U", cells[0])]["served"] = 71                    # run record disagrees with the rider rows
    _, R = rs.analyse(doc, riders, runs, cells, baselines=SYNTH_BASELINES, sf_w0817=25)
    check("served rows != run records -> validity stop", R.get("stopped") == "validity")
    doc, riders, runs = synthetic_data(cells, set())
    runs[("CF-U", cells[0])]["requestIdsSha256"] = "other"
    _, R = rs.analyse(doc, riders, runs, cells, baselines=SYNTH_BASELINES, sf_w0817=25)
    check("a cell whose roles have different request sets -> validity stop", R.get("stopped") == "validity")


def write_json(path: pathlib.Path, value):
    path.write_text(json.dumps(value), encoding="utf-8", newline="\n")


def make_run(folder: pathlib.Path, label: str, status="pass", run_status="succeeded", extra=None, transcript=b"t\n"):
    folder.mkdir(parents=True)
    files = {"summary.json": json.dumps({"status": status, "label": label}).encode(),
             "run-00.json": json.dumps({"status": run_status, "label": label}).encode(),
             "transcript-00.ndjson": transcript}
    for name, data in files.items():
        (folder / name).write_bytes(data)
    manifest = {"files": [{"path": n, "sha256": sha(d), "lengthBytes": len(d)} for n, d in sorted(files.items())]}
    (folder / "bundle-manifest.json").write_bytes(json.dumps(manifest).encode())
    for name in extra or ():
        (folder / name).write_bytes(b"{}")
    return folder


def part_a5(work: pathlib.Path):
    log("A5 extractor gate on synthetic folders, logs, anchors and harness files (work folder only)")
    out, logs = work / "a5-out" / "main", work / "a5-logs" / "main"
    logs.mkdir(parents=True)
    cell = "d20181116-s10-r1-w08"

    def spec(job, label=None):
        return {"folder": str(out / job), "label": label or f"{job}-s1b", "job": job, "pin": None}

    make_run(out / f"CF-U-{cell}", f"CF-U-{cell}-s1b")
    check("complete passing CF run -> no problem", ex.folder_problems(spec(f"CF-U-{cell}")) == [])
    p = ex.folder_problems(spec(f"CF-C-30-{cell}"))
    check("missing CF run -> MISSING", len(p) == 1 and "MISSING" in p[0], str(p))
    failed = out / f"CF-C-30-{cell}"
    failed.mkdir(parents=True)
    (failed / "failure-00.json").write_bytes(b"{}")
    (failed / "transcript-00.ndjson").write_bytes(b"t\n")
    p = ex.folder_problems(spec(f"CF-C-30-{cell}"))
    check("failed run (failure-00.json, no summary) -> FAILED", any("FAILED" in x for x in p), str(p))
    make_run(out / "CF-U-x1", "CF-U-x1-s1b", status="fail")
    check("summary status fail -> FAILED", any("summary status" in x for x in ex.folder_problems(spec("CF-U-x1"))))
    make_run(out / "CF-U-x2", "CF-U-x2-s1b", run_status="failed")
    check("run-00 status failed -> FAILED", any("run-00 status" in x for x in ex.folder_problems(spec("CF-U-x2"))))
    make_run(out / "CF-U-x3", "CF-U-x3-t38")
    check("wrong label -> problem", any("label" in x for x in ex.folder_problems(spec("CF-U-x3"))))
    make_run(out / "CF-U-x4", "CF-U-x4-s1b", extra=["failure-00.json"])
    check("unexpected file in a run folder -> problem", any("unexpected" in x for x in ex.folder_problems(spec("CF-U-x4"))))
    sf = {"folder": str(out / f"U-{cell}"), "label": f"U-{cell}-t38", "job": f"U-{cell}", "pin": None}
    make_run(out / f"U-{cell}", f"U-{cell}-t38")
    check("archived SF folder without a bundle pin -> problem", any("pin" in x for x in ex.folder_problems(sf)))
    b, other = "a" * 64, "b" * 64
    job = f"CF-U-{cell}"
    (logs / f"{job}.log").write_text(f"returncode=0 seconds=900 analysis-binding {b}\nstdout\n", encoding="utf-8", newline="\n")
    (logs / f"{job}.admitted").write_text(f"analysis-binding {b}\n", encoding="utf-8", newline="\n")
    check("log returncode 0 and anchor under the binding -> no problem", ex.cf_log_problems(job, b, logs) == [])
    check("log and anchor under another binding -> 2 problems", len(ex.cf_log_problems(job, other, logs)) == 2)
    (logs / "J1.log").write_text(f"returncode=1 seconds=900 analysis-binding {b}\n", encoding="utf-8", newline="\n")
    (logs / "J1.admitted").write_text(f"analysis-binding {b}\n", encoding="utf-8", newline="\n")
    p = ex.cf_log_problems("J1", b, logs)
    check("log returncode 1 -> FAILED", len(p) == 1 and "returncode=1" in p[0], str(p))
    (logs / "J2.log").write_text(f"returncode=0 seconds=900 analysis-binding {b}\n", encoding="utf-8", newline="\n")
    p = ex.cf_log_problems("J2", b, logs)
    check("no admission anchor -> problem", len(p) == 1 and "anchor" in p[0], str(p))
    (logs / "J3.admitted").write_text(f"analysis-binding {b}\n", encoding="utf-8", newline="\n")
    p = ex.cf_log_problems("J3", b, logs)
    check("no main job log -> problem", len(p) == 1 and "no main job log" in p[0], str(p))
    (logs / "J4.log").write_text("returncode=0 seconds=900\n", encoding="utf-8", newline="\n")
    (logs / "J4.admitted").write_text(f"analysis-binding {b}\n", encoding="utf-8", newline="\n")
    check("log first line without a binding -> malformed", any("malformed" in x for x in ex.cf_log_problems("J4", b, logs)))
    expected = {f"CF-U-{cell}", f"CF-C-30-{cell}", "CF-U-x1", "CF-U-x2", "CF-U-x3", "CF-U-x4", f"U-{cell}"}
    check("main folder without extra entries -> no problem", ex.unexpected_main_entries(expected, out) == [])
    (out / f"CF-U-{cell}-rerun1").mkdir()
    p = ex.unexpected_main_entries(expected, out)
    check("an unexpected rerun folder in main -> problem", len(p) == 1 and "rerun1" in p[0], str(p))
    harness = work / "a5-harness"
    harness.mkdir()
    for name in ex.STUDY_FILES:
        (harness / name).write_text("ok\n", encoding="utf-8", newline="\n")
    (harness / "extract_stage1b.py").write_text("# stand-in\n", encoding="utf-8", newline="\n")
    (harness / "report_stage1b.py").write_text("# stand-in 2\n", encoding="utf-8", newline="\n")
    (harness / "STAGE1B-ANALYSIS-AMENDMENT.md").write_text("# stand-in record\n", encoding="utf-8", newline="\n")
    freeze = (f"{ex.sha256_file(harness / 'extract_stage1b.py')}  extract_stage1b.py\n"
              f"{ex.sha256_file(harness / 'report_stage1b.py')}  report_stage1b.py\n"
              f"{ex.sha256_file(harness / 'STAGE1B-ANALYSIS-AMENDMENT.md')}  STAGE1B-ANALYSIS-AMENDMENT.md\n")
    (harness / ex.FREEZE.name).write_text(freeze, encoding="utf-8", newline="\n")
    binding = f"# binding\nmanifest-sha256 {sha(freeze.encode())}\n" + freeze
    (harness / ex.BINDING.name).write_text(binding, encoding="utf-8", newline="\n")
    state, p = ex.study_state(harness)
    check("complete study state -> no problem", p == [] and state["bindingSha256"] == sha(binding.encode()), str(p))
    (harness / "SMOKE-FAILED.txt").write_text("x\n", encoding="utf-8", newline="\n")
    _, p = ex.study_state(harness)
    check("a *-FAILED.txt latch -> problem", any("SMOKE-FAILED.txt" in x for x in p), str(p))
    (harness / "SMOKE-FAILED.txt").rename(harness / "SMOKE-FAILED.txt.moved")
    (harness / "INPUT-DRIFT-STOP.txt").write_text("x\n", encoding="utf-8", newline="\n")
    _, p = ex.study_state(harness)
    check("INPUT-DRIFT-STOP.txt -> problem", any("INPUT-DRIFT-STOP.txt" in x for x in p), str(p))
    (harness / "INPUT-DRIFT-STOP.txt").rename(harness / "INPUT-DRIFT-STOP.txt.moved")
    (harness / "report_stage1b.py").write_text("# changed after the binding\n", encoding="utf-8", newline="\n")
    _, p = ex.study_state(harness)
    check("report changed after the binding -> problem", any("report_stage1b.py" in x for x in p), str(p))
    (harness / "report_stage1b.py").write_text("# stand-in 2\n", encoding="utf-8", newline="\n")
    (harness / "STAGE1B-ANALYSIS-AMENDMENT.md").write_text("# edited record\n", encoding="utf-8", newline="\n")
    _, p = ex.study_state(harness)
    check("any other bound file (the amendment record) changed after the binding -> problem",
          any("STAGE1B-ANALYSIS-AMENDMENT.md" in x for x in p), str(p))
    (harness / "STAGE1B-ANALYSIS-AMENDMENT.md").write_text("# stand-in record\n", encoding="utf-8", newline="\n")
    _, p = ex.study_state(harness)
    check("restored bound files -> no problem again", p == [], str(p))
    (harness / ex.FREEZE.name).write_text(freeze + "# appended\n", encoding="utf-8", newline="\n")
    _, p = ex.study_state(harness)
    check("freeze manifest not the one named by the binding -> problem", any(ex.FREEZE.name in x for x in p), str(p))
    (harness / ex.FREEZE.name).write_text(freeze, encoding="utf-8", newline="\n")
    (harness / "SMOKE-INSPECTION.txt").rename(harness / "SMOKE-INSPECTION.txt.moved")
    _, p = ex.study_state(harness)
    check("missing SMOKE-INSPECTION.txt -> problem", any("SMOKE-INSPECTION.txt" in x for x in p), str(p))


def part_a6(work: pathlib.Path):
    log("A6 extractor guards: bundle and pins, read window, identity, pin_problems, output paths")
    folder = make_run(work / "a6-run", "L", transcript=b"line\n")
    hashes = ex.verify_bundle(folder, None)
    check("verify_bundle on a consistent bundle returns the transcript SHA-256",
          hashes["transcript-00.ndjson"] == sha(b"line\n"))
    pin = sha((folder / "bundle-manifest.json").read_bytes())
    check("verify_bundle with the right pin passes", ex.verify_bundle(folder, pin)["bundle-manifest.json"] == pin)
    for name, action in (("a wrong pin", lambda: ex.verify_bundle(folder, "0" * 64)),):
        try:
            action()
            check(f"verify_bundle refuses {name}", False)
        except ex.Refusal:
            check(f"verify_bundle refuses {name}", True)
    tampered = make_run(work / "a6-tampered", "L", transcript=b"line\n")
    (tampered / "transcript-00.ndjson").write_bytes(b"lin3\n")          # same length, other bytes
    try:
        ex.verify_bundle(tampered, None)
        check("verify_bundle refuses a same-length tampered transcript", False)
    except ex.Refusal:
        check("verify_bundle refuses a same-length tampered transcript", True)
    real = ex.SF_ROOT / "U-d20181116-s10-r1-w08"
    try:
        ex.verify_bundle(real, "f" * 64)
        check("verify_bundle refuses a real SF folder under a wrong pin (before hashing the transcript)", False)
    except ex.Refusal:
        check("verify_bundle refuses a real SF folder under a wrong pin (before hashing the transcript)", True)
    inside = str(folder / "transcript-00.ndjson")
    stdlib = str(pathlib.Path(sys.base_prefix) / "Lib" / "json" / "__init__.py")
    pinned = next(iter(ex.PINNED_DATA))
    window = {"reads": [inside, stdlib, pinned], "writes": [], "hookErrors": 0}
    check("read window: run folder, interpreter and pinned files only -> no problem",
          ex.window_problems(window, folder) == [])
    window["reads"].append(str(ex.HERE / "STAGE1B-PLAN-DRAFT-v7.md"))
    check("read window: an unpinned file read -> problem", len(ex.window_problems(window, folder)) == 1)
    window = {"reads": [], "writes": [inside], "hookErrors": 0}
    check("read window: any write -> problem", len(ex.window_problems(window, folder)) == 1)
    good_spec = {"job": "J", "label": "J-s1b", "policy": "p" * 64, "dll": "d" * 64}
    semantic = "e" * 64
    summary = {"status": "pass", "repeatCount": 1, "label": "J-s1b", "repositoryInventorySha256": ex.INVENTORY,
               "semanticHash": semantic, "sourceScenarioContentSha256": "f" * 64,
               "runs": [{"repeat": 0, "semanticHash": semantic}]}
    run00 = {"status": "succeeded", "repeat": 0, "label": "J-s1b", "artifactReceiptsEqual": True,
             "semanticHash": semantic, "semantic": {"requestCount": 108, "sourceScenarioContentSha256": "f" * 64}}
    manifest = {"policyConfigurationHash": "p" * 64, "binarySha256": "d" * 64, "coreCommitSha": ex.CORE_COMMIT,
                "masterSeed": 7, "policyId": ex.POLICY_ID, "policyVersion": ex.POLICY_VERSION}
    check("identity of a consistent run -> no problem", ex.identity_problems(good_spec, summary, run00, manifest) == [])
    mutations = [("policy hash", "manifest", "policyConfigurationHash", "q" * 64),
                 ("Runner DLL", "manifest", "binarySha256", ex.OLD_RUNNER_DLL),
                 ("core commit", "manifest", "coreCommitSha", "x"), ("seed", "manifest", "masterSeed", 8),
                 ("seed type", "manifest", "masterSeed", 7.0), ("policy version", "manifest", "policyVersion", "v2"),
                 ("label", "summary", "label", "J-t38"), ("inventory", "summary", "repositoryInventorySha256", "0"),
                 ("repeatCount", "summary", "repeatCount", 2), ("receipts", "run00", "artifactReceiptsEqual", False),
                 ("semantic hash", "run00", "semanticHash", "1" * 64), ("status", "run00", "status", "failed")]
    for name, where, key, value in mutations:
        parts = {"summary": json.loads(json.dumps(summary)), "run00": json.loads(json.dumps(run00)),
                 "manifest": dict(manifest)}
        parts[where][key] = value
        p = ex.identity_problems(good_spec, parts["summary"], parts["run00"], parts["manifest"])
        check(f"identity refuses a changed {name}", len(p) >= 1, str(p))
    r = json.loads(json.dumps(run00))
    r["semantic"]["requestCount"] = 107
    check("identity refuses a request count of 107", len(ex.identity_problems(good_spec, summary, r, manifest)) >= 1)
    check("pin_problems: every pinned reader and data file matches now", ex.pin_problems() == [])
    saved = dict(ex.PINNED_MODULES)
    ex.PINNED_MODULES["audit_vep"] = (saved["audit_vep"][0], "0" * 64)
    check("pin_problems reports a changed reader SHA-256", len(ex.pin_problems()) == 1)
    ex.PINNED_MODULES.clear()
    ex.PINNED_MODULES.update(saved)
    for name, prefix in (("an input root", r"C:/RideBoundData/research/should-not-exist"),):
        try:
            ex.extract("dryrun", {"CF-U": "SF-U", "CF-C30": "SF-C30", "SF-U": "SF-U", "SF-C30": "SF-C30"}, prefix)
            check(f"extract refuses an output prefix under {name}", False)
        except ex.Refusal:
            check(f"extract refuses an output prefix under {name}", not pathlib.Path(prefix + "-riders.tsv").exists())
    existing = work / "a6-existing"
    (work / "a6-existing-riders.tsv").write_text("x\n", encoding="utf-8", newline="\n")
    try:
        ex.extract("dryrun", {"CF-U": "SF-U", "CF-C30": "SF-C30", "SF-U": "SF-U", "SF-C30": "SF-C30"}, str(existing))
        check("extract refuses an existing output (mode x)", False)
    except ex.Refusal:
        check("extract refuses an existing output (mode x)", not (work / "a6-existing-runs.json").exists())
    check("runner-tier3-v3 DLL is read from the sealed manifest and is not the v2 DLL",
          ex.runner_v3_dll() != ex.OLD_RUNNER_DLL)


def part_a7(cells):
    log("A7 the verbatim copies equal their originals")
    sys.path.insert(0, str(SCRATCH / "optimize"))
    sys.path.insert(0, str(SCRATCH / "optimize" / "codex-debate" / "claude-work"))
    import forecast_family_a as fa  # noqa: E402  (originals, read-only, imported only by this test)
    import budget_use_check as buc  # noqa: E402
    for name in ("U-d20181116-s10-r1-w08", "C-30-d20181114-s10-r3-w17-rerun1"):
        t = ex.SF_ROOT / name / "transcript-00.ndjson"
        check(f"event_times copy == sweep_analyze.event_times on {name}", ex.event_times(t) == fa.sweep_analyze.event_times(t))
    t = ex.C120_ROOT / "C-120-d20181114-s10-r3-w08" / "transcript-00.ndjson"
    check("event_times copy == sweep_analyze.event_times on C-120-d20181114-s10-r3-w08",
          ex.event_times(t) == fa.sweep_analyze.event_times(t))
    names = [f"{arm}-{c}" for arm in ("U", "C-30") for c in cells]
    same = sum(ex.sf_effective(ex.SF_ROOT, n) == fa.t38_calibrate.effective(n) for n in names)
    check(f"sf_effective == t38_calibrate.effective for {len(names)} SF names", same == len(names), f"{same}")
    check("sf_effective resolves C-30-d20181114-s10-r3-w17 to its -rerun1",
          ex.sf_effective(ex.SF_ROOT, "C-30-d20181114-s10-r3-w17") == "C-30-d20181114-s10-r3-w17-rerun1")
    check("every resolved SF folder has a bundle pin", all(ex.sf_effective(ex.SF_ROOT, n) in ex.SF_BUNDLE_PINS
                                                          for n in names) and len(ex.SF_BUNDLE_PINS) == 68)
    check("day_of copy == forecast_family_a.day_of for 34 cells", all(ex.day_of(c) == fa.day_of(c) for c in cells))
    rng, bad = random.Random(7), 0
    for _ in range(500):
        xs = [rng.uniform(0, 600) for _ in range(rng.randint(0, 40))]
        for p in (0.5, 0.9):
            a, b = rs.q(xs, p), buc.q(xs, p)
            bad += not (a == b or (math.isnan(a) and math.isnan(b)))
    check("q copy == budget_use_check.q on 500 random lists x 2 percentiles (empty lists included)", bad == 0)


def part_a8(work: pathlib.Path, cells):
    log("A8 report binding check, layout consistency with the v7 driver, policy bindings, CF job mapping")
    saved = rs.BINDING
    rep = ex.sha256_file(HERE / "report_stage1b.py")
    text = f"# synthetic\nmanifest-sha256 {'0' * 64}\n{rep}  report_stage1b.py\n{'e' * 64}  extract_stage1b.py\n"
    binding = work / "a8-binding.txt"
    binding.write_bytes(text.encode("utf-8"))
    doc = {"extractor": {"file": "extract_stage1b.py", "sha256": "e" * 64}, "studyState": {"bindingSha256": sha(text.encode())}}
    try:
        rs.BINDING = binding
        check("report binding check passes when report, extractor and binding agree", rs.binding_problems(doc) == [])
        bad = json.loads(json.dumps(doc))
        bad["extractor"]["sha256"] = "f" * 64
        check("report binding check refuses an extraction by an unbound extractor", len(rs.binding_problems(bad)) == 1)
        bad = json.loads(json.dumps(doc))
        bad["studyState"]["bindingSha256"] = "1" * 64
        check("report binding check refuses a binding changed since the extraction", len(rs.binding_problems(bad)) == 1)
        other = text.replace(rep, "2" * 64)
        binding2 = work / "a8-binding2.txt"
        binding2.write_bytes(other.encode("utf-8"))
        rs.BINDING = binding2
        doc2 = json.loads(json.dumps(doc))
        doc2["studyState"]["bindingSha256"] = sha(other.encode())
        check("report binding check refuses a report not bound with its current SHA-256", len(rs.binding_problems(doc2)) == 1)
    finally:
        rs.BINDING = saved
    drv = (HERE / "run_stage1b-v7.py").read_text(encoding="utf-8")
    needles = ['OUT_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-stage1b-v1")', 'ARMS = ("U", "C-30")',
               'return [(f"CF-{arm}-{cell}", arm, cell, "on", f"CF-{arm}-{cell}-s1b") for cell in cells for arm in ARMS]',
               'LOG_ROOT = HERE / "logs"', 'return LOG_ROOT / "main" / f"{name}.admitted"',
               'handle.write(f"analysis-binding {ANALYSIS_BINDING_SHA}' + chr(92) + 'n")',
               'anchor = f" analysis-binding {ANALYSIS_BINDING_SHA}" if phase == "main" else ""',
               'handle.write(f"returncode={completed.returncode} seconds={time.time() - started:.0f}{anchor}' + chr(92)
               + 'n"',
               'ANALYSIS_BINDING = HERE / "stage1b-analysis-binding.txt"',
               'ANALYSIS_FREEZE = HERE / "stage1b-analysis-freeze.sha256"',
               'SMOKE_PASSED, SMOKE_FAILED = HERE / "SMOKE-PASSED.txt", HERE / "SMOKE-FAILED.txt"',
               'INSPECTION = HERE / "SMOKE-INSPECTION.txt"', 'DRIFT_STOP = HERE / "INPUT-DRIFT-STOP.txt"',
               'f"manifest-sha256 {hashlib.sha256(manifest_bytes).hexdigest()}"',
               'ANALYSIS_LINE = re.compile(r"^([0-9a-f]{64})  ([A-Za-z0-9][A-Za-z0-9._-]*)$")']
    missing = [n for n in needles if n not in drv]
    check(f"run_stage1b-v7.py has the {len(needles)} layout facts the extractor relies on", not missing, str(missing))
    cmp_src = (HERE / "compare_equivalence_stage1b-v7.py").read_text(encoding="utf-8")
    needles = ['GATE0_PASS_FILE, GATE0_FAIL_FILE = "GATE0-BEHAVIOURAL-PASSED.txt", "GATE0-BEHAVIOURAL-FAILED.txt"',
               'BUNDLE_FILES = ("run-00.json", "summary.json", "transcript-00.ndjson")',
               f'("on", "U"): "{ex.POLICY_BINDING["CF-U"]}"', f'("on", "C-30"): "{ex.POLICY_BINDING["CF-C30"]}"',
               f'OLD_RUNNER_DLL = "{ex.OLD_RUNNER_DLL}"']
    missing = [n for n in needles if n not in cmp_src]
    check("compare_equivalence_stage1b-v7.py agrees on verdict names, bundle files, CF bindings and the v2 DLL",
          not missing, str(missing))
    found = {}
    for line in (HERE / "make_wp4_costfirst.log").read_text(encoding="utf-8").splitlines():
        parts = line.split()
        if parts and parts[0] in ("U", "C-30") and "OFF" in parts:
            found[parts[0]] = (parts[parts.index("OFF") + 2], parts[parts.index("ON") + 2])
    check("policy bindings equal make_wp4_costfirst.log (OFF = SF, ON = CF)",
          found == {"U": (ex.POLICY_BINDING["SF-U"], ex.POLICY_BINDING["CF-U"]),
                    "C-30": (ex.POLICY_BINDING["SF-C30"], ex.POLICY_BINDING["CF-C30"])}, str(found))
    cell = cells[0]
    u, c30 = ex.expected_run("CF-U", cell, "d" * 64), ex.expected_run("CF-C30", cell, "d" * 64)
    check("expected_run maps CF-U / CF-C30 to main/CF-U-<cell> and main/CF-C-30-<cell> with -s1b labels",
          (u["folder"], u["label"], c30["folder"], c30["label"]) ==
          (str(ex.CF_ROOT / f"CF-U-{cell}"), f"CF-U-{cell}-s1b", str(ex.CF_ROOT / f"CF-C-30-{cell}"), f"CF-C-30-{cell}-s1b")
          and u["dll"] == c30["dll"] == "d" * 64 and u["pin"] is None and str(ex.CF_ROOT).endswith("main"))


def part_a9(work: pathlib.Path):
    log("A9 readers() closure refusals and the live read window (fresh subprocesses)")
    head = f"import sys, pathlib\nsys.path.insert(0, r'{HERE}')\nimport extract_stage1b as ex\n"
    cases = [
        ("an unpinned scratchpad module (world_v1 removed from the pins) is refused",
         "del ex.PINNED_MODULES['world_v1']\n", "unpinned scratchpad module world_v1"),
        ("a factor folder other than the pinned one is refused", "ex.FACTORS = ex.FACTORS.parent\n",
         "preflight_world.FACTORS is not the pinned factor folder"),
    ]
    for name, patch, want in cases:
        code = head + patch + "try:\n    ex.readers()\n    print('NO REFUSAL')\nexcept ex.Refusal as e:\n    print('REFUSED', e)\n"
        done = subprocess.run([PY, "-B", "-c", code], cwd=str(HERE), capture_output=True, text=True, encoding="utf-8")
        check(name, want in done.stdout and "REFUSED" in done.stdout, (done.stdout + done.stderr)[-200:])
    target = work / "a9-written.txt"
    code = head + (
        "ex.readers()\n"
        "folder = pathlib.Path(r'%s')\n"
        "ex._WINDOW = {'reads': [], 'writes': [], 'hookErrors': 0}\n"
        "open(r'%s', encoding='utf-8').read(100)\n"
        "open(r'%s', 'x').close()\n"
        "w, ex._WINDOW = ex._WINDOW, None\n"
        "print('READS', len(w['reads']), 'WRITES', len(w['writes']))\n"
        "for p in ex.window_problems(w, folder):\n    print('PROBLEM', p)\n") % (
        ex.SF_ROOT / "U-d20181116-s10-r1-w08", HERE / "STAGE1B-PLAN-DRAFT-v7.md", target)
    done = subprocess.run([PY, "-B", "-c", code], cwd=str(HERE), capture_output=True, text=True, encoding="utf-8")
    out = done.stdout
    check("the live audit hook records the read and the write", "READS 1 WRITES 1" in out, out[-300:] + done.stderr[-300:])
    check("window_problems flags the unpinned read and the write", out.count("PROBLEM") == 2
          and "unpinned file read" in out and "opened for writing" in out, out[-300:])


# ============================================================================== Part B

def run_cli(args, timeout=1800):
    started = time.time()
    done = subprocess.run([PY, "-B"] + [str(a) for a in args], cwd=str(HERE), capture_output=True, text=True,
                          encoding="utf-8", errors="replace", timeout=timeout)
    return done.returncode, done.stdout + done.stderr, time.time() - started


def stage1a_rows():
    out = {}
    with open(STAGE1A, encoding="utf-8") as fh:
        header = fh.readline().rstrip("\n").split("\t")
        for line in fh:
            r = dict(zip(header, line.rstrip("\n").split("\t")))
            out[(r["arm"], r["cell"], r["requestId"])] = r
    return out


def new_rows(prefix):
    out = {}
    with open(f"{prefix}-riders.tsv", encoding="utf-8") as fh:
        header = fh.readline().rstrip("\n").split("\t")
        for line in fh:
            r = dict(zip(header, line.rstrip("\n").split("\t")))
            out[(r["role"], r["cell"], r["requestId"])] = r
    return out


COMMON = ("lifecycle", "t0", "p0", "D0", "A", "sumAbsC", "nonzeroC", "netC", "shared")


def compare_rows(new, old, role, arm):
    mine = {(c, r): v for (ro, c, r), v in new.items() if ro == role}
    theirs = {(c, r): v for (a, c, r), v in old.items() if a == arm}
    diff = sum(1 for k in mine.keys() | theirs.keys() if k not in mine or k not in theirs
               or any(mine[k][f] != theirs[k][f] for f in COMMON))
    served_flag = all((v["served"] == "1") == (v["A"] != "" and v["lifecycle"] == "completed") for v in mine.values())
    return len(mine), len(theirs), diff, served_flag


def dry(work, name, cfu, cfc):
    prefix = work / name
    code, out, secs = run_cli(["extract_stage1b.py", "dryrun", cfu, cfc, prefix])
    check(f"{name}: extractor dryrun {cfu} {cfc} exits 0 ({secs:.0f} s)", code == 0, out.strip().splitlines()[-1][:300]
          if out.strip() else "")
    code, out, secs = run_cli(["report_stage1b.py", prefix, f"{prefix}-report.txt"])
    check(f"{name}: report exits 0 and writes {prefix.name}-report.txt", code == 0
          and pathlib.Path(f"{prefix}-report.txt").is_file(), out[-300:] if code else "")
    doc, riders, runs, cells = rs.load(str(prefix))
    _, R = rs.analyse(doc, riders, runs, cells)
    text = pathlib.Path(f"{prefix}-report.txt").read_text(encoding="utf-8")
    check(f"{name}: every report section is labelled DRY RUN", text.count("[DRY RUN:") == 6 and "DRY RUN / PLUMBING" in text)
    return prefix, doc, riders, runs, cells, R, text


def part_b(work: pathlib.Path):
    log("B0 extractor main mode refuses now and writes nothing")
    main_existed, root_existed = ex.CF_ROOT.exists(), ex.CF_ROOT.parent.exists()
    code, out, _ = run_cli(["extract_stage1b.py", "main", work / "b0"])
    check("main mode exits 2 (refused)", code == 2, f"exit {code}")
    check("refusal names the 68 missing CF runs", "(68 missing run(s))" in out, out.splitlines()[1][:200] if out else "")
    check("nothing written by the refused run", not (work / "b0-riders.tsv").exists() and not (work / "b0-runs.json").exists())
    check(f"the refused run created no output folder (Stage-1b root existed before: {root_existed}; main: {main_existed})",
          ex.CF_ROOT.exists() == main_existed and ex.CF_ROOT.parent.exists() == root_existed)
    check("this test runs under the frozen job interpreter (main mode accepts it)",
          ex.norm(sys.executable) == ex.norm(ex.JOB_PYTHON) and "frozen job interpreter" not in out)
    ml = pathlib.Path(r"C:/RideBoundData/envs/ml-v1/Scripts/python.exe")
    if ml.is_file():
        done = subprocess.run([str(ml), "-B", "extract_stage1b.py", "main", str(work / "b0ml")], cwd=str(HERE),
                              capture_output=True, text=True, encoding="utf-8", errors="replace")
        check("main mode under another interpreter (ml-v1 venv) is refused for that reason too", done.returncode == 2
              and "frozen job interpreter" in done.stdout, done.stdout[-200:] + done.stderr[-200:])
    old = stage1a_rows()

    log("B1 dryrun SF-U SF-C30 (SF as both selectors): every interaction is 0")
    prefix, doc, riders, runs, cells, R, text = dry(work, "b1", "SF-U", "SF-C30")
    check("B1 baselines reproduced (19 values + 25/25 cells)", R.get("baselines") is True and "stopped" not in R)
    check("B1 I_L = I_served = I_timely60 = 0", (R["I_L"], R["I_served"], R["I_timely60"]) == (0, 0, 0))
    check("B1 identity holds", R["identity"] is True)
    check("B1 I_L,c = 0 in 34 / 34 cells", R["cells_IL_signs"] == (0, 34, 0))
    check("B1 I_L per day = 0 in 5 / 5 days; leave-one-day-out all 0", R["days_IL_signs"] == (0, 5, 0)
          and set(R["lodo"].values()) == {0})
    M = R["M"]
    check("B1 every metric of CF-U equals SF-U and of CF-C30 equals SF-C30", M["CF-U"] == M["SF-U"]
          and M["CF-C30"] == M["SF-C30"])
    check("B1 served-set overlap CF vs SF of the same cap is complete", R["overlap"][("CF-U", "SF-U")] == (2434, 0, 0)
          and R["overlap"][("CF-C30", "SF-C30")] == (2338, 0, 0))
    check("B1 SF-U vs SF-C30 overlap = Stage 1a (2207 common, 227 only U, 131 only C-30)",
          R["overlap"][("SF-U", "SF-C30")] == (2207, 227, 131))
    check("B1 predictions on the stand-in: H1 PASS, H2 F, H3 T, H4 F, H5 T (+31), H6 F, H7 F (4)",
          R["predictions"] == {"G0": None, "H1": "PASS", "H2": False, "H3": True, "H4": False, "H5": True, "H6": False,
                               "H7": False}, str(R["predictions"]))
    check("B1 numbers are printed with their denominators", "203 / 2434 (8.34%)" in text and "2231 / 3672 (60.76%)" in text
          and "555 / 2434 (22.80%)" in text)
    new = new_rows(prefix)
    for role, arm in (("SF-U", "U"), ("SF-C30", "C-30"), ("CF-U", "U"), ("CF-C30", "C-30")):
        n_new, n_old, diff, flag = compare_rows(new, old, role, arm)
        check(f"B1 {role} rider rows == Stage 1a {arm} rows on {', '.join(COMMON)}", diff == 0 and n_new == n_old,
              f"{n_new} vs {n_old}, {diff} differ")
        check(f"B1 {role} served flag == (completed and alighted) on every row", flag)
    check("B1 runs.json records the extractor SHA-256 and the pins", doc["extractor"]["sha256"] ==
          ex.sha256_file(HERE / "extract_stage1b.py") and len(doc["pinnedModules"]) == 10 and doc["sfBundlePins"] == 68)
    check("B1 bundle-manifest SHA-256 of every SF run equals its pin", all(
        r["bundleSha256"]["bundle-manifest.json"] == ex.SF_BUNDLE_PINS[pathlib.Path(r["run"]).name]
        for r in doc["runs"]))
    sources = {"d20181114": ["2018-11-12_tt_factors.csv", "2018-11-13_tt_factors.csv"]}
    for d in ("d20181115", "d20181116"):
        sources[d] = ["2018-11-12_tt_factors.csv", "2018-11-13_tt_factors.csv", "2018-11-14_tt_factors.csv"]
    bad = [(r["role"], r["cell"], r["readWindow"]) for r in doc["runs"]
           if r["readWindow"]["runFolderOpens"] < 5 or r["readWindow"]["pinnedFilesRead"] != sources.get(r["cell"][:9], [])
           or r["forecastActive"] != bool(sources.get(r["cell"][:9]))]
    check("B1 the read window was live in all 136 role-runs (>= 5 run-folder opens) and the only other files read are "
          "the causal-no15 factor CSVs of active cells (14/11 <- 12,13; 15,16/11 <- 12,13,14; 17,18/11 none)",
          not bad, str(bad[:3]))

    log("B2 dryrun SF-C30 SF-U (selectors swapped): I_L = -254, I_served = -192, I_timely60 = +62")
    prefix2, doc2, riders2, runs2, cells2, R2, _ = dry(work, "b2", "SF-C30", "SF-U")
    check("B2 I_L = (76 - 203) - (203 - 76) = -254", R2["I_L"] == -254, str(R2["I_L"]))
    check("B2 I_served = (2338 - 2434) - (2434 - 2338) = -192", R2["I_served"] == -192, str(R2["I_served"]))
    check("B2 I_timely60 = (2262 - 2231) - (2231 - 2262) = +62", R2["I_timely60"] == 62, str(R2["I_timely60"]))
    check("B2 identity holds", R2["identity"] is True)
    cm = {(role, c): rs.outcome([r for r in riders2[role] if r["cell"] == c], 108) for role in rs.ROLES for c in cells2}
    ok = all(rs.interaction({role: cm[(role, c)]["late60"] for role in rs.ROLES})
             == -2 * (cm[("SF-U", c)]["late60"] - cm[("SF-C30", c)]["late60"]) for c in cells2)
    check("B2 I_L,c = -2 (L_SF,U,c - L_SF,C30,c) in every cell", ok)
    check("B2 w08/w17 cells with I_L,c > 0 = 0 (SF U > C30 in 25/25)", R2["cells_IL_pos_w0817"] == 0)

    log("B3 dryrun SF-U SF-C120 (C-120 as the capped stand-in): Stage 1a and Codex R19 reproduced")
    prefix3, doc3, riders3, runs3, cells3, R3, _ = dry(work, "b3", "SF-U", "SF-C120")
    check("B3 I_L = (203 - 132) - (203 - 76) = -56", R3["I_L"] == -56, str(R3["I_L"]))
    check("B3 I_served = (2434 - 2387) - (2434 - 2338) = -49", R3["I_served"] == -49, str(R3["I_served"]))
    check("B3 I_timely60 = (2231 - 2255) - (2231 - 2262) = +7", R3["I_timely60"] == 7, str(R3["I_timely60"]))
    check("B3 identity holds", R3["identity"] is True)
    c120 = R3["M"]["CF-C30"]
    want = {"served": 2387, "late60": 132, "late120": 21, "late300": 0, "early60": 1, "timely60": 2255,
            "timely120": 2366, "fc_late60": 65, "fc_late120": 8, "fc_late300": 0, "fc_early60": 2, "fc_timely60": 2322,
            "fc_timely120": 2379, "exposed": 103, "repeated": 0, "shared": 428}
    check("B3 C-120 stand-in reproduces Stage 1a (C120-RESULTS.md; stage1a-report-2026-10-10.txt:29-52)",
          all(c120[k] == v for k, v in want.items()), str({k: c120[k] for k in want if c120[k] != want[k]}))
    check("B3 C-120 mean forecast extension 7.27 s", f"{c120['fc_ext_s']:.2f}" == "7.27", f"{c120['fc_ext_s']}")
    cls = collections.Counter()
    for c in cells3:
        cls.update(runs3[("CF-C30", c)]["pruneClassesJudge"])
    check("B3 C-120 prune records 521 per-step-also + 3 cumulative-only", dict(cls) == {"per-step-also": 521,
                                                                                         "cumulative-only": 3}, str(cls))
    over30 = sum(1 for (a, _, _), r in old.items() if a == "C-120" and int(r["sumAbsC"]) > 30_000)
    check(f"B3 H1 on the C-120 stand-in: FAIL with {over30} riders over 30 s (from the Stage 1a rows)",
          R3["predictions"]["H1"] == "FAIL" and R3["D"]["CF-C30"]["over"] == over30, str(R3["D"]["CF-C30"]))
    new3 = new_rows(prefix3)
    n_new, n_old, diff, flag = compare_rows(new3, old, "CF-C30", "C-120")
    check("B3 C-120 rider rows == Stage 1a C-120 rows", diff == 0 and n_new == n_old == 2387, f"{n_new}/{n_old}/{diff}")
    codex = json.loads(CODEX_R19.read_text(encoding="utf-8"))
    by = {(r["arm"], r["cell"]): r for r in codex["runs"]}
    role_of = {"U": "CF-U", "C-30": "SF-C30", "C-120": "CF-C30"}
    mismatches = []
    for arm, role in role_of.items():
        for c in cells3:
            theirs, mine_rows = by[(arm, c)], [r for r in riders3[role] if r["cell"] == c]
            m = rs.outcome(mine_rows, 108)
            n, f = theirs["frontier"]["naive"], theirs["frontier"]["causal-no15"]
            pairs = [("served", m["served"], theirs["served"]), ("late60", m["late60"], n["late60"]),
                     ("late120", m["late120"], n["late120"]), ("late300", m["late300"], n["late300"]),
                     ("early60", m["early60"], n["early60"]), ("fc_late60", m["fc_late60"], f["late60"]),
                     ("fc_late120", m["fc_late120"], f["late120"]), ("fc_late300", m["fc_late300"], f["late300"]),
                     ("fc_early60", m["fc_early60"], f["early60"]),
                     ("exposed", m["exposed"], theirs["ledger"].get("drop_exposed", 0)),
                     ("repeated", m["repeated"], theirs["ledger"].get("two_or_more_nonzero_changes", 0)),
                     ("max S", max([r["S"] for r in mine_rows] or [0]), theirs["max_absolute_dispatch_ms"]),
                     ("served ids", sorted(r["id"] for r in mine_rows if r["served"]), sorted(theirs["served_ids"])),
                     ("acknowledged", runs3[(role, c)]["decisionsAcknowledged"], theirs["acknowledged_decisions"]),
                     ("non-normal", len(runs3[(role, c)]["nonNormalDecisionHashes"]), theirs["non_normal_decisions"]),
                     ("cumulative-only", runs3[(role, c)]["pruneClassesJudge"].get("cumulative-only", 0),
                      theirs["prune_classes"].get("cumulative-only-reported", 0)),
                     ("per-step", runs3[(role, c)]["pruneClassesJudge"].get("per-step-also", 0),
                      theirs["prune_classes"].get("single-increment-fails", 0)),
                     ("transcript", runs3[(role, c)]["bundleSha256"]["transcript-00.ndjson"], theirs["transcript_sha256"])]
            mismatches += [(arm, c, k) for k, a, b in pairs if a != b]
    check(f"B3 per-run numbers == Codex R19-stage1a-analysis.json for U, C-30, C-120 (102 runs x 18 quantities)",
          not mismatches, str(mismatches[:10]))

    log("B4 refusals")
    code, out, _ = run_cli(["extract_stage1b.py", "dryrun", "SF-U", "SF-C30", prefix])
    check("re-extraction to an existing prefix refused (exit 2)", code == 2, f"exit {code}")
    t = work / "b4t"
    raw = pathlib.Path(f"{prefix}-riders.tsv").read_bytes()
    pathlib.Path(f"{t}-riders.tsv").write_bytes(raw.replace(b"\t660", b"\t661", 1) if b"\t660" in raw else raw + b"x")
    shutil.copyfile(f"{prefix}-runs.json", f"{t}-runs.json")
    code, out, _ = run_cli(["report_stage1b.py", t, work / "b4t-report.txt"])
    check("report refuses a tampered riders.tsv (exit 2, nothing written)", code == 2
          and not (work / "b4t-report.txt").exists(), out[-200:])
    code, out, _ = run_cli(["report_stage1b.py", prefix, f"{prefix}-report.txt"])
    check("report refuses an existing output file (exit 2)", code == 2, out[-200:])
    m = work / "b4m"
    shutil.copyfile(f"{prefix}-riders.tsv", f"{m}-riders.tsv")
    main_doc = json.loads(pathlib.Path(f"{prefix}-runs.json").read_text(encoding="utf-8"))
    main_doc.update(mode="main", dryRun=False)
    pathlib.Path(f"{m}-runs.json").write_text(json.dumps(main_doc), encoding="utf-8", newline="\n")
    code, out, _ = run_cli(["report_stage1b.py", m, work / "b4m-report.txt"])
    check("report refuses a main-mode extraction without stage1b-analysis-binding.txt (exit 2)", code == 2
          and "binding" in out and not (work / "b4m-report.txt").exists(), out[-200:])


def main(argv):
    global LOG
    if len(argv) != 3:
        raise SystemExit(__doc__)
    log_path, work = pathlib.Path(argv[1]).resolve(), pathlib.Path(argv[2]).resolve()
    for p in (log_path, work):
        if not str(p).lower().startswith(str(HERE).lower() + os.sep):
            raise SystemExit(f"{p} is not inside the harness folder {HERE}")
    work.mkdir()                                          # must be new
    LOG = open(log_path, "x", encoding="utf-8", newline="\n")
    started = time.time()
    if os.name == "nt":       # below-normal priority, inherited by the extractor processes: Stage-1b jobs come first
        import ctypes
        kernel32 = ctypes.windll.kernel32
        kernel32.SetPriorityClass(kernel32.GetCurrentProcess(), 0x00004000)
    log(f"# test_analysis_stage1b.py, {time.strftime('%Y-%m-%d %H:%M:%S')} local; interpreter {sys.executable} "
        f"{sys.version.split()[0]}")
    for name in ("extract_stage1b.py", "report_stage1b.py", "test_analysis_stage1b.py"):
        log(f"# {name} SHA-256 {ex.sha256_file(HERE / name)}")
    log(f"# work folder {work} (synthetic folders and dry-run outputs; read-only on every input)")
    cells = ex.load_cells()
    part_a1()
    part_a2()
    part_a3()
    part_a4(cells)
    part_a5(work)
    part_a6(work)
    part_a7(cells)
    part_a8(work, cells)
    part_a9(work)
    part_b(work)
    log(f"\n# {COUNTS['pass'] + COUNTS['fail']} checks: {COUNTS['pass']} passed, {COUNTS['fail']} failed "
        f"({time.time() - started:.0f} s)")
    log("ALL CHECKS PASSED" if COUNTS["fail"] == 0 else "SOME CHECKS FAILED")
    LOG.close()
    return 0 if COUNTS["fail"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
