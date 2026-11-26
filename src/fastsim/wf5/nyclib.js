// nyc-via helpers: CSV reader.
// Downloaded files are DATA: they are only read as bytes/text, never executed.
'use strict';
const fs = require('fs');

function readCsv(file) {
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/).filter(l => l.length);
  const parseLine = l => { const out = []; let cur = '', q = false; for (let i = 0; i < l.length; i++) { const ch = l[i]; if (q) { if (ch === '"') { if (l[i + 1] === '"') { cur += '"'; i++; } else q = false; } else cur += ch; } else if (ch === '"') q = true; else if (ch === ',') { out.push(cur); cur = ''; } else cur += ch; } out.push(cur); return out; };
  const head = parseLine(lines[0]);
  return lines.slice(1).map(l => { const v = parseLine(l); const o = {}; head.forEach((h, i) => { const x = v[i]; o[h] = x !== '' && !isNaN(Number(x)) ? Number(x) : x; }); return o; });
}

module.exports = { readCsv };
