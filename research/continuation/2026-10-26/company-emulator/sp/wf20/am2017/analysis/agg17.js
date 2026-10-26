// wf17 r2-penalty-regime analysis helpers (EXPLORATORY, POST-AUDIT; RUNPLAN 2.1-2.4), used by r4_analyze.js and r5_analyze.js.
// Unit = day (r0 only): X_d = X_{w08,r0} + X_{w17,r0}. A day enters an arm's map only when both windows are present; a day missing any
// arm of a contrast is excluded from that contrast, and every exclusion is logged. Share metrics are per-day shares of that day's served
// riders (sum of counts over the two windows / sum of served), differences in pp. sumTV5 is reported as seconds per served rider.
'use strict';
const L = require('./lib17.js');

const COUNT_FIELDS = ['served', 'visRev60', 'sumTV5', 'pDisp', 'pDispAny1', 'late60First', 'breachDeadline60', 'breachLedger', 'relocations', 'switchedRiders', 'requests'];
// RUNPLAN 2.2 fixed metric list. kind: count (per day), share (pp of served), tv (s per served rider). better = benefit side for Rule C.
const METRICS = [
  { id: 'served', kind: 'count', field: 'served', better: 'higher' },
  { id: 'visRev60', kind: 'count', field: 'visRev60', better: 'lower' },
  { id: 'visRev60_per1000', kind: 'rate1000', field: 'visRev60', better: 'lower' },
  { id: 'TV5_s_per_served', kind: 'tv', field: 'sumTV5', better: 'lower' },
  { id: 'Pchg_pp', kind: 'share', field: 'pDisp', better: 'lower' },
  { id: 'pDispAny1_pp', kind: 'share', field: 'pDispAny1', better: 'lower' },
  { id: 'late60First_pp', kind: 'share', field: 'late60First', better: 'lower' },
  { id: 'breachDeadline60_pp', kind: 'share', field: 'breachDeadline60', better: 'lower' },
  { id: 'breachLedger_pp_BY_CONSTRUCTION', kind: 'share', field: 'breachLedger', better: null },
  { id: 'late60First', kind: 'count', field: 'late60First', better: 'lower' },
  { id: 'breachDeadline60', kind: 'count', field: 'breachDeadline60', better: 'lower' },
  { id: 'relocations', kind: 'count', field: 'relocations', better: null },
  { id: 'switchedRiders', kind: 'count', field: 'switchedRiders', better: null },
];

// rows -> Map(arm -> Map(day -> summed counts)); only days with both windows (w08, w17) at r0
function dayAgg(rows, log, tag) {
  const cells = new Map();
  for (const r of rows) {
    if (r.rs !== 'r0') continue;
    const k = r.arm + '|' + r.day;
    if (!cells.has(k)) cells.set(k, { arm: r.arm, day: r.day, wins: new Set(), x: Object.fromEntries(COUNT_FIELDS.map(f => [f, 0])) });
    const c = cells.get(k);
    if (c.wins.has(r.win)) throw new Error(`duplicate row ${tag} ${r.arm} ${r.cell}`);
    c.wins.add(r.win);
    for (const f of COUNT_FIELDS) c.x[f] += r[f] || 0;
  }
  const out = new Map();
  for (const c of cells.values()) {
    if (!(c.wins.has('w08') && c.wins.has('w17'))) { log.push(`${tag}: ${c.arm} ${c.day} has windows [${[...c.wins]}] only -> day dropped for this arm`); continue; }
    if (!out.has(c.arm)) out.set(c.arm, new Map());
    out.get(c.arm).set(c.day, c.x);
  }
  return out;
}
const valOf = (m, x) => (m.kind === 'count' ? x[m.field] : m.kind === 'share' ? 100 * x[m.field] / x.served : m.kind === 'tv' ? x[m.field] / x.served / 1000 : 1000 * x[m.field] / x.served);
const mapOf = (A, m) => new Map([...A.entries()].map(([d, x]) => [d, valOf(m, x)]));

