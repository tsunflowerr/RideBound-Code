using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Scheduling;

public sealed record ProjectedStop(
    StopId StopId,
    SimTime ArrivalTime,
    SimTime ServiceStartTime,
    SimTime DepartureTime);

public sealed record ProjectedRouteSchedule(
    IReadOnlyList<ProjectedStop> Stops,
    long OperationalCost);

public sealed record RouteScheduleProjectionResult
{
    private RouteScheduleProjectionResult(
        ProjectedRouteSchedule? schedule,
        DomainFailure? failure)
    {
        Schedule = schedule;
        Failure = failure;
    }

    public bool IsSuccess => Schedule is not null;

    public ProjectedRouteSchedule? Schedule { get; }

    public DomainFailure? Failure { get; }

    public static RouteScheduleProjectionResult Success(
        ProjectedRouteSchedule schedule) =>
        new(schedule, null);

    public static RouteScheduleProjectionResult Fail(string message) =>
        new(
            null,
            new DomainFailure(
                SchedulingFailureCodes.ScheduleProjectionFailed,
                message));
}

public sealed class RouteScheduleProjector
{
    public RouteScheduleProjectionResult Project(
        RideBoundRun run,
        VehicleState vehicle,
        RoutePlan route,
        ITravelTimeLookup travelTimes,
        SimTime evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(travelTimes);

        var time = evaluationTime;
        NodeId currentNode;

        if (vehicle.Position is NodePosition node)
        {
            currentNode = node.NodeId;
        }
        else if (vehicle.Position is EdgeProgressPosition edge)
        {
            if (!travelTimes.TryGetTravelTime(
                    edge.FromNodeId,
                    edge.ToNodeId,
                    out var fullEdgeTime))
            {
                return Fail(
                    $"Travel snapshot has no current directed edge " +
                    $"'{edge.FromNodeId}->{edge.ToNodeId}'.");
            }

            try
            {
                var remaining = DivideRoundUp(
                    checked(
                        fullEdgeTime.Milliseconds
                        * (1000 - edge.ProgressPermille)),
                    1000);
                time = travelTimes.Arrive(evaluationTime, time, new Duration(remaining));
            }
            catch (Exception error) when (
                error is OverflowException or ArgumentOutOfRangeException)
            {
                return Fail("Current edge schedule exceeds canonical time.");
            }

            currentNode = edge.ToNodeId;
        }
        else
        {
            return Fail("Vehicle position type is unsupported.");
        }

        var stops = new List<ProjectedStop>();

        foreach (var stop in route.RemainingStops)
        {
            if (!travelTimes.TryGetTravelTime(
                    currentNode,
                    stop.NodeId,
                    out var travelTime))
            {
                return Fail(
                    $"Travel snapshot has no directed arc " +
                    $"'{currentNode}->{stop.NodeId}'.");
            }

            SimTime arrival;
            SimTime serviceStart;
            SimTime departure;

            try
            {
                arrival = travelTimes.Arrive(evaluationTime, time, travelTime);
                serviceStart = GetServiceStart(run, stop, arrival);
                departure = serviceStart + stop.ServiceDuration;
            }
            catch (Exception error) when (
                error is OverflowException or ArgumentOutOfRangeException)
            {
                return Fail("Route schedule exceeds canonical time.");
            }

            stops.Add(
                new ProjectedStop(
                    stop.StopId,
                    arrival,
                    serviceStart,
                    departure));
            time = departure;
            currentNode = stop.NodeId;
        }

        var cost = time.Milliseconds - evaluationTime.Milliseconds;

        return cost < 0
            ? Fail("Route operational cost cannot be negative.")
            : RouteScheduleProjectionResult.Success(
                new ProjectedRouteSchedule(stops.AsReadOnly(), cost));
    }

    private static SimTime GetServiceStart(
        RideBoundRun run,
        RouteStop stop,
        SimTime arrival)
    {
        if (stop.Kind != RouteStopKind.Pickup
            || stop.RequestId is not RequestId requestId
            || !run.Requests.TryGetValue(requestId, out var request))
        {
            return arrival;
        }

        return arrival.Milliseconds < request.EarliestPickup.Milliseconds
            ? request.EarliestPickup
            : arrival;
    }

    private static long DivideRoundUp(long value, long divisor) =>
        value / divisor + (value % divisor == 0 ? 0 : 1);

    private static RouteScheduleProjectionResult Fail(string message) =>
        RouteScheduleProjectionResult.Fail(message);
}

public static class SchedulingFailureCodes
{
    public const string ScheduleProjectionFailed =
        "SCHEDULE_PROJECTION_FAILED";
}
