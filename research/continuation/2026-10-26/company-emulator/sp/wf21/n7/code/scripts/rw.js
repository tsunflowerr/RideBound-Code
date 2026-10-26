// wf17 R3: outcome-free flat drift ratio r_w per panel x window (RUNPLAN R3, CLF arm):
//   r_w = exp( mean over days d of the panel, b0 in {0..7}, b in {b0+1..b0+4} of ln(prof_d[b] / prof_d[b0]) ),
// prof_d = forecast.profile(d, s0 = 32 | 68, 'prev4', index, 15). Uses no outcomes and no test-day traffic. -> work/r3/rw.json
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const KEY = path.resolve(__dirname, '..', '..');
const FC = require('./forecast.js');
const INDEX = JSON.parse(fs.readFileSync(path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json'), 'utf8'));
const PAN = JSON.parse(fs.readFileSync(path.join(KEY, 'work/panels.json'), 'utf8'));
const out = { label: 'wf17-exploratory-post-audit', definition: 'r_w = exp(mean_{d, b0 in 0..7, b in b0+1..b0+4} ln(prof_d[b]/prof_d[b0])), prev4 profiles', F14: {}, F16: {}, detail: {} };
for (const panel of ['F14', 'F16']) for (const [win, s0] of [['w08', 32], ['w17', 68]]) {
  let s = 0, n = 0;
  for (const d of PAN[panel]) {
    const p = FC.profile(d, s0, 'prev4', INDEX, 15);
    if (!p) throw new Error('no profile ' + d);
    for (let b0 = 0; b0 <= 7; b0++) for (let b = b0 + 1; b <= b0 + 4; b++) { s += Math.log(p[b] / p[b0]); n++; }
  }
  out[panel][win] = Math.exp(s / n);
  out.detail[panel + '|' + win] = { terms: n, meanLog: s / n };
}
fs.mkdirSync(path.join(KEY, 'work/r3'), { recursive: true });
fs.writeFileSync(path.join(KEY, 'work/r3/rw.json'), JSON.stringify(out, null, 1));
console.log(JSON.stringify({ F14: out.F14, F16: out.F16 }));
