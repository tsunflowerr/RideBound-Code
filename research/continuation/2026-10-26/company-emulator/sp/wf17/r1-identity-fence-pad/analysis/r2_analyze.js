// wf17 R2 analysis (EXPLORATORY, POST-AUDIT; RUNPLAN R2): strict-fence rounding artefact, tolerance 0 / 1 / 5 / 60 s.
// Written and hashed before the R2 production run. Inputs: results/r2a.ndjson (real traffic, D30 x {w08, w17} x r0) and
// results/r2b.ndjson (flat world, D10 x {w08, w17} x r0). Unit = day = w08 + w17 (r0 only).
// Quantities: Gap(tau)_d = served(SHRC) - served(SHF_tau); rho(tau) = mean Gap(tau) / mean Gap(0); the same with SHHZ in place of SHRC;
// EO effect served(EO35_tau) - served(EO35_0); forced vehicle-decisions with the exceedance histogram; Shadow guaranteed-rider fields.
// Decision rules (RUNPLAN R2; primary rho = the SHRC version, the SHHZ version is reported):
//   C5 "mostly artefact": REVERSED if rho(5 s) >= 0.9 in both configs AND R2b shows |SHF(tau 0) - SHHZ| <= 2/day in both configs;
//      else WEAKENED if rho(5 s) > 0.6 in both configs; else HOLDS if rho(5 s) <= 0.4 (INS) and <= 0.7 (G2); else INCONCLUSIVE.
//   "EO35 barely affected": WEAKENED if |mean(EO35_5s - EO35_0)| > 3/day with >= 2/3 of days on one side; else HOLDS (per config).
//   "SHHZ is a fair primary comparator": SUPPORTED if |mean(SHF_5s - SHHZ)| <= 3/day; "real in our model, not in Elmi's" if
//      mean(SHF_5s - SHHZ) < -8/day; otherwise PARTLY (per config).
'use strict';
const fs = require('fs'), path = require('path');
const L = require('./lib17.js');
const KEY = path.resolve(__dirname, '..');
const fileA = process.argv[2] || path.join(KEY, 'results/r2a.ndjson');
const fileB = process.argv[3] || path.join(KEY, 'results/r2b.ndjson');
const outTxt = process.argv[4] || path.join(KEY, 'results/r2_tables.txt');
const outJson = outTxt.replace(/\.txt$/, '.json');
const XS = { INS: 72, G2: 71 };
const TAUS = [0, 1000, 5000, 60000];
const LINES = [], LOG = [];
const P = s => LINES.push(s);
const isStrict = a => /^(EO|SHF|DLS)\d+$/.test(a);
const keyOf = (r, tau) => [r.config, r.arm, isStrict(r.arm) ? tau : '-'].join('|');

function load(file) {
  const rows = L.readNd(file).filter(r => r.label === 'wf17-exploratory-post-audit');
  const M = new Map();   // config|arm|tau -> Map(day -> {win -> row})
  for (const r of rows) {
    const k = keyOf(r, r.fenceTolMs);
    if (!M.has(k)) M.set(k, new Map());
    const dm = M.get(k);
    if (!dm.has(r.day)) dm.set(r.day, {});
    if (dm.get(r.day)[r.win]) LOG.push(`duplicate row ${k} ${r.cell}`);
    dm.get(r.day)[r.win] = r;
  }
  return { rows, M };
}
// day map of fn(row) summed over both windows (a day missing a window is excluded and logged)
function dayMap(D, config, arm, tau, fn) {
  const dm = D.M.get([config, arm, isStrict(arm) ? tau : '-'].join('|'));
  const out = new Map();
  if (!dm) { LOG.push(`no rows ${config} ${arm} tau ${tau}`); return out; }
  for (const [day, w] of dm) { if (w.w08 && w.w17) out.set(day, fn(w.w08) + fn(w.w17)); else LOG.push(`excluded ${config} ${arm} tau ${tau} ${day}: missing window`); }
  return out;
}
const ratioMap = (D, c, a, t, num, den, sc = 1) => { const x = dayMap(D, c, a, t, num), y = dayMap(D, c, a, t, den); return new Map([...x.keys()].filter(d => y.has(d)).map(d => [d, sc * x.get(d) / y.get(d)])); };
const meanOf = m => L.mean([...m.values()]);
const served = r => r.served;

const A = load(fileA), B = load(fileB);
const SUM = { label: 'wf17-exploratory-post-audit', check: 'R2', r2a: {}, r2b: {}, labels: {}, log: LOG };

