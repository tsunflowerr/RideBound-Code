# Máy công ty (chính)

(Máy công ty ghi vào đây: phân tích, quyết định, và các `## YÊU CẦU <số>` gửi máy nhà.)

## YÊU CẦU 3 — 2026-11-27 06:05: kiểm các gói [10.08]–[10.13]

Commit trên nhánh:

| Commit | Gỡ gì | Mục |
|---|---|---|
| `945e581` [10.08] | Nhánh chết nội bộ C#: điểm Shadow không ai đọc, đọc θ ±Infinity, lần kiểm lại trùng trong `Finish`, chốt một loại chính sách, các thành viên không ai đọc (khóa `MandatoryServiceLowerBound` luôn 0, ...), đường seed từ tệp, mã lý do sửa không dùng | C09, C12, C13 (một phần), C18, N01 |
| `a178a55` [10.09] | Chế độ băm `full-v1` (360 lượt dùng `light-v1`) và golden của nó. Vắng khóa `auditHash` nay là light; `full-v1` bị từ chối | C19 |
| `18f7afe` [10.10] | Mã chỉ test dùng: bộ mã hóa `decisionApplied`, `PendingState`, golden giao thức 01–07, bản chép chính sách và tham số `requireSafetyNoOp` (sản xuất luôn truyền true) | C25 (một phần), C26, G1-04, G2-01, G2-04, G2-05 |
| `67658ea` [10.11] | Cấu hình final/commitment và mẫu INS không ai đọc, `run_all/run_eta/run_pp.sh`, `--g2pen`/`--nveh` của driver eta và pp, danh sách ô smoke/via-full, `days_F14.csv`, 8 dòng expected không ai tra | C27, C40, C41 |
| `2546384` [10.12] | JS chết: `shcore.runTask` và những gì chỉ nó dùng, hàm/export thừa của shlib, nyclib, netfull, wf15emu; đường ghi golden của `export_network.js manhattan` | C36, C42 |
| `3e07752` [10.13] | Sửa sha trong README fixture `Final/` (cũ từ [10.03]) | — |

Không gói nào đổi giao thức hay quyết định. [10.10] có xóa thành viên trong DLL sản xuất (Contracts, Application,
Algorithms, Runner), nên phép kiểm là replay từng byte, không phải sha DLL.

Nếu YÊU CẦU 1 chưa chạy thì chạy trước: cần 4 transcript của nó làm mốc byte B1. Mọi bước dưới đây chạy ở **đầu nhánh**
(commit chứa yêu cầu này), trừ khi ghi khác.

**C# ([10.08]–[10.10])**
1. `dotnet test src/core/RideBound.slnx -c Release`: kỳ vọng 0 hỏng. Ghi tổng số test để sửa `README.md:132` (còn ghi
   "884 test"). Nếu hỏng, chạy lại ở [10.08], [10.09], [10.10] để biết gói nào. Các lớp test đáng để ý:
   `ExactSmallCommitmentDifferentialTests`, `GoldenFixtureTests`, `Wp4RunnerIntegrationTests` (seed solver nay lấy từ
   manifest, 20260729), `FinalRuns`/`FinalConfigGoldenTests` (6 lượt light-v1 + early-boarding), `RunnerSessionTests`,
   `CommitmentRunnerSessionTests`, `ProtocolHashTests`.
2. Build Runner, chạy `replay.py runner` trên 4 transcript B1: mọi khung Runner giống từng byte, kể cả `decisionHash`,
   `stateAfterHash` và chuỗi lý do từ chối. Có khác: làm lại ở [10.08], [10.09], [10.10]; gửi khung khác đầu tiên (số
   thứ tự, loại khung, khoảng 300 ký tự quanh chỗ khác).
3. Nếu còn 8 transcript ô cổng của đợt 360 lượt: `replay.py runner` cũng phải giống từng byte.
4. Mọi `job-configs-v2/wp4/*/*.json` có `"auditHash": "light-v1"`.

**Dữ liệu và JS ([10.11]–[10.12])**

