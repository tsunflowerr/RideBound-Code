#!/usr/bin/env node
// The final report's tables that compare our system with Elmi's rule, rebuilt from the shipped rows.
//
//   ours    FS-G2-SHRC71-CL   Shadow + Rule C + closed-loop forecast (our system)
//   elmi    FS-G2-SHHZ71-N    Shadow + Elmi's relaxed fence, no forecast
//   fenceF  FS-G2-SHHZ71-CL   Elmi's fence + forecast           (only for the §6 split)
//   ruleN   FS-G2-SHRC71-N    Rule C without forecast           (only for the §6 split, reported only)
//
// Three sources describe the same 30 days (F14: 14 days, F16: 16 days; windows w08, w17; resamples r0, r1, r2):
//   plain    the emulator's own rows                       data/wf20-fullsys-rows.ndjson.gz
//   adapter  the emulator's predictions, rider by rider    data/f1x-adapter-predictions.tsv.gz (+ -attr- for the split arms)
//   real     the real RideBound runs, rider by rider       data/f1x-real-riders.tsv.gz
// Promise reversals (visRev60) and TV5 per cell come from data/f1x-sequences.json.gz (real and emulator side).
//
// Metrics: late60First = served riders dropped more than 60 s after the first promised drop time (p0);
// visRev60 = riders whose shown drop time moved by 60 s or more and then back (a promise reversal);
// TV5 = total movement of the shown drop time, counting only moves of 5 s or more (seconds per served rider).
//
// expected/ holds the stored results of the original scripts (synth_real_30d.py, d1_split.py, d1_gap.py,
// compare_via_full.py, eta_metrics.py and promise.js at commit ebca912), with their local path fields removed.
//
// Output file (same name in expected/)        Report    Rebuilt by
//   f14-, f16-riders-compare.json             §3        ridersCompare()   real vs predicted, rider by rider
//   f14-, f16-eta.json (served, visRev60)     §4        realRunCounts()
//   synth-real-30d.json, .md                  §5        contrast()        day contrast ours - elmi, 3 sources
//   promise.json, promise.md                  §5        promiseTables()   promise metrics (port of promise.js)
//   d1-split.json, .md; d1-gap.txt            §6        split(), d1Gap()  plain vs adapter, rule C vs forecast
//
// Usage: node report_tables.js --data <dir> [--out <dir>] [--check <dir>]
//   --out    write the files above into <dir>
//   --check  compare every value with the stored results in <dir>; exit code 1 on any difference
//   neither  print the §5 table
//
// Some stored values need data that is not shipped: the timed adapter sequences of the two split arms, the run
// transcripts and the per-rider run evidence. They print as "—", and --check counts them as open (OPEN below).
'use strict';
const fs = require('fs'), path = require('path'), zlib = require('zlib'), crypto = require('crypto');

const ARMS = {   // name: [role in the rider files, config|mode|arm in the plain rows]
  ours: ['FS-G2-SHRC71-CL', 'G2|CL|SHRC71'],
  elmi: ['FS-G2-SHHZ71-N', 'G2|N|SHHZ71'],
  fenceF: ['FS-G2-SHHZ71-CL', 'G2|CL|SHHZ71'],
  ruleN: ['FS-G2-SHRC71-N', 'G2|N|SHRC71'],
};
const ARM_OF_ROLE = Object.fromEntries(Object.entries(ARMS).map(([arm, [role]]) => [role, arm]));
const ARM_OF_PLAIN = Object.fromEntries(Object.entries(ARMS).map(([arm, [, plain]]) => [plain, arm]));
const PANELS = ['f14', 'f16'];
const GROUPS = ['F14', 'F16', 'F14+F16'];
const DAY_PARTS = ['day', 'w08', 'w17'];          // whole day = w08 + w17
const SOURCES = ['plain', 'adapter', 'real'];
const COUNTS = ['served', 'visRev60', 'late60First', 'sumTV5'];
const METRICS = [   // id, label in the report, value of one day (or window) of one arm
  ['served', 'Phục vụ (khách/ngày)', x => x.served],
  ['visRev60', 'Đảo hứa (khách/ngày)', x => x.visRev60],
  ['late60First/1000', 'Trễ >60 s so với hứa đầu /1000', x => 1000 * x.late60First / x.served],
  ['TV5', 'TV5 (giây/khách)', x => x.sumTV5 / 1000 / x.served],
];
const SPLIT = [['total', 'ours', 'elmi'], ['ruleC', 'ours', 'fenceF'], ['forecast', 'fenceF', 'elmi']];

