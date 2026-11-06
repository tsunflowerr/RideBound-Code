#!/usr/bin/env node
// Policy curves: how promise reversals, dispatch changes and deadline breaches move along each rule's parameter, compared
// at the same level of service (study R6 of wf17, exploratory, run after the audit). The system report's §6 sentence
// "reversals +20-91 % at the same service level" is the rel gap column of "frontier SHHZ points vs EOHZ curve",
// visRev60 /1000: INS +19.8 % (x = 50) up to +91.2 % (x = 90), G2 +20.3 % up to +78.6 %.
// Rebuilt from the recorded emulator rows of the study. Nothing is rerun.
//
// The rows hold one emulator run per cell x config x arm: the 30 days of D30 (data/panels.json) x 2 windows (w08: 08-10 h,
// w17: 17-19 h) x resample r0 = 60 cells, 2 configs and 27 arms per config = 3,240 rows.
//   configs  INS        insertion only
//            G2         insertion, plus moving at most one waiting rider to one of the 8 nearest vehicles per decision
//   arms     U          no limit on dispatch changes
//            EOHZ<a>    Elmi's relaxed fence: the pickup may move up to B = round(a/100 x slack) past the first promise,
//                       but never tighter than the keep-route projection; a = 0, 10, 20, 35, 50, 75, 100
//            RCE<a>     Rule C: the changes of a rider, summed in absolute value, stay within the same B (a per-rider ledger)
//            SHHZ<x>    the relaxed fence with the Shadow budget: 0 for guaranteed riders, the full slack for allowed riders
//            SHRC<x>    Rule C with the Shadow budget. The arm name carries the Shadow share x: 50, 60, 70, 80, 90 and
//                       x* (72 for INS, 71 for G2, the share of the 60-day study)
//
// The unit is the day: each count of a day is w08 + w17; a day that misses a window is left out and logged. Every Y is
// per served rider of that day:
//   Pchg (pp)              100 pDisp / served (riders the dispatcher pushed later by >= 5 s); the main curve metric
//   pDispAny1 (pp)         the same for a dispatch change of >= 1 s either way
//   visRev60 /1000         promise reversals (a shown change >= 60 s one way, later one the other way)
//   TV5/served (s)         total promise movement with a 5 s deadband
//   breachDeadline60 /1000 deadline breaches, each arm against its own budget (so this yardstick moves with a and x)
//   late60First /1000      arrival > 60 s after the first promise
//   breachLedger /1000     0 by construction for the RC arms; printed, never used as a result
//   relocations, switchedRiders /1000   G2 only
//
// Frontier comparison (PRIMARY, per day). Each day, the comparator arms (EOHZ over a, or SHHZ over x) give points
// (served, Y); points with equal served are averaged and Y is interpolated linearly in served. For a target arm,
// Delta_d = Y_target - Y_comparator(served_target). A day whose target served lies outside the comparator's served range
// is left out and counted (below / above). Per target arm: mean +- SE with a 95 % t interval, median, exact two-sided
// sign test (ties |Delta| <= 1e-9 dropped), days in favour = share of the untied days with Delta < 0 (lower Y is better
// for every Y here), rel gap = sum Y_target / sum Y_comparator - 1 over the days used.
// Comparisons: RCE(a) vs the EOHZ curve; SHRC(x) vs the SHHZ curve; SHHZ(x) vs the EOHZ curve (Elmi's own claim).
// SECONDARY, pooled: the same gap on the curves of the 30-day means, with a 95 % percentile interval from 10,000 day
// bootstrap resamples (days drawn with replacement, mulberry32 seeded with FNV-1a('wf17-r6'); one draw per resample is
// shared by every comparison and metric of the config; a resample whose target falls outside the comparator range is NA,
// and the NA share is printed). Only days with both windows for every arm of the config are drawn.
//
// Decision labels, written before the run. A target point is EVALUABLE if >= 10 of the 30 days are in the comparator's
// served range.
//   C9 reversals (claim C9 of the study plan: Rule C has fewer reversals than the relaxed fence at the same service;
//     Y = visRev60 /1000, RCE vs EOHZ, the 6 points a = 10, 20, 35, 50, 75, 100):
//     a point HOLDS if evaluable, rel gap <= -20 % and days in favour >= 75 %; WITHIN10 if evaluable and |rel gap| <= 10 %;
//     ABOVE if evaluable and mean Delta > 0. Label: REVERSED if >= 3 points ABOVE; else WEAKENED if >= 3 points WITHIN10;
//     else HOLDS if >= 4 points HOLD; else INCONCLUSIVE. G2 is the test; INS gets the same rule as a SECONDARY label.
//   C9 P(change) ("Rule C costs nothing in Elmi's P(change)"), per config, the same 6 points, Y = Pchg: a point is WORSE
//     if evaluable and mean Delta > +2 pp. WEAKENED if >= 3 points WORSE; else HOLDS if >= 4 points evaluable; else
//     INCONCLUSIVE.
//   C9 overall (G2): REVERSED if the G2 reversal label is REVERSED; else WEAKENED if the G2 reversal or the G2 Pchg label
//     is WEAKENED; else HOLDS if both HOLD; else INCONCLUSIVE.
//   Elmi replication (SHHZ vs EOHZ, Y = Pchg), per config, over the 6 SHHZ points: a point is LOWER if evaluable, mean
//     Delta < 0 and days in favour >= 75 %; NOT-LOWER if evaluable and mean Delta >= 0. REPLICATED if the LOWER points are
//     more than half of the evaluable points; NOT REPLICATED if the NOT-LOWER points are; else MIXED; INCONCLUSIVE if
//     fewer than 3 points are evaluable. Not a Rule C result.
//   SHRC vs SHHZ (reversals, Pchg) and RCE vs EOHZ deadline breach: the same LOWER / NOT-LOWER count, descriptive.
// Descriptive extras (no label): equal-parameter paired contrasts RCE(a) - EOHZ(a) and SHRC(x) - SHHZ(x), the exchange
// rates at each equal a (ARR, ER_served, ER_deadline, ER_late), and the study plan's predictions, counted as the points
// that meet each prediction / the evaluable points.
//
// Gates. The arms that repeat earlier runs must equal those rows in every field except labels, timing and the a5 driver
// field nReq (which must equal requests): a = 10, 20, 50, 100 against the wf16 a5 rows (variants V7-V10); EOHZ35, RCE35
// and U against wf14 main r0; SHHZ and SHRC at x*, U, EOHZ35 and RCE35 against the 60-day rows (wf15). Then the row count:
// 60 x 2 x 27 = 3,240, no duplicate (cell, config, arm).
// The a5 gate prints "0/960 equal ... MISMATCH": the a5 rows name the arm "EOHZ" or "RCE" (a is in the variant), and the
// field check compares that name with "EOHZ10" and so on; arm is the only field listed as different (tests/ check that
// all 960 rows agree on every other field). The stored tables carry the same line, so the check stays as it was.
//
// Output: <prefix>.txt and <prefix>.json (the same results; every member named days or deltas is left out, which also
// drops the day count of the pooled section). Line 1 of the txt holds the run time and the sha256 of this script; every
// later line is fixed. stdout repeats the gate and label lines (the /C9/ pattern also picks up the SHRC90 rows).
// The shipped inputs are data/wf17-r6-rows.ndjson.gz, data/wf16-a5-alpha-rows.ndjson.gz, data/wf14-main-r0-rows.ndjson.gz
// and data/wf15-confirm-rows.ndjson.gz; the a5 and wf14 files hold only the rows these gates read (the selection is in
// data/MANIFEST.tsv). expected/policy-curves.txt and .json are the stored outputs.
//
// Usage: node policy_curves.js --rows <r6 rows> --a5 <a5 rows> --wf14 <wf14 rows> --wf15 <wf15 rows> --out <prefix>
//   Every rows file is .ndjson or .ndjson.gz. Writes <prefix>.txt and <prefix>.json.
'use strict';
const crypto = require('crypto'), fs = require('fs'), zlib = require('zlib');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
if (!['rows', 'a5', 'wf14', 'wf15', 'out'].every(k => arg(k))) {
  throw new Error('usage: node policy_curves.js --rows <r6 rows> --a5 <a5 rows> --wf14 <wf14 rows> --wf15 <wf15 rows> --out <prefix>');
}
for (const name of ['R6_NBOOT', 'R6_SKIP_GATES']) {
  if (process.env[name] !== undefined) throw new Error(`${name} removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)`);
}
const readText = file => { const raw = fs.readFileSync(file); return (file.endsWith('.gz') ? zlib.gunzipSync(raw) : raw).toString('utf8'); };
const readNd = file => readText(file).split('\n').filter(Boolean).map(l => JSON.parse(l));

