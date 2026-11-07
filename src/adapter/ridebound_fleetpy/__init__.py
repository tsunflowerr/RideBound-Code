"""RideBound-owned FleetPy adapter boundary."""

from .errors import AdapterFailure
from .mapping import (
    CanonicalIdRegistry,
    FleetPyProtocolMapper,
    canonical_json_bytes,
    canonical_json_file_hash,
    seconds_to_milliseconds,
    wp4_policy_binding_hash,
)
from .runner_client import RunnerClient
from .protocol import OrderedEventBuffer, ParsedDecision, parse_decision
from .session import RideBoundProtocolSession, RideBoundSessionSettings

__all__ = [
    "AdapterFailure",
    "CanonicalIdRegistry",
    "FleetPyProtocolMapper",
    "canonical_json_bytes",
    "canonical_json_file_hash",
    "seconds_to_milliseconds",
    "RunnerClient",
    "OrderedEventBuffer",
    "ParsedDecision",
    "parse_decision",
    "RideBoundProtocolSession",
    "RideBoundSessionSettings",
    "wp4_policy_binding_hash",
]
