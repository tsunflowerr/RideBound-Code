// The golden files of the C# tests (src/core/tests) that the emulator tests read too, so that the emulator and the C#
// code are checked against the same numbers. Not a test file: node --test runs only the *.test.js files here.
'use strict';
const path = require('path');
const NETWORK = require('../network.js');

const CORE_TESTS = path.join(__dirname, '..', '..', 'core', 'tests');
// 40 random forecast profiles x 75 legs of the C# TravelTimeForecast test
const FORECAST_GOLDEN = path.join(CORE_TESTS, 'RideBound.Domain.Tests', 'Validation', 'travel-forecast-golden.json');
// 24 small networks with 10 shareability scores each, written by export_network.js toy (C# ShareabilityScorer test)
const SHAREABILITY_GOLDEN = path.join(CORE_TESTS, 'RideBound.Algorithms.Tests', 'Shareability',
  'shareability-golden-toy.json');

// The network of one cell of the shareability golden, rebuilt by network.toy() from the cell's edge list (csrStart,
// csrTo, csrW: the edges leaving node u are k = csrStart[u] .. csrStart[u+1]-1). The golden numbers the zones 0, 1, ...
// (zoneIdx), so those indices serve as the zone ids.
function toyNetworkOf(cell) {
  const { N, csrStart, csrTo, csrW, zoneIdx } = cell.net;
  const edges = [];
  for (let u = 0; u < N; u++) for (let k = csrStart[u]; k < csrStart[u + 1]; k++) edges.push([u, csrTo[k], csrW[k]]);
  return NETWORK.toy(N, edges, zoneIdx);
}

module.exports = { FORECAST_GOLDEN, SHAREABILITY_GOLDEN, toyNetworkOf };