// ---------------------------------------------------------------- statistics
const sum = a => a.reduce((s, x) => s + x, 0);
const mean = a => (a.length ? sum(a) / a.length : NaN);
const sd = a => {
  if (a.length < 2) return NaN;
  const m = mean(a);
  return Math.sqrt(a.reduce((s, x) => s + (x - m) ** 2, 0) / (a.length - 1));
};
const median = a => {
  if (!a.length) return NaN;
  const s = a.slice().sort((x, y) => x - y), n = s.length;
  return n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
};

// exact two-sided sign test (binomial with p = 1/2); |d| <= eps counts as a tie and is dropped
function signTest(d, eps = 1e-9) {
  let pos = 0, neg = 0, ties = 0;
  for (const x of d) { if (x > eps) pos++; else if (x < -eps) neg++; else ties++; }
  const n = pos + neg, k = Math.min(pos, neg);
  let p = 1;
  if (n > 0) {
    let c = 1, acc = 0;   // c = n choose i
    for (let i = 0; i <= k; i++) { if (i > 0) c = c * (n - i + 1) / i; acc += c; }
    p = Math.min(1, 2 * acc / 2 ** n);
  }
  return { pos, neg, ties, n, p };
}

// Student t quantile for a two-sided 95 % interval: bisection on the two-sided tail probability, which is the regularised
// incomplete beta function I_x(df/2, 1/2) with x = df / (df + t^2) (log Gamma and continued fraction as in Numerical Recipes)
function gammaln(x) {
  const c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
  let y = x, t = x + 5.5;
  t -= (x + 0.5) * Math.log(t);
  let s = 1.000000000190015;
  for (const ci of c) s += ci / ++y;
  return -t + Math.log(2.5066282746310005 * s / x);
}
function betacf(a, b, x) {
  const MAXIT = 300, EPS = 3e-14, FPMIN = 1e-300;
  let qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap;
  if (Math.abs(d) < FPMIN) d = FPMIN;
  d = 1 / d;
  let h = d;
  for (let m = 1; m <= MAXIT; m++) {
    const m2 = 2 * m;
    let aa = m * (b - m) * x / ((qam + m2) * (a + m2));
    d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN;
    c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN;
    d = 1 / d; h *= d * c;
    aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
    d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN;
    c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN;
    d = 1 / d;
    const del = d * c;
    h *= del;
    if (Math.abs(del - 1) < EPS) break;
  }
  return h;
}
function ibeta(a, b, x) {
  if (x <= 0) return 0;
  if (x >= 1) return 1;
  const bt = Math.exp(gammaln(a + b) - gammaln(a) - gammaln(b) + a * Math.log(x) + b * Math.log(1 - x));
  return x < (a + 1) / (a + b + 2) ? bt * betacf(a, b, x) / a : 1 - bt * betacf(b, a, 1 - x) / b;
}
const tTwoSidedP = (t, df) => ibeta(df / 2, 0.5, df / (df + t * t));
const tCache = new Map();
function t975(df) {
  if (tCache.has(df)) return tCache.get(df);
  let lo = 0, hi = 1000;
  for (let i = 0; i < 200; i++) { const mid = (lo + hi) / 2; if (tTwoSidedP(mid, df) > 0.05) lo = mid; else hi = mid; }
  tCache.set(df, (lo + hi) / 2);
  return (lo + hi) / 2;
}