// ---------------------------------------------------------------- inputs
// data/MANIFEST.tsv lists every input with the sha256 of its uncompressed content; a changed input stops the run.
function openData(dir) {
  const manifest = new Map(fs.readFileSync(path.join(dir, 'MANIFEST.tsv'), 'utf8').split('\n').slice(1)
    .filter(Boolean).map(line => line.split('\t').slice(0, 2)));
  return file => {
    const raw = fs.readFileSync(path.join(dir, file)), text = file.endsWith('.gz') ? zlib.gunzipSync(raw) : raw;
    const sha = crypto.createHash('sha256').update(text).digest('hex');
    if (manifest.get(file) !== sha) throw new Error(`${file}: sha256 ${sha} is not the one in MANIFEST.tsv`);
    return text.toString('utf8');
  };
}
function parseTsv(text) {
  const [head, ...lines] = text.split('\n').filter(line => line.length);
  const keys = head.split('\t');
  return lines.map(line => {
    const cells = line.split('\t'), row = {};
    keys.forEach((k, i) => (row[k] = cells[i]));
    return row;
  });
}
function loadData(dir) {
  const read = openData(dir);
  const data = { plainRows: read('wf20-fullsys-rows.ndjson.gz').split('\n').filter(Boolean).map(JSON.parse) };
  for (const p of PANELS) {
    data[p] = {
      real: parseTsv(read(`${p}-real-riders.tsv.gz`)),
      pred: parseTsv(read(`${p}-adapter-predictions.tsv.gz`)),
      attr: parseTsv(read(`${p}-adapter-attr-predictions.tsv.gz`)),
      sequences: JSON.parse(read(`${p}-sequences.json.gz`)).results,
    };
  }
  return data;
}
// rider cell "d20190919-vf-r0-w08" -> [day "2019-09-19", window "w08", resample "r0"]
function cellKey(cell) {
  const m = /^d(\d{4})(\d{2})(\d{2})-vf-(r\d)-(w\d\d)$/.exec(cell);
  if (!m) throw new Error('cell name ' + cell);
  return [`${m[1]}-${m[2]}-${m[3]}`, m[5], m[4]];
}

// ---------------------------------------------------------------- statistics (fsan.js rules, as synth_real_30d.py)
// The t quantile of a 95 % interval (0.975) for the degrees of freedom that occur: 13, 15, 29 (14, 16, 30 days).
// The original found them by bisection on the t distribution; tests/ checks them by integration.
const T975 = { 13: 2.1603686564627704, 15: 2.1314495455597706, 29: 2.0452296421329086 };
function tQuantile(table, df) {
  if (!(df in table)) throw new Error(`no t quantile for ${df} degrees of freedom`);
  return table[df];
}
const sum = xs => xs.reduce((a, b) => a + b, 0);   // plain left-to-right sum, as Python 3.10's sum() (3.12 compensates)
const mean = xs => sum(xs) / xs.length;
// Exact two-sided sign test, ties dropped: 2 * P(X <= min(n+, n-)) for X ~ Binomial(n+ + n-, 1/2).
function signTest(nPlus, nMinus) {
  const n = nPlus + nMinus;
  if (!n) return 1;
  let c = 1n, tail = 0n;
  for (let i = 0; i <= Math.min(nPlus, nMinus); i++) { if (i > 0) c = c * BigInt(n - i + 1) / BigInt(i); tail += c; }
  return Math.min(1, 2 * Number(tail) / 2 ** n);
}
// Paired day differences -> mean, 95 % t interval, median, signs, sign-test p. A NaN marks a value the shipped data
// cannot give (an arm without reversal sequences); the whole summary is then null and prints as "—".
function summary(d) {
  if (d.some(Number.isNaN)) return null;
  const n = d.length, m = mean(d);
  const sd = Math.sqrt(sum(d.map(x => (x - m) ** 2)) / (n - 1)), se = sd / Math.sqrt(n), t = tQuantile(T975, n - 1);
  const s = d.slice().sort((a, b) => a - b), median = n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
  const nPlus = d.filter(x => x > 1e-9).length, nMinus = d.filter(x => x < -1e-9).length;
  return { n, mean: m, se, lo: m - t * se, hi: m + t * se, median, nPlus, nMinus, ties: n - nPlus - nMinus,
    p: signTest(nPlus, nMinus) };
}

// ---------------------------------------------------------------- Python number formatting
// f"{x:.2f}" rounds the exact binary value of x and sends an exact tie to the even digit (343.125 -> 343.12).
// JS toFixed also rounds the exact value but sends a tie away from zero (343.13), so the rounding is redone here.
function pyFixed(x, digits) {
  const [whole, frac] = Math.abs(x).toFixed(80).split('.');   // exact for every double these tables print
  const rest = frac.slice(digits), half = '5'.padEnd(rest.length, '0');
  let n = BigInt(whole + frac.slice(0, digits));
  if (rest > half || (rest === half && n % 2n === 1n)) n += 1n;
  const s = n.toString().padStart(digits + 1, '0');
  return (x < 0 ? '-' : '') + s.slice(0, -digits) + '.' + s.slice(-digits);
}
const fmt = x => (x === null || x === undefined || Number.isNaN(x) ? '—' : pyFixed(x, 2).replace('.', ','));
// p as synth_real_30d.py prints it: "9e-7" below 0.001, else 3 decimals. (p is k / 2^n, so f"{p:.0e}" never ties.)
const fmtP = p => (p < 1e-3 ? p.toExponential(0) : pyFixed(p, 3).replace('.', ','));
// str(float) as Python prints it (d1-gap.txt): shortest digits, "1.0" for whole numbers, exponent below 1e-4.
function pyRepr(x) {
  const [digits, exp] = x.toExponential().split('e'), e = Number(exp);
  if (e < -4 || e >= 16) return `${digits}e${e < 0 ? '-' : '+'}${String(Math.abs(e)).padStart(2, '0')}`;
  return Number.isInteger(x) ? x.toFixed(1) : String(x);
}

