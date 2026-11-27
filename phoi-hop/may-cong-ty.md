# Máy công ty (chính)

(Máy công ty ghi vào đây: phân tích, quyết định, và các `## YÊU CẦU <số>` gửi máy nhà.)

## KẾT THÚC — 2026-11-27 18:20: nhận TRẢ LỜI 8; thu gọn xong, không còn yêu cầu

**Nhận TRẢ LỜI 8 ([10.33n] `2dcc8fe`):** đạt.
- unittest 40/0, discover 117/0.
- `riders.py` trên toàn đợt 360 lượt giống từng byte bản `ab697fe` (`--out` `452378ec…`, `--sequences` `79660500…`).
- `compare_riders.js` 125.871/125.964 khách, 353/360 lượt; 4 job B1 giống bảng mốc.

Giữ [10.31]. Việc thu gọn trước buổi bảo vệ dừng ở đây; máy nhà không cần chạy gì thêm. Commit [10.33n] vẫn còn dòng
Co-Authored-By; không viết lại lịch sử (không force-push), chỉ lưu ý cho lần sau.

## YÊU CẦU 8 — 2026-11-27 16:10: kiểm C31 [10.31] (gói cuối); nhận TRẢ LỜI 6, 7

**Nhận TRẢ LỜI 6 và 7:** đạt.
- 790 test qua.
- Phát lại Runner trên 4 transcript B1 giống từng byte, cả ở `6a887ae` lẫn `9cfa332`.
- `kiem_nhanh` báo 36 equal; dự đoán trên ô mẫu giống bản `ab697fe`.

Giữ [10.24], [10.25], [10.27]. `README.md:132` đã sửa thành 790 ([10.30] `a003e25`).

**[10.31] `2abef42`: C31, bỏ đường đọc transcript của `riders.py`.** Từ nay `riders.py` chỉ đọc `evidence.json.gz` đã
niêm phong. Lý do:
- Mọi lượt của đợt chạy đều có evidence: `run_job` ghi nó cả khi giữ transcript.
- Bảng kiểm toán 12 cột trong evidence chính là bảng mà `run_job.audit` dựng lại từ transcript.
- TRẢ LỜI 4 đã cho thấy đọc qua evidence ra bảng giống từng byte.

Đã gỡ:
- `read_transcript`, `audit_table`, `check_chains`;
- các phép từ chối chỉ phục vụ đường transcript (hash khung, `frameLengthBytes`, transcript không nén);
- công tắc cũ `RB_PREFER_EVIDENCE`.

Phần đọc evidence, `rider_rows` và `drop_sequences` giữ nguyên từng dòng. `test_riders.py` nay dựng job thẳng bằng
evidence. Hàng và chuỗi kỳ vọng không đổi; máy công ty đã kiểm bằng một bản port Node. Test chỉ đọc transcript thì gỡ.
`riders.py` giảm từ 315 xuống 185 dòng.

Kiểm ở **đầu nhánh**:
1. **unittest:**
   - 5 module của README §2.3: kỳ vọng **40 qua** (42 − 2: `test_riders` còn 3 test);
   - `discover` có `RIDEBOUND_FLEETPY_ROOT`: kỳ vọng **117**.
2. **`riders.py --out --sequences` trên toàn bộ các root của đợt 360 lượt** (cùng lệnh và thứ tự `--root` như lần tái lập
   §5.2): hai tệp ra giống từng byte bản do `riders.py` ở `ab697fe` ghi. Ở `ab697fe`, 8 lượt ô cổng còn transcript nên được
   đọc qua transcript; ở đầu nhánh chúng được đọc qua evidence. Đây là phép so chính của C31.
   `compare_riders.js` phải ra 125.871/125.964 và 353/360 như cũ.
3. **4 job B1 (mỗi job có cả transcript lẫn evidence):** `riders.py` ở đầu nhánh cho `--out` và `--sequences` giống bảng
   mốc B1.

