// Section 9 check "584 riders of real runs that were not used to tune the emulator": six real RideBound + FleetPy jobs
// on cell d20181112-s10-r1-w08 (panel wp14-development-panel-v1), maximum ride 1.5x and 2.5x (the 2.5x jobs use the
// 1.5x fixture with permille 2500), arms U, RC 30 s and RC 300 s.
//   1. Always: the accepted and rejected request sets of each job (data/realjob/<job>-run-00.json, "semantic").
//   2. Only when the two review transcripts are given (jobs rv-x15-U-mf and rv-x25s-U-gc896, decision-identical reruns
//      of x15-U-mf2 and x25s-U-gc896; about 90 MB each, not shipped): per rider t0, p0, pickup, A and the drop-side
//      publication sequence (decision time, drop ETA, |c|), without RideBound's extra publication at alighting.
// Usage: node realjob.js <cells dir> [<rv-x15-U-mf transcript-00.ndjson> <rv-x25s-U-gc896 transcript-00.ndjson>]
'use strict';
const fs = require('fs'), path = require('path'), readline = require('readline');
const F = require('./fixtures.js');
const E = require('../engine.js');

const [cellsDir, tx15, tx25] = process.argv.slice(2);
if (!cellsDir || (tx15 && !tx25)) {
  console.error('usage: node realjob.js <cells dir> [<x15 transcript> <x25s transcript>]');
  process.exit(2);
}
const CELL = 'd20181112-s10-r1-w08';
const panel = path.join(cellsDir, 'wp14-development-panel-v1');
const simulate = (permille, name, B) =>
  E.simulate(F.prepareFixtureCell(CELL, panel, { permille }), { arm: { name, B } });

const JOBS = [['x15-U-mf2', 1500, 'U', 0], ['x15-C30-mf', 1500, 'RC', 30000], ['x15-C300-mf', 1500, 'RC', 300000],
  ['x25s-U-gc896', 2500, 'U', 0], ['x25s-C30-mf', 2500, 'RC', 30000], ['x25s-C300-gc896', 2500, 'RC', 300000]];
let jobsOk = 0, accepted = 0;
for (const [job, permille, arm, B] of JOBS) {
  const file = path.join(__dirname, 'data', 'realjob', job + '-run-00.json');
  const sem = JSON.parse(fs.readFileSync(file, 'utf8')).semantic;
  const realAcc = new Set(sem.acceptedRequestIds), realRej = new Set(sem.rejectedRequestIds);
  const P = F.prepareFixtureCell(CELL, panel, { permille });
  const rqToId = new Map(P.reqs.map(q => [q.rq, q.id]));
  const res = E.simulate(P, { arm: { name: arm, B } });
  const simAcc = new Set(res.riders.map(r => rqToId.get(r.rq)));
  const onlyReal = [...realAcc].filter(x => !simAcc.has(x)).length;
  const onlySim = [...simAcc].filter(x => !realAcc.has(x)).length;
  const same = onlyReal === 0 && onlySim === 0 && realRej.size === res.rejected;
  if (same) { jobsOk++; accepted += realAcc.size; }
  console.log(JSON.stringify({ job, realAccepted: realAcc.size, realRejected: realRej.size, simAccepted: simAcc.size,
    simRejected: res.rejected, onlyReal, onlySim, acceptSetIdentical: same }));
}
console.log(`${jobsOk}/6 jobs accept/reject sets identical; accepted riders in identical jobs ${accepted}`);
let ok = jobsOk === 6;

// Real side of one transcript: per rider the publications [time, drop ETA, |c|], t0, pickup and A.
async function readTranscript(file) {
  const R = new Map(); let now = 0;
  const get = id => { if (!R.has(id)) R.set(id, { pubs: [], t0: NaN, pickup: NaN, A: NaN }); return R.get(id); };
  for await (const l of readline.createInterface({ input: fs.createReadStream(file), crlfDelay: Infinity })) {
    if (!l) continue;
    const m = JSON.parse(Buffer.from(JSON.parse(l).frameBase64, 'base64').toString('utf8'));
    if (m.messageType === 'eventBatch') {
      now = m.simTimeMs;
      for (const e of m.payload.events) {
        if (e.eventType === 'passengerBoarded') get(e.payload.requestId).pickup = now;
        if (e.eventType === 'passengerAlighted') get(e.payload.requestId).A = now;
      }
    } else if (m.messageType === 'decision') {
      for (const a of m.payload.actions) {
        if (a.decisionType !== 'promisePublished') continue;
        const p = a.payload, r = get(p.promise.requestId);
        if (p.reasonCode === 'INITIAL_BOOKING_CONFIRMATION') r.t0 = now;
        r.pubs.push([now, p.promise.dropEtaMs, p.decisionDelta.dropEtaTotalMs]);
      }
    }
  }
  return R;
}

function compareRiders(label, R, res) {
  const st = { label, riders: 0, realPromised: [...R.values()].filter(r => r.pubs.length).length, t0: 0, p0: 0,
    pickup: 0, A: 0, dropSeqExact: 0, allExact: 0, atArrival: 0, revisionsCompared: 0 };
  for (const s of res.riders) {
    const r = R.get(s.rq);
    if (!r || !r.pubs.length) continue;
    st.riders++;
    let pubs = r.pubs;
    if (pubs.length > 1 && pubs[pubs.length - 1][0] === r.A) { st.atArrival++; pubs = pubs.slice(0, -1); }
    const sim = [[s.t0, s.p0, 0], ...s.revs.map(v => [v[0], v[7], Math.abs(v[2])])];
    st.revisionsCompared += sim.length;
    const hit = { t0: s.t0 === r.t0, p0: s.p0 === r.pubs[0][1], pickup: s.pickup === r.pickup, A: s.A === r.A,
      dropSeqExact: JSON.stringify(sim) === JSON.stringify(pubs) };
    for (const [k, v] of Object.entries(hit)) if (v) st[k]++;
    if (Object.values(hit).every(Boolean)) st.allExact++;
  }
  console.log(JSON.stringify(st));
  return st.riders > 0 && st.allExact === st.riders && st.riders === st.realPromised;
}

(async () => {
  if (tx15) {
    const R15 = await readTranscript(tx15);
    ok = compareRiders('x15-U', R15, simulate(1500, 'U', 0)) && ok;
    ok = compareRiders('x15-C300', R15, simulate(1500, 'RC', 300000)) && ok;   // x15-C300-mf = x15-U-mf2 decisions
    ok = compareRiders('x25s-U', await readTranscript(tx25), simulate(2500, 'U', 0)) && ok;
  }
  process.exitCode = ok ? 0 : 1;
})();
