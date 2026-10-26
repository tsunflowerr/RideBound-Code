// wf21/n7 COPY of wf20/fullsys/code/scripts/run_sh.js (label wf21-n7-exploratory-post-audit). Added ONLY:
//   --fc N|CL|PAD|CLc (shcore.js), --flat <json> (PAD: flat ratio per "config|win|armBase", e.g. "G2|w08|SHRC"), --fc-index <json>
//   (CLc: causal per-target index), row fields flatRatio and fcIndex in the resume key, the flat file and index sha256 in the run
//   manifest, and the wf21 label.
// wf20/fullsys COPY of wf17/r1-identity-fence-pad/code/scripts/run_sh.js (label wf20-fullsys-exploratory-post-audit). Added ONLY:
//   --fc N|CL (forecast mode, see shcore.js; row field mode; part of the resume key), --panel <label> (row field; part of the resume
//   key), --prior <csv> (lambda prior file; default wf15/work/prior_prevhour.csv, needed for F16 days that wf15 never exported),
//   at most 4 workers (default 4), forecast.js in the manifest code hashes, and the wf20 label.
// wf17 COPY (r1-identity-fence-pad) of wf15/scripts/run_sh.js, extended only by --fence-tol <ms> (EO*/SHF*/DLS* arms; part of the
// resume key and the row), --audit (exceedance histogram of the strict-fence arms), --variant <label> (part of the resume key and the
// row), default engine engine17, default 6 workers, code hashes of this copy in the manifest, and a job footer (wall, CPU, peak RSS,
// largest heartbeat gap) appended to <out>.runs.ndjson.
// wf15 driver (SPEC 6.3; derived from wf14/scripts/confirm.js): worker pool over (cell, dispatcher config) tasks, each running
// its arms with the wf15 engine. Resumable: a run whose metric row is already in --out is skipped. The resume key is
// cell|config|scoreMode|arm|truncate|engine|repeat|theta|alphaS|strideMs|pi, so a run with another threshold or score
// parameter is never mistaken for a finished one. A --dump file belongs to one --out (owner file <dump>.owner.json); at
// start-up only orphan dump rows of THIS invocation's runs (no metric row: crash between the two writes) are removed, and
// rows of any other run make the driver refuse to start. Oracle and random score modes are discovery-only (SPEC 6.4).
//
// Usage: node run_sh.js --out <rows.ndjson> [options]
//   cells     --set discovery|confirm | --scen dir1,dir2   [--exclude d1,d2] [--days a..b] [--days-file f] [--win w08|w17]
//             [--first-days n] [--cells cellA,cellB]
//   configs   --configs INS,G2 (default both)
//   arms      --arms U,EOHZ35,SHHZ50 (both configs) | --arms-INS ... --arms-G2 ...
//             --x 0,10,...,100 | --x star   expands bare Shadow names (SHF, SHHZ, SHRC, SHNC) to SHHZ<x>; 'star' = frozen x*
//   scores    --score causal|oracle|random|off (default causal when any Shadow arm or --log is given, else off)
//             --log  score logging also for non-Shadow arms (U-log runs)
//   threshold --frozen frozen/shadow-params.json (x*, theta*, alphaS, strideMs, randomShare) | --ref <reference.json> (theta(x)
//             = type-1 quantile of the reference scores per config and window, SPEC 5.2)
//   params    --alphaS a (default: frozen, else results/alpha_calibration.json) --stride ms (default: frozen, else 0)
//             --pi INS=0.8,G2=0.8 (random mode; default frozen randomShare)
//   output    --dump <dump.ndjson> [--dump-arms U,SHHZ80] (default: every scored arm)  --part label  --workers 8 (max 8)
//   tests     --repeat n  --truncate ms  --cmp-ref  --hashes <file.ndjson>  --order reverse  --engine wf13
'use strict';
const fs = require('fs'), path = require('path');
const LIB = require('./shlib.js');
const POOL = require('./pool.js');

