// wf21/n7 COPY of wf20/fullsys/code/scripts/shcore.js (label wf21-n7-exploratory-post-audit). Added ONLY:
//   task.fc = 'PAD' : equal-length flat pad THROUGH DISPATCH = engine17 patch P3 (wf17 CLF): opts.forecast = { prof, clipPickup: true,
//                     flatRatio: task.flat[armKey] }, prof = the CL profile (only its bin anchor is read when flatRatio is set).
//   task.fc = 'CLc' : closed loop with a CAUSAL index: prof = forecast.profile(day, s0, 'prev4', IDX.targets[day].F, 15), IDX read from
//                     task.fcIndex (built by wf21/n7/cidx/scripts/build_index.js); otherwise identical to CL.
//   Row fields (appended AFTER every existing field; nothing that decides anything changes): flatRatio, fcIndex, plN (promised
//   riders), plSumArr_ms (sum p0 - arrival), plSumT0_ms (sum p0 - t0), plServedN, plServedSumArr_ms, early60First (served riders with
//   A < p0 - 60 s), early60FirstG (guaranteed class, Shadow arms) and the label wf21-n7-exploratory-post-audit.
// wf20/fullsys COPY of wf17/r1-identity-fence-pad/code/scripts/shcore.js (label wf20-fullsys-exploratory-post-audit). Added ONLY:
//   task.fc = 'N' | 'CL'. CL = the closed-loop prev4 forecast exactly as wf17 r3drive.js (opts.forecast = { prof, clipPickup: true },
//   prof = forecast.profile(day, w08 ? 32 : 68, 'prev4', wf7 accuracy-index-2019.json, 15)), set on the SAME opts object that carries
//   opts.sscore, so Shadow scoring/classification and the forecast run together in one engine17 simulation. Row fields mode (= fc),
//   panel, and sigR3 (r3drive.sigOf: rider fingerprint without the clipped shown pickup ETA) for the g2 gate. Nothing else changed.
// wf17 COPY (r1-identity-fence-pad) of wf15/scripts/shcore.js: engine17.js; row fields variant, fenceTolMs, audit, sig (rider
// fingerprint, same identity) and sigX (identity-free rider fingerprint keyed by the scenario request id). Every metric is
// metrics_sh.metrics() unchanged. The opt-in fence tolerance and audit of the strict-fence arms (EO*, SHF*, DLS*) are removed (see
// engine.js); their row fields stay so the row layout is unchanged: fenceTolMs = 0 for those arms (else null), audit = null.
// wf15 worker-side core: one task = (cell, dispatcher config) x arms. Builds the cell's shareability-score context
// (full-network scale kappa*f_b, local->global node map, causal lambda tables), wires opts.sscore per arm, runs the engine,
// asserts the SPEC 9 T3/T7 invariants, and returns the metric rows.
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const SC = require('./scenario.js');
const E = require('./engine.js');
const SS = require('./sscore.js');
const LAM = require('./lambda.js');
const MET = require('./metrics.js');
const LIB = require('./shlib.js');
const FC = require('./forecast.js');                                                   // wf20: forecast path (r3drive.js)
const indexFile = () => path.join(LIB.SP, 'wf7/c-forecast/results/accuracy-index-2019.json'); // wf20: same index as r3drive.js (under --data)
let INDEX = null;
const LABEL = 'wf21-n7-exploratory-post-audit';
const CIDX = new Map();                                                               // wf21: causal index files (CLc), by path
// wf20: r3drive.sigOf verbatim (rider fingerprint without the shown (clipped) pickup ETA revs[i][9])
const sigR3Of = res => sha(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.t0, r.p0, r.pk0, r.A, r.pickup,
  r.revs.map(v => v.filter((x, i) => i !== 9))])));

const OMEGA_P = 600000;
const sha = s => crypto.createHash('sha256').update(s).digest('hex');

function readCell(file) {
  const s = JSON.parse(fs.readFileSync(file, 'utf8'));
  const dir = path.dirname(file);
  const prov = JSON.parse(fs.readFileSync(path.resolve(dir, s.provenance.provenanceFile), 'utf8'));
  return { s, prov, dir };
}

