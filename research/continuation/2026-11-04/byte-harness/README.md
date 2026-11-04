# Harness so từng byte giữa hai bản Runner (ADR-090)

Harness chạy hai bản `RideBound.Runner` (cũ và mới) trên **cùng stdin**, rồi so stdout, stderr và mã thoát từng byte.
Mỗi ca in một dòng `SAME` hoặc `DIFF`, kèm sha256 rút gọn của stdout. Nếu `DIFF`, nó in thêm dòng đầu tiên khác nhau.
Đây là bằng chứng cho câu "không đổi byte" trong ADR-090. Đây **không** phải phép chạy lại transcript đã ghi.

## Các chế độ

| Lệnh | Nội dung |
|---|---|
| (không đối số) | Ca cơ bản trên kịch bản wp3: chạy thường, bản chốt sau mỗi quyết định, khôi phục từ từng bản chốt, wp2, wp6; các ca biên NDJSON (CRLF, BOM, dòng trống, UTF-8 hỏng, dòng đúng/vượt giới hạn, 64 MiB); một bản chốt bị chèn `shadowAllocations` |
| `big` | Dòng lớn (đệm nhiều cỡ, LF và CRLF) |
| `light` | **Chỉ chạy bản mới**, không so hai bản. Cấu hình `auditHash` light-v1 và full-v1 (trong `cfg/`), có và không có `RIDEBOUND_STATE_HASH=v2`. Kiểm rằng khôi phục từ mỗi bản chốt cho ra đúng phần quyết định và bản chốt còn lại |
| `ids` | Định danh có ký tự thoát ASCII, Unicode, dấu runId; khôi phục từ từng bản chốt |
| `bindings-47.txt` | 47 cặp cấu hình cam kết + WP4 trong `benchmarks/configurations/`. Mỗi cặp chạy trên 2 kịch bản (wp3 tiny, wp6 e2e tiny), có bản chốt sau mỗi quyết định và khôi phục từ từng bản chốt: 564 lượt so |

Mỗi dòng của `bindings-47.txt` có dạng `cấu hình cam kết|cấu hình WP4|policyId|policyVersion|binding hash`.

## Giới hạn (đọc trước khi trích)

- Chỉ dùng cấu hình **trong repo**. Cấu hình của đợt chạy cuối (final-v2: Shadow, dự báo, light-v1) nằm ngoài repo.
  Repo không có tệp JSON nào chứa `shadowAllocation` hay `planningForecast`, nên **không có phiên Runner nào bật Shadow
  hoặc dự báo** trong harness này. Với những cấu hình đó, "không đổi byte" dựa trên lập luận và test đơn vị, không dựa
  trên phép so byte. Phần lập luận nằm ở ADR-090, mục Consequences.
- Kịch bản là các kịch bản tí hon trong repo, không phải job Manhattan hay Via đã ghi. Các cổng G1/G2 (chạy lại transcript
  đã ghi) là việc khác, và không chạy ở đây.
- Cả hai bản chạy trên cùng runtime .NET của máy, nên harness không cho biết kết quả có giữ nguyên khi đổi runtime hay không.

## Cách chạy

`global.json` của repo ghim SDK 10.0.301. Vì vậy build và chạy harness **ngoài repo**, để `bin/` và `obj/` không rơi vào
cây repo.

```bash
# 1. Hai bản Runner, mỗi bản publish từ git archive của một commit (sửa global.json trong bản chép nếu máy không có 10.0.301)
git -C <repo> archive 101a4f5 | tar -x -C /tmp/src-old
dotnet publish /tmp/src-old/src/RideBound.Runner/RideBound.Runner.csproj -c Release -o /tmp/pub-old
# làm tương tự cho commit mới -> /tmp/pub-new

# 2. Harness: chép thư mục này ra ngoài repo rồi build
dotnet build -c Release

# 3. Chạy
export RIDEBOUND_REPO=<repo> OLD_RUNNER=/tmp/pub-old NEW_RUNNER=/tmp/pub-new
dotnet bin/Release/net10.0/harness.dll                      # ca cơ bản
dotnet bin/Release/net10.0/harness.dll big
dotnet bin/Release/net10.0/harness.dll light
dotnet bin/Release/net10.0/harness.dll ids
dotnet bin/Release/net10.0/harness.dll <repo>/research/continuation/2026-11-04/byte-harness/bindings-47.txt
```

Bản gốc của harness đọc đường dẫn repo và thư mục Runner từ hằng số và từ biến `SCR` của máy công ty. Bản lưu ở đây đọc
chúng từ `RIDEBOUND_REPO`, `OLD_RUNNER` và `NEW_RUNNER`; ngoài ra logic không đổi.

## Kết quả

`RESULT.txt`: máy công ty, 04/11/2026, .NET SDK 10.0.400, runtime 10.0.11.

- Bản cũ: build từ `101a4f5`. Bản mới: build từ cây làm việc của nhánh lúc 13:15, trước khi commit `ad470c1` lúc 13:38.
- Khối cuối so `59c4ee8` với bản mới.

| Khối | Kết quả |
|---|---|
| Ca cơ bản | 25 SAME, 1 DIFF có chủ ý: bản chốt bị chèn `shadowAllocations` giờ bị từ chối như trước khi có Shadow |
| `big` | 14 SAME |
| `light` | light-v1 + v2 thoát mã 64 (sửa lỗi 4). Ba tổ hợp còn lại: 6 quyết định, 7 bản chốt; khôi phục ở cả 6 điểm cho ra đúng phần còn lại |
| `ids` | 11 SAME |
| 47 cặp cấu hình | 564 SAME, 0 DIFF |
| `59c4ee8` so với bản mới | 25 SAME, 1 DIFF dự kiến: dòng 64 MiB + 1 (S3 của `9ba5568`); ca khôi phục ở trên trùng `59c4ee8` |

`RESULT-rerun.txt`: cũng máy công ty, 04/11/2026, runtime 10.0.11. Lần này chạy lại toàn bộ bằng chính bản lưu ở đây,
với hai Runner publish từ `git archive` của `101a4f5` và `ad470c1`, nên mỗi bản ứng với đúng một commit.

- Mọi khối trùng `RESULT.txt` từng dòng: cùng SAME/DIFF, cùng sha của stdout, 564/564 lượt WP4.
- Khác duy nhất là vài dòng trống trong log, ngay sau phần stderr được in ra. Log `RESULT.txt` đã mất các dòng trống này
  lúc được ghi lại. Bản harness gốc chạy lại trên cùng hai Runner cũng in đúng các dòng trống đó.
