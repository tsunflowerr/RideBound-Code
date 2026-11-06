// Exact emulation of the identifiers RideBound uses for its final tie-break (candidate-id-rank levels):
//   adapter ids (mapping.py:20-77: "<ns>-" + sha256(typed frame)), travel snapshot hash (mapping.py:303-333),
//   generated stop ids and candidate ids (CandidateIdentity.cs:41-87, 89-104, 164-237).
'use strict';
const crypto = require('crypto');

const sha = buf => crypto.createHash('sha256').update(buf).digest('hex');

function u64(n) { const b = Buffer.alloc(8); b.writeBigUInt64BE(BigInt(n)); return b; }
function typedInt(n) { const d = Buffer.from(String(n), 'ascii'); return Buffer.concat([Buffer.from('i'), u64(d.length), d]); }
function typedStr(s) { const d = Buffer.from(s, 'utf8'); return Buffer.concat([Buffer.from('s'), u64(d.length), d]); }

const vehicleId = vid => 'veh-' + sha(typedInt(vid));
const nodeId = idx => 'node-' + sha(typedInt(idx));

function frame(tag, value) {
  const t = Buffer.from(tag, 'utf8');
  const v = Buffer.from(String(value), 'utf8');
  const h = Buffer.alloc(2); h.writeUInt16BE(t.length);
  return Buffer.concat([h, t, u64(v.length), v]);
}

const STOP_DOMAIN = Buffer.from('RideBound.GeneratedStop.v1\0', 'utf8');
const CAND_DOMAIN = Buffer.from('RideBound.InsertionCandidate.v1\0', 'utf8');
const SNAP_DOMAIN = Buffer.from('RideBound.FleetPyTravelSnapshot.v1\0', 'utf8');

function stopId(rq, kind) {
  const h = sha(Buffer.concat([STOP_DOMAIN, frame('kind', kind === 0 ? 'pickup' : 'dropOff'), frame('requestId', rq)]));
  return (kind === 0 ? 'rb-p-' : 'rb-d-') + h;
}

// Python: seconds_to_milliseconds(x) = Decimal(str(x)) * 1000 quantized ROUND_HALF_EVEN.
function msFromSecondsFloat(x) {
  let s = String(x);
  if (/e/i.test(s)) throw new Error('exponent form not supported: ' + s);
  let [ip, fp = ''] = s.split('.');
  const head = fp.slice(0, 3).padEnd(3, '0');
  const rest = fp.slice(3);
  let n = Number(ip + head);
  if (rest.length && /[1-9]/.test(rest)) {
    if (rest[0] > '5' || (rest[0] === '5' && /[1-9]/.test(rest.slice(1)))) n += 1;
    else if (rest[0] === '5') { if (n % 2 === 1) n += 1; }
  }
  return n;
}

// Snapshot travel time of an arc: FleetPy edge tt = float(ms/1000) * factor (preflight_world.py:154-157),
// reported through seconds_to_milliseconds.
function snapshotMs(ms, factor) {
  if (ms === 0) return 0;
  return msFromSecondsFloat((ms / 1000) * factor);
}

function snapshotHash(N, T0, factor, version, nodeIds) {
  const ids = nodeIds || Array.from({ length: N }, (_, i) => nodeId(i));
  const pairs = [];
  for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) if (i !== j) pairs.push([ids[i], ids[j], snapshotMs(T0[i * N + j], factor)]);
  pairs.sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : a[1] < b[1] ? -1 : a[1] > b[1] ? 1 : 0));
  const parts = pairs.map(p => `{"fromNodeId":"${p[0]}","toNodeId":"${p[1]}","travelTimeMs":${p[2]}}`);
  const json = `{"arcs":[${parts.join(',')}],"version":${version}}`;
  return sha(Buffer.concat([SNAP_DOMAIN, Buffer.from(json, 'utf8')]));
}

// Stop body after the leading (tag, stopId) frame.
function stopBody(nodeIdStr, kind, rq, serviceMs) {
  return Buffer.concat([frame('nodeId', nodeIdStr), frame('kind', kind === 0 ? 'Pickup' : 'DropOff'), frame('requestId', rq), frame('serviceDurationMs', serviceMs)]);
}

// stops: arrays of {sid, body}
function candidateId(vehIdStr, evalMs, snapHash, planVersion, executedCount, newRqs, frozen, mutable) {
  const parts = [CAND_DOMAIN, frame('vehicleId', vehIdStr), frame('evaluationTimeMs', evalMs), frame('travelSnapshotHash', snapHash),
    frame('planVersion', planVersion), frame('executedStopCount', executedCount)];
  for (const rq of [...newRqs].sort()) parts.push(frame('newRequestId', rq));
  for (const s of frozen) { parts.push(frame('frozenStop', s.sid)); parts.push(s.body); }
  for (const s of mutable) { parts.push(frame('mutableStop', s.sid)); parts.push(s.body); }
  return 'candidate-v1-' + sha(Buffer.concat(parts));
}

module.exports = { vehicleId, nodeId, stopId, stopBody, snapshotHash, snapshotMs, msFromSecondsFloat, candidateId, frame };
