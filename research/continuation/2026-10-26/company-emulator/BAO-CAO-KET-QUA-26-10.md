# Ba kết quả mới ngày 26/10: hệ đầy đủ, mốc Alonso-Mora, tốc độ RideBound

**Mọi số ở đây là thăm dò sau kiểm toán**, chạy trên bộ mô phỏng ở máy công ty:
- giả thuyết do AI viết, không có dấu thời gian ngoài;
- ngày đã xem trước.

Mỗi phần đều có:
- kế hoạch khóa sha256 trước khi chạy;
- cổng tái lập số cũ;
- một script tính lại độc lập;
- một lượt phản biện. Các chỗ phản biện sửa đã được đưa vào dưới đây.

Mã và số gốc nằm ở thư mục `wf20/`, đóng trong `06-Bo-sung/RideBound-wf20-Ket-Qua-Moi-26-10.zip`.

---

## 1. Chạy cả ba cùng lúc: Shadow + luật C + dự báo vòng kín

**Đã chạy:**
- 30 ngày đã có chỉ mục dự báo (F14 + F16), 180 ô; INS và G2; không dự báo (N) và dự báo vòng kín (CL).
- Các nhánh: SHF, SHHZ, SHRC, cùng EOHZ35 và RCE35 làm tham chiếu.
- 3.600 lượt chạy, 0 lỗi.

**Cổng, tất cả qua:**
- các dòng Shadow chế độ N trùng wf15, 504/504 (trùng ở mức chỉ số);
- các dòng EOHZ35/RCE35 trùng wf17 R3, 1.440/1.440, trùng tới từng khách;
- SHRC có 0 lần vượt sổ.

**Ngưỡng Shadow đóng băng** ở 72/71%. Ngưỡng này được hiệu chỉnh để khớp EO35 khi không dự báo, và không hiệu chỉnh lại cho chạy có dự báo.

**Phép so chính: luật C so với hàng rào nới, cùng có Shadow và dự báo (SHRC_CL − SHHZ_CL), mỗi ngày:**

| | G2 (gán lại) | INS (chỉ chèn) |
|---|---|---|
| Phục vụ | **−8,9** (27/30 ngày ít hơn) | **−3,5** (25/30) |
| Đảo hứa | −13,8 (−42,5%, 30/30) | −0,34 (không ý nghĩa) |
| Trễ > 60 s so với giờ hứa đầu, trên 1000 khách | **+4,0** (24/30 ngày xấu hơn) | +0,4 (không ý nghĩa) |
| P(điều phối dời giờ), trên 1000 | −4,8 | −1,3 (không ý nghĩa) |
| TV5 (giây mỗi khách) | −18 | +0,3 |
| Vượt sổ, trên 1000 | 0 so với 46,8 | 0 so với 4,4 |

- **Có Shadow, luật C đắt hơn:** dưới G2 mất thêm 6,5 khách/ngày so với khi không có Shadow, trong khi mức giảm đảo hứa gần như bằng nhau.
- **Khi chỉ chèn (chế độ thật của RideBound)**, luật C không giảm được đảo hứa mà mất 3,5 khách/ngày.

**Dự báo thêm gì khi có Shadow:**
- **Lợi:** ở cả 6 ô, cả ngày bớt 32–38 khách trễ trên 1000 (khoảng −13 đến −15%, gần bằng khi không có Shadow); đảo hứa −25 đến −48%; vi phạm hạn chót −78 đến −83%.
- **Hại:** **khung 17 giờ xấu thêm +17 đến +21 trên 1000**, ở 26–29/30 ngày, cả 6 ô. Khi không có Shadow, mức hại này là +37 đến +43.
- **Cả ngày**, dự báo không đổi phép so luật C − hàng rào. Nhưng buổi sáng nó thu hẹp mức cắt đảo của luật C 1,6/ngày (p 0,016), và buổi chiều bù lại.

**Bảng toàn hệ** (mức mỗi ngày; chỉ để mô tả, vì đổi ba thứ cùng lúc):

| | (a) Elmi như trong bài: SHHZ, chỉ chèn, không dự báo | (b) **Hệ đầy đủ**: SHRC + G2 + dự báo | (c) Đối chứng công bằng: SHHZ + G2 + dự báo | (d) Luật C + G2 + dự báo, không Shadow |
|---|---|---|---|---|
| Phục vụ | 695,1 | 694,7 | **703,6** | 700,0 |
| Đảo hứa | 26,8 | 18,7 | 32,5 | **12,1** |
| Trễ > 60 s trên 1000, cả ngày | 243,5 | 211,5 | **207,5** | 277,3 |
| …chỉ khung 17 giờ | **183,9** | 210,2 | 208,1 | 302,1 |
| P(dời) trên 1000 | **198,7** | 205,3 | 210,1 | 305,1 |
| Vượt sổ trên 1000 | 6,2 | **0** | 46,8 | **0** |

