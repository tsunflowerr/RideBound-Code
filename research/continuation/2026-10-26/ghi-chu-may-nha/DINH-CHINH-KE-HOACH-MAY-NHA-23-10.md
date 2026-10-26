# Đính chính kế hoạch máy nhà 23/10 theo bản kiểm toán cuối (25/10/2026)

Kế hoạch gốc: `E:/RideBoundData/company-intake/2026-10-23/ve-nha-23-10/RideBound-Ve-Nha-Day-Du-23-10/05-Doc-ngay/RideBound-Ke-Hoach-May-Nha-23-10.md`
(KH). Kiểm toán cuối: cùng thư mục, `RideBound-Kiem-Toan-Va-Ke-Hoach-Cung-Co-23-10.md` (KT).

Không sửa tệp gốc. Khi làm theo KH, áp các dòng dưới đây.

| # | KH viết | Sửa thành | Nguồn |
|---|---|---|---|
| 1 | "Hàng rào chặt bị ép xe chỉ vì làm tròn ≤ 5 s"; thêm biến thể EO35 dung sai 5 s (KH:106, 220-222) | Nguyên nhân là **cách ép xe**; làm tròn chỉ giải thích 35–63% (INS) và 38% (G2). Artefact nằm ở **SHF** (B = 0); EO35 hầu như không bị ép (7/1.066 và 10/2.795). Thay biến thể 5 s bằng §6-N1: dung sai 1/5/60 s trên điểm Shadow thật, kèm đường đánh đổi | KT:87-90, 303-306, 669 |
| 2 | "Sửa 3 lỗi sự thật trong LaTeX" (KH:298) | Năm chỗ ở §5.3, cộng các chỗ kiểm toán sót: `09-chuong5.tex:104`; `08-chuong4.tex:778,1310`; cột \|c\| ≥ 1 s; df = 5; Veterans Day | KT:639; R1 mục 5.1.6 |
| 3 | Kết quả Manhattan cũ "đã được kiểm là thật" (KH:304) | Chỉ các bảng chính VERIFIED. Giữ chương thì phải ghi lại F04, F09, F10, F16 và các ô "chưa đếm" của Bảng 4.1 | KT:109, 201, 580 |
| 4 | Hiệu chỉnh SHHZ khớp EOHZ35, "máy công ty x ≈ 72%, 71%" (KH:205) | Công ty hiệu chỉnh SHHZ khớp **EO35**. Nếu đối thủ chính là EOHZ35 (chủ chốt ngày 25/10) thì phải hiệu chỉnh lại x\* theo quy tắc ghi trước, không dùng lại 72/71 | `sp/wf15/work/CALIBRATION-RULE.md:24-34` |
| 5 | Kiểm ô: ma trận thỏa bất đẳng thức tam giác (KH:173-179) | Ô ở chế độ adapter để `closure: 'none'` (`gen_cells_rs.js:104`). Phép kiểm tam giác chỉ dùng cho thí nghiệm W_E; đây là hai hợp đồng khác nhau | Khảo sát 22/10:167-171 |
| 6 | Chỉ số dự báo chuẩn hóa theo trung vị của từng tháng (KH:213) | Chuẩn hóa theo tham chiếu tháng trước. Trung vị cả tháng dùng cả ngày tương lai, tức rò thông tin | wf10 part2:356; Kế hoạch đêm 20/10:190 |
| 7 | Nhánh đệm cho dự báo (KH:216) | Thêm hai điều: (a) hệ số đệm k **đặt trước** trên ngày khám phá; (b) cổng bất biến "Σ\|c\| khách thấy ≤ B ở mọi ô" khi chạy vòng kín | part2:358, 374 |
| 8 | Lưới độ bền: phí chuyển, mật độ, đội xe, trần, dwell, α, x, seed (KH:242-249), "máy công ty đang chạy" | Thêm: kiểu BT (trần tính lúc đặt), Chicago, khung 13–15, ngưỡng trễ 300 s, đăng ký trước lưới α. Kết quả thăm dò a5 (V0–V13) đã có ở `sp/wf16/a5-steering/results/robust_summary.txt`. wf17 (độ bền R1–R7) còn ở máy công ty | KT:611, 613, 458-482; transcript-late:208, 400 |
| 9 | Ô 200–256 yêu cầu (KH:109-111) | Hợp đồng v1 dừng ở 128 yêu cầu và 96 nút. Đóng việc này bằng điều kiện dừng có ghi rõ; không nâng lõi chỉ để vượt cỡ | KH:111; ADR-082 |
| 10 | Cầu nối Via P128 → RideBound (KH:181) | Không ưu tiên: một ô 128 yêu cầu có khoảng 256 điểm đầu cuối, vượt trần 96 nút; P128 cũng "gần như không thấy hiệu ứng". Thay bằng P5 (đã chạy ở nhà) | part1:371, 480; R2 5.3.5 |
| 11 | Đợt 3 dùng 02/01–06/03/2020 (KH:258) | Giai đoạn này chỉ có 45 ngày thường sạch. Kiểm toán đề xuất khoảng 91 ngày 2019 còn lại. **Chờ chủ quyết.** Dù chọn đợt nào cũng phải loại: 2019-10-22, 2019-12-06, tháng 11/2019, mọi ngày đã đụng, 8 ngày wf7; và phải phân tầng ngày nghỉ học | KT:680; transcript-late:137-138, 392 |
| 12 | Không làm A1/A2 (KH:111) | Đảo chỉ đạo ngày 21/10 của chủ ("viết lại phần lõi không sao đâu"). Cần chủ xác nhận | extract-full:2390 |
| 13 | Thiếu hẳn trong KH | Bổ sung: §6-P7 (đối chiếu TLC, máy nhà đang làm); §8.2 (sinh viên tự kiểm); §6-N10 (kiểm tính mới, email tác giả Elmi); §6-N11 (đuôi, \|A − p0\|); mục "Tính chất của luật C" (chứng minh c ≥ 0 có điều kiện, BBP) | KT:607, 683-684, 760-770; R2 5.3.2-3 |
| 14 | Đối thủ chính "Elmi + HZ" (KH:24, 31) | **Đã chốt ngày 25/10:** EOHZ35/SHHZ chính, luôn báo kèm EO35/SHF | ADR-082 |

