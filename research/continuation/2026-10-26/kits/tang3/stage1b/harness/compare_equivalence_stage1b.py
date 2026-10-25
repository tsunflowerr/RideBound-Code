"""Stage-1b Gate-0 comparator: the exact criterion of Codex R22 §5 (optimize/codex-debate/codex-work/
R22-CODE-REVIEW-CODEX.md:158-222), built as a reviewed wrapper around the archived comparator
optimize/compare_equivalence.py (kept unchanged). It keeps that comparator's five exact components (:47-75) and fixes
F1/F2 (R22:32-50):
  * F1: the ONLY permitted initialization difference is initializeRun.payload.manifest.binarySha256, and only after
    each value is verified against its own Runner DLL and complete runner-tree manifest (R22:187).
  * F2: stable configuration/input hashes are no longer stripped; they must match exactly (R22:183-186), and the
    initialization policyConfigurationHash must equal the archived arm binding recomputed from the config bytes.

What is compared (all exact, no epsilon, no prefix pass, R22:198-208):
  1. decisions  ordered fleet_decisions (tang3/pilot/pilot_analyze.py:36-54, imported unchanged)
  2. table      every signed v/e/p audit row (tang3/evidence/audit_vep.py:84-128, imported unchanged; its
                cross-checks raise on an inconsistent ledger/publication)
  3. served set, lifecycle per request, boarding and alight times (event rule of tang3/sweep/sweep_analyze.py:40-57,
                re-implemented in memory; on real runs it is checked against the original function)
  4. frames     every decoded frame, direction and position, after the narrow normalization below
  5. final online state, commitment ledger and incident ledger (normalized), reported separately
  6. run-00.json semantic block minus its three run-derived digests
Normalization (R22:179-194), applied to decoded copies only:
  * binarySha256 at exactly ('adapterToRunner', initializeRun, payload.manifest.binarySha256) -> one common token,
    only if the raw value equals the verified DLL SHA-256 of that run's runner tree; otherwise left raw (fails).
  * STABLE hash keys are kept raw: policyConfigurationHash, scenarioContentHash, graphSnapshotHash,
    travelTimeSnapshotHash, snapshotHash, expectedInitialTravelTimeSnapshotHash (and coreCommitSha, versions, IDs).
  * DERIVED hash keys (manifestHash, stateHash, stateBeforeHash, stateAfterHash, inputStateHash, proposedStateHash,
    decisionHash, previousDecisionHash, checkpointHash) are renamed H0, H1, ... in order of first appearance, one
    namespace per run, the all-zero genesis hash kept literal. Equal raw values get equal tokens, so every link
    (ACK, chain, state continuity) must have the same pattern in both runs. The links are also validated per run.
  * Salted identities: runId -> "<run>"; publicationId -> "pub:<requestId>#<ledger entry index>" and
    breachId -> "breach:<kind>:<requestId>:<recordedEpoch>:<sourceEventSeq>#<n>", both built from the final ledgers;
    a publication/breach reference that has no ledger record is a failure. publicationIds lists are compared as
    sorted membership; the decision "actions" list is compared as a sorted multiset (compare_equivalence.py:39-40).
  * Any other key ending in hash/Hash/sha256/Sha256/digest/Digest is UNCLASSIFIED and fails (review first).
Transport fields (frameBase64, length, frameSha256, ordinal) are checked per run, not compared across runs.

Usage:
  python -B compare_equivalence_stage1b.py pair <ref-run> <new-run> <U|C-30> <out-file>
  python -B compare_equivalence_stage1b.py gate0 <out-file>   four Gate-0 pairs -> GATE0-PASSED.txt / GATE0-FAILED.txt
  python -B compare_equivalence_stage1b.py smoke <out-file>   two ON smokes -> SMOKE-PASSED.txt / SMOKE-FAILED.txt
Every output is created with mode "x" (never overwritten). Reads runs and runner trees; writes only into this folder.
"""
from __future__ import annotations

