// wf21/n7 analysis (label wf21-n7-exploratory-post-audit = "thăm dò sau kiểm toán"). Pure function of the production row file.
// Unit = day. X_d = sum over windows {w08, w17} of the mean over resamples (r0, r1, r2) of X; per window X_{d,w} = mean over
// resamples. Rates per 1000 served = 1000 * X / served (day or window); TV5 = sumTV5 / 1000 / served (s per served rider);
// PL = mean initial promise length = plSumArr_ms / plN / 1000 (s; p0 - request time over promised riders; promised = served here).
// A day enters a contrast only if both sides have every resample of both windows (else excluded and logged).
// Paired difference by day: mean, SE, 95 % t-interval, median, n+/n-/ties, exact two-sided sign test (ties dropped); counts also
// rel = sum(A)/sum(B) - 1. F14, F16 and pooled (30 days) separately. Modes: N, CL, PAD, CLc (+ PADc = rows mode PAD variant PADc).
// Usage: node an7.js --rows <rows.ndjson> --out <prefix> [--flat <flat_final.json>] [--flatc <flat_final_padc.json>]
'use strict';
const fs = require('fs'), path = require('path');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const PANELS = JSON.parse(fs.readFileSync(path.join(SP, 'wf17/r1-identity-fence-pad/work/panels.json'), 'utf8'));
const F14 = new Set(PANELS.F14), F16 = new Set(PANELS.F16);
const LABEL = 'wf21-n7-exploratory-post-audit (thăm dò sau kiểm toán)';

// ---------------------------------------------------------------- statistics (copied from wf20/fullsys/analysis/fsan.js)
function lgamma(x) { const c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
  let y = x, t = x + 5.5; t -= (x + 0.5) * Math.log(t); let s = 1.000000000190015; for (const v of c) s += v / ++y; return -t + Math.log(2.5066282746310005 * s / x); }
function betacf(a, b, x) { let qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap; if (Math.abs(d) < 1e-300) d = 1e-300; d = 1 / d; let h = d;
  for (let m = 1; m <= 300; m++) { const m2 = 2 * m; let aa = m * (b - m) * x / ((qam + m2) * (a + m2)); d = 1 + aa * d; if (Math.abs(d) < 1e-300) d = 1e-300; c = 1 + aa / c; if (Math.abs(c) < 1e-300) c = 1e-300; d = 1 / d; h *= d * c;
    aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2)); d = 1 + aa * d; if (Math.abs(d) < 1e-300) d = 1e-300; c = 1 + aa / c; if (Math.abs(c) < 1e-300) c = 1e-300; d = 1 / d; const del = d * c; h *= del; if (Math.abs(del - 1) < 3e-14) break; }
  return h; }
function ibeta(a, b, x) { if (x <= 0) return 0; if (x >= 1) return 1; const bt = Math.exp(lgamma(a + b) - lgamma(a) - lgamma(b) + a * Math.log(x) + b * Math.log(1 - x));
  return x < (a + 1) / (a + b + 2) ? bt * betacf(a, b, x) / a : 1 - bt * betacf(b, a, 1 - x) / b; }
const tcdf = (t, df) => { const x = df / (df + t * t), p = 0.5 * ibeta(df / 2, 0.5, x); return t > 0 ? 1 - p : p; };
function tq975(df) { let lo = 0, hi = 100; for (let i = 0; i < 200; i++) { const m = (lo + hi) / 2; if (tcdf(m, df) < 0.975) lo = m; else hi = m; } return (lo + hi) / 2; }
function signP(np, nm) { const n = np + nm; if (!n) return 1; const k = Math.min(np, nm); let lc = 0, s = 0;
  for (let i = 0; i <= k; i++) { if (i > 0) lc += Math.log(n - i + 1) - Math.log(i); s += Math.exp(lc - n * Math.LN2); } return Math.min(1, 2 * s); }
function summary(d) {
  const n = d.length; if (!n) return null;
  const mean = d.reduce((a, b) => a + b, 0) / n;
  const sd = n > 1 ? Math.sqrt(d.reduce((a, b) => a + (b - mean) ** 2, 0) / (n - 1)) : NaN, se = sd / Math.sqrt(n);
  const t = n > 1 ? tq975(n - 1) : NaN, s = d.slice().sort((a, b) => a - b);
  const med = n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
  const EPS = 1e-9, np = d.filter(x => x > EPS).length, nm = d.filter(x => x < -EPS).length;
  return { n, mean, se, lo: mean - t * se, hi: mean + t * se, median: med, nPlus: np, nMinus: nm, ties: n - np - nm, p: signP(np, nm) };
}

