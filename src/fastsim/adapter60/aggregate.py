"""Tổng hợp kết quả mô phỏng nhanh khớp FleetPy cho Chương 5 (2026-11-08).

Quy tắc giống ch5_numbers.py / recompute.py: mỗi ngày gộp các dòng của ngày đó (2 khung x số mẫu); khách/ngày = tổng / số mẫu;
tỷ lệ gộp tử số và số khách hoàn tất trong ngày rồi mới chia; trung bình trên các ngày; hiệu ghép cặp theo ngày; KTC 95% theo t.
Chỉ dùng thư viện chuẩn.
"""
import collections
import json
import math
import sys
import os
from pathlib import Path
from statistics import mean, stdev

sys.stdout.reconfigure(encoding='utf-8')
R = Path(os.environ.get('RB_RESULTS') or Path(__file__).resolve().parent.parent / 'results')
OUT = Path(__file__).resolve().parent / 'aggregate.json'
T975 = {9: 2.2621572, 13: 2.1603687, 15: 2.1314495, 29: 2.0452296, 59: 2.0009957}


def load(name):
    rows = []
    for p in sorted((R / name).glob('rows-*.ndjson')):
        rows += [json.loads(l) for l in p.read_text(encoding='utf-8').splitlines() if l.strip()]
    return rows


def per_day(rows, config, arm, mode='N', samples=3, win=None):
    g = collections.defaultdict(list)
    for r in rows:
        if r['config'] == config and r['arm'] == arm and r['mode'] == mode and (win is None or r['win'] == win):
            g[r['day']].append(r)
    want = samples * (1 if win else 2)
    out = {}
    for day, rr in g.items():
        assert len(rr) == want, (config, arm, mode, day, len(rr))
        su = lambda k: sum(r[k] for r in rr)
        sv = su('served')
        out[day] = {'served': sv / samples, 'rev': su('visRev60') / samples, 'late': 1000 * su('late60First') / sv,
                    'early': 1000 * su('early60First') / sv, 'tv5': su('sumTV5') / sv / 1000,
                    'lead': su('sumLead') / su('nLead') / 1000, 'deadline_pct': 100 * su('breachDeadline60') / sv,
                    'breachLedger': su('breachLedger'), 'servedTotal': sv}
    return out


def avg(d, k):
    return mean(x[k] for x in d.values())


def paired(a, b, k):
    assert sorted(a) == sorted(b)
    v = [a[d][k] - b[d][k] for d in sorted(a)]
    m = mean(v)
    hw = T975[len(v) - 1] * stdev(v) / math.sqrt(len(v))
    return {'mean': m, 'lo': m - hw, 'hi': m + hw, 'up': sum(x > 0 for x in v), 'down': sum(x < 0 for x in v), 'n': len(v)}


res = {}
K = ['served', 'rev', 'late', 'tv5']