5. `bash src/fastsim/sample/kiem_nhanh.sh <thư mục mới>`: `36 rows equal, 0 differ, 0 without an expected row`.
6. `sha256sum -c` cho `src/datagen/expected/network.sha256`, `wf14-60day-cells.sha256`, `wf17-f16-cells.sha256`: OK.
7. `make_job_configs.py` (README §2.2 bước 4): `commitment/SHRC71.json` có sha `40244dbf…`, `SHHZ71.json` có
   `f01cf219…`, tức bằng byte `src/core/tests/RideBound.Runner.Tests/Final/Fixtures/commitment/`. Hai sha này khác
   `ab697fe` (`d536e48d…`/`9ca0f404…`) từ [10.03], không do các gói mới.
8. Dòng lệnh `manhattan` đã đổi: `node src/datagen/export_network.js manhattan <thư mục mới> $RB_DATA`. Kỳ vọng
   `D.u16` = `4ae39087…` (bằng `ff_apsp_u16.bin` trong `network.sha256`), `network.json` = `52e30836…` (máy công ty
   đo bằng lệnh cũ ở `ab697fe` và lệnh mới đều ra số này). Nếu còn cấu hình wp4 của đợt 360 lượt thì so thêm với
   `distancesSha256`/`graphSha256` trong đó.
   Hướng dẫn nào còn dạng cũ (`manhattan out.json 1 1 1 <net> <root>`) thì đổi sang dạng mới.
9. `node src/datagen/export_network.js toy <out.json> 24 10 20261103`: phần từ `"cells"` trở đi giống
   `src/core/tests/RideBound.Algorithms.Tests/Shareability/shareability-golden-toy.json`.
10. `emulate_fullsys.js adapter`, `emulate_fullsys_f16.js adapter`, `emulate_forecast_gate.js`: các tệp predictions
    `.tsv`, `shadow-inputs/*.json`, `forecast-gate.tsv` và `-profiles.json` có sha như `ab697fe`. Bỏ qua `*-counts.tsv`
    (có cột ms).
11. Không bắt buộc: chạy lại họ main60 và eta30, so bằng `sync-rerun/compare_sig.js` (3600/3600, 1080/1080).

**Python:** chưa gói nào đụng Python. Chạy unittest như README (5 module, và `discover` có FleetPy) để có mốc trước các
gói Python sắp tới. Không cần cập nhật hash ghim nào.

**Không bắt buộc:** một lượt đầy đủ C# + FleetPy, ô w08, `FS-G2-SHHZ71-N` (README §2.2), rồi `riders.py` so với mốc
`ab697fe`: giống từng byte.

**Cần trả về:** số test C# qua, hỏng, bỏ qua (kèm tên test hỏng); replay cho từng transcript; dòng cuối
`kiem_nhanh.sh`; kết quả từng phép kiểm sha (5–10); thời gian chạy.

**Đánh số commit.** Máy nhà từng có một commit [10.07] trùng số với commit của máy công ty. Từ đây xin máy nhà thêm hậu
tố `n` vào số commit (ví dụ `[10.20n]`); máy công ty giữ số trơn.

**Đổi kế hoạch so với [10.07]:**
- **C07 không làm.** Lợi ích nhỏ. Gỡ phần kiểm pha và `HardLimit` cố định sẽ làm khóa `limits` trong tệp cấu hình bị bỏ
  qua mà không báo, và đụng tới phần bảo vệ hạn 0 mà C02 giữ.
- **C14 (nhánh waypoint) chuyển sang giao thức đợt 2**, cả C# lẫn Python. `"waypoint"` vẫn là loại điểm dừng hợp lệ
  trên wire, trong schema và ở `EventReducer`. Khoảng 28 test (có fixture `wp2/valid-bootstrap-event-batch.json`) dùng
  waypoint làm điểm dừng có thời gian chờ.
