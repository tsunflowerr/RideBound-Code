// wf15 (SPEC 4.5): per-cell zone demand rates lambda_z (requests per second) for the shareability score.
//   observations  obs^i[z] = number of the cell's requests with arrivalMs in [i*I, (i+1)*I) and origin (destination) zone z
//                 (every request counts, rejected ones included; I = 600 000 ms; after the 2-h window obs = 0)
//   prior         lambda_hat^0[z] = phi * nPrev[z] / 3600, phi = sampled / eligible (cell provenance), nPrev = eligible Via
//                 requests of the same day's preceding hour (wf15/work/prior_prevhour.csv); fallback when sum nPrev = 0:
//                 (sampled / 7200) * |N_z| / N (counted in .fallbacks)
//   smoothing     lambda_hat^i[z] = a * obs^{i-1}[z] / (I/1000) + (1 - a) * lambda_hat^{i-1}[z]   (i >= 1, O and D apart)
//   causal use    a promise at tEval uses lambda_hat^{floor(tEval / I)} for every instant (flat SES forecast)
//   oracle use    (non-causal diagnostic, SPEC 6.4) an instant at t uses obs^{floor(t / I)} / (I/1000)
// Zone weights per node: w[n] = lambda[z(n)] / |N_z(n)| (the "z" arrays below are lambda_z / |N_z| per zone).
'use strict';

function makeLambda(o) {
  const { nZones, zoneSize, arrivals, zO, zD, priorO, priorD, phi, alpha, sampled } = o;
  const I = o.intervalMs || 600000, Isec = I / 1000;
  const N = o.N;
  if (!(alpha >= 0 && alpha <= 1)) throw new Error('lambda: alpha must be in [0, 1]');
  if (arrivals.length !== zO.length || zO.length !== zD.length) throw new Error('lambda: request arrays');
  // observations per interval (counts)
  const nIv = arrivals.length ? Math.floor(Math.max(...arrivals) / I) + 1 : 1;
  const obsO = [], obsD = [];
  for (let i = 0; i < nIv; i++) { obsO.push(new Float64Array(nZones)); obsD.push(new Float64Array(nZones)); }
  for (let k = 0; k < arrivals.length; k++) {
    const i = Math.floor(arrivals[k] / I);
    if (i < 0) throw new Error('lambda: negative arrival');
    obsO[i][zO[k]]++; obsD[i][zD[k]]++;
  }
  const zero = new Float64Array(nZones);
  const obs = (side, i) => (i < nIv ? (side === 'O' ? obsO[i] : obsD[i]) : zero);
  // prior
  const fallbacks = { O: 0, D: 0 };
  const prior = (counts, side) => {
    const out = new Float64Array(nZones);
    let tot = 0; for (let z = 0; z < nZones; z++) tot += counts[z];
    if (tot > 0) for (let z = 0; z < nZones; z++) out[z] = phi * counts[z] / 3600;
    else { fallbacks[side]++; for (let z = 0; z < nZones; z++) out[z] = (sampled / 7200) * zoneSize[z] / N; }
    return out;
  };
  const hatO = [prior(priorO, 'O')], hatD = [prior(priorD, 'D')];
  const hat = (side, i) => {
    const arr = side === 'O' ? hatO : hatD;
    while (arr.length <= i) {
      const j = arr.length, prev = arr[j - 1], ob = obs(side, j - 1), cur = new Float64Array(nZones);
      for (let z = 0; z < nZones; z++) cur[z] = alpha * ob[z] / Isec + (1 - alpha) * prev[z];
      arr.push(cur);
    }
    return arr[i];
  };
  const perZone = (lam) => { const w = new Float64Array(nZones); for (let z = 0; z < nZones; z++) w[z] = lam[z] / zoneSize[z]; return w; };
  const cacheC = new Map(), cacheX = new Map();
  // causal zone weights for interval i: { O, D } with lambda_hat^i[z] / |N_z|
  const wz = i => {
    if (i < 0) throw new Error('lambda: negative interval');
    let v = cacheC.get(i);
    if (!v) { v = { O: perZone(hat('O', i)), D: perZone(hat('D', i)) }; cacheC.set(i, v); }
    return v;
  };
  // oracle zone weights for interval i (realised rates)
  const wzOracle = i => {
    let v = cacheX.get(i);
    if (!v) {
      const lo = new Float64Array(nZones), ld = new Float64Array(nZones), a = obs('O', i), b = obs('D', i);
      for (let z = 0; z < nZones; z++) { lo[z] = a[z] / Isec; ld[z] = b[z] / Isec; }
      v = { O: perZone(lo), D: perZone(ld) }; cacheX.set(i, v);
    }
    return v;
  };
  return { intervalMs: I, wz, wzOracle, hat, obs, fallbacks, nIntervalsObserved: nIv };
}

module.exports = { makeLambda };
