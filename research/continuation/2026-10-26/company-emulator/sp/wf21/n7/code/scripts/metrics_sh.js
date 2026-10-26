// wf15 (SPEC 8.1, 8.2): metrics() of wf14/scripts/confirm.js (= wf13/scripts/pilot.js), byte-identical for every old arm,
// with ONE change: for Shadow arms (codes 13-16) budgetOf(r) = r.bSH (their per-rider budget; with A.B = 0 every rider would
// otherwise count as breached), and Shadow arms also report medBudget_s. New fields are appended after the old ones.
// X_switched (G/A classes) counts riders relocated by G2 AFTER their first promise (r.switches - r.swP > 0); relocations chosen
// in the micro-step before the promise (no class, budget, lock or admission yet) are counted in X_switchedPre. The old field
// switchedRiders (any relocation) is unchanged.
'use strict';

function hasReversal(zs, th) {
  let last = 0;
  for (const z of zs) { if (Math.abs(z) < th) continue; const s = Math.sign(z); if (last !== 0 && s !== last) return true; last = s; }
  return false;
}
const median = a => { if (!a.length) return NaN; const s = a.slice().sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };
const isShadow = A => A.code >= 13 && A.code <= 16;
const budgetOfFor = A => r => (isShadow(A) ? r.bSH : A.alpha !== undefined ? Math.round(A.alpha * r.slk) : (A.name === 'U' ? null : A.B));

// per-rider change statistics (SPEC 8.1 per-rider definitions); B = null means unlimited (U)
function riderStats(r, B) {
  let nDisp5 = 0, sumCpos5 = 0, dispUp1 = false, nDispAny1 = 0, nPk5 = 0;
  for (const v of r.revs) {
    const c = v[2];
    if (c >= 5000) { nDisp5++; sumCpos5 += c; }
    if (c >= 1000) dispUp1 = true;
    if (Math.abs(c) >= 1000) nDispAny1++;
    if (Math.abs(v[5]) >= 5000) nPk5++;
  }
  return {
    nDisp5, sumCpos5, dispUp5: nDisp5 > 0, dispUp1, dispAny1: nDispAny1 > 0, nDispAny1, nPk5,
    visUp60: r.zs.some(z => z >= 60000), vis60: r.zs.some(z => Math.abs(z) >= 60000), rev60: hasReversal(r.zs, 60000),
    ledgerBreach: B !== null && r.Labs > B + 1000,
    deadlineBreach60: B !== null && r.served && r.A > r.p0 + B + 60000,
    late60First: r.served && r.A - r.p0 > 60000,
  };
}

