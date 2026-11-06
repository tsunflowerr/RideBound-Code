# Hệ của mình (Shadow + luật C + dự báo) so với hàng rào nới + Shadow (không dự báo)

Thăm dò sau kiểm toán, tính lại từ wf20/fullsys/results/rows.ndjson (30 ngày F14+F16, 3 lần lấy mẫu); không chạy thêm. Ngày = khung 8 giờ + khung 17 giờ, mỗi khung lấy trung bình 3 lần lấy mẫu. Hiệu = của mình − của đối thủ; [khoảng tin cậy 95% theo t]; (số ngày hiệu dương / âm; p của phép kiểm dấu chính xác).

## Chế độ G2 (gán lại)

### Mức mỗi ngày (trung bình 30 ngày)

| Chỉ số | Của mình: SHRC + dự báo | Đối thủ: SHHZ, không dự báo | Tham khảo: SHF (hàng rào chặt), không dự báo |
|---|---|---|---|
| Phục vụ (khách/ngày) | 694,69 | 703,53 | 666,03 |
| Đảo hứa (khách/ngày) | 18,70 | 46,16 | 42,18 |
| Trễ >60 s so với hứa đầu /1000 | 211,52 | 245,56 | 242,09 |
| Bị điều phối dời giờ /1000 | 205,28 | 210,69 | 214,37 |
| Vi phạm hạn chót Elmi /1000 | 5,68 | 32,57 | 26,93 |
| TV5 (giây/khách) | 192,65 | 230,76 | 223,59 |
| Khách bảo đảm trễ (khách/ngày) | 3,62 | 21,47 | 16,61 |
| Vượt sổ luật C /1000 | 0,00 | 48,69 | 39,95 |

### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), cả ngày

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | -8,84 | [-10,91; -6,78] | 2/28 | 9e-7 |
| Đảo hứa (khách/ngày) | -27,46 | [-31,32; -23,60] | 0/30 | 2e-9 |
| Trễ >60 s so với hứa đầu /1000 | -34,04 | [-40,20; -27,89] | 1/29 | 6e-8 |
| Bị điều phối dời giờ /1000 | -5,42 | [-8,11; -2,73] | 7/23 | 0,005 |
| Vi phạm hạn chót Elmi /1000 | -26,89 | [-30,85; -22,92] | 1/29 | 6e-8 |
| TV5 (giây/khách) | -38,11 | [-41,88; -34,34] | 0/30 | 2e-9 |
| Khách bảo đảm trễ (khách/ngày) | -17,84 | [-20,40; -15,29] | 1/29 | 6e-8 |
| Vượt sổ luật C /1000 | -48,69 | [-52,35; -45,03] | 0/30 | 2e-9 |

### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), khung 8 giờ

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | -4,69 | [-6,02; -3,36] | 4/26 | 6e-5 |
| Đảo hứa (khách/ngày) | -6,83 | [-9,16; -4,50] | 2/28 | 9e-7 |
| Trễ >60 s so với hứa đầu /1000 | -91,51 | [-101,28; -81,74] | 0/30 | 2e-9 |
| Bị điều phối dời giờ /1000 | -7,29 | [-10,97; -3,61] | 6/24 | 0,001 |
| Vi phạm hạn chót Elmi /1000 | -59,48 | [-66,53; -52,44] | 0/30 | 2e-9 |
| TV5 (giây/khách) | -49,64 | [-54,13; -45,15] | 0/30 | 2e-9 |
| Khách bảo đảm trễ (khách/ngày) | -19,07 | [-21,09; -17,04] | 0/30 | 2e-9 |
| Vượt sổ luật C /1000 | -50,60 | [-54,72; -46,48] | 0/30 | 2e-9 |

### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), khung 17 giờ

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | -4,16 | [-5,36; -2,95] | 3/27 | 8e-6 |
| Đảo hứa (khách/ngày) | -20,62 | [-23,20; -18,04] | 0/30 | 2e-9 |
| Trễ >60 s so với hứa đầu /1000 | 20,56 | [12,65; 28,47] | 26/4 | 6e-5 |
| Bị điều phối dời giờ /1000 | -3,84 | [-7,39; -0,29] | 9/21 | 0,043 |
| Vi phạm hạn chót Elmi /1000 | 4,07 | [-0,67; 8,82] | 14/3 | 0,013 |
| TV5 (giây/khách) | -27,30 | [-33,17; -21,43] | 1/29 | 6e-8 |
| Khách bảo đảm trễ (khách/ngày) | 1,22 | [-0,26; 2,70] | 14/3 | 0,013 |
| Vượt sổ luật C /1000 | -46,21 | [-51,87; -40,55] | 0/30 | 2e-9 |