**So hệ đầy đủ (b) với Elmi như trong bài (a):**
- **Tốt hơn:**
  - phục vụ như nhau;
  - đảo hứa −30%. Nhưng toàn bộ mức giảm đến từ khung chiều; buổi sáng (b) đảo **nhiều hơn** (8,1 so với 2,5/ngày, 26/30 ngày);
  - trễ cả ngày −32 trên 1000;
  - vượt sổ 0.
- **Xấu hơn:**
  - khung 17 giờ trễ +26 trên 1000 (30/30 ngày);
  - P(dời) +6,6 trên 1000 (27/30 ngày);
  - khung 17 giờ có thêm vi phạm hạn chót (+4,2 trên 1000) và khách bảo đảm trễ (0,40 → 1,66/ngày).

**Kết luận: "hệ đầy đủ" không thắng toàn diện.** Mỗi thành phần mua một thứ và trả một thứ:
- Shadow giảm trễ và P(dời) nhưng tăng đảo hứa (+54% so với không có Shadow);
- luật C giảm đảo hứa nhưng mất khách;
- dự báo giảm trễ cả ngày nhưng làm hại khung chiều.

Không tổ hợp nào hơn trên mọi chỉ số.

**Giới hạn:**
- ngày đã xem;
- ngưỡng Shadow không hiệu chỉnh lại;
- các phép so ở cùng ngưỡng, không phải cùng mức phục vụ;
- chỉ mục dự báo không nhân quả, lại cùng nguồn với thế giới mô phỏng, nên dự báo được lợi lạc quan;
- **chỉ trong bộ mô phỏng**: RideBound không có Shadow.

Báo cáo đầy đủ: `wf20/fullsys/REPORT-VI.md`.

### 1b. So theo yêu cầu của chủ: hệ của mình (Shadow + luật C + dự báo) với Shadow + hàng rào nới (không dự báo)

- Dự báo chỉ gắn cho hệ của mình. Hai bên cùng chế độ điều phối.
- Số tính lại từ 3.600 lượt chạy đã có, không chạy thêm. Nguồn: `wf20/ours-vs-theirs/OURS-VS-THEIRS.md`, đầy đủ theo từng khung giờ, khoảng tin cậy và so thêm với hàng rào chặt.
- Thăm dò sau kiểm toán.

| Cả ngày, 30 ngày | G2: của mình | G2: đối thủ | **G2: hiệu** | INS: của mình | INS: đối thủ | **INS: hiệu** |
|---|---|---|---|---|---|---|
| Phục vụ | 694,7 | 703,5 | **−8,8** (28/30 ngày ít hơn) | 693,1 | 695,1 | **−2,0** (21/30) |
| Đảo hứa | 18,7 | 46,2 | **−27,5** (30/30 ngày tốt hơn) | 14,0 | 26,8 | **−12,7** (27/30) |
| Trễ > 60 s /1000 | 211,5 | 245,6 | **−34,0** (29/30) | 207,3 | 243,5 | **−36,2** (30/30) |
| Vi phạm hạn chót Elmi /1000 | 5,7 | 32,6 | **−26,9** (29/30) | 6,1 | 33,4 | **−27,3** (29/30) |
| Khách bảo đảm trễ | 3,6 | 21,5 | **−17,8** (29/30) | 3,9 | 21,4 | **−17,5** (29/30) |
| Vượt sổ /1000 | 0 | 48,7 | −48,7 | 0 | 6,2 | −6,2 |

- **Khung 17 giờ xấu hơn:** trễ +20,6 trên 1000 khi G2 (26/30 ngày), +21,5 khi INS (30/30 ngày). Vi phạm hạn chót và khách bảo đảm trễ cũng nhích lên ở khung này.
- **Khi chỉ chèn, buổi sáng hệ của mình đảo hứa nhiều hơn:** +3,2 khách/ngày (19/4 ngày).

**Lợi đến từ đâu** (tổng = phần luật C + phần dự báo):

| Cả ngày | G2: phần luật C | G2: phần dự báo | INS: phần luật C | INS: phần dự báo |
|---|---|---|---|---|
| Phục vụ | −8,9 | +0,1 | −3,5 | +1,5 |
| Đảo hứa | −13,8 | −13,6 | −0,3 | −12,4 |
| Trễ > 60 s /1000 | +4,0 | −38,0 | +0,4 | −36,5 |
| Vi phạm hạn chót /1000 | −1,0 | −25,9 | −0,3 | −27,0 |
| Khách bảo đảm trễ | −0,7 | −17,2 | −0,2 | −17,3 |
| Vượt sổ /1000 | −46,8 | −1,9 | −4,4 | −1,8 |

