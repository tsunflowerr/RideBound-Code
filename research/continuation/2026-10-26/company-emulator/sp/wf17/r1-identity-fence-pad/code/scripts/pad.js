// A3-style pad control (thesis forecast_family_a.py): pad promise = p0 + k (p0 - t0), k from promises only (no outcomes).
//  padW  : k per window, pooled over all 18 days (thesis k_w)
//  padC  : k per cell (same total extension as the display layer in that cell; isolates SHAPE from AMOUNT)
// Day = unit (w08 + w17 pooled). Paired per-day differences.
'use strict';
const fs = require('fs');
const X = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const cells = Object.keys(X.cells).sort();
const days = [...new Set(cells.map(c => c.slice(0, 10)))].sort();
const T975 = { 17: 2.110, 15: 2.131 };
const binom = (pos, neg) => { const n = pos + neg; const k = Math.min(pos, neg); let p = 0, c = 1; for (let i = 0; i <= n; i++) { if (i > 0) c = c * (n - i + 1) / i; if (i <= k) p += c; } return Math.min(1, 2 * p / 2 ** n); };
const out = [];
for (const arm of ['RC120', 'U']) {
  const key = arm + '|N';
  const kW = {};
  for (const w of ['w08', 'w17']) { let num = 0, den = 0; for (const c of cells.filter(c => c.endsWith(w))) for (const [t0, p0, D0] of X.cells[c][key].per) { num += D0 - p0; den += p0 - t0; } kW[w] = num / den; }
  const agg = {}; // day -> {n, naive, dl, padW, padC, Enaive, Edl, EpadW, EpadC}
  for (const c of cells) {
    const per = X.cells[c][key].per, d = c.slice(0, 10), w = c.slice(11);
    let num = 0, den = 0; for (const [t0, p0, D0] of per) { num += D0 - p0; den += p0 - t0; }
    const kC = num / den;
    const a = (agg[d] ||= { n: 0, naive: 0, dl: 0, padW: 0, padC: 0, Enaive: 0, Edl: 0, EpadW: 0, EpadC: 0 });
    for (const [t0, p0, D0, A] of per) {
      const pw = p0 + kW[w] * (p0 - t0), pc = p0 + kC * (p0 - t0);
      a.n++;
      a.naive += A - p0 > 60000; a.dl += A - D0 > 60000; a.padW += A - pw > 60000; a.padC += A - pc > 60000;
      a.Enaive += A - p0 < -60000; a.Edl += A - D0 < -60000; a.EpadW += A - pw < -60000; a.EpadC += A - pc < -60000;
    }
  }
  const tot = k => days.reduce((s, d) => s + agg[d][k], 0) / days.reduce((s, d) => s + agg[d].n, 0);
  out.push(`${arm}: k_w08 ${(100 * kW.w08).toFixed(2)}%  k_w17 ${(100 * kW.w17).toFixed(2)}% of remaining promise time`);
  out.push(`  pooled Y: naive ${(100 * tot('naive')).toFixed(2)}%  display ${(100 * tot('dl')).toFixed(2)}%  pad(k_w) ${(100 * tot('padW')).toFixed(2)}%  pad(k_cell) ${(100 * tot('padC')).toFixed(2)}%`);
  out.push(`  pooled E: naive ${(100 * tot('Enaive')).toFixed(2)}%  display ${(100 * tot('Edl')).toFixed(2)}%  pad(k_w) ${(100 * tot('EpadW')).toFixed(2)}%  pad(k_cell) ${(100 * tot('EpadC')).toFixed(2)}%`);
  for (const [a, b, lab] of [['dl', 'naive', 'display - naive'], ['padW', 'naive', 'pad(k_w) - naive'], ['dl', 'padW', 'display - pad(k_w)'], ['dl', 'padC', 'display - pad(k_cell)']]) {
    const diffs = days.map(d => (agg[d][a] - agg[d][b]) / agg[d].n);
    const n = diffs.length, mu = diffs.reduce((s, x) => s + x, 0) / n, sd = Math.sqrt(diffs.reduce((s, x) => s + (x - mu) ** 2, 0) / (n - 1)), h = T975[n - 1] * sd / Math.sqrt(n);
    const pos = diffs.filter(x => x > 1e-12).length, neg = diffs.filter(x => x < -1e-12).length;
    out.push(`  dY ${lab.padEnd(22)} ${(100 * mu).toFixed(2)} pp [${(100 * (mu - h)).toFixed(2)}, ${(100 * (mu + h)).toFixed(2)}]  ${pos}+/${neg}-  sign p ${binom(pos, neg).toPrecision(2)}`);
  }
  // per window
  for (const w of ['w08', 'w17']) {
    let n = 0, y = { naive: 0, dl: 0, padW: 0 };
    for (const c of cells.filter(c => c.endsWith(w))) for (const [t0, p0, D0, A] of X.cells[c][key].per) { n++; y.naive += A - p0 > 60000; y.dl += A - D0 > 60000; y.padW += A - (p0 + kW[w] * (p0 - t0)) > 60000; }
    out.push(`  ${w}: Y naive ${(100 * y.naive / n).toFixed(2)}%  display ${(100 * y.dl / n).toFixed(2)}%  pad(k_w) ${(100 * y.padW / n).toFixed(2)}%`);
  }
}
console.log(out.join('\n'));
