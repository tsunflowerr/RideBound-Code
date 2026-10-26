# Chạy cả ba cùng lúc: Shadow + luật C + dự báo vòng kín (wf20/fullsys)

**Mọi con số trong tài liệu này đều là thăm dò sau kiểm toán, không phải kết quả xác nhận.** Lý do:
- Giả thuyết do AI viết sau kiểm toán, không có dấu thời gian bên ngoài.
- F14 đã chạy nhiều lần.
- F16 đã chạy ở wf17 R3, nhưng chưa từng chạy với nhánh Shadow.
- Giá trị p chỉ để mô tả, không hiệu chỉnh cho nhiều phép thử.

Đường dẫn viết tắt:
- K = .../scratchpad/wf20/fullsys
- V = .../scratchpad/wf20/verify-fullsys (kiểm tra độc lập)

Cách tính:
- Đơn vị là ngày = w08 + w17, mỗi khung lấy trung bình 3 lần lấy mẫu lại.
- Gộp = 30 ngày (F14 + F16).
- "/1000" = trên 1000 khách được phục vụ.

## 1. Tóm tắt
1. **Chạy chung được, mọi cổng đều qua.**
   - 3.600 lượt chạy, 0 lỗi.
   - Mình tính lại toàn bộ bằng script riêng: lệch lớn nhất 1,3·10⁻¹² (V/results/compare_v2.txt:1).
   - Nhãn dự đoán khớp 282/282 (:3).
2. **G2, luật C so với hàng rào nới** (cùng có Shadow và dự báo):
   - Lợi: đảo giờ hứa −13,81/ngày (−42,5 %, 30/30 ngày); TV5 −18,2 s mỗi khách (30/30).
   - **Hại: phục vụ −8,90 khách/ngày (27/30 ngày); trễ > 60 s +4,0 trên 1000 (24/30 ngày, p 0,001).**
3. **INS (chế độ RideBound thật):** luật C không có lợi đo được về đảo giờ hứa (−0,34/ngày, n.s.) mà mất 3,52 khách/ngày (25/30 ngày). Lợi duy nhất là bảo đảm sổ cái: 0 so với 4,38 vi phạm trên 1000.
4. **Dự báo dưới Shadow:**
   - Cả ngày: trễ −32 đến −38 trên 1000, đảo −25 % đến −48 %.
   - **Nhưng khung 17 giờ xấu đi +17 đến +21 trên 1000, trong 26–29/30 ngày, ở cả 6 ô.**
5. **Hệ mình (SHRC+G2+CL) so với Elmi như trong bài báo (SHHZ+INS+N):**
   - Phục vụ như nhau; đảo −30 %; trễ −31,9 trên 1000.
   - **Xấu hơn: trễ khung 17 +26,3 trên 1000 (30/30 ngày) và Pchg +6,6 trên 1000 (27/30 ngày).**
6. Bản tóm tắt của người chạy có 5 chỗ cần sửa nhỏ (§8). Không chỗ nào đổi kết luận.

## 2. Đã chạy gì
**Thiết kế (K/PLAN.md:23-37):**
- 180 ô: F14 có 14 ngày × 2 khung × 3 lần lấy mẫu = 84 ô; F16 có 16 × 2 × 3 = 96 ô.
- Mỗi ô chạy 2 cấu hình (INS, G2) × 2 chế độ (N không dự báo, CL vòng kín prev4) × 5 nhánh = **3.600 lượt**.
- 5 nhánh:
  - SHF = Shadow + hàng rào chặt;
  - SHHZ = Shadow + hàng rào nới;
  - SHRC = Shadow + luật C;
  - EOHZ35 và RCE35 làm tham chiếu không Shadow.
- Thế giới: P512, 62 xe, sức chứa 4.

**Thay đổi mã** (chỉ trong bản chép K/code; diff với wf17 khớp đúng như mô tả):
- shcore.js: ở chế độ CL đặt `opts.forecast = {prof, clipPickup: true}` y như r3drive.js, trên cùng `opts` đang mang bộ chấm Shadow (K/code/scripts/shcore.js:130-148).
- run_sh.js: thêm --fc, --panel, --prior; tối đa 4 worker.
- runner/ không đổi; engine17 sha256 81d7b47e… = engine của R3.

