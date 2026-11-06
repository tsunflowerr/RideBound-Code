using System.Collections.Frozen;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Vehicles;

namespace RideBound.Domain.Runs;

public sealed class RideBoundRun
{
    private RideBoundRun(
        RunIdentifier id,
        ScenarioIdentifier scenarioId,
        long appliedEpoch,
        SimTime simulationTime,
        IEnumerable<KeyValuePair<RequestId, RideRequest>> requests,
        IEnumerable<KeyValuePair<VehicleId, VehicleState>> vehicles)
    {
        Id = id;
        ScenarioId = scenarioId;
        AppliedEpoch = appliedEpoch;
        SimulationTime = simulationTime;
        Requests = requests.ToFrozenDictionary();
        Vehicles = vehicles.ToFrozenDictionary();
    }

    public RunIdentifier Id { get; }

    public ScenarioIdentifier ScenarioId { get; }

    public long AppliedEpoch { get; }

    public SimTime SimulationTime { get; }

    public IReadOnlyDictionary<RequestId, RideRequest> Requests { get; }

    public IReadOnlyDictionary<VehicleId, VehicleState> Vehicles { get; }

    public static RideBoundRun Create(
        RunIdentifier id,
        ScenarioIdentifier scenarioId,
        SimTime initialTime) =>
        new(id, scenarioId, 0, initialTime, [], []);

    public static DomainResult<RideBoundRun> Rehydrate(
        RunIdentifier id,
        ScenarioIdentifier scenarioId,
        long appliedEpoch,
        SimTime simulationTime,
        IEnumerable<RideRequest> requests,
        IEnumerable<VehicleState> vehicles)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(vehicles);
        var requestArray = requests.ToArray();
        var vehicleArray = vehicles.ToArray();

