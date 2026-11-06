// Tests for sixty_day_tables.js. Run: node --test src/analysis/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const crypto = require('crypto'), fs = require('fs'), os = require('os'), path = require('path'), zlib = require('zlib');
const { spawnSync } = require('child_process');

const DIR = path.join(__dirname, '..');
const ROWS = path.join(DIR, 'data', 'wf15-confirm-rows.ndjson.gz');

// Runs the script with args plus --out <temporary folder>/out.txt; returns exit status, stdout, stderr and the file.
// rowsText, when given, is written to the temporary folder and passed as --rows.
function run(args, rowsText) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'sixty-day-tables-'));
  try {
    const out = path.join(tmp, 'out.txt');
    if (rowsText !== undefined) {
      fs.writeFileSync(path.join(tmp, 'rows.ndjson'), rowsText);
      args = ['--rows', path.join(tmp, 'rows.ndjson'), ...args];
    }
    const p = spawnSync(process.execPath, [path.join(DIR, 'sixty_day_tables.js'), ...args, '--out', out], { encoding: 'utf8' });
    return { status: p.status, stdout: p.stdout, stderr: p.stderr, text: fs.existsSync(out) ? fs.readFileSync(out) : null };
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true });
  }
}
// the first line that differs, so that a failure shows where the output moved
function firstDifference(made, stored) {
  const a = made.split('\n'), b = stored.split('\n');
  const n = b.findIndex((line, i) => line !== a[i]);
  return n < 0 ? 'no line differs' : `line ${n + 1}:\n  made   ${a[n]}\n  stored ${b[n]}`;
}
// the lines that follow the heading line starting with `heading`, up to the next blank line
function section(text, heading) {
  const lines = text.split('\n'), h = lines.findIndex(l => l.startsWith(heading));
  assert.ok(h >= 0, 'heading not found: ' + heading);
  const end = lines.indexOf('', h);
  return lines.slice(h + 1, end < 0 ? lines.length : end);
}

let full;   // made once and shared
const fullRun = () => (full ??= run(['--rows', ROWS]));
const fullText = () => fullRun().text.toString('utf8');

test('TOTALS, SECONDARY and S16 are the stored study report\'s lines 30-118 byte for byte, and the output is printed', () => {
  const r = fullRun(), stored = fs.readFileSync(path.join(DIR, 'expected', 'confirm-shadow-report.txt'), 'utf8');
  assert.strictEqual(r.status, 0, r.stderr);
  const text = r.text.toString('utf8');
  assert.ok(text.startsWith(stored), firstDifference(text, stored));
  assert.ok(text.slice(stored.length).startsWith('\nREPORT TABLES'));
  // the TOTALS block up to the SECONDARY heading (lines 30-46 of the stored report)
  const lines = text.split('\n'), from = lines.findIndex(l => l.startsWith('TOTALS ')), to = lines.findIndex(l => l.startsWith('SECONDARY '));
  const block = lines.slice(from, to + 1).join('\n') + '\n';
  assert.strictEqual(crypto.createHash('sha256').update(block).digest('hex'), '02500a2488895b1d18cad42bed5191d7b26c2ec9416e35d3cb49305ccabc4288');
  assert.strictEqual(r.stdout, text);
});

