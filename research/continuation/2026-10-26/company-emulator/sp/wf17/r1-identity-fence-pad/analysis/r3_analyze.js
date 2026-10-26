// wf17 R3 analysis (EXPLORATORY, POST-AUDIT; RUNPLAN R3 + R3-X + 2.1-2.4). Written and hashed before the R3 production run.
// Inputs: results/r3.ndjson (rows), dumps/r3.dump.ndjson (served riders t0, p0, A, pLast), work/panels.json, forecast index.
// Unit = day. Row counts: X_d = sum over windows of the mean over resamples r0..r2. Shares: pooled counts / pooled served riders of the
// day (all windows and resamples). A day missing any cell of a contrast is excluded from that contrast (logged).
// Promise variants on the N runs (per panel x window x config x arm): naive p0; display D0 = forecast.display(t0, p0, prof);
// pad(k_w) p0 + k_w (p0 - t0), k_w = sum(D0 - p0) / sum(p0 - t0); pad(k_cell) the same with k per cell; padCL p0 + kCL_w (p0 - t0),
// kCL_w = mean_CL(p0 - t0) / mean_N(p0 - t0) - 1. Y = share late > 60 s vs the shown first promise, E = share early > 60 s.
// Contrasts: (a) display-naive (b) pad(k_w)-naive (c) display-pad(k_w) [primary open loop] (c2) display-pad(k_cell)
// (d) CL-naive (e) CL-padCL [primary closed loop] (f) CLF-CL.
// Decision labels (pre-stated; primary = INS RCE35, pooled windows; other config x arm labels are descriptive):
//   C6 open loop  REVERSED if (mean(c) >= 0 or < 60 % of days negative) in BOTH panels; STRENGTHENED if mean(c) <= -4 pp with
//                 >= 80 % of days negative in both panels; HOLDS if mean(c) < 0 and >= 60 % of days negative in both panels and
//                 |mean(c)| >= 1.0 pp in at least one panel; WEAKENED otherwise (one panel fails, or |mean(c)| < 1.0 pp in both).
//   C6 closed loop per panel: HOLDS if |mean(e)| <= 1.5 pp; WEAKENED if mean(e) <= -2 pp with >= 75 % of days negative;
//                 STRENGTHENED (forecast worse than padding) if mean(e) >= +1.5 pp with >= 75 % of days positive; else INCONCLUSIVE.
//                 Overall = the label of both panels when they agree, else INCONCLUSIVE (per-panel labels reported).
//   R3-X (F16 fresh; F14 reported as seen): C1 WEAKENED if rel. reduction < 20 % or < 11/16 days in favour (Delta < 0), REVERSED if
//                 >= 8/16 days unfavourable (Delta > 0), else HOLDS; C2 WEAKENED if mean Delta served < -8/day with >= 11/16 days
//                 negative; C3 WEAKENED if |mean Delta served| > 8/day with >= 11/16 days on one side; C4 REVERSED if
//                 Delta breachDeadline60 <= 0 on >= 8/16 days (per config). Day thresholds scale with the panel size (11/16, 8/16).
// Sensitivity: the same primary contrasts without the NYC-DOE non-school target days in forecast.js EXCL.
'use strict';
const fs = require('fs'), path = require('path');
const L = require('./lib17.js');
const KEY = path.resolve(__dirname, '..');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const FC = require(path.join(KEY, 'code/scripts/forecast.js'));
const INDEX = JSON.parse(fs.readFileSync(path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json'), 'utf8'));
const PAN = JSON.parse(fs.readFileSync(path.join(KEY, 'work/panels.json'), 'utf8'));
const rowsFile = process.argv[2] || path.join(KEY, 'results/r3.ndjson');
const dumpFile = process.argv[3] || path.join(KEY, 'dumps/r3.dump.ndjson');
const outTxt = process.argv[4] || path.join(KEY, 'results/r3_tables.txt');
const outJson = outTxt.replace(/\.txt$/, '.json');
const WINS = ['w08', 'w17'], RS = ['r0', 'r1', 'r2'], S0 = { w08: 32, w17: 68 };
const LINES = [];
const P = s => LINES.push(s);
const LOG = [];

const rows = L.readNd(rowsFile).filter(r => r.label === 'wf17-exploratory-post-audit' && r.check === 'R3');
const R = new Map(rows.map(r => [[r.panel, r.cell, r.config, r.mode, r.arm].join('|'), r]));
const D = new Map();
for (const l of fs.existsSync(dumpFile) ? fs.readFileSync(dumpFile, 'utf8').split('\n') : []) { if (!l) continue; const d = JSON.parse(l); D.set([d.panel, d.cell, d.config, d.mode, d.arm].join('|'), d.riders); }
const profC = new Map();
const prof = (day, win) => { const k = day + win; if (!profC.has(k)) profC.set(k, FC.profile(day, S0[win], 'prev4', INDEX, 15)); return profC.get(k); };
const cellOf = (day, win, rs) => `${day}-${win}-${rs}`;

// ---------------------------------------------------------------- k factors
const K = {};   // K[panel|win|config|arm] = { kW, kCL, extCL, extCLF, ... }
const kCell = new Map();
for (const panel of ['F14', 'F16']) for (const win of WINS) for (const config of ['INS', 'G2']) for (const arm of ['U', 'EOHZ35', 'RCE35']) {
  let num = 0, den = 0, nN = 0;
  const sumLen = { N: 0, CL: 0, CLF: 0 }, cnt = { N: 0, CL: 0, CLF: 0 };
  for (const day of PAN[panel]) for (const rs of RS) {
    const cell = cellOf(day, win, rs);
    const rN = D.get([panel, cell, config, 'N', arm].join('|'));
    if (rN) {
      let cn = 0, cd = 0;
      const pr = prof(day, win);
      for (const [, t0, p0] of rN) { const D0 = FC.display(t0, p0, pr); cn += D0 - p0; cd += p0 - t0; sumLen.N += p0 - t0; cnt.N++; }
      num += cn; den += cd; nN += rN.length;
      kCell.set([panel, cell, config, arm].join('|'), cn / cd);
    }
    for (const mode of ['CL', 'CLF']) { const rr = D.get([panel, cell, config, mode, arm].join('|')); if (rr) for (const [, t0, p0] of rr) { sumLen[mode] += p0 - t0; cnt[mode]++; } }
  }
  const mN = sumLen.N / cnt.N, mCL = cnt.CL ? sumLen.CL / cnt.CL : NaN, mCLF = cnt.CLF ? sumLen.CLF / cnt.CLF : NaN;
  const extCL = mCL - mN, extCLF = mCLF - mN;
  K[[panel, win, config, arm].join('|')] = { kW: num / den, nN, meanLenN_s: mN / 1000, meanLenCL_s: mCL / 1000, meanLenCLF_s: mCLF / 1000, kCL: mCL / mN - 1,
    extCL_s: extCL / 1000, extCLF_s: extCLF / 1000, clfEqualTime: arm === 'U' ? null : Math.abs(extCLF - extCL) <= 0.25 * Math.abs(extCL) };
}

// ---------------------------------------------------------------- per-cell counts and per-day aggregation
function countsOf(riders, promise) {
  let n = 0, Y = 0, E = 0;
  for (const r of riders) { const pr = promise(r), A = r[3]; n++; if (A - pr > 60000) Y++; if (A - pr < -60000) E++; }
  return { n, Y, E };
}
// variant -> function(panel, day, win, rs, config, arm) -> counts | null
const VAR = {
  naive: (pa, d, w, rs, c, a) => { const rr = D.get([pa, cellOf(d, w, rs), c, 'N', a].join('|')); return rr ? countsOf(rr, r => r[2]) : null; },
  display: (pa, d, w, rs, c, a) => { const rr = D.get([pa, cellOf(d, w, rs), c, 'N', a].join('|')); if (!rr) return null; const pr = prof(d, w); return countsOf(rr, r => FC.display(r[1], r[2], pr)); },
  padW: (pa, d, w, rs, c, a) => { const rr = D.get([pa, cellOf(d, w, rs), c, 'N', a].join('|')); if (!rr) return null; const k = K[[pa, w, c, a].join('|')].kW; return countsOf(rr, r => r[2] + k * (r[2] - r[1])); },
  padC: (pa, d, w, rs, c, a) => { const rr = D.get([pa, cellOf(d, w, rs), c, 'N', a].join('|')); if (!rr) return null; const k = kCell.get([pa, cellOf(d, w, rs), c, a].join('|')); return countsOf(rr, r => r[2] + k * (r[2] - r[1])); },
  padCL: (pa, d, w, rs, c, a) => { const rr = D.get([pa, cellOf(d, w, rs), c, 'N', a].join('|')); if (!rr) return null; const k = K[[pa, w, c, a].join('|')].kCL; return countsOf(rr, r => r[2] + k * (r[2] - r[1])); },
  CL: (pa, d, w, rs, c, a) => { const rr = D.get([pa, cellOf(d, w, rs), c, 'CL', a].join('|')); return rr ? countsOf(rr, r => r[2]) : null; },
  CLF: (pa, d, w, rs, c, a) => { const rr = D.get([pa, cellOf(d, w, rs), c, 'CLF', a].join('|')); return rr ? countsOf(rr, r => r[2]) : null; },
};
// day share maps for one variant: Map(day -> share) for wins (default both); days with any missing cell -> excluded
function shareDays(panel, variant, config, arm, which, wins = WINS, dropDays = null) {
  const out = new Map(), missing = [];
  for (const day of PAN[panel]) {
    if (dropDays && dropDays.has(day)) continue;
    let n = 0, x = 0, ok = true;
    for (const w of wins) for (const rs of RS) { const c = VAR[variant](panel, day, w, rs, config, arm); if (!c) { ok = false; break; } n += c.n; x += c[which]; }
    if (ok && n > 0) out.set(day, x / n); else missing.push(day);
  }
  if (missing.length) LOG.push(`excluded (missing cells) ${panel} ${config} ${arm} ${variant}: ${missing.join(' ')}`);
  return out;
}
// day maps for row metrics: count X_d = sum_w mean_rs X; ratio metrics num/den pooled
function rowDays(panel, mode, config, arm, fn, wins = WINS, dropDays = null) {
  const out = new Map(), missing = [];
  for (const day of PAN[panel]) {
    if (dropDays && dropDays.has(day)) continue;
    let s = 0, ok = true;
    for (const w of wins) { let ws = 0; for (const rs of RS) { const r = R.get([panel, cellOf(day, w, rs), config, mode, arm].join('|')); if (!r) { ok = false; break; } ws += fn(r); } if (!ok) break; s += ws / RS.length; }
    if (ok) out.set(day, s); else missing.push(day);
  }
  if (missing.length) LOG.push(`excluded (missing rows) ${panel} ${mode} ${config} ${arm}: ${missing.join(' ')}`);
  return out;
}
const ratioDays = (panel, mode, config, arm, num, den, wins, dropDays) => {
  const a = rowDays(panel, mode, config, arm, num, wins, dropDays), b = rowDays(panel, mode, config, arm, den, wins, dropDays);
  return new Map([...a.keys()].filter(d => b.has(d)).map(d => [d, a.get(d) / b.get(d)]));
};

// ---------------------------------------------------------------- forecast / pad contrasts
const SUMMARY = { label: 'wf17-exploratory-post-audit', check: 'R3', K, contrasts: {}, levels: {}, labels: {}, r3x: {}, rates: {}, sensitivity: {}, log: LOG };
const EXCL = new Set([...FC.EXCL]);
function padBlock(panel, config, arm, dropDays = null, tag = '') {
  const res = {};
  const pairs = [['a', 'display', 'naive'], ['b', 'padW', 'naive'], ['c', 'display', 'padW'], ['c2', 'display', 'padC']];
  if (arm !== 'U') pairs.push(['d', 'CL', 'naive'], ['e', 'CL', 'padCL'], ['f', 'CLF', 'CL']);
  for (const which of ['Y', 'E']) for (const [lab, va, vb] of pairs) {
    for (const wins of [WINS, ['w08'], ['w17']]) {
      const wl = wins.length === 2 ? 'pooled' : wins[0];
      const A = shareDays(panel, va, config, arm, which, wins, dropDays), B = shareDays(panel, vb, config, arm, which, wins, dropDays);
      res[`${lab}|${which}|${wl}`] = L.contrast(A, B, { better: 'lower', count: false, scale: 100 });
    }
  }
  return res;
}
function levelBlock(panel, config, arm) {
  const out = {};
  const vars = ['naive', 'display', 'padW', 'padC'].concat(arm === 'U' ? [] : ['padCL', 'CL', 'CLF']);
  for (const v of vars) for (const wins of [WINS, ['w08'], ['w17']]) {
    let n = 0, Y = 0, E = 0;
    for (const day of PAN[panel]) for (const w of wins) for (const rs of RS) { const c = VAR[v](panel, day, w, rs, config, arm); if (c) { n += c.n; Y += c.Y; E += c.E; } }
    out[`${v}|${wins.length === 2 ? 'pooled' : wins[0]}`] = { n, Y: 100 * Y / n, E: 100 * E / n };
  }
  return out;
}
const fracNeg = c => (c.nDays ? c.neg / c.nDays : NaN), fracPos = c => (c.nDays ? c.pos / c.nDays : NaN);

for (const config of ['INS', 'G2']) for (const arm of ['U', 'EOHZ35', 'RCE35']) {
  P(`\n==================== ${config} ${arm} ====================`);
  for (const panel of ['F14', 'F16']) {
    const kk = WINS.map(w => K[[panel, w, config, arm].join('|')]);
    P(`-- ${panel} (${panel === 'F16' ? 'FRESH, never simulated before' : 'SEEN'}) k_w08 ${L.f(100 * kk[0].kW, 2)}% k_w17 ${L.f(100 * kk[1].kW, 2)}%` +
      (arm === 'U' ? '' : ` | kCL_w08 ${L.f(100 * kk[0].kCL, 2)}% kCL_w17 ${L.f(100 * kk[1].kCL, 2)}% | mean promise length N/CL/CLF w08 ${L.f(kk[0].meanLenN_s, 1)}/${L.f(kk[0].meanLenCL_s, 1)}/${L.f(kk[0].meanLenCLF_s, 1)} s, w17 ${L.f(kk[1].meanLenN_s, 1)}/${L.f(kk[1].meanLenCL_s, 1)}/${L.f(kk[1].meanLenCLF_s, 1)} s | CLF equal time w08 ${kk[0].clfEqualTime} w17 ${kk[1].clfEqualTime}`));
    const lv = levelBlock(panel, config, arm);
    SUMMARY.levels[[panel, config, arm].join('|')] = lv;
    P('   levels (pooled over the panel) Y% / E%: ' + Object.entries(lv).filter(([k]) => k.endsWith('pooled')).map(([k, v]) => `${k.split('|')[0]} ${L.f(v.Y)}/${L.f(v.E)}`).join('  '));
    P('   levels w08 Y%: ' + Object.entries(lv).filter(([k]) => k.endsWith('w08')).map(([k, v]) => `${k.split('|')[0]} ${L.f(v.Y)}`).join('  ') + ' | w17 Y%: ' + Object.entries(lv).filter(([k]) => k.endsWith('w17')).map(([k, v]) => `${k.split('|')[0]} ${L.f(v.Y)}`).join('  '));
    const blk = padBlock(panel, config, arm);
    SUMMARY.contrasts[[panel, config, arm].join('|')] = Object.fromEntries(Object.entries(blk).map(([k, c]) => [k, { ...c, deltas: c.deltas }]));
    const NAMES = { a: 'display - naive', b: 'pad(k_w) - naive', c: 'display - pad(k_w) [PRIMARY open loop]', c2: 'display - pad(k_cell)', d: 'CL - naive', e: 'CL - padCL [PRIMARY closed loop]', f: 'CLF - CL' };
    for (const k of Object.keys(blk).filter(k => k.endsWith('|pooled'))) {
      const [lab, which] = k.split('|');
      P(`   (${lab}) ${which} ${NAMES[lab].padEnd(40)} ${L.fmtC(blk[k], { unit: ' pp' })}`);
    }
    for (const lab of ['c', 'e'].filter(l => arm !== 'U' || l === 'c')) for (const w of WINS) P(`   (${lab}) Y ${w}: ${L.fmtC(blk[`${lab}|Y|${w}`], { unit: ' pp' })}`);
    if (arm !== 'U') {
      // row metrics for CL - N and CLF - CL
      const M = { served: r => r.served, visRev60: r => r.visRev60, late60Last: r => r.late60Last };
      for (const [lab, ma, mb] of [['d', 'CL', 'N'], ['f', 'CLF', 'CL']]) {
        for (const [mk, fn] of Object.entries(M)) {
          const c = L.contrast(rowDays(panel, ma, config, arm, fn), rowDays(panel, mb, config, arm, fn), { better: mk === 'served' ? 'higher' : 'lower' });
          SUMMARY.contrasts[[panel, config, arm].join('|')][`${lab}|${mk}|row`] = c;
          P(`   (${lab}) ${ma}-${mb} ${mk.padEnd(10)} per day ${L.fmtC(c)}`);
        }
        const c = L.contrast(ratioDays(panel, ma, config, arm, r => r.sumTV5 / 1000, r => r.served), ratioDays(panel, mb, config, arm, r => r.sumTV5 / 1000, r => r.served), { count: false });
        SUMMARY.contrasts[[panel, config, arm].join('|')][`${lab}|TV5perServed_s|row`] = c;
        P(`   (${lab}) ${ma}-${mb} TV5/served s ${L.fmtC(c)}`);
      }
    }
  }
}

// ---------------------------------------------------------------- C6 labels (primary INS RCE35; all config x arm descriptive)
function openLabel(cF14, cF16) {
  if (!cF14.nDays || !cF16.nDays) return 'INCONCLUSIVE (missing panel data)';
  const fails = c => c.mean >= 0 || fracNeg(c) < 0.6;
  if (fails(cF14) && fails(cF16)) return 'REVERSED';
  if ([cF14, cF16].every(c => c.mean <= -4 && fracNeg(c) >= 0.8)) return 'HOLDS (STRENGTHENED: <= -4 pp, >= 80 % days negative in both panels)';
  if (!fails(cF14) && !fails(cF16) && (Math.abs(cF14.mean) >= 1.0 || Math.abs(cF16.mean) >= 1.0)) return 'HOLDS';
  return 'WEAKENED';
}
function closedPanel(c) {
  if (!c.nDays) return 'INCONCLUSIVE (no data)';
  if (c.mean <= -2 && fracNeg(c) >= 0.75) return 'WEAKENED (closed-loop forecast beats an equal-length pad)';
  if (c.mean >= 1.5 && fracPos(c) >= 0.75) return 'STRENGTHENED (forecast worse than an equal-length pad)';
  if (Math.abs(c.mean) <= 1.5) return 'HOLDS';
  return 'INCONCLUSIVE';
}
P('\n==================== C6 decision labels ====================');
for (const config of ['INS', 'G2']) for (const arm of ['U', 'EOHZ35', 'RCE35']) {
  const prim = config === 'INS' && arm === 'RCE35';
  const cc = p => SUMMARY.contrasts[[p, config, arm].join('|')]['c|Y|pooled'];
  const ol = openLabel(cc('F14'), cc('F16'));
  let cl = null, clp = null;
  if (arm !== 'U') {
    const ce = p => SUMMARY.contrasts[[p, config, arm].join('|')]['e|Y|pooled'];
    clp = { F14: closedPanel(ce('F14')), F16: closedPanel(ce('F16')) };
    cl = clp.F14 === clp.F16 ? clp.F14 : 'INCONCLUSIVE (panels disagree)';
  }
  SUMMARY.labels[`C6open|${config}|${arm}`] = ol;
  if (cl) { SUMMARY.labels[`C6closed|${config}|${arm}`] = cl; SUMMARY.labels[`C6closedPanels|${config}|${arm}`] = clp; }
  P(`${prim ? 'PRIMARY ' : 'descr.  '}${config} ${arm}: C6 open loop (c) ${ol}` + (cl ? ` | C6 closed loop (e) ${cl} [F14 ${clp.F14}; F16 ${clp.F16}]` : ''));
}

// ---------------------------------------------------------------- R3-X (fresh-day replication on N rows) and 2.2 metric list
const MET = {
  served: { fn: r => r.served, better: 'higher' },
  visRev60: { fn: r => r.visRev60, better: 'lower' },
  late60First: { fn: r => r.late60First, better: 'lower' },
  breachDeadline60: { fn: r => r.breachDeadline60, better: 'lower' },
  breachLedger: { fn: r => r.breachLedger, better: 'lower' },
  pDisp: { fn: r => r.pDisp, better: 'lower' },
  pDispAny1: { fn: r => r.pDispAny1, better: 'lower' },
  relocations: { fn: r => r.relocations, better: null },
  switchedRiders: { fn: r => r.switchedRiders, better: null },
};
const SHARE = { 'TV5/served (s)': [r => r.sumTV5 / 1000, 1], 'Pchg=pDisp/served (pp)': [r => r.pDisp, 100], 'pDispAny1/served (pp)': [r => r.pDispAny1, 100],
  'late60First/served (pp)': [r => r.late60First, 100], 'breachDeadline60/served (pp)': [r => r.breachDeadline60, 100], 'breachLedger/served (pp) [BY-CONSTRUCTION for RC]': [r => r.breachLedger, 100] };
function r3xBlock(panel, dropDays = null) {
  const out = {};
  const pairs = [['INS', 'RCE35', 'EOHZ35'], ['INS', 'RCE35', 'EO35'], ['INS', 'RCE35', 'U'], ['G2', 'RCE35', 'EOHZ35'], ['G2', 'RCE35', 'NCE35'], ['G2', 'RCE35', 'EO35'], ['G2', 'RCE35', 'U']];
  for (const [config, a, b] of pairs) {
    for (const [mk, m] of Object.entries(MET)) {
      if ((mk === 'relocations' || mk === 'switchedRiders') && config === 'INS') continue;
      out[`${config}|${a}-${b}|${mk}`] = L.contrast(rowDays(panel, 'N', config, a, m.fn, WINS, dropDays), rowDays(panel, 'N', config, b, m.fn, WINS, dropDays), { better: m.better });
    }
    for (const [mk, [fn, sc]] of Object.entries(SHARE)) {
      out[`${config}|${a}-${b}|${mk}`] = L.contrast(ratioDays(panel, 'N', config, a, fn, r => r.served, WINS, dropDays), ratioDays(panel, 'N', config, b, fn, r => r.served, WINS, dropDays), { better: 'lower', count: false, scale: sc });
    }
  }
  return out;
}
function r3xLabels(x, nDays) {
  const t11 = Math.ceil(11 * nDays / 16), t8 = Math.ceil(8 * nDays / 16);
  const v = x['G2|RCE35-EOHZ35|visRev60'], s = x['G2|RCE35-EOHZ35|served'], si = x['INS|RCE35-EOHZ35|served'];
  const lab = {};
  if (!v.nDays || !s.nDays || !si.nDays) return { all: 'INCONCLUSIVE (no data)', thresholds: { nDays } };
  lab.C1 = v.pos >= t8 ? 'REVERSED' : (v.rel < 0.20 || v.neg < t11) ? 'WEAKENED' : 'HOLDS';
  lab.C2 = s.mean < -8 && s.neg >= t11 ? 'WEAKENED' : 'HOLDS';
  lab.C3 = Math.abs(si.mean) > 8 && Math.max(si.neg, si.pos) >= t11 ? 'WEAKENED' : 'HOLDS';
  for (const c of ['INS', 'G2']) { const b = x[`${c}|RCE35-EOHZ35|breachDeadline60`]; lab['C4_' + c] = (b.neg + b.ties) >= t8 ? 'REVERSED' : 'HOLDS'; }
  lab.thresholds = { inFavour: t11, unfavourable: t8, nDays };
  return lab;
}
function rates(x) {
  const out = {};
  for (const [config, b] of [['INS', 'EOHZ35'], ['G2', 'EOHZ35'], ['G2', 'NCE35']]) {
    const v = x[`${config}|RCE35-${b}|visRev60`], s = x[`${config}|RCE35-${b}|served`], dl = x[`${config}|RCE35-${b}|breachDeadline60`], lt = x[`${config}|RCE35-${b}|late60First`];
    out[`${config}|RCE35 vs ${b}`] = { dVisRev60_day: v.mean, dServed_day: s.mean, dDeadline60_day: dl.mean, dLate60First_day: lt.mean,
      ARR_per1000: -v.mean / s.meanB * 1000,
      ER_served: s.mean < 0 ? v.mean / s.mean : 'no service price',
      ER_deadline: dl.mean > 0 ? -v.mean / dl.mean : 'no deadline price',
      ER_late: lt.mean > 0 ? -v.mean / lt.mean : 'no lateness price' };
  }
  return out;
}
P('\n==================== R3-X: N rows, RCE35 vs comparators (F16 = FRESH replication; F14 = seen) ====================');
for (const panel of ['F16', 'F14']) {
  const x = r3xBlock(panel);
  const lab = r3xLabels(x, PAN[panel].length);
  SUMMARY.r3x[panel] = { contrasts: x, labels: lab };
  SUMMARY.rates[panel] = rates(x);
  P(`-- ${panel} labels: ${JSON.stringify(lab)}`);
  for (const [k, c] of Object.entries(x)) P(`   ${k.padEnd(62)} ${L.fmtC(c)}`);
  P(`   exchange rates (2.4): ${JSON.stringify(SUMMARY.rates[panel])}`);
}

// ---------------------------------------------------------------- sensitivity without NYC-DOE non-school target days
P('\n==================== Sensitivity: without forecast.js EXCL target days ====================');
for (const panel of ['F14', 'F16']) {
  const drop = new Set(PAN[panel].filter(d => EXCL.has(d)));
  P(`-- ${panel} dropped: ${[...drop].join(' ') || '(none)'}`);
  if (!drop.size) continue;
  const sens = {};
  for (const config of ['INS', 'G2']) for (const arm of ['EOHZ35', 'RCE35']) {
    const blk = padBlock(panel, config, arm, drop);
    sens[`${config}|${arm}|c`] = blk['c|Y|pooled']; sens[`${config}|${arm}|e`] = blk['e|Y|pooled'];
    P(`   ${config} ${arm} (c) ${L.fmtC(blk['c|Y|pooled'], { unit: ' pp' })}`);
    P(`   ${config} ${arm} (e) ${L.fmtC(blk['e|Y|pooled'], { unit: ' pp' })}`);
  }
  const x = r3xBlock(panel, drop);
  sens.r3xLabels = r3xLabels(x, PAN[panel].length - drop.size);
  for (const k of ['G2|RCE35-EOHZ35|visRev60', 'G2|RCE35-EOHZ35|served', 'INS|RCE35-EOHZ35|served', 'INS|RCE35-EOHZ35|breachDeadline60', 'G2|RCE35-EOHZ35|breachDeadline60']) { sens[k] = x[k]; P(`   ${k.padEnd(40)} ${L.fmtC(x[k])}`); }
  P(`   R3-X labels without EXCL days: ${JSON.stringify(sens.r3xLabels)}`);
  SUMMARY.sensitivity[panel] = sens;
}

P('\n==================== Log ====================');
for (const l of [...new Set(LOG)]) P(l);
P(`rows ${rows.length}, dumps ${D.size}`);
const header = `wf17 R3 (wf17-exploratory-post-audit) - forecast vs equal-time pad + fresh-day replication. Generated ${new Date().toISOString()} by analysis/r3_analyze.js (sha256 ${L.sha256File(__filename)})\nrows ${rowsFile}\ndumps ${dumpFile}\n`;
fs.writeFileSync(outTxt, header + LINES.join('\n') + '\n');
const strip = o => JSON.parse(JSON.stringify(o, (k, v) => (k === 'days' ? undefined : v)));
fs.writeFileSync(outJson, JSON.stringify(strip(SUMMARY), null, 1));
console.log(LINES.filter(l => /C6|labels|PRIMARY/.test(l)).join('\n'));
