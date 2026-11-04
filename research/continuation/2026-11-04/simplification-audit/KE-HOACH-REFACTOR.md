# RideBound: kiểm toán mã và kế hoạch refactor tối giản

**Phạm vi.**
- Repo RideBound-Code, nhánh `research/fixes-perf-2026-11-04`, HEAD `ad470c1`. Kiểm toán chỉ đọc repo: không sửa, không build vào repo. Cách lập tài liệu này: xem `README.md` cùng thư mục.
- Cấu hình của 168 job cuối nằm ngoài repo, trong `E:/RideBoundData/final-v2-pack`. Commit export `0eb9a6ca` cũng không có trong repo.
- Mọi điều nói về cấu hình cuối đều suy ra từ ADR-089/090, các kit và các template trong repo. Chỗ nào không chắc, tôi ghi "chưa rõ".

**Quy ước.**
- **(a)** Không tới được: không đường chạy nào dùng, không kết quả đã ghi nào dùng.
- **(b)** Thí nghiệm cuối không dùng, nhưng cần để tái lập hoặc kiểm chứng một kết quả, một cổng (gate) hay một con số đã ghi. **(b)-LV** nghĩa là con số đó có trong luận văn.
- **(c)** Thí nghiệm cuối cần.
- Phân loại cuối là **GIỮ**, **ĐƠN GIẢN HÓA** hoặc **ỨNG VIÊN BỎ**. Phân loại này đã đối chiếu với phản biện từng vùng và với các phát hiện bổ sung.
- **ỨNG VIÊN BỎ** luôn theo đúng thứ tự: gắn tag lưu trữ, ghi rõ thứ sẽ mất, rồi mới bỏ khỏi HEAD. Không viết lại lịch sử git. Không xóa dữ liệu hay kết quả (`AGENTS.md:31-33`).
- Viết tắt đường dẫn:
  - `kits/` là `research/continuation/2026-10-26/kits/`
  - `CE/` là `research/continuation/2026-10-26/company-emulator/`
  - `docs/18` là `docs/18-status-and-decision-log.md`
  - `docs/19` là `docs/19-requirement-traceability.md`
- Tên tệp `.cs` là duy nhất trong repo nên chỉ ghi tên tệp.
- Số giữ cách viết của bằng chứng gốc: dấu phẩy ngăn hàng nghìn, dấu chấm cho phần thập phân.

---

## 1. Tóm tắt

**Phần thiết yếu (c).** Thí nghiệm cuối chỉ có một đường chạy. Adapter FleetPy khởi động Runner bằng:

`--mode commitment --policy-config C --wp4-config W --solver-seed-source manifest-master-seed --maximum-line-bytes N`

Bên trong đường chạy này là:
- chính sách C1 `ridebound-hard-vector`, xếp hạng cost-first;
- bộ sinh ứng viên chèn (INS), có thêm lớp G2;
- cổng cam kết: rào chặt, rào lỏng (rào mềm), luật C, Shadow;
- phục hồi kiểu no-worse hoặc forced-reference;
- CP-SAT giải theo từng mức;
- sổ lời hứa;
- forecast CL;
- hai chế độ audit `full-v1` và `light-v1`;
- checkpoint ghi kiểu stream; dòng NDJSON dài tới 1 GiB.

Con số luận văn chính đến từ emulator JavaScript ở chế độ plain, không đến từ C#.