**Tham số Shadow đóng băng:**
- x* = 72 % (INS) / 71 % (G2), θ* lấy từ wf15/frozen/shadow-params.json.
- x* được hiệu chỉnh trên ngày khám phá, **ở chế độ N**, để SHHZ phục vụ bằng EO35.

**Thứ tự thời gian** (theo giờ ghi file):
- PLAN.md và PLAN-HASH: 04:08:46Z.
- Cổng xong lúc 04:09:55Z.
- Chạy chính: 04:10:04Z → 04:37:28Z.
- 17 sha256 trong PLAN-HASH đều khớp.

## 3. Cổng (mình kiểm lại độc lập, tất cả QUA)
| Cổng | Kết quả | Nguồn |
|---|---|---|
| g0a | Xuất lại prior của wf15: sha256 d40e2f3b… trùng từng byte | tự sha256sum |
| g0b | Prior F16: 16 ngày, 32 cặp ngày–khung, lấy từ giờ trước mỗi khung; SQL chỉ khác tệp danh sách ngày | K/work/prior_report.txt |
| x* | 2.232 hàng Shadow, 0 sai; θ* đúng theo cấu hình × khung | V/results/indep_gates_v2.txt:3 |
| g1 | 504/504 hàng Shadow F14-N trùng wf15 trên mọi trường chỉ số (tới 102 trường). Chỉ khác ở trường wf15 không ghi (sig, sigX, diagTieBreaks, variant, audit, fenceTolMs) và trường sổ sách | :6-7 |
| g1x | 336/336 | :8-9 |
| g2 | 1.440/1.440 hàng EOHZ35/RCE35 (N và CL) trùng R3, **kể cả dấu vân tay từng khách** | :11-12 |
| g3 | 720/720 hàng SHRC có 0 vi phạm sổ cái; có kiểm tra Labs ≤ bSH ngay trong lúc chạy | :14-15; shcore.js:100 |
| g4 | 40 nhóm đủ ô, 0 trùng | :4 |
| g5 | 120/120 hàng cổng trùng hàng chạy chính | :17 |
| Dự báo có tác dụng | 0/180 cặp CL–N trùng chữ ký ở mỗi nhánh; chỉ CL có lần cắt ETA | :18 |
| Lỗi | 0 ở cả 4 lượt | :19 |

**Giới hạn của g1:** chỉ kiểm được ở mức chỉ số, không tới từng khách, vì wf15 không lưu dấu vân tay khách.

## 4. Bảng toàn hệ (gộp 30 ngày, mức mỗi ngày; V/results/indep_v2.txt:964-995)
| Chỉ số | (a) SHHZ+INS+N | (b) SHRC+G2+CL | (c) SHHZ+G2+CL | (d) RCE35+G2+CL | (e) EOHZ35+G2+CL |
|---|---|---|---|---|---|
| Phục vụ/ngày | 695,1 | 694,7 | **703,6** | 700,0 | 702,4 |
| Đảo giờ hứa/ngày | 26,76 | 18,70 | 32,51 | **12,11** | 24,30 |
| Trễ/1000, cả ngày | 243,5 | 211,5 | **207,5** | 277,3 | 275,8 |
| … khung 08 | 306,1 | 212,7 | 206,9 | 250,5 | 249,9 |
| … khung 17 | **183,9** | 210,2 | 208,1 | 302,1 | 299,7 |
| Pchg/1000 | **198,7** | 205,3 | 210,1 | 305,1 | 309,5 |
| Vi phạm hạn chót/1000 | 33,36 | 5,68 | 6,65 | 2,63 | **1,78** |
| TV5 (s) | 203,8 | 192,6 | 210,9 | **101,2** | 114,2 |
| Khách bảo đảm trễ/ngày | 21,37 | **3,62** | 4,29 | — | — |
| Tỷ lệ khách bảo đảm (%) | 69,08 | 67,79 | 68,51 | — | — |
| Vi phạm sổ cái/1000 | 6,20 | **0** | 46,79 | **0** | 31,25 |
| Chuyển xe/ngày | 0 | 8,83 | 33,73 | 13,86 | 25,97 |

