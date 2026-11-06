using RideBound.Application.Scheduling;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Promises;

public sealed class PromiseProjector
{
    public DomainResult<PromiseProjection> Project(
        RideBoundRun run,
        VehicleState vehicle,
        RoutePlan route,
        ProjectedRouteSchedule schedule,
        RequestId requestId,
        PromiseProjection? previous = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(vehicle);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(schedule);

        if (!run.Requests.TryGetValue(requestId, out var request)
            || !request.IsAcceptedActive
            || request.AssignedVehicleId != vehicle.Id
            || !vehicle.AcceptedRequestIds.Contains(requestId))
        {
            return Fail(
                requestId,
                "Promise projection requires an active accepted assignment.");
        }

        var remaining = route.RemainingStops.ToArray();
        var pickupStop = remaining.SingleOrDefault(
            stop => stop.RequestId == requestId
                && stop.Kind == RouteStopKind.Pickup);
        var dropStop = remaining.SingleOrDefault(
            stop => stop.RequestId == requestId
                && stop.Kind == RouteStopKind.DropOff);

        if (dropStop is null)
        {
            return Fail(
                requestId,
                "Active request has no remaining drop-off stop.");
        }

        var dropSchedule = schedule.Stops.SingleOrDefault(
            stop => stop.StopId == dropStop.StopId);

        if (dropSchedule is null)
        {
            return Fail(
                requestId,
                "Drop-off stop is missing from the projected schedule.");
        }

        StopId pickupStopId;
        NodeId pickupNodeId;
        SimTime pickupEta;

        if (pickupStop is not null)
        {
            var pickupSchedule = schedule.Stops.SingleOrDefault(
                stop => stop.StopId == pickupStop.StopId);

            if (pickupSchedule is null)
            {
                return Fail(
                    requestId,
                    "Pickup stop is missing from the projected schedule.");
            }

            pickupStopId = pickupStop.StopId;
            pickupNodeId = pickupStop.NodeId;
            pickupEta = pickupSchedule.ServiceStartTime;
        }
        else if (request.Lifecycle == RequestLifecycle.Onboard
            && previous is not null
            && previous.RequestId == requestId)
        {
            pickupStopId = previous.PickupStopId;
            pickupNodeId = previous.PickupNodeId;
            pickupEta = previous.PickupEta;
        }
        else if (request.Lifecycle == RequestLifecycle.Onboard
            && request.ActualPickupTime is not null)
        {
            // Booking confirmation and boarding can legitimately be reduced in
            // the same simulator batch. In booking-confirmation mode there is no
            // prior publication yet, so recover the just-realized pickup from the
            // executed frozen prefix and bind its actual service time.
            var realizedPickup = route.FrozenPrefix
                .Take(route.ExecutedStopCount)
                .LastOrDefault(
                    stop => stop.RequestId == requestId
                        && stop.Kind == RouteStopKind.Pickup);

            if (realizedPickup is null)
            {
                return Fail(
                    requestId,
                    "Onboard request has no realized pickup stop in the executed route.");
            }

            pickupStopId = realizedPickup.StopId;
            pickupNodeId = realizedPickup.NodeId;
            pickupEta = request.ActualPickupTime.Value;
        }
        else
        {
            return Fail(
                requestId,
                "Pre-pickup request has no projected pickup stop.");
        }

        try
        {
            return DomainResult<PromiseProjection>.Success(
                new PromiseProjection(
                    requestId,
                    vehicle.Id,
                    pickupStopId,
                    pickupNodeId,
                    dropStop.StopId,
                    dropStop.NodeId,
                    pickupEta,
                    dropSchedule.ServiceStartTime,
                    remaining.Select(
                        stop => new PromiseServiceToken(
                            stop.StopId,
                            stop.RequestId,
                            stop.Kind))));
        }
        catch (ArgumentException error)
        {
            return Fail(requestId, error.Message);
        }
    }

    private static DomainResult<PromiseProjection> Fail(
        RequestId requestId,
        string message) =>
        DomainResult<PromiseProjection>.Fail(
            CommitmentFailureCodes.PromiseProjectionFailed,
            message,
            requestId.Value);
}
