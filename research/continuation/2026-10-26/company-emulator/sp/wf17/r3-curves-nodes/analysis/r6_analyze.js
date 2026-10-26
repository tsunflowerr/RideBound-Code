// wf17 R6 analysis (EXPLORATORY, POST-AUDIT; RUNPLAN R6): policy curves over alpha (EOHZ / RCE) and Shadow x (SHHZ / SHRC) on
// D30 x {w08, w17} x r0, INS and G2. Written and sha256-hashed BEFORE the R6 production run (ANALYSIS-HASH.txt).
// Input: results/r6.ndjson (variant 'r6'). Unit = day = w08 + w17 (RUNPLAN 2.1). Share metrics are per-day shares of that day's served.
//
// Y metrics (per day): Pchg = pDisp/served (pp, PRIMARY curve metric); pDispAny1/served (pp); visRev60 per 1000 served;
// sumTV5/served (s); breachDeadline60 per 1000 served (each arm against ITS OWN budget: the yardstick moves with alpha / x);
// late60First per 1000 served; breachLedger per 1000 served (BY-CONSTRUCTION for RC arms; reported, never a result);
// G2 only: relocations and switchedRiders per 1000 served.
//
// Frontier comparison, PRIMARY, per day (RUNPLAN R6): comparator points (EOHZ over alpha, or SHHZ over x) sorted by served, points
// with equal served averaged, Y interpolated linearly; for each target point Delta_d = Y_target - Y_comp(served_target). Days whose
// target served lies outside that day's comparator served range are excluded and counted (below / above). Per target point: sign
// test (ties |Delta| <= 1e-9 dropped), mean +- SE, 95 % t-interval, median, relative gap = sum Y_target / sum Y_comp(interp) - 1 over
// the included days, days in favour = share of NON-TIED included days with Delta < 0 (lower Y is better for every Y here).
// Comparisons: RCE(alpha) vs EOHZ curve; SHRC(x) vs SHHZ curve; SHHZ(x) vs EOHZ curve (Elmi's own claim).
// SECONDARY, pooled: comparator curve of the 30-day means (mean served, mean Y per arm), vertical gap at the target's mean served,
// day-bootstrap 95 % percentile CI (10,000 resamples of the days with replacement, mulberry32 seeded by FNV-1a('wf17-r6'); one index
// draw per resample shared by every comparison/metric of that config; a resample whose target falls outside the comparator range
// is NA and the NA share is printed). Bootstrap days = days with both windows for EVERY arm of that config.
//
// Decision rules (pre-stated; RUNPLAN R6 flip criteria, made operational here):
//   A target point is EVALUABLE if >= 10 of the 30 days are inside the comparator's served range.
//   C9 reversals (Y = visRev60 per 1000 served, RCE vs EOHZ, the 6 points alpha in {10,20,35,50,75,100}):
//     point HOLDS if evaluable, relative gap <= -20 % and days in favour >= 75 %;
//     point WITHIN10 if evaluable and |relative gap| <= 10 %; point ABOVE if evaluable and mean Delta > 0.
//     Label: REVERSED if >= 3 points ABOVE; else WEAKENED if >= 3 points WITHIN10; else HOLDS if >= 4 points HOLD; else INCONCLUSIVE.
//     G2 is the C9 test (RUNPLAN "C9 (G2 reversals)"); INS gets the same rule as a SECONDARY label (C3 predicts INS fails it).
//   C9 P(change) ("Rule C costs nothing in Elmi's P(change)"), per config, same 6 points, Y = Pchg (pp):
//     point WORSE if evaluable and mean Delta > +2 pp. WEAKENED if >= 3 points WORSE; else HOLDS if >= 4 points evaluable; else INCONCLUSIVE.
//   C9 overall (G2): REVERSED if the G2 reversal label is REVERSED; else WEAKENED if the G2 reversal or G2 Pchg label is WEAKENED;
//     else HOLDS if both HOLD; else INCONCLUSIVE.
//   Elmi replication (SHHZ vs EOHZ, Y = Pchg), per config, over the 6 SHHZ points: point LOWER if evaluable, mean Delta < 0 and days
//     in favour >= 75 %; point NOT-LOWER if evaluable and mean Delta >= 0. REPLICATED if LOWER points > half of the evaluable points;
//     NOT REPLICATED if NOT-LOWER points > half; else MIXED; INCONCLUSIVE if < 3 points evaluable. Not a Rule C result.
//   SHRC vs SHHZ (reversals, Pchg) and RCE vs EOHZ deadline breach: same LOWER / NOT-LOWER counting, descriptive (prediction check).
// Descriptive extras (no label): equal-parameter paired contrasts RCE(alpha) - EOHZ(alpha) and SHRC(x) - SHHZ(x); RUNPLAN 2.4 exchange
// rates at each equal alpha (ARR, ER_served, ER_deadline, ER_late); predictions table.
// Post-run gates (RUNPLAN R6): EOHZ/RCE alpha in {10,20,50,100} = a5 V7-V10 rows (every reference field except labels/timing and
// the a5 driver field nReq, which must equal 'requests'); EOHZ35/RCE35/U = wf14 main r0 rows; SHHZ/SHRC at x* and U/EOHZ35/RCE35 =
// wf15 main rows. Row count 60 x 2 x 27 = 3240, no duplicate (cell, config, arm).
'use strict';
const fs = require('fs'), path = require('path');
const L = require('./lib17.js');
const KEY = path.resolve(__dirname, '..');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const file = process.argv[2] || path.join(KEY, 'results/r6.ndjson');
const outTxt = process.argv[3] || path.join(KEY, 'results/r6_tables.txt');
const outJson = outTxt.replace(/\.txt$/, '.json');
const NBOOT = Number(process.env.R6_NBOOT || 10000);
const SKIP_GATES = process.env.R6_SKIP_GATES === '1';   // synthetic smoke test only
const XS = { INS: 72, G2: 71 };
const ALPHAS = [0, 10, 20, 35, 50, 75, 100];
const XG = cfg => [50, 60, 70, XS[cfg], 80, 90].sort((a, b) => a - b);
const FAM = { EOHZ: cfg => ALPHAS.map(a => 'EOHZ' + a), RCE: cfg => ALPHAS.map(a => 'RCE' + a), SHHZ: cfg => XG(cfg).map(x => 'SHHZ' + x), SHRC: cfg => XG(cfg).map(x => 'SHRC' + x) };
const parOf = arm => Number(/\d+$/.exec(arm)[0]);
const LINES = [], LOG = [];
const P = s => LINES.push(s);

