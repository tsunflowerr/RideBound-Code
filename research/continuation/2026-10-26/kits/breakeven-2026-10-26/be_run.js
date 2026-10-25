// Break-even re-run (BREAKEVEN-PLAN.md, sealed 2026-10-26 00:54): the company's wf15 engine, scoreMode 'off' path of
// shcore.runTask (shcore.js:99-120, 164-178), on the 60 P5 plain cells, arms EO/EOHZ/RCE x alpha {0,10,20,35,50,75,100}
// x configs INS/G2 (shlib.js:12-15, RUN_OVERRIDES shlib.js:16). Company files are only require()d, never written.
// Writes, per (cell, config, arm), the company metric row (metrics_sh.metrics) plus dispatch-only fields, and one line per
// promised rider. Refuses to overwrite any output.
// Usage: node be_run.js <outDir> [--workers 3]
'use strict';
const fs = require('fs'), path = require('path'), os = require('os'), crypto = require('crypto');
const { Worker, isMainThread, parentPort, workerData } = require('worker_threads');

const SPWF = 'E:/RideBoundData/research/p5-via-adapter-v1/sp/wf15';
const CELLS = 'E:/RideBoundData/research/p5-via-adapter-v1/cells/plain';
const FAMILIES = ['EO', 'EOHZ', 'RCE'];
const ALPHAS = [0, 10, 20, 35, 50, 75, 100];
const CONFIG_NAMES = ['INS', 'G2'];
const CODE_FILES = ['runner/engine.js', 'runner/scenario.js', 'scripts/metrics_sh.js', 'scripts/shcore.js', 'scripts/shlib.js'];
const EXPECTED = {
  'runner/engine.js': '908a29619f55a134f9ed5a0d35d4be1d3c502f07de78dc6fc704963690e7496f',
  'runner/scenario.js': 'd27d0bee61cb05aceacab98ec0e94630ab1746f2bf3a1fe34e3042d8bdeb2670',
  'scripts/metrics_sh.js': 'f1ee54cc4b9bdf9394b113b323a035115f948268994e280fb7ad0ece4bd294ed',
  'scripts/shcore.js': 'ba2b9c785d68f3cd7609f99fb7ed2e53ec3f6ebdbc609980527f8cff749fc08b',
  'scripts/shlib.js': 'fe27583126c1066b8519e5c2a90eb6817a1ae0c50b41bd52a3329c108ec27021',
};
const sha = f => crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');

function runTask(task) {
  const SC = require(path.join(SPWF, 'runner/scenario.js'));
  const E = require(path.join(SPWF, 'runner/engine.js'));
  const MET = require(path.join(SPWF, 'scripts/metrics_sh.js'));
  const LIB = require(path.join(SPWF, 'scripts/shlib.js'));
  const CORE = require(path.join(SPWF, 'scripts/shcore.js'));
  const rc = CORE.readCell(task.file, null);
  const P = SC.prepareScenario(rc.s, LIB.RUN_OVERRIDES, rc.dir);
  const rows = [], riders = [];
  for (const key of task.arms) {
    const arm = E.parseArm(key), A = E.armOf(arm);
    const o = { ...JSON.parse(JSON.stringify(LIB.CONFIGS[task.config])), arm, wallBudgetMs: 900000 };
    const ts = Date.now();
    const res = E.simulate(P, o);
    const ms = Date.now() - ts;
    const m = MET.metrics(res, A);
    // dispatch-only fields (plan section 3)
    let Ld60 = 0, Ld120 = 0, Lt60 = 0, La60 = 0, R60 = 0, sumNet = 0, sumNetPos = 0, sumDA = 0;
    let LdAndLa = 0, LdNotLa = 0, LaNotLd = 0, netMismatch = 0;
    for (const r of res.riders) {
      let s = 0;
      for (const v of r.revs) s += v[2];
      if (Math.abs(s - r.net) > 1e-6) netMismatch++;
      if (r.served) {
        const dA = r.A - r.p0, ld = r.net > 60000, la = dA > 60000;
        if (ld) Ld60++;
        if (r.net > 120000) Ld120++;
        if (dA - r.net > 60000) Lt60++;
        if (la) La60++;
        if (MET.hasReversal(r.zs, 60000)) R60++;
        if (ld && la) LdAndLa++; else if (ld) LdNotLa++; else if (la) LaNotLd++;
        sumNet += r.net; sumNetPos += Math.max(0, r.net); sumDA += dA;
      }
      riders.push({ cell: task.cell, config: task.config, arm: key, rq: r.rq, served: r.served, p0: r.p0, A: r.served ? r.A : null,
        net: r.net, rev60: MET.hasReversal(r.zs, 60000), nRevs: r.revs.length, Labs: r.Labs, slk: r.slk });
    }
    rows.push({ part: 'BE-plain', cell: task.cell, day: task.day, win: task.win, rs: task.rs, config: task.config, arm: key,
      scoreMode: 'off', engine: 'wf15', ms, ...m,
      be: { Ld60, Ld120, Lt60, La60, R60, sumNet, sumNetPos, sumDA, LdAndLa, LdNotLa, LaNotLd, netMismatch } });
  }
  return { rows, riders };
}

