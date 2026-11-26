// wf15 (SPEC 2.3, 4.2, 4.8 notes): the FULL FleetPy Manhattan network for the Elmi et al. (2026) shareability score.
//   D        Uint16Array N*N, free-flow seconds (row-major from -> to), from wf5/ds-nyc-via/scripts/network.js (read-only)
//   DT       transpose of D (column reads D[n][b] become row reads DT[b][n])
//   csrStart/csrTo/csrW  out-edges of the edge list (integer travel_time seconds) for shortest-path reconstruction
//   zoneIdx  node -> zone index (0..nZones-1, zones = TLC taxi zone ids sorted ascending), zoneSize, zoneIds
// build() runs once in the main thread and places every array in a SharedArrayBuffer; attach() adds the zone-id map and
// the shortest-path cache. Pure data, no side effects besides reading the network files.
'use strict';
const path = require('path');

const SP = require('../../paths.js').DATA; // data root (RB_DATA)
const NET_MODULE = path.join(__dirname, '..', '..', 'wf5', 'network.js');
const NYCLIB = path.join(__dirname, '..', '..', 'wf5', 'nyclib.js');
const DATA_DIR = path.join(SP, 'wf5', 'data', 'nyc-via', 'fleetpy-manhattan');
const WORK_DIR = path.join(SP, 'wf6', 'via-many-days', 'work');

function shared(Ctor, n) { return new Ctor(new SharedArrayBuffer(Ctor.BYTES_PER_ELEMENT * Math.max(1, n))); }

function build() {
  const NETM = require(NET_MODULE);
  const L = require(NYCLIB);
  const net = NETM.load(DATA_DIR, WORK_DIR);
  const N = net.N;
  const D = shared(Uint16Array, N * N);
  D.set(net.D);
  const DT = shared(Uint16Array, N * N);
  for (let i = 0; i < N; i++) { const ri = i * N; for (let j = 0; j < N; j++) DT[j * N + i] = D[ri + j]; }
  const edges = L.readCsv(NETM.files(DATA_DIR).edges);
  const csrStart = shared(Int32Array, N + 1);
  for (const e of edges) {
    if (!Number.isInteger(e.from_node) || !Number.isInteger(e.to_node) || !Number.isInteger(e.travel_time) || e.travel_time <= 0) throw new Error('netfull: bad edge row');
    csrStart[e.from_node + 1]++;
  }
  for (let i = 0; i < N; i++) csrStart[i + 1] += csrStart[i];
  const csrTo = shared(Int32Array, edges.length), csrW = shared(Int32Array, edges.length);
  const fill = Int32Array.from(csrStart);
  for (const e of edges) { const k = fill[e.from_node]++; csrTo[k] = e.to_node; csrW[k] = e.travel_time; }
  const zoneIdsArr = [...net.zoneNodes.keys()].sort((a, b) => a - b);
  const nZones = zoneIdsArr.length;
  const zoneIds = shared(Int32Array, nZones); zoneIds.set(zoneIdsArr);
  const zpos = new Map(zoneIdsArr.map((z, i) => [z, i]));
  const zoneIdx = shared(Int32Array, N), zoneSize = shared(Int32Array, nZones);
  for (let n = 0; n < N; n++) {
    const z = net.nodeZone[n];
    if (!zpos.has(z)) throw new Error('netfull: node without zone ' + n);
    zoneIdx[n] = zpos.get(z); zoneSize[zpos.get(z)]++;
  }
  const S = { N, nZones, nEdges: edges.length, D, DT, csrStart, csrTo, csrW, zoneIds, zoneIdx, zoneSize };
  return attach(S);
}

function attach(S) {
  const NET = { ...S };
  NET.zoneOfId = new Map(Array.from(S.zoneIds, (z, i) => [z, i]));
  NET.paths = new Map();            // shortest-path cache (key a*N+b), LRU cap 50 000, per thread
  NET.pathCap = 50000;
  NET.pathStats = { hits: 0, misses: 0, checked: 0 };
  return NET;
}

module.exports = { build };
