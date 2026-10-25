"""In-memory positive and negative controls for compare_equivalence_stage1b.py (Codex R22 §5.4,
optimize/codex-debate/codex-work/R22-CODE-REVIEW-CODEX.md:222) and for the WP4 canonicalization (R22 §6).

Data: two archived T3.8 runs, read only (C:/RideBoundData/research/tier3-t38-v1/U- and C-30-d20181116-s10-r1-w08),
the archived runner-v2 manifest and tree (read only), and synthetic runner trees built in memory. Every variant is a
modified copy of the archived bytes held in memory (a mutated frame is re-encoded and, unless the control is about
transport integrity, given a correct frame SHA-256/length and a consistent bundle-manifest, as a real run would have).
Nothing is simulated, built or published; nothing is written except the optional log (mode "x").

Pair controls (reference = archived U run; expected verdict in brackets):
  P1 identical copy [PASS]                      P2 only binarySha256 differs, verified new DLL [PASS]
  P3 consistent re-salt: runId, publication/breach IDs, all derived hashes, action and publicationIds order, plus
     a verified new DLL [PASS]                  P4 publicationIds and actions order permuted only [PASS]
  N1 new binarySha256 not the verified DLL [FAIL]          N2 new runner tree differs from its manifest [FAIL]
  N3 policyConfigurationHash of the cost-first config [FAIL]
  N3b policyConfigurationHash of v2 + explicit "stability-first" [FAIL]
  N4-N7 scenarioContentHash / graphSnapshotHash / travelTimeSnapshotHash / one event snapshotHash changed [FAIL]
  N8 coreCommitSha changed [FAIL]              N9 one decision time +1 ms [FAIL]
  N10 one route stop order swapped [FAIL]      N11 one final-ledger value +1 [FAIL]
  N12 one alight time +1 ms [FAIL]             N13 one publication missing from a certificate [FAIL]
  N14 one ledger publication reference replaced by an unknown ID [FAIL]
  N15 one decisionApplied hash changed (broken ACK link) [FAIL]
  N16 an unclassified hash field added [FAIL]  N17 frame bytes changed, frameSha256 kept [FAIL]
  N18 transcript changed, bundle-manifest kept [FAIL]      N19 summary status fail [FAIL]
  N20 the C-30 run of the same cell [FAIL]
Other checks: canonical WP4 hashes and bindings (C1), canonicalizer rejections (C2), runner-identity unit cases
(C3), archived runner v2 verifies against its manifest (R1), the four Gate-0 references have the R22 SHA-256 and
frame counts (R2) and pass every per-run precondition with the archived readers (R3).
Usage: python -B test_compare_equivalence_stage1b.py [<out-log>]
"""
from __future__ import annotations

import base64
import hashlib
import json
import pathlib
import sys
import time

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent.parent
sys.path.insert(0, str(HERE))
import canonical_wp4 as cj  # noqa: E402
import compare_equivalence_stage1b as ce  # noqa: E402

ARCHIVE = pathlib.Path(r"C:/RideBoundData/research/tier3-t38-v1")
OLD_RUNNER_ROOT = pathlib.Path(r"C:/RideBoundData/research/runner-tier3-v2")
OLD_MANIFEST = TANG3 / "runner-tier3-v2.sha256.tsv"
BASE = "U-d20181116-s10-r1-w08"     # U run with 94 exogenous breaches and 3 late pickups
OTHER = "C-30-d20181116-s10-r1-w08"
V2 = TANG3 / "configs" / "wp4-tier3-v2.json"
ON = HERE / "wp4-tier3-v2-costfirst.json"
U_BINDING = ce.BINDINGS[("off", "U")]
LOG: list[str] = []
FAILURES: list[str] = []


def say(text: str) -> None:
    LOG.append(text)
    print(text, flush=True)


def check(name: str, condition: bool, detail: str = "") -> None:
    say(f"{'OK ' if condition else 'BAD'} {name}" + (f" -- {detail}" if detail else ""))
    if not condition:
        FAILURES.append(name)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def read_files(directory: pathlib.Path) -> dict[str, bytes]:
    return {name: (directory / name).read_bytes()
            for name in ("bundle-manifest.json", "run-00.json", "summary.json", "transcript-00.ndjson")}


def split_lines(files: dict[str, bytes]) -> list[str]:
    lines = files["transcript-00.ndjson"].decode("utf-8").split("\n")
    assert lines[-1] == ""
    return lines[:-1]