import base64
import binascii
import collections
import hashlib
import json
import pathlib
import re
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent.parent
for sub in ("pilot", "evidence", "sweep"):
    sys.path.insert(0, str(TANG3 / sub))
sys.path.insert(0, str(TANG3))
sys.path.insert(0, str(HERE))
import audit_vep  # noqa: E402
import runner_manifest  # noqa: E402
from pilot_analyze import fleet_decisions  # noqa: E402

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


def canonical(value) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class MemText:
    """Duck-typed path for the archived readers (audit_vep.read_frames and pilot_analyze.fleet_decisions only call
    .read_text(encoding="utf-8")), so they run unchanged on in-memory transcripts."""

    def __init__(self, text: str, label: str):
        self._text, self._label = text, label

    def read_text(self, encoding: str = "utf-8") -> str:
        return self._text

    def __str__(self) -> str:
        return self._label


# ---------------------------------------------------------------- runner identity (F1/F3)

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
    """A run bundle in memory: the bytes of bundle-manifest.json, run-00.json, summary.json, transcript-00.ndjson."""

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
        except (KeyError, ValueError) as error:
            raise SystemExit(f"{label}: unreadable run bundle: {error}")
        listed = {item.get("path"): item for item in self.bundle.get("files", [])}
        if sorted(listed) != sorted(BUNDLE_FILES):
            self.problems.append(f"bundle lists {sorted(listed)}, expected {list(BUNDLE_FILES)}")
        for name, item in listed.items():
            data = files.get(name)
            if data is None or len(data) != item.get("lengthBytes") or sha256(data) != item.get("sha256"):
                self.problems.append(f"bundle file {name} does not match bundle-manifest.json")
        if self.summary.get("status") != "pass":
            self.problems.append(f"summary status {self.summary.get('status')!r} (pass required)")
        if self.run0.get("status") != "succeeded":
            self.problems.append(f"run-00 status {self.run0.get('status')!r} (succeeded required)")
        self.transcript_sha256 = sha256(raw)
        self.text = raw.decode("utf-8")
        self.lines = self.text.split("\n")
        if self.lines and self.lines[-1] == "":
            self.lines.pop()
        else:
            self.problems.append("transcript does not end with a newline")

    @classmethod
    def load(cls, directory: pathlib.Path) -> "Run":
        if not directory.is_dir():
            raise SystemExit(f"run folder missing: {directory}")
        names = sorted(p.name for p in directory.iterdir())
        files = {name: (directory / name).read_bytes() for name in names
                 if name in BUNDLE_FILES + ("bundle-manifest.json",)}
        extra = tuple(n for n in names if n not in BUNDLE_FILES + ("bundle-manifest.json",))
        return cls(directory.name, files, directory, extra)

    def frame(self, index: int):
        record = json.loads(self.lines[index])
        return record["direction"], json.loads(base64.b64decode(record["frameBase64"]))


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
        if value in self.publications:
            return self.publications[value]
        self.problems.append(f"publication reference at {where} has no ledger record")
        return f"UNMAPPED:{value}"

    def breach(self, value, where):
        if value in self.breaches:
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
    """Integrity, link checks and the compared components of one run. Problems go to out['problems']."""
    problems = list(run.problems)
    out = {"label": run.label, "problems": problems, "frameCount": len(run.lines),
           "transcriptSha256": run.transcript_sha256}
    decoded_tail, final_index = None, None
    for index in range(len(run.lines) - 1, max(-1, len(run.lines) - 8), -1):
        try:
            direction, envelope = run.frame(index)
        except (ValueError, KeyError, binascii.Error):
            continue
        if direction == "runnerToAdapter" and envelope.get("messageType") == "checkpoint":
            decoded_tail, final_index = envelope, index
            break
    if decoded_tail is None:
        problems.append("no final Runner checkpoint")
        state = {}
    else:
        state = decoded_tail["payload"]["content"]["onlineState"]
    try:
        _, init_envelope = run.frame(2)
        run_id = init_envelope["runId"]
        init_manifest = init_envelope["payload"]["manifest"]
        if init_envelope.get("messageType") != "initializeRun":
            raise KeyError("frame 3 is not initializeRun")
    except (ValueError, KeyError, IndexError, binascii.Error) as error:
        problems.append(f"no initializeRun at frame 3: {error}")
        run_id, init_manifest = None, {}
    out["initManifest"] = init_manifest
    out["runId"] = run_id
    publications, breaches = ledger_maps(state, problems)
    normalizer = Normalizer(run_id, publications, breaches, verified_dll, problems)

    frames, boarded, alighted = [], {}, {}
    initialized = last_decision = first_decision = pending = None
    action_publications = set()
    kinds = collections.Counter()
    for index, line in enumerate(run.lines):
        try:
            record = json.loads(line)
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
        except (ValueError, KeyError, binascii.Error) as error:
            problems.append(f"frame {index + 1}: undecodable ({error})")
            frames.append(("?", "UNDECODABLE"))
            continue
        direction, kind = record["direction"], envelope.get("messageType")
        kinds[(direction, kind)] += 1
        # ---- per-run link checks (verified on the four archived references: 0 violations)
        if "runId" in envelope and envelope["runId"] != run_id:
            problems.append(f"frame {index + 1}: runId differs from initializeRun")
        if direction == "runnerToAdapter" and kind == "initialized":
            initialized = envelope["payload"]
        elif direction == "runnerToAdapter" and kind == "decision":
            payload = envelope["payload"]
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
            if sorted(published) != sorted(certified) or len(set(published)) != len(published):
                problems.append(f"frame {index + 1}: certificate publicationIds != published actions")
            if action_publications & set(published):
                problems.append(f"frame {index + 1}: publication published twice")
            action_publications |= set(published)
            if first_decision is None:
                first_decision = payload
            last_decision, pending = payload, (payload.get("decisionHash"), envelope.get("epochId"))
        elif direction == "adapterToRunner" and kind == "decisionApplied":
            if pending != (envelope["payload"].get("decisionHash"), envelope.get("epochId")):
                problems.append(f"frame {index + 1}: decisionApplied does not acknowledge the last decision")
            pending = None
        elif direction == "runnerToAdapter" and kind == "checkpoint":
            content = envelope["payload"]["content"]
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
        normalized = normalizer.walk(envelope, (), (direction, kind))
        frames.append((direction, canonical(normalized)))
        if index == final_index:
            out["finalOnlineState"] = canonical(normalized["payload"]["content"]["onlineState"])
            out["commitmentLedger"] = [canonical(h) for h in normalized["payload"]["content"]["onlineState"]
                                       .get("commitmentLedger", [])]
            out["incidentLedger"] = canonical(normalized["payload"]["content"]["onlineState"].get("incidentLedger"))
    if pending is not None:
        problems.append("the last decision was not acknowledged")
    if initialized is None or first_decision is None:
        problems.append("missing initialized frame or decisions")
    elif initialized.get("initialStateIdentity", {}).get("stateHash") != first_decision.get("stateBeforeHash"):
        problems.append("initialized stateHash != first decision stateBeforeHash")
    if run.lines:
        try:
            last_direction, last_envelope = run.frame(len(run.lines) - 1)
            if (last_direction, last_envelope.get("messageType")) != ("adapterToRunner", "shutdown"):
                problems.append("last frame is not the adapter shutdown")
        except (ValueError, KeyError, binascii.Error):
            problems.append("last frame undecodable")
    if final_index is not None and final_index < len(run.lines) - 3:
        problems.append("final Runner checkpoint is not at the end of the transcript")
    if set(publications) - action_publications:
        problems.append("a ledger publication has no published action")
    out["frames"] = frames
    out["messageCounts"] = {f"{d}:{k}": n for (d, k), n in sorted(kinds.items())}
    # ---- components 1-3 with the archived readers
    memory = MemText(run.text, run.label)
    out["decisions"] = fleet_decisions(memory)
    try:
        table, summary = audit_vep.audit(memory)
        if table is None:
            problems.append("audit_vep: no final checkpoint")
        out["table"], out["auditSummary"] = table, summary
    except audit_vep.EvidenceError as error:
        problems.append(f"audit_vep EvidenceError: {error}")
        out["table"], out["auditSummary"] = None, None
    if run.path is not None:
        import sweep_analyze  # noqa: E402  (the archived reader; checked against the in-memory copy)
        if sweep_analyze.event_times(run.path / "transcript-00.ndjson") != (boarded, alighted):
            problems.append("in-memory event_times differs from sweep_analyze.event_times")
    out["boarded"], out["alighted"] = dict(sorted(boarded.items())), dict(sorted(alighted.items()))
    requests = state.get("requests", [])
    out["served"] = sorted(r["requestId"] for r in requests if r.get("lifecycle") == "completed")
    out["lifecycle"] = {r["requestId"]: r.get("lifecycle") for r in sorted(requests, key=lambda r: r["requestId"])}
    semantic = {}
    for key, value in sorted((run.run0.get("semantic") or {}).items()):
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
            if x is None or y is None:
                detail = "missing in " + ("reference" if x is None else "new run")
            elif isinstance(x, list):
                n = min(len(x), len(y))
                first = next((i for i in range(n) if x[i] != y[i]), n)
                detail = f"lengths {len(x)}/{len(y)}, first difference at index {first}"
                if key == "frames" and first < n:
                    da, db = json.loads(x[first][1]), json.loads(y[first][1])
                    detail += (f" ({x[first][0]} {da.get('messageType')}; path "
                               f"{first_path_difference(da, db)})")
            elif isinstance(x, dict):
                differ = [k for k in set(x) | set(y) if x.get(k) != y.get(k)]
                detail = f"{len(differ)} keys differ, e.g. {sorted(differ)[:3]}"
            else:
                detail = f"differs at {first_path_difference(json.loads(x), json.loads(y))}"
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
    ok = not pre and not init and all(same for same, _ in result.values())
    lines.append(f"  PAIR {'EQUIVALENT' if ok else 'NOT EQUIVALENT'}")
    return ok, lines


