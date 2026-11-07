// Tests for forecast_tables.js. Run: node --test src/analysis/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), os = require('os'), path = require('path');
const { spawnSync } = require('child_process');

const DIR = path.join(__dirname, '..');
const data = file => path.join(DIR, 'data', file);
const ROWS = ['--rows', data('wf21-n7-rows.ndjson.gz'), '--panels', data('panels.json')];
const FLAT = ['--flat', data('flat_final.json'), '--flatc', data('flatc_final.json')];

// Runs the script with args plus --out <temporary folder>/tables; returns exit status, stdout, stderr and the two files.
function run(args) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'forecast-tables-'));
  try {
    const prefix = path.join(tmp, 'tables');
    const p = spawnSync(process.execPath, [path.join(DIR, 'forecast_tables.js'), ...args, '--out', prefix], { encoding: 'utf8' });
    const read = ext => (fs.existsSync(prefix + ext) ? fs.readFileSync(prefix + ext) : null);
    return { status: p.status, stdout: p.stdout, stderr: p.stderr, txt: read('.txt'), json: read('.json') };
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true, maxRetries: 10 });
  }
}
// the first line that differs, so that a failure shows where the tables moved
function firstDifference(made, stored) {
  const a = made.toString('utf8').split('\n'), b = stored.toString('utf8').split('\n');
  const n = a.findIndex((line, i) => line !== b[i]);
  return n < 0 ? `${a.length} lines made, ${b.length} stored` : `line ${n + 1}:\n  made   ${a[n]}\n  stored ${b[n]}`;
}

let full;   // the full run takes a few seconds; it is made once and shared
const fullRun = () => (full ??= run([...ROWS, ...FLAT]));

test('tables.txt and tables.json are rebuilt byte for byte from the shipped rows', () => {
  const r = fullRun(), stored = name => fs.readFileSync(path.join(DIR, 'expected', name));
  assert.strictEqual(r.status, 0, r.stderr);
  assert.ok(r.txt.equals(stored('forecast-tables.txt')), firstDifference(r.txt, stored('forecast-tables.txt')));
  assert.ok(r.json.equals(stored('forecast-tables.json')), 'forecast-tables.json differs from the stored file');
  assert.match(r.stdout, /^written .*tables\.txt \(4033 lines\) and \.json\n$/);
});

test('the report\'s reversal split: -27.46 per day = Rule C -14.63 + length -6.33 + information -6.49', () => {
  const d = JSON.parse(fullRun().json).decomp['G2: SHRC_CL - SHHZ_N'];
  const mean = c => c.pooled.R['visRev60|day'].mean;
  const parts = Object.values(d.parts).map(mean);
  assert.deepStrictEqual(Object.keys(d.parts), ['RuleC(SHRC_N-SHHZ_N)', 'length(SHRC_PAD-SHRC_N)', 'info(SHRC_CL-SHRC_PAD)']);
  assert.deepStrictEqual([mean(d.total), ...parts].map(x => x.toFixed(2)), ['-27.46', '-14.63', '-6.33', '-6.49']);
  assert.ok(Math.abs(parts.reduce((a, b) => a + b, 0) - mean(d.total)) < 1e-9);   // additive by day
});

test('--flat and --flatc only add the calibrated ratios to the header', () => {
  const r = run(ROWS), lines = fullRun().txt.toString('utf8').split('\n');
  assert.strictEqual(r.status, 0, r.stderr);
  assert.match(lines[2], /^# PAD flat ratios: \{"INS\|w08\|SHRC":1\.03253,/);
  assert.match(lines[3], /^# PADc flat ratios: \{"INS\|w08\|SHRC":1\.031702,/);
  assert.strictEqual(r.txt.toString('utf8'), [...lines.slice(0, 2), ...lines.slice(4)].join('\n'));
  assert.ok(r.json.equals(fullRun().json));
});

test('--rows, --panels and --out are required', () => {
  const r = run(['--rows', data('wf21-n7-rows.ndjson.gz')]);   // run() adds --out; --panels is missing
  assert.notStrictEqual(r.status, 0);
  assert.match(r.stderr, /usage: node forecast_tables\.js --rows/);
  assert.strictEqual(r.txt, null);
});
