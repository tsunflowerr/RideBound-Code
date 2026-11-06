using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Algorithms.Tests.Candidates;

public sealed class CandidatePortfolioRetainerTests
{
    private static readonly VehicleId VehicleOne = new("vehicle-1");
    private static readonly VehicleId VehicleTwo = new("vehicle-2");
    private static readonly RequestId RequestA = new("request-a");
    private static readonly RequestId RequestB = new("request-b");
    private static readonly RequestId RequestC = new("request-c");
    private static readonly RequestId RequestD = new("request-d");
    private readonly CandidatePortfolioRetainer _retainer = new();

    [Fact]
    public void Portfolio_preserves_a_no_more_expensive_B1_anchor_for_every_legacy_set()
    {
        var candidates = new[]
        {
            NoOp(VehicleOne),
            Candidate("ab-cheap", VehicleOne, [RequestA, RequestB], 4),
            Candidate("ab-middle", VehicleOne, [RequestA, RequestB], 8),
            Candidate("ab-expensive", VehicleOne, [RequestA, RequestB], 12),
            Candidate("cd-cheap", VehicleOne, [RequestC, RequestD], 10),
            Candidate("cd-expensive", VehicleOne, [RequestC, RequestD], 14),
            Candidate("a-only", VehicleOne, [RequestA], 1),
        };

        for (var cap = 1; cap <= candidates.Length; cap++)
        {
            var legacy = _retainer.Retain(
                candidates,
                cap,
                CandidateRetentionStrategy.LegacyAcceptedCountCostSlack);
            var portfolio = _retainer.Retain(
                candidates.Reverse().ToArray(),
                cap,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1);

            foreach (var legacyCandidate in legacy.Retained.Where(
                         candidate => !candidate.IsNoOp))
            {
                var anchor = Assert.Single(
                    portfolio.Retained.Where(
                        candidate => SameServiceSet(
                            candidate,
                            legacyCandidate))
                        .OrderBy(candidate => candidate.Schedule.OperationalCost)
                        .Take(1));
                Assert.True(
                    anchor.Schedule.OperationalCost
                        <= legacyCandidate.Schedule.OperationalCost,
                    $"cap={cap}, legacy={legacyCandidate.CandidateId}, " +
                    $"anchor={anchor.CandidateId}");
            }

            Assert.Equal(cap, portfolio.Retained.Count);
            Assert.Equal(
                candidates.Length,
                portfolio.Retained.Count + portfolio.Omitted.Count);
        }
    }

    [Fact]
    public void Service_set_coverage_strictly_improves_an_adversarial_fleet_assignment()
    {
        var vehicleOne = new[]
        {
            NoOp(VehicleOne),
            Candidate("v1-ab-1", VehicleOne, [RequestA, RequestB], 4),
            Candidate("v1-ab-2", VehicleOne, [RequestA, RequestB], 8),
            Candidate("v1-cd-1", VehicleOne, [RequestC, RequestD], 10),
            Candidate("v1-cd-2", VehicleOne, [RequestC, RequestD], 14),
        };
        var vehicleTwo = new[]
        {
            NoOp(VehicleTwo),
            Candidate("v2-ab-1", VehicleTwo, [RequestA, RequestB], 3),
            Candidate("v2-bc-1", VehicleTwo, [RequestB, RequestC], 6),
            Candidate("v2-ab-2", VehicleTwo, [RequestA, RequestB], 7),
            Candidate("v2-bc-2", VehicleTwo, [RequestB, RequestC], 9),
        };
        var legacy = SelectFleet(vehicleOne, vehicleTwo, 3,
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack);
        var portfolio = SelectFleet(vehicleOne, vehicleTwo, 3,
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1);

        Assert.Equal(2, legacy.AcceptedRequestCount);
        Assert.Equal(4, portfolio.AcceptedRequestCount);
        Assert.Equal(13, portfolio.OperationalCost);
        Assert.Contains(
            portfolio.VehiclePlans,
            plan => plan.Candidate.CandidateId == "v1-cd-1");
        Assert.Contains(
            portfolio.VehiclePlans,
            plan => plan.Candidate.CandidateId == "v2-ab-1");
    }

