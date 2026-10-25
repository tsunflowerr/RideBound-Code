// Emulator side of the G2 bridge arms: the 11 bridge cells (2.0x) x G2-U, G2-RCE35, G2-EOHZ35, exact mode,
// riders.tsv layout. G2 as in the company configs.js entry `G2 = { reopt: { g2: 99 }, lock: 'ratchet' }`
// (move at most one confirmed waiting rider per decision to another vehicle; every promised waiting rider's
// pickup stays <= the keep-route projection; all other vehicles are targets). Emulator copy = emulator-wf5-eoc
// (adds the RCE/EOHZ arms; 43 tests and the 4083/4083 calibration pass).
// Also prints, per cell-arm, the emulator's relocation count, so the bridge can show that G2 is active.
// Usage: node bridge_emulate_g2.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-22/emulator-wf5-eoc';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const G2 = { reopt: { g2: 99 }, lock: 'ratchet' };
const ARMS = [['G2-U', { name: 'U', B: 0 }], ['G2-RCE35', { name: 'RCE', permille: 350 }], ['G2-EOHZ35', { name: 'EOHZ', permille: 350 }]];

const out = process.argv[2];
if (!out) { console.error('usage: node bridge_emulate_g2.js <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
if (cells.length !== 11) { console.error('expected 11 cells, found ' + cells.length); process.exit(4); }
const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions'];
let violations = 0;
for (const cell of cells) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  if (P.meta.permille !== 2000) throw new Error(cell + ': permille ' + P.meta.permille);
  for (const [role, arm] of ARMS) {
    const res = E.simulate(P, { ...G2, arm });
    for (const r of res.riders) {
      const B = arm.permille ? Math.round((arm.permille / 1000) * Math.max(0, r.pk0 + r.maxRide - r.p0)) : Infinity;
      if (arm.name === 'RCE' && r.S > B) violations++;
      if (arm.name === 'EOHZ') for (const rev of r.revs) if (rev[4] + rev[2] > Math.max(B, rev[4])) violations++;
      lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
        r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
    }
    const reloc = res.diag ? res.diag.relocations : undefined;
    const decisions = res.diag ? res.diag.genDecisions : undefined;
    counts.push([role, cell, res.riders.length, res.riders.filter(r => r.served).length, res.rejected, reloc, decisions].join('\t'));
    console.log(`${cell} ${role.padEnd(10)} riders ${res.riders.length} served ${res.riders.filter(r => r.served).length} rejected ${res.rejected} relocations ${reloc}`);
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-counts.tsv'), counts.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows; invariant violations ${violations}`);
if (violations) process.exit(1);
