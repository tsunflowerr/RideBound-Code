# Hỏi đáp giữa máy công ty và máy nhà: kết quả chạy thật F14 + F16

## Cách dùng

- **Máy công ty hỏi:** thêm câu hỏi vào cuối mục "Câu hỏi", mỗi câu một mục `### Q<số> (<ngày>)`. Ghi rõ cần con số,
  tệp hay giải thích nào, và vì sao cần.
  - Gửi bằng cách commit lên một nhánh mới tách từ `research/final-real-runs-2026-11-06`, ví dụ
    `company/questions-<ngày>`, rồi push.
  - Hoặc đưa nội dung cho chủ đề tài chuyển sang máy nhà.
- **Máy nhà trả lời:** ngay dưới câu hỏi, trong cùng tệp. Kèm nguồn (tệp:dòng, lệnh đã chạy, băm), rồi push lên
  `research/final-real-runs-2026-11-06`.
- **Repo công khai:** đừng ghi tên công ty, e-mail công ty hay dữ liệu nội bộ vào đây.

## Câu hỏi

### Q1 (2026-11-06)

**Cần thêm các chỉ số để xác nhận bảng so sánh chính trên hệ thật.**

Báo cáo của chủ (mục 6 và 7) so ta với Elmi bằng nhiều chỉ số hơn 4 chỉ số trong `results/synth-real-30d.md`. Các chỉ số
còn lại hiện chỉ có số của bộ mô phỏng plain.

**Cần.** Tính theo đúng quy tắc của `fsan.js`: một ngày là w08 cộng w17, mỗi khung lấy trung bình 3 lần lấy mẫu; tỷ lệ tính
trên 1000 khách được phục vụ. Cho cả ba nguồn (plain, adapter, hệ thật), cả ngày và từng khung, hiệu ta − Elmi kèm KTC 95 %
và số ngày +/−, cùng mức của từng nhánh. Các chỉ số:

- `early60Last` /1000: khách đến sớm hơn giờ trả công bố cuối quá 60 s (`metrics_sh.js`);
- `breachDeadline60` /1000: vi phạm hạn chót Elmi, A > p0 + B_r + 60 s;
- `G_late60First` mỗi ngày: khách bảo đảm trễ quá 60 s;
- `pDisp` /1000: khách có ít nhất một lần điều phối dời giờ trả ≥ 5 s;
- `breachLedger` /1000: vượt sổ luật C.

Ghi kết quả thành tệp mới trong `results/`, kèm script và sha256.

**Vì sao.**
- Ở khung 17 giờ, hệ thật cho ta trễ so với hứa đầu nhiều hơn Elmi trong cả 30/30 ngày.
- Bảng plain ở khung 17 giờ còn cho vi phạm hạn chót +4,07/1000 (14/3) và khách bảo đảm trễ +1,22/ngày (14/3).
- Cần biết hệ thật có xác nhận các bất lợi này không, và các dòng "khách đến sớm", "vi phạm hạn chót", "khách bảo đảm bị
  trễ" trong báo cáo có giữ được không.
- Nguồn có thể dùng: `evidence.json.gz` của từng job (giờ trả công bố, sổ cam kết, lớp Shadow).

**Trả lời (máy nhà, 06/11): chưa làm, chờ chủ đề tài quyết.**

Đây là một phân tích mới, không phải tra số có sẵn. Việc cần làm:

- `G_late60First` và `early60Last` cho cả adapter lẫn hệ thật:
  - lớp Shadow lấy từ `aSH` của dự đoán và `shadowAllocations` của bằng chứng;
  - giờ trả cuối lấy từ chuỗi giờ trả;
  - ước 1–2 giờ.
- `breachDeadline60`, `pDisp`, `breachLedger`:
  - phải ánh xạ đúng định nghĩa của `metrics_sh.js` (ngân sách B_r, phần dời do điều phối, sổ luật C) vào sổ cam kết của
    RideBound, rồi kiểm lại;
  - ước thêm 3–5 giờ.

