"""Stage-1b Gate-0 comparator, VERSION 2 (fixes Codex R24, optimize/codex-debate/codex-work/
R24-HARNESS-REVIEW-CODEX.md F1, F3, F4). The reviewed v1 (compare_equivalence_stage1b.py) is kept unchanged.

Unchanged from v1: the exact criterion of Codex R22 §5 (R22-CODE-REVIEW-CODEX.md:158-222), built as a wrapper around
the archived readers; the eleven compared components; the narrow normalization (binarySha256 token at exactly
('adapterToRunner', initializeRun, payload.manifest) for the verified DLL only; STABLE hash keys raw; DERIVED hash keys
renamed by first appearance per run; salted run/publication/breach identities mapped to ledger records; any other hash
field fails). Raw cross-run digest equality stays waived (R22:192; R24:66).

New in v2:
  F1  Per-run protocol DIGEST VALIDITY, checked before the cross-run comparison (R24:66; R22:173,190). Recomputed from
      the transcript with the Runner's own formulas (C# file:line in C:/Code/RideBound-costfirst, read only):
        * initialized.manifestHash  = H("RideBound.ManifestHash.v1\\0", canonicalManifest)      ProtocolHash.cs:10-29;
          manifest written by RunManifestIdentityCodec.Write (InitializeRunMessages.cs:109-126,350-402): conversions
          ordered by quantity, capabilities distinct + ordinal (HelloMessages.cs:109-117,274-287,463-486)
        * initialStateIdentity     = epoch 0 / nextEventSeq 1 / simTime 0 (RunnerSession.cs:337-345) and stateHash =
          OnlineStateHash of OnlineState.Create(run, travelTimeSnapshotHash) (RunnerSession.cs:348-361;
          OnlineState.cs:35-58; OnlineStateCanonicalizer.cs:19-50,113-123,131-140,656-672)
        * decision 1 decisionHash  = H("RideBound.DecisionHash.v1\\0", previous, manifestHash, policyVersion,
          canonicalInputState, canonicalDecision)  ProtocolHash.cs:13-14,31-67; input RunnerSession.cs:532-540,768-800;
          hash projection without previousDecisionHash/decisionHash DecisionMessages.cs:457-527
        * every Runner checkpoint  : checkpointVersion 1.0.0, exact content fields, checkpointHash =
          H("RideBound.CheckpointHash.v1\\0", canonicalCheckpointContent) CheckpointMessages.cs:32-45,129-159, and
          content.stateHash = OnlineStateHash(content.onlineState) (RunnerSession.cs:651-662)
        * final checkpoint boundary: appliedEpoch = #decisions, nextEventSeq = last eventSeq + 1, simTimeMs = last batch
          time (as the adapter verifier, actual_fleetpy_medium_verify.py:1133-1146)
      All five reproduce every archived digest of the four Gate-0 references (self-test R4). NOT recomputable offline:
      decisionHash of decisions 2..N and the intermediate state hashes, because canonicalInputState contains the
      Runner's pre-batch online state (RunnerSession.cs:787-794), which a transcript carries only at the final
      checkpoint. Those keep the v1 per-run link checks (chain, ACK, continuity) and the content comparison; the gap is
      disclosed in HARNESS-FIX-REPORT-R24.md and logged as GAP by the self-test, never as a pass of digest validity.
  F3  A started run whose artifacts are malformed is a per-job FAILURE (MalformedRun / any evaluation error), never an
      escaping exception; the phase verdict is written exactly once, BEFORE the report; the report argument must be a
      bare file name inside this folder with the phase prefix (pair-*.txt, gate0-compare-*.txt, smoke-check-*.txt),
      not existing, so it can never collide with a verdict file. Any *-FAILED.txt refuses every phase verdict; a
      phase with its own verdict is closed. Not ready (no seal / no freeze / no runner manifest / a job not started)
      still writes nothing; input drift found after the jobs started is a FAILED verdict.
  F4  The ON smoke requires the execution evidence the ON configuration promises ("emitSolverExecutionEvidence": true,
      wp4-tier3-v2-costfirst.json:6) with explicit, valid status/path/omission/work accounting and the cost-first
      level order, accepts the legitimate fallback / unproven-budget shapes only with their supported reason, and
      prints objective bounds and work values. The configuration identity stays the verified ON binding (R22:242).

Usage (the report name is a bare file name; it is written into this folder):
  python -B compare_equivalence_stage1b-v2.py pair <ref-run> <new-run> <U|C-30> pair-<tag>.txt
  python -B compare_equivalence_stage1b-v2.py gate0 gate0-compare-<tag>.txt  -> GATE0-PASSED.txt / GATE0-FAILED.txt
  python -B compare_equivalence_stage1b-v2.py smoke smoke-check-<tag>.txt    -> SMOKE-PASSED.txt / SMOKE-FAILED.txt
`pair` is exploratory and never a Gate-0 verdict. Every output is created with mode "x" (never overwritten).
"""
from __future__ import annotations

import base64
import binascii
import collections
import hashlib
import importlib.util
import json
import pathlib
import re
import struct
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent.parent
for sub in ("pilot", "evidence", "sweep"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(TANG3))
sys.path.insert(0, str(HERE))
import audit_vep  # noqa: E402
import canonical_wp4 as cj  # noqa: E402
import runner_manifest  # noqa: E402
from pilot_analyze import fleet_decisions  # noqa: E402

DRIVER_FILE = "run_stage1b-v2.py"
DRIVER = None            # the loaded driver module (tests may replace it)

ZERO_HASH = "0" * 64
HEX64 = re.compile(r"^[0-9a-f]{64}$")
HASH_KEY = re.compile(r"(hash|Hash|sha256|Sha256|digest|Digest)$")
STABLE_HASH_KEYS = frozenset({"policyConfigurationHash", "scenarioContentHash", "graphSnapshotHash",
                              "travelTimeSnapshotHash", "snapshotHash", "expectedInitialTravelTimeSnapshotHash"})
DERIVED_HASH_KEYS = frozenset({"manifestHash", "stateHash", "stateBeforeHash", "stateAfterHash", "inputStateHash",
                               "proposedStateHash", "decisionHash", "previousDecisionHash", "checkpointHash"})
RUN_SEMANTIC_DERIVED = frozenset({"manifestHash", "checkpointBindingHash", "publicationDigest"})
RUN_SEMANTIC_STABLE = frozenset({"nodeMappingHash", "sourceScenarioContentSha256"})
DLL_TOKEN = "<verified-runner-dll>"
RUN_TOKEN = "<run>"
RECORD_KEYS = ("direction", "frameBase64", "frameLengthBytes", "frameSha256", "ordinal", "schemaVersion")
DIRECTIONS = ("adapterToRunner", "runnerToAdapter")
BUNDLE_FILES = ("run-00.json", "summary.json", "transcript-00.ndjson")
FORBIDDEN_CF_LEVEL = "worst-hard-utilization-ppm"
VERDICT_FILES = ("GATE0-PASSED.txt", "GATE0-FAILED.txt", "SMOKE-PASSED.txt", "SMOKE-FAILED.txt")
FAILED_FILES = ("GATE0-FAILED.txt", "SMOKE-FAILED.txt")
REPORT_NAME = {"pair": re.compile(r"^pair-[A-Za-z0-9._-]+\.txt$"),
               "gate0": re.compile(r"^gate0-compare-[A-Za-z0-9._-]+\.txt$"),
               "smoke": re.compile(r"^smoke-check-[A-Za-z0-9._-]+\.txt$")}

# Frozen identities (Codex R22 §4-§6 and §5.1; archived runs under C:/RideBoundData/research/tier3-t38-v1).
HARNESS_COMMIT = "1ce15e92d2c1459d4254bbbcd581529197257fb2"                         # R22:177
HARNESS_INVENTORY = "0da63eaf2b30ab332fba265a6b2b6e5c36b3ee78950cbac6d6267e60f81e4cbe"  # R22:177; run_w07.py:46
OLD_RUNNER_DLL = "a631038b6ea4850ffa29be2d5e4058fba2e57b725ee568075686d7a43c6f5fac"    # R22:196
OLD_RUNNER_TREE = "ac251ef32a4865ea3bd3d8d87b4b1bcaccc4462a9b80160b4d205b8d474cff3f"   # run_w07.py:41
BINDINGS = {  # (WP4 mode, arm) -> initializeRun policyConfigurationHash
    ("off", "U"): "251b331eb56fe870e734d651bf6937e95304919ed92967b2f3b0d0b9da1ac346",     # R22:129 (archived 34/34)
    ("off", "C-30"): "a2f11a3ada0773f9b30b85c56c935058e2e931f13c907f46b2ddfff8c1285f1c",  # R22:128 (archived 51/51)
    ("on", "U"): "90b290d7e63f6885ac8e93e18017d55dddc54c27a05d8476a65dcf7d802e2df5",      # R22:237 (derived)
    ("on", "C-30"): "e6b33be5649dc2b1208b42b90cef227d00721cba59e4340fdbc4e96f21796f76",   # R22:238 (derived)
}
REFERENCE_TRANSCRIPTS = {  # R22:162-167, verified transcript SHA-256 and frame count of the four Gate-0 references
    "U-d20181116-s10-r1-w08": ("c82d27969f6e8b8be34606a4496f2599772a0016e9eddf0f1f0bc6a261921740", 2020),
    "U-d20181116-s10-r1-w17": ("6ec5242cb60356759d1da6c38d295cebc138397fe3c254e6126a56f56780f2e1", 2284),
    "C-30-d20181116-s10-r1-w08": ("42ba5f1aab73d363d1c8a355d1646e8bb71bae7a6b5a0cda50be8448fc45272b", 2035),
    "C-30-d20181116-s10-r1-w17": ("371565dd40299e7755aaafefeebb4481bfdcdcb379b7d8d892099bbb4607ebbc", 2146),
}

