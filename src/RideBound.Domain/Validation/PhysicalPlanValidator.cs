using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Domain.Validation;

/// <param name="ServiceQuality">
/// Relaxation of the service-quality dimensions for this vehicle, obtained from
/// <see cref="PhysicalPlanValidator.ProbeServiceQuality"/>. <c>null</c> means the
/// published contractual bounds apply unchanged, which is the behaviour of every
/// call site that does not probe.
/// </param>
/// <param name="Relocation">
/// The one waiting rider this decision moves between vehicles (G2). The receiver may then
/// carry the donor's rider and the donor must drop it; <c>null</c> keeps every accepted rider
/// on its vehicle, which is the behaviour of every call site without relocation.
/// </param>
public sealed record PhysicalValidationContext(
    RideBoundRun State,
    VehicleId VehicleId,
    RoutePlan CandidateRoute,
    ITravelTimeLookup TravelTimes,
    SimTime EvaluationTime,
    ServiceQualityAllowance? ServiceQuality = null,
    WaitingRiderRelocation? Relocation = null);

public sealed record PhysicalViolationWitness(
    string Code,
    VehicleId VehicleId,
    string Message,
    RequestId? RequestId = null,
    StopId? StopId = null,
    string? Dimension = null,
    long? Expected = null,
    long? Actual = null);

public sealed record PhysicalValidationResult
{
    private PhysicalValidationResult(PhysicalViolationWitness? witness)
    {
        Witness = witness;
    }

    public bool IsFeasible => Witness is null;

    public PhysicalViolationWitness? Witness { get; }

    public static PhysicalValidationResult Feasible { get; } =
        new((PhysicalViolationWitness?)null);

    public static PhysicalValidationResult Infeasible(
        PhysicalViolationWitness witness) =>
        new(witness);
}

public sealed class PhysicalPlanValidator
{
    public PhysicalValidationResult Validate(PhysicalValidationContext context) =>
        Validate(context, observed: null);

    /// <summary>
    /// Projects a vehicle's <em>unchanged</em> active route under the current
    /// travel snapshot and reports every service-quality deadline it no longer
    /// meets. Nothing here is a decision: the route is the one already in force,
    /// so a breach it produces is exogenous by construction (ADR-045).
    ///
    /// <para>Structural violations are not relaxed. If the active route fails
    /// one, the probe fails closed and the caller must keep treating it as a
    /// defect.</para>
    /// </summary>
    public ServiceQualityProbeResult ProbeServiceQuality(
        RideBoundRun state,
        VehicleId vehicleId,
        ITravelTimeLookup travelTimes,
        SimTime evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(travelTimes);

        if (!state.Vehicles.TryGetValue(vehicleId, out var vehicle))
        {
            return ServiceQualityProbeResult.Failure(
                new PhysicalViolationWitness(
                    PhysicalViolationCodes.UnknownVehicle,
                    vehicleId,
                    "Service-quality probe references an unknown vehicle.",
                    Dimension: "vehicleId"));
        }

        var observed = new List<ServiceQualityBreach>();
        var result = Validate(
            new PhysicalValidationContext(
                state,
                vehicleId,
                vehicle.Route,
                travelTimes,
                evaluationTime),
            observed);

        return result.IsFeasible
            ? ServiceQualityProbeResult.Success(
                ServiceQualityAllowance.FromBreaches(observed))
            : ServiceQualityProbeResult.Failure(result.Witness!);
    }

    /// <summary>
    /// Validates a candidate route under the vehicle's own exogenous relief
    /// (ADR-045): the probe is derived from the unchanged active route, so the
    /// relaxation admits exactly the deadlines traffic has already broken and
    /// nothing the candidate breaks itself. Call sites that re-check a route the
    /// generator produced must use this, otherwise a candidate the generator
    /// legitimately kept would be rejected downstream.
    /// </summary>
    /// <param name="relocation">
    /// The decision's waiting-rider relocation, if any. The relief still comes from this
    /// vehicle's own active route, so a rider moved onto a receiver gets none there.
    /// </param>
    public PhysicalValidationResult ValidateWithExogenousRelief(
        RideBoundRun state,
        VehicleId vehicleId,
        RoutePlan candidateRoute,
        ITravelTimeLookup travelTimes,
        SimTime evaluationTime,
        WaitingRiderRelocation? relocation = null)
    {
        var probe = ProbeServiceQuality(
            state,
            vehicleId,
            travelTimes,
            evaluationTime);

        return probe.IsSuccess
            ? Validate(
                new PhysicalValidationContext(
                    state,
                    vehicleId,
                    candidateRoute,
                    travelTimes,
                    evaluationTime,
                    probe.Allowance,
                    relocation))
            : PhysicalValidationResult.Infeasible(probe.Witness!);
    }

