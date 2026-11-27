"""A6: tap khach chung tren HE THONG DAY DU (30 ngay), tu chuoi gio bao da luu (khong chay moi).

Doc publication-projection-f14/f16.jsonl.gz (moi khach hoan tat cua hai cau hinh FS-G2-SHHZ71-N = doi chung va FS-G2-SHRC71-CL =
ket hop: gio tra thuc te A va moi gio bao). Tinh lai tung khach cung dinh nghia nhu metrics_sh.js / recompute_eta.py:
late60 (A - p0 > 60 s), early60 (p0 - A > 60 s), rev60 (doi qua lai, nguong 60 s), tv5 (giay gio bao bi doi, nguong 5 s),
sai so gio bao dau |A - p0| va sai so theo thoi gian (trong so thoi gian hieu luc), ca co dau.

Cong kiem truoc khi dung: gop theo ngay nhu aggregate.py roi so voi Bang 5.8 (phuc vu 703,1/696,4; doi qua lai 45,8/18,5;
muon 245,2/211,0; giay 230,4/192,7) va Bang 5.10 (so khach 63.277/62.678; sai so dau 3,05/2,88; theo thoi gian 0,68/0,60).
Ket qua: full_common.json.
"""
import collections
import gzip
import json
import math
import re
import sys
import os
from pathlib import Path
from statistics import mean, stdev

sys.stdout.reconfigure(encoding='utf-8')
HERE = Path(__file__).resolve().parent
SRC = Path(os.environ['RB_EVIDENCE'])   # publication-projection-f14/f16.jsonl.gz of the RideBound runs (not shipped)
PAT = re.compile(r'^FS-G2-(SHHZ71-N|SHRC71-CL)-d(\d{4})(\d{2})(\d{2})-vf-r([012])-(w08|w17)$')
T975_29 = 2.0452296
ROLE = {'SHHZ71-N': 'ctl', 'SHRC71-CL': 'full'}


def rider_metrics(pp, a):
    pp = [p for p in pp if p[0] < a]
    t0, p0 = pp[0]
    total = a - t0
    area = area_s = 0.0
    for i, (t, e) in enumerate(pp):
        end = pp[i + 1][0] if i + 1 < len(pp) else a
        area += abs(a - e) * (end - t)
        area_s += (a - e) * (end - t)
    # published series: z = change of the published drop time; same rules as metrics_sh.js
    last = 0
    rev = False
    shown = p0
    tv5 = 0
    prev = p0
    for t, e in pp[1:]:
        z = e - prev
        prev = e
        if z != 0 and abs(z) >= 60000:
            s = 1 if z > 0 else -1
            if last != 0 and s != last:
                rev = True
            last = s
        if abs(e - shown) >= 5000:
            tv5 += abs(e - shown)
            shown = e
    return dict(late=int(a - p0 > 60000), early=int(p0 - a > 60000), rev=int(rev), tv5=tv5 / 1000.0,
                init_abs=abs(a - p0) / 60000.0, init_s=(a - p0) / 60000.0,
                timed_abs=area / total / 60000.0, timed_s=area_s / total / 60000.0)


def load():
    riders = {'ctl': {}, 'full': {}}
    for group in ['f14', 'f16']:
        with gzip.open(SRC / f'publication-projection-{group}.jsonl.gz', 'rt', encoding='utf-8') as f:
            for line in f:
                r = json.loads(line)
                m = PAT.fullmatch(r['j'])
                role, y, mo, d, smp, win = m.groups()
                if 'p' in r:
                    pp = [tuple(x) for x in r['p']]
                else:
                    t, e = r['t'], r['e']
                    pp = [(t, e)]
                    for dt, de in zip(r['d'][::2], r['d'][1::2]):
                        t += dt
                        e += de
                        pp.append((t, e))
                key = (f'{y}-{mo}-{d}', smp, win, r['r'])
                riders[ROLE[role]][key] = rider_metrics(pp, r['A'])
        print(group, 'done', {k: len(v) for k, v in riders.items()}, flush=True)
    return riders