function metrics(res, A) {
  const R = res.riders.filter(r => r.served);
  const n = R.length;
  const budgetOf = budgetOfFor(A);
  const m = { requests: res.requests, promised: res.riders.length, served: n, rejected: res.rejected,
    late60First: 0, late120First: 0, late60Last: 0, early60Last: 0,
    pDisp: 0, sumCposDisp: 0, nDisp: 0, maxNDisp: 0, pVisUp60: 0, visRev60: 0, sumTV5: 0, tv5gt60: 0,
    sumS: 0, maxS: 0, breachLedger: 0, breachDeadline60: 0, sumBudget: 0, budgets: 0, switchedRiders: 0, sumWait: 0, sumRide: 0 };
  const brs = [], slks = [];
  for (const r of R) {
    const pLast = r.revs.length ? r.revs[r.revs.length - 1][7] : r.p0;
    if (r.A - r.p0 > 60000) m.late60First++;
    if (r.A - r.p0 > 120000) m.late120First++;
    if (r.A - pLast > 60000) m.late60Last++;
    if (r.A - pLast < -60000) m.early60Last++;
    let cpos = 0, nd = 0, S = 0;
    for (const v of r.revs) { const c = v[2]; S += Math.abs(c); if (c >= 5000) { cpos += c; nd++; } }
    if (nd > 0) { m.pDisp++; m.sumCposDisp += cpos; m.nDisp += nd; if (nd > m.maxNDisp) m.maxNDisp = nd; }
    if (r.zs.some(z => z >= 60000)) m.pVisUp60++;
    if (hasReversal(r.zs, 60000)) m.visRev60++;
    // shown drop value with a 5-s deadband (wf7 amendment A1, jitter-robust churn)
    let shown = r.p0, tv5 = 0;
    for (const v of r.revs) { const p = v[7]; if (Math.abs(p - shown) >= 5000) { tv5 += Math.abs(p - shown); shown = p; } }
    m.sumTV5 += tv5; if (tv5 > 60000) m.tv5gt60++;
    m.sumS += S; if (S > m.maxS) m.maxS = S;
    const B = budgetOf(r);
    if (B !== null) {
      m.sumBudget += B; m.budgets++;
      if (S > B + 1000) m.breachLedger++;
      if (r.A > r.p0 + B + 60000) m.breachDeadline60++;
      if (A.alpha !== undefined || isShadow(A)) brs.push(B);
    }
    slks.push(r.slk);
    if (r.switches > 0) m.switchedRiders++;
    m.sumWait += r.pickup - r.arrival; m.sumRide += r.A - r.pickup;
  }
  m.medBudget_s = brs.length ? median(brs) / 1000 : null;
  m.medSlack_s = median(slks) / 1000;
  m.forcedVehDecisions = res.diag.forcedVehicleDecisions;
  m.relocations = res.diag.relocations;
  m.fleetMove_h = res.fleet.moveMs / 3.6e6;
  m.fc = res.fcCount;
  // ------------------------------------------------------------------ wf15 additions (SPEC 8.1)
  m.pDispUp1 = 0; m.pDispAny1 = 0; m.pVis60 = 0; m.sumDirect = 0; m.nAllowedB = 0;
  const SH = isShadow(A);
  const CLS = { G: null, A: null };
  if (SH) for (const k of ['G', 'A']) CLS[k] = { n: 0, dispUp5: 0, nDisp5: 0, sumCpos5: 0, maxN5: 0, dispUp1: 0, dispAny1: 0, visUp60: 0, vis60: 0, rev60: 0, ledgerBreach: 0, deadlineBreach60: 0, late60First: 0, switched: 0, switchedPre: 0, sumWait: 0, sumRide: 0, sumDirect: 0 };
  for (const r of R) {
    const B = budgetOf(r), st = riderStats(r, B);
    if (st.dispUp1) m.pDispUp1++;
    if (st.dispAny1) m.pDispAny1++;
    if (st.vis60) m.pVis60++;
    m.sumDirect += r.direct;
    if (B === null || B > 0) m.nAllowedB++;
    if (SH) {
      const X = r.aSH === 0 ? CLS.G : r.aSH === 1 ? CLS.A : null;
      if (!X) throw new Error('metrics: Shadow arm rider without class');
      X.n++; if (st.dispUp5) X.dispUp5++; X.nDisp5 += st.nDisp5; X.sumCpos5 += st.sumCpos5; if (st.nDisp5 > X.maxN5) X.maxN5 = st.nDisp5;
      if (st.dispUp1) X.dispUp1++; if (st.dispAny1) X.dispAny1++; if (st.visUp60) X.visUp60++; if (st.vis60) X.vis60++; if (st.rev60) X.rev60++;
      if (st.ledgerBreach) X.ledgerBreach++; if (st.deadlineBreach60) X.deadlineBreach60++; if (st.late60First) X.late60First++;
      if (!(Number.isInteger(r.swP) && r.swP >= 0 && r.swP <= r.switches)) throw new Error('metrics: Shadow arm rider without switches-at-promise');
      if (r.switches - r.swP > 0) X.switched++;                // relocated after the first promise (S7)
      if (r.swP > 0) X.switchedPre++;                          // relocated before the first promise (descriptive)
      X.sumWait += r.pickup - r.arrival; X.sumRide += r.A - r.pickup; X.sumDirect += r.direct;
    }
  }
  const scored = res.riders.filter(r => r.sS !== undefined && r.sS !== null);
  if (scored.length) {
    m.nScored = scored.length;
    m.sumS_score = scored.reduce((a, r) => a + r.sS, 0);      // named sumS_score: the old field sumS (sum |c|) is kept unchanged
    m.sumSw = scored.reduce((a, r) => a + r.sSw, 0);
    m.sumSd = scored.reduce((a, r) => a + r.sSd, 0);
    m.medS = median(scored.map(r => r.sS));
    m.sumInst = scored.reduce((a, r) => a + r.sInst, 0);
  }
  if (SH) {
    for (const k of ['G', 'A']) for (const [f, v] of Object.entries(CLS[k])) m[k + '_' + f] = v;
    m.guaranteedShare = n ? CLS.G.n / n : null;
    const promG = res.riders.filter(r => r.aSH === 0).length;
    m.guaranteedSharePromised = res.riders.length ? promG / res.riders.length : null;
  }
  return m;
}

// SPEC 8.2 per-rider dump row (one per promised rider)
function dumpRows(res, A, meta) {
  const budgetOf = budgetOfFor(A);
  return res.riders.map((r, i) => {
    const B = budgetOf(r), st = riderStats(r, B);
    return { ...meta, i, rq8: r.rq.replace(/^rq-/, '').slice(0, 8), t0: r.t0, S: r.sS, Sw: r.sSw, Sd: r.sSd, nInst: r.sInst,
      cls: r.aSH === undefined ? null : r.aSH, slk: r.slk, B, nDisp5: st.nDisp5, nDispAny1: st.nDispAny1, sumCpos5: st.sumCpos5,
      vis60: st.vis60, visUp60: st.visUp60, rev60: st.rev60, dA_p0: r.served ? r.A - r.p0 : null, switches: r.switches, served: r.served,
      nPk5: st.nPk5, Labs: r.Labs, swP: r.swP === undefined ? null : r.swP, direct: r.direct };
  });
}

module.exports = { metrics, dumpRows, riderStats, hasReversal, isShadow, budgetOfFor };
