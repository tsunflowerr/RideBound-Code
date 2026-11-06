using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Commitments;

/// <summary>
/// Research probe for the deadline family. A deadline compares the candidate drop ETA
/// with the rider's INITIAL promise, not with the exogenous projection, so exogenous
/// drift alone can trip it on the safety no-op. Disabled unless a slack is configured.
/// </summary>
public sealed class CommitmentDeadlineLockTests
{
    private const long Anchor = 900_000;
    private const long Slack = 30_000;

    private static readonly RequestId Request = new("request-1");
    private static readonly VehicleId Vehicle = new("vehicle-1");
    private static readonly NodeId PickupNode = new("node-pickup");
    private static readonly NodeId DropNode = new("node-drop");
    private static readonly StopId PickupStop = new("stop-pickup");
    private static readonly StopId DropStop = new("stop-drop");

    [Fact]
    public void Without_a_slack_no_deadline_witness_is_produced()
    {
        var far = Projection(Anchor + 10 * Slack);

        var witnesses = Evaluate(Policy(slack: null), far, far, Projection(Anchor));

        Assert.Empty(witnesses);
    }

    [Fact]
    public void Without_a_slack_the_anchor_may_be_omitted()
    {
        var far = Projection(Anchor + 10 * Slack);

        var witnesses = Evaluate(Policy(slack: null), far, far, anchor: null);

        Assert.Empty(witnesses);
    }

    [Theory]
    [InlineData(Anchor + Slack, false)]         // exactly on the deadline: allowed
    [InlineData(Anchor + Slack + 1, true)]      // one millisecond past: violation
    [InlineData(Anchor, false)]                 // unchanged
    [InlineData(Anchor - Slack - 1, false)]     // one-sided: earlier is never a violation
    [InlineData(0, false)]                      // one-sided: far earlier is never a violation
    public void One_sided_deadline_rejects_only_a_drop_later_than_anchor_plus_slack(
        long candidateDropMs,
        bool expectViolation)
    {
        var exogenous = Projection(Anchor);
        var candidate = Projection(candidateDropMs);

        var witnesses = Evaluate(Policy(Slack), exogenous, candidate, Projection(Anchor));

        if (expectViolation)
        {
            var witness = Assert.Single(witnesses);
            Assert.Equal("drop_eta_ms", witness.Dimension);
            Assert.Equal("deadline_cap", witness.Rule);
            Assert.Equal(Request, witness.RequestId);
        }
        else
        {
            Assert.Empty(witnesses);
        }
    }

    [Fact]
    public void Drift_alone_trips_the_deadline_on_the_safety_no_op()
    {
        // The no-op keeps the route, so candidate == exogenous and the decision adds
        // nothing. A lock never fires here; the deadline does, because the kept
        // route already arrives later than the initial promise plus the slack.
        var drifted = Projection(Anchor + Slack + 5_000);

        var witnesses = Evaluate(Policy(Slack), drifted, drifted, Projection(Anchor));

        var witness = Assert.Single(witnesses);
        Assert.Equal("deadline_cap", witness.Rule);
    }

    [Fact]
    public void A_deadline_without_an_anchor_fails_loudly()
    {
        var exogenous = Projection(Anchor);

        var error = Assert.Throws<ArgumentException>(
            () => Evaluate(Policy(Slack), exogenous, exogenous, anchor: null));

        Assert.Equal("anchor", error.ParamName);
    }

    [Fact]
    public void A_deadline_anchor_for_another_request_fails_loudly()
    {
        var exogenous = Projection(Anchor);
        var foreign = Projection(Anchor, new RequestId("request-2"));

        var error = Assert.Throws<ArgumentException>(
            () => Evaluate(Policy(Slack), exogenous, exogenous, foreign));

        Assert.Equal("anchor", error.ParamName);
    }

    [Theory]
    [InlineData(Anchor - Slack, null)]                  // exactly on the floor: allowed
    [InlineData(Anchor - Slack - 1, "deadline_floor")]  // one millisecond early: floor
    [InlineData(Anchor + Slack, null)]                  // exactly on the cap: allowed
    [InlineData(Anchor + Slack + 1, "deadline_cap")]    // one millisecond late: cap only
    [InlineData(Anchor, null)]
    public void Two_sided_deadline_is_a_window_around_the_initial_promise(
        long candidateDropMs,
        string? expectedRule)
    {
        var exogenous = Projection(Anchor);
        var candidate = Projection(candidateDropMs);

        var witnesses = Evaluate(
            Policy(Slack, twoSided: true),
            exogenous,
            candidate,
            Projection(Anchor));

        if (expectedRule is null)
        {
            Assert.Empty(witnesses);
        }
        else
        {
            var witness = Assert.Single(witnesses);
            Assert.Equal("drop_eta_ms", witness.Dimension);
            Assert.Equal(expectedRule, witness.Rule);
        }
    }

