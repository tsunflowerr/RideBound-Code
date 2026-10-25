# Stage 1b analysis amendment — R46 corrections

Status: FINAL REVIEW CANDIDATE, 2026-10-15, BEFORE any MAIN admission. The Gate-0 and smoke jobs had already finished on 2026-10-14; this is not a claim that the study has no prior output. This amendment replaces the unsealed analysis draft only. It does not change STAGE1B-PLAN.md, its 17-file seal, the driver/comparator, threshold predictions, trial panel, archived baselines, or experimental authority. A main admission remains prohibited until the final self-test and inventory review are recorded and the analysis manifest is written.

## A. Local inventory to freeze in the harness

| Bare file name | Role |
|---|---|
| extract_stage1b.py | Executed extractor, containing all external source/data/SF bundle pins and shared study-state authentication |
| report_stage1b.py | Executed reporter; authenticates and compiles the bound extractor source for the shared state checker |
| runner-tier3-v3.sha256.tsv | Sealed data manifest; expected CF DLL and prerequisite receipt build links |
| STAGE1B-ANALYSIS-AMENDMENT.md | This final reviewed record, installed under the driver's required bare name |
| test_analysis_stage1b-v3.py | Full numerical, boundary and refusal self-test evidence |
| analysis-selftest-2026-10-15-v3.log | Full self-test output identifying exact source/test bytes |
| test_extract_r46_regressions.py | Independent cache/data/stop/interpreter failure regressions |
| extract-r46-regressions-2026-10-15-v2.log | 19/19 targeted extractor checks, final source bytes |
| test_report_r46_regressions_v2.py | Independent mode/source/nonfinite/accounting/state failure regressions |
| report-r46-regressions-v2-run1.log | 18/18 targeted report checks, final source bytes |

The first two scripts and runner manifest are runtime inputs. The amendment/tests/logs are retained review/evidence inputs: the shared state guard checks EVERY bound file, although the tests/logs do not execute during main analysis. The original scratch development copies, self-test work folders, counterexample outputs, earlier source versions/reports and PDF review are preserved on C and are not runtime dependencies. Review reports may be kept beside the manifest as documentation; they do not introduce code dependencies.

## B. External source and data closure

The extractor retains the original ten PINNED_MODULES, four PINNED_DATA files (split and factor CSVs 12/13/14-Nov), and all 68 SF_BUNDLE_PINS. Their SHA-256 values remain unchanged from the original draft/R46 review. The published runner manifest is additionally pinned to ed69d23e953447eb62e416a0b23a575d319e8ee1647e9d8ee5a4a14155864587, the exact sealed file.

All ten direct/transitive helpers execute source bytes read and SHA-verified BEFORE compilation, via the dedicated source loader. Timestamp-valid or hash-valid adjacent bytecode cannot substitute for those bytes. Preloaded pinned modules that bypassed this loader are refused. Newly imported external helper modules must belong to the pinned closure or the frozen interpreter environment/tree (sys.base_prefix/sys.prefix); observed runtime imports belong to the standard library, but the code's admitted trust boundary is the entire frozen environment.

Forecast source CSVs are read once into immutable verified byte buffers in each analysis worker. The UNCHANGED v1.3 CSV parser/profile arithmetic reads through a read-only path facade serving those buffers. It does not reread mutable CSV contents for later trajectories. The open audit records the logical pinned paths of these memory reads. Final source/data pin verification additionally refuses persistent drift, but is not the mechanism that authenticates transient CSV consumption. The split and archived SF bundle files retain their existing content/length/identity checks.

CF main outputs cannot be hashed before they exist. They retain the original expected-run identity, bundle checks, 108-request checks, cross-role episode/request-set equality, admission anchors and job-log checks. Future coordinated replacement of run data together with self-authenticating bundle manifests remains a disclosed limitation; no stronger attestation is claimed. Middle decisions retain the sealed behavioural-only gate scope.

## C. Current state and result mode

Before main extraction, after all final source/data/CF layout/log/anchor scans and immediately before first output creation, the same state checker verifies prerequisite receipt CONTENTS, sealed Runner DLL/tree links, the smoke inspection's actual smoke-verdict hash/acceptance/inspector, absence of failure/drift latches, the retained binding/manifest and EVERY listed analysis file. It compares the current snapshot to the one held at extraction admission. Prerequisite drift is refused even when replacement text is itself a structurally valid pass receipt.

