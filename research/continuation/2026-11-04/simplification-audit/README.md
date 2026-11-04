# Kiểm toán mã và kế hoạch refactor (giai đoạn 3–4 của đợt 04/11/2026)

**Trạng thái: chỉ là phân tích và kế hoạch, chờ chủ duyệt. Chưa refactor, chưa bỏ thành phần nào.**

| Tệp | Nội dung |
|---|---|
| `KE-HOACH-REFACTOR.md` | Tài liệu chính, tiếng Việt: phạm vi thí nghiệm cuối, kiểm kê thành phần (giữ / đơn giản hóa / ứng viên bỏ), chế độ chỉ chèn (insert-only), kế hoạch refactor 9 giai đoạn (0–8), kiến trúc tối giản, các câu hỏi chủ cần quyết |
| `BANG-CHUNG-BO-SUNG.md` | Bằng chứng thô, tiếng Anh: 8 khoảng hở và 8 mâu thuẫn mà bước rà soát chéo tìm ra, cùng báo cáo của 8 agent bổ sung, mỗi agent xử lý một khoảng hở |

## Cách lập

Một workflow gồm 30 agent chỉ đọc mã, chạy trên `ad470c1`:
1. **Phạm vi:** đọc ADR-081..090, các kit và emulator để xác định thí nghiệm cuối dùng những nhánh, chế độ và cấu hình nào, và con số luận văn đến từ đâu.
2. **Chế độ chỉ chèn:** phân tích riêng.
3. **Kiểm kê:** 9 vùng (Runner, Domain, Application, Algorithms + OR-Tools, Contracts, Benchmarking + tools, adapter runtime, script adapter, cấu hình + kit). Mỗi thành phần được phân loại: (a) không tới được; (b) cần để tái lập kết quả đã ghi; (c) thí nghiệm cuối cần.
4. **Phản biện:** mỗi vùng có một agent riêng tìm cách bác các đề xuất ĐƠN GIẢN HÓA / BỎ.
5. **Rà soát chéo:** tìm khoảng hở và mâu thuẫn giữa các vùng.
6. **Bổ sung:** 8 agent, mỗi agent xử lý một khoảng hở bằng bằng chứng trong repo. Một agent đọc bản luận văn trong repo `Report_INT3508` công khai (clone tạm, chỉ đọc). Một agent publish Runner từ `git archive` ở ngoài repo để so tệp phụ thuộc.
7. **Tổng hợp:** viết `KE-HOACH-REFACTOR.md` chỉ từ bằng chứng của các bước trên.

Sau đó người lập kế hoạch đối chiếu thêm một số điểm với mã, và cập nhật các mục đã xử lý trong ngày:
- harness byte ADR-090 đã lưu vào repo;
- câu "không đổi byte" của ADR-090 đã thu hẹp.

## Phát hiện làm đổi cách nhìn so với giả định ban đầu

- Bản luận văn `7f93ee6` (95 trang) trích nhiều số do Runner C# sinh: T3.8 stability-first, `customerVisible`, M+, Stage 1b, WP14 `runner-v1`. Bản `34920ee` (chỉ Via) gần như không trích. Chưa rõ bản nào được nộp, nên các đường chạy đó được giữ.
- Cổng G1/G2 (228 job) chỉ phủ cost-first, Manhattan, `full-v1`, không Shadow, không dự báo. Cần thêm lưới N1–N5 trước khi refactor.
- Cấu hình của 168 job cuối, các script cổng và bộ dự đoán adapter-mode (`engine_wf15_clint.js`) nằm ngoài repo. Mọi đề xuất "bỏ khóa cấu hình" phụ thuộc vào kết quả grep trên ổ E: (bước 0.4).
