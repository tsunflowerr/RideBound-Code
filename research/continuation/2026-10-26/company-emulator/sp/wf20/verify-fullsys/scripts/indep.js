// verify-fullsys: independent recomputation of wf20/fullsys (label: thăm dò sau kiểm toán = exploratory, post-audit).
// Written from scratch; does not import or copy wf20/fullsys/analysis/*. Reads raw rows only; producer tables.json is read
// at the very end, solely to compare numbers. Writes results/indep.txt, results/indep.json, results/compare.txt, results/labels_indep.txt.
'use strict';
const fs = require('fs');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const VER = process.argv[2] || '';   // output suffix (outputs are never overwritten)
const K = SP + '/wf20/fullsys', V = SP + '/wf20/verify-fullsys/results';
const nd = f => fs.readFileSync(f, 'utf8').split(/\r?\n/).filter(s => s.trim()).map(s => JSON.parse(s));
const csvDays = f => fs.readFileSync(f, 'utf8').split(/\r?\n/).map(s => s.trim()).filter(s => /^\d{4}-\d\d-\d\d$/.test(s));
const T = [], G = [];
const say = (...s) => { T.push(...s); };
const gsay = (...s) => { G.push(...s); };

const LBL = 'wf20-fullsys-exploratory-post-audit';
const rows = nd(K + '/results/rows.ndjson');
const gateRows = nd(K + '/gates/gate_rows.ndjson');
const D14 = csvDays(K + '/work/days_F14.csv'), D16 = csvDays(K + '/work/days_F16.csv');
const FZ = JSON.parse(fs.readFileSync(SP + '/wf15/frozen/shadow-params.json', 'utf8'));
const shBase = a => { const m = /^(SHF|SHHZ|SHRC)(\d+)$/.exec(a); return m ? m[1] : a; };
const isSh = a => /^(SHF|SHHZ|SHRC)\d+$/.test(a);
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);

// =============================================================== GATES (independent)
gsay(`verify-fullsys independent gate check ${new Date().toISOString()} (thăm dò sau kiểm toán)`);
gsay(`rows ${rows.length}; label == ${LBL}: ${rows.filter(r => r.label === LBL).length}; gate rows ${gateRows.length} (label ok ${gateRows.filter(r => r.label === LBL).length})`);
// x* and theta
{
  let bad = 0, nSh = 0; const ex = [];
  for (const r of [...rows, ...gateRows]) {
    if (isSh(r.arm)) {
      nSh++;
      const want = FZ.xPct[r.config], th = FZ.theta[r.config][r.win];
      const ok = r.arm === shBase(r.arm) + want && Math.abs(r.x - want / 100) < 1e-12 && r.theta === th;
      if (!ok) { bad++; if (ex.length < 3) ex.push(`${r.cell} ${r.config} ${r.arm} x ${r.x} theta ${r.theta}`); }
    } else if (r.x !== null || r.theta !== null) { bad++; if (ex.length < 3) ex.push('non-shadow with x/theta ' + r.arm); }
  }
  gsay(`x*/theta*: Shadow rows ${nSh}, wrong ${bad} -> ${bad ? 'FAIL' : 'PASS'} (x* 72 INS / 71 G2; theta = frozen theta*[config][window] exactly) ${ex.join('; ')}`);
}
// completeness
{
  const cellsOf = days => new Set(days.flatMap(d => ['w08', 'w17'].flatMap(w => ['r0', 'r1', 'r2'].map(s => `${d}-${w}-${s}`))));
  const want = { F14: cellsOf(D14), F16: cellsOf(D16) };
  const grp = new Map(); let dups = 0, incons = 0;
  for (const r of rows) {
    if (r.cell !== `${r.day}-${r.win}-${r.rs}`) incons++;
    const k = [r.panel, r.config, r.mode, shBase(r.arm)].join('|');
    if (!grp.has(k)) grp.set(k, new Set());
    const s = grp.get(k); if (s.has(r.cell)) dups++; s.add(r.cell);
  }
  let badG = 0;
  for (const p of ['F14', 'F16']) for (const c of ['INS', 'G2']) for (const m of ['N', 'CL']) for (const a of ['SHF', 'SHHZ', 'SHRC', 'EOHZ35', 'RCE35']) {
    const s = grp.get([p, c, m, a].join('|')) || new Set();
    const w = want[p];
    if (s.size !== w.size || [...w].some(x => !s.has(x))) badG++;
  }
  gsay(`completeness: groups ${grp.size} (want 40), groups with wrong cell set ${badG}, duplicate keys ${dups}, cell/day/win/rs inconsistent ${incons}; F14 days ${D14.length} (cells ${want.F14.size}), F16 days ${D16.length} (cells ${want.F16.size}) -> ${badG || dups || incons || grp.size !== 40 ? 'FAIL' : 'PASS'}`);
}
// field-level comparison helper (union of fields)
function compareSet(name, list, refMap, keyOf, extraCheck) {
  const tally = new Map(); let noRef = 0, n = 0, rowsDiff = 0;
  for (const r of list) {
    const ref = refMap.get(keyOf(r));
    if (!ref) { noRef++; continue; }
    n++;
    const fields = new Set([...Object.keys(ref), ...Object.keys(r)]);
    let d = false;
    for (const f of fields) if (!same(ref[f], r[f])) { tally.set(f, (tally.get(f) || 0) + 1); }
    if (extraCheck) { const e = extraCheck(r, ref); if (e) tally.set(e, (tally.get(e) || 0) + 1); }
    void d;
  }
  return { name, rows: list.length, compared: n, noRef, tally: Object.fromEntries([...tally].sort((a, b) => b[1] - a[1])) };
}
function refIndex(list, keyOf) { const m = new Map(); let dup = 0; for (const r of list) { const k = keyOf(r); if (m.has(k)) dup++; m.set(k, r); } return { m, dup }; }
// g1: F14 mode-N rows vs wf15 confirm main
const W15 = nd(SP + '/wf15/results/confirm/main.ndjson');
const w15i = refIndex(W15, r => [r.cell, r.config, r.arm].join('|'));
const g1sh = compareSet('g1 F14 N Shadow vs wf15', rows.filter(r => r.panel === 'F14' && r.mode === 'N' && isSh(r.arm)), w15i.m, r => [r.cell, r.config, r.arm].join('|'));
const g1x = compareSet('g1x F14 N EOHZ35/RCE35 vs wf15', rows.filter(r => r.panel === 'F14' && r.mode === 'N' && !isSh(r.arm)), w15i.m, r => [r.cell, r.config, r.arm].join('|'));
gsay(`wf15 main rows ${W15.length}, duplicate (cell|config|arm) keys ${w15i.dup}`);
// fields that are allowed to differ (bookkeeping, timing, new fields)
const BOOK1 = new Set(['part', 'set', 'ms', 'msScore', 'engine', 'label', 'panel', 'sigR3', 'mode']);
for (const g of [g1sh, g1x]) {
  const metricDiffs = Object.entries(g.tally).filter(([f]) => !BOOK1.has(f));
  gsay(`${g.name}: rows ${g.rows}, compared ${g.compared}, no ref ${g.noRef}; fields differing (count of rows): ${JSON.stringify(g.tally)}`);
  gsay(`   -> non-bookkeeping fields differing: ${metricDiffs.length ? JSON.stringify(metricDiffs) : 'none'}`);
}
// g2: EOHZ35/RCE35 N and CL vs wf17 R3
const R3 = nd(SP + '/wf17/r1-identity-fence-pad/results/r3.ndjson').filter(r => r.flatRatio === null || r.flatRatio === undefined);
const r3i = refIndex(R3, r => [r.panel, r.cell, r.config, r.mode, r.arm].join('|'));
const g2 = compareSet('g2 EOHZ35/RCE35 vs wf17 R3', rows.filter(r => r.arm === 'EOHZ35' || r.arm === 'RCE35'), r3i.m, r => [r.panel, r.cell, r.config, r.mode, r.arm].join('|'),
  (r, ref) => (r.sigR3 === ref.sig ? null : 'SIGR3!=R3sig'));