The reporter uses that same implementation by hashing and compiling the bound extract_stage1b.py source bytes; ordinary project import/cache lookup is not used. It compares current state to the extraction snapshot before analysis and immediately before reporting output. A new durable stop or changed retained evidence refuses output. Both programs are read-only on study evidence: they do not clear stops or write new drift-stop receipts themselves.

Report metadata accepts exactly mode=main with dryRun=false and the four real sources, or mode=dryrun with dryRun=true and the permitted archived stand-ins. Types and every retained run.source must match roleSources[role]. Unknown/inconsistent modes, missing/source-mismatched runs and main results claimed from stand-ins are refused. Archived numerical reproduction is always labelled DRY RUN and cannot become a Stage-1b result through one flag edit.

## D. Definitions resolved before freeze

Primary/guard arithmetic, strict >60/120-second lateness, populations and predictions remain unchanged from the sealed plan. No thresholds were selected from new main outcomes; no main outcomes exist at this review.

1. Initial promise horizon means p0−t0, not realised onboard duration or forecast extension. The main median cohort is every accepted rider having a first drop promise, using nearest rank (lower middle for an even sample); the served-cohort median is also reported. This records the convention already used by the original scripts. Empty/nonfinite comparisons are explicitly NOT EVALUABLE and never reported as a met guard.
2. H3 late percentages are among served riders and require a POSITIVE served denominator. Zero served leaves H3 NOT EVALUABLE; all counts and failed service guards remain visible.
3. Served means lifecycle completed AND an alight event. To reconcile the literal completed wording of H7 with its served-rate context, MAIN analysis refuses completed riders without alight, making those two cohorts identical before H7 is evaluated. Dry runs keep the original anomaly diagnostics. The max-single≤120s<sum criterion and ≥10 threshold do not change.
4. H1 retains the original C30 overshoot condition and pending explanations for non-normal decisions/non-exogenous breach entries. Those are different record units. Ledger budgetBefore/budgetAfter arithmetic discrepancies are a SEPARATE accounting validity failure across ALL roles; recovery explanations cannot waive them. Such a failure stops implementation validity reporting before CF result tables.
5. Five days remain five dependent day clusters, not 34 independent replications. Cell/day patterns and descriptive leave-one-day-out contrasts remain; no confidence interval or population guarantee is introduced.

## E. Commands and output boundaries

Run both commands from the ORIGINAL harness after all 68 main jobs and the driver post-phase verification succeed:

    E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B extract_stage1b.py main C:/RideBoundContinuation/2026-10-15/results/stage1b
    E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B report_stage1b.py C:/RideBoundContinuation/2026-10-15/results/stage1b C:/RideBoundContinuation/2026-10-15/results/stage1b-report.txt

The output directory must be created beforehand on C. Output files use exclusive creation; no result is overwritten. C:/RideBoundData and E:/RideBoundData remain forbidden output roots for analysis, protecting input artifacts. The extractor's main command requires the frozen job interpreter. Both commands are prescribed with that interpreter. Its standard library/native code and the OS remain trusted through the existing execution freeze; the scripts do not claim to close operating-system or deliberately coordinated-receipt trust.

## F. Review and evidence record (to be completed before seal)

The original analysis was written by Claude; Codex R46 reviewed its exact bytes on 2026-10-14. This continuation's changes, two implementation roles, their exchanged reviews and final independent judge are Codex. No new Claude/Antigravity review is asserted. R43 Antigravity Chapter-1 review remains pending and is unrelated to admitting analysis.

Current script SHA-256:

- extract_stage1b.py: 6ff7f303f51a36d30afd22ea2c40c66290e8b9c1ee5a361461e2dca57083429c
- report_stage1b.py: ecbec711715a75f70a4dc5632b7978821e7c9b9e9767edfa84c5f07d00bdfc12

Full self-test v2: 189/192, with all 1,836 raw numerical comparisons against independent Codex R19 passing. Three stale refusal-message fixtures were updated to reach the intended stricter checks; they were not removed. Full self-test v3: 192/192 passed, 0 failed, exit 0; log analysis-selftest-2026-10-15-v3.log records exact source/test hashes. All 1,836 raw numerical comparisons with R19 passed. The final judge verifies this record before installation/freezing. The 19/19 and 18/18 specialised regressions were also rerun independently by the final judge.

inventory-reviewed-by: Codex coordinator; Codex extractor implementer (/root/fix_extract); Codex report implementer (/root/fix_report); Codex independent judge (/root/thesis_read_review)
inventory-coverage: complete