# ---------------------------------------------------------------- ON smoke checks (R22 F4, R17 H1)

def objective_level_problems(run: Run) -> tuple[list[str], dict]:
    problems, info = [], collections.Counter()
    for line in run.lines:
        record = json.loads(line)
        envelope = json.loads(base64.b64decode(record["frameBase64"]))
        if record["direction"] != "runnerToAdapter" or envelope.get("messageType") != "decision":
            continue
        solver = envelope["payload"].get("solver") or {}
        info[f"solver.status={solver.get('status')}"] += 1
        selection = (solver.get("executionEvidence") or {}).get("selection") or {}
        info[f"executionPath={selection.get('executionPath')}"] += 1
        info[f"finalSolveStatus={selection.get('finalSolveStatus')}"] += 1
        info["omittedCandidateCount(sum)"] += selection.get("omittedCandidateCount") or 0
        info["omissionCountWasSaturated"] += bool(selection.get("omissionCountWasSaturated"))
        for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
            diagnostics = selection.get(which)
            if not isinstance(diagnostics, dict):
                info[f"{which}=absent"] += 1
                continue
            names = [b.get("objectiveName") for b in diagnostics.get("objectiveBounds", [])]
            if FORBIDDEN_CF_LEVEL in names or any(str(n).startswith("revision:") for n in names):
                problems.append(f"{which} at simTime {envelope.get('simTimeMs')} still ranks stability levels")
            if "operational-cost" in names and ("accepted-request-count" not in names or
                                                names.index("accepted-request-count") > names.index("operational-cost")):
                problems.append(f"{which} at simTime {envelope.get('simTimeMs')}: accepted count not before cost")
            info[f"{which}.levels={len(names)}"] += 1
            info[f"{which}.forced-level-present"] += "forced-reference-count" in names
    return problems, dict(sorted(info.items()))