// ---------------------------------------------------------------- per-cell counts: "arm|day|win|rs" -> {served, ...}
function riderCells(rows) {   // served and late60First (A - p0 > 60 s against the first promise) per cell
  const cells = new Map();
  for (const r of rows) {
    const arm = ARM_OF_ROLE[r.role];
    if (!arm) continue;
    const key = [arm, ...cellKey(r.cell)].join('|');
    if (!cells.has(key)) cells.set(key, { served: 0, late60First: 0 });
    const c = cells.get(key);
    if (r.served === '1') { c.served++; if (+r.A - +r.p0 > 60000) c.late60First++; }
  }
  return cells;
}
function addSequences(cells, sequences, side) {   // side: 'Emulator' (adapter) or 'Real'
  for (const r of sequences) {
    const arm = ARM_OF_ROLE[r.role];
    if (!arm) continue;
    const c = cells.get([arm, ...cellKey(r.cell)].join('|'));
    if (!c) throw new Error(`sequences for ${r.role} ${r.cell} but no rider rows`);
    c.visRev60 = r['visRev60' + side];
    c.sumTV5 = r['tv5' + side];
  }
  return cells;
}
function plainCells(rows) {
  const cells = new Map();
  for (const r of rows) {
    const arm = ARM_OF_PLAIN[`${r.config}|${r.mode}|${r.arm}`];
    if (!arm) continue;
    const key = [arm, r.day, r.win, r.rs].join('|');
    if (cells.has(key)) throw new Error('two plain rows for ' + key);
    cells.set(key, Object.fromEntries(COUNTS.map(k => [k, r[k]])));
  }
  return cells;
}

// ---------------------------------------------------------------- day tables and contrasts (fsan.js dayTable)
// One arm: window value = mean over the resamples r0, r1, r2; day = w08 + w17. A day counts only when both windows
// have all three resamples.
function dayTable(cells, arm, days) {
  const byDay = {};
  for (const [key, counts] of cells) {
    const [a, day, win, rs] = key.split('|');
    if (a === arm && days.has(day)) (byDay[day] ??= []).push({ win, rs, counts });
  }
  const table = {};
  for (const [day, list] of Object.entries(byDay)) {
    const row = {};
    for (const win of ['w08', 'w17']) {
      const runs = list.filter(c => c.win === win);
      if (runs.map(c => c.rs).sort().join() !== 'r0,r1,r2') break;
      row[win] = Object.fromEntries(COUNTS.map(k => [k, runs.reduce((s, c) => s + c.counts[k], 0) / 3]));
    }
    if (row.w08 && row.w17) table[day] = { ...row, day: Object.fromEntries(COUNTS.map(k => [k, row.w08[k] + row.w17[k]])) };
  }
  return table;
}
// §5: ours - elmi by day, over the days both arms have.
function contrast(cells, days) {
  const A = dayTable(cells, 'ours', days), B = dayTable(cells, 'elmi', days);
  const common = Object.keys(A).filter(d => d in B).sort();
  const out = { days: common.length, excluded: [...days].filter(d => !common.includes(d)).sort() };
  for (const part of DAY_PARTS) {
    out[part] = {};
    for (const [id, , f] of METRICS) {
      const x = common.map(d => f(A[d][part])), y = common.map(d => f(B[d][part]));
      out[part][id] = { ours: mean(x), elmi: mean(y), diff: summary(x.map((v, i) => v - y[i])) };
    }
  }
  return out;
}
// §6: the level of the four arms and the three paired differences of SPLIT, over the days all four arms have.
function split(cells, days) {
  const T = Object.fromEntries(Object.keys(ARMS).map(arm => [arm, dayTable(cells, arm, days)]));
  const common = Object.keys(T.ours).filter(d => Object.values(T).every(t => d in t)).sort();
  const out = { days: common.length };
  for (const part of DAY_PARTS) {
    out[part] = {};
    for (const [id, , f] of METRICS) {
      out[part][id] = {
        level: Object.fromEntries(Object.keys(ARMS).map(arm => [arm, mean(common.map(d => f(T[arm][d][part])))])),
        diff: Object.fromEntries(SPLIT.map(([name, a, b]) =>
          [name, summary(common.map(d => f(T[a][d][part]) - f(T[b][d][part])))])),
      };
    }
  }
  return out;
}
// §6 noise check (d1_gap.py): per day, adapter difference - plain difference, for each part and each arm's level.
function d1Gap(plain, adapter, days, label) {
  const tables = cells => Object.fromEntries(Object.keys(ARMS).map(arm => [arm, dayTable(cells, arm, days)]));
  const TP = tables(plain), TA = tables(adapter);
  const common = Object.keys(TP.ours).filter(d => [...Object.values(TP), ...Object.values(TA)].every(t => d in t)).sort();
  const interval = s => `${fmt(s?.mean)} [${fmt(s?.lo)}; ${fmt(s?.hi)}] (${s ? s.nPlus : '—'}/${s ? s.nMinus : '—'})`;
  const lines = [];
  for (const [id, , f] of METRICS) {
    for (const [name, a, b] of SPLIT) {
      const s = summary(common.map(d => (f(TA[a][d].day) - f(TA[b][d].day)) - (f(TP[a][d].day) - f(TP[b][d].day))));
      lines.push(`  ${id.padEnd(12)} ${name.padEnd(9)} adapter-plain ${interval(s)} p=${s ? pyRepr(s.p) : '—'}`);
    }
    for (const arm of Object.keys(ARMS)) {
      const s = summary(common.map(d => f(TA[arm][d].day) - f(TP[arm][d].day)));
      lines.push(`  ${id.padEnd(12)} level ${arm.padEnd(6)} adapter-plain ${interval(s)}`);
    }
  }
  return [`== ${label}: ${common.length} days`, ...lines];
}

