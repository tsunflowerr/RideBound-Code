#!/usr/bin/env python3
"""Run the WP6 medium public derivative through actual FleetPy vehicle movement."""

from __future__ import annotations

import argparse
import collections
import csv
import hashlib
import json
import os
import pathlib
import subprocess
import sys
import tempfile
import time
import traceback


def _strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def _read_json(path):
    with path.open("r", encoding="utf-8", newline="") as source:
        value = json.load(source, object_pairs_hook=_strict_object)
    if not isinstance(value, dict):
        raise RuntimeError(f"{path}: root must be an object")
    return value


def _sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _write_new(path, value):
    encoded = (
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")
    with path.open("xb") as target:
        target.write(encoded)


def _write_bundle_manifest(output_directory):
    files = []
    for path in sorted(
        (value for value in output_directory.rglob("*") if value.is_file()),
        key=lambda value: value.relative_to(output_directory).as_posix(),
    ):
        relative = path.relative_to(output_directory).as_posix()
        files.append(
            {
                "path": relative,
                "lengthBytes": path.stat().st_size,
                "sha256": _sha256(path),
            }
        )
    _write_new(
        output_directory / "bundle-manifest.json",
        {
            "schemaVersion": "1.0.0",
            "bundleType": "ridebound-wp7-actual-fleetpy-medium-v1",
            "files": files,
        },
    )


class _PublicRequest:
    def __init__(self, source, node_index):
        for field in [
            "arrivalTimeMs",
            "earliestPickupMs",
            "latestPickupMs",
        ]:
            if source[field] % 1000 != 0:
                raise RuntimeError(f"{source['requestId']}: {field} is not whole seconds")
        self.rid = source["requestId"]
        self.nr_pax = source["partySize"]
        self.rq_time = source["arrivalTimeMs"] // 1000
        self.o_pos = (node_index[source["originNodeId"]], None, None)
        self.d_pos = (node_index[source["destinationNodeId"]], None, None)
        self.earliest_start_time = source["earliestPickupMs"] // 1000
        self.latest_start_time = source["latestPickupMs"] // 1000
        # FleetPy's public request contract uses seconds and accepts fractional
        # values.  Keep the source millisecond bound rather than rounding it to
        # the event clock's whole-second granularity.
        self.max_trip_time = source["maxRideTimeMs"] / 1000
        self.pu_time = None
        self.is_parcel = False

    def get_rid_struct(self):
        return self.rid

    def get_rid(self):
        return self.rid


def _materialize_network(root, source):
    snapshot = source["travelSnapshots"][0]
    arcs = snapshot["arcs"]
    nodes = sorted(
        {arc["fromNodeId"] for arc in arcs}
        | {arc["toNodeId"] for arc in arcs}
    )
    node_index = {node_id: index for index, node_id in enumerate(nodes)}
    base = root / "base"
    base.mkdir(parents=True)
    with (base / "nodes.csv").open("x", encoding="utf-8", newline="") as target:
        writer = csv.writer(target, lineterminator="\n")
        writer.writerow(["node_index", "is_stop_only", "pos_x", "pos_y"])
        for index, _node_id in enumerate(nodes):
            writer.writerow([index, "True", index * 1000, 0])
    with (base / "edges.csv").open("x", encoding="utf-8", newline="") as target:
        writer = csv.writer(target, lineterminator="\n")
        writer.writerow(
            ["from_node", "to_node", "distance", "travel_time", "source_edge_id"]
        )
        for ordinal, arc in enumerate(arcs):
            seconds = arc["travelTimeMs"] / 1000
            writer.writerow(
                [
                    node_index[arc["fromNodeId"]],
                    node_index[arc["toNodeId"]],
                    seconds * 10,
                    seconds,
                    ordinal,
                ]
            )
    with (base / "crs.info").open("x", encoding="ascii", newline="") as target:
        target.write("EPSG:32632\n")
    mapping_hash = hashlib.sha256(
        b"RideBound.Wp7MediumNodeMap.v1\0"
        + json.dumps(
            node_index,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()
    return node_index, mapping_hash


def _assert_complete_shortest_path_closure(source, node_index):
    """ADR-089: every ordered node pair has an arc and no two-arc path is shorter (integer ms, Floyd-Warshall test)."""
    import numpy as np

    size = len(node_index)
    matrix = np.full((size, size), -1, dtype=np.int64)
    np.fill_diagonal(matrix, 0)
    for arc in source["travelSnapshots"][0]["arcs"]:
        matrix[node_index[arc["fromNodeId"]], node_index[arc["toNodeId"]]] = arc["travelTimeMs"]
    if (matrix < 0).any():
        raise RuntimeError("complete-graph-direct-v1 needs an arc for every ordered node pair")
    for via in range(size):
        if (matrix[:, via:via + 1] + matrix[via:via + 1, :] < matrix).any():
            raise RuntimeError(f"arcs are not a shortest-path closure (a path through node index {via} is shorter)")


def _source_requests(source):
    result = []
    for event in source["events"]:
        if event["eventType"] != "requestArrived":
            raise RuntimeError(f"unsupported source event {event['eventType']!r}")
        payload_bytes = bytes.fromhex(event["payloadCanonicalJsonHex"])
        if hashlib.sha256(payload_bytes).hexdigest() != event["payloadSha256"]:
            raise RuntimeError("source event payload hash mismatch")
        payload = json.loads(payload_bytes, object_pairs_hook=_strict_object)
        request = payload["request"]
        if request["requestId"] != event["stableSubjectId"]:
            raise RuntimeError("source request identity mismatch")
        if request["arrivalTimeMs"] != event["simTimeMs"]:
            raise RuntimeError("source request time mismatch")
        result.append(request)
    return result


def _assert_request_round_trip(control, source, node_index):
    mapped = control._rb_mapper.request(
        control.rq_dict[source["requestId"]],
        control._rb_settings.service_class,
        control._rb_settings.commitment_policy_id,
    )
    scalar_expected = {
        "arrivalTimeMs": source["arrivalTimeMs"],
        "earliestPickupMs": source["earliestPickupMs"],
        "latestPickupMs": source["latestPickupMs"],
        "maxRideTimeMs": source["maxRideTimeMs"],
        "partySize": source["partySize"],
        "serviceClass": "fleetpy-tlc-public-derivative-v1",
        "commitmentPolicyId": "wp6-synthetic-policy-overlay-v1",
    }
    if any(mapped[key] != value for key, value in scalar_expected.items()):
        raise RuntimeError(
            f"{source['requestId']}: FleetPy PlanRequest scalar round-trip drift"
        )
    if (
        control._rb_mapper.raw_request(mapped["requestId"])
        != source["requestId"]
        or control._rb_mapper.raw_node(mapped["originNodeId"])
        != node_index[source["originNodeId"]]
        or control._rb_mapper.raw_node(mapped["destinationNodeId"])
        != node_index[source["destinationNodeId"]]
    ):
        raise RuntimeError(
            f"{source['requestId']}: FleetPy PlanRequest identity/node round-trip drift"
        )


def _operator_settings(
    arguments,
    scenario_root,
    additional,
    complete_travel_snapshot_maximum_nodes,
    transcript_path,
):
    from src.misc.globals import G_OP_FLEET, G_OP_VR_CTRL_F

    return {
        G_OP_VR_CTRL_F: "ridebound-external-runner",
        G_OP_FLEET: "wp7-medium-public-derivative",
        "ridebound_dotnet_path": str(arguments.dotnet.resolve()),
        "ridebound_runner_root": str(arguments.runner_root.resolve()),
        "ridebound_commitment_config": str(
            arguments.commitment_config.resolve()
        ),
        "ridebound_wp4_config": str(arguments.wp4_config.resolve()),
        "ridebound_fleetpy_root": str(arguments.fleetpy_root.resolve()),
        "ridebound_repository_root": str(arguments.repository_root),
        "ridebound_scenario_root": str(scenario_root),
        "ridebound_additional_artifacts": [str(path) for path in additional],
        "ridebound_run_id": f"wp7-medium-{arguments.label}",
        "ridebound_scenario_id": "fleetpy-manhattan-v1-medium-wp7-physical-closure",
        "ridebound_master_seed": arguments.master_seed,
        "ridebound_service_class": "fleetpy-tlc-public-derivative-v1",
        "ridebound_commitment_policy_id": "wp6-synthetic-policy-overlay-v1",
        "ridebound_timeout_seconds": 60,
        "ridebound_complete_travel_snapshot_maximum_nodes":
            complete_travel_snapshot_maximum_nodes,
        "ridebound_transcript_path": str(transcript_path),
        "op_min_wait_time": 0,
        "op_max_wait_time": 600,
        "op_const_boarding_time": 0,
    }


def _move_vehicles(control, vehicles, requests, start_time, end_time):
    if end_time < start_time:
        raise RuntimeError("medium event clock regressed")
    for vehicle in vehicles:
        boarding, alighting, passed, _started_alighting = vehicle.update_veh_state(
            start_time,
            end_time,
        )
        for rid, (boarding_time, _position) in boarding.items():
            requests[rid].pu_time = boarding_time
            control.acknowledge_boarding(rid, vehicle.vid, boarding_time)
        for rid, alighting_time in alighting.items():
            control.acknowledge_alighting(rid, vehicle.vid, alighting_time)
        control.receive_status_update(
            vehicle.vid,
            end_time,
            passed,
            bool(boarding or alighting),
        )


def _advance_immediate_vehicle_legs(control, vehicles, requests, simulation_time):
    """Advance only FleetPy transitions that occur at the current instant.

    FleetPy's regular update loop requires a positive step to close a
    zero-duration service VRL. RideBound candidates deliberately use zero
    service duration, so the event-driven validation clock performs the same
    upstream end/start calls at the exact instant instead of retimestamping a
    passenger callback to a later demand arrival.
    """
    from src.misc.globals import G_DRIVING_STATUS

    changed = False
    notified = False
    for vehicle in vehicles:
        boarding = {}
        alighting = {}
        passed = []
        if vehicle.start_next_leg_first:
            boarded, _started_alighting = vehicle.start_next_leg(simulation_time)
            vehicle.start_next_leg_first = False
            boarding.update(
                {rid: (simulation_time, vehicle.pos) for rid in boarded}
            )
            changed = True
        elif (
            vehicle.assigned_route
            and vehicle.status not in G_DRIVING_STATUS
            and vehicle.cl_remaining_time == 0
        ):
            alighted, finished = vehicle.end_current_leg(simulation_time)
            alighting.update({rid: simulation_time for rid in alighted})
            if finished:
                passed.append(finished)
            if vehicle.assigned_route:
                boarded, _started_alighting = vehicle.start_next_leg(
                    simulation_time
                )
                boarding.update(
                    {rid: (simulation_time, vehicle.pos) for rid in boarded}
                )
            changed = True

        if not (boarding or alighting or passed):
            continue
        for rid, (boarding_time, _position) in boarding.items():
            requests[rid].pu_time = boarding_time
            control.acknowledge_boarding(rid, vehicle.vid, boarding_time)
        for rid, alighting_time in alighting.items():
            control.acknowledge_alighting(rid, vehicle.vid, alighting_time)
        control.receive_status_update(
            vehicle.vid,
            simulation_time,
            passed,
            bool(boarding or alighting),
        )
        notified = True
    return changed, notified


def _next_physical_boundary(vehicles, routing, simulation_time):
    from src.misc.globals import G_DRIVING_STATUS

    boundaries = []
    for vehicle in vehicles:
        if not vehicle.assigned_route:
            continue
        if vehicle.start_next_leg_first:
            boundaries.append(simulation_time)
            continue
        if vehicle.status in G_DRIVING_STATUS:
            travel_time = routing.return_travel_costs_1to1(
                vehicle.pos,
                vehicle.assigned_route[0].destination_pos,
            )[1]
            if travel_time < 0:
                raise RuntimeError(
                    f"vehicle {vehicle.vid}: negative remaining travel time"
                )
            boundaries.append(simulation_time + travel_time)
            continue
        remaining = vehicle.cl_remaining_time
        if remaining is None:
            raise RuntimeError(
                f"vehicle {vehicle.vid}: active non-driving leg has no duration"
            )
        if remaining < 0:
            raise RuntimeError(
                f"vehicle {vehicle.vid}: negative remaining service time"
            )
        boundaries.append(simulation_time + remaining)
    return min(boundaries) if boundaries else None


def _resolve_offers(control, request_ids, simulation_time):
    confirmed = []
    rejected = []
    for rid in sorted(request_ids):
        state = control._rb_states.get(rid)
        if state == "offered":
            offer = control.get_current_offer(rid)
            if offer is None or offer.service_declined():
                raise RuntimeError(f"{rid}: offered lifecycle has no service offer")
            control.user_confirms_booking(rid, simulation_time)
            confirmed.append(rid)
        elif state == "rejected":
            offer = control.get_current_offer(rid)
            if offer is None or not offer.service_declined():
                raise RuntimeError(f"{rid}: rejected lifecycle has no rejection")
            control.user_cancels_request(rid, simulation_time)
            rejected.append(rid)
    return confirmed, rejected


def _drain_confirmations(control, request_ids, simulation_time):
    confirmed = []
    rejected = []
    for _microstep in range(4):
        new_confirmed, new_rejected = _resolve_offers(
            control,
            request_ids,
            simulation_time,
        )
        confirmed.extend(new_confirmed)
        rejected.extend(new_rejected)
        if not new_confirmed:
            break
        control.time_trigger(simulation_time)
    else:
        raise RuntimeError("confirmation microstep did not converge")
    return confirmed, rejected


def _trigger_and_resolve(
    control,
    request_ids,
    request_db,
    simulation_time,
    confirmed_ids,
    rejected_ids,
):
    control.time_trigger(simulation_time)
    confirmed, rejected = _drain_confirmations(
        control,
        request_ids,
        simulation_time,
    )
    confirmed_ids.update(confirmed)
    rejected_ids.update(rejected)
    for rid in list(request_db):
        if control._rb_states.get(rid) == "closed":
            request_db.pop(rid, None)


def _run_repeat(arguments, repeat, source, driver, output_directory):
    import psutil
    from src.misc.globals import G_DIR_DATA, G_DIR_OUTPUT, G_SKIP_OUTPUT
    from src.routing.NetworkBasic import NetworkBasic
    from src.simulation.Vehicles import SimulationVehicle
    from src.misc.init_modules import load_fleet_control_module
    from ridebound_fleetpy.mapping import seconds_to_milliseconds

    started = time.perf_counter()
    process = psutil.Process()
    cpu_before = process.cpu_times()
    rss_before = process.memory_info().rss
    with tempfile.TemporaryDirectory(prefix="ridebound-wp7-medium-") as temporary:
        temporary_path = pathlib.Path(temporary)
        network_root = temporary_path / "network"
        node_index, node_mapping_hash = _materialize_network(network_root, source)
        routing_engine = driver.get("routingEngine", "network-basic-v1")
        if routing_engine == "network-basic-v1":
            routing = NetworkBasic(str(network_root))
        elif routing_engine == "complete-graph-direct-v1":
            # ADR-089: full-node Via cells. The direct-edge network is only valid when every arc is a shortest
            # path, which NetworkBasic's per-arc check below would otherwise establish with a Dijkstra per arc.
            from ridebound_fleetpy.direct_network import CompleteGraphDirectNetwork

            _assert_complete_shortest_path_closure(source, node_index)
            routing = CompleteGraphDirectNetwork(str(network_root))
        else:
            raise RuntimeError(f"unknown routingEngine {routing_engine!r}")
        snapshot = source["travelSnapshots"][0]
        for arc in snapshot["arcs"]:
            origin = (node_index[arc["fromNodeId"]], None, None)
            destination = (node_index[arc["toNodeId"]], None, None)
            actual_ms = round(
                routing.return_travel_costs_1to1(origin, destination)[1] * 1000
            )
            if actual_ms != arc["travelTimeMs"]:
                raise RuntimeError(
                    f"metric closure drift {arc['fromNodeId']}->{arc['toNodeId']}: "
                    f"expected={arc['travelTimeMs']}; actual={actual_ms}"
                )

        request_values = _source_requests(source)
        request_db = {}
        vehicle_directory = (
            arguments.repository_root
            / "src"
            / "adapter"
            / "assets"
        )
        vehicles = [
            SimulationVehicle(
                0,
                index,
                str(vehicle_directory),
                "wp7_tiny",
                routing,
                request_db,
                [],
                False,
                False,
            )
            for index in range(len(source["fleet"]))
        ]
        for vehicle, source_vehicle in zip(vehicles, source["fleet"], strict=True):
            vehicle.pos = (
                node_index[source_vehicle["position"]["nodeId"]],
                None,
                None,
            )
            vehicle.soc = 1.0

        scenario_root = (
            arguments.repository_root
            / "src"
            / "adapter"
            / "assets"
            / "wp7-fleetpy-medium"
        )
        additional = [
            arguments.scenario.resolve(),
            arguments.derivative_manifest.resolve(),
            arguments.normalization_report.resolve(),
            arguments.selection_frame.resolve(),
            vehicle_directory / "wp7_tiny.csv",
            pathlib.Path(__file__).resolve(),
            pathlib.Path(__file__).with_name(
                "actual_fleetpy_medium_verify.py"
            ).resolve(),
        ]
        compression = driver.get("transcriptCompression", "none")
        if compression not in ("none", "gzip-v1"):
            raise RuntimeError(f"unknown transcriptCompression {compression!r}")
        operator = _operator_settings(
            arguments,
            scenario_root,
            additional,
            driver["completeTravelSnapshotMaximumNodes"],
            output_directory
            / (f"transcript-{repeat:02d}.ndjson" + (".gz" if compression == "gzip-v1" else "")),
        )
        scenario = {
            "n_cpu_per_sim": 1,
            "start_time": 0,
            G_SKIP_OUTPUT: 1,
            "scenario_name": "fleetpy-manhattan-v1-medium-wp7-physical-closure",
        }
        directories = {
            G_DIR_DATA: str(temporary_path),
            G_DIR_OUTPUT: str(temporary_path),
        }
        control_class = load_fleet_control_module("RideBoundFleetControl")
        control = control_class(
            0,
            operator,
            vehicles,
            routing,
            None,
            scenario,
            directories,
        )
        confirmed_ids = set()
        rejected_ids = set()
        all_ids = {request["requestId"] for request in request_values}
        grouped = collections.defaultdict(list)
        for value in request_values:
            grouped[value["arrivalTimeMs"] // 1000].append(value)
        current_time = 0
        try:
            _trigger_and_resolve(
                control,
                all_ids,
                request_db,
                0,
                confirmed_ids,
                rejected_ids,
            )
            arrival_times = sorted(grouped)
            arrival_index = 0
            drain_steps = 0
            while True:
                # Start newly assigned legs and close only zero-duration service
                # legs at this exact instant. This prevents an actual boarding
                # timestamp from being relabelled as the next demand arrival.
                for _immediate_step in range(4096):
                    changed, notified = _advance_immediate_vehicle_legs(
                        control,
                        vehicles,
                        request_db,
                        current_time,
                    )
                    if notified:
                        _trigger_and_resolve(
                            control,
                            all_ids,
                            request_db,
                            current_time,
                            confirmed_ids,
                            rejected_ids,
                        )
                    if not changed:
                        break
                else:
                    raise RuntimeError("immediate FleetPy leg closure did not converge")

                active = any(
                    state != "closed" for state in control._rb_states.values()
                )
                physical = any(
                    vehicle.assigned_route or vehicle.pax for vehicle in vehicles
                )
                next_arrival = (
                    arrival_times[arrival_index]
                    if arrival_index < len(arrival_times)
                    else None
                )
                if next_arrival is None and not active and not physical:
                    break

                physical_boundary = _next_physical_boundary(
                    vehicles,
                    routing,
                    current_time,
                )
                if (
                    physical_boundary is not None
                    and physical_boundary <= current_time
                ):
                    raise RuntimeError(
                        "non-positive physical boundary remained after immediate closure"
                    )
                timer_boundary = None
                if next_arrival is None and (active or physical):
                    if drain_steps >= driver["maximumDrainSteps"]:
                        raise RuntimeError("medium drain budget exhausted")
                    timer_boundary = current_time + driver["drainStepSeconds"]

                boundaries = [
                    value
                    for value in (
                        next_arrival,
                        physical_boundary,
                        timer_boundary,
                    )
                    if value is not None
                ]
                if not boundaries:
                    raise RuntimeError("medium event clock has no next boundary")
                next_time = min(boundaries)
                _move_vehicles(
                    control,
                    vehicles,
                    request_db,
                    current_time,
                    next_time,
                )
                if next_arrival is not None and next_time == next_arrival:
                    for value in grouped[next_arrival]:
                        request = _PublicRequest(value, node_index)
                        request_db[request.rid] = request
                        control.user_request(request, next_arrival)
                        _assert_request_round_trip(control, value, node_index)
                    arrival_index += 1
                _trigger_and_resolve(
                    control,
                    all_ids,
                    request_db,
                    next_time,
                    confirmed_ids,
                    rejected_ids,
                )
                if timer_boundary is not None and next_time == timer_boundary:
                    drain_steps += 1
                current_time = next_time

            state_counts = collections.Counter(control._rb_states.values())
            if state_counts != {"closed": len(request_values)}:
                raise RuntimeError(f"non-terminal medium states: {state_counts!r}")
            if confirmed_ids & rejected_ids:
                raise RuntimeError("request appears in accepted and rejected sets")
            if confirmed_ids | rejected_ids != all_ids:
                raise RuntimeError("request outcome conservation failed")
            if request_db or control.rq_dict or control.rid_to_assigned_vid:
                raise RuntimeError("request database/index did not drain")
            if any(vehicle.assigned_route or vehicle.pax for vehicle in vehicles):
                raise RuntimeError("physical FleetPy fleet did not drain")
            if any(route.remaining_stops for route in control._rb_routes.values()):
                raise RuntimeError("protocol fleet did not drain")
            initial_publications = [
                publication
                for publication in control._rb_publications
                if publication["reasonCode"] == "INITIAL_BOOKING_CONFIRMATION"
            ]
            if len(initial_publications) != len(confirmed_ids):
                raise RuntimeError("accepted/initial-promise conservation failed")
            control.checkpoint_runner()
            semantic = {
                "sourceScenarioContentSha256": _sha256(arguments.scenario),
                "nodeMappingHash": node_mapping_hash,
                "manifestHash": control._rb_session.manifest_hash,
                "checkpointBindingHash": control._rb_checkpoint_binding_hash,
                "requestCount": len(request_values),
                "acceptedRequestIds": sorted(confirmed_ids),
                "rejectedRequestIds": sorted(rejected_ids),
                "publicationCount": len(control._rb_publications),
                "publicationDigest": hashlib.sha256(
                    json.dumps(
                        control._rb_publications,
                        ensure_ascii=False,
                        sort_keys=True,
                        separators=(",", ":"),
                    ).encode("utf-8")
                ).hexdigest(),
                "travelSnapshotVersion": control._rb_travel_version,
                "nextEventSeq": control._rb_events.next_event_sequence,
                "nextEpoch": control._rb_events.next_epoch,
                "finalSimulationTimeMs": seconds_to_milliseconds(
                    current_time,
                    "$.medium.finalSimulationTime",
                ),
                "finalVehiclePositions": [list(vehicle.pos) for vehicle in vehicles],
            }
            semantic_hash = hashlib.sha256(
                b"RideBound.Wp7ActualFleetPyMedium.v1\0"
                + json.dumps(
                    semantic,
                    ensure_ascii=False,
                    sort_keys=True,
                    separators=(",", ":"),
                ).encode("utf-8")
            ).hexdigest()
        finally:
            control.close()
        receipts = control._rb_session.artifact_receipts
        if receipts["before"] != receipts["after"]:
            raise RuntimeError("medium artifact receipt drift")

    cpu_after = process.cpu_times()
    report = {
        "schemaVersion": "1.0.0",
        "status": "succeeded",
        "label": arguments.label,
        "repeat": repeat,
        "semanticHash": semantic_hash,
        "semantic": semantic,
        "artifactCount": len(receipts["before"]),
        "artifactReceiptsEqual": True,
        "resources": {
            "wallMilliseconds": round((time.perf_counter() - started) * 1000),
            "userCpuMilliseconds": round(
                (cpu_after.user - cpu_before.user) * 1000
            ),
            "systemCpuMilliseconds": round(
                (cpu_after.system - cpu_before.system) * 1000
            ),
            "rssBeforeBytes": rss_before,
            "rssAfterBytes": process.memory_info().rss,
        },
    }
    _write_new(output_directory / f"run-{repeat:02d}.json", report)
    return report


def _git_head_commit(repository):
    completed = subprocess.run(
        ["git", "-C", str(repository), "rev-parse", "HEAD"],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="ascii",
        timeout=10,
    )
    return completed.stdout.strip()


def _repository_inventory_sha256(repository):
    head = _git_head_commit(repository).encode("ascii")
    completed = subprocess.run(
        [
            "git",
            "-C",
            str(repository),
            "ls-files",
            "-z",
            "--cached",
            "--others",
            "--exclude-standard",
        ],
        check=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=30,
    )
    relative_paths = sorted(
        value for value in completed.stdout.split(b"\0") if value
    )
    if len(relative_paths) != len(set(relative_paths)):
        raise RuntimeError("repository inventory contains duplicate paths")
    digest = hashlib.sha256(b"RideBound.Wp9RepositoryInventory.v1\0")
    digest.update(len(head).to_bytes(8, "big"))
    digest.update(head)
    for relative_bytes in relative_paths:
        relative = pathlib.Path(os.fsdecode(relative_bytes))
        path = (repository / relative).resolve()
        try:
            path.relative_to(repository)
        except ValueError as failure:
            raise RuntimeError("repository inventory path escaped root") from failure
        if not path.is_file():
            raise RuntimeError(f"repository inventory file is missing: {relative}")
        digest.update(len(relative_bytes).to_bytes(8, "big"))
        digest.update(relative_bytes)
        length = path.stat().st_size
        digest.update(length.to_bytes(8, "big"))
        with path.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
    return digest.hexdigest()


def _validate_master_seed(value):
    if (
        isinstance(value, bool)
        or not isinstance(value, int)
        or value < 0
        or value > 2_147_483_647
    ):
        raise ValueError("master seed must be in the non-negative Int32 range")
    return value


def run(arguments):
    adapter_root = pathlib.Path(__file__).parent.resolve()
    arguments.repository_root = adapter_root.parents[1]
    sys.path.insert(0, str(adapter_root))
    sys.path.insert(0, str(arguments.fleetpy_root.resolve()))
    source = _read_json(arguments.scenario.resolve())
    driver = _read_json(arguments.driver.resolve())
    expected_source_hash = driver["sourceScenarioContentSha256"]
    actual_source_hash = _sha256(arguments.scenario.resolve())
    if actual_source_hash != expected_source_hash:
        raise RuntimeError(
            f"source scenario hash mismatch: {actual_source_hash}"
        )
    # The cardinality a run must see is declared by its driver, so a grid can
    # vary fleet size or demand window without weakening the check. Drivers that
    # omit the fields keep the original WP7 medium constants exactly.
    expected_cardinality = {
        "requestCount": driver.get("expectedRequestCount", 128),
        "vehicleCount": driver.get("expectedVehicleCount", 32),
        "nodeCount": driver.get("expectedNodeCount", 96),
        "directedArcCount": driver.get("expectedDirectedArcCount", 9120),
    }
    if source["validationSummary"] != {
        **source["validationSummary"],
        **expected_cardinality,
    }:
        raise RuntimeError(
            "source cardinality drift: driver declared "
            f"{expected_cardinality}, scenario has "
            + repr(
                {
                    key: source["validationSummary"][key]
                    for key in expected_cardinality
                }
            )
        )
    if driver["completeTravelSnapshotMaximumNodes"] != source[
        "validationSummary"
    ]["nodeCount"]:
        raise RuntimeError("complete travel snapshot bound must equal node count")
    commit_before = _git_head_commit(arguments.repository_root)
    inventory_before = _repository_inventory_sha256(arguments.repository_root)
    expected_inventory = getattr(
        arguments,
        "expected_repository_inventory_sha256",
        None,
    )
    if expected_inventory is not None and inventory_before != expected_inventory:
        raise RuntimeError(
            "repository inventory differs from the frozen matrix source state"
        )
    output = arguments.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    reports = []
    for repeat in range(arguments.repeats):
        try:
            reports.append(
                _run_repeat(arguments, repeat, source, driver, output)
            )
        except BaseException as failure:
            failure_record = {
                "schemaVersion": "1.0.0",
                "status": "failed",
                "label": arguments.label,
                "repeat": repeat,
                "failureType": type(failure).__name__,
                "failureMessage": str(failure),
                "traceback": traceback.format_exc(),
            }
            _write_new(output / f"failure-{repeat:02d}.json", failure_record)
            raise
    # Provenance is sampled per repeat and is part of the manifest, so a commit
    # landing mid-matrix changes coreCommitSha and every downstream hash. That
    # is a frozen-source violation, not a nondeterministic engine, and it must
    # say so: diagnosing it from an opaque "repeats diverged" message cost a
    # full transcript diff once already.
    commit_after = _git_head_commit(arguments.repository_root)
    inventory_after = _repository_inventory_sha256(arguments.repository_root)
    if commit_after != commit_before:
        raise RuntimeError(
            "core commit drifted during the matrix "
            f"({commit_before} -> {commit_after}); the source tree must stay "
            "frozen for the whole repeat matrix"
        )
    if inventory_after != inventory_before:
        raise RuntimeError(
            "repository content inventory drifted during the matrix run; "
            "the Git-visible source tree must stay frozen"
        )
    hashes = {report["semanticHash"] for report in reports}
    if len(hashes) != 1:
        raise RuntimeError(f"medium semantic repeats diverged: {sorted(hashes)!r}")
    summary = {
        "schemaVersion": "1.0.0",
        "status": "pass",
        "label": arguments.label,
        "repeatCount": len(reports),
        "semanticHash": reports[0]["semanticHash"],
        "repositoryInventorySha256": inventory_before,
        "sourceScenarioContentSha256": actual_source_hash,
        "runs": [
            {
                "repeat": report["repeat"],
                "semanticHash": report["semanticHash"],
                "resources": report["resources"],
            }
            for report in reports
        ],
    }
    _write_new(output / "summary.json", summary)
    _write_bundle_manifest(output)
    print(
        json.dumps(
            summary,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
    )
    return summary


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--label", required=True)
    parser.add_argument("--fleetpy-root", required=True, type=pathlib.Path)
    parser.add_argument("--runner-root", required=True, type=pathlib.Path)
    parser.add_argument("--dotnet", required=True, type=pathlib.Path)
    parser.add_argument("--commitment-config", required=True, type=pathlib.Path)
    parser.add_argument("--wp4-config", required=True, type=pathlib.Path)
    parser.add_argument("--scenario", required=True, type=pathlib.Path)
    parser.add_argument("--derivative-manifest", required=True, type=pathlib.Path)
    parser.add_argument("--normalization-report", required=True, type=pathlib.Path)
    parser.add_argument("--selection-frame", required=True, type=pathlib.Path)
    parser.add_argument("--driver", required=True, type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--repeats", type=int, default=3)
    parser.add_argument("--master-seed", type=int, default=7)
    parser.add_argument("--expected-repository-inventory-sha256")
    arguments = parser.parse_args()
    if arguments.repeats < 1:
        parser.error("--repeats must be positive")
    try:
        _validate_master_seed(arguments.master_seed)
    except ValueError as failure:
        parser.error(f"--master-seed {failure}")
    if (
        arguments.expected_repository_inventory_sha256 is not None
        and (
            len(arguments.expected_repository_inventory_sha256) != 64
            or any(
                character not in "0123456789abcdef"
                for character in arguments.expected_repository_inventory_sha256
            )
        )
    ):
        parser.error("--expected-repository-inventory-sha256 must be lowercase SHA-256")
    run(arguments)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
