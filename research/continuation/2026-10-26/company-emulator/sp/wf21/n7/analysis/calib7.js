// wf21/n7 PAD calibration (label wf21-n7-exploratory-post-audit = thăm dò sau kiểm toán). Reads ONLY promise-length fields
// (plSumArr_ms, plN) and the run identity (config, win, arm, mode, flatRatio, variant, day, rs) of row files; it never reads an
// outcome field. Group g = config x window x arm base (SHRC, SHHZ): 8 groups, each pooled over all 30 days x 3 resamples.
//   L(rows) = sum(plSumArr_ms) / sum(plN) / 1000  (mean initial promise length p0 - request time, s, over promised riders)
//   target L*_g = L of the reference rows (mode CL, or mode CLc for PADc), L_N,g = L of the mode-N rows (= PAD at r = 1)
//   points (r, L(r)) = (1, L_N) plus every complete calibration group (90 cells) at a tried r
//   next r: secant on the two points closest to L* that bracket it (regula falsi) when a bracket exists, otherwise secant on the
//   two most recent points; the first guess uses the prior slope 750 s per unit r (wf17 R3 dumps, work/pl_slope_estimate.txt).
//   converged: |L(r) - L*| <= 0.5 s. Final r = the tried r with the smallest |L(r) - L*|.
// Usage: node calib7.js --ref <rows.ndjson> --refMode CL|CLc --calib <calib.ndjson> --variant <calib variant label> --out <flat_next.json>
//        [--final <flat_final.json>]   (prints a table; writes the next flat file, or the final one when every group converged or --final)
'use strict';
const fs = require('fs');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const TOL_S = 0.5, PRIOR_SLOPE_S = 750, MAXIT = 6;
const read = f => (fs.existsSync(f) ? fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => { try { return JSON.parse(l); } catch (e) { return null; } }).filter(Boolean) : []);
// keep ONLY identity + promise-length fields (no outcome field is kept in memory)
const slim = r => ({ config: r.config, win: r.win, arm: r.arm.replace(/\d+$/, ''), mode: r.mode, flatRatio: r.flatRatio === undefined ? null : r.flatRatio,
  variant: r.variant || null, cell: r.cell, pl: r.plSumArr_ms, n: r.plN });
const refRows = read(arg('ref')).map(slim), calRows = read(arg('calib')).map(slim);
const refMode = arg('refMode') || 'CL', variant = arg('variant');
const groups = [];
for (const config of ['INS', 'G2']) for (const win of ['w08', 'w17']) for (const arm of ['SHRC', 'SHHZ']) groups.push({ config, win, arm, key: `${config}|${win}|${arm}` });
const Lof = rows => { if (rows.length !== 90) return null; let s = 0, n = 0; for (const r of rows) { if (typeof r.pl !== 'number') return null; s += r.pl; n += r.n; } return s / n / 1000; };
const uniq = rows => { const m = new Map(); for (const r of rows) m.set(r.cell, r); return [...m.values()]; };
const out = [], next = {}, fin = {};
let allConv = true;
for (const g of groups) {
  const sel = (rows, mode, extra) => uniq(rows.filter(r => r.config === g.config && r.win === g.win && r.arm === g.arm && r.mode === mode && (!extra || extra(r))));
  const Lstar = Lof(sel(refRows, refMode)), LN = Lof(sel(refRows, 'N'));
  if (Lstar === null || LN === null) throw new Error('incomplete reference/N rows for ' + g.key);
  const pts = [{ r: 1, L: LN, src: 'N' }];
  const rs = [...new Set(calRows.filter(r => r.config === g.config && r.win === g.win && r.arm === g.arm && r.mode === 'PAD' && r.variant === variant).map(r => r.flatRatio))];
  for (const r of rs) { const L = Lof(sel(calRows, 'PAD', x => x.flatRatio === r && x.variant === variant)); if (L !== null) pts.push({ r, L, src: 'calib' }); }
  const tried = pts.filter(p => p.src === 'calib');
  const best = tried.slice().sort((a, b) => Math.abs(a.L - Lstar) - Math.abs(b.L - Lstar))[0] || null;
  const conv = best && Math.abs(best.L - Lstar) <= TOL_S;
  let rn;
  if (conv) rn = best.r;
  else if (!tried.length) rn = 1 + (Lstar - LN) / PRIOR_SLOPE_S;
  else {
    const lo = pts.filter(p => p.L < Lstar).sort((a, b) => b.L - a.L)[0], hi = pts.filter(p => p.L > Lstar).sort((a, b) => a.L - b.L)[0];
    let a, b;
    if (lo && hi) { a = lo; b = hi; } else { const s = pts.slice(-2); a = s[0]; b = s[1]; if (pts.length < 2) { a = pts[0]; b = pts[0]; } }
    rn = a.L === b.L ? best.r + (Lstar - best.L) / PRIOR_SLOPE_S : a.r + (Lstar - a.L) * (b.r - a.r) / (b.L - a.L);
    rn = Math.round(rn * 1e6) / 1e6;                                                    // D3: compare AFTER rounding
    if (tried.some(p => p.r === rn)) rn = best.r + (Lstar - best.L) / PRIOR_SLOPE_S;   // never repeat a tried value
    rn = Math.round(rn * 1e6) / 1e6;                                                    // D6: the fallback is guarded too:
    const step = (Lstar > best.L ? 1 : -1) * 0.0002;                                   // step on from best toward L* until untried
    while (tried.some(p => p.r === rn)) rn = Math.round((rn + step) * 1e6) / 1e6;
  }
  rn = Math.round(rn * 1e6) / 1e6;
  if (!conv) allConv = false;
  next[g.key] = rn; fin[g.key] = best ? best.r : null;
  out.push(`${g.key.padEnd(14)} L_N ${LN.toFixed(2)}  L*(${refMode}) ${Lstar.toFixed(2)}  (L*-L_N ${(Lstar - LN).toFixed(2)} s)  tried ${tried.map(p => `r=${p.r} L=${p.L.toFixed(2)} gap=${(p.L - Lstar).toFixed(2)}`).join('; ') || '-'}  ${conv ? 'CONVERGED' : 'next r ' + rn}`);
}
const iters = groups.map(g => calRows.filter(r => r.config === g.config && r.win === g.win && r.arm === g.arm && r.mode === 'PAD' && r.variant === variant).length / 90);
out.push(`max iterations so far (complete groups) ${Math.max(...iters)}; all converged ${allConv}`);
console.log(out.join('\n'));
const label = 'wf21-n7-exploratory-post-audit; calibration reads promise length only';
if (allConv || arg('final') && Math.max(...iters) >= MAXIT || process.argv.includes('--force-final')) {
  const f = arg('final');
  if (!f) throw new Error('--final <file> needed');
  if (fs.existsSync(f)) throw new Error('final file exists, never overwritten: ' + f);
  fs.writeFileSync(f, JSON.stringify({ label, refMode, variant, tolS: TOL_S, converged: allConv, flat: fin, table: out }, null, 1) + '\n');
  console.log('FINAL written ' + f);
} else {
  const f = arg('out');
  if (fs.existsSync(f)) throw new Error('next-flat file exists, never overwritten: ' + f);
  fs.writeFileSync(f, JSON.stringify({ label, refMode, variant, flat: next }, null, 1) + '\n');
  console.log('next written ' + f);
}
