# Runner provenance receipt — TEMPLATE v7 (copy to `RUNNER-PROVENANCE-v3.md` at publish time; never edit this file)

v7 of `RUNNER-PROVENANCE-TEMPLATE.md` (v1–v6 kept unchanged; v7, like v6, changes only the harness file names, now to
the v7 files; the v4 template was a byte copy of v3, Codex R41-5).
**One receipt file name:** the filled copy is `harness/RUNNER-PROVENANCE-v3.md`. The `-v3` names the **runner build**
(`runner-tier3-v3`), not the harness version; it is the file the v7 driver reads (`run_stage1b-v7.py` `PROVENANCE`) and
the file listed in the seal (`SEALED_REQUIRED`, plan v7 §13). The harness identities to record in §D are the **v7**
files: `run_stage1b-v7.py`, `compare_equivalence_stage1b-v7.py`, `test_compare_equivalence_stage1b-v7.py`,
`selftest-2026-10-14-v7.log`. Earlier changes (Codex R24 §3, `R24-HARNESS-REVIEW-CODEX.md:197,201`): the publish
snippet in §E starts with `Set-Location` so the SDK diagnostics run in the source worktree (global.json applies);
`run_stage1b-v7.py` refuses a receipt that still contains any backticked template field such as `` `<...>` `` (an
automated check of completeness only — the recorded facts still need manual review before the seal, R24:197). Delete
this header paragraph from the filled copy: its example field would itself be refused as unfilled.

Purpose: the separate source-to-runner record required by Codex R22 F3 and §7.2
(`optimize/codex-debate/codex-work/R22-CODE-REVIEW-CODEX.md:52-58,246-250`). The `initializeRun.coreCommitSha` of every
Stage-1b job is the HEAD of the **harness** root (`E:/Code/RideBound-deadline`, adapter `session.py:140-156,339-340`), not
the source of the Runner DLL. The DLL identity in `initializeRun.payload.manifest.binarySha256` is the SHA-256 of
`<runner root>/RideBound.Runner.dll` (`session.py:482-483`). This receipt binds that DLL and its full tree to the reviewed
source. `run_stage1b-v7.py` refuses to run unless the filled copy `RUNNER-PROVENANCE-v3.md` exists, contains the tree hash and the DLL SHA-256, and
has no unfilled template field.

Fill every `<...>`. Record command output verbatim (or as a log file with its SHA-256). If a value differs from an
expectation below, write the observed value and stop; do not adjust the expectation.

## A. Runner source (facts about the build input)

| Field | Expected / how to obtain | Recorded value |
|---|---|---|
| Worktree | `C:/Code/RideBound-costfirst` | `<...>` |
| Branch | `research/tier3-cost-first` | `<...>` |
| Base commit | `761915cbb89d03f410cd9989ad1cf136e6234bf8` (HEAD of `research/tier3-no-worse`; `IMPLEMENTATION-REPORT.md:8-10`) | `<...>` |
| `src/` at base equals the runner-tier3-v2 source | `git diff --stat 45f8026 761915c -- src simulators tools` prints nothing (checked read-only 2026-10-11: the 97 files changed between them are all under `docs/`; independently reproduced by Codex R24:199) | `<...>` |
| Reviewed diff | `tang3/stage1b/costfirst-diff-2026-10-11.patch`, 1,035 lines, SHA-256 `c48042af4e7cee2db01bdee5efb804dac703d80c8c657276fc5718816e1ad690` (`IMPLEMENTATION-REPORT.md:215-217`; Codex R22:21-28 verdict ACCEPT WITH CHANGES for exactly this snapshot) | `<...>` |
| Runner-source commit | `git -C C:/Code/RideBound-costfirst rev-parse HEAD` after the owner-approved commit | `<40-hex>` |
| Commit reproduces the reviewed diff | `git -C C:/Code/RideBound-costfirst -c core.autocrlf=false diff 761915c <commit> \| sha256sum` == `c48042af…` (the patch covers src, tests and the two docs) | `<sha256 and equal: yes/no>` |
| Clean tree at publish | `git -C C:/Code/RideBound-costfirst status --porcelain` prints nothing (v2 was published "từ `45f8026` với cây sạch", `tang3/sweep/SWEEP-AMENDMENTS.md:32-33`) | `<...>` |
| Pushed | must be **not pushed**: owner decision 2026-10-13 allows the worktree commit and the runner publication once Codex rates the harness READY, **no push** (`optimize/codex-debate/DEBATE-LEDGER.md`, "Owner decisions at 14:00"; Codex R41 §6) | `<not pushed: yes/no>` |
| Tests at the commit | `dotnet test RideBound.slnx` on the clean committed tree. Expect 1033 passed + the known clean-tree failure `StrictBagItBundleTests.Source_inventory_binds_dirty_worktree_files_not_only_base_commit` (`IMPLEMENTATION-REPORT.md:245-246`) | `<passed/failed/total; log file + SHA-256>` |

