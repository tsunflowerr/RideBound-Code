// ADR-089 GĐ4/GĐ6: emulator side of the final real-system comparison (owner 2026-11-03: F14 panel, G2, two arms).
//   OURS    G2 SHRC71 + closed-loop forecast (CL, prev4, clipPickup)   = report "Shadow + luật C + dự báo, có gán lại"
//   THEIRS  G2 SHHZ71, no forecast (N)                                 = report "Shadow + hàng rào nới, không dự báo"
// The Shadow and forecast wiring is shcore.js's (wf20/fullsys, the run behind the report): makeCellCtx (scale kappa*f_b,
// local->global node map, causal lambda with the wf15 prior, alphaS 0.15, stride 0), the score wrapper and
// classOf = (sS <= theta ? 0 : 1) with the frozen theta (wf15/frozen/shadow-params.json), forecast = forecast.profile(day,
// w08 ? 32 : 68, 'prev4', accuracy-index-2019.json, 15) with clipPickup. Nothing of the engines is changed.
//
// Modes
//   adapter  the panel cells x 2 arms (ARMS) on the full-node fixtures (convert_via_full.py) -> rider predictions (same columns as
//            emulate_via_full.js plus sS, aSH, bSH) and per-cell Shadow inputs for the Runner; sealed before any job.
// Usage: node emulate_fullsys.js adapter <data root with fixtures/ worlds/> <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const SP = require('../paths.js').DATA;
const K = path.join(__dirname, '..', 'wf20');
const SS = require(path.join(K, 'runner/sscore.js'));
const NF = require(path.join(K, 'runner/netfull.js'));
const SH = require(path.join(K, 'scripts/shcore.js'));
const FC = require(path.join(K, 'scripts/forecast.js'));
const LIB = require(path.join(K, 'scripts/shlib.js'));
const EMU = path.join(__dirname, '..', 'wf15emu');
const E15 = require(path.join(EMU, 'engine.js'));

const CELLS_DIR = path.join(SP, 'wf14/scenarios/weekdays-P512');
const PANELS = JSON.parse(fs.readFileSync(path.join(SP, 'wf17/r1-identity-fence-pad/work/panels.json'), 'utf8'));
const FROZEN = JSON.parse(fs.readFileSync(path.join(SP, 'wf15/frozen/shadow-params.json'), 'utf8'));
const INDEX = JSON.parse(fs.readFileSync(path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json'), 'utf8'));
const CONFIG = 'G2';
const ARMS = [
  { role: 'FS-G2-SHRC71-CL', key: 'SHRC71', fc: 'CL' },
  { role: 'FS-G2-SHHZ71-N', key: 'SHHZ71', fc: 'N' },
];
const sha = s => crypto.createHash('sha256').update(s).digest('hex');
const sigOf = res => sha(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.p0, r.A, r.revs])));   // shcore.js:211

function f14Cells() {
  const out = [];
  for (const day of [...PANELS.F14].sort()) for (const win of ['w08', 'w17']) for (const rs of [0, 1, 2]) out.push({ day, win, rs, cell: `${day}-${win}-r${rs}` });
  return out;
}

let NET = null, PRIOR = null;
function shared() {
  if (!NET) {
    NET = NF.build();
    PRIOR = LIB.loadPrior(path.join(LIB.WF, 'work/prior_prevhour.csv'), NET.zoneIds);
  }
  return { NET, PRIOR };
}

// shcore.runTask lines 116-191 for one arm, with the scenario P and the local->global map given by the caller.
function optionsFor(c, rc, P, globalOf, arm) {
  const { NET, PRIOR } = shared();
  const task = { cell: c.cell, prior: PRIOR.get(c.day + '|' + c.win), alphaS: FROZEN.alphaS, strideMs: FROZEN.strideMs };
  if (!task.prior) throw new Error('no prior for ' + c.cell);
  const cellCtx = SH.makeCellCtx(rc, NET, task, 'causal');
  if (globalOf) {
    if (globalOf.length !== cellCtx.globalOf.length || globalOf.some((g, i) => g !== cellCtx.globalOf[i])) throw new Error(c.cell + ': node map differs from the plain cell');
    cellCtx.globalOf = Int32Array.from(globalOf);
  }
  const scorer = SS.makeScorer(NET, cellCtx);
  const theta = FROZEN.theta[CONFIG][c.win];
  if (FROZEN.xPct[CONFIG] !== 71 || arm.key.slice(-2) !== '71') throw new Error('x* mismatch');
  const rec = new Map();
  const sscore = {
    score: ctx => {
      const res = scorer.score(ctx);
      const prev = rec.get(ctx.rq);
      if (prev) prev.dup = true; else rec.set(ctx.rq, { S: res.S });
      return res;
    },
    classOf: r => {
      const a = r.sS <= theta ? 0 : 1, e = rec.get(r.q.rq);
      if (!e || e.a !== undefined) throw new Error('classOf before score or twice');
      e.a = a; e.b = Math.round(a * r.slk);
      return a;
    },
  };
  const o = { ...JSON.parse(JSON.stringify(LIB.CONFIGS[CONFIG])), arm: ECL.parseArm(arm.key), wallBudgetMs: 900000, sscore };
  let prof = null;
  if (arm.fc === 'CL') {
    prof = FC.profile(c.day, c.win === 'w08' ? 32 : 68, 'prev4', INDEX, 15);
    if (!prof) throw new Error('no forecast sources for ' + c.day);
    o.forecast = { prof, clipPickup: true };
  }
  return { o, rec, theta, prof, cellCtx };
}

