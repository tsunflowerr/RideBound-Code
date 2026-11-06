// Tests for policy_curves.js. Run: node --test src/analysis/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const crypto = require('crypto'), fs = require('fs'), os = require('os'), path = require('path'), zlib = require('zlib');
const { spawnSync } = require('child_process');

const DIR = path.join(__dirname, '..');
const SCRIPT = path.join(DIR, 'policy_curves.js');
const data = file => path.join(DIR, 'data', file);
const INPUTS = { rows: 'wf17-r6-rows.ndjson.gz', a5: 'wf16-a5-alpha-rows.ndjson.gz', wf14: 'wf14-main-r0-rows.ndjson.gz',
  wf15: 'wf15-confirm-rows.ndjson.gz' };
const ARGS = Object.entries(INPUTS).flatMap(([k, file]) => ['--' + k, data(file)]);
const readRows = file => zlib.gunzipSync(fs.readFileSync(data(file))).toString('utf8').split('\n').filter(Boolean).map(JSON.parse);

// Runs the script with args plus --out <temporary folder>/pc; returns exit status, stdout, stderr and the two files.
function run(args, env = process.env) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'policy-curves-'));
  try {
    const prefix = path.join(tmp, 'pc');
    const p = spawnSync(process.execPath, [SCRIPT, ...args, '--out', prefix], { encoding: 'utf8', env });
    const read = ext => (fs.existsSync(prefix + ext) ? fs.readFileSync(prefix + ext) : null);
    return { status: p.status, stdout: p.stdout, stderr: p.stderr, txt: read('.txt'), json: read('.json') };
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}
// the first line that differs, so that a failure shows where the tables moved
function firstDifference(made, stored) {
  const a = made.split('\n'), b = stored.split('\n');
  const n = a.findIndex((line, i) => line !== b[i]);
  return n < 0 ? `${a.length} lines made, ${b.length} stored` : `line ${n + 1}:\n  made   ${a[n]}\n  stored ${b[n]}`;
}

let full;   // the full run (10,000 bootstrap resamples) takes several seconds; it is made once and shared
const fullRun = () => (full ??= run(ARGS));

test('the tables are rebuilt from the shipped rows: txt from line 2 and json byte for byte, stdout as stored', () => {
  const r = fullRun(), stored = name => fs.readFileSync(path.join(DIR, 'expected', name));
  assert.strictEqual(r.status, 0, r.stderr);
  const made = r.txt.toString('utf8').split('\n'), want = stored('policy-curves.txt').toString('utf8').split('\n');
  assert.ok(made.slice(1).join('\n') === want.slice(1).join('\n'), firstDifference(made.slice(1).join('\n'), want.slice(1).join('\n')));
  assert.ok(r.json.equals(stored('policy-curves.json')), 'policy-curves.json differs from the stored file');
  // line 1: the run time and the sha256 of the script
  const sha = crypto.createHash('sha256').update(fs.readFileSync(SCRIPT)).digest('hex');
  assert.match(made[0], /^wf17 R6 \(wf17-exploratory-post-audit\) - policy curves\. Generated \d{4}-\d\d-\d\dT[\d:.]+Z by policy_curves\.js \(sha256 [0-9a-f]{64}\)$/);
  assert.ok(made[0].endsWith(`(sha256 ${sha})`));
  // stdout: the label and gate lines (the /C9/ pattern also picks up the SHRC90 rows)
  assert.strictEqual(r.stdout, made.slice(1).filter(l => /C9|Elmi|SHRC vs SHHZ|deadline breach at|^gate /.test(l)).join('\n') + '\n');
  assert.strictEqual(r.stdout.split('\n').filter(l => /SHRC90/.test(l)).length, 26);
});

test('the report\'s "+20-91 %": SHHZ vs the EOHZ curve at matched service, reversals, every point on 30 days', () => {
  const j = JSON.parse(fullRun().json);
  const gaps = cfg => j.frontier[cfg].SHHZ_vs_EOHZ['visRev60 /1000'].map(q => {
    assert.strictEqual(q.nIncl, 30);
    return `${q.arm} ${(100 * q.relGap).toFixed(1)}`;
  });
  assert.deepStrictEqual(gaps('INS'), ['SHHZ50 19.8', 'SHHZ60 33.9', 'SHHZ70 41.7', 'SHHZ72 39.4', 'SHHZ80 65.0', 'SHHZ90 91.2']);
  assert.deepStrictEqual(gaps('G2'), ['SHHZ50 20.3', 'SHHZ60 26.0', 'SHHZ70 33.1', 'SHHZ71 34.4', 'SHHZ80 50.4', 'SHHZ90 78.6']);
  assert.deepStrictEqual([j.labels.INS.C9_reversals.label, j.labels.G2.C9_reversals.label, j.labels.C9_overall_G2], ['WEAKENED', 'HOLDS', 'HOLDS']);
});