Có khác thì gửi job và dòng khác đầu tiên của cả hai bản. Máy công ty sẽ hoàn tác C31.

**Nhắc lại:** commit [10.29n] `dbc4bfc` vẫn còn dòng `Co-Authored-By`. Từ commit sau, máy nhà không thêm dòng này.

**Kế hoạch:** C31 đạt thì phần thu gọn trước bảo vệ coi như xong. Không còn gói nào được lên lịch. Các mục còn lại đã được
xếp là không làm hoặc để sau bảo vệ (xem YÊU CẦU 7).

## YÊU CẦU 7 — 2026-11-27 13:55: thêm [10.27] (gộp với YÊU CẦU 6, chạy một lần ở đầu nhánh)

YÊU CẦU 6 chưa có trả lời. Máy nhà chạy YÊU CẦU 6 và YÊU CẦU 7 **một lần**, ở đầu nhánh (commit chứa yêu cầu này), thay
cho việc chạy YÊU CẦU 6 ở `6a887ae`. Nếu đã chạy xong một phần YÊU CẦU 6 ở `6a887ae` thì vẫn gửi kết quả đó, rồi chạy lại
mục 1 và 2 dưới đây ở đầu nhánh.

**[10.27] `a93d8e0`: gỡ 17 chỗ C# thừa tìm được khi rà lại sau C37/C11.** 46 tệp trong `src/core`, +156/−678 dòng.
Mỗi chỗ đã được hai người phản biện độc lập xác nhận, rồi qua QA hai góc và một lượt soát. Các chỗ đã gỡ:
- tài liệu canonical của ràng buộc cấu hình và bộ đọc lại nó (giữ `Calculate`, test vẫn ghim `613e242c…c1c0`);
- hai mã lý do không ai dùng;
- `ReceiverVersion`/`SenderVersion` của kết quả tương thích;
- tập chính sách mà quyết định của bộ giải mang theo (luôn bằng `context.Policies`);
- chẩn đoán lần giải chính và lần dự phòng;
- các trường phụ của witness đánh giá, rolling-cost, sinh ứng viên và rút gọn sự kiện (giữ `Code`, `Message`);
- tham số constructor tùy chọn không nơi nào truyền;
- `Omitted` của bộ giữ 100 ứng viên;
- ba nhánh không tới được của `InitializeRunValidator` và mã lỗi enum của chúng;
- envelope của phản hồi streamed;
- thành viên chết của `CanonicalBuffer`;
- kiểm ngữ cảnh lặp lại trong `EventBatchOrderingValidator`;
- `StateAfterHash` chỉ đi một vòng;
- try/catch không bao giờ bắt được ở `RunnerHost`;
- hai danh sách bound luôn trùng nhau của OR-Tools (proto không đổi).

Mọi `Message`, mã giao thức và exit code của đường tới được giữ nguyên. Khung Runner, mô hình CP-SAT và mọi hash phải
không đổi. Không đụng Python, JS hay chữ khóa luận. Đã gỡ đúng 3 `[Fact]`, không đổi `InlineData`.

Kiểm ở **đầu nhánh**:
1. **`dotnet test src/core/RideBound.slnx -c Release`:** kỳ vọng **790** qua, 0 hỏng, 0 bỏ qua. Theo dự án: Architecture
   9, Contracts 121, Domain 218, Application 99, Solvers 14, Algorithms 171, Runner 158 (so với 794 ở `8a0d305`: −1
   Application do [10.25], −1 Contracts và −2 Runner do [10.27]). Cần thấy qua:
   - `OrToolsModelIdentityTests` (proto CP-SAT giống từng byte);
   - `CandidateSearchWorkProfileTests`;
   - `Wp4PolicyConfigurationBindingTests`;
   - `FinalConfigGoldenTests`;
   - `CommitmentCheckpointProcessTests`;
   - `FinalCliTests` (exit 64).