const rows = L.readNd(file).filter(r => r.label === 'wf17-exploratory-post-audit' && r.variant === 'r6');
const SUM = { label: 'wf17-exploratory-post-audit', check: 'R6', nRows: rows.length, curves: {}, frontier: {}, pooled: {}, equal: {}, rates: {}, labels: {}, predictions: {}, gates: {}, log: LOG };

// ------------------------------------------------------------------ aggregation: config|arm -> Map(day -> summed counts)
const CNT = ['served', 'pDisp', 'pDispAny1', 'visRev60', 'sumTV5', 'breachDeadline60', 'late60First', 'breachLedger', 'relocations', 'switchedRiders'];
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
    const dm = tmp.get(k); if (!dm.has(r.day)) dm.set(r.day, {}); dm.get(r.day)[r.win] = r;
  }
  for (const [k, dm] of tmp) {
    const out = new Map();
    for (const [d, w] of dm) {
      if (!(w.w08 && w.w17)) { LOG.push(`excluded ${k} ${d}: missing window`); continue; }
      const o = {}; for (const c of CNT) o[c] = (w.w08[c] || 0) + (w.w17[c] || 0);
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

// ------------------------------------------------------------------ frontier interpolation
function frontierAt(points, s) {
  const g = new Map();
  for (const p of points) { if (!Number.isFinite(p.s) || !Number.isFinite(p.y)) continue; if (!g.has(p.s)) g.set(p.s, []); g.get(p.s).push(p.y); }
  const q = [...g].map(([sv, ys]) => ({ s: sv, y: L.mean(ys) })).sort((a, b) => a.s - b.s);
  if (!q.length) return { v: null, why: 'empty' };
  if (s < q[0].s) return { v: null, why: 'below' };
  if (s > q[q.length - 1].s) return { v: null, why: 'above' };
  for (let i = 0; i < q.length; i++) {
    if (q[i].s === s) return { v: q[i].y };
    if (i + 1 < q.length && s > q[i].s && s < q[i + 1].s) { const t = (s - q[i].s) / (q[i + 1].s - q[i].s); return { v: q[i].y + t * (q[i + 1].y - q[i].y) }; }
  }
  return { v: null, why: 'nan' };
}

// ------------------------------------------------------------------ curves (30-day means)
P('wf17 R6 policy curves (EXPLORATORY, POST-AUDIT). Unit = day (w08 + w17). Means over days; per-day shares of served.');
for (const cfg of ['INS', 'G2']) {
  P(`\n==================== ${cfg}: curves (mean over days; served/day and per-day Y) ====================`);
  const yk = YKEYS(cfg);
  P(['arm'.padEnd(8), 'days'.padStart(4), 'served'.padStart(8), 'SE'.padStart(5), ...yk.map(k => k.replace(' [BY-CONSTRUCTION for RC]', '*').padStart(12))].join(' '));
  const C = [];
  for (const fam of ['U', 'EOHZ', 'RCE', 'SHHZ', 'SHRC']) {
    const arms = fam === 'U' ? ['U'] : FAM[fam](cfg);
    for (const arm of arms) {
      const sm = sMap(cfg, arm); if (!sm.size) { LOG.push(`no rows ${cfg} ${arm}`); continue; }
      const sv = [...sm.values()];
      const rec = { config: cfg, family: fam, param: fam === 'U' ? null : parOf(arm), arm, nDays: sv.length, served: L.mean(sv), servedSE: L.sd(sv) / Math.sqrt(sv.length), Y: {} };
      for (const k of yk) { const v = [...yMap(cfg, arm, k).values()]; rec.Y[k] = L.mean(v); rec.Y[k + ' SE'] = L.sd(v) / Math.sqrt(v.length); }
      C.push(rec);
      P([arm.padEnd(8), String(rec.nDays).padStart(4), L.f(rec.served, 1).padStart(8), L.f(rec.servedSE, 1).padStart(5), ...yk.map(k => L.f(rec.Y[k], 2).padStart(12))].join(' '));
    }
  }
  SUM.curves[cfg] = C;
}
P('(* breachLedger is BY-CONSTRUCTION 0 for RC arms; breachDeadline60 is measured against each arm\'s own budget; U has no budget, so its breach fields are 0 by definition.)');

// ------------------------------------------------------------------ primary frontier comparisons (per day)
const COMPS = [
  { id: 'RCE_vs_EOHZ', target: 'RCE', comp: 'EOHZ' },
  { id: 'SHRC_vs_SHHZ', target: 'SHRC', comp: 'SHHZ' },
  { id: 'SHHZ_vs_EOHZ', target: 'SHHZ', comp: 'EOHZ' },
];
function frontierPoint(cfg, tArm, compArms, yk) {
  const tS = sMap(cfg, tArm), tY = yMap(cfg, tArm, yk);
  const cS = compArms.map(a => sMap(cfg, a)), cY = compArms.map(a => yMap(cfg, a, yk));
  const a = new Map(), b = new Map(); let below = 0, above = 0, missing = 0;
  for (const d of [...tS.keys()].sort()) {
    if (!compArms.every((_, i) => cS[i].has(d))) { missing++; continue; }
    const pts = compArms.map((_, i) => ({ s: cS[i].get(d), y: cY[i].get(d) }));
    const fr = frontierAt(pts, tS.get(d));
    if (fr.v === null) { if (fr.why === 'below') below++; else if (fr.why === 'above') above++; else missing++; continue; }
    a.set(d, tY.get(d)); b.set(d, fr.v);
  }
  const c = L.contrast(a, b, { better: 'lower', count: true });
  const relGap = c.nDays && c.meanB !== 0 ? c.meanA / c.meanB - 1 : null;
  return { arm: tArm, param: parOf(tArm), nIncl: c.nDays, below, above, missing, evaluable: c.nDays >= 10, mean: c.mean, se: c.se, lo: c.lo, hi: c.hi, median: c.median,
    neg: c.neg, pos: c.pos, ties: c.ties, p: c.p, favour: c.favourShareNonTied, relGap, meanTarget: c.meanA, meanCompInterp: c.meanB, deltas: c.deltas };
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
      for (const q of pts) P(`   ${q.arm.padEnd(8)} ${String(q.nIncl).padStart(4)} ${String(q.below).padStart(5)} ${String(q.above).padStart(5)} | ${L.f(q.mean, 3).padStart(8)} [${L.f(q.lo, 3)}, ${L.f(q.hi, 3)}] ${L.f(q.se, 3).padStart(7)} ${L.f(q.median, 3).padStart(8)} | ${q.neg}-/${q.pos}+/${q.ties}= p ${L.fp(q.p)} | ${q.favour === null ? 'NA' : L.f(100 * q.favour, 0) + '%'} | ${q.relGap === null ? 'NA' : L.f(100 * q.relGap, 1) + '%'} | ${L.f(q.meanTarget, 3)} ${L.f(q.meanCompInterp, 3)}${q.evaluable ? '' : '  [not evaluable: < 10 days in range]'}`);
    }
  }
}