**Cách đọc:**
- Phần lớn mức giảm trễ, vi phạm hạn chót và khách bảo đảm trễ đến từ **dự báo**.
- Luật C đóng góp nửa mức giảm đảo hứa khi G2 và đảm bảo vượt sổ bằng 0. Đổi lại, luật C mất khách (−8,9 khi G2, −3,5 khi INS).
- Khi chỉ chèn, gần như toàn bộ lợi là của dự báo.
- **Hội đồng sẽ hỏi: dự báo không gắn riêng với luật C, sao không cho đối thủ dùng?** Vì vậy phép so "hệ của mình với đối thủ như bài báo" phải đi kèm bảng tách phần ở trên, và không được ghi phần lợi của dự báo cho luật C.
- **Giới hạn của dự báo:**
  - một phần mức giảm trễ là do dự báo hứa giờ trễ hơn (lời hứa dài hơn; xem wf17 R3);
  - chỉ mục dự báo không nhân quả, lại cùng nguồn với thế giới mô phỏng, nên kết quả lạc quan;
  - khung 17 giờ bị hại.

---

## 2. Mốc Alonso-Mora: 512 yêu cầu và 62 xe từ đâu, và đổi cỡ đội thì sao

### Sự thật về cấu hình
- **512 yêu cầu** là mẫu khoảng **11%** (8–20% tùy ngày) trong số khoảng 4.500 chuyến Via đủ điều kiện của mỗi khung 2 giờ. Về lịch sử, 512 = 4 × trần 128 yêu cầu của RideBound. Không bài báo nào đưa ra con số này.
- **62 xe** là đội nhỏ nhất để nhánh không giới hạn (U) phục vụ ít nhất 79,2% khách, tức bằng mức phục vụ của bộ ô Manhattan của RideBound. Quy tắc này được đặt trước, chạy chỉ chèn, trần 2,5×, trên 4 ô để riêng: 58 xe được 78,3%, 62 xe được 82,3%.
- **Alonso-Mora 2017 (AM17):** taxi Manhattan tháng 5/2013, khoảng 430.000 yêu cầu/ngày; đội 1.000/2.000/3.000 xe 4 chỗ; chờ tối đa 2–7 phút, trễ tối đa gấp đôi. 3.000 xe phục vụ 97–98%; 2.000 xe phục vụ 91–94%.
- **Fielbaum & Alonso-Mora 2020 (FA20):** cùng nhóm, và nghiên cứu đúng chủ đề "thay đổi giờ của khách". 19.610 yêu cầu trong 2 giờ, 2.000 xe → **9,8 yêu cầu/xe/2 giờ**; phục vụ khoảng 66%.
- **Của mình:** **8,3 yêu cầu/xe/2 giờ.** Chuyến Via dài hơn chuyến taxi, nên quy về "chuyến taxi tương đương" là 10,9–12,8, tùy cách đo độ dài.
- **So với AM17 ở cùng khung giờ** (ước lượng), tải mỗi xe của mình nhẹ hơn mọi đội của AM17 (từ 17 trở lên). Chỉ khi so với trung bình 24 giờ của AM17 thì mới nằm giữa đội 3.000 và đội 2.000. Tải của mình cao hơn FA20.
- **Mật độ:** số yêu cầu trên mỗi km² thấp hơn AM17 khoảng 70 lần, số xe trên mỗi km² thấp hơn 32 lần. Vì vậy **đừng nói đây là bản thu nhỏ của AM17.**

### Đã chạy
- 7 cỡ đội từ 20 đến 81 xe: 20 (giờ cao điểm AM17, ước lượng), 29 và 43 (trung bình AM17), 52 (FA20), 62 (của mình), 67 và 81 (đã chỉnh theo độ dài chuyến).
- 30 ngày × 2 khung; nhánh U, EO35, EOHZ35, RCE35; cả INS và G2.
- 3.360 lượt, 0 lỗi; cổng 32/32, 10/10, 480/480.

### Kết quả
- **Mọi nhãn kết luận giữ nguyên ở cả 7 cỡ đội:**
  - G2 giảm đảo hứa;
  - G2 mất ít khách;
  - khi chỉ chèn, luật C ≈ Elmi;
  - hàng rào giữ hạn chót tốt hơn.
