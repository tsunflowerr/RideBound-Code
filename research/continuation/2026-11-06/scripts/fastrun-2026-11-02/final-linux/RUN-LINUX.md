# Chạy thật lần cuối trên máy thuê Linux (ADR-089 GĐ5/GĐ6, bản Linux 04/11/2026)

Viết cho: chủ khóa luận, người thuê máy và bấm chạy.

## 1. Chạy cái gì, khác bản Windows ở đâu

Vẫn là 168 job (84 ô F14 × 2 nhánh `FS-G2-SHRC71-CL` và `FS-G2-SHHZ71-N`), cộng 8 job cổng. Kết quả từng khách phải
trùng dự đoán đã khóa trước mọi job.

Giữ nguyên so với bản Windows: Runner (cùng file DLL), mã adapter (commit `0eb9a6ca`), dữ liệu, dự đoán đã khóa và công cụ
so sánh. Phần khác:

- Python 3.10.22 bản Linux, các gói pip cài đúng phiên bản như môi trường ở nhà (cài từ wheel mang theo, không cần mạng).
  Riêng numpy là bản pip, ở nhà là bản conda.
- .NET 10.0.9 bản Linux (ở nhà cũng 10.0.9).
- Đường dẫn `E:/` đổi thành `/rb/`. Trong 168 cấu hình WP4 chỉ đổi `networkDirectory`; danh sách job và công cụ chạy cũng
  chỉ đổi đường dẫn.

Bỏ cổng A của bản Windows (khung giao thức giống máy nhà từng byte). Manifest ghi đường dẫn Linux của .NET, Runner và cấu
hình, nên khung luôn khác. Hai cổng còn lại phải qua trước khi chạy 168 job:

- **Cổng B:** 4 job nhánh chính ở 2 ô cổng phải trùng dự đoán đã khóa trên cả 4 mặt: trường từng khách, chuỗi ETA trả
  khách, lớp và điểm Shadow, chuỗi (thời điểm công bố, ETA). Ở nhà, Windows đã ra đúng các dự đoán này. Cổng B qua nghĩa là
  Linux cho kết quả như Windows, từng khách một.
- **Cổng C:** 4 job quy công (`FS-G2-SHHZ71-CL`, `FS-G2-SHRC71-N`) trùng dự đoán đã khóa.

Đã soát mã (04/11):

- Runner chỉ dùng Max, Min, Floor, Ceiling, Round, Clamp, Abs. Không có hàm log, exp, cos, nên không phụ thuộc thư viện
  toán của hệ điều hành.
- CP-SAT chạy 1 luồng, seed cố định, giới hạn bằng thời gian tất định, không theo đồng hồ.
- World của các job cuối không nhiễu (sigma = 0, shockRate = 0; wrapper từ chối mọi world khác). Hệ số thời gian đi vì thế
  đúng bằng base[b] từng bit; log, cos, exp trong `world_v1.py` không ảnh hưởng.
- Hàm mục tiêu có log10 của FleetPy không được gọi: lớp điều khiển RideBound đặt hàm mục tiêu bằng 0.

**Rủi ro còn lại (đọc mã không loại được):** OR-Tools bản Linux biên dịch bằng trình biên dịch khác bản Windows, và numpy
pip khác numpy conda. Nếu một trong hai làm lệch một quyết định, cổng B/C sẽ đỏ và script dừng trước 168 job. Thiệt hại
khi đó là khoảng 1 giờ thuê máy.

## 2. Thuê máy gì (vast.ai)

- Chọn **On-Demand**, không chọn Interruptible. Interruptible rẻ hơn nhưng có thể bị dừng bất cứ lúc nào. Khi đó các job
  đang chạy mất, phải chờ máy được trả lại rồi chạy lại script; script làm tiếp an toàn nhưng có thể mất hàng giờ.
- Template **Ubuntu 22.04 VM** nếu offer có. Nếu không có, dùng template Ubuntu 22.04 dạng container. Offer container chỉ
  cho một phần máy, ví dụ "12.0/96 CPU, 64/516 GB" là 12 nhân và 64 GB. Trong container script bắt phải đặt **WORKERS**,
  vì nó có thể nhìn thấy cả máy. Cách tính: WORKERS = min(số nhân thuê, (RAM thuê − 4) / 7). Ví dụ 12 nhân và 64 GB thì
  `WORKERS=8 bash run_linux.sh 2>&1 | tee -a run_linux.log`, và lần chạy lại nào cũng phải ghi `WORKERS=8`.
- Gọi W là số job chạy song song. Yêu cầu:
  - RAM dành cho instance ≥ 7 GB × W + 4 GB;
  - số nhân ≥ W (mỗi job khoảng 1 nhân);
  - đĩa ≥ 22 GB + 1 GB × W, nên đặt 60 GB.
- Gợi ý:

  | W | Nhân | RAM | Đĩa | Thời gian ước tính |
  |---|---|---|---|---|
  | 16 | ≥ 16 | ≥ 128 GB | 60 GB | khoảng 5 giờ |
  | 24 | ≥ 24 | ≥ 192 GB | 60 GB | khoảng 3,5 giờ |

  Cách tính: ở nhà mỗi job mất khoảng 25 phút, nên thời gian ≈ ⌈168 / W⌉ × 25 phút, cộng 30–50 phút cho cổng. Nếu mỗi nhân
  của máy thuê chậm hơn máy nhà thì lâu hơn.
  - Máy nhỏ, ví dụ 12 nhân và 64 GB, chạy W = 8: khoảng 9–10 giờ. Tiền vẫn ít, nhưng lâu.