const SHADOW = ['SHF', 'SHHZ', 'SHRC', 'SHNC'];
const isShadowKey = k => /^(SHF|SHHZ|SHRC|SHNC)\d+$/.test(k);

function parseArgs(argv) {
  const a = {};
  for (let i = 0; i < argv.length; i++) {
    if (!argv[i].startsWith('--')) continue;
    const k = argv[i].slice(2);
    if (i + 1 < argv.length && !argv[i + 1].startsWith('--')) { a[k] = argv[i + 1]; i++; } else a[k] = true;
  }
  return a;
}
const list = s => (s === undefined || s === true ? null : String(s).split(',').map(x => x.trim()).filter(Boolean));
const abs = p => path.resolve(LIB.WF, p);

function loadFrozen(file) {
  const f = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (!f.xPct || !f.theta) throw new Error('frozen file without xPct/theta');
  return f;
}
function loadRef(file) {
  const r = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (r.kind !== 'wf15-score-reference') throw new Error('not a wf15 score reference: ' + file);
  return r;
}

// builds tasks; returns { tasks, meta }
function buildTasks(args) {
  const configs = list(args.configs) || ['INS', 'G2'];
  const frozen = args.frozen ? loadFrozen(abs(args.frozen)) : null;
  const ref = args.ref ? loadRef(abs(args.ref)) : null;
  const xs = args.x === 'star' ? 'star' : (list(args.x) || []).map(Number);
  const armsFor = cfg => {
    let arms = list(args['arms-' + cfg]) || list(args.arms) || [];
    const out = [];
    for (const k of arms) {
      if (SHADOW.includes(k)) {
        if (xs === 'star') { if (!frozen) throw new Error('--x star needs --frozen'); out.push(k + frozen.xPct[cfg]); }
        else { if (!xs.length) throw new Error(`bare Shadow arm ${k} needs --x`); for (const x of xs) out.push(k + x); }
      } else out.push(k);
    }
    return [...new Set(out)];
  };
  const anyShadow = configs.some(c => armsFor(c).some(isShadowKey));
  const scoreMode = args.score || (anyShadow || args.log ? 'causal' : 'off');
  if (anyShadow && scoreMode === 'off') throw new Error('Shadow arms need scores');
  let alphaS = null, strideMs = 0;
  if (scoreMode !== 'off') {
    alphaS = args.alphaS !== undefined ? Number(args.alphaS) : frozen ? frozen.alphaS : JSON.parse(fs.readFileSync(path.join(LIB.WF, 'results/alpha_calibration.json'), 'utf8')).aStar;
    strideMs = args.stride !== undefined ? Number(args.stride) : frozen ? frozen.strideMs : 0;
    if (frozen && (alphaS !== frozen.alphaS || strideMs !== frozen.strideMs)) throw new Error('alphaS/stride differ from the frozen values');
  }
  let pi = null;
  if (scoreMode === 'random') {
    pi = {};
    if (args.pi) for (const kv of list(args.pi)) { const [c, v] = kv.split('='); pi[c] = Number(v); }
    else if (frozen && frozen.randomShare) Object.assign(pi, frozen.randomShare);
    for (const c of configs) if (!(pi[c] >= 0 && pi[c] <= 1)) throw new Error('random mode needs --pi for ' + c);
  }
  if (ref && ref.scoreMode !== (scoreMode === 'oracle' ? 'oracle' : 'causal')) throw new Error(`reference scoreMode ${ref.scoreMode} does not match --score ${scoreMode}`);
  const thetaOf = (cfg, win, key) => {
    const pct = Number(/\d+$/.exec(key)[0]);
    if (frozen && scoreMode === 'causal' && pct === frozen.xPct[cfg]) return frozen.theta[cfg][win];   // oracle: own reference (SPEC 6.4)
    if (pct === 0) return LIB.thetaToJson(-Infinity);
    if (pct === 100) return LIB.thetaToJson(Infinity);
    if (!ref) throw new Error(`no theta source for ${key} (${cfg}): give --ref or --frozen`);
    const d = ref.configs[cfg] && ref.configs[cfg][win];
    if (!d) throw new Error(`reference has no ${cfg} ${win}`);
    return LIB.thetaToJson(LIB.thetaFromSorted(d.scores, pct));
  };
  let cells = LIB.listCells({ set: args.set, scen: list(args.scen), exclude: list(args.exclude), days: args.days, daysFile: args['days-file'] ? abs(args['days-file']) : null,
    win: args.win, firstDays: args['first-days'] ? Number(args['first-days']) : 0 });
  if (args.cells) { const keep = new Set(list(args.cells)); cells = cells.filter(c => keep.has(c.cell)); }
  if (args['max-cells']) cells = cells.slice(0, Number(args['max-cells']));
  if (scoreMode === 'oracle' || scoreMode === 'random') {          // SPEC 6.4: exploratory diagnostics never run on confirmatory cells
    const confirmDays = LIB.dayList('confirm');
    const bad = cells.filter(c => c.set === 'confirm' || confirmDays.has(c.day));
    if (bad.length) throw new Error(`--score ${scoreMode} is a discovery-only diagnostic (SPEC 6.4): ${bad.length} confirmatory cells selected, e.g. ${bad[0].cell}`);
  }
  const prior = scoreMode !== 'off' ? LIB.loadPrior(args.prior ? abs(args.prior) : path.join(LIB.WF, 'work/prior_prevhour.csv'), POOL.sharedNet().zoneIds) : null;
  const dumpArms = list(args['dump-arms']);
  const FLAT = args.flat && args.flat !== true ? JSON.parse(fs.readFileSync(path.resolve(String(args.flat)), 'utf8')).flat : null;   // wf21
  const tasks = [];
  for (const config of configs) for (const c of cells) {
    const arms = armsFor(config);
    const thetas = {};
    if (scoreMode !== 'random') for (const k of arms) if (isShadowKey(k)) thetas[k] = thetaOf(config, c.win, k);
    const pr = prior ? prior.get(c.day + '|' + c.win) : null;
    if (prior && !pr) throw new Error('no prior for ' + c.day + ' ' + c.win);
    const fcMode = args.fc && args.fc !== true ? String(args.fc) : 'N';                         // wf21
    let flat = null;
    if (fcMode === 'PAD') {
      if (!FLAT) throw new Error('--fc PAD needs --flat <json>');
      flat = {};
      for (const k of arms) { const v = FLAT[config + '|' + c.win + '|' + k.replace(/\d+$/, '')]; if (typeof v !== 'number') throw new Error('no flat ratio for ' + config + '|' + c.win + '|' + k); flat[k] = v; }
    }
    const fcIndex = fcMode === 'CLc' ? (args['fc-index'] && args['fc-index'] !== true ? path.resolve(String(args['fc-index'])) : null) : null;
    if (fcMode === 'CLc' && !fcIndex) throw new Error('--fc CLc needs --fc-index <json>');
    tasks.push({ ...c, fc: fcMode, flat, fcIndex, panel: args.panel && args.panel !== true ? String(args.panel) : null, part: args.part || null, config, arms, scoreMode, thetas, pi: pi ? pi[config] : null, alphaS, strideMs,
      prior: pr, dump: args.dump ? (dumpArms || true) : false, engine: args.engine || 'engine17',
      variant: args.variant && args.variant !== true ? String(args.variant) : null, fenceTolMs: args['fence-tol'] !== undefined ? Number(args['fence-tol']) : 0, audit: !!args.audit, hashes: !!args.hashes, cmpRef: !!args['cmp-ref'],
      truncateMs: args.truncate !== undefined ? Number(args.truncate) : null, repeat: args.repeat ? Number(args.repeat) : 1 });
  }
  return { tasks, meta: { configs, scoreMode, alphaS, strideMs, pi, frozen: args.frozen || null, ref: args.ref || null, cells: cells.length } };
}

