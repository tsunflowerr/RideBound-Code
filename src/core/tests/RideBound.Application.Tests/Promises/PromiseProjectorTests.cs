using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Promises;

public sealed class PromiseProjectorTests
{
    [Fact]
    public void Accepted_route_projects_full_initial_promise()
    {
        var (run, vehicle, route) = AcceptedState();
        var schedule = new RouteScheduleProjector().Project(
            run,
            vehicle,
            route,
            ApplicationTestData.Travel(),
            new SimTime(0)).Schedule!;

        var result = new PromiseProjector().Project(
            run,
            vehicle,
            route,
            schedule,
            ApplicationTestData.RequestId);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(ApplicationTestData.VehicleId, result.Value!.VehicleId);
        Assert.Equal(1000, result.Value.PickupEta.Milliseconds);
        Assert.Equal(1100, result.Value.DropEta.Milliseconds);
        Assert.Equal(
            ["pickup", "drop"],
            result.Value.ServiceOrder.Select(value => value.StopId.Value));
    }

    [Fact]
    public void Onboard_projection_carries_realized_pickup_from_prior_promise()
    {
        var (acceptedRun, _, route) = AcceptedState();
        var confirmed = acceptedRun.ConfirmWaitingPickup(
            ApplicationTestData.RequestId).Value!;
        var reached = confirmed.ReachStop(
            ApplicationTestData.VehicleId,
            new StopId("pickup"),
            route.Version,
            new NodePosition(ApplicationTestData.NodeOne),
            1).Value!;
        var onboard = reached.Board(
            ApplicationTestData.VehicleId,
            ApplicationTestData.RequestId,
            route.Version,
            new SimTime(1000)).Value!;
        var vehicle = onboard.Vehicles[ApplicationTestData.VehicleId];
        var schedule = new RouteScheduleProjector().Project(
            onboard,
            vehicle,
            vehicle.Route,
            ApplicationTestData.Travel(),
            new SimTime(1000)).Schedule!;
        var previous = new PromiseProjection(
            ApplicationTestData.RequestId,
            ApplicationTestData.VehicleId,
            new StopId("pickup"),
            ApplicationTestData.NodeOne,
            new StopId("drop"),
            ApplicationTestData.NodeTwo,
            new SimTime(1000),
            new SimTime(1100),
            [
                new PromiseServiceToken(
                    new StopId("pickup"),
                    ApplicationTestData.RequestId,
                    RouteStopKind.Pickup),
                new PromiseServiceToken(
                    new StopId("drop"),
                    ApplicationTestData.RequestId,
                    RouteStopKind.DropOff),
            ]);

        var result = new PromiseProjector().Project(
            onboard,
            vehicle,
            vehicle.Route,
            schedule,
            ApplicationTestData.RequestId,
            previous);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(1000, result.Value!.PickupEta.Milliseconds);
        var only = Assert.Single(result.Value.ServiceOrder);
        Assert.Equal(RouteStopKind.DropOff, only.Kind);
    }

    [Fact]
    public void First_projection_after_same_batch_boarding_uses_realized_pickup()
    {
        var (acceptedRun, _, route) = AcceptedState();
        var confirmed = acceptedRun.ConfirmWaitingPickup(
            ApplicationTestData.RequestId).Value!;
        var reached = confirmed.ReachStop(
            ApplicationTestData.VehicleId,
            new StopId("pickup"),
            route.Version,
            new NodePosition(ApplicationTestData.NodeOne),
            1).Value!;
        var onboard = reached.Board(
            ApplicationTestData.VehicleId,
            ApplicationTestData.RequestId,
            route.Version,
            new SimTime(1000)).Value!;
        var vehicle = onboard.Vehicles[ApplicationTestData.VehicleId];
        var schedule = new RouteScheduleProjector().Project(
            onboard,
            vehicle,
            vehicle.Route,
            ApplicationTestData.Travel(),
            new SimTime(1000)).Schedule!;

        var result = new PromiseProjector().Project(
            onboard,
            vehicle,
            vehicle.Route,
            schedule,
            ApplicationTestData.RequestId);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(new StopId("pickup"), result.Value!.PickupStopId);
        Assert.Equal(ApplicationTestData.NodeOne, result.Value.PickupNodeId);
        Assert.Equal(1000, result.Value.PickupEta.Milliseconds);
        var only = Assert.Single(result.Value.ServiceOrder);
        Assert.Equal(RouteStopKind.DropOff, only.Kind);
    }

    private static (RideBoundRun Run, VehicleState Vehicle, RoutePlan Route)
        AcceptedState()
    {
        var request = ApplicationTestData.Request();
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            route,
            0).Value!;
        var run = RideBoundRun.Create(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            new SimTime(0));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(request.Id, vehicle.Id).Value!;

        return (run, run.Vehicles[vehicle.Id], route);
    }
}
