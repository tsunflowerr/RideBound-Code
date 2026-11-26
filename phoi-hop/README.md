# Phối hợp hai máy trên nhánh `scope/thesis-trim`

Nhánh này thu gọn RideBound về đúng phạm vi khóa luận mà **bảng từng khách không đổi**. Hai phiên AI cùng làm, trao
đổi qua các file trong thư mục này:

| Máy | Vai trò | Ghi vào |
|---|---|---|
| Máy công ty | **chính**: tìm nguyên nhân, quyết định sửa gì, sửa code | `may-cong-ty.md` |
| Máy nhà | **phụ**: chạy lại theo yêu cầu (test, `kiem_nhanh.sh`, lượt C# + FleetPy), tìm hiểu phụ, báo kết quả | `may-nha.md` |

## Quy tắc

1. Trước khi làm: `git pull --rebase`. Sau khi làm: commit rồi push ngay, để bên kia thấy.
2. Mỗi bên chỉ ghi vào file của mình; thêm mục mới **ở đầu file**, có ngày giờ và commit liên quan.
3. Yêu cầu gửi bên kia: một mục `## YÊU CẦU <số>` trong file của mình, nói rõ chạy gì trên commit nào và cần trả về gì.
   Trả lời: mục `## TRẢ LỜI <số>` trong file của bên kia trả lời.
4. Không đưa vào file được commit: đường dẫn máy, tên công ty, e-mail. Kết quả lớn (transcript, bảng từng khách) để
   ngoài repo; trong file chỉ ghi số đếm, sha256, dòng khác đầu tiên.
5. Không sửa `dev`/`main`, không force-push. Thông điệp commit theo kiểu `[10.xx] ...`.

## Tiêu chí "kết quả không đổi"

- `riders.py` của mỗi lượt sau khi sửa giống **từng byte** bảng mốc chuẩn (code `ab697fe`) của cùng lượt.
- `bash src/fastsim/sample/kiem_nhanh.sh <thư mục mới>`: `36 rows equal, 0 differ, 0 without an expected row`.
- Toàn bộ test C# (`dotnet test src/core/RideBound.slnx -c Release`) và test Python của adapter qua.
- Hash quyết định/trạng thái/checkpoint được phép đổi nếu do gỡ trường; phải ghi lại.

Lượt mốc chuẩn: ô `d20190919-vf-r0-w08` và `-w17`, cấu hình `FS-G2-SHHZ71-N` và `FS-G2-SHRC71-CL` (README mục 2.2).
