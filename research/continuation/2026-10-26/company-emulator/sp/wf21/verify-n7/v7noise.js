'use strict';
const L = require('./v7lib.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7';
const cal = L.readNd(K + '/calib/calib.ndjson');
const lateMean = rs => { const by = new Map(); for (const r of rs) { if (!by.has(r.day)) by.set(r.day, []); by.get(r.day).push(r); }
  let s = 0; for (const a of by.values()) { s += 1000 * a.reduce((x, y) => x + y.late60First, 0) / a.reduce((x, y) => x + y.served, 0); } return s / by.size; };
const Lof = rs => rs.reduce((s, r) => s + r.plSumArr_ms, 0) / rs.reduce((s, r) => s + r.plN, 0) / 1000;
const groups = {}; for (const r of cal) { const k = [r.config, r.win, L.base(r.arm), r.flatRatio].join('|'); (groups[k] ||= new Map()).set(r.cell, r); }
for (const c of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) for (const w of ['w08', 'w17']) {
  const keys = Object.keys(groups).filter(k => k.startsWith(`${c}|${w}|${arm}|`)); if (keys.length < 4) continue;
  const line = [];
  for (const pan of ['F14', 'F16', 'all']) {
    const pts = keys.map(k => { const g = [...groups[k].values()].filter(r => pan === 'all' || r.panel === pan); return { r: +k.split('|')[3], L: Lof(g), late: lateMean(g) }; });
    const a = L.ols(pts.map(p => p.r), pts.map(p => p.L)), b = L.ols(pts.map(p => p.r), pts.map(p => p.late));
    const res = (o, f) => Math.sqrt(pts.reduce((s, p) => s + (f(p) - o.a - o.b * p.r) ** 2, 0) / (pts.length - 2));
    line.push(`${pan}: resid SD L ${res(a, p => p.L).toFixed(2)} s, late ${res(b, p => p.late).toFixed(2)}/1000 (n ${pts.length})`);
  }
  console.log(`${c} ${arm} ${w}  ` + line.join(' | '));
}
