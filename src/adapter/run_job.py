"""Run one RideBound job: FleetPy moves the fleet, the .NET Runner makes every dispatch decision, and the job's
evidence is written at the end.

  python -B run_job.py --label <label> --role <arm> --fleetpy-root <FleetPy 1.0.2> --runner-root <published Runner>
      --dotnet <dotnet> --commitment-config <file> --wp4-config <file> --scenario <file> --derivative-manifest <file>
      --normalization-report <file> --selection-frame <file> --driver <file> --world <file> --output <new folder>
      [--master-seed 7] [--transcript keep|drop]

The inputs are one full-node Via cell (fixture files, run driver, world) and one arm (commitment and WP4
configuration). The output folder must not exist and its name must be <role>-<cell>. It receives:
  transcript-00.ndjson.gz  every protocol frame (deleted at the end with --transcript drop)
  run-00.json              what the run did (accepted and rejected requests, publications, final time) and its resources
  evidence.json.gz         what the rider tables read from the transcript, and EVIDENCE.sha256 with its SHA-256
  summary.json             "status": "pass", written only after the evidence has been read back
  bundle-manifest.json     size and SHA-256 of every file above
A failed job writes failure-00.json instead of summary.json.

Travel times: every edge starts at its free-flow time. At the first trigger of each 900 s bin b, every edge is set
to free-flow x factor(b), with factor(0) = 1 and factor(b) = values[min(b, n - 1)] / values[0] for the n base
values of the world, and the fleet control is told. The event clock stops at every bin start for this.
"""
import argparse
import atexit
import base64
import collections
import csv
import dataclasses
import gc
import gzip
import hashlib
import io
import json
import pathlib
import re
import subprocess
import sys
import tempfile
import time
import traceback

ADAPTER_ROOT = pathlib.Path(__file__).parent.resolve()
REPOSITORY_ROOT = ADAPTER_ROOT.parents[1]
VEHICLE_DIRECTORY = ADAPTER_ROOT / "assets"  # wp7_tiny.csv: the FleetPy vehicle type, 4 seats
# Its files are only hashed with the other job artifacts (run-00.json counts them), never read.
SCENARIO_ROOT = ADAPTER_ROOT / "assets" / "wp7-fleetpy-medium"
BIN_SECONDS = 900
# The Runner may need minutes for one answer on a full-node cell, and its frames reach about 0.8 GB.
RUNNER_TIMEOUT_SECONDS = 900
MAXIMUM_LINE_BYTES = 2**30
# Legacy names (WP7, WP6, Manhattan) kept on the Via runs: they go into the frames, so new frames stay
# byte-identical to the recorded runs. The other wire strings (adapter, FleetPy, hello) live in session.py.
RUN_ID_PREFIX = "wp7-medium-"
SCENARIO_ID = "fleetpy-manhattan-v1-medium-wp7-physical-closure"
SERVICE_CLASS = "fleetpy-tlc-public-derivative-v1"
COMMITMENT_POLICY_ID = "wp6-synthetic-policy-overlay-v1"
FLEET_NAME = "wp7-medium-public-derivative"
REMOVED = "removed; see commit ebca912 (branch research/final-real-runs-2026-11-06)"
WORLD_KEYS = {"worldVersion", "worldSeed", "sigma", "rho", "shockRate", "shockMean", "shockHalfLife", "shockWindow",
              "base"}
CELL = re.compile(r"d\d{8}-vf-r\d-w\d\d")
EVIDENCE = "evidence.json.gz"
EVIDENCE_FORMAT = "ridebound-job-evidence-v1"
# In a frame over 16 MiB the arcs of every travel snapshot are dropped before parsing: nothing below reads them.
BIG_FRAME = 16 * 2**20
ARCS = re.compile(rb'"arcs":\[.*?\],"snapshotHash"', re.DOTALL)
COORDINATES = (("pickup", "pickupEtaMs", "pickupEtaTotalMs"), ("drop", "dropEtaMs", "dropEtaTotalMs"))
MAX_CANONICAL = 2**53 - 1


def strict_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON field {key!r}")
        result[key] = value
    return result


def read_json(path):
    with path.open("r", encoding="utf-8", newline="") as source:
        value = json.load(source, object_pairs_hook=strict_object)
    if not isinstance(value, dict):
        raise RuntimeError(f"{path}: root must be an object")
    return value


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def write_new(path, value):
    with path.open("xb") as target:
        target.write(canonical(value) + b"\n")


def git_head(repository):
    return subprocess.run(["git", "-C", str(repository), "rev-parse", "HEAD"], check=True, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, text=True, encoding="ascii", timeout=10).stdout.strip()


# ---- world: travel-time factor per 900 s bin ----

def read_world(path):
    """The base values of a world-v1 file. Only noise-free worlds with explicit base values are run (sigma 0 and
    shockRate 0, as every Via world); their other noise parameters are then not used."""
    config = json.loads(path.read_bytes().decode("utf-8"))
    if config.get("sigma") != 0 or config.get("shockRate") != 0 or set(config.get("base", {})) != {"values"}:
        raise SystemExit("only noise-free worlds with an explicit base are run")
    unknown, missing = set(config) - WORLD_KEYS, WORLD_KEYS - set(config)
    if unknown or missing:
        raise SystemExit(f"world config keys: unknown={sorted(unknown)} missing={sorted(missing)}")
    if config["worldVersion"] != "world-v1":
        raise SystemExit(f"unsupported worldVersion {config['worldVersion']!r}")
    values = tuple(float(value) for value in config["base"]["values"])
    if not values or any(value <= 0 for value in values):
        raise SystemExit("explicit world base values must be positive and non-empty")
    return values


def world_factor(values, b):
    """Travel-time factor of bin b against bin 0; the last value holds after the last bin."""
    return 1.0 if b == 0 else values[min(b, len(values) - 1)] / values[0]


