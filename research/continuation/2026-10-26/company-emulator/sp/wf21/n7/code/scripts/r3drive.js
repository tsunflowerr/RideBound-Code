// wf17 R3 driver (EXPLORATORY, POST-AUDIT; RUNPLAN R3): generic worker pool modelled on wf16/a5-steering/scripts/robustness.js.
// One task = (panel, cell, config INS|G2, mode N|CL|CLF) x its arms; engine17.js (all knobs default except the forecast) and
// metrics_sh.metrics() unchanged. CL = closed-loop prev4 forecast (opts.forecast = { prof, clipPickup: true }, as wf14 part S);
// CLF = the same with flatRatio = r_w (patch P3; r_w per panel x window from work/r3/rw.json, outcome-free).
// Per-rider dumps (served riders: t0, p0, A, pLast) for N {U, EOHZ35, RCE35}, CL and CLF.
// Resumable on the full task key panel|cell|config|mode|arm|flatRatio.
// Usage: node r3drive.js --out <rows.ndjson> --dump <dump.ndjson> --rw <rw.json> --panels F14,F16 [--workers 6] [--cells a,b]
//        [--modes N,CL,CLF] [--flat-override 1] (gate G-R3d only: CLF with this flatRatio instead of r_w)
'use strict';
const { Worker, isMainThread, parentPort } = require('worker_threads');
const fs = require('fs'), path = require('path'), crypto = require('crypto');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const KEY = path.resolve(__dirname, '..', '..');
const PANEL_DIRS = { F14: path.join(SP, 'wf14/scenarios/weekdays-P512'), F16: path.join(KEY, 'cells/f16/weekdays-P512') };
const INDEX_FILE = path.join(SP, 'wf7/c-forecast/results/accuracy-index-2019.json');
const CONFIGS = { INS: {}, G2: { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' } };
const ARMS = {
  N: { INS: ['U', 'EO35', 'EOHZ35', 'RCE35'], G2: ['U', 'EO35', 'EOHZ35', 'RCE35', 'NCE35'] },
  CL: { INS: ['EOHZ35', 'RCE35'], G2: ['EOHZ35', 'RCE35'] },
  CLF: { INS: ['EOHZ35', 'RCE35'], G2: ['EOHZ35', 'RCE35'] },
};
const DUMP = { N: new Set(['U', 'EOHZ35', 'RCE35']), CL: null, CLF: null };   // null = every arm of the mode
const LABEL = 'wf17-exploratory-post-audit';
const keyOf = r => [r.panel, r.cell, r.config, r.mode, r.arm, r.flatRatio === null || r.flatRatio === undefined ? '' : r.flatRatio].join('|');
// rider fingerprint without the shown (clipped) pickup ETA revs[i][9]: the only rider field that clipPickup changes by design
const sigOf = res => crypto.createHash('sha256').update(JSON.stringify(res.riders.map(r => [r.rq, r.served, r.veh, r.t0, r.p0, r.pk0, r.A, r.pickup,
  r.revs.map(v => v.filter((x, i) => i !== 9))]))).digest('hex');

if (!isMainThread) {
  const SC = require('../runner/scenario.js');
  const E = require('../runner/engine17.js');
  const MET = require('./metrics_sh.js');
  const FC = require('./forecast.js');
  const INDEX = JSON.parse(fs.readFileSync(INDEX_FILE, 'utf8'));
  parentPort.on('message', task => {
    if (task === null) process.exit(0);
    const rows = [], dumps = [];
    try {
      const P = SC.loadScenarioFile(task.file, { maxRidePermille: 2000, dwellMs: 0, nVeh: 62 });
      let prof = null;
      if (task.mode !== 'N') {
        prof = FC.profile(task.day, task.win === 'w08' ? 32 : 68, 'prev4', INDEX, 15);
        if (!prof) throw new Error('no forecast sources for ' + task.day);
      }
      for (const key of task.arms) {
        const arm = E.parseArm(key);
        const o = { ...JSON.parse(JSON.stringify(CONFIGS[task.config])), arm, wallBudgetMs: 900000 };
        if (task.mode === 'CL') o.forecast = { prof, clipPickup: true };
        if (task.mode === 'CLF') o.forecast = { prof, clipPickup: true, flatRatio: task.flatRatio };
        const ts = Date.now();
        const res = E.simulate(P, o);
        const ms = Date.now() - ts;
        const m = MET.metrics(res, E.armOf(arm));
        rows.push({ label: LABEL, check: 'R3', panel: task.panel, cell: task.cell, day: task.day, win: task.win, rs: task.rs, config: task.config, mode: task.mode, arm: key,
          flatRatio: task.mode === 'CLF' ? task.flatRatio : null, ms, sig: sigOf(res), ...m });
        const want = DUMP[task.mode] === null || DUMP[task.mode].has(key);
        if (want) dumps.push({ label: LABEL, panel: task.panel, cell: task.cell, config: task.config, mode: task.mode, arm: key, flatRatio: task.mode === 'CLF' ? task.flatRatio : null,
          fields: ['rq8', 't0', 'p0', 'A', 'pLast'],
          riders: res.riders.filter(r => r.served).map(r => [r.rq.replace(/^rq-/, '').slice(0, 8), r.t0, r.p0, r.A, r.revs.length ? r.revs[r.revs.length - 1][7] : r.p0]) });
      }
      parentPort.postMessage({ task, rows, dumps });
    } catch (e) { parentPort.postMessage({ task, rows, dumps, error: String(e && e.stack || e) }); }
  });
} else if (require.main === module) {
  const arg = k => { const i = process.argv.indexOf('--' + k); return i > 0 ? process.argv[i + 1] : undefined; };
  const outFile = arg('out'), dumpFile = arg('dump');
  if (!outFile || !dumpFile) throw new Error('--out and --dump required');
  const RW = JSON.parse(fs.readFileSync(arg('rw'), 'utf8'));
  const flatOverride = arg('flat-override') !== undefined ? Number(arg('flat-override')) : null;
  const nW = Math.min(8, Number(arg('workers') || 6));
  const panels = (arg('panels') || 'F14,F16').split(',');
  const onlyCells = arg('cells') ? new Set(arg('cells').split(',')) : null;
  const modes = (arg('modes') || 'N,CL,CLF').split(',');
  const PAN = JSON.parse(fs.readFileSync(path.join(KEY, 'work/panels.json'), 'utf8'));
  const done = new Set();
  if (fs.existsSync(outFile)) for (const l of fs.readFileSync(outFile, 'utf8').split('\n')) if (l) { try { done.add(keyOf(JSON.parse(l))); } catch (e) { /* torn last line */ } }
  const tasks = [];
  for (const panel of panels) {
    const days = new Set(PAN[panel]);
    const files = fs.readdirSync(PANEL_DIRS[panel]).filter(f => f.endsWith('.json')).sort();
    for (const config of ['INS', 'G2']) for (const mode of modes) for (const f of files) {
      const cell = f.replace('.json', ''), m = /^(\d{4}-\d\d-\d\d)-(w08|w17)-(r\d)$/.exec(cell);
      if (!m || !days.has(m[1])) continue;
      if (onlyCells && !onlyCells.has(cell)) continue;
      const flatRatio = mode === 'CLF' ? (flatOverride !== null ? flatOverride : RW[panel][m[2]]) : null;
      const arms = ARMS[mode][config].filter(a => !done.has(keyOf({ panel, cell, config, mode, arm: a, flatRatio })));
      if (arms.length) tasks.push({ panel, file: path.join(PANEL_DIRS[panel], f), cell, day: m[1], win: m[2], rs: m[3], config, mode, arms, flatRatio });
    }
  }
  const code = Object.fromEntries(['runner/engine17.js', 'runner/scenario.js', 'scripts/metrics_sh.js', 'scripts/forecast.js', 'scripts/r3drive.js']
    .map(f => [f, crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname, '..', f))).digest('hex')]));
  fs.appendFileSync(outFile + '.runs.ndjson', JSON.stringify({ label: LABEL, argv: process.argv.slice(2), tasks: tasks.length, started: new Date().toISOString(), rw: RW, flatOverride, code }) + '\n');
  console.log(`panels ${panels}, modes ${modes}, tasks ${tasks.length}, workers ${nW}`);
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
      if (msg.error) { errors++; console.log('ERROR', msg.task.cell, msg.task.config, msg.task.mode, msg.error.slice(0, 300)); fs.appendFileSync(outFile + '.errors.ndjson', JSON.stringify({ task: msg.task, error: msg.error }) + '\n'); }
      // a failed task writes nothing (no partial rows); dumps are written before rows, so a row exists only when its dump exists
      if (!msg.error) {
        if (msg.dumps.length) fs.appendFileSync(dumpFile, msg.dumps.map(d => JSON.stringify(d)).join('\n') + '\n');
        if (msg.rows.length) fs.appendFileSync(outFile, msg.rows.map(r => JSON.stringify(r)).join('\n') + '\n');
      }
      if (nDone % 25 === 0) console.log(`${nDone}/${tasks.length} ${((Date.now() - t0) / 1000).toFixed(0)} s`);
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
module.exports = { keyOf, ARMS, sigOf };
