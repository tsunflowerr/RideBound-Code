// wf17 r2-penalty-regime driver (EXPLORATORY, POST-AUDIT; RUNPLAN R4 and R5). Generic worker pool modelled on
// wf16/a5-steering/scripts/robustness.js. engine17.js with only the knobs named below changed; metrics_sh.metrics() unchanged.
//   R4  G2 friction: cells D60 x {w08, w17} x r0 (wf14 P512 cells), config G2 = { reopt: { g2: 99, g2Targets: T, g2PenaltyMs: pi },
//       lock: 'ratchet' } for T in {8, 16}, pi in {0, 60000, 300000}; arms U, EO35, EOHZ35, RCE35, NCE35.
//   R5  regime grid: cells D30 x {w08, w17} x r0; one factor at a time from B0 (P512, 62 veh, 2000 permille, dwell 0);
//       INS {} arms EOHZ35, RCE35; G2 { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' } arms U, EOHZ35, RCE35, NCE35.
// One task = (check, variant, cell, config) x its arms. Resumable on the full key check|variant|cell|config|arm.
// A failed task writes no row (logged to <out>.errors.ndjson). Job footer (wall, CPU, peak RSS, heartbeat gap) in <out>.runs.ndjson.
// Usage: node drive17.js --check R4|R5 --out <rows.ndjson> [--workers 6] [--variants a,b] [--cells c1,c2] [--configs INS,G2] [--arms A,B]
'use strict';
const { Worker, isMainThread, parentPort } = require('worker_threads');
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const KEY = path.resolve(__dirname, '..', '..');
const LABEL = 'wf17-exploratory-post-audit';
const SCEN = { 512: path.join(SP, 'wf14/scenarios/weekdays-P512'), 256: path.join(KEY, 'cells/d30/weekdays-P256') };
const BASE = { density: 512, nVeh: 62, maxRidePermille: 2000, dwellMs: 0 };
const CHECKS = {
  R4: {
    panel: 'D60',
    variants: {
      T8P0: { g2Targets: 8, g2PenaltyMs: 0 }, T8P60: { g2Targets: 8, g2PenaltyMs: 60000 }, T8P300: { g2Targets: 8, g2PenaltyMs: 300000 },
      T16P0: { g2Targets: 16, g2PenaltyMs: 0 }, T16P60: { g2Targets: 16, g2PenaltyMs: 60000 }, T16P300: { g2Targets: 16, g2PenaltyMs: 300000 },
    },
    arms: { G2: ['U', 'EO35', 'EOHZ35', 'RCE35', 'NCE35'] },
  },
  R5: {
    panel: 'D30',
    variants: {
      B0: {}, 'V-P256': { density: 256, nVeh: 42 }, 'V-F56': { nVeh: 56 }, 'V-F68': { nVeh: 68 },
      'V-R15': { maxRidePermille: 1500 }, 'V-R25': { maxRidePermille: 2500 }, 'V-D24': { dwellMs: 24000 },
    },
    arms: { G2: ['U', 'EOHZ35', 'RCE35', 'NCE35'], INS: ['EOHZ35', 'RCE35'] },
  },
};
const cfgOf = (check, config, V) => {
  if (config === 'INS') return {};
  if (check === 'R4') return { reopt: { g2: 99, g2Targets: V.g2Targets, g2PenaltyMs: V.g2PenaltyMs }, lock: 'ratchet' };
  return { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' };
};
const keyOf = r => [r.check, r.variant, r.cell, r.config, r.arm].join('|');
const sigOf = res => crypto.createHash('sha256').update(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.t0, r.p0, r.pk0, r.A, r.pickup, r.revs]))).digest('hex');

