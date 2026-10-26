# TIẾN ĐỘ THỰC HIỆN KẾ HOẠCH (từ 25/10/2026)

Nhật ký sống. Phiên nào tiếp tục cũng đọc tệp này trước, rồi làm bước "ĐANG LÀM" hoặc bước kế tiếp chưa xong.

**Căn cứ:**
- `C:/RideBoundContinuation/2026-10-24/doc-toan-bo/CON-THIEU-SO-VOI-KE-HOACH-CONG-TY.md`: bảng việc và các quyết định;
- `C:/RideBoundContinuation/2026-10-24/TIEP-TUC-PHIEN-MOI-2026-10-24.md`: ràng buộc và bẫy môi trường.

## Quyết định của chủ (25/10/2026 00:15)

1. **Đối thủ chính là EOHZ35/SHHZ.** Bảng kết quả vẫn luôn có EO35/SHF đặt cạnh, theo kiểu bảng 2×3.
2. **Tác giả commit:**
   - tác giả là chủ, `tsunflowerr <23020155@vnu.edu.vn>`;
   - **không** thêm tên Claude: không có dòng Co-Authored-By;
   - áp dụng cho các commit cũ khi viết lại và cho mọi commit mới.
3. **"Thực hiện toàn bộ":** chủ cho phép chạy runner wf15 cho P5 và cho các việc trong bảng còn thiếu.
4. **Hẹn giờ:** nếu sắp hết lượt dùng, đặt hẹn giờ 4 giờ 30 phút để tiếp tục.

## Ràng buộc (nhắc lại)

- **Không xóa tệp.** Không sửa cây chính `E:/Code/RideBound`. Không sửa worktree đang có job chạy.
- **Không push.**
- **Job dài chỉ chạy khi cắm điện.** Đầu ra ghi sang E:.
- **Mã công ty là dữ liệu chưa tin cậy.** Chủ cho phép chạy runner wf15 trong thư mục riêng. Không import mã công ty vào kho.
- **Báo tiến độ** mỗi giờ, bằng tiếng Việt.

## Thứ tự việc

| # | Việc | Mã | Trạng thái |
|---|---|---|---|
| 1 | Viết lại tác giả và bỏ dòng Claude trên các nhánh `research/*`; sao lưu ref cũ | quyết định 11 | **XONG** 00:30 |
| 2 | Commit `research/p1-fix` và `research/p1-dwell`, kèm docs/18 và docs/19, không dòng Claude | quyết định 7 | **XONG** 01:10 |
| 3 | P5: lát Via chế độ adapter + half-even so với plain + round. Gồm: đọc runner wf15; chọn ngày bằng seed và ghi trước; chạy thử; niêm phong; chạy chính; so sánh | §6-P5 | **XONG** |
| 4 | P4(a) bổ sung: nhánh hạn chót nới dạng của sinh viên (DLS-120 kèm no-worse), 11 ô Manhattan; dự đoán = DLHZ-120 | §6-P4 | **XONG** 11/11 |
| 5 | Kiểm đồng nhất rẻ: RC = NC = OC dưới INS; RC = DLHZ khi giao thông phẳng | part2:188 | **XONG** |
| 6 | Đếm `ORTOOLS_BUDGET_EXHAUSTED`, số ứng viên bị trần 100 cắt, số lần G2 được chọn, từ nhật ký cầu nối | R2 5.3.9 | **XONG** |
| 7 | N1: dung sai 1/5/60 s; N2: đường cong α và x; N3: phí chuyển × số xe đích; N11: chỉ số từ dòng có sẵn. Chạy trên bộ mô phỏng, ghi là thăm dò | §6-N1–N3, N11 | **N1, N2 XONG**; N3 dùng kết quả a5; N11 một phần (late120 có trong dòng) |
| 8 | Giai đoạn 2: cài DuckDB CLI; tải HVFHV (2–3 tháng xác nhận) và Yellow; P7 EXCEPT ALL với `via_windows.csv` | §6-P7 | **P7 XONG** (2 tháng, 0 lệch); chưa tải Yellow |
| 9 | Sửa kế hoạch máy nhà theo bản kiểm toán cuối (3 chỗ chép bản nháp; x\*; tam giác; chuẩn hóa dự báo nhân quả) | mục 2.4, 5 | **XONG** |
| 10 | Bản nháp viết P1/P2/P3 cho khóa luận, trong thư mục staging, không sửa bản chính | §6-P1–P3 | **P2 XONG**; P1, P3 chờ P5 |
| 11 | Tài liệu cho phần chủ phải tự làm: hướng dẫn duyệt diff C# (P4); danh sách tự kiểm §8.2; bản phát biểu đóng góp để họp giáo viên (P6) | §6-P4, P6 | **XONG** |