// ------------------------------------------------------------------ secondary: pooled curves + day bootstrap
function fnv1a(s) { let h = 0x811c9dc5; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 0x01000193) >>> 0; } return h >>> 0; }
function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const rnd = mulberry32(fnv1a('wf17-r6'));
const BOOT_Y = ['Pchg (pp)', 'visRev60 /1000', 'breachDeadline60 /1000', 'TV5/served (s)', 'pDispAny1 (pp)'];
const pct = (a, q) => { if (!a.length) return NaN; const s = a.slice().sort((x, y) => x - y); const i = (s.length - 1) * q, lo = Math.floor(i), hi = Math.ceil(i); return s[lo] + (s[hi] - s[lo]) * (i - lo); };
for (const cfg of ['INS', 'G2']) {
  const allArms = ['U', ...FAM.EOHZ(cfg), ...FAM.RCE(cfg), ...FAM.SHHZ(cfg), ...FAM.SHRC(cfg)];
  const dayset = [...new Set([...dayMap(cfg, 'U').keys()])].filter(d => allArms.every(a => dayMap(cfg, a).has(d))).sort();
  const dropped = [...new Set(allArms.flatMap(a => [...dayMap(cfg, a).keys()]))].filter(d => !dayset.includes(d));
  if (dropped.length) LOG.push(`${cfg} bootstrap: days dropped (some arm missing): ${dropped.join(',')}`);
  const D = dayset.length;
  // arrays [arm][day] of served and Y
  const S = Object.fromEntries(allArms.map(a => [a, dayset.map(d => dayMap(cfg, a).get(d).served)]));
  const Y = Object.fromEntries(BOOT_Y.map(yk => [yk, Object.fromEntries(allArms.map(a => [a, dayset.map(d => YDEF[yk](dayMap(cfg, a).get(d)))]))]));
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
  for (const cp of COMPS) for (const tArm of FAM[cp.target](cfg)) for (const yk of BOOT_Y) { const k = `${cp.id}|${tArm}|${yk}`; est[k] = gapAt(ident, cp, tArm, yk); boots[k] = { g: [], r: [], na: 0 }; }
  for (let b = 0; b < NBOOT; b++) {
    const idx = new Array(D); for (let i = 0; i < D; i++) idx[i] = Math.floor(rnd() * D);
    for (const cp of COMPS) for (const tArm of FAM[cp.target](cfg)) for (const yk of BOOT_Y) {
      const k = `${cp.id}|${tArm}|${yk}`, g = gapAt(idx, cp, tArm, yk);
      if (g === null) boots[k].na++; else { boots[k].g.push(g.gap); if (g.rel !== null) boots[k].r.push(g.rel); }
    }
  }
  SUM.pooled[cfg] = { days: D, nBoot: NBOOT, seed: 'wf17-r6', points: {} };
  P(`\n==================== ${cfg}: SECONDARY pooled (30-day-mean curves), vertical gap at matched mean served, day bootstrap ${NBOOT} (seed wf17-r6), ${D} days ====================`);
  for (const cp of COMPS) for (const yk of BOOT_Y) {
    P(`-- ${cp.id} ${yk}:   point     gap [95% CI]              rel gap [95% CI]           NA share`);
    for (const tArm of FAM[cp.target](cfg)) {
      const k = `${cp.id}|${tArm}|${yk}`, e = est[k], bt = boots[k];
      const rec = { estimate: e, gapLo: pct(bt.g, 0.025), gapHi: pct(bt.g, 0.975), relLo: pct(bt.r, 0.025), relHi: pct(bt.r, 0.975), naShare: bt.na / NBOOT };
      SUM.pooled[cfg].points[k] = rec;
      P(`   ${tArm.padEnd(8)} ${e ? L.f(e.gap, 3) : 'NA (outside range)'} [${L.f(rec.gapLo, 3)}, ${L.f(rec.gapHi, 3)}]   ${e && e.rel !== null ? L.f(100 * e.rel, 1) + '%' : 'NA'} [${L.f(100 * rec.relLo, 1)}%, ${L.f(100 * rec.relHi, 1)}%]   ${L.f(100 * rec.naShare, 1)}%`);
    }
  }
}