2. **`replay.py runner` trên 4 transcript mốc B1** với Runner build ở đầu nhánh: mọi khung giống từng byte. Ghi
   `runnerDllSha256`, thời gian và RSS đỉnh như TRẢ LỜI 3. Đây là phép kiểm chính cho cả [10.25] lẫn [10.27].
3. **Mục 3–5 của YÊU CẦU 6** (`kiem_nhanh.sh` 36 equal; `emulate_fullsys.js adapter` trên 2 ô mẫu so `ab697fe`; tùy
   chọn F16/cổng/chuỗi): [10.27] không đụng JS, nên chạy một lần ở đầu nhánh là đủ.

Có khác thì gửi khung khác đầu tiên (số thứ tự, loại khung, khoảng 300 ký tự quanh chỗ khác) và tên test hỏng kèm thông
báo lỗi. Máy công ty sẽ hoàn tác riêng mục gây ra.

**Nhắc về commit:** các commit `[10.xxn]` của máy nhà (và [10.01]–[10.05], [10.07]) còn dòng `Co-Authored-By`. Từ nay
không thêm dòng này. Commit đã push thì để nguyên, không viết lại lịch sử.

**Kế hoạch:** đạt thì máy công ty sửa `README.md:132` (794 → số đo được), rồi làm gói cuối C31. Sau đó nhánh coi như
xong phần thu gọn trước bảo vệ. Các mục còn lại đã được phân loại là không làm hoặc để sau bảo vệ: C08, C20 + G1-01 lệch
chữ khóa luận; C05, witnesses, C04 phần còn lại, G1-03, C23 đổi wire; R02, R03, C24, G1-02, C14 lợi ích nhỏ.

## YÊU CẦU 6 — 2026-11-27 11:15: kiểm C37 [10.24] và C11 [10.25]; nhận TRẢ LỜI 3, 4, 5

**Nhận TRẢ LỜI 3, 4, 5:** đạt cả ba.
- **TRẢ LỜI 5:** K = 1 có thật. Bảng từng khách vẫn giống từng byte ở ba nguồn, quyết định giống nhau, `summary` bằng
  nhau. Giữ [10.03].
- **8 transcript ô cổng (TRẢ LỜI 3 mục 3):** không cần chạy. Định dạng đổi có chủ ý, và B1 đã phủ phần này.
- **`make_figs.py`:** không cần vẽ lại. Máy công ty đã đọc diff [10.15]: chỉ gỡ hình `du-bao-theo-khung-gio` (khóa luận
  không dùng) và hàm `vn` (không còn ai gọi). Hai hình khóa luận dùng, `phuc-vu-va-thay-doi` và `do-nhay-han-muc`, có mã
  không đổi. `calib_pad.py` không commit nào đụng.

Chủ đề tài cho làm C37 và nhắm phần C#. Hai commit mới:

| Commit | Gỡ gì | Vì sao kết quả không đổi |
|---|---|---|
| `4f33ef4` [10.24] C37 | Chế độ `plain` của `emulate_fullsys*.js` (so với dòng `RB_EXPECTED_ROWS` không đi kèm repo); `wf20/runner/{engine17,scenario,lib,identity,normalizer_emul}.js`; `RUN_OVERRIDES` của `shlib.js`. 14 tệp, −1529 dòng | Ba bộ chạy adapter60 và `emulate_fullsys*` nay lấy `parseArm`/`armOf` từ engine wf15 (`engine_wf15_clint.js`, `_pp`), thay cho `engine17.js`. Mã nguồn hai hàm giống từng chữ. Trên 2520 mã nhánh, cả hàm thường lẫn hàm ném lỗi: 0 khác. Trên 2 ô mẫu: 50/50 ô-nhánh cho cùng `sig`. Khóa luận không nhắc chế độ plain |
| `e013b25` [10.25] C11 | Chẩn đoán của bộ sinh ứng viên và bộ giải mà không ai đọc: witness/digest bỏ sót, kế toán bỏ sót trước khi giải, vòng xả frontier, bộ đếm đường/đổi xe, `WasTruncated`, best bound, gap, thời gian tường, `DetailCode`. 9 tệp mã + 22 tệp test, −786 dòng | Ngân sách, thứ tự duyệt, mô hình CP-SAT (`fixedBounds` chỉ dùng `LevelIndex`, `IncumbentValue`), nhánh khả thi chưa chứng minh tối ưu và dự phòng giữ nguyên. Chuỗi lỗi vượt ngân sách giữ nguyên từng byte. Runner chỉ đọc `SolveResult.Status` và `ExogenousServiceQualityBreaches`. `ValidationWitnesses` (lên khung lỗi) và `ExecutionPath` giữ nguyên |

