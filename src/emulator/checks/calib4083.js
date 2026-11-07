// Section 9 check "4,083/4,083 riders": the emulator against the archived cost-first RideBound runs of the cost-cap
// study (data/costcap-riders.tsv: 14 w08 cells of panel wp9-confirmatory-fixed-panel-v2 x roles CF-U, CF-C30, CF-C60
// and CF-C120, i.e. arm U and the fixed cumulative caps RC 30, 60 and 120 s). A rider counts as identical when both
// sides accept it and agree on t0, p0, A, sum|c|, k, max|c|, net c, served and shared.
// Usage: node calib4083.js <cells dir filled by fetch_cells.js>
'use strict';
const path = require('path');
const F = require('./fixtures.js');
const E = require('../engine.js');

const cellsDir = process.argv[2];
if (!cellsDir) { console.error('usage: node calib4083.js <cells dir>'); process.exit(2); }
const panel = path.join(cellsDir, 'wp9-confirmatory-fixed-panel-v2');
const allReal = F.readRidersTsv(path.join(__dirname, 'data', 'costcap-riders.tsv'))
  .filter(r => r.cell.endsWith('-w08'));
const cells = [...new Set(allReal.map(r => r.cell))].sort().map(c => c.replace('-w08', ''));
const roles = [['CF-U', 'U', 0], ['CF-C30', 'RC', 30], ['CF-C60', 'RC', 60], ['CF-C120', 'RC', 120]];
const same = (a, b) => a.t0 === b.t0 && a.p0 === b.p0 && a.A === b.A && a.S === b.S && a.k === b.k && a.M === b.M &&
  a.net === b.net && a.served === b.served && a.shared === b.shared;

let total = 0, identical = 0;
for (const [role, arm, capS] of roles) {
  const real = allReal.filter(r => r.role === role);
  let n = 0, ok = 0, cellsExact = 0;
  for (const cell of cells) {
    const P = F.prepareFixtureCell(cell, panel);
    const res = E.simulate(P, { arm: { name: arm, B: capS * 1000 } });
    const simBy = new Map(res.riders.map(r => [r.rq, r]));
    const realBy = new Map(real.filter(r => r.cell === cell + '-w08').map(r => [r.rq, r]));
    let exact = true;
    for (const q of P.reqs) {
      const a = realBy.get(q.rq), b = simBy.get(q.rq);
      if (!a && !b) continue;
      n++;
      if (a && b && same(a, b)) ok++;
      else exact = false;
    }
    if (exact) cellsExact++;
  }
  total += n; identical += ok;
  console.log(`${role.padEnd(8)} ${arm}${capS || ''}: ${ok}/${n} riders identical, ` +
    `${cellsExact}/${cells.length} cells exact`);
}
const pass = identical === total && total === 4083;
console.log(`ARCHIVED: ${identical}/${total} ${pass ? 'PASS' : 'FAIL'}`);
process.exitCode = pass ? 0 : 1;
