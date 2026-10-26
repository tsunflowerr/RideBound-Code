# 24 chỗ cần sửa trong khóa luận (bản cho sinh viên tự viết, 26/10/2026)

**Phạm vi.** Khóa luận nằm ở repo `Report_INT3508`, nhánh `thesis/audit-fixes-2026-10-25`, commit `f1a0c73`, thư mục `ridebound/noi-dung/`. Số dòng tính ở commit đó.

**Cách dùng:**
- Với mỗi điểm: mở tệp ở đúng dòng, đọc câu "Đang viết", rồi tự viết lại bằng lời của bạn theo các ý ở mục "Cần viết".
- Số liệu lấy ở mục "Số và nguồn".
- Số trong các điểm có ghi **[CT]** là số chỉ có ở wf17, do máy công ty chạy, chưa ai chạy lại. Nếu dùng thì ghi nguồn và ghi "chưa kiểm độc lập". Muốn xem số gốc thì cần thư mục bản 24/10.
- Sửa xong mỗi điểm thì build lại PDF.

Viết tắt:
- **KT:** báo cáo kiểm toán ngày 23/10.
- **wf17:** báo cáo độ bền ngày 24/10.
- **TABLES:** tệp `research/continuation/2026-10-26/kits/p5-via-adapter-2026-10-25/TABLES-2x3-60days.txt` trong repo RideBound-Code.

Số thứ tự giữ như bảng trong tài liệu gửi máy nhà. Các điểm được xếp theo mức cần thiết.

---

## A. Phải sửa: câu đang sai hoặc tự mâu thuẫn

### 7. Câu phạm vi ở chương 1
- **Ở đâu:** `05-chuong1.tex:352-353`.
- **Đang viết:** "các lần chạy ở chương~4 không chuyển khách đã nhận sang xe khác".
- **Vì sao:** mục so với Elmi (08c) nằm *trong* chương 4 (`08-chuong4.tex:1253`) và có chế độ G2, tức là có chuyển khách đang chờ sang xe khác, cả trong bộ mô phỏng lẫn trong cầu nối Manhattan (`08c:164-169`). Đây là lỗi còn sót của kiểm toán (KT:562).
- **Cần viết:**
  - giới hạn câu vào các lần đánh giá chính của chương 4 (các mục trước mục so với Elmi);
  - nói rõ việc chuyển khách (G2) chỉ xuất hiện ở mục so với Elmi.

### 8. "RideBound và bộ mô phỏng không đổi"
- **Ở đâu:** `08c-via-elmi.tex:170-172`.
- **Đang viết:** "Để chạy được, lớp nối FleetPy được sửa ở hai chỗ, … RideBound và bộ mô phỏng không đổi."
- **Vì sao:** để chạy cầu nối với ngân sách theo từng khách và G2, các tính năng này đã được **thêm vào RideBound** trên nhánh nghiên cứu (ADR-081). RideBound ở chương 3 thì không có chúng.
- **Cần viết:**
  - "không đổi" chỉ đúng với hai chỗ sửa lớp nối;
  - ngân sách theo khách và G2 được thêm vào RideBound trên nhánh nghiên cứu, chỉ để chạy phép kiểm này, không thuộc phần RideBound được đánh giá ở chương 3–4.

### 9. "RideBound chưa chạy được ở cỡ đó" (đã cũ)
- **Ở đâu:** `08c-via-elmi.tex:175-179`.
- **Đang viết:** "Ở kịch bản Via 512 yêu cầu, các giới hạn này có thể ràng buộc, và RideBound chưa chạy được ở cỡ đó. Shadow và dự báo chỉ có trong bộ mô phỏng."
- **Vì sao:** ngày 26/10 RideBound thật đã chạy ở cỡ Via (ADR-085).
- **Cần viết:**
  - RideBound thật đã chạy trên kịch bản Via **gộp điểm theo khu TLC**, ngày 13/2/2019, hai ca, 509 yêu cầu, 62 xe. Kết quả trùng bộ mô phỏng từng khách ở 6/6 cặp (2.407 khách).
  - Muốn chạy được phải nâng ngân sách cấp quyết định và ngân sách bộ giải (cấu hình A1), vì ngân sách mặc định bị vượt ở 62 xe. Như vậy câu "các giới hạn này có thể ràng buộc" nay đã thành sự thật.
  - Chưa kiểm được: luật C chỉ chèn (hết bộ nhớ ở bản chốt cuối), không trần chỉ chèn, Shadow.
  - Chính các ô P512 gốc (824–858 điểm) thì RideBound không chạy được.
  - Bỏ chữ "dự báo", hoặc nói rõ là dự báo vòng kín.
