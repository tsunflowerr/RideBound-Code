// emulate_adapter_pp.js (2026-11-08, A5: copy of emulate_adapter.js for PAD and the G2 penalty only) — Chương 5, phương án A đã được chủ đề tài duyệt: thay mọi kết quả "mô phỏng nhanh,
// cài đặt mặc định" bằng mô phỏng nhanh khớp FleetPy. Không sửa engine, quy tắc hay tham số nào:
//   * prepareViaCell: bản sao nguyên văn của emulate_fullsys.js (= emulate_via_full.js, khóa trong VIA-FULL-PREDICTIONS.sha256);
//   * Shadow + dự báo: optionsFor của emulate_fullsys.js; chỉ thêm tham số cấu hình (INS: x* 72, G2: x* 71) và lấy theta,
//     các tùy chọn cấu hình, wallBudgetMs, phép tách arm/A đúng như shcore.runTask;
//   * engine: engine_wf15_clint.js (engine wf15; không dự báo thì trùng engine gốc, đã kiểm ở emulate_fullsys.js);
//   * chỉ số: metrics_sh.metrics(res, A) của wf20/fullsys, A = armOf(parseArm(key)), như các dòng 60 ngày cũ.
// Thêm (không thay chỉ số cũ): early60First (khách hoàn tất đến sớm hơn giờ báo đầu quá 60 s), sumLead/nLead (tổng và số
// khách của p0 - giờ đặt, trên khách được hứa) để tính thời lượng báo đầu.
//
// Usage:
//   node emulate_adapter.js --cells <file> --arms G2:SHHZ71:N,G2:SHRC71:CL,INS:U:N,... --roots <root1>;<root2>
//                           --out <dir> [--shard i/n] [--riders]
// cells: mỗi dòng một ô dạng 2019-02-05-w08-r0. Mỗi ô được tìm trong các root theo thứ tự (fixtures/ và worlds/).
// Ghi <out>/rows-<i>.ndjson (một dòng mỗi ô-nhánh) và, nếu --riders, <out>/riders-<i>.tsv. Không ghi đè.
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const SP = require('../paths.js').DATA;
const K = path.join(__dirname, '..', 'wf20');
const E17 = require(path.join(K, 'runner/engine17.js'));
const SS = require(path.join(K, 'runner/sscore.js'));
const NF = require(path.join(K, 'runner/netfull.js'));
const SH = require(path.join(K, 'scripts/shcore.js'));
const FC = require(path.join(K, 'scripts/forecast.js'));
const LIB = require(path.join(K, 'scripts/shlib.js'));
const MET = require(path.join(K, 'scripts/metrics_sh.js'));
const EMU = path.join(__dirname, '..', 'wf15emu');
const L15 = require(path.join(EMU, 'lib.js'));
const ID15 = require(path.join(EMU, 'identity.js'));
const NE15 = require(path.join(EMU, 'normalizer_emul.js'));
const ECL = require('../engines/engine_wf15_clint_pp.js');   // A5: + PAD flatRatio, + G2 penalty

