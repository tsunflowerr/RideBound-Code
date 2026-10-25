# P5: lát Via ở chế độ adapter + half-even (đóng wf9-B3). Protocol viết TRƯỚC mọi lần chạy

25/10/2026. Kiểm toán công ty 23/10, §6-P5 (KT:605,655). Chủ cho phép chạy runner wf15 ngày 25/10.

## 1. Câu hỏi

Mọi số liệu Via của công ty sinh ở chế độ "plain + round". Cầu nối RideBound thật chỉ chứng minh chế độ "adapter +
half-even". Câu hỏi: **kết luận chính trên Via có đổi khi đổi chế độ không?**

Đây là phép thăm dò sau kiểm toán, trên các ngày xác nhận đã xem. Nó không phải một phép xác nhận mới.

## 2. Dữ liệu và cài đặt (cố định)

- **Ô:** 10 ngày trong 60 ngày xác nhận của wf14, chọn bằng quy tắc
  `sorted(days, key = sha256("p5-adapter-v1|" + day))[:10]`. Danh sách ở `P5-DAYS.txt`.
  - Mỗi ngày lấy cả hai khung w08, w17 và cả ba lần rút mẫu r0–r2: tổng 60 ô (`P5-CELLS.tsv`).
- **Bản plain:** tệp ô gốc, chép nguyên byte.
- **Bản adapter:** chỉ đổi hai trường:
  - `identity.mode` thành `ridebound-fleetpy-adapter`;
  - `travel.snapshotRounding` thành `fleetpy-seconds-half-even`.
- **Mã:** runner wf15 của công ty, chép nguyên byte vào `E:/RideBoundData/research/p5-via-adapter-v1/sp/wf15`; không sửa
  dòng nào. Đã kiểm 53/53 tệp với `PREREG-SHADOW-HASH.txt` (`setup_tree.sh`).
  - Mạng FleetPy lấy từ bản Zenodo của máy nhà, khớp sha trong provenance của ô.
  - `network.js` và `nyclib.js` khớp danh sách hash của công ty.
- **Lệnh cho mỗi chế độ:** giống lệnh xác nhận của công ty (`PREREG-SHADOW.md:81-84`), chỉ đổi thư mục ô, tệp ra và số
  luồng:

  ```
  node scripts/run_sh.js --scen <cells/plain hoặc cells/adapter> --frozen frozen/shadow-params.json --x star \
    --arms U,EO35,EOHZ35,RCE35,SHF,SHHZ,SHRC --out <rows> --part P5-<mode> --workers <n>
  ```

  Trên 60 ô: 2 cấu hình (INS, G2) × 7 nhánh = 840 dòng cho mỗi chế độ.

## 3. Cổng 0: dựng đúng chưa

Mỗi dòng plain chạy ở nhà phải **giống hệt** dòng xác nhận của công ty (`sp/wf15/results/confirm.ndjson`) cùng ô, cấu hình
và nhánh, ở mọi trường trừ `part`, `set`, `ms`, `msScore`.
- Đạt khi 840/840 giống.
- Không đạt thì **dừng**: không chạy hoặc không đọc chế độ adapter. Ghi lần lệch đầu tiên.

## 4. So sánh chính (`compare_p5.py`, viết và niêm phong trước khi chạy)

- **Giá trị một ngày** = tổng hai khung của trung bình ba lần rút mẫu. Một ngày chỉ vào một phép so khi cả hai nhánh đủ 6
  ô. Tỷ lệ tính bằng 100 × tổng số đếm / tổng số khách phục vụ của ngày.
- **Phép so**, với mỗi cấu hình INS và G2:
  - RCE35 − EOHZ35 (chính);
  - RCE35 − EO35;
  - EOHZ35 − EO35;
  - SHRC − SHHZ (chính, Shadow);
  - SHRC − SHF;
  - SHHZ − EOHZ35;
  - RCE35 − U.
- **Chỉ số:** served, visRev60 (đảo hứa), pDispPct (P(dời do điều phối) % khách phục vụ), late60First, TV5 (phút),
  breachLedger.
- **Quy tắc của kiểm toán:**
  - **FLIP** (trung bình theo ngày đổi dấu giữa hai chế độ): bỏ kết luận đó;
  - **SAME-SIGN**: báo cả hai độ lớn;
  - **ZERO** (một chế độ bằng 0): ghi rõ.
- **Báo thêm:** số dòng giống hệt giữa hai chế độ, theo từng nhánh.

## 5. Giới hạn đã biết trước

- 10 ngày là lát nhỏ: dấu của một hiệu nhỏ có thể đổi do nhiễu. Báo cả số ngày.
- Kết quả là về **bộ mô phỏng** ở chế độ adapter. Cầu nối Manhattan (227 ô-nhánh) mới là thứ nối chế độ này với RideBound
  thật, và nó chỉ phủ ô nhỏ.
- G2 trên ô Via dùng 8 xe nhận (cài đặt của công ty). Giới hạn này chưa được bắc cầu ở cỡ 62 xe.
