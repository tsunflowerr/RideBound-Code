using RideBound.Domain.Common;

namespace RideBound.Domain.Requests;

public enum RequestLifecycle
{
    Pending,
    Accepted,
    WaitingPickup,
    Onboard,
    Completed,
    Rejected,
    CancelledBeforeAcceptance,
    CancelledAfterAcceptance,
}

public sealed record RideRequest
{
    private RideRequest(
        RequestId id,
        SimTime arrivalTime,
        NodeId originNodeId,
        NodeId destinationNodeId,
        SimTime earliestPickup,
        SimTime latestPickup,
        Duration maxRideTime,
        long partySize,
        string serviceClass,
        string commitmentPolicyId,
        RequestLifecycle lifecycle,
        VehicleId? assignedVehicleId,
        SimTime? actualPickupTime)
    {
        Id = id;
        ArrivalTime = arrivalTime;
        OriginNodeId = originNodeId;
        DestinationNodeId = destinationNodeId;
        EarliestPickup = earliestPickup;
        LatestPickup = latestPickup;
        MaxRideTime = maxRideTime;
        PartySize = partySize;
        ServiceClass = serviceClass;
        CommitmentPolicyId = commitmentPolicyId;
        Lifecycle = lifecycle;
        AssignedVehicleId = assignedVehicleId;
        ActualPickupTime = actualPickupTime;
    }

    public RequestId Id { get; }

    public SimTime ArrivalTime { get; }

    public NodeId OriginNodeId { get; }

    public NodeId DestinationNodeId { get; }

    public SimTime EarliestPickup { get; }

    public SimTime LatestPickup { get; }

    public Duration MaxRideTime { get; }

    public long PartySize { get; }

    public string ServiceClass { get; }

    public string CommitmentPolicyId { get; }

    public RequestLifecycle Lifecycle { get; }

    public VehicleId? AssignedVehicleId { get; }

    public SimTime? ActualPickupTime { get; }

    public bool IsAcceptedActive =>
        Lifecycle is RequestLifecycle.Accepted
            or RequestLifecycle.WaitingPickup
            or RequestLifecycle.Onboard;