# ---- protocol digest formulas (F1); C# sources cited in the module docstring
MANIFEST_DOMAIN = b"RideBound.ManifestHash.v1\0"           # ProtocolHash.cs:10-11
DECISION_DOMAIN = b"RideBound.DecisionHash.v1\0"           # ProtocolHash.cs:13-14
ONLINE_STATE_DOMAIN = b"RideBound.OnlineStateHash.v1\0"    # OnlineStateCanonicalizer.cs:19-20
CHECKPOINT_DOMAIN = b"RideBound.CheckpointHash.v1\0"       # CheckpointMessages.cs:32-33
MANIFEST_FIELDS = frozenset({"protocolVersion", "masterSeed", "policyId", "policyVersion", "policyConfigurationHash",
                             "scenarioContentHash", "graphSnapshotHash", "travelTimeSnapshotHash", "costUnitId",
                             "sourceUnitConversions", "capabilitySelection", "adapter", "simulator", "coreCommitSha",
                             "binarySha256"})                                            # InitializeRunMessages.cs:109-126
CONVERSION_FIELDS = frozenset({"quantity", "sourceUnit", "canonicalUnit", "roundingRule"})  # :129-136
CHECKPOINT_FIELDS = frozenset({"checkpointVersion", "checkpointHash", "content"})          # CheckpointMessages.cs:35-36
CHECKPOINT_CONTENT_FIELDS = frozenset({"manifestHash", "stateHash", "previousDecisionHash", "appliedEpoch",
                                       "nextEventSeq", "simTimeMs", "onlineState"})        # CheckpointMessages.cs:38-45

# ---- execution evidence (F4)
EXPECTED_EVIDENCE_VERSION = "1.1.0"   # all 111,766 archived T3.8 decisions (HARNESS-FIX-REPORT-R24.md §F4); R22:242
EVIDENCE_FIELDS = frozenset({"evidenceVersion", "generation", "prunedCandidates", "selection"})   # verifier :187-199
GENERATION_FIELDS = frozenset({"totalPendingRequestCount", "consideredRequestCount", "omittedRequestCount",
                               "vehicleLosses", "omissions", "exogenousServiceQualityBreaches"})   # verifier :202-215
SELECTION_REQUIRED = frozenset({"consumedGenerationWorkUnits", "consumedValidationWorkUnits", "omittedCandidateCount",
                                "omissionCountWasSaturated", "primarySolveStatus", "primarySolverDiagnostics",
                                "finalSolveStatus", "finalSolverDiagnostics", "executionPath",
                                "fallbackValidationAttempts", "primaryIncumbentRejected", "validationWitnesses"})
SELECTION_OPTIONAL = frozenset({"omissionDigest"})          # SolverExecutionEvidenceMapper.cs:519-568; verifier :404-422
DIAGNOSTICS_REQUIRED = frozenset({"consumedWorkUnits", "consumedDeterministicTimeMicros", "objectiveBounds"})
DIAGNOSTICS_OPTIONAL = frozenset({"detailCode"})            # SolverExecutionEvidenceMapper.cs:571-601
BOUND_FIELDS = frozenset({"levelIndex", "objectiveName", "incumbentValue", "bestBound", "gapNumerator",
                          "gapDenominator", "isProvenOptimal"})                       # :585-595
SOLVE_STATUSES = frozenset({"optimal", "feasible", "infeasible", "unknown", "modelInvalid", "safeFallback"})  # :620-630
PUBLISHING_PATHS = frozenset({"validatedIncumbent", "safeNoOp", "greedySingleRequest"})  # :632-641 minus "none"
FALLBACK_DETAIL = {"safeNoOp": "SAFE_FALLBACK_NO_OP",                                   # CandidateSelectionExecution
                   "greedySingleRequest": "SAFE_FALLBACK_GREEDY_SINGLE_REQUEST"}       # .cs:287-293,309-315,366-386
CF_NAMED_LEVELS = ("forced-reference-count", "accepted-request-count", "operational-cost")  # SolverBackedFleetSelection
CANDIDATE_RANK = "candidate-id-rank:"                                                   # .cs:310-416 (cost-first)


class MalformedRun(Exception):
    """A run folder that exists but cannot be read as a bundle (F3): judged as a failure, never an escape."""


class DigestError(ValueError):
    pass


def canonical(value) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load_module(name: str, filename: str):
    if name in sys.modules:
        return sys.modules[name]
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def driver():
    global DRIVER
    if DRIVER is None:
        DRIVER = load_module("run_stage1b_v2", DRIVER_FILE)
    return DRIVER


class MemText:
    """Duck-typed path for the archived readers (audit_vep.read_frames and pilot_analyze.fleet_decisions only call
    .read_text(encoding="utf-8")), so they run unchanged on in-memory transcripts."""

    def __init__(self, text: str, label: str):
        self._text, self._label = text, label

    def read_text(self, encoding: str = "utf-8") -> str:
        return self._text

    def __str__(self) -> str:
        return self._label


# ---------------------------------------------------------------- protocol digests (F1)

_SURROGATE = re.compile("[\ud800-\udfff]")
_ABOVE_E000 = re.compile("[\ue000-\U0010ffff]")


def canonical_bytes(value) -> bytes:
    """Exactly cj.canonical_bytes_of_value (the C# CanonicalJson mirror), computed fast: a validating walk applies
    the same rejections (null, non-integer, out-of-range integer, unpaired surrogate) and then the C-level json.dumps
    writes the bytes. The two writers agree on this domain: same escapes (\\" \\\\ \\b \\f \\n \\r \\t, other
    code points <= 0x1F as lowercase \\u00xx, everything else raw UTF-8) and the same key order as long as no key holds
    a code point >= U+E000, where code-point order and UTF-16 ordinal order can differ; such a document falls back to
    the slow mirror. Equality with cj on real states is a self-test check (F1-R5)."""
    stack = [value]
    while stack:
        item = stack.pop()
        if item is None:
            raise cj.CanonicalJsonError("null is not permitted")
        if isinstance(item, bool):
            continue
        if isinstance(item, int):
            if not -cj.MAX_CANONICAL_INTEGER <= item <= cj.MAX_CANONICAL_INTEGER:
                raise cj.CanonicalJsonError("integer outside the canonical safe range")
        elif isinstance(item, str):
            if _SURROGATE.search(item):
                raise cj.CanonicalJsonError("unpaired UTF-16 surrogate")
        elif isinstance(item, list):
            stack.extend(item)
        elif isinstance(item, dict):
            for key in item:
                if not isinstance(key, str) or _SURROGATE.search(key):
                    raise cj.CanonicalJsonError("invalid property name")
                if _ABOVE_E000.search(key):
                    return cj.canonical_bytes_of_value(value)
            stack.extend(item.values())
        else:
            raise cj.CanonicalJsonError(f"unsupported value {type(item).__name__}")
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"),
                      allow_nan=False).encode("utf-8")


def _frame(tag: str, value: bytes) -> bytes:
    tag_bytes = tag.encode("utf-8")
    return struct.pack(">H", len(tag_bytes)) + tag_bytes + struct.pack(">Q", len(value)) + value


def framed_hash(domain: bytes, frames) -> str:
    """ProtocolHash.cs:92-130 / CheckpointMessages.cs:145-156 / OnlineStateCanonicalizer.cs:656-672: domain, then per
    frame u16-BE tag length, UTF-8 tag, u64-BE value length, value."""
    return sha256(domain + b"".join(_frame(tag, value) for tag, value in frames))


def _ordinal(text: str) -> bytes:
    return text.encode("utf-16-be")          # StringComparer.Ordinal compares UTF-16 code units


def manifest_hash(manifest) -> str:
    if not isinstance(manifest, dict) or set(manifest) != MANIFEST_FIELDS:
        raise DigestError("initializeRun manifest fields differ from RunManifestIdentityCodec")
    conversions, selection = manifest["sourceUnitConversions"], manifest["capabilitySelection"]
    if not isinstance(conversions, list) or any(not isinstance(item, dict) or set(item) != CONVERSION_FIELDS
                                                or not isinstance(item["quantity"], str) for item in conversions):
        raise DigestError("sourceUnitConversions malformed")
    if not isinstance(selection, dict) or not isinstance(selection.get("capabilities"), list) or any(
            not isinstance(item, str) for item in selection["capabilities"]):
        raise DigestError("capabilitySelection malformed")
    normalized = dict(manifest)
    normalized["sourceUnitConversions"] = sorted(conversions, key=lambda item: _ordinal(item["quantity"]))
    normalized["capabilitySelection"] = dict(selection, capabilities=sorted(set(selection["capabilities"]),
                                                                            key=_ordinal))
    return framed_hash(MANIFEST_DOMAIN, [("canonicalManifest", canonical_bytes(normalized))])


def online_state_hash(state) -> str:
    if not isinstance(state, dict):
        raise DigestError("onlineState is not an object")
    return framed_hash(ONLINE_STATE_DOMAIN, [("canonicalOnlineState", canonical_bytes(state))])


def initial_online_state(run_id: str, scenario_id: str, travel_snapshot_hash: str) -> dict:
    """OnlineState.Create (OnlineState.cs:35-58) as written by OnlineStateCanonicalizer.Canonicalize: no travelTimes
    (null), no planPool (version 0), empty requests/vehicles/ledgers."""
    return {"runId": run_id, "scenarioId": scenario_id, "appliedEpoch": 0, "simulationTimeMs": 0, "nextEventSeq": 1,
            "expectedInitialTravelTimeSnapshotHash": travel_snapshot_hash, "requests": [], "vehicles": [],
            "commitmentLedger": [], "incidentLedger": {"incidents": [], "breaches": []}}


