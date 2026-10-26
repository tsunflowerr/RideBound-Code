// wf17 R4 analysis (EXPLORATORY, POST-AUDIT; RUNPLAN R4 + 2.1-2.4). Written and sha256-hashed before the R4 production run.
// Input: results/r4.ndjson (rows of code/scripts/drive17.js --check R4). Panel D60 x {w08, w17} x r0; config G2 with
// g2Targets T in {8, 16} and g2PenaltyMs pi in {0, 60000, 300000} (6 configs); arms U, EO35, EOHZ35, RCE35, NCE35.
// Unit = day; exact two-sided sign test; mean, SE, 95 % t-interval, median; relative change for counts; days in favour (RUNPLAN 2.1).
// Contrasts per config (every RUNPLAN 2.2 metric): primary RCE35 - EOHZ35 and RCE35 - NCE35 (visRev60, served); secondary RCE35 - EO35.
// Decision labels per config (pre-stated, RUNPLAN R4 flip criteria):
//   C1  REVERSED if dVisRev60 (RCE35 - EOHZ35) >= 0 on >= half of the days; else WEAKENED if the relative reduction
//       1 - sum(RCE35)/sum(EOHZ35) < 20 % or < 75 % of the non-tied days are in favour (RUNPLAN 2.1 "days in favour"); else HOLDS.
//   C2  FAILS if mean dServed (RCE35 - EOHZ35) < -8/day with >= 2/3 of the days negative; else HOLDS.
//   Robust (RUNPLAN 2.3): HOLDS in >= 80 % of the 6 configs (i.e. >= 5/6) and REVERSED in none.
//   pi = 300 s rule: if C2 FAILS at pi = 300 s for both T, the thesis must state the service price as "about 1 % under realistic friction".
// Descriptive only (no label): exchange rates (RUNPLAN 2.4) vs EOHZ35 and vs NCE35; per-window relative reductions; difference-in-
// differences of the RCE35 - EOHZ35 effect across friction (pi vs pi = 0 at the same T; T16 vs T8 at the same pi), sign test on DD_d.
'use strict';
const fs = require('fs'), path = require('path');
const L = require('./lib17.js');
const G = require('./agg17.js');
const KEY = path.resolve(__dirname, '..');
const rowsFile = process.argv[2] || path.join(KEY, 'results/r4.ndjson');
const outTxt = process.argv[3] || path.join(KEY, 'results/r4_tables.txt');
const outJson = outTxt.replace(/\.txt$/, '.json');
const VARS = ['T8P0', 'T8P60', 'T8P300', 'T16P0', 'T16P60', 'T16P300'];
const ARMS = ['U', 'EO35', 'EOHZ35', 'RCE35', 'NCE35'];
const PRED = {
  T8P0: 'visRev60 -16 to -18/day (-40 to -44 %), 60/60 days; served -1.3 to -4.8/day',
  T8P60: 'visRev60 about -15/day (about -39 %); served about -4/day',
  T8P300: 'visRev60 about -10/day (about -30 %); served about -6.5/day',
  T16P0: 'no prior data; larger absolute cut than T8 at the same pi, relative 35-50 %; served -2 to -8/day',
  T16P60: 'as T16P0, shrinking with pi', T16P300: 'as T16P0, shrinking with pi',
};
const LINES = [], LOG = [];
const P = s => LINES.push(s);
const all = L.readNd(rowsFile).filter(r => r.label === 'wf17-exploratory-post-audit' && r.check === 'R4');
const S = { label: 'wf17-exploratory-post-audit', check: 'R4', input: rowsFile, nRows: all.length, perConfig: {}, labels: {}, robust: {}, dd: {}, log: LOG };

