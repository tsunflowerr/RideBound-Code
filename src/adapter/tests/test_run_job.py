"""run_job.py without FleetPy: the world factor and the clock boundary, the event clock and the travel-time update of
each bin, the settings the Runner session gets, the input checks, the evidence of a small hand-made job, the job's
refusals and the order of the job's output files.

The expected evidence is what the old evidence writer (finalize_job.py at commit ebca912) wrote for the same job.
"""
import argparse
import contextlib
import gzip
import hashlib
import io
import json
import pathlib
import sys
import tempfile
import types
import unittest
from unittest import mock

ADAPTER_ROOT = pathlib.Path(__file__).parents[1]
sys.path.insert(0, str(ADAPTER_ROOT))

import run_job  # noqa: E402
from ridebound_fleetpy.session import RideBoundProtocolSession, RideBoundSessionSettings  # noqa: E402
from ridebound_fleetpy.transcript import ProtocolTranscriptRecorder  # noqa: E402

ROLE, CELL = "FS-G2-SHRC71-CL", "d20190919-vf-r0-w08"
JOB = f"{ROLE}-{CELL}"
# requestId: (vehicle, lifecycle, boarding time, promises). A promise is (published at, pickup (e, p), drop (e, p));
# the first one is the initial promise, the others are revisions of the promise before.
RIDERS = {
    "rq-a": ("v1", "completed", 1000, [(100, (900, 900), (4800, 4800)), (600, (950, 950), (4900, 5100)),
                                       (2000, (1000, 1000), (5000, 4950))]),
    "rq-b": ("v1", "completed", 3000, [(200, (2800, 2800), (6900, 6900)), (2000, (2900, 2900), (6900, 7000))]),
    "rq-c": ("v2", "cancelled", None, [(300, (1500, 1500), (4000, 4000)), (600, (1600, 1550), (4300, 4100))]),
    "rq-d": ("v2", "completed", 2000, [(300, (1900, 1900), (3900, 3900))]),
}
ALIGHTED = [(4000, "rq-d"), (4500, "rq-d"), (4950, "rq-a"), (7000, "rq-b")]
# sha256 of the evidence JSON (keys sorted, compact) with the transcript path, file hash and file size set to "",
# and the evidence fields that the old writer gave.
EXPECTED_EVIDENCE_SHA256 = "019341f202eee22b7d975b048cdbfd1eda4e4cfaa3398859c3bed537fe24cd6a"
EXPECTED_MOVES = {"job": JOB, "moves": 1, "maxMovesPerDecision": 1, "decisions": 6,
                  "firstMoves": [["rq-c", "v2", "v1", 600]]}
EXPECTED_SHADOW = {"rq-a": ["guaranteed", "0.25"], "rq-b": ["allowed", "1.5"]}
EXPECTED_SUM_DROP_C = 550
# requestId, lifecycle, t0, p0, D0, A, served, sumAbsC, nonzeroC, netC, maxAbsC, shared (old rows_for, same job)
EXPECTED_ROWS = [["rq-a", "completed", 100, 4800, "", 4950, 1, 250, 2, 150, 200, 1],
                 ["rq-b", "completed", 200, 6900, "", 7000, 1, 100, 1, 100, 100, 1],
                 ["rq-c", "cancelled", 300, 4000, "", "", 0, 200, 1, -200, 200, 0],
                 ["rq-d", "completed", 300, 3900, "", 4000, 1, 0, 0, 0, 0, 0]]


WORLD = {"worldVersion": "world-v1", "worldSeed": 101, "rho": 0.7, "shockMean": 0.4, "shockHalfLife": 2.0,
         "shockWindow": 12, "sigma": 0.0, "shockRate": 0.0, "base": {"values": [1]}}


def deltas(v, e, p):
    return {name: {"pickupEtaTotalMs": abs(b["pickupEtaMs"] - a["pickupEtaMs"]),
                   "dropEtaTotalMs": abs(b["dropEtaMs"] - a["dropEtaMs"])}
            for name, a, b in (("exogenous", v, e), ("decisionInduced", e, p), ("visible", v, p))}


