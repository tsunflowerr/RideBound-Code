# Chạy RideBound thật trên F14 + F16 (30 ngày): kết quả và đối chiếu với bộ mô phỏng

Ngày 06/11/2026. Nhánh `research/final-real-runs-2026-11-06`. Mọi con số dưới đây lấy từ các tệp trong `results/` của thư
mục này. Danh sách băm ở `results/RESULTS.sha256`.

## 1. Tóm tắt

- **Đã chạy:** 360 job RideBound thật trên máy thuê Linux, 168 job F14 và 192 job F16. Cả 360 job đều `status-pass`.
- **Không trùng tuyệt đối với dự đoán đã khóa trước** (`allExact` = false ở cả hai đợt):
  - F14: lệch dồn vào một cặp ô-nhánh. Nguyên nhân là giới hạn của bộ mô phỏng (mục 3.1).
  - F16: chỉ 5/67.922 khách lệch, và lệch ở mức mili giây.
- **Hệ thật cho đúng con số của dự đoán adapter**, không phải con số của bộ mô phỏng plain.
- 30 ngày hệ thật, ta − Elmi mỗi ngày:
  - ta ít khách bị đảo hứa hơn (−27,34), ít trễ so với hứa đầu hơn (−34,23/1000), TV5 thấp hơn (−37,70 giây/khách);
  - ta phục vụ ít hơn 6,66 khách/ngày (khoảng 0,95 %);
  - ở khung 17 giờ, ta trễ so với hứa đầu nhiều hơn (+21,83/1000), trong cả 30/30 ngày.

## 2. Thiết lập

- **Hai nhánh:**

  | Tên | Mã nhánh | Gồm |
  |---|---|---|
  | Ta | `FS-G2-SHRC71-CL` | G2 + Shadow luật C (SHRC71) + dự báo vòng kín (CL) |
  | Elmi | `FS-G2-SHHZ71-N` | G2 + Shadow hàng rào Elmi (SHHZ71), không dự báo |

- **Ô:**
  - F14: 14 ngày × 2 khung (w08, w17) × 3 lần lấy mẫu = 84 ô.
  - F16: 16 ngày × 2 khung × 3 lần lấy mẫu = 96 ô.
  - Tổng 30 ngày, mỗi ô chạy cả 2 nhánh.
- **Đơn vị ngày** (quy tắc `fsan.js`): một ngày = tổng hai khung của trung bình ba lần lấy mẫu. Hiệu tính theo cặp ngày.
  Báo kèm khoảng tin cậy 95 % theo t và kiểm định dấu chính xác.
- **Chỉ số:**
  - Khách bị đảo hứa (`visRev60`): số khách có giờ trả hiển thị đổi chiều ít nhất một lần; mỗi lần đổi ≥ 60 s.
  - Trễ so với hứa đầu: khách được phục vụ có A − p0 > 60 s, tính trên 1000 khách.
  - TV5: tổng biến thiên của chuỗi giờ trả đã công bố, giây/khách.
  - Sai số ETA: trung bình theo thời gian của |ETA đang hiển thị − A| trên [t0, A), lấy trung bình trên khách được phục vụ
    (`eta_metrics.py`).
- **Mã chạy:**
  - Gói `final-v2`, commit xuất `0eb9a6ca`. Commit này không có trên GitHub, nhưng trùng commit công khai `9ba5568` ở
    `src`, `simulators`, `tests` (498/498 blob).
  - Runner không build lại. Kiểm kê repo `271166f4…`.
- **Máy thuê:** Linux, 11 vCPU, 49 GiB, 6 worker.
- **Dự đoán khóa trước job đầu tiên:**
  - F14: các tệp `FULLSYS-F14-*.sha256`.
  - F16: kế hoạch `plan/PLAN-F16.md` (`7c9f251e…`), dự đoán khóa 04:49Z, chuỗi ETA 05:16Z, ETA theo thời gian 06:49Z
    ngày 05/11.
