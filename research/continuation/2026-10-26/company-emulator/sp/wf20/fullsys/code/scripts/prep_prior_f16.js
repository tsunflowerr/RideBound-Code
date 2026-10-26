// wf20/fullsys (label wf20-fullsys-exploratory-post-audit): the Shadow lambda prior (preceding-hour zone counts, input data, no
// outcome) for the 16 F16 days, which wf15 never exported. Same SQL template, DuckDB binary, data dir and zone list as
// wf15/scripts/prep_data.js; only __DAYS__ and __OUT__ differ.
//   gate g0a: re-export wf15's own day list (wf15/work/days_all.csv) into work/g0 -> sha256 must equal wf15's prior
//             (d40e2f3b... = wf15/frozen/shadow-params.json prior.csvSha256)
//   F16:      export work/prior_F16/prior_prevhour.csv; checks: 32 day-window keys, O total == D total, every F16 cell's
//             provenance pu/dz equal the node zones of its request nodes (as prep_data.js)
// Usage: node prep_prior_f16.js   (writes work/prior_report.txt)
'use strict';
const fs = require('fs'), path = require('path'), cp = require('child_process');
const LIB = require('./shlib.js');
const NETF = require('../runner/netfull.js');

const W = path.resolve(__dirname, '..', '..');                // wf20/fullsys
const WORK = path.join(W, 'work');
const DUCK = path.join(LIB.SP, 'tools/duckdb-v1.5.5-sd/duckdb.exe');
const DATA = path.join(LIB.SP, 'wf6/data/via-many-days');
const ZONES = path.join(LIB.WF, 'work/zones.csv');
const TMPL = path.join(LIB.WF, 'scripts/export_prior.sql.tmpl');
const WF15_PRIOR_SHA = JSON.parse(fs.readFileSync(path.join(LIB.WF, 'frozen/shadow-params.json'), 'utf8')).prior.csvSha256;
const F16DIR = path.join(LIB.SP, 'wf17/r1-identity-fence-pad/cells/f16/weekdays-P512');
const PANELS = JSON.parse(fs.readFileSync(path.join(LIB.SP, 'wf17/r1-identity-fence-pad/work/panels.json'), 'utf8'));
const fwd = p => p.replace(/\\/g, '/');
const out = [];
const P = s => { out.push(s); console.log(s); };

function exportPrior(daysCsv, outDir) {
  fs.mkdirSync(outDir, { recursive: true });
  const sql = fs.readFileSync(TMPL, 'utf8').split('__DATA__').join(fwd(DATA)).split('__DAYS__').join(fwd(daysCsv))
    .split('__ZONES__').join(fwd(ZONES)).split('__OUT__').join(fwd(outDir));
  fs.writeFileSync(path.join(outDir, 'export_prior.sql'), sql);
  const r = cp.spawnSync(DUCK, [], { input: sql, encoding: 'utf8', maxBuffer: 1 << 26 });
  if (r.status !== 0) { console.error(r.stdout, r.stderr); throw new Error('duckdb failed'); }
  return path.join(outDir, 'prior_prevhour.csv');
}

const NET = NETF.build();
P('wf20-fullsys-exploratory-post-audit: prior export ' + new Date().toISOString());
// g0a
const g0 = exportPrior(path.join(LIB.WF, 'work/days_all.csv'), path.join(WORK, 'g0'));
const shaG0 = LIB.sha256File(g0);
P(`g0a re-export of wf15 days_all.csv: sha256 ${shaG0}; wf15 ${WF15_PRIOR_SHA}; ${shaG0 === WF15_PRIOR_SHA ? 'PASS' : 'FAIL'}`);
if (shaG0 !== WF15_PRIOR_SHA) { fs.writeFileSync(path.join(WORK, 'prior_report.txt'), out.join('\n') + '\n'); process.exit(1); }
// F16
const days = PANELS.F16.slice().sort();
const daysCsv = path.join(WORK, 'days_F16.csv');
fs.writeFileSync(daysCsv, 'day\n' + days.join('\n') + '\n');
const f16 = exportPrior(daysCsv, path.join(WORK, 'prior_F16'));
const prior = LIB.loadPrior(f16, NET.zoneIds);
P(`F16 prior ${fwd(f16)}: sha256 ${LIB.sha256File(f16)}; day-window keys ${prior.size} (want ${days.length * 2})`);
const cells = LIB.listCells({ scen: [F16DIR] });
let missing = 0, mism = 0, zoneMism = 0;
const tot = [];
for (const c of cells) {
  const pr = prior.get(c.day + '|' + c.win);
  if (!pr) { missing++; P('NO PRIOR ' + c.cell); continue; }
  const sO = pr.O.reduce((a, b) => a + b, 0), sD = pr.D.reduce((a, b) => a + b, 0);
  if (sO !== sD) mism++;
  if (c.rs === 'r0') tot.push(sO);
  const s = JSON.parse(fs.readFileSync(c.file, 'utf8'));
  const prov = JSON.parse(fs.readFileSync(path.resolve(path.dirname(c.file), s.provenance.provenanceFile), 'utf8'));
  const byId = new Map(prov.requests.map(q => [q.id, q]));
  for (const q of s.requests) {
    const pq = byId.get(q.id), go = Number(q.origin.slice(1)), gd = Number(q.destination.slice(1));
    if (!pq || NET.zoneIds[NET.zoneIdx[go]] !== pq.pu || NET.zoneIds[NET.zoneIdx[gd]] !== pq.dz) zoneMism++;
  }
}
tot.sort((a, b) => a - b);
P(`F16 cells ${cells.length}; cells without prior ${missing}; O/D total mismatch ${mism}; request zone mismatches ${zoneMism}`);
P(`F16 preceding-hour eligible requests per window: min ${tot[0]}, median ${tot[Math.floor(tot.length / 2)]}, max ${tot[tot.length - 1]}`);
const ok = prior.size === days.length * 2 && missing === 0 && mism === 0 && zoneMism === 0;
P('g0b F16 prior checks: ' + (ok ? 'PASS' : 'FAIL'));
fs.writeFileSync(path.join(WORK, 'prior_report.txt'), out.join('\n') + '\n');
process.exit(ok ? 0 : 1);
