# Mục 9 (viết lại ngày 04/11/2026): Hệ thống thật và bộ mô phỏng

Bản này thay mục 9 cũ, "Vì sao không chạy trên hệ thống thật, và vì sao tin được bộ mô phỏng", trong báo cáo hệ thống
của chủ. Các con số lấy từ `docs/18` (ADR-081..090) và các kit trong repo, và đã được kiểm chéo ngày 04/11. Khi chạy xong
168 lượt, 5 lượt cổng Shadow còn lại và GĐ6, cần cập nhật bảng kiểm chứng và phần giới hạn.

---

**9) Hệ thống thật và bộ mô phỏng: mỗi bên làm gì, và vì sao tin được số liệu**

**Mỗi bên làm gì**

- **Bộ mô phỏng chạy các bảng ở mục 6 và 7.** Các bảng này cần hàng nghìn lượt chạy. Riêng bảng 30 ngày đã có 3.600 lượt:
  180 ô × 2 cách điều phối × có/không dự báo × 5 luật. Bộ mô phỏng chạy hết trong khoảng nửa giờ.
- **Hệ thống thật dùng để kiểm chứng bộ mô phỏng.** Hệ thống thật gồm FleetPy (mô phỏng xe chạy trên đường) và RideBound
  (ra quyết định). Cách kiểm: chạy một số ô, rồi so từng khách với bộ mô phỏng.

**Trước đây hệ thống thật chỉ chạy được ô Via gộp khu**

Ô gộp khu có 58–60 nút, mỗi lượt mất 81–118 phút. Ô Via đủ 512 yêu cầu thì không chạy được. Có 3 chỗ nghẽn:

1. **Dữ liệu quá lớn.** Ô đủ có 824–858 nút; nối mọi cặp nút thì được từ khoảng 680.000 cung trở lên. Bảng thời gian đi
   gửi cho RideBound vượt giới hạn 64 MiB của một dòng dữ liệu. Bộ tìm đường của FleetPy cũng quá chậm ở cỡ này.
2. **RideBound chạy chậm, vì ba việc:**
   - để kiểm toán, nó băm lại toàn bộ trạng thái hai lần ở mỗi quyết định, trong khi trạng thái lớn dần tới khoảng 400 MB;
   - mô hình của bộ giải tối ưu bị dựng lại nhiều lần trong mỗi quyết định;
   - sổ lời hứa thêm mục ngày càng chậm.
3. **Hết bộ nhớ.** Lúc ghi bản chốt cuối, RideBound tạo nhiều bản sao trạng thái. Trong 10 lượt Via gộp khu đã chạy, 4
   lượt hỏng vì lý do này.

Ngoài ra, với 62 xe phải nâng ngân sách tính toán của mỗi quyết định bằng cấu hình. Việc này không đổi luật nào.

**Đã sửa (02–04/11/2026), không đổi quyết định nào**

- Giới hạn một dòng dữ liệu nâng lên 1 GiB.
- Ô đủ nút dùng mạng đường dạng bảng (đi thẳng giữa mọi cặp nút), thay cho bộ tìm đường của FleetPy. Trên ô 60 nút, hai
  cách cho kết quả giống từng byte.
- Mô hình của bộ giải dựng một lần cho mỗi quyết định. Sổ lời hứa đổi sang cấu trúc dữ liệu thêm mục nhanh.
- Thêm chế độ kiểm toán nhẹ: mỗi quyết định chỉ băm mã nhận dạng của trạng thái; bản chốt cuối vẫn băm toàn bộ trạng thái.
- Bản chốt được ghi dần ra ngoài, không tạo bản sao. Những lượt từng hết bộ nhớ ở trần 6 GiB nay chỉ cần 1,3–1,9 GiB cho
  RideBound, chưa kể FleetPy và adapter.
- Gộp các sửa trên, phần RideBound của một lượt ô 60 nút giảm từ khoảng 80 phút xuống 9 phút, đo bằng phát lại. Một lượt
  trọn mất 6–10 phút. Ở chế độ kiểm toán đầy đủ, cùng lượt vẫn mất khoảng 75 phút.
- Bằng chứng "nhanh hơn nhưng không đổi quyết định":
  - phát lại 228 lượt Manhattan đã ghi: 176.074/176.074 dòng giống từng byte;
  - phát lại một lượt Via gộp khu: 2.908/2.908 dòng giống từng byte;
  - các phép phát lại này chưa có Shadow và dự báo.

Nay một ô Via đủ 512 yêu cầu chạy trọn trong 15–21 phút, cả hệ cần 5–8 GB RAM. Đã thử 2 ô (824 và 830 nút), ở chế độ kiểm
toán nhẹ, với các luật không có Shadow hay dự báo. Thời gian của lượt có Shadow hoặc dự báo chưa đo.