- **Cổng F16:** 4/4 cặp, 1.412/1.412 khách trùng trên cả 4 mặt.
- **Thời gian chạy:**
  - F14 xong 168 job lúc 10:24 ngày 05/11, so sánh xong 13:39.
  - F16 bắt đầu 15:02 ngày 05/11, job cuối 03:07, so sánh xong 06:35, nén xong 06:37 ngày 06/11.

## 3. Đối chiếu từng khách với dự đoán adapter đã khóa

| Mặt | F14 | F16 |
|---|---|---|
| Khách (t0, p0, A, phục vụ, cam kết, dời G2): cặp trùng | 166/168 (ta 84/84, Elmi 82/84) | 187/192 (ta 94/96, Elmi 93/96) |
| Khách trùng | 57.954/58.042 | 67.917/67.922 |
| Lần dời G2 trùng (cặp) | 167/168 | 192/192 |
| Chuỗi giờ trả: cặp trùng | 143/168 | 158/192 |
| Chuỗi giờ trả: khách lệch | 393 | 225 |
| visRev60 tổng (dự đoán / thật) | 2.695 / 2.694 | 3.097 / 3.097 |
| Shadow: cặp trùng | 167/168 | 190/192 |
| ETA theo thời gian: cặp trùng | 141/168 | 158/192 |

Nguồn: `results/f14-report/FINAL-REPORT.json`, `results/f16-report/FINAL-REPORT.json`.

### 3.1 F14: một cặp lệch lớn, nguyên nhân nằm ở bộ mô phỏng

- **Cặp lệch:** Elmi `d20191015-vf-r1-w08`, chiếm 87 khách lệch và 275/393 khách lệch chuỗi.
- **Hệ thật tự lặp lại.** Chạy lại đúng job này với cùng đầu vào (`ridebound-diag-20261105.tar`) cho kết quả trùng
  hoàn toàn:
  - `audit.table` 185.900/185.900 dòng, 25 lần dời / 2.903 quyết định, 8.716 khung transcript;
  - chỉ khác `runId`.
- **Chỗ rẽ:** ở 2.224,046 s (`scripts/session/first_divergence.py`, `transcript_window.py`). Hệ thật có 5 vòng quyết
  định ở cùng thời điểm (epoch 961–965):
  - 961: xe `veh-4a17` tới nút, đón `rq-fd9f`. Bộ mô phỏng làm giống hệt.
  - 962: một lô chỉ có `vehicleAdvanced` của chính xe đó gây thêm một quyết định. Quyết định này chuyển `rq-954a`, khách
    đang chờ ở đúng nút đó, từ `veh-7b6d` sang `veh-4a17`.
  - 963: đón `rq-954a` trong cùng mili giây.
- **Vì sao bộ mô phỏng khác:** nó chỉ quyết định một lần mỗi thời điểm, trừ khi vừa nhận khách mới (`engine_wf15_clint.js`
  dòng 783–794). Nó chỉ tìm ra nước chuyển này ở sự kiện sau, 2.226,935 s, khi xe đã rời nút. Kết quả: `rq-954a` sớm
  39 s thay vì 142 s, hai khách kia muộn thêm 113 s thay vì 10 s.
- **Kết luận:** đây là giới hạn của mô hình lô sự kiện trong bộ mô phỏng, không phải lỗi của RideBound. Không sửa gì, vì
  dự đoán đã khóa.
- **Chưa kiểm:** 24 cặp lệch nhỏ còn lại ở chuỗi ETA, vì transcript của chúng đã bị bỏ.
- **Ảnh hưởng lên tổng:** nhỏ. Mỗi ngày, Elmi:
  - visRev60 45,74 → 45,71;
  - số lần đảo hứa 70,57 → 70,52;
  - phục vụ 29.214 → 29.213 (tổng 14 ngày).

  Phía ta trùng hoàn toàn.

