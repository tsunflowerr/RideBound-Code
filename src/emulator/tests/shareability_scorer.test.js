// Tests for the shareability score of a rider at its first promise. sscore.makeScorer is the fast scorer the driver
// uses (node counts per zone, only the nodes inside each section's ellipse); tests/ref_scorer.js is the reference
// scorer, a literal transcription of the SPEC pseudo-code (one pass over every node per instant). Both are checked on
// the golden file of the C# ShareabilityScorer (goldens.js SHAREABILITY_GOLDEN: 24 small networks, 10 scored contexts
// each, written by export_network.js toy), and the reference scorer again on more random contexts.
// Run: node --test src/emulator/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), os = require('os'), path = require('path');
const { spawnSync } = require('child_process');
const SS = require('../sscore.js');
const LAM = require('../lambda.js');
const { makeRefScorer } = require('./ref_scorer.js');
const { SHAREABILITY_GOLDEN, toyNetworkOf } = require('./goldens.js');

const FIELDS = ['S', 'Sw', 'Sd', 'n', 'kP', 'kK', 'nMain', 'nArc'];   // score, its waiting/riding parts, instant counts

// Both scorers of every golden cell, with the cell's contexts and the scores the golden stores for them.
function goldenCells() {
  const golden = JSON.parse(fs.readFileSync(SHAREABILITY_GOLDEN, 'utf8'));
  return golden.cells.map(cell => {
    const net = toyNetworkOf(cell), L = cell.lambda;
    const lam = LAM.makeLambda({ nZones: net.nZones, zoneSize: net.zoneSize, N: net.N, arrivals: L.arrivals, zO: L.zO,
      zD: L.zD, priorO: L.priorO, priorD: L.priorD, phi: L.phi, alpha: L.alpha, sampled: L.sampled,
      intervalMs: L.intervalMs });
    const c = cell.cell;
    const ctx = { kappa: c.kappa, factors: c.factors, globalOf: Int32Array.from(c.globalOf), omegaP: c.omegaP,
      strideMs: c.strideMs, lam, mode: c.mode };
    return { fast: SS.makeScorer(net, ctx), ref: makeRefScorer(net, ctx), cases: cell.cases };
  });
}

test('the fast scorer reproduces every score of the C# golden exactly', () => {
  let n = 0;
  for (const { fast, cases } of goldenCells()) {
    for (const { ctx, expected } of cases) {
      const got = fast.score(ctx);
      for (const f of FIELDS) assert.ok(Object.is(got[f], expected[f]), `case ${n} ${f}: ${got[f]} != ${expected[f]}`);
      n++;
    }
  }
  assert.strictEqual(n, 240);
});

test('the reference scorer finds the same instants; the scores differ only by summation order', () => {
  let nonzero = 0, withArc = 0, maxRel = 0;
  for (const { fast, ref, cases } of goldenCells()) {
    for (const { ctx } of cases) {
      const a = fast.score(ctx), b = ref.score(ctx);
      assert.deepStrictEqual(['n', 'kP', 'kK', 'nMain', 'nArc'].map(f => b[f]),
        ['n', 'kP', 'kK', 'nMain', 'nArc'].map(f => a[f]));
      for (const f of ['S', 'Sw', 'Sd']) {
        if (a[f] === 0) { assert.strictEqual(b[f], 0); continue; }
        maxRel = Math.max(maxRel, Math.abs(a[f] - b[f]) / Math.abs(a[f]));
      }
      if (a.S !== 0) nonzero++;
      if (ctx.arc) withArc++;
    }
  }
  assert.ok(maxRel <= 1e-12, `relative difference ${maxRel}`);
  assert.deepStrictEqual({ nonzero, withArc }, { nonzero: 167, withArc: 91 });   // the golden exercises both paths
});

// export_network.js scores every random context with both scorers, stops if they find different instants, and reports
// the largest relative difference of the two scores (refMaxRel). It takes under a second; the time limit turns a hang
// (it draws contexts until it has enough that the fast scorer accepts) into a failure.
test('the reference scorer on 6,000 more random contexts (export_network.js toy, 300 networks, seed 1)', () => {
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'scorer-'));
  try {
    const p = spawnSync(process.execPath, [path.join(__dirname, '..', 'export_network.js'), 'toy',
      path.join(tmp, 'toy.json'), '300', '20', '1'], { encoding: 'utf8', timeout: 300_000 });
    assert.strictEqual(p.status, 0, p.error ? String(p.error) : p.stderr);
    const { cases, refMaxRel, nonzero, withArc } = JSON.parse(p.stdout);
    assert.strictEqual(cases, 6000);
    assert.ok(refMaxRel <= 1e-12, `relative difference ${refMaxRel}`);
    assert.ok(nonzero > 4000 && withArc > 2000, `nonzero ${nonzero}, with an arc ${withArc}`);
  } finally {
    fs.rmSync(tmp, { recursive: true, force: true, maxRetries: 10 });
  }
});
