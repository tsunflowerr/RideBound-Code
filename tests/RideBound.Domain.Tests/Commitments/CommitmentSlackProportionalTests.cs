using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Commitments;

/// <summary>
/// Research probe for the slack-proportional budgets of Elmi et al. (2026) Ordinary Control as
/// formalised in the wf14 preregistration: each rider's budget is
/// round(alpha * max(0, pickup0 + maxRide - drop0)), fixed at the first promise. It is used either
/// as the rider's drop_eta_total_ms hard limit (rule C, RCE) or as its deadline slack (EO, EOHZ).
/// </summary>
public sealed class CommitmentSlackProportionalTests
{
    private const long PickupAnchor = 100_000;
    private const long DropAnchor = 400_000;
    private const long MaxRide = 600_000;

    // slack = 100_000 + 600_000 - 400_000 = 300_000; 35% of it = 105_000.
    private const long Budget35 = 105_000;

    private static readonly RequestId Request = new("request-1");
    private static readonly VehicleId Vehicle = new("vehicle-1");
    private static readonly NodeId PickupNode = new("node-pickup");
    private static readonly NodeId DropNode = new("node-drop");
    private static readonly StopId PickupStop = new("stop-pickup");
    private static readonly StopId DropStop = new("stop-drop");

    [Theory]
    [InlineData(100_000, 400_000, 600_000, 350, 105_000)]
    [InlineData(100_000, 400_000, 600_000, 1000, 300_000)]
    [InlineData(100_000, 400_000, 600_000, 1, 300)]
    // The first promise already uses the whole ride-time room: no slack.
    [InlineData(100_000, 700_000, 600_000, 350, 0)]
    // The first promise is past the ride-time room (drift before it opened): clamped to zero.
    [InlineData(100_000, 800_000, 600_000, 350, 0)]
    // Halves round away from zero, like the emulator's Math.round on a positive value.
    [InlineData(0, 0, 1, 500, 1)]
    [InlineData(0, 0, 3, 500, 2)]
    [InlineData(0, 0, 1, 350, 0)]
    [InlineData(0, 0, 2, 350, 1)]
    public void The_slack_proportional_limit_is_a_rounded_share_of_the_first_promise_slack(
        long pickup0,
        long drop0,
        long maxRide,
        int permille,
        long expected)
    {
        Assert.Equal(
            expected,
            CommitmentPolicy.SlackProportionalLimit(
                Projection(pickup0, drop0),
                new Duration(maxRide),
                permille));
    }

