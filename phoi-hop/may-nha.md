# Máy nhà (phụ)

## TRẢ LỜI 5 — 2026-11-27 09:35 (YÊU CẦU 5: ô F14 FS-G2-SHHZ71-N d20191216 r1 w08 chạy thật)

Chạy ở `ab697fe` (1073 s) và ở đầu nhánh `4ad7d39` (1033 s), cấu hình sinh bằng `make_job_configs.py` của chính commit
đó từ panel F14 đã lưu, giữ transcript. Lượt thứ ba để so: evidence đã lưu của đợt 360 lượt cho ô này (chạy trên Linux).
**Trường hợp [10.03] có xảy ra (K = 1) và không đổi gì ngoài một lần phát.**

1. **Bảng từng khách** (361 khách): `--out` sha256 `4e38f88f2f05b5e1…` và `--sequences` `a76f4fd4f2d3b39a…` **giống từng byte ở
   cả ba**: lượt `ab697fe`, lượt đầu nhánh, evidence đã lưu của đợt 360 lượt.
2. **Số lần phát:** P_ab697fe = 94200, K = 1, P_đầu nhánh = 94199 = P_ab697fe − K. Lần sửa bị bỏ: requestId
   `rq-8b25d05f1b5c2b2792a9c1304590ee29e24ba6121b2c9804b38b7e6ddb74e4c8`, `simTimeMs` 998000, promiseVersion 25; trong 5
   chiều đã gỡ chỉ `prePickupInsertedStopCount` khác 0 (đúng trường hợp "chèn trước điểm đón" ở nhận xét [10.06]).
3. **Quyết định:** 3023 khung `decision` ở cả hai; các hành động `vehiclePlanUpdated` của từng khung giống nhau theo thứ tự
   (đã bỏ khóa đuôi Hash/Sha256).
4. **summary** (`ab697fe` / đầu nhánh / đã lưu): requests 512, promisedRiders 361, sumDropC 60165772, decisionsAcknowledged
   3023, breaches `{exogenousServiceQuality: 6356}`, latePickups 7, latePickupMaxMs 11886: bằng nhau ở cả ba.

Kết luận: giữ [10.03]. Không khác chỗ nào để gửi.

## TRẢ LỜI 4 — 2026-11-27 08:55 (YÊU CẦU 4, các gói Python, ở `8a0d305`)

1. 5 module README §2.3: **42 qua, 0 hỏng, 0 bỏ qua**. `EvidenceTests.test_evidence_of_a_small_job` qua, tức hash ghim
   `9d718888…` đúng với Python.
2. `discover` có `RIDEBOUND_FLEETPY_ROOT`: **119 qua, 0 hỏng** (134 ở `ab697fe`).
3. `replay.py adapter` trên 4 transcript B1 (cùng đối số như lượt B1, thư mục ra mới): cả 4 `identical: true`,
   `jobOk: true`, `initializeRunRebound: true`; khung kiểm 8545 / 9436 / 8632 / 9274; 617–672 s mỗi lượt.
4. Với 4 job vừa sinh:
   - `riders.py` đọc transcript: `--out` và `--sequences` **giống từng byte** bảng mốc `ab697fe` (và B1) của cả hai nhánh.
   - Chép job sang root khác **không kèm** transcript, `riders.py` đọc `evidence.json.gz` mới: cũng **giống từng byte**.
   - So evidence mới với evidence của YÊU CẦU 1: `audit.table`, `finalOnlineState`, `events`, `moves` (bỏ `firstMoves`),
     `shadowAllocations`, `format`, `role`, `job` bằng nhau; `summary` còn lại (`requests`, `promisedRiders`, `sumDropC`,
     `decisionsAcknowledged`, `breaches`, `ridersWithForcedBreach`, `latePickups`, `latePickupMaxMs`) bằng nhau. Chỉ thiếu
     đúng các trường đã gỡ: `E1ServedShare`, `E2ShareDropVAbove60s`, `dropVCvar95Ms`, `lifecycle`, `nonNormalCertificates`,
     `sumDropV`, `sumDropX`, `sumPickupC`, `sumPickupV`, `sumPickupX` và khóa gốc `transcript`. Không có khóa mới.
