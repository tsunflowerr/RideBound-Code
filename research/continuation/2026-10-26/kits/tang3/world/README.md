# T3.5 (phần logic) — Thế giới v1 và dự báo tách rời

Trạng thái: logic xong 2026-09-24; **đã tích hợp** vào FleetPy ngày 2026-09-25 (mục "Tích hợp" dưới đây). Bản v1 được
dùng cho thí điểm T3.7; bản v1.1 được dùng cho toàn bộ phép quét C/M⁺ (156 job) và 28 job luật phục hồi (b). (Cập nhật
2026-09-29; dòng này trước ghi "chưa tích hợp".)

## Thiết kế (theo quyết định 7a của T3.1)

- Hệ số thời gian đi của cặp OD `od` ở khung `b` (900 s):
  `f(od, b) = base(b) · exp(L(od, b)) · (1 + S(od, b))`.
  - `base`: hệ số Manhattan đo thật của Tầng 2 (tùy chọn).
  - `L`: nhiễu log hai chiều, AR(1), quay về 0.
  - `S`: các cú sốc; mỗi cú khởi phát ở một khung với xác suất `shock_rate` và hồi phục theo chu kỳ bán rã `shock_half_life`.
- **Số ngẫu nhiên dùng chung có khóa.** Mọi số ngẫu nhiên là băm SHA-256 của `(worldSeed, od, bin, component)`. Không dùng RNG toàn cục, nên giá trị không phụ thuộc thứ tự hay số lần truy vấn: mọi nhánh cùng hạt giống thấy cùng một thế giới.
- **Thời gian đi tích phân tốc độ `1/f` theo khung.** Nhờ vậy khởi hành muộn hơn không bao giờ tới sớm hơn (FIFO).
- **Dự báo "điều kiện hiện tại".** Dự báo chỉ đọc khung đã bắt đầu tới `now`, và giữ nguyên hệ số đó cho tương lai.
- Mạng vẫn là bao đóng OD 96 điểm (K8), **không** phải mạng đường phố.

## Kiểm chứng

- `test_world_v1.py`: 0 lỗi.
  - Tất định; không phụ thuộc thứ tự truy vấn.
  - Hạt giống và cặp OD có tác dụng.
  - Dương; hai chiều.
  - Có sốc và sốc hồi phục.
  - Thành phần `base`.
  - FIFO qua biên khung.
  - Dòng tự do; khoảng cách 0.
  - Dự báo không đọc tương lai (đổi tương lai, dự báo giữ nguyên).
- `mutate_world.py`: 6/6 đột biến bị bắt:
  - dự báo nhìn trước một khung;
  - bỏ tích phân qua biên khung (phá FIFO);
  - dùng RNG toàn cục;
  - bỏ hạt giống khỏi khóa;
  - nhiễu một chiều;
  - bỏ `base`.

## Tích hợp (2026-09-25)

- **`preflight_world.py`:** bọc đúng preflight medium của repo, giống cách Tầng 2 bọc (`deadline/preflight_drift_v2.py`),
  và không sửa repo. Mỗi khi một trigger rơi vào khung mới, mọi cạnh của bao đóng 96 điểm được đặt bằng
  free-flow × hệ số của thế giới **neo** (`AnchoredWorld`, khung 0 = đúng fixture). Việc đặt đi qua hook
  `NetworkBasic._set_edge_tt` của FleetPy. Sau đó bộ đệm cung của adapter bị xóa và cờ `dirty` được bật.
- **Thế giới và dự báo tách rời:** FleetPy chạy xe trong thế giới. Runner chỉ nhận ảnh chụp
  `travelTimesUpdated` do adapter dựng từ routing lúc gửi lô, và giữ phẳng ảnh chụp đó về tương lai (dự báo
  điều kiện hiện tại). `EdgeTimes.apply` chỉ đọc khung chứa `now`.
- **Cấu hình thế giới:** JSON khóa chặt (`worldVersion`, `worldSeed`, `sigma`, `rho`, `shock*`, `base`). `base`
  là ngày đo của Tầng 2 hoặc danh sách giá trị theo khung. SHA-256 của cấu hình được in vào log.
- **Kiểm:**
  - `test_preflight_world.py`: 0 lỗi, với mạng giả. Kiểm khung 0 = fixture, hệ số riêng từng cạnh, đổi tương
    lai giữ quá khứ, cờ `dirty`/bộ đệm, và cấu hình khóa chặt.
  - `mutate_preflight_world.py`: 7/7 đột biến bị bắt.
