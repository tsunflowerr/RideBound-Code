'use strict';
const fs = require('fs');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const FC = require(SP + '/wf21/n7/code/scripts/forecast.js');
const ref = JSON.parse(fs.readFileSync(SP + '/wf7/c-forecast/results/accuracy-index-2019.json', 'utf8'));
const PANELS = JSON.parse(fs.readFileSync(SP + '/wf17/r1-identity-fence-pad/work/panels.json', 'utf8'));
const days = [...PANELS.F14, ...PANELS.F16];
for (const [w, s0] of [['w08', 32], ['w17', 68]]) {
  // per day: weight at horizon h=2 (30 min) for every origin o = 0..7  -> matrix day x origin
  const M = days.map(d => { const m = FC.profile(d, s0, 'prev4', ref, 23); return Array.from({ length: 8 }, (_, o) => m[o + 2] / m[o]); });
  const grand = M.flat().reduce((a, b) => a + b, 0) / (M.length * 8);
  const originMean = Array.from({ length: 8 }, (_, o) => M.reduce((a, r) => a + r[o], 0) / M.length);
  const dayMean = M.map(r => r.reduce((a, b) => a + b, 0) / 8);
  const ssO = originMean.reduce((a, x) => a + M.length * (x - grand) ** 2, 0), ssD = dayMean.reduce((a, x) => a + 8 * (x - grand) ** 2, 0);
  let ssT = 0; for (const r of M) for (const x of r) ssT += (x - grand) ** 2;
  console.log(`${w}: 30-min-ahead CL weight m(o+2)/m(o): grand mean ${grand.toFixed(4)}; variance share: time-of-day (origin) ${(ssO / ssT * 100).toFixed(1)} %, day ${(ssD / ssT * 100).toFixed(1)} %, day x origin ${(100 - (ssO + ssD) / ssT * 100).toFixed(1)} %; origin means ${originMean.map(x => x.toFixed(3)).join(' ')}; SD of day means ${Math.sqrt(dayMean.reduce((a, x) => a + (x - grand) ** 2, 0) / (dayMean.length - 1)).toFixed(4)}`);
}
