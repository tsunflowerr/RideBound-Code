// wf21/n7 gate checker (label wf21-n7-exploratory-post-audit). Prints only match counts (no metric values).
// Usage: node gcheck7.js --rows <f> --out <txt> [--post] [--det <gate_rows.ndjson>] [--calib <calib.ndjson>]
//  G1  every N / CL row (variant null) equals the wf20 row (same panel|cell|config|mode|arm) on EVERY wf20 field except label, ms,
//      msScore; and carries exactly the wf20 field set plus the wf21 fields.
//  G2  PAD rows with variant 'gate-r1' (flatRatio 1) equal the N row of the same cell|config|arm on sigR3 and on every wf20 field
//      except label, ms, msScore, mode, variant, sig, sigX, fc (the shown pickup ETA is clipped under PAD by design, as CL; wf17 G-R3d).
//  G3  CLc rows with variant 'gate-oldidx' (CLc code path, OLD-rule per-target index) equal the CL row on every field except label,
//      ms, msScore, mode, fcIndex, variant.
//  G4  every SHRC row: breachLedger = G_ledgerBreach = A_ledgerBreach = 0.   G5  x = 72 (INS) / 71 (G2) on every row.
//  G8  plN = promised and plServedN = served on every row.
//  --post adds:
//  G6  production CLc rows on the 6 days whose causal prev4 profile is bit-identical to CL's (2019-10-02, -03, -07, -08, 2019-12-04,
//      -05; cidx/results/profile_diff.txt) equal the CL rows on every field except label, ms, msScore, mode, fcIndex.
//  G7  completeness: 180 cells per production mode x config x arm (variant null; PADc rows: variant 'PADc'), no duplicate keys.
//  G9  determinism: rows of --det equal production rows (same mode/variant/flatRatio/fcIndex) except ms/msScore; production PAD
//      rows equal the --calib rows at the same flatRatio except variant, label, ms, msScore.
'use strict';
const fs = require('fs');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const read = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
const rows = read(arg('rows'));
const W20 = read(SP + '/wf20/fullsys/results/rows.ndjson');
const NEWF = ['flatRatio', 'fcIndex', 'plN', 'plSumArr_ms', 'plSumT0_ms', 'plServedN', 'plServedSumArr_ms', 'early60First', 'early60FirstG'];
const k5 = r => [r.panel, r.cell, r.config, r.mode, r.arm].join('|');
const w20 = new Map(W20.map(r => [k5(r), r]));
const W20KEYS = Object.keys(W20[0]);
const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const cmp = (a, b, keys, skip) => keys.filter(k => !skip.includes(k) && !eq(a[k], b[k]));
const G = [];
const isProd = r => r.variant === null || r.variant === undefined;
// G1
{ let n = 0, bad = 0, keyBad = 0, missing = 0; const ex = [];
  for (const r of rows) { if (!isProd(r) || !['N', 'CL'].includes(r.mode)) continue; n++;
    const o = w20.get(k5(r)); if (!o) { missing++; continue; }
    const d = cmp(r, o, W20KEYS, ['label', 'ms', 'msScore']); if (d.length) { bad++; if (ex.length < 3) ex.push(k5(r) + ' ' + d.join(',')); }
    const ks = Object.keys(r).sort().join(','), want = [...W20KEYS, ...NEWF].sort().join(','); if (ks !== want) keyBad++; }
  G.push(`G1 N/CL rows == wf20 rows on every wf20 field (except label, ms, msScore): ${n - bad - missing}/${n} equal, missing ${missing}, field-set mismatches ${keyBad} -> ${n > 0 && !bad && !missing && !keyBad ? 'PASS' : 'FAIL'} ${ex.join(' | ')}`); }
const k4 = r => [r.panel, r.cell, r.config, r.arm].join('|');
const byMode = (mode, pred) => new Map(rows.filter(r => r.mode === mode && pred(r)).map(r => [k4(r), r]));
const Nrows = byMode('N', isProd), CLrows = byMode('CL', isProd);
// G2
{ const P = rows.filter(r => r.mode === 'PAD' && r.variant === 'gate-r1'); let bad = 0, miss = 0; const ex = [];
  for (const r of P) { const o = Nrows.get(k4(r)) || w20.get([r.panel, r.cell, r.config, 'N', r.arm].join('|')); if (!o) { miss++; continue; }
    const d = cmp(r, o, W20KEYS, ['label', 'ms', 'msScore', 'mode', 'sig', 'sigX', 'fc', 'variant']); if (!eq(r.sigR3, o.sigR3)) d.push('sigR3'); if (r.flatRatio !== 1) d.push('flatRatio');
    if (d.length) { bad++; if (ex.length < 3) ex.push(k4(r) + ' ' + d.join(',')); } }
  G.push(`G2 PAD(r=1) == N (sigR3 + every wf20 field except label, ms, msScore, mode, sig, sigX, fc): ${P.length - bad - miss}/${P.length} -> ${P.length && !bad && !miss ? 'PASS' : P.length ? 'FAIL' : 'n/a'} ${ex.join(' | ')}`); }
