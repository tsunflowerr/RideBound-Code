# Bàn giao RideBound cho máy công ty — 26/10/2026

Tài liệu này dành cho AI (Claude) và chủ nghiên cứu ở máy công ty. Đọc hết trước khi làm gì. Mọi số ở đây đã được kiểm
trên máy nhà; đường dẫn bắt đầu bằng `E:/` hay `C:/` là của máy nhà, KHÔNG có trên máy công ty.

---

## 0. Lấy code và bắt đầu

```bash
git clone https://github.com/tsunflowerr/RideBound-Code.git   # nếu chưa có
git fetch origin
git checkout research/via-zone-2026-10-26                       # commit mới nhất: e430b7d
```

Nên làm trong một worktree riêng, không sửa thẳng nhánh `main`. Repo `RideBound-Code` đang **công khai**.

Đọc theo thứ tự này:
1. `AGENTS.md` (quy tắc của repo).
2. Tài liệu này.
3. `docs/18-status-and-decision-log.md`:
   - §5 "Next action" (đoạn đầu);
   - ADR-080 tới ADR-085 (tìm "### ADR-08").
4. Bảng đầu của `docs/19-requirement-traceability.md`.
5. `research/continuation/2026-10-26/README.md`: bản đồ các kit script.
6. Theo việc cần làm, các tệp kết quả ở mục 6.

---

## 1. Đề tài trong một đoạn

