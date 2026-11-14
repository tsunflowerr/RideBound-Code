using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Commitments;

/// <summary>
/// Research probe for the relaxed deadline (DL-HZ): the one-sided deadline measured from the
/// rider's initial promise <c>p0</c> is lifted to the kept route when drift has already
/// carried it past <c>p0 + slack</c>, so a candidate fails only when its drop ETA is later
/// than <c>max(p0 + slack, e)</c>, with <c>e</c> the exogenous (kept-route) projection.
/// </summary>
public sealed class CommitmentRelaxedDeadlineLockTests
{
    private const long Anchor = 900_000;
    private const long Slack = 30_000;
    private const long Deadline = Anchor + Slack;

    private static readonly RequestId Request = new("request-1");
    private static readonly VehicleId Vehicle = new("vehicle-1");
    private static readonly NodeId PickupNode = new("node-pickup");
    private static readonly NodeId DropNode = new("node-drop");
    private static readonly StopId PickupStop = new("stop-pickup");
    private static readonly StopId DropStop = new("stop-drop");

    [Theory]
    // The kept route is inside the deadline: only the deadline counts.
    [InlineData(Anchor, Anchor, false)]
    [InlineData(Anchor, Deadline, false)]
    [InlineData(Anchor, Deadline + 1, true)]
    [InlineData(Deadline, Deadline, false)]
    [InlineData(Deadline, Deadline + 1, true)]
    // Favourable drift: the kept route is earlier than the initial promise.
    [InlineData(Anchor - Slack, Anchor - Slack, false)]
    [InlineData(Anchor - Slack, Deadline, false)]
    [InlineData(Anchor - Slack, Deadline + 1, true)]
    // Adverse drift past the deadline: the kept route itself and anything no later pass.
    [InlineData(Deadline + 5_000, Deadline + 5_000, false)]
    [InlineData(Deadline + 5_000, Deadline + 4_999, false)]
    [InlineData(Deadline + 5_000, Deadline + 1, false)]
    [InlineData(Deadline + 5_000, Deadline, false)]
    [InlineData(Deadline + 5_000, Anchor, false)]
    [InlineData(Deadline + 5_000, Deadline + 5_001, true)]
    [InlineData(Deadline + 5_000, Deadline + 60_000, true)]
    // The kept route one millisecond past the deadline.
    [InlineData(Deadline + 1, Deadline + 1, false)]
    [InlineData(Deadline + 1, Deadline + 2, true)]
    public void A_relaxed_deadline_fails_only_past_the_later_of_the_deadline_and_the_kept_route(
        long exogenousDropMs,
        long candidateDropMs,
        bool expectViolation)
    {
        var witnesses = Evaluate(
            RelaxedPolicy(Slack),
            Projection(exogenousDropMs),
            Projection(candidateDropMs));

        Assert.Equal(
            candidateDropMs > Math.Max(Deadline, exogenousDropMs),
            expectViolation);

        if (expectViolation)
        {
            var witness = Assert.Single(witnesses);
            Assert.Equal(Request, witness.RequestId);
            Assert.Equal("drop_eta_ms", witness.Dimension);
            Assert.Equal(CommitmentLockEvaluator.DeadlineCapRule, witness.Rule);
        }
        else
        {
            Assert.Empty(witnesses);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Slack)]
    [InlineData(Slack + 1)]
    [InlineData(10 * Slack)]
    public void The_kept_route_never_trips_a_relaxed_deadline(long drift)
    {
        var kept = Projection(Anchor + drift);

        Assert.Empty(Evaluate(RelaxedPolicy(Slack), kept, kept));
        Assert.Empty(Evaluate(RelaxedPolicy(0), kept, kept));
    }

    [Fact]
    public void A_relaxed_zero_slack_deadline_is_the_later_of_the_initial_promise_and_the_kept_route()
    {
        Assert.Empty(Evaluate(RelaxedPolicy(0), Projection(Anchor), Projection(Anchor)));
        Assert.Single(Evaluate(RelaxedPolicy(0), Projection(Anchor), Projection(Anchor + 1)));
        Assert.Empty(Evaluate(RelaxedPolicy(0), Projection(Anchor + 9), Projection(Anchor + 9)));
        Assert.Single(Evaluate(RelaxedPolicy(0), Projection(Anchor + 9), Projection(Anchor + 10)));
    }

    [Fact]
    public void A_relaxed_deadline_never_rejects_an_early_drop()
    {
        // Being early is never a violation.
        Assert.Empty(Evaluate(RelaxedPolicy(Slack), Projection(Anchor), Projection(0)));
        Assert.Empty(Evaluate(RelaxedPolicy(Slack), Projection(0), Projection(0)));
    }

    [Fact]
    public void A_relaxed_deadline_still_requires_the_initial_promise_anchor()
    {
        var kept = Projection(Anchor);

        var error = Assert.Throws<ArgumentException>(
            () => new CommitmentLockEvaluator().Evaluate(
                WaitingRequest(),
                Published(kept),
                kept,
                kept,
                RelaxedPolicy(Slack),
                anchor: null));

        Assert.Equal("anchor", error.ParamName);
    }

    private static IReadOnlyList<CommitmentLockWitness> Evaluate(
        CommitmentPolicy policy,
        PromiseProjection exogenous,
        PromiseProjection candidate) =>
        new CommitmentLockEvaluator().Evaluate(
            WaitingRequest(),
            Published(Projection(Anchor)),
            exogenous,
            candidate,
            policy,
            Projection(Anchor));

    private static CommitmentPolicy RelaxedPolicy(long slack) =>
        new(
            "policy-1",
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: new Duration(slack));

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

    private static PromiseProjection Projection(long dropMs) =>
        new(
            Request,
            Vehicle,
            PickupStop,
            PickupNode,
            DropStop,
            DropNode,
            new SimTime(0),
            new SimTime(dropMs),
            [
                new PromiseServiceToken(PickupStop, Request, RouteStopKind.Pickup),
                new PromiseServiceToken(DropStop, Request, RouteStopKind.DropOff),
            ]);

    private static PublishedPromise Published(PromiseProjection projection) =>
        new(new PromiseVersion(1), 1, new SimTime(0), projection);
}
