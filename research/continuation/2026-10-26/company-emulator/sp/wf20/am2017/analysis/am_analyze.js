// wf20/am2017 analysis (thăm dò sau kiểm toán; exploratory, post-audit). Adapted from wf17/r2-penalty-regime/analysis/r5_analyze.js
// (sha256 7ff23053...): same day aggregation (agg17.js), same sign test / t-interval (lib17.js), same C1/C2/C3/C4 label rules (R5),
// written and sha256-hashed (PLAN-HASH.txt) BEFORE the production run.
// Input: results/am.ndjson (rows of code/scripts/drive20.js --check AM). Panel D30 x {w08, w17} x r0. Variants = fleet points
// F20 (AM17 2000x4, peak-window proxy), F29 (AM17 2000x4, 24-h average), F43 (AM17 3000x4, 24-h average), F52 (FA20 2000x4, 13-15 h),
// F62 (thesis P512 = wf14 / R5 B0), F67 (AM17 3000x4 trip-time adjusted, kappa 1.55), F81 (FA20 trip-time adjusted).
// Arms: G2 and INS each U, EO35, EOHZ35, RCE35. Unit = day; exact two-sided sign test (ties dropped); mean, 95 % t-interval.
// Labels (PLAN.md section 4, = wf17 R5 rules):
//   C1 per point (G2 visRev60, RCE35 - EOHZ35): REVERSED if dVis >= 0 on >= half of the days; WEAKENED if rel < 20 % or < 75 % of
//      non-tied days in favour; else HOLDS.
//   C8' overall ("C1 direction does not depend on the fleet anchor"): REVERSED if any point REVERSED; else WEAKENED if any point has
//      rel < 20 % or favour < 75 %; else HOLDS if every point has rel in [20 %, 60 %]; else INCONCLUSIVE (direction holds, size above range).
//   C2 per point (G2 served): FAILS if mean < -8/day with >= 2/3 of days negative; else HOLDS (threshold literal; % of EOHZ35 printed).
//   C3 per point (INS served): WEAKENED if |mean| > 8/day with >= 2/3 of days on the side of the mean; else HOLDS.
//   C4 per point (INS breachDeadline60): REVERSED if Delta <= 0 on >= half of the days; else HOLDS.
//   Robust = HOLDS in >= 80 % of the 7 points (>= 6/7) and REVERSED in none.
// Descriptive only (no label): U service rate (served / requests) per point and config; RCE35 - EO35 and EOHZ35 - EO35 contrasts;
// exchange rates; DD vs F62; H-req / H-veh reading of the G2 per-1000 effect at F29 and F20 (PLAN.md 4.3).
'use strict';
const fs = require('fs'), path = require('path');
const L = require('./lib17.js');
const G = require('./agg17.js');
const KEY = path.resolve(__dirname, '..');
const rowsFile = process.argv[2] || path.join(KEY, 'results/am.ndjson');
const outTxt = process.argv[3] || path.join(KEY, 'results/am_tables.txt');
const outJson = outTxt.replace(/\.txt$/, '.json');
const LABEL = 'wf20-am2017 thăm dò sau kiểm toán';
const VARS = ['F20', 'F29', 'F43', 'F52', 'F62', 'F67', 'F81'];
const NVEH = { F20: 20, F29: 29, F43: 43, F52: 52, F62: 62, F67: 67, F81: 81 };
const ANCHOR = { F20: 'AM17 2000x4 peak-window proxy', F29: 'AM17 2000x4 24-h avg', F43: 'AM17 3000x4 24-h avg', F52: 'FA20 2000x4 13-15 h',
  F62: 'thesis P512 (wf14/R5 B0)', F67: 'AM17 3000x4 x kappa', F81: 'FA20 x kappa' };
