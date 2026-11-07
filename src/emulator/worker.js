// wf15 worker thread: attaches the shared full network once, then runs tasks (shcore.runTask) posted by pool.js.
'use strict';
const { parentPort, workerData } = require('worker_threads');
const NETWORK = require('./network.js');
const CORE = require('./shcore.js');
const LIB = require('./shlib.js');

LIB.setData(workerData.data);   // the data root of the main thread (run.js --data)

const NET = NETWORK.attach(workerData.net);
parentPort.on('message', ({ idx, task }) => {
  try {
    const out = CORE.runTask(task, NET);
    parentPort.postMessage({ idx, task, ...out });
  } catch (e) {
    parentPort.postMessage({ idx, task, rows: [], error: String(e && e.stack || e) });
  }
});
