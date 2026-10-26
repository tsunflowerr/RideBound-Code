// wf17 gate G-P2 (engine integrity of the shared engine17.js; R4's gate, run here because this key builds engine17 first):
// G2 with g2PenaltyMs 60000 / 300000 (targets 8), arms EO35/EOHZ35/RCE35, 2 D30 days x {w08, w17} x r0 -> compare with a5 V11/V12.
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const SC = require('../runner/scenario.js'), E = require('../runner/engine17.js'), MET = require('./metrics_sh.js');
const out = process.argv[2];
const A5 = fs.readFileSync(path.join(SP, 'wf16/a5-steering/results/robust.ndjson'), 'utf8').split('\n').filter(Boolean).map(JSON.parse);
let eq = 0, n = 0; const bad = [];
for (const day of ['2019-02-05', '2019-02-11']) for (const win of ['w08', 'w17']) {
  const cell = `${day}-${win}-r0`;
  const P = SC.loadScenarioFile(path.join(SP, 'wf14/scenarios/weekdays-P512', cell + '.json'), { maxRidePermille: 2000, dwellMs: 0, nVeh: 62 });
  for (const [variant, pen] of [['V11_g2pen60', 60000], ['V12_g2pen300', 300000]]) for (const key of ['EO35', 'RCE35', 'EOHZ35']) {
    const arm = E.parseArm(key);
    const res = E.simulate(P, { reopt: { g2: 99, g2Targets: 8, g2PenaltyMs: pen }, lock: 'ratchet', arm, wallBudgetMs: 900000 });
    const m = MET.metrics(res, E.armOf(arm));
    const row = { label: 'wf17-exploratory-post-audit', gate: 'G-P2', variant, cell, config: 'G2', arm: key, ...m };
    fs.appendFileSync(out, JSON.stringify(row) + '\n');
    const ref = A5.find(r => r.variant === variant && r.cell === cell && r.config === 'G2' && r.arm === key.replace(/\d+$/, ''));
    n++;
    if (!ref) { bad.push([cell, variant, key, 'no ref']); continue; }
    const diff = Object.keys(ref).filter(k => !['variant', 'cell', 'day', 'win', 'rs', 'config', 'arm', 'alpha', 'nReq'].includes(k) && JSON.stringify(ref[k]) !== JSON.stringify(row[k]));
    if (diff.length) bad.push([cell, variant, key, diff.slice(0, 5)]); else eq++;
  }
}
console.log(JSON.stringify({ gate: 'G-P2', runs: n, equal: eq, pass: eq === n, bad }));
