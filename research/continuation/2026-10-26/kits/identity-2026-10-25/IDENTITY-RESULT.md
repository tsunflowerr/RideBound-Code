# Kiểm đồng nhất rẻ trên bộ mô phỏng (25/10/2026)

**Nguồn yêu cầu:** wf10 part2:188,326; kiểm toán §6-N5 (KT:612,673).

**Cách làm** (`emulate_identity.js`):
- Bộ mô phỏng wf15 (bản sao ở nhà, engine sha `908a2961…`), trên 11 ô Manhattan đã bắc cầu. Ở các ô này, bộ mô phỏng khớp
  RideBound thật từng khách.
- So từng khách trên các trường t0, p0, A, served, Σ\|c\|, số lần c ≠ 0, Σc, max\|c\|, shared.
- "Giao thông phẳng" nghĩa là giữ hệ số của bin đầu cho mọi bin.

**Kết quả** (`identity-results-2026-10-25.tsv`):

| Phép thử | Thế giới | Cặp | Ô giống hệt | Khách giống |
|---|---|---|---|---|
| (a) chỉ chèn | giao thông của ô | RC-120 = NC-120 = OC-120 | 11/11 | 977/977 |
| (a) chỉ chèn | giao thông của ô | RC-300 = NC-300 = OC-300 | 11/11 | 999/999 |
| (b) chỉ chèn | phẳng | RC-120 = DLHZ-120 | 11/11 | 1.006/1.006 |
| (b) chỉ chèn | phẳng | RC-300 = DLHZ-300 | **9/11** | 980/1.034 |
| (b) chỉ chèn | phẳng | RCE35 = EOHZ35 (thế giới E0) | **10/11** | 1.033/1.036 |
| đối chứng | giao thông của ô | RC-120 so với DLHZ-120 | 6/11 | 814/993 |
| đối chứng | giao thông của ô | RCE35 so với EOHZ35 | 4/11 | 760/1.023 |

**Kết luận:**
- (a) **Đúng tuyệt đối** trên các ô này: khi chỉ chèn, cách xử lý c âm (hoàn hay không hoàn) không đổi quyết định nào.
  Ở B = 120 có 10 khách có c âm, nhưng không quyết định nào khác.
- (b) **Gần đúng, không tuyệt đối:**
  - khi giao thông phẳng, luật C và hàng rào nới trùng nhau ở hầu hết các ô;
  - nhưng có 2/11 ô lệch ở B = 300 và 1/11 ô lệch ở α = 35% (3 khách).
  - Mệnh đề "luật C quy về Elmi nới trong thế giới Elmi" chỉ được viết kèm phạm vi đã kiểm này. Việc lần vết các ô lệch tới
    quyết định đầu tiên khác nhau thuộc §6-N5 và chưa làm.
- Đối chứng cho thấy khi có giao thông, hai luật khác nhau rõ: 4–6/11 ô giống. Vậy phép (b) có sức phân biệt.
