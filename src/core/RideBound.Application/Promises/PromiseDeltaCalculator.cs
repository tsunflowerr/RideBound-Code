using RideBound.Domain.Commitments;

namespace RideBound.Application.Promises;

public sealed class PromiseDeltaCalculator
{
    /// <summary>
    /// The three deltas of one rider: exogenous (previous promise to kept route), decision-induced (kept route to
    /// candidate) and visible (previous promise to candidate).
    /// </summary>
    public ThreeWayPromiseDelta Calculate(
        PublishedPromise previous,
        PromiseProjection exogenous,
        PromiseProjection proposed)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(exogenous);
        ArgumentNullException.ThrowIfNull(proposed);
        var requestId = previous.Projection.RequestId;

        if (exogenous.RequestId != requestId
            || proposed.RequestId != requestId)
        {
            throw new ArgumentException(
                "Three-way promise identities must match.");
        }

        return new ThreeWayPromiseDelta(
            CalculatePair(previous.Projection, exogenous),
            CalculatePair(exogenous, proposed),
            CalculatePair(previous.Projection, proposed));
    }

    private static CommitmentVector CalculatePair(
        PromiseProjection before,
        PromiseProjection after) =>
        new(
            AbsoluteDifference(before.PickupEta, after.PickupEta),
            AbsoluteDifference(before.DropEta, after.DropEta),
            before.VehicleId == after.VehicleId ? 0 : 1,
            before.PickupStopId == after.PickupStopId ? 0 : 1,
            before.DropStopId == after.DropStopId ? 0 : 1);

    private static long AbsoluteDifference(Domain.Common.SimTime left, Domain.Common.SimTime right) =>
        left.Milliseconds >= right.Milliseconds
            ? left.Milliseconds - right.Milliseconds
            : right.Milliseconds - left.Milliseconds;
}