def with_lines(files: dict[str, bytes], lines: list[str], rebuild_bundle: bool = True,
               summary: dict | None = None) -> dict[str, bytes]:
    out = dict(files)
    out["transcript-00.ndjson"] = ("\n".join(lines) + "\n").encode("utf-8")
    if summary is not None:
        out["summary.json"] = json.dumps(summary, sort_keys=True, separators=(",", ":")).encode("utf-8")
    if rebuild_bundle:
        bundle = json.loads(files["bundle-manifest.json"])
        for item in bundle["files"]:
            item["lengthBytes"], item["sha256"] = len(out[item["path"]]), sha(out[item["path"]])
        out["bundle-manifest.json"] = json.dumps(bundle, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return out


def decode(line: str):
    record = json.loads(line)
    return record["direction"], json.loads(base64.b64decode(record["frameBase64"]))


def encode(line: str, envelope: dict, fix_integrity: bool = True) -> str:
    record = json.loads(line)
    raw = json.dumps(envelope, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")
    record["frameBase64"] = base64.b64encode(raw).decode("ascii")
    if fix_integrity:
        record["frameSha256"], record["frameLengthBytes"] = sha(raw), len(raw)
    return json.dumps(record, sort_keys=True, separators=(",", ":"))


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


def main(argv: list[str]) -> int:
    started = time.time()
    say("# test_compare_equivalence_stage1b.py -- in-memory controls; archived inputs are read only")
    for name in ("compare_equivalence_stage1b.py", "canonical_wp4.py", "test_compare_equivalence_stage1b.py",
                 "wp4-tier3-v2-costfirst.json"):
        say(f"#   tested file {name} sha256 {sha((HERE / name).read_bytes())}")

    # ---------------- C1-C3: canonicalization and runner identity (pure)
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
    check("C1 explicit stability-first changes the binding (R22:50,232)", sf_binding != U_BINDING)
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
    new_tree = {"RideBound.Runner.dll": b"stage1b synthetic v3 Runner dll", "RideBound.Algorithms.dll": b"alg v3"}
    new_lines = tree_lines(new_tree)
    new_manifest = ce.tree_text(new_lines)
    new_id = ce.runner_identity(new_manifest, new_lines, "synthetic-v3")
    new_dll = sha(new_tree["RideBound.Runner.dll"])
    check("C3 synthetic runner verifies and reports its DLL", new_id["ok"] and new_id["dll"] == new_dll)
    tampered = tree_lines(dict(new_tree, **{"RideBound.Algorithms.dll": b"changed after the manifest"}))
    tampered_id = ce.runner_identity(new_manifest, tampered, "synthetic-v3-tampered")
    check("C3 tree differing from its manifest is refused", not tampered_id["ok"])
    check("C3 missing manifest is refused", not ce.runner_identity(None, new_lines, "x")["ok"])
    no_dll = tree_lines({"other.dll": b"x"})
    check("C3 tree without RideBound.Runner.dll is refused", not ce.runner_identity(ce.tree_text(no_dll), no_dll, "x")["ok"])
    other_id = ce.runner_identity(ce.tree_text(tree_lines({"RideBound.Runner.dll": b"another dll"})),
                                  tree_lines({"RideBound.Runner.dll": b"another dll"}), "synthetic-other")

    # ---------------- R1-R2: archived provenance (read only)
    old_text = OLD_MANIFEST.read_text(encoding="utf-8")
    old_id = ce.runner_identity_from_disk(OLD_RUNNER_ROOT, OLD_MANIFEST, "runner-tier3-v2")
    check("R1 archived runner-tier3-v2 tree == its manifest; DLL a631038b...",
          old_id["ok"] and old_id["dll"] == ce.OLD_RUNNER_DLL and old_id["tree"] == ce.OLD_RUNNER_TREE,
          f"tree {old_id['tree']} files {old_id['files']} {old_id['problems']}")
    if not old_id["ok"]:   # keep testing the comparator with the manifest as its own tree
        old_id = ce.runner_identity(old_text, old_text.split("\n")[1:-1], "runner-tier3-v2 (manifest)")
    for name, (want_sha, want_frames) in ce.REFERENCE_TRANSCRIPTS.items():
        data = (ARCHIVE / name / "transcript-00.ndjson").read_bytes()
        check(f"R2 {name} transcript SHA-256 and {want_frames} frames (R22:162-167)",
              sha(data) == want_sha and data.count(b"\n") == want_frames)
        del data

    # ---------------- R3: every reference passes the per-run preconditions (archived readers, real paths)
    for name in ce.REFERENCE_TRANSCRIPTS:
        run = ce.Run.load(ARCHIVE / name)
        run_facts = ce.facts(run, old_id["dll"])
        check(f"R3 {name}: integrity, links, audit, readers", not run_facts["problems"]
              and run_facts["table"] is not None, "; ".join(run_facts["problems"][:3]))
        del run, run_facts

    # ---------------- pair controls
    base_files = read_files(ARCHIVE / BASE)
    base_run = ce.Run(BASE, base_files)
    base_facts = ce.facts(base_run, old_id["dll"])
    check("P0 base facts have no precondition problem", not base_facts["problems"], str(base_facts["problems"][:3]))
    lines = split_lines(base_files)
    summary = json.loads(base_files["summary.json"])

    def judge(name: str, expected: bool, files: dict[str, bytes], identity: dict = old_id,
              binding: str = U_BINDING) -> None:
        run = ce.Run(f"{name}", files)
        run_facts = ce.facts(run, identity.get("dll") if identity.get("ok") else None)
        ok, report = ce.pair_report(base_run, base_facts, run, run_facts, binding, old_id, identity)
        reasons = [line.strip() for line in report if "FAIL" in line or "DIFFERENT" in line][:3]
        check(f"{name} [{'PASS' if expected else 'FAIL'} expected] -> {'PASS' if ok else 'FAIL'}", ok == expected,
              "; ".join(reasons) if reasons else "all components identical")

    init_index = 2
    decision_index = find(lines, is_kind("runnerToAdapter", "decision"), len(lines) // 2)
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
    checkpoint_index = find(lines, is_kind("runnerToAdapter", "checkpoint"), len(lines) - 6)
    say(f"   frames used: decision {decision_index + 1}, ack {applied_index + 1}, multi-publication {multi_index + 1}, "
        f"route {route_index + 1}, alight {alight_index + 1}, snapshot {snapshot_index + 1}, checkpoint {checkpoint_index + 1}")

    def set_manifest(key, value):
        def change(envelope):
            envelope["payload"]["manifest"][key] = value
        return change

    judge("P1 identical copy", True, with_lines(base_files, lines))
    judge("P2 only binarySha256 differs, verified new DLL", True,
          with_lines(base_files, edit(lines, init_index, set_manifest("binarySha256", new_dll))), new_id)

    # P3: consistent re-salt of every salted identity and derived hash, with salted order permuted
    _, final_envelope = decode(lines[checkpoint_index])
    state = final_envelope["payload"]["content"]["onlineState"]
    mapping = {}
    for history in state["commitmentLedger"]:
        for entry in history["entries"]:
            mapping[entry["publicationId"]] = "promise-" + sha(b"resalt" + entry["publicationId"].encode())
    for record in state["incidentLedger"]["breaches"]:
        prefix = record["breachId"].split("-", 1)[0]
        mapping[record["breachId"]] = prefix + "-" + sha(b"resalt" + record["breachId"].encode())
    _, init_envelope = decode(lines[init_index])
    mapping[init_envelope["runId"]] = "wp7-medium-resalted-label-s1b"

    def collect(value):
        if isinstance(value, dict):
            for key, item in value.items():
                if key in ce.DERIVED_HASH_KEYS and item != ce.ZERO_HASH:
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
        if index == init_index:
            envelope["payload"]["manifest"]["binarySha256"] = new_dll
        resalted.append(encode(line, envelope))
    judge("P3 consistent re-salt (runId, publication/breach IDs, derived hashes, orders) + verified new DLL", True,
          with_lines(base_files, resalted), new_id)
    del resalted

    def permute(envelope):
        body = envelope["payload"]["certificate"]["body"]
        body["publicationIds"] = list(reversed(body["publicationIds"]))
        envelope["payload"]["actions"] = list(reversed(envelope["payload"]["actions"]))
    judge("P4 publicationIds and actions order permuted only", True, with_lines(base_files, edit(lines, multi_index, permute)))

    judge("N1 binarySha256 of an unverified DLL", False,
          with_lines(base_files, edit(lines, init_index, set_manifest("binarySha256", new_dll))), other_id)
    judge("N2 new runner tree differs from its manifest", False,
          with_lines(base_files, edit(lines, init_index, set_manifest("binarySha256", new_dll))), tampered_id)
    judge("N3 policyConfigurationHash of the cost-first config", False,
          with_lines(base_files, edit(lines, init_index, set_manifest("policyConfigurationHash", ce.BINDINGS[("on", "U")]))))
    judge("N3b policyConfigurationHash of v2 + explicit stability-first", False,
          with_lines(base_files, edit(lines, init_index, set_manifest("policyConfigurationHash", sf_binding))))
    for key in ("scenarioContentHash", "graphSnapshotHash", "travelTimeSnapshotHash"):
        judge(f"N4-6 {key} changed", False,
              with_lines(base_files, edit(lines, init_index, set_manifest(key, sha(key.encode())))))

    def snapshot(envelope):
        event = next(ev for ev in envelope["payload"]["events"] if ev["eventType"] == "travelTimesUpdated")
        event["payload"]["snapshot"]["snapshotHash"] = sha(b"other snapshot")
    judge("N7 one event snapshotHash changed", False, with_lines(base_files, edit(lines, snapshot_index, snapshot)))
    judge("N8 coreCommitSha changed", False,
          with_lines(base_files, edit(lines, init_index, set_manifest("coreCommitSha", "f" * 40))))

    def later(envelope):
        envelope["simTimeMs"] += 1
    judge("N9 one decision time +1 ms", False, with_lines(base_files, edit(lines, decision_index, later)))

    def swap(envelope):
        action = next(a for a in envelope["payload"]["actions"] if a["decisionType"] == "vehiclePlanUpdated"
                      and len(a["payload"]["route"]["mutableSuffix"]) >= 2)
        suffix = action["payload"]["route"]["mutableSuffix"]
        suffix[0], suffix[1] = suffix[1], suffix[0]
    judge("N10 one route stop order swapped", False, with_lines(base_files, edit(lines, route_index, swap)))

    def ledger_value(envelope):
        envelope["payload"]["content"]["onlineState"]["commitmentLedger"][0]["entries"][-1]["budgetAfter"][
            "dropEtaTotalMs"] += 1
    judge("N11 one final-ledger value +1", False, with_lines(base_files, edit(lines, checkpoint_index, ledger_value)))
    judge("N12 one alight time +1 ms", False, with_lines(base_files, edit(lines, alight_index, later)))

    def drop_publication(envelope):
        envelope["payload"]["certificate"]["body"]["publicationIds"].pop()
    judge("N13 one publication missing from a certificate", False,
          with_lines(base_files, edit(lines, multi_index, drop_publication)))

    def unknown_publication(envelope):
        envelope["payload"]["content"]["onlineState"]["commitmentLedger"][0]["entries"][0]["publicationId"] = \
            "promise-" + "1" * 64
    judge("N14 a ledger publication reference replaced by an unknown ID", False,
          with_lines(base_files, edit(lines, checkpoint_index, unknown_publication)))

    def broken_ack(envelope):
        envelope["payload"]["decisionHash"] = sha(b"not the decision")
    judge("N15 one decisionApplied hash changed", False, with_lines(base_files, edit(lines, applied_index, broken_ack)))

    def unclassified(envelope):
        envelope["payload"]["candidateSetHash"] = "ab" * 32
    judge("N16 an unclassified hash field added", False, with_lines(base_files, edit(lines, decision_index, unclassified)))

    def reason(envelope):
        envelope["payload"]["reasonCode"] = "TAMPERED"
    judge("N17 frame bytes changed, frameSha256 kept", False,
          with_lines(base_files, edit(lines, decision_index, reason, fix_integrity=False)))
    judge("N18 transcript changed, bundle-manifest kept", False,
          with_lines(base_files, edit(lines, decision_index, later), rebuild_bundle=False))
    judge("N19 summary status fail", False, with_lines(base_files, lines, summary=dict(summary, status="fail")))
    other = ce.Run.load(ARCHIVE / OTHER)
    other_facts = ce.facts(other, old_id["dll"])
    ok, report = ce.pair_report(base_run, base_facts, other, other_facts, U_BINDING, old_id, old_id)
    check("N20 C-30 run of the same cell [FAIL expected] -> " + ("PASS" if ok else "FAIL"), not ok,
          "; ".join(line.strip() for line in report if "FAIL" in line or "DIFFERENT" in line)[:300])

    say(f"SELFTEST {'PASS' if not FAILURES else 'FAIL'}: {len(FAILURES)} unexpected result(s) {FAILURES}; "
        f"{time.time() - started:.0f} s")
    if argv:
        with open(argv[0], "x", encoding="utf-8", newline="\n") as handle:
            handle.write("\n".join(LOG) + "\n")
    return 0 if not FAILURES else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
