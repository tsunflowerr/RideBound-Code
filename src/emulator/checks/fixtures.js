// Section 9 re-checks: loader for the RideBound/FleetPy Manhattan fixture cells (<panel>/<cell>/scenario-content.json
// and normalizer-configuration.json, copied out of commit ebca912 by fetch_cells.js) and prepareFixtureCell, which
// turns one cell into the prepared scenario that engine.js simulates (the interface in the engine.js header).
// Traffic: data/factors.json holds the FleetPy day factors (96 bins of 15 min per day; see data/SOURCES.tsv). A cell
// starting in bin s sees f(b) = F[s + min(b, 8)] / F[s]: the anchored real-traffic world W5 (sigma 0, no shocks), which
// reads [start, start + 2 h] and holds the last value.
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const ID = require('../identity.js');
const { rqId, maxRideFromPermille } = require('../scenario.js');

const BIN_MS = 900000;
const HOLD_BIN = 8;
const FACTORS_FILE = path.join(__dirname, 'data', 'factors.json');
const PICK = 0, DROP = 1;

let factorCache = null;
function trafficProfile(cell, startBin) {
  if (!factorCache) factorCache = JSON.parse(fs.readFileSync(FACTORS_FILE, 'utf8'));
  const day = cell.slice(1, 5) + '-' + cell.slice(5, 7) + '-' + cell.slice(7, 9);
  const F = factorCache[day];
  if (!F || F.length !== 96) throw new Error('no factors for ' + day);
  if (startBin + HOLD_BIN >= 96) throw new Error('window past end of day');
  const prof = [];
  for (let b = 0; b <= HOLD_BIN; b++) prof.push(F[startBin + b] / F[startBin]);
  return prof;
}

function loadCell(cell, panelRoot) {
  const file = path.join(panelRoot, cell, 'scenario-content.json');
  const j = JSON.parse(fs.readFileSync(file, 'utf8'));
  const norm = JSON.parse(fs.readFileSync(path.join(panelRoot, cell, 'normalizer-configuration.json'), 'utf8'));
  const arcs = j.travelSnapshots[0].arcs;
  const nodeIds = [...new Set(arcs.flatMap(a => [a.fromNodeId, a.toNodeId]))].sort();
  const idx = new Map(nodeIds.map((n, i) => [n, i]));
  const N = nodeIds.length;
  const T0 = new Float64Array(N * N); // ms, fixture (bin 0) travel times
  for (const a of arcs) T0[idx.get(a.fromNodeId) * N + idx.get(a.toNodeId)] = a.travelTimeMs;
  const requests = [];
  for (const ev of j.events) {
    if (ev.eventType !== 'requestArrived') throw new Error('unexpected event ' + ev.eventType);
    const payload = Buffer.from(ev.payloadCanonicalJsonHex, 'hex');
    if (crypto.createHash('sha256').update(payload).digest('hex') !== ev.payloadSha256) throw new Error('payload hash');
    const r = JSON.parse(payload.toString('utf8')).request;
    if (r.arrivalTimeMs !== ev.simTimeMs) throw new Error('time mismatch');
    requests.push({
      id: r.requestId,
      rq: rqId(r.requestId),
      arrival: r.arrivalTimeMs,
      earliest: r.earliestPickupMs,
      latest: r.latestPickupMs,
      maxRide: r.maxRideTimeMs,
      o: idx.get(r.originNodeId),
      d: idx.get(r.destinationNodeId),
      party: r.partySize,
      direct: T0[idx.get(r.originNodeId) * N + idx.get(r.destinationNodeId)],
    });
  }
  const fleet = j.fleet.map(v => ({ id: v.vehicleId, node: idx.get(v.position.nodeId), capacity: v.capacity }))
    .sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  return { N, T0, requests, fleet, norm };
}

// The prepared scenario of fixture cell <panelRoot>/<cell> (as wf4/sim/sim.js prepareCell built it).
//   permille  maximum ride max(direct, halfEven(direct * permille / 1000)); default: the fixture's own permille
//   profile   travel factor per 15-min bin, held after the last one; default: trafficProfile from the cell's window
function prepareFixtureCell(cell, panelRoot, { permille, profile } = {}) {
  const base = loadCell(cell, panelRoot);
  const fixturePermille = base.norm.maximumRideTimePermille;
  if (!profile) {
    const startBin = Math.round(base.norm.sourceWindowStartSeconds / 900);
    if (startBin * 900 !== base.norm.sourceWindowStartSeconds) throw new Error('window start not on a bin');
    profile = trafficProfile(cell, startBin);
  }
  const H = profile.length - 1;
  const { N, T0 } = base;
  const snaps = profile.map(f => T0.map(ms => ID.snapshotMs(ms, f)));
  const nodeIds = Array.from({ length: N }, (_, i) => ID.nodeId(i));
  const reqs = base.requests.map(q => {
    if (maxRideFromPermille(q.direct, fixturePermille) !== q.maxRide) {
      throw new Error('fixture maxRide does not follow the normalizer rule');
    }
    return { ...q, maxRide: maxRideFromPermille(q.direct, permille || fixturePermille) };
  });
  reqs.sort((a, b) => a.arrival - b.arrival || (a.rq < b.rq ? -1 : 1));
  reqs.forEach((q, i) => {
    q.stopP = { r: i, kind: PICK, node: q.o, code: 2 * i, sid: ID.stopId(q.rq, 0),
      body: ID.stopBody(nodeIds[q.o], 0, q.rq, 0) };
    q.stopD = { r: i, kind: DROP, node: q.d, code: 2 * i + 1, sid: ID.stopId(q.rq, 1),
      body: ID.stopBody(nodeIds[q.d], 1, q.rq, 0) };
  });
  const snapHashes = new Map();
  const snapHash = bin => {
    if (!snapHashes.has(bin)) snapHashes.set(bin, ID.snapshotHash(N, T0, profile[Math.min(bin, H)], bin + 1, nodeIds));
    return snapHashes.get(bin);
  };
  return {
    N, T0, binMs: BIN_MS, reqs, fleet: base.fleet, dwell: 0,
    physFactor: bin => profile[Math.min(bin, H)],
    snapOf: bin => snaps[Math.min(bin, H)],
    snapHash,
    vehIdStr: base.fleet.map((f, vi) => ID.vehicleId(vi)),
    drainTimerMs: 600000, clockGuardMs: 40 * 3600 * 1000,
  };
}

// Per-rider rows of the archived real runs (data/costcap-riders.tsv).
function readRidersTsv(file) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean);
  const hdr = lines[0].split('\t');
  const ix = Object.fromEntries(hdr.map((h, i) => [h, i]));
  return lines.slice(1).map(l => {
    const f = l.split('\t');
    const num = k => (f[ix[k]] === '' ? NaN : Number(f[ix[k]]));
    return {
      role: f[ix.role], cell: f[ix.cell], rq: f[ix.requestId], life: f[ix.lifecycle],
      t0: num('t0'), p0: num('p0'), D0: num('D0'), A: num('A'), served: f[ix.served] === '1',
      S: num('sumAbsC'), k: num('nonzeroC'), net: num('netC'), M: num('maxAbsC'), shared: f[ix.shared] === '1',
    };
  });
}

module.exports = { prepareFixtureCell, readRidersTsv };
