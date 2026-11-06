#!/usr/bin/env node
// System report §7: how accurate is the traffic forecast? The report says its error is 2.6-3.1 % against about 5 % for
// holding the current traffic flat: the "DEV Sep-Oct school days" prev4 rows below (w08 2.64 vs 4.98, w17 3.09 vs 9.34;
// so "about 5 %" is the w08 value). Rebuilds the wf7 forecast-accuracy outputs from the shipped TLC bin effects.
// Nothing is rerun in the emulator.
//
// The traffic factor f(d, b): how slow Manhattan traffic is on day d in 15-minute clock bin b (96 bins a day).
//   e(d, b)   bin effect of a two-way median polish of log trip duration, log duration = r(origin zone, destination
//             zone) + e(day, pickup bin) + noise, over one month of NYC Yellow taxi trips that start and end in
//             Manhattan (rate code 1, OD pairs with >= 20 trips; 4 alternations). The DuckDB query that made it from
//             the public TLC trip records is not shipped; data/tlc-yellow-bins-<month>.csv.gz hold its output:
//             e = column e_med, n = trips, one row per day x bin (the other columns are not used).
//   f(d, b)   = exp(e(d, b) - med), med = the month's e values sorted, at position floor(count / 2). Aug-Dec 2019 form
//             the index the emulator's closed-loop forecast reads (the <prefix>-index-2019.json output); the source
//             rule and calendar below are the ones in src/emulator/forecast.js, copied here so that this script needs
//             no other file.
//
// The forecast for target day d and a window starting at clock bin s0 (w08: 32 = 08:00, w17: 68 = 17:00):
//   sources   the same weekday 1..4 weeks before d (rule prev4, the one the emulator uses) or 1..8 weeks (prev8),
//             keeping only weekdays on or after the school start 2019-09-05 that are not in EXCL (school breaks and
//             federal holidays). Fewer than 2 sources: no forecast for that day and rule.
//   profile   m(b) = mean over the sources of f(source, s0 + b)
//   forecast  from origin bin s0 + o, h bins ahead: log f(d, s0 + o) + log m(o + h) - log m(o)
//   naive     the current factor held flat: log f(d, s0 + o)
// Errors of one day x window, over the origins o = 0..7 and horizons h = 1..8 (15 min to 2 h):
//   MAE15     mean of |forecast - log f(d, s0 + o + h)| over the 64 pairs (printed x 100, %)
//   MAE2h     mean over the 8 origins of |m(o + 8) / m(o) - g|, g = f(d, s0 + o + 8) / f(d, s0 + o) the true 2-h
//             growth; naive |1 - g| (printed x 100, percentage points)
// Days: the weekdays from 2019-09-01 on. "school" = the day could itself be a source. Groups, fixed by the calendar
// before the runs: DEV = Sep-Oct (a pipeline check; nothing was tuned), TEST = Nov-Dec, and the 18 November days whose
// Via cells were used for the dispatch test. Per window, group and rule: mean errors, days where the forecast beats
// naive, and the exact two-sided sign test
//   p = sum of C(n, i) / 2^n over the i with min(i, n - i) <= min(k, n - k), capped at 1 (k = days better, n = days).
//
// First, a check of the index itself: the same index built from Yellow 2018-11 against FleetPy's Manhattan
// travel-time factors on 2018-11-12..16 (data/fleetpy-tt-factors-<day>.csv.gz, one row per 15-minute bin), bins
// 06:00-22:00: Pearson correlation of the log level and of the log 2-hour change, per day and pooled.
// The output text keeps the study's labels (wf7, PREREG s.4, wf5 survey: the earlier study the check is compared to).
//
// Usage: node forecast_accuracy.js --data <dir> --out <prefix>
//   <dir> holds tlc-yellow-bins-{2018-11,2019-08..2019-12}.csv.gz and fleetpy-tt-factors-2018-11-{12..16}.csv.gz
//   (shipped in data/). Writes <prefix>.txt (the tables; also printed), <prefix>-index-2019.json (f by day, Aug-Dec
//   2019), <prefix>-rows.json (the errors per day x window x rule) and <prefix>-summary.json (per window x group x
//   rule). expected/forecast-accuracy.txt, -index-2019.json, -rows.json and -summary.json are the stored outputs.
'use strict';
const fs = require('fs'), path = require('path'), zlib = require('zlib');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
if (!arg('data') || !arg('out')) throw new Error('usage: node forecast_accuracy.js --data <dir> --out <prefix>');
const dataDir = arg('data'), outPrefix = arg('out');
const readText = file => zlib.gunzipSync(fs.readFileSync(file)).toString('utf8');
const binsFile = month => path.join(dataDir, `tlc-yellow-bins-${month}.csv.gz`);

