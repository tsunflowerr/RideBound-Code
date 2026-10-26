'use strict';
const L = require('./v7lib.js');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const K = SP + '/wf21/n7';
const rows = L.readNd(K + '/results/rows.ndjson'), gate = L.readNd(K + '/gates/gate_rows.ndjson'), cal = L.readNd(K + '/calib/calib.ndjson');
const w20 = L.readNd(SP + '/wf20/fullsys/results/rows.ndjson');
const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const diffKeys = (a, b, skip) => [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => !skip.includes(k) && !eq(a[k], b[k]));
// G1
const w20sh = w20.filter(r => /^SH(RC|HZ)/.test(r.arm));
console.log('wf20 rows', w20.length, 'SHRC/SHHZ', w20sh.length, 'modes', [...new Set(w20sh.map(r => r.mode))].join(','));
const k5 = r => [r.panel, r.cell, r.config, r.mode, r.arm].join('|');
const W = new Map(w20sh.map(r => [k5(r), r]));
const W20KEYS = Object.keys(w20[0]);
let n = 0, ok = 0, miss = 0; const bad = {};
for (const r of rows) { if (r.variant != null || !['N', 'CL'].includes(r.mode)) continue; n++; const o = W.get(k5(r)); if (!o) { miss++; continue; }
  const d = W20KEYS.filter(k => !['label', 'ms', 'msScore'].includes(k) && !eq(r[k], o[k])); if (!d.length) ok++; else for (const k of d) bad[k] = (bad[k] || 0) + 1; }
console.log(`G1: ${ok}/${n} equal on all ${W20KEYS.length - 3} wf20 fields (missing ${miss}); diff fields ${JSON.stringify(bad)}; wf20 SH rows not matched: ${w20sh.length - n}`);
// G2 PAD r=1 vs N in gate rows (and vs production N)
const k4 = r => [r.panel, r.cell, r.config, r.arm].join('|');
const gN = new Map(gate.filter(r => r.mode === 'N' && r.variant == null).map(r => [k4(r), r]));
const gCL = new Map(gate.filter(r => r.mode === 'CL' && r.variant == null).map(r => [k4(r), r]));
const pN = new Map(rows.filter(r => r.mode === 'N' && r.variant == null).map(r => [k4(r), r]));
const pCL = new Map(rows.filter(r => r.mode === 'CL' && r.variant == null).map(r => [k4(r), r]));
const pad1 = gate.filter(r => r.mode === 'PAD' && r.variant === 'gate-r1');
const ag = {}; let ok2 = 0;
for (const r of pad1) { const o = gN.get(k4(r)); const d = diffKeys(r, o, ['label', 'ms', 'msScore', 'mode', 'variant', 'flatRatio']); if (!d.filter(k => !['sig', 'sigX', 'fc'].includes(k)).length) ok2++; for (const k of d) ag[k] = (ag[k] || 0) + 1;
  const o2 = pN.get(k4(r)); if (diffKeys(o, o2, ['label', 'ms', 'msScore']).length) console.log('gate N != prod N', k4(r)); }
console.log(`G2: PAD r=1 (n ${pad1.length}, flatRatio ${[...new Set(pad1.map(r => r.flatRatio))]}) equal to N on all fields incl. new ones except sig/sigX/fc: ${ok2}/${pad1.length}; differing fields (count): ${JSON.stringify(ag)}`);
const old = gate.filter(r => r.mode === 'CLc' && r.variant === 'gate-oldidx');
const ag3 = {}; let ok3 = 0;
for (const r of old) { const o = gCL.get(k4(r)); const d = diffKeys(r, o, ['label', 'ms', 'msScore', 'mode', 'variant', 'fcIndex']); if (!d.length) ok3++; for (const k of d) ag3[k] = (ag3[k] || 0) + 1; }
console.log(`G3: CLc old-index (n ${old.length}) equal to CL on ALL fields incl. new ones: ${ok3}/${old.length}; diffs ${JSON.stringify(ag3)}; fcIndex values ${[...new Set(old.map(r => r.fcIndex))].join(',')}`);
// G9b own check: production PAD rows equal calib rows at same r on all fields except variant/label/ms/msScore
const ck = r => [r.panel, r.cell, r.config, r.arm, r.flatRatio].join('|');
const C = new Map(); for (const r of cal) { const kk = ck(r) + '|' + (r.variant === 'calPADc' ? 'c' : ''); C.set(kk, r); }
let ok9 = 0, n9 = 0; for (const r of rows.filter(r => r.mode === 'PAD')) { n9++; const o = C.get(ck(r) + '|' + (r.variant === 'PADc' ? 'c' : '')); if (o && !diffKeys(r, o, ['variant', 'label', 'ms', 'msScore']).length) ok9++; }
console.log(`G9b: ${ok9}/${n9}`);
// G6-like on CLc vs CL for the 6 days, and how many CLc rows differ from CL overall
const pCLc = rows.filter(r => r.mode === 'CLc' && r.variant == null);
let same = 0; const sameDays = new Set(); for (const r of pCLc) { const o = pCL.get(k4(r)); if (!diffKeys(r, o, ['label', 'ms', 'msScore', 'mode', 'fcIndex']).length) { same++; sameDays.add(r.day); } }
console.log(`CLc rows identical to CL: ${same}/${pCLc.length}; days ${[...sameDays].sort().join(',')}`);
// arms in calib: does calibration run both arms at each group's r? check r per (config,win,arm)
