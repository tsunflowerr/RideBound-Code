// Recompute the claimed key numbers of wf21/n7 from results/rows.ndjson (independent code).
'use strict';
const L = require('./v7lib.js');
const K = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf21/n7';
const rows = L.readNd(K + '/results/rows.ndjson');
console.log('rows', rows.length);
const T = L.dayTables(rows, r => r.variant == null ? r.mode : r.variant === 'PADc' ? 'PADc' : null);
console.log('tables', [...T.keys()].sort().join(' '), 'days per table', [...T.values()].map(t => t.size).join(','));
const S = (c, m, a) => `${c}|${m}|${a}`;
const out = [];
const P = s => { out.push(s); console.log(s); };
for (const [A, B] of [['CL', 'PAD'], ['CLc', 'PADc'], ['CLc', 'PAD'], ['CLc', 'CL'], ['PAD', 'N'], ['CL', 'N'], ['PADc', 'PAD']]) {
  for (const arm of ['SHRC', 'SHHZ']) for (const c of ['G2', 'INS']) {
    P(`\n## ${arm} ${c}: ${A} - ${B}`);
    for (const met of ['late', 'early', 'rev', 'tv5', 'dead', 'glate', 'served', 'pchg', 'pl', 'gshare']) for (const sc of ['day', 'w08', 'w17']) {
      const po = L.summ(L.diffs(T, S(c, A, arm), S(c, B, arm), met, sc).d);
      const f14 = L.summ(L.diffs(T, S(c, A, arm), S(c, B, arm), met, sc, 'F14').d);
      const f16 = L.summ(L.diffs(T, S(c, A, arm), S(c, B, arm), met, sc, 'F16').d);
      P(`${(met + ' ' + sc).padEnd(12)} pooled ${L.fs_(po)} | F14 ${L.f(f14.mean)} (${f14.nm}-/${f14.np}+ p ${f14.p.toFixed(3)}) | F16 ${L.f(f16.mean)} (${f16.nm}-/${f16.np}+ p ${f16.p.toFixed(3)})`);
    }
  }
}
P('\n## Levels pooled (day|w08|w17)');
for (const c of ['G2', 'INS']) for (const arm of ['SHRC', 'SHHZ']) for (const m of ['N', 'PAD', 'CL', 'CLc', 'PADc']) {
  P(`${c} ${arm} ${m.padEnd(4)} ` + ['late', 'pl', 'served', 'rev', 'tv5', 'early'].map(met => met + ' ' + ['day', 'w08', 'w17'].map(sc => L.level(T, S(c, m, arm), met, sc).toFixed(1)).join('|')).join('  '));
}
P('\n## Decomposition SHRC_X - SHHZ_N = RuleC + length + info');
for (const c of ['G2', 'INS']) for (const [X, pad] of [['CL', 'PAD'], ['CLc', 'PADc']]) for (const met of ['late', 'rev', 'served', 'tv5']) for (const sc of ['day', 'w08', 'w17']) {
  const tot = L.summ(L.diffs(T, S(c, X, 'SHRC'), S(c, 'N', 'SHHZ'), met, sc).d).mean;
  const rc = L.summ(L.diffs(T, S(c, 'N', 'SHRC'), S(c, 'N', 'SHHZ'), met, sc).d).mean;
  const ln = L.summ(L.diffs(T, S(c, pad, 'SHRC'), S(c, 'N', 'SHRC'), met, sc).d).mean;
  const inf = L.summ(L.diffs(T, S(c, X, 'SHRC'), S(c, pad, 'SHRC'), met, sc).d).mean;
  P(`${c} ${X} ${met} ${sc}: total ${L.f(tot)} = RuleC ${L.f(rc)} + length ${L.f(ln)} + info ${L.f(inf)} (resid ${(tot - rc - ln - inf).toExponential(1)}); length/(length+info) ${(ln / (ln + inf)).toFixed(3)}; length/total ${(ln / tot).toFixed(3)}`);
}
require('fs').writeFileSync(__dirname + '/v7main_out.txt', out.join('\n') + '\n');