// Paired contrast over days. a, b: Map(day -> value), compared on the days both have with finite values.
// better = 'lower' | 'higher' | null: the side that counts as "in favour". For counts, rel = 1 - sum(a) / sum(b).
function contrast(a, b, { better = 'lower', count = true, scale = 1 } = {}) {
  const days = [...a.keys()].filter(d => b.has(d) && Number.isFinite(a.get(d)) && Number.isFinite(b.get(d))).sort();
  const d = days.map(x => (a.get(x) - b.get(x)) * scale);
  const n = d.length, m = mean(d), s = sd(d), se = s / Math.sqrt(n), h = n > 1 ? t975(n - 1) * se : NaN;
  const st = signTest(d);
  const sa = sum(days.map(x => a.get(x))), sb = sum(days.map(x => b.get(x)));
  const favour = better === 'lower' ? st.neg : better === 'higher' ? st.pos : null;
  return { nDays: n, days, mean: m, se, lo: m - h, hi: m + h, median: median(d), ...st,
    rel: count && sb !== 0 ? 1 - sa / sb : null, meanA: sa / n, meanB: sb / n,
    favour, favourShareNonTied: favour === null || st.n === 0 ? null : favour / st.n,
    favourShareAll: favour === null || !n ? null : favour / n, deltas: Object.fromEntries(days.map((x, i) => [x, d[i]])) };
}
// f: fixed decimals, NA for null / undefined / NaN; fp: a p value (exponent below 1e-4, else 2 significant digits)
const f = (x, k = 2) => (x === null || x === undefined || Number.isNaN(x) ? 'NA' : typeof x === 'number' ? x.toFixed(k) : String(x));
const fp = p => (p === null || p === undefined || Number.isNaN(p) ? 'NA' : p < 1e-4 ? p.toExponential(1) : p.toPrecision(2));
function fmtC(c, { k = 2, unit = '' } = {}) {
  return `mean ${f(c.mean, k)}${unit} [${f(c.lo, k)}, ${f(c.hi, k)}] SE ${f(c.se, k)} med ${f(c.median, k)} | ${c.neg}-/${c.pos}+/${c.ties}= of ${c.nDays}, sign p ${fp(c.p)}` +
    (c.rel !== null && c.rel !== undefined ? ` | rel ${f(100 * c.rel, 1)}%` : '') + ` | A ${f(c.meanA, k)} B ${f(c.meanB, k)}`;
}

// ---------------------------------------------------------------- arms: the families over a (EOHZ, RCE) and over x (SHHZ, SHRC)
const NBOOT = 10000;
const XS = { INS: 72, G2: 71 };   // x* of each config
const ALPHAS = [0, 10, 20, 35, 50, 75, 100];
const XG = cfg => [50, 60, 70, XS[cfg], 80, 90].sort((a, b) => a - b);
const FAM = {
  EOHZ: cfg => ALPHAS.map(a => 'EOHZ' + a), RCE: cfg => ALPHAS.map(a => 'RCE' + a),
  SHHZ: cfg => XG(cfg).map(x => 'SHHZ' + x), SHRC: cfg => XG(cfg).map(x => 'SHRC' + x)
};
const parOf = arm => Number(/\d+$/.exec(arm)[0]);   // EOHZ35 -> 35
const LINES = [], LOG = [];
const P = s => LINES.push(s);

// only the rows of this study are used; a repeated (cell, config, arm) keeps its first row (logged below)
const rows = readNd(arg('rows')).filter(r => r.label === 'wf17-exploratory-post-audit' && r.variant === 'r6');
const SUM = { label: 'wf17-exploratory-post-audit', check: 'R6', nRows: rows.length, curves: {}, frontier: {}, pooled: {},
  equal: {}, rates: {}, labels: {}, predictions: {}, gates: {}, log: LOG };

// ---------------------------------------------------------------- day values: config|arm -> Map(day -> counts of w08 + w17)
const CNT = ['served', 'pDisp', 'pDispAny1', 'visRev60', 'sumTV5', 'breachDeadline60', 'late60First', 'breachLedger',
  'relocations', 'switchedRiders'];
const byArm = new Map();
{
  const tmp = new Map();
  const seen = new Set();
  for (const r of rows) {
    const uk = [r.cell, r.config, r.arm].join('|');
    if (seen.has(uk)) { LOG.push('duplicate row ' + uk); continue; }
    seen.add(uk);
    const k = r.config + '|' + r.arm;
    if (!tmp.has(k)) tmp.set(k, new Map());
    const dm = tmp.get(k);
    if (!dm.has(r.day)) dm.set(r.day, {});
    dm.get(r.day)[r.win] = r;
  }
  for (const [k, dm] of tmp) {
    const out = new Map();
    for (const [d, w] of dm) {
      if (!(w.w08 && w.w17)) { LOG.push(`excluded ${k} ${d}: missing window`); continue; }
      const o = {};
      for (const c of CNT) o[c] = (w.w08[c] || 0) + (w.w17[c] || 0);
      out.set(d, o);
    }
    byArm.set(k, out);
  }
}
const YDEF = {
  'Pchg (pp)': o => 100 * o.pDisp / o.served,
  'pDispAny1 (pp)': o => 100 * o.pDispAny1 / o.served,
  'visRev60 /1000': o => 1000 * o.visRev60 / o.served,
  'TV5/served (s)': o => o.sumTV5 / 1000 / o.served,
  'breachDeadline60 /1000': o => 1000 * o.breachDeadline60 / o.served,
  'late60First /1000': o => 1000 * o.late60First / o.served,
  'breachLedger /1000 [BY-CONSTRUCTION for RC]': o => 1000 * o.breachLedger / o.served,
  'relocations /1000 (G2)': o => 1000 * o.relocations / o.served,
  'switchedRiders /1000 (G2)': o => 1000 * o.switchedRiders / o.served,
};
const YKEYS = cfg => Object.keys(YDEF).filter(k => cfg === 'G2' || !/\(G2\)/.test(k));
const FRONT_Y = ['Pchg (pp)', 'visRev60 /1000', 'breachDeadline60 /1000', 'TV5/served (s)', 'pDispAny1 (pp)', 'late60First /1000'];
const dayMap = (cfg, arm) => byArm.get(cfg + '|' + arm) || new Map();
const yMap = (cfg, arm, yk) => new Map([...dayMap(cfg, arm)].map(([d, o]) => [d, YDEF[yk](o)]));
const sMap = (cfg, arm) => new Map([...dayMap(cfg, arm)].map(([d, o]) => [d, o.served]));

