'use strict';
const fs = require('fs');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const K = SP + '/wf21/n7';
const ref = JSON.parse(fs.readFileSync(SP + '/wf7/c-forecast/results/accuracy-index-2019.json', 'utf8'));
// own rebuild of old rule from rebuilt bins.csv
const F = {};
const monthMed = {};
for (const m of ['2019-08', '2019-09', '2019-10', '2019-11', '2019-12']) {
  const lines = fs.readFileSync(`${K}/cidx/work/yellow-${m}/bins.csv`, 'utf8').trim().split(/\r?\n/);
  const h = lines[0].split(','); const iD = h.indexOf('day'), iB = h.indexOf('bin'), iE = h.indexOf('e_med');
  const rs = lines.slice(1).map(l => l.split(',')).map(v => ({ d: v[iD], b: +v[iB], e: +v[iE] }));
  const es = rs.map(r => r.e).sort((a, b) => a - b); const med = es[Math.floor(es.length / 2)]; monthMed[m] = med;
  const days = new Set(rs.map(r => r.d)); const outside = [...days].filter(d => d.slice(0, 7) !== m);
  if (outside.length) console.log('month', m, 'has days outside', outside);
  for (const r of rs) (F[r.d] ||= new Array(96).fill(null))[r.b] = Math.exp(r.e - med);
}
let n = 0, bad = 0; const keysA = Object.keys(F).sort(), keysB = Object.keys(ref).sort();
for (const d of keysB) for (let b = 0; b < 96; b++) { n++; const x = F[d] ? F[d][b] : undefined, y = ref[d][b]; if (!(x === y || (y === null && (x === null || Number.isNaN(x))))) bad++; }
console.log(`old rule own rebuild: days ${keysA.length} vs ${keysB.length}; values ${n}; mismatches ${bad}`);
// compare wf7 original bins.csv e_med vs rebuilt e_med (CI-0) for each month
for (const m of ['2019-08', '2019-09', '2019-10', '2019-11', '2019-12']) {
  const rd = f => { const L = fs.readFileSync(f, 'utf8').trim().split(/\r?\n/); const h = L[0].split(','); const iE = h.indexOf('e_med'); return new Map(L.slice(1).map(l => { const v = l.split(','); return [v[0] + '|' + v[1], v[iE]]; })); };
  const a = rd(`${SP}/wf7/c-forecast/work/yellow-${m}/bins.csv`), b = rd(`${K}/cidx/work/yellow-${m}/bins.csv`);
  let mm = 0; for (const [k, v] of a) if (b.get(k) !== v) mm++; console.log(`CI-0 ${m}: rows ${a.size}/${b.size}, e_med string mismatches ${mm}`);
}
// causal file checks
const C = JSON.parse(fs.readFileSync(`${K}/cidx/results/index-causal-pertarget.json`, 'utf8'));
const O = JSON.parse(fs.readFileSync(`${K}/cidx/results/index-old-pertarget.json`, 'utf8'));
let viol = 0, nt = 0; const months = {}; let sameMonthSrc = 0, totalSrc = 0;
const bn = {}; for (const R of ['2019-08', '2019-09', '2019-11']) { const L = fs.readFileSync(`${K}/cidx/work/yellow-${R}/bins_next.csv`, 'utf8').trim().split(/\r?\n/).slice(1); for (const l of L) { const v = l.split(','); (bn[v[0]] ||= {})[+v[1]] = +v[3]; } }
let recompute = 0, recBad = 0;
for (const [d, T] of Object.entries(C.targets)) {
  nt++; const M = d.slice(0, 7); months[M] = (months[M] || 0) + 1;
  if (!(T.refEndExclusive <= M + '-01')) viol++;
  for (const s of Object.keys(T.F)) {
    if (!(s < d)) viol++;
    totalSrc++; if (s.slice(0, 7) === M) sameMonthSrc++;
    // recompute causal factor
    const R = T.ref;
    for (let b = 0; b < 96; b++) { let e; if (s.slice(0, 7) === R) { const x = F[s][b]; e = x === null ? null : Math.log(x) + monthMed[R]; } else { e = bn[s] && bn[s][b] !== undefined ? bn[s][b] : null; }
      const want = e === null ? null : Math.exp(e - monthMed[R]); const got = T.F[s][b]; recompute++;
      if (!(want === got || (want === null && (got === null || Number.isNaN(got)))) && Math.abs(want - got) > 1e-12 * Math.abs(want)) recBad++; }
  }
}
console.log(`causal targets ${nt} by month ${JSON.stringify(months)}; violations (stored day >= target or ref not before target month) ${viol}; stored source days ${totalSrc}, same-month ${sameMonthSrc}; recomputed causal factors ${recompute}, mismatches ${recBad}`);
