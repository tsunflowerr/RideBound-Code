"""Stage 1b analysis, step 1 of 2: per-rider and per-run extraction, READ-ONLY on every run folder.

Adapted copy of optimize/codex-debate/claude-work/r21-stage1a/extract_stage1a.py (Stage 1a, 2026-10-10) for the Stage 1b
plan (STAGE1B-PLAN-DRAFT-v7.md sections 1, 5-8, 12; unchanged from v6 in those sections). Written 2026-10-14, before any
Stage-1b job output existed. Step 2 is report_stage1b.py; the self-test is test_analysis_stage1b.py.

Roles (the 34 test cells of tang3/t38/split-t38-v1.json "test"; 108 requests per run, 3,672 per role):
  CF-U    main job CF-U-<cell>    C:/RideBoundData/research/tier3-stage1b-v1/main/CF-U-<cell>     label CF-U-<cell>-s1b
  CF-C30  main job CF-C-30-<cell> C:/RideBoundData/research/tier3-stage1b-v1/main/CF-C-30-<cell>  label CF-C-30-<cell>-s1b
  SF-U    archived T3.8 run       C:/RideBoundData/research/tier3-t38-v1/U-<cell>     (or its -rerun1, see sf_effective)
  SF-C30  archived T3.8 run       C:/RideBoundData/research/tier3-t38-v1/C-30-<cell>  (or its -rerun1)
  Layout and labels: run_stage1b-v7.py:162 (OUT_ROOT), :244-249 (jobs), :187 + :985-995 (logs/main/<job>.admitted),
  :1254 (first line of logs/main/<job>.log); plan section 1.2.

REFUSALS (exit status 2; nothing is written):
  - any expected run missing or failed: no folder, no summary.json (e.g. failure-00.json only), an unexpected file in the
    folder, summary status != pass, run-00 status != succeeded, bundle files not matching bundle-manifest.json;
  - main mode, CF runs: a missing main job log, a non-zero returncode, a log or admission anchor naming another analysis
    binding, a missing anchor; any entry of the main output folder that is not one of the 68 expected jobs;
  - main mode, study state: any *-FAILED.txt or INPUT-DRIFT-STOP.txt in the harness folder; a missing
    GATE0-BEHAVIOURAL-PASSED.txt, SMOKE-PASSED.txt or SMOKE-INSPECTION.txt; malformed verdict/build links or smoke
    inspection; changed prerequisite receipts, retained inventory or latches before final output;
    stage1b-analysis-binding.txt missing, or not listing all required analysis files with their current SHA-256,
    or naming another manifest; an
    interpreter other than the frozen job interpreter JOB_PYTHON (its tree is in the Stage-1b input freeze);
  - identity: label, policyConfigurationHash (per role), binarySha256 (CF: the RideBound.Runner.dll line of
    runner-tier3-v3.sha256.tsv; SF: runner-tier3-v2 a631038b...), coreCommitSha, masterSeed, policyId, policyVersion,
    repository inventory, repeat / receipt fields, 108 requests; per cell, equal scenario / graph / travel-time
    snapshot hashes, source fixture hash and request-id sets across the four roles;
  - pins: a reader module or data file whose SHA-256 differs from PINNED_MODULES / PINNED_DATA, a reader module loaded
    from a path other than its pinned one, any module newly loaded by the readers that is neither pinned nor inside the
    interpreter, a file read while a run is read that is outside the pinned files, the run folder and the interpreter
    (audit hook), any file opened for writing while a run is read, an SF bundle-manifest.json differing from
    SF_BUNDLE_PINS;
  - integrity: two drop initialPromise rows for a rider, a first drop entry that is not the initialPromise, per-rider
    sum |c| != audit sumDropC, a served rider (completed and alighted) without a drop initialPromise, a transcript
    changed while it was read.
RECORDED, not refused (printed by report_stage1b.py): every ledger entry whose budgetBefore / budgetAfter
  dropEtaTotalMs differs from the running sum |c| (an accounting-consistency diagnostic, separate from recovery);
  completed-without-alight and
  alighted-not-completed requests (not served by the plan's definition).

Reader source reused UNCHANGED (all ten modules execute retained SHA-verified source bytes; no cached bytecode):
  tang3/evidence/audit_vep.py (+ tang3/oracle/accounting.py): load / audit (final checkpoint, signed ledger rows,
      summary: lifecycle, nonNormalCertificates, breaches, forced-breach riders); load() is memoised for the current
      transcript exactly as extract_stage1a.py:44-56 does (the function itself is unchanged);
  optimize/display_layer.py (+ optimize/promise_stretch.py, tang3/world/preflight_world_v1_3.py, preflight_world.py,
      preflight_world_v1_1.py, preflight_world_v1_2.py, world_v1.py; factor CSVs of 2018-11-12/13/14): the frozen
      causal-no15 forecast, D0 = display(t0, p0, profile(day, window, "causal-no15")) (naive when inactive);
  optimize/codex-debate/claude-work/r13-final-judge/cumulative_decisive_check.py: analyze(job), the budget-pruned
      candidate classes of acknowledged decisions; re-derived by own_prune_classes as a cross-check (as Stage 1a).
The v1.3 CSV parser receives immutable SHA-verified factor bytes through a read-only path facade; its bin/profile
arithmetic is unchanged. Parent and workers each verify/capture their own sources/data once; final pins detect
persistent disk drift, while transient disk substitution cannot change the retained forecast input.
Verbatim copies (the original modules import the whole run / sweep driver graph; test_analysis_stage1b.py checks that
each copy equals its original on real data):
  event_times = tang3/sweep/sweep_analyze.py:40-57; sf_effective = tang3/t38/t38_calibrate.py:43-48 with the state rule
  of tang3/t38/run_t38.py:74-80; day_of = optimize/forecast_family_a.py:53-54.

Per accepted rider (a drop initialPromise exists), as Stage 1a plus `served` and `maxAbsC`:
  t0, p0 = publishedAtMs and p of the drop initialPromise; D0 = display(t0, p0, causal-no15 profile of the cell);
  A = simTimeMs of the first adapter batch with the rider's passengerAlighted event (empty if none);
  served = lifecycle "completed" AND A present (plan section 5);
  S = sumAbsC = sum |c| over the later drop revisions (ms); k = nonzeroC = revisions with c != 0; netC = sum c;
  maxAbsC = max |c| over the later drop revisions (0 if none);
  shared = another alighted rider on the same vehicle with an overlapping on-board interval [actualPickupTimeMs, A)
  (extract_stage1a.py:133-142, rule of q2_cap_dial.py / wp14r_ladder_reanalysis.py).

Usage (interpreter: E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe -B):
  python -B extract_stage1b.py main <out-prefix>
  python -B extract_stage1b.py dryrun <CF-U stand-in> <CF-C30 stand-in> <out-prefix>
      stand-ins: SF-U | SF-C30 | SF-C120 (archived runs only). A PLUMBING CHECK: the output is marked dryRun and its
      "CF" numbers are archived stand-ins, never Stage-1b results. Study-state and CF-layout checks are recorded, not
      enforced (no CF run is read).
  Writes <prefix>-riders.tsv, then <prefix>-runs.json (both mode "x"); runs.json records the SHA-256 of riders.tsv.
  The prefix may not lie under C:/RideBoundData or E:/RideBoundData (input roots).
"""
from __future__ import annotations

import base64
import collections
import hashlib
import importlib
import importlib.abc
import importlib.util
import io
import json
import os
import pathlib
import re
import sys
from concurrent.futures import ProcessPoolExecutor
from types import MappingProxyType

sys.dont_write_bytecode = True        # never write __pycache__ next to the reused readers

HERE = pathlib.Path(__file__).resolve().parent
SCRATCH = pathlib.Path(r"E:/Code/Report_INT3508/ridebound-scratchpad")
FACTORS = pathlib.Path(r"E:/RideBoundData/wp6/extracted/sha256/d9/d9e86f33645e5eec287d387f8d63ad41ddf41d4ef648138b65d636"
                       r"482e2c599e/FleetPy_Manhattan/networks/Manhattan_2019_corrected")   # preflight_world.FACTORS
SPLIT = SCRATCH / "tang3" / "t38" / "split-t38-v1.json"
SF_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-t38-v1")             # run_t38.py:53
C120_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-t38c120-v1")       # Stage 1a (dry-run stand-in only)
CF_ROOT = pathlib.Path(r"C:/RideBoundData/research/tier3-stage1b-v1") / "main"  # run_stage1b-v7.py:162,261
LOG_MAIN = HERE / "logs" / "main"                                             # run_stage1b-v7.py:187,985
RUNNER_V3_MANIFEST = HERE / "runner-tier3-v3.sha256.tsv"                      # sealed (plan section 13)
BINDING = HERE / "stage1b-analysis-binding.txt"                               # run_stage1b-v7.py:184 (plan s12)
FREEZE = HERE / "stage1b-analysis-freeze.sha256"                              # run_stage1b-v7.py:183
STUDY_FILES = ("GATE0-BEHAVIOURAL-PASSED.txt", "SMOKE-PASSED.txt", "SMOKE-INSPECTION.txt")
DRIFT_STOP = "INPUT-DRIFT-STOP.txt"
OUTPUT_FORBIDDEN = (pathlib.Path(r"C:/RideBoundData"), pathlib.Path(r"E:/RideBoundData"))
WORKERS = 2                                                                   # as extract_stage1a.py:174
JOB_PYTHON = pathlib.Path(r"E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe")   # run_pilot.py:28; its whole tree is
# frozen by stage1b-external-python-env.sha256.tsv (plan s1.1), so main mode requires it (the stdlib is then covered)

