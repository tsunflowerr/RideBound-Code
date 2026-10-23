using System.Collections.Frozen;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Vehicles;

public sealed class VehicleState
{
    private VehicleState(
        VehicleId id,
        long capacity,
        long occupiedSeats,
        VehiclePosition position,
        IEnumerable<RequestId> onboardRequestIds,
        IEnumerable<RequestId> acceptedRequestIds,
        RoutePlan route,
        long lastObservedEpoch)
    {
        Id = id;
        Capacity = capacity;
        OccupiedSeats = occupiedSeats;
        Position = position;
        OnboardRequestIds = onboardRequestIds.ToFrozenSet();
        AcceptedRequestIds = acceptedRequestIds.ToFrozenSet();
        Route = route;
        LastObservedEpoch = lastObservedEpoch;
    }

    public VehicleId Id { get; }

    public long Capacity { get; }

    public long OccupiedSeats { get; }

    public VehiclePosition Position { get; }

    public IReadOnlySet<RequestId> OnboardRequestIds { get; }

    public IReadOnlySet<RequestId> AcceptedRequestIds { get; }

    public RoutePlan Route { get; }

    public long LastObservedEpoch { get; }

    public static DomainResult<VehicleState> Create(
        VehicleId id,
        long capacity,
        long occupiedSeats,
        VehiclePosition position,
        IEnumerable<RequestId> onboardRequestIds,
        IEnumerable<RequestId> acceptedRequestIds,
        RoutePlan route,
        long observedEpoch)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(onboardRequestIds);
        ArgumentNullException.ThrowIfNull(acceptedRequestIds);
        ArgumentNullException.ThrowIfNull(route);
        var onboard = onboardRequestIds.ToArray();
        var accepted = acceptedRequestIds.ToArray();

