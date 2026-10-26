// wf20/fullsys COPY of wf17/r1-identity-fence-pad/code/scripts/shcore.js (label wf20-fullsys-exploratory-post-audit). Added ONLY:
//   task.fc = 'N' | 'CL'. CL = the closed-loop prev4 forecast exactly as wf17 r3drive.js (opts.forecast = { prof, clipPickup: true },
//   prof = forecast.profile(day, w08 ? 32 : 68, 'prev4', wf7 accuracy-index-2019.json, 15)), set on the SAME opts object that carries
//   opts.sscore, so Shadow scoring/classification and the forecast run together in one engine17 simulation. Row fields mode (= fc),
//   panel, and sigR3 (r3drive.sigOf: rider fingerprint without the clipped shown pickup ETA) for the g2 gate. Nothing else changed.
// wf17 COPY (r1-identity-fence-pad) of wf15/scripts/shcore.js: engine17.js; opt-in fence tolerance / audit for the strict-fence arms
// (EO*, SHF*, DLS*: task.fenceTolMs, task.audit); row fields variant, fenceTolMs, audit, sig (rider fingerprint, same identity) and
// sigX (identity-free rider fingerprint keyed by the scenario request id). Every metric is metrics_sh.metrics() unchanged.
// wf15 worker-side core: one task = (cell, dispatcher config) x arms. Builds the cell's shareability-score context
// (full-network scale kappa*f_b, local->global node map, causal/oracle lambda tables), wires opts.sscore per arm and score
// mode, runs the engine, asserts the SPEC 9 T3/T7 invariants, and returns metric rows, per-rider dumps and (tests) hashes.
'use strict';
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const SC = require('../runner/scenario.js');
const E15 = require('../runner/engine17.js');
const SS = require('../runner/sscore.js');
const LAM = require('../runner/lambda.js');
const MET = require('./metrics_sh.js');
const LIB = require('./shlib.js');
const FC = require('./forecast.js');                                                   // wf20: forecast path (r3drive.js)
const INDEX_FILE = path.join(LIB.SP, 'wf7/c-forecast/results/accuracy-index-2019.json'); // wf20: same index as r3drive.js
let INDEX = null;
const LABEL = 'wf20-fullsys-exploratory-post-audit';
// wf20: r3drive.sigOf verbatim (rider fingerprint without the shown (clipped) pickup ETA revs[i][9])
const sigR3Of = res => sha(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.t0, r.p0, r.pk0, r.A, r.pickup,
  r.revs.map(v => v.filter((x, i) => i !== 9))])));

const OMEGA_P = 600000;
const NEW_FIELDS = ['sS', 'sSw', 'sSd', 'sInst', 'aSH', 'bSH', 'swP'];   // rider fields added by the wf15 patch (only with opts.sscore)
const sha = s => crypto.createHash('sha256').update(s).digest('hex');
let E13 = null;
const engineOf = name => {
  if (name === 'wf13') { if (!E13) E13 = require(path.join(LIB.SP, 'wf13', 'runner', 'engine.js')); return E13; }
  if (name && name !== 'engine17') throw new Error('engine ' + name);
  return E15;
};

function readCell(file, truncateMs) {
  const s = JSON.parse(fs.readFileSync(file, 'utf8'));
  const dir = path.dirname(file);
  const prov = JSON.parse(fs.readFileSync(path.resolve(dir, s.provenance.provenanceFile), 'utf8'));
  if (truncateMs !== undefined && truncateMs !== null) s.requests = s.requests.filter(q => q.arrivalMs < truncateMs);
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

function makeCellCtx(rc, NET, task, mode) {
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
  return { kappa, factors, globalOf, omegaP: OMEGA_P, strideMs: task.strideMs || 0, lam, mode };
}

function stripNew(res) {
  return { ...res, riders: res.riders.map(r => { const o = { ...r }; for (const k of NEW_FIELDS) delete o[k]; return o; }) };
}

// random allocation control (SPEC 6.4): u = first 8 hex digits of sha256(cellKey + '|' + rq) / 2^32
const uRandom = (cellKey, rq) => parseInt(sha(cellKey + '|' + rq).slice(0, 8), 16) / 4294967296;

// T3 invariants for Shadow runs (+ T3a recording for every scored run)
function checkInvariants(res, A, rec, tol = 0) {   // wf17: tol = the P1 fence tolerance of a strict-fence run (0 otherwise)
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
      if (A.code === 13 && !(p <= r.p0 + r.bSH + tol || c === 0)) bad.push('SHF admission violated ' + r.rq);   // wf17: tolerance-aware (P1)
    }
    if (A.code === 16 && r.net > r.bSH) bad.push('SHNC net above budget ' + r.rq);
  }
  for (const [rq, e] of rec) if (e.dup) bad.push('scored twice ' + rq);
  return bad;
}