ROLES = ("CF-U", "CF-C30", "SF-U", "SF-C30")
STANDINS = ("SF-U", "SF-C30", "SF-C120")
SOURCE_ARM = {"CF-U": "U", "CF-C30": "C-30", "SF-U": "U", "SF-C30": "C-30", "SF-C120": "C-120"}
POLICY_BINDING = {   # initializeRun.policyConfigurationHash per source (plan section 1 "Run identity")
    "CF-U": "90b290d7e63f6885ac8e93e18017d55dddc54c27a05d8476a65dcf7d802e2df5",    # make_wp4_costfirst.log; R22:237
    "CF-C30": "e6b33be5649dc2b1208b42b90cef227d00721cba59e4340fdbc4e96f21796f76",  # make_wp4_costfirst.log; R22:238
    "SF-U": "251b331eb56fe870e734d651bf6937e95304919ed92967b2f3b0d0b9da1ac346",    # archived (R22:126-130)
    "SF-C30": "a2f11a3ada0773f9b30b85c56c935058e2e931f13c907f46b2ddfff8c1285f1c",  # archived (R22:126-130)
    "SF-C120": "a9ac58b33558c6ec2d604ee7fe3e7fdb02deaecfd866ea73282b911326ed9010",  # Stage 1a (R19-stage1a-analyze-v4.py:15)
}
OLD_RUNNER_DLL = "a631038b6ea4850ffa29be2d5e4058fba2e57b725ee568075686d7a43c6f5fac"   # runner-tier3-v2 (R22:196)
CORE_COMMIT = "1ce15e92d2c1459d4254bbbcd581529197257fb2"                             # plan sections 1.1, 2
RUNNER_V3_MANIFEST_SHA256 = "ed69d23e953447eb62e416a0b23a575d319e8ee1647e9d8ee5a4a14155864587"  # sealed plan s2
INVENTORY = "0da63eaf2b30ab332fba265a6b2b6e5c36b3ee78950cbac6d6267e60f81e4cbe"       # run_w07.EXPECTED_INVENTORY
SPLIT_SHA256 = "b72ac0f731fa7425891f364f0976054ae172a3117a507155ebe7e5c122ea03f1"    # run_t38.py:41; plan section 1
POLICY_ID, POLICY_VERSION, SEED, REQUESTS_PER_RUN = "ridebound-hard-vector", "tier3-exploratory-v1", 7, 108
BUNDLE_FILES = ("run-00.json", "summary.json", "transcript-00.ndjson")   # compare_equivalence_stage1b-v7.py:147
FOLDER_FILES = frozenset(BUNDLE_FILES + ("bundle-manifest.json",))
MANIFEST_KEYS = ("policyConfigurationHash", "binarySha256", "coreCommitSha", "masterSeed", "policyId", "policyVersion",
                 "scenarioContentHash", "graphSnapshotHash", "travelTimeSnapshotHash")
CELL_EQUAL_KEYS = ("scenarioContentHash", "graphSnapshotHash", "travelTimeSnapshotHash")
NOT_FORCED_BREACH = "exogenousServiceQuality"     # the only breach kind of the archived U / C-30 runs (Stage 1a)

# ---- pinned dependencies (SHA-256 read 2026-10-14; mtimes all before the Stage 1a run of 2026-10-10)
PINNED_MODULES = {   # module name -> (path under SCRATCH, SHA-256)
    "audit_vep": ("tang3/evidence/audit_vep.py",
                  "ffa86b31fdc464505854ecf44821115bc890d1b51197d6f802cb934d2706807f"),
    "accounting": ("tang3/oracle/accounting.py",
                   "0eb831be076fed1fde8571f00f3eb68194e9ce25ac174be14f80b0cc5e83e55d"),
    "display_layer": ("optimize/display_layer.py",
                      "0f3d8b646337606e8baa0e28d9e7d535f1f97fb68f2f5bb1b30658ba0eaeeb87"),
    "promise_stretch": ("optimize/promise_stretch.py",
                        "e35c38b73eb4f3d86b81dc3a227b5f014fe187643f1d92ffe7f2c887711b91a5"),
    "preflight_world_v1_3": ("tang3/world/preflight_world_v1_3.py",
                             "0bcad103232dd0a4c0ab87041d1ed48c2e0e9956e563701f30b5ed7f4bfa2047"),
    "preflight_world": ("tang3/world/preflight_world.py",
                        "9467611cd0b691996abb7da16469402b03b9904ea991425d568d005ee43f396d"),
    "preflight_world_v1_1": ("tang3/world/preflight_world_v1_1.py",
                             "7df4a458210fbd4a12956489f5786f20bb1bcae734f93000acc1132f4bfe7725"),
    "preflight_world_v1_2": ("tang3/world/preflight_world_v1_2.py",       # = WRAPPER_SHA256, run_stage1b-v7.py:170
                             "df28b6cb6adf2c94e5b539f137bda674b8498046d56fb0c5f1ab4ec1937ca262"),
    "world_v1": ("tang3/world/world_v1.py",
                 "40830b3419f4a38d48ce7469eabc6915c384084c46842bbf96425ed30c1dc6da"),
    "cumulative_decisive_check": ("optimize/codex-debate/claude-work/r13-final-judge/cumulative_decisive_check.py",
                                  "24fb60e137dadb312a594e5d04fc6e4b94f015fb8de2ac14602aec857ce6772e"),
}
PINNED_DATA = {   # path -> SHA-256 (factor hashes equal R19-stage1a-analyze-v4.py:16-19)
    str(SPLIT): SPLIT_SHA256,
    str(FACTORS / "2018-11-12_tt_factors.csv"): "89f64a74ff7262b93cea7270cd62639cef3fa1dbc8a091f8e3b927d1100c7317",
    str(FACTORS / "2018-11-13_tt_factors.csv"): "442290763bd4f46faa02a8854cacf4b75f7a3b6df97af16b4b9490078c342b0c",
    str(FACTORS / "2018-11-14_tt_factors.csv"): "0f09e2651a1257680d1209518a6f91fba98d2ae57a0b8278458daecfecf2f85f",
}
READER_PATHS = (SCRATCH / "tang3" / "evidence", SCRATCH / "optimize",
                SCRATCH / "optimize" / "codex-debate" / "claude-work" / "r13-final-judge")