Máy công ty đã build Runner và biên dịch 7 dự án test (`UseAppHost=false`): 0 lỗi, 0 cảnh báo, không có `.exe`. Hai góc
QA đều đạt. Chỉ gỡ 1 test: `CandidateSelectionModelTests.Objective_bound_preserves_exact_gap_and_validates_direction`.

Kiểm ở **đầu nhánh** (commit chứa yêu cầu này):
1. **`dotnet test src/core/RideBound.slnx -c Release`:** mọi test qua. Ghi tổng số test và số theo từng dự án; kỳ vọng
   793 = 794 − 1 (Application 99). Cần thấy rõ hai bộ:
   - `OrToolsModelIdentityTests` (proto CP-SAT giống từng byte);
   - `CandidateSearchWorkProfileTests` (work unit và số lần build không đổi).
2. **`replay.py runner` trên 4 transcript B1** với Runner build ở đầu nhánh: mọi khung Runner giống từng byte. Ghi
   `runnerDllSha256`. Phát lại giống thì cả lượt giống, nên không cần chạy lại lượt C# đầy đủ.
3. **`kiem_nhanh.sh`:** `36 rows equal, 0 differ`. Lệnh này chạy cả `emulate_adapter.js`, `_pp` và `_eta`, tức ba bộ chạy
   đã đổi nguồn `parseArm`.
4. **`emulate_fullsys.js adapter` trên 2 ô mẫu** (`FS_ONLY`, như TRẢ LỜI 3 mục 10): `fullsys-f14-predictions.tsv`,
   `-counts.tsv` (bỏ cột ms nếu có) và `shadow-inputs/*.json` giống bản `ab697fe`. Thêm: `node emulate_fullsys.js` (không
   đối số) và `node emulate_fullsys.js plain` đều in dòng usage và thoát mã 2.
5. **Tùy chọn, nếu có panel đầy đủ:** `emulate_fullsys_f16.js adapter`, `emulate_forecast_gate.js`, `emulate_sequences*.js`
   giống bản `ab697fe` từng byte. Không có panel thì bỏ qua.

Python chỉ đổi một dòng docstring (`direct_network.py:7`), không cần chạy lại.

Có khác thì gửi tệp hoặc khung khác đầu tiên: số thứ tự, loại khung, khoảng 300 ký tự quanh chỗ khác.

**Kế hoạch:** đạt thì máy công ty sửa `README.md:132` theo số test đo được, rồi làm gói cuối C31. Phần rà C# thêm (chỉ
báo cáo) đang chạy ở máy công ty. Các mục đụng chữ khóa luận chờ chủ đề tài quyết: C08 + C28, C20 + G1-01, giao thức đợt 2.

## YÊU CẦU 5 — 2026-11-27 08:40: chạy thật một ô có N = 1; nhận TRẢ LỜI 1, 2

**Nhận TRẢ LỜI 1 và 2:** đạt. 4 lượt mốc giống từng byte, `summarize` bằng nhau. Không có restore, sự cố, hủy hay
waypoint trên 8 transcript ô cổng; `incidents` rỗng và `nonNormalCertificates` = 0 ở cả 364 evidence. Phần đã xong của
YÊU CẦU 3 + 4 cũng đạt; `README.md:132` đã sửa thành 794 test ([10.22] `3ec116c`).

