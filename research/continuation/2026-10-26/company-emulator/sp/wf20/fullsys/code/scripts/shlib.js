// wf15 shared helpers for the main thread: cell sets, day lists, prior table, theta from a reference distribution, hashing.
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');

const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp'; // wf17 copy: absolute scratchpad (was path.resolve(__dirname, '..', '..')); WF stays wf15 (prior, frozen, ref, day lists are read from there, read-only)
const WF = path.join(SP, 'wf15');
const SETS = {
  discovery: { dirs: [path.join(SP, 'wf6/via-many-days/scenarios/weekdays-P512'), path.join(SP, 'wf6/via-many-days/scenarios/replacements-P512')],
    excluded: ['2019-10-22', '2019-12-06'] },   // wf6 data-quality exclusions (replaced by 2019-10-01, 2019-12-20)
  confirm: { dirs: [path.join(SP, 'wf14/scenarios/weekdays-P512')], excluded: [] },
};
const CONFIGS = {
  INS: {},
  G2: { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' },
};
const RUN_OVERRIDES = { maxRidePermille: 2000, dwellMs: 0, nVeh: 62 };

const sha256 = buf => crypto.createHash('sha256').update(buf).digest('hex');
const sha256File = f => sha256(fs.readFileSync(f));

// cell list: files named <day>-<win>[-r<k>].json
function listCells(o = {}) {
  let dirs = [], excluded = new Set(o.exclude || []);
  if (o.set) { const S = SETS[o.set]; if (!S) throw new Error('unknown set ' + o.set); dirs = S.dirs; for (const d of S.excluded) excluded.add(d); }
  if (o.scen) dirs = dirs.concat(o.scen);
  if (!dirs.length) throw new Error('no scenario dirs (--set or --scen)');
  let dayFilter = null;
  if (o.daysFile) dayFilter = new Set(fs.readFileSync(o.daysFile, 'utf8').split(/\r?\n/).map(s => s.trim()).filter(s => /^\d{4}-\d\d-\d\d$/.test(s)));
  const out = [];
  for (const d of dirs) for (const f of fs.readdirSync(d).filter(x => x.endsWith('.json')).sort()) {
    const cell = f.replace('.json', ''), m = /^(\d{4}-\d\d-\d\d)-(w08|w17)(?:-(r\d+))?$/.exec(cell);
    if (!m) continue;
    const day = m[1], win = m[2], rs = m[3] || null;
    if (excluded.has(day)) continue;
    if (o.days) { const [a, b] = o.days.split('..'); if (day < a || day > (b || a)) continue; }
    if (dayFilter && !dayFilter.has(day)) continue;
    if (o.win && o.win !== win) continue;
    out.push({ file: path.join(d, f), cell, day, win, rs, set: o.set || 'custom' });
  }
  out.sort((x, y) => (x.cell < y.cell ? -1 : x.cell > y.cell ? 1 : 0));
  if (o.firstDays) { const keep = new Set([...new Set(out.map(c => c.day))].sort().slice(0, o.firstDays)); return out.filter(c => keep.has(c.day)); }
  return out;
}

// prior_prevhour.csv -> Map 'day|win' -> { O: number[nZones], D: number[nZones] } in zone-index order (zoneIds ascending)
function loadPrior(file, zoneIds) {
  const zpos = new Map(Array.from(zoneIds, (z, i) => [z, i]));
  const lines = fs.readFileSync(file, 'utf8').trim().split(/\r?\n/);
  const head = lines[0].split(',');
  const ix = k => { const i = head.indexOf(k); if (i < 0) throw new Error('prior csv: no column ' + k); return i; };
  const iD = ix('day'), iW = ix('win'), iS = ix('side'), iZ = ix('zone'), iN = ix('n');
  const M = new Map();
  for (const l of lines.slice(1)) {
    const v = l.split(','), key = v[iD] + '|' + v[iW];
    if (!M.has(key)) M.set(key, { O: new Array(zoneIds.length).fill(0), D: new Array(zoneIds.length).fill(0) });
    const z = zpos.get(Number(v[iZ]));
    if (z === undefined) throw new Error('prior csv: unknown zone ' + v[iZ]);
    if (v[iS] !== 'O' && v[iS] !== 'D') throw new Error('prior csv: side');
    M.get(key)[v[iS]][z] += Number(v[iN]);
  }
  return M;
}

// SPEC 5.2: theta_{c,w}(x) = s_(ceil(x * M)) (type-1 empirical quantile of the sorted reference scores, 1-indexed);
// x = 0 -> -Infinity (everyone allowed), x = 100 -> +Infinity (everyone guaranteed). pct is the integer percent of the key.
function thetaFromSorted(sorted, pct) {
  if (!Number.isInteger(pct) || pct < 0 || pct > 100) throw new Error('theta: pct must be an integer 0..100');
  if (pct === 0) return -Infinity;
  if (pct === 100) return Infinity;
  const M = sorted.length;
  if (!M) throw new Error('theta: empty reference');
  const k = Math.ceil(pct * M / 100);
  return sorted[k - 1];
}
const thetaToJson = t => (t === Infinity ? '+Infinity' : t === -Infinity ? '-Infinity' : t);
const thetaFromJson = t => (t === '+Infinity' ? Infinity : t === '-Infinity' ? -Infinity : t);

// day lists written by prep_data.js (work/days_discovery.csv, work/days_confirm.csv): Set of 'YYYY-MM-DD'
function dayList(name) {
  const f = path.join(WF, 'work', 'days_' + name + '.csv');
  const days = new Set(fs.readFileSync(f, 'utf8').split('\n').map(s => s.trim()).filter(s => /^\d{4}-\d\d-\d\d$/.test(s)));
  if (!days.size) throw new Error('empty day list ' + f);
  return days;
}

function readNdjson(file) {
  const rows = [];
  if (!fs.existsSync(file)) return rows;
  for (const l of fs.readFileSync(file, 'utf8').split('\n')) { if (!l) continue; try { rows.push(JSON.parse(l)); } catch (e) { /* torn last line */ } }
  return rows;
}

module.exports = { SP, WF, SETS, CONFIGS, RUN_OVERRIDES, sha256, sha256File, listCells, loadPrior, thetaFromSorted, thetaToJson, thetaFromJson, readNdjson, dayList };