if (!isMainThread) {
  const SC = require('../runner/scenario.js');
  const E = require('../runner/engine17.js');
  const MET = require('./metrics_sh.js');
  parentPort.on('message', task => {
    if (task === null) process.exit(0);
    const rows = [];
    try {
      const W = { ...BASE, ...task.V };
      const P = SC.loadScenarioFile(task.file, { maxRidePermille: W.maxRidePermille, dwellMs: W.dwellMs, nVeh: W.nVeh });
      if (P.fleet.length !== W.nVeh) throw new Error(`fleet ${P.fleet.length} != nVeh ${W.nVeh}`);
      if (P.reqs.length !== W.density) throw new Error(`requests ${P.reqs.length} != density ${W.density}`);
      for (const key of task.arms) {
        const arm = E.parseArm(key);
        const cfg = cfgOf(task.check, task.config, task.V);
        const o = { ...JSON.parse(JSON.stringify(cfg)), arm, wallBudgetMs: 900000 };
        const ts = Date.now();
        const res = E.simulate(P, o);
        const ms = Date.now() - ts;
        const m = MET.metrics(res, E.armOf(arm));
        rows.push({ label: LABEL, check: task.check, variant: task.variant, cell: task.cell, day: task.day, win: task.win, rs: task.rs, config: task.config, arm: key,
          density: W.density, nVeh: W.nVeh, maxRidePermille: W.maxRidePermille, dwellMs: W.dwellMs,
          g2Targets: cfg.reopt ? cfg.reopt.g2Targets : null, g2PenaltyMs: cfg.reopt ? (cfg.reopt.g2PenaltyMs || 0) : null,
          ms, sig: sigOf(res), nReq: P.reqs.length, ...m });
      }
      parentPort.postMessage({ task, rows });
    } catch (e) { parentPort.postMessage({ task, rows, error: String(e && e.stack || e) }); }
  });
} else if (require.main === module) {
  const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
  const check = arg('check'), outFile = arg('out');
  if (!CHECKS[check] || !outFile) throw new Error('--check R4|R5 and --out required');
  const C = CHECKS[check];
  const nW = Math.min(8, Number(arg('workers') || 6));
  const variants = arg('variants') ? arg('variants').split(',') : Object.keys(C.variants);
  for (const v of variants) if (!C.variants[v]) throw new Error('unknown variant ' + v);
  const configs = arg('configs') ? arg('configs').split(',') : Object.keys(C.arms);
  const onlyCells = arg('cells') ? new Set(arg('cells').split(',')) : null;
  const onlyArms = arg('arms') ? new Set(arg('arms').split(',')) : null;
  const PAN = JSON.parse(fs.readFileSync(path.join(KEY, 'work/panels.json'), 'utf8'));
  const days = new Set(PAN[C.panel]);
  const done = new Set();
  if (fs.existsSync(outFile)) for (const l of fs.readFileSync(outFile, 'utf8').split('\n')) if (l) { try { done.add(keyOf(JSON.parse(l))); } catch (e) { /* torn last line */ } }
  const tasks = [];
  for (const variant of variants) {
    const V = C.variants[variant], dens = V.density || BASE.density;
    const dir = SCEN[dens];
    const files = fs.readdirSync(dir).filter(f => /-r0\.json$/.test(f)).sort();
    for (const config of configs) {
      if (!C.arms[config]) continue;
      for (const f of files) {
        const cell = f.replace('.json', ''), m = /^(\d{4}-\d\d-\d\d)-(w08|w17)-(r0)$/.exec(cell);
        if (!m || !days.has(m[1])) continue;
        if (onlyCells && !onlyCells.has(cell)) continue;
        const arms = C.arms[config].filter(a => (!onlyArms || onlyArms.has(a)) && !done.has(keyOf({ check, variant, cell, config, arm: a })));
        if (arms.length) tasks.push({ check, variant, V, file: path.join(dir, f), cell, day: m[1], win: m[2], rs: m[3], config, arms });
      }
    }
  }
  const nCells = new Set(tasks.map(t => t.variant + t.cell)).size;
  const code = Object.fromEntries(['runner/engine17.js', 'runner/scenario.js', 'scripts/metrics_sh.js', 'scripts/drive17.js']
    .map(f => [f, crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname, '..', f))).digest('hex')]));
  fs.appendFileSync(outFile + '.runs.ndjson', JSON.stringify({ label: LABEL, check, argv: process.argv.slice(2), tasks: tasks.length, variantCells: nCells, started: new Date().toISOString(), code }) + '\n');
  console.log(`check ${check}, variants ${variants}, configs ${configs}, tasks ${tasks.length}, arm-runs ${tasks.reduce((s, t) => s + t.arms.length, 0)}, workers ${nW}`);
  if (!tasks.length) process.exit(0);
  const t0 = Date.now(); let next = 0, nDone = 0, errors = 0; const workers = [];
  const hb = { last: Date.now(), maxGapMs: 0 };
  const hbTimer = setInterval(() => { const n = Date.now(); hb.maxGapMs = Math.max(hb.maxGapMs, n - hb.last); if (n - hb.last > 120000) console.log('SUSPEND_GAP', n - hb.last); hb.last = n; }, 10000);
  const feed = w => { if (next < tasks.length) w.postMessage(tasks[next++]); };
  for (let i = 0; i < Math.min(nW, tasks.length); i++) {
    const w = new Worker(__filename, { resourceLimits: { maxOldGenerationSizeMb: 600 } });
    workers.push(w);
    w.on('message', msg => {
      nDone++;
      if (msg.error) { errors++; console.log('ERROR', msg.task.variant, msg.task.cell, msg.task.config, msg.error.slice(0, 300)); fs.appendFileSync(outFile + '.errors.ndjson', JSON.stringify({ task: { ...msg.task, V: undefined }, error: msg.error }) + '\n'); }
      else if (msg.rows.length) fs.appendFileSync(outFile, msg.rows.map(r => JSON.stringify(r)).join('\n') + '\n');
      if (nDone % 50 === 0) console.log(`${nDone}/${tasks.length} ${((Date.now() - t0) / 1000).toFixed(0)} s`);
      if (nDone === tasks.length) {
        clearInterval(hbTimer);
        const ru = process.resourceUsage();
        fs.appendFileSync(outFile + '.runs.ndjson', JSON.stringify({ finished: new Date().toISOString(), wallMs: Date.now() - t0, cpuUserMs: Math.round(ru.userCPUTime / 1000), cpuSysMs: Math.round(ru.systemCPUTime / 1000), maxRssMB: Math.round(ru.maxRSS / 1024), maxHeartbeatGapMs: hb.maxGapMs, tasks: tasks.length, errors, workers: nW }) + '\n');
        for (const x of workers) x.postMessage(null);
        console.log(`done ${nDone} in ${((Date.now() - t0) / 60000).toFixed(1)} min, errors ${errors}, maxRSS ${Math.round(ru.maxRSS / 1024)} MB, maxGap ${hb.maxGapMs} ms`);
        setTimeout(() => process.exit(errors ? 1 : 0), 300);
      } else feed(w);
    });
    w.on('error', e => { console.error(e); process.exit(1); });
    feed(w);
  }
}
module.exports = { CHECKS, keyOf, cfgOf, BASE };
