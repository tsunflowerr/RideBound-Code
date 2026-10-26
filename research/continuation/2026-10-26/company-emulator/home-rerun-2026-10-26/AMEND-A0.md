# wf22-home sửa đổi A0: đầu vào mạng FleetPy (26/10/2026)

Ghi trước khi chép; `PLAN.md` không đổi.

## Phát hiện

Lần chạy `wf20/fullsys/RUNBOOK.sh f14n` đầu tiên dừng sau 0,17 giây, trước khi chạy lượt nào, vì thiếu module (log:
`wf22-home/logs/fullsys-f14n.log`). Lượt F14 cũng cần mạng FleetPy đầy đủ cho điểm chia sẻ: `code/runner/netfull.js`
dòng 11-15 đọc `wf5/ds-nyc-via/scripts/{network,nyclib}.js`, `wf5/data/nyc-via/fleetpy-manhattan/`,
`wf6/via-many-days/work/ff_apsp_u16.bin`. `PLAN.md` ghi sai rằng các tệp này chỉ cần cho sinh ô F16. Không có hàng nào
được ghi (`results/rows.ndjson` chưa tồn tại).

## Sửa đổi

Chép 7 tệp, giữ thời gian sửa (`cp -p`), từ `E:/RideBoundData/research/p5-via-adapter-v1/sp/` vào đúng đường dẫn tương
đối trong SP. Chỉ chép khi đích chưa có. Băm của bản ở máy nhà trùng hoàn toàn băm mà máy công ty đã ghi:

| Tệp | Băm (đã ghi = máy nhà) | Nơi ghi |
|---|---|---|
| `wf5/ds-nyc-via/scripts/network.js` | `5c3bfdcc…a8f7ad8e` | `wf15/PREREG-SHADOW-HASH.txt:65` |
| `wf5/ds-nyc-via/scripts/nyclib.js` | `e00fec4d…2a47095d` | `wf15/PREREG-SHADOW-HASH.txt:66` |
| `wf6/via-many-days/work/ff_apsp_u16.bin` | `4ae39087…7c48e915` | `wf15/PREREG-SHADOW-HASH.txt:67` |
| `…__base__nodes.csv` | `ccc68c06…f26901fe` | `wf15/DEVIATIONS.md:81` |
| `…__base__edges.csv` | `2d5eeacb…a8332674` | `wf15/DEVIATIONS.md:82` |
| `…__node_zone_info.csv` | `e887c89d…2c83859b` | `wf15/DEVIATIONS.md:83` |
| `…__general_info.csv` | `0f74569d…bb383f12` | `wf15/DEVIATIONS.md:84` |

Băm đầy đủ: xem `AMEND-A0.inputs.sha256`, dùng với `sha256sum -c` ngay trong SP sau khi chép.

`network.load` chỉ ghi `ff_apsp_u16.bin` khi tệp này thiếu hoặc sai cỡ (`network.js` dòng 53-63). Vì tệp đã có, lượt chạy
không ghi gì vào đây.

`factors.json` không cần cho lượt F14/F16 của `run_sh.js`. Nó chỉ được `gen17.js` dùng, và `runner/lib.js` dòng 9 trỏ tới
một tệp `wf2` khác, không thuộc wf6. Nếu lượt chạy đòi thêm tệp nào, dừng và ghi sửa đổi mới.

Sau A0, chạy lại đúng `f14n` theo `PLAN.md` S1.
