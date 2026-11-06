// GĐ4 gate data (ADR-089): golden shareability scores from the comparison emulator's own scorer, for the C# port
// (RideBound.Algorithms.Shareability). Read-only use of the emulator files: sscore.js (makeScorer = golden,
// makeRefScorer = sanity), lambda.js, netfull.js (toy networks; full Manhattan network via build()).
//
// Cells share one network and one demand table, like the riders of one emulator cell. Contexts are random but valid
// (non-decreasing main-node times, target pickup before drop, arc start != arc end); a context the scorer refuses is
// skipped and counted. Deterministic: xorshift32 seeded from the command line.
//
// Usage: node gen_golden.js toy <out.json> <cells> <casesPerCell> <seed>
//        node gen_golden.js manhattan <out.json> <cells> <casesPerCell> <seed> <networkOutDir>
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const RUNNER = 'E:/Code/RideBound-fastrun-run/research/continuation/2026-10-26/company-emulator/sp/wf20/fullsys/code/runner';
const SS = require(path.join(RUNNER, 'sscore.js'));
const LAM = require(path.join(RUNNER, 'lambda.js'));
const NF = require(path.join(RUNNER, 'netfull.js'));
const sha = f => crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');

const [mode, out, cellsArg, casesArg, seedArg, netOut] = process.argv.slice(2);
if (!['toy', 'manhattan'].includes(mode) || !out || fs.existsSync(out)) throw new Error('usage, or output exists');
let h = (Number(seedArg) >>> 0) || 1;
const rnd = () => { h ^= h << 13; h >>>= 0; h ^= h >>> 17; h ^= h << 5; h >>>= 0; return h / 4294967296; };
const ri = (a, b) => a + Math.floor(rnd() * (b - a + 1));
const pick = arr => arr[ri(0, arr.length - 1)];
const frac = () => (rnd() < 0.3 ? rnd() : 0);

function toyNet() {
  const N = ri(3, 16);
  const edges = [];
  for (let i = 0; i < N; i++) edges.push([i, (i + 1) % N, ri(1, 120)]);       // ring: strongly connected
  for (let e = ri(0, 2 * N); e > 0; e--) { const a = ri(0, N - 1), b = ri(0, N - 1); if (a !== b) edges.push([a, b, ri(1, 120)]); }
  const nz = ri(1, Math.min(4, N));
  const zoneOfNode = Array.from({ length: N }, (_, i) => 100 + (i < nz ? i : ri(0, nz - 1)));
  return NF.toy(N, edges, zoneOfNode);
}

function exportNet(NET) {
  return { N: NET.N, nZones: NET.nZones, D: Array.from(NET.D), csrStart: Array.from(NET.csrStart), csrTo: Array.from(NET.csrTo),
    csrW: Array.from(NET.csrW), zoneIdx: Array.from(NET.zoneIdx) };
}

function makeCell(NET) {
  const L = Math.min(NET.N, mode === 'toy' ? ri(2, NET.N) : ri(20, 80));
  const all = Array.from({ length: NET.N }, (_, i) => i);
  for (let i = all.length - 1; i > 0; i--) { const j = ri(0, i); [all[i], all[j]] = [all[j], all[i]]; }
  const globalOf = all.slice(0, L);
  const nReq = ri(0, mode === 'toy' ? 60 : 300);
  const arrivals = [], zO = [], zD = [];
  for (let k = 0; k < nReq; k++) { arrivals.push(ri(0, 7_500_000)); zO.push(ri(0, NET.nZones - 1)); zD.push(ri(0, NET.nZones - 1)); }
  const empty = rnd() < 0.2;
  const priorO = Array.from({ length: NET.nZones }, () => (empty ? 0 : ri(0, 20)));
  const priorD = Array.from({ length: NET.nZones }, () => (empty ? 0 : ri(0, 20)));
  const alpha = pick([0, 1, rnd(), rnd()]);
  const lambda = { arrivals, zO, zD, priorO, priorD, phi: 0.05 + rnd() * 0.9, alpha, sampled: ri(1, 600), intervalMs: 600000 };
  const cell = { kappa: 0.5 + rnd(), factors: Array.from({ length: ri(1, 10) }, () => 1 + rnd() * 0.3), globalOf,
    omegaP: 600000, strideMs: pick([0, 0, 30000, 60000]), mode: rnd() < 0.25 ? 'oracle' : 'causal' };
  return { cell, lambda };
}