// task: { file, cell, day, win, rs, set, part, config, arms, scoreMode ('off'|'causal'|'oracle'|'random'), thetas {armKey: theta},
//         pi, alphaS, strideMs, prior {O, D}, dump (true | [armKeys]), engine ('wf15'|'wf13'), hashes, cmpRef, truncateMs, repeat }
function runTask(task, NET) {
  const t0 = Date.now();
  const rc = readCell(task.file, task.truncateMs);
  const P = SC.prepareScenario(rc.s, LIB.RUN_OVERRIDES, rc.dir);
  const E = engineOf(task.engine);
  const mode = task.scoreMode || 'off';
  if (!['off', 'causal', 'oracle', 'random'].includes(mode)) throw new Error('scoreMode ' + mode);
  if (mode !== 'off' && task.engine === 'wf13') throw new Error('wf13 engine has no score hook');
  let cellCtx = null, scorer = null, refScorer = null;
  if (mode !== 'off') {
    cellCtx = makeCellCtx(rc, NET, task, mode === 'oracle' ? 'oracle' : 'causal');
    kappaCheck(P, cellCtx.globalOf, cellCtx.kappa, NET, task.cell);
    scorer = SS.makeScorer(NET, cellCtx);
    if (task.cmpRef) refScorer = SS.makeRefScorer(NET, cellCtx);
  }
  const rows = [], dump = [], hashes = {}, cmp = { n: 0, maxRel: 0, maxAbs: 0, instMismatch: 0, kMismatch: 0, arcCtx: 0 };
  // wf20: forecast mode (r3drive.js semantics); 'N' = no forecast (exact RideBound projection time + w)
  const fc = task.fc || 'N';
  if (fc !== 'N' && fc !== 'CL') throw new Error('fc ' + fc);
  let prof = null;
  if (fc === 'CL') {
    if (!INDEX) INDEX = JSON.parse(fs.readFileSync(INDEX_FILE, 'utf8'));
    prof = FC.profile(task.day, task.win === 'w08' ? 32 : 68, 'prev4', INDEX, 15);
    if (!prof) throw new Error('no forecast sources for ' + task.day);
  }
  const reps = task.repeat || 1;
  for (let rep = 0; rep < reps; rep++) for (const key of task.arms) {
    const arm = E.parseArm(key), A = E.armOf(arm);
    const SH = MET.isShadow(A);
    if (SH && mode === 'off') throw new Error('Shadow arm without scores');
    const o = { ...JSON.parse(JSON.stringify(LIB.CONFIGS[task.config])), arm, wallBudgetMs: 900000 };
    const STRICT = A.code === 4 || A.code === 8 || A.code === 13;   // wf17: strict-fence arms (P1 knobs apply only to these)
    if (STRICT && task.fenceTolMs) o.fenceTolMs = task.fenceTolMs;
    if (STRICT && task.audit) o.audit = true;
    if (fc === 'CL') o.forecast = { prof, clipPickup: true };   // wf20: as r3drive.js mode CL
    let rec = null, theta = null;
    const st0 = scorer ? { ns: scorer.stats.ns, calls: scorer.stats.calls, inst: scorer.stats.instants, arcCalls: scorer.stats.arcCalls, arcInst: scorer.stats.arcInstants } : null;
    if (mode !== 'off') {
      rec = new Map();
      const sscore = {
        score: ctx => {
          const res = scorer.score(ctx);
          if (refScorer) {
            const q = refScorer.score(ctx);
            cmp.n++;
            const d = Math.abs(q.S - res.S), rel = d / Math.max(1e-300, Math.abs(q.S));
            if (d > cmp.maxAbs) cmp.maxAbs = d;
            if (q.S !== 0 && rel > cmp.maxRel) cmp.maxRel = rel;
            if (Math.abs(q.Sw - res.Sw) > 1e-9 * Math.max(1, Math.abs(q.Sw)) || Math.abs(q.Sd - res.Sd) > 1e-9 * Math.max(1, Math.abs(q.Sd))) cmp.maxRel = Math.max(cmp.maxRel, 1);
            if (q.n !== res.n || q.nArc !== res.nArc) cmp.instMismatch++;
            if (ctx.arc) cmp.arcCtx++;
            if (q.kP !== res.kP || q.kK !== res.kK) cmp.kMismatch++;
          }
          const prev = rec.get(ctx.rq);
          if (prev) prev.dup = true; else rec.set(ctx.rq, { S: res.S });
          return res;
        },
      };
      if (SH) {
        let classOf;
        if (mode === 'random') {
          const pi = task.pi;
          if (!(pi >= 0 && pi <= 1)) throw new Error('random mode needs pi');
          classOf = r => (uRandom(task.cell, r.q.rq) < pi ? 0 : 1);
        } else {
          theta = LIB.thetaFromJson(task.thetas ? task.thetas[key] : undefined);
          if (typeof theta !== 'number' || Number.isNaN(theta)) throw new Error(`no theta for ${key} (${task.config} ${task.win})`);
          classOf = r => (r.sS <= theta ? 0 : 1);
        }
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
      inv = checkInvariants(res, A, rec, STRICT ? (task.fenceTolMs || 0) : 0);
      if (inv.length) throw new Error(`invariant violations ${task.cell} ${task.config} ${key}: ${inv.length} e.g. ${inv.slice(0, 3).join('; ')}`);
    }
    const msScore = scorer ? Number(scorer.stats.ns - st0.ns) / 1e6 : 0;
    const row = { panel: task.panel || null, part: task.part, set: task.set, cell: task.cell, day: task.day, win: task.win, rs: task.rs, config: task.config, mode: fc, arm: key,
      scoreMode: mode, x: SH ? A.x : null, theta: SH && mode !== 'random' ? LIB.thetaToJson(theta) : null, pi: SH && mode === 'random' ? task.pi : null,
      alphaS: mode !== 'off' ? task.alphaS : null, strideMs: mode !== 'off' ? (task.strideMs || 0) : null, engine: task.engine || 'engine17',
      variant: task.variant || null, fenceTolMs: STRICT ? (task.fenceTolMs || 0) : null, label: LABEL,
      ms, msScore: Math.round(msScore), scoreCalls: scorer ? scorer.stats.calls - st0.calls : 0, scoreInstants: scorer ? scorer.stats.instants - st0.inst : 0,
      scoreArcCalls: scorer ? scorer.stats.arcCalls - st0.arcCalls : 0, scoreArcInstants: scorer ? scorer.stats.arcInstants - st0.arcInst : 0,
      ...MET.metrics(res, A) };
    if (task.truncateMs !== undefined && task.truncateMs !== null) row.truncateMs = task.truncateMs;
    if (rep > 0) row.repeat = rep;
    row.audit = res.audit || null;
    row.sig = sha(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.p0, r.A, r.revs])));
    { const idOf = new Map(P.reqs.map(q => [q.rq, q.id]));
      row.sigX = sha(JSON.stringify(res.riders.map(r => [idOf.get(r.rq), r.served, r.veh, r.t0, r.p0, r.pk0, r.A, r.pickup, r.revs]).sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0)))); }
    row.diagTieBreaks = res.diag.tieBreaksByIdentity;
    row.sigR3 = sigR3Of(res);                                     // wf20: comparable to wf17 r3.ndjson 'sig'
    rows.push(row);
    const wantDump = task.dump === true || (Array.isArray(task.dump) && task.dump.includes(key));
    if (wantDump && mode !== 'off') {
      // run parameters on every dump row (with them, run_sh.rowKey of a dump row equals that of its metric row)
      const meta = { cell: task.cell, day: task.day, win: task.win, rs: task.rs, set: task.set, config: task.config, arm: key, scoreMode: mode,
        theta: row.theta, alphaS: row.alphaS, strideMs: row.strideMs, pi: row.pi };
      if (rep > 0) meta.repeat = rep;
      if (task.truncateMs !== undefined && task.truncateMs !== null) meta.truncateMs = task.truncateMs;
      for (const d of MET.dumpRows(res, A, meta)) dump.push(d);
    }
    if (task.hashes) {
      const strip = stripNew(res);
      hashes[key + (rep ? '#' + rep : '')] = { full: sha(JSON.stringify(res)), strip: sha(JSON.stringify(strip)),
        core: sha(JSON.stringify({ riders: strip.riders, rejected: strip.rejected, diag: strip.diag })) };
    }
  }
  return { rows, dump, hashes, cmp: refScorer ? cmp : null, ms: Date.now() - t0,
    lam: cellCtx ? { fallbacks: cellCtx.lam.fallbacks } : null, paths: NET.pathStats ? { ...NET.pathStats, size: NET.paths.size } : null };
}

module.exports = { runTask, readCell, lambdaInputs, makeCellCtx, kappaCheck, stripNew, uRandom, NEW_FIELDS, OMEGA_P };
