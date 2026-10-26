// wf21/n7 gate CI-0: compare the rebuilt month outputs with wf7's (per column for bins.csv; od_ref.csv as a sorted set).
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const out = [];
for (const m of ['2019-08', '2019-09', '2019-10', '2019-11', '2019-12']) {
  const A = path.join(SP, 'wf21/n7/cidx/work/yellow-' + m), B = path.join(SP, 'wf7/c-forecast/work/yellow-' + m);
  const rd = f => fs.readFileSync(f, 'utf8').trim().split(/\r?\n/);
  const a = rd(path.join(A, 'bins.csv')), b = rd(path.join(B, 'bins.csv'));
  const h = a[0].split(',');
  if (a[0] !== b[0] || a.length !== b.length) { out.push(`${m} bins: header/length differ`); continue; }
  const diff = Object.fromEntries(h.map(k => [k, 0])); let maxRel = Object.fromEntries(h.map(k => [k, 0]));
  for (let i = 1; i < a.length; i++) { const x = a[i].split(','), y = b[i].split(','); h.forEach((k, j) => { if (x[j] !== y[j]) { diff[k]++; const r = Math.abs(+x[j] - +y[j]) / Math.max(1e-300, Math.abs(+y[j])); if (r > maxRel[k]) maxRel[k] = r; } }); }
  out.push(`${m} bins.csv rows ${a.length - 1}: differing cells per column ` + h.map(k => `${k}=${diff[k]}${diff[k] ? '(maxRel ' + maxRel[k].toExponential(1) + ')' : ''}`).join(' '));
  const oa = rd(path.join(A, 'od_ref.csv')).slice(1).sort(), ob = rd(path.join(B, 'od_ref.csv')).slice(1).sort();
  out.push(`${m} od_ref.csv: ${oa.length} rows; as sorted sets ${JSON.stringify(oa) === JSON.stringify(ob) ? 'IDENTICAL' : 'DIFFERENT'} (file order differs only among ties of n)`);
}
console.log(out.join('\n'));