        if (capacity is < 1 or > DomainLimits.MaxCanonicalInteger
            || occupiedSeats < 0
            || occupiedSeats > capacity)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.InvalidVehicleState,
                "Vehicle capacity/load is outside the canonical range.",
                id.Value,
                "capacity");
        }

        if (observedEpoch is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.InvalidVehicleState,
                "Observed epoch is outside the canonical range.",
                id.Value,
                "lastObservedEpoch");
        }

        if (onboard.Distinct().Count() != onboard.Length
            || accepted.Distinct().Count() != accepted.Length)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.DuplicateRider,
                "Vehicle rider sets cannot contain duplicate IDs.",
                id.Value,
                "requestIds");
        }

        if (onboard.Except(accepted).Any())
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.InvalidVehicleState,
                "Accepted riders must contain every onboard rider.",
                id.Value,
                "acceptedRequestIds");
        }

        return DomainResult<VehicleState>.Success(
            new VehicleState(
                id,
                capacity,
                occupiedSeats,
                position,
                onboard,
                accepted,
                route,
                observedEpoch));
    }

    public DomainResult<VehicleState> Observe(
        long capacity,
        long occupiedSeats,
        VehiclePosition position,
        IEnumerable<RequestId> onboardRequestIds,
        IEnumerable<RequestId> acceptedRequestIds,
        RoutePlan observedRoute,
        long observedEpoch)
    {
        if (observedEpoch <= LastObservedEpoch)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.StaleObservation,
                "Vehicle observation epoch must advance monotonically.",
                Id.Value,
                "lastObservedEpoch");
        }

        if (capacity != Capacity
            || occupiedSeats != OccupiedSeats
            || !OnboardRequestIds.SetEquals(onboardRequestIds)
            || !AcceptedRequestIds.SetEquals(acceptedRequestIds)
            || !Route.IsSemanticallyEqual(observedRoute))
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.ObservationConflict,
                "External observation cannot overwrite core-owned load, riders, or route.",
                Id.Value);
        }

        return Create(
            Id,
            Capacity,
            OccupiedSeats,
            position,
            OnboardRequestIds,
            AcceptedRequestIds,
            Route,
            observedEpoch);
    }

    public DomainResult<VehicleState> Assign(RequestId requestId)
    {
        if (AcceptedRequestIds.Contains(requestId))
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.DuplicateRider,
                "Request is already assigned to the vehicle.",
                requestId.Value,
                "acceptedRequestIds");
        }

        return Create(
            Id,
            Capacity,
            OccupiedSeats,
            Position,
            OnboardRequestIds,
            AcceptedRequestIds.Append(requestId),
            Route,
            LastObservedEpoch);
    }

    public DomainResult<VehicleState> ReachStop(
        StopId stopId,
        PlanVersion observedPlanVersion,
        NodePosition position,
        long observedEpoch)
    {
        if (observedPlanVersion != Route.Version)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.StalePlanVersion,
                "Reached-stop event does not match the active plan version.",
                Id.Value,
                "planVersion");
        }

        if (observedEpoch < LastObservedEpoch)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.StaleObservation,
                "Reached-stop epoch cannot move backwards.",
                Id.Value,
                "lastObservedEpoch");
        }

        var expectedStop = Route.RemainingStops.FirstOrDefault();

        if (expectedStop is not null && expectedStop.NodeId != position.NodeId)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.PositionMismatch,
                "Reached-stop position does not match the active route stop.",
                stopId.Value,
                "position");
        }

        var advanced = Route.AdvanceReachedStop(stopId);

        return advanced.IsSuccess
            ? Create(
                Id,
                Capacity,
                OccupiedSeats,
                position,
                OnboardRequestIds,
                AcceptedRequestIds,
                advanced.Value!,
                observedEpoch)
            : DomainResult<VehicleState>.Fail(
                advanced.Failure!.Code,
                advanced.Failure.Message,
                advanced.Failure.EntityId,
                advanced.Failure.Dimension);
    }

    public DomainResult<VehicleState> Board(
        RequestId requestId,
        long partySize,
        PlanVersion observedPlanVersion)
    {
        if (observedPlanVersion != Route.Version)
        {
            return StalePlan();
        }

        if (!AcceptedRequestIds.Contains(requestId)
            || OnboardRequestIds.Contains(requestId))
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.RiderStateMismatch,
                "Boarding request must be accepted and not already onboard.",
                requestId.Value,
                "onboardRequestIds");
        }

        long nextLoad;

        try
        {
            nextLoad = checked(OccupiedSeats + partySize);
        }
        catch (OverflowException)
        {
            return CapacityFailure(requestId);
        }

        if (partySize <= 0 || nextLoad > Capacity)
        {
            return CapacityFailure(requestId);
        }

        return Create(
            Id,
            Capacity,
            nextLoad,
            Position,
            OnboardRequestIds.Append(requestId),
            AcceptedRequestIds,
            Route,
            LastObservedEpoch);
    }

    public DomainResult<VehicleState> Alight(
        RequestId requestId,
        long partySize,
        PlanVersion observedPlanVersion)
    {
        if (observedPlanVersion != Route.Version)
        {
            return StalePlan();
        }

        if (!OnboardRequestIds.Contains(requestId)
            || !AcceptedRequestIds.Contains(requestId)
            || partySize <= 0
            || partySize > OccupiedSeats)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.RiderStateMismatch,
                "Alighting request/load does not match the vehicle state.",
                requestId.Value,
                "onboardRequestIds");
        }

        return Create(
            Id,
            Capacity,
            OccupiedSeats - partySize,
            Position,
            OnboardRequestIds.Where(value => value != requestId),
            AcceptedRequestIds.Where(value => value != requestId),
            Route,
            LastObservedEpoch);
    }

    public DomainResult<VehicleState> CancelAccepted(RequestId requestId)
    {
        if (OnboardRequestIds.Contains(requestId))
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.RiderStateMismatch,
                "An onboard request cannot use after-acceptance cancellation.",
                requestId.Value,
                "onboardRequestIds");
        }

        if (!AcceptedRequestIds.Contains(requestId))
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.RiderStateMismatch,
                "Cancelled request is not assigned to the vehicle.",
                requestId.Value,
                "acceptedRequestIds");
        }

        var route = Route.RemoveRequestFromMutableSuffix(requestId);

        return route.IsSuccess
            ? Create(
                Id,
                Capacity,
                OccupiedSeats,
                Position,
                OnboardRequestIds,
                AcceptedRequestIds.Where(value => value != requestId),
                route.Value!,
                LastObservedEpoch)
            : DomainResult<VehicleState>.Fail(
                route.Failure!.Code,
                route.Failure.Message,
                route.Failure.EntityId,
                route.Failure.Dimension);
    }

    /// <summary>
    /// Removes a waiting rider that moves to another vehicle from the accepted set. Unlike
    /// <see cref="CancelAccepted"/> it leaves the route alone: the donor route without the
    /// rider's stops must already be applied.
    /// </summary>
    public DomainResult<VehicleState> Release(RequestId requestId)
    {
        if (OnboardRequestIds.Contains(requestId)
            || !AcceptedRequestIds.Contains(requestId))
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.RiderStateMismatch,
                "A released request must be assigned to the vehicle and not onboard.",
                requestId.Value,
                "acceptedRequestIds");
        }

        if (Route.RemainingStops.Any(stop => stop.RequestId == requestId))
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.RiderStateMismatch,
                "A released request must have no remaining stop on the vehicle route.",
                requestId.Value,
                "routeStops");
        }

        return Create(
            Id,
            Capacity,
            OccupiedSeats,
            Position,
            OnboardRequestIds,
            AcceptedRequestIds.Where(value => value != requestId),
            Route,
            LastObservedEpoch);
    }

    public DomainResult<VehicleState> UpdateRoute(RoutePlan route)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (!Route.HasExactFrozenPrefix(route))
        {
            return DomainResult<VehicleState>.Fail(
                RouteFailureCodes.FrozenPrefix,
                "Candidate route changed the exact frozen prefix.",
                Id.Value,
                "frozenPrefix");
        }

        if (route.Version.Value != Route.Version.Value + 1)
        {
            return DomainResult<VehicleState>.Fail(
                VehicleFailureCodes.StalePlanVersion,
                "A changed route must advance planVersion by exactly one.",
                Id.Value,
                "planVersion");
        }

        return Create(
            Id,
            Capacity,
            OccupiedSeats,
            Position,
            OnboardRequestIds,
            AcceptedRequestIds,
            route,
            LastObservedEpoch);
    }

    private DomainResult<VehicleState> StalePlan() =>
        DomainResult<VehicleState>.Fail(
            VehicleFailureCodes.StalePlanVersion,
            "Lifecycle event does not match the active plan version.",
            Id.Value,
            "planVersion");

    private DomainResult<VehicleState> CapacityFailure(RequestId requestId) =>
        DomainResult<VehicleState>.Fail(
            VehicleFailureCodes.Capacity,
            "Boarding would exceed vehicle capacity.",
            requestId.Value,
            "capacity");
}

public static class VehicleFailureCodes
{
    public const string InvalidVehicleState = "INVALID_VEHICLE_STATE";
    public const string DuplicateRider = "DUPLICATE_RIDER";
    public const string StaleObservation = "STALE_VEHICLE_OBSERVATION";
    public const string ObservationConflict = "VEHICLE_OBSERVATION_CONFLICT";
    public const string StalePlanVersion = "STALE_PLAN_VERSION";
    public const string RiderStateMismatch = "RIDER_STATE_MISMATCH";
    public const string Capacity = "CAPACITY";
    public const string PositionMismatch = "POSITION_MISMATCH";
}