def decision_hash(previous: str, manifest_hash_value: str, policy_version: str, canonical_input: bytes,
                  canonical_decision: bytes) -> str:
    if not (isinstance(previous, str) and HEX64.match(previous) and isinstance(manifest_hash_value, str)
            and HEX64.match(manifest_hash_value) and isinstance(policy_version, str)):
        raise DigestError("decision hash inputs malformed")
    return framed_hash(DECISION_DOMAIN, [("previousDecisionHash", bytes.fromhex(previous)),
                                         ("manifestHash", bytes.fromhex(manifest_hash_value)),
                                         ("policyVersion", policy_version.encode("utf-8")),
                                         ("canonicalInputState", canonical_input),
                                         ("canonicalDecision", canonical_decision)])


def decision_input_bytes(batch_envelope: dict, epoch: int, next_seq: int, sim_time: int, state_hash: str,
                         online_state: dict) -> bytes:
    return canonical_bytes({
        "eventBatch": batch_envelope,
        "stateIdentityBefore": {"epochId": epoch, "nextEventSeq": next_seq, "simTimeMs": sim_time,
                                "stateHash": state_hash},
        "onlineStateBefore": online_state})


def decision_projection_bytes(payload: dict) -> bytes:
    if not isinstance(payload, dict):
        raise DigestError("decision payload is not an object")
    return canonical_bytes({key: value for key, value in payload.items()
                                        if key not in ("previousDecisionHash", "decisionHash")})


def checkpoint_hash(content) -> str:
    if not isinstance(content, dict) or set(content) != CHECKPOINT_CONTENT_FIELDS:
        raise DigestError("checkpoint content fields differ from CheckpointPayloadCodec")
    return framed_hash(CHECKPOINT_DOMAIN, [("canonicalCheckpointContent", canonical_bytes(content))])


def digest_problems(init_envelope, initialized, first_batch, first_decision, checkpoints, closure) -> tuple:
    """F1: per-run digest validity. Returns (problems, coverage). Every argument may be malformed; any failure to
    evaluate is itself a problem."""
    problems, coverage = [], collections.Counter()

    def attempt(label, check):
        try:
            check()
        except (DigestError, cj.CanonicalJsonError, KeyError, TypeError, AttributeError, ValueError,
                IndexError) as error:
            problems.append(f"digest {label}: cannot be validated ({type(error).__name__}: {error})")

    state = {}

    def manifest_check():
        want = manifest_hash(init_envelope["payload"]["manifest"])
        state["manifestHash"] = want
        if initialized["payload"]["manifestHash"] != want:
            problems.append("digest: initialized.manifestHash is not the hash of the initializeRun manifest")
        coverage["manifestHash"] += 1

    def initial_check():
        identity = initialized["payload"]["initialStateIdentity"]
        if (identity.get("epochId"), identity.get("nextEventSeq"), identity.get("simTimeMs")) != (0, 1, 0):
            problems.append("digest: initialStateIdentity is not epoch 0 / nextEventSeq 1 / simTime 0")
        manifest = init_envelope["payload"]["manifest"]
        initial = initial_online_state(init_envelope["runId"], init_envelope["scenarioId"],
                                       manifest["travelTimeSnapshotHash"])
        want = online_state_hash(initial)
        state["initialState"], state["initialHash"] = initial, want
        if identity.get("stateHash") != want:
            problems.append("digest: initial stateHash is not the hash of the initial online state")
        coverage["initialStateHash"] += 1

    def first_decision_check():
        if first_batch is None or first_decision is None:
            raise DigestError("no first eventBatch/decision pair")
        payload = first_decision["payload"]
        if payload.get("previousDecisionHash") != ZERO_HASH:
            problems.append("digest: decision 1 previousDecisionHash is not the zero hash")
        if "manifestHash" not in state or "initialHash" not in state:
            raise DigestError("manifest or initial state digest unavailable")
        canonical_input = decision_input_bytes(first_batch, 0, 1, 0, state["initialHash"], state["initialState"])
        want = decision_hash(ZERO_HASH, state["manifestHash"], init_envelope["payload"]["manifest"]["policyVersion"],
                             canonical_input, decision_projection_bytes(payload))
        if payload.get("decisionHash") != want:
            problems.append("digest: decision 1 decisionHash is not the hash of its content")
        coverage["decision1Hash"] += 1

    def checkpoint_check(index, envelope):
        payload = envelope["payload"]
        if not isinstance(payload, dict) or set(payload) != CHECKPOINT_FIELDS:
            raise DigestError("checkpoint payload fields differ")
        if payload["checkpointVersion"] != "1.0.0":
            problems.append(f"digest: frame {index + 1} checkpointVersion {payload['checkpointVersion']!r}")
        if payload["checkpointHash"] != checkpoint_hash(payload["content"]):
            problems.append(f"digest: frame {index + 1} checkpointHash is not the hash of its content")
        if payload["content"]["stateHash"] != online_state_hash(payload["content"]["onlineState"]):
            problems.append(f"digest: frame {index + 1} checkpoint stateHash is not the hash of its onlineState")
        if "manifestHash" in state and payload["content"]["manifestHash"] != state["manifestHash"]:
            problems.append(f"digest: frame {index + 1} checkpoint manifestHash != recomputed manifest hash")
        coverage["checkpointHash"] += 1
        coverage["checkpointStateHash"] += 1

    def closure_check():
        content = checkpoints[-1][1]["payload"]["content"]
        decisions, last_seq, last_time = closure
        if (content["appliedEpoch"], content["nextEventSeq"], content["simTimeMs"]) != (
                decisions, None if last_seq is None else last_seq + 1, last_time):
            problems.append("digest: final checkpoint epoch/eventSeq/simTime do not close the transcript")
        coverage["checkpointClosure"] += 1

    attempt("manifestHash", manifest_check)
    attempt("initial stateHash", initial_check)
    attempt("decision 1 decisionHash", first_decision_check)
    if not checkpoints:
        problems.append("digest: no Runner checkpoint to validate")
    for index, envelope in checkpoints:
        attempt(f"checkpoint frame {index + 1}", lambda i=index, e=envelope: checkpoint_check(i, e))
    if checkpoints:
        attempt("final checkpoint closure", closure_check)
    return problems, dict(coverage)


# ---------------------------------------------------------------- runner identity (F1/F3 of R22)

def tree_text(lines: list[str]) -> str:
    """runner_manifest.py:31-33: header + body; tree = SHA-256 of the body lines joined by LF."""
    tree = sha256("\n".join(lines).encode("utf-8"))
    return f"# tree-sha256 {tree} files {len(lines)}\n" + "\n".join(lines) + "\n"


def runner_identity(manifest_text: str | None, actual_lines: list[str] | None, label: str) -> dict:
    """Verify a runner tree against its frozen manifest; return its tree hash and Runner DLL SHA-256."""
    out = {"label": label, "ok": False, "tree": None, "files": None, "dll": None, "problems": []}
    if manifest_text is None:
        out["problems"].append(f"{label}: runner manifest missing")
        return out
    if actual_lines is None:
        out["problems"].append(f"{label}: runner directory missing")
        return out
    header = manifest_text.split("\n", 1)[0]
    match = re.fullmatch(r"# tree-sha256 ([0-9a-f]{64}) files (\d+)", header)
    if not match:
        out["problems"].append(f"{label}: manifest header malformed")
        return out
    if manifest_text != tree_text(actual_lines):
        out["problems"].append(f"{label}: runner directory differs from its manifest (tree or file list)")
    dll = [line.split("\t") for line in actual_lines if line.split("\t")[0] == "RideBound.Runner.dll"]
    if len(dll) != 1 or not HEX64.match(dll[0][1]):
        out["problems"].append(f"{label}: no unique RideBound.Runner.dll entry")
    else:
        out["dll"] = dll[0][1]
    out.update(tree=match.group(1), files=int(match.group(2)))
    out["ok"] = not out["problems"]
    return out


def runner_identity_from_disk(root: pathlib.Path, manifest: pathlib.Path, label: str) -> dict:
    text = manifest.read_text(encoding="utf-8") if manifest.exists() else None
    lines = runner_manifest.body(root) if root.is_dir() else None
    return runner_identity(text, lines, label)


# ---------------------------------------------------------------- one run, in memory

class Run:
    """A run bundle in memory: the bytes of bundle-manifest.json, run-00.json, summary.json, transcript-00.ndjson.
    Unreadable bundles raise MalformedRun (an Exception, so a phase loop records it as a failed job; F3)."""

    def __init__(self, label: str, files: dict[str, bytes], path: pathlib.Path | None = None,
                 extra_files: tuple[str, ...] = ()):
        self.label, self.path, self.problems = label, path, []
        for name in extra_files:
            self.problems.append(f"unexpected file in run folder: {name}")
        try:
            self.bundle = json.loads(files["bundle-manifest.json"])
            self.summary = json.loads(files["summary.json"])
            self.run0 = json.loads(files["run-00.json"])
            raw = files["transcript-00.ndjson"]
            if not all(isinstance(x, dict) for x in (self.bundle, self.summary, self.run0)):
                raise ValueError("bundle-manifest/summary/run-00 root is not an object")
            if not isinstance(raw, bytes):
                raise ValueError("transcript is not bytes")
            self.text = raw.decode("utf-8")
        except (KeyError, ValueError, TypeError) as error:
            raise MalformedRun(f"{label}: unreadable run bundle: {type(error).__name__}: {error}") from error
        files_list = self.bundle.get("files", [])
        if not isinstance(files_list, list) or any(not isinstance(item, dict) for item in files_list):
            self.problems.append("bundle-manifest files list malformed")
            files_list = []
        listed = {item.get("path"): item for item in files_list}
        if sorted(map(str, listed)) != sorted(BUNDLE_FILES):
            self.problems.append(f"bundle lists {sorted(map(str, listed))}, expected {list(BUNDLE_FILES)}")
        for name, item in listed.items():
            data = files.get(name)
            if data is None or len(data) != item.get("lengthBytes") or sha256(data) != item.get("sha256"):
                self.problems.append(f"bundle file {name} does not match bundle-manifest.json")
        if self.summary.get("status") != "pass":
            self.problems.append(f"summary status {self.summary.get('status')!r} (pass required)")
        if self.run0.get("status") != "succeeded":
            self.problems.append(f"run-00 status {self.run0.get('status')!r} (succeeded required)")
        self.transcript_sha256 = sha256(raw)
        self.lines = self.text.split("\n")
        if self.lines and self.lines[-1] == "":
            self.lines.pop()
        else:
            self.problems.append("transcript does not end with a newline")

    @classmethod
    def load(cls, directory: pathlib.Path) -> "Run":
        if not directory.is_dir():
            raise MalformedRun(f"run folder missing: {directory}")
        names = sorted(p.name for p in directory.iterdir())
        files = {name: (directory / name).read_bytes() for name in names
                 if name in BUNDLE_FILES + ("bundle-manifest.json",)}
        extra = tuple(n for n in names if n not in BUNDLE_FILES + ("bundle-manifest.json",))
        return cls(directory.name, files, directory, extra)

    def frame(self, index: int):
        record = json.loads(self.lines[index])
        return record["direction"], json.loads(base64.b64decode(record["frameBase64"]))


