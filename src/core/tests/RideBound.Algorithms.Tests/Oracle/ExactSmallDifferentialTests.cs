using RideBound.Algorithms.Candidates;
using RideBound.Algorithms.Policies;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Oracle;

public sealed class ExactSmallDifferentialTests
{
    private static readonly CandidateGenerationOptions ExactOptions =
        CandidateGenerationOptions.ExactSmall;

    [Theory]
    [MemberData(nameof(PublishedSeeds))]
    public void Production_generator_and_selector_match_independent_oracle(
        int seed)
    {
        var state = CreateGeneratedState(seed);
        var generator = new InsertionCandidateGenerator();
        var generated = generator.Generate(state, ExactOptions);
        Assert.True(
            generated.IsSuccess,
            $"seed={seed}; generator={generated.Witness}");
        var oracle = ExactSmallOracle.Generate(state);
        var productionIds =
            new Dictionary<(VehicleId, string), string>();

        foreach (var set in generated.VehicleCandidates!)
        {
            var production = set.Candidates.ToDictionary(
                SemanticKey,
                value => value,
                StringComparer.Ordinal);
            var expected = oracle[set.VehicleId].ToDictionary(
                value => value.SemanticKey,
                value => value,
                StringComparer.Ordinal);

            Assert.True(
                production.Keys.Order(StringComparer.Ordinal).SequenceEqual(
                    expected.Keys.Order(StringComparer.Ordinal)),
                $"seed={seed}; generator gap; vehicle={set.VehicleId}; " +
                $"production=[{string.Join(",", production.Keys.Order())}]; " +
                $"oracle=[{string.Join(",", expected.Keys.Order())}]");

            foreach (var pair in expected)
            {
                Assert.Equal(
                    pair.Value.OperationalCost,
                    production[pair.Key].Schedule.OperationalCost);
                productionIds.Add(
                    (set.VehicleId, pair.Key),
                    production[pair.Key].CandidateId);
            }
        }

        var oracleSelection = ExactSmallOracle.Select(oracle, productionIds);
        var productionDecision =
            new RollingCostPolicy().Decide(state, ExactOptions);

        Assert.True(
            productionDecision.IsSuccess,
            $"seed={seed}; selector={productionDecision.Witness}");
        Assert.Equal(
            oracleSelection.AcceptedRequestCount,
            productionDecision.Decision!.AcceptedRequestCount);
        Assert.Equal(
            oracleSelection.OperationalCost,
            productionDecision.Decision.OperationalCost);
        Assert.Equal(
            oracleSelection.Candidates.Select(value => value.SemanticKey),
            productionDecision.Decision.VehiclePlans
                .OrderBy(
                    value => value.VehicleId.Value,
                    StringComparer.Ordinal)
                .Select(value => SemanticKey(value.Candidate)));
        Assert.DoesNotContain(
            productionDecision.Decision.ProposedState.Run.Requests.Values,
            value => value.Lifecycle == RequestLifecycle.Rejected
                && state.Run.Requests[value.Id].IsAcceptedActive);
    }

    public static IEnumerable<object[]> PublishedSeeds() =>
        Enumerable.Range(0, 64).Select(value => new object[] { value });

