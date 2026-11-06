using RideBound.Algorithms.Candidates;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests.Candidates;

public sealed class ForwardSlackAndScheduleStrategyTests
{
    [Fact]
    public void Backward_profile_combines_pickup_deadline_wait_absorption_and_ride_time()
    {
        var (state, vehicle, route) = WaitingRoute();

        var result = new ForwardSlackProfileBuilder().Build(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);

        Assert.True(result.IsSuccess, result.Message);
        var profile = result.Profile!;
        Assert.Equal(2, profile.Stops.Count);
        Assert.Equal(900, profile.Stops[0].WaitingBeforeServiceMilliseconds);
        Assert.Equal(1900, profile.Stops[0].LocalDeadlineSlackMilliseconds);
        Assert.Equal(900, profile.Stops[1].LocalDeadlineSlackMilliseconds);
        Assert.Equal(900, profile.Stops[1].CertifiedDelayBeforeArrivalMilliseconds);
        Assert.Equal(1800, profile.CertifiedDelayAtRouteStartMilliseconds);
        Assert.Equal(1100, profile.Schedule.OperationalCost);
    }

    [Fact]
    public void Origin_hold_is_an_executable_waypoint_with_exact_service_equivalence()
    {
        var request = WaitingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var state = AlgorithmTestData.CreateState(
            [request],
            [vehicle],
            arcs: UnitArcs());
        var generator = new InsertionCandidateGenerator();
        var earliest = generator.Generate(
            state,
            new CandidateGenerationOptions(
                100,
                1,
                exactSmallMode: true,
                CandidateScheduleStrategy.EarliestFeasible));
        var held = generator.Generate(
            state,
            new CandidateGenerationOptions(
                100,
                1,
                exactSmallMode: true,
                CandidateScheduleStrategy.OriginHoldRelocatedWait));

        Assert.True(earliest.IsSuccess, earliest.Witness?.Message);
        Assert.True(held.IsSuccess, held.Witness?.Message);
        var earliestInsertion = Assert.Single(
            earliest.VehicleCandidates!.Single().Candidates,
            candidate => !candidate.IsNoOp);
        var heldInsertion = Assert.Single(
            held.VehicleCandidates!.Single().Candidates,
            candidate => !candidate.IsNoOp);
        var hold = heldInsertion.Route.MutableSuffix[0];

        Assert.Equal(CandidateScheduleStrategy.EarliestFeasible,
            earliestInsertion.ScheduleStrategy);
        Assert.Equal(CandidateScheduleStrategy.OriginHoldRelocatedWait,
            heldInsertion.ScheduleStrategy);
        Assert.Equal(RouteStopKind.Waypoint, hold.Kind);
        Assert.Equal(AlgorithmTestData.NodeZero, hold.NodeId);
        Assert.Equal(900, hold.ServiceDuration.Milliseconds);
        Assert.Equal(900, heldInsertion.RelocatedWaitMilliseconds);
        Assert.Equal(
            [RouteStopKind.Waypoint, RouteStopKind.Pickup, RouteStopKind.DropOff],
            heldInsertion.Route.MutableSuffix.Select(stop => stop.Kind));
        Assert.Equal(
            earliestInsertion.Schedule.OperationalCost,
            heldInsertion.Schedule.OperationalCost);

        var heldOriginalStops = heldInsertion.Schedule.Stops.Skip(1).ToArray();
        Assert.Equal(
            earliestInsertion.Schedule.Stops.Select(stop => stop.StopId),
            heldOriginalStops.Select(stop => stop.StopId));
        Assert.Equal(
            earliestInsertion.Schedule.Stops.Select(stop => stop.ServiceStartTime),
            heldOriginalStops.Select(stop => stop.ServiceStartTime));
        Assert.Equal(
            earliestInsertion.Schedule.Stops.Select(stop => stop.DepartureTime),
            heldOriginalStops.Select(stop => stop.DepartureTime));

        var physical = new PhysicalPlanValidator().Validate(
            new PhysicalValidationContext(
                state.Run,
                vehicle.Id,
                heldInsertion.Route,
                state.TravelTimes!,
                state.Run.SimulationTime));
        Assert.True(physical.IsFeasible, physical.Witness?.Message);
        Assert.Single(
            held.VehicleCandidates!.Single().Candidates,
            candidate => candidate.IsNoOp);
    }