// resume key: run identity INCLUDING the threshold and score parameters (row fields theta, alphaS, strideMs, pi; null for
// arms/modes without them). Dump rows carry the same fields, so rowKey(dump row) === rowKey(its metric row).
const kv = v => (v === null || v === undefined ? '' : String(v));
const rowKey = r => [r.cell, r.config, r.scoreMode, r.arm, r.truncateMs || '', r.engine || 'engine17', r.repeat || 0, kv(r.theta), kv(r.alphaS), kv(r.strideMs), kv(r.pi), kv(r.variant), kv(r.fenceTolMs), kv(r.mode), kv(r.panel), kv(r.flatRatio), kv(r.fcIndex)].join('|');   // wf21: + flatRatio, fcIndex
const isStrictKey = k => /^(EO|SHF|DLS)\d+$/.test(k);
// the row-parameter fields a task writes for one of its arms (mirrors shcore.runTask)
function rowParams(t, arm) {
  const SH = isShadowKey(arm), off = t.scoreMode === 'off';
  return { theta: SH && t.scoreMode !== 'random' ? t.thetas[arm] : null, pi: SH && t.scoreMode === 'random' ? t.pi : null,
    alphaS: off ? null : t.alphaS, strideMs: off ? null : (t.strideMs || 0), variant: t.variant || null, fenceTolMs: isStrictKey(arm) ? (t.fenceTolMs || 0) : null };
}
const taskKey = (t, arm, rep) => rowKey({ flatRatio: t.fc === 'PAD' ? t.flat[arm] : null, fcIndex: t.fc === 'CLc' ? path.basename(t.fcIndex) : null, mode: t.fc, panel: t.panel, cell: t.cell, config: t.config, scoreMode: t.scoreMode, arm, truncateMs: t.truncateMs, engine: t.engine, repeat: rep, ...rowParams(t, arm) });

