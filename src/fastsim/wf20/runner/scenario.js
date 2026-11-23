// Generic scenario format v1 ("ridebound-screen-scenario/v1"): validation and preparation for engine.js.
// Specification: README_SCENARIO.md; JSON schema: scenario.schema.json. Data files are never executed: a scenario is
// parsed as JSON, validated field by field, and only numbers/strings are copied into typed arrays.
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const ID = require('./identity.js');
const NE = require('./normalizer_emul.js');
const L = require('./lib.js');

const FORMAT = 'ridebound-screen-scenario/v1';
const PICK = 0, DROP = 1;
const EARTH_R = 6371008.8;

function fail(msg) { throw new Error('scenario v1: ' + msg); }
const isInt = x => Number.isInteger(x);
const isNum = x => typeof x === 'number' && Number.isFinite(x);
const isStr = x => typeof x === 'string' && x.length > 0;

function validateScenario(s) {
  if (!s || typeof s !== 'object') fail('not an object');
  if (s.format !== FORMAT) fail(`format must be "${FORMAT}"`);
  if (!isStr(s.scenarioId)) fail('scenarioId');
  if (s.family !== undefined && !isStr(s.family)) fail('family must be a string');
  if (!s.provenance || !isStr(s.provenance.source) || !isStr(s.provenance.licence)) fail('provenance.source and provenance.licence are required');
  const h = s.horizon;
  if (!h || h.startMs !== 0 || !isInt(h.endMs) || h.endMs <= 0 || !isInt(h.drainMs) || h.drainMs < 0) fail('horizon {startMs: 0, endMs > 0, drainMs >= 0} (times are ms from the scenario start)');
  if (!Array.isArray(s.nodes) || s.nodes.length < 2) fail('nodes: array of >= 2');
  const ids = new Set();
  for (const n of s.nodes) {
    if (!n || !isStr(n.id)) fail('node id');
    if (ids.has(n.id)) fail('duplicate node id ' + n.id);
    ids.add(n.id);
    if (n.lat !== undefined && (!isNum(n.lat) || !isNum(n.lon))) fail('node lat/lon');
    if (n.x !== undefined && (!isNum(n.x) || !isNum(n.y))) fail('node x/y');
    if (n.region !== undefined && !isStr(n.region)) fail('node region must be a string');
  }
  const tr = s.travel;
  if (!tr || !tr.base || !tr.profile) fail('travel.base and travel.profile are required');
  if (tr.base.kind === 'matrix') {
    if (!Array.isArray(tr.base.matrix) && !isStr(tr.base.matrixFile)) fail('travel.base.matrix (N x N ms) or matrixFile');
  } else if (tr.base.kind === 'metric') {
    if (!['haversine', 'euclidean', 'manhattan'].includes(tr.base.distance)) fail('travel.base.distance');
    if (!isNum(tr.base.detourFactor) || tr.base.detourFactor < 1) fail('travel.base.detourFactor >= 1');
    if (!isNum(tr.base.speedMps) || tr.base.speedMps <= 0) fail('travel.base.speedMps > 0');
  } else fail('travel.base.kind must be matrix or metric');
  const pr = tr.profile;
  if (!isInt(pr.binMs) || pr.binMs <= 0) fail('travel.profile.binMs');
  const checkF = (arr, what) => { if (!Array.isArray(arr) || !arr.length || arr.some(f => !isNum(f) || f <= 0)) fail(what + ': non-empty array of positive factors'); };
  if (pr.regions) {
    if (!pr.regions.factors || typeof pr.regions.factors !== 'object') fail('travel.profile.regions.factors');
    for (const [k, arr] of Object.entries(pr.regions.factors)) checkF(arr, 'regions.factors.' + k);
    for (const n of s.nodes) if (!n.region || !pr.regions.factors[n.region]) fail(`node ${n.id} has no region with a factor profile`);
  } else checkF(pr.factors, 'travel.profile.factors');
  if ((pr.afterLast || 'hold') !== 'hold') fail('travel.profile.afterLast must be "hold"');
  if (!['fleetpy-seconds-half-even', 'round'].includes(tr.snapshotRounding || 'round')) fail('travel.snapshotRounding');
  if ((tr.forecast || 'current-bin-flat') !== 'current-bin-flat') fail('travel.forecast must be "current-bin-flat"');
  if (!['none', 'shortest-path'].includes(tr.closure || 'none')) fail('travel.closure must be none or shortest-path');
  if (tr.closure === 'shortest-path' && tr.snapshotRounding === 'fleetpy-seconds-half-even') fail('travel.closure shortest-path needs snapshotRounding round');
  if (!Array.isArray(s.requests) || !s.requests.length) fail('requests');
  const rids = new Set();
  for (const q of s.requests) {
    if (!isStr(q.id) || rids.has(q.id)) fail('request id missing or duplicate: ' + q.id);
    rids.add(q.id);
    if (!isInt(q.arrivalMs) || q.arrivalMs < 0 || q.arrivalMs > h.endMs) fail(`request ${q.id} arrivalMs outside [0, endMs]`);
    if (!ids.has(q.origin) || !ids.has(q.destination)) fail(`request ${q.id} origin/destination not a node`);
    if (q.origin === q.destination) fail(`request ${q.id} origin == destination`);
    for (const k of ['earliestPickupMs', 'latestPickupMs', 'maxRideMs']) if (q[k] !== undefined && !isInt(q[k])) fail(`request ${q.id} ${k}`);
    if (q.partySize !== undefined && (!isInt(q.partySize) || q.partySize < 1)) fail(`request ${q.id} partySize`);
  }
  if (!Array.isArray(s.fleet) || !s.fleet.length) fail('fleet');
  const vids = new Set();
  for (const v of s.fleet) {
    if (!isStr(v.id) || vids.has(v.id)) fail('vehicle id missing or duplicate');
    vids.add(v.id);
    if (!ids.has(v.startNode)) fail(`vehicle ${v.id} startNode`);
    if (!isInt(v.capacity) || v.capacity < 1) fail(`vehicle ${v.id} capacity`);
  }
  const sv = s.service;
  if (!sv || !isInt(sv.pickupWindowMs) || sv.pickupWindowMs < 0) fail('service.pickupWindowMs');
  if (!isInt(sv.maxRidePermille) || sv.maxRidePermille < 1000) fail('service.maxRidePermille >= 1000');
  for (const k of ['maxRidePlusMs', 'dwellMs', 'decisionTimerMs']) if (sv[k] !== undefined && (!isInt(sv[k]) || sv[k] < 0)) fail('service.' + k);
  const idm = (s.identity && s.identity.mode) || 'plain';
  if (!['plain', 'ridebound-fleetpy-adapter'].includes(idm)) fail('identity.mode');
  if (idm === 'ridebound-fleetpy-adapter' && (pr.regions || tr.snapshotRounding !== 'fleetpy-seconds-half-even')) fail('identity ridebound-fleetpy-adapter needs a uniform profile and snapshotRounding fleetpy-seconds-half-even');
  return true;
}