    [Fact]
    public void Origin_hold_never_operates_from_edge_progress()
    {
        var request = WaitingRequest();
        var baseVehicle = AlgorithmTestData.Vehicle();
        var edgeVehicle = VehicleState.Create(
            baseVehicle.Id,
            baseVehicle.Capacity,
            baseVehicle.OccupiedSeats,
            new EdgeProgressPosition(
                AlgorithmTestData.NodeZero,
                AlgorithmTestData.NodeOne,
                "edge-0-1",
                500),
            baseVehicle.OnboardRequestIds,
            baseVehicle.AcceptedRequestIds,
            baseVehicle.Route,
            baseVehicle.LastObservedEpoch).Value!;
        var state = AlgorithmTestData.CreateState(
            [request],
            [edgeVehicle],
            arcs: UnitArcs());

        var result = new InsertionCandidateGenerator().Generate(
            state,
            new CandidateGenerationOptions(
                100,
                1,
                exactSmallMode: true,
                CandidateScheduleStrategy.OriginHoldRelocatedWait));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var insertion = Assert.Single(
            result.VehicleCandidates!.Single().Candidates,
            candidate => !candidate.IsNoOp);
        Assert.Equal(
            CandidateScheduleStrategy.EarliestFeasible,
            insertion.ScheduleStrategy);
        Assert.DoesNotContain(
            insertion.Route.MutableSuffix,
            stop => stop.Kind == RouteStopKind.Waypoint);
    }

    [Fact]
    public void Every_delay_within_the_forward_certificate_remains_physically_feasible()
    {
        var (state, vehicle, route) = WaitingRoute();
        var profile = new ForwardSlackProfileBuilder().Build(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime).Profile!;

        Assert.Equal(1800, profile.CertifiedDelayAtRouteStartMilliseconds);

        foreach (var delay in new long[] { 0, 1, 900, 1_799, 1_800 })
        {
            var delayed = vehicle.Route.ReplaceMutableSuffix(
                new[]
                {
                    new RouteStop(
                        new StopId($"delay-{delay}"),
                        AlgorithmTestData.NodeZero,
                        RouteStopKind.Waypoint,
                        null,
                        new Duration(delay)),
                }.Concat(route.MutableSuffix)).Value!;
            var physical = new PhysicalPlanValidator().Validate(
                new PhysicalValidationContext(
                    state.Run,
                    vehicle.Id,
                    delayed,
                    state.TravelTimes!,
                    state.Run.SimulationTime));

            Assert.True(
                physical.IsFeasible,
                $"delay={delay}: {physical.Witness?.Message}");
        }
    }

    [Fact]
    public void Cache_hits_only_when_full_schedule_key_is_unchanged()
    {
        var (state, vehicle, route) = WaitingRoute();
        var builder = new CountingProfileBuilder();
        var cache = new ForwardSlackProfileCache(builder, maximumEntries: 100);

        var first = cache.GetOrBuild(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);
        var repeated = cache.GetOrBuild(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);
        var changedTime = cache.GetOrBuild(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            new SimTime(state.Run.SimulationTime.Milliseconds + 1));
        var changedRoute = route.ReplaceMutableSuffix(
            route.MutableSuffix.Append(
                new RouteStop(
                    new StopId("extra-waypoint"),
                    AlgorithmTestData.NodeThree,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)))).Value!;
        var routeMiss = cache.GetOrBuild(
            state,
            vehicle,
            changedRoute,
            state.TravelTimes!,
            state.Run.SimulationTime);
        var changedTravel = TravelTimeSnapshot.Create(
            2,
            new string('b', 64),
            UnitArcs(travel01: 101)).Value!;
        var travelMiss = cache.GetOrBuild(
            state,
            vehicle,
            route,
            changedTravel,
            state.Run.SimulationTime);
        var movedVehicle = VehicleState.Create(
            vehicle.Id,
            vehicle.Capacity,
            vehicle.OccupiedSeats,
            new NodePosition(AlgorithmTestData.NodeThree),
            vehicle.OnboardRequestIds,
            vehicle.AcceptedRequestIds,
            vehicle.Route,
            vehicle.LastObservedEpoch).Value!;
        var positionMiss = cache.GetOrBuild(
            state,
            movedVehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);
        var changedVehicle = VehicleState.Create(
            vehicle.Id,
            capacity: vehicle.Capacity + 1,
            vehicle.OccupiedSeats,
            vehicle.Position,
            vehicle.OnboardRequestIds,
            vehicle.AcceptedRequestIds,
            vehicle.Route,
            vehicle.LastObservedEpoch).Value!;
        var vehicleMiss = cache.GetOrBuild(
            state,
            changedVehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);
        var rebuiltState = AlgorithmTestData.CreateState(
            [WaitingRequest()],
            [AlgorithmTestData.Vehicle()],
            arcs: UnitArcs());
        var runMiss = cache.GetOrBuild(
            rebuiltState,
            rebuiltState.Run.Vehicles[vehicle.Id],
            route,
            rebuiltState.TravelTimes!,
            rebuiltState.Run.SimulationTime);

