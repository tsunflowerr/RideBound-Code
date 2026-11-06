#!/usr/bin/env node
// Our whole system against the competitor's, in the plain emulator: the final report's "plain emulator" column (§5:
// served -8.84, reversals -27.46, late -34.04, TV5 -38.11 for G2) and its plain split (§6), and thesis §4.7.
// Rebuilds OURS-VS-THEIRS.md from the recorded wf20 fullsys emulator rows. Nothing is rerun.
//
// The rows hold one emulator run per cell x config x promise mode x arm. A cell is one day (30 days: F14 has 14,
// F16 has 16) x one window (w08, w17) x one resample (r0, r1, r2). This script uses:
//   configs   G2         insertion, plus moving at most one waiting rider to one of the 8 nearest vehicles per decision
//             INS        insertion only (the mode of the real RideBound runs)
//   sides     ours       SHRC, mode CL: Shadow + Rule C + closed-loop forecast
//             theirs     SHHZ, mode N:  Shadow + Elmi's relaxed fence, no forecast
//             reference  SHF,  mode N:  Shadow + the strict fence, no forecast
//   The rows name the arm with its Shadow share (SHRC71, SHHZ71, SHF72, ...); the share is dropped here.
// Both sides use the same dispatch config. The split of ours - theirs is exactly additive by day:
//   ours - theirs = (SHRC_CL - SHHZ_CL) [Rule C part] + (SHHZ_CL - SHHZ_N) [forecast part]
//
// Statistics. The unit is the day:
//   X of a window = mean of X over the 3 resamples; X of a day = X(w08) + X(w17). The rows must cover exactly 30 days,
//   and every config x mode x arm used here needs 3 resamples in each window of each day; otherwise the script stops.
//   A rate is computed per day (count * 1000 / served of that day; per window for the window tables);
//   a level is the mean over the 30 days.
//   A contrast A - B pairs the two sides by day: mean, 95 % t interval with the fixed quantile t(0.975, 29) = 2.045
//   (report_tables.js uses the unrounded 2.0452296...), days with a difference > 0 / < 0 (|d| > 1e-9), and the exact
//   two-sided sign test with the ties dropped.
// Metrics (the output labels are Vietnamese, as quoted in the reports):
//   Phục vụ                     served riders per day
//   Đảo hứa                     riders whose shown promise moved >= 60 s one way, then >= 60 s the other way; per day
//   Trễ >60 s so với hứa đầu    riders arriving > 60 s after their first promise, per 1000 served
//   Bị điều phối dời giờ        riders that dispatch pushed later by >= 5 s at least once, per 1000 served
//   Vi phạm hạn chót Elmi       riders arriving > 60 s after Elmi's deadline (first promise + budget), per 1000 served
//   TV5                         total promise movement, 5 s deadband, in s per served rider
//   Khách bảo đảm trễ           guaranteed-class riders arriving > 60 s after their first promise, per day
//   Vượt sổ luật C              riders whose summed promise changes exceed their Rule C budget + 1 s, per 1000 served
// Numbers use a decimal comma and 2 decimals (|x| < 0.005 prints as 0,00); p uses 3 decimals, or one significant
// digit in exponent form below 0.001 (2e-9).
// The output text names its source by the original path wf20/fullsys/results/rows.ndjson; that file is shipped as
// data/wf20-fullsys-rows.ndjson.gz.
//
// Usage: node ours_vs_theirs.js --rows <rows.ndjson[.gz]> --out <file.md>
//   Writes <file.md> and prints the same text. expected/ours-vs-theirs.md is the stored output.
'use strict';
const fs = require('fs'), zlib = require('zlib');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
if (!arg('rows') || !arg('out')) {
  throw new Error('usage: node ours_vs_theirs.js --rows <rows.ndjson[.gz]> --out <file.md>');
}
const readText = file => { const raw = fs.readFileSync(file); return (file.endsWith('.gz') ? zlib.gunzipSync(raw) : raw).toString('utf8'); };
const rows = readText(arg('rows')).split('\n').filter(Boolean).map(JSON.parse);

// ---------------------------------------------------------------- per-day values
const base = a => a.replace(/\d+$/, '');   // SHRC71 -> SHRC
// the fields of a row that the metrics below use
const F = ['served', 'visRev60', 'late60First', 'pDisp', 'breachDeadline60', 'sumTV5', 'G_late60First', 'breachLedger'];
// config|mode|arm|day -> the rows of that day (2 windows x 3 resamples)
const idx = new Map();
for (const r of rows) {
  const k = [r.config, r.mode, base(r.arm), r.day].join('|');
  if (!idx.has(k)) idx.set(k, { panel: r.panel, cells: [] });
  idx.get(k).cells.push(r);
}
const days = [...new Set(rows.map(r => r.day))].sort();
if (days.length !== 30) throw new Error('expected 30 days, got ' + days.length);
// the fields F of one config x mode x arm on one day: { w08, w17, day }, each window the mean of its 3 resamples
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
// the metrics: [output label, value of a window or a day]
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

