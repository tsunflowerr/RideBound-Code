# research/thesis-emulator-home-2026-11-18

Danh sách sha256 của gói bằng chứng cho **mô phỏng nhanh** dùng ở Chương 5 của khóa luận (bản PDF 17/11/2026).

Gói gồm: code thời khóa luận (bộ chạy `emulate_adapter*.js`, `engine_wf15_clint*.js` và mọi module chúng nạp), công cụ tính ngưỡng θ, các dữ liệu cấu hình nhỏ, các dòng kết quả đã lưu của từng họ (main60, fc30, alpha10, fleet-N, pen300, eta30), bằng chứng kiểm ngày 16/11/2026, lệnh chạy từng họ, và các bản diff giữa các động cơ.

**File thật chưa nằm trên nhánh này.** Các bộ chạy nạp nhau bằng đường dẫn tuyệt đối của máy cục bộ; để giữ nguyên từng byte (và hash), file không được sửa, nên không đưa lên repo công khai. Gói được chuyển riêng dưới dạng file nén
(`goi-may-nha-20261118.zip`, sha256 `bf02796568648f15e00a281b3542f65216bbf3cfc1f6bf3ce3ecf785b8bc1fab`).

Nội dung nhánh:

| File | Ý nghĩa |
|---|---|
| `SHA256SUMS.txt` | sha256 của mọi file trong gói (đường dẫn tương đối, không tính chính nó). Kiểm: `sha256sum -c SHA256SUMS.txt` trong thư mục đã giải nén |
| `PUSH-SCAN-HITS.tsv` | file nào trong gói chứa đường dẫn máy cục bộ hoặc từ khóa nhạy cảm (chỉ tên file, loại, số dòng; không có nội dung) |

Nhánh này không dựa trên `dev` hay `main`, không đổi gì ở hai nhánh đó. Commit 9ba5568 và nhánh `research/final-real-runs-2026-11-06` (chương trình chính, 360 lượt chạy chính) vẫn là nguồn của các số liệu của chương trình chính.
