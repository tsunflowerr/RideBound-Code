// wf21/n7 pre-plan evidence (promise length ONLY): mean initial promise length (p0 - t0) of SERVED riders in wf17 R3 dumps,
// modes N / CL / CLF, arms EOHZ35 / RCE35, by panel x config x window. Reads no outcome field (A, pLast are ignored).
'use strict';
const fs = require('fs'), rl = require('readline');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const f = SP + '/wf17/r1-identity-fence-pad/dumps/r3.dump.ndjson';
const acc = {};
const it = rl.createInterface({ input: fs.createReadStream(f) });
it.on('line', l => { if (!l) return; const d = JSON.parse(l); if (!['EOHZ35', 'RCE35'].includes(d.arm)) return;
  const iT = d.fields.indexOf('t0'), iP = d.fields.indexOf('p0'); const w = d.cell.slice(11, 14);
  const k = [d.panel, d.config, w, d.arm, d.mode, d.flatRatio === null ? '' : d.flatRatio].join('|'); const a = (acc[k] ||= { n: 0, s: 0 });
  for (const r of d.riders) { a.n++; a.s += r[iP] - r[iT]; } });
it.on('close', () => { const out = [];
  for (const k of Object.keys(acc).sort()) out.push(k.padEnd(40) + ' n ' + String(acc[k].n).padStart(6) + '  meanPL_s ' + (acc[k].s / acc[k].n / 1000).toFixed(2));
  console.log(out.join('\n')); });