### 3.2 F16: năm khách lệch ở mức mili giây

Mỗi cặp dưới đây lệch đúng 1 khách. Lấy từ `results/f16-report/1-riders.log`:

| Nhánh | Ô | Trường lệch (dự đoán / thật) |
|---|---|---|
| Elmi | `d20191010-vf-r2-w17` | `sumAbsC` 898.213 / 898.837, `netC` 79.357 / 78.733 |
| Elmi | `d20191025-vf-r0-w17` | p0 7.168.820 / 7.168.959 ms (139 ms) |
| Elmi | `d20191028-vf-r1-w08` | A 9.039.640 / 9.039.639 ms (1 ms) |
| Ta | `d20191008-vf-r0-w17` | p0 2.811.553 / 2.811.751 ms (198 ms) |
| Ta | `d20191025-vf-r0-w17` | A 6.267.684 / 6.267.683 ms (1 ms) |

- Tổng TV5 lệch 673 ms trên 67.922 khách.
- Không có cặp lệch lớn như ở F14.
- Chưa tìm nguyên nhân các lệch mili giây, vì transcript của các job này đã bị bỏ.

## 4. ETA và đảo hứa theo nhánh (hệ thật)

| | F14 Elmi | F14 ta | F16 Elmi | F16 ta |
|---|---|---|---|---|
| Khách được phục vụ | 29.213 | 28.820 | 34.064 | 33.858 |
| Sai số ETA trung bình (phút) | 0,673 | 0,604 | 0,679 | 0,591 |
| Sai số ETA trung vị / P90 (phút) | 0,148 / 1,958 | 0,090 / 1,755 | 0,152 / 1,972 | 0,092 / 1,717 |
| Sai số của ETA đầu tiên (phút) | 3,062 | 2,908 | 3,040 | 2,865 |
| Khách bị đảo hứa (visRev60) | 1.920 | 774 | 2.206 | 891 |
| Tổng số lần đảo hứa | 2.962 | 1.072 | 3.389 | 1.228 |
| Khách đảo 1 / 2 / ≥ 3 lần | 1.151 / 574 / 195 | 524 / 215 / 35 | 1.310 / 696 / 200 | 596 / 264 / 31 |
| Tối đa số lần đảo của một khách | 7 | 5 | 7 | 5 |

Nguồn: `eta.json` và `reversals.json` trong `results/f14-report/` và `results/f16-report/`. Ở F16, phía dự đoán trùng hệ thật tới 3 chữ số thập phân. Ở F14,
dự đoán của Elmi khác hệ thật ở chữ số thứ ba (ví dụ visRev60 1.921 so với 1.920).

## 5. Bảng 30 ngày: ta − Elmi mỗi ngày

Nguồn: `results/synth-real-30d.md` (`synth_real_30d.py`). Mỗi ô ghi giá trị trung bình, KTC 95 % và (số ngày ta cao
hơn / ta thấp hơn).

### Cả ngày (30 ngày)

| Chỉ số | Bộ mô phỏng plain | Dự đoán adapter | **Hệ thật** |
|---|---|---|---|
| Khách phục vụ | −8,84 [−10,91; −6,78] (2/28) | −6,67 [−8,77; −4,57] (4/26) | **−6,66** [−8,75; −4,56] (4/26) |
| Khách bị đảo hứa | −27,46 (0/30) | −27,36 (0/30) | **−27,34** [−31,38; −23,31] (0/30) |
| Trễ > 60 s so với hứa đầu /1000 | −34,04 (1/29) | −34,26 (1/29) | **−34,23** [−40,15; −28,32] (1/29) |
| TV5 (giây/khách) | −38,11 (0/30) | −37,78 (0/30) | **−37,70** [−41,39; −34,01] (0/30) |

