// Compare the rows of a fast-simulator run on the sample cells with expected.tsv (the rows the thesis runs wrote for
// the same cells). A row matches when its signature `sig` (SHA-256 of every rider's id, served flag, vehicle, initial
// promise, arrival and promise revisions) equals the expected one.
// usage: node check_sample.js <expected.tsv> <driver> <rows folder> [<driver> <rows folder> ...]
'use strict';
const fs = require('fs');
const path = require('path');

const [expectedFile, ...pairs] = process.argv.slice(2);
if (!expectedFile || pairs.length === 0 || pairs.length % 2) {
  console.error('usage: node check_sample.js <expected.tsv> <driver> <rows folder> [<driver> <rows folder> ...]');
  process.exit(2);
}

// expected: driver -> "cell|config|arm|mode" -> set of signatures (one key can appear in two result families)
const expected = new Map();
const lines = fs.readFileSync(expectedFile, 'utf8').trim().split(/\r?\n/);
const head = lines.shift().split('\t');
for (const line of lines) {
  const r = Object.fromEntries(line.split('\t').map((v, i) => [head[i], v]));
  if (!expected.has(r.driver)) expected.set(r.driver, new Map());
  const key = [r.cell, r.config, r.arm, r.mode].join('|');
  if (!expected.get(r.driver).has(key)) expected.get(r.driver).set(key, new Set());
  expected.get(r.driver).get(key).add(r.sig);
}

let equal = 0, differ = 0, unknown = 0;
for (let i = 0; i < pairs.length; i += 2) {
  const [driver, dir] = [pairs[i], pairs[i + 1]];
  const want = expected.get(driver) || new Map();
  for (const f of fs.readdirSync(dir).filter(x => /^rows-\d+\.ndjson$/.test(x))) {
    for (const line of fs.readFileSync(path.join(dir, f), 'utf8').split('\n').filter(l => l.trim())) {
      const r = JSON.parse(line);
      const key = [r.cell, r.config, r.arm, r.mode].join('|');
      const sigs = want.get(key);
      if (!sigs) { unknown++; console.log(`  no expected row: ${driver} ${key}`); }
      else if (sigs.size === 1 && sigs.has(r.sig)) { equal++; console.log(`  equal   ${driver} ${key}  served ${r.served}, reversals ${r.visRev60}`); }
      else { differ++; console.log(`  DIFFERS ${driver} ${key}`); }
    }
  }
}
console.log(`sample check: ${equal} rows equal, ${differ} differ, ${unknown} without an expected row`);
process.exit(differ || unknown || equal === 0 ? 1 : 0);