## B. Toolchain and publish

| Field | Expected / how to obtain | Recorded value |
|---|---|---|
| SDK pin | `global.json`: `10.0.301`, `rollForward: latestPatch` | `<...>` |
| `dotnet --version` (run in the worktree) | `10.0.3xx` | `<...>` |
| `dotnet --list-sdks`, `dotnet --list-runtimes` | record both; the published runner is framework-dependent `net10.0` (`runtimeconfig.json` of v2: `Microsoft.NETCore.App 10.0.0`); installed on 2026-10-11: `Microsoft.NETCore.App 10.0.9`, `host/fxr 10.0.9` (read-only listing) | `<...>` |
| Host | `dotnet` used by the jobs: `C:/Program Files/dotnet/dotnet.exe` (`run_pilot.py:31`); its SHA-256 and the `host/fxr` and `shared/Microsoft.NETCore.App` trees are frozen by `run_stage1b-v7.py freeze` | `<...>` |
| Output root did not exist before publish | `Test-Path C:/RideBoundData/research/runner-tier3-v3` → `False` | `<...>` |
| Publish command (exact) | see §E | `<...>` |
| Publish console log | file name + SHA-256 | `<...>` |
| Publish time (UTC) | | `<...>` |

## C. Published tree (facts about the build output)

| Field | How to obtain | Recorded value |
|---|---|---|
| Runner root | `C:/RideBoundData/research/runner-tier3-v3` | `<...>` |
| Tree manifest | `python -B tang3/runner_manifest.py write C:/RideBoundData/research/runner-tier3-v3 tang3/stage1b/harness/runner-tier3-v3.sha256.tsv` (mode "x"; format and tree definition `runner_manifest.py:1-6,17-33`) | `<...>` |
| Tree SHA-256 / file count | header line of the TSV | `<64-hex> / <n>` |
| TSV file SHA-256 | `sha256sum runner-tier3-v3.sha256.tsv` | `<...>` |
| `RideBound.Runner.dll` SHA-256 / bytes | TSV line `RideBound.Runner.dll` | `<64-hex> / <n>` |
| Differs from v2 DLL | must differ from `a631038b6ea4850ffa29be2d5e4058fba2e57b725ee568075686d7a43c6f5fac` (R22:196) | `<yes/no>` |
| File list vs v2 | `diff <(cut -f1 runner-tier3-v2.sha256.tsv \| tail -n +2) <(cut -f1 runner-tier3-v3.sha256.tsv \| tail -n +2)`; v2 has 429 files (19 top-level + `runtimes/` 4×100 + win-x64 10) | `<identical / list of differences>` |
| Files whose SHA-256 differs from v2 | list (expected at least `RideBound.Algorithms.dll/.pdb` and `RideBound.Runner.dll/.pdb`; other `RideBound.*` files may differ through embedded build paths — record, do not explain away) | `<...>` |
| Verification | `python -B tang3/runner_manifest.py verify C:/RideBoundData/research/runner-tier3-v3 tang3/stage1b/harness/runner-tier3-v3.sha256.tsv` → `OK` | `<...>` |

## D. Harness (separate facts; NOT the runner source)