def load_run(directory) -> Run:
    return Run.load(directory)


class Normalizer:
    def __init__(self, run_id: str, publications: dict, breaches: dict, verified_dll: str | None, problems: list):
        self.run_id, self.publications, self.breaches = run_id, publications, breaches
        self.verified_dll, self.problems = verified_dll, problems
        self.tokens: dict[str, str] = {}

    def derived(self, value, where):
        if not isinstance(value, str) or not HEX64.match(value):
            self.problems.append(f"derived hash at {where} is not 64 lowercase hex")
            return value
        if value == ZERO_HASH:
            return value
        if value not in self.tokens:
            self.tokens[value] = f"H{len(self.tokens)}"
        return self.tokens[value]

    def publication(self, value, where):
        if isinstance(value, str) and value in self.publications:
            return self.publications[value]
        self.problems.append(f"publication reference at {where} has no ledger record")
        return f"UNMAPPED:{value}"

    def breach(self, value, where):
        if isinstance(value, str) and value in self.breaches:
            return self.breaches[value]
        self.problems.append(f"breach reference at {where} has no incident-ledger record")
        return f"UNMAPPED:{value}"

    def walk(self, value, path: tuple, frame_kind: tuple, inside_actions: bool = False):
        where = f"{frame_kind[0]}:{frame_kind[1]}:$." + ".".join(path)
        if isinstance(value, dict):
            out = {}
            for key in sorted(value):
                item, child = value[key], path + (key,)
                if key == "runId":
                    if item != self.run_id:
                        self.problems.append(f"second runId value at {where}.{key}")
                    out[key] = RUN_TOKEN
                elif key == "publicationId":
                    out[key] = self.publication(item, where + "." + key)
                elif key == "publicationIds":
                    if not isinstance(item, list):
                        self.problems.append(f"publicationIds at {where} is not a list")
                        out[key] = item
                    else:
                        out[key] = sorted(self.publication(x, where + "." + key) for x in item)
                elif key == "breachId":
                    out[key] = self.breach(item, where + "." + key)
                elif HASH_KEY.search(key):
                    if key in STABLE_HASH_KEYS:
                        out[key] = item
                    elif key in DERIVED_HASH_KEYS:
                        if inside_actions:
                            self.problems.append(f"derived hash inside an order-insensitive actions list at {where}")
                        out[key] = self.derived(item, where + "." + key)
                    elif (key == "binarySha256" and frame_kind == ("adapterToRunner", "initializeRun")
                          and path == ("payload", "manifest")):
                        if self.verified_dll is not None and item == self.verified_dll:
                            out[key] = DLL_TOKEN
                        else:
                            self.problems.append("binarySha256 is not the verified DLL SHA-256 of this run's runner")
                            out[key] = item
                    else:
                        self.problems.append(f"UNCLASSIFIED hash field {where}.{key} (review before adding a rule)")
                        out[key] = item
                else:
                    out[key] = self.walk(item, child, frame_kind, inside_actions or key == "actions")
            if isinstance(out.get("actions"), list):
                out["actions"] = sorted(out["actions"], key=canonical)
            return out
        if isinstance(value, list):
            return [self.walk(item, path + ("[]",), frame_kind, inside_actions) for item in value]
        if isinstance(value, str):
            if value == self.run_id:
                return RUN_TOKEN
            if value in self.publications:
                return self.publications[value]
            if value in self.breaches:
                return self.breaches[value]
        return value


def ledger_maps(state: dict, problems: list):
    publications, breaches, seen = {}, {}, collections.Counter()
    try:
        for history in state.get("commitmentLedger", []):
            for index, entry in enumerate(history.get("entries", [])):
                raw = entry.get("publicationId")
                if raw in publications:
                    problems.append("a publicationId appears twice in the final commitment ledger")
                publications[raw] = f"pub:{history.get('requestId')}#{index}"
        for record in state.get("incidentLedger", {}).get("breaches", []):
            key = (f"breach:{record.get('kind')}:{record.get('requestId')}:{record.get('recordedEpoch')}:"
                   f"{record.get('sourceEventSeq')}")
            raw = record.get("breachId")
            if raw in breaches:
                problems.append("a breachId appears twice in the final incident ledger")
            breaches[raw] = f"{key}#{seen[key]}"
            seen[key] += 1
    except (AttributeError, TypeError) as error:
        problems.append(f"final ledgers malformed ({type(error).__name__}: {error})")
    return publications, breaches


def update_event_times(boarded: dict, alighted: dict, direction: str, envelope: dict) -> None:
    """sweep_analyze.py:40-57 in memory, one frame at a time: requestId -> simTimeMs of the first adapter batch
    that carries the rider's passengerBoarded / passengerAlighted event."""
    if direction != "adapterToRunner" or envelope.get("messageType") != "eventBatch":
        return
    for event in envelope["payload"].get("events", []):
        kind = event.get("eventType")
        if kind == "passengerBoarded":
            boarded.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
        elif kind == "passengerAlighted":
            alighted.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])


