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
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Tests.Online;

/// <summary>
/// A state in which one waiting rider was moved (G2) checkpoints exactly: the Runner's canonical writer and the
/// reference path give the same bytes, with the rider on the receiver, both accepted sets and the switch-count revision
/// in its ledger.
/// </summary>
public sealed class WaitingRiderRelocationCheckpointTests
{
    private static readonly RequestId Rider = new("request-1");
    private static readonly VehicleId Donor = new("vehicle-1");
    private static readonly VehicleId Receiver = new("vehicle-2");
    private static readonly NodeId PickupNode = new("pickup-node");
    private static readonly NodeId DropNode = new("drop-node");

    [Fact]
    public void A_state_after_a_move_is_written_alike_by_both_state_paths()
    {
        var moved = MovedState();

        Assert.Equal(
            OnlineStateReferenceCanonicalizer.CanonicalizeReference(moved),
            OnlineStateCanonicalWriter.Write(moved, null).ToArray());
        var run = moved.Run;
        Assert.Equal(Receiver, run.Requests[Rider].AssignedVehicleId);
        Assert.Equal(RequestLifecycle.WaitingPickup, run.Requests[Rider].Lifecycle);
        Assert.Empty(run.Vehicles[Donor].AcceptedRequestIds);
        Assert.Equal([Rider], run.Vehicles[Receiver].AcceptedRequestIds);
        var current = moved.Commitments.Histories[Rider].Current;
        Assert.Equal(Receiver, current.PublishedPromise.Projection.VehicleId);
        Assert.Equal(Donor, current.ExogenousProjection.VehicleId);
        Assert.Equal(1, current.BudgetAfter.VehicleSwitchCount);
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
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(dimension, null, CommitmentPhase.AllActive)),
            finalConfirmationLocks: PromiseLock.PickupStop | PromiseLock.PickupEta,
            ratchetLocks: PromiseLock.PickupEta,
            waitingRiderRelocation: true);

        var validated = new CommitmentDecisionValidator().Validate(
            new CommitmentValidationContext(
                before,
                reduced,
                reduced with { Run = candidate },
                new CommitmentPolicyCatalog([policy]),
                "checkpoint-scope",
                2));

        Assert.True(validated.IsValid, string.Join("; ", validated.Witnesses.Select(value => value.Message)));
        return validated.ValidatedState!;
    }
}