| Field | Value (frozen, R22:177) | Recorded check |
|---|---|---|
| Harness root (cwd of every job) | `E:/Code/RideBound-deadline`, never modified | `<...>` |
| Harness commit = `coreCommitSha` in every initializeRun | `1ce15e92d2c1459d4254bbbcd581529197257fb2` (`git -C E:/Code/RideBound-deadline rev-parse HEAD`; `run_stage1b-v7.py plan` checks it) | `<...>` |
| Repository inventory | `0da63eaf2b30ab332fba265a6b2b6e5c36b3ee78950cbac6d6267e60f81e4cbe` (`run_w07.py:46`, passed as `--expected-repository-inventory-sha256`) | `<...>` |
| World wrapper | `tang3/world/preflight_world_v1_2.py` SHA-256 `df28b6cb…a262` (`W07-PLAN.sha256:6`) | `<...>` |
| Driver / comparator / self-test | SHA-256 of `run_stage1b-v7.py`, `compare_equivalence_stage1b-v7.py`, `test_compare_equivalence_stage1b-v7.py` and `selftest-2026-10-14-v7.log` (all four also in `STAGE1B-PLAN.sha256`) | `<...>` |

Statement to keep verbatim in the filled receipt: *"coreCommitSha `1ce15e9…` in the Stage-1b transcripts identifies the
harness root. The Runner DLL `<dll>` (tree `<tree>`) was published from `<runner-source commit>` on
`research/tier3-cost-first` (base `761915c`, reviewed diff `c48042af…`). These are separate facts."*

## E. Proposed publish command (to run only after owner approval of the commit and the publish)

```powershell
# 0. preconditions (record outputs in §A/§B); every command runs in the source worktree so global.json applies
Set-Location C:/Code/RideBound-costfirst
git status --porcelain                                         # must print nothing
git rev-parse HEAD                                             # the runner-source commit
Test-Path C:/RideBoundData/research/runner-tier3-v3            # must be False
dotnet --version; dotnet --list-sdks; dotnet --list-runtimes

# 1. publish (same form as the recorded Tier-2 publish, see provenance of this command below)
dotnet publish src\RideBound.Runner\RideBound.Runner.csproj -c Release -o C:\RideBoundData\research\runner-tier3-v3 *> E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness\publish-runner-tier3-v3.log

# 2. manifest (mode "x") and verification
E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe -B E:\Code\Report_INT3508\ridebound-scratchpad\tang3\runner_manifest.py write C:\RideBoundData\research\runner-tier3-v3 E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness\runner-tier3-v3.sha256.tsv
E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe -B E:\Code\Report_INT3508\ridebound-scratchpad\tang3\runner_manifest.py verify C:\RideBoundData\research\runner-tier3-v3 E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness\runner-tier3-v3.sha256.tsv
```

Provenance of the proposed command (searched 2026-10-11):
- The exact command used for `runner-tier3-v2` is **not recorded** in the scratchpad or in `docs/18`. The records give only
  the source commit, the clean tree, the tree hash and the file count: `tang3/sweep/SWEEP-AMENDMENTS.md:32-33`;
  `C:/Code/RideBound-noworse/docs/18-status-and-decision-log.md:5117-5118`. The same holds for `runner-tier3-v1`
  (`E:/Code/RideBound-deadline/docs/18-status-and-decision-log.md:5092`; `tang3/world/README.md:70-71`).
- The only recorded publish command for a research runner is the Tier-2 one:
  `session-2026-09-23-archive/tier2-v2-draft.md:352-356`,
  `dotnet publish src\RideBound.Runner\RideBound.Runner.csproj -c Release -o E:\RideBoundData\research\runner-deadline-v1`.
- The v2 tree layout matches that command form (framework-dependent, no runtime identifier): `RideBound.Runner.exe`
  apphost + `RideBound.Runner.dll` + `.deps.json` + `.runtimeconfig.json` (`net10.0`, framework `Microsoft.NETCore.App`)
  and native assets kept under `runtimes/{linux-arm64,linux-x64,osx-arm64,osx-x64,win-x64}/` (429 files,
  `tang3/runner-tier3-v2.sha256.tsv`). A `-r win-x64` publish would not keep the other runtime folders.
- This is therefore an inference, not a record. The file-list comparison in §C is the check: a different list means the
  command form differs from v2's and must be reported before any job.
