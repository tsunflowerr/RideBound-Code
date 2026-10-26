# Ma trận truy vết yêu cầu

## RideBound thật trên dữ liệu Via, phép hòa vốn — 2026-10-26

ADR-085; nhánh `research/via-zone-2026-10-26` (trên `research/p1-dwell`, không đổi mã). Nhật ký:
`research/continuation/2026-10-26/kits/via-real-2026-10-26/VIA-LOG.md` và
`research/continuation/2026-10-26/kits/breakeven-2026-10-26/BREAKEVEN-LOG.md`.

| Yêu cầu | Chứng cứ | Trạng thái/giới hạn |
|---|---|---|
| Chạy RideBound thật trên dữ liệu Via ở cỡ Via | Bộ chuyển theo khu TLC: 509/508 yêu cầu, 62 xe, 60/58 điểm; dự đoán bộ mô phỏng khóa trước mọi job; **3/3 cặp trùng, 1.127/1.127 khách** (EOHZ35, G2K8-EOHZ35, G2K8-U; G2 dời 17/17, 27/27) | Họ kịch bản mới (điểm gộp theo khu), 1 ô; luật C (RCE35) chưa kiểm vì hết bộ nhớ ở bản chốt cuối |
| Ngân sách mặc định có đủ cho cỡ Via | Không: job thử dừng an toàn, ngân sách sinh phương án cấp quyết định 10.000 bị vượt với 62 xe | Sửa đổi A1 chỉ nâng ngân sách cấp quyết định và bộ giải bằng cấu hình; trần từng xe giữ nguyên |
| Job Via chạy hết tới bản chốt cuối | Job A1 hỏng ở bản chốt cuối; với A3 (heap 6 GiB) 4/8 job đã xong qua, 4 job (RCE35 ×3, U w08) báo `OutOfMemoryException` ở bản chốt cuối | Bản chốt là một dòng vài trăm MiB; cần đổi cách ghi bản chốt hoặc máy nhiều RAM hơn |
| Điều kiện để luật C đáng dùng (phép hòa vốn) | Kế hoạch khóa trước; cổng 2.520/2.520 lượt trùng P5/N2; G2, cùng số khách, so EOHZ35: w\* = 0,94 (0,46–1,61) | Bộ mô phỏng, 10 ngày đã xem, thăm dò; không đo cảm giác khách; INS không phân biệt được |
| Đưa code và script lên GitHub cho máy công ty | Nhánh `research/via-zone-2026-10-26`; `research/continuation/2026-10-26/kits/` (danh sách tệp và sha256 trong `COPY-MANIFEST.tsv`) | Không có dữ liệu lớn, transcript, bộ mô phỏng của công ty; repo công khai |

## Đóng cầu nối giai đoạn 1 — 2026-10-25

ADR-082 (và ADR-083 trên `research/p1-dwell`); nhánh thăm dò `research/p1-fix`, chưa push. Nhật ký:
`E:/Code/Report_INT3508/ridebound-scratchpad/bridge-phase1-2026-10-23/PHASE1-LOG.md`.

| Yêu cầu | Chứng cứ | Trạng thái/giới hạn |
|---|---|---|
| Bộ mô phỏng sinh số Elmi của công ty (wf15) là bộ mô phỏng đã bắc cầu | wf15 tái tạo đúng từng byte 4 tệp dự đoán đã khóa | Xong |
| Hàng rào chặt (ép xe) khớp RideBound thật | EO35, DLS-120 với `forced-reference-v1`, có và không có G2: 44/44 ô-nhánh; 35–534 quyết định bị ép mỗi nhánh | Chỉ 11 ô Manhattan |
| G2 giới hạn k xe nhận khớp | k = 3: 33/33; giới hạn đổi kết quả ở 93–166 khách | k = 8 chưa thử ở đội 62 xe |
| Ô lớn hơn | 10 ô 128 yêu cầu × 10 xe: 40/40 | Trần hợp đồng v1: 128 yêu cầu, 96 nút |
| Sửa adapter ca "xe cho" theo 4 điều kiện của chủ | Test tái hiện hỏng trước, đạt sau; chạy lại 108 job G2: 106/106 giữ nguyên, 108/108 khớp; bộ test adapter 461 với 6 lỗi đã biết | Không đổi RideBound, không đổi bộ mô phỏng |
| Tác giả commit là chủ nghiên cứu, không lộ e-mail máy công ty | Chuỗi `7435661..research/g2-core` tạo lại, cùng cây; ref cũ ở `refs/backup/pre-author-rewrite-20261025/` | Nguồn gốc ghi trong ADR-080/081/082 |
| Đối thủ chính | Chủ quyết 25/10: EOHZ35/SHHZ chính, luôn báo kèm EO35/SHF | Thay câu "đối chứng chính EO35" của ADR-081 |
| Thời gian phục vụ ở điểm dừng (ADR-083) | Tùy chọn `stopServiceDurationMs`, mặc định tắt; 1230/1230 test; khi tắt, phát lại 228/228 job giống từng byte | Dwell khác 0 bị chặn (lõi thiếu "xe bận tới T"); khóa luận ghi dwell 0 |
| Kết luận Via không phụ thuộc chế độ định danh (ADR-084, §6-P5) | Cổng 0: 840/840 dòng trùng công ty; các phép so chính giữ dấu trên 10 ngày | 7/84 ô đánh dấu, không dùng; thăm dò |
| Dữ liệu Via là chuyến thật (ADR-084, §6-P7) | TLC 3/2019 và 9/2019 so EXCEPT ALL: 0/0 dòng lệch | Mới kiểm 2/10 tháng |
| Hạn chót nới dạng của sinh viên (ADR-084, §6-P4a) | 11/11 ô, 976/976 khách trùng dự đoán DLHZ-120; tổng cầu nối 238 cặp | Ô Manhattan nhỏ |
| Hàng rào chặt và độ dư α (ADR-084, §6-N1, N2) | Dung sai 1/5/60 s; α 0–100% | Thăm dò trên 10 ngày đã xem |

## Cầu nối bộ mô phỏng công ty: A0.1, Elmi, G2 — 2026-10-23

ADR-080 (nhập từ máy công ty) và ADR-081; nhánh thăm dò `research/via-ref` → `research/via-core` → `research/g2-core`, chưa
push; không nâng work package. Nhật ký: `E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/AMENDMENTS.md`.

| Yêu cầu | Chứng cứ | Trạng thái/giới hạn |
|---|---|---|
| Nhập patch của máy công ty, giữ nguồn gốc | 4 commit giữ ngày và thông điệp gốc, kèm sha256 patch; dựng lại đúng cây `086beacc`, `c112fdbb`. Tác giả được viết lại thành chủ nghiên cứu ngày 25/10 (ADR-082) | Xong; nguồn gốc ghi trong ADR-080/081; bản nháp ADR-080 chỉ tóm tắt, chưa chép nguyên văn |
| Bộ mô phỏng khớp RideBound thật (chỉ chèn) | 55/55 ô-nhánh, 4.972/4.972 khách; dự đoán khóa sha256 trước job chính | Chỉ 11 ô Manhattan, chế độ adapter-identity; Via plain chưa nối |
| Runner nhanh hơn mà không đổi đầu ra (A0.1) | Phát lại 58/58 job, 43.154/43.154 khung giống từng byte | A0.2–A0.5 chưa làm |
| Đối chứng Elmi theo từng khách (RCE35, EOHZ35) | `6d6fa47`, `4a24810`; 1178/1178 test; cầu nối 22/22, 1.966/1.966 khách | Comparator, không phải đóng góp; EOC dùng floor, chỉ so EOHZ35 với EOHZ35 |
| G2: dời tối đa 1 khách đang chờ mỗi quyết định | `115a4fc`; 1225/1225 test; tắt G2 thì phát lại 80/80 job giống từng byte | Không claim tính mới; chỉ C1 solver cost-first |
| G2 khớp bộ mô phỏng từng khách | v4: 33/33 ô-nhánh, 2.994/2.994 khách, 142/142 lần dời; 24.439 quyết định evidence 1.3.0, 0 lỗi kiểm | Lần đầu 30/33; không chứng minh lợi ích G2 trên Via |
| Sửa adapter theo 4 điều kiện của chủ | `fa676b4`; dwell 0 ở hai phía; 3 ca tái hiện và test fail trước khi sửa; chạy lại 113/113; job đã khớp không đổi | Không đổi RideBound, không đổi bộ mô phỏng; dwell khác 0 chưa kiểm |
| Ghi lỗi của chính quy trình | Lỗi launcher (job G2 trước v4 dùng adapter cũ) và lỗi Contracts 1.3.0 ghi ở AMENDMENTS A6, A8 | Số 30/33 lần đầu đo với adapter cũ |
| Giữ biên repo | Chỉ commit trong worktree; cây chính `E:/Code/RideBound` và `E:/Code/RideBound-deadline` không đổi; không xóa đầu ra | Chưa push; trộn vào cây chính cần chủ quyết |

## Bàn giao GitHub và khảo sát C60/C120 — 2026-10-16

| Yêu cầu | Chứng cứ | Trạng thái/giới hạn |
|---|---|---|
| Mang mã/kết quả lên máy công ty | ADR079; hai nhánh codex handoff; START-HERE và inventory xuất | Đã push hai nhánh codex; không rewrite lịch sử, không raw/vendor/env/secret/paper |
| Chạy C60/C120 cost-first | main/analysis-complete.json;rootlog exit0;independent-arithmetic4355/4355 | COMPLETE:2/2smoke,68/68main,0active/pending;C60 +32served/+19late60 soC30;C120 +34served/+121late60 soC60;thăm dò,không optimum |
| Bảo toàn dữ liệu/kết quả cũ | Original seal/freeze/binding, bundle index và kết quả C30/U | Không sửa/rerun; H6/E1/H7 giữ nguyên; không per-step/Stage2 |
| Thực thi trong tài nguyên owner cho phép | C30/E40GiB,AC-only,410MiB/job,6workers;final runtime snapshot | Đã hoàn tất,không hạ sàn/xóa/retry;không resume/rerun |
| Phân biệt code portable với tái lập raw | START-HERE, external input paths và hash | Clone để đọc/sửa nguồn; máy mới cần provision/rebind execution manifest |

Chi tiết [hồ sơ mới](benchmarking/cost-cap-sensitivity-handoff-2026-10-16.md).

## Tiếp phiên khóa luận và Stage 1b — 2026-10-15

ADR-078 ghi nhận nghiên cứu ngoài cây chính; không nâng work package.
[Hồ sơ](benchmarking/thesis-stage1b-continuation-2026-10-15.md).

| Yêu cầu | Chứng cứ | Gate/giới hạn |
|---|---|---|
| Kế thừa đúng trạng thái, không chạy trùng | Script/output inherited-state, 17/17 seal SHA, 4 gate/2 smoke bundle | Main/freeze/binding chưa có lúc kiểm; giữ raw và failure receipts |
| Selector mới hoạt động đúng | 746 CF-C30 + 768 CF-U quyết định từ frame bytes | Activation/correctness, không phải service/profit claim |
| Khóa analysis trước main | Sửa R46, review chéo, 19/19 + 18/18; full v3 192/192; judge READY; khóa 10 tệp 01:10 +07 | Manifest f2db6445…952f1; 17 tệp seal cũ giữ nguyên, bản gốc analysis được lưu C; thresholds giữ nguyên |
| Tính lại số lịch sử | 1.836 đối chiếu R19 không lệch trong full v2 | Ba diagnostic fixtures được sửa đúng tình huống, không bỏ test |
| Baseline minh bạch | Required dotnet test trên cost-first checkout: 1.033 pass + 1 known failure | Exit 1; không nhận suite pass hoặc baseline BeGo mới |
| Luận văn dễ đọc, đủ đánh đổi và provenance | Bản thật 77 trang đã cập nhật 2026-10-16; backup/receipt thesis-v5-deployment.json;47/47 kiểm bảng;visual review toàn PDF | Tên cấu hình tự nhiên, không stage jargon;giữ giới hạn/đối chứng bất lợi;QA không phải tái lập mọi số lịch sử |
| Giữ quyền và biên repo | E core/job read-only, output C, AC-only; owner đổi C45 thành C30 GiB ngày15/10, E40/reserve410MiB giữ nguyên | Không xóa, commit/push, chạy Stage2 hoặc cứu H6/WP14; không hạ dưới sàn mới |
| Hoãn tài nguyên tách khỏi lỗi thực nghiệm | Main68/68 pass; toàn bộ bundle khớp length/SHA/returncode0/binding; final post-phase exit0 lúc11:28;0stop | Không retry failed/partial; boundary sau20 run giữ rõ; admission evidence/main-final-admission.json trên C |
| Phân tích toàn panel bằng mã đã khóa | Extract/report exit0;9.638 rider rows/136 role-runs;19 SF constants+25/25 dấu ô tái lập;83/83 kiểm số học độc lập đạt | Native exit0 sau68/final post-phase exit0;outputs exclusive;không claim kiểm độc lập raw/forecast bằng arithmetic checker |
| Ghi nhận thay đổi chính sách tài nguyên, giữ frozen inputs | Resource30-v3 manifest7c862409…11950;12/12 boundary checks;17 seal/10 analysis/binding gốc khớp | Chỉ đổi FLOOR_MIB=30720 trong bộ nhớ sau xác thực nguyên byte v7; nêu rõ ranh giới sau20 run, không tự nhận toàn bộ chạy dưới sàn45 |
| Tự tiếp tục một lần, giữ theo dõi theo giờ | Wake06:20 thực tế nhận09:33;one-off PAUSED09:33;hourly PAUSED sau báo kết quả11:43 | Không driver trùng,dời ngày hoặc archive;closure evidence/analysis-completion.json |
| Báo cả kết quả và đánh đổi, giữ claim hẹp | I_L+180,I_served+42,I_timely60−138;CF-C30 served2364,late60 72,shared432;exposed106 vsSF-C30 33;forecast late60 11 vs10 | Thăm dò34 ô/5 ngày phụ thuộc;không causal/general/profit/cumulative-vs-per-step claim;giữ H6/E1 gốc |
| Giữ điểm dừng Stage2 | Stage1b G0,H1–H6 PASS,H7 FAIL(7<10) trong report đã khóa | Không xây/chạy per-step arm;H7 fail là resource screen,không phủ định cơ chế tích lũy |
| Tắt theo dõi khi phân tích và báo xong | evidence/analysis-completion.json;main/extract/report exit0;hourly vàone-off đều PAUSED | Không archive chat;không rerun/overwrite;luận văn thật đã cập nhật theo yêu cầu mới, có sao lưu và receipt ngày2026-10-16 |
| Rà mức sẵn sàng bảo vệ, không đồng nhất PDF với hoàn tất nghiên cứu | [Audit13điểm](C:/RideBoundContinuation/2026-10-16/thesis-readiness-audit/THESIS-READINESS-AUDIT.md);trace-evidence/selection-trace;22payload+13result entries khớp | Còn mô tả60→49→34/15, giả định CI, trạng thái dự báo và mẫu áp dụng;chưa đóng bản nộp/WP12;không chạy nghiên cứu mới |

## 1. Yêu cầu từ mục tiêu dự án

