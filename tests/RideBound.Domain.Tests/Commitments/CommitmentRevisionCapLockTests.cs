using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;

namespace RideBound.Domain.Tests.Commitments;

/// <summary>
/// Research probe for a per-decision cap (P-b): a candidate may move a rider's drop ETA at
/// most <c>b</c> away from the exogenous (kept-route) projection, in either direction. The
/// reference is the projection every lock uses, never the previous published promise, so
/// drift is free and the kept route never trips the cap. Nothing is remembered between
/// decisions. Disabled unless a limit is configured.
/// </summary>
public sealed class CommitmentRevisionCapLockTests
{
    private const long Exogenous = 900_000;
    private const long Limit = 30_000;

    // The previous published promise differs from the exogenous projection by drift, so a
    // rule that compared with it instead of the exogenous projection would be detected.
    private const long Previous = Exogenous - 7 * Limit;

    private static readonly RequestId Request = new("request-1");
    private static readonly VehicleId Vehicle = new("vehicle-1");
    private static readonly NodeId PickupNode = new("node-pickup");
    private static readonly NodeId DropNode = new("node-drop");
    private static readonly StopId PickupStop = new("stop-pickup");
    private static readonly StopId DropStop = new("stop-drop");

    [Fact]
    public void Without_a_limit_no_revision_cap_witness_is_produced()
    {
        var witnesses = Evaluate(
            Policy(limit: null),
            Projection(Previous),
            Projection(Exogenous),
            Projection(Exogenous + 10 * Limit));

        Assert.Empty(witnesses);
    }

    [Theory]
    [InlineData(Exogenous + Limit, false)]       // exactly the cap later: allowed
    [InlineData(Exogenous + Limit + 1, true)]    // one millisecond past the cap: violation
    [InlineData(Exogenous - Limit, false)]       // exactly the cap earlier: allowed
    [InlineData(Exogenous - Limit - 1, true)]    // earlier by more than the cap: violation
    [InlineData(Exogenous, false)]               // the kept route itself
    public void The_cap_bounds_the_change_against_the_exogenous_projection_in_both_directions(
        long candidateDropMs,
        bool expectViolation)
    {
        var witnesses = Evaluate(
            Policy(Limit),
            Projection(Previous),
            Projection(Exogenous),
            Projection(candidateDropMs));

        if (expectViolation)
        {
            var witness = Assert.Single(witnesses);
            Assert.Equal(Request, witness.RequestId);
            Assert.Equal("drop_eta_ms", witness.Dimension);
            Assert.Equal(CommitmentLockEvaluator.RevisionCapRule, witness.Rule);
            Assert.Equal("revision_cap", witness.Rule);
        }
        else
        {
            Assert.Empty(witnesses);
        }
    }

