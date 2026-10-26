# Hướng dẫn máy nhà tự chạy lại và tự xác nhận (gói `company-emulator`, 26/10/2026)

Gói này thay cho việc chép cả thư mục 2,5 GB từ máy công ty. Gói chỉ có **mã, kế hoạch, báo cáo và bảng dấu vân tay kết quả**; không có ô kịch bản, dump hay tệp dòng kết quả.

Máy nhà dùng dữ liệu đã có từ bản chép ngày 23/10 (`E:/RideBoundData/company-intake/2026-10-23/sp`, gọi tắt là **SP**) để chạy lại, rồi so với dấu vân tay. Bộ mô phỏng là tất định, nên chạy đúng thì kết quả phải **trùng từng khách**.

Mọi kết quả ở đây là **thăm dò sau kiểm toán**:
- chạy trên ngày đã xem;
- giả thuyết do AI viết;
- chỉ chạy trong bộ mô phỏng.

Khóa luận chỉ cần **một đoạn giới hạn** nói điều này; không dùng các chữ "xác nhận" hay "chứng minh".

---

## 0. Gói có gì

| Thư mục | Nội dung |
|---|---|
| `sp/wf17/` | Độ bền R1–R7 (ngày 23–24/10): mã, kế hoạch `a0-plan/RUNPLAN.md`, ghi chú, bảng kết quả dạng `.txt`. Gồm cả mã sinh ô F16 (`r1-identity-fence-pad/code/scripts/gen17.js`, `work/f16/`) |
| `sp/wf20/fullsys/` | **Hệ đầy đủ:** Shadow + luật C + dự báo vòng kín. Gồm mã, `PLAN.md`, `RUNBOOK.sh`, `REPORT-VI.md`, prior λ của F16 |
| `sp/wf20/am2017/` | 7 cỡ đội xe theo mốc Alonso-Mora 2017 và Fielbaum & Alonso-Mora 2020 |
| `sp/wf20/ours-vs-theirs/` | Phép so "hệ của mình (SHRC + dự báo) − đối thủ (SHHZ, không dự báo)", có tách phần |
| `sp/wf20/verify-fullsys/` | Script tính lại độc lập cho hệ đầy đủ |
| `sp/wf21/n7/` | **N7:** dự báo so với đệm cùng độ dài đi qua điều phối; chỉ mục nhân quả. Gồm mã, `PLAN.md`, `RUNBOOK.sh`, `REPORT-VI.md` (có mục sửa sau kiểm tra), r đã hiệu chỉnh (`calib/*_final.json`), chỉ mục theo ngày đích (`cidx/results/*.json`) |
| `sp/wf21/verify-n7/` | Script kiểm độc lập N7 |
| `expected/` | Dấu vân tay từng dòng kết quả của máy công ty: khóa, chỉ số chính, `sig` = dấu vân tay từng khách. Kèm sha256 của 96 ô F16 |
| `tools/` | `check_inputs.js` (kiểm đầu vào), `confirm_digest.js` (so kết quả với dấu vân tay) |
| `BAO-CAO-KET-QUA-26-10.md` | Báo cáo gộp: hệ đầy đủ, so với đối thủ, mốc Alonso-Mora, tốc độ RideBound, N7 |
| `24-CHO-SUA-KHOA-LUAN-26-10.md` | 24 chỗ sửa khóa luận; **chủ tự viết lời** |

Mọi đường dẫn trong mã đã trỏ sẵn về `E:/RideBoundData/company-intake/2026-10-23/sp`. Nếu gốc ở nhà khác, sửa chuỗi này **trong bản chép** (mục 1) và ghi vào nhật ký.

---

## 1. Chuẩn bị (15 phút)

1. **Node:** máy công ty dùng Node v20.20.2. Máy khác phiên bản thì vẫn chạy, nhưng nếu kết quả lệch thì kiểm phiên bản Node trước tiên.
2. **Chép mã vào SP:** chép `company-emulator/sp/wf17`, `sp/wf20`, `sp/wf21` vào `E:/RideBoundData/company-intake/2026-10-23/sp/`, giữ nguyên cấu trúc. Các thư mục này chưa có ở nhà, nên không ghi đè gì.
3. **Kiểm đầu vào:** chạy `node tools/check_inputs.js E:/RideBoundData/company-intake/2026-10-23/sp`. Phải ra 19/19.
   - Nếu thiếu `tools/duckdb-v1.5.5-sd/duckdb.exe`, giải nén zip DuckDB có trong P0.
   - Nếu thiếu tệp khác, xem cột "gói" trong kết quả của lệnh để biết lấy ở đâu.
