// Section 9 check "Via-zone pairs": re-emulates the Via-zone predictions (6 arms x the 2 cells of 2019-02-13, 62
// vehicles, maximum ride 2.0x), which were sealed before any RideBound job, and compares
//   - the riders TSV with the sealed file data/via/emulator-riders-via.tsv byte for byte (sha256 c0cd48de...),
//   - the counts TSV with data/via/emulator-riders-via-counts.tsv in every column except ms (wall time),
//   - the riders with the real RideBound riders data/via/runA3-final-real-riders.tsv (compare_riders.js rule).
// Traffic: each cell's explicit world-v1 base (worlds/<cell>.world.json, sigma 0, no shocks):
// f(b) = values[min(b, H)] / values[0], held after the last bin.
// Usage: node via_zone.js <cells dir> <out dir>    writes <out dir>/emulator-riders-via.tsv and -counts.tsv (never
// overwrites them)
'use strict';
const fs = require('fs'), path = require('path');
const F = require('./fixtures.js');
const E = require('../engine.js');
const { compareFiles, printReport } = require('./compare_riders.js');

const [cellsDir, outDir] = process.argv.slice(2);
if (!cellsDir || !outDir) { console.error('usage: node via_zone.js <cells dir> <out dir>'); process.exit(2); }
const DATA = path.join(__dirname, 'data', 'via');
const ridersFile = path.join(outDir, 'emulator-riders-via.tsv');
const countsFile = path.join(outDir, 'emulator-riders-via-counts.tsv');
for (const f of [ridersFile, countsFile]) {
  if (fs.existsSync(f)) { console.error('refused: ' + f + ' exists'); process.exit(3); }
}

const CELLS = ['d20190213-vz-r0-w08', 'd20190213-vz-r0-w17'];
const K8 = { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' };   // G2 with at most 8 receiving vehicles
const ARMS = [
  ['VIA-U', {}, { name: 'U', B: 0 }],
  ['VIA-RCE35', {}, { name: 'RCE', alpha: 0.35 }],
  ['VIA-EOHZ35', {}, { name: 'EOHZ', alpha: 0.35 }],
  ['VIA-G2K8-U', K8, { name: 'U', B: 0 }],
  ['VIA-G2K8-RCE35', K8, { name: 'RCE', alpha: 0.35 }],
  ['VIA-G2K8-EOHZ35', K8, { name: 'EOHZ', alpha: 0.35 }],
];

function worldProfile(cell) {
  const world = JSON.parse(fs.readFileSync(path.join(cellsDir, 'via-zone', 'worlds', cell + '.world.json'), 'utf8'));
  if (world.sigma !== 0 || world.shockRate !== 0) throw new Error('world must be noise-free');
  const values = world.base.values;
  if (!Array.isArray(values) || !values.length || values[0] !== 1) throw new Error('explicit base must start at 1');
  return values.map(v => v / values[0]);
}

const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tforcedVehicleDecisions\tvehicles\t' +
  'requests\tnodes\tms'];
for (const cell of CELLS) {
  const P = F.prepareFixtureCell(cell, path.join(cellsDir, 'via-zone', 'fixtures'), { profile: worldProfile(cell) });
  for (const [role, opts, arm] of ARMS) {
    const t = Date.now();
    const res = E.simulate(P, { ...opts, arm });
    const ms = Date.now() - t;
    for (const r of res.riders) {
      lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
        r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
    }
    const served = res.riders.filter(r => r.served).length;
    // forcedVehicleDecisions stays empty, as in the sealed file (its driver read a field the engine does not have)
    counts.push([role, cell, res.riders.length, served, res.rejected, res.diag.relocations, res.diag.genDecisions, '',
      P.fleet.length, P.reqs.length, P.N, ms].join('\t'));
    console.log(`${role} ${cell}: riders ${res.riders.length}, served ${served}, ` +
      `relocations ${res.diag.relocations}, decisions ${res.diag.genDecisions}, ${ms} ms`);
  }
}
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(ridersFile, lines.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(countsFile, counts.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${ridersFile}: ${lines.length - 1} rider rows`);

const sealedRiders = fs.readFileSync(path.join(DATA, 'emulator-riders-via.tsv'));
const ridersSame = sealedRiders.equals(fs.readFileSync(ridersFile));
const noMs = text => text.trim().split('\n').map(l => l.split('\t').slice(0, -1).join('\t')).join('\n');
const countsSame = noMs(fs.readFileSync(path.join(DATA, 'emulator-riders-via-counts.tsv'), 'utf8')) ===
  noMs(fs.readFileSync(countsFile, 'utf8'));
console.log(`riders TSV ${ridersSame ? 'identical to' : 'DIFFERENT from'} the sealed predictions; ` +
  `counts ${countsSame ? 'equal' : 'DIFFERENT'} except ms`);
const report = compareFiles(path.join(DATA, 'runA3-final-real-riders.tsv'), ridersFile);
printReport('Via-zone, real RideBound riders vs these predictions', report);
process.exitCode = ridersSame && countsSame && report.cellArmsExact === report.cellArmsCompared ? 0 : 1;
