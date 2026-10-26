'use strict';
const fs = require('fs');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const FC = require(SP + '/wf21/n7/code/scripts/forecast.js');
const ref = JSON.parse(fs.readFileSync(SP + '/wf7/c-forecast/results/accuracy-index-2019.json', 'utf8'));
const PANELS = JSON.parse(fs.readFileSync(SP + '/wf17/r1-identity-fence-pad/work/panels.json', 'utf8'));
const flat = JSON.parse(fs.readFileSync(SP + '/wf21/n7/calib/flat_final.json', 'utf8')).flat;
for (const [w, s0] of [['w08', 32], ['w17', 68]]) for (const pan of ['F14', 'F16']) {
  const r = flat['G2|' + w + '|SHRC'];
  let eCL = 0, eN = 0, eP = 0, eClim = 0, n = 0;
  // climatological profile: mean over all 30 target days' prev4 profiles (generic time-of-day shape, no day-specific info)
  const all = [...PANELS.F14, ...PANELS.F16].map(d => FC.profile(d, s0, 'prev4', ref, 23));
  const clim = all[0].map((_, i) => Math.exp(all.reduce((a, m) => a + Math.log(m[i]), 0) / all.length));
  for (const d of PANELS[pan]) {
    const f = ref[d], m = FC.profile(d, s0, 'prev4', ref, 23);
    for (let o = 0; o < 8; o++) for (let h = 1; h <= 8; h++) {
      const truth = Math.log(f[s0 + o + h]);
      eCL += Math.abs(Math.log(f[s0 + o]) + Math.log(m[o + h]) - Math.log(m[o]) - truth);
      eClim += Math.abs(Math.log(f[s0 + o]) + Math.log(clim[o + h]) - Math.log(clim[o]) - truth);
      eN += Math.abs(Math.log(f[s0 + o]) - truth);
      eP += Math.abs(Math.log(f[s0 + o]) + Math.log(r) - truth);  // flat pad as a forecast (current bin x r for every later bin)
      n++;
    }
  }
  console.log(`${w} ${pan}: MAE15 % CL ${(100 * eCL / n).toFixed(3)}  climatological shape ${(100 * eClim / n).toFixed(3)}  flat r ${(100 * eP / n).toFixed(3)}  naive ${(100 * eN / n).toFixed(3)}`);
}