// ---------------------------------------------------------------- §3 and §4: real runs against the predictions
// compare_via.py's rider rule: a rider is identical when these fields agree; a cell-arm is exact when every rider is
// identical and no rider is on one side only.
const RIDER_FIELDS = ['t0', 'p0', 'A', 'served', 'sumAbsC', 'nonzeroC', 'netC', 'maxAbsC', 'shared'];
const norm = v => {   // blank and NaN are equal; numbers compare as whole numbers
  v = (v ?? '').trim();
  if (['', 'NaN', 'nan', 'undefined'].includes(v)) return '';
  return /^-?d+(.d+)?$/.test(v) ? String(Math.trunc(+v)) : v;
};
function byCellArm(rows) {   // "role<TAB>cell" -> requestId -> row
  const m = new Map();
  for (const r of rows) {
    const k = r.role + '	' + r.cell;
    if (!m.has(k)) m.set(k, new Map());
    m.get(k).set(r.requestId, r);
  }
  return m;
}
function ridersCompare(realRows, predRows) {
  const real = byCellArm(realRows), predicted = byCellArm(predRows), results = [];
  // A tab sorts before every character of a role, so this is the order (role, cell).
  for (const key of [...real.keys()].sort()) {
    const [role, cell] = key.split('	'), R = real.get(key), P = predicted.get(key) || new Map();
    const both = [...R.keys()].filter(q => P.has(q))
      .sort((x, y) => (+R.get(x).t0 - +R.get(y).t0) || (x < y ? -1 : x > y ? 1 : 0));   // by booking time, then id
    let identical = 0, firstDifference = null;
    for (const q of both) {
      const differ = RIDER_FIELDS.filter(f => norm(R.get(q)[f]) !== norm(P.get(q)[f]));
      if (!differ.length) identical++;
      else if (!firstDifference) {
        const fields = Object.fromEntries(differ.map(f => [f, [R.get(q)[f], P.get(q)[f]]]));   // [real, predicted]
        firstDifference = { requestId: q, t0: R.get(q).t0, fields };
      }
    }
    const onlyReal = R.size - both.length, onlyEmulator = P.size - both.length;
    const riders = both.length + onlyReal + onlyEmulator;
    results.push({ arm: role, cell, riders, identical, exact: identical === riders, onlyReal, onlyEmulator, firstDifference });
  }
  const byArm = {};
  for (const r of results) { const a = (byArm[r.arm] ??= { cellArms: 0, exact: 0 }); a.cellArms++; a.exact += r.exact; }
  return {
    cellArmsCompared: results.length, cellArmsExact: results.filter(r => r.exact).length,
    riders: sum(results.map(r => r.riders)), ridersIdentical: sum(results.map(r => r.identical)), byArm, results,
  };
}
function realRunCounts(realRows, sequences) {   // §4: served riders and promise reversals of the real runs, per arm
  const real = {};
  for (const role of [ARMS.elmi[0], ARMS.ours[0]]) {
    real[role] = {
      servedRiders: realRows.filter(r => r.role === role && r.served === '1').length,
      visRev60: sum(sequences.filter(r => r.role === role).map(r => r.visRev60Real)),
    };
  }
  return { real };
}

// ---------------------------------------------------------------- §5 promise metrics (port of promise.js)
// Ta = ours, Elmi = elmi. Real riders take their Shadow class (aSH; 0 = guaranteed) and budget (bSH) from the adapter
// prediction of the same rider. Sums run in promise.js's order and the JSON keeps its key order, so promise.json and
// promise.md stay byte for byte. promise.js rounded t to 4 digits and formed the interval as (t * sd) / sqrt(n).
const PROMISE_T975 = { 13: 2.1604, 15: 2.1314, 29: 2.0452 };
const TA = ARMS.ours[0], EL = ARMS.elmi[0];
const PROMISE_COUNTS = ['served', 'late60First', 'early60First', 'onTime60', 'breachDeadline60', 'G_n', 'G_late60First',
  'breachLedger', 'G_ledgerBreach', 'chgAny', 'chgAny1', 'pDispLo', 'pDispHi', 'sumS', 'sumLate', 'noClass', 'netUp',
  'netDown'];
