# P5: kết quả (25/10/2026, 06:05)

Theo protocol đã niêm phong 00:33 (`P5-SEAL.sha256`; 4 tệp niêm phong kiểm lại vẫn OK sau khi chạy). Báo cáo máy:
`E:/RideBoundData/research/p5-via-adapter-v1/report/P5-REPORT.txt` và `P5-RESULTS.json`.

## Cổng 0: ĐẠT

840/840 dòng plain chạy ở nhà giống hệt dòng xác nhận wf15 của công ty, ở mọi trường trừ `part`, `set`, `ms`, `msScore`.

## Đổi chế độ nhận dạng làm đổi từng khách, nhưng không đổi kết luận chính

- Số ô giống hệt giữa plain và adapter chỉ 1–9 trên 60, tùy nhánh. Vậy chế độ nhận dạng đổi kết quả ở mức từng khách,
  khớp với con số 0/20 ô của wf9-B3.
- Ở mức ngày, các phép so chính **giữ dấu** trên 10 ngày:

| Cấu hình | Phép so (trung bình mỗi ngày) | served plain → adapter | visRev60 (đảo hứa) | pDispPct | breachLedger |
|---|---|---|---|---|---|
| G2 | RCE35 − EOHZ35 (**chính**) | −4,13 → −3,47 | −14,90 → −16,13 | −0,30 → −0,53 | −28,6 → −29,5 |
| G2 | SHRC71 − SHHZ71 (**chính, Shadow**) | −9,83 → −10,10 | −15,73 → −14,97 | −0,60 → −0,44 | −39,8 → −40,6 |
| G2 | RCE35 − EO35 | −2,73 → −1,53 | −14,50 → −16,20 | −0,36 → −0,49 | −28,8 → −30,3 |
| INS | RCE35 − EOHZ35 | +0,13 → +1,77 | −2,00 → −1,33 | +0,28 → +0,47 | −7,9 → −8,1 |
| INS | SHRC72 − SHHZ72 | −2,00 → −2,07 | −0,70 → −0,07 | −0,30 → −0,09 | −5,9 → −5,8 |
| INS / G2 | SHRC − SHF | +35,0 / +29,4 → +35,3 / +29,0 | | | |

Lợi thế giả của SHRC so với SHF vẫn còn nguyên ở chế độ adapter. Đó là artefact của hàng rào chặt, không do chế độ nhận dạng
(KT:87, 303).

## Các ô bị đánh dấu theo quy tắc niêm phong: 7/84

| Phép so | Chỉ số | plain | adapter | Nhãn | Đọc thế nào |
|---|---|---|---|---|---|
| INS RCE35 − EOHZ35 | late60First | −1,13 | +0,80 | FLIP | **Bỏ** mọi câu "luật C ít trễ hơn Elmi nới khi chỉ chèn". Hiệu cỡ 1 khách mỗi ngày |
| INS EOHZ35 − EO35 | visRev60 | +0,33 | −3,6·10⁻¹⁶ | FLIP | Theo quy tắc là FLIP. Thực chất giá trị adapter bằng 0 do sai số dấu phẩy động |
| INS EOHZ35 − EO35 | breachLedger | −0,10 | 0 | ZERO | Gần 0 ở cả hai chế độ |
| G2 EOHZ35 − EO35 | visRev60, pDispPct, late60First, TV5 | +0,40; −0,07; −0,87; +1,04 | −0,07; +0,03; +0,33; −1,81 | FLIP | Khác biệt giữa hai hàng rào Elmi gần 0 và không bền theo chế độ. **Không viết** kết luận nào về hiệu của hàng rào nới so với chặt, trừ served (+1,4 → +1,9, giữ dấu) |

## Kết luận cho khóa luận

1. **Kết luận chính đứng vững** khi đổi sang chế độ adapter, tức chế độ đã được bắc cầu với RideBound thật:
   - với G2, luật C ít đảo hứa hơn Elmi hàng rào nới (−15 đến −16 lần mỗi ngày trên 10 ngày) và phục vụ ít hơn khoảng 3,5–4
     khách mỗi ngày;
   - Shadow cộng luật C cũng vậy so với Shadow cộng hàng rào nới.
2. **Độ lớn có đổi.** Ví dụ INS RCE35 − EOHZ35 served từ +0,13 thành +1,77; INS SHRC − SHHZ TV5 từ −35 thành −12 phút. Theo
   quy tắc của kiểm toán, phải báo cả hai con số.
3. **Phải bỏ hai loại câu:**
   - về trễ (late60First) của luật C so với Elmi nới khi chỉ chèn;
   - mọi hiệu nhỏ giữa EOHZ35 và EO35 ngoài số khách phục vụ.
4. **Giới hạn.** 10 ngày đã xem, thăm dò sau kiểm toán; không có kiểm định thống kê. Bắc cầu từng khách vẫn chỉ có trên ô
   Manhattan nhỏ, chưa có ở cỡ Via P512.
