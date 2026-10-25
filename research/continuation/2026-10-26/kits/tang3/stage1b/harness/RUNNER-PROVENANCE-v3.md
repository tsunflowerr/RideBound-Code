# Runner provenance receipt: runner-tier3-v3 (filled 2026-10-14 by the coordinator from RUNNER-PROVENANCE-TEMPLATE-v7.md)

Purpose: the separate source-to-runner record required by Codex R22 F3 and §7.2
(`optimize/codex-debate/codex-work/R22-CODE-REVIEW-CODEX.md:52-58,246-250`). The `initializeRun.coreCommitSha` of every
Stage-1b job is the HEAD of the **harness** root (`E:/Code/RideBound-deadline`), not the source of the Runner DLL. The
DLL identity in `initializeRun.payload.manifest.binarySha256` is the SHA-256 of the Runner DLL listed below. Every value
was recorded from command output on 2026-10-14 (UTC+7); the logs named below hold the full output.

## A. Runner source (facts about the build input)

| Field | Expected / how obtained | Recorded value |
|---|---|---|
| Worktree | `C:/Code/RideBound-costfirst` | `C:/Code/RideBound-costfirst` (gitdir `E:/Code/RideBound/.git/worktrees/RideBound-costfirst`) |
| Branch | `research/tier3-cost-first` | `research/tier3-cost-first` |
| Base commit | `761915cbb89d03f410cd9989ad1cf136e6234bf8` | `761915cbb89d03f410cd9989ad1cf136e6234bf8` (parent of the runner-source commit) |
| `src/` at base equals the runner-tier3-v2 source | `git diff --stat 45f8026 761915c -- src simulators tools` prints nothing | printed nothing (2026-10-14) |
| Reviewed diff | `tang3/stage1b/costfirst-diff-2026-10-11.patch`, 1,035 lines, SHA-256 `c48042af4e7cee2db01bdee5efb804dac703d80c8c657276fc5718816e1ad690` | file present, 1,035 lines, SHA-256 `c48042af4e7cee2db01bdee5efb804dac703d80c8c657276fc5718816e1ad690` |
| Runner-source commit | `git -C C:/Code/RideBound-costfirst rev-parse HEAD` | `dce84ec4e51b9b7489402d81bb0dda28afbda0a0` ("Add an opt-in cost-first selection objective order for the Stage 1b study (ADR-078, proposed)") |
| Commit reproduces the reviewed diff | `git -c core.autocrlf=false diff 761915c dce84ec \| sha256sum` | `c48042af4e7cee2db01bdee5efb804dac703d80c8c657276fc5718816e1ad690`; equal: yes. The same hash was also obtained for the uncommitted working diff immediately before the commit. |
| Clean tree at publish | `git status --porcelain` prints nothing | printed nothing before and after the publish |
| Pushed | must be not pushed | not pushed: yes (no push was run; `git branch -r --contains dce84ec` prints nothing; remote `origin` = github.com/tsunflowerr/RideBound) |
| Tests at the commit | `dotnet test RideBound.slnx` on the clean committed tree; expect 1033 passed + the known clean-tree failure | 1,034 total: 1,033 passed, 1 failed = `StrictBagItBundleTests.Source_inventory_binds_dirty_worktree_files_not_only_base_commit` (known clean-tree failure); log `harness/dotnet-test-costfirst-dce84ec-2026-10-14.log`, SHA-256 `5b845e7bce2cb45fd80e7cef7b116b0041aafcb9bd87feb7251f4701ec54cc55` |

## B. Toolchain and publish

| Field | Expected / how obtained | Recorded value |
|---|---|---|
| SDK pin | `global.json`: `10.0.301`, `rollForward: latestPatch` | `global.json`: version `10.0.301`, rollForward `latestPatch` |
| `dotnet --version` (in the worktree) | `10.0.3xx` | `10.0.301` |
| `dotnet --list-sdks`, `dotnet --list-runtimes` | record both | SDK `10.0.301 [C:\Program Files\dotnet\sdk]`; runtimes `Microsoft.AspNetCore.App 10.0.9`, `Microsoft.NETCore.App 10.0.9`, `Microsoft.WindowsDesktop.App 10.0.9` (all under `C:\Program Files\dotnet\shared`) |
| Host | `C:/Program Files/dotnet/dotnet.exe`; frozen by `run_stage1b-v7.py freeze` | `C:/Program Files/dotnet/dotnet.exe`, SHA-256 `a5ccdc3a41d5e5c6014ff64509aed176db39f4f14caffff3dd1997f8907e94d7` (2026-10-14; the freeze records it again with host/fxr and Microsoft.NETCore.App) |
| Output root did not exist before publish | `Test-Path C:/RideBoundData/research/runner-tier3-v3` → False | False |
| Publish command (exact) | template §E | run from `C:/Code/RideBound-costfirst` in PowerShell: `dotnet publish src\RideBound.Runner\RideBound.Runner.csproj -c Release -o C:\RideBoundData\research\runner-tier3-v3 *> E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness\publish-runner-tier3-v3.log`; exit code 0 |
| Publish console log | file name + SHA-256 | `harness/publish-runner-tier3-v3.log`, SHA-256 `be43a1e78df7d4e5b3043fbcf055d3a709de94d90a552567df092a9c9aaab1b3` |
| Publish time (UTC) | | started 2026-10-14T05:40:58Z, finished 2026-10-14T05:41:21Z |

