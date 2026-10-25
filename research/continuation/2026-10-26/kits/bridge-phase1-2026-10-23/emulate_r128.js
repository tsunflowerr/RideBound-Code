// Step 1.4 emulator predictions: the 10 bridge cells at 128 requests and 10 vehicles (the normalizer contract v1 bound),
// written and sealed before any 1.4 RideBound job. Engine: the company wf15 engine (local copy, step 1.0 passed).
//   R128-U, R128-RCE35       insertion-only (bridge configs U, RCE35; WP4 cost-first, no-worse recovery);
//   R128-G2K8-U, -G2K8-RCE35 G2 with at most 8 receivers (the company's setting; 9 other vehicles, so it binds).
// Also writes, for information only (not a bridge arm), unlimited-G2 riders R128-G2ALL-* to measure what the cap of 8
// changes (discrimination).
// Usage: node emulate_r128.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-23/emulator-wf15';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-r128-v1';
const K8 = { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' };
const ALL = { reopt: { g2: 99 }, lock: 'ratchet' };
const ARMS = [
  ['R128-U', {}, { name: 'U', B: 0 }],
  ['R128-RCE35', {}, { name: 'RCE', alpha: 0.35 }],
  ['R128-G2K8-U', K8, { name: 'U', B: 0 }],
  ['R128-G2K8-RCE35', K8, { name: 'RCE', alpha: 0.35 }],
];
const INFO = [
  ['R128-G2ALL-U', ALL, { name: 'U', B: 0 }],
  ['R128-G2ALL-RCE35', ALL, { name: 'RCE', alpha: 0.35 }],
];

const out = process.argv[2];
if (!out) { console.error('usage: node emulate_r128.js <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
if (cells.length !== 10) { console.error('expected 10 cells, found ' + cells.length); process.exit(4); }
const header = 'role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared';
const lines = [header], info = [header];
const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tvehicles\trequests'];
for (const cell of cells) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  if (P.meta.permille !== 2000) throw new Error(cell + ': permille ' + P.meta.permille);
  if (P.fleet.length !== 10 || P.reqs.length !== 128) throw new Error(cell + ': size ' + P.fleet.length + '/' + P.reqs.length);
  for (const [set, target] of [[ARMS, lines], [INFO, info]]) {
    for (const [role, opts, arm] of set) {
      const res = E.simulate(P, { ...opts, arm });
      for (const r of res.riders) {
        target.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
          r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
      }
      const d = res.diag || {};
      counts.push([role, cell, res.riders.length, res.riders.filter(r => r.served).length, res.rejected,
        d.relocations, d.genDecisions, P.fleet.length, P.reqs.length].join('\t'));
    }
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-counts.tsv'), counts.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-info-g2all.tsv'), info.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows (+ ${info.length - 1} info rows)`);