- Mức của hệ thật, ta / Elmi:
  - phục vụ 696,42 / 703,08 khách/ngày;
  - đảo hứa 18,50 / 45,84;
  - trễ 210,97 / 245,20 trên 1000;
  - TV5 192,74 / 230,43 giây/khách.

### Theo khung giờ, hệ thật (30 ngày)

| Chỉ số | Khung 8 giờ | Khung 17 giờ |
|---|---|---|
| Khách phục vụ | −3,74 [−5,33; −2,16] (5/25) | −2,91 [−4,25; −1,57] (3/26) |
| Khách bị đảo hứa | −6,91 [−9,34; −4,48] (2/28) | −20,43 [−23,04; −17,82] (0/30) |
| Trễ > 60 s so với hứa đầu /1000 | −93,26 [−103,23; −83,28] (0/30) | **+21,83** [13,64; 30,03] (30/0) |
| TV5 (giây/khách) | −49,48 [−54,32; −44,65] (0/30) | −26,70 [−32,24; −21,17] (1/29) |

### Theo đợt, chỉ số phục vụ, hệ thật

- F14: −9,36 [−11,98; −6,73], 0/14 ngày.
- F16: −4,29 [−7,22; −1,37], 4/12 ngày, kiểm định dấu p = 0,077. KTC theo t không chứa 0, nhưng kiểm định dấu không đạt
  mức 5 %.

## 6. D1: bộ mô phỏng plain so với adapter, tách luật C và dự báo

Nguồn: `results/d1-split.md` và `results/d1-gap.txt` (`d1_split.py`, `scripts/session/d1_gap.py`).

- **Hai nhánh tách phần:** `FS-G2-SHHZ71-CL` (hàng rào Elmi + dự báo) và `FS-G2-SHRC71-N` (luật C, không dự báo). Hai
  nhánh này chỉ có trong bộ mô phỏng, không chạy trên hệ thật.
- **Nguồn dự đoán adapter của hai nhánh này:**
  - F16: nằm trong tệp đã khóa trước khi chạy.
  - F14: sinh ngày 05/11 lúc 16:39–18:01, sau khi F14 đã chạy thật, vào
    `fullsys-f14-v2/predictions-timed-attr-d1/`. Bước sinh tự kiểm: trùng 58.034 hàng và chuỗi đã khóa của hai nhánh
    chính; nhánh N mới trùng engine gốc ở 84 cặp.
- **Cách tách:** tổng = ta − Elmi = luật C (ta − `SHHZ71-CL`) + dự báo (`SHHZ71-CL` − Elmi).
- **Kiểm:**
  - Phần plain trùng từng số với bảng tách phần của máy công ty.
  - rev60/tv5 tính lại trùng tệp chuỗi đã khóa ở 125.956/125.956 khách.

30 ngày:

| Chỉ số | plain: tổng = luật C + dự báo | adapter: tổng = luật C + dự báo |
|---|---|---|
| Khách phục vụ | −8,84 = −8,90 + 0,06 | −6,67 = −7,56 + 0,89 |
| Khách bị đảo hứa | −27,46 = −13,81 − 13,64 | −27,36 = −13,99 − 13,37 |
| Trễ > 60 s /1000 | −34,04 = +4,00 − 38,04 | −34,26 = +4,08 − 38,34 |
| TV5 | −38,11 = −18,22 − 19,89 | −37,78 = −17,61 − 20,17 |

**Khoảng cách adapter − plain theo ngày:**

- Đảo hứa, trễ, TV5: không khác. Mọi KTC chứa 0, cả ở tổng lẫn ở từng phần.
- Phục vụ: +2,18 [0,22; 4,14], dấu 20/10, p = 0,10, là ca biên giới.
  - Hầu hết đến từ F16: +3,67 [0,91; 6,43]. F14 chỉ +0,48 [−2,39; 3,34].
  - Chỉ nhánh ta đổi: +1,73 [0,27; 3,20] khách/ngày. Elmi −0,44 [−1,89; 1,00].
  - Tách phần: luật C +1,34 [−0,55; 3,24], dự báo +0,83 [−0,78; 2,45]. Không phần nào tự nó có ý nghĩa.