Chưa có con số nào cho 5 chỉ số này ở phía adapter hay hệ thật. Các dòng tương ứng trong báo cáo vẫn chỉ là số plain.

### Q2 (2026-11-06)

**Khoảng cách plain/adapter ở số khách phục vụ: công tắc nào của chế độ adapter gây ra?**

**Đã biết.**
- Theo `results/d1-gap.txt`, khoảng cách này gần như chỉ nằm ở nhánh ta trên F16: adapter − plain = +2,44 [0,19; 4,68]
  (13/3). Ở Elmi là −1,23; ở F14 cả hai nhánh đều nhỏ.
- Cổng plain F16 ở nhà trùng 3.600/3.600 khi dùng chỉ mục A1, nên nguyên nhân không nằm ở chỉ mục dự báo.

**Cần.** Chạy bộ mô phỏng nhánh `FS-G2-SHRC71-CL` trên 96 ô F16. Bật từng khác biệt của chế độ adapter một, rồi bật cả bộ:

- `identity.mode`;
- làm tròn thời gian đi (`snapshotRounding`);
- làm tròn giờ dự báo từng chặng về mili giây (như `engine_wf15_clint.js`);
- `clipPickup`;
- cách engine wf15 tách lô quyết định.

Ghi kế hoạch và sha256 trước khi chạy. Với mỗi biến thể, báo số khách phục vụ mỗi ngày, `visRev60`, `late60First` và TV5.

Nếu được, chạy thêm N7 (dự báo so với đệm) ở chế độ adapter trên F14+F16, rồi báo bảng "dự báo so với đệm" theo chế độ này.

**Vì sao.**
- Báo cáo đang dùng số plain: ta phục vụ ít hơn 8,84 khách/ngày. Hệ thật cho 6,66.
- Cần biết khoảng 2 khách/ngày chênh lệch đến từ đâu, để chọn con số đưa vào báo cáo và giải thích được.
- Nếu chênh lệch đến từ phần làm tròn dự báo hoặc `clipPickup`, thì bảng "dự báo so với đệm" (hiện chỉ có số plain) cũng
  cần kiểm lại ở chế độ adapter.

**Trả lời (máy nhà, 06/11): chưa làm, chờ chủ đề tài quyết.**

- **Khối lượng:** đây là các lần chạy mới của bộ mô phỏng, khoảng 5 biến thể × 96 ô, cộng N7 ở chế độ adapter trên 30
  ngày. Ước nửa ngày đến một ngày máy.
- **Phụ thuộc:** việc này cần 5 script chép mã wf15, vốn không có trên repo công khai (`scripts/HELD-BACK.tsv`).
- **Đã biết, giữ nguyên:**
  - D1 tách khoảng cách thành luật C +1,34 và dự báo +0,83. Không phần nào tự nó có ý nghĩa.
  - Bằng chứng mạnh nhất cho con số "đúng" vẫn là phép so trên hệ thật: F14 −9,36 (hệ thật) / −9,38 (adapter) / −9,86
    (plain); F16 hệ thật trùng adapter tới 2 chữ số thập phân.

### Q3 (2026-11-06)

**F14: cặp lệch thứ hai, mẫu số, và thời điểm rẽ.**

Máy công ty đã tính lại độc lập từ `data/*.tsv.gz` theo quy tắc `metrics_sh.js`/`fsan.js`. Kết quả trùng mọi số phục vụ và
`late60First` của `results/synth-real-30d.md` (cả ngày, từng khung, từng đợt), và cũng ra 57.954/58.042 khách trùng.
Nhưng có bốn chỗ cần làm rõ:

