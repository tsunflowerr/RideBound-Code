// wf20/am2017 Step 1 facts (NO simulation; thăm dò sau kiểm toán). Reads (read-only) the wf14 P512 r0 cells, wf17 panels,
// wf12 day_window_counts.csv, the FleetPy Manhattan nodes and the profile CSVs written by profile.sql; prints facts/facts.json + facts.txt.
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const ME = path.join(SP, 'wf20/am2017');
const SC = require(path.join(SP, 'wf17/r2-penalty-regime/code/runner/scenario.js'));
const CELLS = path.join(SP, 'wf14/scenarios/weekdays-P512');
const PAN = JSON.parse(fs.readFileSync(path.join(SP, 'wf17/r2-penalty-regime/work/panels.json'), 'utf8'));
const med = a => { const s = a.slice().sort((x, y) => x - y), n = s.length; return n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2; };
const mean = a => a.reduce((s, x) => s + x, 0) / a.length;
const out = { label: 'thăm dò sau kiểm toán (wf20/am2017 Step 1, facts only)', cells: {}, panels: {} };

// 1. per cell: eligible Via requests (from the scenario notes), fleet written, service fields, direct times at the window-start matrix
const rows = [];
for (const f of fs.readdirSync(CELLS).filter(f => /-r0\.json$/.test(f)).sort()) {
  const s = JSON.parse(fs.readFileSync(path.join(CELLS, f), 'utf8'));
  const m = /512 of (\d+) eligible/.exec(s.provenance.notes); if (!m) throw new Error('no eligible count in ' + f);
  const P = SC.loadScenarioFile(path.join(CELLS, f), { maxRidePermille: 2000, dwellMs: 0, nVeh: 62 });
  const direct = P.reqs.map(q => q.direct / 1000);
  const fac = s.travel.profile.factors;
  rows.push({ cell: f.replace('.json', ''), day: f.slice(0, 10), win: f.slice(11, 14), eligible: Number(m[1]), fleetWritten: s.fleet.length,
    pickupWindowS: s.service.pickupWindowMs / 1000, decisionTimerS: s.service.decisionTimerMs / 1000, capacity: s.fleet[0].capacity,
    fileMaxRidePermille: s.service.maxRidePermille, nodes: s.nodes.length,
    directMedS: med(direct), directMeanS: mean(direct), directSumH: direct.reduce((a, x) => a + x, 0) / 3600, factorMinMax: [Math.min(...fac), Math.max(...fac)] });
}
out.cells.n = rows.length;
for (const [pname, days] of [['D60', PAN.D60], ['D30', PAN.D30]]) {
  const R = rows.filter(r => days.includes(r.day));
  const by = w => R.filter(r => r.win === w);
  const o = { nCells: R.length };
  for (const w of ['w08', 'w17', 'both']) {
    const X = w === 'both' ? R : by(w);
    const el = X.map(r => r.eligible), fr = X.map(r => 512 / r.eligible);
    o[w] = { n: X.length, eligibleMed: med(el), eligibleMean: mean(el), eligibleMin: Math.min(...el), eligibleMax: Math.max(...el),
      fracMedPct: 100 * med(fr), fracMinPct: 100 * Math.min(...fr), fracMaxPct: 100 * Math.max(...fr),
      directMedOfCellMedS: med(X.map(r => r.directMedS)), directMeanS: mean(X.map(r => r.directMeanS)),
      loadP512_62: mean(X.map(r => r.directSumH / (62 * 2))), loadP256_42_approx: mean(X.map(r => r.directSumH / 2 / (42 * 2))),
      fleetFullViaAt62: med(X.map(r => 62 * r.eligible / 512)) };
  }
  out.panels[pname] = o;
}
const uniq = k => [...new Set(rows.map(r => JSON.stringify(r[k])))];
out.cells.serviceFields = { pickupWindowS: uniq('pickupWindowS'), decisionTimerS: uniq('decisionTimerS'), capacity: uniq('capacity'), fileMaxRidePermille: uniq('fileMaxRidePermille'), fleetWritten: uniq('fleetWritten') };
out.cells.rows = rows;

