# Bộ giải và trần ứng viên trên các job cầu nối (25/10/2026)

**Câu hỏi.** Câu hỏi đến từ phát hiện R2 5.3.9 (lịch sử chat của công ty, `extract-full.md:1421-1424`): tỷ lệ
`ORTOOLS_BUDGET_EXHAUSTED` là bao nhiêu, và trần 100 ứng viên cắt bao nhiêu ứng viên trên các job RideBound thật đã chạy?

**Cách làm.**
- Script `count_solver_evidence.py`, chỉ đọc, chạy trên mọi job đã đạt: `main`, `elmi`, `p1/*`, `fix1/*` (trên ổ C: và E:).
  Thư mục `g2v4` không được đếm vì có cấu trúc lồng; các job G2 cùng cấu hình đã có trong `fix1bridge`.
- Đọc `solver.executionEvidence` của từng quyết định trong transcript. Đầu ra: `solver-evidence-counts-2026-10-25.tsv`.

**Kết quả: 300 job, 232.103 quyết định.**

| Chỉ số | Giá trị |
|---|---|
| Lần giải chính và lần giải cuối | 232.103/232.103 `optimal` (`ORTOOLS_OPTIMAL`) |
| Quyết định có xe chạm trần ứng viên | 0 |
| Ứng viên khả thi bị trần cắt | 0 |
| Lần xe hết ngân sách khám phá (`workBudgetExhausted`) | 0 |
| Ứng viên bị bỏ ở tầng chọn | 0 |
| Số ứng viên khả thi lớn nhất của một xe trước khi áp trần | 12 (trần là 100) |

**Kết luận.**
- Trên các ô Manhattan đã bắc cầu, trần 100 ứng viên và mọi ngân sách CP-SAT **chưa bao giờ ràng buộc**.
- Vì vậy cầu nối không nói gì về hai cơ chế này. Ở ô Via P512, wf9 đo được ngân sách sinh hỏng ở 49/5.100 quyết định, và G2
  thêm tới 148 ứng viên trong một quyết định. Đó vẫn là vùng chỉ có bộ mô phỏng.
- Khóa luận phải ghi đúng như vậy: "trên các ô đã kiểm, trần và ngân sách không ràng buộc; ở cỡ Via chưa kiểm".
