using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Tests.Policies;

public sealed class RollingCostPolicyTests
{
    private static readonly CandidateGenerationOptions ExactOptions =
        new(10_000, 4, exactSmallMode: true);

    [Fact]
    public void Lower_integer_operational_cost_wins_across_two_vehicles()
    {
        var request = AlgorithmTestData.PendingRequest();
        var near = AlgorithmTestData.Vehicle(
            AlgorithmTestData.VehicleOne,
            position: AlgorithmTestData.NodeZero);
        var far = AlgorithmTestData.Vehicle(
            AlgorithmTestData.VehicleTwo,
            position: AlgorithmTestData.NodeThree);
        var state = AlgorithmTestData.CreateState([request], [near, far]);

        var result = new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var accepted = Assert.Single(
            result.Decision!.RequestActions,
            value => value.Outcome == RequestDecisionOutcome.Accepted);
        Assert.Equal(AlgorithmTestData.VehicleOne, accepted.VehicleId);
        Assert.Equal(
            RequestLifecycle.Accepted,
            result.Decision.ProposedState.Run.Requests[request.Id].Lifecycle);
        Assert.Equal(
            AlgorithmTestData.VehicleOne,
            result.Decision.ProposedState.Run.Requests[
                request.Id].AssignedVehicleId);
    }

