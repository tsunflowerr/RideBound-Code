# Brief: fix the Stage 1b harness per Codex R28 (2026-10-12)

Harness folder: `E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/`.

Review to address: `E:/Code/Report_INT3508/ridebound-scratchpad/optimize/codex-debate/codex-work/R28-HARNESS-RECHECK-CODEX.md`
(verdict NOT READY; findings N1–N8; §4 replay; §5 freeze wording). Your previous work: `HARNESS-FIX-REPORT-R24.md`
and the `-v2` files.

## Owner decision (2026-10-12, recorded by the coordinator)

On N1, the owner chose a **behavioural-only Gate 0, explicitly disclosed**. No .NET replay tool will be written.

**What changes in Gate 0.** It becomes:
- the recomputable digests that already pass (manifest, initial state, decision 1, checkpoints, final state and
  closure); plus
- exact equality on all behavioural comparisons; plus
- link and content checks for decisions 2..N.

**What the plan must say.** `STAGE1B-PLAN-DRAFT-v3.md` must state plainly that the criterion was amended from R22's
full-validity criterion, and why:
- the middle-decision digests (decisions 2..N) are not validated by the production .NET code;
- a protocol-invalid tape with identical actions, ledgers and frame content could therefore pass;
- results are reported as exploratory.

Use Codex's own wording where possible.

**Labels.**
- Rename the gate verdict and labels so they cannot be read as full protocol validity, for example
  "GATE0-BEHAVIOURAL-PASSED".
- Relabel the synthetic positives P2v/P3v as validity-limited (normalisation tests), as R28 N1 asks.

## Fix N2–N8

1. **N2 (P1).** Reject duplicate JSON fields. Use a strict parser (`object_pairs_hook` that raises), everywhere a frame,
   checkpoint, manifest or verdict is parsed. Add Codex's counterexample as a negative test: the same `checkpointHash`
   field duplicated with the same value, with refreshed frame SHA, length and receipt.
2. **N3.** Close the freeze's resolver scope as R28 describes, and make observed drift a durable failure latch.
3. **N4.** Frozen-input exceptions must write the failure verdict, not skip it.
4. **N5.** Recheck AC power immediately before each launch, after the admission queue and stat checks.
5. **N6.** Smoke evidence must include every tie objective, with valid numeric bounds.
6. **N7.** The self-test fallback must not hide a failed live runner-tree verification.
7. **N8.** The Unicode canonical fallback must apply the integer-range check.

Also adopt the R28 §5 freeze-limitation paragraph in the plan, verbatim or nearly so.

## Rules

- Write **new `-v3` files only**: `compare_equivalence_stage1b-v3.py`, `test_compare_equivalence_stage1b-v3.py`,
  `run_stage1b-v3.py`, `STAGE1B-PLAN-DRAFT-v3.md`, `RUNNER-PROVENANCE-TEMPLATE-v3.md` (only if it changes), and the logs
  `selftest-2026-10-13-v3.log`, `selftest-2026-10-13-r28-on-v2.log` (the new R28 tests run against the v2 files, to
  show they fail for the right reason) and `plan-dryrun-2026-10-13-v3.txt`.
- Never edit an existing file, including your own `-v2` files. If a `-v3` file needs another pass, write `-v4`.
- Run only the pure self-test and the dry run.
  - No FleetPy job, no Runner or .NET run, no build, no publish, no commit, no freeze, no seal.
  - Never write to `E:/Code/RideBound`, `E:/Code/RideBound-deadline` or `C:/Code/*`.
- Report: `HARNESS-FIX-REPORT-R28.md`, mapping each finding to its test, fix and result with file:line, plus log names
  and SHA-256 hashes, and the open items.
