using RideBound.Algorithms.Candidates;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Tests.Candidates;

public sealed class InsertionCandidateGeneratorTests
{
    private readonly InsertionCandidateGenerator _generator = new();

    [Fact]
    public void Empty_route_has_one_insertion_and_one_no_op()
    {
        var request = AlgorithmTestData.PendingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!).Candidates;
        Assert.Equal(2, candidates.Count);
        Assert.Single(candidates, value => value.IsNoOp);
        var insertion = Assert.Single(candidates, value => !value.IsNoOp);
        Assert.Equal([request.Id], insertion.NewRequestIds);
        Assert.Equal(
            [RouteStopKind.Pickup, RouteStopKind.DropOff],
            insertion.Route.MutableSuffix.Select(value => value.Kind));
    }

    [Fact]
    public void Existing_mutable_stop_has_all_hand_enumerated_pair_positions()
    {
        var request = AlgorithmTestData.PendingRequest();
        var waypoint = new RouteStop(
            new StopId("waypoint"),
            AlgorithmTestData.NodeThree,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var vehicle = AlgorithmTestData.Vehicle(mutableSuffix: [waypoint]);
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!).Candidates;
        Assert.Equal(4, candidates.Count);
        Assert.Equal(
            3,
            candidates.Count(value => !value.IsNoOp));
        Assert.Equal(
            candidates.OrderBy(value => value.CandidateId, StringComparer.Ordinal),
            candidates);
    }

    [Fact]
    public void Frozen_prefix_is_never_an_insertion_position_and_input_is_unchanged()
    {
        var request = AlgorithmTestData.PendingRequest();
        var frozen = new RouteStop(
            new StopId("frozen"),
            AlgorithmTestData.NodeZero,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var route = RoutePlan.Create(
            new PlanVersion(4),
            1,
            [frozen],
            []).Value!;
        var vehicle = RideBound.Domain.Vehicles.VehicleState.Create(
            AlgorithmTestData.VehicleOne,
            4,
            0,
            new NodePosition(AlgorithmTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var first = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1));
        var second = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1));

        Assert.True(first.IsSuccess, first.Witness?.Message);
        Assert.True(second.IsSuccess, second.Witness?.Message);
        Assert.Same(route, vehicle.Route);
        Assert.Equal([frozen], route.FrozenPrefix);
        Assert.All(
            first.VehicleCandidates!.Single().Candidates,
            candidate => Assert.Equal([frozen], candidate.Route.FrozenPrefix));
        Assert.Equal(
            first.VehicleCandidates!.Single().Candidates.Select(
                value => value.CandidateId),
            second.VehicleCandidates!.Single().Candidates.Select(
                value => value.CandidateId));
    }

    [Fact]
    public void Physical_invalid_candidates_are_pruned_with_witness()
    {
        var request = AlgorithmTestData.PendingRequest(partySize: 5);
        var vehicle = AlgorithmTestData.Vehicle(capacity: 4);
        var state = AlgorithmTestData.CreateState([request], [vehicle]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var set = Assert.Single(result.VehicleCandidates!);
        Assert.Single(set.Candidates);
        Assert.True(set.Candidates[0].IsNoOp);
        var prune = Assert.Single(set.PrunedCandidates);
        Assert.Equal("CAPACITY", prune.Code);
        Assert.Equal(request.Id, Assert.Single(prune.NewRequestIds));
        Assert.NotNull(prune.PhysicalWitness);
    }

    [Fact]
    public void Physically_invalid_active_route_fails_with_original_no_op_witness()
    {
        var waypoint = new RouteStop(
            new StopId("unreachable"),
            AlgorithmTestData.NodeOne,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var state = AlgorithmTestData.CreateState(
            [],
            [AlgorithmTestData.Vehicle(mutableSuffix: [waypoint])],
            arcs: AlgorithmTestData.CompleteArcs().Where(
                pair => pair.Key != new RideBound.Application.Travel.TravelArc(
                    AlgorithmTestData.NodeZero,
                    AlgorithmTestData.NodeOne)));

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CandidateGenerationFailureCodes.ActiveRouteInfeasible,
            result.Witness?.Code);
        Assert.Contains("ROUTE_CONNECTIVITY", result.Witness?.Message);
    }

    [Fact]
    public void Bounded_mode_retains_no_op_under_stable_cap()
    {
        var request = AlgorithmTestData.PendingRequest();
        var waypoint = new RouteStop(
            new StopId("waypoint"),
            AlgorithmTestData.NodeThree,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var state = AlgorithmTestData.CreateState(
            [request],
            [AlgorithmTestData.Vehicle(mutableSuffix: [waypoint])]);
        var options =
            new CandidateGenerationOptions(2, 1);

        var first = _generator.Generate(state, options);
        var second = _generator.Generate(state, options);

        Assert.True(first.IsSuccess, first.Witness?.Message);
        var set = Assert.Single(first.VehicleCandidates!);
        Assert.Equal(2, set.Candidates.Count);
        Assert.Contains(set.Candidates, value => value.IsNoOp);
        Assert.Equal(
            set.Candidates.Select(value => value.CandidateId),
            second.VehicleCandidates!.Single().Candidates.Select(
                value => value.CandidateId));
    }

    [Fact]
    public void Two_requests_enumerate_every_precedence_preserving_empty_route_order()
    {
        var firstRequest = AlgorithmTestData.PendingRequest("request-1");
        var secondRequest = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree);
        var state = AlgorithmTestData.CreateState(
            [secondRequest, firstRequest],
            [AlgorithmTestData.Vehicle()]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 2));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!).Candidates;
        Assert.Equal(9, candidates.Count);
        Assert.Equal(
            6,
            candidates.Count(value => value.NewRequestIds.Count == 2));
    }

    [Fact]
    public void Bounded_request_selection_uses_deadline_arrival_and_id_priority()
    {
        var lateAlphabeticallyFirst = AlgorithmTestData.PendingRequest(
            "a-late",
            latestPickup: 9_000);
        var urgentAlphabeticallyLast = AlgorithmTestData.PendingRequest(
            "z-urgent",
            latestPickup: 2_000);
        var state = AlgorithmTestData.CreateState(
            [lateAlphabeticallyFirst, urgentAlphabeticallyLast],
            [AlgorithmTestData.Vehicle()]);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(
                100,
                1));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.All(
            result.VehicleCandidates!.Single().Candidates
                .Where(candidate => !candidate.IsNoOp),
            candidate => Assert.Equal(
                [urgentAlphabeticallyLast.Id],
                candidate.NewRequestIds));
    }

    [Fact]
    public void Bounded_best_first_work_keeps_higher_acceptance_and_counts_unknown_paths()
    {
        var first = AlgorithmTestData.PendingRequest("request-1");
        var second = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree);
        var state = AlgorithmTestData.CreateState(
            [first, second],
            [AlgorithmTestData.Vehicle()]);
        var options = new CandidateGenerationOptions(
            100,
            2,
            maximumExplorationWorkUnits: 3);

        var firstRun = _generator.Generate(state, options);

        Assert.True(firstRun.IsSuccess, firstRun.Witness?.Message);
        var set = Assert.Single(firstRun.VehicleCandidates!);
        var accepted = Assert.Single(set.Candidates, candidate => !candidate.IsNoOp);
        Assert.Equal(2, accepted.NewRequestIds.Count);
        var loss = Assert.Single(firstRun.Diagnostics!.VehicleLosses);
        Assert.Equal(3, loss.ExplorationWorkUnits);
    }

    [Fact]
    public void Candidate_cap_prefers_acceptance_then_cost_and_reports_feasible_loss()
    {
        var first = AlgorithmTestData.PendingRequest("request-1");
        var second = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree);
        var state = AlgorithmTestData.CreateState(
            [first, second],
            [AlgorithmTestData.Vehicle()]);
        var full = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 2));
        var boundedOptions = new CandidateGenerationOptions(
            2,
            2);
        var bounded = _generator.Generate(state, boundedOptions);

        Assert.True(full.IsSuccess, full.Witness?.Message);
        Assert.True(bounded.IsSuccess, bounded.Witness?.Message);
        var fullTwoRequest = full.VehicleCandidates!.Single().Candidates
            .Where(candidate => candidate.NewRequestIds.Count == 2)
            .OrderBy(candidate => candidate.Schedule.OperationalCost)
            .ThenBy(candidate => candidate.CertifiedForwardSlackMilliseconds is null ? 0 : 1)
            .ThenByDescending(candidate => candidate.CertifiedForwardSlackMilliseconds ?? 0)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .First();
        var boundedSet = Assert.Single(bounded.VehicleCandidates!);
        var retained = Assert.Single(
            boundedSet.Candidates,
            candidate => !candidate.IsNoOp);
        Assert.Equal(2, retained.NewRequestIds.Count);
        Assert.Equal(fullTwoRequest.CandidateId, retained.CandidateId);
    }

    [Fact]
    public void More_search_work_never_loses_an_already_generated_acceptance_level()
    {
        var first = AlgorithmTestData.PendingRequest("request-1");
        var second = AlgorithmTestData.PendingRequest(
            "request-2",
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree);
        var state = AlgorithmTestData.CreateState(
            [second, first],
            [AlgorithmTestData.Vehicle()]);
        var previousMaximumAccepted = 0;

        for (var work = 1L; work <= 20; work++)
        {
            var result = _generator.Generate(
                state,
                new CandidateGenerationOptions(
                    100,
                    2,
                    maximumExplorationWorkUnits: work));

            Assert.True(result.IsSuccess, result.Witness?.Message);
            var maximumAccepted = result.VehicleCandidates!.Single().Candidates
                .Max(candidate => candidate.NewRequestIds.Count);
            Assert.True(
                maximumAccepted >= previousMaximumAccepted,
                $"work {work} reduced accepted level from " +
                $"{previousMaximumAccepted} to {maximumAccepted}.");
            previousMaximumAccepted = maximumAccepted;
        }

        Assert.Equal(2, previousMaximumAccepted);
    }
}