Các bảng chính vẫn dùng bộ mô phỏng vì hai lý do:
- 3.600 lượt × 15–21 phút là khoảng 900–1.260 giờ máy (ước lượng thô);
- phép đối chứng "đệm" ở mục 7 chưa có trong hệ thống thật.

**Vì sao tin được bộ mô phỏng**

Trước mỗi lần chạy hệ thống thật, kết quả dự đoán của bộ mô phỏng được khóa bằng mã sha256. Nếu tệp dự đoán bị sửa sau
đó, phép so sẽ phát hiện. Thời điểm khóa chỉ được ghi trên máy chạy, không có mốc thời gian độc lập bên ngoài.

Sau đó so từng khách: có được phục vụ không, lúc nhận lời hứa đầu, giờ trả đã hứa, giờ trả thực tế, có đi chung xe không,
và các lần đổi giờ trả (tổng, số lần, lần lớn nhất). Khi có gán lại thì so thêm số lần đổi xe.

| Phép so | Kết quả |
|---|---|
| Manhattan: 238 cặp ô × luật, kể cả gán lại | trùng từng khách |
| Via gộp khu (58–60 nút, ngày 13/2/2019): 3 luật × 2 cách điều phối × 2 ô = 12 cặp | 4.878/4.878 khách |
| Via đủ 512 yêu cầu (824 và 830 nút, ngày 13/2/2019): 4 luật × 2 cách điều phối × 2 ô = 16 lượt, không Shadow, không dự báo | 5.925/5.925 khách |
| Dự báo tắc đường: 2 lượt | 704/704 khách |
| Shadow: 3 lượt (so thêm lớp Shadow của khách và chuỗi giờ hiển thị) | 1.045/1.045 khách |

Ba cổng (Manhattan có gán lại, dự báo, Shadow) lần đầu đều có lượt hỏng. Chúng chỉ đạt sau khi sửa lỗi phía hệ thống thật:
adapter, và mã nút của Shadow. Dự đoán của bộ mô phỏng giữ nguyên.

Riêng khi có dự báo, adapter bỏ phép kiểm giờ đón muộn nhất và thời gian đi tối đa của FleetPy. RideBound vẫn giữ hai ràng
buộc này, nhưng tính theo giờ dự báo, nên xe có thể đón muộn thật.

**Giới hạn**

- **Hai chế độ của bộ mô phỏng.** Các phép so ở trên dùng chế độ "adapter": bộ mô phỏng đặt mã khách và làm tròn thời gian
  đi giống hệ thống thật, để so được từng khách. Còn các bảng ở mục 6 và 7 dùng chế độ "plain".
  - Hai chế độ khác nhau ở mã nhận dạng (dùng để phá hòa khi hai phương án ngang nhau) và ở cách làm tròn thời gian đi,
    lệch tới 1 ms. Một khác biệt nhỏ ở một quyết định kéo theo các quyết định sau, nên hai chế độ không trùng từng khách.
  - Với các luật không có dự báo, khoảng cách đã đo hai lần trên 10 ngày (60 ô và 20 ô).
    - Hiệu về đảo hứa giữ cả chiều lẫn độ lớn: khi gán lại, luật C bớt khoảng 41–43% so với hàng rào nới.
    - Hiệu số khách phục vụ giữa luật C và hàng rào nới chỉ vài khách mỗi ngày và không bền: ở một lần đo nó đổi dấu (cả
      khi chỉ chèn lẫn khi gán lại), ở lần kia nó giữ dấu.
  - Với các luật có dự báo, khoảng cách chưa đo. Ở đây hai chế độ còn khác thêm hai điểm:
    - dự đoán adapter được sinh bằng một bản khác của bộ mô phỏng, có làm tròn giờ dự báo từng chặng;
    - chế độ plain không làm tròn, và có cắt giờ đón hiển thị.
- **Lần kiểm cuối chưa chạy.** Nó gồm 168 lượt trên hệ thống thật: 84 ô của 14 ngày, mỗi ô chạy hệ của mình và đối thủ, có
  gán lại. Dự đoán cho lần này đã khóa từ 03/11.
- **Chưa có kiểm chứng trên hệ thống thật cho:**
  - các ngày của bảng 30 ngày, cho tới khi 168 lượt chạy xong. Ngày 13/2/2019 ở trên thuộc bảng 60 ngày;
  - 16 ngày F16, kể cả sau 168 lượt;
  - hai nhánh tách biến (luật C không dự báo, hàng rào nới có dự báo), ngoài 2 ô cổng;
  - các luật Shadow ở chế độ chỉ chèn;
  - Shadow kèm hàng rào chặt;
  - phép đối chứng "đệm";
  - bảng 60 ngày, mới có 1 ngày;
  - thời gian dừng khác 0 giây.
- **Nguồn của ô đủ nút.** Ô Via đủ nút dựng từ chính các ô mà bộ mô phỏng dùng (lấy từ dữ liệu TLC công khai), chưa dựng
  lại bằng một bộ chuyển đổi độc lập.