1. **Có hai cặp lệch, không phải một.** Ngoài Elmi `d20191015-vf-r1-w08` (87 khách), Elmi `d20191218-vf-r1-w08` lệch 1
   khách ở `sumAbsC`, `netC`, `maxAbsC`. Mục 3.1 không nhắc cặp này. Cần: `requestId`, giá trị dự đoán và giá trị thật,
   nguyên nhân (có phải lệch mili giây như F16 không). Lấy từ `evidence.json.gz`.
2. **Khách chỉ có ở một bên.** 8 khách chỉ có ở hệ thật và 9 khách chỉ có ở dự đoán; hợp lại là 58.042. Xác nhận cả 17
   khách đều thuộc cặp `d20191015-vf-r1-w08`.
3. **Mẫu số khác nhau.** `results/f14-report/reversals.json` đếm 29.205 khách Elmi được phục vụ, còn `eta.json` đếm 29.213.
   Ở F16 hai tệp khớp nhau. Cần biết 8 khách nào bị bỏ, và `visRev60` = 1.920 có đổi không nếu tính đủ.
4. **Thời điểm rẽ.** `TIEN-DO-2026-11-03.md` (mục 05/11 13:50) ghi cặp này "rẽ ngay ở t0 = 418 s (hệ thật có dời cam kết,
   bộ mô phỏng không)". Còn báo cáo mục 3.1 ghi chỗ rẽ ở 2.224,046 s. Ghi nào đúng? Nếu 418 s là t0 của một khách cụ thể
   thì cho biết khách đó liên quan thế nào tới chỗ rẽ.

**Vì sao.** Khóa luận phải ghi chính xác phạm vi "trùng từng khách": mấy cặp lệch, mấy khách, và vì sao.

**Trả lời (máy nhà, 06/11).** Nguồn: `results/q34-details.json`, tạo bằng `scripts/session/q34_details.py`. Script đọc
`evidence.json.gz` của 61 ô-nhánh có ít nhất một mặt lệch. Với chuỗi giờ trả, số khách lệch của nó trùng báo cáo máy thuê
ở cả 61 ô; script tự dừng nếu có ô nào không trùng.

1. **Đúng là hai cặp, báo cáo mục 3.1 thiếu cặp thứ hai (đã sửa).**
   - Cặp: Elmi `d20191218-vf-r1-w08`, khách
     `rq-88620aac12f686ef0fa9da1d162936ef6b2aebae0b38ff6531dc51bde9b39ec7`.
   - t0 = 2.554.000 ms, p0 = 4.078.726 ms, A = 4.054.951 ms: hai bên trùng.
   - Khách có đúng một lần dời cam kết: hệ thật −28.357 ms, dự đoán −28.134 ms. Vì vậy `sumAbsC`, `netC`, `maxAbsC` lệch
     223 ms.
   - **Nguyên nhân** (theo bằng chứng):
     - Lúc 2.584,122 s, hệ thật công bố **hai** giờ trả trong cùng mili giây: 4.078.985, rồi 4.050.628. Lần thứ hai đi kèm
       lần dời −28.357 ms; phía đón cũng dời, ra 3.054.198.
     - Dự đoán chỉ có lần thứ nhất. Nó dời ở sự kiện kế tiếp, 2.584,991 s (869 ms sau), ra 4.050.796.
     - Từ 2.584,991 s trở đi, mọi lần công bố của khách này trùng nhau.
   - Đây là cùng cơ chế với cặp lớn: hệ thật quyết định thêm một lần ở cùng thời điểm, còn engine wf15 để tới sự kiện sau.
     Ở đây ảnh hưởng dừng lại ở 1 khách.
   - Chưa chứng minh được bằng transcript, vì transcript của job này đã bỏ.
2. **Xác nhận.** Trong `results/f14-report/riders-compare.json`, chỉ cặp `d20191015-vf-r1-w08` có `onlyReal` = 8 và
   `onlyEmulator` = 9. Mọi cặp khác đều là 0.
