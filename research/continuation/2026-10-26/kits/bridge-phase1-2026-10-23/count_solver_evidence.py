"""Count solver and candidate-cap evidence in every passed bridge job (read-only; R2 finding 5.3.9 of the company read).

Per job, over runner->adapter decision frames that carry solver.executionEvidence:
  decisions, final/primary solve status and detail codes, decisions with any vehicle whose candidate cap applied,
  feasible candidates omitted by the cap (sum), vehicles whose exploration work budget was exhausted,
  max unique feasible candidates before the cap, selection-level omitted candidates.
Usage: python -I count_solver_evidence.py <out.tsv> <phase folder> [<phase folder> ...]
"""
import base64
import collections
import json
import pathlib
import sys

COLS = ["phase", "job", "decisions", "finalStatus", "primaryStatus", "finalDetail", "decisionsCapApplied",
        "omittedByCap", "vehiclesWorkExhausted", "maxFeasibleBeforeCap", "selectionOmitted"]


def count(transcript: pathlib.Path) -> dict:
    n = cap_dec = omitted = exhausted = max_before = sel_omit = 0
    final, primary, detail = collections.Counter(), collections.Counter(), collections.Counter()
    with open(transcript, encoding="utf-8") as handle:
        for line in handle:
            if '"runnerToAdapter"' not in line:
                continue
            record = json.loads(line)
            if record.get("direction") != "runnerToAdapter":
                continue
            frame = json.loads(base64.b64decode(record["frameBase64"]))
            ev = ((frame.get("payload") or {}).get("solver") or {}).get("executionEvidence")
            if not ev:
                continue
            n += 1
            sel = ev.get("selection") or {}
            final[sel.get("finalSolveStatus")] += 1
            primary[sel.get("primarySolveStatus")] += 1
            detail[(sel.get("finalSolverDiagnostics") or {}).get("detailCode")] += 1
            sel_omit += sel.get("omittedCandidateCount") or 0
            any_cap = False
            for v in (ev.get("generation") or {}).get("vehicleLosses") or []:
                any_cap |= bool(v.get("candidateCapApplied"))
                omitted += v.get("omittedFeasibleCandidateCountByCap") or 0
                exhausted += bool(v.get("workBudgetExhausted"))
                max_before = max(max_before, v.get("uniqueFeasibleCandidateCountBeforeCap") or 0)
            cap_dec += any_cap
    fmt = lambda c: ";".join(f"{k}:{v}" for k, v in sorted(c.items(), key=lambda kv: str(kv[0])))
    return {"decisions": n, "finalStatus": fmt(final), "primaryStatus": fmt(primary), "finalDetail": fmt(detail),
            "decisionsCapApplied": cap_dec, "omittedByCap": omitted, "vehiclesWorkExhausted": exhausted,
            "maxFeasibleBeforeCap": max_before, "selectionOmitted": sel_omit}


def main() -> int:
    out = pathlib.Path(sys.argv[1])
    with open(out, "x", encoding="utf-8", newline="\n") as handle:
        handle.write("\t".join(COLS) + "\n")
        for phase_dir in map(pathlib.Path, sys.argv[2:]):
            for job in sorted(phase_dir.iterdir()):
                summary = job / "summary.json"
                if not summary.exists() or json.loads(summary.read_text(encoding="utf-8")).get("status") != "pass":
                    continue
                for transcript in sorted(job.glob("transcript-*.ndjson")):
                    row = {"phase": phase_dir.name, "job": job.name, **count(transcript)}
                    handle.write("\t".join(str(row[c]) for c in COLS) + "\n")
                    handle.flush()
    print("written", out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
