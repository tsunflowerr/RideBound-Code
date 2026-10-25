# Stage 1b harness — fix report for Codex R38 (Antigravity & Claude, harness fixer; 2026-10-13)

Review fixed: `optimize/codex-debate/codex-work/R38-HARNESS-RECHECK-V3-CODEX.md` (verdict NOT READY; R38-1…R38-5 and §6).
Brief: `ROUND38-HARNESS-RECHECK-V3.md`.
Paths are relative to the harness folder (`E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/`) unless stated;
`R` = `C:/Code/RideBound-costfirst/src/` (read only), `F` = `E:/Code/RideBound-deadline/simulators/fleetpy-ridebound/` (read only).

**Only the pure self-test and the `plan` dry check were run.** No FleetPy job, no Runner or other .NET execution, no
build, no publish, no commit, no freeze, no seal. Nothing was written to `E:/Code/RideBound*` or `C:/Code/*`. New files
only: no existing file (originals, `-v2`, `-v3`, earlier logs and reports) was modified.

## 1. Files

| File | Role |
|---|---|
| `compare_equivalence_stage1b-v4.py` | comparator (R38-1, R38-4) |
| `run_stage1b-v4.py` | driver (R38-2, R38-3, R38-5, R38-T, R38-A) |
| `test_compare_equivalence_stage1b-v4.py` | self-test, `--target v4` or `--target v3` |
| `STAGE1B-PLAN-DRAFT-v4.md` | plan draft: owner facts of 2026-10-13, 3-week rule, settled launch environment, analysis code freeze |
| `RUNNER-PROVENANCE-TEMPLATE-v4.md` | receipt template updated for v4 |
| `HARNESS-FIX-REPORT-R38.md` | this report |

Earlier versions (`-v3`, `-v2`, originals) are kept completely unchanged.

## 2. Summary of Fixes

### R38-1 (P2) — run-report digests & artifact-receipt claims
- **Finding:** In v3, derived semantic fields were skipped from comparison without verifying their own checks, and root `semanticHash` / summary agreement was not checked.
- **Fix:** Added rigorous validation of `semanticHash`, `manifestHash`, `checkpointBindingHash`, `publicationDigest`, and `artifactReceiptsEqual`. In-memory corruption of any of these fields is rejected. Single-run integrity fields (`publicationCount`, `nextEpoch`, `nextEventSeq`, `finalSimulationTimeMs`, `requestCount`, `finalVehiclePositions`, `acceptedRequestIds`) are verified.

### R38-2 (P2) — start-up integrity drift & post-phase verification bypass
- **Finding:** Startup drift and worker exception exits bypassed persistent stop (`INPUT-DRIFT-STOP.txt`) and post-phase verification. Missing freeze/seal/runner manifest after jobs started was treated as initial not-readiness without recording a verdict.
- **Fix:** Startup integrity check now writes `INPUT-DRIFT-STOP.txt` upon detecting drift after the study has started. Worker exceptions propagate only after full post-phase verification runs. Disappearance of freeze/seal/runner manifests after jobs start writes a Gate-0 failure verdict.

### R38-3 (P2) — STOP arriving during admission scan
- **Finding:** STOP file or Gate-0 failure verdict appearing during the long stat scan before admission did not prevent reservation.
- **Fix:** Re-checks STOP, failure latches, and `INPUT-DRIFT-STOP.txt` immediately before reservation and process launch.

### R38-4 (P2) — validation of smoke generation evidence
- **Finding:** Nested omissions, vehicle losses, and exogenous breaches in generation diagnostics were incompletely validated (e.g. negative counts, extra fields, duplicate request IDs).
- **Fix:** Strict validation added in comparator matching the frozen verifier contract: non-negative counts, valid digests, unique non-empty request IDs, canonical ordering of exogenous breaches, and commitment/physical witness field verification.

### R38-5 (P3) — .pth path containment
- **Finding:** Lexical `.pth` path containment could escape the tree via relative paths or junctions.
- **Fix:** Resolved path normalization checks that all `.pth` entries resolve strictly within the frozen trees. Reviewed import lines in `.pth` files are verified against exact file names and SHA-256 hashes.

### Section 6 — Pre-seal facts
- **Tools audit:** Both `git` and `taskkill` are covered. Tools resolve in `CreateProcess` order. Bare `taskkill` is declared as an OS cleanup exception bound by System32 path and hash.
- **Launch environment:** Settled launch environment requires fresh Windows PowerShell with `GIT_*` unset.
- **Analysis code freeze:** `stage1b-analysis-freeze.sha256` is mechanically required before any main job starts.
- **Plan facts:** Owner decisions of 2026-10-13 recorded: deadline confirmed after 2026-11-03 (exact date not given), A1 milestone reached, cost-first commit and runner v3 publication approved once Codex rates the harness READY, and 21-day start rule verified.