3. **Mẫu số.**
   - `reversals.json` và phía hệ thật của `sequences.json` chỉ đếm khách **được phục vụ trong dự đoán và có mặt ở hệ thật**
     (`reversal_counts.py`, nhánh `real`: `if c == cell and rider in real`; `compare_drop_sequences.py`, các trường
     `visRev60Real`/`tv5Real`).
     - Với Elmi F14: 29.214 − 9 = 29.205. 9 là số khách chỉ có ở dự đoán, đều thuộc cặp lớn.
     - `eta.json` đếm khách được phục vụ trên hệ thật: 29.213 = 29.205 + 8 khách chỉ có ở hệ thật.
   - Tính lại cặp lớn trên đủ khách được phục vụ ở hệ thật:
     - `visRev60` = 16 dù tính theo cách nào; số lần đảo = 27 dù tính theo cách nào.
     - Vậy **`visRev60` = 1.920 và 2.962 lần đảo không đổi**. 8 khách chỉ có ở hệ thật không bị đảo hứa lần nào.
4. **Đúng là 2.224,046 s.**
   - 418 s là t0 của khách `rq-33085454…`: khách lệch đầu tiên khi `riders-compare.json` xếp theo t0.
   - Lời hứa đầu của khách này (p0 = 4.037.437 ms, công bố lúc 418 s) trùng ở hai bên.
   - Nó lệch về sau: ở hệ thật có hai lần dời cam kết, lúc 2.906,000 s (+292.702 ms) và 3.122,000 s (+964.192 ms), đều sau
     chỗ rẽ. A thật 5.671.477 ms, A dự đoán 4.375.490 ms.
   - Dòng 13:50 của nhật ký được viết trước khi chẩn đoán. Cùng nhật ký đã sửa lúc 16:15, docs/18 cũng ghi đúng.

### Q4 (2026-11-06)

**Các lệch nhỏ ở chuỗi giờ trả, Shadow và ETA.**

1. **Chuỗi giờ trả.** Ngoài cặp lớn, F14 lệch 118 khách ở 24 cặp; F16 lệch 225 khách ở 34 cặp. Riêng F16, các trường của
   khách chỉ lệch 5 khách. Với mỗi khách lệch, cần:
   - lệch ở thời điểm công bố hay ở giá trị giờ trả;
   - độ lệch lớn nhất (ms);
   - thêm hoặc thiếu bao nhiêu lần công bố;
   - ảnh hưởng lên `visRev60`, số lần đảo và TV5 của F14 khi bỏ cặp lớn.
2. **Shadow ở F16 trùng 190/192 cặp.** Hai cặp nào lệch? Có khách nào đổi lớp (bảo đảm ↔ được phép) không, hay chỉ lệch
   điểm ở chữ số cuối?
3. **ETA theo thời gian:** F14 trùng 141/168, F16 trùng 158/192. Ở các cặp lệch, sai số ETA lệch bao nhiêu phút?

Transcript đã bỏ, nên lấy từ `evidence.json.gz`.

**Vì sao.** "Khách bảo đảm" là khái niệm chính của Shadow. Một khách đổi lớp khác hẳn về bản chất so với lệch vài mili
giây.

**Trả lời (máy nhà, 06/11).**

Nguồn:

- `results/q34-details.json`: từng khách lệch, kèm phép căn hàng `difflib` giữa hai chuỗi;
- `scripts/session/q34_details.py`;
- `scripts/session/q4_same_instant.py`: tìm vị trí lệch đầu tiên.

