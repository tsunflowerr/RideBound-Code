"""D1 (PLAN-F16.md step 6, 2026-11-05): the plain/adapter gap of the emulator, split into rule C and forecast, on the
four G2 arms. Reads only; writes <out>.json and <out>.md (refuses to overwrite).

  ours    FS-G2-SHRC71-CL  (plain: G2 CL SHRC71)      total    = ours - elmi
  elmi    FS-G2-SHHZ71-N   (plain: G2 N  SHHZ71)      rule C   = ours - fenceF      (SHRC+forecast - SHHZ+forecast)
  fenceF  FS-G2-SHHZ71-CL  (plain: G2 CL SHHZ71)      forecast = fenceF - elmi      (SHHZ+forecast - SHHZ)
  ruleN   FS-G2-SHRC71-N   (plain: G2 N  SHRC71)      (the three are the OURS-VS-THEIRS split; ruleN is reported only)

plain    the company rows (wf20/fullsys/results/rows.ndjson).
adapter  the timed adapter predictions (all four arms; one or more timed-adapter.tsv[.gz] files): per served rider,
         p0 = first published value, late60First = A - p0 > 60 s (metrics_sh.js:33), rev60 = metrics_sh.hasReversal and
         tv5 (metrics_sh.js:58-59) on the published values (compare_drop_sequences.py's has_reversal and tv5). For the two
         sealed arms these must equal the sealed drop-sequence columns (rev60, tv5) rider by rider (--check files).
Day unit, statistics and metrics: synth_real_30d.py (fsan.js rules).
Usage: python -I -B -X utf8 d1_split.py --out <prefix> --timed <f> [--timed <f> ...] --check <drop-sequences.tsv> [...]
"""
import argparse
import collections
import csv
import gzip
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import synth_real_30d as S  # noqa: E402

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent / "fullsys"))
from compare_drop_sequences import has_reversal, tv5  # noqa: E402

ARMS = {"ours": ("FS-G2-SHRC71-CL", ("G2", "CL", "SHRC71")), "elmi": ("FS-G2-SHHZ71-N", ("G2", "N", "SHHZ71")),
        "fenceF": ("FS-G2-SHHZ71-CL", ("G2", "CL", "SHHZ71")), "ruleN": ("FS-G2-SHRC71-N", ("G2", "N", "SHRC71"))}
ROLE = {role: arm for arm, (role, _) in ARMS.items()}
PARTS = [("total", "ours", "elmi"), ("ruleC", "ours", "fenceF"), ("forecast", "fenceF", "elmi")]


def plain_cells():
    wanted = {spec: arm for arm, (_, spec) in ARMS.items()}
    out = {}
    with open(S.ROWS, encoding="utf-8") as handle:
        for line in handle:
            if not line.strip():
                continue
            r = json.loads(line)
            arm = wanted.get((r.get("config"), r.get("mode"), r.get("arm")))
            if arm is None:
                continue
            m = S.CO_CELL.match(r["cell"])
            key = (arm, (m[1], m[2], m[3]))
            if key in out:
                raise SystemExit(f"duplicate {key}")
            out[key] = {k: r[k] for k in S.COUNTS}
    return out


