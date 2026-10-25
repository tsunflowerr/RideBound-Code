// Emulator predictions for the Via-zone bridge (owner order 2026-10-26), written and sealed before any RideBound job.
// Engine: the company wf15 engine, local copy C:/RideBoundContinuation/2026-10-23/emulator-wf15 (bridge phase 1.0: it
// reproduces all four sealed bridge prediction files byte for byte; phase 1 and fix1 matched RideBound rider for rider).
//
// prepareViaCell = fixtures.js prepareFixtureCell (lines 13-67) with ONE change: the traffic profile is the explicit
// world-v1 base of the cell (worlds/<cell>.world.json, the cell's TLC-MP15 factors) instead of lib.trafficProfile's
// FleetPy 2018 day file. The RideBound side reads the same file through preflight_world.load_world (explicit base,
// preflight_world.py:106-109) and AnchoredWorld.factor (world_v1.py:115-120): bin 0 -> 1.0, bin b -> base[b] / base[0],
// held after the last bin (world_v1.py:79-80). With sigma 0 and no shocks this equals values[min(b, H)] / values[0].
//
// Arms (12 jobs = 6 arms x 2 cells; the specs are the ones phase 1 / fix1 / r128 bridged):
//   VIA-U, VIA-RCE35, VIA-EOHZ35           insertion-only  (RideBound configs U, RCE35, EOHZ35; WP4 cost-first)
//   VIA-G2K8-U, VIA-G2K8-RCE35, VIA-G2K8-EOHZ35  G2, at most 8 receivers (configs G2-*; WP4 cost-first k8)
// Usage: node emulate_via.js <data root E:/RideBoundData/research/via-real-v1> <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-23/emulator-wf15';
const L = require(path.join(EMU, 'lib.js'));
const ID = require(path.join(EMU, 'identity.js'));
const NE = require(path.join(EMU, 'normalizer_emul.js'));
const E = require(path.join(EMU, 'engine.js'));

const PICK = 0, DROP = 1;
const K8 = { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' };
const ARMS = [
  ['VIA-U', {}, { name: 'U', B: 0 }],
  ['VIA-RCE35', {}, { name: 'RCE', alpha: 0.35 }],
  ['VIA-EOHZ35', {}, { name: 'EOHZ', alpha: 0.35 }],
  ['VIA-G2K8-U', K8, { name: 'U', B: 0 }],
  ['VIA-G2K8-RCE35', K8, { name: 'RCE', alpha: 0.35 }],
  ['VIA-G2K8-EOHZ35', K8, { name: 'EOHZ', alpha: 0.35 }],
];

function prepareViaCell(cellName, root) {
  const base = L.loadCell(cellName, path.join(root, 'fixtures'));
  const world = JSON.parse(fs.readFileSync(path.join(root, 'worlds', cellName + '.world.json'), 'utf8'));
  if (world.sigma !== 0 || world.shockRate !== 0) throw new Error('world must be noise-free');
  const values = world.base.values;
  if (!Array.isArray(values) || !values.length || values[0] !== 1) throw new Error('explicit base must start at 1');
  const H = values.length - 1;
  const prof = values.map(v => v / values[0]);
  const permille = base.norm.maximumRideTimePermille;
  const dwell = 0;
  const N = base.N;
  const snaps = prof.map(f => {
    const a = new Float64Array(N * N);
    for (let i = 0; i < N * N; i++) a[i] = ID.snapshotMs(base.T0[i], f);
    return a;
  });
  const nodeIds = Array.from({ length: N }, (_, i) => ID.nodeId(i));
  const reqs = base.requests.map(q => {
    if (NE.maxRideFromPermille(q.direct, permille) !== q.maxRide) throw new Error('fixture maxRide does not follow the normalizer rule');
    return { ...q, maxRide: NE.maxRideFromPermille(q.direct, permille) };
  });
  reqs.sort((a, b) => a.arrival - b.arrival || (a.rq < b.rq ? -1 : 1));
  reqs.forEach((q, i) => {
    q.stopP = { r: i, kind: PICK, node: q.o, code: 2 * i, sid: ID.stopId(q.rq, 0), body: ID.stopBody(nodeIds[q.o], 0, q.rq, dwell) };
    q.stopD = { r: i, kind: DROP, node: q.d, code: 2 * i + 1, sid: ID.stopId(q.rq, 1), body: ID.stopBody(nodeIds[q.d], 1, q.rq, dwell) };
  });
  const fleet = base.fleet.slice();
  const snapHashCache = new Map();
  return {
    id: cellName, family: 'd' + cellName.slice(1, 9), source: 'fixture', N, T0: base.T0, binMs: L.BIN_MS,
    maxBins: H + 1,
    physFactor: bin => prof[Math.min(bin, H)],
    snapOf: bin => snaps[Math.min(bin, H)],
    snapHash: bin => {
      if (!snapHashCache.has(bin)) snapHashCache.set(bin, ID.snapshotHash(N, base.T0, prof[Math.min(bin, H)], bin + 1, nodeIds));
      return snapHashCache.get(bin);
    },
    reqs, fleet, dwell,
    vehIdStr: fleet.map((f, vi) => ID.vehicleId(vi)),
    drainTimerMs: 600000, clockGuardMs: 40 * 3600 * 1000,
    meta: { prof, startBin: Math.round(base.norm.sourceWindowStartSeconds / 900), permille, plusMs: 0, nVeh: fleet.length,
      scenarioId: base.scenarioId, norm: base.norm, nodeIdsFixture: base.nodeIds, timeWindow: base.timeWindow,
      sourceArtifactSha256: base.sourceArtifactSha256 },
  };
}

const [root, out] = process.argv.slice(2);
if (!root || !out) { console.error('usage: node emulate_via.js <data root> <out.tsv>'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const cells = fs.readdirSync(path.join(root, 'fixtures')).filter(c => /^d\d{8}-vz-r\d-w(08|17)$/.test(c)).sort();
if (cells.length !== 2) { console.error('expected 2 cells, found ' + cells.length); process.exit(4); }
const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tforcedVehicleDecisions\tvehicles\trequests\tnodes\tms'];
for (const cell of cells) {
  const P = prepareViaCell(cell, root);
  if (P.meta.permille !== 2000) throw new Error(cell + ': permille ' + P.meta.permille);
  if (P.fleet.length !== 62) throw new Error(cell + ': fleet ' + P.fleet.length);
  for (const [role, opts, arm] of ARMS) {
    const t = Date.now();
    const res = E.simulate(P, { ...opts, arm });
    const ms = Date.now() - t;
    for (const r of res.riders) {
      lines.push([role, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
        r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
    }
    const d = res.diag || {};
    counts.push([role, cell, res.riders.length, res.riders.filter(r => r.served).length, res.rejected, d.relocations,
      d.genDecisions, d.forcedVehDecisions, P.fleet.length, P.reqs.length, P.N, ms].join('\t'));
    console.log(`${role} ${cell}: riders ${res.riders.length}, served ${res.riders.filter(r => r.served).length}, ` +
      `relocations ${d.relocations}, decisions ${d.genDecisions}, ${ms} ms`);
  }
}
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-counts.tsv'), counts.join('\n') + '\n', { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows`);