// 2. wf12 day_window_counts (all HV0004 Manhattan-internal trips per window, before the zone-with-node filter)
const dw = fs.readFileSync(path.join(SP, 'wf12/r5-data-days/work/day_window_counts.csv'), 'utf8').trim().split('\n').slice(1).map(l => l.split(','));
const dwD60 = dw.filter(x => PAN.D60.includes(x[0]));
out.dayWindowCounts = { daysInFile: dw.length, D60found: dwD60.length,
  D60_w08_med: med(dwD60.map(x => +x[3])), D60_w17_med: med(dwD60.map(x => +x[4])), D60_day_med: med(dwD60.map(x => +x[2])) };

// 3. network area (FleetPy Manhattan_2019_corrected nodes, EPSG:32618 metres): convex hull and 250-m / 500-m grid coverage
const nodes = fs.readFileSync(path.join(SP, 'wf5/data/nyc-via/fleetpy-manhattan/FleetPy_Manhattan__networks__Manhattan_2019_corrected__base__nodes.csv'), 'utf8')
  .trim().split('\n').slice(1).map(l => l.split(',')).map(x => [+x[2], +x[3]]);
const pts = nodes.slice().sort((a, b) => a[0] - b[0] || a[1] - b[1]);
const cross = (o, a, b) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
const lower = [], upper = [];
for (const p of pts) { while (lower.length >= 2 && cross(lower[lower.length - 2], lower[lower.length - 1], p) <= 0) lower.pop(); lower.push(p); }
for (const p of pts.slice().reverse()) { while (upper.length >= 2 && cross(upper[upper.length - 2], upper[upper.length - 1], p) <= 0) upper.pop(); upper.push(p); }
const hull = lower.slice(0, -1).concat(upper.slice(0, -1));
let A = 0; for (let i = 0; i < hull.length; i++) { const [x1, y1] = hull[i], [x2, y2] = hull[(i + 1) % hull.length]; A += x1 * y2 - x2 * y1; }
const grid = g => new Set(nodes.map(([x, y]) => Math.floor(x / g) + ',' + Math.floor(y / g))).size * g * g / 1e6;
out.network = { nodes: nodes.length, hullKm2: Math.abs(A) / 2 / 1e6, grid250Km2: grid(250), grid500Km2: grid(500) };

// 4. profile CSVs (from profile.sql)
const csv = f => { const L = fs.readFileSync(path.join(ME, 'facts', f), 'utf8').replace(/\r/g, '').trim().split('\n'); const h = L[0].split(','); return L.slice(1).map(l => Object.fromEntries(l.split(',').map((v, i) => [h[i], isNaN(+v) ? v : +v]))); };
const win = csv('window_weekday_2019.csv');
const get = (src, w) => win.find(r => r.src === src && r.w === w);
const prof = {};
for (const src of ['yellow', 'via']) {
  const day = get(src, 'day').per_day_mean;
  prof[src] = { perDay: day };
  for (const w of ['w08', 'w13', 'w17']) { const r = get(src, w); prof[src][w] = { per2h: r.per_day_mean, shareOfDayPct: 100 * r.per_day_mean / day, peakFactor: r.per_day_mean / (day / 12), medDurS: r.med_d_s, meanDurS: r.mean_d_s, medMi: r.med_mi }; }
}
const hr = csv('hourly_weekday_2019.csv').filter(r => r.src === 'yellow');
let best = null; for (let h = 0; h < 23; h++) { const s = hr[h].per_day + hr[h + 1].per_day; if (!best || s > best.s) best = { h, s }; }
prof.yellow.peak2h = { startHour: best.h, per2h: best.s, peakFactor: best.s / (prof.yellow.perDay / 12) };
// trip-time ratio Via / Yellow in our windows (mean actual durations, pooled over w08 and w17 by volume) = kappa (pre-specified in PLAN)
const pool = (src, k) => (get(src, 'w08')[k] * get(src, 'w08').per_day_mean + get(src, 'w17')[k] * get(src, 'w17').per_day_mean) / (get(src, 'w08').per_day_mean + get(src, 'w17').per_day_mean);
prof.kappaMeanDur = pool('via', 'mean_d_s') / pool('yellow', 'mean_d_s');
prof.kappaMedDur = pool('via', 'med_d_s') / pool('yellow', 'med_d_s');
prof.kappaMedMiles = pool('via', 'med_mi') / pool('yellow', 'med_mi');
out.profile2019 = prof;

