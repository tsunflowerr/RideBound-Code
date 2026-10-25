# P7: đối chiếu dữ liệu Via với TLC tải ở máy nhà. Kết quả ngày 25/10/2026

Kiểm toán công ty 23/10, §6-P7 (KT:607,665).

**Dữ liệu tải ở máy nhà** (`E:/RideBoundData/tlc-raw-2026-10-25/`; mã băm, URL và giờ tải trong `DOWNLOAD-LOG.txt`):
- `fhvhv_tripdata_2019-03.parquet` (610.854.382 B, sha `53098177…`);
- `fhvhv_tripdata_2019-09.parquet` (515.944.585 B, sha `ef68aa96…`);
- `taxi+_zone_lookup.csv`;
- DuckDB CLI v1.1.3 (sha `401c36ac…`).

**Phép so.** Dựng lại các chuyến Via trong Manhattan của hai tháng, rồi so bằng EXCEPT ALL hai chiều với
`sp/wf14/work/via_windows.csv` (đầu vào mô phỏng của công ty). Các trường so: ngày, khung, `rid`, giây yêu cầu, khu đón,
khu trả, quãng đường, thời gian chuyến.

**Kết quả:**

| Lần | Quy tắc `rid` | Số dòng csv / TLC | csv − TLC | TLC − csv |
|---|---|---|---|---|
| v1 (`p7_via_windows.sql`) | quy tắc của `extract_via.sql.tmpl` (gói 20/10): sắp theo request, pickup, dropoff, pu, dz, miles, time, fare | 66.856 / 66.856 (3/2019); 51.219 / 51.219 (9/2019) | 33.888 | 33.888 |
| chẩn đoán (`p7_diagnose.sql`) | bỏ cột `rid` | như trên | **0** | **0** |
| v2 (`p7_via_windows_v2.sql`) | sắp theo request_datetime, pu, dz, trip_miles, trip_time (tìm bằng `p7_rid_rule.sql`: khớp 66.856/66.856) | như trên | **0** | **0** |

**Kết luận:**
1. Mọi chuyến trong đầu vào mô phỏng của 2 tháng kiểm (12 ngày, 118.075 chuyến) là chuyến thật của TLC HVFHV (Via,
   HV0004, trong Manhattan), trùng đúng từng trường. Đây là điều §6-P7 cần chứng minh.
2. `rid` của công ty được đánh theo thứ tự **(giờ yêu cầu, khu đón, khu trả, quãng đường, thời gian)**. Thứ tự này khác bản
   `extract_via.sql.tmpl` ở gói 20/10, nghĩa là bước trích của wf6 (`extract_via_all.sh`, không có ở máy nhà) đã dùng quy tắc
   khác.
   - `rid` quyết định nút được gán cho chuyến (seed `via-many-days|node|month|rid`).
   - Muốn dựng lại ô Via đúng từng byte ở giai đoạn 2 thì phải dùng quy tắc v2. Đặc tả của Antigravity chép quy tắc của bản
     20/10, nên sai ở điểm này.
3. Phép này chỉ kiểm 2 trong 10 tháng. Công ty đã kiểm cả 10 tháng giữa parquet trích của họ và csv
   (`wf16/finalize/via_windows_all_months.out.txt`). Phép ở nhà kiểm khâu còn lại: từ TLC thô tới csv, trên 2 tháng.

Lệnh: `duckdb.exe < p7_via_windows_v2.sql`. Đầu ra: `p7_via_windows_v2.out.txt`.