4. **Ghi kế hoạch trước:** tạo `SP/wf22-home/PLAN.md` ghi sẽ chạy lại những gì và tiêu chí "trùng", rồi ghi sha256 của tệp. Tiêu chí trùng là `confirm_digest.js` báo đủ N/N dòng trùng hoàn toàn.

**Lệnh chạy:** dùng Git Bash, `bash <đường dẫn>/RUNBOOK.sh <bước>`. Mỗi RUNBOOK tự `cd` vào thư mục mã và chạy tối đa 4 tiến trình. Chạy mỗi lúc một RUNBOOK.

---

## 2. Thứ tự chạy

### Bước 1. Sinh lại 96 ô F16 (cần cho phần F16 của bước 2 và 3)

Muốn làm nhanh thì **bỏ qua bước này** và chỉ chạy phần F14 ở bước 2 và 3 (ô F14 đã có trong P4). Khi đó so bằng `--panel F14`.

1. **Xuất chuyến Via của 16 ngày F16:**
   ```bash
   SP=E:/RideBoundData/company-intake/2026-10-23/sp
   "$SP/tools/duckdb-v1.5.5-sd/duckdb.exe" < "$SP/wf17/r1-identity-fence-pad/work/f16/export_windows.sql"
   ```
   Kết quả: `wf17/r1-identity-fence-pad/work/f16/via_windows.csv`.
   - **Cổng G-EXP:** số yêu cầu mỗi ngày-khung phải bằng các cột `w08`/`w17` trong `wf12/r5-data-days/work/day_status.json`.
2. **Sinh ô, chạy 3 lần với k = 0, 1, 2:**
   ```bash
   cd "$SP/wf17/r1-identity-fence-pad/code/scripts"
   node gen17.js --days "$SP/wf17/r1-identity-fence-pad/work/f16/days.json" \
     --windows "$SP/wf17/r1-identity-fence-pad/work/f16/via_windows.csv" \
     --factors "$SP/wf6/via-many-days/work/factors.json" --data "$SP/wf5/data/nyc-via/fleetpy-manhattan" \
     --work "$SP/wf6/via-many-days/work" --set weekdays --seedtag wf17-fresh --fmax 512:96 --resample k \
     --out "$SP/wf17/r1-identity-fence-pad/cells/f16"
   ```
3. **Kiểm:** trong `SP/wf17/r1-identity-fence-pad/cells/f16`, chạy `sha256sum -c company-emulator/expected/wf17-f16-cells.sha256`. Phải ra 292/292 OK (96 ô × kịch bản, ma trận, provenance, cộng các tệp chung).
   - Nếu lệch, dừng lại và tìm nguyên nhân trước khi chạy tiếp. Thường là do sai đường dẫn `--data` hoặc `factors.json`.

### Bước 2. Hệ đầy đủ: Shadow + luật C + dự báo (`sp/wf20/fullsys/RUNBOOK.sh`)

- **Thứ tự bước:** `gate` → `gatecheck` → `f14n` → `f14cl` → (`f16n` → `f16cl` nếu đã có ô F16) → `check` → `analyze`.
- **Không cần chạy bước `prior`:** prior λ của F16 có sẵn ở `work/prior_F16/prior_prevhour.csv`. Muốn tự dựng lại thì chạy `prior` và so sha256 với tệp có sẵn.
- **Cổng** (`gates/GATES-pre.txt`, `results/GATES-post.txt`):
  - các dòng Shadow chế độ N trùng `wf15/results/confirm/main.ndjson`;
  - các dòng EOHZ35/RCE35 trùng các dòng đã có;
  - SHRC không có lần vượt sổ nào.
  - Riêng cổng so với wf17 R3 cần `wf17/r1-identity-fence-pad/results/r3.ndjson`, tệp này không có trong gói. Bỏ qua cổng đó; việc so ở mục 3 thay cho nó.
- **Thời gian:** khoảng 30 phút cho 3.600 lượt; F14 riêng khoảng 15 phút.
- **So:**
  ```bash
  node tools/confirm_digest.js expected/wf20__fullsys__results__rows.tsv "$SP/wf20/fullsys/results/rows.ndjson"
  ```
  Thêm `--panel F14` nếu chỉ chạy F14. Mong đợi 3.600/3.600 (F14: 1.680/1.680) trùng hoàn toàn.

### Bước 3. N7: dự báo so với đệm cùng độ dài (`sp/wf21/n7/RUNBOOK.sh`)

