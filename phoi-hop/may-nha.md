# Máy nhà (phụ)

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
