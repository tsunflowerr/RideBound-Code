# Nhật ký phép hòa vốn

Kế hoạch: `BREAKEVEN-PLAN.md`, niêm phong lúc 00:54:37 ngày 26/10/2026, sha `2e82c711…` (`BREAKEVEN-PLAN.sha256`). Mọi
sửa đổi sau niêm phong được ghi ở đây. Không sửa tệp kế hoạch.

## 00:55: thử nhanh (`be_smoke.js`, không ghi tệp)

- Ô `2019-02-13-w08-r0`; INS và G2; các nhánh EOHZ35, RCE35, EO10.
- Cả 6 lượt khớp 5 trường cổng với dòng cũ: EOHZ35/RCE35 so với `plain-a1`, EO10 so với `n2-plain`.
- Với mọi khách, tổng `c` trên `revs` bằng `r.net`: 0 lệch.
- Máy cắm sạc, RAM trống 5,8 GB.

## 00:56: bắt đầu lượt chạy đầy đủ

- Lệnh: `node be_run.js E:/RideBoundData/research/breakeven-v1/run1 --workers 3`.
- Nhật ký chạy: `E:/RideBoundData/research/breakeven-v1/run1.log`.
- Quy mô: 120 việc (60 ô × 2 cấu hình), mỗi việc 21 nhánh.

**Ghi chú về cách tính (không đổi kế hoạch):**
- Phân vị bootstrap dùng `numpy.percentile(method="inverted_cdf")`, tức chọn đúng một giá trị trong mẫu. Lý do: có
  giá trị +∞ (E-luôn) nên không nội suy được.
- `be_table.py` báo thêm `boot_cases_Ld`, tỉ lệ chọn C ở từng w, và bảng đường cong mô tả `BREAKEVEN-CURVES.tsv`.

## 01:05: chạy xong, 0 lỗi

- 120/120 việc trong 8,9 phút, 0 lỗi.
- Đầu ra:
  - `run1/rows-be.ndjson`: 2.520 dòng;
  - `run1/riders-be.ndjson`: 207 MB, một dòng mỗi khách đã được hứa.

## 01:05: cổng ĐẠT (`GATE.txt`)

**A3 (sửa lỗi số học trong kế hoạch, không đổi nội dung).** Mục 4 của kế hoạch ghi "60 ô × 2 cấu hình × 42 nhánh =
5.040". Con số đó sai vì đếm cấu hình hai lần. Số đúng là 60 × 2 × 21 = 2.520, khớp với mục 2 ("42 nhánh mỗi ô, 2.520 lượt
mô phỏng", tức 21 nhánh × 2 cấu hình). `be_gate.py` dùng 2.520.

Kết quả cổng:
- 2.520/2.520 dòng có dòng cũ để so;
- 0 chênh lệch ở 5 trường cổng;
- 0 chênh lệch ở mọi trường số khác có ở cả hai bên;
- 0 dòng sai bất biến (tổng `c` bằng `net`, `R60` bằng `visRev60`, `La60` bằng `late60First`).

Với α = 35, các dòng chạy ở chế độ `off` khớp đúng với `plain-a1` chạy ở chế độ `causal`. Bộ chấm điểm quả thật chỉ đọc.

## 01:05: bảng v1

`be_table.py` (nay giữ ở `be_table_v1.py`, sha `0e13a901…`) ghi ba tệp:
- `BREAKEVEN-TABLE.tsv` (`8bedc1f4…`);
- `BREAKEVEN-CURVES.tsv` (`f68e50d2…`);
- `BREAKEVEN-SUMMARY.json` (`9be48f88…`).

Đường S theo α tăng đơn điệu ở cả 6 cặp (cấu hình, họ luật).

## 01:06: A4, lỗi định dạng, viết lại thành v2 (không đổi số)

**Lỗi.** Ở 4 dòng "outside-grid" của `BREAKEVEN-TABLE.tsv`, đoạn đệm trống thiếu 1 cột (15 thay vì 16). Hệ quả là các cột
bootstrap của 4 dòng này bị lệch sang trái một cột.

**Sửa.**
- Sửa đoạn đệm trong `be_table.py`.
- Thêm 4 cột mô tả ở cuối, theo mục 3 của kế hoạch: `dLd120`, `dLt60`, `dNet_min`, `dDA_min`.
- Ghi ra các tệp mới `*-v2.*`. Tệp v1 giữ nguyên.

**Kiểm.**
- Ở mọi dòng không phải "outside-grid", 32 cột đầu của v2 giống hệt v1.
- `BREAKEVEN-CURVES-v2.tsv` giống hệt v1 từng byte.
- Từ đây dùng v2.

## 01:08: kết quả

Xem `BREAKEVEN-RESULT.md`. Kết quả chính, G2, phục vụ bằng nhau, so với EOHZ35:
- ΔR = −13,48, ΔLd = +12,72;
- w\* = 0,944, khoảng bootstrap 95% từ 0,463 tới 1,614;
- 100% số lần lặp rơi vào trường hợp C-nếu-w-lớn.

Tính theo La cho w\* = 0,769, khớp con số minh họa 0,77 trước đó.