// paired contrast of arm a vs arm b over every metric of METRICS; logs days present in one arm only
function contrastAll(agg, a, b, log, tag) {
  const A = agg.get(a), B = agg.get(b);
  if (!A || !B) { log.push(`${tag}: arm ${!A ? a : b} has no complete day`); return null; }
  const onlyA = [...A.keys()].filter(d => !B.has(d)), onlyB = [...B.keys()].filter(d => !A.has(d));
  if (onlyA.length || onlyB.length) log.push(`${tag}: ${a} vs ${b}: days excluded (missing an arm): ${[...onlyA, ...onlyB].join(' ')}`);
  const res = {};
  for (const m of METRICS) res[m.id] = L.contrast(mapOf(A, m), mapOf(B, m), { better: m.better, count: m.kind === 'count' });
  return res;
}
const levels = (agg, arm) => {
  const A = agg.get(arm); if (!A) return null;
  const days = [...A.keys()], o = { nDays: days.length };
  for (const m of METRICS) o[m.id] = L.mean(days.map(d => valOf(m, A.get(d))));
  return o;
};
// RUNPLAN 2.4 exchange rates (pooled means over the days of the contrast)
function rates(C, servedB) {
  const v = C.visRev60, s = C.served, dl = C.breachDeadline60, lt = C.late60First;
  return {
    ARR_per1000: -v.mean / servedB * 1000,
    dVisRev60_day: v.mean, dServed_day: s.mean, dBreachDeadline60_day: dl.mean, dLate60First_day: lt.mean,
    ER_served: s.mean < 0 ? v.mean / s.mean : 'no service price (mean dServed >= 0)',
    ER_deadline: dl.mean > 1e-9 ? -v.mean / dl.mean : 'no deadline price (mean dBreach <= 0)',
    ER_late: lt.mean > 1e-9 ? -v.mean / lt.mean : 'no lateness price (mean dLate <= 0)',
  };
}
// counts of days by sign of a contrast's deltas
function signCounts(c) {
  const d = Object.values(c.deltas);
  return { n: d.length, neg: d.filter(x => x < -1e-9).length, pos: d.filter(x => x > 1e-9).length, nonNeg: d.filter(x => x >= -1e-9).length, nonPos: d.filter(x => x <= 1e-9).length };
}
// C1 rule (R4 per config; R5 per variant): REVERSED if dVisRev60 >= 0 on >= half of the days; WEAKENED if relative reduction < 20 % or
// < 75 % of (non-tied) days in favour; else HOLDS. Also reports the RUNPLAN 2.3 general REVERSED test (sign p < 0.05 in the opposite direction).
function labelC1(cv) {
  const sc = signCounts(cv), rel = cv.rel, fav = cv.favourShareNonTied;
  let label;
  if (sc.nonNeg >= sc.n / 2) label = 'REVERSED';
  else if (!(rel >= 0.20) || !(fav >= 0.75)) label = 'WEAKENED';
  else label = 'HOLDS';
  return { label, rel, favourNonTied: fav, favourAllDays: cv.favourShareAll, daysNonNeg: sc.nonNeg, nDays: sc.n, signP: cv.p,
    general23Reversed: cv.pos >= sc.n / 2 && cv.p < 0.05 && cv.mean > 0 };
}
// C2 rule: FAILS if mean dServed (RCE35 - EOHZ35) < -8/day with >= 2/3 of days negative; else HOLDS
function labelC2(cs) {
  const sc = signCounts(cs);
  return { label: cs.mean < -8 && sc.neg >= (2 / 3) * sc.n ? 'FAILS' : 'HOLDS', meanDServed: cs.mean, lo: cs.lo, hi: cs.hi, daysNeg: sc.neg, nDays: sc.n,
    relToB_pct: 100 * cs.mean / cs.meanB };
}
const fmtLevels = (lv) => lv ? `served ${L.f(lv.served, 1)} | visRev60 ${L.f(lv.visRev60, 2)} (${L.f(lv.visRev60_per1000, 2)}/1000) | TV5 ${L.f(lv.TV5_s_per_served, 2)} s | Pchg ${L.f(lv.Pchg_pp, 2)}% | any1 ${L.f(lv.pDispAny1_pp, 2)}% | late60F ${L.f(lv.late60First_pp, 2)}% | breachDL60 ${L.f(lv.breachDeadline60_pp, 2)}% | breachLedger ${L.f(lv.breachLedger_pp_BY_CONSTRUCTION, 2)}% (by construction for RC arms) | reloc ${L.f(lv.relocations, 1)} | switched ${L.f(lv.switchedRiders, 1)} | days ${lv.nDays}` : 'NA';
const strip = c => { if (!c) return c; const o = {}; for (const [k, v] of Object.entries(c)) { const { days, ...rest } = v; o[k] = rest; } return o; };

module.exports = { METRICS, dayAgg, contrastAll, levels, rates, signCounts, labelC1, labelC2, fmtLevels, strip, valOf };
