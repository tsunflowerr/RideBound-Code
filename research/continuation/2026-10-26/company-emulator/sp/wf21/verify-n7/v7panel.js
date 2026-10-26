// Per-panel equal-length comparison: for each group and panel, fit pooled late/1000 (mean of per-day window rates) on the
// panel's rider-weighted L across all PAD runs (calibration rows at every tried r), evaluate at the panel's L_CL.
'use strict';
const L = require('./v7lib.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7';
const rows = L.readNd(K + '/results/rows.ndjson');
const cal = L.readNd(K + '/calib/calib.ndjson');
const out = []; const P = s => { out.push(s); console.log(s); };
const lateMean = rs => { const by = new Map(); for (const r of rs) { if (!by.has(r.day)) by.set(r.day, []); by.get(r.day).push(r); }
  let s = 0; for (const a of by.values()) { const sv = a.reduce((x, y) => x + y.served, 0), lt = a.reduce((x, y) => x + y.late60First, 0); s += 1000 * lt / sv; } return s / by.size; };
const Lof = rs => rs.reduce((s, r) => s + r.plSumArr_ms, 0) / rs.reduce((s, r) => s + r.plN, 0) / 1000;
const groups = {}; for (const r of cal) { const k = [r.config, r.win, L.base(r.arm), r.flatRatio].join('|'); (groups[k] ||= new Map()).set(r.cell, r); }
P('group panel: CL late, L_CL | final PAD late, L | slope late~L across PAD runs (n) | CL - PAD raw | CL - PAD at equal panel length (interp) | with slope incl N');
const summary = {};
for (const c of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) for (const w of ['w08', 'w17']) for (const pan of ['F14', 'F16', 'all']) {
  const sel = (m, v) => rows.filter(r => r.config === c && L.base(r.arm) === w.length && false);
  const pr = (mode, variant) => rows.filter(r => r.config === c && L.base(r.arm) === arm && r.win === w && r.mode === mode && (r.variant ?? null) === variant && (pan === 'all' || r.panel === pan));
  const cl = pr('CL', null), pad = pr('PAD', null), n = pr('N', null);
  const keys = Object.keys(groups).filter(k => k.startsWith(`${c}|${w}|${arm}|`));
  const pts = keys.map(k => { const g = [...groups[k].values()].filter(r => pan === 'all' || r.panel === pan); return { r: +k.split('|')[3], L: Lof(g), late: lateMean(g) }; });
  const o = L.ols(pts.map(p => p.L), pts.map(p => p.late));
  const oN = L.ols([Lof(n), ...pts.map(p => p.L)], [lateMean(n), ...pts.map(p => p.late)]);
  // smooth in r: late~r and L~r linear, solve L(r*) = L_CL
  const oLr = L.ols(pts.map(p => p.r), pts.map(p => p.L)), olr = L.ols(pts.map(p => p.r), pts.map(p => p.late));
  const rstar = (Lof(cl) - oLr.a) / oLr.b, lateRstar = olr.a + olr.b * rstar;
  const lc = lateMean(cl), lp = lateMean(pad);
  const eq = lc - (o.a + o.b * Lof(cl)), eqN = lc - (oN.a + oN.b * Lof(cl));
  summary[`${c}|${arm}|${w}|${pan}`] = { raw: lc - lp, eq, eqN, eqR: lc - lateRstar };
  P(`${c} ${arm} ${w} ${pan}: CL ${lc.toFixed(2)} L ${Lof(cl).toFixed(2)} | PAD ${lp.toFixed(2)} L ${Lof(pad).toFixed(2)} (gap ${L.f(Lof(pad) - Lof(cl))}) | slope ${L.f(o.b)} (n ${pts.length}, r ${o.r.toFixed(2)}) | raw ${L.f(lc - lp)} | eqLen ${L.f(eq)} | eqLen(N incl, slope ${L.f(oN.b)}) ${L.f(eqN)} | via r* ${rstar.toFixed(5)}: ${L.f(lc - lateRstar)}`);
}
P('\nSummary (CL - PAD late/1000): raw vs equal-length by interpolation across PAD runs [late~L] / [late~L incl N] / [smooth in r]');
for (const c of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) for (const w of ['w08', 'w17']) {
  const g = p => summary[`${c}|${arm}|${w}|${p}`];
  P(`${c} ${arm} ${w}: F14 raw ${L.f(g('F14').raw)} eq ${L.f(g('F14').eq)}/${L.f(g('F14').eqN)}/${L.f(g('F14').eqR)} | F16 raw ${L.f(g('F16').raw)} eq ${L.f(g('F16').eq)}/${L.f(g('F16').eqN)}/${L.f(g('F16').eqR)} | all raw ${L.f(g('all').raw)} eq ${L.f(g('all').eq)}/${L.f(g('all').eqN)}/${L.f(g('all').eqR)}`);
}
require('fs').writeFileSync(__dirname + '/v7panel_out.txt', out.join('\n') + '\n');
