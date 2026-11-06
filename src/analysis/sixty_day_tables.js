#!/usr/bin/env node
// The 60-day Shadow experiment: the system report's §6 (the INS and G2 tables, 7 arms each, and the numbers its text
// cites). Rebuilt from the recorded emulator rows of that experiment (wf15, cell set "confirm"). Nothing is rerun.
//
// The rows hold one emulator run per cell x config x arm: 60 weekdays of 2019 x 2 windows (w08: 08-10 h, w17: 17-19 h)
// x 3 resamples (r0, r1, r2) = 360 cells, each with 512 sampled requests and 62 vehicles.
//   configs  INS   insertion only
//            G2    insertion, plus moving at most one waiting rider to one of the 8 nearest vehicles per decision
//   arms     U     no limit on dispatch changes
//            EO35  Elmi's strict fence: the pickup may move up to B = round(0.35 x slack) past the first promise
//            EOHZ35 the relaxed fence: as EO35, but never tighter than the keep-route projection
//            RCE35 Rule C: the changes of a rider, summed in absolute value, stay within B (a per-rider ledger)
//            SHF, SHHZ, SHRC  the same three rules with the Shadow budget: 0 for guaranteed riders, the full slack
//                  for allowed riders; the arm name carries the Shadow share x (72 for INS, 71 for G2)
//
// Day value: for each count, the mean over the 3 resamples of a window, summed over the two windows. The rows must
// cover exactly 60 days and the 14 config x arm pairs, with 3 resamples in each window; otherwise the script stops.
// A level is the mean over the 60 days of the day value. Rates are pooled: a ratio of two levels, for example
// P(chg5) = 100 * level(pDisp) / level(served). They are not means of daily ratios (forecast_tables.js uses those).
// A paired contrast A - B takes the difference of the day values (or of daily ratios) day by day.
//
// Row fields used (counts per cell, from the emulator's metrics):
//   served, pDisp (riders the dispatcher pushed later by c >= 5 s at least once), sumCposDisp (the sum of those c, ms),
//   pVis60 (riders with a shown change >= 60 s), visRev60 (reversals: a shown change >= 60 s one way, later one the other
//   way), sumTV5 (total promise movement with a 5 s deadband, ms), late60First (arrival > 60 s after the first promise),
//   breachLedger (riders whose summed |c| exceeds their budget + 1 s);
//   Shadow arms only: G_* for guaranteed riders (G_n riders, G_dispAny1 with |c| >= 1 s, G_dispUp5 with c >= 5 s,
//   G_vis60, G_rev60, G_switched relocated after the promise, G_late60First) and A_* for allowed riders.
// Burden ("Gánh nặng" in the report) = E(chg|chg) = level(sumCposDisp) / level(pDisp), in minutes.
//
// Output, in this order:
//   TOTALS, SECONDARY and S16: the 60-day study's own analysis, ported from its pre-registered script. These lines are
//     byte-identical to lines 30-118 of the stored study report (expected/confirm-shadow-report.txt); the lines before
//     them need the run manifests and the earlier study's rows, and the lines after them need the per-rider score dump.
//     The SECONDARY list and its predicted directions are the study's pre-registration.
//   REPORT TABLES: the two tables of the system report §6 at the report's precision. They print the true values: the
//     report shows G2 burden U 9.1 and SHF71 12.0, rounded twice from TOTALS' 9.05 and 11.95; the values are 9.0 and 11.9.
//   REPORT PAIRS and GUARANTEED RIDERS: the contrasts and the guaranteed-rider counts that the §6 text cites.
// The rows file is shipped as data/wf15-confirm-rows.ndjson.gz.
//
// Usage: node sixty_day_tables.js --rows <rows.ndjson[.gz]> --out <file.txt>
//   Writes <file.txt> and prints the same text.
'use strict';
const fs = require('fs'), zlib = require('zlib');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
if (!arg('rows') || !arg('out')) {
  throw new Error('usage: node sixty_day_tables.js --rows <rows.ndjson[.gz]> --out <file.txt>');
}
const readText = file => { const raw = fs.readFileSync(file); return (file.endsWith('.gz') ? zlib.gunzipSync(raw) : raw).toString('utf8'); };
const rows = readText(arg('rows')).split('\n').filter(Boolean).map(JSON.parse);