| ID | Yêu cầu | Thiết kế/tài liệu | Artifact dự kiến | Verification | Trạng thái |
|---|---|---|---|---|---|
| R-001 | Hệ thống RideBound độc lập | `05` | `Domain/Application/Runner` trong Git repo riêng | 5 architecture rules + 2 cross-platform path cases | WP0 verified; Linux CI rerun pending |
| R-002 | Gắn được vào BeGo | `02`, `14`, `32` | BeGo adapter/API | integration replay | WP5 `002..014` có Application, persistence, intake/lease, Runner, bootstrap/API, fenced T2/T3 recovery, Applied-only outbox/SignalR, rebuildable timeline, default-off Shadow/Live rollout và paired Layer-1 bundle |
| R-003 | Core mang sang benchmark | `05`, `06`, `24`, `26`, `32`, `36`, `40` | contracts + runner | same binary hash | WP1/WP2 verified; WP5 BeGo and WP7 FleetPy use pinned Runner; WP10 RidePy canonical uses same exact Runner tree, representative Layer 3 subset fails for position capability rather than a second decision implementation |
| R-004 | Layer 1 cùng codebase | `09`, `32`, `34`, `39` | B1/C1 chung RideBound engine; BeGo/WP6 paired harness | paired runs | Mechanical same-Runner evidence verified WP5/WP6/WP9 Layer 1; không nâng thành effectiveness |
| R-005 | Layer 2 simulator chung | `09`, `12`, `35`, `36`, ADR-038/048 | FleetPy adapter gọi same Runner | capability/preflight + actual lifecycle/tiny/medium/confirmatory closed-loop + external verifier | WP7 mechanical complete; WP9 H6 FleetPy effectiveness có 100/100 raw bundle verified và kết quả âm có điều kiện |
| R-006 | Layer 3 framework độc lập | `09`, `13`, `40`, ADR-050/051 | RidePy adapter gọi same Runner | exact source/env + canonical + paired Layer 3 gate | WP10 complete negative: source/env/same-Runner/canonical PASS; paired subset FAIL CLOSED do `nodeOnly` concurrent mid-edge; Layer 3 claim not established |
| R-007 | Giới hạn cumulative revision | `04`, `07`, `28` | ledger/validator | property/mutation tests | WP3 implemented: independent hard-vector recomputation + immutable ledger + certificate |
| R-008 | Nhiều chiều promise | `04`, `07`, `28` | promise vector | golden fixtures | WP3 implemented: 10 dimensions, projection, three-way delta, strict wire actions và tiny replay |
| R-009 | Certificate/witness | `07`, `28` | certificate DTO/validator | invalid-plan mutations | WP3 implemented: physical→lock→budget stages, normal/witness body và decision/publication cross-binding |
| R-010 | Đánh giá bằng data rõ | `10`, `18`, `33`, `34`, WP6 contract | immutable public-source registry + deterministic normalization + scenario identity | source checksum/license/schema/vector gates | WP6 `002..004` complete: strict contracts, verified FleetPy source and exact tiny/medium canonical derivatives |
| R-011 | Metric/statistics rõ | `11`, `18`, `33`, `34`, WP6 contract, WP8/WP9 reports | raw observations + explicit denominator/missingness + independent oracle | golden/mutation/recompute gates | WP9 H6 complete: exact two-panel service/burden gates, locked/earned split, robustness non-rescue và finite-panel precision boundary |
| R-012 | Dùng paper nhưng không làm lại | `03`, `21` | claim ledger + full-PDF provenance | novelty re-audit | WP1–WP8 mapped; full PDFs Alonso-Mora/Gschwind/Simonetto/Engelhardt/Zalesak/Schulz read and hashed; only exact same-state reuse applied, no random/direction/sparse prune |
| R-013 | Nêu công nghệ và tối ưu | `05`, `08`, `12`–`14` | projects/adapters | build/performance | WP4 solver + WP5 DB/Runner/concurrency/local curves implemented; no SLA claim |
| R-014 | Nêu thêm/bỏ gì | `02`, mục 2 dưới | migration plan | code review | Docs v1 |
| R-015 | Agent sau biết tiếp tục | `00`, `17`, `18`, `23`–`40` | root `AGENTS.md` + ordered queue | reading order + closed queue/evidence/review | WP1–WP10 DONE; WP10 negative result/next action/receipts được ghi ở ADR-051 và report |
| R-016 | Thuật ngữ dễ hiểu | `22` | glossary | doc review | Docs v1 |
| R-017 | Không bias bằng dữ liệu giả | `01`, `10`, `11`, WP8 prereg/amendments | pilot/holdout | prereg + leakage audit | Pilot ngày 11–12 tách holdout ngày 14–18; node-cap, integrity và Runner-repin amendments đều pre-outcome confirmatory; margin 1 pp giữ dù pilot bất lợi |
| R-018 | Tái lập | `06`, `15`, `24`, `26`, `28`, `32`–`39`, WP6/WP7/WP8/WP9 evidence | identities + seed tree + strict bundle + pinned FleetPy/Runner + repository content inventory | tamper/no-extra/source/assembly/hash + actual lifecycle/verifier + freeze verifier | H6 recompute 30 file hashes + 5 tree seals; independent verifier PASS 100 raw bundle, cross-panel identity và deterministic repeat; không claim independent reproduction |
| R-019 | Fair paired effectiveness design | `11`, `18`, `37`–`39`, ADR-048 | oriented pair + fixed panel + locked/earned decomposition | arm swap/config/label/scenario/inventory mutations | WP9 complete: cả hai service gate FAIL, burden gate PASS; panel không pool và robustness không rescue |

## 2. Những gì cần thêm

### Source projects đã scaffold trong WP0

- `RideBound.Contracts`
- `RideBound.Domain`
- `RideBound.Application`
- `RideBound.Algorithms`
- `RideBound.Solvers.OrTools`
- `RideBound.Infrastructure`
- `RideBound.Runner`
- `RideBound.Domain.Tests`
- `RideBound.ArchitectureTests`
- `RideBound.Contracts.Tests` (WP1)
- `RideBound.Application.Tests` (WP2)
- `RideBound.Algorithms.Tests` (WP2)
- `RideBound.Runner.Tests` (WP1)

ADR-025 khóa placement WP5: adapter/API/EF/SignalR nằm trong tracked BeGo `src`
và chỉ gọi exact hashed/versioned Runner artifact qua NDJSON. Không thêm project
reference xuyên repository hoặc scaffold `RideBound.Adapters.BeGo`/
`RideBound.Persistence` rỗng. RideBound chỉ nhận harness/docs/evidence cần tái lập;
core không biết BeGo.

### WP0 artifacts đã có

- `RideBound.slnx`, `global.json`, `Directory.Build.props`;
- 7 source project và 2 test project;
- `tests/RideBound.ArchitectureTests/DependencyRuleTests.cs`;
- root `README.md`, `AGENTS.md`, GitHub Actions CI;
- PR template, Dependabot, format/test/coverage/dependency gates, conditional
  Sonar Quality Gate và conditional PR-Agent review;
- `benchmarks/`, `simulators/`, `scripts/`, `artifacts/` có boundary README;
- 8/8 RideBound tests hiện tại cùng baseline 25/25 backend và 7/7 frontend BeGo.

### Runtime/system

- event protocol;
- long-lived runner;
- online vehicle/request state;
- promise ledger;
- certificate validator;
- paired replay harness;
- simulator adapters;
- experiment artifact pipeline.

WP1 runtime boundary đã được cài theo `24`: contract harness,
primitives/envelope, canonical JSON, schema/version policy, hello/init,
event/decision/error shell, hash chain, NDJSON session và idempotent retry đều
có code/test. WP2 hiện đã có typed online payload, vehicle/request state, route,
atomic reducer, physical validator, deterministic candidate/B1 selection,
independent exact-small oracle và online produced decision. WP3 đã thêm ledger,
hard commitment validator/certificate, incident/breach và checkpoint trên cùng
Runner boundary. WP4 đã hiện thực C1/objective/solver; WP5 đã hiện thực BeGo adapter;
WP6 đã hiện thực common harness. WP7 đã thêm FleetPy closed-loop adapter chỉ gọi
published external Runner; closed-loop evidence là mechanical Layer 2, không phải
effectiveness evaluation.

### Product hoặc BeGo integration

- RideBound endpoints/client nằm ở repository BeGo khi WP5 yêu cầu;
- persistence;
- SignalR events;
- promise/revision timeline;
- incident UX.

## 3. Những gì cần bỏ, không dùng hoặc deprecate trong phạm vi RideBound

Không nhất thiết xóa ngay khỏi repo:

- Không dùng `HybridOutingRoutePlanner` làm RideBound engine.
- Không dùng `Session.LatestOptimizationSnapshotJson` làm ledger.
- Không dùng current static assignment làm online baseline chính.
- Không dùng Li & Lim v1 hiện tại làm bằng chứng hard time-window.
- Không dùng synthetic user budget làm nhãn hành vi thật.
- Không dùng per-epoch process spawn.
- Không dùng adapter-specific reimplementation.
- Không dùng một weighted scalar duy nhất để che hard budget dimension.
- Không dùng customer satisfaction/demographic fairness claim khi thiếu data.
- Không đưa OpenRidepoolSimulator/MOSEK cũ vào critical path.

Sau khi RideBound ổn định có thể deprecate benchmark claims quá mạnh trong `benchmarks/public/README.md`; trước đó phải giữ compatibility và thêm caveat, không xóa dữ liệu tùy tiện.

## 4. Functional requirements

| ID | Requirement | Test/evidence |
|---|---|---|
| F-001 | Nhận ordered event batch | WP1 protocol golden + WP2 mapper/atomic reducer verified |
| F-002 | Advance/freeze vehicle state | Domain/reducer/route properties verified cho WP2-003..006 |
| F-003 | Accept/reject/defer request | lifecycle + B1 typed actions/no-reassignment/ACK apply verified |
| F-004 | Generate/evaluate route candidates | deterministic generation, independent physical validation và exact-small differential verified |
| F-005 | Publish promise | WP3 verified: initial/revision actions bind ledger, certificate, state hash và matching ACK |
| F-006 | Track cumulative/switch budgets | WP3 verified: immutable ledger + 10-dimension property/boundary/mutation-killing tests |
| F-007 | Reject candidate vượt budget | WP3 verified: candidate filter + independent physical/lock/budget recomputation and witness |
| F-008 | Handle incident breach riêng | WP3 verified: typed open/resolve, affected riders, immutable breach chronology/budget relation |
| F-009 | Checkpoint/restore | WP3 verified: canonical hash/tamper/reachable-state checks + genesis-suffix process equivalence |
| F-010 | Export transcript/metrics | WP5 timeline/export và WP6 immutable transcript, observation index, 36-metric production/oracle rows, strict bundle verified |

## 5. Non-functional requirements

| ID | Requirement | Verification |
|---|---|---|
| N-001 | Deterministic replay | decision hash |
| N-002 | Cross-language portability | same runner |
| N-003 | Auditability | append-only + certificate |
| N-004 | Idempotency | duplicate event tests |
| N-005 | Performance deadline | p95/p99 tests |
| N-006 | Không làm hỏng BeGo độc lập | external BeGo 25+7 regression |
| N-007 | Reproducibility | clean bundle run |
| N-008 | Security/privacy | auth/retention review |
| N-009 | Versionability | schema compatibility tests |

## 6. Research claims → evidence

| Claim | Cần evidence tối thiểu | Không đủ |
|---|---|---|
| Giữ hard budget | validator + property/mutation + zero normal breach | chỉ log |
| Giảm revision | paired Layer 1/2 + CI | một demo |
| Không giảm service đáng kể | prereg non-inferiority | mean difference |
| Portable | same binary ở 3 layers | ba bản code |
| Robust cross-system | Layer 3 paired + capability report | native baseline khác input |
| Fairer burden | distribution metrics, scope rõ | demographic claim |

## 7. Audit rule

Mỗi khi work package hoàn thành:

- đổi `Planned` thành `Implemented/Verified`;
- điền file/test/result cụ thể;
- nếu requirement bị cắt, ghi rationale trong decision log;
- không để status “Verified” chỉ dựa trên docs.

## 8. WP1 ticket traceability

| Ticket | Requirement chính | Evidence dự kiến |
|---|---|---|
| RB-WP1-001 | N-001, N-002, N-009 | ADR-014 + normative decision table `06` mục 2–14; docs verified, runtime vẫn planned |
| RB-WP1-002 | R-003, N-009 | `RideBound.Contracts.Tests`, fixture loader + smoke tests |
| RB-WP1-003 | R-003, N-009 | protocol primitives/envelope codec + structural fixtures/tests |
| RB-WP1-004 | R-003, N-001 | canonical units/JSON + exact UTF-8 golden hex + 63 Contracts tests |
| RB-WP1-005 | R-003, R-008, N-009 | schemas + compatibility matrix/tests; implemented |
| RB-WP1-006 | N-002, N-009 | hello/ack schema, fixture và negotiation tests; implemented |
| RB-WP1-007 | R-018, N-001, N-009 | init/manifest schema, identity validation tests; implemented |
| RB-WP1-008–009 | F-001, N-002, N-009 | event/decision/error schemas/codecs và structural shell tests; verified |
| RB-WP1-010 | N-001, N-003 | domain-separated length-frame SHA-256 code + published vectors; verified |
| RB-WP1-011–013 | N-001, N-004 | async transport, process/session, exact full-batch retry, duplicate/version/gap tests; verified |
| RB-WP1-014 | R-018, N-001, N-007 | exactly 10 fixtures + exact replay/hash/tamper proof; verified |
| RB-WP1-015 | R-003, R-015, R-018 | Q1 Release gate report + `RB-WP2-001` handoff; complete |

### 8.1. RB-WP1-001 closure

| Contract concern | Normative evidence | Trạng thái |
|---|---|---|
| exact schema version/compatibility boundary | `06` mục 2, ADR-014 | Khóa cho v1; implementation ở `003`/`005` |
| unit, rounding và overflow | `06` mục 3, ADR-014 | Khóa cho v1; test vectors ở `004` |
| node/edge-progress position | `06` mục 7/9, ADR-014 | Contract khóa; FleetPy extraction preflight ở WP7 |
| batch/epoch/event ordering | `06` mục 2/12, ADR-014 | Khóa cho v1; implementation ở `008`/`013` |
| envelope/payload/manifest ownership | `06` mục 2/4.1, ADR-014 | Khóa cho v1 |
| error taxonomy/severity | `06` mục 12.2, ADR-014 | Khóa cho v1; implementation ở `009`/`011`–`013` |
| canonical bytes/hash framing | `06` mục 10, ADR-014 | Khóa cho v1; vectors/code ở `004`/`010` |
| fixture support taxonomy | `06` mục 13, ADR-014 | Khóa cho v1; assets ở `002`/`014` |

`RB-WP1-001` là docs/ADR evidence; runtime verification được bổ sung bởi
`RB-WP1-002..015` và closure evidence dưới đây.

### 8.2. RB-WP1-002–004 implementation evidence

| Ticket | Implementation | Verification | Trạng thái |
|---|---|---|---|
| `002` | `tests/RideBound.Contracts.Tests`, `benchmarks/schemas/fixtures` | fixture path/UTF-8/missing/traversal tests | Implemented; task tests verified |
| `003` | `ProtocolPrimitives`, `ProtocolEnvelope`, `ProtocolEnvelopeCodec` | version, identity, context, round-trip và invalid fixtures | Implemented; task tests verified |
| `004` | `CanonicalUnits`, `CanonicalJson`, canonical input/hex vector | byte equality, culture, Unicode, order, range/overflow tests | Implemented; task tests verified |

