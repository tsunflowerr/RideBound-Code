# Báo cáo cầu nối Manhattan và cổng A0.1, ngày 22/10/2026

**Người đọc:** chủ nghiên cứu.

**Trạng thái:** xong. Chưa commit, chưa sửa luận văn, chưa cập nhật docs/18–19 (đang chờ quyền commit).

Bảng số đầy đủ: `BRIDGE-TABLES-2026-10-22.txt`. Sửa đổi trong quá trình chạy: `AMENDMENTS.md` (A1).

---

## 1. Đã chạy gì

- **Runner tham chiếu:** `runner-via-ref-v1` = 7435661 + 2 patch luật của máy công ty (PS và DL-HZ, ADR-080),
  1.134/1.134 test.
- **Adapter:** E:/Code/RideBound-deadline (inventory `0da63eaf…`, giống mọi đợt chạy cũ). Thêm bản sửa đo đạc
  (đọc có bộ đệm, chờ 900 s) và trần GC 896 MiB qua `bridge_launcher.py`. Không sửa tệp nào trong repo.
- **11 ô:** sáng (w08) 7 ngày 12–18/11/2018; chiều (w17) 4 ngày 12, 13, 14, 18/11; đều mẫu r1. Trần đi tối đa 2,0×,
  giữ nguyên `scenarioId`.
  - Kiểm trước: dựng lại ở 1,5× ra **giống từng byte** kịch bản gốc (11/11 ô, 7/7 tệp mỗi ô). Bản 2,0× chỉ khác ở
    `maxRideTimeMs`, tỉ lệ đúng 2000/1500.
- **5 nhánh:** U, C-120, C-300, PS-300, DLHZ-120; bộ chọn cost-first; phục hồi no-worse; thế giới W5 (giao thông đo
  thật theo ngày); seed 7.
- **Thứ tự chạy:** hồi quy (1 job) → smoke (2 job) → chính (55 job). Tất cả 58 job ra `status-pass`.
- **Gián đoạn duy nhất:** lúc 00:54, ổ C chạm sàn 30 GiB và driver dừng đúng luật. Bạn chạy lệnh nén (giải phóng
  151,9 GiB, 2.393 tệp, 0 lỗi) rồi chạy lại lúc 09:00. Không job nào hỏng.

## 2. Kết quả có giá trị bằng chứng

| Kiểm tra | Kết quả |
|---|---|
| Hồi quy: Runner mới + patch luật (tắt) + bản sửa đo đạc + trần GC, so với job Stage 1b đã lưu | 2.311/2.311 khung giống hệt (sau sửa đổi A1 bỏ số đo RAM/CPU khỏi phép so) |
| Hai luật mới thật sự hoạt động | PS-300: 36 lần mã `COMMITMENT_REVISION_LIMIT_EXCEEDED`; DL-HZ-120: 116 lần `COMMITMENT_DEADLINE_EXCEEDED` (job smoke) |
| **Cầu nối: RideBound thật vs dự đoán bộ mô phỏng đã khóa sha256 trước khi có job chính nào** | **55/55 tổ hợp ô × nhánh khớp hoàn toàn, 4.972/4.972 khách**: t0, p0, A, phục vụ, Σ\|c\|, k, net c, max\|c\|, đi chung |
| Bộ so phân biệt được | Kết quả thật PS-300 so với dự đoán U: 61/101; với C-120: 18/100; với PS-300: 97/97 |
| **Cổng A0.1: Runner băm tăng dần phát lại mọi job đã ghi** | **58/58 job giống từng byte, 43.154/43.154 khung**, gồm hash trạng thái và checkpoint; cũng giống hệt khi bật `RIDEBOUND_CANONICAL_SELFCHECK=1` |
| CPU Runner khi phát lại | A0: trung vị 50 s/job; bản cũ: 940 s cho job hồi quy (cùng điều kiện tải). Khoảng 13 lần |

**Ý nghĩa:**
- Ở chế độ chỉ chèn, adapter-identity, trên ô Manhattan 2,0×, bộ mô phỏng **chính là** RideBound cho cả 5 luật,
  kể cả hai luật mới PS và DL-HZ. Đây là chỗ nối giữa bằng chứng của bộ mô phỏng và hệ thật.
- A0.1 làm Runner nhanh khoảng 13 lần mà không đổi một byte đầu ra nào.

**Giới hạn:**
- Cầu nối chỉ phủ chế độ chỉ chèn. **G2 không có trong RideBound**, nên chưa nối được S2.
- Cầu nối chạy ở chế độ adapter-identity. Bằng chứng Via 55 ngày của máy công ty chạy ở chế độ plain, mà hai chế độ
  khớp từng khách ở 0/20 ô (điểm B3). Phải chạy lại 55 ngày Via ở chế độ adapter (cần tệp từ máy công ty).
