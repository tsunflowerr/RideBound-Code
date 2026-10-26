# Điểm hòa vốn giữa luật C và Elmi: kết quả (26/10/2026, 01:08)

**Nhãn:** thăm dò sau kiểm toán; 10 ngày đã xem (60 ô P5); số của **bộ mô phỏng** wf15 của công ty, không phải của
RideBound. Kế hoạch được ghi và niêm phong trước khi tính (`BREAKEVEN-PLAN.md`, sha `2e82c711…`). Các sửa đổi ghi trong
`BREAKEVEN-LOG.md`.

## 1. Phép tính này trả lời câu gì

So với Elmi, luật C làm **ít khách bị đảo hứa hơn**. Đảo hứa là khi giờ trả báo cho khách bị lệch qua lệch lại, mỗi lần ít
nhất 1 phút, ví dụ báo 8:30, rồi 8:32, rồi lại 8:29. Đổi lại, luật C làm **nhiều khách bị điều phối dời giờ trả muộn hơn
quá 1 phút**. Luật nào tốt hơn còn tùy khách ghét chuyện nào hơn.

Gọi **w** là mức khó chịu của một lần đảo hứa, tính bằng "số khách bị điều phối dời muộn quá 1 phút":
- w = 1: khách ghét hai chuyện như nhau;
- w = 2: đảo hứa khó chịu gấp đôi;
- w = 0,5: đảo hứa khó chịu bằng một nửa.

**Điểm hòa vốn w\*** là mức mà hai luật ngang nhau. Nếu w thật lớn hơn w\* thì nên chọn luật C, nhỏ hơn thì nên chọn
Elmi.

**Khác với con số 0,77 lần trước.** Con số đó đếm "muộn so với lời hứa đầu" theo mọi nguyên nhân, kể cả tắc đường. Lần
này thước đo chính **chỉ đếm phần do điều phối**: tổng có dấu của các lần điều phối dời giờ trả trong cả chuyến (`r.net`,
`engine.js:90`). Phần do tắc đường bị loại ra.

## 2. Kiểm tra trước khi tính (cổng): ĐẠT

- Chạy lại 2.520 lượt mô phỏng (60 ô × 2 cấu hình × 21 nhánh), mất 8,9 phút, 0 lỗi.
- Mọi dòng đều trùng tuyệt đối với kết quả cũ của P5 và N2, ở mọi trường số chứ không chỉ 5 trường cổng.
- Với mọi khách, tổng các lần dời do điều phối bằng đúng `r.net`.
- Khi tính lại bằng thước đo cũ, kết quả ra **0,769**, đúng con số minh họa 0,77 lần trước.

## 3. Kết quả chính: có gán lại xe (G2), hai luật phục vụ cùng số khách, so với Elmi nới (EOHZ)

Số tính cho mỗi ngày, trung bình 10 ngày. Δ = luật C trừ Elmi. "Khoảng 95%" là khoảng bootstrap theo ngày (10.000 lần).

| α của Elmi | α luật C cần để phục vụ bằng | Δ đảo hứa | Δ khách bị điều phối dời muộn > 1 phút | **w\*** | Khoảng 95% của w\* |
|---|---|---|---|---|---|
| 10% | 10,5% | −6,4 | +2,0 | 0,32 | 0 – 1,25 |
| 20% | 22,3% | −9,6 | +11,9 | 1,24 | 0,37 – 2,38 |
| **35% (đối thủ chính)** | **37,4%** | **−13,5** | **+12,7** | **0,94** | **0,46 – 1,61** |
| 50% | 53,1% | −19,1 | +7,3 | 0,38 | 0 – 0,96 |
| 75% | 89,5% | −21,6 | +15,7 | 0,73 | 0,44 – 1,10 |
| 100% | ngoài lưới: luật C ở α = 100% vẫn phục vụ ít hơn Elmi | | | | |

**Đọc kết quả ở α = 35%:**
- Mỗi ngày, luật C bớt **13,5 khách bị đảo hứa** (36,4 xuống 22,9), đổi lấy thêm **12,7 khách bị điều phối dời muộn quá 1
  phút** (188,1 lên 200,8).
- Luật C đáng dùng nếu khách thấy **một lần bị đảo hứa khó chịu ít nhất gần bằng (0,94 lần) một lần bị dời muộn quá 1
  phút**.
- Tỉ lệ lần bootstrap mà luật C được chọn:

  | w | 0,5 | 1 | 2 |
  |---|---|---|---|
  | Chọn luật C | 4% | 58% | 99,8% |

  Ở w = 1 kết quả như tung đồng xu. Luật C chỉ thắng rõ khi khách ghét đảo hứa **gấp đôi** trở lên, và thua rõ khi khách
  chỉ ghét đảo hứa bằng **một nửa** trở xuống.
- Khoảng 95% của w\* là 0,46 – 1,61: số liệu không đủ để nói w\* nhỏ hơn hay lớn hơn 1.
- Ở mọi α trong bảng, w\* nằm trong khoảng **0,3 – 1,2**. Theo điểm ước lượng, không có mức α nào mà luật C hơn Elmi trên
  cả hai thước đo khi phục vụ cùng số khách.
- Trong bootstrap, trường hợp "luật C hơn trên cả hai thước đo" chỉ xuất hiện ở α = 10% (24% số lần) và α = 50% (6%).

## 4. Phần "muộn" là do điều phối hay do tắc đường?

**Do điều phối.** Ở α = 35% (G2, phục vụ bằng nhau):
- Chênh lệch "bị điều phối dời muộn quá 1 phút" là **+12,7** khách mỗi ngày.
- Chênh lệch "muộn so với lời hứa đầu, mọi nguyên nhân" là +10,4.

