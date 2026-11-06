using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Candidates;

public sealed class WaitingIncumbentRepairTests
{
    [Fact]
    public void Repair_seeds_move_one_complete_pair_without_mutating_input()
    {
        var fixture = CreateTwoIncumbents();
        var originalStops = fixture.Vehicle.Route.MutableSuffix.ToArray();

        var result = new WaitingIncumbentRepairSeedBuilder().Build(
            fixture.State,
            fixture.Vehicle,
            maximumRequestsConsidered: 2);

        Assert.Equal([fixture.First.Id, fixture.Second.Id], result.EligibleRequestIds);
        Assert.Equal(result.EligibleRequestIds, result.ConsideredRequestIds);
        Assert.Empty(result.OmittedRequestIds);
        Assert.NotEmpty(result.Seeds);
        Assert.Equal(originalStops, fixture.Vehicle.Route.MutableSuffix);
        Assert.All(
            result.Seeds,
            seed =>
            {
                Assert.Equal(fixture.Vehicle.Route.FrozenPrefix, seed.Route.FrozenPrefix);
                Assert.Equal(
                    fixture.Vehicle.Route.ExecutedStopCount,
                    seed.Route.ExecutedStopCount);
                Assert.Equal(
                    fixture.Vehicle.Route.Version.Value + 1,
                    seed.Route.Version.Value);
                AssertPairIsAtomic(seed.Route, seed.RequestId);
                Assert.Single(
                    new[] { fixture.First.Id, fixture.Second.Id },
                    requestId => requestId == seed.RequestId);
            });
        Assert.Contains(
            result.Seeds,
            seed => Order(seed.Route, fixture.Second.Id)
                < Order(seed.Route, fixture.First.Id));
    }

    [Fact]
    public void Unexecuted_frozen_pair_is_never_a_repair_eligible_incumbent()
    {
        var fixture = CreateTwoIncumbents();
        var firstPair = fixture.Vehicle.Route.MutableSuffix
            .Where(stop => stop.RequestId == fixture.First.Id)
            .ToArray();
        var secondPair = fixture.Vehicle.Route.MutableSuffix
            .Where(stop => stop.RequestId == fixture.Second.Id)
            .ToArray();
        var frozenRoute = RoutePlan.Create(
            fixture.Vehicle.Route.Version,
            executedStopCount: 0,
            firstPair,
            secondPair).Value!;
        var frozenVehicle = VehicleState.Create(
            fixture.Vehicle.Id,
            fixture.Vehicle.Capacity,
            fixture.Vehicle.OccupiedSeats,
            fixture.Vehicle.Position,
            fixture.Vehicle.OnboardRequestIds,
            fixture.Vehicle.AcceptedRequestIds,
            frozenRoute,
            fixture.Vehicle.LastObservedEpoch).Value!;

        var result = new WaitingIncumbentRepairSeedBuilder().Build(
            fixture.State,
            frozenVehicle,
            maximumRequestsConsidered: 2);

        Assert.DoesNotContain(fixture.First.Id, result.EligibleRequestIds);
        Assert.Contains(fixture.Second.Id, result.EligibleRequestIds);
        Assert.All(
            result.Seeds,
            seed => Assert.Equal(fixture.Second.Id, seed.RequestId));
    }

    [Fact]
    public void Onboard_incumbent_is_never_repair_eligible()
    {
        var fixture = CreateTwoIncumbents();
        var run = fixture.State.Run.ConfirmWaitingPickup(fixture.First.Id).Value!;
        run = run.Board(
            fixture.Vehicle.Id,
            fixture.First.Id,
            fixture.Vehicle.Route.Version,
            new SimTime(1_000)).Value!;
        var state = fixture.State with { Run = run };
        var vehicle = state.Run.Vehicles[fixture.Vehicle.Id];

        var result = new WaitingIncumbentRepairSeedBuilder().Build(
            state,
            vehicle,
            maximumRequestsConsidered: 2);

        Assert.DoesNotContain(fixture.First.Id, result.EligibleRequestIds);
        Assert.Contains(fixture.Second.Id, result.EligibleRequestIds);
        Assert.All(
            result.Seeds,
            seed => Assert.Equal(fixture.Second.Id, seed.RequestId));
    }