test('gates: wf14 360/360, wf15 600/600, 3,240 rows; the a5 gate fails on the arm name alone', () => {
  const g = JSON.parse(fullRun().json).gates;
  assert.deepStrictEqual(g['EOHZ35/RCE35/U = wf14 main r0'], { compared: 360, equal: 360, missingRef: 0, pass: true, bad: [] });
  assert.deepStrictEqual(g['SHHZ/SHRC x*, U, EOHZ35, RCE35 = wf15 main'], { compared: 600, equal: 600, missingRef: 0, pass: true, bad: [] });
  assert.deepStrictEqual(g.rowCount, { rows: 3240, unique: 3240, expected: 3240, pass: true });
  const a5 = g['EOHZ/RCE alpha 10,20,50,100 = a5 V7-V10'];
  assert.deepStrictEqual([a5.compared, a5.equal, a5.missingRef, a5.pass], [960, 0, 0, false]);
  assert.ok(a5.bad.every(b => JSON.stringify(b[3]) === '["arm"]'));
  // all 960 rows, not only the 5 listed: with the arm name mapped (EOHZ + 10 = EOHZ10), every other field is equal
  const IGN = new Set(['part', 'set', 'engine', 'ms', 'msScore', 'label', 'variant', 'mode', 'alpha', 'check', 'nReq', 'arm']);
  const VA = { V7_a10: 10, V8_a20: 20, V9_a50: 50, V10_a100: 100 };
  const ref = new Map(readRows(INPUTS.a5).map(r => [[r.cell, r.config, r.arm + VA[r.variant]].join('|'), r]));
  let equal = 0;
  for (const m of readRows(INPUTS.rows).filter(r => /^(EOHZ|RCE)(10|20|50|100)$/.test(r.arm))) {
    const r = ref.get([m.cell, m.config, m.arm].join('|'));
    if (Object.keys(r).every(k => IGN.has(k) || JSON.stringify(r[k]) === JSON.stringify(m[k])) && r.nReq === m.requests) equal++;
  }
  assert.strictEqual(equal, 960);
});

test('the four inputs have the sha256 listed in data/MANIFEST.tsv', () => {
  const manifest = new Map(fs.readFileSync(data('MANIFEST.tsv'), 'utf8').split('\n').filter(Boolean).map(l => l.split('\t')));
  for (const file of Object.values(INPUTS)) {
    const sha = crypto.createHash('sha256').update(zlib.gunzipSync(fs.readFileSync(data(file)))).digest('hex');
    assert.strictEqual(sha, manifest.get(file), file);
  }
});

test('every input and --out are required; a missing file stops the script with nothing written', () => {
  let r = run(ARGS.slice(0, 6));   // --wf15 is missing
  assert.notStrictEqual(r.status, 0);
  assert.match(r.stderr, /usage: node policy_curves\.js --rows/);
  assert.deepStrictEqual([r.txt, r.json], [null, null]);
  r = run(['--rows', data('no-such-rows.ndjson.gz'), ...ARGS.slice(2)]);
  assert.notStrictEqual(r.status, 0);
  assert.match(r.stderr, /ENOENT/);
  assert.deepStrictEqual([r.txt, r.json], [null, null]);
});

test('the removed settings R6_NBOOT and R6_SKIP_GATES stop the script', () => {
  for (const name of ['R6_NBOOT', 'R6_SKIP_GATES']) {
    const r = run(ARGS, { ...process.env, [name]: '1' });
    assert.notStrictEqual(r.status, 0);
    assert.match(r.stderr, new RegExp(`${name} removed; see commit ebca912 \\(branch research/final-real-runs-2026-11-06\\)`));
    assert.deepStrictEqual([r.txt, r.json], [null, null]);
  }
});
