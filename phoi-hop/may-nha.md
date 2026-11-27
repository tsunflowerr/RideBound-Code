# Máy nhà (phụ)

## TRẢ LỜI 11 — 2026-11-27 21:35 (ở `471a311`)

Đạt.

1. `full_common.py` mới (`RB_EVIDENCE` như TRẢ LỜI 10):
   ```
   w08 pooled {'ctl_all': (30896, 3.102, 0.665), 'full_all': (30559, 2.573, 0.52), 'ctl_common': (27372, 3.152, 0.673), 'full_common': (27372, 2.586, 0.519)}
   w17 pooled {'ctl_all': (32381, 3.0, 0.688), 'full_all': (32119, 3.181, 0.671), 'ctl_common': (28958, 2.99, 0.685), 'full_common': (28958, 3.158, 0.67)}
   ```
2. `full_common.json` mới bỏ `pooled_win` **bằng** bản cũ (`4138777`) khi cả hai chạy với `PYTHONHASHSEED=0`. Không đặt biến này
   thì hai tệp lệch ở chữ số thứ 15–16 (ví dụ `diff.day.all.tv5.mean` −37,71381903414692 / −37,713819034146944): đó là nhiễu
   thứ tự cộng do duyệt `set` (`full_common.py:121`, `allc, allf = set(ctl), set(full)`); bản cũ chạy hai lần không đặt seed
   cũng lệch như vậy. Nên ghi `PYTHONHASHSEED=0` cạnh lệnh `full_common.py` trong README như đã làm với `eta_analysis.py`.
   `gen_eta_tables.py` (`RB_RESULTS` = rows đã lưu, `PYTHONHASHSEED=0`) in giống hệt từng byte với tệp cũ và tệp mới (85 dòng).
3. `kiem_chay_chinh.js` (bảng và dự đoán như TRẢ LỜI 10): `TỔNG OK 134 KHÁC 0 BỎ QUA 0`.

Còn lại: YÊU CẦU 10 mục 1–3 và YÊU CẦU 9; đợt mô phỏng nhanh của YÊU CẦU 9 đang chạy (xong main60, nce-oce60, fc30,
alpha10, fleet-20…52).

## TRẢ LỜI 10 (phần 1: mục 4) — 2026-11-27 19:40

Phần còn lại (mục 1–3) chạy sau khi xong đợt mô phỏng nhanh của YÊU CẦU 9 (đang chạy 6 tiến trình, máy hết RAM cho Runner).

**Mục 4, số liệu chạy chính:** `TỔNG OK 126 KHÁC 4 BỎ QUA 0`.
- `riders.tsv`, `sequences.tsv`: bảng toàn đợt 360 lượt dựng ở TRẢ LỜI 8 bằng `riders.py` của `d758335` (giữa `d758335` và
  `4138777` không đổi tệp nào trong `src/adapter`, `src/fastsim`): sha256 `452378ec…` và `79660500…`.
- `--pred`: dự đoán F14 + F16 nối lại (sha256 `3bdfa2d0…`), như khi chạy `compare_riders.js`.
- `full_common.py` với `RB_EVIDENCE` = thư mục có `publication-projection-f14/f16.jsonl.gz` (sha256 `4dbfe261…`, `320c444c…`),
  ghi `full_common.json` (không commit).
- 4 dòng KHÁC đều là sai số theo thời gian ở `04:104`, lệch ở chữ số thứ ba: w08 đối chứng 0,665 → 0,667; w08 kết hợp
  0,520 → 0,521; w17 đối chứng 0,688 → 0,687; w17 kết hợp 0,671 → 0,668. Không sửa gì trong khóa luận. Có thể do định
  nghĩa (gộp theo ngày, hay tập khách) khác với cách tính của khóa luận; `full_common.json` cho `timed_s` ở mục ALL/COMMON
  để đối chiếu.

Toàn bộ đầu ra:

