# Stage 1b harness — report (2026-10-11, Claude, harness builder)

Folder: `E:/Code/Report_INT3508/ridebound-scratchpad/tang3/stage1b/harness/` (new; nothing outside it was written).
No FleetPy job, runner publish, build, test run of .NET, or commit was made. Anchors as in `STAGE1B-PLAN-DRAFT.md`
(`R22` = `optimize/codex-debate/codex-work/R22-CODE-REVIEW-CODEX.md`, `IR` = `tang3/stage1b/IMPLEMENTATION-REPORT.md`).

## 1. Files

| File | SHA-256 | Purpose |
|---|---|---|
| `wp4-tier3-v2-costfirst.json` | 37040657…00ae | ON config: v2 + exactly `"selectionObjectiveOrder": "cost-first"` (LF, no BOM, one inserted line) |
| `make_wp4_costfirst.py` / `.log` | 5288e143…9a84 / fa231700…e660 | Byte-level edit of v2 (as `configs/make_wp4_v2.py`), asserts and canonical hashes |
| `canonical_wp4.py` | b8ed04ce…84a1 | Python mirror of C# `CanonicalJson` and the WP4 binding (rules cited per line in its docstring) |
| `compare_equivalence_stage1b.py` | 4b3384fa…e2e7 | Gate-0 comparator (R22 §5), ON smoke check, CLI `pair` / `gate0` / `smoke` |
| `test_compare_equivalence_stage1b.py` | 689e4fe2…80f2 | In-memory positive/negative controls |
| `selftest-2026-10-11-final.log` | 807656ef…d519 | Self-test log of the final code (records the SHA-256 of the files it tested) |
| `run_stage1b.py` | bce7e2ad…5615 | Driver, phases `gate0` / `smoke` / `main`, modes `plan` / `report` / `freeze` / `run` |
| `plan-dryrun-2026-10-11-final.txt` | 87335e98…43ca | `plan` output for the three phases (final code) |
| `RUNNER-PROVENANCE-TEMPLATE.md` | ebd64a24…b4a8 | Receipt template + proposed publish command |
| `STAGE1B-PLAN-DRAFT.md` | 8b7338c0…ce1b | Plan text to complete and seal |
| `selftest-2026-10-11.log`, `plan-dryrun-2026-10-11.txt` | e3debcc9…9067, d30dc671…1a13 | Superseded earlier runs (kept). Both also PASS / same blockers; code changed afterwards only in the comparator CLI guards, the self-test header and the sealed log name |

All `.py`/`.json`/`.md` files are LF-only without BOM (checked byte-wise); the two stdout-captured logs have CRLF.
No `__pycache__` was created (every Python run used `-B` and `PYTHONDONTWRITEBYTECODE=1`).

## 2. Item 1 — cost-first WP4 and canonical hash (`make_wp4_costfirst.log`)

- Canonicalization mirrored from `CanonicalJson.cs:39-43,88-104,120-124,145-151,156-158,160-175,178-201,203-225,247-303,305-319`,
  integer range `ProtocolPrimitives.cs:8,10`, WP4 hash `Wp4RunnerConfiguration.cs:245-248`, commitment hash
  `CommitmentPolicyConfiguration.cs:69-72`, binding `Wp4PolicyConfigurationBinding.cs:19-20,31-45` (all in
  `C:/Code/RideBound-costfirst`, read only). Cross-checked against the adapter's own canonicalizer
  (`mapping.py:130-158`): identical bytes for v2 and ON. The Runner rejects a manifest whose binding differs
  (`RunnerSession.cs:301-310`).
- v2: file 66e9b592…4518, canonical **fb2813c4…cd42** (archived value reproduced).
- ON: parsed JSON differs from v2 in exactly one root key; removing it gives canonical bytes identical to v2.
- **ON canonical SHA-256 = `16dcb7e3337763aab7889f7528365dced2aa49028db6dbf390f8b3dce79b3140` — equals Codex R22:236.**
- Bindings: OFF-U 251b331e…c346 and OFF-C30 a2f11a3a…5f1c (archived, reproduced); **CF-U 90b290d7…2df5 and CF-C30
  e6b33be5…6f76 — both equal Codex R22:237-238.**