// ---------------------------------------------------------------- R2a
P('==================== R2a: real traffic (D30 x {w08, w17} x r0; unit = day = w08 + w17) ====================');
for (const config of ['INS', 'G2']) {
  const x = XS[config], SHF = 'SHF' + x, SHHZ = 'SHHZ' + x, SHRC = 'SHRC' + x;
  const o = { rho: {}, rhoHZ: {}, gap: {}, gapHZ: {}, eo: {}, shfVsHZ: {}, levels: {}, forced: {}, guaranteed: {} };
  P(`\n-- ${config} (x* = ${x})`);
  const sRC = dayMap(A, config, SHRC, 0, served), sHZ = dayMap(A, config, SHHZ, 0, served);
  for (const tau of TAUS) {
    const sF = dayMap(A, config, SHF, tau, served);
    o.gap[tau] = L.contrast(sRC, sF, { better: 'higher' });
    o.gapHZ[tau] = L.contrast(sHZ, sF, { better: 'higher' });
    o.shfVsHZ[tau] = L.contrast(sF, sHZ, { better: 'higher' });
    const eoT = dayMap(A, config, 'EO35', tau, served), eo0 = dayMap(A, config, 'EO35', 0, served);
    o.eo[tau] = L.contrast(eoT, eo0, { better: 'higher' });
  }
  for (const tau of TAUS) { o.rho[tau] = o.gap[tau].mean / o.gap[0].mean; o.rhoHZ[tau] = o.gapHZ[tau].mean / o.gapHZ[0].mean; }
  for (const tau of TAUS) P(`   tau ${String(tau / 1000).padStart(2)} s: Gap SHRC-SHF ${L.fmtC(o.gap[tau])} | rho ${L.f(o.rho[tau], 3)}`);
  for (const tau of TAUS) P(`   tau ${String(tau / 1000).padStart(2)} s: Gap SHHZ-SHF ${L.fmtC(o.gapHZ[tau])} | rhoHZ ${L.f(o.rhoHZ[tau], 3)}`);
  for (const tau of TAUS.slice(1)) P(`   tau ${String(tau / 1000).padStart(2)} s: EO35_tau - EO35_0 served ${L.fmtC(o.eo[tau])}`);
  // forced vehicle-decisions and exceedance histogram (per day, both windows)
  for (const [arm, taus] of [[SHF, TAUS], ['EO35', TAUS]]) for (const tau of taus) {
    const f = meanOf(dayMap(A, config, arm, tau, r => r.forcedVehDecisions));
    const h = {}; for (const b of ['le1s', 'le5s', 'le60s', 'gt60s']) h[b] = meanOf(dayMap(A, config, arm, tau, r => (r.audit ? r.audit[b] : NaN)));
    o.forced[`${arm}|${tau}`] = { forcedPerDay: f, hist: h };
    P(`   ${arm} tau ${tau / 1000} s: forced vehicle-decisions/day ${L.f(f, 1)}; exceedance <=1 s / 1-5 s / 5-60 s / >60 s per day ${['le1s', 'le5s', 'le60s', 'gt60s'].map(b => L.f(h[b], 1)).join(' / ')}`);
  }
  // guaranteed riders (Shadow arms)
  const gArms = TAUS.map(t => [SHF, t]).concat([[SHHZ, 0], [SHRC, 0]]);
  for (const [arm, tau] of gArms) {
    const g = {}; for (const k of ['G_n', 'G_dispAny1', 'G_dispUp5', 'G_late60First', 'G_deadlineBreach60', 'A_n', 'A_rev60']) g[k] = meanOf(dayMap(A, config, arm, tau, r => r[k]));
    o.guaranteed[`${arm}|${tau}`] = g;
    P(`   ${(arm + (isStrict(arm) ? ' tau ' + tau / 1000 + ' s' : '')).padEnd(18)} guaranteed/day: n ${L.f(g.G_n, 1)}, dispAny1 ${L.f(g.G_dispAny1, 2)} (${L.f(100 * g.G_dispAny1 / g.G_n, 2)}%), dispUp5 ${L.f(g.G_dispUp5, 2)}, late60First ${L.f(g.G_late60First, 2)} (${L.f(100 * g.G_late60First / g.G_n, 2)}%)`);
  }
  // 2.2 metric levels per arm (mean per day) and shares per served
  const armsAll = TAUS.map(t => [SHF, t]).concat([[SHHZ, 0], [SHRC, 0]], TAUS.map(t => ['EO35', t]), [['EOHZ35', 0], ['RCE35', 0]]);
  P('   levels per day: arm | served | visRev60 | TV5/served s | Pchg % | pDispAny1 % | late60First % | breachDeadline60 % | breachLedger % [BY-CONSTRUCTION for RC] | relocations | switchedRiders');
  for (const [arm, tau] of armsAll) {
    const lv = {
      served: meanOf(dayMap(A, config, arm, tau, served)), visRev60: meanOf(dayMap(A, config, arm, tau, r => r.visRev60)),
      tv5: meanOf(ratioMap(A, config, arm, tau, r => r.sumTV5 / 1000, served)), pchg: meanOf(ratioMap(A, config, arm, tau, r => r.pDisp, served, 100)),
      any1: meanOf(ratioMap(A, config, arm, tau, r => r.pDispAny1, served, 100)), late: meanOf(ratioMap(A, config, arm, tau, r => r.late60First, served, 100)),
      dl: meanOf(ratioMap(A, config, arm, tau, r => r.breachDeadline60, served, 100)), led: meanOf(ratioMap(A, config, arm, tau, r => r.breachLedger, served, 100)),
      reloc: meanOf(dayMap(A, config, arm, tau, r => r.relocations)), sw: meanOf(dayMap(A, config, arm, tau, r => r.switchedRiders)),
    };
    o.levels[`${arm}|${tau}`] = lv;
    P(`   ${(arm + (isStrict(arm) ? '@' + tau / 1000 + 's' : '')).padEnd(12)} ${L.f(lv.served, 1)} | ${L.f(lv.visRev60, 2)} | ${L.f(lv.tv5, 1)} | ${L.f(lv.pchg)} | ${L.f(lv.any1)} | ${L.f(lv.late)} | ${L.f(lv.dl)} | ${L.f(lv.led)} | ${L.f(lv.reloc, 1)} | ${L.f(lv.sw, 1)}`);
  }
  for (const tau of TAUS) P(`   SHF_tau ${tau / 1000} s - SHHZ served ${L.fmtC(o.shfVsHZ[tau])}`);
  SUM.r2a[config] = o;
}

