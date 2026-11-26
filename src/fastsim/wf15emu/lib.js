// Shared helpers: fixture loading, id mapping.
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

// LOCAL COPY (2026-10-22, home machine): only these path constants differ from the bundle's code/wf5/runner/lib.js.
// SP was a folder of the machine that ran it. Fixtures come from the reference worktree at 7435661 (same committed bytes).
const SP = require('../paths.js').DATA;
const FIXTURE_ROOT = SP + '/wp9-confirmatory-fixed-panel-v2';   // default only: the Chapter 5 drivers always pass the fixture folder

const BIN_MS = 900000;

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

module.exports = {
  BIN_MS,
  loadCell,
};