- **Số và nguồn:** `docs/18` ADR-085 (`:5574-5579`, `:5608`, `:5613`, câu đề xuất ở `:5616-5619`).

### 20. "Luật giới hạn từng lần chưa được cài đặt"
- **Ở đâu:** `08-chuong4.tex:778` và `:1324`.
- **Đang viết:**
  - :778: "luật mỗi lần một trần chưa được cài đặt và chưa chạy";
  - :1324: "Luật giới hạn từng lần chưa được cài đặt".
- **Vì sao:** trần từng lần (trần sửa ETA mỗi quyết định) đã được cài trên nhánh nghiên cứu (ADR-080), và đã chạy trong cầu nối. `08c:167` liệt kê "trần từng lần" trong số các luật trùng hoàn toàn. Hai đoạn đang mâu thuẫn nhau.
- **Cần viết:** luật này đã được cài trên nhánh nghiên cứu và chạy trong phép kiểm cầu nối, nhưng không nằm trong nghiên cứu chính của chương 4 (nếu đúng như vậy). Nên kiểm lại với `docs/18` ADR-080.
- **Thêm:** máy nhà còn ghi hai lỗi "df = 5" và "Veterans Day" (DINH-CHINH dòng 2, nguồn R1 mục 5.1.6). Tìm chữ trong các tệp `.tex` ở f1a0c73 không thấy hai cụm này. Xem lại nguồn R1 để biết chính xác chỗ; có thể chúng nằm trong bảng hoặc hình, hoặc đã được sửa.

### 4. "Mọi hiệu giữ dấu" khi đổi cách định danh
- **Ở đâu:** `08c-via-elmi.tex:153-155` và `09-chuong5.tex:52-53`.
- **Đang viết:**
  - 08c: "Kết quả theo từng khách đổi ở hầu hết kịch bản, nhưng mọi hiệu ở đoạn trên giữ dấu …";
  - chương 5: "Hướng của các hiệu chính giữ nguyên khi bộ mô phỏng phá thế hòa theo cách của RideBound."
- **Vì sao:** trên mẫu 10 ngày của máy nhà (P5), các hiệu chính giữ dấu. Nhưng trên một mẫu 10 ngày khác (wf17 R1), hiệu **số khách phục vụ** đổi dấu. Chỉ hiệu đảo hứa thì giữ dấu ở cả hai mẫu.
- **Cần viết:**
  - hiệu đảo hứa (của luật C, và của Shadow với luật C) giữ dấu ở cả hai mẫu;
  - hiệu phục vụ vài khách/ngày có thể đổi dấu, nên coi là nhiễu và không rút kết luận từ nó;
  - sửa câu ở chương 5 cho khớp.
- **Số và nguồn:**
  - P5: đảo hứa −14,9 → −16,1; phục vụ −4,1 → −3,5 (`P5-RESULT.md`).
  - **[CT]** wf17 R1: phục vụ khi chỉ chèn −1,40 → +2,10; khi gán lại −1,50 → +1,20; đảo hứa −41,0% → −43,4% (`wf17/r1-identity-fence-pad/results/r1_tables.txt`).

### 13. "72% / 71% khách nhận ngân sách 0"
- **Ở đâu:** `08c-via-elmi.tex:47-48`.
- **Đang viết:** "khách có điểm thấp nhất (72\% với INS, 71\% với G2) nhận ngân sách 0".
- **Vì sao:** 72%/71% là ngưỡng phân vị hiệu chỉnh trên các ngày khám phá. Trên 60 ngày, số khách thực sự nhận ngân sách 0 là khoảng 68%.
- **Cần viết:** ngưỡng đặt ở phân vị 72/71 của điểm trên ngày khám phá; trên 60 ngày, khoảng 68% khách (67,6–69,1%) nhận ngân sách 0.
- **Số và nguồn:** KT §1-C10.

---

## B. Phải thêm: thiếu bối cảnh, dễ bị hội đồng hỏi

