# Nhật ký P5

Các sửa đổi so với protocol đã niêm phong (`P5-SEAL.sha256`, 00:33:17) được ghi ở đây. Không sửa tệp đã niêm phong.

## A1: 00:33, thiếu tệp provenance của ô

**Chuyện gì xảy ra.** Lần chạy plain đầu (`rows/plain.ndjson.*`) dừng ở cả 120/120 việc, trước mọi mô phỏng:
`ENOENT …/cells/provenance/<cell>-n512.prov.json`. Runner đọc tệp provenance mà ô trỏ tới (`shcore.js:26`), còn
`prepare_p5.py` không chép thư mục này.

**Sửa.**
- `add_provenance.py` chép nguyên byte 60 tệp `provenance/<cell>-n512.prov.json` từ gói vào `cells/provenance/`, và kiểm sha.
- Không đổi ô, ma trận, mã hay phân tích.
- Chạy lại vào `rows/plain-a1.ndjson`. Tệp của lần hỏng được giữ nguyên.

**Kiểm thêm khi dựng.** Tệp APSP `ff_apsp_u16.bin` tính ở nhà có sha `4ae39087…`, khớp danh sách hash của công ty.

## Cổng 0: ĐẠT, 00:39

- Lượt plain (`rows/plain-a1.ndjson`): 120 việc, 4,6 phút, 3 luồng, 0 lỗi.
- Đối chiếu bằng `gate0_check.py`, cùng quy tắc với `compare_p5.py`: **840/840 dòng giống hệt** dòng xác nhận của công ty
  (`sp/wf15/results/confirm.ndjson`), ở mọi trường trừ `part`, `set`, `ms`, `msScore`. Ghi ở `GATE0.txt`.
- Vậy cây dựng ở nhà (mã công ty nguyên byte, mạng Zenodo ở nhà, `network.js` và `nyclib.js` của gói 20/10) tái tạo đúng
  số liệu Shadow của wf15 trên 60 ô này.
- 00:40: bắt đầu lượt adapter (`rows/adapter.ndjson`, 2 luồng).

## A2: 00:58–05:42, lượt adapter dừng theo phiên làm việc

- Lượt adapter dừng lúc 00:58, cùng lúc phiên Claude kết thúc. Lúc đó đã xong 46/120 việc (322 dòng). Không còn tiến trình
  nào chạy.
- 05:42 kiểm lại tệp: 322 dòng hợp lệ, 46 việc trọn 7 nhánh, dòng cuối kết thúc bằng xuống dòng.
- Cho runner chạy tiếp vào chính tệp đó. Runner của công ty bỏ qua việc đã có dòng kết quả (`run_sh.js` dòng 1-5). Dùng 3
  luồng.
- Số luồng không đổi kết quả, vì mỗi việc tất định. Không đổi gì khác.
