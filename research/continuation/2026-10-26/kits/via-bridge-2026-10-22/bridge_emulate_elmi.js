// Emulator side of the Elmi-budget bridge arms: the 11 bridge cells (2.0x) x RCE35 and EOHZ35 (wf14 formula,
// alpha = 350 permille), exact mode, riders.tsv layout. Emulator copy = emulator-wf5-eoc (RCE/EOHZ arms added
// 2026-10-22; existing 43 tests and the 4083/4083 calibration still pass). Also checks each arm's invariant:
//   RCE:  per rider sum|c| (S) <= B_r;   EOHZ: every published revision p <= max(p0 + B_r, e).
// Usage: node bridge_emulate_elmi.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-22/emulator-wf5-eoc';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const ARMS = [['RCE35', 'RCE', 350], ['EOHZ35', 'EOHZ', 350]];
const budget = (r, permille) => Math.round((permille / 1000) * Math.max(0, r.pk0 + r.maxRide - r.p0));

const out = process.argv[2];
if (!out) { console.error('usage: node bridge_emulate_elmi.js <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
if (cells.length !== 11) { console.error('expected 11 cells, found ' + cells.length); process.exit(4); }
const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
let violations = 0, revisions = 0, bindingRiders = 0;
for (const cell of cells) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  if (P.meta.permille !== 2000) throw new Error(cell + ': permille ' + P.meta.permille);
  for (const [role, name, permille] of ARMS) {
    const res = E.simulate(P, { arm: { name, permille } });
    for (const r of res.riders) {
      const B = budget(r, permille);
      if (name === 'RCE') {
        if (r.S > B) violations++;
        if (r.S > 0 && r.S === B) bindingRiders++;
      } else {
        for (const rev of r.revs) {
          revisions++;
          const eOff = rev[4], c = rev[2];
          if (eOff + c > Math.max(B, eOff)) violations++;
        }
      }
      lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
        r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
    }
    console.log(`${cell} ${role.padEnd(8)} riders ${res.riders.length} served ${res.riders.filter(r => r.served).length} rejected ${res.rejected}`);
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows; invariant violations ${violations} (EOHZ revisions checked ${revisions}; RCE riders exactly at budget ${bindingRiders})`);
if (violations) process.exit(1);