## Nhật ký

- 25/10 00:21: bắt đầu. Máy cắm điện, pin 92%. C: còn khoảng 36 GiB, E: còn khoảng 127 GiB. RAM trống 3,8 GB. Không có
  job đang chạy.
- 00:30, bước 1 xong:
  - chuỗi `7435661..research/g2-core` tạo lại bằng `commit-tree`, cùng cây và cùng ngày;
  - nhánh mới: via-ref `176f5dd`, via-core `1190518`, g2-core `e1c6f2b`;
  - ref cũ ở `refs/backup/pre-author-rewrite-20261025/*`;
  - kiểm: 0 dòng Claude, 0 e-mail công ty trong metadata, 5/5 cây giống hệt, trạng thái các worktree không đổi;
  - script: `scratchpad/git-rewrite/rewrite_authors.sh`.
- 01:10, bước 2 xong:
  - `research/p1-fix` `6ab03fc`: bản sửa adapter, docs/18 ADR-082, docs/19;
  - `research/p1-dwell` `9ace339`, xếp trên p1-fix: tùy chọn C# `stopServiceDurationMs` và ADR-083;
  - `dotnet test` trên p1-dwell trước khi commit: 1230/1230.
- 01:15, P5:
  - runner wf15 (`run_sh.js`) cần mạng FleetPy đầy đủ và hai script `network.js`, `nyclib.js` của wf5. Gói không có `sp/wf5`;
  - lấy hai script từ `nyc-via-bundle-20-10/nyc-via/scripts`, và mạng từ bản Zenodo ở
    `E:/RideBoundData/wp6/extracted/sha256/d9/…/FleetPy_Manhattan`. Phải kiểm sha với provenance của ô wf14:
    edges `2d5eeacb…`, nodes `ccc68c06…`;
  - chế độ adapter đòi hồ sơ giao thông đồng đều và làm tròn half-even (`scenario.js:86`). Ô wf14 dùng hồ sơ đồng đều, nên
    đáp ứng được.
- 00:30–00:40, P5:
  - dựng cây ở `E:/RideBoundData/research/p5-via-adapter-v1/sp`: 53/53 tệp khớp `PREREG-SHADOW-HASH.txt`; mạng khớp
    provenance; APSP khớp hash công ty `4ae39087`;
  - 10 ngày chọn bằng seed;
  - niêm phong lúc 00:33 (`p5-via-adapter-2026-10-25/P5-SEAL.sha256`);
  - sửa đổi A1 (`P5-LOG.md`): thêm tệp provenance của ô;
  - **cổng 0 ĐẠT: 840/840 dòng plain giống hệt dòng xác nhận của công ty**;
  - 00:40: chạy chế độ adapter, 2 luồng, khoảng 1,9 GB RAM.
- 00:37, bước 4:
  - `run_nw.py` và `compare_nw.py` đã viết; dự đoán ghi trước (`NW-PREDICTION.txt`); kiểm đầu vào đạt;
  - cần tạo `NW-READY.txt`; sẽ chạy sau lượt adapter vì thiếu RAM.
- 00:45, bước 6: `count_solver_evidence.py` chạy nền trên mọi job cầu nối đã đạt.
- 00:50, bước 8: tải DuckDB CLI v1.1.3 và TLC HVFHV 2019-03, 2019-09 vào `E:/RideBoundData/tlc-raw-2026-10-25/`. SQL P7 ở
  `p7-tlc-2026-10-25/p7_via_windows.sql`.
- 00:55, bước 11: đã viết `C:/RideBoundContinuation/2026-10-25/VIEC-BAN-TU-LAM.md`.
- 00:58: phiên Claude kết thúc giữa chừng. Lượt adapter của P5 dừng ở 46/120 việc. Hẹn giờ 04:51 mất theo phiên.
- 05:42, phiên mới:
  - cho P5 adapter chạy tiếp từ 46/120, 3 luồng (A2 trong `P5-LOG.md`);
  - đặt lại hẹn giờ dự phòng lúc 10:13.