// lambda inputs from the cell's own request stream (all requests, rejected ones included)
function lambdaInputs(s, NET) {
  const idx = new Map(s.nodes.map((n, i) => [n.id, Number(n.id.slice(1))]));
  const n = s.requests.length, arrivals = new Float64Array(n), zO = new Int32Array(n), zD = new Int32Array(n);
  s.requests.forEach((q, k) => { arrivals[k] = q.arrivalMs; zO[k] = NET.zoneIdx[idx.get(q.origin)]; zD[k] = NET.zoneIdx[idx.get(q.destination)]; });
  return { arrivals, zO, zD };
}

// SPEC 4.2 load-time assertion: T0[i][j] === max(1, round(kappa * D[g_i][g_j] * 1000)) on 1000 seeded random pairs
function kappaCheck(P, globalOf, kappa, NET, seedStr) {
  let h = parseInt(sha('wf15|kappa|' + seedStr).slice(0, 8), 16) >>> 0 || 1;
  const rnd = () => { h ^= h << 13; h >>>= 0; h ^= h >>> 17; h ^= h << 5; h >>>= 0; return h / 4294967296; };
  const n = P.N;
  for (let t = 0; t < 1000; t++) {
    const i = Math.floor(rnd() * n), j = Math.floor(rnd() * n);
    const want = i === j ? 0 : Math.max(1, Math.round(kappa * NET.D[globalOf[i] * NET.N + globalOf[j]] * 1000));
    if (P.T0[i * n + j] !== want) throw new Error(`kappa check failed at ${i},${j}: T0 ${P.T0[i * n + j]} vs ${want}`);
  }
}

function makeCellCtx(rc, NET, task) {
  const { s, prov } = rc;
  if (s.service.pickupWindowMs !== OMEGA_P) throw new Error('pickup window is not 600 s');
  const globalOf = Int32Array.from(s.nodes.map(n => Number(n.id.slice(1))));
  const kappa = prov.travel.F_windowStart, factors = s.travel.profile.factors;
  const li = lambdaInputs(s, NET);
  if (!task.prior) throw new Error('task.prior missing for ' + task.cell);
  if (!(task.alphaS >= 0 && task.alphaS <= 1)) throw new Error('task.alphaS missing');
  const lam = LAM.makeLambda({ nZones: NET.nZones, zoneSize: NET.zoneSize, N: NET.N, arrivals: li.arrivals, zO: li.zO, zD: li.zD,
    priorO: task.prior.O, priorD: task.prior.D, phi: prov.sampling.sampled / prov.sampling.eligible, sampled: prov.sampling.sampled,
    alpha: task.alphaS, intervalMs: SS.IV_MS });
  return { kappa, factors, globalOf, omegaP: OMEGA_P, strideMs: task.strideMs || 0, lam, mode: 'causal' };
}

// T3 invariants for Shadow runs (+ T3a recording for every scored run)
function checkInvariants(res, A, rec) {
  const SH = MET.isShadow(A);
  const bad = [];
  for (const r of res.riders) {
    const e = rec.get(r.rq);
    if (!e) { bad.push('not scored ' + r.rq); continue; }
    if (!Object.is(e.S, r.sS)) bad.push('score changed ' + r.rq);
    if (!(Number.isInteger(r.swP) && r.swP >= 0 && r.swP <= r.switches)) bad.push('switches at promise not in 0..switches ' + r.rq);
    if (!SH) continue;
    if (e.a === undefined || e.a !== r.aSH || e.b !== r.bSH) bad.push('class/budget changed ' + r.rq);
    if (!(r.bSH === 0 || r.bSH === Math.round(r.slk))) bad.push('bSH not in {0, round(slk)} ' + r.rq);
    if (r.aSH === 0 && r.bSH !== 0) bad.push('guaranteed rider with budget ' + r.rq);
    if (A.code === 15) { if (r.Labs > r.bSH) bad.push('SHRC ledger above budget ' + r.rq); if (r.aSH === 0 && r.Labs !== 0) bad.push('SHRC guaranteed rider moved ' + r.rq); }
    for (const v of r.revs) {
      const p = v[7], e2 = v[8], c = v[2];
      if (A.code === 14 && !(p <= Math.max(r.p0 + r.bSH, e2))) bad.push('SHHZ admission violated ' + r.rq);
      if (A.code === 13 && !(p <= r.p0 + r.bSH || c === 0)) bad.push('SHF admission violated ' + r.rq);
    }
  }
  for (const [rq, e] of rec) if (e.dup) bad.push('scored twice ' + rq);
  return bad;
}

