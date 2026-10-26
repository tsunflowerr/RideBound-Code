// wf20/fullsys: compact key-number extract of results/tables.json (presentation only; no new statistic). Label
// wf20-fullsys-exploratory-post-audit (thăm dò sau kiểm toán). Usage: node keys.js --tables <tables.json> --out <key_numbers.txt>
'use strict';
const fs = require('fs');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const T = JSON.parse(fs.readFileSync(arg('tables'), 'utf8'));
const out = [], P = s => { out.push(s); console.log(s); };
const K = ['served|day', 'visRev60|day', 'visRev60/1000|day', 'late60First/1000|day', 'late60First/1000|w08', 'late60First/1000|w17', 'Pchg/1000|day',
  'breachDeadline60/1000|day', 'TV5 s/served|day', 'G_late60First|day', 'G_late60First/1000|day', 'guaranteedShare %|day', 'guaranteedShare %|w08', 'guaranteedShare %|w17', 'breachLedger/1000|day', 'relocations|day'];
const f = x => (x === null || x === undefined || !Number.isFinite(x) ? 'n/a' : x.toFixed(2));
const pv = p => (p < 0.001 ? p.toExponential(1) : p.toFixed(3));
const c = s => (s ? `${f(s.mean)}±${f(s.se)} ${s.nMinus}-/${s.nPlus}+ p${pv(s.p)}${s.rel !== null && s.rel !== undefined ? ' (' + (100 * s.rel).toFixed(1) + '%)' : ''}` : 'n/a');
P('wf20-fullsys-exploratory-post-audit (thăm dò sau kiểm toán) — key numbers from results/tables.json');
P('cell = mean Δ ± SE, days −/+, exact sign-test p, (rel = ΣA/ΣB − 1 for counts); A/B = pooled day means');
for (const [id, r] of Object.entries(T.contrasts)) {
  if (!r) continue;
  P(`
=== ${id}  (days F14 ${r.F14.days}, F16 ${r.F16.days})`);
  for (const k of K) { const p = r.pooled.R[k]; if (!p) continue;
    P(`${k.padEnd(26)} A ${f(p.meanA).padStart(7)} B ${f(p.meanB).padStart(7)} | pooled ${c(p)} | F14 ${c(r.F14.R[k])} | F16 ${c(r.F16.R[k])}`); }
}
for (const [id, r] of Object.entries(T.did)) {
  if (!r) continue;
  P(`
=== ${id}`);
  for (const k of K) { const p = r.pooled.R[k]; if (!p) continue; P(`${k.padEnd(26)} pooled ${c(p)} | F14 ${c(r.F14.R[k])} | F16 ${c(r.F16.R[k])}`); }
}
P('\n=== WHOLE SYSTEM LEVELS (pooled 30 days; F14 / F16 in brackets)');
const L = T.levels, ks = Object.keys(L);
P('metric'.padEnd(26) + ks.map(k => (k + '=' + L[k].sel.arm + '_' + L[k].sel.config + '_' + L[k].sel.mode).padStart(30)).join(''));
for (const k of K) { if (ks.every(x => L[x].pooled.R[k] === undefined)) continue;
  P(k.padEnd(26) + ks.map(x => (`${f(L[x].pooled.R[k])} [${f(L[x].F14.R[k])}/${f(L[x].F16.R[k])}]`).padStart(30)).join('')); }
fs.writeFileSync(arg('out'), out.join('\n') + '\n');
