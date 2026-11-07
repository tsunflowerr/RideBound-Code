// Section 9: real RideBound riders against the emulator predictions. A Node port of the comparison in compare_via.py
// (kit via-real-2026-10-26 at commit ebca912; bridge_compare.py uses the same rule).
// Rule: rows are grouped by (role, cell) and keyed by requestId. A rider is identical when both sides list it and t0,
// p0, A, served, sumAbsC, nonzeroC, netC, maxAbsC and shared are equal after norm(); a cell-arm is exact when every
// rider of either side is identical. Not ported: reading the riders out of the real run folders (the real-riders TSVs
// in data/ are that output) and the G2 move counts, which need those folders.
// Usage:
//   node compare_riders.js                      every section 9 pair from data/: the 238 Manhattan bridge pairs and the
//                                               6 Via-zone pairs, each against its sealed predictions
//   node compare_riders.js <real.tsv> <emulator.tsv> [--json <out.json>]
//                                               one comparison (the json file is never overwritten)
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');

const DATA = path.join(__dirname, 'data');
const FIELDS = ['t0', 'p0', 'A', 'served', 'sumAbsC', 'nonzeroC', 'netC', 'maxAbsC', 'shared'];

// compare_via.py norm(): '' for an empty or NaN value; a plain decimal number (digits, at most one '.', leading '-')
// becomes its integer part, as Python str(int(float(v))) does; anything else is compared as written.
function norm(v) {
  v = v.trim();
  if (v === '' || v === 'NaN' || v === 'nan' || v === 'undefined') return '';
  return /^\d+$/.test(v.replace(/^-+/, '').replace('.', '')) ? BigInt(Math.trunc(Number(v))).toString() : v;
}

// Python int() of a t0 string (orders the riders in which the first difference is searched).
function toInt(s) {
  if (!/^\s*[+-]?\d+\s*$/.test(s)) throw new Error('t0 is not an integer: ' + s);
  return Number(s);
}

// A tab-separated file with a header line (no quoting) as [{role, cell, rows: Map(requestId -> row)}], sorted by
// role and then cell.
function readGroups(file) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).filter(l => l !== '');
  const header = lines[0].split('\t');
  const groups = new Map();
  for (const line of lines.slice(1)) {
    const f = line.split('\t');
    const row = Object.fromEntries(header.map((h, i) => [h, f[i]]));
    const key = row.role + '\t' + row.cell;
    if (!groups.has(key)) groups.set(key, { role: row.role, cell: row.cell, rows: new Map() });
    groups.get(key).rows.set(row.requestId, row);
  }
  const byText = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
  return [...groups.values()].sort((a, b) => byText(a.role, b.role) || byText(a.cell, b.cell));
}

// emulatorRole: { real role: emulator role } for a real arm compared with the predictions of another role.
function compareFiles(realFile, emulatorFile, emulatorRole = {}) {
  const emu = new Map(readGroups(emulatorFile).map(g => [g.role + '\t' + g.cell, g.rows]));
  const results = [];
  for (const { role, cell, rows: rr } of readGroups(realFile)) {
    const ee = emu.get((emulatorRole[role] || role) + '\t' + cell) || new Map();
    const onlyReal = [...rr.keys()].filter(q => !ee.has(q)).length;
    const onlyEmulator = [...ee.keys()].filter(q => !rr.has(q)).length;
    const both = [...rr.keys()].filter(q => ee.has(q)).map(q => [toInt(rr.get(q).t0), q])
      .sort((a, b) => a[0] - b[0] || (a[1] < b[1] ? -1 : a[1] > b[1] ? 1 : 0));
    let identical = 0, firstDifference = null;
    for (const [, q] of both) {
      const a = rr.get(q), b = ee.get(q);
      const diffs = FIELDS.filter(f => norm(a[f]) !== norm(b[f]));
      if (diffs.length && firstDifference === null) {
        firstDifference = { requestId: q, t0: a.t0, fields: Object.fromEntries(diffs.map(f => [f, [a[f], b[f]]])) };
      }
      if (!diffs.length) identical++;
    }
    const riders = rr.size + onlyEmulator;
    results.push({ arm: role, cell, riders, identical, exact: identical === riders && !onlyReal && !onlyEmulator,
      onlyReal, onlyEmulator, firstDifference });
  }
  const byArm = {};
  for (const r of results) {
    byArm[r.arm] = byArm[r.arm] || { cellArms: 0, exact: 0 };
    byArm[r.arm].cellArms++;
    byArm[r.arm].exact += r.exact ? 1 : 0;
  }
  const sum = k => results.reduce((s, r) => s + r[k], 0);
  return { cellArmsCompared: results.length, cellArmsExact: results.filter(r => r.exact).length,
    riders: sum('riders'), ridersIdentical: sum('identical'), byArm, results };
}

function printReport(label, report, perArm = false) {
  console.log(`${label}: cell-arms exact ${report.cellArmsExact}/${report.cellArmsCompared}; ` +
    `riders identical ${report.ridersIdentical}/${report.riders}`);
  for (const [arm, v] of perArm ? Object.entries(report.byArm) : []) {
    console.log(`  ${arm.padEnd(16)} exact ${v.exact}/${v.cellArms}`);
  }
  for (const r of report.results.filter(x => !x.exact)) {
    console.log(`  CHECK ${r.arm} ${r.cell}: ${r.identical}/${r.riders} riders, onlyReal ${r.onlyReal}, ` +
      `onlyEmu ${r.onlyEmulator}, first ${JSON.stringify(r.firstDifference).slice(0, 300)}`);
  }
}

