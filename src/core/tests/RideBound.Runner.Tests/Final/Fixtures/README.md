# Final-configuration goldens

Inputs and recorded output for the tests in `Final/` and for the other Runner tests that use them through
`FinalRuns`. They pin the behaviour of the Runner configuration that the final experiment used, so that a
refactor can show it changed nothing. The expected output was recorded with the Runner at commit `ebca912`.

## Runs

There are 12 runs: 6 arms, each with 2 audit modes.

| arm | commitment | WP4 |
|---|---|---|
| FS-G2-SHRC71-CL | `SHRC71` | `G2-CL-*` |
| FS-G2-SHHZ71-N | `SHHZ71` | `G2-N-*` |
| FS-G2-SHHZ71-CL | `SHHZ71` | `G2-CL-*` |
| FS-G2-SHRC71-N | `SHRC71` | `G2-N-*` |
| INS-SHRC-CL | `INS-SHRC` | `INS-CL-*` |
| INS-SHHZ-N | `INS-SHHZ` | `INS-N-*` |

The two audit modes:
- `light-v1`, the mode the final runs used;
- `full-v1`.

Every run uses the final runs' argument vector:

```
--mode commitment --policy-config commitment/<c>.json --wp4-config wp4/<w>.json
--solver-seed-source manifest-master-seed --maximum-line-bytes 1073741824
```

The Runner runs as a child process, and its working directory is this folder.

## Files

**`commitment/`**
- `SHRC71.json` and `SHHZ71.json` are the commitment configs of the final campaign, byte for byte (SHA-256
  `d536e48d…` and `9ca0f404…`).
- `INS-SHRC.json` and `INS-SHHZ.json` are their insertion-only twins: `RCE35` / `EOHZ35` with the same slack
  (1000 ‰) and Shadow keys. Compared with the G2 configs, they:
  - add `"vehicle"` to `finalConfirmationLocks`;
  - set `vehicle_switch_count` `hardLimit 0`;
  - have no `ratchetLocks` and no `waitingRiderRelocation`.

**`wp4/`**
- `G2-*` comes from `wp4-via-g2k8-a1`. `INS-*` comes from `wp4-via-ins-a1`, which has no
  `candidateGeneration.waitingRiderRelocation`.
- `*-light-v1` adds `auditHash` `light-v1`. `*-full-v1` has no `auditHash` key, which means full-v1.
- All of them set `emitSolverExecutionEvidence` to `false`.
- All of them have a `shadowAllocation` block on the tiny network: priors `[3,1]` / `[2,4]`, threshold
  `0.01`. Both Shadow classes occur in the SHRC71 and INS arms; the SHHZ71 arms record only `guaranteed`.
- `*-CL-*` also has a `planningForecast`: profile `1.25`, `1.5`, `1.1`, with 900 s bins.
- `networkDirectory` is the relative path `network`, so no file depends on the machine.

**`network/`**
- `D.u16`: a 4 × 4 matrix of uint16 free-flow seconds.
- `network.json`: CSR edges, with nodes `n-0`..`n-3` in 2 zones.

**`scenarios/<arm>.ndjson`**

These are the lines the adapter sends to the Runner. Each driver reply (`decisionApplied` with the decision's
hash) is added at run time. The order is:
1. `hello`;
2. `initializeRun`, whose `policyConfigurationHash` is the light-v1 run's binding (the test puts in the binding
   of the run it is driving);
3. a checkpoint request;
4. 16 × (`eventBatch`, checkpoint request);
5. `shutdown`.

The world is a closed loop with 2 vehicles and 5 riders:
- a booking is confirmed one epoch after its request;
- the run starts 10 s before the 900 s forecast and Shadow bin edge;
- a traffic jam appears on `n-1`↔`n-2`;
- the movements were generated once from the baseline Runner's own plans and then frozen.

In the G2 arms, rider `r-1` is moved from `v-2` to `v-1` at decision 2. The INS arms never switch a vehicle.

**`expected/<run>.txt`**
- The first line holds the byte length and SHA-256 of the whole standard output.
- After it comes one line per stdout line:
  - a line that starts with `{` is the exact line;
  - any other line has the form `<messageType> <UTF-8 byte length> <sha256>`. This form is used for every
    checkpoint, and for the decisions of every run except the `light-v1` runs of the two main arms
    (FS-G2-SHRC71-CL and FS-G2-SHHZ71-N), which are kept verbatim. It keeps this folder under 0.5 MB.
- When a test fails, it writes the actual stdout to `<temp>/ridebound-final-actual/<run>.stdout`.

## How the expected output was made and checked

1. A separate driver ran the `ebca912` Runner. It handled the protocol the same way as `RunnerProcess.cs`
   and the ADR-090 byte harness.
2. The same driver, given the original absolute-path configs, reproduced the stdout SHA-256 of the extended
   byte harness for all 12 runs.
3. With the relative path, the output is the same once the 64-hex hash values are masked. Only the
   configuration hash changes, and with it every hash that depends on it.

Restoring from any of the 17 checkpoints of a run reproduces the rest of that run byte for byte
(`Restore_from_every_checkpoint_reproduces_the_rest_of_the_session`).

## The run that ends in an error

`INS-SHRC-CL-early-boarding.light-v1` (`FinalRuns.EarlyBoarding`) uses the configuration and binding of
`INS-SHRC-CL.light-v1`. Its scenario, `scenarios/INS-SHRC-CL-early-boarding.ndjson`, has the same lines as
`INS-SHRC-CL.ndjson` up to batch 10. Then it has its own batch 11 and `shutdown`:
1. In batch 11, rider `r-1` boards `v-2` at 955,003 ms, about 16 s before its promised pickup (971,401 ms).
2. An onboard rider keeps its promised pickup, and the projected drop now comes before it. So every candidate of
   the vehicle is rejected, the kept route too.
3. The Runner answers `INTERNAL_ERROR` with disposition `failSession`. The checkpoint request after it gets
   `INVALID_SESSION_STATE`.

The expected file was recorded with the `ebca912` Runner, like the other runs. It keeps the two error lines
verbatim. The test is `An_early_boarding_session_fails_with_the_error_recorded_at_ebca912`.