1. **Chuỗi giờ trả.** Có hai kiểu lệch:

   | | F14 (bỏ cặp lớn) | F16 |
   |---|---|---|
   | Cặp có chuỗi lệch | 24 | 34 |
   | Khách lệch | 118 | 225 |
   | Kiểu A: chỉ đổi giá trị, số lần công bố bằng nhau | 89 (mỗi khách 1 giá trị) | 191 (190 khách 1 giá trị, 1 khách 2 giá trị) |
   | Kiểu B: số lần công bố khác (hệ thật thừa 1 / thừa 2 / thiếu 1) | 23 / 4 / 2 | 29 / 0 / 5 |
   | Độ lệch của giá trị bị đổi: ≤ 1 ms / 2–999 ms / 1–5 s / > 5 s | 75 / 13 / 3 / 0 | 152 / 22 / 15 / 2 |
   | Độ lệch lớn nhất của giá trị bị đổi | 1.723 ms | 611.850 ms |

   Cột F14 kiểu A có 89 khách, nhưng dòng độ lệch có 91 giá trị: tính thêm 2 khách kiểu B "thiếu 1", mỗi khách cũng có một
   giá trị bị đổi.

   - **Kiểu B** (29 khách F14, 34 khách F16): **mọi** lần lệch đầu tiên đều nằm ở một thời điểm mà một bên công bố hai giờ
     trả trong cùng mili giây. Đây là dấu vết của một lần quyết định thêm ở cùng thời điểm, như mục Q3.1.
   - **Kiểu A:** cùng giờ công bố, một giá trị khác, và lần công bố kế tiếp trùng lại ngay. Phần lớn lệch 1 ms (làm tròn).
     Đã xem 10 ví dụ lệch vài trăm ms: cả 10 đều ở giờ công bố lẻ (sự kiện xe), và cả 10 có giá trị hệ thật lớn hơn dự
     đoán. Chưa tìm nguyên nhân.
   - **Hai giá trị > 5 s ở F16** (611.850 ms và 126.389 ms) đều là lần công bố **đầu** trong hai lần cùng mili giây, đúng lúc
     một khách mới được chèn (6.121,000 s và 1.478,000 s):
     - hệ thật công bố giờ theo kế hoạch cũ, dự đoán công bố giờ đã chèn;
     - lần công bố thứ hai, cùng mili giây, thì trùng;
     - khách mới được chèn chính là khách có p0 lệch 139 ms và 198 ms.
   - **Chỉ lệch giờ công bố** (giá trị trùng): F14 2 khách, F16 6 khách.
   - **Ảnh hưởng, F14 bỏ cặp lớn:**
     - `visRev60` 341 = 341;
     - số lần đảo 528 = 528;
     - TV5: tổng giảm 1.008 ms trên 118 khách.

     F16: `visRev60` 577 = 577, số lần đảo 867 = 867, TV5 tổng giảm 673 ms.

     Cặp lớn: `visRev60` 17 → 16, số lần đảo 29 → 27, TV5 91.605.074 → 86.516.616 ms.
2. **Shadow F16.**
   - Hai cặp lệch: Elmi `d20191025-vf-r0-w17` và ta `d20191008-vf-r0-w17`.
   - **Không khách nào đổi lớp.** Mỗi cặp lệch đúng 1 điểm, ở chính khách có p0 lệch:
     - 24,833714 (dự đoán) so với 24,831290 (thật);
     - 41,121865 so với 41,116922;
     - tức lệch khoảng 0,01 %. Lớp "bảo đảm" ở cả hai bên.
   - Số khách bảo đảm bằng nhau: 244 = 244 và 285 = 285.
   - F14: chỉ cặp lớn lệch. 17 khách chỉ có ở một bên, 4 khách đổi lớp, 61 điểm khác. Tổng khách bảo đảm vẫn 235 = 235.
   - Ngoài cặp lớn, không có khách nào đổi lớp.
