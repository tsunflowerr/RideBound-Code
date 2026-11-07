// Builds the NYC Via cells ("P512") that the 60-day, F14 and F16 tables run on, from public data:
//   requests  NYC TLC High Volume FHV trip records 2019, Via (hvfhs_license_num HV0004), Manhattan-internal trips,
//             exported per day and window by via_month.sql and via_windows.sql (DuckDB) into via_windows.csv
//   network   the FleetPy Manhattan network (Zenodo 15187906, CC BY 4.0) through network.js: the free-flow shortest
//             travel times D (seconds) and the taxi zone of every node
//   traffic   F(day, 15-min bin), travel-time factors on the FleetPy scale (TLC-MP15, a median polish of TLC Yellow
//             trip durations): data/factors.json.gz, the gzip of the original factors.json (content sha256 6831cc6e...)
// One cell = one day, one window (w08 = 08:00-10:00, w17 = 17:00-19:00) and one resample index r:
//   requests  one seeded shuffle of the window's eligible requests (both zones are on the network); P<n> = the first n
//   nodes     a seeded random network node of the pickup zone and of the drop-off zone (seeded by month and TLC row id,
//             so the same request gets the same nodes in every cell); a trip inside one zone gets two different nodes
//   fleet     seeded random distinct origin nodes of the sampled requests, as many as --fmax gives for n (a run cuts
//             the fleet with its nVeh override)
//   travel    matrix kappa x D in ms, kappa = F(day, window start s); profile f(b) = F(day, s+b) / F(day, s) for
//             b = 0..15 (4 h), rounded to 6 decimals and held after the last bin
//   service   pickup window 600 s, decision timer 600 s, max ride 1500 permille (runs override it), dwell 0, capacity 4
// Writes <out>/weekdays-P<n>/<cell>.json (a format-v1 scenario, see scenario.js), <out>/matrices/<cell>-n<n>.bin
// (int32 little-endian ms, row-major over the cell's nodes in ascending node order),
// <out>/provenance/<cell>-n<n>.prov.json and <out>/summary-weekdays.json (the cells of this run only: the next run
// overwrites it). Prints one line per cell.
//
// Usage: node gen17.js --days <days.csv> --windows <via_windows.csv> --factors <factors.json[.gz]> --data <data root>
//          --out <dir> [--resample k] [--seedtag tag] [--fmax n:fleet,...]
//   --days      a CSV with a column "day"; the days run in file order
//   --data      the data root given to run.js as --data (network.js reads the network files and the all-pairs cache)
//   --resample  k (default 0): another request sample and fleet per k; the nodes of a request do not change
//   --seedtag   the tag in the sample and fleet seeds (default wf14-confirm)
//   --fmax      cell sizes and their fleets (default 512:96,256:56); P256 = the first 256 of the P512 shuffle
// The shipped cells (check with  sha256sum -c <list>  inside <out>):
//   60-day (F14 is a subset): --days data/days_60.csv --fmax 512:96, then --resample 0, 1 and 2 in that order;
//     expected/wf14-60day-cells.sha256 (its summary-weekdays.json is the one the r2 run writes)
//   F16: --days data/days_F16.csv --seedtag wf17-fresh --fmax 512:96, --resample 0, 1, 2, copying
//     summary-weekdays.json to summary-weekdays-r<k>.json after each run; expected/wf17-f16-cells.sha256
// The options --set, --work and --nodetag of the research copy are removed (see REMOVED_NOTE).
'use strict';
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const crypto = require('crypto');
const NETWORK = require('../network.js');

const REMOVED_NOTE = 'removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)';
const USAGE = 'usage: node gen17.js --days <days.csv> --windows <via_windows.csv> --factors <factors.json[.gz]> '
  + '--data <data root> --out <dir> [--resample k] [--seedtag tag] [--fmax n:fleet,...]';
const REQUIRED = ['days', 'windows', 'factors', 'data', 'out'];
const OPTIONAL = ['resample', 'seedtag', 'fmax'];
const REMOVED = ['set', 'work', 'nodetag'];

function fail(message) { console.error(message); process.exit(2); }

