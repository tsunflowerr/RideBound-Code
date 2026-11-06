using RideBound.Algorithms.Candidates;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Candidates;

/// <summary>
/// ADR-045. Worsening travel can push an already committed route past a
/// service-quality deadline. Before ADR-045 that pruned the safety no-op, left
/// the vehicle with zero candidates and killed the run — which is exactly how
/// the WP9 smoke failed, in both arms. These lock the replacement semantics:
/// the no-op survives, the breach is reported, and no candidate is allowed to
/// hide its own damage behind it.
/// </summary>
public sealed class ExogenousServiceQualityTests
{
    private static readonly RequestId Incumbent = new("request-1");
    private static readonly RequestId Newcomer = new("request-2");

    private readonly InsertionCandidateGenerator _generator = new();

    [Fact]
    public void Traffic_that_breaches_a_committed_ride_time_keeps_the_safety_no_op()
    {
        var state = CommittedState();

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!).Candidates;
        Assert.Single(candidates, value => value.IsNoOp);
    }

    [Fact]
    public void The_breach_is_reported_with_its_contractual_and_exogenous_values()
    {
        var state = CommittedState();

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var breach = Assert.Single(
            result.Diagnostics!.ExogenousServiceQualityBreaches);
        Assert.Equal(AlgorithmTestData.VehicleOne, breach.VehicleId);
        Assert.Equal(Incumbent, breach.RequestId);
        Assert.Equal(PhysicalViolationCodes.MaxRideTime, breach.Code);
        Assert.Equal(150, breach.ContractualMilliseconds);
        Assert.Equal(5_000, breach.ExogenousMilliseconds);
    }

    [Fact]
    public void A_route_that_meets_every_deadline_reports_no_breach()
    {
        var state = CommittedState(maxRideTime: 10_000);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Empty(result.Diagnostics!.ExogenousServiceQualityBreaches);
    }

    [Fact]
    public void Every_changed_candidate_is_judged_against_the_contractual_bound()
    {
        var state = CommittedState(withPendingRequest: true);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var vehicle = Assert.Single(result.VehicleCandidates!);

        // ADR-047: relief reaches the safety no-op and nothing else. A changed
        // candidate carrying the breached incumbent is pruned against the
        // published 150 ms bound, not the 5,000 ms the traffic already caused —
        // so RideBound never proposes a plan the simulator would reject.
        var pruned = vehicle.PrunedCandidates
            .Where(value => value.Code == PhysicalViolationCodes.MaxRideTime)
            .ToArray();
        Assert.NotEmpty(pruned);
        Assert.All(
            pruned,
            value =>
            {
                Assert.Equal(Incumbent, value.PhysicalWitness?.RequestId);
                Assert.Equal(150, value.PhysicalWitness?.Expected);
            });

        // The no-op survives regardless; that is the whole point of the relief.
        Assert.Single(vehicle.Candidates, value => value.IsNoOp);
    }

    [Fact]
    public void The_breached_incumbent_leaves_the_vehicle_with_only_the_no_op()
    {
        var state = CommittedState(withPendingRequest: true);

        var result = _generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var vehicle = Assert.Single(result.VehicleCandidates!);

        // Once the active route is in breach, no changed plan can honour the
        // contract for the incumbent, so the vehicle rides out its current
        // route. That is a real service cost and it must not be hidden: the run
        // continues, the pending request simply is not inserted here.
        Assert.All(vehicle.Candidates, value => Assert.True(value.IsNoOp));
    }

    /// <summary>
    /// One vehicle carrying one accepted, not-yet-picked-up request, under a
    /// travel snapshot whose <c>node-1 -&gt; node-2</c> leg has become far
    /// slower than the ride time the request was accepted under.
    /// </summary>
    private static OnlineState CommittedState(
        long maxRideTime = 150,
        bool withPendingRequest = false)
    {
        var incumbent = AlgorithmTestData.PendingRequest(
            Incumbent.Value,
            maxRideTime: maxRideTime);
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("p-1"),
                    AlgorithmTestData.NodeOne,
                    RouteStopKind.Pickup,
                    Incumbent,
                    new Duration(0)),
                new RouteStop(
                    new StopId("d-1"),
                    AlgorithmTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    Incumbent,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            AlgorithmTestData.VehicleOne,
            4,
            0,
            new NodePosition(AlgorithmTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;

        var run = RideBoundRun.Create(
            AlgorithmTestData.RunId,
            AlgorithmTestData.ScenarioId,
            new SimTime(1_000));
        run = run.AddRequest(incumbent).Value!;

        if (withPendingRequest)
        {
            run = run.AddRequest(
                AlgorithmTestData.PendingRequest(
                    Newcomer.Value,
                    origin: AlgorithmTestData.NodeThree,
                    destination: AlgorithmTestData.NodeTwo)).Value!;
        }

        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(Incumbent, AlgorithmTestData.VehicleOne).Value!;
        run = run.ConfirmWaitingPickup(Incumbent).Value!;

        var travel = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            SlowedArcs()).Value!;

        return new OnlineState(
            run,
            travel,
            1,
            travel.SnapshotHash,
            CommitmentLedger.Empty);
    }

    private static IReadOnlyList<KeyValuePair<TravelArc, Duration>> SlowedArcs()
    {
        var arcs = AlgorithmTestData.CompleteArcs()
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        arcs[new TravelArc(AlgorithmTestData.NodeOne, AlgorithmTestData.NodeTwo)] =
            new Duration(5_000);
        arcs[new TravelArc(AlgorithmTestData.NodeOne, AlgorithmTestData.NodeThree)] =
            new Duration(4_000);
        arcs[new TravelArc(AlgorithmTestData.NodeThree, AlgorithmTestData.NodeTwo)] =
            new Duration(4_000);
        return arcs.ToArray();
    }
}