function baseMatrix(s, scenarioDir) {
  const N = s.nodes.length;
  const T0 = new Float64Array(N * N);
  const b = s.travel.base;
  if (b.kind === 'matrix') {
    if (b.matrix) {
      if (b.matrix.length !== N) fail('matrix rows != nodes');
      for (let i = 0; i < N; i++) {
        const row = b.matrix[i];
        if (!Array.isArray(row) || row.length !== N) fail('matrix row ' + i);
        for (let j = 0; j < N; j++) {
          const x = row[j];
          if (!isInt(x) || x < 0) fail(`matrix[${i}][${j}] must be a non-negative integer ms`);
          if ((i === j) !== (x === 0)) fail(`matrix[${i}][${j}]: zero exactly on the diagonal`);
          T0[i * N + j] = x;
        }
      }
    } else {
      const file = path.resolve(scenarioDir || '.', b.matrixFile);
      const buf = fs.readFileSync(file);
      if (buf.length !== N * N * 4) fail('matrixFile must hold N*N int32 little-endian ms values');
      for (let k = 0; k < N * N; k++) T0[k] = buf.readInt32LE(k * 4);
      for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) { const x = T0[i * N + j]; if (x < 0 || (i === j) !== (x === 0)) fail(`matrixFile[${i}][${j}]`); }
    }
  } else {
    const dist = (p, q) => {
      if (b.distance === 'haversine') {
        if (p.lat === undefined || q.lat === undefined) fail('haversine needs lat/lon on every node');
        const r = Math.PI / 180, dLat = (q.lat - p.lat) * r, dLon = (q.lon - p.lon) * r;
        const a = Math.sin(dLat / 2) ** 2 + Math.cos(p.lat * r) * Math.cos(q.lat * r) * Math.sin(dLon / 2) ** 2;
        return 2 * EARTH_R * Math.asin(Math.min(1, Math.sqrt(a)));
      }
      if (p.x === undefined || q.x === undefined) fail(b.distance + ' needs x/y (metres) on every node');
      return b.distance === 'euclidean' ? Math.hypot(q.x - p.x, q.y - p.y) : Math.abs(q.x - p.x) + Math.abs(q.y - p.y);
    };
    const minMs = b.minMs || 1000;
    for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) {
      if (i === j) continue;
      T0[i * N + j] = Math.max(minMs, Math.round(dist(s.nodes[i], s.nodes[j]) * b.detourFactor / b.speedMps * 1000));
    }
  }
  return T0;
}