**N = 11: quyết chạy kiểm thật.** Đây là chỗ duy nhất [10.03] có thể đổi hành vi trên 360 lượt: một lần sửa chỉ khác ở
các chiều đã gỡ thì nay không được phát. Theo cấu trúc thì không bảng nào đổi:
- Ở lần sửa đó c = p − e = 0, nên `sumAbsC`, `nonzeroC`, `netC`, `maxAbsC` của `riders.py` không đổi.
- `--sequences` bỏ giá trị lặp liền nhau. Trường hợp biên duy nhất: hai lần sửa của cùng khách phát cùng lúc tại đúng
  giờ trả.
- Quyết định không đọc các chiều đã gỡ (nhận xét [10.06]).

Nhưng 4 lượt mốc đều có K = 0, nên lập luận này chưa được chạy thử.

Chạy ô F14 `FS-G2-SHHZ71-N` `d20191216` r1 w08 ở `ab697fe` và ở đầu nhánh, giữ transcript như B1. So:
1. **Bảng từng khách:** `riders.py --out --sequences` của lượt `ab697fe`, lượt đầu nhánh và evidence đã lưu của đợt 360
   lượt cho ô này. Ba bảng (phần của ô) giống từng byte.
2. **Số lần phát:** `P` trên lượt `ab697fe`, `K` trên transcript `ab697fe` (định nghĩa ở YÊU CẦU 1), `P` trên lượt đầu nhánh.
   Kỳ vọng `P_đầu nhánh = P_ab697fe − K` với K ∈ {0, 1}. Nếu K = 1, ghi requestId, `simTimeMs` và chiều nào trong 5 chiều
   đã gỡ khác 0 ở lần sửa đó.
3. **Quyết định:** số khung `decision` bằng nhau. Theo thứ tự, các hành động `vehiclePlanUpdated` của từng khung giống
   nhau sau khi bỏ mọi khóa đuôi `Hash`/`Sha256`.
4. **`summary`:** `requests`, `promisedRiders`, `sumDropC`, `decisionsAcknowledged`, `breaches`, `latePickups`,
   `latePickupMaxMs` bằng nhau.

Có khác thì gửi requestId và khung khác đầu tiên (số thứ tự, loại, khoảng 300 ký tự quanh chỗ khác). Máy công ty sẽ quyết
giữ hay khôi phục phần đó của [10.03].

**Phần còn lại của YÊU CẦU 3 + 4:** làm tiếp như đang làm (replay Runner B1, `replay.py adapter`, `riders.py` hai đường
đọc, so evidence). `make_figs.py`: thử với `MPLBACKEND=Agg` (mã 127 thường do backend có giao diện). Vẫn lỗi thì ghi lại
và bỏ qua: [10.15] chỉ gỡ một khối hình và hàm `vn`.

**`make_run_drivers.py` báo `IndexError` khi thiếu đối số:** bản cũ cũng vậy, không do [10.17]. Không sửa, để giữ phạm vi.

**Kế hoạch:** YÊU CẦU 3, 4, 5 đạt thì làm gói cuối C31 (bỏ đường đọc transcript của `riders.py`). Vẫn chờ chủ đề tài
quyết: C08 + C28, C37, giao thức đợt 2 (C14, C21, G1-02), C20 + G1-01, C11.

## YÊU CẦU 4 — 2026-11-27 07:32: kiểm các gói Python [10.15]–[10.18]

Commit trên nhánh (sau `ca96d32`):

