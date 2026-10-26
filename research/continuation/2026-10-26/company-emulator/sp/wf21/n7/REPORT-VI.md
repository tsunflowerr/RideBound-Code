# Dự báo vòng kín có hơn "hứa dài hơn" không? Đối chứng đệm cùng độ dài đi qua điều phối (wf21/n7, mục kiểm toán §6-N7)

> Bản lưu báo cáo của người chạy ngày 26/10/2026. Kết quả kiểm tra độc lập (`wf21/verify-n7/`) được ghi ở mục "Sửa sau kiểm tra" cuối tệp khi có.

**Mọi số ở đây là thăm dò sau kiểm toán, không phải kết quả xác nhận.** Lý do:
- cả 30 ngày đều đã được chạy trước đây: F14 ở wf14, wf15, wf17, wf20; F16 ở wf17 R3 và wf20;
- giả thuyết và dải dự đoán do AI viết sau kiểm toán;
- không có dấu thời gian bên ngoài, chỉ có PLAN-HASH.txt trên máy;
- giá trị p chỉ để mô tả, không hiệu chỉnh nhiều phép thử.

**Quy ước:**
- K = `.../scratchpad/wf21/n7`.
- "/1000" là trên 1000 khách được phục vụ.
- Ngày = khung 08 + khung 17, mỗi khung là trung bình 3 lần lấy mẫu lại.
- Gộp = 30 ngày (F14 có 14 ngày, F16 có 16 ngày).
- Trễ = đến muộn hơn giờ hứa đầu quá 60 s. Sớm = đến sớm hơn giờ hứa đầu quá 60 s.

**Các chế độ hứa:**
- **N:** không dự báo.
- **CL:** dự báo vòng kín, chỉ mục hiện có.
- **PAD:** đệm phẳng cùng độ dài, đi qua điều phối.
- **CLc:** dự báo vòng kín, chỉ mục nhân quả.
- **PADc:** đệm khớp độ dài với CLc.

## 1. Trả lời ngắn

1. **Khung 08 (buổi sáng): CÓ, dự báo thắng đệm cùng độ dài.**
   - SHRC G2: trễ −14,4/1000 [−22,8; −6,0], 23/30 ngày, p 0,005.
   - SHRC INS: −12,8/1000 [−20,6; −4,9], 21/30 ngày, p 0,043.
   - Cùng dấu ở F14 và F16.
   - Nhưng phần lớn lợi buổi sáng của dự báo (so với N) là do hứa dài hơn: riêng đệm đã lấy −83 trong tổng −97/1000, khoảng 85%.
2. **Khung 17 (buổi chiều): KHÔNG khác rõ.**
   - SHRC G2 −6,1 [−14,6; +2,4], 17/30 ngày, p 0,59; SHRC INS −5,8, p 0,59.
   - F14 ngược dấu (+4,5 và +6,8, tức đệm tốt hơn); F16 thì −15 đến −17.
   - Cả dự báo lẫn đệm đều làm buổi chiều trễ hơn N (+19 và +25/1000), vì cả hai đều hứa ngắn lại.
3. **Cả ngày: dự báo hơn đệm −9 đến −10/1000 (p 0,016–0,043), nhưng mong manh.** Gần như toàn bộ đến từ F16 (−16); F14 chỉ −0,7 đến −2,6.
4. **Với chỉ mục nhân quả, kết quả không đổi.**
   - CLc gần trùng CL: mọi chênh lệch trễ ≤ 1,5/1000, không có ý nghĩa.
   - Sai số MAE15: 2,871% so với 2,874% (khung 08), 3,066% so với 3,071% (khung 17).
   - CLc − PADc cho cùng bức tranh: khung 08 −19,5 (G2) / −15,2 (INS); cả ngày −10,9 / −11,5; khung 17 −3,2 / −8,5, không có ý nghĩa.
5. **Lợi rõ nhất của thông tin dự báo (ngoài độ dài) không nằm ở số khách trễ**, mà ở:
   - ít khách đến sớm: −19/1000, 27/30 ngày;
   - ít đổi giờ hứa: TV5 −9 đến −11 s mỗi khách;
   - ít đảo giờ hứa: −6/ngày, toàn bộ ở khung 17;
   - ít vi phạm hạn chót: −6/1000;
   - ít khách bảo đảm trễ: −4/ngày.

   Không mất khách phục vụ.