// overrides: { maxRidePermille, maxRidePlusMs, nVeh, dwellMs }
function prepareScenario(s, overrides = {}, scenarioDir) {
  validateScenario(s);
  const N = s.nodes.length;
  const nodeIndex = new Map(s.nodes.map((n, i) => [n.id, i]));
  const T0 = baseMatrix(s, scenarioDir);
  const pr = s.travel.profile;
  const binMs = pr.binMs;
  const regional = !!pr.regions;
  const regionNames = regional ? [...new Set(s.nodes.map(n => n.region))].sort() : ['*'];
  const regionOf = regional ? Int32Array.from(s.nodes.map(n => regionNames.indexOf(n.region))) : new Int32Array(N);
  const prof = regional ? regionNames.map(r => pr.regions.factors[r]) : [pr.factors];
  const fac = (bin, reg) => { const a = prof[reg]; return a[Math.min(bin, a.length - 1)]; };
  const maxBins = Math.max(...prof.map(a => a.length));
  const rounding = s.travel.snapshotRounding || 'round';
  const closure = s.travel.closure || 'none';
  if (closure === 'shortest-path') floydWarshall(T0, N); // base (and hence direct times) closed first
  // closure shortest-path: each bin's scaled matrix is closed under Floyd-Warshall and used for BOTH physical motion and
  // the dispatcher snapshot (a vehicle drives the fastest path; projections stay consistent with motion).
  const physCache = new Map();
  const physMat = closure === 'none' ? null : bin => {
    const b = Math.min(bin, maxBins - 1);
    if (physCache.has(b)) return physCache.get(b);
    const a = new Float64Array(N * N);
    for (let i = 0; i < N; i++) { const f = fac(b, regionOf[i]); for (let j = 0; j < N; j++) a[i * N + j] = T0[i * N + j] * f; }
    floydWarshall(a, N);
    if (physCache.size >= 3) physCache.delete(physCache.keys().next().value);
    physCache.set(b, a);
    return a;
  };
  const snapCache = new Map();
  const snapOf = bin => {
    const b = Math.min(bin, maxBins - 1);
    if (snapCache.has(b)) return snapCache.get(b);
    const a = new Float64Array(N * N);
    if (physMat) { const m = physMat(b); for (let k = 0; k < N * N; k++) a[k] = Math.round(m[k]); }
    else for (let i = 0; i < N; i++) {
      const f = fac(b, regionOf[i]);
      for (let j = 0; j < N; j++) {
        const x = T0[i * N + j];
        a[i * N + j] = rounding === 'round' ? Math.round(x * f) : ID.snapshotMs(x, f);
      }
    }
    if (snapCache.size >= 3) snapCache.delete(snapCache.keys().next().value);
    snapCache.set(b, a);
    return a;
  };
  const idm = (s.identity && s.identity.mode) || 'plain';
  const nodeIdStr = Array.from({ length: N }, (_, i) => ID.nodeId(i));
  const snapHashCache = new Map();
  const snapHash = bin => {
    const b = Math.min(bin, maxBins - 1);
    if (snapHashCache.has(bin)) return snapHashCache.get(bin);
    let h;
    if (idm === 'ridebound-fleetpy-adapter') {
      if (regional || rounding !== 'fleetpy-seconds-half-even') fail('identity ridebound-fleetpy-adapter needs a uniform profile and fleetpy rounding');
      h = ID.snapshotHash(N, T0, fac(b, 0), bin + 1, nodeIdStr);
    } else h = crypto.createHash('sha256').update(`${s.scenarioId}\0snapshot\0${b}\0${bin + 1}`).digest('hex');
    snapHashCache.set(bin, h);
    return h;
  };
  const sv = s.service;
  const permille = overrides.maxRidePermille || null;
  const plusMs = overrides.maxRidePlusMs !== undefined ? overrides.maxRidePlusMs : null;
  const dwell = overrides.dwellMs !== undefined ? overrides.dwellMs : (sv.dwellMs || 0);
  const reqs = s.requests.map(q => {
    const o = nodeIndex.get(q.origin), d = nodeIndex.get(q.destination);
    const direct = T0[o * N + d];
    const earliest = q.earliestPickupMs !== undefined ? q.earliestPickupMs : q.arrivalMs;
    const latest = q.latestPickupMs !== undefined ? q.latestPickupMs : earliest + sv.pickupWindowMs;
    let maxRide;
    if (permille === null && plusMs === null && q.maxRideMs !== undefined) maxRide = q.maxRideMs;
    else {
      maxRide = NE.maxRideFromPermille(direct, permille || sv.maxRidePermille);
      const plus = plusMs !== null ? plusMs : (sv.maxRidePlusMs || 0);
      if (plus) maxRide = Math.max(maxRide, direct + plus);
    }
    if (latest < earliest) fail(`request ${q.id}: latest < earliest`);
    const rq = idm === 'ridebound-fleetpy-adapter' ? L.rqId(q.id) : 'rq-' + crypto.createHash('sha256').update(q.id).digest('hex');
    return { id: q.id, rq, arrival: q.arrivalMs, earliest, latest, maxRide, o, d, party: q.partySize || 1, direct };
  });
  reqs.sort((a, b) => a.arrival - b.arrival || (a.rq < b.rq ? -1 : 1));
  reqs.forEach((q, i) => {
    q.stopP = { r: i, kind: PICK, node: q.o, code: 2 * i, sid: ID.stopId(q.rq, 0), body: ID.stopBody(nodeIdStr[q.o], 0, q.rq, dwell) };
    q.stopD = { r: i, kind: DROP, node: q.d, code: 2 * i + 1, sid: ID.stopId(q.rq, 1), body: ID.stopBody(nodeIdStr[q.d], 1, q.rq, dwell) };
  });
  let fleet = s.fleet.map(v => ({ id: v.id, node: nodeIndex.get(v.startNode), capacity: v.capacity }))
    .sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  if (overrides.nVeh) fleet = fleet.slice(0, overrides.nVeh);
  return {
    id: s.scenarioId, family: s.family || s.scenarioId, source: 'generic-v1', N, T0, binMs,
    physFactor: (bin, from) => fac(bin, regional ? regionOf[from] : 0),
    physMat: physMat || undefined, maxBins,
    snapOf, snapHash, reqs, fleet, dwell,
    vehIdStr: fleet.map((f, vi) => ID.vehicleId(vi)),
    drainTimerMs: sv.decisionTimerMs || 600000,
    clockGuardMs: s.horizon.endMs + s.horizon.drainMs + 24 * 3600 * 1000,
    meta: { permille: permille || sv.maxRidePermille, identity: idm, regional, rounding, provenance: s.provenance },
  };
}

