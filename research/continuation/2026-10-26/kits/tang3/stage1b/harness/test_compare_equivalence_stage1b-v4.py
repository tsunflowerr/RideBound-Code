"""Self-test v4 for the Stage-1b harness (Codex R38 fixes). Pure, in-memory controls plus read-only archived inputs;
nothing is simulated, built, published or written except the optional log (mode "x").

  python -B test_compare_equivalence_stage1b-v4.py [--target v4|v3] [<out-log>]

--target v4 (default) runs
  A. every control of the v1/v2 self-tests against the v4 comparator. P2n / P3n are NORMALISATION tests with
     validity-limited fixtures (R28 N1): the transcript-verifiable digests (manifest, initial state, decision 1,
     final state, checkpoint) are recomputed by code independent of the comparator, but the digests of decisions
     2..N are not rebuilt, so these fixtures are not protocol-valid tapes. v4 (R38-1): a fixture built from a changed
     transcript also gets its run report receipts refreshed (refresh_report: semantic manifestHash, publicationCount,
     publicationDigest, semanticHash and the summary receipts, with the frozen verifier's formulas), so no positive
     silently keeps inconsistent recomputable metadata.
  B. the R24 finding controls F1-F6, with the behavioural-only Gate-0 verdict names.
  C. the R28 finding controls N1-N8.
  D. the R38 finding controls (R38-1 .. R38-5 and the section-6 tool / launch-environment / analysis-timing items).
--target v3 runs only D against the reviewed v3 files (compare_equivalence_stage1b-v3.py, run_stage1b-v3.py,
STAGE1B-PLAN-DRAFT-v3.md): every R38 counterexample is expected to show BAD there (the reproduction log); positives
are expected OK.
Lines: OK = expected result; BAD = unexpected; LIMIT = a disclosed limit: (1) a consistently rewritten decision 2..N
digest is not detected (behavioural-only Gate 0, owner decision 2026-10-12); (2) a well-formed but false
checkpointBindingHash with a consistent semanticHash is not detected (its preimage contains adapter state that a
bundle does not carry, F/ridebound_fleetpy/fleet_control.py:1004-1035; Codex R38-1).
"""
from __future__ import annotations

import base64
import contextlib
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import subprocess
import sys
import time
import types

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent.parent
sys.path.insert(0, str(HERE))
ADAPTER = pathlib.Path(r"E:/Code/RideBound-deadline/simulators/fleetpy-ridebound")
sys.path.insert(0, str(ADAPTER))
import actual_fleetpy_medium_verify as av  # noqa: E402  (frozen harness verifier, read only, imported unchanged)
import canonical_wp4 as cj  # noqa: E402

ARCHIVE = pathlib.Path(r"C:/RideBoundData/research/tier3-t38-v1")
OLD_RUNNER_ROOT = pathlib.Path(r"C:/RideBoundData/research/runner-tier3-v2")
OLD_MANIFEST = TANG3 / "runner-tier3-v2.sha256.tsv"
BASE = "U-d20181116-s10-r1-w08"     # U run with 94 exogenous breaches and 3 late pickups
OTHER = "C-30-d20181116-s10-r1-w08"
FORCED_RUN = "Mplus-30-d20181114-s10-r3-w07"   # archived run whose decisions carry forced levels (N6 calibration)
V2 = TANG3 / "configs" / "wp4-tier3-v2.json"
ON = HERE / "wp4-tier3-v2-costfirst.json"
ZERO = "0" * 64
SOURCES: dict[str, pathlib.Path] = {}      # file name -> path override (development only); default HERE / name
LOG: list[str] = []
FAILURES: list[str] = []
LIMITS: list[str] = []
COUNTS = {"OK": 0, "BAD": 0, "LIMIT": 0}


def source(name: str) -> pathlib.Path:
    return SOURCES.get(name, HERE / name)


def say(text: str) -> None:
    LOG.append(text)
    print(text, flush=True)


def check(name: str, condition: bool, detail: str = "") -> None:
    say(f"{'OK ' if condition else 'BAD'} {name}" + (f" -- {detail}" if detail else ""))
    COUNTS["OK" if condition else "BAD"] += 1
    if not condition:
        FAILURES.append(name)


def limit(name: str, detail: str) -> None:
    say(f"LIMIT {name} -- {detail}")
    COUNTS["LIMIT"] += 1
    LIMITS.append(name)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def load(name: str, filename: str):
    if name in sys.modules:
        return sys.modules[name]
    spec = importlib.util.spec_from_file_location(name, source(filename))
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


@contextlib.contextmanager
def patched(obj, **attrs):
    missing = object()
    saved = {key: getattr(obj, key, missing) for key in attrs}
    for key, value in attrs.items():
        setattr(obj, key, value)
    try:
        yield
    finally:
        for key, value in saved.items():
            if value is missing:
                delattr(obj, key)
            else:
                setattr(obj, key, value)


@contextlib.contextmanager
def environ(**values):
    saved = {key: os.environ.get(key) for key in values}
    for key, value in values.items():
        if value is None:
            os.environ.pop(key, None)
        else:
            os.environ[key] = value
    try:
        yield
    finally:
        for key, value in saved.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value


# ---------------------------------------------------------------- in-memory file system for driver/comparator tests

class FakeFile:
    def __init__(self, name: str, text: str | None = None, present=None, data: bytes | None = None):
        self.name, self.text, self.present, self.data = name, text, present, data

    def exists(self) -> bool:
        if self.present is not None:
            return self.present() if callable(self.present) else bool(self.present)
        return self.text is not None or self.data is not None

    def is_dir(self) -> bool:
        return False

    def mkdir(self, parents: bool = False, exist_ok: bool = False) -> None:
        pass

    def read_text(self, encoding: str = "utf-8") -> str:
        if self.text is None:
            raise FileNotFoundError(self.name)
        return self.text

    def read_bytes(self) -> bytes:
        if self.data is None:
            raise FileNotFoundError(self.name)
        return self.data

    def __truediv__(self, other):
        return FakeFile(f"{self.name}/{other}")

    def __str__(self) -> str:
        return f"<fake {self.name}>"


class FakeDir:
    def __init__(self, name: str, present: bool = True):
        self.name, self.present, self.children = name, present, {}
        self.run_files = None

    def exists(self) -> bool:
        return self.present

    def is_dir(self) -> bool:
        return self.present

    def __truediv__(self, other):
        other = str(other)
        if other not in self.children:
            self.children[other] = FakeFile(other)
        return self.children[other]

    def dir(self, name: str) -> "FakeDir":
        if not isinstance(self.children.get(name), FakeDir):
            self.children[name] = FakeDir(name)
        return self.children[name]

    def mkdir(self, parents: bool = False, exist_ok: bool = False) -> None:
        pass

    def __str__(self) -> str:
        return f"<fakedir {self.name}>"


class Writer:
    """In-memory replacement of the comparator's write(path, lines) with mode "x" semantics."""

    def __init__(self, here: FakeDir):
        self.here, self.order = here, []

    def __call__(self, path, lines):
        if isinstance(path, (str, pathlib.PurePath)):
            text_path = pathlib.Path(path)
            name = text_path.name if text_path.parent.resolve() == HERE else str(path)
            target = self.here / name
        else:
            target = path
        if target.exists():
            raise FileExistsError(target.name)
        target.text = "\n".join(lines) + "\n"
        self.order.append(target.name)


# ---------------------------------------------------------------- transcript helpers

def read_files(directory: pathlib.Path) -> dict[str, bytes]:
    return {name: (directory / name).read_bytes()
            for name in ("bundle-manifest.json", "run-00.json", "summary.json", "transcript-00.ndjson")}


def split_lines(files: dict[str, bytes]) -> list[str]:
    lines = files["transcript-00.ndjson"].decode("utf-8").split("\n")
    assert lines[-1] == ""
    return lines[:-1]