```
THÔNG TIN	đầu vào	khách hoàn tất: đối chứng 63277, kết hợp 62678	ô: 180 + 180	bỏ qua: nhánh khác 0 dòng, chưa hoàn tất 0, thiếu chuỗi 0
OK   	số lượt mỗi cấu hình	khóa luận 180	tính lại 180	04:72
OK   	số lượt mỗi cấu hình (kết hợp)	khóa luận 180	tính lại 180	04:72
OK   	khách hoàn tất đối chứng	khóa luận 63.277	tính lại 63277	04:121 Bảng 5.3
OK   	khách hoàn tất kết hợp	khóa luận 62.678	tính lại 62678	04:121 Bảng 5.3
OK   	khách chung	khóa luận 56.330	tính lại 56330	04:136
OK   	khách chung / đối chứng (%)	khóa luận 89,0	tính lại 89,02 (89.02128735559523)	04:136
OK   	khách chung / kết hợp (%)	khóa luận 89,9	tính lại 89,87 (89.87204441749897)	04:136
OK   	Bảng 5.2 khách phục vụ: đối chứng	khóa luận 703,1	tính lại 703,08 (703.0777777777778)	04:88-91
OK   	Bảng 5.2 khách phục vụ: kết hợp	khóa luận 696,4	tính lại 696,42 (696.4222222222222)	04:88-91
OK   	Bảng 5.2 khách phục vụ: hiệu	khóa luận −6,66	tính lại -6,66 (-6.655555555555554)	04:88-91
OK   	Bảng 5.2 khách phục vụ: cận dưới	khóa luận −8,75	tính lại -8,75 (-8.749439097035658)	04:88-91
OK   	Bảng 5.2 khách phục vụ: cận trên	khóa luận −4,56	tính lại -4,56 (-4.56167201407545)	04:88-91
OK   	Bảng 5.2 khách phục vụ: số ngày cao/thấp	khóa luận 4/26	tính lại 4/26	04:88-91
OK   	Bảng 5.2 đổi qua lại 60 s: đối chứng	khóa luận 45,8	tính lại 45,84 (45.84444444444444)	04:88-91
OK   	Bảng 5.2 đổi qua lại 60 s: kết hợp	khóa luận 18,5	tính lại 18,50 (18.500000000000004)	04:88-91
OK   	Bảng 5.2 đổi qua lại 60 s: hiệu	khóa luận −27,34	tính lại -27,34 (-27.344444444444445)	04:88-91
OK   	Bảng 5.2 đổi qua lại 60 s: cận dưới	khóa luận −31,38	tính lại -31,38 (-31.380006693360066)	04:88-91
OK   	Bảng 5.2 đổi qua lại 60 s: cận trên	khóa luận −23,31	tính lại -23,31 (-23.308882195528824)	04:88-91
OK   	Bảng 5.2 đổi qua lại 60 s: số ngày cao/thấp	khóa luận 0/30	tính lại 0/30	04:88-91
OK   	Bảng 5.2 muộn > 1 phút /1.000: đối chứng	khóa luận 245,2	tính lại 245,20 (245.20461662465297)	04:88-91
OK   	Bảng 5.2 muộn > 1 phút /1.000: kết hợp	khóa luận 211,0	tính lại 210,97 (210.9727302148931)	04:88-91
OK   	Bảng 5.2 muộn > 1 phút /1.000: hiệu	khóa luận −34,23	tính lại -34,23 (-34.231886409760016)	04:88-91
OK   	Bảng 5.2 muộn > 1 phút /1.000: cận dưới	khóa luận −40,15	tính lại -40,15 (-40.146490082619614)	04:88-91
OK   	Bảng 5.2 muộn > 1 phút /1.000: cận trên	khóa luận −28,32	tính lại -28,32 (-28.317282736900417)	04:88-91
OK   	Bảng 5.2 muộn > 1 phút /1.000: số ngày cao/thấp	khóa luận 1/29	tính lại 1/29	04:88-91
OK   	Bảng 5.2 tổng mức đổi giờ s/khách: đối chứng	khóa luận 230,4	tính lại 230,45 (230.44944178229122)	04:88-91
OK   	Bảng 5.2 tổng mức đổi giờ s/khách: kết hợp	khóa luận 192,7	tính lại 192,74 (192.73562274814427)	04:88-91
OK   	Bảng 5.2 tổng mức đổi giờ s/khách: hiệu	khóa luận −37,71	tính lại -37,71 (-37.71381903414699)	04:88-91
OK   	Bảng 5.2 tổng mức đổi giờ s/khách: cận dưới	khóa luận −41,40	tính lại -41,40 (-41.400748157385564)	04:88-91
OK   	Bảng 5.2 tổng mức đổi giờ s/khách: cận trên	khóa luận −34,03	tính lại -34,03 (-34.02688991090841)	04:88-91
OK   	Bảng 5.2 tổng mức đổi giờ s/khách: số ngày cao/thấp	khóa luận 0/30	tính lại 0/30	04:88-91
OK   	đổi qua lại giảm (%)	khóa luận 59,6	tính lại 59,65 (59.64614638875423)	04:77
OK   	đổi qua lại giảm ở mọi ngày	khóa luận 30	tính lại 30	04:77
OK   	khách phục vụ giảm (%)	khóa luận 0,95	tính lại 0,95 (0.9466314774720708)	04:77
OK   	sáng: đổi qua lại hiệu	khóa luận −6,91	tính lại -6,91 (-6.9111111111111105)	04:98
OK   	sáng: tổng mức đổi giờ hiệu	khóa luận −49,52	tính lại -49,52 (-49.524362865790906)	04:98
OK   	chiều: đổi qua lại hiệu	khóa luận −20,43	tính lại -20,43 (-20.433333333333334)	04:98
OK   	chiều: tổng mức đổi giờ hiệu	khóa luận −26,70	tính lại -26,70 (-26.701877722818413)	04:98
OK   	sáng: muộn hiệu	khóa luận −93,26	tính lại -93,26 (-93.25897978069187)	04:100
OK   	sáng: muộn cận dưới	khóa luận −103,2	tính lại -103,23 (-103.23488821112332)	04:100
OK   	sáng: muộn cận trên	khóa luận −83,3	tính lại -83,28 (-83.28307135026043)	04:100
OK   	chiều: muộn hiệu	khóa luận +21,83	tính lại 21,83 (21.832140223053575)	04:100
OK   	chiều: muộn cận dưới	khóa luận +13,6	tính lại 13,64 (13.63546315403805)	04:100
OK   	chiều: muộn cận trên	khóa luận +30,0	tính lại 30,03 (30.0288172920691)	04:100
OK   	chiều: muộn tăng ở mọi ngày	khóa luận 30	tính lại 30	04:100
OK   	sai số giờ báo đầu, đối chứng (gộp khách)	khóa luận 3,05	tính lại 3,05 (3.0500269618766445)	04:102
OK   	sai số giờ báo đầu, kết hợp (gộp khách)	khóa luận 2,88	tính lại 2,88 (2.8847231444844903)	04:102
OK   	sai số giờ báo đầu 04:104 sáng, đối chứng (gộp khách)	khóa luận 3,10	tính lại 3,10 (3.1024669331520642)	04:104 sáng
OK   	sai số giờ báo đầu 04:104 sáng, kết hợp (gộp khách)	khóa luận 2,57	tính lại 2,57 (2.573424695397964)	04:104 sáng
THÔNG TIN	sai số giờ báo đầu 04:104 sáng, trung bình theo ngày	3,112	2,575
OK   	sai số giờ báo đầu 04:104 chiều, đối chứng (gộp khách)	khóa luận 3,00	tính lại 3,00 (2.999991899570755)	04:104 chiều
OK   	sai số giờ báo đầu 04:104 chiều, kết hợp (gộp khách)	khóa luận 3,18	tính lại 3,18 (3.180902020091945)	04:104 chiều
THÔNG TIN	sai số giờ báo đầu 04:104 chiều, trung bình theo ngày	2,999	3,175
OK   	Bảng 5.3 mọi khách: đổi qua lại hiệu	khóa luận −27,34	tính lại -27,34 (-27.344444444444445)	04:123
OK   	Bảng 5.3 mọi khách: đổi qua lại cận dưới	khóa luận −31,38	tính lại -31,38 (-31.380006693360066)	04:123
OK   	Bảng 5.3 mọi khách: đổi qua lại cận trên	khóa luận −23,31	tính lại -23,31 (-23.308882195528824)	04:123
OK   	Bảng 5.3 mọi khách: muộn hiệu	khóa luận −34,23	tính lại -34,23 (-34.231886409760016)	04:124
OK   	Bảng 5.3 mọi khách: muộn cận dưới	khóa luận −40,15	tính lại -40,15 (-40.146490082619614)	04:124
OK   	Bảng 5.3 mọi khách: muộn cận trên	khóa luận −28,32	tính lại -28,32 (-28.317282736900417)	04:124
OK   	Bảng 5.3 mọi khách: tổng mức đổi giờ hiệu	khóa luận −37,71	tính lại -37,71 (-37.71381903414699)	04:125
OK   	Bảng 5.3 mọi khách: tổng mức đổi giờ cận dưới	khóa luận −41,40	tính lại -41,40 (-41.400748157385564)	04:125
OK   	Bảng 5.3 mọi khách: tổng mức đổi giờ cận trên	khóa luận −34,03	tính lại -34,03 (-34.02688991090841)	04:125
OK   	Bảng 5.3 mọi khách: sai số giờ báo đầu hiệu	khóa luận −0,17	tính lại -0,17 (-0.1694017721446464)	04:126
OK   	Bảng 5.3 mọi khách: sai số giờ báo đầu cận dưới	khóa luận −0,22	tính lại -0,22 (-0.22429492088624675)	04:126
OK   	Bảng 5.3 mọi khách: sai số giờ báo đầu cận trên	khóa luận −0,11	tính lại -0,11 (-0.11450862340304607)	04:126
OK   	Bảng 5.3 khách chung: đổi qua lại hiệu	khóa luận −24,59	tính lại -24,59 (-24.58888888888889)	04:123
OK   	Bảng 5.3 khách chung: đổi qua lại cận dưới	khóa luận −28,17	tính lại -28,17 (-28.173409385892896)	04:123
OK   	Bảng 5.3 khách chung: đổi qua lại cận trên	khóa luận −21,00	tính lại -21,00 (-21.00436839188488)	04:123
OK   	Bảng 5.3 khách chung: muộn hiệu	khóa luận −34,02	tính lại -34,02 (-34.02456639359348)	04:124
OK   	Bảng 5.3 khách chung: muộn cận dưới	khóa luận −39,63	tính lại -39,63 (-39.63233319382596)	04:124
OK   	Bảng 5.3 khách chung: muộn cận trên	khóa luận −28,42	tính lại -28,42 (-28.416799593361006)	04:124
OK   	Bảng 5.3 khách chung: tổng mức đổi giờ hiệu	khóa luận −40,04	tính lại -40,04 (-40.03736740356028)	04:125
OK   	Bảng 5.3 khách chung: tổng mức đổi giờ cận dưới	khóa luận −43,82	tính lại -43,82 (-43.82268604998616)	04:125
OK   	Bảng 5.3 khách chung: tổng mức đổi giờ cận trên	khóa luận −36,25	tính lại -36,25 (-36.2520487571344)	04:125
OK   	Bảng 5.3 khách chung: sai số giờ báo đầu hiệu	khóa luận −0,19	tính lại -0,19 (-0.1928731998677329)	04:126
OK   	Bảng 5.3 khách chung: sai số giờ báo đầu cận dưới	khóa luận −0,25	tính lại -0,25 (-0.2488523268805303)	04:126
OK   	Bảng 5.3 khách chung: sai số giờ báo đầu cận trên	khóa luận −0,14	tính lại -0,14 (-0.13689407285493552)	04:126
OK   	Bảng 5.3 chiều mọi khách: muộn hiệu	khóa luận +21,83	tính lại 21,83 (21.832140223053575)	04:129
OK   	Bảng 5.3 chiều mọi khách: muộn cận dưới	khóa luận +13,64	tính lại 13,64 (13.63546315403805)	04:129
OK   	Bảng 5.3 chiều mọi khách: muộn cận trên	khóa luận +30,03	tính lại 30,03 (30.0288172920691)	04:129
OK   	Bảng 5.3 chiều mọi khách: sai số giờ báo đầu hiệu	khóa luận +0,18	tính lại 0,18 (0.17643677310743497)	04:130
OK   	Bảng 5.3 chiều mọi khách: sai số giờ báo đầu cận dưới	khóa luận +0,11	tính lại 0,11 (0.10580746802066027)	04:130
OK   	Bảng 5.3 chiều mọi khách: sai số giờ báo đầu cận trên	khóa luận +0,25	tính lại 0,25 (0.24706607819420967)	04:130
OK   	Bảng 5.3 chiều khách chung: muộn hiệu	khóa luận +20,84	tính lại 20,84 (20.841605560062863)	04:129
OK   	Bảng 5.3 chiều khách chung: muộn cận dưới	khóa luận +13,36	tính lại 13,36 (13.36108650959316)	04:129
OK   	Bảng 5.3 chiều khách chung: muộn cận trên	khóa luận +28,32	tính lại 28,32 (28.322124610532565)	04:129
OK   	Bảng 5.3 chiều khách chung: sai số giờ báo đầu hiệu	khóa luận +0,17	tính lại 0,17 (0.16532622056492371)	04:130
OK   	Bảng 5.3 chiều khách chung: sai số giờ báo đầu cận dưới	khóa luận +0,10	tính lại 0,10 (0.09814566428034202)	04:130
OK   	Bảng 5.3 chiều khách chung: sai số giờ báo đầu cận trên	khóa luận +0,23	tính lại 0,23 (0.2325067768495054)	04:130
THÔNG TIN	Bảng 5.3 khách chung: hiệu số khách phục vụ (phải 0)	0
OK   	chiều FS-G2-SHHZ71-N: dời muộn ròng do điều phối (phút, gộp khách)	khóa luận 2,77	tính lại 2,77 (2.7664226871725295)	04:581
OK   	chiều FS-G2-SHHZ71-N: sai số có dấu trừ phần ròng (phút, gộp khách)	khóa luận −0,54	tính lại -0,54 (-0.5403399730294492)	04:581
THÔNG TIN	chiều FS-G2-SHHZ71-N: sai số có dấu A−p0 (gộp khách / theo ngày)	2,226	2,224
OK   	chiều FS-G2-SHRC71-CL: dời muộn ròng do điều phối (phút, gộp khách)	khóa luận 2,94	tính lại 2,94 (2.936846870492024)	04:581
OK   	chiều FS-G2-SHRC71-CL: sai số có dấu trừ phần ròng (phút, gộp khách)	khóa luận 0,00	tính lại 0,00 (0.003717986757578688)	04:581
THÔNG TIN	chiều FS-G2-SHRC71-CL: sai số có dấu A−p0 (gộp khách / theo ngày)	2,941	2,933
OK   	phần dời sớm trong mức giảm tổng |c| (%)	khóa luận 73,1	tính lại 73,08 (73.08033245511271)	04:583
THÔNG TIN	số lần chuyển xe 3.033 → 757	không tính từ bảng từng khách; đã kiểm 18/11 (riders-compare: mọi lần chuyển; sổ cam kết 2.918 → 726 chỉ tính sau giờ báo đầu)
OK   	ngưỡng 30 s: đối chứng (khách/ngày)	khóa luận 79,60	tính lại 79,60 (79.59999999999998)	máy nhà 23/11
OK   	ngưỡng 30 s: kết hợp (khách/ngày)	khóa luận 53,43	tính lại 53,43 (53.43333333333333)	máy nhà 23/11
OK   	ngưỡng 30 s: mức giảm (%)	khóa luận 32,9	tính lại 32,87 (32.87269681742042)	04:712
OK   	ngưỡng 30 s: cận trên khoảng tin cậy < 0	khóa luận 1	tính lại 1	04:713
OK   	ngưỡng 60 s: đối chứng (khách/ngày)	khóa luận 45,84	tính lại 45,84 (45.84444444444444)	máy nhà 23/11
OK   	ngưỡng 60 s: kết hợp (khách/ngày)	khóa luận 18,50	tính lại 18,50 (18.500000000000004)	máy nhà 23/11
OK   	ngưỡng 60 s: mức giảm (%)	khóa luận 59,6	tính lại 59,65 (59.64614638875423)	04:712
OK   	ngưỡng 60 s: cận trên khoảng tin cậy < 0	khóa luận 1	tính lại 1	04:713
OK   	ngưỡng 120 s: đối chứng (khách/ngày)	khóa luận 21,50	tính lại 21,50 (21.500000000000007)	máy nhà 23/11
OK   	ngưỡng 120 s: kết hợp (khách/ngày)	khóa luận 5,49	tính lại 5,49 (5.488888888888889)	máy nhà 23/11
OK   	ngưỡng 120 s: mức giảm (%)	khóa luận 74,5	tính lại 74,47 (74.4702842377261)	04:712
OK   	ngưỡng 120 s: cận trên khoảng tin cậy < 0	khóa luận 1	tính lại 1	04:713
OK   	nhóm hạn mức 0 FS-G2-SHHZ71-N: số khách	khóa luận 43.339	tính lại 43339	04:320
OK   	nhóm hạn mức 0 FS-G2-SHHZ71-N: có thay đổi do điều phối	khóa luận 2.854	tính lại 2854	04:320
THÔNG TIN	nhóm hạn mức 0 FS-G2-SHHZ71-N	tỷ lệ 68,5 % (khóa luận: khoảng 68 %)	không có trong dự đoán: 8
OK   	nhóm hạn mức 0 FS-G2-SHRC71-CL: số khách	khóa luận 42.543	tính lại 42543	04:320
OK   	nhóm hạn mức 0 FS-G2-SHRC71-CL: có thay đổi do điều phối	khóa luận 0	tính lại 0	04:320
THÔNG TIN	nhóm hạn mức 0 FS-G2-SHRC71-CL	tỷ lệ 67,9 % (khóa luận: khoảng 68 %)	không có trong dự đoán: 0
OK   	nhóm hạn mức 0 đối chứng: tỷ lệ có thay đổi (%)	khóa luận 6,59	tính lại 6,59 (6.5852926924940585)	04:320 (số học trên số khóa luận)
OK   	full_common: khách đối chứng	khóa luận 63.277	tính lại 63277	đối chiếu chéo
OK   	full_common: khách kết hợp	khóa luận 62.678	tính lại 62678	đối chiếu chéo
OK   	full_common: khách chung	khóa luận 56.330	tính lại 56330	đối chiếu chéo
OK   	sai số theo thời gian đối chứng (gộp khách)	khóa luận 0,68	tính lại 0,68 (0.6763547936481654)	04:102
OK   	sai số theo thời gian kết hợp (gộp khách)	khóa luận 0,60	tính lại 0,60 (0.5973853440792765)	04:102
KHÁC 	sai số theo thời gian w08 đối chứng (theo ngày)	khóa luận 0,665	tính lại 0,667 (0.6665906851359935)	04:104
KHÁC 	sai số theo thời gian w08 kết hợp (theo ngày)	khóa luận 0,520	tính lại 0,521 (0.5205030820785929)	04:104
KHÁC 	sai số theo thời gian w17 đối chứng (theo ngày)	khóa luận 0,688	tính lại 0,687 (0.6867434657310638)	04:104
KHÁC 	sai số theo thời gian w17 kết hợp (theo ngày)	khóa luận 0,671	tính lại 0,668 (0.6677400200218361)	04:104
OK   	chiều: hiệu sai số theo thời gian chưa rõ (khoảng tin cậy chứa 0)	khóa luận 1	tính lại 1	04:104
OK   	Bảng 5.3 mọi khách: sai số theo thời gian hiệu	khóa luận −0,080	tính lại -0,080 (-0.08023883651672717)	04:127
OK   	Bảng 5.3 mọi khách: sai số theo thời gian cận dưới	khóa luận −0,097	tính lại -0,097 (-0.09699794842736525)	04:127
OK   	Bảng 5.3 mọi khách: sai số theo thời gian cận trên	khóa luận −0,063	tính lại -0,063 (-0.06347972460608908)	04:127
OK   	Bảng 5.3 khách chung: sai số theo thời gian hiệu	khóa luận −0,084	tính lại -0,084 (-0.08406525156364492)	04:127
OK   	Bảng 5.3 khách chung: sai số theo thời gian cận dưới	khóa luận −0,101	tính lại -0,101 (-0.10147456283998513)	04:127
OK   	Bảng 5.3 khách chung: sai số theo thời gian cận trên	khóa luận −0,067	tính lại -0,067 (-0.0666559402873047)	04:127
OK   	đối chiếu chéo rev (riders.tsv và full_common.json)	-27.344444444444445	-27.344444444444445	cùng định nghĩa
OK   	đối chiếu chéo late (riders.tsv và full_common.json)	-34.23188640976	-34.231886409760016	cùng định nghĩa
OK   	đối chiếu chéo tv5 (riders.tsv và full_common.json)	-37.71381903414692	-37.71381903414699	cùng định nghĩa
OK   	đối chiếu chéo initAbs (riders.tsv và full_common.json)	-0.16940177214464544	-0.1694017721446464	cùng định nghĩa
OK   	đối chiếu chéo served (riders.tsv và full_common.json)	-6.655555555555554	-6.655555555555554	cùng định nghĩa
TỔNG	OK 126	KHÁC 4	BỎ QUA 0
```

