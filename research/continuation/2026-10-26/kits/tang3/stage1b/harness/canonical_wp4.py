"""Canonical JSON and WP4 policy binding, mirrored from the Runner source (read-only reference: worktree
C:/Code/RideBound-costfirst at 761915c + the uncommitted cost-first diff c48042af...).

C# rules mirrored here (file:line in that worktree):
  src/RideBound.Contracts/Serialization/CanonicalJson.cs
    :39-43   strict parse, no trailing commas, no comments
    :120-124 null is rejected
    :145-151 a duplicate property is rejected
    :156-158 properties sorted with StringComparer.Ordinal (UTF-16 code units)
    :160-175 compact object: '{' "name" ':' value (',' ...) '}'; arrays :89-104 the same with '[' ']'
    :178-201 numbers: integers only; raw text with '.', 'e', 'E' or "-0" is rejected; range checked
    :203-225 an unpaired UTF-16 surrogate is rejected
    :247-303 strings: \\" \\\\ \\b \\t \\n \\f \\r escaped; other code points <= 0x1F as \\u00xx (lowercase
             hex, :305-319); everything else written as raw UTF-8
  src/RideBound.Contracts/Protocol/ProtocolPrimitives.cs:8,10  integer range +-9_007_199_254_740_991
  src/RideBound.Runner/Configuration/Wp4RunnerConfiguration.cs:245-248  WP4 content hash = SHA-256(canonical bytes)
  src/RideBound.Runner/Configuration/CommitmentPolicyConfiguration.cs:69-72  commitment content hash, same rule
  src/RideBound.Contracts/Protocol/Wp4PolicyConfigurationBinding.cs:19-20,31-45  binding =
      SHA-256("RideBound.Wp4RunnerConfigurationBinding.v1\\0" || commitment digest bytes || WP4 digest bytes)
The FleetPy adapter computes the same binding in Python (E:/Code/RideBound-deadline/simulators/fleetpy-ridebound/
ridebound_fleetpy/mapping.py:130-158: json.dumps(sort_keys, separators=(",", ":"), ensure_ascii=False));
adapter_canonical_bytes() mirrors that second implementation so the two can be cross-checked.
Pure functions; no file is written.
"""
from __future__ import annotations

import hashlib
import json

MAX_CANONICAL_INTEGER = 9_007_199_254_740_991
BINDING_DOMAIN = b"RideBound.Wp4RunnerConfigurationBinding.v1\0"


class CanonicalJsonError(ValueError):
    pass


def _reject_constant(name: str):
    raise CanonicalJsonError(f"non-finite number {name} is not canonical")


def _reject_float(text: str):
    raise CanonicalJsonError(f"number {text} is not a canonical integer")


def _parse_int(text: str) -> int:
    if text == "-0":
        raise CanonicalJsonError("-0 is not a canonical integer")
    value = int(text)
    if not -MAX_CANONICAL_INTEGER <= value <= MAX_CANONICAL_INTEGER:
        raise CanonicalJsonError(f"integer {text} outside the canonical safe range")
    return value


def _pairs(pairs):
    out = {}
    for key, value in pairs:
        if key in out:
            raise CanonicalJsonError(f"duplicate property {key!r}")
        out[key] = value
    return out


def parse_strict(raw: bytes):
    if raw.startswith(b"\xef\xbb\xbf"):
        raise CanonicalJsonError("byte order mark is not accepted here")
    try:
        return json.loads(raw.decode("utf-8"), object_pairs_hook=_pairs, parse_float=_reject_float,
                          parse_int=_parse_int, parse_constant=_reject_constant)
    except json.JSONDecodeError as error:
        raise CanonicalJsonError(f"malformed JSON: {error}") from error


def _check_unicode(text: str) -> None:
    for character in text:
        if 0xD800 <= ord(character) <= 0xDFFF:
            raise CanonicalJsonError("unpaired UTF-16 surrogate")


def _string(text: str) -> str:
    _check_unicode(text)
    named = {'"': '\\"', "\\": "\\\\", "\b": "\\b", "\t": "\\t", "\n": "\\n", "\f": "\\f", "\r": "\\r"}
    out = []
    for character in text:
        if character in named:
            out.append(named[character])
        elif ord(character) <= 0x1F:
            out.append(f"\\u{ord(character):04x}")
        else:
            out.append(character)
    return '"' + "".join(out) + '"'


def _write(value) -> str:
    if value is None:
        raise CanonicalJsonError("null is not permitted")
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, str):
        return _string(value)
    if isinstance(value, list):
        return "[" + ",".join(_write(item) for item in value) + "]"
    if isinstance(value, dict):
        keys = sorted(value, key=lambda name: name.encode("utf-16-be"))   # StringComparer.Ordinal
        return "{" + ",".join(_string(key) + ":" + _write(value[key]) for key in keys) + "}"
    raise CanonicalJsonError(f"unsupported value {type(value).__name__}")


def canonical_bytes(raw: bytes) -> bytes:
    """C# CanonicalJson.Canonicalize, for the JSON subset used by RideBound configuration files."""
    return _write(parse_strict(raw)).encode("utf-8")


def canonical_bytes_of_value(value) -> bytes:
    return _write(value).encode("utf-8")


def adapter_canonical_bytes(raw: bytes) -> bytes:
    """The adapter's second implementation (mapping.py:130-147), for cross-checking."""
    return json.dumps(parse_strict(raw), ensure_ascii=False, allow_nan=False, sort_keys=True,
                      separators=(",", ":")).encode("utf-8")


def canonical_sha256(raw: bytes) -> str:
    return hashlib.sha256(canonical_bytes(raw)).hexdigest()


def binding(commitment_raw: bytes, wp4_raw: bytes) -> str:
    return binding_from_hashes(canonical_sha256(commitment_raw), canonical_sha256(wp4_raw))


def binding_from_hashes(commitment_sha256: str, wp4_sha256: str) -> str:
    return hashlib.sha256(BINDING_DOMAIN + bytes.fromhex(commitment_sha256) + bytes.fromhex(wp4_sha256)).hexdigest()
