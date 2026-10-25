# Điểm hòa vốn giữa luật C và Elmi: kế hoạch ghi trước khi tính (26/10/2026, 00:55)

**Nhãn:** thăm dò sau kiểm toán. Dùng lại 10 ngày đã xem (60 ô P5). Mọi số là số của bộ mô phỏng wf15 của công ty, không
phải của RideBound. Không phải xác nhận.

## 1. Câu hỏi

So với Elmi (hàng rào nới EOHZ, và hàng rào chặt EO), luật C (RCE) giảm số khách bị đảo hứa nhưng có thể làm tăng số
khách bị điều phối dời giờ trả muộn hơn. Phải ghét một lần đảo hứa đến mức nào, tính bằng "một khách bị điều phối dời muộn
quá 1 phút", thì luật C mới đáng dùng? Con số này gọi là điểm hòa vốn w\*.

Lần trước con số minh họa 0,77 dùng `late60First`, chỉ số trộn phần do điều phối với phần do tắc đường. Lần này thước đo
chính chỉ đếm phần do điều phối.

## 2. Chạy

- **Ô:** 60 ô `E:/RideBoundData/research/p5-via-adapter-v1/cells/plain` (10 ngày × 2 ca × 3 lần lấy mẫu, chế độ giống công
  ty, đã khớp 840/840 ở P5).
- **Bộ mô phỏng:** bản chép nguyên byte của công ty, chỉ đọc. `runner/engine.js` sha `908a2961…`, `scripts/metrics_sh.js`
  `f1ee54cc…`, `scripts/shcore.js` `ba2b9c78…`, `scripts/shlib.js` `fe275831…`, `runner/scenario.js` `d27d0bee…`. Giống
  các tệp ghi trong `rows/n2-plain.ndjson.runs.ndjson`.
- **Cấu hình:** `INS` và `G2` đúng như `shlib.js:12-15` (G2: `reopt {g2: 99, g2Targets: 8}`, `lock: 'ratchet'`);
  `RUN_OVERRIDES` của `shlib.js:16` (trần đi xe 2,0×, dwell 0, 62 xe).
- **Nhánh:** EO, EOHZ, RCE × α ∈ {0, 10, 20, 35, 50, 75, 100}% × {INS, G2}: 42 nhánh mỗi ô, 2.520 lượt mô phỏng.
- **Trình chạy:** `be_run.js` của bộ này. Nó làm đúng đường `scoreMode off` của `shcore.runTask` (`shcore.js:99-120`,
  `164-178`): `readCell`, `SC.prepareScenario(s, RUN_OVERRIDES, dir)`, `E.simulate(P, {...CONFIGS[cfg], arm,
  wallBudgetMs: 900000})`, rồi `MET.metrics(res, A)`. Thêm các trường mới tính từ `res.riders`, và ghi một dòng cho mỗi
  khách đã được hứa.
- **Tài nguyên:** tối đa 3 luồng; chỉ bắt đầu khi máy cắm sạc và RAM trống ≥ 2 GB. Không ghi đè tệp đã có.

## 3. Định nghĩa

Mọi đơn vị thời gian là ms trong bộ mô phỏng.

- **Phục vụ (S):** số khách `served` (`metrics_sh.js:38`).
- **Đảo hứa (R):** khách có `hasReversal(zs, 60000)` (`metrics_sh.js:9-13`, `:56`), tức `visRev60`. `zs` là các lần đổi
  giờ trả mà khách nhìn thấy, `z = p − pubP` (`engine.js:716`, `:721`), gồm cả phần do tắc đường lẫn phần do điều phối.
- **Bị điều phối dời muộn (Ld, thước đo chính):** khách đã phục vụ có `r.net > 60000`. `r.net` là tổng có dấu của các
  phần dời do điều phối `c` qua cả chuyến (`engine.js:90`, hàm `charge`). `c = p − e` (`engine.js:716`), với `e` là giờ trả
  tính lại khi giữ nguyên kế hoạch cũ (`engine.js:64-66`). Trình chạy kiểm từng khách: tổng `v[2]` trên `r.revs` phải bằng
  `r.net`.
- **Muộn so với lời hứa đầu (La, thước đo phụ):** `late60First`, tức `A − p0 > 60000` với mọi nguyên nhân
  (`metrics_sh.js:33`, `:48`).
- **Mô tả thêm (không dùng để quyết định):**
  - `Ld120`: `net > 120000`.
  - `T = A − p0 − net`: phần không do điều phối, gồm tính lại theo tắc đường và sai số giờ đến cuối.
  - `Lt`: số khách có `T > 60000`.
  - Tổng `net` và tổng `A − p0` trên khách đã phục vụ.

## 4. Cổng (phải đạt trước khi tính bất kỳ số hòa vốn nào)

- Với mọi bộ (ô, cấu hình, nhánh) đã có dòng cũ, so 5 trường `served`, `visRev60`, `late60First`, `pDisp`, `breachLedger`
  phải bằng nhau tuyệt đối.
  - Dòng cũ của α ≠ 35 nằm trong `rows/n2-plain.ndjson`.
  - Dòng cũ của α = 35 nằm trong `rows/plain-a1.ndjson`. Tệp này chạy ở `scoreMode causal`, nhưng bộ chấm điểm chỉ đọc trạng
    thái nên kết quả phải trùng.