// ---------------------------------------------------------------- arms and day values
const X = { INS: 72, G2: 71 };                                      // the Shadow share x of each config
const BASES = ['U', 'EO35', 'EOHZ35', 'RCE35', 'SHF', 'SHHZ', 'SHRC'];   // the order of the report tables
const K = (config, base) => `${config}|${base.startsWith('SH') ? base + X[config] : base}`;   // INS,SHF -> INS|SHF72
const KEYS = ['INS', 'G2'].flatMap(c => BASES.map(b => K(c, b)));
const isShadow = key => /\|SH/.test(key);
// config|arm|day -> the rows of that day, in file order
const cells = new Map(), seen = new Set();
for (const r of rows) {
  const key = `${r.config}|${r.arm}`;
  if (!KEYS.includes(key)) throw new Error('unexpected config|arm ' + key);
  if (seen.has(key + '|' + r.cell)) throw new Error('duplicate row ' + key + '|' + r.cell);
  seen.add(key + '|' + r.cell);
  if (!cells.has(key + '|' + r.day)) cells.set(key + '|' + r.day, []);
  cells.get(key + '|' + r.day).push(r);
}
const days = [...new Set(rows.map(r => r.day))].sort();
if (days.length !== 60) throw new Error('expected 60 days, got ' + days.length);
// the counts used below; a count missing from a row (G_* and A_* of the uniform arms) becomes NaN
const F = ['served', 'pDisp', 'sumCposDisp', 'pVis60', 'visRev60', 'sumTV5', 'late60First', 'breachLedger',
  'G_n', 'G_dispAny1', 'G_dispUp5', 'G_vis60', 'G_rev60', 'G_switched', 'G_late60First', 'A_n', 'A_dispUp5', 'A_sumCpos5'];
// key|day -> { w08, w17, day }: each window the mean of its 3 resamples, the day the sum of the two windows
const AGG = new Map();
for (const key of KEYS) for (const d of days) {
  const rs = cells.get(key + '|' + d);
  if (!rs) throw new Error('missing ' + key + '|' + d);
  const v = { day: {} };
  for (const w of ['w08', 'w17']) {
    const x = rs.filter(r => r.win === w);
    if (x.length !== 3) throw new Error(`incomplete ${key}|${d}|${w}: ${x.length} resamples`);
    v[w] = {};
    for (const f of F) v[w][f] = x.reduce((a, r) => a + (r[f] ?? NaN), 0) / x.length;
  }
  for (const f of F) v.day[f] = v.w08[f] + v.w17[f];
  AGG.set(key + '|' + d, v);
}
// level of a count: the mean over the 60 days of its day value
const level = (key, f) => days.reduce((t, d) => t + AGG.get(key + '|' + d).day[f], 0) / days.length;
// metrics of one day value (or one window value) v, compared day by day in the paired contrasts
const M = {
  served: v => v.served, visRev60: v => v.visRev60, late60First: v => v.late60First, TV5min: v => v.sumTV5 / 60000,
  pDispPct: v => 100 * v.pDisp / v.served, breach: v => v.breachLedger,
  EchgMin: v => (v.pDisp ? v.sumCposDisp / v.pDisp / 60000 : NaN),
  G_dispAny1: v => v.G_dispAny1, G_dispUp5: v => v.G_dispUp5, G_vis60: v => v.G_vis60, G_switched: v => v.G_switched,
};

