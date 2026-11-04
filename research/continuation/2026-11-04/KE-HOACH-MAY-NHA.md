# Kế hoạch ở nhà sau ngày 04/11/2026: từ đợt chạy cuối trên hệ thật đến refactor

Viết cho: chủ dự án và Claude ở máy nhà (máy có ổ `E:`).
- Làm theo thứ tự A → F.
- Mỗi bước có điều kiện đạt và điều kiện dừng. Gặp điều kiện dừng thì ghi lại và hỏi chủ; không sửa số, không xóa gì.
- Kế hoạch đã được kiểm chéo với repo ngày 04/11. Chỗ nào chỉ kiểm được trên `E:` thì ghi "kiểm ở nhà".

## 0. Nguyên tắc

- **Không refactor trước khi xong A–D.** Đợt chạy cuối dùng Runner trong gói `final-v2`.
- **Không xuất lại gói `final-v2`, và không sửa worktree đang chạy job.** Worktree đó bị ghim bằng hash kho mã.
  - Nếu buộc phải dựng lại gói (ví dụ để có các sửa của ADR-090), phải chạy lại **mọi** cổng trên binary mới:
    - 3 job cổng Shadow đã đạt, cùng 5 job ở B2;
    - cổng dự báo;
    - G1/G2 phát lại 228 job;
    - biên nhận từng khung mới ở máy nhà cho B1.
  - Kèm theo đó là biên bản build mới.
- **Không xóa dữ liệu**, trừ một việc đã định sẵn: `finalize_job.py` bỏ transcript của một job sau khi bằng chứng đọc lại
  qua `evidence_reader.py` trùng, theo chính sách của từng job. Job dở được giữ dưới tên `.interrupted-k`.
- **Lệch khác hỏng.**
  - Job hỏng vì hạ tầng (hết RAM, máy khởi động lại, mất điện): để `run_queue.py` chạy lại, và ghi lý do.
  - Khách lệch dự đoán: giữ nguyên số, ghi lại, tìm nguyên nhân. Không chỉnh dự đoán, không chạy lại để "cho trùng".
- **Git:** tác giả tsunflowerr <23020155@vnu.edu.vn>, không trailer AI. Chỉ push khi chủ đồng ý.

## A. Chuẩn bị (ở nhà, khoảng nửa ngày)

**A1. Lấy code mới.**
- Dùng một bản clone riêng của `https://github.com/tsunflowerr/RideBound-Code.git`, không phải worktree chạy job và không
  phải `E:/Code/RideBound`. Trong đó chạy `git fetch origin`, rồi `git switch research/fixes-perf-2026-11-04`.
- Đọc:
  - ADR-090 trong `docs/18`;
  - `simplification-audit/KE-HOACH-REFACTOR.md`;
  - `MUC-9-HE-THAT-VA-BO-MO-PHONG.md` (mục 9 mới của báo cáo).
- Đợt chạy cuối **không cần** code này:
  - Các lỗi ADR-090 sửa chỉ hiện ở cấu hình mà các lần chạy đã ghi không dùng. Các lần chạy đó dùng `intervalMs`
    600 000, `binMs` 900 000, `earliest-feasible`, và chỉ một chiều quỹ.
  - Riêng sửa 1 (khoảng thời gian của bộ chấm điểm Shadow) cho kết quả trùng từng bit ở 600 000.
  - Đây là lập luận. Cấu hình của gói chưa được so byte; kiểm ở A3.
  - Gói `final-v2` dựng trước ADR-090, nên đằng nào cũng không chứa các sửa này.

