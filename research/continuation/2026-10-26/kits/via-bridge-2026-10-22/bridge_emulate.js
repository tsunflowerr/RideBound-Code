// Emulator side of the Manhattan bridge: the 11 bridge cells (2.0x fixtures) x U, RC120, RC300, PS300, DLHZ120,
// exact mode (adapter identity + FleetPy half-even snapshots, measured-day W5 profile), riders.tsv layout.
// Emulator = local copy C:/RideBoundContinuation/2026-10-22/emulator-wf5 (bundle code/wf5/runner, path constants only;
// its calibration reproduces 4083/4083 archived riders on this machine, out-local/calibration.json).
// Usage: node bridge_emulate.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-22/emulator-wf5';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const ARMS = [['U', 'U', 0], ['C-120', 'RC', 120], ['C-300', 'RC', 300], ['PS-300', 'PS', 300], ['DLHZ-120', 'DLHZ', 120]];

const out = process.argv[2];
if (!out) { console.error('usage: node bridge_emulate.js <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
if (cells.length !== 11) { console.error('expected 11 cells, found ' + cells.length); process.exit(4); }
const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
const t00 = Date.now();
for (const cell of cells) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  if (P.meta.permille !== 2000) throw new Error(cell + ': permille ' + P.meta.permille);
  for (const [role, name, Bs] of ARMS) {
    const res = E.simulate(P, { arm: { name, B: Bs * 1000 } });
    for (const r of res.riders) {
      lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
        r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
    }
    console.log(`${cell} ${role.padEnd(8)} riders ${res.riders.length} served ${res.riders.filter(r => r.served).length} rejected ${res.rejected}`);
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows in ${((Date.now() - t00) / 1000).toFixed(1)} s`);
