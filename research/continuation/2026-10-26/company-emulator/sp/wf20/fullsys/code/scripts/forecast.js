// wf7/c-forecast: NYC port of the thesis Family A forecast (FORECAST-PLAN.md s.2; display_layer.py, promise_stretch.py,
// preflight_world_v1_3.forecast_profile). Library, no side effects.
//   index      f(d, b) = exp(e_med(d, b) - month median of e_med), TLC-MP15 pickup-bin Yellow polish, one polish per month
//   sources    prev4 (PRIMARY) / prev8: same weekday, previous 4 (8) calendar weeks, minus EXCLUDED days, >= 2 sources
//   profile    m(b) = mean over sources of the ABSOLUTE factor at clock bin s + b, b = 0..HOLD (held after HOLD)
//   stretch    t + S solves integral_t^{t+S} 1/weight = work; weight(b) = 1 in the current bin b0, m(b)/m(b0) after
//   display    D = max(p, S(t, p - t)) (one-sided)
'use strict';
const fs = require('fs');
const BIN = 900000;

function readBins(file) {
  const L = fs.readFileSync(file, 'utf8').trim().split(/\r?\n/);
  const h = L[0].split(',');
  const iD = h.indexOf('day'), iB = h.indexOf('bin'), iE = h.indexOf('e_med'), iN = h.indexOf('n');
  return L.slice(1).map(l => { const v = l.split(','); return { d: v[iD], b: +v[iB], e: +v[iE], n: +v[iN] }; });
}
// files: list of bins.csv (one per month). Each month normalised by its own median bin effect (as wf5 analyze_tlc.js).
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

// ---- calendar (PREREG s.2): non-school days of the NYC DOE 2019-20 calendar and federal holidays
const EXCL = new Set(['2019-09-30', '2019-10-01', '2019-10-09', '2019-10-14', '2019-11-05', '2019-11-11', '2019-11-28', '2019-11-29',
  '2019-12-24', '2019-12-25', '2019-12-26', '2019-12-27', '2019-12-30', '2019-12-31']);
const SCHOOL_START = '2019-09-05';
const dateAdd = (d, k) => { const x = new Date(d + 'T12:00:00Z'); x.setUTCDate(x.getUTCDate() + k); return x.toISOString().slice(0, 10); };
const dow = d => new Date(d + 'T12:00:00Z').getUTCDay();
const isWeekday = d => { const w = dow(d); return w >= 1 && w <= 5; };
const isSourceOk = d => isWeekday(d) && d >= SCHOOL_START && !EXCL.has(d);
const MIN_SOURCES = 2;

function sources(day, rule, F) {
  const weeks = rule === 'prev4' ? 4 : rule === 'prev8' ? 8 : null;
  if (weeks === null) throw new Error('rule ' + rule);
  const out = [];
  for (let k = 1; k <= weeks; k++) { const s = dateAdd(day, -7 * k); if (isSourceOk(s) && F[s]) out.push(s); }
  return out.length >= MIN_SOURCES ? out : [];
}
// absolute forecast profile for bins b = 0..hold relative to the window-start clock bin s0
function profile(day, s0, rule, F, hold = 15) {
  if (rule === 'oracle') { const f = F[day]; return Array.from({ length: hold + 1 }, (_, b) => f[s0 + b]); }
  const src = sources(day, rule, F);
  if (!src.length) return null;
  const m = [];
  for (let b = 0; b <= hold; b++) {
    let s = 0; for (const d of src) { const v = F[d][s0 + b]; if (!isFinite(v)) throw new Error(`missing factor ${d} bin ${s0 + b}`); s += v; }
    m.push(s / src.length);
  }
  return m;
}

function stretch(t0, work, weight) {
  if (work <= 0) return t0 + work;
  let t = t0, remaining = work;
  for (let i = 0; i < 10000; i++) {
    const b = Math.floor(t / BIN);
    const w = weight(b);
    const segment = (b + 1) * BIN - t;
    const covered = segment / w;
    if (covered >= remaining) return t + remaining * w;
    remaining -= covered;
    t = (b + 1) * BIN;
  }
  throw new Error('stretch did not converge');
}
function forecastWeight(prof, t0) {
  const H = prof.length - 1;
  const b0 = Math.floor(t0 / BIN);
  const l0 = prof[Math.min(b0, H)];
  return b => (b === b0 ? 1 : prof[Math.min(b, H)] / l0);
}
function display(t, p, prof) {
  if (!prof || p <= t) return p;
  return Math.max(p, stretch(t, p - t, forecastWeight(prof, t)));
}

module.exports = { BIN, loadIndex, readBins, EXCL, SCHOOL_START, dateAdd, dow, isWeekday, isSourceOk, sources, profile, stretch, forecastWeight, display };