        if (appliedEpoch is < 0 or > DomainLimits.MaxCanonicalInteger
            || requestArray.Select(value => value.Id).Distinct().Count()
                != requestArray.Length
            || vehicleArray.Select(value => value.Id).Distinct().Count()
                != vehicleArray.Length)
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.InvalidEpoch,
                "Checkpoint run identity, epoch, or entity sets are invalid.",
                id.Value);
        }

        var requestMap = requestArray.ToDictionary(value => value.Id);
        var vehicleMap = vehicleArray.ToDictionary(value => value.Id);

        foreach (var request in requestArray.Where(
                     value => value.AssignedVehicleId is not null))
        {
            if (!vehicleMap.ContainsKey(request.AssignedVehicleId!.Value))
            {
                return DomainResult<RideBoundRun>.Fail(
                    RunFailureCodes.VehicleRiderMismatch,
                    "Checkpoint request assignment references an unknown vehicle.",
                    request.Id.Value,
                    "assignedVehicleId");
            }
        }

        foreach (var vehicle in vehicleArray)
        {
            long derivedLoad = 0;

            foreach (var stop in vehicle.Route.RemainingStops)
            {
                if (stop.RequestId is RequestId requestId
                    && (!requestMap.TryGetValue(requestId, out var request)
                        || !request.IsAcceptedActive
                        || request.AssignedVehicleId != vehicle.Id))
                {
                    return DomainResult<RideBoundRun>.Fail(
                        RunFailureCodes.VehicleRiderMismatch,
                        "Checkpoint remaining route stop has no active matching assignment.",
                        requestId.Value,
                        "routeStops");
                }
            }

            foreach (var requestId in vehicle.AcceptedRequestIds)
            {
                if (!requestMap.TryGetValue(requestId, out var request)
                    || !request.IsAcceptedActive
                    || request.AssignedVehicleId != vehicle.Id
                    || request.Lifecycle == RequestLifecycle.Onboard
                        != vehicle.OnboardRequestIds.Contains(requestId))
                {
                    return DomainResult<RideBoundRun>.Fail(
                        RunFailureCodes.VehicleRiderMismatch,
                        "Checkpoint vehicle rider sets disagree with request state.",
                        requestId.Value,
                        "acceptedRequestIds");
                }
            }

            foreach (var requestId in vehicle.OnboardRequestIds)
            {
                derivedLoad = checked(derivedLoad + requestMap[requestId].PartySize);
            }

            if (derivedLoad != vehicle.OccupiedSeats)
            {
                return DomainResult<RideBoundRun>.Fail(
                    RunFailureCodes.VehicleRiderMismatch,
                    "Checkpoint occupied seats disagree with onboard riders.",
                    vehicle.Id.Value,
                    "occupiedSeats");
            }
        }

        foreach (var request in requestArray.Where(value => value.IsAcceptedActive))
        {
            if (request.AssignedVehicleId is not VehicleId vehicleId
                || !vehicleMap.TryGetValue(vehicleId, out var vehicle)
                || !vehicle.AcceptedRequestIds.Contains(request.Id))
            {
                return DomainResult<RideBoundRun>.Fail(
                    RunFailureCodes.VehicleRiderMismatch,
                    "Checkpoint active request has no matching vehicle assignment.",
                    request.Id.Value,
                    "assignedVehicleId");
            }

            var remaining = vehicle.Route.RemainingStops.ToArray();
            var pickupCount = remaining.Count(
                stop => stop.RequestId == request.Id
                    && stop.Kind == RouteStopKind.Pickup);
            var dropCount = remaining.Count(
                stop => stop.RequestId == request.Id
                    && stop.Kind == RouteStopKind.DropOff);
            var expectedPickupCount = request.Lifecycle
                == RequestLifecycle.Onboard
                    ? 0
                    : 1;

            if (pickupCount != expectedPickupCount || dropCount != 1)
            {
                return DomainResult<RideBoundRun>.Fail(
                    RunFailureCodes.VehicleRiderMismatch,
                    "Checkpoint route does not preserve the active request stops.",
                    request.Id.Value,
                    "routeStops");
            }
        }

        return DomainResult<RideBoundRun>.Success(
            new RideBoundRun(
                id,
                scenarioId,
                appliedEpoch,
                simulationTime,
                requestMap,
                vehicleMap));
    }

    public DomainResult<RideBoundRun> AdvanceEpoch(long epoch, SimTime simulationTime)
    {
        if (epoch != AppliedEpoch + 1
            || simulationTime.Milliseconds < SimulationTime.Milliseconds)
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.InvalidEpoch,
                "Epoch must advance by one and simulation time cannot decrease.",
                Id.Value,
                "epoch");
        }

        return DomainResult<RideBoundRun>.Success(
            Copy(
                epoch,
                simulationTime,
                Requests,
                Vehicles));
    }

    public DomainResult<RideBoundRun> AddRequest(RideRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Requests.ContainsKey(request.Id))
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.DuplicateRequest,
                "Request ID already exists in the run.",
                request.Id.Value,
                "requestId");
        }

        return DomainResult<RideBoundRun>.Success(
            Copy(
                AppliedEpoch,
                SimulationTime,
                Requests.Append(
                    new KeyValuePair<RequestId, RideRequest>(request.Id, request)),
                Vehicles));
    }

    public DomainResult<RideBoundRun> BootstrapVehicle(VehicleState vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (AppliedEpoch != 0)
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.VehicleBootstrapOnly,
                "Unknown vehicles can only be bootstrapped in the first epoch.",
                vehicle.Id.Value,
                "vehicleId");
        }

        if (Vehicles.ContainsKey(vehicle.Id))
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.DuplicateVehicle,
                "Vehicle ID already exists in the run.",
                vehicle.Id.Value,
                "vehicleId");
        }

        foreach (var requestId in vehicle.AcceptedRequestIds)
        {
            if (!Requests.TryGetValue(requestId, out var request)
                || !request.IsAcceptedActive
                || request.AssignedVehicleId != vehicle.Id)
            {
                return DomainResult<RideBoundRun>.Fail(
                    RunFailureCodes.VehicleRiderMismatch,
                    "Bootstrap vehicle rider set has no matching request state.",
                    requestId.Value,
                    "acceptedRequestIds");
            }
        }

        return DomainResult<RideBoundRun>.Success(
            Copy(
                AppliedEpoch,
                SimulationTime,
                Requests,
                Vehicles.Append(
                    new KeyValuePair<VehicleId, VehicleState>(vehicle.Id, vehicle))));
    }

    public DomainResult<RideBoundRun> ObserveVehicle(VehicleState observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (!Vehicles.TryGetValue(observation.Id, out var current))
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.UnknownVehicle,
                "Vehicle ID is unknown after bootstrap.",
                observation.Id.Value,
                "vehicleId");
        }

        var observed = current.Observe(
            observation.Capacity,
            observation.OccupiedSeats,
            observation.Position,
            observation.OnboardRequestIds,
            observation.AcceptedRequestIds,
            observation.Route,
            observation.LastObservedEpoch);

        return observed.IsSuccess
            ? ReplaceVehicle(observed.Value!)
            : FromFailure(observed.Failure!);
    }

    public DomainResult<RideBoundRun> AcceptRequest(
        RequestId requestId,
        VehicleId vehicleId)
    {
        if (!TryGetRequestAndVehicle(
                requestId,
                vehicleId,
                out var request,
                out var vehicle,
                out var failure))
        {
            return failure!;
        }

        var accepted = request!.Accept(vehicleId);
        var assigned = vehicle!.Assign(requestId);

        if (!accepted.IsSuccess)
        {
            return FromFailure(accepted.Failure!);
        }

        if (!assigned.IsSuccess)
        {
            return FromFailure(assigned.Failure!);
        }

        return ReplaceBoth(accepted.Value!, assigned.Value!);
    }

    public DomainResult<RideBoundRun> ConfirmWaitingPickup(RequestId requestId)
    {
        if (!Requests.TryGetValue(requestId, out var request))
        {
            return UnknownRequest(requestId);
        }

        var confirmed = request.ConfirmWaitingPickup();
        return confirmed.IsSuccess
            ? ReplaceRequest(confirmed.Value!)
            : FromFailure(confirmed.Failure!);
    }

    public DomainResult<RideBoundRun> RejectRequest(RequestId requestId)
    {
        if (!Requests.TryGetValue(requestId, out var request))
        {
            return UnknownRequest(requestId);
        }

        var rejected = request.Reject();
        return rejected.IsSuccess
            ? ReplaceRequest(rejected.Value!)
            : FromFailure(rejected.Failure!);
    }

    public DomainResult<RideBoundRun> CancelBeforeAcceptance(RequestId requestId)
    {
        if (!Requests.TryGetValue(requestId, out var request))
        {
            return UnknownRequest(requestId);
        }

        var cancelled = request.CancelBeforeAcceptance();
        return cancelled.IsSuccess
            ? ReplaceRequest(cancelled.Value!)
            : FromFailure(cancelled.Failure!);
    }

    public DomainResult<RideBoundRun> CancelAfterAcceptance(RequestId requestId)
    {
        if (!Requests.TryGetValue(requestId, out var request))
        {
            return UnknownRequest(requestId);
        }

        if (request.AssignedVehicleId is not VehicleId vehicleId
            || !Vehicles.TryGetValue(vehicleId, out var vehicle))
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.VehicleRiderMismatch,
                "Accepted request has no matching vehicle state.",
                requestId.Value,
                "vehicleId");
        }

        var cancelled = request.CancelAfterAcceptance();
        var vehicleAfter = vehicle.CancelAccepted(requestId);

        if (!cancelled.IsSuccess)
        {
            return FromFailure(cancelled.Failure!);
        }

        if (!vehicleAfter.IsSuccess)
        {
            return FromFailure(vehicleAfter.Failure!);
        }

        return ReplaceBoth(cancelled.Value!, vehicleAfter.Value!);
    }

    public DomainResult<RideBoundRun> ReachStop(
        VehicleId vehicleId,
        StopId stopId,
        PlanVersion planVersion,
        NodePosition position,
        long observedEpoch)
    {
        if (!Vehicles.TryGetValue(vehicleId, out var vehicle))
        {
            return UnknownVehicle(vehicleId);
        }

        var reached = vehicle.ReachStop(
            stopId,
            planVersion,
            position,
            observedEpoch);
        return reached.IsSuccess
            ? ReplaceVehicle(reached.Value!)
            : FromFailure(reached.Failure!);
    }

    public DomainResult<RideBoundRun> Board(
        VehicleId vehicleId,
        RequestId requestId,
        PlanVersion planVersion,
        SimTime pickupTime,
        bool allowLatePickup = false)
    {
        if (!TryGetRequestAndVehicle(
                requestId,
                vehicleId,
                out var request,
                out var vehicle,
                out var failure))
        {
            return failure!;
        }

        var boardedRequest = request!.Board(vehicleId, pickupTime, allowLatePickup);
        var boardedVehicle = vehicle!.Board(
            requestId,
            request.PartySize,
            planVersion);

        if (!boardedRequest.IsSuccess)
        {
            return FromFailure(boardedRequest.Failure!);
        }

        if (!boardedVehicle.IsSuccess)
        {
            return FromFailure(boardedVehicle.Failure!);
        }

        return ReplaceBoth(boardedRequest.Value!, boardedVehicle.Value!);
    }

    public DomainResult<RideBoundRun> Alight(
        VehicleId vehicleId,
        RequestId requestId,
        PlanVersion planVersion)
    {
        if (!TryGetRequestAndVehicle(
                requestId,
                vehicleId,
                out var request,
                out var vehicle,
                out var failure))
        {
            return failure!;
        }

        var completedRequest = request!.Complete(vehicleId);
        var alightedVehicle = vehicle!.Alight(
            requestId,
            request.PartySize,
            planVersion);

        if (!completedRequest.IsSuccess)
        {
            return FromFailure(completedRequest.Failure!);
        }

        if (!alightedVehicle.IsSuccess)
        {
            return FromFailure(alightedVehicle.Failure!);
        }

        return ReplaceBoth(completedRequest.Value!, alightedVehicle.Value!);
    }

    public DomainResult<RideBoundRun> UpdateVehicleRoute(
        VehicleId vehicleId,
        Routes.RoutePlan route)
    {
        if (!Vehicles.TryGetValue(vehicleId, out var vehicle))
        {
            return UnknownVehicle(vehicleId);
        }

        var updated = vehicle.UpdateRoute(route);
        return updated.IsSuccess
            ? ReplaceVehicle(updated.Value!)
            : FromFailure(updated.Failure!);
    }

    /// <summary>
    /// Moves one waiting rider from its vehicle (donor) to <paramref name="receiverVehicleId"/>
    /// in one step: the request's assignment and both accepted sets change together. Both
    /// routes must already be applied: the donor route has no remaining stop of the rider and
    /// the receiver's mutable suffix has its pickup before its drop, once each.
    /// </summary>
    public DomainResult<RideBoundRun> ReassignWaitingRequest(
        RequestId requestId,
        VehicleId receiverVehicleId)
    {
        if (!TryGetRequestAndVehicle(
                requestId,
                receiverVehicleId,
                out var request,
                out var receiver,
                out var failure))
        {
            return failure!;
        }

        var reassigned = request!.ReassignWaiting(receiverVehicleId);

        if (!reassigned.IsSuccess)
        {
            return FromFailure(reassigned.Failure!);
        }

        if (request.AssignedVehicleId is not VehicleId donorId
            || !Vehicles.TryGetValue(donorId, out var donor))
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.VehicleRiderMismatch,
                "Waiting request has no matching donor vehicle state.",
                requestId.Value,
                "assignedVehicleId");
        }

        var suffix = receiver!.Route.MutableSuffix;
        var pickupIndexes = Enumerable.Range(0, suffix.Count)
            .Where(index => suffix[index].RequestId == requestId
                && suffix[index].Kind == RouteStopKind.Pickup)
            .ToArray();
        var dropIndexes = Enumerable.Range(0, suffix.Count)
            .Where(index => suffix[index].RequestId == requestId
                && suffix[index].Kind == RouteStopKind.DropOff)
            .ToArray();

        if (pickupIndexes.Length != 1
            || dropIndexes.Length != 1
            || pickupIndexes[0] > dropIndexes[0]
            || receiver.Route.RemainingStops.Count(stop => stop.RequestId == requestId) != 2)
        {
            return DomainResult<RideBoundRun>.Fail(
                RunFailureCodes.VehicleRiderMismatch,
                "Receiver route must carry the relocated request's pickup before its drop, once each.",
                requestId.Value,
                "routeStops");
        }

        var released = donor.Release(requestId);

        if (!released.IsSuccess)
        {
            return FromFailure(released.Failure!);
        }

        var assigned = receiver.Assign(requestId);

        if (!assigned.IsSuccess)
        {
            return FromFailure(assigned.Failure!);
        }

        return DomainResult<RideBoundRun>.Success(
            Copy(
                AppliedEpoch,
                SimulationTime,
                Requests.Select(
                    pair => pair.Key == requestId
                        ? new KeyValuePair<RequestId, RideRequest>(
                            requestId,
                            reassigned.Value!)
                        : pair),
                Vehicles.Select(
                    pair => pair.Key == donorId
                        ? new KeyValuePair<VehicleId, VehicleState>(
                            donorId,
                            released.Value!)
                        : pair.Key == receiverVehicleId
                            ? new KeyValuePair<VehicleId, VehicleState>(
                                receiverVehicleId,
                                assigned.Value!)
                            : pair)));
    }

    private bool TryGetRequestAndVehicle(
        RequestId requestId,
        VehicleId vehicleId,
        out RideRequest? request,
        out VehicleState? vehicle,
        out DomainResult<RideBoundRun>? failure)
    {
        if (!Requests.TryGetValue(requestId, out request))
        {
            vehicle = null;
            failure = UnknownRequest(requestId);
            return false;
        }

        if (!Vehicles.TryGetValue(vehicleId, out vehicle))
        {
            failure = UnknownVehicle(vehicleId);
            return false;
        }

        failure = null;
        return true;
    }

    private DomainResult<RideBoundRun> ReplaceRequest(RideRequest request) =>
        DomainResult<RideBoundRun>.Success(
            Copy(
                AppliedEpoch,
                SimulationTime,
                Requests.Select(
                    pair => pair.Key == request.Id
                        ? new KeyValuePair<RequestId, RideRequest>(
                            request.Id,
                            request)
                        : pair),
                Vehicles));

    private DomainResult<RideBoundRun> ReplaceVehicle(VehicleState vehicle) =>
        DomainResult<RideBoundRun>.Success(
            Copy(
                AppliedEpoch,
                SimulationTime,
                Requests,
                Vehicles.Select(
                    pair => pair.Key == vehicle.Id
                        ? new KeyValuePair<VehicleId, VehicleState>(
                            vehicle.Id,
                            vehicle)
                        : pair)));

    private DomainResult<RideBoundRun> ReplaceBoth(
        RideRequest request,
        VehicleState vehicle) =>
        DomainResult<RideBoundRun>.Success(
            Copy(
                AppliedEpoch,
                SimulationTime,
                Requests.Select(
                    pair => pair.Key == request.Id
                        ? new KeyValuePair<RequestId, RideRequest>(
                            request.Id,
                            request)
                        : pair),
                Vehicles.Select(
                    pair => pair.Key == vehicle.Id
                        ? new KeyValuePair<VehicleId, VehicleState>(
                            vehicle.Id,
                            vehicle)
                        : pair)));

    private RideBoundRun Copy(
        long appliedEpoch,
        SimTime simulationTime,
        IEnumerable<KeyValuePair<RequestId, RideRequest>> requests,
        IEnumerable<KeyValuePair<VehicleId, VehicleState>> vehicles) =>
        new(Id, ScenarioId, appliedEpoch, simulationTime, requests, vehicles);

    private static DomainResult<RideBoundRun> FromFailure(DomainFailure failure) =>
        DomainResult<RideBoundRun>.Fail(
            failure.Code,
            failure.Message,
            failure.EntityId,
            failure.Dimension);

    private static DomainResult<RideBoundRun> UnknownRequest(RequestId requestId) =>
        DomainResult<RideBoundRun>.Fail(
            RunFailureCodes.UnknownRequest,
            "Request ID is unknown.",
            requestId.Value,
            "requestId");

    private static DomainResult<RideBoundRun> UnknownVehicle(VehicleId vehicleId) =>
        DomainResult<RideBoundRun>.Fail(
            RunFailureCodes.UnknownVehicle,
            "Vehicle ID is unknown.",
            vehicleId.Value,
            "vehicleId");
}

public static class RunFailureCodes
{
    public const string InvalidEpoch = "INVALID_EPOCH";
    public const string DuplicateRequest = "DUPLICATE_REQUEST";
    public const string DuplicateVehicle = "DUPLICATE_VEHICLE";
    public const string UnknownRequest = "UNKNOWN_REQUEST";
    public const string UnknownVehicle = "UNKNOWN_VEHICLE";
    public const string VehicleBootstrapOnly = "VEHICLE_BOOTSTRAP_ONLY";
    public const string VehicleRiderMismatch = "VEHICLE_RIDER_MISMATCH";
}
