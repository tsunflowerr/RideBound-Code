"""Is the plain/adapter difference of the D1 split larger than day-to-day noise? (read only, prints)
Per day d: gap(d) = diff_adapter(d) - diff_plain(d) for each part (total, ruleC, forecast), and per arm
level_adapter(d) - level_plain(d); summarised with the fsan.js rules (synth_real_30d.summary)."""
import pathlib
import sys

FINAL = pathlib.Path(r"E:\Code\Report_INT3508\ridebound-scratchpad\fastrun-2026-11-02\final")
sys.path.insert(0, str(FINAL))
import d1_split as D  # noqa: E402
import synth_real_30d as S  # noqa: E402

timed = [r"E:\RideBoundData\research\fullsys-f14-v2\predictions-timed-attr-d1\timed-adapter.tsv.gz",
         r"E:\RideBoundData\research\fullsys-f16-v2\predictions-timed\timed-adapter.tsv.gz"]
plain = D.plain_cells()
adapter, _ = D.adapter_cells(timed, [])
groups = {"F14": set(S.PANELS["F14"]), "F16": set(S.PANELS["F16"])}
groups["F14+F16"] = groups["F14"] | groups["F16"]
for gname, days in groups.items():
    tp = {arm: S.day_table(plain, arm, days) for arm in D.ARMS}
    ta = {arm: S.day_table(adapter, arm, days) for arm in D.ARMS}
    common = sorted(set.intersection(*(set(t) for t in list(tp.values()) + list(ta.values()))))
    print(f"== {gname}: {len(common)} days")
    for mid, label, f in S.METRICS:
        for name, a, b in D.PARTS:
            g = [(f(ta[a][d]["day"]) - f(ta[b][d]["day"])) - (f(tp[a][d]["day"]) - f(tp[b][d]["day"])) for d in common]
            s = S.summary(g)
            print(f"  {mid:12s} {name:9s} adapter-plain {S.fmt(s['mean'])} [{S.fmt(s['lo'])}; {S.fmt(s['hi'])}] "
                  f"({s['nPlus']}/{s['nMinus']}) p={s.get('signP', s.get('p'))}")
        for arm in D.ARMS:
            g = [f(ta[arm][d]["day"]) - f(tp[arm][d]["day"]) for d in common]
            s = S.summary(g)
            print(f"  {mid:12s} level {arm:6s} adapter-plain {S.fmt(s['mean'])} [{S.fmt(s['lo'])}; {S.fmt(s['hi'])}] "
                  f"({s['nPlus']}/{s['nMinus']})")
