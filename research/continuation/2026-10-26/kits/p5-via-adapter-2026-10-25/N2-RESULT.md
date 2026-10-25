# N2: đường cong α, kết quả (25/10/2026, 06:27). Thăm dò, sau kiểm toán

Thiết kế: `N2-PLAN.md`, ghi trước khi chạy. Chạy ở chế độ plain, vì P5 không có phép so chính nào đổi dấu. Dữ liệu: 60 ô
của P5 (10 ngày xác nhận đã xem); 2.160 dòng, 0 lỗi (`rows/n2-plain.ndjson`). α = 35% lấy từ dòng P5 plain. Bảng đầy đủ ở
`N2-ALPHA-TABLE.tsv`, với giá trị ngày như P5.

## Gán lại (G2): luật C so với hàng rào nới cùng α

| α | served RCE / EOHZ (hiệu) | đảo hứa RCE / EOHZ (hiệu, %) |
|---|---|---|
| 0% | 611,2 / 621,7 (−10,5) | 0,8 / 3,4 |
| 10% | 658,3 / 659,9 (−1,6) | 5,6 / 12,4 (−55%) |
| 20% | 689,4 / 694,2 (−4,8) | 12,7 / 23,6 (−46%) |
| 35% | 721,4 / 725,6 (−4,1) | 21,5 / 36,4 (−41%) |
| 50% | 746,9 / 750,7 (−3,8) | 30,2 / 51,1 (−41%) |
| 75% | 777,3 / 786,4 (−9,2) | 45,0 / 74,7 (−40%) |
| 100% | 793,1 / 799,1 (−6,0) | 59,1 / 87,9 (−33%) |

## Chỉ chèn (INS)

Luật C và hàng rào nới gần như trùng ở mọi α:
- served lệch từ −6,3 (α = 0) tới +1,8;
- đảo hứa của luật C thấp hơn 0,2–1,7 lần mỗi ngày.

Riêng **hàng rào chặt ở α = 0** phục vụ thấp hẳn (579,5 so với 617,5 của hàng rào nới). Đó là artefact ép xe khi ngân sách
bằng 0, cùng loại với SHF.

## Đọc kết quả

- **Hướng của kết luận chính không chỉ đúng ở α = 35%.** Với gán lại, ở mọi α từ 10% tới 100%, luật C giảm đảo hứa 33–55% so
  với hàng rào nới cùng ngân sách, đổi lại phục vụ ít hơn 1,6–9,2 khách mỗi ngày (0,2–1,2%).
- **Khi chỉ chèn, không có khác biệt đáng kể ở bất kỳ α nào.** Điều này khớp với phép kiểm đồng nhất: phần hai chiều, không
  hoàn của luật C gần như không có việc khi không gán lại.
- Vẽ served theo đảo hứa (kiểu Hình 10 của Elmi): với gán lại, đường của luật C nằm về phía ít đảo hứa ở cùng mức phục vụ,
  trên toàn dải α.
- **Giới hạn:** 10 ngày đã xem; một cấu hình (62 xe, P512, 2,0×); chế độ plain; không kiểm định. Đây là phép thăm dò "vì
  sao 35%", không phải xác nhận.
