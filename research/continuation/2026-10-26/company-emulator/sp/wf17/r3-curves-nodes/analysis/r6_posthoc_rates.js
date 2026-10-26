// wf17 R6 POST-HOC descriptive arithmetic (written AFTER the R6 tables were read; no new test, no label): exchange rates at MATCHED
// service, as ratios of the per-day frontier means printed by the pre-hashed analysis/r6_analyze.js (results/r6_tables.json, frontier
// RCE_vs_EOHZ). ARR_m = reversals avoided per 1000 served; per extra Elmi-deadline breach and per extra rider late > 60 s vs the first
// promise (only when that Delta > 0). Means are per-day ratios per 1000 served, so the ratios are "per 1000 served" on both sides.
'use strict';
const S = require('../results/r6_tables.json');
const out = ['wf17 R6 post-hoc matched-service exchange rates (wf17-exploratory-post-audit; derived from results/r6_tables.json, RCE_vs_EOHZ frontier)',
  'config point  dVisRev60/1000  dBreachDeadline60/1000  dLate60First/1000  dPchg(pp) | rev avoided per extra breach | per extra late rider'];
for (const cfg of ['INS', 'G2']) {
  const F = S.frontier[cfg].RCE_vs_EOHZ;
  for (let i = 0; i < F['visRev60 /1000'].length; i++) {
    const v = F['visRev60 /1000'][i], b = F['breachDeadline60 /1000'][i], l = F['late60First /1000'][i], p = F['Pchg (pp)'][i];
    if (v.param === 0) continue;
    const r = (d) => (d > 0 ? (-v.mean / d).toFixed(2) : 'no price (Delta <= 0)');
    out.push(`${cfg.padEnd(4)} ${v.arm.padEnd(7)} ${v.mean.toFixed(2).padStart(8)} ${b.mean.toFixed(2).padStart(12)} ${l.mean.toFixed(2).padStart(14)} ${p.mean.toFixed(2).padStart(10)} | ${r(b.mean).padStart(10)} | ${r(l.mean)}  (days ${v.nIncl})`);
  }
}
require('fs').writeFileSync(require('path').join(__dirname, '..', 'results', 'r6_posthoc_rates.txt'), out.join('\n') + '\n');
console.log(out.join('\n'));
