"""Vẽ lại Hình 5.1, 5.4 của Chương 5 từ aggregate.json (mô phỏng nhanh khớp FleetPy), cùng kiểu với hình cũ
(verify_evidence.py ngày 29/10: Times New Roman 11, bỏ khung trên và phải, cùng bảng màu). Ghi vào thư mục hình mới, không
ghi đè hình cũ. Chạy bằng môi trường fleetpy-1.0.2 với PATH chứa các thư mục Library của môi trường (xem ghi chú bẫy môi trường).
"""
import json
import sys
import os
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib import font_manager

sys.stdout.reconfigure(encoding='utf-8')
HERE = Path(__file__).resolve().parent
FIG = Path(os.environ.get('RB_FIGURES') or HERE.parent / 'results' / 'figures')
FIG.mkdir(parents=True, exist_ok=True)
for f in ['times.ttf', 'timesbd.ttf', 'timesi.ttf']:
    p = Path(r'C:\Windows\Fonts') / f
    if p.exists():
        font_manager.fontManager.addfont(str(p))
plt.rcParams.update({'font.family': 'Times New Roman', 'font.size': 11, 'axes.spines.top': False, 'axes.spines.right': False,
                     'pdf.fonttype': 42, 'savefig.bbox': 'tight'})
agg = json.loads((HERE / 'aggregate.json').read_text(encoding='utf-8'))

# ---------------- Hình 5.1: phục vụ và đổi qua lại, 60 ngày
names = {'U': 'Không giới hạn', 'EO35': 'Hạn cố định', 'EOHZ35': 'Hạn điều chỉnh', 'RCE35': 'Cộng dồn'}
cols = {'U': '#858b92', 'EO35': '#ab7e54', 'EOHZ35': '#27617b', 'RCE35': '#ad3e3e'}
offs = {'U': (-75, 6), 'EO35': (-62, -16), 'EOHZ35': (14, 14), 'RCE35': (16, -4)}
fig, axes = plt.subplots(1, 2, figsize=(8.2, 3.5))
for ax, cfg, title in zip(axes, ['INS', 'G2'], ['Chỉ chèn điểm dừng', 'Cho đổi xe']):
    xs, ys = [], []
    for a in names:
        s = agg['A_levels'][f'{cfg}/{a}']
        xs.append(s['served']); ys.append(s['rev'])
        ax.scatter(s['served'], s['rev'], color=cols[a], s=45, zorder=3)
        ax.annotate(names[a], (s['served'], s['rev']), xytext=offs[a], textcoords='offset points', fontsize=10,
                    arrowprops=None if a == 'U' else {'arrowstyle': '-', 'color': cols[a], 'linewidth': .7})
    ax.set_xlim(min(xs) - 25, max(xs) + 10)
    ax.set_ylim(min(ys) - 0.15 * (max(ys) - min(ys)) - 2, max(ys) + 0.12 * (max(ys) - min(ys)) + 2)
    ax.set_title(title, fontsize=12); ax.set_xlabel('Khách được phục vụ mỗi ngày'); ax.grid(alpha=.2)
axes[0].set_ylabel('Khách có giờ trả đổi qua lại mỗi ngày')
fig.tight_layout(); fig.savefig(FIG / 'phuc-vu-va-thay-doi.pdf'); fig.savefig(FIG / 'phuc-vu-va-thay-doi.png', dpi=180); plt.close(fig)

# ---------------- Hình 5.4: ngân sách đổi giờ, 10 ngày
C1 = agg['C1']
al = sorted(int(a) for a in C1)
fig, axes = plt.subplots(1, 2, figsize=(8.2, 3.3))
for fam, col, label in [('EOHZ', '#27617b', 'Hạn điều chỉnh'), ('RCE', '#ad3e3e', 'Cộng dồn')]:
    for ax, field in zip(axes, ['served', 'rev']):
        ax.plot(al, [C1[str(a)][fam][field] for a in al], '-o', color=col, label=label, ms=4)
        ax.set_xlabel('Ngân sách đổi giờ (% khoảng dự phòng)'); ax.grid(alpha=.2)
axes[0].set_ylabel('Khách được phục vụ mỗi ngày'); axes[1].set_ylabel('Khách có giờ trả đổi qua lại mỗi ngày')
axes[0].legend(frameon=False)
fig.tight_layout(); fig.savefig(FIG / 'do-nhay-han-muc.pdf'); fig.savefig(FIG / 'do-nhay-han-muc.png', dpi=180); plt.close(fig)
print('đã vẽ vào', FIG)