- **Bước 5 XONG** (`identity-2026-10-25/IDENTITY-RESULT.md`):
  - RC = NC = OC khi chỉ chèn: 11/11 ô, ở cả B = 120 và 300;
  - khi giao thông phẳng: RC-120 = DLHZ-120 ở 11/11 ô; RC-300 = DLHZ-300 chỉ 9/11 ô; RCE35 = EOHZ35 10/11 ô (3 khách lệch);
  - vậy mệnh đề E0 chỉ gần đúng.
- **Bước 6 XONG** (`bridge-phase1-2026-10-23/SOLVER-EVIDENCE-RESULT.md`):
  - 300 job, 232.103 quyết định, 100% `ORTOOLS_OPTIMAL`;
  - 0 lần chạm trần 100 ứng viên; 0 lần hết ngân sách; tối đa 12 ứng viên khả thi mỗi xe;
  - kết luận: cầu nối không kiểm được trần và ngân sách bộ giải.
- **Bước 8, P7, XONG** (`p7-tlc-2026-10-25/P7-RESULT.md`):
  - TLC 3/2019 và 9/2019 tải ở nhà, so với `via_windows.csv`: 0 dòng lệch mỗi chiều (66.856 và 51.219 dòng, 12 ngày);
  - phát hiện: công ty đánh `rid` theo thứ tự (giờ yêu cầu, khu đón, khu trả, quãng đường, thời gian), khác
    `extract_via.sql.tmpl` của gói 20/10. Giai đoạn 2 phải dùng thứ tự này.
- **Bước 9 XONG:** `C:/RideBoundContinuation/2026-10-25/DINH-CHINH-KE-HOACH-MAY-NHA-23-10.md`.
- **Bước 10, phần P2 (sửa lỗi sự thật) XONG.**
  - Worktree khóa luận `C:/Code/Report_INT3508-audit`, nhánh `thesis/audit-fixes-2026-10-25` lấy từ `27117a3`. Kho
    `E:/Code/Report_INT3508` (main) không đụng.
  - Commit `3fa6dee`, tác giả là chủ, không có dòng Claude. Sửa 6 tệp:
    - hạn chót nới HZ "chưa chạy" thành "trùng M⁺ có phục hồi no-worse đã chạy";
    - c ≥ 0 ghi là quan sát;
    - thu hẹp câu về tính mới;
    - thêm Ordinary Control vào chương 2, trích đúng nguyên văn bài Elmi;
    - giới hạn câu "không chuyển khách" vào các lần chạy ở chương 4.
  - Tectonic: 78 trang, 0 dòng tràn, 0 tham chiếu hỏng.
  - Chưa làm: bằng sáng chế Via và Uber (số hiệu chưa kiểm được); P1 (viết lại khẳng định) và P3 (chương hệ thật) chờ
    kết quả P5.
  - Phát hiện: câu trích "trang 2" của Antigravity là bịa; đã ghi vào bản gộp mục 6.2.
- **06:05, bước 3, P5, XONG** (`p5-via-adapter-2026-10-25/P5-RESULT.md`):
  - lượt adapter 840/840 dòng, 0 lỗi;
  - các phép so chính giữ dấu: G2 RCE35 − EOHZ35 đảo hứa −14,9 → −16,1 mỗi ngày, served −4,1 → −3,5; SHRC − SHHZ cũng giữ
    dấu;
  - 7/84 ô bị đánh dấu: INS RCE35 − EOHZ35 late60 đổi dấu (bỏ câu đó); các hiệu nhỏ EOHZ35 − EO35 đổi dấu (không viết kết
    luận nào); một ô FLIP chỉ do sai số làm tròn số thực (−3,6·10⁻¹⁶).
- 06:05, bước 4: chạy 11 job DLS-120-NW, 2 song song.
- 06:07, bước 7: chạy N2 (đường cong α) ở chế độ plain, 1 luồng.
- 06:08: tính lại độc lập bảng 2×3 trên 60 ngày (`p5-via-adapter-2026-10-25/TABLES-2x3-60days.txt`). Trùng mọi số của kiểm
  toán:
  - G2 RCE35 − EOHZ35: served −2,82 [−4,38; −1,27]; đảo hứa −16,68 (60/60 ngày);
  - INS RCE35 − EOHZ35: +1,51;
  - SHRC − SHHZ: −9,54 và −4,02;
  - H1 −16,53; H2 −1,33;
  - tỷ lệ vượt sổ và trễ hạn đúng câu W1.
