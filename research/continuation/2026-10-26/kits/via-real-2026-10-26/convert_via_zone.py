"""Convert one company Via P512 cell into a RideBound/FleetPy fixture at TLC-zone resolution (research converter v1).

WHY (owner order 2026-10-26: "Chuẩn bị mọi thứ cho việc chạy bộ dữ liệu thật trên Via")
-----------------------------------------------------------------------------------------
The real RideBound job (FleetPy + adapter + C# Runner) materializes a COMPLETE directed graph over the scenario
nodes and checks every arc against FleetPy's own router before the first event
(actual_fleetpy_medium_preflight.py:113-155, :437-447), and every traffic bin sends a complete snapshot of all node
pairs to the Runner. A company Via cell has 824-858 nodes (about 680,000 arcs), so the job cannot start in practice.
TLC Via trips are recorded only by taxi zone; the company placed each trip end on a FleetPy node sampled inside its
zone (cell provenance). This converter maps every node to ONE representative node of its zone, which keeps the full
demand (512 requests minus intra-zone trips) and the full fleet (62 vehicles) on about 60 nodes.

The result is a NEW scenario family ("vz"), not the company cell: travel times change because trip ends move to the
zone representative. It tests RideBound against the emulator at Via scale (requests, vehicles, density, G2 with 8 of
62 receivers); it does not reproduce the P5 numbers.

RULES (fixed before any emulator prediction or RideBound job)
-------------------------------------------------------------
* Source: one P5 plain cell (company-identical) and its int32 little-endian ms matrix (scenario.js:108-111).
* Fleet: the first 62 vehicles of the cell (company runs: nVeh 62, scenario.js:222, shlib.js:16).
* Zone representative: among the cell's nodes with that zone label, the node with the smallest sum of travel times to
  and from the other nodes of the zone; ties -> smaller FleetPy node index. Only zones used by a request end or a
  vehicle start are kept.
* Travel times: the representative sub-matrix, then an integer Floyd-Warshall closure (the job checks that FleetPy's
  shortest path equals every arc). The number of closed entries is reported.
* Requests: both ends mapped to their zone representative; a request whose two ends map to the same node is dropped
  and listed in the selection frame. Times: arrival = earliest pickup = the cell's arrivalMs (must be whole seconds);
  latest pickup = arrival + the cell's pickupWindowMs; max ride = max(direct, ties-to-even(direct * 2000 / 1000))
  (company runs: maxRidePermille 2000; normalizer_emul.js:32-43). Party size 1.
* Identifiers: node "node-%06d" of the FleetPy node index; request "req-" + first 32 hex of
  sha256("ridebound-via-zone-v1\\0" + Via request id); vehicles "veh-%03d" in the cell's order.
* Ordering as in the normalizer fixtures: requests by requestId, events by (arrival, requestId), arcs by
  (fromNodeId, toNodeId).
* Traffic: the cell's 15-minute factor profile (TLC-MP15) as an explicit world-v1 base (preflight_world.py:59-60,
  :106-109); sigma 0, no shocks; held after the last bin.
* Hash fields the Runner does not read (snapshotHash, scenarioHash, invariantSetHash) follow this converter's own
  domain-separated rule, NOT the C# normalizer's; the C# normalizer and its 128/32/96 bound are not used or changed.

Usage: python -I convert_via_zone.py <p5 plain cell json> <out root> <cell name dYYYYMMDD-vz-rN-wHH>
Writes <out root>/fixtures/<cell>/, <out root>/drivers/<cell>.driver.json, <out root>/worlds/<cell>.world.json.
Never overwrites. Prints a JSON summary.
"""

from __future__ import annotations

import datetime
import hashlib
import json
import pathlib
import re
import struct
import sys

CONVERTER_ID = "via-zone-research-converter"
CONVERTER_VERSION = "1.0.0"
NVEH = 62
PERMILLE = 2000
SERVICE_CLASS = "fleetpy-tlc-public-derivative-v1"          # asserted by the preflight (:182-190)
POLICY_ID = "wp6-synthetic-policy-overlay-v1"                # asserted by the preflight (:182-190)
HORIZON_MS = 7_200_000
DRAIN_END_MS = 14_400_000
CELL_NAME = re.compile(r"^d(\d{4})(\d{2})(\d{2})-vz-r\d-w(08|17)$")
EST = datetime.timedelta(hours=-5)  # New York standard time; DST 2019 ran 10 Mar - 3 Nov (checked below)