## C. Published tree (facts about the build output)

| Field | How obtained | Recorded value |
|---|---|---|
| Runner root | `C:/RideBoundData/research/runner-tier3-v3` | `C:/RideBoundData/research/runner-tier3-v3` |
| Tree manifest | `python -B tang3/runner_manifest.py write …` (mode "x") | `tang3/stage1b/harness/runner-tier3-v3.sha256.tsv`, written 2026-10-14 with `E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B` |
| Tree SHA-256 / file count | header line of the TSV | `d08c0057d8239c6c51cceb0d2ba2d7f41dc899a9ac085671c363ede388a0b4b8` / 429 |
| TSV file SHA-256 | `sha256sum runner-tier3-v3.sha256.tsv` | `ed69d23e953447eb62e416a0b23a575d319e8ee1647e9d8ee5a4a14155864587` |
| `RideBound.Runner.dll` SHA-256 / bytes | TSV line `RideBound.Runner.dll` | `1839a54413b2729cf353db7ef8b2359d32f4698709bba75a96eca492e36c69a9` / 198,656 |
| Differs from v2 DLL | must differ from `a631038b6ea4850ffa29be2d5e4058fba2e57b725ee568075686d7a43c6f5fac` | yes, it differs |
| File list vs v2 | compare the first columns of the two TSVs | identical (429 files each) |
| Files whose SHA-256 differs from v2 | list | 15 files: `RideBound.Algorithms.dll/.pdb`, `RideBound.Application.dll/.pdb`, `RideBound.Contracts.dll/.pdb`, `RideBound.Domain.dll/.pdb`, `RideBound.Infrastructure.dll/.pdb`, `RideBound.Runner.dll/.exe/.pdb`, `RideBound.Solvers.OrTools.dll/.pdb`. Every other file (third-party libraries and `runtimes/`) is byte-identical to v2. Not explained away: the changed source touches Algorithms and Runner only; the other `RideBound.*` files differ in their build output (new build). |
| Verification | `python -B tang3/runner_manifest.py verify …` → OK | `OK tree-sha256 d08c0057d8239c6c51cceb0d2ba2d7f41dc899a9ac085671c363ede388a0b4b8 files 429` |

## D. Harness (separate facts; NOT the runner source)

| Field | Value (frozen, R22:177) | Recorded check |
|---|---|---|
| Harness root (cwd of every job) | `E:/Code/RideBound-deadline`, never modified | `E:/Code/RideBound-deadline`; not modified by this work (read-only `rev-parse` and `status` only) |
| Harness commit = `coreCommitSha` in every initializeRun | `1ce15e92d2c1459d4254bbbcd581529197257fb2` | `git -C E:/Code/RideBound-deadline rev-parse HEAD` = `1ce15e92d2c1459d4254bbbcd581529197257fb2` |
| Repository inventory | `0da63eaf2b30ab332fba265a6b2b6e5c36b3ee78950cbac6d6267e60f81e4cbe` | the value passed to every job as `--expected-repository-inventory-sha256`; the job itself refuses a mismatch (as in T3.8) |
| World wrapper | `tang3/world/preflight_world_v1_2.py` SHA-256 `df28b6cb…a262` | `df28b6cb6adf2c94e5b539f137bda674b8498046d56fb0c5f1ab4ec1937ca262` |
| Driver / comparator / self-test | SHA-256 of the v7 files (also in `STAGE1B-PLAN.sha256`) | `run_stage1b-v7.py` `7495dd515154e81932ce61ea537f648739d940099b37425d0eb6b58f34e64811`; `compare_equivalence_stage1b-v7.py` `8fb8c6a614db3cedd43488e4525e6492c6b7ee95c68ee435162a308eb75929da`; `test_compare_equivalence_stage1b-v7.py` `7d2267df3d25f0cea1bc03955f85069416e57a723a5e80f73036cd4c522c44e7`; `selftest-2026-10-14-v7.log` `ce5a477aea535d95c05051308033ec773775e8b32571710b8943887da2dbacca` |

Statement (verbatim from the template): *"coreCommitSha `1ce15e9…` in the Stage-1b transcripts identifies the harness root.
The Runner DLL `1839a54413b2729cf353db7ef8b2359d32f4698709bba75a96eca492e36c69a9` (tree
`d08c0057d8239c6c51cceb0d2ba2d7f41dc899a9ac085671c363ede388a0b4b8`) was published from
`dce84ec4e51b9b7489402d81bb0dda28afbda0a0` on `research/tier3-cost-first` (base `761915c`, reviewed diff `c48042af…`).
These are separate facts."*

Manual review (R28:207): every value above was taken from command output in this session and checked against the
expectation column; no value differs from its expectation.
