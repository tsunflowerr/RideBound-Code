using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Domain.Tests.Validation;

public sealed class PhysicalPlanValidatorTests
{
    [Fact]
    public void Valid_no_op_reconstructs_schedule_and_passes()
    {
        var state = WaitingRun(TestData.PendingRequest());
        var vehicle = state.Vehicles[TestData.VehicleOne];

        var result = Validate(state, vehicle.Route, CompleteTravel());

        Assert.True(result.IsFeasible, result.Witness?.Message);
        Assert.Null(result.Witness);
    }

    [Fact]
    public void Capacity_mutation_returns_exact_request_stop_and_dimension()
    {
        var state = WaitingRun(TestData.PendingRequest(partySize: 5));
        var vehicle = state.Vehicles[TestData.VehicleOne];

        var result = Validate(state, vehicle.Route, CompleteTravel());

        AssertViolation(
            result,
            PhysicalViolationCodes.Capacity,
            "r-1",
            "pickup",
            "capacity");
        Assert.Equal(4, result.Witness?.Expected);
        Assert.Equal(5, result.Witness?.Actual);
    }

    [Fact]
    public void Pickup_window_mutation_is_detected_from_derived_arrival()
    {
        var request = TestData.PendingRequest(
            earliestPickupMs: 1000,
            latestPickupMs: 1050);
        var state = WaitingRun(request);

        var result = Validate(
            state,
            state.Vehicles[TestData.VehicleOne].Route,
            CompleteTravel());

        AssertViolation(
            result,
            PhysicalViolationCodes.PickupWindow,
            "r-1",
            "pickup",
            "latestPickupMs");
        Assert.Equal(1100, result.Witness?.Actual);
    }

    [Fact]
    public void Maximum_ride_time_mutation_is_detected_from_derived_schedule()
    {
        var request = TestData.PendingRequest(maxRideTimeMs: 50);
        var state = WaitingRun(request);

        var result = Validate(
            state,
            state.Vehicles[TestData.VehicleOne].Route,
            CompleteTravel());

        AssertViolation(
            result,
            PhysicalViolationCodes.MaxRideTime,
            "r-1",
            "drop",
            "maxRideTimeMs");
        Assert.Equal(100, result.Witness?.Actual);
    }

    [Fact]
    public void Drop_before_pickup_returns_precedence_witness()
    {
        var state = TestData.RunWithPendingAndVehicle();
        var candidate = state.Vehicles[TestData.VehicleOne]
            .Route
            .ReplaceMutableSuffix([TestData.DropOff(), TestData.Pickup()])
            .Value!;

        var result = Validate(state, candidate, CompleteTravel());

        AssertViolation(
            result,
            PhysicalViolationCodes.Precedence,
            "r-1",
            "drop",
            "precedence");
    }

    [Fact]
    public void Frozen_prefix_mutation_is_detected_by_ordered_identity()
    {
        var current = TestData.Route(
            frozen: [TestData.Waypoint("locked", TestData.NodeOne)]);
        var state = TestData.RunWithPendingAndVehicle(current);
        var candidate = TestData.Route(
            frozen: [TestData.Waypoint("changed", TestData.NodeOne)],
            version: 1);

        var result = Validate(state, candidate, CompleteTravel());

        Assert.False(result.IsFeasible);
        Assert.Equal(PhysicalViolationCodes.FrozenPrefix, result.Witness?.Code);
        Assert.Equal("frozenPrefix", result.Witness?.Dimension);
    }

    [Fact]
    public void Removing_onboard_drop_is_rejected()
    {
        var waiting = WaitingRun(TestData.PendingRequest());
        var reached = waiting.ReachStop(
            TestData.VehicleOne,
            new StopId("pickup"),
            new PlanVersion(0),
            new NodePosition(TestData.NodeOne),
            observedEpoch: 0).Value!;
        var onboard = reached.Board(
            TestData.VehicleOne,
            TestData.RequestOne,
            new PlanVersion(0),
            new SimTime(1100)).Value!;
        var candidate = onboard.Vehicles[TestData.VehicleOne]
            .Route
            .ReplaceMutableSuffix([])
            .Value!;

        var result = Validate(
            onboard,
            candidate,
            CompleteTravel(),
            evaluationTimeMs: 1100);

        Assert.False(result.IsFeasible);
        Assert.Equal(
            PhysicalViolationCodes.OnboardPreservation,
            result.Witness?.Code);
        Assert.Equal(TestData.RequestOne, result.Witness?.RequestId);
    }

