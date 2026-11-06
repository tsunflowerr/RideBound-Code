// ADR-089 GĐ4/GĐ6, added 2026-11-04 (owner's question on a time-weighted ETA error, and the rule-C attribution point).
// Per rider, the published drop-ETA sequence WITH publication times, plus the actual drop time A, so the time-weighted
// ETA error E_i = (1/(A - t0)) * integral_{t0}^{A} |p(t) - A| dt can be computed (eta_metrics.py). Publication time of a
// revision = tEval of the decision that published it (engine_wf15_clint.js:724, engine17 the same), of the initial
// promise = r.t0 (engine_wf15_clint.js:733). Consecutive revisions with the same p are dropped (the step function is
// unchanged by them).
//
// Arms: the two sealed arms of the final comparison plus the two attribution arms that separate rule C from the forecast:
//   FS-G2-SHRC71-CL (ours) | FS-G2-SHHZ71-N (Elmi) | FS-G2-SHHZ71-CL (fence + forecast) | FS-G2-SHRC71-N (rule C, no forecast)
// Nothing of the sealed emulator is changed: emulate_fullsys.js is only required (optionsFor, prepareViaCell, f14Cells).
//
// Modes
//   adapter <data root> <out dir>   full-node fixtures, engine_wf15_clint.js. For the two sealed arms every rider row must
//            equal the sealed predictions row and the p-sequence must equal the sealed drop sequence; for the two new
//            arms the rider rows are written to attr-predictions.tsv (same columns; sealed before any job of those arms),
//            and the N arm must equal the original wf15 engine (as the sealed run checked for its N arm).
//   plain <out dir>                 the report's own setting (wf14 plain cells, engine17, LIB.RUN_OVERRIDES): every
//            cell-arm must reproduce the company row's rider fingerprint (sig, shcore.js:211) for its config/mode/arm.
// FS_CELLS=<comma-separated vf names or plain cells> limits the cells (gate subsets; the subset is sealed on its own).
// Usage: node emulate_timed.js adapter <data root> <out dir> | node emulate_timed.js plain <out dir>
'use strict';
const fs = require('fs');
const path = require('path');
const FS = require('./emulate_fullsys.js');
const ECL = require('./engine_wf15_clint.js');
const SP = 'E:/RideBoundData/company-intake/2026-10-23/sp';
const K = path.join(SP, 'wf20/fullsys/code');
const SC = require(path.join(K, 'runner/scenario.js'));
const E17 = require(path.join(K, 'runner/engine17.js'));
const SH = require(path.join(K, 'scripts/shcore.js'));
const LIB = require(path.join(K, 'scripts/shlib.js'));
const E15 = require(path.join('C:/RideBoundContinuation/2026-10-23/emulator-wf15', 'engine.js'));
const CELLS_DIR = path.join(SP, 'wf14/scenarios/weekdays-P512');
const EXPECTED = 'E:/Code/RideBound-fastrun-run/research/continuation/2026-10-26/company-emulator/expected/wf20__fullsys__results__rows.tsv';
const SEALED_ROWS = 'E:/RideBoundData/research/fullsys-f14-v2/predictions/fullsys-f14-predictions.tsv';
const SEALED_SEQ = 'E:/RideBoundData/research/fullsys-f14-v2/predictions/fullsys-f14-drop-sequences.tsv';

const ARMS = [
  ...FS.ARMS,
  { role: 'FS-G2-SHHZ71-CL', key: 'SHHZ71', fc: 'CL', added: true },
  { role: 'FS-G2-SHRC71-N', key: 'SHRC71', fc: 'N', added: true },
];
if (FS.ARMS.length !== 2 || FS.ARMS[0].role !== 'FS-G2-SHRC71-CL' || FS.ARMS[1].role !== 'FS-G2-SHHZ71-N') throw new Error('sealed arms changed');
const vfName = c => `d${c.day.replace(/-/g, '')}-vf-r${c.rs}-${c.win}`;
const only = process.env.FS_CELLS ? new Set(process.env.FS_CELLS.split(',')) : null;

