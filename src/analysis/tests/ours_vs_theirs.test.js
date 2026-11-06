// Tests for ours_vs_theirs.js. Run: node --test src/analysis/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), os = require('os'), path = require('path'), zlib = require('zlib');
const { spawnSync } = require('child_process');

const DIR = path.join(__dirname, '..');
const ROWS = path.join(DIR, 'data', 'wf20-fullsys-rows.ndjson.gz');

// Runs the script with args plus --out <temporary folder>/ovt.md; returns exit status, stdout, stderr and the file.
// rowsText, when given, is written to the temporary folder and passed as --rows.
function run(args, rowsText) {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'ours-vs-theirs-'));
  try {
    const out = path.join(tmp, 'ovt.md');
    if (rowsText !== undefined) {
      fs.writeFileSync(path.join(tmp, 'rows.ndjson'), rowsText);
      args = ['--rows', path.join(tmp, 'rows.ndjson'), ...args];
    }
    const p = spawnSync(process.execPath, [path.join(DIR, 'ours_vs_theirs.js'), ...args, '--out', out], { encoding: 'utf8' });
    return { status: p.status, stdout: p.stdout, stderr: p.stderr, md: fs.existsSync(out) ? fs.readFileSync(out) : null };
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
// the cells of the table rows that follow the heading line starting with `heading` (first match after `from`)
function table(md, heading, from = 0) {
  const lines = md.split('\n'), h = lines.findIndex((l, i) => i >= from && l.startsWith(heading));
  assert.ok(h >= 0, 'heading not found: ' + heading);
  const rows = [];
  for (let i = h + 4; lines[i] && lines[i].startsWith('|'); i++) rows.push(lines[i].split(' | ').map(c => c.replace(/^\| |\s\|$/g, '')));
  return { rows, end: h + 4 + rows.length };
}
const num = s => Number(s.replace(',', '.'));

let full;   // made once and shared
const fullRun = () => (full ??= run(['--rows', ROWS]));

test('OURS-VS-THEIRS.md is rebuilt byte for byte from the shipped rows, and printed', () => {
  const r = fullRun(), stored = fs.readFileSync(path.join(DIR, 'expected', 'ours-vs-theirs.md'));
  assert.strictEqual(r.status, 0, r.stderr);
  assert.ok(r.md.equals(stored), firstDifference(r.md, stored));
  assert.strictEqual(r.stdout, stored.toString('utf8') + '\n');
});

test('the report\'s plain column (G2, whole day) and the split total = Rule C part + forecast part', () => {
  const md = fullRun().md.toString('utf8');
  const g2 = table(md, '### Hiệu của mình − đối thủ (SHRC+dự báo − SHHZ), cả ngày').rows;
  const mean = label => g2.find(c => c[0].startsWith(label))[1];
  assert.deepStrictEqual(['Phục vụ', 'Đảo hứa', 'Trễ >60 s', 'TV5'].map(mean), ['-8,84', '-27,46', '-34,04', '-38,11']);
  let from = 0;
  for (const cfg of ['G2', 'INS']) {
    const t = table(md, '### Tách phần', from);
    from = t.end;
    assert.strictEqual(t.rows.length, 8, cfg);
    for (const [label, total, ruleC, forecast] of t.rows) {
      const sum = num(ruleC) + num(forecast), tot = num(total.split(' ')[0]);
      assert.ok(Math.abs(tot - sum) <= 0.015 + 1e-9, `${cfg} ${label}: ${total} vs ${ruleC} + ${forecast}`);   // 3 roundings
    }
    if (cfg === 'G2') assert.deepStrictEqual(t.rows[1].slice(1), ['-27,46 (0/30)', '-13,81', '-13,64']);
  }
});

test('a missing resample or a missing day stops the script', () => {
  const lines = zlib.gunzipSync(fs.readFileSync(ROWS)).toString('utf8').split('\n').filter(Boolean);
  const keep = f => lines.filter(l => f(JSON.parse(l))).join('\n') + '\n';
  const noResample = run([], keep(r => !(r.config === 'G2' && r.mode === 'CL' && r.cell === '2019-09-19-w08-r1' && r.arm.startsWith('SHRC'))));
  assert.notStrictEqual(noResample.status, 0);
  assert.match(noResample.stderr, /Error: incomplete G2,CL,SHRC,2019-09-19,w08/);
  assert.strictEqual(noResample.md, null);
  const noDay = run([], keep(r => r.day !== '2019-09-19'));
  assert.notStrictEqual(noDay.status, 0);
  assert.match(noDay.stderr, /Error: expected 30 days, got 29/);
});

test('--rows and --out are required', () => {
  const r = run([]);   // run() adds --out; --rows is missing
  assert.notStrictEqual(r.status, 0);
  assert.match(r.stderr, /usage: node ours_vs_theirs\.js --rows/);
  assert.strictEqual(r.md, null);
});