**Phần phức tạp mà thí nghiệm cuối không cần.**
- Bảy chính sách WP4 (B1–B5, C1, C2) dùng chung một bộ sinh, nhưng thí nghiệm cuối chỉ chạy C1.
- Bộ công cụ WP6 (khoảng 21 nghìn dòng C#, tính cả test) và script WP9–WP14R (khoảng 36 nghìn dòng Python, tính cả test) không được luồng cuối gọi.
- Có mã chết: nguyên mẫu hash v2, `HashTiming`, tùy chọn cache không bật được, nhánh hạ cấp năng lực, dự án `RideBound.Infrastructure` rỗng.
- Có trùng lặp: ba bộ helper đọc JSON cấu hình, nhiều bản cài khung hash.

**Phần có thể bỏ (sau khi gắn tag).**
- Khoảng 3.000–3.500 dòng trong lõi C#: B2–B5, origin-hold, hash v2, mã chết trong Contracts và Domain.
- Khoảng 55–60 nghìn dòng công cụ lịch sử.
- Lõi (c) chỉ đơn giản hóa, không bỏ.

**Rủi ro chính.**
1. **Luận văn trích số do C# sinh.** Bản `7f93ee6` (95 trang) trích T3.8 (stability-first, `customerVisible`, nhánh M+), Stage 1b và WP14 `runner-v1`, đều do Runner C# sinh. Bản `34920ee` (55 trang, chỉ Via) gần như không trích. Chưa rõ bản nào được nộp. Vì vậy stability-first, `customerVisible`, hạn chót tính bằng ms cố định, trần `drop_eta_total_ms`, B1 rolling-cost và normalizer WP6 đều là **(b)-LV**.
2. **G1/G2 chỉ phủ một phần.** Cả 228 job của G1/G2 đều là cost-first, mạng Manhattan, `full-v1`, không có Shadow, không có forecast. Chúng không bắt được lỗi ở stability-first, B1/C2, byte của `light-v1`, Shadow/forecast, mạng đầy đủ (complete-graph-direct) hay gzip. Cần năm lưới mới (N1–N5) trước khi refactor.
3. **Nhiều thứ quan trọng nằm ngoài repo.** Cấu hình cuối, script gate và bộ dự đoán adapter-mode (`engine_wf15_clint.js`) đều ở ngoài. Parser từ chối khóa lạ, nên trước khi "bỏ khóa" nào phải grep thư mục trên ổ E: trước.
4. **Mọi thay đổi đều đổi dấu vết provenance.** `coreCommitSha`, `binarySha256` và hash kho mã sẽ đổi theo. Vì thế job mới chỉ so được theo kiểu bỏ qua commit (commit-free). G1 không bị ảnh hưởng.
5. **OR-Tools có thể bị nâng âm thầm.** Nếu nâng phiên bản OR-Tools, cấu hình không hỏng mà bị gắn nhãn sai, vì `AdapterVersion` là hằng số. Dependabot chưa có mục `ignore` cho gói này.
6. **Harness byte ADR-090 dùng chính các tính năng lịch sử** (C2, freeze horizon, retained-portfolio). Bỏ các tính năng này thì harness mất các cặp tương ứng. Harness đã được chép vào repo ngày 04/11 (`research/continuation/2026-11-04/byte-harness/`, bước 0.3).

**Khuyến nghị.**
- Không sửa gì cho tới khi chiến dịch 168 job xong.
- Sau đó: gắn tag, lưu harness, thêm các lưới an toàn, rồi đi lần lượt 8 giai đoạn ở mục 5.

---

## 2. Phạm vi thí nghiệm cuối

### 2.1 Hai chương trình, hai vai trò

| Chương trình | Vai trò |
|---|---|
| Emulator JS (`CE/sp/wf20/fullsys`, `wf21/n7`, `wf20/am2017`, `wf17`), chế độ plain | Sinh con số luận văn: bảng 2×2 luật × forecast, so sánh chính, quy kết, cỡ đội xe, độ bền |
| RideBound C#: FleetPy + adapter Python (`simulators/fleetpy-ridebound`) + Runner .NET | Kiểm chứng emulator theo từng khách: G1/G2, G3a/G3b, gate forecast, gate Shadow. Chạy chiến dịch Via 168 job trên máy thuê |

Hai lưu ý quan trọng:
- Bản luận văn `7f93ee6` vẫn trích số do Runner C# sinh (mục 2.6). Chưa rõ bản nộp là `7f93ee6` hay `34920ee`.
- Mọi phép so C# với emulator đều dựa trên dự đoán **adapter-mode**: `identity.mode ridebound-fleetpy-adapter`, forecast làm tròn nguyên. Khoảng cách plain/adapter của các nhánh cuối chưa đo (`docs/18:5952-5955`, "GĐ6").

### 2.2 Các nhánh (arm)

Ký hiệu:
- `d0`: giờ trả đã hứa ở lần hứa đầu.
- `e`: giờ trả dự chiếu nếu giữ tuyến cũ.
- `L_r`: sổ cộng dồn thay đổi do quyết định gây ra.
- `B_r = round(0.35 × slack)`, với `slack = max(0, đón_hứa + đi_tối_đa − trả_hứa)`, đo ở lần hứa đầu và cố định từ đó (ADR-081, `docs/18:5380-5384`; `CommitmentPolicy.cs:322-385`). C# làm tròn xa số 0. Emulator dùng `Math.round` (`engine17.js:43`).

| Nhánh | Luật cho giờ trả | Khóa C# (overlay `wp6-synthetic-policy-overlay-v1`) | WP4 `commitmentRecovery` | Emulator | Đã chạy C# |
|---|---|---|---|---|---|
| U | không trần | không có khóa `dropEta*` | `no-worse-than-reference-v1` | mã 0 (`engine17.js:36`) | có |
| EO35 | rào chặt: `d ≤ d0 + B_r` | `dropEtaDeadlineSlackPermille 350` | `forced-reference-v1`, bắt buộc (`docs/18:5719-5720`) | mã 8 (`:81`) | có (P1, G3a, G3b) |
| EOHZ35 | rào lỏng ("rào mềm"): `d ≤ max(d0 + B_r, e)` | thêm `dropEtaDeadlineRelaxed true` | no-worse | mã 9 (`:82`) | có |
| RCE35 | luật C: `L_r + abs(c) ≤ B_r` | `dropEtaBudgetSlackPermille 350` | no-worse; bắt buộc cost-first (`Wp4RunnerConfiguration.cs:506-517`) | mã 10 (`:83`) | có |
| SHF71/72 | rào chặt; B_r = 0 hoặc toàn bộ slack | suy luận: `…SlackPermille 1000` + `dropEtaShadowAllocation` | suy luận: forced-reference | mã 13 (`:86`) | **chưa** |
| SHHZ71/72 | rào lỏng + Shadow | suy luận: 1000 + relaxed + Shadow | suy luận: no-worse | mã 14 (`:87`) | có (gate Shadow) |
| SHRC71/72 | luật C + Shadow | suy luận: `dropEtaBudgetSlackPermille 1000` + Shadow | suy luận: no-worse | mã 15 (`:88`) | có (gate Shadow) |

Giải thích thêm:
- Với Shadow, permille phải đúng 1000, và lớp của khách chỉ cho hệ số 0 hoặc 1 (`CommitmentPolicy.cs:288-293,318-320`).
- "71" là ngưỡng x* của G2, "72" là ngưỡng x* của INS (`CE/sp/wf20/fullsys/PLAN.md:29`).
- Mọi nhánh dùng chung: `budgetBasis decisionInduced`, `materialRevisionRule {1000, 60000}`, `hardLimit 0` cho các chiều đổi điểm đón/trả, `stopDistances []`.

### 2.3 Các bộ điều chỉnh

| Bộ điều chỉnh | Giá trị | Bằng chứng |
|---|---|---|
| Điều phối | INS: không có `waitingRiderRelocation`. G2K8: `candidateGeneration.waitingRiderRelocation {maximumTargetVehicles 8}` cộng 4 thiết lập trong policy (mục 4) | `wp4-via-ins-a1.json` và `wp4-via-g2k8-a1.json` |
| Forecast | CL: `planningForecast {profile, binMs 900000}`. N: không có khóa, hash cấu hình không đổi | `Wp4RunnerConfiguration.cs:53-63,434-477`; `docs/18:6062-6067` |
| Audit | `auditHash` vắng hoặc `full-v1` cho job mẫu và G1. `light-v1` cho job lớn và G2 | `docs/18:5896-5902` |
| Bằng chứng solver | `emitSolverExecutionEvidence`: tắt ở job lớn, bật ở job mẫu và gate | `docs/18:5902` |
| Mạng đường | `routingEngine`: `network-basic-v1` (ô 60 nút, Manhattan) hoặc `complete-graph-direct-v1` (ô 824–830 nút) | `actual_fleetpy_medium_preflight.py:157-169,451-462` |
| Nén transcript | `none` hoặc `gzip-v1` | `:525-527` |
| Giới hạn dòng | Runner nhận từ 1 MiB tới 1 GiB. Adapter: các job đã ghi nâng giới hạn bằng cách vá launcher; cài đặt `ridebound_maximum_line_bytes` (ADR-090) chưa job nào dùng | `Program.cs:11-31`; `kits/via-real-2026-10-26/bridge_launcher_via.py:14-19,45-49`; `docs/19:9` |
| Ngân sách solver (Via A1) | 5e6 / 5e6 / 1e7 đơn vị công, 30 s thời gian tất định, seed 7 (bị seed của manifest ghi đè) | `docs/18:5603-5608` |

### 2.4 So sánh chính và chiến dịch

- **So sánh chính:** SHRC71+CL (hệ của mình) với SHHZ71+N (đối thủ). Nhánh tách: SHHZ71-CL và SHRC71-N. Tham chiếu: SHF ở chế độ N (`docs/18:6000-6005`).
- **Chiến dịch 168 job** = 84 ô F14 × {SHRC71-CL, SHHZ71-N}, chỉ G2. Các nhánh tách chạy trên 2 ô mẫu (`docs/18:6023-6026`).
- **Chưa xong:** SHHZ71-N w17 và 4 job nhánh tách của gate Shadow, dời sang máy thuê (`docs/18:6015-6022`).

### 2.5 Điểm vào (entry points)

| Điểm vào | Dùng cho | Bằng chứng |
|---|---|---|
| Runner CLI, dạng 8 tham số + `--maximum-line-bytes` | mọi job C# | `session.py:426-439`; `Program.cs:33-99` |
| Môi trường Runner | `DOTNET_GCHeapHardLimit` 6 GiB hoặc 2 GiB; `RIDEBOUND_STATE_HASH` và `RIDEBOUND_HASH_TIMING` không đặt | `docs/18:5374,5691` |
| Job | `python -B <launcher> <world wrapper> … --repeats 1 --master-seed 7 --expected-repository-inventory-sha256 <sha>` với `RB_WORLD_CONFIG=<ô>.world.json` | `kits/via-real-2026-10-26/run_via.py:178-196`; `world-via/preflight_world.py:49-50,201` |
| Cài đặt adapter (`ridebound_*`) | đường dẫn dotnet/Runner/cấu hình, seed 7, `service_class`, `commitment_policy_id`, timeout 60 (launcher nâng lên 900), giới hạn snapshot đầy đủ, transcript | `actual_fleetpy_medium_preflight.py:224-258` |
| Emulator | `bash sp/wf20/fullsys/RUNBOOK.sh gate\|gatecheck\|f14n\|f14cl\|f16n\|f16cl\|check\|analyze` → `node run_sh.js … --fc N\|CL --x star --arms SHF,SHHZ,SHRC,EOHZ35,RCE35` → `node fsan.js --spec production` | `CE/sp/wf20/fullsys/RUNBOOK.sh:10-26` |
| Ngoài repo (chiến dịch cuối) | `replay_gate_g1/g2.py`, `replay_runner_big.py`, `complete_oom_job.py`, `convert/emulate/compare_via_full.*`, `make_run_drivers.py`, `docker/{run_queue,make_jobs,assemble_context}.py`, `final/{finalize_job,evidence_reader}.py`, `run_cloud.ps1`, `cloud_gates.py` | `docs/18:5920-6056` |

### 2.6 Con số luận văn đến từ đâu

**A. Từ emulator, chế độ plain.**

| Kết quả | Mã sinh | Đầu ra |
|---|---|---|
| Toàn hệ (Shadow + luật C + CL), 30 ngày F14+F16, 3.600 lượt chạy | `CE/sp/wf20/fullsys/code/{runner/engine17.js, scenario.js, sscore.js, lambda.js, …; scripts/run_sh.js, shcore.js, forecast.js}` | `results/rows.ndjson` (SP, ngoài repo) → `fsan.js` → `tables.txt`, `REPORT-VI.md` |
| "Ours vs theirs" (G2 served −8.84, visRev60 −27.46, late60/1000 −34.04) | `CE/sp/wf20/ours-vs-theirs/ours_vs_theirs.js` | `OURS-VS-THEIRS.md` |
| Forecast so với độn độ dài (N7) | `CE/sp/wf21/n7/*` | `an7.js` → `REPORT-VI.md` |
| Độ bền R1–R7, cỡ đội xe | `CE/sp/wf17/r*`, `CE/sp/wf20/am2017` | `r*_tables.txt`, `am_tables.txt` |
| Bảng 60 ngày Ordinary/Shadow | dòng wf14/wf15 và engine wf15 (ngoài repo); tính lại trong repo: `kits/p5-via-adapter-2026-10-25/tables_2x3.py` | `TABLES-2x3-60days.txt` |
| Bảng 2×2 (sai số ETA 0.607/0.608/0.675/0.675 phút) | `eta_metrics.py`, `reversal_counts.py` (ngoài repo) | `docs/18:6034-6038` |
| Break-even w* = 0.94 | `kits/breakeven-2026-10-26/be_run.js` (gọi engine wf15 ở ngoài repo) | `BREAKEVEN-RESULT.md` |

**B. Từ Runner C#, trong bản `7f93ee6` (phát hiện bổ sung).**

| Mục trong luận văn | Mã sinh |
|---|---|
| Bảng 4.2–4.8, 4.10–4.12, phụ lục C | `runner-tier3-v2` (build từ `45f8026`) với `wp4-tier3-v2.json`: stability-first, phục hồi no-worse (T3.8, C-120, w07/w07x) |
| Bảng 4.9 | WP14 `runner-v1` (FREE = B1 rolling-cost) |
| Bảng 4.13, phụ lục D | phía stability-first: `runner-tier3-v2`; phía cost-first: Runner từ `dce84ec` (68 job) |
| Bảng 4.14–4.17 | phát lại bằng Python trên transcript do C# sinh |
| Tóm tắt (2,364 vs 2,338); `05-chuong1.tex:143,253-254`; `09-chuong5.tex:12-48` | C# |
| `08c` mục cầu nối (238 cặp Manhattan, 6/12 cặp Via zone) | C# |

Ở bản `34920ee`, C# chỉ còn sinh bảng cầu nối (`tab:bridge`) và bảng cấu hình.

### 2.7 Những gì chỉ để kiểm chứng (không sinh số luận văn)

| Cổng | Nội dung | Kết quả |
|---|---|---|
| G1 | phát lại từng byte 228 job Manhattan (ins 80, g2 33, p1 76, r128 39), `full-v1` | 228/228, 176,074 khung giống hệt (`docs/18:5920-5925`) |
| G2 | cùng 228 job, `light-v1`, bỏ các trường `*Hash` rồi so | 228/228 (`:5926-5927`) |
| Lưới Via zone | 12/12 cặp, 4,878 khách, G2 dời 6/6 | `compare_via.py` (`:5935-5942`) |
| G3a | ô 60 nút, network-basic so với complete-graph-direct | 36,952/36,952 dòng (`:5961-5967`) |
| G3b | ô đầy đủ ngày 13/2/2019, 8 nhánh × w08/w17 | 16/16 job, 5,925 khách (`:5943-5960`) |
| export-win6 | so với main16, bỏ commit và hash | 55,635/55,635 (`:5985-5992`) |
| Gate forecast | 2 job | 704/704 khách (`:6006-6014`) |
| Gate Shadow | SHRC71-CL w08/w17, SHHZ71-N w08 | 1,045/1,045, 4 phương diện (`:6015-6022`) |
| Test đơn vị và golden | scorer bit-exact, `TravelTimeForecastTests`, đồng nhất `NdjsonReader`, byte protobuf CP-SAT, light-audit | `.NET` 1,261/1,265; Python 467 test với 6 lỗi đã biết |
| Gate nội bộ emulator | `gcheck.js`, `indep.js`, `confirm_digest.js` | không sinh số |

---

## 3. Kiểm kê thành phần

Cột "Vai trò" ghi loại (a), (b) hoặc (c). Độ phức tạp và rủi ro chia ba mức: thấp, TB (trung bình), cao.

### 3.1 Runner (`src/RideBound.Runner`)

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| CLI `Program.cs` | (c) dạng 8 tham số | ĐƠN GIẢN HÓA (làm cuối) | `session.py:427-439`; ba nơi đặt mặc định khác nhau: `Program.cs:4`, `RunnerHost.cs:32-33`, `RunnerSession.cs:114-115` | thấp | TB |
| `RunnerHost`, `RunnerDefaults` | (c) | GIỮ | `RunnerHost.cs:11-21,60-66` | thấp | cao |
| `NdjsonReader/Writer`, `StreamedResponse` | (c) dòng tới 1 GiB | GIỮ | `NdjsonReader.cs:18-36`; checkpoint 406–563 MB | TB | cao |
| Bộ kiểm handshake/init/thứ tự batch | (c) | GIỮ | `RunnerSession.cs:286-288,333-341,526-544` | TB | TB |
| Nhánh downgrade trong `CapabilityNegotiator` | (a) | ỨNG VIÊN BỎ | `RunnerHost.cs:11-21` không đặt `Downgrade`; chỉ có test ở `CapabilityNegotiatorTests.cs:92` | thấp | thấp |
| Lõi `RunnerSession` | (c) | GIỮ | `RunnerSession.cs:207-266,446-735` | cao | cao |
| `--mode conformance` | (b) oracle Q1 cho test | GIỮ | `RunnerEndToEndTests.cs:100-101,183-184` | thấp | thấp |
| `--mode online` (mặc định khi không có tham số) | (b) golden WP2; là test CLI duy nhất cho `--maximum-line-bytes` | GIỮ (bỏ được sau khi chuyển test, bước 7.1) | `Wp2TinyDemoTests.cs:12-60`; `Wp4RunnerIntegrationTests.cs:18-58` | thấp | TB |
| commitment không có WP4 (4 tham số) | (b) golden WP3; test checkpoint stream S2; RB-WP6-006 | GIỮ (bỏ được sau khi chuyển test) | `CommitmentCheckpointProcessTests.cs:61-95`; `ExternalProcessSupervisorTests.cs:308-395` | thấp | TB |
| Đường commitment + WP4 + solver | (c) | GIỮ | `RunnerSession.cs:1148-1360` | cao | cao |
| Dạng 6 tham số (không có manifest seed) | (b) các run trước `d48b115` (chưa rõ); B5 chỉ khởi động được qua dạng này | ĐƠN GIẢN HÓA (sau khi bỏ B5) | `RunnerSession.cs:188-194` | thấp | TB |
| B5 multiple-plan + plan pool | (a) với mọi cấu hình trong repo | ỨNG VIÊN BỎ (sau khi grep E:) | `planPool` chỉ được ghi khi `Version != 0` (`OnlineStateCanonicalWriter.cs:118-121`) | cao | TB |
| Audit `full-v1` | (c) job mẫu, G1 | GIỮ | `docs/18:6052` | cao | cao |
| Audit `light-v1` | (c) 168 job | GIỮ, thêm vector cố định (N2) | `ProtocolHash.cs:110-131`; test hiện chỉ dùng `Assert.NotEqual` (`Wp4RunnerIntegrationTests.cs:943`) | TB | cao |
| `RIDEBOUND_STATE_HASH=v2` | (a) với mọi job đã ghi; harness ADR-090 dùng nó để test việc từ chối | ỨNG VIÊN BỎ (có điều kiện) | `docs/18:5374,5691`; `Light.cs:18-21` của harness | TB | thấp |
| `HashTiming` | (a) | ỨNG VIÊN BỎ | `HashTiming.cs:6-10` | thấp | thấp |
| `OnlineStateCanonicalWriter` + cache mảnh | (c) | GIỮ | A0.1 58/58 job; G1 | cao | cao |
| `CacheHistories`, bộ đếm mảnh, `InternalsVisibleTo("HashBench")` | (a) | ỨNG VIÊN BỎ | `OnlineStateCanonicalWriter.cs:25-30,86-90`; HashBench không có trong repo | thấp | thấp |
| `OnlineStateCanonicalizer` + `RIDEBOUND_CANONICAL_SELFCHECK` | (b) oracle độc lập cho writer | GIỮ | `OnlineStateCanonicalizer.cs:22-74` | TB | TB |
| Handler checkpoint | (c) | ĐƠN GIẢN HÓA (build một lần ở `light-v1`) | `RunnerSession.cs:761-774` | TB | cao |
| Restore + `OnlineStateCheckpointCodec` | (b) WP3/WP7; harness ADR-090 restore sau mỗi quyết định | GIỮ | `docs/18:6094,6098-6100` | cao | TB |
| Nối dây `planningForecast` | (c) nhánh CL | GIỮ, thêm test cấp phiên (N3) | `RunnerSession.cs:970-992` | thấp | cao |
| Nối dây Shadow | (c) nhánh SH* | GIỮ, thêm test cấp phiên (N3) | `RunnerSession.cs:1267-1280`; `git grep "\.Allocate(" tests` = 0 | TB | cao |
| `ExogenousServiceQualityBreachBridge` | (c) | GIỮ | `RunnerSession.cs:1244-1265` | TB | cao |
| `OnlineEventMapper`, `OnlineDecisionActionMapper` | (c) | GIỮ | | TB | cao |
| Bằng chứng solver (`SolverExecutionEvidenceMapper`) | (c, tùy chọn) | GIỮ | `docs/18:5902` | TB | TB |
| Profile retained-portfolio + `skipConstantObjectiveLevels` | (b) WP13 E1, WP14; harness ADR-090 | GIỮ (mục 5, bước 8.7) | `Wp4RunnerConfiguration.cs:20-31`; E1 chỉ tái lập được ở `38d517d` | TB | TB |
| `Wp4RunnerConfiguration` | (c) | ĐƠN GIẢN HÓA (chỉ gộp helper) | content hash = SHA-256 của bytes chuẩn hóa (`:294-297`) | cao | cao |
| `CommitmentPolicyConfiguration` | (c) | ĐƠN GIẢN HÓA (chỉ gộp helper) | `:75-78`; `NonNegativeInteger` không kiểm `ValueKind` (`:412`) | TB | cao |
| Nội dung `stopDistances` | cả 48/48 cấu hình trong repo là `[]` | GIỮ khóa; nội dung trong pack chưa rõ | khóa bắt buộc (`:16-19,82`) | thấp | TB |
| Kiểm binding cấu hình ↔ manifest | (c) | GIỮ | `RunnerSession.cs:352-386`; `Program.cs:111-115` | thấp | cao |
| `AssemblyReference.cs`, tham chiếu `RideBound.Infrastructure` | (a) | ỨNG VIÊN BỎ | git grep: không ai dùng | thấp | thấp |

### 3.2 Domain (`src/RideBound.Domain`)

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| `CommitmentDimension`, `CommitmentVector` (10 chiều) | (c); đủ 10 giá trị nằm trong state chuẩn hóa | GIỮ | `OnlineStateCanonicalWriter.cs:739-746` | thấp | cao |
| Mô hình lời hứa (`RiderPromise.cs`) | (c) | GIỮ | | thấp | cao |
| Lõi `CommitmentPolicy` | (c) | GIỮ | `CommitmentPolicy.cs:129-311` | TB | cao |
| Khóa cơ bản (accepted assignment, onboard, final confirmation) | (c) | GIỮ | `CommitmentLockEvaluator.cs:28-106` | TB | cao |
| `ratchetLocks` | (c) G2 | GIỮ | `G2-EOHZ35.json` | thấp | cao |
| Freeze horizon | (b) nhánh WP14R c1-freeze300/600/300ratchet; harness ADR-090 (20 lượt) | GIỮ | `wp14r-010:46-49`, `wp14r-013:82` (phản biện cũ ghi REMOVE_OK, đã bị bác) | thấp | TB |
| Hạn chót tỉ lệ slack, rào chặt và rào lỏng | (c) EO35/EOHZ35/SHF/SHHZ | GIỮ | `CommitmentPolicy.cs:322-385`; `CommitmentLockEvaluator.cs:185-236` | TB | cao |
| Hạn chót slack cố định (`dropEtaDeadlineSlackMs`) | (b) G1 (DLHZ-120, DLS-120); (b)-LV nhánh M+ | GIỮ | `replay_gate_dwell.py:43-70`; `Mplus-*.json` | thấp | TB |
| Hạn chót hai chiều | (b) Tier 2; Tier 3 VM 48 job + pilot 60 job | GIỮ (lưu trữ muộn nếu chủ chấp nhận) | `kits/tang3/configs/M-*.json:99`; `docs/18:5185-5188,6349-6354` | thấp | thấp |
| Trần từng lần (`dropEtaRevisionLimitMs`) | (b) 12 job PS-300 trong G1; (b)-LV W28 | GIỮ | `DWELLOFF-GATE-SUMMARY.json:574-588`; `docs/18:5777,5850` | thấp | TB |
| `CommitmentBudgetEvaluator`, ngân sách tỉ lệ slack (luật C) | (c) RCE35/SHRC | GIỮ | `CommitmentBudgetEvaluator.cs:41-119` | thấp | cao |
| `customerVisible` | (b)-LV T3.8 V-30 (Bảng 4.3/4.5/4.6) | GIỮ | `kits/tang3/configs/V-30.json:6` | thấp | TB |
| Hỗ trợ G2 (relocation, khóa, validator) | (c) | GIỮ | `CommitmentLockEvaluator.cs:52-57`; `PhysicalPlanValidator.cs:116-145` | TB | cao |
| `ShadowAllocation` | (c) | GIỮ | `ShadowAllocation.cs:1-41` | thấp | cao |
| `CommitmentLedger` (bền vững) | (c) | GIỮ | ADR-089 | TB | cao |
| `PhysicalPlanValidator` | (c) | GIỮ | | cao | cao |
| Nới chất lượng dịch vụ + breach ngoại sinh | (c) | GIỮ | ADR-045/047/049 | TB | cao |
| `TravelTimeForecast`, `ITravelTimeLookup.Arrive` | (c) CL | GIỮ | gate forecast | TB | cao |
| Breach ForcedReference / ForcedNoWorse | (c) EO35/SHF; (b) nhóm p1 trong G1 | GIỮ | `OperationalIncidentLedger.cs:178-192` | TB | TB |
| Ghi lên xe trễ (`ObservedLatePickup`) | (c) | GIỮ | `wp4-via-*-a1.json:8` | thấp | TB |
| Sự cố vận hành (incident) | (a) với mọi lượt chạy, nhưng thuộc giao thức v1 (fixture golden 08) | GIỮ | `EventBatchMessages.cs:18-19`; `docs/06:610-622` | TB | cao |
| `RideBoundRun`, `RideRequest`, `VehicleState` | (c) | GIỮ | | TB | cao |
| `Rehydrate` | (b) restore | GIỮ | | TB | TB |
| `RoutePlan`, kiểu nguyên thủy | (c) | GIỮ | | thấp | cao |
| Helper không ai gọi (`GetRemainingLegs`, `RouteLeg`, `CreateNoOp`, `WaitingRiderRelocation.Find`, `ServiceQualityAllowance.HasBreaches`) | (a) | ỨNG VIÊN BỎ | git grep | thấp | thấp |

### 3.3 Application (`src/RideBound.Application`)

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| Lõi `CommitmentDecisionValidator` | (c) | GIỮ | `RunnerSession.cs:1211-1226` | cao | cao |
| Trigger lời hứa đầu (2 nhánh) | booking-confirmation (c); initial-acceptance (b) | GIỮ | `CommitmentDecisionValidator.cs:272-334` | TB | cao |
| Phục hồi forced/no-worse | (c) | GIỮ | `:624-861` | cao | cao |
| Biên quyết định G2 | (c) | GIỮ | `:864-1090` | cao | cao |
| Mốc slack + móc Shadow | (c) | GIỮ, thêm test (chưa có test Application nào cho nhánh Shadow) | `:386-425` | TB | cao |
| Ánh xạ các biến thể cổng (floor, revision cap, customerVisible) | (b) | GIỮ | `:461-516,807-809` | thấp | TB |
| `CollectAllCommitmentWitnesses` | (b) WP14; harness | GIỮ | `Wp4RunnerConfiguration.cs:228-231` | thấp | thấp |
| `RevisionReasonCode` | (c), nhưng giá trị công bố luôn là `"ONLINE_REPLAN"` | ĐƠN GIẢN HÓA | `RunnerSession.cs:1211-1224` | thấp | thấp |
| `EventReducer` | (c) | GIỮ | | TB | cao |
| Sự kiện incident | (b) giao thức | GIỮ | | thấp | TB |
| `EventReductionCoordinator` | (c) | GIỮ | | thấp | TB |
| `OnlineState` (constructor 5 tham số, `PlanPool`) | (c) | ĐƠN GIẢN HÓA (bỏ `PlanPool` cùng B5) | `OnlineState.cs:17-33` | thấp | cao |
| `VersionedPlanPool` | (a) | ỨNG VIÊN BỎ (cùng B5) | | TB | TB |
| `CandidateSelectionProblem/Option/ObjectiveLevel` | (c) | GIỮ | | TB | cao |
| Phát hiện mức mục tiêu hằng | (b) WP14; harness | GIỮ | ADR-067 | thấp | thấp |
| `CandidateSelectionSolution`, `LexicographicObjectiveComparer` | (c) | GIỮ | | thấp | TB |
| Cổng solver (`ICandidateSelectionSolver`, ngân sách tất định) | (c) | GIỮ | | TB | TB |
| `SafeCandidateSelectionExecutor` + fallback | (c) | GIỮ | | TB | cao |
| `PromiseProjector`, `PromiseDeltaCalculator`, `RouteScheduleProjector`, `TravelTimeSnapshot` | (c) | GIỮ | | thấp–TB | cao |

### 3.4 Algorithms và OR-Tools

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| Lõi `InsertionCandidateGenerator` (best-first) | (c) | GIỮ | `SolverBackedRidePoolingPolicy.cs:302-304` | cao | cao |
| Probe chất lượng dịch vụ ngoại sinh | (c) | GIỮ | `InsertionCandidateGenerator.cs:424-479` | TB | cao |
| `CandidateModels` (các tùy chọn sinh) | (c) | ĐƠN GIẢN HÓA (chỉ khi bỏ thành phần tương ứng) | bằng chứng ghi `scheduleStrategy`, `relocatedWaitMs`, số đếm repair (`SolverExecutionEvidenceMapper.cs:191-195,396-404`) | thấp | TB |
| `CandidateIdentity` | (c) quyết định tie-break; emulator có bản sao trong `identity.js` | GIỮ | `CandidateIdentity.cs:41-85,148-160`; `engine17.js:340-344` | thấp | cao |
| Forward slack + cache | (c) | GIỮ | | TB | cao |
| origin-hold | (a) với mọi cấu hình trong repo | ỨNG VIÊN BỎ (sau khi grep E:) | 27/27 giá trị là earliest-feasible; Shadow và G2 đều cấm (`Wp4RunnerConfiguration.cs:565-571,704-713`) | TB | thấp |
| Lưu giữ `service-set-stability-portfolio-v1` | (c) | GIỮ | ADR-037 | cao | cao |
| Lưu giữ legacy (mặc định khi vắng khóa) | (b) golden WP2/WP4/WP6 | GIỮ | `Wp4RunnerConfiguration.cs:684-695` | thấp | TB |
| Chế độ exact-small | (b) oracle test | GIỮ | 13 tệp test | thấp | thấp |
| Hạt giống repair B4 | (a) | ỨNG VIÊN BỎ (giữ `HasExactMutablePair`, chuỗi `"no-repair"`, bộ đếm repair = 0) | `WaitingRiderRelocationBuilder.cs:51`; `InsertionCandidateGenerator.cs:1245-1250` | TB | TB |
| `stopServiceDurationMs` (dwell) | (b) 6 job dwell-24 thất bại; luận văn nói mã chấp nhận khóa này (`08c:285-288`) | GIỮ | `PHASE1-LOG.md:405-434`; ADR-083 | thấp | thấp |
| G2: sinh ứng viên | (c) | GIỮ | `wp4-via-g2k8-a1.json` | cao | cao |
| G2: `maximumRidersPerVehicle` | không cấu hình nào trong repo dùng | GIỮ cho tới khi grep E: | `WaitingRiderRelocationBuilder.cs:56-58` | thấp | thấp |
| G2: đánh giá cặp, ràng buộc CP-SAT, áp dụng | (c) | GIỮ | `OrToolsCandidateSelectionSolver.cs:381-445` | cao | cao |
| `HardVectorCandidateAssessor` (cổng C1) | (c) | GIỮ | `SolverBackedRidePoolingPolicy.cs:357-366` | cao | cao |
| Phục hồi forced/no-worse | (c) | GIỮ | | cao | cao |
| Vector sử dụng cứng, revision, warning | (c) chạy trên mọi ứng viên (3 chiều `hardLimit 0`), nhưng dưới cost-first giá trị không đi ra đầu ra | ĐƠN GIẢN HÓA (tùy chọn) | `HardVectorCandidateAssessor.cs:871-980`; `SolverBackedFleetSelection.cs:298-300,487-512` | TB | TB |
| Thứ tự mục tiêu | cost-first (c); stability-first (b)-LV T3.8, WP9 | GIỮ, không đổi mặc định | `SolverBackedFleetSelection.cs:23-27,347-360`; `Wp4RunnerConfiguration.cs:489-496` | thấp | cao |
| Điều phối `SolverBackedRidePoolingPolicy` | (c) | ĐƠN GIẢN HÓA (bỏ nhánh B2–B4) | `:322-386` | TB | cao |
| `SolverBackedFleetSelector` | (c) | GIỮ | `:300-410` | cao | cao |
| Helper áp lựa chọn (các hàm static của `RollingCostPolicy`, `Finish`) | (c) | ĐƠN GIẢN HÓA (dời chỗ, đổi tên) | `SolverBackedRidePoolingPolicy.cs:309,425,613-628` | TB | TB |
| B1 rolling-cost qua solver | (b)-LV Bảng 4.9 (FREE), WP9 H6 | GIỮ | `wp9-fleetpy-rolling-cost-audited-v1.json` | thấp | TB |
| Đường không WP4 (`RollingCostPolicy.Decide`, `CandidateFleetSelector`, `CommitmentCandidateFilter`) | (b) golden WP2/WP3 | GIỮ | `RunnerSession.cs:1173-1191` | thấp | TB |
| B2, B3, B4 | (a) | ỨNG VIÊN BỎ (sau khi grep E:) | git grep policyId: chỉ xuất hiện trong docs | TB | thấp |
| B5 | (a) | ỨNG VIÊN BỎ (sau khi grep E:) | | cao | TB |
| C2 + warning profiles | (b) WP9; harness ADR-090 | GIỮ | `wp9-fleetpy-soft-hard-hybrid-audited-v1.json` | TB | thấp |
| Oracle C1/C2 không dùng solver | (b) oracle test, gồm cả test lưu giữ dùng ở luồng cuối | ĐƠN GIẢN HÓA (dời vào dự án test) | `ExactSmallCommitmentDifferentialTests.cs:309-338,487` | TB | thấp |
| Bằng chứng retained-portfolio | (b) | GIỮ | | TB | thấp |
| `OrToolsCandidateSelectionSolver` | (c) | GIỮ | ADR-089 S1/S1b | cao | cao |
| `skipConstantObjectiveLevels` | (b) WP14; harness | GIỮ | ADR-067 | thấp | thấp |
| Shareability scorer | (c) SH* | GIỮ | golden bit-exact | cao | cao |
| `ShadowAllocator` | (c) SH* | GIỮ, thêm test cho `Allocate` | | TB | cao |
| `tools/RideBound.CandidateHotPathBenchmark` | (b) ADR-052; JSON của nó có thời gian đồng hồ nên không tái lập từng byte được | ỨNG VIÊN BỎ (khôi phục được từ `2d6791f`) | 4/6 bộ đếm đã được ghim trong `CandidateSearchWorkProfileTests.cs:22-24` | thấp | thấp |
| `tools/RideBound.Wp4Microbenchmark` + `scripts/run-wp4-microbenchmark.ps1` | (b) bảng thời gian mô tả | ỨNG VIÊN BỎ | không có trong `RideBound.slnx` | thấp | thấp |

### 3.5 Contracts và Infrastructure

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| Dự án `RideBound.Infrastructure` | (a) rỗng từ lúc scaffold | ỨNG VIÊN BỎ | chỉ có một commit `d672814` | thấp | thấp |
| Các `AssemblyReference` không dùng (giữ bản của Domain) | (a) | ỨNG VIÊN BỎ | chỉ `AssemblySmokeTests.cs:10` dùng bản của Domain | thấp | thấp |
| Kiểu nguyên thủy giao thức | (c) | GIỮ | | thấp | cao |
| `finalizeRun`, `runSummary` | (a), nhưng là từ vựng v1 | ỨNG VIÊN BỎ (cần ADR giao thức) | `RunnerSession.cs:252-264` không có case cho chúng | thấp | thấp |
| `ProtocolEnvelopeCodec` | (c) | GIỮ | | TB | cao |
| Lõi `ProtocolVersionCompatibility` | (c) | GIỮ | | thấp | TB |
| Profile forward-minor | (a) | ỨNG VIÊN BỎ | không caller nào truyền profile | thấp | thấp |
| `ProtocolPayloadReader` (+ `FirstError` đặt nhầm chỗ) | (c) | GIỮ (dời `FirstError`) | `HelloMessages.cs:289` | thấp | TB |
| `CanonicalJson` | (c); nằm cả trong đường byte của normalizer | GIỮ, đóng băng byte | `NdjsonWriter.cs:29`; `FleetPyManhattanNormalizer.cs:1010,1145` | TB | cao |
| `ProtocolHash` v1 (manifest, state identity, decision) | (c) | GIỮ | G1 | TB | cao |
| Overload span của decision hash v1 | test-only, nhưng là chỗ duy nhất có vector cố định | ĐƠN GIẢN HÓA (biến thành wrapper, sau N2) | `ProtocolHashTests.cs:23-83` | thấp | TB |
| Domain `DecisionHash.light.v1` | (c) | GIỮ, thêm vector (N2) | `ProtocolHash.cs:110-131` | thấp | cao |
| Hash v2 (`CalculateDecisionHashV2`) | (a) | ỨNG VIÊN BỎ (cùng nguyên mẫu v2) | | thấp | thấp |
| Hash và shell checkpoint dạng stream | (c) | GIỮ | ADR-089 S2 | TB | cao |
| Decode/kiểm checkpoint; restore | (b) | GIỮ | fixture golden 10; RB-WP3-012 | TB | TB |
| `CheckpointPayloadCodec.Encode` | test oracle cho đường stream | GIỮ tại chỗ, ghi chú là oracle | test `CheckpointStreamingHashTests` | thấp | thấp |
| `RestoreAcknowledgedPayloadCodec.Decode` | (a) không caller | ỨNG VIÊN BỎ | | thấp | thấp |
| Đàm phán năng lực (hello/helloAck, `PositionModel`) | (c); `nodeOnly` là yêu cầu chung cho mọi adapter | GIỮ | `RunnerHost.cs:11-21`; `CapabilityNegotiator.cs:254-258` | TB | cao |
| Từ vựng `downgraded`, `downgradePolicyId` | không dữ liệu nào dùng; là đặc tả v1 | GIỮ (chỉ bỏ qua ADR giao thức) | `docs/06:423-433` | thấp | thấp |
| 3 ID năng lực không ai dùng | (a) | ỨNG VIÊN BỎ (cần ADR) | | thấp | thấp |
| `InitializeRun`, `RunManifestIdentityCodec` | (c) | GIỮ | | TB | cao |
| Decode event batch | (c) | GIỮ | | cao | cao |
| Đường encode `requestArrived` (`EventBatchPayloadCodec.Encode`, `ProtocolPayloadReader.Write`, `OnlineContractCodec.WriteRequest`) | (b) nằm trong đường byte của normalizer | GIỮ, đóng băng byte | `FleetPyManhattanNormalizer.cs:986` | TB | cao |
| Các codec chiều ngược còn lại | (b) chỉ harness WP6 dùng | theo quyết định WP6 (bước 8.1) | | cao | TB |
| Encode quyết định, `OnlineDecisionActionCodec` | (c) | GIỮ | | cao | cao |
| `offerProposed`, `commitmentBreachDeclared` | (a) Runner không phát ra, adapter từ chối | ỨNG VIÊN BỎ (cần ADR; giữ metric `BreachCount` = 0 chừng nào bộ tính metric còn) | `protocol.py:326-327` | thấp | thấp |
| Validator bằng chứng solver 1.0–1.3 | (c) | GIỮ | | TB | TB |
| Validator retained-portfolio | (b) | GIỮ (chỉ bỏ cùng một khối với profile của Runner) | `DecisionMessages.cs:121-122,177-265,944-1806` | cao | TB |
| Codec lỗi | (c) | GIỮ | | thấp | thấp |
| `Wp4PolicyConfigurationBinding` | (c) | GIỮ | `mapping.py:150-158` | thấp | cao |
| `CanonicalUnits` | (a) | ỨNG VIÊN BỎ | chỉ test dùng | thấp | thấp |
| Schema `benchmarks/schemas/v1` và fixture giao thức | (b) đặc tả | GIỮ | | TB | thấp |

### 3.6 Benchmarking và tools

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| Normalizer FleetPy Manhattan (5 tệp `Normalization/*`) | (b)-LV, sinh mọi fixture Manhattan (luận văn `7f93ee6`, cầu nối, H6) | GIỮ, đóng băng byte | hash nguồn `34517a83…4d90` khớp cả 86 fixture | cao | cao |
| Đường byte ngoài 5 tệp (Contracts encode, `CanonicalJson`, `BenchmarkContractCodec`, `BenchmarkIdentity`, `SafeZipExtractor.CanonicalInventory`, mục FleetPy trong `DatasetSourceRegistry`, `Wp6Normalize/Program.cs`) | (b) cùng lý do | GIỮ, đóng băng byte | hash nguồn không phủ các phần này | TB | cao |
| `Wp6Normalize` CLI | (b) | GIỮ | `kits/tang3/calib/panelA/normalize_panelA.py:23` | TB | cao |
| `Datasets/*` | (b) | GIỮ | | cao | TB |
| `Wp6Dataset` CLI | trùng với Wp6Normalize, chỉ thêm `--resume-partial` | ĐƠN GIẢN HÓA (tùy chọn) | | thấp | thấp |
| Contracts phần scenario/report | (b) | GIỮ, đóng băng | | cao | cao |
| Contracts phần run-side + `BenchmarkSeed` | (b) chỉ WP6 dùng | ỨNG VIÊN BỎ (cùng khối WP6) | | TB | TB |
| Mảnh H6 của WP8/WP9 (`ExperimentalUnitModels`, bộ tính và oracle burden) | (b) bị ghim trong biên lai freeze WP9; số H6 do Python tính | ỨNG VIÊN BỎ (lưu trữ, không sửa tại chỗ) | `wp9_freeze_verify.py:81-89` | TB | TB |
| Planning, Execution, Storage, metric cơ học + `Wp6MetricOracle`, Claims, Bundles, EndToEnd, `Wp6Tiny/Medium/BundleVerify/FakeChild/SeedVectors` | (b) WP6 | ỨNG VIÊN BỎ (lưu trữ như một khối) | | cao | thấp |
| `Benchmarking.Tests` | phần data-side (khoảng 1,3k dòng) cần giữ | ĐƠN GIẢN HÓA (tách đôi) | `Wp6SourceAuditTests` test đầu bảo vệ tính tất định của normalizer | cao | thấp |
| `Benchmarking.Contracts.Tests` + schema WP6 | data-side cần giữ | ĐƠN GIẢN HÓA (tách đôi) | | TB | thấp |
| `tools/create_post_wp10_review_pdf.py` | (b) tài liệu | ỨNG VIÊN BỎ (giữ tệp PDF) | | thấp | thấp |

### 3.7 Adapter runtime (`simulators/fleetpy-ridebound/ridebound_fleetpy`)

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| `dev/misc/init_modules.py` | (c) | GIỮ | | thấp | cao |
| Khung và guard của `RideBoundFleetControl` | (c) | GIỮ | `fleet_control.py:82-143` | TB | cao |
| Nhận callback, máy trạng thái yêu cầu | (c) | GIỮ | `:190-361` | TB | cao |
| Tạo sự kiện từ chặng đã xong | (c) | GIỮ | `:588-817` | cao | cao |
| Chu kỳ batch/quyết định | (c) | GIỮ | `:402-461` | TB | cao |
| Áp quyết định, dựng `VehiclePlan` | (c) | GIỮ | `:863-993` | cao | cao |
| Cờ late boarding và forecast | (c) | GIỮ | `session.py:175-209` | thấp | cao |
| Hai hàm xử lý chặng khóa dài 0 (twins) | (c) | ĐƠN GIẢN HÓA (sau chiến dịch) | `fleet_control.py:1011-1106` | cao | cao |
| Snapshot thời gian đi đầy đủ + cache | (c) | GIỮ | `:513-586` | TB | cao |
| Chế độ snapshot thưa (`= 0`) | (b) chỉ preflight WP7 | ỨNG VIÊN BỎ (cùng WP7) | `actual_fleetpy_medium_preflight.py:863-866` | thấp | thấp |
| Yêu cầu checkpoint + `checkpointBindingHash` | (c) | GIỮ | `:690,695` | TB | TB |
| Restart/restore phía adapter, `acknowledge_and_checkpoint` | (b) cổng đóng WP7 (ADR-038) | ỨNG VIÊN BỎ (cùng WP7; viết lại 2 test) | `actual_fleetcontrol_preflight.py:262` | TB | thấp |
| Hook trừu tượng của FleetControlBase | (c) cần để khởi tạo lớp | GIỮ | | thấp | TB |
| Đọc cài đặt phiên | (c) | GIỮ | | TB | TB |
| Biên lai artifact | (c) | GIỮ | `session.py:320-386` | thấp | thấp |
| Nút chỉnh giới hạn dòng/timeout (hằng module, cài đặt, vá) | (c) | ĐƠN GIẢN HÓA (sau chiến dịch) | `session.py:20-33,88-96,455-471` | thấp | TB |
| `RideBoundProtocolSession` | (c) | GIỮ | | TB | TB |
| `RunnerClient` | (c); RidePy cũng dùng | GIỮ | `runner_client.py:310-325` | cao | TB |
| `protocol.py` | (c); RidePy cũng dùng | GIỮ | | TB | cao |
| `mapping.py` (ID mờ, hash snapshot) | (c); emulator có bản sao | GIỮ, đóng băng | `CE/…/identity.js:25-60` | TB | cao |
| `FleetPyProtocolMapper.stop_id` | (a) | ỨNG VIÊN BỎ | | thấp | thấp |
| `relocation.py` | (c) G2 | GIỮ | | TB | TB |
| `direct_network.py` | (c) ô đầy đủ | GIỮ | G3a/G3b | TB | cao |
| `transcript.py` | (c) | GIỮ | | thấp | cao |
| `errors.py`, `__init__.py` | (c) | GIỮ; không thêm `fleet_control` vào `__init__` (RidePy sẽ hỏng khi import) | | thấp | thấp |
| Bề mặt bị vá từ bên ngoài (tên private) | (c) launcher và world wrapper dựa vào | GIỮ, đóng băng tên | `bridge_launcher_via.py:37-85`; `preflight_world.py:171-192` | thấp | cao |

### 3.8 Script adapter và test Python

| Thành phần | Vai trò | Phân loại | Bằng chứng | Phức tạp | Rủi ro |
|---|---|---|---|---|---|
| `actual_fleetpy_medium_preflight.py` | (c) bộ chạy job duy nhất | GIỮ | `world-via/preflight_world.py:50` | cao | cao |
| Bề mặt cấu hình preflight (khóa driver, ID WP7 cứng) | (c) | ĐƠN GIẢN HÓA (chuyển timeout/giới hạn dòng sang driver; **không** đổi ID WP7) | `:224-258` | TB | cao |
| Gác freeze lúc chạy (inventory, semanticHash) | (c) | GIỮ | `:758-813,867-918` | thấp | TB |
| `actual_fleetpy_medium_verify.py` | (b); artifact bắt buộc; không đọc được gzip | GIỮ | `:520-523,1394` | cao | TB |
| Preflight WP7 (Runner, FleetControl, lifecycle matrix, clock tiny) | (b) WP7-014 | ỨNG VIÊN BỎ (giữ `wp7_tiny.csv`, `wp7-fleetpy-medium/`) | | TB | thấp |
| `capability_probe.py` + `capability-matrix.json` + `environment.lock.yml` | (c) và kiểm môi trường | GIỮ | `session.py:329-333` | thấp | thấp |
| README adapter | ghi baseline cũ (77/77) | ĐƠN GIẢN HÓA | | thấp | thấp |
| 41 tệp `.pyc` đang được track | (a) | ỨNG VIÊN BỎ | `.gitignore:31-35` | thấp | thấp |
| Bộ phân tích WP9 (`fixed_panel`, `robustness`, `reproducibility`) | (b) H6 còn được trích (`docs/03:224,238`) | GIỮ (hoặc lưu trữ cả họ WP9) | | cao | thấp |
| Chạy và freeze WP9 | (b) | ỨNG VIÊN BỎ (tag `41a4e11`; cần cùng bộ phân tích) | | TB | thấp |
| Họ WP13 (13 script) | (b) | ỨNG VIÊN BỎ (tag `38d517d`) | | cao | thấp |
| Họ WP14, WP14R (32 script) | (b) | ỨNG VIÊN BỎ (tag `41a4e11`; grep `wp14r_` ở E: trước) | | cao | thấp |
| Test cho đường cuối (11 tệp, `fake_runner.py`) | kiểm chứng | GIỮ | | TB | TB |
| Test WP lịch sử | kiểm chứng | ỨNG VIÊN BỎ cùng script, **trừ** `test_wp13_runner_retained_candidate_portfolio_schema.py` và test bằng-nhau inventory (`test_wp9_run_matrix.py:120-132`) | | cao | thấp |
| Test provenance đang đỏ (6 lỗi đã biết) | che lỗi mới | ĐƠN GIẢN HÓA (khai báo đúng nguyên nhân) | | TB | thấp |

### 3.9 Cấu hình, kịch bản, kit nghiên cứu

| Thành phần | Vai trò | Phân loại | Bằng chứng | Rủi ro |
|---|---|---|---|---|
| `benchmarks/scenarios/wp7-fleetpy-medium/` | (c) được băm vào manifest | GIỮ, không sửa cả README | `session.py:334-353` | cao |
| `wp7-fleetpy-tiny/data/vehicles/wp7_tiny.csv` | (c) sức chứa 4 | GIỮ | `actual_fleetpy_medium_preflight.py:478-497` | cao |
| `wp2-tiny`, `wp3-commitment-tiny` | (b) golden | GIỮ | | TB |
| `wp3-boundary-test-v1.json`, `wp4-rolling-cost-boundary-v1.json` | test vá chuỗi trong các tệp này | GIỮ, không định dạng lại | `ShadowAllocationConfigurationTests.cs:122-138` | TB |
| `wp14-c1-h6-reference-v1.json` (gốc mọi overlay) | (b) | GIỮ | `make_configs_t3.py:4,20` | cao |
| Overlay yếu tố WP14 | (b); harness dùng `f1f2-freeze300-ratchet` | GIỮ | | thấp |
| `wp14-development-ridebound-hard-vector-v1.json` | (b) gốc chuỗi WP4 | GIỮ | | thấp |
| Cấu hình WP6–WP13 | (b); harness dùng một phần | GIỮ | | TB |
| `wp8-drop-eta-budget-medium-v1.json` | harness ADR-090 dùng **theo tên** | GIỮ (hoặc đổi harness sang `wp14-c1-budget60-v1.json`, byte giống hệt) | phát hiện bổ sung về harness | thấp |
| `wp8-relaxed-stopswitch-commitment-v1.json` | (b) WP8-002 §5 | ỨNG VIÊN BỎ (khôi phục: `git show e584a66:…`, sha `f1ac67c0…`) | | thấp |
| `wp10-ridepy-paired-subset-v1.json` + 3 cấu hình mà `wp10_subset_analyze.py:20-26` ghim | (b) | GIỮ | | thấp |
| Panel WP8–WP14R | (b) | GIỮ | | TB |
| `fixtures/wp6/public/**` | (b)-LV, emulator đọc | GIỮ | `CE/…/lib.js:8,119` | cao |
| `fixtures/wp6/{e2e,metrics,planning}`, schema WP6 run-side | (b) WP6 | ỨNG VIÊN BỎ (cùng WP6) | | thấp |
| Schema `v1`, schema WP6 data-side, `runner-retained-candidate-portfolio-evidence.schema.json` | (b)/(c) | GIỮ | | TB |
| Mô tả dataset Zenodo | (c) provenance | GIỮ | | thấp |
| `scripts/run-wp2-tiny-demo.ps1`, `run-wp3-commitment-demo.ps1` | (b); test đã phủ hành vi tương đương | ỨNG VIÊN BỎ | | thấp |
| `kits/via-bridge-2026-10-22`, `bridge-phase1-2026-10-23` (cấu hình, driver, `replay_gate_dwell.py`) | (b) định nghĩa tập 228 job | GIỮ | | cao |
| `kits/via-real-2026-10-26` | (b) G3a, lưới zone | GIỮ | | TB |
| `kits/tang3` (+ harness Stage 1b) | (b)-LV | GIỮ | | TB |
| `kits/g2-2026-10-23`: patch v3/v4/fix | tái tạo được bằng `git diff` | ỨNG VIÊN BỎ (kèm công thức tái tạo + sha) | | thấp |
| `kits/g2-2026-10-23`: patch v2/A-F/A/AB, `world/` | cây nguồn không còn trong object store; driver kiểm `world/` | GIỮ | | cao |
| `bridge-phase1`: 3 bản sao byte (`fleet_control.*`, `p1dwell-tracked-changes.patch`) | tái tạo được từ git | ỨNG VIÊN BỎ (kèm công thức + sha) | | thấp |
| `bridge-phase1`: `restore_index_bytes_*.py` | (b) cần để tái lập inventory `4f4dacd3` | GIỮ | | TB |
| Kit p5, breakeven, identity, p7 | (b)-LV | GIỮ | | thấp |
| `research/continuation/2026-10-15`, `2026-10-16` | (b)-LV Stage 1b; cost-cap là (b) | GIỮ | | thấp |
| `START-HERE.md` | lỗi thời | ĐƠN GIẢN HÓA (thêm con trỏ ADR-089/090) | | thấp |

### 3.10 Emulator JavaScript (`CE/`)

| Thành phần | Vai trò | Phân loại | Bằng chứng |
|---|---|---|---|
| `engine17.js`: 6 bản giống hệt (blob `927c9b4c`, sha `81d7b47e…`) | (c) nguồn số luận văn | GIỮ, niêm phong; coi như **một** module có 6 bản sao | mọi `CODE-HASH`/`PLAN-HASH` khớp |
| `identity.js`, `normalizer_emul.js`, `scenario.js`, `sscore.js`, `lambda.js`, `forecast.js`, `metrics_sh.js` | (c) | GIỮ, niêm phong | sha khớp seal |
| Các tệp chứa đường dẫn `E:/RideBoundData` (`lib.js`, `netfull.js`, `shlib.js`, `RUNBOOK.sh`, `drive*.js`, `gen17.js` …) | (c) | GIỮ; ghi rõ là **khác seal** (sửa đường dẫn trong `21af556`) | chỉ được kiểm bằng hành vi: F14 1,680/1,680 |
| Seal fullsys không phủ `identity.js`, `normalizer_emul.js`, `lib.js` | khoảng hở | ghi vào tài liệu | `wf20/fullsys/PLAN-HASH.txt` |
| `wf20/fullsys/code/RUNBOOK.sh` | là bản sao runbook r1 | ghi chú; runbook thật là `wf20/fullsys/RUNBOOK.sh` | blob `09fa3a2c` |
| `expected/*.tsv`, `tools/confirm_digest.js`, `check_inputs.js` | (b) oracle | GIỮ | |
| Engine wf15 `engine.js` (sha `908a2961…`), `engine_wf15_clint.js`, `emulate_via_full.js` | (c) cho mọi dự đoán adapter-mode; **không có trong repo**; sha của clint chưa rõ | lấy bản và ghi sha (bước 0.4) | `TravelTimeForecast.cs:10-11` |

Phân loại các nhánh bên trong `engine17.js` (bản niêm phong không sửa; bảng này dùng khi viết lại):

| Loại | Nhánh |
|---|---|
| (c) | các arm U, EO, EOHZ, RCE, SHF, SHHZ, SHRC (`:73,81-83,86-88`); `brOf` (`:43`); xe bị ép cho EO/SHF (`:474`); khóa `exact`/`ratchet` (`:294-298`); poolCap 100 (INS) hoặc vô hạn (G2) (`:129`); lưu giữ ứng viên (`:347-396`); dời G2 với 8 xe đích (`:524-555`); tie-break ưu tiên giữ (`:571,647`); tie-break theo ID ứng viên (`:340-344,563-575`); `legF` + `clipPickup` (`:153-169,722,739`); móc điểm shareability (`:743-777`) |
| (b) | dung sai rào P1 (`:30,77,81,86`); phạt dời P2 `g2PenaltyMs` (`:615`); P3 `flatRatio` (`:161`); arm NCE (`:84`); RC/PS/DLHZ/DLS/NC/OC chỉ qua `engine.js` ngoài repo |
| (a) | RR, FZ, OCE, SHNC; tùy chọn `g1`, `dr`, `g2MaxSwitches`; khóa `ratchetFloor`/`none`; `tieMode 'index'`; `reoptTie 'id'`; `shadowArms`, `pairs`, `trace`; `physMat` (khoảng 85–90 dòng, ước lượng) |

### 3.11 RidePy (`simulators/ridepy-ridebound`, WP10)

| Thành phần | Vai trò | Phân loại | Bằng chứng |
|---|---|---|---|
| Toàn bộ thư mục (20 tệp, 3,125 dòng .py) | (b) bằng chứng WP10 (kết quả âm Layer 3) | GIỮ, đóng băng | import `RunnerClient`, `protocol`, `mapping`, `errors`, `transcript` |
| Script tính lại (`wp10_subset_analyze`, `bundle_verify`, `source_verify`) | (b) chỉ dùng thư viện chuẩn | GIỮ | |
| Tham số `RunnerClient` `expected_position_model`, `required_capabilities` | (b) chỉ RidePy dùng | GIỮ | `runner_client.py:53-58`; gộp hai tham số này là RidePy hỏng |
| `nodeOnly` trong Runner | (c) Runner đòi mọi adapter có | GIỮ | `RunnerHost.cs:11-21` |
| 4 tệp `.pyc` đang được track | (a) | ỨNG VIÊN BỎ | 2 tệp đã lỗi thời |

### 3.12 Chuỗi công cụ build

| Thành phần | Vai trò | Phân loại | Bằng chứng |
|---|---|---|---|
| Ghim `Google.OrTools 9.15.6755` + hằng `AdapterVersion` + kiểm cấu hình | (c); chỉ kiểm **nhãn**, không kiểm binary thật | GIỮ; thêm kiểm khi khởi động (tùy chọn) | `RideBound.Solvers.OrTools.csproj:9`; `Wp4RunnerConfiguration.cs:769-774` |
| `DependencyRuleTests.cs:137-169` (ghim phiên bản) | (c) gác duy nhất | GIỮ | |
| `.github/dependabot.yml` | rủi ro | thêm `ignore` cho OR-Tools/Protobuf | không có `ignore` |
| `global.json` 10.0.301 | (c)/(b): freeze WP13/WP14 đòi đúng 10.0.301/10.0.9 | GIỮ; ghi lại cách vượt pin trên máy chỉ có 10.0.400 | `docs/18:6096` |
| Manifest Runner `kits/**/runner-*.sha256.tsv` | (b) | GIỮ | 10 manifest có `deps.json` giống hệt nhau |
| Biên bản build cho `runner-fastrun-final1`, `runner-gd4-v1`, Runner final-v2 | **thiếu** | thêm | chỉ có dòng văn bản trong `docs/18` |

### 3.13 Mã trùng lặp

| Trùng lặp | Ở đâu | Hành động |
|---|---|---|
| Ba bộ helper đọc JSON cấu hình | `Wp4RunnerConfiguration.cs:921-1044`, `CommitmentPolicyConfiguration.cs:335-448`, `ShadowAllocationConfiguration.cs:125-166` | gộp (bước 3.1) |
| Parse chuỗi thập phân → double | `Wp4RunnerConfiguration.cs:450-465`, `ShadowAllocationConfiguration.cs:151-166` | gộp |
| Tra từ vựng chiều cam kết | `Wp4RunnerConfiguration.cs:853-861`, `CommitmentPolicyConfiguration.cs:295-303` | gộp |
| Khung hash có domain | `ProtocolHash.cs:223-254`, `CheckpointMessages.cs:140-160,168-203`, `OnlineStateCanonicalWriter.cs:181-190`, `OnlineStateCanonicalizer.cs:213` | gộp phần C#; **không** gộp bản Python của bộ kiểm độc lập |
| Hash state identity (4 chỗ gọi) | `RunnerSession.cs:393-396,559-563,594-597,998-1003` | dùng một helper |
| Hai cách tính decision hash v1 | `ProtocolHash.cs:31-67` và `:74-86,133-183` | biến cái thứ nhất thành wrapper |
| Ba bộ serializer state | writer, canonicalizer (oracle), `OnlineStateHashV2` | bỏ v2; giữ cặp còn lại có chủ ý |
| Sao chép từng trường `SolverBackedRidePoolingPolicyOptions` | `Wp4RunnerConfiguration.cs:247-287` | dùng `with` |
| Ánh xạ lifecycle → phase | `CommitmentBudgetEvaluator.cs:121-128`, `CommitmentLockEvaluator.cs:275-282` | gộp |
| Chọn cơ sở ngân sách (3 chỗ) | `CommitmentLedger.cs:331-333`, `CommitmentDecisionValidator.cs:806-808`, `OperationalIncidentLedger.cs:339-408` | helper chung |
| So sánh `PromiseProjection` từng trường | `OperationalIncidentLedger.cs:579-599`, `OnlineStateCheckpointCodec.cs:806-818` | helper chung |
| Tính lịch tuyến | `PhysicalPlanValidator.cs`, `RouteScheduleProjector.cs`, `ShadowAllocator.cs:217-240` | **giữ** (kiểm lại độc lập); chỉ gộp khi có gate byte |
| Hệ thứ bậc chọn đội xe | bản dùng solver + 4 oracle không solver | dời oracle vào test |
| Liệt kê cách chèn cặp đón/trả | `Expand`, `ReceiverRoutes`, repair seeds, oracle | để nguyên (thứ tự con ảnh hưởng tìm kiếm) |
| `Rank` và `RankComparer` | `CandidatePortfolioRetainer.cs:271-330` | thêm test so hai bản |
| Hai hàm twins chặng khóa | `fleet_control.py:1011-1106` | gộp (bước 3.7) |
| `_strict_object`, `_sha256`, `_fail` trong Python | 10, 23 và 4 bản | gộp phần trong package |
| Đọc/ghi dòng stdout có buffer | `runner_client.py:310-325` và các launcher | giữ để launcher cũ vẫn chạy |
| Cách thức chỉnh giới hạn dòng/timeout | cài đặt + vá + hằng 60 s | gom về một chỗ (bước 7.2) |
| Luật normalizer | C#, 6 bản `normalizer_emul.js`, `convert_via_zone.py` | C# là chuẩn; đổi một bên thì phải kiểm cả ba |
| Binding hash WP4 | C# và `mapping.py:150-158` | cố ý, giữ |
| `engine17.js` và đồng bọn | 6 bản sao | cố ý (niêm phong); chỉ ghi chú |
| Freeze WP14R v2…v6 | khoảng 5 nghìn dòng gần giống nhau | lưu trữ |

### 3.14 Mã không dùng (a)

- **Runner:** nhánh downgrade; `HashTiming`; `CacheHistories` và bộ đếm mảnh; `InternalsVisibleTo("HashBench")`; `AssemblyReference.cs`; tham chiếu `RideBound.Infrastructure`.
- **Domain:** `RoutePlan.GetRemainingLegs`, `RouteLeg`, `RoutePlan.CreateNoOp`, `WaitingRiderRelocation.Find`, `ServiceQualityAllowance.HasBreaches`.
- **Algorithms:** tham số `collectAllWitnesses` của `CommitmentCandidateFilter` (`:17-41,88`).
- **Contracts:** `RestoreAcknowledgedPayloadCodec.Decode`, `CanonicalUnits`, profile forward-minor, dự án `RideBound.Infrastructure`.
- **Adapter:** `FleetPyProtocolMapper.stop_id`, 45 tệp `.pyc` đang được track.
- **Không tới được với mọi cấu hình trong repo** (cần grep E: trước khi bỏ): B2/B3/B4/B5, origin-hold, `maximumRidersPerVehicle`, nguyên mẫu hash v2.
- **Từ vựng giao thức không ai phát** (bỏ cần ADR): `finalizeRun`, `runSummary`, 3 ID năng lực, `offerProposed`, `commitmentBreachDeclared`.

### 3.15 Tính năng lịch sử (b): kết quả cần gì, khôi phục ở đâu

| Tính năng | Kết quả cần nó | Điểm tái lập |
|---|---|---|
| stability-first, `customerVisible`, M+ (`dropEtaDeadlineSlackMs`), trần `hardLimit` | luận văn `7f93ee6` (T3.8, Stage 1b) | `runner-tier3-v2` (`45f8026`); HEAD **chưa rõ** (cần N1) |
| B1 rolling-cost | Bảng 4.9, WP9 H6 | WP14 `runner-v1`; HEAD dùng N5 |
| C2 + warning profiles | WP9 | harness ADR-090 |
| forced-reference-v1, DLS-120, G2K3, PS-300, C-120/C-300 | G1/G2 | HEAD (G1) |
| Hạn chót hai chiều | Tier 2, Tier 3 VM | `runner-deadline-v1`, `runner-tier3-v1/v2` |
| Freeze horizon, retained-portfolio, `skipConstantObjectiveLevels` | WP13 E1, WP14/WP14R | `38d517d` (E1); `41a4e11..c6588f8~1` (WP14) |
| `stopServiceDurationMs` | 6 job dwell-24 thất bại (ADR-083) | `9ace339` / `research/p1-dwell` |
| Chế độ online, commitment không WP4 | golden WP2/WP3 | HEAD (test) |
| Restart/restore, snapshot thưa, preflight WP7 | WP7-014 | HEAD |
| Bộ công cụ WP6 | đóng WP6 (tiny A, medium H/I) | HEAD hoặc `2d6791f` |
| Mảnh H6 C# | biên lai freeze WP9 v6 | `41a4e11` (mapping.py dạng CRLF) |

### 3.16 Chế độ chạy không cần cho thí nghiệm cuối

`--mode online`, `--mode conformance`, commitment không WP4, dạng 6 tham số, các chính sách B1–B5 và C2, stability-first, origin-hold, exact-small `true`, snapshot thưa, restart/restore, hash v2, `RIDEBOUND_HASH_TIMING`. Trong số này, chỉ những mục ở 3.14 là (a). Phần còn lại là (b) và phải giữ, hoặc lưu trữ có tag.

### 3.17 Khóa cấu hình có thể bỏ

Nguyên tắc chung:
- Parser từ chối khóa lạ (`CommitmentPolicyConfiguration.cs:368-392`; `Wp4RunnerConfiguration.cs:300-310`).
- Bỏ một khóa khỏi parser làm hỏng **mọi** cấu hình đang có khóa đó.
- Cấu hình không có khóa đó thì giữ nguyên hash.

| Khóa | Điều kiện để bỏ |
|---|---|
| WP4 `fixedFreeze`, `repair`, `multiplePlan` + policyId `rolling-penalty`, `fixed-freeze-horizon`, `no-reassignment-repair`, `least-commitment-consensus` | grep E: (pack, `fastrun-2026-11-02`, bundle `wp*`) không thấy; bỏ cùng B2–B5 |
| Giá trị `scheduleStrategy: origin-hold-relocated-wait` | như trên; vẫn giữ khóa và giá trị `earliest-feasible` |
| `waitingRiderRelocation.maximumRidersPerVehicle` | grep E: không thấy |
| Biến môi trường `RIDEBOUND_STATE_HASH`, `RIDEBOUND_HASH_TIMING` | `run_cloud.ps1`, `run_queue.py` không đặt |
| `completeTravelSnapshotMaximumNodes = 0` (snapshot thưa) | lưu trữ WP7; đổi mặc định thành báo lỗi |
| **Không được bỏ** | `stopDistances`, `solverExecution.randomSeed`, `adapterVersion`, mọi khóa (b) ở 3.15, `stopServiceDurationMs`, `freezeHorizonMs/Locks`, `dropEtaDeadlineTwoSided`, `budgetBasis` |

---

## 4. Chế độ chỉ chèn (insert-only, INS) và các chế độ tùy chọn khác

### 4.1 INS là gì

INS **không phải** một chế độ riêng. Trong `src`, `tests`, `simulators` không có enum, cờ hay lớp nào tên insert-only (git grep cho ra 0 kết quả). INS chính là bộ điều phối gốc khi tắt G2:
- Mỗi xe có một no-op (giữ tuyến), cộng các cách chèn best-first tối đa 2 yêu cầu chờ vào phần còn sửa được của tuyến (`InsertionCandidateGenerator.cs:102-121`).
- Không bao giờ xóa điểm dừng cũ hay chuyển khách sang xe khác.
- C1 (cost-first, OR-Tools) chọn một kế hoạch cho mỗi xe.

G2 là INS **cộng** năm thiết lập phải đi cùng nhau, nằm trong hai tệp:

| Thiết lập | INS | G2 |
|---|---|---|
| WP4 `candidateGeneration.waitingRiderRelocation` | vắng | `{maximumTargetVehicles: 8}` |
| Policy `waitingRiderRelocation` | vắng | `true` |
| `finalConfirmationLocks` | `[vehicle, pickupStop, pickupEta]` | `[pickupStop, pickupEta]` |
| `ratchetLocks` | vắng | `[pickupEta]` |
| `hardLimit` của `vehicle_switch_count` | `0` | không có |

Khóa xe có sẵn O-001 chỉ được nhấc khi policy có relocation **và** khách đang chờ đón (`CommitmentLockEvaluator.cs:54-57`). G2 còn đòi C1 cost-first và earliest-feasible (`Wp4RunnerConfiguration.cs:519-552`).

Trong emulator, `INS = {}` và `G2 = {reopt:{g2:99, g2Targets:8}, lock:'ratchet'}` (`shlib.js:12-14`). Với G2, poolCap của emulator là vô hạn, còn C# giữ 100. Mức 100 này chỉ được chứng minh là không chạm trần trên Manhattan (tối đa 12 ứng viên khả thi mỗi xe). Ở cỡ Via, chỉ có kết quả khớp của G3b làm bằng chứng.

Vì sao không thể viết INS thành "G2 với 0 lần dời":
- C# từ chối giới hạn dời nhỏ hơn 1 (`CandidateModels.cs:342-350`).
- WP4 và policy phải cùng bật (`Wp4RunnerConfiguration.cs:526-549`).
- G2 còn đổi khóa đón từ `exact` sang `ratchet`, nên các phép chèn thường cũng thay đổi.

### 4.2 Thí nghiệm cuối có cần INS không

**Trực tiếp: không.** Chiến dịch 168 job chạy G2 (số 71 nghĩa là G2). **Gián tiếp: có, theo ba cách:**
1. INS là mã nền mà G2 chạy lên trên: mọi quyết định G2 trước hết chạy bộ sinh INS.
2. Bảng luận văn có cột INS (x* = 72; `BAO-CAO-KET-QUA-26-10.md:33,94,108,213`).
3. INS gánh phần lớn việc kiểm chứng: 122/228 job G1/G2, 6/12 job Via zone, các job INS của G3a/G3b, và mọi lượt chạy trước 23/10.

Phân loại: phần mã INS là (c), phần cấu hình INS là (b). Không có phần nào của INS là (a).

**Khuyến nghị: GIỮ nguyên INS ở C#, adapter và emulator.**
- Đơn giản hóa ở mức tài liệu: mô tả là "bộ chèn gốc + lớp G2 tùy chọn", liệt kê 5 thiết lập ở một chỗ.
- Có thể thêm preset INS/G2K8 trong bộ sinh job (nằm ngoài repo).
- Có thể (tùy chọn) dời `GenerateRelocations` (khoảng 300 dòng) ra khỏi `InsertionCandidateGenerator` (bước 6.2).
- Sửa câu "INS là chế độ thật của RideBound" trong `BAO-CAO-KET-QUA-26-10.md:43`.
- Validator hiện **không** đòi `ratchetLocks` khi bật G2. Một cấu hình G2 viết tay thiếu ratchet vẫn chạy được, nhưng ra một biến thể emulator chưa từng chạy. Nên ghi điều này thành tài liệu. Nếu muốn ép thành luật, đó là luật mới và phải có test.

### 4.3 Các chế độ tùy chọn khác

| Chế độ / tùy chọn | Thí nghiệm cuối | Loại | Khuyến nghị |
|---|---|---|---|
| G2 relocation | có | (c); (b) cho 106 job G1 | GIỮ |
| `auditHash` `full-v1` / `light-v1` | có cả hai | (c) | GIỮ; khóa vắng phải luôn nghĩa là `full-v1` |
| `planningForecast` (CL/N) | có | (c) | GIỮ; thêm test phiên |
| `shadowAllocation` | có | (c) | GIỮ; thêm test phiên |
| `selectionObjectiveOrder` | cost-first (c); stability-first là mặc định (b)-LV | | GIỮ cả hai; không đổi mặc định |
| `commitmentRecovery` | no-worse (c); forced-reference (c cho EO35/SHF, b cho G1); fail-closed (b) | | GIỮ |
| `lateBoarding` | record-v1 (c, bắt buộc khi có forecast); reject (b) | | GIỮ |
| `initialPromiseTrigger` | booking-confirmation (c); initial-acceptance (b) | | GIỮ |
| `retentionStrategy` | stability-portfolio (c); legacy (b) | | GIỮ |
| `emitSolverExecutionEvidence` | bật/tắt (c) | | GIỮ |
| `routingEngine`, `transcriptCompression` | (c) | | GIỮ; chưa có lưới phát lại (N4) |
| B1 rolling-cost | không | (b)-LV | GIỮ |
| C2 | không | (b) WP9, harness | GIỮ |
| B2, B3, B4, B5 | không | (a) với repo | ỨNG VIÊN BỎ sau grep E: |
| origin-hold | không | (a) | ỨNG VIÊN BỎ sau grep E: |
| exact-small `true` | không | oracle test | GIỮ |
| `stopServiceDurationMs` | không (dwell 0, ADR-083) | (b) | GIỮ; chủ đóng quyết định dwell còn treo (`PHASE1-LOG.md:433-434`) |
| retained-portfolio, `skipConstantObjectiveLevels` | không | (b) | GIỮ |
| `--mode online` / `conformance` / commitment không WP4 | không | (b) | GIỮ; xem bước 7.1 |
| Dạng 6 tham số | không | (b) chưa rõ | ĐƠN GIẢN HÓA sau B5 |
| Snapshot thưa, restart/restore của adapter | không | (b) WP7 | lưu trữ cùng WP7 |
| Restore của Runner | không | (b) harness ADR-090 | GIỮ |
| `RIDEBOUND_STATE_HASH=v2` | không | (a) | ỨNG VIÊN BỎ có điều kiện |
| `RIDEBOUND_CANONICAL_SELFCHECK` | không | (b) oracle A0.1 | GIỮ |
| Emulator `identity.mode` plain / adapter | plain (c, số luận văn); adapter (c cho dự đoán, nhưng bộ sinh thật nằm ngoài repo) | | GIỮ; lấy bản `engine_wf15_clint.js` |

---

## 5. Kế hoạch refactor

**Nguyên tắc.**
- Làm trên một nhánh mới tách từ `ad470c1`, **sau** khi chiến dịch 168 job xong.
- Mỗi bước là một commit, hoàn tác được bằng `git revert`.
- Không xuất lại pack giữa chừng chiến dịch.
- Chiến dịch chạy bằng Runner trong pack, nên refactor HEAD không làm đổi kết quả của nó. Nhưng nếu phải build lại pack, ví dụ để thêm các sửa ADR-090, thì phải chạy lại mọi gate trên binary mới.

**Các phép kiểm dùng chung.**

| Mã | Kiểm tra | Chuẩn đạt |
|---|---|---|
| K1 | `dotnet test RideBound.slnx` | 1,265 test; 1,261 đạt trên máy công ty. 4 lỗi môi trường đã biết: 2 golden Contracts do CRLF, 2 Tiny harness. `StrictBagItBundleTests.cs:32` đỏ trên cây sạch |
| K2 | Harness byte ADR-090 | 26 ca cơ bản; 564 lượt WP4 (47 cặp cấu hình) có checkpoint + restore sau mỗi quyết định; 14 dòng lớn; 11 danh tính; `FAILS 0` |
| K3 | G1 | 228/228 job, 176,074 khung giống từng byte, kể cả checkpoint cuối |
| K4 | G2 | 228/228, bằng nhau sau khi bỏ `*Hash`, có bằng chứng solver |
| K5 | Cổng Via so với dự đoán emulator đã khóa | zone 12/12 (`compare_via.py`); G3a 36,952/36,952; G3b 16/16 (`compare_via_full.py`); forecast 704/704; Shadow 1,045/1,045 |
| K6 | Test adapter Python | 467 test, đúng 6 lỗi đã biết; đặt `RIDEBOUND_FLEETPY_ROOT`, 0 skip ở test FleetPy; bộ RidePy 23 test + smoke |
| K7 | So sánh emulator | `node tools/confirm_digest.js expected/<tệp>.tsv rows.ndjson`: fullsys và n7 F14 1,680/1,680; am2017 3,360/3,360; `check_inputs.js` 19/19 |
| K8 | Job mới so kiểu commit-free | `compare_runs_commit_free.py`: mọi dòng transcript bằng nhau sau khi bỏ `coreCommitSha`, `*Hash`, `*Sha256` |
| N1–N5, R1 | Lưới mới | xem bước 0.6 |

### Giai đoạn 0: chuẩn bị (không đổi mã sản phẩm)

**Bước 0.1: Đóng băng trong lúc chạy chiến dịch.**
- Không merge, không xuất lại pack.
- Không sửa worktree đang bị ghim inventory (`run_via.py:26`).
- Dừng ngay nếu có job nào đang chạy từ worktree đó.

**Bước 0.2: Gắn tag lưu trữ (tag cục bộ; việc đẩy lên remote do chủ quyết).**

| Tag | Dùng để tái lập |
|---|---|
| `ad470c1` | HEAD trước refactor |
| `38d517d` | WP13 E1; cần `mapping.py` và 21 JSON chuyển sang CRLF |
| `41a4e11` (hoặc `8d1ea4d`) | WP9 v6, WP14 v2, WP14R; cần `mapping.py` dạng CRLF |
| `45f8026` | T3.8 `runner-tier3-v2` |
| `dce84ec` | Stage 1b, phía cost-first |
| `2d6791f` | ADR-052, WP10, CandidateHotPathBenchmark |
| `6ab03fc` | `ADAPTER-INVENTORY` của Via zone |
| `c6588f8`, `ef2b02f` | G2 v3 và v4 |
| `1190518` | verifier đã freeze `89a9e9a7` |
| `e584a66` | cấu hình WP8 |

Ghi chú: commit `0eb9a6ca` và `937bb31` (trước khi viết lại tác giả) không có trong repo.

**Bước 0.3: Lưu harness ADR-090 vào chỗ lâu dài.**
- **Đã làm ngày 04/11.** Harness, danh sách 47 cặp, 2 cấu hình light/full và kết quả đã chép vào
  `research/continuation/2026-11-04/byte-harness/`. Đường dẫn giờ đọc từ `RIDEBOUND_REPO`, `OLD_RUNNER` và `NEW_RUNNER`.
- Đã chạy lại bằng bản lưu, với hai Runner publish từ `git archive` của `101a4f5` và `ad470c1`. Kết quả trùng lần đầu
  từng dòng: 564/564 lượt WP4; 25/26 ca cơ bản, ca khác là ca sửa có chủ ý.
- Trước mỗi bước refactor: chạy lại với `OLD_RUNNER` = HEAD trước bước đó. Phải ra `FAILS 0`, chỉ trừ các khác biệt đã khai
  báo.

**Bước 0.4: Ghi sổ những gì nằm ngoài repo (làm trên máy có ổ E:).**
- Ghi sha256 cấu hình cuối từ `final-v2-pack/MANIFEST.tsv` vào docs.
- Grep các tệp `*.json` trong pack và trong `fastrun-2026-11-02` tìm:
  - `fixedFreeze`, `repair`, `multiplePlan`, `origin-hold`, `maximumRidersPerVehicle`, `stopServiceDurationMs`, `freezeHorizon`, `dropEtaDeadlineTwoSided`, `solverExecutionEvidenceProfile`;
  - `stopDistances` không rỗng;
  - các giá trị `policyId`.
- Grep các tệp `*.py` và `*.ps1` tìm: `ridebound_fleetpy`, `restore(`, `restart`, `_repository_inventory_sha256`, `wp14r_`, `RIDEBOUND_STATE_HASH`.
- Lấy bản `engine_wf15_clint.js`, `emulate_via_full.js` và ghi sha.
- Kết quả bước này quyết định có làm các bước 2.x hay không.

**Bước 0.5: Chuỗi công cụ.**
- Thêm `ignore` cho `Google.OrTools*` và `Google.Protobuf` trong `dependabot.yml`.
- Viết biên bản build (SDK, runtime, lệnh publish, sha của cây, `deps.json`) cho `runner-fastrun-final1`, `runner-gd4-v1`, Runner final-v2.
- Tùy chọn: cho Runner báo lỗi khi khởi động nếu phiên bản OR-Tools đã nạp khác `AdapterVersion`. Kiểm bằng K1 + K3; không byte nào được đổi.

**Bước 0.6: Thêm lưới an toàn.** Mỗi test mới phải đỏ khi cố tình làm hỏng mã.

| Lưới | Nội dung | Ghi chú |
|---|---|---|
| N1 | Phát lại từng byte transcript stability-first: 4 transcript Gate-0 U/C-30 `d20181116-s10-r1` w08/w17 (2020/2284/2035/2146 khung, `STAGE1B-PLAN.md:160-167`), thêm V-30 và M+ của T3.8, qua `replay_runner.py` với `wp4-tier3-v2.json` | nếu HEAD không khớp: T3.8 chỉ tái lập từ `45f8026`. (Câu "không đổi byte" của ADR-090 đã được thu hẹp ngày 04/11 về đúng phạm vi harness.) |
| N2 | Vector cố định cho `CalculateDecisionHashLight`; overload parts bằng vector đã công bố; `decisionHash` light và `checkpointHash` ở cấp phiên (kịch bản `Wp4RunnerIntegrationTests.cs:897-966`) | |
| N3 | Golden `RunnerSession` có bật `shadowAllocation` + `planningForecast`, chạy cả `full-v1` lẫn `light-v1`, tắt bằng chứng, có quyết định + checkpoint + restore; gọi tới `ShadowAllocator.Allocate` | |
| N4 | Phát lại từng khung, tính cả hash, cho một job G3a 60 nút và một job G3b 824 nút, giới hạn dòng tới 1 GiB | chưa rõ transcript còn không; nếu mất thì chạy lại và so với biên lai từng khung |
| N5 | Golden B1/C2 từ `wp4-rolling-cost-boundary-v1` và `wp9-…-soft-hard-hybrid-audited-v1` | |
| R1 | Normalizer. Mức A (mỗi commit): round-trip cả 86 ô, dựng lại payload bằng `EventBatchPayloadCodec`→`CanonicalJson` rồi so `payloadCanonicalJsonHex`; ghim hash nguồn `34517a83…`. Mức B (có cache Zenodo): `Wp6Normalize --grid` cho `d20181114-s10-r1` (wp9-v2), `d20181112-s10-r1-w08` (wp14) và một ô cầu nối, kết quả phải là `reusedExactDerivative=true` | |
| Khác | Smoke RidePy (dựng `RideBoundRidePySession` mà không chạy tiến trình); test vi sai giữa writer có cache và `CanonicalizeReference` (`docs/18:5714-5715`); test `Rank` so với `RankComparer`; chuyển test bằng-nhau inventory về test chính; test decode mọi cấu hình đã track (`ContentHash` + giá trị); biến thể `light-v1` của test checkpoint stream S2 | |

**Bước 0.7: Làm cho test đỏ trở nên trung thực.**
- `benchmarks/scenarios/wp13-e1/source-divergence-v1.json` hiện chỉ khai báo 1/8 tệp bị trôi. Cần khai báo đủ và ghi nguyên nhân từng tệp: nội dung đổi (commit khôi phục) hay chỉ đổi EOL.
- Thêm khai báo tương tự cho WP14 và WP9.
- Sửa `StrictBagItBundleTests.cs:32`.
- Kiểm bằng K1 + K6. Mục tiêu: chỉ còn các lỗi đã khai báo.

### Giai đoạn 1: bỏ mã chết thật sự (a)

**1.1 Nhánh downgrade trong Runner**
- **Tệp:** `CapabilityNegotiator.cs:11-19,96-126`; test `CapabilityNegotiatorTests.cs:92`.
- **Thay đổi:** bỏ `CapabilityDowngradeProfile` và nhánh thử lại. Giữ từ vựng downgrade trong Contracts.
- **Lý do:** `RunnerDefaults` không đặt `Downgrade`; adapter chỉ nhận `accepted`.
- **Giảm:** khoảng 45 dòng, 1 khái niệm. **Rủi ro:** thấp.
- **Kiểm:** K1, K3 (khung `helloAck`, `initialized`).
- **Dừng nếu:** có khung nào khác.

**1.2 `HashTiming`**
- **Tệp:** `HashTiming.cs`; tham số ref luồn qua `RunnerSession.cs:484-488,565-569,612,635,653-657,1010-1035,1059-1063`.
- **Giảm:** khoảng 100 dòng, gỡ một tham số khỏi đường nóng. **Rủi ro:** thấp.
- **Kiểm:** K1, K2, K3.
- **Dừng nếu:** có byte nào khác.

**1.3 Tùy chọn cache không bật được**
- **Tệp:** `OnlineStateCanonicalWriter.cs:25-30,40,46-48,86-90,206-268`; `InternalsVisibleTo.cs:4`.
- **Giữ:** cache mảnh theo thực thể.
- **Giảm:** khoảng 60 dòng. **Rủi ro:** thấp.
- **Kiểm:** K1 (chạy cả với `RIDEBOUND_CANONICAL_SELFCHECK=1`), K2, K3.

**1.4 Marker và dự án Infrastructure**
- **Tệp:** `src/RideBound.Infrastructure/*`, `RideBound.slnx:9`, `RideBound.Runner.csproj:7`, `DependencyRuleTests.cs:20-29`, `AssemblyReference.cs` ở 6 dự án (giữ bản của Domain).
- **Giảm:** 1 dự án, khoảng 50 dòng. **Rủi ro:** thấp; binary mới sẽ có inventory mới.
- **Kiểm:** K1 (build với warnings-as-errors); so danh sách tệp publish (chỉ khác `Infrastructure.dll/.pdb` và `deps.json`); K3; K8 trên một job.
- **Dừng nếu:** publish khác ở tệp nào khác.

**1.5 Helper không dùng trong Domain**
- **Tệp:** `RoutePlan.cs:47-50,79-88,170`; `WaitingRiderRelocation.cs:19-45`; `ServiceQualityAllowance.cs:143`.
- **Viết lại test:** `WaitingRiderRelocationPhysicalTests.cs:107-125` thành test cho `FromChange`; `RoutePlanTests.cs:120-140` thành test cho `RemainingStops`.
- **Giảm:** khoảng 45 dòng. **Rủi ro:** thấp.
- **Kiểm:** K1, K3.
- **Dừng nếu:** test viết lại không đỏ khi cố tình làm hỏng.

**1.6 Mã chết trong Contracts**
- **Tệp:** `CheckpointMessages.cs:398-434` (`Decode`); `CanonicalUnits.cs` + `CanonicalUnitTests`; `ProtocolVersionCompatibility.cs:19-34,40-41,55-56,72-88` + 2 test viết lại.
- **Giảm:** khoảng 320 dòng, 3 khái niệm. **Rủi ro:** thấp.
- **Kiểm:** K1, K3.
- **Ghi chú:** số đếm test WP1 trong `docs/19:327,359` sẽ lệch.

**1.7 Tham số `collectAllWitnesses`**
- **Tệp:** `CommitmentCandidateFilter.cs:17-41,88`.
- **Giảm:** khoảng 10 dòng. **Rủi ro:** thấp.
- **Kiểm:** K1 (gồm `CommitmentCheckpointProcessTests`), K3.

**1.8 Adapter: `stop_id` và `.pyc`**
- **Tệp:** `mapping.py:188,335-339`; bỏ track 41 + 4 tệp `.pyc`.
- **Giảm:** khoảng 10 dòng + 45 tệp. **Rủi ro:** thấp.
- **Kiểm:** K6.
- **Ghi chú:** hash kho mã của các lượt chạy sau sẽ đổi. Tính lại hash mong đợi trước khi chạy job mới, và không làm việc này khi đang có job chạy.

### Giai đoạn 2: bỏ cấu hình và nhánh không cần

**2.1 Nguyên mẫu hash v2**
- **Tệp:** `ProtocolHash.cs:88-108`; `OnlineStateHashV2.cs`; các nhánh v2 trong `RunnerSession.cs:102-108,151-157,622-634,765-787,877-881,1024-1029,1054`; `RunnerSessionAuditHashTests.cs`; `OnlineStateCheckpointCodecTests.cs:440-442`; sửa `Light.cs` của harness.
- **Phụ thuộc:** 0.3, 0.4 (`RIDEBOUND_STATE_HASH` không được đặt ở đâu).
- **Giảm:** khoảng 300 dòng; 5 chỗ rẽ ba nhánh còn hai.
- **Rủi ro:** thấp.
- **Kiểm:** K1, K2, K3, K4, N2.
- **Dừng nếu:** byte v1 hoặc light đổi, hoặc grep thấy biến môi trường được đặt.

**2.2 B5 multiple-plan**
- **Tệp:**
  - `MultiplePlanPolicy.cs`, `VersionedPlanPool.cs`, `OnlineState.PlanPool`;
  - `RunnerSession.cs:84,167-168,1114-1147,1336-1337,1347-1348`;
  - writer `:118-122,434-482`, canonicalizer `:169-173,356-395`, codec `:114-118,273-295,543-582,702-767`;
  - `Wp4RunnerConfiguration.cs:145-148,625-642,907-916`; giá trị enum `LeastCommitmentConsensus`.
- **Giữ:** các hàm static của `RollingCostPolicy`, `CommitmentCandidateFilter`, provider `RevisionPenalty`, lớp ghép đôi `wp4-multiple-plan-v1` trong schema WP6 đã đóng băng.
- **Phụ thuộc:** 0.4.
- **Giảm:** khoảng 1.500 dòng + 800 dòng test; 1 chính sách; 1 thành phần state.
- **Rủi ro:** TB.
- **Kiểm:** K1, K2, K3, K4; round-trip checkpoint của một job mẫu.
- **Dừng nếu:** khung K3 nào khác, hoặc grep thấy B5.

**2.3 B2, B3, B4**
- **Tệp:**
  - `CommitmentMechanismPolicies.cs:9-68,106-218`; `RevisionPenaltyFleetSelector.cs`;
  - `CommitmentCandidateAssessor.cs:57-79,121-284`, nhưng **giữ** `CommitmentAssessmentWitness` (`:131-142`) và `CommitmentMechanismContext` (`:11-20`);
  - `SolverBackedRidePoolingPolicy.cs:139-151,297-301,324-353`; `SolverBackedFleetSelection.cs:269-281,335`;
  - repair seeds `WaitingIncumbentRepairSeedBuilder.cs:25-128` (dời `HasExactMutablePair` sang `WaitingRiderRelocationBuilder`); gốc repair trong `InsertionCandidateGenerator.cs:405-422,490-506,641-670`;
  - khóa `fixedFreeze`, `repair`.
- **Giữ:**
  - chuỗi `"no-repair"` trong `ExplorationNode.StableId` (`:1245-1250`);
  - 3 bộ đếm repair (luôn 0) trong bằng chứng (`SolverExecutionEvidenceMapper.cs:396-404`);
  - `Finish`, `GenerationFailure` (dời đi);
  - provider `RevisionPenalty` (B1 dùng);
  - khóa commitment `freezeHorizon*`.
- **Phụ thuộc:** 0.4, N1, N5.
- **Giảm:** khoảng 800 dòng + khoảng 20 test; 3 chính sách.
- **Rủi ro:** TB.
- **Kiểm:** K1, K2, K3, K4, N1, N5.
- **Dừng nếu:** byte bằng chứng hoặc thứ tự tìm kiếm đổi.

**2.4 origin-hold**
- **Tệp:** `OriginHoldCandidateTransformer.cs`; `InsertionCandidateGenerator.cs:1082-1211`; `CandidateIdentity.cs:23-24,123-127`.
- **Giữ:** giá trị `earliest-feasible`; chuỗi `"earliestFeasible"` và `relocatedWaitMs: 0` trong bằng chứng; `CertifiedDelayAtRouteStartMilliseconds`; `RouteStopKind.Waypoint`.
- **Test:** viết lại `ShadowAllocationConfigurationTests.cs:141-144` để kiểm chiến lược lạ bị từ chối.
- **Giảm:** khoảng 250 dòng. **Rủi ro:** thấp.
- **Kiểm:** K1, K2, K3, K6.
- **Dừng nếu:** hash của một cấu hình đã ghi đổi.

**2.5 Dạng CLI 6 tham số**
- **Tệp:** `Program.cs:35-50`.
- **Giữ:** `solverExecution.randomSeed` là khóa bắt buộc.
- **Phụ thuộc:** 2.2.
- **Giảm:** khoảng 20 dòng. **Rủi ro:** TB (transcript trước ngày 17/08 nếu có).
- **Kiểm:** K1, K3.

**2.6 (Tùy chọn, cần ADR giao thức) Từ vựng v1 không ai phát**
- **Gồm:** `finalizeRun`, `runSummary`, 3 ID năng lực, `offerProposed`, `commitmentBreachDeclared`, `downgraded`.
- **Sửa kèm:** schema, `docs/06`, `docs/19`. Giữ metric `BreachCount` (bằng 0) chừng nào `MechanicalMetricCalculator` còn.
- **Giảm:** khoảng 200 dòng. **Rủi ro:** thấp với kết quả.
- **Kiểm:** K1 (`SchemaAssetTests`), K3 (`helloAck` vẫn ghi `"accepted"`).

**2.7 (Sau khi grep E:) `maximumRidersPerVehicle`**
- **Tệp:** `WaitingRiderRelocationBuilder.cs:56-58`; cấu hình `:116-118,715-721`.
- **Giữ:** giới hạn số xe đích được phép null.
- **Kiểm:** K1, K3 (các job G2).

### Giai đoạn 3: gộp trùng lặp

**3.1 Helper đọc cấu hình**
- **Gộp:** ba bộ helper + parse thập phân + tra chiều thành một.
- **Phụ thuộc:** test decode cấu hình ở 0.6.
- **Giảm:** khoảng 200 dòng; 3 bộ còn 1.
- **Rủi ro:** TB (chữ trong thông báo lỗi có thể đổi; sửa test theo).
- **Kiểm:** decode cả 23 cấu hình WP4 + 48 cấu hình commitment + cấu hình final-v2, `ContentHash` và giá trị giải mã phải giống hệt; K1; K2.
- **Dừng nếu:** có cấu hình đã ghi không parse được, hoặc hash đổi.

**3.2 Helper hash**
- **Gộp:** một hằng domain `OnlineStateHash.v1`; một helper state identity; một helper khung hash dùng chung cho `ProtocolHash` và `CheckpointMessages`; một helper `Sha256Hex`.
- **Không đụng:** các bản Python của bộ kiểm độc lập.
- **Phụ thuộc:** N2.
- **Giảm:** khoảng 120 dòng. **Rủi ro:** TB–cao.
- **Kiểm:** K1 (vector `ProtocolHashTests`, `CheckpointStreamingHashTests`), N2, K2, K3, K4.

**3.3 `CreateSolverPolicyOptionsForRun`**
- **Thay đổi:** dùng `with { … }` thay cho sao chép từng trường.
- **Lý do:** bỏ rủi ro tùy chọn mới bị âm thầm đánh rơi.
- **Giảm:** khoảng 30 dòng.
- **Kiểm:** K1, K2, K3.

**3.4 Domain**
- **Gộp:** ánh xạ lifecycle → phase; helper chọn cơ sở ngân sách (giữ `customerVisible`).
- **Giảm:** khoảng 40 dòng.
- **Kiểm:** K1, K3, N1 (có V-30).

**3.5 Overload span của `ProtocolHash`**
- **Thay đổi:** biến thành wrapper gọi đường parts.
- **Phụ thuộc:** N2.
- **Giảm:** khoảng 35 dòng.
- **Kiểm:** K1 (vector đã công bố), K3.

**3.6 Helper Python trong package**
- **Gộp:** `_strict_object`, sha256 tệp, `_fail`. Không đổi tên mà RidePy import.
- **Giảm:** khoảng 60 dòng.
- **Kiểm:** K6 (gồm smoke RidePy), K8 (một job INS + một job G2).

**3.7 Hai hàm twins chặng khóa**
- **Tệp:** `fleet_control.py:1011-1106`.
- **Giữ:** tên `_build_VRLs`, `_leg_signature`, `_leg_equivalent`, `assign_vehicle_plan`, và thứ tự các lần return sớm.
- **Giảm:** khoảng 30 dòng. **Rủi ro:** cao.
- **Kiểm:** 8 test `test_in_progress_*`; K6; K8 (INS + G2); chạy lại tập 108 job G2 cầu nối (`compare/fix1-2026-10-24/condition3.json`) hoặc ít nhất 3 job lỗi lịch sử; một ô G3b G2 (K5).

### Giai đoạn 4: đơn giản hóa các phương thức phức tạp

**4.1 Checkpoint `light-v1` chỉ build một lần**
- **Tệp:** `RunnerSession.cs:761-774`.
- **Thay đổi:** ở light, build một lần với cache null, nhưng vẫn gọi `OnlineStateCanonicalizer.Verify`. Giữ phép so cache-với-không-cache ở full-v1.
- **Phụ thuộc:** N2, N3, N4.
- **Giảm:** một lần build 0.4–0.8 GB mỗi job.
- **Kiểm:** K1 (test S2 bản light), K4, N2, N3, N4; đo RAM đỉnh bằng `replay_runner_big.py`.
- **Dừng nếu:** `checkpointHash` khác, hoặc RAM tăng.

**4.2 `RevisionReasonCode`**
- **Thay đổi:** hằng `"ONLINE_REPLAN"` tại `CommitmentDecisionValidator.cs:609`; bỏ tham số ở 5 chỗ gọi.
- **Giảm:** khoảng 15 dòng.
- **Kiểm:** K1, K3, K4.
- **Dừng nếu:** `reasonCode` nào trong sổ đổi.

**4.3 (Tùy chọn) Bỏ phần số học vector dưới cost-first**
- **Tệp:** `HardVectorCandidateAssessor.cs:196-245,871-980`; `SolverBackedFleetSelection.cs:487-512`.
- **Giữ:** tra policy/sổ, kiểm giới hạn 0 và vượt giới hạn (dạng assert rẻ). Cố ý sửa `SolverBackedFleetSelectionTests.cs:590-592`.
- **Phụ thuộc:** N1, N5.
- **Rủi ro:** TB.
- **Kiểm:** K1, K2, K3, K4, N1, N5.
- **Dừng nếu:** mất một kiểm fail-closed, hoặc byte đổi.

**4.4 (Tùy chọn, ưu tiên thấp) Hiệu năng validator**
- **Thay đổi:** cache lịch của xe cũ theo từng xe (`CommitmentDecisionValidator.cs:355-361`); encode quyết định một lần.
- **Kiểm:** K1, K3, K4.

**Không khuyến nghị:** tách `NoWorseReference` ra riêng (`:776-830`). Lợi nhỏ, chạm đường T3.8 V-30 và các job p1.

### Giai đoạn 5: giảm trừu tượng

**5.1 Dời oracle không solver vào dự án test**
- **Tệp:** `HardVectorPolicy.cs`, `SoftHardHybridPolicy.cs` → `RideBound.Algorithms.Tests`. Trước đó dời `Finish`/`GenerationFailure` sang một helper trung lập.
- **Giữ:** đường `requireSafetyNoOp=false`.
- **Giảm:** khoảng 590 dòng khỏi assembly sản phẩm. **Rủi ro:** thấp.
- **Kiểm:** K1 (cùng test, đổi vị trí), K3.

**5.2 `SelectionApplier` trung lập**
- **Thay đổi:** dời nguyên văn `ValidateSelection`, `ApplySelection`, `SelectedRelocation` ra khỏi `RollingCostPolicy`. Giữ `Decide` tham lam.
- **Kiểm:** K1 (gồm `Wp2TinyDemoTests`), K2, K3.

**5.3 Mặc định chế độ**
- **Thay đổi:** ghi rõ chế độ tại chỗ gọi, không đổi chế độ mà mỗi lời gọi đang nhận. Sửa `docs/08:46`.
- **Kiểm:** K1.

### Giai đoạn 6: đặt tên và tổ chức

**6.1 Đổi tên `RollingCost*`**
- **Tệp:** `RollingCostDecisionModels.cs`.
- **Thay đổi:** đổi định danh C# sang tên trung lập. Giữ nguyên mọi chuỗi (`:321-340`), id registry, chuỗi profile.
- **Kiểm:** K1, K3, N5.

**6.2 Tách G2 ra khỏi `InsertionCandidateGenerator`**
- **Thay đổi:** `GenerateRelocations` + `RelocationGenerationResult` (khoảng 300 dòng) sang một lớp riêng; chỉ dời, không đổi logic.
- **Kiểm:** K1, K3 (122 INS + 106 G2), K4, K5 (G3b, forecast, Shadow).

**6.3 Tài liệu (không đổi mã)**
- Từ vựng INS/G2 và 5 thiết lập.
- Baseline README adapter (467/6).
- Con trỏ trong `START-HERE.md`.
- Danh sách tên private bị vá từ bên ngoài.
- Ghi "không đổi ID WP7".
- Emulator là "một module, 6 bản niêm phong".
- Ghi chú về `wf20/fullsys/code/RUNBOOK.sh`.
- **Không sửa** `BAN-GIAO-MAY-CONG-TY.md` và `ghi-chu-may-nha/*` (đã bị niêm phong).

### Giai đoạn 7: đơn giản hóa luồng chính

**7.1 (Có điều kiện) CLI chỉ còn một dạng**
- **Chuẩn bị:** chuyển các test sau sang dạng 8 tham số WP4:
  - test line-budget CLI (dùng khung lớn hơn 64 MiB);
  - test S2 (`full-v1` + `light-v1`);
  - test restore-suffix;
  - `ExternalProcessSupervisorTests`.
- **Điều kiện:** chủ chấp nhận demo WP2/WP3 chỉ tái lập được từ tag.
- **Bỏ:** `--mode online`, dạng 4 tham số. Có thể giữ conformance cho 21 test, hoặc chuyển các test đó đi.
- **Giảm:** khoảng 100 dòng, 2 chế độ. **Rủi ro:** TB.
- **Kiểm:** K1, K3, K6.
- **Dừng nếu:** test đã chuyển không đỏ khi cố tình làm hỏng.

**7.2 Preflight đọc giới hạn dòng và timeout từ driver**
- **Thay đổi:** thêm khóa driver tùy chọn; mặc định bằng hành vi hiện nay. Chỉ bỏ vá cho job **mới**.
- **Giữ:** hằng module và tên private (launcher cũ phải còn chạy).
- **Kiểm:** unit test (driver cũ cho ra dict y hệt); K6; một job ô đầy đủ dùng cài đặt mới, so K8 với job dùng vá.

### Giai đoạn 8: bỏ mã hỗ trợ lỗi thời và test (làm cuối)

**8.1 Khối WP6**
- **Gồm:** Planning, Execution, Storage, metric + `Wp6MetricOracle` (cùng `MechanicalMetricCalculator`), Claims, Bundles, EndToEnd, các tool `Wp6Tiny/Medium/BundleVerify/FakeChild/SeedVectors`, phần run-side của Contracts và schema/fixture, phần run-side của test.
- **Giữ:** normalizer + toàn đường byte của nó (đóng băng), `Datasets`, `Wp6Normalize`, test data-side, test tất định của `Wp6SourceAuditTests`.
- **Phụ thuộc:** R1.
- **Giảm:** khoảng 13k dòng sản phẩm + 2,3k tool + 5,5k test.
- **Mất:** chạy lại đóng WP6 (tiny A, medium H/I) từ HEAD; 2 test quy trình đang chạy Runner mới nhất.
- **Kiểm:** K1 (ghi lại số test mới), R1, K3.
- **Dừng nếu:** R1 đỏ, hoặc lỡ chạm vào đường byte của normalizer.

**8.2 Mảnh H6 của C#**
- **Gồm:** `ExperimentalUnitModels.cs`, `DecisionInducedBurdenCalculator.cs`, `DecisionInducedBurdenOracle.cs`.
- **Điều kiện:** đã có khai báo phân kỳ WP9 (0.7). Số H6 do Python tính.
- **Kiểm:** K1, K6.

**8.3 Họ script Python WP13, WP14, WP14R**
- **Tag:** `38d517d` và `41a4e11`.
- **Tách giữ lại:** `test_wp13_runner_retained_candidate_portfolio_schema.py` + schema của nó; test inventory đã chuyển chỗ; `benchmarks/configurations/wp14-*`; `wp14-development-panel-v1`; biên lai.
- **WP9:** giữ bộ phân tích nếu H6 còn được trích; nếu không thì lưu trữ cả họ (vì `wp9_run_matrix.py` là phụ thuộc).
- **Phụ thuộc:** 0.4 (grep `wp14r_`).
- **Giảm:** khoảng 25–35k dòng.
- **Kiểm:** K6 (mục tiêu 0 lỗi đã biết), K1.

**8.4 WP7**
- **Gồm:** 4 preflight WP7, restart/restore của adapter, `acknowledge_and_checkpoint`, snapshot thưa (mặc định đổi thành báo lỗi).
- **Viết lại:** `test_runner_client.py:62-73,109-120` dùng `acknowledge()` + `request_checkpoint()`; sửa `test_session_line_bounds.py:62-74`.
- **Giữ:** `wp7_tiny.csv`, `wp7-fleetpy-medium/`, `_rb_travel_dirty`, `_rb_travel_arc_cache`, trường `travelDirty` trong binding hash.
- **Mất:** chạy lại WP7-014 từ HEAD; mất kiểm end-to-end cho đường hủy/từ chối.
- **Kiểm:** K6, K8 (một job medium).

**8.5 Tool và script lẻ**
- **Gồm:** `CandidateHotPathBenchmark` (sửa `docs/19:747`), `Wp4Microbenchmark` + script + `scripts/README.md`, `create_post_wp10_review_pdf.py` (giữ PDF), 2 script demo `.ps1` (giữ kịch bản và cấu hình).
- **Kiểm:** K1, build.

**8.6 Bản trùng trong kit**
- **Gồm:** patch G2 v3/v4/fix (công thức `git diff 1190518 c6588f8`, `1190518 ef2b02f`, `c6588f8 ef2b02f`); 3 bản sao byte trong bridge-phase1 (sha `0022a9e0`, `490db8b6`, `54e30a53`); `wp8-relaxed-stopswitch`; `wp8-drop-eta-budget-medium` (chỉ sau khi harness đã chuyển sang tệp có byte giống hệt).
- **Kiểm:** công thức tái tạo phải cho đúng sha; K2.

**8.7 (Không khuyến nghị) Bỏ retained-portfolio, `skipConstantObjectiveLevels`, `CollectAllCommitmentWitnesses`**
- **Lý do không khuyến nghị:** harness ADR-090 mất các cặp WP13/WP14; WP13/WP14 chỉ còn tái lập được từ tag.

**Không làm.** Không bỏ stability-first, B1, C2, `customerVisible`, M+, `hardLimit` budget, trần từng lần, forced-reference, incident, restore phía Runner, conformance (nếu chưa chuyển test). Không đổi tên ID WP7, mặc định cấu hình, chuỗi lỗi hay domain hash.

---

## 6. Kiến trúc tối giản đề xuất

### 6.1 Các khối

```
FleetPy (xe thật, đồng hồ sự kiện)         actual_fleetpy_medium_preflight.py
  │ callback: yêu cầu, tới điểm, lên/xuống xe, đổi thời gian đi
  ▼
Adapter Python                              ridebound_fleetpy/{fleet_control, protocol,
  │ eventBatch NDJSON (ID mờ, ms)            mapping, runner_client, transcript,
  ▼                                          relocation, direct_network}
Runner .NET (1 tiến trình)                  RunnerHost → RunnerSession
  ├ Giao thức, hash, checkpoint             Contracts; Runner/Protocol; Runner/Online
  ├ Trạng thái, giảm sự kiện                Domain; Application/State
  ├ Sinh ứng viên, cổng cam kết             Algorithms/Candidates; Algorithms/Commitments
  ├ Chọn tối ưu (CP-SAT từng mức)           Application/Optimization; Solvers.OrTools
  └ Kiểm lại, sổ lời hứa, Shadow            Application/Commitments; Algorithms/Shareability
  │ decision (kế hoạch xe, nhận/từ chối, lời hứa, decisionHash)
  ▼
Adapter: FleetPy kiểm kế hoạch → gán → decisionApplied (ACK theo hash)
```

### 6.2 Luồng chính, từng bước

**Khởi tạo**
1. Launcher và world wrapper nạp preflight.
2. Preflight dựng mạng FleetPy từ snapshot đầu và kiểm từng cung. Với `complete-graph-direct-v1`, kiểm thêm closure bằng Floyd–Warshall (`:158-171`).
3. Preflight tạo xe kiểu `wp7_tiny` (4 chỗ) và nạp `RideBoundFleetControl` qua `dev/misc/init_modules.py`.
4. `RideBoundSessionSettings.from_attributes` đọc cài đặt `ridebound_*`, tính binding hash WP4, đọc cờ G2/forecast/late boarding, rồi băm artifact.
5. Adapter khởi động Runner (`session.py:426-439`).
6. Bắt tay: `hello` → `helloAck` (`directedEdgeProgress`).
7. Adapter gửi `initializeRun` (seed, `policyConfigurationHash`, hash kịch bản, `coreCommitSha`, `binarySha256`). Runner kiểm binding và trả `manifestHash`.

**Mỗi lần trigger**
1. Callback FleetPy đi vào `OrderedEventBuffer` theo pha, có lọc trùng.
2. Adapter thêm `travelTimesUpdated` (n×n, có phiên bản) nếu thời gian đi đổi, và `vehicleAdvanced` cho xe vừa động. Rồi gửi `eventBatch`.
3. Runner kiểm thứ tự epoch/seq/thời gian, rồi `OnlineEventMapper` → `EventReductionCoordinator.Propose` → `EventReducer`. Nếu là `record-v1`, khách lên xe trễ được ghi lại chứ không bị từ chối.
4. Nếu có `planningForecast`, Runner gắn `TravelTimeForecast` vào snapshot (`RunnerSession.cs:970-992`).
5. `SolverBackedRidePoolingPolicy` làm sáu việc:
   1. Với mỗi xe: tạo no-op; probe chất lượng dịch vụ ngoại sinh; chèn best-first tối đa 2 khách chờ trong 10.000 đơn vị công; kiểm vật lý; lưu giữ 100 ứng viên. Nếu G2: thêm cặp donor/receiver.
   2. `HardVectorCandidateAssessor`: áp từng ứng viên rồi gọi `CommitmentDecisionValidator` trong phạm vi xe đó (khóa, hạn chót, ngân sách). Nếu no-op bị loại, tạo phương án forced hoặc no-worse.
   3. Dựng `CandidateSelectionProblem`: mỗi xe đúng 1 phương án; mỗi yêu cầu tối đa 1 xe; tối đa 1 lần dời G2.
   4. OR-Tools giải theo từng mức: số forced ↓, số khách nhận ↑, chi phí vận hành ↓, số lần dời ↓, rồi hạng ID ứng viên của từng xe.
   5. Kiểm toàn đội; nếu hỏng thì dùng no-op hoặc fallback tham lam.
   6. `Finish` tạo trạng thái đề xuất và các hành động.
6. Runner kiểm lần cuối bằng `CommitmentDecisionValidator`. Bước này ghi lời hứa vào `CommitmentLedger` với ID tất định và ghi breach forced.
7. `ExogenousServiceQualityBreachBridge` ghi các vi phạm do giao thông.
8. `ShadowAllocator` đóng băng lớp Shadow cho khách vừa có lời hứa đầu.
9. Runner hash trạng thái staged, dựng `decision` và nối chuỗi `decisionHash`.
10. Adapter `parse_decision` rồi, với từng xe: dựng `VehiclePlan`, để FleetPy kiểm khả thi, gán không ép, kiểm chữ ký chặng. Nếu G2: suy ra lần dời và kiểm nó.
11. Adapter gửi `decisionApplied` kèm hash. Runner chỉ commit khi hash khớp. Có thể có thêm một batch quan sát; khi đó lặp lại từ bước 3.

**Kết thúc**
1. Preflight xả hàng đợi và kiểm bảo toàn số yêu cầu.
2. `checkpoint_runner()` khiến Runner stream checkpoint toàn trạng thái. Adapter tính `checkpointBindingHash`.
3. Preflight ghi `run-NN.json`, `summary.json` (`semanticHash`, `repositoryInventorySha256`), `bundle-manifest.json`, và kiểm artifact cùng inventory không trôi.
4. Ngoài repo, `finalize_job.py` rút sổ cuối ra để đo.

### 6.3 Lõi để tự viết lại

```text
on eventBatch(batch):
    require ordered(batch)                         # epoch, seq, time liền mạch
    S = reduce(S_committed, batch)                 # lên xe trễ: ghi lại khi record-v1
    T = S.travel.withForecast(profile) if CL else S.travel
    for v in vehicles:
        C[v] = [keep(v)] + bestFirstInsert(v, pending, max_new=2, work=10000)
        C[v] = retain(C[v], cap=100)               # service-set-stability-portfolio-v1
        if G2: C[v] += relocationPairs(v, k=8)     # donor bỏ khách chờ, receiver chèn khách đó
        C[v] = [c for c in C[v] if gate(c)]        # khóa + hạn chót + luật C + Shadow
        if keep(v) bị loại: C[v] = recover(v)      # forced-reference hoặc no-worse
    pick = lexicographic_cp_sat(C)                 # forced↓, accepted↑, cost↓, reloc↓, id-rank
    D = validate_fleet(pick) or fallback()
    ledger.append(publications(D)); bridge_exogenous(); shadow.classify(new_riders)
    h = hash(S_staged) if full-v1 else hash(epoch, seq, time)
    send decision(D, h); await decisionApplied(h); commit

gate(c), với mỗi khách r bị c chạm tới:
    B_r = round(permille/1000 * max(0, pickup0 + maxRide - drop0))   # cố định ở lời hứa đầu
    if Shadow: B_r = 0 nếu r "guaranteed" (score ≤ θ), ngược lại = toàn bộ slack
    strict  (EO, SHF):  từ chối nếu drop(c) > drop0 + B_r
    relaxed (EOHZ, SHHZ): từ chối nếu drop(c) > max(drop0 + B_r, drop(keep))
    rule C  (RCE, SHRC): từ chối nếu L_r + abs(Δ_decision drop(c)) > B_r
```

Bản viết lại phải giữ **chính xác**:
- định danh ứng viên (SHA-256, quyết định tie-break; emulator nhân bản ở `identity.js`);
- canonical JSON, các domain hash và thứ tự sự kiện;
- cách làm tròn `B_r` và forecast;
- 10 chiều của vector cam kết trong state;
- các mức mục tiêu và hạng theo từng xe.

Nếu chỉ đạt K5 (khớp từng khách) mà không đạt K3 (khớp từng byte), bản viết lại vẫn kiểm chứng được emulator, nhưng không còn tái lập từng byte các transcript đã ghi.

### 6.4 Shadow, luật C, forecast và rào mềm khớp với nhau thế nào

| Nhánh | Hạn chót (`CommitmentLockEvaluator`) | Ngân sách (`CommitmentBudgetEvaluator`) | Shadow (`ShadowAllocator`) | Forecast |
|---|---|---|---|---|
| SHRC71+CL (hệ của mình) | không | luật C trên chiều `drop_eta_total_ms`, cơ sở `decisionInduced` | quyết B_r là 0 hay toàn bộ slack | có |
| SHHZ71+N (đối thủ) | rào mềm | không | có | không |
| SHF | rào chặt + forced-reference | không | có | (N, tham chiếu) |

Cách các cơ chế này phối hợp:
- **Thứ tự:** Shadow chạy **sau** lần kiểm cuối. Lớp Shadow của một khách được đặt một lần, khi khách có lời hứa đầu (`CommitmentLedger.cs:159-210`). Mọi quyết định sau đó đọc lớp này qua `CommitmentPolicy.DropEtaTotalLimitFor` hoặc `DeadlineSlackFor`.
- **Luật C:** sổ chỉ tính phần thay đổi **do quyết định gây ra** (exogenous được tách riêng bằng delta ba chiều). Vì vậy tuyến giữ nguyên tốn 0 ngân sách, nên luật C không sinh breach forced.
- **Rào mềm:** dùng `e` là giờ trả nếu giữ tuyến cũ. Lệch do giao thông không bao giờ làm loại no-op.
- **Forecast:** chỉ đổi phép **dự chiếu** của Runner: bộ sinh, validator, mốc `e`, và phần Δ mà luật C đo. Thời gian vật lý vẫn do FleetPy quyết định. Adapter bỏ kiểm cửa sổ đón và chuyến tối đa của FleetPy, vì Runner đã sở hữu các giới hạn đó (`fleet_control.py:1119-1161`); điều này buộc phải dùng `record-v1`.
- **Khác biệt chưa có đối ứng:** `clipPickup` của emulator chưa có trong C#.

### 6.5 Audit và checkpoint

- **`full-v1`:** `stateHash` là hash toàn bộ state chuẩn hóa mỗi quyết định. `decisionHash` buộc cả `onlineStateBefore`. Dùng cho job mẫu, G1 và mọi transcript trước ADR-089.
- **`light-v1`:** `stateHash` chỉ là hash danh tính (epoch, seq, thời gian). Decision hash dùng domain `light.v1`. Nhanh hơn rõ rệt (81.6 xuống 9.1 phút mỗi job). Dùng cho 168 job.
- **Checkpoint:** cả hai chế độ đều ghi toàn bộ state, mỗi job đúng một lần ở cuối, dạng stream (0.4–0.8 GB). Lượt chạy cuối không bao giờ restore. Restore chỉ để kiểm chứng (harness ADR-090) và cho WP3/WP7.
- **Kết luận:** giữ cả hai chế độ. Khóa vắng luôn nghĩa là `full-v1`.

### 6.6 Chế độ chạy phải giữ

- **(c):**
  - commitment + WP4 + manifest seed;
  - INS và G2K8;
  - `full-v1` và `light-v1`;
  - CL và N;
  - có và không có Shadow;
  - `network-basic-v1` và `complete-graph-direct-v1`;
  - transcript `none` và `gzip-v1`;
  - bằng chứng solver bật và tắt;
  - no-worse và forced-reference.
- **(b):**
  - stability-first, B1, C2, `customerVisible`, M+, trần cố định, trần từng lần, hai chiều, freeze horizon;
  - các chế độ online/conformance/không WP4 (cho test);
  - restore;
  - selfcheck.

### 6.7 Thành phần thiết yếu theo từng kết quả

| Kết quả | Cần |
|---|---|
| Con số luận văn chính (Via) | emulator niêm phong + dữ liệu SP ngoài repo + `confirm_digest.js` |
| Kiểm chứng C# ↔ emulator (Via) | lõi Runner (c) + adapter + preflight + world wrapper + dự đoán đã niêm phong + engine clint (ngoài repo) |
| G1/G2 | lõi (c) + mọi khóa (b) của 228 job + kit via-bridge/bridge-phase1 |
| Số Manhattan trong `7f93ee6` | `runner-tier3-v2` (`45f8026`), WP14 `runner-v1`, `dce84ec` + normalizer đóng băng + `fixtures/wp6/public` + harness tang3 |
| 168 job | Runner trong pack + .NET 10.0.9 + cấu hình trong pack |

---

## 7. Điều chưa chắc và cần chủ quyết

| Câu hỏi | Vì sao quan trọng | Cách kiểm |
|---|---|---|
| Bản nộp là `7f93ee6` hay `34920ee`? | Quyết định stability-first, `customerVisible`, M+, B1, normalizer là (b)-LV hay chỉ là (b) | **Chủ đã trả lời ngày 04/11: chỉ dùng Via, như bản `34920ee`.** Vì vậy các mục trên là (b), không còn là (b)-LV. Riêng bảng cầu nối (238 cặp Manhattan, cost-first) vẫn được trích làm kiểm chứng; nó được G1 canh |
| HEAD có tái lập từng byte T3.8/Stage 1b không? | Nếu không, các số đó chỉ tái lập từ tag | chạy N1 |
| Byte cấu hình final-v2: recovery của SH*, `auditHash` ghi rõ hay để vắng, ngân sách ô đầy đủ, bằng chứng của 168 job, `stopDistances`, `maximumRidersPerVehicle`, input Shadow/forecast từng ô | Ràng buộc mọi bước bỏ khóa | bước 0.4 |
| Runner nào chạy 168 job: pack (trước ADR-090) hay HEAD? | Sửa ADR-090 chưa được G1/G2 kiểm trên transcript thật (`docs/19:9`) | `MANIFEST.tsv` |
| Giới hạn dòng hai chiều tới ~1 GiB được nâng thế nào trong các job đã ghi? | Launcher trong repo chỉ vá phía đầu ra | launcher fast-run (ngoài repo) |
| Khoảng cách plain/adapter của các nhánh cuối | Mọi kiểm C# là adapter-mode; `clipPickup` chưa có ở C# | đo ở GĐ6 |
| Bộ dự đoán adapter-mode (`engine_wf15_clint.js`) | Không có trong repo, sha chưa rõ; engine trong repo không tái sinh được dự đoán CL (làm tròn khác) | bước 0.4 |
| F16 chưa tái lập ở nhà; dòng wf14/wf15, engine wf15, ô P512, chỉ số forecast đều ở ngoài | Một nửa số liệu 30 ngày phụ thuộc vào dữ liệu ngoài | |
| Gate Shadow còn 5 job; SHF và Shadow INS chưa từng chạy bằng C# | Phạm vi kiểm chứng | máy thuê |
| Khóa B2–B5/origin-hold/`maximumRidersPerVehicle`/v2 có bị dùng ngoài repo không? | Bỏ parser thì cấu hình ngoài sẽ hỏng | bước 0.4 |
| Có bỏ khả năng tái lập ở HEAD cho WP2/WP3 demo, WP6, WP7, WP9 H6, WP13, WP14/WP14R? | Khôi phục được từ tag, nhưng mất lưới hồi quy (gồm một phần harness ADR-090) | chủ quyết |
| Đổi từ vựng giao thức v1 | Cần ADR (`docs/06:12-14`) | chủ quyết |
| Quyết định dwell còn treo | Ảnh hưởng `stopServiceDurationMs` | `PHASE1-LOG.md:433-434` |
| Hạn chót hai chiều và freeze horizon có lưu trữ không? | Kết quả đã ghi chỉ chạy được bằng binary cũ | chủ quyết |
| Runtime .NET tự nâng (runtimeconfig ghi `10.0.0`); `run_cloud.ps1` dùng .NET đi kèm hay của máy? | Tái lập qua các máy khác nhau | đọc `run_cloud.ps1` |
| `binarySha256` phụ thuộc đường build (DLL chứa đường dẫn PDB) | Build lại không tái lập được `manifestHash` của lượt chạy sống; G1 không bị ảnh hưởng | dùng binary lưu trữ; build sau này nên bật cờ build tất định |
| Pin SDK 10.0.301 trong khi máy công ty chỉ có 10.0.400 | Bằng chứng ADR-090 đã chạy khi vượt pin | ghi cách vượt pin; giữ pin |
| Harness ADR-090 | Đã chép vào repo ngày 04/11 (bước 0.3) | — |