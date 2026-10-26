// Independent verifier library for wf21/n7 (written from scratch; does not import producer code).
'use strict';
const fs = require('fs');
const readNd = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
const base = a => a.replace(/\d+$/, '');
// exact two-sided sign test: 2 * P(X <= min(k, n-k)), X ~ Bin(n, 1/2), capped at 1
function signP(nPlus, nMinus) {
  const n = nPlus + nMinus; if (n === 0) return 1;
  const k = Math.min(nPlus, nMinus);
  // use BigInt binomial coefficients for exactness
  let c = 1n, s = 0n;
  for (let i = 0; i <= k; i++) { if (i > 0) c = c * BigInt(n - i + 1) / BigInt(i); s += c; }
  const p = 2 * Number(s) / Math.pow(2, n);
  return Math.min(1, p);
}
// t 0.975 quantiles (standard tables)
const T975 = { 1: 12.7062047, 2: 4.30265273, 5: 2.57058184, 9: 2.26215716, 12: 2.17881283, 13: 2.16036866, 14: 2.14478669, 15: 2.13144955, 27: 2.05183052, 28: 2.04840714, 29: 2.04522964 };
function summ(d) {
  const n = d.length; const mean = d.reduce((a, b) => a + b, 0) / n;
  const sd = Math.sqrt(d.reduce((a, b) => a + (b - mean) ** 2, 0) / (n - 1)); const se = sd / Math.sqrt(n);
  const t = T975[n - 1]; if (!t) throw new Error('no t for df ' + (n - 1));
  const np = d.filter(x => x > 1e-9).length, nm = d.filter(x => x < -1e-9).length;
  const s = d.slice().sort((a, b) => a - b); const med = n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
  return { n, mean, se, lo: mean - t * se, hi: mean + t * se, med, nm, np, ties: n - np - nm, p: signP(np, nm) };
}
const KEYS = ['served', 'late60First', 'early60First', 'visRev60', 'pDisp', 'breachDeadline60', 'sumTV5', 'G_late60First', 'breachLedger', 'G_n', 'plSumArr_ms', 'plN', 'relocations'];
// rows -> Map key "config|mode|arm" -> Map day -> {panel, w08:{...means}, w17:{...}, day:{...sums}}
function dayTables(rows, modeOf) {
  const cell = new Map();
  for (const r of rows) {
    const mode = modeOf(r); if (!mode) continue;
    const k = [r.config, mode, base(r.arm)].join('|');
    if (!cell.has(k)) cell.set(k, new Map());
    const m = cell.get(k); const dk = r.day;
    if (!m.has(dk)) m.set(dk, { panel: r.panel, cells: {} });
    const ck = r.win + '|' + r.rs;
    if (m.get(dk).cells[ck]) throw new Error('dup ' + k + ' ' + r.cell);
    m.get(dk).cells[ck] = r;
  }
  const out = new Map();
  for (const [k, m] of cell) {
    const t = new Map();
    for (const [day, o] of m) {
      const W = {};
      for (const w of ['w08', 'w17']) {
        const cs = ['r0', 'r1', 'r2'].map(rs => o.cells[w + '|' + rs]);
        if (cs.some(c => !c)) throw new Error('incomplete ' + k + ' ' + day + ' ' + w);
        W[w] = {}; for (const f of KEYS) W[w][f] = cs.reduce((a, c) => a + (c[f] ?? NaN), 0) / 3;
      }
      const D = {}; for (const f of KEYS) D[f] = W.w08[f] + W.w17[f];
      t.set(day, { panel: o.panel, w08: W.w08, w17: W.w17, day: D });
    }
    out.set(k, t);
  }
  return out;
}
const MET = {
  late: X => 1000 * X.late60First / X.served,
  early: X => 1000 * X.early60First / X.served,
  rev: X => X.visRev60,
  served: X => X.served,
  tv5: X => X.sumTV5 / 1000 / X.served,
  dead: X => 1000 * X.breachDeadline60 / X.served,
  glate: X => X.G_late60First,
  pchg: X => 1000 * X.pDisp / X.served,
  pl: X => X.plSumArr_ms / X.plN / 1000,
  gshare: X => 100 * X.G_n / X.served,
};
function diffs(T, A, B, met, scope, panel) {
  const ta = T.get(A), tb = T.get(B); if (!ta || !tb) throw new Error('missing ' + A + ' or ' + B);
  const days = [...ta.keys()].filter(d => tb.has(d) && (!panel || panel === 'pooled' || ta.get(d).panel === panel)).sort();
  return { days, d: days.map(d => MET[met](ta.get(d)[scope]) - MET[met](tb.get(d)[scope])), a: days.map(d => MET[met](ta.get(d)[scope])), b: days.map(d => MET[met](tb.get(d)[scope])) };
}
const level = (T, A, met, scope, panel) => { const t = T.get(A); const days = [...t.keys()].filter(d => !panel || panel === 'pooled' || t.get(d).panel === panel); return days.reduce((s, d) => s + MET[met](t.get(d)[scope]), 0) / days.length; };
function ols(x, y) { const n = x.length, mx = x.reduce((a, b) => a + b, 0) / n, my = y.reduce((a, b) => a + b, 0) / n;
  let sxx = 0, sxy = 0, syy = 0; for (let i = 0; i < n; i++) { sxx += (x[i] - mx) ** 2; sxy += (x[i] - mx) * (y[i] - my); syy += (y[i] - my) ** 2; }
  const b = sxy / sxx, a = my - b * mx; let rss = 0; for (let i = 0; i < n; i++) rss += (y[i] - a - b * x[i]) ** 2;
  const s2 = rss / (n - 2), seb = Math.sqrt(s2 / sxx), sea = Math.sqrt(s2 * (1 / n + mx * mx / sxx));
  return { a, b, seb, sea, r: sxy / Math.sqrt(sxx * syy), n, mx, my }; }
const f = (x, d = 2) => (x >= 0 ? '+' : '') + x.toFixed(d);
const fs_ = s => `${f(s.mean)} [${f(s.lo)}, ${f(s.hi)}] ${s.nm}-/${s.np}+ p ${s.p < 0.001 ? s.p.toExponential(1) : s.p.toFixed(3)}`;
module.exports = { readNd, base, signP, summ, dayTables, MET, diffs, level, ols, f, fs_, KEYS };