function parseArgs(argv) {
  const args = {};
  for (let i = 0; i < argv.length; i += 2) {
    const key = argv[i].startsWith('--') ? argv[i].slice(2) : '';
    if (REMOVED.includes(key)) fail(`--${key}: ${REMOVED_NOTE}`);
    if (![...REQUIRED, ...OPTIONAL].includes(key) || key in args || argv[i + 1] === undefined) fail(USAGE);
    args[key] = argv[i + 1];
  }
  for (const key of REQUIRED) if (!(key in args)) fail(USAGE);
  return args;
}

// A CSV file without quoted fields -> one object per data line, keyed by the header. A cell that reads as a number
// becomes a number, any other cell stays text (so "2019-02-05" and "w08" stay text).
function readCsv(file) {
  const text = fs.readFileSync(file, 'utf8');
  if (text.includes('"')) throw new Error(`${file}: quoted CSV fields are not supported`);
  const lines = text.split(/\r?\n/).filter(line => line.length > 0);
  const header = lines[0].split(',');
  return lines.slice(1).map(line => {
    const cells = line.split(','), row = {};
    header.forEach((name, i) => { const v = cells[i]; row[name] = v !== '' && !isNaN(Number(v)) ? Number(v) : v; });
    return row;
  });
}

// mulberry32: seeded uniform numbers in [0, 1); each draw adds 0x6d2b79f5 to the 32-bit state and mixes it
function rng(seed) {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// FNV-1a, 32 bits, over the UTF-16 code units of a text: turns the name of a random stream into its seed
function strSeed(text) {
  let h = 2166136261;
  for (let i = 0; i < text.length; i++) { h ^= text.charCodeAt(i); h = Math.imul(h, 16777619) >>> 0; }
  return h;
}

const sorted = values => Float64Array.from(values).sort();
function median(values) {
  const s = sorted(values), n = s.length;
  return n ? (n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2) : NaN;
}
function quantile(values, p) {         // linear interpolation between the two nearest ranks
  const s = sorted(values);
  if (!s.length) return NaN;
  const i = (s.length - 1) * p, lo = Math.floor(i), hi = Math.ceil(i);
  return s[lo] + (s[hi] - s[lo]) * (i - lo);
}

const args = parseArgs(process.argv.slice(2));
const dayRows = readCsv(args.days);
if (!dayRows.length || !('day' in dayRows[0])) fail(`${args.days}: no column "day"`);
const dayList = dayRows.map(r => r.day);
const SEEDTAG = args.seedtag || 'wf14-confirm';
const NODETAG = 'via-many-days';
const DEFAULT_TAGS = SEEDTAG === 'wf14-confirm';
const RS = Number(args.resample || 0);
if (!Number.isInteger(RS) || RS < 0) fail(`--resample ${args.resample}: not a whole number >= 0`);
const fmax = Object.fromEntries((args.fmax || '512:96,256:56').split(',').map(s => s.split(':').map(Number)));
const dens = Object.keys(fmax).map(Number).sort((a, b) => b - a);   // largest cell size first: [512, 256]
const outDir = args.out;

const net = NETWORK.build(args.data);
const { N, D } = net;
const zoneOf = node => net.zoneIds[net.zoneIdx[node]];             // TLC taxi zone of a node
const zoneNodes = new Map();                                       // taxi zone -> its nodes, ascending
for (let node = 0; node < N; node++) {
  if (!zoneNodes.has(zoneOf(node))) zoneNodes.set(zoneOf(node), []);
  zoneNodes.get(zoneOf(node)).push(node);
}
// the same files network.js reads; their sha256 and the node coordinates (UTM 18N metres) go into the cells
const networkDir = path.join(args.data, 'wf5', 'data', 'nyc-via', 'fleetpy-manhattan');
const nodesFile = path.join(networkDir, 'FleetPy_Manhattan__networks__Manhattan_2019_corrected__base__nodes.csv');
const edgesFile = path.join(networkDir, 'FleetPy_Manhattan__networks__Manhattan_2019_corrected__base__edges.csv');
const xy = readCsv(nodesFile).map(r => [r.pos_x, r.pos_y]);

const factorBytes = fs.readFileSync(args.factors);
const FJ = JSON.parse((args.factors.endsWith('.gz') ? zlib.gunzipSync(factorBytes) : factorBytes).toString('utf8'));
const via = readCsv(args.windows);
const WINDOWS = { w08: 8, w17: 17 };
const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
// the source notes as the shipped cells carry them (the paths are those of the research run)
const SRC = {
  hvfhv: 'https://d37ci6vzurychx.cloudfront.net/trip-data/fhvhv_tripdata_<YYYY-MM>.parquet (remote column-pruned '
    + 'DuckDB query; ETag/size in work/via_extract/sources_heads.txt)',
  yellow: 'https://d37ci6vzurychx.cloudfront.net/trip-data/yellow_tripdata_<YYYY-MM>.parquet (TLC-MP15, work/factors/)',
  fleetpyNetwork: { record: 'Zenodo 15187906 (CC BY 4.0)', edgesSha256: sha256(fs.readFileSync(edgesFile)),
    nodesSha256: sha256(fs.readFileSync(nodesFile)) },
};
const hhmm = hour => `${String(hour).padStart(2, '0')}:00`;

const matDir = path.join(outDir, 'matrices'), provDir = path.join(outDir, 'provenance');
const cellDirs = dens.map(n => path.join(outDir, `weekdays-P${n}`));
for (const d of [matDir, provDir, ...cellDirs]) fs.mkdirSync(d, { recursive: true });
const summary = [];
for (const day of dayList) for (const [win, h] of Object.entries(WINDOWS)) {
  const cellKey = `${day}-${win}-r${RS}`;
  const s = h * 4;                                                 // 15-min bin of the window start
  const F = FJ.F[day];
  if (!F || !(F[s] > 0) || F.slice(s, s + 16).some(x => !(x > 0))) {
    console.log(cellKey, 'NO FACTORS');
    summary.push({ cell: cellKey, skipped: 'no factors' });
    continue;
  }
  const all = via.filter(r => r.day === day && r.win === win);
  const elig = all.filter(r => zoneNodes.has(r.pu) && zoneNodes.has(r.dz));
  const rnd = rng(strSeed(`${SEEDTAG}|sample|${cellKey}`));
  const idx = elig.map((_, i) => i);
  for (let i = idx.length - 1; i > 0; i--) {
    const j = Math.floor(rnd() * (i + 1));
    [idx[i], idx[j]] = [idx[j], idx[i]];
  }
  // epoch second of the window start in naive local time (req_s is the naive local request time)
  const tws = Math.floor((elig.length ? elig[0].req_s : 0) / 86400) * 86400 + h * 3600;
  for (const n of dens) {
    if (elig.length < n) {
      console.log(cellKey, `P${n}: only ${elig.length} eligible -> skipped`);
      summary.push({ cell: cellKey, n, skipped: `only ${elig.length} eligible` });
      continue;
    }
    const pick = idx.slice(0, n).map(i => elig[i])
      .sort((a, b) => a.req_s - b.req_s || (a.month < b.month ? -1 : a.month > b.month ? 1 : a.rid - b.rid));
    const reqs = pick.map(r => {
      const A = zoneNodes.get(r.pu), B = zoneNodes.get(r.dz);
      const ro = rng(strSeed(`${NODETAG}|node|${r.month}|${r.rid}`));
      const o = A[Math.floor(ro() * A.length)];
      let d = B[Math.floor(ro() * B.length)], guard = 0;
      while (d === o) {
        d = B[Math.floor(ro() * B.length)];
        if (++guard > 1000) throw new Error('no distinct node in zone ' + r.dz);
      }
      const arr = (r.req_s - tws) * 1000;
      if (arr < 0 || arr > 7200000) throw new Error(`${cellKey}: arrival outside window`);
      return { id: `v${r.month.replace('-', '')}-${r.rid}`, o, d, arr, row: r };
    });
    const fr = rng(strSeed(`${SEEDTAG}|fleet|${cellKey}|n${n}`));
    const origins = [...new Set(reqs.map(q => q.o))];
    for (let i = origins.length - 1; i > 0; i--) {
      const j = Math.floor(fr() * (i + 1));
      [origins[i], origins[j]] = [origins[j], origins[i]];
    }
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
      cell: cellKey, family: `via-${day}`, n, window: { day, start: hhmm(h), end: hhmm(h + 2) },
      sources: SRC,
      sampling: {
        eligible: elig.length, windowRequests: all.length, sampled: n,
        seed: `${SEEDTAG}|sample|${cellKey} (P256 = first 256 of the same shuffle as P512)`,
        fleetSeed: `${SEEDTAG}|fleet|${cellKey}|n${n}`,
        ...(DEFAULT_TAGS ? {} : { nodeSeed: `${NODETAG}|node|<month>|<rid>` }),
      },
      travel: { F_windowStart: kappa, factorBins: factors, matrixFile: 'matrices/' + matName, matrixSha256: sha256(buf),
        nodes: M },
      requests: reqs.map(q => ({ id: q.id, request: q.row.req_s, pu: q.row.pu, dz: q.row.dz, originNode: q.o,
        destNode: q.d, viaTripTime_s: q.row.trip_time, viaMatched: q.row.smatch,
        directModel_s: Math.round(kappa * D[q.o * N + q.d]) })),
      fleetNodes,
    };
    fs.writeFileSync(path.join(provDir, `${cellKey}-n${n}.prov.json`), JSON.stringify(prov));
    const scen = {
      format: 'ridebound-screen-scenario/v1',
      scenarioId: `via-${cellKey}-n${n}`,
      family: `via-${day}`,
      provenance: {
        source: 'NYC TLC High Volume FHV trip records 2019 (Via, hvfhs_license_num HV0004, Manhattan-internal '
          + 'requests) on the FleetPy Manhattan network (Manhattan_2019_corrected); travel-time factors TLC-MP15 from '
          + 'TLC Yellow trips',
        licence: 'TLC trip records: NYC Open Data / NYC.gov Terms of Use (public, AWS Open Data registry nyc-tlc); '
          + 'FleetPy Manhattan network: CC BY 4.0 (Zenodo 15187906, TUM-VT FleetPy)',
        citation: 'NYC Taxi and Limousine Commission, TLC Trip Record Data; Engelhardt et al., FleetPy: Input Data for '
          + 'Manhattan Case Study, Zenodo 15187906',
        derivedBy: DEFAULT_TAGS ? 'wf14/scripts/gen_cells_rs.js (wf6 gen_cells.js + resample seeds)'
          : `wf17/r1-identity-fence-pad/code/scripts/gen17.js (gen_cells_rs.js + seedtag ${SEEDTAG}, `
            + `nodetag ${NODETAG}; wf17-exploratory-post-audit)`,
        provenanceFile: `../provenance/${cellKey}-n${n}.prov.json`,
        notes: `${day} ${hhmm(h)}-${hhmm(h + 2)}; ${n} of ${elig.length} eligible Via requests; `
          + `F(window start) ${kappa.toFixed(4)} (FleetPy scale)`,
      },
      horizon: { startMs: 0, endMs: 7200000, drainMs: 7200000 },
      nodes: used.map(g => ({ id: 'n' + g, x: Math.round(xy[g][0] * 10) / 10, y: Math.round(xy[g][1] * 10) / 10,
        label: 'zone' + zoneOf(g) })),
      travel: { base: { kind: 'matrix', unit: 'ms', matrixFile: '../matrices/' + matName },
        profile: { binMs: 900000, factors, afterLast: 'hold' }, snapshotRounding: 'round', forecast: 'current-bin-flat',
        closure: 'none' },
      requests: reqs.map(q => ({ id: q.id, arrivalMs: q.arr, origin: 'n' + q.o, destination: 'n' + q.d })),
      fleet: fleetNodes.map((g, i) => ({ id: 'veh-' + String(i).padStart(3, '0'), startNode: 'n' + g, capacity: 4 })),
      service: { pickupWindowMs: 600000, maxRidePermille: 1500, maxRidePlusMs: 0, dwellMs: 0, decisionTimerMs: 600000 },
      identity: { mode: 'plain' },
    };
    fs.writeFileSync(path.join(outDir, `weekdays-P${n}`, `${cellKey}.json`), JSON.stringify(scen));
    summary.push({ cell: cellKey, n, eligible: elig.length, window: all.length, nodes: M, F0: +kappa.toFixed(4),
      f2h: factors[8], medDirect_s: Math.round(median(direct)), p90Direct_s: Math.round(quantile(direct, 0.9)),
      medViaTripTime_s: median(pick.map(r => r.trip_time)),
      matchShare: +(pick.filter(r => r.smatch === 'Y').length / n).toFixed(3) });
  }
  console.log(cellKey, 'eligible', elig.length, '/', all.length, 'F0', F[s].toFixed(3),
    'f2h', (F[s + 8] / F[s]).toFixed(3));
}
fs.writeFileSync(path.join(outDir, 'summary-weekdays.json'),
  JSON.stringify({ set: 'weekdays', fmax, cells: summary }, null, 1));