    private PhysicalValidationResult Validate(
        PhysicalValidationContext context,
        List<ServiceQualityBreach>? observed)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.State);
        ArgumentNullException.ThrowIfNull(context.CandidateRoute);
        ArgumentNullException.ThrowIfNull(context.TravelTimes);

        if (!context.State.Vehicles.TryGetValue(
                context.VehicleId,
                out var vehicle))
        {
            return Fail(
                PhysicalViolationCodes.UnknownVehicle,
                context.VehicleId,
                "Candidate references an unknown vehicle.",
                dimension: "vehicleId");
        }

        var versionResult = ValidatePlanVersion(vehicle, context.CandidateRoute);

        if (versionResult is not null)
        {
            return versionResult;
        }

        if (!vehicle.Route.HasExactFrozenPrefix(context.CandidateRoute))
        {
            return Fail(
                PhysicalViolationCodes.FrozenPrefix,
                vehicle.Id,
                "Candidate changed the exact executed/locked frozen prefix.",
                dimension: "frozenPrefix");
        }

        var schedule = EvaluateSchedule(context, vehicle, observed);

        if (schedule is not null)
        {
            return schedule;
        }

        return ValidateAcceptedPreservation(context, vehicle);
    }

    private static PhysicalValidationResult? ValidatePlanVersion(
        VehicleState vehicle,
        RoutePlan candidate)
    {
        var sameContent =
            vehicle.Route.ExecutedStopCount == candidate.ExecutedStopCount
            && vehicle.Route.FrozenPrefix.SequenceEqual(candidate.FrozenPrefix)
            && vehicle.Route.MutableSuffix.SequenceEqual(candidate.MutableSuffix);
        var expected = sameContent
            ? vehicle.Route.Version.Value
            : vehicle.Route.Version.Value + 1;

        if (candidate.Version.Value != expected)
        {
            return Fail(
                PhysicalViolationCodes.PlanVersion,
                vehicle.Id,
                sameContent
                    ? "No-op candidate must keep the active plan version."
                    : "Changed candidate must advance planVersion by exactly one.",
                dimension: "planVersion",
                expected: expected,
                actual: candidate.Version.Value);
        }

        return null;
    }

    private static PhysicalValidationResult? EvaluateSchedule(
        PhysicalValidationContext context,
        VehicleState vehicle,
        List<ServiceQualityBreach>? observed)
    {
        var onboard = new HashSet<RequestId>(vehicle.OnboardRequestIds);
        var pickedUp = new HashSet<RequestId>();
        var droppedOff = new HashSet<RequestId>();
        var pickupTimes = new Dictionary<RequestId, SimTime>();
        var load = vehicle.OccupiedSeats;
        var time = context.EvaluationTime;
        NodeId currentNode;
        long derivedInitialLoad = 0;

        foreach (var requestId in vehicle.OnboardRequestIds.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            if (!context.State.Requests.TryGetValue(requestId, out var request)
                || request.Lifecycle != RequestLifecycle.Onboard
                || request.AssignedVehicleId != vehicle.Id
                || request.ActualPickupTime is null)
            {
                return Fail(
                    PhysicalViolationCodes.OnboardPreservation,
                    vehicle.Id,
                    "Vehicle onboard set has no matching onboard request state.",
                    requestId,
                    dimension: "onboardRequestIds");
            }

            try
            {
                derivedInitialLoad = checked(
                    derivedInitialLoad + request.PartySize);
            }
            catch (OverflowException)
            {
                return Fail(
                    PhysicalViolationCodes.Capacity,
                    vehicle.Id,
                    "Derived onboard load overflowed.",
                    requestId,
                    dimension: "occupiedSeats");
            }
        }

        if (derivedInitialLoad != vehicle.OccupiedSeats)
        {
            return Fail(
                PhysicalViolationCodes.Capacity,
                vehicle.Id,
                "occupiedSeats does not equal the sum of onboard party sizes.",
                dimension: "occupiedSeats",
                expected: derivedInitialLoad,
                actual: vehicle.OccupiedSeats);
        }

        if (vehicle.Position is NodePosition node)
        {
            currentNode = node.NodeId;
        }
        else if (vehicle.Position is EdgeProgressPosition edge)
        {
            if (!context.TravelTimes.TryGetTravelTime(
                    edge.FromNodeId,
                    edge.ToNodeId,
                    out var fullEdgeTime))
            {
                return Fail(
                    PhysicalViolationCodes.RouteConnectivity,
                    vehicle.Id,
                    "Travel snapshot has no directed arc for current edge progress.",
                    dimension: "routeConnectivity");
            }

            Duration remainingDuration;

            try
            {
                var remaining = DivideRoundUp(
                    checked(
                        fullEdgeTime.Milliseconds
                        * (1000 - edge.ProgressPermille)),
                    1000);
                remainingDuration = new Duration(remaining);
            }
            catch (Exception error) when (
                error is OverflowException or ArgumentOutOfRangeException)
            {
                return Overflow(vehicle.Id);
            }

            var advanced = TryAdvance(time, remainingDuration);

            if (advanced is null)
            {
                return Overflow(vehicle.Id);
            }

            time = advanced.Value;
            currentNode = edge.ToNodeId;
        }
        else
        {
            return Fail(
                PhysicalViolationCodes.InvalidPosition,
                vehicle.Id,
                "Vehicle position type is unsupported.",
                dimension: "position");
        }

        foreach (var stop in context.CandidateRoute.RemainingStops)
        {
            if (!context.TravelTimes.TryGetTravelTime(
                    currentNode,
                    stop.NodeId,
                    out var travelTime))
            {
                return Fail(
                    PhysicalViolationCodes.RouteConnectivity,
                    vehicle.Id,
                    $"Travel snapshot has no directed arc '{currentNode}->{stop.NodeId}'.",
                    stopId: stop.StopId,
                    dimension: "routeConnectivity");
            }

            var arrival = TryAdvance(time, travelTime);

            if (arrival is null)
            {
                return Overflow(vehicle.Id, stop.StopId);
            }

            time = arrival.Value;
            var stopResult = stop.Kind switch
            {
                RouteStopKind.Waypoint => null,
                RouteStopKind.Pickup => ValidatePickup(
                    context,
                    vehicle,
                    stop,
                    ref time,
                    ref load,
                    onboard,
                    pickedUp,
                    droppedOff,
                    pickupTimes,
                    observed),
                RouteStopKind.DropOff => ValidateDropOff(
                    context,
                    vehicle,
                    stop,
                    time,
                    ref load,
                    onboard,
                    pickedUp,
                    droppedOff,
                    pickupTimes,
                    observed),
                _ => Fail(
                    PhysicalViolationCodes.InvalidRouteStop,
                    vehicle.Id,
                    "Route contains an unknown stop kind.",
                    stopId: stop.StopId,
                    dimension: "stopKind"),
            };

            if (stopResult is not null)
            {
                return stopResult;
            }

            var departed = TryAdvance(time, stop.ServiceDuration);

            if (departed is null)
            {
                return Overflow(vehicle.Id, stop.StopId);
            }

            time = departed.Value;
            currentNode = stop.NodeId;
        }

        foreach (var requestId in vehicle.OnboardRequestIds.OrderBy(
                     value => value.Value,
                     StringComparer.Ordinal))
        {
            if (!droppedOff.Contains(requestId))
            {
                return Fail(
                    PhysicalViolationCodes.OnboardPreservation,
                    vehicle.Id,
                    "Candidate removed the remaining drop-off of an onboard request.",
                    requestId,
                    dimension: "onboardDropOff");
            }
        }

        return null;
    }

    private static PhysicalValidationResult? ValidatePickup(
        PhysicalValidationContext context,
        VehicleState vehicle,
        RouteStop stop,
        ref SimTime time,
        ref long load,
        IReadOnlySet<RequestId> onboard,
        ISet<RequestId> pickedUp,
        IReadOnlySet<RequestId> droppedOff,
        IDictionary<RequestId, SimTime> pickupTimes,
        List<ServiceQualityBreach>? observed)
    {
        var requestResult = ResolveRequest(context, vehicle, stop);

        if (requestResult.Result is not null)
        {
            return requestResult.Result;
        }

        var request = requestResult.Request!;

        if (stop.NodeId != request.OriginNodeId)
        {
            return Fail(
                PhysicalViolationCodes.StopLocation,
                vehicle.Id,
                "Pickup stop does not match the request origin.",
                request.Id,
                stop.StopId,
                "originNodeId");
        }

        if (onboard.Contains(request.Id)
            || pickedUp.Contains(request.Id)
            || droppedOff.Contains(request.Id)
            || request.Lifecycle is RequestLifecycle.Completed
                or RequestLifecycle.CancelledBeforeAcceptance
                or RequestLifecycle.CancelledAfterAcceptance
                or RequestLifecycle.Rejected)
        {
            return Fail(
                PhysicalViolationCodes.Precedence,
                vehicle.Id,
                "Pickup is duplicated or follows a terminal/onboard state.",
                request.Id,
                stop.StopId,
                "precedence");
        }

        if (observed is not null)
        {
            // Probe pass: the route is the one already in force, so a missed
            // window here is exogenous. Record it and keep going rather than
            // deleting the safety no-op.
            if (time.Milliseconds > request.LatestPickup.Milliseconds)
            {
                observed.Add(
                    new ServiceQualityBreach(
                        request.Id,
                        PhysicalViolationCodes.PickupWindow,
                        "latestPickupMs",
                        request.LatestPickup.Milliseconds,
                        time.Milliseconds));
            }
        }
        else
        {
            var bound = context.ServiceQuality?.LatestPickupBound(
                    request.Id,
                    request.LatestPickup.Milliseconds)
                ?? request.LatestPickup.Milliseconds;

            if (time.Milliseconds > bound)
            {
                return Fail(
                    PhysicalViolationCodes.PickupWindow,
                    vehicle.Id,
                    "Pickup arrival is later than the request window.",
                    request.Id,
                    stop.StopId,
                    "latestPickupMs",
                    bound,
                    time.Milliseconds);
            }
        }

        if (time.Milliseconds < request.EarliestPickup.Milliseconds)
        {
            time = request.EarliestPickup;
        }

        try
        {
            load = checked(load + request.PartySize);
        }
        catch (OverflowException)
        {
            return Fail(
                PhysicalViolationCodes.Capacity,
                vehicle.Id,
                "Vehicle load overflowed while applying pickup.",
                request.Id,
                stop.StopId,
                "capacity");
        }

        if (load > vehicle.Capacity)
        {
            return Fail(
                PhysicalViolationCodes.Capacity,
                vehicle.Id,
                "Pickup would exceed vehicle capacity.",
                request.Id,
                stop.StopId,
                "capacity",
                vehicle.Capacity,
                load);
        }

        pickedUp.Add(request.Id);
        pickupTimes.Add(request.Id, time);
        return null;
    }

    private static PhysicalValidationResult? ValidateDropOff(
        PhysicalValidationContext context,
        VehicleState vehicle,
        RouteStop stop,
        SimTime time,
        ref long load,
        IReadOnlySet<RequestId> onboard,
        IReadOnlySet<RequestId> pickedUp,
        ISet<RequestId> droppedOff,
        IReadOnlyDictionary<RequestId, SimTime> pickupTimes,
        List<ServiceQualityBreach>? observed)
    {
        var requestResult = ResolveRequest(context, vehicle, stop);

        if (requestResult.Result is not null)
        {
            return requestResult.Result;
        }

        var request = requestResult.Request!;

        if (stop.NodeId != request.DestinationNodeId)
        {
            return Fail(
                PhysicalViolationCodes.StopLocation,
                vehicle.Id,
                "Drop-off stop does not match the request destination.",
                request.Id,
                stop.StopId,
                "destinationNodeId");
        }

        if (droppedOff.Contains(request.Id)
            || !onboard.Contains(request.Id) && !pickedUp.Contains(request.Id))
        {
            return Fail(
                PhysicalViolationCodes.Precedence,
                vehicle.Id,
                "Drop-off must follow pickup or an onboard initial state.",
                request.Id,
                stop.StopId,
                "precedence");
        }

        var pickupTime = onboard.Contains(request.Id)
            ? request.ActualPickupTime
            : pickupTimes.GetValueOrDefault(request.Id);

        if (pickupTime is null)
        {
            return Fail(
                PhysicalViolationCodes.OnboardPreservation,
                vehicle.Id,
                "Onboard request has no recorded actual pickup time.",
                request.Id,
                stop.StopId,
                "actualPickupTime");
        }

        var rideTime = time.Milliseconds - pickupTime.Value.Milliseconds;

        // A negative ride time is a precedence defect, not traffic, and is never
        // relaxed or observed.
        if (rideTime < 0)
        {
            return Fail(
                PhysicalViolationCodes.MaxRideTime,
                vehicle.Id,
                "Drop-off precedes the recorded pickup.",
                request.Id,
                stop.StopId,
                "maxRideTimeMs",
                request.MaxRideTime.Milliseconds,
                rideTime);
        }

        if (observed is not null)
        {
            if (rideTime > request.MaxRideTime.Milliseconds)
            {
                observed.Add(
                    new ServiceQualityBreach(
                        request.Id,
                        PhysicalViolationCodes.MaxRideTime,
                        "maxRideTimeMs",
                        request.MaxRideTime.Milliseconds,
                        rideTime));
            }
        }
        else
        {
            var bound = context.ServiceQuality?.MaxRideTimeBound(
                    request.Id,
                    request.MaxRideTime.Milliseconds)
                ?? request.MaxRideTime.Milliseconds;

            if (rideTime > bound)
            {
                return Fail(
                    PhysicalViolationCodes.MaxRideTime,
                    vehicle.Id,
                    "Drop-off exceeds maximum ride time.",
                    request.Id,
                    stop.StopId,
                    "maxRideTimeMs",
                    bound,
                    rideTime);
            }
        }

        load -= request.PartySize;

        if (load < 0)
        {
            return Fail(
                PhysicalViolationCodes.Capacity,
                vehicle.Id,
                "Drop-off would make vehicle load negative.",
                request.Id,
                stop.StopId,
                "capacity",
                0,
                load);
        }

        droppedOff.Add(request.Id);
        return null;
    }

    private static RequestResolution ResolveRequest(
        PhysicalValidationContext context,
        VehicleState vehicle,
        RouteStop stop)
    {
        if (stop.RequestId is not RequestId requestId
            || !context.State.Requests.TryGetValue(requestId, out var request))
        {
            return RequestResolution.Fail(
                Fail(
                    PhysicalViolationCodes.UnknownRequest,
                    vehicle.Id,
                    "Route stop references an unknown request.",
                    stop.RequestId,
                    stop.StopId,
                    "requestId"));
        }

        if (request.IsAcceptedActive
            && request.AssignedVehicleId is VehicleId assigned
            && assigned != vehicle.Id
            && !IsDeclaredRelocation(context.Relocation, request, assigned, vehicle.Id))
        {
            return RequestResolution.Fail(
                Fail(
                    PhysicalViolationCodes.Reassignment,
                    vehicle.Id,
                    "Candidate moves an accepted incumbent to another vehicle.",
                    request.Id,
                    stop.StopId,
                    "vehicleId"));
        }

        return RequestResolution.Success(request);
    }

    /// <summary>
    /// True only for the declared relocation itself: the waiting rider, still assigned to the
    /// declared donor in the validated state, carried by the declared receiver.
    /// </summary>
    private static bool IsDeclaredRelocation(
        WaitingRiderRelocation? relocation,
        RideRequest request,
        VehicleId assigned,
        VehicleId receiver) =>
        relocation is not null
        && relocation.RequestId == request.Id
        && relocation.DonorVehicleId == assigned
        && relocation.ReceiverVehicleId == receiver
        && request.Lifecycle == RequestLifecycle.WaitingPickup;

    private static PhysicalValidationResult ValidateAcceptedPreservation(
        PhysicalValidationContext context,
        VehicleState vehicle)
    {
        var remaining = context.CandidateRoute.RemainingStops.ToArray();

        if (context.Relocation is { } relocation
            && relocation.ReceiverVehicleId == vehicle.Id
            && context.State.Requests.TryGetValue(relocation.RequestId, out var moved)
            && IsDeclaredRelocation(relocation, moved, relocation.DonorVehicleId, vehicle.Id)
            && moved.AssignedVehicleId == relocation.DonorVehicleId
            && (remaining.Count(stop => stop.Kind == RouteStopKind.Pickup
                    && stop.RequestId == moved.Id) != 1
                || remaining.Count(stop => stop.Kind == RouteStopKind.DropOff
                    && stop.RequestId == moved.Id) != 1))
        {
            return Fail(
                PhysicalViolationCodes.AcceptedPreservation,
                vehicle.Id,
                "The receiver route must carry the relocated request's pickup and drop once each.",
                moved.Id,
                dimension: "routeStops");
        }

        foreach (var request in context.State.Requests.Values
                     .Where(
                         value => value.IsAcceptedActive
                             && value.AssignedVehicleId == vehicle.Id)
                     .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            var pickupCount = remaining.Count(
                stop => stop.Kind == RouteStopKind.Pickup
                    && stop.RequestId == request.Id);
            var dropCount = remaining.Count(
                stop => stop.Kind == RouteStopKind.DropOff
                    && stop.RequestId == request.Id);

            // The declared relocated rider leaves the donor completely.
            if (context.Relocation is { } leaving
                && leaving.DonorVehicleId == vehicle.Id
                && leaving.RequestId == request.Id
                && request.Lifecycle == RequestLifecycle.WaitingPickup)
            {
                if (pickupCount != 0 || dropCount != 0)
                {
                    return Fail(
                        PhysicalViolationCodes.AcceptedPreservation,
                        vehicle.Id,
                        "A relocated request must have no stop left on the donor route.",
                        request.Id,
                        dimension: "routeStops");
                }

                continue;
            }

            var preserved = request.Lifecycle == RequestLifecycle.Onboard
                ? pickupCount == 0 && dropCount == 1
                : pickupCount == 1 && dropCount == 1;

            if (!preserved)
            {
                return Fail(
                    request.Lifecycle == RequestLifecycle.Onboard
                        ? PhysicalViolationCodes.OnboardPreservation
                        : PhysicalViolationCodes.AcceptedPreservation,
                    vehicle.Id,
                    "Candidate does not preserve every required incumbent stop.",
                    request.Id,
                    dimension: "routeStops");
            }

            if (!vehicle.AcceptedRequestIds.Contains(request.Id))
            {
                return Fail(
                    PhysicalViolationCodes.AcceptedPreservation,
                    vehicle.Id,
                    "Vehicle accepted set does not contain the assigned request.",
                    request.Id,
                    dimension: "acceptedRequestIds");
            }

            if (request.Lifecycle == RequestLifecycle.Onboard
                != vehicle.OnboardRequestIds.Contains(request.Id))
            {
                return Fail(
                    PhysicalViolationCodes.OnboardPreservation,
                    vehicle.Id,
                    "Request lifecycle and vehicle onboard set disagree.",
                    request.Id,
                    dimension: "onboardRequestIds");
            }
        }

        return PhysicalValidationResult.Feasible;
    }

    private static SimTime? TryAdvance(SimTime time, Duration duration)
    {
        try
        {
            return time + duration;
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static long DivideRoundUp(long value, long divisor) =>
        value / divisor + (value % divisor == 0 ? 0 : 1);

    private static PhysicalValidationResult Overflow(
        VehicleId vehicleId,
        StopId? stopId = null) =>
        Fail(
            PhysicalViolationCodes.ScheduleOverflow,
            vehicleId,
            "Physical schedule exceeds the canonical time range.",
            stopId: stopId,
            dimension: "simTimeMs");

    private static PhysicalValidationResult Fail(
        string code,
        VehicleId vehicleId,
        string message,
        RequestId? requestId = null,
        StopId? stopId = null,
        string? dimension = null,
        long? expected = null,
        long? actual = null) =>
        PhysicalValidationResult.Infeasible(
            new PhysicalViolationWitness(
                code,
                vehicleId,
                message,
                requestId,
                stopId,
                dimension,
                expected,
                actual));

    private sealed record RequestResolution(
        RideRequest? Request,
        PhysicalValidationResult? Result)
    {
        public static RequestResolution Success(RideRequest request) =>
            new(request, null);

        public static RequestResolution Fail(PhysicalValidationResult result) =>
            new(null, result);
    }
}

public static class PhysicalViolationCodes
{
    public const string UnknownVehicle = "UNKNOWN_VEHICLE";
    public const string UnknownRequest = "UNKNOWN_REQUEST";
    public const string PlanVersion = "PLAN_VERSION";
    public const string FrozenPrefix = "FROZEN_PREFIX";
    public const string RouteConnectivity = "ROUTE_CONNECTIVITY";
    public const string InvalidPosition = "INVALID_POSITION";
    public const string InvalidRouteStop = "INVALID_ROUTE_STOP";
    public const string StopLocation = "STOP_LOCATION";
    public const string Precedence = "PRECEDENCE";
    public const string Capacity = "CAPACITY";
    public const string PickupWindow = "PICKUP_WINDOW";
    public const string MaxRideTime = "MAX_RIDE_TIME";
    public const string OnboardPreservation = "ONBOARD_PRESERVATION";
    public const string AcceptedPreservation = "ACCEPTED_PRESERVATION";
    public const string Reassignment = "REASSIGNMENT";
    public const string ScheduleOverflow = "SCHEDULE_OVERFLOW";
}
