// wf21/n7 COPY of wf20/fullsys/code/scripts/run_sh.js (label wf21-n7-exploratory-post-audit). Added ONLY:
//   --fc N|CL|PAD|CLc (shcore.js), --flat <json> (PAD: flat ratio per "config|win|armBase", e.g. "G2|w08|SHRC"), --fc-index <json>
//   (CLc: causal per-target index), row fields flatRatio and fcIndex in the resume key, the flat file and index sha256 in the run
//   manifest, and the wf21 label.
// wf20/fullsys COPY of wf17/r1-identity-fence-pad/code/scripts/run_sh.js (label wf20-fullsys-exploratory-post-audit). Added ONLY:
//   --fc N|CL (forecast mode, see shcore.js; row field mode; part of the resume key), --panel <label> (row field; part of the resume
//   key), --prior <csv> (lambda prior file; default wf15/work/prior_prevhour.csv, needed for F16 days that wf15 never exported),
//   at most 4 workers (default 4), forecast.js in the manifest code hashes, and the wf20 label.
// wf17 COPY (r1-identity-fence-pad) of wf15/scripts/run_sh.js, extended only by --fence-tol <ms> and --audit (both removed again,
// see REMOVED; the row field fenceTolMs stays in the resume key and the row), --variant <label> (part of the resume key and the
// row), default engine engine17, default 6 workers, code hashes of this copy in the manifest, and a job footer (wall, CPU, peak RSS,
// largest heartbeat gap) appended to <out>.runs.ndjson.
// wf15 driver (SPEC 6.3; derived from wf14/scripts/confirm.js): worker pool over (cell, dispatcher config) tasks, each running
// its arms with the wf15 engine. Resumable: a run whose metric row is already in --out is skipped. The resume key is
// cell|config|scoreMode|arm|truncate|engine|repeat|theta|alphaS|strideMs|pi|..., so a run with another threshold or score
// parameter is never mistaken for a finished one.
//
// Usage: node run.js --data <dir> --out <rows.ndjson> [options]
//   data      --data <dir>  data root (shlib.js): cells, network, prior, frozen parameters, forecast index. Relative paths of
//             --out, --frozen, --prior, --days-file resolve against <dir>/wf15; --flat, --fc-index, --scen against the current folder
//   cells     --set discovery|confirm | --scen dir1,dir2   [--exclude d1,d2] [--days a..b] [--days-file f] [--win w08|w17]
//             [--first-days n] [--cells cellA,cellB]
//   configs   --configs INS,G2 (default both)
//   arms      --arms U,EOHZ35,SHHZ50 (both configs) | --arms-INS ... --arms-G2 ...
//             --x 0,10,...,100 | --x star   expands bare Shadow names (SHF, SHHZ, SHRC) to SHHZ<x>; 'star' = frozen x*
//   scores    --score causal|off (default causal when any Shadow arm or --log is given, else off)
//             --log  score logging also for non-Shadow arms (U-log runs)
//   threshold --frozen frozen/shadow-params.json (x*, theta*, alphaS, strideMs); x = 0 and x = 100 need no threshold source
//   params    --alphaS a (default: frozen, else results/alpha_calibration.json) --stride ms (default: frozen, else 0)
//   output    --part label  --workers n (at most 4)
// The research options --ref, --cmp-ref, --hashes, --truncate, --repeat, --order, --engine, --dump, --dump-arms, --pi, --fence-tol,
// --audit and the oracle and random score modes are removed (see REMOVED below), and the run manifest no longer lists code hashes.
// The removed engine arms (NC OC RR FZ NCE OCE SHNC) fail in engine.parseArm with the same note.
'use strict';
const fs = require('fs'), path = require('path');
const LIB = require('./shlib.js');
const POOL = require('./pool.js');

const SHADOW = ['SHF', 'SHHZ', 'SHRC'];
const isShadowKey = k => /^(SHF|SHHZ|SHRC)\d+$/.test(k);

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

// options of the research driver that this copy no longer has: rejected, never silently ignored
const REMOVED = ['ref', 'cmp-ref', 'hashes', 'truncate', 'repeat', 'order', 'engine', 'dump', 'dump-arms', 'pi', 'fence-tol', 'audit'];
const REMOVED_NOTE = 'removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)';
function rejectRemoved(args) {
  for (const k of REMOVED) if (k in args) throw new Error(`--${k}: ${REMOVED_NOTE}`);
  if (args.score === 'oracle' || args.score === 'random') throw new Error(`--score ${args.score}: ${REMOVED_NOTE}`);
}

function loadFrozen(file) {
  const f = JSON.parse(fs.readFileSync(file, 'utf8'));
  if (!f.xPct || !f.theta) throw new Error('frozen file without xPct/theta');
  return f;
}

