using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Tests.Commitments;

/// <summary>
/// The waiting-rider relocation flag lifts the accepted-assignment vehicle lock for a confirmed
/// waiting rider only; every other rule and lifecycle keeps its locks.
/// </summary>
public sealed class WaitingRiderRelocationLockTests
{
    private static readonly RideRequest Accepted =
        TestData.PendingRequest().Accept(TestData.VehicleOne).Value!;

    private static readonly RideRequest Waiting = Accepted.ConfirmWaitingPickup().Value!;

    private static readonly RideRequest Onboard =
        Waiting.Board(TestData.VehicleOne, new SimTime(1_200)).Value!;

    [Fact]
    public void A_waiting_rider_may_change_vehicle_only_with_the_flag()
    {
        var moved = CommitmentTestData.Projection(vehicleId: TestData.VehicleTwo);

        Assert.Empty(Evaluate(Waiting, moved, Policy(relocation: true)));

        var locked = Assert.Single(Evaluate(Waiting, moved, Policy(relocation: false)));
        Assert.Equal(("vehicle_id", "accepted_assignment"), (locked.Dimension, locked.Rule));
    }

    [Fact]
    public void Accepted_and_onboard_riders_keep_the_vehicle_lock_under_the_flag()
    {
        var moved = CommitmentTestData.Projection(vehicleId: TestData.VehicleTwo);

        var accepted = Assert.Single(Evaluate(Accepted, moved, Policy(relocation: true)));
        Assert.Equal(("vehicle_id", "accepted_assignment"), (accepted.Dimension, accepted.Rule));

        var onboard = Evaluate(Onboard, moved, Policy(relocation: true));
        Assert.Equal(
            [("vehicle_id", "accepted_assignment"), ("vehicle_id", "onboard")],
            onboard.Select(value => (value.Dimension, value.Rule)));
    }

    [Fact]
    public void A_moved_waiting_rider_still_meets_the_pickup_ratchet_and_stop_locks()
    {
        // The emulator's G2 lock: pickup no later than the kept route, same pickup stop.
        var policy = Policy(
            relocation: true,
            confirmationLocks: PromiseLock.PickupStop | PromiseLock.PickupEta,
            ratchet: PromiseLock.PickupEta);

        Assert.Empty(Evaluate(
            Waiting,
            CommitmentTestData.Projection(vehicleId: TestData.VehicleTwo, pickupEta: 9_000),
            policy));

        var later = Assert.Single(Evaluate(
            Waiting,
            CommitmentTestData.Projection(vehicleId: TestData.VehicleTwo, pickupEta: 10_001),
            policy));
        Assert.Equal(("pickup_eta_ms", "final_confirmation"), (later.Dimension, later.Rule));

        var otherStop = Assert.Single(Evaluate(
            Waiting,
            CommitmentTestData.Projection(
                vehicleId: TestData.VehicleTwo,
                pickupStopId: new StopId("pickup-other")),
            policy));
        Assert.Equal(("pickup_stop", "final_confirmation"), (otherStop.Dimension, otherStop.Rule));
    }

    [Fact]
    public void The_flag_is_rejected_where_it_would_be_silently_inert()
    {
        Assert.Contains(
            "vehicle lock at final confirmation",
            Assert.Throws<ArgumentException>(
                () => Policy(relocation: true, confirmationLocks: PromiseLock.Vehicle | PromiseLock.PickupEta)).Message);
        Assert.Contains(
            "zero vehicle_switch_count limit",
            Assert.Throws<ArgumentException>(() => Policy(relocation: true, switchLimit: 0)).Message);
        Assert.Contains(
            "zero vehicle_switch_count limit",
            Assert.Throws<ArgumentException>(
                () => Policy(relocation: true, switchLimit: 0, switchPhases: CommitmentPhase.WaitingPickup)).Message);

        // A zero limit outside the waiting phase, a positive limit, or the flag off are allowed.
        Assert.True(Policy(relocation: true, switchLimit: 0, switchPhases: CommitmentPhase.Onboard).WaitingRiderRelocation);
        Assert.True(Policy(relocation: true, switchLimit: 1).WaitingRiderRelocation);
        Assert.False(Policy(relocation: false, switchLimit: 0, confirmationLocks: PromiseLock.Vehicle).WaitingRiderRelocation);

        // A vehicle lock inside the freeze horizon would make the flag inert there.
        Assert.Contains(
            "inside the freeze horizon",
            Assert.Throws<ArgumentException>(
                () => new CommitmentPolicy(
                    "test-boundary-v1",
                    CommitmentBudgetBasis.DecisionInduced,
                    CommitmentDimensionVocabulary.Ordered.Select(
                        dimension => new CommitmentDimensionLimit(dimension, null, CommitmentPhase.AllActive)),
                    new MaterialRevisionRule(1_000, 60_000),
                    freezeHorizon: new Duration(60_000),
                    freezeHorizonLocks: PromiseLock.Vehicle | PromiseLock.PickupEta,
                    waitingRiderRelocation: true)).Message);
    }

    private static IReadOnlyList<CommitmentLockWitness> Evaluate(
        RideRequest request,
        PromiseProjection candidate,
        CommitmentPolicy policy)
    {
        var previous = new PublishedPromise(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            CommitmentTestData.Projection());

        return new CommitmentLockEvaluator().Evaluate(
            request,
            previous,
            previous.Projection,
            candidate,
            new SimTime(1_000),
            policy);
    }

    private static CommitmentPolicy Policy(
        bool relocation,
        PromiseLock confirmationLocks = PromiseLock.None,
        PromiseLock ratchet = PromiseLock.None,
        long? switchLimit = null,
        CommitmentPhase switchPhases = CommitmentPhase.AllActive) =>
        new(
            "test-boundary-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => dimension == CommitmentDimension.VehicleSwitchCount
                    ? new CommitmentDimensionLimit(dimension, switchLimit, switchPhases)
                    : new CommitmentDimensionLimit(dimension, null, CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, 60_000),
            finalConfirmationLocks: confirmationLocks,
            ratchetLocks: ratchet,
            waitingRiderRelocation: relocation);
}
