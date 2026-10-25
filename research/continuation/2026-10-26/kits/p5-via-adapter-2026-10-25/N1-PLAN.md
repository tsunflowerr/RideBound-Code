# N1: dung sai của hàng rào chặt trên điểm Shadow thật. Thăm dò; thiết kế ghi trước khi chạy (25/10/2026)

Kiểm toán §6-N1 (KT:608,669): chạy SHF và EO35 với dung sai 1 s, 5 s và 60 s, trên điểm Shadow thật, rồi báo phần còn lại
của khoảng cách SHRC − SHF.

**Khác với kiểm toán:**
- Kiểm toán muốn chạy trên 55 ngày khám phá, nhưng các ô khám phá (wf6) không có ở máy nhà. Ở đây dùng 60 ô của P5 (10
  ngày xác nhận, đã xem); ghi là thăm dò.
- Bộ chạy `a3/fence_tol.js` của kiểm toán dùng phân bổ Shadow ngẫu nhiên (π = 0,68). Ở đây dùng điểm Shadow thật và
  ngưỡng đóng băng x\* = 72/71, qua `run_sh.js`.

**Mã:** cây riêng `E:/RideBoundData/research/p5-via-adapter-v1/sp-tol/`.
- `wf15/runner/engine.js` là `sp/wf16/a3-code-emulator/engine_tol.js` của kiểm toán (bản gốc giữ ở
  `engine_tol.company.js`), với 3 thay đổi:
  - sửa đường dẫn `identity.js` thành `./identity.js`;
  - TOL đọc từ biến môi trường `FENCE_TOL_MS`, mặc định 0;
  - dòng `TOL = opts.fenceTolMs || 0` giữ ưu tiên cho `opts.fenceTolMs` nếu có, còn không thì lấy biến môi trường.
- Các tệp khác chép nguyên từ cây P5.

**Lượt chạy:** chế độ plain, 60 ô, cấu hình INS và G2, nhánh EO35 và SHF ở x\* đóng băng. Bốn lượt:
- `FENCE_TOL_MS` = 0: **kiểm tra**. Mọi dòng phải giống hệt dòng EO35/SHF của P5 plain, trừ `part` và `ms`. Không giống thì
  dừng.
- `FENCE_TOL_MS` = 1000, 5000, 60000.

**Sửa đổi A1 (06:28).** Lượt kiểm tra dung sai 0 đạt: 240/240 dòng giống hệt P5 plain. Nhưng các lượt có dung sai hỏng
ở mọi việc: bộ kiểm bất biến của `shcore.js:89` vẫn kiểm `p ≤ p0 + bSH` của luật gốc, trong khi dung sai đã nới luật đó
đúng như thiết kế.
- Sửa trong cây riêng: dòng 89 cộng thêm `FENCE_TOL_MS` (bản gốc giữ ở `shcore.company.js`).
- Các lượt có dung sai chạy lại vào `rows/n1b-tol*.ndjson`. Tệp của lượt hỏng (`n1-tol1000`, `n1-tol5000`, `n1-tol60000`)
  được giữ nguyên.

**Báo cáo:**
- Với mỗi dung sai: served và visRev60 theo ngày (cách tính như P5).
- Phần còn lại của khoảng cách SHRC − SHF và EOHZ35 − EO35, so với dòng P5 plain của SHRC, SHHZ, EOHZ35.
- Số khách bảo đảm bị dời (`G_dispAny1`, nếu có trong dòng).