def rebuild_bundle(files: dict[str, bytes]) -> dict[str, bytes]:
    out = dict(files)
    bundle = json.loads(files["bundle-manifest.json"])
    for item in bundle["files"]:
        item["lengthBytes"], item["sha256"] = len(out[item["path"]]), sha(out[item["path"]])
    out["bundle-manifest.json"] = json.dumps(bundle, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return out


def refresh_report(files: dict[str, bytes]) -> dict[str, bytes]:
    """R38-1: rebuild the run report receipts that a transcript change invalidates, with the frozen verifier's formulas
    (actual_fleetpy_medium_verify.py:116-119,1096-1106,1206-1210) and independently of the comparator: semantic
    manifestHash (the initialized frame), publicationCount / publicationDigest (promisePublished actions in transcript
    order), run-00 semanticHash and the summary receipts. An undecodable transcript keeps its report unchanged."""
    try:
        manifest_hash, publications = None, []
        for line in split_lines(files):
            direction, envelope = decode(line)
            if direction != "runnerToAdapter":
                continue
            if envelope.get("messageType") == "initialized":
                manifest_hash = envelope["payload"]["manifestHash"]
            elif envelope.get("messageType") == "decision":
                for action in envelope["payload"].get("actions", []):
                    if action.get("decisionType") == "promisePublished":
                        payload = action.get("payload", {})
                        publications.append({"requestId": payload.get("promise", {}).get("requestId"),
                                             "publicationId": payload.get("publicationId"),
                                             "promiseVersion": payload.get("promiseVersion"),
                                             "reasonCode": payload.get("reasonCode"),
                                             "sourceEventSeq": payload.get("sourceEventSeq")})
        run0, summary = json.loads(files["run-00.json"]), json.loads(files["summary.json"])
        semantic = run0["semantic"]
        wanted = {"manifestHash": manifest_hash, "publicationCount": len(publications),
                  "publicationDigest": hashlib.sha256(av._canonical(publications)).hexdigest()}
        if all(semantic.get(key) == value for key, value in wanted.items()):
            return files
        semantic.update(wanted)
        run0["semanticHash"] = av._semantic_hash(semantic)
        summary["semanticHash"] = run0["semanticHash"]
        for item in summary.get("runs") or []:
            item["semanticHash"] = run0["semanticHash"]
    except Exception:
        return files
    out = dict(files)
    out["run-00.json"] = json.dumps(run0, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    out["summary.json"] = json.dumps(summary, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return out


def with_lines(files: dict[str, bytes], lines: list[str], rebuild: bool = True,
               summary: dict | None = None, report: bool = True) -> dict[str, bytes]:
    out = dict(files)
    out["transcript-00.ndjson"] = ("\n".join(lines) + "\n").encode("utf-8")
    if summary is not None:
        out["summary.json"] = json.dumps(summary, sort_keys=True, separators=(",", ":")).encode("utf-8")
    if not rebuild:
        return out
    return rebuild_bundle(refresh_report(out) if report else out)     # v4: R38-1 receipts refreshed


def decode(line: str):
    record = json.loads(line)
    return record["direction"], json.loads(base64.b64decode(record["frameBase64"]))


def canon(value) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def encode(line: str, envelope, fix_integrity: bool = True) -> str:
    return encode_raw(line, canon(envelope), fix_integrity)


def encode_raw(line: str, raw: bytes, fix_integrity: bool = True) -> str:
    record = json.loads(line)
    record["frameBase64"] = base64.b64encode(raw).decode("ascii")
    if fix_integrity:
        record["frameSha256"], record["frameLengthBytes"] = sha(raw), len(raw)
    return json.dumps(record, sort_keys=True, separators=(",", ":"))


def raw_frame(line: str) -> bytes:
    return base64.b64decode(json.loads(line)["frameBase64"])


def edit(lines: list[str], index: int, change, fix_integrity: bool = True) -> list[str]:
    _, envelope = decode(lines[index])
    change(envelope)
    out = list(lines)
    out[index] = encode(lines[index], envelope, fix_integrity)
    return out


def find(lines: list[str], predicate, start: int = 0) -> int:
    for index in range(start, len(lines)):
        direction, envelope = decode(lines[index])
        if predicate(direction, envelope):
            return index
    raise LookupError("no frame matches")


def is_kind(direction: str, kind: str):
    return lambda d, e: d == direction and e.get("messageType") == kind


def tree_lines(tree: dict[str, bytes]) -> list[str]:
    return [f"{path}\t{sha(data)}\t{len(data)}" for path, data in sorted(tree.items())]


class Transcript:
    """Mutable decoded copy of a transcript; only touched frames are re-encoded (fresh transport receipts)."""

    def __init__(self, lines: list[str]):
        self.lines, self.cache = list(lines), {}

    def env(self, index: int):
        if index not in self.cache:
            self.cache[index] = decode(self.lines[index])[1]
        return self.cache[index]

    def out(self) -> list[str]:
        result = list(self.lines)
        for index, envelope in self.cache.items():
            result[index] = encode(self.lines[index], envelope)
        return result


# ---- fixture digests, implemented independently of the comparator (adapter verifier helpers + this file)

def fx_state_hash(state) -> str:
    return av._framed_hash(b"RideBound.OnlineStateHash.v1\0", "canonicalOnlineState", av._canonical(state))


def fx_initial(init) -> dict:
    return {"runId": init["runId"], "scenarioId": init["scenarioId"], "appliedEpoch": 0, "simulationTimeMs": 0,
            "nextEventSeq": 1, "expectedInitialTravelTimeSnapshotHash":
                init["payload"]["manifest"]["travelTimeSnapshotHash"],
            "requests": [], "vehicles": [], "commitmentLedger": [], "incidentLedger": {"incidents": [], "breaches": []}}


def fx_decision_hash(previous: str, manifest_hash: str, policy_version: str, batch, h0: str, s0, payload) -> str:
    projection = {k: v for k, v in payload.items() if k not in ("previousDecisionHash", "decisionHash")}
    material = {"eventBatch": batch, "stateIdentityBefore": {"epochId": 0, "nextEventSeq": 1, "simTimeMs": 0,
                                                             "stateHash": h0}, "onlineStateBefore": s0}
    digest = hashlib.sha256(b"RideBound.DecisionHash.v1\0")
    for tag, value in (("previousDecisionHash", bytes.fromhex(previous)), ("manifestHash", bytes.fromhex(manifest_hash)),
                       ("policyVersion", policy_version.encode("utf-8")),
                       ("canonicalInputState", av._canonical(material)),
                       ("canonicalDecision", av._canonical(projection))):
        tag_bytes = tag.encode("utf-8")
        digest.update(len(tag_bytes).to_bytes(2, "big") + tag_bytes + len(value).to_bytes(8, "big") + value)
    return digest.hexdigest()


class Layout:
    """Frame positions of the base transcript (identical in every fixture)."""

    def __init__(self, lines):
        self.init, self.initialized = 2, 3
        self.first_decision = find(lines, is_kind("runnerToAdapter", "decision"))
        self.first_batch = self.first_decision - 1
        self.first_ack = self.first_decision + 1
        self.second_decision = find(lines, is_kind("runnerToAdapter", "decision"), self.first_decision + 1)
        self.middle_decision = find(lines, is_kind("runnerToAdapter", "decision"), len(lines) // 2)
        self.checkpoint = find(lines, is_kind("runnerToAdapter", "checkpoint"), len(lines) - 6)
        self.last_decision = max(i for i in range(self.checkpoint - 6, self.checkpoint)
                                 if decode(lines[i])[1].get("messageType") == "decision"
                                 and decode(lines[i])[0] == "runnerToAdapter")


def rebuild(t: Transcript, lay: Layout, manifest_override=None, initial_override=None, final_override=None):
    """Recompute the transcript-verifiable digests (manifest, initial state, decision 1 and its links, final state,
    checkpoint). VALIDITY-LIMITED (R28 N1): the digests of decisions 2..N are not rebuilt, so the result is a
    normalisation fixture, not a protocol-valid tape."""
    init, initialized = t.env(lay.init), t.env(lay.initialized)
    manifest_hash = manifest_override or av._manifest_hash(init["payload"]["manifest"])
    initialized["payload"]["manifestHash"] = manifest_hash
    s0 = fx_initial(init)
    h0 = initial_override or fx_state_hash(s0)
    initialized["payload"]["initialStateIdentity"]["stateHash"] = h0
    first = t.env(lay.first_decision)["payload"]
    first["stateBeforeHash"] = h0
    if first["certificate"].get("body"):
        first["certificate"]["body"]["inputStateHash"] = h0
    content = t.env(lay.checkpoint)["payload"]["content"]
    content["manifestHash"] = manifest_hash
    final_hash = final_override or fx_state_hash(content["onlineState"])
    last = t.env(lay.last_decision)["payload"]
    last["stateAfterHash"] = final_hash
    if last["certificate"].get("body"):
        last["certificate"]["body"]["proposedStateHash"] = final_hash
    content["stateHash"] = final_hash
    d1 = fx_decision_hash(ZERO, manifest_hash, init["payload"]["manifest"]["policyVersion"],
                          t.env(lay.first_batch), h0, s0, first)
    first["decisionHash"] = d1
    t.env(lay.first_ack)["payload"]["decisionHash"] = d1
    t.env(lay.second_decision)["payload"]["previousDecisionHash"] = d1
    t.env(lay.checkpoint)["payload"]["checkpointHash"] = av._checkpoint_hash(content)
    return t


# ---------------------------------------------------------------- target modules

def targets(version: str):
    if version == "v2":
        s = load("run_stage1b_v2", "run_stage1b-v2.py")
        ce = sys.modules["compare_equivalence_stage1b_v2"]
        ce.DRIVER = s
        return types.SimpleNamespace(
            version=version, ce=ce, s=s, plan=source("STAGE1B-PLAN-DRAFT-v2.md"), gate_word="GATE0",
            gate_pass="GATE0-PASSED.txt", gate_fail="GATE0-FAILED.txt",
            files=("compare_equivalence_stage1b-v2.py", "run_stage1b-v2.py", "STAGE1B-PLAN-DRAFT-v2.md",
                   "test_compare_equivalence_stage1b-v2.py"))
    if version == "v3":
        s = load("run_stage1b_v3", "run_stage1b-v3.py")
        ce = sys.modules["compare_equivalence_stage1b_v3"]
        ce.DRIVER = s
        return types.SimpleNamespace(
            version=version, ce=ce, s=s, plan=source("STAGE1B-PLAN-DRAFT-v3.md"), gate_word=ce.GATE0_WORD,
            gate_pass=ce.GATE0_PASS_FILE, gate_fail=ce.GATE0_FAIL_FILE,
            files=("compare_equivalence_stage1b-v3.py", "run_stage1b-v3.py", "STAGE1B-PLAN-DRAFT-v3.md"))
    s = load("run_stage1b_v4", "run_stage1b-v4.py")
    ce = sys.modules["compare_equivalence_stage1b_v4"]
    ce.DRIVER = s
    return types.SimpleNamespace(
        version=version, ce=ce, s=s, plan=source("STAGE1B-PLAN-DRAFT-v4.md"), gate_word=ce.GATE0_WORD,
        gate_pass=ce.GATE0_PASS_FILE, gate_fail=ce.GATE0_FAIL_FILE,
        files=("compare_equivalence_stage1b-v4.py", "run_stage1b-v4.py", "STAGE1B-PLAN-DRAFT-v4.md",
               "RUNNER-PROVENANCE-TEMPLATE-v4.md"))


class Ctx:
    """Shared archived inputs (read once). R28 N7: a failed LIVE verification of the archived runner tree is
    reported (BAD) before any manifest-only identity is used for the unrelated synthetic controls."""

    def __init__(self, T, report=None):
        report = check if report is None else report
        self.T = T
        ce = T.ce
        new_tree = {"RideBound.Runner.dll": b"stage1b synthetic v3 Runner dll", "RideBound.Algorithms.dll": b"alg v3"}
        self.new_lines_tree = tree_lines(new_tree)
        self.new_manifest = ce.tree_text(self.new_lines_tree)
        self.new_id = ce.runner_identity(self.new_manifest, self.new_lines_tree, "synthetic-v3")
        self.new_dll = sha(new_tree["RideBound.Runner.dll"])
        tampered = tree_lines(dict(new_tree, **{"RideBound.Algorithms.dll": b"changed after the manifest"}))
        self.tampered_id = ce.runner_identity(self.new_manifest, tampered, "synthetic-v3-tampered")
        self.other_id = ce.runner_identity(ce.tree_text(tree_lines({"RideBound.Runner.dll": b"another dll"})),
                                           tree_lines({"RideBound.Runner.dll": b"another dll"}), "synthetic-other")
        self.live_old_id = ce.runner_identity_from_disk(OLD_RUNNER_ROOT, OLD_MANIFEST, "runner-tier3-v2")
        self.old_id = self.live_old_id
        if not self.live_old_id["ok"]:
            report("R1-live the archived runner-tier3-v2 tree verifies live against its manifest (a manifest-only "
                   "identity is used below only for the synthetic controls)", False, str(self.live_old_id["problems"]))
            text = OLD_MANIFEST.read_text(encoding="utf-8")
            self.old_id = ce.runner_identity(text, text.split("\n")[1:-1], "runner-tier3-v2 (manifest only)")
        self.base_files = read_files(ARCHIVE / BASE)
        self.lines = split_lines(self.base_files)
        self.lay = Layout(self.lines)
        self.base_run = ce.Run(BASE, self.base_files)
        self.base_facts = ce.facts(self.base_run, self.old_id["dll"])
        self.u_binding = ce.BINDINGS[("off", "U")]

    def judge(self, name: str, expected: bool, files, identity=None, binding=None, must_mention=None,
              digest_clean: bool = False) -> bool:
        ce = self.T.ce
        identity = self.old_id if identity is None else identity
        malformed = getattr(ce, "MalformedRun", None)
        try:
            run = ce.Run(name, files)
            run_facts = ce.facts(run, identity.get("dll") if identity.get("ok") else None)
            ok, report = ce.pair_report(self.base_run, self.base_facts, run, run_facts,
                                        binding or self.u_binding, self.old_id, identity)
        except BaseException as error:  # noqa: B902 - an escaping exception is itself a result here
            if malformed is not None and isinstance(error, malformed):
                ok, report = False, [f"  PRECONDITION FAIL malformed run bundle: {error}"]
            else:
                check(f"{name} [{'PASS' if expected else 'FAIL'} expected]", False,
                      f"exception escaped: {type(error).__name__}: {str(error)[:120]}")
                return False
        reasons = [line.strip() for line in report if "FAIL" in line or "DIFFERENT" in line]
        text = "\n".join(report)
        good = ok == expected
        extra = ""
        if good and must_mention and must_mention not in text:
            good, extra = False, f" (failure does not mention {must_mention!r})"
        if good and digest_clean and any("digest" in r for r in reasons):
            good, extra = False, " (failure masked by a digest problem)"
        check(f"{name} [{'PASS' if expected else 'FAIL'} expected] -> {'PASS' if ok else 'FAIL'}{extra}", good,
              "; ".join(reasons[:3]) if reasons else "all components identical")
        return ok


# ---------------------------------------------------------------- fixtures

def fx_p2v(c: Ctx) -> Transcript:
    t = Transcript(c.lines)
    t.env(c.lay.init)["payload"]["manifest"]["binarySha256"] = c.new_dll
    return rebuild(t, c.lay)


def fx_p3(c: Ctx) -> list[str]:
    """The v1 P3 construction, unchanged (test_compare_equivalence_stage1b.py:255-298)."""
    lines = c.lines
    _, final_envelope = decode(lines[c.lay.checkpoint])
    state = final_envelope["payload"]["content"]["onlineState"]
    mapping = {}
    for history in state["commitmentLedger"]:
        for entry in history["entries"]:
            mapping[entry["publicationId"]] = "promise-" + sha(b"resalt" + entry["publicationId"].encode())
    for record in state["incidentLedger"]["breaches"]:
        prefix = record["breachId"].split("-", 1)[0]
        mapping[record["breachId"]] = prefix + "-" + sha(b"resalt" + record["breachId"].encode())
    _, init_envelope = decode(lines[c.lay.init])
    mapping[init_envelope["runId"]] = "wp7-medium-resalted-label-s1b"
    derived = c.T.ce.DERIVED_HASH_KEYS

    def collect(value):
        if isinstance(value, dict):
            for key, item in value.items():
                if key in derived and item != ZERO:
                    mapping.setdefault(item, sha(b"derived" + item.encode()))
                collect(item)
        elif isinstance(value, list):
            for item in value:
                collect(item)

    def remap(value):
        if isinstance(value, dict):
            out = {key: remap(item) for key, item in value.items()}
            if isinstance(out.get("actions"), list):
                out["actions"] = list(reversed(out["actions"]))
            return out
        if isinstance(value, list):
            return [remap(item) for item in value]
        return mapping.get(value, value) if isinstance(value, str) else value

    resalted = []
    for line in lines:
        collect(decode(line)[1])
    for index, line in enumerate(lines):
        _, envelope = decode(line)
        envelope = remap(envelope)
        if envelope.get("messageType") == "decision":
            envelope["payload"]["certificate"]["body"]["publicationIds"].reverse()
        if index == c.lay.init:
            envelope["payload"]["manifest"]["binarySha256"] = c.new_dll
        resalted.append(encode(line, envelope))
    return resalted


def set_manifest(key, value):
    def change(envelope):
        envelope["payload"]["manifest"][key] = value
    return change


# ---------------------------------------------------------------- section A: v1/v2 controls on the v4 comparator

def section_a(c: Ctx) -> None:
    ce = c.T.ce
    v2, on = V2.read_bytes(), ON.read_bytes()
    check("C1 v2 file SHA-256 66e9b592...", sha(v2) == "66e9b59249d9491d520f3ed592e8c878a0d3b100e39432d5f4b73321617b4518")
    check("C1 v2 canonical fb2813c4... (archived)", cj.canonical_sha256(v2) ==
          "fb2813c4036d309490e12df77e37de1683b92b92a9ebdd71e16cad35d50ecd42")
    check("C1 cost-first canonical 16dcb7e3... (Codex R22:236)", cj.canonical_sha256(on) ==
          "16dcb7e3337763aab7889f7528365dced2aa49028db6dbf390f8b3dce79b3140", cj.canonical_sha256(on))
    stripped = cj.parse_strict(on)
    check("C1 cost-first differs from v2 in exactly one root key",
          sorted(set(stripped) ^ set(cj.parse_strict(v2))) == ["selectionObjectiveOrder"]
          and stripped["selectionObjectiveOrder"] == "cost-first")
    del stripped["selectionObjectiveOrder"]
    check("C1 removing the key restores canonical v2 bytes", cj.canonical_bytes_of_value(stripped) == cj.canonical_bytes(v2))
    for raw in (v2, on):
        check("C1 C# mirror == adapter json.dumps canonicalizer", cj.canonical_bytes(raw) == cj.adapter_canonical_bytes(raw))
    for arm in ("U", "C-30"):
        commitment = (TANG3 / "configs" / f"{arm}.json").read_bytes()
        check(f"C1 OFF binding {arm} == archived", cj.binding(commitment, v2) == ce.BINDINGS[("off", arm)])
        check(f"C1 ON binding {arm} == Codex R22:237-238", cj.binding(commitment, on) == ce.BINDINGS[("on", arm)])
    explicit_sf = v2.replace(b'  "lateBoarding": "record-v1",\n',
                             b'  "lateBoarding": "record-v1",\n  "selectionObjectiveOrder": "stability-first",\n')
    sf_binding = cj.binding((TANG3 / "configs" / "U.json").read_bytes(), explicit_sf)
    check("C1 explicit stability-first changes the binding (R22:50,232)", sf_binding != c.u_binding)
    for label, bad in (("null", b'{"a":null}'), ("float", b'{"a":1.0}'), ("exponent", b'{"a":1e3}'),
                       ("minus zero", b'{"a":-0}'), ("duplicate", b'{"a":1,"a":2}'),
                       ("out of range", b'{"a":9007199254740992}'), ("lone surrogate", b'{"a":"\\ud800"}')):
        try:
            cj.canonical_bytes(bad)
            check(f"C2 canonicalizer rejects {label}", False)
        except cj.CanonicalJsonError:
            check(f"C2 canonicalizer rejects {label}", True)
    check("C2 canonical escapes and key order", cj.canonical_bytes(b'{"b":"\\u0001\\n\\"","a":[true,false,-3]}') ==
          b'{"a":[true,false,-3],"b":"\\u0001\\n\\""}')
    check("C3 synthetic runner verifies and reports its DLL", c.new_id["ok"] and c.new_id["dll"] == c.new_dll)
    check("C3 tree differing from its manifest is refused", not c.tampered_id["ok"])
    check("C3 missing manifest is refused", not ce.runner_identity(None, c.new_lines_tree, "x")["ok"])
    no_dll = tree_lines({"other.dll": b"x"})
    check("C3 tree without RideBound.Runner.dll is refused", not ce.runner_identity(ce.tree_text(no_dll), no_dll, "x")["ok"])
    check("R1 archived runner-tier3-v2 tree verifies LIVE against its manifest; DLL a631038b...",
          c.live_old_id["ok"] and c.live_old_id["dll"] == ce.OLD_RUNNER_DLL and c.live_old_id["tree"] == ce.OLD_RUNNER_TREE,
          f"tree {c.live_old_id['tree']} files {c.live_old_id['files']} {c.live_old_id['problems']}")
    for name, (want_sha, want_frames) in ce.REFERENCE_TRANSCRIPTS.items():
        data = (ARCHIVE / name / "transcript-00.ndjson").read_bytes()
        check(f"R2 {name} transcript SHA-256 and {want_frames} frames (R22:162-167)",
              sha(data) == want_sha and data.count(b"\n") == want_frames)
        del data
    for name in ce.REFERENCE_TRANSCRIPTS:
        run = ce.Run.load(ARCHIVE / name)
        run_facts = ce.facts(run, c.old_id["dll"])
        check(f"R3 {name}: integrity, strict canonical decoding, digests, links, audit, readers",
              not run_facts["problems"] and run_facts["table"] is not None, "; ".join(run_facts["problems"][:3]) or
              f"digests content-verified {run_facts['digestCoverage']}")
        del run, run_facts
    check("P0 base facts have no precondition problem", not c.base_facts["problems"], str(c.base_facts["problems"][:3]))
    lines, lay = c.lines, c.lay
    decision_index = lay.middle_decision
    applied_index = find(lines, is_kind("adapterToRunner", "decisionApplied"), decision_index)
    multi_index = find(lines, lambda d, e: d == "runnerToAdapter" and e.get("messageType") == "decision"
                       and len(e["payload"]["certificate"]["body"].get("publicationIds", [])) >= 2)
    route_index = find(lines, lambda d, e: d == "runnerToAdapter" and e.get("messageType") == "decision" and any(
        a["decisionType"] == "vehiclePlanUpdated" and len(a["payload"]["route"]["mutableSuffix"]) >= 2
        for a in e["payload"]["actions"]))
    alight_index = find(lines, lambda d, e: d == "adapterToRunner" and e.get("messageType") == "eventBatch" and any(
        ev["eventType"] == "passengerAlighted" for ev in e["payload"]["events"]))
    snapshot_index = find(lines, lambda d, e: d == "adapterToRunner" and e.get("messageType") == "eventBatch" and any(
        ev["eventType"] == "travelTimesUpdated" for ev in e["payload"]["events"]), 10)
    say(f"   frames used: decision {decision_index + 1}, ack {applied_index + 1}, multi-publication {multi_index + 1}, "
        f"route {route_index + 1}, alight {alight_index + 1}, snapshot {snapshot_index + 1}, checkpoint "
        f"{lay.checkpoint + 1}, first decision {lay.first_decision + 1}, last decision {lay.last_decision + 1}")
    files = c.base_files
    c.judge("P1 identical copy", True, with_lines(files, lines))
    c.judge("P2n NORMALISATION test (validity-limited: decisions 2..N digests not rebuilt): only binarySha256 "
            "differs, verified new DLL", True, with_lines(files, fx_p2v(c).out()), c.new_id)
    p3 = fx_p3(c)
    c.judge("P3n NORMALISATION test (validity-limited): consistent re-salt of runId, publication/breach IDs, derived "
            "hashes and orders + verified new DLL", True, with_lines(files, rebuild(Transcript(p3), lay).out()), c.new_id)
    del p3

    def permute(envelope):
        body = envelope["payload"]["certificate"]["body"]
        body["publicationIds"] = list(reversed(body["publicationIds"]))
        envelope["payload"]["actions"] = list(reversed(envelope["payload"]["actions"]))
    c.judge("P4 publicationIds and actions order permuted only", True, with_lines(files, edit(lines, multi_index, permute)))

    c.judge("N1 binarySha256 of an unverified DLL", False,
            with_lines(files, edit(lines, lay.init, set_manifest("binarySha256", c.new_dll))), c.other_id)
    c.judge("N1r binarySha256 of an unverified DLL (digests rebuilt)", False, with_lines(files, fx_p2v(c).out()),
            c.other_id, must_mention="binarySha256", digest_clean=True)
    c.judge("N2 new runner tree differs from its manifest", False,
            with_lines(files, edit(lines, lay.init, set_manifest("binarySha256", c.new_dll))), c.tampered_id)
    c.judge("N2r new runner tree differs from its manifest (digests rebuilt)", False,
            with_lines(files, fx_p2v(c).out()), c.tampered_id, must_mention="differs from its manifest",
            digest_clean=True)

    def init_rebuilt(key, value):
        t = Transcript(lines)
        t.env(lay.init)["payload"]["manifest"][key] = value
        return with_lines(files, rebuild(t, lay).out())
    c.judge("N3 policyConfigurationHash of the cost-first config", False,
            with_lines(files, edit(lines, lay.init, set_manifest("policyConfigurationHash", ce.BINDINGS[("on", "U")]))))
    c.judge("N3r policyConfigurationHash of the cost-first config (digests rebuilt)", False,
            init_rebuilt("policyConfigurationHash", ce.BINDINGS[("on", "U")]), must_mention="policyConfigurationHash",
            digest_clean=True)
    c.judge("N3b policyConfigurationHash of v2 + explicit stability-first", False,
            with_lines(files, edit(lines, lay.init, set_manifest("policyConfigurationHash", sf_binding))))
    c.judge("N3br policyConfigurationHash of v2 + explicit stability-first (digests rebuilt)", False,
            init_rebuilt("policyConfigurationHash", sf_binding), must_mention="policyConfigurationHash",
            digest_clean=True)
    for key in ("scenarioContentHash", "graphSnapshotHash", "travelTimeSnapshotHash"):
        c.judge(f"N4-6 {key} changed", False,
                with_lines(files, edit(lines, lay.init, set_manifest(key, sha(key.encode())))))
        c.judge(f"N4-6r {key} changed (digests rebuilt)", False, init_rebuilt(key, sha(key.encode())),
                must_mention=key)

    def snapshot(envelope):
        event = next(ev for ev in envelope["payload"]["events"] if ev["eventType"] == "travelTimesUpdated")
        event["payload"]["snapshot"]["snapshotHash"] = sha(b"other snapshot")
    c.judge("N7 one event snapshotHash changed", False, with_lines(files, edit(lines, snapshot_index, snapshot)))
    c.judge("N8 coreCommitSha changed", False,
            with_lines(files, edit(lines, lay.init, set_manifest("coreCommitSha", "f" * 40))))
    c.judge("N8r coreCommitSha changed (digests rebuilt)", False, init_rebuilt("coreCommitSha", "f" * 40),
            must_mention="coreCommitSha", digest_clean=True)

    def later(envelope):
        envelope["simTimeMs"] += 1
    c.judge("N9 one decision time +1 ms", False, with_lines(files, edit(lines, decision_index, later)))

    def swap(envelope):
        action = next(a for a in envelope["payload"]["actions"] if a["decisionType"] == "vehiclePlanUpdated"
                      and len(a["payload"]["route"]["mutableSuffix"]) >= 2)
        suffix = action["payload"]["route"]["mutableSuffix"]
        suffix[0], suffix[1] = suffix[1], suffix[0]
    c.judge("N10 one route stop order swapped", False, with_lines(files, edit(lines, route_index, swap)))

    def ledger_value(envelope):
        envelope["payload"]["content"]["onlineState"]["commitmentLedger"][0]["entries"][-1]["budgetAfter"][
            "dropEtaTotalMs"] += 1
    c.judge("N11 one final-ledger value +1", False, with_lines(files, edit(lines, lay.checkpoint, ledger_value)))
    t = Transcript(lines)
    ledger_value(t.env(lay.checkpoint))
    c.judge("N11r one final-ledger value +1 (final state and checkpoint digests rebuilt)", False,
            with_lines(files, rebuild(t, lay).out()), must_mention="commitmentLedger", digest_clean=True)
    c.judge("N12 one alight time +1 ms", False, with_lines(files, edit(lines, alight_index, later)))

    def drop_publication(envelope):
        envelope["payload"]["certificate"]["body"]["publicationIds"].pop()
    c.judge("N13 one publication missing from a certificate", False,
            with_lines(files, edit(lines, multi_index, drop_publication)))

    def unknown_publication(envelope):
        envelope["payload"]["content"]["onlineState"]["commitmentLedger"][0]["entries"][0]["publicationId"] = \
            "promise-" + "1" * 64
    c.judge("N14 a ledger publication reference replaced by an unknown ID", False,
            with_lines(files, edit(lines, lay.checkpoint, unknown_publication)))
    t = Transcript(lines)
    unknown_publication(t.env(lay.checkpoint))
    c.judge("N14r unknown ledger publication ID (digests rebuilt)", False, with_lines(files, rebuild(t, lay).out()),
            must_mention="no ledger record", digest_clean=True)

    def broken_ack(envelope):
        envelope["payload"]["decisionHash"] = sha(b"not the decision")
    c.judge("N15 one decisionApplied hash changed", False, with_lines(files, edit(lines, applied_index, broken_ack)))

    def unclassified(envelope):
        envelope["payload"]["candidateSetHash"] = "ab" * 32
    c.judge("N16 an unclassified hash field added", False, with_lines(files, edit(lines, decision_index, unclassified)))

    def reason(envelope):
        envelope["payload"]["reasonCode"] = "TAMPERED"
    c.judge("N17 frame bytes changed, frameSha256 kept", False,
            with_lines(files, edit(lines, decision_index, reason, fix_integrity=False)))
    c.judge("N18 transcript changed, bundle-manifest kept", False,
            with_lines(files, edit(lines, decision_index, later), rebuild=False))
    summary = json.loads(files["summary.json"])
    c.judge("N19 summary status fail", False, with_lines(files, lines, summary=dict(summary, status="fail")))
    other = ce.Run.load(ARCHIVE / OTHER)
    other_facts = ce.facts(other, c.old_id["dll"])
    ok, report = ce.pair_report(c.base_run, c.base_facts, other, other_facts, c.u_binding, c.old_id, c.old_id)
    check("N20 C-30 run of the same cell [FAIL expected] -> " + ("PASS" if ok else "FAIL"), not ok,
          "; ".join(line.strip() for line in report if "FAIL" in line or "DIFFERENT" in line)[:300])
    del other, other_facts


# ---------------------------------------------------------------- section B: R24 finding controls

def f1(c: Ctx) -> None:
    ce, lines, lay, files = c.T.ce, c.lines, c.lay, c.base_files
    for name in ce.REFERENCE_TRANSCRIPTS:   # R4: the formulas reproduce every archived digest (ground truth = Runner v2)
        ref_lines = split_lines(read_files(ARCHIVE / name))
        rl = Layout(ref_lines)
        init, initd = decode(ref_lines[rl.init])[1], decode(ref_lines[rl.initialized])[1]
        batch, dec = decode(ref_lines[rl.first_batch])[1], decode(ref_lines[rl.first_decision])[1]
        ck = decode(ref_lines[rl.checkpoint])[1]
        mh = ce.manifest_hash(init["payload"]["manifest"])
        s0 = ce.initial_online_state(init["runId"], init["scenarioId"],
                                     init["payload"]["manifest"]["travelTimeSnapshotHash"])
        h0 = ce.online_state_hash(s0)
        d1 = ce.decision_hash(ZERO, mh, init["payload"]["manifest"]["policyVersion"],
                              ce.decision_input_bytes(batch, 0, 1, 0, h0, s0), ce.decision_projection_bytes(dec["payload"]))
        content = ck["payload"]["content"]
        results = {
            "manifestHash": mh == initd["payload"]["manifestHash"] == av._manifest_hash(init["payload"]["manifest"]),
            "initial stateHash": h0 == initd["payload"]["initialStateIdentity"]["stateHash"] == fx_state_hash(s0),
            "decision-1 hash": d1 == dec["payload"]["decisionHash"] == fx_decision_hash(
                ZERO, mh, init["payload"]["manifest"]["policyVersion"], batch, h0, s0, dec["payload"]),
            "checkpointHash": ce.checkpoint_hash(content) == ck["payload"]["checkpointHash"]
            == av._checkpoint_hash(content),
            "final stateHash": ce.online_state_hash(content["onlineState"]) == content["stateHash"],
        }
        check(f"F1-R4 {name}: comparator digest formulas reproduce every archived digest and agree with the "
              f"adapter verifier / fixture builder", all(results.values()), str(results))
        if name == BASE:   # F1-R5: the fast canonical writer equals the C# mirror on real protocol values
            samples = {"final onlineState": content["onlineState"], "checkpoint content": content,
                       "decision-1 input": {"eventBatch": batch, "onlineStateBefore": s0},
                       "decision-1 projection": dec["payload"], "manifest": init["payload"]["manifest"]}
            same = {label: ce.canonical_bytes(v) == cj.canonical_bytes_of_value(v) for label, v in samples.items()}
            check("F1-R5 fast canonical writer == canonical_wp4 C# mirror on real states/decisions",
                  all(same.values()), str(same))
        del ref_lines
    fast = {"b": "".join(map(chr, (1, 0x1F, 10, 9, 13, 8, 12, 34, 92, 47, 0x7F, 0x2028, 0xE9, 0x1F600))),
            "a": [True, False, -3, 0, 9_007_199_254_740_991, -9_007_199_254_740_991], "z" + chr(0xE9): {"y": [], "x": {}}}
    check("F1-R5 fast canonical writer == C# mirror on escapes, astral strings and extreme integers (fast path)",
          ce.canonical_bytes(fast) == cj.canonical_bytes_of_value(fast))
    fallback = {chr(0xE000): 1, chr(0x1F600): 2, "a": 3}   # code-point order != UTF-16 ordinal order for these keys
    check("F1-R5 keys at or above U+E000 take the exact C# mirror (ordinal UTF-16 key order)",
          ce.canonical_bytes(fallback) == cj.canonical_bytes_of_value(fallback)
          and ce.canonical_bytes(fallback) != canon(fallback))
    rejected = 0
    for bad in ({"a": None}, {"a": 1.5}, {"a": 9_007_199_254_740_992}, {"a": chr(0xD800)}):
        try:
            ce.canonical_bytes(bad)
        except cj.CanonicalJsonError:
            rejected += 1
    check("F1-R5 fast canonical writer rejects null, float, out-of-range integer and lone surrogate", rejected == 4,
          f"{rejected}/4 rejected")
    c.judge("F1-P2n normalisation test (validity-limited), only the permitted DLL difference", True,
            with_lines(files, fx_p2v(c).out()), c.new_id)
    t = fx_p2v(c)
    t.env(lay.checkpoint)["payload"]["checkpointHash"] = sha(b"incorrect checkpoint digest")
    c.judge("F1-a R24 counterexample: final checkpointHash incorrect, fresh transport receipts", False,
            with_lines(files, t.out()), c.new_id)
    t = fx_p2v(c)
    rewritten = sha(b"rewritten decision 1 digest")
    t.env(lay.first_decision)["payload"]["decisionHash"] = rewritten
    t.env(lay.first_ack)["payload"]["decisionHash"] = rewritten
    t.env(lay.second_decision)["payload"]["previousDecisionHash"] = rewritten
    c.judge("F1-b decision-1 digest consistently rewritten (decision, ACK, chain), links preserved, content digest "
            "wrong", False, with_lines(files, t.out()), c.new_id)
    c.judge("F1-c v1 P2 construction: binarySha256 changed, manifestHash/decision-1 not rebuilt (stale)", False,
            with_lines(files, edit(lines, lay.init, set_manifest("binarySha256", c.new_dll))), c.new_id)
    t = Transcript(lines)
    t.env(lay.init)["payload"]["manifest"]["binarySha256"] = c.new_dll
    c.judge("F1-d manifestHash consistently rewritten (initialized, checkpoint, decision 1, checkpointHash)", False,
            with_lines(files, rebuild(t, lay, manifest_override=sha(b"bogus manifest")).out()), c.new_id)
    t = Transcript(lines)
    t.env(lay.init)["payload"]["manifest"]["binarySha256"] = c.new_dll
    c.judge("F1-e initial stateHash consistently rewritten (initialized, decision 1, certificate, digest)", False,
            with_lines(files, rebuild(t, lay, initial_override=sha(b"bogus initial state")).out()), c.new_id)
    t = Transcript(lines)
    t.env(lay.init)["payload"]["manifest"]["binarySha256"] = c.new_dll
    c.judge("F1-f final stateHash consistently rewritten (last decision, certificate, checkpoint, checkpointHash)",
            False, with_lines(files, rebuild(t, lay, final_override=sha(b"bogus final state")).out()), c.new_id)
    c.judge("F1-g v1 P3 construction: verifiable digests replaced by arbitrary values", False,
            with_lines(files, fx_p3(c)), c.new_id)
    c.judge("F1-P3n normalisation test (validity-limited): consistent re-salt with rebuilt verifiable digests", True,
            with_lines(files, rebuild(Transcript(fx_p3(c)), lay).out()), c.new_id)
    middle = lay.middle_decision
    nxt = find(lines, is_kind("runnerToAdapter", "decision"), middle + 1)
    t = fx_p2v(c)
    bogus = sha(b"rewritten middle decision digest")
    t.env(middle)["payload"]["decisionHash"] = bogus
    t.env(middle + 1)["payload"]["decisionHash"] = bogus
    t.env(nxt)["payload"]["previousDecisionHash"] = bogus
    run = ce.Run("N1-LIMIT", with_lines(files, t.out()))
    ok, _ = ce.pair_report(c.base_run, c.base_facts, run, ce.facts(run, c.new_dll), c.u_binding, c.old_id, c.new_id)
    limit(f"N1-LIMIT decision at frame {middle + 1} consistently rewritten",
          f"comparator {'PASSES' if ok else 'FAILS'} it; accepted by the behavioural-only Gate 0 (owner decision "
          f"2026-10-12): decisions 2..N digests are not validated by the production .NET code (RunnerSession.cs:787-794)")


# ---- F2 / N3 freeze world

class World:
    """A synthetic in-memory freeze world: files, external trees, environment, resolved tools, .pth lines and an
    absent path. Each version gets the freeze text in its own format."""

    def __init__(self, T):
        self.T = T
        self.files = {"X:/s/run_pilot.py": b"pilot", "X:/s/fixture.json": b"fixture", "X:/s/dotnet.exe": b"host"}
        self.tree_data = {"X:/env": {"python.exe": b"py", "Lib/os.py": b"os", "Lib/site-packages/x.py": b"x"}}
        self.tree_ids = {"python-env": "X:/env"}
        if T.version in ("v3", "v4"):
            self.tree_data["X:/tools/git"] = {"cmd/git.exe": b"git launcher", "mingw64/bin/git.exe": b"git"}
            self.tree_ids["git"] = "X:/tools/git"
        self.environ = {"DOTNET_MULTILEVEL_LOOKUP": "0", "PATH": "X:/tools/git/cmd;C:/Windows"}
        self.git_at = "X:/tools/git/cmd/git.exe"
        self.git_driver_at = None                         # v4: None = the driver context resolves like the jobs
        self.taskkill_at = "X:/win/System32/taskkill.exe"
        self.tool_bytes = {self.taskkill_at: b"taskkill"}
        self.omit_taskkill = False
        self.pth = []
        self.absent_path, self.absent_present = "X:/usersite", False
        for tree_id, root in self.tree_ids.items():
            self.files[T.s.external_manifest(tree_id).as_posix()] = T.ce.tree_text(self.body(root)).encode("utf-8")
        self.required = list(self.files)
        self.unreadable = set()

    def body(self, root=None, excludes=()):
        return tree_lines(self.tree_data[pathlib.Path(root).as_posix()])

    def trees(self):
        return tuple((tree_id, pathlib.Path(root), ()) for tree_id, root in self.tree_ids.items())

    def sha(self, path):
        posix = pathlib.Path(path).as_posix()
        if posix in self.unreadable:
            raise OSError(f"permission denied: {posix}")
        if posix in self.tool_bytes:
            return sha(self.tool_bytes[posix])
        for root, tree in self.tree_data.items():
            if posix.startswith(root + "/") and posix[len(root) + 1:] in tree:
                return sha(tree[posix[len(root) + 1:]])
        return sha(self.files[posix])

    def resolve(self, name, context):
        if name == "git":
            return self.git_driver_at if context == "driver" and self.git_driver_at else self.git_at
        return self.taskkill_at if name == "taskkill" else None

    def exists(self, path):
        posix = pathlib.Path(path).as_posix()
        if posix == self.absent_path:
            return self.absent_present
        return posix in self.files or posix in self.tree_data

    def which(self, name, path=None):
        return self.git_at if name == "git" else None

    def text(self, drop=(), duplicate=None, extra=None, malformed=False, header_only=False):
        lines = ["# synthetic freeze"]
        if header_only:
            return "\n".join(lines) + "\n"
        for path in self.required:
            if path not in drop:
                lines.append(f"{sha(self.files[path])}  {path}")
        if duplicate:
            lines.append(f"{sha(self.files[duplicate])}  {duplicate}")
        if extra:
            lines.append(f"{sha(b'extra')}  {extra}")
        if malformed:
            lines.append("garbage-without-separator")
        for tree_id, root in self.tree_ids.items():
            body = self.body(root)
            lines.append(f"tree {tree_id} {sha(chr(10).join(body).encode('utf-8'))} {len(body)} {root}")
        if self.T.version == "v3":
            lines.append(f"tool git {self.git_at}")
        elif self.T.version == "v4":
            lines.append(f"tool git {self.sha(self.git_at)} {self.git_at}")
            if not self.omit_taskkill:
                lines.append(f"tool taskkill {self.sha(self.taskkill_at)} {self.taskkill_at}")
        lines += [f"env {k}={v}" for k, v in sorted(self.T.s.frozen_env(self.environ).items())]
        lines.append(f"absent {self.absent_path}")
        return "\n".join(lines) + "\n"

    def problems(self, text):
        T = self.T
        kwargs = dict(targets=list(self.required), trees=self.trees(), environ=self.environ,
                      absent=(self.absent_path,), sha=self.sha, exists=self.exists,
                      read=lambda p: self.files[pathlib.Path(p).as_posix()].decode(), body=self.body)
        if T.version == "v3":
            kwargs.update(which=self.which, pth=lambda: list(self.pth), site=pathlib.Path("X:/env/Lib/site-packages"))
        elif T.version == "v4":
            kwargs.update(resolve=self.resolve, pth=lambda: list(self.pth), site=pathlib.Path("X:/env/Lib/site-packages"))
        return T.s.freeze_problems(text, **kwargs)


def freeze_case(T, name, expect_problem, make, mention=None):
    world = World(T)
    try:
        text = make(world)
        problems = world.problems(text)
    except BaseException as error:  # noqa: B902
        check(name, False, f"exception escaped: {type(error).__name__}: {str(error)[:100]}")
        return
    good = bool(problems) == expect_problem and (not mention or any(mention in p for p in problems))
    check(name, good, f"problems {problems[:2]}" if problems else "no problem reported")


def f2(c: Ctx) -> None:
    T = c.T
    freeze_case(T, "F2-1 header-only freeze is rejected (R24:72-76)", True, lambda w: w.text(header_only=True))
    freeze_case(T, "F2-2 duplicate file entry is rejected", True, lambda w: w.text(duplicate="X:/s/fixture.json"),
                "duplicate")
    freeze_case(T, "F2-3 a required file missing from the freeze is rejected", True,
                lambda w: w.text(drop=("X:/s/fixture.json",)))
    freeze_case(T, "F2-4 a file outside the required inventory is rejected", True,
                lambda w: (w.files.__setitem__("X:/s/extra.txt", b"extra"), w.text(extra="X:/s/extra.txt"))[1])
    freeze_case(T, "F2-5 a malformed freeze line is a problem, not an exception", True, lambda w: w.text(malformed=True))
    freeze_case(T, "F2-6 a complete, unchanged freeze passes [positive]", False, lambda w: w.text())

    def env_drift(w):
        text = w.text()
        w.tree_data["X:/env"]["Lib/site-packages/x.py"] = b"x upgraded after the freeze"
        return text
    freeze_case(T, "F2-7 a change in the external Python environment after the freeze is detected", True, env_drift)

    def env_var(w):
        text = w.text()
        w.environ["PYTHONPATH"] = "X:/shadow"
        return text
    freeze_case(T, "F2-8 a module-resolution environment variable set after the freeze is detected", True, env_var)

    def absent(w):
        text = w.text()
        w.absent_present = True
        return text
    freeze_case(T, "F2-9 the interpreter's user site-packages appearing after the freeze is detected", True, absent)
    import run_pilot
    required = {pathlib.Path(run_pilot.FLEETPY).as_posix(), pathlib.Path(run_pilot.PYTHON).parent.as_posix(),
                (pathlib.Path(run_pilot.DOTNET).parent / "shared" / "Microsoft.NETCore.App").as_posix(),
                (pathlib.Path(run_pilot.DOTNET).parent / "host" / "fxr").as_posix()}
    covered = {pathlib.Path(root).as_posix() for _, root, _ in T.s.EXTERNAL_TREES} & required
    manifests = {T.s.external_manifest(tree_id).as_posix() for tree_id, _, _ in T.s.EXTERNAL_TREES}
    listed = {pathlib.Path(p).as_posix() for p in T.s.freeze_targets()}
    check("F2-10 the freeze covers FleetPy, the job interpreter's environment, the .NET host and every "
          "Microsoft.NETCore.App (R24:80-86)", (covered if manifests <= listed else set()) == required,
          f"covered {sorted(covered)}")
    import runner_manifest
    for label, root, excludes in (("FleetPy", pathlib.Path(run_pilot.FLEETPY), (".git/",)),
                                  ("NETCore.App", pathlib.Path(run_pilot.DOTNET).parent / "shared" /
                                   "Microsoft.NETCore.App", ())):
        reference = [line for line in runner_manifest.body(root)
                     if not any(line.startswith(prefix) for prefix in excludes)]
        mine = T.s.tree_body(root, excludes)
        check(f"F2-11 read-only: tree_body({label}) equals runner_manifest.body minus {list(excludes)}",
              mine == reference and mine and not any(x.startswith(".git/") for x in mine), f"{len(mine)} files")


# ---- F3 / F5 driver harness

def verdict_text(word: str, identity: dict) -> str:
    return f"runnerDllSha256 {identity['dll']}\nrunnerTreeSha256 {identity['tree']}\n{word} PASSED\n"


def driver_stubs(T, identity, **verdicts):
    """Every input check passes; verdict / inspection / drift files are in-memory (present when given)."""
    s = T.s
    analysis_text = verdicts.get("ANALYSIS_FREEZE", "# freeze\n")
    files = {key: FakeFile(name, verdicts.get(key)) for key, name in
             (("GATE0_PASSED", T.gate_pass), ("GATE0_FAILED", T.gate_fail),
              ("SMOKE_PASSED", "SMOKE-PASSED.txt"), ("SMOKE_FAILED", "SMOKE-FAILED.txt"),
              ("INSPECTION", "SMOKE-INSPECTION.txt"), ("DRIFT_STOP", "INPUT-DRIFT-STOP.txt"))}
    files["ANALYSIS_FREEZE"] = FakeFile("stage1b-analysis-freeze.sha256", analysis_text)
    stubs = dict(runner_problems=lambda: ([], identity), config_problems=lambda: [], harness_problems=lambda: [],
                 cell_problems=lambda cells: [], reference_problems=lambda: [], seal_problems=lambda: [],
                 freeze_problems=lambda *a, **k: [],
                 analysis_freeze_problems=lambda text=None, *a, **k: [] if s.ANALYSIS_FREEZE.exists() else [f"refused: {s.ANALYSIS_FREEZE.name} does not exist"],
                 sha256_file=lambda p: sha((p.text or "").encode("utf-8")))
    return patched(s, **stubs, **files)


def f3_driver(c: Ctx) -> None:
    T, ident = c.T, dict(c.new_id, tree="t" * 64)
    gate0_ok = verdict_text(T.gate_word, ident)
    smoke_ok = verdict_text("SMOKE", ident)
    cases = [
        ("F3-1 run gate0 refused when the Gate-0 failure verdict exists", "gate0", dict(GATE0_FAILED="fail"), True),
        ("F3-2 run smoke refused when the Gate-0 failure verdict exists", "smoke", dict(GATE0_FAILED="fail"), True),
        ("F3-3 run smoke refused when SMOKE-FAILED.txt exists", "smoke",
         dict(GATE0_PASSED=gate0_ok, SMOKE_FAILED="fail"), True),
        ("F3-4 run gate0 refused when SMOKE-FAILED.txt exists", "gate0", dict(SMOKE_FAILED="fail"), True),
        ("F3-5 run main refused when SMOKE-FAILED.txt exists", "main",
         dict(GATE0_PASSED=gate0_ok, SMOKE_FAILED="fail"), True),
        ("F3-6 run gate0 refused after the Gate-0 pass verdict (phase closed)", "gate0", dict(GATE0_PASSED=gate0_ok),
         True),
        ("F3-7 run smoke refused after SMOKE-PASSED.txt (phase closed)", "smoke",
         dict(GATE0_PASSED=gate0_ok, SMOKE_PASSED=smoke_ok), True),
        ("F3-8 run gate0 admitted when no verdict exists [positive]", "gate0", {}, False),
        ("F3-9 run smoke admitted after a valid Gate-0 pass verdict [positive]", "smoke", dict(GATE0_PASSED=gate0_ok),
         False),
        ("F4-1 run main refused without the recorded smoke inspection (R24:114)", "main",
         dict(GATE0_PASSED=gate0_ok, SMOKE_PASSED=smoke_ok), True),
        ("F4-2 run main admitted with a valid inspection record [positive]", "main",
         dict(GATE0_PASSED=gate0_ok, SMOKE_PASSED=smoke_ok,
              INSPECTION=f"smokeVerdictSha256 {sha(smoke_ok.encode())}\ninspected-by: owner\ninspection: accepted\n"),
         False),
    ]
    with environ(**{"RB_ALLOW_BATTERY_MIN_PERCENT": None}):
        for name, phase, verdicts, expect_refusal in cases:
            with driver_stubs(T, ident, **verdicts):
                try:
                    problems = T.s.frozen_input_problems(phase)
                except BaseException as error:  # noqa: B902
                    check(name, False, f"exception escaped: {type(error).__name__}: {error}")
                    continue
            check(name, bool(problems) == expect_refusal, f"problems {problems[:2]}" if problems else "admitted")
        with environ(RB_ALLOW_BATTERY_MIN_PERCENT="50"), driver_stubs(T, ident):
            problems = T.s.frozen_input_problems("gate0")
        check("F5-1 run refused while RB_ALLOW_BATTERY_MIN_PERCENT is set (AC only)", bool(problems),
              f"problems {problems[:1]}" if problems else "admitted")


class FakeClock:
    def __init__(self):
        self.t, self.on_sleep = 1_000_000.0, None

    def time(self):
        return self.t

    def sleep(self, seconds):
        self.t += seconds
        if self.on_sleep:
            self.on_sleep()


@contextlib.contextmanager
def job_world(T, *, power, free, stop=lambda: False, failed=lambda: False, clock=None):
    """run_job / wait_for_power with every side effect in memory: no process, no file, no real sleep."""
    import run_pilot
    clock = clock or FakeClock()
    launches = []

    def fake_run(cmd, **kwargs):
        label = cmd[cmd.index("--label") + 1]
        launches.append(label)
        hook = job_world.on_launch
        if hook:
            hook(label)
        return types.SimpleNamespace(returncode=0, stdout="", stderr="")

    def fake_open(path, mode="r", **kwargs):
        if "x" not in mode and "w" not in mode:
            raise AssertionError(f"unexpected read of {path}")
        return io.StringIO()

    s = T.s
    attrs = dict(state_of=lambda phase, name: "pending", LOG_ROOT=FakeDir("logs", present=False),
                 STOP_FILE=FakeFile("STOP", present=stop), free_mib=free,
                 time=types.SimpleNamespace(time=clock.time, sleep=clock.sleep, strftime=time.strftime,
                                            gmtime=time.gmtime),
                 subprocess=types.SimpleNamespace(run=fake_run, SubprocessError=subprocess.SubprocessError),
                 open=fake_open,
                 GATE0_FAILED=FakeFile(T.gate_fail, present=failed),
                 SMOKE_FAILED=FakeFile("SMOKE-FAILED.txt", present=False),
                 GATE0_PASSED=FakeFile(T.gate_pass, present=False),
                 SMOKE_PASSED=FakeFile("SMOKE-PASSED.txt", present=False),
                 DRIFT_STOP=FakeFile("INPUT-DRIFT-STOP.txt", present=False))
    if hasattr(s, "Admission"):
        attrs["ADMISSION"] = s.Admission("gate0", None)
    job_world.on_launch = None
    with patched(s, **attrs), patched(run_pilot, power_status=power):
        yield types.SimpleNamespace(launches=launches, clock=clock, s=s)


job_world.on_launch = None


def call_world(T, world_kwargs, action):
    try:
        with job_world(T, **world_kwargs) as w:
            return action(w), w
    except BaseException as error:  # noqa: B902
        return error, None


def f5(c: Ctx) -> None:
    T = c.T
    job = T.s.jobs("gate0")[0]
    with environ(RB_ALLOW_BATTERY_MIN_PERCENT="50"):
        result, _ = call_world(T, dict(power=lambda: (0, 100), free=lambda: 60_000), lambda w: w.s.wait_for_power())
    check("F5-2 battery power with RB_ALLOW_BATTERY_MIN_PERCENT=50 does not admit a job (strict AC)", result is False,
          f"wait_for_power returned {result!r}")
    clock = FakeClock()
    stop = {"on": False}
    seq = iter([(0, 100), (1, 100), (1, 100), (1, 100)])
    clock.on_sleep = lambda: stop.__setitem__("on", True)
    result, _ = call_world(T, dict(power=lambda: next(seq), free=lambda: 60_000, stop=lambda: stop["on"], clock=clock),
                           lambda w: w.s.wait_for_power())
    check("F5-3 STOP created during the power wait is honoured when AC returns", result is False,
          f"wait_for_power returned {result!r}")
    clock = FakeClock()
    state = {"waited": False}
    clock.on_sleep = lambda: state.__setitem__("waited", True)
    seq = iter([(0, 100)] + [(1, 100)] * 10)
    floor = T.s.FLOOR_MIB
    result, _ = call_world(T, dict(power=lambda: next(seq), free=lambda: floor + 100 if state["waited"] else 60_000,
                                   clock=clock), lambda w: (T.s.run_job("gate0", *job), list(w.launches)))
    check("F5-4 disk is rechecked after the power wait (stale free space does not launch)",
          isinstance(result, tuple) and result[1] == [], f"result {result}")
    jobs3 = T.s.jobs("gate0")[:3]

    def nested(w):
        order = iter(jobs3[1:])

        def on_launch(label):
            following = next(order, None)
            if following is not None:
                T.s.run_job("gate0", *following)
        job_world.on_launch = on_launch
        T.s.run_job("gate0", *jobs3[0])
        return list(w.launches)
    result, _ = call_world(T, dict(power=lambda: (1, 100), free=lambda: floor + 900), nested)
    check("F5-5 overlapping starts reserve 410 MiB each (free = floor + 900 MiB admits 2 of 3)",
          isinstance(result, list) and len(result) == 2, f"launched {result}")
    import run_pilot
    admission = T.s.Admission("gate0", None)
    with patched(T.s, free_mib=lambda: floor + 900, STOP_FILE=FakeFile("STOP", present=False),
                 GATE0_FAILED=FakeFile("x", present=False), SMOKE_FAILED=FakeFile("y", present=False),
                 GATE0_PASSED=FakeFile("z", present=False), DRIFT_STOP=FakeFile("d", present=False)), \
            patched(run_pilot, power_status=lambda: (1, 100)):
        first, second, third = admission.admit("a"), admission.admit("b"), admission.admit("c")
        admission.release()
        fourth = admission.admit("d")
    check("F5-6 a finished job releases its reservation", (first, second, fourth) == (None, None, None)
          and str(third).startswith("deferred-disk"), f"{first} {second} {third} {fourth}")
    result, _ = call_world(T, dict(power=lambda: (1, 100), free=lambda: 60_000, failed=lambda: True),
                           lambda w: (T.s.run_job("gate0", *job), list(w.launches)))
    check("F3-10 a job is not launched once a failure verdict exists (checked right before launch)",
          isinstance(result, tuple) and result[1] == [], f"result {result}")


# ---- F3 comparator harness (in memory)

def run_comparator(c: Ctx, argv: list[str], new_files, present_verdicts=(), started=True, frozen=None,
                   identity_error=None, runner_manifest_text=None, readiness=None, jobs=None):
    """Runs <comparator>.main(argv) for ONE Gate-0 job (the base cell) entirely in memory."""
    T, ce, s = c.T, c.T.ce, c.T.s
    here = FakeDir("harness")
    for name in present_verdicts:
        (here / name).text = "present\n"
    out_root = FakeDir("out")
    gate_dir = out_root.dir("gate0")
    if started:
        job_dir = gate_dir.dir(BASE)
        job_dir.run_files = new_files
        for f in ("bundle-manifest.json", "run-00.json", "summary.json", "transcript-00.ndjson"):
            (job_dir / f).text = "present"
    ref_root = FakeDir("ref")
    ref_dir = ref_root.dir(BASE)
    ref_dir.run_files = c.base_files
    writer = Writer(here)
    run_class = ce.Run

    def loader(cls, directory):
        return run_class(BASE, directory.run_files)

    identities = {"runner-tier3-v2": c.old_id, "runner-tier3-v3": c.new_id}

    def identity(root, manifest, label):
        if identity_error is not None and label == "runner-tier3-v3":
            raise identity_error
        return identities[label]

    driver_attrs = dict(frozen_input_problems=frozen or (lambda phase, for_comparator=False: []),
                        jobs=(lambda phase: list(jobs)) if jobs else
                        (lambda phase: [(BASE, "U", "d20181116-s10-r1-w08", "off", BASE + "-t38")]),
                        OUT_ROOT=out_root, REFERENCE_ROOT=ref_root,
                        GATE0_PASSED=here / T.gate_pass, GATE0_FAILED=here / T.gate_fail,
                        SMOKE_PASSED=here / "SMOKE-PASSED.txt", SMOKE_FAILED=here / "SMOKE-FAILED.txt",
                        DRIFT_STOP=here / "INPUT-DRIFT-STOP.txt",
                        RUNNER_MANIFEST=FakeFile("runner-tier3-v3.sha256.tsv", runner_manifest_text))
    if hasattr(s, "readiness_problems"):
        driver_attrs["readiness_problems"] = readiness or (lambda phase: [])
    outcome = {}
    original_load = run_class.__dict__["load"]
    with patched(ce, HERE=here, write=writer, runner_identity_from_disk=identity), \
            patched(s, **driver_attrs), contextlib.redirect_stdout(io.StringIO()):
        run_class.load = classmethod(loader)
        try:
            outcome["return"] = ce.main(argv)
        except BaseException as error:  # noqa: B902
            outcome["exception"] = f"{type(error).__name__}: {str(error)[:160]}"
        finally:
            run_class.load = original_load
    outcome["writes"] = list(writer.order)
    outcome["verdicts"] = {name: (here / name).text.splitlines()[-1] for name in (T.gate_pass, T.gate_fail)
                           if (here / name).text}
    outcome["texts"] = {name: (here / name).text for name in writer.order}
    return outcome


def report_name(c: Ctx, tag: str = "selftest") -> str:
    return f"gate0-compare-{tag}.txt" if c.T.version != "v1" else str(HERE / f"gate0-compare-{tag}.txt")


def f3_comparator(c: Ctx) -> None:
    T = c.T
    good = with_lines(c.base_files, fx_p2v(c).out())
    malformed = dict(good, **{"summary.json": b"{not json"})
    report = report_name(c)
    out = run_comparator(c, ["gate0", report], malformed)
    check("F3-11 a started job with malformed summary.json yields the Gate-0 failure verdict exactly once",
          out["writes"].count(T.gate_fail) == 1 and "exception" not in out, str(out["writes"]))
    out = run_comparator(c, ["gate0", report], good)
    check("F3-12 the verdict is written before the report (a report failure cannot skip the latch)",
          out["writes"][:1] in ([T.gate_pass], [T.gate_fail]) and len(out["writes"]) == 2, str(out["writes"]))
    check("F3-13 a normalisation fixture passes the in-memory gate0 comparator [positive]",
          out["verdicts"] == {T.gate_pass: f"{T.gate_word} PASSED"}, str(out["verdicts"]))
    out = run_comparator(c, ["gate0", T.gate_pass], good)
    check("F3-14 a report path equal to a verdict file name is refused before evaluation", out["writes"] == []
          and "exception" in out, str(out.get("exception")))
    out = run_comparator(c, ["gate0", "../gate0-compare-outside.txt"], good)
    check("F3-15 a report path outside the harness folder is refused", out["writes"] == [], str(out["writes"]))
    out = run_comparator(c, ["gate0", report], good, present_verdicts=("SMOKE-FAILED.txt",))
    check("F3-16 the gate0 comparator refuses while any *-FAILED.txt exists", out["writes"] == [], str(out["writes"]))
    for label, index, change in (
            ("F3-17 a decision payload that is a list", c.lay.second_decision, lambda e: e.__setitem__("payload", [])),
            ("F3-18 a final checkpoint without content", c.lay.checkpoint,
             lambda e: e["payload"].pop("content"))):
        files = with_lines(c.base_files, edit(c.lines, index, change))
        try:
            run = T.ce.Run("malformed", files)
            run_facts = T.ce.facts(run, c.old_id["dll"])
            check(f"{label} is a precondition problem, not an exception", bool(run_facts["problems"]),
                  "; ".join(run_facts["problems"][:2]))
        except BaseException as error:  # noqa: B902
            check(f"{label} is a precondition problem, not an exception", False,
                  f"exception escaped: {type(error).__name__}: {str(error)[:100]}")


# ---- F4 / N6 smoke evidence

def stability_index(lines) -> int:
    """First archived decision whose levels start accepted-request-count > worst-hard-utilization-ppm (no forced)."""
    return find(lines, lambda d, e: d == "runnerToAdapter" and e.get("messageType") == "decision" and [
        b["objectiveName"] for b in
        e["payload"]["solver"]["executionEvidence"]["selection"]["finalSolverDiagnostics"]["objectiveBounds"]][:2]
        == ["accepted-request-count", "worst-hard-utilization-ppm"])


def cf_decision(c: Ctx):
    """An archived decision whose two diagnostics are rewritten to the cost-first level list (stability levels
    removed, levels re-indexed). Every other evidence field is the archived value."""
    _, envelope = decode(c.lines[stability_index(c.lines)])
    for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
        diagnostics = envelope["payload"]["solver"]["executionEvidence"]["selection"][which]
        kept = [b for b in diagnostics["objectiveBounds"]
                if b["objectiveName"] != "worst-hard-utilization-ppm" and not b["objectiveName"].startswith("revision:")]
        for index, bound in enumerate(kept):
            bound["levelIndex"] = index
        diagnostics["objectiveBounds"] = kept
    return envelope


def unprove(bound: dict) -> None:
    """Make one level an unproven bound with consistent gap fields (CandidateSelectionSolver.cs:110-150)."""
    incumbent = bound["incumbentValue"]
    if bound["objectiveName"] == "accepted-request-count":          # Maximize
        bound["bestBound"] = incumbent + 1
    else:                                                            # Minimize
        assert incumbent >= 1, "an unproven minimize level needs a positive incumbent"
        bound["bestBound"] = incumbent - 1
    bound["gapNumerator"], bound["gapDenominator"], bound["isProvenOptimal"] = 1, max(1, incumbent), False


def as_run(envelopes=(), raws=()):
    lines = []
    frames = [canon(e) for e in envelopes] + list(raws)
    for ordinal, raw in enumerate(frames, start=1):
        lines.append(json.dumps({"direction": "runnerToAdapter", "frameBase64": base64.b64encode(raw).decode("ascii"),
                                 "frameLengthBytes": len(raw), "frameSha256": sha(raw), "ordinal": ordinal,
                                 "schemaVersion": "1.0.0"}, sort_keys=True, separators=(",", ":")))
    return types.SimpleNamespace(lines=lines)


def selection(solver):
    return solver["executionEvidence"]["selection"]


def variant(base, change):
    envelope = json.loads(json.dumps(base))
    change(envelope["payload"]["solver"], envelope)
    return envelope


def evidence_case(T, name, envelope, expect_problem, cf_order=True, raws=()):
    try:
        run = as_run([envelope] if envelope is not None else [], raws)
        problems, _ = (T.ce.objective_level_problems(run) if cf_order else
                       T.ce.objective_level_problems(run, cf_order=False))
    except BaseException as error:  # noqa: B902
        check(name, False, f"exception escaped: {type(error).__name__}: {error}")
        return
    check(name, bool(problems) == expect_problem, f"problems {problems[:2]}" if problems else "no problem")


def f4(c: Ctx) -> None:
    T = c.T
    base = cf_decision(c)
    names = [b["objectiveName"] for b in selection(base["payload"]["solver"])["finalSolverDiagnostics"]["objectiveBounds"]]
    say(f"   F4 base decision levels: {names[:3]} + {len(names) - 3} candidate-id ranks")

    def fallback(path, detail, primary_found):
        def change(solver, _):
            sel = selection(solver)
            sel["executionPath"], sel["finalSolveStatus"] = path, "safeFallback"
            primary = sel["primarySolverDiagnostics"]
            if primary_found:
                sel["primarySolveStatus"], sel["primaryIncumbentRejected"] = "feasible", True
                sel["validationWitnesses"] = [{"attemptedPath": "validatedIncumbent", "reasonCode": "INVALID",
                                               "selectedOptionIds": ["o1"]}]
                primary["objectiveBounds"] = primary["objectiveBounds"][:2]
                unprove(primary["objectiveBounds"][-1])
                primary["detailCode"] = "ORTOOLS_FEASIBLE_NOT_PROVEN"
                sel["fallbackValidationAttempts"] = 2
            else:
                sel["primarySolveStatus"], sel["primaryIncumbentRejected"] = "unknown", False
                sel["validationWitnesses"] = []
                primary["objectiveBounds"], primary["detailCode"] = [], "ORTOOLS_UNKNOWN"
                sel["fallbackValidationAttempts"] = 1
            sel["finalSolverDiagnostics"] = {"consumedWorkUnits": primary["consumedWorkUnits"],
                                             "consumedDeterministicTimeMicros":
                                                 primary["consumedDeterministicTimeMicros"],
                                             "objectiveBounds": [], "detailCode": detail}
        return change

    def feasible_prefix(solver, _):
        for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
            bounds = selection(solver)[which]["objectiveBounds"][:2]
            unprove(bounds[-1])
            selection(solver)[which]["objectiveBounds"] = bounds
            selection(solver)[which]["detailCode"] = "ORTOOLS_FEASIBLE_NOT_PROVEN"
        selection(solver)["primarySolveStatus"] = selection(solver)["finalSolveStatus"] = "feasible"

    def both(fn):
        def change(solver, _):
            for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
                fn(selection(solver)[which]["objectiveBounds"])
        return change

    def swap_first_two(bounds):
        bounds[0]["objectiveName"], bounds[1]["objectiveName"] = bounds[1]["objectiveName"], bounds[0]["objectiveName"]

    _, sf = decode(c.lines[stability_index(c.lines)])
    cases = [
        ("F4-3 valid cost-first decision evidence [positive]", base, False),
        ("F4-4 executionEvidence missing (status completed)", variant(base, lambda s, e: s.pop("executionEvidence")),
         True),
        ("F4-5 R24 probe: solver status feasible, no execution evidence",
         variant(base, lambda s, e: (s.clear(), s.update(status="feasible"))), True),
        ("F4-6 selection evidence missing", variant(base, lambda s, e: s["executionEvidence"].pop("selection")), True),
        ("F4-7 forbidden stability levels (archived stability-first decision)", sf, True),
        ("F4-8 a revision level present",
         variant(base, both(lambda b: b[1].__setitem__("objectiveName", "revision:dropEtaTotalMs"))), True),
        ("F4-9 operational-cost ranked before accepted-request-count", variant(base, both(swap_first_two)), True),
        ("F4-10 omittedCandidateCount != sum of generation omissions",
         variant(base, lambda s, e: selection(s).__setitem__("omittedCandidateCount",
                                                             selection(s)["omittedCandidateCount"] + 1)), True),
        ("F4-11 saturated omission evidence",
         variant(base, lambda s, e: selection(s).__setitem__("omissionCountWasSaturated", True)), True),
        ("F4-12 executionPath none (no validated publishing path)",
         variant(base, lambda s, e: selection(s).__setitem__("executionPath", "none")), True),
        ("F4-13 optimal solve with no objective bounds (absence without a reason)",
         variant(base, both(lambda b: b.clear())), True),
        ("F4-14 safeNoOp fallback after no primary incumbent (unknown, no bounds) [legitimate positive]",
         variant(base, fallback("safeNoOp", "SAFE_FALLBACK_NO_OP", False)), False),
        ("F4-15 greedySingleRequest fallback after a rejected feasible incumbent [legitimate positive]",
         variant(base, fallback("greedySingleRequest", "SAFE_FALLBACK_GREEDY_SINGLE_REQUEST", True)), False),
        ("F4-16 fallback with empty bounds but the wrong detailCode",
         variant(base, fallback("safeNoOp", "ORTOOLS_OPTIMAL", False)), True),
        ("F4-17 feasible solve with a consistent unproven prefix (exhausted budget) [legitimate positive]",
         variant(base, feasible_prefix), False),
    ]
    for name, envelope, expect_problem in cases:
        evidence_case(T, name, envelope, expect_problem)
    problems, info = T.ce.objective_level_problems(as_run([base]))
    keys = " | ".join(f"{k}={v}" for k, v in info.items())
    check("F4-18 the smoke report prints objective bounds and work values, not only list lengths (R22:66)",
          "operational-cost" in keys and "consumedWorkUnits" in keys and "incumbent" in keys, keys[:200])
    total, problems = 0, []
    for name in T.ce.REFERENCE_TRANSCRIPTS:
        run = types.SimpleNamespace(lines=split_lines(read_files(ARCHIVE / name)))
        found, info = T.ce.objective_level_problems(run, cf_order=False)
        total += info.get("decisions", 0)
        problems += found
    check("F4-19 calibration: every evidence rule except the cost-first order holds on all archived Gate-0 "
          "decisions (no false rejection of genuine evidence)", total > 0 and not problems,
          f"{total} decisions; {problems[:2]}")


# ---- F6 plan text and provenance

def f6(c: Ctx) -> None:
    T = c.T
    flat = " ".join(T.plan.read_text(encoding="utf-8").split())
    check("F6-1 plan records the R17 start rule: after the A1 milestone, deadline confirmed, >= 3 weeks for Stage 1",
          "A1 milestone" in flat and "deadline" in flat and "3 weeks" in flat, T.plan.name)
    check("F6-2 plan records the time-box consequence: drop the rest and report it as future work",
          "drop the rest" in flat and "future work" in flat, T.plan.name)
    check("F6-3 plan states that all Stage-2 arms use the same second published binary",
          "same second published binary" in flat, T.plan.name)
    check("F6-4 plan states that the two OFF rechecks exercise the changed 120-s path against available controls",
          "exercise the changed 120-s path against available controls" in flat, T.plan.name)
    wanted = ("Failing means the changed policy missed a predeclared aggregate reliability threshold; report the "
              "service composition and trajectory evidence before attributing the difference to the selector.")
    check("F6-5 H3 failure wording replaced verbatim (R24:140)", wanted in flat
          and "the selector does reliability work under the cap" not in flat, T.plan.name)
    s = T.s
    with patched(s, RUNNER_MANIFEST=FakeFile("runner-tier3-v3.sha256.tsv", "x"),
                 PROVENANCE=FakeFile("RUNNER-PROVENANCE-v3.md", f"tree {c.new_id['tree']} dll {c.new_dll} "
                                                                 f"commit `<40-hex>`"),
                 RUNNER=HERE / "no-such-runner-directory"), \
            patched(s.ce, runner_identity_from_disk=lambda *a: dict(c.new_id)):
        problems, _ = s.runner_problems()
    check("F6-6 a provenance receipt with unfilled template fields is refused (R24:197)",
          any("unfilled" in p for p in problems), str(problems))
    v = T.version
    check(f"F6-7 the {v} seal list names the {v} files and the external-tree manifests",
          {f"run_stage1b-{v}.py", f"compare_equivalence_stage1b-{v}.py", f"test_compare_equivalence_stage1b-{v}.py",
           f"selftest-2026-10-13-{v}.log"} <= set(s.SEALED_REQUIRED)
          and all(s.external_manifest(i).name in s.SEALED_REQUIRED for i, _, _ in s.EXTERNAL_TREES),
          str(s.SEALED_REQUIRED))


# ================================================================ section C: R28 finding controls

def n1(c: Ctx) -> None:
    T = c.T
    check("N1-a the Gate-0 verdict files are named behavioural-only", "BEHAVIOURAL" in T.s.GATE0_PASSED.name
          and "BEHAVIOURAL" in T.s.GATE0_FAILED.name, f"{T.s.GATE0_PASSED.name} / {T.s.GATE0_FAILED.name}")
    out = run_comparator(c, ["gate0", report_name(c, "n1")], with_lines(c.base_files, fx_p2v(c).out()))
    text = " ".join(out["texts"].values())
    check("N1-b every Gate-0 verdict discloses that decisions 2..N digests are not validated by the production .NET "
          "code", bool(out["texts"]) and "not validated by the production .NET code" in text
          and "protocol-invalid tape" in text, str(out["writes"]))
    flat = " ".join(T.plan.read_text(encoding="utf-8").split())
    check("N1-c the plan states the amended behavioural-only criterion and why (R28:56,62)",
          "amended from R22's full-validity criterion" in flat
          and "not validated by the production .NET code" in flat
          and "protocol-invalid tape with identical actions, ledgers and frame content could therefore pass" in flat
          and "exploratory" in flat, T.plan.name)
    paragraph = ("Stage 1b uses the execution software, resolved tools and steering environment recorded in the input "
                 "freeze. They are fixed from freeze time and shared by all new OFF, smoke and main jobs.")
    check("N1-d the plan adopts the R28 section-5 freeze-limitation paragraph",
          paragraph in flat and "historical software equality is unverified" in flat
          and "not equivalence on every main-panel cell or absence of runtime confounding" in flat, T.plan.name)


def frame_with_duplicate(line: str, old: bytes, new: bytes) -> str:
    raw = raw_frame(line)
    assert old in raw, old
    return encode_raw(line, raw.replace(old, new, 1))


def n2(c: Ctx) -> None:
    T, lines, lay, files = c.T, c.lines, c.lay, c.base_files

    def judge_lines(name, expected, new_lines, **kw):
        c.judge(name, expected, with_lines(files, new_lines), **kw)

    _, ck = decode(lines[lay.checkpoint])
    h = ck["payload"]["checkpointHash"].encode()
    out = list(lines)
    out[lay.checkpoint] = frame_with_duplicate(lines[lay.checkpoint], b'"checkpointHash":"' + h + b'"',
                                               b'"checkpointHash":"' + h + b'","checkpointHash":"' + h + b'"')
    judge_lines("N2-a R28 counterexample: final checkpointHash field duplicated with the same value, fresh frame "
                "SHA/length/receipt", False, out)
    mid = lay.middle_decision
    out = list(lines)
    out[mid] = frame_with_duplicate(lines[mid], b'"messageType":"decision"',
                                    b'"messageType":"decision","messageType":"decision"')
    judge_lines("N2-b duplicate envelope field (messageType) in a middle decision, fresh receipts", False, out)
    _, dm = decode(lines[mid])
    d = dm["payload"]["decisionHash"].encode()
    out = list(lines)
    out[mid] = frame_with_duplicate(lines[mid], b'"decisionHash":"' + d + b'"',
                                    b'"decisionHash":"' + d + b'","decisionHash":"' + d + b'"')
    judge_lines("N2-c duplicate nested decision field (decisionHash) in a middle decision, fresh receipts", False, out)
    act = find(lines, lambda d, e: d == "runnerToAdapter" and e.get("messageType") == "decision"
               and e["payload"]["actions"], mid)
    kind = decode(lines[act])[1]["payload"]["actions"][0]["decisionType"].encode()
    out = list(lines)
    out[act] = frame_with_duplicate(lines[act], b'"decisionType":"' + kind + b'"',
                                    b'"decisionType":"' + kind + b'","decisionType":"' + kind + b'"')
    judge_lines(f"N2-d duplicate field inside an action of the decision at frame {act + 1}, fresh receipts", False, out)
    record = json.loads(lines[mid])
    out = list(lines)
    out[mid] = lines[mid].replace(f'"ordinal":{record["ordinal"]}',
                                  f'"ordinal":{record["ordinal"]},"ordinal":{record["ordinal"]}', 1)
    judge_lines("N2-e duplicate field in a transcript record (ordinal)", False, out)
    dup_summary = dict(files)
    assert b'"status":"pass"' in files["summary.json"]
    dup_summary["summary.json"] = files["summary.json"].replace(b'"status":"pass"',
                                                                b'"status":"fail","status":"pass"', 1)
    c.judge("N2-f duplicate field in summary.json (status fail then pass), fresh bundle receipt", False,
            rebuild_bundle(dup_summary))
    dup_bundle = rebuild_bundle(dict(files))
    dup_bundle["bundle-manifest.json"] = dup_bundle["bundle-manifest.json"].replace(
        b'"schemaVersion":"1.0.0"', b'"schemaVersion":"1.0.0","schemaVersion":"1.0.0"', 1)
    c.judge("N2-g duplicate field in bundle-manifest.json", False, dup_bundle)
    out = list(lines)
    out[mid] = encode_raw(lines[mid], json.dumps(dm, sort_keys=True, separators=(", ", ": "),
                                                 ensure_ascii=False).encode("utf-8"))
    judge_lines("N2-h non-canonical frame encoding (whitespace) with fresh receipts is outside the transcript "
                "contract (F/actual_fleetpy_medium_verify.py:879-883)", False, out)
    out = list(lines)
    out[mid] = encode(lines[mid], dm)
    judge_lines("N2-i ordinary canonical re-encoding of a frame with fresh receipts [positive]", True, out)
    base = cf_decision(c)
    raw = canon(base)
    assert b'"executionPath":"validatedIncumbent"' in raw
    dup = raw.replace(b'"executionPath":"validatedIncumbent"',
                      b'"executionPath":"none","executionPath":"validatedIncumbent"', 1)
    evidence_case(T, "N2-j smoke decoding rejects a duplicate field (executionPath none, then validatedIncumbent)",
                  None, True, raws=(dup,))


def n3(c: Ctx) -> None:
    T = c.T

    def after(change):
        def make(w):
            text = w.text()
            change(w)
            return text
        return make
    freeze_case(T, "N3-a PATH changed after the freeze is detected (resolver policy frozen)", True,
                after(lambda w: w.environ.__setitem__("PATH", "X:/other-tools;" + w.environ["PATH"])))
    freeze_case(T, "N3-b SETUPTOOLS_USE_DISTUTILS set after the freeze is detected (.pth steering variable)", True,
                after(lambda w: w.environ.__setitem__("SETUPTOOLS_USE_DISTUTILS", "stdlib")))
    freeze_case(T, "N3-c GIT_DIR set after the freeze is detected", True,
                after(lambda w: w.environ.__setitem__("GIT_DIR", "X:/elsewhere/.git")))

    def pythonpath_at_freeze(w):
        w.environ["PYTHONPATH"] = "X:/shadow"
        return w.text()
    freeze_case(T, "N3-d PYTHONPATH recorded consistently at freeze time is still refused (redirect outside the "
                   "frozen trees)", True, pythonpath_at_freeze)
    freeze_case(T, "N3-e git resolving to another executable after the freeze is detected", True,
                after(lambda w: setattr(w, "git_at", "X:/other-tools/git.exe")))
    freeze_case(T, "N3-f a .pth path entry outside the frozen trees is refused", True,
                after(lambda w: w.pth.append(("evil.pth", "D:/outside/site-packages"))))
    freeze_case(T, "N3-g a complete freeze with tools, PATH and an in-tree .pth path passes [positive]", False,
                after(lambda w: w.pth.append(("ok.pth", "X:/env/Lib/site-packages/extra"))))
    import run_pilot
    s = T.s
    snapshots = {"drift": ["X:/frozen/file (size/mtime changed)"]}
    receipt = FakeFile("INPUT-DRIFT-STOP.txt")

    def write_receipt(path, lines):
        if receipt.text is not None:
            return False
        receipt.text = "\n".join(lines) + "\n"
        return True
    results = []
    with patched(s, STOP_FILE=FakeFile("STOP", present=False), GATE0_FAILED=FakeFile("f", present=False),
                 SMOKE_FAILED=FakeFile("g", present=False), GATE0_PASSED=FakeFile("p", present=False),
                 DRIFT_STOP=receipt, write_receipt=write_receipt, free_mib=lambda: 60_000,
                 stat_drift=lambda snapshot: list(snapshots["drift"])), \
            patched(run_pilot, power_status=lambda: (1, 100)):
        results.append(s.Admission("gate0", {}).admit("job-1"))
        snapshots["drift"] = []                    # the drifted file is restored; a new `run` invocation starts
        results.append(s.Admission("gate0", {}).admit("job-2"))
    check("N3-h observed drift is a durable stop: a new driver invocation still refuses after the bytes are restored",
          results[0] is not None and results[1] is not None and receipt.text is not None, str(results))
    post = getattr(s, "post_phase_problems", None)
    receipt2 = FakeFile("INPUT-DRIFT-STOP.txt")

    def write_receipt2(path, lines):
        receipt2.text = "\n".join(lines) + "\n"
        return True
    if post is None:
        check("N3-i a full post-phase verification (main included) writes the stop receipt", False,
              "no post-phase verification exists")
    else:
        with patched(s, input_integrity_problems=lambda phase: (["frozen input changed: X:/f"], {}),
                     DRIFT_STOP=receipt2, write_receipt=write_receipt2):
            found = post("main")
        check("N3-i a full post-phase verification (main included) writes the stop receipt", bool(found)
              and receipt2.text is not None and "after the phase" in receipt2.text, str(found))
    roots = {pathlib.Path(root).as_posix().lower() for _, root, _ in s.EXTERNAL_TREES}
    check("N3-j the resolver scope is declared: Git tree frozen, git a resolved tool, PATH a frozen name",
          "c:/program files/git" in roots and "git" in getattr(s, "REQUIRED_TOOLS", ())
          and "PATH" in getattr(s, "FROZEN_ENV_NAMES", ()), str(sorted(roots)))
    if T.version == "v3":
        resolved = s.resolve_tool("git")
        tree_roots = [pathlib.Path(root).as_posix() for _, root, _ in s.EXTERNAL_TREES]
        check("N3-k read-only: git resolves inside the frozen Git tree in this environment", resolved is not None
              and s._inside(resolved, tree_roots), str(resolved))
        audit = s.pth_redirect_problems(s.pth_lines(), s.PYTHON_SITE, tree_roots)
        check("N3-l read-only: the job interpreter's .pth files add no path outside the frozen trees", not audit,
              f"{len(s.pth_lines())} .pth line(s); {audit}")
    elif T.version == "v4":
        resolved = s.tool_resolution("git", "job")
        tree_roots = [pathlib.Path(root).as_posix() for _, root, _ in s.EXTERNAL_TREES]
        check("N3-k read-only: git resolves inside the frozen Git tree in this environment (v4: CreateProcess order, "
              "job context)", resolved is not None and s._inside(resolved, tree_roots), str(resolved))
        audit = s.pth_audit_problems(tree_roots)
        check("N3-l read-only: the job interpreter's .pth files (both site directories) add no path outside the "
              "frozen trees and only reviewed import lines", not audit, str(audit))


def n4(c: Ctx) -> None:
    T = c.T
    good = with_lines(c.base_files, fx_p2v(c).out())

    def raising(phase, for_comparator=False):
        raise ValueError("injected: frozen WP4 is malformed JSON")
    out = run_comparator(c, ["gate0", report_name(c, "n4a")], good, frozen=raising)
    check("N4-a a frozen-input exception after the jobs started writes the Gate-0 failure verdict exactly once",
          out["writes"].count(T.gate_fail) == 1 and "exception" not in out, f"{out['writes']} {out.get('exception')}")
    out = run_comparator(c, ["gate0", report_name(c, "n4b")], good, identity_error=OSError("tree unreadable"),
                         runner_manifest_text="# tree-sha256 " + "e" * 64 + " files 3\nx\n")
    text = " ".join(out["texts"].values())
    check("N4-b an unreadable new runner tree after the jobs started writes the failure verdict with the frozen "
          "expected identity", out["writes"].count(T.gate_fail) == 1 and "e" * 64 in text,
          f"{out['writes']} {out.get('exception')}")
    malformed_wp4 = FakeFile("wp4-tier3-v2-costfirst.json", data=b'{"a":1,"a":2}')
    try:
        with patched(T.s, WP4_ON=malformed_wp4):
            problems = T.s.config_problems()
        check("N4-c a malformed frozen WP4 file is an input problem, not an exception", bool(problems), str(problems[:2]))
    except BaseException as error:  # noqa: B902
        check("N4-c a malformed frozen WP4 file is an input problem, not an exception", False,
              f"exception escaped: {type(error).__name__}: {error}")

    def unreadable(w):
        w.unreadable.add("X:/s/fixture.json")
        return w.text()
    freeze_case(T, "N4-d an unreadable frozen file is an input problem, not an exception", True, unreadable)
    out = run_comparator(c, ["gate0", report_name(c, "n4e")], good, started=False)
    check("N4-e a phase whose jobs have not started is still refused without any verdict [positive]",
          out["writes"] == [] and "not ready" in str(out.get("exception")), str(out.get("exception")))


def n5(c: Ctx) -> None:
    T = c.T
    job = T.s.jobs("gate0")[0]
    calls = {"n": 0}

    def power():
        calls["n"] += 1
        return (1, 100) if calls["n"] == 1 else (0, 100)    # AC at the wait, lost while queued / scanning
    result, _ = call_world(T, dict(power=power, free=lambda: 60_000),
                           lambda w: (T.s.run_job("gate0", *job), list(w.launches)))
    check("N5-a AC lost after the power wait (queue / stat scan) prevents the launch", isinstance(result, tuple)
          and result[1] == [], f"result {result}")
    result, _ = call_world(T, dict(power=lambda: (1, 100), free=lambda: 60_000),
                           lambda w: (T.s.run_job("gate0", *job), list(w.launches)))
    check("N5-b AC present throughout launches the job [positive]", isinstance(result, tuple) and len(result[1]) == 1,
          f"result {result}")
    acquire = getattr(T.s, "acquire_instance_lock", None)
    if acquire is None:
        check("N5-c a second concurrent driver instance is refused (reservations are per process)", False,
              "no instance lock exists")
        return
    held = {"locked": False}

    class Handle:
        def close(self):
            pass

    def locker(handle):
        if held["locked"]:
            raise OSError("lock held by another process")
        held["locked"] = True
    outcomes = []
    with patched(T.s, _INSTANCE_HANDLE=None):
        for _ in range(2):
            try:
                acquire(opener=lambda path, mode: Handle(), locker=locker)
                outcomes.append("acquired")
            except SystemExit as error:
                outcomes.append(f"refused: {error}")
    check("N5-c a second concurrent driver instance is refused (reservations are per process)",
          outcomes[0] == "acquired" and outcomes[1].startswith("refused"), str(outcomes))


def n6(c: Ctx) -> None:
    T = c.T
    base = cf_decision(c)

    def both(fn):
        def change(solver, _):
            for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
                fn(selection(solver)[which]["objectiveBounds"])
        return change

    def keep_first_two(bounds):
        del bounds[2:]

    def negative_cost(bounds):
        cost = next(b for b in bounds if b["objectiveName"] == "operational-cost")
        cost["incumbentValue"] = cost["bestBound"] = -1

    def swap_ranks(bounds):
        ranks = [b for b in bounds if b["objectiveName"].startswith("candidate-id-rank:")]
        ranks[0]["objectiveName"], ranks[1]["objectiveName"] = ranks[1]["objectiveName"], ranks[0]["objectiveName"]

    def foreign_rank(bounds):
        ranks = [b for b in bounds if b["objectiveName"].startswith("candidate-id-rank:")]
        ranks[-1]["objectiveName"] = "candidate-id-rank:veh-zzz-not-in-the-generation-evidence"

    def bool_level_index(bounds):
        bounds[1]["levelIndex"] = True

    def prefix(fn):
        def change(solver, _):
            for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
                bounds = selection(solver)[which]["objectiveBounds"][:2]
                unprove(bounds[-1])
                fn(bounds[-1])
                selection(solver)[which]["objectiveBounds"] = bounds
                selection(solver)[which]["detailCode"] = "ORTOOLS_FEASIBLE_NOT_PROVEN"
            selection(solver)["primarySolveStatus"] = selection(solver)["finalSolveStatus"] = "feasible"
        return change

    def wrong_gap(bound):
        bound["gapNumerator"] += 1

    def wrong_side(bound):
        bound["bestBound"] = bound["incumbentValue"] + 1      # minimize: bound above the incumbent

    def forced_witness(solver, envelope):
        body = envelope["payload"]["certificate"].setdefault("body", {})
        body.setdefault("witnesses", []).append({"stage": "forcedReference", "code": "SELFTEST_FORCED"})

    def over_budget(solver, _):
        for which in ("primarySolverDiagnostics", "finalSolverDiagnostics"):
            selection(solver)[which]["consumedWorkUnits"] = 100_001
    cases = [
        ("N6-a R28 probe: optimal diagnostics keep only accepted count and cost (all tie ranks missing)",
         variant(base, both(keep_first_two)), True),
        ("N6-b R28 probe: optimal cost incumbent and best bound -1", variant(base, both(negative_cost)), True),
        ("N6-c candidate-id ranks not in ordinal vehicle order", variant(base, both(swap_ranks)), True),
        ("N6-d a candidate-id rank for a vehicle absent from the generation evidence",
         variant(base, both(foreign_rank)), True),
        ("N6-e levelIndex given as a boolean", variant(base, both(bool_level_index)), True),
        ("N6-f unproven prefix with inconsistent gap arithmetic", variant(base, prefix(wrong_gap)), True),
        ("N6-g unproven prefix with the best bound on the wrong side for a minimize level",
         variant(base, prefix(wrong_side)), True),
        ("N6-h forced witness in the certificate but no forced-reference level", variant(base, forced_witness), True),
        ("N6-i solver work above the frozen budget (100,001 > 100,000)", variant(base, over_budget), True),
        ("N6-j consistent unproven prefix of a feasible solve [positive]", variant(base, prefix(lambda b: None)), False),
    ]
    for name, envelope, expect_problem in cases:
        evidence_case(T, name, envelope, expect_problem)
    forced_lines = split_lines(read_files(ARCHIVE / FORCED_RUN))
    run = types.SimpleNamespace(lines=forced_lines)
    found, info = T.ce.objective_level_problems(run, cf_order=False)
    shapes = [k for k in info if "forced-reference-count" in k]
    check(f"N6-k calibration on {FORCED_RUN} (forced levels present): every evidence rule except the cost-first "
          f"order holds [positive]", info.get("decisions", 0) > 0 and not found and bool(shapes),
          f"{info.get('decisions')} decisions; {found[:2]}; forced shapes {len(shapes)}")
    wp4 = json.loads(ON.read_text(encoding="utf-8"))
    want = {"maximumExplorationWorkUnits": wp4["candidateGeneration"]["maximumExplorationWorkUnits"],
            "maximumGenerationWorkUnits": wp4["solverExecution"]["maximumGenerationWorkUnits"],
            "maximumValidationWorkUnits": wp4["solverExecution"]["maximumValidationWorkUnits"],
            "maximumSolverWorkUnits": wp4["solverExecution"]["maximumSolverWorkUnits"],
            "maximumSolverDeterministicTimeMicros": wp4["solverExecution"]["maximumSolverDeterministicTimeMicros"]}
    check("N6-l the evidence budgets equal the frozen ON WP4 values", getattr(T.ce, "BUDGETS", None) == want,
          str(getattr(T.ce, "BUDGETS", None)))


def n7(c: Ctx) -> None:
    T = c.T
    failing = {"label": "runner-tier3-v2", "ok": False, "tree": None, "files": None, "dll": None,
               "problems": ["runner-tier3-v2: runner directory differs from its manifest (injected)"]}
    if T.version in ("v3", "v4"):
        reports = []
        with patched(T.ce, runner_identity_from_disk=lambda *a: dict(failing)):
            Ctx(T, report=lambda name, ok, detail="": reports.append((name, ok)))
        check("N7-a a failed live runner-tree verification is reported as BAD before the manifest-only fallback",
              any(not ok and "R1-live" in name for name, ok in reports), str(reports))
    else:
        v2test = load("test_compare_equivalence_stage1b_v2_module", "test_compare_equivalence_stage1b-v2.py")
        before = list(v2test.FAILURES)
        with patched(T.ce, runner_identity_from_disk=lambda *a: dict(failing)):
            ctx = v2test.Ctx(T)
        reported = len(v2test.FAILURES) > len(before)
        check("N7-a a failed live runner-tree verification is reported as BAD before the manifest-only fallback",
              reported, f"v2 Ctx used identity ok={ctx.old_id['ok']} ({ctx.old_id['label']}) and reported nothing")


def n8(c: Ctx) -> None:
    T = c.T
    try:
        T.ce.canonical_bytes({chr(0xE000): 9_007_199_254_740_992})
        check("N8-a a key at U+E000 with an out-of-range integer is rejected (range check before the writer choice)",
              False, "serialized without error")
    except cj.CanonicalJsonError as error:
        check("N8-a a key at U+E000 with an out-of-range integer is rejected (range check before the writer choice)",
              True, str(error))
    value = {chr(0xE000): 1, chr(0x1F600): 9_007_199_254_740_991}
    check("N8-b a key at U+E000 with an in-range integer still equals the C# mirror [positive]",
          T.ce.canonical_bytes(value) == cj.canonical_bytes_of_value(value))


# ================================================================ section D: R38 finding controls

F64 = "f" * 64


def report_files(files, change=None, consistent=False, summary_change=None):
    """Codex R38-1: a fresh-bundle copy of a bundle with one run report change. consistent=True recomputes run-00
    semanticHash with the frozen verifier's formula (av._semantic_hash, actual_fleetpy_medium_verify.py:116-119) and
    copies it into the summary receipts, so only the changed link itself can reveal the change."""
    run0, summary = json.loads(files["run-00.json"]), json.loads(files["summary.json"])
    if change is not None:
        change(run0)
    if consistent:
        run0["semanticHash"] = av._semantic_hash(run0["semantic"])
        summary["semanticHash"] = run0["semanticHash"]
        for item in summary.get("runs") or []:
            item["semanticHash"] = run0["semanticHash"]
    if summary_change is not None:
        summary_change(summary)
    out = dict(files)
    out["run-00.json"] = json.dumps(run0, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    out["summary.json"] = json.dumps(summary, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return rebuild_bundle(out)


def semantic_set(key, value=None, delta=None):
    def change(run0):
        semantic = run0["semantic"]
        semantic[key] = semantic[key] + delta if delta is not None else value
    return change


def pair_ok(c: Ctx, name: str, files, identity=None) -> bool:
    ce = c.T.ce
    identity = c.old_id if identity is None else identity
    try:
        run = ce.Run(name, files)
        ok, _ = ce.pair_report(c.base_run, c.base_facts, run,
                               ce.facts(run, identity.get("dll") if identity.get("ok") else None),
                               c.u_binding, c.old_id, identity)
        return ok
    except Exception:
        return False


def facts_case(c: Ctx, name: str, files, expect_problem: bool, mention: str | None = None) -> None:
    """Single-run integrity, as the smoke check uses it (no reference run to compare against)."""
    ce = c.T.ce
    try:
        problems = ce.facts(ce.Run(name, files), c.old_id["dll"])["problems"]
    except BaseException as error:  # noqa: B902
        malformed = getattr(ce, "MalformedRun", None)
        if malformed is None or not isinstance(error, malformed):
            check(name, False, f"exception escaped: {type(error).__name__}: {str(error)[:100]}")
            return
        problems = [f"malformed run bundle: {error}"]
    good = bool(problems) == expect_problem and (not mention or any(mention in p for p in problems))
    check(name, good, f"problems {problems[:2]}" if problems else "no problem")


def r38_1(c: Ctx) -> None:
    T, files = c.T, c.base_files
    c.judge("R38-1-pos the archived U-w08 bundle as an identical-copy pair, fresh receipts [positive]", True,
            report_files(files))

    def four(run0):
        run0["semanticHash"] = F64
        for key in ("manifestHash", "checkpointBindingHash", "publicationDigest"):
            run0["semantic"][key] = F64
    codex = [
        ("R38-1-a Codex: run-00 semanticHash = 64 'f'", lambda r: r.__setitem__("semanticHash", F64)),
        ("R38-1-b Codex: semantic.manifestHash = 64 'f'", semantic_set("manifestHash", F64)),
        ("R38-1-c Codex: semantic.checkpointBindingHash = 64 'f'", semantic_set("checkpointBindingHash", F64)),
        ("R38-1-d Codex: semantic.publicationDigest = 64 'f'", semantic_set("publicationDigest", F64)),
        ("R38-1-e Codex: artifactReceiptsEqual = false", lambda r: r.__setitem__("artifactReceiptsEqual", False)),
        ("R38-1-f Codex: the four digest fields changed together", four),
    ]
    for name, change in codex:
        c.judge(f"{name}; identical-copy pair, fresh bundle receipt", False, report_files(files, change))
    c.judge("R38-1-g semantic.manifestHash = 64 'f' with a CONSISTENT semanticHash and summary receipts", False,
            report_files(files, semantic_set("manifestHash", F64), consistent=True))
    c.judge("R38-1-h semantic.publicationDigest = 64 'f' with a consistent semanticHash and summary receipts", False,
            report_files(files, semantic_set("publicationDigest", F64), consistent=True))
    c.judge("R38-1-i semantic.checkpointBindingHash not lowercase hex ('F' x 64), consistent semanticHash", False,
            report_files(files, semantic_set("checkpointBindingHash", "F" * 64), consistent=True))
    c.judge("R38-1-j summary semanticHash differs from run-00 (both well-formed)", False,
            report_files(files, summary_change=lambda s: s.__setitem__("semanticHash", F64)))
    c.judge("R38-1-k summary repeatCount 2 for a single-repeat bundle", False,
            report_files(files, summary_change=lambda s: s.__setitem__("repeatCount", 2)))
    p3 = with_lines(files, rebuild(Transcript(fx_p3(c)), c.lay).out(), report=False)
    c.judge("R38-1-l the P3n normalisation fixture WITHOUT refreshed report receipts fails (its stale publication "
            "receipt is recomputable metadata; Codex R38-1)", False, p3, c.new_id)
    c.judge("R38-1-m the same P3n fixture with refreshed report receipts passes [positive]", True,
            with_lines(files, rebuild(Transcript(fx_p3(c)), c.lay).out()), c.new_id)
    facts_case(c, "R38-1-s0 single-run report checks on the archived bundle [positive]", report_files(files), False)

    def swap_ids(run0):
        ids = run0["semantic"]["acceptedRequestIds"]
        ids[0], ids[1] = ids[1], ids[0]
    single = [
        ("R38-1-s1 semantic.publicationCount + 1", semantic_set("publicationCount", delta=1)),
        ("R38-1-s2 semantic.nextEpoch + 1", semantic_set("nextEpoch", delta=1)),
        ("R38-1-s3 semantic.nextEventSeq + 1", semantic_set("nextEventSeq", delta=1)),
        ("R38-1-s4 semantic.finalSimulationTimeMs + 1", semantic_set("finalSimulationTimeMs", delta=1)),
        ("R38-1-s5 semantic.requestCount + 1", semantic_set("requestCount", delta=1)),
        ("R38-1-s6 semantic.finalVehiclePositions with one entry dropped",
         lambda r: r["semantic"]["finalVehiclePositions"].pop()),
        ("R38-1-s7 semantic.acceptedRequestIds out of order", swap_ids),
        ("R38-1-s8 run-00 repeat 1 in a single-repeat bundle", lambda r: r.__setitem__("repeat", 1)),
    ]
    for name, change in single:
        facts_case(c, f"{name}, consistent semanticHash (single-run integrity, as the smoke check uses it)",
                   report_files(files, change, consistent=True), True, mention="run report")
    ok = pair_ok(c, "R38-1-LIMIT", report_files(files, semantic_set("checkpointBindingHash", F64), consistent=True))
    limit("R38-1-LIMIT a well-formed but false checkpointBindingHash with a consistent semanticHash",
          f"comparator {'PASSES' if ok else 'FAILS'} it; only its 64-hex format is checkable: the preimage contains "
          f"physical adapter state (F/ridebound_fleetpy/fleet_control.py:1004-1035) that a bundle does not carry; raw "
          f"cross-run equality of this salted digest stays waived (R22:194; R38-1 'does not demand an invented "
          f"offline preimage')")
    if getattr(T.ce, "report_problems", None) is None:
        say("   R38-1-cal not applicable: this comparator has no run-report check")
        return
    found, names = [], list(T.ce.REFERENCE_TRANSCRIPTS) + [FORCED_RUN]
    for name in names:
        run = T.ce.Run(name, read_files(ARCHIVE / name))
        found += [f"{name}: {p}" for p in T.ce.facts(run, c.old_id["dll"])["problems"] if p.startswith("run report")]
        del run
    check(f"R38-1-cal calibration: every run-report rule holds on the four Gate-0 references and {FORCED_RUN} "
          f"(no false rejection) [positive]", not found, f"{len(names)} archived runs; {found[:2]}")


def run_main(c: Ctx, phase: str, *, started=(), freeze_results=((),), run_job=None, verdicts=None, analysis=None):
    """The driver's `run <phase>` (main()) with every side effect in memory: no lock file, no process, no keep-awake,
    no file written; INPUT-DRIFT-STOP.txt is an in-memory receipt. started: phases with a job output directory.
    freeze_problems returns freeze_results[k] on its k-th call (the last entry repeats)."""
    T, s = c.T, c.T.s
    import run_pilot
    ident = dict(c.new_id, tree="t" * 64)
    out_root, log_root = FakeDir("out"), FakeDir("logs")
    for done in started:
        out_root.dir(done).dir(s.jobs(done)[0][0])
    receipt = FakeFile("INPUT-DRIFT-STOP.txt")
    state = {"freeze": 0, "awake": []}

    def write_receipt(path, lines):
        if receipt.text is not None:
            return False
        receipt.text = "\n".join(lines) + "\n"
        return True

    def freeze(*args, **kwargs):
        state["freeze"] += 1
        return list(freeze_results[min(state["freeze"], len(freeze_results)) - 1])
    attrs = dict(freeze_problems=freeze, write_receipt=write_receipt, DRIFT_STOP=receipt,
                 acquire_instance_lock=lambda *a, **k: None, OUT_ROOT=out_root, LOG_ROOT=log_root,
                 frozen_paths=lambda: [], stat_snapshot=lambda paths: {},
                 run_job=run_job or (lambda job_phase, name, *rest: (name, "deferred-stop")))
    if analysis is not None:
        attrs["analysis_freeze_problems"] = analysis
    with driver_stubs(T, ident, **(verdicts or {})), patched(s, **attrs), \
            patched(run_pilot, keep_awake=lambda on: state["awake"].append(on)), \
            patched(sys, argv=["run_stage1b.py", "run", phase]), environ(RB_ALLOW_BATTERY_MIN_PERCENT=None), \
            contextlib.redirect_stdout(io.StringIO()):
        try:
            state["return"] = s.main()
        except BaseException as error:  # noqa: B902
            state["exception"] = error
    state["receipt"] = receipt.text
    return state


def describe(st: dict) -> str:
    error = st.get("exception")
    shown = f"{type(error).__name__}: {str(error)[:90]}" if error is not None else None
    return (f"return {st.get('return')!r}; exception {shown}; freeze checks {st['freeze']}; keep-awake {st['awake']}; "
            f"drift receipt {'written: ' + st['receipt'].splitlines()[0][:70] if st['receipt'] else 'none'}")


def r38_2(c: Ctx) -> None:
    T = c.T
    ident = dict(c.new_id, tree="t" * 64)
    drift = ["frozen input changed or missing: X:/frozen/configs/U.json"]
    st = run_main(c, "gate0", started=("gate0",), freeze_results=(drift,))
    check("R38-2-a Codex case 1: start-up integrity drift after earlier gate0 outputs exist is persisted "
          "(INPUT-DRIFT-STOP.txt), not only refused", isinstance(st.get("exception"), SystemExit)
          and st["receipt"] is not None, describe(st))
    st = run_main(c, "smoke", started=("gate0",), freeze_results=(drift,),
                  verdicts=dict(GATE0_PASSED=verdict_text(T.gate_word, ident)))
    check("R38-2-b Codex case 1, later invocation: `run smoke` after the Gate-0 phase persists the drift",
          isinstance(st.get("exception"), SystemExit) and st["receipt"] is not None, describe(st))
    st = run_main(c, "gate0", freeze_results=(["stage1b-input-freeze.sha256 does not exist (run `freeze` ...)"],))
    check("R38-2-c never started: a missing freeze is refused WITHOUT a drift receipt (no invented drift) [positive]",
          isinstance(st.get("exception"), SystemExit) and st["receipt"] is None, describe(st))

    def failing_job(job_phase, name, *rest):
        raise OSError("injected: exclusive log write failed")
    st = run_main(c, "gate0", freeze_results=((), drift), run_job=failing_job)
    check("R38-2-d Codex case 2: after a worker OSError the full post-phase verification still runs and persists the "
          "drift; the original OSError propagates", isinstance(st.get("exception"), OSError)
          and "injected" in str(st.get("exception")) and st["freeze"] >= 2 and st["receipt"] is not None
          and "after the phase" in st["receipt"] and st["awake"] == [True, False], describe(st))
    st = run_main(c, "gate0", freeze_results=((), ()), run_job=failing_job)
    check("R38-2-e a worker OSError followed by a CLEAN post-phase verification: the verification ran, no receipt, "
          "and the OSError still propagates (no clean completion)", isinstance(st.get("exception"), OSError)
          and st["freeze"] >= 2 and st["receipt"] is None and "return" not in st, describe(st))
    st = run_main(c, "gate0", freeze_results=((), drift))
    check("R38-2-f a deferred phase with a failing post-phase verification returns 4 and persists the drift "
          "[positive]", st.get("return") == 4 and st["receipt"] is not None, describe(st))
    st = run_main(c, "gate0", freeze_results=((), ()))
    check("R38-2-g a deferred phase with a clean post-phase verification returns 3, no receipt [positive]",
          st.get("return") == 3 and st["receipt"] is None, describe(st))
    good = with_lines(c.base_files, fx_p2v(c).out())
    for tag, missing in (("h", "stage1b-input-freeze.sha256 does not exist"),
                         ("i", "STAGE1B-PLAN.sha256 does not exist"),
                         ("j", "runner-tier3-v3.sha256.tsv does not exist")):
        out = run_comparator(c, ["gate0", report_name(c, f"r38-2{tag}")], good,
                             readiness=lambda phase, m=missing: [m])
        text = " ".join(out["texts"].values())
        check(f"R38-2-{tag} Codex case 3: a started job and {missing.split()[0]} missing afterwards writes the Gate-0 "
              f"failure verdict exactly once (loss after start), not 'not ready'",
              out["writes"].count(T.gate_fail) == 1 and "exception" not in out and missing.split()[0] in text,
              f"{out['writes']} {out.get('exception')}")
    out = run_comparator(c, ["gate0", report_name(c, "r38-2k")], good, started=False,
                         readiness=lambda phase: ["stage1b-input-freeze.sha256 does not exist"])
    check("R38-2-k never started and the freeze missing: refused without any verdict [positive]",
          out["writes"] == [] and "not ready" in str(out.get("exception")), str(out.get("exception")))
    two = [(BASE, "U", "d20181116-s10-r1-w08", "off", BASE + "-t38"),
           ("C-30-d20181116-s10-r1-w08", "C-30", "d20181116-s10-r1-w08", "off", "C-30-d20181116-s10-r1-w08-t38")]
    out = run_comparator(c, ["gate0", report_name(c, "r38-2l")], good, jobs=two)
    check("R38-2-l a phase with one job started and one not started (prerequisites intact) gets no verdict yet "
          "[positive]", out["writes"] == [] and "exception" in out, str(out.get("exception")))


def r38_3(c: Ctx) -> None:
    T, s = c.T, c.T.s
    import run_pilot

    def admit_with(during_scan):
        flags = {"stop": False, "failed": False, "drift": False}

        def stat_drift(snapshot):
            during_scan(flags)
            return []
        admission = s.Admission("gate0", {})
        with patched(s, STOP_FILE=FakeFile("STOP", present=lambda: flags["stop"]),
                     GATE0_FAILED=FakeFile("f", present=lambda: flags["failed"]),
                     SMOKE_FAILED=FakeFile("g", present=False), GATE0_PASSED=FakeFile("p", present=False),
                     DRIFT_STOP=FakeFile("d", present=lambda: flags["drift"]), free_mib=lambda: 60_000,
                     stat_drift=stat_drift), patched(run_pilot, power_status=lambda: (1, 100)):
            result = admission.admit("job-1")
        return result, admission.reserved
    for tag, key, what in (("a", "stop", "Codex: STOP created during the long stat scan"),
                           ("b", "failed", "the Gate-0 failure verdict written during the scan"),
                           ("c", "drift", "INPUT-DRIFT-STOP.txt written during the scan")):
        result, reserved = admit_with(lambda flags, k=key: flags.__setitem__(k, True))
        check(f"R38-3-{tag} {what} prevents the admission; nothing is reserved", result is not None and reserved == 0,
              f"admit -> {result!r}; reserved {reserved} MiB")
    result, reserved = admit_with(lambda flags: None)
    check("R38-3-d nothing changes during the scan: admitted with 410 MiB reserved [positive]",
          result is None and reserved == s.NEED_MIB, f"admit -> {result!r}; reserved {reserved} MiB")


def verifier_rejects(envelope) -> tuple[bool, str]:
    """The frozen audited verifier's verdict on the same solver block (actual_fleetpy_medium_verify.py:179-446)."""
    try:
        av._verify_audited_solver_evidence(json.loads(json.dumps(envelope["payload"]["solver"])), 3)
        return False, "frozen verifier accepts"
    except RuntimeError as error:
        return True, f"frozen verifier: {error}"


def grounded_case(T, name: str, envelope, expect_problem: bool) -> None:
    """An evidence case whose expected result is also the frozen verifier's verdict on the same input."""
    rejected, why = verifier_rejects(envelope)
    try:
        problems, _ = T.ce.objective_level_problems(as_run([envelope]))
    except BaseException as error:  # noqa: B902
        check(name, False, f"exception escaped: {type(error).__name__}: {error}")
        return
    good = bool(problems) == expect_problem and rejected == expect_problem
    check(name, good, f"comparator: {problems[:1] or 'no problem'}; {why[:120]}")


def r38_4(c: Ctx) -> None:
    T = c.T
    base = cf_decision(c)
    digest = sha(b"selftest omission digest")

    def generation_of(solver):
        return solver["executionEvidence"]["generation"]

    def omission(count, **extra):
        item = {"code": "WORK_BUDGET", "count": count, "stableDigest": digest, "countWasSaturated": False}
        item.update(extra)
        return item

    def append_omissions(*items):
        def change(solver, _):
            generation_of(solver)["omissions"].extend(json.loads(json.dumps(items)))
        return change

    def set_loss(key, value):
        def change(solver, _):
            generation_of(solver)["vehicleLosses"][0][key] = value
        return change

    def drop_loss(key):
        def change(solver, _):
            del generation_of(solver)["vehicleLosses"][0][key]
        return change

    def breaches(items):
        def change(solver, _):
            generation_of(solver)["exogenousServiceQualityBreaches"] = json.loads(json.dumps(items))
        return change

    def pruned(items):
        def change(solver, _):
            solver["executionEvidence"]["prunedCandidates"] = json.loads(json.dumps(items))
        return change

    def valid_omission(solver, _):
        generation_of(solver)["omissions"].append(omission(2, vehicleId="veh-a", requestIds=["req-1", "req-2"]))
        sel = selection(solver)
        sel["omittedCandidateCount"] += 2
        sel.setdefault("omissionDigest", sha(b"selftest selection omission digest"))
    b1 = {"vehicleId": "veh-a", "requestId": "req-1", "code": "EXOGENOUS_DELAY", "dimension": "pickup",
          "contractualMilliseconds": 1000, "exogenousMilliseconds": 2000}
    b2 = dict(b1, requestId="req-2")
    commitment = {"candidateId": "cand-1", "vehicleId": "veh-a", "newRequestIds": ["req-1"], "code": "COMMITMENT",
                  "commitmentWitnesses": [{"stage": "commitment", "code": "C30", "vehicleId": "veh-a",
                                           "requestId": "req-0", "dimension": "drop", "rule": "maxDelay",
                                           "limit": 30000, "before": 0, "delta": 31000, "after": 31000}]}
    physical = dict(commitment, candidateId="cand-2", code="CAPACITY", commitmentWitnesses=[],
                    physicalWitness={"code": "CAPACITY", "vehicleId": "veh-a", "stopId": "s1", "dimension": "seats",
                                     "expected": 4, "actual": 5})
    rejected, why = verifier_rejects(base)
    found, _ = T.ce.objective_level_problems(as_run([base]))
    check("R38-4-base the CF-shaped fixture is accepted by the frozen audited verifier and by the comparator "
          "[positive]", not rejected and not found, f"{why}; comparator {found[:1] or 'no problem'}")
    cases = [
        ("R38-4-a Codex: two complete omissions with counts -1 and +1 (sum 0, totals agree)",
         append_omissions(omission(-1), omission(1)), True),
        ("R38-4-b a vehicle-loss counter is negative (evaluatedCandidatePathCount -1)",
         set_loss("evaluatedCandidatePathCount", -1), True),
        ("R38-4-c a vehicle-loss counter is a boolean (retainedCandidateCount true)",
         set_loss("retainedCandidateCount", True), True),
        ("R38-4-d a vehicle-loss flag is a string (candidateCapApplied 'no')", set_loss("candidateCapApplied", "no"),
         True),
        ("R38-4-e a vehicle loss without eligibleRepairRequestCount", drop_loss("eligibleRepairRequestCount"), True),
        ("R38-4-f an omission with an extra field", append_omissions(omission(0, note="x")), True),
        ("R38-4-g an omission stableDigest that is not lowercase SHA-256",
         append_omissions(dict(omission(0), stableDigest="F" * 64)), True),
        ("R38-4-h an omission with duplicate requestIds", append_omissions(omission(0, requestIds=["req-1", "req-1"])),
         True),
        ("R38-4-i an omission with an empty code", append_omissions(dict(omission(0), code="")), True),
        ("R38-4-j an exogenous breach that does not exceed its contract",
         breaches([dict(b1, exogenousMilliseconds=1000)]), True),
        ("R38-4-k exogenous breaches out of canonical order", breaches([b2, b1]), True),
        ("R38-4-l a duplicate exogenous breach", breaches([b1, b1]), True),
        ("R38-4-m a pruned candidate without its code",
         pruned([{k: v for k, v in commitment.items() if k != "code"}]), True),
        ("R38-4-n a commitment witness with an unknown field",
         pruned([dict(commitment, commitmentWitnesses=[{"stage": "commitment", "code": "C30", "unexpected": 1}])]),
         True),
        ("R38-4-o a physical witness without vehicleId", pruned([dict(physical, physicalWitness={"code": "CAPACITY"})]),
         True),
        ("R38-4-p a pruned candidate with duplicate newRequestIds",
         pruned([dict(commitment, newRequestIds=["req-1", "req-1"])]), True),
        ("R38-4-P1 a valid omission (count 2, vehicleId, requestIds) with matching selection totals [positive]",
         valid_omission, False),
        ("R38-4-P2 two valid exogenous breaches in canonical order [positive]", breaches([b1, b2]), False),
        ("R38-4-P3 valid pruned candidates (commitment witness with every optional field; physical witness) "
         "[positive]", pruned([commitment, physical]), False),
    ]
    for name, change, expect in cases:
        grounded_case(T, f"{name} [frozen verifier agrees]", variant(base, change), expect)


def r38_5(c: Ctx) -> None:
    T, s = c.T, c.T.s
    site, roots = "E:/frozen/python/Lib/site-packages", ["E:/frozen/python"]

    def audit(lines):
        try:
            return s.pth_redirect_problems(lines, site, roots)
        except BaseException as error:  # noqa: B902
            return [f"exception escaped: {type(error).__name__}: {error}"]
    found = audit([("evil.pth", "../../../../outside")])
    check("R38-5-a Codex: path line ../../../../outside (site E:/frozen/python/Lib/site-packages, root "
          "E:/frozen/python) resolves to E:/outside and is refused", bool(found) and "escaped" not in str(found),
          str(found))
    found = audit([("ok.pth", "../extra-in-tree")])
    check("R38-5-b a relative path line that stays inside the root after resolution is accepted [positive]",
          not found, str(found))
    found = audit([("ok.pth", "E:/frozen/python/Lib/site-packages/pkg")])
    check("R38-5-c an absolute in-tree path line is accepted [positive]", not found, str(found))
    found = audit([("evil.pth", "import os; os.environ['SELFTEST'] = '1'")])
    check("R38-5-d an unreviewed executable import line is refused", bool(found) and "escaped" not in str(found),
          str(found))
    real = pathlib.Path(s.PYTHON_SITE) / "distutils-precedence.pth"
    line = next(text for text in real.read_text(encoding="utf-8").splitlines() if text.startswith("import"))
    found = audit([("distutils-precedence.pth", line)])
    check("R38-5-e the reviewed import line of distutils-precedence.pth (read only) is accepted [positive]",
          not found, f"line sha256 {sha(line.encode('utf-8'))[:16]}...; {found}")
    found = audit([("other.pth", line)])
    check("R38-5-f the same import line under another .pth name is refused (reviewed by file name and line)",
          bool(found), str(found))
    original = os.path.realpath

    def fake_realpath(path, *args, **kwargs):
        text = str(path).replace("\\", "/")
        if "/linked" in text:
            return "D:\\elsewhere" + text[text.index("/linked"):].replace("/", "\\")
        return original(path, *args, **kwargs)
    with patched(os.path, realpath=fake_realpath):
        found = audit([("link.pth", "linked/pkg")])
    check("R38-5-g a path line through a junction / symbolic link that leads outside is refused (resolution; realpath "
          "simulated in memory)", bool(found), str(found))
    if getattr(s, "pth_audit_problems", None) is None:
        say("   R38-5-h not applicable: this driver has no resolved two-directory .pth audit")
        return
    tree_roots = [pathlib.Path(root).as_posix() for _, root, _ in s.EXTERNAL_TREES]
    found = s.pth_audit_problems(tree_roots)
    check("R38-5-h read-only: the job interpreter's two site directories pass the resolved audit [positive]",
          not found, f"{[pathlib.Path(d).as_posix() for d in s.PTH_DIRS]}; {found}")


def r38_tools(c: Ctx) -> None:
    T, s = c.T, c.T.s
    check("R38-T-a every bare command name of the audited call graph is a required, frozen tool: git and taskkill "
          "(R38 s6)", set(getattr(s, "REQUIRED_TOOLS", ())) == {"git", "taskkill"}, str(getattr(s, "REQUIRED_TOOLS", None)))
    resolver = getattr(s, "process_resolve", None)
    if resolver is None:
        check("R38-T-b tools are resolved in the CreateProcess order that subprocess uses (application directory "
              "first)", False, "no CreateProcess resolver: v3 resolves with shutil.which on PATH only")
    else:
        present = {"X:/app/git.exe", "X:/path1/git.exe", "X:/cwd/taskkill.exe", "X:/win/System32/taskkill.exe",
                   "X:/path1/where.exe", "X:/win/System32/where.exe"}

        def is_file(path):
            return pathlib.Path(path).as_posix() in present
        env = {"PATH": "X:/path0;X:/path1", "SYSTEMROOT": "X:/win"}
        got = (resolver("git", environ=env, app_dir="X:/app", cwd="X:/cwd", is_file=is_file),
               resolver("taskkill", environ=env, app_dir="X:/app", cwd="X:/cwd", is_file=is_file),
               resolver("where", environ=env, app_dir="X:/app", cwd="X:/none", is_file=is_file),
               resolver("git", environ=env, app_dir="X:/none", cwd="X:/none", is_file=is_file),
               resolver("nosuch", environ=env, app_dir="X:/app", cwd="X:/cwd", is_file=is_file))
        want = ("X:/app/git.exe", "X:/cwd/taskkill.exe", "X:/win/System32/where.exe", "X:/path1/git.exe", None)
        check("R38-T-b CreateProcess order: application directory, current directory, System32 before PATH; '.exe' "
              "appended", got == want, str(got))
    found = s.forbidden_env({"GIT_EDITOR": "true", "PATH": "X:/p"})
    check("R38-T-c launch environment: a GIT_* variable (GIT_EDITOR, set by the agent shells) is refused, not only "
          "recorded", bool(found), str(found))
    check("R38-T-d launch environment: a PATH-only environment is not refused [positive]",
          not s.forbidden_env({"PATH": "X:/p"}), str(s.forbidden_env({"PATH": "X:/p"})))
    check("R38-T-e NoDefaultCurrentDirectoryInExePath is a frozen steering name (observed set in the agent shells on "
          "2026-10-13)", "NODEFAULTCURRENTDIRECTORYINEXEPATH" in getattr(s, "FROZEN_ENV_NAMES", ()),
          str(sorted(getattr(s, "FROZEN_ENV_NAMES", ()))))
    if T.version != "v4":
        say("   R38-T-f..m not applicable: this driver has no taskkill binding or CreateProcess resolver")
        return

    def after(change):
        def make(w):
            text = w.text()
            change(w)
            return text
        return make

    def plant(w):
        w.taskkill_at = "X:/repo/taskkill.exe"
        w.tool_bytes["X:/repo/taskkill.exe"] = b"planted"

    def outside_git(w):
        w.git_at = "X:/elsewhere/git.exe"
        w.tool_bytes["X:/elsewhere/git.exe"] = b"git elsewhere"
        return w.text()
    freeze_case(T, "R38-T-f taskkill resolving to another executable after the freeze (planted in the job cwd) is "
                   "detected", True, after(plant))
    freeze_case(T, "R38-T-g taskkill bytes changed at the frozen path are detected", True,
                after(lambda w: w.tool_bytes.__setitem__(w.taskkill_at, b"taskkill replaced")))
    freeze_case(T, "R38-T-h git resolving differently for the driver than for the jobs is detected", True,
                after(lambda w: setattr(w, "git_driver_at", "X:/tools/git/mingw64/bin/git.exe")))
    freeze_case(T, "R38-T-i a freeze without the taskkill line is rejected", True,
                lambda w: (setattr(w, "omit_taskkill", True), w.text())[1])
    freeze_case(T, "R38-T-j git (no exception) outside the frozen trees is refused even when frozen consistently",
                True, outside_git)
    freeze_case(T, "R38-T-k taskkill outside the frozen trees is accepted only as the declared OS cleanup exception, "
                   "bound by path and SHA-256 [positive]", False, lambda w: w.text())
    tree_roots = [pathlib.Path(root).as_posix() for _, root, _ in s.EXTERNAL_TREES]
    job, driver = s.tool_resolution("taskkill", "job"), s.tool_resolution("taskkill", "driver")
    check("R38-T-l read-only: taskkill resolves to C:/Windows/System32/taskkill.exe in both contexts here",
          job == driver == "C:/Windows/System32/taskkill.exe", f"job {job}; driver {driver}")
    job, driver = s.tool_resolution("git", "job"), s.tool_resolution("git", "driver")
    check("R38-T-m read-only: git resolves inside the frozen Git tree, identically in both contexts here",
          job is not None and job == driver and s._inside(job, tree_roots), f"job {job}; driver {driver}")


def r38_analysis(c: Ctx) -> None:
    T, s = c.T, c.T.s
    ident = dict(c.new_id, tree="t" * 64)
    gate0_ok, smoke_ok = verdict_text(T.gate_word, ident), verdict_text("SMOKE", ident)
    inspection = f"smokeVerdictSha256 {sha(smoke_ok.encode())}\ninspected-by: owner\ninspection: accepted\n"
    verdicts = dict(GATE0_PASSED=gate0_ok, SMOKE_PASSED=smoke_ok, INSPECTION=inspection)
    with environ(RB_ALLOW_BATTERY_MIN_PERCENT=None), driver_stubs(T, ident, **verdicts), \
            patched(s, ANALYSIS_FREEZE=FakeFile("stage1b-analysis-freeze.sha256")):
        problems = s.frozen_input_problems("main")
    check("R38-A-a `run main` is refused while the main-phase analysis code is not frozen "
          "(stage1b-analysis-freeze.sha256 absent; R38 s6)", any("analysis" in p for p in problems),
          f"problems {problems[:2]}" if problems else "admitted")
    check_fn = getattr(s, "analysis_freeze_problems", None)
    if check_fn is None:
        check("R38-A-b the analysis freeze is verified by content (hashed amendment)", False,
              "no analysis-freeze check in this driver")
    else:
        files = {"extract_stage1b.py": b"extract", "STAGE1B-ANALYSIS-AMENDMENT.md": b"amendment"}
        text = "".join(f"{sha(v)}  {k}\n" for k, v in sorted(files.items()))
        kw = dict(sha=lambda n: sha(files[n]), exists=lambda n: n in files)
        found = check_fn(text, **kw)
        check("R38-A-b a complete analysis freeze verifies [positive]", found == [], str(found))
        changed = dict(files, **{"extract_stage1b.py": b"extract edited after the freeze"})
        found = check_fn(text, sha=lambda n: sha(changed[n]), exists=lambda n: n in changed)
        check("R38-A-c an analysis file changed after its freeze is detected", bool(found), str(found))
        found = check_fn("# nothing listed\n", **kw)
        check("R38-A-d an analysis freeze that lists no file is refused", bool(found), str(found))
        found = check_fn(f"{sha(b'x')}  ../outside.py\n", **kw)
        check("R38-A-e a non-bare analysis path is refused", bool(found), str(found))
    changed_input = ["analysis input changed or missing: extract_stage1b.py"]
    st = run_main(c, "main", started=("gate0", "smoke", "main"), verdicts=verdicts,
                  analysis=lambda *a, **k: list(changed_input))
    check("R38-A-f an analysis input changed after the main phase started is persisted as drift at start-up",
          isinstance(st.get("exception"), SystemExit) and st["receipt"] is not None, describe(st))
    missing = ["refused: stage1b-analysis-freeze.sha256 does not exist"]
    st = run_main(c, "main", started=("gate0", "smoke"), verdicts=verdicts, analysis=lambda *a, **k: list(missing))
    check("R38-A-g before any main job a missing analysis freeze is refused without a drift receipt",
          isinstance(st.get("exception"), SystemExit) and st["receipt"] is None, describe(st))


def r38_plan(c: Ctx) -> None:
    T = c.T
    flat = " ".join(T.plan.read_text(encoding="utf-8").split())
    check("R38-P-a the plan records the owner facts of 2026-10-13 (deadline after 2026-11-03, exact date not given; A1 "
          "milestone reached; cost-first commit and runner v3 publication approved once Codex rates the harness READY)",
          all(x in flat for x in ("after 2026-11-03", "exact date was not given", "A1 milestone is reached",
                                  "approved committing the cost-first branch", "once Codex rates the harness READY")),
          T.plan.name)
    check("R38-P-b the plan records the three-week calculation and its start-date consequence",
          "2026-10-14" in flat and "21 days" in flat and "do not start" in flat, T.plan.name)
    check("R38-P-c the plan settles the launch environment (one fresh Windows PowerShell; GIT_* unset; the same "
          "recorded environment for freeze, run and verdict commands)", "Windows PowerShell" in flat
          and "GIT_* unset" in flat and "launch environment" in flat, T.plan.name)
    check("R38-P-d the plan declares the taskkill OS cleanup exception and the CreateProcess tool resolution",
          "taskkill" in flat and "OS cleanup exception" in flat and "CreateProcess" in flat, T.plan.name)
    check("R38-P-e the plan makes the analysis-code timing explicit and mechanical (stage1b-analysis-freeze.sha256 "
          "before any main job)", "stage1b-analysis-freeze.sha256" in flat and "before any main job" in flat,
          T.plan.name)


# ---------------------------------------------------------------- main

def main(argv: list[str]) -> int:
    version = "v4"
    if argv[:1] == ["--target"]:
        version, argv = argv[1], argv[2:]
    if version not in ("v4", "v3"):
        print(__doc__)
        return 2
    started = time.time()
    T = targets(version)
    say(f"# test_compare_equivalence_stage1b-v4.py --target {version} -- in-memory controls; archived inputs read only")
    for name in T.files + ("test_compare_equivalence_stage1b-v4.py", "canonical_wp4.py", "wp4-tier3-v2-costfirst.json"):
        say(f"#   tested file {name} sha256 {sha(source(name).read_bytes())}")
    say(f"#   fixture digest helper actual_fleetpy_medium_verify.py sha256 "
        f"{sha((ADAPTER / 'actual_fleetpy_medium_verify.py').read_bytes())} (frozen harness, read only)")
    c = Ctx(T)
    if version == "v4":
        say("## A. v1/v2 controls on the v4 comparator")
        section_a(c)
        say("## B. R24 finding controls")
        for part in (f1, f2, f3_driver, f5, f3_comparator, f4, f6):
            part(c)
        say("## C. R28 finding controls")
        for part in (n1, n2, n3, n4, n5, n6, n7, n8):
            part(c)
    say("## D. R38 finding controls")
    for part in (r38_1, r38_2, r38_3, r38_4, r38_5, r38_tools, r38_analysis, r38_plan):
        part(c)
    say(f"SELFTEST {'PASS' if not FAILURES else 'FAIL'} (--target {version}): {COUNTS['OK']} OK, {COUNTS['BAD']} BAD, "
        f"{COUNTS['LIMIT']} LIMIT; unexpected {FAILURES}; limits {LIMITS}; {time.time() - started:.0f} s")
    if argv:
        with open(argv[0], "x", encoding="utf-8", newline="\n") as handle:
            handle.write("\n".join(LOG) + "\n")
    return 0 if not FAILURES else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