    [Fact]
    public void Exact_B4_fails_if_explicit_repair_request_cap_omits_an_incumbent()
    {
        var fixture = CreateTwoIncumbents();

        var result = new InsertionCandidateGenerator().Generate(
            fixture.State,
            new CandidateGenerationOptions(
                1_000,
                0,
                exactSmallMode: true,
                maximumRepairRequestsConsideredPerVehicle: 1));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CandidateGenerationFailureCodes
                .ExactSmallRepairRequestBoundExceeded,
            result.Witness?.Code);
        Assert.Equal(fixture.Second.Id, result.Witness?.RequestId);
    }

    [Fact]
    public void Bounded_B4_reports_omitted_repair_request_separately()
    {
        var fixture = CreateTwoIncumbents();
        var options = new CandidateGenerationOptions(
            1_000,
            0,
            exactSmallMode: false,
            maximumRepairRequestsConsideredPerVehicle: 1);

        var first = new InsertionCandidateGenerator().Generate(
            fixture.State,
            options);
        var second = new InsertionCandidateGenerator().Generate(
            fixture.State,
            options);

        Assert.True(first.IsSuccess, first.Witness?.Message);
        var loss = Assert.Single(first.Diagnostics!.VehicleLosses);
        Assert.Equal(2, loss.EligibleRepairRequestCount);
        Assert.Equal(1, loss.ConsideredRepairRequestCount);
        Assert.Equal(1, loss.OmittedRepairRequestCount);
        var omission = Assert.Single(
            first.Diagnostics.Omissions,
            witness => witness.Code
                == CandidateGenerationFailureCodes.RepairRequestBoundOmission);
        Assert.Equal([fixture.Second.Id], omission.RequestIds);
        Assert.Equal(
            omission.StableDigest,
            Assert.Single(
                second.Diagnostics!.Omissions,
                witness => witness.Code
                    == CandidateGenerationFailureCodes.RepairRequestBoundOmission)
                .StableDigest);
        Assert.True(Assert.Single(first.VehicleCandidates!).WasTruncated);
        Assert.False(first.Diagnostics.IsComplete);
    }

    [Fact]
    public void B1_disabled_neighborhood_is_unchanged_and_B4_adds_real_repair_routes()
    {
        var fixture = CreateTwoIncumbents();
        var generator = new InsertionCandidateGenerator();
        var b1 = generator.Generate(
            fixture.State,
            new CandidateGenerationOptions(1_000, 0, exactSmallMode: true));
        var b4 = generator.Generate(
            fixture.State,
            new CandidateGenerationOptions(
                1_000,
                0,
                exactSmallMode: true,
                maximumRepairRequestsConsideredPerVehicle: 2));

        Assert.True(b1.IsSuccess, b1.Witness?.Message);
        Assert.True(b4.IsSuccess, b4.Witness?.Message);
        Assert.Single(b1.VehicleCandidates!.Single().Candidates);
        var repaired = b4.VehicleCandidates!.Single().Candidates
            .Where(candidate => candidate.RepairedIncumbentRequestId is not null)
            .ToArray();
        Assert.True(repaired.Length > 1);
        Assert.All(repaired, candidate => Assert.False(candidate.IsNoOp));
        Assert.All(
            repaired,
            candidate =>
            {
                Assert.Empty(candidate.NewRequestIds);
                AssertPairIsAtomic(
                    candidate.Route,
                    candidate.RepairedIncumbentRequestId!.Value);
            });
    }

    [Fact]
    public void B4_policy_selects_cheaper_repair_without_reassignment()
    {
        var fixture = CreateTwoIncumbents();
        var noRepair = new InsertionCandidateGenerator().Generate(
            fixture.State,
            new CandidateGenerationOptions(1_000, 0, exactSmallMode: true));
        var noOpCost = Assert.Single(
            noRepair.VehicleCandidates!.Single().Candidates).Schedule.OperationalCost;
        var repairGenerated = new InsertionCandidateGenerator().Generate(
            fixture.State,
            new CandidateGenerationOptions(
                1_000,
                0,
                exactSmallMode: true,
                maximumRepairRequestsConsideredPerVehicle: 2));
        Assert.True(repairGenerated.IsSuccess, repairGenerated.Witness?.Message);
        var repairCandidates = repairGenerated.VehicleCandidates!.Single().Candidates;
        Assert.Contains(
            repairCandidates,
            candidate => candidate.RepairedIncumbentRequestId is not null
                && candidate.Schedule.OperationalCost < noOpCost);

        var decision = new NoReassignmentRepairPolicy(
            maximumRepairRequestsConsideredPerVehicle: 2).Decide(
                fixture.State,
                new CandidateGenerationOptions(
                    1_000,
                    0,
                    exactSmallMode: true));

        Assert.True(decision.IsSuccess, decision.Witness?.Message);
        var selected = Assert.Single(decision.Decision!.VehiclePlans).Candidate;
        Assert.True(
            selected.RepairedIncumbentRequestId is not null,
            $"selected={selected.CandidateId}:{selected.Schedule.OperationalCost}; " +
            string.Join(
                ",",
                repairCandidates.Select(
                    candidate => $"{candidate.CandidateId}:" +
                        $"{candidate.Schedule.OperationalCost}:" +
                        $"{candidate.RepairedIncumbentRequestId?.Value ?? "none"}")));
        Assert.False(selected.IsNoOp);
        Assert.True(selected.Schedule.OperationalCost < noOpCost);
        Assert.Empty(decision.Decision.RequestActions);
        Assert.Equal(
            fixture.Vehicle.Id,
            decision.Decision.ProposedState.Run.Requests[fixture.First.Id]
                .AssignedVehicleId);
        Assert.Equal(
            fixture.Vehicle.Id,
            decision.Decision.ProposedState.Run.Requests[fixture.Second.Id]
                .AssignedVehicleId);
    }

    [Fact]
    public void Bounded_B4_root_priority_projects_every_repair_seed_before_work_is_spent()
    {
        var fixture = CreateTwoIncumbents();
        var seeds = new WaitingIncumbentRepairSeedBuilder().Build(
            fixture.State,
            fixture.Vehicle,
            maximumRequestsConsidered: 2);
        Assert.True(seeds.Seeds.Count > 1);

        var slackBuilder = new RecordingSlackBuilder();
        var generator = new InsertionCandidateGenerator(
            slackCache: new ForwardSlackProfileCache(slackBuilder));
        var result = generator.Generate(
            fixture.State,
            new CandidateGenerationOptions(
                maximumCandidatesPerVehicle: 1_000,
                maximumNewRequestsPerVehicle: 0,
                exactSmallMode: false,
                maximumExplorationWorkUnits: 1,
                maximumRepairRequestsConsideredPerVehicle: 2));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.True(Assert.Single(result.Diagnostics!.VehicleLosses)
            .WorkBudgetExhausted);
        Assert.True(
            seeds.Seeds.All(
                seed => slackBuilder.RouteFingerprints.Contains(RouteKey(seed.Route))),
            "Each repair root must be ranked using the slack profile of its " +
            "own route before a tight work cap can choose which root to expand.");
    }

    private static Fixture CreateTwoIncumbents()
    {
        var first = AlgorithmTestData.PendingRequest(
            "incumbent-a",
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            latestPickup: 10_000,
            maxRideTime: 10_000);
        var second = AlgorithmTestData.PendingRequest(
            "incumbent-b",
            AlgorithmTestData.NodeThree,
            AlgorithmTestData.NodeOne,
            latestPickup: 10_000,
            maxRideTime: 10_000);
        var routeStops = new[]
        {
            Stop(first, RouteStopKind.Pickup),
            Stop(first, RouteStopKind.DropOff),
            Stop(second, RouteStopKind.Pickup),
            Stop(second, RouteStopKind.DropOff),
        };
        var initialVehicle = AlgorithmTestData.Vehicle(mutableSuffix: routeStops);
        var initial = AlgorithmTestData.CreateState(
            [first, second],
            [initialVehicle]);
        var run = initial.Run.AcceptRequest(first.Id, initialVehicle.Id).Value!;
        run = run.AcceptRequest(second.Id, initialVehicle.Id).Value!;
        var state = initial with { Run = run };
        return new Fixture(
            state,
            state.Run.Vehicles[initialVehicle.Id],
            first,
            second);
    }

    private static RouteStop Stop(
        RideRequest request,
        RouteStopKind kind) =>
        new(
            new StopId($"{request.Id.Value}-{kind}"),
            kind == RouteStopKind.Pickup
                ? request.OriginNodeId
                : request.DestinationNodeId,
            kind,
            request.Id,
            new Duration(0));

    private static void AssertPairIsAtomic(
        RoutePlan route,
        RequestId requestId)
    {
        var indexed = route.MutableSuffix
            .Select((stop, index) => (stop, index))
            .Where(pair => pair.stop.RequestId == requestId)
            .ToArray();
        Assert.Equal(2, indexed.Length);
        Assert.Equal(RouteStopKind.Pickup, indexed[0].stop.Kind);
        Assert.Equal(RouteStopKind.DropOff, indexed[1].stop.Kind);
        Assert.True(indexed[0].index < indexed[1].index);
    }

    private static int Order(RoutePlan route, RequestId requestId) =>
        route.MutableSuffix
            .Select((stop, index) => (stop, index))
            .First(pair => pair.stop.RequestId == requestId).index;

    private sealed class RecordingSlackBuilder : IForwardSlackProfileBuilder
    {
        public ISet<string> RouteFingerprints { get; } =
            new HashSet<string>(StringComparer.Ordinal);

        public ForwardSlackProfileBuildResult Build(
            OnlineState state,
            VehicleState vehicle,
            RoutePlan route,
            TravelTimeSnapshot travelTimes,
            SimTime evaluationTime,
            ServiceQualityAllowance? serviceQuality = null)
        {
            RouteFingerprints.Add(RouteKey(route));
            var stops = route.RemainingStops
                .Select(
                    (stop, index) => new ForwardSlackStop(
                        stop.StopId,
                        new SimTime(index),
                        new SimTime(index),
                        new SimTime(index),
                        WaitingBeforeServiceMilliseconds: 0,
                        LocalDeadlineSlackMilliseconds: 10_000,
                        CertifiedDelayBeforeArrivalMilliseconds: 10_000))
                .ToArray();
            var schedule = new CandidateSchedule(
                stops.Select(
                    stop => new ScheduledStop(
                        stop.StopId,
                        stop.ArrivalTime,
                        stop.ServiceStartTime,
                        stop.DepartureTime)).ToArray(),
                OperationalCost: 0);
            return ForwardSlackProfileBuildResult.Success(
                new ForwardSlackProfile(schedule, stops, 10_000));
        }
    }

    private static string RouteKey(RoutePlan route) => string.Join(
        "|",
        route.RemainingStops.Select(
            stop => $"{stop.StopId.Value}:{stop.RequestId?.Value ?? "none"}"));

    private sealed record Fixture(
        OnlineState State,
        VehicleState Vehicle,
        RideRequest First,
        RideRequest Second);
}