gsay(`wf17 R3 rows (flatRatio null) ${R3.length}, duplicate keys ${r3i.dup}`);
{
  // restrict to fields present in the R3 row (R3 rows carry fewer fields); report separately the R3 fields that differ
  const r3fields = new Set(R3.flatMap(r => Object.keys(r)));
  const inR3 = Object.entries(g2.tally).filter(([f]) => r3fields.has(f) || f === 'SIGR3!=R3sig');
  gsay(`${g2.name}: rows ${g2.rows}, compared ${g2.compared}, no ref ${g2.noRef}; R3 fields differing: ${JSON.stringify(inR3)}`);
  const BOOK2 = new Set(['label', 'check', 'ms', 'sig']);
  const nb = inR3.filter(([f]) => !BOOK2.has(f));
  gsay(`   -> non-bookkeeping R3 fields differing (sig differs by design: wf20 sig includes the clipped pickup ETA; sigR3 is the R3 fingerprint): ${nb.length ? JSON.stringify(nb) : 'none'}`);
}
// sig of Shadow N rows vs wf15 (rider-level identity, excluded by the producer's g1)
{
  const L = rows.filter(r => r.panel === 'F14' && r.mode === 'N');
  let same1 = 0, n = 0; for (const r of L) { const ref = w15i.m.get([r.cell, r.config, r.arm].join('|')); if (!ref || ref.sig === undefined) continue; n++; if (ref.sig === r.sig) same1++; }
  gsay(`rider fingerprint 'sig' of F14 N rows vs wf15 rows that carry sig: equal ${same1}/${n}`);
}
// g3
for (const m of ['N', 'CL']) {
  const L = rows.filter(r => shBase(r.arm) === 'SHRC' && r.mode === m);
  const bad = L.filter(r => r.breachLedger !== 0 || r.G_ledgerBreach !== 0 || r.A_ledgerBreach !== 0).length;
  gsay(`g3 SHRC ${m}: rows ${L.length}, rows with any ledger breach ${bad} -> ${L.length && !bad ? 'PASS' : 'FAIL'}`);
}
{
  const ctx = {};
  for (const r of rows) { const k = shBase(r.arm) + '_' + r.config + '_' + r.mode; ctx[k] = (ctx[k] || 0) + r.breachLedger; }
  gsay(`context: total breachLedger by arm (sum over rows): ${JSON.stringify(ctx)}`);
}
// g5
{
  const prod = new Map(rows.map(r => [[r.panel, r.cell, r.config, r.mode, r.arm].join('|'), r]));
  let n = 0, bad = 0; const fd = {};
  for (const g of gateRows) {
    const p = prod.get([g.panel, g.cell, g.config, g.mode, g.arm].join('|')); if (!p) continue; n++;
    const f = [...new Set([...Object.keys(g), ...Object.keys(p)])].filter(k => k !== 'ms' && k !== 'msScore' && !same(g[k], p[k]));
    if (f.length) { bad++; for (const k of f) fd[k] = (fd[k] || 0) + 1; }
  }
  gsay(`g5 determinism: gate rows ${gateRows.length}, matched in production ${n}, differing ${bad} ${JSON.stringify(fd)} -> ${n === gateRows.length && !bad ? 'PASS' : 'FAIL'}`);
}
// forecast really active in Shadow CL rows
{
  const byKey = new Map(rows.map(r => [[r.panel, r.cell, r.config, r.mode, r.arm].join('|'), r]));
  const res = {};
  for (const r of rows) if (r.mode === 'CL') {
    const n = byKey.get([r.panel, r.cell, r.config, 'N', r.arm].join('|'));
    const k = shBase(r.arm) + '_' + r.config;
    if (!res[k]) res[k] = { pairs: 0, sigEqual: 0, clipCL: 0, clipN: 0, metricsEqual: 0 };
    const o = res[k]; o.pairs++;
    if (n.sig === r.sig) o.sigEqual++;
    if (n.served === r.served && n.late60First === r.late60First && n.visRev60 === r.visRev60 && n.sumTV5 === r.sumTV5) o.metricsEqual++;
    o.clipCL += (r.fc.clippedInitial || 0) + (r.fc.clippedRevision || 0);
    o.clipN += (n.fc.clippedInitial || 0) + (n.fc.clippedRevision || 0);
  }
  gsay(`forecast active (CL vs N same cell): ${JSON.stringify(res)}`);
}
// run manifests and error files
{
  const errFiles = fs.readdirSync(K + '/results').filter(f => /error/i.test(f));
  const runs = nd(K + '/results/rows.ndjson.runs.ndjson');
  const summ = runs.map(r => Object.fromEntries(Object.entries(r).filter(([k]) => /err|done|tasks|fin|end|rows/i.test(k)))) ;
  gsay(`error files in results/: ${errFiles.length ? errFiles.join(',') : 'none'}; run manifest lines ${runs.length}; fields ${JSON.stringify(summ).slice(0, 600)}`);
}