    [Fact]
    public void Stability_anchor_preserves_incumbent_prefix_instead_of_a_duplicate_variant()
    {
        var incumbentPickup = Stop(
            "incumbent-pickup",
            "node-1",
            RouteStopKind.Pickup,
            new RequestId("incumbent"));
        var incumbentDrop = Stop(
            "incumbent-drop",
            "node-2",
            RouteStopKind.DropOff,
            new RequestId("incumbent"));
        var newPickup = Stop(
            "new-pickup",
            "node-3",
            RouteStopKind.Pickup,
            RequestA);
        var newDrop = Stop(
            "new-drop",
            "node-4",
            RouteStopKind.DropOff,
            RequestA);
        var noOp = Candidate(
            "noop",
            VehicleOne,
            [],
            0,
            [incumbentPickup, incumbentDrop],
            [1_000, 2_000],
            isNoOp: true);
        var cheapest = Candidate(
            "new-before-all",
            VehicleOne,
            [RequestA],
            10,
            [newPickup, newDrop, incumbentPickup, incumbentDrop],
            [1_000, 1_100, 2_000, 3_000]);
        var secondCheapest = Candidate(
            "new-between-incumbent",
            VehicleOne,
            [RequestA],
            11,
            [newPickup, incumbentPickup, newDrop, incumbentDrop],
            [1_000, 1_800, 1_900, 2_800]);
        var stable = Candidate(
            "incumbent-prefix-anchor",
            VehicleOne,
            [RequestA],
            20,
            [incumbentPickup, incumbentDrop, newPickup, newDrop],
            [1_000, 2_000, 2_100, 2_200]);
        var candidates = new[] { noOp, cheapest, secondCheapest, stable };

        var legacy = _retainer.Retain(
            candidates,
            3,
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack);
        var portfolio = _retainer.Retain(
            candidates,
            3,
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1);

        Assert.Equal(
            ["new-before-all", "new-between-incumbent", "noop"],
            legacy.Retained.Select(candidate => candidate.CandidateId));
        Assert.Equal(
            ["new-before-all", "incumbent-prefix-anchor", "noop"],
            portfolio.Retained.Select(candidate => candidate.CandidateId));
    }

    [Fact]
    public void Portfolio_is_permutation_deterministic_and_conserves_exact_omission_digest_input()
    {
        var candidates = new[]
        {
            NoOp(VehicleOne),
            Candidate("ab-2", VehicleOne, [RequestA, RequestB], 8),
            Candidate("cd-1", VehicleOne, [RequestC, RequestD], 10),
            Candidate("ab-1", VehicleOne, [RequestA, RequestB], 4),
            Candidate("cd-2", VehicleOne, [RequestC, RequestD], 14),
        };
        var expected = _retainer.Retain(
            candidates,
            3,
            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1);

        foreach (var permutation in new[]
                 {
                     candidates.Reverse().ToArray(),
                     candidates.Skip(2).Concat(candidates.Take(2)).ToArray(),
                 })
        {
            var actual = _retainer.Retain(
                permutation,
                3,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1);
            Assert.Equal(
                expected.Retained.Select(candidate => candidate.CandidateId),
                actual.Retained.Select(candidate => candidate.CandidateId));
            Assert.Equal(
                expected.Omitted.Select(candidate => candidate.CandidateId),
                actual.Omitted.Select(candidate => candidate.CandidateId));
        }

        Assert.Equal(3, expected.Retained.Count);
        Assert.Equal(2, expected.Omitted.Count);
    }