// ---------------------------------------------------------------- R2b (flat world)
P('\n==================== R2b: flat world (factors = factors[0]; D10 x {w08, w17} x r0) ====================');
for (const config of ['INS', 'G2']) {
  const x = XS[config], SHF = 'SHF' + x, SHHZ = 'SHHZ' + x, SHRC = 'SHRC' + x;
  const o = { vsHZ: {}, forced: {}, levels: {} };
  P(`\n-- ${config}`);
  const sHZ = dayMap(B, config, SHHZ, 0, served);
  for (const [arm, tau] of [[SHF, 0], [SHF, 1000], [SHF, 5000], [SHRC, 0], ['EO35', 0], ['EO35', 5000], ['EOHZ35', 0], ['RCE35', 0]]) {
    const c = L.contrast(dayMap(B, config, arm, tau, served), sHZ, { better: 'higher' });
    o.vsHZ[`${arm}|${tau}`] = c;
    P(`   ${(arm + (isStrict(arm) ? '@' + tau / 1000 + 's' : '')).padEnd(12)} - ${SHHZ} served ${L.fmtC(c)}`);
  }
  for (const [arm, tau] of [['EO35', 0], ['EO35', 5000], ['RCE35', 0]]) {
    const c = L.contrast(dayMap(B, config, arm, tau, served), dayMap(B, config, 'EOHZ35', 0, served), { better: 'higher' });
    o.vsHZ[`${arm}|${tau}|vsEOHZ35`] = c;
    P(`   ${(arm + (isStrict(arm) ? '@' + tau / 1000 + 's' : '')).padEnd(12)} - EOHZ35 served ${L.fmtC(c)}`);
  }
  for (const [arm, tau] of [[SHF, 0], [SHF, 1000], [SHF, 5000], ['EO35', 0], ['EO35', 5000]]) {
    const f = meanOf(dayMap(B, config, arm, tau, r => r.forcedVehDecisions));
    const h = {}; for (const b of ['le1s', 'le5s', 'le60s', 'gt60s']) h[b] = meanOf(dayMap(B, config, arm, tau, r => (r.audit ? r.audit[b] : NaN)));
    o.forced[`${arm}|${tau}`] = { forcedPerDay: f, hist: h };
    P(`   ${arm} tau ${tau / 1000} s: forced/day ${L.f(f, 1)}; exceedance <=1 s / 1-5 s / 5-60 s / >60 s ${['le1s', 'le5s', 'le60s', 'gt60s'].map(b => L.f(h[b], 1)).join(' / ')}`);
  }
  for (const [arm, tau] of [[SHF, 0], [SHF, 1000], [SHF, 5000], [SHHZ, 0], [SHRC, 0], ['EO35', 0], ['EO35', 5000], ['EOHZ35', 0], ['RCE35', 0]]) {
    o.levels[`${arm}|${tau}`] = { served: meanOf(dayMap(B, config, arm, tau, served)), visRev60: meanOf(dayMap(B, config, arm, tau, r => r.visRev60)), late60First: meanOf(dayMap(B, config, arm, tau, r => r.late60First)), G_dispAny1: meanOf(dayMap(B, config, arm, tau, r => (r.G_dispAny1 === undefined ? NaN : r.G_dispAny1))) };
  }
  P('   levels per day (served / visRev60 / late60First / G_dispAny1): ' + Object.entries(o.levels).map(([k, v]) => `${k} ${L.f(v.served, 1)}/${L.f(v.visRev60, 1)}/${L.f(v.late60First, 1)}/${L.f(v.G_dispAny1, 1)}`).join('; '));
  SUM.r2b[config] = o;
}