    [Fact]
    public void Removing_accepted_incumbent_stops_is_rejected()
    {
        var state = WaitingRun(TestData.PendingRequest());
        var candidate = state.Vehicles[TestData.VehicleOne]
            .Route
            .ReplaceMutableSuffix([])
            .Value!;

        var result = Validate(state, candidate, CompleteTravel());

        Assert.False(result.IsFeasible);
        Assert.Equal(
            PhysicalViolationCodes.AcceptedPreservation,
            result.Witness?.Code);
        Assert.Equal(TestData.RequestOne, result.Witness?.RequestId);
    }

    [Fact]
    public void Accepted_incumbent_cannot_appear_on_another_vehicle_route()
    {
        var route = TestData.Route(
            mutable: [TestData.Pickup(), TestData.DropOff()]);
        var state = TestData.RunWithPendingAndVehicle(route);
        state = state.BootstrapVehicle(
            TestData.EmptyVehicle(id: TestData.VehicleTwo)).Value!;
        state = state.AcceptRequest(
            TestData.RequestOne,
            TestData.VehicleOne).Value!;
        state = state.ConfirmWaitingPickup(TestData.RequestOne).Value!;
        var candidate = state.Vehicles[TestData.VehicleTwo]
            .Route
            .ReplaceMutableSuffix([TestData.Pickup(), TestData.DropOff()])
            .Value!;

        var result = new PhysicalPlanValidator().Validate(
            new PhysicalValidationContext(
                state,
                TestData.VehicleTwo,
                candidate,
                CompleteTravel(),
                new SimTime(1000)));

        AssertViolation(
            result,
            PhysicalViolationCodes.Reassignment,
            "r-1",
            "pickup",
            "vehicleId");
    }

    [Fact]
    public void Missing_directed_arc_is_connectivity_failure()
    {
        var state = WaitingRun(TestData.PendingRequest());
        var incomplete = new DictionaryTravelLookup(
            (TestData.NodeZero, TestData.NodeOne, 100));

        var result = Validate(
            state,
            state.Vehicles[TestData.VehicleOne].Route,
            incomplete);

        Assert.False(result.IsFeasible);
        Assert.Equal(
            PhysicalViolationCodes.RouteConnectivity,
            result.Witness?.Code);
        Assert.Equal(new StopId("drop"), result.Witness?.StopId);
    }

    [Fact]
    public void Changed_route_with_stale_plan_version_is_rejected_first()
    {
        var state = TestData.RunWithPendingAndVehicle();
        var stale = TestData.Route(
            mutable: [TestData.Pickup(), TestData.DropOff()],
            version: 0);

        var result = Validate(state, stale, CompleteTravel());

        Assert.False(result.IsFeasible);
        Assert.Equal(PhysicalViolationCodes.PlanVersion, result.Witness?.Code);
        Assert.Equal(1, result.Witness?.Expected);
        Assert.Equal(0, result.Witness?.Actual);
    }

    [Fact]
    public void Pickup_node_must_match_request_origin()
    {
        var state = TestData.RunWithPendingAndVehicle();
        var wrongPickup = new RouteStop(
            new StopId("wrong-pickup"),
            TestData.NodeTwo,
            RouteStopKind.Pickup,
            TestData.RequestOne,
            new Duration(0));
        var candidate = state.Vehicles[TestData.VehicleOne]
            .Route
            .ReplaceMutableSuffix([wrongPickup, TestData.DropOff()])
            .Value!;

        var result = Validate(state, candidate, CompleteTravel());

        AssertViolation(
            result,
            PhysicalViolationCodes.StopLocation,
            "r-1",
            "wrong-pickup",
            "originNodeId");
    }

    [Fact]
    public void Occupied_seats_must_equal_derived_onboard_load()
    {
        var run = TestData.EmptyRun().AddRequest(TestData.PendingRequest()).Value!;
        var inconsistent = VehicleState.Create(
            TestData.VehicleOne,
            4,
            1,
            new NodePosition(TestData.NodeZero),
            [],
            [],
            TestData.Route(),
            0).Value!;
        run = run.BootstrapVehicle(inconsistent).Value!;

        var result = Validate(run, inconsistent.Route, CompleteTravel());

        Assert.False(result.IsFeasible);
        Assert.Equal(PhysicalViolationCodes.Capacity, result.Witness?.Code);
        Assert.Equal("occupiedSeats", result.Witness?.Dimension);
        Assert.Equal(0, result.Witness?.Expected);
        Assert.Equal(1, result.Witness?.Actual);
    }