- **Độ lớn tăng theo số xe.** Mức giảm đảo hứa dưới G2: 27% (20 xe) → 34% (29) → 36% (43, 52) → 41% (62) → 46% (81). Tính trên 1000 khách: từ −12,4 lên −26,4.
  - Nhỏ hơn rõ ở 20–43 xe;
  - sát ngưỡng ở 52 xe;
  - không khác 62 xe ở 67–81 xe.
- **Cái giá cũng tăng theo số xe.** Vi phạm hạn chót Elmi khi chỉ chèn tăng từ +2,5 lên +5,1/ngày; dưới G2 từ +2,6 lên +6,8/ngày. Lợi ích đảo hứa khi chỉ chèn thì gần như không đổi (−1,1 đến −2,0/ngày).
- **Đội tỉ lệ không tái tạo được mức phục vụ của các bài báo.** Ví dụ ở 29 xe, U phục vụ 43%, trong khi AM17 đạt 91%. Một phần là vì số của AM17 là trung bình 24 giờ, thời gian đi cố định, và có điều xe rỗng.

### Ý trả lời hội đồng (bạn tự viết lời)
- 512 là mẫu khoảng 11% cầu Via thật của mỗi khung. 62 xe đặt theo một mức phục vụ ghi trước (U khoảng 79%).
- Tải mỗi xe (8,3) gần FA20 (9,8), nghiên cứu cùng nhóm về thay đổi giờ của khách, và nhẹ hơn AM17 ở cùng giờ.
- Chạy lại ở các cỡ đội suy từ AM17 và FA20 (20–81 xe): mọi nhãn kết luận không đổi. Độ lớn hiệu ứng và cả cái giá đều tăng theo số xe.

---

## 3. Vì sao RideBound thật chậm hơn bộ mô phỏng nhiều đến vậy

- **Thời gian:** một job Via gộp khu mất 81–118 phút; bộ mô phỏng làm cùng ô trong 0,1–1 giây. Trên cùng máy, chỉ chèn chậm hơn khoảng 2–4 × 10⁴ lần, G2 chậm hơn khoảng 5–8 × 10³ lần.
- **Nguyên nhân lớn nhất: băm lại toàn bộ trạng thái kiểm toán ở mỗi epoch.**
  - Runner băm SHA-256 hai lượt toàn bộ trạng thái. Sổ ghi không bao giờ bị cắt, nên trạng thái lớn dần tới khoảng 400 MB.
  - Mô hình cho riêng việc băm 37–61 phút, tức 39–56% thời gian.
  - Thời gian chạy tăng theo kích thước sổ (R² 0,77), không theo số lần quyết định (R² 0,28).
  - Còn 20–35 phút chưa quy được nguyên nhân: có thể băm chậm hơn ở máy nhà, có thể do bộ thu gom rác. Chưa đo.
- **Hết bộ nhớ ở bản chốt cuối:** bước này giữ 5–7 bản sao trạng thái và chuẩn hóa 4 lượt. Runner cần 4,6–5,7 GiB, Python cần 2,3–3,2 GiB, nên 4/10 job hỏng ở đây.
- **Chi phí cố định khác:**
  - hai lô quyết định cho mỗi lần kích hoạt;
  - trao đổi giữa hai tiến trình bằng JSON chuẩn;
  - nhật ký base64 có băm;
  - dựng mô hình CP-SAT ở mỗi epoch;
  - FleetPy chạy bằng Python.

  Bộ mô phỏng thì chạy trong một tiến trình, dùng ma trận tính sẵn, liệt kê chính xác tối đa 2 yêu cầu mới, và không băm trạng thái.