// =============================================================== DAY TABLES
const FIELDS = ['served', 'visRev60', 'late60First', 'pDisp', 'breachDeadline60', 'sumTV5', 'G_late60First', 'G_n', 'breachLedger', 'relocations'];
const idx = new Map();   // config|mode|arm|day -> {panel, cells:[]}
for (const r of rows) {
  const k = [r.config, r.mode, shBase(r.arm), r.day].join('|');
  if (!idx.has(k)) idx.set(k, { panel: r.panel, cells: [] });
  idx.get(k).cells.push(r);
}
const allDays = [...D14.map(d => ['F14', d]), ...D16.map(d => ['F16', d])];
const dayCache = new Map();
function dayAgg(config, mode, arm, day) {
  const ck = [config, mode, arm, day].join('|');
  if (dayCache.has(ck)) return dayCache.get(ck);
  const e = idx.get(ck);
  if (!e) throw new Error('missing ' + ck);
  const res = { panel: e.panel };
  for (const w of ['w08', 'w17']) {
    const cs = e.cells.filter(c => c.win === w);
    if (cs.length !== 3 || new Set(cs.map(c => c.rs)).size !== 3) throw new Error('incomplete ' + ck + ' ' + w);
    const o = {};
    for (const f of FIELDS) { const v = cs.map(c => c[f]); o[f] = v.some(x => x === undefined || x === null) ? null : (v[0] + v[1] + v[2]) / 3; }
    res[w] = o;
  }
  res.day = {}; for (const f of FIELDS) res.day[f] = res.w08[f] === null ? null : res.w08[f] + res.w17[f];
  dayCache.set(ck, res);
  return res;
}
// metrics (id names chosen to match the producer's ids only for the final comparison)
const MET = [
  ['served', X => X.served, true],
  ['visRev60', X => X.visRev60, true],
  ['visRev60/1000', X => X.visRev60 * 1000 / X.served, false],
  ['late60First', X => X.late60First, true],
  ['late60First/1000', X => X.late60First * 1000 / X.served, false],
  ['Pchg(pDisp)', X => X.pDisp, true],
  ['Pchg/1000', X => X.pDisp * 1000 / X.served, false],
  ['breachDeadline60', X => X.breachDeadline60, true],
  ['breachDeadline60/1000', X => X.breachDeadline60 * 1000 / X.served, false],
  ['TV5 s/served', X => X.sumTV5 / X.served / 1000, false],
  ['G_late60First', X => X.G_late60First, true],
  ['G_late60First/1000', X => (X.G_late60First === null ? null : X.G_late60First * 1000 / X.served), false],
  ['guaranteedShare %', X => (X.G_n === null ? null : X.G_n * 100 / X.served), false],
  ['breachLedger/1000', X => X.breachLedger * 1000 / X.served, false],
  ['relocations', X => X.relocations, true],
];
const SCOPES = ['day', 'w08', 'w17'];