- Số dòng phải so: 60 ô × 2 cấu hình × 42 nhánh = 5.040.
- Các trường số khác có ở cả hai bên cũng được so và báo cáo, nhưng chỉ để thông tin.
- Có bất kỳ chênh lệch nào trong 5 trường thì dừng, ghi lại, không tính bảng.

## 5. Giá trị ngày và trung bình

- Giá trị ngày = tổng của hai ca, mỗi ca lấy trung bình 3 lần lấy mẫu, giống P5 và N2 (`n2_table.py`).
- Đường cong của một luật = trung bình 10 ngày của S, R, Ld, La theo α.

## 6. So sánh

Làm cho từng cấu hình INS và G2.

**(a) Cùng α.** Tính RCE − EOHZ và RCE − EO ở α ∈ {10, 20, 35, 50, 75, 100}, gồm ΔS, ΔR, ΔLd, ΔLa. Vì ΔS ≠ 0, w\* ở đây
bỏ qua chênh lệch số khách phục vụ, và bảng ghi ΔS ngay bên cạnh.

**(b) Cùng số khách phục vụ (chính).** Với mỗi α_E ∈ {10, 20, 35, 50, 75, 100} của EOHZ:
- đặt mục tiêu S\* = S_EOHZ(α_E);
- trên đường RCE (lưới α 0…100), tìm đoạn [α_i, α_{i+1}] đầu tiên tính từ α nhỏ có S_RCE(α_i) ≤ S\* ≤ S_RCE(α_{i+1});
- nội suy tuyến tính R, Ld, La theo cùng tỉ lệ f = (S\* − S_i)/(S_{i+1} − S_i).

Nếu S\* nằm ngoài [S_RCE(0), S_RCE(100)] thì không tính, ghi "ngoài lưới". Có kiểm và ghi lại đường S_RCE có tăng đơn điệu
theo α không.

So sánh phụ: cũng làm (b) với EO (hàng rào chặt) thay cho EOHZ.

**Kết quả chính:** G2, cùng số khách phục vụ, với EOHZ ở α_E = 35%, là đối thủ chính theo ADR-082. Các ô còn lại là bảng
đầy đủ.

## 7. Quy tắc quyết định

Gọi ΔR = R_C − R_E và ΔL = Ld_C − Ld_E. Đặt w ≥ 0 là mức khó chịu của một lần đảo hứa, tính bằng số khách bị điều phối dời
muộn quá 1 phút. Luật C được chọn khi w·ΔR + ΔL < 0.

| Trường hợp | Điều kiện | Kết luận |
|---|---|---|
| C-luôn | ΔR ≤ 0 và ΔL ≤ 0, không cùng bằng 0 | luật C hơn với mọi w ≥ 0 trên hai thước đo này |
| E-luôn | ΔR ≥ 0 và ΔL ≥ 0, không cùng bằng 0 | Elmi hơn với mọi w |
| C-nếu-w-lớn | ΔR < 0 và ΔL > 0 | C hơn khi w > w\* = ΔL / (−ΔR) |
| C-nếu-w-nhỏ | ΔR > 0 và ΔL < 0 | C hơn khi w < w\* = (−ΔL) / ΔR |
| hòa | ΔR = ΔL = 0 | |

Tính thêm cùng quy tắc với La thay cho Ld, để so với con số cũ 0,77.

## 8. Độ bất định

- Bootstrap theo ngày, có cặp: lấy lại 10 ngày có hoàn lại, cùng một bộ ngày cho mọi luật. 10.000 lần lặp, hạt giống
  cố định 20261026 (mulberry32). Trong mỗi lần lặp, tính lại đường trung bình, phép nội suy và trường hợp.
- **Báo cáo:**
  - tỉ lệ lần lặp rơi vào từng trường hợp;
  - phân vị 2,5 / 50 / 97,5 của w\* theo quy ước "C hơn khi w > w\*": C-luôn tính là 0, E-luôn là +∞; lần lặp rơi vào
    C-nếu-w-nhỏ hoặc ngoài lưới được đếm riêng, không đưa vào phân vị;
  - tỉ lệ lần lặp mà luật C được chọn ở w ∈ {0,25; 0,5; 1; 2; 4}.
- Chỉ có 10 ngày nên khoảng bootstrap thô. Báo cáo sẽ nói rõ điều này.

## 9. Không kết luận

- Không biết w thật; cảm giác của khách chưa được đo.
- Phép đếm coi dời 61 giây và dời 10 phút như nhau.
- Đây là bộ mô phỏng, không phải RideBound; 10 ngày đã xem; thăm dò.

## 10. Sửa đổi

Mọi thay đổi sau khi niêm phong được ghi vào `BREAKEVEN-LOG.md` kèm giờ và lý do. Không sửa tệp này.
