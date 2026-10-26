// wf20/fullsys analysis (label: wf20-fullsys-exploratory-post-audit = "thăm dò sau kiểm toán"). Pure function of row files.
// Unit = day. X_d = sum over windows {w08, w17} of the mean over resamples (r0, r1, r2) of X (wf14 PREREG §3, wf17 RUNPLAN §2.1).
// Per-window values X_{d,w} = mean over resamples. Rates: per 1000 served = 1000 * X_d / served_d (same for a window);
// TV5 = sumTV5 / 1000 / served (s per served rider). A day enters a contrast only if both arms have every resample of both
// windows (otherwise excluded and logged). Paired difference by day; exact two-sided sign test (ties dropped); mean, SE,
// 95 % t-interval, median; for counts the relative change sum(A)/sum(B) - 1. F14, F16 and pooled are reported separately.
// Usage: node fsan.js --rows f1[,f2,...] --spec production|evidence --out <prefix>   (writes <prefix>.txt and <prefix>.json)
'use strict';
const fs = require('fs'), path = require('path');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const PANELS = JSON.parse(fs.readFileSync(path.join(SP, 'wf17/r1-identity-fence-pad/work/panels.json'), 'utf8'));
const F14 = new Set(PANELS.F14), F16 = new Set(PANELS.F16);
const LABEL = 'wf20-fullsys-exploratory-post-audit (thăm dò sau kiểm toán)';

