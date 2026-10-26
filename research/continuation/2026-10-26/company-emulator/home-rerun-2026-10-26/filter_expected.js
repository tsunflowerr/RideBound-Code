// wf22-home: keep only the expected rows of one panel and the listed forecast modes (and optional variants). Copies
// matching lines byte for byte; changes nothing else. Usage:
//   node filter_expected.js <expected.tsv> <out.tsv> --panel F14 --modes N,CLc [--variants ,PADc]
'use strict';
const fs = require('fs');
const [inp, out] = process.argv.slice(2);
const arg = k => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : null; };
const panel = arg('--panel'), modes = new Set((arg('--modes') || '').split(',').filter(Boolean));
const variants = arg('--variants') === null ? null : new Set(arg('--variants').split(','));
if (!inp || !out || !panel || !modes.size) { console.error('usage: see header'); process.exit(2); }
if (fs.existsSync(out)) { console.error('refused: ' + out + ' exists'); process.exit(3); }
const lines = fs.readFileSync(inp, 'utf8').split(/\r?\n/).filter(Boolean);
const head = lines[0].split('\t'), iP = head.indexOf('panel'), iM = head.indexOf('mode'), iV = head.indexOf('variant');
if (iP < 0 || iM < 0) { console.error('expected file has no panel/mode column'); process.exit(4); }
const keep = lines.slice(1).filter(l => { const v = l.split('\t'); return v[iP] === panel && modes.has(v[iM]) && (variants === null || variants.has(iV < 0 ? '' : v[iV])); });
fs.writeFileSync(out, [lines[0], ...keep].join('\n') + '\n', { flag: 'wx' });
const byMode = {}; for (const l of keep) { const v = l.split('\t'); const k = v[iM] + '/' + (iV < 0 ? '' : v[iV]); byMode[k] = (byMode[k] || 0) + 1; }
console.log(`kept ${keep.length} of ${lines.length - 1} rows -> ${out}`, JSON.stringify(byMode));
