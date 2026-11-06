// wf17 engine17.js = wf15/runner/engine.js + the opt-in patch P3 closed-loop flat drift (opts.forecast.flatRatio;
// RUNPLAN 1.1). Every option at its default reproduces wf15. The research arms and options that no kept result uses are
// removed and rejected (see REMOVED below): arms NC OC RR FZ NCE OCE SHNC, shadowArms/pairs, trace, physMat, the G1/DR
// re-optimization roots, lock modes ratchetFloor/none, tieMode, reoptTie, P1 fence tolerance/audit (fenceTolMs, audit)
// and P2 G2 friction (g2PenaltyMs, g2MaxSwitches).
// Generalized screening engine derived from the exact RideBound cost-first emulator (wf4/sim/sim.js).
//
// DEFAULT = exact RideBound Runner v3 semantics (insertion-only generator, exact final-confirmation pickup-ETA locks,
// cost-first hard-vector selection, candidate-id-rank tie-break, 100-candidate pool retention). With every option at
// its default the engine reproduces wf4/sim/sim.js decision for decision (calibrate.js checks 4083/4083 archived
// riders).
//
// Prepared scenario interface P (fixtures.js for the RideBound fixtures, scenario.js for the generic format v1):
//   N, T0 (Float64Array N*N base travel ms), binMs, physFactor(bin, fromNode) (physical speed factor, FIFO across
//   bins), snapOf(bin) -> Float64Array N*N (Runner view: snapshot of the bin containing now, held flat),
//   snapHash(bin) (identity of the snapshot, enters candidate ids), reqs (sorted by arrival, protocol id; each with
//   stopP/stopD {r, kind, node, code, sid, body}), fleet [{id, node, capacity}], vehIdStr[], dwell, drainTimerMs,
//   clockGuardMs.
//
// Options (opts):
//   arm        the run's one arm {name, B (ms)}; names U RC PS DLHZ DLS (B), EO EOHZ RCE (alpha), SHF SHHZ SHRC (x).
//              Its admission rule (ADMIT) gates every candidate, and the plan selected under it is the trajectory.
//   reopt      { g2: cap (0 = off), g2Targets: max target vehicles (Infinity),
//                rootPairs: true (re-optimization roots are full alternative roots: single AND pair insertions,
//                as B4) }
//   lock       'exact' (RideBound) | 'ratchet' (pickup <= e)
//   poolCap    per-vehicle candidate pool cap incl. no-op (RideBound 100); re-optimization modes default to Infinity
//   Ties: candidate-id rank (RideBound); with re-optimization on, exactly tied plans first prefer fewer
//   re-optimization moves.
//   wallBudgetMs optional wall-clock budget for one run (default none). When exceeded the run throws an Error with
//              code 'WALL_BUDGET' (a truncated run is never returned as if complete). Only read when set, so exact
//              mode is unaffected.
//
// One decision (decide), in this order: re-project the kept routes -> generatePools (insertions per vehicle) ->
// generateRelocations (G2 moves) -> selectPlan (cost-first fleet selection) -> applyPlan (routes; new requests
// accepted, deferred or rejected) -> publish (revisions of the promised riders) -> openPromises (first promises of the
// riders accepted in the previous micro-step) -> scoreNewPromises (Shadow score and class).
'use strict';
const ID = require('./identity.js');

const PICK = 0, DROP = 1;
const ST_PENDING = 0, ST_ACCEPTED = 1, ST_WAITING = 2, ST_ONBOARD = 3, ST_DONE = 4, ST_REJECTED = 5;
const MAX_NEW_REQUESTS = 2;      // maximumNewRequestsPerVehicle
const MAX_WORK_UNITS = 10000;    // maximumExplorationWorkUnits (diagnostic only)
const ARM_CODES = { U: 0, RC: 1, PS: 2, DLHZ: 3, DLS: 4, EO: 8, EOHZ: 9, RCE: 10, SHF: 13, SHHZ: 14, SHRC: 15 };
// wf15: Elmi et al. 2026 Shadow Control. At the first promise every rider gets a shareability score sS
// (opts.sscore.score) and a frozen class aSH = opts.sscore.classOf(r) (0 guaranteed | 1 allowed); budget
// bSH = round(aSH * slk). SHF = strict deadline p <= p0 + bSH (forced vehicles as EO), SHHZ = Hoerl-Zwick floor,
// SHRC = Rule C ledger, all with bSH.
// wf13: Elmi et al. 2026 per-rider allowance B_r = round(alpha * slk_r), slk_r = residual drop slack at the first
// promise (pk0 + maxRide - p0). EO = Ordinary Control as a deadline p <= p0 + B_r (strict, forced vehicles as DLS);
// EOHZ adds the Hoerl-Zwick keep-route floor; RCE = Rule C ledger with the same B_r.
const brOf = (A, r) => Math.round(A.alpha * r.slk);
const isShadow = A => A.code >= 13 && A.code <= 15;                  // SHF SHHZ SHRC: budget bSH (Shadow class)
const isElmi = A => A.code >= 8 && A.code <= 10;                     // EO EOHZ RCE: budget brOf (alpha * slack)
const isStrictFence = A => A.code === 4 || A.code === 8 || A.code === 13;   // DLS EO SHF: strict deadline p0 + budget
// Per-rider budget (ms) of arm A for the promised rider r: unlimited for U, bSH (Shadow), brOf (Elmi), else B.
function budgetOf(A, r) {
  if (A.code === 0) return Infinity;
  if (isShadow(A)) return r.bSH;
  if (isElmi(A)) return brOf(A, r);
  return A.B;
}

// arms and options of the research engine that this copy no longer has: rejected, never silently ignored
const REMOVED_NOTE = 'removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)';
const REMOVED_ARMS = ['NC', 'OC', 'RR', 'FZ', 'NCE', 'OCE', 'SHNC'];
const REMOVED_OPTS = ['shadowArms', 'pairs', 'trace', 'tieMode', 'reoptTie', 'fenceTolMs', 'audit'];
const REMOVED_REOPT = ['g1', 'dr', 'g2PenaltyMs', 'g2MaxSwitches'];
function rejectRemoved(P, opts) {
  for (const k of REMOVED_OPTS) if (opts[k] !== undefined) throw new Error(`opts.${k}: ${REMOVED_NOTE}`);
  for (const k of REMOVED_REOPT) {
    if (opts.reopt && opts.reopt[k] !== undefined) throw new Error(`opts.reopt.${k}: ${REMOVED_NOTE}`);
  }
  if (opts.lock === 'ratchetFloor' || opts.lock === 'none') {
    throw new Error(`opts.lock '${opts.lock}': ${REMOVED_NOTE}`);
  }
  if (P.physMat) {
    throw new Error(`P.physMat (per-bin physical matrix, scenario travel.closure other than 'none'): ${REMOVED_NOTE}`);
  }
}