- **Thứ tự bước:** `gate` → `gatecheck` → `n14 cl14 pad14 clc14 padc14` → (`n16 cl16 pad16 clc16 padc16` nếu có ô F16) → `analyze`.
- **Không cần hiệu chỉnh lại r:** đã có sẵn ở `calib/flat_final.json` (PAD) và `calib/flatc_final.json` (PADc). Muốn tự kiểm việc hiệu chỉnh thì chạy vòng `calnext`/`calrun14`/`calrun16` theo `calloop.sh`, khoảng 4.770 lượt. Vòng này chỉ đọc độ dài lời hứa.
- **Bước `check`:** cần tệp dòng hiệu chỉnh, tệp này không có trong gói. Bỏ `--calib` hoặc bỏ qua bước này; dùng `confirm_digest.js` thay.
- **Chỉ mục nhân quả:** có sẵn ở `cidx/results/index-causal-pertarget.json`. Muốn dựng lại thì xem mục 4.
- **So:**
  ```bash
  node tools/confirm_digest.js expected/wf21__n7__results__rows.tsv "$SP/wf21/n7/results/rows.ndjson"
  ```
  Mong đợi 3.600/3.600 (F14: 1.680/1.680).

### Bước 4. Bảng "hệ của mình so với đối thủ" (sau bước 2)

```bash
node "$SP/wf20/ours-vs-theirs/ours_vs_theirs.js" "$SP/wf20/fullsys/results/rows.ndjson" "$SP/wf20/ours-vs-theirs/OURS-VS-THEIRS.home.md"
```

So với `sp/wf20/ours-vs-theirs/OURS-VS-THEIRS.md` có sẵn: phải giống từng số.

### Bước 5. Bảy cỡ đội xe (`sp/wf20/am2017/code/RUNBOOK.sh`, tùy chọn)

- **Thứ tự bước:** `gates` → `prod` → `postgate` → `analyze`. Chạy trên D30 (ô có trong P4), khoảng 15 phút.
- **So:** `node tools/confirm_digest.js expected/wf20__am2017__results__am.tsv "$SP/wf20/am2017/results/am.ndjson"`. Mong đợi 3.360/3.360.

### Bước 6. wf17 R4, R5, R6 (tùy chọn)

- R4/R5: `sp/wf17/r2-penalty-regime/code/RUNBOOK.sh gates r4 r5`. Cổng cần `wf16/a5-steering/results/robust.ndjson`.
- R6: `sp/wf17/r3-curves-nodes/code/RUNBOOK.sh r6`.
- So bằng `expected/wf17__r2-penalty-regime__results__r4.tsv`, `…r5.tsv`, `wf17__r3-curves-nodes__results__r6.tsv`.
- R3 trên F16 (`expected/wf17__r1-identity-fence-pad__results__r3.tsv`) cần ô F16 và driver `r3drive.js` (lệnh nằm ở dòng chú thích đầu `r1-identity-fence-pad/code/RUNBOOK.sh`).

### Bước 7. Kiểm độc lập bằng script riêng (tùy chọn)

Chạy `sp/wf20/verify-fullsys/scripts/indep.js` và các script `sp/wf21/verify-n7/v7*.js` trên các dòng vừa chạy. Chúng tính lại các bảng bằng mã khác hẳn mã phân tích gốc.

---

## 3. Đọc kết quả `confirm_digest.js`

| Kết quả | Ý nghĩa | Làm gì |
|---|---|---|
| N/N trùng hoàn toàn | Chạy lại ở nhà ra đúng từng khách | Ghi vào `docs/18`: "đã chạy lại ở máy nhà, trùng N/N dòng". Trong khóa luận, đổi nhãn [CT] thành nguồn ở máy nhà |
| "Trùng chỉ số nhưng khác dấu vân tay khách" | Số tổng giống, nhưng lộ trình của một số khách khác | Kiểm phiên bản Node, đường dẫn ô, cờ `--x star`; ghi lại |
| "Lệch chỉ số" hoặc "Thiếu" | Chạy khác hoặc chưa đủ | Xem 5 dòng lệch đầu tiên mà script in ra; tìm nguyên nhân; **không chỉnh mã cho khớp** |

---

## 4. Dựng lại chỉ mục dự báo nhân quả (tùy chọn)

- **Mã:** `sp/wf21/n7/cidx/scripts/`. Lần lượt chạy `build_sql.js` → `run_duck.sh` (DuckDB) → `build_index.js`.
- **Cần dữ liệu chuyến taxi vàng thô** tháng 8–12/2019 (`yellow_tripdata_2019-MM.parquet`, đặt ở `SP/wf7/data/tlc-yellow-2019/`). Nếu chưa có thì tải từ trang dữ liệu NYC TLC.
- **Cổng:**
  - dựng theo luật cũ phải ra đúng `wf7/c-forecast/results/accuracy-index-2019.json` (14.688/14.688 giá trị);
  - dựng theo luật nhân quả phải ra đúng `cidx/results/index-causal-pertarget.json` có trong gói.