# ---------------- A: 60 ngày
A = load('main60')
if A:
    res['A_rows'] = len(A)
    lv = {}
    for cfg in ['INS', 'G2']:
        for arm in ['U', 'EO35', 'EOHZ35', 'RCE35']:
            d = per_day(A, cfg, arm)
            assert len(d) == 60, (cfg, arm, len(d))
            lv[f'{cfg}/{arm}'] = {k: avg(d, k) for k in K + ['deadline_pct']}
            print(f'{cfg:3} {arm:7}', ' '.join(f'{k}={lv[f"{cfg}/{arm}"][k]:.2f}' for k in K + ['deadline_pct']))
    res['A_levels'] = lv
    diffs = {}
    for label, cfg, a_arm, b_arm in [('INS', 'INS', 'RCE35', 'EOHZ35'), ('G2', 'G2', 'RCE35', 'EOHZ35'), ('G2+Shadow', 'G2', 'SHRC71', 'SHHZ71')]:
        a, b = per_day(A, cfg, a_arm), per_day(A, cfg, b_arm)
        diffs[label] = {k: paired(a, b, k) for k in K + ['deadline_pct']}
        diffs[label]['levels'] = {'a': {k: avg(a, k) for k in K}, 'b': {k: avg(b, k) for k in K}}
        print(label, {k: (round(v['mean'], 2), round(v['lo'], 2), round(v['hi'], 2), v['up'], v['down']) for k, v in diffs[label].items() if k != 'levels'})
        print('   mức:', {s: {k: round(x, 2) for k, x in v.items()} for s, v in diffs[label]['levels'].items()})
    res['A_diffs'] = diffs
    for cfg in ['INS', 'G2']:
        rr = [r for r in A if r['config'] == cfg and r['arm'] == 'RCE35']
        print(f'{cfg} RCE35: vượt ngân sách {sum(r["breachLedger"] for r in rr)} trên {sum(r["served"] for r in rr)} khách hoàn tất')
        res[f'A_breach_{cfg}'] = [sum(r['breachLedger'] for r in rr), sum(r['served'] for r in rr)]
    shkeys = sorted(k for k in A[0] if k.startswith(('G_', 'A_')))
    if shkeys:
        for arm in ['SHHZ71', 'SHRC71']:
            rr = [r for r in A if r['config'] == 'G2' and r['arm'] == arm]
            tot = {k: sum(r.get(k, 0) or 0 for r in rr) for k in shkeys if isinstance(rr[0].get(k), (int, float))}
            res[f'A_shadow_groups_{arm}'] = tot
            print(arm, 'nhóm Shadow:', tot)

# ---------------- B: dự báo 30 ngày
B = load('fc30')
if B:
    res['B_rows'] = len(B)
    fc = {}
    for mode in ['N', 'CL', 'CLc']:
        d = per_day(B, 'G2', 'SHRC71', mode)
        assert len(d) == 30, (mode, len(d))
        e = {'served': avg(d, 'served'), 'late': avg(d, 'late')}
        for w in ['w08', 'w17']:
            dw = per_day(B, 'G2', 'SHRC71', mode, win=w)
            e['late_' + w] = avg(dw, 'late'); e['early_' + w] = avg(dw, 'early'); e['lead_' + w] = avg(dw, 'lead')
        fc[mode] = e
        print('dự báo', mode, {k: round(v, 2) for k, v in e.items()})
    for a, b in [('CL', 'N'), ('CLc', 'N'), ('CLc', 'CL')]:
        da, db = per_day(B, 'G2', 'SHRC71', a), per_day(B, 'G2', 'SHRC71', b)
        fc[f'{a}-{b}'] = {k: paired(da, db, k) for k in ['served', 'late']}
        for w in ['w08', 'w17']:
            fc[f'{a}-{b}'][f'late_{w}'] = paired(per_day(B, 'G2', 'SHRC71', a, win=w), per_day(B, 'G2', 'SHRC71', b, win=w), 'late')
        print(f'{a}-{b}', {k: (round(v['mean'], 2), round(v['lo'], 2), round(v['hi'], 2)) for k, v in fc[f'{a}-{b}'].items()})
    res['B'] = fc
    cal = Path(__file__).resolve().parent / 'calib-result.json'
    if cal.exists():
        cr = json.loads(cal.read_text(encoding='utf-8'))
        P = load(cr['final'])
        d = per_day(P, 'G2', 'SHRC71', 'PAD')
        assert len(d) == 30
        e = {'served': avg(d, 'served'), 'late': avg(d, 'late'), 'converged': cr['converged'], 'run': cr['final'],
             'flat': cr['iterations'][-1]['flat'], 'gap_s': cr['iterations'][-1]['gap']}
        for w in ['w08', 'w17']:
            dw = per_day(P, 'G2', 'SHRC71', 'PAD', win=w)
            e['late_' + w] = avg(dw, 'late'); e['early_' + w] = avg(dw, 'early'); e['lead_' + w] = avg(dw, 'lead')
        fc['PAD'] = e
        both = B + P
        for a, b in [('CL', 'PAD'), ('PAD', 'N')]:
            da, db = per_day(both, 'G2', 'SHRC71', a), per_day(both, 'G2', 'SHRC71', b)
            fc[f'{a}-{b}'] = {k: paired(da, db, k) for k in ['served', 'late']}
            for w in ['w08', 'w17']:
                fc[f'{a}-{b}'][f'late_{w}'] = paired(per_day(both, 'G2', 'SHRC71', a, win=w), per_day(both, 'G2', 'SHRC71', b, win=w), 'late')
            print(f'{a}-{b}', {k: (round(v['mean'], 2), round(v['lo'], 2), round(v['hi'], 2)) for k, v in fc[f'{a}-{b}'].items()})
        print('dự báo PAD', {k: (round(v, 2) if isinstance(v, float) else v) for k, v in e.items()})

