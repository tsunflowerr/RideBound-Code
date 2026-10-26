// wf21/n7 pre-plan evidence: wf17 R3 (non-Shadow EOHZ35/RCE35; already seen). Per panel x config x window: late60First/1000 and
// served-rider promise length (dumps, p0 - t0) for N, CL, CLF; linear length-scaling of CLF to CL's length (through 0 at N) gives a
// rough equal-length estimate of CL - PAD. Used ONLY to set prediction bands.
'use strict';
const fs = require('fs');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const rows = fs.readFileSync(SP + '/wf17/r1-identity-fence-pad/results/r3.ndjson', 'utf8').trim().split('\n').map(JSON.parse);
const PL = {}; for (const l of fs.readFileSync('pl_evidence_wf17_bypanel.txt', 'utf8').trim().split('\n')) { const p = l.trim().split(/\s+/); const k = p[0].split('|'); PL[k.slice(0, 5).join('|')] = +p[4]; }
const agg = {};
for (const r of rows) { if (!['EOHZ35', 'RCE35'].includes(r.arm)) continue; const k = [r.panel, r.config, r.win, r.arm, r.mode].join('|'); const a = (agg[k] ||= { late: 0, served: 0, tv: 0, vr: 0, n: 0 }); a.late += r.late60First; a.served += r.served; a.tv += r.sumTV5; a.vr += r.visRev60; a.n++; }
const rate = a => 1000 * a.late / a.served;
for (const p of ['F14', 'F16']) for (const c of ['INS', 'G2']) for (const w of ['w08', 'w17']) for (const arm of ['EOHZ35', 'RCE35']) {
  const k = [p, c, w, arm].join('|'); const N = agg[k + '|N'], CL = agg[k + '|CL'], F = agg[k + '|CLF'];
  const dL_CL = PL[k + '|CL'] - PL[k + '|N'], dL_F = PL[k + '|CLF'] - PL[k + '|N'];
  const dY_CL = rate(CL) - rate(N), dY_F = rate(F) - rate(N), padEst = dY_F * dL_CL / dL_F;
  console.log(`${k.padEnd(22)} dL CL ${dL_CL.toFixed(1)} CLF ${dL_F.toFixed(1)} | late/1000 CL-N ${dY_CL.toFixed(1)} CLF-N ${dY_F.toFixed(1)} | est PADeq-N ${padEst.toFixed(1)} -> CL-PADeq ${(dY_CL - padEst).toFixed(1)}`);
}
