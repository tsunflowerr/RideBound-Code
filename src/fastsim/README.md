# Mô phỏng nhanh

Bộ mô phỏng nhanh viết lại luật điều phối của RideBound bằng JavaScript và chạy trên **đúng các fixture của chương trình
chính** (sinh bằng `src/adapter/convert_via_full_v2.py`). Nó sinh các kết quả mô phỏng nhanh của báo cáo
(Bảng 5.4–5.11, Hình 5.2–5.3, khảo sát quy mô đội xe), dự đoán đã khóa trước các lượt chạy chương trình chính, và đầu vào
Shadow của các lượt chạy đó. Cách chạy: `README.md` ở thư mục gốc, mục 2.1, 3.2 và 3.3.

## Nguồn gốc của code

Các file `.js`, `.py`, `.sh` trong `adapter60/`, `engines/`, `fullsys/`, `wf20/`, `wf5/`, `wf15emu/`, `sync-rerun/`
được chép nguyên văn từ bản đã chạy cho báo cáo; tên thư mục giữ như lúc đó. Thay đổi duy nhất là đường dẫn: code cũ ghi
đường dẫn tuyệt đối của máy đã chạy, bản này đọc thư mục dữ liệu từ biến môi trường (`paths.js` cho JS, `os.environ` cho
Python và shell). Không dòng logic nào bị sửa.

| Thư mục | Nội dung |
|---|---|
| `engines/` | `engine_wf15_clint.js`: engine; khi có dự báo, mỗi chặng được làm tròn về mili giây nguyên như RideBound. `engine_wf15_clint_pp.js`: thêm hai tùy chọn tắt mặc định, phạt đổi xe (`g2PenaltyMs`) và hệ số đều (`flatRatio`) |
| `adapter60/` | Bộ chạy (`emulate_adapter.js`; `_pp` cho phạt đổi xe và hệ số đều; `_eta` thêm sai số giờ báo từng khách), script chạy từng họ (`run_shards*.sh`), danh sách ô (`cells-*.txt`), hiệu chỉnh đều (`calib_pad.py`, `flat_iter*.json`, `calib-result.json`), dựng bảng (`aggregate.py`, `gen_tables.py`, `shadow_scope.py`, `eta_analysis.py`, `make_figs.py`), chuyển ô 81 xe (`convert_via_full_v2_nveh81.py`) |
| `fullsys/` | Cho chương trình chính: `emulate_fullsys.js` (F14) và `emulate_fullsys_f16.js` ghi dự đoán từng khách và `predictions/shadow-inputs/<ô>.json`; `emulate_forecast_gate.js` ghi hồ sơ cổng dự báo; `emulate_sequences*.js` ghi chuỗi giờ trả dự đoán |
| `wf20/`, `wf5/`, `wf15emu/` | Các module mà engine và bộ chạy gọi tới: kịch bản, chấm điểm Shadow (`sscore.js`), cường độ nhu cầu (`lambda.js`), dự báo (`forecast.js`), chỉ số (`metrics_sh.js`), mạng đường, đọc fixture |
| `sync-rerun/` | Bảng 5.7 (`table57.js`), so chữ ký hai lần chạy (`compare_sig.js`), chạy lại mỗi họ một ô (`repro_spotcheck.sh`) |
| `data/` | Tham số đã đóng băng, chép vào data root: ngưỡng Shadow `wf15/frozen/shadow-params.json` (sha256 `62ae26a6…`), prior giờ trước, chỉ số dự báo (toàn tháng và tháng trước), quy tắc hiệu chỉnh θ, danh sách tập ngày |
| `sample/` | Hai ô mẫu (`wf14/`: file yêu cầu, ma trận thời gian đi, provenance), `kiem_nhanh.sh`, chữ ký mong đợi (`expected.tsv`), `check_sample.js`, `compare_riders.js` (so từng khách giữa RideBound và mô phỏng nhanh) |

## Biến môi trường

| Biến | Ý nghĩa |
|---|---|
| `RB_DATA` | Data root: `wf5/` (mạng đường), `wf6/` (bảng thời gian đi), `wf14/` (ô 60 ngày), `wf17/r1-identity-fence-pad/cells/f16/` (ô F16), và các thư mục chép từ `data/` |
| `RB_ROOTS` | Các thư mục fixture (mỗi thư mục có `fixtures/` và `worlds/`), cách nhau bằng `;` |
| `RB_FLEET81_ROOT` | Thư mục fixture 81 xe (khảo sát quy mô đội xe) |
| `RB_RESULTS` | Thư mục kết quả: script chạy ghi vào đây (không ghi đè), script dựng bảng đọc từ đây |
| `RB_FIGURES` | Nơi `make_figs.py` ghi hình (mặc định `src/fastsim/results/figures`) |
| `RB_STORE` | Thư mục kết quả để `repro_spotcheck.sh` so |
| `RB_EVIDENCE`, `RB_RESEARCH`, `RB_EXPECTED_ROWS` | Chỉ cho `full_common.py`, `check_gates.py` và chế độ `plain` của `emulate_fullsys*.js`: các tệp của lượt chạy cuối, không đi kèm repo |

## Cấu hình (tên nhánh)

`<cấu hình>:<cơ chế>:<dự báo>`, ví dụ `G2:SHRC71:CL`.
- Cấu hình: `INS` chỉ chèn điểm dừng; `G2` cho phép chuyển khách đang chờ sang một trong 8 xe gần nhất.
- Cơ chế: `U` không giới hạn; `EO35` hạn cố định; `EOHZ35` hạn điều chỉnh; `RCE35` giới hạn cộng dồn (cấp đều 35 %);
  `SHHZ71`, `SHRC71` như trên nhưng cấp hạn mức theo Shadow (x\* = 71 % với G2, 72 % với INS); `NCE35` sổ có bù trừ;
  `OCE35` chỉ tính dời muộn; số sau tên là tỷ lệ cấp (Hình 5.3: 0–100).
- Dự báo: `N` không; `CL` dự báo, chuẩn hóa theo tháng của ngày nguồn; `CLc` chuẩn hóa theo tháng trước; `PAD` hiệu chỉnh đều.

## Ghi chú

- `gen_tables.py` đặt tên khối theo số bảng cũ: "Bảng 5.5" = Bảng 5.4, "Bảng 5.6" = Bảng 5.5 và 5.6 (cột G2+Shadow),
  "Ngân sách" = Hình 5.3, "Bảng 5.12" = quy mô đội xe, "Bảng 5.13" = Bảng 5.11.
- `gen_eta_tables.py` cũng dùng số cũ: "5.4: bang muc" = Bảng 5.8, "hieu du bao - hieu chinh deu" = Bảng 5.9,
  "5.6: phan ra" = Bảng 5.10; khối "5.5" (= Bảng 5.3) chỉ in khi đã có `full_common.json` (`full_common.py`, cần `RB_EVIDENCE`).
- Chạy `eta_analysis.py` với `PYTHONHASHSEED=0` để `eta_analysis.json` giống nhau giữa các lần chạy (README gốc, mục 3.2).
- `aggregate.py`, `eta_analysis.py`, `full_common.py` ghi kết quả JSON cạnh chúng (đã có trong `.gitignore`).
- Engine tất định: giới hạn thời gian chạy (`wallBudgetMs`) chỉ làm lượt chạy dừng với lỗi, không làm đổi kết quả.
- `make_figs.py` tìm phông Times New Roman trong thư mục phông của Windows; trên hệ khác hình dùng phông mặc định.