    [Fact]
    public void Favourable_drift_trips_only_the_two_sided_deadline_on_the_no_op()
    {
        // Corollary on drift direction: when the kept route has become EARLIER than
        // the initial promise by more than the slack, the two-sided deadline rejects
        // the no-op, while the one-sided deadline stays safe in the same state.
        var early = Projection(Anchor - Slack - 1);

        var twoSided = Evaluate(Policy(Slack, twoSided: true), early, early, Projection(Anchor));
        var oneSided = Evaluate(Policy(Slack), early, early, Projection(Anchor));

        Assert.Equal("deadline_floor", Assert.Single(twoSided).Rule);
        Assert.Empty(oneSided);
    }

    [Fact]
    public void A_zero_slack_is_an_exact_deadline()
    {
        var anchor = Projection(Anchor);

        Assert.Empty(Evaluate(Policy(0), anchor, Projection(Anchor), anchor));
        Assert.Equal(
            "deadline_cap",
            Assert.Single(Evaluate(Policy(0), anchor, Projection(Anchor + 1), anchor)).Rule);
    }

    [Fact]
    public void The_deadline_witness_follows_existing_lock_witnesses()
    {
        // Callers read the first witness as the reason for a prune, so a lock that
        // fires must stay first; the deadline is appended after the lock witnesses.
        var policy = Policy(Slack, finalConfirmationLocks: PromiseLock.DropEta);
        var exogenous = Projection(Anchor);
        var candidate = Projection(Anchor + Slack + 1);

        var witnesses = Evaluate(policy, exogenous, candidate, Projection(Anchor));

        Assert.Collection(
            witnesses,
            witness =>
            {
                Assert.Equal("drop_eta_ms", witness.Dimension);
                Assert.Equal("final_confirmation", witness.Rule);
            },
            witness =>
            {
                Assert.Equal("drop_eta_ms", witness.Dimension);
                Assert.Equal(CommitmentLockEvaluator.DeadlineCapRule, witness.Rule);
            });
    }

    [Fact]
    public void The_deadline_witness_follows_a_lock_on_a_later_sorting_dimension()
    {
        // "drop_eta_ms" sorts before "pickup_eta_ms", so without the append rule the
        // deadline would displace a pickup lock from the first position.
        var policy = Policy(Slack, finalConfirmationLocks: PromiseLock.PickupEta);
        var exogenous = Projection(Anchor);
        var candidate = Projection(Anchor + Slack + 1, pickupMs: 1);

        var witnesses = Evaluate(policy, exogenous, candidate, Projection(Anchor));

        Assert.Collection(
            witnesses,
            witness =>
            {
                Assert.Equal("pickup_eta_ms", witness.Dimension);
                Assert.Equal("final_confirmation", witness.Rule);
            },
            witness =>
            {
                Assert.Equal("drop_eta_ms", witness.Dimension);
                Assert.Equal(CommitmentLockEvaluator.DeadlineCapRule, witness.Rule);
            });
    }

    [Fact]
    public void A_zero_slack_two_sided_deadline_rejects_one_millisecond_early()
    {
        var anchor = Projection(Anchor);

        Assert.Equal(
            CommitmentLockEvaluator.DeadlineFloorRule,
            Assert.Single(
                Evaluate(Policy(0, twoSided: true), anchor, Projection(Anchor - 1), anchor))
                .Rule);
    }

    [Fact]
    public void Extreme_canonical_times_do_not_overflow()
    {
        var zero = Projection(0);
        var max = Projection(DomainLimits.MaxCanonicalInteger);

        Assert.Equal(
            CommitmentLockEvaluator.DeadlineCapRule,
            Assert.Single(Evaluate(Policy(Slack), zero, max, zero)).Rule);
        Assert.Equal(
            CommitmentLockEvaluator.DeadlineFloorRule,
            Assert.Single(Evaluate(Policy(Slack, twoSided: true), max, zero, max)).Rule);
        Assert.Empty(Evaluate(Policy(Slack), max, zero, max));
    }

    private static IReadOnlyList<CommitmentLockWitness> Evaluate(
        CommitmentPolicy policy,
        PromiseProjection exogenous,
        PromiseProjection candidate,
        PromiseProjection? anchor) =>
        new CommitmentLockEvaluator().Evaluate(
            WaitingRequest(),
            Published(exogenous),
            exogenous,
            candidate,
            new SimTime(0),
            policy,
            anchor);

    private static CommitmentPolicy Policy(
        long? slack,
        bool twoSided = false,
        PromiseLock finalConfirmationLocks = PromiseLock.None) =>
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
            dropEtaDeadlineSlack: slack is long value ? new Duration(value) : null,
            dropEtaDeadlineTwoSided: twoSided);

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

    private static PromiseProjection Projection(
        long dropMs,
        RequestId? requestId = null,
        long pickupMs = 0)
    {
        var id = requestId ?? Request;
        return new(
            id,
            Vehicle,
            PickupStop,
            PickupNode,
            DropStop,
            DropNode,
            new SimTime(pickupMs),
            new SimTime(dropMs),
            [
                new PromiseServiceToken(PickupStop, id, RouteStopKind.Pickup),
                new PromiseServiceToken(DropStop, id, RouteStopKind.DropOff),
            ]);
    }

    private static PublishedPromise Published(PromiseProjection projection) =>
        new(new PromiseVersion(1), 1, new SimTime(0), projection);
}