// ---------------------------------------------------------------- statistics
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
const COUNTS = ['served', 'visRev60', 'late60First', 'pDisp', 'breachDeadline60', 'sumTV5', 'G_late60First', 'G_n', 'breachLedger', 'relocations', 'G_ledgerBreach'];
const baseArm = a => a.replace(/^(SHF|SHHZ|SHRC|SHNC)\d+$/, '$1');
function loadRows(files) {
  const M = new Map(), conflicts = [], dup = { same: 0 };
  for (const f of files) {
    if (!fs.existsSync(f)) throw new Error('no rows file ' + f);
    for (const l of fs.readFileSync(f, 'utf8').split('\n')) {
      if (!l) continue; let r; try { r = JSON.parse(l); } catch (e) { continue; }
      if (r.flatRatio !== undefined && r.flatRatio !== null) continue;          // wf17 R3 CLF rows are not part of this design
      if (r.truncateMs !== undefined && r.truncateMs !== null) continue;
      const panel = r.panel || (F14.has(r.day) ? 'F14' : F16.has(r.day) ? 'F16' : null);
      if (!panel) continue;
      const mode = r.mode || 'N', arm = baseArm(r.arm);
      const key = [panel, r.config, mode, arm, r.cell].join('|');
      const v = {}; for (const k of COUNTS) v[k] = r[k] === undefined ? null : r[k];
      const row = { panel, config: r.config, mode, arm, cell: r.cell, day: r.day, win: r.win, rs: r.rs, v, src: path.basename(f) };
      if (M.has(key)) { const o = M.get(key); const same = COUNTS.filter(k => o.v[k] !== null && v[k] !== null).every(k => o.v[k] === v[k]);
        if (same) dup.same++; else conflicts.push(key + ' ' + o.src + ' vs ' + row.src); continue; }
      M.set(key, row);
    }
  }
  return { rows: [...M.values()], conflicts, dupSame: dup.same };
}
// day table for one (config, mode, arm): Map day -> { panel, day: {X}, w08: {X}, w17: {X} } (complete days only)
function dayTable(rows, sel, log) {
  const by = new Map();
  for (const r of rows) if (r.config === sel.config && r.mode === sel.mode && r.arm === sel.arm) {
    const k = r.day; if (!by.has(k)) by.set(k, { panel: r.panel, cells: [] }); by.get(k).cells.push(r);
  }
  const out = new Map();
  for (const [day, o] of by) {
    const W = {};
    let ok = true;
    for (const w of ['w08', 'w17']) {
      const cs = o.cells.filter(c => c.win === w), rss = new Set(cs.map(c => c.rs));
      if (cs.length !== 3 || !['r0', 'r1', 'r2'].every(x => rss.has(x))) { ok = false; break; }
      W[w] = {}; for (const k of COUNTS) { const vals = cs.map(c => c.v[k]); W[w][k] = vals.some(x => x === null) ? null : vals.reduce((a, b) => a + b, 0) / 3; }
    }
    if (!ok) { log.push(`incomplete day excluded: ${sel.config} ${sel.mode} ${sel.arm} ${day} (${o.cells.length} cells)`); continue; }
    const D = {}; for (const k of COUNTS) D[k] = W.w08[k] === null ? null : W.w08[k] + W.w17[k];
    out.set(day, { panel: o.panel, day: D, w08: W.w08, w17: W.w17 });
  }
  return out;
}
// metric definitions on an aggregate X (day or window)
const METRICS = [
  { id: 'served', f: X => X.served, count: true },
  { id: 'visRev60', f: X => X.visRev60, count: true },
  { id: 'visRev60/1000', f: X => 1000 * X.visRev60 / X.served },
  { id: 'late60First', f: X => X.late60First, count: true },
  { id: 'late60First/1000', f: X => 1000 * X.late60First / X.served },
  { id: 'Pchg(pDisp)', f: X => X.pDisp, count: true },
  { id: 'Pchg/1000', f: X => 1000 * X.pDisp / X.served },
  { id: 'breachDeadline60', f: X => X.breachDeadline60, count: true },
  { id: 'breachDeadline60/1000', f: X => 1000 * X.breachDeadline60 / X.served },
  { id: 'TV5 s/served', f: X => X.sumTV5 / 1000 / X.served },
  { id: 'G_late60First', f: X => X.G_late60First, count: true },
  { id: 'G_late60First/1000', f: X => (X.G_late60First === null ? null : 1000 * X.G_late60First / X.served) },
  { id: 'guaranteedShare %', f: X => (X.G_n === null ? null : 100 * X.G_n / X.served) },
  { id: 'breachLedger/1000', f: X => 1000 * X.breachLedger / X.served },
  { id: 'relocations', f: X => X.relocations, count: true },
];
const SCOPES = ['day', 'w08', 'w17'];
function contrast(rows, A, B, log) {
  const tA = dayTable(rows, A, log), tB = dayTable(rows, B, log);
  if (!tA.size || !tB.size) return null;
  const res = {};
  for (const pset of ['F14', 'F16', 'pooled']) {
    const days = [...tA.keys()].filter(d => tB.has(d) && (pset === 'pooled' || tA.get(d).panel === pset)).sort();
    for (const d of [...tA.keys(), ...tB.keys()]) if (!(tA.has(d) && tB.has(d))) { const m = `day ${d} missing an arm of ${fmtSel(A)} vs ${fmtSel(B)}`; if (!log.includes(m)) log.push(m); }
    const R = {};
    for (const m of METRICS) for (const sc of SCOPES) {
      const a = days.map(d => m.f(tA.get(d)[sc])), b = days.map(d => m.f(tB.get(d)[sc]));
      if (a.some(x => x === null || !Number.isFinite(x)) || b.some(x => x === null || !Number.isFinite(x))) continue;
      const s = summary(a.map((x, i) => x - b[i]));
      if (!s) continue;
      const sa = a.reduce((x, y) => x + y, 0), sb = b.reduce((x, y) => x + y, 0);
      R[m.id + '|' + sc] = { meanA: sa / days.length, meanB: sb / days.length, rel: m.count && sb ? sa / sb - 1 : null, ...s };
    }
    res[pset] = { days: days.length, R };
  }
  return res;
}
function levels(rows, sel, log) {
  const t = dayTable(rows, sel, log), res = {};
  for (const pset of ['F14', 'F16', 'pooled']) {
    const days = [...t.keys()].filter(d => pset === 'pooled' || t.get(d).panel === pset);
    const R = {};
    for (const m of METRICS) for (const sc of SCOPES) {
      const a = days.map(d => m.f(t.get(d)[sc]));
      if (!a.length || a.some(x => x === null || !Number.isFinite(x))) continue;
      R[m.id + '|' + sc] = a.reduce((x, y) => x + y, 0) / a.length;
    }
    res[pset] = { days: days.length, R };
  }
  return res;
}
const fmtSel = s => `${s.arm}_${s.config}_${s.mode}`;
// difference-in-differences: (A1 - B1) - (A2 - B2) by day
function did(rows, A1, B1, A2, B2, log) {
  const t = [A1, B1, A2, B2].map(s => dayTable(rows, s, log));
  if (t.some(x => !x.size)) return null;
  const res = {};
  for (const pset of ['F14', 'F16', 'pooled']) {
    const days = [...t[0].keys()].filter(d => t.every(x => x.has(d)) && (pset === 'pooled' || t[0].get(d).panel === pset)).sort();
    const R = {};
    for (const m of METRICS) for (const sc of SCOPES) {
      const v = days.map(d => { const q = t.map(x => m.f(x.get(d)[sc])); return q.some(y => y === null || !Number.isFinite(y)) ? null : (q[0] - q[1]) - (q[2] - q[3]); });
      if (!v.length || v.some(x => x === null)) continue;
      R[m.id + '|' + sc] = summary(v);
    }
    res[pset] = { days: days.length, R };
  }
  return res;
}