P('wf17 R4 G2 friction (EXPLORATORY, POST-AUDIT; label wf17-exploratory-post-audit). Unit = day (w08 + w17, r0), panel D60.');
P(`rows ${all.length} (expected 3600). Exact two-sided sign test, ties dropped; 95 % t-interval over days. Shares in % of served, deltas in pp.`);
const AGG = {};
for (const v of VARS) {
  const rows = all.filter(r => r.variant === v);
  const cnt = Object.fromEntries(ARMS.map(a => [a, rows.filter(r => r.arm === a).length]));
  if (Object.values(cnt).some(n => n !== 120)) LOG.push(`${v}: rows per arm ${JSON.stringify(cnt)} (expected 120 each)`);
  AGG[v] = G.dayAgg(rows, LOG, v);
}
for (const v of VARS) {
  const agg = AGG[v];
  P(''); P(`==================== ${v} (g2Targets ${v.startsWith('T16') ? 16 : 8}, g2PenaltyMs ${v.endsWith('P0') ? 0 : v.endsWith('P60') ? 60000 : 300000}) ====================`);
  P(`prediction (RUNPLAN R4): ${PRED[v]}`);
  const lv = {};
  for (const a of ARMS) { lv[a] = G.levels(agg, a); P(`level ${a.padEnd(6)} ${G.fmtLevels(lv[a])}`); }
  const C = {};
  for (const [a, b] of [['RCE35', 'EOHZ35'], ['RCE35', 'NCE35'], ['RCE35', 'EO35'], ['EOHZ35', 'EO35'], ['NCE35', 'EOHZ35']]) {
    const k = `${a}-${b}`;
    C[k] = G.contrastAll(agg, a, b, LOG, `${v} ${k}`);
    if (!C[k]) { P(`${k}: NA`); continue; }
    P(`--- ${k}`);
    for (const m of G.METRICS) P(`   ${m.id.padEnd(32)} ${L.fmtC(C[k][m.id], { k: m.kind === 'count' ? 2 : 3 })} | favour(non-tied) ${C[k][m.id].favourShareNonTied === null ? 'NA' : L.f(100 * C[k][m.id].favourShareNonTied, 0) + '%'}`);
  }
  const ce = C['RCE35-EOHZ35'], cn = C['RCE35-NCE35'];
  const c1 = ce ? G.labelC1(ce.visRev60) : null, c2 = ce ? G.labelC2(ce.served) : null;
  const rE = ce ? G.rates(ce, ce.served.meanB) : null, rN = cn ? G.rates(cn, cn.served.meanB) : null;
  // per-window relative reductions (descriptive)
  const byWin = {};
  for (const w of ['w08', 'w17']) {
    const rr = all.filter(r => r.variant === v && r.win === w);
    const sA = L.sum(rr.filter(r => r.arm === 'RCE35').map(r => r.visRev60)), sB = L.sum(rr.filter(r => r.arm === 'EOHZ35').map(r => r.visRev60));
    byWin[w] = { relVisRev60: sB ? 1 - sA / sB : null, sumRCE35: sA, sumEOHZ35: sB };
  }
  P(`C1 [RCE35 vs EOHZ35 visRev60]: ${c1 ? c1.label : 'NA'} | rel ${c1 ? L.f(100 * c1.rel, 1) : 'NA'}% | favour(non-tied) ${c1 ? L.f(100 * c1.favourNonTied, 0) : 'NA'}% | days dVis>=0 ${c1 ? c1.daysNonNeg : 'NA'}/${c1 ? c1.nDays : 'NA'} | sign p ${c1 ? L.fp(c1.signP) : 'NA'}`);
  P(`C2 [RCE35 vs EOHZ35 served]: ${c2 ? c2.label : 'NA'} | mean ${c2 ? L.f(c2.meanDServed, 2) : 'NA'}/day [${c2 ? L.f(c2.lo, 2) : 'NA'}, ${c2 ? L.f(c2.hi, 2) : 'NA'}] (${c2 ? L.f(c2.relToB_pct, 2) : 'NA'}% of EOHZ35 served) | days negative ${c2 ? c2.daysNeg : 'NA'}/${c2 ? c2.nDays : 'NA'}`);
  P(`per window rel. reduction visRev60: w08 ${L.f(100 * byWin.w08.relVisRev60, 1)}% , w17 ${L.f(100 * byWin.w17.relVisRev60, 1)}%`);
  P(`exchange rates vs EOHZ35 (2.4): ${JSON.stringify(rE)}`);
  P(`exchange rates vs NCE35  (2.4): ${JSON.stringify(rN)}`);
  S.perConfig[v] = { levels: lv, contrasts: Object.fromEntries(Object.entries(C).map(([k, c]) => [k, G.strip(c)])), byWin, rates: { vsEOHZ35: rE, vsNCE35: rN } };
  S.labels[v] = { C1: c1, C2: c2 };
}
// robustness across the 6 configs
for (const c of ['C1', 'C2']) {
  const labs = VARS.map(v => S.labels[v][c] && S.labels[v][c].label);
  const nHold = labs.filter(x => x === 'HOLDS').length, nRev = labs.filter(x => x === 'REVERSED').length;
  S.robust[c] = { labels: Object.fromEntries(VARS.map((v, i) => [v, labs[i]])), nHolds: nHold, nReversed: nRev, robust: nHold >= 0.8 * VARS.length && nRev === 0 };
}
const c2p300 = ['T8P300', 'T16P300'].map(v => S.labels[v].C2 && S.labels[v].C2.label);
S.robust.C2_pi300_rule = c2p300.every(x => x === 'FAILS') ? 'C2 FAILS at pi = 300 s for both T: state the service price as about 1 % under realistic friction' : `C2 at pi = 300 s: T8 ${c2p300[0]}, T16 ${c2p300[1]}`;
// difference-in-differences across friction (descriptive)
const effMap = (v, m) => { const ce = G.contrastAll(AGG[v], 'RCE35', 'EOHZ35', [], v); return ce ? new Map(Object.entries(ce[m].deltas)) : new Map(); };
P(''); P('==================== DD across friction (descriptive): DD_d = Delta_d(config) - Delta_d(reference), Delta = RCE35 - EOHZ35 ====================');
for (const [v, ref] of [['T8P60', 'T8P0'], ['T8P300', 'T8P0'], ['T16P60', 'T16P0'], ['T16P300', 'T16P0'], ['T16P0', 'T8P0'], ['T16P60', 'T8P60'], ['T16P300', 'T8P300']]) {
  for (const m of ['visRev60', 'served', 'breachDeadline60']) {
    const dd = L.contrast(effMap(v, m), effMap(ref, m), { better: null, count: false });
    S.dd[`${v}-vs-${ref}|${m}`] = { mean: dd.mean, se: dd.se, lo: dd.lo, hi: dd.hi, neg: dd.neg, pos: dd.pos, ties: dd.ties, p: dd.p, nDays: dd.nDays };
    P(`${(v + ' vs ' + ref).padEnd(18)} ${m.padEnd(17)} DD ${L.fmtC(dd)}`);
  }
}
P(''); P('==================== Summary (one line per config) ====================');
P('config  | rel visRev60 vs EOHZ35 | dVis/day [CI] | favour | C1 | dServed/day [CI] | days neg | C2 | rel vs NCE35 | dServed vs NCE35 | ARR/1000 | ER_served | ER_deadline | ER_late');
for (const v of VARS) {
  const pc = S.perConfig[v]; const ce = pc.contrasts['RCE35-EOHZ35'], cn = pc.contrasts['RCE35-NCE35'], lb = S.labels[v];
  if (!ce || !cn) { P(`${v}: NA`); continue; }
  const r = pc.rates.vsEOHZ35, fx = x => (typeof x === 'number' ? L.f(x, 2) : x);
  P(`${v.padEnd(7)} | ${L.f(100 * ce.visRev60.rel, 1)}% | ${L.f(ce.visRev60.mean, 2)} [${L.f(ce.visRev60.lo, 2)}, ${L.f(ce.visRev60.hi, 2)}] | ${L.f(100 * ce.visRev60.favourShareNonTied, 0)}% | ${lb.C1.label} | ${L.f(ce.served.mean, 2)} [${L.f(ce.served.lo, 2)}, ${L.f(ce.served.hi, 2)}] | ${lb.C2.daysNeg}/${lb.C2.nDays} | ${lb.C2.label} | ${L.f(100 * cn.visRev60.rel, 1)}% | ${L.f(cn.served.mean, 2)} | ${fx(r.ARR_per1000)} | ${fx(r.ER_served)} | ${fx(r.ER_deadline)} | ${fx(r.ER_late)}`);
}
P(`robust C1: ${JSON.stringify(S.robust.C1)}`);
P(`robust C2: ${JSON.stringify(S.robust.C2)}`);
P(S.robust.C2_pi300_rule);
P(''); P('log:'); for (const l of LOG) P('  ' + l);
fs.writeFileSync(outTxt, LINES.join('\n') + '\n');
fs.writeFileSync(outJson, JSON.stringify(S, null, 1));
console.log(`wrote ${outTxt} and ${outJson}; rows ${all.length}; log ${LOG.length} lines`);