function riderPromiseCounts(r, cls) {   // one served rider -> counts (0/1) and seconds; cls = its adapter prediction
  if (!(r.served === '1' || r.served === 'true')) return null;
  const v = Object.fromEntries(PROMISE_COUNTS.map(k => [k, 0]));
  const A = +r.A, p0 = +r.p0, S = +r.sumAbsC, net = +r.netC, mx = +r.maxAbsC, nz = +r.nonzeroC;
  v.served = 1;
  if (A - p0 > 60000) v.late60First = 1;
  if (A - p0 < -60000) v.early60First = 1;
  if (Math.abs(A - p0) <= 60000) v.onTime60 = 1;
  v.sumLate = (A - p0) / 1000;
  v.sumS = S / 1000;
  if (nz > 0) v.chgAny = 1;
  if (mx >= 1000) v.chgAny1 = 1;
  if (net > 0) v.netUp = 1;
  if (net < 0) v.netDown = 1;
  // pDisp = at least one promise change of +5 s or more, decided from (S = sum |c|, net, max |c|, count nz):
  // P = sum of the positive changes, N = sum of |negative changes|. If max |c| > N the largest change is positive;
  // otherwise P / (nz - 1) >= 5 s forces a positive change >= 5 s. No rider is left undecided (else this throws).
  const P = (S + net) / 2, N = (S - net) / 2;
  let pDisp;
  if (nz === 0 || P === 0) pDisp = false;
  else if (N === 0 || mx > N) pDisp = mx >= 5000;
  else if (P < 5000) pDisp = false;
  else if (P / (nz - 1) >= 5000) pDisp = true;
  else throw new Error('pDisp undecided ' + r.requestId);
  if (pDisp) { v.pDispLo = 1; v.pDispHi = 1; }
  if (!cls) { v.noClass = 1; return v; }
  const B = +cls.bSH, guaranteed = +cls.aSH === 0;
  if (A > p0 + B + 60000) v.breachDeadline60 = 1;
  if (S > B + 1000) v.breachLedger = 1;
  if (guaranteed) { v.G_n = 1; if (A - p0 > 60000) v.G_late60First = 1; if (S > B + 1000) v.G_ledgerBreach = 1; }
  return v;
}
function riderPromiseCells(rows, predRows) {   // "role|yyyymmdd|win|rs" -> summed counts, in first-seen order
  const cls = new Map(predRows.map(r => [`${r.role}|${r.cell}|${r.requestId}`, r])), acc = {};
  for (const r of rows) {
    if (r.role !== TA && r.role !== EL) continue;
    const m = /^d(\d{8})-vf-(r\d)-(w\d\d)$/.exec(r.cell);
    if (!m) throw new Error('cell ' + r.cell);
    const v = riderPromiseCounts(r, cls.get(`${r.role}|${r.cell}|${r.requestId}`));
    if (!v) continue;
    const c = (acc[`${r.role}|${m[1]}|${m[3]}|${m[2]}`] ??= Object.fromEntries(PROMISE_COUNTS.map(x => [x, 0])));
    for (const x of PROMISE_COUNTS) c[x] += v[x];
  }
  return acc;
}
function plainPromiseCells(rows) {
  const acc = {}, roleOf = { 'SHRC71|CL': TA, 'SHHZ71|N': EL };
  for (const r of rows) {
    const role = r.config === 'G2' && roleOf[`${r.arm}|${r.mode}`];
    if (!role) continue;
    const k = `${role}|${r.day.replace(/-/g, '')}|${r.win}|${r.rs}`;
    if (acc[k]) throw new Error('two plain rows for ' + k);
    acc[k] = { served: r.served, late60First: r.late60First, breachDeadline60: r.breachDeadline60, G_n: r.G_n,
      G_late60First: r.G_late60First, breachLedger: r.breachLedger, G_ledgerBreach: r.G_ledgerBreach, chgAny1: r.pDispAny1,
      pDispLo: r.pDisp, pDispHi: r.pDisp, sumS: r.sumS / 1000, early60Last: r.early60Last, late60Last: r.late60Last };
  }
  return acc;
}
function promiseDays(acc) {   // day -> "role|win" -> mean over the three resamples
  const W = {};
  for (const [k, v] of Object.entries(acc)) {
    const [role, day, win, rs] = k.split('|');
    const o = ((W[day] ??= {})[`${role}|${win}`] ??= { rs: new Set(), sum: {} });
    o.rs.add(rs);
    for (const [x, y] of Object.entries(v)) if (typeof y === 'number') o.sum[x] = (o.sum[x] || 0) + y;
  }
  for (const D of Object.values(W)) for (const o of Object.values(D)) {
    if (o.rs.size !== 3) throw new Error('a window without its three resamples');
    for (const x in o.sum) o.sum[x] /= 3;
  }
  return W;
}
const perMille = key => X => (X[key] === undefined ? null : 1000 * X[key] / X.served);   // null: the source lacks it
const PROMISE_METRICS = [   // id, label, value of one day
  ['served', 'Khách phục vụ /ngày', X => X.served],
  ['late60First', 'Trễ >60 s so với hứa đầu /1000', X => 1000 * X.late60First / X.served],
  ['early60First', 'Sớm >60 s so với hứa đầu /1000', perMille('early60First')],
  ['onTime60', 'Đúng hứa đầu ±60 s /1000', perMille('onTime60')],
  ['meanLate', 'A − p0 trung bình (s)', X => (X.sumLate === undefined ? null : X.sumLate / X.served)],
  ['guar', 'Khách bảo đảm (% khách phục vụ)', X => 100 * X.G_n / X.served],
  ['G_late', 'Khách bảo đảm trễ >60 s /ngày', X => X.G_late60First],
  ['breachDeadline60', 'Vi phạm hạn chót /1000', X => 1000 * X.breachDeadline60 / X.served],
  ['breachLedger', 'Vượt sổ (Σ|c| > B_r + 1 s) /1000', X => 1000 * X.breachLedger / X.served],
  ['G_ledger', 'Khách bảo đảm vượt sổ /ngày', X => X.G_ledgerBreach],
  ['chgAny1', 'Có ≥1 lần dời cam kết ≥1 s /1000', X => 1000 * X.chgAny1 / X.served],
  ['pDispLo', 'Dời muộn ≥5 s (cận dưới) /1000', X => 1000 * X.pDispLo / X.served],
  ['pDispHi', 'Dời muộn ≥5 s (cận trên) /1000', X => 1000 * X.pDispHi / X.served],
  ['sumS', 'Tổng độ dời Σ|c| (s/khách)', X => X.sumS / X.served],
  ['early60Last', 'Sớm >60 s so với giờ công bố cuối /1000', perMille('early60Last')],
];
function promiseStats(d) {
  const n = d.length, m = mean(d);
  const sd = Math.sqrt(sum(d.map(x => (x - m) ** 2)) / (n - 1)), h = tQuantile(PROMISE_T975, n - 1) * sd / Math.sqrt(n);
  const np = d.filter(x => x > 1e-9).length, nm = d.filter(x => x < -1e-9).length;
  return { mean: m, lo: m - h, hi: m + h, np, nm, p: signTest(np, nm) };
}
function promiseTable(W, panel) {   // "part|metric" -> ta, el and the paired day difference ta - el
  const out = {};
  for (const part of DAY_PARTS) {
    const windows = part === 'day' ? ['w08', 'w17'] : [part];
    for (const [id, , f] of PROMISE_METRICS) {
      const dd = [], ta = [], el = [];
      for (const day of Object.keys(W).sort()) {
        if (panel && !panel.includes(day)) continue;
        const add = role => {
          const s = {};
          for (const w of windows) for (const [x, y] of Object.entries(W[day][`${role}|${w}`].sum)) s[x] = (s[x] || 0) + y;
          return s;
        };
        const a = f(add(TA)), b = f(add(EL));
        if (a === null || b === null || Number.isNaN(a) || Number.isNaN(b)) continue;
        dd.push(a - b); ta.push(a); el.push(b);
      }
      if (dd.length) out[`${part}|${id}`] = { n: dd.length, ta: mean(ta), el: mean(el), ...promiseStats(dd) };
    }
  }
  return out;
}
function promiseTables(data) {
  const accReal = {}, accPred = {};
  for (const p of PANELS) {
    Object.assign(accReal, riderPromiseCells(data[p].real, data[p].pred));
    Object.assign(accPred, riderPromiseCells(data[p].pred, data[p].pred));
  }
  const coverage = { riders: 0, noClass: 0 };
  for (const v of Object.values(accReal)) { coverage.riders += v.served; coverage.noClass += v.noClass; }
  const f14days = Object.keys(promiseDays(riderPromiseCells(data.f14.real, data.f14.pred))).sort();
  const res = { coverage, f14days: f14days.length };
  for (const [source, acc] of [['plain', plainPromiseCells(data.plainRows)], ['adapter', accPred], ['real', accReal]]) {
    const W = promiseDays(acc);
    const f16days = Object.keys(W).filter(d => !f14days.includes(d));
    res[source] = { all: promiseTable(W, null), f14: promiseTable(W, f14days), f16: promiseTable(W, f16days) };
  }
  return res;
}
function promiseMarkdown(res) {   // promise.js printed with JS rounding (toFixed), kept as it was
  const fix = (x, d = 2) => x.toFixed(d).replace('.', ','), pf = p => (p < 1e-3 ? p.toExponential(0) : fix(p, 3));
  const L = [];
  for (const part of DAY_PARTS) {
    L.push('', `### all ${part}`, '| Chỉ số | Nguồn | Ta | Elmi | Hiệu | KTC 95% | +/− | p |');
    for (const [id, label] of PROMISE_METRICS) for (const source of SOURCES) {
      const s = res[source].all[`${part}|${id}`];
      if (s) {
        L.push(`| ${label} | ${source} | ${fix(s.ta)} | ${fix(s.el)} | ${fix(s.mean)} | [${fix(s.lo)}; ${fix(s.hi)}] | ` +
          `${s.np}/${s.nm} | ${pf(s.p)} |`);
      }
    }
  }
  L.push('', 'coverage ' + JSON.stringify(res.coverage));
  return L.join('\n') + '\n';
}

