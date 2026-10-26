// wf15 (SPEC section 4): Elmi, Moylan, Alonso-Mora & Fielbaum (2026) time-integrated shareability score of a rider at the
// rider's first promise, on the FULL FleetPy Manhattan network. Pure and passive: reads the context built by the engine
// (vehicle start, route stops with projected times and headrooms), never writes engine state.
//
//   main nodes   vehicle start + route stops, consecutive coincident stops merged (D11)
//   current arc  if the vehicle is mid-arc (ctx.arc = { from, pm }), M[0] is the arc end; the part of the arc still ahead
//                (free-flow fraction > pm/1000 of the reconstructed path from -> M[0]) is "section 0", integrated node by node
//                like every later section (paper p11-12: all main and intermediate nodes from the vehicle's position; D9)
//   sections     k = 1..kK (only those ending at or before the target's drop main node kK, D10)
//   headroom     H[k] = min_{q >= first stop of M[k]} h_q (suffix minimum; C_v(s_i) of Eq. 3-4)
//   shadow       N_DO = union over remaining sections of {n : g*(D[a][n] + D[n][b] - D[a][b]) <= H}            (Eq. 4)
//                N_PU = first two remaining sections only (footnote 9), with W = L1 + L3 <= Omega_p            (Eq. 3)
//   instant      S(t) = c_v * ( sum_z lambdaO_z |N_PU in z| / |N_z| + sum_z lambdaD_z |N_DO in z| / |N_z| )      (Eq. 9)
//   integration  S_r = sum over main and intermediate node instants of S(t_n) * (t_n - t_{n-1}) / 1000          (Eq. 11)
//                instants up to and including M[kP] -> S^w, later -> S^d
//
// Two implementations with the same definition:
//   makeScorer     production: per-zone integer node counts (Eq. 9 as written), integer free-flow thresholds equivalent to
//                  the float tests, and per-instant iteration restricted to the section ellipse R_k (exact: for x on a
//                  shortest a->b path, dev(x,b,n) <= H implies dev(a,b,n) <= H by the triangle inequality of the exact
//                  integer APSP). Floating-point sums differ from the reference only by summation order.
//   makeRefScorer  literal transcription of the SPEC 4.8 pseudo-code (node weights, one O(N) pass per instant); test oracle.
'use strict';

const PICK = 0;
const IV_MS = 600000;

// largest integer d with fl(g*d) <= H (fl(g*d) is non-decreasing in d for g > 0); -1 if none; Infinity if H = +Infinity
function dMaxMul(g, H) {
  if (H === Infinity) return Infinity;
  if (!(H >= 0)) return -1;
  let d = Math.floor(H / g);
  if (d > 1e7) return Infinity;                  // beyond any free-flow distance on the network (uint16 seconds)
  while (g * (d + 1) <= H) d++;
  while (d >= 0 && g * d > H) d--;
  return d;
}
// largest integer d with fl(rem + fl(g*d)) <= OM (non-decreasing in d); -1 if none
function dMaxAdd(g, rem, OM) {
  if (!(rem <= OM)) return -1;
  let d = Math.floor((OM - rem) / g);
  if (d > 1e7) return Infinity;
  while (rem + g * (d + 1) <= OM) d++;
  while (d >= 0 && rem + g * d > OM) d--;
  return d;
}

// one shortest free-flow path a -> b on the edge list: greedy step to the out-neighbour u with w(cur,u) + D[u][b] == D[cur][b],
// smallest u on ties (SPEC 4.7). Exact for integer weights and the closed integer APSP. Cached per thread (LRU, cap 50 000).
function pathOf(NET, a, b) {
  const { N, D, csrStart, csrTo, csrW } = NET;
  const key = a * N + b;
  const cache = NET.paths;
  const hit = cache.get(key);
  if (hit) { cache.delete(key); cache.set(key, hit); NET.pathStats.hits++; return hit; }
  NET.pathStats.misses++;
  const out = [a];
  let cur = a, wsum = 0;
  for (let guard = 0; cur !== b; guard++) {
    if (guard > N) throw new Error('sscore: path reconstruction loop');
    const dcb = D[cur * N + b];
    let best = -1, bw = 0;
    for (let k = csrStart[cur]; k < csrStart[cur + 1]; k++) {
      const u = csrTo[k];
      if (csrW[k] + D[u * N + b] === dcb && (best < 0 || u < best)) { best = u; bw = csrW[k]; }
    }
    if (best < 0) throw new Error(`sscore: no shortest-path step ${cur} -> ${b}`);
    out.push(best); wsum += bw; cur = best;
  }
  if (wsum !== D[a * N + b]) throw new Error(`sscore: path edge sum ${wsum} != D ${D[a * N + b]} (${a} -> ${b})`); // T7
  NET.pathStats.checked++;
  const p = Int32Array.from(out);
  cache.set(key, p);
  if (cache.size > NET.pathCap) cache.delete(cache.keys().next().value);
  return p;
}