def facts(run: Run, verified_dll: str | None) -> dict:
    """Integrity, digest validity (F1), link checks and the compared components of one run. Problems go to
    out['problems']; malformed content becomes a problem, never an exception (F3)."""
    problems = list(run.problems)
    out = {"label": run.label, "problems": problems, "frameCount": len(run.lines),
           "transcriptSha256": run.transcript_sha256}
    decoded_tail, final_index = None, None
    for index in range(len(run.lines) - 1, max(-1, len(run.lines) - 8), -1):
        try:
            direction, envelope = run.frame(index)
        except (ValueError, KeyError, TypeError, binascii.Error):
            continue
        if direction == "runnerToAdapter" and isinstance(envelope, dict) and \
                envelope.get("messageType") == "checkpoint":
            decoded_tail, final_index = envelope, index
            break
    state = {}
    if decoded_tail is None:
        problems.append("no final Runner checkpoint")
    else:
        try:
            state = decoded_tail["payload"]["content"]["onlineState"]
            if not isinstance(state, dict):
                raise TypeError("onlineState is not an object")
        except (KeyError, TypeError) as error:
            problems.append(f"final checkpoint content malformed ({type(error).__name__}: {error})")
            state, final_index = {}, None
    init_envelope = None
    try:
        _, init_envelope = run.frame(2)
        run_id = init_envelope["runId"]
        init_manifest = init_envelope["payload"]["manifest"]
        if init_envelope.get("messageType") != "initializeRun" or not isinstance(init_manifest, dict):
            raise KeyError("frame 3 is not initializeRun")
    except (ValueError, KeyError, IndexError, TypeError, AttributeError, binascii.Error) as error:
        problems.append(f"no initializeRun at frame 3: {error}")
        run_id, init_manifest = None, {}
    out["initManifest"] = init_manifest
    out["runId"] = run_id
    publications, breaches = ledger_maps(state, problems)
    normalizer = Normalizer(run_id, publications, breaches, verified_dll, problems)

    frames, boarded, alighted = [], {}, {}
    initialized = initialized_envelope = last_decision = first_decision = pending = None
    first_batch = first_decision_envelope = None
    last_batch = None
    last_event_seq = None
    decision_count = 0
    checkpoints = []
    action_publications = set()
    kinds = collections.Counter()
    for index, line in enumerate(run.lines):
        try:
            record = json.loads(line)
            if not isinstance(record, dict):
                raise ValueError("record is not an object")
            if tuple(sorted(record)) != RECORD_KEYS:
                problems.append(f"frame {index + 1}: record keys {sorted(record)}")
            raw = base64.b64decode(record["frameBase64"], validate=True)
            if record["ordinal"] != index + 1:
                problems.append(f"frame {index + 1}: ordinal {record['ordinal']} (contiguous from 1 required)")
            if sha256(raw) != record["frameSha256"] or len(raw) != record["frameLengthBytes"]:
                problems.append(f"frame {index + 1}: frame SHA-256/length mismatch")
            if record["direction"] not in DIRECTIONS or record["schemaVersion"] != "1.0.0":
                problems.append(f"frame {index + 1}: direction/schemaVersion invalid")
            envelope = json.loads(raw)
            if not isinstance(envelope, dict):
                raise ValueError("envelope is not an object")
        except (ValueError, KeyError, TypeError, binascii.Error) as error:
            problems.append(f"frame {index + 1}: undecodable ({error})")
            frames.append(("?", "UNDECODABLE"))
            continue
        direction, kind = record["direction"], envelope.get("messageType")
        kinds[(str(direction), str(kind))] += 1
        try:
            # ---- per-run link checks (verified on the four archived references: 0 violations)
            if "runId" in envelope and envelope["runId"] != run_id:
                problems.append(f"frame {index + 1}: runId differs from initializeRun")
            if direction == "runnerToAdapter" and kind == "initialized":
                initialized, initialized_envelope = envelope["payload"], envelope
            elif direction == "adapterToRunner" and kind == "eventBatch":
                last_batch = envelope
                if first_batch is None:
                    first_batch = envelope
                events = envelope["payload"].get("events") or []
                if events:
                    last_event_seq = events[-1].get("eventSeq")
            elif direction == "runnerToAdapter" and kind == "decision":
                payload = envelope["payload"]
                decision_count += 1
                if pending is not None:
                    problems.append(f"frame {index + 1}: previous decision was not acknowledged")
                expected_previous = last_decision["decisionHash"] if last_decision else ZERO_HASH
                if payload.get("previousDecisionHash") != expected_previous:
                    problems.append(f"frame {index + 1}: previousDecisionHash breaks the decision chain")
                body = payload.get("certificate", {}).get("body") or {}
                if body.get("inputStateHash") != payload.get("stateBeforeHash"):
                    problems.append(f"frame {index + 1}: certificate inputStateHash != stateBeforeHash")
                if body.get("proposedStateHash") != payload.get("stateAfterHash"):
                    problems.append(f"frame {index + 1}: certificate proposedStateHash != stateAfterHash")
                published = [a["payload"].get("publicationId") for a in payload.get("actions", [])
                             if a.get("decisionType") == "promisePublished"]
                certified = body.get("publicationIds", [])
                if sorted(map(str, published)) != sorted(map(str, certified)) or \
                        len(set(published)) != len(published):
                    problems.append(f"frame {index + 1}: certificate publicationIds != published actions")
                if action_publications & set(published):
                    problems.append(f"frame {index + 1}: publication published twice")
                action_publications |= set(published)
                if first_decision is None:
                    first_decision, first_decision_envelope = payload, envelope
                    if last_batch is not first_batch:
                        problems.append(f"frame {index + 1}: first decision does not answer the first eventBatch")
                last_decision, pending = payload, (payload.get("decisionHash"), envelope.get("epochId"))
            elif direction == "adapterToRunner" and kind == "decisionApplied":
                if pending != (envelope["payload"].get("decisionHash"), envelope.get("epochId")):
                    problems.append(f"frame {index + 1}: decisionApplied does not acknowledge the last decision")
                pending = None
            elif direction == "runnerToAdapter" and kind == "checkpoint":
                content = envelope["payload"]["content"]
                checkpoints.append((index, envelope))
                if initialized is None or content.get("manifestHash") != initialized.get("manifestHash"):
                    problems.append(f"frame {index + 1}: checkpoint manifestHash != initialized manifestHash")
                want_previous = last_decision["decisionHash"] if last_decision else ZERO_HASH
                want_state = last_decision["stateAfterHash"] if last_decision else (
                    (initialized or {}).get("initialStateIdentity", {}).get("stateHash"))
                if content.get("previousDecisionHash") != want_previous or content.get("stateHash") != want_state:
                    problems.append(f"frame {index + 1}: checkpoint does not continue the decision chain")
                if content.get("onlineState", {}).get("runId") != run_id:
                    problems.append(f"frame {index + 1}: checkpoint onlineState.runId differs")
            update_event_times(boarded, alighted, direction, envelope)
        except (KeyError, TypeError, AttributeError, IndexError, ValueError) as error:
            problems.append(f"frame {index + 1}: malformed protocol content ({type(error).__name__}: {error})")
        try:
            normalized = normalizer.walk(envelope, (), (str(direction), str(kind)))
            frames.append((direction, canonical(normalized)))
            if index == final_index:
                content = normalized["payload"]["content"]["onlineState"]
                out["finalOnlineState"] = canonical(content)
                out["commitmentLedger"] = [canonical(h) for h in content.get("commitmentLedger", [])]
                out["incidentLedger"] = canonical(content.get("incidentLedger"))
        except (KeyError, TypeError, AttributeError, IndexError, ValueError) as error:
            problems.append(f"frame {index + 1}: cannot be normalized ({type(error).__name__}: {error})")
            frames.append((direction, "UNNORMALIZABLE"))
    if pending is not None:
        problems.append("the last decision was not acknowledged")
    if initialized is None or first_decision is None:
        problems.append("missing initialized frame or decisions")
    elif (initialized.get("initialStateIdentity") or {}).get("stateHash") != first_decision.get("stateBeforeHash"):
        problems.append("initialized stateHash != first decision stateBeforeHash")
    # ---- F1: digest validity, before any cross-run comparison
    digest, coverage = digest_problems(init_envelope or {}, initialized_envelope or {}, first_batch,
                                       first_decision_envelope, checkpoints,
                                       (decision_count, last_event_seq,
                                        (last_batch or {}).get("simTimeMs")))
    problems += digest
    out["digestCoverage"] = coverage
    if run.lines:
        try:
            last_direction, last_envelope = run.frame(len(run.lines) - 1)
            if (last_direction, last_envelope.get("messageType")) != ("adapterToRunner", "shutdown"):
                problems.append("last frame is not the adapter shutdown")
        except (ValueError, KeyError, TypeError, AttributeError, binascii.Error):
            problems.append("last frame undecodable")
    if final_index is not None and final_index < len(run.lines) - 3:
        problems.append("final Runner checkpoint is not at the end of the transcript")
    if set(publications) - action_publications:
        problems.append("a ledger publication has no published action")
    out["frames"] = frames
    out["messageCounts"] = {f"{d}:{k}": n for (d, k), n in sorted(kinds.items())}
    # ---- components 1-3 with the archived readers
    memory = MemText(run.text, run.label)
    try:
        out["decisions"] = fleet_decisions(memory)
    except Exception as error:  # an archived reader failing on a malformed transcript is a failure (F3)
        problems.append(f"fleet_decisions failed ({type(error).__name__}: {error})")
        out["decisions"] = None
    try:
        table, summary = audit_vep.audit(memory)
        if table is None:
            problems.append("audit_vep: no final checkpoint")
        out["table"], out["auditSummary"] = table, summary
    except audit_vep.EvidenceError as error:
        problems.append(f"audit_vep EvidenceError: {error}")
        out["table"], out["auditSummary"] = None, None
    except Exception as error:
        problems.append(f"audit_vep failed ({type(error).__name__}: {error})")
        out["table"], out["auditSummary"] = None, None
    if run.path is not None:
        import sweep_analyze  # noqa: E402  (the archived reader; checked against the in-memory copy)
        try:
            if sweep_analyze.event_times(run.path / "transcript-00.ndjson") != (boarded, alighted):
                problems.append("in-memory event_times differs from sweep_analyze.event_times")
        except Exception as error:
            problems.append(f"sweep_analyze.event_times failed ({type(error).__name__}: {error})")
    out["boarded"], out["alighted"] = dict(sorted(boarded.items())), dict(sorted(alighted.items()))
    try:
        requests = state.get("requests", [])
        out["served"] = sorted(r["requestId"] for r in requests if r.get("lifecycle") == "completed")
        out["lifecycle"] = {r["requestId"]: r.get("lifecycle")
                            for r in sorted(requests, key=lambda r: r["requestId"])}
    except (KeyError, TypeError, AttributeError) as error:
        problems.append(f"final requests malformed ({type(error).__name__}: {error})")
        out["served"], out["lifecycle"] = None, None
    semantic = {}
    raw_semantic = run.run0.get("semantic") or {}
    if not isinstance(raw_semantic, dict):
        problems.append("run-00 semantic block is not an object")
        raw_semantic = {}
    for key, value in sorted(raw_semantic.items()):
        if key in RUN_SEMANTIC_DERIVED:
            continue
        if HASH_KEY.search(key) and key not in RUN_SEMANTIC_STABLE:
            problems.append(f"UNCLASSIFIED run-00 semantic hash field {key}")
        semantic[key] = value
    out["runSemantic"] = semantic
    out.setdefault("finalOnlineState", None)
    out.setdefault("commitmentLedger", None)
    out.setdefault("incidentLedger", None)
    return out


COMPONENTS = ("decisions", "table", "served", "lifecycle", "boarded", "alighted", "frames",
              "finalOnlineState", "commitmentLedger", "incidentLedger", "runSemantic")


def first_path_difference(a, b, path="$"):
    if type(a) is not type(b):
        return path
    if isinstance(a, dict):
        for key in sorted(set(a) | set(b)):
            if key not in a or key not in b:
                return f"{path}.{key}"
            if a[key] != b[key]:
                return first_path_difference(a[key], b[key], f"{path}.{key}")
        return path
    if isinstance(a, list):
        for index in range(min(len(a), len(b))):
            if a[index] != b[index]:
                return first_path_difference(a[index], b[index], f"{path}[{index}]")
        return f"{path}[len {len(a)}/{len(b)}]"
    return path


def compare(a: dict, b: dict) -> dict:
    result = {}
    for key in COMPONENTS:
        x, y = a.get(key), b.get(key)
        same = x is not None and y is not None and x == y
        detail = ""
        if not same:
            try:
                if x is None or y is None:
                    detail = "missing in " + ("reference" if x is None else "new run")
                elif isinstance(x, list) and isinstance(y, list):
                    n = min(len(x), len(y))
                    first = next((i for i in range(n) if x[i] != y[i]), n)
                    detail = f"lengths {len(x)}/{len(y)}, first difference at index {first}"
                    if key == "frames" and first < n:
                        da, db = json.loads(x[first][1]), json.loads(y[first][1])
                        detail += (f" ({x[first][0]} {da.get('messageType')}; path "
                                   f"{first_path_difference(da, db)})")
                elif isinstance(x, dict) and isinstance(y, dict):
                    differ = [k for k in set(x) | set(y) if x.get(k) != y.get(k)]
                    detail = f"{len(differ)} keys differ, e.g. {sorted(map(str, differ))[:3]}"
                else:
                    detail = f"differs at {first_path_difference(json.loads(x), json.loads(y))}"
            except (ValueError, TypeError, AttributeError) as error:
                detail = f"differs ({type(error).__name__} while locating the difference)"
        result[key] = (same, detail)
    return result