def sha_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def compact(value) -> bytes:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def ties_to_even(value: int, numerator: int, denominator: int) -> int:
    q, r = divmod(value * numerator, denominator)
    if 2 * r > denominator or (2 * r == denominator and q % 2 == 1):
        q += 1
    return q


def write_new(path: pathlib.Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "xb") as handle:
        handle.write(data)


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__)
        return 2
    source_path, out_root, cell = pathlib.Path(argv[0]), pathlib.Path(argv[1]), argv[2]
    match = CELL_NAME.match(cell)
    if not match:
        raise SystemExit(f"cell name {cell!r} must look like dYYYYMMDD-vz-rN-wHH")
    raw = source_path.read_bytes()
    src = json.loads(raw.decode("utf-8"))
    if src.get("format") != "ridebound-screen-scenario/v1":
        raise SystemExit("not a screen scenario v1")
    day = datetime.date(int(match.group(1)), int(match.group(2)), int(match.group(3)))
    window_hour = int(match.group(4))
    if not (day < datetime.date(2019, 3, 10) or day > datetime.date(2019, 11, 3)):
        raise SystemExit(f"{day} is in daylight saving time; the fixed UTC-5 offset would be wrong")
    if src["family"] != f"via-{day.isoformat()}" or f"-w{window_hour:02d}-" not in src["scenarioId"]:
        raise SystemExit(f"cell name {cell} does not match the source {src['scenarioId']}")

    # ---------------------------------------------------------------- source matrix and nodes
    nodes = src["nodes"]
    n_src = len(nodes)
    matrix_path = (source_path.parent / src["travel"]["base"]["matrixFile"]).resolve()
    matrix_bytes = matrix_path.read_bytes()
    if len(matrix_bytes) != n_src * n_src * 4:
        raise SystemExit("matrix size")
    T = struct.unpack(f"<{n_src * n_src}i", matrix_bytes)
    index = {node["id"]: i for i, node in enumerate(nodes)}
    zone_of = {node["id"]: node["label"] for node in nodes}
    fleet_src = src["fleet"][:NVEH]
    if len(fleet_src) != NVEH:
        raise SystemExit("fewer than 62 vehicles")
    service = src["service"]
    if service["dwellMs"] != 0 or service["maxRidePlusMs"] != 0:
        raise SystemExit("unexpected service settings")
    pickup_window = int(service["pickupWindowMs"])

    used_zones = {zone_of[q["origin"]] for q in src["requests"]} | {zone_of[q["destination"]] for q in src["requests"]}
    used_zones |= {zone_of[v["startNode"]] for v in fleet_src}

    def fleetpy_index(node_id: str) -> int:
        m = re.fullmatch(r"n(\d+)", node_id)
        if not m:
            raise SystemExit(f"bad node id {node_id}")
        return int(m.group(1))

    representative = {}
    for zone in sorted(used_zones):
        members = [node["id"] for node in nodes if node["label"] == zone]
        best = min(members, key=lambda a: (sum(T[index[a] * n_src + index[b]] + T[index[b] * n_src + index[a]]
                                               for b in members), fleetpy_index(a)))
        representative[zone] = best
    reps = sorted(set(representative.values()), key=fleetpy_index)
    if len(reps) != len(representative):
        raise SystemExit("two zones share a representative")
    rid = {r: f"node-{fleetpy_index(r):06d}" for r in reps}
    order = sorted(reps, key=lambda r: rid[r])           # string order = the preflight's node index
    n = len(order)
    M = [[T[index[a] * n_src + index[b]] for b in order] for a in order]
    for i in range(n):
        for j in range(n):
            if (i == j) != (M[i][j] == 0) or M[i][j] < 0:
                raise SystemExit(f"bad sub-matrix entry {i},{j}={M[i][j]}")
    closed = 0
    for k in range(n):
        Mk = M[k]
        for i in range(n):
            via = M[i][k]
            Mi = M[i]
            for j in range(n):
                if via + Mk[j] < Mi[j]:
                    Mi[j] = via + Mk[j]
                    closed += 1
    pos = {r: i for i, r in enumerate(order)}

    # ---------------------------------------------------------------- requests
    requests, events, frame = [], [], []
    dropped = 0
    for ordinal, q in enumerate(src["requests"]):
        o_rep, d_rep = representative[zone_of[q["origin"]]], representative[zone_of[q["destination"]]]
        arrival = int(q["arrivalMs"])
        if arrival % 1000:
            raise SystemExit(f"{q['id']}: arrival not whole seconds")
        if not 0 <= arrival < HORIZON_MS:
            raise SystemExit(f"{q['id']}: arrival outside the horizon")
        request_id = "req-" + sha_bytes(b"ridebound-via-zone-v1\0" + q["id"].encode("utf-8"))[:32]
        if o_rep == d_rep:
            dropped += 1
            frame.append({"disposition": "excludedIntraZone", "sourceRecordOrdinal": ordinal, "viaRequestId": q["id"],
                          "zone": zone_of[q["origin"]]})
            continue
        frame.append({"disposition": "selected", "requestId": request_id, "sourceRecordOrdinal": ordinal,
                      "viaRequestId": q["id"]})
        direct = M[pos[o_rep]][pos[d_rep]]
        payload = {
            "arrivalTimeMs": arrival,
            "commitmentPolicyId": POLICY_ID,
            "destinationNodeId": rid[d_rep],
            "earliestPickupMs": arrival,
            "latestPickupMs": arrival + pickup_window,
            "maxRideTimeMs": max(direct, ties_to_even(direct, PERMILLE, 1000)),
            "originNodeId": rid[o_rep],
            "partySize": 1,
            "requestId": request_id,
            "serviceClass": SERVICE_CLASS,
        }
        payload_bytes = compact({"request": payload})
        requests.append({**payload, "policyObservationClass": "syntheticPolicyOverlay",
                         "sourceProvenanceId": f"via-request-{ordinal:05d}", "sourceRecordOrdinal": ordinal})
        events.append({
            "eventType": "requestArrived",
            "payloadCanonicalJsonHex": payload_bytes.hex(),
            "payloadSha256": sha_bytes(payload_bytes),
            "simTimeMs": arrival,
            "sourceProvenanceId": f"via-request-{ordinal:05d}",
            "sourceRecordOrdinal": ordinal,
            "sourceSequencePreserved": False,
            "stableSubjectId": request_id,
        })
    if len({r["requestId"] for r in requests}) != len(requests):
        raise SystemExit("request id collision")
    requests.sort(key=lambda r: r["requestId"])
    events.sort(key=lambda e: (e["simTimeMs"], e["stableSubjectId"]))
    for sequence, event in enumerate(events, start=1):
        event["eventSequence"] = sequence

    fleet = []
    for i, vehicle in enumerate(fleet_src):
        fleet.append({
            "acceptedRequestIds": [],
            "capacity": int(vehicle["capacity"]),
            "initialRoute": {"executedStopCount": 0, "frozenPrefix": [], "mutableSuffix": [], "planVersion": 0},
            "occupiedSeats": 0,
            "onboardRequestIds": [],
            "position": {"kind": "node", "nodeId": rid[representative[zone_of[vehicle["startNode"]]]]},
            "sourceProvenanceId": f"via-fleet-{vehicle['id']}",
            "vehicleId": f"veh-{i:03d}",
        })
        if fleet[-1]["capacity"] != 4:
            raise SystemExit("capacity")

    arcs = [{"fromNodeId": rid[a], "toNodeId": rid[b], "travelTimeMs": M[i][j]}
            for i, a in enumerate(order) for j, b in enumerate(order) if i != j]
    snapshot_hash = sha_bytes(b"RideBound.ViaZoneConverter.Snapshot.v1\0" + compact({"arcs": arcs, "version": 1}))

    # ---------------------------------------------------------------- side files
    start_seconds = window_hour * 3600
    start_utc = datetime.datetime.combine(day, datetime.time(window_hour)) - EST
    end_utc = start_utc + datetime.timedelta(seconds=7200)
    utc = lambda t: t.strftime("%Y-%m-%dT%H:%M:%S.0000000Z")  # noqa: E731
    converter_sha = sha_bytes(pathlib.Path(__file__).read_bytes())
    scenario_id = f"via-zone-v1-{cell}"
    norm_config = {
        "commitmentPolicyId": POLICY_ID,
        "converterId": CONVERTER_ID,
        "converterVersion": CONVERTER_VERSION,
        "drainDurationMs": DRAIN_END_MS - HORIZON_MS,
        "intraZoneRule": "drop-v1",
        "matrixClosure": "integer-floyd-warshall-ms-v1",
        "maximumNodeCount": n,
        "maximumRideTimePermille": PERMILLE,
        "normalizerSourceSha256": converter_sha,
        "pickupWindowMs": pickup_window,
        "requestTarget": len(requests),
        "scenarioId": scenario_id,
        "sourceCellFile": source_path.name,
        "sourceCellSha256": sha_bytes(raw),
        "sourceLocalDate": day.isoformat(),
        "sourceMatrixFile": matrix_path.name,
        "sourceMatrixSha256": sha_bytes(matrix_bytes),
        "sourceTimezoneId": "America/New_York",
        "sourceWindowEndSeconds": start_seconds + 7200,
        "sourceWindowStartSeconds": start_seconds,
        "travelFactorAtSeconds": 0,
        "vehicleCapacity": 4,
        "vehicleCount": NVEH,
        "zoneRepresentativeRule": "zone-medoid-in-plus-out-travel-time-tie-min-fleetpy-index-v1",
    }
    norm_bytes = compact(norm_config)
    frame_bytes = compact(frame)
    validation = {
        "directedArcCount": len(arcs),
        "duplicateIdCount": 0,
        "eventCount": len(events),
        "excludedSourceRowCount": dropped,
        "invalidTimeRowCount": 0,
        "invariantSetHash": sha_bytes(b"RideBound.ViaZoneConverter.Invariants.v1\0" + compact(
            {"closedEntries": closed, "nodes": n, "requests": len(requests), "vehicles": NVEH})),
        "nodeCount": n,
        "overflowRowCount": 0,
        "requestCount": len(requests),
        "selectedSourceRowCount": len(requests),
        "snapshotCount": 1,
        "unreachableSelectedRowCount": 0,
        "vehicleCount": NVEH,
    }
    scenario = {
        "datasetId": "nyc-tlc-hvfhv-2019-via-hv0004-company-p512-zone-v1",
        "driverSemanticsId": "wp6-fleetpy-public-derivative-driver-v1",
        "eventOrderingId": "ridebound-event-order-v1",
        "events": events,
        "evidenceClass": "mechanical",
        "fleet": fleet,
        "normalizerConfigurationSha256": sha_bytes(norm_bytes),
        "normalizerId": CONVERTER_ID,
        "normalizerSourceSha256": converter_sha,
        "normalizerVersion": CONVERTER_VERSION,
        "requests": requests,
        "scenarioId": scenario_id,
        "scenarioKind": "publicDerivative",
        "schemaVersion": "1.0.1",
        "sourceArtifactSha256": sha_bytes(raw),
        "sourceSelectionSha256": sha_bytes(frame_bytes),
        "timeWindow": {
            "batchingId": "event-driven-v1",
            "drainEndMs": DRAIN_END_MS,
            "horizonEndMs": HORIZON_MS,
            "scoreStartMs": 0,
            "sourceTimezoneId": "America/New_York",
            "sourceWindowEndUtc": utc(end_utc),
            "sourceWindowStartUtc": utc(start_utc),
            "warmupStartMs": 0,
        },
        "travelSnapshots": [{"arcs": arcs, "snapshotHash": snapshot_hash, "version": 1}],
        "validationSummary": validation,
    }
    scenario_bytes = compact(scenario)
    scenario_sha = sha_bytes(scenario_bytes)
    scenario_hash = sha_bytes(b"RideBound.ViaZoneConverter.Scenario.v1\0" + scenario_bytes)
    report = {
        "configurationSha256": sha_bytes(norm_bytes),
        "converterId": CONVERTER_ID,
        "converterVersion": CONVERTER_VERSION,
        "datasetId": scenario["datasetId"],
        "excludedRecordCount": dropped,
        "inputRecordCount": len(src["requests"]),
        "matrixClosedEntries": closed,
        "nodeCount": n,
        "scenarioContentSha256": scenario_sha,
        "scenarioHash": scenario_hash,
        "selectedRecordCount": len(requests),
        "selectionFrameSha256": sha_bytes(frame_bytes),
        "sourceArtifactSha256": sha_bytes(raw),
        "sourceNodeCount": n_src,
        "zoneCount": len(representative),
    }
    report_bytes = compact(report)
    manifest = {
        "citation": "NYC Taxi & Limousine Commission, TLC Trip Record Data, High Volume For-Hire Vehicle trips 2019 "
                    "(hvfhs_license_num HV0004, Via), https://www.nyc.gov/site/tlc/about/tlc-trip-record-data.page; "
                    "trip ends placed on the FleetPy Manhattan network (Engelhardt & Dandl 2025, Zenodo 15187906) "
                    "by the company P512 cell generator, then mapped to one node per TLC zone by this converter",
        "converterId": CONVERTER_ID,
        "converterSourceSha256": converter_sha,
        "forbiddenClaims": ["observedCommitmentPreference", "userSatisfaction", "equivalenceToCompanyP512Cell",
                            "confirmatoryResult"],
        "normalizationReportHash": sha_bytes(report_bytes),
        "policyObservationClass": "syntheticPolicyOverlay",
        "scenarioContentSha256": scenario_sha,
        "scenarioHash": scenario_hash,
        "scenarioId": scenario_id,
        "schemaVersion": "1.0.0",
        "selectionFrameSha256": sha_bytes(frame_bytes),
        "sourceArtifactSha256": sha_bytes(raw),
        "transformationSteps": [
            "read-company-p512-plain-cell-and-int32-ms-matrix",
            "keep-first-62-vehicles",
            "map-every-node-to-its-tlc-zone-medoid",
            "integer-floyd-warshall-closure-on-representatives",
            "drop-requests-whose-ends-share-a-representative",
            "max-ride-ties-to-even-2000-permille",
            "pseudonymize-request-ids-with-domain-separated-sha256",
            "emit-canonical-scenario",
        ],
    }
    driver = {
        "driverVersion": "1.0.0",
        "scenarioId": scenario_id,
        "sourceScenarioContentSha256": scenario_sha,
        "sourceScenarioHash": scenario_hash,
        "sourceArtifactSha256": sha_bytes(raw),
        "fleetpyVersion": "1.0.2",
        "fleetpyCommit": "053aa9d4fcfde91c5d303435d5748f9206c071b0",
        "eventDriver": "exact-source-arrival-event-clock-v1",
        "arrivalTimeTransform": "none",
        "routingRepresentation": "directed-normalized-shortest-path-closure-v1",
        "completeTravelSnapshotMaximumNodes": n,
        "physicalDistanceRule": "travel-time-seconds-times-ten-synthetic-meters-v1",
        "drainStepSeconds": 600,
        "maximumDrainSteps": 24,
        "measuredRepeatsPerArm": 1,
        "serviceClass": SERVICE_CLASS,
        "commitmentPolicyId": POLICY_ID,
        "claimClass": "development-exploratory-only",
        "forbiddenClaims": ["population-inference", "external-validity", "fairness", "satisfaction", "SLA", "novelty",
                            "confirmatory-result", "h6-reinterpretation", "equivalence-to-company-p512-cell"],
        "expectedRequestCount": len(requests),
        "expectedVehicleCount": NVEH,
        "expectedNodeCount": n,
        "expectedDirectedArcCount": len(arcs),
    }
    factors = src["travel"]["profile"]["factors"]
    if src["travel"]["profile"]["binMs"] != 900_000 or src["travel"].get("closure", "none") != "none":
        raise SystemExit("unexpected travel profile")
    if (src["travel"]["profile"].get("afterLast") or "hold") != "hold" or factors[0] != 1:
        raise SystemExit("profile must be anchored at 1 and held after the last bin")
    world = {"worldVersion": "world-v1", "worldSeed": 101, "rho": 0.7, "shockMean": 0.4, "shockHalfLife": 2.0,
             "shockWindow": 12, "sigma": 0.0, "shockRate": 0.0, "base": {"values": factors}}

    fixture = out_root / "fixtures" / cell
    if fixture.exists():
        raise SystemExit(f"refused: {fixture} exists")
    write_new(fixture / "scenario-content.json", scenario_bytes)
    write_new(fixture / "normalizer-configuration.json", norm_bytes)
    write_new(fixture / "selection-frame.json", frame_bytes)
    write_new(fixture / "normalization-report.json", report_bytes)
    write_new(fixture / "derivative-manifest.json", compact(manifest))
    write_new(fixture / "ATTRIBUTION.md", (
        f"# {scenario_id}\n\nResearch fixture made by {CONVERTER_ID} {CONVERTER_VERSION} from the company Via P512 cell "
        f"`{source_path.name}` (sha256 {sha_bytes(raw)}).\n\nSource data: {manifest['citation']}.\n\n"
        "This is not the company cell: trip ends are moved to one representative node per TLC zone.\n").encode("utf-8"))
    write_new(out_root / "drivers" / f"{cell}.driver.json",
              (json.dumps(driver, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))
    write_new(out_root / "worlds" / f"{cell}.world.json", json.dumps(world).encode("utf-8"))
    print(json.dumps({"cell": cell, "source": source_path.name, "sourceNodes": n_src, "zones": len(representative),
                      "nodes": n, "arcs": len(arcs), "matrixClosedEntries": closed, "requests": len(requests),
                      "droppedIntraZone": dropped, "vehicles": NVEH, "scenarioContentSha256": scenario_sha,
                      "scenarioBytes": len(scenario_bytes), "bins": len(factors)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