// ------------------------------------------------------------------------------------------------ adapter mode
const L15 = require(path.join(EMU, 'lib.js'));
const ID15 = require(path.join(EMU, 'identity.js'));
const NE15 = require(path.join(EMU, 'normalizer_emul.js'));
const ECL = require('../engines/engine_wf15_clint.js');   // emulator-wf15 engine; legF rounds each leg to integer ms (forecast only)
const PICK = 0, DROP = 1;

// verbatim copy of emulate_via_full.js prepareViaCell (sealed in VIA-FULL-PREDICTIONS.sha256; G3b 5,925/5,925 riders)
function prepareViaCell(cellName, root) {
  const base = L15.loadCell(cellName, path.join(root, 'fixtures'));
  const world = JSON.parse(fs.readFileSync(path.join(root, 'worlds', cellName + '.world.json'), 'utf8'));
  if (world.sigma !== 0 || world.shockRate !== 0) throw new Error('world must be noise-free');
  const values = world.base.values;
  if (!Array.isArray(values) || !values.length || values[0] !== 1) throw new Error('explicit base must start at 1');
  const H = values.length - 1;
  const prof = values.map(v => v / values[0]);
  const permille = base.norm.maximumRideTimePermille;
  const dwell = 0;
  const N = base.N;
  const snaps = prof.map(f => {
    const a = new Float64Array(N * N);
    for (let i = 0; i < N * N; i++) a[i] = ID15.snapshotMs(base.T0[i], f);
    return a;
  });
  const nodeIds = Array.from({ length: N }, (_, i) => ID15.nodeId(i));
  const reqs = base.requests.map(q => {
    if (NE15.maxRideFromPermille(q.direct, permille) !== q.maxRide) throw new Error('fixture maxRide does not follow the normalizer rule');
    return { ...q, maxRide: NE15.maxRideFromPermille(q.direct, permille) };
  });
  reqs.sort((a, b) => a.arrival - b.arrival || (a.rq < b.rq ? -1 : 1));
  reqs.forEach((q, i) => {
    q.stopP = { r: i, kind: PICK, node: q.o, code: 2 * i, sid: ID15.stopId(q.rq, 0), body: ID15.stopBody(nodeIds[q.o], 0, q.rq, dwell) };
    q.stopD = { r: i, kind: DROP, node: q.d, code: 2 * i + 1, sid: ID15.stopId(q.rq, 1), body: ID15.stopBody(nodeIds[q.d], 1, q.rq, dwell) };
  });
  const fleet = base.fleet.slice();
  const snapHashCache = new Map();
  return {
    id: cellName, family: 'd' + cellName.slice(1, 9), source: 'fixture', N, T0: base.T0, binMs: L15.BIN_MS,
    maxBins: H + 1,
    physFactor: bin => prof[Math.min(bin, H)],
    snapOf: bin => snaps[Math.min(bin, H)],
    snapHash: bin => {
      if (!snapHashCache.has(bin)) snapHashCache.set(bin, ID15.snapshotHash(N, base.T0, prof[Math.min(bin, H)], bin + 1, nodeIds));
      return snapHashCache.get(bin);
    },
    reqs, fleet, dwell,
    vehIdStr: fleet.map((f, vi) => ID15.vehicleId(vi)),
    drainTimerMs: 600000, clockGuardMs: 40 * 3600 * 1000,
    meta: { prof, startBin: Math.round(base.norm.sourceWindowStartSeconds / 900), permille, plusMs: 0, nVeh: fleet.length,
      scenarioId: base.scenarioId, norm: base.norm, nodeIdsFixture: base.nodeIds, timeWindow: base.timeWindow,
      sourceArtifactSha256: base.sourceArtifactSha256 },
  };
}

