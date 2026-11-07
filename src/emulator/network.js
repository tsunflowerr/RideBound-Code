// The FleetPy Manhattan road network (FleetPy, Zenodo record 15187906, CC BY 4.0) for the shareability score
// (sscore.js) and the lambda tables (lambda.js). fetch_network.js downloads its four CSV files; expected/network.sha256
// lists them.
//
// build(dataRoot) reads <dataRoot>/wf5/data/nyc-via/fleetpy-manhattan/FleetPy_Manhattan__*.csv and returns
//   N                    number of nodes (4,411); node n is line n of nodes.csv
//   D, DT                Uint16Array N*N: free-flow seconds of the shortest path from -> to (row-major), and its
//                        transpose (DT[b*N + a] = D[a*N + b], so a column of D is read as a row of DT)
//   csrStart/csrTo/csrW  the directed edges grouped by start node, in file order, with integer travel seconds: the
//                        edges leaving node u are k = csrStart[u] .. csrStart[u+1]-1 (sscore.js rebuilds paths)
//   zoneIds              the taxi zones that contain a node, ascending; zoneIdx: node -> index into zoneIds; zoneSize
// D comes from Dijkstra run from every node. That takes seconds, so D is cached in
// <dataRoot>/wf6/via-many-days/work/ff_apsp_u16.bin (N*N uint16, little-endian; its sha256 is in
// expected/network.sha256), and a cache file of the right size is used as it is.
// Every array lives in a SharedArrayBuffer: share() hands the network to worker threads without a copy, and attach()
// adds the per-thread parts (zone lookup, shortest-path cache). toy() builds a small network for tests with the same
// code.
'use strict';
const fs = require('fs');
const path = require('path');

const NETWORK_DIR = path.join('wf5', 'data', 'nyc-via', 'fleetpy-manhattan');
const APSP_CACHE = path.join('wf6', 'via-many-days', 'work', 'ff_apsp_u16.bin');
const FILE = {
  nodes: 'FleetPy_Manhattan__networks__Manhattan_2019_corrected__base__nodes.csv',
  edges: 'FleetPy_Manhattan__networks__Manhattan_2019_corrected__base__edges.csv',
  nodeZones: 'FleetPy_Manhattan__zones__Manhattan_Taxi_Zones__Manhattan_2019_corrected__node_zone_info.csv',
  zones: 'FleetPy_Manhattan__zones__Manhattan_Taxi_Zones__general_info.csv',
};

const shared = (Type, n) => new Type(new SharedArrayBuffer(Type.BYTES_PER_ELEMENT * n));

// The named columns of a CSV file, as integers, one object per data line (these files have no quoted fields).
function readCsv(file, columns) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).filter(line => line.length > 0);
  const header = lines[0].split(',');
  const at = columns.map(c => {
    if (!header.includes(c)) throw new Error(`network: ${path.basename(file)} has no column ${c}`);
    return header.indexOf(c);
  });
  return lines.slice(1).map((line, k) => {
    const cells = line.split(','), row = {};
    columns.forEach((c, j) => {
      const v = Number(cells[at[j]]);
      if (cells[at[j]] === '' || !Number.isInteger(v)) {
        throw new Error(`network: ${path.basename(file)} line ${k + 2}: ${c} is not an integer`);
      }
      row[c] = v;
    });
    return row;
  });
}

function build(dataRoot) {
  const dir = path.join(dataRoot, NETWORK_DIR);
  const nodes = readCsv(path.join(dir, FILE.nodes), ['node_index']);
  nodes.forEach((r, i) => {
    if (r.node_index !== i) throw new Error(`network: nodes.csv row ${i} is node ${r.node_index}`);
  });
  const edges = readCsv(path.join(dir, FILE.edges), ['from_node', 'to_node', 'travel_time'])
    .map(e => [e.from_node, e.to_node, e.travel_time]);
  // node -> FleetPy zone id -> TLC taxi zone
  const taxiZone = new Map(readCsv(path.join(dir, FILE.zones), ['zone_id', 'taxi_zone'])
    .map(z => [z.zone_id, z.taxi_zone]));
  const zoneOfNode = new Array(nodes.length);
  for (const r of readCsv(path.join(dir, FILE.nodeZones), ['node_index', 'zone_id'])) {
    zoneOfNode[r.node_index] = taxiZone.get(r.zone_id);
  }
  return fromEdges(nodes.length, edges, zoneOfNode, path.join(dataRoot, APSP_CACHE));
}

// Toy network for unit tests: nodes 0..N-1, directed edges [[from, to, seconds], ...], the zone of every node
// (any integer ids).
function toy(N, edges, zoneOfNode) { return fromEdges(N, edges, zoneOfNode, null); }

