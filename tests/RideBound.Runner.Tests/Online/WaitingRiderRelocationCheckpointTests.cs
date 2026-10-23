using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

/// <summary>
/// Review gap S2: a state in which one waiting rider was moved (G2) must checkpoint and restore exactly, with the
/// rider on the receiver, both accepted sets and the switch-count revision in its ledger; a checkpoint that puts the
/// rider back on the donor without its route must not restore.
/// </summary>
public sealed class WaitingRiderRelocationCheckpointTests
{
    private static readonly RequestId Rider = new("request-1");
    private static readonly VehicleId Donor = new("vehicle-1");
    private static readonly VehicleId Receiver = new("vehicle-2");
    private static readonly NodeId PickupNode = new("pickup-node");
    private static readonly NodeId DropNode = new("drop-node");

    [Fact]
    public void A_state_after_a_move_round_trips_exactly()
    {
        var moved = MovedState();
        var canonical = OnlineStateCanonicalizer.Canonicalize(moved);
        using var document = JsonDocument.Parse(canonical);

        var decoded = OnlineStateCheckpointCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error);
        Assert.Equal(canonical, OnlineStateCanonicalizer.Canonicalize(decoded.State!));
        var run = decoded.State!.Run;
        Assert.Equal(Receiver, run.Requests[Rider].AssignedVehicleId);
        Assert.Equal(RequestLifecycle.WaitingPickup, run.Requests[Rider].Lifecycle);
        Assert.Empty(run.Vehicles[Donor].AcceptedRequestIds);
        Assert.Equal([Rider], run.Vehicles[Receiver].AcceptedRequestIds);
        var current = decoded.State.Commitments.Histories[Rider].Current;
        Assert.Equal(Receiver, current.PublishedPromise.Projection.VehicleId);
        Assert.Equal(Donor, current.ExogenousProjection.VehicleId);
        Assert.Equal(1, current.BudgetAfter.VehicleSwitchCount);
    }

    [Fact]
    public void A_checkpoint_that_moves_the_rider_back_without_its_route_does_not_restore()
    {
        var canonical = OnlineStateCanonicalizer.Canonicalize(MovedState());
        var forged = JsonNode.Parse(canonical)!;
        var replaced = ReplaceAssignedVehicle(forged, Rider.Value, Donor.Value);
        Assert.Equal(1, replaced);
        using var forgedDocument = JsonDocument.Parse(forged.ToJsonString());

        Assert.False(OnlineStateCheckpointCodec.Decode(forgedDocument.RootElement).IsSuccess);
    }

    /// <summary>
    /// The validated state of one decision that moves the waiting rider from vehicle-1 to vehicle-2 (both idle at the
    /// pickup node), under a policy with waiting-rider relocation and the pickup ratchet.
    /// </summary>
    private static OnlineState MovedState()
    {
        var request = RideRequest.CreatePending(
            Rider, new SimTime(0), PickupNode, DropNode, new SimTime(0), new SimTime(1_000), new Duration(1_000), 1,
            "standard", "uniform-v1").Value!;
        var pickup = new RouteStop(new StopId("pickup-stop"), PickupNode, RouteStopKind.Pickup, Rider, new Duration(0));
        var drop = new RouteStop(new StopId("drop-stop"), DropNode, RouteStopKind.DropOff, Rider, new Duration(0));
        var donorRoute = RoutePlan.Create(new PlanVersion(0), 0, [], [pickup, drop]).Value!;
        var emptyRoute = RoutePlan.Create(new PlanVersion(0), 0, [], []).Value!;
        var run = RideBoundRun.Create(new RunIdentifier("checkpoint-run"), new ScenarioIdentifier("checkpoint-scenario"), new SimTime(0));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(VehicleState.Create(Donor, 4, 0, new NodePosition(PickupNode), [], [], donorRoute, 0).Value!).Value!;
        run = run.BootstrapVehicle(VehicleState.Create(Receiver, 4, 0, new NodePosition(PickupNode), [], [], emptyRoute, 0).Value!).Value!;
        run = run.AcceptRequest(Rider, Donor).Value!;
        run = run.ConfirmWaitingPickup(Rider).Value!;
        run = run.AdvanceEpoch(1, new SimTime(0)).Value!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            [new KeyValuePair<TravelArc, Duration>(new TravelArc(PickupNode, DropNode), new Duration(100))]).Value!;
        var schedule = new RouteScheduleProjector().Project(run, run.Vehicles[Donor], donorRoute, travel, new SimTime(0)).Schedule!;
        var initial = new PromiseProjector().Project(run, run.Vehicles[Donor], donorRoute, schedule, Rider).Value!;
        var ledger = CommitmentLedger.Empty.OpenInitial("publication-1", initial, 1, new SimTime(0), "INITIAL_ACCEPTANCE", 1).Ledger!;
        var before = new OnlineState(run, travel, 2, travel.SnapshotHash, ledger);
        var reduced = before with { Run = run.AdvanceEpoch(2, new SimTime(0)).Value!, NextEventSequence = 3 };
        var candidate = reduced.Run
            .UpdateVehicleRoute(Donor, RoutePlan.Create(new PlanVersion(1), 0, [], []).Value!).Value!
            .UpdateVehicleRoute(Receiver, RoutePlan.Create(new PlanVersion(1), 0, [], [pickup, drop]).Value!).Value!
            .ReassignWaitingRequest(Rider, Receiver).Value!;
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(dimension, null, CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, null),
            finalConfirmationLocks: PromiseLock.PickupStop | PromiseLock.PickupEta,
            ratchetLocks: PromiseLock.PickupEta,
            waitingRiderRelocation: true);

        var validated = new CommitmentDecisionValidator().Validate(
            new CommitmentValidationContext(
                before,
                reduced,
                reduced with { Run = candidate },
                new CommitmentPolicyCatalog([policy]),
                NoDistances.Instance,
                "checkpoint-scope",
                2));

        Assert.True(validated.IsValid, string.Join("; ", validated.Witnesses.Select(value => value.Message)));
        return validated.ValidatedState!;
    }

    /// <summary>Sets assignedVehicleId of the request object with the given requestId; returns how many changed.</summary>
    private static int ReplaceAssignedVehicle(JsonNode? node, string requestId, string vehicleId)
    {
        var changed = 0;

        if (node is JsonObject obj)
        {
            if ((string?)obj["requestId"] == requestId && obj.ContainsKey("assignedVehicleId"))
            {
                obj["assignedVehicleId"] = vehicleId;
                changed++;
            }

            foreach (var (_, child) in obj.ToArray())
            {
                changed += ReplaceAssignedVehicle(child, requestId, vehicleId);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                changed += ReplaceAssignedVehicle(child, requestId, vehicleId);
            }
        }

        return changed;
    }

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(NodeId fromNodeId, NodeId toNodeId, out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
