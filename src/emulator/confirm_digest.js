// So các dòng kết quả chạy lại ở máy nhà với dấu vân tay của máy công ty (expected/*.tsv).
// Cách dùng: node confirm_digest.js <expected.tsv> <rows.ndjson> [--panel F14|F16]
// Khóa so: cell, config, mode, arm, flatRatio, fcIndex, nVeh, variant, g2PenaltyMs, fenceTolMs, panel.
// So: served, visRev60, late60First, pDisp, breachDeadline60, sumTV5 và sig (dấu vân tay từng khách).
'use strict';
const fs = require('fs');
const [expFile, rowsFile] = process.argv.slice(2);
const pi = process.argv.indexOf('--panel'); const PANEL = pi > 0 ? process.argv[pi + 1] : null;
if (!expFile || !rowsFile) { console.error('usage: node confirm_digest.js <expected.tsv> <rows.ndjson> [--panel F14|F16]'); process.exit(2); }
const KEY = ['cell', 'config', 'mode', 'arm', 'flatRatio', 'fcIndex', 'nVeh', 'variant', 'g2PenaltyMs', 'fenceTolMs', 'panel'];
const MET = ['served', 'visRev60', 'late60First', 'pDisp', 'breachDeadline60', 'sumTV5', 'sig'];
const lines = fs.readFileSync(expFile, 'utf8').split(/\r?\n/).filter(Boolean);
const head = lines[0].split('\t');
const exp = new Map();
for (const l of lines.slice(1)) {
  const v = l.split('\t'); const o = Object.fromEntries(head.map((h, i) => [h, v[i]]));
  if (PANEL && o.panel !== PANEL) continue;
  exp.set(KEY.map(k => o[k]).join('|'), o);
}
const str = x => (x === undefined || x === null) ? '' : String(x);
const got = new Map();
for (const l of fs.readFileSync(rowsFile, 'utf8').split(/\r?\n/).filter(Boolean)) {
  const r = JSON.parse(l);
  if (PANEL && r.panel !== PANEL) continue;
  got.set(KEY.map(k => str(r[k])).join('|'), r);
}
let same = 0, metricOnly = 0, diff = 0, missing = 0; const firstDiff = [];
for (const [k, e] of exp) {
  const r = got.get(k);
  if (!r) { missing++; if (firstDiff.length < 5) firstDiff.push('THIẾU ' + k); continue; }
  const metOk = MET.filter(m => m !== 'sig').every(m => str(r[m]) === e[m]);
  const sigOk = str(r.sig) === e.sig;
  if (metOk && sigOk) same++; else if (metOk) metricOnly++; else { diff++; if (firstDiff.length < 5) firstDiff.push('LỆCH ' + k + ' :: ' + MET.filter(m => str(r[m]) !== e[m]).map(m => `${m} ${e[m]} -> ${str(r[m])}`).join('; ')); }
}
const extra = [...got.keys()].filter(k => !exp.has(k)).length;
console.log(`Mong đợi ${exp.size} dòng${PANEL ? ' (panel ' + PANEL + ')' : ''}. Trùng hoàn toàn (kể cả từng khách): ${same}. ` +
  `Trùng chỉ số nhưng khác dấu vân tay khách: ${metricOnly}. Lệch chỉ số: ${diff}. Thiếu: ${missing}. Dòng thừa không có trong bản mong đợi: ${extra}.`);
for (const x of firstDiff) console.log('  ' + x);
process.exit(same === exp.size ? 0 : 1);
