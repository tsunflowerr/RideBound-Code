// wf17 r1-identity-fence-pad: transformed copies of wf14 D10 r0 cells (wf17-exploratory-post-audit).
//   adapter (R1): identity.mode -> ridebound-fleetpy-adapter, travel.snapshotRounding -> fleetpy-seconds-half-even,
//                 travel.base.matrixFile and provenance.provenanceFile -> absolute paths of the wf14 originals (read only)
//   flat (R2b):   every travel.profile.factors[b] -> factors[0]; matrixFile and provenanceFile -> absolute paths (as a3 fence_flat.js / a5 V6)
// A JSON diff of every copy against its original must show exactly the intended keys; scenario.js validation must pass.
// Usage: node make_cells.js adapter|flat
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const KEY = path.resolve(__dirname, '..', '..');
const SC = require('../runner/scenario.js');
const SRC = path.join(SP, 'wf14/scenarios/weekdays-P512');
const kind = process.argv[2];
if (!['adapter', 'flat'].includes(kind)) throw new Error('adapter|flat');
const PAN = JSON.parse(fs.readFileSync(path.join(KEY, 'work/panels.json'), 'utf8'));
const out = path.join(KEY, 'cells', kind);
fs.mkdirSync(out, { recursive: true });
// flatten to path -> JSON value for a structural diff
function flat(o, p = '', acc = {}) {
  if (o && typeof o === 'object' && !Array.isArray(o)) { for (const k of Object.keys(o)) flat(o[k], p ? p + '.' + k : k, acc); }
  else if (Array.isArray(o)) { if (!o.length) acc[p] = '[]'; o.forEach((v, i) => flat(v, p + '[' + i + ']', acc)); }
  else acc[p] = JSON.stringify(o);
  return acc;
}
const report = [];
for (const day of PAN.D10) for (const win of ['w08', 'w17']) {
  const cell = `${day}-${win}-r0`;
  const file = path.join(SRC, cell + '.json');
  const orig = JSON.parse(fs.readFileSync(file, 'utf8'));
  const s = JSON.parse(JSON.stringify(orig));
  s.travel.base.matrixFile = path.resolve(SRC, orig.travel.base.matrixFile).split(path.sep).join('/');
  s.provenance.provenanceFile = path.resolve(SRC, orig.provenance.provenanceFile).split(path.sep).join('/');
  if (kind === 'adapter') { s.identity.mode = 'ridebound-fleetpy-adapter'; s.travel.snapshotRounding = 'fleetpy-seconds-half-even'; }
  else { const f0 = s.travel.profile.factors[0]; s.travel.profile.factors = s.travel.profile.factors.map(() => f0); }
  SC.validateScenario(s);
  const a = flat(orig), b = flat(s);
  const changed = [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => a[k] !== b[k]);
  const allowed = kind === 'adapter' ? ['identity.mode', 'travel.snapshotRounding', 'travel.base.matrixFile', 'provenance.provenanceFile']
    : ['travel.base.matrixFile', 'provenance.provenanceFile', ...Object.keys(a).filter(k => k.startsWith('travel.profile.factors['))];
  const bad = changed.filter(k => !allowed.includes(k));
  if (bad.length) throw new Error(`${cell}: unexpected changes ${bad.join(', ')}`);
  if (kind === 'adapter' && changed.length !== 4) throw new Error(`${cell}: expected exactly 4 changed keys, got ${changed.length}`);
  if (!fs.existsSync(s.travel.base.matrixFile) || !fs.existsSync(s.provenance.provenanceFile)) throw new Error(cell + ': absolute paths do not resolve');
  fs.writeFileSync(path.join(out, cell + '.json'), JSON.stringify(s));
  report.push({ cell, changed: changed.length, keys: kind === 'adapter' ? changed : changed.filter(k => !k.startsWith('travel.profile.factors[')).concat([`travel.profile.factors[1..15] (${changed.filter(k => k.startsWith('travel.profile.factors[')).length} values)`]) });
}
fs.writeFileSync(path.join(out, '_diff_report.json'), JSON.stringify({ label: 'wf17-exploratory-post-audit', kind, cells: report }, null, 1));
console.log(kind, report.length, 'cells; changed keys per cell:', [...new Set(report.map(r => r.changed))].join(','), JSON.stringify(report[0].keys));
