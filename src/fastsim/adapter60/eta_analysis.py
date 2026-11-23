"""A6: ket qua cua eta30 (1.080 luot, mo phong nhanh khop FleetPy): sai so gio bao cua ba cach uoc luong (muc 5.4), bon cau hinh
(muc 5.6) va tap khach chung. Quy tac gop nhu aggregate.py: moi ngay gop cac dong cua ngay (khung x 3 mau); khach/ngay = tong / 3;
ty le gop tu so va mau so trong ngay; trung binh tren 30 ngay; hieu ghep cap theo ngay, khoang tin cay 95% theo t (df 29).

Cong kiem (in ra truoc ket qua): (1) hieu bon cau hinh phai tai tao Bang 5.11 (-7,56 / -13,99 / +4,08 / -17,61; +0,89 / -13,37 /
-38,34 / -20,17; tong -6,67 / -27,36 / -34,26 / -37,78); (2) bang 5.7 (muon, som, phuc vu, H, sai so co dau); (3) sai so tuyet doi cua
SHHZ71-N va SHRC71-CL so voi he thong day du (3,05 / 2,88 va 0,68 / 0,60 phut); (4) tap khach chung.
Ghi eta_analysis.json.
"""
import collections
import glob
import json
import math
import sys
import os
from pathlib import Path
from statistics import mean, stdev

sys.stdout.reconfigure(encoding='utf-8')
HERE = Path(__file__).resolve().parent
D = Path(os.environ.get('RB_RESULTS') or Path(__file__).resolve().parent.parent / 'results') / 'eta30'
T975_29 = 2.0452296


def load_rows():
    return [json.loads(l) for p in sorted(glob.glob(str(D / 'rows-*.ndjson'))) for l in open(p, encoding='utf-8') if l.strip()]


ROWS = load_rows()
assert len(ROWS) == 1080, len(ROWS)
ARM = {'N': ('SHRC71', 'N'), 'PAD': ('SHRC71', 'PAD'), 'CL': ('SHRC71', 'CL'), 'CLc': ('SHRC71', 'CLc'),
       'X00': ('SHHZ71', 'N'), 'X01': ('SHHZ71', 'CL'), 'X10': ('SHRC71', 'N'), 'X11': ('SHRC71', 'CL')}
KEYS = ['served', 'rev', 'late', 'early', 'tv5', 'lead', 'init_abs', 'timed_abs', 'init_s', 'timed_s']


def day_levels(label, win=None):
    arm, mode = ARM[label]
    g = collections.defaultdict(list)
    for r in ROWS:
        if r['arm'] == arm and r['mode'] == mode and (win is None or r['win'] == win):
            g[r['day']].append(r)
    out = {}
    for day, rr in g.items():
        assert len(rr) == (3 if win else 6), (label, day, len(rr))
        su = lambda k: sum(r[k] for r in rr)
        sv, ne = su('served'), su('nEta')
        assert ne == sv and su('nBadEta') == 0
        out[day] = {'served': sv / 3, 'rev': su('visRev60') / 3, 'late': 1000 * su('late60First') / sv, 'early': 1000 * su('early60First') / sv,
                    'tv5': su('sumTV5') / sv / 1000, 'lead': su('sumLead') / su('nLead') / 1000,
                    'init_abs': su('sumInitAbs') / ne / 60000, 'timed_abs': su('sumTimedAbs') / ne / 60000,
                    'init_s': su('sumInitSigned') / ne / 60000, 'timed_s': su('sumTimedSigned') / ne / 60000}
    assert len(out) == 30, (label, len(out))
    return out


def avg(d, k):
    return mean(v[k] for v in d.values())


def paired(a, b, k):
    days = sorted(a)
    assert days == sorted(b)
    v = [a[x][k] - b[x][k] for x in days]
    m = mean(v)
    hw = T975_29 * stdev(v) / math.sqrt(len(v))
    return {'mean': m, 'lo': m - hw, 'hi': m + hw, 'up': sum(x > 0 for x in v), 'down': sum(x < 0 for x in v)}


def fmt(p, n=2):
    return f"{p['mean']:+.{n}f} [{p['lo']:+.{n}f}; {p['hi']:+.{n}f}] ({p['down']}/{p['up']})"


res = {'levels': {}, 'diff': {}}
WINS = [None, 'w08', 'w17']
for label in ARM:
    for w in WINS:
        res['levels'].setdefault(label, {})[w or 'day'] = {k: avg(day_levels(label, w), k) for k in KEYS}

