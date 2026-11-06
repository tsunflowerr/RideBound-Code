using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// Waiting-rider relocation (G2) through the real validator and ledger. Two vehicles start at
/// n-0; the rider waits on v-1 (pickup 1100 ms, drop 1200 ms). The candidate clears v-1 and puts
/// the same two stops on v-2, so a move costs the rider nothing unless v-2 is made slower.
/// </summary>
public sealed class WaitingRiderRelocationValidatorTests
{
    private const long Now = 1_000;
    private static readonly VehicleId Donor = ApplicationTestData.VehicleId;
    private static readonly VehicleId Receiver = new("v-2");

    [Fact]
    public void A_permitted_move_publishes_the_rider_on_the_receiver_and_charges_one_switch()
    {
        var result = new CommitmentDecisionValidator().Validate(Moved(Decision(Policy(relocation: true))));

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        var entry = Assert.Single(result.Publications).Entry;
        Assert.Equal(Receiver, entry.PublishedPromise.Projection.VehicleId);
        Assert.Equal(Donor, entry.ExogenousProjection.VehicleId);
        Assert.Equal(1, entry.BudgetAfter.VehicleSwitchCount);
        Assert.Equal(0, entry.BudgetAfter.DropEtaTotalMs);
        Assert.Equal(1_100, entry.PublishedPromise.Projection.PickupEta.Milliseconds);
        Assert.Equal(1_200, entry.PublishedPromise.Projection.DropEta.Milliseconds);
    }

