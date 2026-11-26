// wf20/fullsys COPY of wf17/r1-identity-fence-pad/code/scripts/shcore.js (label wf20-fullsys-exploratory-post-audit).
// wf17 COPY (r1-identity-fence-pad) of wf15/scripts/shcore.js.
// wf15 worker-side core. Builds the cell's shareability-score context
// (full-network scale kappa*f_b, local->global node map, causal/oracle lambda tables).
'use strict';
const fs = require('fs'), path = require('path');
const SS = require('../runner/sscore.js');
const LAM = require('../runner/lambda.js');

const OMEGA_P = 600000;

function readCell(file, truncateMs) {
  const s = JSON.parse(fs.readFileSync(file, 'utf8'));
  const dir = path.dirname(file);
  const prov = JSON.parse(fs.readFileSync(path.resolve(dir, s.provenance.provenanceFile), 'utf8'));
  if (truncateMs !== undefined && truncateMs !== null) s.requests = s.requests.filter(q => q.arrivalMs < truncateMs);
  return { s, prov, dir };
}

// lambda inputs from the cell's own request stream (all requests, rejected ones included)
function lambdaInputs(s, NET) {
  const idx = new Map(s.nodes.map((n, i) => [n.id, Number(n.id.slice(1))]));
  const n = s.requests.length, arrivals = new Float64Array(n), zO = new Int32Array(n), zD = new Int32Array(n);
  s.requests.forEach((q, k) => { arrivals[k] = q.arrivalMs; zO[k] = NET.zoneIdx[idx.get(q.origin)]; zD[k] = NET.zoneIdx[idx.get(q.destination)]; });
  return { arrivals, zO, zD };
}

function makeCellCtx(rc, NET, task, mode) {
  const { s, prov } = rc;
  if (s.service.pickupWindowMs !== OMEGA_P) throw new Error('pickup window is not 600 s');
  const globalOf = Int32Array.from(s.nodes.map(n => Number(n.id.slice(1))));
  const kappa = prov.travel.F_windowStart, factors = s.travel.profile.factors;
  const li = lambdaInputs(s, NET);
  if (!task.prior) throw new Error('task.prior missing for ' + task.cell);
  if (!(task.alphaS >= 0 && task.alphaS <= 1)) throw new Error('task.alphaS missing');
  const lam = LAM.makeLambda({ nZones: NET.nZones, zoneSize: NET.zoneSize, N: NET.N, arrivals: li.arrivals, zO: li.zO, zD: li.zD,
    priorO: task.prior.O, priorD: task.prior.D, phi: prov.sampling.sampled / prov.sampling.eligible, sampled: prov.sampling.sampled,
    alpha: task.alphaS, intervalMs: SS.IV_MS });
  return { kappa, factors, globalOf, omegaP: OMEGA_P, strideMs: task.strideMs || 0, lam, mode };
}

module.exports = { readCell, makeCellCtx };