test('the report tables: 82 of 84 cells equal the system report; the other 2 are its twice-rounded burden cells', () => {
  // the system report's two §6 tables: served, % postponed, burden, reversals, TV5, late > 60 s
  const REPORT = {
    INS: { U: [795.6, 45.8, 8.8, 42.4, 3910, 373.8], EO35: [723.2, 31.5, 3.8, 21.6, 1390, 235.2], EOHZ35: [725.2, 31.6, 3.8, 21.7, 1397, 236.1],
      RCE35: [726.7, 31.8, 3.8, 19.9, 1403, 235.0], SHF72: [685.5, 21.3, 11.7, 30.3, 2225, 166.4], SHHZ72: [723.9, 20.7, 12.6, 31.4, 2459, 177.1],
      SHRC72: [719.8, 20.6, 12.8, 30.9, 2460, 177.6] },
    G2: { U: [804.5, 47.1, 9.1, 88.2, 4558, 372.7], EO35: [730.6, 32.5, 4.0, 39.6, 1634, 234.6], EOHZ35: [732.1, 32.5, 4.0, 39.8, 1634, 234.6],
      RCE35: [729.2, 32.1, 3.8, 23.1, 1436, 235.2], SHF71: [694.4, 22.5, 12.0, 50.0, 2627, 168.3], SHHZ71: [732.8, 22.0, 12.6, 53.6, 2826, 179.2],
      SHRC71: [723.3, 21.4, 12.5, 36.9, 2550, 181.9] },
  };
  const rows = section(fullText(), 'REPORT TABLES');
  let cells = 0; const differ = [];
  for (const cfg of ['INS', 'G2']) {
    const h = rows.findIndex(l => l.startsWith('-- ' + cfg + ' '));
    for (const [arm, want] of Object.entries(REPORT[cfg])) {
      const cols = rows.slice(h + 1, h + 8).find(l => l.split(/\s+/)[0] === arm).split(/\s+/).slice(1).map(Number);
      want.forEach((x, i) => { cells++; if (cols[i] !== x) differ.push(`${cfg} ${arm} column ${i + 1}: ${cols[i]} (report ${x})`); });
    }
  }
  assert.strictEqual(cells, 84);
  assert.deepStrictEqual(differ, ['G2 U column 3: 9 (report 9.1)', 'G2 SHF71 column 3: 11.9 (report 12)']);
});