// ---------------------------------------------------------------- rows
const COUNTS = ['served', 'visRev60', 'late60First', 'early60First', 'pDisp', 'breachDeadline60', 'sumTV5', 'G_late60First', 'G_n', 'breachLedger',
  'relocations', 'plSumArr_ms', 'plN', 'early60FirstG'];
const baseArm = a => a.replace(/^(SHF|SHHZ|SHRC|SHNC)\d+$/, '$1');
const log = [];
function loadRows(file) {
  const M = new Map();
  for (const l of fs.readFileSync(file, 'utf8').split('\n')) {
    if (!l) continue; let r; try { r = JSON.parse(l); } catch (e) { continue; }
    const v0 = r.variant || null;
    if (!(v0 === null || v0 === 'PADc')) continue;                      // gate / calibration variants are not analysed
    const mode = r.mode + (v0 === 'PADc' ? 'c' : '');
    const panel = F14.has(r.day) ? 'F14' : F16.has(r.day) ? 'F16' : null; if (!panel) continue;
    if (r.panel !== panel) throw new Error('panel mismatch ' + r.cell);
    const key = [panel, r.config, mode, baseArm(r.arm), r.cell].join('|');
    if (M.has(key)) throw new Error('duplicate production row ' + key);
    const v = {}; for (const k of COUNTS) v[k] = r[k] === undefined ? null : r[k];
    M.set(key, { panel, config: r.config, mode, arm: baseArm(r.arm), cell: r.cell, day: r.day, win: r.win, rs: r.rs, v, flatRatio: r.flatRatio });
  }
  return [...M.values()];
}
function dayTable(rows, sel) {
  const by = new Map();
  for (const r of rows) if (r.config === sel.config && r.mode === sel.mode && r.arm === sel.arm) { if (!by.has(r.day)) by.set(r.day, { panel: r.panel, cells: [] }); by.get(r.day).cells.push(r); }
  const out = new Map();
  for (const [day, o] of by) {
    const W = {}; let ok = true;
    for (const w of ['w08', 'w17']) {
      const cs = o.cells.filter(c => c.win === w), rss = new Set(cs.map(c => c.rs));
      if (cs.length !== 3 || !['r0', 'r1', 'r2'].every(x => rss.has(x))) { ok = false; break; }
      W[w] = {}; for (const k of COUNTS) { const vals = cs.map(c => c.v[k]); W[w][k] = vals.some(x => x === null) ? null : vals.reduce((a, b) => a + b, 0) / 3; }
    }
    if (!ok) { log.push(`incomplete day excluded: ${fmt(sel)} ${day}`); continue; }
    const D = {}; for (const k of COUNTS) D[k] = W.w08[k] === null ? null : W.w08[k] + W.w17[k];
    out.set(day, { panel: o.panel, day: D, w08: W.w08, w17: W.w17 });
  }
  return out;
}
const fmt = s => `${s.arm}_${s.config}_${s.mode}`;
const METRICS = [
  { id: 'served', f: X => X.served, count: true },
  { id: 'PL s', f: X => X.plSumArr_ms / X.plN / 1000 },
  { id: 'late60First/1000', f: X => 1000 * X.late60First / X.served },
  { id: 'early60First/1000', f: X => 1000 * X.early60First / X.served },
  { id: 'visRev60', f: X => X.visRev60, count: true },
  { id: 'Pchg/1000', f: X => 1000 * X.pDisp / X.served },
  { id: 'breachDeadline60/1000', f: X => 1000 * X.breachDeadline60 / X.served },
  { id: 'TV5 s', f: X => X.sumTV5 / 1000 / X.served },
  { id: 'G_late60First', f: X => X.G_late60First, count: true },
  { id: 'breachLedger/1000', f: X => 1000 * X.breachLedger / X.served },
  { id: 'guaranteedShare %', f: X => 100 * X.G_n / X.served },
  { id: 'late60First', f: X => X.late60First, count: true },
  { id: 'relocations', f: X => X.relocations, count: true },
];
const SCOPES = ['day', 'w08', 'w17'], PSETS = ['F14', 'F16', 'pooled'];
const TABLES = new Map();
const tbl = (rows, sel) => { const k = fmt(sel); if (!TABLES.has(k)) TABLES.set(k, dayTable(rows, sel)); return TABLES.get(k); };
function contrast(rows, A, B) {
  const tA = tbl(rows, A), tB = tbl(rows, B);
  if (!tA.size || !tB.size) return null;
  const res = {};
  for (const pset of PSETS) {
    const days = [...tA.keys()].filter(d => tB.has(d) && (pset === 'pooled' || tA.get(d).panel === pset)).sort();
    const R = {};
    for (const m of METRICS) for (const sc of SCOPES) {
      const a = days.map(d => m.f(tA.get(d)[sc])), b = days.map(d => m.f(tB.get(d)[sc]));
      if (a.some(x => x === null || !Number.isFinite(x)) || b.some(x => x === null || !Number.isFinite(x))) continue;
      const s = summary(a.map((x, i) => x - b[i])); if (!s) continue;
      const sa = a.reduce((x, y) => x + y, 0), sb = b.reduce((x, y) => x + y, 0);
      R[m.id + '|' + sc] = { meanA: sa / days.length, meanB: sb / days.length, rel: m.count && sb ? sa / sb - 1 : null, ...s, diffs: a.map((x, i) => x - b[i]) };
    }
    res[pset] = { days: days.length, R };
  }
  return res;
}
function levels(rows, sel) {
  const t = tbl(rows, sel), res = {};
  for (const pset of PSETS) {
    const days = [...t.keys()].filter(d => pset === 'pooled' || t.get(d).panel === pset), R = {};
    for (const m of METRICS) for (const sc of SCOPES) { const a = days.map(d => m.f(t.get(d)[sc])); if (!a.length || a.some(x => x === null || !Number.isFinite(x))) continue; R[m.id + '|' + sc] = a.reduce((x, y) => x + y, 0) / a.length; }
    res[pset] = { days: days.length, R };
  }
  return res;
}