def per_day(rs, keys, win=None):
    """day -> aggregated metrics over the riders in `keys` (a set) of one config; samples = 3 per window"""
    acc = collections.defaultdict(lambda: collections.defaultdict(float))
    for k in keys:
        day, smp, w, _ = k
        if win and w != win:
            continue
        x = rs[k]
        a = acc[day]
        a['n'] += 1
        for f in ['late', 'early', 'rev', 'tv5', 'init_abs', 'init_s', 'timed_abs', 'timed_s']:
            a[f] += x[f]
    out = {}
    for day, a in acc.items():
        n = a['n']
        out[day] = {'served': n / 3.0, 'rev': a['rev'] / 3.0, 'late': 1000 * a['late'] / n, 'early': 1000 * a['early'] / n,
                    'tv5': a['tv5'] / n, 'init_abs': a['init_abs'] / n, 'timed_abs': a['timed_abs'] / n,
                    'init_s': a['init_s'] / n, 'timed_s': a['timed_s'] / n}
    return out


def avg(d, k):
    return mean(v[k] for v in d.values())


def paired(a, b, k):
    days = sorted(a)
    assert days == sorted(b) and len(days) == 30
    v = [a[x][k] - b[x][k] for x in days]
    m = mean(v)
    hw = T975_29 * stdev(v) / math.sqrt(len(v))
    return {'mean': m, 'lo': m - hw, 'hi': m + hw, 'up': sum(x > 0 for x in v), 'down': sum(x < 0 for x in v)}


def main():
    R = load()
    ctl, full = R['ctl'], R['full']
    allc, allf = set(ctl), set(full)
    common = allc & allf
    only_c, only_f = allc - allf, allf - allc
    res = {'n': {'ctl': len(allc), 'full': len(allf), 'common': len(common), 'only_ctl': len(only_c), 'only_full': len(only_f)}}
    print(res['n'])
    K = ['served', 'rev', 'late', 'early', 'tv5', 'init_abs', 'timed_abs', 'init_s', 'timed_s']
    pooled = {}
    for name, rs, keys in [('ctl_all', ctl, allc), ('full_all', full, allf), ('ctl_common', ctl, common), ('full_common', full, common),
                           ('ctl_only', ctl, only_c), ('full_only', full, only_f)]:
        n = len(keys)
        pooled[name] = {'n': n, **{f: sum(rs[k][f] for k in keys) / n for f in ['late', 'early', 'rev', 'tv5', 'init_abs', 'timed_abs', 'init_s', 'timed_s']}}
    res['pooled'] = pooled
    for k, v in pooled.items():
        print(k, {a: round(b, 4) for a, b in v.items()})
    # one window, pooled over its riders: the morning and afternoon levels of the thesis (first-promise and time-weighted error)
    res['pooled_win'] = {}
    for w in ['w08', 'w17']:
        res['pooled_win'][w] = {}
        for name, rs, keys in [('ctl_all', ctl, allc), ('full_all', full, allf), ('ctl_common', ctl, common), ('full_common', full, common)]:
            ks = [k for k in keys if k[2] == w]
            res['pooled_win'][w][name] = {'n': len(ks), **{f: sum(rs[k][f] for k in ks) / len(ks) for f in ['init_abs', 'timed_abs', 'init_s', 'timed_s']}}
        print(w, 'pooled', {name: (v['n'], round(v['init_abs'], 3), round(v['timed_abs'], 3)) for name, v in res['pooled_win'][w].items()})
    res['levels'], res['diff'] = {}, {}
    for scope in [None, 'w08', 'w17']:
        sc = scope or 'day'
        dc_all, df_all = per_day(ctl, allc, scope), per_day(full, allf, scope)
        dc_com, df_com = per_day(ctl, common, scope), per_day(full, common, scope)
        res['levels'][sc] = {'ctl_all': {k: avg(dc_all, k) for k in K}, 'full_all': {k: avg(df_all, k) for k in K},
                             'ctl_common': {k: avg(dc_com, k) for k in K}, 'full_common': {k: avg(df_com, k) for k in K}}
        res['diff'][sc] = {'all': {k: paired(df_all, dc_all, k) for k in K}, 'common': {k: paired(df_com, dc_com, k) for k in K}}
        print(sc, 'ALL    ', {k: (round(v['mean'], 2), round(v['lo'], 2), round(v['hi'], 2)) for k, v in res['diff'][sc]['all'].items()})
        print(sc, 'COMMON ', {k: (round(v['mean'], 2), round(v['lo'], 2), round(v['hi'], 2)) for k, v in res['diff'][sc]['common'].items()})
    (HERE / 'full_common.json').write_text(json.dumps(res, ensure_ascii=False, indent=1), encoding='utf-8')
    print('written full_common.json')


if __name__ == '__main__':
    main()
