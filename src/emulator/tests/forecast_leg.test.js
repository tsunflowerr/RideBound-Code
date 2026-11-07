// Tests for the forecast leg function legF of engine.js (the closed-loop forecast of the CL, CLc and PAD runs) against
// the golden file of the C# TravelTimeForecast (goldens.js FORECAST_GOLDEN): 40 random factor profiles x 75 legs
// [evaluation time, departure, leg ms under the current snapshot, arrival rounded to whole ms].
// legF lives inside simulate(), so every leg runs as a one-rider scenario whose first promised drop p0 is exactly
// legF(departure, leg): the request arrives at the evaluation time, the only vehicle waits at the pickup node, the
// pickup cannot start before earliest = departure, and the drop node is one leg away. The engine calls legF only with a
// departure at or after the evaluation time and a positive leg between two different nodes, so golden legs outside
// that domain are not run (they are counted). Run: node --test src/emulator/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), os = require('os'), path = require('path');
const E = require('../engine.js');
const SC = require('../scenario.js');
const { FORECAST_GOLDEN } = require('./goldens.js');

// The rider of the one-rider scenario of one leg (simulate with opts.forecast = { prof: profile, flatRatio }).
function runLeg(dir, { profile, binMs, evaluation, departure, leg, flatRatio }) {
  const matrix = Buffer.alloc(16);   // 2 x 2 int32 ms, from -> to: node a -> b takes leg ms
  [0, leg, leg, 0].forEach((ms, k) => matrix.writeInt32LE(ms, 4 * k));
  fs.writeFileSync(path.join(dir, 'leg.bin'), matrix);
  const scenario = {
    format: SC.FORMAT, scenarioId: 'leg', provenance: { source: 'forecast_leg.test.js', licence: 'test' },
    horizon: { startMs: 0, endMs: evaluation + 1, drainMs: 0 },
    nodes: [{ id: 'a' }, { id: 'b' }],
    travel: { base: { kind: 'matrix', matrixFile: 'leg.bin' }, profile: { binMs, factors: [1] } },
    requests: [{ id: 'r', arrivalMs: evaluation, origin: 'a', destination: 'b', earliestPickupMs: departure,
      latestPickupMs: departure, maxRideMs: 1e9 }],
    fleet: [{ id: 'v', startNode: 'a', capacity: 1 }],
    service: { pickupWindowMs: 0, maxRidePermille: 1000 },
  };
  const forecast = flatRatio === undefined ? { prof: profile } : { prof: profile, flatRatio };
  const { riders } = E.simulate(SC.prepareScenario(scenario, {}, dir), { forecast });
  assert.strictEqual(riders.length, 1, 'the rider was not promised');
  return riders[0];
}

function withTempDir(fn) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'forecast-leg-'));
  try { return fn(dir); } finally { fs.rmSync(dir, { recursive: true, force: true }); }
}

test('hand-worked legs: weight 1 in the evaluation bin, profile[b] / profile[b0] after it, held after the last', () => {
  const bin = 900_000;
  const rows = [   // [profile, evaluation, departure, leg, flatRatio, arrival]
    // inside the evaluation bin: not stretched
    [[1, 2], 0, 100_000, 300_000, undefined, 400_000],
    // 800 s left in bin 0 at weight 1, the other 100 s in bin 1 at weight 2 / 1: 900 s + 200 s
    [[1, 2], 0, 100_000, 900_000, undefined, 1_100_000],
    // the flat pad (PAD): weight 1.5 after the evaluation bin, whatever the profile
    [[1, 2], 0, 100_000, 900_000, 1.5, 1_050_000],
    // bin 2 is past the profile: the last factor (2) is held
    [[1, 2], 0, 1_800_000, 100_000, undefined, 2_000_000],
    // evaluated in bin 1 (factor 2): 100 s left in bin 1 at weight 1, then 100 s in bin 2 at weight 3 / 2
    [[1, 2, 3], 900_000, 1_700_000, 200_000, undefined, 1_950_000],
  ];
  withTempDir(dir => {
    for (const [profile, evaluation, departure, leg, flatRatio, arrival] of rows) {
      const r = runLeg(dir, { profile, binMs: bin, evaluation, departure, leg, flatRatio });
      assert.deepStrictEqual([r.t0, r.pk0, r.p0], [evaluation, departure, arrival], JSON.stringify(profile));
    }
  });
});

test('every golden leg in the engine\'s domain: Math.round(p0) equals the C# arrival', () => {
  const golden = JSON.parse(fs.readFileSync(FORECAST_GOLDEN, 'utf8'));
  let run = 0, before = 0, zero = 0, stretched = 0;
  const failures = [];
  withTempDir(dir => {
    for (const { profile, cases } of golden.profiles) {
      for (const [evaluation, departure, leg, arrival] of cases) {
        if (departure < evaluation) { before++; continue; }
        if (leg <= 0) { zero++; continue; }
        const r = runLeg(dir, { profile, binMs: golden.binMs, evaluation, departure, leg });
        if (r.t0 !== evaluation || r.pk0 !== departure || Math.round(r.p0) !== arrival) {
          failures.push(`[${evaluation}, ${departure}, ${leg}]: p0 ${r.p0}, C# ${arrival}`);
        }
        if (arrival !== departure + leg) stretched++;
        run++;
      }
    }
  });
  assert.deepStrictEqual(failures.slice(0, 5), []);
  assert.deepStrictEqual({ run, before, zero }, { run: 2280, before: 612, zero: 108 });
  assert.ok(stretched > 900, `only ${stretched} legs differ from the flat sum`);
});
