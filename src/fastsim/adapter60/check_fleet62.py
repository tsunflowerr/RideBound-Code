"""A4 kiểm: ô 81 xe cắt còn 62 xe (fleet-62) phải cho đúng kết quả của ô chuẩn 62 xe (main60, mẫu r0), từng ô-nhánh (chữ ký)."""
import json
import sys
import os
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
R = Path(os.environ.get('RB_RESULTS') or Path(__file__).resolve().parent.parent / 'results')


def load(n):
    return [json.loads(l) for p in sorted((R / n).glob('rows-*.ndjson')) for l in p.read_text(encoding='utf-8').splitlines() if l.strip()]


a = {(r['cell'], r['arm']): r['sig'] for r in load('main60') if r['rs'] == 'r0' and r['config'] == 'G2' and r['arm'] in ('EOHZ35', 'RCE35')}
f = {(r['cell'], r['arm']): r['sig'] for r in load('fleet-62')}
same = sum(1 for k in f if a.get(k) == f[k])
print(f'fleet-62 (ô 81 xe cắt còn 62) trùng ô chuẩn: {same}/{len(f)}')