def clock_boundary(physical, now):
    """The next stop of the event clock: the next physical boundary (None: no vehicle moves), or the next bin start
    when that comes first."""
    bin_start = (int(now // BIN_SECONDS) + 1) * BIN_SECONDS
    return bin_start if physical is None or bin_start < physical else physical


class WorldTravelTimes:
    """Sets every edge to its free-flow time x the factor of the bin that holds the current time."""

    def __init__(self, routing, values):
        self.routing, self.values, self.bin = routing, values, None
        self.free_flow = {(node.node_index, other.node_index): edge.get_tt()
                          for node in routing.nodes for other, edge in node.edges_to.items()}

    def update(self, control, simulation_time):
        """On the first trigger of a new bin: set every edge, then tell the control (it drops its cached arcs)."""
        b = int(simulation_time // BIN_SECONDS)
        if b == self.bin:
            return
        self.bin = b
        factor = world_factor(self.values, b)
        for (origin, destination), free_flow in self.free_flow.items():
            self.routing._set_edge_tt(origin, destination, free_flow * factor)
        self.routing._reset_internal_attributes_after_travel_time_update()
        control.inform_network_travel_time_update(simulation_time)
        print(f"[world] t={simulation_time:.0f}s bin={b} factor={factor!r}", flush=True)


# ---- inputs: network, requests, settings ----

def materialize_network(root, source):
    """FleetPy network files (nodes.csv, edges.csv, crs.info) for the scenario's travel snapshot; returns the node
    index of every node id and the hash of that mapping."""
    arcs = source["travelSnapshots"][0]["arcs"]
    nodes = sorted({arc["fromNodeId"] for arc in arcs} | {arc["toNodeId"] for arc in arcs})
    node_index = {node_id: index for index, node_id in enumerate(nodes)}
    base = root / "base"
    base.mkdir(parents=True)
    with (base / "nodes.csv").open("x", encoding="utf-8", newline="") as target:
        writer = csv.writer(target, lineterminator="\n")
        writer.writerow(["node_index", "is_stop_only", "pos_x", "pos_y"])
        for index in range(len(nodes)):
            writer.writerow([index, "True", index * 1000, 0])
    with (base / "edges.csv").open("x", encoding="utf-8", newline="") as target:
        writer = csv.writer(target, lineterminator="\n")
        writer.writerow(["from_node", "to_node", "distance", "travel_time", "source_edge_id"])
        for ordinal, arc in enumerate(arcs):
            seconds = arc["travelTimeMs"] / 1000
            writer.writerow([node_index[arc["fromNodeId"]], node_index[arc["toNodeId"]], seconds * 10, seconds,
                             ordinal])
    with (base / "crs.info").open("x", encoding="ascii", newline="") as target:
        target.write("EPSG:32632\n")
    mapping_hash = hashlib.sha256(b"RideBound.Wp7MediumNodeMap.v1\0" + canonical(node_index)).hexdigest()
    return node_index, mapping_hash


def check_closure(source, node_index):
    """Every ordered node pair has an arc and no two-arc path is shorter (integer ms), so the direct edge is a
    shortest path and the complete-graph router may take it."""
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


def check_routing(routing, source, node_index):
    """FleetPy's router gives every arc its scenario travel time."""
    for arc in source["travelSnapshots"][0]["arcs"]:
        origin, destination = (node_index[arc["fromNodeId"]], None, None), (node_index[arc["toNodeId"]], None, None)
        actual_ms = round(routing.return_travel_costs_1to1(origin, destination)[1] * 1000)
        if actual_ms != arc["travelTimeMs"]:
            raise RuntimeError(f"metric closure drift {arc['fromNodeId']}->{arc['toNodeId']}: "
                               f"expected={arc['travelTimeMs']}; actual={actual_ms}")


def source_requests(source):
    """The scenario's requests in event order, each checked against its event record."""
    result = []
    for event in source["events"]:
        if event["eventType"] != "requestArrived":
            raise RuntimeError(f"unsupported source event {event['eventType']!r}")
        payload_bytes = bytes.fromhex(event["payloadCanonicalJsonHex"])
        if hashlib.sha256(payload_bytes).hexdigest() != event["payloadSha256"]:
            raise RuntimeError("source event payload hash mismatch")
        request = json.loads(payload_bytes, object_pairs_hook=strict_object)["request"]
        if request["requestId"] != event["stableSubjectId"]:
            raise RuntimeError("source request identity mismatch")
        if request["arrivalTimeMs"] != event["simTimeMs"]:
            raise RuntimeError("source request time mismatch")
        result.append(request)
    return result


class PublicRequest:
    """A scenario request in FleetPy's request contract (seconds)."""

    def __init__(self, source, node_index):
        for field in ["arrivalTimeMs", "earliestPickupMs", "latestPickupMs"]:
            if source[field] % 1000 != 0:
                raise RuntimeError(f"{source['requestId']}: {field} is not whole seconds")
        self.rid = source["requestId"]
        self.nr_pax = source["partySize"]
        self.rq_time = source["arrivalTimeMs"] // 1000
        self.o_pos = (node_index[source["originNodeId"]], None, None)
        self.d_pos = (node_index[source["destinationNodeId"]], None, None)
        self.earliest_start_time = source["earliestPickupMs"] // 1000
        self.latest_start_time = source["latestPickupMs"] // 1000
        # FleetPy accepts fractional seconds here: keep the exact millisecond bound.
        self.max_trip_time = source["maxRideTimeMs"] / 1000
        self.pu_time = None
        self.is_parcel = False

    def get_rid_struct(self):
        return self.rid

    def get_rid(self):
        return self.rid


def assert_request_round_trip(control, source, node_index):
    """The protocol request the control builds from FleetPy's request equals the scenario request."""
    mapped = control._rb_mapper.request(control.rq_dict[source["requestId"]], control._rb_settings.service_class,
                                        control._rb_settings.commitment_policy_id)
    expected = {"arrivalTimeMs": source["arrivalTimeMs"], "earliestPickupMs": source["earliestPickupMs"],
                "latestPickupMs": source["latestPickupMs"], "maxRideTimeMs": source["maxRideTimeMs"],
                "partySize": source["partySize"], "serviceClass": SERVICE_CLASS,
                "commitmentPolicyId": COMMITMENT_POLICY_ID}
    if any(mapped[key] != value for key, value in expected.items()):
        raise RuntimeError(f"{source['requestId']}: FleetPy PlanRequest scalar round-trip drift")
    if (control._rb_mapper.raw_request(mapped["requestId"]) != source["requestId"]
            or control._rb_mapper.raw_node(mapped["originNodeId"]) != node_index[source["originNodeId"]]
            or control._rb_mapper.raw_node(mapped["destinationNodeId"]) != node_index[source["destinationNodeId"]]):
        raise RuntimeError(f"{source['requestId']}: FleetPy PlanRequest identity/node round-trip drift")


def operator_settings(args, artifacts, maximum_nodes, transcript):
    """The fleet control's operator settings: Runner, configurations, files to hash, and the run's wire names."""
    return {
        "op_vr_control_func_dict": "ridebound-external-runner",  # FleetPy's G_OP_VR_CTRL_F
        "op_fleet_composition": FLEET_NAME,  # FleetPy's G_OP_FLEET
        "ridebound_dotnet_path": str(args.dotnet.resolve()),
        "ridebound_runner_root": str(args.runner_root.resolve()),
        "ridebound_commitment_config": str(args.commitment_config.resolve()),
        "ridebound_wp4_config": str(args.wp4_config.resolve()),
        "ridebound_fleetpy_root": str(args.fleetpy_root.resolve()),
        "ridebound_repository_root": str(REPOSITORY_ROOT),
        "ridebound_scenario_root": str(SCENARIO_ROOT),
        "ridebound_additional_artifacts": [str(path) for path in artifacts],
        "ridebound_run_id": RUN_ID_PREFIX + args.label,
        "ridebound_scenario_id": SCENARIO_ID,
        "ridebound_master_seed": args.master_seed,
        "ridebound_service_class": SERVICE_CLASS,
        "ridebound_commitment_policy_id": COMMITMENT_POLICY_ID,
        "ridebound_timeout_seconds": RUNNER_TIMEOUT_SECONDS,
        "ridebound_maximum_line_bytes": MAXIMUM_LINE_BYTES,
        "ridebound_complete_travel_snapshot_maximum_nodes": maximum_nodes,
        "ridebound_transcript_path": str(transcript),
        "op_min_wait_time": 0,
        "op_max_wait_time": 600,
        "op_const_boarding_time": 0,
    }


# ---- the event clock ----

def report_vehicle(control, vehicle, requests, simulation_time, boarding, alighting, passed):
    """Tell the control what one vehicle did: its boardings and alightings, then its status."""
    for rid, (boarding_time, _position) in boarding.items():
        requests[rid].pu_time = boarding_time
        control.acknowledge_boarding(rid, vehicle.vid, boarding_time)
    for rid, alighting_time in alighting.items():
        control.acknowledge_alighting(rid, vehicle.vid, alighting_time)
    control.receive_status_update(vehicle.vid, simulation_time, passed, bool(boarding or alighting))


def move_vehicles(control, vehicles, requests, start_time, end_time):
    if end_time < start_time:
        raise RuntimeError("medium event clock regressed")
    for vehicle in vehicles:
        boarding, alighting, passed, _started_alighting = vehicle.update_veh_state(start_time, end_time)
        report_vehicle(control, vehicle, requests, end_time, boarding, alighting, passed)


def advance_immediate_vehicle_legs(control, vehicles, requests, simulation_time):
    """Start newly assigned legs and close zero-duration service legs at this instant. FleetPy's own update needs a
    positive step for that, and the Runner plans stops of zero duration. Returns (a vehicle changed, the control was
    told)."""
    from src.misc.globals import G_DRIVING_STATUS

    changed = notified = False
    for vehicle in vehicles:
        boarding, alighting, passed = {}, {}, []
        if vehicle.start_next_leg_first:
            boarded, _started_alighting = vehicle.start_next_leg(simulation_time)
            vehicle.start_next_leg_first = False
            boarding.update({rid: (simulation_time, vehicle.pos) for rid in boarded})
            changed = True
        elif vehicle.assigned_route and vehicle.status not in G_DRIVING_STATUS and vehicle.cl_remaining_time == 0:
            alighted, finished = vehicle.end_current_leg(simulation_time)
            alighting.update({rid: simulation_time for rid in alighted})
            if finished:
                passed.append(finished)
            if vehicle.assigned_route:
                boarded, _started_alighting = vehicle.start_next_leg(simulation_time)
                boarding.update({rid: (simulation_time, vehicle.pos) for rid in boarded})
            changed = True
        if boarding or alighting or passed:
            report_vehicle(control, vehicle, requests, simulation_time, boarding, alighting, passed)
            notified = True
    return changed, notified


def next_physical_boundary(vehicles, routing, simulation_time):
    """The first time a vehicle reaches the end of its current leg, or None when no vehicle has a route."""
    from src.misc.globals import G_DRIVING_STATUS

    boundaries = []
    for vehicle in vehicles:
        if not vehicle.assigned_route:
            continue
        if vehicle.start_next_leg_first:
            boundaries.append(simulation_time)
            continue
        if vehicle.status in G_DRIVING_STATUS:
            travel_time = routing.return_travel_costs_1to1(vehicle.pos, vehicle.assigned_route[0].destination_pos)[1]
            if travel_time < 0:
                raise RuntimeError(f"vehicle {vehicle.vid}: negative remaining travel time")
            boundaries.append(simulation_time + travel_time)
            continue
        remaining = vehicle.cl_remaining_time
        if remaining is None:
            raise RuntimeError(f"vehicle {vehicle.vid}: active non-driving leg has no duration")
        if remaining < 0:
            raise RuntimeError(f"vehicle {vehicle.vid}: negative remaining service time")
        boundaries.append(simulation_time + remaining)
    return min(boundaries) if boundaries else None


def resolve_offers(control, request_ids, simulation_time):
    """Riders take every offer the control made and accept every rejection."""
    confirmed, rejected = [], []
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


def trigger_and_resolve(control, request_ids, request_db, simulation_time, confirmed_ids, rejected_ids):
    """One control trigger, then up to 4 more while riders confirm new offers; closed requests leave request_db."""
    control.time_trigger(simulation_time)
    for _microstep in range(4):
        confirmed, rejected = resolve_offers(control, request_ids, simulation_time)
        confirmed_ids.update(confirmed)
        rejected_ids.update(rejected)
        if not confirmed:
            break
        control.time_trigger(simulation_time)
    else:
        raise RuntimeError("confirmation microstep did not converge")
    for rid in list(request_db):
        if control._rb_states.get(rid) == "closed":
            request_db.pop(rid, None)


def drive(control, vehicles, routing, world, requests, request_db, node_index, driver):
    """Run the event clock from time 0 until every request is closed and the fleet is empty.

    The clock stops at request arrivals, at the end of a vehicle leg, at every bin start and, when no request is left
    to arrive, every drainStepSeconds. At each stop the fleet moves there, new requests arrive and the control is
    triggered. Returns the final time and the sets of accepted and rejected request ids."""
    accepted, rejected = set(), set()
    all_ids = {request["requestId"] for request in requests}
    arrivals = collections.defaultdict(list)
    for request in requests:
        arrivals[request["arrivalTimeMs"] // 1000].append(request)
    arrival_times = sorted(arrivals)

    def trigger(simulation_time):
        world.update(control, simulation_time)
        trigger_and_resolve(control, all_ids, request_db, simulation_time, accepted, rejected)

    now, next_arrival_index, drain_steps = 0, 0, 0
    trigger(0)
    while True:
        # Start new legs and close zero-duration legs at this instant, so that a boarding keeps its own time.
        for _immediate_step in range(4096):
            changed, notified = advance_immediate_vehicle_legs(control, vehicles, request_db, now)
            if notified:
                trigger(now)
            if not changed:
                break
        else:
            raise RuntimeError("immediate FleetPy leg closure did not converge")
        active = any(state != "closed" for state in control._rb_states.values())
        physical = any(vehicle.assigned_route or vehicle.pax for vehicle in vehicles)
        next_arrival = arrival_times[next_arrival_index] if next_arrival_index < len(arrival_times) else None
        if next_arrival is None and not active and not physical:
            return now, accepted, rejected
        boundary = clock_boundary(next_physical_boundary(vehicles, routing, now), now)
        if boundary <= now:
            raise RuntimeError("non-positive physical boundary remained after immediate closure")
        timer = None
        if next_arrival is None:  # a request is still open or a vehicle still moves
            if drain_steps >= driver["maximumDrainSteps"]:
                raise RuntimeError("medium drain budget exhausted")
            timer = now + driver["drainStepSeconds"]
        next_time = min(value for value in (next_arrival, boundary, timer) if value is not None)
        move_vehicles(control, vehicles, request_db, now, next_time)
        if next_arrival is not None and next_time == next_arrival:
            for value in arrivals[next_arrival]:
                request = PublicRequest(value, node_index)
                request_db[request.rid] = request
                control.user_request(request, next_arrival)
                assert_request_round_trip(control, value, node_index)
            next_arrival_index += 1
        trigger(next_time)
        if timer is not None and next_time == timer:
            drain_steps += 1
        now = next_time


def check_drained(control, vehicles, request_db, requests, accepted, rejected):
    """Every request ended accepted or rejected, every index is empty and every accepted request got its promise."""
    state_counts = collections.Counter(control._rb_states.values())
    if state_counts != {"closed": len(requests)}:
        raise RuntimeError(f"non-terminal medium states: {state_counts!r}")
    if accepted & rejected:
        raise RuntimeError("request appears in accepted and rejected sets")
    if accepted | rejected != {request["requestId"] for request in requests}:
        raise RuntimeError("request outcome conservation failed")
    if request_db or control.rq_dict or control.rid_to_assigned_vid:
        raise RuntimeError("request database/index did not drain")
    if any(vehicle.assigned_route or vehicle.pax for vehicle in vehicles):
        raise RuntimeError("physical FleetPy fleet did not drain")
    if any(route.remaining_stops for route in control._rb_routes.values()):
        raise RuntimeError("protocol fleet did not drain")
    initial = [item for item in control._rb_publications if item["reasonCode"] == "INITIAL_BOOKING_CONFIRMATION"]
    if len(initial) != len(accepted):
        raise RuntimeError("accepted/initial-promise conservation failed")


def run_fleet(args, source, driver, values, output, source_hash):
    """Run the cell through FleetPy with the Runner deciding, then take the final checkpoint; returns run-00.json."""
    import psutil
    from src.misc.globals import G_DIR_DATA, G_DIR_OUTPUT, G_SKIP_OUTPUT
    from src.simulation.Vehicles import SimulationVehicle
    from src.misc.init_modules import load_fleet_control_module
    from ridebound_fleetpy.mapping import seconds_to_milliseconds
    from ridebound_fleetpy.direct_network import CompleteGraphDirectNetwork

    started = time.perf_counter()
    process = psutil.Process()
    cpu_before, rss_before = process.cpu_times(), process.memory_info().rss
    with tempfile.TemporaryDirectory(prefix="ridebound-job-") as temporary:
        temporary = pathlib.Path(temporary)
        node_index, node_mapping_hash = materialize_network(temporary / "network", source)
        check_closure(source, node_index)
        routing = CompleteGraphDirectNetwork(str(temporary / "network"))
        check_routing(routing, source, node_index)
        requests, request_db = source_requests(source), {}
        vehicles = [SimulationVehicle(0, index, str(VEHICLE_DIRECTORY), "wp7_tiny", routing, request_db, [], False,
                                      False) for index in range(len(source["fleet"]))]
        for vehicle, start in zip(vehicles, source["fleet"], strict=True):
            vehicle.pos = (node_index[start["position"]["nodeId"]], None, None)
            vehicle.soc = 1.0
        script = pathlib.Path(__file__).resolve()
        artifacts = [args.scenario.resolve(), args.derivative_manifest.resolve(), args.normalization_report.resolve(),
                     args.selection_frame.resolve(), VEHICLE_DIRECTORY / "wp7_tiny.csv", script,
                     script.with_name("actual_fleetpy_medium_verify.py")]
        operator = operator_settings(args, artifacts, driver["completeTravelSnapshotMaximumNodes"],
                                     output / "transcript-00.ndjson.gz")
        scenario = {"n_cpu_per_sim": 1, "start_time": 0, G_SKIP_OUTPUT: 1, "scenario_name": SCENARIO_ID}
        directories = {G_DIR_DATA: str(temporary), G_DIR_OUTPUT: str(temporary)}
        control = load_fleet_control_module("RideBoundFleetControl")(0, operator, vehicles, routing, None, scenario,
                                                                     directories)
        try:
            world = WorldTravelTimes(routing, values)
            now, accepted, rejected = drive(control, vehicles, routing, world, requests, request_db, node_index,
                                            driver)
            check_drained(control, vehicles, request_db, requests, accepted, rejected)
            control.checkpoint_runner()
            semantic = {
                "sourceScenarioContentSha256": source_hash,
                "nodeMappingHash": node_mapping_hash,
                "manifestHash": control._rb_session.manifest_hash,
                "checkpointBindingHash": control._rb_checkpoint_binding_hash,
                "requestCount": len(requests),
                "acceptedRequestIds": sorted(accepted),
                "rejectedRequestIds": sorted(rejected),
                "publicationCount": len(control._rb_publications),
                "publicationDigest": hashlib.sha256(canonical(control._rb_publications)).hexdigest(),
                "travelSnapshotVersion": control._rb_travel_version,
                "nextEventSeq": control._rb_events.next_event_sequence,
                "nextEpoch": control._rb_events.next_epoch,
                "finalSimulationTimeMs": seconds_to_milliseconds(now, "$.medium.finalSimulationTime"),
                "finalVehiclePositions": [list(vehicle.pos) for vehicle in vehicles],
            }
            semantic_hash = hashlib.sha256(b"RideBound.Wp7ActualFleetPyMedium.v1\0" + canonical(semantic)).hexdigest()
        except BaseException:
            # The Runner answers an unexpected exception with a generic error and writes the detail to its stderr.
            print(f"[runner-stderr]\n{control._rb_session._client.diagnostics}\n[end runner-stderr]", file=sys.stderr,
                  flush=True)
            raise
        finally:
            control.close()
        receipts = control._rb_session.artifact_receipts
        # The control registered close() to run at exit, which would keep it, and the final checkpoint it holds, in
        # memory while the evidence is read. It is closed now, so let it go.
        atexit.unregister(control.close)
    cpu_after = process.cpu_times()
    return {"schemaVersion": "1.0.0", "status": "succeeded", "label": args.label, "repeat": 0,
            "semanticHash": semantic_hash, "semantic": semantic, "artifactCount": len(receipts["before"]),
            "artifactReceiptsEqual": receipts["before"] == receipts["after"],
            "resources": {"wallMilliseconds": round((time.perf_counter() - started) * 1000),
                          "userCpuMilliseconds": round((cpu_after.user - cpu_before.user) * 1000),
                          "systemCpuMilliseconds": round((cpu_after.system - cpu_before.system) * 1000),
                          "rssBeforeBytes": rss_before, "rssAfterBytes": process.memory_info().rss}}


# ---- evidence: what the rider tables read from the transcript ----

class PlanMoves:
    """G2 moves by plan membership: after each Runner decision, a request belongs to the vehicle whose route holds its
    pickup stop; a move is a request that belongs to a vehicle before and after a decision, and the two differ."""

    def __init__(self, job):
        self.job, self.routes, self.owner, self.moves, self.most, self.decisions = job, {}, {}, [], 0, 0

    def add(self, envelope):
        self.decisions += 1
        for action in envelope["payload"]["actions"]:
            if action["decisionType"] == "vehiclePlanUpdated":
                self.routes[action["payload"]["vehicleId"]] = action["payload"]["route"]
        after = {}
        for vehicle, route in self.routes.items():
            for stop in route["frozenPrefix"] + route["mutableSuffix"]:
                if stop["kind"] == "pickup":
                    if stop["requestId"] in after:
                        raise RuntimeError(f"{self.job}: pickup of {stop['requestId']} on two vehicles")
                    after[stop["requestId"]] = vehicle
        here = [(rq, self.owner[rq], vehicle, envelope["simTimeMs"]) for rq, vehicle in after.items()
                if rq in self.owner and self.owner[rq] != vehicle]
        self.moves.extend(here)
        self.most = max(self.most, len(here))
        self.owner = after

    def result(self):
        return {"job": self.job, "moves": len(self.moves), "maxMovesPerDecision": self.most,
                "decisions": self.decisions, "firstMoves": self.moves[:3]}


def shadow_allocations(frame):
    """requestId -> (class, score) from a checkpoint frame's shadowAllocations array ({} without one); only the array
    is parsed."""
    k = frame.rfind(b'"shadowAllocations":[')
    if k < 0:
        return {}
    k += len(b'"shadowAllocations":')
    return {a["requestId"]: (a["class"], a["score"]) for a in json.loads(frame[k:frame.index(b"]", k) + 1])}


def read_transcript(path, job):
    """One pass over the gzip transcript; every frame is checked against its record. Returns the decisions and the
    acknowledged decision hashes, the last checkpoint's online state, the first boarding and alighting time of every
    rider, the G2 moves, the last Shadow allocations and the transcript receipt."""
    received, decisions, acknowledged, state, shadow = [], {}, set(), None, None  # received: one row per frame
    boarded, alighted, moves = {}, {}, PlanMoves(job)
    raw, raw_bytes = hashlib.sha256(), 0
    with gzip.open(path, "rb") as handle:
        for number, line in enumerate(handle):
            raw.update(line)
            raw_bytes += len(line)
            record = json.loads(line)
            frame = base64.b64decode(record["frameBase64"])
            digest = hashlib.sha256(frame).hexdigest()
            if digest != record["frameSha256"]:
                raise RuntimeError(f"{path.with_suffix('')} line {number}: frame hash mismatch")
            if len(frame) != record["frameLengthBytes"]:
                raise RuntimeError(f"{path}: frame {record['ordinal']} does not match its record")
            direction = record["direction"]
            received.append([record["ordinal"], direction, record["frameLengthBytes"], digest])
            envelope = json.loads(ARCS.sub(b'"arcs":[],"snapshotHash"', frame) if len(frame) > BIG_FRAME else frame)
            kind = envelope.get("messageType")
            if direction == "adapterToRunner" and kind == "decisionApplied":
                acknowledged.add(envelope["payload"]["decisionHash"])
            elif direction == "adapterToRunner" and kind == "eventBatch":
                for event in envelope["payload"].get("events", []):
                    if event.get("eventType") == "passengerBoarded":
                        boarded.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
                    elif event.get("eventType") == "passengerAlighted":
                        alighted.setdefault(event["payload"]["requestId"], envelope["simTimeMs"])
            elif direction == "runnerToAdapter" and kind == "decision":
                decisions[envelope["payload"]["decisionHash"]] = envelope["payload"]
                moves.add(envelope)
            elif direction == "runnerToAdapter" and kind == "checkpoint":
                state = envelope["payload"]["content"]["onlineState"]
            # The Shadow allocations are read from the frame bytes of the record line, as the Shadow reader does.
            if line.startswith(b'{"direction":"runnerToAdapter"') and b'"messageType":"checkpoint"' in frame[:400]:
                shadow = shadow_allocations(frame)
    if [row[0] for row in received] != list(range(1, len(received) + 1)):
        raise RuntimeError(f"{path}: ordinals are not 1..n")
    receipt = {"file": path.name, "fileSha256": sha256_file(path), "fileBytes": path.stat().st_size,
               "uncompressedSha256": raw.hexdigest(), "uncompressedBytes": raw_bytes, "frameCount": len(received),
               "frames": received}
    return decisions, acknowledged, state, {"boarded": boarded, "alighted": alighted}, moves.result(), shadow, receipt


def published_actions(decisions, acknowledged):
    """publicationId -> payload of every promisePublished action of an acknowledged decision."""
    out = {}
    for decision_hash, payload in decisions.items():
        if decision_hash not in acknowledged:
            continue
        for action in payload.get("actions", []):
            if action.get("decisionType") != "promisePublished":
                continue
            value = action["payload"]
            if value["publicationId"] in out:
                raise RuntimeError(f"publication {value['publicationId']} published twice")
            out[value["publicationId"]] = value
    return out


@dataclasses.dataclass
class Totals:
    """One rider's ETA (pickup or drop) over its committed publications."""
    p0: int  # the initial promise
    last_p: int  # the last published value while the ETA was open
    chain_p: int  # the last published value: the next publication's v
    C: int = 0  # sum |p - e|, the decision-induced changes
    X: int = 0  # sum |e - v|, the exogenous changes
    V: int = 0  # sum |p - v|, the visible changes
    M: int = 0  # max |p - p0|
    Mplus: int = 0  # max(0, p - p0) at its largest


def accumulate(rows):
    """Totals per (request, ETA) from the audit rows in sequence order. A pickup ETA closes at the boarding: later
    publications move the chain but add nothing. Refuses rows that are not whole canonical milliseconds, that skip the
    initial promise or break the chain v = previous p, and totals that break V <= C + X or |last p - p0| <= M <= V."""
    out, last_sequence = {}, {}
    for request_id, coordinate, sequence, v, e, p, initial, closed in sorted(rows, key=lambda row: row[:3]):
        for name, value in (("v", v), ("e", e), ("p", p)):
            if not isinstance(value, int) or isinstance(value, bool):
                raise RuntimeError(f"{name} of {request_id}/{coordinate}#{sequence} is not an integer")
            if value < 0 or value > MAX_CANONICAL:
                raise RuntimeError(f"{name} of {request_id}/{coordinate}#{sequence} outside canonical range")
        key = (request_id, coordinate)
        if key in last_sequence and sequence <= last_sequence[key]:
            raise RuntimeError(f"duplicate or out-of-order committed row {key}#{sequence}")
        last_sequence[key] = sequence
        totals = out.get(key)
        if totals is None:
            if not initial or not (v == e == p):
                raise RuntimeError(f"first committed row of {key} must be the initial promise (v = e = p)")
            out[key] = Totals(p, p, p)
            continue
        if initial:
            raise RuntimeError(f"second initial promise for {key}#{sequence}")
        if v != totals.chain_p:
            raise RuntimeError(f"{key}#{sequence}: v={v} is not the previous published p={totals.chain_p}")
        totals.chain_p = p
        if closed:
            continue
        totals.C += abs(p - e)
        totals.X += abs(e - v)
        totals.V += abs(p - v)
        totals.last_p = p
        totals.Mplus = max(totals.Mplus, max(0, p - totals.p0))
        totals.M = max(totals.M, abs(p - totals.p0))
    for totals in out.values():
        if not (totals.V <= totals.C + totals.X and abs(totals.last_p - totals.p0) <= totals.M <= totals.V
                and totals.Mplus <= totals.M):
            raise RuntimeError(f"invariant broken: {totals}")
    return out


def audit(decisions, acknowledged, state, transcript):
    """The signed audit rows (one per ledger entry and ETA: v = the promise before, e = the kept route, p = the
    published value) rebuilt from the final ledger, and the job summary. Every ledger publication must be an
    acknowledged promisePublished action with the same p, and |e - v|, |p - e|, |p - v| must equal the ledger deltas."""
    requests = {request["requestId"]: request for request in state["requests"]}
    actions = published_actions(decisions, acknowledged)
    rows, table, seen = [], [], set()
    for history in state["commitmentLedger"]:
        request_id = history["requestId"]
        boarded = requests.get(request_id, {}).get("actualPickupTimeMs")
        for sequence, entry in enumerate(history["entries"]):
            published, publication = entry["publishedPromise"], entry["publicationId"]
            seen.add(publication)
            action = actions.get(publication)
            if action is None:
                raise RuntimeError(f"{request_id}: ledger publication {publication} has no acknowledged action")
            for coordinate, eta, dimension in COORDINATES:
                p = published["projection"][eta]
                if action["promise"][eta] != p:
                    raise RuntimeError(f"{request_id}/{coordinate}: action p differs from ledger p")
                initial = entry["kind"] == "initialPromise"
                if initial:
                    v = e = p
                else:
                    v, e = entry["previousPromise"]["projection"][eta], entry["exogenousProjection"][eta]
                    for name, value in (("exogenous", abs(e - v)), ("decisionInduced", abs(p - e)),
                                        ("visible", abs(p - v))):
                        if entry["deltas"][name][dimension] != value:
                            raise RuntimeError(f"{request_id}/{coordinate}#{sequence}: |{name}| ledger="
                                               f"{entry['deltas'][name][dimension]} recomputed={value}")
                closed = (coordinate == "pickup" and boarded is not None
                          and published["publishedAtMs"] >= boarded and not initial)
                rows.append((request_id, coordinate, sequence, v, e, p, initial, closed))
                table.append((request_id, coordinate, sequence, entry["kind"], published["publishedAtMs"],
                              v, e, p, e - v, p - e, p - v, int(closed)))
    missing = set(actions) - seen
    if missing:
        raise RuntimeError(f"{len(missing)} acknowledged publications are not in the final ledger")
    return table, summarize(transcript, state, accumulate(rows), decisions, acknowledged)


def summarize(transcript, state, totals, decisions, acknowledged):
    lifecycle = collections.Counter(request["lifecycle"] for request in state["requests"])
    incidents = state["incidentLedger"]
    breach_kinds = collections.Counter(b.get("kind", "operationalIncident") for b in incidents["breaches"])
    late = incidents.get("latePickups", [])
    non_normal = sum(1 for payload in decisions.values() if payload["decisionHash"] in acknowledged
                     and (payload.get("certificate", {}).get("body") or {}).get("normalOperation") is False)
    drop_v = sorted(t.V for (_request, coordinate), t in totals.items() if coordinate == "drop")
    tail = drop_v[(95 * len(drop_v)) // 100:] if drop_v else []  # the worst 5 %, by an exact integer index
    requests = len(state["requests"])
    served = lifecycle.get("completed", 0)
    return {
        "transcript": str(transcript),
        "finalCheckpoint": True,
        "requests": requests,
        "lifecycle": dict(sorted(lifecycle.items())),
        "E1ServedShare": served / requests if requests else None,
        "promisedRiders": len(drop_v),
        "E2ShareDropVAbove60s": sum(1 for value in drop_v if value > 60_000) / len(drop_v) if drop_v else None,
        "dropVCvar95Ms": sum(tail) / len(tail) if tail else None,
        "sumDropC": sum(t.C for (_r, c), t in totals.items() if c == "drop"),
        "sumDropX": sum(t.X for (_r, c), t in totals.items() if c == "drop"),
        "sumDropV": sum(drop_v),
        "sumPickupC": sum(t.C for (_r, c), t in totals.items() if c == "pickup"),
        "sumPickupX": sum(t.X for (_r, c), t in totals.items() if c == "pickup"),
        "sumPickupV": sum(t.V for (_r, c), t in totals.items() if c == "pickup"),
        "decisionsAcknowledged": len(acknowledged & set(decisions)),
        "nonNormalCertificates": non_normal,
        "breaches": dict(sorted(breach_kinds.items())),
        "ridersWithForcedBreach": len({b["requestId"] for b in incidents["breaches"]
                                       if b.get("kind") == "forcedReference"}),
        "latePickups": len(late),
        "latePickupMaxMs": max((p["actualPickupMs"] - p["latestPickupMs"] for p in late), default=0),
    }


def rider_rows(folder, evidence):
    """The job's rows of the rider table, without role and cell: requestId, lifecycle, t0, p0, D0 (empty), A, served,
    sumAbsC, nonzeroC, netC, maxAbsC, shared. t0 and p0 are the drop initial promise; A is the first alighting; c the
    drop revisions' p - e; shared means another rider was on board of the same vehicle at the same time. Refuses a job
    whose per-rider sums disagree with the summary or the ledger."""
    table, state = evidence["audit"]["table"], evidence["finalOnlineState"]
    alighted = evidence["events"]["alighted"]
    first, c_by_seq = {}, {}
    s_drop, k_drop, net, max_c = (collections.defaultdict(int) for _ in range(4))
    for r, coordinate, seq, kind, published_at, _v, _e, p, _x, c, _z, _closed in table:
        if coordinate != "drop":
            continue
        c_by_seq[(r, seq)] = 0 if kind == "initialPromise" else c
        if kind == "initialPromise":
            if r in first:
                raise RuntimeError(f"{folder}: {r} has two drop initialPromise rows")
            first[r] = (published_at, p)
            continue
        s_drop[r] += abs(c)
        k_drop[r] += c != 0
        net[r] += c
        max_c[r] = max(max_c[r], abs(c))
    if sum(s_drop.values()) != evidence["audit"]["summary"]["sumDropC"]:
        raise RuntimeError(f"{folder}: per-rider sum |c| != audit sumDropC")
    for history in state["commitmentLedger"]:
        r = history["requestId"]
        spent = sum(abs(c_by_seq[(r, seq)]) for seq in range(len(history["entries"])))
        if r in first and spent != s_drop[r]:
            raise RuntimeError(f"{folder}: {r} ledger running sum {spent} != {s_drop[r]}")
    life = {q["requestId"]: q["lifecycle"] for q in state["requests"]}
    served = {r for r, value in life.items() if value == "completed"} & set(alighted)
    rides = collections.defaultdict(list)
    for q in state["requests"]:
        if q["requestId"] in alighted:
            rides[q["assignedVehicleId"]].append((q["actualPickupTimeMs"], alighted[q["requestId"]], q["requestId"]))
    shared = set()
    for vehicle_rides in rides.values():
        for a0, a1, rid in vehicle_rides:
            if any(o != rid and b0 < a1 and a0 < b1 for b0, b1, o in vehicle_rides):
                shared.add(rid)
    return [[r, life.get(r, ""), first[r][0], first[r][1], "", alighted.get(r, ""), int(r in served), s_drop[r],
             k_drop[r], net[r], max_c[r], int(r in shared)] for r in sorted(first)]


def write_evidence(folder, role):
    """Write evidence.json.gz (gzip, mtime 0) and EVIDENCE.sha256 from the folder's transcript, then read the file
    back: the rider rows, moves, audit table and Shadow allocations must come out the same as from the transcript.
    Returns a one-line report."""
    transcript = folder / "transcript-00.ndjson.gz"
    decisions, acknowledged, state, events, moves, shadow, receipt = read_transcript(transcript, folder.name)
    if state is None:
        raise RuntimeError(f"{folder}: no final checkpoint")
    table, summary = audit(decisions, acknowledged, state, folder / "transcript-00.ndjson")
    evidence = {"format": EVIDENCE_FORMAT, "job": folder.name, "role": role,
                "audit": {"table": [list(row) for row in table], "summary": summary}, "finalOnlineState": state,
                "events": events, "moves": moves, "shadowAllocations": shadow, "transcript": receipt}
    rows = rider_rows(folder, evidence)
    buffer = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", fileobj=buffer, compresslevel=9, mtime=0) as handle:
        handle.write(json.dumps(evidence, sort_keys=True, separators=(",", ":")).encode("utf-8"))
    data = buffer.getvalue()
    digest = hashlib.sha256(data).hexdigest()
    with open(folder / EVIDENCE, "xb") as target:
        target.write(data)
    with open(folder / "EVIDENCE.sha256", "x", encoding="utf-8", newline="\n") as target:
        target.write(f"{digest} *{EVIDENCE}\n")

    stored = (folder / EVIDENCE).read_bytes()
    if hashlib.sha256(stored).hexdigest() != digest:
        raise RuntimeError(f"{folder}: evidence.json.gz does not match EVIDENCE.sha256")
    back = json.loads(gzip.decompress(stored))
    if back.get("format") != EVIDENCE_FORMAT or back.get("job") != folder.name:
        raise RuntimeError(f"{folder}: not this job's evidence")
    for name, from_transcript, from_evidence in (
            ("rows", rows, rider_rows(folder, back)), ("moves", moves, back["moves"]),
            ("audit", evidence["audit"]["table"], back["audit"]["table"]),
            ("shadow", shadow, back["shadowAllocations"])):
        if json.dumps(from_transcript, sort_keys=True) != json.dumps(from_evidence, sort_keys=True):
            raise RuntimeError(f"{folder.name}: {name} from the evidence differs from the transcript; transcript kept")
    return {"job": folder.name, "evidenceBytes": len(stored), "evidenceSha256": digest, "riders": len(rows),
            "moves": moves["moves"], "shadowAllocations": None if shadow is None else len(shadow)}


def write_bundle_manifest(folder):
    files = [{"path": path.relative_to(folder).as_posix(), "lengthBytes": path.stat().st_size,
              "sha256": sha256_file(path)}
             for path in sorted((p for p in folder.rglob("*") if p.is_file()),
                                key=lambda p: p.relative_to(folder).as_posix())]
    write_new(folder / "bundle-manifest.json", {"schemaVersion": "1.0.0",
                                                "bundleType": "ridebound-wp7-actual-fleetpy-medium-v1",
                                                "files": files})


# ---- the job ----

def read_inputs(args):
    """The scenario and the run driver, checked against each other; returns them and the scenario's SHA-256."""
    source, driver = read_json(args.scenario.resolve()), read_json(args.driver.resolve())
    source_hash = sha256_file(args.scenario.resolve())
    if source_hash != driver["sourceScenarioContentSha256"]:
        raise RuntimeError(f"source scenario hash mismatch: {source_hash}")
    if driver.get("routingEngine") != "complete-graph-direct-v1" or driver.get("transcriptCompression") != "gzip-v1":
        raise RuntimeError("only full-node run drivers (routingEngine complete-graph-direct-v1, transcriptCompression "
                           f"gzip-v1) are run; other drivers: {REMOVED}")
    counts = {"requestCount": "expectedRequestCount", "vehicleCount": "expectedVehicleCount",
              "nodeCount": "expectedNodeCount", "directedArcCount": "expectedDirectedArcCount"}
    missing = [name for name in counts.values() if name not in driver]
    if missing:
        raise RuntimeError(f"the driver has no {', '.join(missing)}; drivers without the expected counts: {REMOVED}")
    expected = {key: driver[name] for key, name in counts.items()}
    summary = source["validationSummary"]
    if summary != {**summary, **expected}:
        raise RuntimeError(f"source cardinality drift: driver declared {expected}, scenario has "
                           + repr({key: summary[key] for key in expected}))
    if driver["completeTravelSnapshotMaximumNodes"] != summary["nodeCount"]:
        raise RuntimeError("complete travel snapshot bound must equal node count")
    return source, driver, source_hash


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--label", required=True, help='the run id in the frames is "wp7-medium-<label>"')
    parser.add_argument("--role", required=True, help="the arm, e.g. FS-G2-SHRC71-CL")
    for name in ("--fleetpy-root", "--runner-root", "--dotnet", "--commitment-config", "--wp4-config", "--scenario",
                 "--derivative-manifest", "--normalization-report", "--selection-frame", "--driver", "--world",
                 "--output"):
        parser.add_argument(name, required=True, type=pathlib.Path)
    parser.add_argument("--master-seed", type=int, default=7)
    parser.add_argument("--transcript", choices=("keep", "drop"), default="keep")
    args = parser.parse_args(argv)
    if not 0 <= args.master_seed <= 2_147_483_647:
        parser.error("--master-seed must be in the non-negative Int32 range")
    output = args.output.resolve()
    if not output.name.startswith(args.role + "-d") or not CELL.search(output.name):
        parser.error(f"--output {output.name}: the folder of a job is named <role>-<cell> (dYYYYMMDD-vf-rN-wHH)")

    values = read_world(args.world)
    sys.path.insert(0, str(ADAPTER_ROOT))
    sys.path.insert(0, str(args.fleetpy_root.resolve()))
    source, driver, source_hash = read_inputs(args)
    commit = git_head(REPOSITORY_ROOT)
    output.mkdir(parents=True, exist_ok=False)
    try:
        report = run_fleet(args, source, driver, values, output, source_hash)
        write_new(output / "run-00.json", report)
        after = git_head(REPOSITORY_ROOT)
        if after != commit:
            raise RuntimeError(f"the checkout's commit changed while the job ran ({commit} -> {after})")
        gc.collect()  # free the fleet's memory before the transcript is read
        evidence = write_evidence(output, args.role)
        if args.transcript == "drop":
            (output / "transcript-00.ndjson.gz").unlink()
        print(json.dumps({**evidence, "transcript": args.transcript, "selfCheck": "equal"}), flush=True)
    except BaseException as failure:
        write_new(output / "failure-00.json", {"schemaVersion": "1.0.0", "status": "failed", "label": args.label,
                                               "repeat": 0, "failureType": type(failure).__name__,
                                               "failureMessage": str(failure), "traceback": traceback.format_exc()})
        raise
    summary = {"schemaVersion": "1.0.0", "status": "pass", "label": args.label, "repeatCount": 1,
               "semanticHash": report["semanticHash"], "sourceScenarioContentSha256": source_hash,
               "runs": [{"repeat": 0, "semanticHash": report["semanticHash"], "resources": report["resources"]}]}
    write_new(output / "summary.json", summary)
    write_bundle_manifest(output)
    print(canonical(summary).decode("utf-8"), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