test('the §6 text: Rule C against the fences, the Shadow pairs, the ledger and the guaranteed riders', () => {
  const text = fullText();
  // REPORT PAIRS line of "A - B" and metric m -> [level A, level B, A vs B %, paired mean, days A<B, days A>B]
  const pairs = section(text, 'REPORT PAIRS');
  const pair = (ab, m) => {
    const l = pairs.find(x => x.startsWith(ab + ' ') && x.split(/\s+/)[3] === m);
    assert.ok(l, ab + ' ' + m);
    const g = l.match(/(-?[\d.]+) vs +(-?[\d.]+) \((-?[\d.]+) %\) \| paired +(-?[\d.]+), days (\d+)\/(\d+)$/);
    return g.slice(1).map(Number);
  };
  // reversals -42 %, TV5 -12 %, served -2.8 (G2; the comparator decides the served number)
  assert.deepStrictEqual(pair('G2|RCE35 - EO35', 'visRev60'), [23.078, 39.606, -41.73, -16.528, 60, 0]);
  assert.deepStrictEqual(pair('G2|RCE35 - EOHZ35', 'visRev60').slice(1, 3), [39.761, -41.96]);
  assert.deepStrictEqual(pair('G2|RCE35 - EO35', 'TV5min').slice(0, 3), [1436.05, 1633.989, -12.11]);
  assert.deepStrictEqual(pair('G2|RCE35 - EOHZ35', 'TV5min').slice(1, 3), [1633.512, -12.09]);
  assert.deepStrictEqual(pair('G2|RCE35 - EOHZ35', 'served').slice(3), [-2.822, 37, 21]);
  assert.deepStrictEqual(pair('G2|RCE35 - EO35', 'served').slice(3), [-1.328, 31, 27]);
  // P(change) 31.5 -> 20.6, burden 3.8 -> 12.6, 53-69 fewer late riders per day (every day)
  assert.deepStrictEqual(pair('INS|SHRC72 - EO35', 'pDispPct').slice(0, 2), [20.567, 31.454]);
  assert.deepStrictEqual(pair('INS|SHHZ72 - EOHZ35', 'EchgMin').slice(0, 2), [12.636, 3.795]);
  const late = [['INS|SHF72 - EO35', -68.733], ['INS|SHHZ72 - EOHZ35', -59.033], ['INS|SHRC72 - RCE35', -57.461],
    ['G2|SHF71 - EO35', -66.256], ['G2|SHHZ71 - EOHZ35', -55.378], ['G2|SHRC71 - RCE35', -53.239]];
  for (const [ab, mean] of late) assert.deepStrictEqual(pair(ab, 'late60First').slice(3), [mean, 60, 0], ab);
  // Rule C with Shadow against the relaxed fence with Shadow: served -4 / -9.5, G2 reversals -16.6
  assert.deepStrictEqual(pair('INS|SHRC72 - SHHZ72', 'served').slice(3), [-4.017, 52, 7]);
  assert.deepStrictEqual(pair('G2|SHRC71 - SHHZ71', 'served').slice(3), [-9.544, 57, 3]);
  assert.deepStrictEqual(pair('G2|SHRC71 - SHHZ71', 'visRev60').slice(3), [-16.644, 60, 0]);
  // ledger breach in % of served: 0 under Rule C, 1.2 % (INS) and 4.2 % (G2) under the strict fence
  const breach = {};
  for (const l of section(text, 'REPORT TABLES')) { const c = l.split(/\s+/); if (c[0] === '--') breach.cfg = c[1]; else breach[breach.cfg + ' ' + c[0]] = c[7]; }
  assert.deepStrictEqual(['INS EO35', 'G2 EO35', 'INS EOHZ35', 'G2 EOHZ35', 'INS RCE35', 'G2 RCE35', 'INS SHRC72', 'G2 SHRC71'].map(k => breach[k]),
    ['1.184', '4.175', '1.178', '4.132', '0.000', '0.000', '0.000', '0.000']);
  // about 69 % guaranteed; no guaranteed rider moved under Rule C; about 4 % of guaranteed riders late > 60 s
  const g = section(text, 'GUARANTEED RIDERS').map(l => l.match(/^(\S+) .*\(([\d.]+) %\); .* 1 s (\d+) .* 60 s (\d+) \(([\d.]+) %\)$/).slice(1));
  const share = g.map(x => Number(x[1]));
  assert.deepStrictEqual([Math.min(...share), Math.max(...share)], [67.64, 69.09]);
  assert.deepStrictEqual(g.filter(x => x[0].includes('|SHRC')).map(x => x.slice(2)), [['0', '3873', '4.342'], ['0', '3717', '4.221']]);
});

test('a missing resample, a missing day, a duplicate row or an arm outside the study stops the script', () => {
  const lines = zlib.gunzipSync(fs.readFileSync(ROWS)).toString('utf8').split('\n').filter(Boolean);
  const keep = f => lines.filter(l => f(JSON.parse(l))).join('\n') + '\n';
  const fails = (rowsText, message) => {
    const r = run([], rowsText);
    assert.notStrictEqual(r.status, 0);
    assert.match(r.stderr, message);
    assert.strictEqual(r.text, null);
  };
  fails(keep(r => !(r.config === 'G2' && r.arm === 'SHRC71' && r.cell === '2019-03-12-w17-r2')), /Error: incomplete G2\|SHRC71\|2019-03-12\|w17: 2 resamples/);
  fails(keep(r => r.day !== '2019-02-05'), /Error: expected 60 days, got 59/);
  fails(lines.join('\n') + '\n' + lines[6] + '\n', /Error: duplicate row INS\|SHRC72\|2019-02-07-w08-r1/);
  fails(lines.join('\n') + '\n' + JSON.stringify({ ...JSON.parse(lines[0]), arm: 'SHHZ90' }) + '\n', /Error: unexpected config\|arm INS\|SHHZ90/);
});

test('--rows and --out are required', () => {
  const r = run([]);   // run() adds --out; --rows is missing
  assert.notStrictEqual(r.status, 0);
  assert.match(r.stderr, /usage: node sixty_day_tables\.js --rows/);
  assert.strictEqual(r.text, null);
});
