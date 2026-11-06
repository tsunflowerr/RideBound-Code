// Owner's request 2026-11-04: number of ETA reversals in the report's own setting (plain mode, engine17 of wf20/fullsys,
// F14 panel, the two final arms). The 168 cell-arms are simulated exactly as the plain gate did (emulate_fullsys.js
// optionsFor, LIB.RUN_OVERRIDES); each must reproduce the company row's rider fingerprint (sig, shcore.js:211) before its
// riders are written. Output: the same columns as emulate_sequences.js (served, rev60, tv5, dropSequence).
// Usage: node emulate_plain_sequences.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const FS = require('./emulate_fullsys.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf20/fullsys/code';
const SC = require(path.join(K, 'runner/scenario.js'));
const E17 = require(path.join(K, 'runner/engine17.js'));
const SH = require(path.join(K, 'scripts/shcore.js'));
const MET = require(path.join(K, 'scripts/metrics_sh.js'));
const LIB = require(path.join(K, 'scripts/shlib.js'));
const CELLS_DIR = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf14/scenarios/weekdays-P512';
const EXPECTED = 'E:/Code/RideBound-fastrun-run/research/continuation/2026-10-26/company-emulator/expected/wf20__fullsys__results__rows.tsv';

const out = process.argv[2];
if (!out || fs.existsSync(out)) { console.error('usage / output exists'); process.exit(2); }
const want = new Map();
const lines0 = fs.readFileSync(EXPECTED, 'utf8').trim().split(/\r?\n/), h = lines0[0].split('\t'), ix = k => h.indexOf(k);
for (const l of lines0.slice(1)) { const v = l.split('\t'); want.set([v[ix('cell')], v[ix('config')], v[ix('mode')], v[ix('arm')]].join('|'), v[ix('sig')]); }
const lines = ['role\tcell\trequestId\tserved\trev60\ttv5\tdropSequence'];
let ok = 0;
for (const c of FS.f14Cells()) {
  const rc = SH.readCell(path.join(CELLS_DIR, c.cell + '.json'));
  for (const arm of FS.ARMS) {
    const P = SC.prepareScenario(rc.s, LIB.RUN_OVERRIDES, rc.dir);
    const res = E17.simulate(P, FS.optionsFor(c, rc, P, null, arm).o);
    if (FS.sigOf(res) !== want.get([c.cell, FS.CONFIG, arm.fc, arm.key].join('|'))) throw new Error(`${c.cell} ${arm.role}: sig differs from the company row`);
    ok++;
    for (const r of res.riders) {
      const seq = [r.p0];
      for (const v of r.revs) if (v[7] !== seq[seq.length - 1]) seq.push(v[7]);
      let shown = r.p0, tv5 = 0;
      for (const v of r.revs) { const p = v[7]; if (Math.abs(p - shown) >= 5000) { tv5 += Math.abs(p - shown); shown = p; } }
      lines.push([arm.role, c.cell, r.rq, r.served ? 1 : 0, MET.hasReversal(r.zs, 60000) ? 1 : 0, tv5, JSON.stringify(seq)].join('\t'));
    }
    console.log(`${c.cell} ${arm.role}: sig equal to the company row, ${res.riders.length} riders`);
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} riders; ${ok} cell-arms equal to the company rows`);