### 1. "Giảm 42% đảo hứa" đứng một mình
- **Ở đâu:** `02-tom-tat.tex:30` (tiếng Việt) và `:66-67` (tiếng Anh); `09-chuong5.tex:50-51`; `08c-via-elmi.tex:87-89`.
- **Đang viết (tóm tắt):** "Khi cho dời khách đang chờ sang xe khác, luật C giảm 42\% số lần đảo hứa, đổi lại phục vụ ít hơn 0,4\%."
- **Vì sao:** 42% là so với Elmi **cùng có gán lại**. Nhưng bảng của chính khóa luận (`08c:75-76`) cho thấy gán lại + luật C có **23,1** đảo/ngày, vẫn cao hơn trường hợp không gán lại + hàng rào nới (**21,7**). Hội đồng sẽ hỏi: vậy gán lại để làm gì?
- **Cần viết:**
  - luật C dập phần lớn số đảo hứa mà việc gán lại sinh ra;
  - so với không gán lại, gán lại + luật C vẫn nhiều hơn 1,4 lần đảo/ngày, nhưng phục vụ nhiều hơn 4 khách/ngày.
- **Số và nguồn:** đọc thẳng từ bảng `08c:75-76`.
  - Gán lại sinh thêm 39,8 − 21,7 = 18,1 lần đảo; luật C bỏ 39,8 − 23,1 = 16,7, tức khoảng **92%**.
  - Gán lại thêm 732,1 − 725,2 = 6,9 khách; luật C giữ 729,2 − 725,2 = 4,0, tức khoảng **58%**.

### 2. Bản chất của kết quả giảm đảo hứa
- **Ở đâu:** cùng các chỗ ở điểm 1; cộng đoạn kết quả `08c:86-93`.
- **Vì sao:** chiều của kết quả đến từ chính cơ chế của luật (tính |c| cả hai chiều, không hoàn lại). Chỉ số đảo hứa được chọn làm chỉ số chính sau thử nghiệm sơ bộ. Chưa nghiên cứu nào đo được khách coi trọng việc ít đảo hứa.
- **Cần viết:**
  - đây là kết quả theo cơ chế;
  - chỉ số đảo hứa được chọn sau thử nghiệm sơ bộ;
  - chưa có bằng chứng khách được lợi.
  - Có thể nêu một phép định giá **minh họa** **[CT]**: dưới gán lại, tính bằng tham số văn liệu, luật C gây tổn thất phúc lợi khoảng 0,95–1,34% doanh thu cước (riêng cước nhà vận hành thực mất 0,18–0,39%). Để hòa vốn, mỗi lần đảo tránh được phải đáng 12,8–17,8 phút ngồi xe, hoặc 7,4–7,8 phút nếu coi khách bị từ chối không tốn gì. Ghi rõ đây là minh họa, không phải số đo.
- **Số và nguồn:** KT:94, :99, :448; **[CT]** wf17 :64, :545-551, :572-575.

### 3. Câu đường cong α thiếu cái giá "trễ"
- **Ở đâu:** `08c-via-elmi.tex:91-93`.
- **Đang viết:** "… khi gán lại, luật C có ít hơn 33--55\% số lần đảo hứa so với hàng rào nới cùng $\alpha$, đổi lại phục vụ ít hơn 0,2--1,2\% …"
- **Vì sao:** câu này so ở **cùng α**. Nếu so khi hai luật **phục vụ cùng số khách** thì lộ thêm một cái giá: luật C làm thêm khách bị điều phối dời muộn.
- **Cần viết:**
  - đưa phép hòa vốn vào như một **điều kiện**: dưới gán lại, cùng số khách phục vụ, so với hàng rào nới, mỗi ngày luật C bớt khoảng 13,5 khách bị đảo hứa nhưng thêm khoảng 12,7 khách bị điều phối dời muộn quá 1 phút;
  - luật C chỉ đáng dùng nếu một lần đảo hứa khó chịu ít nhất bằng 0,94 lần bị dời muộn (khoảng tin cậy 0,46–1,61, chứa 1, nên chưa phân thắng thua theo số khách);
  - tính theo phút: khoảng 13,4–13,8 phút trễ thêm cho mỗi lần đảo tránh được;
  - khi chỉ chèn, không phân biệt được hai luật.
  - Ghi rõ: phép hòa vốn là thăm dò; kế hoạch được viết sau một lần tính thử (0,77) trên cùng 10 ngày.