const vfName = c => `d${c.day.replace(/-/g, '')}-vf-r${c.rs}-${c.win}`;

function adapter(root, out) {
  if (fs.existsSync(out)) throw new Error('refused: ' + out + ' exists');
  const inputsDir = path.join(path.dirname(out), 'shadow-inputs');
  if (fs.existsSync(inputsDir)) throw new Error('refused: ' + inputsDir + ' exists');
  fs.mkdirSync(inputsDir);
  const lines = ['role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared\tsS\taSH\tbSH'];
  const counts = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tguaranteed\tclippedInitial\tclippedRevision\tms'];
  let checkedN = 0;
  const only = process.env.FS_ONLY ? new Set(process.env.FS_ONLY.split(',')) : null;   // smoke tests only; never for the seal
  for (const c of f14Cells().filter(x => !only || only.has(x.cell))) {
    const name = vfName(c);
    const rc = SH.readCell(path.join(CELLS_DIR, c.cell + '.json'));
    const P = prepareViaCell(name, root);
    if (P.meta.permille !== 2000 || P.fleet.length !== 62) throw new Error(name + ': permille/fleet');
    const globalOf = P.meta.nodeIdsFixture.map(id => { const m = /^node-(\d{6})$/.exec(id); if (!m) throw new Error('node id ' + id); return Number(m[1]); });
    let inputs = null;
    for (const arm of ARMS) {
      const t = Date.now();
      const { o, theta, prof, cellCtx } = optionsFor(c, rc, P, globalOf, arm);
      const res = ECL.simulate(P, o);
      const ms = Date.now() - t;
      if (arm.fc === 'N') {   // without a forecast the patched engine must equal the original one
        const again = E15.simulate(prepareViaCell(name, root), optionsFor(c, rc, null, globalOf, arm).o);
        if (sigOf(again) !== sigOf(res)) throw new Error(name + ': patched engine differs without forecast');
        checkedN++;
      }
      for (const r of res.riders) {
        lines.push([arm.role, name, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
          r.S, r.k, r.net, r.M, r.shared ? 1 : 0, r.sS, r.aSH, r.bSH].join('\t'));
      }
      const fc = res.fcCount || {};
      counts.push([arm.role, name, res.riders.length, res.riders.filter(r => r.served).length, res.rejected, (res.diag || {}).relocations,
        (res.diag || {}).genDecisions, res.riders.filter(r => r.aSH === 0).length, fc.clippedInitial, fc.clippedRevision, ms].join('\t'));
      console.log(`${arm.role} ${name}: riders ${res.riders.length}, served ${res.riders.filter(r => r.served).length}, ${ms} ms`);
      const record = { cell: name, sourceCell: c.cell, day: c.day, win: c.win, config: CONFIG, xPct: FROZEN.xPct[CONFIG],
        theta, alphaS: FROZEN.alphaS, strideMs: FROZEN.strideMs, omegaPms: cellCtx.omegaP, intervalMs: SS.IV_MS,
        kappa: cellCtx.kappa, factors: cellCtx.factors, globalOf, prior: shared().PRIOR.get(c.day + '|' + c.win),
        phi: rc.prov.sampling.sampled / rc.prov.sampling.eligible, sampled: rc.prov.sampling.sampled,
        zoneIds: Array.from(shared().NET.zoneIds) };
      inputs = inputs || record;
      if (prof) inputs.forecast = { profile: prof, binMs: 900000, clipPickup: true, rule: 'prev4', source: 'accuracy-index-2019.json (home A1)' };
    }
    fs.writeFileSync(path.join(inputsDir, name + '.json'), JSON.stringify(inputs), { flag: 'wx' });
  }
  fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
  fs.writeFileSync(out.replace(/\.tsv$/, '-counts.tsv'), counts.join('\n') + '\n', { flag: 'wx' });
  console.log(`wrote ${out}: ${lines.length - 1} rider rows; patched engine == original without forecast on ${checkedN} cell-arms`);
  return 0;
}

module.exports = { f14Cells, optionsFor, ARMS, CONFIG, sigOf, prepareViaCell };

if (require.main === module) {
  const [mode, ...rest] = process.argv.slice(2);
  if (mode === 'adapter') process.exit(adapter(rest[0], rest[1]));
  console.error('usage: node emulate_fullsys.js adapter <root> <out.tsv>');
  process.exit(2);
}