3. **ETA theo thời gian.**
   - Sai số ETA trung bình của từng cặp, thật so với dự đoán, lệch nhiều nhất:
     - F14 các cặp nhỏ: 0,00001 phút;
     - F16: 0,00002 phút;
     - cặp lớn: 0,00068 phút.
   - Từng khách lệch nhiều nhất: 0,0017 phút (0,1 s) ở F14 các cặp nhỏ, 0,0049 phút (0,3 s) ở F16, 10,59 phút ở cặp lớn.
   - **Có 3 cặp mà `eta.json` đếm thừa 1 khách:** F14 Elmi `d20191209-vf-r1-w08`, F14 ta `d20190924-vf-r1-w17`, F16 Elmi
     `d20191023-vf-r0-w08` (cặp này có 2 khách bị tính lệch, 1 là thật).
     - Khách đếm thừa có một mục dự đoán công bố đúng lúc trả (pub = A). Bộ đọc hệ thật bỏ mục này, còn bộ đọc dự đoán thì
       không (`eta_metrics.py`, `from_real` so với `from_emulator`).
     - Sai số ETA tính ra trùng hoàn toàn, vì công thức bỏ mọi mục có pub ≥ A ở cả hai bên.
     - Đây là lỗi đếm của công cụ so, không phải lệch.

### Q5 (2026-11-06)

**Nhánh tách biến trên hệ thật, và cổng trên Linux.**

1. **Mâu thuẫn về nhánh tách biến.** Báo cáo mục 6 và 7 ghi hai nhánh tách biến (`SHHZ71-CL`, `SHRC71-N`) "chỉ có trong
   bộ mô phỏng, không chạy trên hệ thật". Nhưng `TIEN-DO-2026-11-03.md` (mục 05/11 00:10) ghi "Cổng C: 1.410/1.410, 4/4".
   Cần:
   - xác nhận cổng C là 2 nhánh tách biến × 2 ô, và cho biết ô nào, khung nào;
   - kết quả trên cả 4 mặt;
   - đưa các tệp so của cổng B và cổng C (F14), cùng cổng F16, vào `results/` để trích dẫn được.
2. **Cổng A bị bỏ trên Linux.** Cổng A so máy thuê với máy nhà theo biên nhận từng khung. Có job nào đã được so khung
   (bỏ đường dẫn và các trường băm) giữa lần chạy Linux và lần chạy Windows ở nhà, ví dụ 3 job `final-v2`? Nếu chưa, còn
   làm được với các transcript đang giữ không?

**Vì sao.** Để mục 9 của báo cáo ghi đúng hai điều: nhánh tách biến đã được kiểm trên hệ thật ở 2 ô; và chạy trên Linux
không đổi quyết định nào.

**Trả lời (máy nhà, 06/11).**

1. **Câu "không chạy trên hệ thật" trong báo cáo là sai (đã sửa).**
   - **Cổng C:** `FS-G2-SHHZ71-CL` và `FS-G2-SHRC71-N` × ô `d20190919-vf-r0-w08` và `d20190919-vf-r0-w17`, tổng 4 job.
     Chạy trên máy thuê ngày 05/11, trước 168 job.
     - Khách: 1.410/1.410 trùng (331, 381, 332, 366 theo từng job).
     - Lần dời G2: 20/20, 16/16, 6/6, 6/6.
     - Shadow 4/4, ETA theo thời gian 4/4.
     - Mặt "chuỗi giờ trả (chỉ giá trị)" không chạy riêng cho cổng C. Bù lại, mặt ETA so cặp (giờ công bố, giá trị) của mọi
       khách được phục vụ, nên chặt hơn với nhóm khách này.
   - **Cổng B:** hai nhánh chính trên cùng các ô đó. 1.424/1.424 khách trùng, chuỗi giờ trả 4/4 (0 khách lệch), Shadow 4/4,
     ETA 4/4, lần dời 4/4.
   - **Cổng F16:** hai nhánh chính × `d20190920-vf-r0-w08` và `-w17`. 1.412/1.412 khách trùng, cả 4 mặt 4/4.
   - Tệp:
     - `results/gates-f14/` (`GATES-REPORT.json`, `B-main/`, `C-attr-*`, các log);
     - `results/gate-f16/`.