## TRẢ LỜI 8 — 2026-11-27 17:55 (YÊU CẦU 8, C31, ở đầu nhánh `d758335`)

Tất cả đạt.

1. **unittest** ở `d758335`: 5 module README §2.3 **40 qua, 0 hỏng**; `discover` có `RIDEBOUND_FLEETPY_ROOT` **117 qua, 0 hỏng**.
2. **Toàn đợt 360 lượt** (evidence 168 F14 + 192 F16 giải nén từ kho đã lưu, 8 ô cổng kèm transcript), lệnh
   `riders.py --root <F14 final> --root <F16 final-f16> --out … --sequences …`, mặc định hai nhánh chính:
   - `ab697fe` (8 ô cổng đọc qua transcript): 125955 khách từ 360 job, 2820 s.
   - đầu nhánh (đọc evidence): 2402 s.
   - `--out` sha256 `452378ec7111e9132a5631294a9f81914c5dc77e5306a3edf9e78a9d8a2aea8a` và `--sequences`
     `79660500a78f7b68da4918426719ea4618c11c8f48e84357d12a40917aedae8a`: **giống từng byte** giữa hai bản.
   - `compare_riders.js` (dự đoán F14 + F16 nối lại, sha256 `3bdfa2d0…`): `total: 125871/125964 riders identical on 9 fields;
     353/360 cell-arms identical`, như cũ.