- **Số và nguồn:**
  - `kits/breakeven-2026-10-26/BREAKEVEN-RESULT.md`, `BREAKEVEN-TABLE-v2.tsv:28` (số của máy nhà, đã được máy công ty chạy lại khớp).
  - **[CT]** wf17 R6 cũng thấy khoảng 1 khách trễ thêm cho mỗi 1–2 lần đảo tránh được. Đây là hậu kiểm, chỉ có ý nghĩa ở α 50 và 100.

### 5. Độ lớn của hiệu ứng đổi theo cấu hình
- **Ở đâu:** sau câu α (`08c:91-93`), hoặc đoạn giới hạn (`08c:188-190`).
- **Đang viết (đoạn giới hạn):** "Mọi hiệu đo trong một cấu hình (62 xe, 512 yêu cầu …); ở cấu hình khác, hướng có thể giữ nhưng độ lớn đổi."
- **Vì sao:** đã có số cụ thể cho câu này, nên ghi số thay vì chỉ nói chung chung.
- **Cần viết** (thăm dò) **[CT]**:
  - trên 16 ngày chưa từng chạy, mức giảm là −38,5%, cả 16/16 ngày;
  - qua các biến thể ma sát và chế độ vận hành ở α 35, mức giảm là 28–43%;
  - với phí chuyển xe 300 s còn −28,6%;
  - ở mật độ thấp (256 yêu cầu), mức giảm tính trên 1000 khách chỉ còn khoảng 1/3;
  - giả thuyết hậu kiểm, không phải kết quả: chỉ riêng việc gắn phí chuyển xe vào hàng rào đã đạt khoảng 56% mức giảm của luật C.
- **Số và nguồn:** wf17 :108, :258-265, :285-307, :319-327.

### 11. Còn thiếu hai kết quả bất lợi
- **Ở đâu:** đoạn kết quả bất lợi `08c-via-elmi.tex:129-135`.
- **Cần viết thêm:**
  - khi chỉ chèn, tổng thay đổi hiển thị (TV5) của luật C **tăng** khoảng 12,5 phút/ngày so với hàng rào chặt, và khoảng 5,6 phút/ngày (không có ý nghĩa) so với hàng rào nới;
  - ba dự đoán phụ của nghiên cứu Shadow bị trượt: S2-G2 phục vụ +2,28; S11-G2 TV5 −77,0; S4-INS phục vụ −3,33.
- **Số và nguồn:** TABLES, dòng TV5min của INS (1390,2 / 1397,1 / 1402,7); khoảng tin cậy **[CT]** wf17 :73; KT:635.

### 12. Câu Shadow so với cách siết đều còn thiếu một ưu điểm của Shadow
- **Ở đâu:** `08c-via-elmi.tex:144-145`.
- **Đang viết:** "So với cách siết đều, Shadow giảm khoảng 10 điểm phần trăm số khách bị điều phối lùi giờ trả, nhưng có nhiều đảo hứa hơn và nhiều khách trễ hạn hơn."
- **Vì sao:** "trễ hạn" ở đây tính theo hạn p0 + B_r, mà khách bảo đảm có B_r = 0, nên dễ "trễ hạn". Trên thước đo trễ so với giờ hứa đầu, Shadow tốt hơn hẳn.
- **Cần viết:** Shadow có ít khách trễ quá 60 s so với giờ hứa đầu hơn nhiều: khoảng −59 khách/ngày khi chỉ chèn, −55 khi gán lại, cả 60/60 ngày.
- **Số và nguồn:** TABLES dòng 39 và 80.

### 10. Đoạn giới hạn về cách chọn cấu hình (chủ chốt ngày 26/10: viết gọn, không dán nhãn từng phép so)
- **Ở đâu:** `08c-via-elmi.tex:185-190`.
- **Đang viết:** "… Vì vậy các so sánh với hàng rào nới ở mục này được ghi là thăm dò."
- **Vì sao:** câu này khiến người đọc nghĩ các so sánh với hàng rào chặt chắc chắn hơn. Không cần dán nhãn "thăm dò" cho từng câu; chỉ cần **một đoạn giới hạn** nói thật cách đã làm.
- **Cần viết** (một đoạn ngắn):
  - cấu hình (mật độ, số xe, trần, α, chế độ gán lại, chỉ số đảo hứa) được chọn qua thử nghiệm sơ bộ;
  - kế hoạch phân tích được tự khóa bằng mã băm trước khi chạy trên 60 ngày mới, nhưng không có dấu thời gian bên ngoài;
  - vì vậy các kết quả là kết quả thực nghiệm trên bộ mô phỏng, chưa phải phép kiểm định được đăng ký độc lập.
