# F16 trên hệ thật: kế hoạch (05/11/2026, viết và băm trước khi tạo dự đoán)

Chủ hỏi ngày 05/11: "Tôi tính F14+F16 trên hệ thật rồi so sánh với bộ mô phỏng kia chứ?". 168 job trên máy thuê chỉ chạy F14
(84 ô), vì lúc dựng gói máy nhà chưa có ô F16. Ô F16 nay đã có (zip wf17 ngày 24/10, 292/292 tệp đúng băm).

## Mục tiêu

- Chạy RideBound thật trên 96 ô F16 (16 ngày × 2 khung × 3 lần lấy mẫu) × 2 nhánh, tổng cộng 192 job:
  - `FS-G2-SHRC71-CL` (của mình);
  - `FS-G2-SHHZ71-N` (Elmi).
- So từng khách với dự đoán chế độ adapter đã khóa, theo 4 mặt như F14: khách, chuỗi giờ trả, Shadow, ETA theo thời gian.
- Gộp với F14 thành 30 ngày hệ thật.

## Giữ nguyên so với F14

- Runner, adapter và công cụ trong gói `final-v2`: commit xuất `0eb9a6ca`. Không build lại.
- Bộ chuyển ô `convert_via_full_v2.py`, `make_run_drivers.py` (`complete-graph-direct-v1`, `gzip-v1`), `make_job_configs.js`,
  `make_job_configs_v2.py`, và các công cụ so (`compare_fullsys.py`, `compare_drop_sequences.py`, `compare_shadow.py`,
  `eta_metrics.py`, `reversal_counts.py`, `final_report_linux.py` qua tham số).
- Các tệp dùng chung không đổi:
  - tham số Shadow đóng băng (`wf15/frozen/shadow-params.json`);
  - chỉ mục dự báo dựng lại A1 (`wf7/c-forecast/results/accuracy-index-2019.json`). Phép chạy lại bộ mô phỏng ngày
    05/11 đã trùng từng khách trên F16, cả ở chế độ CL;
  - mạng Shadow.

## Khác so với F14 (chỉ có ba chỗ)

Ba bản sao `emulate_fullsys_f16.js`, `emulate_sequences_f16.js` và `emulate_timed2_f16.js` được tạo bằng phép thay chuỗi có
đếm (`make_f16_scripts.py`). Mỗi bản chỉ đổi:

1. thư mục ô: `wf17/r1-identity-fence-pad/cells/f16/weekdays-P512`;
2. panel F16 (`PANELS.F16`);
3. tệp prior λ: `wf20/fullsys/work/prior_F16/prior_prevhour.csv`. Đây chính là tệp `RUNBOOK.sh` của máy công ty dùng cho
   F16, vì tệp prior của wf15 không có ngày F16.

Riêng `emulate_timed2_f16.js` đổi thêm đường dẫn tới hai tệp đã khóa của F16. Bản F16 dựa trên `emulate_timed2.js`, vì đó là
bản có trong `FULLSYS-F14-TIMED.sha256`.

## Thứ tự và cổng

1. **Cổng plain F16.** `emulate_fullsys_f16.js plain`: 192/192 cặp ô-nhánh, cả hai engine, dấu vân tay từng khách phải bằng
   dòng của máy công ty. Không đạt thì dừng.
2. **Chuyển 96 ô** thành fixture, world và driver chạy.
3. **Dự đoán chế độ adapter**, khóa từng bước:
   - `emulate_fullsys_f16.js adapter` → `predictions/fullsys-f16-predictions.tsv` và `shadow-inputs/`, khóa
     `FULLSYS-F16-PREDICTIONS.sha256`;
   - `emulate_sequences_f16.js` → khóa `FULLSYS-F16-SEQUENCES.sha256`;
   - `emulate_timed2_f16.js adapter` với `TIMED_GZIP=1`, **có cả hai nhánh tách** (SHHZ71-CL, SHRC71-N) để dùng cho D1 →
     khóa `FULLSYS-F16-TIMED.sha256`.
   - Mọi tệp khóa xong trước job F16 đầu tiên.
4. **Cấu hình job:** `make_job_configs.js fullsys`, sau đó `make_job_configs_v2.py` với adapter của `final-v2`. Bản Linux chỉ
   đổi `networkDirectory`. Danh sách job: 192 job F16 và 4 job cổng (2 ô × 2 nhánh).
5. **Máy thuê** (chính máy vast đang chạy F14), gói bổ sung riêng:
   - trước hết chạy 4 job cổng, so 4 mặt;
   - khác dù 1 khách thì dừng, không chạy 192 job;
   - đạt thì chạy 192 job, rồi `final_report_linux.py` với các tệp F16.
6. **Tổng hợp:** F14 (84 ô) + F16 (96 ô) = 180 ô. Mỗi ngày = tổng / 90 (F16 riêng: / 48). So hệ thật với dự đoán adapter. Sau
   đó D1 dùng bảng 4 nhánh của bộ mô phỏng để đo khoảng cách giữa chế độ adapter và chế độ plain.

## Tiêu chí đạt

- `allExact` = true trên 192 cặp ô-nhánh: khách, chuỗi giờ trả, Shadow và ETA theo thời gian đều trùng.
- Mọi job `status-pass`.

## Giới hạn ghi trước

- Hệ thật chỉ có 2 nhánh, nên chỉ cho hiệu tổng. Phần tách giữa luật C và dự báo vẫn chỉ có từ bộ mô phỏng.
- Chế độ adapter không phải chế độ plain của báo cáo; khoảng cách giữa hai chế độ được đo ở bước D1.
- Ô F16 lấy từ zip, chưa sinh lại từ dữ liệu gốc (thiếu `factors.json` 2019).