### Tách phần: luật C và dự báo (cả ngày)

| Chỉ số | Tổng (của mình − đối thủ) | Phần luật C (SHRC+dự báo − SHHZ+dự báo) | Phần dự báo (SHHZ+dự báo − SHHZ) |
|---|---|---|---|
| Phục vụ (khách/ngày) | -8,84 (2/28) | -8,90 | 0,06 |
| Đảo hứa (khách/ngày) | -27,46 (0/30) | -13,81 | -13,64 |
| Trễ >60 s so với hứa đầu /1000 | -34,04 (1/29) | 4,00 | -38,04 |
| Bị điều phối dời giờ /1000 | -5,42 (7/23) | -4,82 | -0,60 |
| Vi phạm hạn chót Elmi /1000 | -26,89 (1/29) | -0,96 | -25,92 |
| TV5 (giây/khách) | -38,11 (0/30) | -18,22 | -19,89 |
| Khách bảo đảm trễ (khách/ngày) | -17,84 (1/29) | -0,67 | -17,18 |
| Vượt sổ luật C /1000 | -48,69 (0/30) | -46,79 | -1,90 |

### So thêm với hàng rào chặt (SHRC+dự báo − SHF), cả ngày

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | 28,66 | [25,58; 31,73] | 30/0 | 2e-9 |
| Đảo hứa (khách/ngày) | -23,48 | [-27,38; -19,58] | 1/29 | 6e-8 |
| Trễ >60 s so với hứa đầu /1000 | -30,57 | [-36,74; -24,40] | 1/29 | 6e-8 |
| Bị điều phối dời giờ /1000 | -9,09 | [-11,75; -6,43] | 5/25 | 3e-4 |
| Vi phạm hạn chót Elmi /1000 | -21,25 | [-25,00; -17,50] | 1/29 | 6e-8 |
| TV5 (giây/khách) | -30,94 | [-35,08; -26,80] | 0/30 | 2e-9 |
| Khách bảo đảm trễ (khách/ngày) | -12,99 | [-15,37; -10,61] | 1/29 | 6e-8 |
| Vượt sổ luật C /1000 | -39,95 | [-43,56; -36,34] | 0/30 | 2e-9 |

## Chế độ INS (chỉ chèn, chế độ thật của RideBound)

### Mức mỗi ngày (trung bình 30 ngày)

| Chỉ số | Của mình: SHRC + dự báo | Đối thủ: SHHZ, không dự báo | Tham khảo: SHF (hàng rào chặt), không dự báo |
|---|---|---|---|
| Phục vụ (khách/ngày) | 693,08 | 695,09 | 656,57 |
| Đảo hứa (khách/ngày) | 14,03 | 26,76 | 25,10 |
| Trễ >60 s so với hứa đầu /1000 | 207,31 | 243,46 | 241,42 |
| Bị điều phối dời giờ /1000 | 198,91 | 198,66 | 203,70 |
| Vi phạm hạn chót Elmi /1000 | 6,11 | 33,36 | 26,73 |
| TV5 (giây/khách) | 186,28 | 203,79 | 191,07 |
| Khách bảo đảm trễ (khách/ngày) | 3,86 | 21,37 | 16,37 |
| Vượt sổ luật C /1000 | 0,00 | 6,20 | 5,60 |

### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), cả ngày

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | -2,01 | [-4,13; 0,10] | 9/21 | 0,043 |
| Đảo hứa (khách/ngày) | -12,72 | [-16,43; -9,02] | 3/27 | 8e-6 |
| Trễ >60 s so với hứa đầu /1000 | -36,15 | [-41,69; -30,61] | 0/30 | 2e-9 |
| Bị điều phối dời giờ /1000 | 0,24 | [-1,26; 1,74] | 14/16 | 0,856 |
| Vi phạm hạn chót Elmi /1000 | -27,25 | [-31,59; -22,91] | 1/29 | 6e-8 |
| TV5 (giây/khách) | -17,51 | [-21,34; -13,69] | 2/28 | 9e-7 |
| Khách bảo đảm trễ (khách/ngày) | -17,51 | [-20,32; -14,70] | 1/29 | 6e-8 |
| Vượt sổ luật C /1000 | -6,20 | [-7,29; -5,10] | 0/30 | 2e-9 |

### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), khung 8 giờ

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | -0,70 | [-2,03; 0,63] | 10/16 | 0,327 |
| Đảo hứa (khách/ngày) | 3,21 | [1,21; 5,21] | 19/4 | 0,003 |
| Trễ >60 s so với hứa đầu /1000 | -97,17 | [-106,65; -87,70] | 0/30 | 2e-9 |
| Bị điều phối dời giờ /1000 | -1,93 | [-4,79; 0,93] | 12/18 | 0,362 |
| Vi phạm hạn chót Elmi /1000 | -60,84 | [-68,48; -53,20] | 0/30 | 2e-9 |
| TV5 (giây/khách) | -26,82 | [-31,57; -22,06] | 0/30 | 2e-9 |
| Khách bảo đảm trễ (khách/ngày) | -18,86 | [-21,13; -16,58] | 0/30 | 2e-9 |
| Vượt sổ luật C /1000 | -0,73 | [-1,18; -0,28] | 0/12 | 5e-4 |

### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), khung 17 giờ

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | -1,31 | [-2,78; 0,16] | 10/19 | 0,136 |
| Đảo hứa (khách/ngày) | -15,93 | [-18,58; -13,28] | 0/30 | 2e-9 |
| Trễ >60 s so với hứa đầu /1000 | 21,54 | [13,67; 29,41] | 30/0 | 2e-9 |
| Bị điều phối dời giờ /1000 | 2,11 | [-0,19; 4,40] | 18/12 | 0,362 |
| Vi phạm hạn chót Elmi /1000 | 4,48 | [-0,71; 9,68] | 13/3 | 0,021 |
| TV5 (giây/khách) | -8,85 | [-14,03; -3,66] | 6/24 | 0,001 |
| Khách bảo đảm trễ (khách/ngày) | 1,34 | [-0,22; 2,91] | 13/2 | 0,007 |
| Vượt sổ luật C /1000 | -11,31 | [-13,15; -9,46] | 0/30 | 2e-9 |

### Tách phần: luật C và dự báo (cả ngày)

| Chỉ số | Tổng (của mình − đối thủ) | Phần luật C (SHRC+dự báo − SHHZ+dự báo) | Phần dự báo (SHHZ+dự báo − SHHZ) |
|---|---|---|---|
| Phục vụ (khách/ngày) | -2,01 (9/21) | -3,52 | 1,51 |
| Đảo hứa (khách/ngày) | -12,72 (3/27) | -0,34 | -12,38 |
| Trễ >60 s so với hứa đầu /1000 | -36,15 (0/30) | 0,39 | -36,54 |
| Bị điều phối dời giờ /1000 | 0,24 (14/16) | -1,31 | 1,55 |
| Vi phạm hạn chót Elmi /1000 | -27,25 (1/29) | -0,26 | -26,98 |
| TV5 (giây/khách) | -17,51 (2/28) | 0,28 | -17,80 |
| Khách bảo đảm trễ (khách/ngày) | -17,51 (1/29) | -0,22 | -17,29 |
| Vượt sổ luật C /1000 | -6,20 (0/30) | -4,38 | -1,82 |

### So thêm với hàng rào chặt (SHRC+dự báo − SHF), cả ngày

| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |
|---|---|---|---|---|
| Phục vụ (khách/ngày) | 36,51 | [32,71; 40,31] | 30/0 | 2e-9 |
| Đảo hứa (khách/ngày) | -11,07 | [-14,62; -7,51] | 2/26 | 3e-6 |
| Trễ >60 s so với hứa đầu /1000 | -34,10 | [-40,15; -28,06] | 1/29 | 6e-8 |
| Bị điều phối dời giờ /1000 | -4,79 | [-7,52; -2,07] | 7/23 | 0,005 |
| Vi phạm hạn chót Elmi /1000 | -20,62 | [-24,66; -16,58] | 1/29 | 6e-8 |
| TV5 (giây/khách) | -4,79 | [-8,56; -1,01] | 8/22 | 0,016 |
| Khách bảo đảm trễ (khách/ngày) | -12,51 | [-15,05; -9,97] | 1/29 | 6e-8 |
| Vượt sổ luật C /1000 | -5,60 | [-6,71; -4,49] | 0/30 | 2e-9 |