- **Không dùng** các chữ "xác nhận" hay "chứng minh" cho các phép so.
- **Số và nguồn:** KT:389, :721 (W20); ADR-082.

### 14. Dung sai hàng rào: báo cả hai mẫu
- **Ở đâu:** `08c-via-elmi.tex:140-142`.
- **Đang viết:** "Trong một phép thăm dò trên 10 ngày, cho hàng rào chặt một dung sai 1 giây đã xóa một phần ba (chỉ chèn) tới một nửa (gán lại) khoảng cách này, 5 giây xóa một nửa tới hai phần ba …"
- **Cần viết:**
  - giữ số 10 ngày của máy nhà (1 s xóa 33%/54%, 5 s xóa 54%/66%);
  - thêm số 30 ngày **[CT]** (1 s xóa 41%/65%, 5 s xóa 58%/82%);
  - với chỉ chèn, theo tiêu chí đặt trước, kết luận là "chưa kết luận".
- **Số và nguồn:** `kits/p5-via-adapter-2026-10-25/N1-RESULT.md`; wf17 :201-211.

### 6. Nguyên nhân SHF phục vụ ít
- **Ở đâu:** `08c-via-elmi.tex:139-140`.
- **Đang viết:** "khi tắc đường đẩy $e$ của một khách có ngân sách 0 vượt $p_0$, xe của khách đó bị giữ nguyên tuyến …"
- **Vì sao:** không chỉ tắc đường. Việc làm tròn vị trí xe cũng đẩy e vượt p0 một chút; khi giao thông phẳng, dung sai 1 s xóa hết khoảng cách.
- **Cần viết:** e vượt p0 do làm tròn vị trí xe *hoặc* do tắc đường; khi giao thông phẳng, dung sai 1 s xóa hết khoảng cách; khi có tắc đường thì còn lại một phần.
- **Số và nguồn:** KT:87-90.

### 23. Trình bày đối thủ theo 3 bước: Elmi chặt → thêm miễn tắc đường → luật C (chủ chốt ngày 26/10)
- **Ở đâu:** mục Đối chứng `08c-via-elmi.tex:35-42`, và cách sắp đoạn kết quả `08c:86-99`, `:137-146`.
- **Đang viết:** hai hàng rào được giới thiệu cùng lúc ("Khóa luận xét hai cách mở rộng: hàng rào chặt (EO35) … và hàng rào nới (EOHZ35) … Hàng rào nới là đối chứng chính …").
- **Vì sao:** trình bày theo bậc thang giúp người đọc thấy vì sao phải nới hàng rào. Nó cũng cho thấy phép so chính tách đúng **một** khác biệt: cả hàng rào nới và luật C đều không đổ lỗi tắc đường cho điều phối (hàng rào nới qua max(·, e); luật C qua c = p − e). Khác nhau chỉ còn ở chỗ **hạn chót theo mức** so với **sổ cộng dồn hai chiều, không hoàn lại**.
- **Cần viết, theo 3 bước:**
  1. **Elmi như trong bài:** B_r = round(α·độ dư), α = 35%, giờ trả p ≤ p0 + B_r. Bài giả định thời gian đi cố định. Khi có tắc đường, cách hiểu thẳng (do khóa luận hình thức hóa) là **hàng rào chặt**: tắc đường đẩy e vượt hạn thì xe bị giữ nguyên tuyến, không nhận thêm khách.
     - Số: EO35 phục vụ 723,2 / 730,6 (chỉ chèn / gán lại).
     - Với Shadow, ~68% khách có B_r = 0, nên xe bị giữ rất nhiều lần. SHF phục vụ ít hơn hẳn: 685,5 / 694,4, thấp hơn SHHZ khoảng 38 khách/ngày ở cả hai chế độ.
     - Một phần là do làm tròn vị trí xe: dung sai 1 s xóa được 33–54% khoảng cách (điểm 14).
  2. **Thêm cơ chế miễn tắc đường (sàn của Hörl & Zwick 2022):** p ≤ max(p0 + B_r, e). Giờ trả chỉ được muộn hơn hạn đúng bằng phần do tắc đường đẩy; điều phối vẫn không được dời thêm. Xe không bao giờ bị giữ chết.
     - Số: EOHZ35 phục vụ 725,2 / 732,1 (hơn EO35 1,5–2,0 khách/ngày); SHHZ hơn SHF khoảng 38 khách/ngày.
     - Vi phạm hạn chót gần như không đổi (1,13 / 1,01% so với 1,12 / 1,03%).
     - **[CT]** Lời hứa của các hãng thật cũng miễn phần tắc đường (khung giờ trả của MOIA, "no increase" của UberX Share; wf17 B2). Vì vậy hàng rào nới vừa công bằng vừa gần thực tế, nên được dùng làm **đối thủ chính**.
  3. **Luật C so với hàng rào nới:** cùng ngân sách, cùng miễn tắc đường, chỉ khác cách tính.
     - Kết quả như điểm 1–3: chỉ chèn thì ngang; gán lại thì −42% đảo hứa, −2,8 khách, và nhiều khách vượt hạn chót của Elmi hơn.
     - Hàng rào chặt vẫn báo cạnh, như một hàng tham khảo trong các bảng.
