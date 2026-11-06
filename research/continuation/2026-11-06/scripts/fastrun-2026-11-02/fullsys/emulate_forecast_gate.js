// ADR-089 GĐ4 forecast gate (sealed before the RideBound job): the closed-loop forecast alone, without Shadow.
// Arm FS-G2-RCE35-CL = G2 k8 + Rule C with Elmi's budget (RCE35) + CL forecast (prev4 profile of the day/window, clipPickup),
// on the full-node F14 cells d20190919-vf-r0-w08 and -w17, in adapter mode with the integer-ms leg rule
// (engine_wf15_clint.js). Same rider columns as emulate_via_full.js.
// Usage: node emulate_forecast_gate.js <data root> <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const FS = require('./emulate_fullsys.js');
const ECL = require('./engine_wf15_clint.js');
const FC = require('E:/RideBoundData/company-intake/2026-10-23/sp/wf20/fullsys/code/scripts/forecast.js');
const INDEX = JSON.parse(fs.readFileSync('E:/RideBoundData/company-intake/2026-10-23/sp/wf7/c-forecast/results/accuracy-index-2019.json', 'utf8'));
const K8 = { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' };
const ROLE = 'FS-G2-RCE35-CL';

const [root, out] = process.argv.slice(2);
if (!root || !out || fs.existsSync(out)) { console.error('usage / output exists'); process.exit(2); }
const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared'];
const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tclippedInitial\tclippedRevision\tms'];
const profiles = {};
for (const [day, win] of [['2019-09-19', 'w08'], ['2019-09-19', 'w17']]) {
  const cell = `d${day.replace(/-/g, '')}-vf-r0-${win}`;
  const P = FS.prepareViaCell(cell, root);
  const prof = FC.profile(day, win === 'w08' ? 32 : 68, 'prev4', INDEX, 15);
  if (!prof) throw new Error('no forecast sources');
  profiles[cell] = prof;
  const t = Date.now();
  const res = ECL.simulate(P, { ...JSON.parse(JSON.stringify(K8)), arm: { name: 'RCE', alpha: 0.35 }, forecast: { prof, clipPickup: true } });
  const ms = Date.now() - t;
  for (const r of res.riders) {
    lines.push([ROLE, cell, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0, r.S, r.k, r.net, r.M, r.shared ? 1 : 0].join('\t'));
  }
  const d = res.diag || {}, fc = res.fcCount || {};
  counts.push([ROLE, cell, res.riders.length, res.riders.filter(r => r.served).length, res.rejected, d.relocations, d.genDecisions, fc.clippedInitial, fc.clippedRevision, ms].join('\t'));
  console.log(`${ROLE} ${cell}: riders ${res.riders.length}, served ${res.riders.filter(r => r.served).length}, relocations ${d.relocations}, clippedRevision ${fc.clippedRevision}, ${ms} ms`);
}
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-counts.tsv'), counts.join('\n') + '\n', { flag: 'wx' });
fs.writeFileSync(out.replace(/\.tsv$/, '-profiles.json'), JSON.stringify(profiles), { flag: 'wx' });
console.log(`wrote ${out}: ${lines.length - 1} rider rows`);
