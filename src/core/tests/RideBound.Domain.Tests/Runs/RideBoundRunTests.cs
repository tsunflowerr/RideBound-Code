using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Domain.Tests.Runs;

public sealed class RideBoundRunTests
{
    [Fact]
    public void Boarding_updates_request_and_vehicle_atomically()
    {
        var route = TestData.Route(
            mutable: [TestData.Pickup(), TestData.DropOff()]);
        var initial = TestData.RunWithPendingAndVehicle(route);
        var accepted = initial.AcceptRequest(
            TestData.RequestOne,
            TestData.VehicleOne).Value!;
        var waiting = accepted.ConfirmWaitingPickup(TestData.RequestOne).Value!;

        var boarded = waiting.Board(
            TestData.VehicleOne,
            TestData.RequestOne,
            new PlanVersion(0),
            new SimTime(1200));

        Assert.True(boarded.IsSuccess);
        Assert.Equal(
            RequestLifecycle.Onboard,
            boarded.Value!.Requests[TestData.RequestOne].Lifecycle);
        Assert.Contains(
            TestData.RequestOne,
            boarded.Value.Vehicles[TestData.VehicleOne].OnboardRequestIds);
        Assert.Equal(1, boarded.Value.Vehicles[TestData.VehicleOne].OccupiedSeats);
        Assert.Equal(
            RequestLifecycle.WaitingPickup,
            waiting.Requests[TestData.RequestOne].Lifecycle);
        Assert.Equal(0, waiting.Vehicles[TestData.VehicleOne].OccupiedSeats);
    }

    [Fact]
    public void Capacity_failure_leaves_both_aggregate_members_unchanged()
    {
        var route = TestData.Route(
            mutable: [TestData.Pickup(), TestData.DropOff()]);
        var request = TestData.PendingRequest(partySize: 2);
        var run = TestData.EmptyRun().AddRequest(request).Value!;
        run = run.BootstrapVehicle(
            TestData.EmptyVehicle(route, capacity: 1)).Value!;
        run = run.AcceptRequest(TestData.RequestOne, TestData.VehicleOne).Value!;
        run = run.ConfirmWaitingPickup(TestData.RequestOne).Value!;

        var result = run.Board(
            TestData.VehicleOne,
            TestData.RequestOne,
            new PlanVersion(0),
            new SimTime(1200));

        Assert.False(result.IsSuccess);
        Assert.Equal(VehicleFailureCodes.Capacity, result.Failure?.Code);
        Assert.Equal(
            RequestLifecycle.WaitingPickup,
            run.Requests[TestData.RequestOne].Lifecycle);
        Assert.Equal(0, run.Vehicles[TestData.VehicleOne].OccupiedSeats);
    }

    [Fact]
    public void Duplicate_request_and_vehicle_do_not_create_second_state()
    {
        var request = TestData.PendingRequest();
        var run = TestData.EmptyRun().AddRequest(request).Value!;
        var duplicateRequest = run.AddRequest(request);
        var vehicle = TestData.EmptyVehicle();
        run = run.BootstrapVehicle(vehicle).Value!;
        var duplicateVehicle = run.BootstrapVehicle(vehicle);

        Assert.False(duplicateRequest.IsSuccess);
        Assert.Equal(RunFailureCodes.DuplicateRequest, duplicateRequest.Failure?.Code);
        Assert.Single(run.Requests);
        Assert.False(duplicateVehicle.IsSuccess);
        Assert.Equal(RunFailureCodes.DuplicateVehicle, duplicateVehicle.Failure?.Code);
        Assert.Single(run.Vehicles);
    }

    [Fact]
    public void Unknown_vehicle_after_bootstrap_and_stale_observation_are_rejected()
    {
        var run = TestData.RunWithPendingAndVehicle();
        var epochOne = run.AdvanceEpoch(1, new SimTime(1000)).Value!;
        var unknown = epochOne.BootstrapVehicle(
            TestData.EmptyVehicle(id: TestData.VehicleTwo, observedEpoch: 1));
        var stale = epochOne.ObserveVehicle(
            TestData.EmptyVehicle(observedEpoch: 0));

        Assert.False(unknown.IsSuccess);
        Assert.Equal(RunFailureCodes.VehicleBootstrapOnly, unknown.Failure?.Code);
        Assert.False(stale.IsSuccess);
        Assert.Equal(
            VehicleFailureCodes.StaleObservation,
            stale.Failure?.Code);
        Assert.Single(epochOne.Vehicles);
    }

    [Fact]
    public void External_snapshot_cannot_overwrite_core_route()
    {
        var run = TestData.RunWithPendingAndVehicle();
        var changedRoute = TestData.Route(
            mutable: [TestData.Waypoint("foreign")],
            version: 1);
        var observation = TestData.EmptyVehicle(
            changedRoute,
            observedEpoch: 1);

        var result = run.ObserveVehicle(observation);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            VehicleFailureCodes.ObservationConflict,
            result.Failure?.Code);
        Assert.Empty(run.Vehicles[TestData.VehicleOne].Route.MutableSuffix);
    }

    [Fact]
    public void Reached_stop_position_mismatch_preserves_route_progress()
    {
        var route = TestData.Route(
            mutable: [TestData.Waypoint("expected", TestData.NodeOne)]);
        var run = TestData.RunWithPendingAndVehicle(route);

        var result = run.ReachStop(
            TestData.VehicleOne,
            new StopId("expected"),
            new PlanVersion(0),
            new NodePosition(TestData.NodeTwo),
            observedEpoch: 0);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            VehicleFailureCodes.PositionMismatch,
            result.Failure?.Code);
        Assert.Equal(
            0,
            run.Vehicles[TestData.VehicleOne].Route.ExecutedStopCount);
        Assert.Single(
            run.Vehicles[TestData.VehicleOne].Route.MutableSuffix);
    }
}