5. `make_run_drivers.py write <sealed> <out> complete-graph-direct-v1` cho w08 và w17: sha256 bằng tệp đang dùng
   (`b2dc50ad…`, `0f673f19…`). Ghi chú nhỏ: lệnh với chế độ cũ `check` nay dừng bằng `IndexError` (traceback) thay vì in
   cách dùng; nếu muốn gọn, thêm kiểm số đối số.
6. Bảng mô phỏng nhanh trên rows đã lưu (`ca96d32` và đầu nhánh, `PYTHONHASHSEED=0`):
   - `aggregate.py`: `aggregate.json` mới = cũ bỏ `B`, `B_rows`; stdout mới = cũ bỏ đúng 9 dòng "dự báo N/CL/CLc/PAD",
     "CL-N", "CLc-N", "CLc-CL", "CL-PAD", "PAD-N" (khác thêm dòng đường dẫn "đã ghi …", do thư mục worktree).
   - `gen_tables.py`: stdout mới = cũ bỏ khối từ "% Bảng 5.7" tới ngay trước "% Ngân sách (10 ngày):" (đúng từng byte).
     Chưa so lại từng ô với .tex 22/11 lần này; phần còn lại giống từng byte bản cũ, bản cũ đã khớp .tex ở lần kiểm 23/11.
   - `eta_analysis.py` (stdout và `eta_analysis.json`), `gen_eta_tables.py`: giống từng byte. `git status` sạch ở cả hai.
   - `make_figs.py`: **không chạy được ở máy nhà**, ở cả hai bản: Python thoát mã 127, không thông báo, ngay sau khi nạp
     `matplotlib.backends.backend_agg` (matplotlib 3.10.9). Lỗi môi trường, chưa kiểm PNG.
   - `calib_pad.py` chưa chạy lần này.
7. Không làm riêng: phần này đã có ở YÊU CẦU 3 bước 2 và bước 3–4 ở trên (replay Runner và adapter ở đầu nhánh, riders giống mốc).
8. Không biết script nào sinh `publication-projection-*.jsonl.gz`; trên máy nhà chỉ có tệp đã lưu và `recompute_from_evidence.py`
   (ngoài repo). Chưa kiểm.

## TRẢ LỜI 3 — 2026-11-27 08:55 (YÊU CẦU 3, các gói [10.08]–[10.13], ở `8a0d305`)

1. `dotnet test src/core/RideBound.slnx -c Release`: **794 qua, 0 hỏng, 0 bỏ qua** (81 s). Theo dự án: Architecture 9,
   Contracts 122, Domain 218, Application 100, Solvers 14, Algorithms 171, Runner 160. Số cho `README.md:132`: 7 dự án,
   794 test.
2. `replay.py runner` trên 4 transcript B1 với Runner build ở đầu nhánh (`runnerDllSha256` `cd33111f…`): mọi khung Runner
   **giống từng byte**:

   | B1 | khung so | giống | thời gian | RSS đỉnh Runner |
   |---|---|---|---|---|
   | SHHZ71-N w08 | 2849 | 2849 | 674 s | 3922 MiB |
   | SHHZ71-N w17 | 3146 | 3146 | 551 s | 4291 MiB |
   | SHRC71-CL w08 | 2878 | 2878 | 474 s | 4396 MiB |
   | SHRC71-CL w17 | 3092 | 3092 | 510 s | 4291 MiB |

3. Không chạy trên 8 transcript ô cổng của đợt 360 lượt: chúng do code thời khóa luận ghi, vectơ cam kết còn 10 trường và
   trạng thái còn `incidents`, nên sau [10.03]/[10.04] không thể giống từng byte (đổi định dạng có chủ ý). Nếu cần, có thể
   chạy để xác nhận khung khác đầu tiên là khung đầu tiên có `promisePublished` hoặc checkpoint.
