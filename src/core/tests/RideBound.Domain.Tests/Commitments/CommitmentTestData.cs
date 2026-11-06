using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Commitments;

internal static class CommitmentTestData
{
    public static PromiseProjection Projection(
        VehicleId? vehicleId = null,
        StopId? pickupStopId = null,
        NodeId? pickupNodeId = null,
        StopId? dropStopId = null,
        NodeId? dropNodeId = null,
        long pickupEta = 10_000,
        long dropEta = 20_000,
        IEnumerable<PromiseServiceToken>? order = null)
    {
        var pickup = pickupStopId ?? new StopId("pickup");
        var drop = dropStopId ?? new StopId("drop");

        return new PromiseProjection(
            TestData.RequestOne,
            vehicleId ?? TestData.VehicleOne,
            pickup,
            pickupNodeId ?? TestData.NodeOne,
            drop,
            dropNodeId ?? TestData.NodeTwo,
            new SimTime(pickupEta),
            new SimTime(dropEta),
            order ??
            [
                new PromiseServiceToken(
                    pickup,
                    TestData.RequestOne,
                    RouteStopKind.Pickup),
                new PromiseServiceToken(
                    drop,
                    TestData.RequestOne,
                    RouteStopKind.DropOff),
            ]);
    }

    public static CommitmentVector Vector(
        CommitmentDimension dimension,
        long value)
    {
        var values = new long[10];
        values[(int)dimension] = value;
        return new CommitmentVector(
            values[0],
            values[1],
            values[2],
            values[3],
            values[4],
            values[5],
            values[6],
            values[7],
            values[8],
            values[9]);
    }

    public static CommitmentPolicy Policy(
        long? hardLimit = 10,
        CommitmentPhase phases = CommitmentPhase.AllActive,
        Duration? freezeHorizon = null,
        PromiseLock freezeLocks = PromiseLock.None,
        PromiseLock confirmationLocks = PromiseLock.None) =>
        new(
            "test-boundary-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    hardLimit,
                    phases)),
            new MaterialRevisionRule(1_000, 60_000),
            freezeHorizon,
            freezeLocks,
            confirmationLocks);
}