- Trên VM, script tự chọn W từ RAM và số nhân. Muốn đặt tay: `WORKERS=16 bash run_linux.sh ...`.

## 3. Các bước

1. **File tải lên** (7 file, để chung một thư mục):
   - từ `E:\RideBoundData\final-v2-pack\`: `ridebound-final-v2.zip` (1,37 GB) và `MANIFEST.tsv`;
   - từ `E:\RideBoundData\final-v2-linux-pack\`: `ridebound-final-v2-linux.tar.gz`, `MANIFEST-linux.tsv`, `run_linux.sh`,
     `SHA256SUMS`, `RUN-LINUX.md`.
2. **Tải lên** từ PowerShell ở nhà. HOST và PORT lấy ở nút SSH của instance trên vast:
   ```
   ssh -p PORT root@HOST "mkdir -p /root/rb-upload"
   scp -P PORT E:\RideBoundData\final-v2-pack\ridebound-final-v2.zip E:\RideBoundData\final-v2-pack\MANIFEST.tsv root@HOST:/root/rb-upload/
   scp -P PORT E:\RideBoundData\final-v2-linux-pack\* root@HOST:/root/rb-upload/
   ```
3. **Chạy:**
   ```
   ssh -p PORT root@HOST
   cd /root/rb-upload
   tmux new -s rb
   bash run_linux.sh 2>&1 | tee -a run_linux.log
   ```
   - Nếu máy chưa có tmux: `apt-get install -y tmux`. Template container của vast thường tự mở tmux khi SSH; khi đó bỏ
     lệnh `tmux new`.
   - Thoát ra mà máy vẫn chạy: Ctrl+B rồi D. Vào lại: `tmux attach -t rb`.
   - Chạy lại đúng lệnh trên là chạy tiếp từ chỗ dừng: bước và job đã xong được bỏ qua, không file nào bị xóa.
   - Giữ nguyên thư mục `/root/rb-upload`: mỗi lần chạy, script đọc lại hai file MANIFEST ở đó để đối chiếu.
4. **Theo dõi** (trong một cửa sổ SSH khác):
   ```
   ls /rb/runs/final/*/evidence.json.gz | wc -l     # số job đã xong (tối đa 168)
   tail -5 /root/rb-upload/run_linux.log
   free -g; df -h /rb
   ```
   Muốn dừng nhận job mới thì chạy `touch /rb/runs/STOP`; các job đang chạy vẫn chạy hết. Muốn chạy tiếp thì
   `rm /rb/runs/STOP` rồi chạy lại lệnh ở bước 3.
5. **Nếu cổng đỏ:** script dừng với dòng `GATES FAILED` hoặc `the gates failed earlier`. Khi đó **không chạy 168 job**:
   - tải về `/rb/runs/gates-report/`, `/rb/runs/logs/`, `/rb/runs/ENVIRONMENT.json` và `/root/rb-upload/run_linux.log`;
   - gửi lại để phân tích, rồi hủy máy.
6. **Khi xong:** script in ra tên 3 file trong `/rb/`.
   - Tải về `ridebound-results-*.tar.gz` (khoảng 2,5 GB: evidence, log, cổng, báo cáo, môi trường) trước, sau đó
     `ridebound-transcripts-*.tar` (4 transcript của ô cổng, khoảng 3 GB) và `ridebound-results-*.sha256`:
     ```
     scp -P PORT root@HOST:/rb/ridebound-results-* .
     scp -P PORT root@HOST:/rb/ridebound-transcripts-* .
     ```
   - Kiểm mã băm ở nhà: `Get-FileHash <file> -Algorithm SHA256`, so với file `.sha256`.
   - Cuối cùng **Destroy** instance. Chỉ Stop thì vast vẫn tính tiền đĩa.

## 4. Script làm gì

| Bước | Việc | Dừng khi |
|---|---|---|
| 0 | Kiểm x86_64, git, libicu; thiếu thì cài bằng apt-get | không cài được |
| 1 | `sha256sum -c SHA256SUMS` cho 5 file tải lên | file hỏng hoặc thiếu |
| 2 | Giải nén overlay, Python, .NET (đối chiếu mã băm), rồi zip gốc vào `/rb` | thiếu chỗ (cần 10 GB) |
| 3 | Cài gói pip từ wheel mang theo | thiếu wheel |
| 4 | Đối chiếu SHA-256 từng file với `MANIFEST.tsv` và `MANIFEST-linux.tsv` | một file khác |
| 5 | Kiểm phiên bản Python, gói, .NET, libicu; git HEAD = `0eb9a6ca`; kiểm kê repository = giá trị đo ở nhà; ghi `ENVIRONMENT.json` | một mục sai |
| 6 | Cổng B và C (8 job) | một khách lệch dự đoán |
| 7 | 168 job (job đã xong được bỏ qua) | còn job chưa xong; chạy lại để làm tiếp |
| 8 | So sánh và số đo, ghi `/rb/runs/report/FINAL-REPORT.json` | lỗi báo cáo |
| 9 | Đóng gói kết quả | thiếu chỗ |
