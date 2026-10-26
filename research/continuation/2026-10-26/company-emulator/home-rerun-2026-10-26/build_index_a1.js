// wf22-home amendment A1 (PLAN.md S2): rebuild wf7/c-forecast/results/accuracy-index-2019.json as the union of
// targets[d].F in wf21/n7/cidx/results/index-old-pertarget.json (rule recorded in that file: "wf7 accuracy-index-2019.json
// restricted to [d-28, d-1]"). Stops if a day appears under two targets with different arrays. Refuses to overwrite.
// NOT the original file: valid only for the F14/F16 target days, and only if confirm_digest then matches every rider.
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const SRC = path.join(SP, 'wf21/n7/cidx/results/index-old-pertarget.json');
const OUT = path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json');
const o = JSON.parse(fs.readFileSync(SRC, 'utf8'));
if (!/accuracy-index-2019\.json restricted to \[d-28, d-1\]/.test(o.rule)) throw new Error('unexpected rule: ' + o.rule);
const U = {}, seen = {};
let overlaps = 0;
for (const [t, v] of Object.entries(o.targets)) {
  for (const [d, a] of Object.entries(v.F)) {
    const s = JSON.stringify(a);
    if (d in seen) { overlaps++; if (seen[d] !== s) throw new Error(`conflict on ${d} (target ${t})`); continue; }
    if (!Array.isArray(a) || a.length !== 96) throw new Error(`bad array on ${d}`);
    seen[d] = s; U[d] = a;
  }
}
const days = Object.keys(U).sort();
const out = {}; for (const d of days) out[d] = U[d];
fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, JSON.stringify(out), { flag: 'wx' });
console.log(`targets ${Object.keys(o.targets).length}, days ${days.length} (${days[0]}..${days[days.length - 1]}), overlaps ${overlaps}, conflicts 0 -> ${OUT}`);
