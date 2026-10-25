# Trạng thái và decision log

> Tệp sống — cập nhật ở cuối mọi task RideBound
> Tiếp phiên ngoài cây chính:2026-10-16 — ADR079: C60/C120 COMPLETE: 2/2 smoke, 68/68 main; final verification và analysis exit0 lúc17:24:02+07; 4.355/4.355 đối chiếu số học đạt;0active/0pending/0STOP. C60 so C30: +32phục vụ/+113ghép/+19muộn60/+13trong60; C120 so C60: +34phục vụ/+192ghép/+121muộn60/−87trong60. Thăm dò34ô/nămngày đã thấy, không optimum/satisfaction/profit. PDF77trang giữ nguyên; audit/WP12 còn mở. Hourly PAUSED sau khi báo vàpush kết quả;one-off PAUSED.
> Cầu nối bộ mô phỏng công ty (2026-10-23; nhánh `research/via-ref` → `research/via-core` → `research/g2-core`, chưa push): ADR-080 (PS/DL-HZ, bản nháp từ máy công ty) và ADR-081 (A0.1, Elmi, G2). Bộ mô phỏng khớp RideBound thật từng khách trên 11 ô Manhattan: chỉ chèn 55/55 ô-nhánh, Elmi 22/22, G2 33/33 (sau bản sửa adapter đã được chủ duyệt). Đây là độ trung thực cài đặt, không phải lợi ích của luật. Cây chính không đổi.
> Cập nhật gần nhất: 2026-08-28 (cây chính); nhánh thăm dò `research/deadline-gate`: 2026-09-24 (ADR-074 Proposed); nhánh thăm dò `research/tier3-failure-aware`: 2026-09-25 (ADR-075, ADR-076 Proposed; thí điểm T3.7 xong, cổng dừng phục hồi bị kích hoạt); nhánh thăm dò `research/tier3-no-worse`: 2026-09-26 (ADR-077 Proposed; 28 job đầu-cuối A1 pass), 2026-09-29 (Tầng 3 việc A: 48 job `V`/`M` với hai luật phục hồi, thăm dò), 2026-09-30 (Tầng 3 việc B: 88 job khung 7:00–9:00 chỉ tắc đường thật, thăm dò), 2026-10-01 (w07x: mức hứa 90–240 s, 104 job, thăm dò), 2026-10-02 (T3.8/T3.9 trên panel A: 185 job, thăm dò có niêm phong), 2026-10-06 (hứa giờ có tính trước tắc đường: lớp lời hứa phát lại 170 job, 43 job đầu-cuối, thăm dò có niêm phong)

## 1. Trạng thái tổng thể

| Mục | Trạng thái |
|---|---|
| Research direction | `LOCKED_FOR_IMPLEMENTATION_PLANNING` |
| Documentation | `MIGRATED_AND_VERIFIED_V1` |
| Implementation | `WP1_Q1_COMPLETE; WP2_COMPLETE; WP3_COMPLETE_14_OF_14; WP4_COMPLETE_14_OF_14; WP5_COMPLETE_14_OF_14; WP6_COMPLETE_14_OF_14; WP7_COMPLETE_14_OF_14; WP8_COMPLETE_14_OF_14; WP9_COMPLETE_001_TO_009; WP10_COMPLETE_NEGATIVE_CAPABILITY_001_TO_010; WP13_COMPLETE_001_TO_013; WP14_V1_STOPPED_FAIL; WP14R_001_TO_008R_DONE_009_MATRIX_HALTED_40_OF_160_010_SLICE_DONE` |
| Current work package | `ADR-073 freeze v3 AUTHORIZED, receipt SHA 07baeda2…fa9227; RB-WP14R-008 remains CLOSED — FAIL CLOSED under v2; RB-WP14R-008R paired gate running under v3; 009..012/WP15/H7 UNAUTHORIZED; WP14-v1 and H6/E1 frozen; F2-inert prediction registered pre-outcome` |
| Repository | `https://github.com/tsunflowerr/RideBound` |
| Thesis / Stage 1b continuation | ADR-078: COMPLETE exploratory Stage1b;68/68 main and final post-phase/extract/report exit0;83/83 independent extraction-arithmetic checks. I_L+180,I_served+42,I_timely60−138;Stage1b H1–H6 PASS,H7 FAIL(7<10),no Stage2. Results reported; hourly paused again after ADR079 completion/report/push; one-off PAUSED. Resource boundary after20 runs preserved; actual thesis updated 2026-10-16, 77 pages, backed up and verified. [Hồ sơ](benchmarking/thesis-stage1b-continuation-2026-10-15.md) |
| Cost-cap sensitivity / GitHub handoff | ADR079: C60/C120 COMPLETE: 2/2 smoke, 68/68 main; final verification và analysis exit0 lúc17:24:02+07; 4.355/4.355 đối chiếu số học đạt;0active/0pending/0STOP. C60 so C30: +32phục vụ/+113ghép/+19muộn60/+13trong60; C120 so C60: +34phục vụ/+192ghép/+121muộn60/−87trong60. Thăm dò34ô/nămngày đã thấy, không optimum/satisfaction/profit. PDF77trang giữ nguyên; audit/WP12 còn mở. Hourly PAUSED sau khi báo vàpush kết quả;one-off PAUSED. [Hồ sơ](benchmarking/cost-cap-sensitivity-handoff-2026-10-16.md) |
| Cầu nối bộ mô phỏng / kế hoạch công ty 21/10 | ADR-080/081/082, Proposed, thăm dò. Nhánh `research/p1-fix` trên `research/g2-core` `e1c6f2b`, `research/via-core` `1190518`, `research/via-ref` `176f5dd` (SHA sau khi viết lại tác giả ngày 25/10, ADR-082); chưa push. Giai đoạn 1 (25/10): bộ mô phỏng wf15 khớp 117/117 ô-nhánh, 11.521/11.521 khách (EO35 chặt, DLS-120, G2 k = 3, ô 128 yêu cầu); đối thủ chính EOHZ35/SHHZ, luôn báo kèm EO35/SHF. Cầu nối Manhattan 11 ô, so với dự đoán bộ mô phỏng đã khóa sha256 trước: chỉ chèn 55/55 ô-nhánh (4.972/4.972 khách), Elmi RCE35/EOHZ35 22/22 (1.966/1.966), G2 33/33 (2.994/2.994 khách, 142/142 lần dời; lần đầu 30/33, sửa adapter theo quyết định của chủ). Cổng phát lại: A0.1 58/58 job, Runner G2 khi tắt G2 80/80 job. Không chứng minh lợi ích của G2 hay luật C trên Via; Via P512 chờ tệp từ máy công ty. [Tổng hợp](C:/RideBoundContinuation/2026-10-23/TONG-HOP-2026-10-23.md) |
| Main baseline | B1 `rolling-cost` |
| Main treatment | C1 `ridebound-hard-vector` |
| Layer 2 | FleetPy 1.0.2 |
| Layer 3 | RidePy v2.10.1 evaluated; canonical PASS, representative subset FAIL CLOSED; AMoD2 unevaluated alternate |

## 2. Đã hoàn thành

- Kiểm toán kiến trúc BeGo hiện tại.
- Xác nhận `Session` không cho thay pickup sau computation.
- Xác nhận benchmark hiện tại là snapshot, không có promise history.
- Backend 25/25 test pass.
- Frontend 7/7 test pass.
- Đọc/đối chiếu evidence trực tiếp cho RideBound.
- Xác minh official repos/versions:
  - FleetPy 1.0.2 / `053aa9d...`;
  - RidePy v2.10.1 / `bf1863e...`;
  - AMoD2 / `aaa66dd...` tại ngày kiểm tra;
  - AMoDeus;
  - OpenRidepoolSimulator.
- Kiểm source extension points của FleetPy/RidePy/AMoD2.
- Tạo repository Git độc lập `E:\Code\RideBound`.
- Tạo `RideBound.slnx` với 7 source project và 2 test project.
- Khóa dependency Clean Architecture/DDD bằng architecture tests.
- Thêm `global.json`, shared build policy, `.editorconfig`, CI và README.
- Sửa architecture test để đọc `ProjectReference` dùng cả dấu phân cách Windows và
  Linux; bổ sung regression cases cho hai kiểu đường dẫn.
- Mở rộng CI thành các gate format, Release build/test/coverage, dependency audit,
  main runner artifact, Sonar Quality Gate tùy cấu hình và PR-Agent tùy cấu hình.
- Chuyển 23 tài liệu lõi cùng archive evidence liên quan sang `docs/` và tạo root `AGENTS.md`.
- Xác minh 37 tệp Markdown/6.821 dòng: 0 link nội bộ hỏng, 0 code fence lệch,
  0 dấu hiệu mojibake; kèm 3 JSON evidence machine-readable.
- Giữ nguyên cây mã nguồn `E:\Code\BeGo`; không copy `BeGo/src`.
- Xác minh WP0:
  - RideBound restore/build Release: 0 warning, 0 error;
  - RideBound tests hiện tại: 8/8 pass;
  - BeGo backend: 25/25 pass;
  - BeGo frontend: 7/7 pass.
- Hoàn thành `RB-WP1-001`: khóa schema version, unit/range, position union,
  event ordering, envelope/payload/manifest boundary, error taxonomy, canonical
  JSON/hash framing và fixture taxonomy bằng ADR-014.
- Hoàn thành `RB-WP1-002`: thêm Contracts test project, UTF-8 fixture loader và
  smoke fixture dùng chung.
- Hoàn thành `RB-WP1-003`: thêm protocol primitives, typed envelope,
  encode/decode và structural validation có reason code.
- Hoàn thành `RB-WP1-004`: thêm canonical unit conversions, canonical JSON
  byte writer và source-controlled golden byte vector.
- Hoàn thành `RB-WP1-005`: thêm machine-readable JSON Schema v1, schema inventory,
  compatibility matrix và executable version/unknown-field policy.
- Hoàn thành `RB-WP1-006`: thêm hello/helloAck contracts, capability vocabulary
  và deterministic fail-fast/named-downgrade negotiation.
- Hoàn thành `RB-WP1-007`: thêm immutable initialize manifest/initial state
  identity cùng validation chéo envelope/hello/ack không mutate.
- Hoàn thành `RB-WP1-008`: event vocabulary/payload schema và ordering validator
  cho sequence, epoch, simulation time, gap/overlap/overflow.
- Hoàn thành `RB-WP1-009`: decision/error/certificate shell; Q1 luôn ghi rõ
  `notProduced`, không phát action/certificate/solver giả.
- Hoàn thành `RB-WP1-010`: SHA-256 domain separation, tagged length framing,
  manifest/state/decision vectors và chain/tamper/order tests.
- Hoàn thành `RB-WP1-011`: async NDJSON reader/writer có UTF-8/size/EOF/LF/flush
  semantics và diagnostic tách khỏi stdout.
- Hoàn thành `RB-WP1-012`: executable long-lived runner
  `new → negotiated → initialized → awaitingDecisionApplied`, memory pipe và
  child-process integration tests.
- Hoàn thành `RB-WP1-013`: retry nguyên batch trả cached response không advance;
  conflict/overlap/gap/hash/version có đúng disposition; cache giữ một response.
- Hoàn thành `RB-WP1-014`: đúng 10 required golden fixture, full tiny transcript,
  exact expected output/final hash, replay hai lần và tamper proof.
- Hoàn thành `RB-WP1-015`: đóng Q1 bằng ADR-016, traceability/gate evidence và
  tạo ticket refinement duy nhất `RB-WP2-001`.
- Revalidate end-to-end toàn WP1: phát hiện và sửa cache idempotency từng coi
  batch đổi `simTimeMs` là retry hợp lệ; exact retry hiện hash toàn canonical
  eventBatch envelope + payload theo ADR-017.
- Bổ sung regression test cho changed-time duplicate và replay exact transcript
  qua hai runner process sạch; WP1 revalidation inventory đạt 161/161.
- Hoàn thành `RB-WP2-001`: ADR-018 khóa state/reducer/route/reassignment boundary
  và tạo ordered queue `RB-WP2-002..012` trong execution plan WP2.
- Hoàn thành `RB-WP2-002`: typed request/vehicle/position/route/travel contracts,
  strict event payload schema dispatch, thay `fixtureIntent` và thêm
  bootstrap/two-epoch fixtures.
- Hoàn thành `RB-WP2-003`: immutable Domain run/request/vehicle state machine,
  exhaustive lifecycle table và accepted-never-rejected evidence.
- Hoàn thành `RB-WP2-004`: exact frozen prefix, ordered route leg, mutable suffix,
  no-op, planVersion và monotonic reached-stop progress.
- Hoàn thành `RB-WP2-005`: Runner contract mapper, Application internal events,
  manifest-bound travel snapshot, atomic reducer và pending/ack coordinator.
- Hoàn thành `RB-WP2-006`: independent physical validator tự dựng schedule và
  deterministic witness cho capacity/window/max-ride/precedence/connectivity/
  prefix/onboard/accepted/reassignment.
- Hoàn thành `RB-WP2-007`: deterministic pickup/drop insertion generator,
  canonical candidate/stop identity, exact/bounded caps, stable ordering,
  physical prune witness và no-op retention.
- Hoàn thành `RB-WP2-008`: exhaustive B1 fleet selection tối đa accepted count,
  tối thiểu integer route cost, stable tie-break, accept/reject/defer và staged
  apply không reassign incumbent.
- Hoàn thành `RB-WP2-009`: independent exact-small brute-force oracle với 32
  published seeds; generator/selection gap bằng 0 trong bound 2 vehicle/2
  pending request.
- Hoàn thành `RB-WP2-010`: Runner default online produced typed decisions,
  full state/action hash, ACK-only commit và named `--mode conformance` giữ Q1
  transcript oracle.
- Hoàn thành `RB-WP2-011`: source-controlled four-epoch tiny demo chạy hai clean
  self-contained process, byte-exact golden/final hash và tamper proof.
- Hoàn thành `RB-WP2-012`: đóng WP2 physical/B1 bằng ADR-020, đồng bộ gate/
  traceability và tạo đúng một next refinement ticket `RB-WP3-001`.
- Hoàn thành `RB-WP3-001`: đọc lại paper trực tiếp, khóa ADR-021 và tạo ordered
  queue 14 ticket trong `tasks/28-wp3-ledger-certificate-ticket-plan.md`.
- Hoàn thành `RB-WP3-002`: pure Domain promise/version/service-order model, stable
  10-dimension vector và explicit policy zero/unbounded/phase/material/lock types.
- Hoàn thành `RB-WP3-003`: shared `RouteScheduleProjector`, candidate evaluator
  dùng lại projector và `PromiseProjector` cho initial/onboard promise.
- Hoàn thành `RB-WP3-004`: three-way exogenous/decision/visible delta đủ
  ETA/material/vehicle/stop/order/insertion dimension cùng distance port/witness.
- Hoàn thành `RB-WP3-005`: immutable initial/revision ledger, exact version/P1
  conservation/no-refund và ledger nằm trong pending `OnlineState`/ACK transaction.
- Hoàn thành `RB-WP3-006`: hard vector budget evaluator cho đủ 10 dimension,
  exact before/delta/after witness, hard zero, unbounded và monotonic feasible set.
- Hoàn thành `RB-WP3-007`: accepted assignment, onboard, freeze-horizon và final
  confirmation lock evaluator với policy flag/witness rõ.
- Hoàn thành `RB-WP3-008`: typed incident open/resolve, affected-rider derivation,
  immutable breach record với chronology, vehicle và budget relation; không
  reset/refund normal ledger.
- Hoàn thành `RB-WP3-009`: independent combined validator tự dựng lại physical
  plan, state boundary, promise, three-way delta, locks và hard-vector budget;
  candidate filter chỉ là early pruning, Runner revalidate toàn fleet trước publish.
- Hoàn thành `RB-WP3-010`: certificate/action/schema strict cho normal operation
  và witness; input/proposed state hash cùng publication IDs bị cross-check với
  containing decision/actions.
- Hoàn thành `RB-WP3-011`: named commitment policy configuration/content hash,
  Runner `commitment` mode, atomic promise/ledger/certificate/state hash và
  matching ACK/retry semantics.
- Hoàn thành `RB-WP3-012`: canonical full-state checkpoint/restore với content
  hash, manifest/travel/reachable-state/tamper validation và cấm checkpoint khi
  decision còn pending.
- Hoàn thành `RB-WP3-013`: 10-dimension mutation-killing tests, 64×12 generated
  ledger histories, 16-seed independent exact-small P2/P3, two-process replay và
  checkpoint suffix equivalence.
- Hoàn thành `RB-WP3-014`: audit toàn code WP1–WP3, Browser research recheck,
  ADR-022, review giải thích chi tiết và chỉ `RB-WP4-001` refinement READY.
- Hoàn thành `RB-WP5-001`: khóa BeGo/RideBound source provenance, NDJSON Runner
  ownership, append-only schema, idempotency fingerprint, short local transaction,
  outbox, per-run claim/lease, crash recovery qua checkpoint + replay + exact hash,
  bootstrap field provenance, privacy/feature flag và paired B1/C1 Layer-1 protocol
  bằng ADR-025. In-app Browser đọc paper/tài liệu primary; queue `RB-WP5-002..014`
  có đúng một implementation ticket `002 READY`, chưa có WP5 production code.
- Hoàn thành `RB-WP5-002`: BeGo Application có immutable validated contract/port
  cho run/operation/idempotency/Runner/timeline; exhaustive operation/run state
  transition, monotonic revision/time, contiguous epoch/event cursor, strict UTF-8
  hash và actor/resource/payload-bound idempotency. Runner frame phải single-line,
  duplicate-free và embedded `messageType` khớp declaration; decision/certificate/
  outbox hash/order bị guard, T3 contract bắt buộc checkpoint proof sau ACK. Ba
  architecture tests giữ Application khỏi EF/Npgsql/ASP.NET/SignalR/RideBound.
- Hoàn thành `RB-WP5-005`: BeGo Infrastructure có pinned long-lived Runner
  supervisor, một session/process mỗi run và pool bounded. Strict UTF-8 NDJSON,
  line/stderr/time bounds, exact schema/capability/manifest/context binding,
  atomic ACK+checkpoint và process-tree cleanup đều fail closed. Lifecycle audit
  sửa race dispose/start và orphan child. Stub process gate cover adversarial
  framing/lifecycle; published RideBound Runner Release thật hoàn tất online
  bootstrap/decision/ACK/checkpoint cycle.
- Hoàn thành `RB-WP5-006`: mapper chụp immutable BeGo bootstrap source trước
  external I/O, pseudonym HMAC run-local, E7/ms ties-to-even, complete directed
  matrix có node cap, field provenance và exact negotiated manifest/domain hash.
  Old assignment/snapshot chỉ là hashed provenance; generated bootstrap chạy
  xuyên published Runner thật và full BeGo Release pass 98/98 không skip.
- Hoàn thành `RB-WP5-007`: authenticated host/member HTTP service, strict bounded
  DTO, RFC Problem Details, stable idempotent response và explicit write rate
  limit. Sửa request fingerprint để không chứa server-owned sequence, serialize
  create bằng composite advisory lock và pin patched `Microsoft.OpenApi 2.7.5`.
  Full Release PostgreSQL + Runner thật pass 116/116, vulnerability audit sạch.
- Hoàn thành `RB-WP5-008`: T2 ghi exact decision/certificate/projection/timeline/
  outbox atomically; T3 chỉ ghi matching ACK/checkpoint dưới owner+revision+DB-time
  fence. Fresh reconstruction phát lại hello/init/checkpoint/event và yêu cầu exact
  decision bytes/hash; mismatch fail closed `Diverged`. Audit bổ sung semantic
  binding cho promise service order. Tám crash windows chạy với PostgreSQL 17 và
  published Runner thật khớp clean oracle, không duplicate committed effect;
  full BeGo Debug/Release đều 125/125, frontend 7/7, RideBound 557/557.
- Hoàn thành `RB-WP5-009`: outbox claim exact per-run head bằng DB-time lease và
  monotonic attempt fence, commit trước external I/O, mark chỉ sau SignalR send,
  retry bounded và stale completion bị từ chối. Exact user-safe allowlist không
  phát route/node/budget/certificate witness/raw identity. Source audit phát hiện
  late sender có thể tạo stale duplicate sau lease takeover; stable wire
  `aggregateSequence`/message/hash cùng frontend monotonic delivery gate chặn
  callback cũ. Real PostgreSQL cover crash/reclaim/order/cross-run/T2 rollback;
  full BeGo Debug/Release 131/131, frontend 9/9 + production build, RideBound
  557/557 và NuGet/npm vulnerability audits sạch.
- Hoàn thành `RB-WP5-010`: exact `(sequence,id)` audit keyset, server-owned member
  scope, operator-only raw evidence, repeatable-read append-log rebuild/live hash
  và fail-closed pseudonymous export. Source audit phát hiện/sửa cross-member
  request access, JSONB canonical/hash mismatch, prefix cursor plan, partial
  migration downgrade, eager policy dependency và message-controlled exception
  classification. Real PostgreSQL cover concurrent append, drift/mutation,
  authorization, migration up/down/re-up và 12.000-row indexed `EXPLAIN`; full
  BeGo Debug/Release 138/138, frontend 9/9 + production build, RideBound 557/557.
- Hoàn thành `RB-WP5-011`: default Disabled không đăng ký COMMIT hosted worker;
  Shadow chỉ decision, Live mới relay. Exact Runner artifact preflight chặn claim/
  member API khi unhealthy. Durable immutable namespace lọc decision và hard-code
  outbox Live-only, nên shadow không publish sau restart/chuyển mode. PostgreSQL
  kiểm lease reclaim, shadow/live separation, old Session route snapshots và
  guarded rollback; full BeGo Debug/Release 147/147, frontend 9/9, RideBound 557/557.
- Hoàn thành `RB-WP5-012`: source-controlled BeGo-domain-shaped pseudonymous
  fixture bind raw/canonical workload, provenance, common policy và exact B1/C1
  config. Chỉ `policyId` được allowlist; effective config hash bind cả policy catalog
  và arm config. Harness stage exact copies để tránh preflight/use TOCTOU, rồi chạy
  hai clean process mỗi arm bằng cùng Runner DLL/work budgets. Exact materializer
  kiểm decision/certificate, checkpoint validator tính lại state/hash chain;
  normalized inputs giống nhau và repeat input/output/decision/checkpoint hash exact.
  Self-verifying bundle bind mọi file, harness source và executing BeGo assemblies;
  final manifest SHA-256 `b843bd20cbe9bf887d00998d4eaad54258848eb41d87ae49fd18a2142a0cb807`.
  BeGo Debug/Release 152/152, 0 skip; RideBound 557/557.
- Hoàn thành `RB-WP5-013`: independent test-owned transition oracle chạy 256×64
  bước, exact-set claim dưới 2/3/4 PostgreSQL worker, hard process crash tại đủ 8
  decision + 4 outbox durable boundary và fresh-Runner recovery exact. Năm mutant
  correctness bắt buộc đều bị phát hiện; queue 8/32/64 × worker 1/2/4 giữ raw
  warm-up/repetition/machine/row-count evidence. Self-verifying manifest SHA-256
  `e21fb0877fbc6d61bf6f1e24adcda24e09a29fea95a9f44d1b61bf4fc1061ca2`;
  BeGo Debug/Release 153/153, 0 skip; RideBound 557/557. Đây không phải LDFI/Elle/
  QuickCheck execution, mutation percentage, production SLA hoặc effectiveness.
- Hoàn thành `RB-WP5-014`: source-level WP1–WP5 audit phát hiện và sửa ba boundary
  thật: `commit_subject_links` trở thành append-only authorization evidence;
  `commit_outbox.operation_id` bắt buộc, chọn absolute head trước và chỉ claim khi
  exact same-run operation của head đã `Applied` (không skip head chưa T3); outbox
  batch tạo scope/DbContext độc lập theo run để run chậm không
  chặn run khác. Real PostgreSQL regression kiểm migration/immutability/pre-T3 claim,
  coordinated relay regression kiểm cross-run progress. BeGo Debug/Release trên hai
  fresh database + published Runner đạt 154/154, 0 skip; frontend 9/9, lint,
  TypeScript/build; full format và vulnerability audits sạch. Review WP1–WP5 kết
  luận GO chỉ cho refinement WP6, NO-GO cho main experiment/SLA/effectiveness.
- Hoàn thành `RB-WP6-001`: đọc toàn bộ 82 Markdown hiện có và nghiên cứu primary
  sources bằng in-app Browser. ADR-026 khóa FleetPy Manhattan v1/CC BY 4.0,
  canonical scenario/result contracts, addressable HMAC seed hierarchy, exact pinned
  Runner boundary, typed failure/exclusion/denominator rules, independent raw metric
  oracle, strict BagIt-compatible bundle, resource accounting và claim checker.
  Tạo contract decision-complete trong `docs/benchmarking/`, research evidence và
  ordered queue `RB-WP6-002..014`.
- Hoàn thành `RB-WP6-002`: thêm pure benchmark-contract project, 10 strict typed
  codecs/models, semantic validator, 10 Draft 2020-12 document schemas + common
  definitions, positive/negative fixtures và domain-separated identities. Hai clean
  process tái tạo exact six-hash published vector. Targeted 28/28, format sạch và
  required full solution 586/586; chỉ `RB-WP6-003` Ready, chưa download public data.
- Hoàn thành `RB-WP6-003`: registry khóa exact Zenodo FleetPy Manhattan v1 source;
  resumable downloader đã tải/xác minh `408878341` byte với publisher MD5 và local
  SHA-256; safe extractor preflight 335 members/`1022750557` uncompressed byte và
  inventory SHA-256. Rerun thật rehash rồi reuse object/extraction không chạm mạng.
  Targeted 26/26, format sạch và required full solution 612/612.
- Hoàn thành `RB-WP6-004`: ADR-027 sửa impossible scenario/report hash cycle thành
  provenance DAG schema `1.0.1`; deterministic normalizer rehash exact members, strict
  parse, directed SCC/Dijkstra, dense node-pool coverage optimizer + HMAC row rank,
  ties-to-even và typed source/time failures. Hai clean process tái tạo exact tiny
  8/2/16/240 và medium 128/32/96/9120 từ 21400 conserved public rows, kèm CC-BY/
  synthetic-overlay caveat. Targeted 29+32 và full solution 619/619.
- Hoàn thành `RB-WP6-005`: framed HMAC addressable seeds + published cross-process/
  int32 vectors; plan compiler bind caller config/protocol/candidate/validator/solver/
  work/capability/pairing, reject asymmetry/B5 mixing/noncanonical/oversized grid;
  materialize full warm-up/measured run set với collision-free repeat IDs và HMAC arm
  order trước outcome. Targeted 32+37, format sạch và full solution 627/627.
- Hoàn thành `RB-WP6-006`: ADR-028/contract `1.0.2` sửa gap typed failure cho
  cancellation/process-count/stream bytes; external-only supervisor pre/postflight
  exact Runner/runtime/config/source, isolated run root, canonical protocol
  hello/init/event/decision/ACK/checkpoint/shutdown và monotonic sampled process-tree
  limits. 15/15 fake/real supervisor cases pass; actual published WP3 fixture chạy
  qua pinned Runner process, không linked-core fallback. Contract 37/37,
  Benchmarking 52/52, format sạch và full solution 647/647; Windows Application
  Control `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-007`: ADR-029/contract `1.0.3`; immutable per-plan/per-run
  intents, six raw roles, regenerated observation index, exactly-one terminal record,
  shared gapless failure/exclusion hash chain và planned terminal conservation. Seven
  injected crash boundaries, seal/in-flight race, 12-arm concurrency, tamper/path/
  denominator/resource/artifact/manifest/checkpoint mutations và full-grid-only
  authorized rerun đều fail/recover đúng. Artifact inventory được tự rederive và
  pre/postflight bind cùng launch; success transcript bind exact plan scenario/config/
  Runner binary. Benchmarking 77/77, Contracts 38/38, format sạch và required full
  solution 673/673, 0 failed/skipped; `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-008`: canonical registry 36 định nghĩa và production calculator
  sinh đúng 132 integer rows/run với arrival-cohort, decision-time window, explicit
  numerator/denominator/missing/unit và semantic/resource evidence identity. Source
  audit bổ sung state-machine chặn time/epoch regression, terminal outcome conflict,
  defer/completion sai lifecycle và resource-terminal drift trước khi emit metric.
  `RideBound.Wp6MetricOracle` là executable BCL-only không ProjectReference, tự parse/
  canonicalize/hash/reconstruct rồi so toàn bộ row/evidence/metric-set byte-exact.
  McKeeman differential testing được áp dụng bằng request/action/promise/vector/window/
  order/resource/denominator/overflow mutations; Dolan–Moré chỉ khóa run-level paired
  evidence cho WP8/WP9, không tạo profile/effectiveness claim ở WP6. Benchmarking
  86/86, format sạch, Release `-warnaserror` sạch và required full solution 682/682,
  0 failed/skipped; WAC `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-009`: deterministic strict BagIt 1.0 builder tạo exact LF/
  SHA-256 payload/tag manifests, oxum, logical no-self manifest, reviewed verifier
  script, build lock/private staging/atomic publication và không overwrite artifact.
  Exact Git dirty/source inventory, scenario/dataset, runtime/Runner/Contracts/
  harness/oracle/verifier assemblies, machine, registry và run-store grid/denominator
  được cross-bind. Ten-stage verifier dùng lại WP6-007 portable run verifier để kiểm
  raw file/artifact receipts/resources/transcript/ACK/checkpoint, kiểm global failure/
  exclusion sequence, production=oracle và còn tái tính production metric từ raw.
  Fresh-process verifier tự hash binary và chỉ tạo new external sidecar. Deterministic
  all-success + mixed-terminal fixtures và missing/extra/tamper/length/type/traversal/
  case/reparse/script/scenario/provenance/grid/transcript/log/oracle/correlated-metric
  mutations pass/fail đúng stage. Browser đối chiếu lại RFC 8493 + LOC conformance
  suite; ADR-031 khóa boundary. Benchmarking 92/92, format sạch, Release
  `-warnaserror` sạch và required full solution 688/688, 0 failed/skipped; WAC
  `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-010`: ADR-032 khóa machine-readable
  `wp6-mechanical-only-v1` trong source/verifier; builder tự sinh profile và exact
  claim report, bind profile SHA-256 trong `reproducibility.json` `1.0.1`, caller/CLI
  không thể cấp profile hoặc report thay thế. Checker chỉ đọc README, manifest/plan/
  packaging-report labels và selected provenance flags; không đọc transcript, public
  trip/scenario/dataset hoặc source prose. Sáu caveat exact được mask trước scan;
  bounded NFKC/casefold/diacritic, punctuation dual skeleton, common Greek/Cyrillic
  confusable mapping và unsafe Unicode rejection trả typed code/rule/category/path/
  selector/original/normalized witness. Stage 10 tái tính exact report và chặn case,
  punctuation, confusable, default-ignorable, synonym, caveat, report, provenance,
  forged-result và profile-switch mutations đã reseal. ACM/NASEM/Unicode cùng Peng
  2011/Munafò 2017 khóa same-team/minimum-standard/non-confirmatory boundary.
  Benchmarking 95/95, format sạch, Release `-warnaserror` 0 warning/error và required
  full solution 691/691, 0 failed/skipped; WAC `0x800711C7` không tái hiện.
- Hoàn thành `RB-WP6-011`: ADR-033/contract `1.0.4` khóa tiny paired E2E correction.
  Source fixture có 1 xe/3 request/2 complete travel snapshot/6 epoch/16 event; B1/C1
  đều đi qua accept, capacity reject, revision và lifecycle completion. Tight feasible
  request ở epoch 2 buộc chèn một stop trước incumbent pickup nên exact certificate có
  decision-induced `prePickupInsertedStopCount=1`; traffic projection tách riêng
  pickup `+50 ms`, drop `+150 ms`. Plan bind cả WP3 commitment + WP4 arm config, và
  Runner chỉ lấy per-run solver seed qua explicit `manifest-master-seed`. Hai clean
  Release harness process, mỗi process B1/C1 × 3 measured repeat, khớp exact mọi
  semantic identity/per-run hash; resource samples và bundle chứa chúng được phép
  khác. Independent oracle summary được strict verifier kiểm lại theo assembly/raw/
  semantic/resource/row/metric hashes. Timeout/crash/solver unknown/incomplete/
  postflight/input/metric/missing/extra/tamper/selective-rerun matrix đều typed đúng
  stage. Bundle ignored `artifacts/wp6/tiny-paired-20260812-release/` có SHA-256
  `0936f8c26b9edb1086696e5a33a99a3a158459fbc1f31a3f53ce147fb03a1671`,
  fresh Release verifier + claim checker pass. Benchmarking 104/104, Algorithms
  136/136, format/Release sạch, required full solution 705/705; WAC không tái hiện.
- Hoàn thành `RB-WP6-012`: ADR-034/contract `1.0.5` khóa medium public-data gate.
  Verified Zenodo artifact SHA-256 `d9e86f33...599e`, MD5/license/length đạt; hai clean
  root sinh exact 128-request/32-vehicle/96-node/9.120-arc scenario `88a8730a...0e88`
  với 21.400 = 21.400 + 0 conservation. Dedicated synthetic-policy config sửa đúng
  identity mismatch từng gây `FLEET_SELECTION_CONFLICT`; validator/data không bị nới.
  Exact Runner conversation kiểm init/ACK/checkpoint/action/lifecycle. Hai fresh Release
  process đều chạy B1/C1 × 3 = 6/6 success và khớp plan/scenario/source/runtime/grid/
  transcript/decision/semantic metric cùng mọi per-run semantic hash; external verifier
  xác nhận bundles `4f3aa1fd...aa90` và `193c5616...8b44`. Resource/full/bundle hashes
  khác đúng contract. Instant-drain driver là nonphysical mechanics, cấm KPI/effectiveness
  claim. Format pass; required full solution 710/710, WAC không tái hiện.
- Hoàn thành `RB-WP6-013`: ADR-035/contract `1.0.6` làm executable warm-up 1 +
  measured 3 mỗi arm, đưa provenance/policy binding lên early preflight, derive
  conservation từ compiled grid và canonicalize driver failure code trước store.
  Required matrix cover nested canonical permutation/parallel, 21 failure-stage, 8
  exclusion, actual process/resource/store/bundle/metric/claim mutations và source
  nondeterminism audit. Fresh medium D/E đều 8/8 success; 13 semantic field cùng mọi
  per-run semantic hash exact, còn 8/8 full resource rows khác đúng contract. Raw
  negative strata giữ C1 chậm hơn B1 ở cả sáu local measured pair nhưng không tạo
  effectiveness/SLA claim. Release/format/dependency/schema/link gates sạch và exact
  full solution cuối 770/770; một run trước 769/770 do medium CPU control được giữ
  đúng là resource variance, không báo thành WAC.
- Hoàn thành `RB-WP6-014`: đọc lại toàn bộ Markdown và source WP1–WP6, audit cả
  protocol/state/physical/commitment/solver/BeGo durable boundary và WP6
  data→process→store→oracle→bundle logic. Không tìm thấy unresolved correctness/
  contract defect và không thêm heuristic chỉ dựa trên paper. Fresh tiny 8/8,
  medium H/I trên exact source cuối 8/8 mỗi process; 16/16 top-level + 72/72 per-run
  semantic fields exact,
  8/8 full resource rows khác hợp lệ, ba bundle external-verify. Required exact
  `dotnet test RideBound.slnx` pass 770/770; WAC không tái hiện. ADR-036, evidence và
  review `docs/reviews/wp1-wp6-final/` đóng WP6; không effectiveness/SLA claim.

## 3. Chưa làm

- Chưa có bằng chứng RideBound tốt hơn B1. WP9 cho kết quả âm có điều kiện ở 4/8 xe;
  WP10 không rescue và cũng không thiết lập Layer 3 claim.
- Layer 3 còn thiếu simulator có đủ observable mid-edge state. RidePy `nodeOnly`
  fail closed trên một workload concurrent-mid-edge; AMoD2 chưa được thực thi.
- O-001 vẫn khóa cross-vehicle reassignment. B4 chỉ là same-vehicle waiting-
  incumbent repair; không được báo thành reassignment optimizer.
- Incident recovery optimizer chưa có; ledger chỉ ghi breach đúng và không che nó
  bằng certificate normal-operation.
- Chưa có cross-city inference, SLA, satisfaction, fairness hoặc novelty claim.
- Công việc hiện hành theo yêu cầu người dùng: review file-by-file WP1–WP10, đối
  chiếu full PDF, thử optimization exploratory không rescue H6, benchmark và báo cáo
  PDF cuối.
- WP14R đã hoàn tất ledger, supervisor/process tree, hard-crash recovery, mechanics
  resource dimensioning và independent verifier/mutation matrix. Freeze v2 còn chờ
  explicit decision `007`; mọi scientific matrix/frontier `008..012` vẫn chưa làm.

## 4. Baseline verification

### RideBound

```text
.NET SDK 10.0.301
Build Release: 0 warnings, 0 errors
Architecture tests: 7 passed
Domain smoke tests: 1 passed
Date: 2026-07-28
```

### RB-WP1-001 protocol decision ticket

```text
dotnet test RideBound.slnx: passed
Architecture tests: 7 passed
Domain smoke tests: 1 passed
Markdown files checked: 48
Broken internal links: 0
Unbalanced code fences: 0
git diff --check: passed
Date: 2026-07-29
```

### RB-WP1-002–004 contract foundation

```text
.NET SDK 10.0.301
Release build: passed, 0 warnings, 0 errors
Contracts tests: 66 passed, 0 failed
Architecture tests from independent artifacts path: 7 passed, 0 failed
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Full RideBound solution test: 73 passed; Domain smoke 1 blocked/reported failed
by Windows Application Control 0x800711C7, same pre-existing local blocker
Date: 2026-07-29
```

### RB-WP1-005–007 schema, handshake và initialize identity

```text
.NET SDK 10.0.301
Contracts tests: 95 passed, 0 failed
Runner boundary tests: 11 passed, 0 failed
Required dotnet test RideBound.slnx: 114 passed, 0 failed
Release build: passed, 0 warnings, 0 errors
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Release full-suite attempt: 113 passed; Domain smoke 1 blocked/reported failed
by Windows Application Control 0x800711C7, same configuration-specific blocker
Date: 2026-07-29
```

### RB-WP1-008–015 Q1 closure và current revalidation

```text
.NET SDK 10.0.301
Release build: passed, 0 warnings, 0 errors
Release full solution at Q1 closure: 157 passed, 0 failed
  Contracts at closure: 114 passed
  Runner: 35 passed
  Architecture: 7 passed
  Domain: 1 passed
WP1 inventory after schema/vocabulary assertion và ba regression/E2E tests: 161
Current Debug full solution attempt:
  Contracts: 115 passed, 0 failed
  Architecture: 7 passed, 0 failed
  Domain: 1 passed, 0 failed
  Runner: 38 blocked before discovery by Windows Application Control 0x800711C7
WP1 Release full solution revalidation:
  Contracts: 115 passed, 0 failed
  Runner: 38 passed, 0 failed
  Architecture: 7 passed, 0 failed
  Domain: 1 passed, 0 failed
Current published CIBuild runner:
  RideBound.Runner.dll SHA-256:
  f3baa7daaec9b9167b52d9e110ac536a1bceff64e4f87498cc3e9d1be9d0c7c0
  Two clean processes: exit 0, empty stderr, exact expected output, byte-equivalent
Historical final Release full-suite attempt at Q1 closure:
  123 passed before Runner load
  Runner: 35 blocked before assertions by Windows Application Control
  At that closure point Runner source/tests were unchanged from their 35/35 pass
Real child-process NDJSON test at Q1 closure: passed
Current exact transcript replay through two clean child processes: passed
Changed-simTime duplicate corruption regression: passed
Published transcript replay twice/exact output/final hash at Q1 closure: passed
Tampered event changes decision hash at Q1 closure: passed
Required golden fixture inventory: exactly 10
dotnet format --verify-no-changes: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
JSON artifact parse audit: 66 files, 0 invalid
Current Markdown audit: 52 files, 0 broken internal links, 0 unbalanced fences
Default Debug full-suite final attempt: Architecture 7 passed; Domain 1 was
blocked; Runner reported 5 passed and 30 load-policy failures; Contracts reported
15 passed and 85 load-policy failures before completing its full 115-case
inventory. Enterprise Code Integrity policy
0283ac0f-fff1-49ae-ada1-8a933130cad6 blocked fresh DLL loads with 0x800711C7.
The Release rerun likewise blocked fresh Runner.dll. Event IDs 3033/3077 confirm
the signing-level policy; no assertion failure is used as correctness evidence.
Policy blocker reproduced for the fresh Debug Runner.Tests.dll but not for the
then-current WP1 Release suite, which passed 161/161.
Date: 2026-07-29
```

### RB-WP2-002–006 typed state, reducer và physical validator

```text
.NET SDK 10.0.301
Required dotnet test RideBound.slnx (Debug): 278 passed, 0 failed
Release full solution: 278 passed, 0 failed
  Contracts: 127 passed
  Domain: 89 passed
  Application: 13 passed
  Runner: 42 passed
  Architecture: 7 passed
Release build: 0 warnings, 0 errors
Whitespace format verification: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Source JSON parse audit: 85 files, 0 invalid
Markdown audit: 52 files, 95 local links valid, 0 unbalanced fences
Git diff whitespace/error audit: passed
Typed WP2 schema additions: 16
Published WP2 fixture flow: bootstrap map/reduce/ack + epoch two pass
Lifecycle transition matrix: exhaustive pass
Small route precedence permutations: 24/24 match expected feasibility
Physical mutation dimensions: capacity, pickup window, max ride, precedence,
  connectivity, stop location, frozen prefix, plan version,
  onboard/accepted preservation, reassignment
Q1 exact transcript/hash/idempotency regression: pass
Date: 2026-07-29
```

### RB-WP2-007–012 B1, exact-small, online Runner và WP2 closure

```text
.NET SDK 10.0.301
Release build --warnaserror: passed, 0 warnings, 0 errors
dotnet format --verify-no-changes: passed; 0/137 files changed
Logical source-controlled test inventory: 333
  Contracts: 128
  Domain: 89
  Application: 15
  Algorithms: 45
  Runner: 49
  Architecture: 7
Required dotnet test RideBound.slnx (Debug): 333/333 passed
  Contracts: 128/128 passed
  Domain: 89/89 passed
  Application: 15/15 passed
  Algorithms: 45/45 passed
  Runner: 49/49 passed
  Architecture: 7/7 passed
Release full-solution xUnit attempt:
  Contracts: 128/128 passed
  Domain: 89/89 passed
  Architecture: 7/7 passed
  Application/Algorithms/Runner: Windows Application Control blocked fresh
  unsigned Application/Runner DLL loads with 0x800711C7 before assertions
Policy-safe supplemental execution for Release artifacts:
  Application: 15/15 passed
  Algorithms: 45/45 passed
  Runner non-child-process: 46/46 passed
Runner child-process cases verified separately: 3/3
  Q1 conformance two-process exact replay: passed
  stdout/stderr diagnostic isolation: passed
  WP2 online two-process exact replay: passed
NuGet direct/transitive vulnerability audit: no vulnerable packages
Portable Domain/Application forbidden-dependency scan: passed
Source JSON parse audit: 89 files, 0 invalid
Markdown audit: 53 files, 0 broken local links, 0 unbalanced fences
Git diff whitespace/error audit: passed
Algorithms detail:
  Hand-enumerated generator/policy cases: 13
  Independent exact-small published seeds: 32/32
  Generator gap: 0 in published bound
  Selection gap: 0 in published bound
Tiny online demo:
  epochs: 4
  lifecycle: accept -> pickup/board -> drop/alight
  physical rejection: r-2 / CAPACITY
  clean self-contained processes: 2/2 byte-exact
  final decision hash:
  56825f3591fb5d10f4c258d2c05897c016d82cb91c1318ffa23731c920146680
WP2 scope exclusion audit:
  no ledger/certificate produced, no hard budget, no OR-Tools behavior,
  no simulator adapter
Date: 2026-07-30
```

### RB-WP3-001–014 closure: commitment correctness boundary

```text
Logical source-controlled test inventory: 414
  Contracts: 133
  Domain: 134
  Application: 34
  Algorithms: 48
  Runner: 58
  Architecture: 7

Required command revalidation on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 414/414 passed; exit code 0; 0 failed; 0 skipped.
  Contracts: 133/133 passed.
  Domain: 134/134 passed.
  Application: 34/34 passed.
  Algorithms: 48/48 passed.
  Runner: 58/58 passed.
  Architecture: 7/7 passed.
  Windows Application Control 0x800711C7 is no longer a current blocker.

Historical host-policy evidence on 2026-08-02:
  required attempts were blocked while loading fresh unsigned Contracts,
  Application and Runner DLLs. Code Integrity events 3033/3077 identified Smart
  App Control policy {0283ac0f-fff1-49ae-ada1-8a933130cad6}. This remains an
  environment record, not a current test failure.

Supplemental same-tree assertion evidence:
  Contracts Release: 133/133 passed.
  Domain Debug in required attempt: 134/134 passed.
  Application Debug in required attempt: 34/34 passed.
  Algorithms Debug in required attempt: 48/48 passed.
  Architecture Debug in required attempt: 7/7 passed.
  Runner exact xUnit methods through self-contained policy-safe harness:
    54/54 non-child-process cases passed.
  Runner child-process cases: 4/4 behavior passed independently:
    Q1 transcript two byte-exact clean processes;
    Q1 stdout/stderr diagnostic isolation;
    WP2 demo two byte-exact clean processes;
    WP3 commitment demo + checkpoint restore clean processes.

Quality/replay gates:
  Release build --warnaserror: 0 warnings, 0 errors.
  dotnet format --verify-no-changes --no-restore: passed after import-order fix.
  source JSON parse: 104/104 passed.
  Markdown: 58 files, 112 relative links, balanced fences.
  portable dependency scan: passed; NuGet direct/transitive audit: clean.
  git diff --check: passed (only configured LF→CRLF worktree warnings).
  WP2 final decision hash:
    c95c3f7e651a5ff5f366051538ecc53663696baa13fbec967d769af5f3c5d90f
  WP3 final decision hash:
    54ebbbdda6753654aab43d522d9d24bffefe56426275035d685ecc8588371589
  WP3 final state hash:
    d91c91c661dd3a2d2de6d5e214bef2a55a9384d635520ca7d5bdbe9d15694527
  Checkpoint restore suffix: byte-equal to uninterrupted genesis replay.
  WP2/WP3 scripts write explicit UTF-8 without BOM and reject non-empty stderr,
    removing PowerShell 5/7 native-pipeline encoding ambiguity.

WP3 correctness evidence:
  all 10 vector dimensions have exact-boundary and killing mutations;
  hard zero/unbounded/overflow/unknown vocabulary semantics are explicit;
  64 seeds × 12 generated ledger revisions preserve P1/no-refund;
  16 independent exact-small seeds match P2 normal hard-gate behavior;
  16 P3 seeds prove relaxing ETA hard limit 40→160 cannot shrink feasible set;
  physical/state-boundary/lock/budget order is independently recomputed;
  incident breach, certificate publication IDs and checkpoint relations are
    structurally cross-validated rather than trusted from solver output.

Browser research recheck using the in-app Browser:
  Gaul et al. 2021 rolling-horizon MILP;
  Schulz & Pfeiffer 2026 forward slack/precomputation;
  Geržinič et al. 2023 stated-preference survey;
  Tiwari et al. 2024 weighted/Pareto/lexicographic objectives;
  Ackermann & Rieck 2025 multiple-plan dynamic DARP.
  Outcome: no numeric paper default adopted; hard gate stays outside objective;
  schedule strategy, bounded precompute and multiple-plan belong to WP4.

Claim limit:
  414/414 is now a full-solution Debug xUnit pass on this host.
  WP3 still proves mechanical correctness in published small bounds, not scale,
  effectiveness, solver optimality or user satisfaction.
Final recheck date: 2026-08-03
```

### RB-WP4-002 closure: solver-neutral selection boundary

```text
Logical source-controlled test inventory: 435
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 48
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 435/435 passed; exit code 0; 0 failed; 0 skipped.

Production boundary:
  CandidateSelectionProblem canonicalizes vehicles/requests/options while
  retaining declared lexicographic objective order.
  Exactly one no-op is required per vehicle; a validated solution selects
  exactly one option per vehicle and accepts each request at most once.
  Sum/Maximum aggregation fails closed on canonical-integer overflow.
  Deterministic work/time/seed budget is separate from observed wall time.
  Bound direction, exact rational gap, bound order and incumbent/solution match
  are validated before OPTIMAL/FEASIBLE may be reported.
  OPTIMAL, FEASIBLE, INFEASIBLE, UNKNOWN, MODEL_INVALID and SAFE_FALLBACK remain
  distinct; no Google.OrTools or other solver package entered Application.

Adversarial evidence:
  20 new Application test cases include missing/duplicate no-op, unknown entity,
  duplicate request, invalid vector/range, aggregation overflow, lexicographic
  dominance, reversed bound, exceeded deterministic budget, reordered bound and
  false-optimal rejection. Architecture adds the Application port-location gate.
```

### RB-WP4-003 closure: executable scheduling and conservative slack

```text
Logical source-controlled test inventory: 444
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 57
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 444/444 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Mechanism:
  Backward slack combines pickup-arrival deadline, drop-off ride-time deadline,
  waiting absorption and future-stop slack under a fixed projected schedule.
  The value is named CertifiedDelay: delay <= certificate is sufficient for
  time feasibility; delay > certificate is never treated as infeasibility.
  Cache key binds exact immutable run snapshot, full vehicle snapshot and
  position, structural route fingerprint, evaluation time, travel version/hash.
  Cache is bounded and failures are not cached.
  Cache may rank a frontier node but cannot admit it; PhysicalPlanValidator
  always runs before a profile can enter a retained candidate.
  origin-hold-relocated-wait moves only first-pickup waiting already present to
  a current-node waypoint with real service duration; edge progress and
  unexecuted frozen prefix are refused. The transformed route is fully
  revalidated and must preserve original stop service/departure times and cost.

Mutation/equivalence evidence:
  9 new Algorithms tests cover backward arithmetic, every delay through the
  certificate boundary, executable hold equivalence, edge refusal, independent
  run/vehicle/position/route/time/travel invalidation, travel-duration mutation,
  cached/uncached equality, cache-cannot-bypass-validator and repeated-build hits.
```

### RB-WP4-004 closure: bounded best-first generation and loss accounting

```text
Logical source-controlled test inventory: 449
  Contracts: 133
  Domain: 134
  Application: 54
  Algorithms: 62
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 449/449 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Bounded semantics:
  request priority = (latestPickup, arrivalTime, requestId);
  global search priority = potential accepted count, mandatory-service lower
  bound, conservative forward slack, stable digest;
  one deterministic work unit = one best frontier node dequeued;
  unexpanded frontier subtrees are counted combinatorially, with explicit
  canonical saturation instead of overflow or a fabricated exact number;
  cap retains the required safety no-op, then orders feasible candidates by
  accepted count, exact operational cost, slack and candidate ID.

Loss boundary:
  REQUEST_BOUND_OMISSION identifies known omitted requests;
  WORK_BOUND_OMISSION counts raw paths whose feasibility remains unknown;
  CANDIDATE_CAP_OMISSION counts already validated feasible candidates;
  every category has stable digest and count, separate from later solver loss;
  exact mode fails if request/work/candidate omission would occur.

Evidence:
  5 new Algorithms tests cover urgent-request priority, exact work fail-closed,
  exhaustive path conservation, best-first high-acceptance retention, feasible
  cap conservation/digest stability and work-monotonic acceptance.
```

### RB-WP4-005 closure: B2 revision penalty and B3 fixed freeze

```text
Logical source-controlled test inventory: 458
  Contracts: 133
  Domain: 135
  Application: 54
  Algorithms: 70
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 458/458 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

B2 rolling-penalty:
  mechanism provider replaces all ten cumulative hard limits with unbounded and
  removes optional freeze/final-confirmation locks, while preserving material
  rule, budget basis and the global O-001 accepted-assignment lock;
  every raw candidate is assessed through the full validator; assessment does
  not mutate/prune the shared raw pool;
  selector order is accepted count, material ETA revision count, the stable ten
  revision dimensions, canonical operational cost and candidate-ID vector.

B3 fixed-freeze-horizon:
  constructor requires a positive explicit horizon and non-empty valid lock mask;
  all cumulative limits remain unbounded and no numeric default exists;
  freeze activates inclusively at timeToPickup <= horizon and source hard budgets
  cannot accidentally prune outside the configured horizon.

Additional correctness fix:
  CommitmentVector.Add and both exact fleet selectors now fail before canonical
  overflow instead of allowing a non-canonical total or risking runtime overflow.

Evidence:
  8 new Algorithms cases include lexicographic precedence, dimension order,
  explicit configuration, canonical cost, B2 raw-pool preservation over 16
  seeds, and B3 exact horizon boundary; 1 Domain vector-overflow regression.
```

### RB-WP4-006 closure: B4 same-vehicle waiting-incumbent repair

```text
Logical source-controlled test inventory: 465
  Contracts: 133
  Domain: 135
  Application: 54
  Algorithms: 77
  Runner: 58
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 465/465 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

B4 repair boundary:
  disabled by default so the B1 candidate set and choice remain unchanged;
  an enabled positive cap admits only Accepted/WaitingPickup incumbents assigned
  to the same vehicle, not onboard, with one pickup/drop pair wholly inside the
  mutable suffix and no unexecuted frozen request stop;
  a seed removes exactly that pair and enumerates every precedence-preserving
  reinsertion; it never combines two repaired pairs or mutates source routes;
  exact mode fails if the repair-request cap omits an eligible incumbent;
  bounded mode reports repair omission count/digest separately and marks the
  diagnostics incomplete;
  every repaired route is physically revalidated and O-001 still prevents
  cross-vehicle reassignment.

Correctness defect found by adversarial testing:
  the original frontier stable ID reused an order-insensitive omission digest,
  so route permutations could collapse into one search identity;
  search nodes now use an order-sensitive token digest while omission-set
  digests remain canonical and order-insensitive.

Evidence:
  7 new Algorithms tests cover atomic pair reinsertion and input immutability,
  frozen/onboard exclusion, exact cap failure, bounded loss stability, disabled
  B1 equivalence, repaired route diversity and cheaper B4 selection without
  reassignment.
```

### RB-WP4-007 closure: B5 canonical multiple-plan pool

```text
Logical source-controlled test inventory: 477
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 82
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 477/477 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Canonical state/checkpoint boundary:
  pool version zero is the only empty value; every non-empty replacement advances
  the previous version exactly once;
  exact plan SHA-256 binds source epoch, ordered vehicle IDs, route version,
  progress, frozen/mutable order and every executable stop field;
  checkpoint restore recomputes the ID and checks the exact vehicle set,
  request-stop assignment membership, frozen/executed compatibility, physical
  feasibility and distinguished-plan equality with the online run.

B5 selection:
  one shared generated candidate set feeds deterministic fleet enumeration;
  pool size and combination work cap are explicit configuration, with exact
  fail-closed and bounded truncation diagnostics;
  alternatives must preserve the distinguished new-request assignment;
  semantic duplicates are removed, Pareto dominance uses accepted count,
  operational cost and conservative forward slack, then top-K uses greedy
  max-min route distance;
  distinguished control maximizes shared executable-prefix consensus before
  operational/stable tie-breaks;
  only distinguished request actions/routes are applied or exposed.

Executable alternative correction:
  adversarial review found that all candidates originate before publication, so
  a non-distinguished route can have the old or same version after the chosen
  route is applied;
  every different retained alternative is therefore rebuilt at exactly
  distinguished route version + 1 and physically validated against the proposed
  run before it can survive checkpoint restore.

Evidence:
  3 Application identity/version/rehydration cases;
  5 Algorithms dominance, stable diversity/consensus, exact/bounded work,
  assignment compatibility and distinguished-only publication cases;
  4 Runner canonical round-trip, forged-ID, forged-distinguished and actual
  policy-output checkpoint cases.
```

### RB-WP4-008 closure: C1 hard-vector lexicographic policy

```text
Logical source-controlled test inventory: 483
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 88
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 483/483 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

C1 hard boundary:
  the common raw physical pool is generated once;
  each candidate is applied and full WP3 commitment validation both removes
  hard-invalid candidates and returns the authoritative validated ledger in one
  pass; C1 neither adds candidates nor repeats the validator as a separate filter;
  feasibility remains exact per request/dimension/phase and never uses PPM.

Lexicographic ranking:
  maximize accepted requests;
  minimize the worst cumulative BudgetAfter/hard-limit ceiling PPM across active
  riders and applicable finite dimensions;
  minimize the stable ten decision-induced revision dimensions in vocabulary
  order, then canonical operational cost and candidate-ID vector;
  UInt128 multiplication prevents overflow at the canonical integer maximum;
  a feasible zero-limit/zero-usage dimension ranks as 1,000,000 ppm because it
  has no reserve, while non-zero usage remains hard-invalid;
  when no applicable finite hard limit exists, utilization and revision ranking
  are disabled so C1 is semantically identical to B1.

Evidence:
  6 new Algorithms tests cover accepted/utilization/revision/cost dominance,
  exact dimension order, one-pass retained-set equality with the reference hard
  filter, 1/3 and canonical-maximum ceiling arithmetic, zero-limit semantics and
  unbounded no-lock exact-small B1 equivalence.
```

### RB-WP4-009 closure: C2 warning/soft-hard hybrid

```text
Logical source-controlled test inventory: 489
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 94
  Runner: 62
  Architecture: 8

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 489/489 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

C2 configuration and hard boundary:
  every warning profile explicitly defines all ten dimensions once; null means
  disabled and no numeric warning default exists;
  an enabled warning requires a finite hard limit and warning <= hard;
  C2 calls the same one-pass C1 validator/assessor, so warning never admits a
  hard-invalid candidate or creates a candidate absent from the shared raw pool.

Objective:
  maximize accepted count, minimize worst hard PPM, then minimize the ordered
  ten-dimension warning-excess vector, ordered decision-induced revision vector,
  canonical operational cost and candidate-ID vector;
  warning excess is accumulated per scoped vehicle/rider with checked canonical
  arithmetic, preserving ms/count/mm dimensions instead of a weighted scalar;
  if every warning is disabled, C2 delegates directly to the C1 selector and
  produces no synthetic warning objective/output.

Evidence:
  6 new Algorithms tests cover warning-before-revision/cost dominance, explicit
  ten-dimension profile shape, exact C1/C2 retained hard set, non-zero boundary
  excess, warning-above-hard rejection and disabled-warning C1 equivalence.
```

### RB-WP4-010 closure: deterministic OR-Tools adapter

```text
Logical source-controlled test inventory: 495
  Contracts: 133
  Domain: 135
  Application: 57
  Algorithms: 94
  Solvers.OrTools: 5
  Runner: 62
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 495/495 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --verify-no-changes --no-restore: passed.

Adapter boundary and model:
  Google.OrTools 9.15.6755 is pinned only in RideBound.Solvers.OrTools;
  BoolVar selection enforces exactly one option per vehicle and at most one
  assignment per request;
  integer Sum objectives use weighted sums, Maximum objectives use an auxiliary
  integer variable with AddMaxEquality;
  canonical upper-bound arithmetic fails ModelInvalid before native model build
  when an aggregate could exceed Int64.

Lexicographic solve and diagnostics:
  every objective pass rebuilds the model with equality constraints for prior
  objective values proven OPTIMAL; a merely FEASIBLE pass is never fixed as if
  optimal;
  one worker, explicit seed, remaining conflict budget and deterministic-time
  budget make the outcome independent of observed wall time;
  OPTIMAL, FEASIBLE, UNKNOWN, INFEASIBLE and MODEL_INVALID remain distinct;
  selected IDs are revalidated by CandidateSelectionSolution.Create and exact
  bounds are rounded conservatively according to minimization/maximization.

Evidence:
  5 solver tests cover four-pass Sum/Maximum trade-offs plus request uniqueness,
  acceptance-before-cost, eight identical deterministic repetitions, aggregate
  overflow and diagnostic budget/version detail;
  1 architecture test prevents the native package from leaking outside the
  solver adapter project.
```

### RB-WP4-011 closure: deterministic deadline and safe fallback

```text
Logical source-controlled test inventory: 507
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 94
  Solvers.OrTools: 5
  Runner: 62
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 507/507 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --no-restore: passed.

Budget and loss boundary:
  deterministic execution budget has independent generation-work,
  semantic-validation-work and solver conflict/deterministic-time limits;
  observed wall time remains a metric and cannot change replay selection;
  pre-solve accounting preserves omitted candidate count, canonical lowercase
  SHA-256 digest and saturation, separately from primary solver status/loss.

Independent validation and fallback:
  every primary solution, including OPTIMAL or FEASIBLE, must pass the injected
  semantic/full-state validator before it can leave the executor;
  fallback order is canonical no-op, then every one-request insertion sorted by
  the exact objective vector and selected option IDs;
  each attempted solution consumes one validation work unit and records a typed
  rejection witness with path and selected IDs;
  exhaustion or an entirely rejected portfolio returns UNKNOWN with no solution;
  no incident result can be fabricated at this solver-neutral boundary;
  primary bounds stay in audit diagnostics, while a fallback result has no
  mismatched incumbent bounds.

Evidence:
  12 new Application cases cover validated optimal, truthful feasible, all three
  no-solution statuses, rejected incumbent, ordered single-request rescue,
  validation exhaustion, rejected portfolio, separate candidate/solver loss,
  accounting contract and cross-budget misuse.
```

### RB-WP4-012 closure: named policy/solver Runner integration

```text
Logical source-controlled test inventory: 523
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 101
  Solvers.OrTools: 5
  Runner: 71
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 523/523 passed; exit code 0; 0 failed; 0 skipped.
  dotnet format RideBound.slnx --no-restore: passed.

Configuration and identity:
  one canonical registry round-trips the seven published B1–B5/C1/C2 names;
  strict WP4 JSON declares generation cap/work/schedule, solver stage budgets and
  only the mechanism-specific B3 freeze, B4 repair, B5 pool or C2 warning fields;
  C2 has one explicit ten-dimension profile per commitment policy and every
  enabled warning is bounded by a finite hard limit;
  WP4 config hash is domain-bound to the commitment config hash, and initialize
  fails before state creation unless manifest policy ID/version/combined hash
  match the loaded implementation exactly.

Solver and publication path:
  B1–B4/C1/C2 generate one shared physical pool and map their exact hierarchy to
  candidate-selection objectives; vehicle-ordered ID ranks preserve the existing
  deterministic final tie-break without weighted scalarization;
  OR-Tools output and fallback pass full semantic validation with the baseline's
  effective commitment provider; Runner independently validates again;
  B5 keeps deterministic plan-pool enumeration and publishes only distinguished;
  ledger, certificate, plan pool and state hash stay in the pending transaction,
  and only matching ACK commits them; solver completed/safeFallback is part of
  the hashed decision shell, so retry is byte-identical.

Evidence:
  7 Algorithms cases cover registry round-trip, B1 request uniqueness/cost, B2
  material+dimension hierarchy, C1 maximum utilization, C2 warning hierarchy,
  unbounded C1=B1 and semantic-validator fallback;
  9 Runner cases cover strict variant config, warning/hard boundary, binding,
  actual OR-Tools decision, retry/wrong ACK/commit, injected UNKNOWN fallback,
  manifest mismatch, B5 ACK/checkpoint restore and real child-process CLI.
```

### RB-WP4-013 closure: independent evidence

```text
Logical source-controlled test inventory: 557
  Contracts: 133
  Domain: 135
  Application: 69
  Algorithms: 134
  Solvers.OrTools: 6
  Runner: 71
  Architecture: 9

Required command on 2026-08-03:
  dotnet test RideBound.slnx
  Result: 557/557 passed; exit code 0; 0 failed; 0 skipped.

Independent correctness:
  B1 production generator/selector matches an independent exact enumerator over
  64 deterministic fixtures within the published 2-vehicle/2-request bound;
  C1 production objective mapper plus actual OR-Tools matches a separately coded
  enumerator over 64 fixtures, selecting identical candidate IDs with OPTIMAL
  status and exact zero gap on every objective level;
  the hard-gate mutation fixture proves the raw set is strictly larger than the
  hard-feasible set, so deleting the gate is observably killed;
  an actual bounded request omission reaches execution count/digest/saturation
  accounting separately from an injected solver UNKNOWN and validated no-op.

Cross-ticket evidence retained:
  cache on/off and route/travel invalidation equivalence; infinite C1=B1 and
  disabled C2=C1; plan-pool checkpoint/tamper; deterministic deadline and
  fallback; ACK/retry publication gates.

Synthetic performance signal:
  4/16/32/128 Boolean-option models all reached exact OPTIMAL. Observed p50 wall
  times were 2.389/12.160/21.406/91.004 ms on .NET 10.0.9, Windows 10.0.26200,
  X64, 12 processors. This is machine-local candidate-selection evidence only;
  it is not a demand-scale, service-quality or effectiveness claim.
```

### RB-WP4-014 closure: full audit and handoff

```text
Quality gates on 2026-08-03:
  dotnet test RideBound.slnx: 557/557 passed
  Release build --no-restore /warnaserror: 0 warnings, 0 errors
  WP4 microbenchmark Release build: 0 warnings, 0 errors
  dotnet format --verify-no-changes: passed
  NuGet direct/transitive vulnerability audit: no vulnerable packages reported
  JSON/Markdown internal-link/fence/diff/process gates: passed

Logic audit:
  reviewed contract/state/physical/commitment/candidate/policy/solver/Runner paths,
  not only test summaries; no production TODO/placeholder or solver dependency
  leak was found. Candidate loss, solver loss and publication failure stay
  distinct. Every solver/fallback selection is independently validated, Runner
  validates again, and only matching ACK commits route/ledger/pool/hash state.

Artifacts:
  ADR-024; tasks/30 complete; docs/reviews/wp1-wp4-final explains WP1-WP4 flow,
  each important production file, paper-to-code optimization, test evidence,
  synthetic curve and unproven claims. Historical wp1-wp3 review is preserved.

Handoff:
  WP4 is Complete and Q2 mechanical correctness is closed. The only READY ticket
  is refinement-only RB-WP5-001; no BeGo implementation ticket exists yet.
```

### Historical CI hardening checkpoint — 2026-07-28

```text
Release build: passed, 0 warnings, 0 errors
Whitespace format verification: passed
NuGet vulnerability audit: no vulnerable direct/transitive packages
Runner publish smoke: passed
Architecture reference graph with normalized separators: passed
Local xUnit execution at this historical checkpoint: blocked by Windows Application Control (0x800711C7)
Linux CI confirmation: pending
Date: 2026-07-28
```

### BeGo backend

```text
.NET SDK 10.0.301
Passed: 25
Failed: 0
Skipped: 0
Date: 2026-08-05 (WP5 refinement recheck)
```

### BeGo frontend

```text
Passed: 7
Failed: 0
Warning: package type/module performance warning
Date: 2026-08-05 (WP5 refinement recheck)
```

### RB-WP5-001 BeGo integration refinement

```text
RideBound checkout: 44ef6a7cacdc58e7c6c0576430fcd7bb02e76c7a
BeGo checkout: ebe0d34365ec4751bd5c629677733032490a1a0d
dotnet test RideBound.slnx: 557/557 passed
BeGo dotnet test src\OptiGo.slnx --no-restore --verbosity minimal: 25/25 passed
BeGo frontend npm test: 7/7 passed
Browser research: Saltzer/Reed/Clark; Helland; transactional outbox; EF
  transactions/concurrency; PostgreSQL SKIP LOCKED; hosted services; expired
  IETF Idempotency-Key draft (prior art only)
Artifacts: ADR-025, tasks/32, research/wp5-distributed-integration-evidence...
Implementation at this checkpoint: none; only RB-WP5-002 READY
Date: 2026-08-05
```

### RB-WP5-002 Application boundary và durable state invariants

```text
Targeted Debug integration tests: 32/32 passed
Targeted Release build /warnaserror: 0 warnings, 0 errors
Targeted Release integration tests: 32/32 passed
Full BeGo backend: 57/57 passed
Required RideBound solution: 557/557 passed
Targeted dotnet format --verify-no-changes: passed
Logic audit: exhaustive transition pair matrix; terminal/revision/time/sequence/
  canonical-range/default-invalid/UTF-8/frame/payload-conflict/hash/order/checkpoint
  boundaries covered; no TODO/framework/core dependency leak
Full BeGo Release /warnaserror: NOT PASSED — pre-existing transitive
  Microsoft.OpenApi 2.0.0 high-severity advisory through
  Microsoft.AspNetCore.OpenApi 10.0.1. Assemblies compiled, build exit 1 on NU1903.
Date: 2026-08-05
```

### RB-WP5-003 append-only EF/PostgreSQL persistence foundation

```text
Migration: 20260805155554_AddCommitIntegrationPersistence
Schema: 11 commit_* tables; five append-only evidence triggers
Real PostgreSQL: postgres:17-alpine, 1/1 Debug and 38/38 targeted Release passed
Real DB cases: guarded empty up/down/re-up; data-loss rollback refusal; duplicate
  event sequence/decision epoch/idempotency; one active op/run; cross-run FK;
  optimistic revision conflict; Session SET NULL without evidence loss
Full BeGo backend: 62 passed, 1 opt-in PostgreSQL test explicitly skipped
Required RideBound solution: 557/557 passed
Targeted dotnet format --verify-no-changes: passed
Date: 2026-08-05
```

### RB-WP5-004 durable T1 intake/lease store

```text
Store: PostgresCommitIntakeStore implements narrow ICommitIntakeStore
T1: run row lock + exact frame binding + idempotency + event/op/run atomic commit
Claim: ordered FOR UPDATE SKIP LOCKED; DB timestamp lease; transaction committed
  before returning work; expired lease reclaim increments revision/attempt
Backpressure: short transaction advisory lock + bounded pending count
Canonical replay: exact bytea, never jsonb-rendered text
Real PostgreSQL clean-run stress: 5/5 passed
Targeted Release with PostgreSQL: 40/40 passed
Full BeGo backend: 64 passed, 1 opt-in PostgreSQL test explicitly skipped
Required RideBound solution: 557/557 passed
Date: 2026-08-05
```

### RB-WP5-005 pinned long-lived Runner process supervisor

```text
Runtime: one long-lived process/session per run; bounded configurable pool
Pinning: absolute command/artifact path + exact artifact SHA-256 + core commit
Protocol: strict UTF-8 NDJSON input/output bound; bounded stderr drain; exact
  hello schema/capability and initialize manifest/provenance binding
Failure semantics: timeout/cancellation/malformed/context mismatch discards
  session and kills the owned process tree; uncertain process is never reused
Atomic client step: decisionApplied write + checkpoint write/read under one gate
Adversarial process tests: 16/16 non-opt-in passed
Published RideBound Runner Release online cycle: 1/1 passed
Targeted Debug/Release /warnaserror with actual Runner: 17/17 passed
Full BeGo Debug without opt-ins: 80 passed, 2 explicit integration skips
Full BeGo Release with PostgreSQL 17 + published Runner: 82/82, 0 skip
BeGo frontend: 7/7 passed (existing module-type performance warning retained)
Required RideBound solution: 557/557 passed
Targeted dotnet format and git diff --check: passed
Date: 2026-08-05
```

### RB-WP5-006 deterministic BeGo bootstrap mapper

```text
Capture boundary: immutable semantic preparation completes before travel/Runner I/O
Privacy: run-local HMAC-SHA256 pseudonyms; restricted subject links; secret buffers zeroed
Mapping: only necessary venue/eligible-vehicle/active-passenger nodes; no legacy
  assignment, route, ledger, raw name/email/account ID enters Runner protocol
Units: WGS84 -> E7 and seconds -> milliseconds with round-ties-to-even evidence
Travel: exact square directed matrix; finite/range/sentinel/diagonal/off-diagonal
  reachability validation; configurable <=4096 node cap checked before O(n²) I/O
Determinism: ordinal semantic order, contiguous events, canonical JSON/hash stable
Negotiation: manifest binds exact helloAck selection; domain-separated manifest hash
Mapper tests with actual Runner enabled: 16/16 passed
Supervisor + mapper targeted Release: 31/31 passed
Full BeGo Release, fresh PostgreSQL 17 + published Runner: 98/98, 0 skip
Required RideBound solution: 557/557 passed
Date: 2026-08-05
```

### RB-WP5-007 authenticated idempotent HTTP boundary

```text
Authorization: authenticated fallback; host create/finalize; current member read/event
Input: strict unknown-field rejection; <=32 KiB writes; no client sequence/raw frame/
  route/ledger/certificate; member v1 event allowlist is exact timerTick only
Idempotency: HMAC actor scope + resource/scope/key + canonical HTTP semantics;
  server-owned epoch/eventSeq excluded from fingerprint and allocated in T1
Create race: composite advisory transaction lock before lookup/insert; run/event/
  operation/subject-links/provenance commit atomically
HTTP replay: pending returns stable 202 operation; completed returns exact cached bytes;
  changed semantic payload is RFC Problem Details 422
Rate limit: explicit 30 writes/min; action policy precedence exercised through TestServer
Security remediation: Microsoft.OpenApi 2.7.5; direct/transitive vulnerability audit clean
Targeted Application/controller/TestServer/PostgreSQL/Runner: 28/28 + 19/19 passed
Full BeGo Release /warnaserror, fresh PostgreSQL 17 + published Runner: 116/116
BeGo frontend: 7/7 passed; required RideBound: 557/557 passed
Date: 2026-08-05
```

### RB-WP5-008 decision transaction, ACK/checkpoint và crash recovery

```text
Migration: 20260809032051_AddCommitRecoveryFencing
T2: exact decision + certificate + user-safe projection + timeline + outbox atomic
T3: matching decisionApplied + independently validated checkpoint under live fence
Fencing: database UTC + lease owner + revision; stale T2/T3 rejected after takeover
Recovery: fresh pinned Runner + exact hello/init/checkpoint/event reconstruction;
  pending decision must match persisted canonical bytes/hash before ACK
Failure injection: before/after Runner, T2, ACK and T3 all exercised against a
  clean-replay oracle on real PostgreSQL 17 + published RideBound.Runner
Mutation gates: wrong stored decision hash never ACK; replay mismatch Diverged;
  missing/invalid certificate never publishes; nested promise order must preserve
  unique stops, exact request/stop binding and pickup-before-drop semantics
Full BeGo Debug: 125/125 passed, 0 skipped
Full BeGo Release /warnaserror: 125/125 passed, 0 skipped
BeGo frontend: 7/7 passed
Required RideBound command: 557/557 passed, exit code 0
Targeted format verify: passed; vulnerability audit: no vulnerable packages
Published Runner SHA-256:
  EC5F224C058D69F6121E127A39F447F421C36E94094E6106517294CE222AD9BC
Research mapping: RIFL unique request/completion records support durable idempotent
  retry; Gray-Cheriton leases support bounded liveness, while correctness remains
  in revision/owner fencing and exact replay. No exactly-once delivery claim.
Date: 2026-08-09
```

### RB-WP5-009 transactional outbox và SignalR relay

```text
Claim: exact unpublished head per run; DISTINCT ON + ordered FOR UPDATE SKIP LOCKED
Lease/fence: PostgreSQL transaction time + owner + incremented attempt_count
I/O boundary: claim transaction commits before SignalR; mark only after send;
  failed send reschedules exponential bounded backoff without holding row lock
Wire: schema v1, stable messageId/runId/aggregateSequence/payloadHash + exact
  nested canonical user-safe payload; retry attempt/owner never leaks into wire
Privacy/auth: exact event/data allowlist; no route/node/budget/certificate witness/
  raw identity; canonical Session GUID group; authenticated member join required
Failure evidence: crash after send -> same ID/payload/hash; expired claim reclaims;
  stale attempt cannot mark; run B progresses while run A leased; run-local order;
  failed send retry; no-audience row not published; T2 rollback leaves no outbox
Late-side-effect audit: lease cannot stop an already-started send. Stable sequence
  plus frontend per-run monotonic/recent-ID gate discards stale duplicate callback;
  disconnected-client catch-up remains RB-WP5-010, not an exactly-once claim
Full BeGo Debug, fresh PostgreSQL + published Runner: 131/131, 0 skipped
Full BeGo Release /warnaserror, separate fresh DB + Runner: 131/131, 0 skipped
Frontend: 9/9; lint, tsc --noEmit and Next 16.3.0 production build passed
Security: Microsoft.OpenApi 2.7.5 retained; Next 16.3.0, NextAuth beta.32 and
  transitive patches; NuGet and npm audits report 0 vulnerable packages
Required RideBound command: 557/557 passed, exit code 0
Targeted format, TypeScript, Markdown/link/fence/diff gates: passed
Date: 2026-08-09
```

### RB-WP5-010 rebuildable audit timeline, privacy và observability

```text
Query contract: strict canonical (sequence,id), UTF-8/limit bounds, deterministic
  order; production PostgreSQL row-value keyset, not OR/prefix pagination
Authorization: server-owned member scope maps own pickup requests through restricted
  raw-subject links; cross-request denied; operator raw policy default deny
Evidence boundary: member timeline is exact canonical user-safe payload only;
  operator endpoint separately returns exact decision/certificate bytes + hashes
Rebuild: repeatable-read snapshot over append-only decision/certificate/operation;
  contiguous epoch, previous hash, input/output state and materializer/certificate
  bindings checked before canonical rebuilt/live projection+timeline hash comparison
Drift/export: mismatch is explicit and blocks pseudonymous export; recursive guard
  rejects subject/token/coordinates/route/witness/budget/manifest/raw fields
Plan evidence: composite (run,sequence,id) and (run,request,sequence,id) indexes;
  PostgreSQL EXPLAIN on 12,000 representative rows uses index + row tuple condition
Migration evidence: up/down/re-up pass; guarded Down refuses before any drop when
  commit data exists, so failed rollback cannot leave a partial downgrade
Privacy/log evidence: commit exceptions do not log/echo raw details; mutation covers
  secret, subject and coordinate; telemetry carries only stable safe metadata
Full BeGo Debug, fresh PostgreSQL + published Runner: 138/138, 0 skipped
Full BeGo Release /warnaserror, separate fresh DB + Runner: 138/138, 0 skipped
Frontend: 9/9; lint, tsc --noEmit and Next production build passed
Security: NuGet and npm audits report 0 vulnerable packages
Required RideBound command: 557/557 passed, exit code 0; WAC did not recur
Published Runner SHA-256:
  EC5F224C058D69F6121E127A39F447F421C36E94094E6106517294CE222AD9BC
Claim: correctness/rebuildability/privacy only; no exactly-once, production SLA,
  throughput or ridepooling-effectiveness claim
Date: 2026-08-09
```

### RB-WP5-011 default-off rollout, compatibility và rollback

```text
Default: RideBound:Commit:Rollout:Mode = Disabled; no COMMIT hosted service
Activation: Shadow = decision worker only; Live = decision + live-only outbox relay
Preflight: exact pinned Runner artifact SHA-256; no process start; cached bounded
  refresh; unhealthy -> /api/health/commit 503 and no claim/member service resolution
Durable namespace: commit_runs.rollout_namespace IN (Shadow,Live), immutable trigger;
  active uniqueness is (session,policy,namespace); existing rows backfilled Shadow
Claim boundary: decision runner/ACK SQL joins exact namespace; outbox SQL can only
  claim Live rows. Shadow outbox remains unpublished/attempt_count=0 across mode switch
Shutdown/restart: cancellation stops new cycles; inflight lease remains durable and
  same-namespace worker reclaims after DB-time expiry using existing revision fence
Compatibility: old /api/health exact shape and Session latest/final route snapshots
  unchanged; operator audit evidence remains available; no append log is deleted
Migration: up/down/re-up real PostgreSQL; Down guard precedes drop and preserves
  rollout column/index/trigger when data exists
Logic fix: renamed active-run unique constraint is mapped back to typed RunUnavailable
Full BeGo Debug fresh PostgreSQL + published Runner: 147/147, 0 skipped
Full BeGo Release /warnaserror separate fresh DB + Runner: 147/147, 0 skipped
Frontend: 9/9; lint, tsc --noEmit and Next production build passed
Security: NuGet and npm audits report 0 vulnerable packages
Required RideBound command: 557/557 passed; WAC did not recur
Repository format: WP5-targeted clean; three pre-existing non-WP5 whitespace files
  remain recorded for RB-WP5-014, so no false full-format claim
Claim: rollout/recovery/compatibility correctness only; no effectiveness/SLA claim
Date: 2026-08-09
```

### RB-WP5-012 paired B1/C1 Layer-1 replay artifact

```text
Fixture: BeGo-domain-shaped pseudonymous source fixture; explicit provenance says
  no production/account/raw-coordinate data; raw + canonical file hashes pinned
Pairing: B1 rolling-cost vs C1 ridebound-hard-vector; same Runner DLL/core commit,
  workload/seed/graph/travel, candidate caps, OR-Tools adapter and deterministic
  generation/validation/solver work limits; only config /policyId allowlisted
Effective config: domain-separated hash binds common commitment policy + exact arm
  config; initialize may differ only policyId/effective config hash
TOCTOU: exact validated config bytes staged once; hash checked before/after every
  process; Runner independently rejects an inconsistent effective config
Execution: B1 x2 + C1 x2 clean processes; each 2 decisions, 2 produced certificates,
  2 checkpoints, exit 0 and empty stderr
Validation: BeGo exact decision/certificate materializer + independent checkpoint
  content/state/hash validator; normalized protocol input identical across arms
Repeat hashes: B1 output 88ffde16... x2; C1 output 13b32d81... x2
Artifact: every payload file enumerated with byte count/SHA-256; manifest sidecar,
  unmanifested-file rejection and transcript tamper test; exact harness source and
  executing BeGo assembly hashes included
Final bundle: E:\Code\BeGo\artifacts\ridebound\layer1-paired-v1\wp5-012-20260809-final
Artifact manifest SHA-256:
  b843bd20cbe9bf887d00998d4eaad54258848eb41d87ae49fd18a2142a0cb807
Full BeGo Debug fresh PostgreSQL + published Runner: 152/152, 0 skipped
Full BeGo Release /warnaserror separate fresh DB + Runner: 152/152, 0 skipped
Security/quality: targeted changed-code format clean; NuGet vulnerability audit 0;
  git diff --check clean (line-ending notices only)
Required RideBound command: 557/557 passed; WAC did not recur
Claim: Layer-1 mechanical/correctness/reproducibility evidence only; no
  effectiveness, non-inferiority, production SLA or novelty claim
Date: 2026-08-09
```

### RB-WP5-013 independent failure/concurrency/mutation/performance evidence

```text
Method sources: LDFI, Elle, QuickCheck, DeMillo-Lipton-Sayward mutation testing,
  Georges-Buytaert-Eeckhout performance evaluation; mechanisms applied with explicit
  limits, no claim that the external tools/formal analyses themselves were run
Transition oracle: test-owned table, no production transition call; 256 histories x
  64 steps = 16,384; accepted 12,261, rejected 4,123; exact seed/step trace retained
Contention: real PostgreSQL exact expected/observed operation sets; 2/3/4 workers,
  queue depths 24/36/48; every worker claimed a bounded share; lost/duplicate = 0
Decision faults: separate OS process Environment.FailFast at all 8 worker failpoints;
  nonzero exit + exact marker + fresh Runner reconstruction + decision/certificate/
  checkpoint equality + stale T2/T3 fence rejection
Outbox faults: separate OS process Environment.FailFast at all 4 relay failpoints;
  BeforePublish invokes once; AfterPublish/BeforeMark retries the same stable delivery;
  AfterMark remains once; exactly one committed outbox row becomes published
Resource cleanup: no newly orphaned dotnet process; Runner active session count 0;
  PostgreSQL connections return 1 -> 1
Required mutants: 5/5 killed — active-run unique index, ACK/checkpoint gate,
  T2/outbox atomicity, semantic idempotency fingerprint, canonical message hash;
  explicit mutants only, no external mutation score/percentage
Local curves: queue 8/32/64 x workers 1/2/4; deterministic randomized order, one
  warm-up + five measured repetitions, raw intake/claim/drain/ops samples and machine/
  PostgreSQL/append-row provenance retained; no latency threshold assertion
Observed median claim-drain ms (w1/w2/w4): q8 5.553/7.130/7.246;
  q32 8.848/8.822/7.867; q64 10.920/10.957/8.938
Artifact: E:\Code\BeGo\artifacts\ridebound\wp5-independent-v1\wp5-013-20260809-final
Manifest SHA-256:
  e21fb0877fbc6d61bf6f1e24adcda24e09a29fea95a9f44d1b61bf4fc1061ca2
Independent rehash: sidecar exact; 18/18 manifest files present/size/hash exact;
  missing/extra/reparse/tamper rejected
Full BeGo Debug, fresh PostgreSQL + published Runner: 153/153, 0 skipped
Full BeGo Release /p:TreatWarningsAsErrors=true, separate fresh DB: 153/153, 0 skipped
Security/quality: changed-code format clean; all BeGo/RideBound NuGet vulnerability
  audits 0; git diff --check clean apart from pre-existing line-ending notices
Required RideBound command: 557/557 passed; WAC 0x800711C7 did not recur
Claim: independent bounded systems-correctness evidence only; no formal LDFI/Elle,
  exhaustive state space, mutation percentage, end-to-end throughput, SLA,
  ridepooling effectiveness or non-inferiority claim
Date: 2026-08-09
```

### RB-WP5-014 closure/source audit

```text
Source findings fixed:
  1. commit_subject_links UPDATE/DELETE rejected by append-only DB trigger
  2. commit_outbox.operation_id non-null; absolute head chosen before Applied gate
  3. claimed per-run heads publish concurrently in independent DI scopes/DbContexts
Real PostgreSQL targeted migration/publication gate: 1/1 passed, 0 skipped
Targeted rollout/outbox/model gate: 20/20 passed
Full BeGo Debug, fresh PostgreSQL + published Runner: 154/154, 0 skipped
Full BeGo Release /p:TreatWarningsAsErrors=true, separate fresh DB: 154/154, 0 skipped
Frontend: npm test 9/9; ESLint, TypeScript --noEmit and Next production build passed
Quality/security: full dotnet format verify passed; NuGet/npm vulnerability audits 0
Required RideBound command: 557/557 passed; WAC 0x800711C7 did not recur
Verdict: GO for RB-WP6-001 refinement only; NO-GO for main experiment,
  production SLA, effectiveness/non-inferiority or user-satisfaction claims
Date: 2026-08-09
```

### RB-WP6-006 external Runner/resource supervisor closure

```text
Contract correction: ADR-028; umbrella 1.0.2; FailureRecord 1.0.1;
  wp6-failure-v1.0.1; five missing cancellation/process/stream codes typed
Actual Runner gate: published WP3 commitment fixture; hello + initialize + 4 event
  batches + 4 decisions + dynamic ACK + checkpoint + shutdown; exit 0, stderr empty
Pinned pre/postflight: dotnet host, 189 .NET 10.0.9 runtime files, 12 non-PDB Runner
  deployment files, exact policy config and exact scenario source
Independent harness checks: capability selection, manifest hash, decision payload/hash,
  ACK context and checkpoint manifest/epoch/previous-decision chain
Adversarial supervisor cases: 15/15 passed; wall/CPU/memory/process count, stdin/
  stdout/stderr, crash, caller cancel, child tree, mutation, incomplete output,
  existing root and unpinned executable all fail closed with retained evidence
Architecture/source boundary: Benchmarking references only Benchmarking.Contracts and
  Contracts; no Runner/Domain/Application/core result path
Targeted: Benchmarking.Contracts 37/37; Benchmarking 52/52
Quality: dotnet format RideBound.slnx --verify-no-changes --no-restore passed
Required command: dotnet test RideBound.slnx passed 647/647; WAC 0x800711C7 did not recur
Claim: local bounded controls/mechanical repeatability only; no SLA/effectiveness claim
Date: 2026-08-09
```

### RB-WP6-013 adversarial determinism/failure/resource closure

```text
Contract correction: ADR-035; umbrella 1.0.6; no public JSON field/failure code removed
Plan: B1/C1 × (1 isolated warm-up + 3 isolated measured) = 8 terminal runs
Canonical/property gate: 10 document types, nested reversal, 16 parallel decode each;
  plan permutation + 32 parallel compile; both HMAC arm-order strata observed
Taxonomy gate: 21 canonical failure/stage cases, 8 pre-outcome exclusions,
  21 deterministic terminal raw-evidence mappings
Actual failure gate: start/crash/cancel, wall/CPU/memory/process/stdin/stdout/stderr,
  postflight/incomplete/unsupported conversation; partial evidence retained
Source boundary: no mutable RNG/runtime hash; exact staging/provenance allowlist;
  metric call sites remain producer + verifier recomputation only
Fresh medium D/E: 8/8 success each; 13 top-level semantic fields and 8 per-run
  semantic records exact; all 8 sampled-resource metric hashes legitimately differ
Bundle D/E: cb6597d89a844099d5af4849f895d0c5f7af3d7351be5345cfcbf558180324a0 /
  27c7f69e5df77b4f1136c75252e8abb9371231e76e5df55c2a5b42c427d2514e; externally valid
Negative local strata retained: C1 wall/CPU > B1 in 6/6 measured pairs; diagnostic only
Required-mutant result: 100% of declared matrix; not a general mutation score
Quality: Release 0 warning/error; format pass; no vulnerable NuGet package;
  schema 4/4; Markdown 91 files/180 internal links/0 broken/0 unbalanced fences
Required command final: dotnet test RideBound.slnx passed 770/770, 0 fail/skip
Preceding run: 769/770 from medium CPU control; standalone pass then exact rerun pass;
  Contracts/Runner loaded and WAC 0x800711C7 did not recur
Evidence: docs/benchmarking/wp6-013-adversarial-closure-evidence-2026-08-12.md
Date: 2026-08-12
```

### RB-WP6-014 source/claim closure

```text
Source verdict: no unresolved correctness/contract issue; no paper-only heuristic added
Fresh tiny A: 8/8 success; bundle 79cb321a...b04; external verify valid
Fresh medium H/I on final exact source: 8/8 success each;
  16/16 top-level semantic exact; 72/72 per-run semantic exact;
  8/8 full resource rows different as expected
Bundle H/I: 89a43921...d9d8 / a954db62...94e9; both externally valid
Required exact command: dotnet test RideBound.slnx passed 770/770, exit 0
WAC: Contracts/Runner loaded; 0x800711C7 did not recur
Claim: WP6 mechanical correctness/reproducibility only; no effectiveness/SLA
BeGo read-only baseline: backend 149 pass + 5 explicit opt-in skip (154 discovered),
  frontend 9/9; no false 154/154 integration claim for this closure run
Evidence: docs/benchmarking/wp6-014-source-claim-closure-evidence-2026-08-13.md
Review: docs/reviews/wp1-wp6-final/README.md
Date: 2026-08-13
```

### RB-WP10-001..010 RidePy Layer 3 closure

```text
Exact source/environment verifier: PASS; 8/8 tests
RidePy pinned-container unit/integration/analyzer suite: 23/23
Generic RunnerClient targeted regression: 14/14
Canonical actual B1/C1: 5/5 completed each; 5 pickup + 5 drop each
Canonical independent verifier: PASS; 5/5 mutation classes caught
Frozen representative subset: 24 planned arm jobs; 22 PASS; 1 FAIL CLOSED; 1 NOT RUN
Subset independent verifier: PASS over 11 valid pairs + retained failure transcript
WP10 full .NET/FleetPy/format/Release/static gates: PASS in final cross-WP review
Date: 2026-08-23
```

### RB-WP14R-001/002 refinement và immutable attempt ledger

```text
Full-PDF corpus: Kalibera–Jones 12/12 + Mytkowicz et al. 12/12 pages read/rendered
Targeted pinned Python ledger: 13/13 passed; 0 failed; 0 skipped
Full pinned Python/FleetPy: 255/255 passed; 0 failed; 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed; 0 failed; 0 skipped
Benchmarking.Tests: 148/148 in 1 m 55 s; hard CPU ceiling unchanged at 120 s
Format PASS; JSON 1.339/1.339; schemas 83/83; Markdown 280 files/366 links/0 broken
Historical WP14-v1 freeze reverify PASS: exact 160 jobs/46 files/SHA `1ce26ff0…37a55`
Scientific execution: none; WP14-v1 receipt/partial/46 bound files unchanged
Evidence: docs/benchmarking/wp14r-002-immutable-attempt-ledger-2026-08-26.md
Date: 2026-08-26
```

### RB-WP14R-003 supervised incremental process evidence

```text
Targeted supervisor with ResourceWarning treated as error: 15/15 passed
Targeted ledger + supervisor: 28/28 passed
Full pinned Python/FleetPy: 270/270 passed; 0 failed; 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed; 0 failed; 0 skipped
Benchmarking.Tests: 148/148 in 1 m 46 s; hard CPU ceiling unchanged at 120 s
Format PASS; JSON 1.341/1.341; schemas 85/85; Markdown 282 files/369 links/0 broken
Historical WP14-v1 freeze reverify PASS: exact 160 jobs/46 files/SHA `1ce26ff0…37a55`
Scientific execution: none; exit zero remains awaiting independent bundle verification
Evidence: docs/benchmarking/wp14r-003-supervised-process-evidence-2026-08-26.md
Date: 2026-08-26
```

### RB-WP14R-004 hard-crash và stale-open recovery evidence

```text
Targeted ledger + supervisor + fault/recovery: 39/39 passed
Actual Windows hard-kill: 11 supervisor points + receipt-to-terminal recovery point
Full pinned Python/FleetPy: 281/281 passed; 0 failed; 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed; 0 failed; 0 skipped
Benchmarking.Tests: 148/148 in 1 m 59 s; hard CPU ceiling unchanged at 120 s
Format PASS; JSON 1.342/1.342; schemas 86/86; Markdown 283 files/371 links/0 broken
Historical WP14-v1 freeze reverify PASS: exact 160 jobs/46 files/SHA `1ce26ff0…37a55`
Scientific execution: none; uncertain tree exhausts and exit zero still awaits verifier
Evidence: docs/benchmarking/wp14r-004-hard-crash-recovery-evidence-2026-08-26.md
Date: 2026-08-26
```

### RB-WP14R-005 mechanics resource dimension và optimization evidence

```text
Retained v1 pilot: 42 pass + 6 large-journal timeouts; report SHA a2795a84…6817f
Retained v2 before-cache matrix: 48/48 pass; report SHA 96e274a2…c2ce5
Authoritative v3 after-cache matrix: 48/48 pass; report SHA 44dce55e…78bce
Fixed 8 MiB v3 verifier peak: 65,875,968 B / 268,435,456 B envelope — PASS
Observed large median launcher/verifier: 71.30/35.39 s → 4.68/1.96 s
Targeted WP14R ledger/supervisor/fault/recovery/dimension: 50/50 passed
Full pinned Python/FleetPy: 292/292 passed; 0 failed; 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed; 0 failed; 0 skipped
Benchmarking.Tests: 148/148 in 1 m 54 s; hard CPU ceiling unchanged at 120 s
Format PASS; JSON 1.343/1.343; schemas 87/87; Markdown 284 files/373 links/0 broken
Historical freeze PASS exact 160 jobs/46 files/SHA 1ce26ff0…37a55
Scientific execution: none; one Windows host session/matrix, no CI/SLA/population claim
Evidence: docs/benchmarking/wp14r-005-mechanics-resource-dimension-evidence-2026-08-27.md
Date: 2026-08-27
```

### RB-WP14R-006 independent verifier và mutation evidence

```text
Authoritative fixture receipt: SHA 1e4a6450…372104; current fixture/schema/source provenance
Authoritative mutation report: SHA 9d8aacf4…72e1e; 15/15 unique classes caught đúng typed code
Valid fixtures: clean complete-open + partial recovery + explicit legacy đều PASS
Independent verifier: read-only, không import/call writer/supervisor/recovery implementation
Source review: sửa Windows junction blindness và hai TOCTOU inventory windows
Targeted WP14R ledger/supervisor/fault/recovery/dimension/independent: 64/64 passed
Full pinned Python/FleetPy: 306/306 passed; 0 failed; 0 skipped
Required dotnet test RideBound.slnx: 908/908 passed; 0 failed; 0 skipped
Benchmarking.Tests: 148/148 in 1 m 49 s; hard CPU ceiling unchanged at 120 s
Format + historical freeze-v1 exact reverify: PASS
JSON 1.346/1.346; schemas 90/90; Markdown 285 files/380 local links/0 broken
Scientific execution/outcome read: none
Evidence: docs/benchmarking/wp14r-006-independent-verifier-evidence-2026-08-27.md
Date: 2026-08-27
```

## 5. Next action

**Nhánh `research/via-zone-2026-10-26`, cập nhật 2026-10-26:** RideBound thật chạy trên dữ liệu Via ở cỡ Via (gộp theo khu
TLC) và phép hòa vốn (ADR-085). Script, cấu hình, kết quả nhỏ ở `research/continuation/2026-10-26/`; trạng thái job và
kết quả so từng khách ở `kits/via-real-2026-10-26/VIA-LOG.md`. Việc kế tiếp:
1. Đọc kết quả so từng khách của 12 job; nếu khớp, sửa câu "RideBound thật chưa chạy ở cỡ Via" trong khóa luận thành câu
   có giới hạn (điểm gộp theo khu, 2 ô, Shadow vẫn chỉ trong bộ mô phỏng).
2. Đưa phép hòa vốn vào khóa luận như một điều kiện ("luật C đáng dùng khi …"), không như một chiến thắng.
3. Các việc của sinh viên giữ nguyên (P4, P6, N9, họp giáo viên, quyết định đợt 3).

**Nhánh `research/p1-fix` / `research/p1-dwell`, cập nhật 2026-10-25:** giai đoạn 1 xong (ADR-082). Phần dwell nằm ở ADR-083 trên
`research/p1-dwell`. Đã đọc toàn bộ tài
liệu công ty; danh sách việc ở
`C:/RideBoundContinuation/2026-10-24/doc-toan-bo/CON-THIEU-SO-VOI-KE-HOACH-CONG-TY.md`.

P5, P7, P4(a) bổ sung, N1, N2 đã xong ngày 25/10 (ADR-084). Việc kế tiếp:
1. Chủ duyệt các commit trên nhánh khóa luận `thesis/audit-fixes-2026-10-25`.
2. Phần việc của sinh viên: duyệt mã (P4), kho riêng và họp giáo viên (P6), giả thuyết đợt 3 (N9).
3. Nếu làm đợt 3: dựng ô cho ngày mới từ TLC theo quy tắc `rid` đã tìm ra ở P7.

**Nhánh `research/g2-core` (cầu nối bộ mô phỏng công ty), cập nhật 2026-10-23:** bước 1 (ngân sách Elmi) và bước 2 (G2)
đã xong và đã commit cục bộ; xem ADR-081. Việc kế tiếp, theo thứ tự:
1. Lấy tệp từ máy công ty (`C:/RideBoundContinuation/2026-10-22/CAN-LAY-TU-MAY-CONG-TY.md`), chạy lại 55 ngày Via ở chế độ
   adapter, rồi làm phép thử đường biên RC / PS / DL-HZ / Elmi.
2. Báo công ty: ở 3/33 ô-nhánh, nước G2 của bộ mô phỏng chỉ chạy được trên FleetPy nhờ bản sửa adapter `fa676b4`.
3. A0.2–A0.5 và A1, chỉ khi cần chạy ô Via P256/P512 trên RideBound.

Không push, không trộn vào cây chính khi chủ chưa quyết định.

**Nhánh thăm dò `research/tier3-no-worse`, cập nhật 2026-10-06:** Tầng 3 (w07x, T3.8/T3.9, hứa giờ có tính trước tắc
đường) đã chạy xong và kiểm độc lập; xem mục 2026-10-06 ở §9. Việc kế tiếp là viết, không phải chạy thêm: đưa các kết quả này
vào khóa luận khi chủ nghiên cứu đồng ý, với đúng nhãn thăm dò, nhãn "đã xem trước" của Họ A và lưu ý "mức trần" của thế giới
một hệ số. Không mở rộng sang dự báo trong lõi (`ITravelTimeLookup` không có giờ khởi hành) khi chưa có ADR và kế hoạch riêng.
Cây chính không đổi.

WP1–WP10 Complete. WP9 H6 vẫn âm ở cả hai điểm năng lực: service gate FAIL tại 8
xe (`−7.1296 pp`) và 4 xe (`−4.9074 pp`). WP10 canonical pass nhưng representative
subset fail closed bằng `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`; Layer 3
claim chưa được thiết lập. Không được thay margin, pool panel, bỏ failed job hoặc dùng
WP10 để cứu primary.

Final review/optimization goal đã hoàn tất bằng ADR-052. Evidence hiện hành gồm
[WP9 result](benchmarking/wp9-confirmatory-result-2026-08-23.md),
[WP10 negative capability report](benchmarking/wp10-ridepy-layer3-negative-capability-result-2026-08-23.md),
[optimization benchmark](benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md),
[WP1–WP10 review](reviews/wp1-wp10-final/README.md) và
[rendered PDF report](../output/pdf/RideBound-WP1-WP10-final-review-2026-08-23.pdf).

ADR-053 đã mở WP13 exploratory theo yêu cầu tiếp tục tối ưu. `RB-WP13-002 Done`:
immutable H6 inventory/alignment/report đã scan 100/100 bundle và required Debug
856/856 pass. CPU failure 120.062/120.000 ms được giữ trong log; profile dẫn tới exact
span-based marker check, medium public-drain pass 1/1 trong 1 phút 33 giây mà không
đổi ceiling, protocol semantics hoặc H6 receipts. `RB-WP13-003 Done`: strict versioned
record set bind exact 40/40 pair và report `002`. `RB-WP13-004 Done`: comparator quét
đủ 80 primary transcripts/44.156 decisions tới EOF; C1 có immediate accepted count thấp
hơn ở 8/40 pair, bằng ở 32/40 và không cao hơn ở pair nào. Đây là descriptive action
comparison, không phải downstream service effect hay causal mechanism. Queue head tại
mốc đó là `RB-WP14-001` refinement. `005` đã link 41 B1 selected candidates với exact C1
evidence: 33 commitment-pruned, 7 absent/not-recorded, 1 C1-selected; clearance chỉ
xóa recorded witness và giữ feasibility `notEvaluated` do validator fail-fast. `006`
đã cross-tab evidence với immediate relation: 7 budget + 1 lock occurrences ở tám
C1-lower pair, nhưng 21 budget + 4 lock occurrences cũng ở equal pair. Đây chỉ là
first-divergence co-occurrence, không causal decomposition. `007` đã verify 80 raw
target decisions: generation evidence exact-equal 40/40 pair, complete 80/80 arm-
epoch, zero cap/work/generation/selection omission. Tuy nhiên full retained IDs/routes/
objectives vẫn `notRecorded`, nên evidence vNext là cần cho candidate-level questions.
`008` đã đóng opt-in v1.2 retained-portfolio instrumentation, strict decoder/schema và
capture-on/off differential. `009` sau đó khóa receipt trước execution và chạy đủ
40 pair/80 arm E1: 8.640 requests, 44.156/44.156 solver decisions có v1.2 portfolio,
zero failure, cùng source inventory; 5.516.098.710 raw bytes nằm ngoài H6. Inventory
chỉ xác nhận execution/coverage, chưa có mechanism conclusion. `010` đã dựng lại
byte-exact 80 bundle/44.156 portfolio, reject đúng typed code cho 31/31 in-memory
mutant và xác nhận E1↔H6 80/80 same-arm behavioral projection equal, zero mismatch.
`011` đã verify lại đủ raw E1 rồi khóa 40/40 generated signature sets equal,
390 signature/arm, zero collision/ID drift/absent; 41 links gồm 33 pruned, 7 eligible-
not-selected và 1 selected. Association rows overlap/non-additive và noncausal;
objective profiles không so số trực tiếp. Required .NET 860/860, pinned Python
191/191. `012` đã audit 80 file source/schema/test/report, deep-verify 100 H6 bundle và
80 E1 arm, sửa một P2 verifier-composition gap bằng supplemental guard/regression và
đóng với zero unresolved P0–P2. `013` đã đóng WP13: bảy exit gate pass, ba P3
limitation có resolution trong
[retention/successor policy](benchmarking/wp13-evidence-retention-and-successor-policy-v1.md),
và verdict `openExploratoryAblationOnly` trong
[closure decision](benchmarking/wp13-013-closure-decision-2026-08-24.md).
WP14 chỉ được mở ở mức refinement trên development namespace/cells mới; WP15–WP20 vẫn
roadmap-level; không đổi margin/panel/failed-job treatment hoặc rescue confirmatory
result hậu outcome.

`RB-WP14-001..005/008` Done; `006/007` Deferred; `009` đã đóng **FAIL CLOSED**.
ADR-069/receipt SHA `1ce26ff0…37a55` pre-outcome freeze exact 16×10 = 160 jobs,
1.728 arrivals/arm, source/runtime/tree seals, analyzer hai trục và resource
envelope. Paired dry-run chỉ có B1 valid; C1 partial thiếu manifest được giữ nguyên,
không retry/replace. Vì vậy `010..014` không được authorize dưới freeze v1. H6/E1
raw roots vẫn read-only và policy v2/H7/WP15 chưa được authorize.

ADR-071 đã mở successor mechanics-only `WP14R`, không mở lại WP14-v1. In-app Browser
và full-PDF QA đọc 24/24 trang Kalibera–Jones/Mytkowicz, từ đó tách host/process/job/
verifier variation, setup bias và fault intervention khỏi scientific outcome. `001`
refinement và `002` immutable attempt ledger đã Done: tối đa hai attempt/job, attempt
không phải unit, recovery chỉ theo mechanical validity, canonical/exclusive receipts,
stable log/output inventory và tamper/state-machine gates. Full pinned Python 255/255,
required .NET 908/908. `003` sau đó thêm exact command/privacy binding, bounded
incremental stdout/stderr/heartbeat journal, Windows Job Object/POSIX process group và
strict partial/terminal verifier; exit zero chỉ `awaitingBundleVerification`. `004` nay
đã hard-kill outer supervisor ở durable boundaries, thêm `launchIntent`, immutable
recovery receipt và classification-aware disposition: incomplete/ambiguous/uncertain
đều exhaust, proven-safe tree mới mở attempt 2. Actual Windows Job Object giết cả child/
grandchild; POSIX exact-group path có contract test. `005` đã giữ ba mechanics matrix:
v1 pilot retain sáu large timeout, v2 before-cache 48/48 pass và v3 after-cache 48/48
pass. Fixed 8 MiB verifier peak `65,875,968` B dưới envelope 256 MiB; không ngoại suy
two-stream hard cap. Source review cache compiled schema validator và bind log/report/
schema-tree provenance; observed large launcher/verifier median giảm `71.30/35.39 s`
xuống `4.68/1.96 s`, chỉ descriptive within-host. `006` thêm verifier read-only
độc lập với writer/supervisor/recovery implementation, ba valid fixture và 15/15
retained mutation class. Review sửa Python 3.10 Windows-junction blindness và hai
TOCTOU inventory windows; current/legacy provenance đều explicit. Full pinned Python
nay 306/306, required .NET 908/908. ADR-072 sau đó đã khóa exact freeze v2;
`RB-WP14R-008 Ready` nhưng AC host preflight đang chặn launch, `009..012` unauthorized.

## 6. Open decisions

| ID | Câu hỏi | Khi nào khóa |
|---|---|---|
| O-012 | Frontier F1/F2 + budget sweep có đủ để mở F3–F6 hoặc WP15 không | `RB-WP14-014`, sau independent verifier/audit `011..013` |

O-013 được khóa bởi ADR-072: mechanics `002..006` đủ để authorize exact protocol
freeze v2, nhưng scientific launch vẫn fail closed cho tới khi frozen host preflight
pass; preflight hiện tại fail `POWER_SOURCE_NOT_AC` và không tiêu thụ attempt.

O-001 đã được khóa bởi ADR-018: B1 WP2 không cho incumbent accepted request đổi
vehicle; WP4 chỉ mở lại bằng ADR superseding và atomic multi-vehicle evidence.
O-007 được khóa bởi ADR-025: WP5 dùng versioned long-lived NDJSON child process;
HTTP/gRPC chỉ mở lại khi có cross-host operational requirement và ADR mới.
O-006 được khóa bởi ADR-037 và executable probe trên exact FleetPy 1.0.2: position
`(start,end,relative)` có direction/range ổn định và `SimulationVehicle._move` cập nhật
`veh_obj.pos`; drift phải fail closed, không suy diễn fraction từ clock.
O-002/O-003/O-004/O-008 được khóa trong WP8/ADR-040..044. O-005 được khóa bởi
ADR-050/051: RidePy là framework đã đánh giá cho WP10 và cho kết quả năng lực âm;
AMoD2 chỉ là hướng tương lai riêng.
O-009 được khóa bởi ADR-059: H6 đủ cho count-only covariates nhưng không ghi full
retained candidate identities/routes/objectives hoặc post-clearance state; evidence
vNext cần cho candidate-level portfolio/replay questions và không backfill H6.
O-010 được khóa bởi ADR-069: active matrix có 10 arm, 16 cell, 108 arrivals/job và
1.728 arrivals/arm; F3–F6 vẫn Deferred chứ không có mức hậu outcome. O-011 được
khóa bởi ADR-068: development panel 2018-11-12/13 rời H6 trên cả bảy trục.
O-012 không thể được trả lời dưới freeze v1 vì matrix/frontier không tồn tại; WP14R
không tự chuyển quyền đó, và chỉ một verified successor frontier mới có thể đưa câu
hỏi trở lại closure decision.

## 7. Decision log

### ADR-001 — 2026-07-27 — Accepted

**Context:** BeGo planner hiện phụ thuộc domain/session và là snapshot.

**Decision:** Xây RideBound thành các project độc lập với portable core; BeGo dùng adapter.

**Consequence:** Tốn contract/mapping ban đầu nhưng tránh khóa core vào product.

### ADR-002 — 2026-07-27 — Accepted

**Decision:** Novelty chỉ đặt ở per-rider, multi-dimensional, cumulative/switch budget qua nhiều epoch kèm certificate.

**Rejected:** claim dynamic insertion/ETA threshold/least-commitment nói chung.

### ADR-003 — 2026-07-27 — Accepted

**Decision:** Layer 1 BeGo và Layer 2 FleetPy là bằng chứng chính; Layer 3 cross-system là bổ sung.

### ADR-004 — 2026-07-27 — Accepted

**Decision:** FleetPy pin `1.0.2` commit `053aa9d4fcfde91c5d303435d5748f9206c071b0`.

### ADR-005 — 2026-07-27 — Accepted

**Decision:** RidePy v2.10.1 là Layer 3 mặc định; AMoD2 là alternate. OpenRidepoolSimulator không nằm critical path.

### ADR-006 — 2026-07-27 — Accepted

**Decision:** B0 BeGo hiện tại chỉ là context; B1 rolling online cùng core là baseline chính.

### ADR-007 — 2026-07-27 — Accepted

**Decision:** Budget synthetic là service-policy stress test, không là user preference truth.

### ADR-008 — 2026-07-27 — Accepted

**Decision:** NDJSON long-lived runner là canonical cross-language interface; in-process BeGo phải pass cùng contract.

### ADR-009 — 2026-07-27 — Accepted

**Decision:** Primary algorithm metric dự kiến tách decision-induced revision khỏi traffic-induced revision.

**Note:** exact primary endpoint chưa preregistered.

### ADR-010 — 2026-07-28 — Accepted

**Context:** Người dùng yêu cầu BeGo và hệ thống nghiên cứu có hai GitHub
repository, lịch sử và vòng đời phát hành riêng.

**Decision:** Đổi tên dự án thành RideBound và đặt tại repository độc lập
`https://github.com/tsunflowerr/RideBound`. Không copy `BeGo/src`. BeGo B0 nằm
ở repository cũ; B1 và C1 cùng nằm trong RideBound để so sánh công bằng.

**Consequence:** Tích hợp BeGo phải qua protocol/scenario/adapter rõ ràng.
Regression BeGo được chạy từ repository ngoài khi task chạm integration.

### ADR-011 — 2026-07-28 — Accepted

**Context:** Cấu trúc `Core` cũ chưa biểu diễn rõ Clean Architecture/DDD.

**Decision:** Dùng các layer `Domain`, `Application`, `Contracts`,
`Algorithms`, `Solvers.OrTools`, `Infrastructure` và `Runner`. Domain không
tham chiếu project/package; Application chỉ tham chiếu Domain. Architecture
tests kiểm project graph và từ khóa framework bị cấm.

**Consequence:** Chỉ thêm adapter/persistence project khi có behavior thật;
không scaffold hàng loạt assembly rỗng.

### ADR-012 — 2026-07-28 — Accepted

**Context:** Architecture test đọc dấu `\` trong `ProjectReference` bằng API path
phụ thuộc hệ điều hành, tạo false positive trên Linux CI. CI WP0 cũng mới chỉ có
restore/build/test nên chưa hiện thực đầy đủ PR fast gates trong tài liệu `15`.

**Decision:** Chuẩn hóa separator trước khi lấy project name và khóa lỗi bằng hai
regression cases Windows/Linux. Tách CI thành format, Release build/test/coverage,
NuGet/dependency review và package runner sau main. Sonar và PR-Agent được khai báo
nhưng chỉ chạy khi repository secrets/variables tương ứng đã tồn tại.

**Consequence:** Architecture rule giữ nguyên; chỉ cách đọc `.csproj` trở nên
cross-platform. AI review không phải required correctness gate. Sonar chỉ được đặt
required sau khi cấu hình và bootstrap scan thành công.

### ADR-013 — 2026-07-28 — Accepted

**Context:** Roadmap đã có 12 work package nhưng chưa có đơn vị delivery đủ nhỏ.
Ticket hóa chi tiết tất cả package ngay khi WP1 chưa khóa contract sẽ tạo dependency
và acceptance criteria dựa trên giả định chưa được kiểm chứng.

**Decision:** Dùng progressive elaboration: giữ WP1–WP12 ở mức topic/outcome/gate,
chỉ chia topic hiện hành thành ticket tuần tự. WP1 có 15 ticket trong `24`; mặc
định WIP là một implementation ticket. Topic kế tiếp chỉ được refinement khi topic
trước đạt exit gate.

**Alternatives considered:** Ticket hóa toàn bộ WP1–WP12 ngay; triển khai trực tiếp
từ deliverable cấp WP.

**Consequence:** Bước tiếp theo luôn nhỏ và kiểm tra được, nhưng backlog xa phải
được refinement lại bằng evidence thực trước khi triển khai.

**Evidence:** `23-delivery-backlog-and-ticket-policy.md`,
`24-wp1-contracts-ticket-plan.md`.

### ADR-014 — 2026-07-29 — Accepted

**Context:** Protocol draft còn cho phép nhiều cách hiểu về `schemaVersion`,
distance unit, node/edge position, batch sequence, field ownership, error
severity và hash concatenation. FleetPy có network position/plan locks trong khi
RidePy có thể chỉ cung cấp node-level state; một contract ngầm chọn một bên sẽ
làm cross-system replay không còn so sánh được.

**Decision:** Protocol v1 bắt đầu ở exact version `1.0.0`; dùng JSON integer-only
trong common safe range, millisecond, millimeter, WGS84 E7 và micro-cost có
`costUnitId`. Position là tagged union `node`/`edgeProgress` với capability
`nodeOnly`/`directedEdgeProgress`. Event sequence liên tiếp trên toàn run, epoch
chỉ tiến sau `decisionApplied`; gap/overlap làm session failed. Envelope chỉ giữ
message routing/identity, payload giữ nội dung message và initialize manifest giữ
config bất biến. Error dùng stable code cùng disposition
`rejectMessage`/`failSession`/`terminateProcess`. Canonical JSON là RFC 8785
subset integer-only; SHA-256 dùng domain prefix và tagged length frames. Fixture
phân loại `schema-only`, `runner-executable` hoặc `future-behavior`.

Chi tiết normative, exact range, field table, framing bytes và checklist nằm
trong `06-event-contract-and-determinism.md`, mục 2–14.

**Alternatives considered:** giữ distance là “mm hoặc meter”; chỉ hỗ trợ node;
ép mọi adapter phát edge progress; nối raw JSON/string khi hash; coi mọi fixture
là executable.

**Consequences:** Contract tests có thể viết exact bytes/error/order mà không tự
chọn semantics. Adapter thiếu edge progress phải công bố `nodeOnly` và
fail/downgrade có tên; không âm thầm bịa position. Mọi thay đổi unit, position
meaning, ordering hoặc hash input sau ADR này là breaking change, cần schema
major, ADR superseding và fixture migration.

**Evidence:** `05-portable-core-architecture.md`,
`06-event-contract-and-determinism.md`, `12-fleetpy-adapter.md`,
`13-cross-system-adapters.md`, `tasks/24-wp1-contracts-ticket-plan.md`.

**Supersedes / superseded by:** Không supersede ADR trước. Đóng phần contract của
O-006; chỉ giữ executable FleetPy capability preflight cho WP7.

### ADR-015 — 2026-07-29 — Accepted

**Context:** ADR-014 khóa semantics chung nhưng RB-WP1-005–007 vẫn cần exact
receiver behavior cho patch/minor/major, capability wire vocabulary và field
identity bất biến của initialize manifest. Nếu receiver tự bỏ field minor,
capability tự mặc định hoặc manifest lặp identity không nhất quán, cross-system
replay có thể dùng input khác nhau mà vẫn tưởng là cùng run.

**Decision:** Version phát hành hiện tại vẫn là exact `1.0.0`; receiver v1 nhận
cùng patch line `1.0.x`, từ chối higher minor nếu chưa có explicit safe-forward
profile và fail session với major khác trước unknown-field check. Safe-forward
profile phải machine-readable và current list rỗng. Capability v1 dùng
single-valued `positionModel`, semantic set có vocabulary cố định và explicit
fleet/request scale. Required capability thiếu/không biết phải fail; downgrade
chỉ hợp lệ khi có `downgradePolicyId`. `initializeRun` giữ run/scenario ID ở
envelope, manifest chỉ giữ content/config hashes, seed, policy, unit conversion,
exact negotiated selection, adapter/simulator, core commit và binary identity.
Pure validation không đọc Git/environment, không mutate active identity và cấm
re-initialize.

**Consequences:** Adapter có machine-readable schema/inventory để kiểm trước khi
gọi runner; patch bug fix không buộc migration semantics. Không có future minor
nào được nhận ngầm. Manifest không lặp ID nên mismatch scenario được kiểm với
envelope/session context, còn nội dung scenario được kiểm bằng hash. Hash values
trong WP1-007 là contract fields do caller cung cấp; calculation/vector vẫn thuộc
RB-WP1-010.

**Evidence:** `benchmarks/schemas/v1`, `ProtocolVersionCompatibility`,
`HelloMessages`, `InitializeRunMessages`, `CapabilityNegotiator`,
`InitializeRunValidator`, Contracts/Runner boundary tests và
`06-event-contract-and-determinism.md` mục 2.1, 4.2, 9.

**Supersedes / superseded by:** Bổ sung cách hiện thực ADR-014; không thay unit,
ordering, lifecycle hay hash framing đã khóa.

### ADR-016 — 2026-07-29 — Accepted

**Context:** RB-WP1-008–015 cần làm event/order/hash/NDJSON/session chạy thật
nhưng WP1 chưa có Domain reducer, solver hay commitment validator. Nếu runner
phát action hoặc certificate rỗng, fixture có thể bị hiểu nhầm là online
behavior đã tồn tại. State identity hash và dedup cache cũng cần exact,
bounded semantics để replay không phụ thuộc runtime.

**Decision:** Event payload v1 giữ exact input order và structural validation
chỉ kiểm schema/sequence/epoch/simulation time. Runner WP1 trả message
`decision` với `status = notProduced`, reason `WP1_STRUCTURAL_ONLY`, không action,
certificate `notProduced` và solver `notRun`; đây là acknowledgement có hash,
không phải routing decision. State identity structural dùng domain
`RideBound.StateIdentityHash.v1\0` và tagged frame
`canonicalStateIdentity`. Manifest/decision hash giữ nguyên ADR-014. Session chỉ
commit epoch/next sequence/previous decision hash sau exact `decisionApplied`.
Dedup giữ đúng một canonical batch/response gần nhất: exact retry trả byte-equivalent
response, cùng key khác payload hoặc partial overlap làm session failed.
Stdout chỉ có canonical NDJSON; diagnostic code đi stderr.

Q1 tính 9 required scenario là `future-behavior`; chỉ duplicate fixture và full
tiny transcript là runner-executable. Mốc đóng Q1 dùng full Release suite 157/157.
Sau khi thêm một assertion đồng bộ vocabulary, Contracts pass 115/115 và inventory
thành 158. Enterprise Code Integrity trên máy này có thể chặn unsigned/fresh DLL
theo lần build với `0x800711C7`; event log 3033/3077 ghi policy
`0283ac0f-fff1-49ae-ada1-8a933130cad6`. Lần full-suite cuối chặn fresh Runner.dll
cả ở Release. Đây là environment/configuration exception được ghi rõ, không phải
bỏ test, sửa policy hay assertion failure.

**Alternatives considered:** phát decision/action giả; dùng object certificate
rỗng; advance state ngay khi nhận event; cache mọi batch không giới hạn; nhận
partial retry; ghi banner/diagnostic vào stdout; bỏ Domain smoke khỏi test count.

**Consequences:** WP2 có executable protocol oracle nhưng vẫn phải cài behavior
thật qua reducer/validator. Replay phát hiện sửa/thứ tự/hash; retry không làm
advance state và memory dedup bounded. Q1 không chứng minh portable cross-system,
online insertion, hard budget hoặc certificate soundness. Default Debug test
phải được chạy lại trên CI/máy không có policy này, nhưng không chặn evidence
Release đã pass đầy đủ.

**Evidence:** `EventBatchMessages`, `DecisionMessages`, `ErrorMessage`,
`ProtocolHash`, `NdjsonReader`, `NdjsonWriter`, `RunnerSession`, `RunnerHost`,
`benchmarks/schemas/v1`, `benchmarks/schemas/fixtures/golden/required`,
`benchmarks/schemas/fixtures/runner`, 115 Contracts tests, 35 Runner tests,
7 Architecture tests và 1 Domain test; exact per-run evidence ở Q1 closure block.

**Supersedes / superseded by:** Bổ sung executable semantics cho ADR-014/015;
không đổi version, unit, event order, manifest/decision hash framing hoặc claim
boundary.

### ADR-017 — 2026-07-29 — Accepted

**Context:** Revalidation end-to-end WP1 phát hiện cache idempotency dùng key
run/scenario/epoch/sequence range nhưng chỉ hash `payload.events`. Một retry giữ
nguyên event payload và sequence nhưng đổi envelope `simTimeMs` được trả lại
decision cũ có time/hash của batch ban đầu. Điều này trái nghĩa “exact retry”,
có thể che transcript corruption và làm canonical input thực tế khác response
được cache.

**Decision:** Identity của retry nguyên batch gồm key run/scenario/epoch/sequence
range và SHA-256 của toàn canonical `eventBatch` envelope + payload. Mọi thay đổi
canonical context, gồm `schemaVersion`, `runId`, `scenarioId`, `epochId`,
`simTimeMs`, hoặc payload dưới cùng batch key là
`DUPLICATE_PAYLOAD_CONFLICT`/`failSession`. Exact retry tiếp tục trả
byte-equivalent cached response và không advance state/hash.

**Alternatives considered:** thêm riêng `simTimeMs` vào cache key rồi phân loại
thành overlap; chỉ so raw JSON bytes; giữ payload-only hash và tin client không
đổi context.

**Consequences:** Property order/whitespace khác nhưng canonical batch tương
đương vẫn idempotent; thay đổi semantic context không còn bị nhận nhầm. Đây là
bug fix patch-compatible cho behavior invalid, không đổi schema/unit/order/hash
framing của decision. Regression test changed-time duplicate và clean-process
replay được giữ trong Runner suite.

**Evidence:** `RunnerSession.CalculateCanonicalBatchHash`,
`Same_duplicate_key_with_changed_simulation_time_fails_session`,
`Published_transcript_replays_twice_through_clean_processes` và
`Canonically_equal_duplicate_ignores_json_formatting`; Runner Release 38/38 và
full solution Release 161/161. Debug fresh Runner test assembly bị host
policy chặn trước discovery; 123 non-Runner test pass.

**Supersedes / superseded by:** Làm rõ “exact retry” trong ADR-016; không
supersede lifecycle/hash-chain decision khác.

### ADR-018 — 2026-07-29 — Accepted

**Context:** Sau Q1, WP2 cần state/reducer/B1 thật nhưng chưa có quyết định chính
xác về ownership, bootstrap vehicle/travel state, atomic event reduction,
frozen-prefix semantics hoặc O-001 reassignment. Đưa Contracts DTO vào Domain,
commit từng event giữa batch hoặc mở reassignment incumbent ngay sẽ phá Clean
Architecture và làm exact-small baseline khó kiểm chứng.

**Decision:** Domain sở hữu run/request/vehicle/route state và invariant thuần;
Application sở hữu internal ordered-event reducer/orchestration; Runner boundary
map Contracts DTO sang internal events. Batch được validate/fold nguyên tử và
domain/plan state chỉ commit tại matching `decisionApplied`. Route dùng exact
executed/locked frozen prefix, mutable suffix và no-op candidate bắt buộc.
Vehicle/travel bootstrap đi qua typed epoch-one events. B1 WP2 cho pending
request chọn vehicle nhưng cấm incumbent accepted request đổi vehicle; mở lại
cần ADR superseding cùng atomic multi-vehicle/exact-small evidence. B1 chỉ dùng
physical constraints, accepted preservation, integer operational cost và stable
tie-break; không có commitment gate.

**Alternatives considered:** đặt reducer trong Contracts; cho Domain nhận
`EventBatchPayload`/`JsonElement`; mutate state từng event; đưa initial mutable
state vào manifest; cho reassignment ngay; kéo OR-Tools/WP3 ledger vào WP2.

**Consequences:** WP2 có queue nhỏ và test-first, giữ Domain/Application độc lập
và cô lập B1 physical baseline. B4/no-reassignment chưa là baseline phân biệt
trong WP2; WP4 phải ghi rõ equivalence hoặc mở reassignment B1 trước khi đánh giá
B4. Q1 structural conformance transcript được giữ bằng path có tên, còn online
B1 không được trả `WP1_STRUCTURAL_ONLY`.

**Evidence:** `04`, `05`, `06`, `08`, `15`,
`tasks/25-wp2-online-state-refinement.md`,
`tasks/26-wp2-online-baseline-ticket-plan.md`.

**Supersedes / superseded by:** Đóng O-001 cho WP2 và bổ sung ADR-011/016; không
đổi protocol v1 hoặc quyết định ledger/certificate của WP3.

### ADR-019 — 2026-07-29 — Accepted

**Context:** `RB-WP2-002..006` cần biến ADR-018 thành types/transition có thể
chạy, nhưng các chi tiết wire route/travel, proposed-state ownership, edge
progress scheduling và witness order chưa có executable semantics. Nếu reducer
nhận raw JSON, snapshot ngoài ghi đè route core hoặc validator tin cost/schedule
do candidate gửi, Q2 correctness không còn độc lập.

**Decision:** Protocol event v1 dispatch exact typed payload theo `eventType`.
Vehicle snapshot mang full canonical position/rider sets và route gồm
`planVersion`, `executedStopCount`, exact `frozenPrefix` cùng `mutableSuffix`;
stop pickup/drop mang request ID, waypoint không mang. Travel snapshot là directed
arc semantic set có version/hash; snapshot đầu phải khớp manifest và bản sau tăng
đúng một. Domain state/route là immutable và transition trả state mới hoặc stable
witness. Runner map toàn wire batch sang internal event trước khi Application
fold; một lỗi bỏ toàn proposed state. Committed state chỉ đổi qua matching
acknowledgement. Physical validator tự suy schedule từ current position,
remaining route và travel lookup; với edge progress nó cộng phần thời gian cạnh
còn lại bằng integer ceiling. Thứ tự witness deterministic bắt planVersion,
frozen prefix, connectivity/schedule, physical service và incumbent preservation.

**Alternatives considered:** giữ `JsonElement` payload; dùng catch-all
`fixtureIntent`; mutate aggregate từng event; cho external vehicle snapshot thay
route core; dùng schedule/cost candidate làm proof; đưa network/solver vào
validator; cho incident behavior chạy sớm.

**Consequences:** Contracts không phụ thuộc Domain; Application chỉ phụ thuộc
Domain; online fixture có thể map/reduce hai epoch mà chưa cần B1. Invalid event
cuối không tạo partial commit. Validator bắt capacity, pickup window,
max-ride-time, precedence, stop location, connectivity, plan/prefix,
onboard/accepted preservation và incumbent reassignment bằng witness máy đọc
được. Incident vẫn typed ở wire nhưng reducer trả unsupported tới WP3. WP2/Q2
chưa hoàn thành cho tới candidate/B1/exact-small/Runner online tickets.

**Evidence:** `OnlineEventModels`, `EventBatchPayloadCodec`,
`RideBoundRun`, `RideRequest`, `VehicleState`, `RoutePlan`,
`TravelTimeSnapshot`, `EventReducer`, `EventReductionCoordinator`,
`OnlineEventMapper`, `PhysicalPlanValidator`; published fixtures trong
`benchmarks/schemas/fixtures/wp2`; Debug/Release 278/278 cùng transition,
mutation và 24-permutation tests.

**Supersedes / superseded by:** Hiện thực hóa ADR-018; không đổi lifecycle/hash
chain của ADR-016/017 và không kéo commitment semantics WP3 vào WP2.

### ADR-020 — 2026-07-30 — Accepted

**Context:** `RB-WP2-007..012` phải biến state/reducer/physical validator của
ADR-018/019 thành baseline B1 chạy online, nhưng vẫn cần giữ candidate
completeness kiểm được, no-op, deterministic selection, independent oracle và
Q1 transcript oracle. Nếu generator tự chứng nhận, exact mode âm thầm truncate,
Runner commit trước ACK hoặc B1 phát certificate commitment, WP2 sẽ vượt claim
và làm mất oracle cho WP3/WP4.

**Decision:** `RideBound.Algorithms` sinh mọi precedence-preserving pickup/drop
insertion trong exact-small bound trên mutable suffix, giữ exact frozen prefix
và luôn xét no-op. Mỗi leaf được `PhysicalPlanValidator` kiểm; exact bound/cap
vượt giới hạn fail rõ, bounded mode cắt theo stable ID và giữ no-op. Fleet
selector exhaustive chọn đúng một candidate/vehicle, không serve request hai
lần, tối đa accepted count, tối thiểu checked integer operational cost rồi
tie-break bằng candidate ID ordinal. Selected candidate được validator độc lập
kiểm lại trước immutable apply; incumbent accepted không đổi vehicle và không
thành rejected. Runner default là online B1; Q1 shell chỉ còn trong named
`--mode conformance`. Full committed online state, event batch và typed actions
đi vào state/decision hash; route/request state chỉ commit sau exact matching
`decisionApplied`. Certificate WP2 luôn `notProduced`.

Exact-small oracle trong test tự enumerate/evaluate, không gọi production
generator/selector/validator. Published differential bound là tối đa 2 vehicle,
2 pending request và 32 deterministic seeds. Tiny demo khóa four-epoch
accept/pickup/board, physical capacity reject, drop/alight transcript cùng exact
final hash. WP2 được đóng chỉ cho physical/B1; P1/P2/P3 commitment, hard budget,
ledger, incident breach, checkpoint và certificate `produced` thuộc WP3.

**Alternatives considered:** chỉ sinh single-request candidate; dùng production
generator làm oracle; dùng weighted scalar/tie ngẫu nhiên; truncate exact mode;
commit ngay khi phát decision; overwrite Q1 golden bằng B1 output; coi
`PhysicalPlanValidator` là commitment certificate; kéo OR-Tools hoặc ledger vào
WP2.

**Consequences:** F-003/F-004 có executable B1 evidence, Q1 vẫn là regression
oracle có tên và WP3 nhận state/hash/ACK boundary thật. Baseline exhaustive chỉ
claim correctness trong published small bound, không claim scale/performance.
Required Debug `dotnet test RideBound.slnx` pass 333/333. Release xUnit vẫn bị
Windows Application Control `0x800711C7` chặn fresh unsigned Application/
Runner DLL trước assertion; policy-safe bundles và process checks pass cho đúng
Release artifacts đó, nhưng không được gọi là Release full-solution xUnit pass.

**Evidence:** `Candidates`, `Policies`, `ExactSmallOracle`,
`OnlineDecisionActionMapper`, `OnlineStateCanonicalizer`, online `RunnerSession`,
typed action schemas/tests, `benchmarks/scenarios/wp2-tiny`,
`scripts/run-wp2-tiny-demo.ps1` và execution plan `26`.

**Supersedes / superseded by:** Hoàn tất ADR-018/019 cho phạm vi WP2; không
supersede protocol/hash framing ADR-014/016/017. WP3 có thể bổ sung commitment
gate/certificate bằng ADR mới nhưng không được đổi physical B1 oracle âm thầm.

### ADR-021 — 2026-07-30 — Accepted

**Context:** WP3 phải thêm promise history, three-way delta, vector budget, phase
locks, incident, certificate và checkpoint lên state/hash/ACK thật của WP2.
Tài liệu `07` trước đó liệt kê `decisionHash` bên trong ledger/certificate, nhưng
nhúng current decision hash vào chính state/body đang được hash sẽ tạo vòng tự
tham chiếu. Candidate evaluator cũng đang sở hữu schedule riêng, dễ làm promise
projection lệch physical schedule. Mức số O-002/O-003 chưa được pilot khóa.

**Decision:** Domain sở hữu stable 10-dimension vocabulary, canonical vector/
explicit policy, promise/version/service tokens, immutable append-only ledger,
budget và phase-lock invariant. Application sở hữu một `RouteScheduleProjector`
dùng chung cho Algorithms và promise flow, `PromiseProjector` cùng
`PromiseDeltaCalculator`; đổi stop node bắt buộc có `IStopDistanceLookup` mm.
Three-way delta tính độc lập `old→exo`, `exo→new`, `old→new`; không giả
`visible=exo+decision`. Initial promise version 1 không tiêu budget; revision tăng
đúng một, round-trip không refund. Ledger nằm trong pending `OnlineState` và chỉ
commit cùng route sau matching ACK.

Ledger dùng stable `publicationId`, không nhúng current `decisionHash` vào state.
Ticket certificate/Runner sau sẽ bind input/state/publication bằng containing
decision envelope hash. `null` hard limit là unbounded, zero là hard zero; không có
default numerical profile. Accepted assignment luôn lock theo O-001; onboard khóa
pickup, freeze/final confirmation chỉ qua explicit policy flags. Incident là breach
record riêng ở ticket `008`.

**Paper/claim evidence:** Full text Multiple-plan dynamic DARP xác nhận dynamic
insertion, plan pool, consensus và least-commitment đã có; Time-consistent DARP
xác nhận consistency/time classes/cost trade-off đã có; forward-looking dispatch
xác nhận rolling/future-aware matching/detour safeguards đã có. RideBound chỉ giữ
claim hẹp per-rider, path-dependent, multi-dimensional cumulative/switch ledger
với machine-checkable certificate.

**Alternatives considered:** schedule commitment riêng trong Algorithms; nén
budget thành weighted scalar; dùng travel time thay stop distance; lưu current
decision hash trong state; chọn default “medium” profile; nối ngay vào Runner khi
incident/validator/schema chưa tồn tại.

**Consequences:** `RB-WP3-001..007` tạo foundation và executable tests nhưng
default B1 Runner vẫn certificate `notProduced`; chưa có C1/P2 guarantee. Queue có
14 ticket, đúng nửa đầu DONE và `RB-WP3-008` là next duy nhất.

**Evidence:** `Domain/Commitments`, `Application/Scheduling`,
`Application/Promises`, refactored `CandidateScheduleEvaluator`, Domain/
Application tests, `tasks/28-wp3-ledger-certificate-ticket-plan.md`; required
Current-tree suite evidence 378/378; host policy exception 0x800711C7 được
ghi riêng, không tính là assertion failure.

**Supersedes / superseded by:** Bổ sung ADR-020 và chỉnh ledger/certificate
self-binding trong `07`; không đổi protocol hash framing ADR-014/016/017, không
mở reassignment O-001 và không chọn O-002/O-003.

### ADR-022 — 2026-08-02 — Accepted

**Context:** `RB-WP3-008..014` phải biến vocabulary/foundation của ADR-021 thành
publication gate thực sự. Audit toàn tuyến phát hiện các đường đi mà test cục bộ
trước đó chưa khóa: candidate có thể mutate state ngoài route, genesis vehicle
có thể preload pending stop, checkpoint có thể chụp pending decision hoặc dựng
state không reachable, certificate có thể không khớp publication actions, breach
có thể không khớp normal ledger và initial state hash chưa bao phủ full state.
Đồng thời B1 chỉ tối ưu exhaustive trong candidate set đã sinh; earliest-feasible,
single-plan, four-pending bound và incumbent-order preservation không phải
state-of-the-art optimization đầy đủ.

**Decision:** Hoàn thành đủ 14 ticket WP3 như một hard correctness boundary.
Incident/breach là immutable ledger riêng. `CommitmentDecisionValidator` tự dựng
lại physical feasibility, immutable state boundary, promise/delta, locks và
budget; candidate filter chỉ early-prune, Runner luôn full-fleet revalidate.
Produced certificate phải bind exact input/proposed state hash và tập publication
ID trong actions. Commitment policy là canonical named config có content hash.
Checkpoint chỉ được phát khi không có pending decision và restore phải qua hash,
manifest, travel identity, genesis/post-genesis reachability cùng ledger/breach
cross-relations. Publication vẫn commit duy nhất tại matching ACK.

**Optimization/claim decision:** Hard vector không được nén vào weighted scalar
hoặc đưa vào objective như soft preference. WP3 chứng minh feasibility/auditability,
không chứng minh C1 tốt hơn B1. Inversion/relocation/vehicle-switch dimensions
được validator hỗ trợ nhưng B1 hiện không chủ động sinh chúng. Schedule strategy,
candidate loss, forward slack/precompute, multiple-plan pool, lexicographic/Pareto
selection và OR-Tools thuộc `RB-WP4-001` refinement. Không sao chép số horizon,
runtime hoặc stated preference từ paper thành default.

**Paper/claim evidence:** In-app Browser đối chiếu Gaul et al. (2021), Schulz &
Pfeiffer (2026), Geržinič et al. (2023), Tiwari et al. (2024), Ackermann & Rieck
(2025). Evidence hỗ trợ rolling horizon, slack/precompute, history sensitivity,
lexicographic/Pareto và multiple-plan baselines nhưng không cấp một universal
numeric policy hoặc novelty claim mới. Mapping chi tiết nằm trong `03` và `21`.

**Alternatives considered:** trust solver-provided delta/certificate; chỉ thêm
if/else vào `RollingCostPolicy`; serialize partial checkpoint; dùng incident để
refund budget; triển khai OR-Tools ngay; gọi current B1 globally optimal; lấy
10–15 phút/99.5%/survey coefficients làm default.

**Consequences:** WP3 Complete; logical inventory 414. Required full-solution
command tại thời điểm chấp nhận ADR bị Windows Application Control `0x800711C7`
chặn fresh DLL, nên
evidence được tách minh bạch thành unaffected suites, 54/54 policy-safe Runner
methods và bốn clean-process cases; không gọi đó là full-solution pass. Chỉ
`RB-WP4-001` refinement READY, không production WP4 implementation nào READY.
Revalidation sau đó ngày 2026-08-03 đã pass full solution 414/414; ADR không đổi
claim boundary vì đây chỉ là thay đổi trạng thái host-policy evidence.

**Evidence:** `Domain/Incidents`, `Application/Commitments`, commitment filter,
strict Contracts/schema, `Runner/Configuration`, `OnlineStateCheckpointCodec`,
WP3 tiny scenario/script, exact-small/property/mutation tests,
`reviews/wp1-wp3/README.md`, `tasks/28` và `tasks/29`; Release build/format và
published replay hashes trong mục 4.

**Supersedes / superseded by:** Hoàn tất executable semantics của ADR-021 và
đóng WP3; không supersede ADR-014/016/017 hash framing, ADR-018 O-001 hoặc
ADR-020 physical B1 semantics. ADR-023 của WP4 chỉ được bổ sung sau refinement.

### ADR-023 — 2026-08-03 — Accepted

**Context:** Audit WP1–WP3 cho thấy hard-vector gate là correctness mechanism
thật nhưng B1 vẫn dùng earliest-feasible, single plan, four-request/ID cap và
Cartesian selector. Nếu C1 được cấp raw candidate khác, prune sau cap không được
ghi, hoặc OR-Tools incumbent tự publish, đánh giá sẽ trộn commitment effect với
compute/candidate bias. Multiple-plan/waiting/repair còn yêu cầu state và
checkpoint semantics, không thể thêm bằng vài nhánh `if/else` trong B1.

**Research evidence:** In-app Browser đọc lại Gaul et al. 2021, Schulz &
Pfeiffer 2026, Tiwari et al. 2024 và Ackermann & Rieck 2025; đọc bổ sung
Mitrović-Minić & Laporte 2004 về drive/wait/dynamic waiting, Masson–Lehuédé–
Péton 2013 và Gschwind 2019 về forward-time-slack/incremental feasibility,
Ackermann & Rieck 2022 về future insertion guidance, cùng official OR-Tools/
NuGet. Evidence hỗ trợ mechanism/baseline, không cho universal horizon, pool
size, weight, budget hoặc effectiveness target.

**Decision:** Khóa đủ 12 quyết định trong `tasks/30`:

1. B1–B5/C1/C2 dùng cùng raw physical candidate set và cap trước policy gate;
   report request/candidate omission, hard-prune và solver loss riêng.
2. Main schedule là `earliest-feasible`; named wait control chuyển waiting slack
   có thật thành current-node hold waypoint, không chỉ sửa ETA nội bộ.
3. Exact mode fail nếu omit. Bounded priority là latest pickup/arrival/ID; cap
   theo accepted count, admissible operational key, slack reserve và stable ID.
4. Slack/precompute cache bind full route/position/time/travel identity; chỉ
   early-prune, cache miss khi key đổi và cached/uncached phải tương đương.
5. Repair chỉ remove/reinsert waiting incumbent trong cùng vehicle, giữ O-001;
   B4 được ghi rõ `no-reassignment-repair`.
6. B5 plan pool/version/distinguished plan nằm trong canonical state/checkpoint;
   alternative incompatible với executed/frozen decisions bị loại.
7. Multi-pass lexicographic là accepted → policy utilization/warning → 10
   revision dimensions → operational cost → candidate-ID vector; không scalar
   hard vector. Normalized utilization chỉ là checked ranking ppm.
8. C1/C2 cùng hard gate; C2 warning chỉ xếp hạng trong hard-feasible set.
9. Solver-neutral port/model ở Application, policy ở Algorithms, package
   `Google.OrTools 9.15.6755` chỉ ở Solvers.OrTools.
10. Replay dùng deterministic work/CP deterministic-time budget, one worker và
    explicit seed. Wall time chỉ metric; status/bound/gap/fallback truthful.
11. Exact-small bound 2 vehicle/2 pending/1 repair incumbent/pool 4, ít nhất 64
    seeds; infinite budget/locks off/earliest/no-repair/single-plan bằng B1.
12. Solver/pool không publish trực tiếp; full WP3 validator, certificate, state
    hash, pending transaction và matching ACK vẫn là gate cuối.

**Alternatives considered:** hard gate thành weighted penalty; C1 sinh thêm raw
candidate sau prune; cap bằng hash ID; cache không bind travel/version; latest ETA
chỉ trên paper mà không có route hold; mở cross-vehicle reassignment; in-memory
plan pool không checkpoint; CP-SAT nhiều thread; dùng wall-clock timeout làm
replay outcome; báo FEASIBLE thành OPTIMAL; lấy số paper làm default.

**Consequences:** `RB-WP4-001` Done, queue `RB-WP4-002..014` được phép thực hiện
tuần tự và chỉ `002` Ready. WP4 vẫn ở claim Implemented/Mechanically valid cho
từng ticket; hiệu quả chỉ được gọi là tín hiệu micro/exact-small trước paired
Layer 1/2. O-001/O-002/O-003/O-004 không đổi.

**Evidence:** `tasks/29`, `tasks/30`, Browser sources trong `21`; required suite
baseline 414/414 trước production WP4.

**Supersedes / superseded by:** Bổ sung ADR-020/022 cho policy/solver quality;
không đổi protocol/hash, reassignment O-001 hay validator/certificate semantics.

### ADR-024 — 2026-08-03 — Accepted

**Context:** `RB-WP4-002..012` đã tạo đầy đủ mechanisms nhưng closure còn cần bằng
chứng độc lập và audit source-level để tránh kết luận từ test happy path. Đặc biệt
cần chứng minh hard gate thực sự loại candidate, mapper/OR-Tools không cùng lỗi với
expected code, bounded omission đi xuyên diagnostics, và machine-local timing
không bị nâng thành effectiveness claim.

**Decision:** Đóng WP4 và Q2 core mechanical correctness vì:

1. B1 generator/selector khớp independent enumeration trên 64 fixtures trong
   published exact-small bound.
2. Production C1 mapper + actual pinned OR-Tools khớp một enumerator độc lập khác
   trên 64 fixtures; mọi objective level optimal với exact gap 0.
3. Hard-gate mutation fixture có raw set lớn hơn hard-feasible set; actual bounded
   omission truyền count/digest tách khỏi solver loss và validated fallback.
4. Cache/infinite-equivalence/plan-pool/checkpoint/deadline/replay/publication gates
   từ tickets trước vẫn pass trong full suite 557/557.
5. Source audit xác nhận objective không scalarize hard vector, solver không tự
   publish, candidate/solver/publication loss tách biệt và matching ACK là commit.
6. Synthetic runtime curve chỉ được ghi là promising machine-local signal; paired
   Layer 1/2, scale, service effect và user satisfaction vẫn unproven.
7. Review `docs/reviews/wp1-wp4-final/` là handoff hiện hành; historical review
   WP1–WP3 được giữ nguyên.

**Alternatives considered:** đóng chỉ vì 523 tests pass; dùng production comparer
làm oracle; ghi microbenchmark thành production SLA; mở luôn BeGo migration mà
chưa khóa process/transaction ownership; xóa historical environment blocker.

**Consequences:** `RB-WP4-001..014` Done, WP4 Complete. Windows Application Control
`0x800711C7` không tái xuất hiện ở closure run nhưng historical record không bị
xóa. Chỉ refinement-only `RB-WP5-001` Ready; không có WP5 implementation ticket.
O-001/O-002/O-003/O-004 và protocol/hash/validator/certificate semantics không đổi.

**Evidence:** closure blocks `RB-WP4-013/014`, `tasks/30`, `tasks/31`, final review,
required suite 557/557, Release/format/vulnerability/JSON/Markdown/process/diff gates.

**Supersedes / superseded by:** Đóng execution của ADR-023; không supersede
ADR-014/016/017/020/022 hoặc claim boundary trong `03`/`21`.

### ADR-025 — 2026-08-05 — Accepted

**Context:** WP4 đã đóng core mechanical correctness nhưng BeGo hiện là snapshot
outing application: `Session`/`PickupRequest` không phải online RideBound aggregate,
không có append-only event/decision/ACK/outbox state, và flow cũ không thể cho biết
Runner call đã commit qua các crash window. Gọi child process rồi `SaveChanges`/
SignalR bằng happy-path `if/else` sẽ tạo khoảng mất/nhân đôi decision và có thể
manufacture ACK/certificate sai.

**Research evidence:** In-app Browser đọc paper Saltzer–Reed–Clark về end-to-end
duplicate suppression/ack/crash recovery; Helland về local transaction entity,
at-least-once messaging và durable activity state; transactional outbox pattern;
official EF Core transaction/optimistic concurrency; PostgreSQL locking/
`SKIP LOCKED`; official ASP.NET hosted service; và IETF HTTPAPI Idempotency-Key
draft-07. Draft cuối đã expired/archived ngày audit nên chỉ dùng như prior art,
không claim RFC compliance. Chi tiết và URL ở
`research/wp5-distributed-integration-evidence-2026-08-05.md`.

**Decision:**

1. Adapter/API/EF/SignalR code nằm trong tracked BeGo `src`; RideBound không copy/
   reference source BeGo và BeGo không reference RideBound core assemblies. BeGo
   chỉ gọi exact versioned `RideBound.Runner` artifact qua NDJSON.
2. O-007 đóng cho WP5 bằng long-lived child process. Config tách command path,
   artifact path, expected binary SHA-256, core commit, mode/policy/config hash;
   preflight mismatch fail closed.
3. Một `CommitRun` là serialization entity: một pending operation/decision, event
   sequence/epoch liên tiếp. Nhiều worker có thể xử lý nhiều run; database lease +
   partial uniqueness ngăn hai owner cùng run.
4. Chỉ local transaction ngắn: T1 append idempotency/event/work; external Runner;
   T2 persist exact decision/certificate/rebuildable projection/outbox; external
   matching ACK; T3 persist ACK/checkpoint. Không giữ DB lock qua process/SignalR.
5. Delivery là at-least-once với idempotent effect, không claim exactly-once.
   Composite idempotency scope bind actor/route/run/key và canonical payload hash;
   same key khác fingerprint conflict, in-flight retry không cấp sequence mới.
6. Outbox cùng T2; relay dùng stable message ID, deterministic per-run order và
   lease. `SKIP LOCKED` chỉ dùng queue claim, không dùng audit query.
7. Crash/ACK uncertain buộc bỏ process handle, start đúng binary, initialize cùng
   manifest, restore checkpoint, replay committed suffix, replay pending event,
   compare exact decision hash rồi mới ACK. Mismatch chuyển `Diverged`, không publish.
8. Bootstrap tạo run-local pseudonymous ID/node map, E7/ms ties-to-even, full
   directed travel matrix và provenance từng field. Time window/max ride phải từ
   explicit override hoặc named stored profile; thiếu thì fail, không có hidden default.
9. Feature flag mặc định off giữ Session/endpoints hiện hành. Runtime rollback là
   stop claim/disable; không xóa append-only evidence. Raw identity link tách khỏi
   pseudonymous research export; log/realtime payload không chứa exact location,
   token hoặc raw witness mặc định.
10. Paired B1/C1 dùng cùng source input, seed, binary/work rules và allowlist duy
    nhất policy/config fields; replay cùng arm phải byte/hash exact. Kết quả chỉ là
    Layer-1 mechanical/descriptive signal trước WP8/WP9.
11. Ordered queue `RB-WP5-002..014` thực hiện tuần tự; mỗi code ticket chạy BeGo
    targeted/full backend, required RideBound suite, frontend khi surface đổi,
    và real PostgreSQL gate khi liên quan persistence/concurrency.

**Alternatives considered:** đặt adapter trong RideBound và project-reference qua
repo; gắn fields vào `Session`; distributed transaction qua DB/process/SignalR;
call Runner trong DB transaction; in-memory `Channel` làm durable queue; key-only
dedup; spawn process mỗi event; retry ACK trên uncertain live process; import old
snapshot thành ledger; auto default missing time windows; HTTP/gRPC ngay WP5;
drop evidence khi rollback.

**Consequences:** `RB-WP5-001` Done, no production implementation claim. Queue
`tasks/32` có đúng `RB-WP5-002 READY`. Persistence/recovery phức tạp hơn direct
call nhưng mọi crash window có durable interpretation, Runner vẫn là decision
authority và BeGo cũ có default-off rollback.

**Evidence:** pinned checkouts/baselines trong mục 4; `tasks/31`, `tasks/32`,
research evidence; Browser excerpts; source audit BeGo Domain/Application/
Infrastructure/API; required RideBound 557/557, BeGo backend 25/25, frontend 7/7.

**Implementation amendment 2026-08-05 (`RB-WP5-003..004`):** T1 không nhận
caller wall clock; lease dùng `transaction_timestamp()` trong DB. Event metadata
epoch/time/contiguous sequence phải bind exact canonical frame; canonical batch
hash bảo vệ bytes của Runner. `jsonb` chỉ phục vụ query; mọi replay/Runner write lấy strict
UTF-8 canonical `bytea`, vì PostgreSQL được phép normalize JSON text. Event batch
lưu cả first/last sequence; same-run composite FK ngăn operation/decision/
checkpoint cross-link. Queue claim dùng ordered `SKIP LOCKED`, bounded capacity
và commit lease trước khi trả work ra ngoài transaction.

**Implementation amendment 2026-08-05 (`RB-WP5-005`):** Runner artifact được
pin bằng absolute command/artifact path, SHA-256 và core commit; process pool có
giới hạn rõ và session là run-local. Client tự kiểm schema/capability/manifest/
run/epoch/time ở cả hai chiều, giới hạn exact UTF-8 line và stderr, gom
`decisionApplied` + checkpoint vào một I/O critical section. Mọi timeout,
cancellation, malformed response hoặc identity mismatch remove session rồi kill
toàn process tree. Dispose/start được serialize để không rò process qua race.

**Implementation amendment 2026-08-05 (`RB-WP5-006`):** Bootstrap được tách
thành synchronous immutable source capture trước external I/O và completion sau
exact capability negotiation. Manifest hash không còn là input trước `helloAck`;
nó được tính từ canonical manifest bằng đúng domain `RideBound.ManifestHash.v1`
sau khi bind exact selection. Adapter chỉ materialize graph node cần thiết, áp
node cap trước complete O(n²) matrix call, fail closed mọi missing/unreachable/
ambiguous conversion và giữ legacy state dưới dạng hashed provenance-only.

**Implementation amendment 2026-08-05 (`RB-WP5-007`):** Sửa một coupling sai
trong `003..004`: idempotency fingerprint bind canonical HTTP method/resource/
path/body semantics, không bind `eventSeq`/epoch do server cấp; exact eventBatch
bytes tiếp tục có hash riêng. Create-run khóa composite idempotency bằng PostgreSQL
advisory transaction lock trước lookup/insert để cùng key luôn replay cùng winner,
không phụ thuộc thứ tự unique index. HTTP chỉ expose user-safe views và exact
cached response; controller không nhận raw protocol/ledger/certificate input.

**Implementation amendment 2026-08-09 (`RB-WP5-008`):** Runner response được
materialize lại trong T2 từ exact canonical frame; decision, certificate,
projection, timeline và outbox commit cùng transaction. Claim bằng raw SQL phải
clear committed EF snapshots có guard trước khi T2/T3 lock row, nếu không identity
map có thể trả revision trước claim và làm fence sai. ACK outcome không chắc chắn
luôn bỏ session, reconstruct fresh Runner và so byte/hash exact; T3 dùng DB time,
owner và revision. Promise service order ở BeGo boundary tái lập invariant Domain,
không chỉ schema field checks. Cơ chế này là at-least-once retry với durable
idempotent effect theo RIFL/outbox prior art, không phải exactly-once delivery.

**Implementation amendment 2026-08-09 (`RB-WP5-009`):** Outbox claim chọn exact
unpublished head của mỗi run trước khi xét availability/lease, nên backoff hoặc
slow head không cho sequence sau overtaking; `SKIP LOCKED` vẫn cho run khác tiến.
Claim tăng `attempt_count` bằng DB time và commit trước SignalR; mark/reschedule
phải khớp message/owner/attempt/unexpired lease. Payload được tái kiểm exact
user-safe allowlist và Session target bị migration trigger khóa không retarget.
Source audit áp dụng đúng giới hạn Gray-Cheriton: lease không fence external send
đã bắt đầu. Wire vì vậy mang stable aggregate sequence/hash và frontend bỏ
duplicate/stale callback theo per-run cursor; offline gap vẫn cần timeline `010`.
`SendAsync` không được ghi thành durable client acknowledgement hoặc exactly-once.

**Implementation amendment 2026-08-09 (`RB-WP5-010`):** Audit timeline dùng exact
row-value `(sequence,id)` keyset và server-owned access scope; raw subject link chỉ
phục vụ ownership join, raw decision/certificate chỉ qua operator policy mặc định
deny. JSONB phải canonicalize rồi kiểm hash/allowlist lại ở end-to-end boundary.
Projection không được coi là source of truth: repeatable-read rebuild tái tạo từ
append-only decision/certificate/operation, kiểm full hash/state/materializer chain
và so rebuilt/live hash; mismatch chặn export. Audit read không dùng `SKIP LOCKED`.
Migration rollback guard phải chạy trước destructive `Down`. Logging/telemetry/
export không mang subject, token, coordinate, route, witness hoặc raw evidence.
Các cơ chế này là correctness/rebuildability/privacy prior-art application, không
phải exactly-once, throughput/SLA, effectiveness hoặc novelty claim.

**Implementation amendment 2026-08-09 (`RB-WP5-011`):** Rollout mode là
`Disabled/Shadow/Live`; omission/default là Disabled và không đăng ký COMMIT hosted
service. Namespace Shadow/Live phải persist bất biến trên run, decision claim phải
lọc namespace và outbox store chỉ claim Live — ngừng đăng ký relay trong RAM là
không đủ vì live restart có thể phát shadow backlog. Existing rows backfill Shadow.
Mọi active worker/member boundary phải qua exact Runner artifact hash preflight;
preflight không spawn process. Disable/cancel ngừng claim mới, để durable lease hết
hạn và same-namespace worker reclaim theo existing fence. Feature rollback không
xóa append-only evidence hoặc sửa Session route; Down guard chạy trước destructive
DDL. Đây là operational correctness/compatibility, không phải effectiveness claim.

**Implementation amendment 2026-08-09 (`RB-WP5-012`):** Layer-1 pair dùng cùng
commitment-mode publication/validator path cho cả B1 và C1; không cho B1 né
certificate bằng online mode khác. Raw/canonical workload, provenance, common
policy và mỗi arm config đều hash-bound; effective config hash domain-separate bind
hai config. Ngoài `/policyId`, normalized config phải exact; initialize chỉ được
khác policy ID/effective hash và `decisionApplied.decisionHash` được phân loại rõ là
output-derived control. Validated config bytes được stage rồi kiểm trước/sau từng
clean process để tránh TOCTOU. Decision phải qua BeGo exact materializer, checkpoint
phải được tính lại độc lập, repeats phải exact, và bundle manifest reject file thiếu,
thừa hoặc tamper. Bundle bind harness source + executing assemblies vì working tree
chưa commit không được giả thành reproducible chỉ bằng base commit. Đây là
mechanical/correctness evidence, không phải effectiveness/non-inferiority/SLA claim.

**Implementation amendment 2026-08-09 (`RB-WP5-013`):** Failure evidence phải
hard-kill executable riêng tại từng durable decision/outbox boundary, có marker trước
crash và fresh-process recovery; exception/finally trong test process không được gọi
là hard crash. Expected state/claim được dựng bởi test-owned observed-history oracle,
không gọi production transition table. Concurrency phải so exact operation set trên
PostgreSQL thật với nhiều worker, không chỉ đếm tổng. Mutation gate gồm năm fault model
độc lập phá unique active-run, ACK/checkpoint, T2/outbox, fingerprint và canonical hash;
`5/5` chỉ là required-mutant result, không phải external mutation percentage.
Performance evidence randomize scenario order, warm-up, nhiều repetition và lưu raw
sample/machine/database/row counts; không assert SLA. LDFI, Elle, QuickCheck, mutation
testing và rigorous-performance papers cung cấp phương pháp, nhưng implementation
không chạy external LDFI/Elle/QuickCheck/mutation engine và không claim formal proof.

**Implementation/closure amendment 2026-08-09 (`RB-WP5-014`):** Dữ liệu dùng
để authorize member phải immutable: `commit_subject_links` dùng cùng append-only
reject trigger như evidence tables. Mọi outbox row phải bind non-null operation cùng
run. Query phải chọn absolute earliest unpublished head trước rồi kiểm head `Applied`,
vì T2 không được lộ trước T3 và row sau không được vượt head chưa T3. Per-run-head
SQL phải đi kèm application-level independent scope/DbContext
cho mỗi run; otherwise một SignalR send chậm vẫn gây cross-run head-of-line blocking.
Migration rollback tiếp tục fail closed khi còn dữ liệu và cần explicit guard. Closure
review không biến mechanical paired/fault/local-curve evidence thành formal delivery,
SLA hoặc effectiveness claim; nó chỉ cho phép mở đúng `RB-WP6-001` refinement.

**Supersedes / superseded by:** Khóa O-007 và thay proposed WP5 project placement
trong `19` bằng BeGo-owned adapter + artifact boundary. Không đổi protocol/hash,
O-001/O-002/O-003/O-004, WP3 publication gate hoặc ADR-024 claim boundary.

### ADR-026 — 2026-08-09 — Accepted

**Context:** WP1–WP5 đã đóng mechanical correctness/integration gate nhưng chưa có
common data-to-result harness. WP5 paired bundle chứng minh cùng exact Runner path
trên một fixture, không có dataset normalizer, public scenario identity, general run
grid, failure/exclusion denominator contract, independent metric oracle hoặc
experiment bundle. Viết harness trước khi khóa các semantics đó sẽ tạo survivorship
bias, metric leakage và artifact có checksum nhưng không tái lập được.

Primary evidence được đọc bằng in-app Browser và ghi tại
`docs/research/wp6-benchmark-reproducibility-evidence-2026-08-09.md`: FleetPy
Manhattan Zenodo/paper, RFC 8785 JCS, Random123, RFC 8493 BagIt, W3C PROV-DM,
FAIR, Datasheets for Datasets, Sandve reproducibility rules và ACM artifact
terminology. Contract đầy đủ nằm tại `docs/benchmarking/wp6-contract-v1.md`.

**Decision 1 — public dataset/source boundary:** Khóa FleetPy Manhattan case-study
data v1, DOI `10.5281/zenodo.15187906`, file `FleetPy_Manhattan.zip`, Zenodo MD5
`8b11882ae9c6d87f666bf6e006806744`, license `CC BY 4.0` làm public source chính.
Downloader phải kiểm publisher length/MD5 và local SHA-256 exact bytes. Raw archive
nằm trong ignored content-addressed read-only cache, không commit/overwrite. Safe
extractor fail closed với traversal/symlink/reparse/duplicate/case/size/ratio attack.
Derivative tiny/medium phải có attribution, transform recipe, source selection hash
và không suy observed preference/satisfaction/identity từ TLC/FleetPy trip records.

**Decision 2 — scenario identity/canonical units:** Scenario content là strict
versioned solver/simulator-neutral document. Canonical bytes dùng RideBound Canonical
JSON v1 — accepted-domain subset hẹp của JCS: UTF-8/no BOM, duplicate/invalid Unicode
reject, recursive ordinal property sort, sequence order preserved, no null/float, safe
integers only. Quantities dùng explicit scaled units; time semantics dùng relative ms.
Semantic identity dùng SHA-256 domain `RideBound.Wp6.Scenario.v1` và length-prefixed
frames, tách khỏi plain file SHA-256. Source bytes, member inventory, selection,
normalizer source/version/config và report hash đều được bind.

**Decision 3 — demand/event semantics:** Scenario khai báo source timezone/window,
warm-up/scoring/horizon/drain boundaries, explicit initial fleet, requests, directed
travel snapshots, driver semantics và event ordering. Nếu upstream có validated total
sequence thì giữ nó; nếu không dùng `(simTimeMs,typeRank,sourceOrdinal,stableId)` theo
`ridebound-event-order-v1`. Missing/unreachable arc làm source exclusion hoặc scenario
invalid; cấm zero/max/Euclidean/reverse imputation. Mọi source row có đúng một
selected/not-selected/excluded disposition và conservation report.

**Decision 4 — plan/pairing:** Benchmark plan được materialize trước outcome, chứa
scenario hashes, exact Runner/runtime/source, arm policy/version/config/effective
hash, candidate/validator/solver/work/capability binding, warm-up/repeats và resource/
failure/exclusion/metric profiles. `wp4-common-candidate-v1` chỉ pair B1/B2/B3/B4/
C1/C2 khi common raw candidate/work semantics exact; B5 ở
`wp4-multiple-plan-v1`. Một fresh isolated Runner process cho mỗi arm/repeat/attempt;
arm order HMAC-counterbalanced. Rerun có authorization tạo new plan/attempt và giữ
old evidence, không rerun seed xấu chọn lọc.

**Decision 5 — seed hierarchy:** Master seed là 256-bit hex. Component seed dùng
HMAC-SHA-256 domain `RideBound.Wp6.Seed.v1` trên scenario hash, repeat index,
component ID và stable item ID. Full digest được lưu; int32 conversion là first-four
big-endian masked `0x7fffffff`. Sampling/order dùng hash rank stable ID. Cấm clock,
GUID, thread/process ID, `Random.Shared`, implicit default, global call-count RNG và
unstable enumeration. Đây là addressable-randomness mechanism học từ Random123;
implementation không dùng/copy Random123 và không kế thừa claim của nó.

**Decision 6 — exact Runner boundary:** Harness/fixture/adapter chỉ phát exact
RideBound protocol input và consume exact output từ pinned external Runner process.
Không reference/call policy/core để tạo alternative result. Pre/postflight bind
assembly, runtime, config, source and staged input bytes. Fixture driver chỉ sở hữu
declared exogenous event/execution semantics; FleetPy control adapter vẫn thuộc WP7.
Mọi decision đi qua WP3 independent publication validator, certificate, state hash,
matching ACK/checkpoint path.

**Decision 7 — raw result and terminal conservation:** Mỗi planned run có immutable
input/output/stderr/resource/preflight/postflight files, observation index và đúng một
terminal record `succeeded|failed|excluded`. Index giữ event/request/vehicle/run/
scenario/arm/repeat locator nhưng không thay raw NDJSON. Plan conservation bắt buộc:
`planned = succeeded + failed + excluded`. Negative/policy-worse output vẫn được giữ.

**Decision 8 — failure taxonomy:** Typed failures gồm invalid input/artifact mismatch,
capability divergence, start/crash, wall/CPU/memory/resource breach, solver unknown,
invalid/incomplete protocol output, state divergence, metric oracle mismatch và
bundle invalid. Failure không thành metric 0/infeasible, không bị silent drop và
không được chuyển thành exclusion sau outcome.

**Decision 9 — exclusion/denominator:** Exclusion chỉ trước outcome theo exact
predeclared rules: license/checksum/invalid source/unreachable/capability/position/
pairing incompatibility. Append-only row ghi rule/source/stage/subject/evidence và
retained denominators. Mỗi metric ghi unit/window/scope/numerator/denominator ID/value/
missing semantics. Valid-run descriptions luôn đi cùng planned failure/exclusion
counts; denominator zero là missing, không phải rate 0.

**Decision 10 — metric ownership:** WP6 registry v1 tính unique arrived/accepted/
rejected/completed, defer actions, decision epochs, promise publications/revisions,
breaches, non-normal certificates, ten decision-delta sum/max và exact resource
metrics. Không lưu IEEE float; rate dùng integer PPM ties-to-even và checked wider
intermediate. Production calculator chỉ đọc raw evidence. Independent oracle nằm ở
separate source/executable, không reference production calculator/models, và phải tạo
byte-identical sorted rows/hash trên tiny bound. Mismatch invalidates bundle trước
aggregate/statistics. Runner/simulator aggregate column không là sole source.

**Decision 11 — resource accounting:** Plan khai báo wall/CPU/working-set/process/
stream byte limits, deterministic policy work budgets, enforcement kind, warm-up và
repeats. Supervisor dùng monotonic time, raw samples và process-tree termination;
machine/OS/CPU/memory/.NET/container/git/source/assembly/command provenance bắt buộc.
Local limit/timing là experiment control, không phải production throughput/SLA.

**Decision 12 — bundle integrity/provenance:** Result bundle dùng strict
BagIt-compatible SHA-256 profile: payload dưới `data/`, payload/tag manifests, logical
bundle manifest, README/verifier, exact role/media type/length/hash/producer/source
derivation. Strict RideBound verifier reject absolute/traversal/symlink/reparse/
case-collision, missing, extra, tamper, source/config/runtime mismatch, run
conservation, transcript/hash, failure/exclusion, metric oracle hoặc claim failure.
Logical manifest không tự hash; BagIt payload manifest hash nó. PROV-like entity/
activity/agent và Datasheet/FAIR metadata được lưu nhưng không claim full external
PROV/FAIR certification. Bag validity không chứng minh scientific validity.

**Decision 13 — tiny/medium gate:** Tiny có tối đa 8 request/2 vehicle/16 node/256
arc, ba measured repeats cho B1/C1 trong hai clean harness processes, non-zero
accept/reject-or-defer/revision/delta coverage, exact raw-to-oracle equality và failure/
bundle mutations. Medium là deterministic CC BY derivative từ verified FleetPy
Manhattan, target 128 request/32 vehicle/max 96 node/9.120 non-self directed arc,
HMAC selection độc lập policy, two-clean normalization và ít nhất ba mechanical
repeats. Nếu source không thỏa mà không fabricate semantics thì stop/amend ADR, không
đổi lén. WP6 medium gate là normalizer/harness mechanics, không phải FleetPy Layer-2
effectiveness; WP7 vẫn sở hữu control adapter.

**Decision 14 — claim checker:** WP6 chỉ cho `mechanical|development` evidence và câu
“same-team clean-process repeatability”. Bundle bắt buộc caveat no effectiveness,
non-inferiority, production SLA, ACM badge, independent reproducibility/replicability,
novel ETA/reassignment/satisfaction claim. Claim profile machine-readable chặn các từ/
synonym tương ứng. Functional/Reusable-like properties là target nội bộ, không phải
ACM award. WP6 không chọn O-002/O-003/O-004 và không gọi public data là consented
human behavior.

**Alternatives rejected:** Một JSON/CSV aggregate duy nhất; seeded `System.Random`
global; fixed arm order; in-process policy call; simulator aggregate as truth; timeout
as zero/infeasible; drop failed/outlier seed; zip-only bundle; checksum-only verifier;
commit 408.9 MB raw archive; reuse WP5 paired/local curve as WP6 effectiveness; mở
FleetPy adapter trong refinement.

**Consequences:** WP6 có nhiều typed/integrity work trước khi có graph/table đẹp, nhưng
data loss, arm asymmetry, hidden RNG, metric circularity và claim inflation trở thành
testable failures. Contract v1 là equivalent contract cho `RB-WP6-001`; JSON schemas
và primitives được hiện thực ở ticket nhỏ nhất `RB-WP6-002`. Ordered queue
`RB-WP6-002..014` nằm trong `docs/tasks/34-wp6-common-benchmark-harness-ticket-plan.md`.
Chỉ `002` Ready; chưa có WP6 executable/result tại thời điểm ADR được accept.

**Supersedes / superseded by:** Thay ví dụ manifest có `TBD` trong `docs/10` bằng
contract v1 cho WP6 implementation. Không đổi WP1 protocol/hash, WP3 publication gate,
ADR-024 policy semantics, ADR-025 BeGo boundary, O-001 hoặc O-002/O-003/O-004.

### ADR-027 — 2026-08-09 — Accepted

**Context:** Khi `RB-WP6-004` chuẩn bị tạo scenario thật, executable contract `1.0.0`
cho scenario chứa `normalizationReportHash`, đồng thời normalization report chứa
`scenarioContentSha256` và `scenarioHash`. Fixture dùng các hash giả độc lập nên test
schema vẫn pass, nhưng artifact thật đòi hai canonical documents là cryptographic
fixed point của nhau. Không có thứ tự tính hoặc placeholder nào thỏa contract đó.

**Decision:** Sửa WP6 scenario/report schemas lên `1.0.1`. Bỏ
`normalizationReportHash` khỏi `ScenarioContent`; giữ scenario bind exact raw source,
member selection, normalizer source/version/config và validation summary. Tính
canonical scenario/plain SHA/domain-separated identity trước; sau đó report ghi hai
hash scenario cùng conservation/selection/exclusion evidence và mới nhận report hash.
Logical bundle bind scenario lẫn report. Dataset/plan/run/metric/bundle document shape
không đổi và tiếp tục `1.0.0`. Runtime validator dùng exact per-document version.

**Evidence required:** schema/runtime property parity, old cyclic field bị reject như
unknown, clean-process identity vectors được republish và regression trực tiếp tạo
scenario identity trước rồi bind nó vào report. Không được tái diễn giải field cũ,
dùng zero hash, lặp đến gần fixed point hoặc bỏ report khỏi bundle.

**Consequences:** provenance trở thành Merkle DAG một chiều có thể tạo và verify độc
lập. Đây là correction trước WP6 executable/result đầu tiên, không phải experiment
protocol change và không làm phát sinh effectiveness claim.

**Supersedes / superseded by:** Supersede riêng câu “report hash được bind vào
scenario” của ADR-026 Decision 2 và contract `1.0.0`; không đổi các decision còn lại,
WP1 protocol/hash, WP3 publication gate hay O-001/O-002/O-003/O-004.

### ADR-028 — 2026-08-09 — Accepted

**Context:** Khi hiện thực `RB-WP6-006`, supervisor bắt buộc phải giới hạn và ghi
typed terminal evidence cho process-tree count, stdin/stdout/stderr bytes và caller
cancellation. ADR-026 Decision 8/11 cùng ticket 006 đã yêu cầu các nhánh này, nhưng
failure schema/rule set `1.0.0` chỉ liệt kê wall/CPU/memory. Vì vậy một implementation
tuân ticket sẽ tạo record bị schema từ chối, còn ép các nhánh đó thành
`process.crash`/`protocol.invalid-output` sẽ làm sai nguyên nhân và audit denominator.

**Decision:** Nâng umbrella contract lên `1.0.2`, riêng `FailureRecord` lên `1.0.1`
và failure rule set thành `wp6-failure-v1.0.1`. Bổ sung đúng năm terminal codes:
`process.cancelled`, `resource.process-count-exceeded`,
`resource.stdin-bytes-exceeded`, `resource.stdout-bytes-exceeded` và
`resource.stderr-bytes-exceeded`. Tất cả có stage `execution`; partial raw evidence
vẫn phải giữ. Benchmark plan fixture/identity vector phải được republish vì rule-set
identity là một phần canonical plan. Scenario/report version và identity không đổi.

**Evidence required:** runtime/schema parity; mỗi code mới decode/validate; supervisor
fake-child mutation kích hoạt đúng code; plan/vector clean-process byte exact; full
solution pass. Không được dùng mã mới để biến local resource control thành SLA.

**Consequences:** failure accounting phản ánh đúng cơ chế đã predeclare thay vì có
nhánh implementation không thể serialize. Đây là contract correction trước WP6 run
record/bundle đầu tiên, không phải thay đổi outcome, exclusion hay claim boundary.

**Supersedes / superseded by:** Bổ sung phần liệt kê cụ thể cho ADR-026 Decision 8/11
và contract `1.0.1`; không đổi WP1 protocol, WP3 publication gate, policy semantics,
O-001 hay O-002/O-003/O-004.

### ADR-029 — 2026-08-09 — Accepted

**Context:** Khi hiện thực crash-safe `RB-WP6-007`, một planned intent có thể tồn tại
nhưng harness chết giữa copy/index/detail/log/atomic-directory boundaries. Failure
rule set `1.0.1` không có code cho nhánh này. Ghi `process.crash` sẽ sai nếu external
Runner đã exit 0; bỏ run sẽ vi phạm terminal conservation. Observation contract cũng
yêu cầu `certificateHash` nhưng chưa khóa projection byte cụ thể.

**Decision:** Nâng umbrella contract lên `1.0.3`, `FailureRecord` lên `1.0.2` và
failure rule set thành `wp6-failure-v1.0.2`. Thêm
`harness.persistence-incomplete` tại stage `persistence`; recovery/seal phải giữ
partial staging evidence rồi tạo một terminal failure mới, không overwrite và không
đổ lỗi cho Runner. `certificateHash` trong observation index là plain SHA-256 của
exact RideBound-canonical certificate-body JSON; envelope/decision identities vẫn
giữ domain hiện có và không bị thay thế bởi locator này.

**Evidence required:** crash injection tại mọi write boundary; retry/recovery cho ra
một terminal directory hoặc typed incomplete, log sequence/hash chain không gap;
failure không sinh zero metric; schema/runtime/vector/full-solution gates pass.

**Consequences:** plan identity vector phải republish vì failure rule-set ID đổi.
Đây là persistence/accounting correction trước bundle đầu tiên, không thay outcome,
policy, exclusion-before-outcome hay claim boundary.

**Implementation amendment 2026-08-11 (`RB-WP6-007`):** Plan/run publication dùng
private staging rồi atomic directory rename; duplicate semantic cell trong cùng plan
bị cấm. Raw identity được pin/copy bằng streaming length/SHA, locator phải đúng exact
run path và layout reparse point bị từ chối. Observation index được tái sinh từ raw;
success bind initialize manifest với planned scenario/config/Runner binary, exact
decision/ACK và checkpoint applied epoch/time. Runtime inventory được rederive từ
role/file/length/SHA và pre/postflight phải cùng launch command. Failure/exclusion dùng
chung gapless previous-hash log; seal và in-flight commit hội tụ theo per-run lock.
Authorized rerun publish plan + `supersedes.json` nguyên tử, giữ trọn semantic grid/
denominator và recursively verify prior terminal evidence. Các invariant này thực thi
ADR-029; không đổi public contract/version hoặc claim boundary.

**Supersedes / superseded by:** Bổ sung ADR-026 Decision 7/8 và ADR-028; không đổi
WP1 protocol, WP3 validator/certificate, O-001 hay O-002/O-003/O-004.

### ADR-030 — 2026-08-11 — Accepted

**Context:** `RB-WP6-008` là lần đầu contract metric v1 được hiện thực. Chỉ decode JSON
và cộng counter sẽ cho kết quả có vẻ hợp lệ nhưng vẫn có thể tính sai khi transcript
đảo thời gian, request vừa accepted vừa rejected, completion trước acceptance, hoặc
window trộn request arrival với action/completion time. Một oracle gọi lại production
model/calculator cũng chỉ lặp cùng bug. Paper McKeeman về differential testing yêu cầu
các implementation thật sự khác nhau; Dolan–Moré nhắc rằng benchmark comparison cần
giữ phân phối/tập bài toán thay vì che mọi thứ trong một aggregate.

**Decision:** Giữ metric registry v1 ở đúng 36 definition/hash đã publish và khóa:

1. `warmup=[warmupStart,scoreStart)`, `scoring=[scoreStart,horizonEnd]`,
   `drain=(horizonEnd,drainEnd]`, `all=[warmupStart,drainEnd]`;
2. request outcome/rate/defer dùng arrival cohort; decision/certificate/promise/breach/
   decision-delta dùng decision-envelope time;
3. raw parser phải kiểm monotonic time/epoch, unique arrival/completion/terminal action,
   accepted/rejected exclusivity, arrival→defer/terminal→completion chronology và
   exact resource terminal maxima trước khi emit row;
4. ratio denominator 0 là missing, không phải 0; vector sum/ratio dùng `BigInteger`
   intermediate rồi fail typed nếu vượt canonical safe integer;
5. oracle là executable/source tree BCL-only, không ProjectReference tới production
   contracts/calculator/models; hai phía tự canonicalize/parse/state/hash và phải khớp
   toàn bộ 132 canonical rows, semantic/resource evidence và metric-set hash;
6. bất kỳ byte/hash mismatch nào là `metric.oracle-mismatch` và chặn bundle. Failed/
   excluded run không được có success metrics;
7. WP6 chỉ giữ run-level/pairing/planned-denominator evidence cho aggregate sau này;
   performance profile, failure penalty, estimand và conclusion thuộc WP8/WP9
   preregistration, không được tự thêm ở WP6.

**Consequences:** Metric path không còn là vài nhánh đếm đơn giản mà là hai state
reconstruction độc lập với lifecycle/window/evidence invariants. Agreement vẫn không
chứng minh specification đúng hay independent reproduction; protocol/store verifier,
mutation matrix và source/hash provenance vẫn bắt buộc. Đây là lần khóa đầu của semantic
boundary trước metric bundle đầu tiên, không đổi field/schema/registry/hash vector nên
contract vẫn `1.0.3`; thay đổi tương lai phải bump metric version và ADR.

**Supersedes / superseded by:** Hiện thực và làm rõ ADR-026 Decision 9/10; không đổi
WP1 protocol, WP3 commitment semantics, O-001/O-002/O-003/O-004 hay claim boundary.

### ADR-031 — 2026-08-11 — Accepted

**Context:** `RB-WP6-009` là bundle đầu tiên. RFC 8493 checksum/completeness một mình
không phát hiện scenario đặt sai address, base commit che dirty source, run bị đổi
seed/grid, transcript sai ACK/checkpoint, hoặc hai file production/oracle cùng bị sửa.
Contract cũng có một `metricSetHash` ở bundle trong khi ADR-030 identity gốc là per-run;
nếu dùng plain file SHA hoặc chọn tùy ý một run sẽ làm field đúng cú pháp nhưng sai
ngữ nghĩa. External verifier ghi report vào bag sẽ tự phá seal hoặc overwrite evidence.

**Decision:** Khóa strict bundle implementation boundary sau:

1. giữ RFC 8493 BagIt 1.0 exact LF/UTF-8, payload/tag SHA-256, every-payload-once,
   no-self tag manifest và payload oxum; RideBound bổ sung no-extra cùng cấm absolute,
   traversal, percent/control, reparse/junction, case/Unicode collision, Windows device,
   trailing dot/space;
2. logical manifest liệt kê đúng union `data/` trừ chính nó; self-reference chỉ được
   giải bằng payload manifest. `verify.ps1` phải byte-exact reviewed template;
3. bundle `metricSetHash` dùng domain mới
   `RideBound.Wp6.BundleMetricSet.v1(planHash, registryHash, exact LF all-run rows)`;
   per-run `RideBound.Wp6.MetricSet.v1` không đổi;
4. source inventory phải capture Git HEAD + raw porcelain status hash/dirty flag và
   exact selected component path/length/SHA. Harness/oracle/verifier source hashes
   được rederive từ entries, không tin base commit hay self-reported digest;
5. provenance cross-bind scenario/dataset, plan, machine, immutable metric registry,
   Runner executable/assembly, Contracts, harness, oracle, verifier assemblies và
   runtime inventory. Fresh verifier phải tự hash assembly đang chạy;
6. export exact canonical run-store plan gồm denominator + full intent grid. Public
   plan materializer, intent, terminal directory và run record phải one-to-one; exact
   solver component seed/runtime/config được so trước transcript. Bundle verifier dùng
   chung portable WP6-007 run verifier, không viết lại protocol logic nông hơn;
7. terminal failure/exclusion logs phải là exact detail union với một global gapless
   sequence. Metric stage yêu cầu production=oracle byte-exact, exact registry/window/
   succeeded-run coverage, bundle metric identity **và** production recomputation từ
   raw run/scenario evidence để correlated edit không qua;
8. builder pin/copy/recheck input, dùng per-destination lock/private staging/atomic
   rename và không overwrite existing/stale bag. External report luôn là new sidecar
   ngoài sealed bag hoặc artifact của derived bag.

**Evidence required:** deterministic two-root bundle equality; valid all-success và
mixed success/failure/exclusion; clean-process verifier/hash/sidecar; mutations tại
mọi ordered stage gồm missing/extra/tamper/length/type/path/traversal/case/reparse,
script, dirty source/provenance, scenario, grid/seed, transcript, terminal log,
oracle-only và correlated production+oracle; Release/format/full-solution gates.

**Consequences:** BagIt validity và RideBound semantic validity trở thành hai gate rõ
ràng; cả hai vẫn không chứng minh algorithm effectiveness, unbiased benchmark hoặc
independent reproduction. Đây là lần khóa đầu của bundle semantics; không đổi public
field/schema/registry hoặc failure rule nên umbrella contract giữ `1.0.3`. Source
evidence và Browser audit nằm trong research doc; claim enforcement tiếp tục ở 010.

**Supersedes / superseded by:** Hiện thực/làm rõ ADR-026 Decision 12 và ADR-030 bundle
handoff; không đổi WP1 protocol, WP3 commitment validator/certificate, O-001,
O-002/O-003/O-004 hay claim profile wording.

### ADR-032 — 2026-08-11 — Accepted

**Context:** `RB-WP6-009` chứng minh một bag đầy đủ và semantic-valid, nhưng checksum,
exact replay và same-team clean process vẫn có thể bị README/report/provenance gắn nhãn
`effective`, `production-ready`, `Results Reproduced` hoặc ACM badge. Quét substring
toàn repository sẽ vừa leak/false-positive raw trip/source prose, vừa tự bắt các câu
phủ định bắt buộc. Regex lowercase đơn giản lại bị né bằng punctuation, full-width,
default-ignorable và Greek/Cyrillic confusable. ACM version 1.1 yêu cầu team khác cho
Results Reproduced/Replicated; NASEM tách same-data/code reproducibility khỏi new-data
replication; Peng 2011 nói reproducibility không bảo đảm correctness/validity; Munafò
et al. 2017 cảnh báo HARKing/analytical flexibility/over-interpretation; Unicode UTS
#39 cung cấp anti-confusable mechanisms nhưng không phải general prose classifier.

**Decision:** Khóa artifact claim boundary sau:

1. `wp6-mechanical-only-v1` là canonical machine-readable profile được compile trong
   verifier và emit thành `data/provenance/claim-profile.json`. Profile ghi ADR,
   normalization ID, bounded surface size, evidence URI, exact scan selectors, caveat
   và forbidden/synonym rules; caller/CLI không có profile switch;
2. builder reserve/tự sinh cả profile và `data/claim-check.json`; payload caller không
   thể đưa kết quả `passed` giả. Profile SHA-256 được cross-bind trong
   `reproducibility.json`; required-field addition bump evidence shape nội bộ từ
   `1.0.0` lên `1.0.1`, public benchmark-contract/umbrella vẫn `1.0.3`;
3. phạm vi scan chỉ gồm README; manifest/plan identity, evidence, rule/resource labels;
   packaging report labels; machine `fileSystemType`/`powerModeNote`/
   `containerImageDigest`; repository `gitDirty`. Cấm quét run transcript, scenario,
   public trip/dataset, failure/metric rows hoặc source-code prose;
4. sáu caveat exact-once khóa mechanical/development, same-team clean-process,
   non-confirmatory, no effectiveness/non-inferiority/SLA/ACM/independent claim,
   local resource controls và absence of observed public-trip preference/satisfaction.
   Exact caveat spans được mask trước forbidden scan;
5. text matching bounded dùng NFKC, invariant casefold, diacritic removal, common
   Greek/Cyrillic confusable mapping và hai skeleton: punctuation-as-separator và
   punctuation-removed. Non-whitespace control/format/default-ignorable, private-use,
   surrogate/unassigned code point fail closed; không claim full UTS #39 conformance;
6. mỗi failure mang stable code, rule/category, relative path, selector, bounded
   original excerpt và normalized witness. Stage 10 dùng typed decoded fields để tái
   tính valid report byte-exact; forged report/profile hoặc consistently resealed
   claim mutation vẫn invalidates bundle;
7. future profile extension bắt buộc ADR + external evidence + source/profile/hash
   change. Không có CLI flag hay README wording để nâng claim ladder.

**Evidence required:** allowed exact wording; direct typed witness; forbidden
effectiveness/non-inferiority/SLA/production/novelty/satisfaction/ACM/reproduced/
replicated; case/punctuation/synonym/full-width/confusable/default-ignorable; missing
or duplicate caveat; report/provenance/profile/forged-check mutations sau khi reseal;
scoped-selection proof; deterministic two-bundle/fresh verifier; format/Release/full
solution gates.

**Consequences:** Claim checker là fail-closed artifact guard chứ không phải general NLP
hay bằng chứng scientific truth. Finite synonym profile giảm bypass trong export
surface đã khóa nhưng không cho phép suy luận rằng mọi prose bên ngoài repository đã
được kiểm. WP6 chỉ đạt mechanical same-team boundary; tiny/medium/adversarial gates và
independent team/main experiment vẫn chưa hoàn tất.

**Supersedes / superseded by:** Hiện thực và siết ADR-026 Decision 14/ADR-031 stage 10;
không đổi WP1 protocol, WP3 hard commitment semantics, O-001, O-002/O-003/O-004 hoặc
novelty boundary.

### ADR-033 — 2026-08-12 — Accepted

**Context:** `RB-WP6-001..010` đã có từng thành phần harness nhưng chưa chứng minh chúng
ghép thành một measurement path trung thực. Tiny draft đầu chỉ tạo ETA revision do
traffic và reject một request quá capacity, nên `decisionDelta` bằng zero; compiler
còn giả định đúng bốn batch. Plan từng hash một policy file trong khi Runner đọc cả
WP3 commitment config lẫn WP4 algorithm config. Derived `solver-rng` chưa có launch
contract nói rõ Runner phải lấy manifest seed. Production/oracle rows bằng nhau cũng
chưa tự chứng minh oracle process/binary/raw input tương ứng nếu thiếu execution
summary. Cuối cùng supervisor từng gắn mọi conversation failure vào stage `protocol`,
không thuộc failure taxonomy đã khóa. Các lỗ này có thể cho test xanh nhưng không cho
phép gọi là paired end-to-end reproduction.

**Decision:** Nâng umbrella contract lên `1.0.4` và khóa tiny E2E như sau:

1. fixture nguồn phải tự chứa lifecycle có thể chạy, không phát sinh decision bằng
   code test. Compiler chấp nhận 1–32 event batch và derive batch indexes, horizon,
   fleet/request/snapshot/event counts từ canonical fixture. Gate hiện tại dùng sáu
   epoch: accept incumbent, traffic projection + tight feasible insertion, capacity
   reject, confirm/board/drop/alight cả hai accepted request;
2. non-zero treatment witness phải là `decisionDelta`, không lấy exogenous traffic
   delta thay thế. Epoch 2 hiện có `prePickupInsertedStopCount=1`; exogenous pickup/
   drop ETA `+50/+150 ms` vẫn ghi riêng. Harness fail nếu bất kỳ measured run nào
   thiếu accept, complete, revision, reject/defer hoặc non-zero decision delta;
3. WP4 arm dùng `ridebound-wp4-policy-binding-v1` trên exact SHA-256 của cả WP3 và WP4
   config. Effective configuration tiếp tục bind policy/solver/budget/capability/
   launch contract. Per-run `solver-rng` int32 trở thành initialize master seed và
   Runner chỉ tiêu thụ qua pinned opt-in `--solver-seed-source manifest-master-seed`;
4. một run là một fresh exact external Runner process và isolated writable root.
   B1/C1 × ba measured repeat phải thành công trong mỗi hai clean harness process.
   So sánh exact plan/scenario/source/runtime/grid/transcript/decision/semantic metric
   và từng run input/output/index/decision/semantic metric; không đòi physical bundle
   hash giống nhau khi monotonic/resource evidence thật khác nhau;
5. independent oracle process emit canonical per-run summary bind oracle assembly,
   raw resource evidence, semantic evidence, row count và per-run metric-set hash.
   Strict bundle chỉ cho exact optional union một summary/successful run và stage 9
   tái tính toàn bộ; corrupted, missing trong partial union hoặc extra summary fail;
6. conversation failure map đúng taxonomy: negotiation/decision/parsing/completion/
   validation. Timeout/tree/resource/crash/postflight/incomplete/solver unknown cùng
   store/transcript/metric/bundle/selective-rerun mutations phải giữ typed terminal
   evidence, không đổi thành zero, drop hoặc selective success;
7. source audit tách model-mapping diagnostic: empty candidate set là “không có
   candidate khả thi”, duplicate ID là global identity collision. Hai invariant có
   regression riêng để benchmark diagnosis không che infeasibility.

Không đổi public JSON field hay bỏ failure code hiện hữu; `1.0.4` là backward-
compatible semantic/verification correction. Existing bundle không có oracle summary
vẫn được đọc; bundle đã cung cấp bất kỳ summary nào phải cung cấp exact complete union.

**Evidence required:** exact six-run conservation; non-zero decision witness trong raw
Runner output; two-clean-process semantic comparison; independent verifier sidecar;
claim report pass; oracle-summary tamper; timeout/crash/unknown/incomplete/input/
postflight/metric/missing/extra/tamper/selective-rerun mutations; format, Release
`-warnaserror` và required full solution.

**Consequences:** Bundle tại
`artifacts/wp6/tiny-paired-20260812-release/` là mechanical/development evidence của
same-team clean-process repeatability. Nó chứng minh harness chạy đúng và B1/C1 tạo
output khác theo semantics đã bind; nó không chứng minh C1 tốt hơn B1, không phải
FleetPy closed-loop effectiveness, independent reproduction, production SLA hay
confirmatory result. Full metric/bundle hash có resource samples nên không phải
semantic determinism key; semantic subset và provenance mới là cross-process gate.

**Supersedes / superseded by:** Làm rõ/hiện thực ADR-026 Decisions 5–14, ADR-028 launch
boundary, ADR-029 terminal conservation, ADR-030 metric oracle, ADR-031 strict bundle
và ADR-032 claim boundary. Không đổi WP1 protocol ordering, WP3 commitment validator/
certificate, O-001 hoặc tự chọn O-002/O-003/O-004.

### ADR-034 — 2026-08-12 — Accepted

**Context:** Medium derivative 128 request đã reproducible nhưng chưa có một exact
Runner conversation và paired bundle path. Draft đầu tái dùng WP3 boundary-test config
chỉ khai báo `uniform-v1`, trong khi public scenario ghi synthetic policy
`wp6-synthetic-policy-overlay-v1`; request vật lý khả thi vì thế trả no-op/defer
`FLEET_SELECTION_CONFLICT`. OR-Tools diagnostic 32 vehicle/34 objective level vẫn đạt
optimal trong budget, nên tăng budget hoặc nới validator sẽ che sai policy identity.
Một six-run attempt khác hoàn thành execution/oracle nhưng bundle preflight phát hiện
source entity IDs chưa sorted. Ngoài ra medium run chứa real resource samples nên yêu
cầu physical bundle hash giống nhau giữa process là sai determinism domain.

**Decision:** Nâng umbrella contract lên `1.0.5` và khóa:

1. medium compiler bind exact verified descriptor/scenario/report/manifest chain và
   exact 128 request, 32 vehicle, 96 node, 9.120 directed arc; 21.400 input phải bảo
   toàn thành eligible/excluded/selected, không raw-row loss hoặc fabricated arc;
2. Runner dùng dedicated commitment config khai báo đúng synthetic policy ID cùng WP4
   config qua existing composite binding. Policy mismatch fail closed; không đổi data,
   solver result hay validator để biến nó thành success;
3. driver `wp6-public-derivative-instant-drain-driver-v1` kiểm exact capability/init,
   decision context/hash/ACK/checkpoint, request/vehicle/candidate/plan/suffix binding
   và lifecycle. Historical frozen prefix chỉ hợp lệ khi executed count exact và không
   chứa request mới; drain decision không được emit allocation action;
4. instant drain ở cùng source timestamp chỉ là nonphysical state-machine mechanics.
   Zero wait/ride không được aggregate/rank/report thành effectiveness, service,
   fairness, satisfaction, non-inferiority hay SLA; WP7 sở hữu simulator semantics;
5. source claims và artifact pins phải unique/sorted ordinal ở early preflight trước
   expensive run. Attempt fail preflight không có receipt và không được gọi pass;
6. B1/C1 × ba measured repeat chạy trong mỗi hai fresh Release harness process. Exact
   semantic domains gồm plan/scenario/source/runtime/grid/transcript/decision/semantic
   metric và từng run input/output/index/decision/semantic rows. Monotonic resource
   samples, full metric/logical/physical bundle hashes được phép khác nhưng phải complete,
   provenance-bound và được external verifier xác nhận;
7. exact-Runner medium test giữ CPU regression ceiling 120 giây. Wall ceiling 180 giây
   cho full-solution scheduler contention; một 120-second wall failure phải ghi là test
   resource timeout, không được báo thành WAC hay algorithm pass.

**Evidence required:** publisher length/MD5 + local SHA/license; two-clean-root exact
normalization; request/exclusion conservation; exact Runner lifecycle; 6/6 × hai fresh
process; per-run semantic comparison; oracle/strict external verifier; rejected
unsorted provenance attempt; mechanical-only claim report; format và required full
solution.

**Evidence:**
[wp6-012-public-medium-evidence-2026-08-12.md](benchmarking/wp6-012-public-medium-evidence-2026-08-12.md)
ghi commands, identities, bundles B/C, fail-closed attempt A và claim caveat. Required
`dotnet test RideBound.slnx` cuối đạt 710/710; Contracts/Runner nạp/chạy và WAC
`0x800711C7` không tái hiện.

**Consequences:** WP6 có medium public-source harness evidence nhưng chưa có FleetPy
closed-loop control, vehicle motion hoặc physical KPI. B1/C1 cùng accept/complete mọi
request dưới instant-drain không chứng minh hai arm tương đương hay C1 tốt hơn. Mọi
effectiveness experiment vẫn phải chờ WP7 adapter và WP8 preregistration.

**Supersedes / superseded by:** Làm rõ ADR-026 medium gate và mở rộng ADR-033 generic
paired harness semantics; không đổi WP1 protocol, WP3 publication validator, WP4 solver
objective, O-001 hoặc O-002/O-003/O-004.

### ADR-035 — 2026-08-12 — Accepted

**Context:** Source/adversarial audit của `RB-WP6-013` phát hiện năm khoảng trống mà
bundle 012 vẫn có thể che: plan generic khai báo không warm-up; preflight message nói
claims/pins/sources sorted nhưng chỉ claims được kiểm; medium policy mismatch còn được
phát hiện sau expensive Runner work; conversation driver có thể phát code ngoài failure
taxonomy; terminal conservation hard-code sáu thay vì lấy compiled grid. Khi warm-up
được bật, literal sáu lập tức làm tiny harness fail, chứng minh đây là lỗi logic chứ
không phải bổ sung test trang trí. Resource evidence D/E cũng xác nhận semantic hash
phải ổn định trong khi full sampled-resource hash phải được phép khác.

**Decision:** Nâng umbrella contract lên `1.0.6` và khóa:

1. paired mechanical gate materialize một warm-up và ba measured repeat mỗi B1/C1;
   từng run có process/root/output riêng, repeat index collision-free, không cache/state
   donation. Conservation dùng exact `compiled.PlannedRuns.Count`;
2. claims, absolute pins và bundle sources phải non-empty, ordinal-sorted và
   case-insensitively unique; relative destination/media type cũng fail-closed tại
   preflight trước Runner execution;
3. public scenario phải derive đúng một commitment policy ID và exact Runner-visible
   config phải khai báo policy đó. Empty/multiple/mismatch fail trước run đầu tiên;
4. conversation chỉ được phát năm code do boundary sở hữu ở negotiation/decision/
   parsing/completion/validation. Code khác canonicalize thành
   `protocol.invalid-output/parsing`, không persist arbitrary taxonomy/stage;
5. required-mutant gate gồm 10 document nested permutation + parallel decode, plan/
   seed/order parallelism, đủ 21 failure/stage và 8 pre-outcome exclusion rules,
   actual supervisor/resource branches, store write/race/log boundaries, metric/
   bundle/claim mutations và exact source nondeterminism audit. `100% killed` chỉ nói
   về declared matrix này, không phải general mutation score;
6. cross-process equality dùng scenario/plan/source/runtime/grid/transcript/decision/
   semantic metric và per-run semantic hashes. UTC/monotonic resource, full metric,
   logical/physical bundle containing those samples được phép khác nhưng phải complete,
   provenance-bound và externally verified;
7. giữ mọi raw resource stratum, kể cả treatment chậm và resource-control failure.
   Không dùng local instant-drain rows làm effectiveness, non-inferiority, production
   latency/throughput hoặc SLA evidence.

Không public schema field/failure code nào bị thêm/bỏ; đây là backward-compatible
semantic/verification correction. WP3 validator/certificate và WP4 objective không
bị nới, public data không đổi, O-001/O-002/O-003/O-004 vẫn khóa.

**Evidence:** Hai fresh medium D/E đều 8/8 success và strict external verify valid;
13/13 top-level semantic fields cùng 8/8 per-run semantic records exact, trong khi
8/8 full resource row hashes khác đúng contract. C1 wall/CPU lớn hơn B1 trong 6/6
local measured pair được giữ như diagnostic âm. Declared mutation matrix pass; Release
0 warning/error, format/dependency/schema/link gates sạch và required exact full
solution cuối 770/770. Một run trước 769/770 do medium CPU control được ghi riêng,
standalone rồi exact rerun pass; WAC không tái hiện. Chi tiết tại
[wp6-013-adversarial-closure-evidence-2026-08-12.md](benchmarking/wp6-013-adversarial-closure-evidence-2026-08-12.md).

**Consequences:** `RB-WP6-013` Done và `RB-WP6-014` là ticket duy nhất In progress.
WP6 chưa đóng cho đến khi source/claim audit toàn WP1–WP6, toàn bộ Markdown, E2E/
artifact verifier và final Vietnamese review folder có evidence đầy đủ.

**Supersedes / superseded by:** Làm rõ ADR-026 determinism/resource/failure boundary,
ADR-033 generic paired harness và ADR-034 public medium gate; không supersede WP1
protocol, WP3 publication boundary, WP4 algorithm semantics hoặc claim profile.

### ADR-036 — 2026-08-13 — Accepted

**Context:** `RB-WP6-014` phải quyết định WP6 có thể đóng dựa trên source/logic và
fresh artifact evidence hay chỉ đang xanh do test. Audit cũng phải phân biệt source
provenance change với nondeterminism: medium D/E lịch sử không có cùng source inventory
với working tree closure hiện hành.

**Decision:** Đóng WP6 với các điều kiện:

1. review source theo chuỗi authority thực, không dùng test count làm kết luận;
2. WP3 full-fleet physical/commitment validator và ACK-only commit giữ nguyên; WP4
   heuristic/solver/filter không được tự cấp publication certificate;
3. WP5 chỉ orchestration/persistence và phải gọi pinned Runner; WP6 chỉ external
   supervision/measurement, không reference core decision path;
4. fresh repeat pair phải dùng cùng exact source inventory. So khác source inventory
   là provenance difference, không phải deterministic failure;
5. semantic equality bao gồm top-level và từng run input/output/index/decision/metric;
   sampled resource/full/bundle hash được phép khác nhưng phải complete/external-verify;
6. paper speed-up/sparse/multiple-plan result chỉ vào code sau claim-boundary,
   deterministic loss/oracle và hard revalidation evidence; closure không tự thêm;
7. final handoff phải giải thích logic/code/file/paper/risk/reproduction bằng tiếng
   Việt và giữ mọi negative result/caveat;
8. WP7 giữ Not Started; WP6 không cấp effectiveness, SLA, non-inferiority, fairness
   hay satisfaction claim.

**Evidence:** Fresh tiny A 8/8, bundle
`79cb321a2aa079c34ddfa49061387e78990f14b7bb368abb762e497c30b27b04` valid.
Medium H/I trên exact source cuối đều 8/8; 16 top-level field + 72 per-run semantic
field có 0 mismatch, 8/8 full resource row khác hợp lệ; bundles
`89a43921e46f57cfc47d9fcb0d63f8f18f58087a1f54d29fe65c7fecc4d6d9d8` và
`a954db621758a6404fba988a491f9f4575add45a771f0b852ce7ab7cd95494e9`
đều fresh-process verify. Exact full solution 770/770; Contracts/Runner load và WAC
không tái hiện. Evidence chi tiết ở
[wp6-014-source-claim-closure-evidence-2026-08-13.md](benchmarking/wp6-014-source-claim-closure-evidence-2026-08-13.md)
và [review WP1–WP6](reviews/wp1-wp6-final/README.md).

**Consequences:** `RB-WP6-001..014` Done, WP6 Complete. Không có ticket active;
WP7 chỉ được mở bằng refinement explicit. D/E, F/G và mọi artifact lỗi/âm vẫn được giữ.

**Supersedes / superseded by:** Đóng exit gate ADR-026 và các amendment ADR-027..035;
không thay protocol WP1, state/physical WP2, publication WP3, algorithm WP4, durable
boundary WP5 hoặc các open decisions O-002/O-003/O-004/O-006/O-008.

### ADR-037 — 2026-08-13 — Accepted

**Context:** Refinement WP7 phải giải quyết hai rủi ro trước khi viết adapter. Thứ
nhất, Candidate cap cũ xếp từng route variant riêng lẻ, nên nhiều variant cùng exact
service set có thể chiếm cap và làm mất một service set hữu ích ở fleet selection;
C1 còn có thể mất variant ít phá incumbent trước hard gate. Thứ hai, FleetPy adapter
không được suy đoán callback, edge progress hoặc vượt locked leg chỉ vì unit test giả
pass. Nghiên cứu lại Alonso-Mora, Engelhardt et al. và Zalesak et al. cho thấy các
RV/subset/random-direction filter có speed/quality trade-off hoặc đòi giả định không
được bảo đảm bởi arbitrary directed sparse RideBound snapshot.

**Decision:**

1. thêm config-bound `CandidateRetentionStrategy`; old WP1–WP6 config thiếu field
   phải parse thành `LegacyAcceptedCountCostSlack`, giữ behavior và content hash cũ;
2. strategy opt-in `ServiceSetStabilityPortfolioV1` chạy riêng từng accepted-count
   tier: giữ cheapest anchor cho mọi exact service set, rồi stability anchor theo
   unchanged incumbent prefix, inserted-before-incumbent-pickup và integer service-
   start shifts, cuối cùng fill bằng legacy rank;
3. no-op luôn được giữ; retained/omitted count và digest vẫn exact. Candidate filter
   không cấp publication authority: WP3 full-fleet physical/commitment validator vẫn
   revalidate trước decision;
4. chỉ nhận strategy mới sau bounded evidence: per-set B1 cost dominance tại mọi cap,
   fleet strict-positive, C1 real-validator/no-regression và permutation/conservation;
   đây không phải chứng minh tối ưu phổ quát hay effectiveness;
5. reject random request/vehicle order, random direction, forecast/reassignment hoặc
   paper-only pruning khỏi default; O-001 và O-002/O-003/O-004 không mở;
6. pin FleetPy tag `1.0.2`, annotated tag object
   `ca5a245243094236c84a0e93b32819ee502beeff`, commit
   `053aa9d4fcfde91c5d303435d5748f9206c071b0`, MIT và source/env hashes ngoài vendor;
7. executable probe phải fail trước adapter import khi commit/tag/dirty/source/env
   drift; probe actual abstract callbacks, position round-trip,
   `SimulationVehicle._move` mutation và non-forced assignment default;
8. khóa directed position model `(startNode,endNode,relativeProgress)` với node form
   `(node,None,None)` và `relativeProgress ∈ [0,1]`. Không có capability thì fail/
   named downgrade; không suy diễn từ clock;
9. FleetPy suffix mapping phải giữ exact active locked leg, không bao giờ gọi
   `force_assign=True`, và chỉ gọi cùng pinned external RideBound Runner; Python không
   port/reimplement Candidate, validator hoặc policy.

**Evidence:** Candidate tests chứng minh per-service-set anchor dominance, exact fleet
adversarial cùng cap tăng accepted `2 → 4`, stability anchor sống qua hard gate và
32 exact-small seeds qua real assessor/production policy không có substantive C1
regression, có strict-positive witness; permutation/loss/config compatibility đều
pass. Algorithm 141/141, Runner 73/73 và exact full solution 776/776. FleetPy probe
trên external clean checkout kiểm đúng annotated tag/commit và six critical source
hashes, Python 3.10.20/package lock, 13 abstract callbacks, position
`(11,12,0.375)` round-trip, `_move` đổi thành `(11,12,0.625)`, và
`force_assign=False` được truyền vào `force_ignore_lock`; bốn drift mutation test
fail đúng typed code.

**Consequences:** `RB-WP7-001..003` Done; `RB-WP7-004` là ticket duy nhất Ready.
Strategy mới là opt-in cho config WP7/future experiment; published WP4/WP6 config
không bị đổi. WP7 vẫn chỉ mechanical Layer-2 cho đến actual preflight/tiny/medium
closed loop; chưa có effectiveness, SLA, non-inferiority, fairness hay novelty claim.

**Supersedes / superseded by:** Đóng O-006 và mở ordered WP7 queue sau ADR-036;
không supersede WP1 protocol, WP2 physical state, WP3 publication gate, WP4 objective,
WP5 durable boundary, WP6 measurement contract hoặc open decisions còn lại.

### ADR-038 — 2026-08-16 — Accepted

**Context:** Sau ADR-037, WP7 phải đóng bằng current-source evidence thay vì chỉ unit
test hoặc một preflight giả. Audit Candidate tìm thấy một lỗi ranking thật: B4 repair
root có thể bị xếp theo slack của route chưa repair dưới work cap. Adapter FleetPy cũng
phải chứng minh actual callbacks, directed position, locked plan và process restart qua
cùng Runner binary ở B1/C1.

**Decision:**

1. Sửa B4 root priority để mọi repair seed xếp theo mutable suffix của chính route đã
   repair; regression ghi việc projection này xảy ra trước khi one-work budget chọn root.
2. Giữ portfolio là opt-in. B1 proof chỉ là substitution cùng vehicle/exact service set:
   cost anchor không đắt hơn và có cùng conflict columns; nó không bảo toàn CandidateId
   tie-break, không là global optimum và không là định lý C1 phổ quát.
3. Khi opt-in cap thực sự áp dụng, một candidate non-no-op phải giữ nguyên mọi no-op
   incumbent stop và tập stop request mới phải đúng `NewRequestIds`. Nhờ vậy label
   service set không thể che route khác nghĩa.
4. Python adapter chỉ map FleetPy và gọi external published Runner v6; không có core
   algorithm Python, không `force_assign=True`, không invented reverse/zero arc và
   locked/current leg mismatch fail typed.
5. Actual evidence bắt buộc dùng FleetPy 1.0.2 clean pin + CPython lock, Runner preflight,
   FleetControl preflight/lifecycle, two-repeat tiny clock và three-repeat public-medium
   B1/C1. Medium bundle phải qua verifier độc lập từ transcript/manifest.
6. Giữ caveat upstream FleetPy future-ABC warning và các raw resource/negative records.
   Không suppress warning, nâng CPU limit, hoặc suy diễn effectiveness/performance từ
   publication count, wall time hay semantic hash khác arm.

**Evidence:** Published Runner v6 SHA-256
`8a227fcd44e2c8e9814821bce317ea07f59c6fe9766dd26b6b8533a8129b75a2`; external
evidence root `E:\\RideBoundData\\wp7\\results\\candidate-portfolio-v6-20260815`;
hard-vector medium manifest `e8f03b56137d9ca54ebeef802cb5c3da0e3cab600c73c08ce42a4c13ae41274e`
and rolling-cost manifest `829eb76645a4c751af5a3bf25f298ed9608ac320351a1713a054b43c9838689f`.
Full .NET suite passes `790/790`; pinned Python adapter suite passes `49/49` without
skip; WAC did not recur. Exact receipts and claim boundary are in
`benchmarking/wp7-014-fleetpy-layer2-closure-evidence-2026-08-15.md`.

**Consequences:** `RB-WP7-001..014` Done and WP7 Complete for mechanical Layer-2
scope. WP8 is not started and needs an explicit refinement/preregistration decision.
No effectiveness, SLA, non-inferiority, fairness, satisfaction, novelty, independent
reproduction or global-optimality claim is authorized.

**Supersedes / superseded by:** Supersedes the open queue state of ADR-037; retains its
pin, claim boundary and all earlier protocol/state/commitment/solver decisions.

### ADR-039 — 2026-08-17 — Accepted

**Context:** Một audit source sau ADR-038 tìm thấy hai loại nợ. Thứ nhất, một nhóm
thay đổi ngữ nghĩa thật đã vào source mà **không có ADR nào khóa**: thời điểm mở
promise (`initialPromiseTrigger`), baseline của lock evaluator, cách reducer xử lý
`OfferDeclined` sau khi đã accept, một failure fail-closed mới ở C1, hai CLI flag mới
của Runner và việc phát plan update do event gây ra. Thứ hai, cổng benchmark medium
từng chạm trần CPU và nguyên nhân chưa từng được **đo**, chỉ được suy đoán. Ngoài ra
mọi receipt tài liệu vẫn ghi `790/790` trong khi source đã đi tiếp.

**Decision:**

1. Khóa `initialPromiseTrigger` là một thuộc tính config có hai giá trị hợp lệ.
   `initial-acceptance` giữ nguyên ngữ nghĩa WP1–WP6: promise mở khi request được
   accept. `booking-confirmation` mô hình hóa Layer-2 thật, nơi assignment chỉ là một
   **offer provisional**: nó được kiểm tra vật lý đầy đủ nhưng **không** tạo promise,
   và promise chỉ mở tại chuyển trạng thái `Accepted → WaitingPickup/Onboard` với
   reason code `INITIAL_BOOKING_CONFIRMATION`. Config thiếu field phải parse thành
   `initial-acceptance`, giữ nguyên content hash cũ.
2. `CommitmentLockEvaluator` so candidate với **exogenous projection**, còn published
   promise trước đó chỉ xác định horizon đang bị khóa. Traffic drift vì thế được ghi
   nhận nhưng không bị báo sai thành vi phạm lock. Đây là áp dụng đúng three-way delta
   đã khóa ở ADR-021, không phải nới lỏng lock.
3. `OfferDeclined` trên một request đang `Accepted` là `CancelAfterAcceptance`, không
   phải `RejectRequest`; từ chối một offer đã được cấp không được ghi thành chưa từng
   được phục vụ.
4. `C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE` là fail-closed bắt buộc: nếu treatment loại
   hết mọi candidate của một vehicle — kể cả no-op an toàn — run phải dừng với witness
   **typed**, không được giao cho solver một vehicle không có lựa chọn nào. Witness
   mang `requestId`, `dimension`, `underlyingCode`, `before`, `after` và số lượng
   generated/rejected ở trường riêng, không nhồi vào prose.
5. Runner nhận `--maximum-line-bytes` (bounded `1 MiB..64 MiB`) và
   `--manifest-solver-seed`; plan update do event gây ra chỉ được phát khi caller bật
   tường minh và route thực sự khác route trước đó.
6. Tối ưu hiệu năng chỉ được chấp nhận khi **không đổi một byte kết quả nào** và được
   chứng minh bằng counter, không bằng đồng hồ. Khóa memo slack chuyển sang so sánh
   cấu trúc chính xác thay cho fingerprint SHA-256; framing identity ghi thẳng UTF-8;
   `RoutePlan.Create` phát hiện stop trùng bằng đếm hai lượt và báo đúng key cũ;
   StableId của search node và route đã chiếu được tính lười; generator rank một lần
   rồi đưa thứ tự cho retainer kèm kiểm tra fail-closed. `CandidateSearchWorkProfileTests`
   khóa chính xác work unit, evaluated path, feasible-before-cap, omitted path, retained
   count và số profile slack riêng biệt.
7. Semantic hash của actual FleetPy **không so sánh được giữa hai Runner artifact**, vì
   `RunManifestIdentity` cố ý bind `binarySha256`. Differential giữa hai binary phải so
   các trường hành vi, không so hash tổng hợp.

**Evidence:** Micro-harness xác định hot path không phải validator (`0,72 µs/route`)
mà là khóa memo slack (`19,63 µs/lookup`, ~39.000 lookup mỗi `Generate`). Sau thay đổi:
`0,64 µs/lookup`. Wall time mỗi `Generate` (min của ba lần chạy) giảm
`25,3 → 16,6 ms` (suffix 4), `220 → 170 ms` (suffix 8), `897 → 587 ms` (suffix 12),
`1662 → 1018 ms` (suffix 16); mọi counter giữ nguyên tuyệt đối. Benchmark medium
mechanical hoàn tất trong `1 m 50 s` dưới trần CPU 120 giây.
Required `dotnet test RideBound.slnx` pass **798/798**, 0 failed, 0 skipped:
Contracts 135, Domain 136, Application 73, Algorithms 154, Runner 77, Benchmarking 135,
Benchmarking.Contracts 71, OrTools 7, Architecture 10. `dotnet format --verify-no-changes`
sạch; Release `-warnaserror` 0 warning/0 error. Pinned Python adapter suite pass
**50/50** không skip.
Runner v8 SHA-256 `13bf5d9b1dfbcb677d2d64c24038dba2c9adc22e664d2a6adecbf1905dcc179e`
tại `E:\\RideBoundData\\wp7\\runner\\candidate-portfolio-v8-identity-hotpath`; evidence root
`E:\\RideBoundData\\wp7\\results\\candidate-portfolio-v8-identity-hotpath-20260817`.
Differential v7↔v8 trên tiny clock với label giống hệt: publication, `requestState`,
`travelSnapshotVersion`, `vehiclePosition`, `nextEpoch`, `nextEventSeq` và
`exactPhysicalBoundaryDrainCount` **giống hệt**; chỉ `manifestHash`/`checkpointBindingHash`
lệch, đúng bằng thiết kế bind binary.

**Consequences:** Mọi receipt `790/790` trong tài liệu được thay bằng `798/798` và mọi
receipt actual được gắn với Runner v8. WP7 vẫn Complete cho phạm vi mechanical Layer-2.
Không có effectiveness, SLA, non-inferiority, fairness, satisfaction, novelty hay
global-optimality claim nào được cấp. Đề xuất *lazy priority* bị **bác bỏ** và ghi lại
như kết quả âm. Constant-time feasibility test của Gschwind–Drexl 2019 được ghi nhận là
hướng WP8 nhưng **chưa áp dụng**, vì full text chưa đọc được tại ngày kiểm tra.

**Supersedes / superseded by:** Bổ sung, không thay thế, ADR-037/038. Không đụng tới
WP1 protocol, WP2 physical state, WP3 publication gate, WP4 objective, WP5 durable
boundary hay WP6 measurement contract.

### ADR-040 — 2026-08-18 — Accepted

**Context:** Hết WP7, RideBound có một đường so sánh cơ học đã công bằng — hai arm dùng
chung scenario, seed, Runner binary, work budget, candidate pool và đường publication,
và hai config chỉ khác đúng `policyId`. Cái chưa có là một **thí nghiệm**: chỉ một hiện
thực nhu cầu, chưa preregister, và cơ chế đắt nhất của treatment chưa thực sự bị ràng
buộc. WP8 phải quyết thí nghiệm trông thế nào rồi khoá lại, trước khi nhìn bất kỳ con
số nào.

**Decision:**

1. Grid scenario dựng **hoàn toàn từ dữ liệu công khai thật**. Bộ FleetPy Manhattan v1
   đã xác minh có 289 file demand trên 8 ngày liên tiếp, kèm sample fraction chính thức,
   tỷ lệ đặt trước và hệ số traffic theo ngày/giờ. Không sinh demand tự chế.
   `tools/RideBound.Wp6Normalize` chuyển từ hai profile hard-code sang chạy theo một
   grid manifest source-controlled; `FleetPyNormalizationConfiguration` vốn đã nhận đủ
   tham số nên đây là mở rộng CLI, không phải viết lại normalizer.
2. **Pilot** là `2018-11-11` và `2018-11-12`; **confirmatory holdout** là `2018-11-13`
   → `2018-11-18` và bị niêm phong cho tới khi preregistration đóng băng. `2018-11-12`
   đúng là ngày WP6/WP7 đã dùng, nên toàn bộ phần dữ liệu đã bị nhìn nằm gọn trong
   pilot; điều này phải được ghi trong preregistration.
3. Đơn vị thí nghiệm là `(scenario, seed, travel realization)`. Rider được gộp lên mức
   run trước khi bootstrap; cấm coi rider trong cùng run là mẫu độc lập.
4. Primary endpoint là `p95_decision_pickup_eta_total_variation_ms`, so paired
   difference `C1 − B1`. Calculator production phải khớp byte-exact với một oracle
   BCL-only không ProjectReference, theo đúng chuẩn đã dùng ở `RB-WP6-008`.
5. Commitment budget được **suy từ phân phối thực nghiệm trên chỉ dữ liệu pilot**, khoá
   thành ba mức strictness, giữ cấu hình unbounded hiện tại làm tầng đối chứng. Quy tắc
   suy dẫn phải khai báo trước khi đọc dữ liệu. Lý do: ở cấu hình hiện tại chỉ 3/10
   chiều có `hardLimit` và cả ba bằng 0, lại luôn thoả sẵn do O-001, nên phần hard
   vector của C1 gần như không ràng buộc gì và treatment thực chất chỉ đang được kiểm ở
   phần thứ tự revision.
6. Non-inferiority margin phải đặt trước, dùng cận một phía, và neo vào chính sách suy
   biến "từ chối hết yêu cầu" — chính sách đó có revision `= 0` và service rate `= 0`,
   nên margin phải đủ hẹp để treatment không thể thắng endpoint chính bằng cách hạ dịch
   vụ. Ghi rõ rằng có margin **không** tự bảo vệ khỏi degradation.
7. Sample size suy từ phương sai pilot và một minimum detectable effect có nghĩa vận
   hành, cộng dự phòng failed run. Cấm chọn số tròn.
8. Partition `planned = succeeded + failed + excluded` giữ nguyên; một primary endpoint,
   một non-inferiority gate, Holm cho key secondary family; exploratory phải ghi rõ.
9. Preregistration file đủ 15 mục của `docs/11` §11, được canonical hash và đóng băng;
   sau freeze chỉ một ADR mới sửa được.
10. WP8 **không** công bố bất kỳ kết quả effectiveness nào. Nó chỉ tạo ra một thí nghiệm
    đã khoá.

**Evidence:** Kiểm kê dataset đã xác minh (289 file demand, 8 ngày `2018-11-11` →
`2018-11-18`, sample/res fractions, `tt_factors` theo ngày và giờ) và xác nhận
`FleetPyNormalizationConfiguration` đã tham số hoá đủ day/window/selection-key/fleet/
window-tightness. Phương pháp non-inferiority đọc full text từ *Non-inferiority
statistics and equivalence studies* (PMC7808096) và *Choice of NI margins does not
protect against degradation* (PMC4117500); guideline EMA CHMP được ghi nhận là nguồn
quy chuẩn nhưng PDF không đọc được dạng text tại ngày kiểm tra nên không trích nội dung.
Nền cơ học kế thừa từ ADR-039: required suite `798/798`, Python `50/50`, và actual
FleetPy B1/C1 trên cây đóng băng `d48b115b` với verifier độc lập pass cho cả hai bundle.

**Consequences:** Tạo `docs/tasks/37` và `docs/tasks/38`; `RB-WP8-002` là ticket Ready
duy nhất. Chưa có production code WP8, chưa có scenario confirmatory nào được sinh, và
không claim nào được nâng. `O-001`/`O-002`/`O-003`/`O-004` vẫn đóng.

**Supersedes / superseded by:** Không supersede ADR nào. Nó mở WP8 sau ADR-039 và kế
thừa nguyên vẹn claim boundary của WP6/WP7.

### ADR-041 — 2026-08-19 — Accepted

**Context:** Pilot WP8 chạy thật trên dữ liệu công khai đã xác minh và buộc phải sửa ba
điểm trong thiết kế mà ADR-040 khoá. Cả ba đều phát hiện từ cơ chế hoặc từ chính pilot,
trước khi bất kỳ ngày confirmatory nào được sinh.

**Decision:**

1. **Điểm vận hành phải có tranh chấp.** Ở điểm WP6/WP7 (128 request rải 24 giờ, 32 xe),
   decision-induced delta bằng 0 cho mọi rider ở cả hai arm; exogenous bằng đúng visible.
   Primary endpoint khi đó bằng 0 đồng nhất và không cỡ mẫu nào cứu được. Điểm vận hành
   preregister là cửa sổ cao điểm thật `08:00–10:00` với 8 xe; chỉ cửa sổ và số xe thay
   đổi, dùng cấu trúc sẵn có của dữ liệu chứ không bịa tải.
2. **Primary endpoint là tổng decision-induced burden liên chiều**, không phải riêng
   pickup ETA. Pickup-ETA zero-inflated tới mức `p50 = p90 = 0` ở mọi run, nên `p95` treo
   trên 5–6 rider trong khoảng 110. Ghi rõ: đây **không** phải hằng đẳng thức cơ chế —
   `C-d20181112-r2-c1` có `prePickupInsertedStopCount = 3` mà pickup delta vẫn bằng 0 vì
   chèn vào slack sẵn có; lý do loại endpoint là tính giòn, không phải tính tất định.
3. **Service rate là cổng đồng thời**, không phải secondary metric, và phải đo ở mốc hoàn
   thành chuyến chứ không phải mốc được hứa.
4. Harness: cardinality do driver khai báo thay vì hard-code; trần frame Runner dùng đúng
   ceiling `64 MiB` mà ADR-039 đã khai, vì full-state checkpoint của kịch bản dày vượt
   `16 MiB`. Trần này là guard tài nguyên, không phải bất biến đúng đắn, và vượt trần là
   lỗi typed fail-closed.

**Evidence:** Normalizer chạy theo grid manifest sinh derivative từ dữ liệu công khai
thật; mỗi cell bảo toàn đủ bản ghi nguồn (ví dụ `21.400 → 128, 0 loại`). Bốn đơn vị
paired ở điểm có tải, hai ngày, sample replicate chính thức của publisher: Δ burden âm ở
cả bốn, median `−3.130.086 ms`, sd `1.128.334 ms`, giảm 75–93%. Δ tỷ lệ hoàn thành
`−6,25 / −5,47 / −0,78 / 0,00` điểm phần trăm, mean `−3,13 pp`, sd `3,19 pp`. Chi tiết và
giới hạn claim ở
`benchmarking/wp8-001-pilot-operating-point-evidence-2026-08-19.md`.

**Consequences:** Theo đúng tiêu chí `docs/11` §5/§14 mà dự án tự đặt trước, **C1 ở cấu
hình hiện tại không vượt cổng service ở điểm vận hành này**: thiếu hụt dịch vụ trung bình
gấp khoảng ba lần margin minh hoạ `1` điểm phần trăm. Đây là kết quả **pilot**, n = 4,
không có khoảng tin cậy và không phải effectiveness claim. Confirmatory holdout
`2018-11-14` → `2018-11-18` chưa được sinh và chưa bị chạm. `RB-WP8-002`/`003` Done;
`RB-WP8-004` là ticket Ready tiếp theo, và nó phải xử lý đánh đổi dịch vụ trước khi bàn
tới cỡ mẫu.

**Supersedes / superseded by:** Sửa ba quyết định thiết kế trong ADR-040; giữ nguyên phần
còn lại của ADR-040 và toàn bộ claim boundary WP6/WP7.

### ADR-042 — 2026-08-19 — Superseded

> Superseded bởi ADR-043. Giữ nguyên làm lịch sử phát hiện sai: unit đã gắn
> `masterSeed`, `N=62` không tồn tại trong holdout, và câu “toàn bộ khớp đúng” vượt
> quá bằng chứng review. Không được dùng ADR-042 làm source of truth hiện hành.

**Context:** Thực hiện các ticket tiếp theo của WP8 (`RB-WP8-004` đến `RB-WP8-007`) và tiến hành kiểm toán toàn diện mã nguồn/thuật toán từ WP1 đến WP7 theo đối chiếu văn hiến khoa học.

**Decision:**
1. **Khóa hợp đồng đơn vị thí nghiệm (`RB-WP8-004`):** Đơn vị thí nghiệm được định nghĩa chính xác là $u = (\text{scenarioHash}, \text{masterSeed}, \text{travelRealizationHash})$. Toàn bộ quan sát rider-level phải được gộp (aggregate) lên mức run trước khi thực hiện so sánh paired difference hay bootstrap (`docs/11` §1, §7). Cấm tuyệt đối coi các rider trong cùng run là mẫu độc lập.
2. **Calculator & Standalone Oracle cho Primary Endpoint (`RB-WP8-005`):** Khóa công thức đo lường `total_decision_induced_burden_ms` ($\Delta^{\text{pickETA}}_{\text{decision}} + \Delta^{\text{dropETA}}_{\text{decision}}$) cùng tỷ lệ hoàn thành chuyến (`completed_service_rate`), kèm theo triển khai Oracle BCL-only độc lập (`DecisionInducedBurdenOracle`) đối chiếu byte-for-byte.
3. **Phân phối thực nghiệm Pilot (`RB-WP8-006`):** Trích xuất phân phối gánh nặng thực nghiệm của B1 trên 4 đơn vị pilot tại điểm vận hành có tranh chấp (Manhattan Zenodo $08:00–10:00$, 8 xe). Trung bình B1 tạo ra $3.692.927\text{ ms}$ gánh nặng toàn mạng ($87,2\%$ ở drop ETA). C1 giảm gánh nặng $83,7\%$ nhưng làm giảm tỷ lệ hoàn thành $3,125\text{ pp}$.
4. **Power Analysis & Cỡ mẫu (`RB-WP8-007`):** Dựa trên $\sigma_{\Delta \text{burden}} = 1.128.334\text{ ms}$ và $\sigma_{\Delta \text{service}} = 3,189\text{ pp}$, tính toán cỡ mẫu yêu cầu cho giai đoạn Confirmatory là $N = 62\text{ đơn vị paired}$ (kèm $10\%$ dự phòng).
5. **Kiểm toán mã nguồn WP1–WP7:** Đã rà soát chi tiết toàn bộ các tầng Domain, Application, Algorithms, Solvers, Runner, Benchmarking và FleetPy adapter. Tất cả bất biến trạng thái, luật bảo toàn, cơ chế cắt tỉa và giải thuật lexicographic CP-SAT đều khớp đúng lý thuyết và văn hiến chuẩn mực.

**Evidence:**
- `docs/benchmarking/wp8-006-pilot-empirical-distributions-2026-08-19.md`
- `docs/benchmarking/wp8-007-power-and-sample-size-report-2026-08-19.md`
- Toàn bộ test suite pass 100%.

**Consequences:** `RB-WP8-004`, `005`, `006`, `007` chuyển sang **Done**. `RB-WP8-008` (Frontier B1 $\rightarrow$ C2 $\rightarrow$ C1) trở thành ticket **Ready** duy nhất.

### ADR-043 — 2026-08-21 — Accepted

**Context:** Tiếp tục handoff WP8, hoàn tất frontier và review adversarial trước khi
chạm outcome confirmatory. Review xác nhận bốn blocker F1/F7/F9/F10/F26 là thật,
đồng thời tìm thêm lỗi metric frame, absolute-delta additivity, Windows PID lineage
và analysis/source binding.

**Decision:**

1. Experimental unit là `(scenarioHash, demandRealizationHash,
   travelRealizationHash)`; `masterSeed` chỉ là robustness và không tăng N. Holdout
   khả dụng là fixed panel đúng 20 cell/5 travel-day cluster; không phát population
   p-value/NI CI.
2. Giữ strict service margin `m=1,0 pp`. Gate dùng exact integer aggregate trên
   denominator `20 × 108 = 2160`; equality tại biên là fail. Burden improvement
   không cứu service failure.
3. Frontier 25/25 đóng: B1→C1 unbounded là lock/ranking price; C1 unbounded→tight
   là budget price. Pickup-ETA reduction do lock định nghĩa và drop-ETA reduction
   không bị lock phải báo riêng; pilot xấp xỉ 18%/82% không được áp sẵn cho WP9.
4. Oracle burden phải chạy process BCL-only và mutation; pairing bind orientation,
   policy/unit/denominator; decision-frame count tối đa một lần/envelope. Verifier
   recompute lifecycle/checkpoint/report/behavior and audited solver evidence.
5. Node-cap failure v1 không được loại cell. Amendment pre-outcome đồng nhất
   request target 128→108, materialize 20/20 derivative v2.
6. Handoff review tìm thấy analyzer chưa bind bundle với exact plan/label/scenario và
   HEAD/status không bind nội dung dirty tree. Amendment pre-outcome `WP8-011b` thêm
   execution binding, full Git-visible content+HEAD inventory pre/post, path-safe IDs,
   robustness analyzer không gate và exact locked/earned decomposition.
7. Freeze receipt v1 `H2=97af95cf…d3049` giữ byte-nguyên làm lịch sử. Receipt v2
   `H3=d028eae4…dd14e` supersede operationally; executable verifier recompute 24
   file hashes, Runner và derivative/scenario tree seals trước outcome.
8. Full-PDF audit Alonso-Mora/Gschwind/Simonetto/Engelhardt/Zalesak/Schulz chỉ cho
   phép exact same-state/same-route reuse. Không thêm direction/random/sparse prune;
   work counters/output giữ exact, local process time giảm khoảng 20–23%, không SLA.

**Evidence:** `docs/benchmarking/wp8-006..014`, amendments `wp8-011a/011b`,
`docs/reviews/wp1-wp8-final`, frontier external 25/25, `.NET 840/840`, pinned Python
suite hiện hành, freeze verifier PASS với `H3` nêu trên.

**Consequences:** `RB-WP8-001..014 Done`; WP8 Complete. WP9 mở theo
`docs/tasks/39-wp9-main-experiment-ticket-plan.md`; `RB-WP9-001 Done`,
`RB-WP9-002` là ticket Ready duy nhất. Không confirmatory outcome tồn tại khi ADR
này được chấp nhận. Sau smoke, mọi bug outcome-bearing phải invalidate affected run,
không được vá lẻ hoặc đổi margin/treatment.

### ADR-044 — 2026-08-21 — Accepted

**Context:** Sau ADR-043 nhưng vẫn trước bất kỳ Layer-2/confirmatory outcome nào,
receipt của Layer-1 mechanical cho thấy harness đã gọi Runner Release hiện hành
`4c297a2c…bd2a8`, trong khi H2/H3 pin artifact cũ `16f3b5e8…3aad`. Hai DLL cùng
174.592 byte nhưng không byte-identical. Một publish sạch từ source đã review cho
đúng hash hiện hành; 19/19 file của publish tree byte-identical với Runner tree mà
Layer-1 đã dùng.

**Decision:**

1. Chấp nhận amendment pre-outcome `WP8-011c`; đây chỉ là sửa provenance, không
   đổi panel, policy, configs, seed, endpoint, strict margin hay analysis.
2. Giữ H2/H3 byte-nguyên làm lịch sử. Receipt v3
   `H4=2f7e6bf36c16784e06cb3266f9764f3103f2de6fc931f3c8e023bdc1a81a32dd`
   là freeze vận hành hiện hành; Runner DLL là `4c297a2c…bd2a8` và Runner-tree seal
   là `29a8195b…4589`.
3. Freeze verifier phải bind 25 file/Runner hashes và recompute ba tree seal
   (derivative, scenario plan, Runner artifact). Mọi job WP9 dùng đúng external
   publish root đã pin.
4. Layer-1 bundle ngoài repo `E:\RideBoundData\wp9\layer1\bundle-20260821-v1`
   PASS 8/8, external verify, evidence class `mechanical`; nó đóng `RB-WP9-003`
   nhưng không phải effectiveness/confirmatory evidence.

**Evidence:** `wp8-011c-pre-outcome-runner-artifact-repin.md`,
`freeze-receipt-v3.json`, freeze verifier PASS (25 hashes + 3 tree seals), Layer-1
receipt `bundle-20260821-v1.receipt.json`, per-file comparison 19/19 exact.

**Consequences:** `RB-WP9-001/003 Done`; `RB-WP9-002` audited smoke là ticket
Ready duy nhất. Chưa có confirmatory outcome. H4/Runner tree và repository inventory
phải giữ bất biến từ smoke tới full matrix; thay đổi outcome-bearing sau smoke phải
invalidate affected runs.

### ADR-045 — 2026-08-22 — Accepted

**Context:** Smoke WP9 chết ở **cả hai** arm với
`The active route could not be retained as the safety no-op candidate: MAX_RIDE_TIME`.
Cơ chế: giao thông xấu đi làm lộ trình *đang chạy* vi phạm `MAX_RIDE_TIME`,
`PhysicalPlanValidator` prune chính no-op đó, xe còn 0 candidate và run fail-closed.
Vì B1 cũng chết nên đây không phải lỗi cam kết; nó cũng không làm mất hiệu lực 25 run
frontier WP8 đã đóng. Gốc rễ là một khoảng trống ngữ nghĩa: `MAX_RIDE_TIME` chưa
bao giờ được định nghĩa là ràng buộc *lúc lập kế hoạch* hay ràng buộc *liên tục*.
`CommitmentBreachRecord`/`AppendBreach` đã tồn tại nhưng `AppendBreach` chỉ được gọi
khi deserialize checkpoint — không đường quyết định nào từng ghi breach.
`PICKUP_WINDOW` có đúng cùng lỗ hổng và cùng nguyên nhân ngoại sinh.

**Decision:**

1. Tách physical constraint thành hai lớp. **Structural** (`ROUTE_CONNECTIVITY`,
   `PRECEDENCE`, `CAPACITY`, `FROZEN_PREFIX`, `ONBOARD_PRESERVATION`,
   `ACCEPTED_PRESERVATION`, `PLAN_VERSION`, `STOP_LOCATION`, `INVALID_*`,
   `SCHEDULE_OVERFLOW`) là bất biến của một kế hoạch well-formed: vi phạm là defect,
   không phải giao thông, và vẫn strict ở mọi nơi. **Service-quality**
   (`MAX_RIDE_TIME`, `PICKUP_WINDOW`) là lời hứa về thời gian và có thể bị phá vỡ
   mà không ai quyết định gì.
2. Service-quality là ràng buộc **lúc tạo kế hoạch**, không phải bất biến liên tục.
   No-op an toàn luôn được giữ; nó không bao giờ bị prune bởi một dimension
   service-quality.
3. Vi phạm ngoại sinh được ghi nhận, không bị nuốt: `ProbeServiceQuality` chiếu lộ
   trình *không đổi* dưới travel snapshot hiện hành và phát
   `ExogenousServiceQualityBreach` (vehicle, request, code, dimension, contractual,
   exogenous) vào `CandidateGenerationDiagnostics`. Xe tiếp tục phục vụ.
4. **Chống rửa vi phạm.** Bound nới ra đúng bằng `max(contractual, exogenous)`, với
   `exogenous` là giá trị mà *không làm gì* đã hiện thực hoá. Do đó không candidate
   nào được phép tệ hơn no-op trên chính dimension đang breach; phần tệ hơn vẫn bị
   prune với `Expected` là bound hiệu lực. Đây đúng là cơ chế ba chiều
   `exogenous / decision-induced / visible` dự án đã có (`PromiseDeltaCalculator`),
   không thêm khái niệm mới.
5. Request chưa nằm trên lộ trình đang chạy (mọi request mới chèn) **không** có
   entry nào trong relaxation, nên vẫn bị enforce contractual tuyệt đối. Không thể
   nhận một khách mà lộ trình không phục vụ nổi.
6. Relaxation là hàm thuần của `(run, vehicle, travelSnapshot, evaluationTime)` và
   được áp dụng **đồng nhất ở cả hai arm**, nên không dịch chuyển arm nào so với arm
   nào. Ba đường re-validate downstream (`RollingCostPolicy`, `MultiplePlanPolicy`,
   `CommitmentDecisionValidator`, `OnlineStateCheckpointCodec`) đều chuyển sang
   `ValidateWithExogenousRelief` để không bác chính candidate mà generator giữ hợp lệ.
7. `ForwardSlackProfile` certify delay theo đúng bound mà validator enforce; cache key
   bind thêm digest của relaxation. Nếu hai bên lệch nhau, một route validator chấp
   nhận vẫn có thể mất slack certificate và bị prune.

**Evidence:** `ServiceQualityAllowance`/`ProbeServiceQuality`/
`ValidateWithExogenousRelief` trong `PhysicalPlanValidator`; 6 regression Domain
(breach ride-time, breach pickup-window, chống rửa, structural fail-closed, no-relief
khi không breach, scoping theo request/dimension) + 4 regression Algorithms (no-op
sống sót, breach được báo cáo đúng số, không breach thì không báo, candidate tệ hơn
no-op vẫn bị prune). Full solution 851/851 Debug và Release; Release `-warnaserror`
0 warning.

**Consequences:** `RB-WP9-002a/002b` hết bị chặn. Ngữ nghĩa đối xứng hai arm nên không
đụng tính công bằng đã khoá ở ADR-043. Ba giới hạn phải giữ trong báo cáo:
(a) `ExogenousServiceQualityBreach` hiện **chỉ tồn tại trong tiến trình**:
`SolverExecutionEvidenceMapper.WriteGeneration` liệt kê field tường minh nên breach
không được serialize vào `solver.executionEvidence`, và `SolverEvidenceFields` của
contract là tập đóng nên thêm nó là một thay đổi contract. Vì vậy breach hiện
**không** vào transcript, **không** vào `CommitmentBreachRecord`/`AppendBreach`, và
**không đo được** từ bundle. Bắc cầu cả hai — evidence và ledger — là `RB-WP9-002c`;
(b) cho tới khi `002c` đóng, không tài liệu nào được báo cáo breach count như một
đại lượng đã đo. Sau `002c` nó vẫn là secondary/descriptive, không phải endpoint đã
prereg, nên không cứu được gate nào; (c) thay đổi này là outcome-bearing với mọi run WP9 chạy sau nó,
nên freeze chain phải repin trước smoke và mọi run trước đó không được trộn vào cùng
estimand.


### ADR-046 — 2026-08-22 — Accepted

**Context:** Review toàn bộ WP8 trước khi mở ma trận WP9 tìm được ba defect trong
đường phân tích confirmatory — nơi con số kết luận thực sự được sinh ra — mà toàn
bộ test suite trước đó không bắt được.

1. **Blocker.** `wp9_fixed_panel_analyze.py` không chạy được trên chính manifest đã
   đóng băng. Manifest khai arm bằng danh tính preregistered
   (`b1-rolling-cost`, `c1-hard-vector-tight-30s`), còn analyzer nối thẳng chuỗi đó
   vào `f"p-{cell}-{arm}-tight-s7"` để dựng jobId kỳ vọng, trong khi execution plan
   dùng token ngắn `b1`/`c1`. Mọi cell raise `primary job binding differs`. Nghĩa
   là `RB-WP9-005` chưa từng chạy end-to-end; unit test cũ chỉ truyền token ngắn
   tổng hợp nên không bao giờ chạm vào artifact thật.
2. **Fail-open.** Cả analyzer chính lẫn analyzer robustness chỉ kiểm manifest
   "không rỗng, cellId không trùng". Một manifest liệt kê 19/20 cell qua được mọi
   kiểm tra và cho ra verdict `pass` trên mẫu số nhỏ hơn mà không nói gì.
3. **Fail-open.** Orientation arm không bị chặn ở tầng Python. `PairedComparisonDesign`
   trong `ExperimentalUnitModels.cs` chặn đúng, nhưng analyzer confirmatory là một
   cài đặt Python độc lập không dùng contract đó; `experimentalUnitModelsSha256`
   trong freeze receipt vì thế cho một cảm giác an toàn sai.

**Decision:**

1. Analyzer bind arm bằng registry tường minh `_PRIMARY_ARMS`
   (danh tính preregistered → token plan + wp4 config) và pin orientation
   `_PRIMARY_ORIENTATION`; arm lạ hoặc đảo chỗ baseline/treatment fail closed.
2. Cả hai analyzer bắt buộc tập cell của manifest **bằng đúng** tập cell primary/
   robustness của execution plan đã đóng băng. Không còn phân tích được panel cụt.
3. Regression mới bind vào **artifact thật** (`analysis-manifest-v1.json` +
   `execution-plan-v1.json`), không phải dict tổng hợp. Đây là gốc rễ vì sao lỗi 1
   sống sót qua nhiều vòng review.
4. Cell dùng lại một bundle cho cả hai arm fail closed.

**Evidence:** Python suite 77 → **86** pass, 0 skip (với `RIDEBOUND_FLEETPY_ROOT`).
`FrozenManifestBindingTests` chứng minh 20/20 cell của manifest thật bind đúng vào
plan thật; `CapacityPanelBindingTests` chứng minh Panel A/B không lẫn nhau.

**Consequences:** `analysisProgramSha256` và `robustnessAnalysisProgramSha256` đổi,
nên `H4` mất hiệu lực và `H5` phải repin — trùng với yêu cầu của ADR-045. Bài học
giữ lại: một freeze receipt pin hash của file không chứng minh file đó *chạy được*;
mọi program nằm trên đường outcome phải có ít nhất một test chạy nó trên đúng
artifact đã đóng băng.

### ADR-047 — 2026-08-22 — Accepted

**Context:** Panel B (`veh4`) bắt được hai defect mà Panel A (`veh8`) chạy 60/60
sạch vẫn không lộ. Đây chính là lý do phải có điểm năng lực căng: ở 8 xe lộ trình
còn dư thời gian nên không bao giờ chạm biên deadline; ở 4 xe thì chạm liên tục.
3/9 job Panel B đầu tiên chết với `RBWP7_FLEETPY_PLAN_INFEASIBLE`.

**Defect 1 — phạm vi relief của ADR-045 quá rộng.** ADR-045 nới bound
service-quality cho *mọi* candidate. Nhưng adapter chỉ gửi kế hoạch sang FleetPy
khi lộ trình **đổi** (`fleet_control.py` bỏ qua route không đổi), và FleetPy giữ
`VehiclePlan.is_feasible()` của riêng nó theo deadline hợp đồng gốc. Hệ quả:
RideBound đề xuất một kế hoạch mà simulator từ chối. Relief đúng ra chỉ cần cho
no-op — thứ không bao giờ được gửi đi.

**Defect 2 — lượng tử hoá vị trí xe làm ETA lạc quan.** `mapping.py` mã hoá tiến
độ trên cạnh thành permille bằng `ROUND_HALF_EVEN`. Permille là 1/1000 cạnh, nên
nửa permille của một cạnh 130 s là 65 ms. Làm tròn tới gần nhất có thể đặt xe
*trước* vị trí thật, khiến mọi ETA hạ nguồn lạc quan. Một job chết vì vượt cửa sổ
đón đúng **13 ms** (`latest 1916000 ms`, FleetPy tính `1916013 ms`) trong khi
RideBound tính ra đúng hạn. Defect này có từ trước ADR-045 và độc lập với nó.

**Decision:**

1. Relief chỉ áp cho **safety no-op**. Mọi candidate đã đổi được sinh dưới
   `ServiceQualityAllowance.Strict`. RideBound vì thế không bao giờ đề xuất kế
   hoạch mà FleetPy từ chối, và tính chống rửa vi phạm mạnh lên mức tối đa.
   Hệ quả hành vi phải nói rõ: một xe có lộ trình đang breach sẽ **chỉ còn no-op**
   cho tới khi breach trôi qua — đó là chi phí dịch vụ thật, không được giấu.
2. Lượng tử hoá permille đổi sang `ROUND_FLOOR`. Kết hợp với `DivideRoundUp` sẵn
   có cho phần thời gian còn lại của cạnh, RideBound bảo thủ ở **cả hai** nửa và
   không bao giờ lạc quan hơn FleetPy.
3. Freeze receipt lên schema `5.0.0` và bind thêm
   `adapterPackageTreeSealSha256` trên `simulators/fleetpy-ridebound/ridebound_fleetpy`.
   Toàn bộ package adapter là outcome-bearing nhưng trước đó **không được pin**:
   một dòng đổi rounding trong `mapping.py` dịch chuyển mọi ETA mà freeze không
   hề biết.

**Evidence:** 3/3 job Panel B từng hỏng nay `completedVerified` — 2 job đầu do
Decision 1, job còn lại chỉ qua sau Decision 2. `.NET 852/852`; pinned Python
86/86, 0 skip.

**Consequences:** Đây là thay đổi **outcome-bearing**. Theo `RB-WP8-014` §5,
mọi dữ liệu confirmatory đã chạy dưới `H5` bị **vô hiệu** và phải chạy lại dưới
freeze mới:

- Panel A 60/60 dưới `H5` và kết quả `Δ_service_panel = −7,08 pp` **không còn là
  outcome confirmatory**. Nó được giữ nguyên byte làm bằng chứng vận hành và
  bằng chứng rằng đường phân tích chạy được end-to-end, **không** được trích dẫn
  như kết quả.
- Panel B 6/40 dưới `H5` cũng bị bỏ.
- Không được tái sử dụng bundle `H5` nào bằng cách so hash rồi giữ lại cái trùng:
  đó là chọn lọc phụ thuộc outcome. Chạy lại toàn bộ hai panel.

Bài học: điểm năng lực căng không chỉ là một điểm dữ liệu thêm — nó là công cụ
kiểm lỗi. Hai defect này sống sót qua toàn bộ WP7/WP8 vì mọi thứ trước đó đều
chạy ở mức đội xe dư.

### ADR-048 — 2026-08-23 — Accepted

**Context:** Toàn bộ ma trận H6 đã hoàn tất: Panel A 60/60 bundle (40 primary,
20 robustness) và Panel B 40/40 primary. Hai analyzer canonical chạy trên đúng
manifest/plan đã freeze. Service gate preregistered thất bại ở cả hai capacity,
trong khi burden gate đạt. Robustness tách được chi phí lock/ranking và hard
budget nhưng không thể thay đổi gate primary.

**Decision:**

1. Chấp nhận và công bố kết quả âm: Panel A `1735 → 1581`, `−7.1296 pp`; Panel B
   `966 → 860`, `−4.9074 pp`; margin là `−1.00 pp`, không đổi hậu outcome.
2. Không pool hai panel. Demand/travel realization giống nhau nhưng fleet state và
   `scenarioHash` khác; between-capacity chỉ là heterogeneity mô tả.
3. Burden reduction phải đi cùng locked/earned decomposition và service result.
   Không gọi `99%+` là cùng công việc được làm tốt hơn khi treatment có thể từ chối
   công việc. Pickup-ETA lock là definitional.
4. Robustness giữ `confirmatoryGate:null`; C1 unbounded, C2 loose và seed19 không
   cứu primary. Seed19 là non-replicate và tăng N bằng 0.
5. Giữ finite-panel boundary: 20 cell nhưng 5 travel realization, precision đạt
   khoảng `1.40 pp`, sign-flip floor `0.03125`; không CI/p-value population.

**Evidence:** `wp9-confirmatory-result-2026-08-23.md`; analysis SHA-256 Panel A
`72f052d7…880e0`, Panel B `3f6a339c…bbe3f`, robustness `ce87ea75…9533b`;
receipt Panel A `8c7cf66a…96a5a`, Panel B `cb86aa4a…2165`.

**Consequences:** `RB-WP9-004..008 Done`. WP9 verdict là kết quả âm có điều kiện
theo đúng hai điểm 4/8 xe. Không có claim population, SLA, satisfaction, fairness
hoặc novelty. WP10 phải giữ kết quả này và chỉ đo heterogeneity cross-system.

### ADR-049 — 2026-08-23 — Accepted

**Context:** ADR-045 tạo `ExogenousServiceQualityBreach` trong generation
diagnostics nhưng evidence v1.0 không serialize nó và runtime không append ledger.
Vì H6 đã hoàn tất, bridge có thể được cài đặt mà không làm thay đổi estimand đã
freeze. Review deserialize cũng cho thấy redundant safety/budget fields cần được
Domain kiểm trực tiếp, không chỉ dựa vào outer checkpoint hash.

**Decision:**

1. Evidence v1.1 thêm canonical `exogenousServiceQualityBreaches`; contract và
   verifier vẫn nhận v1.0 để tái kiểm H6, nhưng bác version/shape/value/order sai.
2. Thêm loại ledger `ExogenousServiceQuality` không cần operational incident giả.
   Exogenous projection phải bằng safety projection, decision delta bằng zero,
   visible bằng exogenous, budget không đổi, witness phải là overrun đúng dimension.
3. Runtime bridge reproject reduced pre-decision state, append breach trước stage,
   và hash certificate/state sau bridge. Lỗi bridge fail session, không nuốt evidence.
4. Checkpoint cũ giữ byte shape; record mới có explicit kind/witness array và decode
   kiểm toàn bộ projection/delta/budget/witness đã serialize.
5. Mọi probe v1.1 là post-outcome mechanism evidence, không được trộn vào H6.

**Evidence:** `.NET` targeted Domain/Runner tests PASS; Python verifier 93/93 trước
final full gate. Real FleetPy Panel A/B1 probe PASS independent verifier: 800 epoch,
43 evidence observation và đúng 43 ledger record; zero decision charge, zero budget
change, zero invalid witness. Empty branch Panel B/C1 PASS 353 epoch với 0/0.
Hash chi tiết ở `benchmarking/evidence/wp9-009-breach-bridge-smoke-v1.json`.

**Consequences:** `RB-WP9-009 Done`; ADR-045 consequence “breach chưa vào transcript/
ledger” bị supersede. H6 outcome và freeze không đổi. WP9 `001..009` đóng; WP10 là
work package active kế tiếp.

### ADR-050 — 2026-08-23 — Accepted

**Context:** WP9 đã đóng với negative confirmatory result. WP10 cần một framework
độc lập nhưng không được thay Runner hoặc reimplement decision logic. Official source
audit xác nhận RidePy tag `v2.10.1` ở commit `bf1863e…9f14`, MIT, có extension point
`FleetState`/`VehicleState`/`TransportSpace`. Upstream pin POSIX/Linux và Windows
CPython 3.10 không có binary wheel. Graph CPE không expose directed-edge fraction
ổn định.

**Decision:** (1) RidePy là Layer 3 final mặc định; AMoD2 chỉ fallback nếu named gate
thất bại. (2) Checkout/vendor build nằm ngoài repo, bind commit + exact `lru-cache`/
`googletest` submodule commits + 527-file tree inventory `d99ffac8…d891e` + license
hash. (3) Environment là Linux container với
base digest `a365ce6a…0e235`; không sửa vendor để chạy Windows. (4) Adapter subclass
`FleetState`, dùng native fast-forward/space/events nhưng mọi B1/C1 decision đi qua
cùng versioned Runner. (5) Position model downgrade explicit thành `nodeOnly`;
party size pin 1; main reassignment false. (6) WP10 subset chỉ là descriptive paired
heterogeneity, không pool hoặc rescue H6. (7) Queue/gates chính thức là
`tasks/40-wp10-ridepy-layer3-ticket-plan.md`.

**Consequences:** `RB-WP10-001 Done`, `RB-WP10-002 Ready`. Source/env verifier phải
pass trước code adapter; canonical scenario phải reconcile native pickup/drop trước
freeze paired subset. O-005 được đóng chọn RidePy, nhưng có rollback named sang
AMoD2 nếu capability gate fail.

### ADR-051 — 2026-08-23 — Accepted

**Context:** Exact RidePy source/runtime và same-Runner canonical scenario đều pass.
Canonical B1/C1 mỗi arm hoàn thành 5/5 request, reconcile 5 pickup + 5 drop và giữ
Runner publish tree bất biến. Final frozen representative subset đã chạy 22/24 arm
jobs: 22 pass, B1 `travel-update-stress-r3` fail closed tại epoch 17 và C1 paired arm
không chạy theo no-partial-reuse policy. Failure xảy ra sau native pickup tại 116 giây
trong khi Runner chỉ quan sát node cuối và còn giữ pickup ETA 178 giây. Một event xe
khác đã tạo epoch khi xe này giữa cạnh; RidePy CPE không cung cấp directed-edge
progress cho capability `nodeOnly`.

**Decision:** (1) Đóng `RB-WP10-002..010` và WP10 bằng **negative capability result**.
(2) Gán mã `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED`; không suy diễn progress
từ clock, không đổi manifest, không bỏ failed job và không chạy paired arm còn lại.
(3) Giữ freeze/output v1/v2/v3 để audit; chỉ v3 là terminal attempt được diễn giải.
(4) Báo 11 valid pairs riêng: B1 `54/62`, C1 `49/62`, `−8.06 pp`, descriptive only;
không coi đây là planned-subset estimand, không CI/population inference và không pool
H6. (5) Không chuyển AMoD2 hậu-outcome; nếu làm sẽ là work package/ADR mới.

**Consequences:** Exact-source/environment, same Runner, mapping, native lifecycle,
canonical và verifier gates PASS; representative subset gate FAIL CLOSED. Layer 3
cross-system claim **not established**. WP10 vẫn hoàn tất vì failure được giữ và gate
được đánh giá trung thực. Evidence chính là
`benchmarking/wp10-ridepy-layer3-negative-capability-result-2026-08-23.md`; external
receipts bind source `2b431062…0775`, freeze v3 `18a74fa3…6672`, strengthened
subset analysis v2 `be3e9077…cca3` và failure transcript `0ee5e3ec…a85`.

### ADR-052 — 2026-08-23 — Accepted

**Context:** Final WP1–WP10 review đọc full PDF Alonso-Mora, Gschwind–Drexl,
Simonetto, Engelhardt, Zalesak và Schulz–Pfeiffer, đồng thời đo lại candidate hot
path. Full constant-time temporal insertion check không thể được nhập nguyên xi:
RideBound còn kiểm structural/commitment constraints và generic travel snapshot không
cam kết triangle inequality. Review cũng tìm thấy `ForwardSlackCacheKey` tạo textual
position identity dù immutable `VehicleState` đã bind position, và terminal node tạo
key thứ hai chỉ để xác nhận lookup do chính node đó prefetched.

**Decision:** (1) Không thêm heuristic prune, sparse/direction/random filter hay
paper-derived default. (2) Bỏ position string dư khỏi process-local cache key; vehicle
reference tiếp tục bind toàn immutable snapshot. (3) Thêm exact `Matches` trên key đã
tạo để kiểm run/vehicle/route/evaluation/travel/allowance mà không allocate/hash key
thứ hai. (4) Giữ nguyên full physical validator, cache-failure lifecycle, search order,
work/candidate caps, candidate identity và solver input. (5) Thêm source-controlled
Release harness và chạy 3 baseline + 3 optimized process; timing chỉ descriptive.

**Evidence:** Key construction giảm allocation `40,000,072 → 28,000,072` byte mỗi
250.000 lookup (−30%) ở cả route 4/8/16 stop. Complete generator giảm 0,79–1,30%
allocation; timing mixed (`−4,9%`, `+2,8%`, `−1,9%`) nên không có speed claim. Sáu
semantic work counters exact ở mọi run; required .NET 855/855, FleetPy 95/95,
RidePy 23/23, Release/format/vulnerability/static gates pass. Evidence ở
`benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md` và review
`reviews/wp1-wp10-final/README.md`.

**Consequences:** Tối ưu được giữ vì allocation benefit xác định và semantic
equivalence, không vì một effectiveness/SLA claim. H6/WP9 và WP10 terminal verdict
không đổi. Exact RidePy image đã archive/load lại với SHA-256
`4783c541…9a872`; đây là restore evidence, không biến Dockerfile apt/pip thành future
byte-reproducible rebuild. Final PDF 12 trang đã render/inspect, SHA-256
`066168872d7ead11362b3f0f7b5832e8e1147bb655f281cc3ec08d939c29b20b`.

**Final test execution note:** Một attempt chạy suite đồng thời với Release build/
format làm medium public-drain chạm đúng `resource.cpu-time-exceeded` (`854/855`).
Không đổi ceiling và không reclassify. Rerun exact `dotnet test RideBound.slnx`
hoàn toàn đơn độc pass 855/855; đó là final baseline.

### ADR-053 — 2026-08-23 — Accepted

**Context:** H6 fail service decisively ở cả 8 xe và 4 xe; robustness cho thấy
lock/ranking và 30-second budget cùng đóng góp vào descriptive service gap, còn
unbounded/loose policy không khôi phục đủ. Raw H6 v1 chứa full transcripts, generation
counts, prune witnesses, selected actions và exact receipts, nhưng không chứa toàn bộ
route/schedule của retained candidate portfolio. State hashes khác ngay epoch đầu vì
bind policy/manifest identity dù observed events/actions còn giống nhau. Full-PDF
review Pillac, Ulmer et al. và Ackermann et al. cho thấy service, route-plan state,
future information và plan-pool/secondary-objective interactions phải được tách; không
có paper-derived budget/horizon mặc định hợp lệ cho RideBound.

**Decision:** (1) Giữ H6/WP10 immutable và mở WP13 như exploratory mechanism
diagnostics. (2) Căn chỉnh arm bằng ordered observed event projection, wire decision
projection và operational decision projection. Operational projection chỉ neutralize
thứ tự `promisePublished` do generated publication ID; request/plan/breach order không
đổi. State/decision hashes vẫn được verify nội bộ nhưng không định nghĩa cross-arm
divergence. (3) Gọi downstream difference là
`trajectoryAssociated`, không causal. (4) Chỉ tính minimal relaxation từ exact
recorded witness; field thiếu là `notRecorded`. (5) Không rerank, tăng budget, đổi
objective/slack, forecast hoặc tạo policy v2 trước evidence. (6) WP14–WP20 chỉ ở
roadmap-level; ticket hóa duy nhất WP13 theo progressive elaboration.

**Evidence:** 106/106 trang từ ba full PDF có local page text/render/SHA receipts tại
`E:\RideBoundData\research\pdf-20260823-post-h6`; synthesis ở
`research/post-h6-mechanism-diagnostics-full-pdf-evidence-2026-08-23.md`. Raw roots
Panel A/B chứa 100 immutable bundles và đã qua independent WP9 reproducibility
verification. Manual transcript check chứng minh equal observed input/actions ở các
epoch đầu trong khi policy-bearing state hashes khác.

**Consequences tại thời điểm chấp nhận ADR-053:** `RB-WP13-001` Done;
`RB-WP13-002` là implementation ticket active và vào In review với một required CPU
regression gate còn mở. ADR-054 sau đó đóng gate này mà không sửa H6.
WP13 không rescue primary, không CI/population inference và không báo burden gần zero
như quality optimization. Chỉ khi WP13 verifier/closure gate đạt mới quyết định mở
WP14. Không tự tạo tag/branch trong dirty/uncommitted working tree; H6 freeze receipts
và hashes tiếp tục là authority.

### ADR-054 — 2026-08-24 — Accepted

**Context:** Closure run của `RB-WP13-002` tái lập duy nhất một failure trong required
Debug suite: medium public-drain chạm `resource.cpu-time-exceeded`. Diagnostic giữ
nguyên ceiling đo 120.062/120.000 ms. Managed sample trace cho thấy canonical-number
validation lặp lại collection expression `['.', 'e', 'E']` trên number-heavy state
canonicalization path; đây là allocation/initialization thừa, không phải solver hoặc
policy semantics.

**Decision:** (1) Thay đúng bốn production call site trong protocol envelope,
protocol payload, canonical JSON và independent WP6 oracle bằng
`ReadOnlySpan<char>.IndexOfAny(char,char,char)`. (2) Giữ nguyên exact marker set `.`,
`e`, `E`, rule `-0`, safe-integer bounds, error classes, property/array order và
canonical bytes. (3) Thêm uppercase-`E` boundary test. (4) Không đổi 120-second CPU
ceiling, không đổi Runner/solver policy và không repin hoặc rerun H6. (5) Giữ failure
trước tối ưu như evidence, không reclassify nó thành pass.

**Evidence:** Nettrace ngoài repo dài 7.298.446 byte, SHA-256
`c336dac72440e1956ac4cc43ca2800c2eecb9059dfab927fb13f8d7871a8e469`;
canonical tests 16/16; exact medium public-drain 1/1 pass trong 1 phút 33 giây với
128 accepted request/801 protocol event; required `dotnet test RideBound.slnx`
856/856 pass, zero skip.

**Consequences:** `RB-WP13-002 Done`, `RB-WP13-003 Ready`. Đây là exact hot-path
optimization và gate closure, không phải service-effectiveness/SLA claim. Frozen H6
vẫn bind Runner artifact cũ của chính experiment; source hiện hành không được dùng để
viết lại primary result hoặc làm mất reproducibility chain.

### ADR-055 — 2026-08-24 — Accepted

**Context:** Report `002` đã tìm first divergence nhưng pair object còn là internal
report shape, dùng `null` cho missing arm và chưa có schema/tool/evidence binding ở cấp
record. Ticket sau không được tự diễn giải cấu trúc ad-hoc hoặc biến missing evidence
thành observed equality.

**Decision:** (1) Khóa strict self-contained Draft 2020-12 record-set schema v1 và
một record cho từng exact Panel A/B B1/C1 pair. (2) Bind exact report `002` bằng
length/SHA, analyzer/verifier hashes, panel inventory, schema và generator source.
(3) Dùng conditional field presence: paired divergence cần hai arm, transcript-length
cần đúng một arm, `noneObserved` không có arm evidence; record set không chứa `null`.
(4) Với equal-input operational divergence, yêu cầu epoch/time/input hash bằng nhau và
operational-decision hash khác; divergence epoch bằng equal-prefix + 1. (5) State hash
chỉ là audit field; interpretation luôn
`descriptiveFirstDivergenceNotCausal`. (6) Generator nhận explicit immutable roots và
cấm output nằm trong đó hoặc overwrite source report.

**Evidence:** 40/40 records (20 A, 20 B), tất cả
`operationalDecisionDivergenceOnEqualObservedInput`; output 102.746 byte SHA-256
`bef27519b5dae4482029be83cd8d1c2b1e0ef2afa72ea63e6645f4991e425618`;
schema `47e24bb…3d8b8`, generator `4f52b76b…f74f8`. Targeted 11/11, pinned Python
122/122, independent schema/binding/invariant check và required Debug 856/856 pass.

**Consequences:** `RB-WP13-003 Done`, `RB-WP13-004 Ready`. Contract này chỉ khóa
where/when observed behavior first differs; mechanism class, minimal relaxation và
downstream association vẫn phải được chứng minh ở ticket sau, không được suy ra từ
40/40 count.

### ADR-056 — 2026-08-24 — Accepted

**Context:** Record `003` khóa where/when behavior khác nhưng chưa đọc lại exact raw
actions tại epoch đó. Không được suy completed-service loss, lock/budget/ranking cause
hoặc policy improvement từ action-type list hay policy-bearing state hash.

**Decision:** (1) Comparator phải bind exact record-set/schema/generator/source-report,
frozen Panel A/B inventory và bundle arm/unit/scenario. (2) Cả 80 primary transcripts
phải được đọc đến shutdown/EOF; mọi frame/file receipt và solver evidence được verify,
không return sớm sau target epoch. (3) Target bind epoch/time cùng observed,
operational và wire projection hashes. (4) Arrived request/outcome phải song ánh;
vehicle ID chỉ có khi accepted, không dùng `null`. (5) Ordered multi-label classes là
disposition, accepted-vehicle assignment, request payload, vehicle plan, promise,
solver status và other action. (6) Immediate accepted-count relation luôn gắn claim
boundary `postOutcomeExploratory`, `descriptiveNotCausal`; downstream service không do
artifact này đánh giá.

**Evidence:** 40/40 records bind raw evidence; 80/80 transcripts và 44.156 decisions
được verify. Panel A: C1-lower 3, equal 17, accepted delta −3; Panel B: C1-lower 5,
equal 15, delta −5; không C1-higher. Output 79.864 byte SHA-256
`3717f093c62c37a339da0b826323fb1604a684bd9990630d9d9dc5563fd4f7e3`;
comparator source `f2c55e1f…dd0ee`. Targeted 13/13, pinned Python 135/135,
independent canonical/hash/aggregate check và required Debug 856/856 pass.

**Consequences:** `RB-WP13-004 Done`, `RB-WP13-005 Ready`. −8 immediate accepted
requests không thay thế Panel A/B completed-service denominators và không chứng minh
causation. Exact minimal relaxation chỉ được tính từ recorded prune witnesses ở `005`;
retained portfolios vẫn `notRecorded` và không được reconstruct.

### ADR-057 — 2026-08-24 — Accepted

**Context:** B1 selected candidate có thể được link bằng policy-independent candidate
identity sang C1 prune witness, nhưng H6 không lưu retained portfolio và commitment
validator fail-fast ở blocker đầu tiên. Vì vậy “minimal relaxation” không được hiểu là
counterfactual candidate feasibility hoặc recovered service.

**Decision:** (1) Bind exact report `004`, record `003`, frozen roots và raw action/
witness receipts bằng two-pass scan tới EOF. (2) `requestAccepted.candidateId` phải bind
`vehiclePlanUpdated` cùng vehicle; duplicate request/candidate hoặc mixed physical/
commitment evidence fail closed. (3) Budget witness phải chứng minh
`after = before + delta > limit`; exact recorded-witness clearance là
`requiredLimit = after`, additive là `after - limit`. (4) Lock witness chỉ có categorical
`disableRecordedRuleForDimension`, không numeric amount. (5) Candidate không ở C1
selected/pruned evidence mang `absentRetainedOrOmittedNotRecorded`. (6) Mọi record giữ
`candidateFeasibilityAfterClearance: notEvaluated`, `recordedWitnessOnly` và
`descriptiveNotCausal`.

**Evidence:** 40 records/41 links: 33 commitment-pruned, 7 absent/not-recorded, 1
selected-by-C1. Có 28 drop-ETA budget clearances và 5 final-confirmation pickup-ETA
lock clearances. Numeric additive min/median/max 10.128/93.060/301.765 ms. Canonical
output 70.531 byte SHA-256
`cdd9a28dd12b91253aa4f848e074d3563312bd0cc13569bc98f17898f739e411`;
schema `7834f04e…8fb1c`, calculator `1ee0abdc…5acff`. Targeted 11/11, pinned
Python 146/146, independent verifier và required Debug 856/856 pass.

**Consequences:** `RB-WP13-005 Done`, `RB-WP13-006 Ready`. Không chọn budget mới từ
distribution này, không nói 33 candidates được cứu và không gán bảy missing links cho
ranking/cap/omission. `006` chỉ được phân loại theo evidence support; `007` giữ missing
field report và quyết định evidence vNext.

### ADR-058 — 2026-08-24 — Accepted

**Context:** Exact behavioral comparator và recorded-witness report có cùng 40
panel/unit records nhưng một pair có thể chứa nhiều candidate links/classes. Immediate
accepted-count relation không phải downstream completed service; evidence co-occurrence
không phải causal attribution.

**Decision:** (1) Bind exact `004`/`005` file, source và schema identities trước merge.
(2) Classify candidate links bằng typed evidence classes: budget, lock, physical,
shared-selected, ranking/search omission indeterminate hoặc unsupported. (3) Aggregate
ở pair-level multi-label và cross-tab với signed immediate acceptance relation; không
ép thành partition. (4) Missing portfolio luôn giữ
`rankingOrSearchOmissionIndeterminate`. (5) Mỗi record khóa
`causalAttribution: notEstablished`, `downstreamTrajectory: notEvaluated`; claim
boundary là post-outcome exploratory/descriptive only. (6) Raw record count và unique
inventory đều phải đúng 40 để duplicate dư không bị map projection che mất.

**Evidence:** 40 records/41 links. Pair-level occurrences: budget 28, lock 5,
indeterminate 7, shared-selected 1, physical/unsupported 0. C1-lower cross-tab có 7
budget + 1 lock; equal có 21 budget + 4 lock + 7 indeterminate; không C1-higher.
Canonical output 44.745 byte SHA-256
`bcc6bed3b1dd8d9c280d7a09125b6fe2e4508eb40bd47ae4da1e2c2fb9f9e9eb`;
schema `060ef7d0…249d`, classifier `bf11f7e1…8b14a`. Targeted 8/8, pinned Python
154/154, independent non-importing verifier và required Debug 856/856 pass.

**Consequences:** `RB-WP13-006 Done`, `RB-WP13-007 Ready`. Không suy budget/lock causal
shares, recovered completions hay candidate feasibility. `007` phải chỉ inventory
option-set covariates thực sự recorded và phát missing-field report; Runner evidence
vNext chỉ được mở nếu evidence gap được chứng minh.

### ADR-059 — 2026-08-24 — Accepted

**Context:** H6 v1.0.0 ghi generation/vehicle-loss/prune/selection counters nhưng không
ghi full retained candidate portfolio. Aggregate equality có thể loại một số omission
explanations, nhưng không chứng minh candidate identity equality hoặc ranking position.

**Decision:** (1) Bind exact `003`–`006`, scan 80 raw target decisions tới EOF và chỉ
emit typed count/work/omission/status covariates. (2) `generationComplete` dùng đúng
recorded completeness counters; cap/work/repair/request omissions và conservation phải
fail closed. (3) Exact generation-object equality vẫn mang
`candidateIdentityEquality: notEstablishedByAggregateEquality`. (4) Field catalog tách
`recordedCountOnly`, `notRecorded`, `notEvaluated`. (5) Evidence vNext được yêu cầu chỉ
cho candidate-level portfolio/replay questions; nó không authorize rerun và cấm H6
backfill.

**Evidence:** 40/40 pairs có exact-equal generation evidence; 80/80 arm-epochs complete,
zero cap/work/generation/selection omission. Generator-retained counts bằng B1/C1:
Panel A 254/254, Panel B 136/136. C1 có thêm 46 commitment-pruned candidates và 390
validation work units; mọi generation deltas zero. Bảy unresolved links ở bảy pairs
vẫn `notRecorded`. Canonical output 221.925 byte SHA-256
`d71c669bb6da0648ccb9c5a6eaa16d990a152a9ebcd7bf0246b0b251a4037258`;
schema `d043d814…d634`, analyzer `85cf42e9…eb75`. Targeted 10/10, pinned Python
164/164, independent 80-target raw verifier và required Debug 856/856 pass.

**Consequences:** `RB-WP13-007 Done`, `RB-WP13-008 Ready`; O-009 closed. `008` được
thiết kế/version retained-portfolio evidence vNext cho exploratory execution tương lai,
không thay protocol/H6 receipt lịch sử. `009` vẫn chưa được phép chạy trước freeze riêng.

### ADR-060 — 2026-08-24 — Accepted

**Context:** ADR-059 chứng minh H6 chỉ đủ count-level và không ghi exact retained
candidate routes/objectives hoặc complete selected set. Evidence mới phải đủ cho
candidate-level replay nhưng không được đổi default Runner output hay backfill H6.

**Decision:** (1) Dùng explicit config profile `retained-portfolio-v1`, chỉ với
solver-backed path và base solver evidence. (2) Profile vắng giữ execution evidence
v1.1.0; profile bật dùng v1.2.0; historical v1.0.0 vẫn decode. (3) Capture defensive-
copy của generated physical set, identity-preserving eligible subset, exact
`CandidateSelectionProblem` và exact feasible selected candidate IDs. (4) Ghi full
route/remaining schedule, eligibility và ordered objective contributions; không tạo
objective vector cho pruned candidate. (5) Strict decoder/schema phải reconcile count,
ordering, vehicle/no-op, selected/request, objective và route/schedule invariants.
(6) Instrumentation không authorize simulator execution; `009` phải freeze toàn bộ
paired target inventory trước run.

**Evidence:** Schema 5.674 byte SHA-256 `1e159250…1bbe81`; mapper SHA-256
`75996041…d06027`. Capture-on/off differential giữ status/state/actions. Snapshot/
config/contract/schema mutations pass 4 targeted .NET + 4 schema tests; required
Debug 860/860, pinned Python 168/168, format/diff/static gates pass. Report:
`benchmarking/wp13-008-runner-retained-portfolio-evidence-2026-08-24.md`.

**Consequences:** `RB-WP13-008 Done`, `RB-WP13-009 Ready`. H6/WP9 conclusions,
receipts và margins không đổi; không external result artifact được tạo ở `008`.

### ADR-061 — 2026-08-24 — Accepted

**Context:** ADR-060 cho phép opt-in retained-portfolio evidence nhưng không authorize
outcome-selected rerun, H6 backfill hoặc mechanism claim. E1 cần khóa exact toàn bộ
paired target set, runtime/source/Runner/config/schema identity, output namespace,
resource envelope và failure treatment trước khi tạo raw evidence mới.

**Decision:** (1) Freeze đủ 40 pair/80 arm B1/C1 ở cả Panel A/B, seed 7, không subset
theo outcome; configs chỉ thêm profile `retained-portfolio-v1`. (2) Bind 40 drivers,
scenario/derivative trees, source/runtime/executable/published Runner hashes, new
output roots, H6 forbidden roots, repeats 1, parallelism tối đa 4, timeout/line/stderr
caps trong source-controlled receipt. (3) Staged selection chỉ được chọn full pair;
success reuse phải independent-verify, failure không retry/replacement. (4) Ticket
chỉ inventory exact bundle/v1.2 coverage; candidate comparison và mechanism chờ
`010`/`011`. (5) H6 roots, gate, margins và negative result không đổi.

**Evidence:** Freeze receipt 15.575 byte SHA-256 `9fcf2193…a4411`. Panel A/B mỗi
panel hoàn tất 40/40 arm, zero failure, cùng repository inventory `22f4914e…f6afb`.
Independent canonical inventory 67.771 byte SHA-256 `a029b978…4674` đọc 80 bundle
tới EOF: 8.640 requests, 44.156 solver decisions, retained v1.2 coverage
44.156/44.156, 5.516.098.710 raw bytes. Targeted 22/22, required Debug 860/860,
pinned Python 181/181, format/diff/line gates pass. Report:
`benchmarking/wp13-009-exploratory-retained-portfolio-replay-2026-08-24.md`.

**Consequences:** `RB-WP13-009 Done`, `RB-WP13-010 Ready`. E1 là post-outcome
exploratory inventory, không có confirmatory gate, causal inference hoặc mechanism
conclusion; `010` phải hoàn tất falsification matrix trước `011` aggregation.

### ADR-062 — 2026-08-24 — Accepted

**Context:** E1 v1.2 đã complete nhưng chưa được phép aggregate candidate evidence nếu
portfolio corruption, artifact-binding drift hoặc instrumentation-induced behavioral
change chưa bị falsify độc lập. Receipt đầu 8.777 byte chỉ lật một boolean truncation;
bản 8.999 byte có 26 mutant nhưng chưa có typed codes và chưa phủ hết artifact/
selection boundary, nên cả hai được giữ làm superseded evidence.

**Decision:** (1) Dựng lại canonical E1 inventory byte-exact từ 80 raw bundle trước
mọi mutation. (2) Dùng exact 31 in-memory mutant ở 9 layer, bắt expected message,
typed rejection code và layer; unclassified/generic error là failure. (3) Phủ job,
summary, manifest, extra-file inventory, source, truncation/transcript/frame hash,
version/shape/candidate/eligibility/selection/objective/route/schedule invariants.
(4) So read-only cùng 80 E1 job với exact H6 same-arm behavioral projection; semantic
hash không phải equality key vì instrumentation/config bind state. (5) Chỉ khi 31/31
reject đúng code và 80/80 hành vi equal mới mở descriptive aggregation `011`.

**Evidence:** Closure receipt 13.515 byte SHA-256 `78bf6313…77785`: 80 bundle,
44.156 decisions/portfolios, 31/31 expected rejects, zero unexpected pass/failure.
E1↔H6 receipt 66.597 byte SHA-256 `4abb24f0…babfc`: 80/80 behavioral equal,
zero mismatch, 8.640 requests/44.156 H6 decisions; semantic hash equal 0/80 expected.
Targeted 6/6, required Debug 860/860 và full sequential pinned Python 187/187 pass,
zero skip; format/diff/JSON/Markdown/line gates pass. Report:
`benchmarking/wp13-010-e1-evidence-falsification-2026-08-24.md`.

**Consequences:** `RB-WP13-010 Done`, `RB-WP13-011 In progress`. `011` được phép
đọc verified E1 portfolio cho finite-panel descriptive aggregation; H6 gate/result,
WP10, causal/population boundary và policy v2 không đổi.

### ADR-063 — 2026-08-24 — Accepted

**Context:** `010` chứng minh E1 instrumentation hợp lệ nhưng H6 vẫn để bảy B1-selected
links ở trạng thái retained/omitted `notRecorded`. Candidate-level join phải loại
candidate ID và policy eligibility/objective khỏi semantic signature, đồng thời không
so raw vectors giữa rolling-cost và hard-vector hoặc cộng trajectory rows có cell
overlap như một decomposition.

**Decision:** (1) Verify lại exact 80 E1 bundle/44.156 decisions rồi lấy v1.2
portfolio tại 40 exact first-divergence pairs. (2) Join bằng vehicle/request/route/
schedule signature; candidate ID chỉ là drift evidence. Signature collision fail
closed. (3) Phân B1 actionful link thành C1 selected, eligible-not-selected,
pruned-with-recorded-witness hoặc absent. (4) Objective chỉ có within-arm/vehicle
descriptive ordinal; cross-profile numeric comparison bị cấm. (5) Aggregate pair rồi
panel, giữ H6 outcome `trajectoryAssociatedNotCausal`; association rows có thể overlap
và bị khóa `overlappingCellsNotAdditive`. (6) Không CI/p-value/counterfactual/service
decomposition hoặc confirmatory rescue.

**Evidence:** Closure report 116.985 byte SHA-256 `0eba293c…ddc1c`: generated set
equal 40/40, 390 signature/arm, zero collision/ID drift, 41 links = 33 pruned
(28 budget, 5 lock) + 7 eligible-not-selected + 1 selected + 0 absent. Mọi 8
C1-lower cell có prune occurrence nhưng 25 prune links khác ở equal-immediate cells;
4/7 eligible-not-selected có within-vehicle ordinal 1. Panel outcomes reconcile
`1735→1581` và `966→860`. Targeted 4/4, required Debug 860/860, pinned Python
191/191, format/diff/schema/JSON/Markdown/line gates pass. Report:
`benchmarking/wp13-011-e1-candidate-descriptive-aggregation-2026-08-24.md`.

**Consequences:** `RB-WP13-011 Done`, `RB-WP13-012 In progress`. Full source/logic/
claim audit là queue head duy nhất; WP14/policy v2 chưa được mở. H6 result và mọi
causal/population boundary giữ nguyên.

### ADR-064 — 2026-08-24 — Accepted

**Context:** Aggregate pass của `011` chưa đủ để đóng WP13. `012` phải đọc từng file
instrumentation/schema/analyzer/test/report, walk lại raw-to-report DAG và kiểm claim
text. Frozen E1 verifier còn một coverage gap: tự nó accept optional repaired-request
identifier sai kiểu/overlength và boolean objective level index, dù C# decoder/schema
đã reject. Mutate frozen source sẽ phá provenance, nhưng bỏ qua gap sẽ làm closure
verifier không độc lập.

**Decision:** (1) Source-control exact path/role/SHA inventory của 80 stable WP13
files và bind external canonical audit receipt. (2) Compose historical E1 verifier
với supplemental canonical-integer, optional-field và exact UTF-8 128-byte identifier
guard; giữ frozen verifier byte/hash bất biến. (3) Deep-verify H6/E1 raw authorities,
all active/superseded DAG edges, schemas, compact receipts, aggregate identities và
62-file E1 freeze inventory. (4) Scan architecture dependency/import graph và stable
claim documents; P0–P2 phải sửa + regression trước closure. (5) Giao ba limitation
P3 cho `013`: successor derived-output exclusivity, E1 retention/archive và versioned
successor verifier. (6) Audit không tạo scientific result, policy parameter hoặc
WP14 authorization.

**Evidence:** Source-controlled inventory có 80 file/1.076.880 byte, inventory SHA
`33d89c17…01c0c4`. External receipt 25.437 byte SHA `cd863988…fad76` deep-verify
100 H6 bundle/57.806 solver decisions và 80 E1 arm/8.640 requests/44.156 decisions/
portfolios; zero failed DAG edge và zero freeze-file mismatch. 45 Domain/Application
files, 115 analyzer imports và 12 stable claim documents được audit; 24 high-risk
term occurrences đều là caveat/prohibited, zero unsafe conclusion. P2 mutation
regression reject wrong-type/overlength identifier và boolean integer; unresolved
P0/P1/P2 bằng 0. Targeted 8/8, required .NET 860/860, pinned Python 199/199 và
format/diff/schema/JSON/Markdown/line gates pass. Report:
`benchmarking/wp13-012-full-source-logic-claim-audit-2026-08-24.md`.

**Consequences:** `RB-WP13-012 Done`, `RB-WP13-013 In progress`. H6/WP10 outcomes,
candidate aggregation boundary và frozen verifier provenance không đổi. `013` là
queue head duy nhất để xử lý ba P3 limitation và ra explicit WP13/WP14 decision;
ablation/policy v2 chưa active.

### ADR-065 — 2026-08-24 — Accepted

**Context:** `013` là exit gate của WP13. Ba limitation P3 từ ADR-064 chưa có
contract, và câu hỏi mở duy nhất còn lại là có mở WP14 hay không. Rủi ro là coi
"audit sạch" thành lý do mặc nhiên chạy ablation matrix, hoặc để evidence sufficiency
biến thành policy selection. H6 negative result (Panel A `−154`, Panel B `−106`) vẫn
là authority và không được rescue.

**Decision:** (1) Đóng WP13 bằng canonical closure manifest có strict schema, đánh giá
bảy exit gate độc lập: source/logic/claim audit, provenance, architecture, claim
boundary, raw immutability, resource readiness và WP14 separation. (2) Khóa contract
cho ba P3: successor artifact phải raw-root reject + complete verify + canonical
UTF-8/LF + exclusive create + explicit supersession; giữ và freeze toàn bộ
5.516.098.710 verified raw byte, không archive/delete trong WP13; successor verifier
phải versioned và giữ mọi historical check cộng regression vector tìm ở `012`.
(3) Verdict `openExploratoryAblationOnly`: WP14 chỉ được mở ở mức refinement để đọc
lại full-PDF boundary, refine factor/gate và lập ordered queue/freeze plan.
(4) WP14 bắt buộc development namespace/cells mới, loại H6 Panel A/B khỏi mọi
tuning/selection, freeze factor matrix/denominator/analyzer/resource envelope trước
outcome, báo paired service–burden Pareto frontier thay vì chọn bằng một scalar hậu
outcome, và fail closed nếu một ràng buộc không freeze được. (5) Không authorize H7,
lifecycle policy v2, H6 backfill hay causal/population inference.

**Evidence:** Closure manifest `benchmarking/evidence/wp13-closure-decision-v1.json`,
4.463 byte, SHA-256 `4e410e2311caa4073ed219ae57b47323e3159540001298a6a085cf0858a72c9c`;
strict schema `benchmarks/schemas/wp13/v1/closure-decision.schema.json` SHA-256
`a4cbfb1438b672f10ea976b9abfe56af10617de2ba37a96b178ac618c9bffaca`. Retention/successor
contract 3.751 byte SHA-256
`fe10a9bef62cffa8974c65900e561a56f26ab200cb4ba48c663e2d8bb91e656e`. Independent
closure/schema/hash/mutation tests 6/6 zero skip; required `dotnet test RideBound.slnx`
860/860 zero skip; full sequential pinned CPython/FleetPy 205/205 zero skip; exact
active/superseded artifact length/SHA resolution, canonical JSON, Draft 2020-12 schema
và format/diff/Markdown/link/line gates pass. Report:
`benchmarking/wp13-013-closure-decision-2026-08-24.md`.

**Consequences:** `RB-WP13-013 Done`; WP13 `001..013` Complete. `RB-WP14-001`
refinement là queue head duy nhất và chưa có WP14 implementation ticket. H6/WP10
outcomes, margin, panels và mọi claim boundary không đổi; ablation refinement không
phải quyết định chạy matrix.

### ADR-066 — 2026-08-24 — Accepted

**Context:** `RB-WP14-001` phải biến "được phép mở ablation" thành một thiết kế cụ
thể. Trước đó chưa ai đo objective profile của C1 hay hình dạng phân phối tiêu thụ
commitment, nên factor matrix sẽ phải đoán. Một probe read-only trên 80 bundle E1
v1.2 đã tái lập chính xác 10/10 con số H6 rồi trả lời bốn câu hỏi tách bạch: layer
nào cắt candidate, level lexicographic nào thực sự phân biệt, rider thấy gì so với
metric tính gì, và phân phối tiêu thụ có hình dạng nào.

**Decision:** (1) Nhận full-PDF evidence WP14 gồm Alonso-Mora et al. cùng
supplement, khảo sát ConVRP/GenConVRP của Lespay et al., và hai paper ride-pooling;
không lấy tham số số học nào từ chúng. (2) Loại bốn factor bằng phép đo chứ không
bằng lập luận: one-sided charging, net-displacement anchoring, và nới các dimension
switch/relocation — tất cả đều vô hiệu trên evidence đã ghi. (3) Nhận sáu factor
F1–F6 với cơ chế trong code, bảo chứng literature và điều kiện falsification riêng.
(4) Khóa ba ràng buộc cứng: không dùng số đo H6/E1 để chọn mức factor, báo frontier
hai trục thay vì scalar hậu outcome, không rescue H6. (5) Ghi ba giới hạn claim mới
vào boundary: `worst-hard-utilization-ppm` là hằng số trong cấu hình đã đo nên C1
không "xếp hạng theo mức tiêu thụ commitment"; attribution `lock/ranking` của WP9
thực chất là lock; và burden giảm 99,83% là attributed, còn experienced là 88,85%.
(6) Ordered queue `RB-WP14-002..014`, với `002` là tối ưu solver trung tính giữa
hai arm vì nó là điều kiện tài nguyên của matrix.

**Evidence:** Probe `wp14_mechanism_probe.py` với 7 targeted test pass, bốn external
artifact canonical:
`mechanism-probe-b1a-v1.json` 2.151 byte SHA `3eb4cbb7…837e5d`,
`c1a` 2.480 byte SHA `03d7a80d…09f528f`,
`b1b` 2.159 byte SHA `c944fcb9…99d2bf`,
`c1b` 2.486 byte SHA `c98991fc…dba7c9`;
compact receipt `wp14-001-mechanism-probe-v1-summary.json` 5.907 byte SHA
`58816604…a5ba9620`. Đo được: commitment prune chỉ đến từ đúng hai nguồn
(`drop_eta_total_ms` budget 780+491, `pickup_eta_ms` final-confirmation lock
160+92), 8/10 dimension không sinh witness nào; 534+339 vehicle choice set bị gate
làm rỗng; 143+212 request bị chặn tức thời; B1 có **0** commitment prune ở cả hai
panel; 94,40–97,78% lexicographic level là thoái hoá; phân phối tiêu thụ lưỡng cực
với 76,8% bằng 0 và p95 = 234.222 ms. Full-PDF corpus 77/77 trang, inventory tại
`E:\RideBoundData\research\pdf-20260824-wp14`. Review:
`reviews/wp1-wp13-optimization-and-fairness/README.md`.

**Consequences:** `RB-WP14-001 Done`, `RB-WP14-002 Ready`. H6/WP10 outcomes, margin,
panels và frozen verifier provenance không đổi; probe chỉ đọc và không ghi vào raw
root. Ba giới hạn claim mới thu hẹp cách mô tả treatment nhưng không đổi kết quả.
WP15–WP20 vẫn roadmap-level.

### ADR-067 — 2026-08-24 — Accepted

**Context:** `RB-WP14-002` phải giảm chi phí solver để ablation matrix khả thi;
WP13-002 từng fail đúng ceiling CPU 120 s trên medium public drain với một cấu
hình. Đo trên evidence đã ghi, 94,40–97,78% lexicographic level là hằng số trên
toàn tập khả thi, nên pass của chúng chỉ chứng minh optimum của một hằng số. Khi
hiện thực mới phát hiện một ràng buộc chưa từng được ghi: `executionEvidence` nằm
**bên trong** hash projection của decision và chứa `consumedDeterministicTimeMicros`,
nên bớt pass tất yếu đổi `decisionHash`. Ticket ban đầu yêu cầu transcript
byte-identical, điều không thể đạt mà không bịa số đo.

**Decision:** (1) Thêm `CandidateSelectionProblem.ConstantObjectiveLevelValues` ở
tầng Application: một level là hằng số khi mọi option của mọi vehicle đóng góp cùng
giá trị, vì `sum` cho `Σ_v c_v` và `maximum` cho `max_v c_v` với mọi assignment khả
thi; overflow được báo là không hằng số để giữ nguyên đường xử lý cũ. (2) Adapter
OR-Tools bỏ pass cho level hằng số, ghi đúng optimum vào `objectiveBounds`, không
đưa ràng buộc vô hiệu vào model, và luôn để lại ít nhất một level được giải để vẫn
có incumbent. (3) Sửa contract của ticket: cờ `solverExecution.skipConstantObjectiveLevels`
là **opt-in, mặc định tắt**, allowed nhưng không required, nên mọi configuration đã
publish và mọi hash đã ghi không đổi. (4) Khi bật, bảo toàn action, candidate được
chọn, `stateAfterHash` và optimum mọi level; `consumedDeterministicTimeMicros` và
`detailCode` đổi một cách trung thực, `decisionHash` theo đó cũng đổi. (5) Không
claim wall-clock; chỉ claim số pass giảm, đại lượng tất định.

**Evidence:** Differential 64 seed trên production C1 mapping cho selected IDs,
objective values và mọi bound bằng nhau khi bật/tắt cờ. Unit 5/5 cho phát hiện hằng
số, adapter 6/6 gồm mutation guard và edge hierarchy toàn hằng số, config 1/1 cho
cờ vắng/true/false/sai kiểu. Toàn bộ 860 test cũ pass **không sửa một dòng**.
Required `dotnet test RideBound.slnx` 873/873 và pinned Python 212/212, zero skip;
`dotnet format --verify-no-changes` và `git diff --check` pass. Report:
`benchmarking/wp14-002-constant-level-skip-2026-08-24.md`.

**Consequences:** `RB-WP14-002 Done`, `RB-WP14-003 Ready`. H6/WP10/WP13 outcomes,
margin, panels và mọi frozen receipt không đổi vì mặc định tắt. Việc evidence nằm
trong hash được ghi lại như ràng buộc kiến trúc: mọi thay đổi tương lai làm đổi số
đo solver đều phải opt-in theo cùng khuôn mẫu này.

### ADR-068 — 2026-08-24 — Accepted

**Context:** `RB-WP14-003` mở witness set đầy đủ, `RB-WP14-004` dựng development
panel. Trong lúc thực hiện, hai ràng buộc provenance chưa từng được viết ra đã lộ
diện. Thứ nhất, bản đầu của `003` suy cờ thu thập witness từ profile
`retained-portfolio-v1`, đúng profile mà hai configuration E1 đã đóng băng khai
báo; nới nó tại chỗ sẽ đổi evidence và `decisionHash` của E1, phá freeze chain mà
không test nào bắt được. Thứ hai, `wp13_full_audit` bind historical H6 verifier qua
`git show HEAD:` và bind 62 frozen source file theo byte trên đĩa; một commit của
chủ repository lúc 23:07 làm HEAD dịch, và `RB-WP14-002/003` sửa
`Wp4RunnerConfiguration.cs` — cả hai đều nằm trong tập bị pin. WP13 đã đóng và WP14
được phép phát triển cùng cây mã, nên hai binding đó không thể giữ nguyên dạng cũ.

**Decision:** (1) Witness set đầy đủ dùng profile **riêng**
`retained-portfolio-full-witness-v1`; `retained-portfolio-v1` giữ nguyên ngữ nghĩa
vĩnh viễn, kèm regression đọc thẳng hai file config E1 để khoá tính chất đó.
(2) Historical verifier bind theo **commit cụ thể** `2d6791fb…97551` thay vì `HEAD`,
vì bất biến thật là “còn khôi phục được từ repository này”, không phải “bằng HEAD”.
(3) Thêm declaration `benchmarks/scenarios/wp13-e1/source-divergence-v1.json`: một
frozen source file chỉ được khác đĩa khi được khai báo ở đó kèm frozen hash, ticket
và lý do, **và** bytes gốc vẫn khôi phục được từ commit khôi phục đã pin; thiếu bất
kỳ điều kiện nào là fail closed. (4) Development panel `wp14-development-panel-v1`
gồm 16 cell trên 2018-11-12/13 với hai cửa sổ trong ngày; 2018-11-11 bị loại vì
verified inventory không có `_tt_factors.csv` cho ngày đó. (5) Leakage audit so bảy
trục với cả hai grid H6 và phải cho 0 giao trên tất cả.

**Evidence:** Development receipt `development-panel-audit-v1.json` 5.109 byte SHA
`f1c731f6…bfd099ba`; compact `wp14-004-development-panel-v1-summary.json`. 0 giao
trên cả bảy trục giữa 16 development cell và 40 frozen cell; development
2018-11-12/13 với frozen 2018-11-14…18. Hai mutation test chứng minh audit bắt được
leakage khi mượn `demandMemberPath` hoặc `scenarioId` của H6. Sáu test khoá
declaration divergence, gồm một test khẳng định mọi frozen file **không** được khai
báo vẫn byte-identical trên đĩa. Required `dotnet test RideBound.slnx` 880/880 và
pinned Python 225/225, zero skip. Reports:
`benchmarking/wp14-003-full-witness-set-2026-08-24.md`,
`benchmarking/wp14-004-development-panel-2026-08-24.md`.

**Consequences:** `RB-WP14-003 Done`, `RB-WP14-004 Done`, `RB-WP14-005 Ready`. E1
freeze chain vẫn đầy đủ: 61/62 frozen file byte-identical trên đĩa, file thứ 62
được khai báo và bytes gốc khôi phục được. H6/WP10/WP13 outcomes không đổi. Từ nay
mọi ticket sửa frozen source phải cập nhật declaration; declaration dài quá tám mục
là tín hiệu phải re-freeze thay vì tiếp tục nối thêm.

### ADR-069 — 2026-08-26 — Accepted

**Context:** `RB-WP14-008` phải freeze toàn bộ development ablation trước outcome.
Review bản nháp phát hiện manifest chỉ hash grid/config không đủ: analyzer, Runner,
runtime hoặc config có thể đổi sau freeze; generic bundle verifier cũng không tự
chứng minh bundle nằm dưới đúng job; analyzer dùng percentile floor bị off-by-one,
float evidence và silent duplicate overwrite. 160 test-run IDs vẫn có thể “pass”
trong khi các lỗi integrity/logic này tồn tại.

**Decision:** (1) Freeze exact 16 cells × 10 arms = 160 unique jobs, 108 arrivals/
job, 1.728 arrivals/arm, paired development cell, seed 7 non-replicate và
retain-typed-failure/no-retry/no-replacement. (2) Bind 46 repository files, whole
adapter/development-fixture/published-Runner trees, Runner DLL, FleetPy clean commit,
CPython capability probe và whole .NET 10.0.9 runtime tree. (3) Exact bốn H6/E1
forbidden roots; ancestor/descendant overlap đều fail. (4) Freeze resource-only
envelope từ E1 Panel A: 40 jobs, median 971.998 ms, projection 15,6 GB/11 giờ p4,
minimum 25 GiB, reserve 5 GiB, max 20 GiB, job 2.700 s, matrix 57.600 s. (5) Matrix
runner verify canonical receipt before launch, hash config/driver/scenario, verify
bundle độc lập rồi bind label/job/scenario/repeat; derived summary exclusive-create.
(6) Analyzer verify/read EOF từng bundle, reject duplicates, dùng integer-canonical
completion/median, nearest-rank p95, tách pickup improvement/worsening, báo per-rider
tail và Pareto dominance trên đúng hai trục, không scalar/ranking. (7) Browser + PDF
QA xác nhận bản Alonso-Mora et al. MIT CSAIL là full text 6/6 trang; không copy số
paper làm default.

**Evidence:** Receipt
`benchmarks/scenarios/wp14-development/freeze-receipt-v1.json`, 101.719 byte,
SHA-256 `1ce26ff0f7d87c30d050e57107ad3e118af7f4b88fe04e62e48376ab34c37a55`,
verify canonical `jobs=160`, `repositoryFiles=46`, status pass. Targeted/mutation
17/17; required `.NET` 908/908; full pinned CPython/FleetPy 242/242; zero skip.
1.639 non-negative JSON/81 Draft 2020-12 schemas, format/diff/Markdown/link/line
gates pass. Report:
`benchmarking/wp14-008-development-ablation-freeze-2026-08-26.md`.

**Consequences:** `RB-WP14-008 Done`, `RB-WP14-009 Ready`; `006/007` vẫn Deferred.
Mọi outcome WP14 phải đi qua receipt này; source đã bind không được sửa sau run đầu.
`009` chỉ paired dry-run/resource gate. H6/WP10/WP13 outcomes, margin, panels và raw
roots không đổi; H7/policy v2/WP15 chưa được authorize.

### ADR-070 — 2026-08-26 — Accepted

**Context:** `RB-WP14-009` chạy đúng paired B1/C1 dry-run đã freeze trước khi được
phép mở matrix 160 job. B1 hoàn tất và pass independent verifier. Khi C1 đang ghi
transcript, session bị chấm dứt ngoài đường timeout có kiểm soát; nguồn chấm dứt
không được process ghi lại. C1 còn partial transcript 83.364.599 byte và thiếu
`bundle-manifest.json`. Freeze v1 quy định
`retainTypedFailureNoRetryNoReplacement`, nên không được xóa, chạy lại hoặc thay
thế partial bằng một observation thuận lợi hơn.

**Decision:** (1) Recovery chỉ verify B1 thành `reusedVerified`, reject C1 partial
và ghi summary schema-valid `1 completed / 1 failed`; không re-execute simulation.
(2) Đóng `RB-WP14-009` bằng verdict `resourceGateFailedClosed`, không đánh dấu Done.
(3) `RB-WP14-010..014` không được authorize dưới freeze v1; `006/007` vẫn Deferred.
(4) B1-only projection 20.037.964.320 byte/8,39 giờ p4 chỉ là kiểm tra trần bảo thủ,
không thể pass paired gate và không phải speedup claim. (5) Không đọc hoặc dùng
outcome completion/burden để sửa factor, denominator hay freeze. Một successor nếu
có phải được owner cho phép bằng protocol/ADR mới, không được thay thế receipt v1.

**Evidence:** Source-controlled summary
`benchmarking/evidence/wp14-009-paired-dry-run-resource-gate-v1-summary.json`;
external recovery summary `E:\RideBoundData\wp14\dryrun-summary-v1.json`, 2.077 byte,
SHA-256 `acc521bc5556ed2ed0f2523bcc889e7513ff6b3aad911551e2acd165ac9a4cc7`,
Draft 2020-12 pass. B1: 755.379 ms, 824 decisions, transcript 125.230.809 byte,
bundle 125.237.277 byte; independent verifier pass với behavioral hash và audited
solver evidence. C1 partial được giữ nguyên, không retry/replacement. Report:
`benchmarking/wp14-009-paired-dry-run-resource-gate-2026-08-26.md`.

**Consequences:** WP14 freeze v1 dừng fail-closed tại `009`; full matrix/frontier
không tồn tại và không được suy diễn. H6/WP10/WP13 outcomes, ADR-069 freeze receipt,
46 bound repository files và bốn forbidden raw roots không đổi.

### ADR-071 — 2026-08-26 — Accepted

**Context:** Owner yêu cầu tiếp tục review/tối ưu/benchmark theo ordered ticket process
sau khi WP14-v1 dừng ở resource gate. Source audit cho thấy
`wp14_run_matrix.py` dùng `subprocess.run(capture_output=True)` và chỉ publish log sau
child return/controlled timeout; outer launcher termination có thể để partial output
nhưng không log/summary. File này đã nằm trong 46-file freeze, nên sửa tại chỗ sẽ phá
freeze. In-app Browser và PDF QA đã đọc toàn bộ Kalibera & Jones 2013 (12 trang) và
Mytkowicz et al. 2009 (12 trang): variation cần tách theo experimental level; repeated
same setup không loại systematic setup bias; setup randomization/intervention hỗ trợ
methodology nhưng không cho phép retry hậu outcome.

**Decision:** (1) Mở work package riêng `WP14R` cho resilient execution mechanics;
WP14-v1/ADR-070 vẫn terminal FAIL CLOSED và WP15/H7 chưa mở. (2) Recovery contract
v1 cố định một initial cộng tối đa một recovery attempt; attempt không phải experimental
unit, giữ mọi bytes, không overwrite/delete, không retry sau independently verified
pass và không attempt 3. (3) Recovery authorization chỉ đọc mechanical validity, cấm
đọc completed/burden/route outcome. (4) Ordered gates là ledger → supervisor/
incremental evidence → fault injection/process tree → no-outcome dimensioning →
independent verifier → explicit freeze-v2 decision. Scientific tickets `008..012`
unauthorized trước decision `007`. (5) Không sửa 46 file freeze-bound hoặc raw H6/E1/
WP14-v1; mọi artifact mới dùng namespace `wp14r`. (6) Không copy repetition count,
CI recipe hoặc numeric default từ paper; paper chỉ định hình measurement protocol.

**Evidence:** Research note
`research/wp14r-resilient-benchmark-full-pdf-evidence-2026-08-26.md`; local PDFs SHA
`b50fb850…dbab` và `67505bfc…2d6`, 24/24 trang extract/render. `RB-WP14R-002` thêm
hai strict Draft 2020-12 schemas, `wp14r_attempt_ledger.py` và 13 state/tamper tests.
Review source sửa partial write, inventory race, pre-publication recovery-binding poison,
renamed-entry escape và process/verifier semantic inconsistencies. Targeted 13/13,
pinned Python/FleetPy 255/255 zero skip, required .NET 908/908 zero skip. Report:
`benchmarking/wp14r-002-immutable-attempt-ledger-2026-08-26.md`.

`RB-WP14R-003` tiếp tục quyết định này bằng strict journal/report schemas và
`wp14r_supervised_process.py`: command/env values chỉ vào hash binding, raw stream giữ
bounded theo chunk, monotonic timeout/heartbeat, Windows Job Object/POSIX process group,
verified partial prefix và exit-zero-awaits-verifier boundary. Targeted WP14R 28/28,
full Python 270/270, .NET 908/908. Report:
`benchmarking/wp14r-003-supervised-process-evidence-2026-08-26.md`.

`RB-WP14R-004` thêm durable launch intent, supervisor PID, strict recovery receipt,
hard-kill harness và stale-open recovery. Actual Windows matrix đi qua 11 supervisor
point cùng receipt→terminal crash window; incomplete/ambiguous/uncertain tree exhaust,
proven-safe tree mới authorize đúng một recovery, exit-zero vẫn await independent bundle
verifier. Targeted WP14R 39/39, full Python 281/281, .NET 908/908. Report:
`benchmarking/wp14r-004-hard-crash-recovery-evidence-2026-08-26.md`.

`RB-WP14R-005` thêm strict mechanics dimension report/schema/tool, fixed eight-cell
fake-child corpus và process-level telemetry. V1 resource pilot giữ sáu large timeout;
v2/v3 48/48 pass. Source review từ v2 cache compiled schema validator, bind schema SHA
trong journal mới và full schema tree trong report v3; legacy branch vẫn verify và wrong
provenance fail closed. Fixed 8 MiB verifier peak `65,875,968` B pass 256 MiB envelope;
large launcher/verifier median observed `71.30/35.39 s` → `4.68/1.96 s`. Targeted
WP14R 50/50, full Python 292/292, .NET 908/908. Report:
`benchmarking/wp14r-005-mechanics-resource-dimension-evidence-2026-08-27.md`.

`RB-WP14R-006` thêm strict independent report/fixture/mutation schemas cùng verifier
read-only không import/call ledger writer, supervisor verifier hoặc recovery classifier.
Ba valid fixture pass và 15/15 unique mutation class bị bắt đúng typed code. Pilot
actual junction phát hiện Python 3.10 thiếu `os.path.isjunction`; implementation hiện
kiểm Windows reparse point ở writer/supervisor/verifier. Review còn đóng hai TOCTOU
window giữa streamed journal/inventory và output-tree enumeration. Authoritative fixture
receipt SHA `1e4a6450…372104`, mutation report SHA `9d8aacf4…72e1e`; targeted 64/64,
full Python 306/306, .NET 908/908. Report:
`benchmarking/wp14r-006-independent-verifier-evidence-2026-08-27.md`.

**Consequences:** `RB-WP14R-001..006 Done`; chỉ `007 Ready`. Resource result vẫn chỉ
cho fixed corpus trong một Windows host session, không CI/SLA/population/two-stream-cap
claim. Native POSIX fault run vẫn thiếu; verifier độc lập theo implementation/AST nhưng
không theo team/organization. Failure ở successor gate vẫn dừng fail-closed; không chọn
best attempt và không thay receipt/partial WP14-v1. Scientific `008..012` Unauthorized.

### ADR-072 — 2026-08-28 — Accepted

**Context:** Mechanics `RB-WP14R-002..006` đã pass nhưng source audit của decision
gate `007` tìm thấy hai điều chưa đủ để ký freeze: supervisor fingerprint chưa bind
power source/scheme/quiescence, và ledger yêu cầu recovery giữ cùng `commandSha256`
trong khi đường dẫn trực tiếp `attempt-01/output`/`attempt-02/output` sẽ đổi command.
Revalidation .NET cũng fail CPU gate hai lần trước build-server shutdown rồi pass cùng
source/ceiling sau shutdown. In-app Browser/PDF QA đọc đủ Stabilizer 10/10 trang;
paper xác nhận repetition cùng một setup không loại systematic setup/layout bias,
nhưng runtime-layout randomization, số run và ANOVA recipe của paper không thể copy
sang RideBound.

**Decision:** (1) Authorize canonical `wp14r-resilient-development-v2` receipt SHA
`6b340108…a31237`, strict Draft 2020-12, tham chiếu byte-exact scientific freeze v1
SHA `1ce26ff0…37a55` thay vì định nghĩa lại arm/cell/factor. (2) Bind 24 protocol/
schema/test source files, current provenance của `002..006`, three authoritative
mechanics artifacts, pinned runtime và ba full PDFs/34 trang. (3) Dùng fixed wrapper
command không chứa attempt path; wrapper suy output từ immutable current open-attempt,
nên recovery command/env/cwd hash giữ nguyên nhưng output không overwrite. (4) Exact
host gate là Windows fingerprint `efebacc0…c6f81`, AC online, Balanced GUID
`381b4222-f694-41f0-9685-ff5bb260df2e`, 10×1 s CPU mean ≤20%/single ≤60%, available
memory ≥8 GiB và free disk ≥25 GiB; không ghi arbitrary process name/command line.
(5) Paired gate chạy tuần tự đúng B1→C1 frozen IDs, cần 2 valid/0 failed, không đọc
outcome; full 160-job matrix chỉ mở bằng canonical paired-gate pass và exact job order.
(6) Owner authorization dựa trên explicit persistent goal tiếp tục WP14R benchmark,
nhưng mỗi scientific launch vẫn cần current preflight pass.

**Evidence:** Receipt
`benchmarks/scenarios/wp14r-development/freeze-v2-authorization.json`; report
`benchmarking/wp14r-007-protocol-freeze-v2-authorization-2026-08-28.md`; research note
`research/wp14r-freeze-v2-full-pdf-evidence-2026-08-28.md`. New tests 31/31 và toàn
targeted WP14R 95/95 pass; freeze v2/v1 rebuild exact. Actual no-outcome preflight
receipt SHA `642b23ef…cd622` ghi CPU mean `11.790%`, max `14.656%`, memory/disk đạt,
nhưng power `offline`, nên typed `POWER_SOURCE_NOT_AC`; zero attempt và zero B1/C1.

**Consequences:** `RB-WP14R-001..007 Done`; `008 Ready` về protocol nhưng launch bị
frozen host precondition chặn cho tới khi AC online. Không tự đổi scheme, hạ threshold
hoặc tiêu thụ attempt để “thử”. `009..012`, WP15 và H7 vẫn Unauthorized. WP14-v1,
H6/E1 và mọi retained failure/pilot không đổi; authorization/preflight không phải
effectiveness, speedup, population hay between-host evidence.

### ADR-073 — 2026-08-29 — Accepted

**Context:** `RB-WP14R-008` dừng FAIL CLOSED sau khi child không verify được chính
receipt mà parent vừa verify. Nguyên nhân là defect tất định của freeze v2:
`host_fingerprint()` gồm `platform.machine()`, mà trên Windows hàm này đọc
`PROCESSOR_ARCHITECTURE` — biến không nằm trong `inheritedEnvironmentNames`. Đo khép
kín: receipt yêu cầu `efebacc0…c6f81`, parent env đầy đủ cho đúng giá trị đó, child
theo allowlist cho `85e172e3…48f8`, child cộng thêm biến đó cho lại `efebacc0…c6f81`.
Sửa tại `wp14r_supervised_process.py` là **bất khả thi**: file này bị
`validate_mechanics_gate_provenance` khoá theo fixture của gate `006`, nên đổi nó sẽ
phá luôn bằng chứng mechanics. Chủ nghiên cứu uỷ quyền chọn phương án tốt nhất.

**Decision:** (1) Tạo freeze v3 tự chứa `wp14r_freeze_v3.py` + schema
`benchmarks/schemas/wp14r/v3/`; giữ `wp14r_freeze_v2.py` **byte-identical** để
authorization v2 và failure của nó còn verify được từ source. (2) v3 đổi đúng ba thứ:
thêm `PROCESSOR_ARCHITECTURE` vào allowlist (qua đó kiến trúc trở thành một term được
hash trong command binding thay vì suy ra ngầm); ledger/control root mới
`development-v3-*`; và đưa hai root v2 vào forbidden set để v3 không bao giờ ghi đè
attempt đã exhausted. Thiết kế khoa học **không đổi**: cùng base freeze v1 byte-exact,
cùng 160 job, cùng paired gate, cùng host threshold. (3) `read_freeze` chọn builder
theo `freezeId` của chính receipt thay vì hardcode v2; an toàn vì builder được chọn
sau đó dựng lại toàn bộ receipt và so sánh, nên một receipt khai gian chỉ chọn đúng
verifier sẽ từ chối nó. (4) Thay đổi `wp14r_scientific_protocol.py` được khai báo
trong `source-divergence-v2.json`, kèm archive bytes đã freeze; bytes gốc được tái
dựng và **khớp exact** `36b4ad3d…631dd`, 44.221 byte. (5) Job v2 đã exhausted
**không** được hồi sinh; freeze v2 vẫn terminal FAIL CLOSED.

**Evidence:** Receipt `benchmarks/scenarios/wp14r-development/freeze-v3-authorization.json`
SHA `07baeda2b79f31b5d79318755afbe917b7a8a47a7509b3f09a1756a484fa9227`, 160 jobs,
strict Draft 2020-12. Regression còn thiếu ở v2 nay có trong
`test_wp14r_freeze_v3.py`: `verify_receipt` chạy trong subprocess với **đúng** allowlist
của child, và host fingerprint dưới allowlist phải bằng fingerprint của parent và bằng
giá trị trong receipt. Report:
`benchmarking/wp14r-008-paired-gate-fail-closed-2026-08-29.md`.

**Consequences:** Freeze v3 authorized; `RB-WP14R-008R` là paired gate dưới v3.
Freeze v2 giữ nguyên bytes và vẫn là bản ghi terminal của một authorization không chạy
được. `009..012`, WP15 và H7 vẫn Unauthorized cho tới khi paired gate v3 pass. Không
đổi margin, panel, denominator, factor, arm set hay failure treatment; H6/E1/WP14-v1
không bị chạm.

### ADR-074 — 2026-09-24 — Proposed (thăm dò; nhánh `research/deadline-gate`; commit `65d867c`)

**Context:** Khung bốn họ cổng của luận văn (chương 3, Định lý thứ tự khả thi) dự đoán
rằng một hạn chót tính từ lời hứa **đầu tiên** --- một chiều (`M+`) hoặc hai chiều (`M`)
--- có thể tự nó làm rỗng tập phương án dưới trôi dạt, còn cổng hành vi thì không. Kiểm
điều đó cần một luật hạn chót trong Runner. Đây là Tầng 2 của kế hoạch nghiên cứu ngoài
kho (`E:\Code\Report_INT3508\KE-HOACH-HOP-NHAT-2026-09-23.md`), chạy ở chế độ **thăm dò
có đối chứng và tiền đăng ký**. `RB-WP14R-009..012`, WP15 và H7 vẫn Unauthorized; ADR
này không authorize chúng. Không claim tính mới cho hạn chót: Schulz và Pfeiffer (2026)
đã có hạn chót cưỡng chế được tính từ lời hứa đầu.

**Decision:**
1. `CommitmentPolicy` thêm `DropEtaDeadlineSlack` (`Duration?`, mặc định `null`) và
   `DropEtaDeadlineTwoSided` (mặc định `false`; `true` mà không có slack bị từ chối).
   Cấu hình: `dropEtaDeadlineSlackMs` (số nguyên dương) và `dropEtaDeadlineTwoSided`
   (boolean). Vắng hai khóa ⇒ luật tắt và bytes/hash cấu hình không đổi.
2. `CommitmentLockEvaluator` sinh witness `deadline_cap` khi `p − p0 > ℓ` và, nếu hai
   chiều, `deadline_floor` khi `p0 − p > ℓ`, với `p0` là lời hứa đầu (`Entries[0]`), **không**
   phải mốc ngoại sinh; vì vậy riêng trôi dạt cũng làm bắn được, kể cả trên no-op.
   Witness này được **nối sau** các witness khóa, để lý do đầu tiên của một prune vẫn là
   khóa khi cả hai cùng bắn. Validator ánh xạ sang `COMMITMENT_DEADLINE_EXCEEDED` ở stage
   `Lock`. Entry đầu chỉ được đọc khi có hạn chót; nếu nó không phải `InitialPromise` thì
   ném `InvalidOperationException` (bất biến sổ cái bị phá, không phải một phương án bị loại).
3. Thay đổi chẩn đoán: `HardVectorCandidateAssessor.AssessAndFilter(..., requireSafetyNoOp)`,
   **chỉ** `SolverBackedRidePoolingPolicy` bật. Khi bật: (a) một xe mất no-op trong khi còn
   phương án khác fail ngay với `COMMITMENT_SAFETY_NOOP_REJECTED` kèm lý do loại của chính
   no-op (trước đây trạng thái này chết muộn hơn ở `CandidateSelectionModel` với lỗi mapping
   chung, vì mô hình chọn đòi đúng một no-op mỗi xe); (b) thông báo fail-closed trích
   `No-op rejection:` thay cho `First rejection:` (ứng viên đầu theo thứ tự mã). Mã
   `COMMITMENT_SAFETY_NOOP_REJECTED` báo sự kiện; lý do được trích mới nói nguyên nhân, có thể
   thuộc stage bất kỳ. `RideBoundHardVectorPolicy`/`CommitSoftHardHybridPolicy` không bật cờ.
4. B1–B4 không bao giờ nhận hạn chót: `MechanismCommitmentPolicyProvider` dựng lại policy
   không có hai tham số mới (pin bởi `EffectivePolicyFairnessTests`).

**Evidence:** worktree `E:\Code\RideBound-deadline` tại `41a4e11` + diff, sau đó commit `65d867c`
(bản lưu `ridebound-scratchpad/deadline/review-src.diff`, `review-tests.diff`). Debug
**963/963** (log `after-review-fix-2.log`); lần chạy sau khi siết một assertion test:
962/963, chỉ drain medium fail với `resource.cpu-time-exceeded` như baseline
(`BASELINE-2026-09-24.md`). Review độc lập hai vòng: vòng 1 tìm 2 P1 (hai chính sách public
đổi hành vi; witness fail-closed đổi) cùng P2/P3; vòng 2 không còn P0/P1, xác nhận bằng probe
rằng chính sách không solver-backed byte-identical khi không có hạn chót và không quyết định
thành công nào của đường solver-backed đổi khi không có hạn chót. Mutation: neo vào lời hứa
hiện tại thay vì lời hứa đầu ⇒ 2 test Application đỏ; tắt cờ ở đường solver-backed ⇒ 1 test
Algorithms đỏ. +12 test (Domain 3, Application 2, Algorithms 4, Runner 3).

**Consequences và giới hạn đã biết:**
- Ở đường solver-backed, chỉ **run vốn đã hỏng** đổi thông báo (nội dung trích, candidate id,
  xe nào được báo trước). So sánh đối chứng dùng nhãn, không dùng text hay candidate id; nhãn
  của một ô có thể đổi giữa `commitment-all-rejected/*` và `all-rejected/non-commitment` vì bộ
  chạy cũ trích ứng viên đầu còn bộ chạy mới trích no-op.
- Mã typed không tới artifact: `AssessmentFailure` thay code bằng `CommitmentAssessmentFailed`
  và `RunnerSession` chỉ giữ `INTERNAL_ERROR` + message, nên phân loại dựa trên văn bản; bộ
  phân loại ngoài kho kiểm mọi hằng số chuỗi với mã nguồn.
- Đếm prune do hạn chót phải quét `commitmentWitnesses`, không dùng `code`.
  `wp14_frontier_analyze.py` và `wp14_mechanism_probe.py` bỏ qua im lặng mã mới;
  `wp13_recorded_witness_relaxation.py` và schema của nó từ chối — không dùng chúng trên output
  có hạn chót.
- Hạn chót cũng áp trên đường OnlineCommitment không có WP4 (chưa test); mọi run Tầng 2 truyền
  `--wp4-config`. Witness hạn chót không mang độ lớn (`ℓ`, `p0`, `p`).
- Các run có trôi dạt không qua verifier dự án (verifier đòi đúng một snapshot) ⇒ mọi kết quả
  Tầng 2 mang nhãn thăm dò.

**Kết quả Tầng 2 (thăm dò có tiền đăng ký, 2026-09-24).** Runner
`E:\RideBoundData\research\runner-deadline-v1` (tree SHA-256 `8fa5dd28…61cda8`); output
`E:\RideBoundData\research\deadline-v1` (301 thư mục); hồ sơ `E:\Code\Report_INT3508\ridebound-scratchpad\deadline\`
(`PREREGISTRATION.md` + `predictions.csv` niêm phong 06:03:52Z, `AMENDMENTS.md` A1–A6,
`analysis-2026-09-24-final.txt`). Đối chứng 40/40: 36 ô hoàn tất trùng từng quyết định với
`wp14/drift-v1`, cổng kết quả vô nghiệm đúng 4 ô đã biết. Bất biến: 0/56 lần chạy cổng hành vi vô
nghiệm do cam kết; 0 `selection-noop-invariant`. Dự đoán theo ô: panel chính 98/102, tập giữ lại sáng
31/32, tối 40/40; hai ngưỡng xác nhận đã niêm phong đạt, không điều kiện phủ định nào xảy ra. Sáng
`k = 1`: hạn chót một chiều 30/60/120 s vô nghiệm 19/13/3 trên 20 ô; `k = 0`: 0/60. Tối (tập giữ lại):
một chiều 0/8, hai chiều 7/8 (đều ở cận dưới), cổng kết quả 8/8. Cả 90 lần vô nghiệm do cam kết trích
lý do của chính no-op. Năm lần chạy hỏng vì máy ngủ được chạy lại theo A2 (A2 làm độ chính xác giảm
từ 99/102 xuống 98/102). Kiểm độc lập bằng tính lại từ artifact thô: mọi con số khớp. Dự đoán phụ
"hạn chót một chiều phục vụ nhiều khách hơn khi tắc giảm" **không** được quan sát (1/8 ô, +1 khách).
Không claim tính mới cho hạn chót; không nói cơ chế tốt hơn Schulz & Pfeiffer.

### ADR-075 — 2026-09-24 — Proposed (thăm dò; nhánh `research/tier3-failure-aware`; commit `8d1ea4d`)

**Context:** Ở Tầng 2 (ADR-074), cả 90 lần vô nghiệm do cam kết đều xảy ra vì chính no-op bị một
cổng loại, và khi đó phiên chạy chết với `INTERNAL_ERROR`. T3.3 của kế hoạch ngoài kho
(`KE-HOACH-HOP-NHAT-2026-09-23.md`) đòi: no-op là *tham chiếu vật lý*, không bắt buộc hợp lệ; vô
nghiệm do cổng là trạng thái có kiểu. Ngày 2026-09-24 chủ nghiên cứu chọn **"Giữ tuyến + ghi vi
phạm"**: xe giữ tuyến hiện tại như một hành động bắt buộc; lời hứa theo tuyến giữ vẫn được công bố;
sổ ngân sách được cộng như mọi lần công bố và **không bao giờ reset**; một vi phạm có kiểu được
ghi. Các định lý của luận văn chỉ phủ phần không phục hồi. Không claim tính mới (giữ kế hoạch cũ
khi không còn phương án là thông lệ). ADR này không authorize `RB-WP14R-009..012`, WP15 hay H7.

**Decision:**
1. Domain: thêm `CommitmentBreachKind.ForcedReference` (cuối enum) và
   `CommitmentBreachRecord.CreateForcedReference`. Bất biến: không incident, không witness dịch
   vụ; projection tuyến giữ trùng projection ngoại sinh; decision delta bằng 0 và exogenous
   bằng visible; `attemptedBudgetAfter` bằng `before + decisionInduced` hoặc
   `before + visible` (theo basis); mã witness ⊆ {`COMMITMENT_BUDGET_EXCEEDED`,
   `COMMITMENT_DEADLINE_EXCEEDED`}. `COMMITMENT_PHASE_LOCK` bị loại có chủ đích: khóa so ứng viên
   với projection ngoại sinh, mà tuyến giữ trùng projection đó, nên một khóa bắn trên tuyến giữ là
   defect và phải fail-closed. Factory nhận cả projection ngoại sinh lẫn projection được công bố và
   kiểm chúng bằng nhau. Breach chỉ vào sổ (mẫu ADR-049). Không thêm action giao thức:
   `commitmentBreachDeclared` chưa từng được phát, và adapter FleetPy từ chối nó.
2. Checkpoint: canonicalizer ghi `"kind":"forcedReference"` bằng một switch vét cạn; kind lạ
   thì ném lỗi. Trước đây mọi kind không phải operational bị ghi là exogenous; lỗi này chưa kích
   hoạt vì chỉ có hai kind. Codec đọc lại kind mới và kiểm giả mạo.
3. Application: `CommitmentValidationContext.ForcedReferenceVehicles` (mặc định `null`). Chỉ khi
   xe nằm trong tập **và** tuyến ứng viên semantically bằng tuyến reduced (cùng version,
   prefix, suffix), lỗi hạn chót và ngân sách của một request trên xe mới thành một **miễn trừ**
   (`ForcedExemptions`). Lời hứa theo tuyến giữ vẫn được công bố, và ledger vẫn cộng như thường,
   có thể vượt trần. Tuyến đã đổi thì không được miễn; lỗi ở stage khác vẫn reject. Breach là một
   **sự kiện**: nó được ghi (`ForcedBreaches`, append vào incident ledger) khi lời hứa được sửa
   theo tuyến giữ, khi khách đổi pha vòng đời trong batch này, khi khách chưa có breach forced
   nào, hoặc khi tập mã đổi. Đổi pha luôn ghi, vì một trần chỉ áp ở pha mới có thể bị vượt mà
   mã vẫn là `COMMITMENT_BUDGET_EXCEEDED`. So tập mã là phòng thủ; với các cổng hiện có, nó
   không tự bắn được khi lời hứa và pha đều không đổi. Một quyết định sau không đổi gì cho khách
   thì lặp lại miễn trừ nhưng không ghi thêm bản ghi, vì ghi mỗi quyết định sẽ đếm
   "khách × quyết định" và phình sổ. Breach id là SHA-256 có domain tag.
4. Algorithms: `HardVectorCandidateAssessor.AssessAndFilter(..., forcedReferenceRecovery)`
   (đòi `requireSafetyNoOp`). Nếu validator loại no-op, no-op được validate lại với xe đặt là
   forced. Chỉ khi lần này hợp lệ và có ≥ 1 miễn trừ, no-op mới được giữ làm phương án forced
   (`IsForcedReference`, utilization 0) và bỏ khỏi danh sách prune. Ngược lại, fail-closed y như
   cũ. `SolverBackedFleetSelector` đặt level **đầu tiên** `forced-reference-count` (MinSum, chỉ
   thêm khi có phương án forced), nên một phương án forced chỉ được chọn khi xe không còn lựa chọn
   nào tránh được vi phạm. Utilization 0 để một xe forced không làm phẳng level MinMaximum của cả
   đội. Quyết định chỉ mang những xe có phương án **được chọn** là forced.
5. Runner: khóa cấu hình WP4 tùy chọn `commitmentRecovery` ∈ {`fail-closed`,
   `forced-reference-v1`}. Vắng khóa thì fail-closed, và bytes/hash của cấu hình cũ không đổi.
   Khóa chỉ hợp lệ cho C1/C2 solver-backed. `RunnerSession` truyền tập forced vào lần validate
   của chính nó. Mỗi quyết định có miễn trừ (kể cả quyết định không ghi breach mới) có
   certificate `normalOperation = false`, kèm một witness `stage = forcedReference` cho mỗi mã
   của mỗi khách, vì contract v1 đã đòi certificate non-normal có ≥ 1 witness.
   `NonNormalCertificateCount` của metric đã đếm được loại này.
6. Sửa lỗi tiềm ẩn: có hai chỗ `RunnerSession` đưa mã ngoài taxonomy v1 vào
   `ErrorPayloadCodec.Encode`. Đó là mã commitment ở lần validate lại, và
   `EXOGENOUS_BREACH_BRIDGE_FAILED`. Hàm này ném `ArgumentException`, khiến host trả
   `INTERNAL_ERROR` và thoát mã 3. Nay hai chỗ trả `INTERNAL_ERROR`/`failSession`, giữ mã gốc
   trong message.

**Evidence:** worktree `E:\Code\RideBound-deadline`, nhánh từ `65d867c`, diff sau đó commit `8d1ea4d`.
Debug **993/993** = 963 + 30 test (Domain 5, Application 8, Algorithms 11, Runner 6). Có sáu
mutation, test đỏ ở cả sáu:
- bỏ điều kiện "tuyến không đổi" thì test laundering đỏ;
- tính utilization cho no-op forced thì test ngân sách đỏ (`ArgumentOutOfRangeException`);
- Runner không truyền tập forced thì test đầu-cuối đỏ. Test nhận envelope `error` thay vì
  exception, xác nhận sửa lỗi 6;
- bỏ luật "không ghi lặp" thì test Application và test Runner nhiều quyết định đỏ;
- certificate dựng từ breach mới thay vì từ miễn trừ thì test Runner nhiều quyết định đỏ;
- bỏ điều kiện "cùng pha" thì test đổi pha đỏ.

Test đầu-cuối trên Runner dùng kịch bản booking C1, hạn chót 1 ms, xe tụt khoảng 50 ms:
- fail-closed (vắng khóa hoặc `"fail-closed"` tường minh): `INTERNAL_ERROR` với "C1 rejected
  every generated candidate…";
- forced: có decision và `promisePublished`; certificate non-normal có đúng 1 witness
  `COMMITMENT_DEADLINE_EXCEEDED` cho `r-1`; breach được commit sau `decisionApplied` và sống
  qua checkpoint/restore;
- quyết định sau ở cùng thời điểm: certificate vẫn non-normal, không `promisePublished`, không
  breach mới; 30 ms sau: lời hứa sửa và breach thứ hai;
- khi không kích hoạt: actions, publication ids, reason code và trạng thái solver trùng
  fail-closed.

Test Algorithms bổ sung:
- C2 có recovery giữ no-op forced; warning excess của nó mang toàn bộ phần vượt, và C2 vẫn chọn
  phương án chèn;
- hai xe: xe forced không làm phẳng xếp hạng utilization của xe kia; test đối chứng với đóng góp
  100% cho thấy nếu thế thì chi phí sẽ quyết định;
- `CollectAllCommitmentWitnesses` không đổi quyết định forced.

Review độc lập hai vòng (2026-09-24). Một lần chạy trước đó bị ngắt vì giới hạn API và không có
kết quả.
- Vòng 1: không có blocker, 3 mục should-fix, 8 nit. Đã sửa: ghi breach lặp mỗi quyết định; khóa
  pha trong tập mã; kiểm projection hình thức; thiếu test (C2, nhiều xe, collect-all,
  `"fail-closed"`, nhiều quyết định); một test gần như rỗng.
- Vòng 2 (chỉ xem phần sửa): không có blocker, 1 should-fix, 4 nit. Đã sửa: chữ và comment còn nói
  khóa pha được miễn; đổi pha với cùng mã bị bỏ sót; thiếu test cho nhánh "chưa có breach"; chữ
  "đóng băng" quá mạnh.
- Các mục còn lại đưa vào Consequences dưới đây.

**Consequences và giới hạn đã biết:**
- Không cần phiên bản mới của bất biến một-no-op: no-op forced vẫn là một phương án hợp lệ, nên
  `CandidateSelectionModel.cs:275-280` và `DecisionMessages.cs:1114-1121` giữ nguyên. Đây là
  lệch so với chữ của tiêu chí T3.3, nhưng cùng mục đích: no-op bị cổng loại không làm hỏng mô
  hình.
- Evidence portfolio không đánh dấu phương án nào là forced; chỉ level `forced-reference-count`
  và certificate cho biết. Công cụ WP13/WP14 đọc level theo vị trí sẽ đọc lệch, nên không dùng
  chúng trên output có recovery.
- Recovery chỉ có ở đường solver-backed C1/C2. `RideBoundHardVectorPolicy`,
  `CommitSoftHardHybridPolicy` (không solver) và B1–B4 không có.
- Recovery thực tế chỉ áp cho cổng hạn chót và ngân sách. Lỗi vật lý (no-op không áp được), lỗi
  cấu hình và khóa pha vẫn fail-closed. Vi phạm dịch vụ ngoại sinh (bridge ADR-049) vẫn không làm
  certificate non-normal, như trước.
- **Xe bị "đóng băng".** Vector ngân sách không âm (`CommitmentVector.cs:33`), và cổng ngân sách
  loại khi `after > hardLimit` (`CommitmentBudgetEvaluator.cs:86-88`). Hạn chót so với lời hứa
  đầu. Vì vậy, một khi khách đã vượt trần hay trễ hạn, mọi ứng viên chèn trên xe đó cũng bị loại,
  vì bộ sinh chỉ chèn nên giờ trả của khách giữ nguyên hoặc trễ thêm. Xe chỉ còn no-op forced
  cho tới khi:
  - khách rời mọi pha mà trần bị vượt còn áp dụng (với ngân sách);
  - khách được trả, hoặc trôi dạt thuận lợi kéo giờ trả về trong hạn (với hạn chót; cổng hạn
    chót không xét pha, `CommitmentLockEvaluator.cs:95-98`).

  Cận dưới của hạn chót hai chiều là ngoại lệ. Đây là hệ quả của lựa chọn "không bao giờ reset";
  tác động lên phục vụ chưa đo được. Luật thay thế "không tệ hơn tham chiếu cho khách đã vi phạm"
  là một quyết định thiết kế riêng của chủ nghiên cứu, chưa cài.
- Nhánh "không ghi lặp" không qua factory, nên không kiểm lại projection. Rủi ro thấp vì miễn trừ
  đã đòi tuyến semantically bằng nhau và delta bằng 0.
- Metric `BreachCount` (`MechanicalMetricCalculator.cs:268-271`) chỉ đếm action
  `commitmentBreachDeclared`, nên luôn bằng 0 dưới recovery. So sánh vi phạm phải dùng
  `NonNormalCertificateCount`, witness của certificate hoặc incident ledger; T3.6 phải thêm cột
  riêng.
- Utilization báo trong quyết định (`WorstHardUtilizationPartsPerMillion`) bằng 0 cho xe forced
  đang vượt trần; đây là giá trị xếp hạng nội bộ, không phải độ đo.
- Record không lưu basis; charge sai basis không phát hiện được từ record hay checkpoint, chỉ
  từ ledger. Witness certificate không mang độ lớn (limit, before, after).
- Một quyết định có breach forced nằm ngoài phạm vi tuyên bố "không vượt trần" của các định lý.
- Sửa lỗi 6 đổi hành vi cả khi tắt recovery: host không còn thoát mã 3; session chuyển `Failed`
  và tiến trình chạy tới EOF; message đổi. Disposition của lần validate lại đổi từ
  `rejectMessage` (chưa từng phát ra được) sang `failSession`. Adapter FleetPy fail trên mọi
  envelope lỗi bất kể disposition (`runner_client.py:400-404`). Không có test cố định cho đường
  này vì không với tới được nếu không tiêm mâu thuẫn; bằng chứng là mutation thứ ba.
- Chưa có test dùng solver OR-Tools thật hay `skipConstantObjectiveLevels`, cũng chưa có
  golden hash cho đường C1 khi tắt recovery (chỉ so từng trường).
- Chưa chạy trên FleetPy hay dữ liệu thật; việc đó thuộc T3.5 trở đi.

### ADR-076 — 2026-09-25 — Proposed (thăm dò; nhánh `research/tier3-failure-aware`; commit `69512d3`)

**Context:** Ở Tầng 2, nhánh hành vi `k = 1` chết 11/20 ô vì `window-wall` (rủi ro R3 của
`E:\Code\Report_INT3508\KE-HOACH-HOP-NHAT-2026-09-23.md`, ngoài kho).
`RideRequest.Board` từ chối lên xe sau giờ đón muộn nhất (`RideRequest.cs:266-274` tại `8d1ea4d`).
Runner trả `SCHEMA_VALIDATION_FAILED`, và adapter nâng thành `RBWP7_RUNNER_PROTOCOL_ERROR`. FleetPy thì
cho khách lên xe mà không kiểm cửa sổ (`ridebound_fleetpy/fleet_control.py:330-338` tại `8d1ea4d`). T3.4 của kế hoạch
ngoài kho đòi: lên xe muộn là **sự thật**, được ghi nhận kèm một vi phạm dịch vụ riêng, và
**không** nới cửa sổ gốc. Tiêu chí nghiệm thu: lên xe muộn round-trip qua checkpoint, và
`_fleetpy_stop` có bound hiệu lực có bằng chứng. Không claim tính mới. ADR này không authorize
`RB-WP14R-009..012`, WP15 hay H7.

**Decision:**
1. Domain: `RideRequest.Board(..., allowLatePickup)` và `Rehydrate(..., allowLatePickup)`, mặc định
   `false`. Khi cho phép, khách lên sau giờ đón muộn nhất chuyển sang `Onboard` với
   `ActualPickupTime` là giờ thật; `EarliestPickup` và `LatestPickup` giữ nguyên. Lên sớm hơn giờ đón
   sớm nhất vẫn bị từ chối. Bản ghi `ObservedLatePickup` gồm khách, xe, giờ đón muộn nhất, giờ lên
   thật, event sequence và epoch, với ràng buộc giờ thật > giờ muộn nhất. Bản ghi nằm trong danh sách
   thứ ba `LatePickups` của incident ledger, tách khỏi commitment breach. Mỗi khách có tối đa một
   bản ghi.
2. Application: `EventReducer(recordLatePickups)`. Khi bật, `PassengerBoarded` muộn thành công và
   thêm một bản ghi.
3. Runner: khóa WP4 tùy chọn `lateBoarding` ∈ {`reject`, `record-v1`}; vắng khóa thì `reject`, và
   bytes/hash của cấu hình cũ không đổi. Khóa áp cho **mọi** chính sách, vì lên xe muộn là quan sát
   chứ không phải quyết định; các nhánh của một so sánh phải dùng chung giá trị. Canonicalizer chỉ
   ghi `latePickups` khi danh sách khác rỗng. Codec chỉ cho khách có bản ghi được rehydrate một lần
   lên muộn. Mỗi bản ghi phải khớp đúng khách của nó (đã lên hoặc đã xong chuyến, cùng xe, cùng giờ
   thật, cùng giờ muộn nhất, event sequence và epoch trong ranh giới); ngược lại, mọi khách lên muộn
   phải có bản ghi.
4. Adapter FleetPy: đọc cùng khóa `lateBoarding` từ cấu hình WP4 (`runner_owns_service_bounds`);
   giá trị lạ thì fail trước khi Runner khởi động. Khi là `record-v1`, `_fleetpy_stop` chỉ bỏ **một**
   bound: giờ đến muộn nhất của điểm trả. Giờ đón sớm nhất, giờ đón muộn nhất và thời gian đi tối đa
   giữ nguyên. Bound hiệu lực của điểm trả khi đó là giờ lên thật cộng thời gian đi tối đa, đúng luật
   của Runner. Lý do, đều đã đọc trong mã:
   - FleetPy lấy min của hai bound cho điểm trả (`VehiclePlan.py:351-372`). Một là giờ lên + thời
     gian đi tối đa, trong đó giờ lên của khách đã lên là giờ thật (`VehiclePlan.py:886-889`,
     `PlanRequest.py:156-161`); bound này trùng luật Runner (`PhysicalPlanValidator.cs:579-631`). Hai
     là giờ đến muộn nhất `t_do_latest = t_pu_latest + max_trip_time`, neo vào **cửa sổ gốc**
     (`PlanRequest.py:101`). Với khách lên đúng giờ, bound thứ hai không bao giờ chặt hơn bound thứ
     nhất; chỉ khi khách lên muộn nó mới chặt hơn Runner.
   - Runner validate mọi ứng viên **có thay đổi** mà không nới gì (`ServiceQualityAllowance.Strict`,
     `InsertionCandidateGenerator.cs:231-246`), và adapter không gửi lại tuyến trùng bản của nó
     (`fleet_control.py`, nhánh `action.route == current`). Ngoại lệ đã biết: sau một lần hủy trong
     batch, Runner bỏ các điểm của khách hủy và tăng version tuyến (`RoutePlan.cs:243-266`), còn bản
     tuyến của adapter không đổi. Khi đó no-op (có phần nới ngoại sinh của ADR-047) được gửi lại và
     FleetPy kiểm nó. Ngoài ngoại lệ đó, giờ đón muộn nhất và thời gian đi tối đa của FleetPy chỉ bất
     đồng với Runner khi mô hình Runner/adapter sai. Hai bound này được giữ làm phép kiểm chéo, loại
     phép kiểm từng bắt lỗi thật (lỗi 13 ms của ADR-047, lỗi 192 s trong `wp14r_freeze_v6.py`).

   Ánh xạ hai bên dùng cùng con số gốc: `maxRideTimeMs` = `max_trip_time` và `latestPickupMs` =
   `t_pu_latest` (`mapping.py:265-266`, `:276-277`).

**Evidence:** worktree `E:\Code\RideBound-deadline`, trên `8d1ea4d`, diff sau đó commit `69512d3`. .NET
**1005/1005** = 993 + 12 test (Domain 5, Application 3, Runner 4).

Python chạy bằng lệnh baseline ghi trong `docs/handoffs/wp14-continuation-2026-08-25.md`: 443 test,
437 pass, 6 test freeze fail. Cùng 6 test đó fail với **thông báo giống hệt** trên một bản checkout LF
sạch của `8d1ea4d`, tức trước T3.4 (worktree tách riêng `E:\Code\RideBound-py-baseline`, 437 test).
Vậy chúng có từ trước và không do T3.4 gây ra. Trong số đó, 21 file cấu hình `wp9-confirmatory` bằng
đúng blob của commit cây chính `41a4e11` nhưng khác freeze E1; 3 file `.cs` do các commit nghiên cứu
đổi. Vì freeze WP9 v6 đã đỏ trên `adapterPackageTreeSealSha256` từ trước, việc T3.4 làm dịch seal đó
bị che, chưa kiểm được riêng. Sáu test Python mới pass; bốn test đầu dùng `VehiclePlan` thật của
FleetPy 1.0.2:
- khi ghi lên xe muộn, chỉ giờ đến muộn nhất bị bỏ; giờ đón sớm nhất, giờ đón muộn nhất và thời
  gian đi tối đa còn nguyên ở cả hai chế độ;
- một kế hoạch có giờ đón trễ vẫn bị FleetPy phủ quyết ở cả hai chế độ (phép kiểm chéo còn);
- khách đã lên lúc 240 s: mặc định FleetPy chặn trả lúc 251 s theo cửa sổ gốc (200 s) dù chỉ đi
  11 s; khi ghi lên xe muộn thì trả lúc 251 s được, trả lúc 346 s vẫn bị chặn (240 + 100 = 340 s);
- sức chứa vẫn bị kiểm khi ghi lên xe muộn;
- khóa cấu hình được đọc đúng như Runner đọc;
- đường đi đầy đủ từ thuộc tính operator và file thật tới `RideBoundSessionSettings`.

Test đầu-cuối Runner: mặc định trả `SCHEMA_VALIDATION_FAILED` với đúng câu "Actual pickup time must
remain inside the accepted pickup window." (thông báo `window-wall` của Tầng 2). Với `record-v1`: có
decision; sau `decisionApplied` có 1 bản ghi 500 ms; khách ở `Onboard`, giờ đón muộn nhất vẫn 2000 ms;
bản ghi còn nguyên qua checkpoint/restore. Test codec còn kiểm: bản ghi ngoài ranh giới event
sequence hoặc epoch, bản ghi trùng, và một danh sách rỗng tường minh đều bị từ chối. Có 2 mutation,
đều đỏ: bỏ bước ghi thì test Application đỏ; bỏ kiểm giờ thật khớp bản ghi thì test codec đỏ.

Review độc lập vòng 1 (2026-09-25): không có blocker. Should-fix chính: bản đầu bỏ cả ba bound của
FleetPy, nhiều hơn bằng chứng cho phép, làm mất phép kiểm chéo từng bắt lỗi thật. Đã sửa: nay chỉ
bỏ giờ đến muộn nhất. Cũng đã sửa: chữ ADR nói quá, thiếu test (đường cấu hình, `AppendBreach`, nhánh
codec, dict ở chế độ mặc định), số dòng trích dẫn, tên tham số trong ngoại lệ, và 12 file `.pyc` được
theo dõi mà lần chạy test đầu (không `-B`) đã ghi đè; các file đó đã được khôi phục về nội dung
commit. Review vòng 2 (chỉ phần sửa): không có blocker. Một should-fix: câu "mọi kế hoạch FleetPy
thấy đều Strict" sai với đường hủy trong batch; đã thu hẹp chữ và ghi đường đó vào Consequences.
"Giống luật Runner" đúng tới mức làm tròn lên giây của FleetPy, vốn chỉ làm FleetPy dễ hơn.

**Consequences và giới hạn đã biết:**
- Khi `record-v1` bật, lớp chết `window-wall` không còn kết thúc run. Không trộn kết quả hai giá trị
  khóa trong một so sánh, và không so trực tiếp với các ô Tầng 2 đã chết vì `window-wall`.
- Ở `record-v1`, FleetPy vẫn kiểm giờ đón muộn nhất và thời gian đi tối đa cho mọi kế hoạch được
  gửi tới nó. Một lỗi ánh xạ kiểu 192 s vẫn làm run dừng thay vì bị ghi thành "lên xe muộn". Chỉ lên
  xe muộn do trôi dạt trên tuyến giữ nguyên (không gửi lại FleetPy) mới thành bản ghi.
- **Đường chết còn lại (có từ ADR-047, T3.4 không làm tệ hơn, chưa có test):** sau một lần hủy trong
  batch, no-op có phần nới được gửi lại FleetPy. Nếu lúc đó giờ đón của một khách đang chờ đã trễ,
  hoặc một khách lên muộn đã quá thời gian đi tối đa, FleetPy từ chối kế hoạch
  (`RBWP7_FLEETPY_PLAN_INFEASIBLE`) và run dừng.
- **Không cộng dồn hai loại.** Probe đã ghi một vi phạm cửa sổ đón **dự báo** trên tuyến giữ thành
  exogenous breach (`PhysicalPlanValidator.cs:465-474`, bridge ADR-049), còn độ trễ **thực** đi vào
  `LatePickups`. T3.6 phải đếm riêng hai loại, không cộng.
- Thay đổi adapter làm dịch `adapterPackageTreeSealSha256` của freeze WP9 v6 theo thiết kế. Tái lập
  WP9/H6 phải dùng adapter của cây chính, không dùng nhánh này. Các test freeze đó vốn đã đỏ trên
  nhánh (xem Evidence).
- Ghi `"lateBoarding": "reject"` tường minh cho kết quả giống vắng khóa, nhưng hash nội dung WP4 và
  hash binding thì khác. Các nhánh của một so sánh phải dùng chung **bytes** cấu hình, không chỉ
  chung hành vi.
- Chưa metric nào đọc `LatePickups`; T3.6 phải thêm cột. Lên xe sớm vẫn fail-closed. Vượt thời
  gian đi tối đa khi trả khách không được ghi ở đâu (`RideRequest.Complete` không kiểm thời gian).
- Dòng gán `_rb_settings` trong `RideBoundFleetControl.__init__` không có test riêng; test dựng
  settings thật rồi đặt vào instance tạo bằng `__new__`.
- Kiểm "mọi khách lên muộn phải có bản ghi" trong `ValidateRelations` là phòng thủ: `Rehydrate` đã từ
  chối trước ở đường này, nên mutation của riêng kiểm đó không bị test nào bắt.
- Chưa chạy mô phỏng FleetPy đầy đủ; việc đó thuộc T3.5 và T3.7.

### ADR-077 — 2026-09-26 — Proposed (thăm dò; nhánh `research/tier3-no-worse`)

**Context.**
- Ở ADR-075, một xe có no-op bị cổng loại chỉ còn giữ được tuyến. Mọi tuyến đã đổi đều bị loại, nên xe
  "đóng băng" và không nhận được khách mới cho tới khi khách trễ xuống xe.
- Trong quét `C` với `M⁺` (`tang3/sweep`, 156 job, thăm dò), `M⁺` bị ép ở 28% quyết định với x = 30 s. Trung bình
  9,4% đội xe bị đóng băng; ở W2 là 14–18,5%. Ở W2, `M⁺` cũng phục vụ ít hơn `C` 2 khách. Nguồn:
  `tang3/sweep/logs/sweep-aggregate-2026-09-26.txt` và `tang3/sweep/sweep-jobs-2026-09-26.tsv` (cột
  `M5_nonNormalShare`, `M5_meanForcedFleetShare`).
- Vì vậy phép so có thể đang phạt luật hạn chót bằng chính luật phục hồi của mình. Tiêu chí kích hoạt được ghi ở
  `tang3/sweep/SWEEP-AMENDMENTS.md` A1.

**Decision.** Thêm khóa WP4 `commitmentRecovery: "no-worse-than-reference-v1"`, mặc định tắt, mở rộng
`forced-reference-v1`. Trên một xe bị ép, mọi phương án validator đã loại được kiểm lại, và phương án được giữ làm
phương án bị ép khi:
1. Mỗi lỗi cổng của một khách trên xe đó chỉ được miễn nếu tuyến giữ (dự báo ngoại sinh) **cũng mắc cùng lỗi đó**,
   và phương án **không tệ hơn** ở lỗi đó:
   - hạn chót trên: giờ trả ≤ giờ trả của tuyến giữ;
   - hạn chót dưới: giờ trả ≥ giờ trả của tuyến giữ;
   - chiều ngân sách: tổng sau ≤ tổng sau của tuyến giữ.
2. Khóa pha và khóa xe không bao giờ được miễn. Khách mà tuyến giữ không vi phạm phải qua cổng như thường.
3. Miễn trừ trên tuyến đã đổi được ghi thành loại vi phạm mới `ForcedNoWorse`:
   - checkpoint `"forcedNoWorse"`, chứng nhận có giai đoạn `forcedNoWorse`;
   - bất biến domain: tuyến đã đổi, cùng xe, ngân sách tính theo cơ sở của chính sách.
4. Ở chế độ này, xếp hạng mức dùng chỉ bỏ qua những chiều mà một khách được miễn đang vượt trần. Mọi chiều còn trong
   trần, của mọi khách trên xe, được xếp hạng như trên bất kỳ xe nào.
5. Ở `forced-reference-v1`, hành vi và thông điệp giữ nguyên từng byte.

**Alternatives considered.**
- Chỉ nhận tuyến đổi giữ nguyên dự báo của khách bị ép. Loại vì vô hiệu: `ServiceOrder` chứa mọi điểm dừng còn lại
  (`PromiseProjector.cs:36`), nên mọi phép chèn đều làm dự báo đổi.
- Đổi mốc hạn chót của khách trễ thành giờ của tuyến giữ. Loại vì như thế lời hứa bị phá mà không có bản ghi.

**Evidence.**
- `dotnet test RideBound.slnx`: 1025/1025, tức 1005 cũ cộng 20 test mới. Test mới gồm:
  - Domain: 4 test, gồm bất biến và bằng nhau theo trường;
  - Application: 8 test: giữ khi không tệ hơn, loại khi tệ hơn, không miễn khách mà tuyến giữ không vi phạm, cơ
    sở nhìn thấy, tuyến không đổi vẫn là `ForcedReference`, chỉ cho xe được nêu tên, cận dưới hai chiều, chế độ thu
    mọi witness;
  - Algorithms: 5 test: giữ phép chèn không làm khách trễ tệ hơn, chính sách phục vụ khách mới, bắt buộc có
    `forced-reference`, xếp hạng như xe thường dưới trần 0, không xếp hạng chiều vượt trần của khách được miễn;
  - Runner: 3 test: codec round-trip kèm chống sửa, đọc cấu hình, từ chối ngoài C1/C2.
- Review độc lập vòng 1: không blocker. Đã sửa: thiên vị xếp hạng mức dùng, chữ lỗi thời, thông điệp và chiều lỗi
  riêng cho no-worse.
- Review độc lập vòng 2: không blocker. Đã sửa:
  - test cho xếp hạng mức dùng;
  - test chiều của cận dưới;
  - chữ trong tài liệu;
  - bỏ qua theo từng (khách, chiều) thay vì theo khách.
- Ba đột biến (xếp hạng như tuyến giữ, xếp hạng chiều vượt trần, đảo chiều cận dưới) đều bị test mới bắt.
- **Chạy đầu-cuối (A1, 2026-09-26, thăm dò).**
  - Runner `C:\RideBoundData\research\runner-tier3-v2` được publish từ `45f8026` với cây sạch: tree `ac251ef3…cff3f`,
    429 file. WP4 v2 chỉ khác v1 ở `commitmentRecovery`.
  - Đã chạy 28 job FleetPy medium: 22 nhánh `M⁺-x` có forced trong quét, và 6 đối chứng (4 `C-30`, 2 `M⁺-900`).
  - 28/28 `status: pass`, audit v/e/p đúng.
  - 49 quyết định đã được xác nhận mang witness `forcedNoWorse`. Final checkpoint có 50 breach `ForcedNoWorse` và
    5155 breach `ForcedReference`.
  - 6/6 đối chứng có chuỗi hành động đội xe giống hệt bản chạy bằng Runner v1.
  - Khách phục vụ trên 22 job điều trị: no-worse 1874, `forced-reference-v1` 1864, `C` 1873. No-worse bằng `C` ở
    21/22 job và cùng chuỗi hành động với `C` ở 19/22.
  - Nguồn: `tang3/sweep/SWEEP-REPORT.md` §7, `tang3/sweep/logs/noworse-analyze-2026-09-26.txt`,
    `tang3/sweep/logs/noworse-aggregate-2026-09-26.txt`.
- **Chạy đầu-cuối cho `V` và `M` (Tầng 3 việc A, 2026-09-29, thăm dò, kế hoạch niêm phong trước khi chạy).**
  - 48 job: `V-30` và `M-30` × luật (a) Runner v1 / luật (b) Runner v2 × 6 thế giới × 2 ô phát triển, world v1.1, cùng
    inventory kho `0da63eaf…4cbe`. 48/48 `status: pass`, audit v/e/p đúng; 8/8 dự đoán niêm phong đúng.
  - Đường miễn trừ **theo chiều ngân sách** (cơ sở nhìn thấy) nay đã chạy đầu-cuối: 118 quyết định của `V-30(b)` có witness
    `forcedNoWorse` mã `COMMITMENT_BUDGET_EXCEEDED`. `M-30(b)` có 12 quyết định `forcedNoWorse` ở thế giới tắc giảm dần.
  - Đối chứng: ở thế giới không nhiễu, cả 4 biến thể trùng từng quyết định với `C-30`.
  - Kết quả: với (b), `V-30` phục vụ đúng bằng `C-30` ở 8/8 ô có tắc (697 = 697) và cùng từng quyết định ở 11/12 ô; với (a)
    là 666. Khoảng cách số khách giữa `C` và `V` ở thí điểm vì vậy đến từ luật phục hồi.
  - Kiểm độc lập bằng mã riêng: 7/7 khẳng định khớp.
  - Nguồn: `tang3/vm/VM-PLAN.md` (niêm phong `VM-PLAN.sha256`), `tang3/vm/VM-REPORT.md`, `tang3/vm/analysis/`,
    `tang3/vm/independent/RESULT.md`.

**Consequences và giới hạn.**
- Là tùy chọn thăm dò. Mặc định và `forced-reference-v1` không đổi.
- Đường Runner khi một tuyến đổi được chọn **chưa có test tích hợp cố định trong kho**: fixture hai xe luôn có xe
  trống nhận khách mới. Đường này **đã được** chạy đầu-cuối bằng 22 job FleetPy của A1, ở mục Evidence, nhưng các job
  đó nằm ngoài `dotnet test`. Việc khôi phục một checkpoint có `ForcedNoWorse` trong Runner mới được kiểm ở mức codec.
- Bản ghi `ForcedNoWorse` **không** nhất thiết có một bản cho mỗi quyết định no-worse:
  - Khi mọi delta bằng 0, ví dụ chèn khách mới sau điểm trả của một khách trễ đang ngồi trên xe, quy tắc "vi phạm là
    một sự kiện" không ghi bản mới.
  - Khi dự báo của khách không đổi, bản ghi là loại `ForcedReference`.
  - Muốn đếm "xe bị ép vẫn phục vụ" thì đếm giai đoạn `forcedNoWorse` trong chứng nhận, không đếm loại bản ghi.
- Chưa có test cho: hai khách trễ trên một xe; khóa pha cộng hạn chót; ngân sách nhiều chiều; C2 dưới no-worse; nhánh
  `reference is null`.
- "Tệ hơn" vẫn có thể xảy ra ở những chiều mà cổng không đo, ví dụ giờ đón của khách không bị giới hạn.
- Tầng `forced-reference-count` vẫn đứng trước số khách nhận. Một phương án no-worse được đếm 1, như no-op bị ép.
- Công việc validate thêm chưa được cộng vào `validationWorkUnits`.

### ADR-079 — 2026-10-16 — Accepted (bàn giao GitHub và độ nhạy C60/C120)

**Context:** Owner trực tiếp yêu cầu đưa mã/kết quả lên GitHub để tiếp tục trên
máy công ty, cho phép dùng báo cáo khi raw lớn; đồng thời yêu cầu chạy thêm
C60/C120 với bộ chọn ưu tiên chi phí. Quyền này thay hạn chế commit/push cũ
trong phạm vi bàn giao; không cho sửa/xóa frozen evidence hoặc hạ sàn tài nguyên.

**Decision:** Nhánh `codex/research-handoff-20261016` dựa trên cost-first dce84ec
chứa mã mới nhất, docs, kết quả nhẹ và audit. Nhánh tương ứng
`codex/ridebound-handoff-20261016` ở Report_INT3508 giữ nguồn/PDF khóa luận,
trạng thái báo cáo thực tập do owner đã có và chương trình/báo cáo nghiên cứu.
Không viết lại lịch sử hoặc sửa cây nguồn E. Raw/vendor/env/paper tải về giữ local;
bàn giao có inventory/hash và hướng dẫn external inputs, không nhận clone đủ tái lập.

Nghiên cứu độ nhạy mới: hai smoke calibration rồi 34 ô × C60/C120 =68 main,
đúng runner v3 và WP4 cost-first của C30/U; chỉ trần drop_eta_total_ms đổi.
Plan/harness mới trên C, outputs mới `tier3-cost-cap-sensitivity-v1`; original
seal/driver/analysis/raw/H6/E1/H7 giữ nguyên. Không per-step, Stage2 hoặc sửa dự báo.
AC-only, C30GiB/E40GiB, reserve410MiB/job, tối đa6workers; pending-only resume,
không retry failed/partial/anchor-only. Chỉ phân tích khi đủ68 và kiểm cuối đạt.

**Evidence/status:** 13/13 selftests entrypoint đạt; required .NET trên checkout
cost-first giữ đúng 1known clean-tree failure (exit1), không nhận suite xanh.
Full original/new input verification đang chạy; chưa có kết quả C60/C120.
Chi tiết [plan mới](C:/RideBoundContinuation/2026-10-16/cost-cap-sensitivity-v1/PLAN.md)
và [hồ sơ bàn giao](benchmarking/cost-cap-sensitivity-handoff-2026-10-16.md).
Suy luận vẫn thăm dò trên 34 ô đã xem, không optimum/satisfaction/profit/general claim.
Những mốc no-push ở ADR078/seal là lịch sử; giữ nguyên thay vì sửa lại receipt.

**Heartbeat 2026-10-16T15:06:26.407675+07:00:** 2/2 smoke đạt; 14/68 main pass/succeeded, 6 đang chạy/chưa kết thúc, 48 chưa nhận;16bundle length/SHA/exit/binding và96pinned+17seal+5policy khớp;AC100%,C39.656/E40.766GiB,0stop. Driver đang chạy,không restart;chưa phân tích partial panel. Evidence `heartbeat-20261016-150626-mechanical.json`;hourly ACTIVE.

**Heartbeat 2026-10-16T16:07:49.664785+07:00:** 2/2 smoke đạt; 37/68 main pass/succeeded, 6 đang chạy/chưa kết thúc, 25 chưa nhận;39bundle length/SHA/exit/binding và96pinned+17seal+5policy khớp;AC100%,C37.875/E40.765GiB,0stop. Driver sống,không restart;chưa phân tích partial panel. Evidence `heartbeat-20261016-160749-mechanical.json`;hourly ACTIVE.

**Heartbeat2026-10-16T17:07:56.317806+07:00:** 2/2 smoke đạt; 65/68 main pass/succeeded, 3 đang chạy/chưa kết thúc, 0 chưa nhận;67bundle/96pinned/17seal/5policy khớp,0stop;AC100%,C36.086/E40.765GiB;chưa partial analysis.Evidence`heartbeat-20261016-170756-mechanical.json`.

**Checkpoint2026-10-16T17:17:22.473151+07:00:** 2/2 smoke và68/68 main pass/succeeded;0active/0pending;driver đang full post-phase check;70bundle/96pinned/17seal/5policy khớp,0stop;chưa main-post/analysis/finalexit0.Checker mới ngoài sealed harness chuẩn bị,không đổi nghiên cứu;.NET1033/1034knownfailure.Evidence`heartbeat-20261016-171722-mechanical.json`.

**Kết quả cuối 2026-10-16:** C60/C120 COMPLETE: 2/2 smoke, 68/68 main; final verification và analysis exit0 lúc17:24:02+07; 4.355/4.355 đối chiếu số học đạt;0active/0pending/0STOP. C60 so C30: +32phục vụ/+113ghép/+19muộn60/+13trong60; C120 so C60: +34phục vụ/+192ghép/+121muộn60/−87trong60. Thăm dò34ô/nămngày đã thấy, không optimum/satisfaction/profit. PDF77trang giữ nguyên; audit/WP12 còn mở. Hourly PAUSED sau khi báo vàpush kết quả;one-off PAUSED.

Bằng chứng: main/analysis-complete và rootlog exit0; checker4355/4355; [hồ sơ đầy đủ](benchmarking/cost-cap-sensitivity-handoff-2026-10-16.md). Không sửa kết quả cũ, không sửa luận văn từ partial.

**Đóng lịch 2026-10-16T18:17:17.444172+07:00:** Đã báo kết quả và push f483e71/74f78c4; hourly PAUSED, one-off PAUSED, không archive. Receipt cost-cap-monitor-closure.json; không resume/rerun.

### ADR-078 — 2026-10-15 — Accepted (ghi nhận tiếp phiên thăm dò bên ngoài)

**Context:** Người dùng yêu cầu kế thừa phiên Claude, tiếp tục cả khóa luận và
Stage 1b theo các quyết định mới nhất; phải kiểm tệp thật trước khi chạy tiếp.

**Decision:** Giữ cây chính/worktree job trên E chỉ đọc; cập nhật trạng thái ở
cây nghiên cứu trên C, không commit/push. Bảo toàn seal, driver/comparator,
ngưỡng, raw và kết quả âm. Sửa analysis chưa khóa theo R46: source/cache, CSV
bytes, receipt/build/binding/stop, mode/source và so sánh undefined. Ledger
arithmetic là validity độc lập. Ghi quy ước median/H7 trước main. Main chỉ sau
full self-test/judge inventory; không tự Stage 2 hoặc chép bản ghép vào luận văn thật.

**Evidence:** [Hồ sơ](benchmarking/thesis-stage1b-continuation-2026-10-15.md):
17/17 SHA, 4 gate/2 smoke, selector frame bytes, targeted 19/19 và 18/18;
1.836 số lịch sử khớp. Full v3 192/192, judge READY TO FREEZE; khóa 10 tệp lúc
01:10:29 +07, manifest f2db6445…952f1; main driver bắt đầu 01:11:07, preflight
qua/binding ghi 01:19:44, bốn lượt đầu active lúc kiểm. Required suite cost-first checkout
1.033 pass + 1 known clean-tree failure, exit 1. PDF v4 74 trang; visual QA 15–17.

Heartbeat04:05: tổng16/68 main pass/succeeded; bảy bundle mới khớp length/SHA,
không stop. Driver trước kết thúc03:34:41 exit3 do52 deferred-disk, post-phase
verification không lỗi. AC và C46,74/E40,80 GiB cho phép resume pending-only
04:07:05; driver7088 qua preflight. Snapshot04:10:58:16 pass,4 active,48 chưa
nhận, C46,59 GiB. Chưa đủ68/post-phase exit0 nên chưa phân tích; câu hỏi thêm
dung lượng chưa có trả lời, không giảm sàn hoặc xóa tệp.

Owner steering09:22–09:26 +07 thay câu hỏi dung lượng bằng quyền trực tiếp:
“Cho phép ổ cứng không dưới 30gb”, rồi “Cứ chạy đầy đủ đi đừng lo về dung lượng”.
Áp dụng C30 GiB, E40 GiB và reserve410 MiB giữ nguyên. Bốn bundle mới khớp
length/SHA, tổng20 pass; đủ17 seal/10 analysis và binding vẫn khớp. Driver cũ
kết thúc04:22:25 exit3 do48 deferred-disk; C43,07 GiB không đủ sàn45 cũ nhưng
đủ sàn mới. Amendment tài nguyên riêng trên C xác thực nguyên byte driver v7,
chỉ đổi FLOOR_MIB trong bộ nhớ; không ghi lại frozen source/seal/binding.
[Amendment](C:/RideBoundContinuation/2026-10-15/resource30-v3/STAGE1B-AMENDMENTS.md)
và manifest7c862409…11950;12/12 kiểm admission/AC/STOP/drift/reservation/skip đạt
qua hidden detached launcher. Hai bản thử trước main được giữ nguyên; không
có main job nào chạy dưới chúng. Resume09:28:30, launcher20396/driver3888,
log C:/RideBoundContinuation/2026-10-15/evidence/run-main-resource30-2026-10-15-092829.log;
preflight lúc09:29. Các heartbeat đã cập nhật sàn mới và đúng launcher/pin.

Báo thức một lần06:20 thực tế gửi heartbeat09:33 +07. Đã xử lý và chuyển riêng
ti-p-t-c-ridebound-l-c-05-40 sang PAUSED, không dời ngày/archive; hourly ACTIVE.
Snapshot09:33:54 vẫn20 pass/48 pending, driver3888 preflight,0stop/resourcefail;
evidence/oneoff-wake-2026-10-15-093354.json trên C. Không tạo driver trùng.

Heartbeat10:24–10:25 +07: **40/68 main pass/run succeeded**,6 wrappers active,
22 chưa nhận; driver3888 đã qua preflight và tiếp tục chạy.20 bundle mới khớp
length/SHA, returncode0 và admission/job-log binding;0 pending child receipts.
17 tệp seal,10 analysis,5 resource-policy và binding khớp;0 failure/STOP/drift.
C41,348/E40,805 GiB, máy AC100%. Evidence
C:/RideBoundContinuation/2026-10-15/evidence/main-mechanical-progress-2026-10-15-102547.json.
Chưa đủ68/final post-phase exit0, chưa chạy analysis; không restart driver.

Heartbeat11:24–11:28: đủ68/68 pass;28 bundle cuối khớp length/SHA,returncode0
và binding. Driver/launcher kết thúc **exit0 lúc11:28:00+07**, full post-phase
verification và resource-policy check đạt. Main admission trên C:
evidence/main-final-admission.json, nối sáu evidence cho đủ68. Không stop.
Đã bắt đầu extractor nguyên SHA6ff7f303…3429c lúc11:29 từ cwd gốc/interpreter-B;
orchestration chỉ giữ env/lệnh/log/receipts, không đổi mã analysis. Chờ extract
exit0 mới chạy report; kết quả chưa được diễn giải hoặc chép vào luận văn.

**Closure11:43 +07:** extract exit0 lúc11:31:45; report exit0 lúc11:40:13;
9.638 rider rows/136 role-runs,19 SF constants và25/25 dấu ô được tái lập.
83/83 kiểm số học độc lập trên extraction/report đạt (không claim dựng lại
độc lập raw/forecast). Stage1b G0,H1–H6 PASS;H7 FAIL:7 served witnesses<10,
**không mở Stage2/per-step arm**. I_L=+180,I_served=+42,I_timely60=−138,
identity đúng;23/25 ô w08/w17 dương,5/5 ngày dương,không CI/causal claim.
CF-C30 so SF-C30: served2364 vs2338;late60 72/2364 vs76/2338;
shared432 vs313;forecast late60 11/2364 vs10/2338;exposed106 vs33.
Trong CF, cap giảm138 served và595 shared,giảm307 late60,tăng169 timely60;
không che các đánh đổi. Cap overshoot0,max29,657s;median guard đạt.
[Kết quả](C:/RideBoundContinuation/2026-10-15/results/STAGE1B-RESULTS.md),
[báo cáo đầy đủ](C:/RideBoundContinuation/2026-10-15/results/stage1b-report.txt),
completion receipt evidence/analysis-completion.json trên C.
Results manifest SHA dac8c1e1…3cdec;32 mục đối chiếu SHA trong3 inventory(17+10+5) khớp sau analysis.
Đã báo kết quả trong chat trước mọi sửa luận văn và **PAUSED hourly** theo yêu
cầu khi hoàn tất;one-off đã PAUSED,không archive. Dữ liệu/H6/E1 gốc,baseline,
claim hẹp và biên repo giữ nguyên. Next: người dùng xem kết quả và bản PDF74
trang đã ghép; luận văn thật vẫn chờ owner duyệt,không tự sửa/commit/push.

### ADR-080 — 2026-10-20 — Proposed (thăm dò; nhập từ máy công ty; nhánh `research/via-ref`; commit `3afcd17` + `176f5dd`, trước khi viết lại tác giả ngày 25/10 là `b43ef5b` + `66558f2`)

**Nguồn:** chủ nghiên cứu viết trên máy công ty (commit gốc `61db5d9`, `dc5bb49`, nhánh `research/perstep-relaxed-deadline`,
không có trong kho này). Toàn văn bản nháp ở
`C:/RideBoundContinuation/2026-10-22/bundle-21-10/extracted/docs/bundle-nycvia/bundle/patches/ADR-080-draft.md`
(sha256 `41e1fd64…c107d`). Bản nháp ghi rõ "chưa chép vào docs/18" để chủ xem trước, nên mục này chỉ tóm tắt và trỏ tới đó,
không chép nguyên văn.

**Decision (tóm tắt bản nháp):**
- `CommitmentPolicy` thêm `DropEtaRevisionLimit` (P-b: loại ứng viên khi `|p − e| > b` ở từng quyết định, không nhớ) và
  `DropEtaDeadlineRelaxed` (DL-HZ: hạn chót nới `p ≤ max(p0 + slack, e)`). Khóa cấu hình `dropEtaRevisionLimitMs`,
  `dropEtaDeadlineRelaxed`. Mã mới `COMMITMENT_REVISION_LIMIT_EXCEEDED`.
- Vắng khóa thì bytes chuẩn tắc và mọi hash cấu hình giữ nguyên; B1–B4 không nhận hai tham số.
- Hai cổng là công cụ đo cho phép so luật C với các họ cổng khác, không phải đề xuất cơ chế; không claim tính mới.

**Evidence bổ sung ở máy này (22/10):**
- Hai patch nhập nguyên văn lên `7435661` cho cây `086beacc`; `dotnet test RideBound.slnx` 1134/1134.
- Hồi quy: Runner mới, hai luật tắt, so với job Stage 1b đã lưu: 2.311/2.311 khung giống hệt.
- Hai luật thật sự hoạt động trên FleetPy (job smoke): PS-300 có 36 lần `COMMITMENT_REVISION_LIMIT_EXCEEDED`, DLHZ-120 có
  116 lần `COMMITMENT_DEADLINE_EXCEEDED`.
- Cầu nối Manhattan: PS-300 và DLHZ-120 khớp dự đoán bộ mô phỏng ở 11/11 ô mỗi nhánh (trong 55/55 của ADR-081).
- Giới hạn của bản nháp vẫn giữ nguyên, trừ hai điểm "chưa chạy lại ô lưu trữ" và "chưa chạy trên FleetPy", nay đã có các
  bằng chứng trên.

### ADR-081 — 2026-10-23 — Proposed (thăm dò; cầu nối bộ mô phỏng công ty; nhánh `research/via-core`, `research/g2-core`)

**Context:**
- Kế hoạch tổng hợp của công ty ngày 21/10 (`RideBound-Tong-Hop-Ke-Hoach-21-10.md`) đo các luật trên Via bằng bộ mô phỏng
  của công ty. Trước khi dùng số của nó, phải chứng minh bộ mô phỏng làm đúng như RideBound thật chạy trên FleetPy (cầu nối).
- Chủ nghiên cứu quyết định ngày 22/10:
  - chấp nhận G2 theo định nghĩa của bộ mô phỏng: mỗi quyết định dời tối đa 1 khách đang chờ sang xe khác, giờ đón không
    muộn hơn dự báo của tuyến giữ;
  - đối chứng chính là Elmi Ordinary Control EO35.
- Không claim tính mới. Dời khách đang chờ (reassignment) và ngân sách tỉ lệ theo slack (Elmi et al. 2026) đều đã có; đây chỉ
  là cài đặt để cầu nối kiểm được (`docs/03-related-work-and-claim-boundary.md`).

**Decision:**
1. **A0.1** (hai patch băm của máy công ty, `365caac`, `365b502`): băm trạng thái một lần mỗi epoch với mảnh chuẩn tắc được
   cache. Byte giao thức và hash không đổi; checkpoint kiểm lại bằng bản dựng không cache.
2. **Ngân sách Elmi theo từng khách** (`6d6fa47`; v1.1 `4a24810`):
   - khóa `dropEtaBudgetSlackPermille` (RCE) và `dropEtaDeadlineSlackPermille` (EO; thêm `dropEtaDeadlineRelaxed` thì là
     EOHZ);
   - `B_r = round(permille/1000 · max(0, pickup0 + maxRide − drop0))`, làm tròn xa số 0, cùng giá trị với `Math.round`
     của bộ mô phỏng; cố định ở lời hứa đầu;
   - v1.1 (sau review độc lập): C1/C2 chỉ nhận khóa ngân sách khi thứ tự chọn là cost-first, vì tầng hard utilization chỉ đọc
     hạn mức cố định.
3. **G2** (`115a4fc`):
   - bật bằng cờ `waitingRiderRelocation` ở mọi policy và `candidateGeneration.waitingRiderRelocation` ở WP4; chỉ với C1
     solver cost-first, `earliest-feasible`;
   - tuyến xe cho phải khả thi chặt khi bỏ khách; xe nhận là mọi cách chèn đón và trả của chính khách đó vào phần đuôi còn sửa
     được của xe khác, không kèm yêu cầu mới; giờ đón bị chặn chỉ được sớm lên (ratchet); không giới hạn số lần đổi xe;
   - CP-SAT ràng buộc cặp cho–nhận; tầng `relocation-candidate-count` đứng sau chi phí vận hành, nên hòa thì giữ nguyên;
   - evidence 1.3.0 (`relocationLosses`, trường dời của ứng viên), Contracts và bộ kiểm Python nhận 1.3.0.
4. **Sửa adapter** (`fa676b4`; chủ chọn phương án A ngày 23/10, kèm 4 điều kiện):
   - khi FleetPy đang khóa một chặng phục vụ dài 0 s mà Runner đã tính là xong, adapter giữ bản sao chặng đó ở đầu kế hoạch
     rồi nối tuyến của Runner. Đây là quy ước của FleetPy khi lập lại kế hoạch giữa lúc phục vụ;
   - không đổi RideBound, không đổi bộ mô phỏng. Mọi trường hợp khác vẫn đúng tuyến của Runner như trước.

**Evidence:**
- Cây và test (`dotnet test RideBound.slnx`, chạy khi worktree còn thay đổi chưa commit):
  - `086beacc` 1134/1134; `c112fdbb` 1134/1134;
  - Elmi v1 `32155156` 1173/1173; v1.1 `290c698b` 1178/1178;
  - G2 `6d071153` 1225/1225; `0b930c4f` 1227/1227.
  - Bộ test adapter Python: 456 test, 6 lỗi có từ trước, giống hệt trên cây không có G2.
- Cầu nối Manhattan (11 ô sáng/chiều 12–18/11/2018, trần đi 2,0×, cost-first, thế giới W5, seed 7). So với dự đoán bộ mô
  phỏng đã khóa sha256 trước mọi job tương ứng:
  - chỉ chèn (U, C-120, C-300, PS-300, DLHZ-120): 55/55 ô-nhánh, 4.972/4.972 khách;
  - Elmi (RCE35, EOHZ35): 22/22, 1.966/1.966 khách. Luật thật sự ràng buộc: RCE35 khác U ở 873/1.072 khách;
  - G2 (G2-U, G2-RCE35, G2-EOHZ35): 33/33, 2.994/2.994 khách, 142/142 lần dời, tối đa 1 lần mỗi quyết định; 24.439 quyết
    định evidence 1.3.0, 0 lỗi kiểm.
- Lần đầu G2 chỉ được 30/33: 3 job dừng với `RBWP7_LOCKED_LEG_MISMATCH`, chính là nước của bộ mô phỏng mà FleetPy không cho
  làm trong bước đó.
  - Điều kiện của chủ: dwell = 0 ở cả hai phía; tái hiện 3 ca bằng test trước khi sửa.
  - Sau khi sửa, chạy lại cả 33 job G2 và 80 job chỉ chèn: 113/113 đạt. 30 job G2 đã khớp trước đó giống từng khung
    (66.951). 80 job chỉ chèn giống hệt, trừ hai trường nguồn gốc ở khung 2 (`binarySha256`, `coreCommitSha`).
- Cổng phát lại:
  - Runner A0.1: 58/58 job, 43.154/43.154 khung giống từng byte;
  - Runner Elmi: 58/58 job;
  - Runner G2 khi tắt G2: 80/80 job, 59.330/59.330 khung, ở cả ba bản v1–v3.
- Hồ sơ:
  - `E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/` (`BRIDGE-REPORT.md`, `AMENDMENTS.md` A1–A9);
  - đầu ra `C:/RideBoundData/research/via-bridge-man-v1/`;
  - tổng hợp `C:/RideBoundContinuation/2026-10-23/TONG-HOP-2026-10-23.md`.

**Consequences và giới hạn:**
- Cầu nối chỉ chứng minh **độ trung thực cài đặt**: trên 11 ô Manhattan nhỏ, bộ mô phỏng cho đúng kết quả của RideBound thật.
  Nó không chứng minh G2 hay luật C có lợi trên Via.
- Cầu nối chạy ở chế độ adapter-identity. Bằng chứng Via 55 ngày của công ty chạy ở chế độ plain, và hai chế độ khớp từng
  khách ở 0/20 ô, nên phải chạy lại Via ở chế độ adapter (cần tệp từ máy công ty). Ô Via P512 lớn hơn nhiều, chưa chứng minh ở
  cỡ đó.
- Khác biệt mô hình đã biết: bộ mô phỏng không có khóa theo bước của FleetPy. Adapter nay xử lý ca chặng phục vụ 0 s; dwell khác
  0 chưa được kiểm.
- Trước lần chạy lại v4, mọi job G2 nạp adapter của `E:/Code/RideBound-deadline` (lỗi launcher, AMENDMENTS A8). Số 30/33 lần
  đầu đo với adapter cũ; số 33/33 đo với adapter của nhánh này.
- Nhánh EOC của bộ mô phỏng dùng `floor`, còn RCE/EOHZ dùng `round`; EOHZ35 của RideBound chỉ được so với EOHZ35 của bộ mô
  phỏng.
- Trên cây sạch sau commit, `StrictBagItBundleTests` có 1 lỗi đã biết (nó đòi worktree có thay đổi, ADR-079).
- Không authorize `RB-WP14R-009..012`, WP15 hay H7. Không push; cây chính `E:/Code/RideBound` không đổi.
- Các SHA trong ADR này là SHA trước ngày 25/10. Ngày đó lịch sử được viết lại, chỉ đổi tác giả và thông điệp (ADR-082).
  Bảng SHA cũ → mới nằm ở ADR-082.

### ADR-082 — 2026-10-25 — Proposed (thăm dò; đóng cầu nối giai đoạn 1; nhánh `research/p1-fix`)

**Context:**
- Bộ mô phỏng wf15 của công ty sinh ra mọi số so sánh với Elmi (wf14, wf15). Cầu nối ADR-081 mới phủ bộ mô phỏng cũ (wf5) và
  chưa phủ ba thứ: hàng rào chặt EO35 (ép xe), G2 giới hạn k xe nhận, ô lớn hơn.
- Chủ duyệt chạy bộ mô phỏng wf15 từ gói công ty (23/10), và duyệt sửa adapter ở ca "xe cho" kèm 4 điều kiện của A8 (24/10).

**Decision:**
1. **Sửa adapter ở ca "xe cho"** (`fleet_control.py`, `_started_first_stop`).
   - Khi chặng FleetPy đang khóa là chặng "cho khách lên" dài 0 s, và chặng đầu của Runner trùng nó ở mọi trường trừ cờ khóa,
     adapter dùng điểm dừng FleetPy đã bắt đầu thay cho bản sao của Runner. Hai bản là cùng một điểm dừng.
   - Nhánh này chỉ chạy đúng ở chỗ mà mã cũ đã từ chối (`RBWP7_LOCKED_LEG_MISMATCH`). Không đổi RideBound, không đổi bộ mô
     phỏng.
2. **Viết lại tác giả commit** (chủ quyết ngày 25/10).
   - Chuỗi `7435661..research/g2-core` được tạo lại với cùng cây và cùng ngày.
   - Tác giả là chủ nghiên cứu; bỏ dòng Co-Authored-By.
   - Lý do: 4 commit nhập từ máy công ty mang e-mail máy công ty trong metadata.
   - Nguồn gốc vẫn ghi ở đây và ở ADR-080/081: các patch A0.1 và ADR-080 được viết trên máy công ty ngày 20–21/10.
   - Bảng SHA cũ → mới:
     - `b43ef5b` → `3afcd17`
     - `66558f2` → `176f5dd`
     - `365caac` → `9b633d9`
     - `365b502` → `2eadfb7`
     - `6d6fa47` → `18978dd`
     - `4a24810` → `1190518`
     - `115a4fc` → `c6588f8`
     - `fa676b4` → `ef2b02f`
     - `937bb31` → `e1c6f2b`
   - Ref cũ giữ ở `refs/backup/pre-author-rewrite-20261025/`. Cây của cả 5 nhánh giống hệt bản cũ.
3. **Đối thủ chính** (chủ quyết ngày 25/10, thay câu "đối chứng chính là EO35" của ADR-081):
   - EOHZ35 (Elmi Ordinary, hàng rào nới) và SHHZ là đối thủ chính;
   - EO35 và SHF luôn được báo cạnh trong cùng bảng.
   - Lý do: trong thế giới thời gian đi cố định của bài Elmi, hai hàng rào trùng nhau. Còn "ép xe" khi giao thông vượt hàng
     rào là cách hình thức hóa thêm vào, không có trong bài Elmi (kiểm toán công ty 23/10, §2.4).

**Evidence:**
- **Bộ mô phỏng wf15** (`engine.js` sha `908a2961…`) tái tạo đúng từng byte cả 4 tệp dự đoán đã khóa của cầu nối ADR-081.
- **Giai đoạn 1: 117/117 ô-nhánh khớp từng khách, 11.521/11.521 khách.** Mọi dự đoán được khóa sha256 trước job tương ứng.
  - EO35 và DLS-120 với `forced-reference-v1`, có và không có G2: 44/44. Xe bị ép thật: 35–534 quyết định mỗi nhánh.
  - G2 chỉ xét 3 xe nhận (`maximumTargetVehicles` 3): 33/33. Giới hạn này đổi kết quả ở 93–166 khách.
  - Ô 128 yêu cầu × 10 xe: 40/40. Với 10 xe, giới hạn 8 xe nhận không đổi khách nào.
- **Bản sửa adapter:**
  - test `test_in_progress_donor_leg.py` (4 test) hỏng trước khi sửa và đạt sau khi sửa;
  - bộ test adapter: 461 test, đúng 6 lỗi đã biết.
- **Chạy lại 108 job G2 sau khi sửa (điều kiện 3):**
  - 106/106 job đã khớp trước đây vẫn giống hệt: 73 giống từng khung; 33 chỉ khác hai trường nguồn gốc ở khung 2;
  - 108/108 khớp bộ mô phỏng;
  - kết quả ở `compare/fix1-2026-10-24/condition3.json`, sha `5dc049f3…`.
- **Hồ sơ:**
  - `E:/Code/Report_INT3508/ridebound-scratchpad/bridge-phase1-2026-10-23/PHASE1-LOG.md` (P1–P9);
  - `C:/RideBoundContinuation/2026-10-24/BAO-CAO-GIAI-DOAN-1.md`.

**Consequences và giới hạn:**
- Vẫn chỉ là **độ trung thực cài đặt**, không phải lợi ích của luật.
- Ô lớn nhất làm được là 128 yêu cầu và 96 nút (hợp đồng dữ liệu v1).
- Giới hạn 8 xe nhận chỉ được kiểm gián tiếp qua k = 3. Trên ô Via 62 xe phải kiểm lại.
- Dwell khác 0 xem ADR-083.
- Số liệu Via của công ty chạy ở chế độ plain; cầu nối chạy ở chế độ adapter. Việc kế tiếp là kiểm toán §6-P5: chạy lại một
  lát Via ở chế độ adapter.
- Không push; cây chính không đổi.

### ADR-083 — 2026-10-25 — Proposed (thăm dò; thời gian phục vụ ở điểm dừng; nhánh `research/p1-dwell`)

**Context:**
- Kế hoạch máy nhà 23/10 của công ty muốn bắc cầu thêm thời gian dừng 24 s mỗi điểm.
- RideBound và adapter đều cứng dwell = 0 (ADR-081).

**Decision:**
1. Thêm tùy chọn `candidateGeneration.stopServiceDurationMs` (WP4):
   - số nguyên không âm, tối đa 3.600.000;
   - vắng khóa nghĩa là 0, và hash nội dung cấu hình giữ nguyên;
   - điểm đón và điểm trả mới chèn mang thời gian phục vụ đó.
2. **Không dùng tùy chọn này khi chạy thật cho tới khi lõi có trạng thái "xe bận tới T".** Chủ chọn phương án B ngày 25/10:
   ghi giới hạn; mọi so sánh trên RideBound thật dùng dwell 0; dwell 24 s chỉ chạy ở bộ mô phỏng (lưới độ bền §6-N4 của kiểm
   toán).

**Evidence:**
- `dotnet test RideBound.slnx`: 1230/1230. Có 2 test `StopServiceDurationTests` và 1 test cấu hình: vắng khóa thì hash giữ
  nguyên; số lẻ thì bị từ chối.
- Cổng phát lại: Runner mới, khi tắt tùy chọn, cho đúng từng byte trên 228/228 job đã ghi, 176.074 khung.
- Thử 6 job với dwell 24 s: cả 6 hỏng, kể cả nhánh chỉ chèn.
  - Ví dụ: xe 1 tới nút 94 lúc 1.222,4 s và cho khách lên tới 1.246,4 s. Yêu cầu mới lúc 1.235 s làm Runner lập tuyến chạy
    ngay, và FleetPy từ chối.
  - Nguyên nhân: `RouteScheduleProjector.cs:62-128` luôn bắt đầu từ "bây giờ" tại vị trí xe. RideBound không có khái niệm xe
    bận tới giờ T, còn bộ mô phỏng có (`busyUntil`).
- Hồ sơ: `PHASE1-LOG.md` P7–P9; `DWELLOFF-GATE-SUMMARY.json`, sha `483aebc9…`.

**Consequences:**
- Khóa luận phải ghi: RideBound chỉ được kiểm với thời gian dừng 0 s.
- Làm thật đòi sửa nhiều tầng, ước 1–2 ngày cộng thời gian chủ duyệt mã:
  - trạng thái xe "rảnh từ T";
  - bộ dự báo lịch;
  - trường giao thức giờ hết phục vụ (đổi hợp đồng);
  - adapter;
  - mở rộng A8 cho chặng có thời lượng.
- Không push; cây chính không đổi.

### ADR-084 — 2026-10-25 — Proposed (thăm dò sau kiểm toán công ty 23/10; không đổi mã)

**Context:**
- Đã đọc toàn bộ tài liệu công ty. Danh sách việc còn thiếu:
  `C:/RideBoundContinuation/2026-10-24/doc-toan-bo/CON-THIEU-SO-VOI-KE-HOACH-CONG-TY.md`.
- Ngày 25/10 chủ duyệt "thực hiện toàn bộ", và cho phép chạy runner wf15 của công ty cho P5 và các việc trong bảng.

**Decision:** ghi nhận các phép kiểm dưới đây là bằng chứng thăm dò. Không có phép nào là xác nhận, và không đổi mã RideBound.

**Evidence** (hồ sơ dưới `E:/Code/Report_INT3508/ridebound-scratchpad/`):
- **§6-P5, lát Via ở chế độ adapter + half-even** (`p5-via-adapter-2026-10-25/P5-RESULT.md`):
  - cây chạy runner wf15 ở nhà khớp 53/53 tệp trong danh sách hash của công ty; APSP khớp `4ae39087`;
  - cổng 0: **840/840 dòng plain giống hệt dòng xác nhận của công ty**;
  - đổi sang chế độ adapter thì các phép so chính giữ dấu trên 10 ngày: G2 RCE35 − EOHZ35 đảo hứa −14,9 → −16,1 mỗi ngày,
    served −4,1 → −3,5;
  - 7/84 ô bị đánh dấu: hiệu late60 của INS RCE35 − EOHZ35, và các hiệu nhỏ EOHZ35 − EO35. Không dùng hai loại hiệu này.
- **§6-P7, đối chiếu TLC** (`p7-tlc-2026-10-25/P7-RESULT.md`):
  - HVFHV 3/2019 và 9/2019 tải ở nhà, dựng lại và so với `via_windows.csv`: 0 dòng lệch mỗi chiều (66.856 và 51.219 dòng);
  - `rid` của công ty đánh theo thứ tự (giờ yêu cầu, khu đón, khu trả, quãng đường, thời gian).
- **§6-P4(a), hạn chót nới dạng của sinh viên** (DLS-120 overlay, phục hồi `no-worse-than-reference-v1`, không có cờ
  `dropEtaDeadlineRelaxed`; `bridge-phase1-2026-10-23/compare/nw-2026-10-25/final-compare.json`):
  - **11/11 ô, 976/976 khách trùng dự đoán DLHZ-120 đã khóa ngày 21/10**, và trùng job DLHZ-120 thật;
  - tổng cầu nối: **238 cặp kịch bản–luật**.
- **Kiểm đồng nhất** (`identity-2026-10-25/IDENTITY-RESULT.md`): RC = NC = OC khi chỉ chèn ở 11/11 ô; khi giao thông
  phẳng, RCE35 = EOHZ35 ở 10/11 ô.
- **Bằng chứng bộ giải** (`bridge-phase1-2026-10-23/SOLVER-EVIDENCE-RESULT.md`): 300 job, 232.103 quyết định, 100%
  `ORTOOLS_OPTIMAL`; trần 100 ứng viên và ngân sách chưa lần nào ràng buộc.
- **§6-N1, dung sai hàng rào** (`N1-RESULT.md`): 1 s xóa 33–54% khoảng cách phục vụ SHRC − SHF; 60 s làm khoảng cách đảo
  dấu, đổi lại dời giờ 43–66 khách bảo đảm mỗi ngày.
- **§6-N2, đường cong α** (`N2-RESULT.md`): với gán lại, luật C ít đảo hứa hơn hàng rào nới 33–55% ở mọi α từ 10% tới
  100%, đổi lại phục vụ ít hơn 0,2–1,2%.
- **Bảng 60 ngày tính lại độc lập** (`TABLES-2x3-60days.txt`) trùng mọi số của kiểm toán. Ví dụ G2 RCE35 − EOHZ35: served
  −2,82 [−4,38; −1,27]; đảo hứa −16,68 (60/60 ngày).

**Consequences:**
- Khóa luận được sửa trên nhánh riêng của kho khóa luận (`thesis/audit-fixes-2026-10-25`, worktree
  `C:/Code/Report_INT3508-audit`):
  - sửa lỗi sự thật (§6-P2);
  - bản nháp mục so sánh với Elmi và cầu nối (§6-P1, §6-P3).

  Chủ duyệt trước khi đưa vào bản chính.
- Còn mở:
  - sinh viên duyệt mã (P4) và các việc P6, N9;
  - lần vết các ô lệch của E0 (N5);
  - N3 và N4 mới có kết quả thăm dò a5 của công ty.
- Không push; cây chính không đổi.

### ADR-085 — 2026-10-26 — Proposed (thăm dò; RideBound thật trên dữ liệu Via ở cỡ Via; phép hòa vốn; nhánh `research/via-zone-2026-10-26`)

**Context:**
- Lệnh của chủ ngày 26/10 (khoảng 00:45): (1) làm phép hòa vốn; (2) chuẩn bị và chạy vài job RideBound thật trên dữ liệu
  Via; (3) commit code C#, script và những thứ cần thiết, không nặng, lên một nhánh của `RideBound-Code` để máy công ty
  pull về.
- Trước đó RideBound thật chỉ chạy tới 128 yêu cầu / 10 xe trên dữ liệu Manhattan cũ (cầu nối 238 cặp, ADR-084); dữ liệu
  Via chỉ chạy trong bộ mô phỏng.

**Decision:**
- **Gộp điểm theo khu TLC.** Job thật dựng đồ thị đầy đủ trên mọi điểm của kịch bản và kiểm từng cạnh bằng bộ tìm đường
  của FleetPy (`actual_fleetpy_medium_preflight.py:113-155, :437-447`); mỗi khung giờ gửi toàn bộ bảng cặp điểm cho
  Runner. Ô Via của công ty có 824–858 điểm (khoảng 680.000 cạnh), không chạy được trong thực tế. Chuyến Via của TLC chỉ ghi
  tới khu; công ty rải điểm trong khu. Bộ chuyển `convert_via_zone.py` đưa mỗi điểm về một điểm đại diện của khu (medoid),
  giữ toàn bộ cầu (509/508 yêu cầu, bỏ 3/4 chuyến nội khu) và 62 xe trên 60/58 điểm. Đây là họ kịch bản mới, không phải ô
  của công ty; nó kiểm bộ mô phỏng ở cỡ Via, không tái tạo số P5.
- Bộ chuyển là script nghiên cứu, không phải bộ chuẩn hóa C#; bộ chuẩn hóa C# và giới hạn 128/32/96 của nó không đổi. Job
  thật nhận số lượng do driver khai báo (`actual_fleetpy_medium_preflight.py:809-817`, có sẵn).
- **Sửa đổi A1 (trước khi chạy lại):** với WP4 đã bắc cầu, job thử đầu tiên dừng an toàn sau 4,1 phút:
  `INTERNAL_ERROR: Pre-solve counters must fit their stage budgets ...`. Ngân sách sinh phương án cấp quyết định
  (10.000) là tổng công việc của mọi xe cộng G2 (`SolverBackedRidePoolingPolicy.cs:520-524`) và bị kiểm cứng
  (`CandidateSelectionExecution.cs:99-112`); 8–10 xe chưa lần nào chạm (232.103 quyết định), 62 xe thì vượt. Cấu hình
  `wp4-via-*-a1.json` chỉ nâng bốn ngân sách cấp quyết định và bộ giải (sinh và kiểm 5.000.000; bộ giải 10.000.000 đơn vị /
  30 s tất định); trần từng xe (100 phương án, 10.000 đơn vị) và mọi luật giữ nguyên. Không đổi mã.
- **Sửa đổi A2, A3:**
  - Job thử với A1 chạy hết mọi quyết định, tới giây mô phỏng 9.000, trong 87,6 phút. Nó hỏng ở bản chốt trạng thái cuối
    cùng: Runner trả `INTERNAL_ERROR` chung (`RunnerHost.cs:96-106`). RAM của runner lúc đó là 1.568 MiB, sát trần heap
    2 GiB của driver; nhiều khả năng là thiếu heap, nhưng chưa chứng minh.
  - Phép so từng khách cần bản chốt này (`bridge_riders.py:57-60`).
  - Đợt chính đã mở sớm theo A2 nên được dừng lại; mọi đầu ra dở dang được giữ.
  - A3 chỉ đổi ba thứ: trần heap lên 6 GiB; một bản vá chẩn đoán in stderr của Runner khi hỏng; một đợt `runA3` gồm
    12 job xếp theo thứ tự ưu tiên, các lần nhận job cách nhau 10 phút. Không đổi mã, cấu hình luật hay dự đoán.

**Evidence** (hồ sơ `E:/Code/Report_INT3508/ridebound-scratchpad/`, bản chép trong
`research/continuation/2026-10-26/kits/`):
- **Phát hiện:** với ngân sách mặc định/đã bắc cầu, RideBound không chạy được một ô cỡ Via; nó dừng theo thiết kế (không
  lặng lẽ bỏ phương án). Đầu ra hỏng giữ ở `E:/RideBoundData/research/via-real-v1/runs/trial/`.
- **Dự đoán bộ mô phỏng** (`via-real-2026-10-26/emulate_via.js`, wf15) cho 6 luật × 2 ô khóa sha256 lúc
  2026-10-25T18:06:24Z, trước mọi job; khoảng 495 quyết định mỗi job, 362–464 khách được phục vụ, G2 dời 6–33 khách.
- **Phép hòa vốn** (`breakeven-2026-10-26/BREAKEVEN-RESULT.md`; kế hoạch khóa trước, sha `2e82c711…`; chạy lại 2.520 lượt
  bộ mô phỏng, trùng tuyệt đối P5/N2): với G2 và cùng số khách phục vụ, so với EOHZ35, luật C bớt 13,5 khách bị đảo hứa và
  thêm 12,7 khách bị điều phối dời muộn quá 1 phút mỗi ngày; **w\* = 0,94** (bootstrap theo ngày 0,46–1,61). Qua α 10–75%,
  w\* 0,3–1,2; không mức α nào luật C hơn trên cả hai thước đo. Khi chỉ chèn: không phân biệt được. Phần "muộn" do điều
  phối, không do tắc đường.

- **Trạng thái lúc push lần 1 (26/10, khoảng 03:00):**
  - đợt `runA3` (12 job) đang chạy;
  - chưa có job Via nào qua tới bản chốt cuối, nên **chưa có kết quả so từng khách**;
  - kết quả sẽ ghi vào `kits/via-real-2026-10-26/VIA-LOG.md` và một lần push sau.

**Consequences:**
- Câu "RideBound thật chưa chạy ở cỡ Via" chỉ được sửa theo kết quả so từng khách của các job `runA3`. Phần Shadow vẫn chỉ
  có trong bộ mô phỏng.
- Bộ máy chạy job hiện tại tốn khoảng 1,5 giờ và khoảng 1 GB nhật ký cho mỗi job Via chỉ chèn; job G2 lâu hơn. Đây là giới
  hạn thực tế cần ghi trong khóa luận nếu dùng.
- Khóa luận không đổi trong ADR này.
- Nhánh `research/via-zone-2026-10-26` đặt trên `research/p1-dwell`, thêm `research/continuation/2026-10-26/` (script,
  cấu hình, kết quả nhỏ; không có dữ liệu lớn và không có bộ mô phỏng của công ty) và tài liệu này; push lên
  `RideBound-Code` theo lệnh của chủ. Repo đang công khai.

## Cập nhật khóa luận — 2026-10-16T00:09:00.907016+07:00

Người dùng yêu cầu cập nhật toàn bộ nội dung theo cách đọc tự nhiên, bổ sung
đánh đổi giữa hai thứ tự chọn phương án và giải thích lý do cần phép so bổ sung.
Đây là quyền cập nhật bản luận văn thật, thay trạng thái chờ duyệt trước đó.

Đã sao lưu nguyên thư mục luận văn cũ tại `C:/RideBoundContinuation/2026-10-15/actual-thesis-backup-20261016-000900` (có manifest từng tệp),
rồi cập nhật đúng `E:/Code/Report_INT3508/ridebound/`. Bản PDF cuối **77 trang**,
SHA-256 `26c8dea1334e68091b421a831bf14e809e76a396e0444b75398d662a8921dd69`. Bộ nguồn, logo, README và hồ sơ kiểm đi kèm khớp bản đã kiểm.
Biên nhận: [thesis-v5-deployment.json](C:/RideBoundContinuation/2026-10-15/thesis-v5-deployment.json).

Đã đồng bộ tóm tắt Việt/Anh, Chương 1, 3, 4, 5 và phụ lục. Dùng “ưu tiên ổn
định” và “ưu tiên chi phí kế hoạch”, bỏ cách gọi bộ chọn cũ/mới và tên giai đoạn
nội bộ khỏi nội dung PDF. Bảng bốn cấu hình giữ cả served/shared, trễ, đón muộn,
khách bị sửa giờ và sửa lặp. Nêu rõ +26 served/+119 shared cùng C30 đổi lại
33→106 khách bị sửa giờ, 0→2 bị sửa lặp, 43→44 đón muộn, 10→11 forecast late60.
Phép so kiểm xem giới hạn còn giữ được khi bỏ xếp hạng ổn định; không kết luận
selector ban đầu kém hơn, không nhận pure causal isolation hay trần30 tối ưu.

Giữ đối chứng hạn chót/đệm bất lợi, hạn chế dự báo, H6/E1 và điểm dừng H7.
Phân biệt bảy lịch sử ở trần giả định120 với một cumulative-only prune C30;
không suy thành outcome advantage của cộng dồn so per-step. Kế hoạch chạy thêm
bốn cấu hình không còn bị ghi là chưa làm/hướng tương lai.

Tectonic exit0; không Overfull, undefined/duplicate references hoặc TeX errors;
không ký tự ngoài trang. Đã render và xem bố cục toàn bộ 77 trang, xem riêng
các trang quan trọng ở cỡ lớn; sửa chồng chữ Hình3.3, mục lục/danh sách bảng tràn
một dòng và nới bảng đánh đổi. Chi tiết [visual review](C:/RideBoundContinuation/2026-10-15/thesis-v5/chung-cu/VISUAL-REVIEW.md).
47/47 kiểm bảng bổ sung và các chênh lệch chính đạt;13 mục manifest kết quả gốc
khớp. Đây là kiểm từ extraction, không tự nhận giải mã raw/forecast độc lập
hoặc tính lại mọi số lịch sử trong toàn luận văn.

Không sửa mã C#, chạy lại mô phỏng/analysis, huấn luyện mô hình, đổi seal/env,
commit/push hoặc bật timer. Baseline .NET 1.033/1.034 với1knownfailure giữ nguyên.
File báo cáo thực tập đã kiểm băm trước/sau không đổi; hai repo E giữ chỉ đọc.
Các tệp khác trong thư mục luận văn không nằm trong payload được giữ nguyên.
Next: người dùng đọc bản cuối; công việc nghiên cứu/thực thi mới theo yêu cầu riêng.

## Rà soát mức sẵn sàng bảo vệ — 16/10/2026

Theo yêu cầu người dùng, đã truy vết bản PDF77trang hiện tại tới nguồn LaTeX,
mã chuẩn hóa/dự báo/thống kê và đầu ra đã giữ.22tệp payload và13mục manifest
kết quả khớp; số gộp bốn cấu hình khớp; hash5tệp chuẩn hóa hiện tại trùng
34517a83…4d90của49report, mỗi report108yêu cầu. Không sửa PDF/nguồn luận văn,
core, frozen inputs, code phân tích, output cũ, env, Git hay lịch tự động.

**Trạng thái:** bản thảo đầy đủ và nghiên cứu bổ sung đã xong; chưa đóng
“toàn bộ khóa luận/bản nộp đã hoàn tất”. Review chỉ ra việc cần hoàn thiện:
R01công khai60tổ hợp→49dựng được→34test/15hiệu chỉnh và11tổ hợp bị loại trước
mô phỏng do giới hạn96nút/quy tắc chuẩn hóa; R02khoảng95% theo8nhóm ngày–khung
còn phụ thuộc chéo và chỉ3ngày bật dự báo; R03cần phân biệt lớp lời hứa phát
lại với tích hợp dự báo vào điều phối (29/32hoàn tất,3dừng;34/1471lời hứa đón
hiển thị vượt hạn); R04có chênh với hướng dẫn UET công khai, cần xác nhận mẫu
áp dụng trước chỉnh hình thức. Không coi mọi hạn chế là lỗi thuật toán.

Chưa có đối chứng trực tiếp cộng dồn-vs-từng-lần, xác nhận mới trên ngày chưa
xem, lợi nhuận/realizedcost, SLAđộ trễ mỗi quyết định hoặc artifact tái lập từ
máy sạch được xác nhận trong lượt này. Đây là giới hạn của mức claim, không
tự cấp quyền chạy nghiên cứu mới. H6/E1/cross-system cũ giữ đúng verdict;
không nâng WP12, không Stage2. Baseline.NET1.033/1.034với1knownfailure giữ nguyên.

Next: bổ sung mô tả chọn mẫu; hạ/giải thích suy luận thống kê; ma trận trạng
thái thành phần và phạm vi đóng góp; chốt mẫu áp dụng với GVHD/khoa; recipe,
slide và demo đúng phiên bản. Chi tiết13điểm, câu hỏi bảo vệ và thứ tự xử lý:
[báo cáo rà soát](C:/RideBoundContinuation/2026-10-16/thesis-readiness-audit/THESIS-READINESS-AUDIT.md).

**Consequences:** Không đổi implementation/protocol, baseline/panel, H6/E1/
WP14/WP14R hoặc novelty boundary. C30 GiB riêng Stage1b theo quyết định mới
15/10 thay C45; E40 GiB, AC-only và reserve410 MiB/job giữ nguyên. Tác tử sửa/review/judge mới đều là Codex; không gán review mới cho
Claude/Antigravity. Không có quyền commit/push mới trong lượt này.

## 8. Work package tracker

| WP | Trạng thái | Bắt đầu | Kết thúc | Evidence |
|---|---|---|---|---|
| WP0 Scaffold | Complete | 2026-07-28 | 2026-07-28 | build + 8 RideBound + 25 backend + 7 frontend tests |
| WP1 Contracts | Complete; Q1 Release revalidated with host-policy exception | 2026-07-29 | 2026-07-29 | ADR-014–017 + 157/157 closure + WP1 revalidation 161/161 + replay/hash proof |
| WP2 Online baseline | Complete; physical/B1 gate, Debug 333/333; Release host-policy exception recorded | 2026-07-29 | 2026-07-30 | ADR-018–020 + Debug 333/333 + Release bundles + two-process tiny replay |
| WP3 Ledger/certificate | Complete; `001..014` DONE; Debug 414/414 | 2026-07-31 | 2026-08-02 | ADR-021/022 + `tasks/28` + full-solution 414/414 + WP3 process/checkpoint replay |
| WP4 Algorithms/solver | Complete; `001..014` Done; Q2 mechanical gate closed | 2026-08-03 | 2026-08-03 | ADR-023/024 + independent oracles + named policy/solver/Runner path + 557/557 + final review |
| WP5 BeGo integration | Complete; `001..014` Done; Q3 mechanical gate closed | 2026-08-05 | 2026-08-09 | ADR-025 + durable adapter/rollout + paired bundle + independent evidence + source/claim review; BeGo 154/154 Debug/Release |
| WP6 Benchmark harness | Complete; `001..014` Done; common mechanical harness gate closed | 2026-08-09 | 2026-08-13 | ADR-026..036 + contract v1.0.6; fresh tiny A + medium H/I exact-source semantic reproduction, strict external verify, final review, 770/770 |
| WP7 FleetPy | Complete; `001..014` Done; mechanical Layer-2 closed | 2026-08-13 | 2026-08-17 | ADR-037/038/039 + `tasks/35`/`tasks/36` + Runner v8 actual B1/C1 preflight/lifecycle/tiny/medium + external verifier |
| WP8 Pilot/prereg | Complete; `001..014` Done | 2026-08-18 | 2026-08-21 | ADR-040/041/043/044 + frontier 25/25 + oracle/verifier + fixed panel/amendments; H6 kế thừa freeze chain |
| WP9 Main experiments | Complete; `001..009` Done; negative result | 2026-08-21 | 2026-08-23 | ADR-045..049 + H6 100/100 raw bundles verified; both service gates FAIL, burden gates PASS; robustness descriptive; breach bridge post-outcome verified |
| WP10 Cross-system | Complete; `001..010 Done`; negative capability result | 2026-08-23 | 2026-08-23 | ADR-050/051 + exact RidePy/Linux image + same-Runner canonical PASS; representative subset FAIL CLOSED; Layer 3 not established |
| Post-WP10 assurance | Complete | 2026-08-23 | 2026-08-23 | ADR-052 + WP1–WP10 final review + 3+3 process optimization benchmark + 12-page rendered PDF |
| WP11 Product UX | Not started | — | — | — |
| WP12 Paper/release | Not started | — | — | — |
| WP13 Post-H6 mechanism diagnostics | Complete; `001..013 Done` | 2026-08-23 | 2026-08-24 | ADR-053..065 + `tasks/41`/`tasks/42`; closure manifest 4.463 byte SHA `4e410e23…58a72c9c`; bảy exit gate pass, zero unresolved P0–P2; H6 immutable; Debug 860/860; Python 205/205 |
| WP14 Exploratory ablation/Pareto | Freeze v1 stopped fail-closed; `001..005/008 Done`, `006/007 Deferred`, `009 Closed — FAIL`, `010..014 unauthorized` | 2026-08-24 | 2026-08-26 | ADR-065..070 + `tasks/43`/`tasks/44`; exact freeze SHA `1ce26ff0…37a55`; paired dry-run 1 valid/1 partial, no retry/replacement; no matrix/frontier; Debug 908/908, Python 242/242 |
| WP14R Resilient execution successor | `001..007 Done`; **`008 Closed — FAIL CLOSED`** do defect freeze v2 (`PROCESSOR_ARCHITECTURE` thiếu trong `inheritedEnvironmentNames`) làm child không verify được receipt; job 1 exhausted sau 32,5 s, zero simulation, zero outcome; `009..012 unauthorized`; cần freeze v3 do owner quyết | 2026-08-26 | — | ADR-071/072 + `tasks/45`/`tasks/46`; canonical freeze SHA `6b340108…a31237`; stable recovery wrapper + host/power/quiescence gate; targeted 95/95, Python 337/337, .NET 908/908; authorization preflight typed `POWER_SOURCE_NOT_AC`, zero job |
| WP15 Lifecycle commitment v2 | Roadmap-level; refinement draft only, **unauthorized to run** | — | — | `tasks/47` + `research/wp15-commitment-design-new-paper-evidence-2026-08-28.md`; 4 paper mới/165 trang; ba trục B/C/D có điều kiện phủ định; ticket chỉ sinh sau verified WP14R frontier và ADR riêng |
| WP16–WP20 | Roadmap-level only | — | — | Không ticket hóa trước verified WP14R frontier/closure gate |

## 9. Change history

- 2026-10-26 (`research/via-zone-2026-10-26`, trên `research/p1-dwell`; không đổi mã; push lên `RideBound-Code` theo lệnh
  của chủ): ADR-085. Bộ chuyển Via theo khu TLC và job RideBound thật ở cỡ Via (sửa đổi A1 về ngân sách cấp quyết định);
  phép hòa vốn w\* = 0,94 (G2, cùng số khách, so EOHZ35); script và kết quả nhỏ của tháng 10 ở
  `research/continuation/2026-10-26/`.
- 2026-10-25 (`research/p1-dwell`, chỉ tài liệu): ADR-084. Các phép thăm dò sau kiểm toán gồm P5, P7, nhánh hạn chót của
  sinh viên (11/11), kiểm đồng nhất, bằng chứng bộ giải, N1 và N2. Cầu nối nay là 238 cặp kịch bản–luật.
- 2026-10-25 (nhánh thăm dò `research/p1-dwell`, trên `research/p1-fix`; chưa push): thêm tùy chọn
  `stopServiceDurationMs`, mặc định tắt (ADR-083). Dwell khác 0 bị chặn vì lõi không có trạng thái "xe bận tới T"; chủ chọn
  ghi giới hạn.
- 2026-10-25 (nhánh thăm dò `research/p1-fix`; chưa push; cây chính không đổi): đóng cầu nối giai đoạn 1 (ADR-082).
  - Bộ mô phỏng wf15 khớp RideBound thật trên 117/117 ô-nhánh: EO35 chặt, DLS-120, G2 k = 3, ô 128 yêu cầu.
  - Sửa adapter ca "xe cho"; chạy lại 108 job G2: 106/106 giữ nguyên, 108/108 khớp.
  - Viết lại tác giả chuỗi `7435661..research/g2-core`: cùng cây, ref cũ được sao lưu.
  - Chủ chốt EOHZ35/SHHZ làm đối thủ chính, luôn báo kèm EO35/SHF.
- 2026-10-23 (nhánh thăm dò `research/via-ref` → `research/via-core` → `research/g2-core`; chưa push; cây chính không đổi):
  cầu nối RideBound với bộ mô phỏng của công ty theo kế hoạch ngày 21/10. Mọi kết quả mang nhãn thăm dò; không authorize
  `RB-WP14R-009..012`, WP15 hay H7. Không tuyên bố mới: dời khách đang chờ và ngân sách tỉ lệ slack (Elmi et al. 2026) đều
  đã có.
  - Bốn patch của máy công ty (ADR-080 và A0.1) được nhập nguyên văn, giữ tác giả và ngày gốc; mỗi cặp dựng lại đúng cây đã
    test (`086beacc`, `c112fdbb`).
  - Ngân sách Elmi theo từng khách (`6d6fa47`, `4a24810`) và G2 (`115a4fc`, `fa676b4`); xem ADR-081.
  - Cầu nối Manhattan khớp bộ mô phỏng từng khách: chỉ chèn 55/55 ô-nhánh, Elmi 22/22, G2 33/33.
  - Nhật ký chạy: `E:/Code/Report_INT3508/ridebound-scratchpad/via-bridge-2026-10-22/AMENDMENTS.md` (A1–A9).

- 2026-10-06 (nhánh thăm dò `research/tier3-no-worse`; không đổi code trong kho): hứa giờ có tính trước tắc đường. Mọi
  kết quả mang nhãn thăm dò; không authorize `RB-WP14R-009..012`, WP15 hay H7. Không tuyên bố mới: dự báo giờ đi theo giờ
  trong ngày, đệm lời hứa và hạn chót đều đã có.
  - Thiết kế do vòng tranh luận ba lập trường cộng trọng tài chốt. Kế hoạch niêm phong `a5524912…7337` và push `33d8071`
    trước mọi job; phụ lục A1 push `df7f893` trước khi tính Họ B. Nguồn: `docs/benchmarking/tier3-forecast/`.
  - Họ A (chính, không chạy mới; nhãn bắt buộc "đo lường có đăng ký trước trên các ngày-khung đã được xem trước"): hiển thị
    lời hứa D = max(p, kéo dãn phần còn lại theo đà tăng dự báo từ các ngày trước đó, bỏ 15/11), phát lại trên 170 job T3.8.
    Trên 21 ô ngày thường, C-30: trễ > 60 s so với lời hứa đầu 6,01% → 0,17% (khung 7h 14,44% → 0,27%; ΔY −5,84 điểm %,
    khoảng bootstrap cụm 95% [−8,78; −3,23]); dao động hiển thị 7,48% → 1,06%; lời hứa dài thêm 13,7 s (khung 7h 27,8 s).
    Đệm cố định cùng giá chỉ xuống 1,94%. A1 và A2 đạt; A3 đạt theo bootstrap nhưng mang nhãn "mong manh với suy luận ít
    cụm" (cluster-t [−3,62; +0,07] điểm %). Độ nhạy bỏ 12/11 (ngày nghỉ bù Veterans Day, là nguồn của mọi dự báo chính):
    **A1 trượt** về cấu trúc (chỉ còn 2 buổi sáng), suy luận rất mong manh. 34/1.471 khách có lời hứa đón đầu tiên hiển thị
    vượt hạn đón.
  - Thế giới mô phỏng dùng một hệ số tắc đường chung toàn thành phố (đo từ dữ liệu thật), nên kết quả gần như tất định theo
    sai số dự báo: đây là **mức trần** cho dự báo theo giờ trong ngày, không phải hiệu quả ngoài đời.
  - Sổ chia trễ ba phần (do điều phối / đường lường trước được / không lường trước được): buổi sáng ngày thường 1,3% /
    86,1% / 12,6%; tuyết 15/11 chiều 0,2% / 7,3% / 92,5%. Tương ứng với cách 49 CFR 37.131(f)(3)(ii) và Phụ lục D tách hai loại.
  - Họ B (đưa dự báo vào bộ điều phối qua wrapper v1.3, 32 job sau khi cổng tương đương E1/E2 đạt): 3/32 lần chạy dừng
    `RBWP7_FLEETPY_PLAN_INFEASIBLE` (một ô chẩn đoán được: dự báo lạc quan hơn giờ đi thật ở bin có tỉ số < 1; ô còn lại chưa
    chẩn đoán); B-C1 chỉ đạt trên 14 ô C-30F chạy xong (−0,14 khách/ô), trượt khi tính ô dừng là 0 khách; dao động 17,9% so
    với 1,4% của lớp lời hứa; quy tắc triển khai chọn lớp lời hứa. Với `M⁺-30` (15 ô chạy xong), thất hứa của chính luật giảm
    69,4% (ARTEFACT: phần lớn do lời hứa ban đầu lạc quan), tỉ lệ bị ép giảm 24,1% (PERSISTS, sát ngưỡng 25%; khung 7h
    76,9% → 59,4%); `M⁺-30F` vẫn bị ép ở 47,6% quyết định trong khi `C-30F` 0 quyết định non-normal.
  - 17/19 dự đoán niêm phong đúng, 2 trong số đó có điều kiện (sai PB1 và PS1). Kiểm độc lập bằng mã riêng: Họ A 25/26 mục
    khớp (mục còn lại là phạm vi một phép đếm), Họ B và tuyết khớp toàn bộ; agent rà số liệu tìm 5 lỗi chặn trong báo cáo,
    đều đã sửa (phụ lục A2).

- 2026-10-02 (nhánh thăm dò `research/tier3-no-worse`; không đổi code trong kho): T3.8/T3.9 trên panel A, chỉ tắc đường
  thật. Kế hoạch niêm phong và push `323a8d9` trước mọi job; γ push `19517b0` trước pha kiểm tra; kết quả `24aa747`.
  - 49 episode (ngày 14–18/11 × 4 mẫu × khung 07/08/17, trừ 11 ô bộ chuẩn hóa không dựng được), chia theo nhóm (ngày,
    mẫu) thành 15 hiệu chỉnh và 34 kiểm tra; 15 + 170 job, 2 lần chạy lại vì lỗi hạ tầng (phụ lục A1, A3).
  - H3: γ = 222,259 s (β + γ = 252,3 s ≤ 300 s; mức tham khảo 120 s không đạt); 2/34 ô vượt, cả hai ở khung 7h (khung 7h
    chỉ phủ 7/9). Cận trên Clopper–Pearson 17,4%. Đạt theo quy tắc niêm phong.
  - H2: `C-30` = `M⁺-30` = 2.338 khách, `V-30` 2.336; chênh trung bình mỗi ô và khoảng 95% của nó nằm trong ±0,25 khách (ô
    lệch nhiều nhất 2 khách); 0 quyết định non-normal ở 49 job `C-30`; `C-30` kém `U` 96 khách (2,6% nhu cầu). Đạt theo quy
    tắc niêm phong. 7/8 dự đoán đúng. Kiểm độc lập 13/13 khớp.
  - Phân tích sau sự kiện (2026-10-05): mọi thước đo phía khách của `C-30`, `M⁺-30`, `V-30` như nhau (`C-30` và `M⁺-30` có
    2.332 khách chung); so với `U`, trên 34 ô mọi trần giảm khoảng một nửa số khách thấy giờ trả đổi > 1 phút (10,96% →
    5,78%), nhưng ở khung 7h chỉ giảm khoảng một phần mười. Nguồn: `tang3/t38/T38-REPORT.md`,
    `tang3/t38/rider-experience-2026-10-05-report.txt`.

- 2026-10-01 (nhánh thăm dò `research/tier3-no-worse`; không đổi code trong kho): w07x, mức hứa 90–240 s trên 8 ô khung
  7:00–9:00. Kế hoạch niêm phong và push `aa98fa5` trước mọi job; 104/104 pass. `C` và `M⁺` chỉ khác ở 90 s (1/8 ô) và
  120 s (2/8 ô), mỗi lần +1 khách; như nhau ở 150–240 s. Cơ chế: tắc đường 61–79 s cộng một lần dời 61–68 s do điều phối
  vượt hạn chót. 6/8 dự đoán đúng; kiểm độc lập 7/7. Nguồn: `tang3/w07x/W07X-REPORT.md`.

- 2026-09-30 (nhánh thăm dò `research/tier3-no-worse`; không đổi code trong kho): Tầng 3 việc B, khung 7:00–9:00 chỉ với tắc
  đường đo thật. Mọi kết quả mang nhãn thăm dò; không authorize `RB-WP14R-009..012`, WP15 hay H7.
  - 8 ô phát triển mới `d2018111{2,3}-s10-r{1..4}-w07` (grid `tier3-development-w07-v1`, nhãn chọn khách mới), sinh bằng
    `tools/RideBound.Wp6Normalize` từ bộ đệm (normalizer source `34517a83…4d90`). Fixture chép ra ngoài kho
    (`C:\RideBoundData\research\fixtures-w07-v1`); bản trong worktree này **chưa được theo dõi và không commit**. Kiểm rò rỉ
    `wp14_development_panel_audit.py`: 0 trùng với 40 ô đóng băng của `wp9-confirmatory` grid v2/v3. Mỗi ô trùng 7–15/108
    khách với ô w08 cùng (ngày, mẫu).
  - Wrapper thế giới v1.2 ngoài kho: mở mốc 07:00 và từ chối thế giới có ngày/khung khác ô; test 24 kiểm, đột biến 9/9; chạy
    thử đầu-cuối PASS (kiểm rò rỉ, đổi mốc đúng giờ, từ chối sai ngày).
  - 88 job (11 nhánh × 8 ô, luật phục hồi (b), Runner v2), kế hoạch và 7 dự đoán niêm phong trước khi chạy; 88/88 pass; 6/7
    dự đoán đúng (Q4 sai ở một ô, một khách).
  - Kết quả: `C-x` và `M⁺-x` phục vụ cùng số khách ở mọi mức và cùng từng quyết định ở 30, 60, 300 s; `V-30`, `M-30` cũng
    như `C-30`. Nhưng lời hứa của `M⁺` bị phá với 24,0% khách ở 30 s, 7,3% ở 60 s (cả 8/8 ô), 0,9% ở 120 s; ở khung
    8:00–10:00 cùng ngày là 2,2% và 0%. `C` giữ lời hứa của nó theo cấu tạo; chấm theo luật `M⁺` nó cho cùng tỉ lệ.
  - Nguồn: `tang3/w07/W07-PLAN.md`, `W07-AMENDMENTS.md`, `W07-REPORT.md`, `tang3/w07/analysis/`.

- 2026-09-29 (nhánh thăm dò `research/tier3-no-worse`; không đổi code trong kho): Tầng 3 việc A. Mọi kết quả mang nhãn
  thăm dò; không authorize `RB-WP14R-009..012`, WP15 hay H7.
  - 48 job `V-30`/`M-30` × luật phục hồi (a)/(b), kế hoạch và 8 dự đoán niêm phong trước khi chạy; 48/48 pass; 8/8 dự đoán
    đúng; kiểm độc lập 7/7.
  - Với (b), `V-30` phục vụ đúng như `C-30` ở cả 8 ô có tắc và ra cùng từng quyết định ở 11/12 ô. Tín hiệu H2 dạng "`C`
    phục vụ nhiều hơn `V`" của thí điểm vì vậy không còn khi xử lý kẹt công bằng. Kết quả âm này được ghi đúng như vậy.
    Khác biệt còn lại nằm ở lời hứa: `V-30(b)` phá lời hứa của chính nó với 7–12% khách trên ngày đo thật và ở chế độ phục
    hồi ở khoảng một nửa số quyết định.
  - Đường miễn trừ theo ngân sách của ADR-077 lần đầu chạy đầu-cuối (118 quyết định). Xem ADR-077 Evidence và docs/19 §31.
  - Sửa nhãn cũ "chưa commit" của ADR-074/075/076 thành commit id `65d867c`, `8d1ea4d`, `69512d3`.
  - Các quyết định của chủ nghiên cứu trước T3.8 (δ_b, δ_r, α, quy tắc H2/H3, nguồn episode…) vẫn chưa chốt.

- 2026-09-26 (nhánh thăm dò `research/tier3-no-worse`): ADR-077 **Proposed**.
  - Khóa WP4 `commitmentRecovery: "no-worse-than-reference-v1"`, mặc định tắt: xe bị ép vẫn được chọn một tuyến đã đổi
    nếu tuyến đó không làm tệ hơn bất kỳ lỗi cổng nào mà tuyến giữ đã mắc.
  - Thêm loại vi phạm `ForcedNoWorse`, cùng giai đoạn chứng nhận `forcedNoWorse`.
  - .NET 1025/1025. Review độc lập 2 vòng không có blocker; các mục nên sửa đã sửa hoặc ghi ở giới hạn.
  - Mục đích là công bằng cho luật hạn chót trong quét `C`/`M⁺`. Mọi kết quả mang nhãn thăm dò.
  - **Chạy đầu-cuối A1:** 28/28 job pass, và 6/6 đối chứng giống hệt Runner v1. `M⁺` có no-worse phục vụ 1874 khách,
    so với 1864 khi dùng `forced-reference-v1` và 1873 của `C`, trên 22 job điều trị. Chênh lệch số khách của quét
    (a) vì vậy đến từ luật phục hồi, còn khác biệt về giữ lời hứa thì vẫn nguyên. Chi tiết ở ADR-077 Evidence.
- 2026-09-25 (nhánh thăm dò `research/tier3-failure-aware`; không đổi code trong kho): Tầng 3, T3.5–T3.7. Mọi kết quả
  mang nhãn thăm dò. Không authorize `RB-WP14R-009..012`, WP15 hay H7.
  - **Công cụ ngoài kho** ở `E:\Code\Report_INT3508\ridebound-scratchpad\tang3\`:
    - thế giới v1 bọc preflight medium, dùng hook `NetworkBasic._set_edge_tt`;
    - audit v/e/p có dấu, dựng lại từ commitment ledger trong final checkpoint, nên không cần đổi protocol;
    - chỉ số lựa chọn.
  - **Runner:** publish `E:\RideBoundData\research\runner-tier3-v1` từ `69512d3`.
  - **Thí điểm:** 60 job = 5 nhánh (`U`, `C-30`, `V-30`, `M⁺-30`, `M-30`) × 2 ô phát triển
    `d20181112-s10-r1-w08/w17` × 6 thế giới.
    - 60/60 `status: pass`; audit đúng 60/60.
    - H1 đứng 12/12: `C-30` không có quyết định phục hồi nào, lệch do điều phối lớn nhất mỗi khách 19,1 s.
    - `C-30` phục vụ ≥ `V-30` ở 12/12 episode, chênh 0–10 khách trên 108.
  - **Cổng dừng của kế hoạch bị kích hoạt:** `V-30` và `M-30` phục hồi ở đa số quyết định trong 5/12 episode mỗi nhánh.
    Đây là xe "đóng băng" của ADR-075, tối đa 30% đội xe tính trung bình. Luật phục hồi phải được chủ nghiên cứu chốt
    trước phép so xác nhận.
  - **Phát hiện về thế giới:** khung, và cả hệ số trôi của Tầng 2, chỉ được áp tại trigger đầu tiên sau mốc.
    - Thí điểm: trễ tới 265 s; lệch giữa các nhánh tới 131 s.
    - Tầng 2: trễ tới 210 s; lệch giữa các họ tới 70 s.
    - Wrapper v1.1 chèn đầu khung vào đồng hồ sự kiện; smoke đầu-cuối PASS.
    - Riêng thời điểm đổi khung đã đổi khoảng cách `C-30 − V-30` ở một ô từ +4 thành +2 khách.
  - **Một ô Panel A đã bị nhìn thấy:** `d20181114-s10-r1`, trong smoke T3.5/T3.6. Phải khai báo khi chia tập ở T3.8.
  - **Kiểm độc lập:** 7/8 khẳng định khớp; một con số độ trễ đã sửa. 10/10 số của Tầng 2 khớp.
  - Báo cáo: `tang3/T3.7-BAO-CAO-THI-DIEM.md`. Truy vết ở docs/19 §30.
- 2026-09-25 (nhánh thăm dò `research/tier3-failure-aware`, commit `69512d3`): ADR-076 **Proposed** —
  Tầng 3, T3.4 "lên xe muộn là sự thật". Khóa WP4 `lateBoarding: record-v1`, mặc định tắt: khách lên
  sau giờ đón muộn nhất được nhận và ghi `ObservedLatePickup` riêng, cửa sổ gốc giữ nguyên, bản ghi
  round-trip qua checkpoint. Adapter FleetPy đọc cùng khóa và chỉ bỏ giờ đến muộn nhất neo vào cửa
  sổ gốc, nên bound của điểm trả thành giờ lên thật + thời gian đi tối đa. Review độc lập 2 vòng,
  không blocker; đã sửa. .NET 1005/1005; Python 437/443, với 6 test freeze fail y hệt trên bản sạch
  trước T3.4. Mọi kết quả mang nhãn thăm dò.
- 2026-09-24 (nhánh thăm dò `research/tier3-failure-aware`, commit `8d1ea4d`): ADR-075
  **Proposed** — Tầng 3, T3.3 "lõi nhận biết thất bại". Tùy chọn `commitmentRecovery:
  forced-reference-v1`, mặc định tắt: no-op chỉ bị cổng hạn chót/ngân sách loại thì được
  giữ làm phương án forced (level đầu `forced-reference-count`). Lời hứa theo tuyến giữ vẫn được
  công bố, ledger cộng như thường, breach `ForcedReference` được ghi vào sổ khi lời hứa đổi, và
  mọi quyết định có miễn trừ mang certificate non-normal. Sửa hai chỗ Runner đưa mã ngoài
  taxonomy vào `ErrorPayloadCodec.Encode`. Review độc lập 2 vòng, không blocker; đã sửa các mục
  should-fix. Debug 993/993. Mọi kết quả mang nhãn thăm dò.
- 2026-09-24 (nhánh thăm dò `research/deadline-gate`, commit `65d867c`): ADR-074 **Proposed** —
  luật hạn chót tính từ lời hứa đầu (một chiều/hai chiều, mặc định tắt) và chẩn đoán no-op
  bị loại chỉ ở đường solver-backed. Debug 963/963; review độc lập hai vòng, vòng 2 không
  P0/P1. Tầng 2 đã chạy xong (296 job + 5 chạy lại do máy ngủ; đối chứng trùng drift-v1 từng
  quyết định; dự đoán niêm phong đúng 98/102, 31/32, 40/40) — xem đoạn "Kết quả Tầng 2" của
  ADR-074. Mọi kết quả mang nhãn thăm dò.
  `§1` và `§5` của cây chính không đổi.
- 2026-09-02: `RB-WP14R-009` **MATRIX HALTED** và `010` descriptive slice. Freeze v5
  (receipt `ba4a7835…0c1463`) đóng được paired gate lần đầu trong lịch sử WP14/WP14R:
  B1 `755,0 s` và C1 `626,3 s`, cả hai `succeeded` và `independentVerificationStatus
  valid`, gate receipt `status pass` với `matrixAuthorized true`. Ba con số trước đây
  chỉ ước lượng nay đã đo: bundle C1 `116.083.763` B **dưới** điểm hoà
  `135.215.555` B; chiếu matrix `18.719.858.304` B = **87,17% trần**; wall **28,29 giờ**
  tuần tự. C1 **nhanh hơn và nhỏ hơn** B1, đúng như constant-level skip dự đoán
  (C1 97,40% level thừa so với B1 94,40%). Matrix chạy **40/160 job**, 4 cell trọn vẹn
  10 arm, 0 fail, 0 lần dùng attempt 2, rồi dừng ở job 41
  `w14-d20181112-s10-r1-w17-b1-ref-s7` với `RBWP7_FLEETPY_PLAN_INFEASIBLE`: Runner phát
  ra route đón `req-d11464…` ở giây `2314,244` trong khi hạn đón muộn nhất là `2122`
  (`pickupWindowMs 600.000`, `1522+600`), trễ `192,24` giây. Cả hai attempt chết giống
  hệt ⇒ tất định; xảy ra ở arm **B1** (không có ràng buộc commitment nào) và chỉ ở cửa
  sổ **`w17`** sau 40 job `w08` liên tiếp pass. Đây là **semantic divergence Layer-2**
  giữa validator vật lý của RideBound và của FleetPy, không phải sự cố host. **H6 không
  bị ảnh hưởng**: `_fleetpy_plan` được gọi ở đúng một chỗ cho mọi lần đổi route và raise
  khi bất khả thi, mà H6 hoàn tất 100/100 bundle ⇒ FleetPy chưa từng từ chối kế hoạch
  nào của H6, tức mọi kế hoạch H6 đã qua **hai** validator độc lập; fail-closed hoạt động
  đúng thiết kế. Adapter `ridebound_fleetpy/` **không bị chạm**; không receipt đã freeze
  nào ghim inventory hash nên `.gitignore` không phá kỳ vọng nào; cả 40 bundle cùng
  `repositoryInventorySha256 c8d4b108…de6c7` nên so sánh arm hợp lệ. `010` phân tích
  slice bằng `wp14r_slice_frontier.py`, **dùng lại nguyên `read_bundle` của analyzer đã
  freeze**, report SHA `02fd03a5…d6d588f`. Kết quả: dự đoán đăng ký trước outcome về
  **F2 vô hiệu CONFIRMED** (`c1-ratchet` ≡ `c1-h6ref` và `c1-freeze300ratchet` ≡
  `c1-freeze300` giống hệt trên mọi counter, mọi cell); **thứ tự budget CONFIRMED**
  (`389 ≤ 391 < 398 < 402 ≤ 415`, độ lớn 7,7% so với dự đoán 7,6%); và nhận định trước
  đây của agent *"cái giá là cấu trúc, không cấu hình nào cứu được"* bị **FALSIFIED** —
  `c1-nobudget` cho **88,83% lợi ích gánh nặng với chỉ −3,009 pp** so với `c1-h6ref`
  99,95% với −6,019 pp. Phát hiện mới ngoài dự đoán: **toàn bộ nhóm pickup-side vô hiệu
  kể cả bỏ hẳn lock** (`c1-nopickuplock` có 0 prune pickup nhưng vẫn completed 389 y hệt
  `c1-h6ref`), và hai ràng buộc **không cộng được** (bỏ lock gỡ 0/26, bỏ budget gỡ 13/26,
  bỏ cả hai gỡ 26/26). Ranh giới: 4/16 cell, ~1/5 trọng số H6, một ngày một cửa sổ sáng,
  descriptive không phải preregistered, **không** so trực tiếp `−6,019 pp` với
  `−7,1296 pp` của H6. Evidence:
  `benchmarking/wp14r-009-matrix-halt-plan-infeasible-2026-09-01.md` và
  `benchmarking/wp14r-010-descriptive-slice-frontier-2026-09-02.md`.

- 2026-08-29 (khuya): Freeze v3 sửa đúng defect thứ nhất nhưng lộ ra defect thứ hai;
  paired gate vẫn chưa đạt. Dưới v3, child verify được receipt và B1 **chạy trọn mô
  phỏng**: 754,4 s, bundle `125.237.277` byte, ledger `succeeded`, process tree thoát
  sạch — so với 32,5 s và 0 byte dưới v2. Nhưng `independent_verify` reject job với
  typed `JOURNAL_CHAIN`. Kiểm độc lập cả hai journal: hash chain, sequence và
  `monotonicElapsedMs` **sạch tuyệt đối**; cái fail là điều kiện UTC
  `observed < previous_observed` tại `attempt-01` record 496 (heartbeat), nơi đồng hồ
  tường lùi `1,846` giây do một bước chỉnh NTP của Windows Time. Đây là mâu thuẫn nội
  bộ của protocol: contract `RB-WP14R-003` ghi *"UTC chỉ provenance"* trong khi verifier
  cưỡng chế UTC monotonicity như bất biến cứng, và nó duyệt **mọi** attempt được giữ
  lại, nên một attempt đã fail làm hỏng verification của attempt sạch. Attempt-01 tồn
  tại **do lỗi của agent**: tài liệu trong repo bị sửa giữa lúc B1 chạy, khiến
  `actual_fleetpy_medium_preflight.py` báo `repository content inventory drifted`. Lỗ
  hổng đó đã được vá bằng `__pycache__/` và `*.pyc` trong `.gitignore` (freeze v1 và v3
  vẫn verify exact sau khi vá), nhưng attempt đã tiêu không lấy lại được. B1 hết 2/2
  attempt; paired gate v3 không thể đạt `2 valid / 0 failed`; C1 chưa từng được chạm.
  **Không sửa được như v2→v3**: `wp14r_independent_verify.py` nằm trong năm file bị
  `validate_mechanics_gate_provenance` khoá theo fixture gate `006`, nên đổi nó phá luôn
  bằng chứng mechanics `002..006`. Ba lựa chọn (dựng lại gate `006` + freeze v4; chấp
  nhận mất job; hoặc thu hẹp phạm vi verify — vẫn vướng provenance) đều cần ADR mới của
  chủ nghiên cứu. Không attempt nào bị hồi sinh, không receipt nào bị sửa, mọi artifact
  v2/v3 giữ nguyên. Evidence:
  `benchmarking/wp14r-008-paired-gate-fail-closed-2026-08-29.md` §8.

- 2026-08-29 (tối): `RB-WP14R-008` **FAIL CLOSED**. Sau khi giải phóng bộ nhớ, host
  preflight `observation-0004` **pass** (memory `8.917.082.112` B, CPU mean `4,065%`,
  AC online, Balanced khớp) và attempt 1 mở lần đầu trong lịch sử WP14R. Child chết sau
  vài giây; recovery mở đúng attempt 2; attempt 2 chết **giống hệt**; không có attempt 3.
  Tổng `32,5 s`, `exitClassification processExitFailure`, `retryDisposition
  attemptsExhausted`, `independentVerificationStatus valid`, mô phỏng **chưa chạy một
  epoch nào** và `retainedOutputBytes = 0`. Nguyên nhân gốc là **defect của freeze v2**,
  không phải host: child verify lại receipt trước khi làm gì, nhưng
  `hostPolicy.requiredHostFingerprintSha256` do `host_fingerprint()` tính có
  `platform.machine()`, mà trên Windows hàm này đọc `PROCESSOR_ARCHITECTURE` — biến
  **không** nằm trong `inheritedEnvironmentNames` (chỉ `PATH`,
  `PYTHONDONTWRITEBYTECODE`, `SystemRoot`, `TEMP`, `TMP`). Đo khép kín: receipt yêu cầu
  `efebacc0…c6f81`; parent env đầy đủ cho `efebacc0…c6f81`; child theo đúng allowlist
  cho `85e172e3…48f8`; child cộng thêm `PROCESSOR_ARCHITECTURE` cho lại
  `efebacc0…c6f81`. Defect **tất định và độc lập host**: mọi lần launch freeze v2 trên
  Windows đều hỏng ở job đầu tiên. Test không bắt được vì không test nào chạy
  `verify_receipt` dưới đúng allowlist của child. Hệ quả theo contract: job
  `w14-d20181112-s10-r1-w08-b1-ref-s7` `exhausted`, paired gate không thể đạt
  `2 valid / 0 failed`, `009..012` **không được authorize** dưới freeze v2, C1 chưa từng
  được chạm. Machinery WP14R hành xử đúng thiết kế: ba preflight fail không tiêu attempt,
  journal giữ đủ stderr thật để chỉ đúng một biến môi trường, recovery đúng một lần rồi
  exhausted, mọi artifact được giữ nguyên. Sửa **cần freeze v3 trước outcome** và là
  quyết định của chủ nghiên cứu, vì ba file liên quan đều bị freeze v2 bind; khuyến nghị
  thêm `PROCESSOR_ARCHITECTURE` vào allowlist kèm regression test chạy `verify_receipt`
  dưới đúng allowlist của child. Job đã exhausted không được hồi sinh dưới freeze v2.
  Evidence: `benchmarking/wp14r-008-paired-gate-fail-closed-2026-08-29.md`.
- 2026-08-29: Pre-launch analysis trong lúc `008` bị chặn; **không** ticket, không ADR,
  zero scientific job. Bốn kết quả. (1) Analyzer đã freeze chạy sạch trên bundle thật:
  gọi `wp14_frontier_analyze.read_bundle` trực tiếp trên bundle B1 `125.237.277` B cho
  `readBundleSucceeded`, 108 arrived/104 completed/824 decisions và `semanticHash
  d072b931…c8393d` khớp independent verifier. (2) Nhưng `pickupEtaImprovementCount`
  tính trên promise đã publish và **không** lọc `decisionDelta`, nên nó gộp exogenous
  drift và **không** trả lời được câu hỏi F2; analyzer nằm trong 46 file freeze-bound
  nên chỉ được bổ sung công cụ. (3) Dự đoán đăng ký trước outcome: **F2 vô hiệu**. Trên
  80 bundle E1 có 142.769 publication, 137.627 revision, **916 lần decision dịch chuyển
  ETA và 0 lần sớm hơn** (pickup 100/0, drop 816/0); dev-panel B1 cho 48/0. Cross-
  validation: analyzer báo `pickupEtaImprovementCount = 654` và công cụ mới cho thấy
  đúng 654 lần đó đều `exogenousOnlyEarlier`, 0 do decision. Report SHA
  `958843ef…3dad8869` và `b3d3dc10…45071a7`. Cơ chế: insertion-only cộng O-001 cấm
  reassignment nên quyết định chỉ thêm detour; stop-reordering về nguyên tắc có thể cải
  thiện nên không claim bất khả thi theo cấu tạo. Dự đoán **không** cho phép bỏ arm.
  (4) Rủi ro trần byte cao hơn rủi ro thời gian: chiếu ADR-070 đạt 93,31% của
  `maximumOutputBytes` `21.474.836.480` B, điểm hoà mỗi bundle C1 là `135.215.555` B
  trong khi B1 đã `125.237.277` B và cả hai arm bật `retained-portfolio-full-witness-v1`;
  vượt trần thì cần freeze v3 trước outcome. Hai công cụ bổ sung không nằm trong freeze
  nào: `wp14r_promise_direction.py` (7 test) và `wp14r_matrix_driver.py` (9 test, có
  guard chống vòng lặp khi ledger không tiến). Verify: pinned Python/FleetPy 353/353
  zero skip trong 67,172 s; required .NET 908/908; freeze v1 pass exact 160/46/
  `1ce26ff0…37a55` và freeze v2 valid exact 160/`6b340108…a31237` **sau khi** thêm hai
  file mới; C1 partial vẫn `83.364.599` B không bị chạm; format/diff/Markdown 290 file
  394 link/JSON 1.350 pass. Blocker của `008` đổi từ `POWER_SOURCE_NOT_AC` sang
  `MEMORY_BELOW_MINIMUM` (thiếu `56.111.104` B); sleep/hibernate trên AC đều Never.
  Evidence: `benchmarking/wp14r-prelaunch-analysis-2026-08-29.md`.
- 2026-08-28 (chiều): Checkpoint verification và WP15 refinement draft; **không**
  scientific execution. Host preflight thật cho `w14-d20181112-s10-r1-w08-b1-ref-s7`
  attempt 1 ghi `POWER_SOURCE_NOT_AC` **đã hết**: AC `online`, Balanced GUID khớp, CPU
  mean `3.618%`/max `9.561%`, free disk `144.954.011.648` B đều pass; chỉ còn
  `MEMORY_BELOW_MINIMUM` với available `8.533.823.488` B so với ngưỡng `8.589.934.592` B,
  thiếu `56.111.104` B. Receipt `preflight-attempt-01-observation-0001.json`; zero
  attempt tiêu thụ, zero job launched, `008` vẫn Ready. Verify lại hiện hành: WP14 freeze
  v1 pass exact 160 jobs/46 files/SHA `1ce26ff0…37a55`; WP14R freeze v2 valid exact
  160 jobs/SHA `6b340108…a31237`; retained WP14-v1 B1 bundle tái kiểm độc lập cho đúng
  behavioral hash `5f2af778…32c5c0` cùng 108 request/824 epoch/2.479 frame/3.883 event/
  3.174 publication; E1 source-divergence claim xác nhận bytes đã freeze
  `b550c14c…c06e54` vẫn khôi phục được từ commit `38d517d`. Baseline: `dotnet build`
  0 warning/0 error, pinned Python/FleetPy 337/337 zero skip trong 68,641 s, required
  `dotnet test RideBound.slnx` exit 0 zero fail/zero skip với Benchmarking.Tests 148/148
  trong 1 m 41 s sau `build-server shutdown`. Thêm corpus paper **mới tải từ web**
  (không dùng lại local): 4 arXiv PDF/165 trang, 165/165 trang extract text không rỗng,
  inventory SHA `70e437b2…6edfb70` tại
  `E:\RideBoundData\research\pdf-20260828-wp15-commitment-design`. Research note
  `research/wp15-commitment-design-new-paper-evidence-2026-08-28.md` và refinement draft
  `tasks/47-wp15-lifecycle-commitment-refinement.md` khoá trước ba trục ứng viên B/C/D
  cùng điều kiện phủ định. Laupichler et al. 2026 (Mt-KaRRi) xác nhận độc lập rằng
  ràng buộc neo vào lời hứa đã accepted là đúng object, và ghi lại đúng failure mode
  "gate làm rỗng tập khả thi" mà H6 đã đo. WP15 vẫn **UNAUTHORIZED**; không ADR mới,
  không đổi margin/panel/denominator/factor/failure treatment, không mở H7/WP16/WP17.
- 2026-08-28: Chấp nhận ADR-072 và hoàn thành `RB-WP14R-007`. Canonical protocol
  freeze v2 SHA `6b340108…a31237` tham chiếu exact 160-job freeze v1, bind 24 source/
  schema/test files, three `002..006` gate artifacts và 34 full-PDF pages. Source audit
  sửa recovery command/output contradiction bằng fixed wrapper và thêm exact AC/power-
  scheme/10×1 s CPU/memory/disk gate. New 31/31, targeted WP14R 95/95, pinned
  Python/FleetPy 337/337 zero skip và required .NET 908/908 pass; JSON 1.350/1.350,
  93 schemas/93, Markdown 287/385, format/diff/freeze-v1 pass. Actual no-outcome
  preflight CPU/memory/disk/scheme pass nhưng battery `offline`, giữ typed receipt SHA
  `642b23ef…cd622`, zero attempt/zero simulation. `008 Ready` nhưng host-blocked;
  `009..012` Unauthorized.
- 2026-08-28: Documentation-close revalidation giữ source/ceiling: pinned
  Python/FleetPy 306/306 zero skip trong 200.231 s. Full .NET lần đầu 907/908 và
  isolated repeat đều fail typed medium-drain CPU gate khi build servers chưa shutdown.
  Sau `dotnet build-server shutdown`, exact test 1/1 và exact required command
  `dotnet test RideBound.slnx` pass 908/908; Benchmarking.Tests 148/148 trong 1 m 55 s,
  ceiling vẫn 120 s. Host diagnostic là Windows Balanced/discharging; `007` phải bind
  power source/scheme và quiescence precondition nếu authorize freeze v2. Không scientific
  execution và không reclassify hai failure thành pass.
- 2026-08-27: Hoàn thành `RB-WP14R-006`; chỉ `007 Ready`. Verifier read-only độc lập
  tự dựng ledger/journal/recovery state và không đọc scientific outcome. Ba valid fixture
  pass; retained mutation v4 bắt đúng 15/15 unique class. Actual Windows junction pilot
  phát hiện Python 3.10 detector gap; source review sửa junction/reparse-point ancestry
  và hai TOCTOU journal/output-inventory windows. Authoritative fixture receipt SHA
  `1e4a6450…372104`, mutation report SHA `9d8aacf4…72e1e`. Targeted 64/64, pinned
  Python/FleetPy 306/306, .NET 908/908, format/freeze-v1 pass; scientific `008..012`
  vẫn Unauthorized.
- 2026-08-27: Hoàn thành `RB-WP14R-005`; chỉ `006 Ready`. Giữ v1 resource pilot
  (42 pass/6 typed large timeout), v2 before-cache và v3 after-cache (48/48 mỗi matrix)
  ở ba external roots bất biến. Fixed 8 MiB verifier peak `65,875,968` B pass envelope
  256 MiB. Review tìm repeated schema load/check/compile trên từng record; compiled
  validator cache + schema provenance giảm observed large launcher/verifier median
  `71.30/35.39 s` xuống `4.68/1.96 s`, descriptive only. Targeted 50/50, Python
  292/292, .NET 908/908, format/freeze pass; không scientific execution. `007` Not
  ready và `008..012` Unauthorized.
- 2026-08-26: Hoàn thành `RB-WP14R-004`; chỉ `005 Ready`. Thêm durable `launchIntent`,
  supervisor PID, recovery receipt và hard-kill harness. Actual Windows matrix đi qua
  11 supervisor boundaries cùng recovery receipt→terminal crash; Job Object kill cả
  child/grandchild. Review sửa uncertainty-retry bug, Popen-stage honesty, incomplete-
  start PID gap, receipt preseed/reseal gap, log/tree cross-binding và unsafe control
  root. Targeted WP14R 39/39, pinned Python 281/281, .NET 908/908, format/freeze-v1
  reverify pass; không chạy FleetPy/scientific workload. `008..012` vẫn unauthorized.
- 2026-08-26: Hoàn thành `RB-WP14R-003`; chỉ `004 Ready`. Tool mới bind executable/
  args/cwd/allowlisted environment nhưng log chỉ hash metadata, rồi `fsync` canonical
  hash-chain journal trước/trong/sau child. Raw non-UTF8 stdout/stderr được giữ bounded,
  heartbeat/timeout dùng monotonic clock, Windows Job Object/POSIX group quản process
  tree; exit 0 vẫn `awaitingBundleVerification`. Review sửa schema composition, 64-bit
  handle signatures, containment-failure honesty, pipe leaks và resealed semantic
  mutations. Targeted 15/15 + WP14R 28/28, Python 270/270, .NET 908/908 zero skip;
  freeze v1 reverify exact. Chưa chạy fault matrix/FleetPy/scientific outcome.
- 2026-08-26: Chấp nhận ADR-071 và hoàn thành `RB-WP14R-001/002`. Browser/PDF QA
  đọc/render 24/24 trang Kalibera–Jones/Mytkowicz, áp dụng variation-level/setup-bias
  lessons vào successor mechanics mà không dùng chúng để retry WP14-v1. Ledger mới
  có exact 2 attempts, canonical/exclusive start/terminal receipts, unchanged freeze/
  job/command binding, stable log/output inventory, mechanical-only recovery và strict
  tamper/state gates. Source review sửa năm lớp gap ngoài test-count. Targeted 13/13,
  pinned Python/FleetPy 255/255 và .NET 908/908 zero skip. Chỉ `RB-WP14R-003` Ready;
  scientific `008..012`, WP15 và H7 vẫn unauthorized.
- 2026-08-26: `RB-WP14-009` đóng **FAIL CLOSED** theo ADR-070. B1 dry-run pass
  independent verifier; C1 bị chấm dứt ngoài controlled timeout path và chỉ còn
  partial transcript thiếu manifest. Recovery không re-execute: B1
  `reusedVerified`, C1 rejected, summary schema-valid 1/1. Freeze v1 cấm retry/
  replacement, nên `010..014` không được authorize; không outcome nào được dùng
  để tune hoặc re-freeze.
- 2026-08-26: `RB-WP14-008 Done`, `RB-WP14-009 Ready`; ADR-069/receipt
  `freeze-receipt-v1.json` 101.719 byte SHA `1ce26ff0…37a55` khóa exact 16×10 = 160
  jobs, 1.728 arrivals/arm, 46 source files, adapter/fixture/Runner/.NET runtime tree,
  FleetPy capability, four forbidden H6/E1 roots và resource envelope. Review sửa
  bundle/job misbinding, p95 off-by-one, float evidence và duplicate overwrite trước
  freeze. Browser + PDF QA xác nhận full-text Alonso-Mora 6/6 trang. Required .NET
  908/908, pinned Python 242/242, targeted 17/17, zero skip; static gates pass.
- 2026-08-25: `RB-WP14-005 Done`; `006`/`007` **Deferred** theo quyết định của chủ
  nghiên cứu; `RB-WP14-008` là queue head. F1 dùng `freezeHorizonMs`/`freezeHorizonLocks`
  đã có sẵn nhưng chưa configuration nào từng dùng; F2 thêm `ratchetLocks` cho phép
  cải thiện ETA nhưng chặn làm muộn hơn, chỉ áp cho hai trường ETA có thứ tự. Chín
  factor level và development panel 16 cell đã sẵn sàng; freeze sinh đúng 160 job.
  Envelope ngoại suy từ E1 thật: ~15,6 GB và ~11 giờ ở parallelism 4. Handoff đầy đủ:
  `handoffs/wp14-continuation-2026-08-25.md`. Required .NET 908/908 và pinned Python
  225/225 trên máy rảnh; medium public drain là test mong manh sát ceiling 120 s và
  fail giả khi máy bận.
- 2026-08-24: `RB-WP14-003 Done`, `RB-WP14-004 Done`, `RB-WP14-005 Ready`. ADR-068
  tách profile `retained-portfolio-full-witness-v1` sau khi phát hiện bản đầu sẽ đổi
  evidence của hai config E1 đã freeze; bind historical verifier theo commit thay vì
  HEAD; thêm declaration `source-divergence-v1.json` cho frozen source file mà WP14
  sửa hợp lệ. Development panel 16 cell trên 2018-11-12/13 có 0 giao với H6 trên cả
  bảy trục. Required .NET 880/880, pinned Python 225/225, zero skip.
- 2026-08-24: `RB-WP14-002 Done`, `RB-WP14-003 Ready`. ADR-067 thêm phát hiện hằng
  số ở tầng Application và cho adapter bỏ pass CP-SAT vô ích; 94,40–97,78% level đo
  được là hằng số. Phát hiện `executionEvidence` nằm trong hash projection buộc đổi
  contract sang opt-in mặc định tắt, nên mọi hash đã publish không đổi. Differential
  64 seed chứng minh assignment bất biến; required .NET 873/873, pinned Python
  212/212, zero skip.
- 2026-08-24: `RB-WP14-001 Done`, `RB-WP14-002 Ready`. ADR-066 khoá factor matrix
  WP14 sau khi một read-only probe trên 80 bundle E1 tái lập đúng 10/10 con số H6.
  Mất mát dịch vụ của C1 quy về đúng hai nguồn: budget `drop_eta_total_ms` và
  `pickup_eta_ms` final-confirmation lock; 8/10 dimension không sinh witness nào và
  B1 có 0 commitment prune. Bốn factor bị loại bằng phép đo; sáu factor F1–F6 được
  nhận kèm điều kiện falsification. Ghi ba giới hạn claim mới: level
  `worst-hard-utilization-ppm` là hằng số, attribution `lock/ranking` thực chất là
  lock, và burden giảm 99,83% là attributed còn experienced là 88,85%. Targeted 7/7;
  H6 và WP13 outcomes không đổi.
- 2026-08-24: `RB-WP13-013 Done`; WP13 `001..013` Complete. ADR-065 đóng bảy exit
  gate, khóa contract cho ba P3 limitation (derived-output transaction, 5.516 GB E1
  retention/archive, versioned successor verifier) và ra verdict
  `openExploratoryAblationOnly`. Closure manifest 4.463 byte SHA `4e410e23…58a72c9c`;
  targeted 6/6, required .NET 860/860, pinned Python 205/205 và static gates pass.
  `RB-WP14-001` refinement là queue head duy nhất; H7, policy v2 và H6 backfill vẫn
  chưa được mở.
- 2026-08-24: `RB-WP13-012 Done`, `RB-WP13-013 In progress`. ADR-064 bind exact
  80-file inventory và deep-verifies 100 H6 bundle plus 80 E1 arm. Audit tìm và sửa
  một P2 verifier-composition gap bằng supplemental guard/regression mà không mutate
  frozen verifier; zero unresolved P0–P2, zero unsafe claim conclusion và zero DAG/
  architecture violation. Required .NET 860/860, pinned Python 199/199; ba P3
  limitation thuộc `013`. WP14/policy v2 vẫn chưa mở.
- 2026-08-24: `RB-WP13-011 Done`, `RB-WP13-012 In progress`. ADR-063 khóa exact
  semantic-signature aggregation sau full 80-bundle verification: generated sets
  equal 40/40, 390 signature/arm, zero collision/ID drift/absent; 41 B1 actionful
  links gồm 33 pruned, 7 eligible-not-selected, 1 selected. Objective profiles không
  so trực tiếp; association rows overlap/non-additive/noncausal. Required .NET
  860/860, pinned Python 191/191 và static gates pass; WP14 chưa mở.
- 2026-08-24: `RB-WP13-010 Done`, `RB-WP13-011 In progress`. ADR-062 khóa exact
  31-mutant typed falsification matrix sau byte-exact 80-bundle E1 rebuild; 31/31
  reject đúng layer/code, zero unexpected pass/failure. Read-only E1↔H6 comparison
  xác nhận 80/80 same-arm behavioral equality, zero mismatch; semantic hash 0/80
  equal là expected do v1.2 binding. Required .NET 860/860, full sequential pinned
  Python 187/187 và static gates pass; H6/result/claim boundary không đổi.
- 2026-08-24: `RB-WP13-009 Done`, `RB-WP13-010 Ready`. ADR-061 khóa E1 trước
  execution và đóng exact 40 pair/80 arm replay, zero failure; independent inventory
  verify 8.640 requests và 44.156/44.156 v1.2 solver decisions, tổng
  5.516.098.710 raw bytes ngoài H6. Kết quả chỉ execution/coverage inventory,
  không mechanism/causal/confirmatory conclusion; required .NET 860/860, pinned
  Python 181/181 và static gates pass.
- 2026-08-24: `RB-WP13-008 Done`, `RB-WP13-009 Ready`. ADR-060 khóa opt-in
  `retained-portfolio-v1`: default evidence v1.1 không đổi; v1.2 ghi generated/
  eligible/selected candidates, full route/schedule và exact solver-neutral objective
  inputs. Strict nested decoder/schema, defensive-copy và capture-on/off differential
  pass; required .NET 860/860, pinned Python 168/168, format/static gates sạch. Không
  simulator run hoặc H6 backfill; `009` phải freeze E1 trước execution.
- 2026-08-24: `RB-WP13-007 Done`, `RB-WP13-008 Ready`; O-009 closed. ADR-059 khóa
  80-arm-epoch count-only sufficiency report: generation exact-equal 40/40 pair,
  complete 80/80, zero cap/work/omission; retained identities/routes/objectives vẫn
  `notRecorded`. Canonical SHA `d71c669b…37258`; targeted 10/10, pinned Python
  164/164, independent raw verifier và required Debug 856/856 pass. Evidence vNext
  không authorize rerun hoặc H6 backfill.
- 2026-08-24: `RB-WP13-006 Done`, `RB-WP13-007 Ready`. ADR-058 khóa 40-record/
  41-link evidence classifier và pair-level multi-label cross-tab. C1-lower có 7
  budget + 1 lock occurrences; equal có 21 budget + 4 lock + 7 indeterminate, nên
  kết quả chỉ co-occurrence/noncausal. Canonical SHA `bcc6bed3…f9e9eb`; targeted 8/8,
  pinned Python 154/154, independent verifier và required Debug 856/856 pass.
- 2026-08-24: `RB-WP13-005 Done`, `RB-WP13-006 Ready`. ADR-057 khóa strict
  recorded-witness clearance trên 40 records/41 links: 28 budget, 5 lock, 7 absent
  `notRecorded`, 1 selected-by-C1. Canonical SHA `cdd9a28d…9e411`; targeted 11/11,
  pinned Python 146/146, independent verification và required Debug 856/856 pass.
  Validator fail-fast nên feasibility sau clearance vẫn `notEvaluated`.
- 2026-08-24: `RB-WP13-004 Done`, `RB-WP13-005 Ready`. ADR-056 khóa exact raw
  action-level comparator: 80/80 primary transcripts, 44.156 decisions và 40/40 target
  bindings verified; Panel A/B immediate C1-lower 3/5, equal 17/15, không C1-higher.
  Canonical report SHA `3717f093…4f7e3`; targeted 13/13, pinned Python 135/135,
  independent reconciliation và required Debug 856/856 pass. Kết quả chỉ
  descriptive/noncausal, không thay completed-service outcome H6.
- 2026-08-24: `RB-WP13-003 Done`, `RB-WP13-004 Ready`. ADR-055 khóa strict
  first-divergence record-set schema và canonical artifact 40/40 exact pair; mọi
  record bind report/schema/generator/panel hashes, không `null`, không same-state hay
  causal claim. Targeted 11/11, pinned Python 122/122, independent schema/invariant
  check và required Debug 856/856 pass.
- 2026-08-24: `RB-WP13-002 Done`, `RB-WP13-003 Ready`. CPU profile của exact
  medium public-drain tìm thấy canonical-number marker collection bị tạo lại trên hot
  path. ADR-054 thay bốn call site bằng allocation-free span overload, giữ nguyên
  number/error/canonical-byte semantics và ceiling 120 s. Uppercase-`E` boundary test,
  targeted drain 1/1 (1 phút 33 giây) và required Debug 856/856 đều pass; H6 receipts
  và negative result không đổi.
- 2026-08-24: `RB-WP13-002` vào In review. Analyzer bind exact Panel A/B shape,
  frozen inventory hashes, analyzer/verifier source hashes, LF/frame/file receipts và
  cấm output trong H6 roots. Full scan pass 100 bundle/57.806 decision; 40/40 primary
  pair có first operational divergence trên equal observed input; 30/40 có earlier
  wire-only publication reorder. Targeted 16/16 và pinned FleetPy 111/111 zero skip;
  Release build 0 warning/error, format/JSON/Markdown/NuGet/static sạch. Required
  Debug và additional Release solution đều 854/855: duy nhất medium public-drain fail
  `resource.cpu-time-exceeded`; diagnostic đo 120.062/120.000 ms và đã revert. Không
  nới ceiling, không mở `003` và không claim ticket Done.
- 2026-08-23: Chấp nhận ADR-053 và mở WP13 exploratory hậu H6. Đọc tuần tự 106/106
  trang từ ba full PDF, khóa state/action/trajectory boundary, không nhận paper default
  hoặc future-value claim từ slack. Thêm roadmap WP13–WP20 nhưng chỉ ticket hóa WP13;
  `RB-WP13-002` active để inventory raw H6 và định nghĩa equal-observed-input
  alignment, giữ H6/WP10 negative outcomes bất biến.
- 2026-08-23: Chấp nhận ADR-052 và hoàn tất final review WP1–WP10. Review toàn cây
  sửa WP10 analyzer để bind exact terminal inventory/freeze/full Runner/seed, sửa
  format drift, và giữ negative outcomes không đổi. Full-PDF audit chỉ cho phép exact
  cache reuse: key allocation giảm 30%, complete generator heap giảm 0,79–1,30%,
  semantic work counters exact; timing mixed nên không claim speed. Final gates:
  .NET 855/855, FleetPy 95/95, RidePy 23/23, Release/format/NuGet/static pass. Exact
  RidePy image archive load lại đúng image ID; future Dockerfile rebuild vẫn không
  được claim byte-reproducible. Báo cáo PDF 12 trang đã render/inspect, SHA-256
  `066168872d7ead11362b3f0f7b5832e8e1147bb655f281cc3ec08d939c29b20b`.
- 2026-08-23: Đóng WP10 `001..010` bằng ADR-051. Exact RidePy 2.10.1 source/Linux
  image và same-Runner canonical B1/C1 pass; mỗi arm 5/5 completed, đủ native pickup/
  drop, artifact tree bất biến và five-class mutation verifier pass. Frozen subset
  plan 24 arm jobs kết thúc 22 pass, một B1 fail closed, paired C1 không chạy. Mười
  một valid pairs cho B1 `54/62`, C1 `49/62` (`−8.06 pp`) nhưng chỉ descriptive.
  Failure `RBWP10_NODEONLY_CONCURRENT_MIDEDGE_UNSUPPORTED` chứng minh `nodeOnly`
  không đủ khi các xe đồng thời giữa cạnh; không nội suy state, không đổi manifest,
  không fallback hậu-outcome. Layer 3 claim chưa được thiết lập.
- 2026-08-23: Đóng WP9 `001..009` với kết quả âm dưới H6. Panel A (8 xe)
  `1735 → 1581`, `−7.1296 pp`; Panel B (4 xe) `966 → 860`, `−4.9074 pp`; cả hai
  service gate FAIL so với `−1 pp`, burden gate PASS. Independent verifier đọc
  100/100 raw bundle, bốn falsification condition và repeat deterministic PASS;
  seed19 tăng N bằng 0. Robustness cho thấy lock/ranking và 30-second budget mỗi
  phần `−3.7037 pp`; C2 không rescue. Sau khi matrices đóng mới cài evidence v1.1
  và exogenous ledger bridge; real FleetPy probe ghi 43/43 evidence/ledger breach,
  không charge decision budget. Đây chỉ là mechanism evidence hậu-kết-quả. Chấp
  nhận ADR-048/049; WP10 trở thành next work package.
- 2026-08-22 (tối): Chấp nhận ADR-047 và **vô hiệu hoá toàn bộ dữ liệu confirmatory
  dưới `H5`**. Panel A đã chạy 60/60 sạch và analyzer cho `Δ_service_panel =
  −7,08 pp` (20/20 cell âm, service gate trượt, burden gate đạt) — nhưng kết quả
  đó không còn là outcome confirmatory vì semantics đã đổi sau đó. Panel B (`veh4`)
  phát hiện hai defect mà Panel A không thể lộ: (1) relief của ADR-045 áp cho cả
  candidate đã đổi, khiến RideBound đề xuất kế hoạch mà `VehiclePlan.is_feasible()`
  của FleetPy từ chối; (2) `mapping.py` lượng tử hoá tiến độ cạnh bằng
  `ROUND_HALF_EVEN`, đặt xe trước vị trí thật và làm ETA lạc quan — một job vượt
  cửa sổ đón đúng 13 ms. Sửa: relief chỉ cho no-op; permille dùng `ROUND_FLOOR`.
  3/3 job từng hỏng nay `completedVerified`. Freeze lên schema 5.0.0, bind thêm
  `adapterPackageTreeSealSha256` vì toàn bộ package adapter là outcome-bearing mà
  trước đó không được pin. `.NET 852/852`, Python 86/86.
- 2026-08-22 (chiều): Chấp nhận ADR-046 và đóng `RB-WP9-002a`. Review WP8 tìm ba
  defect trong đường phân tích confirmatory: analyzer chính **không chạy được**
  trên manifest đã đóng băng (arm khai bằng danh tính preregistered nhưng bị nối
  thẳng vào jobId), và cả hai analyzer chấp nhận manifest thiếu cell hoặc đảo
  orientation. Đã sửa cả ba, regression giờ bind vào artifact thật; Python 77 → 86
  pass, 0 skip. Materialize Panel B (`grid-v3-veh4`, 20/20 cell, 108 request/cell)
  và **đo được** điều kiện tiên quyết: 20/20 cell chọn đúng cùng tập request và
  cùng travel realization như Panel A; chỉ vị trí đội xe khác, đã ghi thành giới
  hạn. Freeze `H5=6720acca…25da6` (schema 4.0.0) bind 30 file hash + 4 tree seal,
  verifier PASS, 4/4 mutation bị từ chối. Xác nhận ADR-045 trên dữ liệu thật:
  cặp diagnostic `d20181114-s10-r1` chạy hết ở **cả hai arm** (B1 738.769 ms,
  C1 650.215 ms, `status: pass`, C1 `completedVerified`), trong khi cùng cặp đó
  trước ADR-045 chết giữa chừng ở transcript 14,7 MB. Một lần chạy hỏng vì tài liệu
  bị sửa giữa ma trận — frozen-source guard bắt đúng, ghi lại làm quy tắc vận hành.
- 2026-08-22: Chấp nhận ADR-045 trước outcome confirmatory và mở khóa WP9. Smoke
  chết ở cả hai arm vì `MAX_RIDE_TIME` bị enforce như bất biến liên tục, prune
  chính safety no-op. Tách physical constraint thành structural (strict) và
  service-quality (plan-time); no-op luôn được giữ; vi phạm ngoại sinh được ghi
  làm `ExogenousServiceQualityBreach` với bound chống rửa `max(contractual,
  exogenous)`; `PICKUP_WINDOW` cùng lớp nên xử lý cùng cách. 851/851 Debug và
  Release, Release `-warnaserror` 0 warning. Cùng đợt: amendment `wp8-011d` thêm
  capacity stratum `veh4` bên cạnh `veh8` đã prereg (80 primary job, N không tăng,
  kết luận thành có điều kiện theo năng lực); `wp8-010` bỏ exogenous burden làm
  negative control — sai vì exogenous baseline là hàm của quyết định trước đó —
  và thay bằng identity chuỗi demand+travel bốn điều kiện; `wp8-008` ghi ba điều
  bất lợi (service không đơn điệu, trượt biên −4,69 pp ở 8 xe và toàn bộ về phía
  budget, 18% mức giảm là definitional theo lock). Đo lại tính độc lập của panel:
  bốn file `sample_10_*` cùng ngày chồng 8,3–10,7% request id ở tầng nguồn nhưng
  **không** lan tới panel — 2.160 slot chứa 2.157 request phân biệt, chỉ 3 lần
  dùng lại; ràng buộc thật là travel factor hằng số theo ngày, tức 5 realization
  chứ không phải 20. `H4` hết hiệu lực vận hành; `RB-WP9-002a` phải repin `H5`.
- 2026-08-21: Chấp nhận ADR-044 trước outcome confirmatory. Kiểm Layer-1 receipt
  phát hiện stale Runner pin trong H2/H3; publish sạch xác nhận DLL hiện hành
  `4c297a2c…bd2a8` và 19/19 file khớp Runner tree Layer-1. Amendment `WP8-011c` và
  current freeze `H4=2f7e6bf3…a32dd` bind thêm Runner-tree seal; verifier 25 hashes
  + 3 tree seals PASS. Layer-1 mechanical 8/8 đóng `RB-WP9-003`; `RB-WP9-002` Ready.
- 2026-08-21: Supersede ADR-042 bằng ADR-043; đóng WP8 `001..014`. Frontier
  25/25, treatment-only fairness, BCL-only burden oracle, oriented pairing,
  contended verifier, audited solver evidence và exact hot-path reuse đã qua gate.
  Bác `N=62`: fixed panel có 20 unit/5 ngày và denominator 2160/arm. Hai amendment
  pre-outcome materialize đồng nhất 108 requests và khóa analysis/source binding.
  Freeze tại thời điểm ADR-043 là `H3=d028eae4…dd14e`; chưa có outcome WP9. Review WP1–WP8 ghi
  đầy đủ defect đã sửa và giới hạn claim; `RB-WP9-002` là việc tiếp theo.
- 2026-08-19: Hoàn thành `RB-WP8-002`/`003` và chấp nhận ADR-041. Normalizer chạy theo
  grid manifest, harness nhận cardinality từ driver, và pilot chạy thật trên dữ liệu
  Manhattan công khai. Pilot bác bỏ điểm vận hành cũ (decision delta bằng 0 ở cả hai arm
  nên endpoint bằng 0 đồng nhất), loại primary endpoint pickup-ETA vì zero-inflated tới
  mức `p50 = p90 = 0`, và nâng service rate thành cổng đồng thời đo ở mốc hoàn thành
  chuyến. Bốn đơn vị paired ở điểm cao điểm thật cho Δ burden âm ở cả bốn (giảm 75–93%,
  median `−3.130.086 ms`) nhưng Δ tỷ lệ hoàn thành `−6,25/−5,47/−0,78/0,00` điểm phần
  trăm. Theo tiêu chí `docs/11` §5/§14 tự đặt trước, C1 ở cấu hình hiện tại **không vượt
  cổng service** ở điểm này. Đây là pilot n = 4, không CI, không effectiveness claim;
  confirmatory holdout chưa sinh.
- 2026-08-18: Hoàn thành `RB-WP8-001` và chấp nhận ADR-040, mở WP8. Kiểm kê nguồn xác
  nhận grid thí nghiệm dựng được **hoàn toàn từ dữ liệu công khai thật**: 289 file
  demand trên 8 ngày `2018-11-11` → `2018-11-18`, kèm sample fraction, tỷ lệ đặt trước
  và `tt_factors` theo ngày/giờ; normalizer đã tham số hoá đủ nên chỉ cần grid manifest
  thay cho hai profile hard-code. Pilot khoá ở `11-11`/`11-12` — bao trọn phần dữ liệu
  WP6/WP7 đã nhìn — và sáu ngày còn lại niêm phong làm confirmatory holdout. Commitment
  budget sẽ suy từ chỉ dữ liệu pilot rồi phân tầng ba mức, vì cấu hình hiện tại chỉ có
  3/10 chiều `hardLimit` và cả ba luôn thoả sẵn do O-001, khiến hard vector của C1 thực
  tế chưa bị ràng buộc. Margin non-inferiority phải đặt trước và neo vào chính sách suy
  biến từ chối hết. Tạo `tasks/37`/`tasks/38`; chỉ `RB-WP8-002` Ready; chưa có production
  code, chưa sinh scenario confirmatory, không claim nào được nâng.
- 2026-08-17: Chấp nhận ADR-039. Audit source khóa bằng ADR các thay đổi ngữ nghĩa
  từng vào source mà chưa có quyết định: `initialPromiseTrigger` hai giá trị, lock
  evaluator dùng baseline exogenous, `OfferDeclined` sau accept là cancel-after-
  acceptance, fail-closed `C1_VEHICLE_HAS_NO_FEASIBLE_CANDIDATE` với witness typed,
  hai CLI flag Runner và event-induced plan update phải opt-in. Hot path được **đo**
  chứ không suy đoán: chi phí nằm ở khóa memo slack (`19,63 → 0,64 µs`), không ở
  validator; wall time mỗi `Generate` giảm `23–39%` với toàn bộ counter bất biến, và
  benchmark medium về `1 m 50 s` dưới trần CPU. Đề xuất lazy priority bị bác bỏ bằng
  phân tích và được giữ làm kết quả âm. Required suite `798/798`, Python `50/50`,
  format/Release sạch; Runner v8 `13bf5d9b…c179e` chạy lại toàn bộ actual FleetPy gate.
  Vẫn không có effectiveness/SLA/novelty claim.
- 2026-08-16: Chấp nhận ADR-038 và đóng `RB-WP7-004..014`. Audit Candidate sửa B4
  repair-root priority để bounded best-first chấm đúng repaired suffix, đồng thời
  portfolio opt-in cấm hidden introduced request. FleetPy 1.0.2 actual B1/C1 dùng cùng
  Runner v6 qua preflight, lifecycle, tiny và public-medium three-repeat verifier;
  .NET 790/790 và Python 49/49 pass, WAC không tái hiện. Đây chỉ là mechanical Layer-2
  closure; raw timing/publication khác nhau không được suy thành effectiveness hay SLA.
- 2026-08-13: Hoàn thành `RB-WP7-001..003` và chấp nhận ADR-037. Browser/source
  research loại paper-only/random pruning; Candidate portfolio opt-in giữ cost/stability
  anchor theo service set, exact loss accounting và old-config legacy semantics. Bounded
  adversarial tăng accepted `2 → 4`; 32-seed real C1 gate không substantive regression
  và có strict positive. FleetPy exact annotated tag/commit/source/env probe pass,
  actual directed-edge `_move` và non-forced lock path được thực thi; bốn drift mutation
  fail closed. Full solution 776/776; chuyển duy nhất `RB-WP7-004` sang Ready.
- 2026-08-13: Hoàn thành `RB-WP6-014`, chấp nhận ADR-036 và đóng WP6. Source-level
  audit WP1–WP6 không phát hiện unresolved correctness/contract defect; WP3 full
  publication gate, WP4 fairness/fallback, WP5 pinned Runner/durable recovery và WP6
  raw/oracle/bundle authority giữ nguyên. Fresh tiny A 8/8; medium H/I trên exact
  source cuối mỗi process 8/8, 16/16 top-level + 72/72 per-run semantic exact,
  8/8 resource rows khác hợp lệ;
  external verifier valid. Required full solution 770/770, WAC không tái hiện. Tạo
  final Vietnamese review và closure evidence; WP7 giữ Not Started, không active ticket.
- 2026-08-12: Hoàn thành `RB-WP6-013` và chấp nhận ADR-035/contract `1.0.6`.
  Audit sửa executable warm-up, complete provenance/policy preflight, plan-derived
  conservation và canonical failure taxonomy. Declared permutation/parallel/failure/
  exclusion/process/store/metric/bundle/claim/source matrix pass. Medium D/E đều 8/8,
  semantic exact và strict verify valid; mọi sampled-resource row khác được giữ, gồm
  C1 chậm hơn B1 ở 6/6 local measured pair nhưng không có effectiveness/SLA claim.
  Release/format/dependency/schema/link sạch, exact full solution cuối 770/770; một
  preceding CPU-control failure không bị báo sai thành WAC. Chuyển `RB-WP6-014` In progress.
- 2026-08-12: Hoàn thành `RB-WP6-012` và chấp nhận ADR-034/contract `1.0.5`.
  Verified FleetPy source và two-clean-root normalization khớp exact medium derivative;
  dedicated synthetic-policy binding cùng exact Runner instant-drain lifecycle đóng lỗi
  config identity mà không nới validator/data. Hai fresh Release harness B/C đều 6/6,
  semantic/per-run hashes exact và external bundle verify valid; resource/full/bundle
  hashes khác đúng phạm vi. Attempt A unsorted provenance fail-closed được giữ lại.
  Format sạch, full solution cuối 710/710; một run trước fail wall-time contention được
  ghi đúng là timeout test, không phải WAC. Chuyển `RB-WP6-013` In progress.
- 2026-08-12: Hoàn thành `RB-WP6-011` và chấp nhận ADR-033/contract `1.0.4`.
  Sáu-epoch source fixture tạo accept/reject/revision/completion và exact non-zero
  decision-induced insertion witness. Composite WP3+WP4 policy binding, manifest
  solver seed opt-in, canonical failure-stage mapping và independently verified
  per-run oracle summaries đóng các lỗ provenance/measurement. Hai clean Release
  harness process khớp toàn bộ semantic identity; verified mechanical bundle
  `0936f8c...a1671` được publish dưới ignored `artifacts/`. Benchmarking 104/104,
  Algorithms 136/136, format/Release sạch, full solution 705/705; WAC không tái hiện.
  Chuyển đúng `RB-WP6-012` sang In progress.
- 2026-08-11: Hoàn thành `RB-WP6-010` và chấp nhận ADR-032. Builder reserve/tự sinh
  canonical claim profile/report, profile SHA được bind trong reproducibility evidence
  `1.0.1`; stage 10 chỉ scan selected claim surfaces rồi recompute report byte-exact.
  Exact caveat masking cùng Unicode/punctuation/confusable/synonym matcher trả typed
  witness và chặn resealed README/report/provenance/profile/forged-check mutations,
  không đọc raw trip/transcript/source prose. Browser đối chiếu ACM, NASEM, Unicode,
  Peng và Munafò; không nâng same-team repeatability thành validity/effectiveness/
  independent reproduction. Benchmarking 95/95, format/Release sạch, required full
  solution 691/691; WAC không tái hiện. Chuyển đúng `RB-WP6-011` In progress.
- 2026-08-11: Hoàn thành `RB-WP6-009` và chấp nhận ADR-031. Strict BagIt builder
  phát deterministic no-extra payload/tag/oxum/logical manifests qua private staging
  và atomic publication. Source capture bind exact dirty working tree; provenance
  rederive source/runtime/Runner/oracle/verifier/grid identities. Ordered verifier tái
  dùng WP6-007 transcript gate, bảo toàn mixed terminal logs, so oracle và recompute
  raw production metrics; external process chỉ emit immutable sidecar. Sáu bundle/
  source tests cùng full mutation matrix pass; Benchmarking 92/92, format và Release
  `-warnaserror` sạch, required full solution 688/688, WAC không tái hiện. Chuyển đúng
  `RB-WP6-010` In progress.
- 2026-08-11: Hoàn thành `RB-WP6-008` và chấp nhận ADR-030. Metric registry exact
  36 definition; production calculator sinh 132 canonical integer rows với explicit
  cohort/window/lifecycle/denominator/missing/resource rules. External BCL-only oracle
  không ProjectReference tự tái dựng và khớp byte-exact mọi row/evidence/metric-set;
  request/action/promise/vector/window/order/resource/zero-denominator/overflow
  mutations bị phát hiện hoặc từ chối typed. Browser evidence bổ sung McKeeman
  differential testing và Dolan–Moré performance-profile boundary; không mở aggregate/
  effectiveness claim. Benchmarking 86/86, format và Release `-warnaserror` sạch,
  required full solution 682/682; WAC không tái hiện. Chuyển đúng `RB-WP6-009`
  In progress.
- 2026-08-11: Hoàn thành `RB-WP6-007`. Append-only store publish plan/run bằng atomic
  rename, exact raw/index/detail/log identities, gapless failure/exclusion hash chain,
  terminal conservation, typed persistence seal, concurrent per-run locking và
  authorized full-grid supersession. Source audit bổ sung streaming hash, exact path/
  reparse guards, canonical resource samples, plan-bound manifest/checkpoint checks,
  independent artifact-inventory/launch verification và supervisor-store mapper.
  23 store + 2 mapper cases; Benchmarking 77/77, Contracts 38/38, format sạch và
  required full solution 673/673, không failed/skipped; WAC `0x800711C7` không tái
  hiện. Chuyển đúng một ticket `RB-WP6-008` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-006`. Source implementation phát hiện failure
  taxonomy thiếu cancellation/process-count/stream-byte branch; ADR-028 + contract
  1.0.2/failure 1.0.1 republish plan/run/metric identity vectors. Supervisor chỉ gọi
  external pinned process, hash pre/post 189 .NET runtime files + 12 Runner deploy
  files/config/source trong actual fixture gate, kiểm độc lập capability/manifest/
  decision/ACK/checkpoint chain và giữ bounded partial evidence. 15/15 supervisor,
  contract 37/37, Benchmarking 52/52, format sạch, full 647/647; WAC 0x800711C7
  không tái hiện. Chuyển `RB-WP6-007` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-005`. Framed HMAC seed vectors tái tạo qua hai clean
  processes; plan compiler bind exact config/protocol/work/capability/pairing, reject
  asymmetric/B5/noncanonical/oversized grids và materialize collision-free full run
  grid với HMAC arm order trước outcome. Permutation/32-way parallel exact; targeted
  32+37, format sạch, full 627/627. Chuyển `RB-WP6-006` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-004`. Source-level review phát hiện cyclic scenario/
  report identities; ADR-027/schema 1.0.1 chuyển thành acyclic hash DAG. Normalizer
  kiểm exact member bytes, directed SCC/Dijkstra, dense coverage node pool + HMAC row
  ranking, ties-to-even/pseudonym/conservation và typed failures. Two clean processes
  byte-exact cho tiny 8/2/16/240 và medium 128/32/96/9120; 21400 input rows conserved,
  targeted 29+32, format sạch, full 619/619. Chuyển `RB-WP6-005` In progress.
- 2026-08-09: Hoàn thành `RB-WP6-003`. Public FleetPy Manhattan ZIP được opt-in
  download/resume và khóa exact length/MD5/SHA-256; safe preflight/extract tạo receipt
  335 members, `1022750557` uncompressed bytes và stable inventory hash. Rerun xác
  minh cache/extraction idempotent không chạm mạng; targeted 26/26, format sạch và
  full solution 612/612. Chuyển `RB-WP6-004` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP6-002`. Thêm project contract thuần, 10 strict
  document codecs/models, semantic/path/pairing/topology/terminal validators, Draft
  2020-12 schemas/inventory, positive/negative fixtures và framed identity helpers.
  Hai clean process khớp published vector sáu hash; targeted 28/28, format sạch,
  required full solution 586/586. Chuyển duy nhất `RB-WP6-003` sang Ready.
- 2026-08-09: Hoàn thành `RB-WP6-001` refinement. Đọc đủ 82 Markdown và dùng
  in-app Browser kiểm FleetPy/Zenodo, RFC 8785, Random123, RFC 8493 BagIt, W3C
  PROV, FAIR, Datasheets, Sandve và ACM artifact terminology. ADR-026/contract v1
  khóa scenario/data/pairing/seed/Runner/failure/exclusion/metric/bundle/resource/
  claim semantics. Tạo queue `RB-WP6-002..014`, chỉ `002` Ready; chưa có harness,
  public download hoặc effectiveness result.
- 2026-08-09: Hoàn thành `RB-WP5-014` và đóng WP5/Q3 mechanical gate. Source audit
  sửa subject-link immutability, pre-T3 outbox publication và cross-run relay scope;
  BeGo Debug/Release 154/154, frontend/format/vulnerability gates sạch. Tạo detailed
  WP1–WP5 review với GO chỉ cho refinement và NO-GO cho experiment/SLA/effectiveness.
  Mở đúng một ticket `RB-WP6-001 READY`, chưa có WP6 implementation.
- 2026-08-09: Hoàn thành `RB-WP5-013`. Thêm executable hard-crash riêng tại đủ
  8 decision + 4 outbox boundary, fresh-Runner exact recovery, test-owned 16.384-step
  transition oracle, exact-set 2/3/4-worker PostgreSQL contention, `5/5` required
  mutants và raw randomized warm-up/repetition local curves. Artifact manifest
  `e21fb08...` rehash đủ 18 file; BeGo Debug/Release 153/153, RideBound 557/557,
  vulnerability audit sạch. Browser mapping ghi rõ LDFI/Elle/QuickCheck/mutation/
  performance mechanism và giới hạn claim. Chuyển duy nhất `RB-WP5-014` sang
  In progress; chưa đóng Q3 hoặc mở WP6 trước source-level closure audit.
- 2026-08-09: Hoàn thành `RB-WP5-012`. Thêm strict paired replay preflight,
  staged exact configs, B1/C1 × two clean Runner processes, exact materializer/
  checkpoint validation, repeat/common-input proof và self-verifying bundle bind
  source + assemblies. Final manifest `b843bd20...`; BeGo Debug/Release 152/152,
  RideBound 557/557, vulnerability audit sạch. Chuyển duy nhất `RB-WP5-013`
  sang In progress; không diễn giải bundle thành effectiveness hoặc SLA.
- 2026-08-09: Hoàn thành `RB-WP5-011`. Thêm default-off conditional hosted
  registration, exact artifact preflight health/gate và durable immutable Shadow/
  Live namespace. Decision claim lọc namespace; outbox hard-code Live-only nên
  shadow backlog không publish sau mode switch. PostgreSQL kiểm same-Session dual
  namespace, old Session snapshot unchanged, expired lease reclaim và guarded
  rollback; logic audit sửa unique-constraint typed mapping. Debug/Release 147/147,
  frontend 9/9, RideBound 557/557, dependency audits sạch. Chuyển duy nhất
  `RB-WP5-012` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP5-010`. Thêm exact canonical audit cursor,
  server-owned member ownership, operator-only raw evidence, repeatable-read
  append-log rebuild/live hash và fail-closed pseudonymous export. Logic audit sửa
  cross-member access, JSONB canonical/hash mismatch, prefix cursor plan, partial
  migration downgrade, eager HMAC authorization resolution và exception
  message-controlled classification. PostgreSQL concurrent append/drift/migration/
  12.000-row indexed-plan gates pass; BeGo Debug/Release 138/138, frontend 9/9,
  RideBound 557/557, audits sạch. Chuyển duy nhất `RB-WP5-011` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP5-009`. Thêm bounded outbox relay contract,
  PostgreSQL exact-per-run-head claim/attempt fence/backoff, canonical authorized
  SignalR publisher và strict user-safe payload gate. Crash-after-send phát cùng
  stable ID/payload/hash; stale owner không mark được và slow run không chặn run
  khác. Logic audit phát hiện external send có thể hoàn tất sau lease takeover;
  bổ sung stable aggregate sequence/hash wire envelope và frontend monotonic
  duplicate/stale gate, đồng thời ghi rõ SignalR enqueue không phải durable client
  ACK. Hai fresh PostgreSQL + published Runner gates đều 131/131 ở Debug/Release,
  frontend 9/9 + lint/tsc/build. Audit dependency phát hiện và vá Auth.js/Next/
  transitive advisories; NuGet/npm về 0 vulnerability. Chuyển duy nhất
  `RB-WP5-010` sang In progress.
- 2026-08-09: Hoàn thành `RB-WP5-008`. Thêm exact decision/certificate
  materializer, atomic T2 projection/timeline/outbox, fenced ACK/T3 checkpoint và
  fresh-process reconstruction. Source audit phát hiện/sửa EF identity-map stale
  revision sau raw SQL claim và thiếu semantic cross-binding trong promise
  service order. Migration recovery frames/FK/immutability/guarded Down được kiểm
  trên PostgreSQL 17. Tám failpoint khớp clean published-Runner oracle; BeGo Debug
  và Release `/warnaserror` 125/125 không skip, frontend 7/7, RideBound required
  command 557/557. Bổ sung paper mapping RIFL/leases, giữ claim at-least-once.
  Chuyển duy nhất `RB-WP5-009` sang In progress.
- 2026-08-05: Hoàn thành refinement `RB-WP5-001` bằng ADR-025. Audit exact
  RideBound/BeGo checkouts và re-run ba baseline độc lập 557/557, 25/25, 7/7.
  In-app Browser đọc primary systems evidence về end-to-end ACK/dedup, local
  transaction activity, outbox, EF concurrency/transaction, PostgreSQL worker
  locking, hosted service và Idempotency-Key draft (ghi rõ expired). Khóa adapter
  trong BeGo gọi exact hashed NDJSON Runner, T1/T2/T3 short transactions,
  per-run lease, checkpoint/replay/hash recovery, explicit bootstrap provenance,
  default-off rollback và same-input paired B1/C1. Tạo queue `RB-WP5-002..014`;
  chỉ `002` Ready và chưa claim WP5 production implementation.
- 2026-08-05: Hoàn thành `RB-WP5-002`. Thêm BeGo Application pure contracts/
  ports và exhaustive state/idempotency/hash/protocol guards cùng 32 targeted
  tests, gồm 3 architecture boundary cases. BeGo full 57/57 và RideBound 557/557 pass; targeted
  Release/format sạch. Full BeGo Release `/warnaserror` phát hiện dependency nền
  `Microsoft.OpenApi 2.0.0` có advisory High qua ASP.NET OpenAPI 10.0.1; ghi rõ
  chưa pass thay vì tắt warning. Chuyển duy nhất `RB-WP5-003` sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-003`. Thêm 11-table EF/PostgreSQL model và
  migration guarded bằng same-run composite FK, partial unique indexes, năm
  append-only triggers và explicit empty-only Down. PostgreSQL 17 thật pass
  migration/constraint/concurrency/evidence cases; BeGo 62 pass + 1 opt-in skip,
  targeted Release 38/38 và RideBound 557/557. Chuyển duy nhất `RB-WP5-004`
  sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-004`. T1 store khóa row run và bind exact
  fingerprint/epoch/time/range, atomic event/op/run, bounded capacity; claim dùng
  DB time + ordered `SKIP LOCKED` rồi commit trước external work. Fix replay đọc
  canonical `bytea`, không đọc text đã bị `jsonb` chuẩn hóa. PostgreSQL clean-run
  race pass 5/5; Release 40/40, BeGo 64 pass + 1 opt-in skip, RideBound 557/557.
  Chuyển duy nhất `RB-WP5-005` sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-005`. Thêm pinned long-lived Runner supervisor,
  bounded pool/NDJSON/stderr/timeout và exact negotiation/provenance/context/
  ACK-checkpoint guards. Audit sửa cleanup semaphore, process-tree child leak,
  dispose/start và removed-Lazy races. 16 adversarial process tests cùng một
  published RideBound Runner online gate pass; full BeGo Release với PostgreSQL
  và Runner thật 82/82 không skip, frontend 7/7, RideBound 557/557. Chuyển duy
  nhất `RB-WP5-006` sang In progress.
- 2026-08-05: Hoàn thành `RB-WP5-006`. Thêm two-phase immutable bootstrap mapper,
  HMAC run-local pseudonymization, restricted subject links, per-field provenance,
  E7/ms ties-to-even, bounded complete directed travel matrix và exact canonical
  negotiated manifest. Audit sửa circular pre-negotiation manifest identity,
  semantic conversion ordering và zeroization path. Mapper 16/16, supervisor +
  mapper 31/31, full BeGo Release trên fresh PostgreSQL 17 và published Runner
  thật 98/98 không skip; RideBound 557/557. Chuyển duy nhất `RB-WP5-007` sang
  In progress.
- 2026-08-05: Hoàn thành `RB-WP5-007`. Thêm host/member authenticated API,
  strict DTO/request bounds, Problem Details, stable cached replay, create/finalize/
  safe queries và server-owned timer sequence. Audit sửa semantic fingerprint,
  composite idempotency locking và rate-policy precedence; PostgreSQL→published
  Runner path thực pass. Pin `Microsoft.OpenApi 2.7.5` vá GHSA-v5pm-xwqc-g5wc;
  Release `/warnaserror` 0 warning, full BeGo 116/116, frontend 7/7, RideBound
  557/557. Chuyển duy nhất `RB-WP5-008` sang In progress.
- 2026-08-03: Hoàn thành `RB-WP4-013..014` và đóng WP4 bằng ADR-024. B1
  generator/selector khớp independent oracle trên 64 fixtures; production C1
  mapper + actual OR-Tools khớp independent enumerator trên 64 fixtures, mọi
  objective optimal/gap 0. Thêm hard-gate mutation witness, actual bounded-loss
  propagation và synthetic 4–128 option curve. Final source/config/Runner/claim
  audit ở `reviews/wp1-wp4-final`; required suite 557/557, Release/format/package/
  JSON/Markdown/process/diff gates pass. Windows Application Control 0x800711C7
  không tái xuất hiện và chỉ còn historical record. Chỉ `RB-WP5-001` refinement Ready.
- 2026-08-03: Hoàn thành `RB-WP4-012`. Thêm canonical B1–B5/C1/C2 registry,
  strict WP4 configuration và domain-bound commitment+algorithm hash; manifest
  phải khớp policy ID/version/hash. B1–B4/C1/C2 map exact hierarchy sang OR-Tools,
  B5 giữ pool selector; Runner revalidate effective policy rồi stage
  ledger/certificate/plan-pool/state/hash/ACK. Solver status nằm trong hashed
  decision. 7 Algorithms + 9 Runner cases mới, gồm child-process CLI; format sạch,
  required suite 523/523. Chỉ `RB-WP4-013` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-011`. Tách deterministic work budget cho
  generation/validation/solver, giữ candidate omission digest/saturation độc lập
  solver loss, và buộc mọi incumbent qua semantic validator injected. Portfolio
  fallback thử no-op rồi single-request theo lexicographic/ID; hết validation
  budget hoặc không pass trả Unknown không solution, không bịa incident. 12
  Application cases mới, format sạch, required suite 507/507. Chỉ
  `RB-WP4-012` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-010`. Thêm project adapter pin
  `Google.OrTools 9.15.6755`, exact-one/at-most-one CP-SAT constraints,
  Sum/Maximum integer objectives và multi-pass lexicographic optimum fixing.
  Một worker/seed/conflict/deterministic-time budget explicit; status và bound
  không bị nâng sai, solution được canonical revalidation. 5 solver cases + 1
  architecture case mới, format sạch, required suite 495/495. Chỉ
  `RB-WP4-011` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-009`. C2 dùng explicit 10-dimension warning
  profile, cùng one-pass hard gate C1, ordered warning-excess vector trước raw
  revision và không scalar hóa đơn vị. Warning phải finite-hard-bounded; toàn
  warning tắt gọi đúng selector C1. 6 Algorithms cases mới, format sạch,
  required suite 489/489. Chỉ `RB-WP4-010` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-008`. C1 assess+hard-filter trong cùng WP3
  validator pass, rank accepted/worst exact ceiling PPM/ordered 10-vector/cost/
  IDs; zero hard reserve rank saturated nhưng không đổi feasibility. Khi mọi
  hard limit unbounded, bỏ treatment ranking để semantic decision đúng B1. 6
  Algorithms cases mới, format sạch, required suite 483/483. Chỉ `RB-WP4-009`
  chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-007`. Thêm versioned canonical plan pool vào
  Application state/checkpoint, exact semantic plan ID, shared-pool enumeration
  với exact/bounded work semantics, Pareto dominance, max-min diversity và
  executable-prefix consensus. Chỉ distinguished được apply; alternative khác
  được rebase đúng next route version. Restore kiểm tra identity, assignment/
  frozen/physical và run equality. 12 cases mới (3 Application, 5 Algorithms,
  4 Runner), format sạch, required suite 477/477. Chỉ `RB-WP4-008` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-006`. Thêm B4 one-pair same-vehicle
  remove/reinsert cho waiting incumbent hoàn toàn trong mutable suffix, explicit
  cap và exact/bounded repair-loss accounting; frozen/onboard/assignment giữ
  nguyên, mọi route qua physical validator. Tách order-sensitive search-node
  digest khỏi order-insensitive omission-set digest sau khi differential case
  phát hiện route permutations bị đồng nhất. 7 Algorithms cases mới; format
  sạch, required suite 465/465. Chỉ `RB-WP4-007` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-005`. B2 đánh giá cùng raw pool với mọi
  cumulative limit unbounded và chọn accepted/material/10-vector/cost/ID; B3 chỉ
  hard-freeze theo horizon/lock explicit, inclusive boundary, không có numeric
  default và không rò source budget. Sửa canonical overflow ở vector và fleet
  cost. 8 Algorithms + 1 Domain cases mới, gồm B2 16-seed raw preservation;
  format sạch, required suite 458/458. Chỉ `RB-WP4-006` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-004`. Thay request-ID/hash cap bằng global
  deterministic best-first frontier; thêm work cap, tổ hợp count/saturation và
  stable omission digest. Diagnostics tách request omission, unknown-feasibility
  raw paths và known-feasible cap loss; exact mode fail-closed. 5 Algorithms
  conservation/priority/monotonic cases mới, format sạch, required suite 449/449.
  Chỉ `RB-WP4-005` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-003`. Thêm backward forward-slack certificate,
  bounded cache bind run/vehicle/position/route/time/travel, executable
  current-node origin hold và revalidation + exact service equivalence. Cache
  không đảo vai validator; vượt certificate không bị suy thành infeasible. 9
  Algorithms mutation/equivalence cases mới, format sạch và required suite
  444/444. Chỉ `RB-WP4-004` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-002`. Thêm solver-neutral
  `CandidateSelectionProblem`/solution/port trong Application: canonical model,
  đúng một option/vehicle, request uniqueness, ordered lexicographic
  Sum/Maximum, deterministic budget, exact bound/gap diagnostics và status
  truthful tách OPTIMAL/FEASIBLE/INFEASIBLE/UNKNOWN/MODEL_INVALID/SAFE_FALLBACK.
  20 Application adversarial cases + một Architecture boundary case; required
  `dotnet test RideBound.slnx` pass 435/435. Chỉ `RB-WP4-003` chuyển Ready.
- 2026-08-03: Hoàn thành `RB-WP4-001` refinement bằng ADR-023 và ordered queue
  `RB-WP4-002..014`. In-app Browser đọc lại nguồn bắt buộc và bổ sung waiting,
  forward-slack/feasibility, future-guidance cùng official OR-Tools/NuGet. Khóa
  common raw candidate/cap, executable origin hold, same-vehicle repair,
  canonical plan pool, multi-pass objective, deterministic solver budget,
  exact-small equivalence và WP3 publication gate. Chỉ `RB-WP4-002` Ready;
  chưa claim hay ghi production WP4 implemented tại mốc refinement.
- 2026-08-03: Re-run đúng required `dotnet test RideBound.slnx` sau khi Smart App
  Control không còn chặn fresh DLL: full solution pass 414/414 — Contracts 133,
  Domain 134, Application 34, Algorithms 48, Runner 58, Architecture 7; exit 0,
  không failed/skipped. Chuyển `0x800711C7` thành historical environment record,
  không còn là current blocker.
- 2026-08-02: Hoàn thành `RB-WP3-008..014` và đóng WP3 bằng ADR-022. Thêm
  incident/breach separation, independent full-state commitment validator,
  strict certificate/action/schema cross-binding, named configuration hash,
  Runner commitment publication/ACK, canonical checkpoint/restore và evidence
  property/mutation/exact-small/process. Audit toàn WP1–WP3 sửa thêm state-boundary,
  genesis-route, sequence exhaustion, pickup-window, breach/ledger và checkpoint
  reachability bugs; sửa cả WP2/WP3 demo pipe để stdin UTF-8 không BOM và không
  phụ thuộc PowerShell host. Browser recheck 5 paper khóa claim/tối ưu còn thiếu cho WP4.
  Inventory 414; Release build/format và WP1/WP2/WP3 clean replay pass. Required
  full solution bị host policy `0x800711C7`, được tách minh bạch thành suite/
  policy-safe/process evidence. Tạo review `docs/reviews/wp1-wp3/README.md`; chỉ
  `RB-WP4-001` refinement READY, chưa có production WP4 code.
- 2026-08-02: Bắt đầu `RB-WP3-008` sau khi đọc lại toàn bộ tài liệu bắt buộc,
  README, đặc tả và execution plan WP1–WP3; baseline full-solution tái hiện đúng
  Windows Application Control `0x800711C7` đã ghi trước đó. Chưa nâng trạng thái
  implementation; đang audit source và incident/breach boundary trước code.
- 2026-07-30: Revalidate WP2 end-to-end rồi commit riêng `07432ce`. Hoàn thành
  refinement ADR-021/queue 14 ticket và triển khai đúng nửa WP3 `001..007`:
  promise/policy/vector, shared schedule/promise projection, three-way delta,
  append-only ledger trong ACK boundary, budget và phase locks. Debug inventory
  378/378 suite evidence; chưa có incident/certificate/Runner/checkpoint, next duy nhất
  `RB-WP3-008`.
- 2026-07-30: Hoàn thành `RB-WP2-007..012`: deterministic candidate generator,
  exhaustive B1 selection/apply, independent exact-small oracle 32/32 seeds,
  default online produced Runner decisions/ACK hash chain và four-epoch tiny
  demo chạy hai clean single-file process với exact final hash. Logical test
  inventory là 333; required Debug solution pass 333/333, Release build/format
  pass và Release blocked suites pass qua policy-safe bundles/process checks.
  Release xUnit host policy chặn fresh unsigned DLL bằng `0x800711C7` trước
  assertion, được ghi như environment exception chứ không tính Release pass.
  ADR-020 đóng WP2 physical/B1; next
  duy nhất là `RB-WP3-001` refinement, chưa có ledger code.
- 2026-07-29: Hoàn thành `RB-WP2-002..006`: typed online schemas/fixtures,
  immutable Domain lifecycle/route, manifest-bound travel snapshot, atomic
  mapper/reducer/ack và independent physical validator. Debug/Release full
  solution pass 278/278; 24 small route permutations và mutation dimensions
  pass. ADR-019 khóa executable semantics; next duy nhất là `RB-WP2-007`.
- 2026-07-29: Revalidate toàn bộ WP1: Release 161/161; Debug pass 123 non-Runner
  test rồi fresh Runner assembly bị host policy chặn trước discovery. Phát hiện
  payload-only dedup hash nhận nhầm retry đổi `simTimeMs`; sửa bằng ADR-017 để
  hash toàn canonical eventBatch, thêm regression conflict và replay qua hai
  clean runner process. Hoàn thành `RB-WP2-001` refinement bằng ADR-018 và
  execution plan `RB-WP2-002..012`; next duy nhất là `RB-WP2-002`.
- 2026-07-29: Hoàn thành `RB-WP1-008..015` và đóng WP1/Q1: event/decision/error
  contracts, hash chain, async NDJSON runner, lifecycle/idempotency/failure
  semantics, đúng 10 golden fixture và exact replay/tamper proof. Release full
  suite pass 157/157 tại mốc đóng; assertion đồng bộ vocabulary thêm sau đó pass,
  đưa Contracts lên 115 và inventory lên 158. Format/build/vulnerability audit
  sạch. Lần full-suite cuối bị enterprise Code Integrity chặn fresh Runner DLL
  với `0x800711C7` trước assertion cả ở Release; Debug cũng bị policy này chặn
  các fresh DLL. Next duy nhất là refinement `RB-WP2-001`, chưa có WP2 code.
- 2026-07-29: Hoàn thành `RB-WP1-005..007`: schema/inventory/compatibility
  assets, hello capability negotiation và immutable initialize identity.
  Required full solution test pass 114/114; Release build/format/dependency audit
  sạch. Release-only Domain smoke vẫn bị Windows Application Control chặn
  `0x800711C7` sau khi 113 test khác pass; next là `RB-WP1-008`.
- 2026-07-29: Hoàn thành `RB-WP1-002..004`: contract fixture harness, typed
  envelope/validation, canonical unit conversion và exact canonical JSON bytes.
  Contracts 66/66 và Architecture 7/7 pass từ independent artifacts path;
  Release build/format/dependency audit sạch. Full solution test vẫn bị Windows
  Application Control chặn Domain smoke (`0x800711C7`); next là `RB-WP1-005`.
- 2026-07-29: Hoàn thành docs ticket `RB-WP1-001` bằng ADR-014 và protocol
  decision checklist; thu hẹp O-006 sang FleetPy executable preflight; chuyển
  `RB-WP1-002` thành next/`READY`. Không thêm runtime/schema code và chưa nâng
  trạng thái implementation của WP1. Full RideBound regression pass 8/8; kiểm
  48 Markdown file có 0 internal link hỏng và 0 code fence lệch.
- 2026-07-28: Chuyển WP1 sang `READY`, thêm delivery policy và 15 ticket chi tiết;
  next action là `RB-WP1-001`. `dotnet test RideBound.slnx` hiện pass 8/8;
  không có implementation status nào được nâng.
- 2026-07-28: Sửa false positive architecture test trên Linux và mở rộng CI quality
  gates; Release build local sạch, test local chờ Linux CI xác nhận do Windows
  Application Control chặn nạp DLL test.
- 2026-07-28: Tách RideBound thành Git repository riêng, hoàn tất WP0 scaffold và chuyển next action sang WP1.
- 2026-07-27: Hoàn tất kiểm tra cấu trúc và mã hóa; chuyển docs sang `COMPLETE_V1_VERIFIED_PENDING_USER_REVIEW`.
- 2026-07-27: Khởi tạo status log và docs v1.


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
