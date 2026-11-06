using RideBound.Application.Events;
using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests;

internal static class ApplicationTestData
{
    public static readonly RunIdentifier RunId = new("run-1");

    public static readonly ScenarioIdentifier ScenarioId = new("scenario-1");

    public static readonly RequestId RequestId = new("r-1");

    public static readonly VehicleId VehicleId = new("v-1");

    public static readonly NodeId NodeZero = new("n-0");

    public static readonly NodeId NodeOne = new("n-1");

    public static readonly NodeId NodeTwo = new("n-2");

    public static OnlineState InitialState() =>
        OnlineState.Create(
            RideBoundRun.Create(RunId, ScenarioId, new SimTime(0)),
            new string('a', 64));

    public static RideRequest Request() =>
        RideRequest.CreatePending(
            RequestId,
            new SimTime(1000),
            NodeOne,
            NodeTwo,
            new SimTime(1000),
            new SimTime(2000),
            new Duration(1000),
            1,
            "standard",
            "uniform-v1").Value!;

    public static TravelTimeSnapshot Travel(long version = 1, char hash = 'a') =>
        TravelTimeSnapshot.Create(
            version,
            new string(hash, 64),
            [
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(NodeZero, NodeOne),
                    new Duration(100)),
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(NodeOne, NodeTwo),
                    new Duration(100)),
                new KeyValuePair<TravelArc, Duration>(
                    new TravelArc(NodeTwo, NodeZero),
                    new Duration(200)),
            ]).Value!;

    public static RoutePlan Route() =>
        RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("waypoint"),
                    NodeOne,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)),
            ]).Value!;

    public static VehicleState Vehicle(
        VehicleId? id = null,
        long observedEpoch = 1) =>
        VehicleState.Create(
            id ?? VehicleId,
            4,
            0,
            new NodePosition(NodeZero),
            [],
            [],
            Route(),
            observedEpoch).Value!;

    public static InternalEventBatch BootstrapBatch(
        bool appendUnknownVehicleEvent = false)
    {
        var time = new SimTime(1000);
        var events = new List<OnlineEvent>
        {
            new TravelTimesUpdated(1, time, Travel()),
            new RequestArrived(2, time, Request()),
            new VehicleAdvanced(3, time, Vehicle()),
        };

        if (appendUnknownVehicleEvent)
        {
            events.Add(
                new VehicleReachedStop(
                    4,
                    time,
                    new VehicleId("v-unknown"),
                    new StopId("waypoint"),
                    new PlanVersion(0),
                    new NodePosition(NodeOne)));
        }

        return new InternalEventBatch(RunId, ScenarioId, 1, time, events);
    }
}
