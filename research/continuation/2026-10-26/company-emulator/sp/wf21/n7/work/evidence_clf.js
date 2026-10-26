// wf21/n7 pre-plan evidence: wf17 R3 rows (already seen in wf17), CLF - CL and CL - N and CLF - N per window, EOHZ35/RCE35,
// per-window rates per 1000 served (mean over resamples, then day-level), pooled F14+F16. Used ONLY to write predictions.
'use strict';
const fs = require('fs');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const rows = fs.readFileSync(SP + '/wf17/r1-identity-fence-pad/results/r3.ndjson', 'utf8').trim().split('\n').map(JSON.parse);
const K = ['served', 'late60First', 'visRev60', 'pDisp', 'breachDeadline60', 'sumTV5', 'early60Last'];
const T = {};
for (const r of rows) { if (!['EOHZ35', 'RCE35'].includes(r.arm)) continue; const k = [r.config, r.arm, r.mode, r.day, r.win].join('|'); const a = (T[k] ||= { n: 0 }); a.n++; for (const f of K) a[f] = (a[f] || 0) + r[f]; }
const days = [...new Set(rows.map(r => r.day))].sort();
const rate = (a, f) => f === 'sumTV5' ? a.sumTV5 / 1000 / a.served : f === 'served' ? a.served / a.n : f === 'visRev60' ? a.visRev60 / a.n : 1000 * a[f] / a.served;
const out = [];
for (const c of ['INS', 'G2']) for (const arm of ['EOHZ35', 'RCE35']) for (const w of ['w08', 'w17']) for (const [A, B] of [['CL', 'N'], ['CLF', 'N'], ['CL', 'CLF']]) {
  const parts = [];
  for (const f of K) { const d = days.map(dd => { const a = T[[c, arm, A, dd, w].join('|')], b = T[[c, arm, B, dd, w].join('|')]; return a && b ? rate(a, f) - rate(b, f) : null; }).filter(x => x !== null);
    const m = d.reduce((x, y) => x + y, 0) / d.length, neg = d.filter(x => x < 0).length; parts.push(`${f} ${m.toFixed(2)} (${neg}-/${d.length})`); }
  out.push(`${c} ${arm} ${w} ${A}-${B}: ` + parts.join('  '));
}
console.log(out.join('\n'));