- **Kiểm đầu-cuối:** `run_world_smoke.py` chạy hai thế giới giống nhau tới khung 3 và khác từ khung 4. Mọi
  frame trước 3600 s phải trùng từng byte.
  - **Kết quả (2026-09-25):** ô `d20181114-s10-r1`, nhánh `conduct-30`, cấu hình `configs/wp4-tier3-v1.json`
    (bật cả `forced-reference-v1` và `record-v1`), Runner `runner-tier3-v1`, cùng nhãn `t35-leak`. Cả hai lần chạy
    kết thúc với `status: pass`, mỗi lần 686 s. Hash cấu hình: thế giới A `c1485099…648889`, thế giới B `d4281c16…33c9a`.
  - **Tiền tố trùng từng byte:** 1051 frame đầu, tức mọi frame trước frame có thời điểm đầu tiên ≥ 3600 s. Frame
    khác đầu tiên là lô sự kiện lúc 3.624.394 ms, lô đầu tiên của khung 4. Không có frame nào trước 3600 s xuất hiện
    sau điểm cắt. **LEAKAGE CHECK PASS.**
  - Trung thực: bản `compare()` đầu tiên lọc theo `simTimeMs` và tính 3 frame cuối không có thời điểm
    (checkpoint, shutdown) là "trước mốc", nên báo FAIL trên một tiền tố thực ra trùng. `inspect_smoke.py` chẩn đoán
    lỗi đó; tiêu chí đã sửa thành "tiền tố trước frame có thời điểm đầu tiên ≥ mốc".
  - **Cơ chế Tầng 3 trên dữ liệu thật** (`smoke_counts.py`):
    - A: 756 quyết định, 0 certificate non-normal, 213 exogenous breach (ADR-049), 2 lần lên xe muộn được ghi
      (trễ nhất 8.872 ms).
    - B: 557 quyết định, 0 non-normal, 222 exogenous breach, 1 lần lên xe muộn (247.440 ms).
    - Không có T3.4, các lần lên muộn này đã làm run chết vì `window-wall`. Cổng hành vi `C` không kích hoạt phục
      hồi forced lần nào, khớp H1; đây mới là một ô, chưa phải bằng chứng.
- **Runner:** publish từ `69512d3` vào `E:\RideBoundData\research\runner-tier3-v1`. Manifest
  `../runner-tier3-v1.sha256.tsv`: tree `5049f6d3…52c5cb`, 429 file.
- **Giới hạn:** nhiễu theo cạnh, nên khi bất đẳng thức tam giác vỡ, FleetPy có thể đi hai cạnh; thời gian OD hiệu
  lực là đường ngắn nhất. FleetPy giữ thời gian cạnh không đổi trong một khung; tích phân FIFO của
  `travel_time` là khái niệm của oracle. Đây không phải mạng đường phố.
- **Còn lại:** chốt `sigma`, `rho`, `shock_*` ở thí điểm T3.7, trước tiền đăng ký.

## Phát hiện ở thí điểm T3.7 và bản v1.1 (2026-09-25; từ 2026-09-25 tối v1.1 được dùng cho phép quét và no-worse)

- **Phát hiện:** `install` chỉ áp khung mới ở `_trigger_and_resolve` đầu tiên từ lúc khung bắt đầu trở đi. Đồng hồ sự
  kiện của preflight medium chỉ dừng ở ba loại mốc (`actual_fleetpy_medium_preflight.py:583-634`): khách tới, mốc vật
  lý của xe (phụ thuộc nhánh) và bộ đếm drain.
  - Giữa lúc khung bắt đầu và sự kiện kế tiếp, xe vẫn chạy theo thời gian cạnh của khung cũ.
  - Đo trên đủ 60 log:
    - Khung được áp trễ tới **265 s** trên mọi khung (W4 sáng, khung 9); tới 149 s trên các khung cả 5 nhánh cùng đi
      qua.
    - Thời điểm đổi khung lệch nhau giữa các nhánh tới 131 s (W5 tối, khung 8).
    - Nguồn: `../pilot/pilot_worlds.py`, và kiểm độc lập ở `../pilot/independent/`.
  - Hệ số mỗi khung vẫn giống hệt nhau giữa các nhánh.
