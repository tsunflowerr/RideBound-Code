using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

public sealed class OnlineStateCheckpointCodecTests
{
    [Fact]
    public void Closed_incident_with_prior_breach_round_trips_exactly()
    {
        var requestId = new RequestId("request-1");
        var vehicleId = new VehicleId("vehicle-1");
        var pickupNode = new NodeId("pickup-node");
        var dropNode = new NodeId("drop-node");
        var pickupStop = new StopId("pickup-stop");
        var dropStop = new StopId("drop-stop");
        var request = RideRequest.CreatePending(
            requestId,
            new SimTime(0),
            pickupNode,
            dropNode,
            new SimTime(0),
            new SimTime(1_000),
            new Duration(1_000),
            1,
            "standard",
            "uniform-v1").Value!;
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    pickupStop,
                    pickupNode,
                    RouteStopKind.Pickup,
                    requestId,
                    new Duration(0)),
                new RouteStop(
                    dropStop,
                    dropNode,
                    RouteStopKind.DropOff,
                    requestId,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            vehicleId,
            4,
            0,
            new NodePosition(pickupNode),
            [],
            [],
            route,
            0).Value!;
        var run = RideBoundRun.Create(
            new RunIdentifier("checkpoint-run"),
            new ScenarioIdentifier("checkpoint-scenario"),
            new SimTime(0));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(requestId, vehicleId).Value!;
        run = run.AdvanceEpoch(1, new SimTime(3)).Value!;
        var projection = new PromiseProjection(
            requestId,
            vehicleId,
            pickupStop,
            pickupNode,
            dropStop,
            dropNode,
            new SimTime(0),
            new SimTime(100),
            [
                new PromiseServiceToken(
                    pickupStop,
                    requestId,
                    RouteStopKind.Pickup),
                new PromiseServiceToken(
                    dropStop,
                    requestId,
                    RouteStopKind.DropOff),
            ]);
        var promises = CommitmentLedger.Empty.OpenInitial(
            "publication-1",
            projection,
            1,
            new SimTime(0),
            "INITIAL_ACCEPTANCE",
            1).Ledger!;
        var incidentId = new IncidentId("incident-1");
        var incidents = OperationalIncidentLedger.Empty.Open(
            incidentId,
            "ROAD_CLOSED",
            [vehicleId],
            [requestId],
            2,
            new SimTime(1)).Ledger!;
        var delta = new CommitmentVector(1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        incidents = incidents.AppendBreach(
            new CommitmentBreachRecord(
                "breach-1",
                incidentId,
                requestId,
                promises.Histories[requestId].Current.PublishedPromise,
                projection,
                projection,
                new ThreeWayPromiseDelta(
                    CommitmentVector.Zero,
                    delta,
                    delta),
                CommitmentVector.Zero,
                delta,
                ["pickup_eta_total_ms"],
                3,
                1,
                new SimTime(2))).Ledger!;
        incidents = incidents.Resolve(
            incidentId,
            4,
            new SimTime(3)).Ledger!;
        incidents = incidents.AppendBreach(
            CommitmentBreachRecord.CreateExogenousServiceQuality(
                "exogenous-breach-1",
                requestId,
                promises.Histories[requestId].Current.PublishedPromise,
                projection,
                projection,
                new ThreeWayPromiseDelta(
                    CommitmentVector.Zero,
                    CommitmentVector.Zero,
                    CommitmentVector.Zero),
                CommitmentVector.Zero,
                CommitmentVector.Zero,
                [PhysicalViolationCodes.MaxRideTime],
                [
                    new ServiceQualityBreach(
                        requestId,
                        PhysicalViolationCodes.MaxRideTime,
                        "maxRideTimeMs",
                        100,
                        101),
                ],
                4,
                1,
                new SimTime(3))).Ledger!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            [
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(pickupNode, dropNode),
                    new Duration(100)),
            ]).Value!;
        var state = new OnlineState(
            run,
            travel,
            5,
            travel.SnapshotHash,
            promises,
            incidents);
        using var document = JsonDocument.Parse(
            OnlineStateCanonicalizer.Canonicalize(state));

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error);
        var restoredIncident = Assert.Single(
            decoded.State!.Incidents.Incidents).Value;
        Assert.False(restoredIncident.IsOpen);
        Assert.Collection(
            decoded.State.Incidents.Breaches,
            breach => Assert.Equal("breach-1", breach.BreachId),
            breach =>
            {
                Assert.Equal("exogenous-breach-1", breach.BreachId);
                Assert.Equal(
                    CommitmentBreachKind.ExogenousServiceQuality,
                    breach.Kind);
                Assert.Null(breach.IncidentId);
                Assert.Single(breach.ServiceQualityWitnesses);
                Assert.Equal(breach.BudgetBefore, breach.AttemptedBudgetAfter);
            });
        Assert.Equal(
            OnlineStateCanonicalizer.Canonicalize(state),
            OnlineStateCanonicalizer.Canonicalize(decoded.State));

        var tampered = JsonNode.Parse(document.RootElement.GetRawText())!;
        tampered["incidentLedger"]!["breaches"]![0]!["previousPromise"]![
            "projection"]!["pickupEtaMs"] = 1;
        using var tamperedDocument = JsonDocument.Parse(tampered.ToJsonString());
        var rejected = OnlineStateCheckpointCodec.Decode(
            tamperedDocument.RootElement);

        Assert.False(rejected.IsSuccess);
        Assert.Contains("promise/run boundary", rejected.Error);

        var forgedExogenous = JsonNode.Parse(document.RootElement.GetRawText())!;
        forgedExogenous["incidentLedger"]!["breaches"]![1]![
            "attemptedBudgetAfter"]!["pickupEtaTotalMs"] = 1;
        using var forgedDocument = JsonDocument.Parse(
            forgedExogenous.ToJsonString());
        var forgedResult = OnlineStateCheckpointCodec.Decode(
            forgedDocument.RootElement);

        Assert.False(forgedResult.IsSuccess);
        Assert.Contains("unchanged budget", forgedResult.Error);
    }

    [Fact]
    public void Forced_reference_breach_round_trips_with_its_own_kind_and_is_tamper_checked()
    {
        var (run, projection, promises, travel) = SingleRiderState();
        var requestId = projection.RequestId;
        var drift = new CommitmentVector(0, 40, 0, 0, 0, 0, 0, 0, 0, 0);
        var incidents = OperationalIncidentLedger.Empty.AppendBreach(
            CommitmentBreachRecord.CreateForcedReference(
                "forced-breach-1",
                requestId,
                promises.Histories[requestId].Current.PublishedPromise,
                projection,
                projection,
                new ThreeWayPromiseDelta(drift, CommitmentVector.Zero, drift),
                CommitmentVector.Zero,
                drift,
                [CommitmentFailureCodes.BudgetExceeded],
                3,
                1,
                new SimTime(2))).Ledger!;
        var state = new OnlineState(run, travel, 5, travel.SnapshotHash, promises, incidents);
        var canonical = OnlineStateCanonicalizer.Canonicalize(state);
        using var document = JsonDocument.Parse(canonical);

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error);
        var breach = Assert.Single(decoded.State!.Incidents.Breaches);
        Assert.Equal(CommitmentBreachKind.ForcedReference, breach.Kind);
        Assert.Null(breach.IncidentId);
        Assert.Equal(40, breach.AttemptedBudgetAfter.DropEtaTotalMs);
        Assert.Equal(canonical, OnlineStateCanonicalizer.Canonicalize(decoded.State));
        Assert.Equal(
            "forcedReference",
            document.RootElement.GetProperty("incidentLedger").GetProperty("breaches")[0]
                .GetProperty("kind").GetString());

        // Relabelled as an exogenous breach it no longer satisfies that kind's rules.
        var relabelled = JsonNode.Parse(document.RootElement.GetRawText())!;
        relabelled["incidentLedger"]!["breaches"]![0]!["kind"] = "exogenousServiceQuality";
        relabelled["incidentLedger"]!["breaches"]![0]!["serviceQualityWitnesses"] = new JsonArray();
        using var relabelledDocument = JsonDocument.Parse(relabelled.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(relabelledDocument.RootElement).IsSuccess);

        // A forged charge that follows neither budget basis is rejected.
        var forged = JsonNode.Parse(document.RootElement.GetRawText())!;
        forged["incidentLedger"]!["breaches"]![0]!["attemptedBudgetAfter"]!["dropEtaTotalMs"] = 7;
        using var forgedDocument = JsonDocument.Parse(forged.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(forgedDocument.RootElement).IsSuccess);

        // A kept-route projection that differs from the exogenous one is rejected.
        var moved = JsonNode.Parse(document.RootElement.GetRawText())!;
        moved["incidentLedger"]!["breaches"]![0]!["safetyProjection"]!["dropEtaMs"] =
            moved["incidentLedger"]!["breaches"]![0]!["safetyProjection"]!["dropEtaMs"]!
                .GetValue<long>() + 1;
        using var movedDocument = JsonDocument.Parse(moved.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(movedDocument.RootElement).IsSuccess);
    }

    [Fact]
    public void No_worse_breach_round_trips_with_its_own_kind_and_is_tamper_checked()
    {
        var (run, projection, promises, travel) = SingleRiderState();
        var requestId = projection.RequestId;
        // The changed route publishes a drop 5 ms later than the kept one.
        var changed = new PromiseProjection(
            projection.RequestId,
            projection.VehicleId,
            projection.PickupStopId,
            projection.PickupNodeId,
            projection.DropStopId,
            projection.DropNodeId,
            projection.PickupEta,
            new SimTime(projection.DropEta.Milliseconds + 5),
            projection.ServiceOrder);
        var drift = new CommitmentVector(0, 40, 0, 0, 0, 0, 0, 0, 0, 0);
        var decision = new CommitmentVector(0, 5, 0, 0, 0, 0, 0, 0, 0, 0);
        var visible = new CommitmentVector(0, 45, 0, 0, 0, 0, 0, 0, 0, 0);
        var incidents = OperationalIncidentLedger.Empty.AppendBreach(
            CommitmentBreachRecord.CreateForcedNoWorse(
                "no-worse-breach-1",
                requestId,
                promises.Histories[requestId].Current.PublishedPromise,
                projection,
                changed,
                new ThreeWayPromiseDelta(drift, decision, visible),
                CommitmentVector.Zero,
                visible,
                [CommitmentFailureCodes.DeadlineExceeded],
                3,
                1,
                new SimTime(2))).Ledger!;
        var state = new OnlineState(run, travel, 5, travel.SnapshotHash, promises, incidents);
        var canonical = OnlineStateCanonicalizer.Canonicalize(state);
        using var document = JsonDocument.Parse(canonical);

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error);
        var breach = Assert.Single(decoded.State!.Incidents.Breaches);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, breach.Kind);
        Assert.Equal(canonical, OnlineStateCanonicalizer.Canonicalize(decoded.State));
        Assert.Equal(
            "forcedNoWorse",
            document.RootElement.GetProperty("incidentLedger").GetProperty("breaches")[0]
                .GetProperty("kind").GetString());

        // Relabelled as a kept-route breach, the two different projections are rejected.
        var relabelled = JsonNode.Parse(document.RootElement.GetRawText())!;
        relabelled["incidentLedger"]!["breaches"]![0]!["kind"] = "forcedReference";
        using var relabelledDocument = JsonDocument.Parse(relabelled.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(relabelledDocument.RootElement).IsSuccess);

        // A no-worse record whose route is in fact unchanged is rejected.
        var unchanged = JsonNode.Parse(document.RootElement.GetRawText())!;
        unchanged["incidentLedger"]!["breaches"]![0]!["safetyProjection"] =
            JsonNode.Parse(unchanged["incidentLedger"]!["breaches"]![0]!["exogenousProjection"]!.ToJsonString());
        using var unchangedDocument = JsonDocument.Parse(unchanged.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(unchangedDocument.RootElement).IsSuccess);
    }

    [Fact]
    public void A_late_pickup_round_trips_only_with_its_matching_record()
    {
        // Window [0, 1000] ms; the rider boards at 1500 ms. The record makes the late pickup
        // restorable; without it, or with a record that does not match, the checkpoint fails.
        var (run, _, promises, travel) = SingleRiderState();
        var requestId = new RequestId("request-1");
        var vehicleId = new VehicleId("vehicle-1");
        run = run.ConfirmWaitingPickup(requestId).Value!;
        run = run.ReachStop(
            vehicleId,
            new StopId("pickup-stop"),
            new PlanVersion(0),
            new NodePosition(new NodeId("pickup-node")),
            2).Value!;
        run = run.Board(
            vehicleId,
            requestId,
            new PlanVersion(0),
            new SimTime(1_500),
            allowLatePickup: true).Value!;
        run = run.AdvanceEpoch(2, new SimTime(1_500)).Value!;
        var incidents = OperationalIncidentLedger.Empty.RecordLatePickup(
            new ObservedLatePickup(
                requestId,
                vehicleId,
                new SimTime(1_000),
                new SimTime(1_500),
                4,
                2)).Ledger!;
        var state = new OnlineState(run, travel, 5, travel.SnapshotHash, promises, incidents);
        var canonical = OnlineStateCanonicalizer.Canonicalize(state);
        using var document = JsonDocument.Parse(canonical);

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error);
        Assert.Equal(canonical, OnlineStateCanonicalizer.Canonicalize(decoded.State!));
        Assert.Equal(500, Assert.Single(decoded.State!.Incidents.LatePickups).LatenessMilliseconds);

        var missing = JsonNode.Parse(document.RootElement.GetRawText())!;
        missing["incidentLedger"]!.AsObject().Remove("latePickups");
        using var missingDocument = JsonDocument.Parse(missing.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(missingDocument.RootElement).IsSuccess);

        var mismatched = JsonNode.Parse(document.RootElement.GetRawText())!;
        mismatched["incidentLedger"]!["latePickups"]![0]!["actualPickupMs"] = 1_400;
        using var mismatchedDocument = JsonDocument.Parse(mismatched.ToJsonString());
        Assert.Contains(
            "late pickup does not match",
            OnlineStateCheckpointCodec.Decode(mismatchedDocument.RootElement).Error);

        // A record must lie inside the checkpoint's event and epoch boundary.
        foreach (var (field, value) in new[] { ("sourceEventSeq", 5L), ("recordedEpoch", 3L) })
        {
            var outside = JsonNode.Parse(document.RootElement.GetRawText())!;
            outside["incidentLedger"]!["latePickups"]![0]![field] = value;
            using var outsideDocument = JsonDocument.Parse(outside.ToJsonString());
            Assert.Contains(
                "late pickup does not match",
                OnlineStateCheckpointCodec.Decode(outsideDocument.RootElement).Error);
        }

        // One record per rider, and an empty list is not the canonical form.
        var duplicated = JsonNode.Parse(document.RootElement.GetRawText())!;
        var records = duplicated["incidentLedger"]!["latePickups"]!.AsArray();
        records.Add(records[0]!.DeepClone());
        using var duplicatedDocument = JsonDocument.Parse(duplicated.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(duplicatedDocument.RootElement).IsSuccess);

        var (plainRun, _, plainPromises, plainTravel) = SingleRiderState();
        var plain = JsonNode.Parse(
            OnlineStateCanonicalizer.Canonicalize(
                new OnlineState(plainRun, plainTravel, 5, plainTravel.SnapshotHash, plainPromises)))!;
        plain["incidentLedger"]!["latePickups"] = new JsonArray();
        using var emptyDocument = JsonDocument.Parse(plain.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(emptyDocument.RootElement).IsSuccess);
    }

    [Fact]
    public void Shadow_allocations_round_trip_on_every_state_path_and_are_absent_by_default()
    {
        // ADR-089 GĐ4: the fast writer, the reference canonicalizer, the v2 hash and the checkpoint codec.
        var (run, _, promises, travel) = SingleRiderState();
        var plain = new OnlineState(run, travel, 5, travel.SnapshotHash, promises);
        var shadow = plain with
        {
            Commitments = promises.WithShadowAllocation(
                new RequestId("request-1"),
                new ShadowAllocation(ShadowAllocationClass.Guaranteed, "52.494082647339084")),
        };

        var plainCanonical = OnlineStateCanonicalizer.Canonicalize(plain);
        var shadowCanonical = OnlineStateCanonicalizer.Canonicalize(shadow);
        using var plainDocument = JsonDocument.Parse(plainCanonical);
        Assert.False(plainDocument.RootElement.TryGetProperty("shadowAllocations", out _));
        Assert.Contains(
            "\"shadowAllocations\":[{\"class\":\"guaranteed\",\"requestId\":\"request-1\",\"score\":\"52.494082647339084\"}]",
            System.Text.Encoding.UTF8.GetString(shadowCanonical));
        Assert.Equal(shadowCanonical, OnlineStateCanonicalWriter.Write(shadow, null).ToArray());
        Assert.NotEqual(
            new OnlineStateHashV2(cached: false).Calculate(plain),
            new OnlineStateHashV2(cached: false).Calculate(shadow));

        using var document = JsonDocument.Parse(shadowCanonical);
        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);
        Assert.True(decoded.IsSuccess, decoded.Error);
        Assert.Equal(shadowCanonical, OnlineStateCanonicalizer.Canonicalize(decoded.State!));
        Assert.Equal(
            ShadowAllocationClass.Guaranteed,
            decoded.State!.Commitments.ShadowAllocationOf(new RequestId("request-1"))!.Class);

        var tampered = JsonNode.Parse(shadowCanonical)!;
        tampered["shadowAllocations"]![0]!["class"] = "maybe";
        using var tamperedDocument = JsonDocument.Parse(tampered.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(tamperedDocument.RootElement).IsSuccess);

        var orphan = JsonNode.Parse(shadowCanonical)!;
        orphan["shadowAllocations"]![0]!["requestId"] = "request-unknown";
        using var orphanDocument = JsonDocument.Parse(orphan.ToJsonString());
        Assert.False(OnlineStateCheckpointCodec.Decode(orphanDocument.RootElement).IsSuccess);
    }

    [Fact]
    public void A_state_without_late_pickups_writes_no_late_pickup_field()
    {
        var (run, _, promises, travel) = SingleRiderState();
        var state = new OnlineState(run, travel, 5, travel.SnapshotHash, promises);

        using var document = JsonDocument.Parse(OnlineStateCanonicalizer.Canonicalize(state));

        Assert.False(
            document.RootElement.GetProperty("incidentLedger").TryGetProperty("latePickups", out _));
    }

    private static (RideBoundRun Run, PromiseProjection Projection, CommitmentLedger Promises,
        TravelTimeSnapshot Travel) SingleRiderState()
    {
        var requestId = new RequestId("request-1");
        var vehicleId = new VehicleId("vehicle-1");
        var pickupNode = new NodeId("pickup-node");
        var dropNode = new NodeId("drop-node");
        var pickupStop = new StopId("pickup-stop");
        var dropStop = new StopId("drop-stop");
        var request = RideRequest.CreatePending(
            requestId,
            new SimTime(0),
            pickupNode,
            dropNode,
            new SimTime(0),
            new SimTime(1_000),
            new Duration(1_000),
            1,
            "standard",
            "uniform-v1").Value!;
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(pickupStop, pickupNode, RouteStopKind.Pickup, requestId, new Duration(0)),
                new RouteStop(dropStop, dropNode, RouteStopKind.DropOff, requestId, new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            vehicleId, 4, 0, new NodePosition(pickupNode), [], [], route, 0).Value!;
        var run = RideBoundRun.Create(
            new RunIdentifier("checkpoint-run"),
            new ScenarioIdentifier("checkpoint-scenario"),
            new SimTime(0));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(requestId, vehicleId).Value!;
        run = run.AdvanceEpoch(1, new SimTime(3)).Value!;
        var projection = new PromiseProjection(
            requestId,
            vehicleId,
            pickupStop,
            pickupNode,
            dropStop,
            dropNode,
            new SimTime(0),
            new SimTime(100),
            [
                new PromiseServiceToken(pickupStop, requestId, RouteStopKind.Pickup),
                new PromiseServiceToken(dropStop, requestId, RouteStopKind.DropOff),
            ]);
        var promises = CommitmentLedger.Empty.OpenInitial(
            "publication-1",
            projection,
            1,
            new SimTime(0),
            "INITIAL_ACCEPTANCE",
            1).Ledger!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            [new KeyValuePair<TravelArc, Duration>(new TravelArc(pickupNode, dropNode), new Duration(100))])
            .Value!;
        return (run, projection, promises, travel);
    }
}
