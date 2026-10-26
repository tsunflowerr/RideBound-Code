// wf20/fullsys gate checker (label wf20-fullsys-exploratory-post-audit). Exact comparisons, no tolerance.
//   g1  every F14 mode-N row of SHF/SHHZ/SHRC (x* 72 INS / 71 G2) equals the wf15 confirm row of the same cell|config|arm on every
//       field of the wf15 row except bookkeeping/timing (EXCL1). Extra (g1x): the same for EOHZ35/RCE35 mode-N rows.
//   g2  every EOHZ35/RCE35 row (N and CL, F14 and F16) equals the wf17 R3 row of the same panel|cell|config|mode|arm (flatRatio
//       null) on every field of the R3 row except EXCL2, AND sigR3 (rider fingerprint) === R3 sig.
//   g3  every SHRC row: breachLedger === 0, G_ledgerBreach === 0, A_ledgerBreach === 0 (the in-run assertion Labs <= bSH, i.e. the
//       ledger sum|c| <= B_r with zero tolerance for every rider, has already passed for every row that exists: shcore throws otherwise)
//   g4  (production) completeness: expected rows per panel|config|mode|arm; no duplicate keys
//   g5  (production, optional --det <gate rows>) determinism: gate-run rows equal production rows on every field except ms/msScore
// Usage: node gcheck.js --rows <rows.ndjson> --out <report.txt> [--expect 1] [--det <gaterows.ndjson>]
'use strict';
const fs = require('fs');
const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const WF15 = SP + '/wf15/results/confirm/main.ndjson', R3 = SP + '/wf17/r1-identity-fence-pad/results/r3.ndjson';
const read = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
const EXCL1 = new Set(['part', 'set', 'ms', 'msScore', 'engine', 'label', 'variant', 'fenceTolMs', 'audit', 'sig', 'sigX', 'diagTieBreaks', 'panel', 'sigR3']);
const EXCL2 = new Set(['label', 'check', 'ms', 'sig', 'flatRatio', 'panel']);
const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);

