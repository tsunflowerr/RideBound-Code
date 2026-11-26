"""Sinh các dòng bảng LaTeX và các số cho lời văn Chương 5 từ aggregate.json (+ nhóm Shadow từ main60). Không làm tròn tay."""
import json
import sys
import os
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
HERE = Path(__file__).resolve().parent
R = Path(os.environ.get('RB_RESULTS') or Path(__file__).resolve().parent.parent / 'results')
g = json.loads((HERE / 'aggregate.json').read_text(encoding='utf-8'))


def f(x, n=1):
    return f'{abs(x):.{n}f}'.replace('.', ',') if x >= 0 else '-' + f'{abs(x):.{n}f}'.replace('.', ',')


def m(x, n=2, sign=True):
    s = f'{abs(x):.{n}f}'.replace('.', '{,}')
    return ('$-' + s + '$') if x < 0 else (('$+' + s + '$') if sign else s)


def ci(p, n=2):
    return f"{m(p['mean'], n)} [{m(p['lo'], n)}; {m(p['hi'], n)}]"


print('% Bảng 5.5')
names = {'U': 'Không giới hạn', 'EO35': 'Hạn cố định', 'EOHZ35': 'Hạn điều chỉnh', 'RCE35': 'Giới hạn cộng dồn'}
for cfg in ['INS', 'G2']:
    print('%', cfg)
    for a, nm in names.items():
        v = g['A_levels'][f'{cfg}/{a}']
        print(f"{nm} & {f(v['served'])} & {f(v['rev'])} & {f(v['late'])} & {f(v['tv5'])}\\\\")
print('% Bảng 5.6')
lab = {'served': 'Khách phục vụ (khách/ngày)', 'rev': 'Khách có giờ trả đổi qua lại (khách/ngày)',
       'late': 'Khách đến muộn quá 1 phút (/1.000)', 'tv5': 'Giây giờ báo bị đổi (giây/khách)'}
for k, nm in lab.items():
    print(f"{nm} & " + ' & '.join(ci(g['A_diffs'][c][k]) for c in ['INS', 'G2', 'G2+Shadow']) + '\\\\')
for c in ['INS', 'G2', 'G2+Shadow']:
    d = g['A_diffs'][c]
    print('%', c, 'cao/thấp:', {k: (d[k]['up'], d[k]['down']) for k in ['served', 'rev', 'late', 'tv5']},
          'vượt hạn trả %:', ci(d['deadline_pct']), 'mức:', {s: {k: round(v, 2) for k, v in d['levels'][s].items()} for s in d['levels']})
print('% vượt hạn trả theo cơ chế (%):', {k: round(v['deadline_pct'], 2) for k, v in g['A_levels'].items()})
print('% vượt ngân sách:', g['A_breach_INS'], g['A_breach_G2'])
for arm in ['SHHZ71', 'SHRC71']:
    rows = [json.loads(l) for p in sorted((R / 'main60').glob('rows-*.ndjson')) for l in p.read_text(encoding='utf-8').splitlines() if l.strip()]
    rr = [r for r in rows if r['config'] == 'G2' and r['arm'] == arm]
    G = (sum(r['G_n'] for r in rr), sum(r['G_rev60'] for r in rr)); A = (sum(r['A_n'] for r in rr), sum(r['A_rev60'] for r in rr))
    print(f'% nhóm Shadow {arm}: ngân sách 0 {G} -> {1000 * G[1] / G[0]:.2f}/1000; có ngân sách {A} -> {1000 * A[1] / A[0]:.2f}/1000')
print('% Ngân sách (10 ngày):')
for a, v in sorted(g['C1'].items(), key=lambda kv: int(kv[0])):
    print(f"%  {a}%: giảm đổi qua lại {v['rev_red_pct']:.1f}%, mất phục vụ {v['served_loss_pct']:.2f}%")
print('% Bảng 5.12 (số xe)')
for n, v in sorted(g['C2'].items(), key=lambda kv: int(kv[0])):
    print(f"{n} & {m(v['rev']['mean'])} & {f(v['rev_red_pct'])} & {m(v['served']['mean'])} & [{m(v['served']['lo'])}; {m(v['served']['hi'])}]\\\\")
D = g.get('D')
if D:
    print('% Bảng 5.13 (phạt)')
    for k, nm in [('HZ300-HZ0', 'Thêm phạt 300 giây vào hạn điều chỉnh'), ('C0-HZ0', 'Thay hạn điều chỉnh bằng cộng dồn, không phạt'),
                  ('C300-HZ300', 'Thêm cộng dồn khi đã có phạt 300 giây')]:
        print(nm + ' & ' + ' & '.join('\\shortstack{' + m(D[k][x]['mean']) + '\\\\{[' + m(D[k][x]['lo']) + '; ' + m(D[k][x]['hi']) + ']}}'
                                     for x in ['served', 'rev', 'late', 'tv5']) + '\\\\[3pt]')
    print('% mức:', {k: {x: round(y, 2) for x, y in v.items()} for k, v in D['levels'].items()})