    [Fact]
    public void The_cap_is_measured_from_the_exogenous_projection_not_the_previous_promise()
    {
        // Drift moved the kept route 7b past the previous promise. A candidate half a cap
        // later than the kept route is 7.5b from the previous promise, but its own change
        // is b/2, so it passes.
        Assert.Empty(
            Evaluate(
                Policy(Limit),
                Projection(Previous),
                Projection(Exogenous),
                Projection(Exogenous + Limit / 2)));

        // A candidate that restores the previous promise against that drift is a decision
        // change of -7b, although the rider sees no change at all.
        Assert.Equal(
            CommitmentLockEvaluator.RevisionCapRule,
            Assert.Single(
                Evaluate(
                    Policy(Limit),
                    Projection(Previous),
                    Projection(Exogenous),
                    Projection(Previous))).Rule);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Limit)]
    [InlineData(Limit + 1)]
    [InlineData(100 * Limit)]
    public void The_kept_route_never_trips_the_cap_whatever_the_drift(long drift)
    {
        var exogenous = Projection(Previous + drift);

        Assert.Empty(Evaluate(Policy(Limit), Projection(Previous), exogenous, exogenous));
        Assert.Empty(Evaluate(Policy(0), Projection(Previous), exogenous, exogenous));
    }

    [Fact]
    public void A_zero_limit_rejects_any_decision_induced_move_of_the_drop()
    {
        var exogenous = Projection(Exogenous);

        Assert.Empty(Evaluate(Policy(0), Projection(Previous), exogenous, Projection(Exogenous)));
        Assert.Equal(
            CommitmentLockEvaluator.RevisionCapRule,
            Assert.Single(
                Evaluate(Policy(0), Projection(Previous), exogenous, Projection(Exogenous + 1)))
                .Rule);
        Assert.Equal(
            CommitmentLockEvaluator.RevisionCapRule,
            Assert.Single(
                Evaluate(Policy(0), Projection(Previous), exogenous, Projection(Exogenous - 1)))
                .Rule);
    }

    [Theory]
    [InlineData(CommitmentPhase.AllActive, true)]
    [InlineData(CommitmentPhase.WaitingPickup, true)]
    [InlineData(CommitmentPhase.Accepted, false)]
    [InlineData(CommitmentPhase.Accepted | CommitmentPhase.Onboard, false)]
    public void The_cap_applies_only_in_the_phases_of_the_drop_eta_dimension(
        CommitmentPhase dropPhases,
        bool expectViolation)
    {
        // The rider is waiting for pickup; the cap follows drop_eta_total_ms's phases even
        // though that dimension has no hard limit here.
        var witnesses = Evaluate(
            Policy(Limit, dropPhases),
            Projection(Previous),
            Projection(Exogenous),
            Projection(Exogenous + Limit + 1));

        Assert.Equal(expectViolation ? 1 : 0, witnesses.Count);
    }

    [Fact]
    public void An_accepted_rider_is_capped_when_the_drop_dimension_applies_while_accepted()
    {
        var accepted = AcceptedRequest();

        var capped = new CommitmentLockEvaluator().Evaluate(
            accepted,
            Published(Projection(Previous)),
            Projection(Exogenous),
            Projection(Exogenous + Limit + 1),
            new SimTime(0),
            Policy(Limit, CommitmentPhase.Accepted));
        var exempt = new CommitmentLockEvaluator().Evaluate(
            accepted,
            Published(Projection(Previous)),
            Projection(Exogenous),
            Projection(Exogenous + Limit + 1),
            new SimTime(0),
            Policy(Limit, CommitmentPhase.WaitingPickup | CommitmentPhase.Onboard));

        Assert.Equal(CommitmentLockEvaluator.RevisionCapRule, Assert.Single(capped).Rule);
        Assert.Empty(exempt);
    }

    [Theory]
    [InlineData(CommitmentPhase.AllActive, true)]
    [InlineData(CommitmentPhase.Onboard, true)]
    [InlineData(CommitmentPhase.Accepted | CommitmentPhase.WaitingPickup, false)]
    public void An_onboard_rider_is_capped_when_the_drop_dimension_applies_onboard(
        CommitmentPhase dropPhases,
        bool expectViolation)
    {
        // Insertions often delay a rider who is already on board, so the cap must follow the
        // rider into the onboard phase whenever drop_eta_total_ms applies there.
        var onboard = OnboardRequest();
        Assert.Equal(RequestLifecycle.Onboard, onboard.Lifecycle);

        var past = new CommitmentLockEvaluator().Evaluate(
            onboard,
            Published(Projection(Previous)),
            Projection(Exogenous),
            Projection(Exogenous + Limit + 1),
            new SimTime(0),
            Policy(Limit, dropPhases));
        var within = new CommitmentLockEvaluator().Evaluate(
            onboard,
            Published(Projection(Previous)),
            Projection(Exogenous),
            Projection(Exogenous + Limit),
            new SimTime(0),
            Policy(Limit, dropPhases));

        if (expectViolation)
        {
            Assert.Equal(CommitmentLockEvaluator.RevisionCapRule, Assert.Single(past).Rule);
        }
        else
        {
            Assert.Empty(past);
        }

        Assert.Empty(within);
    }

    [Fact]
    public void The_cap_witness_follows_lock_and_deadline_witnesses()
    {
        // Callers read the first witness as the reason for a prune; the cap is appended
        // after the sorted lock witnesses and after the deadline.
        var policy = Policy(
            Limit,
            finalConfirmationLocks: PromiseLock.PickupEta,
            deadlineSlack: Limit);
        var candidate = Projection(Exogenous + 2 * Limit, pickupMs: 1);

        var witnesses = new CommitmentLockEvaluator().Evaluate(
            WaitingRequest(),
            Published(Projection(Exogenous)),
            Projection(Exogenous),
            candidate,
            new SimTime(0),
            policy,
            anchor: Projection(Exogenous));

        Assert.Collection(
            witnesses,
            witness =>
            {
                Assert.Equal("pickup_eta_ms", witness.Dimension);
                Assert.Equal("final_confirmation", witness.Rule);
            },
            witness => Assert.Equal(CommitmentLockEvaluator.DeadlineCapRule, witness.Rule),
            witness => Assert.Equal(CommitmentLockEvaluator.RevisionCapRule, witness.Rule));
    }

    [Fact]
    public void Extreme_canonical_times_do_not_overflow()
    {
        var zero = Projection(0);
        var max = Projection(DomainLimits.MaxCanonicalInteger);

        Assert.Equal(
            CommitmentLockEvaluator.RevisionCapRule,
            Assert.Single(Evaluate(Policy(Limit), zero, zero, max)).Rule);
        Assert.Equal(
            CommitmentLockEvaluator.RevisionCapRule,
            Assert.Single(Evaluate(Policy(Limit), max, max, zero)).Rule);
        Assert.Empty(
            Evaluate(
                Policy(DomainLimits.MaxCanonicalInteger),
                zero,
                zero,
                max));
    }

    private static IReadOnlyList<CommitmentLockWitness> Evaluate(
        CommitmentPolicy policy,
        PromiseProjection previous,
        PromiseProjection exogenous,
        PromiseProjection candidate) =>
        new CommitmentLockEvaluator().Evaluate(
            WaitingRequest(),
            Published(previous),
            exogenous,
            candidate,
            new SimTime(0),
            policy);

    private static CommitmentPolicy Policy(
        long? limit,
        CommitmentPhase dropPhases = CommitmentPhase.AllActive,
        PromiseLock finalConfirmationLocks = PromiseLock.None,
        long? deadlineSlack = null) =>
        new(
            "policy-1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    dimension == CommitmentDimension.DropEtaTotalMs
                        ? dropPhases
                        : CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            finalConfirmationLocks: finalConfirmationLocks,
            dropEtaDeadlineSlack: deadlineSlack is long slack ? new Duration(slack) : null,
            dropEtaRevisionLimit: limit is long value ? new Duration(value) : null);

    private static RideRequest AcceptedRequest() =>
        RideRequest.CreatePending(
            Request,
            new SimTime(0),
            PickupNode,
            DropNode,
            new SimTime(0),
            new SimTime(600_000),
            new Duration(3_600_000),
            1,
            "service-class-1",
            "policy-1").Value!.Accept(Vehicle).Value!;

    private static RideRequest WaitingRequest() =>
        AcceptedRequest().ConfirmWaitingPickup().Value!;

    private static RideRequest OnboardRequest() =>
        WaitingRequest().Board(Vehicle, new SimTime(100)).Value!;

    private static PromiseProjection Projection(long dropMs, long pickupMs = 0) =>
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