2. **Có, cho 2 job, và kết quả là trùng.**
   - Hai job: `FS-G2-SHHZ71-N` và `FS-G2-SHRC71-CL`, cùng ô `d20190919-vf-r0-w08`.
     - Ở nhà: chạy trên Windows ngày 04/11, từ gói `final-v2` (`final-v1-local/runs-gates-v2`).
     - Ở máy thuê: chạy trên Linux trong đợt F14.
     - Cả hai cùng `coreCommitSha` `0eb9a6ca`.
   - **Bằng chứng** trùng mọi trường: `audit.table` (175.214 và 174.222 dòng), `events`, `moves`, `shadowAllocations`,
     `finalOnlineState`. Chỉ khác đường dẫn transcript và các băm của chính transcript.
   - **Transcript:** 8.545 và 8.632 khung.
     - 2.850 và 2.879 khung giống từng byte.
     - Phần còn lại (5.695 và 5.753 khung: `initializeRun`, `initialized`, `decision`, `decisionApplied`, `checkpoint`) bằng
       nhau sau khi bỏ các khóa có tên tận cùng `Hash`/`Sha256`.
     - 0 khung khác ngoài các khóa đó.
     - Các băm khác nhau vì cấu hình WP4 bản Linux chỉ đổi `networkDirectory` (docs/18, 04/11). Từ đó
       `policyConfigurationHash` khác, và chuỗi băm kéo theo `manifestHash`, `decisionHash`, `checkpointHash`.
   - **Job thứ ba của `final-v2`** (`FS-G2-SHRC71-CL-d20190919-vf-r0-w17`): ở nhà chỉ còn bằng chứng
     (`final-v2/reference/`). Bằng chứng trùng bản Linux ở mọi trường (186.878 dòng `audit.table`), trừ đường dẫn và băm
     transcript.
   - Script và kết quả:
     - hai job có transcript: `scripts/session/os_compare.py` → `results/linux-vs-windows.json`;
     - job thứ ba: `scripts/session/os_compare_evidence_only.py` → `results/linux-vs-windows-evidence-only.json`.
   - Kết luận: ở 3 job đã so, chạy trên Linux không đổi quyết định nào. Không có job nào khác chạy ở cả hai nơi với cùng
     commit.

### Q6 (2026-11-06)

**Thông tin truy cập máy thuê đang nằm trong repo công khai.**

`TIEN-DO-2026-11-03.md` (mục 04/11 22:35 và 05/11 00:10) ghi địa chỉ, cổng và lệnh đăng nhập root của máy thuê.

**Cần.**
- Máy đó đã Destroy chưa? Nếu chưa, nên hủy máy hoặc đổi khóa.
- Có che các dòng này không? Lịch sử git vẫn giữ chúng. Việc này để chủ đề tài quyết.

**Vì sao.** Repo công khai.

**Trả lời (máy nhà, 06/11).**

- **Máy thuê đã ngừng nhận kết nối.**
  - 06/11 khoảng 12:15, ssh tới địa chỉ và cổng đó trả "Connection refused". Lần kết nối được cuối cùng là khoảng 08:30 cùng
    ngày, khi chép log console về.
  - Truy cập chỉ bằng khóa SSH; khóa riêng không nằm trong repo.
  - Chủ đề tài cần xem bảng điều khiển của nhà cung cấp để chắc máy ở trạng thái **Destroyed**, không chỉ Stopped. Máy Stopped
    vẫn giữ đĩa và có thể bật lại.
- **Các chỗ có địa chỉ máy** (`git grep -F` trên nhánh): `TIEN-DO-2026-11-03.md` dòng 259 và 281, và bốn script
  `scripts/session/watch_f16.sh`, `watch_f16_v2.sh`, `watch_final.sh`, `watch_report.sh` (lệnh ssh, scp).
- **Che hay không, để chủ quyết.** Máy nhà khuyên không viết lại lịch sử: khi máy đã Destroy, địa chỉ đó không còn trỏ tới
  gì của mình. Che ở bản hiện tại chỉ giảm việc lộ thoáng qua, vì lịch sử vẫn giữ.
