// wf17 r1-identity-fence-pad: recompute the RUNPLAN 1.3 day panels (D60, D30, D10, F14, F16) and compare with the lists printed
// in the plan. Writes work/panels.json. Outcome-free (reads day lists, day_status.json and the forecast index only).
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const FC = require('./forecast.js');
const INDEX = JSON.parse(fs.readFileSync(path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json'), 'utf8'));
const fnv = s => { let h = 0x811c9dc5; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; } return h; };
const mulberry = a => () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
const D60 = JSON.parse(fs.readFileSync(path.join(SP, 'wf14/work/days.json'), 'utf8')).weekdays.map(x => x.day).sort();
const D30 = D60.filter((d, i) => i % 2 === 0);
const rng = mulberry(fnv('wf17-a0|D10|2026-10-23'));
const sh = D60.slice();
for (let i = sh.length - 1; i > 0; i--) { const j = Math.floor(rng() * (i + 1)); [sh[i], sh[j]] = [sh[j], sh[i]]; }
const D10 = sh.slice(0, 10).sort();
const F = INDEX.F || INDEX;
const elig = d => FC.profile(d, 32, 'prev4', F, 15) !== null && FC.profile(d, 68, 'prev4', F, 15) !== null;
const F14 = D60.filter(elig);
const ST = JSON.parse(fs.readFileSync(path.join(SP, 'wf12/r5-data-days/work/day_status.json'), 'utf8'));
const used = new Set(D60);
const avail = Object.values(ST).filter(d => d.status === 'AVAILABLE' && !d.dqFail && !used.has(d.day)).map(d => d.day).sort();
const F16 = avail.filter(d => FC.isWeekday(d) && elig(d));
const PLAN = {
  D30: '2019-02-05 02-11 02-15 03-01 03-11 03-13 04-03 04-08 04-23 05-01 05-09 05-13 06-04 06-17 06-21 07-03 07-12 07-22 08-14 08-26 08-29 09-04 09-13 09-19 10-03 10-09 10-18 12-05 12-10 12-16',
  D10: '2019-02-11 03-11 05-09 06-17 06-26 07-12 10-03 10-07 10-15 12-18',
  F14: '2019-09-19 09-24 10-03 10-07 10-09 10-15 10-18 10-24 12-05 12-09 12-10 12-13 12-16 12-18',
  F16: '2019-09-20 09-26 09-30 10-02 10-08 10-10 10-11 10-16 10-17 10-23 10-25 10-28 10-29 12-04 12-17 12-19',
};
const expand = s => s.split(' ').map(x => (x.length === 5 ? '2019-' + x : x));
const out = { D60, D30, D10, F14, F16, nAvailUnused: avail.length, availUnused: avail, check: {} };
for (const k of Object.keys(PLAN)) out.check[k] = JSON.stringify(expand(PLAN[k])) === JSON.stringify(out[k]);
fs.writeFileSync(path.join(__dirname, '..', '..', 'work', 'panels.json'), JSON.stringify(out, null, 1));
console.log(JSON.stringify({ check: out.check, n: { D60: D60.length, D30: D30.length, D10: D10.length, F14: F14.length, F16: F16.length, availUnused: avail.length } }));
for (const k of ['D10', 'F14', 'F16']) console.log(k, out[k].join(' '));