- **Số và nguồn:** bảng `08c:73-80` (TABLES); KT:87-90, :303-306; `N1-RESULT.md`; báo cáo wf17 §3.2.

### 24. Cách viết phần dự báo trong "hệ của mình" (thêm mới sau N7)
- **Ở đâu:** phần mô tả hệ và phần kết quả hệ đầy đủ (nếu đưa vào); phần kết luận.
- **Vì sao:** N7 đã so dự báo với một đệm cùng độ dài đi qua điều phối. Kết quả cho phép viết một câu có số, nhưng phải đúng mức.
- **Cần viết:**
  - dự báo giảm trễ buổi sáng hơn đệm cùng độ dài khoảng 14–18 trên 1000 khách (gán lại) và khoảng 13 (chỉ chèn);
  - phần lớn lợi (khoảng 3/4 cả ngày, 85% buổi sáng) là do hứa dài hơn;
  - buổi chiều không hơn đệm, và trễ hơn lời hứa không dự báo;
  - ngoài trễ, dự báo còn làm ít đổi giờ hứa, ít đảo hứa ở buổi chiều và ít vi phạm hạn chót;
  - giới hạn: chưa thử hồ sơ cố định theo giờ; thế giới mô phỏng và dự báo cùng nguồn dữ liệu, nên đây là cận trên.
- **Ghi công đúng:** mức giảm trễ của hệ là của phần lời hứa (độ dài và dự báo); mức giảm đảo hứa là của luật C (khi gán lại) và một phần của dự báo.
- **Số và nguồn:** **[CT]** `RideBound-Ket-Qua-Moi-26-10.md` §1b và §4; `wf21/n7/REPORT-VI.md` (có mục "Sửa sau kiểm tra").

### 21. Vì sao 512 yêu cầu và 62 xe (thêm mới, dễ bị hỏi)
- **Ở đâu:** đoạn giới hạn `08c-via-elmi.tex:185-190`, hoặc ngay sau đoạn mô tả thiết lập Via.
- **Đang viết:** "Thiết lập Via (mật độ, số xe, thời gian đi tối đa, khung giờ) được chọn qua thăm dò … Mọi hiệu đo trong một cấu hình (62 xe, 512 yêu cầu mỗi hai giờ …)".
- **Vì sao:** hội đồng hay hỏi "sao lại là 512 và 62". Đã có câu trả lời có số, và đã chạy thêm các cỡ đội theo mốc của nhóm Alonso-Mora.
- **Cần viết:**
  - 512 là mẫu khoảng 11% số chuyến Via thật của mỗi khung 2 giờ;
  - 62 xe là đội nhỏ nhất để nhánh không giới hạn phục vụ khoảng 79% khách, theo quy tắc đặt trước;
  - tải mỗi xe khoảng 8,3 yêu cầu/2 giờ, gần nghiên cứu cùng chủ đề của Fielbaum & Alonso-Mora 2020 (9,8) và nhẹ hơn Alonso-Mora 2017 ở cùng giờ;
  - **[CT]** chạy lại ở 7 cỡ đội suy từ hai bài này (20–81 xe): mọi nhãn kết luận giữ nguyên. Mức giảm đảo hứa dưới G2 tăng theo số xe (27% ở 20 xe, 41% ở 62 xe, 46% ở 81 xe), và vi phạm hạn chót Elmi cũng tăng theo;
  - không nói đây là bản thu nhỏ của Alonso-Mora 2017, vì mật độ thấp hơn khoảng 70 lần.