function armKey(a) {
  if (a.key) return a.key;
  if (a.name === 'U') return 'U';
  if (a.alpha !== undefined) return a.name + Math.round(a.alpha * 100);
  if (a.x !== undefined) return a.name + Math.round(a.x * 100);          // wf15 Shadow arms: x in integer percent
  return a.name + a.B / 1000;
}
function parseArm(key) {
  if (key === 'U') return { key, name: 'U', B: 0 };
  if (/^(NCE|OCE|SHNC|NC|OC|RR|FZ)/.test(key)) throw new Error(`arm ${key}: ${REMOVED_NOTE}`);
  let m = /^(EOHZ|EO|RCE)([0-9]+)$/.exec(key);
  if (m) return { key, name: m[1], alpha: Number(m[2]) / 100, B: 0 };
  m = /^(SHHZ|SHRC|SHF)([0-9]+)$/.exec(key);                            // wf15 Shadow arms (x = guaranteed share in %)
  if (m) return { key, name: m[1], x: Number(m[2]) / 100, B: 0 };
  m = /^(RC|PS|DLHZ|DLS)(\d+)$/.exec(key);
  if (!m) throw new Error('bad arm key ' + key);
  return { key, name: m[1], B: Number(m[2]) * 1000 };
}

// ADMIT[code]: may the promised rider r (ledger Labs = sum |c| so far, first promise p0, budgets brOf/bSH) get the
// candidate drop ETA p under arm A? e = r's drop ETA on the kept route re-projected now, so c = p - e is the dispatch
// change. One rule per kept arm; all pure.
const ADMIT = {
  0: () => true,                                                         // U    unconstrained
  1: (A, r, p, e) => r.Labs + Math.abs(p - e) <= A.B,                    // RC   Rule C ledger
  2: (A, r, p, e) => Math.abs(p - e) <= A.B,                             // PS   per-step cap
  3: (A, r, p, e) => p <= Math.max(r.p0 + A.B, e),                       // DLHZ deadline, Hoerl-Zwick floor
  4: (A, r, p) => p <= r.p0 + A.B,                                       // DLS  strict deadline (forced vehicles)
  8: (A, r, p) => p <= r.p0 + brOf(A, r),                                // EO   Elmi Ordinary Control, strict deadline
  9: (A, r, p, e) => p <= Math.max(r.p0 + brOf(A, r), e),                // EOHZ Elmi + Hoerl-Zwick floor
  10: (A, r, p, e) => r.Labs + Math.abs(p - e) <= brOf(A, r),            // RCE  Rule C with Elmi's per-rider budget
  13: (A, r, p) => p <= r.p0 + r.bSH,                                    // SHF  wf15 Shadow Control, strict deadline
  14: (A, r, p, e) => p <= Math.max(r.p0 + r.bSH, e),                    // SHHZ wf15 Shadow Control + Hoerl-Zwick floor
  15: (A, r, p, e) => r.Labs + Math.abs(p - e) <= r.bSH,                 // SHRC wf15 Shadow allocation + Rule C ledger
};
// Admission of a candidate drop ETA p for one promised rider r under arm A (see ADMIT). Pure; also used by tests.
function admits(A, r, p, e) {
  const rule = typeof A.code === 'number' ? ADMIT[A.code] : undefined;   // arm codes are numbers (ARM_CODES)
  if (rule === undefined) throw new Error('arm code');
  return rule(A, r, p, e);
}
// Ledger update after a published revision with dispatch change c: sum |c| (Rule C) and the net sum (per rider).
function charge(r, c) { r.Labs += Math.abs(c); r.net += c; }
function armOf(a) { return { ...a, key: armKey(a), code: ARM_CODES[a.name], B: a.B || 0 }; }

function treeSize(n, q) { // exploration nodes of one root with n mutable stops and q new requests (wf4 work formula)
  const P1 = (n + 1) * (n + 2) / 2, P2 = (n + 3) * (n + 4) / 2;
  return q === 0 ? 1 : q === 1 ? 2 + P1 : 1 + 2 * (1 + P1) + P1 * (1 + P2);
}

