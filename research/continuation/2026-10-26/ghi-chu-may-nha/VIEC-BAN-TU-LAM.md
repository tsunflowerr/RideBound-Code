# Những việc chỉ bạn làm được (để kiểm toán không còn chỗ chê "AI dắt mũi")

> Bản chép để đưa lên repo (26/10/2026): giống hệt tệp gốc ở máy nhà `C:/RideBoundContinuation/2026-10-25/VIEC-BAN-TU-LAM.md`, chỉ bỏ mục 5 (danh sách lấy tệp từ máy công ty).

25/10/2026. Căn cứ: kiểm toán công ty 23/10, các mục §6-P4, §6-P6, §8.2 (KT:595,651,657-663,760-770). Máy làm thay được
những việc khác; mấy việc dưới đây phải do chính bạn làm, có ghi ngày.

Ký hiệu: `SP` = `E:/RideBoundData/company-intake/2026-10-23/sp`.

---

## 1. Duyệt mã C# và adapter mà AI viết (§6-P4)

Kiểm toán yêu cầu sinh viên tự viết, hoặc **duyệt từng dòng trước khi chạy**. Thứ tự đó không làm lại được nữa vì đã chạy
rồi. Vì vậy cần làm hai việc:
- duyệt ngay bây giờ;
- ghi thật trong khóa luận: "mã do trợ lý viết, sinh viên duyệt sau khi chạy".

Các commit cần duyệt (nhánh `research/p1-dwell`; chỉ tính dòng mã chạy, chưa tính test):

| Thứ tự đề nghị | Commit | Nội dung | Dòng mã | Cần nhìn kỹ |
|---|---|---|---|---|
| 1 | `18978dd` | Ngân sách Elmi theo từng khách: B_r = round(α · độ dư) | +224 | Công thức B_r; làm tròn xa số 0; B_r cố định ở lời hứa đầu |
| 2 | `1190518` | Chỉ nhận ngân sách theo khách khi chọn cost-first | +17 | Điều kiện từ chối |
| 3 | `3afcd17`, `176f5dd` | Trần từng lần (PS) và hạn chót nới (DL-HZ), viết trên máy công ty | +173, +6 | Hạn chót nới đúng là p ≤ max(p0 + B, e) |
| 4 | `ef2b02f`, `6ab03fc` | Hai bản sửa adapter FleetPy (ca "xe nhận" và ca "xe cho") | +79, +62 | Chỉ chạy ở chỗ mã cũ đã báo lỗi |
| 5 | `9ace339` | Tùy chọn thời gian dừng, mặc định tắt | +44 | Vắng khóa thì không đổi gì |
| 6 | `c6588f8` | G2: dời 1 khách đang chờ sang xe khác | +2.029 (30 tệp) | Đọc 5 tệp chính: bộ sinh ứng viên dời khách, ràng buộc cặp cho–nhận trong CP-SAT, khóa giờ đón chỉ được sớm lên |
| 7 | `9b633d9`, `2eadfb7` | Băm trạng thái một lần mỗi epoch (A0.1), viết trên máy công ty | +1.829, +16 | Đọc lướt; bằng chứng chính là cổng phát lại 58/58 job giống từng byte |

Lệnh xem:
- `git -C C:/Code/RideBound-p1dwell show 18978dd -- src`
- `git -C C:/Code/RideBound-p1dwell log --stat 7435661..HEAD`

Duyệt xong, ghi một dòng vào docs/18 (ADR-081/082):
- "Sinh viên đã duyệt các commit … ngày …";
- những chỗ chưa hiểu, nếu có.

---

## 2. Tự kiểm không dùng mã của AI (§8.2)

