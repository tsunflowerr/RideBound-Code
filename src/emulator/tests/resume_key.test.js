// Test for the resume key of run.js. A run skips every task whose row is already in --out, so the key a task gives for
// one of its arms (taskKey) must equal the key of the row that task wrote (rowKey). The rows are the recorded n7 and
// fullsys rows in src/analysis/data (7,200 rows written by the research driver: Shadow, EOHZ and RCE arms; forecast
// modes N, CL, PAD and CLc; panels F14 and F16). Run: node --test src/emulator/tests
'use strict';
const test = require('node:test');
const assert = require('node:assert');
const fs = require('fs'), path = require('path'), zlib = require('zlib');
const RUN = require('../run.js');

const DATA = path.join(__dirname, '..', '..', 'analysis', 'data');

test('resume key: a task gives the key of the row it wrote (recorded n7 and fullsys rows)', () => {
  let rows = 0;
  for (const file of ['wf21-n7-rows.ndjson.gz', 'wf20-fullsys-rows.ndjson.gz']) {
    const text = zlib.gunzipSync(fs.readFileSync(path.join(DATA, file))).toString('utf8');
    for (const line of text.split('\n').filter(Boolean)) {
      const row = JSON.parse(line);
      // the task fields that run.js builds for this row's cell, config and arm (fcIndex is a path; the row keeps its file name)
      const task = { cell: row.cell, config: row.config, scoreMode: row.scoreMode, thetas: { [row.arm]: row.theta },
        alphaS: row.alphaS, strideMs: row.strideMs, variant: row.variant, fc: row.mode, flat: { [row.arm]: row.flatRatio },
        fcIndex: row.fcIndex ? path.join('any', row.fcIndex) : null, panel: row.panel };
      assert.strictEqual(RUN.taskKey(task, row.arm), RUN.rowKey(row), `${file}: ${row.cell} ${row.config} ${row.arm}`);
      rows++;
    }
  }
  assert.strictEqual(rows, 7200);
});
