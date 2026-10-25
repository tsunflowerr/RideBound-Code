"""Break-even gate (BREAKEVEN-PLAN.md section 4): the re-run rows must equal the existing rows exactly on served, visRev60,
late60First, pDisp, breachLedger for every (cell, config, arm): alpha 35 against rows/plain-a1.ndjson, other alphas against
rows/n2-plain.ndjson. Other numeric metric fields present on both sides are compared for information. Also checks the
dispatch-only invariants written by be_run.js (netMismatch == 0, R60 == visRev60, La60 == late60First).
Usage: python -I be_gate.py <rows-be.ndjson> <plain-a1.ndjson> <n2-plain.ndjson> <out GATE.txt>
"""
import json
import sys

GATE = ("served", "visRev60", "late60First", "pDisp", "breachLedger")
SKIP = {"part", "set", "ms", "msScore", "scoreCalls", "scoreInstants", "scoreArcCalls", "scoreArcInstants", "scoreMode",
        "alphaS", "strideMs", "theta", "pi", "x", "be"}


def load(path):
    rows = {}
    for line in open(path, encoding="utf-8"):
        if line.strip():
            r = json.loads(line)
            key = (r["cell"], r["config"], r["arm"])
            if key in rows:
                raise SystemExit(f"duplicate key {key} in {path}")
            rows[key] = r
    return rows


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    new = load(sys.argv[1])
    old = {}
    for p in sys.argv[2:4]:
        for k, v in load(p).items():
            old.setdefault(k, v)
    expected = 60 * 2 * 21
    out = [f"rows re-run: {len(new)} (expected {expected})"]
    gate_bad, info_bad, compared, missing, inv_bad = [], {}, 0, [], []
    for key, r in sorted(new.items()):
        be = r["be"]
        if be["netMismatch"] != 0 or be["R60"] != r["visRev60"] or be["La60"] != r["late60First"]:
            inv_bad.append(key)
        o = old.get(key)
        if o is None:
            missing.append(key)
            continue
        compared += 1
        for f in GATE:
            if r[f] != o[f]:
                gate_bad.append((key, f, r[f], o[f]))
        for f, v in r.items():
            if f in SKIP or f in GATE or f not in o:
                continue
            if isinstance(v, (int, float)) and not isinstance(v, bool):
                if v != o[f]:
                    info_bad[f] = info_bad.get(f, 0) + 1
            elif isinstance(v, dict) and v != o[f]:
                info_bad[f] = info_bad.get(f, 0) + 1
    out.append(f"compared with existing rows: {compared}; re-run rows without an existing row: {len(missing)}")
    out.append(f"gate fields {GATE}: {len(gate_bad)} differences")
    for g in gate_bad[:20]:
        out.append(f"  DIFF {g}")
    out.append(f"other shared numeric/object fields with differences (information): {info_bad if info_bad else 'none'}")
    out.append(f"dispatch-only invariants (netMismatch 0, R60 == visRev60, La60 == late60First): {len(inv_bad)} rows fail")
    ok = len(new) == expected and compared == expected and not gate_bad and not inv_bad
    out.append("GATE " + ("PASS" if ok else "FAIL"))
    text = "\n".join(out) + "\n"
    with open(sys.argv[4], "x", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(text, end="")
    raise SystemExit(0 if ok else 1)


if __name__ == "__main__":
    main()