// ---------------------------------------------------------------- index, sources, calendar (as src/emulator/forecast.js)
// one month's bins file -> [{ d: day, b: bin, e: e_med, n: trips }]
function readBins(file) {
  const L = readText(file).trim().split(/\r?\n/);
  const h = L[0].split(',');
  const iD = h.indexOf('day'), iB = h.indexOf('bin'), iE = h.indexOf('e_med'), iN = h.indexOf('n');
  return L.slice(1).map(l => { const v = l.split(','); return { d: v[iD], b: +v[iB], e: +v[iE], n: +v[iN] }; });
}
// files: one bins file per month; each month is normalised by its own median bin effect.
// Returns F[day][bin] = f and N[day][bin] = trips (NaN and 0 where a bin has no row), days in file order.
function loadIndex(files) {
  const F = {}, N = {};
  for (const f of files) {
    const rows = readBins(f);
    const s = rows.map(r => r.e).sort((a, b) => a - b);
    const med = s[Math.floor(s.length / 2)];
    for (const r of rows) {
      (F[r.d] ||= new Array(96).fill(NaN))[r.b] = Math.exp(r.e - med);
      (N[r.d] ||= new Array(96).fill(0))[r.b] = r.n;
    }
  }
  return { F, N };
}

// non-school days of the NYC school calendar 2019-20 and federal holidays: never a source (typed from the published
// calendar when the study was planned, not re-checked)
const EXCL = new Set(['2019-09-30', '2019-10-01', '2019-10-09', '2019-10-14', '2019-11-05', '2019-11-11', '2019-11-28',
  '2019-11-29', '2019-12-24', '2019-12-25', '2019-12-26', '2019-12-27', '2019-12-30', '2019-12-31']);
const SCHOOL_START = '2019-09-05';
const dateAdd = (d, k) => { const x = new Date(d + 'T12:00:00Z'); x.setUTCDate(x.getUTCDate() + k); return x.toISOString().slice(0, 10); };
const dow = d => new Date(d + 'T12:00:00Z').getUTCDay();
const isWeekday = d => { const w = dow(d); return w >= 1 && w <= 5; };
const isSourceOk = d => isWeekday(d) && d >= SCHOOL_START && !EXCL.has(d);
const MIN_SOURCES = 2;

// the source days of `day` under `rule` (prev4 or prev8), nearest first; [] if fewer than MIN_SOURCES
function sources(day, rule, F) {
  const weeks = rule === 'prev4' ? 4 : rule === 'prev8' ? 8 : null;
  if (weeks === null) throw new Error('rule ' + rule);
  const out = [];
  for (let k = 1; k <= weeks; k++) { const s = dateAdd(day, -7 * k); if (isSourceOk(s) && F[s]) out.push(s); }
  return out.length >= MIN_SOURCES ? out : [];
}

// ---------------------------------------------------------------- output and statistics
const out = []; const say = s => { out.push(s); console.log(s); };
const mean = a => a.reduce((x, y) => x + y, 0) / a.length;
// Pearson correlation
const corr = (a, b) => {
  const ma = mean(a), mb = mean(b);
  let sab = 0, saa = 0, sbb = 0;
  for (let i = 0; i < a.length; i++) { sab += (a[i] - ma) * (b[i] - mb); saa += (a[i] - ma) ** 2; sbb += (b[i] - mb) ** 2; }
  return sab / Math.sqrt(saa * sbb);
};

