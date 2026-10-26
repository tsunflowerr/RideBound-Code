'use strict';
const L = require('./v7lib.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7';
const rows = L.readNd(K + '/results/rows.ndjson'), cal = L.readNd(K + '/calib/calib.ndjson');
const agg = rs => { const m = new Map(); for (const r of rs) { if (!m.has(r.day)) m.set(r.day, { panel: r.panel, late: 0, served: 0, n: 0 }); const o = m.get(r.day); o.late += r.late60First / 3; o.served += r.served / 3; o.n++; } return m; };
const Lof = rs => rs.reduce((s, r) => s + r.plSumArr_ms, 0) / rs.reduce((s, r) => s + r.plN, 0) / 1000;
for (const [c, arm] of [['G2', 'SHRC'], ['INS', 'SHRC'], ['G2', 'SHHZ'], ['INS', 'SHHZ']]) {
  const cl = w => rows.filter(r => r.config === c && L.base(r.arm) === arm && r.win === w && r.mode === 'CL' && r.variant == null);
  const LCL = { w08: Lof(cl('w08')), w17: Lof(cl('w17')) };
  const pads = { w08: [], w17: [] };
  const byR = {}; for (const r of cal) if (r.config === c && L.base(r.arm) === arm) { const k = r.win + '|' + r.flatRatio; (byR[k] ||= new Map()).set(r.cell, r); }
  for (const [k, m] of Object.entries(byR)) { const [w, rr] = k.split('|'); const g = [...m.values()]; if (g.length !== 90) continue; const gap = Lof(g) - LCL[w]; if (Math.abs(gap) <= 0.75) pads[w].push({ r: +rr, gap, A: agg(g) }); }
  const C8 = agg(cl('w08')), C17 = agg(cl('w17'));
  const res = [];
  for (const p8 of pads.w08) for (const p17 of pads.w17) {
    const days = [...C8.keys()].sort();
    const d = days.map(dd => 1000 * (C8.get(dd).late + C17.get(dd).late) / (C8.get(dd).served + C17.get(dd).served) - 1000 * (p8.A.get(dd).late + p17.A.get(dd).late) / (p8.A.get(dd).served + p17.A.get(dd).served));
    const s = L.summ(d);
    const m14 = days.map((dd, i) => [C8.get(dd).panel, d[i]]).filter(x => x[0] === 'F14').map(x => x[1]); const m16 = days.map((dd, i) => [C8.get(dd).panel, d[i]]).filter(x => x[0] === 'F16').map(x => x[1]);
    const mean = a => a.reduce((x, y) => x + y, 0) / a.length;
    res.push(`  w08 r ${p8.r} (gap ${L.f(p8.gap)}) + w17 r ${p17.r} (gap ${L.f(p17.gap)}): day CL-PAD ${L.fs_(s)} F14 ${L.f(mean(m14))} F16 ${L.f(mean(m16))} -> rule: ${s.mean < 0 && s.p < 0.05 && mean(m14) < 0 && mean(m16) < 0 ? 'forecast beats pad' : 'no clear difference'}`);
  }
  console.log(`${c} ${arm}: day-level CL - PAD for every combination of PAD runs with |pooled length gap| <= 0.75 s per window (${pads.w08.length} x ${pads.w17.length})`);
  console.log(res.join('\n'));
}