| Commit | Gỡ gì | Mục |
|---|---|---|
| `dbd5e97` [10.15] | Phân tích fc30 cũ: khối B của `aggregate.py` (khóa `B`, `B_rows`), khối "% Bảng 5.7" cũ của `gen_tables.py`, hình `du-bao-theo-khung-gio` của `make_figs.py`. Bảng 5.7–5.10 của khóa luận lấy từ `table57.js`, `eta_analysis.py`, `gen_eta_tables.py`, không đổi. Lượt fc30 vẫn chạy (`calib_pad.py`, `kiem_nhanh.sh` cần) | C39 |
| `eb626ba` [10.16] | Bộ kiểm WP7 phía adapter: `actual_fleetpy_medium_verify.py`, `assets/wp7-fleetpy-medium/`, `test_medium_verifier.py`, biên nhận artifact của session, `semantic`/`semanticHash` trong `run-00.json` và `summary.json`, `checkpointBindingHash`. Giữ `capability-matrix.json` | C29 |
| `4df8ad7` [10.17] | Trường evidence không ai đọc: `summary.lifecycle`, `E1ServedShare`, `E2ShareDropVAbove60s`, `dropVCvar95Ms`, `sumDropX/V`, `sumPickupC/X/V`, `nonNormalCertificates`, `moves.firstMoves`, khóa `transcript` (biên nhận). Bia mộ adapter: tiền tố `op_`, cận 64 MiB bị ghi đè (nay bắt buộc setting, lượt thật luôn 2^30), nhánh `lateBoarding` reject, driver khác `complete-graph-direct-v1`/`gzip-v1` và chế độ `check` của `make_run_drivers.py` | C30, C33 |
| `1e9da42` [10.18] | `EXPECTED_EVIDENCE_SHA256` (`test_run_job.py:41`) đổi `019341f2…` → `9d718888…` | — |

[10.15]–[10.18] chỉ đụng `src/adapter` và `src/fastsim/adapter60/*.py`: không đổi C#, JS, giao thức hay quyết định. Phần
evidence mà `riders.py` đọc (`audit.table`, `audit.summary.sumDropC`, `finalOnlineState`, `events.alighted`) giữ nguyên,
`format` vẫn `ridebound-job-evidence-v1`, nên 360 evidence đã lưu vẫn đọc được.

**Gộp với YÊU CẦU 3.** Vì các gói này không đụng C# hay JS, có thể chạy YÊU CẦU 3 và YÊU CẦU 4 cùng lúc ở **đầu nhánh**
(commit chứa yêu cầu này). Dòng "Python: … không cần cập nhật hash ghim nào" của YÊU CẦU 3 chỉ đúng tới `ca96d32`.
YÊU CẦU 1 vẫn chạy ở `00fd3be`: mục 3 của nó so các trường `summarize` mà [10.17] đã gỡ.

**Giá trị ghim mới** do máy công ty tính bằng bản viết lại bằng JS của `job_frames`, `write_evidence` và
`normalized_sha256`. Bản này ra đúng giá trị cũ `019341f2…` trên `ca96d32`, và ra
`9d718888e49bb9683c122674e7d8f7f5742d71fe109d5c1a6aa4d9eb6674a8e7` trên `4df8ad7`. Chưa chạy bằng Python.

**Python**
1. 5 module của README §2.3: kỳ vọng 42 test, 0 hỏng (số không đổi). Nếu chỉ hỏng
   `EvidenceTests.test_evidence_of_a_small_job`, ghi hash thực tế; khác `9d718888…` thì báo, máy công ty sửa.
2. `discover` có `RIDEBOUND_FLEETPY_ROOT`: kỳ vọng 0 hỏng, 119 test (134 ở `ab697fe`, đếm tĩnh `def test_`). Mất 15 test:
   11 của `test_medium_verifier.py`, `test_runner_client` 2 (biên nhận artifact, bundle manifest của verifier),
   `test_session_line_bounds` 3 (cận 64 MiB) thay bằng 1 (`test_the_setting_is_required_and_within_the_runner_range`).
   Ghi số thật.
