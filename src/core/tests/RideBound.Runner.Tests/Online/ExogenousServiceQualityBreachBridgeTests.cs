using RideBound.Algorithms.Candidates;
using RideBound.Application.Commitments;
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

public sealed class ExogenousServiceQualityBreachBridgeTests
{
    [Fact]
    public void Bridge_reconstructs_no_op_projection_and_preserves_decision_budget()
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
            new SimTime(1),
            new Duration(100),
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
            new RunIdentifier("bridge-run"),
            new ScenarioIdentifier("bridge-scenario"),
            new SimTime(0));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(requestId, vehicleId).Value!;
        run = run.AdvanceEpoch(1, new SimTime(3)).Value!;
        var publishedProjection = new PromiseProjection(
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
        var commitments = CommitmentLedger.Empty.OpenInitial(
            "publication-1",
            publishedProjection,
            1,
            new SimTime(0),
            "INITIAL_ACCEPTANCE",
            1).Ledger!;
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
            2,
            travel.SnapshotHash,
            commitments);
        var diagnostics = new CandidateGenerationDiagnostics(
            0,
            0,
            0,
            [],
            [],
            [
                new ExogenousServiceQualityBreach(
                    vehicleId,
                    requestId,
                    PhysicalViolationCodes.PickupWindow,
                    "latestPickupMs",
                    1,
                    3),
            ]);
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));

        var result = new ExogenousServiceQualityBreachBridge().Apply(
            state,
            state,
            diagnostics,
            new CommitmentPolicyCatalog([policy]),
            EmptyDistances.Instance,
            1);

        Assert.True(result.IsSuccess, result.Error);
        var breach = Assert.Single(result.State!.Incidents.Breaches);
        Assert.Equal(requestId, breach.RequestId);
        Assert.Equal(
            CommitmentBreachKind.ExogenousServiceQuality,
            breach.Kind);
        Assert.Equal(CommitmentVector.Zero, breach.Deltas.DecisionInduced);
        Assert.Equal(breach.BudgetBefore, breach.AttemptedBudgetAfter);
        Assert.Equal(3, breach.ExogenousProjection.PickupEta.Milliseconds);
        Assert.Equal(
            breach.ExogenousProjection.PickupEta,
            breach.SafetyProjection.PickupEta);
    }

    private sealed class EmptyDistances : IStopDistanceLookup
    {
        public static EmptyDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return fromNodeId == toNodeId;
        }
    }
}