Hiệu ghép theo ngày (V/results/indep_v2.txt:700-847):
| Hiệu | Phục vụ | Đảo | Trễ/1000 | Trễ/1000 khung 17 | Pchg/1000 | Hạn chót/1000 | TV5 |
|---|---|---|---|---|---|---|---|
| b−a | −0,40 (14−/13+) | −8,06 (−30,1 %, 25−/5+) | −31,94 (29−/1+) | **+26,26 (0−/30+)** | **+6,61 (3−/27+)** | −27,68 | −11,14 (23−/7+) |
| b−c | **−8,90 (27−/3+)** | −13,81 (30/30) | **+4,00 (6−/24+)** | +2,04 (n.s.) | −4,82 | −0,96 | −18,22 |
| b−d | −5,29 (p 0,099) | **+6,59 (+54,4 %, 0−/30+)** | −65,81 | −91,92 | −99,79 | **+3,05 (3−/24+)** | **+91,42 (0−/30+)** |
| c−a | +8,50 (3−/27+) | +5,76 (8−/22+) | −35,95 | +24,22 (2−/28+) | +11,43 | −26,71 | +7,08 |
| d−a | +4,89 (7−/22+) | −14,64 | +33,86 (2−/28+) | +118,2 | +106,4 | −30,72 | −102,6 |

Cách đọc:
- **Hệ đầy đủ (b) không thắng toàn diện.** (c) phục vụ nhiều nhất và trễ ít nhất, nhưng có 32,5 đảo/ngày và 46,8 vi phạm sổ cái trên 1000.
- Bảng này đổi cùng lúc ba thứ (Shadow, G2, dự báo), nên chỉ để mô tả.
- Khách bảo đảm trễ giảm từ 21,37 xuống 3,62 chủ yếu nhờ dự báo (−17,1 đến −17,3 khách/ngày, §6). Phần của luật C chỉ là −0,67.

## 5. Phép so chính SHRC_CL − SHHZ_CL (G2: indep_v2.txt:352-383; INS: :4-35)
**G2:**
| Phạm vi | Phục vụ | Đảo | Trễ/1000 | Pchg/1000 | Hạn chót/1000 | TV5 |
|---|---|---|---|---|---|---|
| Cả ngày | **−8,90±1,09 (27−/3+)** | −13,81 (−42,5 %, 30/30) | **+4,00 (24/30 xấu hơn, p 0,001)** | −4,82 (26/30) | −0,96 (p 0,008) | −18,22 (30/30) |
| Khung 08 | −3,73 (24−/5+) | −7,64 (−48,6 %) | **+5,77 (22/30, p 0,016)** | −3,73 | −1,17 | −20,88 |
| Khung 17 | −5,17 (28−/2+) | −6,17 (−36,7 %) | +2,04 (n.s.) | −5,92 (23−/7+) | −0,80 | −15,66 |
| F14 / F16 | −10,57 / −7,44 | −13,83 / −13,79 | +2,56 (p 0,42) / +5,26 (p 5e-4) | −5,64 / −4,10 | −1,19 / −0,77 | −18,71 / −17,80 |

Thêm ở G2:
- Khách bảo đảm trễ −0,67/ngày (−15,5 %).
- Chuyển xe −73,8 %.
- Sổ cái 0 so với 46,79.
- Tỷ lệ bảo đảm của SHRC thấp hơn 0,72 điểm % dù cùng x*.

**INS:**
| Phạm vi | Phục vụ | Đảo | Trễ/1000 | Pchg/1000 | Hạn chót/1000 | TV5 |
|---|---|---|---|---|---|---|
| Cả ngày | **−3,52±0,76 (25−/5+)** | −0,34 (n.s.) | +0,39 (n.s.) | −1,31 (p 0,099) | −0,26 | +0,28 |
| Khung 08 | −0,99 | −0,10 | +0,43 | −0,88 | −0,47 | +0,07 |
| Khung 17 | −2,53 (24−/6+) | −0,24 | +0,20 | −1,83 | −0,10 | +0,60 |
| F14 / F16 | −3,81 / −3,27 | −0,02 / −0,62 | −0,38 / +1,07 | −1,84 / −0,84 | −0,54 / −0,02 | +0,68 / −0,06 |

Sổ cái INS: 0 so với 4,38 trên 1000 (30/30 ngày, :29).

Dự báo không làm đổi phép so này. Hiệu-của-hiệu G2, (SHRC−SHHZ) ở CL trừ (SHRC−SHHZ) ở N (:906):
- đảo +0,82±0,70 (p 0,46);
- phục vụ +1,12 (n.s.);
- trễ +0,13 (n.s.).

