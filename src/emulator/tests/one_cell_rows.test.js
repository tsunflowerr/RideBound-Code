// End-to-end test of the driver on one F14 cell, 2019-09-19-w08-r1 (--x star, the frozen Shadow parameters, 62
// vehicles): run.js must reproduce the sealed digests of that cell rider by rider.
//   20 rows of expected/wf20__fullsys__results__rows.tsv: INS and G2 x forecast N and CL x SHF, SHHZ, SHRC, EOHZ35,
//      RCE35
//    8 rows of expected/wf21__n7__results__rows.tsv: INS and G2 x SHRC, SHHZ x the flat pad PAD
//      (data/flat_final.json) and the closed loop on the causal index CLc
// A row matches when served, visRev60, late60First, pDisp, breachDeadline60, sumTV5 and the rider fingerprint sig equal
// the digest (the fields confirm_digest.js compares). The cells, the network and the frozen parameters are not in the
// repository: set EMULATOR_DATA to the data root (the folder given to run.js as --data) and, for the CLc rows,
// EMULATOR_CAUSAL_INDEX to the file index-causal-pertarget.json. Without them these tests are skipped.
// Run: EMULATOR_DATA=<data root> EMULATOR_CAUSAL_INDEX=<index file> node --test src/emulator/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), os = require('os'), path = require('path');
const { spawnSync } = require('child_process');

const DIR = path.join(__dirname, '..');
const DATA = process.env.EMULATOR_DATA, CAUSAL_INDEX = process.env.EMULATOR_CAUSAL_INDEX;
const CELL = '2019-09-19-w08-r1';
const KEY = ['cell', 'config', 'mode', 'arm', 'flatRatio', 'fcIndex', 'nVeh', 'variant', 'g2PenaltyMs', 'fenceTolMs',
  'panel'];
const FIELDS = ['served', 'visRev60', 'late60First', 'pDisp', 'breachDeadline60', 'sumTV5', 'sig'];
const text = x => (x === undefined || x === null ? '' : String(x));   // a row field as the digest writes it

// The digest rows of the cell in expected/<file> that keep() selects, as objects of strings.
function digestRows(file, keep) {
  const [head, ...lines] = fs.readFileSync(path.join(DIR, 'expected', file), 'utf8').split(/\r?\n/).filter(Boolean);
  const names = head.split('\t');
  return lines.map(line => { const v = line.split('\t'); return Object.fromEntries(names.map((n, i) => [n, v[i]])); })
    .filter(o => o.cell === CELL && keep(o));
}

// The rows of one run.js call on the cell (into a temporary folder; at most 4 worker threads). A call takes seconds;
// the time limit turns a hang into a failure.
function runDriver(args) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'one-cell-'));
  try {
    const out = path.join(tmp, 'rows.ndjson');
    const p = spawnSync(process.execPath, [path.join(DIR, 'run.js'), '--data', DATA, '--set', 'confirm',
      '--cells', CELL, '--panel', 'F14', '--frozen', 'frozen/shadow-params.json', '--x', 'star', '--workers', '4',
      ...args, '--out', out], { encoding: 'utf8', timeout: 600_000 });
    assert.strictEqual(p.status, 0, (p.error ? String(p.error) + '\n' : '') + p.stdout + p.stderr);
    return fs.readFileSync(out, 'utf8').split('\n').filter(Boolean).map(line => JSON.parse(line));
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}

function assertRowsMatch(rows, digest) {
  const byKey = new Map(rows.map(r => [KEY.map(k => text(r[k])).join('|'), r]));
  assert.strictEqual(rows.length, digest.length, 'number of rows');
  for (const want of digest) {
    const key = KEY.map(k => want[k]).join('|'), got = byKey.get(key);
    assert.ok(got, 'no row for ' + key);
    assert.deepStrictEqual(FIELDS.map(f => text(got[f])), FIELDS.map(f => want[f]), key);
  }
}

const needData = DATA ? false : 'set EMULATOR_DATA to the data root (run.js --data) to run it';
const ARMS = ['--arms', 'SHF,SHHZ,SHRC,EOHZ35,RCE35'];

test('one cell, forecast N and CL: 20 rows equal the wf20 digest', { skip: needData }, () => {
  const rows = [...runDriver(['--fc', 'N', ...ARMS]), ...runDriver(['--fc', 'CL', ...ARMS])];
  assertRowsMatch(rows, digestRows('wf20__fullsys__results__rows.tsv', () => true));
});

test('one cell, flat pad PAD: 4 rows equal the wf21 digest', { skip: needData }, () => {
  const rows = runDriver(['--fc', 'PAD', '--flat', path.join(DIR, 'data', 'flat_final.json'), '--arms', 'SHRC,SHHZ']);
  assertRowsMatch(rows, digestRows('wf21__n7__results__rows.tsv', o => o.mode === 'PAD' && o.variant === ''));
});

test('one cell, causal closed loop CLc: 4 rows equal the wf21 digest',
  { skip: needData || (CAUSAL_INDEX ? false : 'set EMULATOR_CAUSAL_INDEX to index-causal-pertarget.json to run it') },
  () => {
    const rows = runDriver(['--fc', 'CLc', '--fc-index', path.resolve(CAUSAL_INDEX), '--arms', 'SHRC,SHHZ']);
    assertRowsMatch(rows, digestRows('wf21__n7__results__rows.tsv', o => o.mode === 'CLc'));
  });
