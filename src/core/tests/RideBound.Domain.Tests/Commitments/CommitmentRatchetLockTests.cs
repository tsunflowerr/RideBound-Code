using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Commitments;

/// <summary>
/// RB-WP14-005 factor F2. A ratcheted ETA lock rejects only movement that makes the
/// promise later, matching the one-sided guarantee used in the ride-pooling
/// literature. An exact lock keeps rejecting any movement at all.
/// </summary>
public sealed class CommitmentRatchetLockTests
{
    private static readonly RequestId Request = new("request-1");
    private static readonly VehicleId Vehicle = new("vehicle-1");
    private static readonly NodeId PickupNode = new("node-pickup");
    private static readonly NodeId DropNode = new("node-drop");
    private static readonly StopId PickupStop = new("stop-pickup");
    private static readonly StopId DropStop = new("stop-drop");

    [Theory]
    [InlineData(60_000, false, true)]   // exact lock, later pickup: violation
    [InlineData(20_000, false, true)]   // exact lock, earlier pickup: violation
    [InlineData(40_000, false, false)]  // exact lock, unchanged: allowed
    [InlineData(60_000, true, true)]    // ratchet, later pickup: violation
    [InlineData(20_000, true, false)]   // ratchet, earlier pickup: allowed
    [InlineData(40_000, true, false)]   // ratchet, unchanged: allowed
    public void A_ratchet_only_rejects_a_later_pickup(
        long candidatePickupMs,
        bool ratcheted,
        bool expectViolation)
    {
        var policy = Policy(
            PromiseLock.Vehicle | PromiseLock.PickupEta,
            ratcheted ? PromiseLock.PickupEta : PromiseLock.None);
        var exogenous = Projection(pickupMs: 40_000, dropMs: 90_000);
        var candidate = Projection(pickupMs: candidatePickupMs, dropMs: 90_000);

        var witnesses = new CommitmentLockEvaluator().Evaluate(
            WaitingRequest(),
            Published(exogenous),
            exogenous,
            candidate,
            new SimTime(0),
            policy);

        Assert.Equal(
            expectViolation,
            witnesses.Any(value => value.Dimension == "pickup_eta_ms"));
    }

    [Fact]
    public void A_ratchet_on_one_field_does_not_relax_the_other()
    {
        var policy = Policy(
            PromiseLock.Vehicle | PromiseLock.PickupEta | PromiseLock.DropEta,
            PromiseLock.PickupEta);
        var exogenous = Projection(pickupMs: 40_000, dropMs: 90_000);
        var candidate = Projection(pickupMs: 20_000, dropMs: 80_000);

        var witnesses = new CommitmentLockEvaluator().Evaluate(
            WaitingRequest(),
            Published(exogenous),
            exogenous,
            candidate,
            new SimTime(0),
            policy);

        // Pickup improved and is ratcheted, so it passes. Drop improved too but is
        // an exact lock, so any movement is still a violation.
        Assert.DoesNotContain(witnesses, value => value.Dimension == "pickup_eta_ms");
        Assert.Contains(witnesses, value => value.Dimension == "drop_eta_ms");
    }

    [Fact]
    public void Only_ordered_eta_fields_can_be_ratcheted()
    {
        foreach (var unordered in new[]
                 {
                     PromiseLock.Vehicle,
                     PromiseLock.PickupStop,
                     PromiseLock.DropStop,
                 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Policy(PromiseLock.Vehicle, unordered));
        }
    }

    [Fact]
    public void A_policy_ratchets_nothing_by_default()
    {
        Assert.Equal(PromiseLock.None, Policy(PromiseLock.Vehicle).RatchetLocks);
    }

    private static CommitmentPolicy Policy(
        PromiseLock finalConfirmationLocks,
        PromiseLock ratchetLocks = PromiseLock.None) =>
        new(
            "policy-1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            finalConfirmationLocks: finalConfirmationLocks,
            ratchetLocks: ratchetLocks);

    private static RideRequest WaitingRequest()
    {
        var request = RideRequest.CreatePending(
            Request,
            new SimTime(0),
            PickupNode,
            DropNode,
            new SimTime(0),
            new SimTime(600_000),
            new Duration(3_600_000),
            1,
            "service-class-1",
            "policy-1").Value!;
        return request.Accept(Vehicle).Value!.ConfirmWaitingPickup().Value!;
    }

    private static PromiseProjection Projection(long pickupMs, long dropMs) =>
        new(
            Request,
            Vehicle,
            PickupStop,
            PickupNode,
            DropStop,
            DropNode,
            new SimTime(pickupMs),
            new SimTime(dropMs),
            [
                new PromiseServiceToken(PickupStop, Request, RouteStopKind.Pickup),
                new PromiseServiceToken(DropStop, Request, RouteStopKind.DropOff),
            ]);

    private static PublishedPromise Published(PromiseProjection projection) =>
        new(new PromiseVersion(1), 1, new SimTime(0), projection);
}