function makeCtx(L) {
  const tEval = ri(0, 7_000_000) + frac();
  const dwell = pick([0, 0, 30000]);
  const cap = ri(2, 6);
  const m = ri(2, 10);
  const startNode = ri(0, L - 1);
  const startTime = tEval + (rnd() < 0.5 ? 0 : ri(0, 60000) + frac());
  const stops = [];
  let last = startTime, prevNode = startNode;
  for (let q = 0; q < m; q++) {
    const node = rnd() < 0.2 ? prevNode : ri(0, L - 1);
    const T = last + ri(0, 900000) + frac();
    stops.push({ node, kind: ri(0, 1), T, h: rnd() < 0.15 ? 0 : ri(0, 900000) + frac(), party: ri(1, 2) });
    last = T + dwell; prevNode = node;
  }
  const iP = ri(0, m - 2), iD = ri(iP + 1, m - 1);
  stops[iP].kind = 0; stops[iD].kind = 1;
  let arc = null;
  if (rnd() < 0.4 && L > 1) {
    let from = ri(0, L - 1);
    if (from === startNode) from = (from + 1) % L;
    arc = { from, pm: ri(1, 999) };
  }
  return { tEval, bin: Math.floor(tEval / 900000), start: { node: startNode, time: startTime }, arc, load0: ri(0, cap), cap, stops, iP, iD, dwell, rq: 'q', ri: 0 };
}

let NET = null;
if (mode === 'manhattan') {
  if (!netOut || fs.existsSync(netOut)) throw new Error('network output dir missing or exists');
  NET = NF.build();
  fs.mkdirSync(netOut, { recursive: true });
  fs.writeFileSync(path.join(netOut, 'D.u16'), Buffer.from(NET.D.buffer, NET.D.byteOffset, NET.D.byteLength));
  const e = exportNet(NET); delete e.D;
  fs.writeFileSync(path.join(netOut, 'network.json'), JSON.stringify(e));
}

const result = { generator: 'gen_golden.js', mode, seed: Number(seedArg), sources: {
  sscore: sha(path.join(RUNNER, 'sscore.js')), lambda: sha(path.join(RUNNER, 'lambda.js')), netfull: sha(path.join(RUNNER, 'netfull.js')) },
  cells: [], skipped: 0, refMaxRel: 0 };
for (let c = 0; c < Number(cellsArg); c++) {
  const net = mode === 'toy' ? toyNet() : NET;
  const { cell, lambda } = makeCell(net);
  const lam = LAM.makeLambda({ nZones: net.nZones, zoneSize: net.zoneSize, N: net.N, arrivals: lambda.arrivals, zO: lambda.zO, zD: lambda.zD,
    priorO: lambda.priorO, priorD: lambda.priorD, phi: lambda.phi, alpha: lambda.alpha, sampled: lambda.sampled, intervalMs: lambda.intervalMs });
  const cellCtx = { kappa: cell.kappa, factors: cell.factors, globalOf: Int32Array.from(cell.globalOf), omegaP: cell.omegaP,
    strideMs: cell.strideMs, lam, mode: cell.mode };
  const scorer = SS.makeScorer(net, cellCtx), ref = SS.makeRefScorer(net, cellCtx);
  const cases = [];
  while (cases.length < Number(casesArg)) {
    const ctx = makeCtx(cell.globalOf.length);
    let res;
    try { res = scorer.score(ctx); } catch (err) { result.skipped++; continue; }
    const q = ref.score(ctx);
    const rel = Math.abs(q.S - res.S) / Math.max(1e-300, Math.abs(q.S));
    if (q.S !== 0 && rel > result.refMaxRel) result.refMaxRel = rel;
    if (q.n !== res.n || q.nArc !== res.nArc || q.kP !== res.kP || q.kK !== res.kK) throw new Error('reference scorer disagrees on instants');
    delete ctx.rq; delete ctx.ri;
    cases.push({ ctx, expected: { S: res.S, Sw: res.Sw, Sd: res.Sd, n: res.n, kP: res.kP, kK: res.kK, nMain: res.nMain, nArc: res.nArc } });
  }
  // the weights the scores used (checked separately by the C# lambda port)
  const intervals = Array.from({ length: 14 }, (_, i) => i);
  const weights = intervals.map(i => ({ i, causal: lam.wz(i), oracle: lam.wzOracle(i) }))
    .map(w => ({ i: w.i, cO: Array.from(w.causal.O), cD: Array.from(w.causal.D), oO: Array.from(w.oracle.O), oD: Array.from(w.oracle.D) }));
  result.cells.push({ net: mode === 'toy' ? exportNet(net) : 'manhattan', cell, lambda, weights, fallbacks: lam.fallbacks, cases });
}
fs.writeFileSync(out, JSON.stringify(result));
const n = result.cells.reduce((s, c) => s + c.cases.length, 0);
console.log(JSON.stringify({ out, cells: result.cells.length, cases: n, skipped: result.skipped, refMaxRel: result.refMaxRel,
  nonzero: result.cells.reduce((s, c) => s + c.cases.filter(k => k.expected.S !== 0).length, 0),
  withArc: result.cells.reduce((s, c) => s + c.cases.filter(k => k.ctx.arc).length, 0), bytes: fs.statSync(out).size }));
