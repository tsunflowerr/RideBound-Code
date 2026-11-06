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

### Q6 (2026-11-06)

**Thông tin truy cập máy thuê đang nằm trong repo công khai.**

`TIEN-DO-2026-11-03.md` (mục 04/11 22:35 và 05/11 00:10) ghi địa chỉ, cổng và lệnh đăng nhập root của máy thuê.

**Cần.**
- Máy đó đã Destroy chưa? Nếu chưa, nên hủy máy hoặc đổi khóa.
- Có che các dòng này không? Lịch sử git vẫn giữ chúng. Việc này để chủ đề tài quyết.

**Vì sao.** Repo công khai.
