// wf15 worker thread: attaches the shared full network once, then runs tasks (shcore.runTask) posted by pool.js.
'use strict';
const { parentPort, workerData } = require('worker_threads');
const NETF = require('../runner/netfull.js');
const CORE = require('./shcore.js');

const NET = NETF.attach(workerData.net);
parentPort.on('message', ({ idx, task }) => {
  try {
    const out = CORE.runTask(task, NET);
    parentPort.postMessage({ idx, task, ...out });
  } catch (e) {
    parentPort.postMessage({ idx, task, rows: [], dump: [], error: String(e && e.stack || e) });
  }
});