3. **4 job B1** (có cả transcript lẫn evidence): `riders.py` ở đầu nhánh cho `--out` và `--sequences` giống từng byte bảng mốc
   B1 cả hai nhánh (714 và 710 dòng).

Không có job nào khác để gửi.

## TRẢ LỜI 7 và TRẢ LỜI 6 — 2026-11-27 14:15

Tất cả đạt. Không có khung, tệp hay test nào khác.

**Ở đầu nhánh `9cfa332` (YÊU CẦU 7, mục 1–2, gồm cả [10.25] và [10.27]):**
1. `dotnet test src/core/RideBound.slnx -c Release`: **790 qua, 0 hỏng, 0 bỏ qua** (56 s). Architecture 9, Contracts 121,
   Domain 218, Application 99, Solvers 14, Algorithms 171, Runner 158. Các lớp được nêu đều có trong danh sách test và qua
   (số test: `OrToolsModelIdentityTests` 3, `CandidateSearchWorkProfileTests` 5, `Wp4PolicyConfigurationBindingTests` 1,
   `FinalConfigGoldenTests` 20, `CommitmentCheckpointProcessTests` 2, `FinalCliTests` 22).
2. `replay.py runner` trên 4 transcript B1, Runner build ở `9cfa332` (`runnerDllSha256`
   `b55e2f388171a8577a6ca71fae539caf7f9b8df2b40bc5bed6cb252d1d7b55d5`): mọi khung **giống từng byte**.

   | B1 | khung so / giống | thời gian | RSS đỉnh Runner |
   |---|---|---|---|
   | SHHZ71-N w08 | 2849 / 2849 | 477 s | 4479 MiB |
   | SHHZ71-N w17 | 3146 / 3146 | 513 s | 4137 MiB |
   | SHRC71-CL w08 | 2878 / 2878 | 442 s | 4035 MiB |
   | SHRC71-CL w17 | 3092 / 3092 | 481 s | 4113 MiB |