- Phần còn lại của C13 giữ: mặc định G2, `WasCacheHit`, `SolverBackedFleetSelection.Problem`, hai hằng mã lỗi.
- C25: giữ `RunnerSession.PreviousDecisionHash` và `CommittedOnlineState`, vì test của hành vi còn giữ đọc chúng.
- **Gói Python sắp tới:** [10.15] C39 (khối phân tích fc30 cũ), [10.16] C29 (bộ kiểm WP7 phía adapter, giữ
  `capability-matrix.json`), [10.17] C30 + C33 (trường evidence không ai đọc, bia mộ adapter; không đụng
  `RB_PREFER_EVIDENCE`). Sẽ gửi YÊU CẦU 4 sau.
- **C31 (bỏ đường đọc transcript của `riders.py`) để cuối cùng**, sau khi máy nhà xác nhận các gói khác bằng
  `riders.py` hiện tại.

## YÊU CẦU 2 — 2026-11-27 02:25: đếm trên dữ liệu đã lưu (trước các gói [10.08]+)

Không cần build hay chạy lượt mới. Có thể làm song song với YÊU CẦU 1.

**1. Trên 8 transcript ô cổng của đợt 360 lượt (code thời khóa luận).** Ghi số dòng khớp của từng mẫu, tất cả kỳ vọng 0:

| Mẫu | Liên quan |
|---|---|
| `"messageType":"restore"` | C20 |
| `incidentOpened\|incidentResolved` | [10.04] |
| `offerDeclined\|requestCancelled` | C23 |
| `offerProposed\|commitmentBreachDeclared\|finalizeRun\|runSummary` | C24 |
| `"kind":"waypoint"` | C14 |

Ghi thêm tập các `reasonCode` xuất hiện trong `promisePublished`.

**2. Trên 360 `evidence.json.gz`** (nếu còn):
- `N` = số dòng revision trong `audit.table` có v = e = p ở **cả** dòng pickup lẫn dòng drop của cùng lần phát. Đây chính là
  số lần phát mà [10.03] bỏ. Cho tổng N, và N theo từng nhánh FS-G2-SHRC71-CL / FS-G2-SHHZ71-N.
- `incidentLedger.incidents` có rỗng ở cả 360 lượt không.
- Tổng `nonNormalCertificates`.
- Tập giá trị `requests[].lifecycle` trong checkpoint cuối. Kỳ vọng ⊂ {completed, rejected}.
- Có lượt nào chứa `EXOGENOUS_BREACH_BRIDGE_FAILED`, `COMMITMENT_SAFETY_NOOP_REJECTED`, `SOLVER_MODEL_MAPPING_FAILED` hay
  `INTERNAL_ERROR` không.

**3. Trong mã FleetPy đã ghim:**
- `grep -n "dev_content\." src/misc/init_modules.py` (C34).
- `FleetControlBase.time_trigger` có gọi `user_cancels_request` không (C23).

## 2026-11-27 02:25 — Kết quả phân tích sâu và kế hoạch [10.08]+

**Phân tích sâu.**
- Máy công ty chạy phân tích (chỉ đọc) trên `ab697fe`: 14 agent, có kiểm chứng phản biện.
- Kết quả: 55 mục, gồm 27 gỡ được, 17 gỡ sau khi kiểm ở máy nhà, 11 giữ. Không mục nào đổi quyết định cho khách.
- Phân tích độc lập này đi tới cùng kết luận với nhận xét [10.03] ở trên: revision ledger-only (p = e = v) không còn
  được phát.

**Đối chiếu với nhánh.** [10.01]–[10.04] đã làm:
- C01 (bảng khoảng cách), C03 (material), C06 (khóa drop), C15 (bia mộ), C21 (sự cố).
- Một phần C04: còn `ServiceOrder`.
- Một phần C24: còn `offerProposed`, `finalizeRun`/`runSummary`, `witnesses` của certificate.
- Phần lớn C17: cấu hình test.

**Bổ sung cho YÊU CẦU 1.** Giữ `transcript-00.ndjson.gz` của 4 lượt mới (code `00fd3be`) và ghi sha256 từng file. Bốn
transcript này là **mốc byte B1**: mỗi gói C# không đổi giao thức về sau sẽ được kiểm bằng `replay.py runner` trên B1,
mọi khung Runner phải giống từng byte. Nếu lệnh README §2.2 không giữ transcript thì bật chế độ giữ transcript
(`run_campaign.py`, như với các ô cổng).

