# Đo bộ đọc stdout của adapter (ADR-090, tối ưu #1)

`bench_read.py` chạy đúng `RunnerClient.start()` và luồng `_read_stdout` của một cây adapter, đọc dữ liệu do
`emit_lines.py` phát (cùng tham số thì cùng byte), rồi in thời gian từ lúc khởi động tới EOF, sha256 của mọi dòng nhận
được và bộ nhớ đỉnh của tiến trình (Windows). Lớp giao thức không chạy, nên số đo chỉ là đường đọc.

```powershell
# cũ: một bản xuất của commit trước ADR-090; mới: cây hiện tại
python -B bench_read.py <adapter-root-cu> 67108864 1
python -B bench_read.py <adapter-root-moi> 67108864 1
```

`RESULT.jsonl`: máy công ty, 04/11/2026, Python 3.13.15 (bản đi kèm LibreOffice), chạy liền nhau khi máy rảnh.

| Dữ liệu | Cũ | Mới |
|---|---|---|
| 1 dòng 16 MiB | 28,2 s | 0,68 s |
| 1 dòng 64 MiB | 99,8 s | 0,81 s |
| 3.000 dòng × 20 KB | 87,4 s | 0,75 s |
| 1 dòng 400 MiB | (không đo) | 2,36 s |
| 1 dòng 768 MiB | (không đo) | 3,88 s |

sha256 trùng giữa cũ và mới ở mọi ca đo cả hai. Bản cũ tuyến tính, khoảng 1,5 µs/byte trên máy này.

**Ở job thật:** các kit chạy job trong repo đặt `RB_REALJOB_BUFFERED_READ=1`, nên launcher đã vá đúng bộ đọc này. Vì vậy
job chạy qua launcher gần như không nhanh hơn. Bản sửa chỉ đưa bản vá đó vào chính adapter, có ích cho các lần chạy không
qua launcher.