// ------------------------------------------------------------------ equal-parameter contrasts and exchange rates (descriptive)
const EQM = { served: [o => o.served, 'higher', true], visRev60: [o => o.visRev60, 'lower', true], breachDeadline60: [o => o.breachDeadline60, 'lower', true], late60First: [o => o.late60First, 'lower', true] };
for (const cfg of ['INS', 'G2']) {
  SUM.equal[cfg] = {}; SUM.rates[cfg] = {};
  P(`\n==================== ${cfg}: equal-parameter paired contrasts (descriptive) ====================`);
  const pairs = [...ALPHAS.map(a => ['RCE' + a, 'EOHZ' + a]), ...XG(cfg).map(x => ['SHRC' + x, 'SHHZ' + x])];
  for (const [a, b] of pairs) {
    if (!dayMap(cfg, a).size || !dayMap(cfg, b).size) { LOG.push(`skip equal ${cfg} ${a}-${b}`); continue; }
    P(`${cfg} ${a} - ${b}`);
    const per = {};
    for (const [mk, [fn, better, cnt]] of Object.entries(EQM)) {
      const c = L.contrast(new Map([...dayMap(cfg, a)].map(([d, o]) => [d, fn(o)])), new Map([...dayMap(cfg, b)].map(([d, o]) => [d, fn(o)])), { better, count: cnt });
      per[mk] = c; SUM.equal[cfg][`${a}-${b}|${mk}`] = c;
      P(`   ${mk.padEnd(16)} ${L.fmtC(c)}`);
    }
    for (const yk of ['Pchg (pp)', 'TV5/served (s)', 'pDispAny1 (pp)']) {
      const c = L.contrast(yMap(cfg, a, yk), yMap(cfg, b, yk), { better: 'lower', count: false });
      SUM.equal[cfg][`${a}-${b}|${yk}`] = c;
      P(`   ${yk.padEnd(16)} mean ${L.f(c.mean, 3)} [${L.f(c.lo, 3)}, ${L.f(c.hi, 3)}] SE ${L.f(c.se, 3)} med ${L.f(c.median, 3)} | ${c.neg}-/${c.pos}+/${c.ties}= p ${L.fp(c.p)} | A ${L.f(c.meanA, 3)} B ${L.f(c.meanB, 3)}`);
    }
    if (a.startsWith('RCE')) {
      const dv = per.visRev60.mean, ds = per.served.mean, db = per.breachDeadline60.mean, dl = per.late60First.mean;
      const r = { ARR: -dv / per.served.meanB * 1000, ER_served: ds < 0 ? dv / ds : 'no service price', ER_deadline: db > 1e-9 ? -dv / db : 'no deadline price', ER_late: dl > 1e-9 ? -dv / dl : 'no lateness price', dVisRev60: dv, dServed: ds, dBreachDeadline60: db, dLate60First: dl };
      SUM.rates[cfg][`${a}-${b}`] = r;
      P(`   rates (2.4): ARR ${L.f(r.ARR, 2)} reversals avoided /1000 served | ER_served ${typeof r.ER_served === 'number' ? L.f(r.ER_served, 2) : r.ER_served} | ER_deadline ${typeof r.ER_deadline === 'number' ? L.f(r.ER_deadline, 2) : r.ER_deadline} | ER_late ${typeof r.ER_late === 'number' ? L.f(r.ER_late, 2) : r.ER_late}`);
    }
  }
}