**Thay đổi so với mục trước.** Các commit từ [10.08] không ảnh hưởng YÊU CẦU 1, vì yêu cầu này ghim `00fd3be`. Máy công
ty sẽ chuẩn bị chúng song song; phép kiểm của mỗi commit sẽ gửi bằng các yêu cầu sau, chạy khi đã có B1. Mỗi commit đều
build được bằng `dotnet build -p:UseAppHost=false` ở máy công ty.

**Kế hoạch.** Mỗi commit chỉ gồm các mục đã kiểm chứng:

| Commit | Gói | Mục | Kiểm ở máy nhà |
|---|---|---|---|
| [10.08] | C# nhánh chết nội bộ | C07, C09, C12, C13, C14 (phần C#), C18, N01 | `replay.py runner` trên B1 + `dotnet test` |
| [10.09] | chế độ băm `full-v1` | C19 | `replay.py runner` trên B1 + `dotnet test` |
| [10.10] | chỉ test C# | C25, C26, G1-04, G2-01, G2-04, G2-05 | sha DLL sản xuất không đổi + `dotnet test` |
| [10.11] | dữ liệu và script vỏ | C27, C40, C41 | `kiem_nhanh.sh` + `sha256sum -c src/datagen/expected/*.sha256` |
| [10.12] | mã JS chết | C36, C42 | `kiem_nhanh.sh` + sha của fullsys predictions và shadow-inputs |
| [10.13] | script phân tích lặp | C39 | gen_tables, gen_eta_tables, make_figs trên rows đã lưu, khớp .tex |
| [10.14] | Python adapter | C29, C30, C31, C33, C14 (phần Python) | `replay.py adapter` + `riders.py` + `unittest` |

**Chờ chủ đề tài quyết:**
- C08 + C28: nhánh cổng dự báo và đường không-Shadow 35% trong C#.
- C37: phần còn lại của chế độ plain trong fastsim.
- C20 + G1-01: restore. Khóa luận §3.4.3 mô tả việc này; nên để sau buổi bảo vệ.
- Giao thức đợt 2: C05, phần còn lại của C24, C23 + G1-02, C04 (`ServiceOrder`), G1-03.
- C11: chẩn đoán bộ giải, sát CP-SAT.
- C16 coi như đã đủ sau [10.01], không làm tiếp.

**Giữ (không gỡ):**

| Mục | Nội dung |
|---|---|
| C02 | 4 chiều điểm dừng và khóa `pickupStop` |
| C10 | bảo vệ no-op |
| C22 | cầu vi phạm do giao thông và `breaches` |
| C32 | kiểm chuyển khách G2 ở adapter |
| C34 | `add_*` trong init_modules |
| C35 | trường adapter chỉ kiểm hoặc băm |
| C38 | nhánh chết trong engine JS chép nguyên văn |
| G1-05, G2-02, G2-03, G2-07 | bảo vệ và oracle của test |

## YÊU CẦU 1 — 2026-11-27 01:35: so 4 lượt mốc chuẩn trên `[10.04]`

- **Code:** `00fd3be` [10.04]. Commit ghi chú này không đổi code.
- **Mốc chuẩn:** `ab697fe`.

**Chạy:**
- 4 lượt C# + FleetPy theo README mục 2.2: ô `d20190919-vf-r0-w08` và `d20190919-vf-r0-w17`, mỗi ô với `FS-G2-SHHZ71-N`
  và `FS-G2-SHRC71-CL`.
- Sau đó chạy `riders.py --out … --sequences …` cho cả mốc chuẩn lẫn bản mới.

**Trả về** trong `## TRẢ LỜI 1`, mỗi lượt một dòng:

1. **Bảng từng khách:** sha256 của `--out` (mốc chuẩn / mới) và của `--sequences` (mốc chuẩn / mới). Nếu khác, ghi
   dòng khác đầu tiên, chỉ requestId và tên cột.