        Assert.False(first.WasCacheHit);
        Assert.True(repeated.WasCacheHit);
        Assert.False(changedTime.WasCacheHit);
        Assert.False(routeMiss.WasCacheHit);
        Assert.False(travelMiss.WasCacheHit);
        Assert.False(positionMiss.WasCacheHit);
        Assert.False(vehicleMiss.WasCacheHit);
        Assert.False(runMiss.WasCacheHit);
        Assert.Equal(7, builder.BuildCount);
        Assert.Equal(1, cache.HitCount);
        Assert.Equal(7, cache.MissCount);
    }

    [Fact]
    public void Travel_mutation_invalidates_cache_and_changes_profile_semantics()
    {
        var (state, vehicle, route) = WaitingRoute();
        var builder = new CountingProfileBuilder();
        var cache = new ForwardSlackProfileCache(builder);
        var original = cache.GetOrBuild(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);
        var slower = TravelTimeSnapshot.Create(
            2,
            new string('c', 64),
            UnitArcs(travel01: 200)).Value!;
        var mutated = cache.GetOrBuild(
            state,
            vehicle,
            route,
            slower,
            state.Run.SimulationTime);

        Assert.False(original.WasCacheHit);
        Assert.False(mutated.WasCacheHit);
        Assert.Equal(1100, original.Result.Profile!.Stops[0].ArrivalTime.Milliseconds);
        Assert.Equal(1200, mutated.Result.Profile!.Stops[0].ArrivalTime.Milliseconds);
        Assert.NotEqual(
            original.Result.Profile.CertifiedDelayAtRouteStartMilliseconds,
            mutated.Result.Profile.CertifiedDelayAtRouteStartMilliseconds);
    }

    [Fact]
    public void Cached_and_uncached_profiles_are_semantically_identical()
    {
        var (state, vehicle, route) = WaitingRoute();
        var cache = new ForwardSlackProfileCache();
        var uncached = new ForwardSlackProfileBuilder().Build(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);
        cache.GetOrBuild(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);
        var cached = cache.GetOrBuild(
            state,
            vehicle,
            route,
            state.TravelTimes!,
            state.Run.SimulationTime);

        Assert.True(uncached.IsSuccess, uncached.Message);
        Assert.True(cached.WasCacheHit);
        Assert.Equal(
            uncached.Profile!.Schedule.OperationalCost,
            cached.Result.Profile!.Schedule.OperationalCost);
        Assert.Equal(uncached.Profile.Stops, cached.Result.Profile.Stops);
        Assert.Equal(
            uncached.Profile.CertifiedDelayAtRouteStartMilliseconds,
            cached.Result.Profile.CertifiedDelayAtRouteStartMilliseconds);
    }

    [Fact]
    public void Profile_cache_cannot_bypass_physical_validator()
    {
        var request = WaitingRequest(partySize: 5);
        var vehicle = AlgorithmTestData.Vehicle(capacity: 4);
        var state = AlgorithmTestData.CreateState(
            [request],
            [vehicle],
            arcs: UnitArcs());
        var builder = new CountingProfileBuilder();
        var generator = new InsertionCandidateGenerator(
            slackCache: new ForwardSlackProfileCache(builder));

        var result = generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var set = Assert.Single(result.VehicleCandidates!);
        Assert.Single(set.Candidates, candidate => candidate.IsNoOp);
        Assert.Contains(set.PrunedCandidates, prune => prune.Code == "CAPACITY");
        Assert.True(builder.BuildCount > 1);
    }

    [Fact]
    public void Repeated_generation_reuses_structural_route_profiles()
    {
        var request = WaitingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var state = AlgorithmTestData.CreateState(
            [request],
            [vehicle],
            arcs: UnitArcs());
        var builder = new CountingProfileBuilder();
        var cache = new ForwardSlackProfileCache(builder);
        var generator = new InsertionCandidateGenerator(slackCache: cache);
        var options = new CandidateGenerationOptions(100, 1, exactSmallMode: true);

        var first = generator.Generate(state, options);
        var buildsAfterFirst = builder.BuildCount;
        var hitsBefore = cache.HitCount;
        var second = generator.Generate(state, options);

        Assert.True(first.IsSuccess, first.Witness?.Message);
        Assert.True(second.IsSuccess, second.Witness?.Message);
        Assert.True(buildsAfterFirst > 0);
        Assert.Equal(buildsAfterFirst, builder.BuildCount);
        Assert.True(cache.HitCount > hitsBefore);
        Assert.Equal(
            first.VehicleCandidates!.Single().Candidates.Select(c => c.CandidateId),
            second.VehicleCandidates!.Single().Candidates.Select(c => c.CandidateId));
    }

    [Fact]
    public void Terminal_evaluation_reuses_a_failed_profile_from_the_same_search_node()
    {
        var request = WaitingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var state = AlgorithmTestData.CreateState(
            [request],
            [vehicle],
            arcs: UnitArcs());
        var builder = new FailingNonEmptyProfileBuilder();
        var generator = new InsertionCandidateGenerator(
            slackCache: new ForwardSlackProfileCache(builder));

        var result = generator.Generate(
            state,
            new CandidateGenerationOptions(100, 1, exactSmallMode: true));

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var candidates = Assert.Single(result.VehicleCandidates!);
        Assert.Single(candidates.Candidates, candidate => candidate.IsNoOp);
        Assert.Contains(
            candidates.PrunedCandidates,
            witness => witness.Code == "TEST_PROFILE_FAILURE");
        Assert.Equal(1, builder.NonEmptyBuildCount);
    }

    private static (OnlineState State, VehicleState Vehicle, RoutePlan Route)
        WaitingRoute()
    {
        var request = WaitingRequest();
        var vehicle = AlgorithmTestData.Vehicle();
        var state = AlgorithmTestData.CreateState(
            [request],
            [vehicle],
            arcs: UnitArcs());
        var route = vehicle.Route.ReplaceMutableSuffix(
            [
                new RouteStop(
                    new StopId("pickup-r1"),
                    request.OriginNodeId,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop-r1"),
                    request.DestinationNodeId,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        return (state, vehicle, route);
    }

    private static RideRequest WaitingRequest(long partySize = 1) =>
        RideRequest.CreatePending(
            new RequestId("request-wait"),
            new SimTime(1_000),
            AlgorithmTestData.NodeOne,
            AlgorithmTestData.NodeTwo,
            new SimTime(2_000),
            new SimTime(3_000),
            new Duration(1_000),
            partySize,
            "standard",
            "uniform-v1").Value!;

    private static IReadOnlyList<KeyValuePair<TravelArc, Duration>> UnitArcs(
        long travel01 = 100)
    {
        var arcs = AlgorithmTestData.CompleteArcs().ToDictionary(pair => pair.Key);
        arcs[new TravelArc(AlgorithmTestData.NodeZero, AlgorithmTestData.NodeOne)] =
            new KeyValuePair<TravelArc, Duration>(
                new TravelArc(AlgorithmTestData.NodeZero, AlgorithmTestData.NodeOne),
                new Duration(travel01));
        arcs[new TravelArc(AlgorithmTestData.NodeOne, AlgorithmTestData.NodeTwo)] =
            new KeyValuePair<TravelArc, Duration>(
                new TravelArc(AlgorithmTestData.NodeOne, AlgorithmTestData.NodeTwo),
                new Duration(100));
        return arcs.Values.ToArray();
    }

    private sealed class CountingProfileBuilder : IForwardSlackProfileBuilder
    {
        private readonly ForwardSlackProfileBuilder _inner = new();

        public int BuildCount { get; private set; }

        public ForwardSlackProfileBuildResult Build(
            OnlineState state,
            VehicleState vehicle,
            RoutePlan route,
            TravelTimeSnapshot travelTimes,
            SimTime evaluationTime,
            ServiceQualityAllowance? serviceQuality = null)
        {
            BuildCount++;
            return _inner.Build(
                state,
                vehicle,
                route,
                travelTimes,
                evaluationTime,
                serviceQuality);
        }
    }

    private sealed class FailingNonEmptyProfileBuilder : IForwardSlackProfileBuilder
    {
        private readonly ForwardSlackProfileBuilder _inner = new();

        public int NonEmptyBuildCount { get; private set; }

        public ForwardSlackProfileBuildResult Build(
            OnlineState state,
            VehicleState vehicle,
            RoutePlan route,
            TravelTimeSnapshot travelTimes,
            SimTime evaluationTime,
            ServiceQualityAllowance? serviceQuality = null)
        {
            if (route.RemainingStops.Any())
            {
                NonEmptyBuildCount++;
                return ForwardSlackProfileBuildResult.Failure(
                    "TEST_PROFILE_FAILURE",
                    "Synthetic failed profile used to prove node-local reuse.");
            }

            return _inner.Build(
                state,
                vehicle,
                route,
                travelTimes,
                evaluationTime);
        }
    }
}