def init_checks(ref: dict, new: dict, binding: str, old_identity: dict, new_identity: dict) -> list[str]:
    """Explicit initialization/provenance checks (R22:183-187, 175-177). Empty list = pass."""
    problems = []
    a, b = ref["initManifest"], new["initManifest"]
    if sorted(a) != sorted(b):
        problems.append(f"initializeRun manifest keys differ: {sorted(set(a) ^ set(b))}")
    for key in sorted(set(a) & set(b)):
        if key != "binarySha256" and a[key] != b[key]:
            problems.append(f"initializeRun manifest field {key} differs")
    for label, manifest in (("reference", a), ("new", b)):
        if manifest.get("policyConfigurationHash") != binding:
            problems.append(f"{label} policyConfigurationHash != expected arm binding {binding[:8]}...")
        if manifest.get("coreCommitSha") != HARNESS_COMMIT:
            problems.append(f"{label} coreCommitSha != frozen harness commit {HARNESS_COMMIT[:7]}")
    for label, identity, manifest in (("reference", old_identity, a), ("new", new_identity, b)):
        if not identity.get("ok"):
            problems.extend(identity.get("problems") or [f"{label} runner identity not verified"])
        elif manifest.get("binarySha256") != identity["dll"]:
            problems.append(f"{label} binarySha256 != the verified DLL of {identity['label']}")
    if old_identity.get("dll") != OLD_RUNNER_DLL or old_identity.get("tree") != OLD_RUNNER_TREE:
        problems.append("reference runner identity is not the archived runner-tier3-v2 (ac251ef3.../a631038b...)")
    return problems


def summary_checks(run: Run, reference: Run | None) -> list[str]:
    problems = []
    if run.summary.get("repositoryInventorySha256") != HARNESS_INVENTORY:
        problems.append(f"{run.label}: repositoryInventorySha256 != frozen {HARNESS_INVENTORY[:8]}...")
    if reference is not None and run.summary.get("sourceScenarioContentSha256") != \
            reference.summary.get("sourceScenarioContentSha256"):
        problems.append(f"{run.label}: sourceScenarioContentSha256 differs from the reference")
    return problems


def pair_report(ref: Run, ref_facts: dict, new: Run, new_facts: dict, binding: str,
                old_identity: dict, new_identity: dict) -> tuple[bool, list[str]]:
    lines = [f"pair {ref.label} (reference) vs {new.label} (new)"]
    pre = [f"reference: {p}" for p in ref_facts["problems"]] + [f"new: {p}" for p in new_facts["problems"]]
    init = init_checks(ref_facts, new_facts, binding, old_identity, new_identity)
    init += summary_checks(ref, None) + summary_checks(new, ref)
    result = compare(ref_facts, new_facts)
    for p in pre[:20]:
        lines.append(f"  PRECONDITION FAIL {p}")
    if len(pre) > 20:
        lines.append(f"  ... {len(pre) - 20} more precondition problems")
    for p in init:
        lines.append(f"  INIT/PROVENANCE FAIL {p}")
    for key, (same, detail) in result.items():
        lines.append(f"  {key}: {'identical' if same else 'DIFFERENT ' + detail}")
    raw_run_id_same = ref_facts["runId"] == new_facts["runId"]
    lines.append(f"  info: raw runId equal {raw_run_id_same}; frames {ref_facts['frameCount']}/"
                 f"{new_facts['frameCount']}; new binarySha256 {new_facts['initManifest'].get('binarySha256')}")
    lines.append(f"  info: digests content-verified reference {ref_facts.get('digestCoverage')} new "
                 f"{new_facts.get('digestCoverage')}; decisions 2..N decisionHash and intermediate state hashes "
                 f"are link-checked only (not recomputable from a transcript)")
    ok = not pre and not init and all(same for same, _ in result.values())
    lines.append(f"  PAIR {'EQUIVALENT' if ok else 'NOT EQUIVALENT'}")
    return ok, lines


# ---------------------------------------------------------------- ON smoke checks (R22 F4, R17 H1; R24 F4)

def _nonneg(value) -> bool:
    return isinstance(value, int) and not isinstance(value, bool) and value >= 0


def _fields(value, required, optional=frozenset()) -> bool:
    return isinstance(value, dict) and required <= set(value) <= (required | optional)


def cf_order_problem(names: list) -> str | None:
    """Cost-first level order (SolverBackedFleetSelection.cs:310-416 with the ADR-078 change): optional
    forced-reference-count, accepted-request-count, operational-cost, then candidate-id-rank:<vehicle> levels.
    A diagnostics list is an ordered PREFIX of the levels (CandidateSelectionSolver.cs:216-226)."""
    expected, position = [], 0
    if names and names[0] == "forced-reference-count":
        expected.append("forced-reference-count")
        position = 1
    for name in ("accepted-request-count", "operational-cost"):
        if position < len(names):
            if names[position] != name:
                return f"level {position} is {names[position]!r}, cost-first order expects {name!r}"
            position += 1
    ranks = names[position:]
    if any(not isinstance(n, str) or not n.startswith(CANDIDATE_RANK) for n in ranks):
        return f"unexpected level after operational-cost: {[n for n in ranks if not str(n).startswith(CANDIDATE_RANK)][:2]}"
    if len(set(ranks)) != len(ranks):
        return "duplicate candidate-id-rank level"
    return None