// ---------- statistics (own implementations)
// t quantile: exact gamma ratio for integer df by recursion, Simpson integration of the density, bisection
function gammaRatio(nu) { // Gamma((nu+1)/2) / Gamma(nu/2)
  let r = nu % 2 === 1 ? 1 / Math.sqrt(Math.PI) : Math.sqrt(Math.PI) / 2, k = nu % 2 === 1 ? 1 : 2;
  while (k < nu) { r *= (k + 1) / k; k += 2; } return r;
}
const tqCache = {};
function tq975(nu) {
  if (tqCache[nu]) return tqCache[nu];
  const c = gammaRatio(nu) / Math.sqrt(nu * Math.PI), pdf = t => c * Math.pow(1 + t * t / nu, -(nu + 1) / 2);
  const integ = x => { const N = 20000, h = x / N; let s = pdf(0) + pdf(x); for (let i = 1; i < N; i++) s += (i % 2 ? 4 : 2) * pdf(i * h); return s * h / 3; };
  let lo = 0, hi = 20; for (let i = 0; i < 60; i++) { const m = (lo + hi) / 2; if (integ(m) < 0.475) lo = m; else hi = m; }
  return (tqCache[nu] = (lo + hi) / 2);
}
function binom2(n, k) { let c = 1n; for (let i = 0n; i < BigInt(k); i++) c = c * (BigInt(n) - i) / (i + 1n); return c; }
function signTest(np, nm) {
  const n = np + nm; if (n === 0) return 1;
  const k = Math.min(np, nm); let s = 0n; for (let i = 0; i <= k; i++) s += binom2(n, i);
  const p = 2 * Number(s * 10n ** 18n / (2n ** BigInt(n))) / 1e18; return Math.min(1, p);
}
function paired(d) {
  const n = d.length; const mean = d.reduce((a, b) => a + b, 0) / n;
  let ss = 0; for (const x of d) ss += (x - mean) * (x - mean);
  const sd = Math.sqrt(ss / (n - 1)), se = sd / Math.sqrt(n), t = tq975(n - 1);
  const s = [...d].sort((a, b) => a - b), median = n % 2 ? s[(n - 1) >> 1] : (s[n / 2 - 1] + s[n / 2]) / 2;
  const nPlus = d.filter(x => x > 1e-9).length, nMinus = d.filter(x => x < -1e-9).length;
  return { n, mean, se, lo: mean - t * se, hi: mean + t * se, median, nPlus, nMinus, ties: n - nPlus - nMinus, p: signTest(nPlus, nMinus) };
}
const panelDays = ps => allDays.filter(([p]) => ps === 'pooled' || p === ps).map(([, d]) => d);
function contrast(A, B) {   // A, B = [config, mode, arm]
  const out = {};
  for (const ps of ['F14', 'F16', 'pooled']) {
    const days = panelDays(ps), R = {};
    for (const [id, f, cnt] of MET) for (const sc of SCOPES) {
      const a = days.map(d => f(dayAgg(...A, d)[sc])), b = days.map(d => f(dayAgg(...B, d)[sc]));
      if (a.some(x => x === null || !isFinite(x)) || b.some(x => x === null || !isFinite(x))) continue;
      const sa = a.reduce((x, y) => x + y, 0), sb = b.reduce((x, y) => x + y, 0);
      R[id + '|' + sc] = { meanA: sa / days.length, meanB: sb / days.length, rel: cnt && sb ? sa / sb - 1 : null, ...paired(a.map((x, i) => x - b[i])) };
    }
    out[ps] = { days: days.length, R };
  }
  return out;
}
function did(A1, B1, A2, B2) {
  const out = {};
  for (const ps of ['F14', 'F16', 'pooled']) {
    const days = panelDays(ps), R = {};
    for (const [id, f] of MET) for (const sc of SCOPES) {
      const v = days.map(d => { const q = [A1, B1, A2, B2].map(s => f(dayAgg(...s, d)[sc])); return q.some(x => x === null || !isFinite(x)) ? null : (q[0] - q[1]) - (q[2] - q[3]); });
      if (v.some(x => x === null)) continue;
      R[id + '|' + sc] = paired(v);
    }
    out[ps] = { days: days.length, R };
  }
  return out;
}
function level(S) {
  const out = {};
  for (const ps of ['F14', 'F16', 'pooled']) {
    const days = panelDays(ps), R = {};
    for (const [id, f] of MET) for (const sc of SCOPES) {
      const a = days.map(d => f(dayAgg(...S, d)[sc]));
      if (a.some(x => x === null || !isFinite(x))) continue;
      R[id + '|' + sc] = a.reduce((x, y) => x + y, 0) / a.length;
    }
    out[ps] = { days: days.length, R };
  }
  return out;
}