**A2. Kiểm gói `final-v2` và ghi biên bản.**
- **Giải nén:** dùng .NET (`System.IO.Compression`), **không** dùng `tar.exe` của Windows, vì nó bỏ qua tệp có tên UTF-8.
- **Kiểm gói:** `ridebound-final-v2.zip` phải có sha256 `467c735b…`; đủ 66.545 tệp; sha256 từng tệp khớp `MANIFEST.tsv`.
- **Biên bản Runner.** Theo dạng `research/continuation/2026-10-26/kits/tang3/stage1b/harness/RUNNER-PROVENANCE-v3.md`
  (mẫu trống là `RUNNER-PROVENANCE-TEMPLATE-v7.md` cùng thư mục), ghi vào `docs/18`:
  - Commit xuất `0eb9a6ca` không có trên GitHub. Ghi `git diff --stat` từ commit công khai `9ba5568` tới nó, cho `src` và
    `simulators`.
  - Xác nhận Runner được publish từ chính repo xuất. Gói `final-v1` từng có lỗ hổng: Runner build từ worktree phát triển.
  - Ghi SDK, sha256 cây Runner, `deps.json` và `RideBound.Runner.runtimeconfig.json`.
  - Ghi `run_cloud.ps1` dùng .NET 10.0.9 đi kèm hay `dotnet` của máy. Nếu dùng `dotnet` của máy, runtime có thể tự nâng
    lên bản 10.0.x khác.
  - Mục nào lúc build không ghi lại thì viết "không có bằng chứng", không suy ra.
- **Môi trường job** do `run_queue.py`, launcher và `run_cloud.ps1` của gói đặt. Ghi lại:
  - thời gian chờ Runner 900 s (adapter mặc định 60 s);
  - `RB_REALJOB_BUFFERED_READ=1`;
  - giới hạn dòng **hai chiều** đủ cho ảnh chụp thời gian đi khoảng 130 MB và bản chốt cuối khoảng 768 MB;
  - `DOTNET_GCHeapHardLimit`;
  - không đặt `RIDEBOUND_STATE_HASH`.

**A3. Ghi sổ những gì nằm ngoài repo** (bước 0.4 của kế hoạch refactor; làm luôn vì rẻ).
- Ghi sha256 các cấu hình cuối lấy từ `MANIFEST.tsv` vào `docs/18`.
- Grep mọi `*.json` trong gói và trong `fastrun-2026-11-02`, tìm:
  - các khóa `fixedFreeze`, `repair`, `multiplePlan`, `origin-hold`, `maximumRidersPerVehicle`, `stopServiceDurationMs`, `freezeHorizon`, `dropEtaDeadlineTwoSided`, `solverExecutionEvidenceProfile`;
  - `stopDistances` không rỗng;
  - các giá trị `policyId`.
  - Đồng thời kiểm mỗi nhánh Shadow chỉ có **một** khóa 1000‰: `dropEtaBudgetSlackPermille` hoặc
    `dropEtaDeadlineSlackPermille`. Đây chính là điều sửa 2 của ADR-090 bắt buộc.
- Grep `*.py` và `*.ps1`, tìm: `ridebound_fleetpy`, `restore(`, `restart`, `_repository_inventory_sha256`, `wp14r_`, `RIDEBOUND_STATE_HASH`, `RB_REALJOB_BUFFERED_READ`.
- **Chép nguyên tệp** `engine_wf15_clint.js`, `emulate_via_full.js` và `engine.js` của wf15 vào chỗ lưu lâu dài, rồi ghi
  sha256 vào `docs/18`. Đây là bộ sinh dự đoán adapter-mode duy nhất; engine trong repo không sinh lại được dự đoán cho
  nhánh có dự báo.

**A4. Chọn nơi chạy.**

