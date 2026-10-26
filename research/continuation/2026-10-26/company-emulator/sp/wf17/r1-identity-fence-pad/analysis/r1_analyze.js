// wf17 R1 analysis (EXPLORATORY, POST-AUDIT; RUNPLAN R1): B3 identity, plain/round (P) vs ridebound-fleetpy-adapter +
// fleetpy-seconds-half-even (A) on D10 x {w08, w17} x r0. Written and hashed before the R1 production run.
// Input: results/r1.ndjson (variant 'plain' | 'adapter'). Unit = day = w08 + w17.
// Contrasts per identity: G2 visRev60 and served RCE35 - EOHZ35 and RCE35 - NCE35; INS served and visRev60 RCE35 - EOHZ35; 2.2 metric
// list RCE35 - EOHZ35 (both configs); secondary SHRC - SHHZ served, SHRC - SHF served, SHF - SHHZ served, SHF forcedVehDecisions.
// DD_d = Delta_d(A) - Delta_d(P) with a sign test and mean +- SE. Chaos: per cell and arm |served_A - served_P|; rider-identical cells
// (identity-free fingerprint sigX equal).
// Decision rules (RUNPLAN R1): C7 REVERSED (for C1) if in A, G2 Delta visRev60 (RCE35 - EOHZ35) >= 0 on >= 5/10 days; else C7 WEAKENED if
// |relred_A - relred_P| > 15 pp (G2 visRev60 RCE35 vs EOHZ35) or |mean DD served (RCE35 - EOHZ35)| > 5/day with >= 8/10 days of the same
// DD sign (G2 or INS); else HOLDS. Predictions checked: relred within +-10 pp, |mean DD served| <= 3/day, INS reversal change <= 25 %
// relative. C5 note: SHF - SHHZ served gap changing by > 50 % between identities is reported as a finding about C5.
// Gates: P rows equal the wf15 main rows (U, EO35, EOHZ35, RCE35, SH*) and the wf14 main rows (G2 NCE35) on every metric field; INS
// NCE35 rider-identical (sig) to INS RCE35 in both identities.
'use strict';
const fs = require('fs'), path = require('path');
const L = require('./lib17.js');
const KEY = path.resolve(__dirname, '..');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const file = process.argv[2] || path.join(KEY, 'results/r1.ndjson');
const outTxt = process.argv[3] || path.join(KEY, 'results/r1_tables.txt');
const outJson = outTxt.replace(/\.txt$/, '.json');
const XS = { INS: 72, G2: 71 };
const IDS = ['plain', 'adapter'];
const LINES = [], LOG = [];
const P = s => LINES.push(s);
const rows = L.readNd(file).filter(r => r.label === 'wf17-exploratory-post-audit' && IDS.includes(r.variant));
const M = new Map();   // variant|config|arm -> Map(day -> {w08, w17})
for (const r of rows) {
  const k = [r.variant, r.config, r.arm].join('|');
  if (!M.has(k)) M.set(k, new Map());
  const dm = M.get(k); if (!dm.has(r.day)) dm.set(r.day, {});
  dm.get(r.day)[r.win] = r;
}
function dayMap(id, config, arm, fn) {
  const dm = M.get([id, config, arm].join('|')), out = new Map();
  if (!dm) { LOG.push(`no rows ${id} ${config} ${arm}`); return out; }
  for (const [d, w] of dm) { if (w.w08 && w.w17) out.set(d, fn(w.w08) + fn(w.w17)); else LOG.push(`excluded ${id} ${config} ${arm} ${d}: missing window`); }
  return out;
}
const ratioMap = (id, c, a, num, sc = 1) => { const x = dayMap(id, c, a, num), y = dayMap(id, c, a, r => r.served); return new Map([...x.keys()].filter(d => y.has(d)).map(d => [d, sc * x.get(d) / y.get(d)])); };
const has = (id, config, arm) => M.has([id, config, arm].join('|'));
const deltaMap = c => new Map(Object.entries(c.deltas));
const SUM = { label: 'wf17-exploratory-post-audit', check: 'R1', contrasts: {}, dd: {}, chaos: {}, gates: {}, labels: {}, predictions: {}, log: LOG };

const MET = {
  served: [r => r.served, 'higher'], visRev60: [r => r.visRev60, 'lower'], late60First: [r => r.late60First, 'lower'], breachDeadline60: [r => r.breachDeadline60, 'lower'],
  pDisp: [r => r.pDisp, 'lower'], pDispAny1: [r => r.pDispAny1, 'lower'], relocations: [r => r.relocations, null], switchedRiders: [r => r.switchedRiders, null],
};
const SHARE = { 'TV5/served (s)': [r => r.sumTV5 / 1000, 1], 'Pchg (pp)': [r => r.pDisp, 100], 'pDispAny1/served (pp)': [r => r.pDispAny1, 100], 'late60First/served (pp)': [r => r.late60First, 100],
  'breachDeadline60/served (pp)': [r => r.breachDeadline60, 100], 'breachLedger/served (pp) [BY-CONSTRUCTION for RC]': [r => r.breachLedger, 100] };