// ---------------------------------------------------------------- check of the index: 2018-11 vs FleetPy
{
  const { F } = loadIndex([binsFile('2018-11')]);
  say('# Validation: wf7 pickup-bin TLC-MP15 (Yellow 2018-11) vs FleetPy Manhattan tt_factors, bins 06:00-22:00');
  const pa = [], pb = [], ca = [], cb = [];
  for (const d of ['2018-11-12', '2018-11-13', '2018-11-14', '2018-11-15', '2018-11-16']) {
    const f = path.join(dataDir, `fleetpy-tt-factors-${d}.csv.gz`);
    // FleetPy's factor by bin: column 0 = simulation time (s), column 2 = travel_time_factor
    const R = new Array(96).fill(NaN);
    for (const l of readText(f).trim().split(/\r?\n/).slice(1)) { const v = l.split(','); R[Math.round(+v[0] / 900)] = +v[2]; }
    const a = [], b = [], c1 = [], c2 = [];
    for (let k = 24; k < 88; k++) { a.push(Math.log(F[d][k])); b.push(Math.log(R[k])); }
    for (let k = 24; k + 8 <= 88; k++) { c1.push(Math.log(F[d][k + 8] / F[d][k])); c2.push(Math.log(R[k + 8] / R[k])); }
    pa.push(...a); pb.push(...b); ca.push(...c1); cb.push(...c2);
    say(`  ${d}: corr(log level)=${corr(a, b).toFixed(3)} corr(2-h change)=${corr(c1, c2).toFixed(3)}`);
  }
  say(`  POOLED: corr(log level)=${corr(pa, pb).toFixed(3)} corr(2-h change)=${corr(ca, cb).toFixed(3)}  (wf5 survey pickup-bin: 0.992-0.997 / 0.978-0.990)`);
}

// ---------------------------------------------------------------- the 2019 index
const months = ['2019-08', '2019-09', '2019-10', '2019-11', '2019-12'];
const { F, N } = loadIndex(months.map(binsFile));
fs.writeFileSync(outPrefix + '-index-2019.json', JSON.stringify(F));
// quality check: bins 06:00-22:00 with fewer than 30 trips
let low = 0; for (const d of Object.keys(N)) for (let k = 24; k < 88; k++) if (N[d][k] < 30) low++;
say(`\n# 2019 index: ${Object.keys(F).length} days (Aug-Dec), bins 06-22 with < 30 trips: ${low}`);

// ---------------------------------------------------------------- errors per day x window x rule
const WIN = { w08: 32, w17: 68 };
const days = Object.keys(F).sort().filter(d => d >= '2019-09-01' && isWeekday(d));
const rules = ['prev4', 'prev8'];
const rows = [];
for (const d of days) for (const [w, s0] of Object.entries(WIN)) {
  const f = F[d];
  const r = { day: d, win: w, month: d.slice(0, 7), school: isSourceOk(d) };
  for (const rule of rules) {
    const src = sources(d, rule, F);
    if (!src.length) { r[rule] = null; continue; }
    const m = Array.from({ length: 24 }, (_, b) => mean(src.map(x => F[x][s0 + b])));   // profile m(b), b = 0..23
    let e15 = 0, n15 = 0, e2 = 0, n2 = 0, z15 = 0, z2 = 0;
    for (let o = 0; o < 8; o++) {
      for (let h = 1; h <= 8; h++) {
        const truth = Math.log(f[s0 + o + h]);
        const fc = Math.log(f[s0 + o]) + Math.log(m[o + h]) - Math.log(m[o]);
        const nv = Math.log(f[s0 + o]);
        e15 += Math.abs(fc - truth); z15 += Math.abs(nv - truth); n15++;
      }
      const g = f[s0 + o + 8] / f[s0 + o], gh = m[o + 8] / m[o];
      e2 += Math.abs(gh - g); z2 += Math.abs(1 - g); n2++;
    }
    r[rule] = { nsrc: src.length, mae15: e15 / n15, naive15: z15 / n15, mae2h: e2 / n2, naive2h: z2 / n2 };
  }
  // the day's own 2-h growth from the window start (descriptive)
  r.g2h = f[s0 + 8] / f[s0];
  rows.push(r);
}
fs.writeFileSync(outPrefix + '-rows.json', JSON.stringify(rows, null, 0));