def adapter_cells(timed_files, check_files):
    sealed = {}
    for f in check_files:
        with open(f, encoding="utf-8", newline="") as handle:
            for row in csv.DictReader(handle, delimiter="\t"):
                if row["served"] == "1":
                    sealed[(row["role"], row["cell"], row["requestId"])] = (int(row["rev60"]), int(row["tv5"]))
    cells = collections.defaultdict(lambda: {"served": 0, "visRev60": 0, "late60First": 0, "sumTV5": 0})
    checked, seen = 0, set()
    for f in timed_files:
        opener = gzip.open if f.endswith(".gz") else open
        with opener(f, "rt", encoding="utf-8", newline="") as handle:
            for row in csv.DictReader(handle, delimiter="\t"):
                arm = ROLE.get(row["role"])
                key3 = (row["role"], row["cell"], row["requestId"])
                if arm is None:
                    continue
                if key3 in seen:
                    raise SystemExit(f"rider twice: {key3}")
                seen.add(key3)
                if row["served"] != "1":
                    continue
                values = [p for _, p in json.loads(row["timedDropSequence"])]
                rev, tv = int(has_reversal(values)), tv5(values)
                if key3 in sealed:
                    if sealed[key3] != (rev, tv):
                        raise SystemExit(f"{key3}: rev60/tv5 from the timed sequence differ from the sealed drop sequence")
                    checked += 1
                c = cells[(arm, S.our_key(row["cell"]))]
                c["served"] += 1
                c["visRev60"] += rev
                c["sumTV5"] += tv
                if int(row["A"]) - values[0] > 60_000:
                    c["late60First"] += 1
    if checked != len(sealed):
        raise SystemExit(f"checked {checked} of {len(sealed)} sealed riders")
    return dict(cells), checked


def split(cells, days):
    tables = {arm: S.day_table(cells, arm, days) for arm in ARMS}
    common = sorted(set.intersection(*(set(t) for t in tables.values())))
    out = {"days": len(common)}
    for part in ("day", "w08", "w17"):
        rows = {}
        for mid, _, f in S.METRICS:
            level = {arm: sum(f(tables[arm][d][part]) for d in common) / len(common) for arm in ARMS}
            diffs = {name: S.summary([f(tables[a][d][part]) - f(tables[b][d][part]) for d in common]) for name, a, b in PARTS}
            rows[mid] = {"level": level, "diff": diffs}
        out[part] = rows
    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", required=True)
    parser.add_argument("--timed", action="append", required=True)
    parser.add_argument("--check", action="append", default=[])
    a = parser.parse_args()
    plain = plain_cells()
    adapter, checked = adapter_cells(a.timed, a.check)
    panels = {"F14": set(S.PANELS["F14"]), "F16": set(S.PANELS["F16"])}
    groups = [(n, d) for n, d in panels.items()] + [("F14+F16", panels["F14"] | panels["F16"])]
    report = {"inputs": {"timed": a.timed, "check": a.check, "sealedRidersChecked": checked}, "results": {}}
    lines = ["# D1: bộ mô phỏng plain so với adapter, tách luật C và dự báo (G2)", "",
             f"Kiểm: rev60/tv5 tính từ chuỗi có giờ công bố trùng tệp chuỗi đã khóa ở {checked} khách của hai nhánh chính.",
             "Tổng = SHRC+dự báo − SHHZ; luật C = SHRC+dự báo − SHHZ+dự báo; dự báo = SHHZ+dự báo − SHHZ. Theo ngày, quy tắc fsan.js.", ""]
    for gname, days in groups:
        res = {}
        for sname, cells in (("plain", plain), ("adapter", adapter)):
            arms_ok = all(sum(1 for (arm, k) in cells if arm == x and k[0] in days) == 6 * len(days) for x in ARMS)
            if arms_ok:
                res[sname] = split(cells, days)
        report["results"][gname] = res
        if not res:
            continue
        lines += [f"## {gname}", ""]
        for part in ("day", "w08", "w17"):
            lines += [f"### {'Cả ngày' if part == 'day' else 'Khung ' + part}", "",
                      "| Chỉ số | Nguồn | Ngày | Tổng | Luật C | Dự báo | Tổng [KTC 95%] (+/−) |", "|---|---|---|---|---|---|---|"]
            for mid, label, _ in S.METRICS:
                for sname, r in res.items():
                    d = r[part][mid]["diff"]
                    t = d["total"]
                    lines.append(f"| {label} | {sname} | {r['days']} | {S.fmt(t['mean'])} | {S.fmt(d['ruleC']['mean'])} | "
                                 f"{S.fmt(d['forecast']['mean'])} | [{S.fmt(t['lo'])}; {S.fmt(t['hi'])}] ({t['nPlus']}/{t['nMinus']}) |")
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