- **Ô P512 gốc (824–858 điểm) không chạy được:** chỉ bảng đường đi đã mất 4.582 giây cho mỗi khung 15 phút, và dòng dữ liệu 134 MB vượt trần đầu vào 64 MiB của Runner.
- **Phải giữ, vì đó là thứ làm nên tính kiểm toán được:** tiến trình tách biệt, giao thức chuẩn, chuỗi băm từng quyết định, sổ đầy đủ, FleetPy độc lập, bộ giải tổng quát, nhật ký chính xác.
- **Có thể cắt mà không làm yếu tính kiểm toán** (ước lượng, chưa thử):
  1. **Script so khách chỉ đọc nhật ký**, không cần bản chốt cuối. Nhờ vậy so được 4 job đã hết bộ nhớ, gồm cả **luật C chỉ chèn ở cỡ Via**, **mà không cần chạy lại job**, vì máy nhà đã có nhật ký. Cổng: phải ra lại đúng 2.407/2.407 khách trên 6 job đã qua.
  2. Cờ `RIDEBOUND_STATE_HASH=v2` (đã có, còn là nguyên mẫu) bỏ việc băm toàn trạng thái. Lợi bao nhiêu còn tùy phần 20–35 phút chưa rõ kia.
  3. Bản chốt ghi theo luồng (đổi mã C#).
  4. Băm mã định danh trạng thái thay vì băm cả khối byte.
  5. Một lô cho mỗi lần kích hoạt.
  6. Nén nhật ký.
  7. Máy có CPU hỗ trợ SHA, RAM 64 GB.
  8. Đo trước bằng `RIDEBOUND_HASH_TIMING`.
- **Trả lời "sao không dùng RideBound cho mọi phép đo":**
  - chạy lại hết các lượt mô phỏng đã làm (ít nhất 15.556) bằng RideBound thật sẽ mất khoảng 290–425 ngày-job;
  - các ô P512 gốc thì không chạy được;
  - Shadow không có trong mã C#.

  Vai trò của RideBound thật là **chứng minh bộ mô phỏng làm đúng**: 244 cặp kịch bản–luật trùng hoàn toàn (238 Manhattan + 6 Via gộp khu).

---

## 4. Dự báo có hơn "hứa dài hơn" không (N7, chạy ngày 26/10)

Thăm dò, bộ mô phỏng, 30 ngày F14 + F16. Đã có kiểm tra độc lập: mọi số tái lập đúng, cổng và thứ tự thời gian sạch. Chi tiết ở `wf21/n7/REPORT-VI.md`, gồm cả mục "Sửa sau kiểm tra".

**Đã làm:**
- So dự báo vòng kín (CL) với một **đệm phẳng cùng độ dài lời hứa, đi qua điều phối** (PAD), trên hệ Shadow + luật C (SHRC) và Shadow + hàng rào nới (SHHZ).
- Dựng thêm **chỉ mục dự báo nhân quả** (CLc), chỉ dùng dữ liệu trước ngày đó.
- 3.600 lượt chính, 0 lỗi.

**Kết quả cho hệ của mình (SHRC), trễ > 60 s trên 1000 khách, dự báo so với đệm cùng độ dài:**

| | Gán lại (G2) | Chỉ chèn (INS) |
|---|---|---|
| Khung 8 giờ | **−14 đến −18** (vững) | **−13** |
| Khung 17 giờ | −6, không rõ; hai nhóm ngày ngược nhau | −6, không rõ |
| Cả ngày | −10 (mong manh: 13/15 cách khớp đệm đạt) | −9 (mong manh) |

**Cách đọc:**
- **Phần lớn lợi về trễ là do hứa dài hơn.** Riêng đệm đã lấy khoảng 85% mức giảm trễ buổi sáng, và khoảng 3/4 cả ngày. Phần "thông tin dự báo" chỉ thêm được khoảng 1/4.
- **Buổi chiều, lời hứa không dự báo vẫn trễ ít nhất** (191 so với 210/1000 của dự báo). Cả dự báo lẫn đệm đều hứa ngắn lại khi đường thông dần.
- **Dự báo còn có lợi ngoài độ dài:**
  - ít khách đến sớm (−19/1000);
  - ít đổi giờ hứa (TV5 −9 đến −11 s);
  - ít đảo hứa (−6/ngày, toàn bộ ở buổi chiều);
  - ít vi phạm hạn chót (−6/1000);
  - không mất khách.

  Riêng chỉ số "ít đến sớm" bị thổi phồng một phần, vì đệm phẳng làm khách đến sớm nhiều lên ở buổi sáng.
- **Chỉ mục nhân quả cho kết quả gần như y hệt.** Chỗ rò dữ liệu theo tháng không tạo ra lợi giả.
- **Giới hạn quan trọng:** phép so này cho thấy "kéo dài lời hứa theo hình dạng giờ trong ngày" tốt hơn "kéo dài đều". Nó **chưa** cho thấy thông tin riêng của từng ngày có giá trị. Một hồ sơ cố định theo giờ (trung bình nhiều tuần) dự báo tốt ngang prev4, nhưng chưa được chạy qua điều phối. Ngoài ra, thế giới mô phỏng và dự báo lấy cùng một nguồn dữ liệu, nên lợi của dự báo là cận trên.

**Câu có thể viết:** "Trong bộ mô phỏng, lời hứa có dự báo giảm số khách trễ buổi sáng nhiều hơn một khoảng đệm cùng độ dài (khoảng 14–18 trên 1000 khách), nhưng phần lớn lợi ích đến từ việc hứa dài hơn; buổi chiều không hơn và còn trễ hơn lời hứa không dự báo."