// ---------------------------------------------------------------- statistics (as the study's script)
// t distribution tail through the regularised incomplete beta function (Numerical Recipes 6.1-6.2)
function lgamma(x) { const c = [76.18009172947146, -86.50532032941677, 24.01409824083091, -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5]; let y = x, t = x + 5.5; t -= (x + 0.5) * Math.log(t); let s = 1.000000000190015; for (const v of c) s += v / ++y; return -t + Math.log(2.5066282746310005 * s / x); }
function betacf(a, b, x) { const MAXIT = 300, EPS = 3e-14, FPMIN = 1e-300; let qab = a + b, qap = a + 1, qam = a - 1, c = 1, d = 1 - qab * x / qap; if (Math.abs(d) < FPMIN) d = FPMIN; d = 1 / d; let h = d; for (let m = 1; m <= MAXIT; m++) { const m2 = 2 * m; let aa = m * (b - m) * x / ((qam + m2) * (a + m2)); d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN; c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN; d = 1 / d; h *= d * c; aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2)); d = 1 + aa * d; if (Math.abs(d) < FPMIN) d = FPMIN; c = 1 + aa / c; if (Math.abs(c) < FPMIN) c = FPMIN; d = 1 / d; const del = d * c; h *= del; if (Math.abs(del - 1) < EPS) break; } return h; }
function ibeta(a, b, x) { if (x <= 0) return 0; if (x >= 1) return 1; const bt = Math.exp(lgamma(a + b) - lgamma(a) - lgamma(b) + a * Math.log(x) + b * Math.log(1 - x)); return x < (a + 1) / (a + b + 2) ? bt * betacf(a, b, x) / a : 1 - bt * betacf(b, a, 1 - x) / b; }
function tUpper(t, df) { if (!(df > 0)) return 1; if (!Number.isFinite(t)) return t > 0 ? 0 : 1; const x = df / (df + t * t); const p = 0.5 * ibeta(df / 2, 0.5, x); return t > 0 ? p : 1 - p; }   // P(T > t)
// P(X >= pos) for X ~ Binomial(pos + neg, 1/2): the exact sign test
function signUpper(pos, neg) { const n = pos + neg; if (!n) return 1; let s = 0, c = 1; for (let i = 0; i <= n; i++) { if (i > 0) c = c * (n - i + 1) / i; if (i >= pos) s += c; } return Math.min(1, s / Math.pow(2, n)); }
const fmt = (x, d = 2) => (Number.isFinite(x) ? x.toFixed(d) : '-');
const pe = p => (Number.isFinite(p) ? p.toExponential(2) : '-');
// A - B day by day for metric m (whole day, or one window win); a day whose difference is not finite is left out.
// pos / neg = days with a difference above 1e-9 / below -1e-9
function paired(a, b, m, win) {
  const diffs = [];
  for (const d of days) {
    const v = M[m](AGG.get(a + '|' + d)[win || 'day']) - M[m](AGG.get(b + '|' + d)[win || 'day']);
    if (Number.isFinite(v)) diffs.push(v);
  }
  const n = diffs.length, mean = n ? diffs.reduce((s, v) => s + v, 0) / n : NaN;
  const sd = n > 1 ? Math.sqrt(diffs.reduce((s, v) => s + (v - mean) ** 2, 0) / (n - 1)) : NaN;
  return { n, mean, se: sd / Math.sqrt(n), pos: diffs.filter(v => v > 1e-9).length, neg: diffs.filter(v => v < -1e-9).length };
}

const out = []; const P = s => out.push(s);
// ---------------------------------------------------------------- TOTALS (one line per config|arm, sorted)
P('TOTALS (mean over days of the day value; P in % of served)');
for (const a of KEYS.slice().sort()) {
  const s = f => level(a, f);
  P(`${a.padEnd(12)} days ${String(days.length).padStart(2)} | served ${fmt(s('served'), 1)} | P(chg5) ${fmt(100 * s('pDisp') / s('served'), 1)}% | P(vis60) ${fmt(100 * s('pVis60') / s('served'), 1)}% | E(chg|chg) ${fmt(s('sumCposDisp') / s('pDisp') / 60000)} min | visRev60 ${fmt(s('visRev60'), 1)} | TV5 ${fmt(s('sumTV5') / 60000, 0)} min | late60First ${fmt(s('late60First'), 1)} | breach ${fmt(s('breachLedger'), 2)} (${fmt(100 * s('breachLedger') / s('served'), 2)}%)` +
    (isShadow(a) ? ` | guaranteed ${fmt(100 * s('G_n') / s('served'), 1)}%: |c|>=1s ${fmt(s('G_dispAny1'), 2)} (${fmt(100 * s('G_dispAny1') / s('G_n'), 2)}%), chg5 ${fmt(100 * s('G_dispUp5') / s('G_n'), 2)}%, vis60 ${fmt(100 * s('G_vis60') / s('G_n'), 2)}%, rev60 ${fmt(s('G_rev60'), 2)}, relocated after promise ${fmt(s('G_switched'), 2)} | allowed: chg5 ${fmt(100 * s('A_dispUp5') / s('A_n'), 1)}%, E(chg|chg) ${fmt(s('A_sumCpos5') / s('A_dispUp5') / 60000)} min` : ''));
}