def stop(kind, rider):
    return {"kind": kind, "requestId": rider}


def job_frames(big=False):
    """(direction, envelope) of the job in transcript order. The decision at 600 s moves rq-c from v2 to v1; one
    decision is never acknowledged; the final checkpoint holds Shadow allocations and a travel snapshot, whose
    frame is over 16 MiB when big."""
    requests, ledger, published = [], [], {}
    for rider, (vehicle, lifecycle, boarded, promises) in RIDERS.items():
        requests.append({"requestId": rider, "lifecycle": lifecycle, "assignedVehicleId": vehicle,
                         "actualPickupTimeMs": boarded})
        entries, previous = [], None
        for number, (at, (pickup_e, pickup_p), (drop_e, drop_p)) in enumerate(promises):
            promise = {"pickupEtaMs": pickup_p, "dropEtaMs": drop_p, "vehicleId": vehicle}
            entry = {"kind": "revision" if previous else "initialPromise", "publicationId": f"{rider}-{number}",
                     "publishedPromise": {"publishedAtMs": at, "projection": promise}}
            if previous:
                kept = {"pickupEtaMs": pickup_e, "dropEtaMs": drop_e, "vehicleId": vehicle}
                entry.update(previousPromise={"projection": previous}, exogenousProjection=kept,
                             deltas=deltas(previous, kept, promise))
            entries.append(entry)
            published.setdefault(at, []).append({"publicationId": entry["publicationId"], "promise": promise})
            previous = promise
        ledger.append({"requestId": rider, "entries": entries})
    plans = {300: {"v2": [stop("pickup", "rq-c"), stop("pickup", "rq-d")], "v1": [stop("pickup", "rq-b")]},
             600: {"v1": [stop("pickup", "rq-c"), stop("pickup", "rq-b")], "v2": [stop("pickup", "rq-d")]}}
    timed = [(0, 0, "adapterToRunner", {"messageType": "hello", "payload": {}})]
    batches = {}
    for rider, (_vehicle, _lifecycle, boarded, _promises) in RIDERS.items():
        if boarded is not None:
            batches.setdefault(boarded, []).append({"eventType": "passengerBoarded", "payload": {"requestId": rider}})
    for at, rider in ALIGHTED:
        batches.setdefault(at, []).append({"eventType": "passengerAlighted", "payload": {"requestId": rider}})
    batches[900] = [{"eventType": "travelTimesUpdated", "payload": {"snapshot": {"arcs": [[0, 1, 5]],
                                                                                 "snapshotHash": "a" * 64}}}]
    for at, events in batches.items():
        timed.append((at, 0, "adapterToRunner", {"messageType": "eventBatch", "payload": {"events": events}}))
    for at, promises in published.items():
        actions = [{"decisionType": "promisePublished", "payload": promise} for promise in promises]
        for vehicle, stops in plans.get(at, {}).items():
            actions.append({"decisionType": "vehiclePlanUpdated", "payload": {
                "vehicleId": vehicle, "route": {"frozenPrefix": stops[:1], "mutableSuffix": stops[1:]}}})
        payload = {"decisionHash": f"decision-{at}", "actions": actions,
                   "certificate": {"body": {"normalOperation": at != 2000}}}
        timed.append((at, 1, "runnerToAdapter", {"messageType": "decision", "payload": payload}))
        timed.append((at, 2, "adapterToRunner", {"messageType": "decisionApplied",
                                                 "payload": {"decisionHash": f"decision-{at}"}}))
    ghost = {"publicationId": "ghost-0", "promise": {"pickupEtaMs": 1, "dropEtaMs": 2, "vehicleId": "v1"}}
    unacknowledged = {"decisionHash": "decision-unacknowledged",
                      "actions": [{"decisionType": "promisePublished", "payload": ghost}]}
    timed.append((150, 1, "runnerToAdapter", {"messageType": "decision", "payload": unacknowledged}))
    incidents = {"breaches": [{"requestId": "rq-b", "kind": "forcedReference"}, {"requestId": "rq-a"}],
                 "latePickups": [{"requestId": "rq-b", "actualPickupMs": 3000, "latestPickupMs": 2500}]}
    early = {"requests": [], "commitmentLedger": [], "incidentLedger": {"breaches": [], "latePickups": []}}
    final = {"requests": requests, "commitmentLedger": ledger, "incidentLedger": incidents,
             "shadowAllocations": [{"requestId": "rq-a", "class": "guaranteed", "score": "0.25"},
                                   {"requestId": "rq-b", "class": "allowed", "score": "1.5"}],
             "travelTimes": {"arcs": [[n, n + 1, 1000 + n % 977] for n in range(1_100_000 if big else 3)],
                             "snapshotHash": "b" * 64, "snapshotVersion": 2}}
    for at, state in ((3500, early), (8000, final)):
        timed.append((at, 3, "runnerToAdapter", {"messageType": "checkpoint",
                                                 "payload": {"content": {"onlineState": state}}}))
    return [(direction, dict(envelope, simTimeMs=at)) for at, _, direction, envelope in sorted(
        timed, key=lambda item: item[:2])]


