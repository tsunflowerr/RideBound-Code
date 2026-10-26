// wf20/fullsys: mechanical application of PLAN.md §5 labels to results/tables.json (label wf20-fullsys-exploratory-post-audit).
// Written DURING the production run, before any production result was read (see DEVIATIONS.md D1). Labels: HOLDS, STRONGER,
// WEAKENED, REVERSED, INCONCLUSIVE (PLAN §5). Pooled, F14 and F16 are labelled separately.
// Usage: node labels.js --tables <tables.json> --out <labels.txt>
'use strict';
const fs = require('fs');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const T = JSON.parse(fs.readFileSync(arg('tables'), 'utf8'));
const out = [], P = s => { out.push(s); console.log(s); };
const get = (id, key, pset, did) => { const r = (did ? T.did : T.contrasts)[id]; return r && r[pset] && r[pset].R[key] ? r[pset].R[key] : null; };
const share = (s, side) => { const n = s.nPlus + s.nMinus; return n ? (side > 0 ? s.nPlus : s.nMinus) / n : 0; };
// directional prediction: band [lo, hi] on value v (mean, or rel*100 when useRel), predicted side, day share q, flip(s) -> bool
function dir(s, { lo, hi, side, q = 0, flip, useRel = false }) {
  const v = useRel ? 100 * s.rel : s.mean;
  if (flip(s, v)) return 'REVERSED';
  const sh = share(s, side), okDays = sh >= q;
  const inBand = v >= lo && v <= hi;
  const far = side < 0 ? v < lo : v > hi;
  if (inBand && okDays) return 'HOLDS';
  if (far && okDays) return 'STRONGER';
  if (Math.sign(v) === side) return 'WEAKENED';
  return 'INCONCLUSIVE';
}
function two(s, { lo, hi, flip, useRel = false }) {
  const v = useRel ? 100 * s.rel : s.mean;
  if (v >= lo && v <= hi) return 'HOLDS';
  return flip(s, v) ? 'REVERSED' : 'INCONCLUSIVE';
}
const sig = s => s.p < 0.05;
const plusSig = s => s.nPlus > s.nMinus && sig(s), minusSig = s => s.nMinus > s.nPlus && sig(s);
const fmt = (s, useRel) => (s ? `${useRel ? (100 * s.rel).toFixed(1) + '%' : s.mean.toFixed(2)} (±${s.se.toFixed(2)}; ${s.nMinus}-/${s.nPlus}+; p ${s.p < 0.001 ? s.p.toExponential(1) : s.p.toFixed(3)})` : 'n/a');
const items = [];
const add = (pid, what, id, key, kind, o, did = false) => items.push({ pid, what, id, key, kind, o, did });
const G2M = 'MAIN G2: SHRC_CL - SHHZ_CL', INSM = 'MAIN INS: SHRC_CL - SHHZ_CL';
add('P1', 'G2 MAIN visRev60/day', G2M, 'visRev60|day', 'dir', { lo: -16, hi: -10, side: -1, q: 0.8, flip: (s, v) => v >= 0 || plusSig(s) });
add('P1r', 'G2 MAIN visRev60 rel %', G2M, 'visRev60|day', 'dir', { lo: -40, hi: -25, side: -1, q: 0.8, useRel: true, flip: (s, v) => v >= 0 || plusSig(s) });
add('P2', 'G2 MAIN served/day', G2M, 'served|day', 'dir', { lo: -14, hi: -6, side: -1, q: 0.7, flip: (s, v) => v >= 0 && s.nPlus >= s.nMinus });
add('P3', 'G2 MAIN late60First/1000', G2M, 'late60First/1000|day', 'two', { lo: -3, hi: 6, flip: (s, v) => v <= -6 && minusSig(s) });
add('P4', 'G2 MAIN Pchg/1000', G2M, 'Pchg/1000|day', 'two', { lo: -10, hi: 3, flip: (s, v) => v >= 5 && plusSig(s) });
add('P5', 'G2 MAIN TV5 s/served', G2M, 'TV5 s/served|day', 'dir', { lo: -22, hi: -10, side: -1, flip: (s, v) => v >= 0 });
add('P6', 'G2 MAIN breachDeadline60/1000', G2M, 'breachDeadline60/1000|day', 'two', { lo: -3, hi: 2, flip: (s, v) => sig(s) });
add('P7', 'G2 MAIN G_late60First/day', G2M, 'G_late60First|day', 'two', { lo: -2.5, hi: 1, flip: (s, v) => v >= 2 && plusSig(s) });
add('P8', 'INS MAIN visRev60/day', INSM, 'visRev60|day', 'two', { lo: -2.5, hi: 0.5, flip: (s, v) => 100 * s.rel <= -20 && share(s, -1) >= 0.75 });
add('P9', 'INS MAIN served/day', INSM, 'served|day', 'dir', { lo: -8, hi: -2, side: -1, flip: (s, v) => v >= 2 && plusSig(s) });
add('P10a', 'INS MAIN late60First/1000', INSM, 'late60First/1000|day', 'two', { lo: -4, hi: 4, flip: (s) => sig(s) });
add('P10b', 'INS MAIN Pchg/1000', INSM, 'Pchg/1000|day', 'two', { lo: -4, hi: 4, flip: (s) => sig(s) });
add('P10c', 'INS MAIN TV5 s/served', INSM, 'TV5 s/served|day', 'two', { lo: -3, hi: 3, flip: (s) => sig(s) });
for (const c of ['INS', 'G2']) for (const a of ['SHF', 'SHHZ', 'SHRC']) {
  const id = `FC ${c}: ${a}_CL - ${a}_N`, t = `${c} ${a} CL-N`;
  add('P11', t + ' late60First/1000 day', id, 'late60First/1000|day', 'dir', { lo: -50, hi: -25, side: -1, q: 0.8, flip: (s, v) => v >= 0 || share(s, -1) < 0.6 });
  add('P12', t + ' late60First/1000 w08', id, 'late60First/1000|w08', 'dir', { lo: -140, hi: -80, side: -1, flip: (s, v) => v >= 0 });
  add('P13', t + ' late60First/1000 w17', id, 'late60First/1000|w17', 'dir', { lo: 20, hi: 45, side: 1, q: 0.75, flip: (s, v) => v <= 0 || share(s, 1) < 0.5 });
  add('P14', t + ' visRev60 rel %', id, 'visRev60|day', 'dir', { lo: -45, hi: -20, side: -1, q: 0.75, useRel: true, flip: (s) => s.mean >= 0 });
  add('P15', t + ' TV5 s/served', id, 'TV5 s/served|day', 'dir', { lo: -25, hi: -10, side: -1, flip: (s, v) => v >= 0 });
  add('P16', t + ' breachDeadline60 rel %', id, 'breachDeadline60|day', 'dir', { lo: -80, hi: -30, side: -1, useRel: true, flip: (s) => s.mean >= 0 });
  add('P17a', t + ' Pchg/1000', id, 'Pchg/1000|day', 'two', { lo: -5, hi: 5, flip: (s) => sig(s) });
  add('P17b', t + ' served/day', id, 'served|day', 'two', { lo: -4, hi: 4, flip: (s) => sig(s) });
  for (const sc of ['day', 'w08', 'w17']) add('P18', t + ' guaranteedShare pp ' + sc, id, 'guaranteedShare %|' + sc, 'two', { lo: -3, hi: 3, flip: (s, v) => Math.abs(v) > 5 });
}
add('P19', 'DiD G2 visRev60/day', 'DiD G2: (SHRC-SHHZ)_CL - (SHRC-SHHZ)_N', 'visRev60|day', 'two', { lo: 0, hi: 3, flip: (s, v) => v <= -3 && minusSig(s) }, true);
const BA = 'SYS b-a: full system (SHRC+G2+CL) - Elmi as in paper (SHHZ+INS+N)', BD = 'SYS b-d: full system - RCE35+G2+CL (what Shadow adds)';
add('P20', 'SYS b-a served/day', BA, 'served|day', 'two', { lo: -6, hi: 6, flip: () => false });
add('P21', 'SYS b-a visRev60/day', BA, 'visRev60|day', 'dir', { lo: -10, hi: -2, side: -1, flip: (s, v) => v >= 0 });
add('P22a', 'SYS b-a late60First/1000 day', BA, 'late60First/1000|day', 'dir', { lo: -45, hi: -15, side: -1, flip: (s, v) => v >= 0 });
add('P22b', 'SYS b-a late60First/1000 w17', BA, 'late60First/1000|w17', 'dir', { lo: 15, hi: 50, side: 1, flip: (s, v) => v <= 0 });
add('P23a', 'SYS b-a Pchg/1000', BA, 'Pchg/1000|day', 'two', { lo: -5, hi: 12, flip: (s) => sig(s) });
add('P23b', 'SYS b-a TV5 s/served', BA, 'TV5 s/served|day', 'dir', { lo: -20, hi: -1, side: -1, flip: (s, v) => v >= 0 });
add('P25a', 'SYS b-d served/day', BD, 'served|day', 'dir', { lo: -15, hi: -4, side: -1, flip: (s) => plusSig(s) });
add('P25b', 'SYS b-d visRev60/day', BD, 'visRev60|day', 'dir', { lo: 6, hi: 16, side: 1, flip: (s) => minusSig(s) });
add('P25c', 'SYS b-d late60First/1000', BD, 'late60First/1000|day', 'dir', { lo: -80, hi: -40, side: -1, flip: (s) => plusSig(s) });
add('P25d', 'SYS b-d Pchg/1000', BD, 'Pchg/1000|day', 'dir', { lo: -115, hi: -80, side: -1, flip: (s) => plusSig(s) });
add('P25e', 'SYS b-d TV5 s/served', BD, 'TV5 s/served|day', 'dir', { lo: 70, hi: 110, side: 1, flip: (s) => minusSig(s) });
add('P25f', 'SYS b-d breachDeadline60/1000', BD, 'breachDeadline60/1000|day', 'dir', { lo: 5, hi: 30, side: 1, flip: (s) => minusSig(s) });
for (const c of ['INS', 'G2']) add('P26', c + ' SHRC_CL - SHF_CL served/day', `${c}: SHRC_CL - SHF_CL`, 'served|day', 'dir', { lo: 25, hi: 40, side: 1, flip: (s, v) => v <= 10 });

