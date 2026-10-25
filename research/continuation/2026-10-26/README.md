# Bộ script nghiên cứu tháng 10/2026 (Via, Elmi, cầu nối, hòa vốn)

Thư mục này mang các script, cấu hình và kết quả nhỏ của đợt nghiên cứu 22–26/10/2026 lên nhánh
`research/via-zone-2026-10-26`, để máy khác (máy công ty) pull về và chạy tiếp. Quyết định và kết quả chính ghi ở
`docs/18-status-and-decision-log.md` (ADR-080 tới ADR-085). Mọi kết quả ở đây là **thăm dò**, không phải xác nhận.

## Nhánh này có gì

- **Mã C# và adapter Python**: toàn bộ chuỗi nhánh nghiên cứu `research/via-ref` → `via-core` → `g2-core` → `p1-fix` →
  `p1-dwell` (ADR-080 tới ADR-083). Nhánh này đặt trên `research/p1-dwell` (8ce0194) và không đổi dòng code nào.
- **`kits/`**: bản chép các bộ script từ thư mục làm việc `E:/Code/Report_INT3508/ridebound-scratchpad/` (chép bằng
  `kits/via-real-2026-10-26/copy_kits.py`; danh sách tệp, kích thước và sha256 ở `kits/COPY-MANIFEST.tsv`).

| Thư mục | Nội dung | Kết quả chính |
|---|---|---|
| `kits/via-bridge-2026-10-22/` | Cầu nối Manhattan: RideBound thật so với bộ mô phỏng, cấu hình luật (U, RCE35, EOHZ35, G2-*) | 55/55, rồi Elmi, G2 |
| `kits/g2-2026-10-23/` | Bộ chạy G2 (dời khách đang chờ sang xe khác), patch adapter | ADR-081 |
| `kits/bridge-phase1-2026-10-23/` | Cầu nối giai đoạn 1: hạn chót chặt, G2 giới hạn xe nhận, 128 yêu cầu, dwell, bản sửa fix1 | tổng 238 cặp kịch bản–luật trùng từng khách |
| `kits/p5-via-adapter-2026-10-25/` | P5 (lát Via chế độ adapter), N1 (dung sai hàng rào), N2 (đường cong α) | 840/840 dòng trùng công ty |
| `kits/p7-tlc-2026-10-25/` | Đối chiếu dữ liệu Via với tệp gốc TLC (DuckDB SQL) | 0/0 dòng lệch, 2 tháng |
| `kits/identity-2026-10-25/` | Kiểm đồng nhất RC = NC = OC khi chỉ chèn | 11/11 ô |
| `kits/breakeven-2026-10-26/` | Phép hòa vốn: luật C đáng dùng khi một lần đảo hứa khó chịu ít nhất bằng w\* lần bị điều phối dời muộn quá 1 phút | G2, α 35%, cùng số khách: w\* ≈ 0,94 (khoảng 0,46–1,61) |
| `kits/via-real-2026-10-26/` | RideBound thật trên dữ liệu Via ở cỡ Via (509 yêu cầu, 62 xe), gộp điểm theo khu TLC | xem `VIA-LOG.md` |
| `kits/tang3/` | Phần của tang3 mà các kit trên gọi tới: kiểm manifest runner, bộ trích khách, lớp bọc world, cấu hình WP4 | — |

## Nhánh này KHÔNG có gì (cố ý)

- **Dữ liệu và kết quả chạy lớn**: nhật ký job (transcript, mỗi job 90–400 MB), thư mục `runs/`, tệp TLC parquet,
  bản build `bin/`. Chúng nằm ở `E:/RideBoundData/` và `C:/RideBoundData/` của máy nhà. GitHub từ chối tệp trên 100 MB.
- **Bộ mô phỏng wf15 của công ty** (`emulator-wf15`, `sp/`): bản gốc ở máy công ty; script ở đây chỉ trỏ tới đường dẫn.
- **Tệp nhật ký `.log`, bản sao lưu `.bak`, tệp khóa và trạng thái đang chạy.**

## Chạy lại ở máy khác

1. Các script ghi đường dẫn tuyệt đối của máy nhà (`E:/RideBoundData/...`, `C:/RideBoundData/...`,
   `C:/RideBoundContinuation/2026-10-23/emulator-wf15`, `C:/Code/RideBound-p1fix`). Sửa các hằng số đường dẫn ở đầu mỗi
   script cho khớp máy mới.
2. Dữ liệu:
   - FleetPy Manhattan: Zenodo 15187906 (Engelhardt & Dandl 2025).
   - Chuyến Via: TLC High Volume FHV 2019 (`hvfhs_license_num` = HV0004), dựng lại bằng `kits/p7-tlc-2026-10-25/`
     (quy tắc `rid` v2).
   - Ô Via P512 và bộ mô phỏng: lấy từ máy công ty.
3. Python: môi trường FleetPy 1.0.2 (`python -B` cho driver, `python -I` cho script đọc dữ liệu). Node cho script `.js`.
4. Runner: build `src/RideBound.Runner` (Release) từ chính nhánh này; manifest của runner đã dùng nằm trong các kit
   (`*.sha256.tsv`).
5. Mỗi kit có tệp log/kết quả riêng (`*-LOG.md`, `*-RESULT.md`) ghi thứ tự chạy, cổng kiểm và các sửa đổi.

## Lưu ý

- Repo `RideBound-Code` đang **công khai**: mọi thứ ở đây ai cũng xem được.
- Kết quả Via là của bộ mô phỏng trừ phần `via-real-2026-10-26`; cầu nối mới kiểm bộ mô phỏng khớp RideBound ở cỡ nhỏ
  (tới 128 yêu cầu) và, nếu `via-real` qua, ở cỡ Via với điểm gộp theo khu.