// ---------- the contrasts (producer ids kept as names only for the comparison step)
const C = {};
for (const cfg of ['INS', 'G2']) {
  C[`MAIN ${cfg}: SHRC_CL - SHHZ_CL`] = contrast([cfg, 'CL', 'SHRC'], [cfg, 'CL', 'SHHZ']);
  C[`${cfg}: SHRC_N - SHHZ_N (same contrast without forecast)`] = contrast([cfg, 'N', 'SHRC'], [cfg, 'N', 'SHHZ']);
  C[`${cfg}: SHRC_CL - SHF_CL`] = contrast([cfg, 'CL', 'SHRC'], [cfg, 'CL', 'SHF']);
  for (const a of ['SHF', 'SHHZ', 'SHRC']) C[`FC ${cfg}: ${a}_CL - ${a}_N`] = contrast([cfg, 'CL', a], [cfg, 'N', a]);
  for (const a of ['EOHZ35', 'RCE35']) C[`REF ${cfg}: ${a}_CL - ${a}_N`] = contrast([cfg, 'CL', a], [cfg, 'N', a]);
  C[`REF ${cfg}: RCE35_CL - EOHZ35_CL`] = contrast([cfg, 'CL', 'RCE35'], [cfg, 'CL', 'EOHZ35']);
  C[`REF ${cfg}: RCE35_N - EOHZ35_N`] = contrast([cfg, 'N', 'RCE35'], [cfg, 'N', 'EOHZ35']);
  C[`SHADOW ${cfg}: SHHZ_CL - EOHZ35_CL`] = contrast([cfg, 'CL', 'SHHZ'], [cfg, 'CL', 'EOHZ35']);
  C[`SHADOW ${cfg}: SHRC_CL - RCE35_CL`] = contrast([cfg, 'CL', 'SHRC'], [cfg, 'CL', 'RCE35']);
}
const SYS = { a: ['INS', 'N', 'SHHZ'], b: ['G2', 'CL', 'SHRC'], c: ['G2', 'CL', 'SHHZ'], d: ['G2', 'CL', 'RCE35'], e: ['G2', 'CL', 'EOHZ35'] };
C['SYS b-a: full system (SHRC+G2+CL) - Elmi as in paper (SHHZ+INS+N)'] = contrast(SYS.b, SYS.a);
C['SYS b-c: full system - fair control (SHHZ+G2+CL)'] = contrast(SYS.b, SYS.c);
C['SYS b-d: full system - RCE35+G2+CL (what Shadow adds)'] = contrast(SYS.b, SYS.d);
C['SYS d-a: RCE35+G2+CL (no Shadow) - Elmi as in paper'] = contrast(SYS.d, SYS.a);
C['SYS c-a: fair control (SHHZ+G2+CL) - Elmi as in paper'] = contrast(SYS.c, SYS.a);
const DD = {};
for (const cfg of ['INS', 'G2']) {
  DD[`DiD ${cfg}: (SHRC-SHHZ)_CL - (SHRC-SHHZ)_N`] = did([cfg, 'CL', 'SHRC'], [cfg, 'CL', 'SHHZ'], [cfg, 'N', 'SHRC'], [cfg, 'N', 'SHHZ']);
  DD[`DiD ${cfg}: (SHRC-SHHZ)_CL - (RCE35-EOHZ35)_CL`] = did([cfg, 'CL', 'SHRC'], [cfg, 'CL', 'SHHZ'], [cfg, 'CL', 'RCE35'], [cfg, 'CL', 'EOHZ35']);
}
const LV = {}; for (const k of Object.keys(SYS)) LV[k] = level(SYS[k]);

// ---------- human-readable output
const f2 = x => (x === null || x === undefined || !isFinite(x) ? 'n/a' : Math.abs(x) >= 100 ? x.toFixed(1) : x.toFixed(2));
const fp = p => (p < 0.001 ? p.toExponential(1) : p.toFixed(3));
const cellS = s => (s ? `${f2(s.mean)} ±${f2(s.se)} [${f2(s.lo)},${f2(s.hi)}] ${s.nMinus}-/${s.nPlus}+/${s.ties}= p ${fp(s.p)}` : 'n/a');
const KEYM = ['served', 'visRev60', 'late60First/1000', 'Pchg/1000', 'breachDeadline60/1000', 'TV5 s/served', 'G_late60First', 'guaranteedShare %', 'breachLedger/1000', 'relocations'];
say('verify-fullsys independent recomputation (thăm dò sau kiểm toán). Day = w08 + w17 of the mean over r0..r2; per 1000 = per 1000 served.');
say('Cell: mean Δ ±SE [95% t-CI] n−/n+/ties p(sign). rel = ΣA/ΣB − 1 (counts).');
for (const [id, r] of Object.entries(C)) {
  say('', '=== ' + id);
  for (const m of KEYM) for (const sc of SCOPES) {
    const k = m + '|' + sc, p = r.pooled.R[k]; if (!p) continue;
    say(`${k.padEnd(28)} A ${f2(p.meanA).padStart(7)} B ${f2(p.meanB).padStart(7)} rel ${p.rel === null ? '   ' : (100 * p.rel).toFixed(1) + '%'} | pooled ${cellS(p)} | F14 ${cellS(r.F14.R[k])} | F16 ${cellS(r.F16.R[k])}`);
  }
}
for (const [id, r] of Object.entries(DD)) {
  say('', '=== ' + id);
  for (const m of KEYM) for (const sc of SCOPES) { const k = m + '|' + sc; if (!r.pooled.R[k]) continue; say(`${k.padEnd(28)} | pooled ${cellS(r.pooled.R[k])} | F14 ${cellS(r.F14.R[k])} | F16 ${cellS(r.F16.R[k])}`); }
}
say('', '=== WHOLE SYSTEM LEVELS pooled (30 d): a SHHZ+INS+N | b SHRC+G2+CL | c SHHZ+G2+CL | d RCE35+G2+CL | e EOHZ35+G2+CL');
for (const m of KEYM) for (const sc of SCOPES) { const k = m + '|' + sc; if (Object.values(LV).every(l => l.pooled.R[k] === undefined)) continue;
  say(k.padEnd(28) + Object.keys(SYS).map(s => f2(LV[s].pooled.R[k]).padStart(9)).join('')); }
