// wf17 r2-penalty-regime gate helper (wf17-exploratory-post-audit), adapted from the r1 key's cmp_rows.js: compare rows of a wf17 output
// with reference rows (wf14 main / a5 robust) on every field of the REFERENCE row except labels, timing and run-description fields
// (part, set, engine, ms, msScore, label, variant, mode, alpha, check). Rows are matched on key fields (default cell,config,arm).
// Usage: node gate17.js --name <gate> --mine <ndjson> --ref <ndjson> [--key cell,config,arm] [--filter-mine k=v,...] [--filter-ref k=v,...]
//        [--map-arm-ref EO=EO35,...] [--expect n] [--log <GATES.ndjson>]   -> prints a JSON summary; exit 1 on any mismatch or missing row
'use strict';
const fs = require('fs');
const read = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
const args = process.argv.slice(2);
const opt = k => { const i = args.indexOf('--' + k); return i >= 0 ? args[i + 1] : undefined; };
const keyF = (opt('key') || 'cell,config,arm').split(',');
const parseF = s => (s ? s.split(',').map(kv => kv.split('=')) : []);
const fm = parseF(opt('filter-mine')), fr = parseF(opt('filter-ref'));
const amap = Object.fromEntries(parseF(opt('map-arm-ref')));
const IGN = new Set(['part', 'set', 'engine', 'ms', 'msScore', 'label', 'variant', 'mode', 'alpha', 'check']);
const ok = (r, f) => f.every(([k, v]) => String(r[k]) === v);
const mine = read(opt('mine')).filter(r => ok(r, fm));
const ref = read(opt('ref')).filter(r => ok(r, fr)).map(r => (amap[r.arm] ? { ...r, arm: amap[r.arm] } : r));
const kOf = r => keyF.map(k => r[k]).join('|');
const R = new Map();
for (const r of ref) { if (R.has(kOf(r))) throw new Error('duplicate reference key ' + kOf(r)); R.set(kOf(r), r); }
let matched = 0, equal = 0, missing = 0; const diffs = [], missingKeys = [];
for (const m of mine) {
  const r = R.get(kOf(m));
  if (!r) { missing++; missingKeys.push(kOf(m)); continue; }
  matched++;
  const bad = Object.keys(r).filter(k => !IGN.has(k) && JSON.stringify(r[k]) !== JSON.stringify(m[k]));
  if (bad.length) diffs.push({ key: kOf(m), fields: bad.slice(0, 8), ex: bad.slice(0, 3).map(k => [k, r[k], m[k]]) }); else equal++;
}
const exp = opt('expect') ? Number(opt('expect')) : null;
const out = { gate: opt('name') || null, at: new Date().toISOString(), mine: mine.length, matched, equal, missing, mismatched: diffs.length, expect: exp,
  pass: diffs.length === 0 && missing === 0 && (exp === null || equal === exp), diffs: diffs.slice(0, 10), missingKeys: missingKeys.slice(0, 10),
  args: { filterMine: opt('filter-mine') || null, filterRef: opt('filter-ref') || null, ref: opt('ref') } };
console.log(JSON.stringify(out));
if (opt('log')) fs.appendFileSync(opt('log'), JSON.stringify(out) + '\n');
process.exit(out.pass ? 0 : 1);
