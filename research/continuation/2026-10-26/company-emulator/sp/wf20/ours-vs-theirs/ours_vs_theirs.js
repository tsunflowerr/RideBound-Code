// Post-hoc contrast asked by the owner on 26/10 (thăm dò sau kiểm toán; no new runs):
// ours = Shadow + Rule C + closed-loop forecast (SHRC, mode CL); theirs = Shadow + relaxed fence without forecast (SHHZ, mode N),
// same dispatch config on both sides. Also: strict fence SHF N as a second reference, and the split
// ours - theirs = (SHRC_CL - SHHZ_CL) [Rule C part] + (SHHZ_CL - SHHZ_N) [forecast part].
// Day = w08 + w17 of the mean over r0..r2 (same as verify-fullsys/indep.js); per 1000 = per 1000 served of that day.
'use strict';
const fs = require('fs');
const ROWS = process.argv[2];
const rows = fs.readFileSync(ROWS, 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const base = a => a.replace(/\d+$/, '');
const F = ['served', 'visRev60', 'late60First', 'pDisp', 'breachDeadline60', 'sumTV5', 'G_late60First', 'breachLedger'];
const idx = new Map();
for (const r of rows) {
  const k = [r.config, r.mode, base(r.arm), r.day].join('|');
  if (!idx.has(k)) idx.set(k, { panel: r.panel, cells: [] });
  idx.get(k).cells.push(r);
}
const days = [...new Set(rows.map(r => r.day))].sort();
if (days.length !== 30) throw new Error('expected 30 days, got ' + days.length);
function agg(config, mode, arm, day) {
  const e = idx.get([config, mode, arm, day].join('|'));
  if (!e) throw new Error('missing ' + [config, mode, arm, day]);
  const out = {};
  for (const w of ['w08', 'w17']) {
    const cs = e.cells.filter(c => c.win === w);
    if (cs.length !== 3) throw new Error('incomplete ' + [config, mode, arm, day, w]);
    out[w] = {};
    for (const f of F) out[w][f] = cs.reduce((s, c) => s + c[f], 0) / 3;
  }
  out.day = {}; for (const f of F) out.day[f] = out.w08[f] + out.w17[f];
  return out;
}
const M = [
  ['Phục vụ (khách/ngày)', X => X.served],
  ['Đảo hứa (khách/ngày)', X => X.visRev60],
  ['Trễ >60 s so với hứa đầu /1000', X => X.late60First * 1000 / X.served],
  ['Bị điều phối dời giờ /1000', X => X.pDisp * 1000 / X.served],
  ['Vi phạm hạn chót Elmi /1000', X => X.breachDeadline60 * 1000 / X.served],
  ['TV5 (giây/khách)', X => X.sumTV5 / X.served / 1000],
  ['Khách bảo đảm trễ (khách/ngày)', X => X.G_late60First],
  ['Vượt sổ luật C /1000', X => X.breachLedger * 1000 / X.served],
];
function binom(n, k) { let r = 1; for (let i = 1; i <= k; i++) r = r * (n - k + i) / i; return r; }
function signP(np, nm) { const n = np + nm; if (!n) return 1; const k = Math.min(np, nm); let s = 0; for (let i = 0; i <= k; i++) s += binom(n, i); return Math.min(1, 2 * s / Math.pow(2, n)); }
const T29 = 2.045;
function stat(d) {
  const n = d.length, m = d.reduce((a, b) => a + b, 0) / n, sd = Math.sqrt(d.reduce((a, b) => a + (b - m) ** 2, 0) / (n - 1)), se = sd / Math.sqrt(n);
  const np = d.filter(x => x > 1e-9).length, nm = d.filter(x => x < -1e-9).length;
  return { m, lo: m - T29 * se, hi: m + T29 * se, np, nm, p: signP(np, nm) };
}
const f = (x, k = 2) => (Math.abs(x) < 0.005 && k === 2 ? 0 : x).toFixed(k).replace('.', ',');
const fp = p => p < 0.001 ? p.toExponential(0).replace('.', ',') : f(p, 3);
function levels(config, mode, arm, scope) { const v = M.map(([, g]) => days.reduce((s, d) => s + g(agg(config, mode, arm, d)[scope]), 0) / days.length); return v; }
function contrast(A, B, scope) {
  return M.map(([name, g]) => [name, stat(days.map(d => g(agg(...A, d)[scope]) - g(agg(...B, d)[scope])))]);
}
let out = '# Hệ của mình (Shadow + luật C + dự báo) so với hàng rào nới + Shadow (không dự báo)\n\n';
out += 'Thăm dò sau kiểm toán, tính lại từ wf20/fullsys/results/rows.ndjson (30 ngày F14+F16, 3 lần lấy mẫu); không chạy thêm. ';
out += 'Ngày = khung 8 giờ + khung 17 giờ, mỗi khung lấy trung bình 3 lần lấy mẫu. Hiệu = của mình − của đối thủ; [khoảng tin cậy 95% theo t]; (số ngày hiệu dương / âm; p của phép kiểm dấu chính xác).\n';
for (const cfg of ['G2', 'INS']) {
  out += `\n## Chế độ ${cfg === 'G2' ? 'G2 (gán lại)' : 'INS (chỉ chèn, chế độ thật của RideBound)'}\n\n### Mức mỗi ngày (trung bình 30 ngày)\n\n`;
  const arms = [['Của mình: SHRC + dự báo', [cfg, 'CL', 'SHRC']], ['Đối thủ: SHHZ, không dự báo', [cfg, 'N', 'SHHZ']], ['Tham khảo: SHF (hàng rào chặt), không dự báo', [cfg, 'N', 'SHF']]];
  out += '| Chỉ số | ' + arms.map(a => a[0]).join(' | ') + ' |\n|---|' + arms.map(() => '---').join('|') + '|\n';
  const L = arms.map(a => levels(...a[1], 'day'));
  M.forEach(([name], i) => { out += `| ${name} | ${L.map(l => f(l[i])).join(' | ')} |\n`; });
  for (const scope of ['day', 'w08', 'w17']) {
    out += `\n### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), ${scope === 'day' ? 'cả ngày' : scope === 'w08' ? 'khung 8 giờ' : 'khung 17 giờ'}\n\n| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |\n|---|---|---|---|---|\n`;
    for (const [name, s] of contrast([cfg, 'CL', 'SHRC'], [cfg, 'N', 'SHHZ'], scope)) out += `| ${name} | ${f(s.m)} | [${f(s.lo)}; ${f(s.hi)}] | ${s.np}/${s.nm} | ${fp(s.p)} |\n`;
  }
  out += '\n### Tách phần: luật C và dự báo (cả ngày)\n\n| Chỉ số | Tổng (của mình − đối thủ) | Phần luật C (SHRC+dự báo − SHHZ+dự báo) | Phần dự báo (SHHZ+dự báo − SHHZ) |\n|---|---|---|---|\n';
  const tot = contrast([cfg, 'CL', 'SHRC'], [cfg, 'N', 'SHHZ'], 'day'), rc = contrast([cfg, 'CL', 'SHRC'], [cfg, 'CL', 'SHHZ'], 'day'), fc = contrast([cfg, 'CL', 'SHHZ'], [cfg, 'N', 'SHHZ'], 'day');
  tot.forEach(([name, s], i) => { out += `| ${name} | ${f(s.m)} (${s.np}/${s.nm}) | ${f(rc[i][1].m)} | ${f(fc[i][1].m)} |\n`; });
  out += '\n### So thêm với hàng rào chặt (SHRC+dự báo − SHF), cả ngày\n\n| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |\n|---|---|---|---|---|\n';
  for (const [name, s] of contrast([cfg, 'CL', 'SHRC'], [cfg, 'N', 'SHF'], 'day')) out += `| ${name} | ${f(s.m)} | [${f(s.lo)}; ${f(s.hi)}] | ${s.np}/${s.nm} | ${fp(s.p)} |\n`;
}
fs.writeFileSync(process.argv[3], out);
console.log(out);