function fromEdges(N, edges, zoneOfNode, cacheFile) {
  const isNode = n => Number.isInteger(n) && n >= 0 && n < N;
  // edges grouped by start node (a counting sort, so the edges of one node keep their file order)
  const csrStart = shared(Int32Array, N + 1);
  for (const [from, to, seconds] of edges) {
    if (!isNode(from) || !isNode(to) || !Number.isInteger(seconds) || seconds <= 0) {
      throw new Error(`network: bad edge ${from} -> ${to} (${seconds} s)`);
    }
    csrStart[from + 1]++;
  }
  for (let u = 0; u < N; u++) csrStart[u + 1] += csrStart[u];
  const csrTo = shared(Int32Array, edges.length), csrW = shared(Int32Array, edges.length);
  const next = Int32Array.from(csrStart);
  for (const [from, to, seconds] of edges) { const k = next[from]++; csrTo[k] = to; csrW[k] = seconds; }

  // zones, numbered by ascending zone id
  for (let n = 0; n < N; n++) if (!Number.isInteger(zoneOfNode[n])) throw new Error('network: node without zone ' + n);
  const ids = [...new Set(zoneOfNode.slice(0, N))].sort((a, b) => a - b);
  const zoneIds = shared(Int32Array, ids.length);
  zoneIds.set(ids);
  const indexOfZone = new Map(ids.map((z, i) => [z, i]));
  const zoneIdx = shared(Int32Array, N), zoneSize = shared(Int32Array, ids.length);
  for (let n = 0; n < N; n++) { zoneIdx[n] = indexOfZone.get(zoneOfNode[n]); zoneSize[zoneIdx[n]]++; }

  const D = cacheFile ? cachedAllPairs(cacheFile, N, csrStart, csrTo, csrW) : allPairs(N, csrStart, csrTo, csrW);
  const DT = shared(Uint16Array, N * N);
  for (let a = 0; a < N; a++) { const row = a * N; for (let b = 0; b < N; b++) DT[b * N + a] = D[row + b]; }
  return attach({ N, nZones: ids.length, nEdges: edges.length, D, DT, csrStart, csrTo, csrW,
    zoneIds, zoneIdx, zoneSize });
}

// D from the cache file if it has the right size; otherwise computed and written there
function cachedAllPairs(file, N, csrStart, csrTo, csrW) {
  if (fs.existsSync(file) && fs.statSync(file).size === 2 * N * N) {
    const D = shared(Uint16Array, N * N);
    new Uint8Array(D.buffer).set(fs.readFileSync(file));
    return D;
  }
  const D = allPairs(N, csrStart, csrTo, csrW);
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, new Uint8Array(D.buffer));
  return D;
}

// All-pairs shortest travel times: Dijkstra from every node, with a binary min-heap of (node, distance) entries. A node
// may sit in the heap several times; an entry whose distance is already beaten is skipped when it comes out. Edge
// weights are integers, so every distance is exact.
function allPairs(N, csrStart, csrTo, csrW) {
  const D = shared(Uint16Array, N * N);
  const dist = new Float64Array(N);
  // each edge is relaxed at most once per source, so the heap never holds more than (edges + 1) entries
  const heapNode = new Int32Array(csrTo.length + 1), heapDist = new Float64Array(csrTo.length + 1);
  for (let s = 0; s < N; s++) {
    dist.fill(Infinity);
    dist[s] = 0;
    let size = 0;
    const push = (node, d) => {          // add at the bottom, then move up past every parent with a larger distance
      let i = size++;
      while (i > 0) {
        const parent = (i - 1) >> 1;
        if (heapDist[parent] <= d) break;
        heapNode[i] = heapNode[parent]; heapDist[i] = heapDist[parent]; i = parent;
      }
      heapNode[i] = node; heapDist[i] = d;
    };
    push(s, 0);
    while (size > 0) {
      const u = heapNode[0], du = heapDist[0];
      // remove the top: the last entry moves down from the root past every child with a smaller distance
      const lastNode = heapNode[--size], lastDist = heapDist[size];
      let i = 0;
      for (;;) {
        let child = 2 * i + 1;
        if (child >= size) break;
        if (child + 1 < size && heapDist[child + 1] < heapDist[child]) child++;
        if (heapDist[child] >= lastDist) break;
        heapNode[i] = heapNode[child]; heapDist[i] = heapDist[child]; i = child;
      }
      heapNode[i] = lastNode; heapDist[i] = lastDist;
      if (du > dist[u]) continue;
      for (let k = csrStart[u]; k < csrStart[u + 1]; k++) {
        const v = csrTo[k], dv = du + csrW[k];
        if (dv < dist[v]) { dist[v] = dv; push(v, dv); }
      }
    }
    for (let t = 0; t < N; t++) {
      if (dist[t] === Infinity) throw new Error(`network: node ${t} cannot be reached from node ${s}`);
      if (dist[t] > 65535) throw new Error(`network: ${s} -> ${t} takes ${dist[t]} s, more than a Uint16Array holds`);
      D[s * N + t] = dist[t];
    }
  }
  return D;
}

// workerData-safe description (typed arrays over SharedArrayBuffers are passed by reference)
function share(NET) {
  const { N, nZones, nEdges, D, DT, csrStart, csrTo, csrW, zoneIds, zoneIdx, zoneSize } = NET;
  return { N, nZones, nEdges, D, DT, csrStart, csrTo, csrW, zoneIds, zoneIdx, zoneSize };
}

function attach(S) {
  const NET = { ...S };
  NET.zoneOfId = new Map(Array.from(S.zoneIds, (z, i) => [z, i]));
  NET.paths = new Map();            // shortest-path cache (key a*N+b), LRU cap 50 000, per thread
  NET.pathCap = 50000;
  NET.pathStats = { hits: 0, misses: 0, checked: 0 };
  return NET;
}

module.exports = { build, share, attach, toy };