    [Fact]
    public void Portfolio_is_globally_no_worse_than_legacy_for_seeded_B1_fleets()
    {
        var requestPool = new[] { RequestA, RequestB, RequestC, RequestD };

        for (var seed = 0; seed < 128; seed++)
        {
            var random = new Random(seed);
            var sets = new List<IReadOnlyCollection<InsertionCandidate>>();

            for (var vehicleIndex = 0; vehicleIndex < 4; vehicleIndex++)
            {
                var vehicle = new VehicleId($"seed-{seed}-vehicle-{vehicleIndex}");
                var candidates = new List<InsertionCandidate> { NoOp(vehicle) };
                var serviceSets = Enumerable.Range(1, (1 << requestPool.Length) - 1)
                    .OrderBy(_ => random.Next())
                    .Take(random.Next(3, 8))
                    .ToArray();

                foreach (var mask in serviceSets)
                {
                    var requests = requestPool
                        .Where((_, index) => (mask & (1 << index)) != 0)
                        .ToArray();
                    var variants = random.Next(1, 5);

                    for (var variant = 0; variant < variants; variant++)
                    {
                        candidates.Add(
                            Candidate(
                                $"s{seed}-v{vehicleIndex}-m{mask}-r{variant}",
                                vehicle,
                                requests,
                                random.Next(1, 1000)));
                    }
                }

                sets.Add(candidates);
            }

            var cap = random.Next(2, 8);
            var legacySets = sets.Select(
                    set =>
                    {
                        var retained = _retainer.Retain(
                            set,
                            Math.Min(cap, set.Count),
                            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack);
                        return new VehicleCandidateSet(
                            retained.Retained[0].VehicleId,
                            retained.Retained,
                            [],
                            retained.Omitted.Count > 0);
                    })
                .ToArray();
            var portfolioSets = sets.Select(
                    set =>
                    {
                        var retained = _retainer.Retain(
                            set.Reverse().ToArray(),
                            Math.Min(cap, set.Count),
                            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1);
                        return new VehicleCandidateSet(
                            retained.Retained[0].VehicleId,
                            retained.Retained,
                            [],
                            retained.Omitted.Count > 0);
                    })
                .ToArray();
            var legacy = new CandidateFleetSelector().Select(legacySets);
            var portfolio = new CandidateFleetSelector().Select(portfolioSets);

            Assert.True(legacy.IsSuccess, $"seed={seed}; {legacy.Witness?.Message}");
            Assert.True(portfolio.IsSuccess, $"seed={seed}; {portfolio.Witness?.Message}");
            Assert.True(
                portfolio.Selection!.AcceptedRequestCount
                    > legacy.Selection!.AcceptedRequestCount
                || portfolio.Selection.AcceptedRequestCount
                    == legacy.Selection.AcceptedRequestCount
                && portfolio.Selection.OperationalCost
                    <= legacy.Selection.OperationalCost,
                $"seed={seed}; cap={cap}; legacy=" +
                $"({legacy.Selection.AcceptedRequestCount}," +
                $"{legacy.Selection.OperationalCost}); portfolio=" +
                $"({portfolio.Selection.AcceptedRequestCount}," +
                $"{portfolio.Selection.OperationalCost})");
        }
    }

