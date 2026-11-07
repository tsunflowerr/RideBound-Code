// Section 9, Manhattan bridge: re-emulates the sealed bridge predictions (data/bridge/emulator-riders-{2000,elmi-2000,
// g2-2000}.tsv; 10 arms) on the 9 of the 11 bridge cells whose 1.5x base fixture is in commit ebca912, and compares
// every rider row with the sealed one as a string. The 2.0x bridge fixtures differ from their base only in
// maxRideTimeMs, so the base fixture with permille 2000 gives the same cell. Not re-run here: the other 2 cells (w17 of
// 2018-11-14 and 2018-11-18, fixtures never committed) and the phase-1 and R128 predictions; compare_riders.js still
// checks all 238 bridge pairs from the committed TSVs.
// Usage: node bridge_reemulate.js <cells dir>
'use strict';
const fs = require('fs'), path = require('path');
const F = require('./fixtures.js');
const E = require('../engine.js');

const cellsDir = process.argv[2];
if (!cellsDir) { console.error('usage: node bridge_reemulate.js <cells dir>'); process.exit(2); }
const CELLS = [ // bridge cell, panel, base cell
  ['d20181112-s10-r1-w08', 'wp14-development-panel-v1', 'd20181112-s10-r1-w08'],
  ['d20181112-s10-r1-w17', 'wp14-development-panel-v1', 'd20181112-s10-r1-w17'],
  ['d20181113-s10-r1-w08', 'wp14-development-panel-v1', 'd20181113-s10-r1-w08'],
  ['d20181113-s10-r1-w17', 'wp14-development-panel-v1', 'd20181113-s10-r1-w17'],
  ['d20181114-s10-r1-w08', 'wp9-confirmatory-fixed-panel-v2', 'd20181114-s10-r1'],
  ['d20181115-s10-r1-w08', 'wp9-confirmatory-fixed-panel-v2', 'd20181115-s10-r1'],
  ['d20181116-s10-r1-w08', 'wp9-confirmatory-fixed-panel-v2', 'd20181116-s10-r1'],
  ['d20181117-s10-r1-w08', 'wp9-confirmatory-fixed-panel-v2', 'd20181117-s10-r1'],
  ['d20181118-s10-r1-w08', 'wp9-confirmatory-fixed-panel-v2', 'd20181118-s10-r1']];
const G2 = { reopt: { g2: 99 }, lock: 'ratchet' };
const SETS = { // sealed file -> [role, options, arm]
  'emulator-riders-2000.tsv': [['U', {}, { name: 'U', B: 0 }], ['C-120', {}, { name: 'RC', B: 120000 }],
    ['C-300', {}, { name: 'RC', B: 300000 }], ['PS-300', {}, { name: 'PS', B: 300000 }],
    ['DLHZ-120', {}, { name: 'DLHZ', B: 120000 }]],
  'emulator-riders-elmi-2000.tsv': [['RCE35', {}, { name: 'RCE', alpha: 0.35 }],
    ['EOHZ35', {}, { name: 'EOHZ', alpha: 0.35 }]],
  'emulator-riders-g2-2000.tsv': [['G2-U', G2, { name: 'U', B: 0 }], ['G2-RCE35', G2, { name: 'RCE', alpha: 0.35 }],
    ['G2-EOHZ35', G2, { name: 'EOHZ', alpha: 0.35 }]],
};

let pairs = 0, pairsExact = 0, rows = 0, rowsEqual = 0;
for (const [file, arms] of Object.entries(SETS)) {
  const sealed = new Map();
  const text = fs.readFileSync(path.join(__dirname, 'data', 'bridge', file), 'utf8');
  for (const l of text.trim().split(/\r?\n/).slice(1)) {
    const f = l.split('\t'), k = f[0] + '|' + f[1];
    if (!sealed.has(k)) sealed.set(k, []);
    sealed.get(k).push(l);
  }
  for (const [cell, panel, base] of CELLS) {
    const P = F.prepareFixtureCell(base, path.join(cellsDir, panel), { permille: 2000 });
    for (const [role, opts, arm] of arms) {
      const res = E.simulate(P, { ...opts, arm });
      const mine = res.riders.map(r => [role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '',
        r.served ? 1 : 0, r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
      const ref = sealed.get(role + '|' + cell) || [];
      const eq = mine.filter((l, i) => l === ref[i]).length;
      pairs++; rows += ref.length; rowsEqual += eq;
      if (eq === ref.length && mine.length === ref.length) pairsExact++;
      else console.log('DIFF', file, role, cell, 'mine', mine.length, 'sealed', ref.length, 'equal', eq);
    }
  }
}
console.log(`${pairsExact}/${pairs} cell-arms exact, ${rowsEqual}/${rows} sealed rider rows equal ` +
  '(9 of 11 bridge cells)');
process.exitCode = pairsExact === pairs && pairs === 90 ? 0 : 1;
