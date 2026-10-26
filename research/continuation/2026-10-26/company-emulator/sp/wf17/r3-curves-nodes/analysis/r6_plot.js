// wf17 R6 plots (EXPLORATORY, POST-AUDIT): presentation only, no statistic or label is computed here. Reads results/r6_tables.json
// (curves = 30-day means) and writes plots/r6_<config>_primary.svg (Pchg, visRev60 /1000, breachDeadline60 /1000 vs served/day),
// plots/r6_<config>_secondary.svg (TV5/served, pDispAny1) and plots/r6_ascii.txt (character plots of the same curves).
// Encoding: colour = admission rule (blue = fence EOHZ/SHHZ, orange = Rule C ledger RCE/SHRC); line style + marker = allocation
// (solid + circle = uniform alpha, dashed + square = Shadow x); gray diamond = U. Palette validated with the dataviz validator.
'use strict';
const fs = require('fs'), path = require('path');
const KEY = path.resolve(__dirname, '..');
const inJson = process.argv[2] || path.join(KEY, 'results/r6_tables.json');
const outDir = process.argv[3] || path.join(KEY, 'plots');
fs.mkdirSync(outDir, { recursive: true });
const S = JSON.parse(fs.readFileSync(inJson, 'utf8'));
const SER = [
  { fam: 'EOHZ', name: 'EOHZ (fence, uniform alpha)', col: 'var(--fence)', dash: '', mk: 'circle', ch: 'E', par: 'a' },
  { fam: 'RCE', name: 'RCE (Rule C ledger, uniform alpha)', col: 'var(--ledger)', dash: '', mk: 'circle', ch: 'R', par: 'a' },
  { fam: 'SHHZ', name: 'SHHZ (fence, Shadow x)', col: 'var(--fence)', dash: '6 4', mk: 'square', ch: 'H', par: 'x' },
  { fam: 'SHRC', name: 'SHRC (Rule C ledger, Shadow x)', col: 'var(--ledger)', dash: '6 4', mk: 'square', ch: 'C', par: 'x' },
];
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;');
const nice = (lo, hi, n = 5) => {
  const span = hi - lo || Math.abs(hi) || 1, step0 = span / n, mag = 10 ** Math.floor(Math.log10(step0));
  const step = [1, 2, 2.5, 5, 10].map(k => k * mag).find(k => span / k <= n) || 10 * mag;
  const a = Math.floor(lo / step) * step, b = Math.ceil(hi / step) * step, t = [];
  for (let v = a; v <= b + step / 2; v += step) t.push(+v.toFixed(10));
  return { a, b, t };
};
function panel(curves, yk, x0, y0, w, h) {
  const pts = curves.filter(c => Number.isFinite(c.Y[yk]) && Number.isFinite(c.served));
  const xs = pts.map(c => c.served), ys = pts.map(c => c.Y[yk]);
  const X = nice(Math.min(...xs), Math.max(...xs)), Y = nice(Math.min(...ys), Math.max(...ys));
  const L = 52, B = 34, T = 26, R = 10;
  const px = v => x0 + L + (v - X.a) / (X.b - X.a || 1) * (w - L - R), py = v => y0 + h - B - (v - Y.a) / (Y.b - Y.a || 1) * (h - B - T);
  let s = `<g><text x="${x0 + L}" y="${y0 + 16}" class="t1">${esc(yk)}</text>`;
  for (const v of Y.t) s += `<line x1="${x0 + L}" x2="${x0 + w - R}" y1="${py(v)}" y2="${py(v)}" class="grid"/><text x="${x0 + L - 6}" y="${py(v) + 4}" class="tk" text-anchor="end">${+v.toFixed(3)}</text>`;
  for (const v of X.t) s += `<text x="${px(v)}" y="${y0 + h - B + 16}" class="tk" text-anchor="middle">${+v.toFixed(1)}</text>`;
  s += `<line x1="${x0 + L}" x2="${x0 + w - R}" y1="${y0 + h - B}" y2="${y0 + h - B}" class="axis"/><text x="${x0 + L + (w - L - R) / 2}" y="${y0 + h - 4}" class="tk" text-anchor="middle">served riders / day</text>`;
  for (const sr of SER) {
    const c = curves.filter(q => q.family === sr.fam && Number.isFinite(q.Y[yk])).sort((a, b) => a.param - b.param);
    if (!c.length) continue;
    s += `<polyline fill="none" stroke="${sr.col}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round" ${sr.dash ? `stroke-dasharray="${sr.dash}"` : ''} points="${c.map(q => `${px(q.served).toFixed(1)},${py(q.Y[yk]).toFixed(1)}`).join(' ')}"/>`;
    for (const q of c) {
      const tip = `<title>${esc(q.arm)}: served ${q.served.toFixed(1)}/day, ${esc(yk)} ${q.Y[yk].toFixed(3)}</title>`;
      s += sr.mk === 'circle' ? `<circle cx="${px(q.served)}" cy="${py(q.Y[yk])}" r="4" fill="${sr.col}" class="ring">${tip}</circle>`
        : `<rect x="${px(q.served) - 4}" y="${py(q.Y[yk]) - 4}" width="8" height="8" fill="${sr.col}" class="ring">${tip}</rect>`;
    }
    // sparse direct labels: the two ends of each curve
    for (const q of [c[0], c[c.length - 1]]) s += `<text x="${px(q.served) + 6}" y="${py(q.Y[yk]) - 6}" class="lab">${sr.par}${q.param}</text>`;
  }
  const u = curves.find(q => q.family === 'U');
  if (u && Number.isFinite(u.Y[yk])) s += `<path d="M ${px(u.served)} ${py(u.Y[yk]) - 6} l 6 6 l -6 6 l -6 -6 z" fill="var(--u)" class="ring"><title>U: served ${u.served.toFixed(1)}/day, ${esc(yk)} ${u.Y[yk].toFixed(3)}</title></path><text x="${px(u.served) - 8}" y="${py(u.Y[yk]) - 8}" class="lab" text-anchor="end">U</text>`;
  return s + '</g>';
}
function svg(cfg, keys, title) {
  const curves = S.curves[cfg] || [];
  const W = 360, H = 300, top = 64, w = W * keys.length + 20;
  let s = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${H + top + 30}" viewBox="0 0 ${w} ${H + top + 30}" font-family="system-ui, -apple-system, Segoe UI, sans-serif">
<style>
svg { --bg:#fcfcfb; --t1:#0b0b0b; --t2:#52514e; --grid:#e6e5e1; --axis:#b9b8b3; --fence:#2a78d6; --ledger:#eb6834; --u:#7a7974; }
@media (prefers-color-scheme: dark) { svg { --bg:#1a1a19; --t1:#f0efec; --t2:#c3c2b7; --grid:#33332f; --axis:#55544f; --fence:#3987e5; --ledger:#d95926; --u:#9a9993; } }
.t0 { font-size:15px; font-weight:600; fill:var(--t1); } .t1 { font-size:12px; font-weight:600; fill:var(--t1); } .tk, .lab, .lg { font-size:11px; fill:var(--t2); }
.grid { stroke:var(--grid); stroke-width:1; } .axis { stroke:var(--axis); stroke-width:1; } .ring { stroke:var(--bg); stroke-width:2; }
</style>
<rect width="100%" height="100%" fill="var(--bg)"/>
<text x="10" y="20" class="t0">${esc(title)}</text>`;
  // legend
  let lx = 10;
  for (const sr of [...SER, { name: 'U (no control)', col: 'var(--u)', mk: 'diamond', dash: '' }]) {
    s += `<line x1="${lx}" x2="${lx + 22}" y1="40" y2="40" stroke="${sr.col}" stroke-width="2" ${sr.dash ? `stroke-dasharray="${sr.dash}"` : ''}/>`;
    s += sr.mk === 'circle' ? `<circle cx="${lx + 11}" cy="40" r="4" fill="${sr.col}"/>` : sr.mk === 'square' ? `<rect x="${lx + 7}" y="36" width="8" height="8" fill="${sr.col}"/>` : `<path d="M ${lx + 11} 34 l 6 6 l -6 6 l -6 -6 z" fill="${sr.col}"/>`;
    s += `<text x="${lx + 28}" y="44" class="lg">${esc(sr.name)}</text>`;
    lx += 28 + 6.2 * sr.name.length + 18;
  }
  keys.forEach((k, i) => { s += panel(curves, k, 10 + i * W, top, W, H); });
  s += `<text x="10" y="${H + top + 22}" class="tk">wf17-exploratory-post-audit. 30-day means (D30 x w08+w17, r0). Labels: a = alpha %, x = Shadow guaranteed share %. Hover a marker for its values.</text></svg>\n`;
  return s;
}
function ascii(cfg, yk) {
  const curves = (S.curves[cfg] || []).filter(c => Number.isFinite(c.Y[yk]));
  const Wc = 64, Hc = 16;
  const xs = curves.map(c => c.served), ys = curves.map(c => c.Y[yk]);
  const xa = Math.min(...xs), xb = Math.max(...xs), ya = Math.min(...ys), yb = Math.max(...ys);
  const g = Array.from({ length: Hc }, () => Array(Wc).fill(' '));
  for (const c of curves) {
    const ch = c.family === 'U' ? 'U' : SER.find(s => s.fam === c.family).ch;
    const i = Math.round((c.served - xa) / (xb - xa || 1) * (Wc - 1)), j = Hc - 1 - Math.round((c.Y[yk] - ya) / (yb - ya || 1) * (Hc - 1));
    g[j][i] = g[j][i] === ' ' || g[j][i] === ch ? ch : '*';
  }
  const lines = [`${cfg} | ${yk} (y) vs served/day (x). E=EOHZ(alpha) R=RCE(alpha) H=SHHZ(x) C=SHRC(x) U=U *=overlap`];
  g.forEach((row, j) => { const yv = yb - (yb - ya) * j / (Hc - 1); lines.push(`${yv.toFixed(2).padStart(9)} |${row.join('')}`); });
  lines.push(' '.repeat(10) + '+' + '-'.repeat(Wc));
  lines.push(' '.repeat(10) + ` ${xa.toFixed(1)}${' '.repeat(Math.max(1, Wc - 12))}${xb.toFixed(1)}`);
  return lines.join('\n');
}
const PRIM = ['Pchg (pp)', 'visRev60 /1000', 'breachDeadline60 /1000'], SEC = ['TV5/served (s)', 'pDispAny1 (pp)'];
const asc = ['wf17 R6 ASCII policy curves (wf17-exploratory-post-audit; 30-day means)'];
for (const cfg of ['INS', 'G2']) {
  fs.writeFileSync(path.join(outDir, `r6_${cfg}_primary.svg`), svg(cfg, PRIM, `R6 ${cfg}: policy curves vs served (primary metrics)`));
  fs.writeFileSync(path.join(outDir, `r6_${cfg}_secondary.svg`), svg(cfg, SEC, `R6 ${cfg}: policy curves vs served (secondary metrics)`));
  for (const k of [...PRIM, ...SEC]) asc.push('', ascii(cfg, k));
}
fs.writeFileSync(path.join(outDir, 'r6_ascii.txt'), asc.join('\n') + '\n');
console.log('plots written to', outDir);