6. **Với SHHZ (luật của đối thủ),** lợi về trễ nhỏ hơn và không rõ ở mức cả ngày (G2 −4,7, p 0,59).

## 2. Đã làm gì

**Thiết kế (K/PLAN.md):**
- 2 cấu hình (INS, G2) × 2 nhánh (SHRC = luật C; SHHZ = Shadow + sàn Hörl–Zwick của đối thủ) × 180 ô × 5 chế độ = **3.600 lượt chính**.
- 180 ô = F14 (14 ngày × 2 khung × 3 lần lấy mẫu) + F16 (16 × 2 × 3).

**N và CL:**
- Chạy lại toàn bộ 1.440 lượt SHRC/SHHZ của wf20, để có thêm trường độ dài lời hứa và đến sớm.
- 1.440/1.440 trùng wf20 trên mọi trường cũ.

**PAD (đệm phẳng đi qua điều phối):**
- Đây là bản vá "trôi phẳng" P3 của wf17 (giống CLF). Ô 15 phút hiện tại có hệ số 1; mọi ô sau nhân một hệ số không đổi r. Như vậy PAD chỉ khác CL ở chỗ bỏ hình dạng dự báo theo ngày và theo giờ.
- r chọn riêng cho từng cấu hình × khung × nhánh, sao cho độ dài lời hứa đầu trung bình (p0 − giờ đặt xe), gộp 30 ngày, bằng của CL cùng nhánh. Lúc hiệu chỉnh chỉ đọc độ dài lời hứa.
- Lệch còn lại ≤ 0,5 s ở 7/8 nhóm; riêng G2 khung 17 SHRC lệch −0,60 s.
- r: khung 08 từ 1,030 đến 1,035; khung 17 từ 0,980 đến 0,982.
- Khớp trên cả 30 ngày, vì đệm là công cụ cân độ dài, không phải dự báo. Cái giá: trong từng nhóm ngày, độ dài lệch ±2–5 s (mục 10).

**CLc (chỉ mục nhân quả):**
- Chỉ mục cũ: mỗi tháng làm một lần median polish trên **cả tháng**, kể cả ngày sau ngày đích.
- Chỉ mục nhân quả: với ngày đích ở tháng M, tham chiếu là polish của tháng M−1.
  - Ngày nguồn thuộc tháng M−1: giữ nguyên.
  - Ngày nguồn thuộc tháng M (trước ngày đích): chấm theo hệ số OD của tháng M−1, không ước lượng lại.
- Bộ dựng chạy theo luật cũ ra đúng `accuracy-index-2019.json` (14.688/14.688), và đúng hồ sơ CL ở 60/60 ngày-khung.
- Trọng số CLc khác CL: trung vị 0,27% (khung 08) và 0,23% (khung 17); lớn nhất 0,92%.

**PADc:** CLc hứa ngắn hơn CL tới 1,82 s, vượt ngưỡng 1 s đặt trước, nên chạy thêm đệm khớp với CLc. 8/8 nhóm khớp trong 0,5 s.

**RN (lời hứa không dự báo hiệu chỉnh lại theo giờ): bỏ qua.**
- Muốn đưa qua điều phối phải có vòng phản hồi ngày nối ngày, mà 30 ngày ở đây không liền nhau.
- Một hồ sơ theo giờ học từ các tuần trước thì chính là prev4.

**Khối lượng:** 8.466 lượt (3.600 chính, 4.770 hiệu chỉnh, 96 cổng), 0 lỗi, 54 phút.

## 3. Cổng (tất cả qua; K/gates/GATES-ALL.txt)