    [Fact]
    public void The_slack_proportional_limit_never_exceeds_the_slack_and_grows_with_it()
    {
        var previous = 0L;

        for (var slack = 0L; slack <= 20_000; slack += 7)
        {
            var limit = CommitmentPolicy.SlackProportionalLimit(
                Projection(0, 0),
                new Duration(slack),
                350);

            Assert.InRange(limit, 0, slack);
            Assert.True(limit >= previous);
            Assert.Equal((long)Math.Round(0.35 * slack, MidpointRounding.AwayFromZero), limit);
            previous = limit;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void The_permille_must_be_one_to_one_thousand(int permille)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CommitmentPolicy.SlackProportionalLimit(
                Projection(0, 0),
                new Duration(1_000),
                permille));
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy(budgetPermille: permille));
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy(deadlinePermille: permille));
    }

    [Fact]
    public void A_rider_budget_accepts_its_exact_limit_and_rejects_one_more()
    {
        var policy = Policy(budgetPermille: 350);
        var limit = policy.DropEtaTotalLimitFor(Projection(PickupAnchor, DropAnchor), new Duration(MaxRide));
        Assert.Equal(Budget35, limit);

        var evaluator = new CommitmentBudgetEvaluator();
        var exact = evaluator.Evaluate(
            Request,
            RequestLifecycle.WaitingPickup,
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, Budget35 - 5_000),
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 5_000),
            policy,
            limit);
        var over = evaluator.Evaluate(
            Request,
            RequestLifecycle.WaitingPickup,
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, Budget35 - 5_000),
            CommitmentTestData.Vector(CommitmentDimension.DropEtaTotalMs, 5_001),
            policy,
            limit);

        Assert.True(exact.IsAllowed);
        var witness = Assert.Single(over.Witnesses);
        Assert.Equal("drop_eta_total_ms", witness.Dimension);
        Assert.Equal(Budget35, witness.Limit);
        Assert.Equal(Budget35 + 1, witness.After);
    }

    [Fact]
    public void A_rider_budget_must_be_given_exactly_for_a_slack_proportional_policy()
    {
        var evaluator = new CommitmentBudgetEvaluator();
        var zero = CommitmentVector.Zero;

        Assert.Throws<ArgumentException>(
            () => evaluator.Evaluate(Request, RequestLifecycle.Accepted, zero, zero, Policy(budgetPermille: 350)));
        Assert.Throws<ArgumentException>(
            () => evaluator.Evaluate(Request, RequestLifecycle.Accepted, zero, zero, Policy(), 1_000));
    }

    [Fact]
    public void The_rider_budget_leaves_every_other_dimension_on_its_fixed_limit()
    {
        var policy = Policy(budgetPermille: 350, otherHardLimit: 10);
        var result = new CommitmentBudgetEvaluator().Evaluate(
            Request,
            RequestLifecycle.Accepted,
            CommitmentVector.Zero,
            CommitmentTestData.Vector(CommitmentDimension.PickupEtaTotalMs, 11),
            policy,
            Budget35);

        var witness = Assert.Single(result.Witnesses);
        Assert.Equal("pickup_eta_total_ms", witness.Dimension);
        Assert.Equal(10, witness.Limit);
    }

    [Theory]
    // Kept route inside the rider deadline (drop0 + 105_000): the relaxed rule is the strict one.
    [InlineData(DropAnchor, DropAnchor + Budget35, false)]
    [InlineData(DropAnchor, DropAnchor + Budget35 + 1, true)]
    // Adverse drift past the rider deadline: the kept route and anything no later pass.
    [InlineData(DropAnchor + Budget35 + 9_000, DropAnchor + Budget35 + 9_000, false)]
    [InlineData(DropAnchor + Budget35 + 9_000, DropAnchor + Budget35 + 9_001, true)]
    public void A_relaxed_rider_deadline_fails_only_past_the_later_of_its_deadline_and_the_kept_route(
        long exogenousDropMs,
        long candidateDropMs,
        bool expectViolation)
    {
        var witnesses = EvaluateLocks(
            Policy(deadlinePermille: 350, relaxed: true),
            Projection(PickupAnchor, exogenousDropMs),
            Projection(PickupAnchor, candidateDropMs));

        if (expectViolation)
        {
            var witness = Assert.Single(witnesses);
            Assert.Equal("drop_eta_ms", witness.Dimension);
            Assert.Equal(CommitmentLockEvaluator.DeadlineCapRule, witness.Rule);
        }
        else
        {
            Assert.Empty(witnesses);
        }
    }

    [Fact]
    public void A_slack_proportional_deadline_equals_the_fixed_deadline_of_the_same_slack()
    {
        foreach (var exogenous in new[] { DropAnchor - 1_000, DropAnchor, DropAnchor + Budget35, DropAnchor + Budget35 + 7 })
        {
            for (var candidate = DropAnchor - 50_000; candidate <= DropAnchor + Budget35 + 50_000; candidate += 4_999)
            {
                foreach (var relaxed in new[] { false, true })
                {
                    Assert.Equal(
                        EvaluateLocks(
                            Policy(fixedSlack: Budget35, relaxed: relaxed),
                            Projection(PickupAnchor, exogenous),
                            Projection(PickupAnchor, candidate)),
                        EvaluateLocks(
                            Policy(deadlinePermille: 350, relaxed: relaxed),
                            Projection(PickupAnchor, exogenous),
                            Projection(PickupAnchor, candidate)));
                }
            }
        }
    }

    [Fact]
    public void The_kept_route_never_trips_a_relaxed_rider_deadline()
    {
        foreach (var drift in new[] { 0L, Budget35, Budget35 + 1, 10 * Budget35 })
        {
            var kept = Projection(PickupAnchor, DropAnchor + drift);

            Assert.Empty(EvaluateLocks(Policy(deadlinePermille: 350, relaxed: true), kept, kept));
            Assert.Equal(
                drift > Budget35 ? 1 : 0,
                EvaluateLocks(Policy(deadlinePermille: 350), kept, kept).Count);
        }
    }

    [Fact]
    public void Policy_construction_keeps_the_new_rules_exclusive_and_explicit()
    {
        Assert.Throws<ArgumentException>(() => Policy(fixedSlack: 1_000, deadlinePermille: 350));
        Assert.Throws<ArgumentException>(() => Policy(budgetPermille: 350, dropHardLimit: 120_000));
        Assert.Throws<ArgumentException>(() => Policy(relaxedWithoutDeadline: true));

        var relaxed = Policy(deadlinePermille: 350, relaxed: true);
        Assert.True(relaxed.DropEtaDeadlineRelaxed);
        Assert.Null(relaxed.DropEtaDeadlineSlack);
        Assert.Equal(350, relaxed.DropEtaDeadlineSlackPermille);

        Assert.False(Policy().ReadsInitialPromise);
        Assert.True(Policy(budgetPermille: 350).ReadsInitialPromise);
        Assert.True(Policy(deadlinePermille: 350).ReadsInitialPromise);
        Assert.True(Policy(fixedSlack: 1_000).ReadsInitialPromise);
        Assert.Null(Policy().DropEtaTotalLimitFor(null, new Duration(MaxRide)));
        Assert.Throws<ArgumentException>(
            () => Policy(budgetPermille: 350).DropEtaTotalLimitFor(null, new Duration(MaxRide)));
    }

    private static IReadOnlyList<CommitmentLockWitness> EvaluateLocks(
        CommitmentPolicy policy,
        PromiseProjection exogenous,
        PromiseProjection candidate) =>
        new CommitmentLockEvaluator().Evaluate(
            WaitingRequest(),
            new PublishedPromise(new PromiseVersion(1), 1, new SimTime(0), Projection(PickupAnchor, DropAnchor)),
            exogenous,
            candidate,
            new SimTime(0),
            policy,
            Projection(PickupAnchor, DropAnchor));

    private static CommitmentPolicy Policy(
        int? budgetPermille = null,
        int? deadlinePermille = null,
        long? fixedSlack = null,
        bool relaxed = false,
        long? dropHardLimit = null,
        long? otherHardLimit = null,
        bool relaxedWithoutDeadline = false) =>
        new(
            "policy-1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.DropEtaTotalMs ? dropHardLimit : otherHardLimit,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: fixedSlack is long slack ? new Duration(slack) : null,
            dropEtaDeadlineRelaxed: relaxed || relaxedWithoutDeadline,
            dropEtaBudgetSlackPermille: budgetPermille,
            dropEtaDeadlineSlackPermille: deadlinePermille);

    private static RideRequest WaitingRequest()
    {
        var request = RideRequest.CreatePending(
            Request,
            new SimTime(0),
            PickupNode,
            DropNode,
            new SimTime(0),
            new SimTime(600_000),
            new Duration(MaxRide),
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
}