    public static DomainResult<RideRequest> CreatePending(
        RequestId id,
        SimTime arrivalTime,
        NodeId originNodeId,
        NodeId destinationNodeId,
        SimTime earliestPickup,
        SimTime latestPickup,
        Duration maxRideTime,
        long partySize,
        string serviceClass,
        string commitmentPolicyId)
    {
        if (originNodeId == destinationNodeId)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.InvalidRequest,
                "Request origin and destination must be distinct.",
                id.Value,
                "destinationNodeId");
        }

        if (arrivalTime.Milliseconds > earliestPickup.Milliseconds
            || earliestPickup.Milliseconds > latestPickup.Milliseconds)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.InvalidRequest,
                "Request times must satisfy arrival <= earliest <= latest.",
                id.Value,
                "pickupWindow");
        }

        if (maxRideTime.Milliseconds == 0
            || partySize is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.InvalidRequest,
                "Party size and maximum ride time must be positive.",
                id.Value);
        }

        string service;
        string policy;

        try
        {
            service = DomainIdentifier.Require(serviceClass, nameof(serviceClass));
            policy = DomainIdentifier.Require(
                commitmentPolicyId,
                nameof(commitmentPolicyId));
        }
        catch (ArgumentException error)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.InvalidRequest,
                error.Message,
                id.Value);
        }

        return DomainResult<RideRequest>.Success(
            new RideRequest(
                id,
                arrivalTime,
                originNodeId,
                destinationNodeId,
                earliestPickup,
                latestPickup,
                maxRideTime,
                partySize,
                service,
                policy,
                RequestLifecycle.Pending,
                null,
                null));
    }

    public static DomainResult<RideRequest> Rehydrate(
        RequestId id,
        SimTime arrivalTime,
        NodeId originNodeId,
        NodeId destinationNodeId,
        SimTime earliestPickup,
        SimTime latestPickup,
        Duration maxRideTime,
        long partySize,
        string serviceClass,
        string commitmentPolicyId,
        RequestLifecycle lifecycle,
        VehicleId? assignedVehicleId,
        SimTime? actualPickupTime,
        bool allowLatePickup = false)
    {
        var pending = CreatePending(
            id,
            arrivalTime,
            originNodeId,
            destinationNodeId,
            earliestPickup,
            latestPickup,
            maxRideTime,
            partySize,
            serviceClass,
            commitmentPolicyId);

        if (!pending.IsSuccess
            || !Enum.IsDefined(lifecycle)
            || (lifecycle is RequestLifecycle.Pending
                    or RequestLifecycle.Rejected
                    or RequestLifecycle.CancelledBeforeAcceptance)
                && (assignedVehicleId is not null || actualPickupTime is not null)
            || (lifecycle is RequestLifecycle.Accepted
                    or RequestLifecycle.WaitingPickup
                    or RequestLifecycle.CancelledAfterAcceptance)
                && (assignedVehicleId is null || actualPickupTime is not null)
            || (lifecycle is RequestLifecycle.Onboard
                    or RequestLifecycle.Completed)
                && (assignedVehicleId is null
                    || actualPickupTime is null
                    || actualPickupTime.Value.Milliseconds
                        < earliestPickup.Milliseconds
                    || !allowLatePickup
                        && actualPickupTime.Value.Milliseconds
                            > latestPickup.Milliseconds))
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.InvalidRequest,
                "Checkpoint request lifecycle fields are inconsistent.",
                id.Value,
                "lifecycle");
        }

        return DomainResult<RideRequest>.Success(
            new RideRequest(
                id,
                arrivalTime,
                originNodeId,
                destinationNodeId,
                earliestPickup,
                latestPickup,
                maxRideTime,
                partySize,
                serviceClass,
                commitmentPolicyId,
                lifecycle,
                assignedVehicleId,
                actualPickupTime));
    }

    public DomainResult<RideRequest> Accept(VehicleId vehicleId) =>
        Lifecycle == RequestLifecycle.Pending
            ? Success(RequestLifecycle.Accepted, vehicleId)
            : InvalidTransition(RequestLifecycle.Accepted);

    public DomainResult<RideRequest> ConfirmWaitingPickup() =>
        Lifecycle == RequestLifecycle.Accepted
            ? Success(RequestLifecycle.WaitingPickup, AssignedVehicleId)
            : InvalidTransition(RequestLifecycle.WaitingPickup);

    /// <summary>
    /// Moves a confirmed waiting rider to another vehicle (waiting-rider relocation). An
    /// offered, onboard or terminal rider never moves, and the lifecycle is unchanged.
    /// </summary>
    public DomainResult<RideRequest> ReassignWaiting(VehicleId receiverVehicleId)
    {
        if (Lifecycle != RequestLifecycle.WaitingPickup)
        {
            return InvalidTransition(RequestLifecycle.WaitingPickup);
        }

        if (AssignedVehicleId == receiverVehicleId)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.AssignmentMismatch,
                "A relocation must name a vehicle other than the current assignment.",
                Id.Value,
                "vehicleId");
        }

        return Success(RequestLifecycle.WaitingPickup, receiverVehicleId);
    }

    public DomainResult<RideRequest> Reject() =>
        Lifecycle == RequestLifecycle.Pending
            ? Success(RequestLifecycle.Rejected, null)
            : InvalidTransition(RequestLifecycle.Rejected);

    public DomainResult<RideRequest> CancelBeforeAcceptance() =>
        Lifecycle == RequestLifecycle.Pending
            ? Success(RequestLifecycle.CancelledBeforeAcceptance, null)
            : InvalidTransition(RequestLifecycle.CancelledBeforeAcceptance);

    public DomainResult<RideRequest> CancelAfterAcceptance() =>
        Lifecycle is RequestLifecycle.Accepted or RequestLifecycle.WaitingPickup
            ? Success(RequestLifecycle.CancelledAfterAcceptance, AssignedVehicleId)
            : InvalidTransition(RequestLifecycle.CancelledAfterAcceptance);

    /// <param name="allowLatePickup">
    /// ADR-076 (exploratory, off by default). When true, a boarding after the latest
    /// pickup is accepted as an observed fact; the accepted window is left unchanged, so
    /// the caller records the gap as a separate service violation. A boarding before the
    /// earliest pickup is always rejected.
    /// </param>
    public DomainResult<RideRequest> Board(
        VehicleId vehicleId,
        SimTime pickupTime,
        bool allowLatePickup = false)
    {
        if (Lifecycle != RequestLifecycle.WaitingPickup)
        {
            return InvalidTransition(RequestLifecycle.Onboard);
        }

        if (AssignedVehicleId != vehicleId)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.AssignmentMismatch,
                "Passenger boarded a vehicle other than the accepted assignment.",
                Id.Value,
                "vehicleId");
        }

        if (pickupTime.Milliseconds < EarliestPickup.Milliseconds
            || !allowLatePickup && pickupTime.Milliseconds > LatestPickup.Milliseconds)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.PickupTimeOutsideWindow,
                "Actual pickup time must remain inside the accepted pickup window.",
                Id.Value,
                "actualPickupTime");
        }

        return DomainResult<RideRequest>.Success(
            Copy(
                RequestLifecycle.Onboard,
                vehicleId,
                pickupTime));
    }

    public DomainResult<RideRequest> Complete(VehicleId vehicleId)
    {
        if (Lifecycle != RequestLifecycle.Onboard)
        {
            return InvalidTransition(RequestLifecycle.Completed);
        }

        if (AssignedVehicleId != vehicleId)
        {
            return DomainResult<RideRequest>.Fail(
                RequestFailureCodes.AssignmentMismatch,
                "Passenger alighted from a vehicle other than the accepted assignment.",
                Id.Value,
                "vehicleId");
        }

        return Success(RequestLifecycle.Completed, vehicleId);
    }

    private DomainResult<RideRequest> Success(
        RequestLifecycle lifecycle,
        VehicleId? vehicleId) =>
        DomainResult<RideRequest>.Success(Copy(lifecycle, vehicleId, ActualPickupTime));

    private DomainResult<RideRequest> InvalidTransition(RequestLifecycle target) =>
        DomainResult<RideRequest>.Fail(
            RequestFailureCodes.InvalidLifecycleTransition,
            $"Request lifecycle cannot transition from {Lifecycle} to {target}.",
            Id.Value,
            "lifecycle");

    private RideRequest Copy(
        RequestLifecycle lifecycle,
        VehicleId? vehicleId,
        SimTime? actualPickupTime) =>
        new(
            Id,
            ArrivalTime,
            OriginNodeId,
            DestinationNodeId,
            EarliestPickup,
            LatestPickup,
            MaxRideTime,
            PartySize,
            ServiceClass,
            CommitmentPolicyId,
            lifecycle,
            vehicleId,
            actualPickupTime);
}

public static class RequestFailureCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidLifecycleTransition =
        "INVALID_REQUEST_LIFECYCLE_TRANSITION";
    public const string AssignmentMismatch = "ASSIGNMENT_MISMATCH";
    public const string PickupTimeOutsideWindow =
        "PICKUP_TIME_OUTSIDE_WINDOW";
}