Tính theo phút thì rõ hơn: so với Elmi nới, tổng số phút khách tới muộn hơn lời hứa đầu (cộng A − p0 của mọi khách được
phục vụ) tăng **186,0 phút** mỗi ngày, trong đó **180,3 phút** là tổng phần dời do điều phối, tức khoảng **97%**
(`BREAKEVEN-TABLE-v2.tsv:28`, cột `dDA_min` và `dNet_min`; `be_run.js:57`). Dời quá 2 phút cho khoảng chênh
+13,4 khách, nên đây không phải những lần dời nhỏ vừa vượt ngưỡng 1 phút.

*Đính chính 26/10 (phản hồi của máy công ty):* bản trước viết "phần do điều phối còn lớn hơn tổng" khi so hai số đếm theo
hai ngưỡng khác nhau (+12,7 và +10,4 khách). Hai số đếm ấy không cộng trừ được với nhau; phép so theo phút ở trên mới đúng.

Ngay cả với Elmi nới α = 35%, trung bình mỗi khách tới muộn hơn lời hứa đầu 1,09 phút. Trong đó **1,00 phút do điều phối**.
Phần do tắc đường gần như triệt tiêu giữa ca sáng (muộn hơn) và ca chiều (sớm hơn).

Vì thế w\* tính theo phần do điều phối (**0,94**) còn hơi bất lợi cho luật C hơn con số minh họa 0,77.

## 5. Các so sánh khác

**Chỉ chèn (INS), phục vụ bằng nhau, so với Elmi nới:**
- Hai luật gần như trùng nhau: Δ đảo hứa chỉ −1,5 đến −2,0 khách mỗi ngày, Δ bị dời muộn từ −2,1 đến +1,0.
- w\* điểm là 0 hoặc 0,25 – 0,47, nhưng khoảng 95% rất rộng: cận trên từ 1,4 tới 10.
- Kết luận: **không phân biệt được**. Đúng như đã biết, phần "hai chiều" của luật C không có việc gì để làm khi chỉ chèn.

**So với Elmi chặt (EO), phục vụ bằng nhau:**
- G2: w\* thấp hơn, từ 0 tới 0,65. Ở α = 10%, điểm ước lượng cho thấy luật C hơn trên cả hai thước đo. Ở α = 35%, w\* là
  0,65, khoảng 0 – 1,50.
- INS: luật C hơn trên cả hai thước đo ở mọi α. Lợi thế nhỏ: bớt 1,5 – 2,4 lần đảo hứa và 1,4 – 7,3 khách bị dời muộn mỗi
  ngày.
- Elmi chặt là đối thủ yếu hơn. Đối thủ chính (ADR-082) là Elmi nới.

**Cùng α (G2, so với Elmi nới):**
- Ở α = 10, 20, 50, 75, 100%, điểm ước lượng cho thấy luật C ít đảo hứa hơn mà không bị dời muộn nhiều hơn.
- Nhưng luật C **phục vụ ít hơn 1,6 – 9,2 khách mỗi ngày**. Phép tính w\* bỏ qua chênh lệch này, nên đây **không phải thắng
  miễn phí**: luật C trả bằng số khách phục vụ.
- Ở α = 35%: w\* = 0,31 (khoảng 0,11 – 0,52), với 4,1 khách phục vụ ít hơn mỗi ngày.

## 6. Nói được gì, không nói được gì

**Nói được:**
- Khi có gán lại xe và phục vụ cùng số khách, luật C đổi khoảng **1 khách bị đảo hứa lấy khoảng 1 khách bị điều phối dời
  muộn quá 1 phút** (w\* khoảng 0,9 ở đối thủ chính).
- Luật C đáng dùng khi người làm dịch vụ tin rằng khách ghét bị báo giờ lắc qua lắc lại ít nhất ngang bị dời muộn. Nếu
  khách ghét đảo hứa gấp đôi thì luật C thắng gần như chắc. Nếu chỉ bằng một nửa thì Elmi thắng.

**Không nói được:**
- Không biết w thật là bao nhiêu. Chưa ai đo cảm giác của khách về "đảo hứa"; muốn biết phải khảo sát hoặc thử nghiệm.
- Phép đếm coi dời 61 giây và dời 10 phút như nhau, và coi một khách bị đảo hứa một lần hay năm lần như nhau.
- "Đảo hứa" đếm theo giờ khách nhìn thấy, gồm cả lần tính lại do tắc đường.
- Chỉ có 10 ngày, đã xem trước. Bootstrap trên 10 ngày còn thô. Nội suy α là tuyến tính.
- Đây là bộ mô phỏng, không phải RideBound, và là kết quả **thăm dò**, không phải xác nhận.

## 7. Tệp

| Loại | Đường dẫn |
|---|---|
| Kế hoạch | `BREAKEVEN-PLAN.md` (+ `.sha256`) |
| Nhật ký | `BREAKEVEN-LOG.md` |
| Mã | `be_run.js`, `be_smoke.js`, `be_gate.py`, `be_table.py`; bản v1 giữ ở `be_table_v1.py` |
| Cổng | `GATE.txt` |
| Bảng chính | `BREAKEVEN-TABLE-v2.tsv` (dùng bản này) |
| Đường cong | `BREAKEVEN-CURVES-v2.tsv` |
| Tóm tắt | `BREAKEVEN-SUMMARY-v2.json` |
| Bản v1 (giữ lại) | `BREAKEVEN-TABLE.tsv`: sai cột ở 4 dòng "ngoài lưới", các dòng khác trùng v2 |
| Dữ liệu chạy | `E:/RideBoundData/research/breakeven-v1/run1/` (`rows-be.ndjson`, `riders-be.ndjson` 207 MB, `run-manifest.json`, `run-end.json`) |
| Mã hash | `BREAKEVEN-FILES.sha256` |
