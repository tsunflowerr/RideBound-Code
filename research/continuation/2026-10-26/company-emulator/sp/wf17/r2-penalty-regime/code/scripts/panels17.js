// wf17 r2-penalty-regime: RUNPLAN 1.3 panels D60 and D30 (outcome-free), checked against the D30 list printed in the plan.
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const D60 = JSON.parse(fs.readFileSync(path.join(SP, 'wf14/work/days.json'), 'utf8')).weekdays.map(x => x.day).sort();
const D30 = D60.filter((d, i) => i % 2 === 0);
const PLAN_D30 = '2019-02-05 02-11 02-15 03-01 03-11 03-13 04-03 04-08 04-23 05-01 05-09 05-13 06-04 06-17 06-21 07-03 07-12 07-22 08-14 08-26 08-29 09-04 09-13 09-19 10-03 10-09 10-18 12-05 12-10 12-16'
  .split(' ').map(x => (x.length === 5 ? '2019-' + x : x));
const out = { D60, D30, check: { D60n: D60.length === 60, D30: JSON.stringify(D30) === JSON.stringify(PLAN_D30) } };
fs.writeFileSync(path.join(__dirname, '..', '..', 'work', 'panels.json'), JSON.stringify(out, null, 1));
fs.writeFileSync(path.join(__dirname, '..', '..', 'work', 'days_D30.json'), JSON.stringify({ weekdays: D30.map(day => ({ day })) }));
console.log(JSON.stringify(out.check));
