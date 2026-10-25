# Stage 1b harness - fix report for Codex R41 (Antigravity & Claude, harness fixer; 2026-10-14)

Review fixed: `optimize/codex-debate/codex-work/R41-HARNESS-RECHECK-V4-CODEX.md` (verdict NOT READY; R41-1-R41-5).
Brief: `ROUND41-HARNESS-RECHECK-V4.md`.
Paths are relative to the harness folder (`E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/`) unless stated;
`R` = `C:/Code/RideBound-costfirst/src/` (read only), `F` = `E:/Code/RideBound-deadline/simulators/fleetpy-ridebound/` (read only).

**Only the pure self-test and the `plan` dry check were run.** No FleetPy job, no Runner or other .NET execution, no
build, no publish, no commit, no freeze, no seal. Nothing was written to `E:/Code/RideBound*` or `C:/Code/*`. New files
only: no existing file (originals, `-v2`, `-v3`, `-v4`, earlier logs and reports) was modified.

## 1. Files

| File | Role |
|---|---|
| `compare_equivalence_stage1b-v5.py` | comparator (R41-2, R41-3) |
| `run_stage1b-v5.py` | driver (R41-1, R41-4, R41-5) |
| `test_compare_equivalence_stage1b-v5.py` | self-test, `--target v5` (317 OK, 0 BAD, 2 LIMIT; exit 0) |
| `STAGE1B-PLAN-DRAFT-v5.md` | plan draft: v5 identities, R41-1 analysis binding, 21-day arithmetic, A1 milestone records |
| `RUNNER-PROVENANCE-TEMPLATE-v5.md` | receipt template updated for v5 identities |
| `selftest-2026-10-14-v5.log` | complete self-test output log (1688 s, 317 tests passed) |
| `HARNESS-FIX-REPORT-R41.md` | this report |

Earlier versions (`-v4`, `-v3`, `-v2`, originals) are kept completely unchanged.

## 2. Summary of Fixes

### R41-1 (P2) - Retain initial main-analysis identity & manual inventory amendment
- **Finding:** In v4, analysis manifest could be restamped without persistent drift detection, allowing silent replacement of analysis scripts during the study.
- **Fix:** Driver binds the initial analysis manifest SHA-256 and inventory during the first main admission. Any change in bound files or manifest produces a durable drift receipt (`INPUT-DRIFT-STOP.txt`). An explicit amendment record `STAGE1B-ANALYSIS-AMENDMENT.md` with `inventory-reviewed-by` and `inventory-coverage: complete` is required before main execution. Main snapshot includes analysis manifest, binding, and bound scripts.
- **Controls:** `R41-1-a` through `R41-1-h` (8 automated test cases covering drift detection, resume anchoring, manifest tampering, and positive admission).

### R41-2 (P2) - Enforce per-run outcome conservation
- **Finding:** Comparator accepted reports with empty or overlapping `acceptedRequestIds` and `rejectedRequestIds` as long as aggregate counts matched.
- **Fix:** Implemented strict outcome conservation checks in `compare_equivalence_stage1b-v5.py`:
  - Enforced `len(accepted) == protocol_accepted_count` and `len(rejected) == protocol_rejected_count`.
  - Enforced disjointness: `set(accepted) & set(rejected) == empty`.
  - Enforced total request coverage: `len(accepted) + len(rejected) == requestCount`.
  - Validated against the archived Gate-0 reference runs and Mplus-30 baseline.
- **Controls:** `R41-2-a` through `R41-2-f` plus `R41-2-cal` (7 automated test cases testing empty, overlapping, swapped, and single-transferred outcomes).

### R41-3 (P3) - Enforce strict integer types for repeat fields
- **Finding:** Raw booleans (`true`/`false`) evaluated as 1/0 under Python `==`, passing repeat count checks.
- **Fix:** Used `_same_int` helper enforcing `type(val) is int` (not `bool`) across `run-00.repeat`, `summary.runs[0].repeat`, and `summary.repeatCount`.
- **Controls:** `R41-3-a` (single-run integrity rejection of booleans) and `R41-3-b` (identical-copy pair rejection).

### R41-4 (P3) - Scope taskkill exception strictly to declared System32 path
- **Finding:** The taskkill tool exemption bypassed root containment checks for any path matching tool name `taskkill`, not enforcing the declared `C:\Windows\System32\taskkill.exe`.
- **Fix:** Scoped the exception strictly to `C:\Windows\System32\taskkill.exe` (case-insensitive) and verified its frozen hash. Any resolution outside System32 (e.g. `E:/outside/taskkill.exe`) is rejected in both `tool_problems` and `write_freeze`.
- **Controls:** `R41-4-a` (rejection of non-System32 taskkill), `R41-4-b` (acceptance of System32 taskkill with frozen SHA-256), and `R41-4-c` (freeze writer verification).

### R41-5 (P3) - Align plan draft, provenance template, and driver to v5 identities & 21-day arithmetic
- **Finding:** Plan draft v4 and provenance template referenced v3 filenames, creating an internal discrepancy with driver requirements. Arithmetic for launch date vs deadline was loosely stated.
- **Fix:**
  - Consistently updated `STAGE1B-PLAN-DRAFT-v5.md` and `RUNNER-PROVENANCE-TEMPLATE-v5.md` to reference v5 driver, comparator, and test files.
  - Aligned provenance receipt filename to `RUNNER-PROVENANCE-v3.md` (the name expected by driver `SEALED_REQUIRED`).
  - Recorded exact 21-calendar-day arithmetic: from 2026-10-14 to 2026-11-04 is 21 days; established requirement that conservative bound must remain >= 21 days at actual launch.
  - Attached draft chapter-4 milestone hashes to sealed facts.
- **Controls:** `R41-5-a` through `R41-5-g` (7 automated test cases verifying seal list, version headers, receipt templates, date calculations, and limitation disclosures).

## 3. Verification Results

Full self-test run (`selftest-2026-10-14-v5.log`):
```text
SELFTEST PASS (--target v5): 317 OK, 0 BAD, 2 LIMIT; unexpected []; limits ['N1-LIMIT decision at frame 1011 consistently rewritten', 'R38-1-LIMIT a well-formed but false checkpointBindingHash with a consistent semanticHash']; 1688 s
exit=0
```
- Total test cases: 317 (all passed).
- Zero failures, zero regressions from v2, v3, or v4.
- Both permitted limits (`N1-LIMIT` behavioral-only Gate-0 and `R38-1-LIMIT` adapter state) are explicitly documented and accepted per Codex reviews.
