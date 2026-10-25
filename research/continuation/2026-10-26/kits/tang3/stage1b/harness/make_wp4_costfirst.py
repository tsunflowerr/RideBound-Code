"""Stage 1b ON configuration: wp4-tier3-v2.json plus exactly one root property,
    "selectionObjectiveOrder": "cost-first"
(Codex R22 §6, optimize/codex-debate/codex-work/R22-CODE-REVIEW-CODEX.md:226-240). Made the way make_wp4_v2.py made v2
(tang3/configs/make_wp4_v2.py:1-23): a byte-level edit of the v2 file, so the formatting stays identical (LF, no BOM,
two-space indent). The new line is inserted right after the `"lateBoarding": "record-v1",` line.

Proofs printed (every one is an assert; nothing is written unless all pass):
  1. the v2 file is the archived one (file SHA-256 66e9b592...4518, run_w07.py:43);
  2. parsed ON has exactly one extra root key, with value "cost-first"; every other key/value is equal;
  3. removing that key from parsed ON gives canonical bytes identical to canonical v2 (C# rules, canonical_wp4.py);
  4. the C# mirror and the adapter's json.dumps canonicalizer give the same bytes for v2 and ON;
  5. canonical SHA-256: v2 = fb2813c4...cd42 (archived, R22:122-124); ON is printed and compared with Codex's derived
     16dcb7e3...3140 (R22:236);
  6. bindings with the archived commitment files U.json / C-30.json: OFF = 251b331e... / a2f11a3a... (archived,
     R22:126-130), ON = compared with Codex's 90b290d7... / e6b33be5... (R22:237-238).
Writes wp4-tier3-v2-costfirst.json next to this script with mode "xb" (never overwrites).
Usage: python -B make_wp4_costfirst.py
"""
import hashlib
import json
import pathlib
import sys

HERE = pathlib.Path(__file__).resolve().parent
TANG3 = HERE.parent.parent
sys.path.insert(0, str(HERE))
import canonical_wp4 as cj  # noqa: E402

V2 = TANG3 / "configs" / "wp4-tier3-v2.json"
TARGET = HERE / "wp4-tier3-v2-costfirst.json"
V2_FILE_SHA256 = "66e9b59249d9491d520f3ed592e8c878a0d3b100e39432d5f4b73321617b4518"
V2_CANONICAL = "fb2813c4036d309490e12df77e37de1683b92b92a9ebdd71e16cad35d50ecd42"
CODEX_ON_CANONICAL = "16dcb7e3337763aab7889f7528365dced2aa49028db6dbf390f8b3dce79b3140"
COMMITMENT = {"U": ("998a0a94e7865c3b6e890f1bed9bf27f7f6c9e0de1f73d02a349ddc3de91c273",
                    "7fc79a433abe1796830ecceeac280da0e0af3871daf268dda8d6fc93b270370d"),
              "C-30": ("d6124f3f964d8385db381d53b75c142cf2ac870b22823d6675325c3808808beb",
                       "f07395f5f2c967e0d89cceb4a55b23c4f896971db071d8c7876f05e20ef09884")}
OFF_BINDING = {"U": "251b331eb56fe870e734d651bf6937e95304919ed92967b2f3b0d0b9da1ac346",
               "C-30": "a2f11a3ada0773f9b30b85c56c935058e2e931f13c907f46b2ddfff8c1285f1c"}
CODEX_ON_BINDING = {"U": "90b290d7e63f6885ac8e93e18017d55dddc54c27a05d8476a65dcf7d802e2df5",
                    "C-30": "e6b33be5649dc2b1208b42b90cef227d00721cba59e4340fdbc4e96f21796f76"}
ANCHOR = b'  "lateBoarding": "record-v1",\n'
INSERT = b'  "selectionObjectiveOrder": "cost-first",\n'


def main() -> int:
    source = V2.read_bytes()
    assert hashlib.sha256(source).hexdigest() == V2_FILE_SHA256, "wp4-tier3-v2.json is not the archived file"
    assert b"\r" not in source and not source.startswith(b"\xef\xbb\xbf"), "v2 is expected LF-only without BOM"
    assert source.count(ANCHOR) == 1, "expected exactly one lateBoarding line"
    assert b"selectionObjectiveOrder" not in source
    target = source.replace(ANCHOR, ANCHOR + INSERT)
    a, b = cj.parse_strict(source), cj.parse_strict(target)
    extra = sorted(set(b) - set(a))
    assert extra == ["selectionObjectiveOrder"] and b["selectionObjectiveOrder"] == "cost-first", extra
    assert sorted(set(a) - set(b)) == [] and all(a[k] == b[k] for k in a), "another root value changed"
    stripped = dict(b)
    del stripped["selectionObjectiveOrder"]
    v2_canonical = cj.canonical_bytes(source)
    assert cj.canonical_bytes_of_value(stripped) == v2_canonical, "removing the key does not restore canonical v2"
    on_canonical = cj.canonical_bytes(target)
    assert cj.adapter_canonical_bytes(source) == v2_canonical, "C# mirror and adapter canonicalizer disagree (v2)"
    assert cj.adapter_canonical_bytes(target) == on_canonical, "C# mirror and adapter canonicalizer disagree (ON)"
    v2_sha, on_sha = hashlib.sha256(v2_canonical).hexdigest(), hashlib.sha256(on_canonical).hexdigest()
    assert v2_sha == V2_CANONICAL, f"canonical v2 {v2_sha} differs from the archived value"
    lines = [f"wp4-tier3-v2.json            file {hashlib.sha256(source).hexdigest()}  canonical {v2_sha}",
             f"wp4-tier3-v2-costfirst.json  file {hashlib.sha256(target).hexdigest()}  canonical {on_sha}",
             f"differs only at root key: {extra} = {b['selectionObjectiveOrder']!r}; "
             f"removing it restores canonical v2 bytes: True",
             f"C# mirror == adapter json.dumps canonical bytes (v2 and ON): True",
             f"ON canonical equals Codex R22 derivation 16dcb7e3...: {on_sha == CODEX_ON_CANONICAL}"]
    for arm, (file_sha, canonical_sha) in COMMITMENT.items():
        raw = (TANG3 / "configs" / f"{arm}.json").read_bytes()
        assert hashlib.sha256(raw).hexdigest() == file_sha, f"{arm}.json is not the archived file"
        assert cj.canonical_sha256(raw) == canonical_sha, f"{arm}.json canonical hash differs"
        off, on = cj.binding(raw, source), cj.binding(raw, target)
        assert off == OFF_BINDING[arm], f"OFF binding for {arm} differs from the archived value"
        lines.append(f"{arm:<5} commitment canonical {canonical_sha}  OFF binding {off} (archived: True)  "
                     f"ON binding {on} (equals Codex: {on == CODEX_ON_BINDING[arm]})")
    with open(TARGET, "xb") as handle:
        handle.write(target)
    print("\n".join(lines))
    return 0 if on_sha == CODEX_ON_CANONICAL and all(
        cj.binding((TANG3 / "configs" / f"{arm}.json").read_bytes(), target) == CODEX_ON_BINDING[arm]
        for arm in COMMITMENT) else 1


if __name__ == "__main__":
    raise SystemExit(main())
