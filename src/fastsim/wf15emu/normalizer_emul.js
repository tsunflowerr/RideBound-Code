// Exact emulation of the two normalizer outputs that a round-4 grid would change
// (repos/RideBound-run/src/RideBound.Benchmarking/Normalization/FleetPyManhattanNormalizer.cs):
//   maxRideTimeMs  = max(direct, ScaleIntegerTiesToEven(direct, MaximumRideTimePermille, 1000))   (lines 428-445, 1070-1086)
//   fleet placement = nodes of the selected pool ranked by HmacHex(SelectionKeyHex, "RideBound.Wp6.FleetPlacement.v1",
//                     sourceArtifactSha256, ScenarioId, node) then node, Take(VehicleCount), veh-{index:D3}  (lines 461-489)
//   HmacHex = HMAC-SHA256(key, utf8(domain + "\0") + for each value: int64 big-endian byte length + utf8 bytes) (1103-1120)
// Requests, request ids, node pool and travel arcs do not depend on ScenarioId, VehicleCount or the permille
// (ReadDemand 566-645, SelectRequests 647-757, PseudonymousRequestId 1088-1098); verify_fixtures.js checks this on the
// v2 and v3-veh4 panels, which differ in ScenarioId and VehicleCount only.
'use strict';
const crypto = require('crypto');

function hmacHex(keyHex, domain, values) {
  const parts = [Buffer.from(domain + '\0', 'utf8')];
  for (const v of values) {
    const b = Buffer.from(String(v), 'utf8');
    const len = Buffer.alloc(8);
    len.writeBigInt64BE(BigInt(b.length));
    parts.push(len, b);
  }
  return crypto.createHmac('sha256', Buffer.from(keyHex, 'hex')).update(Buffer.concat(parts)).digest('hex');
}

// nodeInts: integer source node ids of the selected pool (fixture node "node-001234" -> 1234)
function fleetPlacement(selectionKeyHex, sourceArtifactSha256, scenarioId, nodeInts, vehicleCount) {
  const ranked = nodeInts.map(node => ({ node, rank: hmacHex(selectionKeyHex, 'RideBound.Wp6.FleetPlacement.v1', [sourceArtifactSha256, scenarioId, String(node)]) }))
    .sort((a, b) => (a.rank < b.rank ? -1 : a.rank > b.rank ? 1 : a.node - b.node));
  return ranked.slice(0, vehicleCount).map((x, i) => ({ vehicleId: 'veh-' + String(i).padStart(3, '0'), node: x.node, provenance: 'derived-fleet-' + x.rank.slice(0, 24) }));
}

// decimal.Round(value * numerator / denominator, 0, MidpointRounding.ToEven) for non-negative integers
function scaleTiesToEven(value, numerator, denominator) {
  const n = BigInt(value) * BigInt(numerator);
  const d = BigInt(denominator);
  let q = n / d;
  const r2 = (n % d) * 2n;
  if (r2 > d || (r2 === d && q % 2n === 1n)) q += 1n;
  return Number(q);
}

function maxRideFromPermille(direct, permille) {
  return Math.max(direct, scaleTiesToEven(direct, permille, 1000));
}

const nodeInt = id => { const m = /^node-(\d+)$/.exec(id); if (!m) throw new Error('bad node id ' + id); return Number(m[1]); };

module.exports = { hmacHex, fleetPlacement, scaleTiesToEven, maxRideFromPermille, nodeInt };
