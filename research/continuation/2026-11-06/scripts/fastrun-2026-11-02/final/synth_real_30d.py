"""F14 + F16 synthesis (2026-11-05, PLAN-F16.md step 6): the same day-level contrast as the company emulator's
ours_vs_theirs.js / fsan.js, for the two arms that ran on the real system, from three sources:

  plain    the company emulator's own rows (wf20/fullsys/results/rows.ndjson; G2 CL SHRC71 = ours, G2 N SHHZ71 = Elmi)
  adapter  the sealed adapter-mode predictions (rider rows; visRev60 and TV5 from the comparison's emulator side)
  real     the RideBound runs (riders-real-riders.tsv; visRev60 and TV5 from the comparison's real side)

Rules copied from fsan.js (header and dayTable): a day = sum over the windows w08, w17 of the mean over the resamples r0, r1,
r2; a day counts only if both arms have all 6 cells; rates per 1000 served = 1000 * X_d / served_d; TV5 = sumTV5 / 1000 /
served_d (s per served rider); paired difference by day (ours - Elmi), mean, SE, 95 % t-interval, median, exact two-sided
sign test (ties dropped). late60First = served and A - p0 > 60 s (metrics_sh.js:33, 48). visRev60 and TV5 per cell are
compare_drop_sequences.py's (metrics_sh.hasReversal, metrics_sh.js:58-59). Reads only; writes <out>.json and <out>.md.

Usage: python -I -B synth_real_30d.py --out <prefix>
         --panel F14 <predictions.tsv> <drop-sequences.tsv> <report/sequences.json | -> <report/riders-real-riders.tsv | ->
         [--panel F16 ...]   ('-': no real runs yet; the adapter side then comes from the sealed files alone)
"""
import argparse
import collections
import csv
import json
import math
import pathlib
import re

SP = pathlib.Path("E:/RideBoundData/company-intake/2026-10-23/sp")
ROWS = SP / "wf20/fullsys/results/rows.ndjson"
PANELS = json.loads((SP / "wf17/r1-identity-fence-pad/work/panels.json").read_text(encoding="utf-8"))
ARMS = {"ours": ("FS-G2-SHRC71-CL", ("G2", "CL", "SHRC71")), "elmi": ("FS-G2-SHHZ71-N", ("G2", "N", "SHHZ71"))}
ROLE_TO_ARM = {role: arm for arm, (role, _) in ARMS.items()}
COUNTS = ("served", "visRev60", "late60First", "sumTV5")
OUR_CELL = re.compile(r"^d(\d{4})(\d{2})(\d{2})-vf-(r\d)-(w\d\d)$")
CO_CELL = re.compile(r"^(\d{4}-\d{2}-\d{2})-(w\d\d)-(r\d)$")


# ------------------------------------------------------------------ statistics (fsan.js lines 16-38, same arithmetic)
def lgamma(x):
    c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2,
         -0.5395239384953e-5]
    y, t = x, x + 5.5
    t -= (x + 0.5) * math.log(t)
    s = 1.000000000190015
    for v in c:
        y += 1
        s += v / y
    return -t + math.log(2.5066282746310005 * s / x)


def betacf(a, b, x):
    qab, qap, qam, c, d = a + b, a + 1, a - 1, 1.0, 1 - (a + b) * x / (a + 1)
    if abs(d) < 1e-300:
        d = 1e-300
    d = 1 / d
    h = d
    for m in range(1, 301):
        m2 = 2 * m
        aa = m * (b - m) * x / ((qam + m2) * (a + m2))
        d = 1 + aa * d
        d = 1e-300 if abs(d) < 1e-300 else d
        c = 1 + aa / c
        c = 1e-300 if abs(c) < 1e-300 else c
        d = 1 / d
        h *= d * c
        aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
        d = 1 + aa * d
        d = 1e-300 if abs(d) < 1e-300 else d
        c = 1 + aa / c
        c = 1e-300 if abs(c) < 1e-300 else c
        d = 1 / d
        delta = d * c
        h *= delta
        if abs(delta - 1) < 3e-14:
            break
    return h


def ibeta(a, b, x):
    if x <= 0:
        return 0.0
    if x >= 1:
        return 1.0
    bt = math.exp(lgamma(a + b) - lgamma(a) - lgamma(b) + a * math.log(x) + b * math.log(1 - x))
    return bt * betacf(a, b, x) / a if x < (a + 1) / (a + b + 2) else 1 - bt * betacf(b, a, 1 - x) / b


def tcdf(t, df):
    p = 0.5 * ibeta(df / 2, 0.5, df / (df + t * t))
    return 1 - p if t > 0 else p


def tq975(df):
    lo, hi = 0.0, 100.0
    for _ in range(200):
        m = (lo + hi) / 2
        if tcdf(m, df) < 0.975:
            lo = m
        else:
            hi = m
    return (lo + hi) / 2