Evidence ngày 2026-07-29: Contracts 66/66 và Architecture 7/7 pass, Release
build/format/vulnerability audit pass. Full solution local chưa thể xác nhận xanh
do Windows Application Control chặn 1 Domain smoke với `0x800711C7` sau khi 73
test khác pass; Q1 và các
requirement cấp WP vẫn chưa `Verified` cho tới các ticket còn lại và gate `015`.

### 8.3. RB-WP1-005–007 implementation evidence

| Ticket | Implementation | Verification | Trạng thái |
|---|---|---|---|
| `005` | `benchmarks/schemas/v1`, `ProtocolVersionCompatibility`, version-aware envelope validation | schema ID/ref/inventory, patch/minor/major/unknown-field matrix tests | Implemented; task tests verified |
| `006` | `HelloMessages`, `CapabilityNegotiator`, hello/ack schema + fixtures | required/optional/order/unknown/missing/scale/named-downgrade tests | Implemented; task tests verified |
| `007` | `InitializeRunMessages`, `InitializeRunValidator`, manifest/init schemas + fixtures | identity match/mismatch, unoffered capability, re-init, unit/hash/wall-clock boundary tests | Implemented; task tests verified |

Evidence ngày 2026-07-29: Contracts 95/95, Runner boundary 11/11 và required
full solution 114/114 pass; Release build/format/vulnerability audit pass.
Release-only full-suite attempt có 113 pass và Domain smoke bị Windows
Application Control chặn `0x800711C7`. Đây là schema/handshake/identity evidence,
không tự nó là deterministic decision hash hoặc executable NDJSON evidence;
các phần đó được đóng riêng bởi `008–015` dưới đây.

### 8.4. RB-WP1-008–015 Q1 closure evidence

| Ticket | Implementation | Verification | Trạng thái |
|---|---|---|---|
| `008` | `EventBatchMessages`, event schemas, `EventBatchOrderingValidator` | vocabulary, empty/order/gap/overlap/epoch/time/overflow tests | Implemented/Verified |
| `009` | `DecisionMessages`, `ErrorMessage`, decision/error schemas | not-produced shell, hash classification, stable error disposition/sanitization tests | Implemented/Verified |
| `010` | `ProtocolHash` + `fixtures/hash/protocol-hash-vectors.json` | genesis, next-chain, order/tamper và exact lowercase SHA-256 vectors | Implemented/Verified |
| `011` | `NdjsonReader`, `NdjsonWriter` | LF/CRLF/UTF-8/size/EOF/flush/cancellation memory tests | Implemented/Verified |
| `012` | `RunnerSession`, `RunnerHost`, executable `Program` | state edges, memory pipe và real child-process stdout tests | Implemented/Verified |
| `013` | bounded one-response cache + failure lifecycle | exact canonical envelope+payload retry/no-advance; changed payload/time, hash/version/epoch/sequence tests | Implemented/Verified; ADR-017 |
| `014` | `fixtures/golden/required` và `fixtures/runner` | exactly 10 metadata/input pairs; replay twice in memory + two clean processes exact output/hash; tamper changes hash | Implemented/Verified |
| `015` | ADR-016, Q1 report và `tasks/25-wp2-online-state-refinement.md` | Release full solution + docs/artifact audit | Complete |

Evidence ngày 2026-07-29:

- Release full solution tại mốc đóng Q1: 157/157 pass — Contracts 114, Runner
  35, Architecture 7, Domain 1;
- inventory hiện tại là 161 sau assertion đồng bộ vocabulary, changed-time retry
  regression và clean-process replay; Release pass Contracts 115/115, Runner
  38/38, Architecture 7/7 và Domain 1/1;
- `dotnet format ... --verify-no-changes`: pass;
- Release build: 0 warning, 0 error;
- NuGet direct/transitive vulnerability audit: không có package bị báo;
- historical Release full-suite attempt tại Q1 pass 123 test trước khi toàn bộ
  35 Runner test bị chặn lúc nạp fresh Runner.dll; tại thời điểm đó Runner
  source/test không đổi từ mốc pass 35/35;
- historical Debug full-suite tại Q1: Architecture 7 pass; Domain bị chặn;
  Runner báo 5 pass/30 load-policy failure và Contracts báo 15 pass/85
  load-policy failure trước khi hoàn tất inventory. Enterprise Code Integrity
  policy `0283ac0f-fff1-49ae-ada1-8a933130cad6` chặn fresh DLL với
  `0x800711C7`; event 3033/3077 xác nhận signing-level policy. Tại lần WP1
  revalidation sau đó, Debug pass 123 non-Runner test rồi fresh Runner assembly
  lại bị policy chặn; Release pass 161/161.

Phạm vi được Verified ở Q1 là schema/canonical hash/runner lifecycle/replay.
R-003 chưa chứng minh same binary trong adapter, N-002 chưa chứng minh portable
cross-system và N-007 chưa phải experiment bundle hoàn chỉnh. Trạng thái
F-001–F-004 sau WP2-002..006 được cập nhật riêng dưới đây; F-005–F-010 và
ledger/certificate/online selection vẫn Planned.

## 9. WP2 ticket traceability

| Ticket | Requirement chính | Evidence dự kiến/trạng thái |
|---|---|---|
| RB-WP2-001 | R-015, F-001–F-004, N-001 | ADR-018 + execution plan `26`; refinement Done, no WP2 code |
| RB-WP2-002 | F-001, F-002, F-003, N-001, N-009 | Implemented/Verified: typed contracts, 16 schemas, bootstrap/two-epoch fixtures |
| RB-WP2-003 | F-002, F-003, N-001 | Implemented/Verified: Domain lifecycle matrix + aggregate atomic tests |
| RB-WP2-004 | F-002, F-004, N-001 | Implemented/Verified: prefix/suffix/leg/no-op/progress properties |
| RB-WP2-005 | F-001–F-003, N-001, N-004 | Implemented/Verified: Runner mapper + atomic reducer/replay/ack tests |
| RB-WP2-006 | F-002, F-004 | Implemented/Verified: physical mutation/generated/witness tests |
| RB-WP2-007 | F-004, N-001 | Implemented/Verified: deterministic insertion/count/order/cap/no-op/prune tests |
| RB-WP2-008 | F-003, F-004, N-001 | Implemented/Verified: B1 accept/reject/defer, no-reassignment, validator recheck và stable selection |
| RB-WP2-009 | F-004, N-001 | Implemented/Verified: independent exact-small 32 seeds, generator/selection gap 0 |
| RB-WP2-010 | R-003, F-001–F-004, N-001, N-004, N-009 | Implemented/Verified: produced B1 decision, ACK-only apply/hash/retry và Q1 conformance mode |
| RB-WP2-011 | R-018, F-001–F-004, N-001, N-007 | Implemented/Verified: four-epoch two-clean-process replay/final hash/tamper proof |
| RB-WP2-012 | R-015, R-018 | Done: ADR-020, WP2 gate report + only `RB-WP3-001` refinement handoff |

### 9.1. RB-WP2-001 closure

- Domain sở hữu state/invariant; Application sở hữu reducer/orchestration;
  Runner map Contracts sang internal events.
- Batch apply nguyên tử và state/plan chỉ commit tại `decisionApplied`.
- Route tách exact frozen prefix/mutable suffix và có no-op candidate.
- O-001 đóng cho WP2: incumbent accepted request không đổi vehicle.
- B1 chỉ có physical constraints/accepted preservation/cost/tie-break; chưa có
  commitment constraint.
- Ordered queue và acceptance criteria đầy đủ nằm trong
  `tasks/26-wp2-online-baseline-ticket-plan.md`.
- Tại mốc refinement, ticket implementation duy nhất `READY` là
  `RB-WP2-007`; ticket này nay đã `DONE` theo mục 9.3.

### 9.2. RB-WP2-002–006 implementation evidence

| Ticket | Artifact chính | Verification |
|---|---|---|
| `002` | `OnlineEventModels`, strict payload codecs/schema và `fixtures/wp2` | typed round-trip, schema/runtime vocabulary, invalid unknown/null/fraction/duplicate/range |
| `003` | `RideBoundRun`, `RideRequest`, `VehicleState` | exhaustive transition table, duplicate/stale/atomic boarding, accepted-never-rejected |
| `004` | `RoutePlan`, `RouteStop`, `RouteLeg` | exact prefix/no-op/version/progress, generated suffix preservation |
| `005` | `OnlineEventMapper`, internal events, `EventReducer`, coordinator | published two-epoch map/reduce/ack, invalid-last-event rollback, replay, snapshot hash/version |
| `006` | `PhysicalPlanValidator` + travel lookup port | 24 stop permutations và mutations cho mọi physical dimension trong scope |

Evidence ngày 2026-07-29:

- required Debug command `dotnet test RideBound.slnx`: 278/278 pass;
- Release full solution: 278/278 pass — Contracts 127, Domain 89,
  Application 13, Runner 42, Architecture 7;
- Domain/Application không có Contracts/framework/simulator/solver dependency;
- Q1 exact transcript/hash/retry tests tiếp tục pass;
- Tại mốc `002..006`, F-001/F-002 và physical-evaluation phần F-004 đã
  implemented/verified; F-003/B1 và candidate generation khi đó còn chờ
  `RB-WP2-007..008`. Trạng thái closure hiện tại nằm ở mục 9.3.

### 9.3. RB-WP2-007–012 implementation/closure evidence

| Ticket | Artifact chính | Verification |
|---|---|---|
| `007` | `InsertionCandidateGenerator`, canonical candidate/stop identity, schedule evaluator | empty/one-stop/two-request counts, exact cap fail, bounded no-op retention, prefix/no-mutation/prune witness |
| `008` | `CandidateFleetSelector`, `RollingCostPolicy`, decision-state staging | lower cost/tie/request uniqueness, accept/reject/defer, incumbent preservation, independent revalidation, ACK-only commit |
| `009` | test-only `ExactSmallOracle` | 32 deterministic seeds; feasible set/cost/accepted/outcome equal, generator gap 0, selection gap 0 trong bound 2×2 |
| `010` | typed online action codecs/schemas, online `RunnerSession`, full state canonicalizer | produced actions, exact retry, premature batch reject, matching/wrong ACK, two-epoch chain, tamper hash, Q1 named conformance |
| `011` | `benchmarks/scenarios/wp2-tiny` + `run-wp2-tiny-demo.ps1` | two byte-exact clean processes, accept/board/capacity-reject/drop/alight, final hash locked |
| `012` | ADR-020 + docs `00/15/16/18/19/23/26/27` | scope/gate/next-action audit; one WP3 refinement ready |

Evidence ngày 2026-07-30:

- logical inventory 333 — Contracts 128, Domain 89, Application 15,
  Algorithms 45, Runner 49, Architecture 7;
- required Debug `dotnet test RideBound.slnx` pass 333/333;
- Release xUnit pass Contracts 128, Domain 89, Architecture 7; Application 15,
  Algorithms 45 và Runner non-process 46 pass bằng policy-safe bundles, ba
  Runner child-process checks pass riêng;
- Release `--warnaserror` build và format verification pass;
- tiny demo chạy 2/2 clean self-contained process, byte-exact; final hash
  `56825f3591fb5d10f4c258d2c05897c016d82cb91c1318ffa23731c920146680`;
- Release full-solution xUnit bị Windows Application Control chặn fresh unsigned
  Application/Runner DLL bằng `0x800711C7` trước assertions ba suite. Đây là
  environment exception, không được tính là Release xUnit pass;
- F-001–F-004 và phần physical/B1 của Q2 đã implemented/verified trong published
  bounds. F-005–F-009, P1/P2/P3 commitment, ledger/certificate/checkpoint vẫn
  thuộc WP3; C1/OR-Tools thuộc WP4.

## 10. WP3 traceability

| Ticket | Requirement chính | Evidence/trạng thái |
|---|---|---|
| RB-WP3-001 | R-007–R-009, R-015, F-005–F-009, N-003/N-004/N-007 | DONE: ADR-021 + queue `28` |
| RB-WP3-002 | R-007/R-008, F-005/F-006, N-009 | DONE: promise/policy/vector types + Domain tests |
| RB-WP3-003 | R-003/R-008, F-004/F-005, N-001/N-002 | DONE: shared schedule, promise projection, 45 Algorithms regression |
| RB-WP3-004 | R-007/R-008, F-005/F-006, N-001/N-003 | DONE: all-dimension three-way delta/distance witness |
| RB-WP3-005 | R-007/R-008, F-005/F-006, N-003/N-004 | DONE: initial/revision ledger, P1 conservation, pending/ACK atomicity |
| RB-WP3-006 | R-007/R-009, F-006/F-007, N-003 | DONE: exact boundary cho 10 dimensions, zero/unbounded, 441 monotonic samples |
| RB-WP3-007 | R-007/R-009, F-007, N-003 | DONE: accepted/onboard/freeze/final-confirmation lock witnesses |
| RB-WP3-008 | R-009, F-008, N-003/N-004 | DONE: incident lifecycle, affected-rider derivation, immutable breach ledger/chronology |
| RB-WP3-009 | R-007–R-009, F-007, N-003 | DONE: independent physical→state-boundary→lock→budget validator and candidate filter |
| RB-WP3-010 | R-009, F-005/F-007/F-008, N-001/N-009 | DONE: strict certificate/action codecs and schemas with cross-binding checks |
| RB-WP3-011 | R-003/R-007–R-009, F-005–F-008, N-001/N-004/N-009 | DONE: commitment mode, named policy hash, atomic publication/state/hash/ACK |
| RB-WP3-012 | F-009, N-001/N-004/N-007/N-009 | DONE: canonical checkpoint/restore, hash/tamper/reachable-state validation |
| RB-WP3-013 | R-007–R-009/R-018, F-005–F-009, N-001/N-003/N-007 | DONE: all-dimension mutations, 64×12 ledger histories, 16-seed exact-small P2/P3, clean replay |
| RB-WP3-014 | R-007–R-009/R-015/R-018, F-005–F-009 | DONE: ADR-022 closure, code/research review and `RB-WP4-001 READY` |

Evidence closure: inventory 414 — Contracts 133, Domain 134, Application 34,
Algorithms 48, Runner 58, Architecture 7. Required `dotnet test RideBound.slnx`
pass 414/414 ngày 2026-08-03; policy-safe 54 Runner non-process methods và bốn
clean-process cases vẫn là evidence bổ sung. Release build `--warnaserror`,
format, WP1/WP2/WP3 exact replay, checkpoint suffix, schema/link/dependency/diff
audit được ghi trong `18`. O-002/O-003 vẫn để WP8; không có runtime user-default
giả. Các lần `0x800711C7` trước đó chỉ là historical host-policy record.

## 11. WP4 traceability