1. **Tự viết một script ngắn** (Python, hoặc Excel; không chép mã của AI) đọc `SP/wf14/results/main.ndjson`.
   - Giá trị một ngày = tổng hai khung của trung bình 3 lần rút mẫu.
   - Phải ra các số sau:
     - G2, RCE35 − EO35, visRev60: **−16,53/ngày**, 60/60 ngày;
     - G2, RCE35 − EO35, served: **−1,33/ngày**;
     - tỷ lệ breachDeadline60 / served gộp 60 ngày: RCE35 và EO35 lần lượt **1,78% / 1,11%** (INS) và **1,75% / 1,02%**
       (G2).
2. **Lần vết bằng tay 1–2 khách** trong một ô: từ t0, p0, các lần đổi giờ và A, tự tính Σ|c|, rồi xem khách đó có bị tính
   là đảo hứa hay trễ đúng định nghĩa không.
3. **Tự tải một tệp TLC HVFHV** từ trang chính thức của NYC TLC (ví dụ tháng 9/2019).
   - Lọc `hvfhs_license_num = 'HV0004'` và khu đón/trả thuộc Manhattan.
   - Lấy ngẫu nhiên 20 dòng của `SP/wf14/work/via_windows.csv` cùng tháng, tìm từng dòng trong tệp vừa tải. Khớp theo:
     `req_s` (giây Unix của giờ New York, đọc như UTC), `pu`, `dz`, `trip_miles`, `trip_time`.
   - Máy cũng sẽ làm phép EXCEPT ALL (§6-P7), nhưng phép của bạn phải là phép riêng.
4. **Tự đọc lại bài Elmi** (`SP/dl_elmi_orig/orig.pdf`) ở trang 13, 15 (chú thích 12), 19, 24. So với các câu khóa luận
   định trích.
5. **Ghi kết quả 4 bước** vào kho git của bạn, có ngày, trước buổi họp với thầy.

---

## 3. Họp thầy Trần Mạnh Cường (§6-P6 bước 5)

**Mang theo:**
- phát biểu đóng góp dưới đây;
- phạm vi mới: bộ mô phỏng, dữ liệu Via, so với Elmi, G2;
- danh sách giới hạn.

**Phát biểu đóng góp, bản nháp** (sửa từ KT:118 theo hiện trạng 25/10):

> Khóa luận cài đặt trong RideBound một sổ cam kết bị chặn theo từng khách (luật C: quy trách nhiệm c = p − e để không tính
> phần do giao thông; tính |c| hai chiều, không hoàn). Trên các lượt chạy hệ thật, sổ luôn giữ dưới ngân sách. Một bộ mô
> phỏng đã được kiểm khớp RideBound thật từng khách trên 227 ô-nhánh Manhattan, kể cả gán lại xe (G2). Trong bộ mô phỏng
> đó, trên dữ liệu Via: so với Elmi Ordinary hàng rào nới cùng ngân sách, luật C phục vụ ngang khi chỉ chèn; khi cho gán
> lại, luật C giảm khoảng 42% số lần đảo hứa và phục vụ ít hơn khoảng 0,4%. Đây là đánh đổi có điều kiện, không phải ưu thế
> chung so với Elmi.

Hai con số cuối là thăm dò, sẽ được kiểm lại bằng P5 trước khi họp. Nếu P5 làm đổi dấu một kết luận thì câu này phải sửa.

**Cần bạn tự quyết trước khi họp:** khai báo dùng AI.
- Kiểm toán yêu cầu "khai báo quy trình có AI hỗ trợ theo quy định của khoa" (KT:662).
- Ngày 14/10 bạn đã chọn không nhắc AI trong khóa luận.
- Hai điều này trái nhau. Hãy hỏi thầy hoặc xem quy định của khoa.

---

## 4. Đính chính câu trình bày ngày 20/10

Nếu bạn đã trình bày câu "hạn chót bị ép 38% quyết định và phá lời hứa với 12% khách" (trang artifact ngày 20/10) với thầy:
- 7.710/20.103 lần đó là lúc phần nới "không tệ hơn" **được kích hoạt**, không phải bị ép (KT:559);
- câu "đối thủ phá cam kết" bị cấm (KT:702, §7-W1).
