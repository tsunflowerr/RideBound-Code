# Raw evidence: completeness critic and gap-fill findings (English, as returned by the audit agents)

These are the unedited reports the plan in `KE-HOACH-REFACTOR.md` was built from. Local machine paths are replaced by `<scratchpad>` and `<repo>`; probe scripts under `<scratchpad>` are not kept.

## Critic: gaps

### Gap 0: classification-contradicts-scope / missed thesis dependency

The scope says thesis numbers come only from the plain-mode emulator, and that C# Runner outputs only validate it. Repository evidence shows the submitted thesis also cites C# Runner results:
- ADR-088 (thesis commit 7f93ee6, 95 pages) added a W04 component table: 'in RideBound / only in the emulator / verified matching'.
- W05: '6 of the 12 Via-size runs'.
- W28: the per-decision-cap sentence carries the bridge numbers.
- docs/18:6028: 'the thesis cites the Manhattan data'.
- The domain verdict found T3.8 (170 jobs on runner-tier3-v2, stability-first wp4-tier3-v2, incl. V-30 customerVisible and M+ fixed-slack arms) in Table 4.3/4.5/4.6 of the in-repo v5 text.
Only the domain area acted on this. Other areas did not:
- Algorithms leaves stability-first as 'unknown'.
- Runner and research-configs mark the stability-first default, the Stage 1b/T3.8 analysis tooling (research/continuation/2026-10-15, kits/tang3/stage1b harness) and the Wp6Normalize panel fixtures as historical-only.
The REMOVE_OK verdicts were never checked against the W04 component table: two-sided deadline, freeze horizon, B2–B5, origin-hold, and anything listed as 'in RideBound'.

**Where:** Scope thesis_number_sources; docs/18-status-and-decision-log.md:5832-5850 (ADR-088 W04/W05/W28) and :6028; domain verdict 'customerVisible budget basis'; algorithms component 'Selection objective orders'; runner/research-configs historical-only labels

**Suggested check:** 1. Open Report_INT3508 at commit 7f93ee6.
2. Extract every chapter-4 table and appendix number, plus the W04 component table.
3. For each number, record the producer: emulator plain, emulator adapter, or C# Runner (with the Runner build).
4. Reclassify the code paths behind every C# Runner number from historical to (b)-thesis: stability-first order, customerVisible, dropEtaDeadlineSlackMs, the drop_eta_total_ms hardLimit, the tier3 WP4 configs, and the T3.8 analysis scripts.
5. Re-check each REMOVE_OK against the 'in RideBound' column.
6. Replay one recorded T3.8 transcript with a HEAD Runner to learn whether T3.8 is reproducible from HEAD or only from runner-tier3-v2 (45f8026).

### Gap 1: weak evidence: gate coverage overstated

Nearly every SIMPLIFY or REMOVE condition in the runner, domain, application and algorithms areas ends with 'rerun G1/G2 (228/228)'. All 228 G1 jobs use cost-first WP4 configs: grep over a0_gate/run_bridge/run_g2/replay_gate_dwell/run_p1/run_r128 finds only wp4-tier3-v2-costfirst, wp4-costfirst-forced, wp4-g2-costfirst(-k3/-k8/-forced). They also all run on the Manhattan network-basic engine with full-v1, uncompressed transcripts, and no Shadow or planningForecast. G1 therefore cannot detect regressions in:
- stability-first ordering: the T3.8 thesis rows, WP9 H6 and the Stage 1b SF arms;
- C2 and B1 rolling-cost;
- light-v1 hash bytes: G2 strips *Hash, and the contracts area notes there is no fixed light-v1 vector;
- complete-graph-direct and gzip;
- Shadow and forecast session paths: no Runner session test, only 3 passed gate jobs, and 5 still pending.
Several verdicts treat G1 as sufficient while touching exactly these paths:
- skip the hard-utilization vectors under cost-first;
- rename the RollingCost models;
- trim the SolverBackedFleetSelection branches;
- the light-v1 single-build checkpoint;
- the ProtocolHash span-overload wrapper.

**Where:** research/continuation/2026-10-26/kits/{via-bridge-2026-10-22,bridge-phase1-2026-10-23}/*.py config_for mappings; verdicts in the runner (checkpoint handler), algorithms (hard utilization vectors, orchestrator) and contracts (ProtocolHash) areas

**Suggested check:** Map each SIMPLIFY/REMOVE item to the code paths it touches and to the configs that exercise them. Before any refactor, add the guards that are missing:
1. A byte-replay set of stability-first transcripts (recorded T3.8 or WP9 jobs).
2. Committed fixed vectors for light-v1 decision and checkpoint hashes.
3. A Runner-session golden test with shadowAllocation and planningForecast.
4. One complete-graph-direct, gzip job replay.
Record which gate guards which item.

### Gap 2: uncovered area: emulator code (the thesis-number producer)

The JS emulator produced the thesis numbers, but it received a blanket 'KEEP, frozen' with no (a)/(b)/(c) analysis. That is the main gap for the owner's 'minimal, explainable, rewritable' goal. Facts the audit did not record:
- engine17.js is 6 byte-identical copies: blob 927c9b4c, 842 lines, in wf17/r1, r2, r3, wf20/fullsys, wf20/am2017 and wf21/n7.
- identity.js (8f078e7a) and normalizer_emul.js (8b603a52) also have 6 identical copies each, sscore.js (4097f131) 4, and forecast.js (9181db5f) 3.
- ARM_CODES lists 18 arms (engine17.js:36). Most are emulator-only: RC, PS, DLHZ, DLS, NC, OC, RR, FZ, NCE, OCE, SHNC. No area mapped which runbook uses which arm, option (TOL, clipPickup, poolCap, reopt) or identity mode.
- The adapter-mode code behind every sealed C#-vs-emulator prediction is external: engine_wf15_clint.js, emulate_via_full.js, the wf15 engine. Its relation to the in-repo identity.js was not audited.

**Where:** research/continuation/2026-10-26/company-emulator/sp/{wf17/r1-identity-fence-pad,wf17/r2-penalty-regime,wf17/r3-curves-nodes,wf20/fullsys,wf20/am2017,wf21/n7}/code/runner/engine17.js:30-45; sp/wf17/*/CODE-HASH.txt; research-configs components for the emulator

**Suggested check:** 1. For each RUNBOOK.sh (wf20/fullsys, wf21/n7, wf20/am2017, wf17 r1–r3), list the --arms, --fc and --x flags and the opts actually passed.
2. Grep engine17.js for each arm code and option branch, and classify each branch: (c) final 2×2 and main comparison; (b) robustness, bridge predictions or fleet size; (a) unused.
3. Confirm which CODE-HASH/PLAN-HASH seals each copy, to learn whether the copies can be deduplicated.
4. Record sha256 values for engine_wf15_clint.js, emulate_via_full.js and the wf15 engine, and diff their adapter-mode logic against the in-repo identity.js.

### Gap 3: cross-area contradictions

The per-area verdicts disagree on the same paths:
(1) tools/RideBound.CandidateHotPathBenchmark: algorithms says KEEP ('useful to measure'), benchmarking says REMOVE_OK. Application notes that its recorded JSON hashes constrain the 5-argument OnlineState constructor.
(2) Capability downgrade: the runner verdict says to keep Contracts' protocol-level 'downgraded' support (HelloMessages.cs:25,341-482, capability-selection.schema.json, valid-hello-ack-downgraded.json). The contracts verdict removes CapabilitySelectionStatus.Downgraded, downgradePolicyId and that fixture.
(3) Retained-portfolio evidence: runner says KEEP_REQUIRED because WP13 E1 and WP14 'can no longer be re-run from HEAD'. Contracts and adapter-scripts show they already cannot: the E1 freeze pins DecisionMessages.cs 22dd15af while HEAD is 59769c30, so E1 reproduces only at 38d517d and WP14 only at 41a4e11..8d1ea4d.
(4) research-configs keeps all of Benchmarking.Tests, benchmarks/fixtures/wp6 (e2e/metrics/planning) and schemas/wp6/v1. Benchmarking archives about 5.5k test lines plus the run-side schemas and fixtures.
(5) The contracts verdict on offerProposed/commitmentBreachDeclared requires keeping BreachCount in tools/RideBound.Wp6MetricOracle. Benchmarking archives that whole tool.

**Where:** algorithms component 'tools/RideBound.CandidateHotPathBenchmark' vs benchmarking verdict; runner verdict 'Capability downgrade branch' vs contracts verdict; runner verdict 'Retained-portfolio evidence profiles' vs contracts and adapter-scripts verdicts; research-configs test-suite and fixture components vs benchmarking verdicts

**Suggested check:** Merge all area verdicts into one table keyed by file path and resolve each conflict explicitly. Specific checks:
- (3): `git show 38d517d:src/RideBound.Contracts/Protocol/DecisionMessages.cs | sha256sum` against freeze-receipt-v1.json:340-342.
- (2): decide whether HelloMessages downgrade decoding stays as v1 protocol vocabulary.
- (1): check whether docs/19:747 or ADR-052 evidence must stay re-runnable at HEAD.

### Gap 4: missed dependency on recorded results

