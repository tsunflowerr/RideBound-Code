using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Validation;

namespace RideBound.Application.Promises;

public sealed record PromiseDeltaCalculationResult
{
    private PromiseDeltaCalculationResult(
        ThreeWayPromiseDelta? deltas,
        DomainFailure? failure)
    {
        Deltas = deltas;
        Failure = failure;
    }

    public bool IsSuccess => Deltas is not null;

    public ThreeWayPromiseDelta? Deltas { get; }

    public DomainFailure? Failure { get; }

    public static PromiseDeltaCalculationResult Success(
        ThreeWayPromiseDelta deltas) =>
        new(deltas, null);

    public static PromiseDeltaCalculationResult Fail(
        RequestId requestId,
        string message,
        string dimension) =>
        new(
            null,
            new DomainFailure(
                CommitmentFailureCodes.StopDistanceRequired,
                message,
                requestId.Value,
                dimension));
}

public sealed class PromiseDeltaCalculator
{
    public PromiseDeltaCalculationResult Calculate(
        PublishedPromise previous,
        PromiseProjection exogenous,
        PromiseProjection proposed,
        MaterialRevisionRule materialRule,
        IStopDistanceLookup stopDistances)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(exogenous);
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(materialRule);
        ArgumentNullException.ThrowIfNull(stopDistances);
        var requestId = previous.Projection.RequestId;

        if (exogenous.RequestId != requestId
            || proposed.RequestId != requestId)
        {
            throw new ArgumentException(
                "Three-way promise identities must match.");
        }

        var exogenousDelta = CalculatePair(
            previous.Projection,
            exogenous,
            materialRule,
            stopDistances);

        if (!exogenousDelta.IsSuccess)
        {
            return exogenousDelta;
        }

        var decisionDelta = CalculatePair(
            exogenous,
            proposed,
            materialRule,
            stopDistances);

        if (!decisionDelta.IsSuccess)
        {
            return decisionDelta;
        }

        var visibleDelta = CalculatePair(
            previous.Projection,
            proposed,
            materialRule,
            stopDistances);

        return visibleDelta.IsSuccess
            ? PromiseDeltaCalculationResult.Success(
                new ThreeWayPromiseDelta(
                    exogenousDelta.Deltas!.Visible,
                    decisionDelta.Deltas!.Visible,
                    visibleDelta.Deltas!.Visible))
            : visibleDelta;
    }

    private static PromiseDeltaCalculationResult CalculatePair(
        PromiseProjection before,
        PromiseProjection after,
        MaterialRevisionRule materialRule,
        IStopDistanceLookup stopDistances)
    {
        var pickupDistance = GetRelocation(
            before.RequestId,
            before.PickupNodeId,
            after.PickupNodeId,
            "pickup_stop_relocation_mm",
            stopDistances);

        if (!pickupDistance.IsSuccess)
        {
            return PromiseDeltaCalculationResult.Fail(
                before.RequestId,
                pickupDistance.Failure!.Message,
                pickupDistance.Failure.Dimension!);
        }

        var dropDistance = GetRelocation(
            before.RequestId,
            before.DropNodeId,
            after.DropNodeId,
            "drop_stop_relocation_mm",
            stopDistances);

        if (!dropDistance.IsSuccess)
        {
            return PromiseDeltaCalculationResult.Fail(
                before.RequestId,
                dropDistance.Failure!.Message,
                dropDistance.Failure.Dimension!);
        }

        var vector = new CommitmentVector(
            AbsoluteDifference(before.PickupEta, after.PickupEta),
            AbsoluteDifference(before.DropEta, after.DropEta),
            materialRule.IsMaterial(
                before.PickupEta,
                after.PickupEta,
                before.DropEta,
                after.DropEta)
                ? 1
                : 0,
            before.VehicleId == after.VehicleId ? 0 : 1,
            pickupDistance.Value!.Millimeters,
            before.PickupStopId == after.PickupStopId ? 0 : 1,
            dropDistance.Value!.Millimeters,
            before.DropStopId == after.DropStopId ? 0 : 1,
            CountIncumbentInversions(before, after),
            CountPrePickupInsertions(before, after));

        return PromiseDeltaCalculationResult.Success(
            new ThreeWayPromiseDelta(
                CommitmentVector.Zero,
                CommitmentVector.Zero,
                vector));
    }

    private static DomainResult<Relocation> GetRelocation(
        RequestId requestId,
        NodeId before,
        NodeId after,
        string dimension,
        IStopDistanceLookup distances)
    {
        if (before == after)
        {
            return DomainResult<Relocation>.Success(new Relocation(0));
        }

        if (!distances.TryGetDistanceMillimeters(
                before,
                after,
                out var distance)
            || distance is < 0 or > DomainLimits.MaxCanonicalInteger)
        {
            return DomainResult<Relocation>.Fail(
                CommitmentFailureCodes.StopDistanceRequired,
                "Changed promise stop has no canonical distance.",
                requestId.Value,
                dimension);
        }

        return DomainResult<Relocation>.Success(new Relocation(distance));
    }

    private static long CountIncumbentInversions(
        PromiseProjection before,
        PromiseProjection after)
    {
        var beforeOrder = ComparableIncumbents(before).ToArray();
        var afterOrder = ComparableIncumbents(after).ToArray();
        var common = beforeOrder.Intersect(afterOrder).ToHashSet();
        var left = beforeOrder.Where(common.Contains).ToArray();
        var rightPositions = afterOrder
            .Where(common.Contains)
            .Select((token, index) => (token, index))
            .ToDictionary(pair => pair.token, pair => pair.index);
        long inversions = 0;

        for (var first = 0; first < left.Length; first++)
        {
            for (var second = first + 1; second < left.Length; second++)
            {
                if (rightPositions[left[first]] > rightPositions[left[second]])
                {
                    inversions = checked(inversions + 1);
                }
            }
        }

        return inversions;
    }

    private static IEnumerable<(
        RequestId RequestId,
        RouteStopKind Kind,
        StopId StopId)>
        ComparableIncumbents(PromiseProjection promise) =>
        promise.ServiceOrder
            .Where(
                token => token.RequestId is not null
                    && token.RequestId != promise.RequestId)
            .Select(
                token => (
                    token.RequestId!.Value,
                    token.Kind,
                    token.StopId));

    private static long CountPrePickupInsertions(
        PromiseProjection before,
        PromiseProjection after)
    {
        var pickupIndex = after.ServiceOrder
            .Select((token, index) => (token, index))
            .Where(
                pair => pair.token.RequestId == after.RequestId
                    && pair.token.Kind == RouteStopKind.Pickup)
            .Select(pair => (int?)pair.index)
            .SingleOrDefault();

        if (pickupIndex is null)
        {
            return 0;
        }

        var oldStops = before.ServiceOrder
            .Select(token => token.StopId)
            .ToHashSet();

        return after.ServiceOrder
            .Take(pickupIndex.Value)
            .LongCount(token => !oldStops.Contains(token.StopId));
    }

    private static long AbsoluteDifference(SimTime left, SimTime right) =>
        left.Milliseconds >= right.Milliseconds
            ? left.Milliseconds - right.Milliseconds
            : right.Milliseconds - left.Milliseconds;

    private sealed record Relocation(long Millimeters);
}