for (const config of ['INS', 'G2']) {
  const x = XS[config];
  const pairs = [['RCE35', 'EOHZ35'], ['RCE35', 'NCE35'], ['RCE35', 'EO35'], ['SHRC' + x, 'SHHZ' + x], ['SHRC' + x, 'SHF' + x], ['SHF' + x, 'SHHZ' + x], ['SHHZ' + x, 'EOHZ35']];
  P(`\n==================== ${config} ====================`);
  for (const [a, b] of pairs) {
    if (!IDS.every(id => has(id, config, a) && has(id, config, b))) { LOG.push(`skip ${config} ${a}-${b}: arm missing`); continue; }
    const mets = a === 'RCE35' && b === 'EOHZ35' ? Object.keys(MET).concat(Object.keys(SHARE)) : a === 'RCE35' ? ['served', 'visRev60'] : ['served', 'visRev60', 'late60First'];
    for (const mk of mets) {
      if ((mk === 'relocations' || mk === 'switchedRiders') && config === 'INS') continue;
      const per = {};
      for (const id of IDS) {
        const c = MET[mk] ? L.contrast(dayMap(id, config, a, MET[mk][0]), dayMap(id, config, b, MET[mk][0]), { better: MET[mk][1] })
          : L.contrast(ratioMap(id, config, a, SHARE[mk][0], SHARE[mk][1]), ratioMap(id, config, b, SHARE[mk][0], SHARE[mk][1]), { better: 'lower', count: false });
        per[id] = c;
        SUM.contrasts[`${config}|${a}-${b}|${mk}|${id}`] = c;
      }
      const dd = L.contrast(deltaMap(per.adapter), deltaMap(per.plain), { better: null, count: false });
      SUM.dd[`${config}|${a}-${b}|${mk}`] = dd;
      P(`${config} ${a}-${b} ${mk}`);
      for (const id of IDS) P(`   ${id.padEnd(8)} ${L.fmtC(per[id])}`);
      P(`   DD(A-P)  mean ${L.f(dd.mean)} SE ${L.f(dd.se)} [${L.f(dd.lo)}, ${L.f(dd.hi)}] med ${L.f(dd.median)} | ${dd.neg}-/${dd.pos}+/${dd.ties}= sign p ${L.fp(dd.p)}` + (MET[mk] && MET[mk][0] && per.plain.rel !== null ? ` | rel P ${L.f(100 * per.plain.rel, 1)}% A ${L.f(100 * per.adapter.rel, 1)}% (diff ${L.f(100 * (per.adapter.rel - per.plain.rel), 1)} pp)` : ''));
    }
  }
  // SHF forced decisions (levels per identity)
  const shf = 'SHF' + x;
  if (IDS.every(id => has(id, config, shf))) for (const id of IDS) { const f = L.mean([...dayMap(id, config, shf, r => r.forcedVehDecisions).values()]); SUM.contrasts[`${config}|${shf}|forced|${id}`] = f; P(`${config} ${shf} forcedVehDecisions/day ${id} ${L.f(f, 1)}`); }
  // chaos per cell and arm
  const arms = [...new Set(rows.filter(r => r.config === config).map(r => r.arm))].sort();
  P(`${config} chaos per cell (|served_A - served_P|; rider-identical cells by sigX):`);
  for (const arm of arms) {
    const cells = new Map();
    for (const r of rows.filter(r => r.config === config && r.arm === arm)) { if (!cells.has(r.cell)) cells.set(r.cell, {}); cells.get(r.cell)[r.variant] = r; }
    const d = [], same = [];
    for (const [, v] of cells) if (v.plain && v.adapter) { d.push(Math.abs(v.adapter.served - v.plain.served)); same.push(v.adapter.sigX === v.plain.sigX); }
    const ch = { cells: d.length, meanAbs: L.mean(d), medianAbs: L.median(d), maxAbs: Math.max(...d), identical: same.filter(Boolean).length,
      tieBreaksP: L.mean(rows.filter(r => r.config === config && r.arm === arm && r.variant === 'plain').map(r => r.diagTieBreaks)),
      tieBreaksA: L.mean(rows.filter(r => r.config === config && r.arm === arm && r.variant === 'adapter').map(r => r.diagTieBreaks)) };
    SUM.chaos[`${config}|${arm}`] = ch;
    P(`   ${arm.padEnd(8)} cells ${ch.cells} mean |dServed| ${L.f(ch.meanAbs)} median ${L.f(ch.medianAbs, 1)} max ${ch.maxAbs} | rider-identical ${ch.identical}/${ch.cells} | identity tie-breaks/run P ${L.f(ch.tieBreaksP, 1)} A ${L.f(ch.tieBreaksA, 1)}`);
  }
}