// ---------------------------------------------------------------- specs
const S = (arm, config, mode) => ({ arm, config, mode });
function specProduction() {
  const C = [];
  for (const cfg of ['INS', 'G2']) {
    C.push({ id: `MAIN ${cfg}: SHRC_CL - SHHZ_CL`, A: S('SHRC', cfg, 'CL'), B: S('SHHZ', cfg, 'CL') });
    C.push({ id: `${cfg}: SHRC_N - SHHZ_N (same contrast without forecast)`, A: S('SHRC', cfg, 'N'), B: S('SHHZ', cfg, 'N') });
    C.push({ id: `${cfg}: SHRC_CL - SHF_CL`, A: S('SHRC', cfg, 'CL'), B: S('SHF', cfg, 'CL') });
    for (const a of ['SHF', 'SHHZ', 'SHRC']) C.push({ id: `FC ${cfg}: ${a}_CL - ${a}_N`, A: S(a, cfg, 'CL'), B: S(a, cfg, 'N') });
    for (const a of ['EOHZ35', 'RCE35']) C.push({ id: `REF ${cfg}: ${a}_CL - ${a}_N`, A: S(a, cfg, 'CL'), B: S(a, cfg, 'N') });
    C.push({ id: `REF ${cfg}: RCE35_CL - EOHZ35_CL`, A: S('RCE35', cfg, 'CL'), B: S('EOHZ35', cfg, 'CL') });
    C.push({ id: `REF ${cfg}: RCE35_N - EOHZ35_N`, A: S('RCE35', cfg, 'N'), B: S('EOHZ35', cfg, 'N') });
    C.push({ id: `SHADOW ${cfg}: SHHZ_CL - EOHZ35_CL`, A: S('SHHZ', cfg, 'CL'), B: S('EOHZ35', cfg, 'CL') });
    C.push({ id: `SHADOW ${cfg}: SHRC_CL - RCE35_CL`, A: S('SHRC', cfg, 'CL'), B: S('RCE35', cfg, 'CL') });
  }
  // whole system (wf13 §3 layout)
  const sys = { a: S('SHHZ', 'INS', 'N'), b: S('SHRC', 'G2', 'CL'), c: S('SHHZ', 'G2', 'CL'), d: S('RCE35', 'G2', 'CL'), e: S('EOHZ35', 'G2', 'CL') };
  C.push({ id: 'SYS b-a: full system (SHRC+G2+CL) - Elmi as in paper (SHHZ+INS+N)', A: sys.b, B: sys.a });
  C.push({ id: 'SYS b-c: full system - fair control (SHHZ+G2+CL)', A: sys.b, B: sys.c });
  C.push({ id: 'SYS b-d: full system - RCE35+G2+CL (what Shadow adds)', A: sys.b, B: sys.d });
  C.push({ id: 'SYS d-a: RCE35+G2+CL (no Shadow) - Elmi as in paper', A: sys.d, B: sys.a });
  C.push({ id: 'SYS c-a: fair control (SHHZ+G2+CL) - Elmi as in paper', A: sys.c, B: sys.a });
  const D = [];
  for (const cfg of ['INS', 'G2']) D.push({ id: `DiD ${cfg}: (SHRC-SHHZ)_CL - (SHRC-SHHZ)_N`, A1: S('SHRC', cfg, 'CL'), B1: S('SHHZ', cfg, 'CL'), A2: S('SHRC', cfg, 'N'), B2: S('SHHZ', cfg, 'N') });
  for (const cfg of ['INS', 'G2']) D.push({ id: `DiD ${cfg}: (SHRC-SHHZ)_CL - (RCE35-EOHZ35)_CL`, A1: S('SHRC', cfg, 'CL'), B1: S('SHHZ', cfg, 'CL'), A2: S('RCE35', cfg, 'CL'), B2: S('EOHZ35', cfg, 'CL') });
  return { C, D, sys };
}
function specEvidence() {          // existing rows only: wf15 main (Shadow N, F14) + wf17 r3 (EOHZ35/RCE35 N and CL, F14 + F16)
  const C = [], D = [];
  for (const cfg of ['INS', 'G2']) {
    C.push({ id: `EV ${cfg}: SHRC_N - SHHZ_N (wf15, F14)`, A: S('SHRC', cfg, 'N'), B: S('SHHZ', cfg, 'N') });
    C.push({ id: `EV ${cfg}: SHF_N - SHHZ_N (wf15, F14)`, A: S('SHF', cfg, 'N'), B: S('SHHZ', cfg, 'N') });
    C.push({ id: `EV ${cfg}: SHHZ_N - EOHZ35_N (wf15, F14)`, A: S('SHHZ', cfg, 'N'), B: S('EOHZ35', cfg, 'N') });
    for (const a of ['EOHZ35', 'RCE35']) C.push({ id: `EV ${cfg}: ${a}_CL - ${a}_N (r3)`, A: S(a, cfg, 'CL'), B: S(a, cfg, 'N') });
    C.push({ id: `EV ${cfg}: RCE35_CL - EOHZ35_CL (r3)`, A: S('RCE35', cfg, 'CL'), B: S('EOHZ35', cfg, 'CL') });
    C.push({ id: `EV ${cfg}: RCE35_N - EOHZ35_N (r3)`, A: S('RCE35', cfg, 'N'), B: S('EOHZ35', cfg, 'N') });
    D.push({ id: `EV DiD ${cfg}: (RCE35-EOHZ35)_CL - (RCE35-EOHZ35)_N (r3)`, A1: S('RCE35', cfg, 'CL'), B1: S('EOHZ35', cfg, 'CL'), A2: S('RCE35', cfg, 'N'), B2: S('EOHZ35', cfg, 'N') });
  }
  C.push({ id: 'EV SYS: RCE35_G2_CL - SHHZ_INS_N', A: S('RCE35', 'G2', 'CL'), B: S('SHHZ', 'INS', 'N') });
  return { C, D, sys: { a: S('SHHZ', 'INS', 'N'), d: S('RCE35', 'G2', 'CL'), e: S('EOHZ35', 'G2', 'CL') } };
}

