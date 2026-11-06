using RideBound.Algorithms.Candidates;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Candidates;

/// <summary>
/// Locks the exact amount of work bounded generation performs on a loaded
/// vehicle. The identity, cache and allocation optimizations in the generator
/// are only legitimate while every one of these counts is unchanged: the search
/// must dequeue the same nodes, project the same distinct routes and retain the
/// same candidates. Timing is deliberately not asserted — only work.
/// </summary>
public sealed class CandidateSearchWorkProfileTests
{
    [Theory]
    [InlineData(2, 468L, 450L, 451L, 0L, 452L)]
    [InlineData(4, 3_108L, 3_060L, 3_061L, 0L, 3_062L)]
    [InlineData(6, 10_000L, 9_908L, 9_909L, 1_194L, 11_013L)]
    [InlineData(8, 10_000L, 9_848L, 9_849L, 19_528L, 28_845L)]
    public void Bounded_generation_performs_an_exact_amount_of_work(
        int incumbentCount,
        long expectedWorkUnits,
        long expectedEvaluatedPaths,
        long expectedFeasibleBeforeCap,
        long expectedOmittedPaths,
        long expectedDistinctSlackProfiles)
    {
        var state = CreateLoadedState(incumbentCount);
        var cache = new ForwardSlackProfileCache(
            new ForwardSlackProfileBuilder(),
            maximumEntries: 1_000_000);
        var generator = new InsertionCandidateGenerator(slackCache: cache);

        var result = generator.Generate(state, LoadedOptions);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var loss = result.Diagnostics!.VehicleLosses.Single();
        Assert.Equal(expectedWorkUnits, loss.ExplorationWorkUnits);
        Assert.Equal(expectedEvaluatedPaths, loss.EvaluatedCandidatePathCount);
        Assert.Equal(
            expectedFeasibleBeforeCap,
            loss.UniqueFeasibleCandidateCountBeforeCap);
        Assert.Equal(
            expectedOmittedPaths,
            loss.OmittedUnexpandedCandidatePathCount);
        Assert.Equal(0L, loss.PhysicallyOrSchedulePrunedCount);
        Assert.Equal(100L, loss.RetainedCandidateCount);

        // One slack profile per distinct projected route. A regression that
        // reintroduced a duplicate projection or weakened the memo key would
        // move this number even though the decision stayed the same.
        Assert.Equal(expectedDistinctSlackProfiles, cache.MissCount);
    }

    [Fact]
    public void Repeated_generation_is_byte_stable_and_adds_no_new_projection()
    {
        var state = CreateLoadedState(4);
        var cache = new ForwardSlackProfileCache(
            new ForwardSlackProfileBuilder(),
            maximumEntries: 1_000_000);
        var generator = new InsertionCandidateGenerator(slackCache: cache);

        var first = generator.Generate(state, LoadedOptions);
        var missesAfterFirst = cache.MissCount;
        var second = generator.Generate(state, LoadedOptions);

        Assert.True(first.IsSuccess, first.Witness?.Message);
        Assert.True(second.IsSuccess, second.Witness?.Message);
        Assert.Equal(missesAfterFirst, cache.MissCount);
        Assert.Equal(
            first.VehicleCandidates!.Single().Candidates
                .Select(candidate => candidate.CandidateId),
            second.VehicleCandidates!.Single().Candidates
                .Select(candidate => candidate.CandidateId));
        Assert.Equal(
            first.Diagnostics!.Omissions.Select(value => value.StableDigest),
            second.Diagnostics!.Omissions.Select(value => value.StableDigest));
    }

    private static CandidateGenerationOptions LoadedOptions { get; } =
        new(
            maximumCandidatesPerVehicle: 100,
            maximumNewRequestsPerVehicle: 2,
            exactSmallMode: false,
            scheduleStrategy: CandidateScheduleStrategy.EarliestFeasible,
            maximumExplorationWorkUnits: 10_000);

    private static OnlineState CreateLoadedState(int incumbentCount)
    {
        var nodes = Enumerable.Range(0, 2 * incumbentCount + 6)
            .Select(index => new NodeId($"n{index:D3}"))
            .ToArray();
        var arcs = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < nodes.Length; from++)
        {
            for (var to = 0; to < nodes.Length; to++)
            {
                if (from != to)
                {
                    arcs.Add(
                        new KeyValuePair<TravelArc, Duration>(
                            new TravelArc(nodes[from], nodes[to]),
                            new Duration(60 + Math.Abs(to - from) * 7)));
                }
            }
        }

        var requests = new List<RideRequest>();
        var suffix = new List<RouteStop>();

        for (var index = 0; index < incumbentCount; index++)
        {
            var request = RideRequest.CreatePending(
                new RequestId($"inc-{index:D2}"),
                new SimTime(0),
                nodes[2 * index + 1],
                nodes[2 * index + 2],
                new SimTime(0),
                new SimTime(900_000),
                new Duration(900_000),
                1,
                "standard",
                "uniform-v1").Value!;
            requests.Add(request);
            suffix.Add(
                new RouteStop(
                    new StopId($"inc-{index:D2}-p"),
                    request.OriginNodeId,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)));
            suffix.Add(
                new RouteStop(
                    new StopId($"inc-{index:D2}-d"),
                    request.DestinationNodeId,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)));
        }

        for (var index = 0; index < 2; index++)
        {
            requests.Add(
                RideRequest.CreatePending(
                    new RequestId($"new-{index:D2}"),
                    new SimTime(0),
                    nodes[^(2 * index + 2)],
                    nodes[^(2 * index + 1)],
                    new SimTime(0),
                    new SimTime(900_000),
                    new Duration(900_000),
                    1,
                    "standard",
                    "uniform-v1").Value!);
        }

        var route = RoutePlan.Create(new PlanVersion(0), 0, [], suffix).Value!;
        var vehicle = VehicleState.Create(
            new VehicleId("vehicle-1"),
            incumbentCount + 4,
            0,
            new NodePosition(nodes[0]),
            [],
            [],
            route,
            1).Value!;
        var run = RideBoundRun.Create(
            new RunIdentifier("work-profile"),
            new ScenarioIdentifier("work-profile"),
            new SimTime(0));

        foreach (var request in requests)
        {
            run = run.AddRequest(request).Value!;
        }

        run = run.BootstrapVehicle(vehicle).Value!;

        foreach (var request in requests.Take(incumbentCount))
        {
            run = run.AcceptRequest(request.Id, vehicle.Id).Value!;
        }

        var travel = TravelTimeSnapshot.Create(1, new string('a', 64), arcs).Value!;
        return new OnlineState(
            run,
            travel,
            1,
            travel.SnapshotHash,
            RideBound.Domain.Commitments.CommitmentLedger.Empty);
    }
}