function readTsv(file) {
  const lines = fs.readFileSync(file, 'utf8').trim().split(/\r?\n/), head = lines[0].split('\t');
  return { head, rows: lines.slice(1).map(l => l.split('\t')) };
}

function timed(r) {
  const seq = [[r.t0, r.p0]];
  for (const v of r.revs) if (v[7] !== seq[seq.length - 1][1]) seq.push([v[0], v[7]]);
  return seq;
}

function timedLine(role, cell, r) {
  return [role, cell, r.rq, r.served ? 1 : 0, r.t0, Number.isFinite(r.A) ? r.A : '', JSON.stringify(timed(r))].join('\t');
}

const TIMED_HEAD = 'role\tcell\trequestId\tserved\tt0\tA\ttimedDropSequence';
const ROW_HEAD = 'role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared\tsS\taSH\tbSH';
const rowLine = (role, name, r) => [role, name, r.rq, '', r.t0, r.p0, '', Number.isFinite(r.A) ? r.A : '', r.served ? 1 : 0,
  r.S, r.k, r.net, r.M, r.shared ? 1 : 0, r.sS, r.aSH, r.bSH].join('\t');

function adapter(root, outDir) {
  const timedOut = path.join(outDir, 'timed-adapter.tsv'), rowsOut = path.join(outDir, 'attr-predictions.tsv');
  const countsOut = path.join(outDir, 'attr-predictions-counts.tsv');   // same columns as the sealed counts file
  for (const f of [timedOut, rowsOut, countsOut]) if (fs.existsSync(f)) throw new Error('refused: ' + f + ' exists');
  const countLines = ['role\tcell\triders\tserved\trejected\trelocations\tdecisions\tguaranteed\tclippedInitial\tclippedRevision\tms'];
  const sealedRows = new Map(), sealedSeq = new Map();
  { const t = readTsv(SEALED_ROWS); if (t.head.join('\t') !== ROW_HEAD) throw new Error('sealed row header');
    for (const v of t.rows) sealedRows.set([v[0], v[1], v[2]].join('|'), v.join('\t')); }
  { const t = readTsv(SEALED_SEQ), ix = k => t.head.indexOf(k);
    for (const v of t.rows) sealedSeq.set([v[ix('role')], v[ix('cell')], v[ix('requestId')]].join('|'), v[ix('dropSequence')]); }
  const timedLines = [TIMED_HEAD], rowLines = [ROW_HEAD];
  let checkedRows = 0, checkedSeq = 0, checkedN = 0;
  for (const c of FS.f14Cells().filter(x => !only || only.has(vfName(x)))) {
    const name = vfName(c);
    const rc = SH.readCell(path.join(CELLS_DIR, c.cell + '.json'));
    const P = FS.prepareViaCell(name, root);
    const globalOf = P.meta.nodeIdsFixture.map(id => Number(/^node-(\d{6})$/.exec(id)[1]));
    for (const arm of ARMS) {
      const t = Date.now();
      // sealed arms: the same P for both, exactly as the sealed run; added arms: a fresh P each
      const Pa = arm.added ? FS.prepareViaCell(name, root) : P;
      const res = ECL.simulate(Pa, FS.optionsFor(c, rc, Pa, globalOf, arm).o);
      if (arm.added && process.env.CHECK_SHARED === '1') {   // does a used P change the result? (gate subset only)
        const shared = ECL.simulate(P, FS.optionsFor(c, rc, P, globalOf, arm).o);
        if (FS.sigOf(shared) !== FS.sigOf(res)) throw new Error(name + ' ' + arm.role + ': a used P changes the result');
        console.log(`${arm.role} ${name}: used P gives the same result`);
      }
      if (arm.added && arm.fc === 'N') {
        const again = E15.simulate(FS.prepareViaCell(name, root), FS.optionsFor(c, rc, null, globalOf, arm).o);
        if (FS.sigOf(again) !== FS.sigOf(res)) throw new Error(name + ': patched engine differs without forecast');
        checkedN++;
      }
      for (const r of res.riders) {
        const key = [arm.role, name, r.rq].join('|');
        if (!arm.added) {
          if (sealedRows.get(key) !== rowLine(arm.role, name, r)) throw new Error(key + ': rider row differs from the sealed predictions');
          const ps = JSON.stringify(timed(r).map(x => x[1]));
          if (sealedSeq.get(key) !== ps) throw new Error(key + ': drop sequence differs from the sealed sequences');
          checkedRows++; checkedSeq++;
        } else {
          rowLines.push(rowLine(arm.role, name, r));
        }
        timedLines.push(timedLine(arm.role, name, r));
      }
      if (arm.added) {   // emulate_fullsys.js adapter's counts row
        const fc = res.fcCount || {};
        countLines.push([arm.role, name, res.riders.length, res.riders.filter(r => r.served).length, res.rejected,
          (res.diag || {}).relocations, (res.diag || {}).genDecisions, res.riders.filter(r => r.aSH === 0).length,
          fc.clippedInitial, fc.clippedRevision, Date.now() - t].join('\t'));
      }
      console.log(`${arm.role} ${name}: ${res.riders.length} riders, served ${res.riders.filter(r => r.served).length}, ${Date.now() - t} ms`);
    }
  }
  fs.writeFileSync(timedOut, timedLines.join('\n') + '\n', { flag: 'wx' });
  fs.writeFileSync(rowsOut, rowLines.join('\n') + '\n', { flag: 'wx' });
  fs.writeFileSync(countsOut, countLines.join('\n') + '\n', { flag: 'wx' });
  console.log(`wrote ${timedOut} (${timedLines.length - 1} riders) and ${rowsOut} (${rowLines.length - 1} riders); `
    + `sealed rows equal ${checkedRows}, sealed sequences equal ${checkedSeq}, new N arm == original engine on ${checkedN} cell-arms`);
}