// builds tasks; returns { tasks, meta }
function buildTasks(args) {
  const configs = list(args.configs) || ['INS', 'G2'];
  const frozen = args.frozen ? loadFrozen(abs(args.frozen)) : null;
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
  const thetaOf = (cfg, win, key) => {
    const pct = Number(/\d+$/.exec(key)[0]);
    if (frozen && scoreMode === 'causal' && pct === frozen.xPct[cfg]) return frozen.theta[cfg][win];
    if (pct === 0) return LIB.thetaToJson(-Infinity);
    if (pct === 100) return LIB.thetaToJson(Infinity);
    throw new Error(`no theta source for ${key} (${cfg}): give --frozen (x*), or x = 0 or 100 (--ref ${REMOVED_NOTE})`);
  };
  let cells = LIB.listCells({ set: args.set, scen: list(args.scen), exclude: list(args.exclude), days: args.days, daysFile: args['days-file'] ? abs(args['days-file']) : null,
    win: args.win, firstDays: args['first-days'] ? Number(args['first-days']) : 0 });
  if (args.cells) { const keep = new Set(list(args.cells)); cells = cells.filter(c => keep.has(c.cell)); }
  if (args['max-cells']) cells = cells.slice(0, Number(args['max-cells']));
  const prior = scoreMode !== 'off' ? LIB.loadPrior(args.prior ? abs(args.prior) : path.join(LIB.WF, 'work/prior_prevhour.csv'), POOL.sharedNet().zoneIds) : null;
  const FLAT = args.flat && args.flat !== true ? JSON.parse(fs.readFileSync(path.resolve(String(args.flat)), 'utf8')).flat : null;   // wf21
  const tasks = [];
  for (const config of configs) for (const c of cells) {
    const arms = armsFor(config);
    const thetas = {};
    for (const k of arms) if (isShadowKey(k)) thetas[k] = thetaOf(config, c.win, k);
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
    tasks.push({ ...c, fc: fcMode, flat, fcIndex, panel: args.panel && args.panel !== true ? String(args.panel) : null, part: args.part || null, config, arms, scoreMode, thetas, alphaS, strideMs,
      prior: pr, variant: args.variant && args.variant !== true ? String(args.variant) : null });
  }
  return { tasks, meta: { configs, scoreMode, alphaS, strideMs, frozen: args.frozen || null, cells: cells.length } };
}

// resume key: run identity INCLUDING the threshold and score parameters (row fields theta, alphaS, strideMs; null for
// arms/modes without them). truncateMs, engine, repeat and pi belong to removed options; they stay in the key so that rows
// written by the old driver keep their identity (this driver's rows have them empty, 'engine17', 0 and null).
const kv = v => (v === null || v === undefined ? '' : String(v));
const rowKey = r => [r.cell, r.config, r.scoreMode, r.arm, r.truncateMs || '', r.engine || 'engine17', r.repeat || 0, kv(r.theta), kv(r.alphaS), kv(r.strideMs), kv(r.pi), kv(r.variant), kv(r.fenceTolMs), kv(r.mode), kv(r.panel), kv(r.flatRatio), kv(r.fcIndex)].join('|');   // wf21: + flatRatio, fcIndex
const isStrictKey = k => /^(EO|SHF|DLS)\d+$/.test(k);
// the row-parameter fields a task writes for one of its arms (mirrors shcore.runTask)
function rowParams(t, arm) {
  const SH = isShadowKey(arm), off = t.scoreMode === 'off';
  return { theta: SH ? t.thetas[arm] : null,
    alphaS: off ? null : t.alphaS, strideMs: off ? null : (t.strideMs || 0), variant: t.variant || null, fenceTolMs: isStrictKey(arm) ? 0 : null };
}
const taskKey = (t, arm) => rowKey({ flatRatio: t.fc === 'PAD' ? t.flat[arm] : null, fcIndex: t.fc === 'CLc' ? path.basename(t.fcIndex) : null, mode: t.fc, panel: t.panel, cell: t.cell, config: t.config, scoreMode: t.scoreMode, arm, ...rowParams(t, arm) });

async function main(argv) {
  const args = parseArgs(argv);
  rejectRemoved(args);
  LIB.setData(args.data);
  if (!args.out) throw new Error('--out required');
  const outFile = abs(args.out);
  const { tasks: all, meta } = buildTasks(args);
  // resume
  const done = new Set(LIB.readNdjson(outFile).map(rowKey));
  const tasks = [];
  for (const t of all) {
    const missing = t.arms.filter(arm => !done.has(taskKey(t, arm)));
    if (missing.length) tasks.push({ ...t, arms: missing });
  }
  const nW = Math.min(4, Number(args.workers || 4));   // wf20: at most 4 worker threads (another branch may be running)
  fs.mkdirSync(path.dirname(outFile), { recursive: true });
  const manifest = { argv, meta, tasks: tasks.length, started: new Date().toISOString(), label: 'wf21-n7-exploratory-post-audit', prior: args.prior || null,
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
