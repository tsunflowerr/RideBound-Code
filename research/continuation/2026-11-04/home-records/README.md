# Biên bản máy nhà, tối 04/11/2026 (bước A2, A3, B4 của `KE-HOACH-MAY-NHA.md`)

Chỉ đọc và kiểm; không chạy job, không sửa gói `final-v2`, không xóa gì. Công cụ (ngoài repo, theo quy ước để script ở
scratchpad): `ridebound-scratchpad/fastrun-2026-11-02/final/{a3_inventory.py, b4_dry_check.py}`.

## A2. Gói và biên bản Runner

| Mục | Kết quả |
|---|---|
| Gói | `ridebound-final-v2.zip` sha256 `467c735beb7276fdba598dfa1a71616c1a69984fcbc73ea30667a02cd9c3e6cb`; CRC mọi mục đạt; 66.545 mục = `MANIFEST.tsv` (`987f3179…`) = tệp gốc (sha256 từng tệp) |
| Mã trong gói so với GitHub | Commit xuất `0eb9a6ca` (repo xuất riêng, tác giả `ridebound-export`, không có trên GitHub). `src`, `simulators`, `tests`: 498/498 tệp trùng blob với commit công khai `9ba5568`. Toàn cây: trùng `9ba5568`, trừ `research/continuation/2026-10-26/company-emulator/` và `research/continuation/2026-10-15/thesis-v5-deployment.json` (loại theo thiết kế) và `docs/18`, `docs/19` (sửa sau khi xuất) |
| Runner build từ đâu | Đường dẫn PDB trong DLL: `E:\RideBoundData\final-v2\repo\src\RideBound.Runner\obj\Release\net10.0\RideBound.Runner.pdb` → build từ chính repo xuất. (Runner cũ `runner-gd4-v1`: `E:\Code\RideBound-fastrun\src\...` — lỗ hổng của `final-v1`) |
| Lệnh build | `dotnet publish src/RideBound.Runner -c Release -m:1 -nodeReuse:false -p:UseSharedCompilation=false` (`BUILD.json`); mã kiểm kê repo trước = sau build = `271166f4…` |
| SDK | Không ghi lúc build. Suy ra: `global.json` ghim `10.0.301` (`rollForward: latestPatch`), máy chỉ cài SDK `10.0.301` |
| Cây Runner | 429 tệp; digest danh sách sha256 `ae7702528d376fe429f93a4e3efe1ed2cce4a5f9ed7717ac811ff10ff8f9d741` (`RUNNER-TREE-final-v2.sha256`); `RideBound.Runner.dll` `0f324542…`, `deps.json` `a5b21386…`, `runtimeconfig.json` `c230a317…` (khung `Microsoft.NETCore.App` 10.0.0) |
| .NET khi chạy | Đi kèm gói: `final-v2/dotnet/dotnet.exe` (sha256 `a5ccdc3a…`), chỉ có `Microsoft.NETCore.App 10.0.9`; `run_queue_final.py` gọi đúng tệp này, không dùng `dotnet` của máy |
| Môi trường job (`run_queue_final.py`, launcher) | `RB_REALJOB_TIMEOUT_S=900` (adapter mặc định 60 s); `RB_REALJOB_BUFFERED_READ=1`; `RB_VIA_MAX_OUTPUT_LINE_BYTES` = `RB_VIA_MAX_INPUT_LINE_BYTES` = 1 GiB (đủ cho bản chốt ~768 MB, ảnh chụp ~130 MB); `DOTNET_GCHeapHardLimit=0x180000000` (6 GiB); `RIDEBOUND_STATE_HASH` không đặt ở đâu |

## A3. Những gì nằm ngoài repo

- `FINAL-INPUTS-from-MANIFEST.sha256`: 450 tệp (cấu hình chính sách và WP4 v2, danh sách job, driver chạy, world, mạng
  Shadow, dự đoán và các seal), sha256 lấy từ `MANIFEST.tsv`.
- `a3-inventory.json` (2.940 tệp JSON trong dữ liệu F14, bộ công cụ `fastrun-2026-11-02`, công cụ của gói, `gate-g3a`,
  `shadow-gate`):
  - khóa `fixedFreeze`, `repair`, `multiplePlan`, `maximumRidersPerVehicle`, `stopServiceDurationMs`, `freezeHorizon`,
    `dropEtaDeadlineTwoSided`: 0; `solverExecutionEvidenceProfile`: 4, đều trong bản sao repo dở
    `docker/context.failed-maxpath-1` (không phải cấu hình chạy); `stopDistances` không rỗng: 0; `origin-hold`: 0;
  - mỗi nhánh Shadow có đúng một khóa 1000‰ (`SHRC71`: `dropEtaBudgetSlackPermille`; `SHHZ71`:
    `dropEtaDeadlineSlackPermille`), cả v1 lẫn v2 (sửa 2 của ADR-090);
  - 336 cấu hình WP4 có Shadow: `earliest-feasible`, `intervalMs`/`binMs` ≥ 60 000, ô dự báo ≥ 60 000 → 0 vi phạm
    (sửa 5–7 của ADR-090);
  - script ngoài repo không gọi `restore(`, `restart`, `RIDEBOUND_STATE_HASH` (các dòng trúng là danh sách ký hiệu của chính
    `a3_inventory.py` hoặc bản sao repo dở).
- Bộ sinh dự đoán adapter-mode được lưu nguyên ở `E:/RideBoundData/research/fullsys-f14-v2/emulator-archive-2026-11-04/`
  (50 tệp: toàn bộ `emulator-wf15` — khớp `MANIFEST.sha256` của nó —, `engine_wf15_clint.js` `26d6a886…` (= dòng trong seal
  `FULLSYS-F14-PREDICTIONS`), `emulate_via_full.js` `a9a1dad2…`, `emulator-wf15/engine.js` `908a2961…`, các script sinh dự
  đoán và các seal): `EMULATOR-ARCHIVE.SHA256SUMS` (digest `3b093ced…`). Mã công ty mà bộ sinh cần (wf20 `fullsys/code`, tham số
  Shadow đóng băng, panels, chỉ mục dự báo, prior) không chép thêm; chỉ ghi sha256: `EMULATOR-ARCHIVE.COMPANY-DEPENDENCIES.sha256`.

## B4. Kiểm trước 168 job (không chạy)

`b4-dry-check.json`: **đạt**. 168 job, 84 ô, mỗi ô đủ 2 nhánh; 758 tệp đầu vào (cấu hình, driver, world, 5 tệp fixture mỗi
ô) có đúng sha256 trong `MANIFEST.tsv`; Runner của gói nạp 168/168 cặp cấu hình (thoát 0 với đầu vào rỗng; mốc kiểm: một
bản hỏng `networkNodes` thoát 64 với thông báo đúng); `networkNodes` của 84 ô × 2 nhánh khớp nút của fixture (811–879 nút
mỗi ô; mã nút sinh bằng `FleetPyProtocolMapper.node_id` của adapter); tham số Shadow, prior λ và hồ sơ dự báo trùng đầu vào
Shadow đã khóa.

Giới hạn: phép kiểm `networkNodes` dùng cùng quy tắc xếp nút như `loadCell` của bộ mô phỏng. Việc FleetPy đánh số nút theo
đúng thứ tự đó mới được chứng minh trên 2 ô cổng (khung và điểm Shadow trùng); bước 3 trên máy thuê kiểm thêm.
