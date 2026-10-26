// Shared helpers: fixture loading, traffic profile, id mapping, statistics.
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const FIXTURE_ROOT = SP + '/repos/RideBound-run/benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/wp9-confirmatory-fixed-panel-v2';
const FACTORS_FILE = SP + '/wf2/theory-P8/factors.json';
const RIDERS_TSV = SP + '/step0/src/costcap/riders.tsv';
const RESULTS_JSON = SP + '/step0/src/costcap/results.json';

const DAYS = ['20181114', '20181115', '20181116', '20181117', '20181118'];
const CELLS = [];
for (const d of DAYS) for (const r of [1, 2, 3, 4]) CELLS.push(`d${d}-s10-r${r}`);

const BIN_MS = 900000;
const WINDOW_START_BIN = 32; // 08:00 = 28800 s = bin 32 of the day
const HOLD_BIN = 8;          // measured_base reads [start, start+7200] and holds the last value

let factorCache = null;
function loadFactors() {
  if (!factorCache) factorCache = JSON.parse(fs.readFileSync(FACTORS_FILE, 'utf8'));
  return factorCache;
}

// Anchored real-traffic-only world (W5: sigma 0, shockRate 0, exponent 1, windowStart 28800):
// f(b) = F[32 + min(b, 8)] / F[32]  (preflight_world.py:67-93 measured_base; world_v1.py:103-120 AnchoredWorld)
// startBin: 15-min bin of the world windowStart (32 = 08:00 for the w08 panel; 68 = 17:00 for w17 fresh cells).
function trafficProfile(cell, startBin = WINDOW_START_BIN) {
  const day = cell.slice(1, 5) + '-' + cell.slice(5, 7) + '-' + cell.slice(7, 9);
  const F = loadFactors()[day];
  if (!F || F.length !== 96) throw new Error('no factors for ' + day);
  if (startBin + HOLD_BIN >= 96) throw new Error('window past end of day');
  const base = F[startBin];
  const prof = [];
  for (let b = 0; b <= HOLD_BIN; b++) prof.push(F[startBin + b] / base);
  return prof; // index by min(bin, 8)
}

// panelRoot: fixture panel folder (default: the frozen w08 panel A, wp9-confirmatory-fixed-panel-v2)
function loadCell(cell, panelRoot = FIXTURE_ROOT) {
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
  return { cell, N, T0, requests, fleet, norm, timeWindow: j.timeWindow, nodeIds, sourceArtifactSha256: j.sourceArtifactSha256, scenarioId: j.scenarioId };
}

// Adapter request id: "rq-" + sha256(typed frame of the FleetPy raw id) (mapping.py:20-77, namespace "rq").
function rqId(raw) {
  const enc = Buffer.from(raw, 'utf8');
  const len = Buffer.alloc(8);
  len.writeBigUInt64BE(BigInt(enc.length));
  const framed = Buffer.concat([Buffer.from('s'), len, enc]);
  return 'rq-' + crypto.createHash('sha256').update(framed).digest('hex');
}

// Round half to even (Python Decimal ROUND_HALF_EVEN / C# banker's rounding used by the mapper).
function roundHalfEven(x) {
  const f = Math.floor(x);
  const d = x - f;
  if (d > 0.5) return f + 1;
  if (d < 0.5) return f;
  return f % 2 === 0 ? f : f + 1;
}

function quantile(arr, p) {
  if (!arr.length) return NaN;
  const s = Float64Array.from(arr).sort();
  const i = (s.length - 1) * p;
  const lo = Math.floor(i), hi = Math.ceil(i);
  return s[lo] + (s[hi] - s[lo]) * (i - lo);
}

function readRidersTsv(file = RIDERS_TSV) {
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

// Fresh cells (not part of panel A, no archived runs): wp14 development panel, days 12/13 Nov, windows 08:00 and 17:00.
const FRESH_ROOT = SP + '/repos/RideBound-run/benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/wp14-development-panel-v1';
const FRESH_CELLS = [];
for (const d of ['20181112', '20181113']) for (const r of [1, 2, 3, 4]) for (const w of ['w08', 'w17']) FRESH_CELLS.push(`d${d}-s10-r${r}-${w}`);

module.exports = {
  SP, FIXTURE_ROOT, FRESH_ROOT, FRESH_CELLS, CELLS, DAYS, BIN_MS, HOLD_BIN, RIDERS_TSV, RESULTS_JSON,
  trafficProfile, loadCell, rqId, roundHalfEven, quantile, readRidersTsv, loadFactors,
};