**Hệ quả:** con số phục vụ đúng là con số của adapter, đã được hệ thật xác nhận. Con số plain làm cái giá phục vụ của ta
lớn hơn khoảng 2 khách/ngày. Cơ chế gây ra khoảng cách này trong bộ mô phỏng chưa được chứng minh. Các ứng viên là:

- dừng 0 s;
- làm tròn mốc thời gian về mili giây nguyên;
- lưới đủ nút;
- thứ tự yêu cầu.

## 7. Giới hạn

- Tiêu chí "trùng từng khách trên mọi cặp" không đạt ở cả hai đợt. Phải báo đúng như vậy.
- Chỉ chứng minh được nguyên nhân cho cặp lệch lớn của F14. Các lệch nhỏ còn lại chưa kiểm.
- Hai nhánh tách phần của D1 chỉ có trong bộ mô phỏng, không chạy trên hệ thật.
- Không dùng các kết quả này để nhận là mới các ý: chèn động, giới hạn ETA, gán lại, độ giống tuyến, cam kết muộn, nhất
  quán thời gian hay sự hài lòng của khách (xem `docs/03-related-work-and-claim-boundary.md`).

## 8. Tệp

### Trong thư mục này

| Đường dẫn | Nội dung |
|---|---|
| `results/f14-report/`, `results/f16-report/` | Báo cáo so sánh của máy thuê: `FINAL-REPORT.json`, `riders-compare.json`, `sequences.json`, `shadow.json`, `eta.json`, `reversals.json`, các log. Bỏ `riders-real-riders.tsv` (10 MB mỗi tệp, băm ở dưới) |
| `results/synth-real-30d.{md,json}` | Bảng 30 ngày plain / adapter / hệ thật |
| `results/d1-split.{md,json}`, `results/d1-gap.txt` | D1 |
| `results/vast-console/` | Log console của máy thuê: `run_linux.log`, `run_f16_linux.log`, nén tay F14, chạy chẩn đoán; `VAST.sha256` lấy trên máy thuê |
| `plan/` | `PLAN-F16.md` và băm |
| `scripts/fastrun-2026-11-02/` | 70 tệp (script và hướng dẫn) của bộ công cụ chạy lần cuối, chép nguyên từ `ridebound-scratchpad/fastrun-2026-11-02` (bỏ `docker/` và các thư mục đầu ra `replay-*`) |
| `scripts/session/` | Script của phiên 05–06/11: theo dõi, tải, `post_f16.sh`, `d1_gap.py`, tìm chỗ rẽ, chạy chẩn đoán |
| `scripts/HELD-BACK.tsv` | 7 tệp **không đưa lên** vì repo công khai, kèm băm và lý do |
| `TIEN-DO-2026-11-03.md` | Nhật ký tiến độ 03–06/11 |

**Tệp không đưa lên (`scripts/HELD-BACK.tsv`):**

- 5 tệp chép mã bộ mô phỏng wf15 của công ty, vốn chưa công khai: `engine_wf15_clint.js`, `gen_forecast_golden.js`,
  `emulate_via_full.js`, `emulate_fullsys.js`, `emulate_fullsys_f16.js`.
- 2 tệp chứa tên công ty trong biểu thức lọc: `assemble_final.py`, `pack_final.py`.

Thiếu các tệp này thì các script `emulate_*` còn lại không chạy được trên máy khác. Bản gốc vẫn nằm trong
`ridebound-scratchpad`.

### Ngoài repo (`E:/RideBoundData/research/`)