- **Tầng 2 có cùng cơ chế** (`deadline/preflight_drift_v2.py:110-124`). Đo bằng `../pilot/tier2_switch_lag.py`:
  - trễ trung vị 22 s, lớn nhất 210 s, trên 865 lần đổi mốc;
  - lệch giữa các họ lớn nhất 70 s, trung vị 0 s, 1,4% số mốc lệch > 60 s, trên 208 mốc có ≥ 2 họ đi tới.
  - Bản đầu ghi "16 s" vì chỉ so các mốc mọi họ đi tới; con số đó sai và đã được sửa.
  - Đây là một giới hạn phải ghi cho các so sánh giữa họ ở Tầng 2.
- **v1.1 (`preflight_world_v1_1.py`):** bọc `_next_physical_boundary` để trả min(mốc gốc, đầu khung kế tiếp > now).
  - Đồng hồ dừng đúng mỗi đầu khung, nên mọi nhánh đổi khung đúng lúc `bin × 900 s`.
  - Hệ quả: thêm một trigger, tức thêm một lô gửi Runner, ở mỗi đầu khung không trùng sự kiện khác. Trigger này không
    phụ thuộc nhánh.
  - `preflight_world.py` được import, không sửa, vì thí điểm đã chạy trên nó.
- **Kiểm:**
  - `test_preflight_world_v1_1.py`: 14 kiểm, 0 lỗi. Có vòng sự kiện giả với hai "nhánh". Test này cũng tái hiện được
    lỗi v1: trễ, phụ thuộc nhánh, và bỏ sót khung 4 khi không có sự kiện nào trong khung.
  - `mutate_preflight_world_v1_1.py`: 5/5 đột biến bị bắt.
  - Sai sót của tôi, đã sửa: bản test đầu import `test_preflight_world`. Tệp đó `raise SystemExit` ngay khi import,
    nên test v1.1 thoát trước khi chạy mà vẫn báo `0 failure(s)`. Test hiện đếm số kiểm và fail nếu < 14.
- **Smoke đầu-cuối v1.1 (2026-09-25): PASS.**
  - Script `run_world_v11_smoke.py`, trên ô phát triển `d20181112-s10-r1-w08`. Output ở
    `E:\RideBoundData\research\tier3-world-v11-smoke`; kết quả kiểm ở `logs/v11-smoke-check-2026-09-25.txt`.
  - 4/4 job `status: pass`, mỗi job 1048–1063 s.
  - Mọi lần đổi khung rơi đúng `bin × 900 s` (0, 900, …, 8100 s).
  - `C-30` và `V-30` trên W4 đổi khung cùng thời điểm trên 9 khung chung.
  - Hệ số mỗi khung giống hệt v1.
  - Rò rỉ: 961 frame đầu trùng từng byte; frame khác đầu tiên đúng lúc 3.600.000 ms. **LEAKAGE CHECK PASS.**
  - Audit v/e/p (`../evidence/audit_vep.py`) chạy đúng trên hai job W4.
- **So với v1 trong thí điểm, cùng ô W4 sáng:**
  - `C-30`: 771 quyết định thay vì 762 (thêm trigger ở đầu khung). Kết quả không đổi: 94 khách, E2 12/94, ΣC 34.849 ms,
    0 phục hồi.
  - `V-30`: 754 quyết định thay vì 730. Phục vụ 92 khách thay vì 90; E2 0,043 thay vì 0,056.
  - Tức riêng lệch thời điểm đổi khung đã làm khoảng cách `C-30 − V-30` ở ô này đổi từ +4 thành +2 khách. Nhiễu này
    cùng cỡ với các khác biệt của thí điểm. Đó là lý do nên dùng v1.1 cho ma trận chính.
  - Dùng v1.1 hay không vẫn là quyết định thiết kế của chủ nghiên cứu trước tiền đăng ký.
- Docstring của `preflight_world_v1_1.py` được sửa chữ (con số trễ) khi smoke đang chạy. Các job đã nạp tệp lúc
  11:27 và thay đổi không đụng tới code.
- Bộ đếm "bin-start boundaries used" đếm số lần hàm bọc trả về đầu khung, không phải số lần đồng hồ thật sự dừng ở
  đó: 15 lần cho 9 lần đổi khung.