// main nodes (SPEC 4.3) from the engine context; returns arrays and the target's main-node indices
function mainNodes(ctx, G) {
  const Mn = [G[ctx.start.node]], Ma = [ctx.start.time], Md = [ctx.start.time], Mf = [-1];
  const stops = ctx.stops, m = stops.length;
  const mainOf = new Int32Array(m);
  for (let q = 0; q < m; q++) {
    const s = stops[q], gn = G[s.node], L = Mn.length - 1;
    if (gn === Mn[L]) { if (Mf[L] < 0) Mf[L] = q; Md[L] = s.T + ctx.dwell; }
    else { Mn.push(gn); Ma.push(s.T); Md.push(s.T + ctx.dwell); Mf.push(q); }
    mainOf[q] = Mn.length - 1;
  }
  const kP = mainOf[ctx.iP], kK = mainOf[ctx.iD];
  if (!(kK >= 1) || kP > kK) throw new Error(`sscore: bad target main nodes kP ${kP} kK ${kK}`);
  return { Mn, Ma, Md, Mf, kP, kK };
}

function sectionHeadroom(ctx, Mf, kK) {
  const stops = ctx.stops, m = stops.length;
  const Hs = new Float64Array(m + 1); Hs[m] = Infinity;
  for (let q = m - 1; q >= 0; q--) Hs[q] = Math.min(stops[q].h, Hs[q + 1]);
  const H = new Float64Array(kK + 2);
  H[0] = Hs[0];                                    // section 0 (rest of the current arc) delays every stop of the route
  for (let k = 1; k <= kK; k++) H[k] = Hs[Mf[k]];
  return H;
}
function sectionLoads(ctx, Mf, kK) {
  const stops = ctx.stops;
  const load = new Int32Array(kK + 2);
  let l = ctx.load0, q = 0;
  load[0] = l;                                     // load on section 0 (rest of the current arc): no stop served yet
  for (let k = 1; k <= kK; k++) {
    for (; q < Mf[k]; q++) l += stops[q].kind === PICK ? stops[q].party : -stops[q].party;
    load[k] = l;                                   // load on section k (after serving every stop before M[k])
  }
  return load;
}

function checkTimes(ctx, Ma, Md, kK) {
  if (!(Md[0] >= ctx.tEval)) throw new Error('sscore: start time before tEval');
  for (let k = 1; k <= kK; k++) if (!(Ma[k] >= Md[k - 1]) || !(Md[k] >= Ma[k])) throw new Error('sscore: main-node times decrease'); // T7
}