P('wf20-fullsys-exploratory-post-audit (thăm dò sau kiểm toán): PLAN §5 labels, applied mechanically by analysis/labels.js');
P('format: label pooled | F14 | F16 ; value = mean (or rel %) (±SE; days −/+; sign-test p)');
const tally = {};
for (const it of items) {
  const cells = ['pooled', 'F14', 'F16'].map(ps => {
    const s = get(it.id, it.key, ps, it.did);
    if (!s) return { lab: 'n/a', txt: 'n/a' };
    if (it.o.useRel && (s.rel === null || s.rel === undefined)) return { lab: 'n/a', txt: 'no rel' };
    const lab = it.kind === 'dir' ? dir(s, it.o) : two(s, it.o);
    return { lab, txt: fmt(s, it.o.useRel) };
  });
  tally[cells[0].lab] = (tally[cells[0].lab] || 0) + 1;
  P(`${it.pid.padEnd(5)} ${it.what.padEnd(42)} band [${it.o.lo}, ${it.o.hi}]  ${cells.map((c, i) => ['pooled', 'F14', 'F16'][i] + ' ' + c.lab + ' ' + c.txt).join(' | ')}`);
}
P('pooled label tally: ' + JSON.stringify(tally));
fs.writeFileSync(arg('out'), out.join('\n') + '\n');
