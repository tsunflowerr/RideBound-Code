// ADR-089 GĐ4: RideBound configurations for the final comparison and the forecast gate, from the sealed inputs.
//   commitment  SHRC71 = G2-RCE35.json with dropEtaBudgetSlackPermille 1000 + dropEtaShadowAllocation;
//               SHHZ71 = G2-EOHZ35.json with dropEtaDeadlineSlackPermille 1000 (relaxed) + dropEtaShadowAllocation;
//               RCE35 (forecast gate) = G2-RCE35.json as bridged.
//   WP4         wp4-via-g2k8-a1-fast.json plus, per cell, `shadowAllocation` (shadow-inputs/<cell>.json of the sealed
//               prediction run; the network of shadow-gate/manhattan-network with its SHA-256) and, for the CL arms,
//               `planningForecast` (the cell's prev4 profile). Doubles are written as JavaScript's shortest decimal
//               string (String(x)), which .NET parses back to the same double.
// Usage: node make_job_configs.js fullsys <data root> | forecast-gate <data root>
'use strict';
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const KIT = 'E:/Code/Report_INT3508/ridebound-scratchpad';
const NETWORK = 'E:/RideBoundData/research/via-full-v1/shadow-gate/manhattan-network';
const G2_RCE35 = path.join(KIT, 'via-bridge-2026-10-22/configs/G2-RCE35.json');
const G2_EOHZ35 = path.join(KIT, 'via-bridge-2026-10-22/configs/G2-EOHZ35.json');
const WP4 = path.join(KIT, 'fastrun-2026-11-02/configs/wp4-via-g2k8-a1-fast.json');
const sha = f => crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const str = x => (x === Infinity ? '+Infinity' : x === -Infinity ? '-Infinity' : String(x));

function writeNew(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n', { flag: 'wx' });
}

function commitment(base, edit) {
  const config = JSON.parse(fs.readFileSync(base, 'utf8'));
  if (config.policies.length !== 1) throw new Error('one policy expected');
  edit(config.policies[0]);
  return config;
}

function shadowBlock(inputs) {
  return {
    networkDirectory: NETWORK,
    distancesSha256: sha(path.join(NETWORK, 'D.u16')),
    graphSha256: sha(path.join(NETWORK, 'network.json')),
    kappa: str(inputs.kappa),
    factors: inputs.factors.map(str),
    pickupWindowMs: inputs.omegaPms,
    strideMs: inputs.strideMs,
    intervalMs: inputs.intervalMs,
    alpha: str(inputs.alphaS),
    phi: str(inputs.phi),
    sampled: inputs.sampled,
    priorOrigin: inputs.prior.O,
    priorDestination: inputs.prior.D,
    threshold: str(inputs.theta),
    binMs: 900000,
  };
}

function forecastBlock(profile) {
  return { profile: profile.map(str), binMs: 900000 };
}

const [mode, root] = process.argv.slice(2);
const out = path.join(root, 'job-configs');
const wp4 = JSON.parse(fs.readFileSync(WP4, 'utf8'));
if (mode === 'fullsys') {
  writeNew(path.join(out, 'commitment', 'SHRC71.json'), commitment(G2_RCE35, p => {
    if (p.dropEtaBudgetSlackPermille !== 350) throw new Error('G2-RCE35 template changed');
    p.dropEtaBudgetSlackPermille = 1000; p.dropEtaShadowAllocation = true;
  }));
  writeNew(path.join(out, 'commitment', 'SHHZ71.json'), commitment(G2_EOHZ35, p => {
    if (p.dropEtaDeadlineSlackPermille !== 350 || p.dropEtaDeadlineRelaxed !== true) throw new Error('G2-EOHZ35 template changed');
    p.dropEtaDeadlineSlackPermille = 1000; p.dropEtaShadowAllocation = true;
  }));
  const inputsDir = path.join(root, 'predictions', 'shadow-inputs');
  let n = 0;
  for (const file of fs.readdirSync(inputsDir).sort()) {
    const inputs = JSON.parse(fs.readFileSync(path.join(inputsDir, file), 'utf8'));
    if (!inputs.forecast) throw new Error(file + ': no forecast profile');
    writeNew(path.join(out, 'wp4', 'FS-G2-SHRC71-CL', inputs.cell + '.json'),
      { ...wp4, shadowAllocation: shadowBlock(inputs), planningForecast: forecastBlock(inputs.forecast.profile) });
    writeNew(path.join(out, 'wp4', 'FS-G2-SHHZ71-N', inputs.cell + '.json'), { ...wp4, shadowAllocation: shadowBlock(inputs) });
    n++;
  }
  console.log(`fullsys: 2 commitment configs, ${2 * n} WP4 configs`);
} else if (mode === 'forecast-gate') {
  const profiles = JSON.parse(fs.readFileSync(path.join(root, 'predictions-forecast-gate', 'forecast-gate-profiles.json'), 'utf8'));
  writeNew(path.join(out, 'commitment', 'RCE35.json'), commitment(G2_RCE35, () => {}));
  for (const [cell, profile] of Object.entries(profiles)) {
    writeNew(path.join(out, 'wp4', 'FS-G2-RCE35-CL', cell + '.json'), { ...wp4, planningForecast: forecastBlock(profile) });
  }
  console.log(`forecast gate: ${Object.keys(profiles).length} WP4 configs`);
} else {
  console.error('usage: node make_job_configs.js fullsys|forecast-gate <data root>');
  process.exit(2);
}
