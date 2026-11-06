using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;

namespace RideBound.Domain.Tests.Commitments;

public sealed class CommitmentLockEvaluatorTests
{
    [Fact]
    public void Accepted_assignment_is_always_locked()
    {
        var request = TestData.PendingRequest().Accept(TestData.VehicleOne).Value!;
        var previous = Published(CommitmentTestData.Projection());
        var candidate = CommitmentTestData.Projection(
            vehicleId: TestData.VehicleTwo);

        var witnesses = new CommitmentLockEvaluator().Evaluate(
            request,
            previous,
            previous.Projection,
            candidate,
            new SimTime(1_000),
            CommitmentTestData.Policy());

        var witness = Assert.Single(witnesses);
        Assert.Equal("vehicle_id", witness.Dimension);
        Assert.Equal("accepted_assignment", witness.Rule);
    }

    [Fact]
    public void Onboard_locks_realized_pickup_but_not_drop_eta()
    {
        var request = TestData.PendingRequest()
            .Accept(TestData.VehicleOne).Value!
            .ConfirmWaitingPickup().Value!
            .Board(TestData.VehicleOne, new SimTime(1_200)).Value!;
        var previous = Published(CommitmentTestData.Projection());
        var candidate = CommitmentTestData.Projection(
            pickupEta: 10_001,
            dropEta: 20_010);

        var witnesses = new CommitmentLockEvaluator().Evaluate(
            request,
            previous,
            previous.Projection,
            candidate,
            new SimTime(11_000),
            CommitmentTestData.Policy());

        var witness = Assert.Single(witnesses);
        Assert.Equal("pickup_eta_ms", witness.Dimension);
        Assert.Equal("onboard", witness.Rule);
    }

    [Fact]
    public void Freeze_horizon_and_final_confirmation_use_explicit_policy_flags()
    {
        var request = TestData.PendingRequest()
            .Accept(TestData.VehicleOne).Value!
            .ConfirmWaitingPickup().Value!;
        var previous = Published(CommitmentTestData.Projection());
        var candidate = CommitmentTestData.Projection(
            pickupStopId: new StopId("pickup-new"),
            pickupEta: 10_001);
        var policy = CommitmentTestData.Policy(
            freezeHorizon: new Duration(1_000),
            freezeLocks: PromiseLock.PickupEta,
            confirmationLocks: PromiseLock.PickupStop);

        var witnesses = new CommitmentLockEvaluator().Evaluate(
            request,
            previous,
            previous.Projection,
            candidate,
            new SimTime(9_000),
            policy);

        Assert.Collection(
            witnesses,
            witness =>
            {
                Assert.Equal("pickup_eta_ms", witness.Dimension);
                Assert.Equal("freeze_horizon", witness.Rule);
            },
            witness =>
            {
                Assert.Equal("pickup_stop", witness.Dimension);
                Assert.Equal("final_confirmation", witness.Rule);
            });
    }

    [Fact]
    public void Exogenous_drift_does_not_trip_final_lock_but_candidate_delta_does()
    {
        var request = TestData.PendingRequest()
            .Accept(TestData.VehicleOne).Value!
            .ConfirmWaitingPickup().Value!;
        var previous = Published(CommitmentTestData.Projection(
            pickupEta: 10_000));
        var exogenous = CommitmentTestData.Projection(pickupEta: 10_008);
        var policy = CommitmentTestData.Policy(
            confirmationLocks: PromiseLock.PickupEta);
        var evaluator = new CommitmentLockEvaluator();

        var noOp = evaluator.Evaluate(
            request,
            previous,
            exogenous,
            exogenous,
            new SimTime(2_000),
            policy);
        var changedByDecision = evaluator.Evaluate(
            request,
            previous,
            exogenous,
            CommitmentTestData.Projection(pickupEta: 10_009),
            new SimTime(2_000),
            policy);

        Assert.Empty(noOp);
        var witness = Assert.Single(changedByDecision);
        Assert.Equal("pickup_eta_ms", witness.Dimension);
        Assert.Equal("final_confirmation", witness.Rule);
    }

    private static PublishedPromise Published(PromiseProjection projection) =>
        new(
            new PromiseVersion(1),
            1,
            new SimTime(1_000),
            projection);
}
