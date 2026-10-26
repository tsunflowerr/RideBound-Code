# wf22-home: chạy lại gói `company-emulator` ở máy nhà (26/10/2026)

Nhãn: thăm dò sau kiểm toán (ngày đã xem; giả thuyết do AI viết; chỉ trong bộ mô phỏng). Kế hoạch này ghi và băm sha256
TRƯỚC mọi lượt chạy. Không sửa mã của gói; không ghi đè đầu ra cũ; không xóa gì.

## Đầu vào

- SP = `E:/RideBoundData/company-intake/2026-10-23/sp` (bản chép 23/10).
- Mã của gói: nhánh `research/company-emulator-2026-10-26` @ `21af556`, thư mục `company-emulator/sp/{wf17,wf20,wf21}`,
  chép vào SP; đã kiểm trùng từng byte (băm gộp 17518a64…, 7b982cbd…, f441c656…).
- `check_inputs.js`: 10/19. Thiếu: `wf7/c-forecast/results/accuracy-index-2019.json` (P2) và toàn bộ phần sinh ô F16 (P0:
  mạng FleetPy theo đường dẫn của SP, `factors.json`, `via_mn_2019-09/12.parquet`, DuckDB 1.5.5; `nyclib.js`, `network.js`,
  `ff_apsp_u16.bin` có ở cây P5 nhưng không ở SP).
- Node v24.17.0 (máy công ty v20.20.2). Nếu lệch, kiểm phiên bản trước tiên.

## Tiêu chí trùng

`tools/confirm_digest.js` báo N/N dòng "trùng hoàn toàn (kể cả từng khách)". Không có ngoại lệ.

## Các bước

**S1 (không cần chỉ mục cũ).** Theo RUNBOOK, mỗi lúc một RUNBOOK:
1. `wf20/fullsys/RUNBOOK.sh f14n`;
2. `wf21/n7/RUNBOOK.sh n14`, rồi `clc14` (chỉ mục nhân quả có trong gói).

So bằng tập con của bản mong đợi (panel F14, đúng các chế độ đã chạy), lọc bằng `wf22-home/filter_expected.js` (chỉ lọc
dòng, không đổi nội dung), rồi `confirm_digest.js`.

**S2 (sửa đổi A1, chỉ chạy nếu S1 trùng hoàn toàn).** Dựng `SP/wf7/c-forecast/results/accuracy-index-2019.json` bằng hợp
của `targets[d].F` trong `wf21/n7/cidx/results/index-old-pertarget.json` (quy tắc ghi trong tệp: "wf7
accuracy-index-2019.json restricted to [d-28, d-1]").
- Điều kiện: mọi ngày xuất hiện ở nhiều đích phải có mảng giống hệt nhau; nếu không thì dừng.
- Lý do đủ: `forecast.profile(day, s0, 'prev4', F)` chỉ đọc F của 4 ngày cùng thứ trong 4 tuần trước ngày đích
  (`forecast.js`, hàm `sources`), tất cả nằm trong [d-28, d-1].
- Đây KHÔNG phải tệp gốc. Nó chỉ đúng cho các ngày đích F14/F16. Tính đúng được kiểm bằng phép so từng khách.

Sau đó:
1. `wf20/fullsys/RUNBOOK.sh f14cl`;
2. `wf21/n7/RUNBOOK.sh cl14`, `pad14`, `padc14`.

So đủ panel F14: `confirm_digest.js expected/wf20__fullsys__results__rows.tsv … --panel F14` (mong đợi 1.680/1.680) và
`expected/wf21__n7__results__rows.tsv … --panel F14` (mong đợi 1.680/1.680). Đối chiếu thêm bảng tổng F14 ở mục 5 của
`HUONG-DAN-MAY-NHA.md`.

**Không làm được ở máy nhà lúc này:** sinh 96 ô F16 và mọi lượt F16 (thiếu P0), các bước `gate`/`gatecheck`/`check` (cần ô
F16 hoặc tệp hiệu chỉnh), nên phần "đủ 30 ngày" chờ có P0.