async function main(argv) {
  const args = parseArgs(argv);
  if (!args.out) throw new Error('--out required');
  const outFile = abs(args.out), dumpFile = args.dump ? abs(args.dump) : null, hashFile = args.hashes && args.hashes !== true ? abs(args.hashes) : null;
  const { tasks: all, meta } = buildTasks(args);
  // resume
  const done = new Set(LIB.readNdjson(outFile).map(rowKey));
  const tasks = [], mine = new Set();
  for (const t of all) {
    const missing = t.arms.filter(arm => { let miss = false; for (let rep = 0; rep < t.repeat; rep++) { const k = taskKey(t, arm, rep); mine.add(k); if (!done.has(k)) miss = true; } return miss; });
    if (missing.length) tasks.push({ ...t, arms: missing });
  }
  if (dumpFile) {
    // a dump belongs to exactly one --out (owner file); rows of other invocations are never deleted
    const ownerFile = dumpFile + '.owner.json', outRel = path.relative(LIB.WF, outFile).split(path.sep).join('/');
    const existing = LIB.readNdjson(dumpFile);
    if (existing.length || fs.existsSync(ownerFile)) {
      const owner = fs.existsSync(ownerFile) ? JSON.parse(fs.readFileSync(ownerFile, 'utf8')).out : null;
      if (owner !== outRel) throw new Error(`dump ${args.dump} belongs to --out ${owner || '(unknown: no owner file)'}, not ${outRel}: use a fresh --dump path per --out`);
    }
    const foreign = existing.filter(d => { const k = rowKey(d); return !done.has(k) && !mine.has(k); });
    if (foreign.length) throw new Error(`dump ${args.dump} holds ${foreign.length} rows of runs that have no metric row in --out and are not part of this invocation (e.g. ${rowKey(foreign[0])}); refusing to delete them`);
    const keep = existing.filter(d => done.has(rowKey(d)));       // orphans of this invocation's runs (crash between the writes) are dropped and redone
    if (keep.length !== existing.length) fs.writeFileSync(dumpFile, keep.map(d => JSON.stringify(d)).join('\n') + (keep.length ? '\n' : ''));
    if (!fs.existsSync(ownerFile)) { fs.mkdirSync(path.dirname(ownerFile), { recursive: true }); fs.writeFileSync(ownerFile, JSON.stringify({ out: outRel, created: new Date().toISOString() }) + '\n'); }
  }
  if (args.order === 'reverse') tasks.reverse();
  const nW = Math.min(4, Number(args.workers || 4));   // wf20: at most 4 worker threads (another branch may be running)
  fs.mkdirSync(path.dirname(outFile), { recursive: true });
  const manifest = { argv, meta, tasks: tasks.length, started: new Date().toISOString(),
    code: Object.fromEntries(['runner/engine17.js', 'runner/scenario.js', 'runner/identity.js', 'runner/sscore.js', 'runner/lambda.js', 'runner/netfull.js', 'scripts/metrics_sh.js', 'scripts/shcore.js', 'scripts/run_sh.js', 'scripts/shlib.js', 'scripts/pool.js', 'scripts/forecast.js', 'scripts/shworker.js']
      .map(f => [f, LIB.sha256File(path.join(__dirname, '..', f))])), label: 'wf21-n7-exploratory-post-audit', prior: args.prior || null,
    flat: args.flat ? { file: args.flat, sha256: LIB.sha256File(path.resolve(String(args.flat))) } : null,
    fcIndex: args['fc-index'] ? { file: args['fc-index'], sha256: LIB.sha256File(path.resolve(String(args['fc-index']))) } : null };
  fs.appendFileSync(outFile + '.runs.ndjson', JSON.stringify(manifest) + '\n');
  console.log(`cells ${meta.cells}, tasks ${tasks.length} (of ${all.length}), scoreMode ${meta.scoreMode}, alphaS ${meta.alphaS}, stride ${meta.strideMs}, workers ${nW}`);
  const errors = [];
  const hb = { last: Date.now(), maxGapMs: 0 };
  const hbTimer = setInterval(() => { const n = Date.now(); hb.maxGapMs = Math.max(hb.maxGapMs, n - hb.last); if (n - hb.last > 120000) console.log('SUSPEND_GAP', n - hb.last); hb.last = n; }, 10000);
  const wall0 = Date.now();
  const res = await POOL.runPool(tasks, { workers: nW, onResult: msg => {
    if (msg.error) { errors.push({ cell: msg.task.cell, config: msg.task.config, error: msg.error }); return; }
    if (dumpFile && msg.dump.length) fs.appendFileSync(dumpFile, msg.dump.map(d => JSON.stringify(d)).join('\n') + '\n');
    if (hashFile && msg.hashes) fs.appendFileSync(hashFile, JSON.stringify({ cell: msg.task.cell, config: msg.task.config, scoreMode: msg.task.scoreMode, engine: msg.task.engine, truncateMs: msg.task.truncateMs, hashes: msg.hashes, cmp: msg.cmp }) + '\n');
    if (msg.rows.length) fs.appendFileSync(outFile, msg.rows.map(r => JSON.stringify(r)).join('\n') + '\n');
  } });
  clearInterval(hbTimer);
  { const ru = process.resourceUsage(); fs.appendFileSync(outFile + '.runs.ndjson', JSON.stringify({ finished: new Date().toISOString(), wallMs: Date.now() - wall0, cpuUserMs: Math.round(ru.userCPUTime / 1000), cpuSysMs: Math.round(ru.systemCPUTime / 1000), maxRssMB: Math.round(ru.maxRSS / 1024), maxHeartbeatGapMs: hb.maxGapMs, tasks: tasks.length, errors: errors.length, workers: nW }) + '\n'); }
  if (errors.length) fs.appendFileSync(outFile + '.errors.ndjson', errors.map(e => JSON.stringify(e)).join('\n') + '\n');
  console.log(`done ${res.done} tasks in ${((res.ms || 0) / 60000).toFixed(1)} min, errors ${errors.length}`);
  return { ...res, errors };
}

module.exports = { main, buildTasks, parseArgs, rowKey, rowParams, taskKey, isShadowKey };
if (require.main === module) main(process.argv.slice(2)).then(r => process.exit(r.errors.length ? 1 : 0), e => { console.error(e); process.exit(2); });
