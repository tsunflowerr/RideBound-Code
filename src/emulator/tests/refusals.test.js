// Tests for the refusals of the removed research options. Every removed option, arm, travel form or score mode must
// stop with the same note (R7 below) instead of being ignored. None of these tests needs the data root: each refusal
// comes before any data is read. Run: node --test src/emulator/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), os = require('os'), path = require('path');
const { spawnSync } = require('child_process');
const E = require('../engine.js');
const SC = require('../scenario.js');
const RUN = require('../run.js');

const R7 = /removed; see commit ebca912 \(branch research\/final-real-runs-2026-11-06\)/;

// A valid scenario with 2 nodes, one request and one vehicle; the travel matrix file is m.bin.
const scenario = () => ({
  format: SC.FORMAT, scenarioId: 'refusals', provenance: { source: 'refusals.test.js', licence: 'test' },
  horizon: { startMs: 0, endMs: 1000, drainMs: 0 },
  nodes: [{ id: 'a' }, { id: 'b' }],
  travel: { base: { kind: 'matrix', matrixFile: 'm.bin' }, profile: { binMs: 1000, factors: [1] } },
  requests: [{ id: 'r', arrivalMs: 0, origin: 'a', destination: 'b' }],
  fleet: [{ id: 'v', startNode: 'a', capacity: 1 }],
  service: { pickupWindowMs: 0, maxRidePermille: 1000 },
});

// The prepared scenario (the P of engine.simulate): node a -> b takes 100 ms.
function prepared() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'refusals-'));
  try {
    const matrix = Buffer.alloc(16);
    [0, 100, 100, 0].forEach((ms, k) => matrix.writeInt32LE(ms, 4 * k));
    fs.writeFileSync(path.join(dir, 'm.bin'), matrix);
    return SC.prepareScenario(scenario(), {}, dir);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true, maxRetries: 10 });
  }
}

test('engine refuses the removed options, lock modes, physMat and arms', () => {
  const P = prepared();
  E.simulate(P, {});   // the scenario itself runs
  for (const k of ['shadowArms', 'pairs', 'trace', 'tieMode', 'reoptTie', 'fenceTolMs', 'audit']) {
    assert.throws(() => E.simulate(P, { [k]: 0 }), R7, k);
  }
  for (const k of ['g1', 'dr', 'g2PenaltyMs', 'g2MaxSwitches']) {
    assert.throws(() => E.simulate(P, { reopt: { [k]: 0 } }), R7, 'reopt.' + k);
  }
  for (const lock of ['ratchetFloor', 'none']) assert.throws(() => E.simulate(P, { lock }), R7, lock);
  assert.throws(() => E.simulate({ ...P, physMat: () => null }, {}), R7, 'physMat');
  for (const name of ['NC', 'OC', 'RR', 'FZ', 'NCE', 'OCE', 'SHNC']) {
    assert.throws(() => E.simulate(P, { arm: { name, B: 0 } }), R7, name);
  }
});

test('scenario refuses the removed travel forms', () => {
  SC.validateScenario(scenario());   // the scenario itself is valid
  const forms = {
    metric: s => { s.travel.base = { kind: 'metric' }; },
    'inline matrix': s => { s.travel.base.matrix = [[0, 100], [100, 0]]; },
    regions: s => { s.travel.profile.regions = [{}]; },
    'closure shortest-path': s => { s.travel.closure = 'shortest-path'; },
  };
  for (const [name, change] of Object.entries(forms)) {
    const s = scenario();
    change(s);
    assert.throws(() => SC.validateScenario(s), R7, name);
  }
});

test('run.js refuses the removed options and score modes', async () => {
  for (const k of ['ref', 'cmp-ref', 'hashes', 'truncate', 'repeat', 'order', 'engine', 'dump', 'dump-arms', 'pi', 'fence-tol', 'audit']) {
    await assert.rejects(RUN.main(['--' + k, 'x']), e => R7.test(e.message) && e.message.startsWith(`--${k}: `), k);
  }
  for (const mode of ['oracle', 'random']) await assert.rejects(RUN.main(['--score', mode]), R7, mode);
  await assert.rejects(RUN.main(['--out', 'x']), /--data <dir> required/);
  // from the command line: exit code 2 and the note on stderr
  const p = spawnSync(process.execPath, [path.join(__dirname, '..', 'run.js'), '--pi', '1'], { encoding: 'utf8' });
  assert.strictEqual(p.status, 2);
  assert.match(p.stderr, R7);
});

test('gen17.js refuses --set, --work and --nodetag', () => {
  for (const k of ['set', 'work', 'nodetag']) {
    const p = spawnSync(process.execPath, [path.join(__dirname, '..', 'gen', 'gen17.js'), '--' + k, 'x'], { encoding: 'utf8' });
    assert.strictEqual(p.status, 2, k);
    assert.match(p.stderr, R7, k);
  }
});