// ---------------------------------------------------------------- main
const rows = loadRows(arg('rows'));
const modes = [...new Set(rows.map(r => r.mode))].sort();
const HAS_PADC = modes.includes('PADc');
const S = (config, mode, arm) => ({ config, mode, arm });
const out = { label: LABEL, modes, levels: {}, contrasts: {}, decomp: {}, answers: {}, predictions: [], log };
const lines = [];
const P = s => lines.push(s);
const f2 = (x, d = 2) => (x === null || x === undefined || !Number.isFinite(x) ? 'NA' : (x >= 0 ? '+' : '') + x.toFixed(d));
const f2u = (x, d = 2) => (x === null || x === undefined || !Number.isFinite(x) ? 'NA' : x.toFixed(d));
const fp = p => (p < 0.001 ? p.toExponential(1) : p.toFixed(3));
P(`# wf21/n7 — forecast vs equal-length pad through dispatch. ${LABEL}`);
P(`# modes present: ${modes.join(', ')}; rows ${rows.length}`);
if (arg('flat')) P(`# PAD flat ratios: ${JSON.stringify(JSON.parse(fs.readFileSync(arg('flat'), 'utf8')).flat)}`);
if (arg('flatc')) P(`# PADc flat ratios: ${JSON.stringify(JSON.parse(fs.readFileSync(arg('flatc'), 'utf8')).flat)}`);
// levels
const LM = ['served', 'PL s', 'late60First/1000', 'early60First/1000', 'visRev60', 'Pchg/1000', 'breachDeadline60/1000', 'TV5 s', 'G_late60First', 'breachLedger/1000', 'guaranteedShare %'];
for (const config of ['INS', 'G2']) for (const arm of ['SHRC', 'SHHZ']) for (const mode of modes) {
  const k = fmt(S(config, mode, arm)); out.levels[k] = levels(rows, S(config, mode, arm));
}
P('\n## Levels (pooled 30 days; mean per day; day | w08 | w17)');
P('cell'.padEnd(18) + LM.map(m => m.padStart(26)).join(''));
for (const config of ['INS', 'G2']) for (const arm of ['SHRC', 'SHHZ']) for (const mode of modes) {
  const L = out.levels[fmt(S(config, mode, arm))].pooled.R;
  P(fmt(S(config, mode, arm)).padEnd(18) + LM.map(m => `${f2u(L[m + '|day'], 1)}|${f2u(L[m + '|w08'], 1)}|${f2u(L[m + '|w17'], 1)}`.padStart(26)).join(''));
}
// contrasts
const CONTRASTS = [['CL', 'PAD'], ['CLc', 'PAD'], ['CLc', 'CL'], ['PAD', 'N'], ['CL', 'N'], ['CLc', 'N']];
if (HAS_PADC) CONTRASTS.push(['CLc', 'PADc'], ['PADc', 'N'], ['PADc', 'PAD']);
const CM = ['served', 'PL s', 'late60First/1000', 'early60First/1000', 'visRev60', 'Pchg/1000', 'breachDeadline60/1000', 'TV5 s', 'G_late60First', 'breachLedger/1000', 'guaranteedShare %'];
const line = (lab, r) => r ? `${lab.padEnd(30)} ${f2(r.mean)} [${f2(r.lo)}, ${f2(r.hi)}] med ${f2(r.median)}  ${r.nMinus}-/${r.nPlus}+/${r.ties}=  p ${fp(r.p)}  (A ${f2u(r.meanA)} B ${f2u(r.meanB)}${r.rel !== null ? ' rel ' + f2(100 * r.rel, 1) + '%' : ''})` : `${lab.padEnd(30)} NA`;
for (const config of ['INS', 'G2']) for (const arm of ['SHRC', 'SHHZ']) for (const [a, b] of CONTRASTS) {
  if (!modes.includes(a) || !modes.includes(b)) continue;
  const k = `${arm}_${config}: ${a} - ${b}`;
  const c = contrast(rows, S(config, a, arm), S(config, b, arm)); out.contrasts[k] = c;
  P(`\n## ${k}`);
  for (const pset of PSETS) { P(`### ${pset} (${c[pset].days} days)`); for (const m of CM) for (const sc of SCOPES) P(line(`${m} ${sc}`, c[pset].R[m + '|' + sc])); }
}
// answers (pre-stated rule, PLAN §6): late60First/1000 and secondary endpoints, per scope
const cls = (c, m, sc) => {
  const p = c.pooled.R[m + '|' + sc], a = c.F14.R[m + '|' + sc], b = c.F16.R[m + '|' + sc];
  if (!p) return 'NA';
  if (p.mean < 0 && p.p < 0.05 && a.mean < 0 && b.mean < 0) return 'A<B';
  if (p.mean > 0 && p.p < 0.05 && a.mean > 0 && b.mean > 0) return 'A>B';
  return 'no clear difference';
};
P('\n## Answers (PLAN §6 rule: pooled sign p < 0.05 and the same sign in F14 and F16; A<B = first mode lower)');
for (const [a, b] of [['CL', 'PAD'], ['CLc', 'PAD'], ...(HAS_PADC ? [['CLc', 'PADc']] : []), ['CLc', 'CL']]) for (const config of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) {
  const k = `${arm}_${config}: ${a} - ${b}`, c = out.contrasts[k]; if (!c) continue;
  const A = {};
  for (const m of ['late60First/1000', 'TV5 s', 'visRev60', 'breachDeadline60/1000', 'early60First/1000', 'served', 'Pchg/1000', 'G_late60First']) for (const sc of SCOPES) A[m + '|' + sc] = cls(c, m, sc);
  out.answers[k] = A;
  P(`${k.padEnd(26)} late/1000 day: ${A['late60First/1000|day']} | w08: ${A['late60First/1000|w08']} | w17: ${A['late60First/1000|w17']} || TV5 day ${A['TV5 s|day']} | visRev60 day ${A['visRev60|day']} | deadline day ${A['breachDeadline60/1000|day']} | early day ${A['early60First/1000|day']} | served day ${A['served|day']}`);
}
// decompositions (exactly additive by day)
function decomp(name, total, parts) {
  const T = contrast(rows, total[0], total[1]); if (!T) return;
  const Ps = parts.map(([lab, A, B]) => ({ lab, c: contrast(rows, A, B) }));
  if (Ps.some(x => !x.c)) return;
  out.decomp[name] = { total: T, parts: Object.fromEntries(Ps.map(x => [x.lab, x.c])) };
  P(`\n## Decomposition: ${name}  (pooled 30 days; mean [95% CI] n-/n+)`);
  for (const m of ['served', 'late60First/1000', 'visRev60', 'TV5 s', 'breachDeadline60/1000', 'Pchg/1000', 'early60First/1000', 'G_late60First', 'PL s']) for (const sc of SCOPES) {
    const t = T.pooled.R[m + '|' + sc]; if (!t) continue;
    const sum = Ps.reduce((s, x) => s + x.c.pooled.R[m + '|' + sc].mean, 0);
    P(`${(m + ' ' + sc).padEnd(26)} total ${f2(t.mean)} [${f2(t.lo)}, ${f2(t.hi)}] ${t.nMinus}-/${t.nPlus}+ = ` + Ps.map(x => { const r = x.c.pooled.R[m + '|' + sc]; return `${x.lab} ${f2(r.mean)} [${f2(r.lo)}, ${f2(r.hi)}] ${r.nMinus}-/${r.nPlus}+`; }).join(' + ') + `  (check ${Math.abs(sum - t.mean) < 1e-6 ? 'additive' : 'NOT additive ' + f2(sum - t.mean, 6)})`);
  }
}
for (const config of ['G2', 'INS']) for (const X of ['CL', 'CLc']) {
  if (!modes.includes(X)) continue;
  const pad = X === 'CLc' && HAS_PADC ? 'PADc' : 'PAD';
  decomp(`${config}: SHRC_${X} - SHHZ_N`, [S(config, X, 'SHRC'), S(config, 'N', 'SHHZ')],
    [['RuleC(SHRC_N-SHHZ_N)', S(config, 'N', 'SHRC'), S(config, 'N', 'SHHZ')], [`length(SHRC_${pad}-SHRC_N)`, S(config, pad, 'SHRC'), S(config, 'N', 'SHRC')], [`info(SHRC_${X}-SHRC_${pad})`, S(config, X, 'SHRC'), S(config, pad, 'SHRC')]]);
}
for (const X of ['CL', 'CLc']) {
  if (!modes.includes(X)) continue;
  const pad = X === 'CLc' && HAS_PADC ? 'PADc' : 'PAD';
  decomp(`whole system: SHRC_G2_${X} - SHHZ_INS_N`, [S('G2', X, 'SHRC'), S('INS', 'N', 'SHHZ')],
    [['G2(SHHZ_G2_N-SHHZ_INS_N)', S('G2', 'N', 'SHHZ'), S('INS', 'N', 'SHHZ')], ['RuleC(SHRC_G2_N-SHHZ_G2_N)', S('G2', 'N', 'SHRC'), S('G2', 'N', 'SHHZ')],
      [`length(SHRC_G2_${pad}-N)`, S('G2', pad, 'SHRC'), S('G2', 'N', 'SHRC')], [`info(SHRC_G2_${X}-${pad})`, S('G2', X, 'SHRC'), S('G2', pad, 'SHRC')]]);
}
// promise-length match check (PLAN §3): |L_PAD - L_CL| per config x window x arm, pooled and per panel
P('\n## Promise-length match (mean initial promise length, s; pooled over cells of the window = rider-weighted)');
const Lw = (sel, w, pset) => { let s = 0, n = 0; for (const r of rows) if (r.config === sel.config && r.mode === sel.mode && r.arm === sel.arm && r.win === w && (pset === 'pooled' || r.panel === pset)) { s += r.v.plSumArr_ms; n += r.v.plN; } return n ? s / n / 1000 : NaN; };
out.plMatch = {};
for (const config of ['INS', 'G2']) for (const arm of ['SHRC', 'SHHZ']) for (const w of ['w08', 'w17']) for (const pset of PSETS) {
  const v = Object.fromEntries(modes.map(m => [m, Lw(S(config, m, arm), w, pset)]));
  out.plMatch[`${config}|${w}|${arm}|${pset}`] = v;
  P(`${(config + ' ' + w + ' ' + arm + ' ' + pset).padEnd(24)} ` + modes.map(m => `${m} ${f2u(v[m])}`).join('  ') + `  | PAD-CL ${f2(v.PAD - v.CL)}  CL-N ${f2(v.CL - v.N)}${v.CLc !== undefined ? '  CLc-CL ' + f2(v.CLc - v.CL) : ''}${HAS_PADC ? '  PADc-CLc ' + f2(v.PADc - v.CLc) : ''}`);
}
// ---------------------------------------------------------------- predictions (PLAN §5), labelled mechanically
const PRED = JSON.parse(fs.readFileSync(path.join(__dirname, 'predictions.json'), 'utf8'));
P('\n## Prediction labels (PLAN §5; pooled 30 days, plus F14 / F16)');
for (const pr of PRED.items) for (const config of pr.configs || ['INS', 'G2']) for (const arm of pr.arms || ['SHRC', 'SHHZ']) {
  const [a, b] = pr.contrast; let B = b; if (pr.useRefPad && b === 'PAD' && a === 'CLc' && HAS_PADC) B = 'PADc';
  const k = `${arm}_${config}: ${a} - ${B}`; const c = out.contrasts[k]; if (!c) { P(`${pr.id} ${k} NA`); continue; }
  const lab = {};
  for (const pset of PSETS) {
    const r = c[pset].R[pr.metric + '|' + pr.scope]; if (!r) { lab[pset] = 'NA'; continue; }
    const nt = r.nMinus + r.nPlus, negShare = nt ? r.nMinus / nt : 0, posShare = nt ? r.nPlus / nt : 0, m = r.mean;
    let L;
    if (pr.type === 'neg') {
      if (pr.flipAbove !== undefined ? (m > pr.flipAbove && r.p < 0.05) : (m >= 0 || negShare < 0.5)) L = 'REVERSED';
      else if (m >= pr.lo && m <= pr.hi && negShare >= pr.q) L = 'HOLDS';
      else if (m < pr.lo && negShare >= pr.q) L = 'STRONGER';
      else if (m < 0 || (m <= pr.hi && m >= pr.lo)) L = 'WEAKENED';
      else L = 'INCONCLUSIVE';
    } else if (pr.type === 'pos') {
      if (pr.flipBelow !== undefined ? (m < pr.flipBelow && r.p < 0.05) : (m <= 0 || posShare < 0.5)) L = 'REVERSED';
      else if (m >= pr.lo && m <= pr.hi && posShare >= pr.q) L = 'HOLDS';
      else if (m > pr.hi && posShare >= pr.q) L = 'STRONGER';
      else if (m > 0 || (m <= pr.hi && m >= pr.lo)) L = 'WEAKENED';
      else L = 'INCONCLUSIVE';
    } else {   // two-sided band
      if (m >= pr.lo && m <= pr.hi) L = 'HOLDS';
      else if (pr.strongerBelow && m < pr.lo && r.p < 0.05) L = 'STRONGER';
      else if (r.p < 0.05) L = 'REVERSED';
      else L = 'INCONCLUSIVE';
    }
    lab[pset] = L;
  }
  const r = c.pooled.R[pr.metric + '|' + pr.scope];
  out.predictions.push({ id: pr.id, cell: k, metric: pr.metric, scope: pr.scope, band: [pr.lo, pr.hi], labels: lab, mean: r ? r.mean : null, p: r ? r.p : null });
  P(`${pr.id.padEnd(5)} ${k.padEnd(24)} ${(pr.metric + ' ' + pr.scope).padEnd(28)} band [${pr.lo}, ${pr.hi}] mean ${r ? f2(r.mean) : 'NA'} (${r ? r.nMinus + '-/' + r.nPlus + '+ p ' + fp(r.p) : ''}) -> pooled ${lab.pooled} | F14 ${lab.F14} | F16 ${lab.F16}`);
}
if (log.length) { P('\n## Log'); for (const l of log) P(l); }
for (const c of Object.values(out.contrasts)) for (const ps of PSETS) for (const r of Object.values(c[ps].R)) delete r.diffs;
fs.writeFileSync(arg('out') + '.txt', lines.join('\n') + '\n');
fs.writeFileSync(arg('out') + '.json', JSON.stringify(out));
console.log(`written ${arg('out')}.txt (${lines.length} lines) and .json`);