// task: { file, cell, day, win, rs, set, part, config, arms, scoreMode ('off'|'causal'), thetas {armKey: theta}, alphaS, strideMs,
//         prior {O, D}, fc, flat, fcIndex, panel, variant }
function runTask(task, NET) {
  const t0 = Date.now();
  const rc = readCell(task.file);
  const P = SC.prepareScenario(rc.s, LIB.RUN_OVERRIDES, rc.dir);
  const mode = task.scoreMode || 'off';
  if (!['off', 'causal'].includes(mode)) throw new Error('scoreMode ' + mode);
  let cellCtx = null, scorer = null;
  if (mode !== 'off') {
    cellCtx = makeCellCtx(rc, NET, task);
    kappaCheck(P, cellCtx.globalOf, cellCtx.kappa, NET, task.cell);
    scorer = SS.makeScorer(NET, cellCtx);
  }
  const rows = [];
  // wf20: forecast mode (r3drive.js semantics); 'N' = no forecast (exact RideBound projection time + w)
  const fc = task.fc || 'N';
  if (!['N', 'CL', 'PAD', 'CLc'].includes(fc)) throw new Error('fc ' + fc);   // wf21: PAD and CLc added
  let prof = null;
  if (fc === 'CL' || fc === 'PAD') {                                       // wf21: PAD carries the CL profile (anchor only)
    if (!INDEX) INDEX = JSON.parse(fs.readFileSync(indexFile(), 'utf8'));
    prof = FC.profile(task.day, task.win === 'w08' ? 32 : 68, 'prev4', INDEX, 15);
    if (!prof) throw new Error('no forecast sources for ' + task.day);
  }
  if (fc === 'CLc') {                                                       // wf21: causal index, one entry per target day
    if (!task.fcIndex) throw new Error('CLc needs task.fcIndex');
    if (!CIDX.has(task.fcIndex)) CIDX.set(task.fcIndex, JSON.parse(fs.readFileSync(task.fcIndex, 'utf8')));
    const T = CIDX.get(task.fcIndex).targets[task.day];
    if (!T) throw new Error('causal index has no target ' + task.day);
    for (const sd of Object.keys(T.F)) if (!(sd < task.day)) throw new Error('causal index for ' + task.day + ' holds a day >= target: ' + sd);
    prof = FC.profile(task.day, task.win === 'w08' ? 32 : 68, 'prev4', T.F, 15);
    if (!prof) throw new Error('no causal forecast sources for ' + task.day);
  }
  for (const key of task.arms) {
    const arm = E.parseArm(key), A = E.armOf(arm);
    const SH = MET.isShadow(A);
    if (SH && mode === 'off') throw new Error('Shadow arm without scores');
    const o = { ...JSON.parse(JSON.stringify(LIB.CONFIGS[task.config])), arm, wallBudgetMs: 900000 };
    const STRICT = A.code === 4 || A.code === 8 || A.code === 13;   // wf17: strict-fence arms (row field fenceTolMs = 0)
    if (fc === 'CL' || fc === 'CLc') o.forecast = { prof, clipPickup: true };   // wf20: as r3drive.js mode CL (wf21: CLc = same, causal prof)
    let flatRatio = null;
    if (fc === 'PAD') {                                                     // wf21: flat pad through dispatch (wf17 CLF semantics)
      flatRatio = task.flat ? task.flat[key] : undefined;
      if (!(typeof flatRatio === 'number' && flatRatio > 0.5 && flatRatio < 2)) throw new Error('PAD needs task.flat[' + key + ']');
      o.forecast = { prof, clipPickup: true, flatRatio };
    }
    let rec = null, theta = null;
    const st0 = scorer ? { ns: scorer.stats.ns, calls: scorer.stats.calls, inst: scorer.stats.instants, arcCalls: scorer.stats.arcCalls, arcInst: scorer.stats.arcInstants } : null;
    if (mode !== 'off') {
      rec = new Map();
      const sscore = {
        score: ctx => {
          const res = scorer.score(ctx);
          const prev = rec.get(ctx.rq);
          if (prev) prev.dup = true; else rec.set(ctx.rq, { S: res.S });
          return res;
        },
      };
      if (SH) {
        theta = LIB.thetaFromJson(task.thetas ? task.thetas[key] : undefined);
        if (typeof theta !== 'number' || Number.isNaN(theta)) throw new Error(`no theta for ${key} (${task.config} ${task.win})`);
        const classOf = r => (r.sS <= theta ? 0 : 1);
        sscore.classOf = r => {
          const a = classOf(r), e = rec.get(r.q.rq);
          if (!e || e.a !== undefined) throw new Error('classOf before score or twice');
          e.a = a; e.b = Math.round(a * r.slk);
          return a;
        };
      }
      o.sscore = sscore;
    }
    const ts = Date.now();
    const res = E.simulate(P, o);
    const ms = Date.now() - ts;
    let inv = [];
    if (rec) {
      inv = checkInvariants(res, A, rec);
      if (inv.length) throw new Error(`invariant violations ${task.cell} ${task.config} ${key}: ${inv.length} e.g. ${inv.slice(0, 3).join('; ')}`);
    }
    const msScore = scorer ? Number(scorer.stats.ns - st0.ns) / 1e6 : 0;
    const row = { panel: task.panel || null, part: task.part, set: task.set, cell: task.cell, day: task.day, win: task.win, rs: task.rs, config: task.config, mode: fc, arm: key,
      scoreMode: mode, x: SH ? A.x : null, theta: SH ? LIB.thetaToJson(theta) : null, pi: null,   // pi: share of the removed random mode
      alphaS: mode !== 'off' ? task.alphaS : null, strideMs: mode !== 'off' ? (task.strideMs || 0) : null, engine: 'engine17',
      variant: task.variant || null, fenceTolMs: STRICT ? 0 : null, label: LABEL,
      ms, msScore: Math.round(msScore), scoreCalls: scorer ? scorer.stats.calls - st0.calls : 0, scoreInstants: scorer ? scorer.stats.instants - st0.inst : 0,
      scoreArcCalls: scorer ? scorer.stats.arcCalls - st0.arcCalls : 0, scoreArcInstants: scorer ? scorer.stats.arcInstants - st0.arcInst : 0,
      ...MET.metrics(res, A) };
    row.audit = null;                                             // field of the removed fence audit (layout unchanged)
    row.sig = sha(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.p0, r.A, r.revs])));
    { const idOf = new Map(P.reqs.map(q => [q.rq, q.id]));
      row.sigX = sha(JSON.stringify(res.riders.map(r => [idOf.get(r.rq), r.served, r.veh, r.t0, r.p0, r.pk0, r.A, r.pickup, r.revs]).sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0)))); }
    row.diagTieBreaks = res.diag.tieBreaksByIdentity;
    row.sigR3 = sigR3Of(res);                                     // wf20: comparable to wf17 r3.ndjson 'sig'
    // wf21: appended fields (promise length, earliness vs the first promise); computed from the finished run only
    row.flatRatio = flatRatio;
    row.fcIndex = fc === 'CLc' ? path.basename(task.fcIndex) : null;
    { let plSumArr = 0, plSumT0 = 0, sN = 0, sSum = 0, e60 = 0, e60G = 0;
      for (const r of res.riders) {
        plSumArr += r.p0 - r.arrival; plSumT0 += r.p0 - r.t0;
        if (r.served) { sN++; sSum += r.p0 - r.arrival; if (r.A - r.p0 < -60000) { e60++; if (SH && r.aSH === 0) e60G++; } }
      }
      row.plN = res.riders.length; row.plSumArr_ms = plSumArr; row.plSumT0_ms = plSumT0; row.plServedN = sN; row.plServedSumArr_ms = sSum;
      row.early60First = e60; row.early60FirstG = SH ? e60G : null; }
    rows.push(row);
  }
  return { rows, ms: Date.now() - t0,
    lam: cellCtx ? { fallbacks: cellCtx.lam.fallbacks } : null, paths: NET.pathStats ? { ...NET.pathStats, size: NET.paths.size } : null };
}

module.exports = { runTask, readCell, lambdaInputs, makeCellCtx, kappaCheck, OMEGA_P };