def decision_evidence(payload, sim_time, cf_order: bool = True) -> tuple[list[str], dict]:
    """F4: the execution evidence of ONE decision. Returns (problems, facts for the report)."""
    where = f"decision at simTime {sim_time}"
    problems, facts_out = [], {"emptyBoundsReason": []}
    solver = payload.get("solver") if isinstance(payload, dict) else None
    if not isinstance(solver, dict):
        return [f"{where}: solver block missing"], facts_out
    facts_out["solverStatus"] = solver.get("status")
    if solver.get("status") != "completed":
        problems.append(f"{where}: solver.status {solver.get('status')!r} (completed required)")
    evidence = solver.get("executionEvidence")
    if not isinstance(evidence, dict):
        return problems + [f"{where}: executionEvidence missing although the ON config emits it "
                           f"(wp4-tier3-v2-costfirst.json:6)"], facts_out
    if set(evidence) != EVIDENCE_FIELDS or evidence.get("evidenceVersion") != EXPECTED_EVIDENCE_VERSION:
        problems.append(f"{where}: executionEvidence fields {sorted(evidence)} / version "
                        f"{evidence.get('evidenceVersion')!r} (expected {EXPECTED_EVIDENCE_VERSION})")
    generation, selection = evidence.get("generation"), evidence.get("selection")
    generation_work = omitted_sum = None
    if not _fields(generation, GENERATION_FIELDS):
        problems.append(f"{where}: generation evidence missing or fields differ")
    else:
        try:
            if not all(_nonneg(generation[k]) for k in ("totalPendingRequestCount", "consideredRequestCount",
                                                        "omittedRequestCount")) or \
                    generation["consideredRequestCount"] + generation["omittedRequestCount"] != \
                    generation["totalPendingRequestCount"]:
                problems.append(f"{where}: request generation accounting invalid")
            generation_work = sum(loss["explorationWorkUnits"] for loss in generation["vehicleLosses"])
            omitted_sum = sum(item["count"] for item in generation["omissions"])
            if any(loss["omissionCountWasSaturated"] is not False for loss in generation["vehicleLosses"]) or any(
                    item["countWasSaturated"] is not False for item in generation["omissions"]):
                problems.append(f"{where}: saturated omission evidence is not auditable (verifier :275-276,294-295)")
        except (KeyError, TypeError) as error:
            problems.append(f"{where}: generation evidence malformed ({type(error).__name__}: {error})")
    if not _fields(selection, SELECTION_REQUIRED, SELECTION_OPTIONAL):
        return problems + [f"{where}: selection evidence missing or fields differ"], facts_out
    for key in ("consumedGenerationWorkUnits", "consumedValidationWorkUnits", "omittedCandidateCount",
                "fallbackValidationAttempts"):
        if not _nonneg(selection[key]):
            problems.append(f"{where}: selection.{key} is not a non-negative integer")
    path, primary_status, final_status = (selection["executionPath"], selection["primarySolveStatus"],
                                          selection["finalSolveStatus"])
    facts_out.update(executionPath=path, primarySolveStatus=primary_status, finalSolveStatus=final_status,
                     selection=selection)
    if selection["omissionCountWasSaturated"] is not False:
        problems.append(f"{where}: omissionCountWasSaturated must be false (saturated evidence is not auditable)")
    if omitted_sum is not None and selection["omittedCandidateCount"] != omitted_sum:
        problems.append(f"{where}: omittedCandidateCount {selection['omittedCandidateCount']} != sum of "
                        f"generation omissions {omitted_sum}")
    if generation_work is not None and selection["consumedGenerationWorkUnits"] != generation_work:
        problems.append(f"{where}: consumedGenerationWorkUnits != sum of vehicle exploration work")
    has_digest = "omissionDigest" in selection
    if (selection["omittedCandidateCount"] == 0) == has_digest or (
            has_digest and not (isinstance(selection["omissionDigest"], str) and HEX64.match(selection["omissionDigest"]))):
        problems.append(f"{where}: omissionDigest/omittedCandidateCount pairing invalid (verifier :442-446)")
    if primary_status not in SOLVE_STATUSES or final_status not in SOLVE_STATUSES:
        problems.append(f"{where}: unknown solve status {primary_status!r}/{final_status!r}")
    if path not in PUBLISHING_PATHS:
        problems.append(f"{where}: executionPath {path!r} is not a validated publishing path")
    diagnostics = {}
    for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
        value = selection[which]
        if not _fields(value, DIAGNOSTICS_REQUIRED, DIAGNOSTICS_OPTIONAL) or not isinstance(
                value["objectiveBounds"], list) or not _nonneg(value["consumedWorkUnits"]) or not _nonneg(
                value["consumedDeterministicTimeMicros"]):
            problems.append(f"{where}: {which} missing or malformed")
            continue
        bounds = value["objectiveBounds"]
        if any(not _fields(b, BOUND_FIELDS) for b in bounds):
            problems.append(f"{where}: {which} objective bound fields differ")
            continue
        if [b["levelIndex"] for b in bounds] != list(range(len(bounds))):
            problems.append(f"{where}: {which} levelIndex is not 0..n-1")
        names = [b["objectiveName"] for b in bounds]
        if cf_order and (FORBIDDEN_CF_LEVEL in names or any(str(n).startswith("revision:") for n in names)):
            problems.append(f"{which} at simTime {sim_time} still ranks stability levels")
        elif cf_order:
            order = cf_order_problem(names)
            if order:
                problems.append(f"{where}: {which} {order}")
        if "operational-cost" in names and ("accepted-request-count" not in names or
                                            names.index("accepted-request-count") > names.index("operational-cost")):
            problems.append(f"{which} at simTime {sim_time}: accepted count not before cost")
        diagnostics[which] = value
    primary, final = diagnostics.get("primarySolverDiagnostics"), diagnostics.get("finalSolverDiagnostics")
    facts_out.update(primary=primary, final=final)
    # path / status consistency (CandidateSelectionExecution.cs:246-264,267-328,366-397)
    if path == "validatedIncumbent":
        if final_status != primary_status or final_status not in ("optimal", "feasible"):
            problems.append(f"{where}: validatedIncumbent with primary/final status {primary_status}/{final_status}")
        if primary is not None and final is not None and primary != final:
            problems.append(f"{where}: validatedIncumbent but final diagnostics differ from primary")
        if selection["fallbackValidationAttempts"] != 0 or selection["primaryIncumbentRejected"] is not False or \
                selection["validationWitnesses"] != []:
            problems.append(f"{where}: validatedIncumbent with fallback/rejection evidence")
    elif path in FALLBACK_DETAIL:
        if final_status != "safeFallback":
            problems.append(f"{where}: {path} with finalSolveStatus {final_status!r} (safeFallback required)")
        if final is not None and (final["objectiveBounds"] != [] or final.get("detailCode") != FALLBACK_DETAIL[path]):
            problems.append(f"{where}: {path} final diagnostics must have no bounds and detailCode "
                            f"{FALLBACK_DETAIL[path]}")
        if not (_nonneg(selection["fallbackValidationAttempts"]) and selection["fallbackValidationAttempts"] >= 1):
            problems.append(f"{where}: {path} without a fallback validation attempt")
        if primary is not None and final is not None and (
                final["consumedWorkUnits"], final["consumedDeterministicTimeMicros"]) != (
                primary["consumedWorkUnits"], primary["consumedDeterministicTimeMicros"]):
            problems.append(f"{where}: {path} final work counters differ from the primary solve")
        if final is not None and final["objectiveBounds"] == [] and final.get("detailCode") == FALLBACK_DETAIL[path]:
            facts_out["emptyBoundsReason"].append(f"final: {path} fallback ({FALLBACK_DETAIL[path]})")
    # bounds presence: absence needs a supported reason (R24:114)
    for which, status in (("primarySolverDiagnostics", primary_status), ("finalSolverDiagnostics", final_status)):
        value = diagnostics.get(which)
        if value is None:
            continue
        bounds = value["objectiveBounds"]
        if status == "optimal":
            if not bounds or "operational-cost" not in [b["objectiveName"] for b in bounds]:
                problems.append(f"{where}: {which} optimal without bounds through operational-cost "
                                f"(OrToolsCandidateSelectionSolver.cs:30-61,254-318 reports every level)")
            elif any(b["isProvenOptimal"] is not True or b["gapNumerator"] != 0 or b["incumbentValue"] != b["bestBound"]
                     or not (_nonneg(b["gapDenominator"]) and b["gapDenominator"] >= 1) for b in bounds):
                problems.append(f"{where}: {which} optimal with an unproven level")
        elif not bounds:
            if status == "safeFallback" and which == "finalSolverDiagnostics" and path in FALLBACK_DETAIL:
                continue      # reason recorded above
            if status in ("feasible", "unknown", "infeasible", "modelInvalid") and which == "primarySolverDiagnostics" \
                    or (status in ("feasible",) and path == "validatedIncumbent"):
                facts_out["emptyBoundsReason"].append(f"{which.replace('SolverDiagnostics', '')}: status {status} "
                                                      f"(unproven prefix, CandidateSelectionSolver.cs:216-226)")
            else:
                problems.append(f"{where}: {which} has no objective bounds without a supported reason "
                                f"(status {status}, path {path})")
    return problems, facts_out


def objective_level_problems(run, cf_order: bool = True) -> tuple[list[str], dict]:
    """All decisions of a run: evidence problems and the report statistics (bounds and work values; R22:66)."""
    problems, info = [], collections.Counter()
    levels = collections.defaultdict(lambda: [0, 0, 0, 0])     # name -> [count, sum incumbent, sum bound, unproven]
    work = collections.defaultdict(lambda: [0, 0, 0, 0])       # diagnostics -> [sum work, max work, sum micros, max]
    shapes = collections.Counter()
    for index, line in enumerate(run.lines):
        try:
            record = json.loads(line)
            envelope = json.loads(base64.b64decode(record["frameBase64"]))
            if record["direction"] != "runnerToAdapter" or envelope.get("messageType") != "decision":
                continue
            payload = envelope["payload"]
            sim_time = envelope.get("simTimeMs")
        except (ValueError, KeyError, TypeError, AttributeError, binascii.Error) as error:
            problems.append(f"frame {index + 1}: undecodable ({type(error).__name__})")
            continue
        found, decision_facts = decision_evidence(payload, sim_time, cf_order)
        problems += found
        info["decisions"] += 1
        info[f"solver.status={decision_facts.get('solverStatus')}"] += 1
        info[f"executionPath={decision_facts.get('executionPath')}"] += 1
        info[f"primarySolveStatus={decision_facts.get('primarySolveStatus')}"] += 1
        info[f"finalSolveStatus={decision_facts.get('finalSolveStatus')}"] += 1
        for reason in decision_facts["emptyBoundsReason"]:
            info[f"emptyBounds[{reason}]"] += 1
        selection = decision_facts.get("selection") or {}
        for key in ("consumedGenerationWorkUnits", "consumedValidationWorkUnits", "omittedCandidateCount",
                    "fallbackValidationAttempts"):
            if _nonneg(selection.get(key)):
                info[f"selection.{key}(sum)"] += selection[key]
        for which in ("primary", "final"):
            value = decision_facts.get(which)
            if value is None:
                info[f"{which}SolverDiagnostics=absent/malformed"] += 1
                continue
            entry = work[which]
            entry[0] += value["consumedWorkUnits"]
            entry[1] = max(entry[1], value["consumedWorkUnits"])
            entry[2] += value["consumedDeterministicTimeMicros"]
            entry[3] = max(entry[3], value["consumedDeterministicTimeMicros"])
            names = []
            for bound in value["objectiveBounds"]:
                name = str(bound.get("objectiveName"))
                key = CANDIDATE_RANK + "*" if name.startswith(CANDIDATE_RANK) else name
                names.append(key)
                slot = levels[f"{which}:{key}"]
                slot[0] += 1
                if _nonneg(bound.get("incumbentValue")) and _nonneg(bound.get("bestBound")):
                    slot[1] += bound["incumbentValue"]
                    slot[2] += bound["bestBound"]
                slot[3] += bound.get("isProvenOptimal") is not True
            ranks = names.count(CANDIDATE_RANK + "*")
            shape = [n for n in names if n != CANDIDATE_RANK + "*"] + ([f"{CANDIDATE_RANK}*x{ranks}"] if ranks else [])
            shapes[f"{which}: {' > '.join(shape) or '(no bounds)'}"] += 1
    stats = dict(sorted(info.items()))
    for which, (sum_work, max_work, sum_micros, max_micros) in sorted(work.items()):
        stats[f"{which}.consumedWorkUnits sum/max"] = f"{sum_work}/{max_work}"
        stats[f"{which}.consumedDeterministicTimeMicros sum/max"] = f"{sum_micros}/{max_micros}"
    for name, (count, incumbent, bound, unproven) in sorted(levels.items()):
        stats[f"bound {name}: n/sum incumbent/sum bestBound/unproven"] = f"{count}/{incumbent}/{bound}/{unproven}"
    for shape, count in shapes.most_common():
        stats[f"level shape {shape}"] = count
    return problems, stats


def max_drop_c_ms(table) -> int:
    """Per rider sum |p - e| over drop revisions not closed (sweep_analyze.rider_values, :60-70); the maximum."""
    totals = collections.Counter()
    for request, coordinate, _, kind, _, _, _, _, _, c, _, closed in table:
        if coordinate == "drop" and kind != "initialPromise" and not closed:
            totals[request] += abs(c)
    return max(totals.values(), default=0)