## 3. Item 2 — comparator (R22 §2 F1/F2, §5, §7.1)

How each requirement is met (line numbers in `compare_equivalence_stage1b.py`):
- F1, sole permitted difference: `binarySha256` is tokenized only at `('adapterToRunner', initializeRun, payload.manifest)`
  and only when it equals the verified DLL of that run's runner (`:265-271`); runner trees verified against their
  manifests with the `runner_manifest.py` definition (`:121-158`); old value must be the archived v2 DLL/tree, new value
  the v3 DLL; all four new values must agree (`:531-552`, gate loop in `main`).
- F2, stable identities kept: `STABLE_HASH_KEYS` (`:65`) stay raw; init fields compared one by one; the expected arm
  binding is recomputed from the config bytes (`expected_binding_from_files`); coreCommitSha 1ce15e9 and inventory
  0da63eaf required (`:531-562`).
- Salted identities: runId → token; publicationId → `pub:<requestId>#<ledger index>`; breachId →
  `breach:<kind>:<requestId>:<epoch>:<eventSeq>#n`, both from the final ledgers (`:292-308`); unmapped reference = failure;
  `publicationIds` compared as sorted membership; `actions` as a sorted multiset (`:278`).
- Derived digests (`DERIVED_HASH_KEYS`, `:67`): renamed by first appearance per run, so link patterns must match;
  links also validated per run (`:380-436`): decision chain from the zero hash, ACK hash+epoch, certificate
  input/proposed = before/after state, certificate publicationIds = published actions, checkpoint continuity. These
  invariants hold with 0 violations on all four archived references (checked read-only).
- Unclassified hash field = failure (`:273`). Transport/bundle integrity per run (`:161-207`, `:362-378`).
- Exact components (`:483-529`): `fleet_decisions` and `audit_vep.audit` imported unchanged and run on the in-memory
  transcript; served, lifecycle, boarding and alight times (sweep_analyze rule; cross-checked against the original
  function on real runs); frames; final online state, commitment ledger, incident ledger; run-00 semantic block.
- Verdicts: `gate0` writes `GATE0-PASSED.txt` or `GATE0-FAILED.txt` once (mode "x"), with the runner DLL and tree hash;
  it refuses to judge (writes nothing) while inputs are not frozen/sealed or a job has not started (`:700-711`).
  `smoke` checks binding, DLL, objective levels (no `worst-hard-utilization-ppm` / `revision:*`, accepted count before
  cost), CF-C30 drop Σ|c| ≤ 30 s, and prints solver status/path/omission counts (R22 F4).

### Self-test results (`selftest-2026-10-11-final.log`: 59 OK, 0 BAD, SELFTEST PASS, 233 s)
Python `E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B`; archived runs read only; every variant built in memory.
- C1–C3 (27 checks): canonical hashes and bindings above; the explicit-`stability-first` binding differs; canonicalizer
  rejects null, float, exponent, −0, duplicate key, out-of-range integer, lone surrogate; runner-identity unit cases.
- R1: archived `runner-tier3-v2` tree equals its manifest (tree ac251ef3…, 429 files), DLL a631038b… (= R22:196).
- R2: the four Gate-0 references have the R22 SHA-256 and frame counts (2020/2284/2035/2146).
- R3: all four references pass every per-run precondition with the archived readers.
- Pair controls (reference = archived U-w08): PASS as expected for P1 identical copy, P2 only verified new DLL,
  P3 consistent re-salt of runId / publication and breach IDs / every derived hash with permuted salted orders plus a
  verified new DLL, P4 permuted publicationIds/actions. FAIL as expected for N1 unverified DLL, N2 tree ≠ manifest,
  N3 cost-first binding, N3b explicit stability-first binding, N4–N6 scenario/graph/travel-time hash, N7 one event
  snapshotHash, N8 coreCommitSha, N9 one decision time +1 ms, N10 one route stop swap, N11 one ledger value +1,
  N12 one alight time +1 ms, N13 one publication missing from a certificate, N14 unknown ledger publication reference,
  N15 broken ACK, N16 unclassified hash field, N17 frame bytes changed with old frameSha256, N18 stale bundle manifest,
  N19 summary status fail, N20 the C-30 run of the same cell.