const ARMS = { G2: ['U', 'EO35', 'EOHZ35', 'RCE35'], INS: ['U', 'EO35', 'EOHZ35', 'RCE35'] };
const LINES = [], LOG = [];
const P = s => LINES.push(s);
const all = L.readNd(rowsFile).filter(r => r.label === LABEL && r.check === 'AM' && VARS.includes(r.variant));
const S = { label: LABEL, check: 'AM', input: rowsFile, nRows: all.length, perVariant: {}, labels: {}, overall: {}, dd: {}, service: {}, hReading: {}, log: LOG };
P(`wf20/am2017 fleet anchors (${LABEL}; exploratory, post-audit). Unit = day (w08 + w17, r0), panel D30 (seen days).`);
P(`rows ${all.length} (expected 3360). Exact two-sided sign test, ties dropped; 95 % t-interval over days. Shares in % of served, deltas in pp.`);
const AGG = {};
for (const v of VARS) {
  AGG[v] = {};
  for (const cfg of ['G2', 'INS']) {
    const rows = all.filter(r => r.variant === v && r.config === cfg);
    const cnt = Object.fromEntries(ARMS[cfg].map(a => [a, rows.filter(r => r.arm === a).length]));
    if (Object.values(cnt).some(n => n !== 60)) LOG.push(`${v} ${cfg}: rows per arm ${JSON.stringify(cnt)} (expected 60 each)`);
    const bad = rows.filter(r => r.nVeh !== NVEH[v] || r.nReq !== 512 || r.maxRidePermille !== 2000 || r.dwellMs !== 0);
    if (bad.length) LOG.push(`${v} ${cfg}: ${bad.length} rows with unexpected nVeh/nReq/permille/dwell`);
    AGG[v][cfg] = G.dayAgg(rows, LOG, `${v} ${cfg}`);
  }
}
const sideCount = c => { const d = Object.values(c.deltas); return c.mean >= 0 ? d.filter(x => x > 1e-9).length : d.filter(x => x < -1e-9).length; };
const svc = (agg, arm) => { const A = agg.get(arm); if (!A) return null; const d = [...A.values()]; const s = d.reduce((a, x) => a + x.served, 0), q = d.reduce((a, x) => a + x.requests, 0); return { servedPerDay: s / d.length, requestsPerDay: q / d.length, ratePct: 100 * s / q, nDays: d.length }; };
for (const v of VARS) {
  P(''); P(`==================== ${v} (${NVEH[v]} vehicles; ${ANCHOR[v]}) ====================`);
  const out = { levels: {}, contrasts: {}, rates: {} };
  S.service[v] = {};
  for (const cfg of ['G2', 'INS']) {
    for (const a of ARMS[cfg]) {
      out.levels[`${cfg} ${a}`] = G.levels(AGG[v][cfg], a);
      S.service[v][`${cfg} ${a}`] = svc(AGG[v][cfg], a);
      const sv = S.service[v][`${cfg} ${a}`];
      P(`level ${cfg.padEnd(3)} ${a.padEnd(6)} service ${sv ? L.f(sv.ratePct, 1) : 'NA'}% | ${G.fmtLevels(out.levels[`${cfg} ${a}`])}`);
    }
  }
  const pairs = [['G2', 'RCE35', 'EOHZ35'], ['G2', 'RCE35', 'EO35'], ['G2', 'EOHZ35', 'EO35'], ['INS', 'RCE35', 'EOHZ35'], ['INS', 'RCE35', 'EO35'], ['INS', 'EOHZ35', 'EO35']];
  for (const [cfg, a, b] of pairs) {
    const k = `${cfg} ${a}-${b}`;
    const C = G.contrastAll(AGG[v][cfg], a, b, LOG, `${v} ${k}`);
    out.contrasts[k] = C;
    if (!C) { P(`${k}: NA`); continue; }
    P(`--- ${k}`);
    for (const m of G.METRICS) {
      if (cfg === 'INS' && (m.id === 'relocations' || m.id === 'switchedRiders')) continue;
      P(`   ${m.id.padEnd(32)} ${L.fmtC(C[m.id], { k: m.kind === 'count' ? 2 : 3 })} | favour(non-tied) ${C[m.id].favourShareNonTied === null ? 'NA' : L.f(100 * C[m.id].favourShareNonTied, 0) + '%'}`);
    }
    if (a === 'RCE35') { out.rates[k] = G.rates(C, C.served.meanB); P(`   exchange rates: ${JSON.stringify(out.rates[k])}`); }
  }
  const ce = out.contrasts['G2 RCE35-EOHZ35'], ci = out.contrasts['INS RCE35-EOHZ35'];
  const c1 = ce ? G.labelC1(ce.visRev60) : null, c2 = ce ? G.labelC2(ce.served) : null;
  let c3 = null, c4 = null;
  if (ci) {
    const sc = sideCount(ci.served), n = ci.served.nDays;
    c3 = { label: Math.abs(ci.served.mean) > 8 && sc >= (2 / 3) * n ? 'WEAKENED' : 'HOLDS', meanDServed: ci.served.mean, lo: ci.served.lo, hi: ci.served.hi, daysOnMeanSide: sc, nDays: n,
      dVisRev60: ci.visRev60.mean, relVisRev60: ci.visRev60.rel, dVisRev60_per1000: ci.visRev60_per1000.mean };
    const sb = G.signCounts(ci.breachDeadline60);
    c4 = { label: sb.nonPos >= sb.n / 2 ? 'REVERSED' : 'HOLDS', meanDBreach: ci.breachDeadline60.mean, lo: ci.breachDeadline60.lo, hi: ci.breachDeadline60.hi, daysNonPos: sb.nonPos, daysPos: sb.pos, nDays: sb.n,
      dBreach_pp: ci.breachDeadline60_pp.mean };
  }
  P(`C1 (G2 visRev60 RCE35 vs EOHZ35): ${c1 ? c1.label : 'NA'} | rel ${c1 ? L.f(100 * c1.rel, 1) : 'NA'}% | per1000 ${ce ? L.f(ce.visRev60_per1000.mean, 2) : 'NA'} | favour(non-tied) ${c1 ? L.f(100 * c1.favourNonTied, 0) : 'NA'}% | days dVis>=0 ${c1 ? c1.daysNonNeg + '/' + c1.nDays : 'NA'}`);
  P(`C2 (G2 served): ${c2 ? c2.label : 'NA'} | mean ${c2 ? L.f(c2.meanDServed, 2) : 'NA'}/day [${c2 ? L.f(c2.lo, 2) : 'NA'}, ${c2 ? L.f(c2.hi, 2) : 'NA'}] (${c2 ? L.f(c2.relToB_pct, 2) : 'NA'}% of EOHZ35) | days negative ${c2 ? c2.daysNeg + '/' + c2.nDays : 'NA'}`);
  P(`C3 (INS served): ${c3 ? c3.label : 'NA'} | mean ${c3 ? L.f(c3.meanDServed, 2) : 'NA'}/day [${c3 ? L.f(c3.lo, 2) : 'NA'}, ${c3 ? L.f(c3.hi, 2) : 'NA'}] | days on mean side ${c3 ? c3.daysOnMeanSide + '/' + c3.nDays : 'NA'} | INS dVisRev60 ${c3 ? L.f(c3.dVisRev60, 2) : 'NA'}/day (rel ${c3 ? L.f(100 * c3.relVisRev60, 1) : 'NA'}%, per1000 ${c3 ? L.f(c3.dVisRev60_per1000, 2) : 'NA'})`);
  P(`C4 (INS breachDeadline60): ${c4 ? c4.label : 'NA'} | mean ${c4 ? L.f(c4.meanDBreach, 2) : 'NA'}/day [${c4 ? L.f(c4.lo, 2) : 'NA'}, ${c4 ? L.f(c4.hi, 2) : 'NA'}] (${c4 ? L.f(c4.dBreach_pp, 3) : 'NA'} pp) | days <= 0 ${c4 ? c4.daysNonPos + '/' + c4.nDays : 'NA'}`);
  S.perVariant[v] = { levels: out.levels, contrasts: Object.fromEntries(Object.entries(out.contrasts).map(([k, c]) => [k, G.strip(c)])), rates: out.rates };
  S.labels[v] = { C1: c1, C2: c2, C3: c3, C4: c4 };
}
// C8' overall and robustness per conclusion
const c1s = VARS.map(v => S.labels[v].C1).filter(Boolean);
const rev = VARS.filter(v => S.labels[v].C1 && S.labels[v].C1.label === 'REVERSED');
const weak = VARS.filter(v => S.labels[v].C1 && (!(S.labels[v].C1.rel >= 0.20) || !(S.labels[v].C1.favourNonTied >= 0.75)));
const above = VARS.filter(v => S.labels[v].C1 && S.labels[v].C1.rel > 0.60);
let c8;
if (c1s.length < VARS.length) c8 = 'INCONCLUSIVE (missing points)';
else if (rev.length) c8 = 'REVERSED';
else if (weak.length) c8 = 'WEAKENED';
else if (!above.length) c8 = 'HOLDS';
else c8 = 'INCONCLUSIVE';
S.overall.C8prime = { label: c8, reversedIn: rev, weakenedIn: weak, aboveRangeIn: above };
for (const c of ['C1', 'C2', 'C3', 'C4']) {
  const labs = VARS.map(v => S.labels[v][c] && S.labels[v][c].label);
  const nHold = labs.filter(x => x === 'HOLDS').length, nRev = labs.filter(x => x === 'REVERSED').length;
  S.overall[c] = { labels: Object.fromEntries(VARS.map((v, i) => [v, labs[i]])), nHolds: nHold, nReversed: nRev, robust: nHold >= 0.8 * VARS.length && nRev === 0 };
}
S.overall.C3.label = VARS.some(v => S.labels[v].C3 && S.labels[v].C3.label === 'WEAKENED') ? 'WEAKENED' : 'HOLDS';
// H-req / H-veh reading (PLAN 4.3; descriptive): G2 per-1000 effect magnitude at F29 and F20
for (const v of ['F20', 'F29']) {
  const ce = S.perVariant[v] && S.perVariant[v].contrasts['G2 RCE35-EOHZ35'];
  if (!ce) { S.hReading[v] = 'NA'; continue; }
  const mag = -ce.visRev60_per1000.mean;
  S.hReading[v] = { per1000: ce.visRev60_per1000.mean, reading: mag >= 15 ? 'consistent with H-req (request density drives the per-rider effect)' : mag <= 10 ? 'consistent with H-veh (vehicle count/density drives it, like P256)' : 'inconclusive (10-15)' };
}
// DD vs F62 (descriptive)
const eff = (v, cfg, m) => { const c = G.contrastAll(AGG[v][cfg], 'RCE35', 'EOHZ35', [], v); return c ? new Map(Object.entries(c[m].deltas)) : new Map(); };
P(''); P('==================== DD vs F62 (descriptive): DD_d = Delta_d(point) - Delta_d(F62), Delta = RCE35 - EOHZ35 ====================');
for (const v of VARS.filter(x => x !== 'F62')) for (const [cfg, m] of [['G2', 'visRev60'], ['G2', 'visRev60_per1000'], ['G2', 'served'], ['INS', 'served'], ['INS', 'visRev60'], ['INS', 'breachDeadline60']]) {
  const dd = L.contrast(eff(v, cfg, m), eff('F62', cfg, m), { better: null, count: false });
  S.dd[`${v}|${cfg}|${m}`] = { mean: dd.mean, se: dd.se, lo: dd.lo, hi: dd.hi, neg: dd.neg, pos: dd.pos, ties: dd.ties, p: dd.p, nDays: dd.nDays };
  P(`${v.padEnd(4)} ${cfg.padEnd(3)} ${m.padEnd(17)} DD ${L.fmtC(dd)}`);
}
P(''); P('==================== Service (U) per fleet point: served / requests, % (per day = w08 + w17) ====================');
P('point | veh | req/veh/2h | INS U % | G2 U % | INS EOHZ35 % | G2 EOHZ35 % | INS RCE35 % | G2 RCE35 % | INS EO35 % | G2 EO35 %');
for (const v of VARS) {
  const s = S.service[v], g = k => (s[k] ? L.f(s[k].ratePct, 1) : 'NA');
  P(`${v.padEnd(4)} | ${NVEH[v]} | ${L.f(512 / NVEH[v], 2)} | ${g('INS U')} | ${g('G2 U')} | ${g('INS EOHZ35')} | ${g('G2 EOHZ35')} | ${g('INS RCE35')} | ${g('G2 RCE35')} | ${g('INS EO35')} | ${g('G2 EO35')}`);
}
P(''); P('==================== Summary (one line per point; RCE35 - EOHZ35) ====================');
P('point | G2 rel visRev60 | dVis/day [CI] | /1000 served | favour | C1 | G2 dServed [CI] | days neg | C2 | INS dServed [CI] | C3 | INS dVis (rel) /1000 | INS dBreachDL60/day | days<=0 | C4 | G2 dLate60F/day | G2 dPchg pp | INS dLate60F/day | INS dPchg pp');
for (const v of VARS) {
  const pv = S.perVariant[v], lb = S.labels[v];
  const ce = pv.contrasts['G2 RCE35-EOHZ35'], ci = pv.contrasts['INS RCE35-EOHZ35'];
  if (!ce || !ci) { P(`${v}: NA`); continue; }
  P(`${v.padEnd(4)} | ${L.f(100 * ce.visRev60.rel, 1)}% | ${L.f(ce.visRev60.mean, 2)} [${L.f(ce.visRev60.lo, 2)}, ${L.f(ce.visRev60.hi, 2)}] | ${L.f(ce.visRev60_per1000.mean, 2)} | ${L.f(100 * ce.visRev60.favourShareNonTied, 0)}% | ${lb.C1.label} | ` +
    `${L.f(ce.served.mean, 2)} [${L.f(ce.served.lo, 2)}, ${L.f(ce.served.hi, 2)}] | ${lb.C2.daysNeg}/${lb.C2.nDays} | ${lb.C2.label} | ` +
    `${L.f(ci.served.mean, 2)} [${L.f(ci.served.lo, 2)}, ${L.f(ci.served.hi, 2)}] | ${lb.C3.label} | ${L.f(ci.visRev60.mean, 2)} (${L.f(100 * ci.visRev60.rel, 1)}%) ${L.f(ci.visRev60_per1000.mean, 2)} | ${L.f(ci.breachDeadline60.mean, 2)} | ${lb.C4.daysNonPos}/${lb.C4.nDays} | ${lb.C4.label} | ` +
    `${L.f(ce.late60First.mean, 2)} | ${L.f(ce.Pchg_pp.mean, 2)} | ${L.f(ci.late60First.mean, 2)} | ${L.f(ci.Pchg_pp.mean, 2)}`);
}
P(`C8' overall: ${JSON.stringify(S.overall.C8prime)}`);
for (const c of ['C1', 'C2', 'C3', 'C4']) P(`${c} across points: ${JSON.stringify(S.overall[c])}`);
P(`H reading (PLAN 4.3): ${JSON.stringify(S.hReading)}`);
P(''); P('log:'); for (const l of LOG) P('  ' + l);
fs.writeFileSync(outTxt, LINES.join('\n') + '\n');
fs.writeFileSync(outJson, JSON.stringify(S, null, 1));
console.log(`wrote ${outTxt} and ${outJson}; rows ${all.length}; log ${LOG.length} lines`);