// ---------------------------------------------------------------- decision labels
P('\n==================== Decision labels ====================');
const r5 = c => SUM.r2a[c].rho[5000], r5hz = c => SUM.r2a[c].rhoHZ[5000];
const flatClose = c => { const v = SUM.r2b[c] && SUM.r2b[c].vsHZ[`SHF${XS[c]}|0`]; return v && v.nDays ? Math.abs(v.mean) <= 2 : false; };
let c5;
if (!Number.isFinite(r5('INS')) || !Number.isFinite(r5('G2'))) c5 = 'INCONCLUSIVE (missing data)';
else if (r5('INS') >= 0.9 && r5('G2') >= 0.9 && flatClose('INS') && flatClose('G2')) c5 = 'REVERSED';
else if (r5('INS') > 0.6 && r5('G2') > 0.6) c5 = 'WEAKENED';
else if (r5('INS') <= 0.4 && r5('G2') <= 0.7) c5 = 'HOLDS';
else c5 = 'INCONCLUSIVE';
SUM.labels.C5 = c5;
P(`C5 "SHRC > SHF is mostly a strict-fence rounding artefact": ${c5} (rho(5 s) SHRC version: INS ${L.f(r5('INS'), 3)}, G2 ${L.f(r5('G2'), 3)}; SHHZ version: INS ${L.f(r5hz('INS'), 3)}, G2 ${L.f(r5hz('G2'), 3)}; R2b |SHF0 - SHHZ| <= 2/day: INS ${flatClose('INS')}, G2 ${flatClose('G2')})`);
for (const config of ['INS', 'G2']) {
  const e = SUM.r2a[config].eo[5000];
  const eoLab = !e.nDays ? 'INCONCLUSIVE (missing data)' : Math.abs(e.mean) > 3 && Math.max(e.neg, e.pos) >= Math.ceil(2 * e.nDays / 3) ? 'WEAKENED' : 'HOLDS';
  const s = SUM.r2a[config].shfVsHZ[5000];
  const fair = !s.nDays ? 'INCONCLUSIVE (missing data)' : Math.abs(s.mean) <= 3 ? 'SUPPORTED (SHF at 5 s within 3/day of SHHZ)' : s.mean < -8 ? 'REAL IN OUR MODEL, NOT IN ELMI\'S (SHF at 5 s still > 8/day below SHHZ)' : 'PARTLY (SHF at 5 s 3-8/day from SHHZ)';
  SUM.labels['EO35_' + config] = eoLab; SUM.labels['SHHZfair_' + config] = fair;
  P(`${config}: "EO35 barely affected, wf14 EO35 results stand" ${eoLab} (EO35_5s - EO35_0 ${L.f(e.mean, 2)}/day, ${e.neg}-/${e.pos}+/${e.ties}=) | "SHHZ fair comparator" ${fair} (SHF_5s - SHHZ ${L.f(s.mean, 2)}/day)`);
}
P('\n==================== Log ====================');
for (const l of [...new Set(LOG)]) P(l);
P(`rows R2a ${A.rows.length}, R2b ${B.rows.length}`);
fs.writeFileSync(outTxt, `wf17 R2 (wf17-exploratory-post-audit) - strict-fence tolerance. Generated ${new Date().toISOString()} by analysis/r2_analyze.js (sha256 ${L.sha256File(__filename)})\n` + LINES.join('\n') + '\n');
fs.writeFileSync(outJson, JSON.stringify(SUM, (k, v) => (k === 'days' ? undefined : v), 1));
console.log(LINES.filter(l => /^C5|^INS:|^G2:/.test(l)).join('\n'));