    [Fact]
    public void Without_the_flag_the_same_move_breaks_the_decision_boundary()
    {
        var result = new CommitmentDecisionValidator().Validate(Moved(Decision(Policy(relocation: false))));

        Assert.False(result.IsValid);
        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.State, witness.Stage);
        Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", witness.Code);
    }

    [Fact]
    public void A_move_touching_a_forced_reference_vehicle_breaks_the_boundary()
    {
        foreach (var forced in new[] { Donor, Receiver })
        {
            var result = new CommitmentDecisionValidator().Validate(
                Moved(Decision(Policy(relocation: true)))
                    with { ForcedReferenceVehicles = new HashSet<VehicleId> { forced } });

            Assert.False(result.IsValid);
            Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", Assert.Single(result.Witnesses).Code);
        }
    }

    [Fact]
    public void A_move_that_delays_the_pickup_fails_the_ratchet_and_an_earlier_one_passes()
    {
        // A 1 ms dwell before the pickup on v-2 makes the pickup 1 ms later than the kept route.
        var later = new CommitmentDecisionValidator().Validate(
            Moved(Decision(Policy(relocation: true)), receiverDwellBeforePickup: 1));

        Assert.False(later.IsValid);
        var witness = Assert.Single(later.Witnesses);
        Assert.Equal(CommitmentValidationStage.Lock, witness.Stage);
        Assert.Equal("pickup_eta_ms", witness.Dimension);
        Assert.Equal("final_confirmation", witness.Rule);
    }

    [Fact]
    public void A_drop_delay_of_the_moved_rider_is_charged_against_its_budget()
    {
        // Rule C on the moved rider: the drop on v-2 is 300 ms later than the kept route on v-1.
        var within = new CommitmentDecisionValidator().Validate(
            Moved(Decision(Policy(relocation: true, dropBudget: 300)), receiverDwellBeforeDrop: 300));
        var beyond = new CommitmentDecisionValidator().Validate(
            Moved(Decision(Policy(relocation: true, dropBudget: 299)), receiverDwellBeforeDrop: 300));

        Assert.True(within.IsValid, string.Join("; ", within.Witnesses.Select(w => w.Message)));
        Assert.Equal(300, Assert.Single(within.Publications).Entry.BudgetAfter.DropEtaTotalMs);
        Assert.False(beyond.IsValid);
        var witness = Assert.Single(beyond.Witnesses);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
        Assert.Equal("drop_eta_total_ms", witness.Dimension);
    }

    [Fact]
    public void A_switch_limit_of_one_admits_the_first_move()
    {
        var result = new CommitmentDecisionValidator().Validate(
            Moved(Decision(Policy(relocation: true, switchLimit: 1))));

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        Assert.Equal(1, Assert.Single(result.Publications).Entry.BudgetAfter.VehicleSwitchCount);
    }

    [Fact]
    public void Two_moves_in_one_decision_break_the_boundary()
    {
        var second = RideRequest.CreatePending(
            new RequestId("r-2"),
            new SimTime(1000),
            ApplicationTestData.NodeOne,
            ApplicationTestData.NodeTwo,
            new SimTime(1000),
            new SimTime(2000),
            new Duration(1000),
            1,
            "standard",
            "uniform-v1").Value!;
        var context = Decision(Policy(relocation: true), second);
        var run = context.CandidateState.Run
            .UpdateVehicleRoute(Donor, Replace(context.CandidateState.Run.Vehicles[Donor].Route, [])).Value!;
        run = run.UpdateVehicleRoute(
            Receiver,
            Replace(run.Vehicles[Receiver].Route, TwoRiderStops(ApplicationTestData.RequestId, second.Id))).Value!;
        run = run.ReassignWaitingRequest(ApplicationTestData.RequestId, Receiver).Value!;
        run = run.ReassignWaitingRequest(second.Id, Receiver).Value!;

        var result = new CommitmentDecisionValidator().Validate(
            context with { CandidateState = context.CandidateState with { Run = run } });

        Assert.False(result.IsValid);
        Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", Assert.Single(result.Witnesses).Code);
    }

    [Fact]
    public void A_half_move_without_the_reassignment_is_still_a_reassignment_violation()
    {
        // Routes changed but the request still names the donor: the receiver carries a rider
        // that is not its own, exactly as before relocation existed.
        var context = Decision(Policy(relocation: true));
        var run = context.CandidateState.Run
            .UpdateVehicleRoute(Donor, Replace(context.CandidateState.Run.Vehicles[Donor].Route, [])).Value!
            .UpdateVehicleRoute(Receiver, Replace(context.CandidateState.Run.Vehicles[Receiver].Route, Stops(ApplicationTestData.RequestId))).Value!;

        var result = new CommitmentDecisionValidator().Validate(
            context with { CandidateState = context.CandidateState with { Run = run } });

        Assert.False(result.IsValid);
        Assert.Equal("COMMITMENT_STATE_BOUNDARY_MISMATCH", Assert.Single(result.Witnesses).Code);
    }

    private static CommitmentPolicy Policy(
        bool relocation,
        long? dropBudget = null,
        long? switchLimit = null) =>
        new(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension switch
                    {
                        CommitmentDimension.DropEtaTotalMs => dropBudget,
                        CommitmentDimension.VehicleSwitchCount => switchLimit,
                        _ => null,
                    },
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            finalConfirmationLocks: PromiseLock.PickupStop | PromiseLock.PickupEta,
            ratchetLocks: PromiseLock.PickupEta,
            waitingRiderRelocation: relocation);

    private static RouteStop[] Stops(RequestId requestId, string suffix = "") =>
    [
        new(new StopId("pickup" + suffix), ApplicationTestData.NodeOne, RouteStopKind.Pickup, requestId, new Duration(0)),
        new(new StopId("drop" + suffix), ApplicationTestData.NodeTwo, RouteStopKind.DropOff, requestId, new Duration(0)),
    ];

    /// <summary>
    /// The decision at <see cref="Now"/>: every rider waits on v-1 with its first promise open,
    /// v-2 is empty; the candidate state equals the reduced state.
    /// </summary>
    private static CommitmentValidationContext Decision(CommitmentPolicy policy, RideRequest? second = null)
    {
        var request = ApplicationTestData.Request();
        var donorStops = second is null
            ? Stops(request.Id)
            : TwoRiderStops(request.Id, second.Id);
        var donorRoute = RoutePlan.Create(new PlanVersion(0), 0, [], donorStops).Value!;
        var run = RideBoundRun.Create(ApplicationTestData.RunId, ApplicationTestData.ScenarioId, new SimTime(Now));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(Vehicle(Donor, donorRoute)).Value!;
        run = run.BootstrapVehicle(Vehicle(Receiver, RoutePlan.Create(new PlanVersion(0), 0, [], []).Value!)).Value!;
        run = run.AcceptRequest(request.Id, Donor).Value!;
        run = run.ConfirmWaitingPickup(request.Id).Value!;

        if (second is not null)
        {
            run = run.AddRequest(second).Value!;
            run = run.AcceptRequest(second.Id, Donor).Value!;
            run = run.ConfirmWaitingPickup(second.Id).Value!;
        }

        run = run.AdvanceEpoch(1, new SimTime(Now)).Value!;
        var travel = ApplicationTestData.Travel();
        var schedule = new RouteScheduleProjector().Project(
            run,
            run.Vehicles[Donor],
            donorRoute,
            travel,
            new SimTime(Now)).Schedule!;
        var ledger = CommitmentLedger.Empty;

        foreach (var rider in second is null ? [request.Id] : new[] { request.Id, second.Id })
        {
            var initial = new PromiseProjector().Project(run, run.Vehicles[Donor], donorRoute, schedule, rider).Value!;
            ledger = ledger.OpenInitial(
                "initial-" + rider.Value,
                initial,
                1,
                new SimTime(Now),
                "INITIAL_ACCEPTANCE",
                3).Ledger!;
        }

        Assert.Equal(1_100, ledger.Histories[request.Id].Current.PublishedPromise.Projection.PickupEta.Milliseconds);
        var before = new OnlineState(run, travel, 4, travel.SnapshotHash, ledger);
        var reduced = before with
        {
            Run = run.AdvanceEpoch(2, new SimTime(Now)).Value!,
            NextEventSequence = 5,
        };

        return new CommitmentValidationContext(
            before,
            reduced,
            reduced,
            new CommitmentPolicyCatalog([policy]),
            CommitmentValidatorFixtures.EmptyDistances.Instance,
            "test-scope",
            4);
    }

    /// <summary>The candidate: v-1 cleared, the rider's stops on v-2 (with optional dwells), reassigned.</summary>
    private static CommitmentValidationContext Moved(
        CommitmentValidationContext context,
        long receiverDwellBeforePickup = 0,
        long receiverDwellBeforeDrop = 0)
    {
        var stops = Stops(ApplicationTestData.RequestId).ToList();

        if (receiverDwellBeforeDrop > 0)
        {
            stops.Insert(1, Wait("wait-drop", receiverDwellBeforeDrop, ApplicationTestData.NodeOne));
        }

        if (receiverDwellBeforePickup > 0)
        {
            stops.Insert(0, Wait("wait-pickup", receiverDwellBeforePickup));
        }

        var run = context.CandidateState.Run
            .UpdateVehicleRoute(Donor, Replace(context.CandidateState.Run.Vehicles[Donor].Route, [])).Value!;
        run = run.UpdateVehicleRoute(Receiver, Replace(run.Vehicles[Receiver].Route, [.. stops])).Value!;
        run = run.ReassignWaitingRequest(ApplicationTestData.RequestId, Receiver).Value!;
        return context with { CandidateState = context.CandidateState with { Run = run } };
    }

    private static RouteStop Wait(string stopId, long milliseconds, NodeId? nodeId = null) =>
        new(new StopId(stopId), nodeId ?? ApplicationTestData.NodeZero, RouteStopKind.Waypoint, null, new Duration(milliseconds));

    /// <summary>Both riders' stops as pickup, pickup, drop, drop (the snapshot has no n-2 -> n-1 arc).</summary>
    private static RouteStop[] TwoRiderStops(RequestId first, RequestId second)
    {
        var a = Stops(first);
        var b = Stops(second, "2");
        return [a[0], b[0], a[1], b[1]];
    }

    private static RoutePlan Replace(RoutePlan route, RouteStop[] suffix) =>
        RoutePlan.Create(new PlanVersion(route.Version.Value + 1), route.ExecutedStopCount, route.FrozenPrefix, suffix).Value!;

    private static VehicleState Vehicle(VehicleId id, RoutePlan route) =>
        VehicleState.Create(id, 4, 0, new NodePosition(ApplicationTestData.NodeZero), [], [], route, 1).Value!;
}