# ---- gate (1): the four configurations reproduce Table 5.11 (rows with the earlier table: served, rev, late, tv5)
steps = {'cong_don_khi_co_du_bao': ('X11', 'X01'), 'du_bao_khi_dung_han_dieu_chinh': ('X01', 'X00'), 'tong': ('X11', 'X00')}
print('--- cong 1: Bang 5.11 (ca ngay)')
for name, (a, b) in steps.items():
    da, db = day_levels(a), day_levels(b)
    print(name, {k: round(paired(da, db, k)['mean'], 2) for k in ['served', 'rev', 'late', 'tv5']})
print('mong doi: cong don -7,56 -13,99 +4,08 -17,61 | du bao +0,89 -13,37 -38,34 -20,17 | tong -6,67 -27,36 -34,26 -37,78')

# ---- gate (2): Table 5.7 and the signed errors (levels), by window
print('--- cong 2: Bang 5.7 (muon sang chieu ca ngay | som sang chieu | phuc vu | H sang chieu | co dau sang chieu)')
for label in ['N', 'PAD', 'CL', 'CLc']:
    L = res['levels'][label]
    print(label, [round(L['day']['late'], 1), round(L['w08']['late'], 1), round(L['w17']['late'], 1), round(L['w08']['early'], 1), round(L['w17']['early'], 1),
                  round(L['day']['served'], 1), round(L['w08']['lead'], 1), round(L['w17']['lead'], 1), round(L['w08']['init_s'], 2), round(L['w17']['init_s'], 2)])
print('mong doi (bang hien co): N 248,3 309,4 189,2 8,1 76,3 694,0 1523,6 1644,4 3,06 2,28 | PAD 217,0 226,5 208,0 37,3 37,4 693,1 1550,7 1627,7 2,38 2,75 | CL 211,0 211,8 209,8 14,2 16,8 696,4 1550,0 1628,0 2,38 2,93 | CLc 210,3 211,2 209,0 13,2 17,4 695,2 1550,0 1627,6 2,37 2,94')

# ---- gate (3): absolute errors vs the full system
print('--- cong 3: sai so tuyet doi, mo phong nhanh vs he thong day du (3,05 / 2,88 ; 0,68 / 0,60)')
for label in ['X00', 'X11']:
    arm, mode = ARM[label]
    rr = [r for r in ROWS if r['arm'] == arm and r['mode'] == mode]
    n = sum(r['nEta'] for r in rr)
    print(label, 'khach', n, 'dau', round(sum(r['sumInitAbs'] for r in rr) / n / 60000, 3), 'theo thoi gian', round(sum(r['sumTimedAbs'] for r in rr) / n / 60000, 3))
print('he thong day du: doi chung 63.277 khach 3,05 0,68 | ket hop 62.678 khach 2,88 0,60')

# ---- results of the rows (levels and paired contrasts)
for w in WINS:
    sc = w or 'day'
    res['diff'][sc] = {}
    for name, (a, b) in {'CL-PAD': ('CL', 'PAD'), 'CL-N': ('CL', 'N'), 'PAD-N': ('PAD', 'N'), 'CLc-CL': ('CLc', 'CL'),
                         'X11-X01': ('X11', 'X01'), 'X01-X00': ('X01', 'X00'), 'X11-X00': ('X11', 'X00'),
                         'X10-X00': ('X10', 'X00'), 'X11-X10': ('X11', 'X10')}.items():
        da, db = day_levels(a, w), day_levels(b, w)
        res['diff'][sc][name] = {k: paired(da, db, k) for k in KEYS}

print('--- MAE: CL - PAD / CL - N / PAD - N (sai so dau, theo thoi gian; phut) ---')
for sc in ['day', 'w08', 'w17']:
    for name in ['CL-PAD', 'CL-N', 'PAD-N']:
        d = res['diff'][sc][name]
        print(sc, name, 'dau', fmt(d['init_abs'], 3), '| thoi gian', fmt(d['timed_abs'], 3))

# ---- common-rider sets from the per-rider files
def load_riders():
    rs = collections.defaultdict(dict)
    for p in sorted(glob.glob(str(D / 'eta-*.tsv'))):
        with open(p, encoding='utf-8') as f:
            f.readline()
            for line in f:
                role, cell, rq, t0, p0, A, rev, tv5, late, early, tabs, tsig, npub = line.rstrip('\n').split('\t')
                key = (cell, rq)
                p0, A = float(p0), float(A)
                rs[role][key] = (int(rev), float(tv5) / 1000.0, int(late), int(early), float(tabs) / 60000.0, float(tsig) / 60000.0,
                                 abs(A - p0) / 60000.0, (A - p0) / 60000.0)
    return rs