**Ở `6a887ae` (YÊU CẦU 6, đã chạy trước khi có YÊU CẦU 7):**
1. `dotnet test`: **793 qua, 0 hỏng** (Architecture 9, Contracts 122, Domain 218, Application 99, Solvers 14, Algorithms 171,
   Runner 160).
2. `replay.py runner` trên 4 B1 (`runnerDllSha256` `0e7fb566d4bbb547…`): 2849/2849, 3146/3146, 2878/2878, 3092/3092 giống
   từng byte; 445–510 s; RSS đỉnh 4138–4599 MiB.
3. `kiem_nhanh.sh`: `sample check: 36 rows equal, 0 differ, 0 without an expected row` (131 s).
4. `emulate_fullsys.js adapter` trên 2 ô mẫu (`FS_ONLY`): `fullsys-f14-predictions.tsv` và 2 tệp `shadow-inputs` giống bản
   `ab697fe` từng byte; `fullsys-f14-predictions-counts.tsv` chỉ khác cột `ms`, bỏ cột đó thì bằng nhau.
   `node emulate_fullsys.js` và `node emulate_fullsys.js plain`: in `usage: node emulate_fullsys.js adapter <root> <out.tsv>`,
   thoát mã 2.
5. Tùy chọn (F16, cổng, chuỗi): chưa chạy.

Giữa `6a887ae` và `9cfa332` không có thay đổi nào trong `src/fastsim`, `src/datagen`, `src/adapter`, nên mục 3–4 ở trên
đúng cho đầu nhánh.

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
