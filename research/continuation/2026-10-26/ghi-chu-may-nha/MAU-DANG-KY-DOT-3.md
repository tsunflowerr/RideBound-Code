# Mẫu đăng ký đợt xác nhận 3 (§6-N9): phần BẠN tự điền

Kiểm toán: KT:616, 677-682. Mẫu này chỉ nêu **phải điền gì** và các quy tắc bắt buộc. Giả thuyết, ngưỡng và lý do phải do bạn
tự viết; máy không viết hộ.

## 0. Trước khi viết

- Họp thầy, thầy duyệt phạm vi (P6).
- Chốt ngày (mục 3). Đề xuất của máy: các ngày 2019 còn lại, vì đầu năm 2020 chỉ có 45 ngày thường sạch.

## 1. Câu hỏi (1–2 câu, bạn viết)

Ví dụ về **dạng** câu hỏi, không phải câu trả lời: "Với gán lại, luật C có ít đảo hứa hơn hàng rào nới của Elmi cùng ngân
sách mà phục vụ không kém quá ___ không?"

## 2. Đối thủ và nhánh

- **Đối thủ chính:** EOHZ35 (chủ chốt ngày 25/10). Báo cạnh EO35. Nếu có Shadow thì SHHZ là chính, SHF báo cạnh.
- **x\* của Shadow:** nếu dùng, phải hiệu chỉnh lại theo đối thủ chính, với quy tắc ghi trước (không dùng lại 72/71).
- **Nhánh của luật C:** RCE35. Nếu muốn có đường cong thì ghi trước mức α nào (N2 thăm dò đã có).
- **Kiểu điều phối:** INS, G2, hay cả hai. G2 dùng mấy xe nhận.

## 3. Ngày

- **Quy tắc rút:** seed ghi trước; nguồn là `SP/wf12/r5-data-days/work/day_status.json` (khoảng 91 ngày AVAILABLE).
- **Bắt buộc loại:**
  - 2019-10-22 và 2019-12-06 (dữ liệu hỏng);
  - tháng 11/2019;
  - mọi ngày đã dùng: 55 ngày khám phá, 60 ngày wf14/wf15, 8 ngày wf7.
- **Ngày nghỉ học:** ghi tình trạng của từng ngày; phân tầng hoặc báo riêng.
- **Cỡ mẫu:** bao nhiêu ngày, và vì sao. Lần này nên có tính công suất.

## 4. Chỉ số chính (bạn chọn, ghi trước)

Phải có cả chỉ số của luật C lẫn của Elmi, đặt đồng chính:
- **của luật C:** đảo hứa (visRev60);
- **của Elmi:** P(điều phối dời giờ trả ≥ 5 s); tỷ lệ trễ quá p0 + B_r + 60 s;
- **phục vụ:** số khách; ngưỡng không kém (NI) **kèm lý do vận hành**, không phải "1% tự đặt".

## 5. Phân tích (ghi trước)

- **Đơn vị:** ngày. Giá trị ngày = tổng hai khung của trung bình 3 lần rút mẫu.
- **Kiểm định:** t theo ngày (hoặc dấu) với khoảng tin cậy 95%; hiệu chỉnh nhiều phép thử (Holm).
- **Báo cỡ hiệu ứng và khoảng tin cậy**, không chỉ giá trị p.
- **Chế độ nhận dạng:** adapter + half-even (đã bắc cầu với RideBound thật; P5 cho thấy các hiệu chính không đổi dấu).
- **Báo cả kết quả xấu.**

## 6. Dấu thời gian và người chạy

1. Viết xong, commit vào kho git của bạn, rồi push lên kho riêng **trước** mọi lượt chạy, kể cả chạy thử. Gửi e-mail mã
   băm cho thầy.
2. Bạn tự chạy lệnh đã đăng ký và tự chạy script phân tích. Máy chỉ chuẩn bị dữ liệu và công cụ.
3. Nếu phải đổi gì sau khi đã đóng dấu: ghi thành sửa đổi có giờ, nêu lý do; không sửa tệp đã đóng dấu.

## 7. Những câu không được viết sau khi có kết quả

Xem KT §7 (W1–W23). Đặc biệt: không gọi là "xác nhận" nếu thiếu bất kỳ điều nào ở mục 6.