4. Mọi `wp4/*/*.json` của B1 và của mốc đều có `"auditHash": "light-v1"`.
5. `kiem_nhanh.sh`: `sample check: 36 rows equal, 0 differ, 0 without an expected row` (258 s).
6. `network.sha256`: OK (toàn bộ). `wf14-60day-cells.sha256`: 6 tệp của 2 ô mẫu OK; bộ ô đầy đủ 60 ngày và F16 không có
   trên máy nhà ở chỗ có thể kiểm, nên chưa kiểm `wf14-60day-cells` đầy đủ và `wf17-f16-cells`.
7. `make_job_configs.py`: `commitment/SHRC71.json` `40244dbf…`, `SHHZ71.json` `f01cf219…` (bằng fixture `Final/`); wp4 chỉ
   khác `networkDirectory` so với cấu hình B1.
8. `node src/datagen/export_network.js manhattan <thư mục mới> $RB_DATA`: `D.u16` `4ae39087…` (= `ff_apsp_u16.bin`),
   `network.json` `52e30836…`; cả hai bằng mạng mà các lượt B1 dùng (sinh ở `ab697fe` bằng lệnh cũ).
9. `export_network.js toy … 24 10 20261103`: phần từ `"cells"` giống `shareability-golden-toy.json`.
10. `emulate_fullsys.js adapter` trên 2 ô mẫu (`FS_ONLY`): `fullsys-f14-predictions.tsv` và 2 tệp `shadow-inputs` giống bản
    sinh ở `ab697fe`. Chưa chạy `emulate_fullsys_f16.js` và `emulate_forecast_gate.js` (cần panel đầy đủ).
11. Không chạy (không bắt buộc).

Python ở đầu nhánh: xem TRẢ LỜI 4.

## TRẢ LỜI 2 (bổ sung) — 2026-11-27 07:52: N theo từng lượt

N = 11, cả 11 thuộc 360 lượt chính (4 lượt `gates`: 0). Mỗi lượt dưới đây có N = 1:

| Tập | Lượt |
|---|---|
| F14 | SHHZ71-N d20191216 r1 w08; SHRC71-CL d20191216 r1 w08; SHHZ71-N d20191003 r1 w08; SHHZ71-N d20191007 r2 w17 |
| F16 | SHHZ71-N d20191204 r2 w08; SHHZ71-N d20191016 r1 w08; SHHZ71-N d20191029 r1 w08; SHHZ71-N d20190920 r2 w17; SHHZ71-N d20191204 r1 w08; SHHZ71-N d20191017 r1 w08; SHRC71-CL d20191002 r2 w08 |

Theo nhánh: SHHZ71-N 9 (F14 3, F16 6), SHRC71-CL 2 (F14 1, F16 1). Nếu muốn kiểm thật trường hợp này, ô ứng viên có sẵn
dữ liệu trên máy nhà: SHHZ71-N d20191216 r1 w08 (F14), chạy ở `ab697fe` và ở đầu nhánh rồi so `riders.py`
(khoảng 2 × 20 phút). Chờ máy công ty quyết.

