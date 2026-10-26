# wf22-home: kết quả chạy lại gói `company-emulator` ở máy nhà (26/10/2026)

Theo `PLAN.md` (sha256 `c3bd7842…`, niêm phong 11:05:33Z) và các sửa đổi A0 (`AMEND-A0.md`), A1 (`PLAN.md` S2,
`build_index_a1.js` niêm phong 11:14:26Z). Node v24.17.0 (máy công ty v20.20.2). Máy cắm sạc. Không sửa mã của gói.

## Kết quả: panel F14 (14 ngày, 84 ô) trùng hoàn toàn

| Gói | Bước | Thời gian | So bằng `confirm_digest.js` |
|---|---|---|---|
| wf20/fullsys | `f14n` | 4,1 phút | 840/840 (tập con F14, N) |
| wf21/n7 | `n14`, `clc14` | khoảng 4 phút | 672/672 (tập con F14, N + CLc) |
| wf20/fullsys | `f14cl` (dùng chỉ mục A1) | 4,8 phút | **1.680/1.680 cả panel F14** |
| wf21/n7 | `cl14`, `pad14`, `padc14` (dùng chỉ mục A1) | 2,1 + 1,9 + 1,9 phút | **1.680/1.680 cả panel F14** |

"Trùng" nghĩa là trùng mọi chỉ số (served, visRev60, late60First, pDisp, breachDeadline60, sumTV5) **và dấu vân tay
từng khách** (`sig`). Không có dòng thiếu, dòng thừa hay lỗi. Mọi bước báo `errors 0`.

Bảng tổng F14 ở mục 5 của `HUONG-DAN-MAY-NHA.md`, tính lại bằng `f14_totals.js` trên `wf21/n7/results/rows.ndjson`: 7/7
nhóm trùng từng số.

| Nhóm | Phục vụ | Đảo hứa | Trễ | Độ dài hứa TB (s) |
|---|---|---|---|---|
| G2 / N / SHRC | 28.784 | 1.307 | 7.146 | 1.588,79 |
| G2 / CL / SHRC | 28.781 | 748 | 6.072 | 1.589,35 |
| G2 / PAD / SHRC | 28.755 | 1.018 | 6.137 | 1.592,17 |
| G2 / N / SHHZ | 29.195 | 1.939 | 7.167 | 1.599,44 |
| INS / N / SHRC | 28.619 | 1.087 | 6.973 | 1.587,23 |
| INS / CL / SHRC | 28.745 | 578 | 5.971 | 1.589,25 |
| INS / PAD / SHRC | 28.562 | 786 | 5.947 | 1.591,12 |

## Hệ quả

- Bộ mô phỏng của công ty chạy lại ở máy nhà, khác phiên bản Node, ra đúng từng khách trên 14/30 ngày của wf20/fullsys và
  wf21/n7. Các số của hai báo cáo đó trên F14 vì vậy đã được tái lập độc lập về tính toán (cùng mã, cùng đầu vào).
- Sửa đổi A1 đạt: chỉ mục dự báo dựng lại từ `index-old-pertarget.json` (111 ngày, 729 lần trùng ngày, 0 xung đột) cho
  kết quả CL/PAD trùng từng khách. Đây là bằng chứng gián tiếp rằng phần của `accuracy-index-2019.json` mà các ngày đích
  F14 dùng giống hệt bản gốc. Tệp dựng lại không phải tệp gốc (sha256 `1fecc00b…`; tệp gốc có 153 ngày).
- Chưa tái lập được: 16 ngày F16 (một nửa 30 ngày), nên mọi số "30 ngày" của báo cáo công ty (−8,90; −13,81; −42,5%;
  −27,46; −9,94; v.v.) mới được máy nhà kiểm trên phần F14. Bước 4 (`ours_vs_theirs.js`) đòi đủ 30 ngày
  (`ours_vs_theirs.js:19`), nên chưa chạy.

## Sửa đổi và lệch kế hoạch

- **A0 (đầu vào):** `PLAN.md` ghi sai rằng mạng FleetPy chỉ cần cho sinh ô F16. Lần chạy `f14n` đầu dừng sau 0,17 giây vì
  thiếu `wf5/ds-nyc-via/scripts/network.js`, không ghi dòng nào (`logs/fullsys-f14n.log`). Đã chép 7 tệp từ cây P5 ở máy
  nhà; băm trùng băm công ty ghi trong `wf15/PREREG-SHADOW-HASH.txt:65-67` và `wf15/DEVIATIONS.md:81-84`.
- **A1 (chỉ mục dự báo):** đúng như `PLAN.md` S2, chỉ chạy sau khi S1 trùng hoàn toàn.
- Không chạy các bước `gate`, `gatecheck`, `check`, `analyze`: chúng cần ô F16 hoặc đủ 30 ngày.
- `f14_totals.js`: bản đầu tính độ dài hứa sai (lấy hiệu hai tổng, ra 0); đã sửa thành `Σ plSumArr_ms / Σ plN`
  (`wf21/n7/code/scripts/shcore.js:6-7`) trước khi ghi bảng trên.

## F16: bị chặn ở máy nhà

`gen17.js` cần, và máy nhà không có (tìm trên C:, E:, Downloads ngày 26/10):
- `wf6/via-many-days/work/factors.json`, hệ số tắc đường TLC-MP15 năm 2019. Bản `factors.json` ở
  `C:/RideBoundContinuation/2026-10-22/emulator-wf5/data/` là năm 2018 (Manhattan), không dùng được.
- `wf6/data/via-many-days/via_mn_2019-09.parquet` và `via_mn_2019-12.parquet`, cùng template trích
  `extract_via_month.sql.tmpl` và `factors_build.js` (không có ở máy nhà).
- DuckDB 1.5.5 (máy nhà có 1.1.3).

Cách mở khóa: (a) chủ mang gói P0 của máy công ty về qua kênh được phép; hoặc (b) mang thẳng 96 ô F16 (292 tệp). Danh
sách băm để kiểm đã có: `expected/wf17-f16-cells.sha256`. Có ô F16 rồi thì chạy tiếp f16n, f16cl, n16 … padc16, các cổng
và `analyze`, khoảng 30 phút.