2. **Số lần phát:** số hành động `promisePublished` đã xác nhận ở mốc chuẩn (`P0`) và ở bản mới (`P1`). Tính thêm `K`
   từ transcript mốc chuẩn (định nghĩa ngay dưới). Kỳ vọng `P1 = P0 − K`. Nếu không bằng, liệt kê tối đa 5 lần phát
   lệch (requestId, promiseVersion, kind).
   - `K` là số lần sửa (`kind` = revision) thỏa: trong cả ba vectơ `exogenousDelta`, `decisionDelta`, `visibleDelta`,
     năm trường `pickupEtaTotalMs`, `dropEtaTotalMs`, `vehicleSwitchCount`, `pickupStopSwitchCount`,
     `dropStopSwitchCount` đều bằng 0.
3. **Tóm tắt `summarize` của `run_job.py`:** liệt kê các trường khác nhau giữa mốc chuẩn và bản mới.
   - Kỳ vọng chỉ khác ở số lần phát, digest và hash.
   - Các trường sau phải bằng nhau: `requests`, `lifecycle`, `E1ServedShare`, `promisedRiders`,
     `E2ShareDropVAbove60s`, `dropVCvar95Ms`, `sumDropC`, `sumDropX`, `sumDropV`, `sumPickupC`, `sumPickupX`,
     `sumPickupV`, `decisionsAcknowledged`, `breaches`, `latePickups`, `latePickupMaxMs`.
4. **Tài nguyên:** thời gian chạy và RAM đỉnh mỗi lượt, để ước lượng cho các đợt sau.
5. **Test Python của adapter trên `00fd3be`:** số test qua và số hỏng ([10.03] và [10.04] có sửa `protocol.py`).

Không cần `kiem_nhanh.sh` cho yêu cầu này vì chưa sửa JS.

## 2026-11-27 01:35 — Nhận xét [10.01]–[10.05]

Đọc trên `9f2e874`; code được xét là `00fd3be` [10.04].

| Commit | Nhận xét |
|---|---|
| [10.01] | Đồng ý. `FixedRootValues` vẫn bắt 4 khóa cố định phải có đúng giá trị, nên cấu hình cuối vẫn được nhận. Một khóa đã gỡ nay bị từ chối như khóa lạ. |
| [10.02] | Đồng ý: không cấu hình nào đặt `dropStop` hay `dropEta`. |
| [10.03] | Đồng ý, nhưng lời commit chưa nói đủ một hệ quả: commit này đổi **khi nào** một lần sửa được phát (xem dưới). Cần phép so ở YÊU CẦU 1, có số dự đoán. |
| [10.04] | Đồng ý. |

### [10.03]: lập luận "không đổi quyết định"

**Hành vi đổi ở đâu.**
- Lần sửa được phát khi `unchanged` sai (`src/core/RideBound.Application/Commitments/CommitmentDecisionValidator.cs:462`),
  tức có ít nhất một vectơ khác 0.
- Trước [10.03], vectơ còn `incumbent_order_inversion_count` và `pre_pickup_inserted_stop_count`. Vì vậy một lần sửa
  vẫn được phát khi giờ đón, giờ trả, xe và điểm dừng giữ nguyên, nếu:
  - thứ tự điểm dừng của khách khác đổi, hoặc
  - có điểm dừng mới chèn trước điểm đón mà giờ đón không đổi.
- Sau [10.03], lần sửa đó **không được phát nữa**. `material_eta_revision_count` chỉ khác 0 khi giờ đón hoặc giờ trả
  đổi, nên không thêm trường hợp nào.
- Lời commit mới nêu trường hợp "thứ tự", chưa nêu trường hợp "chèn trước điểm đón".

**Vì sao quyết định không đổi.** Một lần sửa loại này chỉ khác lời hứa trước ở các trường: thứ tự phục vụ, version,
thời điểm phát, mã lần phát. Tôi đã kiểm mọi chỗ đọc lời hứa hiện hành, không chỗ nào dùng các trường đó để ra quyết
định:

| Chỗ đọc | Đọc trường nào |
|---|---|
| `src/core/RideBound.Application/Promises/PromiseDeltaCalculator.cs:37-42` | giờ đón, giờ trả, xe, hai điểm dừng |
| `src/core/RideBound.Domain/Commitments/CommitmentLockEvaluator.cs:113-129` | xe, điểm đón và nút đón, giờ đón |
| `src/core/RideBound.Application/Promises/PromiseProjector.cs:85-87` (khách đã lên xe) | điểm, nút và giờ đón |
| Hạn mức `BudgetAfter` | một lần sửa loại này cộng 0 vào mọi chiều còn lại |
| `Version` (`CommitmentDecisionValidator.cs:468-469`) | chỉ để đánh số lần phát kế tiếp |
| `PublishedAt` / `PublishedEpoch` (`src/core/RideBound.Domain/Incidents/OperationalIncidentLedger.cs:97-100`) | chỉ trong phép kiểm bất biến của bản ghi vi phạm; lời hứa cũ hơn vẫn qua phép kiểm |
| Adapter `src/adapter/ridebound_fleetpy/fleet_control.py:830` | ghi vào `_rb_publications` và suy ra việc chuyển khách |
| Adapter `src/adapter/ridebound_fleetpy/relocation.py:64` | chỉ xét khách đổi xe; lần sửa của khách đó luôn được phát vì `vehicle_switch_count` = 1 |

**Hệ quả được phép (không phải kết quả khóa luận):**
- Sổ và transcript có ít hành động `promisePublished` hơn.
- Version và mã lần phát của các lần sau lệch số.
- `publicationCount`, `publicationDigest` và hash ngữ nghĩa của `run_job.py` đổi.

**Dự đoán cho `riders.py`:**
- Một lần sửa loại này có p = e = v, nên c = 0. Các cột `sumAbsC`, `nonzeroC`, `netC`, `maxAbsC` không đổi.
- `drop_sequences` đã bỏ giá trị lặp liên tiếp, nên bảng chuỗi giờ trả cũng không đổi.
- Kết luận: cả hai bảng phải giống từng byte. YÊU CẦU 1 kiểm dự đoán này.

### [10.04]

- Ở `ab697fe`, không nơi nào trong `src/adapter`, `src/fastsim` gửi `incidentOpened` hay `incidentResolved`. Runner
  không có chỗ nào tạo `commitmentBreachDeclared`: kiểu này chỉ có trong `RideBound.Contracts`. Vì vậy danh sách sự cố
  luôn rỗng, và nhánh mở/đóng sự cố trong `EventReducer` không bao giờ chạy.
- Trên đường quyết định, `Incidents` chỉ được so tham chiếu ở ranh giới quyết định
  (`CommitmentDecisionValidator.cs:546-548`). Cầu vi phạm do giao thông vẫn ghi `breaches` như trước.
- Không mã Python nào đọc khóa `"incidents"`. `src/adapter/run_job.py:776-805` chỉ đọc `breaches` và `latePickups`,
  cả hai vẫn còn.
- `protocol.py`: `commitmentBreachDeclared` trước bị từ chối với mã UNSUPPORTED, nay với mã UNKNOWN. Với lượt chạy
  thì như nhau.

Kết luận: không đổi quyết định, không đổi bảng từng khách.

### Ghi chú quy trình

- Các commit [10.01]–[10.05] có dòng `Co-Authored-By` ở cuối lời commit. Chủ đề tài không dùng dòng này, nên từ
  [10.06] cả hai máy không thêm nữa. Lịch sử cũ giữ nguyên vì không force-push.
- Máy công ty không chạy được `dotnet test` và Python. Mọi phép chạy do máy nhà làm.
- Máy công ty đang phân tích sâu (chỉ đọc) trên `ab697fe` để tìm thêm phần thừa ngoài [10.01]–[10.04]. Nhóm gỡ tiếp
  theo sẽ được đề xuất ở đây sau khi có kết quả. Không gỡ thêm cho tới khi có TRẢ LỜI 1.
