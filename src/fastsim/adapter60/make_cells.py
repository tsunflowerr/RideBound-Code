"""Liệt kê các ô cần cho bản chạy khớp FleetPy của Chương 5 (2026-11-08).

Ghi ra:
  cells-60.txt         360 ô của 60 ngày chính (ngày-khung-mẫu), theo nhom-ngay.json nhóm via-60
  cells-60-convert.txt 276 ô cần chuyển sang dạng mạng nút đầy đủ (bỏ 14 ngày đã có trong fullsys-f14-v2)
"""
import json
import sys
import os
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
here = Path(__file__).resolve().parent
groups = json.loads((here / 'nhom-ngay.json').read_text(encoding='utf-8'))['groups']
days60 = sorted(groups['via-60'])
f14 = set(groups['du-bao-14'])
assert len(days60) == 60 and len(f14) == 14 and f14 <= set(days60)
src = Path(os.environ['RB_DATA']) / 'wf14' / 'scenarios' / 'weekdays-P512'
cells, conv = [], []
for d in days60:
    for w in ('w08', 'w17'):
        for r in (0, 1, 2):
            name = f'{d}-{w}-r{r}'
            assert (src / (name + '.json')).is_file(), name
            cells.append(name)
            if d not in f14:
                conv.append(name)
(here / 'cells-60.txt').write_text('\n'.join(cells) + '\n', encoding='utf-8')
(here / 'cells-60-convert.txt').write_text('\n'.join(conv) + '\n', encoding='utf-8')
print(len(cells), 'ô 60 ngày;', len(conv), 'ô cần chuyển')