3. `python -B src/adapter/replay.py adapter --transcript <transcript B1> --out <tệp> -- src/adapter/run_job.py <đối số như
   lượt B1, thư mục ra mới>` cho 4 transcript B1: kỳ vọng `identical` đúng, `initializeRun` chỉ khác `coreCommitSha` và
   `binarySha256`, job kết thúc bình thường.
4. Với mỗi thư mục job vừa sinh ở bước 3:
   - `riders.py --root <root chứa job> --out … --sequences …` (đọc transcript): giống từng byte bảng của B1 và `ab697fe`.
   - Chép job sang root khác **không kèm** `transcript-00.ndjson.gz`, chạy lại `riders.py` (lần này đọc
     `evidence.json.gz` mới): cũng giống từng byte.
   - So `evidence.json.gz` mới với evidence của YÊU CẦU 1: `audit.table`, `finalOnlineState`, `events`, `moves` (bỏ
     `firstMoves`), `shadowAllocations` và các trường `summary` còn lại (`requests`, `promisedRiders`, `sumDropC`,
     `decisionsAcknowledged`, `breaches`, `ridersWithForcedBreach`, `latePickups`, `latePickupMaxMs`) bằng nhau; chỉ
     thiếu các trường đã gỡ.
5. `make_run_drivers.py write <sealed> <out> complete-graph-direct-v1` cho 2 ô B1 (w08, w17), vào thư mục mới: sha256
   bằng tệp trong `drivers-run/` đang dùng. Chế độ `check` đã gỡ.
6. Bảng mô phỏng nhanh trên rows đã lưu (`RB_RESULTS`), mỗi script chạy cả bản `ca96d32` lẫn đầu nhánh:
   - `aggregate.py`: `aggregate.json` mới bằng bản cũ bỏ khóa `B`, `B_rows`; stdout mới bằng bản cũ bỏ các dòng
     "dự báo N/CL/CLc/PAD", "CL-N", "CLc-N", "CLc-CL", "CL-PAD", "PAD-N".
   - `gen_tables.py`: stdout mới bằng bản cũ bỏ khối từ "% Bảng 5.7" tới ngay trước "% Ngân sách (10 ngày):". Bảng 5.4,
     5.5, 5.6, 5.11, Hình 5.3 và quy mô đội xe khớp .tex 22/11 từng ô.
   - `make_figs.py` với `RB_FIGURES` mới: chỉ ra `phuc-vu-va-thay-doi` và `do-nhay-han-muc` (`.pdf`, `.png`); PNG giống
     bản cũ (PDF khác do `CreationDate`).
   - `PYTHONHASHSEED=0 eta_analysis.py`, `gen_eta_tables.py`, `calib_pad.py`: như trước; `git status` sạch.
7. Không bắt buộc: một lượt C# + FleetPy đầy đủ, w08 `FS-G2-SHHZ71-N`, ở đầu nhánh: 335/335 `IDENTICAL`, `sequences.tsv`
   giống từng byte mốc.
8. Nếu biết script sinh `publication-projection-*.jsonl.gz` (ngoài repo, cho Bảng 5.3): nó có đọc trường nào đã gỡ ở trên
   không. Số liệu khóa luận không đổi vì Bảng 5.3 dùng tệp đã lưu.

**Cần trả về:** số test qua, hỏng, bỏ qua của bước 1–2 (kèm hash thực tế nếu `EvidenceTests` hỏng); kết quả `replay.py
adapter` từng transcript; sha `riders.py`/`sequences` qua cả hai đường đọc; kết quả bước 4–6; thời gian chạy.

**Kế hoạch sau:** nhận TRẢ LỜI 3 thì sửa `README.md:132` (số test C#). Sau khi máy nhà xác nhận YÊU CẦU 1–4, gói cuối
là C31 (bỏ đường đọc transcript của `riders.py`). Còn chờ chủ đề tài quyết: C08 + C28, C37, giao thức đợt 2, C20.

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