def sign_p(n_plus, n_minus):
    n = n_plus + n_minus
    if not n:
        return 1.0
    k, lc, s = min(n_plus, n_minus), 0.0, 0.0
    for i in range(k + 1):
        if i > 0:
            lc += math.log(n - i + 1) - math.log(i)
        s += math.exp(lc - n * math.log(2))
    return min(1.0, 2 * s)


def summary(d):
    n = len(d)
    if not n:
        return None
    mean = sum(d) / n
    sd = math.sqrt(sum((x - mean) ** 2 for x in d) / (n - 1)) if n > 1 else float("nan")
    se = sd / math.sqrt(n)
    t = tq975(n - 1) if n > 1 else float("nan")
    s = sorted(d)
    med = s[(n - 1) // 2] if n % 2 else (s[n // 2 - 1] + s[n // 2]) / 2
    eps = 1e-9
    n_plus, n_minus = sum(x > eps for x in d), sum(x < -eps for x in d)
    return {"n": n, "mean": mean, "se": se, "lo": mean - t * se, "hi": mean + t * se, "median": med,
            "nPlus": n_plus, "nMinus": n_minus, "ties": n - n_plus - n_minus, "p": sign_p(n_plus, n_minus)}


# ------------------------------------------------------------------ per-cell counts
def our_key(cell):
    m = OUR_CELL.match(cell)
    if not m:
        raise SystemExit(f"cell name {cell}")
    return (f"{m[1]}-{m[2]}-{m[3]}", m[5], m[4])


def plain_cells():
    out = {}
    wanted = {spec: arm for arm, (_, spec) in ARMS.items()}
    with open(ROWS, encoding="utf-8") as handle:
        for line in handle:
            if not line.strip():
                continue
            r = json.loads(line)
            arm = wanted.get((r.get("config"), r.get("mode"), r.get("arm")))
            if arm is None:
                continue
            m = CO_CELL.match(r["cell"])
            key = (arm, (m[1], m[2], m[3]))
            if key in out:
                raise SystemExit(f"duplicate plain row {key}")
            out[key] = {k: r[k] for k in COUNTS}
    return out


def rider_cells(tsv, side):
    out = collections.defaultdict(lambda: {"served": 0, "late60First": 0})
    with open(tsv, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            arm = ROLE_TO_ARM.get(row["role"])
            if arm is None:
                continue
            c = out[(arm, our_key(row["cell"]))]
            if row["served"] == "1":
                c["served"] += 1
                if int(row["A"]) - int(row["p0"]) > 60_000:
                    c["late60First"] += 1
    return out


def add_drop_sequences(cells, tsv):
    """Adapter side: per-rider rev60 and tv5 columns of the sealed drop-sequence file (served riders)."""
    seen = collections.Counter()
    for c in cells.values():
        c["visRev60"] = c["sumTV5"] = 0
    with open(tsv, encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle, delimiter="\t"):
            arm = ROLE_TO_ARM.get(row["role"])
            if arm is None or row["served"] != "1":
                continue
            key = (arm, our_key(row["cell"]))
            cells[key]["visRev60"] += int(row["rev60"])
            cells[key]["sumTV5"] += int(row["tv5"])
            seen[key] += 1
    for key, c in cells.items():
        if seen[key] != c["served"]:
            raise SystemExit(f"adapter {key}: {seen[key]} served sequences, {c['served']} served rider rows")


def add_sequences(cells, sequences_json, adapter):
    """Real side: compare_drop_sequences.py's per-cell visRev60Real and tv5Real. Its emulator side must equal the adapter
    values computed from the sealed file (a check that both read the same predictions)."""
    data = json.loads(pathlib.Path(sequences_json).read_text(encoding="utf-8"))
    seen = set()
    for r in data["results"]:
        arm = ROLE_TO_ARM.get(r["role"])
        if arm is None:
            continue
        key = (arm, our_key(r["cell"]))
        if key not in cells:
            raise SystemExit(f"real: sequences for {key} but no rider rows")
        if (r["visRev60Emulator"], r["tv5Emulator"]) != (adapter[key]["visRev60"], adapter[key]["sumTV5"]):
            raise SystemExit(f"{key}: sequences.json emulator side differs from the sealed drop sequences")
        cells[key]["visRev60"] = r["visRev60Real"]
        cells[key]["sumTV5"] = r["tv5Real"]
        seen.add(key)
    missing = set(cells) - seen
    if missing:
        raise SystemExit(f"real: no sequences for {sorted(missing)[:3]} ...")


# ------------------------------------------------------------------ day table and contrasts (fsan.js dayTable)
def day_table(cells, arm, days):
    by = collections.defaultdict(list)
    for (a, (day, win, rs)), v in cells.items():
        if a == arm and day in days:
            by[day].append((win, rs, v))
    out = {}
    for day, cs in by.items():
        windows = {}
        for w in ("w08", "w17"):
            vals = [v for win, rs, v in cs if win == w]
            if sorted(rs for win, rs, _ in cs if win == w) != ["r0", "r1", "r2"]:
                break
            windows[w] = {k: sum(v[k] for v in vals) / 3 for k in COUNTS}
        else:
            windows["day"] = {k: windows["w08"][k] + windows["w17"][k] for k in COUNTS}
            out[day] = windows
    return out


METRICS = [
    ("served", "Phục vụ (khách/ngày)", lambda x: x["served"]),
    ("visRev60", "Đảo hứa (khách/ngày)", lambda x: x["visRev60"]),
    ("late60First/1000", "Trễ >60 s so với hứa đầu /1000", lambda x: 1000 * x["late60First"] / x["served"]),
    ("TV5", "TV5 (giây/khách)", lambda x: x["sumTV5"] / 1000 / x["served"]),
]


def contrast(cells, days):
    ours, elmi = day_table(cells, "ours", days), day_table(cells, "elmi", days)
    common = sorted(set(ours) & set(elmi))
    result = {"days": len(common), "excluded": sorted((set(ours) ^ set(elmi)) | (set(days) - set(ours) - set(elmi)))}
    for part in ("day", "w08", "w17"):
        rows = {}
        for mid, _, f in METRICS:
            a = [f(ours[d][part]) for d in common]
            b = [f(elmi[d][part]) for d in common]
            rows[mid] = {"ours": sum(a) / len(a) if a else None, "elmi": sum(b) / len(b) if b else None,
                         "diff": summary([x - y for x, y in zip(a, b)])}
        result[part] = rows
    return result


def fmt(x, nd=2):
    return "—" if x is None else f"{x:.{nd}f}".replace(".", ",")


def fmt_p(p):
    if p is None:
        return "—"
    if p < 1e-3:
        m, e = f"{p:.0e}".split("e")
        return f"{m}e{int(e)}"
    return f"{p:.3f}".replace(".", ",")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--panel", nargs=5, action="append", default=[],
                        metavar=("NAME", "PREDICTIONS", "DROP_SEQUENCES", "SEQUENCES_JSON", "RIDERS_REAL"))
    a = parser.parse_args()
    plain = plain_cells()
    adapter, real, panel_days = {}, {}, {}
    real_panels = []
    for name, predictions, drop_sequences, sequences, riders_real in a.panel:
        panel_days[name] = set(PANELS[name])
        part_adapter = rider_cells(predictions, "adapter")
        add_drop_sequences(part_adapter, drop_sequences)
        if len(part_adapter) != 2 * 3 * 2 * len(panel_days[name]):
            raise SystemExit(f"{name}: {len(part_adapter)} adapter cell-arms")
        adapter.update(part_adapter)
        if riders_real != "-":
            part = rider_cells(riders_real, "real")
            if set(part) != set(part_adapter):
                raise SystemExit(f"{name}: real cell-arms differ from the predictions")
            add_sequences(part, sequences, part_adapter)
            real.update(part)
            real_panels.append(name)
    report = {"inputs": {"rows": str(ROWS), "panels": a.panel}, "results": {}}
    groups = [(n, panel_days[n]) for n in panel_days]
    if len(panel_days) > 1:
        groups.append(("+".join(panel_days), set().union(*panel_days.values())))
    lines = ["# Ta (FS-G2-SHRC71-CL) − Elmi (FS-G2-SHHZ71-N): bộ mô phỏng plain, dự đoán adapter, hệ thật", "",
             "Hiệu = của ta − của Elmi, theo ngày; [khoảng tin cậy 95% theo t]; (ngày +/−; p kiểm dấu). Quy tắc của fsan.js.", ""]
    for gname, days in groups:
        sources = {"plain": plain, "adapter": adapter}
        if all(n in real_panels for n in gname.split("+")):
            sources["real"] = real
        report["results"][gname] = {s: contrast(c, days) for s, c in sources.items()}
        lines += [f"## {gname}", ""]
        for part in ("day", "w08", "w17"):
            lines += [f"### {'Cả ngày' if part == 'day' else 'Khung ' + part}", "",
                      "| Chỉ số | Nguồn | Ngày | Ta | Elmi | Hiệu | Khoảng tin cậy | +/− | p |", "|---|---|---|---|---|---|---|---|---|"]
            for mid, label, _ in METRICS:
                for s in sources:
                    r = report["results"][gname][s]
                    m = r[part][mid]
                    d = m["diff"]
                    lines.append(f"| {label} | {s} | {r['days']} | {fmt(m['ours'])} | {fmt(m['elmi'])} | {fmt(d and d['mean'])} | "
                                 f"[{fmt(d and d['lo'])}; {fmt(d and d['hi'])}] | {d and d['nPlus']}/{d and d['nMinus']} | "
                                 f"{fmt_p(d and d['p'])} |")
            lines.append("")
    out = pathlib.Path(a.out)
    with open(out.with_suffix(".json"), "x", encoding="utf-8", newline="\n") as handle:
        json.dump(report, handle, indent=1, ensure_ascii=False)
    with open(out.with_suffix(".md"), "x", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