Đang chạy YÊU CẦU 3 + 4 ở `8a0d305`. Đã xong và khớp: `dotnet test` 794 qua / 0 hỏng; unittest 42/42 và 119/119
qua (hash `EvidenceTests` = `9d718888…`, test qua); `kiem_nhanh.sh` 36 equal / 0 differ / 0 without (258 s);
`network.sha256` OK và 6 tệp 2 ô mẫu OK trong `wf14-60day-cells.sha256` (bộ ô đầy đủ không có ở máy nhà);
`export_network.js manhattan` mới: `D.u16` `4ae39087…`, `network.json` `52e30836…`; `make_job_configs.py`:
`SHRC71.json` `40244dbf…`, `SHHZ71.json` `f01cf219…`, wp4 chỉ khác `networkDirectory`, đều `light-v1`;
`export_network.js toy` từ `"cells"` giống golden; `emulate_fullsys.js adapter` (2 ô mẫu): predictions và
`shadow-inputs` giống `ab697fe`; `make_run_drivers.py write` 2 ô: giống `drivers-run/` (chế độ `check` đã gỡ nay báo
`IndexError` thay vì lời dùng); bảng mô phỏng nhanh trên rows đã lưu (`ca96d32` vs đầu nhánh): `aggregate.json` bằng nhau
sau khi bỏ `B`, `B_rows`, stdout chỉ mất đúng các dòng nêu trong yêu cầu; `gen_tables.py` mới = cũ bỏ khối "% Bảng 5.7"
đến trước "% Ngân sách (10 ngày):"; `eta_analysis.py` (stdout, json), `gen_eta_tables.py` giống; `git status` sạch.
`make_figs.py` không chạy được ở máy nhà (Python thoát mã 127 ngay sau khi nạp backend matplotlib, ở cả hai bản): lỗi môi
trường, chưa kiểm hình. Còn lại: replay Runner trên B1 (đang chạy), `replay.py adapter` + riders hai đường đọc + so evidence.

## TRẢ LỜI 2 — 2026-11-27 07:55 (YÊU CẦU 2, dữ liệu đã lưu của đợt 360 lượt)

**1. Tám transcript ô cổng** (F14: 19/9, F16: 20/9; mỗi ngày SHHZ71-N, SHRC71-CL × w08, w17). Số khung khớp:

| Transcript | khung | restore | incident | declined/cancelled | offer/breach/finalize/summary | waypoint | reasonCode của promisePublished |
|---|---|---|---|---|---|---|---|
| SHHZ71-N 0919 w08 | 8545 | 0 | 0 | 0 | 0 | 0 | INITIAL_BOOKING_CONFIRMATION 335, ONLINE_REPLAN 87272 |
| SHHZ71-N 0919 w17 | 9436 | 0 | 0 | 0 | 0 | 0 | INITIAL 379, ONLINE_REPLAN 98470 |
| SHRC71-CL 0919 w08 | 8632 | 0 | 0 | 0 | 0 | 0 | INITIAL 339, ONLINE_REPLAN 86772 |
| SHRC71-CL 0919 w17 | 9274 | 0 | 0 | 0 | 0 | 0 | INITIAL 371, ONLINE_REPLAN 93068 |
| SHHZ71-N 0920 w08 | 8545 | 0 | 0 | 0 | 0 | 0 | INITIAL 336, ONLINE_REPLAN 84779 |
| SHHZ71-N 0920 w17 | 9463 | 0 | 0 | 0 | 0 | 0 | INITIAL 380, ONLINE_REPLAN 100076 |
| SHRC71-CL 0920 w08 | 8335 | 0 | 0 | 0 | 0 | 0 | INITIAL 326, ONLINE_REPLAN 79248 |
| SHRC71-CL 0920 w17 | 9247 | 0 | 0 | 0 | 0 | 0 | INITIAL 370, ONLINE_REPLAN 97949 |

Mẫu được tìm trên khung đã giải base64 (cả khung snapshot lớn). Chỉ có hai `reasonCode`.
Ô SHHZ71-N 0919 w08: 335 + 87272 = 87607, bằng P0 của mốc chuẩn ở TRẢ LỜI 1.

**2. Evidence.** Kho lưu có 364 `evidence.json.gz` = 168 (F14) + 192 (F16) + 4 lượt thư mục `gates` (giai đoạn cổng trước đợt chính).
- `summary.json` status: `pass` cả 364.
- `finalOnlineState.incidentLedger.incidents` rỗng ở cả 364.
- Tổng `nonNormalCertificates` = 0.
- `requests[].lifecycle` ⊂ {completed 127365, rejected 59003}.
- Không evidence nào chứa `EXOGENOUS_BREACH_BRIDGE_FAILED`, `COMMITMENT_SAFETY_NOOP_REJECTED`, `SOLVER_MODEL_MAPPING_FAILED`,
  `INTERNAL_ERROR`.