// ---------------------------------------------------------------- SECONDARY (the study's pre-registered paired contrasts)
P('');
P('SECONDARY (exploratory; two-sided exact sign p and paired t p; predicted direction in brackets)');
const two = (a, b, m, pred, win, label) => {
  const r = paired(a, b, m, win);
  if (!r.n) { P(`${label.padEnd(52)} ${m}: no complete days`); return; }
  const ps = Math.min(1, 2 * Math.min(signUpper(r.pos, r.neg), signUpper(r.neg, r.pos)));
  const tp = r.n > 1 && r.se > 0 ? Math.min(1, 2 * Math.min(tUpper(r.mean / r.se, r.n - 1), tUpper(-r.mean / r.se, r.n - 1))) : 1;
  P(`${label.padEnd(52)} ${m.padEnd(13)}${win ? ' ' + win : '    '} mean ${fmt(r.mean, 3).padStart(9)} SE ${fmt(r.se, 3).padStart(7)} days +/- ${r.pos}/${r.neg} n=${r.n} sign p ${pe(ps)} t p ${pe(tp)} ${pred ? '[pred ' + pred + ']' : ''}`);
};
const MARGIN = { INS: 7.9, G2: 8.0 };   // served non-inferiority margin, riders/day (1 % of the uniform arm's served)
for (const c of ['INS', 'G2']) {
  const f = K(c, 'SHF'), z = K(c, 'SHHZ'), rc = K(c, 'SHRC'), E = K(c, 'EO35'), EH = K(c, 'EOHZ35'), RC = K(c, 'RCE35'), U = K(c, 'U');
  const G2 = c === 'G2';
  P(`-- ${c}`);
  two(f, E, 'pDispPct', '<0', null, `S1 ${c} Shadow vs Ordinary, strict: SHF - EO35`);
  two(f, E, 'served', '<0', null, `S1 ${c} SHF - EO35`);
  two(z, E, 'pDispPct', '<0', null, `S2 ${c} Shadow (HZ) vs Elmi Ordinary: SHHZ - EO35`);
  two(z, E, 'served', '~0', null, `S2 ${c} SHHZ - EO35`);
  if (G2) { two(z, EH, 'pDispPct', '<0', null, `S2 G2 SHHZ - EOHZ35`); two(z, EH, 'served', '~0', null, `S2 G2 SHHZ - EOHZ35`); }
  two(rc, RC, 'pDispPct', '<0', null, `S3 ${c} Shadow vs uniform, Rule C: SHRC - RCE35`);
  two(rc, RC, 'served', '<0', null, `S3 ${c} SHRC - RCE35`);
  two(rc, E, 'pDispPct', '<0', null, `S4 ${c} Shadow+Rule C vs Elmi Ordinary: SHRC - EO35`);
  two(rc, E, 'served', '<0', null, `S4 ${c} SHRC - EO35`);
  two(rc, E, 'visRev60', G2 ? '<=0' : '>0', null, `S4 ${c} SHRC - EO35`);
  two(rc, E, 'breach', '<0', null, `S4 ${c} SHRC - EO35`);
  for (const w of ['w08', 'w17']) two(f, z, 'served', w === 'w08' ? '<0 (larger)' : '<0 (smaller)', w, `S5 ${c} SHF - SHHZ by window`);
  two(f, E, 'EchgMin', '>0', null, `S6 ${c} E(c+|changed) SHF - EO35 (min)`);
  two(z, EH, 'EchgMin', '>0', null, `S6 ${c} E(c+|changed) SHHZ - EOHZ35 (min)`);
  two(z, EH, 'visRev60', '>0', null, `S6 ${c} Shadow raises reversals: SHHZ - EOHZ35`);
  two(z, EH, 'TV5min', '>0', null, `S6 ${c} SHHZ - EOHZ35`);
  for (const w of ['w08', 'w17']) two(rc, f, 'late60First', w === 'w08' ? '>0' : '>=0', w, `S7 ${c} SHRC - SHF late60First by window`);
  two(rc, f, 'G_vis60', G2 ? '<0' : '>0', null, `S8 ${c} guaranteed riders, visible change >= 60 s: SHRC - SHF`);
  two(rc, f, 'G_dispUp5', '<0', null, `S9 ${c} guaranteed riders with c >= 5 s: SHRC - SHF`);
  if (!G2) two(rc, f, 'G_dispAny1', '<0', null, `S9 INS guaranteed riders |c|>=1s: SHRC - SHF`);
  two(rc, z, 'G_dispAny1', '<0', null, `S10 ${c} HZ comparator: guaranteed |c|>=1s SHRC - SHHZ`);
  two(rc, z, 'served', '<0', null, `S10 ${c} SHRC - SHHZ`);
  {   // served non-inferiority of SHRC to SHHZ: one-sided paired t, H0: A - B <= -margin
    const r = paired(rc, z, 'served'), p = tUpper((r.mean + MARGIN[c]) / r.se, r.n - 1);
    P(`S10 ${c} served SHRC${X[c]} non-inferior to SHHZ${X[c]} (margin ${MARGIN[c]}/day, one-sided t, unadjusted): mean ${fmt(r.mean, 3)} /day, p ${pe(p)} [pred: ${G2 ? 'NOT shown (discovery -9.4/day)' : 'shown (discovery -5.0/day)'}]`);
  }
  two(rc, z, 'visRev60', G2 ? '<0' : '~0', null, `S10 ${c} SHRC - SHHZ`);
  two(rc, z, 'pDispPct', '<=0', null, `S10 ${c} SHRC - SHHZ`);
  two(rc, f, 'pDispPct', '<=0', null, `S11 ${c} SHRC - SHF`);
  two(rc, f, 'TV5min', G2 ? '~0' : '>0', null, `S11 ${c} SHRC - SHF`);
  two(rc, f, 'breach', '<0', null, `S11 ${c} SHRC - SHF`);
  if (G2) {
    two(rc, f, 'G_switched', '<0', null, `S12 G2 guaranteed riders relocated after promise SHRC - SHF`);
    const r1 = paired(f, rc, 'visRev60'), r2 = paired(E, RC, 'visRev60');
    P(`S13 G2 interaction on visRev60: (SHF - SHRC) - (EO35 - RCE35) = ${fmt(r1.mean - r2.mean, 3)} /day (paired days ${r1.n}/${r2.n}) [pred < 0]`);
  }
  two(U, E, 'served', '>0', null, `S14 ${c} cost of protection: U - EO35`);
}
// S16: the guaranteed share of served riders, over all cells
P('');
for (const c of ['INS', 'G2']) for (const b of ['SHF', 'SHHZ', 'SHRC']) {
  const rs = rows.filter(r => `${r.config}|${r.arm}` === K(c, b));
  const sv = rs.reduce((a, r) => a + r.served, 0), g = rs.reduce((a, r) => a + r.G_n, 0);
  P(`S16 ${c} ${b}${X[c]}: realised guaranteed share ${fmt(100 * g / sv, 2)} % of served riders (${g} of ${sv}, ${rs.length} cells)`);
}