| | Máy thuê Windows (khuyến nghị) | Máy nhà |
|---|---|---|
| Số job song song | Tối đa 8, chốt sau B3. 8 × 8,0 GB = 64 GB là vừa khít, nên chọn máy có RAM trên 64 GB hoặc chạy 6–7 job; cần ít nhất 8 lõi vật lý | 2 (giới hạn commit khoảng 20,5 GB) |
| 168 job (ngoại suy tuyến tính) | khoảng 5–8 giờ | khoảng 21–30 giờ |
| Đĩa | Đọc sàn đĩa trong `run_queue.py`. Nếu giữ sàn như kit trong repo (C ≥ 30 GiB, E ≥ 40 GiB) và `E:` là subst cùng ổ vật lý, cần khoảng 60–70 GB trống. Mỗi job đang chạy dùng 0,7 GB; sau `finalize_job.py` còn 12 MB | `E:` đang ở sàn. Trước tiên kiểm bằng quyền admin vì sao `E:` tụt 4,3 GiB ngoài thư mục RideBound khi chạy job (nghi VSS). Mọi thư mục cũ đều được tệp khác tham chiếu, nên chuyển đi sẽ làm gãy đường dẫn. Chỉ chuyển khi chủ đồng ý, và kiểm sha256 |
| Điều kiện chung | Cùng đường dẫn `E:` (subst), cùng Python/FleetPy, .NET và Runner như A2 | — |

Trong suốt đợt chạy:
- tắt chế độ ngủ và việc Windows Update tự khởi động lại;
- nếu hàng đợi chỉ chạy khi cắm điện, kiểm máy ảo có báo nguồn AC hay không.

Lưu ý: 15–21 phút và 5,2–8,0 GB mỗi job đo trên job **không** có Shadow và dự báo, khi máy có tải khác.

## B. Cổng trước đợt chạy (trên máy sẽ chạy đợt cuối)

**B1. Máy thuê giống máy nhà** (chỉ khi dùng máy thuê).
- Chạy lại đúng các job mà máy nhà đã chạy **từ gói `final-v2`** và còn biên nhận sha256 từng khung. `docs/18` ghi 3 job
  như vậy: `SHRC71-CL` w08, `SHHZ71-N` w08 và `SHRC71-CL` w17.
- Không dùng job chạy từ gói `final-v1` (commit xuất `9b22349f`): khác commit và Runner, nên khung không thể giống.
- So bằng `cloud_gates.py` trong gói.
- **Dừng** nếu khác dù chỉ một khung.

**B2. 5 job còn lại của cổng Shadow.**
- Gồm `SHHZ71-N` w17 và 4 job nhánh tách biến: `SHHZ71-CL` và `SHRC71-N`, mỗi nhánh × w08/w17.
- Chạy qua `run_cloud.ps1`, so bằng `cloud_gates.py`.
- Đạt khi cả 4 mặt `allExact`: trường, chuỗi giờ trả, lớp/điểm Shadow, chuỗi giờ công bố.
  - Dự đoán khóa trước là `FULLSYS-F14-TIMED-GATE.sha256` (2 ô cổng × 4 nhánh, 03/11 20:46:38Z).
  - `eta_metrics.py` theo phụ lục sửa sau seal.
- **Dừng** nếu có khách lệch. Hai lần cổng trước đều bắt được lỗi thật: mã nút của Shadow, và phép kiểm giờ đón của
  FleetPy khi có dự báo.

**B3. Đo khi chạy song song.** Chạy một lượt gồm đúng số job dự định chạy song song, với `SHRC71-CL` và `SHHZ71-N`. Ghi
thời gian, RAM đỉnh và đĩa của từng job, rồi chốt số job song song.

**B4. Kiểm trước đủ 168 job (không chạy).**
- Với mỗi job trong danh sách của gói, kiểm ba điều:
  - có tệp cấu hình WP4 và tệp chính sách;
  - sha256 khớp `MANIFEST.tsv`;
  - Runner đọc được cấu hình.
- Đầu vào của từng ô phải có mặt và đúng sha256: mạng đủ nút, `shadowAllocation.networkNodes` (mỗi khung một bộ: 824 hoặc
  830 nút), λ của ô, hồ sơ dự báo.
- Các cổng mới nạp 2 ô; 82 ô còn lại chưa từng được nạp.
- **Dừng** nếu thiếu hoặc lệch dù chỉ một ô.

## C. Đợt chạy cuối: 168 job

- **Nội dung:** 84 ô F14 (14 ngày × 2 khung × 3 lần lấy mẫu), mỗi ô chạy 2 nhánh, có gán lại (G2K8):
  - `SHRC71-CL`: hệ của mình (Shadow + luật C + dự báo);
  - `SHHZ71-N`: đối thủ (Shadow + hàng rào nới, không dự báo).
