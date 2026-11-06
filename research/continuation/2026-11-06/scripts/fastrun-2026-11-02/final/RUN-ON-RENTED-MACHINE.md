# Chạy thật lần cuối trên máy thuê (ADR-089 GĐ5/GĐ6)

Viết cho: chủ khóa luận, người thuê máy và bấm chạy. Số đo ở mục 7 là số đo thật trên máy nhà ngày 04/11/2026.

## 1. Chạy cái gì

168 job = 84 ô F14 (14 ngày × 2 ca × 3 mẫu, mỗi ô 512 khách) × 2 nhánh:

| Nhánh | Ý nghĩa |
|---|---|
| `FS-G2-SHRC71-CL` | hệ của mình: G2 + Shadow + luật C + dự báo vòng kín |
| `FS-G2-SHHZ71-N` | Elmi: G2 + Shadow + hàng rào nới, không dự báo |

Kết quả từng khách phải trùng dự đoán đã khóa trước (sha256 trong `fullsys/FULLSYS-F14-*.sha256`, khóa trước mọi job).

## 2. Thuê máy gì

- **Windows** x64: Windows Server 2025 hoặc Windows 11 24H2 (cùng thế hệ với máy nhà, Windows 11 build 26200). Không dùng
  Linux: gói chạy đúng các đường dẫn `E:\...`, cùng Python, .NET, Runner như ở nhà, nên mọi khung giao thức phải giống từng
  byte; bước 3 kiểm điều đó. Hàm toán `log/exp/cos` của Python đi qua thư viện C của Windows, nên khác thế hệ Windows có thể
  lệch bit cuối — bước 3 sẽ bắt và dừng.
- Số job song song W: RAM ≥ W × 7 GB + 4 GB; số nhân ≥ W (mỗi job ~1 nhân).
- Đĩa dữ liệu (nơi giải nén) ≈ 12 GB + 0,8 GB × W, cộng 2 GB cho file zip (để ở ổ khác cũng được).
  W = 8 → khoảng 18 GB (+2 GB zip). Giữ dưới 20 GB/ổ thì W ≤ 10.
- Gợi ý: 16 vCPU, 64–128 GB RAM, đĩa dữ liệu 40–64 GB, chạy `-Workers 8`.

## 3. Các bước

1. Cài 2 thứ (một lần):
   ```
   winget install --id Git.Git -e
   winget install --id Microsoft.VCRedist.2015+.x64 -e
   ```
