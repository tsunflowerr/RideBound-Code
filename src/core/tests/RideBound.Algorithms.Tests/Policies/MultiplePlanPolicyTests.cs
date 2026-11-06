using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Algorithms.Tests.Policies;

public sealed class MultiplePlanPolicyTests
{
    [Fact]
    public void Selector_applies_dominance_then_stable_consensus_over_diverse_pool()
    {
        var state = AtEpochOne(
            AlgorithmTestData.CreateState(
                [],
                [AlgorithmTestData.Vehicle()]));
        var candidates = new[]
        {
            Candidate(state, "a-cheapest", 10, 5, AlgorithmTestData.NodeOne,
                AlgorithmTestData.NodeTwo),
            Candidate(state, "b-consensus", 11, 10, AlgorithmTestData.NodeTwo,
                AlgorithmTestData.NodeOne),
            Candidate(state, "c-consensus", 11, 10, AlgorithmTestData.NodeTwo,
                AlgorithmTestData.NodeThree),
            Candidate(state, "d-dominated", 12, 4, AlgorithmTestData.NodeThree,
                AlgorithmTestData.NodeOne),
        };
        var set = new VehicleCandidateSet(
            AlgorithmTestData.VehicleOne,
            candidates,
            [],
            false);
        var selector = new MultiplePlanFleetSelector();
        var options = new MultiplePlanPoolOptions(3, 100, true);

        var result = selector.Select(state, [set], options);
        var reordered = selector.Select(
            state,
            [set with { Candidates = candidates.Reverse().ToArray() }],
            options);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.True(reordered.IsSuccess, reordered.Witness?.Message);
        Assert.Equal(1, result.Selection!.Diagnostics.DominatedPlanCount);
        Assert.Equal(3, result.Selection.PlanPool.Plans.Count);
        Assert.Equal(
            result.Selection.PlanPool.DistinguishedPlanId,
            reordered.Selection!.PlanPool.DistinguishedPlanId);
        Assert.Equal(
            result.Selection.PlanPool.Plans.Select(value => value.PlanId),
            reordered.Selection.PlanPool.Plans.Select(value => value.PlanId));
        Assert.Equal(
            AlgorithmTestData.NodeTwo,
            result.Selection.PlanPool.DistinguishedPlan!
                .VehiclePlans[0].Route.MutableSuffix[0].NodeId);
        Assert.NotEqual(
            "a-cheapest",
            result.Selection.DistinguishedSelection
                .VehiclePlans[0].Candidate.CandidateId);
    }