- **Chạy:**
  - Dùng `run_queue.py`; hàng đợi chạy lại được, job đã đạt thì bỏ qua.
  - Sau mỗi job chạy `finalize_job.py`.
  - Mang bằng chứng về (khoảng 2 GB) kèm sha256.
- **So:** từng khách với dự đoán khóa trước `FULLSYS-F14-TIMED.sha256` (03/11 23:44:51Z). Đạt khi 168/168 cặp, mọi khách
  trùng.
- **Chỉ số:** tính từ bằng chứng của hệ thật bằng `reversal_counts.py` và `eta_metrics.py` (bản có phụ lục, ghi sha256 của
  bản dùng). Đây là bản "hệ thật" của phép so chính, nhưng có ba giới hạn:
  - chỉ trên F14 và ở chế độ adapter;
  - hai nhánh khác nhau cả luật lẫn dự báo, nên bảng chỉ cho hiệu tổng; muốn tách phần của luật C và phần của dự báo vẫn
    phải dùng bảng 2×2 của bộ mô phỏng;
  - bảng này không thay số chế độ plain trong khóa luận (xem D1).
- **Ghi kết quả:** thêm một ADR mới trong `docs/18`, và cập nhật `docs/19`.

## D. Nối kết quả với số trong khóa luận

**D1. GĐ6: khoảng cách plain/adapter cho các nhánh có dự báo.**
- **Lý do:**
  - Số trong khóa luận là chế độ plain; hệ thật chỉ được so với chế độ adapter.
  - Khoảng cách mới đo cho nhánh không dự báo, trên 10 ngày D10 (P5: 60 ô; R1: 20 ô), chưa đo trên F14.
  - Hiệu đảo hứa giữ chiều. Hiệu số khách phục vụ nhạy với chế độ, cỡ 3–4 khách/ngày, và có lúc đổi dấu.
  - Nhánh có dự báo còn khác thêm hai điểm: làm tròn giờ dự báo từng chặng, và `clipPickup` (C# không có).
- **Đã biết:**
  - Dòng plain của 4 nhánh có gán lại trên 84 ô F14 nằm ở `$SP/wf20/fullsys/results/rows.ndjson`. Máy nhà đã chạy lại ngày
    26/10, trùng 1.680/1.680.
  - Dự đoán adapter đã khóa:
    - có cho `SHRC71-CL` và `SHHZ71-N` trên 84 ô;
    - có cho cả 4 nhánh trên 2 ô cổng;
    - **chưa rõ** có cho `SHHZ71-CL` và `SHRC71-N` trên 84 ô hay không. Nếu chưa có thì phải chạy bộ mô phỏng chế độ
      adapter (`engine_wf15_clint.js`) cho 2 nhánh × 84 ô, ghi kế hoạch kèm sha256 trước khi chạy.
- **Ghi trước khi tính:** ngưỡng coi là "đổi chiều", ví dụ như R1: phục vụ ±5 khách/ngày, đảo hứa ±15 điểm phần trăm.
- **So các chỉ số:** khách phục vụ, đảo hứa, khách trễ > 60 s, vi phạm hạn chót, sai số ETA theo thời gian và số lần đảo
  chiều ETA.
- **Khóa luận** ghi cả số plain lẫn số adapter, hoặc nói rõ đang dùng chế độ nào.

**D2. Tái hiện F16 bằng bộ mô phỏng.** Bước này chỉ tái hiện phép tính, không phải kiểm chứng trên hệ thật.
- **Nguồn ô.** Phía công ty cho biết 96 ô nằm trong `cells/f16` của gói ngày 24/10
  `06-Bo-sung/RideBound-wf17-Do-Ben-Day-Du-24-10.zip`. Repo không ghi tệp này.
  - Ghi đường dẫn đầy đủ của zip ở máy nhà.
  - Chạy `unzip -l` để chắc zip có `wf17/r1-identity-fence-pad/cells/f16/{weekdays-P512,matrices,provenance}` và 4 tệp
    `summary-weekdays*.json`.
  - Nếu zip không có ô thì phải có gói P0 (factors.json 2019, các tệp parquet, DuckDB 1.5.5) để chạy `gen17.js`. Khi đó F16
    vẫn bị chặn.
- **Giải nén** sao cho `weekdays-P512/` nằm ngay dưới `$SP/wf17/r1-identity-fence-pad/cells/f16/`, với
  `SP=E:/RideBoundData/company-intake/2026-10-23/sp`. RUNBOOK đọc `F16DIR=$SP/.../cells/f16/weekdays-P512`, và kịch bản trỏ
  tới ma trận bằng `../matrices/`.
- **Kiểm:** `cd "$SP/wf17/r1-identity-fence-pad/cells/f16" && sha256sum -c <bản clone>/research/continuation/2026-10-26/company-emulator/expected/wf17-f16-cells.sha256 | grep -c ': OK$'`
  phải ra 292, và không có dòng FAILED hay báo thiếu tệp.
- **Trước khi chạy:**
  - ghi sha256 của hai tệp `rows.ndjson` hiện chỉ có F14, vì RUNBOOK nối thêm vào chính các tệp đó;
  - sau khi chạy, cập nhật `HOME-RERUN-README.md`.
- **Chạy từng RUNBOOK một:**
  - fullsys: `gate` → `gatecheck` → `f16n` → `f16cl` → `check` → `analyze`;
  - n7: `gate` → `gatecheck` → `n16 cl16 pad16 clc16 padc16` → `analyze`. Bỏ `check` của n7 vì thiếu `calib.ndjson`.
  - `f16cl` và `cl16` dùng chỉ mục dự báo dựng lại A1. Nếu chỉ các dòng CL của F16 lệch thì nghi chỉ mục A1 trước.
- **Đạt:**
  - `confirm_digest.js` báo 3.600/3.600 dòng trùng hoàn toàn, kể cả dấu vân tay từng khách, cho mỗi gói;
  - sau đó chạy `ours_vs_theirs.js` và so với `OURS-VS-THEIRS.md`.

**D3. Tài liệu và khóa luận.**
- Thay mục 9 của báo cáo bằng `MUC-9-HE-THAT-VA-BO-MO-PHONG.md`. Cập nhật bảng kiểm chứng và phần giới hạn sau C, B2 và D1.
- Ghi rõ những gì hệ thật **không** kiểm:
  - F16 (168 job chỉ chạy trên F14);
  - Shadow ở chế độ chỉ chèn;
  - `SHF`;
  - phép đối chứng đệm và CLc của N7;
  - bảng 60 ngày;
  - cỡ đội xe (am2017), độ bền R2–R7, phép hòa vốn;
  - thời gian dừng khác 0 s.
- Khóa luận chỉ dùng Via (chủ quyết ngày 04/11). Cầu nối Manhattan (238 cặp) chỉ là kiểm chứng.

## E. Refactor (sau A–D)

Theo `simplification-audit/KE-HOACH-REFACTOR.md`. Làm trên một nhánh mới tách từ **đỉnh hiện tại** của
`research/fixes-perf-2026-11-04`, không tách từ `ad470c1`: từ `ad470c1` tới đỉnh chỉ có tài liệu và harness.

**E0. Chủ quyết trước** (mục 7 của kế hoạch refactor):
- Có chấp nhận rằng WP2/WP3 (demo), WP6, WP7, WP9 (H6), WP13 và WP14/WP14R chỉ còn tái lập được từ tag không?
- Có đẩy các tag lưu trữ lên remote không?
- Có đổi từ vựng giao thức v1 không (cần ADR)?
- Có lưu trữ hạn chót hai chiều và freeze horizon không?
- Quyết định dwell còn treo.

**E1. Giai đoạn 0, không đổi mã sản phẩm.**
- Gắn tag lưu trữ.
- `dependabot.yml`: thêm `ignore` cho `Google.OrTools*` và `Google.Protobuf`. Phải thêm trên `main`, vì Dependabot chỉ đọc
  cấu hình ở nhánh mặc định; việc đưa vào `main` cần chủ đồng ý.
- Biên bản build (đã làm một phần ở A2).
- Lưới an toàn:
  - N1–N5;
  - R1 (lưới normalizer WP6; khác với R1 độ bền của wf17 ở D1);
  - smoke RidePy;
  - các test "Khác" ở bước 0.6, gồm test decode mọi cấu hình đã track (bước 3.1 cần nó).
  - Mỗi test mới phải đỏ khi cố tình làm hỏng mã.
- Khai báo đúng các test đỏ:
  - `wp13-e1/source-divergence-v1.json` mới khai 1/8 tệp;
  - thêm khai báo cho WP14 và WP9;
  - sửa `StrictBagItBundleTests.cs:32`.

**E2. Giai đoạn 1–8 theo đúng thứ tự.**
- Mỗi bước là một commit, và chạy **đúng các phép kiểm ghi cho bước đó** ở §5 của kế hoạch refactor (K1–K8, N1–N5, R1).
- G1/G2 (K3/K4) không phủ Shadow, dự báo, `light-v1` hay mạng ma trận.
  - Bước nào chạm tới các phần này phải chạy thêm N2–N4 và K5.
  - Bước nào sửa adapter phải chạy K8.
- Harness byte (`research/continuation/2026-11-04/byte-harness/`):
  - chép ra ngoài repo để build;
  - publish hai Runner từ `git archive`: OLD là commit trước bước, NEW là commit của bước;
  - harness không có cấu hình Shadow hay dự báo.
- Script G1/G2 và 228 transcript nằm ở `ridebound-scratchpad/fastrun-2026-11-02/` trên `E:`.
- Thứ tự và phạm vi:
  - chỉ làm các bước 2.x khi kết quả grep ở A3 cho phép;
  - 2.6, 4.3 và 4.4 là tùy chọn;
  - 7.1 chờ chủ quyết;
  - không làm 8.7.
- **Dừng** theo điều kiện riêng của từng bước, ví dụ:
  - byte khác mà chưa khai báo;
  - số test đạt/lỗi khác mốc;
  - test mới không đỏ khi cố tình làm hỏng;
  - RAM tăng;
  - R1 đỏ.

**E3. Sau refactor.** Runner mới khác binary trong gói. Không dùng nó cho số liệu cuối, trừ khi chạy lại mọi cổng (xem 0).

## F. Việc khác

- Không có việc gấp nào phải chen trước đợt chạy cuối. Dependabot chỉ mở PR vào `main`, nên không chạm tới gói.
- Nếu chạy ở nhà: làm rõ chuyện VSS (A4) và dọn `E:` có kiểm soát trước.
- Sau C, B2 và D1: cập nhật mục 9 của báo cáo và `docs/19`.

## Thời gian ước tính

| Việc | Thời gian |
|---|---|
| A | nửa ngày, chưa tính dựng máy thuê (chép và giải nén gói, subst `E:`, cài Python và .NET, kiểm MANIFEST) |
| B | 2–4 giờ (5 job Shadow, 3 job so máy, B3, B4) |
| C | khoảng 5–8 giờ (máy thuê) hoặc 21–30 giờ (máy nhà). Ngoại suy tuyến tính từ 15–21 phút/job đo trên job không Shadow/dự báo; chưa tính `finalize_job.py` và độ chậm khi tranh tài nguyên. Chốt lại sau B3 |
| D1 | 1–2 giờ nếu đủ dữ liệu; thêm thời gian chạy bộ mô phỏng adapter nếu thiếu 2 nhánh tách |
| D2 | khoảng 1 giờ nếu zip có ô; bị chặn nếu không có |
| E | nhiều ngày; làm từng bước nhỏ |