    [Fact]
    public void Complete_tie_uses_candidate_id_ordinal()
    {
        var request = AlgorithmTestData.PendingRequest();
        var first = AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleOne);
        var second = AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleTwo);
        var state = AlgorithmTestData.CreateState([request], [second, first]);
        var generator = new InsertionCandidateGenerator();
        var generated = generator.Generate(state, ExactOptions);
        var sets = generated.VehicleCandidates!
            .OrderBy(value => value.VehicleId.Value, StringComparer.Ordinal)
            .ToArray();
        var acceptOnFirst = new[]
        {
            sets[0].Candidates.Single(
                value => value.NewRequestIds.Contains(request.Id)).CandidateId,
            sets[1].Candidates.Single(value => value.IsNoOp).CandidateId,
        };
        var acceptOnSecond = new[]
        {
            sets[0].Candidates.Single(value => value.IsNoOp).CandidateId,
            sets[1].Candidates.Single(
                value => value.NewRequestIds.Contains(request.Id)).CandidateId,
        };
        var expectedVehicle = CompareKeys(acceptOnFirst, acceptOnSecond) <= 0
            ? AlgorithmTestData.VehicleOne
            : AlgorithmTestData.VehicleTwo;

        var result = new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var selected = result.Decision!.VehiclePlans.Single(
            value => value.Candidate.NewRequestIds.Contains(request.Id));
        Assert.Equal(expectedVehicle, selected.VehicleId);
    }

    [Fact]
    public void Request_is_served_at_most_once_and_other_vehicle_selects_no_op()
    {
        var request = AlgorithmTestData.PendingRequest();
        var state = AlgorithmTestData.CreateState(
            [request],
            [
                AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleOne),
                AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleTwo),
            ]);

        var result = new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(2, result.Decision!.VehiclePlans.Count);
        Assert.Single(
            result.Decision.VehiclePlans,
            value => value.Candidate.NewRequestIds.Contains(request.Id));
        Assert.Single(
            result.Decision.VehiclePlans,
            value => value.Candidate.IsNoOp);
    }

    [Fact]
    public void Physical_impossibility_rejects_pending_request_and_keeps_no_op()
    {
        var request = AlgorithmTestData.PendingRequest(partySize: 5);
        var state = AlgorithmTestData.CreateState(
            [request],
            [AlgorithmTestData.Vehicle(capacity: 4)]);

        var result = new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var action = Assert.Single(result.Decision!.RequestActions);
        Assert.Equal(RequestDecisionOutcome.Rejected, action.Outcome);
        Assert.Equal("CAPACITY", action.ReasonCode);
        Assert.Equal(
            RequestLifecycle.Rejected,
            result.Decision.ProposedState.Run.Requests[request.Id].Lifecycle);
        Assert.True(Assert.Single(result.Decision.VehiclePlans).Candidate.IsNoOp);
    }

    [Fact]
    public void Mixed_prune_causes_use_the_generic_rejection_reason()
    {
        var request = AlgorithmTestData.PendingRequest(
            latestPickup: 1_050,
            partySize: 5);
        var state = AlgorithmTestData.CreateState(
            [request],
            [
                AlgorithmTestData.Vehicle(
                    AlgorithmTestData.VehicleOne,
                    capacity: 4,
                    position: AlgorithmTestData.NodeOne),
                AlgorithmTestData.Vehicle(
                    AlgorithmTestData.VehicleTwo,
                    capacity: 6,
                    position: AlgorithmTestData.NodeThree),
            ]);

        var result = new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var action = Assert.Single(result.Decision!.RequestActions);
        Assert.Equal(RequestDecisionOutcome.Rejected, action.Outcome);
        Assert.Equal(RollingCostReasonCodes.NoFeasibleInsertion, action.ReasonCode);
        Assert.Contains(result.Decision.PrunedCandidates, value => value.Code == "CAPACITY");
        Assert.Contains(
            result.Decision.PrunedCandidates,
            value => value.Code == "PICKUP_WINDOW");
    }

    [Fact]
    public void Feasible_unselected_request_is_deferred_and_stays_pending()
    {
        var first = AlgorithmTestData.PendingRequest(
            "request-a",
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            latestPickup: 1_150);
        var second = AlgorithmTestData.PendingRequest(
            "request-b",
            AlgorithmTestData.NodeThree,
            AlgorithmTestData.NodeTwo,
            latestPickup: 1_150);
        var state = AlgorithmTestData.CreateState(
            [first, second],
            [AlgorithmTestData.Vehicle()]);

        var result = new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Single(
            result.Decision!.RequestActions,
            value => value.Outcome == RequestDecisionOutcome.Accepted);
        var deferred = Assert.Single(
            result.Decision.RequestActions,
            value => value.Outcome == RequestDecisionOutcome.Deferred);
        Assert.Equal(
            RequestLifecycle.Pending,
            result.Decision.ProposedState.Run.Requests[
                deferred.RequestId].Lifecycle);
    }

    [Fact]
    public void Accepted_incumbent_is_preserved_and_never_rejected()
    {
        var incumbentPending = AlgorithmTestData.PendingRequest("incumbent");
        var pickup = new RouteStop(
            new StopId("incumbent-pickup"),
            incumbentPending.OriginNodeId,
            RouteStopKind.Pickup,
            incumbentPending.Id,
            new Duration(0));
        var drop = new RouteStop(
            new StopId("incumbent-drop"),
            incumbentPending.DestinationNodeId,
            RouteStopKind.DropOff,
            incumbentPending.Id,
            new Duration(0));
        var route = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [pickup, drop]).Value!;
        var vehicle = RideBound.Domain.Vehicles.VehicleState.Create(
            AlgorithmTestData.VehicleOne,
            4,
            0,
            new NodePosition(AlgorithmTestData.NodeZero),
            [],
            [incumbentPending.Id],
            route,
            1).Value!;
        var acceptedRequest = incumbentPending.Accept(
            AlgorithmTestData.VehicleOne).Value!;
        var run = RideBound.Domain.Runs.RideBoundRun.Create(
            AlgorithmTestData.RunId,
            AlgorithmTestData.ScenarioId,
            new SimTime(1_000));
        run = run.AddRequest(acceptedRequest).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        var travel = RideBound.Application.Travel.TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            AlgorithmTestData.CompleteArcs()).Value!;
        var state = new RideBound.Application.State.OnlineState(
            run,
            travel,
            1,
            travel.SnapshotHash,
            RideBound.Domain.Commitments.CommitmentLedger.Empty);

        var result = new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Equal(
            RequestLifecycle.Accepted,
            result.Decision!.ProposedState.Run.Requests[
                incumbentPending.Id].Lifecycle);
        Assert.Equal(
            AlgorithmTestData.VehicleOne,
            result.Decision.ProposedState.Run.Requests[
                incumbentPending.Id].AssignedVehicleId);
    }

    private static int CompareKeys(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        for (var index = 0; index < left.Count; index++)
        {
            var comparison = StringComparer.Ordinal.Compare(
                left[index],
                right[index]);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}
