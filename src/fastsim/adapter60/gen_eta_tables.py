"""A6: sinh cac dong bang LaTeX tu full_common.json (he thong day du) va eta_analysis.json (mo phong nhanh). Khong lam tron tay."""
import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
HERE = Path(__file__).resolve().parent
# full_common.json comes from full_common.py, which reads the evidence of the main-program runs (RB_EVIDENCE, not shipped);
# without it only the fast-simulator blocks are printed.
FC = json.loads((HERE / 'full_common.json').read_text(encoding='utf-8')) if (HERE / 'full_common.json').exists() else None
EA = json.loads((HERE / 'eta_analysis.json').read_text(encoding='utf-8')) if (HERE / 'eta_analysis.json').exists() else None


def m(x, n=2):
    s = f'{abs(x):.{n}f}'.replace('.', '{,}')
    return ('$-' + s + '$') if round(x, n) < 0 else ('$+' + s + '$')


def ci(p, n=2):
    return f"{m(p['mean'], n)} [{m(p['lo'], n)}; {m(p['hi'], n)}]"


def f(x, n=1):
    return f'{x:.{n}f}'.replace('.', ',')


LAB = {'rev': 'Khách có giờ trả đổi qua lại (khách/ngày)', 'late': 'Khách đến muộn quá 1 phút (/1.000)', 'early': 'Khách đến sớm quá 1 phút (/1.000)',
       'tv5': 'Giây giờ báo bị đổi (giây/khách)', 'init_abs': 'Sai số tuyệt đối giờ báo đầu (phút)', 'timed_abs': 'Sai số tuyệt đối theo thời gian (phút)',
       'served': 'Khách phục vụ (khách/ngày)'}
DEC = {'rev': 2, 'late': 2, 'early': 2, 'tv5': 2, 'init_abs': 2, 'timed_abs': 3, 'served': 2}

if FC:
    print('% ===== 5.5: he thong day du, moi khach vs khach chung')
    print('% so khach:', FC['n'])
    for sc, name in [('day', 'Cả ngày'), ('w08', 'Sáng (8--10h)'), ('w17', 'Chiều (17--19h)')]:
        print(f'\\multicolumn{{3}}{{@{{}}l}}{{\\textit{{{name}}}}}\\\\')
        for k in ['rev', 'late', 'tv5', 'init_abs', 'timed_abs']:
            a, c = FC['diff'][sc]['all'][k], FC['diff'][sc]['common'][k]
            print(f"{LAB[k]} & {ci(a, DEC[k])} & {ci(c, DEC[k])}\\\\")
        if sc == 'day':
            print('%  (phuc vu, mo ra)', ci(FC['diff'][sc]['all']['served']))
else:
    print('% ===== 5.5: bo qua (khong co full_common.json; can bang chung cua chuong trinh chinh)')
if EA:
    print('\n% ===== 5.4: bang muc (N, PAD, CL, CLc)')
    for lab, nm in [('N', 'Không dự báo'), ('PAD', 'Hiệu chỉnh đều'), ('CL', 'Dự báo theo giờ, chuẩn hóa cả tháng'), ('CLc', 'Dự báo theo giờ, tham chiếu tháng trước')]:
        L = EA['levels'][lab]
        print(f"{nm} & {f(L['w08']['init_abs'], 2)} & {f(L['w17']['init_abs'], 2)} & {f(L['w08']['timed_abs'], 3)} & {f(L['w17']['timed_abs'], 3)}\\\\")
    for lab, nm in [('N', 'Không dự báo'), ('PAD', 'Hiệu chỉnh đều'), ('CL', 'Dự báo theo giờ, chuẩn hóa cả tháng'), ('CLc', 'Dự báo theo giờ, tham chiếu tháng trước')]:
        L = EA['levels'][lab]
        print(f"{nm} & {f(L['w08']['rev'], 1)} & {f(L['w17']['rev'], 1)} & {f(L['w08']['tv5'], 1)} & {f(L['w17']['tv5'], 1)}\\\\")
    print('\n% ===== 5.4: hieu du bao - hieu chinh deu')
    for k in ['late', 'early', 'rev', 'tv5', 'init_abs', 'timed_abs', 'served']:
        print(f"{LAB[k]} & " + ' & '.join(ci(EA['diff'][sc]['CL-PAD'][k], DEC[k]) for sc in ['day', 'w08', 'w17']) + '\\\\')
    print('% CL-N')
    for k in ['late', 'early', 'rev', 'tv5', 'init_abs', 'timed_abs', 'served']:
        print(f"{LAB[k]} & " + ' & '.join(ci(EA['diff'][sc]['CL-N'][k], DEC[k]) for sc in ['day', 'w08', 'w17']) + '\\\\')
    print('% PAD-N')
    for k in ['late', 'early', 'rev', 'tv5', 'init_abs', 'timed_abs', 'served']:
        print(f"{LAB[k]} & " + ' & '.join(ci(EA['diff'][sc]['PAD-N'][k], DEC[k]) for sc in ['day', 'w08', 'w17']) + '\\\\')
    print('% CLc-CL')
    for k in ['late', 'rev', 'tv5', 'init_abs', 'timed_abs']:
        print(f"{LAB[k]} & " + ' & '.join(ci(EA['diff'][sc]['CLc-CL'][k], DEC[k]) for sc in ['day', 'w08', 'w17']) + '\\\\')
    print('\n% ===== 5.6: phan ra bon cau hinh (moi khach) va tren khach chung')
    for blk, src in [('moi khach', lambda sc, st, k: EA['diff'][sc][st][k]),
                     ('khach chung', lambda sc, st, k: EA['common']['bon_cau_hinh']['diff'][sc][st][k])]:
        print('% ', blk, '| so khach chung', EA['common']['bon_cau_hinh']['n_common'], EA['common']['bon_cau_hinh']['n_each'])
        for k in ['served', 'rev', 'late', 'tv5', 'init_abs', 'timed_abs']:
            row = [src('day', st, k) for st in ['X11-X01', 'X01-X00', 'X11-X00']]
            print(f"{LAB[k]} & {ci(row[0], DEC[k])} & {ci(row[1], DEC[k])} & {m(row[2]['mean'], DEC[k])}\\\\")
    print('% theo khung (khach chung)')
    for sc in ['w08', 'w17']:
        for k in ['late', 'init_abs', 'timed_abs']:
            row = [EA['common']['bon_cau_hinh']['diff'][sc][st][k] for st in ['X11-X01', 'X01-X00', 'X11-X00']]
            print(sc, k, ' | '.join(ci(r, DEC[k]) for r in row))