- **N = 11 ≠ 0** (tính trên cả 364): SHHZ71-N 9, SHRC71-CL 2. Tức có 11 lần sửa trong đợt cuối mà giờ đón và giờ trả
  đều không đổi (v = e = p ở cả hai dòng). Các lượt đã thấy, mỗi lượt N = 1: SHHZ71-N d20191216 r1 w08, SHRC71-CL
  d20191216 r1 w08, SHHZ71-N d20191003 r1 w08, …, SHHZ71-N d20190920 r2 w17, SHHZ71-N d20191204 r1 w08. Danh sách đủ 11
  và phần tách khỏi 4 lượt `gates` sẽ bổ sung ở mục sau (đang đọc lại kho).
  Hệ quả: N chưa trừ các lần sửa có đổi xe hay điểm dừng (những lần đó vẫn được phát sau [10.03]), nên số lần phát
  mất đi ở 360 lượt ≤ 11. Theo lập luận [10.03] chúng không đổi bảng từng khách; 4 ô mẫu đều có K = 0 nên phép chạy lại
  ở TRẢ LỜI 1 không chạm tới trường hợp này. Nếu muốn kiểm thật, cần chạy lại một trong các ô trên (ví dụ
  SHHZ71-N d20191216 r1 w08, có trong F14) ở `ab697fe` và ở đầu nhánh.

**3. FleetPy** (commit `053aa9d`):
- `src/misc/init_modules.py` có 12 lời gọi `dev_content.` (dòng 53, 70, 92, 111, 121, 137, 148, 159, 170, 182, 197, 214),
  mỗi lời gọi nằm trong `if dev_content is not None`.
- `FleetControlBase.time_trigger` (dòng 528) không gọi `user_cancels_request`: chỉ cập nhật mạng, reservation, rồi
  `_call_time_trigger_request_batch` và `_call_time_trigger_additional_tasks`.

## TRẢ LỜI 1 — 2026-11-27 07:55 (YÊU CẦU 1, 4 lượt trên `00fd3be` [10.04], mốc chuẩn `ab697fe`)

Tất cả như dự đoán. sha = 16 ký tự đầu của sha256 phần bảng của lượt đó (dòng tiêu đề + các dòng của ô), mốc / mới:

| Lượt | `--out` | `--sequences` | P0 | K | P1 | `summarize` khác ở | thời gian |
|---|---|---|---|---|---|---|---|
| SHHZ71-N w08 | `455395f396749b18` = | `4a00f0be23edd4a2` = | 87607 | 0 | 87607 | không trường nào | 1070 s |
| SHHZ71-N w17 | `9ac41fb984e5e2ea` = | `4b6cdb34181f767a` = | 98849 | 0 | 98849 | không trường nào | 1082 s |
| SHRC71-CL w08 | `f44affe1577b71b2` = | `3574d870c3635140` = | 87111 | 0 | 87111 | không trường nào | 1031 s |
| SHRC71-CL w17 | `2df850e5c1acda49` = | `02249e3ea867c6d8` = | 93439 | 0 | 93439 | không trường nào | 1056 s |

- Bảng từng khách: 335, 379, 339, 371 khách, giống từng byte cả hai bảng.
- `summarize` (`audit.summary` trong evidence, bỏ `transcript`): mọi trường bằng nhau, kể cả các trường bắt buộc bằng.
  `semanticHash` của `summary.json` khác, như dự kiến.
- Thời gian là `wallMilliseconds` của `summary.json`; cả lượt (gồm evidence, riders) 1124–1177 s. Mốc chuẩn: 1085–1505 s.
- RAM đỉnh tổng python + dotnet đo được 8,4–9,1 GB, nhưng trong lúc đó có thêm một tiến trình Python của YÊU CẦU 2 chạy
  song song (tới 2,7 GB). Ước lượng một lượt đứng riêng ≈ 6 GB, khớp lần đo 23/11 (≈ 5 GB).
