// Smoke check before the full break-even run: one cell, INS and G2, arms EOHZ35/RCE35/EO10, same path as be_run.js runTask;
// prints the 5 gate fields next to the existing rows (plain-a1 for alpha 35, n2-plain otherwise). Writes nothing.
'use strict';
const fs = require('fs'), path = require('path');
const SPWF = 'E:/RideBoundData/research/p5-via-adapter-v1/sp/wf15';
const ROWS = 'E:/RideBoundData/research/p5-via-adapter-v1/rows';
const SC = require(path.join(SPWF, 'runner/scenario.js'));
const E = require(path.join(SPWF, 'runner/engine.js'));
const MET = require(path.join(SPWF, 'scripts/metrics_sh.js'));
const LIB = require(path.join(SPWF, 'scripts/shlib.js'));
const CORE = require(path.join(SPWF, 'scripts/shcore.js'));
const cell = '2019-02-13-w08-r0';
const old = new Map();
for (const f of ['plain-a1.ndjson', 'n2-plain.ndjson'])
  for (const l of fs.readFileSync(path.join(ROWS, f), 'utf8').split('\n')) if (l) { const r = JSON.parse(l); if (r.cell === cell) old.set(r.config + '|' + r.arm, r); }
const rc = CORE.readCell(path.join('E:/RideBoundData/research/p5-via-adapter-v1/cells/plain', cell + '.json'), null);
const P = SC.prepareScenario(rc.s, LIB.RUN_OVERRIDES, rc.dir);
const F = ['served', 'visRev60', 'late60First', 'pDisp', 'breachLedger'];
let bad = 0;
for (const config of ['INS', 'G2']) for (const key of ['EOHZ35', 'RCE35', 'EO10']) {
  const arm = E.parseArm(key), A = E.armOf(arm);
  const res = E.simulate(P, { ...JSON.parse(JSON.stringify(LIB.CONFIGS[config])), arm, wallBudgetMs: 900000 });
  const m = MET.metrics(res, A), o = old.get(config + '|' + key);
  const same = F.every(f => m[f] === o[f]);
  if (!same) bad++;
  let mis = 0; for (const r of res.riders) { let s = 0; for (const v of r.revs) s += v[2]; if (Math.abs(s - r.net) > 1e-6) mis++; }
  console.log(config, key, same ? 'SAME' : 'DIFF', F.map(f => `${f} ${m[f]}/${o[f]}`).join(' '), 'netMismatch', mis);
}
console.log(bad ? 'SMOKE FAIL' : 'SMOKE OK');
process.exit(bad ? 1 : 0);