def max_drop_c_ms(table) -> int:
    """Per rider sum |p - e| over drop revisions not closed (sweep_analyze.rider_values, :60-70); the maximum."""
    totals = collections.Counter()
    for request, coordinate, _, kind, _, _, _, _, _, c, _, closed in table:
        if coordinate == "drop" and kind != "initialPromise" and not closed:
            totals[request] += abs(c)
    return max(totals.values(), default=0)


def smoke_report(run: Run, arm: str, new_identity: dict) -> tuple[bool, list[str]]:
    run_facts = facts(run, new_identity.get("dll") if new_identity.get("ok") else None)
    lines = [f"smoke {run.label} (ON, arm {arm})"]
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
    problems += level_problems[:20]
    if len(level_problems) > 20:
        problems.append(f"... {len(level_problems) - 20} more objective-level problems")
    if run_facts["table"] is not None:
        worst = max_drop_c_ms(run_facts["table"])
        lines.append(f"  max per-rider drop sum|c| {worst} ms")
        if arm == "C-30" and worst > 30_000:
            problems.append(f"CF-C30 rider with drop sum|c| {worst} ms > 30000 (H1 contract)")
    audit = run_facts.get("auditSummary") or {}
    lines.append(f"  served {len(run_facts['served'])}; nonNormalCertificates {audit.get('nonNormalCertificates')}; "
                 f"breaches {audit.get('breaches')}")
    lines += [f"  {key}: {value}" for key, value in info.items()]
    for p in problems:
        lines.append(f"  FAIL {p}")
    ok = not problems
    lines.append(f"  SMOKE {'PASS' if ok else 'FAIL'}")
    return ok, lines


