// wf15 shared helpers for the main thread: prior table.
'use strict';
const fs = require('fs'), path = require('path');

const SP = require('../../paths.js').DATA; // data root (RB_DATA); WF stays wf15 (prior, frozen, ref, day lists are read from there, read-only)
const WF = path.join(SP, 'wf15');
const CONFIGS = {
  INS: {},
  G2: { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' },
};

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

module.exports = { WF, CONFIGS, loadPrior };