const CELL_DIRS = [path.join(SP, 'wf14/scenarios/weekdays-P512'), path.join(SP, 'wf17/r1-identity-fence-pad/cells/f16/weekdays-P512')];
const PRIOR_FILES = [path.join(LIB.WF, 'work/prior_prevhour.csv'), path.join(SP, 'wf20/fullsys/work/prior_F16/prior_prevhour.csv')];
const FROZEN = JSON.parse(fs.readFileSync(path.join(SP, 'wf15/frozen/shadow-params.json'), 'utf8'));
const INDEX = JSON.parse(fs.readFileSync(path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json'), 'utf8'));
const CIDX_FILE = path.join(SP, 'wf21/n7/cidx/results/index-causal-pertarget.json');   // A2: index of mode CLc (n7 RUNBOOK.sh:15)
let CIDX_CACHE = null;
const CIDX = () => (CIDX_CACHE = CIDX_CACHE || JSON.parse(fs.readFileSync(CIDX_FILE, 'utf8')));
const PICK = 0, DROP = 1;
const sha = s => crypto.createHash('sha256').update(s).digest('hex');
const armOf = ECL.armOf || E17.armOf;

let NET = null, PRIOR = null;
function shared() {
  if (!NET) {
    NET = NF.build();
    PRIOR = new Map();
    for (const f of PRIOR_FILES) for (const [k, v] of LIB.loadPrior(f, NET.zoneIds)) {
      if (PRIOR.has(k)) throw new Error('prior key twice ' + k);
      PRIOR.set(k, v);
    }
  }
  return { NET, PRIOR };
}

// verbatim copy of emulate_fullsys.js prepareViaCell
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

// emulate_fullsys.js optionsFor, with the configuration as a parameter (shcore.runTask: o = CONFIGS[config] + arm + wallBudgetMs)
function optionsFor(c, rc, globalOf, spec) {
  const arm = E17.parseArm(spec.key);
  const A = armOf(arm);
  const o = { ...JSON.parse(JSON.stringify(LIB.CONFIGS[spec.config])), arm, wallBudgetMs: 900000 };
  let rec = null, theta = null, prof = null;
  if (MET.isShadow(A)) {
    const { NET, PRIOR } = shared();
    const task = { cell: c.cell, prior: PRIOR.get(c.day + '|' + c.win), alphaS: FROZEN.alphaS, strideMs: FROZEN.strideMs };
    if (!task.prior) throw new Error('no prior for ' + c.cell);
    const cellCtx = SH.makeCellCtx(rc, NET, task, 'causal');
    if (globalOf.length !== cellCtx.globalOf.length || globalOf.some((g, i) => g !== cellCtx.globalOf[i])) throw new Error(c.cell + ': node map differs from the plain cell');
    cellCtx.globalOf = Int32Array.from(globalOf);
    const scorer = SS.makeScorer(NET, cellCtx);
    theta = FROZEN.theta[spec.config][c.win];
    if (FROZEN.xPct[spec.config] !== Math.round(A.x * 100) || Number(spec.key.slice(-2)) !== FROZEN.xPct[spec.config]) throw new Error('x* mismatch ' + spec.config + ' ' + spec.key);
    rec = new Map();
    o.sscore = {
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
  }
  if (spec.fc === 'CL') {
    prof = FC.profile(c.day, c.win === 'w08' ? 32 : 68, 'prev4', INDEX, 15);
    if (!prof) throw new Error('no forecast sources for ' + c.day);
    o.forecast = { prof, clipPickup: true };
  } else if (spec.fc === 'CLc') {
    // A2: wf21/n7 shcore.js lines 148-155 and 166 verbatim in meaning: causal per-target index, otherwise identical to CL
    const T = CIDX().targets[c.day];
    if (!T) throw new Error('causal index has no target ' + c.day);
    for (const sd of Object.keys(T.F)) if (!(sd < c.day)) throw new Error('causal index for ' + c.day + ' holds a day >= target: ' + sd);
    prof = FC.profile(c.day, c.win === 'w08' ? 32 : 68, 'prev4', T.F, 15);
    if (!prof) throw new Error('no causal forecast sources for ' + c.day);
    o.forecast = { prof, clipPickup: true };
  } else if (spec.fc === 'PAD') {
    // A5: wf21/n7 shcore.js 143-147 and 168-171: PAD carries the CL profile (anchor only) and the flat ratio of config|win|armBase
    prof = FC.profile(c.day, c.win === 'w08' ? 32 : 68, 'prev4', INDEX, 15);
    if (!prof) throw new Error('no forecast sources for ' + c.day);
    const flatRatio = FLAT[spec.config + '|' + c.win + '|' + spec.key.replace(/\d+$/, '')];
    if (!(typeof flatRatio === 'number' && flatRatio > 0.5 && flatRatio < 2)) throw new Error('PAD needs a flat ratio for ' + spec.key);
    o.forecast = { prof, clipPickup: true, flatRatio };
  } else if (spec.fc !== 'N') throw new Error('forecast mode ' + spec.fc);
  if (G2PEN !== null) {
    // A5: wf17/r2 drive17.js:38: config G2 = { reopt: { g2: 99, g2Targets: 8, g2PenaltyMs }, lock: 'ratchet' }
    if (spec.config !== 'G2') throw new Error('--g2pen needs config G2');
    o.reopt = { ...o.reopt, g2PenaltyMs: G2PEN };
  }
  return { o, A, rec, theta };
}

let FLAT = {}, G2PEN = null;
function parseArgs(argv) {
  const a = {};
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i];
    if (!k.startsWith('--')) throw new Error('arg ' + k);
    if (k === '--riders') a.riders = true; else a[k.slice(2)] = argv[++i];
  }
  return a;
}

function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.flat) FLAT = JSON.parse(fs.readFileSync(args.flat, 'utf8')).flat;
  if (args.g2pen !== undefined) G2PEN = Number(args.g2pen);
  const cells = fs.readFileSync(args.cells, 'utf8').split(/\r?\n/).filter(Boolean).map(n => {
    const m = /^(\d{4}-\d{2}-\d{2})-(w08|w17)-r([0-2])$/.exec(n);
    if (!m) throw new Error('cell ' + n);
    return { cell: n, day: m[1], win: m[2], rs: 'r' + m[3], vf: `d${m[1].replace(/-/g, '')}-vf-r${m[3]}-${m[2]}` };
  });
  const specs = args.arms.split(',').map(s => {
    const [config, key, fc] = s.split(':');
    if (!LIB.CONFIGS[config]) throw new Error('config ' + config);
    return { config, key, fc: fc || 'N', role: `FS-${config}-${key}-${fc || 'N'}` };
  });
  const roots = args.roots.split(';');
  const [si, sn] = (args.shard || '0/1').split('/').map(Number);
  fs.mkdirSync(args.out, { recursive: true });
  const rowsFile = path.join(args.out, `rows-${si}.ndjson`);
  const ridersFile = path.join(args.out, `riders-${si}.tsv`);
  if (fs.existsSync(rowsFile) || (args.riders && fs.existsSync(ridersFile))) throw new Error('refused: output exists in ' + args.out);
  const rowsFd = fs.openSync(rowsFile, 'wx');
  const ridersFd = args.riders ? fs.openSync(ridersFile, 'wx') : null;
  if (ridersFd !== null) fs.writeSync(ridersFd, 'role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared\tsS\taSH\tbSH\n');
  let done = 0;
  const mine = cells.filter((_, i) => i % sn === si);
  for (const c of mine) {
    const root = roots.find(r => fs.existsSync(path.join(r, 'fixtures', c.vf)));
    if (!root) throw new Error('no fixture for ' + c.vf);
    const needShadow = specs.some(s => MET.isShadow(armOf(E17.parseArm(s.key))));
    let rc = null, globalOf = null;
    const P0 = prepareViaCell(c.vf, root);
    if (P0.meta.permille !== 2000) throw new Error(c.vf + ': permille');
    if (!args.nveh && P0.fleet.length !== 62) throw new Error(c.vf + ': fleet ' + P0.fleet.length);
    if (args.nveh) {
      // A3: fleet-size survey. As scenario.js (fleet.slice(0, nVeh)), keep the first n vehicles of the fixture fleet, which the
      // converter takes in source order (src.fleet[:NVEH]); fixtures with 81 vehicles come from the same converter with NVEH 81.
      const n = Number(args.nveh);
      if (!(Number.isInteger(n) && n >= 1 && n <= P0.fleet.length)) throw new Error('--nveh ' + n + ' > fleet ' + P0.fleet.length);
      P0.fleet = P0.fleet.slice(0, n);
      P0.vehIdStr = P0.vehIdStr.slice(0, n);
      P0.meta.nVeh = n;
    }
    if (needShadow) {
      const dir = CELL_DIRS.find(d => fs.existsSync(path.join(d, c.cell + '.json')));
      if (!dir) throw new Error('no plain cell for ' + c.cell);
      rc = SH.readCell(path.join(dir, c.cell + '.json'));
      globalOf = P0.meta.nodeIdsFixture.map(id => { const m = /^node-(\d{6})$/.exec(id); if (!m) throw new Error('node id ' + id); return Number(m[1]); });
    }
    for (const spec of specs) {
      // one prepared cell for every arm, as emulate_via_full.js and emulate_fullsys.js did for the sealed predictions
      const P = P0;
      const { o, A, rec, theta } = optionsFor(c, rc, globalOf, spec);
      const t = Date.now();
      const res = ECL.simulate(P, o);
      const ms = Date.now() - t;
      if (rec) for (const [rq, e] of rec) if (e.dup) throw new Error('scored twice ' + rq + ' ' + c.cell);
      let early60First = 0, sumLead = 0, nLead = 0;
      for (const r of res.riders) {
        sumLead += r.p0 - r.arrival; nLead++;
        if (r.served && r.p0 - r.A > 60000) early60First++;
      }
      const row = { part: 'ADAPTER', nVeh: P.fleet.length, cell: c.cell, fixture: c.vf, day: c.day, win: c.win, rs: c.rs, config: spec.config, mode: spec.fc,
        arm: spec.key, engine: 'wf15-clint-pp', theta: theta === null ? null : theta,
        flatRatio: o.forecast && o.forecast.flatRatio !== undefined ? o.forecast.flatRatio : null, g2PenaltyMs: G2PEN, ms, ...MET.metrics(res, A),
        early60First, sumLead, nLead,
        sig: sha(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.p0, r.A, r.revs]))) };
      fs.writeSync(rowsFd, JSON.stringify(row) + '\n');
      if (ridersFd !== null) {
        const out = [];
        for (const r of res.riders) {
          out.push([spec.role, c.vf, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
            r.S, r.k, r.net, r.M, r.shared ? 1 : 0, r.sS === undefined ? '' : r.sS, r.aSH === undefined ? '' : r.aSH, r.bSH === undefined ? '' : r.bSH].join('\t'));
        }
        fs.writeSync(ridersFd, out.join('\n') + '\n');
      }
      done++;
      console.log(`[${si}/${sn}] ${spec.role} ${c.vf}: served ${row.served}, rev ${row.visRev60}, ${ms} ms (${done}/${mine.length * specs.length})`);
    }
  }
  fs.closeSync(rowsFd);
  if (ridersFd !== null) fs.closeSync(ridersFd);
  console.log(`[${si}/${sn}] done ${done}`);
}

main();
