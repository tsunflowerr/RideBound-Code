using RideBound.Application.Scheduling;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Scheduling;

public sealed class RouteScheduleProjectorTests
{
    [Fact]
    public void Shared_projection_waits_for_pickup_window_and_is_deterministic()
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
                    new Duration(10)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(20)),
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
            new SimTime(0)).AddRequest(request).Value!;
        var projector = new RouteScheduleProjector();

        var first = projector.Project(
            run,
            vehicle,
            route,
            ApplicationTestData.Travel(),
            new SimTime(0));
        var second = projector.Project(
            run,
            vehicle,
            route,
            ApplicationTestData.Travel(),
            new SimTime(0));

        Assert.True(first.IsSuccess, first.Failure?.Message);
        Assert.Equal(
            first.Schedule!.OperationalCost,
            second.Schedule!.OperationalCost);
        Assert.Equal(first.Schedule.Stops, second.Schedule.Stops);
        Assert.Collection(
            first.Schedule.Stops,
            pickup =>
            {
                Assert.Equal(100, pickup.ArrivalTime.Milliseconds);
                Assert.Equal(1000, pickup.ServiceStartTime.Milliseconds);
                Assert.Equal(1010, pickup.DepartureTime.Milliseconds);
            },
            drop =>
            {
                Assert.Equal(1110, drop.ArrivalTime.Milliseconds);
                Assert.Equal(1130, drop.DepartureTime.Milliseconds);
            });
        Assert.Equal(1130, first.Schedule.OperationalCost);
    }

    [Fact]
    public void Edge_progress_uses_integer_ceiling_before_first_stop()
    {
        var nodeThree = new NodeId("n-3");
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("waypoint"),
                    nodeThree,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new EdgeProgressPosition(
                ApplicationTestData.NodeZero,
                ApplicationTestData.NodeOne,
                "edge-1",
                333),
            [],
            [],
            route,
            0).Value!;
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('b', 64),
            [
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(
                        ApplicationTestData.NodeZero,
                        ApplicationTestData.NodeOne),
                    new Duration(101)),
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(ApplicationTestData.NodeOne, nodeThree),
                    new Duration(10)),
            ]).Value!;
        var run = RideBoundRun.Create(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            new SimTime(0));

        var result = new RouteScheduleProjector().Project(
            run,
            vehicle,
            route,
            travel,
            new SimTime(0));

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(78, result.Schedule!.Stops[0].ArrivalTime.Milliseconds);
    }

    [Fact]
    public void Missing_directed_arc_returns_projection_failure()
    {
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("waypoint"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.Waypoint,
                    null,
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
        var travel = TravelTimeSnapshot.Create(
            1,
            new string('b', 64),
            [
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(
                        ApplicationTestData.NodeZero,
                        ApplicationTestData.NodeOne),
                    new Duration(100)),
            ]).Value!;

        var result = new RouteScheduleProjector().Project(
            run,
            vehicle,
            route,
            travel,
            new SimTime(0));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            SchedulingFailureCodes.ScheduleProjectionFailed,
            result.Failure?.Code);
    }
}