    [Fact]
    public void Every_small_stop_permutation_is_feasible_exactly_when_precedence_holds()
    {
        var secondId = new RequestId("r-2");
        var first = TestData.PendingRequest(
            latestPickupMs: 10_000,
            maxRideTimeMs: 10_000);
        var second = RideRequest.CreatePending(
            secondId,
            new SimTime(1000),
            TestData.NodeOne,
            TestData.NodeTwo,
            new SimTime(1000),
            new SimTime(10_000),
            new Duration(10_000),
            1,
            "standard",
            "uniform-v1").Value!;
        var run = TestData.EmptyRun().AddRequest(first).Value!;
        run = run.AddRequest(second).Value!;
        run = run.BootstrapVehicle(TestData.EmptyVehicle()).Value!;
        var firstPickup = TestData.Pickup(first.Id, "p-1");
        var firstDrop = TestData.DropOff(first.Id, "d-1");
        var secondPickup = TestData.Pickup(second.Id, "p-2");
        var secondDrop = TestData.DropOff(second.Id, "d-2");
        var stops = new[]
        {
            firstPickup,
            firstDrop,
            secondPickup,
            secondDrop,
        };

        foreach (var permutation in Permutations(stops))
        {
            var candidate = run.Vehicles[TestData.VehicleOne]
                .Route
                .ReplaceMutableSuffix(permutation)
                .Value!;
            var result = Validate(run, candidate, CompleteTravel());
            var expected = Array.IndexOf(permutation, firstPickup)
                    < Array.IndexOf(permutation, firstDrop)
                && Array.IndexOf(permutation, secondPickup)
                    < Array.IndexOf(permutation, secondDrop);

            Assert.True(
                expected == result.IsFeasible,
                $"Permutation {string.Join(",", permutation.Select(value => value.StopId.Value))}; " +
                $"expected={expected}; witness={result.Witness}");

            if (!expected)
            {
                Assert.Equal(
                    PhysicalViolationCodes.Precedence,
                    result.Witness?.Code);
            }
        }
    }

    [Fact]
    public void Edge_progress_adds_remaining_edge_time_deterministically()
    {
        var route = TestData.Route(
            mutable: [TestData.Pickup(), TestData.DropOff()]);
        var request = TestData.PendingRequest(latestPickupMs: 1060);
        var run = TestData.EmptyRun().AddRequest(request).Value!;
        var edgeVehicle = VehicleState.Create(
            TestData.VehicleOne,
            4,
            0,
            new EdgeProgressPosition(
                TestData.NodeZero,
                TestData.NodeOne,
                "edge-0-1",
                500),
            [],
            [],
            route,
            0).Value!;
        run = run.BootstrapVehicle(edgeVehicle).Value!;
        run = run.AcceptRequest(TestData.RequestOne, TestData.VehicleOne).Value!;
        run = run.ConfirmWaitingPickup(TestData.RequestOne).Value!;

        var result = Validate(run, route, CompleteTravel());

        Assert.True(result.IsFeasible, result.Witness?.Message);
    }

    [Fact]
    public void Edge_progress_near_canonical_bound_returns_a_stable_witness()
    {
        var vehicle = VehicleState.Create(
            TestData.VehicleOne,
            4,
            0,
            new EdgeProgressPosition(
                TestData.NodeZero,
                TestData.NodeOne,
                "edge-overflow",
                1),
            [],
            [],
            TestData.Route(),
            0).Value!;
        var run = TestData.EmptyRun().BootstrapVehicle(vehicle).Value!;

        var result = Validate(
            run,
            vehicle.Route,
            new DictionaryTravelLookup(
                (
                    TestData.NodeZero,
                    TestData.NodeOne,
                    DomainLimits.MaxCanonicalInteger)),
            evaluationTimeMs: DomainLimits.MaxCanonicalInteger);

        Assert.False(result.IsFeasible);
        Assert.Equal(
            PhysicalViolationCodes.ScheduleOverflow,
            result.Witness?.Code);
        Assert.Equal("simTimeMs", result.Witness?.Dimension);
    }

    [Fact]
    public void Same_invalid_candidate_returns_identical_machine_witness()
    {
        var state = WaitingRun(TestData.PendingRequest(partySize: 5));
        var context = new PhysicalValidationContext(
            state,
            TestData.VehicleOne,
            state.Vehicles[TestData.VehicleOne].Route,
            CompleteTravel(),
            new SimTime(1000));
        var validator = new PhysicalPlanValidator();

        var first = validator.Validate(context);
        var second = validator.Validate(context);

        Assert.Equal(first.Witness, second.Witness);
    }