// ---------------------------------------------------------------- frontier: Y of a curve at served s, by linear interpolation
function frontierAt(points, s) {
  const g = new Map();
  for (const p of points) {
    if (!Number.isFinite(p.s) || !Number.isFinite(p.y)) continue;
    if (!g.has(p.s)) g.set(p.s, []);
    g.get(p.s).push(p.y);
  }
  const q = [...g].map(([sv, ys]) => ({ s: sv, y: mean(ys) })).sort((a, b) => a.s - b.s);
  if (!q.length) return { v: null, why: 'empty' };
  if (s < q[0].s) return { v: null, why: 'below' };
  if (s > q[q.length - 1].s) return { v: null, why: 'above' };
  for (let i = 0; i < q.length; i++) {
    if (q[i].s === s) return { v: q[i].y };
    if (i + 1 < q.length && s > q[i].s && s < q[i + 1].s) {
      const t = (s - q[i].s) / (q[i + 1].s - q[i].s);
      return { v: q[i].y + t * (q[i + 1].y - q[i].y) };
    }
  }
  return { v: null, why: 'nan' };
}

// ---------------------------------------------------------------- curves (30-day means)
P('wf17 R6 policy curves (EXPLORATORY, POST-AUDIT). Unit = day (w08 + w17). Means over days; per-day shares of served.');
for (const cfg of ['INS', 'G2']) {
  P(`\n==================== ${cfg}: curves (mean over days; served/day and per-day Y) ====================`);
  const yk = YKEYS(cfg);
  P(['arm'.padEnd(8), 'days'.padStart(4), 'served'.padStart(8), 'SE'.padStart(5),
    ...yk.map(k => k.replace(' [BY-CONSTRUCTION for RC]', '*').padStart(12))].join(' '));
  const C = [];
  for (const fam of ['U', 'EOHZ', 'RCE', 'SHHZ', 'SHRC']) {
    const arms = fam === 'U' ? ['U'] : FAM[fam](cfg);
    for (const arm of arms) {
      const sm = sMap(cfg, arm);
      if (!sm.size) { LOG.push(`no rows ${cfg} ${arm}`); continue; }
      const sv = [...sm.values()];
      const rec = { config: cfg, family: fam, param: fam === 'U' ? null : parOf(arm), arm, nDays: sv.length, served: mean(sv),
        servedSE: sd(sv) / Math.sqrt(sv.length), Y: {} };
      for (const k of yk) {
        const v = [...yMap(cfg, arm, k).values()];
        rec.Y[k] = mean(v);
        rec.Y[k + ' SE'] = sd(v) / Math.sqrt(v.length);
      }
      C.push(rec);
      P([arm.padEnd(8), String(rec.nDays).padStart(4), f(rec.served, 1).padStart(8), f(rec.servedSE, 1).padStart(5),
        ...yk.map(k => f(rec.Y[k], 2).padStart(12))].join(' '));
    }
  }
  SUM.curves[cfg] = C;
}
P('(* breachLedger is BY-CONSTRUCTION 0 for RC arms; breachDeadline60 is measured against each arm\'s own budget; U has no budget, so its breach fields are 0 by definition.)');

// ---------------------------------------------------------------- PRIMARY: frontier comparisons, per day
const COMPS = [
  { id: 'RCE_vs_EOHZ', target: 'RCE', comp: 'EOHZ' },
  { id: 'SHRC_vs_SHHZ', target: 'SHRC', comp: 'SHHZ' },
  { id: 'SHHZ_vs_EOHZ', target: 'SHHZ', comp: 'EOHZ' },
];
function frontierPoint(cfg, tArm, compArms, yk) {
  const tS = sMap(cfg, tArm), tY = yMap(cfg, tArm, yk);
  const cS = compArms.map(a => sMap(cfg, a)), cY = compArms.map(a => yMap(cfg, a, yk));
  const a = new Map(), b = new Map();
  let below = 0, above = 0, missing = 0;
  for (const d of [...tS.keys()].sort()) {
    if (!compArms.every((_, i) => cS[i].has(d))) { missing++; continue; }
    const pts = compArms.map((_, i) => ({ s: cS[i].get(d), y: cY[i].get(d) }));
    const fr = frontierAt(pts, tS.get(d));
    if (fr.v === null) {
      if (fr.why === 'below') below++; else if (fr.why === 'above') above++; else missing++;
      continue;
    }
    a.set(d, tY.get(d)); b.set(d, fr.v);
  }
  const c = contrast(a, b, { better: 'lower', count: true });
  const relGap = c.nDays && c.meanB !== 0 ? c.meanA / c.meanB - 1 : null;
  return { arm: tArm, param: parOf(tArm), nIncl: c.nDays, below, above, missing, evaluable: c.nDays >= 10, mean: c.mean, se: c.se,
    lo: c.lo, hi: c.hi, median: c.median, neg: c.neg, pos: c.pos, ties: c.ties, p: c.p, favour: c.favourShareNonTied, relGap,
    meanTarget: c.meanA, meanCompInterp: c.meanB, deltas: c.deltas };
}
for (const cfg of ['INS', 'G2']) {
  SUM.frontier[cfg] = {};
  for (const cp of COMPS) {
    SUM.frontier[cfg][cp.id] = {};
    P(`\n==================== ${cfg}: frontier ${cp.target} points vs ${cp.comp} curve, PRIMARY per day (Delta = target - comparator at matched served) ====================`);
    for (const yk of FRONT_Y) {
      P(`-- ${yk}`);
      P('   point    incl below above |   mean Delta   [95% CI]          SE   median | n-/n+/ties  sign p | in favour | rel gap | target  comp(interp)');
      const pts = FAM[cp.target](cfg).map(arm => frontierPoint(cfg, arm, FAM[cp.comp](cfg), yk));
      SUM.frontier[cfg][cp.id][yk] = pts;
      for (const q of pts) P(`   ${q.arm.padEnd(8)} ${String(q.nIncl).padStart(4)} ${String(q.below).padStart(5)} ${String(q.above).padStart(5)} | ${f(q.mean, 3).padStart(8)} [${f(q.lo, 3)}, ${f(q.hi, 3)}] ${f(q.se, 3).padStart(7)} ${f(q.median, 3).padStart(8)} | ${q.neg}-/${q.pos}+/${q.ties}= p ${fp(q.p)} | ${q.favour === null ? 'NA' : f(100 * q.favour, 0) + '%'} | ${q.relGap === null ? 'NA' : f(100 * q.relGap, 1) + '%'} | ${f(q.meanTarget, 3)} ${f(q.meanCompInterp, 3)}${q.evaluable ? '' : '  [not evaluable: < 10 days in range]'}`);
    }
  }
}

