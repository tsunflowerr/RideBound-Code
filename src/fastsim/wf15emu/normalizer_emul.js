// Exact emulation of the normalizer output that a round-4 grid would change
// (repos/RideBound-run/src/RideBound.Benchmarking/Normalization/FleetPyManhattanNormalizer.cs):
//   maxRideTimeMs  = max(direct, ScaleIntegerTiesToEven(direct, MaximumRideTimePermille, 1000))   (lines 428-445, 1070-1086)
// Requests, request ids, node pool and travel arcs do not depend on ScenarioId, VehicleCount or the permille
// (ReadDemand 566-645, SelectRequests 647-757, PseudonymousRequestId 1088-1098); verify_fixtures.js checks this on the
// v2 and v3-veh4 panels, which differ in ScenarioId and VehicleCount only.
'use strict';

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

module.exports = { maxRideFromPermille };