function simulate(P, opts = {}) {
  const { N, T0, reqs, dwell } = P;
  const BIN = P.binMs;
  rejectRemoved(P, opts);
  const A = armOf(opts.arm || { name: 'U', B: 0 });     // the run's arm: its admission rule gates every candidate
  if (A.code === undefined) {
    throw new Error(REMOVED_ARMS.includes(A.name) ? `arm ${A.name}: ${REMOVED_NOTE}` : 'unknown arm ' + A.name);
  }
  // wf15: opts.sscore = { score(ctx), classOf(r) } (Elmi's shareability score)
  const SSC = opts.sscore || null;
  const SH = isShadow(A);
  if (SH && !SSC) throw new Error('Shadow arms (SHF/SHHZ/SHRC) need opts.sscore');
  if (SH && typeof SSC.classOf !== 'function') throw new Error('Shadow arms need opts.sscore.classOf');
  if (SSC && typeof SSC.score !== 'function') throw new Error('opts.sscore.score must be a function');
  const STRICT = isStrictFence(A);
  const ro = Object.assign({ g2: 0, g2Targets: Infinity, rootPairs: true }, opts.reopt || {});
  const reoptOn = ro.g2 > 0;
  const lockMode = opts.lock || 'exact';
  if (lockMode !== 'exact' && lockMode !== 'ratchet') throw new Error('bad lock ' + lockMode);
  const RATCHET = lockMode === 'ratchet';
  const poolCap = opts.poolCap !== undefined ? opts.poolCap : (reoptOn ? Infinity : 100);
  const wantSlack = poolCap !== Infinity;          // the forward slack only feeds the portfolio retention
  const nR = reqs.length;
  const R = reqs.map((q, i) => ({
    i, q, state: ST_PENDING, veh: -1,
    t0: NaN, p0: NaN, pk0: NaN, pubP: NaN, pubPk: NaN, hasPromise: false,
    Labs: 0, net: 0, slk: NaN,                    // ledgers: sum|c|, sum c; slk = Elmi residual drop slack
    k: 0, S: 0, M: 0, zs: [], notif: 0, maxPost: 0, revCount: 0, cs: [], revs: [], switches: 0,
    pickup: NaN, A: NaN, latePickup: false,
    // wf15: shareability score, Shadow class/budget, relocations before the first promise (set at the first promise)
    sS: NaN, sSw: NaN, sSd: NaN, sInst: 0, aSH: -1, bSH: NaN, swP: -1,
  }));
  const V = P.fleet.map((f, vi) => ({
    vi, id: f.id, idStr: P.vehIdStr[vi], cap: f.capacity, pos: { node: f.node, from: -1, to: -1, phi: 0 }, route: [],
    load: 0, busyUntil: -Infinity, onboard: [], planVersion: 0, executed: [], moveMs: 0, loadedMoveMs: 0,
  }));
  const vehOrder = V.slice().sort((a, b) => (a.idStr < b.idStr ? -1 : 1));
  const diag = {
    decisions: 0, genDecisions: 0, workCapHits: 0, vehDecisions: 0, poolCapHits: 0, poolOver100: 0, maxSuffix: 0,
    microsteps: 0, deferred: 0, forcedVehicleDecisions: 0, tieDecisions: 0, tieBreaksByIdentity: 0, relocations: 0,
    reoptChosen: 0, candidates: 0, maxPool: 0, rootOnly: 0, rootOnlyLockOk: 0, rootOnlyCheaper: 0,
    rootOnlyCheaperLockOk: 0,
  };
  // physical travel time = T0 x factor, evaluated in the same floating-point order as wf4/sim/sim.js (exactness)
  const physF = (bin, from) => P.physFactor(bin, from);
  const binOf = t => Math.floor(t / BIN);
  // wf7 closed-loop forecast: opts.forecast = { prof: absolute factor profile per sim bin (held after the last),
  // clipPickup }. The Runner integrates every projected leg along weight(b) = 1 in the bin of "now" (fb0),
  // prof[b]/prof[fb0] after (promise_stretch.stretch semantics) instead of holding the current snapshot flat.
  // Unset = exact RideBound (time + w).
  const FC = opts.forecast || null;
  const fprof = FC ? FC.prof : null, fH = FC ? FC.prof.length - 1 : 0;
  let fb0 = 0, fl0 = 1;
  const legF = FC ? (time, w) => {
    if (w <= 0) return time + w;
    let tt = time, rem = w;
    for (let g = 0; g < 100000; g++) {
      const b = Math.floor(tt / BIN);
      // wf17 P3: closed-loop flat drift (CLF)
      const wt = b === fb0 ? 1 : (FC.flatRatio !== undefined ? FC.flatRatio : fprof[Math.min(b, fH)] / fl0);
      const seg = (b + 1) * BIN - tt, cov = seg / wt;
      if (cov >= rem) return tt + rem * wt;
      rem -= cov; tt = (b + 1) * BIN;
    }
    throw new Error('legF did not converge');
  } : null;
  const clipPk = !!(FC && FC.clipPickup);
  const fcCount = { quoteBeyondInitial: 0, quoteBeyondRevision: 0, clippedInitial: 0, clippedRevision: 0 };

  // ---------------------------------------------------------------- physical motion (FleetPy side; FIFO across bins)
  function timeToNextBoundary(v, t) {
    if (v.busyUntil > t + 1e-9) return v.busyUntil - t;
    if (v.route.length === 0) return Infinity;
    const b = binOf(t);
    const s = v.route[0].node;
    const p = v.pos;
    if (p.from < 0) return p.node === s ? 0 : T0[p.node * N + s] * physF(b, p.node);
    const rem = (1 - p.phi) * T0[p.from * N + p.to] * physF(b, p.from);
    return rem + (p.to === s ? 0 : T0[p.to * N + s] * physF(b, p.to));
  }
  function processStops(v, t) {
    while (v.route.length && v.pos.from < 0 && v.route[0].node === v.pos.node && !(v.busyUntil > t + 1e-9)) {
      const s = v.route.shift();
      v.executed.push(s);
      const r = R[s.r];
      if (s.kind === PICK) {
        r.state = ST_ONBOARD; r.pickup = Math.round(t); v.load += r.q.party || 1; v.onboard.push(s.r);
        if (r.pickup > r.q.latest) r.latePickup = true;
      } else {
        r.state = ST_DONE; r.A = Math.round(t); v.load -= r.q.party || 1; v.onboard.splice(v.onboard.indexOf(s.r), 1);
      }
      if (dwell > 0) v.busyUntil = t + dwell;
    }
  }
  function advance(v, t, tn) {
    let dt = tn - t;
    const b = binOf(t);
    if (v.busyUntil > t + 1e-9) {
      if (v.busyUntil <= tn + 1e-6) {
        dt = tn - v.busyUntil; v.busyUntil = -Infinity; processStops(v, tn);
        if (dt <= 1e-9) return;
      } else return;
    }
    if (v.route.length === 0) return;
    const s = v.route[0].node;
    const p = v.pos;
    if (p.from < 0) {
      if (p.node === s) { processStops(v, tn); return; }
      p.from = p.node; p.to = s; p.phi = 0; p.node = -1;
    }
    const full = T0[p.from * N + p.to] * physF(b, p.from);
    const need = (1 - p.phi) * full;
    if (dt < need - 1e-6) { p.phi += dt / full; v.moveMs += dt; if (v.load) v.loadedMoveMs += dt; return; }
    v.moveMs += need; if (v.load) v.loadedMoveMs += need;
    dt -= need;
    const at = p.to;
    p.node = at; p.from = -1; p.to = -1; p.phi = 0;
    if (at === s) {
      if (dt > 1e-3) throw new Error(`overshoot ${dt} at stop`);
      processStops(v, tn);
      return;
    }
    const full2 = T0[at * N + s] * physF(b, at);
    if (dt >= full2 - 1e-6) {
      if (dt - full2 > 1e-3) throw new Error('overshoot after pass-through');
      v.moveMs += dt; if (v.load) v.loadedMoveMs += dt;
      p.node = s; processStops(v, tn); return;
    }
    if (dt > 1e-9) {
      p.from = at; p.to = s; p.phi = dt / full2; p.node = -1;
      v.moveMs += dt; if (v.load) v.loadedMoveMs += dt;
    }
  }

  // ---------------------------------------------------------------- Runner view
  function startOf(v, tEval, snap) {
    const p = v.pos;
    let node, time = tEval;
    if (v.busyUntil > tEval) time = Math.round(v.busyUntil);
    if (p.from < 0) node = p.node;
    else {
      let pm = Math.floor(p.phi * 1000 + 1e-9);
      if (p.phi <= 0) node = p.from;
      else if (pm >= 1000) node = p.to;
      else {
        pm = Math.max(pm, 1);
        const x = snap[p.from * N + p.to] * (1000 - pm);
        const remMs = Math.floor(x / 1000) + (x % 1000 === 0 ? 0 : 1);
        time = legF ? legF(time, remMs) : time + remMs;
        node = p.to;
      }
    }
    return { node, time };
  }

  const tmpPk = new Float64Array(nR), tmpDr = new Float64Array(nR);
  // non-strict projection of a stop list (kept routes, publications, retention profiles)
  function project(v, stops, st, snap) {
    let time = st.time, cur = st.node;
    for (let k = 0; k < stops.length; k++) {
      const s = stops[k];
      const arr = legF ? (cur === s.node ? time : legF(time, snap[cur * N + s.node]))
        : time + (cur === s.node ? 0 : snap[cur * N + s.node]);
      let ss;
      if (s.kind === PICK) { ss = arr < R[s.r].q.earliest ? R[s.r].q.earliest : arr; tmpPk[s.r] = ss; }
      else { ss = arr; tmpDr[s.r] = ss; }
      time = ss + dwell;
      cur = s.node;
    }
    return time;
  }

  // strict evaluation of a changed candidate: -1 if physically/contractually infeasible; else completion time, with
  // EV.lockOk (pickup-lock regime, arm independent), the promised riders' drops (dropR/dropP, EV.nd) and EV.slack.
  const EV = { lockOk: true, nd: 0, slack: null };
  const dropR = new Int32Array(4096), dropP = new Float64Array(4096);
  const localSlack = [], waitArr = [];
  // kept-route ETAs re-projected at the start of each decision (pickup, drop), and the ETAs as applied (publications)
  const ePk = new Float64Array(nR).fill(NaN), eDr = new Float64Array(nR).fill(NaN);
  const pPk = new Float64Array(nR).fill(NaN), pDr = new Float64Array(nR).fill(NaN);
  function evalCand(v, stops, st, snap, withSlack) {
    let time = st.time, cur = st.node, load = v.load, lockOk = true, nd = 0;
    const len = stops.length;
    for (let k = 0; k < len; k++) {
      const s = stops[k];
      const r = R[s.r];
      const arr = legF ? (cur === s.node ? time : legF(time, snap[cur * N + s.node]))
        : time + (cur === s.node ? 0 : snap[cur * N + s.node]);
      let ss;
      if (s.kind === PICK) {
        if (arr > r.q.latest) return -1;
        ss = arr < r.q.earliest ? r.q.earliest : arr;
        load += r.q.party || 1;
        if (load > v.cap) return -1;
        tmpPk[s.r] = ss;
        if (withSlack) { localSlack[k] = r.q.latest - arr; waitArr[k] = ss - arr; }
        if (lockOk && r.hasPromise && r.state === ST_WAITING) {
          const e = ePk[s.r];
          if (RATCHET ? ss > e : ss !== e) lockOk = false;   // exact: pickup ETA unchanged; ratchet: not later
        }
      } else {
        ss = arr;
        const pt = r.state === ST_ONBOARD ? r.pickup : tmpPk[s.r];
        if (ss - pt > r.q.maxRide) return -1;
        load -= r.q.party || 1;
        tmpDr[s.r] = ss;
        if (withSlack) { localSlack[k] = r.q.maxRide - (ss - pt); waitArr[k] = 0; }
        if (r.hasPromise) { dropR[nd] = s.r; dropP[nd] = ss; nd++; }
      }
      time = ss + dwell;
      cur = s.node;
    }
    EV.lockOk = lockOk; EV.nd = nd;
    if (withSlack) { // ForwardSlackProfile.cs:198-235
      let cert = null;
      for (let k = len - 1; k >= 0; k--) {
        const prop = cert === null ? null : waitArr[k] + cert;
        cert = prop === null ? localSlack[k] : Math.min(localSlack[k], prop);
      }
      EV.slack = cert;
    } else EV.slack = null;
    return time;
  }
  // gate of the candidate last evaluated by evalCand: pickup locks kept and every promised drop admitted by the arm
  function gateOk() {
    if (!EV.lockOk) return false;
    for (let k = 0; k < EV.nd; k++) if (!admits(A, R[dropR[k]], dropP[k], eDr[dropR[k]])) return false;
    return true;
  }

  function candId(vi, stops, isNoOp, add, tEval) {
    const v = V[vi];
    return ID.candidateId(v.idStr, tEval, P.snapHash(binOf(tEval)), isNoOp ? v.planVersion : v.planVersion + 1,
      v.executed.length, add.map(ri => R[ri].q.rq), v.executed, stops);
  }
  // candidate id, computed once per candidate (the no-op of a vehicle keeps its route and plan version)
  const idOfCand = (c, tEval) => c.id || (c.id = candId(c.vi, c.route, c.noop === true, c.add, tEval));

  // CandidatePortfolioRetainer service-set-stability-portfolio-v1 (as in wf4/sim/sim.js)
  function retainPortfolio(v, pool, st, snap, tEval) {
    const idOf = c => idOfCand(c, tEval);
    const rankCmp = (a, b) => (b.add.length - a.add.length) || (a.cost - b.cost)
      || ((a.slack === null ? 0 : 1) - (b.slack === null ? 0 : 1)) || ((b.slack || 0) - (a.slack || 0))
      || (idOf(a) < idOf(b) ? -1 : idOf(a) > idOf(b) ? 1 : 0);
    const ranked = pool.slice().sort(rankCmp);
    ranked.forEach((c, i) => { c.rank = i; });
    const baseStops = v.route;
    project(v, baseStops, st, snap);
    const baseStart = new Map();
    baseStops.forEach(s => baseStart.set(s.sid, s.kind === PICK ? tmpPk[s.r] : tmpDr[s.r]));
    const baseIds = new Set(baseStops.map(s => s.sid));
    const profileOf = c => {
      if (c.prof) return c.prof;
      let prefix = 0;
      while (prefix < baseStops.length && prefix < c.route.length && baseStops[prefix].sid === c.route[prefix].sid) {
        prefix++;
      }
      let insBefore = 0, insBeforePick = 0, insBeforeStops = 0;
      for (const s of c.route) {
        if (!baseIds.has(s.sid)) { insBefore++; continue; }
        insBeforeStops += insBefore;
        if (s.kind === PICK) insBeforePick += insBefore;
      }
      project(v, c.route, st, snap);
      let maxShift = 0, totShift = 0, shifted = 0;
      for (const s of baseStops) {
        if (!c.route.includes(s)) continue; // G2 donor routes lack the relocated rider's stops
        const after = s.kind === PICK ? tmpPk[s.r] : tmpDr[s.r];
        const sh = Math.abs(baseStart.get(s.sid) - after);
        if (sh > maxShift) maxShift = sh;
        totShift += sh; if (sh > 0) shifted++;
      }
      return (c.prof = [-prefix, insBeforePick, maxShift, totShift, shifted, insBeforeStops]);
    };
    const profCmp = (a, b) => {
      const x = profileOf(a), y = profileOf(b);
      for (let i = 0; i < 6; i++) if (x[i] !== y[i]) return x[i] - y[i];
      return a.rank - b.rank;
    };
    const slots = poolCap - 1;
    const kept = [], keptSet = new Set();
    const add = c => { if (kept.length < slots && !keptSet.has(c)) { keptSet.add(c); kept.push(c); } };
    const tiers = [...new Set(ranked.map(c => c.add.length))].sort((a, b) => b - a);
    for (const tier of tiers) {
      const members = ranked.filter(c => c.add.length === tier);
      const groups = new Map();
      for (const c of members) {
        const key = c.add.map(ri => R[ri].q.rq).sort().join('|');
        if (!groups.has(key)) groups.set(key, []);
        groups.get(key).push(c);
      }
      const gl = [...groups.values()].sort((a, b) => a[0].rank - b[0].rank);
      for (const g of gl) add(g[0]);
      for (const g of gl) add(g.slice().sort(profCmp)[0]);
      for (const c of members) add(c);
      if (kept.length === slots) break;
    }
    return kept;
  }

  function insertionsOf(src, sp, sd) {
    const len = src.length, res = [];
    for (let i = 0; i <= len; i++) {
      for (let j = i + 1; j <= len + 1; j++) {
        const out = new Array(len + 2);
        let a = 0;
        for (let k = 0; k < len + 2; k++) out[k] = k === i ? sp : k === j ? sd : src[a++];
        res.push(out);
      }
    }
    return res;
  }

  // ---------------------------------------------------------------- one decision
  // dec = { tEval, bin, snap, starts, keepCost, Q }: the evaluation time (ms), its bin and Runner snapshot, every
  // vehicle's start {node, time} and kept-route cost, and the new requests offered in this decision.
  // A candidate is { vi, route, add (new requests it inserts), rep (null, or the G2 move it belongs to), cost, slack,
  // ok (it passed gateOk) }; the no-op of a vehicle (keep the route) has noop: true.

  // Re-project every kept route from now: the starts, the kept-route costs and the kept-route ETAs ePk/eDr.
  function projectKeptRoutes(tEval, snap) {
    const starts = [], keepCost = [];
    for (const v of V) {
      const st = startOf(v, tEval, snap);
      starts.push(st);
      const end = project(v, v.route, st, snap);
      keepCost.push(end - tEval);
      for (const s of v.route) { if (s.kind === PICK) ePk[s.r] = tmpPk[s.r]; else eDr[s.r] = tmpDr[s.r]; }
      for (const ri of v.onboard) ePk[ri] = R[ri].pickup;
    }
    return { starts, keepCost };
  }

  // The new requests offered now: the pending requests that have arrived, by latest pickup, arrival, request id;
  // at most MAX_NEW_REQUESTS of them.
  function nextRequests(tEval) {
    const pending = [];
    for (const r of R) if (r.state === ST_PENDING && r.q.arrival <= tEval) pending.push(r);
    pending.sort((a, b) => a.q.latest - b.q.latest || a.q.arrival - b.q.arrival || (a.q.rq < b.q.rq ? -1 : 1));
    return pending.slice(0, MAX_NEW_REQUESTS);
  }

  // Strict-fence arms (DLS EO SHF): a vehicle whose kept route already misses a promised rider's deadline p0 + budget
  // is forced to keep its route in this decision. forced[vi] is false for every vehicle under the other arms.
  function forcedVehicles() {
    return V.map(v => STRICT && v.route.some(s => s.kind === DROP && R[s.r].hasPromise
      && eDr[s.r] > R[s.r].p0 + budgetOf(A, R[s.r])));
  }

  // The candidates of one root route of vehicle v (InsertionCandidateGenerator): the root itself when it is a G2 donor
  // route, each new request inserted at every position pair, and (allowPairs) the second request inserted into every
  // feasible single of the first. Infeasible routes are dropped; the rest are gated (ok) and appended to out.
  function expandRoot(dec, v, root, st, out, allowPairs) {
    const { Q, snap, tEval } = dec;
    const mk = (route, add) => {
      const end = evalCand(v, route, st, snap, wantSlack);
      if (end < 0) return null;
      const c = { vi: v.vi, route, add, rep: root.rep, cost: end - tEval, slack: EV.slack, ok: gateOk() };
      out.push(c);
      if (root.rep && add.length === 0) {
        diag.rootOnly++;
        if (EV.lockOk) diag.rootOnlyLockOk++;
        if (c.cost < dec.keepCost[v.vi]) { diag.rootOnlyCheaper++; if (EV.lockOk) diag.rootOnlyCheaperLockOk++; }
      }
      return c;
    };
    if (root.rep) mk(root.route, []);
    const singles = Q.map(q => {
      const ok = [];
      for (const route of insertionsOf(root.route, q.q.stopP, q.q.stopD)) {
        const c = mk(route, [q.i]);
        if (c) ok.push(c);
      }
      return ok;
    });
    if (Q.length === 2 && allowPairs) {
      for (const c1 of singles[0]) {
        for (const route of insertionsOf(c1.route, Q[1].q.stopP, Q[1].q.stopD)) {
          mk(route, [Q[0].i, Q[1].i].sort((a, b) => a - b));
        }
      }
    }
  }

  // Insertion candidates per vehicle, the pool cut to poolCap by the portfolio retainer.
  function generatePools(dec) {
    const pools = [];
    for (const v of V) {
      const st = dec.starts[v.vi];
      const n = v.route.length;
      if (n > diag.maxSuffix) diag.maxSuffix = n;
      diag.vehDecisions++;
      const work = dec.Q.length ? treeSize(n, dec.Q.length) : 0;
      if (work > MAX_WORK_UNITS) diag.workCapHits++;
      const pool = [];
      expandRoot(dec, v, { route: v.route, rep: null }, st, pool, true);
      diag.candidates += pool.length;
      if (pool.length > diag.maxPool) diag.maxPool = pool.length;
      if (pool.length + 1 > 100) diag.poolOver100++;
      let kept = pool;
      if (pool.length + 1 > poolCap) { diag.poolCapHits++; kept = retainPortfolio(v, pool, st, dec.snap, dec.tEval); }
      pools.push(kept);
    }
    return pools;
  }

  // G2: move one waiting rider w from its vehicle a to another vehicle b, as one atomic move. Donor candidates aC = a's
  // route without w, expanded with the new requests; receiver candidates bC = w inserted into b's route. Up to ro.g2
  // riders per donor (earliest pickup first) and ro.g2Targets receivers (nearest start to w's origin first).
  function generateRelocations(dec) {
    const relocs = [];
    if (!reoptOn) return relocs;
    const { starts, snap, tEval } = dec;
    for (const a of V) {
      const elig = [];
      a.route.forEach((s, idx) => {
        if (s.kind !== PICK) return;
        const r = R[s.r];
        if ((r.state === ST_ACCEPTED || r.state === ST_WAITING) && r.veh === a.vi) elig.push({ ri: s.r, idx });
      });
      elig.sort((x, y) => x.idx - y.idx || (R[x.ri].q.rq < R[y.ri].q.rq ? -1 : 1));
      for (const { ri } of elig.slice(0, ro.g2)) {
        const q = R[ri].q;
        const rootA = { route: a.route.filter(s => s.r !== ri), rep: { type: 'G2from', ri } };
        const aC = [];
        expandRoot(dec, a, rootA, starts[a.vi], aC, ro.rootPairs);
        if (!aC.length || aC[0].add.length !== 0) continue; // donor route itself infeasible (strict) -> no relocation
        let targets = V.filter(b => b !== a);
        if (targets.length > ro.g2Targets) {
          const sn = starts.map(s => s.node);
          const nearer = (x, y) => (snap[sn[x.vi] * N + q.o] - snap[sn[y.vi] * N + q.o])
            || (x.idStr < y.idStr ? -1 : 1);
          targets = targets.slice().sort(nearer).slice(0, ro.g2Targets);
        }
        const bC = new Map();
        for (const b of targets) {
          const list = [];
          for (const route of insertionsOf(b.route, q.stopP, q.stopD)) {
            const end = evalCand(b, route, starts[b.vi], snap, false);
            if (end < 0) continue;
            const rep = { type: 'G2to', ri };
            list.push({ vi: b.vi, route, add: [], rep, cost: end - tEval, slack: null, ok: gateOk() });
          }
          if (list.length) bC.set(b.vi, list);
        }
        diag.candidates += aC.length;
        for (const l of bC.values()) diag.candidates += l.length;
        if (bC.size) relocs.push({ w: ri, a: a.vi, aC, bC });
      }
    }
    return relocs;
  }

  // ---------------------------------------------------------------- fleet selection
  // Candidates are grouped per vehicle by the new requests they add: '-' none, '0' the first, '1' the second,
  // '01' both.
  const keyOfAdd = (add, Q) => (add.length === 0 ? '-' : add.length === 2 ? '01' : (add[0] === Q[0].i ? '0' : '1'));
  const sizeOfKey = k => (k === '-' ? 0 : k.length);
  function addBest(obj, key, c) { // keep the cheapest candidates per group (all of them on an exact cost tie)
    const cur = obj[key];
    if (!cur || c.cost < cur.cost) obj[key] = { cost: c.cost, list: [c] };
    else if (c.cost === cur.cost) cur.list.push(c);
  }
  function pickFrom(entry, tEval) { // one candidate of a group: smallest candidate id (with G2 on, plain ones first)
    if (entry.pick) return entry.pick;
    let list = entry.list;
    if (list.length > 1 && reoptOn) { const plain = list.filter(c => c.rep === null); if (plain.length) list = plain; }
    if (list.length === 1) return (entry.pick = list[0]);
    let best = null;
    for (const c of list) if (best === null || idOfCand(c, tEval) < idOfCand(best, tEval)) best = c;
    return (entry.pick = best);
  }

  // Cost-first selection (SolverBackedFleetSelection + CP-SAT), solved by exact enumeration, which works because a
  // decision offers at most MAX_NEW_REQUESTS = 2 new requests: the most accepted requests, then the least summed cost;
  // exact ties go to the plan with the smallest candidate ids in vehicle-id order (with G2 on, fewer moves first).
  // Only admissible (ok) candidates of non-forced vehicles take part. Returns { ch: candidate per vehicle,
  // rel: G2 move or null }.
  function selectPlan(dec, forced, pools, relocs) {
    const { Q, keepCost, tEval } = dec;
    const NOOP = V.map(v => ({ vi: v.vi, noop: true, route: v.route, add: [], rep: null, cost: keepCost[v.vi],
      ok: true }));
    const bl = V.map(v => {   // bl[vi][key]: the cheapest admissible candidates of vehicle vi per group
      const o = { '-': { cost: keepCost[v.vi], list: [NOOP[v.vi]] } };
      if (forced[v.vi]) return o;
      for (const c of pools[v.vi]) if (c.ok) addBest(o, keyOfAdd(c.add, Q), c);
      return o;
    });
    let base = 0;   // every vehicle keeps its route
    for (const v of V) base += bl[v.vi]['-'].cost;
    let bestCount = 0, bestCost = base, ties = [{ items: [] }];
    const consider = (count, cost, plan) => {
      if (count > bestCount || (count === bestCount && cost < bestCost)) {
        bestCount = count; bestCost = cost; ties = [plan];
      } else if (count === bestCount && cost === bestCost) ties.push(plan);
    };
    const keys = Q.length === 2 ? ['0', '1', '01'] : Q.length === 1 ? ['0'] : [];
    // one vehicle takes one or both requests; then (two requests) two vehicles take one each
    for (const v of V) {
      for (const key of keys) {
        if (!bl[v.vi][key]) continue;
        consider(sizeOfKey(key), base - bl[v.vi]['-'].cost + bl[v.vi][key].cost, { items: [[v.vi, bl[v.vi][key]]] });
      }
    }
    if (Q.length === 2) {
      for (const v of V) for (const w of V) {
        if (v === w || !bl[v.vi]['0'] || !bl[w.vi]['1']) continue;
        const cost = base - bl[v.vi]['-'].cost - bl[w.vi]['-'].cost + bl[v.vi]['0'].cost + bl[w.vi]['1'].cost;
        consider(2, cost, { items: [[v.vi, bl[v.vi]['0']], [w.vi, bl[w.vi]['1']]] });
      }
    }
    // G2 plans: the move (donor a with group ka, receiver b), plus the remaining requests on one or two other vehicles
    for (const rl of relocs) {
      if (forced[rl.a]) continue;
      const aB = {};
      for (const c of rl.aC) if (c.ok) addBest(aB, keyOfAdd(c.add, Q), c);
      if (!aB['-']) continue; // the donor route must itself be admissible (a relocation is one atomic move)
      for (const [bvi, list] of rl.bC) {
        if (forced[bvi]) continue;
        const bB = {};
        for (const c of list) if (c.ok) addBest(bB, '-', c);
        if (!bB['-']) continue;
        for (const ka of ['-', ...keys]) {
          if (!aB[ka]) continue;
          const cnt = sizeOfKey(ka);
          const cost0 = base - bl[rl.a]['-'].cost - bl[bvi]['-'].cost + aB[ka].cost + bB['-'].cost;
          const items0 = [[rl.a, aB[ka]], [bvi, bB['-']]];
          const rel = { w: rl.w, a: rl.a, b: bvi };
          consider(cnt, cost0, { items: items0, rel });
          const remKeys = ka === '-' ? keys : (ka === '0' && Q.length === 2) ? ['1'] : (ka === '1') ? ['0'] : [];
          for (const c of V) {
            if (c.vi === rl.a || c.vi === bvi) continue;
            for (const kc of remKeys) {
              if (!bl[c.vi][kc]) continue;
              const cost = cost0 - bl[c.vi]['-'].cost + bl[c.vi][kc].cost;
              consider(cnt + sizeOfKey(kc), cost, { items: [...items0, [c.vi, bl[c.vi][kc]]], rel });
            }
          }
          if (ka === '-' && Q.length === 2) {
            for (const c of V) for (const d of V) {
              if (c === d || c.vi === rl.a || c.vi === bvi || d.vi === rl.a || d.vi === bvi) continue;
              if (!bl[c.vi]['0'] || !bl[d.vi]['1']) continue;
              const cost = cost0 - bl[c.vi]['-'].cost - bl[d.vi]['-'].cost + bl[c.vi]['0'].cost + bl[d.vi]['1'].cost;
              consider(cnt + 2, cost, { items: [...items0, [c.vi, bl[c.vi]['0']], [d.vi, bl[d.vi]['1']]], rel });
            }
          }
        }
      }
    }
    if (ties.length > 1) diag.tieDecisions++;
    // resolve: chosen candidate per vehicle
    const vecOf = plan => {
      const ch = V.map(v => null);
      for (const [vi, entry] of plan.items) ch[vi] = pickFrom(entry, tEval);
      for (const v of V) if (!ch[v.vi]) ch[v.vi] = pickFrom(bl[v.vi]['-'], tEval);
      return ch;
    };
    let winner = ties[0], wv = vecOf(ties[0]);
    if (ties.length > 1) {
      diag.tieBreaksByIdentity++;
      const score = ch => ch.reduce((a, c) => a + (c.rep ? 1 : 0), 0);
      for (let k = 1; k < ties.length; k++) {
        const cv = vecOf(ties[k]);
        if (reoptOn) {
          const sa = score(cv), sb = score(wv);
          if (sa !== sb) { if (sa < sb) { winner = ties[k]; wv = cv; } continue; }
        }
        for (const v of vehOrder) {
          const a = cv[v.vi], b = wv[v.vi];
          if (a === b) continue;
          const ia = idOfCand(a, tEval), ib = idOfCand(b, tEval);
          if (ia === ib) continue;
          if (ia < ib) { winner = ties[k]; wv = cv; }
          break;
        }
      }
    }
    return { ch: wv, rel: winner.rel || null };
  }

  // Apply the selected plan: the chosen routes, the accepted requests and the G2 move. Every other new request is
  // deferred if some admissible candidate of a non-forced vehicle served it (RollingCostPolicy.cs:166-221), else
  // rejected. Returns the accepted requests.
  function applyPlan(dec, plan, forced, pools, relocs) {
    const { Q } = dec;
    const chosenRoutes = plan.ch.map(c => (c.noop ? null : c));
    const accepted = [];
    for (const c of plan.ch) if (!c.noop) for (const ri of c.add) accepted.push(ri);
    for (const c of plan.ch) if (!c.noop && c.rep) diag.reoptChosen++;
    const reloc = plan.rel;
    const feasible = Q.map(() => false);
    const markFeasible = c => {
      if (!c.ok) return;
      for (const ri of c.add) { const qi = Q.findIndex(q => q.i === ri); if (qi >= 0) feasible[qi] = true; }
    };
    for (let vi = 0; vi < V.length; vi++) if (!forced[vi]) for (const c of pools[vi]) markFeasible(c);
    for (const rl of relocs) {
      if (forced[rl.a]) continue;
      let okB = false;
      for (const [bvi, l] of rl.bC) if (!forced[bvi] && l.some(c => c.ok)) okB = true;
      if (okB && rl.aC.some(c => c.add.length === 0 && c.ok)) for (const c of rl.aC) markFeasible(c);
    }
    Q.forEach((q, qi) => {
      if (accepted.includes(q.i)) return;
      if (feasible[qi]) { diag.deferred++; return; }
      q.state = ST_REJECTED;
    });
    for (const v of V) { const ch = chosenRoutes[v.vi]; if (ch) { v.route = ch.route.slice(); v.planVersion += 1; } }
    for (const ri of accepted) {
      R[ri].state = ST_ACCEPTED;
      R[ri].veh = chosenRoutes.findIndex(c => c && c.add.includes(ri));
    }
    if (reloc) { R[reloc.w].veh = reloc.b; R[reloc.w].switches++; diag.relocations++; }
    return accepted;
  }

  // Publications for the promised riders (CommitmentDecisionValidator.cs:558-597; PromiseDeltaCalculator.cs:63-155):
  // the routes as applied are projected (pPk/pDr); a rider whose drop or pickup ETA differs from the kept-route ETA
  // (e, ek) or from its last publication gets a revision; c = p - e is the dispatch change charged to the ledger.
  function publish(dec) {
    const { starts, snap, tEval } = dec;
    for (const v of V) {
      project(v, v.route, starts[v.vi], snap);
      for (const s of v.route) { if (s.kind === PICK) pPk[s.r] = tmpPk[s.r]; else pDr[s.r] = tmpDr[s.r]; }
      for (const ri of v.onboard) pPk[ri] = R[ri].pickup;
      const riders = new Set();
      for (const s of v.route) riders.add(s.r);
      for (const ri of riders) {
        const r = R[ri];
        if (!r.hasPromise) continue;
        const e = eDr[ri], p = pDr[ri], ek = ePk[ri], pk = pPk[ri];
        if (e === r.pubP && p === e && ek === r.pubPk && pk === ek) continue;
        let pkShown = pk;
        if (r.state !== ST_ONBOARD && pk > r.q.latest) {
          fcCount.quoteBeyondRevision++;
          if (clipPk) { pkShown = r.q.latest; fcCount.clippedRevision++; }
        }
        const c = p - e, z = p - r.pubP, x = e - r.pubP;
        r.revCount++;
        if (c !== 0) { r.k++; r.S += Math.abs(c); if (Math.abs(c) > r.M) r.M = Math.abs(c); r.cs.push(c); }
        charge(r, c);
        r.revs.push([tEval, x, c, z, e - r.p0, pk - r.pubPk, pk - ek, p, e, pkShown, ek]);
        if (z !== 0) r.zs.push(z);
        if (z > r.maxPost) r.maxPost = z;
        if (isMaterial(r.pubPkShown, pkShown, r.pubP, p)) r.notif++;
        r.pubP = p; r.pubPk = pk; r.pubPkShown = pkShown;
      }
    }
  }

  // The first promise (p0, pk0) of each rider in toOpen, as just published; slk = its residual drop slack.
  function openPromises(dec, toOpen) {
    for (const ri of toOpen) {
      const r = R[ri];
      r.hasPromise = true; r.t0 = dec.tEval; r.p0 = pDr[ri]; r.pk0 = pPk[ri]; r.pubP = r.p0; r.pubPk = r.pk0;
      r.slk = Math.max(0, r.pk0 + r.q.maxRide - r.p0);
      r.pk0Shown = r.pk0;
      if (r.pk0 > r.q.latest) {
        fcCount.quoteBeyondInitial++;
        if (clipPk) { r.pk0Shown = r.q.latest; fcCount.clippedInitial++; }
      }
      r.pubPkShown = r.pk0Shown;
    }
  }

  // wf15: shareability score of the riders promised now (passive: reads route/projection state only); under a Shadow
  // arm also their frozen class aSH and budget bSH.
  function scoreNewPromises(dec, toOpen) {
    const { tEval, bin, starts } = dec;
    const batchSet = new Set(toOpen);
    for (const ri of toOpen) {
      const r = R[ri], v = V[r.veh], st = starts[v.vi];
      const stops = v.route.map(s => {
        const j = R[s.r], T = s.kind === PICK ? pPk[s.r] : pDr[s.r];
        let h;
        if (s.kind === PICK) h = j.hasPromise ? 0 : Math.max(0, j.q.latest - T);       // promised pickups are locked
        else {
          const pt = j.state === ST_ONBOARD ? j.pickup : pPk[s.r];
          const phys = Math.max(0, pt + j.q.maxRide - T);
          // DC-1: a co-rider's managed drop headroom is capped by its remaining budget under the arm
          h = (!j.hasPromise || batchSet.has(s.r)) ? phys : Math.min(phys, Math.max(0, budgetOf(A, j) - j.Labs));
        }
        return { node: s.node, kind: s.kind, r: s.r, T, h, party: j.q.party || 1 };
      });
      const iP = v.route.indexOf(r.q.stopP), iD = v.route.indexOf(r.q.stopD);
      if (iP < 0 || iD < 0 || iD < iP) throw new Error('sscore: target stops not on route');
      // remaining part of the arc the vehicle is driving (same branch and rounding as startOf); the scorer integrates
      // it node by node like every later leg (SPEC 4.7, D9). null when the vehicle stands at a node.
      const pos = v.pos;
      let arc = null;
      if (pos.from >= 0 && pos.phi > 0) {
        const pm = Math.floor(pos.phi * 1000 + 1e-9);
        if (pm < 1000) arc = { from: pos.from, pm: Math.max(pm, 1) };
      }
      if (arc && pos.to !== st.node) throw new Error('sscore: arc end is not the vehicle start');
      r.swP = r.switches;                                             // G2 relocations made before the first promise
      const res = SSC.score({
        tEval, bin, start: st, arc, load0: v.load, cap: v.cap, stops, iP, iD, dwell, rq: r.q.rq, ri,
      });
      r.sS = res.S; r.sSw = res.Sw; r.sSd = res.Sd; r.sInst = res.n;
      if (!Number.isFinite(r.sS)) throw new Error('sscore: non-finite score');
    }
    if (SH) {
      for (const ri of toOpen) {
        const r = R[ri];
        r.aSH = SSC.classOf(r);                                         // 0 guaranteed | 1 allowed (frozen)
        if (r.aSH !== 0 && r.aSH !== 1) throw new Error('sscore: classOf must return 0 or 1');
        r.bSH = Math.round(r.aSH * r.slk);                              // identical to brOf with alpha in {0, 1}
      }
    }
  }

  function decide(t, toOpen) {
    diag.decisions++;
    const tEval = Math.round(t);
    const bin = binOf(t);
    const snap = P.snapOf(bin);
    if (FC) { fb0 = bin; fl0 = fprof[Math.min(bin, fH)]; }
    const { starts, keepCost } = projectKeptRoutes(tEval, snap);
    const dec = { tEval, bin, snap, starts, keepCost, Q: nextRequests(tEval) };
    let accepted = [];
    if (dec.Q.length || reoptOn) {
      if (dec.Q.length) diag.genDecisions++;
      const forced = forcedVehicles();
      const pools = generatePools(dec);
      const relocs = generateRelocations(dec);
      const plan = selectPlan(dec, forced, pools, relocs);
      if (forced.some(x => x)) diag.forcedVehicleDecisions += forced.filter(x => x).length;
      accepted = applyPlan(dec, plan, forced, pools, relocs);
    }
    publish(dec);
    openPromises(dec, toOpen);
    if (SSC && toOpen.length) scoreNewPromises(dec, toOpen);
    return accepted;
  }

  function isMaterial(oPk, nPk, oDr, nDr) { // MaterialRevisionRule (CommitmentPolicy.cs:98-115): 1 s raw, 60 s bucket
    return Math.abs(nPk - oPk) >= 1000 || Math.abs(nDr - oDr) >= 1000
      || Math.floor(oPk / 60000) !== Math.floor(nPk / 60000) || Math.floor(oDr / 60000) !== Math.floor(nDr / 60000);
  }

  function batch(t) {
    let toOpen = [];
    // FleetPy driver re-triggers while confirmations exist (wf4 guard was 6; never reached on Manhattan)
    const maxMicro = opts.maxMicrosteps || 1000;
    for (let micro = 0; micro < maxMicro; micro++) {
      if (micro > 0) diag.microsteps++;
      const acc = decide(t, toOpen);
      if (!acc.length) break;
      for (const ri of acc) R[ri].state = ST_WAITING;
      toOpen = acc;
      if (micro === maxMicro - 1) throw new Error('microsteps did not converge');
    }
  }

  // ---------------------------------------------------------------- event clock
  let t = 0, ai = 0;
  let nActive = 0;
  const wallEnd = opts.wallBudgetMs ? Date.now() + opts.wallBudgetMs : 0;
  batch(0);
  for (let guard = 0; guard < 5000000; guard++) {
    if (wallEnd && Date.now() > wallEnd) {
      const err = new Error(`wall budget ${opts.wallBudgetMs} ms exceeded at sim time ${t} ms`);
      err.code = 'WALL_BUDGET';
      throw err;
    }
    const nextArrival = ai < nR ? reqs[ai].arrival : null;
    let active = false;
    for (let k = nActive; k < nR; k++) {
      const s = R[k].state;
      if (s === ST_PENDING || s === ST_ACCEPTED || s === ST_WAITING || s === ST_ONBOARD) {
        active = true; nActive = k; break;
      }
    }
    if (!active) nActive = nR;
    const physical = V.some(v => v.route.length || v.load || v.busyUntil > t);
    if (nextArrival === null && !active && !physical) break;
    let tn = Infinity;
    for (const v of V) { const d = timeToNextBoundary(v, t); if (t + d < tn) tn = t + d; }
    const binStart = (Math.floor(t / BIN + 1e-12) + 1) * BIN;
    if (binStart < tn) tn = binStart;
    if (nextArrival !== null && nextArrival < tn) tn = nextArrival;
    if (nextArrival === null && t + P.drainTimerMs < tn) tn = t + P.drainTimerMs;
    for (const v of V) advance(v, t, tn);
    t = tn;
    while (ai < nR && reqs[ai].arrival <= t + 1e-6) ai++;
    batch(t);
    if (t > P.clockGuardMs) throw new Error('runaway clock');
  }
  const riders = R.filter(r => r.hasPromise).map(r => ({
    rq: r.q.rq, arrival: r.q.arrival, latest: r.q.latest, maxRide: r.q.maxRide, direct: r.q.direct,
    t0: r.t0, p0: r.p0, pk0: r.pk0, pk0Shown: r.pk0Shown, A: r.A, pickup: r.pickup, served: r.state === ST_DONE,
    veh: r.veh, S: r.S, k: r.k, M: r.M, net: r.net, zs: r.zs, notif: r.notif, maxPost: r.maxPost, cs: r.cs,
    revs: r.revs, revCount: r.revCount, latePickup: r.latePickup, switches: r.switches, slk: r.slk, Labs: r.Labs,
    ...(SSC ? { sS: r.sS, sSw: r.sSw, sSd: r.sSd, sInst: r.sInst, aSH: r.aSH, bSH: r.bSH, swP: r.swP } : {}),
  }));
  const byVeh = new Map();
  for (const r of riders) if (r.served) { if (!byVeh.has(r.veh)) byVeh.set(r.veh, []); byVeh.get(r.veh).push(r); }
  for (const r of riders) {
    r.shared = !!(r.served && byVeh.get(r.veh).some(o => o !== r && o.pickup < r.A && r.pickup < o.A));
  }
  const fleet = {
    moveMs: V.reduce((a, v) => a + v.moveMs, 0), loadedMoveMs: V.reduce((a, v) => a + v.loadedMoveMs, 0),
  };
  return { riders, rejected: R.filter(r => r.state === ST_REJECTED).length, requests: nR, diag, fleet, fcCount };
}

module.exports = {
  simulate, armKey, parseArm, treeSize, admits, charge, armOf, isShadow, isStrictFence,
  ST: { ST_PENDING, ST_ACCEPTED, ST_WAITING, ST_ONBOARD, ST_DONE, ST_REJECTED },
};
