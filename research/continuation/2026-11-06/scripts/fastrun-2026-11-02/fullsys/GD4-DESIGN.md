# GĐ4 — Shadow và dự báo vòng kín trong RideBound (ADR-089, thiết kế 03/11/2026)

Đích: hai nhánh của phép so cuối chạy trên hệ thật, khớp từng khách với dự đoán chế độ adapter đã khóa
(`emulate_fullsys.js adapter`, engine `engine_wf15_clint.js`).

| Nhánh | Bộ mô phỏng | RideBound |
|---|---|---|
| Hệ của mình | G2 SHRC71 + CL | G2 k8, luật C với ngân sách riêng từng khách `bSH`, dự báo bật |
| Elmi | G2 SHHZ71, N | G2 k8, hàng rào nới với ngân sách riêng `bSH`, không dự báo |

`bSH = round(aSH × slk)`, `slk = max(0, pk0 + maxRide − p0)`, `aSH = 0` nếu điểm `sS ≤ θ`, ngược lại `1`
(engine17.js:38-39, 86-89, 737, 771-776). Với `aSH = 1`, `bSH` = `CommitmentPolicy.SlackProportionalLimit(…, 1000)`
(round(1.0 × slack), cùng phép tính double).

## 1. Dự báo (CL)

- Engine dùng `legF` ở đúng 3 chỗ: phần còn lại của đoạn đang đi (`startOf`, engine17.js:250), từng chặng khi chiếu
  lộ trình (`project`, :263) và khi đánh giá ứng viên (`evalCand`, :285). Chặng cùng nút: không gọi `legF`.
- `fb0` = bin 15 phút của thời điểm ra quyết định, `fl0 = prof[min(fb0, H)]` (:454). Trọng số bin `b`: 1 nếu `b = fb0`,
  ngược lại `prof[min(b, H)] / fl0` (:161).
- Quy ước adapter (khóa trước, `engine_wf15_clint.js`): mỗi chặng làm tròn ra mili-giây nguyên bằng `Math.round`
  (chia đôi làm tròn lên; thời gian không âm). C#: `Math.Round(x, MidpointRounding.AwayFromZero)`, cùng giá trị với số
  không âm.
- C#: thêm vào `ITravelTimeLookup` phương thức mặc định `Arrive(departure, travelTime) = departure + travelTime`.
  Thay ở `RouteScheduleProjector` (cạnh đang đi :81-89, chặng :123) và `PhysicalPlanValidator` (cạnh đang đi :314, chặng
  :348). Thời gian phục vụ (:394) vẫn là phép cộng. Lớp `ForecastTravelTimes` bọc ảnh chụp thời gian đi, chỉ dùng trong
  ngữ cảnh ra quyết định, tạo theo từng quyết định với thời điểm đánh giá; không đụng vào phần mô phỏng vật lý.
- Hồ sơ `prof` (16 số) theo từng ô, đọc từ tệp đầu vào của ô. `clipPickup`: chỉ đổi giờ đón hiển thị (:721-740), không đổi
  quyết định. Cần kiểm xem RideBound có dùng giờ đón đã công bố cho quyết định nào không trước khi áp.
- Cổng: chế độ tắt dự báo phải giống từng byte (phát lại G1/G2 một phần); chế độ bật: so từng khách với dự đoán đã khóa.

## 2. Shadow

- Đầu vào theo ô (`shadow-inputs/<cell>.json`, sinh cùng dự đoán): `kappa`, `factors`, `globalOf`, prior O/D theo khu,
  `phi`, `sampled`, `alphaS 0.15`, `strideMs 0`, `omegaP 600000`, `θ`, `zoneIds`; mạng đầy đủ dùng chung
  (`shadow-gate/manhattan-network`: D.u16 và network.json, 4.411 nút, 63 khu).
- λ trực tuyến: đếm yêu cầu theo khoảng 10 phút và khu khi yêu cầu đến (kể cả bị từ chối); trọng số nhân quả của khoảng
  `i` chỉ dùng quan sát của các khoảng trước `i` (lambda.js). `ZoneDemandRates` đã có, cần bản cập nhật dần.
- Lớp khách: tính ngay sau khi lời hứa đầu được công bố, cho mọi khách mở lời hứa ở quyết định đó, theo thứ tự `toOpen`
  (engine17.js:743-776). Ngữ cảnh: điểm xuất phát của xe (`startOf`), cung đang đi (`pm`), tải, sức chứa, các điểm dừng
  với giờ dự kiến đã công bố và khoảng dư `h`:
  - đón: `h = 0` nếu khách đã có lời hứa, ngược lại `max(0, latest − T)`;
  - trả: `phys = max(0, pt + maxRide − T)` (`pt` = giờ đón thật nếu đã lên xe, ngược lại giờ đón dự kiến);
    `h = phys` nếu khách chưa có lời hứa hoặc vừa mở trong quyết định này, ngược lại
    `min(phys, max(0, bSH_j − Labs_j))` (DC-1; `Labs` = tổng |c| đã ghi sổ).