// 5. literature ratios (numbers typed from the sources; file:line in PLAN.md / facts.txt)
const AM = { weekRequests: 3e6, sun: 382779, fri: 460700 };
AM.dayAvg = AM.weekRequests / 7;
const per2h = d => d / 12;
const yShare = (prof.yellow.w08.shareOfDayPct + prof.yellow.w17.shareOfDayPct) / 2 / 100; // mean of the two windows' day shares
out.ratios = {
  AM17: Object.fromEntries([1000, 2000, 3000].map(m => [m, {
    avg: per2h(AM.dayAvg) / m, sun: per2h(AM.sun) / m, fri: per2h(AM.fri) / m,
    peakProxy_ourWindows: AM.dayAvg * yShare / m, peakProxy_w17: AM.dayAvg * prof.yellow.w17.shareOfDayPct / 100 / m,
  }])),
  AM17_dayAvg: AM.dayAvg, AM17_per2hAvg: per2h(AM.dayAvg), AM17_per2h_ourWindowsProxy: AM.dayAvg * yShare,
  AM17_implied_13to15_proxy: AM.dayAvg * prof.yellow.w13.shareOfDayPct / 100,
  FA20: 19610 / 2000, FA20_requests: 19610,
  ours: { P512_62: 512 / 62, P256_42: 256 / 42, P128_32_round5: 128 / 32 },
};
const R = out.ratios, kap = prof.kappaMeanDur;
out.fleetPoints = {
  F_AMpk: Math.round(512 / R.AM17[2000].peakProxy_ourWindows),
  F_AM: Math.round(512 / R.AM17[2000].avg),
  F_AM_sun_fri: [Math.round(512 / R.AM17[2000].sun), Math.round(512 / R.AM17[2000].fri)],
  F_AM3k: Math.round(512 / R.AM17[3000].avg),
  F_AM1k: Math.round(512 / R.AM17[1000].avg),
  F_FA: Math.round(512 / R.FA20),
  kappa: kap,
  F_AM3k_t: Math.round(512 * kap / R.AM17[3000].avg),
  F_AM_t: Math.round(512 * kap / R.AM17[2000].avg),
  F_FA_t: Math.round(512 * kap / R.FA20),
  kappaSensitivity: Object.fromEntries([1.4, 1.5, 1.6].map(k => [k, { AM3k: Math.round(512 * k / R.AM17[3000].avg), AM2k: Math.round(512 * k / R.AM17[2000].avg), FA: Math.round(512 * k / R.FA20) }])),
  oursInTaxiEquivalents: { P512_62: 512 / 62 * kap, P256_42: 256 / 42 * kap },
};
// 6. density per km2 (same island in all three studies, so ratios do not depend on the area figure)
const area = out.network.grid250Km2;
out.density = { areaKm2_used: area, areaNote: 'grid250 coverage of FleetPy nodes; same Manhattan for AM17/FA20/ours',
  AM17_2000: { vehPerKm2: 2000 / area, reqPerKm2PerH_avg: R.AM17_per2hAvg / 2 / area, reqPerKm2PerH_peakProxy: R.AM17_per2h_ourWindowsProxy / 2 / area },
  AM17_3000: { vehPerKm2: 3000 / area },
  FA20: { vehPerKm2: 2000 / area, reqPerKm2PerH: 19610 / 2 / area },
  ours_P512: { vehPerKm2: 62 / area, reqPerKm2PerH: 512 / 2 / area },
  ours_P256: { vehPerKm2: 42 / area, reqPerKm2PerH: 256 / 2 / area },
  ratio_AM17avg_to_P512_requests: R.AM17_per2hAvg / 512, ratio_FA20_to_P512_requests: 19610 / 512,
  ratio_AM17_2000_to_62_vehicles: 2000 / 62 };
fs.writeFileSync(path.join(ME, 'facts', 'facts.json'), JSON.stringify(out, null, 1));
const { rows: _r, ...cellsNoRows } = out.cells;
console.log(JSON.stringify({ ...out, cells: cellsNoRows }, null, 1));