| Cổng | Kết quả |
|---|---|
| CI-0 dựng lại polish 5 tháng | Mọi cột dùng cho chỉ mục trùng; chỉ cột trung bình lệch 2·10⁻¹³ |
| CI-1 luật cũ = chỉ mục hiện có | 14.688/14.688 |
| CI-2 hồ sơ theo luật cũ = hồ sơ CL | 60/60 |
| CI-3 chỉ mục nhân quả chỉ dùng dữ liệu trước ngày đích | Qua |
| G1 N/CL chạy lại = wf20 | 1.440/1.440 |
| G2 PAD với r = 1 = N (tới từng khách) | 24/24 |
| G3 đường mã CLc với chỉ mục cũ = CL | 24/24 |
| G4 SHRC 0 vượt sổ | 1.800/1.800 |
| G5 x\* = 72/71 | 3.600/3.600 |
| G6 CLc = CL ở 6 ngày có hồ sơ trùng | 144/144 |
| G7 đủ ô, không trùng | 20 nhóm × 180 ô |
| G9 chạy lại y hệt | 48/48 và 1.440/1.440 |

## 4. Bảng chính: CL − PAD, trễ/1000

Gộp 30 ngày. Mỗi ô ghi: trung bình [khoảng tin cậy 95%], số ngày âm/dương, p; trong ngoặc là trung bình F14 / F16.

| Nhánh, cấu hình | Cả ngày | Khung 08 | Khung 17 |
|---|---|---|---|
| SHRC G2 | −9,94 [−16,53; −3,35] 21−/9+ p 0,043 (−2,6 / −16,4) | −14,41 [−22,81; −6,02] 23−/7+ p 0,005 (−9,8 / −18,5) | −6,12 [−14,60; +2,36] 17−/13+ p 0,59 (+4,5 / −15,4) |
| SHRC INS | −8,97 [−15,19; −2,74] 22−/8+ p 0,016 (−0,7 / −16,2) | −12,76 [−20,64; −4,87] 21−/9+ p 0,043 (−8,3 / −16,6) | −5,81 [−14,93; +3,31] 17−/13+ p 0,59 (+6,8 / −16,9) |
| SHHZ G2 | −4,67 [−11,22; +1,89] p 0,59 (+2,9 / −11,3) | −8,61 p 0,36 (−2,6 / −13,8) | −1,13 p 0,36 (+8,8 / −9,8) |
| SHHZ INS | −8,14 [−14,35; −1,92] p 0,20 (−0,2 / −15,1) | −17,15 p 0,005 (−12,4 / −21,3) | +0,21 p 1,00 (+11,7 / −9,9) |

## 5. Chỉ số phụ: CL − PAD cho SHRC (G2 | INS)

| Chỉ số | G2 | INS |
|---|---|---|
| Sớm > 60 s /1000, cả ngày | −18,58 (27−/3+) | −19,92 (27−/3+) |
| Đảo giờ hứa/ngày | −6,49 (23−/6+, p 0,002); khung 08 −0,28, khung 17 −6,21 | −5,93 (p 0,008) |
| TV5 (giây/khách) | −10,73 | −8,87 |
| Vi phạm hạn chót /1000 | −6,38 (p 0,005) | −6,06 (p 0,043) |
| Khách bảo đảm trễ/ngày | −4,03 (p 0,024) | −3,78 (p 0,099) |
| Phục vụ/ngày | +1,36 (p 0,20) | +3,34 (p 0,087) |

**Mức của SHRC G2** (cả ngày | khung 08 | khung 17):

| Chế độ | Trễ/1000 | Sớm/1000 | Độ dài hứa (s) |
|---|---|---|---|
| N | 249,4 \| 309,9 \| 191,3 | 42,9 \| 7,8 \| 77,4 | 1582 \| 1523 \| 1644 |
| PAD | 221,5 \| 227,1 \| 216,3 | 33,8 \| 37,3 \| 31,6 | 1586 \| 1550 \| 1625 |
| CL | 211,5 \| 212,7 \| 210,2 | 15,2 \| 14,2 \| 16,6 | 1587 \| 1550 \| 1626 |
| CLc | 211,6 \| 211,2 \| 211,7 | 14,4 \| 13,0 \| 16,3 | 1586 \| 1549 \| 1626 |

- Ở khung 17, N trễ ít nhất, nhưng 77/1000 khách đến sớm quá 60 s, vì lời hứa quá dài khi đường thông dần.
- CL và PAD đều đổi "sớm" lấy "trễ"; CL đổi khéo hơn.

## 6. Riêng hứa dài hơn mua được gì (SHRC G2)

