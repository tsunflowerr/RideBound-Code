// Mechanism check of the Elmi-OC arm (emulator-wf5-eoc) on the 11 Manhattan bridge cells, 2.0x. NOT a result.
//  (1) EOC0 must be rider-identical to DLHZ0 (both mean p <= max(p0, e); different code paths);
//  (2) every published drop revision under EOC(gamma) satisfies p <= max(bound, e),
//      bound = p0 + floor(gamma * (pk0 + maxRide - p0) / 1000); revs rows are [t, x, c, z, e - p0, ...], p = e + c;
//  (3) served riders and sum |c| per gamma, plus how close EOC1000 is to U.
// Usage: node eoc_check.js <out.json>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-22/emulator-wf5-eoc';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));
const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const GAMMAS = [0, 250, 500, 750, 1000];
const sig = r => [r.rq, r.t0, r.p0, r.A, r.S, r.k, r.M, r.net, r.served, r.shared].join('|');

const out = process.argv[2];
if (!out || fs.existsSync(out)) { console.error('usage: node eoc_check.js <new out.json>'); process.exit(2); }
const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
const report = { cells: cells.length, eoc0VsDlhz0: { cells: 0, identicalCells: 0 }, violations: 0, revisionsChecked: 0,
  perGamma: {}, eoc1000VsU: { riders: 0, identical: 0 }, U: { served: 0, sumS: 0 } };
for (const g of GAMMAS) report.perGamma[g] = { served: 0, promised: 0, sumS: 0, ridersWithC: 0 };
for (const cell of cells) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  const u = E.simulate(P, { arm: { name: 'U' } });
  report.U.served += u.riders.filter(r => r.served).length;
  report.U.sumS += u.riders.reduce((a, r) => a + r.S, 0);
  const dl0 = E.simulate(P, { arm: { name: 'DLHZ', B: 0 } });
  for (const g of GAMMAS) {
    const res = E.simulate(P, { arm: { name: 'EOC', gamma: g } });
    const pg = report.perGamma[g];
    pg.promised += res.riders.length;
    pg.served += res.riders.filter(r => r.served).length;
    pg.sumS += res.riders.reduce((a, r) => a + r.S, 0);
    pg.ridersWithC += res.riders.filter(r => r.k > 0).length;
    for (const r of res.riders) {
      const boundOff = Math.floor(g * (r.pk0 + r.maxRide - r.p0) / 1000);
      for (const rev of r.revs) {
        const eOff = rev[4], c = rev[2];
        report.revisionsChecked++;
        if (eOff + c > Math.max(boundOff, eOff)) report.violations++;
      }
    }
    if (g === 0) {
      report.eoc0VsDlhz0.cells++;
      if (res.riders.map(sig).join('\n') === dl0.riders.map(sig).join('\n') && res.rejected === dl0.rejected) report.eoc0VsDlhz0.identicalCells++;
    }
    if (g === 1000) {
      const ub = new Map(u.riders.map(r => [r.rq, sig(r)]));
      const ids = new Set([...u.riders.map(r => r.rq), ...res.riders.map(r => r.rq)]);
      report.eoc1000VsU.riders += ids.size;
      report.eoc1000VsU.identical += res.riders.filter(r => ub.get(r.rq) === sig(r)).length;
    }
  }
}
fs.writeFileSync(out, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify(report, null, 2));