// G3
{ const P = rows.filter(r => r.mode === 'CLc' && r.variant === 'gate-oldidx'); let bad = 0, miss = 0; const ex = [];
  for (const r of P) { const o = CLrows.get(k4(r)) || w20.get([r.panel, r.cell, r.config, 'CL', r.arm].join('|')); if (!o) { miss++; continue; }
    const d = cmp(r, o, W20KEYS, ['label', 'ms', 'msScore', 'mode', 'variant']); if (d.length) { bad++; if (ex.length < 3) ex.push(k4(r) + ' ' + d.join(',')); } }
  G.push(`G3 CLc path with OLD-rule index == CL (every field except label, ms, msScore, mode, fcIndex, variant): ${P.length - bad - miss}/${P.length} -> ${P.length && !bad && !miss ? 'PASS' : P.length ? 'FAIL' : 'n/a'} ${ex.join(' | ')}`); }
// G4, G5, G8
{ const S = rows.filter(r => /^SHRC/.test(r.arm)); const bad = S.filter(r => r.breachLedger !== 0 || r.G_ledgerBreach !== 0 || r.A_ledgerBreach !== 0).length;
  G.push(`G4 SHRC ledger breaches = 0: ${S.length - bad}/${S.length} -> ${!bad ? 'PASS' : 'FAIL'}`);
  const xb = rows.filter(r => r.x !== (r.config === 'INS' ? 72 : 71) / 100 && r.x !== (r.config === 'INS' ? 72 : 71)).length;
  G.push(`G5 x* = 72 % INS / 71 % G2: ${rows.length - xb}/${rows.length} -> ${!xb ? 'PASS' : 'FAIL'}`);
  const pb = rows.filter(r => r.plN !== r.promised || r.plServedN !== r.served).length;
  G.push(`G8 plN = promised, plServedN = served: ${rows.length - pb}/${rows.length} -> ${!pb ? 'PASS' : 'FAIL'}`); }
if (process.argv.includes('--post')) {
  // G6
  { const SAME = new Set(['2019-10-02', '2019-10-03', '2019-10-07', '2019-10-08', '2019-12-04', '2019-12-05']);
    const P = rows.filter(r => r.mode === 'CLc' && isProd(r) && SAME.has(r.day)); let bad = 0, miss = 0; const ex = [];
    for (const r of P) { const o = CLrows.get(k4(r)); if (!o) { miss++; continue; } const d = cmp(r, o, [...W20KEYS, 'plSumArr_ms', 'plSumT0_ms', 'early60First'], ['label', 'ms', 'msScore', 'mode']); if (d.length) { bad++; if (ex.length < 3) ex.push(k4(r) + ' ' + d.join(',')); } }
    G.push(`G6 CLc == CL on the 6 identical-profile days: ${P.length - bad - miss}/${P.length} (expected 144) -> ${P.length === 144 && !bad && !miss ? 'PASS' : 'FAIL'} ${ex.join(' | ')}`); }
  // G7
  { const cnt = new Map(), keys = new Set(); let dup = 0;
    for (const r of rows) { const v = r.variant || ''; if (!(v === '' || v === 'PADc')) continue; const kk = [r.panel, r.cell, r.config, r.mode, v, r.arm].join('|'); if (keys.has(kk)) dup++; keys.add(kk);
      const g = [r.mode + (v ? '/' + v : ''), r.config, r.arm].join('|'); cnt.set(g, (cnt.get(g) || 0) + 1); }
    const badG = [...cnt].filter(([, c]) => c !== 180);
    G.push(`G7 completeness: ${cnt.size} mode x config x arm groups, ${[...cnt].filter(([, c]) => c === 180).length} with 180 cells, duplicates ${dup} -> ${!badG.length && !dup ? 'PASS' : 'FAIL ' + badG.map(([g, c]) => g + ':' + c).join(' ')}`); }
  // G9
  if (arg('det')) { const D = read(arg('det')).filter(r => isProd(r)); let bad = 0, miss = 0;
    const key = r => [r.panel, r.cell, r.config, r.mode, r.arm, r.flatRatio, r.fcIndex].join('|'); const P = new Map(rows.filter(isProd).map(r => [key(r), r]));
    for (const r of D) { const o = P.get(key(r)); if (!o) { miss++; continue; } if (cmp(r, o, Object.keys(o), ['ms', 'msScore']).length) bad++; }
    G.push(`G9a gate rows (variant null) == production rows: ${D.length - bad - miss}/${D.length} (missing ${miss}) -> ${D.length && !bad && !miss ? 'PASS' : 'FAIL'}`); }
  if (arg('calib')) { const C = read(arg('calib')); const key = r => [r.variant, r.panel, r.cell, r.config, r.arm, r.flatRatio].join('|'); const CM = new Map(C.map(r => [key(r), r]));
    const P = rows.filter(r => r.mode === 'PAD' && (isProd(r) || r.variant === 'PADc')); let bad = 0, miss = 0;
    for (const r of P) { const o = CM.get(key({ ...r, variant: r.variant === 'PADc' ? 'calPADc' : 'calPAD' })); if (!o) { miss++; continue; } if (cmp(r, o, Object.keys(r), ['variant', 'label', 'ms', 'msScore']).length) bad++; }
    G.push(`G9b production PAD (and PADc) rows == calibration rows at the same flatRatio: ${P.length - bad - miss}/${P.length} (missing ${miss}) -> ${P.length && !bad && !miss ? 'PASS' : 'FAIL'}`); }
}
fs.writeFileSync(arg('out'), G.join('\n') + '\n');
console.log(G.join('\n'));
