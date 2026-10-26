// wf21/n7 presentation only (written AFTER the results were read; DEVIATIONS D8): compact extract of results/tables.json.
// No new statistic. Usage: node keys7.js <tables.json> <out.txt>
'use strict';
const fs = require('fs');
const T = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const L = [];
const f = (x, d = 2) => (x === null || x === undefined || !Number.isFinite(x) ? 'NA' : (x >= 0 ? '+' : '') + x.toFixed(d));
const fp = p => (p < 0.001 ? p.toExponential(1) : p.toFixed(3));
const cell = r => r ? `${f(r.mean)} [${f(r.lo)}, ${f(r.hi)}] ${r.nMinus}-/${r.nPlus}+ p ${fp(r.p)}` : 'NA';
const short = r => r ? `${f(r.mean, 1)} (${r.nMinus}-/${r.nPlus}+)` : 'NA';
const MET = [['late60First/1000', ['day', 'w08', 'w17']], ['early60First/1000', ['day', 'w08', 'w17']], ['visRev60', ['day', 'w08', 'w17']], ['TV5 s', ['day', 'w08', 'w17']],
  ['breachDeadline60/1000', ['day', 'w08', 'w17']], ['served', ['day']], ['Pchg/1000', ['day']], ['G_late60First', ['day']], ['guaranteedShare %', ['day']], ['PL s', ['day', 'w08', 'w17']]];
L.push('# wf21/n7 key numbers (thăm dò sau kiểm toán). Pooled = 30 days; F14 / F16 means with n-/n+ in brackets. Units: /1000 = per 1000 served; visRev60, served, G_late60First per day; TV5 s per served rider; PL s.');
for (const con of ['CL - PAD', 'CLc - PADc', 'CLc - PAD', 'CLc - CL', 'PAD - N', 'PADc - N', 'CL - N', 'CLc - N']) {
  L.push(`\n## ${con}`);
  for (const config of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) {
    const c = T.contrasts[`${arm}_${config}: ${con}`]; if (!c) continue;
    L.push(`### ${arm}_${config}`);
    for (const [m, scs] of MET) for (const sc of scs) {
      const k = m + '|' + sc;
      L.push(`  ${(m + ' ' + sc).padEnd(28)} pooled ${cell(c.pooled.R[k])}   | F14 ${short(c.F14.R[k])} | F16 ${short(c.F16.R[k])}`);
    }
  }
}
L.push('\n## Answers (PLAN §6)');
for (const [k, A] of Object.entries(T.answers)) L.push(`${k.padEnd(26)} late day ${A['late60First/1000|day']} | w08 ${A['late60First/1000|w08']} | w17 ${A['late60First/1000|w17']} | TV5 ${A['TV5 s|day']} | visRev60 ${A['visRev60|day']} | deadline ${A['breachDeadline60/1000|day']} | early ${A['early60First/1000|day']} | served ${A['served|day']}`);
L.push('\n## Prediction label counts (pooled)');
const cnt = {}; for (const p of T.predictions) cnt[p.labels.pooled] = (cnt[p.labels.pooled] || 0) + 1;
L.push(JSON.stringify(cnt));
for (const p of T.predictions) if (p.labels.pooled !== 'HOLDS') L.push(`  ${p.id} ${p.cell} ${p.metric} ${p.scope}: ${p.labels.pooled} (mean ${f(p.mean)}, band [${p.band}]; F14 ${p.labels.F14}, F16 ${p.labels.F16})`);
fs.writeFileSync(process.argv[3], L.join('\n') + '\n');
console.log(`written ${process.argv[3]} (${L.length} lines)`);
