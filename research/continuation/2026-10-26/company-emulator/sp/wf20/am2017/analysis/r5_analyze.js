// wf17 R5 analysis (EXPLORATORY, POST-AUDIT; RUNPLAN R5 + 2.1-2.4). Written and sha256-hashed before the R5 production run.
// Input: results/r5.ndjson (rows of code/scripts/drive17.js --check R5). Panel D30 x {w08, w17} x r0. Variants, one factor at a time
// from B0 (P512, 62 veh, 2000 permille, dwell 0): B0, V-P256 (P256 cells, 42 veh), V-F56, V-F68, V-R15, V-R25, V-D24 (dwell 24 s).
// Arms: G2 U, EOHZ35, RCE35, NCE35; INS EOHZ35, RCE35. Unit = day; sign test; mean, SE, 95 % t-interval; relative change; per 1000 served.
// Decision labels (pre-stated, RUNPLAN R5 flip criteria):
//   C1 per variant (G2 visRev60, RCE35 - EOHZ35): rule of R4 (REVERSED if dVis >= 0 on >= half of the days; WEAKENED if rel < 20 % or
//      < 75 % of non-tied days in favour; else HOLDS).
//   C8 overall: REVERSED if any variant has dVis >= 0 on >= half of the days; else WEAKENED if any variant has rel < 20 % or < 75 % of
//      non-tied days in favour (variants named; "regime dependent"); else HOLDS if every variant has rel in [20 %, 60 %] with >= 75 % in
//      favour; else INCONCLUSIVE (some variant above 60 %, none below the thresholds; reported as "direction holds, size above range").
//   C2 per variant (G2 served, RCE35 - EOHZ35): FAILS if mean < -8/day with >= 2/3 of days negative; else HOLDS.
//   C3 per variant (INS served, RCE35 - EOHZ35): WEAKENED if |mean| > 8/day with >= 2/3 of the days on the side of the mean; else HOLDS.
//      Overall WEAKENED if any variant is.
//   C4 per variant (INS breachDeadline60, RCE35 - EOHZ35): REVERSED if Delta <= 0 on >= half of the days; else HOLDS.
//   Robust (RUNPLAN 2.3): HOLDS in >= 80 % of the 7 variants (>= 6/7) and REVERSED in none.
// Descriptive only: exchange rates (2.4) for G2 vs EOHZ35 and vs NCE35 and for INS vs EOHZ35; difference-in-differences vs B0
// (DD_d = Delta_d(variant) - Delta_d(B0)) for G2 visRev60, visRev60 per 1000, served and INS served, visRev60, breachDeadline60.
'use strict';
const fs = require('fs'), path = require('path');
const L = require('./lib17.js');
const G = require('./agg17.js');
const KEY = path.resolve(__dirname, '..');
const rowsFile = process.argv[2] || path.join(KEY, 'results/r5.ndjson');
const outTxt = process.argv[3] || path.join(KEY, 'results/r5_tables.txt');
const outJson = outTxt.replace(/\.txt$/, '.json');
const VARS = ['B0', 'V-P256', 'V-F56', 'V-F68', 'V-R15', 'V-R25', 'V-D24'];
const ARMS = { G2: ['U', 'EOHZ35', 'RCE35', 'NCE35'], INS: ['EOHZ35', 'RCE35'] };
const PRED = {
  B0: 'G2 rel about 40-44 % (wf14 / a5 V0)', 'V-P256': 'G2 rel 25-40 %, absolute -3 to -6/day', 'V-F56': 'G2 rel about 36-42 %', 'V-F68': 'G2 rel about 42-47 %',
  'V-R15': 'G2 rel about 40 % (about -7/day)', 'V-R25': 'G2 rel about 39 % (about -26/day)', 'V-D24': 'G2 rel 30-45 % (no prior data)',
};
const LINES = [], LOG = [];
const P = s => LINES.push(s);
const all = L.readNd(rowsFile).filter(r => r.label === 'wf17-exploratory-post-audit' && r.check === 'R5');
const S = { label: 'wf17-exploratory-post-audit', check: 'R5', input: rowsFile, nRows: all.length, perVariant: {}, labels: {}, overall: {}, dd: {}, log: LOG };
P('wf17 R5 regime grid (EXPLORATORY, POST-AUDIT; label wf17-exploratory-post-audit). Unit = day (w08 + w17, r0), panel D30.');
P(`rows ${all.length} (expected 2520). Exact two-sided sign test, ties dropped; 95 % t-interval over days. Shares in % of served, deltas in pp.`);
P('Prediction for every variant (RUNPLAN R5): G2 served vs EOHZ35 between 0 and -5/day; INS |dServed| <= 4/day, dVisRev60 -0.2 to -2.5/day (0 to -25 %), dBreachDeadline60 > 0.');
const AGG = {};
for (const v of VARS) {
  AGG[v] = {};
  for (const cfg of ['G2', 'INS']) {
    const rows = all.filter(r => r.variant === v && r.config === cfg);
    const cnt = Object.fromEntries(ARMS[cfg].map(a => [a, rows.filter(r => r.arm === a).length]));
    if (Object.values(cnt).some(n => n !== 60)) LOG.push(`${v} ${cfg}: rows per arm ${JSON.stringify(cnt)} (expected 60 each)`);
    AGG[v][cfg] = G.dayAgg(rows, LOG, `${v} ${cfg}`);
  }
}
const sideCount = c => { const d = Object.values(c.deltas); return c.mean >= 0 ? d.filter(x => x > 1e-9).length : d.filter(x => x < -1e-9).length; };
for (const v of VARS) {
  P(''); P(`==================== ${v} ====================`);
  P(`prediction (RUNPLAN R5): ${PRED[v]}`);
  const out = { levels: {}, contrasts: {}, rates: {} };
  for (const cfg of ['G2', 'INS']) {
    for (const a of ARMS[cfg]) { out.levels[`${cfg} ${a}`] = G.levels(AGG[v][cfg], a); P(`level ${cfg.padEnd(3)} ${a.padEnd(6)} ${G.fmtLevels(out.levels[`${cfg} ${a}`])}`); }
  }
  const pairs = [['G2', 'RCE35', 'EOHZ35'], ['G2', 'RCE35', 'NCE35'], ['G2', 'NCE35', 'EOHZ35'], ['INS', 'RCE35', 'EOHZ35']];
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
    if (k !== 'G2 NCE35-EOHZ35') { out.rates[k] = G.rates(C, C.served.meanB); P(`   exchange rates (2.4): ${JSON.stringify(out.rates[k])}`); }
  }
  const ce = out.contrasts['G2 RCE35-EOHZ35'], ci = out.contrasts['INS RCE35-EOHZ35'];
  const c1 = ce ? G.labelC1(ce.visRev60) : null, c2 = ce ? G.labelC2(ce.served) : null;
  let c3 = null, c4 = null;
  if (ci) {
    const sc = sideCount(ci.served), n = ci.served.nDays;
    c3 = { label: Math.abs(ci.served.mean) > 8 && sc >= (2 / 3) * n ? 'WEAKENED' : 'HOLDS', meanDServed: ci.served.mean, lo: ci.served.lo, hi: ci.served.hi, daysOnMeanSide: sc, nDays: n,
      dVisRev60: ci.visRev60.mean, relVisRev60: ci.visRev60.rel };
    const sb = G.signCounts(ci.breachDeadline60);
    c4 = { label: sb.nonPos >= sb.n / 2 ? 'REVERSED' : 'HOLDS', meanDBreach: ci.breachDeadline60.mean, lo: ci.breachDeadline60.lo, hi: ci.breachDeadline60.hi, daysNonPos: sb.nonPos, daysPos: sb.pos, nDays: sb.n,
      dBreach_pp: ci.breachDeadline60_pp.mean };
  }
  P(`C1 (G2 visRev60 RCE35 vs EOHZ35): ${c1 ? c1.label : 'NA'} | rel ${c1 ? L.f(100 * c1.rel, 1) : 'NA'}% | favour(non-tied) ${c1 ? L.f(100 * c1.favourNonTied, 0) : 'NA'}% | days dVis>=0 ${c1 ? c1.daysNonNeg + '/' + c1.nDays : 'NA'}`);
  P(`C2 (G2 served): ${c2 ? c2.label : 'NA'} | mean ${c2 ? L.f(c2.meanDServed, 2) : 'NA'}/day [${c2 ? L.f(c2.lo, 2) : 'NA'}, ${c2 ? L.f(c2.hi, 2) : 'NA'}] (${c2 ? L.f(c2.relToB_pct, 2) : 'NA'}% of EOHZ35) | days negative ${c2 ? c2.daysNeg + '/' + c2.nDays : 'NA'}`);
  P(`C3 (INS served): ${c3 ? c3.label : 'NA'} | mean ${c3 ? L.f(c3.meanDServed, 2) : 'NA'}/day [${c3 ? L.f(c3.lo, 2) : 'NA'}, ${c3 ? L.f(c3.hi, 2) : 'NA'}] | days on mean side ${c3 ? c3.daysOnMeanSide + '/' + c3.nDays : 'NA'} | INS dVisRev60 ${c3 ? L.f(c3.dVisRev60, 2) : 'NA'}/day (rel ${c3 ? L.f(100 * c3.relVisRev60, 1) : 'NA'}%)`);
  P(`C4 (INS breachDeadline60): ${c4 ? c4.label : 'NA'} | mean ${c4 ? L.f(c4.meanDBreach, 2) : 'NA'}/day [${c4 ? L.f(c4.lo, 2) : 'NA'}, ${c4 ? L.f(c4.hi, 2) : 'NA'}] (${c4 ? L.f(c4.dBreach_pp, 3) : 'NA'} pp) | days <= 0 ${c4 ? c4.daysNonPos + '/' + c4.nDays : 'NA'}`);
  S.perVariant[v] = { levels: out.levels, contrasts: Object.fromEntries(Object.entries(out.contrasts).map(([k, c]) => [k, G.strip(c)])), rates: out.rates };
  S.labels[v] = { C1: c1, C2: c2, C3: c3, C4: c4 };
}
// C8 overall and robustness per conclusion
const c1s = VARS.map(v => S.labels[v].C1).filter(Boolean);
const rev = VARS.filter(v => S.labels[v].C1 && S.labels[v].C1.label === 'REVERSED');
const weak = VARS.filter(v => S.labels[v].C1 && (!(S.labels[v].C1.rel >= 0.20) || !(S.labels[v].C1.favourNonTied >= 0.75)));
const above = VARS.filter(v => S.labels[v].C1 && S.labels[v].C1.rel > 0.60);
let c8;
if (c1s.length < VARS.length) c8 = 'INCONCLUSIVE (missing variants)';
else if (rev.length) c8 = 'REVERSED';
else if (weak.length) c8 = 'WEAKENED';
else if (!above.length) c8 = 'HOLDS';
else c8 = 'INCONCLUSIVE';
S.overall.C8 = { label: c8, reversedIn: rev, weakenedIn: weak, aboveRangeIn: above };
for (const c of ['C1', 'C2', 'C3', 'C4']) {
  const labs = VARS.map(v => S.labels[v][c] && S.labels[v][c].label);
  const nHold = labs.filter(x => x === 'HOLDS').length, nRev = labs.filter(x => x === 'REVERSED').length;
  S.overall[c] = { labels: Object.fromEntries(VARS.map((v, i) => [v, labs[i]])), nHolds: nHold, nReversed: nRev, robust: nHold >= 0.8 * VARS.length && nRev === 0 };
}
S.overall.C3.label = VARS.some(v => S.labels[v].C3 && S.labels[v].C3.label === 'WEAKENED') ? 'WEAKENED' : 'HOLDS';
// DD vs B0 (descriptive)
const eff = (v, cfg, m) => { const c = G.contrastAll(AGG[v][cfg], 'RCE35', 'EOHZ35', [], v); return c ? new Map(Object.entries(c[m].deltas)) : new Map(); };
P(''); P('==================== DD vs B0 (descriptive): DD_d = Delta_d(variant) - Delta_d(B0), Delta = RCE35 - EOHZ35 ====================');
for (const v of VARS.slice(1)) for (const [cfg, m] of [['G2', 'visRev60'], ['G2', 'visRev60_per1000'], ['G2', 'served'], ['INS', 'served'], ['INS', 'visRev60'], ['INS', 'breachDeadline60']]) {
  const dd = L.contrast(eff(v, cfg, m), eff('B0', cfg, m), { better: null, count: false });
  S.dd[`${v}|${cfg}|${m}`] = { mean: dd.mean, se: dd.se, lo: dd.lo, hi: dd.hi, neg: dd.neg, pos: dd.pos, ties: dd.ties, p: dd.p, nDays: dd.nDays };
  P(`${v.padEnd(7)} ${cfg.padEnd(3)} ${m.padEnd(17)} DD ${L.fmtC(dd)}`);
}
P(''); P('==================== Summary (one line per variant) ====================');
P('variant | G2 rel visRev60 | dVis/day [CI] | /1000 served | favour | C1 | G2 dServed [CI] | days neg | C2 | G2 rel vs NCE35 | dServed vs NCE35 | INS dServed [CI] | C3 | INS dVis (rel) | INS dBreachDL60/day | days<=0 | C4 | G2 ER_served | G2 ER_deadline | INS ER_deadline');
const fx = x => (typeof x === 'number' ? L.f(x, 2) : x);
for (const v of VARS) {
  const pv = S.perVariant[v], lb = S.labels[v];
  const ce = pv.contrasts['G2 RCE35-EOHZ35'], cn = pv.contrasts['G2 RCE35-NCE35'], ci = pv.contrasts['INS RCE35-EOHZ35'];
  if (!ce || !cn || !ci) { P(`${v}: NA`); continue; }
  P(`${v.padEnd(7)} | ${L.f(100 * ce.visRev60.rel, 1)}% | ${L.f(ce.visRev60.mean, 2)} [${L.f(ce.visRev60.lo, 2)}, ${L.f(ce.visRev60.hi, 2)}] | ${L.f(ce.visRev60_per1000.mean, 2)} | ${L.f(100 * ce.visRev60.favourShareNonTied, 0)}% | ${lb.C1.label} | ` +
    `${L.f(ce.served.mean, 2)} [${L.f(ce.served.lo, 2)}, ${L.f(ce.served.hi, 2)}] | ${lb.C2.daysNeg}/${lb.C2.nDays} | ${lb.C2.label} | ${L.f(100 * cn.visRev60.rel, 1)}% | ${L.f(cn.served.mean, 2)} | ` +
    `${L.f(ci.served.mean, 2)} [${L.f(ci.served.lo, 2)}, ${L.f(ci.served.hi, 2)}] | ${lb.C3.label} | ${L.f(ci.visRev60.mean, 2)} (${L.f(100 * ci.visRev60.rel, 1)}%) | ${L.f(ci.breachDeadline60.mean, 2)} | ${lb.C4.daysNonPos}/${lb.C4.nDays} | ${lb.C4.label} | ` +
    `${fx(pv.rates['G2 RCE35-EOHZ35'].ER_served)} | ${fx(pv.rates['G2 RCE35-EOHZ35'].ER_deadline)} | ${fx(pv.rates['INS RCE35-EOHZ35'].ER_deadline)}`);
}
P(`C8 overall: ${JSON.stringify(S.overall.C8)}`);
for (const c of ['C1', 'C2', 'C3', 'C4']) P(`${c} across variants: ${JSON.stringify(S.overall[c])}`);
P(''); P('log:'); for (const l of LOG) P('  ' + l);
fs.writeFileSync(outTxt, LINES.join('\n') + '\n');
fs.writeFileSync(outJson, JSON.stringify(S, null, 1));
console.log(`wrote ${outTxt} and ${outJson}; rows ${all.length}; log ${LOG.length} lines`);