// ---------------------------------------------------------------- REPORT TABLES (system report §6)
P('');
P('REPORT TABLES (system report §6: the TOTALS levels at the report\'s precision, plus the ledger breach rate its text cites)');
const COLS = [   // [heading, value from the levels s of one arm, decimals]
  ['served', s => s('served'), 1],
  ['postponed %', s => 100 * s('pDisp') / s('served'), 1],
  ['burden min', s => s('sumCposDisp') / s('pDisp') / 60000, 1],
  ['reversals', s => s('visRev60'), 1],
  ['TV5 min', s => s('sumTV5') / 60000, 0],
  ['late >60 s', s => s('late60First'), 1],
  ['ledger breach %', s => 100 * s('breachLedger') / s('served'), 3],
];
for (const c of ['INS', 'G2']) {
  P(`-- ${c}`.padEnd(10) + COLS.map(([h]) => h.padStart(h.length + 2)).join(''));
  for (const b of BASES) {
    const a = K(c, b), s = f => level(a, f);
    P(a.split('|')[1].padEnd(10) + COLS.map(([h, value, d]) => fmt(value(s), d).padStart(h.length + 2)).join(''));
  }
}

// ---------------------------------------------------------------- REPORT PAIRS (the contrasts the §6 text cites)
P('');
P('REPORT PAIRS (A and B: 60-day levels, rates pooled as in TOTALS, and A vs B in %; paired: mean of the day differences A - B, days A<B / A>B)');
const LEVEL = {   // the level that goes with each metric of M
  served: a => level(a, 'served'), visRev60: a => level(a, 'visRev60'), late60First: a => level(a, 'late60First'),
  TV5min: a => level(a, 'sumTV5') / 60000, pDispPct: a => 100 * level(a, 'pDisp') / level(a, 'served'),
  EchgMin: a => level(a, 'sumCposDisp') / level(a, 'pDisp') / 60000,
};
const pair = (a, b, m) => {
  const la = LEVEL[m](a), lb = LEVEL[m](b), r = paired(a, b, m);
  P(`${(a + ' - ' + b.split('|')[1]).padEnd(24)} ${m.padEnd(12)} ${fmt(la, 3).padStart(9)} vs ${fmt(lb, 3).padStart(9)} (${fmt(100 * (la / lb - 1))} %) | paired ${fmt(r.mean, 3).padStart(8)}, days ${r.neg}/${r.pos}`);
};
for (const fence of ['EO35', 'EOHZ35']) for (const m of ['visRev60', 'TV5min', 'served']) pair(K('G2', 'RCE35'), K('G2', fence), m);
for (const c of ['INS', 'G2']) {
  for (const [a, b] of [['SHF', 'EO35'], ['SHHZ', 'EOHZ35'], ['SHRC', 'RCE35']])
    for (const m of ['pDispPct', 'EchgMin', 'late60First', 'served']) pair(K(c, a), K(c, b), m);
  pair(K(c, 'SHRC'), K(c, 'EO35'), 'pDispPct');
  for (const m of ['served', 'visRev60']) pair(K(c, 'SHRC'), K(c, 'SHHZ'), m);
}

// ---------------------------------------------------------------- GUARANTEED RIDERS (sums over the 360 cells)
P('');
P('GUARANTEED RIDERS (Shadow arms; counts summed over all cells)');
for (const c of ['INS', 'G2']) for (const b of ['SHF', 'SHHZ', 'SHRC']) {
  const rs = rows.filter(r => `${r.config}|${r.arm}` === K(c, b)), sum = f => rs.reduce((a, r) => a + r[f], 0);
  const g = sum('G_n'), any1 = sum('G_dispAny1'), late = sum('G_late60First');
  P(`${K(c, b).padEnd(11)} served ${sum('served')}, guaranteed ${g} (${fmt(100 * g / sum('served'))} %); guaranteed with |c| >= 1 s ${any1} (${fmt(100 * any1 / g, 3)} %); guaranteed late > 60 s ${late} (${fmt(100 * late / g, 3)} %)`);
}

const text = out.join('\n');
fs.writeFileSync(arg('out'), text + '\n');
console.log(text);