The benchmarking area keeps the FleetPy normalizer and calls it 'byte-frozen' via normalizerSourceSha256 34517a83…, but that hash covers only the 5 Normalization/*.cs files (FleetPyNormalizerSourceIdentity.cs:9-16). The normalizer's output bytes also depend on Contracts code that other areas plan to move or simplify:
- FleetPyManhattanNormalizer.cs:986 builds every requestArrived payload with EventBatchPayloadCodec.Encode. Contracts classifies that as an adapter-direction 'reverse codec, historical-only, move with Benchmarking'.
- :1010 and :1145 call CanonicalJson.Canonicalize.
- :174-209 call BenchmarkContractCodec.Encode and BenchmarkIdentity.Calculate*. Benchmarking proposes to trim both.
A change in any of these would silently change regenerated scenario-content and request bytes, and so scenario hashes, while normalizerSourceSha256 stays unchanged. Those bytes feed the emulator inputs for the thesis (lib.js:8,119), the 228 bridge drivers (sourceScenarioHash) and the WP9/WP14 panels.

**Where:** src/RideBound.Benchmarking/Normalization/FleetPyManhattanNormalizer.cs:174-209,986-1010,1145; FleetPyNormalizerSourceIdentity.cs; contracts component 'Reverse-direction codecs'; benchmarking verdict on Benchmarking.Contracts run-side documents

**Suggested check:** 1. Move the requestArrived encoder, CanonicalJson and the scenario/report codec and identity into the KEEP-frozen set.
2. Add a regeneration test: run tools/RideBound.Wp6Normalize on one wp9-confirmatory and one wp14-development-panel cell into a temp directory, compare against the committed scenario-content.json and normalization-report.json byte for byte, and require reusedExactDerivative=true.
3. Run that test on every change to Contracts or Benchmarking.Contracts.

### Gap 5: uncovered area

simulators/ridepy-ridebound (WP10) is not inventoried by any area, although it imports the final adapter runtime:
- ridebound_ridepy/session.py:8-10 imports canonical_json_bytes, wp4_policy_binding_hash, RunnerClient and ProtocolTranscriptRecorder;
- mapping.py:7-8 imports AdapterFailure, CanonicalIdRegistry and related names;
- fleet_state.py:11-12 imports ridebound_fleetpy.protocol.
Adapter-runtime proposals change these modules without checking RidePy: trimming the package __init__ re-exports, dropping the RunnerClient combined maximum_line_bytes, archiving restore, and consolidating the line bounds. The contracts area ties the nodeOnly position model to RidePy. WP10's negative Layer-3 result is (b) evidence, and wp10-ridepy-paired-subset-v1.json is kept by research-configs. The 4 tracked .pyc files under ridepy-ridebound were also left out.

**Where:** simulators/ridepy-ridebound/ridebound_ridepy/{session.py:8-10,mapping.py:7-8,fleet_state.py:11-12}, simulators/ridepy-ridebound/tests, wp10_canonical_run.py, wp10_subset_execute.py

**Suggested check:** 1. Run `git grep -n ridebound_fleetpy -- simulators/ridepy-ridebound` and list every imported symbol.
2. Add RidePy to the inventory as (b) WP10 evidence, and decide archive or keep together with the Runner's nodeOnly/positionModel support.
3. Make the RidePy test suite a required check for any ridebound_fleetpy change.

### Gap 6: uncovered area: build/toolchain provenance (live risk)

No area audited the toolchain, although recorded hashes depend on it:
- Google.OrTools is pinned to 9.15.6755 (RideBound.Solvers.OrTools.csproj:9), and every WP4 config is refused unless solverExecution.adapterVersion equals that string (Wp4RunnerConfiguration.cs:770-773; OrToolsCandidateSelectionSolver.cs:11).
- .github/dependabot.yml runs weekly NuGet updates with no ignore rule for Google.OrTools.
- The repo has 0 packages.lock.json files, so transitive NuGet versions are not locked.
- global.json allows SDK rollForward latestPatch from 10.0.301.
A merged dependency bump would make every recorded and final config fail to load and would break the CP-SAT byte determinism that G1 relies on. The in-repo record of how runner-fastrun-final1 and the final-v2 Runner were published (SDK, flags, deps.json) was not checked against what HEAD would build.

**Where:** .github/dependabot.yml; src/RideBound.Solvers.OrTools/RideBound.Solvers.OrTools.csproj:9; src/RideBound.Runner/Configuration/Wp4RunnerConfiguration.cs:770-773; global.json; Directory.Build.props; .github/workflows/ci.yml

**Suggested check:** 1. Add an ignore entry for Google.OrTools (and Google.Protobuf) in dependabot.yml.
2. Enable RestoreLockedMode and commit packages.lock.json for Runner and Solvers.
3. Compare the deps.json and runtimeconfig.json of the recorded runner-fastrun-final1 and final-v2 Runner against a HEAD publish (`dotnet publish -c Release` into a scratch directory, outside the repository).
4. Record the SDK and runtime versions in docs/18.

### Gap 7: weak REMOVE evidence (external consumers unchecked)

Many REMOVE_OK verdicts rest only on in-repo `git grep`, yet the final configs and gate tools are external and E: was absent on the audit machine. Two mechanisms make this risky:
- Both config parsers reject unknown keys (CommitmentPolicyConfiguration.cs:368-392; Wp4RunnerConfiguration.cs:300-310 RequireObject). Dropping parser support for a key breaks any external config that carries it, even at its default value.
- External scripts may import adapter or preflight private names, or set environment variables.
Affected verdicts: freezeHorizonMs/Locks, dropEtaDeadlineTwoSided, fixedFreeze, repair, multiplePlan, origin-hold-relocated-wait, stopServiceDurationMs, maximumRidersPerVehicle, the adapter restore/restart path, wp14r_* helpers, and the RIDEBOUND_STATE_HASH v2 prototype.
External places that may use them: the final-v2-pack configs and make_jobs.py output; the ADR-090 '47 config pairs' harness; export commit 0eb9a6ca (absent from this repo); and replay_gate_g1/g2.py, replay_runner_big.py, complete_oom_job.py, finalize_job.py, evidence_reader.py, run_queue.py and run_cloud.ps1.
These verdicts are conditional and should not be reported as established.

**Where:** runner verdict 'WP4 keys and values used by no recorded run'; domain verdicts on freeze horizon and two-sided deadline; algorithms verdicts on origin-hold, B4, dwell and the G2 rider cap; adapter-runtime verdict 'Runner restart/restore path'; adapter-scripts WP14R verdicts; E:/RideBoundData/final-v2-pack; E:/Code/Report_INT3508/ridebound-scratchpad/fastrun-2026-11-02

**Suggested check:** On the machine with E:, run these checks and attach the results before relabelling anything REMOVE_OK:
1. List ridebound-final-v2.zip, then grep every *.json in the pack and under fastrun-2026-11-02 for freezeHorizon, dropEtaDeadlineTwoSided, fixedFreeze, repair, multiplePlan, origin-hold, stopServiceDurationMs, maximumRidersPerVehicle, solverExecutionEvidenceProfile, stopDistances entries and policyId values.
2. Grep the *.py and *.ps1 files there for `ridebound_fleetpy`, `restore(`, `restart`, `_repository_inventory_sha256`, `wp14r_` and `RIDEBOUND_STATE_HASH`.
3. Obtain the ADR-090 harness config list.

## Critic: contradictions

- Scope says thesis numbers are emulator-only ('Not thesis numbers ... C# Runner/adapter outputs'). ADR-088 (docs/18:5832-5850: W04 component table, W05 '6 of 12 Via-size runs', W28 bridge numbers), docs/18:6028 and the domain verdict on customerVisible (T3.8 V-30 in Tables 4.3/4.5/4.6) show the thesis cites C# Runner results.
- The runner verdict 'Capability downgrade branch' keeps Contracts HelloMessages.cs:25,341-482, the downgraded schema and fixture valid-hello-ack-downgraded.json. The contracts verdict removes CapabilitySelectionStatus.Downgraded, downgradePolicyId and that fixture.
- tools/RideBound.CandidateHotPathBenchmark: algorithms says KEEP, benchmarking says REMOVE_OK. Application says its recorded JSON hashes constrain the OnlineState constructor change.
- Retained-portfolio evidence: runner says KEEP_REQUIRED because WP13 E1/WP14 'can no longer be re-run from HEAD'. Contracts and adapter-scripts show these already reproduce only at 38d517d (E1) and 41a4e11..8d1ea4d (WP14).
- research-configs keeps all of tests/RideBound.Benchmarking.Tests, benchmarks/fixtures/wp6 (e2e/metrics/planning) and benchmarks/schemas/wp6/v1 run-side schemas. The benchmarking area archives them with the WP6 stack.
- The contracts verdict (offerProposed/commitmentBreachDeclared) requires keeping BreachCount in tools/RideBound.Wp6MetricOracle. The benchmarking area archives the whole Wp6MetricOracle tool.
- Algorithms first labels the hard-utilization/revision vectors 'historical-only'. Its own verdict then shows they run on every final candidate (hardLimit 0 switch dimensions) and carry four live fail-closed checks.
- The Wp4Microbenchmark verdicts differ on whether it still compiles: algorithms says 'should still compile', benchmarking says 'unknown'. The algorithms component also called it 'orphaned', which its own verdict refutes (scripts/run-wp4-microbenchmark.ps1 calls it).

## Critic: overall

The per-area inventories are thorough on the C# layers and the adapter, but the audit has three structural weaknesses.

1. **Where thesis numbers come from.** The audit assumes the emulator is the only source. The repo shows the submitted thesis also cites C# Runner results: the T3.8 stability-first and customerVisible rows, the bridge numbers, and the 6 of 12 Via runs. The thesis's W04 'in RideBound' component table makes some REMOVE_OK verdicts potentially thesis-visible.
2. **Gate coverage.** G1/G2 is used as the safety net for nearly every SIMPLIFY or REMOVE. In fact all 228 G1 jobs are cost-first Manhattan full-v1 jobs. Stability-first, light-v1 bytes, Shadow, forecast, complete-graph and gzip paths are unguarded at HEAD.
3. **Emulator.** The emulator that produced the thesis numbers was never audited as code. It has 6 byte-identical copies of engine17.js, 18 arm codes and an external adapter-mode engine.

Further gaps:
- simulators/ridepy-ridebound and the build/toolchain pins are uncovered. Dependabot can bump Google.OrTools, which every config hard-binds, and no lock files are committed.
- The kept FleetPy normalizer's 'frozen' hash leaves out Contracts code it calls: EventBatchPayloadCodec.Encode, CanonicalJson and BenchmarkContractCodec.
- Several cross-area verdicts contradict each other (capability downgrade, CandidateHotPathBenchmark, retained-portfolio, WP6 tests and fixtures, Wp6MetricOracle).
- Many REMOVE_OK verdicts rest only on in-repo greps, while the final configs and gate tools that strict unknown-key parsing would break are external.

No file was changed; the repository was only read.

## Gap-fill findings


### gap:6
**Verdict: the risk is real but smaller than the gap states, and the mechanism is different. The bigger problem is missing records of how the final Runners were built.**

A Google.OrTools bump would not make configs fail to load. It would leave them loading under the wrong label. A test already fails on such a PR. No Dependabot commit has ever been merged. Dependabot cannot reach the final-v2 pack.

A HEAD publish done today, outside the repo with a different SDK, gives the same dependency files as all ten Runner builds whose file hashes are recorded in the repo. What is missing in the repo is any build record for `runner-fastrun-final1`, `runner-gd4-v1` and the final-v2 Runner.

**Findings, with evidence**

1. **A version bump would not make configs fail to load.**
   - `AdapterVersion` is a hard-coded constant, `"google-ortools-9.15.6755"` (`src/RideBound.Solvers.OrTools/OrToolsCandidateSelectionSolver.cs:11`). It is not read from the package.
   - The config check (`Wp4RunnerConfiguration.cs:769-774`) only compares strings. Nothing checks the version of the OR-Tools assembly or native library actually loaded: a git grep for Assembly/version use in `src/RideBound.Solvers.OrTools` and `src/RideBound.Runner` finds only `AssemblyReference.cs`.
   - So bumping only `RideBound.Solvers.OrTools.csproj:9` would still accept every recorded and final config. The new CP-SAT would then run, and the solver evidence (`OrToolsCandidateSelectionSolver.cs:317-322`) would still say 9.15.6755.
   - That matters for G1. CP-SAT runs single-threaded with a seed and conflict/deterministic-time limits (`:593-604`), so its results are only reproducible with the same binary.

2. **One guard already exists.** `tests/RideBound.ArchitectureTests/DependencyRuleTests.cs:137-169` asserts the version `"9.15.6755"` (`:168`). `ci.yml:5-6` runs on every pull request, so a Dependabot PR would go red. Branch protection is only "nên" (should) in `docs/15-testing-reproducibility-and-quality-gates.md:216`; whether it is enforced is unknown.

3. **What Dependabot actually does in this repo.**
   - `.github/dependabot.yml:3-16` has no `ignore` entry.
   - Dependabot PRs target `origin/main` only (`origin/HEAD -> origin/main`), not the research branches.
   - No Dependabot commit is on `main` or HEAD (`git log | grep -c dependabot` gives 0 for both).
   - The only Dependabot NuGet branch, `80e43fc` (2026-10-18), bumps test packages only (coverlet, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio). There is no Google.OrTools branch.
   - The final-v2 pack ships an already-built Runner plus .NET 10.0.9 (docs/18:6030, 6040-6043). A dependency bump on `main` cannot change it.

4. **Unpinned indirect packages: there are no lock files, but there is no drift.**
   - `git ls-files | grep -c packages.lock.json` gives 0.
   - The nuspec of Google.OrTools 9.15.6755 lists the five `runtime.*` packages at 9.15.6755 and Google.Protobuf at 3.33.1, all as minimum versions. NuGet resolves minimum versions to the lowest match, so the result stays stable.
   - Ten Runner file-hash manifests in the repo (`runner-tier3-v1/v2/v3`, `runner-g2-v1/v2/v3`, `runner-p1-dwell-v1`, `runner-via-a0/elmi/ref-v1`; 429 files each) have identical hashes for the dependency files:

     | File | SHA-256 (prefix) |
     |---|---|
     | `RideBound.Runner.deps.json` | `a5b21386…` (87,834 B) |
     | `RideBound.Runner.runtimeconfig.json` | `c230a317…` (342 B) |
     | `Google.OrTools.dll` | `a0e7608f…` |
     | `Google.Protobuf.dll` | `5d7a997f…` |
     | `runtimes/win-x64/native/ortools.dll` | `6e1efa4b…` |

5. **I ran a HEAD publish outside the repo (check 3 of the suggested list).**
   - I exported the sources with `git archive HEAD` and ran `dotnet publish -c Release` using SDK 10.0.400 (`global.json` relaxed in the copy only). The result is in `<scratchpad>/headpub/`.
   - Compared with `runner-p1-dwell-v1.sha256.tsv`: the same 429 file paths. All 414 non-RideBound files are byte-identical, including `deps.json`, `runtimeconfig.json`, all OR-Tools/Protobuf files and every native file. Only the 15 `RideBound.*` files differ, as expected after code changes.
   - So the SDK feature band (10.0.3xx vs 10.0.400) does not change the dependency set. The full file list with hashes is `<scratchpad>/headpub/head.tsv`.
   - `runner-fastrun-final1` and the final-v2 Runner could not be compared. `E:/RideBoundData` does not exist on this machine, and no manifest for them is in the repo. Their build details are unknown.

6. **There is no in-repo build record for the final Runners.**
   - git grep finds `runner-fastrun-final1`, `runner-fastrun-g1` and `runner-gd4-v1` only as prose in docs/18:5922, 5924, 5979, 6050 and docs/19:31. There is no `*.sha256.tsv` and no build record giving SDK, flags or file-tree hash.
   - The only toolchain fact for the final Runner is "Runner publish từ chính repo đó, .NET 10.0.9 đi kèm" (docs/18:6041, the final-v1 pack).
   - By contrast, the earlier `runner-tier3-v3` build record (`kits/tang3/stage1b/harness/RUNNER-PROVENANCE-v3.md:28-33`) records SDK 10.0.301, runtime 10.0.9, a framework-dependent build and the exact `dotnet publish … -c Release -o …` command, without `ContinuousIntegrationBuild`.

7. **Toolchain differences the gap did not mention.**
   - **The SDK pin is too strict in practice, not too loose.** The company machine has only SDK 10.0.400 and runtime 10.0.11. In the repo, `dotnet --version` fails ("A compatible .NET SDK was not found"). The ADR-090 evidence was run with the pin bypassed (docs/18:6096).
   - **The runtime moves with the machine.** `runtimeconfig.json` asks for `"version": "10.0.0"`, so the Runner runs on the newest installed 10.0.x: 10.0.11 here, 10.0.9 at home. The ADR-090 byte comparisons ran both sides on the same runtime, so they do not show that results are the same across runtimes.
   - **The Runner's file hash depends on the build folder.** The published `RideBound.Runner.dll` contains the absolute PDB path (I grep'd the bytes and found the scratch path). The recorded builds did not use `ContinuousIntegrationBuild`/PathMap. For live runs, `binarySha256` is the SHA-256 of `RideBound.Runner.dll` (`simulators/fleetpy-ridebound/ridebound_fleetpy/session.py:559`) and goes into `manifestHash`.
     - Live-run hash chains can therefore only be reproduced with the archived Runner binary. Rebuilding from source does not reproduce them.
     - Replay gates G1/G2 are not affected, because they feed in the recorded `initializeRun`.
   - **The adapter's before/after drift receipts miss the native libraries.** They hash only the top-level files of the Runner folder (`session.py:370`), so the native OR-Tools libraries under `runtimes/` are not covered. The kit drivers do verify the whole tree (`run_via.py:235`, `runner_manifest.py verify`).

**Classification changes**

| Item | Classification and note |
|---|---|
| `RideBound.Solvers.OrTools.csproj:9` pin, `AdapterVersion` constant, `Wp4RunnerConfiguration.cs:769-774` | Stays (c) and (b). The check is a label check only, not a binary check. |
| `DependencyRuleTests.cs:137-169` (OR-Tools pin test) | Change from "architecture test, could be trimmed" to (c). It is the only active guard against a bump. Keep it. |
| `global.json` 10.0.301 | (c) and (b). The WP13/WP14 freeze tools require exactly SDK 10.0.301 and runtime 10.0.9 (`benchmarks/schemas/wp13/v1/exploratory-replay-freeze.schema.json:86-87`, `wp14/v1` and `v2` `:203`, `simulators/fleetpy-ridebound/tests/test_wp13_e1_freeze.py:159-160`). Keep the pin. Write down the 10.0.400 bypass instead of loosening it. |
| Runner manifests `kits/**/runner-*.sha256.tsv` | (b). Needed to verify earlier runs. |
| `ci.yml:105-127` `package-runner` job | Neither (b) nor (c). No recorded Runner came from CI; research Runners were published by hand without the CI flag. |
| `dependabot.yml` | Not code. A hazard. Add an `ignore` for `Google.OrTools`, `Google.OrTools.runtime.*` and `Google.Protobuf`. |

**Remaining work, prioritised**

1. Add the Dependabot `ignore` entries above.
2. Optionally, make the Runner fail at startup if the loaded OR-Tools version does not equal `AdapterVersion`. With the pinned version this changes no output, and it turns a silent mislabel into a clear failure.
3. Lock files: optional extra hardening. They would add content-hash checks, but no drift was observed.
4. In docs/18, write build records in the `RUNNER-PROVENANCE-v3` format for `runner-fastrun-final1`, `runner-gd4-v1` and the final-v2 Runner: SDK, runtime, publish command and flags, file-tree SHA-256, and the `deps.json`/`runtimeconfig.json` hashes. This is the real provenance gap; the data must come from `E:/RideBoundData/final-v2-pack/MANIFEST.tsv` on the home machine. Whether `run_cloud.ps1` uses the bundled .NET 10.0.9 or the machine-wide `dotnet` is unknown from the repo.

### gap:5
**RidePy (WP10) gap: audit result**

Put all of `simulators/ridepy-ridebound/**` in the inventory as (b), WP10 evidence (20 tracked files, 3,125 .py lines), and keep it in the tree for now. Three of the area's assumptions don't hold:

- The Runner's `nodeOnly` support is (c), not something RidePy owns.
- Only two `RunnerClient` constructor parameters are used for RidePy alone.
- The RidePy test suite as it stands does not guard against changes to `ridebound_fleetpy`.

**1. What RidePy imports from `ridebound_fleetpy`**

`git grep -n ridebound_fleetpy -- simulators/ridepy-ridebound` gives 12 hits, all in source files. No .pyc matches this pattern; the earlier binary match was for `ridebound_ridepy`.

| Importer | Module | Symbols |
|---|---|---|
| `ridebound_ridepy/session.py:8-10` | mapping, runner_client, transcript | `canonical_json_bytes`, `wp4_policy_binding_hash`, `RunnerClient`, `ProtocolTranscriptRecorder` |
| `ridebound_ridepy/mapping.py:7-13` | errors, mapping | `AdapterFailure`, `MAX_CANONICAL_INTEGER`, `CanonicalIdRegistry`, `canonical_json_bytes`, `seconds_to_milliseconds` |
| `ridebound_ridepy/fleet_state.py:11-18` | errors, protocol | `AdapterFailure`, `OrderedEventBuffer`, `ParsedDecision`, `ProtocolRoute`, `ProtocolStop`, `parse_decision` |
| `tests/test_fleet_state.py:17-24` | errors, protocol | adds `AcceptedAction`, `PlanAction` |
| `tests/test_mapping.py:14` | errors | `AdapterFailure` |
| `wp10_canonical_run.py:14`, `wp10_subset_execute.py:14` | mapping | `canonical_json_bytes`, `seconds_to_milliseconds` |

Every one of these symbols is also used by the final FleetPy path. RidePy therefore constrains their names and signatures, not whether they exist. It also depends on things no import line shows:

- **`RunnerClient` arguments** (`session.py:61-76`): `maximum_input_line_bytes`, `maximum_output_line_bytes`, `expected_position_model="nodeOnly"`, `required_capabilities`, `transcript_writer`, `timeout_seconds`.
- **`RunnerClient` methods** (`session.py:87-198`): `start`, `negotiate`, `initialize`, `decide`, `acknowledge`, `request_checkpoint`, `shutdown`, `state`, `artifact_receipts`. It never calls `restore`.
- **`parse_decision`** (`protocol.py:246,248,381,385`) expects the mapper to provide `raw_request`, `raw_vehicle` and `raw_node`. RidePy's mapper does (`ridepy/mapping.py:49-56`).
- **`ProtocolRoute`** is built with positional arguments (`fleet_state.py:61,460-472` against `protocol.py:51-55`), so its field order matters.
- **`OrderedEventBuffer`** is called as `append(dedupe_key=)` and `drain(prefix=)` (`fleet_state.py:93,271-276`).
- **Error codes:** RidePy rewrites the `RBWP7_` prefix to `RBWP10_` (`ridepy/mapping.py:28-32`).
- **Package `__init__`:** importing any submodule runs `ridebound_fleetpy/__init__.py`, which imports only stdlib-only modules. If `fleet_control` or `direct_network` (they import FleetPy `src.*`) were ever added there, RidePy would fail to import in its container.
- **Runner command-line flags** (`wp10_subset_execute.py:127-139`): `--mode commitment`, `--policy-config`, `--wp4-config`, `--solver-seed-source manifest-master-seed`, `--maximum-line-bytes 67108864`. All are still accepted (`Program.cs:14-61`).

**2. Effect of the adapter-runtime proposals on RidePy**

I tested each proposal on a `git archive` copy in the scratchpad, not in the repository. The probe builds a `RideBoundRidePySession` without starting a process, then the RidePy suite runs.

| Proposal | Effect on RidePy | RidePy suite |
|---|---|---|
| Trim `__init__` re-exports | Safe (session still builds) | OK, 5 skipped |
| Drop the combined `maximum_line_bytes` | Safe (RidePy passes the split arguments) | OK, 5 skipped |
| Archive `restore` | Safe (RidePy never calls it) | OK, 5 skipped |
| Merge the split line bounds into one argument | **Breaks it:** `TypeError ... 'maximum_input_line_bytes'` | **OK, 5 skipped** |
| Hard-code or remove `expected_position_model` | **Breaks it:** `TypeError` | **OK, 5 skipped** |

The suite stays green even when the session can no longer be built. The reasons:

- No test constructs `RideBoundRidePySession`; the only constructions are in `wp10_canonical_run.py:144` and `wp10_subset_execute.py:158`.
- Outside the pinned image, all five `test_fleet_state` tests are skipped (`test_fleet_state.py:31`). Those are the only tests that exercise `protocol`.
- My local run was 23 tests: 18 passed, 5 skipped.

**3. Classification changes**

- **Scripts that re-check the recorded numbers (b):** `wp10_subset_analyze.py`, `wp10_bundle_verify.py`, `wp10_source_verify.py` and their tests use only the standard library. They can re-check the recorded 11 pairs (B1 54/62, C1 49/62) from HEAD without the adapter runtime.
- **Code that produced the evidence (b):** `ridebound_ridepy/*`, `wp10_canonical_run.py`, `wp10_subset_execute.py`, `wp10_subset_freeze.py`, `Dockerfile`.
  - HEAD cannot re-run WP10 exactly. `ridebound_fleetpy` changed after `2d6791f` (in `c6588f8`, `41a4e11`, `9ba5568`, `636fe2d`, `ad470c1`). The WP10 receipts also fix Runner publish tree `be2b9b14…`.
  - None of the WP10 receipts hash `ridebound_fleetpy` files:
    - The freeze hashes only `*.py`, `ridebound_ridepy/*.py` and the Dockerfile (`wp10_subset_freeze.py:41-45`).
    - The analyzer pins five adapter paths (`wp10_subset_analyze.py:85-91`).
    - The runtime is tied only through the freeze's `coreCommit` plus `dirtyTrackedDiffSha256` (`wp10_subset_freeze.py:73-74`). That commit is external and unknown.
- **Configs (b):** research-configs must keep all four configs, not only `wp10-ridepy-paired-subset-v1.json`. `wp10_subset_analyze.py:20-26` pins `wp10-ridepy-paired-subset-v1`, `wp8-drop-eta-budget-tight-v1`, `wp9-fleetpy-rolling-cost-audited-v1` and `wp9-fleetpy-ridebound-hard-vector-audited-v1`. All four HEAD blobs match those hashes.
- **`RunnerClient` `expected_position_model` / `required_capabilities` (`runner_client.py:53-58,81-92,167-175`) are (b).** They were added for RidePy by ticket RB-WP10-003 (`docs/tasks/40:59`, commit `2d6791f`). The final FleetPy session uses the defaults and does not pass them (`fleetpy session.py:460-471`); the defaults equal the old hard-coded values. Other users: `test_runner_client.py:75-93`.
- **The Runner's `nodeOnly` support is (c); do not archive it with RidePy.**
  - Every adapter must meet `RequiredPositionModel: "nodeOnly"` (`RunnerHost.cs:11-21`). FleetPy's `directedEdgeProgress` meets it through the superset rule (`CapabilityNegotiator.cs:254-258`).
  - Removing `PositionModel.NodeOnly` would make the requirement profile invalid for all negotiations (`CapabilityNegotiator.cs:151-159`).
  - The contracts area should change "nodeOnly tied to RidePy" to "only RidePy offers it; the Runner requires it from everyone".
- **Not used by the final workflow:** `git grep -i "ridepy|wp10|nodeOnly"` finds nothing in `research/`. `docs/18` lines 5000-6200 (ADR-076..090) don't mention RidePy. CI runs only dotnet (`.github/workflows/ci.yml`).
- **Wrinkle:** final runs record a hash of every file in the repository.
  - The preflight computes it (`actual_fleetpy_medium_preflight.py:771-781,867-904,965`), and `run_via.py:82,197,247-249` checks it against `ADAPTER-INVENTORY.txt`, taken at commit `6ab03fc`.
  - That commit contains all 20 RidePy files, the .pyc files included.
  - Deleting them later changes no recorded result, but the expected hash must be recomputed before the 168-job campaign, and never while jobs are running.
- **The 4 tracked .pyc files are stray build output, not code in any class.**
  - The ignore rule `.gitignore:31-35` was added in `41a4e11`, after they were committed in `2d6791f`.
  - No WP10 receipt includes them.
  - Two are stale: their headers record source sizes of 7020 and 24122 bytes, while the sources are 8651 and 24201 bytes (`wp10_source_verify`, `wp10_subset_analyze`).
  - They can be untracked between campaigns, with the hash recomputed afterwards.

**4. Archive or keep**

Keep RidePy in the tree as frozen evidence.

- Its runtime code is small (917 lines).
- The freeze-pinned files (`ridebound_ridepy/*.py`, `wp10_subset_execute.py`) are unchanged since `2d6791f`, so the tree holds a readable copy of the code that ran.
- It requires nothing from the final runtime beyond the two constructor parameters.

If it is archived later:
- point to `2d6791f` and the external freeze v3 (`18a74fa3…`), plus the image tar at `E:\RideBoundData\wp10`;
- only then collapse the two `RunnerClient` parameters;
- recompute the repository hash after either step.

**5. Required check for `ridebound_fleetpy` changes**

The RidePy suite by itself is not enough (section 2). For any change to `__init__`, `errors`, `mapping`, `protocol`, `runner_client` or `transcript`:

1. **Without the RidePy wheel:** run the RidePy tests and add a smoke check that imports `ridebound_ridepy.session` and builds `RideBoundRidePySession` without starting a process. This would have caught both breaking proposals.
2. **For `protocol` changes:** run the full 23/23 suite inside the pinned image `ridebound/ridepy-wp10:2.10.1`, as `docs/tasks/40:134-137` already requires.

CI has no Python job, so this is a manual check today.

**Not settled from the repository:**
- The freeze's `coreCommit` and whether the frozen file hashes equal HEAD: the receipts are external.
- I could not run `test_runner_client.py` here. The only interpreter on the machine is LibreOffice's Python 3.13, and the suite fails with 19 `RBWP7_RUNNER_ARTIFACT_MISSING` errors, which come from the environment, not the code.
- `docs/19:731` says the RidePy suite is "20/20", while `docs/18:1642` and `docs/19:751` say 23/23. Today's 23 = 8 source-verifier + 7 mapping + 5 fleet_state + 3 analyzer tests.

Probe scripts:
- `<scratchpad>\probe\probe.py`
- `<scratchpad>\probe\mutate.py`

### gap:1
The gap is real. G1 and G2 are necessary for every SIMPLIFY or REMOVE item they guard, but they are sufficient for none of the five verdicts named in the task. All paths below are relative to `<repo>`.

## 1. What G1 and G2 actually exercise

**Job set.** The G1 and G2 scripts are not in the repository. `docs/18-status-and-decision-log.md:5920` points to the external `ridebound-scratchpad/fastrun-2026-11-02/replay_gate_g1.py`, and `git ls-files | grep replay_gate` finds only `replay_gate_dwell.py`.
- G1 reports the same totals as P9: 228 jobs and 176,074 frames (`docs/18:5924-5925`, `kits/bridge-phase1-2026-10-23/PHASE1-LOG.md:377-378`). So I take the job set to be the one generated in `kits/bridge-phase1-2026-10-23/replay_gate_dwell.py:43-70`.
- `DWELLOFF-GATE-SUMMARY.json` splits the 228 jobs as ins 80, g2 33, p1 76 and r128 39.
- The arms are U, C-120, C-300, DLHZ-120, PS-300, RCE35, EOHZ35, EO35, DLS-120, plus G2, G2K3 and G2K8 variants and one REG-CF-U job.

**Configurations.** A `git grep` for WP4 file names over `a0_gate.py`, `run_bridge.py`, `run_g2.py`, `replay_gate_dwell.py`, `run_p1.py` and `run_r128.py` finds only six files: `wp4-tier3-v2-costfirst`, `wp4-g2-costfirst`, `wp4-costfirst-forced`, `wp4-g2-costfirst-forced`, `wp4-g2-costfirst-k3` and `wp4-g2-costfirst-k8`. They are mapped at `a0_gate.py:24`, `replay_gate_dwell.py:58`, `run_p1.py:125-130` and `run_r128.py:27,44-45`.

Every one of the six files has:
- `"selectionObjectiveOrder": "cost-first"` (line 9 of each);
- `emitSolverExecutionEvidence: true`;
- policyId `ridebound-hard-vector` (C1);
- no `auditHash`, `planningForecast` or `shadowAllocation` key.

**How the replay works.**
- `replay_runner.py:3-6` writes the recorded adapter-to-Runner frames into the Runner, so the adapter is never run.
- `replay_runner.py:46-47` fixes `--maximum-line-bytes` at 64 MiB.
- The jobs read plain `transcript-00.ndjson` files (`replay_gate_dwell.py:84`). They were recorded in October, so they are full-v1 on Manhattan with the network-basic engine.
- G2 replays the same 228 jobs under light-v1 with solver evidence on, and strips every `*Hash` field before comparing (`docs/18:5926-5927`).

## 2. Blind spots and which recorded results depend on them

| Path | Code | Who uses it | G1/G2 |
|---|---|---|---|
| Stability-first ranking | Default when the key is absent (`Wp4RunnerConfiguration.cs:489-496`). The stability-first-only levels are at `SolverBackedFleetSelection.cs:349-362`. Every tang3 and bridge arm, including U, has 3 `hardLimit` entries, so `hardTreatmentActive` (`:298-300`) is always true. | T3.8 (`docs/benchmarking/tier3-t38/T38-PLAN.md:65`: runner-tier3-v2 with `wp4-tier3-v2.json`, which has no order key). The Stage 1b SF comparators and Gate 0 (`STAGE1B-PLAN.md:72,77-80`). WP9 H6 C1 (`wp9-fleetpy-ridebound-hard-vector-audited-v1.json`, no key). Every in-repo C1 template. | Never runs |
| C2 | `SolverBackedFleetSelection.cs:364-393` and `SolverBackedRidePoolingPolicy.cs:355` | WP9 C2 ablation (`wp9-confirmatory-result-2026-08-23.md:63`) | Never runs |
| B1 rolling-cost | RollingCost profile; legacy `RollingCostPolicy` (`RunnerSession.cs:132,1188`) | WP9 H6 reference arm (`wp9-confirmatory-result:5,60`) | Never runs |
| B2, B3, B4 | `SolverBackedRidePoolingPolicy.cs:324-353`; `SolverBackedFleetSelection.cs:335` | `git grep` of the policy ids over docs, benchmarks, research and simulators finds no recorded config, only the spec (`docs/08:24-26`) | Never runs |
| light-v1 hash bytes | `ProtocolHash.cs:116-131`; `RunnerSession.cs:412-416,559-563,615-621,877` | The 168-job campaign and every Via fast-mode job | G1 is full-v1. G2 strips the hashes. |
| Solver evidence off | `RunnerSession.cs:1352` | Final large runs (`docs/18:5902`) | G1 and G2 both run with evidence on |
| Shadow and forecast in a Runner session | `RunnerSession.cs:146-148,858,871-874,976-992,1270-1280`; `OnlineStateCanonicalWriter.cs:145-148`; `TravelTimeSnapshot.cs:44-50`; `CommitmentDecisionValidator.cs:406` | SHF, SHHZ and SHRC arms, CL arms | Never runs |
| Lines over 64 MiB; 0.4–0.8 GB checkpoints | `NdjsonReader.cs:34`; the streamed checkpoint | Via full-node cells | Never runs |
| complete-graph-direct and gzip | Adapter only: `actual_fleetpy_medium_preflight.py:451-462,525-527` | Via full-node cells | The adapter is bypassed by construction |

**What the existing tests cover.**
- No test drives a RunnerSession with Shadow or forecast on. Runner tests touch these features only in config decoding (`ShadowAllocationConfigurationTests.cs`, `Wp4RunnerConfigurationTests.cs:905-935`) and the checkpoint codec (`OnlineStateCheckpointCodecTests.cs:435-465`).
- `git grep "\.Allocate(" tests` returns 0 hits, so `ShadowAllocator.Allocate` (`ShadowAllocator.cs:66`) is never tested.
- The Shadow gate has 3 passed jobs and 5 pending (`docs/18:6019-6022`). The forecast gate has 2 passed jobs plus 1 from the pack (`docs/18:6012-6014`). Both compare per rider against the emulator predictions, not byte for byte.
- No light-v1 hash value is fixed anywhere. `Wp4RunnerIntegrationTests.cs:943` only checks `Assert.NotEqual` against the full-v1 hash. No `checkpointHash` literal exists in `CheckpointMessageTests` or `CheckpointStreamingHashTests`.
- The fixed decision vectors in `ProtocolHashTests.cs:23-83` (`9939c29a…`, `30f1408d…`, `bef7c661…`) test only the span overload (`ProtocolHash.cs:31-65`). `git grep` finds no production caller of that overload; its only callers are in `ProtocolHashTests.cs:33,55,61,67`. Production uses the parts overloads (`:72-131`, called at `RunnerSession.cs:616,623,629`).

## 3. Item by item

**1. Skip the hard-utilization vectors under cost-first.**
- Touches `HardVectorCandidateAssessor.cs:196-245,671,808,871-970`, the hard-treatment flag at `SolverBackedFleetSelection.cs:298-300`, and Map at `:487-512`.
- G1 is a valid guard for the cost-first side. `git grep` of `WorstHardUtilization` in the Runner finds nothing, so the value never reaches a frame.
- G1 cannot detect the skip leaking into stability-first (the default) or into C2.
- It also cannot detect the loss of the fail-closed witnesses at `:907-914` and `:946-956`, which never fired in the 228 passing jobs.
- It contradicts `SolverBackedFleetSelectionTests.cs:590-592`, which asserts that a cost-first selection still reports utilization 800,000 and its revision vector.
- **Guards needed:** G1 + N1 + N5 (C2), plus a deliberate change to that test.

**2. Rename the RollingCost models.**
- Touches `RollingCostDecisionModels.cs:27-340`, `RollingCostPolicy.cs`, the profile name at `SolverBackedFleetSelection.cs:12`, and the `"rolling-cost"` id at `SolverBackedRidePoolingPolicy.cs:27`.
- If only C# identifiers change, a build plus G1 is enough for C1.
- Any change to a string literal is not covered for B1: the reason and failure codes at `:323-339` and the policyId, which feeds `policyConfigurationHash`.
- **Guards needed:** G1, a pin of the emitted strings, and N5 (B1).

**3. Trim the SolverBackedFleetSelection and orchestrator branches.**
- The switch at `:333-394` and the dispatch at `SolverBackedRidePoolingPolicy.cs:322-375`.
- G1 runs only the forced-reference, accepted-count, operational-cost, relocation-count (`:409`) and rank levels.
- The unit tests at `SolverBackedFleetSelectionTests.cs:521-545` pin the stability-first level list only, not end-to-end bytes.
- **Guards needed:** N1 + N5 + G1.

**4. light-v1 single-build checkpoint.**
- I read this as dropping the second, uncached rebuild at `RunnerSession.cs:761-773`. Under light-v1 there is no cached committed state (`:412-416`), so the checkpoint builds the 0.4–0.8 GB state twice.
- G1 never takes this branch: under full-v1 the committed state is always cached.
- G2 checks the checkpoint content minus the hashes, only on Manhattan-sized states, with no Shadow allocations.
- The only related test (`CommitmentCheckpointProcessTests.cs:62-95`) is a full-v1, non-WP4 comparison between two code paths.
- **Guards needed:** N2 + N3 + N4, with G2.

**5. ProtocolHash span-overload wrapper.**
- If the span overload becomes a wrapper over the parts path, the published vectors start testing production code, which is an improvement.
- G1 guards only the v1 domain. The light domain (`:130-131`) has no guard.
- Removing the overload without moving its vectors would delete the only fixed decision-hash vectors.
- **Guards needed:** N2 first, then G1.

## 4. Which gate guards which item

| Gate | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|
| G1 (cost-first, full-v1) | Cost-first side | C1 identifiers | Cost-first levels | — | v1 domain |
| G2 (light-v1, hashes stripped) | — | — | — | Content only | — |
| G3a, G3b, Shadow and forecast gates (per rider) | — | — | — | Partly (semantic) | — |
| N1: stability-first byte replay | Yes | — | Yes | — | — |
| N2: fixed light-v1 vectors | — | — | — | Yes | Yes |
| N3: Shadow and forecast session golden | — | — | — | Yes | — |
| N4: complete-graph-direct and gzip replay | — | — | — | Yes | — |
| N5: B1 and C2 replay (added by me) | C2 | B1 | B1, C2 | — | — |

## 5. Guards to add before any refactor

- **N1.** Byte-replay the four Gate-0 stability-first reference transcripts through `replay_runner.py` with `wp4-tier3-v2.json` and tang3 `U.json` / `C-30.json`. They are U and C-30 on `d20181116-s10-r1`, w08 and w17, with 2020, 2284, 2035 and 2146 frames (`STAGE1B-PLAN.md:160-167`). Add capped T3.8 arms if possible.
  - Gate 0 was behavioural only (`STAGE1B-PLAN.md:150-160`), so it is **unknown** whether HEAD reproduces these bytes.
  - Run the replay on HEAD first. If it is not identical, T3.8 reproduces only through the archived runner-tier3-v2 build, and the claim at `docs/18:6118` ("no byte changes in any recorded config") must be narrowed.
  - The config binding is already pinned: `Wp4RunnerConfigurationTests.cs:697,705-710` (`fb2813c4…`, `251b331e…`, `a2f11a3a…`).
- **N2.** Commit fixed literal vectors for:
  - `CalculateDecisionHashLight`, on the same inputs as `ProtocolHashTests.cs:23-43`;
  - the parts overload equalling the published vectors;
  - the session-level light decision hash and `checkpointHash` in the scenario at `Wp4RunnerIntegrationTests.cs:897-966`.
- **N3.** Add a RunnerSession golden test with `shadowAllocation` and `planningForecast` on, in both full-v1 and light-v1, with solver evidence off, covering decisions, checkpoint and restore.
- **N4.** Replay one complete-graph-direct, gzip job: a G3a 60-node job and one 824-node G3b job, frame by frame including hashes, with the line limit raised up to 1 GiB.
  - Whether those transcripts still exist is **unknown**: `finalize_job.py` may have dropped them (`docs/18:6046-6049`).
  - If they are gone, rerun the full system and compare against the per-frame receipts.
- **N5.** Replay or golden-test B1 and C2: WP9 H6 audited transcripts if HEAD reproduces them (**unknown**), otherwise an in-repo golden from `wp4-rolling-cost-boundary-v1` and `wp9-fleetpy-soft-hard-hybrid-audited-v1`.

The 47-config, 564-turn harness cited in ADR-090 (`docs/18:6091-6093`) is not in the repository and its config list is **unknown**.

## 6. Classification changes

- **For all five items:** the condition "rerun G1/G2 (228/228)" changes from sufficient to necessary but not sufficient.
- **Stability-first branch and its utilization/revision computation:** (b), needed for T3.8, Stage 1b SF and WP9 H6. It is also the default for every in-repo C1 template. Not removable; simplify only after N1.
- **C2 and B1:** (b), needed for WP9.
- **B2, B3, B4:** (b)-candidate. They are reachable through config, so not (a), but I found no recorded result. The owner should decide.
- **Utilization computed under cost-first:** (c) code that the final workflow runs, but its value only matters for the fail-closed checks. Skipping it is a behaviour change, gated by G1 + N1 + N5 + the test change.
- **light-v1 checkpoint and the Shadow/forecast session code:** (c). The single-build change is blocked until N2, N3 and N4 exist.
- **ProtocolHash span overload:** a test-only verification asset. A wrapper is allowed after N2; removal is not allowed until its vectors are moved.
- **Adapter items (complete-graph-direct, gzip):** these can never cite G1 or G2. Their guards are the adapter test suite, G3a and N4.

The final 168-job results run on the packaged Runner (commit `0eb9a6ca`), so a HEAD refactor cannot change them. What it can break is HEAD's ability to regenerate them, and that is what N2, N3 and N4 protect.

### gap:2
## Emulator code audit: engine17.js and its copies, classified (a)/(b)/(c)

The blanket "KEEP, frozen" label should be replaced by two layers. The sealed copies stay frozen as an archive, which is (b). Inside them, engine17.js is about 85–90% (c), the rest is (b) robustness knobs or (a) branches that no in-repo script reaches.

The real gap is somewhere else. The thesis numbers (plain mode) can be rebuilt from the repo. The adapter-mode predictor behind the sealed C#-vs-emulator gates is not in the repo, and its sha256 values are not recorded anywhere in it.

`CE` below means `research/continuation/2026-10-26/company-emulator`. Everything here was read-only.

### 1. Copies and seals (step 3)

**The copies are identical and match their seals.**
- engine17.js has 6 copies, all git blob 927c9b4c, 842 lines.
- sha256 of each copy is `81d7b47e65c582f92ca30f9fd256c7d55ad0b507f33812e12a02f19c197f12e6`, equal to every seal:
  - `sp/wf17/r1-identity-fence-pad/CODE-HASH.txt:3`
  - `sp/wf17/r2-penalty-regime/CODE-HASH.txt:3`
  - `sp/wf17/r3-curves-nodes/CODE-HASH.txt:3`
  - `sp/wf20/am2017/PLAN-HASH.txt:5`
  - `sp/wf20/fullsys/PLAN-HASH.txt:10`
  - `sp/wf21/n7/PLAN-HASH.txt:28`
- The other shared files also match their seals:

| File | Copies | sha256 |
|---|---|---|
| identity.js | 6 | be261baa |
| normalizer_emul.js | 6 | dd47d9e3 |
| scenario.js | 6 | d27d0bee |
| sscore.js | 4 | b295b8a9 |
| forecast.js | 3 | bcd21f2d |
| lambda.js | — | cceb8e61 |
| metrics_sh.js | — | f1ee54cc |
| shcore.js / run_sh.js (n7) | — | ef6a2e74 / 548bd732 |

**The fullsys seal does not cover three files the main run loads.** `wf20/fullsys/PLAN-HASH.txt` lists no identity.js, normalizer_emul.js or lib.js. Both are still loaded: engine17.js loads identity.js (line 29), and scenario.js loads all three (lines 8–10). The identical identity.js and normalizer_emul.js bytes are sealed in r1 and n7. lib.js is in no seal that matches the in-repo bytes (next point).

**New finding: files that contain a path differ from their seals.** Every file holding the literal `E:/RideBoundData` has a different sha256 from its seal; every file without it matches. Affected:
- `runner/lib.js` (c98df7c6, sealed c761de69)
- `runner/netfull.js` (34764fb5, sealed e14c5756)
- `scripts/shlib.js` (190b3dde, sealed c61c518b)
- drive17.js, drive20.js, r3drive.js, gate_p2.js, gen17.js, make_cells.js
- all RUNBOOK.sh files

Commit 21af556 says "paths point to the home SP root". The only check of these in-repo bytes is behavioural, on F14 only: `home-rerun-2026-10-26/RESULT.md` reports 1,680/1,680 rows for fullsys and 1,680/1,680 for n7.

**Physical deduplication is not safe; logical deduplication is free.**
- Git already stores each blob once, so the six copies cost no storage.
- Each seal names its own `code/runner/...` path.
- Each RUNBOOK runs `cd "$K/code/scripts"` and loads `../runner/engine17.js`.
- `run_sh.js:188` hashes `__dirname/..` files into the run manifests.
- Moving the files would break `sha256sum -c` and the runbooks.
- Instead, treat the six as one module and document it ("one module, six sealed copies, sha 81d7b47e").
- `wf20/fullsys/code/RUNBOOK.sh` is a byte copy of r1's runbook (blob 09fa3a2c). The real one is `wf20/fullsys/RUNBOOK.sh`.

### 2. What each runbook actually passes (step 1)

All runs use `shlib.js:12-16`: INS = `{}`, G2 = `{reopt:{g2:99,g2Targets:8},lock:'ratchet'}`, 62 vehicles, ride limit 2000‰, dwell 0. The only drivers that bypass shlib are drive17/drive20 (`drive17.js:36-40`, `drive20.js:50-54`).

| Runbook | Driver | Arms | Forecast / options | Identity |
|---|---|---|---|---|
| wf17 r1 (`code/RUNBOOK.sh:9-21`) | run_sh | r2a/r2b: SHF, SHHZ, SHRC, EO35, EOHZ35, RCE35. r1plain/r1adapter: U, EO35, EOHZ35, RCE35, NCE35, SHF, SHHZ, SHRC | r2a/r2b: `--fence-tol 0/1000/5000/60000 --audit`; `--x star` | r1adapter/r1smoke run the adapter cells built by `make_cells.js:33`; the other steps are plain |
| wf17 r1, R3 (`:11`, via `r3drive.js:16-21,48-49`) | r3drive | N: U, EO35, EOHZ35, RCE35 (+NCE35 under G2). CL and CLF: EOHZ35, RCE35 | CL; CLF uses flatRatio from rw.json | plain |
| wf17 r2 (`:14-32`) | drive17 | R4: U, EO35, EOHZ35, RCE35, NCE35. R5: U, EOHZ35, RCE35, NCE35 (G2), EOHZ35, RCE35 (INS) | R4: g2Targets 8/16, g2PenaltyMs 0/60000/300000. R5: density 256, fleet 42/56/68, ride limit 1500/2500‰, dwell 24 s | plain |
| wf17 r3 (`:12-20`) | run_sh | R6: lists in `work/arms_*.txt` (U, EOHZ/RCE 0–100, SHHZ/SHRC 50–90 and x*). R7: U, EO35, EOHZ35, RCE35, NCE35, SH* | R6: `--ref`, alphaS 0.15, stride 0 | plain (gen17 cells, `gen17.js:108,112`) |
| wf20/fullsys (`RUNBOOK.sh:10,15-23`) | run_sh | SHF, SHHZ, SHRC (x* = 71 G2 / 72 INS), EOHZ35, RCE35 | `--fc N`, `--fc CL`; panels F14 and F16 | plain |
| wf20/am2017 (`code/RUNBOOK.sh:11-19`) | drive20 | U, EO35, EOHZ35, RCE35 | Fleet sizes F20–F81; gate variants GF56/GF68 | plain |
| wf21/n7 (`RUNBOOK.sh:12,25-54`) | run_sh | SHRC, SHHZ | `--fc N/CL/PAD/CLc`, `--flat`, `--fc-index` | plain |

The expected digests confirm these combinations (`expected/*.tsv`). x* = 72 (INS) / 71 (G2) is stated in `wf17/a0-plan/RUNPLAN.md:117`.

The main comparison uses fullsys rows: SHRC-CL against SHHZ-N, with SHF-N as a second reference (`wf20/ours-vs-theirs/ours_vs_theirs.js:2-4`).

### 3. engine17.js branches (step 2)

**(c) needed by the final workflow**
- Arms U, EO, EOHZ, RCE, SHF, SHHZ, SHRC:
  - admission rules, `engine17.js:73,81-83,86-88`;
  - per-rider budget B_r (`brOf`), `:43`;
  - parsing, `:55,59-62`.
- Forced vehicles for EO/SHF, `:474`.
- Lock modes `exact` (INS) and `ratchet` (G2), `:294-298`.
- Default pool cap: 100 for INS, unlimited for G2, `:129`. Candidate retention when the cap is hit, `:347-396`, `:519`.
- Waiting-rider relocation (G2) with 8 target vehicles, `:524-555`. Prefer-keep tie-break, `:571,647`.
- Candidate-id tie-break through identity.js, `:340-344,563-575,649-656`. This applies in plain mode too.
- Closed-loop forecast: `legF` and `clipPickup`, `:153-169,722,739`.
- Shareability score hook, `:743-777`.
- Physical motion, projection, publication ledger and event clock.

**(b) needed to reproduce recorded results**
- P1 fence tolerance and audit (`:30,77,81,86,122-123,475`), for R2 (r1 RUNBOOK `:12-18`).
- P2 relocation penalty `g2PenaltyMs` (`:615`), for R4 and gate G-P2 (`gate_p2.js:15`).
- P3 `flatRatio` (`:161`), for R3 CLF and n7 PAD (`shcore.js:168-171`).
- Arm NCE (`:84`), for R1, R3, R4, R5 and R7. These are 1,560 digest rows in r3/r4/r5, plus R1/R7 rows that have no digest.
- RC, PS, DLHZ, DLS, NC and OC have the same source lines in the external wf15 `engine.js`. There they produced the sealed bridge baselines:
  - `kits/bridge-phase1-2026-10-23/emulate_wf15_equiv.js:22-28`
  - `emulate_p1.js:21-24`
  - `kits/identity-2026-10-25/emulate_identity.js:45-50`
  - ADR-081 (`docs/18:5401-5404`) and ADR-082 (`:5476`).
  - So they are (b) through `engine.js`, not through engine17.

**(a) not reached by any in-repo runbook, kit or test**
- Through engine17, the arms RC, PS, DLHZ, DLS, NC, OC: `:74-79`, `:63-65`.
- The arms RR, FZ, OCE, SHNC: `:80,85,89`, `:56-58`, `:95`. A git grep over research/ and docs finds no use of them.
- Options g1 and dr (`:419-442`), `g2MaxSwitches` (`:527`; nothing passes it).
- Lock modes `ratchetFloor` and `none` (`:297-298`).
- `tieMode` 'index' (`:572,648`), `reoptTie` 'id' (`:130`), explicit `poolCap` override.
- `shadowArms`, `pairs` and the shadow-difference counters (`:106,118-121,664-687`).
- `trace` (`:694`).
- `physMat`/closure (`:148,178-182,215,227`; scenario.js `:150-169`). gen17 cells have no closure (`gen17.js:108`). For the wf14 cells this is inferred: gen17 is a copy of the wf14 generator (`gen17.js:1`).
- These branches are about 85–90 of the 842 lines, an estimate. They cannot be removed from the sealed copies without changing 81d7b47e.

**Supporting modules**
- **identity.js:**
  - (c) in plain mode: `:13-30,65-77`.
  - Adapter-only parts: `snapshotMs`, `snapshotHash` (`:32-62`), reached through scenario.js `:188-190`. They are (b) for R1 in the repo, and (c) for the external predictors (`kits/via-real-2026-10-26/emulate_via.js:48,69`).
- **normalizer_emul.js:**
  - (c): `maxRideFromPermille` (`:31-43`, called at scenario.js `:207`).
  - (a) in the repo: `hmacHex`/`fleetPlacement`/`nodeInt` (`:13-29,45`). They serve only the external `fixtures.js`.
- **lib.js:** in the in-repo emulator only `rqId` is reached, and only in adapter mode (scenario.js `:212`), so (b). The rest is wf4 fixture helpers, (a) here.
- **shcore/run_sh:** these paths are (a) in the repo:
  - the wf13 engine switch (`shcore.js:41-45`);
  - oracle/random score modes (`run_sh.js:113`);
  - the test flags repeat, truncate, cmp-ref, hashes.
- **Scripts copied but not used by their own runbook:** r3drive, rw, pad, panels, make_cells, cmp_rows and gate_p2 in fullsys/n7. They are copies of r1's (b) scripts. gen17.js (r1) is (c) because it rebuilds the 96 F16 cells; their sha list is `expected/wf17-f16-cells.sha256`.

### 4. External adapter-mode code (step 4)

**sha256 values**
- wf15 `engine.js` = `908a29619f55a134f9ed5a0d35d4be1d3c502f07de78dc6fc704963690e7496f`.
  - Recorded in `kits/bridge-phase1-2026-10-23/EMULATOR-PREDICTIONS-P1.sha256:10` (also R128 `:12` and DWELL `:7`).
  - The file itself is not in the repo.
- `emulate_via.js` (in repo) = `df1ed5a8…4fb86`. Verified against `VIA-KIT.sha256:7`.
- `engine_wf15_clint.js` and `emulate_via_full.js`: unknown.
  - Searching all branches and history only finds mentions: `docs/18:5946,6010`, `TravelTimeForecast.cs:11`, `TravelTimeForecastTests.cs:12`.
  - `VIA-FULL-PREDICTIONS.sha256` and the `FULLSYS-F14-*` seals are external.
  - E: and C:/RideBoundContinuation are not present on this machine.

**Link from the predictors to the in-repo identity.js (inferred, not byte-checked)**
- `emulate_via.js:18-22` loads `emulator-wf15/identity.js`.
- `PHASE1-LOG.md:9-14` says that emulator copy is the intake's `wf15/runner/*.js`, byte-identical except lib.js.
- `CODE-HASH r1:4` says the wf17 identity.js is that file's identical copy, be261baa, which equals the in-repo copy.
- emulator-wf15's own `MANIFEST.sha256` (fbdd49bb, P1 seal `:9`) is external.

**Confirmed difference:** clint is not engine17.
- `TravelTimeForecast.cs:10-11` rounds each forecast arrival to whole milliseconds, ties up, "the integer convention of the adapter-mode emulator, engine_wf15_clint.js".
- engine17's `legF` (`engine17.js:156-167`) returns an unrounded arrival.
- So the in-repo engine cannot regenerate the sealed adapter-mode CL predictions (Shadow gate, forecast gate, 168-job campaign).
- The forecast golden test records no source hash. The shareability golden does, and its hashes are the in-repo sscore.js b295b8a9 and lambda.js cceb8e61.
- Whether clint also reproduces the plain-mode numbers is unknown.

### 5. What changes and what stays unknown

**Classification change**

| Item | Before | After |
|---|---|---|
| engine17 + identity + normalizer + scenario + sscore + lambda + forecast + metrics_sh (6 key directories) | blanket "KEEP, frozen" | (b) byte-frozen sealed archive, one logical module, no edits |
| Rewrite scope | none | the (c) subset in section 3, checked with `tools/confirm_digest.js` against the expected digests |
| P1/P2/P3 knobs, NCE, adapter identity path | (inside the blanket keep) | optional (b) extensions |
| RR, FZ, OCE, SHNC; g1, dr, g2MaxSwitches; ratchetFloor/none; tieMode, reoptTie; shadowArms, pairs, trace; physMat | (inside the blanket keep) | (a): left out of any rewrite, kept in the archive |
| RC, PS, DLHZ, DLS, NC, OC | (inside the blanket keep) | (a) through engine17, (b) only through the external `engine.js` |
| engine_wf15_clint.js, emulate_via_full.js | not recorded | (c), external and missing; capture bytes and sha256 before any rewrite |

**Still unknown or unverified**
- clint/emulate_via_full bytes and their sha256.
- Whether the 336/336 2×2 table rerun (`docs/18:6034`) used engine17 or clint.
- R1, R2 and R7 rows have no in-repo digest; their thesis numbers are marked [CT], company-only (`24-CHO-SUA-KHOA-LUAN-26-10.md:8,71`).
- F16 can be reproduced only with the external cells.
- The original bytes of lib.js, netfull.js and shlib.js before the path change.

Nothing outside `CE` loads these files. Only docs mention them, and the C# comments cite `engine17.js:151-167`, `:454`, `:742-776` and `engine.js` line ranges. Editing engine17 would leave those citations stale.

### gap:4
**Verdict: the gap is real but narrower than stated.** The 5-file hash `34517a83…4d90` does not cover the normalizer's full byte path. The dependency mainly threatens two things: the ability to regenerate fixtures, and the scenario families that were generated outside the repo. It does not threaten the thesis's main Via numbers.

## 1. What `normalizerSourceSha256` covers

- It covers only the 5 files in `src/RideBound.Benchmarking/Normalization/` (`FleetPyNormalizerSourceIdentity.cs:9-16`, `:20-24`).
- I recomputed it with the same framing at HEAD ad470c1: `34517a832014c78a572282e01795fc243896db088249fd6dade70beed2aa4d90`. This matches every committed fixture.
- The docs treat this hash as "exact source inventory hash / code identity" (`docs/benchmarking/wp6-contract-v1.md:202,245`). The readiness audit and `prepare_panelA.py:9,35` used it as the code check (`docs/18-status-and-decision-log.md:6163-6165`).
- **The gap has already happened once, harmlessly.** Commit 38d517d (2026-08-24) edited `CanonicalJson.cs:182` and `ProtocolPayloadValidation.cs:203`. That was after the tiny/medium (2026-08-17) and wp9-v2 (2026-08-22) fixtures were made, and the hash did not change.

## 2. Full byte path outside the hash (wider than the gap lists)

**RideBound.Contracts**
- `CanonicalJson.Canonicalize` (`CanonicalJson.cs:51`):
  - called directly at normalizer `:1010` and `:1145`;
  - called indirectly through `BenchmarkContractCodec.Encode` (`:131-132`), `BenchmarkIdentity.RequireCanonical` (`:154-163`) and `SafeZipExtractor.CanonicalInventory` (`:689-709`).
- The requestArrived payload chain:
  - `EventBatchPayloadCodec.Encode` (`EventBatchMessages.cs:195`)
  - → `ProtocolPayloadReader.Write` (`ProtocolPayloadValidation.cs:288`)
  - → `ProtocolEventPayloadCodec.Write`, RequestArrived branch (`EventBatchMessages.cs:310-314`)
  - → `OnlineContractCodec.WriteRequest` (`OnlineEventModels.cs:404`), plus `RequestContract` (`:60`) and `RequestArrivedEventPayload` (`:93`).
  - The payload is stored in every scenario event as `payloadCanonicalJsonHex` and `payloadSha256` (normalizer `:497-508`).

**RideBound.Benchmarking.Contracts**
- `BenchmarkContractCodec.Encode<T>` and `CreateOptions` (`:117-149`): camelCase, WhenWritingNull, string enums.
- `BenchmarkModels.cs`: `ScenarioContent` (`:235`), `NormalizationReport` (`:84`), the nested Scenario* records, the enum wire names (`:106-124`) and `V1_0_1` (`:8`).
- `BenchmarkIdentity`: `CalculateScenario` and `CalculateNormalizationReport` (`:20-30`), the shared helpers (`:109-163`) and the domain strings (`:11-13`).
- `BenchmarkContractValidator` can only make regeneration fail; it cannot change bytes.

**Benchmarking code outside `Normalization/` (not in the gap list)**
- `Datasets/SafeZipExtractor.cs:689-709` produces `sourceMemberInventorySha256`, which goes into the report and so into `reportHash`.
- `DatasetSourceRegistry.cs:33-60` supplies `datasetId` (in the scenario), plus citation and licence (in the manifest and ATTRIBUTION).
- `tools/RideBound.Wp6Normalize/Program.cs:182-206` (`GridConfiguration`) feeds `configurationSha256`, which is embedded in the scenario (normalizer `:154`, `:533-534`). `:239-280` holds the 7-file set and the ATTRIBUTION text.

**Coupling at source level**
- `normalizerSourceSha256` itself is embedded in the scenario and the configuration (normalizer `:185`, `:223`, `:533`; `FleetPyNormalizationModels.cs:27`).
- So any edit to the 5 files changes every regenerated byte. That includes just changing the `using` lines (`FleetPyManhattanNormalizer.cs:5-8`) after moving a type to another namespace.
- A "move" is therefore safe only if the namespace, type name and member signatures stay the same.

**Toolchain:** `global.json` (SDK 10.0.301, latestPatch). This is a residual risk I could not measure.

## 3. Who actually consumes these bytes (corrections to the gap)

**Committed fixtures (602 files, 86 cells)**
- Recorded results read these files as stored bytes, so a Contracts change cannot alter them.
- What it breaks is the reproducibility requirement: R-010, RB-WP6-004 and RB-WP6-012 (`docs/19:177,565,573`) and dataset `README.md:67`.

**Normalizer outputs that are not in the repo (the real recorded-result risk)**

| Fixture family | Cells | Evidence |
|---|---|---|
| via-bridge-check-1500 and man-2000 | 11 each | `make_grids.py:9-12`, `prepare_bridge.py:17-23` |
| man-2000-r128 | 10 | `prepare_r128.py:16` |
| tier3-panel-a-extra | 29 | `normalize_panelA.py:19-24` |
| w07 | 8 | `docs/18:6306-6309` |

- The 228 replay-gate jobs (ins 80, g2 33, p1 76, r128 39; `PHASE1-LOG.md:378`) and ADR-089 G1/G2 (`docs/18:5920-5927`) ran on the bridge and r128 families.
- The only in-repo checks for those bridge and r128 outputs are:
  - `fixtures-via-bridge-2000-v1.sha256.txt` and `fixtures-via-bridge-r128-v1.sha256.txt`;
  - the driver files carrying `sourceScenarioHash`: 11 bridge, 10 r128, 49 panelA.
- For panel-A-extra the only check is its 49 drivers. I found nothing in the repo for w07.
- If the external copies are lost, they can be rebuilt byte-for-byte only if the whole byte path is unchanged. I can't tell from the repo whether final-v2-pack contains them.

**Emulator**
- `lib.js:8,119` point at an external clone of the wp9-v2 and wp14 fixtures, and `loadCell` parses the payload (`lib.js:55-57`).
- But in the repo, `loadCell`'s only callers are `emulate_via.js:36` and `emulate_via_diag.js:25`. They read Via "vz" fixtures built by Python (`convert_via_zone.py:181-203`), which uses its own hash domain (`:315`).
- The F14/F16 Via cells come from `gen17.js` using company scripts (`gen17.js:18-20`; `AMEND-A0.md:33`).
- So the main thesis 2×2 table and the 168-job Via campaign do **not** depend on this byte path. Only the Manhattan results do (panel A, wp14 fresh cells, the bridge).

## 4. Existing protection (partial)

- `FleetPyPublicDerivativeTests.cs:10-11,26-61` checks a decode→re-encode round trip and recomputes both identities, but only for **tiny and medium**.
- The payload is opaque hex. Validation checks only that it is canonical and that its sha matches (`BenchmarkContractValidator.cs:538-555`, `:1043-1064`), so a change in how requests are encoded goes undetected.
- `FleetPyManhattanNormalizerTests` uses synthetic data and checks determinism only, with no stored expected output.
- Two manual regenerations exist:
  - `wp8-012 receipt:21`: 20/20 `reusedExactDerivative=true` (2026-08-21);
  - `BRIDGE-REPORT.md:19-20`: 11/11 cells, 7/7 files byte-identical at 1.5× (2026-10-22).
- `git log` shows no commit touching the byte path after 38d517d. HEAD therefore very likely still matches what passed on 2026-10-22. This is an inference: it relies on ADR-082's statement that the 25/10 history rewrite changed only author and message.

## 5. Classification change

Neither the normalizer nor its byte path is needed by the final workflow: the Runner references no Benchmarking project. Both stay **(b)**: kept to regenerate or verify recorded inputs.

| Code | Before | After |
|---|---|---|
| `EventBatchPayloadCodec.Encode`, `ProtocolPayloadReader.Write`, the RequestArrived write branch, `OnlineContractCodec.WriteRequest` and their records | (b) "historical-only, movable" | **(b) KEEP-frozen**; may move only with namespace and signatures unchanged and the golden test passing |
| `CanonicalJson` | (c), used by the Runner (`RunnerSession.cs:485,613`; `NdjsonWriter.cs:29`; `ProtocolHash.cs:24`) | **(c) plus byte-frozen**: no simplification that changes output |
| `BenchmarkContractCodec` (Encode/Decode/Options), scenario and report models, `CalculateScenario`/`CalculateNormalizationReport` and shared helpers, scenario/report validator rules | proposed trim | **(b) KEEP-frozen** |
| `SafeZipExtractor.CanonicalInventory`, the `DatasetSourceRegistry` FleetPy entry, Wp6Normalize `Program.cs` | not listed | **added to (b) KEEP-frozen** |
| Run-side identity and documents (`CalculateRun`, `CalculateMetricSet`, `CalculateBundle`, `CalculateBenchmarkPlan`, `CalculateExperimentalUnit` and their records) | trim proposed | unchanged by this gap; decide on their own (b) evidence, without touching the shared helpers |

## 6. Changes to the suggested check

1. **KEEP-frozen set:** use the expanded list above. Do not change `FleetPyNormalizerSourceIdentity`, because that would change the embedded hash and so every regenerated byte.
2. **Regeneration test:** the tool has no output-directory option (`Program.cs:89-97`), reads its source from `--repository`, and needs the 408,878,341-byte Zenodo archive cached outside the repo (`DatasetSourceRegistry.cs:41`).
   - Set up a temp root holding the 5 source files, the committed cell folder, and a one-cell grid taken from `grid-v2.json` or `grid-v1.json`. Use wp9-v2 `d20181114-s10-r1` and wp14 `d20181112-s10-r1-w08`.
   - `reusedExactDerivative=true` is itself the 7-file byte comparison (`Program.cs:262-280`). A mismatch throws without overwriting.
   - Add one bridge cell checked against its sha manifest, to cover the families stored outside the repo.
3. **Run on every change, in two tiers:**
   - **(A) Every commit, no dataset needed:** extend the tiny/medium test to all 86 committed cells. For each request, also rebuild the payload through `EventBatchPayloadCodec`→`CanonicalJson` and compare it with `payloadCanonicalJsonHex`.
   - **(B) Full regeneration (step 2):** run it where the Zenodo cache exists, with a path filter covering `src/RideBound.Contracts/**`, `Benchmarking.Contracts/**`, `Benchmarking/Datasets/**`, `Normalization/**`, `tools/RideBound.Wp6Normalize/**` and `global.json`. CI today runs only `dotnet test RideBound.slnx` (`ci.yml:59`), with no dataset.

## Unknowns

- Whether final-v2-pack contains the bridge, r128 and panelA fixture copies.
- Whether the external `RideBound-run` clone's fixtures are byte-equal to the committed ones.
- How much the .NET runtime itself affects the output bytes.

The only scratch file is `<scratchpad>\srchash.js` (recomputes the normalizer source hash). Nothing in the repository was changed.

### gap:7
Only part of this gap could be settled. E: is not on this machine (`Test-Path E:\` is False; the only drives are C, D and G), and export commit `0eb9a6ca` is not in the repository (`git cat-file -t 0eb9a6ca` reports "Not a valid object name"). Checks 1 and 2 against the final-v2 pack and fastrun-2026-11-02 could not be run. Check 3 was done: the ADR-090 harness is still on this machine, in a temporary session folder.

**Check 3: the ADR-090 "47 config pairs" harness**
- **Location:** `<scratchpad>/` (subfolders `harness`..`harness4`, plus `bindings.txt` and `b2.out`). It is not in the repository and sits in a Temp folder, so it should be archived somewhere permanent. Nothing was copied, per the read-only rule.
- **Where the configs come from:** `harness4/Wp4.cs:46` builds both paths from in-repo `benchmarks/configurations/`.
- **The 47 pairs:** `b2.out` has 47 unique pairs, 564 compare lines and "FAILS 0". They are 5 commitment configs paired with in-repo WP4 configs:
  - commitment configs: `wp3-boundary-test`, `wp8-drop-eta-budget-medium`, `wp14-c1-h6-reference`, `wp14-c1-f1f2-freeze300-ratchet`, `wp6-public-mechanical-commitment`;
  - WP4 configs: wp4 boundary, wp7, wp9 (including commit-soft-hard-hybrid), wp13 retained, wp14-development.
- **Keys the harness uses:** `freezeHorizonMs`/`freezeHorizonLocks`, `solverExecutionEvidenceProfile`, `skipConstantObjectiveLevels`, `warningProfiles`.
- **Keys it does not use:** `fixedFreeze`, `repair`, `multiplePlan`, `dropEtaDeadlineTwoSided`, origin-hold, `stopServiceDurationMs`, `maximumRidersPerVehicle`, `waitingRiderRelocation`, `shadowAllocation`, `planningForecast`, `auditHash`.
- **Restore and the v2 flag:** it restores the Runner from every checkpoint (`Wp4.cs:55-66`). `Light.cs:18-21` sets `RIDEBOUND_STATE_HASH=v2` to test the ADR-090 fix 4 refusal.

**In-repo evidence that confirms the risk**
- **Configs must match byte for byte when a recorded run is replayed.**
  - Unknown keys are rejected: `CommitmentPolicyConfiguration.cs:82,368-392`; `Wp4RunnerConfiguration.cs:300-308,953-976`.
  - The canonical config bytes are hash-bound into every run manifest: `mapping.py:150-158` (`wp4_policy_binding_hash`), `Program.cs:111-115`. A mismatch fails the session with `HASH_MISMATCH` (`RunnerSession.cs:352-361`).
  - So a recorded run cannot be replayed with a key stripped out of its config. Any key that appears in a recorded config is at least (b).
- **The final configs are not in the repository.** No tracked `*.json` contains `auditHash`, `planningForecast`, `shadowAllocation` or `dropEtaShadowAllocation` (git grep count 0 for each). An in-repo grep therefore cannot show that a key is unused by the final workflow.
- **Private adapter names are used from outside the package:**
  - `bridge_launcher_via.py:37-75` patches `rc._EOF`, `RunnerClient._read_stdout`, `RunnerClient.__init__`/`_timeout` and `session.RUNNER_MAXIMUM_OUTPUT_LINE_BYTES`.
  - `preflight_world.py:171-191` patches `module._trigger_and_resolve` and `control._rb_travel_arc_cache`/`_rb_travel_dirty`.
  - `preflight_world_v1_1.py:47-58` patches `module._next_physical_boundary`.
  - ADR-090 item 3 (`docs/18:6075-6078`) and `test_session_line_bounds.py:62-74` deliberately keep these patches working.
- **Deleting any tracked file changes provenance hashes.** It changes HEAD and the repository inventory hash (`actual_fleetpy_medium_preflight.py:771,868-877`), and with them `coreCommitSha`, `manifestHash` and `checkpointBindingHash` (`docs/18:5980-5990`).
- **The pack itself is frozen and unaffected by cleanup here.** final-v2 is a separate export with its own Runner and .NET (`docs/18:6019,6030-6045`). What cleanup could break is replaying the pack's transcripts with a Runner built from the cleaned repository, and rebuilding the pack.

**Resulting classification changes**

These five REMOVE_OK verdicts are refuted by in-repo evidence alone and become (b):
- **`freezeHorizonMs`/`freezeHorizonLocks`** (`CommitmentLockEvaluator.cs:76-84`):
  - used by the recorded WP14R arms c1-freeze300, c1-freeze600 and c1-freeze300ratchet (`wp14-c1-f1-freeze300-v1.json:98-99`, `-freeze600:98`, `-f1f2-freeze300-ratchet:98`; bound to arms in `wp14_freeze.py:90-97`);
  - results recorded in `docs/benchmarking/wp14r-010-…:46-49` and `wp14r-013-…:82`;
  - also part of the ADR-090 harness (20 runs, all SAME).
- **`dropEtaDeadlineTwoSided`** (`CommitmentLockEvaluator.cs:237`):
  - used by `tang3/configs/M-30.json`, `M-60.json` and `M-120.json` at line 99 (generated by `make_configs_t3.py:82`);
  - recorded Tier 3 runs: 48/48 jobs and the 60-job pilot (`docs/18:5185-5188,6349-6354`).
- **`stopServiceDurationMs`** (`Wp4RunnerConfiguration.cs:728-735`):
  - used by `bridge-phase1/configs/wp4-costfirst-dwell24.json:17` and `wp4-g2-costfirst-dwell24.json:17`;
  - six recorded dwell-24 runs that failed, cited in the thesis (`docs/18:5518`, ADR-083);
  - the dwell-off replay gate depends on it (`replay_gate_dwell.py:1-9`).
- **Adapter restart/restore** (`fleet_control.py:145-171`, `session.py:590-622`, `runner_client.py:188`):
  - it is the WP7 closure gate (`actual_fleetcontrol_preflight.py:260-263`, `actual_fleetcontrol_lifecycle_matrix.py:179`; `docs/benchmarking/wp7-014-…:83-84`; ADR-038 item 5 at `docs/18:3409-3411`);
  - the Runner-side restore is exercised by the ADR-090 harness (564/564);
  - it is not (c), because no recorded run restores (`docs/18:6094`).
- **`wp14r_*` helpers:**
  - no in-repo importer outside the wp14/wp14r family and its tests;
  - the in-repo job chain imports none of them (`run_via.py` → `bridge_launcher_via.py` → `preflight_world*` → `actual_fleetpy_medium_preflight.py` → `ridebound_fleetpy`);
  - they are the toolchain behind the recorded WP14R and C60/C120 results, so they are (b).

Two more entries were checked and are not removal candidates:
- **`solverExecutionEvidenceProfile`** is (b): it is used by the wp13 and wp14 configs and by the ADR-090 harness.
- **`_repository_inventory_sha256`** is (c): it is on the final job path (`actual_fleetpy_medium_preflight.py:771,868-877`), and `kits/bridge-phase1/inventory.py:1-5` copies it.

These remain conditional, with the open risk narrowed:
- **`fixedFreeze`/`repair`(B4)/`multiplePlan`(B5):**
  - the parser ties them to the B3/B4/B5 policyIds (`Wp4RunnerConfiguration.cs:897-919`);
  - every final-arm feature needs C1 (or C1/C2):
    - cost-first needs C1 (`:498-503`);
    - G2 needs C1 with cost-first (`:527-534`);
    - no-worse recovery needs C1/C2 (`:402-410`);
    - B1–B4 drop the deadline and permille settings (`docs/18:4819-4820,5347`).
  - So no passing final job can carry these keys. That makes them (a) for the final workflow.
  - No recorded use in the repository: `EXTERNAL-ARTIFACTS.json` and `HANDOFF-INVENTORY.json` have 0 hits.
  - Still unchecked: historical bundles under `E:/RideBoundData/wp*`.
- **origin-hold-relocated-wait:**
  - all 23 in-repo WP4 configs use earliest-feasible;
  - Shadow and G2 both force earliest-feasible (`:565-571`, `:708-712`);
  - ADR-090 states that every recorded run used earliest-feasible (`docs/18:6066`).
  - It is (a) for the final workflow if the 168 jobs are 84 cells × 2 Shadow arms. That is an inference from `docs/18:6000,6004-6005`.
  - Still unchecked: the E: configs.
- **`maximumRidersPerVehicle`:**
  - no in-repo use; the G2 configs set only `maximumTargetVehicles` (k3/k8/a1);
  - the emulator's equivalent is a cap of 99 (`engine17.js:124,527-529`; `shlib.js:14`);
  - the final-v2 configs could legally contain it, so it stays unknown.
- **`RIDEBOUND_STATE_HASH` v2:**
  - no recorded number depends on it: every job ran with it unset (`docs/18:5373-5375,5691-5692`);
  - before ADR-090, light-v1 combined with v2 failed at the first checkpoint, and the pack's gate jobs passed (`docs/18:6019-6021,6079-6080`);
  - removing the flag means it is silently ignored, not a parse failure;
  - it is (b) only for the `Light.cs` check;
  - still conditional on `run_cloud.ps1` and `run_queue.py` not setting it.

**Still needed on the machine with E:**
- **List the zip and grep the configs:** run `unzip -l` (or .NET) on `ridebound-final-v2.zip`. Grep its `*.json` files and the fastrun `*.json` files for:
  - keys: `fixedFreeze`, `repair`, `multiplePlan`, `origin-hold`, `maximumRidersPerVehicle`, `stopServiceDurationMs`, `freezeHorizon`, `dropEtaDeadlineTwoSided`;
  - non-empty `stopDistances`, which is a required root key (`CommitmentPolicyConfiguration.cs:16-19,82`), so the parser must keep it either way;
  - the `policyId` values.
- **Grep the fastrun scripts:** search the `*.py` and `*.ps1` files for `ridebound_fleetpy`, `restore(`, `restart`, `_repository_inventory_sha256`, `wp14r_`, `RIDEBOUND_STATE_HASH` and private `_` names from `ridebound_fleetpy` and the preflight. These include `replay_gate_g1/g2.py`, `replay_runner_big.py`, `complete_oom_job.py`, `finalize_job.py`, `evidence_reader.py`, `run_queue.py` and `run_cloud.ps1`.
- **Grep the historical bundles:** search `E:/RideBoundData/wp*` manifests for `fixed-freeze-horizon`, `no-reassignment-repair` and `least-commitment-consensus`.

### gap:0
**Finding: the gap is confirmed. "Thesis numbers come only from the plain-mode emulator" is false for thesis commit `7f93ee6`. It is true for the newer Via-only thesis `34920ee`, but that version still cites C# Runner results.**

**How I got the thesis.** `Report_INT3508` is not on this machine (there is no E: drive). I made a blobless clone of the public repo (https://github.com/tsunflowerr/Report_INT3508) into the scratchpad. I did not run anything from it.
- Clone: `<scratchpad>/thesis-clone/repo.git`
- Extracted chapters: `…/thesis-clone/t7f/`, `…/thesis-clone/tvia/` and `…/thesis-clone/t349/` (same scratchpad folder).
- Nothing in RideBound-Code was changed.

## 1. Two thesis states exist; which one was submitted is unknown

**`7f93ee6` (26/10, 95 pages)** is the commit docs/18 calls the thesis (docs/18:1737, :5832, :6225; docs/19:39, :41).
- `main.tex:120` inputs `08-chuong4`, and `08-chuong4.tex:1268` inputs `08c-via-elmi`.
- `08c-via-elmi.tex:10` says the earlier sections "chạy RideBound thật trên những kịch bản Manhattan nhỏ" (ran the real RideBound on small Manhattan scenarios).

**`34920ee` (04/11 01:16 +0700)** is the current head of `thesis/audit-fixes-2026-10-25` and `thesis/editorial-audit-2026-11-04`.
- It is a 55-page Via-only rewrite. `main.tex:60-72` inputs only `noi-dung/via/*`.
- Its README says the multi-day results come from the JavaScript emulator and that the six C# zone runs only confirm riders.
- This matches the owner's decision of 30/10 (docs/18:5871: Via is the main evaluation, Manhattan is a pilot).
- `git grep` finds no mention of `34920ee` anywhere in RideBound-Code.

Which PDF was or will be submitted is unknown. The submission manifest is external, and items W30–W32 are still open (docs/18:5861). docs/18:6028 (04/11) still says "khóa luận trích dữ liệu Manhattan" (the thesis cites the Manhattan data).

## 2. Who produced each number

**In `7f93ee6`**

| Thesis item | Producer |
|---|---|
| Table 4.2 (:73) | C# Runner settings from `wp4-tier3-v2.json` (100 candidates, 2 new requests, OR-Tools 9.15, seed 7) |
| Table 4.3 (:115), 4.5 (:261), 4.6 (:312), 4.7 (:369), 4.8 (:415), 4.10 (:535), 4.11 (:571), figure (:685), 4.12 (:724) | C# Runner `runner-tier3-v2`, built from `45f8026` (tree `ac251ef3…`) with `wp4-tier3-v2.json`: stability-first selection, no-worse recovery (T3.8, C-120, w07/w07x) |
| Table 4.9 (:499) | Older C# Runner WP14 `runner-v1` (`runnerArtifactSha256 b3260f29…` in `freeze-receipt-v2.json`), no recovery rule; FREE = B1 rolling-cost |
| Table 4.13 (:851) | Stability-first side: `runner-tier3-v2`. Cost-first side: Runner built from `dce84ec`, 68 jobs |
| Tables 4.14 (:950), 4.15 (:1039), 4.16 (:1104), 4.17 (:1194) | Python replay over the C# Runner transcripts (plus 8 snow runs on `runner-tier3-v2`) |
| Validity section (:1236-1266) | C# Runner; test run 1,025/1,025 on `45f8026` |
| Abstract `02-tom-tat.tex:12-13` (2,364 vs 2,338); `05-chuong1.tex:143, 253-254`; `09-chuong5.tex:12-48` | C# Runner |
| Appendix C (`11-phuluc.tex:61-130`) | Names `runner-tier3-v2`/`45f8026`, core `1ce15e92`, `wp4-tier3-v2.json` |
| Appendix D (`11-phuluc.tex:133-205`) | `dce84ec` runs; 9,638 rider rows; 83 checks; Table D.1 is a Python re-selection over WP14 `runner-v1` decisions |
| `08c` tables (:104, :195), robustness section (:249), Shadow section (:304), Appendix E.1 (:218) | Emulator in plain mode (identity robustness also uses adapter mode, P5) |
| `08c` bridge section (:265-302) | C# Runner: 238 Manhattan pairs, 6 of 12 Via zone pairs (2,407 riders), 232,103 decisions |
| W28 sentence (`09-chuong5.tex:43-48`) | Emulator numbers (55 Via days). The "installed in RideBound" claim refers to `dropEtaRevisionLimitMs` (bridge config PS-300) |

**In `34920ee`**
- Emulator in plain mode produces every comparison.
- The C# Runner produces only `tab:bridge` (`04-thuc-nghiem.tex:236-262`, `05-ket-luan.tex:22`) and the configuration table `tab:hai-chuong-trinh` (`03-phuong-phap.tex:119`).

## 3. Repository evidence that ties the in-repo code to the thesis

- **T3.8 configs are byte-identical.** `kits/tang3/configs/{U,C-30,Mplus-30,Mplus-60,V-30}.json` hash exactly to `CONFIG_SHA256` in `docs/benchmarking/tier3-t38/run_t38.py:46-52`. `V-30.json:6` uses `customerVisible`; `Mplus-30` adds `dropEtaDeadlineSlackMs` 30000.
- **The bridge configs descend from the T3.8 config.** `kits/tang3/stage1b/harness/wp4-tier3-v2-costfirst.json` (`37040657…`) is `wp4-tier3-v2.json` plus `selectionObjectiveOrder: cost-first`. It is the base of every bridge WP4 (`bridge-phase1-2026-10-23/make_configs_p1.py:23`, `via-bridge AMENDMENTS.md:62`, `PHASE1-LOG.md:40`). The Via A1 configs differ from it only in 4 solver-budget lines (`VIA-LOG.md:55-58`).
- **Shared files with the Via bridge** (cited by both thesis versions):
  - The Via bridge `U.json` is the same file as the T3.8 `U.json` (`VIA-CONFIGS.sha256:1`, `998a0a94…`).
  - `world-via/preflight_world_v1_1.py` and `v1_2.py` are byte-identical to `kits/tang3/world/`.
- **Stability-first is the default and is thesis-only.**
  - It is used whenever `selectionObjectiveOrder` is absent (`Wp4RunnerConfiguration.cs:489-496`).
  - Per-rider budgets and G2 refuse to run without cost-first (`:506-534`), so the final workflow cannot use stability-first.
  - The stability-first ranking level is at `SolverBackedFleetSelection.cs:23-27, 347-360`.
- **Fixed-set fixtures and normalizer.**
  - `benchmarks/fixtures/wp6/.../wp14-development-panel-v1` holds 16 cells (12–13/11 × r1–r4 × w08/w17). These are Table 4.9's inputs.
  - The normalizer has not changed since `45f8026` (`git log 45f8026..HEAD -- src/RideBound.Benchmarking/Normalization` is empty).
  - The bridge fixtures (`fixtures-via-bridge-2000-v1`, 11 cells) are also normalizer output.
- **Stage 1b results are in the repo.** `research/continuation/2026-10-15/results/stage1b-riders.tsv` has 9,639 lines (9,638 rows plus header). `STAGE1B-RESULTS.md:24-27` holds the Table 4.13 numbers.

## 4. Classification change: historical-only becomes (b)-thesis (`7f93ee6`)

| Code path or artifact | New class |
|---|---|
| Stability-first order (enum, default, ranking) | (b)-thesis |
| `customerVisible` (`CommitmentPolicy.cs:30`, `CommitmentDecisionValidator.cs:536/807`, `CommitmentLedger.cs:331`, `CommitmentPolicyConfiguration.cs:251-256`, canonical writer/codec) | (b)-thesis |
| `dropEtaDeadlineSlackMs` one-sided deadline (`CommitmentLockEvaluator.cs:110, 215-236`) | (b)-thesis plus bridge verification |
| Fixed `drop_eta_total_ms` `hardLimit` | (b)-thesis plus bridge verification. The evaluator line `CommitmentBudgetEvaluator.cs:88-92` stays (c) because the per-rider budget flows through it |
| `dropEtaRevisionLimitMs` (`:124`) | (b)-thesis verification |
| `forced-reference-v1` and `no-worse-than-reference-v1` | (c) and (b). Final EO35 needs forced-reference (`VIA-LOG.md:178-180`) |
| B1 `RollingCostPolicy` (FREE) | (b)-thesis |
| `kits/tang3/configs` (incl. `wp4-tier3-v2.json`, `C-120.json`) | (b)-thesis |
| `kits/tang3/stage1b/harness` (`run/extract/report_stage1b*`; its `wp4-tier3-v2-costfirst.json` also verifies both thesis versions) | (b)-thesis |
| `kits/tang3/world` v1.2/v1.3 | (b)-thesis |
| `research/continuation/2026-10-15` | (b)-thesis |
| `docs/benchmarking/tier3-t38` (incl. `split-t38-v1.json`, which is Appendix Table C.1) | (b)-thesis |
| `docs/benchmarking/tier3-forecast` | (b)-thesis |
| WP6 normalizer, `wp14-development-panel-v1` fixtures, `wp14-development-*-v1` configs | (b)-thesis |
| Dwell config `stopServiceDurationMs` (`Wp4RunnerConfiguration.cs:115, 726-735`) | (b)-thesis statement: `08c:285-288` says the code accepts it |

No change for `research/continuation/2026-10-16/cost-cap-sensitivity-v1` (cost-first C60/C120). It is not cited (`08-chuong4.tex:1336-1337`), so it stays (b)-historical.

## 5. REMOVE_OK verdicts checked against the W04 table (`11-phuluc.tex:244-264`)

The four named verdicts survive the thesis check, but none of them is (a); each is (b)-recorded-historical.

- **Two-sided deadline** (`CommitmentLockEvaluator.cs:237-241`, `CommitmentPolicy.cs:231-251`, config `:177, :205-228`):
  - Not in any thesis number. "Strict" in W04 means one-sided plus forced-reference (`make_configs_p1.py:4-5`), and M+ only blocks the late side (`07-chuong3.tex:436`).
  - But `kits/tang3/configs/M-{30,60,120}.json:99` were used by the 48 VM jobs (docs/18:5184).
- **Freeze horizon:** not in any thesis config, which use `finalConfirmationLocks` only. Used by the WP14 `c1-freeze300/600` ablation (`wp14_freeze_v2.py:109-116`, docs/18:6410).
- **B2–B5:** not in the thesis. B1 must not be removed with them.
- **Origin-hold:** every thesis job used `earliest-feasible` (`wp4-tier3-v2.json`, `wp4-via-*-a1.json`, `wp14-development-ridebound-hard-vector-v1.json`).

Overturned: any REMOVE_OK on an item marked "in RideBound" or "research branch" in W04, namely:
- the fixed rule C cap;
- the per-decision cap;
- the strict and relaxed deadline;
- the slack budgets;
- G2;
- the dwell config;
- and also stability-first and `customerVisible`.

## 6. Step 6 (replay one T3.8 transcript with a HEAD Runner) was not run

- The T3.8 transcripts (`C:/RideBoundData/research/tier3-t38-v1`) and the `runner-tier3-v2` build are not on this machine.
- Building a HEAD Runner would write `bin/obj` into the repository, which is not allowed.

Static evidence instead:
- `45f8026` is an ancestor of HEAD, with 12 source commits since (58 files, +7,042/−328).
- Since `45f8026`, `dce84ec`, `3afcd17`, `18978dd`, `c6588f8` and `9ba5568` touched the ranking/lock files (`HardVectorCandidateAssessor`, `SolverBackedFleetSelection`, `CommitmentLockEvaluator`). `RollingCostPolicy.cs` changed too (+147 lines).
- The G1/G2 replay gates (docs/18:5920-5930) replayed only the 228 bridge jobs, and all of them were cost-first.
- The ADR-090 byte harness (564/564) compared `59c4ee8`/`9ba5568` against the branch, not against `45f8026`, and its config list is not in the repo.
- So, as far as the repository shows, T3.8 can be reproduced only from `runner-tier3-v2` (`45f8026`, manifest `kits/tang3/runner-tier3-v2.sha256.tsv`) with the adapter at `1ce15e9`. Whether HEAD reproduces it is unknown.
- The same applies to the `dce84ec` Runner (tree `d08c0057…`) and the WP14 `runner-v1`, whose source commit is unknown.
- `45f8026` and `dce84ec` exist only on branches; there are no tags.

To settle it, two things are needed:
- **A replay gate:** at least one recorded job per stability-first configuration replayed byte-for-byte on a HEAD Runner, with commit and hash fields stripped.
- **Tags on the producing commits,** so these numbers stay reproducible if the code is later removed from HEAD.

### gap:3
I resolved all five conflicts. None of them puts a recorded result at risk, whichever way it is decided, but four of the five verdicts change. One more problem turned up: the E1 provenance declaration is already out of date at HEAD.

## Merged verdict for the disputed paths

| Path | Area verdicts | Resolution | Class |
|---|---|---|---|
| `tools/RideBound.CandidateHotPathBenchmark/*`, `RideBound.slnx:25` | algorithms KEEP / benchmarking REMOVE_OK | **REMOVE_OK from HEAD**. Recover it from `2d6791f` and repoint `docs/19:747` | (b), recoverable from history |
| `src/RideBound.Runner/Protocol/CapabilityNegotiator.cs:11-19,102-126` (downgrade profile and branch) | runner keep (implied) | **REMOVE_OK** | (a) unreachable |
| `HelloMessages.cs:22-26,43,341,378-460,465-482`, `capability-selection.schema.json:14-19,32-55`, `fixtures/hello/valid-hello-ack-downgraded.json` | runner KEEP / contracts REMOVE | **KEEP as v1 vocabulary**. Remove only through a protocol ADR | spec only, safe for results |
| Retained-portfolio profiles: `Wp4RunnerConfiguration.cs:20-31,45,205,223-231,355-380,619`; `SolverBackedRidePoolingPolicy.cs:84,167,183,436-445`; `SolverExecutionEvidenceMapper.cs:13-15,38-47,72+` | runner KEEP_REQUIRED / contracts, adapter-scripts historical | **Downgrade to (b), recoverable from history**. REMOVE_OK only with divergence declarations and G1/G2 replay | (b) |
| `benchmarks/fixtures/wp6/public/**` | research-configs keep | **KEEP**, and exclude it from any wp6 archive | (c) |
| `fixtures/wp6/{e2e,metrics,planning}`, run-side `schemas/wp6/v1/{benchmark-plan,bundle-manifest,run-record,metric-row,observation-index-row,failure-record}` | research-configs KEEP / benchmarking ARCHIVE | **ARCHIVE_OK** | (b), recoverable from history |
| Data-side `schemas/wp6/v1/{scenario-content,normalization-report,dataset-descriptor,exclusion-record,common}` | as above | **KEEP** | (b) format of (c) data |
| `tests/RideBound.Benchmarking.Tests`: run-side files, about 5,482 lines | research-configs KEEP / benchmarking ARCHIVE | **ARCHIVE_OK** | (b) |
| Same project, data-side files, about 1,289 lines: `FleetPyManhattanNormalizerTests` 357, `DatasetDownloaderTests` 394, `SafeZipExtractorTests` 350, `FleetPyPublicDerivativeTests` 99, `TestDirectory`, csproj | as above | **KEEP** | (b) provenance |
| `tools/RideBound.Wp6MetricOracle` (including `BreachCount`) | contracts keep BreachCount / benchmarking ARCHIVE | **ARCHIVE_OK, but only together with `MechanicalMetricCalculator`**, plus divergence declarations | (b) |

## (1) CandidateHotPathBenchmark

- Neither `docs/19:747` nor ADR-052 needs to stay re-runnable at HEAD.
  - ADR-052 makes no speed claim (`docs/18:4014,4024-4025`).
  - Four of ADR-052's six "semantic counters" (work units, evaluated, feasible before cap, omitted) are already pinned at HEAD in `tests/RideBound.Algorithms.Tests/Candidates/CandidateSearchWorkProfileTests.cs:22-24` (468/450/451/0; 3108/3060/3061/0; 10000/9908/9909/1194). These are the same numbers as report §4 of `docs/benchmarking/post-wp10-exact-reuse-optimization-2026-08-23.md`. That test does not pin the "Retained" counter (100), and its last column is the slack-miss counter, not "Retained".
- The allocation figure (40,000,072 → 28,000,072 bytes) is measured only by the tool; `git grep 28_000_072` finds no test.
- The tool can be recovered from history. `git log` on the tool shows only `2d6791f`. `ForwardSlackProfile.cs` was last changed in `2d6791f`.
- The application area's claim is wrong.
  - The six recorded JSON hashes (report lines 55-62) belong to files kept outside the repo at `E:\RideBoundData\optimization`. Those files include wall-clock fields (`WallTimeP50Micros` in `Program.cs`), so no rerun at any commit can reproduce them byte for byte.
  - The hashes therefore constrain no source code.
  - `Program.cs:266-271` is just one more caller of the 5-argument constructor (`OnlineState.cs:19-24`), which `EventReducer.cs`, `OnlineStateCheckpointCodec.cs` and 16 test files also call.

## (2) Capability downgrade

- **Runner side, unreachable (a):**
  - `RunnerHost.cs:11-21` (`RunnerDefaults`) never sets `Downgrade`.
  - `CapabilityDowngradeProfile` is only ever constructed in `CapabilityNegotiatorTests.cs:92`.
  - `git log -S "new CapabilityDowngradeProfile" -- src` returns nothing.
- **No recorded data uses `downgraded`:**
  - `git grep -l '"downgraded"'` returns only the schema, the fixture and `HelloMessages.cs`.
  - The FleetPy adapter rejects any status other than accepted (`runner_client.py:165-166`).
  - The RidePy session hardcodes `accepted` (`session.py:118-127`).
  - No freeze pins `HelloMessages.cs` or the v1 schemas.
  - The encoder writes `downgradePolicyId` only when it is non-null (`HelloMessages.cs:480-482`), so the bytes for an accepted selection would not change.
- **Decision: keep the Contracts vocabulary.** It is normative v1 text (`docs/06:423-433`, also `:313,:672`; `docs/19:372`). `docs/06:12-14` says a patch release must not change canonical semantics. Changing it needs an ADR plus updates to `docs/06` and `docs/19` (`AGENTS.md:13-16`). Removing it later through that route is still safe for results.

## (3) Retained-portfolio evidence profiles

- **The suggested check passes.** `git show 38d517d:…/DecisionMessages.cs | sha256sum` gives `22dd15af…d0c8` (60,359 B), which equals `freeze-receipt-v1.json:340-342`. HEAD gives `59769c30…d141` (62,101 B). The only change since `38d517d` is `c6588f8` (G2).
- **All 62 E1 pins:**

| Commit | Result |
|---|---|
| `38d517d` | 41 exact plus 21 that match only as CRLF bytes (the wp9-confirmatory veh4 driver files and `execution-plan-panel-b`). Git stores them as LF, so a clean checkout needs those 21 converted to CRLF. |
| `41a4e11` | Only `Wp4RunnerConfiguration.cs` differs |
| `c6588f8~1` | 4 differ |
| HEAD | 8 differ: the preflight and verify scripts, 3 policy files, `DecisionMessages.cs`, `Wp4RunnerConfiguration.cs`, `SolverExecutionEvidenceMapper.cs` |

  The E1 Runner is also sealed as a binary: `runnerDllSha256 fdc58c35…`, tree `c367a71f…` (`docs/benchmarking/wp13-009-…:87-88`).
- **WP14 v2 (46 pins) is wider than adapter-scripts said.** The pins hold at every commit from `41a4e11` to `c6588f8~1`, not only `41a4e11..8d1ea4d`. The only commits after `41a4e11` that touch pinned paths are `c6588f8` and `9ba5568`. Its Runner is the retained binary `E:\RideBoundData\wp14\runner-v1`, not HEAD source.
- **The repository already has a recovery-commit policy.** `benchmarks/scenarios/wp13-e1/source-divergence-v1.json` says frozen files may change at HEAD if each change is declared and still recoverable from git (`recoveryCommit 38d517d`). `tests/test_wp13_e1_source_divergence.py` enforces it, and the same pattern is used in `wp14r-development/source-divergence-v2/v3/v5.json`.
- **The declaration is already stale.** It lists 1 of the 8 drifted files, so `test_undeclared_frozen_files_still_match_the_working_tree` cannot pass at HEAD.
- **The final workflow does not use the profile.**
  - Only 4 configs set `solverExecutionEvidenceProfile` (2 for WP13 E1, 2 for WP14).
  - The kit configs omit it, for example `kits/bridge-phase1-2026-10-23/configs/wp4-g2-costfirst-k8.json`.
  - ADR-089 (`docs/18:5902`) only toggles `emitSolverExecutionEvidence`. The final v2 configs are external, so this is "unknown" but nothing points to the profile.
  - Removing the profile cannot change any config hash, because `ContentHash` is the SHA-256 of the canonical input JSON (`Wp4RunnerConfiguration.cs:292-296`).
- **Conditions for removal:**
  - Complete the E1 divergence declaration (all 8 files).
  - Add a WP14 declaration with a recovery commit.
  - Re-pass the G1/G2 replay gates, because the capture branch sits in the hot path (`SolverBackedRidePoolingPolicy.cs:436-445`).

## (4) research-configs vs benchmarking

- **`public/**` is needed by the thesis numbers.** The emulator reads `wp9-confirmatory-fixed-panel-v2` and `wp14-development-panel-v1` (`company-emulator/sp/wf20/fullsys/code/runner/lib.js:8,119`; same in wf17 and wf21). `AGENTS.md:31` forbids deleting it.
- **The run-side fixtures have no consumers outside the WP6 harness.** `e2e/tiny/paired.input.ndjson`, `metrics/mechanical-metric-registry-v1.json` and `planning/seed-vectors.json` are read only by `TinyPairedHarness.cs`, `TinyProtocolFixtureCompiler.cs`, `MechanicalMetricCalculatorTests.cs`, `StrictBundleTestFixture.cs`, `BenchmarkSeedTests.cs` and `Wp6SeedVectors/Program.cs`. No freeze receipt, evidence summary, adapter or kit references them (`git grep`).
- **No freeze seals `schemas/wp6`.** Only the `wp14r/v1` schema tree is sealed (`wp14r_resource_dimension.py:283-295`). Archiving the run-side schemas means editing `schema-inventory.json`, which `SchemaContractTests.cs:13,37,76` reads.
- **The data-side tests protect thesis inputs.** They cover the C# normalizer that generated the Manhattan fixtures (`kits/tang3/calib/panelA/normalize_panelA.py:23` calls `tools/RideBound.Wp6Normalize`). The Via converter does not use it (`convert_via_zone.py:37`).
- **WP9 constraint.** WP9 freeze v6 pins three files: `DecisionInducedBurdenCalculator.cs`, `ExperimentalUnitModels.cs` and `DecisionInducedBurdenOracle.cs` (`wp9_freeze_verify.py:81-89`).
  - All three are still byte-identical at HEAD (`878efa2d…`, `38f98539…`, `4d7f1b64…`).
  - But WP9 v6 already fails at HEAD on 3 other pins.
  - Its verifier hash `be990614…` matches no committed version of `wp9_freeze_verify.py` (only `e584a66` and `2d6791f` exist).
  - So WP9 verification already relies on external archives, not HEAD. Archiving is fine once a WP9 divergence declaration names a recovery commit, for example `ad470c1`.

## (5) BreachCount

- **The dependency runs the other way.** Benchmarking depends on Contracts, not the reverse.
  - The Contracts code for these action types (`DecisionMessages.cs:7,13,69,75-76`; `OnlineDecisionActions.cs:93-99,170-185`; `CommitmentContracts.cs:45`) never references the oracle.
  - The Runner never constructs `CommitmentBreachDeclaredActionPayload`.
  - The adapter rejects both action types (`protocol.py:326-327`), so `commitment.breach.count` is 0 in every recorded run.
- **`BreachCount` is the oracle half of a pair.** The other half is `MechanicalMetricCalculator.cs:268-270`. They are checked against each other by `Wp6SourceAuditTests.cs:50` and `MechanicalMetricCalculatorTests.cs:185-222`.
- **Resolution:** keep it if and only if the calculator stays; archive both together. Contracts can keep `offerProposed` and `commitmentBreachDeclared` as v1 vocabulary on their own, for the same reason as (2).
- **The oracle also holds historical pins**, so archiving the whole tool needs declarations for both:
  - `DecisionInducedBurdenOracle.cs` (WP9 v6).
  - `OracleJson.cs` (`wp13_full_audit.py:85`).

## Classification changes

- The CandidateHotPathBenchmark tool moves from KEEP to (b), recoverable from `2d6791f`.
- The Runner's downgrade branch becomes (a); the Contracts downgrade vocabulary is kept.
- The retained-portfolio profiles move from KEEP_REQUIRED to (b), recoverable from `38d517d` (E1) and `41a4e11..c6588f8~1` plus the retained Runner (WP14).
- `fixtures/wp6` and `schemas/wp6/v1` split into data-side (keep, (b)/(c)) and run-side (archive, (b)).
- `Wp6MetricOracle` can be archived only as a unit with `MechanicalMetricCalculator`.

Before anything is removed: fix `source-divergence-v1.json`, add WP14 and WP9 divergence declarations, and treat the G1/G2 replay gates as the pass condition for any Runner-side removal.

The check scripts are in `<scratchpad>\` (`checkfreeze2.js`, `checkwp14.js`, `wp9check.js`).