SF_BUNDLE_PINS = {   # SHA-256 of bundle-manifest.json of the 68 archived comparators (resolved folders; read 2026-10-14;
    # every transcript SHA-256 they list equals Codex's codex-work/R13-raw-verification.json, 68/68)
    "U-d20181114-s10-r3-w07": "03d636df0955741e5b1a56ce847d32a4614db0febbfa5ad4946c10a5b12a216c",
    "U-d20181114-s10-r3-w08": "1964c8a76c58136206ce372c62785e56a5dab81afc1b6fdc4db5c5a0d09499e2",
    "U-d20181114-s10-r3-w17": "e88dec436546fd511f55669e96e23370e6a677c47088ef34dbfb02707815d880",
    "U-d20181114-s10-r4-w07": "87bfb5cbffa0f743a0873cb2b38196c4b51d3eb49668265fd89bd738dfa2e41a",
    "U-d20181114-s10-r4-w08": "5b09afe03dc936862cd84ff88eb3a684e4045e5d13b967a032fc66b676cd4555",
    "U-d20181114-s10-r4-w17": "48e74cfe6b9b2e13f8bbe0a4c396a648405580a3b37881443b0b8212a320fe37",
    "U-d20181115-s10-r1-w07": "0b69713bfa4fdf1d05098c07dc473a597ce179769fa055e1cc75ff896f493063",
    "U-d20181115-s10-r1-w08": "0f188443017d2343a0d1a1b70866b0a639ca22e8990e4effafb0b22d1d4073c7",
    "U-d20181115-s10-r2-w07": "91ac4f8b3390e509c2f460db57c74af710f6e5ebcb148288fcbec292e853ec89",
    "U-d20181115-s10-r2-w08": "ee3fdf6df2be3fe8036cb49e09683d968004123ae4b431172ef08ef5f99f83c1",
    "U-d20181115-s10-r4-w07": "83cea87e0873210bf1f3511d1bafcaa5a078a57f1ad44031c354f86c4d809086",
    "U-d20181115-s10-r4-w08": "fa7dea64ab4fe9c73172e28ee221152b4b9b6616ce9dd59d6a68dba71a0ee935",
    "U-d20181116-s10-r1-w07": "48841e9cf418a39572812f95397e8578fd6fc8699d1059ab48aee482e0b84e74",
    "U-d20181116-s10-r1-w08": "3c0701b99f9c426898314240f19b5a615da470791b1d8ebb6703838b47a0778f",
    "U-d20181116-s10-r1-w17": "b7fc468a62e36daab4aeb8e15731acfd74506c2531f2922751b3f657f19c6db5",
    "U-d20181116-s10-r2-w07": "ebafbb49c1fdf7fe7537bf04fd4abbba8ee90cb113b8ba86a0be3c719206c54e",
    "U-d20181116-s10-r2-w08": "10cd3f0535825a4784b5434440c84b8e5f66eba24215827b35c4527dc347b987",
    "U-d20181116-s10-r2-w17": "066a52ef4c60712ee0cd01348bf0c5fd8d4051901d54eda8506c6e400912109a",
    "U-d20181116-s10-r4-w07": "6a16a3c13df469a3e8707c7de5bb4d7a7d6f116003edb400c277461826cb6891",
    "U-d20181116-s10-r4-w08": "59f3d4470921431a7bf680fb52bcf3a78e5e41d74559875323724b275a190411",
    "U-d20181116-s10-r4-w17": "77c799d8237fead4e8d7709091f45b988a67370f7dda0327f141e04a814d59b9",
    "U-d20181117-s10-r1-w08": "1cf959675513601cb7edd92b832a02b33ff2f05f3664c989ecc2236a16b1e244",
    "U-d20181117-s10-r1-w17": "1f7ff1a6296390990e397bf7f04970b142de9c305ee1982883990e0893b125cf",
    "U-d20181117-s10-r2-w08": "b9be1c648a3eb0fea85b728e81a79820b8edefa85b3b8d25855beafd8d4675b2",
    "U-d20181117-s10-r2-w17": "87a5977f76ada9f6c82eb09823e716e4b458b2566578731490f7b2de45a2d945",
    "U-d20181117-s10-r3-w07": "f0e9bad200a2ca13d51820ddcb3b3fe59ef529127a6aa3194f4d39fdcd476f42",
    "U-d20181117-s10-r3-w08": "ec5f5e0a18823eb4d44afa81be8d2e154a3850f3ebb63b8fcf62f1dc868af71f",
    "U-d20181117-s10-r3-w17": "1d30910a3c4928dffada0dca54cdc995b2f59d6e36dd0cf12230432690a12e8c",
    "U-d20181118-s10-r2-w08": "c25f7bcd25741a01145146cce240d098b394e9277cb07703671ce14a53de25f9",
    "U-d20181118-s10-r2-w17": "36ce84d25500cc12297b92d1c45e84a34af71cb9322ec07ea2b982bd7bc06aac",
    "U-d20181118-s10-r3-w08": "ed5bfdf9015c9e15ab9f2d8fc73f0aeba26569da5b7171b624352b51e470f39f",
    "U-d20181118-s10-r3-w17": "7d6a92fc3efb24311c076c948dce4f921df2db6104f252a68963ec3700e832d3",
    "U-d20181118-s10-r4-w08": "7699684da24a80683bd015301fd75f8af8bd9af5c777e94bc66ff47334b5460b",
    "U-d20181118-s10-r4-w17": "e337f9049bb3e4f9bebb61f438f1560de4f1512fd1a11c3a43c9c499cea59f5e",
    "C-30-d20181114-s10-r3-w07": "f6f2c0482da9e4c7002dff298199051693b172db0a18660d39699f08e930e737",
    "C-30-d20181114-s10-r3-w08": "4e1f3a224a8511bb962e900d76f0e07153506be7a950b8d99b68ba5e82a11401",
    "C-30-d20181114-s10-r3-w17-rerun1": "df7a39657a059fb95bf1371445e6b4ab339e40eb5962da54f84538b836076fd2",
    "C-30-d20181114-s10-r4-w07": "4cda40bc3ea34bc1de548e9d57e873f9f6b5f046b5a779f6aadff547c11193ea",
    "C-30-d20181114-s10-r4-w08": "4e996644cbec8cc2e9df5d2b15ac3a8894c65656faec42ceda02e4a542bd6db2",
    "C-30-d20181114-s10-r4-w17": "16d51db2bd81e0fd4699541c9f9a0eda13a2b9f573b1d9d4061c8e63420c183f",
    "C-30-d20181115-s10-r1-w07": "b9dd1fd05aa21f26e93805c4d8ad8e5a7abb7e3cd75f1b21382a79a63761335c",
    "C-30-d20181115-s10-r1-w08": "296b8a69aba542c7c7514e8e997dbd5dd5a987e552e0e8386eaaa0896ae44be2",
    "C-30-d20181115-s10-r2-w07": "ee20c4e2f80a8f7894414f2dd415c42bd15c526c1ddc8e5255a1ab336f9ddb86",
    "C-30-d20181115-s10-r2-w08": "08745cedbe7bab3e5e68c94a6b9f0c4a6cfa5b02f405bb5fb77bd47525b2d22e",
    "C-30-d20181115-s10-r4-w07": "4e65ef1f73c2e65b210e9beea4091315b28a18cecfb4c62cf0630eaf15ae7b1d",
    "C-30-d20181115-s10-r4-w08": "c7b52d3caa97674acef82b15826429b5daec61f0b4c32e2dac34cb9925e4d0c2",
    "C-30-d20181116-s10-r1-w07": "01fc7ee6a4c68882de29ad3f475efac28310922f87178a2d83ee0025bf05f29a",
    "C-30-d20181116-s10-r1-w08": "2330a3c691077a55756e642bde5fb01d2c199164bd2f19d26597ffdadbe24892",
    "C-30-d20181116-s10-r1-w17": "a2112fec4d9721b3b99df49e4f59da879f0654da4b56ada0f2afc3c98319ce4f",
    "C-30-d20181116-s10-r2-w07": "853544b48c56aeb290ad3d5bae2a345beadf12adca0e3a549967c9ec5a113b3a",
    "C-30-d20181116-s10-r2-w08": "3abde13c6d87510288031a712487213c9bb221ba773720de2eec31aee4b8c606",
    "C-30-d20181116-s10-r2-w17": "0ca87c30ce13769750bf84c3b94b2f2c02e33ef888429b99689da1f520710756",
    "C-30-d20181116-s10-r4-w07": "35ad0cb1c75252a333e16115e080837ef4c9ea0ab13761c7c80d98d7d473afd6",
    "C-30-d20181116-s10-r4-w08": "19fb5290469df5463324cc85f2e8e219fbc675a61b2ecbc210a0e53cd2d90edb",
    "C-30-d20181116-s10-r4-w17": "d02675147a9d4879025232b40db3f16e02909bed6659f00394fed39849fb0e48",
    "C-30-d20181117-s10-r1-w08": "af70648f911306fc9840161e2fca10b1d46f007c63eda6bb27ffbbe73495806e",
    "C-30-d20181117-s10-r1-w17": "21db26c0b0aefb8b1fd6f9bd7104b1855ea54244003ad6ee962e45efe50fe945",
    "C-30-d20181117-s10-r2-w08": "37f45bd3385779cefa7aed48fec56daa0dad94a27403927c82d28f4bc6350ff4",
    "C-30-d20181117-s10-r2-w17": "216dbdd320fc561b60ecf2266f5bb0de2267c7d6ffa96d2273e366787b6f87fa",
    "C-30-d20181117-s10-r3-w07": "d341cb0a57f7d1922a262aa34b5e3eca8753b52156fdd6457767a93b41d4b249",
    "C-30-d20181117-s10-r3-w08": "d822fdd647200330c664174aefb68bb848dc94b85abf2e9d2e3c817d682d47ac",
    "C-30-d20181117-s10-r3-w17": "f221e616eeb2fdead04bbe0abeded8ed585053a191c637f858d9f9f1287083d7",
    "C-30-d20181118-s10-r2-w08": "146fe9989de4dce258960f8f10a294432c75b21e18c2df8add35ff8a808f09f5",
    "C-30-d20181118-s10-r2-w17": "b8d750802e74ea8443755e50d95f5112c799d0a781e1c549cbbb802c8655c748",
    "C-30-d20181118-s10-r3-w08": "ceacbfb8a6950170b2ac78e032b54b3daaf998aee3e443a6f19c4267e5a7ba8a",
    "C-30-d20181118-s10-r3-w17": "0460b6d759333bee82621fd1c64725704441909b22c29b4893d8f5ec69e04b62",
    "C-30-d20181118-s10-r4-w08": "7cc05d5b7647700254b001cd86eef760b9cfe12a9a1cfc1c9a000714d97f04bb",
    "C-30-d20181118-s10-r4-w17": "489cda80f54f9dc41e949122b2fb5b1d30c209703f290bc8beefb12492191893",
}
BINDING_LINE = re.compile(r"^([0-9a-f]{64})  ([A-Za-z0-9][A-Za-z0-9._-]*)$")   # run_stage1b-v7.py:858
BINDING_MANIFEST = re.compile(r"^manifest-sha256 ([0-9a-f]{64})$")              # run_stage1b-v7.py:910
LOG_FIRST_LINE = re.compile(r"^returncode=(-?\d+) seconds=\d+ analysis-binding ([0-9a-f]{64})$")   # :1254