RID = load_riders()
ROLES = {'N': 'FS-G2-SHRC71-N', 'PAD': 'FS-G2-SHRC71-PAD', 'CL': 'FS-G2-SHRC71-CL', 'CLc': 'FS-G2-SHRC71-CLc',
         'X00': 'FS-G2-SHHZ71-N', 'X01': 'FS-G2-SHHZ71-CL', 'X10': 'FS-G2-SHRC71-N', 'X11': 'FS-G2-SHRC71-CL'}
for r in set(ROLES.values()):
    assert r in RID, r
# the per-rider files have to agree with the rows
for label in ['N', 'PAD', 'CL', 'CLc', 'X00', 'X01']:
    arm, mode = ARM[label]
    n_rows = sum(r['served'] for r in ROWS if r['arm'] == arm and r['mode'] == mode)
    assert n_rows == len(RID[ROLES[label]]), (label, n_rows, len(RID[ROLES[label]]))


def day_of(cell):
    return cell[:10], cell[11:14], cell[-2:]


def common_levels(labels, win=None):
    roles = [ROLES[x] for x in labels]
    keys = set(RID[roles[0]])
    for r in roles[1:]:
        keys &= set(RID[r])
    out = {}
    for lab, role in zip(labels, roles):
        acc = collections.defaultdict(lambda: collections.defaultdict(float))
        for key in keys:
            cell = key[0]
            day, w, _ = day_of(cell)
            if win and w != win:
                continue
            x = RID[role][key]
            a = acc[day]
            a['n'] += 1
            for i, f in enumerate(['rev', 'tv5', 'late', 'early', 'timed_abs', 'timed_s', 'init_abs', 'init_s']):
                a[f] += x[i]
        out[lab] = {}
        for day, a in acc.items():
            n = a['n']
            out[lab][day] = {'served': n / 3, 'rev': a['rev'] / 3, 'late': 1000 * a['late'] / n, 'early': 1000 * a['early'] / n, 'tv5': a['tv5'] / n,
                             'timed_abs': a['timed_abs'] / n, 'timed_s': a['timed_s'] / n, 'init_abs': a['init_abs'] / n, 'init_s': a['init_s'] / n}
        assert len(out[lab]) == 30
    return len(keys), out


CK = ['served', 'rev', 'late', 'early', 'tv5', 'init_abs', 'timed_abs', 'init_s', 'timed_s']
res['common'] = {}
print('--- tap khach chung')
for setname, labels, contrasts in [('ba_cach', ['N', 'PAD', 'CL', 'CLc'], [('CL', 'PAD'), ('CL', 'N'), ('PAD', 'N'), ('CLc', 'CL')]),
                                   ('bon_cau_hinh', ['X00', 'X01', 'X10', 'X11'], [('X11', 'X01'), ('X01', 'X00'), ('X11', 'X00'), ('X10', 'X00'), ('X11', 'X10')])]:
    res['common'][setname] = {'labels': labels, 'levels': {}, 'diff': {}}
    for w in WINS:
        sc = w or 'day'
        nkeys, lv = common_levels(labels, w)
        if w is None:
            res['common'][setname]['n_common'] = nkeys
            res['common'][setname]['n_each'] = {lab: len(RID[ROLES[lab]]) for lab in labels}
        res['common'][setname]['levels'][sc] = {lab: {k: avg(lv[lab], k) for k in CK} for lab in labels}
        res['common'][setname]['diff'][sc] = {f'{a}-{b}': {k: paired(lv[a], lv[b], k) for k in CK} for a, b in contrasts}
    print(setname, 'khach chung', res['common'][setname]['n_common'], 'moi cau hinh', res['common'][setname]['n_each'])
    for sc in ['day', 'w08', 'w17']:
        for a, b in contrasts:
            d = res['common'][setname]['diff'][sc][f'{a}-{b}']
            print('  ', sc, a, '-', b, 'rev', fmt(d['rev']), '| muon', fmt(d['late']), '| tv5', fmt(d['tv5']), '| dau', fmt(d['init_abs'], 3), '| tg', fmt(d['timed_abs'], 3))

(HERE / 'eta_analysis.json').write_text(json.dumps(res, ensure_ascii=False, indent=1), encoding='utf-8')
print('da ghi eta_analysis.json')