def write_transcript(folder, frames):
    """transcript-00.ndjson.gz as the session writes it."""
    folder.mkdir(parents=True, exist_ok=True)
    recorder = ProtocolTranscriptRecorder(folder / "transcript-00.ndjson.gz")
    for direction, envelope in frames:
        recorder.record(direction, json.dumps(envelope, sort_keys=True, separators=(",", ":")).encode("utf-8"))
    recorder.close()


def normalized_sha256(evidence):
    """The evidence hash without the machine-dependent fields: the transcript path and the gzip file's hash and size."""
    evidence = json.loads(json.dumps(evidence))
    evidence["audit"]["summary"]["transcript"] = ""
    evidence["transcript"]["fileSha256"] = evidence["transcript"]["fileBytes"] = ""
    return hashlib.sha256(json.dumps(evidence, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def read_evidence(folder):
    return json.loads(gzip.decompress((folder / "evidence.json.gz").read_bytes()))


class Folder(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.tmp = pathlib.Path(folder.name)

    def write(self, name, value):
        path = self.tmp / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value) if not isinstance(value, str) else value, encoding="utf-8")
        return path


class WorldTests(Folder):
    def test_factor_is_relative_to_bin_zero_and_holds_the_last_value(self):
        values = (2.0, 2.5, 1.6)
        self.assertEqual([run_job.world_factor(values, b) for b in range(5)], [1.0, 1.25, 0.8, 0.8, 0.8])
        self.assertEqual([run_job.world_factor((1.3,), b) for b in range(3)], [1.0, 1.0, 1.0])

    def test_only_noise_free_worlds_with_explicit_values_are_read(self):
        world = {**WORLD, "base": {"values": [1, 1.1, 0.95]}}
        self.assertEqual(run_job.read_world(self.write("w.json", world)), (1.0, 1.1, 0.95))
        for change in ({"sigma": 0.08}, {"shockRate": 0.02}, {"base": {"day": "x", "exponent": 1, "windowStart": 0}},
                       {"base": {"values": []}}, {"base": {"values": [1, 0]}}, {"worldVersion": "world-v2"},
                       {"extra": 1}):
            with self.subTest(change=change), self.assertRaises(SystemExit):
                run_job.read_world(self.write("w.json", {**world, **change}))

    def test_the_clock_stops_at_every_bin_start(self):
        self.assertEqual(run_job.clock_boundary(None, 0), 900)
        self.assertEqual(run_job.clock_boundary(None, 1799.5), 1800)
        self.assertEqual(run_job.clock_boundary(1000.25, 950), 1000.25)
        self.assertEqual(run_job.clock_boundary(2000, 950), 1800)
        same = run_job.clock_boundary(1800.0, 950)  # a tie keeps the physical boundary, with its type
        self.assertEqual((same, type(same)), (1800.0, float))
        self.assertEqual(run_job.clock_boundary(None, 900), 1800)


class Node:
    def __init__(self, index):
        self.node_index, self.edges_to = index, {}


class Edge:
    def __init__(self, travel_time):
        self.travel_time = travel_time

    def get_tt(self):
        return self.travel_time


class RecordingRouting:
    """FleetPy's routing as the world update uses it: the nodes, and two calls that are recorded."""

    def __init__(self, nodes, calls):
        self.nodes, self.calls = nodes, calls

    def _set_edge_tt(self, origin, destination, travel_time):
        self.calls.append(("set", origin, destination, travel_time))

    def _reset_internal_attributes_after_travel_time_update(self):
        self.calls.append(("reset",))


class RecordingControl:
    """A fleet control that records its calls. At a trigger a new request gets an offer, except r2, which is
    rejected; a confirmed request closes at the first trigger 500 s after its confirmation."""

    def __init__(self, calls):
        self.calls, self._rb_states, self.rq_dict, self.closes_at = calls, {}, {}, {}
        self._rb_settings = types.SimpleNamespace(service_class=run_job.SERVICE_CLASS,
                                                  commitment_policy_id=run_job.COMMITMENT_POLICY_ID)
        self._rb_mapper = types.SimpleNamespace(request=self.protocol_request, raw_request=lambda value: value,
                                                raw_node=lambda value: value)

    @staticmethod
    def protocol_request(request, service_class, policy_id):
        return {"requestId": request.rid, "arrivalTimeMs": request.rq_time * 1000,
                "earliestPickupMs": request.earliest_start_time * 1000,
                "latestPickupMs": request.latest_start_time * 1000,
                "maxRideTimeMs": round(request.max_trip_time * 1000),
                "partySize": request.nr_pax, "serviceClass": service_class, "commitmentPolicyId": policy_id,
                "originNodeId": request.o_pos[0], "destinationNodeId": request.d_pos[0]}

    def inform_network_travel_time_update(self, simulation_time):
        self.calls.append(("world", simulation_time))

    def user_request(self, request, simulation_time):
        self.calls.append(("request", request.rid, simulation_time))
        self.rq_dict[request.rid], self._rb_states[request.rid] = request, "new"

    def time_trigger(self, simulation_time):
        self.calls.append(("trigger", simulation_time))
        for rid, state in self._rb_states.items():
            if state == "new":
                self._rb_states[rid] = "rejected" if rid == "r2" else "offered"
            elif state == "assigned" and self.closes_at[rid] <= simulation_time:
                self._rb_states[rid] = "closed"

    def get_current_offer(self, rid):
        declined = self._rb_states[rid] == "rejected"
        return types.SimpleNamespace(service_declined=lambda: declined)

    def user_confirms_booking(self, rid, simulation_time):
        self.calls.append(("confirm", rid, simulation_time))
        self._rb_states[rid], self.closes_at[rid] = "assigned", simulation_time + 500
        del self.rq_dict[rid]

    def user_cancels_request(self, rid, simulation_time):
        self.calls.append(("cancel", rid, simulation_time))
        self._rb_states[rid] = "closed"
        del self.rq_dict[rid]


class ClockTests(unittest.TestCase):
    """The event clock (drive) and the travel-time update of each bin, with stub objects in place of FleetPy."""

    def setUp(self):
        fleetpy_globals = types.ModuleType("src.misc.globals")
        fleetpy_globals.G_DRIVING_STATUS = ()  # the vehicle states that count as driving
        stubs = {"src": types.ModuleType("src"), "src.misc": types.ModuleType("src.misc"),
                 "src.misc.globals": fleetpy_globals}
        patcher = mock.patch.dict(sys.modules, stubs)
        patcher.start()
        self.addCleanup(patcher.stop)
        self.calls = []

    def test_the_world_sets_every_edge_once_per_bin(self):
        a, b = Node(0), Node(1)
        a.edges_to[b], b.edges_to[a] = Edge(10.0), Edge(20.0)
        world = run_job.WorldTravelTimes(RecordingRouting([a, b], self.calls), (2.0, 3.0, 1.0))
        with contextlib.redirect_stdout(io.StringIO()):  # the update prints one line per bin
            for simulation_time in (0, 899, 900, 1799, 2700, 9000):
                world.update(RecordingControl(self.calls), simulation_time)
        # Factors: bin 0 -> 1, bin 1 -> 3/2, bin 3 -> 1/2, bin 10 -> 1/2 (the last value holds).
        self.assertEqual(self.calls, [("set", 0, 1, 10.0), ("set", 1, 0, 20.0), ("reset",), ("world", 0),
                                      ("set", 0, 1, 15.0), ("set", 1, 0, 30.0), ("reset",), ("world", 900),
                                      ("set", 0, 1, 5.0), ("set", 1, 0, 10.0), ("reset",), ("world", 2700),
                                      ("set", 0, 1, 5.0), ("set", 1, 0, 10.0), ("reset",), ("world", 9000)])

    def test_the_clock_stops_at_arrivals_bin_starts_and_drain_steps(self):
        def request(rid, arrival):
            return {"requestId": rid, "arrivalTimeMs": arrival * 1000, "earliestPickupMs": arrival * 1000,
                    "latestPickupMs": (arrival + 600) * 1000, "maxRideTimeMs": 1_200_000, "partySize": 1,
                    "originNodeId": "n0", "destinationNodeId": "n1"}

        control, request_db = RecordingControl(self.calls), {}
        routing = RecordingRouting([], [])  # no edges; the test above checks the routing's calls
        world = run_job.WorldTravelTimes(routing, (1.0, 2.0))
        requests = [request("r1", 0), request("r2", 600), request("r3", 1000)]
        with contextlib.redirect_stdout(io.StringIO()):
            result = run_job.drive(control, [], routing, world, requests, request_db, {"n0": 0, "n1": 1},
                                   {"maximumDrainSteps": 10, "drainStepSeconds": 200})
        self.assertEqual(result, (1600, {"r1", "r3"}, {"r2"}))
        self.assertEqual(request_db, {})
        # Stops: arrivals at 0, 600 and 1000, the bin start at 900, then drain steps of 200 s until r3 closes.
        self.assertEqual(self.calls, [
            ("world", 0), ("trigger", 0), ("request", "r1", 0), ("trigger", 0), ("confirm", "r1", 0), ("trigger", 0),
            ("request", "r2", 600), ("trigger", 600), ("cancel", "r2", 600),
            ("world", 900), ("trigger", 900),
            ("request", "r3", 1000), ("trigger", 1000), ("confirm", "r3", 1000), ("trigger", 1000),
            ("trigger", 1200), ("trigger", 1400), ("trigger", 1600)])


class SettingsTests(Folder):
    def test_the_session_gets_the_final_timeout_line_bound_and_wire_names(self):
        fleetpy = self.tmp / "fleetpy"
        for name in ("LICENSE", "environment.yml", "src/fleetctrl/FleetControlBase.py", "src/routing/NetworkBase.py",
                     "src/simulation/Vehicles.py", "src/misc/init_modules.py"):
            self.write(f"fleetpy/{name}", "")
        args = argparse.Namespace(
            dotnet=self.write("dotnet", ""), runner_root=self.write("runner/RideBound.Runner.dll", "").parent,
            commitment_config=self.write("c.json", {"policies": [{"policyId": run_job.COMMITMENT_POLICY_ID}]}),
            wp4_config=self.write("w.json", {"policyId": "ridebound-hard-vector", "policyVersion": "1",
                                             "lateBoarding": "record-v1"}),
            fleetpy_root=fleetpy, label=JOB + "-fin2", master_seed=7)
        operator = run_job.operator_settings(args, [self.write("scenario.json", {})], 96, self.tmp / "t.ndjson.gz")
        settings = RideBoundSessionSettings.from_attributes(operator)
        clients = []

        def client_factory(*arguments, **options):
            clients.append((arguments, options))
            return mock.Mock()

        RideBoundProtocolSession(settings, client_factory=client_factory).close()
        (command, _artifacts), options = clients[0]
        self.assertEqual(command[-2:], ("--maximum-line-bytes", "1073741824"))
        self.assertEqual((options["timeout_seconds"], options["maximum_input_line_bytes"],
                          options["maximum_output_line_bytes"]), (900, 2**30, 2**30))
        self.assertEqual((settings.run_id, settings.scenario_id, settings.service_class,
                          settings.commitment_policy_id, settings.master_seed,
                          settings.complete_travel_snapshot_maximum_nodes),
                         (f"wp7-medium-{JOB}-fin2", "fleetpy-manhattan-v1-medium-wp7-physical-closure",
                          "fleetpy-tlc-public-derivative-v1", "wp6-synthetic-policy-overlay-v1", 7, 96))


class InputTests(Folder):
    def test_the_driver_must_be_a_full_node_run_driver_of_the_scenario(self):
        summary = {"requestCount": 3, "vehicleCount": 2, "nodeCount": 4, "directedArcCount": 12, "other": 1}
        scenario = self.write("scenario.json", {"validationSummary": summary})
        driver = {"sourceScenarioContentSha256": hashlib.sha256(scenario.read_bytes()).hexdigest(),
                  "routingEngine": "complete-graph-direct-v1", "transcriptCompression": "gzip-v1",
                  "completeTravelSnapshotMaximumNodes": 4, "expectedRequestCount": 3, "expectedVehicleCount": 2,
                  "expectedNodeCount": 4, "expectedDirectedArcCount": 12}
        args = argparse.Namespace(scenario=scenario, driver=self.write("driver.json", driver))
        self.assertEqual(run_job.read_inputs(args)[1:], (driver, driver["sourceScenarioContentSha256"]))
        for change, text in (({"sourceScenarioContentSha256": "0" * 64}, "hash mismatch"),
                             ({"routingEngine": "network-basic-v1"}, "see commit ebca912"),
                             ({"transcriptCompression": "none"}, "see commit ebca912"),
                             ({"expectedNodeCount": 5}, "cardinality"), ({"expectedRequestCount": None}, "cardinality"),
                             ({"completeTravelSnapshotMaximumNodes": 0}, "snapshot bound")):
            self.write("driver.json", {**driver, **change})
            with self.subTest(change=change), self.assertRaisesRegex(RuntimeError, text):
                run_job.read_inputs(args)
        # The old default counts (128 requests, 32 vehicles, 96 nodes, 9120 arcs) are gone.
        self.write("driver.json", {key: value for key, value in driver.items() if key != "expectedVehicleCount"})
        with self.assertRaisesRegex(RuntimeError, "no expectedVehicleCount; .*see commit ebca912"):
            run_job.read_inputs(args)


class EvidenceTests(Folder):
    def test_evidence_of_a_small_job(self):
        folder = self.tmp / JOB
        write_transcript(folder, job_frames(big=True))
        report = run_job.write_evidence(folder, ROLE)
        evidence = read_evidence(folder)
        self.assertEqual(normalized_sha256(evidence), EXPECTED_EVIDENCE_SHA256)
        self.assertEqual(evidence["audit"]["summary"]["transcript"], str(folder / "transcript-00.ndjson"))
        transcript = folder / "transcript-00.ndjson.gz"
        self.assertEqual((evidence["transcript"]["fileSha256"], evidence["transcript"]["fileBytes"]),
                         (hashlib.sha256(transcript.read_bytes()).hexdigest(), transcript.stat().st_size))
        self.assertEqual((evidence["moves"], evidence["shadowAllocations"], evidence["audit"]["summary"]["sumDropC"]),
                         (EXPECTED_MOVES, EXPECTED_SHADOW, EXPECTED_SUM_DROP_C))
        self.assertEqual(run_job.rider_rows(folder, evidence), EXPECTED_ROWS)
        self.assertEqual(evidence["finalOnlineState"]["travelTimes"]["arcs"], [])  # a frame over 16 MiB
        digest = hashlib.sha256((folder / "evidence.json.gz").read_bytes()).hexdigest()
        self.assertEqual((folder / "EVIDENCE.sha256").read_text(encoding="utf-8"), f"{digest} *evidence.json.gz\n")
        self.assertEqual(report["riders"], len(EXPECTED_ROWS))

    def test_contradicting_evidence_is_refused(self):
        frames = json.loads(json.dumps(job_frames()))  # a copy whose ledger does not share the action's promise
        decision = next(envelope for _, envelope in frames if envelope["messageType"] == "decision"
                        and envelope["payload"]["decisionHash"] == "decision-600")
        decision["payload"]["actions"][0]["payload"]["promise"]["dropEtaMs"] += 1
        write_transcript(self.tmp / JOB, frames)
        with self.assertRaisesRegex(RuntimeError, "action p differs from ledger p"):
            run_job.write_evidence(self.tmp / JOB, ROLE)
        self.assertFalse((self.tmp / JOB / "evidence.json.gz").exists())

    def test_a_transcript_record_that_does_not_match_its_frame_is_refused(self):
        # Record line 2 (counted from 0) holds frame 3. Each case changes one field of that record.
        for field, change, message in (
                ("frameSha256", lambda value: "0" * 64, "transcript-00.ndjson line 2: frame hash mismatch"),
                ("frameLengthBytes", lambda value: value + 1,
                 "transcript-00.ndjson.gz: frame 3 does not match its record"),
                ("ordinal", lambda value: value + 5, "transcript-00.ndjson.gz: ordinals are not 1..n")):
            with self.subTest(field=field):
                folder = self.tmp / field / JOB
                write_transcript(folder, job_frames())
                transcript = folder / "transcript-00.ndjson.gz"
                lines = gzip.decompress(transcript.read_bytes()).splitlines(keepends=True)
                value = json.loads(lines[2])[field]
                old, new = (f'"{field}":{json.dumps(item)}'.encode("utf-8") for item in (value, change(value)))
                self.assertEqual(lines[2].count(old), 1)
                lines[2] = lines[2].replace(old, new)
                transcript.write_bytes(gzip.compress(b"".join(lines)))
                with self.assertRaises(RuntimeError) as raised:
                    run_job.write_evidence(folder, ROLE)
                self.assertTrue(str(raised.exception).endswith(message), raised.exception)
                self.assertFalse((folder / "evidence.json.gz").exists())


class MainTests(Folder):
    def main_with_fleet(self, argv, frames, commit_after="c" * 40):
        """run_job.main with the fleet run replaced by one that writes the given transcript, and no input checks.
        The checkout's commit is "c" * 40 before the run and commit_after after it."""
        def run_fleet(args, source, driver, values, out, source_hash):
            self.assertEqual(values, (1.0,))
            write_transcript(out, frames)
            return {"semanticHash": "s" * 64, "resources": {"wallMilliseconds": 1}}

        with mock.patch.object(run_job, "read_inputs", return_value=({}, {}, "h" * 64)), \
                mock.patch.object(run_job, "git_head", side_effect=["c" * 40, commit_after]), \
                mock.patch.object(run_job, "run_fleet", run_fleet), contextlib.redirect_stdout(io.StringIO()):
            return run_job.main(argv)

    def run_main(self, output, frames, transcript="keep", role=ROLE, commit_after="c" * 40):
        unused = str(self.write("in/unused.json", {}))  # the inputs only the replaced parts read
        argv = ["--label", f"{output.name}-fin2", "--role", role, "--world", str(self.write("in/world.json", WORLD)),
                "--transcript", transcript, "--output", str(output)]
        for name in ("--fleetpy-root", "--runner-root", "--dotnet", "--commitment-config", "--wp4-config", "--scenario",
                     "--derivative-manifest", "--normalization-report", "--selection-frame", "--driver"):
            argv += [name, unused]
        return self.main_with_fleet(argv, frames, commit_after)

    def test_the_command_of_run_campaign_runs(self):
        import run_campaign

        unused = str(self.write("in/unused.json", {}))
        job = {"name": JOB, "label": f"{JOB}-fin2", "role": ROLE, "config": unused, "wp4": unused,
               "fixture": str(self.tmp / "in"), "driver": unused, "world": str(self.write("in/world.json", WORLD)),
               "transcript": "drop"}
        args = argparse.Namespace(fleetpy_root=unused, runner_root=unused, dotnet=unused, out=str(self.tmp / "runs"))
        command = run_campaign.command(job, args)
        self.assertEqual(pathlib.Path(command[2]), ADAPTER_ROOT / "run_job.py")
        self.assertEqual(self.main_with_fleet(command[3:], job_frames()), 0)
        self.assertTrue(run_campaign.passed(self.tmp / "runs" / JOB))
        self.assertFalse((self.tmp / "runs" / JOB / "transcript-00.ndjson.gz").exists())

    def test_the_summary_comes_after_the_evidence(self):
        for transcript in ("keep", "drop"):
            output = self.tmp / transcript / JOB
            self.assertEqual(self.run_main(output, job_frames(), transcript), 0)
            names = sorted(path.name for path in output.iterdir())
            kept = ["transcript-00.ndjson.gz"] if transcript == "keep" else []
            self.assertEqual(names, sorted(["EVIDENCE.sha256", "bundle-manifest.json", "evidence.json.gz",
                                            "run-00.json", "summary.json"] + kept))
            summary = json.loads((output / "summary.json").read_text(encoding="utf-8"))
            self.assertEqual((summary["status"], summary["semanticHash"], summary["runs"][0]["repeat"]),
                             ("pass", "s" * 64, 0))
            manifest = json.loads((output / "bundle-manifest.json").read_text(encoding="utf-8"))
            self.assertEqual([item["path"] for item in manifest["files"]], [name for name in names
                                                                           if name != "bundle-manifest.json"])
        with self.assertRaises(FileExistsError):  # the output folder must be new
            self.run_main(self.tmp / "keep" / JOB, job_frames())

    def test_a_failed_evidence_leaves_no_summary(self):
        output = self.tmp / JOB
        frames = [(direction, envelope) for direction, envelope in job_frames() if envelope["messageType"]
                  != "checkpoint"]
        with self.assertRaisesRegex(RuntimeError, "no final checkpoint"):
            self.run_main(output, frames)
        self.assertEqual(sorted(path.name for path in output.iterdir()),
                         ["failure-00.json", "run-00.json", "transcript-00.ndjson.gz"])

    def test_a_commit_change_during_the_job_fails_it(self):
        output = self.tmp / JOB
        with self.assertRaisesRegex(RuntimeError, "the checkout's commit changed while the job ran"):
            self.run_main(output, job_frames(), commit_after="d" * 40)
        self.assertEqual(sorted(path.name for path in output.iterdir()),
                         ["failure-00.json", "run-00.json", "transcript-00.ndjson.gz"])

    def test_the_output_folder_is_named_after_role_and_cell(self):
        for output, role in ((self.tmp / JOB, "FS-G2-SHHZ71-N"), (self.tmp / f"{ROLE}-d20190919-s10-r0-w08", ROLE)):
            with self.subTest(output=output.name, role=role), self.assertRaises(SystemExit), \
                    contextlib.redirect_stderr(io.StringIO()):
                self.run_main(output, job_frames(), role=role)
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