## Cập nhật 26/10 theo phản hồi của máy công ty

Máy công ty cho biết: 13 trích dẫn "KH:dòng" ở trên khớp từng chữ với bản kế hoạch **trước** wf18, tức bản chép ngày
23/10 khoảng 15:50. Kế hoạch đã được sửa ngày 24/10 (wf18 sửa 74/75 lỗi, thêm mục 0.4). Máy nhà **chưa có** bản 24/10,
nên trạng thái dưới đây là của máy công ty, chưa đối chiếu ở nhà:

- **Đã sửa trong kế hoạch 24/10:** #3, #4, #6, #9, #10.
- **Sửa một phần:**
  - #1: KH:190 vẫn đề xuất biến thể EO35 dung sai 5 s; nên bỏ.
  - #2: các chỗ máy nhà thêm chưa có trong kế hoạch: `08-chuong4.tex:778, 1310`, df = 5, Veterans Day.
  - #7: còn thiếu cổng Σ|c| ≤ B khi chạy vòng kín.
  - #8: BT, Chicago, khung 13–15 giờ, ngưỡng 300 s chưa máy nào chạy; ghi là giới hạn.
  - #13: còn thiếu N10 và mục "Tính chất của luật C".
- **#5 của bản này là sai:** `closure: 'none'` chỉ tắt bước Floyd–Warshall lúc chạy. Ma trận vẫn phải qua phép kiểm tam
  giác (`scenario.js:246-248`), và các ô Via đã qua với 0 vi phạm. Giữ phép kiểm.
- **#11:** "khoảng 91 ngày 2019" là sai; đúng là **75 ngày** (đếm lại ở nhà, xem `MAU-DANG-KY-DOT-3.md` §3).
- **#11, #12, #14:** chủ đã quyết hoặc sẽ quyết; ghi quyết định vào kế hoạch.