| Ticket | Requirement chính | Trạng thái/evidence dự kiến |
|---|---|---|
| RB-WP4-001 | R-003/R-004/R-007–R-009/R-012/R-013/R-015, F-004–F-007, N-001–N-005/N-007/N-009 | DONE: Browser research + ADR-023 + ordered queue `30`; không production code trong refinement |
| RB-WP4-002 | F-004, N-001/N-002/N-005/N-009 | DONE: canonical solver-neutral problem/solution/port; exact assignment/request constraints, ordered Sum/Maximum objectives, deterministic budget, truthful bound/gap/status; 435/435 |
| RB-WP4-003 | F-004, N-001/N-005 | DONE: conservative backward slack, full cache key/invalidation, executable current-node hold + physical/exact-service revalidation; 444/444 |
| RB-WP4-004 | F-004, N-001/N-005 | DONE: deterministic best-first/work cap, exact fail-closed, request/raw-unknown/feasible-cap loss count+digest; 449/449 |
| RB-WP4-005 | R-007/R-008, F-003/F-004, N-001/N-003 | DONE: B2 non-pruning revision lexicographic + B3 explicit inclusive freeze/unbounded cumulative; 16-seed raw preservation; 458/458 |
| RB-WP4-006 | R-007/R-008, F-003/F-004, N-001/N-003 | DONE: B4 atomic one-pair same-vehicle waiting-incumbent repair, exact/bounded repair-loss accounting, order-sensitive search identity; 465/465 |
| RB-WP4-007 | R-007/R-008, F-003–F-007, N-001/N-003/N-005 | DONE: versioned canonical B5 pool, dominance/diversity/consensus, executable alternative rebase, checkpoint tamper gates; 477/477 |
| RB-WP4-008 | R-007/R-008, F-003–F-007, N-001/N-003 | DONE: one-pass hard gate + exact cumulative worst PPM + ordered revision/cost/ID; unbounded C1=B1; 483/483 |
| RB-WP4-009 | R-007/R-008, F-003–F-007, N-001/N-003 | DONE: explicit 10-vector warning profile/excess before revision, same C1 hard set, disabled C2=C1; 489/489 |
| RB-WP4-010 | R-003/R-013, F-004, N-001/N-002/N-005/N-009 | DONE: isolated pinned OR-Tools 9.15.6755 CP-SAT adapter, exact constraints, multi-pass optimum fixing, deterministic budgets, truthful status/bounds; 495/495 |
| RB-WP4-011 | R-003/R-013, F-003–F-007, N-001–N-005/N-009 | DONE: separate stage work budgets/loss, independent incumbent validation, no-op→single-request fallback, fail-closed exhaustion; 507/507 |
| RB-WP4-012 | R-003/R-013, F-003–F-007, N-001–N-005/N-009 | DONE: seven-name registry, manifest/config hash binding, exact solver objective mapper, Runner validator/certificate/hash/ACK/checkpoint/CLI integration; 523/523 |
| RB-WP4-013 | R-012/R-015/R-018, N-001/N-003/N-005/N-007 | DONE: 64-seed B1 oracle; 64-seed production C1 mapper + actual OR-Tools independent differential, all levels optimal/gap 0; hard-gate mutation, actual bounded-loss propagation, synthetic 4–128 option curve; 557/557 |
| RB-WP4-014 | R-012/R-015/R-018, N-001/N-003/N-005/N-007 | DONE: ADR-024, source/config/Runner/claim audit, final WP1–WP4 review, all quality gates và only `RB-WP5-001 READY` |
| RB-WP5-001 | R-003/R-013/R-015/R-018, N-001/N-003/N-007/N-009 | DONE: ADR-025 khóa BeGo source/provenance, Runner ownership, transaction/recovery, persistence, rollback và paired Layer-1 protocol; no production implementation |
| RB-WP5-002 | R-002/R-013/R-015, N-003/N-004/N-009 | DONE: pure BeGo Application contracts/ports; exhaustive run/operation transition, idempotency fingerprint, strict UTF-8/frame/hash/order/checkpoint guards; 32 targeted, BeGo 57/57, RideBound 557/557 |
| RB-WP5-003 | R-002/R-014, N-003/N-004/N-008/N-009 | DONE: 11-table EF/migration, composite same-run FK, canonical bytes/range/time columns, five append-only DB triggers, guarded rollback; PostgreSQL 17 real gate |
| RB-WP5-004 | R-002, N-001/N-003/N-004/N-005 | DONE: T1 row-locked intake, exact frame binding/idempotency, bounded DB queue, ordered `SKIP LOCKED` lease/reclaim with database time; PostgreSQL race 5/5, Release 40/40, BeGo 64 pass/1 opt-in skip, RideBound 557/557 |
| RB-WP5-005 | R-002/R-003, N-001/N-002/N-005/N-009 | DONE: pinned hash/core, bounded long-lived Runner pool, strict NDJSON/schema/context, timeout/tree cleanup, exact ACK/checkpoint critical section; real published Runner online gate |
| RB-WP5-006 | R-002/R-003/R-013, N-001/N-002/N-008/N-009 | DONE: immutable pre-I/O source capture, run-local HMAC pseudonyms, restricted raw-ID links, E7/ms ties-to-even, complete bounded directed matrix, field provenance, exact negotiated manifest/domain hash; real Runner bootstrap; BeGo 98/98 |
| RB-WP5-007 | R-002/R-014, N-004/N-006/N-008/N-009 | DONE: host/member auth, strict bounded HTTP DTO, semantic fingerprint independent server sequence, composite advisory-locked create idempotency, exact cached response, RFC Problem Details/rate gate, PostgreSQL→published Runner API path; 116/116 |
| RB-WP5-008 | R-002/R-003/R-018, F-001/F-005–F-009, N-001/N-003/N-004/N-007 | DONE: atomic T2 decision/certificate/projection/outbox, fenced matching ACK/T3 checkpoint, exact fresh reconstruction, typed divergence; 8 real crash windows, BeGo 125/125, RideBound 557/557 |
| RB-WP5-009 | R-002/R-013, N-003/N-004/N-005/N-006 | DONE: per-run-head DB-time lease/fence, no-lock SignalR I/O, stable sequence/message/hash wire envelope, client duplicate/stale suppression, safe payload/auth gates; real PG + Runner 131/131 |
| RB-WP5-010 | R-002/R-018, F-010, N-003/N-005/N-007/N-008 | DONE: exact keyset/member ownership, operator-only raw evidence, append-log rebuild/live hash, pseudonymous export, privacy/log mutations, indexed PostgreSQL plan; 138/138 |
| RB-WP5-011 | R-002/R-014, N-006/N-008/N-009 | DONE: default-off DI/API, exact artifact preflight, immutable Shadow/Live namespace, namespace-filtered decision claim, live-only relay, restart recovery, old Session snapshots unchanged; 147/147 |
| RB-WP5-012 | R-003/R-004/R-018, N-001/N-002/N-007/N-009 | DONE: exact source/config/binary preflight; B1/C1 × two clean processes; common normalized input, exact repeat hashes, materialized certificates, independently checked checkpoints, self-verifying source/assembly-bound bundle; BeGo 152/152 |
| RB-WP5-013 | R-012/R-013/R-018, N-001/N-003–N-007 | DONE: 16.384-step independent oracle, exact-set 2/3/4-worker claim, actual process crash at 8+4 boundaries, 5/5 explicit mutants, raw local curves + self-verifying artifact; 153/153 |
| RB-WP5-014 | R-012/R-015/R-018, N-001–N-009 | DONE: source/claim/evidence audit; subject-link append-only, absolute-head-then-Applied/non-null operation outbox và independent scoped per-run batch; WP1–WP5 review/verdict; BeGo 154/154 Debug/Release |
| RB-WP6-001 | R-010/R-011/R-015/R-018, N-001/N-002/N-007/N-009 | DONE: ADR-026 + equivalent contract v1 + primary-source evidence + ordered queue; no implementation/experiment result |
| RB-WP6-002 | R-010/R-018, N-001/N-007/N-009 | DONE: 10 strict codecs/models + semantic validator, Draft 2020-12 schema inventory, fixtures, two-process six-identity vector; targeted 28/28, full 586/586 |
| RB-WP6-003 | R-010/R-018, N-001/N-007/N-008/N-009 | DONE: exact Zenodo length/MD5/SHA registry, resumable verified content cache, safe ZIP extraction, actual 335-member public receipt; targeted 26/26, full 612/612 |
| RB-WP6-004 | R-010/R-018, N-001/N-002/N-007/N-009 | DONE: ADR-027 acyclic scenario/report identity; verified-member FleetPy normalizer, SCC/dense-node-pool/HMAC/ties-even/conservation; exact 8/2/16/240 + 128/32/96/9120 derivatives; full 619/619 |
| RB-WP6-005 | R-010/R-018, N-001/N-002/N-007/N-009 | DONE: cross-process framed HMAC/int32 vectors, effective binding + strict pairing compiler, permutation/parallel counterbalanced full grid; targeted 32+37, full 627/627 |
| RB-WP6-006 | R-010/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-028/contract 1.0.2; exact runtime/deploy/config/source pre/postflight, canonical Runner protocol + independent hash-chain checks, typed bounded process-tree/stream evidence; 15 supervisor cases, full 647/647 |
| RB-WP6-007 | R-010/R-011/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-029/contract 1.0.3; immutable plan/run/raw/index store, typed terminal/log chain, crash/seal/concurrency recovery, plan-bound protocol/artifact verification and full-grid authorized rerun; Benchmarking 77/77, full 673/673 |
| RB-WP6-008 | R-010/R-011/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: 36-definition registry; arrival-cohort/decision-window/lifecycle/resource calculator; 132 exact rows; no-reference external oracle; request/action/promise/vector/window/order/resource/overflow mutations; Benchmarking 86/86, full 682/682 |
| RB-WP6-009 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: deterministic strict BagIt builder; logical/source/runtime/run-store provenance; 10-stage semantic verifier; fresh-process immutable sidecar; missing/extra/tamper/path/reparse/grid/transcript/log/metric mutations; Benchmarking 92/92, full 688/688 |
| RB-WP6-010 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-032; source-locked/generated claim profile/report; scoped Unicode/confusable-aware typed checker; exact stage-10 recomputation; forbidden/caveat/report/provenance/profile mutations; Benchmarking 95/95, full 691/691 |
| RB-WP6-011 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-033/contract 1.0.4; B1/C1 × 3 exact external Runner runs in two clean Release processes; real non-zero decision delta, exact semantic identities, oracle summaries, typed mutation matrix, verified mechanical bundle; Benchmarking 104/104, full 705/705 |
| RB-WP6-012 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-034/contract 1.0.5; verified Zenodo + two-clean-root exact 128/32/96/9120 derivative; B1/C1 × 3 in two fresh exact Runner/store/oracle/verifier processes; exact semantic identities, valid strict bundles, explicit nonphysical instant-drain boundary; full 710/710 |
| RB-WP6-013 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-035/contract 1.0.6; executable 1+3 warm-up/measured grid, complete preflight, 10-doc permutation/parallel, 21 failure-stage/8 exclusion plus process/store/metric/bundle/claim/source matrix; medium D/E 8/8 semantic exact, strict verify valid, full 770/770 |
| RB-WP6-014 | R-010/R-011/R-015/R-018, N-001/N-002/N-005/N-007/N-009 | DONE: ADR-036; source/claim audit, fresh tiny A + medium H/I exact-source semantic repeat, external verifier, final WP1–WP6 review, exact 770/770 |

Queue `30` đã complete và ADR-024 đóng WP4. WP3 validator/certificate tiếp tục
là publication gate độc lập. ADR-025/`tasks/32` đã complete và đóng WP5;
`RB-WP5-001..014` Done. `RB-WP6-001..014` Done; WP7 `RB-WP7-001..014` Done và
ADR-038 đóng mechanical Layer-2 theo `tasks/36`. WP8 `001..014` Done bằng ADR-043;
WP9 queue `tasks/39` đang active, `RB-WP9-001/003` Done, `RB-WP9-002` Ready.

WP4 closure evidence: required `dotnet test RideBound.slnx` pass 557/557 ngày
2026-08-03 — Contracts 133, Domain 135, Application 69, Algorithms 134, Solver 6,
Runner 71, Architecture 9. `RB-WP4-002` thêm 20 Application adversarial cases + một
Architecture boundary case; `003` thêm 9 slack/cache/hold mutation-equivalence
cases; `004` thêm 5 priority/conservation/loss/monotonic cases; `005` thêm 8
B2/B3 cases và 1 Domain overflow regression; `006` thêm 7 B4 repair/exclusion/
loss/equivalence cases; `007` thêm 3 Application + 5 Algorithms + 4 Runner cases
cho plan identity/version, dominance/diversity/consensus, work bound,
distinguished-only publication và checkpoint/tamper. Observed wall time không
tham gia deterministic key. `008` thêm 6 Algorithms cases cho C1 objective,
one-pass hard-filter equivalence, exact PPM và unbounded B1 equivalence; `009`
thêm 6 Algorithms cases cho warning ordering/config/hard-set/excess/equivalence;
`010` thêm 5 solver cases cho constraints/objective/status/replay/overflow/budget
và 1 architecture case khóa package vào đúng adapter project; `011` thêm 12
Application cases cho stage accounting, truthful incumbent status, independent
validation, ordered no-op/single-request fallback, exhaustion và loss separation;
`012` thêm 7 Algorithms + 9 Runner cases cho objective mapper, config/hash/manifest
binding, actual OR-Tools/fallback status, ACK transaction, B5 restore và child CLI.
`013` mở B1 oracle lên 64 cases, thêm một 64-seed actual OR-Tools C1 differential,
hard-gate mutation witness, actual bounded omission propagation và synthetic
microbenchmark; `014` audit source/claim/gates và tạo final review folder.

## 15. ADR-039 traceability — semantic lock và hot-path closure

ADR-039 không thêm requirement mới. Nó đóng hai lỗ hổng truy vết: một nhóm hành vi đã
tồn tại trong source nhưng chưa có quyết định nào khóa, và một cổng hiệu năng chưa từng
được đo.

| Hành vi | Nơi thực thi | Bằng chứng |
|---|---|---|
| `initialPromiseTrigger` hai giá trị, mặc định `initial-acceptance` | `Wp4RunnerConfiguration.Decode`, `CommitmentDecisionValidator` | `Wp4RunnerConfigurationTests`; config thiếu field giữ nguyên content hash |
| Provisional offer không mở promise và bị loại khỏi hard/warning scope | `CommitmentDecisionValidator`, `HardVectorCandidateAssessor` | tiny clock actual: đúng một publication `INITIAL_BOOKING_CONFIRMATION` |
| Promise mở tại `Accepted → WaitingPickup/Onboard`, kể cả khi board cùng batch | `PromiseProjector` khôi phục pickup đã thực hiện từ frozen prefix | `PromiseProjectorTests` |
| Lock so trên trục exogenous → candidate | `CommitmentLockEvaluator` | `CommitmentLockEvaluatorTests`; `docs/07` §10 |
| `OfferDeclined` sau accept là cancel-after-acceptance | `EventReducer` | `EventReducerTests` |
| `C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE` fail-closed, witness typed | `HardVectorCandidateAssessor`, `CommitmentFailureCodes` | `C1_fails_closed_with_a_typed_witness_when_a_vehicle_loses_every_candidate`, `C1_fails_closed_when_a_vehicle_set_carries_no_candidate_at_all` |
| `--maximum-line-bytes`, `--manifest-solver-seed` | `Program`, `Wp4RunnerConfiguration.CreateSolverPolicyOptionsForRun` | `Wp4RunnerConfigurationTests`, `Wp4RunnerIntegrationTests` |
| Event-induced plan update chỉ khi opt-in và route thực sự đổi | `OnlineDecisionActionMapper` | `Wp4RunnerIntegrationTests` |
| Retention legacy fast path giữ nguyên hot path WP1–WP6 | `CandidatePortfolioRetainer` | `CandidatePortfolioRetainerTests` |
| Tối ưu hot path không đổi kết quả | `CandidateIdentity`, `ForwardSlackCacheKey`, `RoutePlan.Create`, `InsertionCandidateGenerator` | `CandidateSearchWorkProfileTests` khóa work unit / evaluated path / feasible-before-cap / omitted path / retained count / số slack profile |