// F14 vs F16 sign agreement for the key contrasts
{
  const keys = ['MAIN INS: SHRC_CL - SHHZ_CL', 'MAIN G2: SHRC_CL - SHHZ_CL', ...Object.keys(C).filter(k => k.startsWith('FC ') || k.startsWith('SYS '))];
  const dis = [];
  for (const id of keys) for (const m of ['served', 'visRev60', 'late60First/1000', 'Pchg/1000', 'breachDeadline60/1000', 'TV5 s/served']) for (const sc of SCOPES) {
    const a = C[id].F14.R[m + '|' + sc], b = C[id].F16.R[m + '|' + sc]; if (!a || !b) continue;
    if (Math.sign(a.mean) !== Math.sign(b.mean)) dis.push(`${id} ${m}|${sc}: F14 ${f2(a.mean)} (p ${fp(a.p)}) F16 ${f2(b.mean)} (p ${fp(b.p)})`);
  }
  say('', `F14 vs F16 sign disagreements over ${keys.length} contrasts x 6 metrics x 3 scopes: ${dis.length}`, ...dis.map(s => '  ' + s));
}

// =============================================================== LABELS (own reading of PLAN §5)
const L = [];
function lab(id, desc, src, mkey, band, opt = {}) {
  // band [lo, hi]; opt: {rel: true (use rel %), minDays: fraction, flip: fn(stat, val) -> bool}
  const out = {};
  for (const ps of ['pooled', 'F14', 'F16']) {
    const s = src[ps].R[mkey]; if (!s) { out[ps] = 'n/a'; continue; }
    const val = opt.rel ? 100 * s.rel : s.mean;
    const [lo, hi] = band, twoSided = lo <= 0 && hi >= 0;
    const flip = opt.flip ? opt.flip(s, val) : false;
    let L1;
    if (flip) L1 = 'REVERSED';
    else if (twoSided) L1 = val >= lo && val <= hi ? 'HOLDS' : 'INCONCLUSIVE';
    else {
      const dir = hi < 0 ? -1 : 1, nonTied = s.nPlus + s.nMinus, onSide = dir < 0 ? s.nMinus : s.nPlus;
      const daysOK = !opt.minDays || (nonTied > 0 && onSide / nonTied >= opt.minDays);
      const rightSign = Math.sign(val) === dir;
      const beyond = dir < 0 ? val < lo : val > hi, inside = val >= lo && val <= hi;
      if (rightSign && inside && daysOK) L1 = 'HOLDS';
      else if (rightSign && beyond && daysOK) L1 = 'STRONGER';
      else if (rightSign) L1 = 'WEAKENED';
      else L1 = 'INCONCLUSIVE';
    }
    out[ps] = `${L1} ${opt.rel ? val.toFixed(1) + '%' : f2(val)} (${s.nMinus}-/${s.nPlus}+ p ${fp(s.p)})`;
  }
  L.push({ id, desc, ...out });
}
const sigP = s => s.p < 0.05;
const MAINI = C['MAIN INS: SHRC_CL - SHHZ_CL'], MAING = C['MAIN G2: SHRC_CL - SHHZ_CL'];
lab('P1', 'G2 MAIN visRev60/day', MAING, 'visRev60|day', [-16, -10], { minDays: 0.8, flip: s => s.mean >= 0 || (s.nPlus >= s.nMinus && sigP(s)) });
lab('P1r', 'G2 MAIN visRev60 rel %', MAING, 'visRev60|day', [-40, -25], { rel: true, minDays: 0.8, flip: s => s.mean >= 0 });
lab('P2', 'G2 MAIN served/day', MAING, 'served|day', [-14, -6], { minDays: 0.7, flip: s => s.mean >= 0 && s.nPlus >= (s.nPlus + s.nMinus) / 2 });
lab('P3', 'G2 MAIN late60First/1000', MAING, 'late60First/1000|day', [-3, 6], { flip: (s, v) => v <= -6 && sigP(s) });
lab('P4', 'G2 MAIN Pchg/1000', MAING, 'Pchg/1000|day', [-10, 3], { flip: (s, v) => v >= 5 && sigP(s) });
lab('P5', 'G2 MAIN TV5 s/served', MAING, 'TV5 s/served|day', [-22, -10], { flip: (s, v) => v >= 0 });
lab('P6', 'G2 MAIN breachDeadline60/1000', MAING, 'breachDeadline60/1000|day', [-3, 2], { flip: (s, v) => (v < -3 || v > 2) && sigP(s) });
lab('P7', 'G2 MAIN G_late60First/day', MAING, 'G_late60First|day', [-2.5, 1], { flip: (s, v) => v >= 2 && sigP(s) });
lab('P8', 'INS MAIN visRev60/day', MAINI, 'visRev60|day', [-2.5, 0.5], { flip: s => 100 * s.rel <= -20 && s.nMinus / (s.nPlus + s.nMinus) >= 0.75 });
lab('P8r', 'INS MAIN visRev60 rel % (|rel|<=10)', MAINI, 'visRev60|day', [-10, 10], { rel: true });
lab('P9', 'INS MAIN served/day', MAINI, 'served|day', [-8, -2], { flip: (s, v) => v >= 2 && sigP(s) });
lab('P10a', 'INS MAIN late60First/1000', MAINI, 'late60First/1000|day', [-4, 4], { flip: (s, v) => (v < -4 || v > 4) && sigP(s) });
lab('P10b', 'INS MAIN Pchg/1000', MAINI, 'Pchg/1000|day', [-4, 4], { flip: (s, v) => (v < -4 || v > 4) && sigP(s) });
lab('P10c', 'INS MAIN TV5', MAINI, 'TV5 s/served|day', [-3, 3], { flip: (s, v) => (v < -3 || v > 3) && sigP(s) });
for (const cfg of ['INS', 'G2']) for (const a of ['SHF', 'SHHZ', 'SHRC']) {
  const X = C[`FC ${cfg}: ${a}_CL - ${a}_N`], t = `${cfg} ${a} CL-N`;
  lab('P11', t + ' late60First/1000 day', X, 'late60First/1000|day', [-50, -25], { minDays: 0.8, flip: s => s.mean >= 0 || s.nMinus / (s.nPlus + s.nMinus) < 0.6 });
  lab('P12', t + ' late60First/1000 w08', X, 'late60First/1000|w08', [-140, -80], { flip: s => s.mean >= 0 });
  lab('P13', t + ' late60First/1000 w17', X, 'late60First/1000|w17', [20, 45], { minDays: 0.75, flip: s => s.mean <= 0 || s.nPlus / (s.nPlus + s.nMinus) < 0.5 });
  lab('P14', t + ' visRev60 rel %', X, 'visRev60|day', [-45, -20], { rel: true, minDays: 0.75, flip: s => s.mean >= 0 });
  lab('P15', t + ' TV5', X, 'TV5 s/served|day', [-25, -10], { flip: s => s.mean >= 0 });
  lab('P16', t + ' breachDeadline60 rel %', X, 'breachDeadline60|day', [-80, -30], { rel: true, flip: s => s.mean >= 0 });
  lab('P17a', t + ' Pchg/1000', X, 'Pchg/1000|day', [-5, 5], { flip: (s, v) => Math.abs(v) > 5 && sigP(s) });
  lab('P17b', t + ' served/day', X, 'served|day', [-4, 4], { flip: (s, v) => Math.abs(v) > 4 && sigP(s) });
  for (const sc of SCOPES) lab('P18', t + ' guaranteedShare pp ' + sc, X, 'guaranteedShare %|' + sc, [-3, 3], { flip: (s, v) => Math.abs(v) > 5 });
}
lab('P19', 'DiD G2 visRev60/day', DD['DiD G2: (SHRC-SHHZ)_CL - (SHRC-SHHZ)_N'], 'visRev60|day', [0, 3], { flip: (s, v) => v <= -3 && sigP(s) });
const BA = C['SYS b-a: full system (SHRC+G2+CL) - Elmi as in paper (SHHZ+INS+N)'], BD = C['SYS b-d: full system - RCE35+G2+CL (what Shadow adds)'];
lab('P20', 'SYS b-a served/day', BA, 'served|day', [-6, 6]);
lab('P21', 'SYS b-a visRev60/day', BA, 'visRev60|day', [-10, -2], { flip: s => s.mean >= 0 });
lab('P22a', 'SYS b-a late60First/1000 day', BA, 'late60First/1000|day', [-45, -15], { flip: s => s.mean >= 0 });
lab('P22b', 'SYS b-a late60First/1000 w17', BA, 'late60First/1000|w17', [15, 50], { flip: s => s.mean <= 0 });
lab('P23a', 'SYS b-a Pchg/1000', BA, 'Pchg/1000|day', [-5, 12], { flip: (s, v) => (v < -5 || v > 12) && sigP(s) });
lab('P23b', 'SYS b-a TV5', BA, 'TV5 s/served|day', [-20, -1], { flip: s => s.mean >= 0 });
const flipOpp = dir => s => Math.sign(s.mean) === -dir && sigP(s);
lab('P25a', 'SYS b-d served/day', BD, 'served|day', [-15, -4], { flip: flipOpp(-1) });
lab('P25b', 'SYS b-d visRev60/day', BD, 'visRev60|day', [6, 16], { flip: flipOpp(1) });
lab('P25c', 'SYS b-d late60First/1000', BD, 'late60First/1000|day', [-80, -40], { flip: flipOpp(-1) });
lab('P25d', 'SYS b-d Pchg/1000', BD, 'Pchg/1000|day', [-115, -80], { flip: flipOpp(-1) });
lab('P25e', 'SYS b-d TV5', BD, 'TV5 s/served|day', [70, 110], { flip: flipOpp(1) });
lab('P25f', 'SYS b-d breachDeadline60/1000', BD, 'breachDeadline60/1000|day', [5, 30], { flip: flipOpp(1) });
for (const cfg of ['INS', 'G2']) lab('P26', cfg + ' SHRC_CL - SHF_CL served/day', C[`${cfg}: SHRC_CL - SHF_CL`], 'served|day', [25, 40], { flip: s => s.mean <= 10 });
const tally = {}; for (const l of L) { const k = l.pooled.split(' ')[0]; tally[k] = (tally[k] || 0) + 1; }
const LT = ['verify-fullsys own labels (PLAN §5 read independently; STRONGER/HOLDS require day-consistency; two-sided bands: inside = HOLDS, else flip decides)'];
for (const l of L) LT.push(`${l.id.padEnd(5)} ${l.desc.padEnd(42)} pooled ${l.pooled} | F14 ${l.F14} | F16 ${l.F16}`);
LT.push('pooled tally ' + JSON.stringify(tally));