# ---------------------------------------------------------------- CLI

def write(path: pathlib.Path | str, lines: list[str]) -> None:
    text = "\n".join(lines) + "\n"
    with open(path, "x", encoding="utf-8", newline="\n") as handle:
        handle.write(text)


def expected_binding_from_files(arm: str, wp4: pathlib.Path) -> str:
    import canonical_wp4 as cj  # noqa: E402
    return cj.binding((TANG3 / "configs" / f"{arm}.json").read_bytes(), wp4.read_bytes())


def main(argv: list[str]) -> int:
    if not argv or argv[0] not in ("pair", "gate0", "smoke"):
        print(__doc__)
        return 2
    import run_stage1b as s  # noqa: E402  (constants and the frozen-input checks of the driver)
    old_identity = runner_identity_from_disk(s.OLD_RUNNER, s.OLD_RUNNER_MANIFEST, "runner-tier3-v2")
    new_identity = runner_identity_from_disk(s.RUNNER, s.RUNNER_MANIFEST, "runner-tier3-v3")
    header = [f"# compare_equivalence_stage1b.py {' '.join(argv)}",
              f"old runner: ok {old_identity['ok']} tree {old_identity['tree']} dll {old_identity['dll']} "
              f"{old_identity['problems']}",
              f"new runner: ok {new_identity['ok']} tree {new_identity['tree']} dll {new_identity['dll']} "
              f"{new_identity['problems']}"]
    if argv[0] == "pair":
        ref, new, arm, out = pathlib.Path(argv[1]), pathlib.Path(argv[2]), argv[3], argv[4]
        binding = expected_binding_from_files(arm, s.WP4_OFF)
        ok_binding = binding == BINDINGS[("off", arm)]
        ref_run, new_run = Run.load(ref), Run.load(new)
        ok, lines = pair_report(ref_run, facts(ref_run, old_identity.get("dll")), new_run,
                                facts(new_run, new_identity.get("dll") if new_identity["ok"] else None),
                                binding, old_identity, new_identity)
        lines.insert(0, f"expected binding from config bytes {binding} equals archived: {ok_binding}")
        write(out, header + lines + [f"EQUIVALENT: {ok and ok_binding}"])
        print("\n".join(header + lines))
        return 0 if ok and ok_binding else 1
    out = argv[1]
    verdicts = ("GATE0-PASSED.txt", "GATE0-FAILED.txt") if argv[0] == "gate0" else ("SMOKE-PASSED.txt", "SMOKE-FAILED.txt")
    if any((HERE / name).exists() for name in verdicts):   # one verdict only; no re-judging after a failure (R17 G0)
        raise SystemExit(f"refused: a {argv[0]} verdict file already exists in {HERE}")
    # Not ready is not a verdict: refuse without writing anything while inputs are not frozen/sealed or a job of the
    # phase has not been started. A started job without summary or with a failing status is judged (and fails).
    problems = s.frozen_input_problems(argv[0], for_comparator=True)
    if argv[0] == "smoke":
        problems += s.verdict_problems(s.GATE0_PASSED, s.GATE0_FAILED, "GATE0", new_identity)
    not_started = [name for name, *_ in s.jobs(argv[0]) if not (s.OUT_ROOT / argv[0] / name).exists()]
    if problems or not_started:
        raise SystemExit(f"refused, not ready (no verdict written): input problems {problems}; "
                         f"jobs not started {not_started}")
    if argv[0] == "gate0":
        lines, all_ok, new_dlls = [], True, set()
        for name, arm, cell, mode, _label in s.jobs("gate0"):
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
            ref_run, new_run = Run.load(ref_dir), Run.load(new_dir)
            want_sha, want_frames = REFERENCE_TRANSCRIPTS[name]
            if ref_run.transcript_sha256 != want_sha or len(ref_run.lines) != want_frames:
                all_ok = False
                lines.append(f"{name}: reference transcript is not the R22-verified artifact")
            new_facts = facts(new_run, new_identity.get("dll") if new_identity["ok"] else None)
            ok, pair_lines = pair_report(ref_run, facts(ref_run, old_identity.get("dll")), new_run, new_facts,
                                         binding, old_identity, new_identity)
            new_dlls.add(new_facts["initManifest"].get("binarySha256"))
            all_ok = all_ok and ok
            lines += pair_lines
            del new_facts, ref_run, new_run
        if len(new_dlls) != 1:
            all_ok = False
            lines.append(f"the four new jobs do not agree on one binarySha256: {sorted(map(str, new_dlls))}")
        verdict = "GATE0 PASSED" if all_ok else "GATE0 FAILED"
        footer = [f"runnerDllSha256 {new_identity['dll']}", f"runnerTreeSha256 {new_identity['tree']}", verdict]
        write(out, header + lines + footer)
        write(HERE / ("GATE0-PASSED.txt" if all_ok else "GATE0-FAILED.txt"), header + lines + footer)
        print("\n".join(header + lines + footer))
        return 0 if all_ok else 1
    lines, all_ok = [], True
    for name, arm, cell, mode, _label in s.jobs("smoke"):
        directory = s.OUT_ROOT / "smoke" / name
        if any(not (directory / f).exists() for f in ("bundle-manifest.json",) + BUNDLE_FILES):
            all_ok = False
            lines.append(f"{name}: run incomplete (no summary/bundle) -> FAIL")
            continue
        ok, smoke_lines = smoke_report(Run.load(directory), arm, new_identity)
        all_ok = all_ok and ok
        lines += smoke_lines
    verdict = "SMOKE PASSED" if all_ok else "SMOKE FAILED"
    footer = [f"runnerDllSha256 {new_identity['dll']}", f"runnerTreeSha256 {new_identity['tree']}", verdict]
    write(out, header + lines + footer)
    write(HERE / ("SMOKE-PASSED.txt" if all_ok else "SMOKE-FAILED.txt"), header + lines + footer)
    print("\n".join(header + lines + footer))
    return 0 if all_ok else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
