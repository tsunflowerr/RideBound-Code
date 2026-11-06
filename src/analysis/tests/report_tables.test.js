// Tests for report_tables.js. Run: node --test src/analysis/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const path = require('path');
const RT = require('../report_tables.js');

const DIR = path.join(__dirname, '..');
let tables;   // the full rebuild takes a few seconds; it is made once and shared
const allTables = () => (tables ??= RT.computeTables(RT.loadData(path.join(DIR, 'data'))));

test('pyFixed rounds like Python: an exact tie goes to the even digit', () => {
  assert.strictEqual(RT.pyFixed(343.125, 2), '343.12');   // toFixed would give 343.13
  assert.strictEqual(RT.pyFixed(-343.125, 2), '-343.12');
  assert.strictEqual(RT.pyFixed(0.375, 2), '0.38');
  assert.strictEqual(RT.pyFixed(2.675, 2), '2.67');       // the double is just below 2.675
  assert.strictEqual(RT.pyFixed(0.0625, 3), '0.062');
  assert.strictEqual(RT.pyFixed(-0.001, 2), '-0.00');
});

test('pyRepr prints like Python str(float)', () => {
  assert.strictEqual(RT.pyRepr(1), '1.0');
  assert.strictEqual(RT.pyRepr(0.79052734375), '0.79052734375');
  assert.strictEqual(RT.pyRepr(0.0001), '0.0001');
  assert.strictEqual(RT.pyRepr(3.0517578125e-5), '3.0517578125e-05');
  assert.strictEqual(RT.pyRepr(1e16), '1e+16');
});

test('signTest is the exact two-sided binomial tail', () => {
  assert.strictEqual(RT.signTest(0, 0), 1);
  assert.strictEqual(RT.signTest(8, 6), 2 * 6476 / 2 ** 14);           // 6476 = C(14,0) + ... + C(14,6)
  assert.strictEqual(RT.signTest(2, 28), 2 * (1 + 30 + 435) / 2 ** 30);
  assert.strictEqual(RT.signTest(15, 15), 1);
});

test('T975 holds the 0.975 quantiles of Student t', () => {
  // With x = sqrt(df) tan(u), P(0 < T < t) is proportional to the integral of cos(u)^(df-1) from 0 to atan(t/sqrt(df)).
  const simpson = (f, a, b, n = 20000) => {
    const h = (b - a) / n;
    let s = f(a) + f(b);
    for (let i = 1; i < n; i++) s += (i % 2 ? 4 : 2) * f(a + i * h);
    return s * h / 3;
  };
  for (const [df, t] of Object.entries(RT.T975)) {
    const g = u => Math.cos(u) ** (df - 1);
    const share = simpson(g, 0, Math.atan(t / Math.sqrt(df))) / simpson(g, 0, Math.PI / 2);
    assert.ok(Math.abs(share - 0.95) < 1e-12, `df ${df}: P(|T| < t) = ${share}`);
  }
});

test('summary gives the mean, the t interval, the median and the sign test', () => {
  const d = [5, -1, 3, 2, 0, 4, 7, 1, 6, 2, 3, -2, 8, 1];   // 14 days
  const s = RT.summary(d);
  const mean = 39 / 14, se = Math.sqrt(d.reduce((a, x) => a + (x - mean) ** 2, 0) / 13) / Math.sqrt(14);
  assert.strictEqual(s.n, 14);
  assert.strictEqual(s.mean, mean);
  assert.strictEqual(s.median, 2.5);
  assert.deepStrictEqual([s.nPlus, s.nMinus, s.ties], [11, 2, 1]);
  assert.strictEqual(s.lo, mean - RT.T975[13] * se);
  assert.strictEqual(s.p, RT.signTest(11, 2));
  assert.strictEqual(RT.summary([1, NaN, 2]), null);         // a NaN marks a value the data cannot give
  assert.throws(() => RT.summary([1, 2, 3]), /no t quantile/);
});

test('every stored value is rebuilt from the shipped data', () => {
  const r = RT.checkTables(allTables(), path.join(DIR, 'expected'));
  assert.ok(r.ok, r.report.join('\n'));
  assert.strictEqual(r.values, 9570);
  assert.deepStrictEqual(Object.values(r.open), [397, 1086, 2732]);
  for (const name of ['synth-real-30d.md', 'promise.md']) assert.ok(r.report.some(l => l.startsWith(name) && l.endsWith('byte-identical')), name);
});

test('the check reports a changed value, a changed line and a missing value', () => {
  const t = allTables(), dir = path.join(DIR, 'expected');
  const copy = () => JSON.parse(JSON.stringify(t['synth-real-30d.json']));
  const changed = copy(), missing = copy();
  changed.results.F14.real.day.served.diff.mean *= 1 + 1e-11;
  assert.ok(!RT.checkTables({ 'synth-real-30d.json': changed }, dir).ok);
  delete missing.results.F14.real.day.served.diff.p;
  const r = RT.checkTables({ 'synth-real-30d.json': missing }, dir);
  assert.ok(!r.ok);
  assert.match(r.report.join('\n'), /diff\.p: missing/);
  assert.ok(!RT.checkTables({ 'promise.md': t['promise.md'].replace('694,69', '694,70') }, dir).ok);
  assert.ok(!RT.checkTables({ 'd1-gap.txt': t['d1-gap.txt'].replace('p=0.79052734375', 'p=0.7905273437') }, dir).ok);
  assert.ok(RT.checkTables({ 'd1-gap.txt': t['d1-gap.txt'] }, dir).ok);
});

test('sameLine: "—" stands for an open value, p may differ only by rounding', () => {
  assert.ok(RT.sameLine('| a | — | [1,00; —] (—/—) |', '| a | -13,02 | [1,00; 2,00] (3/4) |'));
  assert.ok(!RT.sameLine('| a | — | [1,00; —] |', '| a | -13,02 | [1,01; 2,00] |'));
  assert.ok(RT.sameLine('  x (8/6) p=0.79052734375', '  x (8/6) p=0.7905273437500007'));
  assert.ok(!RT.sameLine('  x (8/6) p=0.79', '  x (8/6) p=0.7905273437500007'));
});