// ---------------------------------------------------------------- all tables
const partTitle = part => (part === 'day' ? 'Cả ngày' : 'Khung ' + part);
function synthMarkdown(synth) {   // synth_real_30d.py's table
  const L = ['# Ta (FS-G2-SHRC71-CL) − Elmi (FS-G2-SHHZ71-N): bộ mô phỏng plain, dự đoán adapter, hệ thật', '',
    'Hiệu = của ta − của Elmi, theo ngày; [khoảng tin cậy 95% theo t]; (ngày +/−; p kiểm dấu). Quy tắc của fsan.js.', ''];
  for (const g of GROUPS) {
    L.push(`## ${g}`, '');
    for (const part of DAY_PARTS) {
      L.push(`### ${partTitle(part)}`, '', '| Chỉ số | Nguồn | Ngày | Ta | Elmi | Hiệu | Khoảng tin cậy | +/− | p |',
        '|---|---|---|---|---|---|---|---|---|');
      for (const [id, label] of METRICS) for (const source of SOURCES) {
        const r = synth[g][source], m = r[part][id], d = m.diff;
        L.push(`| ${label} | ${source} | ${r.days} | ${fmt(m.ours)} | ${fmt(m.elmi)} | ${fmt(d.mean)} | ` +
          `[${fmt(d.lo)}; ${fmt(d.hi)}] | ${d.nPlus}/${d.nMinus} | ${fmtP(d.p)} |`);
      }
      L.push('');
    }
  }
  return L.join('\n') + '\n';
}
function d1SplitMarkdown(d1) {   // d1_split.py's table; the rider count it checked needs the timed adapter export
  const L = ['# D1: bộ mô phỏng plain so với adapter, tách luật C và dự báo (G2)', '',
    'Kiểm: rev60/tv5 tính từ chuỗi có giờ công bố trùng tệp chuỗi đã khóa ở — khách của hai nhánh chính.',
    'Tổng = SHRC+dự báo − SHHZ; luật C = SHRC+dự báo − SHHZ+dự báo; dự báo = SHHZ+dự báo − SHHZ. Theo ngày, quy tắc fsan.js.', ''];
  for (const g of GROUPS) {
    L.push(`## ${g}`, '');
    for (const part of DAY_PARTS) {
      L.push(`### ${partTitle(part)}`, '', '| Chỉ số | Nguồn | Ngày | Tổng | Luật C | Dự báo | Tổng [KTC 95%] (+/−) |',
        '|---|---|---|---|---|---|---|');
      for (const [id, label] of METRICS) for (const source of ['plain', 'adapter']) {
        const r = d1[g][source], d = r[part][id].diff, t = d.total;
        L.push(`| ${label} | ${source} | ${r.days} | ${fmt(t.mean)} | ${fmt(d.ruleC?.mean)} | ${fmt(d.forecast?.mean)} | ` +
          `[${fmt(t.lo)}; ${fmt(t.hi)}] (${t.nPlus}/${t.nMinus}) |`);
      }
      L.push('');
    }
  }
  return L.join('\n') + '\n';
}
function computeTables(data) {
  const plain = plainCells(data.plainRows), adapter = new Map(), real = new Map(), adapter4 = new Map();
  const days = {};
  for (const p of PANELS) {
    const { real: realRows, pred, attr, sequences } = data[p];
    for (const [k, v] of addSequences(riderCells(pred), sequences, 'Emulator')) { adapter.set(k, v); adapter4.set(k, v); }
    for (const [k, v] of addSequences(riderCells(realRows), sequences, 'Real')) real.set(k, v);
    // The split arms have no shipped reversal sequences: their visRev60 and TV5 stay open (NaN).
    for (const [k, v] of riderCells(attr)) adapter4.set(k, { ...v, visRev60: NaN, sumTV5: NaN });
    days[p.toUpperCase()] = new Set(realRows.map(r => cellKey(r.cell)[0]));
  }
  days['F14+F16'] = new Set([...days.F14, ...days.F16]);
  const synth = {}, d1 = {}, gap = [];
  for (const g of GROUPS) {
    synth[g] = { plain: contrast(plain, days[g]), adapter: contrast(adapter, days[g]), real: contrast(real, days[g]) };
    d1[g] = { plain: split(plain, days[g]), adapter: split(adapter4, days[g]) };
    gap.push(...d1Gap(plain, adapter4, days[g], g));
  }
  const promise = promiseTables(data);
  const tables = {
    'synth-real-30d.json': { results: synth }, 'synth-real-30d.md': synthMarkdown(synth),
    'promise.json': promise, 'promise.md': promiseMarkdown(promise),
    'd1-split.json': { inputs: {}, results: d1 }, 'd1-split.md': d1SplitMarkdown(d1), 'd1-gap.txt': gap.join('\n') + '\n',
  };
  for (const p of PANELS) {
    tables[`${p}-riders-compare.json`] = ridersCompare(data[p].real, data[p].pred);
    tables[`${p}-eta.json`] = realRunCounts(data[p].real, data[p].sequences);
  }
  return tables;
}