- Test Python adapter trên `00fd3be`: 5 module README 42/42 qua; `discover` có FleetPy 133/134 qua, hỏng
  `test_mapping…test_cross_language_configuration_hash_and_binding_are_stable` vì hash ghim của
  `wp3-boundary-test-v1.json` chưa cập nhật sau [10.03]. Đã sửa ở `45a5690` (commit trùng số [10.07] với máy công ty,
  từ nay máy nhà dùng hậu tố `n`).
- **Mốc byte B1** (giữ lại, ngoài repo), sha256 / byte của `transcript-00.ndjson.gz`:
  - SHHZ71-N w08 `9598ef063697a214d2a5b3f251e334d06519d50859e8f40651407b9496927c08` 750458404
  - SHHZ71-N w17 `54d97c87fdf31492bd31a097f94c4127b5bf6471445cd7c53b60ae55ab869d15` 760709393
  - SHRC71-CL w08 `060dcf65510d23dba21c0c71b98c100b9dfc0829a927529acba2529b15241030` 749501244
  - SHRC71-CL w17 `624c6a8cc702e09e6e825b8f157c167a91ab26fef45d657aa0e9bae21cbea5f9` 751124620

Đang chạy: YÊU CẦU 3 + 4 ở `8a0d305` (dotnet test, unittest đã bắt đầu; replay trên B1 tiếp theo).

## 2026-11-27 — Trạng thái đầu tiên

Bốn commit gỡ code trên `ab697fe`:

| Commit | Gỡ gì |
|---|---|
| `[10.01]` | mã từ chối các khóa cấu hình đã gỡ (WP4 + cam kết), thông điệp "removed" của Program.cs, cấu hình test WP14 cũ |
| `[10.02]` | khóa `dropStop`, `dropEta` và ratchet `dropEta` (không cấu hình nào đặt) |
| `[10.03]` | 5 chiều cam kết chỉ để ghi sổ (`material_eta_revision_count` + `MaterialRevisionRule`, 2 chiều `*_relocation_mm` + bảng khoảng cách điểm dừng, `incumbent_order_inversion_count`, `pre_pickup_inserted_stop_count`) |
| `[10.04]` | sự kiện `incidentOpened`/`incidentResolved`, `OperationalIncident`, hành động `commitmentBreachDeclared`, mã `INCIDENT_OVERRIDE` |

Đã kiểm:
- Mốc chuẩn trên `ab697fe`: 884 test C# qua; 134 test Python (có FleetPy) qua; `kiem_nhanh.sh` = 36 equal / 0 differ.
- Lượt C# mốc chuẩn `FS-G2-SHHZ71-N` w08: 335 khách, 1505 s; bảng `riders.py` **giống từng byte** lượt chạy cùng code
  ngày 23/11, tức kết quả tất định. Ba lượt mốc chuẩn còn lại đang chạy.
- Sau mỗi nhóm: toàn bộ test C# qua (884 → 849 → 846 → 833 → 825; số giảm là test chỉ kiểm thứ đã gỡ và test lặp theo
  từng chiều cam kết).
- 13 lượt golden của Runner (`tests/RideBound.Runner.Tests/Final`): stdout cũ (`ab697fe`) và mới giống nhau từng dòng
  sau khi bỏ các trường đã gỡ và che mọi hash 64 ký tự; không lượt nào mất hay thêm lời hứa.

Chưa làm:
- Lượt C# + FleetPy sau khi sửa (so `riders.py` với mốc chuẩn): sẽ chạy trên `[10.04]` cho cả 4 lượt mốc chuẩn và báo ở đây.
- Mã từ chối "removed" trong Python adapter; chế độ băm `full-v1` (360 lượt dùng `light-v1`): đang cân nhắc.

Giữ lại vì khóa luận có nhắc hoặc nằm trên đường quyết định: chiều `pickup_eta_total_ms`, `vehicle_switch_count`, hai
chiều đổi điểm dừng (hạn 0), ratchet giờ đón, sổ vi phạm do giao thông và đón trễ, checkpoint/restore, hash quyết định,
chế độ INS.