| Chỉ số | PAD − N | CL − N | Phần do độ dài |
|---|---|---|---|
| Trễ, cả ngày | −27,97 | −37,91 | khoảng 74% |
| Trễ, khung 08 | −82,86 | −97,27 | khoảng 85% |
| Trễ, khung 17 | +24,99 | +18,87 | đệm còn hại hơn |
| Sớm | −9,13 | −27,70 | khoảng 33% |
| Đảo giờ hứa | −6,33 | −12,82 | khoảng 49% |
| Vi phạm hạn chót | −20,66 | −27,04 | khoảng 76% |

## 7. Chỉ mục nhân quả (CLc − CL)

- Trễ/1000 cả ngày: −0,63 đến +0,08.
- Lớn nhất là SHRC G2 khung 17: +1,53 (p 0,064).
- Kết luận: chỗ "không nhân quả" của chỉ mục cũ không tạo ra lợi giả trong mô phỏng này.

## 8. Tách phần hệ của chủ đề tài (G2)

SHRC_CL − SHHZ_N = phần luật C (SHRC_N − SHHZ_N) + phần độ dài (SHRC_PAD − SHRC_N) + phần thông tin (SHRC_CL − SHRC_PAD):

| Chỉ số | Tổng | Luật C | Độ dài | Thông tin |
|---|---|---|---|---|
| Phục vụ/ngày | −8,84 | −10,02 | −0,18 | +1,36 |
| Trễ/1000, cả ngày | −34,04 | +3,87 | −27,97 | −9,94 |
| Trễ, khung 08 | −91,51 | +5,76 | −82,86 | −14,41 |
| Trễ, khung 17 | +20,56 | +1,69 | +24,99 | −6,12 |
| Đảo giờ hứa/ngày | −27,46 | −14,63 | −6,33 | −6,49 |
| TV5 (s) | −38,11 | −18,76 | −8,61 | −10,73 |
| Sớm/1000 | −46,95 | −19,25 | −9,13 | −18,58 |
| Vi phạm hạn chót/1000 | −26,89 | +0,16 | −20,66 | −6,38 |

- **INS:**
  - trễ −36,15 = +1,63 − 28,81 − 8,97;
  - đảo −12,72 = −0,57 − 6,22 − 5,93;
  - phục vụ −2,01 = −4,43 − 0,92 + 3,34.
- **Cách đọc:**
  - mức giảm trễ của hệ đến hoàn toàn từ phần lời hứa: khoảng 3/4 do độ dài, 1/4 do thông tin dự báo;
  - luật C giảm đảo giờ hứa (khi G2), nhưng làm trễ nhích lên và mất khoảng 10 khách/ngày (so ở chế độ N).

## 9. Dự đoán

- 88 nhãn gộp: 74 GIỮ, 7 YẾU ĐI, 2 KHÔNG KẾT LUẬN, 4 ĐẢO CHIỀU, 1 MẠNH HƠN.
- **ĐẢO CHIỀU (P6):** đã đoán dự báo không giảm đảo hơn đệm, nhưng thực tế giảm thêm 5,9–7,5/ngày.
- Dải dự đoán rộng, nên nhãn GIỮ dễ đạt.

## 10. Kết quả bất lợi

1. Ở F14, lợi về trễ của dự báo so với đệm gần như bằng 0. Ở khung 17 của F14, đệm còn tốt hơn. Kết quả "có" ở mức cả ngày dựa chủ yếu vào F16.
2. Buổi chiều, dự báo không thắng đệm về trễ. Cái hại khung 17 chủ yếu do hứa ngắn lại.
3. 85% lợi buổi sáng và khoảng 74% lợi cả ngày về trễ chỉ là do hứa dài hơn.
4. Với luật của đối thủ (SHHZ), phần thông tin về trễ nhỏ hơn và không rõ ở mức cả ngày.
5. Độ dài chỉ khớp ở mức gộp: ở khung 08, đệm dài hơn CL 2,4–5,1 s ở F14 và ngắn hơn 1,4–3,8 s ở F16.
6. Độ dài lời hứa không trơn theo r; "cùng độ dài" nghĩa là bằng nhau trong khoảng ±0,5 s.
7. Mã hiệu chỉnh có 2 lỗi làm vòng lặp bị kẹt (D3, D6). Cả hai chỉ dính tới độ dài, đã sửa và ghi lại.