- **Cũng có thể bỏ qua:** kết quả N7 cho thấy chỉ mục nhân quả gần như không đổi gì (chênh trễ ≤ 1,5/1000).

---

## 5. Kết quả mong đợi (để đối chiếu bảng sau khi `analyze`)

Chi tiết ở `BAO-CAO-KET-QUA-26-10.md`, `sp/wf20/fullsys/REPORT-VI.md`, `sp/wf21/n7/REPORT-VI.md`.

**Hệ đầy đủ, luật C so với hàng rào nới (cùng Shadow, cùng dự báo), 30 ngày:**
- G2: phục vụ −8,90/ngày; đảo hứa −13,81 (−42,5%); trễ +4,00/1000.
- INS: phục vụ −3,52; đảo hứa −0,34.

**Hệ của mình (SHRC + dự báo) so với đối thủ (SHHZ, không dự báo):**
- G2: phục vụ −8,84; đảo hứa −27,46; trễ −34,04/1000.
- Tách phần trễ (G2): luật C +3,87, độ dài −27,97, thông tin dự báo −9,94.

**N7, dự báo − đệm, trễ/1000, SHRC G2:**
- cả ngày −9,94 (F14 −2,59 / F16 −16,37);
- khung 08 −14,41 (F14 −9,77);
- khung 17 −6,12 (F14 +4,50).

**Bảng tổng F14 (khi chỉ chạy F14):** cộng dồn 84 ô của mỗi nhóm.

| Nhóm (config/chế độ/nhánh) | Tổng phục vụ | Tổng đảo hứa | Tổng trễ | Độ dài hứa TB (s) |
|---|---|---|---|---|
| G2 / N / SHRC | 28.784 | 1.307 | 7.146 | 1.588,79 |
| G2 / CL / SHRC | 28.781 | 748 | 6.072 | 1.589,35 |
| G2 / PAD / SHRC | 28.755 | 1.018 | 6.137 | 1.592,17 |
| G2 / N / SHHZ | 29.195 | 1.939 | 7.167 | 1.599,44 |
| INS / N / SHRC | 28.619 | 1.087 | 6.973 | 1.587,23 |
| INS / CL / SHRC | 28.745 | 578 | 5.971 | 1.589,25 |
| INS / PAD / SHRC | 28.562 | 786 | 5.947 | 1.591,12 |

---

## 6. Việc tiếp theo, chỉ khi chủ muốn

1. **N7b – hồ sơ cố định theo giờ đi qua điều phối.** Câu hỏi: dự báo "hôm nay" có hơn "quy luật giờ cao điểm thường ngày" không?
   - Thêm chế độ `CLIM` trong `sp/wf21/n7/code/scripts/shcore.js`, theo mẫu nhánh `CLc`.
   - Hồ sơ là **trung bình hình học** các hồ sơ của mọi ngày thường trong 4–8 tuần **trước** ngày đích, không theo thứ trong tuần, không dùng ngày đích. Quy tắc phải ghi trước.
   - Lưu ý: `sp/wf21/verify-n7/v7acc.js:11-13` lấy trung bình trên chính 30 ngày đích, nên **không** nhân quả; không dùng nguyên như vậy.
   - So CL − CLIM theo từng khung. Nếu CL hơn rõ thì được viết "thông tin dự báo theo ngày có giá trị".
2. **Kiểm luật C chỉ chèn ở cỡ Via trên RideBound thật, không chạy lại job.**
   - Viết script so khách chỉ đọc nhật ký (transcript), đặt cạnh `kits/via-bridge-2026-10-22/bridge_riders.py`.
   - Cổng: phải ra 2.407/2.407 khách trên 6 job đã qua.
   - Sau đó so 4 job từng hết bộ nhớ ở bản chốt cuối.

---

## 7. Quyết định của chủ (26/10) và quy tắc

**Quyết định:**
- Đối thủ chính là hàng rào nới; trình bày theo 3 bước: hàng rào chặt → hàng rào nới → luật C.
- Bỏ đợt xác nhận 3, kho riêng có dấu thời gian, họp thầy, khai báo AI.
- Dự báo là một phần của hệ, nhưng ghi đúng công từng phần.
- Chủ tự viết lời văn khóa luận; Claude chỉ kiểm số và build PDF khi được nhờ.

**Quy tắc:**
- Ghi kế hoạch và sha256 trước khi chạy.
- Không ghi đè đầu ra cũ; không xóa gì.
- Commit: tác giả và người commit đều là `tsunflowerr <23020155@vnu.edu.vn>`. Không có dòng Co-Authored-By.
- Chỉ push khi chủ cho phép.
- Báo kết quả xấu ngang kết quả tốt.
