// nyc-via: FleetPy Manhattan network (Zenodo 15187906, CC BY 4.0; members range-fetched into data/nyc-via/fleetpy-manhattan)
// -> all-pairs shortest FREE-FLOW travel times (Dijkstra on the directed edge list, integer seconds as in edges.csv),
// node -> taxi zone map (node_zone_info + general_info). Files are DATA (CSV parsed as text).
// Exports load(dataDir, workDir): caches the APSP as work/ff_apsp_u16.bin (N*N uint16 seconds, row-major from -> to).
'use strict';
const fs = require('fs');
const path = require('path');
const L = require('./nyclib.js');

const PFX = 'FleetPy_Manhattan__';
function files(dataDir) {
  return {
    nodes: path.join(dataDir, PFX + 'networks__Manhattan_2019_corrected__base__nodes.csv'),
    edges: path.join(dataDir, PFX + 'networks__Manhattan_2019_corrected__base__edges.csv'),
    nzi: path.join(dataDir, PFX + 'zones__Manhattan_Taxi_Zones__Manhattan_2019_corrected__node_zone_info.csv'),
    gi: path.join(dataDir, PFX + 'zones__Manhattan_Taxi_Zones__general_info.csv'),
  };
}

function dijkstraAll(N, adjStart, adjTo, adjW) {
  const D = new Uint16Array(N * N);
  const dist = new Float64Array(N);
  const heapN = new Int32Array(adjTo.length + N + 5), heapD = new Float64Array(adjTo.length + N + 5);
  for (let s = 0; s < N; s++) {
    dist.fill(Infinity); dist[s] = 0;
    let hs = 0;
    const push = (n, d) => { let i = hs++; while (i > 0) { const p = (i - 1) >> 1; if (heapD[p] <= d) break; heapN[i] = heapN[p]; heapD[i] = heapD[p]; i = p; } heapN[i] = n; heapD[i] = d; };
    const pop = () => { const n = heapN[0], d = heapD[0]; const ln = heapN[--hs], ld = heapD[hs]; let i = 0; for (;;) { let c = 2 * i + 1; if (c >= hs) break; if (c + 1 < hs && heapD[c + 1] < heapD[c]) c++; if (heapD[c] >= ld) break; heapN[i] = heapN[c]; heapD[i] = heapD[c]; i = c; } heapN[i] = ln; heapD[i] = ld; return [n, d]; };
    push(s, 0);
    while (hs > 0) {
      const [u, du] = pop();
      if (du > dist[u]) continue;
      for (let k = adjStart[u]; k < adjStart[u + 1]; k++) { const v = adjTo[k], nd = du + adjW[k]; if (nd < dist[v]) { dist[v] = nd; push(v, nd); } }
    }
    for (let t = 0; t < N; t++) { if (!Number.isFinite(dist[t])) throw new Error(`node ${t} unreachable from ${s}`); if (dist[t] > 65535) throw new Error('uint16 overflow'); D[s * N + t] = dist[t]; }
  }
  return D;
}

function load(dataDir, workDir) {
  const f = files(dataDir);
  const nodes = L.readCsv(f.nodes);
  const N = nodes.length;
  nodes.forEach((n, i) => { if (n.node_index !== i) throw new Error('node_index order'); });
  const edges = L.readCsv(f.edges);
  const gi = L.readCsv(f.gi);
  const zmap = Object.fromEntries(gi.map(g => [g.zone_id, g.taxi_zone]));
  const nzi = L.readCsv(f.nzi);
  const nodeZone = new Int32Array(N).fill(-1);
  for (const r of nzi) nodeZone[r.node_index] = zmap[r.zone_id];
  const zoneNodes = new Map();
  for (let i = 0; i < N; i++) { const z = nodeZone[i]; if (z < 0) continue; if (!zoneNodes.has(z)) zoneNodes.set(z, []); zoneNodes.get(z).push(i); }
  const cache = path.join(workDir, 'ff_apsp_u16.bin');
  let D;
  if (fs.existsSync(cache) && fs.statSync(cache).size === N * N * 2) {
    const b = fs.readFileSync(cache);
    D = new Uint16Array(b.buffer, b.byteOffset, N * N);
  } else {
    const deg = new Int32Array(N + 1);
    for (const e of edges) deg[e.from_node + 1]++;
    for (let i = 0; i < N; i++) deg[i + 1] += deg[i];
    const adjTo = new Int32Array(edges.length), adjW = new Float64Array(edges.length), fill = deg.slice();
    for (const e of edges) { const k = fill[e.from_node]++; adjTo[k] = e.to_node; adjW[k] = e.travel_time; }
    D = dijkstraAll(N, deg, adjTo, adjW);
    fs.writeFileSync(cache, Buffer.from(D.buffer));
  }
  const xy = nodes.map(n => [n.pos_x, n.pos_y]); // EPSG:32618 (UTM 18N) metres
  return { N, D, nodeZone, zoneNodes, xy, edges: edges.length };
}

module.exports = { load, files };