// =============================================================== COMPARISON with producer tables.json
const PJ = JSON.parse(fs.readFileSync(K + '/results/tables.json', 'utf8'));
const CMP = [];
let nNum = 0, nInt = 0, maxAbs = 0, maxRelP = 0, missing = 0, intBad = 0; const worst = [];
function cmpStat(where, mine, theirs) {
  if (!theirs) { missing++; return; }
  for (const f of ['meanA', 'meanB', 'rel', 'mean', 'se', 'lo', 'hi', 'median']) {
    if (mine[f] === undefined) continue;
    if (mine[f] === null && (theirs[f] === null || theirs[f] === undefined)) continue;
    const d = Math.abs(mine[f] - theirs[f]); nNum++;
    if (!(d <= 1e-9 * Math.max(1, Math.abs(theirs[f])))) worst.push(`${where} ${f} mine ${mine[f]} theirs ${theirs[f]}`);
    if (d > maxAbs) maxAbs = d;
  }
  for (const f of ['n', 'nPlus', 'nMinus', 'ties']) { if (mine[f] === undefined) continue; nInt++; if (mine[f] !== theirs[f]) { intBad++; worst.push(`${where} ${f} mine ${mine[f]} theirs ${theirs[f]}`); } }
  if (mine.p !== undefined) { const d = Math.abs(mine.p - theirs.p) / Math.max(1e-300, theirs.p); nNum++; if (d > maxRelP) maxRelP = d; if (d > 1e-9) worst.push(`${where} p mine ${mine.p} theirs ${theirs.p}`); }
}
for (const [id, r] of Object.entries(C)) { const t = PJ.contrasts[id]; if (!t) { CMP.push('producer lacks contrast ' + id); continue; }
  for (const ps of ['F14', 'F16', 'pooled']) for (const [k, s] of Object.entries(r[ps].R)) cmpStat(`${id} ${ps} ${k}`, s, t[ps].R[k]);
  for (const ps of ['F14', 'F16', 'pooled']) { const extra = Object.keys(t[ps].R).filter(k => !r[ps].R[k]); if (extra.length) CMP.push(`producer has extra keys in ${id} ${ps}: ${extra.join(',')}`); } }