    private static OnlineState CreateGeneratedState(int seed)
    {
        var random = new Random(seed);
        var time = new SimTime(1_000);
        var run = RideBoundRun.Create(
            new RunIdentifier($"oracle-run-{seed}"),
            new ScenarioIdentifier("oracle-published-bound-v1"),
            time);
        var vehicles = new List<VehicleState>();
        var vehicleCount = 1 + seed % 2;

        for (var index = 0; index < vehicleCount; index++)
        {
            var vehicleId = new VehicleId($"vehicle-{index}");
            var capacity = 2 + random.Next(0, 2);
            var includeOnboard = (seed + index) % 5 == 0;
            var acceptedIds = new List<RequestId>();
            var onboardIds = new List<RequestId>();
            var existingStops = new List<RouteStop>();
            var occupiedSeats = 0L;

            if (includeOnboard)
            {
                var incumbent = AlgorithmTestData.PendingRequest(
                    $"incumbent-{index}",
                    AlgorithmTestData.NodeZero,
                    AlgorithmTestData.NodeTwo,
                    latestPickup: 1_000,
                    maxRideTime: 2_000);
                var accepted = incumbent.Accept(vehicleId).Value!;
                var waiting = accepted.ConfirmWaitingPickup().Value!;
                var onboard = waiting.Board(vehicleId, new SimTime(1_000)).Value!;
                run = run.AddRequest(onboard).Value!;
                acceptedIds.Add(onboard.Id);
                onboardIds.Add(onboard.Id);
                occupiedSeats = onboard.PartySize;
                existingStops.Add(
                    new RouteStop(
                        new StopId($"incumbent-drop-{index}"),
                        onboard.DestinationNodeId,
                        RouteStopKind.DropOff,
                        onboard.Id,
                        new Duration(0)));
            }
            else if ((seed + index) % 3 == 0)
            {
                existingStops.Add(
                    new RouteStop(
                        new StopId($"waypoint-{index}"),
                        AlgorithmTestData.NodeThree,
                        RouteStopKind.Waypoint,
                        null,
                        new Duration(random.Next(0, 2) * 10)));
            }

            var route = RoutePlan.Create(
                new PlanVersion(includeOnboard ? 1 : 0),
                0,
                [],
                existingStops).Value!;
            var vehicle = VehicleState.Create(
                vehicleId,
                capacity,
                occupiedSeats,
                new NodePosition(AlgorithmTestData.NodeZero),
                onboardIds,
                acceptedIds,
                route,
                1).Value!;
            vehicles.Add(vehicle);
        }

        var requestCount = 1 + seed / 2 % 2;

        for (var index = 0; index < requestCount; index++)
        {
            var origin = index == 0
                ? AlgorithmTestData.NodeOne
                : AlgorithmTestData.NodeThree;
            var destination = index == 0
                ? AlgorithmTestData.NodeTwo
                : AlgorithmTestData.NodeOne;
            var latest = 1_100 + random.Next(0, 5) * 70;
            var request = AlgorithmTestData.PendingRequest(
                $"request-{index}",
                origin,
                destination,
                latest,
                maxRideTime: 250 + random.Next(0, 5) * 100,
                partySize: 1 + random.Next(0, 2));
            run = run.AddRequest(request).Value!;
        }

        foreach (var vehicle in vehicles)
        {
            run = run.BootstrapVehicle(vehicle).Value!;
        }

        var travel = TravelTimeSnapshot.Create(
            1,
            new string('b', 64),
            CompleteArcs(seed)).Value!;
        return new OnlineState(
            run,
            travel,
            1,
            travel.SnapshotHash,
            RideBound.Domain.Commitments.CommitmentLedger.Empty);
    }

    private static IReadOnlyList<KeyValuePair<TravelArc, Duration>> CompleteArcs(
        int seed)
    {
        var nodes = new[]
        {
            AlgorithmTestData.NodeZero,
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            AlgorithmTestData.NodeThree,
        };
        var arcs = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < nodes.Length; from++)
        {
            for (var to = 0; to < nodes.Length; to++)
            {
                if (from == to)
                {
                    continue;
                }

                var time = 60 + ((from + 1) * 17 + (to + 1) * 31 + seed) % 80;
                arcs.Add(
                    new KeyValuePair<TravelArc, Duration>(
                        new TravelArc(nodes[from], nodes[to]),
                        new Duration(time)));
            }
        }

        return arcs;
    }

    private static string SemanticKey(InsertionCandidate candidate) =>
        string.Join(
            "|",
            candidate.Route.MutableSuffix.Select(
                stop => stop.RequestId is RequestId requestId
                    && candidate.NewRequestIds.Contains(requestId)
                    ? $"{(stop.Kind == RouteStopKind.Pickup ? "P" : "D")}:" +
                        requestId.Value
                    : $"E:{stop.StopId.Value}"));
}
