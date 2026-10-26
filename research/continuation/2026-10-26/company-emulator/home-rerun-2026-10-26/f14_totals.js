// wf22-home: F14 totals per (config, mode, arm[, variant]) over the 84 F14 cells, for HUONG-DAN-MAY-NHA.md §5.
// Mean promise length (s) = Σ plSumArr_ms / Σ plN / 1000 (plSumArr_ms = Σ p0 − arrival, wf21/n7 shcore.js:6-7). Read-only. Usage: node f14_totals.js <rows.ndjson>
'use strict';
const fs = require('fs');
const rows = fs.readFileSync(process.argv[2], 'utf8').split('\n').filter(Boolean).map(JSON.parse).filter(r => r.panel === 'F14');
const g = new Map();
for (const r of rows) {
  const k = [r.config, r.mode, r.arm.replace(/\d+$/, ''), r.variant || ''].join(' / ');
  const a = g.get(k) || { cells: 0, served: 0, visRev60: 0, late60First: 0, plN: 0, arr: 0, t0: 0 };
  a.cells++; a.served += r.served; a.visRev60 += r.visRev60; a.late60First += r.late60First;
  a.plN += r.plN || 0; a.arr += r.plSumArr_ms || 0; a.t0 += r.plSumT0_ms || 0;
  g.set(k, a);
}
for (const [k, a] of [...g].sort()) {
  const pl = a.plN ? (a.arr / a.plN / 1000).toFixed(2) : 'NA';
  console.log(`${k.padEnd(22)} cells ${a.cells} served ${a.served} visRev60 ${a.visRev60} late60First ${a.late60First} meanPromise_s ${pl}`);
}
