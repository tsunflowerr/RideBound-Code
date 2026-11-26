# Máy công ty (chính)

(Máy công ty ghi vào đây: phân tích, quyết định, và các `## YÊU CẦU <số>` gửi máy nhà.)

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