for (const [id, r] of Object.entries(DD)) { const t = PJ.did[id]; if (!t) { CMP.push('producer lacks DiD ' + id); continue; }
  for (const ps of ['F14', 'F16', 'pooled']) for (const [k, s] of Object.entries(r[ps].R)) cmpStat(`${id} ${ps} ${k}`, s, t[ps].R[k]); }
for (const [s, l] of Object.entries(LV)) for (const ps of ['F14', 'F16', 'pooled']) for (const [k, v] of Object.entries(l[ps].R)) {
  const t = PJ.levels[s] && PJ.levels[s][ps].R[k]; if (t === undefined) { missing++; continue; }
  nNum++; const d = Math.abs(v - t); if (d > maxAbs) maxAbs = d; if (d > 1e-9 * Math.max(1, Math.abs(t))) worst.push(`level ${s} ${ps} ${k} mine ${v} theirs ${t}`); }
CMP.unshift(`compared numeric values ${nNum}, integer counts ${nInt}; max abs diff ${maxAbs.toExponential(2)}; max rel diff of p ${maxRelP.toExponential(2)}; integer mismatches ${intBad}; keys missing at producer ${missing}; values beyond 1e-9: ${worst.length}`);
CMP.push(...worst.slice(0, 40));
// label comparison with producer labels.txt
{
  const pl = fs.readFileSync(K + '/results/labels.txt', 'utf8').split(/\r?\n/).filter(s => /^P\d/.test(s));
  const theirs = pl.map(s => { const m = /^(\S+)\s+(.*?)\s+band .*?pooled (\S+) (\S+).*?\| F14 (\S+) (\S+).*?\| F16 (\S+) (\S+)/.exec(s); return m ? { id: m[1], desc: m[2].trim(), pooled: m[3], pv: m[4], F14: m[5], F16: m[7] } : null; }).filter(Boolean);
  const mine = L.filter(l => l.id !== 'P8r');
  let agree = 0, diff = []; const n = Math.min(theirs.length, mine.length);
  // match in order of appearance by id and description head
  const used = new Set();
  for (const m of mine) {
    const j = theirs.findIndex((t, i) => !used.has(i) && t.id === m.id && t.desc.replace(/\s+/g, ' ').toLowerCase().includes(m.desc.split(' ').slice(0, 3).join(' ').toLowerCase()));
    if (j < 0) { diff.push('no producer label for ' + m.id + ' ' + m.desc); continue; }
    used.add(j); const t = theirs[j];
    for (const ps of ['pooled', 'F14', 'F16']) { const a = m[ps].split(' ')[0]; if (a === t[ps]) agree++; else diff.push(`${m.id} ${m.desc} ${ps}: mine ${a} theirs ${t[ps]}`); }
  }
  CMP.push('', `labels: producer lines ${theirs.length}, mine ${mine.length} (+P8r extra), label agreements ${agree}, differences ${diff.length}`, ...diff.slice(0, 40));
  void n;
}

fs.writeFileSync(V + '/indep_gates' + VER + '.txt', G.join('\n') + '\n');
fs.writeFileSync(V + '/indep' + VER + '.txt', T.join('\n') + '\n');
fs.writeFileSync(V + '/labels_indep' + VER + '.txt', LT.join('\n') + '\n');
fs.writeFileSync(V + '/compare' + VER + '.txt', CMP.join('\n') + '\n');
fs.writeFileSync(V + '/indep' + VER + '.json', JSON.stringify({ label: 'verify-fullsys (thăm dò sau kiểm toán)', created: new Date().toISOString(), contrasts: C, did: DD, levels: LV, labels: L }, null, 1));
console.log(G.join('\n')); console.log('----'); console.log(CMP.slice(0, 60).join('\n')); console.log('----'); console.log(LT.slice(-1)[0]);
