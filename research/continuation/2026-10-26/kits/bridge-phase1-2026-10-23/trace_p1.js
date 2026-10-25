// Diagnostic only (not evidence): list the emulator's G2 moves of one phase-1 cell-arm, with time, rider and vehicles.
// Usage: node trace_p1.js <cell> <U|RCE|EOHZ|EO|DLS> [alpha|B_ms] [g2Targets]
'use strict';
const path = require('path');
const EMU = 'C:/RideBoundContinuation/2026-10-23/emulator-wf15';
const F = require(path.join(EMU, 'fixtures.js'));
const E = require(path.join(EMU, 'engine.js'));
const [cell, name, param, targets] = process.argv.slice(2);
const P = F.prepareFixtureCell(cell, { variant: { panelRoot: 'C:/RideBoundData/research/fixtures-via-bridge-2000-v1' } });
const arm = name === 'U' ? { name: 'U', B: 0 } : ['RCE', 'EOHZ', 'EO'].includes(name) ? { name, alpha: Number(param) } : { name, B: Number(param) };
const reopt = targets ? { g2: 99, g2Targets: Number(targets) } : { g2: 99 };
const res = E.simulate(P, { reopt, lock: 'ratchet', arm, trace: true });
for (const t of res.trace || []) if (t.rel) console.log(JSON.stringify({ t: t.t, rel: t.rel }));
console.log('relocations', res.diag.relocations);