    [Fact]
    public void Probe_records_exogenous_ride_time_breach_and_keeps_active_route()
    {
        var state = WaitingRun(TestData.PendingRequest(maxRideTimeMs: 50));
        var validator = new PhysicalPlanValidator();

        var probe = validator.ProbeServiceQuality(
            state,
            TestData.VehicleOne,
            CompleteTravel(),
            new SimTime(1000));

        Assert.True(probe.IsSuccess, probe.Witness?.Message);
        var breach = Assert.Single(probe.Allowance.Breaches);
        Assert.Equal(PhysicalViolationCodes.MaxRideTime, breach.Code);
        Assert.Equal(new RequestId("r-1"), breach.RequestId);
        Assert.Equal(50, breach.ContractualMilliseconds);
        Assert.Equal(100, breach.ExogenousMilliseconds);

        var relaxed = validator.ValidateWithExogenousRelief(
            state,
            TestData.VehicleOne,
            state.Vehicles[TestData.VehicleOne].Route,
            CompleteTravel(),
            new SimTime(1000));

        Assert.True(relaxed.IsFeasible, relaxed.Witness?.Message);
    }

    [Fact]
    public void Probe_records_exogenous_pickup_window_breach_and_keeps_active_route()
    {
        var state = WaitingRun(
            TestData.PendingRequest(earliestPickupMs: 1000, latestPickupMs: 1050));
        var validator = new PhysicalPlanValidator();

        var probe = validator.ProbeServiceQuality(
            state,
            TestData.VehicleOne,
            CompleteTravel(),
            new SimTime(1000));

        Assert.True(probe.IsSuccess, probe.Witness?.Message);
        var breach = Assert.Single(probe.Allowance.Breaches);
        Assert.Equal(PhysicalViolationCodes.PickupWindow, breach.Code);
        Assert.Equal(1050, breach.ContractualMilliseconds);
        Assert.Equal(1100, breach.ExogenousMilliseconds);

        var relaxed = validator.ValidateWithExogenousRelief(
            state,
            TestData.VehicleOne,
            state.Vehicles[TestData.VehicleOne].Route,
            CompleteTravel(),
            new SimTime(1000));

        Assert.True(relaxed.IsFeasible, relaxed.Witness?.Message);
    }

    [Fact]
    public void Exogenous_relief_still_rejects_a_candidate_worse_than_doing_nothing()
    {
        var state = WaitingRun(TestData.PendingRequest(maxRideTimeMs: 50));

        // Detour n-1 -> n-2 -> n-1 -> n-2 before the drop. The relief only
        // covers the 100 ms the unchanged route already realizes, so the extra
        // 200 ms this candidate adds is still charged to the decision.
        var candidate = state.Vehicles[TestData.VehicleOne]
            .Route
            .ReplaceMutableSuffix(
                [
                    TestData.Pickup(),
                    TestData.Waypoint("detour-out", TestData.NodeTwo),
                    TestData.Waypoint("detour-back", TestData.NodeOne),
                    TestData.DropOff(),
                ])
            .Value!;

        var result = new PhysicalPlanValidator().ValidateWithExogenousRelief(
            state,
            TestData.VehicleOne,
            candidate,
            CompleteTravel(),
            new SimTime(1000));

        AssertViolation(
            result,
            PhysicalViolationCodes.MaxRideTime,
            "r-1",
            "drop",
            "maxRideTimeMs");
        Assert.Equal(100, result.Witness?.Expected);
        Assert.Equal(300, result.Witness?.Actual);
    }

    [Fact]
    public void Probe_fails_closed_on_a_structural_violation_of_the_active_route()
    {
        var state = WaitingRun(TestData.PendingRequest(partySize: 5));

        var probe = new PhysicalPlanValidator().ProbeServiceQuality(
            state,
            TestData.VehicleOne,
            CompleteTravel(),
            new SimTime(1000));

        Assert.False(probe.IsSuccess);
        Assert.Equal(PhysicalViolationCodes.Capacity, probe.Witness?.Code);
        Assert.Empty(probe.Allowance.Breaches);
    }

