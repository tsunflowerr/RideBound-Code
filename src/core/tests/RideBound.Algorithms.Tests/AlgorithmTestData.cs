using RideBound.Application.State;
using RideBound.Application.Travel;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Algorithms.Tests;

internal static class AlgorithmTestData
{
    public static readonly RunIdentifier RunId = new("algorithm-run");
    public static readonly ScenarioIdentifier ScenarioId =
        new("algorithm-scenario");
    public static readonly VehicleId VehicleOne = new("vehicle-1");
    public static readonly VehicleId VehicleTwo = new("vehicle-2");
    public static readonly NodeId NodeZero = new("node-0");
    public static readonly NodeId NodeOne = new("node-1");
    public static readonly NodeId NodeTwo = new("node-2");
    public static readonly NodeId NodeThree = new("node-3");

    public static OnlineState CreateState(
        IEnumerable<RideRequest> requests,
        IEnumerable<VehicleState> vehicles,
        SimTime? time = null,
        IEnumerable<KeyValuePair<TravelArc, Duration>>? arcs = null)
    {
        var evaluationTime = time ?? new SimTime(1_000);
        var run = RideBoundRun.Create(RunId, ScenarioId, evaluationTime);

        foreach (var request in requests)
        {
            run = run.AddRequest(request).Value!;
        }

        foreach (var vehicle in vehicles)
        {
            run = run.BootstrapVehicle(vehicle).Value!;
        }

        var travel = TravelTimeSnapshot.Create(
            1,
            new string('a', 64),
            arcs ?? CompleteArcs()).Value!;

        return new OnlineState(
            run,
            travel,
            1,
            travel.SnapshotHash,
            RideBound.Domain.Commitments.CommitmentLedger.Empty);
    }

    public static RideRequest PendingRequest(
        string id = "request-1",
        NodeId? origin = null,
        NodeId? destination = null,
        long latestPickup = 10_000,
        long maxRideTime = 10_000,
        long partySize = 1) =>
        RideRequest.CreatePending(
            new RequestId(id),
            new SimTime(1_000),
            origin ?? NodeOne,
            destination ?? NodeTwo,
            new SimTime(1_000),
            new SimTime(latestPickup),
            new Duration(maxRideTime),
            partySize,
            "standard",
            "uniform-v1").Value!;

    public static VehicleState Vehicle(
        VehicleId? id = null,
        long capacity = 4,
        IEnumerable<RouteStop>? mutableSuffix = null,
        NodeId? position = null)
    {
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            mutableSuffix ?? []).Value!;
        return VehicleState.Create(
            id ?? VehicleOne,
            capacity,
            0,
            new NodePosition(position ?? NodeZero),
            [],
            [],
            route,
            1).Value!;
    }

    public static IReadOnlyList<KeyValuePair<TravelArc, Duration>> CompleteArcs()
    {
        var nodes = new[] { NodeZero, NodeOne, NodeTwo, NodeThree };
        var result = new List<KeyValuePair<TravelArc, Duration>>();

        for (var from = 0; from < nodes.Length; from++)
        {
            for (var to = 0; to < nodes.Length; to++)
            {
                if (from == to)
                {
                    continue;
                }

                result.Add(
                    new KeyValuePair<TravelArc, Duration>(
                        new TravelArc(nodes[from], nodes[to]),
                        new Duration(100 + Math.Abs(to - from) * 10)));
            }
        }

        return result;
    }
}
