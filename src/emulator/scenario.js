// Scenario format v1 ("ridebound-screen-scenario/v1"): validation and preparation for engine.js. Data files are never
// executed: a scenario is parsed as JSON, validated field by field, and only numbers/strings are copied into typed
// arrays.
// What a scenario holds (every shipped cell uses exactly this):
//   nodes      [{id}] (other node fields, such as x/y/label, are ignored)
//   travel     base {kind: 'matrix', matrixFile}: N x N base travel ms, int32 little-endian, row-major (from -> to);
//              profile {binMs, factors, afterLast: 'hold'}: one speed factor per bin for the whole network, held after
//              the last bin; snapshotRounding 'round' or 'fleetpy-seconds-half-even' (required by identity mode
//              'ridebound-fleetpy-adapter'); forecast 'current-bin-flat'; closure 'none'
//   requests, fleet, service, identity {mode: 'plain' | 'ridebound-fleetpy-adapter'}
// Removed, and rejected when a scenario selects them: metric bases (haversine, euclidean and manhattan distances), the
// inline matrix, regional factor profiles (travel.profile.regions) and the shortest-path closure (Floyd-Warshall).
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const ID = require('./identity.js');

const FORMAT = 'ridebound-screen-scenario/v1';
const PICK = 0, DROP = 1;
const REMOVED_NOTE = 'removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)';

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
  }
  const tr = s.travel;
  if (!tr || !tr.base || !tr.profile) fail('travel.base and travel.profile are required');
  if (tr.base.kind === 'metric') {
    fail(`travel.base.kind metric (haversine, euclidean, manhattan distances): ${REMOVED_NOTE}`);
  }
  if (tr.base.kind !== 'matrix') fail('travel.base.kind must be matrix');
  if (tr.base.matrix) fail(`travel.base.matrix (inline matrix): ${REMOVED_NOTE}`);
  if (!isStr(tr.base.matrixFile)) fail('travel.base.matrixFile (N x N int32 little-endian ms)');
  const pr = tr.profile;
  if (!isInt(pr.binMs) || pr.binMs <= 0) fail('travel.profile.binMs');
  if (pr.regions) fail(`travel.profile.regions (regional factor profiles): ${REMOVED_NOTE}`);
  if (!Array.isArray(pr.factors) || !pr.factors.length || pr.factors.some(f => !isNum(f) || f <= 0)) {
    fail('travel.profile.factors: non-empty array of positive factors');
  }
  if ((pr.afterLast || 'hold') !== 'hold') fail('travel.profile.afterLast must be "hold"');
  if (!['fleetpy-seconds-half-even', 'round'].includes(tr.snapshotRounding || 'round')) fail('travel.snapshotRounding');
  if ((tr.forecast || 'current-bin-flat') !== 'current-bin-flat') fail('travel.forecast must be "current-bin-flat"');
  if (tr.closure === 'shortest-path') fail(`travel.closure shortest-path (Floyd-Warshall): ${REMOVED_NOTE}`);
  if ((tr.closure || 'none') !== 'none') fail('travel.closure must be none');
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
  if (idm === 'ridebound-fleetpy-adapter' && tr.snapshotRounding !== 'fleetpy-seconds-half-even') {
    fail('identity ridebound-fleetpy-adapter needs a uniform profile and snapshotRounding fleetpy-seconds-half-even');
  }
  return true;
}

// travel.base.matrixFile (relative to the scenario's folder) -> Float64Array N*N of base travel ms
function baseMatrix(s, scenarioDir) {
  const N = s.nodes.length;
  const T0 = new Float64Array(N * N);
  const file = path.resolve(scenarioDir || '.', s.travel.base.matrixFile);
  const buf = fs.readFileSync(file);
  if (buf.length !== N * N * 4) fail('matrixFile must hold N*N int32 little-endian ms values');
  for (let k = 0; k < N * N; k++) T0[k] = buf.readInt32LE(k * 4);
  for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) { const x = T0[i * N + j]; if (x < 0 || (i === j) !== (x === 0)) fail(`matrixFile[${i}][${j}]`); }
  return T0;
}

// Maximum ride time of the C# normalizer: max(direct, direct * permille / 1000 rounded half to even), exact in
// integers.
function maxRideFromPermille(direct, permille) {
  const n = BigInt(direct) * BigInt(permille);
  let q = n / 1000n;
  const r2 = (n % 1000n) * 2n;
  if (r2 > 1000n || (r2 === 1000n && q % 2n === 1n)) q += 1n;
  return Math.max(direct, Number(q));
}

// Request id of the FleetPy adapter (identity mode ridebound-fleetpy-adapter): "rq-" + sha256 of the typed frame of the
// raw id: 's', its utf8 byte length as 8 bytes big-endian, the utf8 bytes (adapter mapping.py, namespace "rq").
function rqId(raw) {
  const enc = Buffer.from(raw, 'utf8');
  const len = Buffer.alloc(8);
  len.writeBigUInt64BE(BigInt(enc.length));
  const framed = Buffer.concat([Buffer.from('s'), len, enc]);
  return 'rq-' + crypto.createHash('sha256').update(framed).digest('hex');
}

// overrides: { maxRidePermille, maxRidePlusMs, nVeh, dwellMs }
function prepareScenario(s, overrides = {}, scenarioDir) {
  validateScenario(s);
  const N = s.nodes.length;
  const nodeIndex = new Map(s.nodes.map((n, i) => [n.id, i]));
  const T0 = baseMatrix(s, scenarioDir);
  const binMs = s.travel.profile.binMs;
  const factors = s.travel.profile.factors;
  const maxBins = factors.length;
  const fac = bin => factors[Math.min(bin, maxBins - 1)];   // held after the last bin
  const rounding = s.travel.snapshotRounding || 'round';
  // dispatcher snapshot of a bin: every base time x the bin's factor, rounded to ms (the last 3 bins are cached)
  const snapCache = new Map();
  const snapOf = bin => {
    const b = Math.min(bin, maxBins - 1);
    if (snapCache.has(b)) return snapCache.get(b);
    const f = fac(b);
    const a = new Float64Array(N * N);
    for (let k = 0; k < N * N; k++) a[k] = rounding === 'round' ? Math.round(T0[k] * f) : ID.snapshotMs(T0[k], f);
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
    const h = idm === 'ridebound-fleetpy-adapter'
      ? ID.snapshotHash(N, T0, fac(b), bin + 1, nodeIdStr)
      : crypto.createHash('sha256').update(`${s.scenarioId}\0snapshot\0${b}\0${bin + 1}`).digest('hex');
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
      maxRide = maxRideFromPermille(direct, permille || sv.maxRidePermille);
      const plus = plusMs !== null ? plusMs : (sv.maxRidePlusMs || 0);
      if (plus) maxRide = Math.max(maxRide, direct + plus);
    }
    if (latest < earliest) fail(`request ${q.id}: latest < earliest`);
    const rq = idm === 'ridebound-fleetpy-adapter' ? rqId(q.id) : 'rq-' + crypto.createHash('sha256').update(q.id).digest('hex');
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
    physFactor: bin => fac(bin), maxBins,
    snapOf, snapHash, reqs, fleet, dwell,
    vehIdStr: fleet.map((f, vi) => ID.vehicleId(vi)),
    drainTimerMs: sv.decisionTimerMs || 600000,
    clockGuardMs: s.horizon.endMs + s.horizon.drainMs + 24 * 3600 * 1000,
    meta: { permille: permille || sv.maxRidePermille, identity: idm, rounding, provenance: s.provenance },
  };
}

module.exports = { FORMAT, validateScenario, prepareScenario, maxRideFromPermille, rqId };