Closure evidence: required `dotnet test RideBound.slnx` pass `798/798` ngày 2026-08-17 —
Contracts 135, Domain 136, Application 73, Algorithms 154, Runner 77, Benchmarking 135,
Benchmarking.Contracts 71, OrTools 7, Architecture 10. Pinned Python adapter `50/50`.
Actual FleetPy gates chạy lại trên Runner v8; chi tiết ở
[`wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md`](benchmarking/wp7-015-hot-path-and-semantics-closure-evidence-2026-08-17.md).

## 16. ADR-043 traceability — WP8 closure và WP9 freeze

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Experimental unit không nhân seed/rider | `ExperimentalUnitModels`, `wp8-007`, fixed panel 20 cell | same-unit/orientation/denominator mutations |
| Primary burden và service gate | production calculator + BCL-only oracle + `wp9_fixed_panel_analyze.py` | canonical differential, strict 1 pp boundary, burden không cứu service |
| Fairness treatment-only | `EffectivePolicies`, audited B1/C1 configs | direct policy tests + B1 tight/unbounded behavioral falsification |
| Locked/earned disclosure | pickup/drop exact components trong primary analyzer | treatment pickup khác zero fail-closed; exact integer decomposition |
| Bundle đúng arm/cell/source | execution plan + label + scenario SHA + repository inventory | swapped arm/seed/job/path/source mutations |
| Reproducibility freeze | `WP8-011a/011b/011c`, `freeze-receipt-v3.json`, freeze verifier | 25 explicit file/Runner hashes + derivative/scenario/Runner tree seals PASS |
| Claim boundary | `docs/03`, `docs/21` full-PDF audit, WP1–WP8 final review | không novelty/population/SLA/satisfaction claim |

Baseline hiện hành trước WP9 smoke: `.NET 840/840`, pinned Python 77/77, H4
`2f7e6bf36c16784e06cb3266f9764f3103f2de6fc931f3c8e023bdc1a81a32dd`;
Layer-1 mechanical 8/8 PASS và không được nâng thành effectiveness evidence.

## 17. ADR-045 traceability — ngữ nghĩa service-quality và capacity stratum

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Safety no-op không bị xoá bởi giao thông | `ProbeServiceQuality` + `ServiceQualityAllowance` trong `PhysicalPlanValidator` | 2 regression Domain (ride-time, pickup-window) + 1 regression Algorithms (no-op sống sót) |
| Không rửa vi phạm qua breach | bound `max(contractual, exogenous)` dùng chung ở validator và `ForwardSlackProfile` | Domain: candidate detour bị prune, `Expected` là bound hiệu lực; Algorithms: mọi prune `MAX_RIDE_TIME` đều strictly worse than no-op |
| Structural vẫn fail-closed | probe trả witness thay vì allowance khi lộ trình vi phạm structural | Domain: `CAPACITY` trên active route vẫn fail, allowance rỗng |
| Request mới không được miễn trừ | allowance chỉ có entry cho request trên lộ trình đang chạy | Domain: scoping theo request/dimension; không breach thì `Strict` |
| Đối xứng hai arm | allowance là hàm thuần của `(run, vehicle, travel, time)`, không đọc policy/ledger | ADR-045 §6; bốn call site downstream cùng `ValidateWithExogenousRelief` |
| Breach được ghi nhận, không nuốt | `ExogenousServiceQualityBreach` trong `CandidateGenerationDiagnostics` | Algorithms: breach báo đúng contractual/exogenous; không breach thì rỗng |
| Kết luận có điều kiện theo năng lực | `wp8-011d`, `wp8-010` gate theo từng stratum, `wp8-008` bảng 4/8/16 xe | 20 cell × 2 arm × 2 stratum; mẫu số 2160 riêng từng stratum; cấm claim phổ quát |
| Độ chính xác đạt được được công bố | `wp8-007` đo lại độc lập panel; `wp8-010` bắt in ~1,40 pp cạnh mọi gate | 2.157/2.160 request phân biệt; 5 travel realization; sàn sign-flip 0,03125 |
| Falsification đúng nhân quả | `wp8-010` thay negative control bằng identity chuỗi demand+travel 4 điều kiện | verifier phải PASS cả 4 trước khi cặp vào estimand |

Baseline sau ADR-045: `.NET 851/851` Debug và Release, Release `-warnaserror`
0 warning. `H4` hết hiệu lực vận hành vì ADR-045 và `wp8-011d` đều outcome-bearing;
`RB-WP9-002a` phải repin `H5` trước audited smoke.

Đã đóng bởi ADR-049/RB-WP9-009: evidence v1.1 serialize breach và runtime append
`ExogenousServiceQuality` ledger không charge budget. H6 vẫn dùng v1.0 nên không
có breach count confirmatory; 43 observation từ probe v1.1 chỉ là post-outcome
mechanism evidence.

## 18. ADR-046 traceability — đường phân tích confirmatory

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Analyzer chạy được trên artifact đã đóng băng | `_PRIMARY_ARMS` registry trong `wp9_fixed_panel_analyze.py` | `FrozenManifestBindingTests`: 20/20 cell của manifest thật bind vào plan thật |
| Không phân tích được panel cụt | manifest cell set phải bằng plan primary/robustness cell set, cả hai analyzer | test panel completeness + `denominators differ` |
| Orientation arm không đảo được | `_PRIMARY_ORIENTATION` pin `(b1, c1)` | test swapped orientation bị từ chối |
| Một bundle không phục vụ hai arm | `baselineBundle != treatmentBundle` | test duplicate bundle bị từ chối |
| Hai panel không lẫn nhau | `--panel` parameterize prefix/driver/fixture root; `_validate_frozen_design(plan, panel)` | `CapacityPanelBindingTests`: plan A bị từ chối làm panel B và ngược lại |
| Panel B chia sẻ demand realization của Panel A | đo 20/20 selection frame + request tuple | ghi trong `wp8-011d`; fail closed nếu lệch |
| Freeze bind cả hai panel | `H5` schema 4.0.0: 30 file hash + derivative A/B, scenario plan, Runner tree seal | verifier PASS; 4/4 mutation rejected |

Baseline sau ADR-046: `.NET 851/851` Debug và Release (Release `-warnaserror` 0
warning); pinned Python **86/86**, 0 skip với `RIDEBOUND_FLEETPY_ROOT` đặt sẵn.
`H5=6720accacdb6c1f685d9d27a196a7aa924cada02f535b35d7787074a5e625da6`.

Bài học giữ lại: freeze receipt pin *hash* của một program không chứng minh program
đó *chạy được* trên artifact nó pin. Mọi program nằm trên đường outcome phải có ít
nhất một test thực thi nó trên đúng artifact đã đóng băng.

## 19. ADR-047/048 traceability — H6 và kết quả WP9

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Adapter rounding không lạc quan | `ROUND_FLOOR` trong FleetPy mapping + `DivideRoundUp` Runner | boundary/mutation mapping tests; adapter package tree seal H6 |
| Relief chỉ dành cho safety no-op | changed candidate dùng `ServiceQualityAllowance.Strict` | Algorithms regression; 3/3 diagnostic job từng hỏng hoàn tất |
| Freeze outcome-bearing đầy đủ | H6 schema 5.0.0 | 30 file hash + derivative A/B, scenario, Runner, adapter tree seal PASS |
| Primary result đúng orientation | two canonical panel analyses | 20/20 cell mỗi panel; exact denominators; service và burden gate độc lập |
| Không oversell burden | locked/earned table cạnh service loss | pickup definitional component và zero-burden cell count bắt buộc trong report |
| Robustness không rescue | analyzer output `confirmatoryGate:null` | C1 unbounded/C2/seed19 descriptive only; seed tăng N bằng 0 |
| Reproducibility raw | `wp9_reproducibility_verify.py` + compact JSON receipt | 100/100 bundle; bốn falsification conditions; repeat deterministic; 5 mutations |
| Claim boundary | `wp9-confirmatory-result-2026-08-23.md` | finite panel, 5 travel realization, precision ~1.40 pp, cấm population/SLA/satisfaction |

## 20. ADR-049 traceability — breach evidence/ledger

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Evidence tương thích ngược | mapper v1.1; Contracts/Python nhận 1.0/1.1 | v1.0 H6 pass; unknown version và bad v1.1 shape/value/order fail |
| Không incident giả | `CommitmentBreachKind.ExogenousServiceQuality`, nullable incident chỉ cho kind mới | Domain append succeeds without incident; operational path unchanged |
| Không rửa decision budget | exact no-op exogenous=safety, decision delta zero, budget unchanged | Domain forged projection/delta/budget/witness mutations fail |
| Runtime không nuốt breach | `ExogenousServiceQualityBreachBridge` trước state staging/hash | fail-session on bridge error; real FleetPy 43 evidence = 43 ledger |
| Checkpoint tương thích và strict | old operational shape unchanged; new kind/witness array | exact round-trip + forged serialized budget rejection |
| Outcome boundary | post-outcome evidence report/receipt | 43 observations không trộn H6 và không rescue primary |

## 21. ADR-050 traceability — WP10 RidePy Layer 3 refinement

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Exact independent source | RidePy v2.10.1 `bf1863e…9f14`, exact two submodules, MIT, external checkout | commit + 527-file tree + license/submodule mutation verifier |
| Supported isolated runtime | pinned Linux container base `a365ce6a…0e235` | image build/import/version/runtime receipt; Windows wheel absence recorded |
| Same decision implementation | RidePy adapter calls versioned `RideBound.Runner`; no Python solver/budget/locks | publish-tree pre/post hashes + transcript proof |
| Honest capability | `nodeOnly`, unit party, reassignment false, explicit traffic updates | negotiation/downgrade/mapping/mutation tests |
| Native simulator semantics | subclass `FleetState`, native fast-forward/space/pickup/drop | 2-vehicle/5-request canonical reconciliation |
| Fair Layer 3 comparison | B1/C1 paired on exact same exogenous inputs | frozen subset identity verifier; oriented per-cell analysis |
| Outcome boundary | descriptive heterogeneity only | no pooled H6 gate/CI/SLA/satisfaction/novelty claim |

## 22. ADR-051 traceability — WP10 negative capability closure

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Reproducible source/runtime | external source/env receipt `2b431062…0775`; image `5468b9cb…e573` | source mutations, actual import/Graph smoke và 8/8 verifier tests pass |
| Generic same-Runner client | explicit position/capability parameters, unchanged FleetPy defaults | 14/14 targeted regression; wrong downgrade fails |
| Strict RidePy adapter | identity/time/travel/route mapping; incremental native clock; atomic stoplist apply | pinned-container suite 20/20; no Python solver/lock/budget implementation |
| Canonical equivalence | B1/C1 5/5 completed, 5 pickup + 5 drop, 22 decisions | independent five-class mutation verifier; Runner tree pre/post identity |
| Pre-outcome subset binding | repo manifest `72ca34d2…b337`; external freeze v3 `18a74fa3…6672` | source/image/Runner/adapter/config seals before terminal execution |
| Failure retention/no partial reuse | 22 pass, B1 stress job fail at epoch 17, paired C1 not run | exact failure transcript `0ee5e3ec…a85`; no missing-job denominator laundering |
| Named capability boundary | `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED` | native pickup 116000 ms vs last nodeOnly ETA 178000 ms; no invented progress |
| Descriptive analysis only | 11 valid pairs: B1 54/62, C1 49/62, `−8.06 pp` | strengthened analysis v2 `be3e9077…cca3` binds exact terminal inventory/full Runner/seed; not planned estimand, no CI/pooling/H6 rescue |
| Layer 3 verdict | representative subset gate FAIL CLOSED | WP10 complete negative; cross-system Layer 3 claim not established |

## 23. ADR-052 traceability — final review và exact-reuse optimization

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Full paper thay vì title-only | six local full PDFs, page count + exact SHA-256 in `docs/21` | every page extracted/read; applied/rejected mechanism recorded |
| Không nhập heuristic thiếu loss bound | no subtree/direction/random/sparse prune | search work/evaluated/feasible/omitted/retained counters unchanged |
| Cache identity không yếu đi | immutable run/vehicle refs + exact route/time/travel/allowance comparison | moved vehicle/run/route/travel tests miss; cached/uncached equivalence pass |
| Terminal reuse không bypass validator | `ForwardSlackCacheKey.Matches`; `_validator.Validate` remains before profile use | physical-capacity regression still prunes despite injected successful profile |
| Benchmark có raw provenance | `RideBound.CandidateHotPathBenchmark`; 3 baseline + 3 optimized JSON hashes | exact allocation and six semantic counters; timing labelled machine-local |
| Review không chỉ dựa test count | 1,226-file full-tree scan + high-risk manual read + mutation/differential/actual simulators | `reviews/wp1-wp10-final` findings and residual-risk ledger |
| WP10 analyzer exact terminal inventory | strengthened v2 analyzer binds freeze/full Runner/seed/job/arm/failure | 7/7 mutation classes; analysis SHA `be3e9077…cca3` |
| Runtime restore honesty | exact Docker image archive SHA `4783c541…9a872`, load returns image `5468b9cb…e573` | restore PASS; no future byte-rebuild claim |
| Final quality baseline | isolated `.NET 855/855`, FleetPy 95/95, RidePy 23/23 | concurrent attempt's CPU guard retained; isolated rerun PASS; Release 0 warning/error; format/diff/vulnerability/static gates PASS |
| Separate BeGo baseline not hidden | read-only current dirty tree, no mutation | backend 149 pass + 5 explicit opt-in skip; frontend 9/9 |
| Proper rendered report | `output/pdf/RideBound-WP1-WP10-final-review-2026-08-23.pdf` | 12/12 pages rendered/visually inspected; SHA `06616887…9b20b` |