// ---------------------------------------------------------------- statistics
function binom(n, k) { let r = 1; for (let i = 1; i <= k; i++) r = r * (n - k + i) / i; return r; }
// exact two-sided sign test: np days above 0, nm days below 0
function signP(np, nm) {
  const n = np + nm; if (!n) return 1;
  const k = Math.min(np, nm); let s = 0; for (let i = 0; i <= k; i++) s += binom(n, i);
  return Math.min(1, 2 * s / Math.pow(2, n));
}
const T29 = 2.045;   // t quantile 0.975 with 29 degrees of freedom (30 days)
// summary of the per-day differences d: mean m, 95 % interval [lo, hi], days above / below 0, sign-test p
function stat(d) {
  const n = d.length, m = d.reduce((a, b) => a + b, 0) / n,
    sd = Math.sqrt(d.reduce((a, b) => a + (b - m) ** 2, 0) / (n - 1)), se = sd / Math.sqrt(n);
  const np = d.filter(x => x > 1e-9).length, nm = d.filter(x => x < -1e-9).length;
  return { m, lo: m - T29 * se, hi: m + T29 * se, np, nm, p: signP(np, nm) };
}
// number format of the tables: decimal comma; f(x) prints 2 decimals, fp(p) a p value
const f = (x, k = 2) => (Math.abs(x) < 0.005 && k === 2 ? 0 : x).toFixed(k).replace('.', ',');
const fp = p => p < 0.001 ? p.toExponential(0).replace('.', ',') : f(p, 3);
// mean over the days of each metric for one config x mode x arm; scope is 'day', 'w08' or 'w17'
function levels(config, mode, arm, scope) {
  const v = M.map(([, g]) => days.reduce((s, d) => s + g(agg(config, mode, arm, d)[scope]), 0) / days.length);
  return v;
}
// A - B by day for each metric, A and B as [config, mode, arm]: [[label, stat], ...]
function contrast(A, B, scope) {
  return M.map(([name, g]) => [name, stat(days.map(d => g(agg(...A, d)[scope]) - g(agg(...B, d)[scope])))]);
}

// ---------------------------------------------------------------- OURS-VS-THEIRS.md
let out = '# Hệ của mình (Shadow + luật C + dự báo) so với hàng rào nới + Shadow (không dự báo)\n\n';
out += 'Thăm dò sau kiểm toán, tính lại từ wf20/fullsys/results/rows.ndjson (30 ngày F14+F16, 3 lần lấy mẫu); không chạy thêm. ';
out += 'Ngày = khung 8 giờ + khung 17 giờ, mỗi khung lấy trung bình 3 lần lấy mẫu. Hiệu = của mình − của đối thủ; [khoảng tin cậy 95% theo t]; (số ngày hiệu dương / âm; p của phép kiểm dấu chính xác).\n';
for (const cfg of ['G2', 'INS']) {
  out += `\n## Chế độ ${cfg === 'G2' ? 'G2 (gán lại)' : 'INS (chỉ chèn, chế độ thật của RideBound)'}\n\n### Mức mỗi ngày (trung bình 30 ngày)\n\n`;
  const arms = [
    ['Của mình: SHRC + dự báo', [cfg, 'CL', 'SHRC']],
    ['Đối thủ: SHHZ, không dự báo', [cfg, 'N', 'SHHZ']],
    ['Tham khảo: SHF (hàng rào chặt), không dự báo', [cfg, 'N', 'SHF']]
  ];
  out += '| Chỉ số | ' + arms.map(a => a[0]).join(' | ') +
    ' |\n|---|' + arms.map(() => '---').join('|') + '|\n';
  const L = arms.map(a => levels(...a[1], 'day'));
  M.forEach(([name], i) => {
    out += `| ${name} | ${L.map(l => f(l[i])).join(' | ')} |\n`;
  });
  // ours - theirs, for the whole day and for each window
  for (const scope of ['day', 'w08', 'w17']) {
    out += `\n### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), ${scope === 'day' ? 'cả ngày' : scope === 'w08' ? 'khung 8 giờ' : 'khung 17 giờ'}\n\n| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |\n|---|---|---|---|---|\n`;
    for (const [name, s] of contrast([cfg, 'CL', 'SHRC'], [cfg, 'N', 'SHHZ'], scope))
      out += `| ${name} | ${f(s.m)} | [${f(s.lo)}; ${f(s.hi)}] | ${s.np}/${s.nm} | ${fp(s.p)} |\n`;
  }
  // the split: total = Rule C part + forecast part
  out += '\n### Tách phần: luật C và dự báo (cả ngày)\n\n| Chỉ số | Tổng (của mình − đối thủ) | Phần luật C (SHRC+dự báo − SHHZ+dự báo) | Phần dự báo (SHHZ+dự báo − SHHZ) |\n|---|---|---|---|\n';
  const tot = contrast([cfg, 'CL', 'SHRC'], [cfg, 'N', 'SHHZ'], 'day'),
    rc = contrast([cfg, 'CL', 'SHRC'], [cfg, 'CL', 'SHHZ'], 'day'),
    fc = contrast([cfg, 'CL', 'SHHZ'], [cfg, 'N', 'SHHZ'], 'day');
  tot.forEach(([name, s], i) => {
    out += `| ${name} | ${f(s.m)} (${s.np}/${s.nm}) | ${f(rc[i][1].m)} | ${f(fc[i][1].m)} |\n`;
  });
  // ours - the strict-fence reference
  out += '\n### So thêm với hàng rào chặt (SHRC+dự báo − SHF), cả ngày\n\n| Chỉ số | Hiệu | Khoảng tin cậy | Ngày +/− | p |\n|---|---|---|---|---|\n';
  for (const [name, s] of contrast([cfg, 'CL', 'SHRC'], [cfg, 'N', 'SHF'], 'day'))
    out += `| ${name} | ${f(s.m)} | [${f(s.lo)}; ${f(s.hi)}] | ${s.np}/${s.nm} | ${fp(s.p)} |\n`;
}
fs.writeFileSync(arg('out'), out);
console.log(out);