// sha256 of the emulator predictions as sealed before any matching RideBound job (the EMULATOR-PREDICTIONS*.sha256 and
// VIA-PREDICTIONS.sha256 seal files at ebca912).
const SEALED = {
  'bridge/emulator-riders-2000.tsv': '06742da62c45af96c1310b85a811088fbaaa7ad9674f2e068557592a8a13d17a',
  'bridge/emulator-riders-elmi-2000.tsv': 'e42c7e2d344e86a27f7ef0992ee7474c561eb1a871f63be531055e0a496d54b1',
  'bridge/emulator-riders-g2-2000.tsv': '447b6e4795a61a4e63603e87e335b3ce6a2e081b32b6b02f9672fce84f4dc1c2',
  'bridge/emulator-riders-p1.tsv': '2083d28236f2e9bad5bb589dc0058fe58fea65f7487a64ea2c413e7b0f1ede7a',
  'bridge/emulator-riders-r128.tsv': 'd8aa4afd2515a9f3c76139efe418dfd376d659a275f6174e0cedc520e5cab1c5',
  'via/emulator-riders-via.tsv': 'c0cd48de54cab3565c78a7e86f20351a2d6b7aab610ddfeaeabf39ceba40e53d',
};
// The Manhattan bridge: 11 cells (2018-11-12..18, 08:00 and 17:00), 238 (arm, cell) pairs. A pair that a later family
// compares again (the R128 G2K8 reruns after the adapter fix) is counted once; one R128 pair exists only in the reruns.
// label, real riders, sealed predictions, and the emulator role of a real role that has no predictions of its own
const BRIDGE = [
  ['insertion-only U C-120 C-300 PS-300 DLHZ-120', 'final-55-2026-10-22-real-riders.tsv', 'emulator-riders-2000.tsv'],
  ['Elmi RCE35 EOHZ35', 'elmi-22-2026-10-23-real-riders.tsv', 'emulator-riders-elmi-2000.tsv'],
  ['G2-U G2-RCE35 G2-EOHZ35', 'g2all-v4-2026-10-23-real-riders.tsv', 'emulator-riders-g2-2000.tsv'],
  ['forced reference EO35 DLS-120 (+G2), G2K3', 'p1all-2026-10-23-real-riders.tsv', 'emulator-riders-p1.tsv'],
  ['G2K3-U smoke after the adapter fix', 'fixsmoke-diag-2026-10-24-real-riders.tsv', 'emulator-riders-p1.tsv'],
  ['R128 (128 requests, 10 vehicles)', 'r128v2all-2026-10-24-real-riders.tsv', 'emulator-riders-r128.tsv'],
  ['R128 G2K8 reruns after the adapter fix', 'fix1-r128-2026-10-24-real-riders.tsv', 'emulator-riders-r128.tsv'],
  ['DLS-120-NW against the DLHZ-120 predictions', 'nw-final-2026-10-25-real-riders.tsv', 'emulator-riders-2000.tsv',
    { 'DLS-120-NW': 'DLHZ-120' }],
];

function checkSeal(file) {
  const sha = crypto.createHash('sha256').update(fs.readFileSync(path.join(DATA, file))).digest('hex');
  if (sha !== SEALED[file]) throw new Error(`${file} changed since sealing (sha256 ${sha})`);
}

function allSection9() {
  const pairs = new Map();   // "arm \t cell" -> { riders, identical } of its first family, exact in every family
  for (const [label, realFile, emulatorFile, emulatorRole] of BRIDGE) {
    checkSeal('bridge/' + emulatorFile);
    const report = compareFiles(path.join(DATA, 'bridge', realFile), path.join(DATA, 'bridge', emulatorFile),
      emulatorRole);
    printReport(label, report);
    for (const r of report.results) {
      const key = r.arm + '\t' + r.cell, seen = pairs.get(key);
      pairs.set(key, seen ? { ...seen, exact: seen.exact && r.exact }
        : { riders: r.riders, identical: r.identical, exact: r.exact });
    }
  }
  const all = [...pairs.values()], exact = all.filter(p => p.exact).length;
  const riders = all.reduce((s, p) => s + p.riders, 0), identical = all.reduce((s, p) => s + p.identical, 0);
  console.log(`Manhattan bridge: ${exact}/${all.length} distinct cell-arm pairs exact; ` +
    `riders identical ${identical}/${riders}`);
  checkSeal('via/emulator-riders-via.tsv');
  const via = compareFiles(path.join(DATA, 'via', 'runA3-final-real-riders.tsv'),
    path.join(DATA, 'via', 'emulator-riders-via.tsv'));
  printReport('Via-zone', via, true);
  return exact === all.length && all.length === 238 && via.cellArmsExact === via.cellArmsCompared &&
    via.cellArmsCompared === 6;
}

function main() {
  const args = process.argv.slice(2);
  const j = args.indexOf('--json');
  const jsonOut = j >= 0 ? args.splice(j, 2)[1] : null;
  if (args.length === 0 && j < 0) { process.exitCode = allSection9() ? 0 : 1; return; }
  if (args.length !== 2 || (j >= 0 && !jsonOut)) {
    console.error('usage: node compare_riders.js [<real.tsv> <emulator.tsv> [--json <out.json>]]');
    process.exit(2);
  }
  const report = compareFiles(args[0], args[1]);
  printReport(path.basename(args[0]), report, true);
  if (jsonOut) fs.writeFileSync(jsonOut, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
  process.exitCode = report.cellArmsExact === report.cellArmsCompared ? 0 : 1;
}

if (require.main === module) main();
module.exports = { compareFiles, printReport };
