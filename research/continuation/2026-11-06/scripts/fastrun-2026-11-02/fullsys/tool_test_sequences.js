// TOOL TEST ONLY (not evidence; written after the real runs exist): emulator drop-ETA sequences for two G3b cell-arms
// (VIA-EOHZ35 and VIA-G2K8-EOHZ35 on d20190213-vf-r0-w08), in the format of emulate_sequences.js, so that
// compare_drop_sequences.py can be checked on real transcripts that already matched rider by rider on the other fields.
// Arms and engine as emulate_via_full.js (emulator-wf15 engine, no forecast).
// Usage: node tool_test_sequences.js <out.tsv>
'use strict';
const fs = require('fs');
const path = require('path');
const FS = require('./emulate_fullsys.js');
const E15 = require('C:/RideBoundContinuation/2026-10-23/emulator-wf15/engine.js');
const ROOT = 'E:/RideBoundData/research/via-full-v1';
const K8 = { reopt: { g2: 99, g2Targets: 8 }, lock: 'ratchet' };
const out = process.argv[2];
if (!out || fs.existsSync(out)) { console.error('usage / exists'); process.exit(2); }
const lines = ['role\tcell\trequestId\tserved\trev60\ttv5\tdropSequence'];
const cell = 'd20190213-vf-r0-w08';
for (const [role, opts] of [['VIA-EOHZ35', {}], ['VIA-G2K8-EOHZ35', K8]]) {
  const res = E15.simulate(FS.prepareViaCell(cell, ROOT), { ...JSON.parse(JSON.stringify(opts)), arm: { name: 'EOHZ', alpha: 0.35 } });
  for (const r of res.riders) {
    const seq = [r.p0];
    for (const v of r.revs) if (v[7] !== seq[seq.length - 1]) seq.push(v[7]);
    lines.push([role, cell, r.rq, r.served ? 1 : 0, '', '', JSON.stringify(seq)].join('\t'));
  }
  console.log(role, res.riders.length, 'riders');
}
fs.writeFileSync(out, lines.join('\n') + '\n', { flag: 'wx' });