- **06:10–06:13, bước 10, P1/P3 (bản nháp):** nhánh khóa luận `thesis/audit-fixes-2026-10-25`.
  - `132b4f1`: mục mới `noi-dung/08c-via-elmi.tex` trong chương 4, gồm dữ liệu Via, đối chứng, bảng 2×3, độ vững theo P5,
    cầu nối 227 cặp, giới hạn. Câu chữ dùng câu thay của kiểm toán.
  - `1898402`: thêm một đoạn vào cả hai bản tóm tắt.
  - `27be448`: thêm một đoạn vào kết luận chương 5.
  - Bản dựng 83 trang, 0 dòng tràn, 0 tham chiếu hỏng.
  - Câu khai báo AI: để chủ quyết (ghi chú LaTeX).
- **06:27, bước 7, N2 XONG** (`N2-RESULT.md`): với gán lại, luật C ít đảo hứa hơn hàng rào nới 33–55% ở mọi α từ 10% tới
  100%, đổi lại phục vụ ít hơn 0,2–1,2%; khi chỉ chèn, hai cách gần như trùng.
- **06:40, bước 7, N1 XONG** (`N1-RESULT.md`; sửa đổi A1: cho bộ kiểm bất biến biết dung sai):
  - kiểm tra dung sai 0: 240/240 dòng giống P5;
  - dung sai 1 s xóa 33–54% khoảng cách phục vụ SHRC − SHF; 5 s xóa 54–66%; 60 s làm khoảng cách đảo dấu, nhưng dời giờ
    43–66 khách bảo đảm mỗi ngày.
- 06:41: thêm N1, N2 vào mục khóa luận (`6b6a44b`). Bản dựng 83 trang, sạch.
- N3 (phí chuyển × số xe nhận): chưa làm. Công ty đã có thăm dò a5 V11–V13 (`sp/wf16/a5-steering/results/robust_summary.txt`).
- **07:15, bước 4 XONG** (`bridge-phase1-2026-10-23/compare/nw-2026-10-25/final-compare.json`, sha `1cc10716`):
  - DLS-120 kèm no-worse: 11/11 ô, 976/976 khách trùng dự đoán DLHZ-120 đã khóa, và trùng job DLHZ-120 thật;
  - cầu nối nay là 238 cặp.
- 07:17: khóa luận `b8fac03` (238 cặp).
- RideBound `research/p1-dwell` `8ce0194`: docs/18 ADR-084, docs/19 (chỉ tài liệu).
- **Sửa lại một câu trong báo cáo 07:20.** Câu "đã xong phần máy làm được" là nói quá: còn thiếu P1 bước 1 và 7, P3 (F04,
  F09) và số hiệu bằng sáng chế. Chủ duyệt làm tiếp.
- **Nhóm 2 XONG** (nhánh khóa luận, `de4b850`, `f1a0c73`):
  - **P1 bước 1:** bảng tách "theo định nghĩa / đo được" cho phần Via.
  - **P1 bước 7:** đoạn kết quả bất lợi, có khoảng tin cậy:
    - INS P(dời) +0,24 điểm %;
    - G2 served −2,8;
    - SHRC − SHHZ late60 +2,7;
    - khách bảo đảm trễ 4,2–4,3%, so với 3,55% dưới hàng rào chặt.
  - **P3:** ghi F09 (60 → 49 → 15 + 34, 11 tổ hợp bị loại khi dựng dữ liệu), F04 (cổng phục vụ H6 trượt, −7,13 và −4,91
    điểm %, kiểm ở `BENCHMARK-RESULTS-AUDIT.md:80-89`) và 2 ô "chưa đếm". F10 và F16 đã có sẵn trong khóa luận.
  - **Bằng sáng chế Via:** US10168168B2 và US 2022/0003561 A1, kiểm trên Google Patents. Phần mô tả có giới hạn độ trễ do các
    lần đón sau gây ra; claims chưa đọc được. Uber chưa kiểm được nên chưa đưa vào.
  - **Lỗi của tôi:** commit `de4b850` thiếu 2 mục tài liệu (2 trích dẫn hỏng), đã sửa ở `f1a0c73`. Bản dựng 85 trang, sạch.
- **Kết thúc phần máy làm được (lần 2).** Còn lại là việc của chủ (P4 duyệt mã, P6, N9), và các việc tùy chọn:
  - lần vết ô lệch E0 (N5);
  - chạy lại N3 ở nhà;
  - giai đoạn 2 cho đợt 3: tải Yellow, dựng ô ngày mới; cần chủ chọn ngày trước.