Shadow làm luật C đắt hơn:
- G2: cái giá thêm 6,49 khách/ngày (24−/6+, p 0,001; :938).
- INS: thêm 4,11 khách/ngày (:880).
- Mức cắt đảo gần như nhau: −13,8 khi có Shadow, −12,2 (−50,2 %) khi không.

## 6. Dự báo thêm gì dưới Shadow (SH*_CL − SH*_N; indep_v2.txt:100-195, 448-543)
| Ô | Trễ/1000 cả ngày | Khung 08 | **Khung 17** | Đảo | TV5 | Hạn chót | Phục vụ | Pchg | Khách bảo đảm trễ | Tỷ lệ bảo đảm (điểm %) |
|---|---|---|---|---|---|---|---|---|---|---|
| INS SHF | −31,78 | −86,97 | **+18,87 (27/30)** | −47,7 % | −17,76 | −78,1 % | −1,87 | −0,53 | −12,78 | +0,10 |
| INS SHHZ | −36,54 | −97,61 | **+21,34 (29/30)** | −46,3 % | −17,80 | −80,9 % | +1,51 | +1,55 | −17,29 | +0,14 |
| INS SHRC | −37,78 | −97,27 | **+18,93 (28/30)** | −46,4 % | −17,52 | −81,4 % | +2,42 | +1,07 | −17,06 | +0,20 |
| G2 SHF | −32,83 | −86,96 | **+17,19 (26/30)** | −24,8 % | −16,28 | −79,7 % | −1,91 | +0,52 | −13,23 | +0,20 |
| G2 SHHZ | −38,04 | −97,29 | **+18,52 (28/30)** | −29,6 % | −19,89 | −79,6 % | +0,06 | −0,60 | −17,18 | −0,01 |
| G2 SHRC | −37,91 | −97,27 | **+18,87 (28/30)** | −40,7 % | −19,35 | −82,6 % | +1,18 | −0,23 | −17,07 | +0,01 |
| Không Shadow | −44,4 đến −46,2 | −131,7 đến −139,1 | +37,1 đến +43,0 (30/30) | −26 đến −44 % | −18,2 đến −18,5 | −85 đến −88 % | ≤0,8 | ≤1,2 | — | — |

Cách đọc:
- **Tốt:** dự báo giảm trễ, đảo, TV5 và vi phạm hạn chót ở mọi ô. Phục vụ và Pchg không đổi đáng kể (mọi p ≥ 0,20).
- **Xấu:** khung 17 giờ xấu đi ở cả 6 ô.
- Dưới Shadow, cả phần lợi lẫn phần hại của dự báo chỉ còn khoảng một nửa so với khi không có Shadow (chỉ là mô tả).
- x* đóng băng vẫn đứng được: tỷ lệ bảo đảm dịch tối đa 0,51 điểm % khi gộp và 0,67 điểm % trong từng nhóm ngày, trong ngưỡng ±3 đã dự đoán.

## 7. Dự đoán trúng/trượt (K/results/labels.txt:1-97; mình áp lại độc lập: 94/94 gộp, 282/282 cả F14/F16)
**Gộp:** 81 GIỮ, 7 MẠNH HƠN, 6 YẾU ĐI, 0 ĐẢO CHIỀU, 0 không kết luận được.

**MẠNH HƠN:**
- P1r: G2 cắt đảo −42,5 % (dự đoán −25 đến −40 %).
- P14 ở 3 ô INS: −46,3 đến −47,7 %.
- P16 ở INS SHHZ, INS SHRC, G2 SHRC: hạn chót −80,9 đến −82,6 %.

**YẾU ĐI:**
- P13 ở 5/6 ô: hại khung 17 là +17,2 đến +18,9, dưới dải +20 đến +45; riêng INS SHHZ +21,34 là GIỮ. Hướng hại vẫn đúng.
- P25f: Shadow thêm hạn chót +3,05 (dự đoán +5 đến +30).

**Dự đoán xấu cũng GIỮ** (tức cái hại đã báo trước):
- P2: G2 −8,90 khách.
- P3: trễ +4,00, nằm trong dải −3 đến +6, nhưng có ý nghĩa theo hướng xấu.
- P9: INS −3,52 khách.
- P8: INS không có lợi về đảo.

