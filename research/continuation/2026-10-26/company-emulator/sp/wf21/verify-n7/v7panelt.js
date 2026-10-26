'use strict';
const L = require('./v7lib.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7';
const rows = L.readNd(K + '/results/rows.ndjson');
const T = L.dayTables(rows, r => r.variant == null ? r.mode : r.variant === 'PADc' ? 'PADc' : null);
const welch = (a, b) => { const m = x => x.reduce((s, y) => s + y, 0) / x.length, v = x => { const mm = m(x); return x.reduce((s, y) => s + (y - mm) ** 2, 0) / (x.length - 1); };
  const se = Math.sqrt(v(a) / a.length + v(b) / b.length); return { d: m(a) - m(b), t: (m(a) - m(b)) / se }; };
for (const c of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) for (const sc of ['w08', 'w17', 'day']) {
  const A = L.diffs(T, `${c}|CL|${arm}`, `${c}|PAD|${arm}`, 'late', sc, 'F14').d, B = L.diffs(T, `${c}|CL|${arm}`, `${c}|PAD|${arm}`, 'late', sc, 'F16').d;
  const w = welch(A, B);
  console.log(`${c} ${arm} ${sc}: CL-PAD F14 - F16 = ${L.f(w.d)} (Welch t ${w.t.toFixed(2)})`);
}
