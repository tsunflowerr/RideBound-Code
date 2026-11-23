'use strict';
// Table 5.7 (ledger accounting variants) recomputed from fast-simulator rows run in the SYNCHRONISED (adapter) mode.
// usage: node table57.js <rowsDir> [<rowsDir> ...]   (each dir holds rows-*.ndjson written by emulate_adapter.js)
// Rules (same as aggregate.py / the thesis, Section 5.1.3): day = 2 windows x 3 samples; counts per day = sum / 3;
// late rate = sum(late60First) / sum(served) * 1000 within the day; paired difference per day; 95% CI with the
// t quantile of (days-1) degrees of freedom; 'Chuyen xe' = relocations / 3.
const fs = require('fs');
const dirs = process.argv.slice(2);
if (!dirs.length) { console.error('give at least one rows directory'); process.exit(2); }
const rows = new Map();
let dup = 0, sigDiff = 0;
for (const d of dirs) for (const f of fs.readdirSync(d).filter(f => /^rows-\d+\.ndjson$/.test(f))) {
  for (const l of fs.readFileSync(d + '/' + f, 'utf8').split('\n')) {
    if (!l.trim()) continue; let r; try { r = JSON.parse(l); } catch (e) { continue; }
    const k = r.cell + '|' + r.config + '|' + r.arm;
    if (rows.has(k)) { dup++; if (rows.get(k).sig !== r.sig) sigDiff++; continue; }
    rows.set(k, r);
  }
}
const ARMS = ['EOHZ35', 'NCE35', 'OCE35', 'RCE35'];
const cells = [...new Set([...rows.values()].map(r => r.cell))].sort();
const missing = []; for (const c of cells) for (const a of ARMS) if (!rows.has(c + '|G2|' + a)) missing.push(c + '|' + a);
console.log({ cells: cells.length, rowsLoaded: rows.size, duplicates: dup, duplicateSigDiffer: sigDiff, missingCellArms: missing.length });
if (missing.length) { console.log('MISSING (first 5):', missing.slice(0, 5)); }
const T975 = { 29: 2.0452296, 59: 2.0009957, 13: 2.1603687, 12: 2.1788128, 9: 2.2621572 };
const allDays = [...new Set(cells.map(c => c.slice(0, 10)))].sort();
const days = allDays.filter(d => ARMS.every(a => [...rows.values()].filter(r => r.config === 'G2' && r.arm === a && r.day === d).length === 6));
if (days.length < allDays.length) console.log('WARNING: only', days.length, 'of', allDays.length, 'days are complete for all four arms; results below use complete days only');
function perDay(arm) {
  const out = {};
  for (const d of days) {
    const rr = [...rows.values()].filter(r => r.config === 'G2' && r.arm === arm && r.day === d);
    if (rr.length !== 6) return null;
    const su = k => rr.reduce((s, r) => s + (r[k] || 0), 0); const sv = su('served');
    out[d] = { served: sv / 3, rev: su('visRev60') / 3, late: 1000 * su('late60First') / sv, reloc: su('relocations') / 3 };
  }
  return out;
}
const A = Object.fromEntries(ARMS.map(a => [a, perDay(a)]));
if (Object.values(A).some(x => !x)) { console.log('incomplete days for some arm; stop'); process.exit(1); }
const mean = a => a.reduce((s, x) => s + x, 0) / a.length;
const sd = a => { const m = mean(a); return Math.sqrt(a.reduce((s, x) => s + (x - m) ** 2, 0) / (a.length - 1)); };
function paired(x, y, k) {
  const v = days.map(d => x[d][k] - y[d][k]); const n = v.length; const t = T975[n - 1]; if (!t) throw new Error('no t for df ' + (n - 1));
  const m = mean(v), hw = t * sd(v) / Math.sqrt(n); return { m, lo: m - hw, hi: m + hw, up: v.filter(z => z > 0).length, down: v.filter(z => z < 0).length };
}
const vn = (x, n = 2) => (x >= 0 ? '+' : '-') + Math.abs(x).toFixed(n).replace('.', ',');
const cmp = [['NCE35', 'EOHZ35', 'Co bu tru - han dieu chinh'], ['OCE35', 'EOHZ35', 'Chi tinh doi muon - han dieu chinh'], ['RCE35', 'OCE35', 'Hai chieu - chi tinh doi muon']];
const KEYS = [['rev', 'Gio tra doi qua lai (khach/ngay)'], ['served', 'Khach phuc vu (khach/ngay)'], ['late', 'Muon qua 1 phut (/1.000 khach)'], ['reloc', 'Chuyen xe (lan/ngay)']];
console.log('days:', days.length);
for (const [k, lab] of KEYS) {
  console.log(lab.padEnd(36) + cmp.map(([a, b]) => { const p = paired(A[a], A[b], k); return `${vn(p.m)} [${vn(p.lo)}; ${vn(p.hi)}]`; }).join('   |   '));
}
// LaTeX cells exactly as in the thesis table (two lines per cell)
console.log('\nLaTeX cells:');
for (const [k, lab] of KEYS) {
  console.log(lab + '\n' + cmp.map(([a, b]) => { const p = paired(A[a], A[b], k); const f = x => (x < 0 ? '-' : '+') + Math.abs(x).toFixed(2).replace('.', '{,}'); return `& \\shortstack{$${f(p.m)}$\\\\$[${f(p.lo)};\\,${f(p.hi)}]$}`; }).join('\n'));
}
// levels
console.log('\nlevels (mean over days):');
for (const a of ARMS) console.log(a.padEnd(8), ['served', 'rev', 'late', 'reloc'].map(k => k + ' ' + mean(days.map(d => A[a][d][k])).toFixed(2)).join(' | '));
