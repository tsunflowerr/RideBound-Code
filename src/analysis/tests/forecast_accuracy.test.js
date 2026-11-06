// Tests for forecast_accuracy.js. Run: node --test src/analysis/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), os = require('os'), path = require('path');
const { spawnSync } = require('child_process');

const DIR = path.join(__dirname, '..');
const DATA = path.join(DIR, 'data');
const OUTPUTS = ['.txt', '-index-2019.json', '-rows.json', '-summary.json'];
const INPUTS = [...['2018-11', '2019-08', '2019-09', '2019-10', '2019-11', '2019-12'].map(m => `tlc-yellow-bins-${m}.csv.gz`),
  ...[12, 13, 14, 15, 16].map(d => `fleetpy-tt-factors-2018-11-${d}.csv.gz`)];

// Runs the script with args plus --out <temporary folder>/forecast-accuracy; returns exit status, stdout, stderr and the
// four output files (null when not written). leaveOut, when given, names an input that is not copied: the script then
// reads a temporary data folder holding the other inputs.
function run(args, leaveOut) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'forecast-accuracy-'));
  try {
    if (leaveOut !== undefined) {
      fs.mkdirSync(path.join(tmp, 'data'));
      for (const f of INPUTS) if (f !== leaveOut) fs.copyFileSync(path.join(DATA, f), path.join(tmp, 'data', f));
      args = ['--data', path.join(tmp, 'data'), ...args];
    }
    const prefix = path.join(tmp, 'forecast-accuracy');
    const p = spawnSync(process.execPath, [path.join(DIR, 'forecast_accuracy.js'), ...args, '--out', prefix], { encoding: 'utf8' });
    const files = {};
    for (const x of OUTPUTS) files[x] = fs.existsSync(prefix + x) ? fs.readFileSync(prefix + x) : null;
    return { status: p.status, stdout: p.stdout, stderr: p.stderr, files };
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}
// the first line that differs, so that a failure shows where the tables moved
function firstDifference(made, stored) {
  const a = made.toString('utf8').split('\n'), b = stored.toString('utf8').split('\n');
  const n = a.findIndex((line, i) => line !== b[i]);
  return n < 0 ? `${a.length} lines made, ${b.length} stored` : `line ${n + 1}:\n  made   ${a[n]}\n  stored ${b[n]}`;
}

let full;   // made once and shared
const fullRun = () => (full ??= run(['--data', DATA]));
const made = x => JSON.parse(fullRun().files[x]);

test('the four outputs are rebuilt byte for byte from the shipped data, and the tables are printed', () => {
  const r = fullRun();
  assert.strictEqual(r.status, 0, r.stderr);
  for (const x of OUTPUTS) {
    const stored = fs.readFileSync(path.join(DIR, 'expected', 'forecast-accuracy' + x));
    assert.ok(r.files[x], x + ' not written');
    assert.ok(r.files[x].equals(stored), x + ': ' + firstDifference(r.files[x], stored));
  }
  assert.strictEqual(r.stdout, r.files['.txt'].toString('utf8'));
});

test('the report\'s claim: forecast error 2.6-3.1 % (DEV, prev4) against 4.98 % (w08) and 9.34 % (w17) held flat', () => {
  const S = made('-summary.json'), pct = x => (100 * x).toFixed(2);
  const dev = w => S[`${w}|DEV Sep-Oct school days|prev4`];
  assert.deepStrictEqual([pct(dev('w08').mae15), pct(dev('w08').naive15)], ['2.64', '4.98']);
  assert.deepStrictEqual([pct(dev('w17').mae15), pct(dev('w17').naive15)], ['3.09', '9.34']);
  assert.deepStrictEqual([dev('w08').days, dev('w08').better15, dev('w17').better15], [27, 25, 27]);
  // over all groups, prev4: MAE15 w08 2.10-3.31, w17 2.36-3.10; held flat w17 8.38-9.80
  const range = (w, field) => {
    const v = Object.keys(S).filter(k => k.startsWith(w) && k.endsWith('|prev4')).map(k => S[k][field]);
    return [pct(Math.min(...v)), pct(Math.max(...v))];
  };
  assert.deepStrictEqual([range('w08', 'mae15'), range('w17', 'mae15'), range('w17', 'naive15')],
    [['2.10', '3.31'], ['2.36', '3.10'], ['8.38', '9.80']]);
});

test('the index is normalised per month, and the sources follow the calendar', () => {
  const F = made('-index-2019.json');
  assert.strictEqual(Object.keys(F).length, 153);   // Aug-Dec 2019
  for (const m of ['2019-08', '2019-09', '2019-10', '2019-11', '2019-12']) {
    const v = Object.keys(F).filter(d => d.startsWith(m)).flatMap(d => F[d]).sort((a, b) => a - b);
    assert.strictEqual(v[Math.floor(v.length / 2)], 1, m);   // exp(median - median)
  }
  const rows = made('-rows.json'), w08 = day => rows.find(r => r.day === day && r.win === 'w08');
  assert.strictEqual(rows.length, 174);   // 87 weekdays Sep-Dec x 2 windows
  // prev4 sources must be weekdays from 2019-09-05 on: 09-12 has 1 (09-05), 09-19 has 2, the first day with a forecast
  assert.strictEqual(w08('2019-09-12').prev4, null);
  assert.strictEqual(rows.find(r => r.prev4).day, '2019-09-19');
  assert.strictEqual(w08('2019-09-19').prev4.nsrc, 2);
  assert.strictEqual(w08('2019-10-07').prev4.nsrc, 3);   // 09-30 is excluded
  assert.strictEqual(w08('2019-10-24').prev4.nsrc, 4);   // 10-17, 10-10, 10-03, 09-26
  assert.strictEqual(w08('2019-10-24').prev8.nsrc, 7);   // the 8th week back, 08-29, is before 09-05
  // an excluded day is never a source but still gets a forecast as a target
  assert.deepStrictEqual([w08('2019-10-09').school, w08('2019-10-09').prev4.nsrc], [false, 4]);
});

test('a missing input stops the script before it writes the outputs', () => {
  for (const f of ['fleetpy-tt-factors-2018-11-14.csv.gz', 'tlc-yellow-bins-2019-10.csv.gz']) {
    const r = run([], f);
    assert.notStrictEqual(r.status, 0, f);
    assert.match(r.stderr, new RegExp('ENOENT.*' + f.replace(/\./g, '\\.')));
    assert.deepStrictEqual(Object.values(r.files), [null, null, null, null], f);
  }
});

test('--data and --out are required', () => {
  const r = run([]);   // run() adds --out; --data is missing
  assert.notStrictEqual(r.status, 0);
  assert.match(r.stderr, /usage: node forecast_accuracy\.js --data <dir> --out <prefix>/);
  assert.deepStrictEqual(Object.values(r.files), [null, null, null, null]);
});
