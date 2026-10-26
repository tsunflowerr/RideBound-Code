// wf17 R7 cell checks (EXPLORATORY, POST-AUDIT; RUNPLAN R7). No simulation outcome is read here.
//  (1) G-GEN on 20 cells: gen17.js with default tags (cells/ggen) reproduces the wf14 D10 r0 cells byte for byte
//      (scenario JSON, matrix .bin, provenance .prov.json sha256 identical).
//  (2) alt node draw (cells/alt1, --nodetag wf17-r7-alt1): same request ids / arrival times / pickup and drop-off zones as wf14 (sample
//      seed unchanged); share of requests whose origin / destination node changed; fleet size; scenario.js validation.
//  (3) Sanity: per cell, median direct model time (provenance directModel_s) of alt vs wf14; RUNPLAN: change < 10 % in every cell,
//      otherwise reported and the analysis still runs.
// Usage: node r7_cells_check.js  -> work/r7_cells_check.json and a printed summary
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const KEY = path.resolve(__dirname, '..');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const SC = require(path.join(KEY, 'code/runner/scenario.js'));
const W14 = path.join(SP, 'wf14/scenarios');
const sha = f => crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const med = a => { const s = a.slice().sort((x, y) => x - y), n = s.length; return n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2; };
const cells = fs.readFileSync(path.join(KEY, 'work/cells_D10_r0.txt'), 'utf8').trim().split(',');
const out = { label: 'wf17-exploratory-post-audit', check: 'R7 cells', at: new Date().toISOString(), ggen: [], alt: [] };
const trip = (base, cell) => ({ scen: path.join(base, 'weekdays-P512', cell + '.json'), mat: path.join(base, 'matrices', cell + '-n512.bin'), prov: path.join(base, 'provenance', cell + '-n512.prov.json') });
for (const cell of cells) {
  const a = trip(path.join(KEY, 'cells/ggen'), cell), b = trip(W14, cell);
  const r = { cell };
  for (const k of ['scen', 'mat', 'prov']) r[k] = fs.existsSync(a[k]) && sha(a[k]) === sha(b[k]);
  r.pass = r.scen && r.mat && r.prov;
  out.ggen.push(r);
}
for (const cell of cells) {
  const a = trip(path.join(KEY, 'cells/alt1'), cell), b = trip(W14, cell);
  const sa = JSON.parse(fs.readFileSync(a.scen, 'utf8')), sb = JSON.parse(fs.readFileSync(b.scen, 'utf8'));
  const pa = JSON.parse(fs.readFileSync(a.prov, 'utf8')), pb = JSON.parse(fs.readFileSync(b.prov, 'utf8'));
  let valid = true; try { SC.validateScenario(sa); } catch (e) { valid = String(e.message || e); }
  const sameReq = sa.requests.length === sb.requests.length && sa.requests.every((q, i) => q.id === sb.requests[i].id && q.arrivalMs === sb.requests[i].arrivalMs);
  const zb = new Map(pb.requests.map(q => [q.id, q]));
  const sameZones = pa.requests.every(q => zb.has(q.id) && zb.get(q.id).pu === q.pu && zb.get(q.id).dz === q.dz);
  const oChg = sa.requests.filter((q, i) => q.origin !== sb.requests[i].origin).length / sa.requests.length;
  const dChg = sa.requests.filter((q, i) => q.destination !== sb.requests[i].destination).length / sa.requests.length;
  const mA = med(pa.requests.map(q => q.directModel_s)), mB = med(pb.requests.map(q => q.directModel_s));
  const fleetChg = sa.fleet.filter((f, i) => f.startNode !== sb.fleet[i].startNode).length;
  out.alt.push({ cell, valid, sameRequestIdsAndArrivals: sameReq, sameZones, originNodeChangedShare: +oChg.toFixed(4), destNodeChangedShare: +dChg.toFixed(4),
    medDirectAlt_s: mA, medDirectWf14_s: mB, medDirectRelChange: +((mA - mB) / mB).toFixed(4), within10pct: Math.abs(mA - mB) / mB < 0.10,
    fleet: sa.fleet.length, fleetStartNodesChanged: fleetChg, nodes: sa.nodes.length, nodesWf14: sb.nodes.length, kappaSame: pa.travel.F_windowStart === pb.travel.F_windowStart });
}
out.ggenPass = out.ggen.length === 20 && out.ggen.every(r => r.pass);
out.altAllValid = out.alt.every(r => r.valid === true && r.sameRequestIdsAndArrivals && r.sameZones && r.kappaSame && r.fleet === 96);
out.sanityAllWithin10pct = out.alt.every(r => r.within10pct);
fs.writeFileSync(path.join(KEY, 'work/r7_cells_check.json'), JSON.stringify(out, null, 1));
console.log(`G-GEN (20 cells, default tags = wf14 bytes): ${out.ggen.filter(r => r.pass).length}/${out.ggen.length} pass`);
console.log(`alt cells valid / same requests, zones, kappa, fleet 96: ${out.altAllValid}`);
console.log(`sanity |median direct change| < 10 %: ${out.alt.filter(r => r.within10pct).length}/${out.alt.length}; range ${Math.min(...out.alt.map(r => r.medDirectRelChange))} .. ${Math.max(...out.alt.map(r => r.medDirectRelChange))}`);
console.log(`origin node changed share: ${Math.min(...out.alt.map(r => r.originNodeChangedShare))} .. ${Math.max(...out.alt.map(r => r.originNodeChangedShare))}; dest: ${Math.min(...out.alt.map(r => r.destNodeChangedShare))} .. ${Math.max(...out.alt.map(r => r.destNodeChangedShare))}`);