## 24. ADR-053 traceability — Post-H6 mechanism diagnostics

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| H6/WP10 không bị reinterpret | raw roots, freeze receipts, margins/panels/outcomes immutable | derived report read-only; no H6 artifact writes |
| Full-text research thật | Pillac 29, Ulmer 42, Ackermann 35 pages; exact local hashes | 106/106 pages extracted/read; first/middle/last render inspected |
| Cross-arm alignment đúng | equal-observed-input + wire/operational decision projections | policy-bearing state hash hoặc publication-ID reorder alone cannot trigger operational divergence |
| Không causal overclaim | inventory report claim boundary khóa `trajectoryAssociatedNotCausal` | future schema/aggregator phải giữ exact label; chưa claim implementation trước ticket |
| Evidence thiếu không được bịa | sufficiency matrix, explicit `notRecorded` | full retained portfolio/reranking requires evidence vNext |
| Paper không cấp default | no copied budget/horizon/slack/consensus/forecast | any v2 parameter requires WP14–WP16 development freeze |
| Progressive delivery | `tasks/41` refinement + `tasks/42` ordered queue | `RB-WP13-001..013 Done`, WP13 Complete; only `RB-WP14-001` refinement open |
| Canonical-number hot path | allocation-free three-marker span search in protocol, canonicalizer and independent oracle | exact marker semantics (`.`, `e`, `E`, `-0`) retained; targeted drain 1/1 and full Debug 856/856 pass without changing 120 s ceiling |
| Versioned first-divergence contract | `benchmarks/schemas/wp13/v1` + `wp13_first_divergence_records.py` + `wp13-003` evidence | 40/40 exact pairs; conditional no-null evidence; source/schema/tool/panel hashes; noncausal/read-only labels |
| Paired action-level comparison | `wp13_behavioral_comparator.py` + `wp13-004` evidence | 80 transcripts/44.156 decisions to EOF; 40/40 exact target bindings; immediate C1-lower 8, equal 32, C1-higher 0; descriptive/noncausal only |
| Recorded-witness clearance | strict schema + `wp13_recorded_witness_relaxation.py` + `wp13-005` evidence | 40 records/41 links; 28 exact numeric budget and 5 categorical lock clearances; 7 absent remain `notRecorded`; feasibility `notEvaluated` |
| First-divergence mechanism classes | strict schema + `wp13_mechanism_classify.py` + `wp13-006` evidence | 40 records/41 links; pair-level multi-label cross-tab; C1-lower 7 budget + 1 lock, equal 21 budget + 4 lock + 7 indeterminate; descriptive/noncausal only |
| H6 option-set sufficiency | strict schema + `wp13_option_set_sufficiency.py` + `wp13-007` evidence | 80 raw targets; generation exact-equal 40/40 and complete 80/80, zero cap/work/omission; full retained identities/routes/objectives remain `notRecorded`; vNext required but rerun not authorized |
| Runner retained-portfolio vNext | profile `retained-portfolio-v1`, defensive snapshot, exact selection problem, v1.2 mapper/strict decoder/schema + `wp13-008` report | profile-off v1.1 and H6 v1.0 compatible; generated/eligible/selected IDs, full route/schedule/objectives; capture differential; .NET 860/860, Python 168/168; no H6 write/rerun |
| Frozen exploratory retained-portfolio replay | E1 freeze receipt/configs/plans, matrix runner, independent inventory schema/tool + `wp13-009` report | exact 40 pair/80 arm, 8.640 requests, 44.156/44.156 v1.2 decisions, zero failure, 5.516.098.710 raw bytes; descriptive inventory only; H6 immutable |
| Independent E1 falsification | strict receipt schema + `wp13_e1_falsify.py` + `wp13-010` report | byte-exact 80-bundle/44.156-portfolio rebuild; exact 31/31 in-memory mutants rejected at expected layer and typed code; zero unexpected pass/failure |
| Instrumentation-only equivalence | `wp13_e1_h6_equivalence.py` + strict 80-record receipt | E1↔H6 same-arm behavioral projection equal 80/80, mismatch 0; semantic equality 0/80 expected; no mechanism/causal claim |
| Finite-panel candidate aggregation | policy-neutral semantic signature + strict `e1-candidate-descriptive-aggregation` schema/tool/report | 40/40 generated sets equal, 390 signatures/arm, zero collision/ID drift; 41 links = 33 pruned + 7 eligible-not-selected + 1 selected + 0 absent; objective profiles incomparable; rows overlapping/non-additive/noncausal |
| Full WP13 assurance audit | 80-file source-controlled inventory + strict `full-source-logic-claim-audit` schema/tool/report | deep H6 100 bundle/57.806 decisions and E1 80 arm/44.156 portfolios; zero failed DAG edge, zero unsafe conclusion, zero unresolved P0–P2; P2 verifier gap fixed by supplemental guard/regression without mutating frozen source |
| WP13 closure ownership | `RB-WP13-013` closure manifest/decision contract + `wp13-evidence-retention-and-successor-policy-v1` | seven exit gates pass; exclusive-create successor policy, 5.516 GB retain-and-freeze decision and versioned successor-verifier boundary all locked |
| Solver pass economy | ADR-067 + `CandidateSelectionProblem.ConstantObjectiveLevelValues` + `solverExecution.skipConstantObjectiveLevels` | opt-in and off by default, so every published configuration and every recorded hash is unchanged; when enabled the actions, selected candidates, state hash and every level optimum are invariant while recorded solver time falls honestly |
| Evidence inside the decision hash | `DecisionPayloadCodec.Encode(hashProjection: true)` | solver execution evidence is hashed, so any future change to recorded solver measurements must be opt-in rather than silently re-baselining published runs |
| WP14 factor design | ADR-066 + `tasks/43` + `wp14_mechanism_probe.py` + probe receipts | four factors rejected by measurement (one-sided charging, net anchoring, switch and relocation dimensions all produced zero witnesses); six factors F1–F6 accepted with explicit falsification conditions; H6/E1 measurements may explain the past outcome but must never select a future level |
| Treatment description boundary | probe objective measurement + `reviews/wp1-wp13-optimization-and-fairness` | `worst-hard-utilization-ppm` is constant in 100% of measured decisions, so C1 must not be described as ranking by commitment utilisation; the WP9 `lock/ranking` attribution is lock, not ranking |
| Burden claim boundary | probe promise-movement measurement | attributed decision-induced reduction is 99,83% Panel A and 99,23% Panel B, experienced published-to-published reduction is 88,85% and 91,43%, and 100% of riders in both arms still see their promise move at least once |
| Baseline strength boundary | O-001 + Alonso-Mora et al. full text | both arms forbid pre-pickup vehicle reassignment while the standard baseline permits it, so the measured service loss is a lower bound relative to a full-reassignment baseline; neither arm rebalances idle vehicles, so absolute completion rates are not comparable to published figures |
| WP14 authorization boundary | ADR-065 verdict `openExploratoryAblationOnly` | refinement only; new development namespace/cells, H6 Panel A/B excluded from tuning/selection, factor matrix/denominator/analyzer/resource envelope frozen before outcome, paired service–burden frontier not a single post-outcome scalar; H7 and lifecycle policy v2 remain unauthorized |
| WP14 pre-outcome design freeze | ADR-069 + `benchmarks/scenarios/wp14-development/freeze-receipt-v1.json` + strict Draft 2020-12 schema | exact 16 cell × 10 arm = 160 unique jobs, 108 arrivals/job, 1.728 arrivals/arm, paired-cell unit, seed 7 non-replicate; canonical/exclusive receipt SHA `1ce26ff0…37a55` |
| WP14 source/runtime integrity | 46 repository artifacts + adapter/fixture/Runner/DLL/.NET-runtime tree seals + FleetPy capability result | config/analyzer/runtime/source mutation after freeze is rejected before launch; FleetPy clean 1.0.2 commit, CPython 3.10.20, .NET 10.0.9 exact |
| WP14 raw-root isolation | exact four forbidden H6/E1 roots + seven-axis audit artifact | any output/report ancestor or descendant overlap fails; development 16 vs frozen 40 cells have zero overlap on all seven axes |
| WP14 matrix output binding | `wp14_run_matrix.py` + matrix summary schema + independent medium verifier | every new/reused bundle verifies behavioral hash/audited solver evidence and binds exact label/job/scenario/repeat; invalid/partial output has no retry/replacement; summaries exclusive-create |
| WP14 frontier semantics | `wp14_frontier_analyze.py` + strict frontier schema | transcript EOF + independent verify; duplicates rejected; integer-canonical rates/median; nearest-rank p95; pickup improvement/worsening split; Pareto only on completed↑ and attributed burden↓; no scalar/ranking |
| WP14 resource gate | ADR-070 + frozen envelope + `wp14-009-paired-dry-run-resource-gate` report/compact summary | FAIL CLOSED: B1 independently valid, C1 partial missing manifest; recovery 1 completed/1 failed with zero execution retry; partial retained, no retry/replacement; `010..014` unauthorized and no matrix/frontier exists under freeze v1 |
| WP14 full-text provenance refresh | in-app Browser + local `pypdf`/6-page visual render + 77-page inventory | MIT CSAIL Alonso-Mora PDF URL opens; local 6/6 pages have text and render correctly with SHA `edbb6215…4c12ea`; no paper numeric default copied |

## 25. ADR-071 traceability — WP14R resilient execution successor

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| WP14-v1 vẫn terminal | ADR-070 receipt/report và immutable external partial không bị sửa; WP14R namespace riêng | không retry/replace C1 partial, không sửa 46 freeze-bound file, không reinterpret resource failure |
| Full PDF thay vì title-only | Kalibera–Jones 12 trang + Mytkowicz et al. 12 trang trong external corpus; Browser cross-check author/publication pages | 24/24 trang extract nonempty, 24/24 render, exact file bytes/SHA và applied/rejected decision trong research note |
| Variation level không bị trộn | refinement `tasks/45`: host session → launcher process → simulator job → verifier | repetition/resource dimensioning chỉ ở `RB-WP14R-005`; không copy paper count, không dùng solver seed làm replicate |
| Setup bias có intervention | planned supervised metadata + fault injection `003/004` | repeated same setup không đủ; kill/timeout/orphan boundary phải có predeclared intervention và typed evidence |
| Attempt không phải experimental unit | start receipt fixed `attemptsAreExperimentalUnits:false`, inspection caveat | maximum 2 attempts/job; denominator/sample size không nhân attempt |
| Recovery không nhìn outcome | fixed `mechanical-validity-only-v1`, `mayReadScientificOutcomeToAuthorizeRecovery:false` | attempt 2 chỉ sau mechanical invalidity; pass chặn retry; attempt 2 fail exhausted |
| Immutable start/terminal DAG | `wp14r_attempt_ledger.py` + attempt-record schema | canonical exclusive receipts; exact previous/start hash; freeze/job/command binding không đổi; timestamps monotonic |
| Partial/log/output retention | per-attempt process log/output inventory, stable file measurement và post-terminal recomputation | renamed/extra/gap/link/junction/log/output/receipt mutations fail closed; không overwrite/delete cleanup |
| Valid success không được bịa | terminal semantic guards | bundle manifest + verifier ID/hash + behavioral hash + process exit 0 + clean tree bắt buộc; `notRun` không có verifier evidence |
| Command/environment exact nhưng không leak | `wp14r_supervised_process.build_command_binding` | executable bytes + args + cwd + allowlisted env values đều vào hash; journal chỉ có args/env hashes và names, mutation fail trước launch |
| Incremental bounded evidence | strict supervision journal schema + exclusive canonical NDJSON writer | start `fsync` trước child; bounded queue; raw non-UTF8 chunk base64/hash/sequence; per-stream caps và typed overflow |
| Host/process/tree state | host fingerprint + monotonic heartbeat/timeout + Windows Job Object/POSIX process group | nonzero/timeout/lingering grandchild/containment failure tách typed; ResourceWarning-as-error test sạch |
| Journal verification | `verify_process_log` + strict report schema | canonical/chain/time/chunk/base64/EOF/stream/terminal mutations, including resealed semantics, fail; only nonterminal truncated tail yields verified partial prefix |
| Exit zero không bypass bundle gate | `childExitedZeroAwaitingBundleVerification`, report claim boundary, ledger composition test | supervisor never reads `output/` and always emits bundle status `notRun`; ledger stays Open until manifest + independent verifier + behavioral hash |
| Durable launch ambiguity gate | `launchIntent` fsync trước Popen + childStarted containment/PID record | incomplete first record và intent-without-durable-child/type failure exhaust; uncertainty không authorize attempt 2 |
| Hard-crash process-tree evidence | `wp14r_fault_injection.py` + actual Windows matrix | 11 supervisor points + recovery crash point; child/grandchild absent dưới Job close; harness barrier không được dùng để nâng ledger claim |
| Immutable stale-open recovery | recovery receipt schema + `wp14r_stale_open_recovery.py` | launcher PID/source/start/command/log/tree bind; live owner/mutation/source drift fail; crash-window reuse exact bytes; exit-zero vẫn await verifier |
| Recovery disposition safety | classification-aware ledger gate | `launcherRecoveredOrphanedStart` cần treeSafe receipt; `processTreeUncertain` luôn exhausted; attempt 2 failure/attempt 3/success retry reject |
| Fixed mechanics resource corpus | `wp14r_resource_dimension.py` + strict report schema + three retained external roots | eight cells, one excluded pilot + five process repetitions/cell; every failure retained; no outcome read, CI, scalar score or between-host claim |
| Resource envelope | v3 canonical report SHA `44dce55e…78bce` | 48/48 pass; fixed 8 MiB verifier peak `65,875,968` B < 256 MiB; không ngoại suy two-stream hard cap |
| Evidence-driven optimization | compiled schema-validator cache + log/report schema SHA + WP14R schema-tree binding | legacy journal remains valid; partial/wrong provenance rejects; observed large launcher/verifier median `71.30/35.39 s` → `4.68/1.96 s`, descriptive only |
| Independent implementation verifier | `wp14r_independent_verify.py` + strict verification-report schema | không import/call ledger writer, supervisor verifier hoặc recovery classifier; tự dựng canonical ledger/journal/recovery semantics; read-only và không parse scientific outcome |
| Independent fixture/provenance | `wp14r_independent_fixture.py` + authoritative fixture receipt SHA `1e4a6450…372104` | clean complete-open, partial recovery và explicit legacy đều pass; current source/schema/tree hashes bind exact fixture |
| Windows path containment | reparse-point ancestry checks trong ledger/supervisor/independent tools | actual junction bị writer và verifier reject `PATH_UNSAFE`; Python 3.10 junction blindness có regression thật, không mock |
| Streaming/TOCTOU safety | streamed journal hash/count bind inventory; before/after stat + directory signature | injected journal byte và output entry giữa verification phases đều reject `INVENTORY_MISMATCH`; journal record ≤1 MiB |
| Independent mutation matrix | `wp14r_independent_mutation.py` + authoritative report SHA `9d8aacf4…72e1e` | 15/15 unique mutation class caught đúng expected typed code; valid fixtures vẫn pass; pilots/failures retained |
| Protocol freeze v2 | canonical receipt SHA `6b340108…a31237` + strict v2 schemas | tham chiếu byte-exact scientific freeze v1; 160-job order, pair B1→C1, parallelism 1, 20 GiB retained-output cap; 24 protocol/source/test files + three mechanics gates + 34 PDF pages bound |
| Stable recovery command | `wp14r_scientific_protocol.py` fixed wrapper | attempt 1/2 dùng cùng command/env/cwd hash; wrapper suy open-attempt output riêng, không overwrite; bundle validity → terminal/recovery → independent ledger verifier |
| Host conditioning | `wp14r_host_preflight.py` + strict receipt | exact Windows fingerprint, AC online, Balanced GUID, 10×1 s CPU gate, 8 GiB memory/25 GiB disk; không log arbitrary process name/command line |
| Progressive authorization | `tasks/46` ordered queue | `001..007 Done`; `008 Ready` nhưng launch chỉ khi exact preflight pass; `009..012` vẫn Unauthorized |
| Current verification | WP14R targeted 95/95; pinned Python/FleetPy 337/337 zero skip; required .NET 908/908; freeze-v2/v1 exact verify | JSON 1.350/1.350, schemas 93/93, Markdown 287 files/385 local links; format/diff pass (only historical LF→CRLF warning); typed `POWER_SOURCE_NOT_AC`, zero attempt/zero simulation |

## 26. Pre-launch analysis — WP14R, 2026-08-29

