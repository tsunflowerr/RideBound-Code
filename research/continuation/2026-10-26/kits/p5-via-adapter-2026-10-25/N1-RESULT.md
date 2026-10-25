# N1: dung sai của hàng rào chặt, kết quả (25/10/2026, 06:40). Thăm dò, sau kiểm toán

Thiết kế ghi trước: `N1-PLAN.md` (kèm sửa đổi A1). Dữ liệu: 60 ô của P5, chế độ plain, điểm Shadow thật, x\* đóng băng.
- Kiểm tra dung sai 0: **240/240 dòng giống hệt P5 plain**.
- Ba mức 1 s, 5 s, 60 s: mỗi mức 120 việc, 0 lỗi.
- Bảng đầy đủ: `N1-TOL-TABLE.tsv`, giá trị ngày như P5.

## Kết quả

| Cấu hình | Dung sai | served SHF | SHRC − SHF served | đảo hứa SHF (SHRC) | khách bảo đảm bị đổi giờ/ngày, SHF |
|---|---|---|---|---|---|
| INS | 0 | 679,5 | +35,0 | 27,7 (29,6) | 2,5 |
| INS | 1 s | 690,9 | +23,6 | 28,9 | 3,1 |
| INS | 5 s | 698,4 | +16,1 | 28,6 | 7,3 |
| INS | 60 s | 725,4 | **−11,0** | 30,5 | 43,3 |
| G2 | 0 | 689,9 | +29,4 | 48,6 (36,7) | 21,8 |
| G2 | 1 s | 705,7 | +13,6 | 54,3 | 34,5 |
| G2 | 5 s | 709,3 | +10,0 | 51,8 | 37,0 |
| G2 | 60 s | 735,9 | **−16,6** | 53,2 | 65,8 |

Tham chiếu (P5 plain), số khách bảo đảm bị đổi giờ mỗi ngày: SHRC 0 / 0; SHHZ 2,7 (INS) / 29,1 (G2).

**EO35 (Ordinary, hàng rào chặt):**
- dung sai 1–5 s gần như không đổi gì: khoảng cách tới EOHZ35 về served giảm từ 2,9 xuống 1,4 (INS) và từ 1,4 xuống 0,4
  (G2);
- dung sai 60 s làm EO35 phục vụ nhiều hơn EOHZ35 11,9 (INS) và 10,2 (G2), đổi lại nhiều khách trễ quá 60 s hơn (264,6 so
  với 232,3).

## Đọc kết quả

1. **Lợi thế phục vụ của SHRC so với SHF chủ yếu là artefact của hàng rào chặt.**
   - Dung sai 1 s xóa một phần ba (INS) tới một nửa (G2) khoảng cách;
   - 5 s xóa một nửa tới hai phần ba;
   - 60 s làm khoảng cách đảo dấu.
   - Đúng như kiểm toán (KT:87, 303). Tỷ lệ ở đây đo trên điểm Shadow thật, khác phép thử 8 ô với phân bổ ngẫu nhiên của
     kiểm toán (35%/63% INS, 38% G2 ở 5 s).
2. **Hàng rào chặt có dung sai 60 s phục vụ nhiều hơn cả SHHZ và SHRC**, nhưng điều phối dời giờ của 43 (INS) tới 66 (G2)
   khách bảo đảm mỗi ngày, so với 0 của SHRC. Các biến thể nằm trên **một đường đánh đổi** giữa số khách phục vụ và số khách
   bảo đảm bị dời; không biến thể nào hơn hẳn (KT:305, §7-W7, W10).
3. **Ưu thế ít đảo hứa của luật C khi gán lại giữ ở mọi mức dung sai:** SHRC ít hơn SHF 11,9–17,6 lần mỗi ngày.
4. **Giới hạn:** 10 ngày đã xem, chế độ plain, một cấu hình, không kiểm định.
