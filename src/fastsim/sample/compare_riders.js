// Rider-by-rider comparison of RideBound (C# Runner in FleetPy) with the fast simulator on the same cells: the check of
// thesis Section 5.2. Both inputs are rider tables with the same columns:
//   real       written by src/adapter/riders.py from the job folders;
//   predicted  written by src/fastsim/fullsys/emulate_fullsys.js adapter (or emulate_fullsys_f16.js).
// A rider is identical when the nine fields below are equal; a cell-arm is identical when every rider is.
// usage: node compare_riders.js <real riders.tsv> <predicted riders.tsv>
'use strict';
const fs = require('fs');

const FIELDS = ['t0', 'p0', 'A', 'served', 'sumAbsC', 'nonzeroC', 'netC', 'maxAbsC', 'shared'];

function read(file) {   // "role|cell" -> requestId -> row
  const lines = fs.readFileSync(file, 'utf8').trim().split(/\r?\n/);
  const head = lines.shift().split('\t');
  const out = new Map();
  for (const line of lines) {
    const r = Object.fromEntries(line.split('\t').map((v, i) => [head[i], v]));
    const key = r.role + '|' + r.cell;
    if (!out.has(key)) out.set(key, new Map());
    out.get(key).set(r.requestId, r);
  }
  return out;
}
// numbers are compared as numbers ("1203199" = "1203199.0"), anything else as text ("" for a rider never dropped off)
const same = (a, b) => (a !== '' && b !== '' && !isNaN(a) && !isNaN(b) ? Number(a) === Number(b) : a === b);

const [realFile, predFile] = process.argv.slice(2);
if (!realFile || !predFile) { console.error('usage: node compare_riders.js <real riders.tsv> <predicted riders.tsv>'); process.exit(2); }
const real = read(realFile), pred = read(predFile);
let riders = 0, identical = 0, cellArms = 0, identicalCellArms = 0;
for (const [key, R] of real) {
  const P = pred.get(key);
  if (!P) { console.log(`${key}: no predicted rows`); continue; }
  let ok = 0, onlyReal = 0;
  const differing = [];
  for (const [id, r] of R) {
    const p = P.get(id);
    if (!p) { onlyReal++; continue; }
    const bad = FIELDS.filter(f => !same(r[f], p[f]));
    if (bad.length) differing.push(`${id.slice(0, 16)}… ${bad.join(',')}`); else ok++;
  }
  const onlyPred = [...P.keys()].filter(id => !R.has(id)).length;
  cellArms++; riders += P.size + onlyReal; identical += ok;   // every rider of either side, as in Section 5.2
  const exact = ok === R.size && onlyPred === 0;
  if (exact) identicalCellArms++;
  console.log(`${key}: riders real ${R.size}, predicted ${P.size}; identical ${ok}; differing ${differing.length}; ` +
    `only real ${onlyReal}; only predicted ${onlyPred}${exact ? '  -> IDENTICAL' : ''}`);
  for (const d of differing.slice(0, 5)) console.log('    ' + d);
}
console.log(`total: ${identical}/${riders} riders identical on ${FIELDS.length} fields; ${identicalCellArms}/${cellArms} cell-arms identical`);