Không phải ticket và không authorize gì; ghi lại các invariant kiểm được **trước** khi
matrix tiêu hàng chục giờ máy.

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Analyzer đã freeze chạy được trên dữ liệu thật | gọi `wp14_frontier_analyze.read_bundle` trực tiếp trên bundle B1 `125.237.277` B | `readBundleSucceeded`; `semanticHash d072b931…c8393d` khớp independent verifier; `analyze()` vẫn đòi đủ 160 job nên không chạy được trước matrix |
| Giới hạn của metric improvement trong analyzer | `pickup_change` tính trên promise đã publish, không lọc `decisionDelta` | `pickupEtaImprovementCount` gộp exogenous drift; **không** dùng được để trả lời F2; analyzer bị freeze v1 bind nên phải bổ sung công cụ mới thay vì sửa |
| Chiều dịch chuyển do quyết định | `wp14r_promise_direction.py` + 7 test | E1 80 bundle: 916 decision-moved ETA publication, **0 sớm hơn**; dev-panel B1: 48 decision-moved, 0 sớm hơn |
| Cross-validation hai công cụ | analyzer `pickupEtaImprovementCount = 654` vs direction tool `exogenousOnlyEarlier = 654`, `decisionMovedEarlier = 0` | cùng bundle, số khớp tuyệt đối; chứng minh trực tiếp metric cũ không tách được nguồn dịch chuyển |
| Dự đoán F2 đăng ký trước outcome | report SHA `958843ef…3dad8869` và `b3d3dc10…45071a7` | `c1-ratchet` ≡ `c1-h6ref` và `c1-freeze300ratchet` ≡ `c1-freeze300` trên 16 cell; dự đoán **không** cho phép bỏ arm, freeze giữ exact 160 job |
| Cơ chế giải thích | insertion-only + O-001 cấm reassignment ở cả hai arm | quyết định chỉ thêm detour cho incumbent; stop-reordering về nguyên tắc có thể cải thiện nên không claim bất khả thi theo cấu tạo |
| Rủi ro trần retained output | `maximumOutputBytes` `21.474.836.480` B; `authorize_phase` hard-fail và cộng mọi attempt | chiếu ADR-070 đạt 93,31% trần; điểm hoà mỗi bundle C1 là `135.215.555` B trong khi B1 đã `125.237.277` B; vượt trần ⇒ freeze v3 trước outcome, không sửa giữa run |
| Driver không có thẩm quyền | `wp14r_matrix_driver.py` + 8 test | chỉ quyết định khi nào dừng; thứ tự/retry/gate do protocol cưỡng chế và verify freeze mỗi lần gọi; `--status-only` trên freeze thật cho đúng 160 job |
| Freeze không bị phá bởi file mới | rebuild-verify sau khi thêm hai tool | freeze v1 pass exact 160/46/`1ce26ff0…37a55`; freeze v2 valid exact 160/`6b340108…a31237` |
| Host blocker đã đổi | preflight receipt `preflight-attempt-01-observation-0001.json` | AC/scheme/CPU/disk pass; chỉ còn `MEMORY_BELOW_MINIMUM` thiếu `56.111.104` B; sleep/hibernate trên AC đều Never |

## 27. ADR-074 traceability — luật hạn chót tính từ lời hứa đầu (thăm dò, Proposed)

Nhánh `research/deadline-gate`, commit `65d867c`; không authorize `RB-WP14R-009..012`, WP15 hay H7.

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Mặc định tắt, không đổi run cũ | slack `null`, two-sided `false`; khóa cấu hình vắng ⇒ bytes/hash không đổi | `Drop_eta_deadline_is_disabled_when_absent`; `Without_a_slack_*`; review vòng 2 xác nhận byte-identical cho chính sách không solver-backed |
| Neo vào lời hứa **đầu**, không vào lần sửa gần nhất | validator truyền `Entries[0]` khi có hạn chót | 2 test sổ có một lần sửa; mutation neo vào lời hứa hiện tại ⇒ cả hai đỏ |
| Biên chính xác | cap khi `p − p0 > ℓ`; floor (hai chiều) khi `p0 − p > ℓ`; bằng ℓ được phép | test Domain trên biên ±1 ms, slack 0, `0` vs `2^53−1` |
| Trôi dạt một mình làm bắn được trên no-op | so với `p0`, không với mốc ngoại sinh | `Drift_alone_trips_the_deadline_on_the_safety_no_op`, `Favourable_drift_trips_only_the_two_sided_deadline_on_the_no_op`, 2 test Application |
| Không che khóa pha | witness hạn chót nối sau witness khóa | 2 test thứ tự (cùng chiều; khóa giờ đón + hạn chót giờ trả) |
| B1–B4 không nhận hạn chót | `MechanismCommitmentPolicyProvider` dựng lại policy | `EffectivePolicyFairnessTests` (+6) |
| Cấu hình sai thì hỏng to | two-sided không slack; slack 0/âm/chuỗi/`null`; cờ không phải boolean | 13 test cấu hình; `Program` thoát mã 64 |
| Chẩn đoán no-op chỉ ở đường solver-backed | cờ `requireSafetyNoOp` | test qua `SolverBackedRidePoolingPolicy.Decide` (solver không bao giờ được gọi); 2 test caller cũ giữ hành vi cũ; mutation tắt cờ ⇒ đỏ |
| Suite | `dotnet test RideBound.slnx` | 963/963; lần sau 962/963 với drain `resource.cpu-time-exceeded` (máy chạy pin) |
| Giới hạn đã biết | mã typed không tới artifact; công cụ WP13/WP14 không hiểu mã mới; đường không-WP4 chưa test; witness không mang độ lớn | ghi ở ADR-074 Consequences |
| Bộ chạy mới tái lập được kết quả cũ khi hạn chót tắt | 40 đối chứng `k = 0` so với `wp14/drift-v1` bằng `decision_identity.trace` | 36 ô hoàn tất trùng từng quyết định + số khách; đúng 4 ô cổng kết quả vô nghiệm |
| Định lý 4(a) trên hệ thật | 56 lần chạy cổng hành vi, mọi panel, `k ∈ {0, 1}` | 0 vô nghiệm do cam kết (thăm dò) |
| Dự đoán theo ô, niêm phong trước | `predictions.csv` SHA `6c5451a0…`, niêm phong 06:03:52Z | 98/102 (panel chính), 31/32 và 40/40 (tập giữ lại sáng/tối); kiểm độc lập khớp |

## 28. ADR-075 traceability — lõi nhận biết thất bại, giữ tuyến và ghi vi phạm (thăm dò, Proposed)

Nhánh `research/tier3-failure-aware`, commit `8d1ea4d`; không authorize `RB-WP14R-009..012`, WP15 hay H7.

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Mặc định tắt, run cũ không đổi | `commitmentRecovery` vắng ⇒ fail-closed; `ForcedReferenceVehicles` null; level mới chỉ thêm khi có phương án forced | `Forced_recovery_changes_nothing_when_no_gate_rejects_the_no_op`; `Without_forced_vehicles_the_validated_state_and_breaches_are_unchanged`; `Forced_reference_recovery_leaves_an_untriggered_decision_unchanged`; suite cũ 963 test vẫn xanh |
| No-op bị cổng loại không làm hỏng mô hình (T3.3) | no-op validate lại với xe forced, giữ làm phương án forced | `Forced_recovery_keeps_a_deadline_rejected_no_op_when_every_candidate_is_rejected`; test đầu-cuối Runner (fail-closed ⇒ `INTERNAL_ERROR`; recovery ⇒ decision) |
| Vô nghiệm do cổng là trạng thái có kiểu | breach `ForcedReference`, mã ⊆ {budget, deadline}; khóa pha bị từ chối vì không thể bắn trên tuyến giữ; certificate non-normal + witness `forcedReference` | 5 test Domain (có `Forced_reference_breach_rejects_a_phase_lock_and_a_differing_kept_projection`), test Application, test Runner kiểm witness và breach sau `decisionApplied` |
| Breach là sự kiện, miễn trừ lặp mỗi quyết định | ghi breach khi lời hứa được sửa, khi khách đổi pha trong batch, khi chưa có breach, hoặc khi tập mã đổi; certificate dựng từ miễn trừ | `A_later_decision_repeats_the_exemption_but_records_a_breach_only_on_a_revision`; `An_unchanged_decision_records_the_first_forced_breach_of_a_rider`; `A_phase_change_records_a_new_breach_even_with_the_same_gate_code`; `Forced_reference_recovery_repeats_the_exemption_but_records_a_breach_per_revision`; 3 mutation ⇒ đỏ |
| Không rửa tuyến đã đổi | miễn chỉ khi tuyến ứng viên semantically bằng tuyến reduced | `A_forced_reference_vehicle_whose_route_changed_is_still_rejected`; mutation bỏ điều kiện ⇒ đỏ |
| Không biến lỗi khác thành vi phạm | lỗi ngoài hai cổng (hạn chót, ngân sách) vẫn fail-closed, kể cả khóa pha | `Forced_recovery_does_not_rescue_a_no_op_rejected_outside_the_gates`; Domain từ chối mã lạ |
| Ledger không reset, lời hứa vẫn công bố | revision theo tuyến giữ + budget cộng theo basis | `A_forced_reference_breach_under_the_visible_basis_charges_the_overrun`; `promisePublished` trong test Runner |
| Tránh vi phạm khi còn lựa chọn | level đầu `forced-reference-count` | `The_solver_backed_policy_prefers_a_surviving_insertion_over_the_forced_no_op` (solver liệt kê chính xác); `C2_with_recovery_keeps_the_forced_no_op_as_an_option_and_prefers_the_insertion` |
| Overrun không làm hỏng xếp hạng | utilization forced = 0, không chia giá trị vượt trần; không làm phẳng mức max của đội | `Forced_recovery_keeps_a_budget_rejected_no_op_without_ranking_its_overrun`; `A_forced_vehicle_does_not_flatten_the_fleet_utilization_ranking` (có đối chứng 100%); mutation ⇒ đỏ |
| Thu đủ witness không đổi quyết định forced | `CollectAllCommitmentWitnesses` | `Collecting_every_witness_does_not_change_a_forced_reference_decision` |
| Checkpoint trung thành | kind `forcedReference`, switch vét cạn | `Forced_reference_breach_round_trips_with_its_own_kind_and_is_tamper_checked`; restore trong test Runner |
| Cấu hình sai thì hỏng to | giá trị lạ, chính sách không phải C1/C2 solver-backed; `"fail-closed"` tường minh = mặc định | `Forced_reference_recovery_is_rejected_outside_the_solver_backed_C1_and_C2`; `Forced_recovery_is_rejected_for_a_policy_other_than_C1_or_C2`; `An_explicit_fail_closed_recovery_behaves_like_the_default` |
| Mã lỗi Runner thuộc taxonomy | hai chỗ đổi sang `INTERNAL_ERROR`/`failSession` | mutation Runner nhận envelope `error` thay vì exception; không có test cố định |
| Suite | `dotnet test RideBound.slnx` | 993/993 |
| Giới hạn đã biết | chỉ solver-backed C1/C2; xe "đóng băng" sau khi khách vượt trần/trễ hạn; `BreachCount` luôn 0; portfolio evidence không đánh dấu forced; chưa có golden hash/OR-Tools; chưa chạy FleetPy | ghi ở ADR-075 Consequences |

## 29. ADR-076 traceability — lên xe muộn là sự thật (thăm dò, Proposed)

Nhánh `research/tier3-failure-aware`, commit `69512d3`; không authorize `RB-WP14R-009..012`, WP15 hay H7.

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Mặc định tắt, run cũ không đổi | `lateBoarding` vắng ⇒ `reject`; `latePickups` chỉ ghi khi khác rỗng; adapter giữ nguyên các dict cửa sổ | `A_state_without_late_pickups_writes_no_late_pickup_field`; `An_on_time_boarding_records_nothing_even_when_recording_is_on`; `The_late_boarding_setting_accepts_only_its_two_values`; `test_recording_late_boarding_drops_only_the_latest_arrival_bound` (chế độ mặc định giữ đủ ba dict); suite .NET cũ vẫn xanh |
| Lên xe muộn là sự thật, không nới cửa sổ gốc (T3.4) | `Board(allowLatePickup)`; `EarliestPickup`/`LatestPickup` giữ nguyên | `A_late_boarding_is_accepted_only_when_allowed_and_keeps_the_window`; `A_late_boarding_fails_by_default_and_is_recorded_as_a_fact_when_asked` |
| Vi phạm dịch vụ riêng | `ObservedLatePickup` trong `LatePickups`, tối đa một mỗi khách, giờ thật > giờ muộn nhất | `A_late_pickup_is_after_the_window_and_recorded_once_per_rider`; `Other_ledger_operations_keep_the_recorded_late_pickups` |
| Lên xe sớm vẫn bị từ chối | chỉ nới phía muộn | `A_boarding_before_the_earliest_pickup_is_rejected_even_when_recording` |
| Round-trip qua checkpoint (T3.4) | codec chỉ cho khách có bản ghi rehydrate lên muộn; bản ghi phải khớp khách | `A_late_pickup_round_trips_only_with_its_matching_record`; `Rehydrating_a_late_pickup_requires_the_explicit_allowance`; restore trong `A_late_boarding_is_rejected_by_default_and_recorded_with_record_v1`; mutation ⇒ đỏ |
| Hết `window-wall` khi bật | Runner nhận quan sát | `A_late_boarding_is_rejected_by_default_and_recorded_with_record_v1` (mặc định: đúng thông báo `window-wall`) |
| Bound hiệu lực của `_fleetpy_stop` có bằng chứng (T3.4) | `record-v1`: chỉ bỏ giờ đến muộn nhất neo vào cửa sổ gốc; bound hiệu lực của điểm trả = giờ lên thật + thời gian đi tối đa (luật Runner); giờ đón muộn nhất và thời gian đi tối đa giữ làm kiểm chéo | Python (FleetPy 1.0.2 thật): `test_recording_late_boarding_drops_only_the_latest_arrival_bound`, `test_a_late_planned_pickup_is_still_vetoed_while_recording`, `test_a_late_boarded_drop_gets_the_boarding_plus_maximum_trip_bound` (251 s được, 346 s bị chặn), `test_recording_late_boarding_keeps_the_capacity_check` |
| Cấu hình adapter đọc đúng khóa | `runner_owns_service_bounds` từ file WP4 thật | `test_late_boarding_key_is_read_exactly_as_the_runner_reads_it`; `test_session_settings_carry_the_key_from_the_wp4_file` |
| Suite | `dotnet test RideBound.slnx`; Python baseline command | .NET 1005/1005; Python 437/443, 6 test freeze fail y hệt trên bản sạch `8d1ea4d` |
| Giới hạn đã biết | seal adapter WP9 dịch theo thiết kế; chưa có metric đọc `LatePickups`; chưa chạy FleetPy đầy đủ | ghi ở ADR-076 Consequences |

## 30. Tầng 3 T3.5–T3.7 traceability — thế giới, audit v/e/p, thí điểm (thăm dò)