2. Chép lên máy, cùng một thư mục: `ridebound-final-v2.zip`, `MANIFEST.tsv`, `run_cloud.ps1`.
3. Mở PowerShell trong thư mục đó và chạy:
   ```
   powershell -ExecutionPolicy Bypass -File run_cloud.ps1 -Unpack D:\E -Workers 8
   ```
   Nếu máy đã có ổ E: thì giải nén thẳng vào E:\ : `-Unpack E:\`.
   Thư mục giải nén nên ngắn (như `D:\E`): đường dẫn dài nhất trong gói là 216 ký tự, Windows dừng ở 260. Script giải nén
   bằng .NET, không dùng `tar.exe` (tar của Windows bỏ qua một tệp có tên tiếng Việt và báo lỗi — đã thử 04/11). Nếu bước giải
   nén bị ngắt giữa chừng, xóa thư mục giải nén dở rồi chạy lại.
4. Máy tắt giữa chừng: chạy lại đúng lệnh trên. Job đã xong được bỏ qua; job dở được giữ dưới tên `<job>.interrupted-k`.
   Muốn ngừng nhận job mới: tạo file `E:\runs\STOP`.
5. Mang về: thư mục `E:\runs` (bằng chứng từng job, log, `gates-report`, `report`).

## 4. Script làm gì (dừng ngay khi một bước sai)

| Bước | Việc | Điều kiện đi tiếp |
|---|---|---|
| 0 | có Git và VC++ runtime | có |
| 1 | giải nén, `subst E:` | có `E:\RideBoundData\final-v2` |
| 2 | kiểm sha256 mọi tệp với `MANIFEST.tsv` | 0 tệp sai |
| 3 | 4 job của 2 ô cổng (2 nhánh chính) + 4 job nhánh tách biến (SHHZ71-CL, SHRC71-N); `cloud_gates.py`: **A** 3 job có tham chiếu từ máy nhà phải trùng từng khung; **B** 4 job nhánh chính trùng dự đoán ở cả 4 mặt (trường từng khách, chuỗi giờ trả, lớp/điểm Shadow, chuỗi giờ công bố); **C** 4 job nhánh tách biến trùng dự đoán của chúng | `GATES-REPORT.json` có `"pass": true` |
| 4 | 168 job (4 job của bước 3 đã xong thì bỏ qua) | mọi job pass và trích xong bằng chứng |
| 5 | mọi phép so + chỉ số → `E:\runs\report\FINAL-REPORT.json` | `"allExact": true` |

Bước 3 thay cho 5 job kiểm chưa chạy được ở nhà (ổ E chạm sàn ngày 04/11): SHHZ71-N w17 và 4 job nhánh tách biến.

## 5. Dữ liệu giữ lại mỗi job

`summary.json`, `run-00.json`, `bundle-manifest.json`, `evidence.json.gz` (+ `EVIDENCE.sha256`): bảng sổ cam kết (mọi lần
công bố, kể cả giờ công bố), trạng thái cuối, giờ lên/xuống xe, số lần chuyển xe G2, lớp và điểm Shadow, biên nhận transcript
(sha256 từng khung, sha256 transcript chưa nén). Transcript chỉ bị bỏ sau khi bằng chứng được đọc lại qua đúng các hàm cũ và
cho kết quả trùng; 4 job của 2 ô cổng giữ transcript. Muốn có lại transcript của job nào: chạy lại job đó (tất định), các
khung phải trùng biên nhận từng cái.

## 6. Kết quả "đúng" là gì

`GATES-REPORT.json` `"pass": true` và `FINAL-REPORT.json` `"allExact": true`. Nếu không: không dùng số đó; mang `E:\runs`
về để tìm nguyên nhân.

## 7. Số đo trên máy nhà (04/11/2026; máy 15,7 GB RAM, 12 luồng, đang thiếu RAM nên chậm hơn máy rảnh)

| Đại lượng | Số đo |
|---|---|
| Thời gian một job ô 512 | 17,7–22,8 phút (máy rảnh trước đây: 13–15 phút) |
| Trích bằng chứng sau job | 278–293 s (bản đầu chưa ghi nhớ: 719 s) |
| RAM đỉnh một job | Runner 4,57 GB, adapter 4,24 GB; tổng hai tiến trình ≤ 6,2 GB (mẫu 5 s) |
| RAM đỉnh bước trích | 5,2 GB |
| Đĩa một job trước khi trích | 0,72–0,76 GB (transcript gzip) |
| Đĩa một job sau khi trích, bỏ transcript | 11,9–12,6 MB |
| Chạy lại hàng đợi trên job đã xong | 8 s, 0 byte dữ liệu mới, mọi tệp trùng sha256 |
| CPU | ~1 nhân mỗi job (FleetPy đơn luồng xen với Runner; CP-SAT cố định 1 luồng) |

Ước thời gian trên máy thuê: (168 job ÷ W) × ~19 phút (job + trích) + ~30 phút bước 3 + ~10 phút báo cáo.
W = 8 → khoảng 7 giờ; W = 10 → khoảng 6 giờ.

## 8. Giới hạn và rủi ro

- Chưa chạy được trên máy Windows khác máy nhà: bước 3A là phép thử đầu tiên. Nếu lệch, dừng, không chạy 168 job.
- Bộ Docker Linux cũ (GĐ5) không dùng cho lần này: chưa cập nhật Shadow/dự báo và chưa kiểm Linux = Windows.
- Gói chứa đầu vào 84 ô (dựng từ ô P512, dữ liệu TLC công khai) và dự đoán đã khóa; không chứa tệp của máy công ty.
