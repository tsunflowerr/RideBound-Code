// wf17 r1-identity-fence-pad analysis library (EXPLORATORY, POST-AUDIT). RUNPLAN 2.1: unit = day; paired differences; exact two-sided
// sign test (binomial 1/2, ties dropped); mean with SE and 95 % t-interval; median; relative change for counts; days in favour.
'use strict';
const fs = require('fs');

const readNd = f => (fs.existsSync(f) ? fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) : []);
const sum = a => a.reduce((s, x) => s + x, 0);
const mean = a => (a.length ? sum(a) / a.length : NaN);
const sd = a => { if (a.length < 2) return NaN; const m = mean(a); return Math.sqrt(a.reduce((s, x) => s + (x - m) ** 2, 0) / (a.length - 1)); };
const median = a => { if (!a.length) return NaN; const s = a.slice().sort((x, y) => x - y), n = s.length; return n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2; };

// exact two-sided sign test; |d| <= eps counts as a tie and is dropped
function signTest(d, eps = 1e-9) {
  let pos = 0, neg = 0, ties = 0;
  for (const x of d) { if (x > eps) pos++; else if (x < -eps) neg++; else ties++; }
  const n = pos + neg, k = Math.min(pos, neg);
  let p = 1;
  if (n > 0) { let c = 1, acc = 0; for (let i = 0; i <= k; i++) { if (i > 0) c = c * (n - i + 1) / i; acc += c; } p = Math.min(1, 2 * acc / 2 ** n); }
  return { pos, neg, ties, n, p };
}

// Student t quantile (two-sided 95 %) via the regularized incomplete beta function (Numerical Recipes betacf) and bisection
function gammaln(x) {
  const c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5];
  let y = x, t = x + 5.5; t -= (x + 0.5) * Math.log(t); let s = 1.000000000190015;
  for (const ci of c) s += ci / ++y;
  return -t + Math.log(2.5066282746310005 * s / x);
}
function betacf(a, b, x) {
  const MAXIT = 300, EPS = 3e-14, FPMIN = 1e-300;
  let qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap;
  if (Math.abs(d) < FPMIN) d = FPMIN; d = 1 / d; let h = d;
  for (let m = 1; m <= MAXIT; m++) {
    const m2 = 2 * m;
    let aa = m * (b - m) * x / ((qam + m2) * (a + m2));
    d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN; c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN; d = 1 / d; h *= d * c;
    aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
    d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN; c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN; d = 1 / d;
    const del = d * c; h *= del; if (Math.abs(del - 1) < EPS) break;
  }
  return h;
}
function ibeta(a, b, x) {
  if (x <= 0) return 0; if (x >= 1) return 1;
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

// paired contrast over days: a, b = Map(day -> value); better = 'lower' | 'higher' | null (benefit side for "days in favour")
function contrast(a, b, { better = 'lower', count = true, scale = 1 } = {}) {
  const days = [...a.keys()].filter(d => b.has(d) && Number.isFinite(a.get(d)) && Number.isFinite(b.get(d))).sort();
  const d = days.map(x => (a.get(x) - b.get(x)) * scale);
  const n = d.length, m = mean(d), s = sd(d), se = s / Math.sqrt(n), h = n > 1 ? t975(n - 1) * se : NaN;
  const st = signTest(d);
  const sa = sum(days.map(x => a.get(x))), sb = sum(days.map(x => b.get(x)));
  const favour = better === 'lower' ? st.neg : better === 'higher' ? st.pos : null;
  return { nDays: n, days, mean: m, se, lo: m - h, hi: m + h, median: median(d), ...st,
    rel: count && sb !== 0 ? 1 - sa / sb : null, meanA: sa / n, meanB: sb / n,
    favour, favourShareNonTied: favour === null || st.n === 0 ? null : favour / st.n, favourShareAll: favour === null || !n ? null : favour / n, deltas: Object.fromEntries(days.map((x, i) => [x, d[i]])) };
}
const f = (x, k = 2) => (x === null || x === undefined || Number.isNaN(x) ? 'NA' : typeof x === 'number' ? x.toFixed(k) : String(x));
const fp = p => (p === null || p === undefined || Number.isNaN(p) ? 'NA' : p < 1e-4 ? p.toExponential(1) : p.toPrecision(2));
function fmtC(c, { k = 2, unit = '' } = {}) {
  return `mean ${f(c.mean, k)}${unit} [${f(c.lo, k)}, ${f(c.hi, k)}] SE ${f(c.se, k)} med ${f(c.median, k)} | ${c.neg}-/${c.pos}+/${c.ties}= of ${c.nDays}, sign p ${fp(c.p)}` +
    (c.rel !== null && c.rel !== undefined ? ` | rel ${f(100 * c.rel, 1)}%` : '') + ` | A ${f(c.meanA, k)} B ${f(c.meanB, k)}`;
}
const sha256File = file => require('crypto').createHash('sha256').update(fs.readFileSync(file)).digest('hex');

module.exports = { readNd, sum, mean, sd, median, signTest, t975, contrast, f, fp, fmtC, sha256File, tTwoSidedP };
