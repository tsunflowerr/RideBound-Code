// wf17 r2-penalty-regime POST-HOC (written after reading the R4 tables; descriptive only, no decision label; see DEVIATIONS 16:18).
// Question: how much of Rule C's G2 reversal cut could a plain relocation penalty buy? Paired day contrasts across configs on the same
// D60 cells: X(arm a, config v) - X(arm b, config ref). Unit = day (w08 + w17, r0). Same statistics as the pre-specified analysis.
'use strict';
const path = require('path'), fs = require('fs');
const L = require('./lib17.js'), G = require('./agg17.js');
const KEY = path.resolve(__dirname, '..');
const rows = L.readNd(path.join(KEY, 'results/r4.ndjson'));
const AGG = {};
for (const v of ['T8P0', 'T8P60', 'T8P300', 'T16P0', 'T16P60', 'T16P300']) AGG[v] = G.dayAgg(rows.filter(r => r.variant === v), [], v);
const mapOf = (v, arm, m) => new Map([...AGG[v].get(arm).entries()].map(([d, x]) => [d, G.valOf(m, x)]));
const M = Object.fromEntries(G.METRICS.map(m => [m.id, m]));
const out = [], J = {};
out.push('wf17 R4 POST-HOC (descriptive; decided after reading R4): cross-config paired contrasts, D60, unit = day');
for (const T of ['T8', 'T16']) for (const [a, va, b, vb] of [
  ['EOHZ35', `${T}P60`, 'EOHZ35', `${T}P0`], ['EOHZ35', `${T}P300`, 'EOHZ35', `${T}P0`],
  ['RCE35', `${T}P0`, 'EOHZ35', `${T}P0`], ['RCE35', `${T}P300`, 'EOHZ35', `${T}P0`], ['RCE35', `${T}P300`, 'EOHZ35', `${T}P300`],
  ['RCE35', `${T}P300`, 'RCE35', `${T}P0`], ['U', `${T}P300`, 'U', `${T}P0`]]) {
  const k = `${a}@${va} - ${b}@${vb}`; J[k] = {};
  out.push(`--- ${k}`);
  for (const id of ['served', 'visRev60', 'visRev60_per1000', 'TV5_s_per_served', 'Pchg_pp', 'late60First_pp', 'breachDeadline60_pp', 'breachDeadline60', 'late60First', 'relocations']) {
    const c = L.contrast(mapOf(va, a, M[id]), mapOf(vb, b, M[id]), { better: M[id].better, count: M[id].kind === 'count' });
    const { days, deltas, ...rest } = c; J[k][id] = rest;
    out.push(`   ${id.padEnd(20)} ${L.fmtC(c, { k: M[id].kind === 'count' ? 2 : 3 })}`);
  }
}
fs.writeFileSync(path.join(KEY, 'results/r4_posthoc.txt'), out.join('\n') + '\n');
fs.writeFileSync(path.join(KEY, 'results/r4_posthoc.json'), JSON.stringify({ label: 'wf17-exploratory-post-audit', status: 'POST-HOC descriptive', contrasts: J }, null, 1));
console.log(out.join('\n'));