# ---------------- D: phạt đổi xe 300 s, 60 ngày, r0
PEN = load('pen300')
if PEN and A:
    r0 = [r for r in A if r['rs'] == 'r0']
    arms = {'HZ0': per_day(r0, 'G2', 'EOHZ35', samples=1), 'C0': per_day(r0, 'G2', 'RCE35', samples=1),
            'HZ300': per_day(PEN, 'G2', 'EOHZ35', samples=1), 'C300': per_day(PEN, 'G2', 'RCE35', samples=1)}
    assert all(len(v) == 60 for v in arms.values())
    pen = {'levels': {k: {m: avg(v, m) for m in K} for k, v in arms.items()}}
    for a, b in [('HZ300', 'HZ0'), ('C0', 'HZ0'), ('C300', 'HZ300')]:
        pen[f'{a}-{b}'] = {m: paired(arms[a], arms[b], m) for m in K}
        print('phạt', f'{a}-{b}', {m: (round(v['mean'], 2), round(v['lo'], 2), round(v['hi'], 2)) for m, v in pen[f'{a}-{b}'].items()})
    res['D'] = pen

# ---------------- C1: ngân sách 10 ngày
C1 = load('alpha10')
if C1 and A:
    days10 = sorted({r['day'] for r in C1})
    al = {}
    for a in [0, 10, 20, 35, 50, 75, 100]:
        src = [r for r in A if r['day'] in days10] if a == 35 else C1
        hz, rc = per_day(src, 'G2', f'EOHZ{a}'), per_day(src, 'G2', f'RCE{a}')
        assert len(hz) == 10 and len(rc) == 10, (a, len(hz), len(rc))
        al[a] = {'EOHZ': {k: avg(hz, k) for k in ['served', 'rev']}, 'RCE': {k: avg(rc, k) for k in ['served', 'rev']}}
        al[a]['rev_red_pct'] = 100 * (1 - al[a]['RCE']['rev'] / al[a]['EOHZ']['rev']) if al[a]['EOHZ']['rev'] else None
        al[a]['served_loss_pct'] = 100 * (1 - al[a]['RCE']['served'] / al[a]['EOHZ']['served'])
        print('ngân sách', a, {k: (round(v, 2) if isinstance(v, float) else v) for k, v in al[a].items() if not isinstance(v, dict)}, al[a]['EOHZ'], al[a]['RCE'])
    res['C1'] = al

# ---------------- C2: số xe 30 ngày, r0
fl = {}
for n in [20, 29, 43, 52, 62, 67, 81]:
    rows = load(f'fleet-{n}')
    if not rows:
        continue
    assert all(r['nVeh'] == n for r in rows)
    hz, rc = per_day(rows, 'G2', 'EOHZ35', samples=1), per_day(rows, 'G2', 'RCE35', samples=1)
    assert len(hz) == 30 and len(rc) == 30
    fl[n] = {'rev': paired(rc, hz, 'rev'), 'served': paired(rc, hz, 'served'), 'rev_hz': avg(hz, 'rev')}
    fl[n]['rev_red_pct'] = -100 * fl[n]['rev']['mean'] / fl[n]['rev_hz']
    print('số xe', n, 'hiệu đổi qua lại', round(fl[n]['rev']['mean'], 2), f"({fl[n]['rev_red_pct']:.1f}%)", 'hiệu phục vụ',
          round(fl[n]['served']['mean'], 2), [round(fl[n]['served']['lo'], 2), round(fl[n]['served']['hi'], 2)])
res['C2'] = fl
OUT.write_text(json.dumps(res, ensure_ascii=False, indent=1, default=str), encoding='utf-8')
print('đã ghi', OUT)