function floydWarshall(a, N) {
  for (let k = 0; k < N; k++) {
    const rk = k * N;
    for (let i = 0; i < N; i++) {
      if (i === k) continue;
      const ri = i * N, dik = a[ri + k];
      for (let j = 0; j < N; j++) { const v = dik + a[rk + j]; if (v < a[ri + j]) a[ri + j] = v; }
    }
  }
}

// Diagnostic: triangle-inequality violations of the DISPATCHER snapshot, t(i,k) + t(k,j) <= t(i,j) - 1000 ms.
// On a non-metric snapshot insertion-only dispatch can produce negative c without any re-optimization (a detour through the
// inserted stop is faster), so a public matrix must pass this check (or use travel.closure = shortest-path) before S2 is read.
// triangleStats(P, bins = 'all', opts) :
//   bins     'all' (default) = every bin 0 .. nBins-1 (nBins = P.maxBins, or opts.nBins, or 96), deduplicated by the
//            per-node factor signature (two bins with the same factors give the same snapshot); or an explicit bin list.
//   opts     { mode: 'auto' (default: exhaustive when N^3 <= opts.exhaustiveMax = 6.4e7, i.e. N <= 400, else sampled) |
//              'exhaustive' | 'sampled', samples: 200000 (sampled mode, per bin), seed: 7, nBins }
//            (legacy call triangleStats(P, bins, samples, seed) still works and forces sampled mode).
// Returns { mode, N, binsRequested, snapshotsChecked, bins: {bin: {triples, violations, violationShare, worstGapS}},
//           summary: {violations, maxViolationShare, worstGapS, binsWithViolations} }.
// Exhaustive cost: N^3 per distinct snapshot (N = 263 -> 1.8e7, ~0.1 s each).
function triangleStats(P, bins = 'all', opts = {}, legacySeed) {
  if (typeof opts === 'number') opts = { mode: 'sampled', samples: opts, seed: legacySeed };
  const N = P.N;
  const mode0 = opts.mode || 'auto';
  const exhaustive = mode0 === 'exhaustive' || (mode0 === 'auto' && N * N * N <= (opts.exhaustiveMax || 6.4e7));
  const samples = opts.samples || 200000;
  let s = (opts.seed || 7) >>> 0 || 1;
  const rnd = () => { s ^= s << 13; s >>>= 0; s ^= s >>> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; };
  const nBins = opts.nBins || P.maxBins || 96;
  let list = bins === 'all' ? Array.from({ length: nBins }, (_, b) => b) : bins.slice();
  // dedupe bins by factor signature (identical factors -> identical snapshot)
  const seen = new Map();
  const groups = [];
  for (const b of list) {
    let sig;
    try { sig = Array.from({ length: N }, (_, i) => P.physFactor(b, i)).join(','); } catch (e) { sig = 'bin' + b; }
    if (seen.has(sig)) { seen.get(sig).push(b); continue; }
    const g = [b]; seen.set(sig, g); groups.push(g);
  }
  const out = { mode: exhaustive ? 'exhaustive' : 'sampled', N, binsRequested: list.length, snapshotsChecked: groups.length, bins: {}, summary: { violations: 0, maxViolationShare: 0, worstGapS: 0, binsWithViolations: [] } };
  for (const g of groups) {
    const b = g[0];
    const m = P.snapOf(b);
    let viol = 0, worst = 0, n = 0;
    if (exhaustive) {
      for (let i = 0; i < N; i++) {
        const ri = i * N;
        for (let k = 0; k < N; k++) {
          if (k === i) continue;
          const dik = m[ri + k], rk = k * N;
          for (let j = 0; j < N; j++) {
            if (j === i || j === k) continue;
            const gap = m[ri + j] - (dik + m[rk + j]);
            if (gap >= 1000) { viol++; if (gap > worst) worst = gap; }
          }
        }
      }
      n = N * (N - 1) * (N - 2);
    } else {
      for (let t = 0; t < samples; t++) {
        const i = Math.floor(rnd() * N), j = Math.floor(rnd() * N), k = Math.floor(rnd() * N);
        if (i === j || k === i || k === j) continue;
        n++;
        const gap = m[i * N + j] - (m[i * N + k] + m[k * N + j]);
        if (gap >= 1000) { viol++; if (gap > worst) worst = gap; }
      }
    }
    const rec = { triples: n, violations: viol, violationShare: n ? viol / n : 0, worstGapS: worst / 1000, sameSnapshotBins: g };
    out.bins[b] = rec;
    out.summary.violations += viol;
    if (rec.violationShare > out.summary.maxViolationShare) out.summary.maxViolationShare = rec.violationShare;
    if (rec.worstGapS > out.summary.worstGapS) out.summary.worstGapS = rec.worstGapS;
    if (viol) out.summary.binsWithViolations.push(...g);
  }
  return out;
}

function loadScenarioFile(file, overrides) {
  const s = JSON.parse(fs.readFileSync(file, 'utf8'));
  return prepareScenario(s, overrides, path.dirname(file));
}

module.exports = { FORMAT, validateScenario, prepareScenario, loadScenarioFile, triangleStats, floydWarshall };