- **Số và nguồn:** `RideBound-Ket-Qua-Moi-26-10.md` §2; `wf20/am2017/results/am_tables.txt`; `wf6/via-many-days/T.txt:146-149`.

---

## C. Nên sửa nhỏ / tùy chọn

### 15. Ghi rõ "gộp"
- **Ở đâu:** `08c-via-elmi.tex:97-99`.
- **Vấn đề:** câu văn dùng tỷ lệ gộp trên 60 ngày (1,78/1,75 so với 1,11/1,02), còn bảng 1 dùng trung bình theo ngày (1,79/1,77…).
- **Sửa:** thêm chữ "gộp trên 60 ngày" vào câu văn.

### 16. Câu "luật C ≈ hàng rào nới khi giao thông phẳng"
- **Vấn đề:** khóa luận hiện **chưa có** câu này.
- **Nếu thêm:** viết là *gần* đúng: Manhattan 10/11 ô (1.033/1.036 khách); Via phẳng, chỉ chèn, 16/20 ô **[CT]**; với gán lại 0/20.
- **Nguồn:** `kits/identity-2026-10-25/IDENTITY-RESULT.md:19`.

### 17. Tài liệu tham khảo
- Thêm bằng sáng chế Uber US10928210B2 (KT:639).
- Ở bảng 2 có thể thêm: khách bảo đảm **chỉ bị dời muộn** là 1,47% (SHF) và 1,71% (SHHZ) khi gán lại (KT:165).

### 18. Dự báo ở Via
- Nếu nhắc tới dự báo trong mục so với Elmi **[CT]**, ghi là kết quả bộ mô phỏng trên dữ liệu Via:
  - dự báo vòng kín làm hại khung 17 giờ (khoảng +2,7 điểm % so với nhánh đệm cùng độ dài);
  - lời hứa không dự báo, nếu hiệu chỉnh theo giờ từ các ngày trước, hòa với dự báo thô.
- **Không** gắn các số này vào các đoạn dự báo Manhattan (`08-chuong4.tex:176-178, 449`; `09-chuong5.tex:57-62`); đó là thí nghiệm khác.

---

### 22. Hàng "hệ đầy đủ" Shadow + luật C + dự báo (chỉ nếu thầy muốn)
- Đã chạy ngày 26/10 **[CT]**. Kết quả: không tổ hợp nào hơn trên mọi chỉ số.
- Nếu đưa vào thì đặt ở phần thảo luận, ghi "thăm dò, bộ mô phỏng". Số ở `RideBound-Ket-Qua-Moi-26-10.md` §1.

## D. Cần bạn quyết

### 19. Khai báo dùng AI
- **Đang viết:** `08c:187` "các giả thuyết được đặt sau thử nghiệm sơ bộ, không do người viết khóa luận đặt từ đầu". Câu này sẽ khiến người đọc hỏi "vậy ai đặt?".
- Comment LaTeX ở `08c:192` có nhắc tới AI, và đọc được trên GitHub công khai.
- KT khuyên khai báo theo quy định của khoa. Bạn chốt với thầy, rồi sửa nhất quán cả khóa luận.

---

**Đã đúng, không cần sửa:**
- số ở hai bảng Via và các phép so ở 08c khớp nguồn;
- đã tách "theo định nghĩa" khỏi "đo được";
- không gọi các đợt chạy cũ là xác nhận;
- SHRC hơn SHF được viết là do cách hình thức hóa, không phải ưu thế;
- 4/5 lỗi sự thật kiểm toán liệt kê đã sửa (lỗi còn lại là điểm 7);
- mọi số Via được ghi là số của bộ mô phỏng.
