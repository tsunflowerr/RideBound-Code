// Tests for network.js on small networks built by network.toy(): the all-pairs table D (Dijkstra from every node, in
// whole seconds), its transpose DT, the edges grouped by start node and the zone numbering. The FleetPy Manhattan
// network goes through the same code (build() only reads the CSV files first). Run: node --test src/emulator/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs');
const NETWORK = require('../network.js');
const { SHAREABILITY_GOLDEN, toyNetworkOf } = require('./goldens.js');

const plain = typed => Array.from(typed);

// Shortest travel times by Floyd-Warshall, written independently of network.js: d[a][b] = min over every path a -> b.
function floydWarshall(N, edges) {
  const d = Array.from({ length: N }, (_, a) => Array.from({ length: N }, (_, b) => (a === b ? 0 : Infinity)));
  for (const [a, b, seconds] of edges) d[a][b] = Math.min(d[a][b], seconds);
  for (let k = 0; k < N; k++) for (let a = 0; a < N; a++) for (let b = 0; b < N; b++) {
    if (d[a][k] + d[k][b] < d[a][b]) d[a][b] = d[a][k] + d[k][b];
  }
  return d.flat();
}

test('hand example: shortest paths, transpose, edges by start node in file order, zones by ascending id', () => {
  // 0 -> 1 -> 2 (10 s + 10 s) beats the direct 0 -> 2 (25 s); 2 -> 3 -> 0 closes the cycle
  const edges = [[0, 2, 25], [2, 3, 5], [0, 1, 10], [3, 0, 7], [1, 2, 10]];
  const net = NETWORK.toy(4, edges, [7, 7, 3, 9]);
  assert.deepStrictEqual(plain(net.D), [
    0, 10, 20, 25,     // from 0
    22, 0, 10, 15,     // from 1: 1 -> 2 -> 3 -> 0
    12, 22, 0, 5,      // from 2
    7, 17, 27, 0,      // from 3: 3 -> 0 -> 1 -> 2 (27 s) beats 3 -> 0 -> 2 (32 s)
  ]);
  assert.deepStrictEqual(plain(net.DT), [0, 22, 12, 7, 10, 0, 22, 17, 20, 10, 0, 27, 25, 15, 5, 0]);
  assert.deepStrictEqual([plain(net.csrStart), plain(net.csrTo), plain(net.csrW)],
    [[0, 2, 3, 4, 5], [2, 1, 2, 3, 0], [25, 10, 10, 5, 7]]);
  assert.deepStrictEqual([net.nZones, plain(net.zoneIds), plain(net.zoneIdx), plain(net.zoneSize)],
    [3, [3, 7, 9], [1, 1, 0, 2], [1, 2, 1]]);
});

test('300 random strongly connected networks: D equals Floyd-Warshall', () => {
  let h = 20261107;   // xorshift32, so the networks are the same on every run
  const rnd = () => { h ^= h << 13; h >>>= 0; h ^= h >>> 17; h ^= h << 5; h >>>= 0; return h / 4294967296; };
  const ri = (a, b) => a + Math.floor(rnd() * (b - a + 1));
  for (let t = 0; t < 300; t++) {
    const N = ri(2, 25), edges = [];
    for (let i = 0; i < N; i++) edges.push([i, (i + 1) % N, ri(1, 120)]);   // a ring: every node reaches every node
    for (let e = ri(0, 3 * N); e > 0; e--) edges.push([ri(0, N - 1), ri(0, N - 1), ri(1, 120)]);   // loops, repeats too
    const net = NETWORK.toy(N, edges, Array.from({ length: N }, () => ri(1, 4)));
    assert.deepStrictEqual(plain(net.D), floydWarshall(N, edges), `network ${t}`);
  }
});

test('the 24 networks of the C# shareability golden: same D, edges and zones', () => {
  const golden = JSON.parse(fs.readFileSync(SHAREABILITY_GOLDEN, 'utf8'));
  assert.strictEqual(golden.cells.length, 24);
  golden.cells.forEach((cell, c) => {
    const net = toyNetworkOf(cell), want = cell.net;
    assert.deepStrictEqual(
      [net.N, net.nZones, plain(net.D), plain(net.csrStart), plain(net.csrTo), plain(net.csrW), plain(net.zoneIdx)],
      [want.N, want.nZones, want.D, want.csrStart, want.csrTo, want.csrW, want.zoneIdx], `cell ${c}`);
  });
});

test('the loader refuses what the uint16 table or the scorer cannot use', () => {
  assert.throws(() => NETWORK.toy(3, [[0, 1, 5], [1, 0, 5], [0, 2, 5]], [1, 1, 1]),
    /node 0 cannot be reached from node 2/);
  assert.throws(() => NETWORK.toy(2, [[0, 1, 65536], [1, 0, 1]], [1, 1]), /0 -> 1 takes 65536 s/);
  assert.deepStrictEqual(plain(NETWORK.toy(2, [[0, 1, 65535], [1, 0, 1]], [1, 1]).D), [0, 65535, 1, 0]);
  for (const edge of [[0, 1, 0], [0, 1, 1.5], [0, 2, 5], [-1, 0, 5]]) {
    assert.throws(() => NETWORK.toy(2, [edge, [1, 0, 1]], [1, 1]), /bad edge/, JSON.stringify(edge));
  }
  assert.throws(() => NETWORK.toy(2, [[0, 1, 1], [1, 0, 1]], [1]), /node without zone 1/);
});
