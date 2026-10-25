// Diagnostic only (not sealed, not a prediction): re-run the sealed emulate_via.js arms and print the engine's cap
// counters per cell-arm, to know before the comparison whether RideBound's per-vehicle caps can bind where the
// emulator does not cut (engine.js:500-502 workCapHits is diagnostic; :512 poolCapHits = the 100-candidate cap, which
// the engine applies). prepareViaCell below is a byte copy of emulate_via.js (sealed in VIA-KIT.sha256).
// Usage: node emulate_via_diag.js <data root> <out.tsv>
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
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const keys = ['genDecisions', 'workCapHits', 'workCapHitsBase', 'vehDecisions', 'poolCapHits', 'poolOver100', 'maxPool', 'maxSuffix', 'tieBreaksByIdentity', 'relocations', 'forcedVehicleDecisions'];
const lines = ['role\tcell\tserved\t' + keys.join('\t')];
for (const cell of fs.readdirSync(path.join(root, 'fixtures')).filter(c => /^d\d{8}-vz-r\d-w(08|17)$/.test(c)).sort()) {
  const P = prepareViaCell(cell, root);
  for (const [role, opts, arm] of ARMS) {
    const res = E.simulate(P, { ...opts, arm });
    const d = res.diag || {};
    lines.push([role, cell, res.riders.filter(r => r.served).length, ...keys.map(k => d[k])].join('\t'));
  }
}
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
console.log(lines.join('\n'));
