# Mô phỏng nhanh của khóa luận (Chương 5)

Thư mục này chứa bộ mô phỏng nhanh **khớp FleetPy** đã sinh mọi số liệu mô phỏng nhanh của Chương 5:
Bảng 5.4–5.11, Hình 5.2–5.3 và khảo sát quy mô đội xe. Bảng 5.2, 5.3 và Hình 5.1 lấy từ chương trình chính
(RideBound C# chạy trong FleetPy); xem `src/analysis/report_tables.js` và mục "Dựng bảng" dưới đây.

Bộ mô phỏng chạy trên **đúng các fixture mà RideBound C# dùng**. Fixture do `src/adapter/convert_via_full_v2.py`
sinh ra; file đó giống từng byte với bộ chuyển đã dùng khi làm khóa luận.

## Code này đến từ đâu

Mọi file `.js`, `.py`, `.sh` ở đây được chép **nguyên văn** từ bản đã chạy khi làm khóa luận (8/11/2026 và lần chạy lại
Bảng 5.7 ngày 16/11/2026). Thay đổi duy nhất là tên thư mục của máy đã chạy: code cũ ghi đường dẫn tuyệt đối, bản này
đọc chúng từ biến môi trường (`paths.js` cho JS, `os.environ` cho Python và shell). Không dòng logic nào bị sửa.
Tổng cộng 82 dòng đổi trong 29 file; mỗi dòng là một tên thư mục hoặc một lệnh `import os` đi kèm.

| Thư mục | Nội dung |
|---|---|
| `adapter60/` | Bộ chạy đồng bộ (`emulate_adapter.js`; `_pp` thêm phạt đổi xe và hệ số đều; `_eta` thêm sai số giờ báo), script chạy (`run_*.sh`), danh sách ô (`cells-*.txt`), hiệu chỉnh đều (`calib_pad.py`, `flat_iter*.json`, `calib-result.json`), dựng bảng (`aggregate.py`, `gen_tables.py`, `eta_analysis.py`, `gen_eta_tables.py`, `full_common.py`, `shadow_scope.py`, `make_figs.py`), kiểm cổng (`check_*.py`) |
| `engines/` | Engine: `engine_wf15_clint.js` (làm tròn mỗi chặng dự báo về mili giây nguyên như RideBound); `engine_wf15_clint_pp.js` thêm hai tùy chọn tắt mặc định: phạt đổi xe (`g2PenaltyMs`) và hệ số đều (`flatRatio`). Hai engine có các nhánh NCE/OCE của Bảng 5.7. |
| `wf20/`, `wf5/`, `wf15emu/` | Các module engine và bộ chạy gọi tới: chấm điểm Shadow, cường độ nhu cầu, dự báo, chỉ số, mạng đường, đọc fixture |
| `sync-rerun/` | Bảng 5.7 (`table57.js`), so chữ ký dòng (`compare_sig.js`), kiểm tái lập mỗi họ một ô (`repro_spotcheck.sh`) |
| `data/` | Tham số đã đóng băng, đặt vào data root (xem dưới): Shadow `wf15/frozen/shadow-params.json` (sha256 `62ae26a6…`), prior giờ trước, chỉ số dự báo (toàn tháng và tháng trước), quy tắc hiệu chỉnh θ |
| `results/` | Dòng kết quả gốc của mọi họ chạy (`MANIFEST.tsv` ghi sha256 và số dòng) |

## Dựng bảng từ dòng kết quả đi kèm (không chạy mô phỏng)

Cần Python 3 (chỉ thư viện chuẩn; `make_figs.py` cần thêm matplotlib) và Node 20. Các script đọc `results/` mặc định.

| Trong khóa luận (bản 22/11) | Lệnh | Ghi chú |
|---|---|---|
| Bảng 5.2, Hình 5.1 | `node src/analysis/report_tables.js --data src/analysis/data` | Chương trình chính. TV5 in −37,70 (khóa luận −37,71: tệp chuỗi đi kèm không cộng 8 khách chỉ có ở lần chạy C#) |
| Bảng 5.3 | `python adapter60/gen_eta_tables.py` (khối "5.5") | Đọc `full_common.json` (tính từ bản ghi lời hứa của các lượt chạy C#, không đi kèm repo) |
| Bảng 5.4, 5.5, 5.6 | `python adapter60/aggregate.py` rồi `python adapter60/gen_tables.py` (khối "Bảng 5.5", "Bảng 5.6") và `python adapter60/shadow_scope.py` | Tên khối trong script là số bảng cũ. `aggregate.py` ghi lại `aggregate.json`; nếu file không đổi thì số không đổi |
| Hình 5.2 | `python adapter60/make_figs.py` | Ghi vào `results/figures` (hoặc `RB_FIGURES`) |
| Bảng 5.7 | `node sync-rerun/table57.js results/nce-oce60 results/nce-oce60b results/nce-oce60c` | 360 ô, 60 ngày |
| Bảng 5.8, 5.9, 5.10 | `python adapter60/gen_eta_tables.py` (khối "5.4", "5.6") | Đọc `eta_analysis.json`. Chạy lại `eta_analysis.py` cần thêm bảng từng khách `eta-*.tsv` (sinh khi chạy lại với `--riders`) |
| Hình 5.3, quy mô đội xe, Bảng 5.11 | `python adapter60/gen_tables.py` (khối "Ngân sách", "Bảng 5.12", "Bảng 5.13") | |

## Chạy lại mô phỏng

### 1. Data root

Mọi đầu vào lớn nằm trong một thư mục, đặt tên trong biến `RB_DATA`:

```
$RB_DATA/
  wf5/data/nyc-via/fleetpy-manhattan/*.csv     mạng đường FleetPy Manhattan (src/emulator/fetch_network.js)
  wf6/via-many-days/work/ff_apsp_u16.bin        bảng thời gian đi ngắn nhất (fetch_network.js tự dựng)
  wf14/scenarios/weekdays-P512/                 các ô 60 ngày (src/emulator/gen/gen17.js)
  wf17/r1-identity-fence-pad/cells/f16/weekdays-P512/   các ô F16 (gen17.js, --out vào thư mục này)
  wf15/, wf20/, wf7/, wf21/                     chép từ src/fastsim/data:  cp -r src/fastsim/data/. "$RB_DATA"/
```

Mạng đường và các ô sinh theo `README.md` của repo, mục 3.2, bước 1–4 (cần DuckDB và dữ liệu TLC công khai).

### 2. Fixture (đầu vào chung với RideBound C#)

```bash
export RB_DATA=/duong/dan/data-root  PY=python
RB_FIXTURE_OUT=$RB_DATA/../fixtures60 bash src/fastsim/adapter60/convert60.sh        # 276 ô của 60 ngày
RB_FIXTURE_OUT=$RB_DATA/../fixtures81 bash src/fastsim/adapter60/convert_fleet81.sh  # ô 81 xe cho khảo sát đội xe
```

Các ô của 30 ngày F14/F16 dùng fixture của lượt chạy RideBound C# (sinh bằng cùng bộ chuyển).

### 3. Chạy

```bash
export RB_DATA=...  RB_ROOTS="<root 1>;<root 2>"  RB_RESULTS=/thu-muc-moi  RB_FLEET81_ROOT=...
bash src/fastsim/adapter60/run_shards.sh main60 cells-60.txt "INS:U:N,INS:EO35:N,INS:EOHZ35:N,INS:RCE35:N,G2:U:N,G2:EO35:N,G2:EOHZ35:N,G2:RCE35:N,G2:SHHZ71:N,G2:SHRC71:N" 6
```

`run_all.sh`, `run_pp.sh`, `run_eta.sh` chứa đúng các lệnh của mọi họ chạy (main60, fc30, alpha10, fleet-*, pen300,
padcal, eta30). Mỗi thư mục gốc trong `RB_ROOTS` có `fixtures/` và `worlds/`. Bộ chạy không ghi đè dòng đã có.
Thời gian ở máy đã chạy (6 tiến trình): main60 khoảng 43 phút, fc30 16 phút, alpha10 8 phút, đội xe 32 phút, pen300 11 phút.

### 4. So với dòng gốc

```bash
node src/fastsim/sync-rerun/compare_sig.js $RB_RESULTS/main60 src/fastsim/results/main60 main60
bash src/fastsim/sync-rerun/repro_spotcheck.sh /thu-muc-tam     # mỗi họ chạy lại một ô và so chữ ký
```

Chữ ký `sig` của mỗi dòng là SHA-256 của (khách, phục vụ, xe, giờ báo đầu, giờ đến, các lần đổi giờ) của mọi khách.
Engine tất định: giới hạn thời gian chạy (`wallBudgetMs`) chỉ làm lượt chạy dừng với lỗi, không làm đổi kết quả.

## Giới hạn

- Ngưỡng Shadow θ và x\* (72 % INS, 71 % G2) được hiệu chuẩn trên 55 ngày riêng trước khi đánh giá, rồi đóng băng trong
  `data/wf15/frozen/shadow-params.json`. Bộ chạy dùng nguyên giá trị đó; phép hiệu chuẩn không nằm trong thư mục này.
- `full_common.py` cần bản ghi lời hứa của các lượt chạy C# (`RB_EVIDENCE`); `check_gates.py` cần các tệp dự đoán đã khóa
  (`RB_RESEARCH`). Hai nguồn này không đi kèm repo; kết quả của chúng có sẵn trong `full_common.json` và báo cáo cổng.
- `make_figs.py` tìm phông Times New Roman trong thư mục phông của Windows; trên hệ khác hình dùng phông mặc định.