Nhánh `research/tier3-failure-aware`; công cụ ngoài kho ở `E:\Code\Report_INT3508\ridebound-scratchpad\tang3\`, không đổi
code trong kho. Không authorize `RB-WP14R-009..012`, WP15 hay H7.

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Thế giới v1 neo ở fixture, nhiễu theo cặp OD, hai chiều, có hồi phục (T3.5) | `world/world_v1.py`, `world/preflight_world.py` (per-edge qua `NetworkBasic._set_edge_tt`) | `test_world_v1.py`, `test_preflight_world.py` 0 lỗi; đột biến 6/6 và 7/7 |
| Runner không thấy tương lai | Runner chỉ nhận ảnh chụp điều kiện hiện tại | `run_world_smoke.py`: 1051 frame đầu trùng từng byte, frame khác đầu tiên ở 3.624.394 ms |
| Thế giới giống nhau giữa các nhánh | hệ số giống hệt trên 60/60 log (`pilot/pilot_worlds.py`); thời điểm áp lệch tới 131 s ⇒ `world/preflight_world_v1_1.py` | v1.1: 14 kiểm, đột biến 5/5; smoke đầu-cuối PASS (đổi khung đúng `bin × 900 s`, `C-30`/`V-30` trùng thời điểm, kiểm rò rỉ PASS) |
| Dòng audit có dấu v/e/p (T3.6) | `evidence/audit_vep.py`, dựng lại từ commitment ledger của final checkpoint; đối chiếu delta sổ và hành động đã ACK | 60/60 job; tổng dòng khớp tổng oracle; người kiểm độc lập khớp mọi trường trên smoke và 60 job thí điểm |
| H1: `C` không kích hoạt phục hồi | chỉ số non-normal và breach `forcedReference` | 0 trên 12/12 episode `C-30`, kiểm độc lập |
| Cổng tiếp tục P6 | báo cáo `tang3/T3.7-BAO-CAO-THI-DIEM.md` §4 | audit **đạt**; cơ chế **đạt**; phục hồi **KHÔNG ĐẠT** (`V-30`/`M-30` 5/12, `M⁺-30` 2/12 episode có > 50% quyết định non-normal) |
| Giới hạn đã biết | mỗi ô một episode; xe "đóng băng"; lựa chọn cách chèn sau cổng mỏng (0,7–3,3% theo xe dưới `C-30`); ô Panel A `d20181114-s10-r1` đã bị nhìn trong smoke T3.5/T3.6 | báo cáo §5–§6 |

## 31. ADR-077 traceability — phục hồi "không tệ hơn tham chiếu" (thăm dò, Proposed)

Nhánh `research/tier3-no-worse`; mặc định tắt; không authorize `RB-WP14R-009..012`, WP15 hay H7.

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Mặc định và `forced-reference-v1` không đổi | `IsForcedNoWorse` trả sớm khi cờ tắt; lời gọi no-op dùng `noWorse:false`; thông điệp lỗi giữ nguyên | suite cũ 1005 test vẫn xanh; review độc lập mục 1 |
| Xe bị ép vẫn được chọn một tuyến đã đổi không tệ hơn | `HardVectorCandidateAssessor` kiểm lại phương án bị validator loại, với `ForcedNoWorse` | `No_worse_recovery_also_keeps_the_insertions_that_do_not_delay_the_late_rider`; `The_solver_backed_policy_serves_the_new_rider_under_no_worse_recovery` |
| Chỉ miễn khi tuyến giữ cũng mắc cùng lỗi và phương án không tệ hơn | `NoWorseReference.AllowsLock/AllowsBudget` | `..._rejects_a_changed_route_that_delays_the_late_rider_further`; `..._never_exempts_a_rider_whose_kept_route_meets_the_deadline`; `..._under_the_visible_basis_allows_no_more_than_the_kept_overrun`; `..._under_a_two_sided_deadline_compares_on_the_floor_side` |
| Khóa pha và khóa xe không bao giờ được miễn | `_ => false` trong `AllowsLock` | review độc lập mục 2; chưa có test cố định |
| Vi phạm có kiểu riêng, round-trip checkpoint | `CommitmentBreachKind.ForcedNoWorse`; codec và canonicalizer `forcedNoWorse` | 4 test Domain; `No_worse_breach_round_trips_with_its_own_kind_and_is_tamper_checked` |
| Xe bị ép được xếp hạng mức dùng như xe thường, trừ chiều vượt trần của khách được miễn | `CalculateWorstUtilization(unrankedRiders)` bỏ qua cặp (khách được miễn, chiều có giá trị > trần) | `No_worse_recovery_ranks_a_forced_vehicle_like_any_vehicle_under_a_zero_limit`; `No_worse_recovery_does_not_rank_an_exempted_dimension_over_its_limit`; 3 đột biến bị bắt |
| Cấu hình sai thì hỏng to | chỉ C1/C2 solver-backed; `no-worse` đòi `forced-reference` | `No_worse_recovery_is_rejected_outside_the_solver_backed_C1_and_C2`; `No_worse_recovery_requires_forced_reference_recovery` |
| Suite | `dotnet test RideBound.slnx` | 1025/1025 |
| Đường Runner khi chọn tuyến đổi, đầu-cuối | Runner v2 (`45f8026`, tree `ac251ef3…cff3f`) trên 28 job FleetPy medium A1: 49 quyết định có witness `forcedNoWorse`, 50 breach `ForcedNoWorse` | 28/28 `status: pass`, audit v/e/p đúng; đối chứng 6/6 giống hệt Runner v1 (`tang3/sweep/logs/noworse-analyze-2026-09-26.txt`); ngoài `dotnet test` |
| Đường miễn trừ theo ngân sách (cơ sở nhìn thấy) và cận dưới hai chiều, đầu-cuối | Runner v2 trên 48 job FleetPy medium của Tầng 3 việc A (`V-30`, `M-30` × luật (a)/(b) × 6 thế giới × 2 ô): `V-30(b)` 118 quyết định có witness `forcedNoWorse` mã `COMMITMENT_BUDGET_EXCEEDED`; `M-30(b)` 12 quyết định `forcedNoWorse` ở thế giới tắc giảm dần | 48/48 `status: pass`, audit v/e/p đúng; đối chứng: ở W0, (b) trùng từng quyết định với (a) và với `C-30`; kiểm độc lập 7/7 (`tang3/vm/VM-REPORT.md`, `tang3/vm/independent/RESULT.md`); ngoài `dotnet test`. Mã witness hạn chót không tách cận trên/cận dưới, nên "cận dưới" ở W3 là suy luận |
| Luật (b) cho mọi nhánh dưới tắc đường đo thật tăng nhanh (Tầng 3 việc B) | Runner v2 trên 88 job khung 7:00–9:00, 8 ô phát triển mới, 11 nhánh | 88/88 `status: pass`, audit đúng; `C-x` 0 quyết định non-normal ở 32/32 job; kiểm độc lập ở `tang3/w07/independent/RESULT.md`; ngoài `dotnet test` |
| Giới hạn đã biết | chưa có test tích hợp cố định trong kho cho đường chọn tuyến đổi; thiếu test hai khách trễ, khóa pha, nhiều chiều ngân sách, C2; bản ghi `ForcedNoWorse` không phải một bản cho mỗi quyết định | ADR-077 Consequences |

## 32. Tầng 3 w07x, T3.8/T3.9 và hứa giờ có tính trước tắc đường — traceability (thăm dò, có niêm phong)

Nhánh `research/tier3-no-worse`; không đổi code trong kho (mọi công cụ nằm ngoài kho, ở `E:\Code\Report_INT3508\ridebound-scratchpad`;
các đường dẫn tương đối dưới đây tính từ thư mục đó); không authorize
`RB-WP14R-009..012`, WP15 hay H7; không tuyên bố mới.

| Requirement | Cài đặt/evidence | Gate |
|---|---|---|
| Kế hoạch và dự đoán được niêm phong trước khi chạy, có mốc bên thứ ba | SHA-256 trong `*-PLAN.sha256`; commit/push trước job đầu: w07x `aa98fa5`, T3.8 `323a8d9` (γ `19517b0`), dự báo `33d8071` (phụ lục `df7f893`) | blob trong git trùng mã băm niêm phong |
| T3.8 H3: khoảng dự phòng có độ phủ ≥ 1 − α và hữu ích | conformal số nguyên (`tang3/calib/conformal.py`, 30 test, 11/11 đột biến); γ = 222,259 s trên 15 ô hiệu chỉnh | 2/34 vi phạm ≤ 3/34 và β + γ ≤ 300 s: đạt; khung 7h chỉ 7/9 (`tang3/t38/T38-REPORT.md`) |
| T3.9 H2: cùng số khách, không xử lý kẹt | 170 job kiểm tra, bootstrap cặp theo ô | chênh trung bình và khoảng 95% trong ±0,25 khách, nằm trong [−2; 2]; 0/49 non-normal; kiểm độc lập 13/13 |
| Dùng lại lần chạy cũ khi đổi lớp bọc thế giới | `optimize/compare_equivalence.py` (bỏ `runId`, `publicationId(s)`, `breachId`, `*Hash`; `actions` so như tập), tự kiểm đột biến và đối chứng dương | cổng E1/E2 tương đương hoàn toàn (`docs/benchmarking/tier3-forecast/check-gates-2026-10-06.log` trong kho này) |
| Lớp lời hứa có dự báo giảm trễ so với lời hứa đầu mà không thêm dao động, vượt đệm cùng giá | `optimize/display_layer.py`, `optimize/promise_stretch.py` (23 kiểm, 8/8 đột biến: `optimize/report-extras/test-display-layer-2026-10-06.log`, `mutate-display-layer-2026-10-06.log`); `optimize/forecast_family_a.py` (bootstrap cụm ngày-khung; 7 kiểm tổng hợp: `optimize/report-extras/test-family-a-stats-2026-10-06.log`) | A1, A2 đạt; A3 đạt nhưng mang nhãn "mong manh với suy luận ít cụm"; độ nhạy bỏ 12/11: A1 trượt; kiểm độc lập Họ A 25/26 khớp (`optimize/independent-A/RESULT.md`) |
| Sổ chia trễ ba phần cho từng khách | đẳng thức trễ = điều phối + đường (+ thực thi) kiểm trên 7 lần chạy; kéo dãn theo tắc đường thật khớp trong 2 s cho mọi khách có phần do điều phối bằng 0: 2.305/2.305 trên 34 ô (1.452/1.452 trên 21 ô) | `business-need/contract-split-check-2026-10-06.txt`; `optimize/report-extras/report-extras-2026-10-06.txt` |
| Đưa dự báo vào bộ điều phối (wrapper v1.3) | 32 job sau cổng; `optimize/check_runs_v2.py` kiểm tỉ số theo bảng niêm phong | 3/32 dừng `RBWP7_FLEETPY_PLAN_INFEASIBLE`; B-C1 chỉ đạt trên ô chạy xong; quy tắc triển khai chọn lớp lời hứa (`optimize/test-family-b-2026-10-06-report.txt`); kiểm độc lập khớp (`optimize/independent-B/RESULT.md`) |
| Giới hạn đã biết | thế giới tắc đường là một hệ số chung toàn thành phố (kết quả gần như tất định, là mức trần); ô hiệu chỉnh trùng ngày-khung với ô kiểm tra; 8 ngày-khung; một tuần dữ liệu có ngày nghỉ bù 12/11 làm nguồn; không mô hình hủy chuyến; 2,3% khách có lời hứa đón đầu tiên hiển thị vượt hạn đón | `optimize/FORECAST-REPORT.md` §6 |


Cập nhật thực thi16/10 khoảng14:03+07: full freeze exit0,manifest cedecbb1…42d6f;driver11224/launcher14260 khởi động14:01:08, đang preflight trước2smoke;0/68main. Hourly đã ACTIVE cho đợt mới,one-off PAUSED. Nguồn cost-first đã push dce84ec;hai gói bàn giao chờ push. Xem CURRENT-STATUS.md trên C để tránh driver trùng.


## GitHub đã bàn giao, C60/C120 đang chạy — 2026-10-16T14:09:33.348015+07:00

Đã push research/tier3-cost-first=dce84ec; hai nhánh bàn giao codex đã push
thành công: RideBound d4ee68692573dcf23748d188f515fe1bbedbd089, Report_INT3508 1d529f5f7629b7e13c9bb608737cef5b668cf313.
Đọc START-HERE.md; raw/env/vendor/paper giữ local, nguồn/báo cáo/hash lên GitHub.
Freeze manifest cedecbb1…42d6f đạt; driver11224/launcher14260 từ14:01:08.
Smoke0/2pass,2active/chưa kết thúc;
main0/68pass. Không có lỗi/dừng mới tại checkpoint.
13/13 selftests; .NET1033/1034 với1knownfailure,exit1.
Hourly ACTIVE cho đợt mới;one-off PAUSED. C30/E40GiB/AC/410MiB giữ nguyên.
Checkpoint local chi tiết: cost-cap-sensitivity-v1/CURRENT-STATUS.md vàcheckpoint.json.
Không clone rồi chạy trùng trên máy công ty. Không phân tích partial hoặc đổi
frozen inputs;driver tự đi tiếp sau2smoke. Chưa nhận có kết quả C60/C120.


## Kết quả C60/C120 đã kiểm — 2026-10-16T18:14:17.513937+07:00

C60/C120 COMPLETE: 2/2 smoke, 68/68 main; final verification và analysis exit0 lúc17:24:02+07; 4.355/4.355 đối chiếu số học đạt;0active/0pending/0STOP. C60 so C30: +32phục vụ/+113ghép/+19muộn60/+13trong60; C120 so C60: +34phục vụ/+192ghép/+121muộn60/−87trong60. Thăm dò34ô/nămngày đã thấy, không optimum/satisfaction/profit. PDF77trang giữ nguyên; audit/WP12 còn mở. Hourly PAUSED sau khi báo vàpush kết quả;one-off PAUSED.

| Trần cost-first | Phục vụ | Muộn >60s p0 | Trong60s | Ghép xe | Bị sửa ETA | Sửa lặp | Đón muộn | Muộn >60s D0 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| C30 | 2364 | 72 | 2292 | 432 | 106 | 2 | 44 | 11 |
| C60 | 2396 | 91 | 2305 | 545 | 166 | 3 | 45 | 21 |
| C120 | 2430 | 212 | 2218 | 737 | 284 | 14 | 47 | 142 |
| Không trần | 2502 | 379 | 2123 | 1027 | 434 | 50 | 39 | 318 |

C giới hạn tổng biến động ETA trả do điều phối, không giới hạn thời gian chờ
hoặc trễ thực tế. C60 có số hoàn thành trong60s cao nhất trong các mức đã thử;
đây là mô tả bộ thử, không là trần tối ưu. D0 là phát lại chỉ đọc, chưa tích hợp
vào quyết định. Không có raw decoder độc lập mới;4355check là số học/binding.
H6/E1/H7=7<10 và cây nguồn E giữ nguyên;không Stage2/per-step/rerun.
Toàn bộ JSON/TSV/hash và giải thích ở `C:/RideBoundContinuation/2026-10-16/COST-CAP-RESULTS.md`.
.NET1033/1034 với1known clean-tree failure,exit1;không suite xanh.
PDF77trang giữ hash26c8dea1…1dd69,chưa chèn kết quả cap mới;readiness/WP12 mở.


## Đóng lịch theo dõi — 2026-10-16T18:17:17.444172+07:00

Đã báo kết quả đầy đủ và xác minh push hai nhánh bàn giao. Lịch theo giờ đã
PAUSED bằng automation_update; one-off06:20 giữ PAUSED; không archive chat.
Receipt: C:/RideBoundContinuation/2026-10-16/cost-cap-monitor-closure.json.
Không còn job chờ; không resume/rerun đợt này. Kết quả và raw được giữ nguyên.
Các trạng thái ACTIVE/đang chạy trong mục lịch sử chỉ đúng tại thời điểm cũ.