// ------------------------------------------------------------------------------------------------ production scorer
// cell = { kappa, factors, globalOf (local -> global node), omegaP, strideMs, lam (lambda.js), mode: 'causal'|'oracle',
//          onInstant (optional test hook) }
function makeScorer(NET, cell) {
  const { N, D, DT, zoneIdx, nZones } = NET;
  const G = cell.globalOf, OM = cell.omegaP, stride = cell.strideMs || 0, lam = cell.lam;
  const oracle = cell.mode === 'oracle';
  if (!['causal', 'oracle'].includes(cell.mode || 'causal')) throw new Error('sscore: mode');
  const onInstant = cell.onInstant || null;
  const stats = { calls: 0, instants: 0, ns: 0n, maskNodes: 0, sections: 0, arcCalls: 0, arcInstants: 0 };
  const masks = [];
  const getMask = k => { while (masks.length <= k) masks.push(new Uint8Array(N)); return masks[k]; };
  const maxSec = new Int32Array(N), inU = new Uint8Array(N);
  const tmp = new Int32Array(N);
  const cPU = new Int32Array(nZones), cDO = new Int32Array(nZones);
  const zsum = (w, c) => { let s = 0; for (let z = 0; z < nZones; z++) if (c[z] !== 0) s += w[z] * c[z]; return s; };
  const csum = c => { let s = 0; for (let z = 0; z < nZones; z++) s += c[z]; return s; };

  function score(ctx) {
    const h0 = process.hrtime.bigint();
    stats.calls++;
    const g = cell.kappa * cell.factors[Math.min(ctx.bin, cell.factors.length - 1)] * 1000;
    const { Mn, Ma, Md, Mf, kP, kK } = mainNodes(ctx, G);
    checkTimes(ctx, Ma, Md, kK);
    const H = sectionHeadroom(ctx, Mf, kK);
    const load = sectionLoads(ctx, Mf, kK);
    const cap = ctx.cap;
    const dPU = dMaxMul(g, OM);
    const dMaxK = new Float64Array(kK + 2);
    // full-section ellipses R_k (mask + node list)
    const lists = new Array(kK + 2);
    lists[kK + 1] = new Int32Array(0);
    for (let k = 1; k <= kK; k++) {
      const a = Mn[k - 1], b = Mn[k], rowA = a * N, rowB = b * N, dab = D[rowA + b], dm = dMaxK[k] = dMaxMul(g, H[k]);
      const mk = getMask(k);
      let c = 0;
      for (let n = 0; n < N; n++) if (D[rowA + n] + DT[rowB + n] - dab <= dm) { mk[n] = 1; tmp[c++] = n; }
      lists[k] = tmp.slice(0, c);
      stats.maskNodes += c; stats.sections++;
    }
    // section 0 = remaining part of the current arc (vehicle mid-arc): ellipse of the whole arc a0 -> M[0] (exact superset
    // of every sigma1 = (x -> M[0]) for x on the reconstructed shortest path a0 -> M[0], as for the full sections)
    const arc = ctx.arc || null;
    let a0 = -1;
    if (arc) {
      a0 = G[arc.from];
      if (!(arc.pm >= 1 && arc.pm <= 999) || !Number.isInteger(arc.pm)) throw new Error('sscore: arc.pm must be an integer 1..999');
      if (a0 === Mn[0]) throw new Error('sscore: arc start equals arc end');
      const rowA = a0 * N, rowB = Mn[0] * N, dab = D[rowA + Mn[0]], dm = dMaxK[0] = dMaxMul(g, H[0]);
      const mk = getMask(0);
      let c = 0;
      for (let n = 0; n < N; n++) if (D[rowA + n] + DT[rowB + n] - dab <= dm) { mk[n] = 1; tmp[c++] = n; }
      lists[0] = tmp.slice(0, c);
      stats.maskNodes += c; stats.sections++;
    }
    // maxSec[n] = largest section containing n (n in U[k+1] <=> maxSec[n] >= k+1); per-zone counts of the suffix unions U[k]
    for (let k = 1; k <= kK; k++) { const L = lists[k]; for (let i = 0; i < L.length; i++) maxSec[L[i]] = k; }
    const cntU = new Array(kK + 2);
    cntU[kK + 1] = new Int32Array(nZones);
    const run = new Int32Array(nZones);
    for (let k = kK; k >= 1; k--) {
      const L = lists[k];
      for (let i = 0; i < L.length; i++) { const n = L[i]; if (!inU[n]) { inU[n] = 1; run[zoneIdx[n]]++; } }
      cntU[k] = Int32Array.from(run);
    }
    const wC = oracle ? null : lam.wz(Math.floor(ctx.tEval / IV_MS));
    const wAt = t => (oracle ? lam.wzOracle(Math.floor(t / IV_MS)) : wC);

    // vehicle at main node M[k] after service (k < kK): sigma1 = section k+1, sigma2 = section k+2
    const atMain = (k, t, w) => {
      if (k >= kK || load[k + 1] >= cap) { if (onInstant) cPU.fill(0), cDO.fill(0); return 0; }
      const rowX = Mn[k] * N, L1 = lists[k + 1];
      const has2 = k + 2 <= kK;
      let d2 = -1, rowX2 = 0, mk2 = null;
      if (has2) { d2 = dMaxAdd(g, Md[k + 1] - t, OM); rowX2 = Mn[k + 1] * N; mk2 = masks[k + 2]; }
      cPU.fill(0);
      for (let i = 0; i < L1.length; i++) {
        const n = L1[i];
        if (D[rowX + n] <= dPU && !(has2 && mk2[n] === 1 && D[rowX2 + n] <= d2)) cPU[zoneIdx[n]]++;
      }
      if (has2 && d2 >= 0) { const L2 = lists[k + 2]; for (let i = 0; i < L2.length; i++) { const n = L2[i]; if (D[rowX2 + n] <= d2) cPU[zoneIdx[n]]++; } }
      cDO.set(cntU[k + 1]);
      return zsum(w.O, cPU) + zsum(w.D, cDO);
    };
    // vehicle at intermediate node x of section k: sigma1 = (x -> M[k]) with H[k], sigma2 = section k+1
    // (k = 0: x on the rest of the current arc, sigma1 = (x -> M[0]) with H[0], sigma2 = section 1)
    const atInter = (k, x, t, w) => {
      if (load[k] >= cap) { if (onInstant) cPU.fill(0), cDO.fill(0); return 0; }
      const b = Mn[k], rowX = x * N, rowB = b * N, dxb = D[rowX + b], dm = dMaxK[k];
      const has2 = k + 1 <= kK;
      let d2 = -1, mk2 = null;
      if (has2) { d2 = dMaxAdd(g, Md[k] - t, OM); mk2 = masks[k + 1]; }
      cPU.fill(0); cDO.set(cntU[k + 1]);
      const L = lists[k];
      for (let i = 0; i < L.length; i++) {
        const n = L[i], dxn = D[rowX + n];
        if (dxn + DT[rowB + n] - dxb <= dm) {
          if (maxSec[n] === k) cDO[zoneIdx[n]]++;
          if (dxn <= dPU && !(has2 && mk2[n] === 1 && D[rowB + n] <= d2)) cPU[zoneIdx[n]]++;
        }
      }
      if (has2 && d2 >= 0) { const L2 = lists[k + 1]; for (let i = 0; i < L2.length; i++) { const n = L2[i]; if (D[rowB + n] <= d2) cPU[zoneIdx[n]]++; } }
      return zsum(w.O, cPU) + zsum(w.D, cDO);
    };

    let Sw = 0, Sd = 0, tPrev = ctx.tEval, nInst = 0, sumDt = 0;
    const add = (val, t, waiting, k, kind, node) => {
      const dt = t - tPrev, c = val * dt / 1000;
      if (waiting) Sw += c; else Sd += c;
      sumDt += dt; tPrev = t; nInst++;
      if (onInstant) onInstant({ k, kind, node, t, dt, val, waiting, nPU: csum(cPU), nDO: csum(cDO) });
    };
    let nArc = 0;
    if (arc) {                                      // nodes of the current arc still ahead of the vehicle (section 0)
      const b0 = Mn[0], p = pathOf(NET, a0, b0), dab = D[a0 * N + b0], pm = arc.pm, span = Ma[0] - ctx.tEval;
      if (p[0] !== a0 || p[p.length - 1] !== b0) throw new Error('sscore: path ends');
      let tKept = tPrev;
      for (let j = 1; j < p.length - 1; j++) {
        const num = 1000 * D[a0 * N + p[j]] - pm * dab;   // > 0 <=> free-flow fraction of p[j] > pm / 1000 (exact integers)
        if (num <= 0) continue;
        const t = ctx.tEval + span * num / ((1000 - pm) * dab);
        if (stride && t - tKept < stride) continue;
        add(atInter(0, p[j], t, wAt(t)), t, true, 0, 'arc', p[j]);
        tKept = t; nArc++;
      }
    }
    add(atMain(0, Md[0], wAt(Md[0])), Md[0], 0 <= kP, 0, 'main', Mn[0]);
    for (let k = 1; k <= kK; k++) {
      const a = Mn[k - 1], b = Mn[k], p = pathOf(NET, a, b), dab = D[a * N + b];
      if (p[0] !== a || p[p.length - 1] !== b) throw new Error('sscore: path ends');
      let tKept = tPrev;
      for (let j = 1; j < p.length - 1; j++) {
        const t = Md[k - 1] + (Ma[k] - Md[k - 1]) * D[a * N + p[j]] / dab;
        if (stride && t - tKept < stride) continue;
        add(atInter(k, p[j], t, wAt(t)), t, k <= kP, k, 'inter', p[j]);
        tKept = t;
      }
      add(atMain(k, Md[k], wAt(Md[k])), Md[k], k <= kP, k, 'main', b);
    }
    if (Math.abs(sumDt - (Md[kK] - ctx.tEval)) > 1e-6) throw new Error('sscore: sum of instant weights != drop - tEval'); // T7
    // reset scratch
    for (let k = 1; k <= kK; k++) { const L = lists[k], mk = masks[k]; for (let i = 0; i < L.length; i++) { const n = L[i]; mk[n] = 0; maxSec[n] = 0; inU[n] = 0; } }
    if (arc) { const L = lists[0], mk = masks[0]; for (let i = 0; i < L.length; i++) mk[L[i]] = 0; stats.arcCalls++; stats.arcInstants += nArc; }
    stats.instants += nInst;
    stats.ns += process.hrtime.bigint() - h0;
    const S = Sw + Sd;
    return { S, Sw, Sd, n: nInst, kP, kK, nMain: Mn.length, nArc };
  }
  return { score, stats };
}

// ------------------------------------------------------------------------------------------------ reference (SPEC 4.8)
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

module.exports = { makeScorer, makeRefScorer, pathOf, dMaxMul, dMaxAdd, IV_MS };