function plain(outDir) {
  const out = path.join(outDir, 'timed-plain.tsv');
  if (fs.existsSync(out)) throw new Error('refused: ' + out + ' exists');
  const want = new Map();
  { const t = readTsv(EXPECTED), ix = k => t.head.indexOf(k);
    for (const v of t.rows) want.set([v[ix('cell')], v[ix('config')], v[ix('mode')], v[ix('arm')]].join('|'), v[ix('sig')]); }
  const lines = [TIMED_HEAD];
  let ok = 0;
  for (const c of FS.f14Cells().filter(x => !only || only.has(x.cell))) {
    const rc = SH.readCell(path.join(CELLS_DIR, c.cell + '.json'));
    for (const arm of ARMS) {
      const P = SC.prepareScenario(rc.s, LIB.RUN_OVERRIDES, rc.dir);
      const res = E17.simulate(P, FS.optionsFor(c, rc, P, null, arm).o);
      const expected = want.get([c.cell, FS.CONFIG, arm.fc, arm.key].join('|'));
      if (!expected || FS.sigOf(res) !== expected) throw new Error(`${c.cell} ${arm.role}: sig differs from the company row`);
      ok++;
      for (const r of res.riders) lines.push(timedLine(arm.role, c.cell, r));
      console.log(`${c.cell} ${arm.role}: sig equal to the company row, ${res.riders.length} riders`);
    }
  }
  fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
  console.log(`wrote ${out}: ${lines.length - 1} riders; ${ok} cell-arms equal to the company rows`);
}

const [mode, a, b] = process.argv.slice(2);
if (mode === 'adapter' && a && b) adapter(a, b);
else if (mode === 'plain' && a) plain(a);
else { console.error('usage: adapter <data root> <out dir> | plain <out dir>'); process.exit(2); }