    [Fact]
    public void Exact_selector_fails_closed_when_combination_work_is_omitted()
    {
        var state = AtEpochOne(
            AlgorithmTestData.CreateState(
                [],
                [AlgorithmTestData.Vehicle()]));
        var set = new VehicleCandidateSet(
            AlgorithmTestData.VehicleOne,
            [
                Candidate(state, "a", 1, 1, AlgorithmTestData.NodeOne),
                Candidate(state, "b", 2, 2, AlgorithmTestData.NodeTwo),
                Candidate(state, "c", 3, 3, AlgorithmTestData.NodeThree),
            ],
            [],
            false);

        var result = new MultiplePlanFleetSelector().Select(
            state,
            [set],
            new MultiplePlanPoolOptions(2, 2, true));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            MultiplePlanFailureCodes.CombinationWorkBoundExceeded,
            result.Witness!.Code);
    }

    [Fact]
    public void Bounded_selector_reports_truncation_instead_of_claiming_complete_pool()
    {
        var state = AtEpochOne(
            AlgorithmTestData.CreateState(
                [],
                [AlgorithmTestData.Vehicle()]));
        var set = new VehicleCandidateSet(
            AlgorithmTestData.VehicleOne,
            [
                Candidate(state, "a", 1, 1, AlgorithmTestData.NodeOne),
                Candidate(state, "b", 2, 2, AlgorithmTestData.NodeTwo),
                Candidate(state, "c", 3, 3, AlgorithmTestData.NodeThree),
            ],
            [],
            false);

        var result = new MultiplePlanFleetSelector().Select(
            state,
            [set],
            new MultiplePlanPoolOptions(2, 2, false));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.True(result.Selection!.Diagnostics.WasCombinationWorkTruncated);
        Assert.Equal(2, result.Selection.Diagnostics.CombinationWorkUnits);
    }

    [Fact]
    public void Alternatives_with_a_different_new_request_assignment_are_removed()
    {
        var request = AlgorithmTestData.PendingRequest();
        var state = AtEpochOne(
            AlgorithmTestData.CreateState(
                [request],
                [
                    AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleOne),
                    AlgorithmTestData.Vehicle(AlgorithmTestData.VehicleTwo),
                ]));
        var vehicleOneAccept = RequestCandidate(
            state,
            AlgorithmTestData.VehicleOne,
            request.Id,
            "a-accept");
        var vehicleTwoAccept = RequestCandidate(
            state,
            AlgorithmTestData.VehicleTwo,
            request.Id,
            "z-accept");
        var vehicleOneNoOp = NoOp(state, AlgorithmTestData.VehicleOne, "z-noop");
        var vehicleTwoNoOp = NoOp(state, AlgorithmTestData.VehicleTwo, "a-noop");

        var result = new MultiplePlanFleetSelector().Select(
            state,
            [
                new VehicleCandidateSet(
                    AlgorithmTestData.VehicleOne,
                    [vehicleOneAccept, vehicleOneNoOp],
                    [],
                    false),
                new VehicleCandidateSet(
                    AlgorithmTestData.VehicleTwo,
                    [vehicleTwoAccept, vehicleTwoNoOp],
                    [],
                    false),
            ],
            new MultiplePlanPoolOptions(4, 100, true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var only = Assert.Single(result.Selection!.PlanPool.Plans);
        Assert.Contains(
            only.VehiclePlans.Single(
                value => value.VehicleId == AlgorithmTestData.VehicleOne)
                .Route.AllStops,
            stop => stop.RequestId == request.Id);
        Assert.DoesNotContain(
            only.VehiclePlans.Single(
                value => value.VehicleId == AlgorithmTestData.VehicleTwo)
                .Route.AllStops,
            stop => stop.RequestId == request.Id);
    }

    [Fact]
    public void Policy_publishes_only_distinguished_plan_and_versions_pool_in_state()
    {
        var first = AlgorithmTestData.PendingRequest(
            "request-a",
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            latestPickup: 20_000,
            maxRideTime: 20_000);
        var second = AlgorithmTestData.PendingRequest(
            "request-b",
            AlgorithmTestData.NodeThree,
            AlgorithmTestData.NodeTwo,
            latestPickup: 20_000,
            maxRideTime: 20_000);
        var state = AtEpochOne(
            AlgorithmTestData.CreateState(
                [first, second],
                [AlgorithmTestData.Vehicle()]));

        var result = new MultiplePlanConsensusPolicy().Decide(
            state,
            CandidateGenerationOptions.ExactSmall,
            new MultiplePlanPoolOptions(4, 1_000_000, true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var decision = result.Decision!;
        Assert.Equal(
            "least-commitment-consensus",
            new MultiplePlanConsensusPolicy().PolicyId);
        Assert.Equal(1, decision.PlanPool.Version);
        Assert.True(decision.PlanPool.Plans.Count > 1);
        Assert.Same(
            decision.PlanPool,
            decision.DistinguishedDecision.ProposedState.PlanPool);
        Assert.All(
            decision.DistinguishedDecision.RequestActions,
            action => Assert.Equal(RequestDecisionOutcome.Accepted, action.Outcome));

        var currentRoute = decision.DistinguishedDecision.ProposedState.Run
            .Vehicles[AlgorithmTestData.VehicleOne].Route;
        var distinguishedRoute = decision.PlanPool.DistinguishedPlan!
            .VehiclePlans.Single().Route;
        Assert.True(currentRoute.IsSemanticallyEqual(distinguishedRoute));
        Assert.Contains(
            decision.PlanPool.Plans,
            plan => !plan.VehiclePlans.Single().Route
                .IsSemanticallyEqual(currentRoute));

        foreach (var alternative in decision.PlanPool.Plans.Where(
                     plan => !plan.VehiclePlans.Single().Route
                         .IsSemanticallyEqual(currentRoute)))
        {
            var route = alternative.VehiclePlans.Single().Route;
            Assert.Equal(currentRoute.Version.Next(), route.Version);
            var validation = new PhysicalPlanValidator().Validate(
                new PhysicalValidationContext(
                    decision.DistinguishedDecision.ProposedState.Run,
                    AlgorithmTestData.VehicleOne,
                    route,
                    state.TravelTimes!,
                    state.Run.SimulationTime));
            Assert.True(validation.IsFeasible, validation.Witness?.Message);
        }
    }

    private static OnlineState AtEpochOne(OnlineState state) =>
        state with
        {
            Run = state.Run.AdvanceEpoch(1, state.Run.SimulationTime).Value!,
        };

    private static InsertionCandidate Candidate(
        OnlineState state,
        string id,
        long cost,
        long slack,
        params NodeId[] nodes)
    {
        var vehicle = state.Run.Vehicles[AlgorithmTestData.VehicleOne];
        var route = vehicle.Route.ReplaceMutableSuffix(
            nodes.Select(
                (node, index) => new RouteStop(
                    new StopId($"waypoint-{node.Value}-{index}"),
                    node,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)))).Value!;

        return new InsertionCandidate(
            id,
            vehicle.Id,
            route,
            [],
            new CandidateSchedule([], cost),
            false,
            CertifiedForwardSlackMilliseconds: slack);
    }

    private static InsertionCandidate RequestCandidate(
        OnlineState state,
        VehicleId vehicleId,
        RequestId requestId,
        string id)
    {
        var request = state.Run.Requests[requestId];
        var vehicle = state.Run.Vehicles[vehicleId];
        var route = vehicle.Route.ReplaceMutableSuffix(
            [
                new RouteStop(
                    new StopId($"{id}-pickup"),
                    request.OriginNodeId,
                    RouteStopKind.Pickup,
                    requestId,
                    new Duration(0)),
                new RouteStop(
                    new StopId($"{id}-drop"),
                    request.DestinationNodeId,
                    RouteStopKind.DropOff,
                    requestId,
                    new Duration(0)),
            ]).Value!;

        return new InsertionCandidate(
            id,
            vehicleId,
            route,
            [requestId],
            new CandidateSchedule([], 10),
            false,
            CertifiedForwardSlackMilliseconds: 10);
    }

    private static InsertionCandidate NoOp(
        OnlineState state,
        VehicleId vehicleId,
        string id) =>
        new(
            id,
            vehicleId,
            state.Run.Vehicles[vehicleId].Route,
            [],
            new CandidateSchedule([], 0),
            true,
            CertifiedForwardSlackMilliseconds: null);
}