    [Fact]
    public void Probe_of_a_route_that_meets_every_deadline_grants_no_relief()
    {
        var state = WaitingRun(TestData.PendingRequest());

        var probe = new PhysicalPlanValidator().ProbeServiceQuality(
            state,
            TestData.VehicleOne,
            CompleteTravel(),
            new SimTime(1000));

        Assert.True(probe.IsSuccess, probe.Witness?.Message);
        Assert.Same(ServiceQualityAllowance.Strict, probe.Allowance);
        Assert.Empty(probe.Allowance.Digest);
    }

    [Fact]
    public void Relief_is_scoped_to_the_breached_request_and_dimension()
    {
        var allowance = ServiceQualityAllowance.FromBreaches(
            [
                new ServiceQualityBreach(
                    new RequestId("r-1"),
                    PhysicalViolationCodes.MaxRideTime,
                    "maxRideTimeMs",
                    50,
                    100),
            ]);

        Assert.Equal(100, allowance.MaxRideTimeBound(new RequestId("r-1"), 50));
        Assert.Equal(50, allowance.LatestPickupBound(new RequestId("r-1"), 50));
        Assert.Equal(50, allowance.MaxRideTimeBound(new RequestId("r-2"), 50));

        // Relief never tightens a bound either: a contractual limit above the
        // exogenous value wins.
        Assert.Equal(400, allowance.MaxRideTimeBound(new RequestId("r-1"), 400));
    }

    private static RideBoundRun WaitingRun(RideRequest request)
    {
        var route = TestData.Route(
            mutable: [TestData.Pickup(request.Id), TestData.DropOff(request.Id)]);
        var run = TestData.EmptyRun().AddRequest(request).Value!;
        run = run.BootstrapVehicle(TestData.EmptyVehicle(route)).Value!;
        run = run.AcceptRequest(request.Id, TestData.VehicleOne).Value!;
        return run.ConfirmWaitingPickup(request.Id).Value!;
    }

    private static PhysicalValidationResult Validate(
        RideBoundRun state,
        RoutePlan candidate,
        ITravelTimeLookup travelTimes,
        long evaluationTimeMs = 1000) =>
        new PhysicalPlanValidator().Validate(
            new PhysicalValidationContext(
                state,
                TestData.VehicleOne,
                candidate,
                travelTimes,
                new SimTime(evaluationTimeMs)));

    private static DictionaryTravelLookup CompleteTravel() =>
        new(
            (TestData.NodeZero, TestData.NodeOne, 100),
            (TestData.NodeZero, TestData.NodeTwo, 200),
            (TestData.NodeOne, TestData.NodeTwo, 100),
            (TestData.NodeTwo, TestData.NodeOne, 100),
            (TestData.NodeTwo, TestData.NodeZero, 200));

    private static void AssertViolation(
        PhysicalValidationResult result,
        string code,
        string requestId,
        string stopId,
        string dimension)
    {
        Assert.False(result.IsFeasible);
        Assert.Equal(code, result.Witness?.Code);
        Assert.Equal(new RequestId(requestId), result.Witness?.RequestId);
        Assert.Equal(new StopId(stopId), result.Witness?.StopId);
        Assert.Equal(dimension, result.Witness?.Dimension);
    }

    private static IEnumerable<RouteStop[]> Permutations(RouteStop[] values)
    {
        return Permute(values, 0);

        static IEnumerable<RouteStop[]> Permute(RouteStop[] source, int index)
        {
            if (index == source.Length)
            {
                yield return source.ToArray();
                yield break;
            }

            for (var current = index; current < source.Length; current++)
            {
                (source[index], source[current]) = (source[current], source[index]);

                foreach (var value in Permute(source, index + 1))
                {
                    yield return value;
                }

                (source[index], source[current]) = (source[current], source[index]);
            }
        }
    }

    private sealed class DictionaryTravelLookup : ITravelTimeLookup
    {
        private readonly Dictionary<(NodeId From, NodeId To), Duration> _times;

        public DictionaryTravelLookup(
            params (NodeId From, NodeId To, long TimeMs)[] arcs)
        {
            _times = arcs.ToDictionary(
                arc => (arc.From, arc.To),
                arc => new Duration(arc.TimeMs));
        }

        public bool TryGetTravelTime(
            NodeId fromNodeId,
            NodeId toNodeId,
            out Duration travelTime)
        {
            if (fromNodeId == toNodeId)
            {
                travelTime = new Duration(0);
                return true;
            }

            return _times.TryGetValue((fromNodeId, toNodeId), out travelTime);
        }
    }
}