// ---------------------------------------------------------------- gates
const W15 = L.readNd(path.join(SP, 'wf15/results/confirm/main.ndjson')), W14 = L.readNd(path.join(SP, 'wf14/results/main.ndjson'));
const IGN = new Set(['part', 'set', 'engine', 'ms', 'msScore', 'label', 'variant']);
let gN = 0, gEq = 0; const gBad = [];
for (const r of rows.filter(r => r.variant === 'plain')) {
  const src = r.arm === 'NCE35' ? (r.config === 'G2' ? W14 : null) : W15;
  if (!src) continue;
  const ref = src.find(x => x.cell === r.cell && x.config === r.config && x.arm === r.arm);
  gN++;
  if (!ref) { gBad.push([r.cell, r.config, r.arm, 'no ref']); continue; }
  const bad = Object.keys(ref).filter(k => !IGN.has(k) && JSON.stringify(ref[k]) !== JSON.stringify(r[k]));
  if (bad.length) gBad.push([r.cell, r.config, r.arm, bad.slice(0, 5)]); else gEq++;
}
SUM.gates.plainEqualsWf15Wf14 = { compared: gN, equal: gEq, pass: gN > 0 && gN === gEq, bad: gBad.slice(0, 10) };
const nce = {};
for (const id of IDS) {
  let n = 0, same = 0;
  for (const r of rows.filter(r => r.variant === id && r.config === 'INS' && r.arm === 'NCE35')) { const b = rows.find(x => x.variant === id && x.config === 'INS' && x.arm === 'RCE35' && x.cell === r.cell); n++; if (b && b.sig === r.sig) same++; }
  nce[id] = { compared: n, riderIdentical: same, pass: n > 0 && n === same };
}
SUM.gates.insNceEqualsRce = nce;
P(`\n==================== Gates ====================\nP rows = wf15 main (U, EO35, EOHZ35, RCE35, SH*) / wf14 main (G2 NCE35): ${gEq}/${gN} equal${gBad.length ? ' MISMATCH ' + JSON.stringify(gBad.slice(0, 5)) : ''}\nINS NCE35 rider-identical to INS RCE35: ${JSON.stringify(nce)}`);

// ---------------------------------------------------------------- decision labels and predictions
const g = k => SUM.contrasts[k];
const vP = g('G2|RCE35-EOHZ35|visRev60|plain'), vA = g('G2|RCE35-EOHZ35|visRev60|adapter');
let c7 = 'INCONCLUSIVE (missing data)';
const ddS = { G2: SUM.dd['G2|RCE35-EOHZ35|served'], INS: SUM.dd['INS|RCE35-EOHZ35|served'] };
if (vP && vA && vA.nDays && ddS.G2 && ddS.INS) {
  const relDiff = Math.abs(vA.rel - vP.rel);
  const servedFlip = ['G2', 'INS'].some(c => Math.abs(ddS[c].mean) > 5 && Math.max(ddS[c].neg, ddS[c].pos) >= 8);
  if (vA.pos + vA.ties >= 5) c7 = 'REVERSED (for C1)';
  else if (relDiff > 0.15 || servedFlip) c7 = 'WEAKENED';
  else c7 = 'HOLDS';
  SUM.predictions = {
    relredWithin10pp: relDiff <= 0.10, relredP: vP.rel, relredA: vA.rel, relDiffPp: 100 * relDiff,
    ddServedWithin3: { G2: Math.abs(ddS.G2.mean) <= 3, INS: Math.abs(ddS.INS.mean) <= 3, G2mean: ddS.G2.mean, INSmean: ddS.INS.mean },
    insReversalChangeLe25pct: ['plain', 'adapter'].map(id => { const c = g(`INS|RCE35-EOHZ35|visRev60|${id}`); return { id, rel: c.rel, ok: c.rel === null ? null : Math.abs(c.rel) <= 0.25 }; }),
  };
}
SUM.labels.C7 = c7;
const gapP = g('INS|SHF72-SHHZ72|served|plain'), gapA = g('INS|SHF72-SHHZ72|served|adapter'), gapP2 = g('G2|SHF71-SHHZ71|served|plain'), gapA2 = g('G2|SHF71-SHHZ71|served|adapter');
const c5 = {};
for (const [c, p, a] of [['INS', gapP, gapA], ['G2', gapP2, gapA2]]) if (p && a) c5[c] = { gapP: p.mean, gapA: a.mean, changePct: 100 * (a.mean - p.mean) / Math.abs(p.mean), finding: Math.abs(a.mean - p.mean) > 0.5 * Math.abs(p.mean) };
SUM.labels.C5note = c5;
P(`\n==================== Decision labels ====================\nC7 (identity mode does not change the effects): ${c7}\npredictions: ${JSON.stringify(SUM.predictions)}\nC5 note (SHF - SHHZ served gap, per day, P vs A; finding if change > 50 %): ${JSON.stringify(c5)}`);
P('\n==================== Log ====================');
for (const l of [...new Set(LOG)]) P(l);
P(`rows ${rows.length}`);
fs.writeFileSync(outTxt, `wf17 R1 (wf17-exploratory-post-audit) - identity mode. Generated ${new Date().toISOString()} by analysis/r1_analyze.js (sha256 ${L.sha256File(__filename)})\n` + LINES.join('\n') + '\n');
fs.writeFileSync(outJson, JSON.stringify(SUM, (k, v) => (k === 'days' ? undefined : v), 1));
console.log(LINES.filter(l => /^C7|^C5 note|^P rows|^INS NCE35|^predictions/.test(l)).join('\n'));
