// wf17 r2-penalty-regime post-run gates (wf17-exploratory-post-audit; RUNPLAN R4 / R5 gates). Same comparison rule as gate17.js: every
// field of the REFERENCE row except labels, timing and run-description fields. Rows matched on cell|config|arm (reference arm names mapped).
// Usage: node postgates.js R4|R5   -> appends to work/gates/GATES.ndjson, prints one JSON line per gate; exit 1 if any gate fails
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const KEY = path.resolve(__dirname, '..', '..');
const read = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
const IGN = new Set(['part', 'set', 'engine', 'ms', 'msScore', 'label', 'variant', 'mode', 'alpha', 'check']);
const AMAP = { EO: 'EO35', EOHZ: 'EOHZ35', RCE: 'RCE35' };
const PAN = JSON.parse(fs.readFileSync(path.join(KEY, 'work/panels.json'), 'utf8'));
const D30 = new Set(PAN.D30);
const W14 = read(path.join(SP, 'wf14/results/main.ndjson')).filter(r => r.part === 'M' && r.mode === 'N' && r.rs === 'r0');
const A5 = read(path.join(SP, 'wf16/a5-steering/results/robust.ndjson')).map(r => ({ ...r, arm: AMAP[r.arm] || r.arm }));
const kOf = r => [r.cell, r.config, r.arm].join('|');
const results = [];
function gate(name, mine, ref, expect) {
  const R = new Map(ref.map(r => [kOf(r), r]));
  let equal = 0, missing = 0; const diffs = [];
  for (const m of mine) {
    const r = R.get(kOf(m));
    if (!r) { missing++; continue; }
    const bad = Object.keys(r).filter(k => !IGN.has(k) && JSON.stringify(r[k]) !== JSON.stringify(m[k]));
    if (bad.length) diffs.push({ key: kOf(m), fields: bad.slice(0, 6) }); else equal++;
  }
  const out = { gate: name, at: new Date().toISOString(), mine: mine.length, equal, missing, mismatched: diffs.length, expect, pass: !diffs.length && !missing && equal === expect, diffs: diffs.slice(0, 5) };
  results.push(out); console.log(JSON.stringify(out));
}
const check = process.argv[2];
if (check === 'R4') {
  const M = read(path.join(KEY, 'results/r4.ndjson'));
  gate('R4 post: T8P0 rows (5 arms x 120 cells) vs wf14 main G2 r0', M.filter(r => r.variant === 'T8P0'), W14.filter(r => r.config === 'G2'), 600);
  for (const [v, a5v] of [['T8P60', 'V11_g2pen60'], ['T8P300', 'V12_g2pen300']])
    gate(`R4 post (extra): ${v} EO35/EOHZ35/RCE35 on D30 cells vs a5 ${a5v}`, M.filter(r => r.variant === v && D30.has(r.day) && ['EO35', 'EOHZ35', 'RCE35'].includes(r.arm)), A5.filter(r => r.variant === a5v), 180);
} else if (check === 'R5') {
  const M = read(path.join(KEY, 'results/r5.ndjson'));
  gate('R5 post: B0 rows (G2 4 arms + INS 2 arms x 60 cells) vs wf14 main r0', M.filter(r => r.variant === 'B0'), W14, 360);
  for (const [v, a5v] of [['V-R15', 'V1_ride15'], ['V-R25', 'V2_ride25']])
    gate(`R5 post: ${v} EOHZ35/RCE35 (INS + G2, 60 cells) vs a5 ${a5v}`, M.filter(r => r.variant === v && ['EOHZ35', 'RCE35'].includes(r.arm)), A5.filter(r => r.variant === a5v), 240);
  const R4f = path.join(KEY, 'results/r4.ndjson');
  if (fs.existsSync(R4f)) gate('R5 post (extra): B0 G2 rows vs R4 T8P0 rows on the same D30 cells', M.filter(r => r.variant === 'B0' && r.config === 'G2'), read(R4f).filter(r => r.variant === 'T8P0' && D30.has(r.day)), 240);
} else throw new Error('usage: node postgates.js R4|R5');
fs.appendFileSync(path.join(KEY, 'work/gates/GATES.ndjson'), results.map(r => JSON.stringify(r)).join('\n') + '\n');
process.exit(results.every(r => r.pass) ? 0 : 1);