// ---------------------------------------------------------------- SECONDARY: pooled curves and the day bootstrap
function fnv1a(s) {
  let h = 0x811c9dc5;
  for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; }
  return h >>> 0;
}
function mulberry32(a) {
  return function () {
    a |= 0; a = a + 0x6D2B79F5 | 0;
    let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}
const rnd = mulberry32(fnv1a('wf17-r6'));
const BOOT_Y = ['Pchg (pp)', 'visRev60 /1000', 'breachDeadline60 /1000', 'TV5/served (s)', 'pDispAny1 (pp)'];
// percentile q of a, linear between order statistics
const pct = (a, q) => {
  if (!a.length) return NaN;
  const s = a.slice().sort((x, y) => x - y);
  const i = (s.length - 1) * q, lo = Math.floor(i), hi = Math.ceil(i);
  return s[lo] + (s[hi] - s[lo]) * (i - lo);
};
for (const cfg of ['INS', 'G2']) {
  const allArms = ['U', ...FAM.EOHZ(cfg), ...FAM.RCE(cfg), ...FAM.SHHZ(cfg), ...FAM.SHRC(cfg)];
  const dayset = [...new Set([...dayMap(cfg, 'U').keys()])].filter(d => allArms.every(a => dayMap(cfg, a).has(d))).sort();
  const dropped = [...new Set(allArms.flatMap(a => [...dayMap(cfg, a).keys()]))].filter(d => !dayset.includes(d));
  if (dropped.length) LOG.push(`${cfg} bootstrap: days dropped (some arm missing): ${dropped.join(',')}`);
  const D = dayset.length;
  // arrays [arm][day] of served and of each Y
  const S = Object.fromEntries(allArms.map(a => [a, dayset.map(d => dayMap(cfg, a).get(d).served)]));
  const Y = Object.fromEntries(BOOT_Y.map(yk => [yk, Object.fromEntries(allArms.map(a => [a, dayset.map(d => YDEF[yk](dayMap(cfg, a).get(d)))]))]));
  // gap of the target's mean point to the comparator's curve of means, over the days idx (with repeats)
  const gapAt = (idx, cp, tArm, yk) => {
    const m = (arr) => { let s = 0; for (const i of idx) s += arr[i]; return s / idx.length; };
    const pts = FAM[cp.comp](cfg).map(a => ({ s: m(S[a]), y: m(Y[yk][a]) }));
    const fr = frontierAt(pts, m(S[tArm]));
    if (fr.v === null) return null;
    const yt = m(Y[yk][tArm]);
    return { gap: yt - fr.v, rel: fr.v !== 0 ? yt / fr.v - 1 : null, yt, yc: fr.v };
  };
  const ident = dayset.map((_, i) => i);
  const est = {}, boots = {};
  for (const cp of COMPS) for (const tArm of FAM[cp.target](cfg)) for (const yk of BOOT_Y) {
    const k = `${cp.id}|${tArm}|${yk}`;
    est[k] = gapAt(ident, cp, tArm, yk);
    boots[k] = { g: [], r: [], na: 0 };
  }
  for (let b = 0; b < NBOOT; b++) {
    const idx = new Array(D);
    for (let i = 0; i < D; i++) idx[i] = Math.floor(rnd() * D);
    for (const cp of COMPS) for (const tArm of FAM[cp.target](cfg)) for (const yk of BOOT_Y) {
      const k = `${cp.id}|${tArm}|${yk}`, g = gapAt(idx, cp, tArm, yk);
      if (g === null) boots[k].na++;
      else { boots[k].g.push(g.gap); if (g.rel !== null) boots[k].r.push(g.rel); }
    }
  }
  SUM.pooled[cfg] = { days: D, nBoot: NBOOT, seed: 'wf17-r6', points: {} };
  P(`\n==================== ${cfg}: SECONDARY pooled (30-day-mean curves), vertical gap at matched mean served, day bootstrap ${NBOOT} (seed wf17-r6), ${D} days ====================`);
  for (const cp of COMPS) for (const yk of BOOT_Y) {
    P(`-- ${cp.id} ${yk}:   point     gap [95% CI]              rel gap [95% CI]           NA share`);
    for (const tArm of FAM[cp.target](cfg)) {
      const k = `${cp.id}|${tArm}|${yk}`, e = est[k], bt = boots[k];
      const rec = { estimate: e, gapLo: pct(bt.g, 0.025), gapHi: pct(bt.g, 0.975), relLo: pct(bt.r, 0.025), relHi: pct(bt.r, 0.975),
        naShare: bt.na / NBOOT };
      SUM.pooled[cfg].points[k] = rec;
      P(`   ${tArm.padEnd(8)} ${e ? f(e.gap, 3) : 'NA (outside range)'} [${f(rec.gapLo, 3)}, ${f(rec.gapHi, 3)}]   ${e && e.rel !== null ? f(100 * e.rel, 1) + '%' : 'NA'} [${f(100 * rec.relLo, 1)}%, ${f(100 * rec.relHi, 1)}%]   ${f(100 * rec.naShare, 1)}%`);
    }
  }
}

// ---------------------------------------------------------------- equal-parameter contrasts and exchange rates (descriptive)
// metric -> [day value, better side, count?]
const EQM = { served: [o => o.served, 'higher', true], visRev60: [o => o.visRev60, 'lower', true],
  breachDeadline60: [o => o.breachDeadline60, 'lower', true], late60First: [o => o.late60First, 'lower', true] };
for (const cfg of ['INS', 'G2']) {
  SUM.equal[cfg] = {}; SUM.rates[cfg] = {};
  P(`\n==================== ${cfg}: equal-parameter paired contrasts (descriptive) ====================`);
  const pairs = [...ALPHAS.map(a => ['RCE' + a, 'EOHZ' + a]), ...XG(cfg).map(x => ['SHRC' + x, 'SHHZ' + x])];
  for (const [a, b] of pairs) {
    if (!dayMap(cfg, a).size || !dayMap(cfg, b).size) { LOG.push(`skip equal ${cfg} ${a}-${b}`); continue; }
    P(`${cfg} ${a} - ${b}`);
    const per = {};
    for (const [mk, [fn, better, cnt]] of Object.entries(EQM)) {
      const c = contrast(new Map([...dayMap(cfg, a)].map(([d, o]) => [d, fn(o)])), new Map([...dayMap(cfg, b)].map(([d, o]) => [d, fn(o)])),
        { better, count: cnt });
      per[mk] = c; SUM.equal[cfg][`${a}-${b}|${mk}`] = c;
      P(`   ${mk.padEnd(16)} ${fmtC(c)}`);
    }
    for (const yk of ['Pchg (pp)', 'TV5/served (s)', 'pDispAny1 (pp)']) {
      const c = contrast(yMap(cfg, a, yk), yMap(cfg, b, yk), { better: 'lower', count: false });
      SUM.equal[cfg][`${a}-${b}|${yk}`] = c;
      P(`   ${yk.padEnd(16)} mean ${f(c.mean, 3)} [${f(c.lo, 3)}, ${f(c.hi, 3)}] SE ${f(c.se, 3)} med ${f(c.median, 3)} | ${c.neg}-/${c.pos}+/${c.ties}= p ${fp(c.p)} | A ${f(c.meanA, 3)} B ${f(c.meanB, 3)}`);
    }
    // exchange rates at equal a: reversals avoided per 1000 served (ARR), and per rider of service, per deadline breach
    // and per late rider given up
    if (a.startsWith('RCE')) {
      const dv = per.visRev60.mean, ds = per.served.mean, db = per.breachDeadline60.mean, dl = per.late60First.mean;
      const r = { ARR: -dv / per.served.meanB * 1000, ER_served: ds < 0 ? dv / ds : 'no service price',
        ER_deadline: db > 1e-9 ? -dv / db : 'no deadline price', ER_late: dl > 1e-9 ? -dv / dl : 'no lateness price',
        dVisRev60: dv, dServed: ds, dBreachDeadline60: db, dLate60First: dl };
      SUM.rates[cfg][`${a}-${b}`] = r;
      P(`   rates (2.4): ARR ${f(r.ARR, 2)} reversals avoided /1000 served | ER_served ${typeof r.ER_served === 'number' ? f(r.ER_served, 2) : r.ER_served} | ER_deadline ${typeof r.ER_deadline === 'number' ? f(r.ER_deadline, 2) : r.ER_deadline} | ER_late ${typeof r.ER_late === 'number' ? f(r.ER_late, 2) : r.ER_late}`);
    }
  }
}

// ---------------------------------------------------------------- decision labels (the rules in the header)
const FR = (cfg, id, yk) => (SUM.frontier[cfg] && SUM.frontier[cfg][id] && SUM.frontier[cfg][id][yk]) || [];
const pos6 = pts => pts.filter(q => q.param > 0);   // the 6 points with a > 0
function revLabel(cfg) {
  const pts = pos6(FR(cfg, 'RCE_vs_EOHZ', 'visRev60 /1000'));
  const holds = pts.filter(q => q.evaluable && q.relGap !== null && q.relGap <= -0.20 && q.favour !== null && q.favour >= 0.75);
  const within = pts.filter(q => q.evaluable && q.relGap !== null && Math.abs(q.relGap) <= 0.10);
  const above = pts.filter(q => q.evaluable && q.mean > 0);
  const label = above.length >= 3 ? 'REVERSED' : within.length >= 3 ? 'WEAKENED' : holds.length >= 4 ? 'HOLDS' : 'INCONCLUSIVE';
  return { label, points: pts.length, evaluable: pts.filter(q => q.evaluable).length, holdPoints: holds.map(q => q.arm),
    within10Points: within.map(q => q.arm), abovePoints: above.map(q => q.arm),
    relGaps: Object.fromEntries(pts.map(q => [q.arm, q.relGap])), favour: Object.fromEntries(pts.map(q => [q.arm, q.favour])) };
}
function pchgLabel(cfg) {
  const pts = pos6(FR(cfg, 'RCE_vs_EOHZ', 'Pchg (pp)'));
  const worse = pts.filter(q => q.evaluable && q.mean > 2);
  const ev = pts.filter(q => q.evaluable).length;
  const label = worse.length >= 3 ? 'WEAKENED' : ev >= 4 ? 'HOLDS' : 'INCONCLUSIVE';
  return { label, evaluable: ev, worsePoints: worse.map(q => q.arm), meanDeltaPp: Object.fromEntries(pts.map(q => [q.arm, q.mean])) };
}
function lowerCount(cfg, id, yk) {
  const pts = FR(cfg, id, yk);
  const ev = pts.filter(q => q.evaluable);
  const lower = ev.filter(q => q.mean < 0 && q.favour !== null && q.favour >= 0.75), notLower = ev.filter(q => q.mean >= 0);
  const label = ev.length < 3 ? 'INCONCLUSIVE' : lower.length > ev.length / 2 ? 'LOWER' : notLower.length > ev.length / 2 ? 'NOT LOWER' : 'MIXED';
  return { label, evaluable: ev.length, lower: lower.map(q => q.arm), notLower: notLower.map(q => q.arm),
    meanDelta: Object.fromEntries(pts.map(q => [q.arm, q.mean])), relGap: Object.fromEntries(pts.map(q => [q.arm, q.relGap])) };
}
for (const cfg of ['INS', 'G2']) {
  SUM.labels[cfg] = { C9_reversals: revLabel(cfg), C9_Pchg: pchgLabel(cfg) };
  const el = lowerCount(cfg, 'SHHZ_vs_EOHZ', 'Pchg (pp)');
  SUM.labels[cfg].ElmiShadowBeatsOrdinary_Pchg = { ...el, label: el.label === 'LOWER' ? 'REPLICATED' : el.label === 'NOT LOWER' ? 'NOT REPLICATED' : el.label };
  SUM.labels[cfg].SHRC_vs_SHHZ_reversals = lowerCount(cfg, 'SHRC_vs_SHHZ', 'visRev60 /1000');
  SUM.labels[cfg].SHRC_vs_SHHZ_Pchg = lowerCount(cfg, 'SHRC_vs_SHHZ', 'Pchg (pp)');
  SUM.labels[cfg].RCE_vs_EOHZ_deadline = lowerCount(cfg, 'RCE_vs_EOHZ', 'breachDeadline60 /1000');
}
{
  const r = SUM.labels.G2.C9_reversals.label, p = SUM.labels.G2.C9_Pchg.label;
  SUM.labels.C9_overall_G2 = r === 'REVERSED' ? 'REVERSED' : (r === 'WEAKENED' || p === 'WEAKENED') ? 'WEAKENED' : (r === 'HOLDS' && p === 'HOLDS') ? 'HOLDS' : 'INCONCLUSIVE';
}

// ---------------------------------------------------------------- predictions of the study plan, checked point by point
{
  // cnt: the points with a > 0; cntAll: every point; both count only the evaluable ones
  const cnt = (cfg, id, yk, fn) => { const pts = pos6(FR(cfg, id, yk)).filter(q => q.evaluable); return { met: pts.filter(fn).length, of: pts.length }; };
  const cntAll = (cfg, id, yk, fn) => { const pts = FR(cfg, id, yk).filter(q => q.evaluable); return { met: pts.filter(fn).length, of: pts.length }; };
  SUM.predictions = {
    'G2 RCE vs EOHZ visRev60: 30-55 % lower (alpha >= 10)': cnt('G2', 'RCE_vs_EOHZ', 'visRev60 /1000', q => q.relGap <= -0.30 && q.relGap >= -0.55),
    'G2 RCE vs EOHZ breachDeadline60: higher, +3 to +10 per 1000 (+0.3 to +1 pp)': cnt('G2', 'RCE_vs_EOHZ', 'breachDeadline60 /1000', q => q.mean >= 3 && q.mean <= 10),
    'G2 RCE vs EOHZ breachDeadline60: higher (any size)': cnt('G2', 'RCE_vs_EOHZ', 'breachDeadline60 /1000', q => q.mean > 0),
    'G2 RCE vs EOHZ Pchg: within +-1.5 pp': cnt('G2', 'RCE_vs_EOHZ', 'Pchg (pp)', q => Math.abs(q.mean) <= 1.5),
    'INS RCE vs EOHZ reversals: 0 to -25 %': cnt('INS', 'RCE_vs_EOHZ', 'visRev60 /1000', q => q.relGap <= 0 && q.relGap >= -0.25),
    'INS RCE vs EOHZ Pchg: within +-1 pp': cnt('INS', 'RCE_vs_EOHZ', 'Pchg (pp)', q => Math.abs(q.mean) <= 1),
    'INS RCE vs EOHZ deadline breach: higher': cnt('INS', 'RCE_vs_EOHZ', 'breachDeadline60 /1000', q => q.mean > 0),
    'INS SHHZ vs EOHZ Pchg: lower': cntAll('INS', 'SHHZ_vs_EOHZ', 'Pchg (pp)', q => q.mean < 0),
    'G2 SHHZ vs EOHZ Pchg: lower': cntAll('G2', 'SHHZ_vs_EOHZ', 'Pchg (pp)', q => q.mean < 0),
    'INS SHRC vs SHHZ reversals: lower': cntAll('INS', 'SHRC_vs_SHHZ', 'visRev60 /1000', q => q.mean < 0),
    'G2 SHRC vs SHHZ reversals: lower': cntAll('G2', 'SHRC_vs_SHHZ', 'visRev60 /1000', q => q.mean < 0),
    'INS SHRC vs SHHZ Pchg: lower': cntAll('INS', 'SHRC_vs_SHHZ', 'Pchg (pp)', q => q.mean < 0),
    'G2 SHRC vs SHHZ Pchg: lower': cntAll('G2', 'SHRC_vs_SHHZ', 'Pchg (pp)', q => q.mean < 0),
  };
  for (const cfg of ['INS', 'G2']) {
    const eq = XG(cfg).map(x => { const c = SUM.equal[cfg] && SUM.equal[cfg][`SHRC${x}-SHHZ${x}|served`]; return c ? c.mean : null; });
    SUM.predictions[`${cfg} at equal x SHRC serves less than SHHZ (wf15 S10 G2 -9.5/day at x*)`] = { met: eq.filter(v => v !== null && v < 0).length,
      of: eq.filter(v => v !== null).length, means: Object.fromEntries(XG(cfg).map((x, i) => ['x' + x, eq[i]])) };
  }
}

// ---------------------------------------------------------------- gates: the arms that repeat earlier runs
{
  const IGN = new Set(['part', 'set', 'engine', 'ms', 'msScore', 'label', 'variant', 'mode', 'alpha', 'check', 'nReq']);
  const A5 = readNd(arg('a5'));
  const W14 = readNd(arg('wf14')).filter(r => r.part === 'M' && r.mode === 'N' && r.rs === 'r0');
  const W15 = readNd(arg('wf15'));
  const idx = (arr, armOf) => { const m = new Map(); for (const r of arr) m.set([r.cell, r.config, armOf(r)].join('|'), r); return m; };
  const VA = { V7_a10: 10, V8_a20: 20, V9_a50: 50, V10_a100: 100 };   // a5 variant -> a
  const a5i = idx(A5.filter(r => VA[r.variant] !== undefined && (r.arm === 'EOHZ' || r.arm === 'RCE')), r => r.arm + VA[r.variant]);
  const w14i = idx(W14, r => r.arm), w15i = idx(W15, r => r.arm);
  const cmp = (name, mine, refIdx, nreq) => {
    let n = 0, eq = 0, miss = 0;
    const bad = [];
    for (const m of mine) {
      const r = refIdx.get([m.cell, m.config, m.arm].join('|'));
      n++;
      if (!r) { miss++; continue; }
      const diff = Object.keys(r).filter(k => !IGN.has(k) && JSON.stringify(r[k]) !== JSON.stringify(m[k]));
      if (nreq && r.nReq !== undefined && r.nReq !== m.requests) diff.push('nReq!=requests');
      if (diff.length) bad.push([m.cell, m.config, m.arm, diff.slice(0, 5)]); else eq++;
    }
    SUM.gates[name] = { compared: n, equal: eq, missingRef: miss, pass: n > 0 && eq === n, bad: bad.slice(0, 5) };
    P(`gate ${name}: ${eq}/${n} equal, missing ref ${miss}${bad.length ? ' MISMATCH ' + JSON.stringify(bad.slice(0, 3)) : ''}`);
  };
  P('\n==================== Post-run gates ====================');
  const isA = (r, list) => list.includes(r.arm);
  cmp('EOHZ/RCE alpha 10,20,50,100 = a5 V7-V10', rows.filter(r => isA(r, ['EOHZ10', 'EOHZ20', 'EOHZ50', 'EOHZ100', 'RCE10', 'RCE20', 'RCE50', 'RCE100'])), a5i, true);
  cmp('EOHZ35/RCE35/U = wf14 main r0', rows.filter(r => isA(r, ['EOHZ35', 'RCE35', 'U'])), w14i, false);
  cmp('SHHZ/SHRC x*, U, EOHZ35, RCE35 = wf15 main', rows.filter(r => isA(r, ['U', 'EOHZ35', 'RCE35']) ||
    (r.config === 'INS' && isA(r, ['SHHZ72', 'SHRC72'])) || (r.config === 'G2' && isA(r, ['SHHZ71', 'SHRC71']))), w15i, false);
  const uk = new Set(rows.map(r => [r.cell, r.config, r.arm].join('|')));
  SUM.gates.rowCount = { rows: rows.length, unique: uk.size, expected: 3240, pass: rows.length === 3240 && uk.size === 3240 };
  P(`gate rowCount: ${rows.length} rows, ${uk.size} unique (cell, config, arm), expected 3240`);
}

// ---------------------------------------------------------------- label summary, predictions, log
P('\n==================== Decision labels (pre-stated rules in the header) ====================');
for (const cfg of ['INS', 'G2']) {
  const lb = SUM.labels[cfg];
  P(`${cfg} C9 reversals (RCE vs EOHZ at matched service${cfg === 'INS' ? ', SECONDARY' : ', PRIMARY'}): ${lb.C9_reversals.label} | rel gaps ${JSON.stringify(Object.fromEntries(Object.entries(lb.C9_reversals.relGaps).map(([k, v]) => [k, v === null ? null : +(100 * v).toFixed(1)])))} | in favour ${JSON.stringify(Object.fromEntries(Object.entries(lb.C9_reversals.favour).map(([k, v]) => [k, v === null ? null : +(100 * v).toFixed(0)])))} | hold ${lb.C9_reversals.holdPoints.length}, within10 ${lb.C9_reversals.within10Points.length}, above ${lb.C9_reversals.abovePoints.length}, evaluable ${lb.C9_reversals.evaluable}/6`);
  P(`${cfg} C9 P(change) at matched service: ${lb.C9_Pchg.label} | mean Delta pp ${JSON.stringify(Object.fromEntries(Object.entries(lb.C9_Pchg.meanDeltaPp).map(([k, v]) => [k, Number.isFinite(v) ? +v.toFixed(2) : null])))} | worse > 2 pp: ${lb.C9_Pchg.worsePoints.length}`);
  P(`${cfg} Elmi Shadow (SHHZ) vs Ordinary (EOHZ) on Pchg: ${lb.ElmiShadowBeatsOrdinary_Pchg.label} (lower ${lb.ElmiShadowBeatsOrdinary_Pchg.lower.length}, not lower ${lb.ElmiShadowBeatsOrdinary_Pchg.notLower.length}, evaluable ${lb.ElmiShadowBeatsOrdinary_Pchg.evaluable})`);
  P(`${cfg} SHRC vs SHHZ reversals: ${lb.SHRC_vs_SHHZ_reversals.label} (lower ${lb.SHRC_vs_SHHZ_reversals.lower.length}/${lb.SHRC_vs_SHHZ_reversals.evaluable}); Pchg: ${lb.SHRC_vs_SHHZ_Pchg.label} (lower ${lb.SHRC_vs_SHHZ_Pchg.lower.length}/${lb.SHRC_vs_SHHZ_Pchg.evaluable})`);
  P(`${cfg} RCE vs EOHZ deadline breach at matched service (LOWER = Rule C better on Elmi's yardstick): ${lb.RCE_vs_EOHZ_deadline.label} (lower ${lb.RCE_vs_EOHZ_deadline.lower.length}, not lower ${lb.RCE_vs_EOHZ_deadline.notLower.length}, evaluable ${lb.RCE_vs_EOHZ_deadline.evaluable})`);
}
P(`C9 overall (G2): ${SUM.labels.C9_overall_G2}`);
P('\n==================== Predictions (RUNPLAN R6), points meeting the prediction / evaluable points ====================');
for (const [k, v] of Object.entries(SUM.predictions)) P(`${k}: ${v.met}/${v.of}${v.means ? ' ' + JSON.stringify(Object.fromEntries(Object.entries(v.means).map(([a, b]) => [a, b === null ? null : +b.toFixed(2)]))) : ''}`);
P('\n==================== Log ====================');
for (const l of [...new Set(LOG)]) P(l);
P(`rows ${rows.length}`);

// ---------------------------------------------------------------- write
const sha256 = crypto.createHash('sha256').update(fs.readFileSync(__filename)).digest('hex');
fs.writeFileSync(arg('out') + '.txt', `wf17 R6 (wf17-exploratory-post-audit) - policy curves. Generated ${new Date().toISOString()} by policy_curves.js (sha256 ${sha256})\n` + LINES.join('\n') + '\n');
fs.writeFileSync(arg('out') + '.json', JSON.stringify(SUM, (k, v) => (k === 'days' || k === 'deltas' ? undefined : v), 1));
console.log(LINES.filter(l => /C9|Elmi|SHRC vs SHHZ|deadline breach at|^gate /.test(l)).join('\n'));