- Lưu `aSH` (hoặc `bSH`) vào lịch sử cam kết của khách, cố định từ lời hứa đầu; vào bản chốt; chỉ có khi bật Shadow
  (tắt thì trạng thái giống từng byte).
- Chính sách: SHRC = giới hạn `drop_eta_total_ms` của khách là `bSH`; SHHZ = hạn chót nới `p ≤ max(p0 + bSH, e)`.
- Cổng: điểm `sS` từng khách bằng `sS` trong dự đoán (đã có bộ chấm điểm khớp từng bit); lớp và ngân sách bằng nhau; rồi so
  từng khách như G3b.

## 2b. Shadow — chỗ cắm cụ thể trong mã (đọc 03/11 23:15)

- Chính sách: thêm cờ `dropEtaShadowAllocation` (CommitmentPolicy + đọc cấu hình), đi kèm `dropEtaBudgetSlackPermille:
  1000` (SHRC) hoặc `dropEtaDeadlineSlackPermille: 1000` + `dropEtaDeadlineRelaxed: true` (SHHZ). Lớp 0 → giới hạn 0;
  lớp 1 → `SlackProportionalLimit(…, 1000)`.
- Đọc ngân sách: `CommitmentDecisionValidator.cs:405` (`DropEtaTotalLimitFor`) và `CommitmentLockEvaluator.cs:215`
  (`DeadlineSlackFor`) → thêm tham số lớp của khách, lấy từ sổ.
- Sổ: `CommitmentLedger` thêm bảng `ShadowAllocations` (RequestId → lớp, điểm). Ghi ở 4 đường, chỉ khi không rỗng:
  `OnlineStateCanonicalWriter`, `OnlineStateCanonicalizer`, `OnlineStateHashV2`, `OnlineStateCheckpointCodec`.
- Gán lớp: `RunnerSession.cs` ngay sau `stateToStage = validation.ValidatedState!` (và sau breach bridge), trước
  `HashStagedState`. Khách mở lời hứa đầu = các publication có entry kiểu InitialPromise. Ngữ cảnh điểm lấy từ
  `stateToStage`: xe được gán, `RouteScheduleProjector.Project(run, xe, route, travelTimes, tEval)` cho `T` (giờ bắt đầu
  phục vụ), điểm xuất phát như bộ chiếu (cạnh đang đi: `from`, `permille` 1..999 → arc). Nút: `node-%06d` = chỉ số nút
  toàn mạng → `GlobalOf` đồng nhất (identity).
- λ: lập lại từ các yêu cầu đã biết (mọi vòng đời, kể cả bị từ chối) ở mỗi lần chấm; trọng số nhân quả của khoảng `i` chỉ
  dùng các khoảng trước `i`, nên giống bản dùng toàn bộ yêu cầu của bộ mô phỏng.
- `Labs_j` = tổng |thành phần drop của `Deltas.DecisionInduced`| trên các mục sổ của khách j.
- Cấu hình WP4 `shadowAllocation`: đường dẫn mạng + sha256, `kappa`, `factors` (chuỗi), `omegaPMs`, `strideMs`,
  `intervalMs`, `alphaS` (chuỗi), `phi` (chuỗi), `sampled`, `prior.origin/destination` (số nguyên theo thứ tự khu),
  `theta` (chuỗi). Lấy từ `shadow-inputs/<cell>.json` của bước khóa dự đoán.

## 2c. Chỉ số chính phải được so trực tiếp (thêm 04/11)

- Kết quả chính của báo cáo là đảo hứa (visRev60) và TV5, không nằm trong các trường so từng khách cũ (t0, p0, A, served,
  Σ|c|, số c≠0, tổng c, max|c|, shared). Cả hai chỉ phụ thuộc chuỗi giờ trả đã công bố của khách được phục vụ
  (`metrics_sh.js:47-60`).
- Bộ mô phỏng: `emulate_sequences.js` (chạy lại 168 cặp bằng `emulate_fullsys.js` đã khóa, kiểm mọi trường cũ trùng tệp
  khóa, xuất chuỗi giờ trả, rev60, tv5) → `fullsys-f14-drop-sequences.tsv`, khóa trước mọi job.
- Hệ thật: `compare_drop_sequences.py` (giờ trả `p` của các dòng drop trong bảng sổ, gộp lặp liền nhau) → so chuỗi từng
  khách và tổng visRev60/TV5 mỗi cặp ô–nhánh.

## 3. Thứ tự

1. Khóa dự đoán 84 ô × 2 nhánh + tệp đầu vào Shadow/dự báo (sha256).
2. Dự báo trong C#: giao diện, lớp bọc, cấu hình WP4, test (tắt = byte cũ; bật = khớp số học với `legF` đã làm tròn).
3. Shadow trong C#: tệp đầu vào, λ trực tuyến, ngữ cảnh điểm, lớp khách trong sổ cam kết, SHRC/SHHZ, test.
4. Adapter/launcher: truyền tệp đầu vào theo ô; chạy thử 2 ô ở nhà, so từng khách; rồi 168 job.
