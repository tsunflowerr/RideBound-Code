// wf21/n7 descriptive (run only after production, PLAN §2.2): forecast accuracy of CL (old index) vs CLc (causal index) vs naive on
// the 30 target days, wf7 accuracy.js definitions: MAE15 = mean |log f^ - log f| over origins o = 0..7 (window bins) and horizons
// h = 1..8 (15 min..2 h), log f^ = log f(b0) + log m(b0+h) - log m(b0); naive log f^ = log f(b0). Truth f = the target day's own
// factors (old index; the month constant cancels). Not used for any decision.
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const N7 = path.join(SP, 'wf21/n7');
const FC = require(path.join(N7, 'code/scripts/forecast.js'));
const ref = JSON.parse(fs.readFileSync(path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json'), 'utf8'));
const CAU = JSON.parse(fs.readFileSync(path.join(N7, 'cidx/results/index-causal-pertarget.json'), 'utf8'));
const days = Object.keys(CAU.targets).sort();
const out = ['# wf21/n7 forecast accuracy (descriptive): MAE15 in % (mean |log error|), per window; days = 30 targets'];
const tot = {};
for (const [w, s0] of [['w08', 32], ['w17', 68]]) {
  const acc = { CL: [], CLc: [], naive: [] };
  for (const d of days) {
    const f = ref[d];
    const mO = FC.profile(d, s0, 'prev4', ref, 23), mC = FC.profile(d, s0, 'prev4', CAU.targets[d].F, 23);
    const e = { CL: 0, CLc: 0, naive: 0 }; let n = 0;
    for (let o = 0; o < 8; o++) for (let h = 1; h <= 8; h++) {
      const truth = Math.log(f[s0 + o + h]);
      e.CL += Math.abs(Math.log(f[s0 + o]) + Math.log(mO[o + h]) - Math.log(mO[o]) - truth);
      e.CLc += Math.abs(Math.log(f[s0 + o]) + Math.log(mC[o + h]) - Math.log(mC[o]) - truth);
      e.naive += Math.abs(Math.log(f[s0 + o]) - truth); n++;
    }
    for (const k of Object.keys(e)) acc[k].push(100 * e[k] / n);
  }
  const mean = a => a.reduce((x, y) => x + y, 0) / a.length;
  const better = (a, b) => a.filter((x, i) => x < b[i] - 1e-12).length;
  out.push(`${w}: MAE15 CL ${mean(acc.CL).toFixed(3)} %  CLc ${mean(acc.CLc).toFixed(3)} %  naive ${mean(acc.naive).toFixed(3)} %  | days CLc better than CL ${better(acc.CLc, acc.CL)}, worse ${better(acc.CL, acc.CLc)}, equal ${days.length - better(acc.CLc, acc.CL) - better(acc.CL, acc.CLc)} | CL better than naive ${better(acc.CL, acc.naive)}/30, CLc better than naive ${better(acc.CLc, acc.naive)}/30`);
  tot[w] = acc;
}
fs.writeFileSync(path.join(N7, 'cidx/results/accuracy_c.txt'), out.join('\n') + '\n');
console.log(out.join('\n'));