// ---------------------------------------------------------------- --check: compare with the stored results
// Stored values the shipped data cannot give: they were made from the timed adapter export of the split arms, the run
// transcripts and the per-rider run evidence, which stay with the run archive.
const OPEN = [
  [/^d1-split\.json:(inputs\.sealedRidersChecked|results\.[^.]+\.adapter\.[^.]+\.(visRev60|TV5)\.(level\.(fenceF|ruleN)|diff\.(ruleC|forecast)\.\w+))$/,
    'reversals and TV5 of the split arms (timed adapter export)'],
  [/^f1[46]-riders-compare\.json:(finishedJobs|movesEqual|g2CellArms|results\.\d+\.(movesReal|movesEmulator|maxMovesPerDecision))$/,
    'G2 moves (run transcripts)'],
  [/^f1[46]-eta\.json:(?!real\.[^.]+\.(servedRiders|visRev60)$)/, 'ETA errors and per-rider reversals (evidence export)'],
];
const TEXT_WITH_OPEN = ['d1-split.md', 'd1-gap.txt'];
const TOLERANCE = 1e-12;   // relative to max(1, |stored|)
function checkTables(tables, dir) {
  const tally = { values: 0, open: {}, bad: [], worst: { rel: 0, where: '-' } }, report = [];
  const leaf = (where, want, got) => {
    if (got === undefined || (got === null && want !== null) || Number.isNaN(got)) {
      const reason = OPEN.find(([re]) => re.test(where));
      if (reason) tally.open[reason[1]] = (tally.open[reason[1]] || 0) + 1;
      else tally.bad.push(`${where}: missing, stored ${JSON.stringify(want)}`);
      return;
    }
    tally.values++;
    if (typeof want === 'number' && typeof got === 'number') {
      const rel = Math.abs(got - want) / Math.max(1, Math.abs(want));
      if (rel > tally.worst.rel) tally.worst = { rel, where };
      if (rel <= TOLERANCE) return;
    } else if (got === want) return;
    tally.bad.push(`${where}: got ${JSON.stringify(got)}, stored ${JSON.stringify(want)}`);
  };
  const walk = (where, want, got) => {
    if (want === null || typeof want !== 'object') return leaf(where, want, got);
    const inner = got !== null && typeof got === 'object' ? got : undefined;
    if (Array.isArray(want)) leaf(where + '.length', want.length, inner && inner.length);
    for (const k of Object.keys(inner || {})) if (!(k in want)) tally.bad.push(`${where}.${k}: not in the stored file`);
    for (const k of Object.keys(want)) walk(`${where}${where.endsWith(':') ? '' : '.'}${k}`, want[k], inner && inner[k]);
  };
  for (const [name, got] of Object.entries(tables)) {
    const stored = fs.readFileSync(path.join(dir, name), 'utf8');
    if (name.endsWith('.json')) {
      const before = { values: tally.values, open: sum(Object.values(tally.open)), bad: tally.bad.length };
      walk(name + ':', JSON.parse(stored), got);
      const open = sum(Object.values(tally.open)) - before.open;
      report.push(`${name.padEnd(26)} ${tally.values - before.values} values equal${open ? `, ${open} open` : ''}` +
        `${tally.bad.length > before.bad ? `, ${tally.bad.length - before.bad} DIFFERENT` : ''}`);
    } else if (got === stored) {
      report.push(`${name.padEnd(26)} byte-identical`);
    } else {
      const g = got.split('\n'), w = stored.replace(/\r\n/g, '\n').split('\n');
      const lineOk = (a, b) => (TEXT_WITH_OPEN.includes(name) ? sameLine(a, b) : a === b);
      const bad = g.length === w.length ? g.filter((line, i) => !lineOk(line, w[i])).length : g.length;
      const open = g.filter(line => line.includes('—')).length;
      if (bad) tally.bad.push(`${name}: ${bad} lines differ (${g.length} lines made, ${w.length} stored)`);
      report.push(`${name.padEnd(26)} ${g.length - 1 - bad} lines equal${open ? ` (${open} with open values)` : ''}` +
        `${bad ? `, ${bad} DIFFERENT` : ''}`);
    }
  }
  const open = Object.entries(tally.open).map(([why, n]) => `${n} ${why}`).join('; ');
  report.push(`TOTAL ${tally.values} values equal, ${tally.bad.length} differences; worst relative difference ` +
    `${tally.worst.rel.toExponential(2)} (${tally.worst.where})`, `open (not in the shipped data): ${open}`);
  for (const b of tally.bad.slice(0, 40)) report.push('  ' + b);
  return { ok: tally.bad.length === 0, values: tally.values, open: tally.open, report };
}
// A made line equals the stored line, except that "—" (an open value) matches any stored value, and a trailing
// "p=<number>" may differ within the tolerance (Python printed its log-space sign test, here the exact one).
function sameLine(made, stored) {
  const [m, mp] = made.split(' p='), [s, sp] = stored.split(' p=');
  if ((mp === undefined) !== (sp === undefined)) return false;
  const value = '[^\\s;\\[\\]()/|]+', escape = t => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  if (!new RegExp('^' + m.split('—').map(escape).join(value) + '$').test(s)) return false;
  return mp === undefined || mp === '—' || Math.abs(+mp - +sp) / Math.max(1, Math.abs(+sp)) <= TOLERANCE;
}