function main() {
  const rows = read(arg('rows')), out = [], P = s => { out.push(s); console.log(s); };
  P(`wf20-fullsys-exploratory-post-audit gate check ${new Date().toISOString()} rows ${rows.length} (${arg('rows')})`);
  const w15 = new Map(read(WF15).map(r => [[r.cell, r.config, r.arm].join('|'), r]));
  const r3 = new Map(read(R3).filter(r => r.flatRatio === null || r.flatRatio === undefined).map(r => [[r.panel, r.cell, r.config, r.mode, r.arm].join('|'), r]));
  let ok = true;
  const cmp = (name, list, refOf, excl, extra) => {
    let n = 0, bad = 0, noRef = 0; const ex = [];
    for (const r of list) {
      const ref = refOf(r);
      if (!ref) { noRef++; bad++; if (ex.length < 5) ex.push('no reference ' + r.cell + ' ' + r.config + ' ' + r.mode + ' ' + r.arm); continue; }
      n++;
      const diffs = Object.keys(ref).filter(k => !excl.has(k) && !eq(ref[k], r[k]));
      if (extra) { const e = extra(r, ref); if (e) diffs.push(e); }
      if (diffs.length) { bad++; if (ex.length < 5) ex.push(`${r.cell} ${r.config} ${r.mode} ${r.arm}: ${diffs.slice(0, 6).map(k => k + ' ' + JSON.stringify(ref[k]) + ' vs ' + JSON.stringify(r[k])).join('; ')}`); }
    }
    const pass = bad === 0 && list.length > 0;
    P(`${name}: rows ${list.length}, compared ${n}, mismatched ${bad - noRef}, no reference ${noRef} -> ${pass ? 'PASS' : list.length ? 'FAIL' : 'NO ROWS'}`);
    for (const e of ex) P('   ' + e);
    return pass;
  };
  const isSH = a => /^(SHF|SHHZ|SHRC)\d+$/.test(a);
  const xOK = r => !isSH(r.arm) || r.arm.endsWith(r.config === 'INS' ? '72' : '71');
  if (!rows.every(xOK)) { P('x* check: FAIL (Shadow arm with x other than 72 INS / 71 G2)'); ok = false; } else P('x* check: PASS (every Shadow row uses frozen x* 72 INS / 71 G2)');
  ok = cmp('g1  F14 N Shadow rows vs wf15 main', rows.filter(r => r.panel === 'F14' && r.mode === 'N' && isSH(r.arm)), r => w15.get([r.cell, r.config, r.arm].join('|')), EXCL1) && ok;
  cmp('g1x F14 N EOHZ35/RCE35 rows vs wf15 main (extra)', rows.filter(r => r.panel === 'F14' && r.mode === 'N' && !isSH(r.arm)), r => w15.get([r.cell, r.config, r.arm].join('|')), EXCL1);
  ok = cmp('g2  EOHZ35/RCE35 rows (N, CL) vs wf17 r3', rows.filter(r => r.arm === 'EOHZ35' || r.arm === 'RCE35'), r => r3.get([r.panel, r.cell, r.config, r.mode, r.arm].join('|')), EXCL2,
    (r, ref) => (r.sigR3 === ref.sig ? null : 'sigR3')) && ok;
  for (const m of ['N', 'CL']) {
    const sh = rows.filter(r => /^SHRC\d+$/.test(r.arm) && r.mode === m);
    const badL = sh.filter(r => r.breachLedger !== 0 || r.G_ledgerBreach !== 0 || r.A_ledgerBreach !== 0);
    const pass = sh.length > 0 && badL.length === 0;
    P(`g3  SHRC ${m}: rows ${sh.length}, rows with breachLedger/G_ledgerBreach/A_ledgerBreach != 0: ${badL.length} -> ${pass ? 'PASS' : sh.length ? 'FAIL' : 'NO ROWS'}`);
    ok = pass && ok;
  }
  if (arg('expect')) {
    const keyOf = r => [r.panel, r.cell, r.config, r.mode, r.arm].join('|');
    const seen = new Map(); let dups = 0; for (const r of rows) { const k = keyOf(r); if (seen.has(k)) dups++; seen.set(k, r); }
    const cnt = {}; for (const r of seen.values()) { const k = [r.panel, r.config, r.mode, r.arm.replace(/\d+$/, m => (isSH(r.arm) ? '' : m))].join('|'); cnt[k] = (cnt[k] || 0) + 1; }
    const want = { F14: 84, F16: 96 }; let bad = 0;
    for (const p of ['F14', 'F16']) for (const c of ['INS', 'G2']) for (const m of ['N', 'CL']) for (const a of ['SHF', 'SHHZ', 'SHRC', 'EOHZ35', 'RCE35']) {
      const k = [p, c, m, a].join('|'); if ((cnt[k] || 0) !== want[p]) { bad++; P(`   g4 count ${k}: ${cnt[k] || 0} (want ${want[p]})`); } }
    P(`g4  completeness: ${Object.keys(cnt).length} groups, wrong counts ${bad}, duplicate keys ${dups} -> ${bad === 0 && dups === 0 ? 'PASS' : 'FAIL'}`);
    ok = bad === 0 && dups === 0 && ok;
  }
  if (arg('det')) {
    const g = read(arg('det')), prod = new Map(rows.map(r => [[r.panel, r.cell, r.config, r.mode, r.arm].join('|'), r]));
    let n = 0, bad = 0; const ex = [];
    for (const r of g) { const p = prod.get([r.panel, r.cell, r.config, r.mode, r.arm].join('|')); if (!p) continue; n++;
      const d = Object.keys(r).filter(k => k !== 'ms' && k !== 'msScore' && !eq(r[k], p[k])); if (d.length) { bad++; if (ex.length < 5) ex.push(r.cell + ' ' + r.arm + ': ' + d.join(',')); } }
    P(`g5  determinism gate rows vs production: compared ${n}, differing ${bad} -> ${n > 0 && bad === 0 ? 'PASS' : 'FAIL'}`); for (const e of ex) P('   ' + e);
    ok = n > 0 && bad === 0 && ok;
  }
  P('OVERALL (g1, g2, g3' + (arg('expect') ? ', g4' : '') + (arg('det') ? ', g5' : '') + '): ' + (ok ? 'PASS' : 'FAIL'));
  fs.writeFileSync(arg('out'), out.join('\n') + '\n');
  process.exit(ok ? 0 : 1);
}
main();
