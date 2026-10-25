// Phase 1 emulator predictions (steps 1.1 and 1.2), written and sealed before any phase-1 RideBound job.
// Engine: the company wf15 engine (local copy C:/RideBoundContinuation/2026-10-23/emulator-wf15; step 1.0 showed it
// reproduces all four sealed bridge prediction files byte for byte).
//   1.1 strict deadlines (forced vehicles keep their route and are excluded from G2; RideBound: forced-reference-v1):
//       EO35 = {EO, alpha 0.35}, DLS-120 = {DLS, B 120 s}, insertion-only and with G2 (all other vehicles as targets).
//   1.2 G2 with at most 3 receiver vehicles (g2Targets 3 < the 7 other vehicles; RideBound maximumTargetVehicles 3):
//       G2K3-U, G2K3-RCE35, G2K3-EOHZ35.
// Same riders.tsv layout as the bridge; counts add relocations and forced-vehicle decisions per cell-arm.
// Usage: node emulate_p1.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-23/emulator-wf15';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const G2 = { reopt: { g2: 99 }, lock: 'ratchet' };
const G2K3 = { reopt: { g2: 99, g2Targets: 3 }, lock: 'ratchet' };
const ARMS = [
  ['EO35', {}, { name: 'EO', alpha: 0.35 }],
  ['DLS-120', {}, { name: 'DLS', B: 120000 }],
  ['G2-EO35', G2, { name: 'EO', alpha: 0.35 }],
  ['G2-DLS-120', G2, { name: 'DLS', B: 120000 }],
  ['G2K3-U', G2K3, { name: 'U', B: 0 }],
  ['G2K3-RCE35', G2K3, { name: 'RCE', alpha: 0.35 }],
  ['G2K3-EOHZ35', G2K3, { name: 'EOHZ', alpha: 0.35 }],
];

const out = process.argv[2];
if (!out) { console.error('usage: node emulate_p1.js <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
if (cells.length !== 11) { console.error('expected 11 cells, found ' + cells.length); process.exit(4); }
const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tforcedVehicleDecisions'];
for (const cell of cells) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  if (P.meta.permille !== 2000) throw new Error(cell + ': permille ' + P.meta.permille);
  for (const [role, opts, arm] of ARMS) {
    const res = E.simulate(P, { ...opts, arm });
    for (const r of res.riders) {
      lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
        r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
    }
    const d = res.diag || {};
    counts.push([role, cell, res.riders.length, res.riders.filter(r => r.served).length, res.rejected,
      d.relocations, d.genDecisions, d.forcedVehicleDecisions].join('\t'));
    console.log(`${cell} ${role.padEnd(12)} riders ${res.riders.length} served ${res.riders.filter(r => r.served).length} ` +
      `relocations ${d.relocations} forcedVehicleDecisions ${d.forcedVehicleDecisions}`);
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-counts.tsv'), counts.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows`);
