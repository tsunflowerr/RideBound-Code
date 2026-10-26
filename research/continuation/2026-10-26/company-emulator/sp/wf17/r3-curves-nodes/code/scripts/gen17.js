// wf17 COPY (r1-identity-fence-pad) of wf14/scripts/gen_cells_rs.js plus two options (RUNPLAN 1.4): --seedtag (default wf14-confirm) in the
// sample and fleet seeds, --nodetag (default via-many-days) in the node seed. With default tags the output is byte-identical (gate G-GEN).
// via-many-days: format-v1 scenarios (wf5/runner/README_SCENARIO.md) for the pre-registered days, adapted from
// wf5/ds-nyc-via/scripts/gen_scenarios.js (same network, node draw and fleet rule; changes: many days, TLC-yellow MP15 factors
// on the FleetPy scale, nested P256 c P512 samples, fleets written at the grid maximum and cut with the nVeh run override).
//   travel   = matrix F(day, s) x ff (ff = free-flow shortest path on the FleetPy Manhattan network, s); profile
//              f(b) = F(day, s+b) / F(day, s), b = 0..15 (4 h), held; F from work/factors.json (factors_build.js)
//   requests = one seeded shuffle of the window's eligible Via requests per cell; P512 = first 512, P256 = first 256
//   nodes    = seeded random network node of the PU / DO zone (seed by month + TLC row id); intra-zone trips get 2 distinct nodes
//   fleet    = seeded random distinct origin nodes of the density's sampled requests (largest grid fleet; runs cut with nVeh)
//   service  = pickup window 600 s, decision timer 600 s, permille 1500 (runs override), dwell 0 (runs override), capacity 4
// Usage: node gen_cells.js --days <days.json> --windows <via_windows.csv> --factors <factors.json> --data <fleetpy dir>
//          --work <dir with ff_apsp_u16.bin> --out <scenarios dir> [--set weekdays|weekends|calibration] [--fmax 512:96,256:56]
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const R5 = 'E:/RideBoundData/company-intake/2026-10-23/sp/wf5/ds-nyc-via/scripts/';
const L = require(R5 + 'nyclib.js');
const NET = require(R5 + 'network.js');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const DAYS = JSON.parse(fs.readFileSync(arg('days'), 'utf8'));
const set = arg('set') || 'weekdays';
const SEEDTAG = arg('seedtag') || 'wf14-confirm', NODETAG = arg('nodetag') || 'via-many-days';
const DEFAULT_TAGS = SEEDTAG === 'wf14-confirm' && NODETAG === 'via-many-days';
const RS = Number(arg('resample') || 0); // wf14: resample index (different request sample and fleet draw per resample)
const dayList = set === 'calibration' ? DAYS.calibration : DAYS[set].map(x => x.day);
const fmax = Object.fromEntries((arg('fmax') || '512:96,256:56').split(',').map(s => s.split(':').map(Number)));
const dens = Object.keys(fmax).map(Number).sort((a, b) => b - a); // [512, 256]
const outDir = arg('out');
const net = NET.load(arg('data'), arg('work'));
const { N, D, zoneNodes } = net;
const FJ = JSON.parse(fs.readFileSync(arg('factors'), 'utf8'));
const via = L.readCsv(arg('windows'));
const WINS = { w08: 8, w17: 17 };
const sha = f => crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const SRC = {
  hvfhv: 'https://d37ci6vzurychx.cloudfront.net/trip-data/fhvhv_tripdata_<YYYY-MM>.parquet (remote column-pruned DuckDB query; ETag/size in work/via_extract/sources_heads.txt)',
  yellow: 'https://d37ci6vzurychx.cloudfront.net/trip-data/yellow_tripdata_<YYYY-MM>.parquet (TLC-MP15, work/factors/)',
  fleetpyNetwork: { record: 'Zenodo 15187906 (CC BY 4.0)', edgesSha256: sha(NET.files(arg('data')).edges), nodesSha256: sha(NET.files(arg('data')).nodes) },
};
const matDir = path.join(outDir, 'matrices'), provDir = path.join(outDir, 'provenance');
for (const d of [matDir, provDir, ...dens.map(n => path.join(outDir, `${set}-P${n}`))]) fs.mkdirSync(d, { recursive: true });
const summary = [];
for (const day of dayList) for (const [win, h] of Object.entries(WINS)) {
  const cellKey = `${day}-${win}-r${RS}`;
  const s = h * 4;
  const F = FJ.F[day];
  if (!F || !(F[s] > 0) || F.slice(s, s + 16).some(x => !(x > 0))) { console.log(cellKey, 'NO FACTORS'); summary.push({ cell: cellKey, skipped: 'no factors' }); continue; }
  const all = via.filter(r => r.day === day && r.win === win);
  const elig = all.filter(r => zoneNodes.has(r.pu) && zoneNodes.has(r.dz));
  const rnd = L.rng(L.strSeed(`${SEEDTAG}|sample|${cellKey}`));
  const idx = elig.map((_, i) => i);
  for (let i = idx.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [idx[i], idx[j]] = [idx[j], idx[i]]; }
  const tws = Math.floor((elig.length ? elig[0].req_s : 0) / 86400) * 86400 + h * 3600; // naive-local epoch of the window start
  for (const n of dens) {
    if (elig.length < n) { console.log(cellKey, `P${n}: only ${elig.length} eligible -> skipped`); summary.push({ cell: cellKey, n, skipped: `only ${elig.length} eligible` }); continue; }
    const pick = idx.slice(0, n).map(i => elig[i]).sort((a, b) => a.req_s - b.req_s || (a.month < b.month ? -1 : a.month > b.month ? 1 : a.rid - b.rid));
    const reqs = pick.map(r => {
      const A = zoneNodes.get(r.pu), B = zoneNodes.get(r.dz);
      const ro = L.rng(L.strSeed(`${NODETAG}|node|${r.month}|${r.rid}`));
      const o = A[Math.floor(ro() * A.length)];
      let d = B[Math.floor(ro() * B.length)], guard = 0;
      while (d === o) { d = B[Math.floor(ro() * B.length)]; if (++guard > 1000) throw new Error('no distinct node in zone ' + r.dz); }
      const arr = (r.req_s - tws) * 1000;
      if (arr < 0 || arr > 7200000) throw new Error(`${cellKey}: arrival outside window`);
      return { id: `v${r.month.replace('-', '')}-${r.rid}`, o, d, arr, row: r };
    });
    const fr = L.rng(L.strSeed(`${SEEDTAG}|fleet|${cellKey}|n${n}`));
    const origins = [...new Set(reqs.map(q => q.o))];
    for (let i = origins.length - 1; i > 0; i--) { const j = Math.floor(fr() * (i + 1)); [origins[i], origins[j]] = [origins[j], origins[i]]; }
    if (origins.length < fmax[n]) throw new Error(`${cellKey}: ${origins.length} distinct origins < fleet ${fmax[n]}`);
    const fleetNodes = origins.slice(0, fmax[n]);
    const used = [...new Set([...reqs.flatMap(q => [q.o, q.d]), ...fleetNodes])].sort((a, b) => a - b);
    const M = used.length;
    const kappa = F[s];
    const buf = Buffer.alloc(M * M * 4);
    for (let i = 0; i < M; i++) for (let j = 0; j < M; j++) {
      const v = i === j ? 0 : Math.max(1, Math.round(kappa * D[used[i] * N + used[j]] * 1000));
      buf.writeInt32LE(v, (i * M + j) * 4);
    }
    const matName = `${cellKey}-n${n}.bin`;
    fs.writeFileSync(path.join(matDir, matName), buf);
    const factors = [];
    for (let b = 0; b < 16; b++) factors.push(Math.round((F[s + b] / F[s]) * 1e6) / 1e6);
    const direct = reqs.map(q => kappa * D[q.o * N + q.d]);
    const prov = {
      cell: cellKey, family: `via-${day}`, n, window: { day, start: `${String(h).padStart(2, '0')}:00`, end: `${String(h + 2).padStart(2, '0')}:00` },
      sources: SRC, sampling: { eligible: elig.length, windowRequests: all.length, sampled: n, seed: `${SEEDTAG}|sample|${cellKey} (P256 = first 256 of the same shuffle as P512)`, fleetSeed: `${SEEDTAG}|fleet|${cellKey}|n${n}`, ...(DEFAULT_TAGS ? {} : { nodeSeed: `${NODETAG}|node|<month>|<rid>` }) },
      travel: { F_windowStart: kappa, factorBins: factors, matrixFile: 'matrices/' + matName, matrixSha256: crypto.createHash('sha256').update(buf).digest('hex'), nodes: M },
      requests: reqs.map(q => ({ id: q.id, request: q.row.req_s, pu: q.row.pu, dz: q.row.dz, originNode: q.o, destNode: q.d, viaTripTime_s: q.row.trip_time, viaMatched: q.row.smatch, directModel_s: Math.round(kappa * D[q.o * N + q.d]) })),
      fleetNodes,
    };
    fs.writeFileSync(path.join(provDir, `${cellKey}-n${n}.prov.json`), JSON.stringify(prov));
    const scen = {
      format: 'ridebound-screen-scenario/v1',
      scenarioId: `via-${cellKey}-n${n}`,
      family: `via-${day}`,
      provenance: {
        source: 'NYC TLC High Volume FHV trip records 2019 (Via, hvfhs_license_num HV0004, Manhattan-internal requests) on the FleetPy Manhattan network (Manhattan_2019_corrected); travel-time factors TLC-MP15 from TLC Yellow trips',
        licence: 'TLC trip records: NYC Open Data / NYC.gov Terms of Use (public, AWS Open Data registry nyc-tlc); FleetPy Manhattan network: CC BY 4.0 (Zenodo 15187906, TUM-VT FleetPy)',
        citation: 'NYC Taxi and Limousine Commission, TLC Trip Record Data; Engelhardt et al., FleetPy: Input Data for Manhattan Case Study, Zenodo 15187906',
        derivedBy: DEFAULT_TAGS ? 'wf14/scripts/gen_cells_rs.js (wf6 gen_cells.js + resample seeds)' : `wf17/r1-identity-fence-pad/code/scripts/gen17.js (gen_cells_rs.js + seedtag ${SEEDTAG}, nodetag ${NODETAG}; wf17-exploratory-post-audit)`, provenanceFile: `../provenance/${cellKey}-n${n}.prov.json`,
        notes: `${day} ${String(h).padStart(2, '0')}:00-${String(h + 2).padStart(2, '0')}:00; ${n} of ${elig.length} eligible Via requests; F(window start) ${kappa.toFixed(4)} (FleetPy scale)`,
      },
      horizon: { startMs: 0, endMs: 7200000, drainMs: 7200000 },
      nodes: used.map(g => ({ id: 'n' + g, x: Math.round(net.xy[g][0] * 10) / 10, y: Math.round(net.xy[g][1] * 10) / 10, label: 'zone' + net.nodeZone[g] })),
      travel: { base: { kind: 'matrix', unit: 'ms', matrixFile: '../matrices/' + matName }, profile: { binMs: 900000, factors, afterLast: 'hold' }, snapshotRounding: 'round', forecast: 'current-bin-flat', closure: 'none' },
      requests: reqs.map(q => ({ id: q.id, arrivalMs: q.arr, origin: 'n' + q.o, destination: 'n' + q.d })),
      fleet: fleetNodes.map((g, i) => ({ id: 'veh-' + String(i).padStart(3, '0'), startNode: 'n' + g, capacity: 4 })),
      service: { pickupWindowMs: 600000, maxRidePermille: 1500, maxRidePlusMs: 0, dwellMs: 0, decisionTimerMs: 600000 },
      identity: { mode: 'plain' },
    };
    fs.writeFileSync(path.join(outDir, `${set}-P${n}`, `${cellKey}.json`), JSON.stringify(scen));
    summary.push({ cell: cellKey, n, eligible: elig.length, window: all.length, nodes: M, F0: +kappa.toFixed(4), f2h: factors[8], medDirect_s: Math.round(L.median(direct)), p90Direct_s: Math.round(L.quant(direct, 0.9)), medViaTripTime_s: L.median(pick.map(r => r.trip_time)), matchShare: +(pick.filter(r => r.smatch === 'Y').length / n).toFixed(3) });
  }
  console.log(cellKey, 'eligible', elig.length, '/', all.length, 'F0', F[s].toFixed(3), 'f2h', (F[s + 8] / F[s]).toFixed(3));
}
fs.writeFileSync(path.join(outDir, `summary-${set}.json`), JSON.stringify({ set, fmax, cells: summary }, null, 1));