## 4. Item 3 — driver and dry check

`run_stage1b.py`: jobs and command exactly as `run_t38.py:84-103` except runner root (`runner-tier3-v3`), WP4 per phase,
label and output root `C:/RideBoundData/research/tier3-stage1b-v1/<phase>/<job>`. Gate-0 labels are the archived
`<arm>-<cell>-t38`. Every `run` re-checks: runner tree = `runner-tier3-v3.sha256.tsv` byte for byte, DLL ≠ v2,
receipt records tree and DLL (`:140-156`); config file/canonical/binding hashes and "ON = v2 + one key"
(`:158-185`); wrapper v1.2 hash, inventory constant, split hash, harness HEAD 1ce15e9 (`:187-203`); per-cell inputs and
world check as `run_t38.py:146-167` (`:205-233`); Gate-0 references (`:235-241`); input freeze (`:264-276`); plan seal
covering `SEALED_REQUIRED` (`:278-298`); `smoke` needs a valid `GATE0-PASSED.txt`, `main` also `SMOKE-PASSED.txt`, both
bound to this runner build; a `*-FAILED.txt` stops everything (`:300-329`). Safety: AC power wait 20 min, STOP file,
keep-awake, logs mode "x", outputs never reused (`run_t38.py:106-132`), plus the C: disk floor 51,200 MiB + 410 MiB/job
(`run_forecast.py:50,105-106,126-127`).

`plan` (dry check, no job, no directory created; `plan-dryrun-2026-10-11-final.txt`): gate0 4, smoke 2, main 68 jobs,
all `pending`. The only input problems are the expected blockers: `runner-tier3-v3.sha256.tsv` missing, plan not sealed,
input freeze missing, and (smoke/main) no `GATE0-PASSED.txt` / `SMOKE-PASSED.txt`. Every other check passed:
configs, bindings, wrapper, harness HEAD = 1ce15e9, 37 cells' fixtures/drivers/worlds, Gate-0 references.
C: free 60,895 MiB at the check; 74 jobs × 82–96 MB ≈ 6.5–7 GB leaves ≈ 54 GB (floor 50 GiB; margin ≈ 4 GB).
The comparator's `gate0`/`smoke` were also invoked once each as a refusal check: both refused "not ready", nothing written.

## 5. What was executed (complete list)
1. Read-only inspection: archived T3.8 transcripts/bundles (decoded in memory), the v2 runner tree, git read-only
   commands on `C:/Code/RideBound-costfirst` (`log`, `diff --stat 45f8026 761915c`, `merge-base`, `rev-parse`, `status`).
2. `make_wp4_costfirst.py` (pure Python; wrote the JSON with mode "xb" and its log).
3. `test_compare_equivalence_stage1b.py` three times (console debug pass, then two logged passes; all PASS).
4. `run_stage1b.py plan gate0|smoke|main` twice (this includes `git -C E:/Code/RideBound-deadline rev-parse HEAD`).
5. `compare_equivalence_stage1b.py gate0|smoke` refusal check.

## 6. Blocked or open (owner decisions)
1. **Runner v3 does not exist.** Commit + publish need owner approval (IR:262-267). Everything stops at the manifest check.
2. **Exact v2 publish command is not recorded** anywhere I found (`SWEEP-AMENDMENTS.md:32-33`; noworse `docs/18:5117-5118`
   record only commit, clean tree, tree hash, 429 files). The proposed command copies the recorded Tier-2 form
   (`session-2026-09-23-archive/tier2-v2-draft.md:352-356`) and matches the v2 layout; this is an inference, checked
   after publish by the file-list comparison (template §C).
