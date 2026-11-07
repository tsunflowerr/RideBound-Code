// Tests for the admission rule of engine.js. admits(A, r, p, e) says whether the promised rider r may get the candidate
// drop ETA p under arm A, where e is r's drop ETA on the kept route re-projected now (so c = p - e is the dispatch
// change). The truth table lists, for every kept arm, ETAs just inside and just outside its bound, worked out by hand
// for one rider. Run: node --test src/emulator/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const E = require('../engine.js');

const s = seconds => seconds * 1000;   // the engine works in ms
// The rider: first promised drop p0 = 1,000 s, residual drop slack slk = 400 s, Shadow budget bSH = 100 s, and 30 s of
// dispatch changes already charged to its Rule C ledger (Labs = sum of |c|).
const rider = () => ({ p0: s(1000), slk: s(400), bSH: s(100), Labs: s(30), net: s(30) });
const arm = key => E.armOf(E.parseArm(key));

// [arm key, its bound for this rider, rows [p, e, admitted]]. B = 100 s for RC/PS/DLHZ/DLS; alpha = 0.5 gives the
// Elmi budget round(0.5 * slk) = 200 s for EO/EOHZ/RCE; the Shadow arms SHF/SHHZ/SHRC use bSH = 100 s.
const TRUTH_TABLE = [
  ['U', 'no bound', [[s(5000), s(1050), true], [s(1050), s(1050), true]]],
  ['RC100', 'Rule C: Labs + |p - e| <= 100 s, so |p - e| <= 70 s',
    [[s(1120), s(1050), true], [s(1120) + 1, s(1050), false], [s(980), s(1050), true], [s(980) - 1, s(1050), false]]],
  ['PS100', 'per-step cap: |p - e| <= 100 s (the ledger does not count)',
    [[s(1150), s(1050), true], [s(1150) + 1, s(1050), false], [s(950), s(1050), true], [s(950) - 1, s(1050), false]]],
  ['DLHZ100', 'deadline with the Hoerl-Zwick floor: p <= max(p0 + 100 s, e)',
    [[s(1100), s(1050), true], [s(1100) + 1, s(1050), false], [s(1300), s(1300), true], [s(1300) + 1, s(1300), false]]],
  ['DLS100', 'strict deadline: p <= p0 + 100 s, even when the kept route is later',
    [[s(1100), s(1050), true], [s(1100) + 1, s(1050), false], [s(1300), s(1300), false]]],
  ['EO50', 'Elmi Ordinary Control, strict: p <= p0 + 200 s',
    [[s(1200), s(1050), true], [s(1200) + 1, s(1050), false], [s(1300), s(1300), false]]],
  ['EOHZ50', 'Elmi with the Hoerl-Zwick floor: p <= max(p0 + 200 s, e)',
    [[s(1200), s(1050), true], [s(1200) + 1, s(1050), false], [s(1300), s(1300), true], [s(1300) + 1, s(1300), false]]],
  ['RCE50', 'Rule C with the Elmi budget: Labs + |p - e| <= 200 s, so |p - e| <= 170 s',
    [[s(1220), s(1050), true], [s(1220) + 1, s(1050), false], [s(880), s(1050), true], [s(880) - 1, s(1050), false]]],
  ['SHF71', 'Shadow, strict: p <= p0 + bSH = 1,100 s',
    [[s(1100), s(1050), true], [s(1100) + 1, s(1050), false], [s(1300), s(1300), false]]],
  ['SHHZ71', 'Shadow with the Hoerl-Zwick floor: p <= max(p0 + bSH, e)',
    [[s(1100), s(1050), true], [s(1100) + 1, s(1050), false], [s(1300), s(1300), true], [s(1300) + 1, s(1300), false]]],
  ['SHRC71', 'Shadow with Rule C: Labs + |p - e| <= bSH, so |p - e| <= 70 s',
    [[s(1120), s(1050), true], [s(1120) + 1, s(1050), false], [s(980), s(1050), true], [s(980) - 1, s(1050), false]]],
];

for (const [key, bound, rows] of TRUTH_TABLE) {
  test(`admits ${key}: ${bound}`, () => {
    for (const [p, e, admitted] of rows) {
      assert.strictEqual(E.admits(arm(key), rider(), p, e), admitted, `p = ${p} ms, e = ${e} ms`);
    }
  });
}

test('a guaranteed Shadow rider (class 0, bSH = 0) keeps its promise or the kept route', () => {
  const guaranteed = () => ({ ...rider(), bSH: 0, Labs: 0 });
  const rows = [
    ['SHF71', s(1000), s(900), true], ['SHF71', s(1000) + 1, s(900), false],
    ['SHHZ71', s(1050), s(1050), true], ['SHHZ71', s(1050) + 1, s(1050), false],
    ['SHRC71', s(1050), s(1050), true], ['SHRC71', s(1050) + 1, s(1050), false],
    ['SHRC71', s(1050) - 1, s(1050), false],
  ];
  for (const [key, p, e, admitted] of rows) assert.strictEqual(E.admits(arm(key), guaranteed(), p, e), admitted, key);
});

test('the Elmi budget round(alpha * slk) rounds half up', () => {
  const r = { ...rider(), slk: 400_001 };                    // 0.5 * 400,001 ms = 200,000.5 ms -> 200,001 ms
  assert.strictEqual(E.admits(arm('EO50'), r, r.p0 + 200_001, s(1050)), true);
  assert.strictEqual(E.admits(arm('EO50'), r, r.p0 + 200_002, s(1050)), false);
});

test('charge adds |c| to the Rule C ledger and c to the net sum', () => {
  const r = rider();
  E.charge(r, -s(20));
  assert.deepStrictEqual([r.Labs, r.net], [s(50), s(10)]);
  E.charge(r, s(5));
  assert.deepStrictEqual([r.Labs, r.net], [s(55), s(15)]);
});

test('arm keys: code, budget parameter, Shadow and strict-fence flags', () => {
  const rows = [   // [key, code, parameter, value, isShadow, isStrictFence]
    ['U', 0, 'B', 0, false, false], ['RC100', 1, 'B', s(100), false, false], ['PS100', 2, 'B', s(100), false, false],
    ['DLHZ100', 3, 'B', s(100), false, false], ['DLS100', 4, 'B', s(100), false, true],
    ['EO35', 8, 'alpha', 0.35, false, true], ['EOHZ35', 9, 'alpha', 0.35, false, false],
    ['RCE35', 10, 'alpha', 0.35, false, false], ['SHF71', 13, 'x', 0.71, true, true],
    ['SHHZ71', 14, 'x', 0.71, true, false], ['SHRC71', 15, 'x', 0.71, true, false],
  ];
  for (const [key, code, param, value, shadow, strict] of rows) {
    const A = arm(key);
    assert.deepStrictEqual([A.key, A.code, A[param], E.isShadow(A), E.isStrictFence(A)],
      [key, code, value, shadow, strict], key);
  }
});

test('removed and unknown arms are refused', () => {
  for (const key of ['NC100', 'OC100', 'RR100', 'FZ100', 'NCE35', 'OCE35', 'SHNC71']) {
    assert.throws(() => E.parseArm(key), /removed; see commit ebca912 \(branch research\/final-real-runs-2026-11-06\)/,
      key);
  }
  assert.throws(() => E.parseArm('XYZ1'), /bad arm key/);
  assert.throws(() => E.admits({ code: 5 }, rider(), s(1000), s(1000)), /arm code/);     // 5..7, 11, 12: no arm
  assert.throws(() => E.admits({ code: '15' }, rider(), s(1000), s(1000)), /arm code/);  // codes are numbers
});
