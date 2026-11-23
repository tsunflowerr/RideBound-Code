'use strict';
// usage: node compare_sig.js <newRowsDir> <storedRowsDir> <label>
const fs = require('fs');
function load(dir) {
  const m = new Map();
  for (const f of fs.readdirSync(dir).filter(f => /^rows-.*\.ndjson$/.test(f)))
    for (const l of fs.readFileSync(dir + '/' + f, 'utf8').split('\n')) {
      if (!l.trim()) continue; let r; try { r = JSON.parse(l); } catch (e) { continue; }
      m.set([r.cell, r.config, r.arm, r.mode].join('|'), r);
    }
  return m;
}
const [, , nd, sd, label] = process.argv;
const N = load(nd), S = load(sd);
let eq = 0, diff = 0, nostore = 0;
for (const [k, r] of N) {
  const s = S.get(k);
  if (!s) { nostore++; console.log('  not in stored rows:', k); continue; }
  if (s.sig === r.sig) eq++; else { diff++; console.log('  SIG DIFFERS:', k); }
}
console.log(`  ${label}: new rows ${N.size}; sig equal to stored ${eq}; differ ${diff}; not found in stored ${nostore}`);