    [Fact]
    public void Cost_anchor_dominance_holds_for_exhaustive_small_variant_counts_and_caps()
    {
        var serviceSets = new[]
        {
            new[] { RequestA },
            new[] { RequestB },
            new[] { RequestA, RequestB },
        };

        for (var firstCount = 1; firstCount <= 3; firstCount++)
        {
            for (var secondCount = 1; secondCount <= 3; secondCount++)
            {
                for (var thirdCount = 1; thirdCount <= 3; thirdCount++)
                {
                    var counts = new[] { firstCount, secondCount, thirdCount };
                    var candidates = new List<InsertionCandidate> { NoOp(VehicleOne) };

                    for (var setIndex = 0; setIndex < serviceSets.Length; setIndex++)
                    {
                        for (var variant = 0; variant < counts[setIndex]; variant++)
                        {
                            candidates.Add(
                                Candidate(
                                    $"set-{setIndex}-variant-{variant}",
                                    VehicleOne,
                                    serviceSets[setIndex],
                                    cost: (variant + 1) * 10 + setIndex));
                        }
                    }

                    for (var cap = 1; cap <= candidates.Count; cap++)
                    {
                        var legacy = _retainer.Retain(
                            candidates,
                            cap,
                            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack);
                        var portfolio = _retainer.Retain(
                            candidates.AsEnumerable().Reverse().ToArray(),
                            cap,
                            CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1);

                        foreach (var legacyCandidate in legacy.Retained.Where(
                                     candidate => !candidate.IsNoOp))
                        {
                            Assert.Contains(
                                portfolio.Retained,
                                candidate => SameServiceSet(candidate, legacyCandidate)
                                    && candidate.Schedule.OperationalCost
                                        <= legacyCandidate.Schedule.OperationalCost);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Retainer_rejects_ambiguous_or_cross_vehicle_portfolios()
    {
        var noOp = NoOp(VehicleOne);
        var candidate = Candidate("duplicate", VehicleOne, [RequestA], 1);
        var duplicate = candidate with { Schedule = new CandidateSchedule([], 2) };

        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [candidate],
                1,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [noOp, candidate, duplicate],
                2,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [noOp, Candidate("v2", VehicleTwo, [RequestA], 1)],
                2,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [noOp with { NewRequestIds = [RequestA] }],
                1,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [noOp, candidate with { NewRequestIds = [RequestA, RequestA] }],
                2,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [noOp, candidate with { NewRequestIds = [RequestB] }],
                2,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
        var hiddenRequestStops = new[]
        {
            Stop("hidden-a-pickup", "hidden-a-origin", RouteStopKind.Pickup, RequestA),
            Stop("hidden-a-drop", "hidden-a-destination", RouteStopKind.DropOff, RequestA),
            Stop("hidden-c-pickup", "hidden-c-origin", RouteStopKind.Pickup, RequestC),
            Stop("hidden-c-drop", "hidden-c-destination", RouteStopKind.DropOff, RequestC),
        };
        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [noOp, Candidate("hidden-request", VehicleOne, [RequestA], 1, hiddenRequestStops)],
                1,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
        Assert.Throws<ArgumentException>(
            () => _retainer.Retain(
                [
                    noOp,
                    candidate with
                    {
                        Schedule = new CandidateSchedule([], 1),
                    },
                ],
                2,
                CandidateRetentionStrategy.ServiceSetStabilityPortfolioV1));
    }

    [Fact]
    public void Candidate_generation_options_require_explicit_portfolio_opt_in()
    {
        var options = new CandidateGenerationOptions(10, 2, exactSmallMode: false);

        Assert.Equal(
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack,
            options.RetentionStrategy);
    }

    [Fact]
    public void Legacy_cap_preserves_historical_fast_path_without_portfolio_shape_proof()
    {
        var noOp = NoOp(VehicleOne);
        var malformedOnlyForThePortfolioProof = Candidate(
            "legacy-shape-not-read",
            VehicleOne,
            [RequestA],
            1) with
        {
            Schedule = new CandidateSchedule([], 1),
        };

        var retained = _retainer.Retain(
            [noOp, malformedOnlyForThePortfolioProof],
            1,
            CandidateRetentionStrategy.LegacyAcceptedCountCostSlack);

        Assert.Equal([noOp.CandidateId], retained.Retained.Select(candidate => candidate.CandidateId));
        Assert.Equal(
            [malformedOnlyForThePortfolioProof.CandidateId],
            retained.Omitted.Select(candidate => candidate.CandidateId));
    }

    private FleetSelection SelectFleet(
        IReadOnlyCollection<InsertionCandidate> vehicleOne,
        IReadOnlyCollection<InsertionCandidate> vehicleTwo,
        int cap,
        CandidateRetentionStrategy strategy)
    {
        var first = _retainer.Retain(vehicleOne, cap, strategy);
        var second = _retainer.Retain(vehicleTwo, cap, strategy);
        var selected = new CandidateFleetSelector().Select(
        [
            new VehicleCandidateSet(VehicleOne, first.Retained, [], true),
            new VehicleCandidateSet(VehicleTwo, second.Retained, [], true),
        ]);
        Assert.True(selected.IsSuccess, selected.Witness?.Message);
        return selected.Selection!;
    }

    private static bool SameServiceSet(
        InsertionCandidate left,
        InsertionCandidate right) => left.NewRequestIds
        .OrderBy(requestId => requestId.Value, StringComparer.Ordinal)
        .SequenceEqual(
            right.NewRequestIds.OrderBy(
                requestId => requestId.Value,
                StringComparer.Ordinal));

    private static InsertionCandidate NoOp(VehicleId vehicleId) =>
        Candidate($"{vehicleId.Value}-noop", vehicleId, [], 0, isNoOp: true);

    private static InsertionCandidate Candidate(
        string id,
        VehicleId vehicleId,
        IReadOnlyList<RequestId> requests,
        long cost,
        IReadOnlyList<RouteStop>? stops = null,
        IReadOnlyList<long>? serviceTimes = null,
        bool isNoOp = false)
    {
        var routeStops = stops ?? requests.SelectMany(
                request => new[]
                {
                    Stop(
                        $"{id}-{request.Value}-pickup",
                        $"{id}-{request.Value}-origin",
                        RouteStopKind.Pickup,
                        request),
                    Stop(
                        $"{id}-{request.Value}-drop",
                        $"{id}-{request.Value}-destination",
                        RouteStopKind.DropOff,
                        request),
                })
            .ToArray();
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            routeStops).Value!;
        var times = serviceTimes
            ?? Enumerable.Repeat(0L, routeStops.Count).ToArray();
        var schedule = routeStops
            .Select(
                (stop, index) => new ScheduledStop(
                    stop.StopId,
                    new SimTime(times[index]),
                    new SimTime(times[index]),
                    new SimTime(times[index])))
            .ToArray();
        return new InsertionCandidate(
            id,
            vehicleId,
            route,
            requests,
            new CandidateSchedule(schedule, cost),
            isNoOp);
    }

    private static RouteStop Stop(
        string stopId,
        string nodeId,
        RouteStopKind kind,
        RequestId requestId) => new(
        new StopId(stopId),
        new NodeId(nodeId),
        kind,
        requestId,
        new Duration(0));
}
