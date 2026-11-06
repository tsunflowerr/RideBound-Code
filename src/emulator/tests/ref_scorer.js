// Reference shareability scorer (test oracle): a literal transcription of the SPEC 4.8 pseudo-code, with node weights and one
// O(N) pass per instant. It has the same definition as the production scorer sscore.makeScorer, which counts nodes per zone and
// visits only each section's ellipse; the two differ only in floating-point summation order. Kept here, outside the driver, to
// test sscore.js against (cell.mode 'causal' or 'oracle', as in sscore.makeScorer).
'use strict';

const { pathOf, IV_MS } = require('../sscore.js');
const PICK = 0;   // stop kind of a pickup (engine.js)

function makeRefScorer(NET, cell) {
  const { N, D, DT, zoneIdx } = NET;
  const G = cell.globalOf, OM = cell.omegaP, lam = cell.lam;
  const oracle = cell.mode === 'oracle';
  const onInstant = cell.onInstant || null;
  const nodeW = new Map();
  const weights = (i, orc) => {
    const key = (orc ? 'x' : 'c') + i;
    let v = nodeW.get(key);
    if (!v) {
      const z = orc ? lam.wzOracle(i) : lam.wz(i), O = new Float64Array(N), Dd = new Float64Array(N);
      for (let n = 0; n < N; n++) { O[n] = z.O[zoneIdx[n]]; Dd[n] = z.D[zoneIdx[n]]; }
      v = { O, D: Dd }; nodeW.set(key, v);
    }
    return v;
  };
  function score(ctx) {
    const g = cell.kappa * cell.factors[Math.min(ctx.bin, cell.factors.length - 1)] * 1000;
    const iv = Math.floor(ctx.tEval / IV_MS);
    const wAt = t => (oracle ? weights(Math.floor(t / IV_MS), true) : weights(iv, false));
    const M = [{ node: G[ctx.start.node], tArr: ctx.start.time, tDep: ctx.start.time, first: -1 }];
    const mainOfStop = new Int32Array(ctx.stops.length);
    ctx.stops.forEach((s, q) => {
      const gn = G[s.node], L = M[M.length - 1];
      if (gn === L.node) { if (L.first < 0) L.first = q; L.tDep = s.T + ctx.dwell; }
      else M.push({ node: gn, tArr: s.T, tDep: s.T + ctx.dwell, first: q });
      mainOfStop[q] = M.length - 1;
    });
    const kP = mainOfStop[ctx.iP], kK = mainOfStop[ctx.iD];
    const m = ctx.stops.length, Hs = new Float64Array(m + 1); Hs[m] = Infinity;
    for (let q = m - 1; q >= 0; q--) Hs[q] = Math.min(ctx.stops[q].h, Hs[q + 1]);
    const H = k => Hs[Math.max(0, M[k].first)];   // k = 0 (rest of the current arc): every stop of the route
    const load = k => { let l = ctx.load0; for (let q = 0; q < M[k].first; q++) l += ctx.stops[q].kind === PICK ? ctx.stops[q].party : -ctx.stops[q].party; return l; };
    const Rk = [], U = [];
    U[kK + 1] = new Uint8Array(N);
    for (let k = 1; k <= kK; k++) {
      const a = M[k - 1].node, b = M[k].node, dab = D[a * N + b], Hk = H(k), mask = new Uint8Array(N);
      for (let n = 0; n < N; n++) mask[n] = g * (D[a * N + n] + DT[b * N + n] - dab) <= Hk ? 1 : 0;
      Rk[k] = mask;
    }
    for (let k = kK; k >= 1; k--) { const u = new Uint8Array(N); for (let n = 0; n < N; n++) u[n] = Rk[k][n] | U[k + 1][n]; U[k] = u; }
    let Sw = 0, Sd = 0, tPrev = ctx.tEval, nInst = 0;
    let nPU = 0, nDO = 0;
    const add = (val, t, waiting, k, kind, node) => {
      const c = val * (t - tPrev) / 1000;
      if (waiting) Sw += c; else Sd += c;
      if (onInstant) onInstant({ k, kind, node, t, dt: t - tPrev, val, waiting, nPU, nDO });
      tPrev = t; nInst++;
    };
    const atMain = (k, t, w) => {
      nPU = 0; nDO = 0;
      if (k >= kK || load(k + 1) >= ctx.cap) return 0;
      const x = M[k].node; let pu = 0, dO = 0;
      const has2 = k + 2 <= kK, x2 = has2 ? M[k + 1].node : -1, rem2 = has2 ? M[k + 1].tDep - t : 0;
      for (let n = 0; n < N; n++) {
        if ((Rk[k + 1][n] && g * D[x * N + n] <= OM) || (has2 && Rk[k + 2][n] && rem2 + g * D[x2 * N + n] <= OM)) { pu += w.O[n]; nPU++; }
        if (U[k + 1][n]) { dO += w.D[n]; nDO++; }
      }
      return pu + dO;
    };
    const atInter = (k, x, t, w) => {
      nPU = 0; nDO = 0;
      if (load(k) >= ctx.cap) return 0;
      const b = M[k].node, dxb = D[x * N + b], Hk = H(k), has2 = k + 1 <= kK, rem2 = M[k].tDep - t;
      let pu = 0, dO = 0;
      for (let n = 0; n < N; n++) {
        const dxn = D[x * N + n];
        const inP = g * (dxn + DT[b * N + n] - dxb) <= Hk;
        if (inP || U[k + 1][n]) { dO += w.D[n]; nDO++; }
        if ((inP && g * dxn <= OM) || (has2 && Rk[k + 1][n] && rem2 + g * D[b * N + n] <= OM)) { pu += w.O[n]; nPU++; }
      }
      return pu + dO;
    };
    let nArc = 0;
    if (ctx.arc) {                                  // section 0: nodes of the current arc still ahead of the vehicle
      const a = G[ctx.arc.from], b = M[0].node, p = pathOf(NET, a, b), dab = D[a * N + b], pm = ctx.arc.pm;
      let tKept = tPrev;
      for (let j = 1; j < p.length - 1; j++) {
        const num = 1000 * D[a * N + p[j]] - pm * dab;
        if (num <= 0) continue;
        const t = ctx.tEval + (M[0].tArr - ctx.tEval) * num / ((1000 - pm) * dab);
        if (cell.strideMs && t - tKept < cell.strideMs) continue;
        add(atInter(0, p[j], t, wAt(t)), t, true, 0, 'arc', p[j]);
        tKept = t; nArc++;
      }
    }
    add(atMain(0, M[0].tDep, wAt(M[0].tDep)), M[0].tDep, 0 <= kP, 0, 'main', M[0].node);
    for (let k = 1; k <= kK; k++) {
      const a = M[k - 1].node, b = M[k].node, p = pathOf(NET, a, b), dab = D[a * N + b];
      let tKept = tPrev;
      for (let j = 1; j < p.length - 1; j++) {
        const t = M[k - 1].tDep + (M[k].tArr - M[k - 1].tDep) * D[a * N + p[j]] / dab;
        if (cell.strideMs && t - tKept < cell.strideMs) continue;
        add(atInter(k, p[j], t, wAt(t)), t, k <= kP, k, 'inter', p[j]);
        tKept = t;
      }
      add(atMain(k, M[k].tDep, wAt(M[k].tDep)), M[k].tDep, k <= kP, k, 'main', b);
    }
    return { S: Sw + Sd, Sw, Sd, n: nInst, kP, kK, nMain: M.length, nArc };
  }
  return { score };
}

module.exports = { makeRefScorer };