if (!isMainThread) {
  parentPort.on('message', task => {
    try { parentPort.postMessage({ task, ...runTask(task) }); }
    catch (e) { parentPort.postMessage({ task, rows: [], riders: [], error: String(e && e.stack || e) }); }
  });
} else {
  const outDir = process.argv[2];
  const wi = process.argv.indexOf('--workers');
  const nW = wi > 0 ? Number(process.argv[wi + 1]) : 3;
  if (!outDir) { console.error('usage: node be_run.js <outDir> [--workers 3]'); process.exit(2); }
  if (!(nW >= 1 && nW <= 4)) { console.error('workers must be 1..4'); process.exit(2); }
  const code = {};
  for (const f of CODE_FILES) { code[f] = sha(path.join(SPWF, f)); if (code[f] !== EXPECTED[f]) { console.error('hash mismatch ' + f); process.exit(3); } }
  const freeGB = os.freemem() / 2 ** 30;
  if (freeGB < 2) { console.error(`refused: free RAM ${freeGB.toFixed(2)} GB < 2 GB`); process.exit(4); }
  fs.mkdirSync(outDir, { recursive: true });
  const fRows = fs.openSync(path.join(outDir, 'rows-be.ndjson'), 'wx');        // refuses to overwrite
  const fRiders = fs.openSync(path.join(outDir, 'riders-be.ndjson'), 'wx');
  const arms = [];
  for (const fam of FAMILIES) for (const a of ALPHAS) arms.push(fam + a);
  const tasks = [];
  const files = fs.readdirSync(CELLS).filter(f => f.endsWith('.json')).sort();
  for (const config of CONFIG_NAMES) for (const f of files) {
    const cell = f.replace('.json', ''), m = /^(\d{4}-\d\d-\d\d)-(w08|w17)(?:-(r\d+))?$/.exec(cell);   // shlib.js:31
    if (!m) continue;
    tasks.push({ file: path.join(CELLS, f), cell, day: m[1], win: m[2], rs: m[3] || null, config, arms });
  }
  const manifest = { argv: process.argv.slice(2), started: new Date().toISOString(), node: process.version, workers: nW,
    freeRamGB: Number(freeGB.toFixed(2)), cells: files.length, tasks: tasks.length, arms, code,
    script: sha(__filename) };
  fs.writeFileSync(path.join(outDir, 'run-manifest.json'), JSON.stringify(manifest, null, 1) + '\n', { flag: 'wx' });
  console.log(`tasks ${tasks.length}, arms/task ${arms.length}, workers ${nW}, free RAM ${freeGB.toFixed(1)} GB`);
  const t0 = Date.now();
  let next = 0, done = 0, errors = 0;
  const ws = [];
  const finish = () => {
    for (const w of ws) w.terminate();
    fs.closeSync(fRows); fs.closeSync(fRiders);
    const end = { finished: new Date().toISOString(), minutes: Number(((Date.now() - t0) / 60000).toFixed(2)), done, errors };
    fs.writeFileSync(path.join(outDir, 'run-end.json'), JSON.stringify(end) + '\n', { flag: 'wx' });
    console.log(JSON.stringify(end));
    process.exit(errors ? 1 : 0);
  };
  const feed = w => { if (next < tasks.length) w.postMessage(tasks[next++]); };
  for (let i = 0; i < Math.min(nW, tasks.length); i++) {
    const w = new Worker(__filename, { resourceLimits: { maxOldGenerationSizeMb: 1200 } });
    ws.push(w);
    w.on('message', msg => {
      done++;
      if (msg.error) { errors++; console.log('ERROR', msg.task.cell, msg.task.config, msg.error.slice(0, 800)); }
      if (msg.rows.length) fs.writeSync(fRows, msg.rows.map(r => JSON.stringify(r)).join('\n') + '\n');
      if (msg.riders.length) fs.writeSync(fRiders, msg.riders.map(r => JSON.stringify(r)).join('\n') + '\n');
      if (done % 10 === 0 || done === tasks.length) console.log(`${done}/${tasks.length} ${((Date.now() - t0) / 1000).toFixed(0)} s`);
      if (done === tasks.length) finish(); else feed(w);
    });
    w.on('error', e => { console.error('worker error', e); errors++; done++; if (done === tasks.length) finish(); });
    feed(w);
  }
}
