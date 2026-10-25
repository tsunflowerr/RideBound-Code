// Phase 1.0: does the company wf15 engine reproduce the sealed bridge predictions?
// Re-creates, with the wf15 engine (local copy C:/RideBoundContinuation/2026-10-23/emulator-wf15), the three sealed
// emulator files of the Manhattan bridge, with the same layout, row order and role names as the original drivers:
//   ins  -> emulator-riders-2000.tsv        (bridge_emulate.js:      U, C-120, C-300, PS-300, DLHZ-120)
//   elmi -> emulator-riders-elmi-2000.tsv   (bridge_emulate_elmi.js: RCE35, EOHZ35)
//   g2   -> emulator-riders-g2-2000.tsv and -counts.tsv (bridge_emulate_g2.js: G2-U, G2-RCE35, G2-EOHZ35)
// The gate is byte equality (sha256) with the sealed files. Only the arm spelling differs: wf15 takes the Elmi share as
// alpha = 0.35 where the bridge emulator took permille 350 (the same double).
// Usage: node emulate_wf15_equiv.js <outDir>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-23/emulator-wf15';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));

const FIXTURES = 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1';
const HEADER = 'role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared';
const G2 = { reopt: { g2: 99 }, lock: 'ratchet' };
const SETS = {
  ins: { file: 'emulator-riders-2000.tsv', arms: [
    ['U', {}, { name: 'U', B: 0 }], ['C-120', {}, { name: 'RC', B: 120000 }], ['C-300', {}, { name: 'RC', B: 300000 }],
    ['PS-300', {}, { name: 'PS', B: 300000 }], ['DLHZ-120', {}, { name: 'DLHZ', B: 120000 }]] },
  elmi: { file: 'emulator-riders-elmi-2000.tsv', arms: [
    ['RCE35', {}, { name: 'RCE', alpha: 0.35 }], ['EOHZ35', {}, { name: 'EOHZ', alpha: 0.35 }]] },
  g2: { file: 'emulator-riders-g2-2000.tsv', counts: true, arms: [
    ['G2-U', G2, { name: 'U', B: 0 }], ['G2-RCE35', G2, { name: 'RCE', alpha: 0.35 }],
    ['G2-EOHZ35', G2, { name: 'EOHZ', alpha: 0.35 }]] },
};

const outDir = process.argv[2];
if (!outDir) { console.error('usage: node emulate_wf15_equiv.js <outDir>'); process.exit(2); }
if (fs.existsSync(outDir)) { console.error('refused: ' + outDir + ' exists'); process.exit(3); }
fs.mkdirSync(outDir, { recursive: true });
const cells = fs.readdirSync(FIXTURES).filter(c => /^d\d{8}-s10-r1-w(08|17)$/.test(c)).sort();
if (cells.length !== 11) { console.error('expected 11 cells, found ' + cells.length); process.exit(4); }
const prepared = cells.map(cell => {
  const P = F.prepareFixtureCell(cell, { variant: { panelRoot: FIXTURES } });
  if (P.meta.permille !== 2000) throw new Error(cell + ': permille ' + P.meta.permille);
  return [cell, P];
});
for (const [name, set] of Object.entries(SETS)) {
  const lines = [HEADER];
  const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions'];
  for (const [cell, P] of prepared) {
    for (const [role, opts, arm] of set.arms) {
      const res = E.simulate(P, { ...opts, arm });
      for (const r of res.riders) {
        lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
          r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
      }
      if (set.counts) {
        counts.push([role, cell, res.riders.length, res.riders.filter(r => r.served).length, res.rejected,
          res.diag ? res.diag.relocations : undefined, res.diag ? res.diag.genDecisions : undefined].join('\t'));
      }
    }
  }
  fs.writeFileSync(path.join(outDir, set.file), lines.join('\n') + '\n', { flag: 'wx' });
  if (set.counts) fs.writeFileSync(path.join(outDir, set.file.replace(/\.tsv$/, '-counts.tsv')), counts.join('\n') + '\n', { flag: 'wx' });
  console.log(`${name}: ${lines.length - 1} rider rows`);
}