// ---------------------------------------------------------------- output
const f2 = x => (x === null || x === undefined || !Number.isFinite(x) ? 'n/a' : Math.abs(x) >= 100 ? x.toFixed(1) : x.toFixed(2));
const fp = p => (p < 0.001 ? p.toExponential(1) : p.toFixed(3));
const cellTxt = s => (s ? `${f2(s.mean)} ± ${f2(s.se)} [${f2(s.lo)}, ${f2(s.hi)}] ${s.nMinus}-/${s.nPlus}+/${s.ties}= p ${fp(s.p)}` : 'n/a');
function main() {
  const files = (arg('rows') || '').split(',').filter(Boolean);
  const spec = arg('spec') === 'evidence' ? specEvidence() : specProduction();
  const outP = arg('out');
  if (!files.length || !outP) throw new Error('--rows and --out required');
  const { rows, conflicts, dupSame } = loadRows(files);
  const log = [];
  const J = { label: LABEL, files, nRows: rows.length, dupSame, conflicts, created: new Date().toISOString(), contrasts: {}, did: {}, levels: {} };
  const T = [];
  T.push(`${LABEL}`, `rows ${rows.length} from ${files.map(f => path.basename(f)).join(', ')}; duplicate keys identical ${dupSame}; conflicting ${conflicts.length}`);
  if (conflicts.length) T.push('CONFLICTS (first 5): ' + conflicts.slice(0, 5).join('; '));
  T.push('Cell format: mean Δ ± SE [95 % t-CI] n−/n+/ties= p (exact two-sided sign test, days). rel = ΣA/ΣB − 1 (counts). Day = w08 + w17 of the resample mean.', '');
  for (const c of spec.C) {
    const r = contrast(rows, c.A, c.B, log);
    J.contrasts[c.id] = r;
    T.push('=== ' + c.id + (r ? '' : '   (n/a: no rows)'));
    if (!r) { T.push(''); continue; }
    T.push(`days F14 ${r.F14.days}, F16 ${r.F16.days}, pooled ${r.pooled.days}`);
    T.push(['metric|scope'.padEnd(28), 'A pooled', 'B pooled', 'rel', '| F14', '| F16', '| pooled'].join('  '));
    for (const m of METRICS) for (const sc of SCOPES) {
      const k = m.id + '|' + sc, p = r.pooled.R[k];
      if (!p) continue;
      T.push([k.padEnd(28), f2(p.meanA).padStart(8), f2(p.meanB).padStart(8), (p.rel === null ? '' : (100 * p.rel).toFixed(1) + '%').padStart(7),
        '| ' + cellTxt(r.F14.R[k]), '| ' + cellTxt(r.F16.R[k]), '| ' + cellTxt(p)].join('  '));
    }
    T.push('');
  }
  for (const c of spec.D) {
    const r = did(rows, c.A1, c.B1, c.A2, c.B2, log);
    J.did[c.id] = r;
    T.push('=== ' + c.id + (r ? '' : '   (n/a: no rows)'));
    if (!r) { T.push(''); continue; }
    T.push(`days F14 ${r.F14.days}, F16 ${r.F16.days}, pooled ${r.pooled.days}`);
    for (const m of METRICS) for (const sc of SCOPES) { const k = m.id + '|' + sc; if (!r.pooled.R[k]) continue;
      T.push([k.padEnd(28), '| ' + cellTxt(r.F14.R[k]), '| ' + cellTxt(r.F16.R[k]), '| ' + cellTxt(r.pooled.R[k])].join('  ')); }
    T.push('');
  }
  // whole-system levels
  T.push('=== WHOLE SYSTEM LEVELS (day means; per-1000 = per 1000 served) ');
  const keys = Object.keys(spec.sys);
  for (const k of keys) J.levels[k] = { sel: spec.sys[k], ...levels(rows, spec.sys[k], log) };
  for (const pset of ['F14', 'F16', 'pooled']) {
    T.push(`-- ${pset}: ` + keys.map(k => `${k}=${fmtSel(spec.sys[k])} (${J.levels[k][pset].days} d)`).join(', '));
    for (const m of METRICS) for (const sc of SCOPES) { const kk = m.id + '|' + sc; if (keys.every(k => J.levels[k][pset].R[kk] === undefined)) continue;
      T.push(kk.padEnd(28) + keys.map(k => f2(J.levels[k][pset].R[kk]).padStart(10)).join('')); }
  }
  T.push('', 'LOG (exclusions):', ...(log.length ? [...new Set(log)] : ['none']));
  J.log = [...new Set(log)];
  fs.writeFileSync(outP + '.txt', T.join('\n') + '\n');
  fs.writeFileSync(outP + '.json', JSON.stringify(J, null, 1));
  console.log(`wrote ${outP}.txt/.json; rows ${rows.length}; conflicts ${conflicts.length}; log ${J.log.length}`);
}
module.exports = { summary, signP, tq975, loadRows, dayTable, contrast, did, METRICS };
if (require.main === module) main();