def smoke_report(run: Run, arm: str, new_identity: dict) -> tuple[bool, list[str]]:
    run_facts = facts(run, new_identity.get("dll") if new_identity.get("ok") else None)
    lines = [f"smoke {run.label} (ON, arm {arm}); configuration identity = verified ON binding (R22:242); objective "
             f"diagnostics below are supporting evidence"]
    problems = list(run_facts["problems"]) + summary_checks(run, None)
    manifest = run_facts["initManifest"]
    if manifest.get("policyConfigurationHash") != BINDINGS[("on", arm)]:
        problems.append(f"policyConfigurationHash {str(manifest.get('policyConfigurationHash'))[:8]}... != "
                        f"CF binding {BINDINGS[('on', arm)][:8]}...")
    if manifest.get("coreCommitSha") != HARNESS_COMMIT:
        problems.append("coreCommitSha != frozen harness commit")
    if not new_identity.get("ok") or manifest.get("binarySha256") != new_identity.get("dll"):
        problems.append("binarySha256 is not the verified new Runner DLL")
    level_problems, info = objective_level_problems(run)
    if not info.get("decisions"):
        level_problems.append("no decision with execution evidence")
    problems += level_problems[:20]
    if len(level_problems) > 20:
        problems.append(f"... {len(level_problems) - 20} more execution-evidence problems")
    if run_facts["table"] is not None:
        worst = max_drop_c_ms(run_facts["table"])
        lines.append(f"  max per-rider drop sum|c| {worst} ms")
        if arm == "C-30" and worst > 30_000:
            problems.append(f"CF-C30 rider with drop sum|c| {worst} ms > 30000 (H1 contract)")
    audit = run_facts.get("auditSummary") or {}
    lines.append(f"  served {len(run_facts['served'] or [])}; nonNormalCertificates "
                 f"{audit.get('nonNormalCertificates')}; breaches {audit.get('breaches')}")
    lines.append(f"  digests content-verified {run_facts.get('digestCoverage')}")
    lines += [f"  {key}: {value}" for key, value in info.items()]
    for p in problems:
        lines.append(f"  FAIL {p}")
    ok = not problems
    lines.append(f"  SMOKE {'PASS' if ok else 'FAIL'}")
    return ok, lines


# ---------------------------------------------------------------- CLI

def write(path, lines: list[str]) -> None:
    text = "\n".join(lines) + "\n"
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def report_path_problems(mode: str, name: str) -> list[str]:
    """F3: the report is a bare, new file name in this folder with the phase prefix; never a verdict name."""
    problems = []
    if not isinstance(name, str) or pathlib.PurePath(name).name != name or "/" in name or "\\" in name:
        return [f"report must be a bare file name written into {HERE} (got {name!r})"]
    if not REPORT_NAME[mode].match(name):
        problems.append(f"report name {name!r} must match {REPORT_NAME[mode].pattern}")
    if name in VERDICT_FILES:
        problems.append(f"report name {name!r} is a reserved verdict file")
    if (HERE / name).exists():
        problems.append(f"report {name} already exists (outputs are never overwritten)")
    return problems


def expected_binding_from_files(arm: str, wp4: pathlib.Path) -> str:
    return cj.binding((TANG3 / "configs" / f"{arm}.json").read_bytes(), wp4.read_bytes())


def evaluate_gate0(s, old_identity: dict, new_identity: dict, input_problems: list[str]) -> tuple[bool, list[str]]:
    lines, all_ok, new_dlls = [], not input_problems, set()
    for problem in input_problems:
        lines.append(f"INPUT/FREEZE FAIL (jobs already started) {problem}")
    for name, arm, cell, mode, _label in s.jobs("gate0"):
        try:
            binding = expected_binding_from_files(arm, s.WP4_OFF)
            if binding != BINDINGS[("off", arm)]:
                all_ok = False
                lines.append(f"{name}: binding from config bytes {binding} != archived")
            ref_dir, new_dir = s.REFERENCE_ROOT / name, s.OUT_ROOT / "gate0" / name
            if any(not (new_dir / f).exists() for f in ("bundle-manifest.json",) + BUNDLE_FILES):
                all_ok = False
                lines.append(f"{name}: new run incomplete (no summary/bundle) -> NOT EQUIVALENT (a failed job is "
                             f"not a pass, R22:208)")
                continue
            ref_run = load_run(ref_dir)
            want_sha, want_frames = REFERENCE_TRANSCRIPTS[name]
            if ref_run.transcript_sha256 != want_sha or len(ref_run.lines) != want_frames:
                all_ok = False
                lines.append(f"{name}: reference transcript is not the R22-verified artifact")
            new_run = load_run(new_dir)
            new_facts = facts(new_run, new_identity.get("dll") if new_identity["ok"] else None)
            ok, pair_lines = pair_report(ref_run, facts(ref_run, old_identity.get("dll")), new_run, new_facts,
                                         binding, old_identity, new_identity)
            new_dlls.add(new_facts["initManifest"].get("binarySha256"))
            all_ok = all_ok and ok
            lines += pair_lines
        except Exception as error:     # F3: a started, malformed run is a failed job, never an escape
            all_ok = False
            lines.append(f"{name}: malformed or unreadable run artifact -> NOT EQUIVALENT "
                         f"({type(error).__name__}: {error})")
    if len(new_dlls) != 1:
        all_ok = False
        lines.append(f"the new jobs do not agree on one binarySha256: {sorted(map(str, new_dlls))}")
    return all_ok, lines


def evaluate_smoke(s, new_identity: dict, input_problems: list[str]) -> tuple[bool, list[str]]:
    lines, all_ok = [], not input_problems
    for problem in input_problems:
        lines.append(f"INPUT/FREEZE FAIL (jobs already started) {problem}")
    for name, arm, cell, mode, _label in s.jobs("smoke"):
        try:
            directory = s.OUT_ROOT / "smoke" / name
            if any(not (directory / f).exists() for f in ("bundle-manifest.json",) + BUNDLE_FILES):
                all_ok = False
                lines.append(f"{name}: run incomplete (no summary/bundle) -> FAIL")
                continue
            ok, smoke_lines = smoke_report(load_run(directory), arm, new_identity)
            all_ok = all_ok and ok
            lines += smoke_lines
        except Exception as error:     # F3
            all_ok = False
            lines.append(f"{name}: malformed or unreadable run artifact -> FAIL ({type(error).__name__}: {error})")
    return all_ok, lines


def main(argv: list[str]) -> int:
    if not argv or argv[0] not in ("pair", "gate0", "smoke") or len(argv) != (5 if argv[0] == "pair" else 2):
        print(__doc__)
        return 2
    mode, name = argv[0], argv[-1]
    problems = report_path_problems(mode, name)
    if problems:                          # validated before anything is evaluated or written (F3)
        raise SystemExit(f"refused (nothing written): {problems}")
    s = driver()
    old_identity = runner_identity_from_disk(s.OLD_RUNNER, s.OLD_RUNNER_MANIFEST, "runner-tier3-v2")
    new_identity = runner_identity_from_disk(s.RUNNER, s.RUNNER_MANIFEST, "runner-tier3-v3")
    header = [f"# compare_equivalence_stage1b-v2.py {' '.join(argv)}",
              f"old runner: ok {old_identity['ok']} tree {old_identity['tree']} dll {old_identity['dll']} "
              f"{old_identity['problems']}",
              f"new runner: ok {new_identity['ok']} tree {new_identity['tree']} dll {new_identity['dll']} "
              f"{new_identity['problems']}"]
    if mode == "pair":
        ref, new, arm = pathlib.Path(argv[1]), pathlib.Path(argv[2]), argv[3]
        binding = expected_binding_from_files(arm, s.WP4_OFF)
        ok_binding = binding == BINDINGS[("off", arm)]
        try:
            ref_run, new_run = load_run(ref), load_run(new)
            ok, lines = pair_report(ref_run, facts(ref_run, old_identity.get("dll")), new_run,
                                    facts(new_run, new_identity.get("dll") if new_identity["ok"] else None),
                                    binding, old_identity, new_identity)
        except Exception as error:
            ok, lines = False, [f"malformed or unreadable run artifact ({type(error).__name__}: {error})"]
        lines.insert(0, f"expected binding from config bytes {binding} equals archived: {ok_binding}")
        lines.append("exploratory pair comparison; this is NOT a Gate-0 verdict")
        write(HERE / name, header + lines + [f"EQUIVALENT: {ok and ok_binding}"])
        print("\n".join(header + lines))
        return 0 if ok and ok_binding else 1
    refusals = s.latch_problems(mode, for_comparator=True)
    if refusals:                          # a failure latch or this phase's own verdict exists: nothing is judged
        raise SystemExit(f"refused (nothing written): {refusals}")
    not_ready = s.readiness_problems(mode)
    if mode == "smoke":
        not_ready += s.verdict_problems(s.GATE0_PASSED, s.GATE0_FAILED, "GATE0", new_identity)
    not_started = [job for job, *_ in s.jobs(mode) if not (s.OUT_ROOT / mode / job).exists()]
    if not_ready or not_started:          # not ready is not a verdict (unsealed / unpublished / unstarted)
        raise SystemExit(f"refused, not ready (no verdict written): {not_ready}; jobs not started {not_started}")
    input_problems = s.frozen_input_problems(mode, for_comparator=True)
    if mode == "gate0":
        all_ok, lines = evaluate_gate0(s, old_identity, new_identity, input_problems)
        verdict_name = "GATE0-PASSED.txt" if all_ok else "GATE0-FAILED.txt"
        verdict = "GATE0 PASSED" if all_ok else "GATE0 FAILED"
    else:
        all_ok, lines = evaluate_smoke(s, new_identity, input_problems)
        verdict_name = "SMOKE-PASSED.txt" if all_ok else "SMOKE-FAILED.txt"
        verdict = "SMOKE PASSED" if all_ok else "SMOKE FAILED"
    footer = [f"runnerDllSha256 {new_identity['dll']}", f"runnerTreeSha256 {new_identity['tree']}", verdict]
    write(HERE / verdict_name, header + lines + footer)      # the verdict first, exactly once (mode "x")
    write(HERE / name, header + lines + footer)
    print("\n".join(header + lines + footer))
    return 0 if all_ok else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