// ---------------------------------------------------------------- command line
function main(argv) {
  const opt = {};
  for (let i = 0; i < argv.length; i += 2) {
    if (!['--data', '--out', '--check'].includes(argv[i]) || argv[i + 1] === undefined) {
      throw new Error('usage: node report_tables.js --data <dir> [--out <dir>] [--check <dir>]');
    }
    opt[argv[i].slice(2)] = argv[i + 1];
  }
  if (!opt.data) throw new Error('--data <dir> is required');
  const tables = computeTables(loadData(opt.data));
  if (opt.out) {
    fs.mkdirSync(opt.out, { recursive: true });
    for (const [name, v] of Object.entries(tables)) {
      fs.writeFileSync(path.join(opt.out, name), typeof v === 'string' ? v : JSON.stringify(v, null, 1));
    }
  }
  if (opt.out) console.log(`wrote ${Object.keys(tables).length} files to ${opt.out}`);
  if (!opt.out && !opt.check) process.stdout.write(tables['synth-real-30d.md']);
  if (!opt.check) return 0;
  const result = checkTables(tables, opt.check);
  console.log(result.report.join('\n'));
  return result.ok ? 0 : 1;
}

module.exports = { loadData, computeTables, checkTables, summary, signTest, pyFixed, pyRepr, sameLine, T975 };
if (require.main === module) process.exitCode = main(process.argv.slice(2));