// ---------------------------------------------------------------- group means and sign tests
// exact two-sided sign test: k of n days better
const binom = (k, n) => {
  let p = 0;
  const c = (n, k) => { let r = 1; for (let i = 0; i < k; i++) r = r * (n - i) / (i + 1); return r; };
  for (let i = 0; i <= n; i++) if (Math.min(i, n - i) <= Math.min(k, n - k)) p += c(n, i);
  return Math.min(1, p / 2 ** n);
};
say('\n# Forecast accuracy of the 15-min factor (PREREG s.4): MAE15 = mean |log error| over 8 origins x horizons 15 min..2 h (%),');
say('#   MAE2h = mean |predicted - true 2-h growth| (percentage points). naive = current factor held flat. Days = weekdays with an active rule.');
say('# group                      rule   days  MAE15 fc  naive | MAE2h fc  naive | days fc better (15 / 2h)  sign-test p (15 / 2h)');
const groups = [
  ['DEV Sep-Oct school days', r => (r.month === '2019-09' || r.month === '2019-10') && r.school],
  ['TEST Nov school days', r => r.month === '2019-11' && r.school],
  ['TEST Dec school days', r => r.month === '2019-12' && r.school],
  ['TEST Nov-Dec school days', r => (r.month === '2019-11' || r.month === '2019-12') && r.school],
  ['TEST Nov-Dec all weekdays', r => r.month === '2019-11' || r.month === '2019-12'],
  ['Via test days (18)', r => ['2019-11-01', '2019-11-04', '2019-11-05', '2019-11-06', '2019-11-07', '2019-11-08', '2019-11-12',
    '2019-11-13', '2019-11-14', '2019-11-15', '2019-11-18', '2019-11-19', '2019-11-20', '2019-11-21', '2019-11-22', '2019-11-25',
    '2019-11-26', '2019-11-27'].includes(r.day)],
];
const summary = {};
for (const w of ['w08', 'w17']) for (const [name, sel] of groups) for (const rule of rules) {
  const R = rows.filter(r => r.win === w && sel(r) && r[rule]);
  if (!R.length) continue;
  const m15 = mean(R.map(r => r[rule].mae15)), n15 = mean(R.map(r => r[rule].naive15));
  const m2 = mean(R.map(r => r[rule].mae2h)), n2 = mean(R.map(r => r[rule].naive2h));
  const b15 = R.filter(r => r[rule].mae15 < r[rule].naive15).length, b2 = R.filter(r => r[rule].mae2h < r[rule].naive2h).length;
  summary[`${w}|${name}|${rule}`] = { days: R.length, mae15: m15, naive15: n15, mae2h: m2, naive2h: n2, better15: b15, better2h: b2,
    p15: binom(b15, R.length), p2h: binom(b2, R.length) };
  say(`${w} ${name.padEnd(26)} ${rule}  ${String(R.length).padStart(3)}   ${(100 * m15).toFixed(2).padStart(5)}  ${(100 * n15).toFixed(2).padStart(5)} |  ${(100 * m2).toFixed(2).padStart(5)}  ${(100 * n2).toFixed(2).padStart(5)} |  ${b15}/${R.length}  ${b2}/${R.length}   ${binom(b15, R.length).toPrecision(2)} / ${binom(b2, R.length).toPrecision(2)}`);
}
// the 8 worst forecast day-windows (Nov-Dec, prev4, by MAE2h)
say('\n# Largest prev4 errors (MAE2h, Nov-Dec):');
for (const r of rows.filter(r => r.month >= '2019-11' && r.prev4).sort((a, b) => b.prev4.mae2h - a.prev4.mae2h).slice(0, 8))
  say(`  ${r.day} ${r.win} MAE2h ${(100 * r.prev4.mae2h).toFixed(1)} pp (naive ${(100 * r.prev4.naive2h).toFixed(1)}), true 2-h growth from window start ${(100 * (r.g2h - 1)).toFixed(1)}%${r.school ? '' : ' (non-school day)'}`);
fs.writeFileSync(outPrefix + '-summary.json', JSON.stringify(summary, null, 1));
fs.writeFileSync(outPrefix + '.txt', out.join('\n') + '\n');
