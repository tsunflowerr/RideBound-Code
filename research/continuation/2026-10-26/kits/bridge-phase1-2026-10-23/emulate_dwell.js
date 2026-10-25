// Step 1.3 emulator predictions: dwell 24 s at every pickup and drop (Elmi's value), written and sealed before any
// step-1.3 RideBound job. Engine: the company wf15 engine (local copy; step 1.0 passed). The engine's dwell option
// (fixtures.js:19 cfg.dwell) enters both the stop identity (stopBody) and the timing (engine.js: time = ss + dwell).
// Cells (fixed before any run): the first two w08 and the first two w17 bridge cells in name order:
//   d20181112-s10-r1-w08, d20181113-s10-r1-w08, d20181112-s10-r1-w17, d20181113-s10-r1-w17.
// Arms: D24-U, D24-RCE35, D24-EOHZ35 (insertion-only) and D24-G2-U, D24-G2-RCE35, D24-G2-EOHZ35 (G2, all vehicles).
// Usage: node emulate_dwell.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-23/emulator-wf15';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const CELLS = ['d20181112-s10-r1-w08', 'd20181113-s10-r1-w08', 'd20181112-s10-r1-w17', 'd20181113-s10-r1-w17'];
const DWELL = 24000;
const G2 = { reopt: { g2: 99 }, lock: 'ratchet' };
const ARMS = [
  ['D24-U', {}, { name: 'U', B: 0 }],
  ['D24-RCE35', {}, { name: 'RCE', alpha: 0.35 }],
  ['D24-EOHZ35', {}, { name: 'EOHZ', alpha: 0.35 }],
  ['D24-G2-U', G2, { name: 'U', B: 0 }],
  ['D24-G2-RCE35', G2, { name: 'RCE', alpha: 0.35 }],
  ['D24-G2-EOHZ35', G2, { name: 'EOHZ', alpha: 0.35 }],
];

const out = process.argv[2];
if (!out) { console.error('usage: node emulate_dwell.js <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tdwell'];
for (const cell of CELLS) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES }, dwell: DWELL });
  if (P.meta.permille !== 2000 || P.dwell !== DWELL) throw new Error(cell + ': setup');
  for (const [role, opts, arm] of ARMS) {
    const res = E.simulate(P, { ...opts, arm });
    for (const r of res.riders) {
      lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
        r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
    }
    const d = res.diag || {};
    counts.push([role, cell, res.riders.length, res.riders.filter(r => r.served).length, res.rejected,
      d.relocations, d.genDecisions, P.dwell].join('\t'));
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-counts.tsv'), counts.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows`);
