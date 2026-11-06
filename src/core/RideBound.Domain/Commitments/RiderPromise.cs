using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Commitments;

public readonly record struct PromiseVersion
{
    public PromiseVersion(long value)
    {
        if (value is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = value;
    }

    public long Value { get; }

    public PromiseVersion Next()
    {
        if (Value == DomainLimits.MaxCanonicalInteger)
        {
            throw new OverflowException("Promise version cannot advance.");
        }

        return new PromiseVersion(Value + 1);
    }
}

public sealed record PromiseServiceToken
{
    public PromiseServiceToken(
        StopId stopId,
        RequestId? requestId,
        RouteStopKind kind)
    {
        if (kind == RouteStopKind.Waypoint && requestId is not null
            || kind != RouteStopKind.Waypoint && requestId is null)
        {
            throw new ArgumentException(
                "Promise service tokens must match route-stop request semantics.");
        }

        StopId = stopId;
        RequestId = requestId;
        Kind = kind;
    }

    public StopId StopId { get; }

    public RequestId? RequestId { get; }

    public RouteStopKind Kind { get; }
}

public sealed class PromiseProjection
{
    public PromiseProjection(
        RequestId requestId,
        VehicleId vehicleId,
        StopId pickupStopId,
        NodeId pickupNodeId,
        StopId dropStopId,
        NodeId dropNodeId,
        SimTime pickupEta,
        SimTime dropEta,
        IEnumerable<PromiseServiceToken> serviceOrder)
    {
        ArgumentNullException.ThrowIfNull(serviceOrder);
        var order = serviceOrder.ToArray();

        if (pickupStopId == dropStopId
            || pickupEta.Milliseconds > dropEta.Milliseconds
            || order.Select(value => value.StopId).Distinct().Count()
                != order.Length)
        {
            throw new ArgumentException(
                "Promise stops, ETA order and service order must be valid.");
        }

        var pickupTokens = order.Count(
            value => value.RequestId == requestId
                && value.Kind == RouteStopKind.Pickup);
        var dropTokens = order.Count(
            value => value.RequestId == requestId
                && value.Kind == RouteStopKind.DropOff);
        var pickupTokenMatches = order
            .Where(
                value => value.RequestId == requestId
                    && value.Kind == RouteStopKind.Pickup)
            .All(value => value.StopId == pickupStopId);
        var dropTokenMatches = order
            .Where(
                value => value.RequestId == requestId
                    && value.Kind == RouteStopKind.DropOff)
            .All(value => value.StopId == dropStopId);
        var pickupIndex = Array.FindIndex(
            order,
            value => value.RequestId == requestId
                && value.Kind == RouteStopKind.Pickup);
        var dropIndex = Array.FindIndex(
            order,
            value => value.RequestId == requestId
                && value.Kind == RouteStopKind.DropOff);

        if (pickupTokens > 1
            || dropTokens != 1
            || !pickupTokenMatches
            || !dropTokenMatches
            || pickupIndex >= dropIndex)
        {
            throw new ArgumentException(
                "Promise service order requires one drop and at most one pickup.");
        }

        RequestId = requestId;
        VehicleId = vehicleId;
        PickupStopId = pickupStopId;
        PickupNodeId = pickupNodeId;
        DropStopId = dropStopId;
        DropNodeId = dropNodeId;
        PickupEta = pickupEta;
        DropEta = dropEta;
        ServiceOrder = Array.AsReadOnly(order);
    }

    public RequestId RequestId { get; }

    public VehicleId VehicleId { get; }

    public StopId PickupStopId { get; }

    public NodeId PickupNodeId { get; }

    public StopId DropStopId { get; }

    public NodeId DropNodeId { get; }

    public SimTime PickupEta { get; }

    public SimTime DropEta { get; }

    public IReadOnlyList<PromiseServiceToken> ServiceOrder { get; }
}

public sealed record PublishedPromise
{
    public PublishedPromise(
        PromiseVersion version,
        long publishedEpoch,
        SimTime publishedAt,
        PromiseProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        if (publishedEpoch is < 1 or > DomainLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(nameof(publishedEpoch));
        }

        Version = version;
        PublishedEpoch = publishedEpoch;
        PublishedAt = publishedAt;
        Projection = projection;
    }

    public PromiseVersion Version { get; }

    public long PublishedEpoch { get; }

    public SimTime PublishedAt { get; }

    public PromiseProjection Projection { get; }
}

public sealed record ThreeWayPromiseDelta(
    CommitmentVector Exogenous,
    CommitmentVector DecisionInduced,
    CommitmentVector Visible);
