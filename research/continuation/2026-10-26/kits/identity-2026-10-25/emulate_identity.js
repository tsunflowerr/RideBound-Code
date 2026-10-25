// Cheap identity checks (company plan part2:188,326; audit §6-N5), emulator only (wf15 engine, local copy), on the 11
// Manhattan bridge cells, whose emulator runs match the real RideBound per rider (bridge: 227 cell-arms):
//   (a) insertion-only, cell's own traffic: RC(B) = NC(B) = OC(B), B in {120, 300} s  (no negative c to refund)
//   (b) insertion-only, FLAT traffic (window-start factor held for every bin): RC(B) = DLHZ(B) and RCE35 = EOHZ35
//       (no traffic drift: Rule C's ledger and the HZ fence coincide -- Elmi's constant-time world, E0)
// A pair is identical when every rider has the same t0, p0, A, served, S (sum|c|), k (nonzero c), net, M (max|c|), shared.
// Usage: node emulate_identity.js <out.tsv>   (refuses to overwrite)
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-23/emulator-wf15';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));
const ID = require(path.join(EMU, 'identity.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const out = process.argv[2];
if (!out) { console.error('usage: node emulate_identity.js <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }

function flat(P) {
  const f0 = P.meta.prof[0];
  const snap0 = P.snapOf(0);
  const nodeIds = Array.from({ length: P.N }, (_, i) => ID.nodeId(i));
  const cache = new Map();
  return { ...P, id: P.id + '-flat', physFactor: () => f0, snapOf: () => snap0,
    snapHash: bin => { if (!cache.has(bin)) cache.set(bin, ID.snapshotHash(P.N, P.T0, f0, bin + 1, nodeIds)); return cache.get(bin); } };
}
const key = r => [r.t0, r.p0, Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0, r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('|');
function compare(a, b) {
  const ma = new Map(a.riders.map(r => [r.rq, key(r)])), mb = new Map(b.riders.map(r => [r.rq, key(r)]));
  let same = 0, firstDiff = '';
  const all = new Set([...ma.keys(), ...mb.keys()]);
  for (const rq of all) { if (ma.get(rq) === mb.get(rq)) same++; else if (!firstDiff) firstDiff = `${rq}: ${ma.get(rq)} vs ${mb.get(rq)}`; }
  const negC = a.riders.reduce((s, r) => s + (r.net < r.S ? 1 : 0), 0);
  return { riders: all.size, same, identical: same === all.size, firstDiff, ridersWithNegativeC_a: negC,
    servedA: a.riders.filter(r => r.served).length, servedB: b.riders.filter(r => r.served).length };
}

const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
if (cells.length !== 11) { console.error('expected 11 cells'); process.exit(4); }
const lines = ['test\tworld\tcell\tarmA\tarmB\triders\tsame\tidentical\tservedA\tservedB\tridersNegC_A\tfirstDiff'];
const tests = [];
for (const B of [120000, 300000]) {
  tests.push(['a', 'own', { name: 'RC', B }, { name: 'NC', B }]);
  tests.push(['a', 'own', { name: 'RC', B }, { name: 'OC', B }]);
  tests.push(['b', 'flat', { name: 'RC', B }, { name: 'DLHZ', B }]);
}
tests.push(['b', 'flat', { name: 'RCE', alpha: 0.35 }, { name: 'EOHZ', alpha: 0.35 }]);
tests.push(['b-control', 'own', { name: 'RC', B: 120000 }, { name: 'DLHZ', B: 120000 }]);
tests.push(['b-control', 'own', { name: 'RCE', alpha: 0.35 }, { name: 'EOHZ', alpha: 0.35 }]);
const lbl = a => a.alpha !== undefined ? `${a.name}${Math.round(a.alpha * 100)}` : `${a.name}-${a.B / 1000}`;
for (const cell of cells) {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  const PF = flat(P);
  const cache = new Map();
  const run = (world, arm) => { const k = world + lbl(arm); if (!cache.has(k)) cache.set(k, E.simulate(world === 'flat' ? PF : P, { arm })); return cache.get(k); };
  for (const [test, world, a, b] of tests) {
    const c = compare(run(world, a), run(world, b));
    lines.push([test, world, cell, lbl(a), lbl(b), c.riders, c.same, c.identical ? 1 : 0, c.servedA, c.servedB, c.ridersWithNegativeC_a, c.firstDiff].join('\t'));
  }
  console.log(cell, 'done');
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
console.log('rows', lines.length - 1);
