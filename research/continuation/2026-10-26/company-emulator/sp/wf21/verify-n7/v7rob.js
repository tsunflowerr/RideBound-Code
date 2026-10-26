// Robustness checks: (1) per-panel promise-length mismatch, regression of per-day late diff on PL diff;
// (2) CL vs every PAD run at any tried r (calibration rows) -> sensitivity of the "information" effect to the pad realisation;
// (3) smooth equal-length pad by linear interpolation of late on L across all PAD runs of a group.
'use strict';
const L = require('./v7lib.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7';
const rows = L.readNd(K + '/results/rows.ndjson');
const cal = L.readNd(K + '/calib/calib.ndjson');
const out = []; const P = s => { out.push(s); console.log(s); };
const T = L.dayTables(rows, r => r.variant == null ? r.mode : r.variant === 'PADc' ? 'PADc' : null);
const S = (c, m, a) => `${c}|${m}|${a}`;
// ---- (1) rider-weighted PL per panel/window
P('## (1a) rider-weighted mean promise length per panel x window (s): PAD - CL, PADc - CLc');
const Lw = (sel) => { let s = 0, n = 0; for (const r of rows) if (sel(r)) { s += r.plSumArr_ms; n += r.plN; } return s / n / 1000; };
for (const c of ['INS', 'G2']) for (const arm of ['SHRC', 'SHHZ']) for (const w of ['w08', 'w17']) {
  const line = [];
  for (const pan of ['F14', 'F16', 'all']) {
    const g = (mode, v) => Lw(r => r.config === c && L.base(r.arm) === arm && r.win === w && r.mode === mode && (r.variant ?? null) === v && (pan === 'all' || r.panel === pan));
    line.push(`${pan}: PAD-CL ${L.f(g('PAD', null) - g('CL', null))} PADc-CLc ${L.f(g('PAD', 'PADc') - g('CLc', null))}`);
  }
  P(`${c} ${arm} ${w}  ` + line.join(' | '));
}
// ---- (1b) regression per-day late diff on per-day PL diff, CL - PAD
P('\n## (1b) OLS of per-day (CL - PAD) late/1000 on per-day (CL - PAD) PL (s), 30 days; slope b, intercept a = length-adjusted info effect at equal PL');
P('     cross-mode reference slope = (PAD - N late)/(PAD - N PL) pooled');
for (const [A, B] of [['CL', 'PAD'], ['CLc', 'PADc']]) for (const c of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) for (const sc of ['w08', 'w17', 'day']) {
  const dl = L.diffs(T, S(c, A, arm), S(c, B, arm), 'late', sc), dp = L.diffs(T, S(c, A, arm), S(c, B, arm), 'pl', sc);
  const o = L.ols(dp.d, dl.d);
  const xl = L.summ(L.diffs(T, S(c, 'PAD', arm), S(c, 'N', arm), 'late', sc).d).mean, xp = L.summ(L.diffs(T, S(c, 'PAD', arm), S(c, 'N', arm), 'pl', sc).d).mean;
  const xs = xl / xp;
  // panel means adjusted with (i) OLS slope (ii) cross-mode slope
  const adj = (pan, slope) => { const idx = dl.days.map((d, i) => i).filter(i => pan === 'all' || T.get(S(c, A, arm)).get(dl.days[i]).panel === pan);
    const v = idx.map(i => dl.d[i] - slope * dp.d[i]); return { m: v.reduce((a, b) => a + b, 0) / v.length, raw: idx.reduce((a, i) => a + dl.d[i], 0) / idx.length, pl: idx.reduce((a, i) => a + dp.d[i], 0) / idx.length, v }; };
  const r = ['F14', 'F16', 'all'].map(p => [p, adj(p, o.b), adj(p, xs)]);
  const sAll = L.summ(adj('all', xs).v), sAllO = L.summ(adj('all', o.b).v);
  P(`${A}-${B} ${c} ${arm} ${sc}: b ${L.f(o.b)} (se ${o.seb.toFixed(2)}, r ${o.r.toFixed(2)}), a ${L.f(o.a)} (se ${o.sea.toFixed(2)}); xmode slope ${L.f(xs)}` +
    ' || ' + r.map(([p, a1, a2]) => `${p}: raw ${L.f(a1.raw)} PLd ${L.f(a1.pl)} adjOLS ${L.f(a1.m)} adjX ${L.f(a2.m)}`).join(' | ') +
    ` || pooled adjX ${L.fs_(sAll)} ; adjOLS ${L.fs_(sAllO)}`);
}
// ---- (2) CL vs PAD at every tried r (window level), using calibration rows (calPAD and calPADc variants are all PAD runs)
P('\n## (2) CL - PAD(r) late/1000 per window for every PAD run in calib.ndjson (pooled 30 days), with that run\'s pooled PL gap (rider-weighted) ');
const winTab = (rs) => { // rs = rows of one window, one mode/run -> Map day -> mean over resamples
  const by = new Map(); for (const r of rs) { if (!by.has(r.day)) by.set(r.day, []); by.get(r.day).push(r); }
  const m = new Map(); for (const [d, a] of by) { if (a.length !== 3) throw new Error('rs ' + d + ' ' + a.length); const o = { panel: a[0].panel }; for (const k of L.KEYS) o[k] = a.reduce((s, x) => s + x[k], 0) / 3; m.set(d, o); } return m; };
const groups = {};
for (const r of cal) { const k = [r.config, r.win, L.base(r.arm), r.flatRatio].join('|'); (groups[k] ||= []).push(r); }
const res2 = {};
for (const c of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) for (const w of ['w08', 'w17']) {
  const clRows = rows.filter(r => r.config === c && L.base(r.arm) === arm && r.win === w && r.mode === 'CL' && r.variant == null);
  const nRows = rows.filter(r => r.config === c && L.base(r.arm) === arm && r.win === w && r.mode === 'N' && r.variant == null);
  const clT = winTab(clRows), nT = winTab(nRows);
  const Lcl = clRows.reduce((s, r) => s + r.plSumArr_ms, 0) / clRows.reduce((s, r) => s + r.plN, 0) / 1000;
  const keys = Object.keys(groups).filter(k => k.startsWith(`${c}|${w}|${arm}|`)).sort();
  const pts = [];
  for (const k of keys) {
    const g = groups[k]; const r = +k.split('|')[3]; const vars = [...new Set(g.map(x => x.variant))].join('+');
    // de-duplicate cells (same r may appear in both variants? check)
    const uniq = new Map(); for (const x of g) { const ck = x.cell; if (uniq.has(ck)) { const y = uniq.get(ck); if (y.late60First !== x.late60First || y.plSumArr_ms !== x.plSumArr_ms) throw new Error('inconsistent dup ' + k); } else uniq.set(ck, x); }
    const gg = [...uniq.values()]; if (gg.length !== 90) { P(`skip ${k} n ${gg.length}`); continue; }
    const pT = winTab(gg);
    const Lp = gg.reduce((s, x) => s + x.plSumArr_ms, 0) / gg.reduce((s, x) => s + x.plN, 0) / 1000;
    const days = [...clT.keys()].sort();
    const d = days.map(dd => L.MET.late(clT.get(dd)) - L.MET.late(pT.get(dd)));
    const s = L.summ(d);
    const latePad = days.reduce((a, dd) => a + L.MET.late(pT.get(dd)), 0) / days.length;
    const plPadDay = days.reduce((a, dd) => a + L.MET.pl(pT.get(dd)), 0) / days.length;
    const d14 = days.filter(dd => clT.get(dd).panel === 'F14').map(dd => L.MET.late(clT.get(dd)) - L.MET.late(pT.get(dd)));
    const d16 = days.filter(dd => clT.get(dd).panel === 'F16').map(dd => L.MET.late(clT.get(dd)) - L.MET.late(pT.get(dd)));
    const m = a => a.reduce((x, y) => x + y, 0) / a.length;
    pts.push({ r, L: Lp, late: latePad, plDay: plPadDay, s, vars });
    P(`${c} ${arm} ${w} r ${r.toFixed(6)} (${vars}) L ${Lp.toFixed(2)} gap(PAD-CL) ${L.f(Lp - Lcl)}  CL-PAD late ${L.fs_(s)}  F14 ${L.f(m(d14))} F16 ${L.f(m(d16))}`);
  }
  // (3) smooth equal-length pad: OLS of pooled late on pooled L across PAD runs (excluding N), evaluate at L_CL
  const lateCL = [...clT.values()].reduce((a, o) => a + L.MET.late(o), 0) / clT.size;
  const lateN = [...nT.values()].reduce((a, o) => a + L.MET.late(o), 0) / nT.size;
  const LN = nRows.reduce((s, r) => s + r.plSumArr_ms, 0) / nRows.reduce((s, r) => s + r.plN, 0) / 1000;
  if (pts.length >= 2) {
    const o = L.ols(pts.map(p => p.L), pts.map(p => p.late));
    const o2 = L.ols([LN, ...pts.map(p => p.L)], [lateN, ...pts.map(p => p.late)]);
    const oR = L.ols(pts.map(p => p.r), pts.map(p => p.late)), oRL = L.ols(pts.map(p => p.r), pts.map(p => p.L));
    const sdLate = Math.sqrt(pts.reduce((a, p) => a + (p.late - pts.reduce((x, q) => x + q.late, 0) / pts.length) ** 2, 0) / (pts.length - 1));
    P(`  -> ${pts.length} PAD runs; late(PAD) range ${Math.min(...pts.map(p => p.late)).toFixed(2)}..${Math.max(...pts.map(p => p.late)).toFixed(2)} (sd ${sdLate.toFixed(2)}); CL late ${lateCL.toFixed(2)}; CL - PAD range ${L.f(Math.min(...pts.map(p => p.s.mean)))}..${L.f(Math.max(...pts.map(p => p.s.mean)))}; sign p range ${Math.min(...pts.map(p => p.s.p)).toFixed(3)}..${Math.max(...pts.map(p => p.s.p)).toFixed(3)}`);
    P(`     late~L over PAD runs: slope ${L.f(o.b)} /s (r ${o.r.toFixed(2)}), at L_CL ${Lcl.toFixed(2)}: late ${(o.a + o.b * Lcl).toFixed(2)} -> CL - smoothPAD ${L.f(lateCL - (o.a + o.b * Lcl))}; incl N point slope ${L.f(o2.b)} -> CL - PAD ${L.f(lateCL - (o2.a + o2.b * Lcl))}`);
    P(`     late~r slope ${L.f(oR.b * 0.001)} per 0.001 r (r ${oR.r.toFixed(2)}); L~r slope ${L.f(oRL.b * 0.001)} s per 0.001 r (r ${oRL.r.toFixed(2)})`);
  }
}
require('fs').writeFileSync(__dirname + '/v7rob_out.txt', out.join('\n') + '\n');
