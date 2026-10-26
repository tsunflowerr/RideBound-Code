'use strict';
const fs = require('fs');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const FC = require(SP + '/wf21/n7/code/scripts/forecast.js'); // read-only use of the library (pure functions)
const ref = JSON.parse(fs.readFileSync(SP + '/wf7/c-forecast/results/accuracy-index-2019.json', 'utf8'));
const PANELS = JSON.parse(fs.readFileSync(SP + '/wf17/r1-identity-fence-pad/work/panels.json', 'utf8'));
const flat = JSON.parse(fs.readFileSync(SP + '/wf21/n7/calib/flat_final.json', 'utf8')).flat;
for (const [w, s0] of [['w08', 32], ['w17', 68]]) {
  for (const pan of ['F14', 'F16']) {
    const days = PANELS[pan];
    // origins o = 0..7 within the 2-h window, horizons h = 1..8: weight m(o+h)/m(o)
    const W = Array.from({ length: 8 }, () => Array(9).fill(0));
    for (const d of days) { const m = FC.profile(d, s0, 'prev4', ref, 23); for (let o = 0; o < 8; o++) for (let h = 1; h <= 8; h++) W[o][h] += m[o + h] / m[o] / days.length; }
    const fmt = a => a.slice(1).map(x => x.toFixed(3)).join(' ');
    console.log(`${w} ${pan}: mean CL weight by horizon h=1..8 at origin o=0 (window start): ${fmt(W[0])}`);
    console.log(`${w} ${pan}:                                   at origin o=4 (+1 h):        ${fmt(W[4])}`);
    console.log(`${w} ${pan}:                                   at origin o=7 (+1h45):       ${fmt(W[7])}`);
  }
  console.log(`${w}: flat r (G2 SHRC) ${flat['G2|' + w + '|SHRC']}, (INS SHRC) ${flat['INS|' + w + '|SHRC']}`);
}
