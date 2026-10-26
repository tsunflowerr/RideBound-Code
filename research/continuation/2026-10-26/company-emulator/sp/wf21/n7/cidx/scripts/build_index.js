// wf21/n7 causal-index builder, step 2. Pure function of the month outputs written by build_sql.js + DuckDB.
// OLD RULE (= wf7 accuracy-index-2019.json): F[s][b] = exp(e_med(s, b) - median of month(s)'s e_med), e_med from the median polish
//   of the calendar month containing s (that polish uses every day of the month, including days AFTER a target day).
// CAUSAL RULE (CLc, fixed before any CLc run): for a target day d in month M the reference is month R = M - 1 (entirely before d).
//   Source day s in R      : e(s, b) = e_med of R's own polish (identical to the old value: the polish of R uses only R).
//   Source day s in M, s<d : e(s, b) = median over s's trips in bin b of (log duration - r_OD of R), OD pairs of R's reference only
//                            (bins_next.csv of R; no re-estimation with month-M data).
//   F_d[s][b] = exp(e(s, b) - median of R's e_med): ONE constant for all sources of d (it cancels in the engine's ratios
//   prof[b]/prof[b0]; the old rule used month(s)'s median, which weights sources from two months slightly differently).
//   Only days s in [d - 28, d - 1] are stored for target d (prev4 sources are d - 7k, k = 1..4).
// Gates written to cidx/results/GATES-cidx.txt:
//   CI-1 old rule rebuilt from the rebuilt month outputs == accuracy-index-2019.json (every day, every bin, exact)
//   CI-2 per-target old-rule file: forecast.profile(d, s0, 'prev4', .) == profile from accuracy-index-2019.json (30 days x 2 windows, exact)
//   CI-3 causal file: every stored day < target; reference month ends before the target's month; >= 2 sources, same source days as CL
// Outputs: cidx/results/index-old-pertarget.json (gate input), cidx/results/index-causal-pertarget.json (CLc), profile_diff.txt
'use strict';
const fs = require('fs'), path = require('path');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const N7 = path.join(SP, 'wf21/n7');
const FC = require(path.join(N7, 'code/scripts/forecast.js'));
const MONTHS = ['2019-08', '2019-09', '2019-10', '2019-11', '2019-12'];
const W = m => path.join(N7, 'cidx/work/yellow-' + m);
const PANELS = JSON.parse(fs.readFileSync(path.join(SP, 'wf17/r1-identity-fence-pad/work/panels.json'), 'utf8'));
const TARGETS = [...PANELS.F14, ...PANELS.F16].sort();
const out = [], G = [];
// ---- CI-1
const old = FC.loadIndex(MONTHS.map(m => path.join(W(m), 'bins.csv'))).F;
const ref = JSON.parse(fs.readFileSync(path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json'), 'utf8'));
{
  let n = 0, bad = 0;
  const kA = Object.keys(old).sort(), kB = Object.keys(ref).sort();
  if (JSON.stringify(kA) !== JSON.stringify(kB)) bad++;
  for (const d of kB) for (let b = 0; b < 96; b++) {
    n++;
    const x = old[d] ? old[d][b] : undefined, y = ref[d][b];
    if (!(Object.is(x, y) || (y === null && Number.isNaN(x)))) bad++;
  }
  const strEq = JSON.stringify(old) === JSON.stringify(ref);
  G.push(`CI-1 old rule rebuilt == accuracy-index-2019.json: ${kB.length} days x 96 bins = ${n} values, mismatches ${bad}, JSON string identical ${strEq} -> ${bad === 0 && strEq ? 'PASS' : 'FAIL'}`);
}
// month medians (as loadIndex)
const monthMed = {}, monthE = {};
for (const m of MONTHS) {
  const rows = FC.readBins(path.join(W(m), 'bins.csv'));
  const s = rows.map(r => r.e).sort((a, b) => a - b);
  monthMed[m] = s[Math.floor(s.length / 2)];
  monthE[m] = rows;
}
const nextE = {};
for (const R of ['2019-08', '2019-09', '2019-11']) nextE[R] = FC.readBins(path.join(W(R), 'bins_next.csv'));
const prevMonth = m => { const [y, mo] = m.split('-').map(Number); return mo === 1 ? `${y - 1}-12` : `${y}-${String(mo - 1).padStart(2, '0')}`; };
const nextMonth = m => { const [y, mo] = m.split('-').map(Number); return mo === 12 ? `${y + 1}-01` : `${y}-${String(mo + 1).padStart(2, '0')}`; };
const table = rows => { const T = {}; for (const r of rows) (T[r.d] ||= new Array(96).fill(NaN))[r.b] = r.e; return T; };
const E = Object.fromEntries(MONTHS.map(m => [m, table(monthE[m])]));
const EN = Object.fromEntries(Object.keys(nextE).map(R => [R, table(nextE[R])]));
const window28 = d => Array.from({ length: 28 }, (_, i) => FC.dateAdd(d, -(i + 1))).sort();
const OLD = { label: 'wf21-n7-exploratory-post-audit', rule: 'old (wf7 accuracy-index-2019.json restricted to [d-28, d-1])', targets: {} };
const CAU = { label: 'wf21-n7-exploratory-post-audit', rule: 'causal: reference = calendar month before the target month; same-month source days scored against that reference; one normalising constant (median of the reference month e_med)', targets: {} };
for (const d of TARGETS) {
  const M = d.slice(0, 7), R = prevMonth(M);
  if (!E[R]) throw new Error('no reference month ' + R + ' for ' + d);
  const o = {}, c = {};
  for (const s of window28(d)) {
    if (ref[s]) o[s] = ref[s];
    const ms = s.slice(0, 7);
    let e = null;
    if (ms === R) e = E[R][s];
    else if (ms === M) { if (!EN[R]) throw new Error('no scored month for reference ' + R); e = EN[R][s]; }
    else throw new Error(`source window of ${d} reaches ${s} outside ${R}/${M}`);
    if (e) c[s] = e.map(x => Math.exp(x - monthMed[R]));
  }
  OLD.targets[d] = { F: o };
  CAU.targets[d] = { ref: R, refEndExclusive: nextMonth(R) + '-01', F: c };
}
// ---- CI-2
{
  let n = 0, bad = 0, nullBoth = 0;
  for (const d of TARGETS) for (const s0 of [32, 68]) {
    n++;
    const a = FC.profile(d, s0, 'prev4', ref, 15), b = FC.profile(d, s0, 'prev4', OLD.targets[d].F, 15);
    if (a === null && b === null) { nullBoth++; continue; }
    if (JSON.stringify(a) !== JSON.stringify(b)) bad++;
  }
  G.push(`CI-2 per-target old-rule profiles == CL profiles from accuracy-index-2019.json: ${n} day-windows, mismatches ${bad}, both-null ${nullBoth} -> ${bad === 0 && nullBoth === 0 ? 'PASS' : 'FAIL'}`);
}
// ---- CI-3
{
  let bad = 0;
  const lines = [];
  for (const d of TARGETS) {
    const T = CAU.targets[d];
    for (const s of Object.keys(T.F)) if (!(s < d)) bad++;
    if (!(T.refEndExclusive <= d.slice(0, 7) + '-01')) bad++;
    const src = FC.sources(d, 'prev4', T.F), srcOld = FC.sources(d, 'prev4', ref);
    if (src.length < 2) bad++;
    if (JSON.stringify(src) !== JSON.stringify(srcOld)) bad++;
    lines.push(`  ${d} ref ${T.ref}: sources ${src.join(' ')} (same-month ${src.filter(s => s.slice(0, 7) === d.slice(0, 7)).length}/${src.length})`);
  }
  G.push(`CI-3 causal file: all stored days < target, reference month before target month, >= 2 sources and the same source days as CL -> ${bad === 0 ? 'PASS' : 'FAIL (' + bad + ')'}`);
  out.push('# sources per target (causal file)');
  out.push(...lines);
}
// ---- descriptive: profile ratio differences (horizon b = 1..8 = 2 h), causal vs old (no traffic truth, no dispatch outcome)
out.push('\n# CLc vs CL forecast weights w(b) = m(b)/m(0), b = 1..8 (15 min..2 h): mean and max |w_c - w_old| over b, and mean w (old, causal)');
const summ = { w08: [], w17: [] };
for (const d of TARGETS) for (const [w, s0] of [['w08', 32], ['w17', 68]]) {
  const a = FC.profile(d, s0, 'prev4', ref, 15), b = FC.profile(d, s0, 'prev4', CAU.targets[d].F, 15);
  let mx = 0, sm = 0, wa = 0, wb = 0;
  for (let k = 1; k <= 8; k++) { const x = a[k] / a[0], y = b[k] / b[0]; mx = Math.max(mx, Math.abs(x - y)); sm += Math.abs(x - y); wa += x; wb += y; }
  summ[w].push(mx);
  out.push(`  ${d} ${w}: mean|dw| ${(100 * sm / 8).toFixed(3)} %  max|dw| ${(100 * mx).toFixed(3)} %  mean w old ${(wa / 8).toFixed(4)} causal ${(wb / 8).toFixed(4)}`);
}
for (const w of ['w08', 'w17']) {
  const s = summ[w].slice().sort((x, y) => x - y);
  out.push(`  ${w}: max|dw| over days: median ${(100 * s[Math.floor(s.length / 2)]).toFixed(3)} %, max ${(100 * s[s.length - 1]).toFixed(3)} %`);
}
fs.writeFileSync(path.join(N7, 'cidx/results/index-old-pertarget.json'), JSON.stringify(OLD));
fs.writeFileSync(path.join(N7, 'cidx/results/index-causal-pertarget.json'), JSON.stringify(CAU));
fs.writeFileSync(path.join(N7, 'cidx/results/GATES-cidx.txt'), G.join('\n') + '\n');
fs.writeFileSync(path.join(N7, 'cidx/results/profile_diff.txt'), out.join('\n') + '\n');
console.log(G.join('\n'));
