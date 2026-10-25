# N2: đường cong α. Thăm dò, sau kiểm toán; thiết kế ghi trước khi chạy (25/10/2026)

Kiểm toán §6-N2 (KT:609,670): α ∈ {0, 10, 20, 35, 50, 75, 100}% × {EO, EOHZ, RCE} × {INS, G2}.

**Câu hỏi:** "vì sao 35%". Kết luận "luật C so với Elmi" có chỉ đúng ở một điểm α không?

**Cài đặt:**
- Cùng 60 ô của P5 (`cells/plain` hoặc `cells/adapter`) và cùng runner wf15.
- Nhánh: `EO{0,10,20,50,75,100}`, `EOHZ{…}`, `RCE{…}`. α = 35 đã có trong P5.
- Không có Shadow (Shadow cần hiệu chỉnh x\* riêng, việc của N2-x).
- **Chế độ:** chọn sau khi xem P5.
  - Nếu P5 không có kết luận nào đổi dấu, chạy plain: nhanh hơn khoảng 7 lần, và đã khớp 840/840 với công ty.
  - Nếu có, chạy adapter.

**Báo cáo:**
- Với mỗi cấu hình và mỗi α: served, visRev60, pDispPct, late60First theo ngày, cùng cách tính ngày như P5.
- Vẽ served theo pDispPct và served theo visRev60, kiểu Hình 10 của Elmi.
- Ghi rõ: 10 ngày đã xem, thăm dò, không phải xác nhận.