| Tệp | Byte | sha256 |
|---|---|---|
| `fullsys-f14-v2/runs-linux-2026-11-05/ridebound-results-20261105T064554Z.tar.gz` | 1.901.747.869 | `d60b9f82d45c1ded1c94add6553235f9df4ac70481001f8df2b154d23d928ae4` |
| `fullsys-f14-v2/runs-linux-2026-11-05/ridebound-transcripts-20261105T064554Z.tar` | 3.038.238.720 | `5c2cb751bc304fae3a48dccd6468e279492087cb62518fc362dd67b98bec47e2` |
| `fullsys-f14-v2/runs-linux-2026-11-05/ridebound-diag-20261105.tar` | 708.372.480 | `8aab583e26f224915b85aaa72ffa4fb99fd589c9e03f510b2a78056f1a27ffdf` |
| `fullsys-f16-v2/runs-linux-2026-11-06/ridebound-f16-results-20261105T233511Z.tar.gz` | 2.179.000.962 | `b25fae51e81130c5fe90c548496bd1718f46c1ba4b91c16ce27b5fcef5193a62` |
| `fullsys-f16-v2/runs-linux-2026-11-06/ridebound-f16-transcripts-20261105T233511Z.tar` | 2.908.887.040 | `9b83fee388b7b3914f7ba50bec804ae09b535d2ed74c18bac18ecdf914d35c45` |
| F14 `report/riders-real-riders.tsv` | — | `786048fb931a66a6f3c3e55bf12bd358203615013c5bc04ea6c510f45bf0a28a` |
| F16 `report-f16/riders-real-riders.tsv` | — | `fa785fde16310ddbc99723dd4839f156abcc6e7d43df1ee51cb916316ef1e87e` |

Mọi gói đều đã kiểm sha256 sau khi tải về. Thư mục `/rb/runs` và `/rb/runs-diag` trên máy thuê đều nằm trong các gói này.

## 9. Chạy lại phân tích

Cần dữ liệu ở `E:/RideBoundData` và các hàng của máy công ty (đường dẫn ghi trong `synth_real_30d.py`). Dùng Python
`E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe`:

```bash
# Bảng 30 ngày (F14 và F16 đều là hệ thật). post_f16.sh làm đúng việc này, nhưng thư mục đầu ra của nó nay đã có tệp
# nên nó sẽ từ chối chạy; muốn chạy lại thì gọi thẳng với --out mới:
F14=E:/RideBoundData/research/fullsys-f14-v2; F16=E:/RideBoundData/research/fullsys-f16-v2
python -I -B -X utf8 scripts/fastrun-2026-11-02/final/synth_real_30d.py --out <tiền tố> \
  --panel F14 $F14/predictions/fullsys-f14-predictions.tsv $F14/predictions/fullsys-f14-drop-sequences.tsv \
              $F14/runs-linux-2026-11-05/extracted/runs/report/sequences.json \
              $F14/runs-linux-2026-11-05/extracted/runs/report/riders-real-riders.tsv \
  --panel F16 $F16/predictions/fullsys-f16-predictions.tsv $F16/predictions/fullsys-f16-drop-sequences.tsv \
              $F16/runs-linux-2026-11-06/extracted/runs/report-f16/sequences.json \
              $F16/runs-linux-2026-11-06/extracted/runs/report-f16/riders-real-riders.tsv

# D1
python -I -B -X utf8 scripts/fastrun-2026-11-02/final/d1_split.py --out <tiền tố> \
  --timed E:/RideBoundData/research/fullsys-f14-v2/predictions-timed-attr-d1/timed-adapter.tsv.gz \
  --timed E:/RideBoundData/research/fullsys-f16-v2/predictions-timed/timed-adapter.tsv.gz \
  --check E:/RideBoundData/research/fullsys-f14-v2/predictions/fullsys-f14-drop-sequences.tsv \
  --check E:/RideBoundData/research/fullsys-f16-v2/predictions/fullsys-f16-drop-sequences.tsv
```

Các script ghi ra tệp mới và từ chối ghi đè.
