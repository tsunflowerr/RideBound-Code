using System.Text.Json;
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
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Tests.Online;

/// <summary>
/// The checkpoint's online-state bytes on hand-built states: the Runner's canonical writer agrees with the independent
/// reference path on breaches, late pickups and Shadow allocations, and leaves out the members a state does not use.
/// </summary>
public sealed class OnlineStateWriterTests
{
    [Fact]
    public void An_exogenous_breach_is_written_alike_by_both_state_paths()
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
        var incidents = OperationalIncidentLedger.Empty.AppendBreach(
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
        var canonical = OnlineStateCanonicalWriter.Write(state, null).ToArray();

        Assert.Equal(canonical, OnlineStateReferenceCanonicalizer.CanonicalizeReference(state));
        using var document = JsonDocument.Parse(canonical);
        var breach = Assert.Single(
            document.RootElement.GetProperty("incidentLedger").GetProperty("breaches").EnumerateArray());
        Assert.Equal("exogenous-breach-1", breach.GetProperty("breachId").GetString());
        Assert.Equal("exogenousServiceQuality", breach.GetProperty("kind").GetString());
        Assert.Single(breach.GetProperty("serviceQualityWitnesses").EnumerateArray());
        Assert.Equal(
            breach.GetProperty("budgetBefore").GetRawText(),
            breach.GetProperty("attemptedBudgetAfter").GetRawText());
    }

    [Fact]
    public void A_late_pickup_is_written_alike_by_both_state_paths()
    {
        // Window [0, 1000] ms; the rider boards at 1500 ms and the ledger records the late pickup.
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
            new SimTime(1_500)).Value!;
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
        var canonical = OnlineStateCanonicalWriter.Write(state, null).ToArray();

        Assert.Equal(canonical, OnlineStateReferenceCanonicalizer.CanonicalizeReference(state));
        using var document = JsonDocument.Parse(canonical);
        var late = Assert.Single(
            document.RootElement.GetProperty("incidentLedger").GetProperty("latePickups").EnumerateArray());
        Assert.Equal(1_500, late.GetProperty("actualPickupMs").GetInt64());
        Assert.Equal(1_000, late.GetProperty("latestPickupMs").GetInt64());
        Assert.Equal(500, Assert.Single(state.Incidents.LatePickups).LatenessMilliseconds);
    }

    [Fact]
    public void Shadow_allocations_are_written_by_both_state_paths_and_are_absent_by_default()
    {
        // Shadow Control: the fast writer and the reference canonicalizer.
        var (run, _, promises, travel) = SingleRiderState();
        var plain = new OnlineState(run, travel, 5, travel.SnapshotHash, promises);
        var shadow = plain with
        {
            Commitments = promises.WithShadowAllocation(
                new RequestId("request-1"),
                new ShadowAllocation(ShadowAllocationClass.Guaranteed, "52.494082647339084")),
        };

        var plainCanonical = OnlineStateCanonicalWriter.Write(plain, null).ToArray();
        var shadowCanonical = OnlineStateCanonicalWriter.Write(shadow, null).ToArray();
        using var plainDocument = JsonDocument.Parse(plainCanonical);
        Assert.False(plainDocument.RootElement.TryGetProperty("shadowAllocations", out _));
        Assert.Contains(
            "\"shadowAllocations\":[{\"class\":\"guaranteed\",\"requestId\":\"request-1\",\"score\":\"52.494082647339084\"}]",
            System.Text.Encoding.UTF8.GetString(shadowCanonical));
        Assert.Equal(plainCanonical, OnlineStateReferenceCanonicalizer.CanonicalizeReference(plain));
        Assert.Equal(shadowCanonical, OnlineStateReferenceCanonicalizer.CanonicalizeReference(shadow));
    }

    [Fact]
    public void A_state_without_late_pickups_writes_no_late_pickup_field()
    {
        var (run, _, promises, travel) = SingleRiderState();
        var state = new OnlineState(run, travel, 5, travel.SnapshotHash, promises);

        using var document = JsonDocument.Parse(OnlineStateCanonicalWriter.Write(state, null).ToArray());

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
