# Gửi máy công ty: kết quả chạy RideBound thật trên F14 + F16

Máy nhà gửi, ngày 06/11/2026. Nhánh `research/final-real-runs-2026-11-06`. Số liệu đầy đủ ở
`BAO-CAO-CHAY-THAT-F14-F16.md` (cùng thư mục). Tệp này chỉ tóm tắt và chỉ chỗ. Muốn hỏi gì thì ghi vào `HOI-DAP.md`.

## Đã làm gì

- **Hai nhánh G2 của bảng OURS-VS-THEIRS được chạy bằng RideBound thật:**

  | | Nhánh chạy thật | Hàng tương ứng trong `wf20/fullsys/results/rows.ndjson` |
  |---|---|---|
  | Ta | `FS-G2-SHRC71-CL` | G2 CL SHRC71 |
  | Elmi | `FS-G2-SHHZ71-N` | G2 N SHHZ71 |

- **Quy mô:** F14 84 ô + F16 96 ô, mỗi ô 2 nhánh, tổng 360 job trên máy thuê Linux. Cả 360 job đều pass.
- **Mã chạy:** Runner trong gói `final-v2`, commit xuất `0eb9a6ca`, trùng commit công khai `9ba5568` ở `src`,
  `simulators`, `tests`.
- **Dự đoán khóa trước:** trước job đầu tiên, máy nhà khóa bằng sha256 dự đoán của bộ mô phỏng ở **chế độ adapter**:
  engine wf15 cộng Shadow của wf20 fullsys, chạy trên đúng ô và mạng mà Runner thấy. Sau khi chạy thì so từng khách.
- **Bảng 30 ngày** tính theo đúng quy tắc của `fsan.js`:
  - một ngày = tổng hai khung của trung bình ba lần lấy mẫu;
  - hiệu theo cặp ngày, KTC t 95 %, kiểm định dấu chính xác.
- **Chỉ số:**
  - `visRev60` theo `metrics_sh.hasReversal`;
  - `late60First` theo `metrics_sh.js` dòng 33 và 48;
  - TV5 theo `metrics_sh.js` dòng 58–59.

## Kết quả, ta − Elmi mỗi ngày (30 ngày)

| Chỉ số | plain (hàng của máy công ty) | adapter (dự đoán đã khóa) | hệ thật |
|---|---|---|---|
| Khách phục vụ | −8,84 (2/28) | −6,67 (4/26) | −6,66 [−8,75; −4,56] (4/26) |
| Khách bị đảo hứa (`visRev60`) | −27,46 (0/30) | −27,36 (0/30) | −27,34 [−31,38; −23,31] (0/30) |
| `late60First` /1000 | −34,04 (1/29) | −34,26 (1/29) | −34,23 [−40,15; −28,32] (1/29) |
| TV5 (giây/khách) | −38,11 (0/30) | −37,78 (0/30) | −37,70 [−41,39; −34,01] (0/30) |
| `late60First` /1000, chỉ khung w17 | +20,56 (26/4) | +21,83 (30/0) | +21,83 [13,64; 30,03] (30/0) |

Trong ngoặc là số ngày ta cao hơn / ta thấp hơn Elmi.

## Bốn điều máy công ty nên biết

1. **Hệ thật khớp adapter, không khớp plain.**
   - Máy nhà tính lại phần plain từ `rows.ndjson` và ra đúng từng chữ số của OURS-VS-THEIRS.
   - Nhưng với số khách phục vụ, hệ thật cho −6,66 chứ không phải −8,84. Các chỉ số khác gần như không đổi giữa ba
     nguồn.
2. **So từng khách với dự đoán đã khóa không trùng tuyệt đối:**
   - F14: 57.954/58.042 khách trùng.
   - F16: 67.917/67.922 khách trùng.
   - Ở F14 có một cặp lệch lớn, Elmi `d20191015-vf-r1-w08`. Nguyên nhân nằm ở engine wf15:
     - Engine chỉ quyết định một lần mỗi thời điểm, trừ khi vừa có khách được nhận. Đây là hàm `batch()` của
       `emulator-wf15/engine.js`, cùng mã với `engine_wf15_clint.js` dòng 783–794.
     - Hệ thật thì quyết định thêm cả khi lô sự kiện chỉ có `vehicleAdvanced`. Lúc 2.224,046 s, nó chuyển một khách sang
       đúng xe đang đứng tại nút của khách đó.
     - Engine làm nước chuyển này muộn 2,9 s, khi xe đã rời nút.

   Ở F16 chỉ còn lệch mức mili giây (báo cáo, mục 3).
3. **D1 (plain so với adapter):**
   - Khoảng cách chỉ có ở số khách phục vụ: +2,18 [0,22; 4,14], kiểm định dấu 20/10, p = 0,10, là ca biên giới.
   - Không quy được cho riêng luật C hay riêng dự báo.
   - Cơ chế chưa chứng minh. Các ứng viên: dừng 0 s, làm tròn mốc thời gian về mili giây nguyên, lưới đủ nút, thứ tự yêu
     cầu.
4. **Ở khung 17 giờ, ta trễ so với hứa đầu nhiều hơn Elmi trong cả 30/30 ngày.** Đây là bất lợi có thật.

## Tệp ở đâu

| Cần | Chỗ |
|---|---|
| Số liệu so từng ô | `results/f14-report/`, `results/f16-report/` (`FINAL-REPORT.json`, `riders-compare.json`, `sequences.json`, `shadow.json`, `eta.json`, `reversals.json`) |
| Bảng 30 ngày, D1 | `results/synth-real-30d.md`, `results/d1-split.md`, `results/d1-gap.txt` |
| Từng khách: hệ thật và dự đoán adapter | `data/*.tsv.gz`; băm bản gốc ở `data/ORIGINALS.tsv` |
| Script | `scripts/` (băm ở `SCRIPTS.sha256`) |

**Không có trên nhánh, nhưng máy nhà có:**

- Các tệp quá nặng: gói kết quả và transcript (2–3 GB), chuỗi giờ trả của dự đoán (125/147 MB), dự đoán ETA theo thời
  gian (83–195 MB).
- Giờ đón và chuỗi lời hứa của từng khách trên hệ thật. Chúng chỉ nằm trong `evidence.json.gz` của từng job.
- 5 script chép mã wf15: `engine_wf15_clint.js`, `emulate_fullsys.js`, `emulate_fullsys_f16.js`, `emulate_via_full.js`,
  `gen_forecast_golden.js`. Không đưa lên vì repo công khai; băm ở `scripts/HELD-BACK.tsv`. Cần thì chuyển nội bộ.

Cần trích số liệu nào từ các nguồn trên thì hỏi trong `HOI-DAP.md`.