## 11. Giới hạn

- Ngày đã xem; giả thuyết do AI viết; không có dấu thời gian ngoài.
- x\* đóng băng từ wf15.
- Chỉ trong bộ mô phỏng.
- Giao thông "thật" và dự báo lấy từ cùng chuỗi Yellow, nên giá trị thông tin có lẽ lạc quan.
- Đệm phẳng là đối chứng yếu; chưa thử đệm phóng to đều mọi đoạn (kể cả ô hiện tại), chưa thử RN.
- Luật nhân quả chỉ là một lựa chọn (tham chiếu tháng trước).

## Sửa sau kiểm tra độc lập (wf21/verify-n7, 26/10)

Mọi con số ở trên tái lập đúng bằng script riêng. Cổng, thứ tự thời gian, mã băm và việc hiệu chỉnh chỉ đọc độ dài lời hứa đều đúng. Các chỗ cần sửa:

1. **"CL − PAD" không đo "thông tin riêng của từng ngày".**
   - Đệm phẳng chỉ khớp độ dài trung bình, không khớp chỗ đặt thêm thời gian. Lúc 8 giờ CL kéo dài các ô sau thêm 4,5–15%, tới 9 giờ chỉ còn 1–3%; đệm thì luôn +3,1%.
   - 85% (khung 08) và 67% (khung 17) biến thiên trong trọng số của CL là hình dạng chung theo giờ trong ngày; chỉ 3–6% thay đổi giữa các ngày.
   - Một hồ sơ cố định theo giờ (lấy trung bình nhiều tuần) dự báo tốt ngang hoặc hơn prev4.
   - Vì vậy kết quả cho thấy **"trôi có hình dạng theo giờ" thắng "trôi phẳng"**. Nó chưa cho thấy thông tin của riêng từng ngày có giá trị (chưa thử).
2. **Khác biệt F14/F16 ở khung 08 phần lớn do lệch độ dài trong từng nhóm ngày.**
   - Đệm dài hơn CL 2,4–5,1 s ở F14 và ngắn hơn 1,4–3,8 s ở F16; số trễ gần như là hàm của r.
   - So với đệm cùng độ dài trong từng nhóm thì cả hai nhóm đều khoảng −10 đến −14/1000 cả ngày. Ở khung 08, F14 còn −20 đến −26.
   - Riêng khung 17 thì hai nhóm khác nhau thật (F14 − F16 khoảng +20/1000): nên viết là **"không đồng nhất"** thay vì "không khác".
3. **Kết quả cả ngày vẫn mong manh.**
   - SHRC G2: 13/15 tổ hợp đệm gần cùng độ dài đạt luật; SHRC INS: 2/4.
   - CLc so với đệm gốc: −9,86, p 0,099.
4. **Khung 08 vững:** với mọi đệm lệch ≤ 0,5 s, SHRC G2 khung 08 là −14,4 đến −18,0, p ≤ 0,016. Còn SHHZ G2 khung 08 không có ý nghĩa.
5. **Phần do độ dài:** 0,74 (G2) / 0,76 (INS) của CL − N; 0,82 / 0,80 của tổng "hệ của mình − đối thủ". Con số này còn phụ thuộc hình dạng đệm.
6. **Chỉ mục nhân quả:** CLc − CL lớn nhất là +1,53 ở SHRC G2 khung 17 (p 0,064). Phép thử chỉ cho thấy chỗ rò theo tháng là không đáng kể; nó không giải quyết chuyện dự báo và thế giới cùng nguồn dữ liệu.
7. **Kết quả bất lợi bị bỏ sót:**
   - ở khung 17, N trễ ít nhất (191 so với CL 210 và PAD 216/1000);
   - đệm phẳng làm số khách đến sớm ở khung 08 tăng gấp ba (7,8 → 37,3/1000), nên ưu thế "ít đến sớm" của CL bị thổi phồng;
   - P14 của SHRC G2 là 0,494, ngay dưới cận 0,5.
8. **Bỏ sót trong hồ sơ:** có chạy thử (smoke) trên một ô thật (2019-10-16 F16, INS) trước khi viết dự đoán, nhưng không ghi trong PLAN hay DEVIATIONS. Thông tin rất ít, nhưng có kết quả của PAD.
