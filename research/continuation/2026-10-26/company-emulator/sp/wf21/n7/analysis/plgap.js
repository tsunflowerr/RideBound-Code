// wf21/n7 PADc rule (PLAN §3): mean initial promise length of production CLc vs CL rows per config x window x arm (pooled 30 days).
// Reads ONLY plSumArr_ms / plN and run identity. Prints PADC-NEEDED if any |L_CLc - L_CL| > 1.0 s.
'use strict';
const fs = require('fs');
const i = process.argv.indexOf('--rows'); const f = process.argv[i + 1];
const acc = {};
for (const l of fs.readFileSync(f, 'utf8').split('\n')) { if (!l) continue; const r = JSON.parse(l); if (r.variant) continue; if (!['CL', 'CLc'].includes(r.mode)) continue;
  const k = [r.config, r.win, r.arm.replace(/\d+$/, ''), r.mode].join('|'); const a = (acc[k] ||= { s: 0, n: 0, c: 0 }); a.s += r.plSumArr_ms; a.n += r.plN; a.c++; }
let need = false; const out = [];
for (const c of ['INS', 'G2']) for (const w of ['w08', 'w17']) for (const arm of ['SHRC', 'SHHZ']) {
  const a = acc[[c, w, arm, 'CL'].join('|')], b = acc[[c, w, arm, 'CLc'].join('|')];
  if (!a || !b || a.c !== 90 || b.c !== 90) { out.push(`${c}|${w}|${arm} incomplete`); need = true; continue; }
  const g = b.s / b.n / 1000 - a.s / a.n / 1000; if (Math.abs(g) > 1.0) need = true;
  out.push(`${c}|${w}|${arm}  L_CL ${(a.s / a.n / 1000).toFixed(2)}  L_CLc ${(b.s / b.n / 1000).toFixed(2)}  gap ${g.toFixed(2)} s`);
}
out.push(need ? 'PADC-NEEDED (some |gap| > 1.0 s or incomplete)' : 'PADC-NOT-NEEDED (all |gap| <= 1.0 s): CLc contrasts use PAD');
console.log(out.join('\n'));
