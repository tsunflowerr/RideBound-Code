# T3.6 — Hồ sơ bằng chứng nghiên cứu (audit `v/e/p` có dấu, bảng episode, bộ kiểm độc lập)

Trạng thái: **đạt tiêu chí nghiệm thu trên hai episode thử** (2026-09-25). Không sửa repo, không đổi giao thức.

## Phát hiện chính: không cần đổi giao thức

T3.1 ghi rằng biên bản không lưu mốc `e`, và đúng là hành động `promisePublished` chỉ mang `p` cùng ba độ lệch
**tuyệt đối**. Nhưng checkpoint cuối của Runner giữ **toàn bộ sổ cam kết**. Mỗi lần sửa lời hứa lưu
`previousPromise` (`v`), `exogenousProjection` (`e`) và `publishedPromise` (`p`), với ETA nguyên chính xác. Vì vậy
dòng audit có dấu dựng lại được đúng từ checkpoint, và ticket C02/P03 được đáp ứng mà không đụng lõi. Giới hạn:
lần chạy không có checkpoint cuối thì không có dòng audit; nó được báo `finalCheckpoint: false`, không bị bỏ qua.

## Tệp

- `audit_vep.py`:
  - dựng dòng audit có dấu `x = e − v`, `c = p − e`, `z = p − v`, theo khách và tọa độ;
  - tọa độ đón đóng lúc lên xe thật;
  - tính tổng bằng oracle độc lập T3.2 (`../oracle/accounting.py`);
  - ghi bảng episode ra JSON.
- **Hai đối chiếu với bản ghi sản xuất** (dừng có kiểu `EvidenceError` nếu sai):
  1. `|x|`, `|c|`, `|z|` bằng đúng delta `exogenous`, `decisionInduced` và `visible` của sổ, cho cả hai tọa độ;
  2. mỗi lần công bố trong sổ khớp **đúng một** hành động `promisePublished` của một quyết định đã ACK, cùng `p`,
     và ngược lại.
- `METRICS-SPEC.md` v1.1: định nghĩa để người khác cài lại từ đầu. v1.1 chốt 9 điểm mơ hồ mà bộ kiểm độc lập báo.
- `independent/verify_independent.py`: do một agent khác viết từ đặc tả, không đọc `audit_vep.py` hay
  `accounting.py` trước khi có kết quả riêng. Chỉ dùng thư viện chuẩn.

## Kết quả trên hai episode thử (`tier3-world-smoke-v1`, ô `d20181114-s10-r1`, nhánh `conduct-30`)

| Trường | world-a | world-b |
|---|---|---|
| requests / completed / rejected | 108 / 93 / 15 | 108 / 65 / 43 |
| E1 (tỉ lệ phục vụ) | 0,8611 | 0,6019 |
| E2 (khách có `V` giờ trả > 60 s) | 4/93 | 6/65 |
| CVaR₉₅ `V` giờ trả | 179.538 ms | 564.621 ms |
| `ΣC` / `ΣX` / `ΣV` giờ trả | 26.484 / 2.130.704 / 2.157.188 | 26.484 / 3.434.331 / 3.460.815 |
| `ΣC` / `ΣX` / `ΣV` giờ đón (dòng mở) | 0 / 465.017 / 465.017 | 0 / 754.596 / 754.596 |
| quyết định đã ACK / non-normal | 756 / 0 | 557 / 0 |
| exogenous breach / lên xe muộn (trễ nhất) | 213 / 2 (8.872 ms) | 222 / 1 (247.440 ms) |

- **Kiểm độc lập:** mọi trường khớp **tuyệt đối** ở cả hai thế giới. Hai đối chiếu đều qua: 2389 và 1578 lần sửa;
  2482 và 1643 lần công bố. Tổng giờ đón của bộ kiểm độc lập cũng trùng.
- **Quan sát:** `ΣC` giờ trả ở mỗi thế giới đến từ **một** lần sửa duy nhất (lúc 3091 s, trong đoạn chung). Dưới cổng
  hành vi, quyết định gần như không đổi giờ trả của khách đang có. Mọi so sánh dựa trên `C` giữa hai thế giới này
  vì thế chỉ có n = 1. Đây là một dấu hiệu "tập lựa chọn mỏng" cần đo ở thí điểm T3.7.

## Chưa làm (thuộc T3.7 trở đi)

- Thống kê thất bại đủ mẫu số trên nhiều episode, gồm episode chết, tách theo lý do.
- Chỉ số cơ hội lựa chọn (`choice_metrics.py`).
- Đột biến bằng chứng theo P5: schema drift, sai hash, thiếu run hỏng, sửa timestamp, sai cohort, ACK trùng. Hiện
  mới có kiểm hash frame, ACK và tính liên tục; đột biến có hệ thống còn nợ.