class Refusal(Exception):
    """The analysis refuses: an expected input is missing, failed, changed or inconsistent. Nothing is written."""


# ------------------------------------------------------------------------------------------ small helpers

def sha256_file(path) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def norm(path) -> str:
    return os.path.normcase(os.path.abspath(os.fspath(path)))


def day_of(cell: str) -> str:
    """Verbatim: optimize/forecast_family_a.py:53-54."""
    return f"2018-11-{cell[7:9]}"


def sf_effective(root: pathlib.Path, name: str) -> str:
    """Verbatim rule of tang3/t38/t38_calibrate.py:43-48 (amendment A1 of T3.8): a job whose own folder exists without
    summary.json (run_t38.py:74-80 state "exists-no-summary", an infrastructure failure) is read from its -rerun1."""
    output = root / name
    if output.exists() and not (output / "summary.json").exists() and (root / f"{name}-rerun1").exists():
        return f"{name}-rerun1"
    return name


def event_times(transcript: pathlib.Path):
    """Verbatim copy of tang3/sweep/sweep_analyze.py:40-57.
    requestId -> simTimeMs of the first adapter batch with its passengerBoarded / passengerAlighted event."""
    boarded, alighted = {}, {}
    with open(transcript, encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            if record["direction"] != "adapterToRunner":
                continue
            envelope = json.loads(base64.b64decode(record["frameBase64"]))
            if envelope.get("messageType") != "eventBatch":
                continue
            for event in envelope["payload"].get("events", []):
                kind = event.get("eventType")
                if kind == "passengerBoarded":
                    boarded.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
                elif kind == "passengerAlighted":
                    alighted.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
    return boarded, alighted


def initialize_manifest(transcript: pathlib.Path) -> dict:
    """The adapter's initializeRun manifest (first such frame); every frame read is checked against its SHA-256."""
    with open(transcript, encoding="utf-8") as handle:
        for number, line in enumerate(handle):
            record = json.loads(line)
            frame = base64.b64decode(record["frameBase64"])
            if hashlib.sha256(frame).hexdigest() != record["frameSha256"]:
                raise Refusal(f"{transcript}: line {number}: frame hash mismatch")
            envelope = json.loads(frame)
            if record["direction"] == "adapterToRunner" and envelope.get("messageType") == "initializeRun":
                return envelope["payload"]["manifest"]
    raise Refusal(f"{transcript}: no initializeRun frame")


def load_cells() -> list[str]:
    raw = SPLIT.read_bytes()
    if hashlib.sha256(raw).hexdigest() != SPLIT_SHA256:
        raise Refusal(f"{SPLIT} SHA-256 is not {SPLIT_SHA256}")
    cells = json.loads(raw.decode("utf-8"))["test"]
    if len(cells) != 34 or len(set(cells)) != 34:
        raise Refusal("the split does not list 34 distinct test cells")
    return list(cells)


def runner_v3_dll() -> str:
    """RideBound.Runner.dll SHA-256 of the published cost-first Runner (sealed runner-tier3-v3.sha256.tsv)."""
    if not RUNNER_V3_MANIFEST.is_file():
        raise Refusal(f"{RUNNER_V3_MANIFEST.name} does not exist")
    found = [line.split("\t") for line in RUNNER_V3_MANIFEST.read_text(encoding="utf-8").splitlines()
             if line.startswith("RideBound.Runner.dll\t")]
    if len(found) != 1 or not re.fullmatch(r"[0-9a-f]{64}", found[0][1]):
        raise Refusal(f"{RUNNER_V3_MANIFEST.name} has no single RideBound.Runner.dll line")
    if found[0][1] == OLD_RUNNER_DLL:
        raise Refusal("runner-tier3-v3 lists the archived v2 DLL")
    return found[0][1]


# ------------------------------------------------------------------------------------------ pinned readers

_READERS = None
_WINDOW: dict | None = None          # open() calls recorded while a run folder is read (audit hook)


def verified_bytes(path, digest: str) -> bytes:
    """Authenticate the exact bytes retained/consumed, rather than a separate earlier read."""
    raw = pathlib.Path(path).read_bytes()
    if hashlib.sha256(raw).hexdigest() != digest:
        raise Refusal(f"{path}: SHA-256 changed")
    return raw


class _VerifiedSourceLoader(importlib.abc.Loader):
    def __init__(self, path: pathlib.Path, raw: bytes, executed: dict):
        self.path, self.raw, self.executed = path, raw, executed

    def create_module(self, spec):
        return None

    def exec_module(self, module):
        module.__file__ = str(self.path)
        # compile(bytes) honors source encoding declarations. Never consult a .pyc or reread the path.
        exec(compile(self.raw, str(self.path), "exec", dont_inherit=True), module.__dict__)
        self.executed[module.__name__] = module


class _VerifiedSourceFinder(importlib.abc.MetaPathFinder):
    def __init__(self, sources: dict, executed: dict):
        self.sources, self.executed = sources, executed

    def find_spec(self, fullname, path=None, target=None):
        if fullname not in self.sources:
            return None
        filename, raw = self.sources[fullname]
        loader = _VerifiedSourceLoader(filename, raw, self.executed)
        return importlib.util.spec_from_loader(fullname, loader, origin=str(filename))


class _FrozenFactorFile:
    """A read-only path facade: the unchanged v1.3 CSV parser reads SHA-verified immutable bytes."""
    def __init__(self, path: pathlib.Path, raw: bytes):
        self.path, self.raw = path, raw

    def __fspath__(self):
        return str(self.path)

    def __str__(self):
        return str(self.path)

    def exists(self):
        return True

    def open(self, mode="r", buffering=-1, encoding=None, errors=None, newline=None):
        if mode not in ("r", "rt", "rb"):
            raise Refusal(f"write to immutable factor input refused: {self.path}")
        if _WINDOW is not None:
            _WINDOW["reads"].append(str(self.path))
        binary = io.BytesIO(self.raw)
        return binary if mode == "rb" else io.TextIOWrapper(binary, encoding=encoding, errors=errors, newline=newline)


class _FrozenFactorFolder:
    def __init__(self, root: pathlib.Path, files: dict):
        self.root, self.files = root, MappingProxyType(dict(files))

    def __fspath__(self):
        return str(self.root)

    def __truediv__(self, name):
        path = self.root / name
        if norm(path) not in self.files:
            raise Refusal(f"forecast requested an unpinned factor file: {path}")
        return _FrozenFactorFile(path, self.files[norm(path)])


class _FrozenFactorSource:
    def __init__(self, original, folder):
        self.original, self.FACTORS = original, folder

    def __getattr__(self, name):
        return getattr(self.original, name)


def _audit_hook(event, args):
    try:
        if event != "open" or _WINDOW is None or not args:
            return
        path = args[0]
        if isinstance(path, int):
            return
        mode = args[1] if len(args) > 1 else None
        flags = args[2] if len(args) > 2 else 0
        writing = (isinstance(mode, str) and any(ch in mode for ch in "wxa+")) or (
            mode is None and isinstance(flags, int)
            and flags & (os.O_WRONLY | os.O_RDWR | os.O_APPEND | os.O_CREAT | os.O_TRUNC))
        _WINDOW["writes" if writing else "reads"].append(os.fsdecode(os.fspath(path)))
    except Exception:          # an audit hook must never raise into the reader
        if _WINDOW is not None:
            _WINDOW["hookErrors"] += 1


def pin_problems() -> list[str]:
    problems = []
    for name, (relative, want) in PINNED_MODULES.items():
        path = SCRATCH / relative
        if not path.is_file() or sha256_file(path) != want:
            problems.append(f"pinned reader {relative} missing or SHA-256 changed")
    for path, want in PINNED_DATA.items():
        if not pathlib.Path(path).is_file() or sha256_file(path) != want:
            problems.append(f"pinned data file {path} missing or SHA-256 changed")
    return problems


def readers():
    """Execute retained SHA-verified source for all ten helpers (including their transitive imports),
    bypass caches, and route the unchanged forecast CSV parser through immutable verified bytes.
    Memoise audit_vep.load as Stage 1a and arm the read audit hook. Interpreter/OS code remains trusted."""
    global _READERS
    if _READERS is not None:
        return _READERS
    sources = {name: (SCRATCH / relative, verified_bytes(SCRATCH / relative, digest))
               for name, (relative, digest) in PINNED_MODULES.items()}
    data = {norm(path): verified_bytes(path, digest) for path, digest in PINNED_DATA.items()}
    existing = sorted(set(PINNED_MODULES) & set(sys.modules))
    if existing:
        raise Refusal(f"pinned reader modules already loaded without this source verifier: {existing}")
    for path in READER_PATHS:
        sys.path.insert(0, str(path))
    before = set(sys.modules)
    executed = {}
    finder = _VerifiedSourceFinder(sources, executed)
    sys.meta_path.insert(0, finder)
    try:
        audit_vep = importlib.import_module("audit_vep")
        display_layer = importlib.import_module("display_layer")
        cdc = importlib.import_module("cumulative_decisive_check")
        for name in PINNED_MODULES:
            importlib.import_module(name)
    finally:
        sys.meta_path.remove(finder)
    pinned_paths = {norm(SCRATCH / relative): name for name, (relative, _) in PINNED_MODULES.items()}
    for name, (relative, _) in PINNED_MODULES.items():
        module = sys.modules.get(name)
        if module is None or executed.get(name) is not module or norm(getattr(module, "__file__", "")) != norm(SCRATCH / relative):
            raise Refusal(f"reader module {name} not loaded from its pinned path {relative}")
    interpreter = (norm(sys.base_prefix) + os.sep, norm(sys.prefix) + os.sep)
    for name in sorted(set(sys.modules) - before):
        file = getattr(sys.modules[name], "__file__", None)
        if file and norm(file) not in pinned_paths and not norm(file).startswith(interpreter):
            where = "scratchpad" if norm(file).startswith(norm(SCRATCH)) else "external"
            raise Refusal(f"the readers loaded an unpinned {where} module {name} ({file})")
    if norm(sys.modules["preflight_world"].FACTORS) != norm(FACTORS):
        raise Refusal("preflight_world.FACTORS is not the pinned factor folder")
    factors = {path: raw for path, raw in data.items() if norm(path).startswith(norm(FACTORS) + os.sep)}
    # Only the v1.3 parser's source object is adapted; its profile/bin arithmetic executes unchanged source.
    v13 = sys.modules["preflight_world_v1_3"]
    v13.preflight_world = _FrozenFactorSource(v13.preflight_world, _FrozenFactorFolder(FACTORS, factors))
    original = audit_vep.load
    cache: dict = {}

    def cached_load(path):
        key = str(path)
        if key not in cache:
            cache.clear()
            cache[key] = original(path)
        return cache[key]

    audit_vep.load = cached_load        # audit() resolves load() through the module global (as Stage 1a)
    sys.addaudithook(_audit_hook)
    _READERS = {"audit_vep": audit_vep, "display_layer": display_layer, "cdc": cdc, "cache": cache,
                "verifiedSources": MappingProxyType(dict(executed)), "verifiedData": MappingProxyType(data)}
    return _READERS


def window_problems(window: dict, folder: pathlib.Path) -> list[str]:
    allowed_dirs = [norm(folder) + os.sep, norm(sys.base_prefix) + os.sep, norm(sys.prefix) + os.sep]
    allowed_files = {norm(p) for p in PINNED_DATA} | {norm(SCRATCH / r) for r, _ in PINNED_MODULES.values()}
    problems = [f"file opened for writing while reading {folder.name}: {p}" for p in window["writes"]]
    for path in sorted(set(window["reads"])):
        n = norm(path)
        if n not in allowed_files and not any(n.startswith(d) for d in allowed_dirs):
            problems.append(f"unpinned file read while reading {folder.name}: {path}")
    if window["hookErrors"]:
        problems.append(f"read audit hook failed {window['hookErrors']} time(s) while reading {folder.name}")
    return problems


# ------------------------------------------------------------------------------------------ expected runs

def expected_run(source: str, cell: str, v3_dll: str | None) -> dict:
    arm = SOURCE_ARM[source]
    if source.startswith("CF-"):
        job = f"CF-{arm}-{cell}"
        return {"folder": str(CF_ROOT / job), "label": f"{job}-s1b", "job": job, "policy": POLICY_BINDING[source],
                "dll": v3_dll, "pin": None}
    if source == "SF-C120":
        name = f"C-120-{cell}"
        return {"folder": str(C120_ROOT / name), "label": f"{name}-t38", "job": name, "policy": POLICY_BINDING[source],
                "dll": OLD_RUNNER_DLL, "pin": None}
    name = sf_effective(SF_ROOT, f"{arm}-{cell}")
    return {"folder": str(SF_ROOT / name), "label": f"{name}-t38", "job": name, "policy": POLICY_BINDING[source],
            "dll": OLD_RUNNER_DLL, "pin": SF_BUNDLE_PINS.get(name)}


def folder_problems(spec: dict) -> list[str]:
    """Missing / failed run (no outcome is read): folder, file set, summary status and label, run-00 status."""
    folder, job = pathlib.Path(spec["folder"]), spec["job"]
    if not folder.is_dir():
        return [f"{job}: MISSING (no folder {folder})"]
    problems = []
    names = set(os.listdir(folder))
    if names - FOLDER_FILES:
        problems.append(f"{job}: FAILED or foreign run folder (unexpected {sorted(names - FOLDER_FILES)})")
    if FOLDER_FILES - names:
        problems.append(f"{job}: FAILED / incomplete run folder (missing {sorted(FOLDER_FILES - names)})")
        return problems
    try:
        summary = json.loads((folder / "summary.json").read_text(encoding="utf-8"))
        run00 = json.loads((folder / "run-00.json").read_text(encoding="utf-8"))
    except (OSError, ValueError) as error:
        return problems + [f"{job}: FAILED (unreadable summary / run-00: {type(error).__name__})"]
    if summary.get("status") != "pass":
        problems.append(f"{job}: FAILED (summary status {summary.get('status')!r})")
    if run00.get("status") != "succeeded":
        problems.append(f"{job}: FAILED (run-00 status {run00.get('status')!r})")
    if summary.get("label") != spec["label"] or run00.get("label") != spec["label"]:
        problems.append(f"{job}: label {summary.get('label')!r} / {run00.get('label')!r} is not {spec['label']!r}")
    if spec["pin"] is None and spec["job"].startswith(("U-", "C-30-")):
        problems.append(f"{job}: no SF bundle pin for this archived folder")
    return problems


def cf_log_problems(job: str, binding_sha: str | None, log_root: pathlib.Path = LOG_MAIN) -> list[str]:
    """Main job log (returncode 0, admitted binding) and admission anchor (plan s12; run_stage1b-v7.py:995,1254)."""
    problems = []
    log, anchor = log_root / f"{job}.log", log_root / f"{job}.admitted"
    if not log.is_file():
        problems.append(f"{job}: no main job log {log.name}")
    else:
        first = log.read_text(encoding="utf-8", errors="replace").split("\n", 1)[0]
        match = LOG_FIRST_LINE.match(first)
        if not match:
            problems.append(f"{job}: main job log first line malformed: {first[:80]!r}")
        else:
            if int(match.group(1)) != 0:
                problems.append(f"{job}: FAILED (main job log returncode={match.group(1)})")
            if match.group(2) != binding_sha:
                problems.append(f"{job}: main job log names another analysis binding")
    if not anchor.is_file():
        problems.append(f"{job}: no admission anchor {anchor.name}")
    elif anchor.read_text(encoding="utf-8", errors="replace").split("\n", 1)[0] != f"analysis-binding {binding_sha}":
        problems.append(f"{job}: admission anchor names another analysis binding")
    return problems


def unexpected_main_entries(expected_jobs: set[str], root: pathlib.Path = CF_ROOT) -> list[str]:
    if not root.is_dir():
        return []
    return [f"main output folder holds an unexpected entry {name!r} (rerun or foreign output)"
            for name in sorted(set(os.listdir(root)) - expected_jobs)]


def binding_inventory(text: str) -> tuple[str | None, dict[str, str]]:
    manifest, inventory = None, {}
    for line in text.splitlines():
        if not line or line.startswith("#"):
            continue
        match = BINDING_MANIFEST.match(line)
        if match and manifest is None:
            manifest = match.group(1)
            continue
        match = BINDING_LINE.match(line)
        if match:
            if match.group(2) in inventory:
                raise Refusal(f"{BINDING.name}: duplicate inventory entry {match.group(2)}")
            inventory[match.group(2)] = match.group(1)
            continue
        raise Refusal(f"{BINDING.name}: malformed line {line[:80]!r}")
    return manifest, inventory


def analysis_inventory(text: str) -> dict[str, str]:
    inventory = {}
    for line in text.splitlines():
        if not line or line.startswith("#"):
            continue
        match = BINDING_LINE.fullmatch(line)
        if not match or match.group(2) in inventory:
            raise Refusal(f"{FREEZE.name}: malformed or duplicate line {line[:80]!r}")
        inventory[match.group(2)] = match.group(1)
    return inventory


def runner_receipt_identity(raw: bytes) -> dict:
    """Authenticate the sealed manifest's tree and DLL identities for receipt links.
    No Runner code executes in analysis; this does not rehash the unused live published Runner directory."""
    digest = hashlib.sha256(raw).hexdigest()
    if digest != RUNNER_V3_MANIFEST_SHA256:
        raise Refusal(f"{RUNNER_V3_MANIFEST.name}: differs from the manifest in the sealed plan")
    lines = raw.decode("utf-8").splitlines()
    match = re.fullmatch(r"# tree-sha256 ([0-9a-f]{64}) files (\d+)", lines[0] if lines else "")
    if not match or len(lines) - 1 != int(match.group(2)):
        raise Refusal(f"{RUNNER_V3_MANIFEST.name}: malformed tree header/file count")
    body = lines[1:]
    if hashlib.sha256("\n".join(body).encode("utf-8")).hexdigest() != match.group(1):
        raise Refusal(f"{RUNNER_V3_MANIFEST.name}: tree body SHA-256 differs from header")
    dll = [line.split("\t")[1] for line in body if line.startswith("RideBound.Runner.dll\t")]
    if len(dll) != 1 or not re.fullmatch(r"[0-9a-f]{64}", dll[0]) or dll[0] == OLD_RUNNER_DLL:
        raise Refusal(f"{RUNNER_V3_MANIFEST.name}: no unique v3 Runner DLL identity")
    return {"dll": dll[0], "tree": match.group(1), "files": len(body), "manifestSha256": digest}


def receipt_problems(raw: bytes, name: str, word: str, identity: dict | None) -> list[str]:
    """Same verdict terminator/build-link conditions as run_stage1b-v7.py:768-780."""
    lines, problems = raw.decode("utf-8").splitlines(), []
    if not lines or lines[-1] != f"{word} PASSED":
        problems.append(f"{name} does not end with '{word} PASSED'")
    if identity is None or any(f"{field} {identity[key]}" not in lines for field, key in
                               (("runnerDllSha256", "dll"), ("runnerTreeSha256", "tree"))):
        problems.append(f"{name} was not written for the sealed Runner build")
    return problems


def _latches(harness: pathlib.Path) -> list[str]:
    return sorted(p.name for p in harness.iterdir() if p.is_file() and
                  (p.name.endswith("-FAILED.txt") or p.name == DRIFT_STOP))


def study_state(harness: pathlib.Path = HERE) -> tuple[dict, list[str]]:
    """Authenticate prerequisite content/build links and the entire retained analysis inventory.
    Exact receipt bytes are hashed and parsed from the same read; dry runs only record these facts."""
    state, problems = {}, []
    latches = _latches(harness)
    state["latches"] = latches
    if latches:
        problems.append(f"stop / failure receipt(s) present: {latches} (owner review required; no analysis)")
    raw_files = {}
    for name in STUDY_FILES + (BINDING.name, FREEZE.name, RUNNER_V3_MANIFEST.name):
        path = harness / name
        try:
            raw_files[name] = path.read_bytes()
            digest = hashlib.sha256(raw_files[name]).hexdigest()
        except OSError as error:
            digest = None
            problems.append(f"{name} cannot be read ({type(error).__name__})")
        if name in STUDY_FILES:
            state[name] = digest
        if digest is None:
            problems.append(f"{name} does not exist")
    identity = None
    if RUNNER_V3_MANIFEST.name in raw_files:
        try:
            identity = runner_receipt_identity(raw_files[RUNNER_V3_MANIFEST.name])
        except (Refusal, ValueError, UnicodeError) as error:
            problems.append(str(error))
    state["runnerIdentity"] = identity
    for name, word in ((STUDY_FILES[0], "GATE0 BEHAVIOURAL"), (STUDY_FILES[1], "SMOKE")):
        if name in raw_files:
            try:
                problems += receipt_problems(raw_files[name], name, word, identity)
            except UnicodeError:
                problems.append(f"{name}: invalid UTF-8")
    if STUDY_FILES[2] in raw_files:
        try:
            lines = raw_files[STUDY_FILES[2]].decode("utf-8").splitlines()
            if state[STUDY_FILES[1]] is None or f"smokeVerdictSha256 {state[STUDY_FILES[1]]}" not in lines:
                problems.append(f"{STUDY_FILES[2]} does not name the SHA-256 of {STUDY_FILES[1]}")
            if "inspection: accepted" not in lines:
                problems.append(f"{STUDY_FILES[2]} does not record 'inspection: accepted'")
            if not any(line.startswith("inspected-by: ") and line[len("inspected-by: "):].strip() for line in lines):
                problems.append(f"{STUDY_FILES[2]} does not name the inspector")
        except UnicodeError:
            problems.append(f"{STUDY_FILES[2]}: invalid UTF-8")
    state["bindingSha256"] = (hashlib.sha256(raw_files[BINDING.name]).hexdigest()
                              if BINDING.name in raw_files else None)
    state["analysisManifestSha256"] = (hashlib.sha256(raw_files[FREEZE.name]).hexdigest()
                                      if FREEZE.name in raw_files else None)
    if BINDING.name in raw_files:
        try:
            manifest, inventory = binding_inventory(raw_files[BINDING.name].decode("utf-8"))
        except (Refusal, UnicodeError) as error:
            problems.append(str(error))
            manifest, inventory = None, {}
        state["bindingInventory"] = inventory
        if manifest is None or state["analysisManifestSha256"] != manifest:
            problems.append(f"{FREEZE.name} missing or not the manifest named by {BINDING.name}")
        if FREEZE.name in raw_files:
            try:
                if analysis_inventory(raw_files[FREEZE.name].decode("utf-8")) != inventory:
                    problems.append(f"{FREEZE.name}: inventory differs from the retained binding")
            except (Refusal, UnicodeError) as error:
                problems.append(str(error))
        required = ("extract_stage1b.py", "report_stage1b.py", RUNNER_V3_MANIFEST.name,
                    "STAGE1B-ANALYSIS-AMENDMENT.md")
        for name in required:
            if name not in inventory:
                problems.append(f"{name} is not listed in {BINDING.name}")
        verified_inventory = {}
        for name, digest in sorted(inventory.items()):
            try:
                verified_inventory[name] = verified_bytes(harness / name, digest)
            except (OSError, Refusal):
                problems.append(f"bound analysis file {name} changed or missing since {BINDING.name}")
        try:
            lines = verified_inventory["STAGE1B-ANALYSIS-AMENDMENT.md"].decode("utf-8").splitlines()
            if "inventory-coverage: complete" not in lines or not any(
                    line.startswith("inventory-reviewed-by: ") and line[len("inventory-reviewed-by: "):].strip()
                    for line in lines):
                problems.append("STAGE1B-ANALYSIS-AMENDMENT.md: inventory review/coverage missing")
        except (KeyError, UnicodeError):
            problems.append("STAGE1B-ANALYSIS-AMENDMENT.md: unreadable inventory review")
    # A durable stop can arrive during the receipt/inventory scan itself.
    state["latches"] = _latches(harness)
    if state["latches"] and state["latches"] != latches:
        problems.append(f"stop / failure receipt(s) appeared during validation: {state['latches']}")
    return state, problems


def require_main_state(expected: dict | None = None, harness: pathlib.Path = HERE) -> dict:
    """Shared extractor/report authentication. Refuse current defects and ANY held evidence drift.
    This read-only check neither rewrites receipts nor authorizes clearing an existing durable stop."""
    state, problems = study_state(harness)
    if expected is not None and state != expected:
        changed = sorted(key for key in set(expected) | set(state) if expected.get(key) != state.get(key))
        problems.append(f"study evidence changed since the retained extraction snapshot: {changed}")
    if problems:
        raise Refusal("current study state:\n  - " + "\n  - ".join(problems))
    return state


def recorded_study_state() -> dict:
    """Dry run: the same facts, recorded only."""
    state, problems = study_state()
    state["problemsNotEnforcedInDryRun"] = problems
    return state


# ------------------------------------------------------------------------------------------ one run

def verify_bundle(folder: pathlib.Path, pin: str | None) -> dict:
    raw = (folder / "bundle-manifest.json").read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if pin is not None and digest != pin:
        raise Refusal(f"{folder.name}: bundle-manifest.json SHA-256 {digest[:12]} is not the pinned {pin[:12]}")
    files = json.loads(raw.decode("utf-8")).get("files", [])
    listed = {item.get("path"): item for item in files}
    if sorted(map(str, listed)) != sorted(BUNDLE_FILES) or len(files) != len(BUNDLE_FILES):
        raise Refusal(f"{folder.name}: bundle-manifest lists {sorted(map(str, listed))}")
    out = {"bundle-manifest.json": digest}
    for name in BUNDLE_FILES:
        path = folder / name
        if path.stat().st_size != listed[name].get("lengthBytes") or sha256_file(path) != listed[name].get("sha256"):
            raise Refusal(f"{folder.name}: {name} does not match bundle-manifest.json")
        out[name] = listed[name]["sha256"]
    return out


def own_prune_classes(decisions, acknowledged):
    """Unchanged from extract_stage1a.py:65-97: cumulative_decisive_check's class rule, plus the drop limits seen."""
    cls_count = collections.Counter()
    limits = collections.Counter()
    cum_detail = []
    for h, payload in decisions.items():
        if h not in acknowledged:
            continue
        ev = ((payload.get("solver") or {}).get("executionEvidence") or {})
        for pc in ev.get("prunedCandidates", []):
            if pc.get("code") != "COMMITMENT_BUDGET_EXCEEDED":
                continue
            ws = [w for w in pc.get("commitmentWitnesses", []) if w.get("code") == "COMMITMENT_BUDGET_EXCEEDED"]
            drop = [w for w in ws if w.get("dimension") == "drop_eta_total_ms"]
            other = [w for w in ws if w.get("dimension") != "drop_eta_total_ms"]
            for w in drop:
                limits[w["limit"]] += 1
            if drop:
                if any(w["delta"] > w["limit"] for w in drop):
                    cls = "per-step-also"
                elif all(w["before"] > 0 and w["delta"] <= w["limit"] < w["after"] for w in drop):
                    cls = "cumulative-only"
                    cum_detail.append({"decisionHash": h, "newRequestIds": pc.get("newRequestIds", []),
                                       "witness": [(w["requestId"], w["before"], w["delta"], w["after"], w["limit"])
                                                   for w in drop]})
                else:
                    cls = "unclassified"
            elif other:
                cls = "lock-like:" + ",".join(sorted({w["dimension"] for w in other}))
            else:
                cls = "no-witness"
            cls_count[cls] += 1
    return dict(cls_count), {str(k): v for k, v in limits.items()}, cum_detail


def identity_problems(spec: dict, summary: dict, run00: dict, manifest: dict) -> list[str]:
    job, problems = spec["job"], []
    checks = [
        ("summary status", summary.get("status"), "pass"), ("repeatCount", summary.get("repeatCount"), 1),
        ("summary label", summary.get("label"), spec["label"]), ("run-00 label", run00.get("label"), spec["label"]),
        ("repository inventory", summary.get("repositoryInventorySha256"), INVENTORY),
        ("run-00 status", run00.get("status"), "succeeded"), ("run-00 repeat", run00.get("repeat"), 0),
        ("artifactReceiptsEqual", run00.get("artifactReceiptsEqual"), True),
        ("requestCount", (run00.get("semantic") or {}).get("requestCount"), REQUESTS_PER_RUN),
        ("run-00 semanticHash = summary semanticHash", run00.get("semanticHash"), summary.get("semanticHash")),
        ("source fixture hash (summary = run-00 semantic)", summary.get("sourceScenarioContentSha256"),
         (run00.get("semantic") or {}).get("sourceScenarioContentSha256")),
        ("policyConfigurationHash", manifest.get("policyConfigurationHash"), spec["policy"]),
        ("binarySha256", manifest.get("binarySha256"), spec["dll"]),
        ("coreCommitSha", manifest.get("coreCommitSha"), CORE_COMMIT), ("masterSeed", manifest.get("masterSeed"), SEED),
        ("policyId", manifest.get("policyId"), POLICY_ID), ("policyVersion", manifest.get("policyVersion"), POLICY_VERSION),
    ]
    for what, got, want in checks:
        if got != want or type(got) is not type(want):
            problems.append(f"{job}: {what} is {got!r}, expected {want!r}")
    runs = summary.get("runs")
    if not isinstance(runs, list) or len(runs) != 1 or runs[0].get("semanticHash") != run00.get("semanticHash"):
        problems.append(f"{job}: summary runs receipt does not hold exactly run 0 with the run-00 semanticHash")
    if not (isinstance(run00.get("semanticHash"), str) and re.fullmatch(r"[0-9a-f]{64}", run00["semanticHash"])):
        problems.append(f"{job}: run-00 semanticHash is not a SHA-256 hex digest")
    return problems


def extract_run(task):
    """One run folder -> (rider rows, run record). Raises Refusal on any problem."""
    global _WINDOW
    source, cell, spec = task
    R = readers()
    folder = pathlib.Path(spec["folder"])
    transcript = folder / "transcript-00.ndjson"
    _WINDOW = {"reads": [], "writes": [], "hookErrors": 0}
    try:
        stat_before = transcript.stat()
        hashes = verify_bundle(folder, spec["pin"])
        summary_json = json.loads((folder / "summary.json").read_text(encoding="utf-8"))
        run00 = json.loads((folder / "run-00.json").read_text(encoding="utf-8"))
        manifest = initialize_manifest(transcript)
        problems = identity_problems(spec, summary_json, run00, manifest)
        if problems:
            raise Refusal("; ".join(problems))
        decisions, acknowledged, state = R["audit_vep"].load(transcript)
        table, summary = R["audit_vep"].audit(transcript)
        if table is None:
            raise Refusal(f"{folder}: no final checkpoint")
        _, alighted = event_times(transcript)
        prof = R["display_layer"].profile(day_of(cell), cell[-3:], "causal-no15")
        judged = R["cdc"].analyze(("stage1b", source, transcript))
        stat_after = transcript.stat()
    finally:
        window, _WINDOW = _WINDOW, None
        R["cache"].clear()
    problems = window_problems(window, folder)
    interpreter = (norm(sys.base_prefix) + os.sep, norm(sys.prefix) + os.sep)
    outside = sorted({os.path.basename(p) for p in window["reads"] if not norm(p).startswith(norm(folder) + os.sep)
                      and not norm(p).startswith(interpreter)})
    read_window = {"opens": len(window["reads"]), "runFolderOpens": sum(norm(p).startswith(norm(folder) + os.sep)
                                                                        for p in window["reads"]),
                   "pinnedFilesRead": outside}       # proves the hook was live; outside = pinned data files only
    if (stat_before.st_size, stat_before.st_mtime_ns) != (stat_after.st_size, stat_after.st_mtime_ns):
        problems.append(f"{folder}: transcript changed while it was read")
    if problems:
        raise Refusal("; ".join(problems))
    # ---- per rider (extract_stage1a.py:112-149, plus maxAbsC and the ledger budget cross-check)
    first, s_drop, k_drop, net = {}, collections.defaultdict(int), collections.defaultdict(int), collections.defaultdict(int)
    max_c, c_by_seq = collections.defaultdict(int), {}
    first_kind_check = {}
    for r, coord, seq, kind, pub, v, e, p, x, cc, z, closed in table:
        if coord != "drop":
            continue
        first_kind_check.setdefault(r, (seq, kind))
        if seq < first_kind_check[r][0]:
            first_kind_check[r] = (seq, kind)
        c_by_seq[(r, seq)] = 0 if kind == "initialPromise" else cc
        if kind == "initialPromise":
            if r in first:
                raise Refusal(f"{folder}: {r} has two drop initialPromise rows")
            first[r] = (pub, p)
            continue
        s_drop[r] += abs(cc)
        k_drop[r] += cc != 0
        net[r] += cc
        max_c[r] = max(max_c[r], abs(cc))
    bad_first = [r for r, (seq, kind) in first_kind_check.items() if kind != "initialPromise"]
    if bad_first:
        raise Refusal(f"{folder}: first drop entry not initialPromise for {bad_first[:3]}")
    if sum(s_drop.values()) != summary["sumDropC"]:
        raise Refusal(f"{folder}: per-rider sum |c| {sum(s_drop.values())} != audit sumDropC {summary['sumDropC']}")
    budget_mismatches = []      # cross-check with the Runner's own drop budget accounting (recorded, reported in H1)
    for history in state["commitmentLedger"]:
        r, spent = history["requestId"], 0
        for seq, entry in enumerate(history["entries"]):
            before, after = entry["budgetBefore"]["dropEtaTotalMs"], entry["budgetAfter"]["dropEtaTotalMs"]
            if before != spent:
                budget_mismatches.append([r, seq, "budgetBefore", before, spent])
            spent += abs(c_by_seq[(r, seq)])
            if after != spent:
                budget_mismatches.append([r, seq, "budgetAfter", after, spent])
        if r in first and spent != s_drop[r]:
            raise Refusal(f"{folder}: {r} ledger running sum {spent} != per-rider sum |c| {s_drop[r]}")
    reqs = state["requests"]
    ids = [q["requestId"] for q in reqs]
    if len(ids) != len(set(ids)) or len(ids) != REQUESTS_PER_RUN or summary["requests"] != REQUESTS_PER_RUN:
        raise Refusal(f"{folder}: {len(ids)} requests ({len(set(ids))} distinct), expected {REQUESTS_PER_RUN}")
    life = {q["requestId"]: q["lifecycle"] for q in reqs}
    completed = {r for r, value in life.items() if value == "completed"}
    served = completed & set(alighted)
    if served - set(first):
        raise Refusal(f"{folder}: served riders without a drop initialPromise {sorted(served - set(first))[:3]}")
    legs = collections.defaultdict(list)
    for q in reqs:
        if q["requestId"] in alighted:
            legs[q["assignedVehicleId"]].append((q["actualPickupTimeMs"], alighted[q["requestId"]], q["requestId"]))
    shared = set()
    for rs in legs.values():
        for a0, a1, rid in rs:
            if any(o != rid and b0 < a1 and a0 < b1 for b0, b1, o in rs):
                shared.add(rid)
    rows = []
    for r in sorted(first):
        t0, p0 = first[r]
        d0 = R["display_layer"].display(t0, p0, prof)
        rows.append([cell, r, life.get(r, ""), t0, p0, repr(d0), alighted.get(r, ""), int(r in served), s_drop[r],
                     k_drop[r], net[r], max_c[r], int(r in shared)])
    own_cls, limits, cum_detail = own_prune_classes(decisions, acknowledged)
    non_normal = sorted(h for h, payload in decisions.items() if h in acknowledged
                        and (payload.get("certificate", {}).get("body") or {}).get("normalOperation") is False)
    breaches = state["incidentLedger"]["breaches"]
    to_explain = sorted([b.get("requestId"), b.get("kind", "operationalIncident")] for b in breaches
                        if b.get("kind", "operationalIncident") != NOT_FORCED_BREACH)
    rec = {
        "source": source, "cell": cell, "run": str(folder), "job": spec["job"], "label": summary_json.get("label"),
        "readWindow": read_window,
        "bundleSha256": hashes, "summaryStatus": summary_json.get("status"), "run00Status": run00.get("status"),
        "repositoryInventorySha256": summary_json.get("repositoryInventorySha256"),
        "sourceScenarioContentSha256": summary_json.get("sourceScenarioContentSha256"),
        "manifest": {k: manifest.get(k) for k in MANIFEST_KEYS},
        "requests": summary["requests"],
        "requestIdsSha256": hashlib.sha256("\n".join(sorted(ids)).encode("utf-8")).hexdigest(),
        "lifecycle": summary["lifecycle"], "promisedRiders": summary["promisedRiders"],
        "alighted": len(alighted), "served": len(served), "completedWithoutAlight": len(completed - set(alighted)),
        "alightedNotCompleted": len(set(alighted) - completed),
        "alightedWithoutPromise": sum(1 for r in alighted if r not in first),
        "decisionsAcknowledged": summary["decisionsAcknowledged"], "decisionFrames": len(decisions),
        "nonNormalCertificates": summary["nonNormalCertificates"], "nonNormalDecisionHashes": non_normal,
        "ridersWithForcedBreach": summary["ridersWithForcedBreach"], "breaches": summary["breaches"],
        "breachRecordsToExplain": to_explain, "incidentEntries": len(state["incidentLedger"].get("incidents", [])),
        "ledgerBudgetMismatches": budget_mismatches,
        "latePickups": summary["latePickups"], "sumDropC": summary["sumDropC"], "forecastActive": prof is not None,
        "pruneClassesOwn": own_cls, "dropWitnessLimits": limits, "cumulativeOnlyOwn": cum_detail,
        "pruneClassesJudge": judged["candidates"], "judgeAckedDecisions": judged["acked_decisions"],
        "judgeDecisionsWithCumulativeOnly": judged["decisions_with_cumulative_only"],
        "judgeRequestsWithCumulativeOnly": judged["requests_with_cumulative_only_candidate"],
        "judgeOfWhichFinallyRejected": judged["of_which_finally_rejected"], "judgeServed": judged["served"],
        "judgeExamples": judged["examples"],
    }
    return rows, rec


# ------------------------------------------------------------------------------------------ whole extraction

def cross_run_problems(records: dict, cells: list[str], role_sources: dict) -> list[str]:
    """Per cell, the four roles must describe the same episode (same fixture, snapshots and request set)."""
    problems = []
    for cell in cells:
        recs = {role: records[(role_sources[role], cell)] for role in ROLES}
        for key in CELL_EQUAL_KEYS:
            if len({rec["manifest"][key] for rec in recs.values()}) != 1:
                problems.append(f"{cell}: {key} differs across roles")
        for key in ("sourceScenarioContentSha256", "requestIdsSha256", "requests"):
            if len({rec[key] for rec in recs.values()}) != 1:
                problems.append(f"{cell}: {key} differs across roles")
    return problems


def extract(mode: str, role_sources: dict, prefix: str) -> int:
    riders_path, runs_path = pathlib.Path(f"{prefix}-riders.tsv"), pathlib.Path(f"{prefix}-runs.json")
    for path in (riders_path, runs_path):
        if path.exists():
            raise Refusal(f"output {path} exists (mode 'x'; outputs are never overwritten)")
        if any(norm(path).startswith(norm(root) + os.sep) for root in OUTPUT_FORBIDDEN):
            raise Refusal(f"output {path} lies under an input root {OUTPUT_FORBIDDEN}")
    cells = load_cells()
    sources = sorted(set(role_sources.values()))
    problems = []
    if mode == "main":
        if norm(sys.executable) != norm(JOB_PYTHON):
            raise Refusal(f"main mode must run under the frozen job interpreter {JOB_PYTHON} (not {sys.executable})")
        study = require_main_state()
        binding_sha = study["bindingSha256"]
    else:
        study, binding_sha = recorded_study_state(), None
    v3_dll = runner_v3_dll() if any(s.startswith("CF-") for s in sources) else None
    specs = {(s, c): expected_run(s, c, v3_dll) for s in sources for c in cells}
    for spec in specs.values():
        problems += folder_problems(spec)
    if any(s.startswith("CF-") for s in sources):
        problems += unexpected_main_entries({spec["job"] for (s, _), spec in specs.items() if s.startswith("CF-")})
        for (s, _), spec in specs.items():
            if s.startswith("CF-") and pathlib.Path(spec["folder"]).is_dir():   # a missing run is already reported
                problems += cf_log_problems(spec["job"], binding_sha)
    missing = sum(1 for p in problems if "MISSING" in p)
    if problems:
        raise Refusal(f"{len(problems)} problem(s) before any outcome was read ({missing} missing run(s)):\n  - "
                      + "\n  - ".join(problems))
    readers()                         # fail early in the parent: pins, imports, closure
    tasks = [(s, c, specs[(s, c)]) for s in sources for c in cells]
    with ProcessPoolExecutor(max_workers=WORKERS) as pool:
        results = dict(zip([(s, c) for s, c, _ in tasks], pool.map(extract_run, tasks)))
    records = {key: rec for key, (_, rec) in results.items()}
    problems = cross_run_problems(records, cells, role_sources)
    if problems:
        raise Refusal("cross-role identity:\n  - " + "\n  - ".join(problems))
    lines = ["role\tcell\trequestId\tlifecycle\tt0\tp0\tD0\tA\tserved\tsumAbsC\tnonzeroC\tnetC\tmaxAbsC\tshared\n"]
    runs = []
    for role in ROLES:
        for cell in cells:
            rows, rec = results[(role_sources[role], cell)]
            lines += ["\t".join(str(x) for x in [role] + row) + "\n" for row in rows]
            runs.append(dict(rec, role=role))
    riders_bytes = "".join(lines).encode("utf-8")
    document = {
        "schema": "stage1b-extract-v1", "mode": mode, "dryRun": mode != "main", "roleSources": role_sources,
        "extractor": {"file": pathlib.Path(__file__).name, "sha256": sha256_file(__file__)},
        "python": {"executable": sys.executable, "version": sys.version},
        "pinnedModules": {name: sha for name, (_, sha) in PINNED_MODULES.items()},
        "pinnedData": PINNED_DATA, "sfBundlePins": len(SF_BUNDLE_PINS), "runnerV3Dll": v3_dll,
        "studyState": study, "cells": cells,
        "ridersTsv": {"file": riders_path.name, "sha256": hashlib.sha256(riders_bytes).hexdigest(),
                      "rows": len(lines) - 1},
        "runs": runs,
    }
    # All output bytes are prepared before the final current-state/inventory checks. Nothing is written on a refusal.
    runs_bytes = (json.dumps(document, indent=1) + "\n").encode("utf-8")
    problems = pin_problems()
    if mode == "main":
        problems += unexpected_main_entries({spec["job"] for (s, _), spec in specs.items() if s.startswith("CF-")})
        for (source, _), spec in specs.items():
            if source.startswith("CF-"):
                problems += cf_log_problems(spec["job"], binding_sha)
    if problems:
        raise Refusal("final input verification before extraction output:\n  - " + "\n  - ".join(problems))
    if mode == "main":
        require_main_state(study)       # after ALL slow pin/log/anchor scans, immediately before output creation
    with open(riders_path, "xb") as handle:
        handle.write(riders_bytes)
    with open(runs_path, "xb") as handle:
        handle.write(runs_bytes)
    print(f"{'DRY RUN (stand-ins ' + str(role_sources) + '): ' if mode != 'main' else ''}written {riders_path} "
          f"({len(lines) - 1} rider rows) and {runs_path} ({len(runs)} role-runs, {len(results)} distinct folders)")
    return 0


def parse(argv: list[str]):
    if len(argv) == 3 and argv[1] == "main":
        return "main", {role: role for role in ROLES}, argv[2]
    if len(argv) == 5 and argv[1] == "dryrun" and argv[2] in STANDINS and argv[3] in STANDINS:
        return "dryrun", {"CF-U": argv[2], "CF-C30": argv[3], "SF-U": "SF-U", "SF-C30": "SF-C30"}, argv[4]
    raise SystemExit(__doc__)


def main(argv: list[str]) -> int:
    mode, role_sources, prefix = parse(argv)
    try:
        return extract(mode, role_sources, prefix)
    except Refusal as error:
        print(f"REFUSED ({mode}); nothing written:\n{error}", flush=True)
        return 2
    except Exception:                  # an unexpected reader error is not a result either; nothing is written
        import traceback
        print(f"ERROR ({mode}); nothing written:\n{traceback.format_exc()}", flush=True)
        return 3


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
