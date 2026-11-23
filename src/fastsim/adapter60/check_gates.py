"""Kiểm hai cổng của PLAN.md cho bản chạy khớp FleetPy (2026-11-08).

G1: từng dòng khách của G2 SHHZ71 (không dự báo) và G2 SHRC71 (dự báo cả tháng) trên 84 ô 14 ngày phải trùng file dự đoán
    đã khóa fullsys-f14-predictions.tsv. Kèm kiểm số tổng hợp của nhánh đối chứng theo quy tắc ngày của Chương 5.
G2: 8 nhánh không Shadow trên 2 ô phải trùng emulator-riders-via-full.tsv (14 cột đầu).
"""
import collections
import json
import sys
import os
from pathlib import Path
from statistics import mean

sys.stdout.reconfigure(encoding='utf-8')
R = Path(os.environ.get('RB_RESULTS') or Path(__file__).resolve().parent.parent / 'results')


def lines_of(path, roles=None, ncol=None, rename=None):
    out = collections.Counter()
    with open(path, encoding='utf-8') as f:
        next(f)
        for ln in f:
            v = ln.rstrip('\n').split('\t')
            if rename:
                v[0] = rename.get(v[0], v[0])
            if roles and v[0] not in roles:
                continue
            out['\t'.join(v[:ncol] if ncol else v)] += 1
    return out


def mine(name, **kw):
    c = collections.Counter()
    for p in sorted((R / name).glob('riders-*.tsv')):
        c += lines_of(p, **kw)
    return c


ok = True
# ---------------- G1
roles = {'FS-G2-SHHZ71-N', 'FS-G2-SHRC71-CL'}
sealed = lines_of(os.path.join(os.environ['RB_RESEARCH'], 'fullsys-f14-v2', 'predictions', 'fullsys-f14-predictions.tsv'), roles=roles)
got = mine('gate1-f14', roles=roles)
g1 = sealed == got
ok &= g1
print(f'G1 dòng khách: đã khóa {sum(sealed.values())}, chạy lại {sum(got.values())}, trùng hoàn toàn: {g1}')
if not g1:
    print('  chỉ ở bản khóa:', sum((sealed - got).values()), '| chỉ ở bản chạy lại:', sum((got - sealed).values()))

rows = [json.loads(l) for p in sorted((R / 'gate1-f14').glob('rows-*.ndjson')) for l in p.read_text(encoding='utf-8').splitlines() if l.strip()]
want = {'served': 695.57, 'visRev60': 45.74, 'late': 245.08, 'tv5': 230.73}   # synth-real-30d.md, F14 cả ngày, adapter, đối chứng
by = collections.defaultdict(list)
for r in rows:
    if r['config'] == 'G2' and r['arm'] == 'SHHZ71' and r['mode'] == 'N':
        by[r['day']].append(r)
assert len(by) == 14 and all(len(v) == 6 for v in by.values()), {k: len(v) for k, v in by.items()}
d = []
for day, rr in by.items():
    sv = sum(r['served'] for r in rr)
    d.append({'served': sv / 3, 'visRev60': sum(r['visRev60'] for r in rr) / 3,
              'late': 1000 * sum(r['late60First'] for r in rr) / sv, 'tv5': sum(r['sumTV5'] for r in rr) / sv / 1000})
agg = {k: round(mean(x[k] for x in d), 2) for k in want}
print('G1 số tổng hợp đối chứng 14 ngày:', agg, '| số đã lưu:', want)

# ---------------- G2
ren = {'FS-INS-U-N': 'VIA-U', 'FS-INS-RCE35-N': 'VIA-RCE35', 'FS-INS-EOHZ35-N': 'VIA-EOHZ35', 'FS-INS-EO35-N': 'VIA-EO35',
       'FS-G2-U-N': 'VIA-G2K8-U', 'FS-G2-RCE35-N': 'VIA-G2K8-RCE35', 'FS-G2-EOHZ35-N': 'VIA-G2K8-EOHZ35', 'FS-G2-EO35-N': 'VIA-G2K8-EO35'}
sealed2 = lines_of(os.path.join(os.environ['RB_RESEARCH'], 'via-full-v1', 'predictions', 'emulator-riders-via-full.tsv'), ncol=14)
got2 = mine('gate2-viafull', ncol=14, rename=ren)
g2 = sealed2 == got2
ok &= g2
print(f'G2 dòng khách: đã khóa {sum(sealed2.values())}, chạy lại {sum(got2.values())}, trùng hoàn toàn: {g2}')
if not g2:
    print('  chỉ ở bản khóa:', sum((sealed2 - got2).values()), '| chỉ ở bản chạy lại:', sum((got2 - sealed2).values()))
print('CỔNG ĐẠT' if ok else 'CỔNG KHÔNG ĐẠT')