**Theo nhóm ngày:**
- F16 P25a: YẾU ĐI (−2,77).
- F16 P26 INS: MẠNH HƠN (+41,85).

P27 (cổng): QUA.

Lưu ý: mình đã đọc nhãn của người chạy trước khi viết luật nhãn của mình.

## 8. Cần sửa trong bản tóm tắt của người chạy
1. Sổ cái INS phải là "0 so với 4,38 trên 1000 (30/30 ngày)", không phải "0 so với ≈0".
2. "F14 và F16 cùng dấu ở mọi chỗ" là nói quá. Có 18 chỗ khác dấu, đều ở hiệu gần 0 (indep_v2.txt:996-1014). Chỉ 1 chỗ có p < 0,05: b−d, phục vụ khung 08, F14 −1,88 so với F16 +4,71 (p 0,004).
3. "26–30/30 ngày" phải là "26–29/30 ngày".
4. TV5 của (b) là 192,65, làm tròn là 192,6, không phải 192,7.
5. g1 trùng ở mức chỉ số, không phải mức từng khách.

## 9. Giới hạn
- **Ngày đã xem:**
  - F14 đã chạy nhiều lần.
  - F16 đã chạy ở R3 với các nhánh không Shadow.
  - Hàng Shadow-N trên F14 chỉ là chạy lại wf15.
  - Phần thật sự mới: toàn bộ nhánh CL có Shadow, và Shadow-N trên F16.
- **Giả thuyết do AI viết sau kiểm toán**, không có dấu thời gian bên ngoài.
- **x* đóng băng**, hiệu chỉnh theo EO35 ở chế độ N, không hiệu chỉnh lại cho CL. SHHZ_CL là "ngưỡng của Elmi đặt trong dự báo của mình".
- **So ở cùng x*, không phải cùng mức phục vụ.** Vì vậy không so thẳng được với wf17 R6.
- **Bảng toàn hệ đổi ba thứ cùng lúc**, chỉ để mô tả.
- **Chỉ có trong bộ mô phỏng JS.** Shadow không có bản C# trong RideBound, nên mọi kết quả có Shadow phải ghi là "chỉ trong bộ mô phỏng".
- **Prior λ của F16 là mới**, nhưng chỉ là dữ liệu đầu vào và đã qua g0.
- **Giá trị p chỉ để mô tả.**

## 10. Tệp
- K/PLAN.md, K/PLAN-HASH.txt, K/DEVIATIONS.md (D1–D3, chỉ về trình bày)
- K/results/rows.ndjson, K/results/GATES-post.txt, K/results/tables.json, K/results/labels.txt
- V/PLAN.md, V/PLAN-HASH.txt, V/scripts/indep.js
- V/results/indep_gates_v2.txt, V/results/indep_v2.txt, V/results/indep_v2.json, V/results/compare_v2.txt, V/results/labels_indep_v2.txt
- V/DEVIATIONS.md

## 11. Sửa sau phản biện (máy công ty, 26/10)
- Dưới Shadow chỉ phần hại khung 17 của dự báo còn khoảng một nửa (+17 đến +21 so với +37 đến +43/1000). Phần lợi buổi sáng còn khoảng 2/3–3/4 (−87 đến −98 so với −132 đến −139). Tính theo tỷ lệ cả ngày, lợi gần như không đổi (khoảng −13% đến −15%, có hay không Shadow).
- (b) so với (a) còn ba điểm xấu theo khung: buổi sáng đảo nhiều hơn (8,08 so với 2,49/ngày, 26/30 ngày), nên mức −30% cả ngày đến hoàn toàn từ khung chiều (−13,64); vi phạm hạn chót khung 17 cao hơn (+4,2/1000); khách bảo đảm trễ khung 17 tăng từ 0,40 lên 1,66/ngày (mức giảm 21,37 → 3,62 nằm toàn bộ ở buổi sáng). b − d phục vụ khung 17 là −6,9/ngày (25/30 ngày).
- "Dự báo không làm đổi phép so" chỉ đúng cho cả ngày: buổi sáng dự báo thu hẹp mức cắt đảo của luật C 1,60/ngày (p 0,016), buổi chiều bù lại.
- g1 trùng trên 97 trường không phải sổ sách, ở mức chỉ số, không tới từng khách.
- "Chạy chung được" nghĩa là chạy không lỗi và qua mọi cổng; không phải kết quả xác nhận.