- Ô Via lớn hơn nhiều (P512, khoảng 850 nút). Cầu nối Manhattan 108 yêu cầu chưa chứng minh trực tiếp sự khớp ở cỡ đó.
- A0.1 mới là một phần của A0. A0.2 (đọc ống trong adapter), A0.3, A0.4 và A0.5 chưa làm.

## 3. Số liệu mô tả theo nhánh (không phải phép thử: 11 ô, 7 ngày)

| Nhánh | Hứa | Phục vụ | Khách bị dời (k ≥ 1) | Dời ≥ 2 lần | Σ\|c\| (s) | Trễ > 60 s so với hứa đầu |
|---|---|---|---|---|---|---|
| U | 1.011 | 1.011 | 323 | 84 | 68.302 | 261 |
| C-120 | 977 | 977 | 170 | 7 | 10.252 | 99 |
| C-300 | 999 | 999 | 277 | 50 | 38.372 | 215 |
| PS-300 | 1.009 | 1.009 | 293 | 61 | 43.740 | 229 |
| DLHZ-120 | 976 | 976 | 177 | 8 | 10.696 | 106 |

**Đọc thẳng, không tô hồng:**
- **C-300 so với PS-300 (tình huống S1, trí nhớ):** phục vụ ít hơn 10 khách, trễ ít hơn 14 khách. Cùng chiều với
  Via, nhưng vẫn là mẫu "chặt hơn thì phục vụ ít hơn và trễ ít hơn". **Chưa so ở cùng mức phục vụ**, nên không nói
  được trí nhớ có ích. Phải dùng phép thử đường biên.
- **C-120 so với DLHZ-120:** gần như trùng (977/976 phục vụ, 99/106 trễ). Đúng như lý thuyết: khi chỉ chèn khách,
  cộng dồn ≈ hạn chót.
- Mọi khách được hứa đều được phục vụ (hứa = phục vụ ở cả 5 nhánh).
- "Trễ > 60 s so với hứa đầu" gồm cả phần do giao thông. Không dùng con số này làm lợi ích riêng của luật.
- Σ\|c\| nhỏ dưới luật C là đúng theo định nghĩa (luật chặn chính đại lượng này). Không phải phát hiện.

## 4. Tài nguyên (cho việc chọn VM)

- **Mỗi job** (2 job song song, một phần thời gian chung máy với việc nén): trung vị 13,9 phút (6,9–19,2). Đỉnh RAM
  cả cây tiến trình: trung vị 916 MiB, tối đa 1.007 MiB.
- **Phát lại qua A0:** khoảng 50 s CPU Runner mỗi job; RAM Runner 270–660 MiB.
- **Hệ quả:** với A0.1, phần Runner của một job Manhattan còn khoảng 1 phút. Thời gian còn lại chủ yếu là FleetPy và
  adapter. A0.2–A0.5 và A1 nhắm đúng vào phần này.

## 5. Tệp chính

- **Kịch bản:** `C:/RideBoundData/research/fixtures-via-bridge-2000-v1` (hash trong `fixtures-via-bridge-2000-v1.sha256.txt`).
- **Đầu ra job:** `C:/RideBoundData/research/via-bridge-man-v1/{regress,smoke,main}`; log trong `logs/`.
- **So sánh:** `compare/final-55-2026-10-22-*`. Phát lại: `replay/a0-*.json`, `A0-GATE-SUMMARY.json`.
- **Dự đoán đã khóa:** `emulator-riders-2000.tsv` + `EMULATOR-PREDICTIONS.sha256` (khóa lúc 0 job chính xong).
- **Bộ mô phỏng:** `C:/RideBoundContinuation/2026-10-22/emulator-wf5` (chỉ sửa đường dẫn; tái tạo 4.083/4.083).
  Bản có Elmi-OC: `emulator-wf5-eoc` (`ELMI-OC-SPEC.md`).
- **Runner:** `C:/RideBoundData/research/runner-via-ref-v1` (cây `ad316b8e…`), `runner-via-a0-v1` (cây `0113e13d…`).
- **Worktree** (chưa commit): `C:/Code/RideBound-via-ref` (2 patch luật), `C:/Code/RideBound-via` (4 patch).

## 6. Việc tiếp theo

1. **Chờ tệp từ máy công ty** (`CAN-LAY-TU-MAY-CONG-TY.md`). Sau đó chạy lại 55 ngày Via ở chế độ adapter, rồi
   phép thử đường biên RC / PS / DL-HZ / Elmi-OC.
2. **Trong lúc chờ:** làm A0.2–A0.5, rồi A1 (bộ nhập ô Via, checkpoint ra tệp riêng), để RideBound chạy được Via P256.
3. **Cần bạn quyết:**
   - cho commit cục bộ hai nhánh worktree (không push) để giữ dấu vết;
   - chấp nhận G2 (xe đón sớm hơn giờ hứa);
   - dùng Elmi làm đối chứng chính.