// ------------------------------------------------------------------ labels
const FR = (cfg, id, yk) => (SUM.frontier[cfg] && SUM.frontier[cfg][id] && SUM.frontier[cfg][id][yk]) || [];
const pos6 = pts => pts.filter(q => q.param > 0);
function revLabel(cfg) {
  const pts = pos6(FR(cfg, 'RCE_vs_EOHZ', 'visRev60 /1000'));
  const holds = pts.filter(q => q.evaluable && q.relGap !== null && q.relGap <= -0.20 && q.favour !== null && q.favour >= 0.75);
  const within = pts.filter(q => q.evaluable && q.relGap !== null && Math.abs(q.relGap) <= 0.10);
  const above = pts.filter(q => q.evaluable && q.mean > 0);
  const label = above.length >= 3 ? 'REVERSED' : within.length >= 3 ? 'WEAKENED' : holds.length >= 4 ? 'HOLDS' : 'INCONCLUSIVE';
  return { label, points: pts.length, evaluable: pts.filter(q => q.evaluable).length, holdPoints: holds.map(q => q.arm), within10Points: within.map(q => q.arm), abovePoints: above.map(q => q.arm),
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
  return { label, evaluable: ev.length, lower: lower.map(q => q.arm), notLower: notLower.map(q => q.arm), meanDelta: Object.fromEntries(pts.map(q => [q.arm, q.mean])), relGap: Object.fromEntries(pts.map(q => [q.arm, q.relGap])) };
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

// ------------------------------------------------------------------ predictions (RUNPLAN R6 table), checked point by point (alpha > 0)
{
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
    SUM.predictions[`${cfg} at equal x SHRC serves less than SHHZ (wf15 S10 G2 -9.5/day at x*)`] = { met: eq.filter(v => v !== null && v < 0).length, of: eq.filter(v => v !== null).length, means: Object.fromEntries(XG(cfg).map((x, i) => ['x' + x, eq[i]])) };
  }
}

// ------------------------------------------------------------------ gates
if (!SKIP_GATES) {
  const IGN = new Set(['part', 'set', 'engine', 'ms', 'msScore', 'label', 'variant', 'mode', 'alpha', 'check', 'nReq']);
  const A5 = L.readNd(path.join(SP, 'wf16/a5-steering/results/robust.ndjson'));
  const W14 = L.readNd(path.join(SP, 'wf14/results/main.ndjson')).filter(r => r.part === 'M' && r.mode === 'N' && r.rs === 'r0');
  const W15 = L.readNd(path.join(SP, 'wf15/results/confirm/main.ndjson'));
  const idx = (arr, armOf) => { const m = new Map(); for (const r of arr) m.set([r.cell, r.config, armOf(r)].join('|'), r); return m; };
  const VA = { V7_a10: 10, V8_a20: 20, V9_a50: 50, V10_a100: 100 };
  const a5i = idx(A5.filter(r => VA[r.variant] !== undefined && (r.arm === 'EOHZ' || r.arm === 'RCE')), r => r.arm + VA[r.variant]);
  const w14i = idx(W14, r => r.arm), w15i = idx(W15, r => r.arm);
  const cmp = (name, mine, refIdx, nreq) => {
    let n = 0, eq = 0, miss = 0; const bad = [];
    for (const m of mine) {
      const r = refIdx.get([m.cell, m.config, m.arm].join('|')); n++;
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
  cmp('SHHZ/SHRC x*, U, EOHZ35, RCE35 = wf15 main', rows.filter(r => isA(r, ['U', 'EOHZ35', 'RCE35']) || (r.config === 'INS' && isA(r, ['SHHZ72', 'SHRC72'])) || (r.config === 'G2' && isA(r, ['SHHZ71', 'SHRC71']))), w15i, false);
  const uk = new Set(rows.map(r => [r.cell, r.config, r.arm].join('|')));
  SUM.gates.rowCount = { rows: rows.length, unique: uk.size, expected: 3240, pass: rows.length === 3240 && uk.size === 3240 };
  P(`gate rowCount: ${rows.length} rows, ${uk.size} unique (cell, config, arm), expected 3240`);
}

// ------------------------------------------------------------------ label summary
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
fs.writeFileSync(outTxt, `wf17 R6 (wf17-exploratory-post-audit) - policy curves. Generated ${new Date().toISOString()} by analysis/r6_analyze.js (sha256 ${L.sha256File(__filename)})\n` + LINES.join('\n') + '\n');
fs.writeFileSync(outJson, JSON.stringify(SUM, (k, v) => (k === 'days' || k === 'deltas' ? undefined : v), 1));
console.log(LINES.filter(l => /C9|Elmi|SHRC vs SHHZ|deadline breach at|^gate /.test(l)).join('\n'));
