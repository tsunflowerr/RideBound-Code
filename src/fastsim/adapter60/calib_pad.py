"""A5: hiệu chỉnh hệ số đều (PAD) ở chế độ khớp FleetPy, đúng mục tiêu của wf21/n7 calib7.js: với mỗi khung giờ, chọn hệ số r
sao cho thời lượng báo đầu trung bình (giờ trả báo đầu trừ giờ đặt, gộp mọi khách được hứa) của PAD lệch không quá 0,5 giây so
với dự báo cả tháng (CL). Chỉ đọc thời lượng báo đầu, không đọc kết quả. Cấu hình: cho đổi xe, Shadow, cộng dồn (G2 SHRC71), 30 ngày.

Vòng 0 dùng hệ số cuối của bản cũ (flat_iter0.json); các vòng sau dùng phương pháp dây cung (secant), điểm xuất phát gồm r = 1
(= không dự báo, mode N) và vòng 0. Tối đa 6 vòng. Ghi calib-result.json.
"""
import json
import shutil
import subprocess
import sys
import os
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
HERE = Path(__file__).resolve().parent
R = Path(os.environ.get('RB_RESULTS') or Path(__file__).resolve().parent.parent / 'results')
TOL = 0.5
WINS = ['w08', 'w17']
BASH = shutil.which('bash')


def rows(name):
    out = []
    for p in sorted((R / name).glob('rows-*.ndjson')):
        out += [json.loads(l) for l in p.read_text(encoding='utf-8').splitlines() if l.strip()]
    return out


def lead(rr, mode, win):
    s = [r for r in rr if r['config'] == 'G2' and r['arm'] == 'SHRC71' and r['mode'] == mode and r['win'] == win]
    assert len(s) == 90, (mode, win, len(s))
    return sum(r['sumLead'] for r in s) / sum(r['nLead'] for r in s) / 1000


fc = rows('fc30')
target = {w: lead(fc, 'CL', w) for w in WINS}
lN = {w: lead(fc, 'N', w) for w in WINS}
print('đích (CL):', target, '| không dự báo (r=1):', lN)
flat = json.loads((HERE / 'flat_iter0.json').read_text(encoding='utf-8'))['flat']
hist = {w: [(1.0, lN[w])] for w in WINS}
result = {'tolS': TOL, 'target': target, 'iterations': []}
for k in range(6):
    ff = HERE / f'flat_iter{k}.json'
    if k > 0:
        ff.write_text(json.dumps({'note': f'A5 adapter calibration iteration {k}', 'flat': flat}, indent=1), encoding='utf-8')
    name = f'padcal-{k}'
    if not (R / name).exists():
        cp = subprocess.run([BASH, str(HERE / 'run_shards_pp.sh'), name, 'cells-30.txt', 'G2:SHRC71:PAD', '6', f'--flat {ff.as_posix()}'], cwd=HERE)
        if cp.returncode != 0:
            raise SystemExit('shard run failed ' + name)
    rr = rows(name)
    got = {w: lead(rr, 'PAD', w) for w in WINS}
    gap = {w: got[w] - target[w] for w in WINS}
    result['iterations'].append({'k': k, 'run': name, 'flat': dict(flat), 'lead': got, 'gap': gap})
    print(f'vòng {k}:', {w: (flat[f"G2|{w}|SHRC"], round(got[w], 2), round(gap[w], 2)) for w in WINS})
    for w in WINS:
        hist[w].append((flat[f'G2|{w}|SHRC'], got[w]))
    if all(abs(gap[w]) <= TOL for w in WINS):
        result['converged'] = True
        result['final'] = name
        break
    for w in WINS:
        (r1, l1), (r2, l2) = hist[w][-2], hist[w][-1]
        if abs(gap[w]) <= TOL:
            continue
        flat[f'G2|{w}|SHRC'] = round(r2 + (target[w] - l2) * (r2 - r1) / (l2 - l1), 6) if l2 != l1 else r2
else:
    result['converged'] = False
    result['final'] = result['iterations'][-1]['run']
(HERE / 'calib-result.json').write_text(json.dumps(result, ensure_ascii=False, indent=1), encoding='utf-8')
print('kết quả:', 'hội tụ' if result['converged'] else 'chưa hội tụ', '| lượt dùng:', result['final'])
