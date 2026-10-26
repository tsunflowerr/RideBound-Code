// wf15 worker pool (pattern of wf14/scripts/confirm.js): at most 8 workers; the full network is shared by reference
// (SharedArrayBuffers in workerData). Worker entry: shworker.js. runPool resolves when every task has answered.
'use strict';
const { Worker } = require('worker_threads');
const path = require('path');
const NETF = require('../runner/netfull.js');

let NET_SHARED = null;
function sharedNet() { if (!NET_SHARED) NET_SHARED = NETF.share(NETF.build()); return NET_SHARED; }

function runPool(tasks, { workers = 8, onResult = () => {}, log = true } = {}) {
  if (workers > 8) throw new Error('at most 8 worker threads');
  return new Promise((resolve, reject) => {
    if (!tasks.length) { resolve({ done: 0, errors: 0 }); return; }
    const t0 = Date.now();
    const net = sharedNet();
    let next = 0, nDone = 0, errors = 0;
    const ws = [];
    const feed = w => { if (next < tasks.length) w.postMessage({ idx: next, task: tasks[next++] }); };
    for (let i = 0; i < Math.min(workers, tasks.length); i++) {
      const w = new Worker(path.join(__dirname, 'shworker.js'), { workerData: { net }, resourceLimits: { maxOldGenerationSizeMb: 1200 } });
      ws.push(w);
      w.on('message', msg => {
        nDone++;
        if (msg.error) { errors++; if (log) console.log('ERROR', msg.task.cell, msg.task.config, (msg.task.arms || []).join(','), msg.error.slice(0, 600)); }
        try { onResult(msg); } catch (e) { reject(e); return; }
        if (log && (nDone % 20 === 0 || nDone === tasks.length)) console.log(`${nDone}/${tasks.length} ${((Date.now() - t0) / 1000).toFixed(0)} s`);
        if (nDone === tasks.length) { for (const x of ws) x.terminate(); resolve({ done: nDone, errors, ms: Date.now() - t0 }); } else feed(w);
      });
      w.on('error', e => reject(e));
      feed(w);
    }
  });
}

module.exports = { runPool, sharedNet };
