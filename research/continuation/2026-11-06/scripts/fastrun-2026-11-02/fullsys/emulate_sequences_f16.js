// F16 COPY (2026-11-05) of emulate_sequences.js, made by make_f16_scripts.py with counted exact replacements only: the cell
// directory (wf17 F16 cells, 292/292 sha256 = expected/wf17-f16-cells.sha256), the panel (F16) and, in
// emulate_fullsys, the lambda prior file (the F16 prior of wf20/fullsys/RUNBOOK.sh: work/prior_F16/prior_prevhour.csv).
// ADR-089 GĐ4/GĐ6, added 2026-11-04 after the owner's question: the headline metric of the report is the promise reversal
// (visRev60, riders whose shown drop ETA moved up then down or down then up, each move >= 60 s) together with TV5. Both are
// functions of each served rider's published drop-ETA sequence only (metrics_sh.js:47-60), which the sealed rider TSV
// (FULLSYS-F14-PREDICTIONS.sha256) does not hold. This script simulates the same 168 cell-arms again with the sealed
// emulate_fullsys.js (unchanged; only its exports are used): every sealed rider field must come out equal (determinism
// check), and per rider it writes the published drop-ETA sequence (p0, then p of every revision; consecutive repeats
// dropped), rev60 = metrics_sh.hasReversal(r.zs, 60000) and tv5 (metrics_sh.js:58-59). Sealed before any RideBound job.
// Usage: node emulate_sequences.js <data root> <sealed predictions.tsv> <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const FS = require('./emulate_fullsys_f16.js');
const ECL = require('./engine_wf15_clint.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf20/fullsys/code';
const SH = require(path.join(K, 'scripts/shcore.js'));
const MET = require(path.join(K, 'scripts/metrics_sh.js'));
const CELLS_DIR = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf17/r1-identity-fence-pad/cells/f16/weekdays-P512';
const vfName = c => `d${c.day.replace(/-/g, '')}-vf-r${c.rs}-${c.win}`;

const [root, sealedTsv, out] = process.argv.slice(2);
if (!root || !sealedTsv || !out || fs.existsSync(out)) { console.error('usage / output exists'); process.exit(2); }
const sealed = new Map();
const tsv = fs.readFileSync(sealedTsv, 'utf8').trim().split(/\r?\n/);
const head = tsv[0].split('\t');
for (const line of tsv.slice(1)) {
  const v = line.split('\t'), row = Object.fromEntries(head.map((k, i) => [k, v[i]]));
  sealed.set([row.role, row.cell, row.requestId].join('|'), row);
}
const lines = ['role\tcell\trequestId\tserved\trev60\ttv5\tdropSequence'];
let checked = 0;
for (const c of FS.f16Cells()) {
  const name = vfName(c);
  const rc = SH.readCell(path.join(CELLS_DIR, c.cell + '.json'));
  const P = FS.prepareViaCell(name, root);
  const globalOf = P.meta.nodeIdsFixture.map(id => Number(/^node-(\d{6})$/.exec(id)[1]));
  for (const arm of FS.ARMS) {
    const res = ECL.simulate(P, FS.optionsFor(c, rc, P, globalOf, arm).o);
    let riders = 0;
    for (const r of res.riders) {
      const s = sealed.get([arm.role, name, r.rq].join('|'));
      const now = [r.t0, r.p0, Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0, r.S, r.k, r.net, r.M, r.shared ? 1 : 0, r.sS, r.aSH, r.bSH].map(String);
      const was = s && [s.t0, s.p0, s.A, s.served, s.sumAbsC, s.nonzeroC, s.netC, s.maxAbsC, s.shared, s.sS, s.aSH, s.bSH];
      if (!was || now.some((x, i) => x !== was[i])) throw new Error(`${arm.role} ${name} ${r.rq}: differs from the sealed rider row`);
      checked++; riders++;
      const seq = [r.p0];
      for (const v of r.revs) if (v[7] !== seq[seq.length - 1]) seq.push(v[7]);
      let shown = r.p0, tv5 = 0;
      for (const v of r.revs) { const p = v[7]; if (Math.abs(p - shown) >= 5000) { tv5 += Math.abs(p - shown); shown = p; } }
      lines.push([arm.role, name, r.rq, r.served ? 1 : 0, MET.hasReversal(r.zs, 60000) ? 1 : 0, tv5, JSON.stringify(seq)].join('\t'));
    }
    const sealedCount = [...sealed.keys()].filter(k => k.startsWith(arm.role + '|' + name + '|')).length;
    if (riders !== sealedCount) throw new Error(`${arm.role} ${name}: ${riders} riders, sealed ${sealedCount}`);
    console.log(`${arm.role} ${name}: ${riders} riders equal to the sealed rows`);
  }
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} riders; ${checked} rider rows equal to the sealed predictions`);
