using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Domain.Tests;

internal static class TestData
{
    public static readonly VehicleId VehicleOne = new("v-1");

    public static readonly VehicleId VehicleTwo = new("v-2");

    public static readonly RequestId RequestOne = new("r-1");

    public static readonly NodeId NodeZero = new("n-0");

    public static readonly NodeId NodeOne = new("n-1");

    public static readonly NodeId NodeTwo = new("n-2");

    public static RideRequest PendingRequest(
        RequestId? id = null,
        long partySize = 1,
        long earliestPickupMs = 1000,
        long latestPickupMs = 2000,
        long maxRideTimeMs = 1000) =>
        RideRequest.CreatePending(
            id ?? RequestOne,
            new SimTime(1000),
            NodeOne,
            NodeTwo,
            new SimTime(earliestPickupMs),
            new SimTime(latestPickupMs),
            new Duration(maxRideTimeMs),
            partySize,
            "standard",
            "uniform-v1").Value!;

    public static RouteStop Pickup(RequestId? id = null, string stopId = "pickup") =>
        new(
            new StopId(stopId),
            NodeOne,
            RouteStopKind.Pickup,
            id ?? RequestOne,
            new Duration(0));

    public static RouteStop DropOff(RequestId? id = null, string stopId = "drop") =>
        new(
            new StopId(stopId),
            NodeTwo,
            RouteStopKind.DropOff,
            id ?? RequestOne,
            new Duration(0));

    public static RouteStop Waypoint(
        string stopId = "waypoint",
        NodeId? nodeId = null) =>
        new(
            new StopId(stopId),
            nodeId ?? NodeOne,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));

    public static RoutePlan Route(
        IEnumerable<RouteStop>? frozen = null,
        IEnumerable<RouteStop>? mutable = null,
        long executed = 0,
        long version = 0) =>
        RoutePlan.Create(
            new PlanVersion(version),
            executed,
            frozen ?? [],
            mutable ?? []).Value!;

    public static VehicleState EmptyVehicle(
        RoutePlan? route = null,
        VehicleId? id = null,
        long capacity = 4,
        long observedEpoch = 0) =>
        VehicleState.Create(
            id ?? VehicleOne,
            capacity,
            0,
            new NodePosition(NodeZero),
            [],
            [],
            route ?? Route(),
            observedEpoch).Value!;

    public static RideBoundRun EmptyRun() =>
        RideBoundRun.Create(
            new RunIdentifier("run-1"),
            new ScenarioIdentifier("scenario-1"),
            new SimTime(0));

    public static RideBoundRun RunWithPendingAndVehicle(RoutePlan? route = null)
    {
        var withRequest = EmptyRun().AddRequest(PendingRequest()).Value!;
        return withRequest.BootstrapVehicle(EmptyVehicle(route)).Value!;
    }
}
