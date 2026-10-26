// Kiểm máy nhà đã có đủ đầu vào (từ bản chép 23/10) trước khi chạy lại.
// Cách dùng: node check_inputs.js [SP]   (mặc định E:/RideBoundData/company-intake/2026-10-23/sp)
'use strict';
const fs = require('fs'), path = require('path');
const SP = process.argv[2] || 'E:/RideBoundData/company-intake/2026-10-23/sp';
const NEED = [
  // [đường dẫn trong SP, cần cho, gói của bản 23/10]
  ['wf14/scenarios/weekdays-P512', 'ô F14 và D30/D10 (mọi phép chạy)', 'P4'],
  ['wf14/results/main.ndjson', 'cổng so với wf14', 'P3'],
  ['wf14/results/sys.ndjson', 'cổng CL (dự báo) so với wf14', 'P3'],
  ['wf15/frozen/shadow-params.json', 'ngưỡng Shadow x* 72/71', 'P3'],
  ['wf15/work/prior_prevhour.csv', 'prior λ của Shadow (ngày wf15)', 'P3'],
  ['wf15/work/zones.csv', 'bảng khu', 'P3'],
  ['wf15/work/days_all.csv', 'danh sách ngày wf15', 'P3'],
  ['wf15/results/confirm/main.ndjson', 'cổng g1 so với wf15', 'P3'],
  ['wf7/c-forecast/results/accuracy-index-2019.json', 'chỉ mục dự báo (CL, PAD)', 'P2'],
  ['wf5/data/nyc-via/fleetpy-manhattan', 'mạng FleetPy Manhattan', 'P0'],
  ['wf5/ds-nyc-via/scripts/nyclib.js', 'thư viện sinh ô', 'P0'],
  ['wf5/ds-nyc-via/scripts/network.js', 'thư viện mạng', 'P0'],
  ['wf6/via-many-days/work/factors.json', 'hệ số giao thông (sinh ô F16)', 'P0'],
  ['wf6/via-many-days/work/ff_apsp_u16.bin', 'ma trận đi tự do (sinh ô F16)', 'P0'],
  ['wf6/data/via-many-days/via_mn_2019-09.parquet', 'chuyến Via đã trích (sinh ô F16)', 'P0'],
  ['wf6/data/via-many-days/via_mn_2019-12.parquet', 'chuyến Via đã trích (sinh ô F16)', 'P0'],
  ['wf12/r5-data-days/work/day_status.json', 'cổng G-EXP (số yêu cầu mỗi khung)', 'P3'],
  ['tools/duckdb-v1.5.5-sd/duckdb.exe', 'DuckDB (giải nén từ zip trong P0)', 'P0'],
  ['wf16/a5-steering/results/robust.ndjson', 'cổng wf17 R4/R5 (tùy chọn)', 'P3'],
];
let ok = 0;
for (const [p, why, pack] of NEED) {
  const has = fs.existsSync(path.join(SP, p));
  if (has) ok++;
  console.log(`${has ? 'CÓ  ' : 'THIẾU'}  ${p}  — ${why} [${pack}]`);
}
console.log(`\n${ok}/${NEED.length} đầu vào có mặt trong ${SP}.`);
process.exit(ok === NEED.length ? 0 : 1);