RideBound là bộ điều phối xe đi chung (C#, .NET 10) chạy cùng bộ mô phỏng FleetPy qua một lớp nối Python. Khóa luận
nghiên cứu **luật C**:
- Mỗi khách có một sổ ghi tổng độ lớn các lần **điều phối** dời giờ trả: Σ|c| ≤ B, với c = p − e.
  - p là giờ trả mới công bố;
  - e là giờ trả tính lại nếu giữ nguyên kế hoạch cũ.
- Tắc đường không bị tính vào sổ. Sổ tính cả hai chiều (sớm và muộn) và không hoàn lại.

Đối thủ là **Elmi Ordinary** (bài Elmi và cộng sự 2026), với ngân sách B_r = round(α·độ dư), α = 35%:
- **EO35, hàng rào chặt:** ép xe giữ lộ trình.
- **EOHZ35, hàng rào nới:** p ≤ max(p0 + B_r, e). Đây là **đối thủ chính** theo quyết định của chủ ngày 25/10; luôn báo
  kèm EO35.
- **RCE35:** luật C với cùng B_r.

Hai chế độ điều phối:
- **INS:** chỉ chèn khách mới.
- **G2:** mỗi quyết định được dời tối đa một khách đang chờ sang 1 trong 8 xe gần nhất, giờ đón không muộn hơn.

**Shadow** (cũng của bài Elmi): khách khó đi chung được bảo đảm tuyệt đối (ngân sách 0). Có ba cách xử lý ngân sách
đi cùng:
- SHF: hàng rào chặt;
- SHHZ: hàng rào nới;
- SHRC: luật C.

**Ranh giới lời khẳng định** (`docs/03-related-work-and-claim-boundary.md`): không được nói là mới về chèn động, giới
hạn ETA, gán lại, ổn định lộ trình, cam kết tối thiểu, nhất quán thời gian, hay sự hài lòng của khách.

---

## 2. Bằng chứng đã có và mức tin cậy

### 2.1 Số Via 60 ngày: từ bộ mô phỏng của công ty (thăm dò)

- Bộ mô phỏng JS wf15 của công ty viết lại logic điều phối của RideBound, cộng Elmi, Shadow và dự báo vòng kín.
- Hai đợt chạy trên **cùng 60 ngày** Via (TLC HVFHV 2019, HV0004, chuyến nội Manhattan):
  - đợt 1: wf14;
  - đợt 2: wf15.
- Ô P512: 512 yêu cầu trong 2 giờ, 62 xe 4 chỗ, trần thời gian ngồi xe 2,0× đi thẳng, thời gian dừng đón/trả 0, hai ca
  w08/w17, 3 lần lấy mẫu mỗi ca.
- Nhãn **thăm dò**: giả thuyết đặt sau khi chạy thử, do AI đặt, không có dấu thời gian bên ngoài.
- Kết quả chính, trung bình mỗi ngày (`docs/18` ADR-084; bảng 60 ngày tính lại độc lập trùng kiểm toán):
  - **G2, RCE35 − EOHZ35:** phục vụ −2,82 [−4,38; −1,27]; khách bị đảo hứa −16,68 (khoảng −42%, cả 60/60 ngày).
  - **INS:** hai luật gần như bằng nhau. Khi chỉ chèn thì c ≥ 0, nên phần "hai chiều" của luật C không có việc gì để
    làm (kiểm đồng nhất RC = NC = OC ở 11/11 ô).
- **Thước đo của Elmi** (đến muộn hơn p0 + B_r + 60 s): luật C để 1,78% / 1,75% khách muộn (INS / G2), EO35 để
  1,11% / 1,02%. Ngược lại, theo sổ của luật C, EO35 vượt ngân sách ở 1,18% / 4,17% khách. Mỗi luật thắng trên thước
  đo của chính nó. Nguồn: bản nháp khóa luận `08c-via-elmi.tex:98-122`, chỉ có ở máy nhà, xem mục 5.
- **Shadow:**
  - SHRC − SHHZ phục vụ −4,0 (INS) và −9,5 (G2) khách mỗi ngày; G2 bớt 16,6 lần đảo hứa.
  - Khách được bảo đảm vẫn đến muộn quá 60 s vì tắc đường: 4,34% / 4,22% (SHRC), 3,55% (SHF).
  - Shadow **chỉ có trong bộ mô phỏng**; RideBound không có dòng code Shadow nào.

### 2.2 Các phép thăm dò sau kiểm toán, 25/10 (ADR-084)

- **P5:** chạy lại 10 ngày ở máy nhà; 840/840 dòng trùng công ty. Đổi sang chế độ định danh adapter thì các phép so chính
  vẫn giữ dấu.
- **P7:** dữ liệu Via khớp tệp gốc TLC tháng 3 và 9/2019, 0/0 dòng lệch (66.856 và 51.219 dòng). `rid` của công ty xếp
  theo (giờ yêu cầu, khu đón, khu trả, quãng đường, thời gian).
- **N1:** dung sai hàng rào 1 s xóa 33–54% khoảng cách phục vụ SHRC − SHF.
- **N2:** với G2, luật C bớt 33–55% đảo hứa ở mọi α từ 10% tới 100%, đổi lại phục vụ ít hơn 0,2–1,2%.

### 2.3 Cầu nối: RideBound thật so với bộ mô phỏng

- **Cỡ nhỏ** (dữ liệu Manhattan FleetPy 2018, 108–128 yêu cầu, 8–10 xe): **238 cặp kịch bản–luật trùng từng khách**.
  Bộ giải: 232.103 quyết định, 100% tối ưu, chưa chạm trần.
- **Cỡ Via, mới làm 26/10 (ADR-085):**
  - Dữ liệu: ô Via ngày 13/2/2019, mỗi điểm được gộp về một điểm đại diện của khu TLC; 509/508 yêu cầu, 62 xe, 60/58
    điểm.
  - Kết quả: **6/6 cặp trùng hoàn toàn, 2.407/2.407 khách**; số lần G2 dời khách trùng 4/4. Phủ được:
    - EOHZ35 chỉ chèn, cả hai ca;
    - EOHZ35 có G2, cả hai ca;
    - **luật C có G2, ca chiều (430 khách)**;
    - U có G2, ca sáng.
  - **Chưa phủ:** luật C chỉ chèn. Hai job này cùng RCE35 G2 ca sáng và U chỉ chèn ca sáng đều chạy hết mọi quyết định,
    nhưng runner báo `OutOfMemoryException` khi dựng bản chốt cuối (một dòng vài trăm MB), dù trần heap là 6 GiB.
  - Hai job U ca chiều không chạy vì đường ray ổ đĩa (ổ C dưới 30 GiB).
- Ý nghĩa: số của bộ mô phỏng đáng tin ở cỡ Via cho EOHZ35, G2 và luật C có G2; luật C chỉ chèn chưa kiểm được.

### 2.4 Phép hòa vốn, 26/10 (`kits/breakeven-2026-10-26/BREAKEVEN-RESULT.md`)

- G2, hai luật phục vụ cùng số khách, so với EOHZ35. Mỗi ngày luật C:
  - bớt 13,5 khách bị đảo hứa (36,4 → 22,9);
  - thêm 12,7 khách bị điều phối dời muộn quá 1 phút (188,1 → 200,8).
- **w\* = 0,94** (bootstrap theo ngày 0,46–1,61). Luật C chỉ đáng dùng nếu khách thấy một lần bị đảo hứa khó chịu ít
  nhất bằng 0,94 lần bị dời muộn quá 1 phút. Khoảng tin cậy chứa 1, nên **chưa phân thắng thua**.
- Khi chỉ chèn: không phân biệt được hai luật. Phần "muộn" do điều phối, không do tắc đường.
- Kế hoạch được khóa trước khi tính; cổng: chạy lại 2.520 lượt trùng tuyệt đối P5/N2.

### 2.5 Kết luận trung thực hiện tại

**Luật C là một đánh đổi có điều kiện, không phải thắng.**
- Nó chỉ có tác dụng khi có gán lại xe: bớt nhiều đảo hứa, đổi lại phục vụ ít hơn chút và nhiều khách bị dời muộn hơn.
- Chưa ai đo cảm giác của khách. Muốn biết thì cần khảo sát hoặc thử nghiệm.

---

## 3. Mã C# và adapter trên nhánh này

Nhánh `research/via-zone-2026-10-26` = `main` (41a4e11) + các nhánh nghiên cứu. Ba commit cuối chỉ thêm tài liệu và
script.

| Commit | Nội dung | ADR |
|---|---|---|
| 3afcd17, 176f5dd | Trần số lần sửa ETA trả mỗi quyết định; hạn chót nới | 080 |
| 9b633d9, 2eadfb7 | Băm trạng thái một lần mỗi epoch có cache (A0.1, nhanh hơn khoảng 13×, phát lại giống từng byte) | 081 |
| 18978dd, 1190518 | Ngân sách theo độ dư kiểu Elmi (`dropEtaBudgetSlackPermille`), hạn chót theo độ dư (`dropEtaDeadlineSlackPermille` + `dropEtaDeadlineRelaxed`) | 081 |
| c6588f8, ef2b02f | G2: `waitingRiderRelocation`, `maximumTargetVehicles`; sửa adapter chặng 0 giây | 081 |
| 6ab03fc | Sửa adapter: chặng dừng 0 giây bị khóa | 082 |
| 9ace339 | `stopServiceDurationMs`, mặc định tắt (dwell khác 0 bị chặn: lõi thiếu trạng thái "xe bận tới T") | 083 |
| 8ce0194 | Tài liệu: P5, P7, N1, N2, cầu nối 238 | 084 |
| 2704600, 00ac6b4, e430b7d | Kit tháng 10, chạy Via thật, hòa vốn | 085 |

- Kiểm thử: `dotnet test RideBound.slnx` đạt 1230/1230 trên p1-dwell (ADR-083). Trên cây sạch, `StrictBagItBundleTests`
  có thể hỏng vì nó đòi worktree "bẩn"; đây không phải lỗi mã.
- Cấu hình luật dùng cho Via: `kits/via-bridge-2026-10-22/configs/` (U, RCE35, EOHZ35, G2-*).
- WP4 cho Via (ngân sách nâng): `kits/via-real-2026-10-26/configs/wp4-via-*-a1.json`.

---

## 4. Không có trong repo, và lấy ở đâu

| Thứ | Ở đâu |
|---|---|
| Bộ mô phỏng wf15 (`sp/`, engine.js, ...) và các ô Via P512 | Máy công ty (bản gốc); máy nhà có bản chép ở `E:/RideBoundData/research/p5-via-adapter-v1/sp` |
| Dữ liệu TLC HVFHV 2019 | Tải từ trang TLC; dựng lại bằng `kits/p7-tlc-2026-10-25/` (DuckDB) |
| Mạng FleetPy Manhattan | Zenodo 15187906 |
| Nhật ký job (transcript, mỗi job 1–2 GB), thư mục `runs/` | Chỉ ở máy nhà, `E:/RideBoundData/research/...` |
| Bản sửa khóa luận | Repo `Report_INT3508`, nhánh `thesis/audit-fixes-2026-10-25` (mục 5) |
| Tóm tắt tài liệu công ty (`CON-THIEU-SO-VOI-KE-HOACH-CONG-TY.md`, `TIEP-TUC-PHIEN-MOI-2026-10-24.md`) | Chỉ ở máy nhà; cố ý không đưa lên repo công khai vì có e-mail công ty và thông tin nội bộ. Máy công ty đã có tài liệu gốc |

---

## 5. Khóa luận

- Repo khóa luận: `Report_INT3508` (GitHub, công khai). Lấy về:

  ```bash
  git clone https://github.com/tsunflowerr/Report_INT3508.git   # nếu chưa có
  git fetch origin
  git checkout thesis/audit-fixes-2026-10-25                    # commit f1a0c73
  ```

  Khóa luận LaTeX nằm ở `ridebound/` (`main.tex`; các chương ở `ridebound/noi-dung/`). Ở gốc repo là báo cáo thực tập,
  một tài liệu khác: không sửa.
- Bản sửa sau kiểm toán ở **nhánh `thesis/audit-fixes-2026-10-25`** (đã push 26/10; worktree máy nhà
  `C:/Code/Report_INT3508-audit`), tới commit `f1a0c73`, 85 trang, build sạch. Nội dung:
  - sửa lỗi sự thật;
  - mục mới `noi-dung/08c-via-elmi.tex` (so với Elmi, cầu nối);
  - tóm tắt, kết luận, N1/N2, bảng "theo định nghĩa / đo được", trích dẫn bằng sáng chế Via.
- Nhánh này chưa trộn vào `main` của repo khóa luận; chủ duyệt rồi mới trộn.
- **Chưa đưa vào khóa luận:** kết quả cầu nối cỡ Via (6/6, 2.407 khách) và phép hòa vốn (w\* = 0,94). Câu đề xuất ở
  docs/18 ADR-085, phần Consequences.
- Quy tắc: **không nhắc tới AI** trong khóa luận. Câu kiểm bằng tác tử phải viết là "kiểm lại bằng chương trình đọc
  riêng". Việc có khai báo dùng AI hay không do chủ quyết, vẫn đang chờ.

---

## 6. Tệp nên đọc theo việc

Đường dẫn tính từ `research/continuation/2026-10-26/kits/`:

| Việc | Tệp |
|---|---|
| Chạy Via thật (toàn bộ diễn biến, sửa đổi A1–A3, rà soát R1) | `via-real-2026-10-26/VIA-LOG.md`; kết quả `compare/runA3-final-compare.json` |
| Phép hòa vốn | `breakeven-2026-10-26/BREAKEVEN-RESULT.md`, `BREAKEVEN-PLAN.md`, `BREAKEVEN-TABLE-v2.tsv` |
| P5, N1, N2 | `p5-via-adapter-2026-10-25/P5-RESULT.md`, `N1-RESULT.md`, `N2-RESULT.md`, `TABLES-2x3-60days.txt` |
| Đối chiếu TLC | `p7-tlc-2026-10-25/P7-RESULT.md` |
| Cầu nối giai đoạn 1 | `bridge-phase1-2026-10-23/PHASE1-LOG.md`, `SOLVER-EVIDENCE-RESULT.md` |
| Cầu nối Manhattan ban đầu | `via-bridge-2026-10-22/BRIDGE-REPORT.md`, `AMENDMENTS.md` |
| Danh sách tệp đã chép, sha256 | `COPY-MANIFEST.tsv`, `COPY-UPDATE-*.tsv` |

Ghi chú của máy nhà, ở `research/continuation/2026-10-26/ghi-chu-may-nha/` (bản chép, tiếng Việt):

| Tệp | Nội dung |
|---|---|
| `TIEN-DO-2026-10-26.md` | Diễn biến đêm 26/10: hòa vốn, chạy Via thật, ba lần push |
| `TIEN-DO-THUC-HIEN.md` | Diễn biến 25/10: P5, P7, N1, N2, viết lại tác giả commit, sửa khóa luận |
| `VIEC-BAN-TU-LAM.md` | Việc chỉ chủ làm được: thứ tự duyệt commit (P4), các số tự kiểm (§8.2), họp thầy, đính chính slide 20/10. Bản chép bỏ mục 5 (lấy tệp từ máy công ty) |
| `MAU-DANG-KY-DOT-3.md` | Mẫu đăng ký trước cho đợt xác nhận 3 |
| `DINH-CHINH-KE-HOACH-MAY-NHA-23-10.md` | 14 điểm đính chính kế hoạch máy nhà 23/10 |

---

## 7. Chạy lại ở máy công ty: những điều phải biết

- Script ghi **đường dẫn tuyệt đối của máy nhà** ở đầu tệp. Sửa hằng số đường dẫn trước khi chạy.
- Môi trường cần có:
  - Python của môi trường FleetPy 1.0.2 (dùng `-B` cho driver, `-I` cho script đọc dữ liệu ngoài);
  - Node (v24 trên máy nhà);
  - .NET SDK 10;
  - runner build Release từ `src/RideBound.Runner`.
- **Job RideBound thật ở cỡ Via rất nặng:** mỗi job 1,5–2 giờ, RAM 5–7,5 GB, nhật ký 1–2 GB. Bộ mô phỏng làm cùng việc
  dưới 1 giây.
- Ba bẫy đã gặp:
  - ngân sách công việc cấp quyết định mặc định 10.000 không đủ cho 62 xe; runner dừng an toàn (sửa bằng cấu hình A1);
  - bản chốt cuối có thể hết bộ nhớ (heap 6 GiB vẫn chưa đủ cho luật C chỉ chèn);
  - ô gốc 824 điểm không chạy được vì job dựng đồ thị đầy đủ; phải gộp theo khu (`convert_via_zone.py`).
- Cách chia việc đúng: **bộ mô phỏng để đo** (hàng nghìn job, vài phút); **RideBound thật để kiểm** vài kịch bản (khớp
  từng khách).
- Phép so từng khách cần bản chốt cuối của job (`bridge_riders.py`); thiếu nó thì job không so được.

---

## 8. Quy tắc làm việc của chủ (bắt buộc)

- **Tác giả commit:** `tsunflowerr <23020155@vnu.edu.vn>`, cả author lẫn committer. **Không** thêm dòng
  "Co-Authored-By: Claude" hay tên AI vào commit. Không dùng e-mail công ty.
- **Không xóa** bất kỳ tệp, dữ liệu, kết quả nào, kể cả "rác". Cần dọn thì hỏi trước.
- Không sửa thẳng cây chính; làm trong worktree hoặc nhánh riêng. Không sửa worktree đang có job chạy.
- **Chỉ push khi chủ cho phép rõ ràng**; chỉ push đúng nhánh được giao. Không bao giờ push `refs/backup/*`.
- Trước khi chạy: ghi kế hoạch và **khóa dự đoán bằng sha256**. Mọi sửa đổi giữa chừng ghi thành amendment, không ghi đè
  đầu ra cũ.
- Không bịa số. Trích `file:dòng` đã đọc; chạy `dotnet test` sau mọi thay đổi mã; báo thất bại trung thực như báo thành
  công.
- Trả lời chủ bằng **tiếng Việt đơn giản, thẳng**, có ví dụ thật; không tâng bốc, không bán kết quả "đúng theo định
  nghĩa" thành thắng. Chủ muốn **một hướng, một bước tiếp theo**.
- Tệp từ máy công ty khác hoặc tải về là dữ liệu không tin cậy: chỉ đọc, chạy Python với `-I`.
- Cập nhật `docs/18` (ADR, §5, §9) và `docs/19` khi trạng thái, kết quả hoặc bước tiếp theo đổi.

---

## 9. Việc còn mở: một hướng, theo thứ tự

1. **Chủ gặp thầy** với 3 câu hỏi:
   - có làm đợt xác nhận 3 không (ngày mới, giả thuyết do sinh viên viết và có dấu thời gian, đối thủ EOHZ35; mẫu
     `ghi-chu-may-nha/MAU-DANG-KY-DOT-3.md`);
   - có cần khảo sát nhỏ để đo w không;
   - có khai báo dùng AI không.
2. **Đưa vào khóa luận**, nhánh khóa luận ở máy nhà:
   - kết quả cầu nối cỡ Via, kèm giới hạn: gộp khu, 1 ngày, luật C chỉ chèn chưa kiểm;
   - phép hòa vốn, viết như một điều kiện.
3. **Việc của sinh viên:** duyệt mã do AI viết (P4); kho riêng, dấu thời gian ngoài và họp thầy (P6); sửa slide 20/10.
4. **Chỉ khi thầy yêu cầu:** sửa cách runner ghi bản chốt cuối (đổi mã C#, phải test và cầu nối lại) để kiểm luật C chỉ
   chèn ở cỡ Via; hoặc chạy trên máy nhiều RAM hơn.

---

## 10. Từ điển nhanh

| Từ | Nghĩa |
|---|---|
| p0 | Giờ trả hứa đầu tiên (chưa cộng dự báo) |
| c | Phần dời giờ trả do điều phối của một lần công bố, c = p − e |
| độ dư, B_r | Độ dư S_r = pk0 + maxRide − p0 (pk0: giờ đón hứa ban đầu; maxRide: thời gian ngồi xe tối đa); B_r = round(α·S_r) |
| đảo hứa (visRev60) | Giờ khách thấy đổi lên rồi xuống, mỗi lần ít nhất 60 s (đếm theo khách) |
| late60First | Đến muộn hơn p0 quá 60 s (mọi nguyên nhân) |
| Ld (hòa vốn) | Tổng có dấu các lần điều phối dời giờ trả của khách > +60 s |
| pDisp | Khách bị điều phối dời muộn ít nhất 5 s |
| w\* | Mức khó chịu của một lần đảo hứa, tính theo "một lần bị dời muộn > 1 phút", mà ở đó hai luật hòa |
| P512, w08/w17 | Ô 512 yêu cầu / 2 giờ; ca 8h và 17h |
| plain / adapter | Chế độ định danh của bộ mô phỏng: số thứ tự đơn giản (như công ty) / như RideBound (half-even) |
| cầu nối, cặp kịch bản–luật | Chạy RideBound thật và bộ mô phỏng trên cùng kịch bản, so từng khách; một cặp = một ô × một luật |
| transcript, checkpoint | Nhật ký mọi tin nhắn giữa adapter và runner; bản chốt trạng thái cuối job (cần cho phép so) |
| vz | Họ kịch bản Via gộp điểm theo khu TLC (`convert_via_zone.py`) |