3. `src/` at base `761915c` equals the v2 source `45f8026` (the 97 changed files between them are all under `docs/`), so
   the new runner differs from v2 in source only by the reviewed cost-first diff.
4. Choices of mine the owner may change before the seal: smoke cell `d20181114-s10-r1-w08` (calibration, not test);
   `main` also gated on `SMOKE-PASSED.txt`.
5. Recommended: an independent Codex review of `compare_equivalence_stage1b.py` and `run_stage1b.py` before the seal
   (R22 §7.1 asks to validate the profile before the four OFF jobs; the self-tests are mine).
6. Analysis scripts for the main phase (I_L identity, guards, H5 forecast replay, H7 cumulative-only screen) are not
   written yet; the plan requires them frozen before main outputs are opened.
7. Where the sealed plan lives: T3.8 practice committed and pushed the sealed plan to the research branch before the
   first job (`tang3/t38/T38-PLAN.md:3-5`); same decision needed here.
8. On a clean committed tree `dotnet test` will show the known `StrictBagItBundleTests` failure (IR:245-246).

## 7. Steps after owner approval (in order)
`PY` = `E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe`, `H` = this folder.
1. Confirm the reviewed snapshot: `git -C C:/Code/RideBound-costfirst -c core.autocrlf=false diff | sha256sum` → `c48042af…`.
2. Commit the 8 files in `C:/Code/RideBound-costfirst` (branch `research/tier3-cost-first`); `git status --porcelain`
   empty; `git -c core.autocrlf=false diff 761915c HEAD | sha256sum` → `c48042af…`; push if the owner wants.
3. `dotnet test RideBound.slnx` in that worktree; record (expect 1033 pass + the known clean-tree failure).
4. Publish exactly as `RUNNER-PROVENANCE-TEMPLATE.md` §E (output root must not exist; log the console).
5. `PY -B tang3\runner_manifest.py write C:\RideBoundData\research\runner-tier3-v3 H\runner-tier3-v3.sha256.tsv`, then
   `verify`; compare the file list with `runner-tier3-v2.sha256.tsv` (429 paths).
6. Copy the template to `H\RUNNER-PROVENANCE-v3.md` and fill it (commit, toolchain, tree hash, DLL SHA-256, harness facts).
7. `PY -B H\run_stage1b.py freeze gate0` → `stage1b-input-freeze.sha256`.
8. (Recommended) Codex review of the harness. Then copy the draft to `STAGE1B-PLAN.md`, fill the `<FILL>` marks, and
   seal: `sha256sum` of every `SEALED_REQUIRED` file (plan first, `sealed <UTC>` after its name) into
   `STAGE1B-PLAN.sha256` (noclobber). Commit/push plan + seal if decided (§6.7).
9. `PY -B H\run_stage1b.py plan gate0` → `input problems: none`; then `run gate0` (4 jobs).
10. `PY -B H\compare_equivalence_stage1b.py gate0 H\gate0-compare-<date>.txt` → `GATE0-PASSED.txt`. If FAILED: stop,
    report the first difference, no re-run without the owner (R17:259; R18:230).
11. `plan smoke` → `run smoke` (2 jobs) → `compare_equivalence_stage1b.py smoke H\smoke-check-<date>.txt` →
    `SMOKE-PASSED.txt`; read the solver status / execution path / omission counts it prints.
12. Write the main analysis scripts; record their SHA-256 in `STAGE1B-AMENDMENTS.md` before opening any main output.
13. `plan main` → `run main` (68 jobs; re-invoke `run main` for deferred jobs, outputs are never reused).
14. Analysis and Codex independent recomputation; judge G0–H7 as sealed; decide Stage 2 by its conditions; update
    `docs/18` and `docs/19` in the research worktree (ADR-078 status, Gate-0 evidence), per AGENTS.md.
