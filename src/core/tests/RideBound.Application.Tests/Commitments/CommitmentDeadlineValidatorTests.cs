using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// The validator passes the rider's initial promise to the deadline rule. The candidate
/// is always the safety no-op (the route is kept), so the decision itself moves nothing;
/// only the kept route drifts between the initial promise and the reduced state.
/// </summary>
public sealed class CommitmentDeadlineValidatorTests
{
    // Pickup window [1000, 2000] ms, 100 ms per arc: at t = 1000 the kept route
    // drops the rider at 1200 ms, at t = 1500 it drops at 1700 ms.
    private const long Early = 1_000;
    private const long Late = 1_500;

    [Fact]
    public void Adverse_drift_past_the_deadline_rejects_the_no_op()
    {
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400));

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.False(result.IsValid);
        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.Lock, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, witness.Code);
        Assert.Equal(
            "The candidate delays the drop ETA beyond the initial-promise deadline.",
            witness.Message);
        Assert.Equal("drop_eta_ms", witness.Dimension);
        Assert.Equal("deadline_cap", witness.Rule);
    }

    [Fact]
    public void Drift_exactly_on_the_deadline_keeps_the_no_op()
    {
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 500));

        Assert.True(new CommitmentDecisionValidator().Validate(context).IsValid);
    }

    [Fact]
    public void Without_a_deadline_the_same_drift_keeps_the_no_op()
    {
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: null));

        Assert.True(new CommitmentDecisionValidator().Validate(context).IsValid);
    }

    [Fact]
    public void Favourable_drift_rejects_the_no_op_only_under_a_two_sided_deadline()
    {
        var twoSided = NoOpUnderDrift(
            promiseAt: Late,
            reducedAt: Early,
            Policy(slack: 400, twoSided: true));
        var oneSided = NoOpUnderDrift(
            promiseAt: Late,
            reducedAt: Early,
            Policy(slack: 400));

        var rejected = new CommitmentDecisionValidator().Validate(twoSided);
        var kept = new CommitmentDecisionValidator().Validate(oneSided);

        Assert.False(rejected.IsValid);
        var witness = Assert.Single(rejected.Witnesses);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, witness.Code);
        Assert.Equal(
            "The candidate moves the drop ETA outside the initial-promise window.",
            witness.Message);
        Assert.Equal("deadline_floor", witness.Rule);
        Assert.True(kept.IsValid);
    }

    [Fact]
    public void The_deadline_is_measured_from_the_first_promise_not_the_latest_revision()
    {
        // First promise drops at 1200 ms, a later revision republished 1700 ms, and the
        // kept route still drops at 1700 ms. Against the revision the no-op moved 0 ms;
        // against the first promise it moved 500 ms, past the 400 ms slack.
        var context = NoOpUnderDrift(
            promiseAt: Early,
            reducedAt: Late,
            Policy(slack: 400),
            revisedAt: Late);

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.False(result.IsValid);
        Assert.Equal(
            CommitmentLockEvaluator.DeadlineCapRule,
            Assert.Single(result.Witnesses).Rule);
    }

    [Fact]
    public void A_revision_outside_the_slack_does_not_move_the_anchor()
    {
        // First promise drops at 1700 ms, a revision republished 1200 ms, and the kept
        // route drops at 1700 ms again: 500 ms late against the revision, but exactly
        // on the first promise, so the no-op is kept.
        var context = NoOpUnderDrift(
            promiseAt: Late,
            reducedAt: Late,
            Policy(slack: 400),
            revisedAt: Early);

        Assert.True(new CommitmentDecisionValidator().Validate(context).IsValid);
    }

    [Fact]
    public void A_forced_reference_vehicle_keeps_its_route_and_records_a_typed_breach()
    {
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400))
            with { ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId } };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        var breach = Assert.Single(result.ForcedBreaches);
        Assert.Equal(CommitmentBreachKind.ForcedReference, breach.Kind);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], breach.WitnessCodes);
        Assert.Same(breach, Assert.Single(result.ValidatedState!.Incidents.Breaches));
        // The rider is told the kept-route drop ETA; the decision itself moved nothing.
        var publication = Assert.Single(result.Publications);
        Assert.Equal(new SimTime(1_700), publication.Entry.PublishedPromise.Projection.DropEta);
        Assert.Equal(CommitmentVector.Zero, breach.Deltas.DecisionInduced);
    }

    [Fact]
    public void A_forced_reference_vehicle_whose_route_changed_is_still_rejected()
    {
        // A changed route that passes every physical check and keeps the rider's drop ETA: a
        // waypoint appended after the drop. Only the deadline rejects it, and the exemption must
        // not apply because the route is no longer the kept route.
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400));
        var vehicle = context.CandidateState.Run.Vehicles[ApplicationTestData.VehicleId];
        var waypoint = new RouteStop(
            new StopId("after-drop"),
            ApplicationTestData.NodeZero,
            RouteStopKind.Waypoint,
            null,
            new Duration(0));
        var changed = RoutePlan.Create(
            new PlanVersion(vehicle.Route.Version.Value + 1),
            vehicle.Route.ExecutedStopCount,
            vehicle.Route.FrozenPrefix,
            vehicle.Route.MutableSuffix.Append(waypoint)).Value!;
        var candidateRun = context.CandidateState.Run
            .UpdateVehicleRoute(ApplicationTestData.VehicleId, changed).Value!;
        var laundering = context with
        {
            CandidateState = context.CandidateState with { Run = candidateRun },
            ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
        };

        var result = new CommitmentDecisionValidator().Validate(laundering);

        Assert.False(result.IsValid);
        Assert.Empty(result.ForcedBreaches);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(result.Witnesses).Code);
    }

    [Fact]
    public void A_forced_reference_breach_under_the_visible_basis_charges_the_overrun()
    {
        var visible = new CommitmentPolicy(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentBudgetBasis.CustomerVisible,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.DropEtaTotalMs ? 400 : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));
        var plain = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, visible);
        var forced = plain with
        {
            ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
        };

        var rejected = new CommitmentDecisionValidator().Validate(plain);
        var kept = new CommitmentDecisionValidator().Validate(forced);

        Assert.False(rejected.IsValid);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, Assert.Single(rejected.Witnesses).Code);
        Assert.True(kept.IsValid);
        var breach = Assert.Single(kept.ForcedBreaches);
        Assert.Equal([CommitmentFailureCodes.BudgetExceeded], breach.WitnessCodes);
        // 500 ms of drift charged against a 400 ms cap: recorded, never reset.
        Assert.Equal(0, breach.BudgetBefore.DropEtaTotalMs);
        Assert.Equal(500, breach.AttemptedBudgetAfter.DropEtaTotalMs);
        Assert.Equal(500, Assert.Single(kept.Publications).Entry.BudgetAfter.DropEtaTotalMs);
    }

    [Fact]
    public void A_later_decision_repeats_the_exemption_but_records_a_breach_only_on_a_revision()
    {
        // Decision 1 keeps the late route (drop 1700 ms, deadline 1600 ms) and records a breach.
        // Decision 2 at the same time changes nothing: the rider is still past the deadline, so
        // the exemption repeats, but no second record is written. Decision 3, 100 ms later,
        // revises the kept-route promise to 1800 ms and records a second breach.
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var first = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400))
            with { ForcedReferenceVehicles = forced };
        var validator = new CommitmentDecisionValidator();
        var committed = validator.Validate(first).ValidatedState!;

        var same = validator.Validate(NextDecision(committed, Late, forced));

        Assert.True(same.IsValid, string.Join("; ", same.Witnesses.Select(w => w.Message)));
        Assert.Empty(same.ForcedBreaches);
        Assert.Empty(same.Publications);
        var exemption = Assert.Single(same.ForcedExemptions);
        Assert.Equal(ApplicationTestData.VehicleId, exemption.VehicleId);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], exemption.WitnessCodes);
        Assert.Single(same.ValidatedState!.Incidents.Breaches);

        var later = validator.Validate(NextDecision(committed, Late + 100, forced));

        Assert.True(later.IsValid, string.Join("; ", later.Witnesses.Select(w => w.Message)));
        var revised = Assert.Single(later.ForcedBreaches);
        Assert.Equal(
            new SimTime(1_800),
            Assert.Single(later.Publications).Entry.PublishedPromise.Projection.DropEta);
        Assert.Equal(2, later.ValidatedState!.Incidents.Breaches.Count);
        Assert.NotEqual(committed.Incidents.Breaches[0].BreachId, revised.BreachId);
    }

    [Fact]
    public void An_unchanged_decision_records_the_first_forced_breach_of_a_rider()
    {
        // A revision already republished the late kept route (1700 ms) without any breach, so
        // this decision changes nothing, yet the rider is past the deadline measured from the
        // first promise (1200 + 400 ms). With no earlier record, the overrun is recorded now.
        var context = NoOpUnderDrift(
                promiseAt: Early,
                reducedAt: Late,
                Policy(slack: 400),
                revisedAt: Late)
            with { ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId } };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        Assert.Empty(result.Publications);
        var breach = Assert.Single(result.ForcedBreaches);
        Assert.Equal(CommitmentVector.Zero, breach.Deltas.Visible);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], breach.WitnessCodes);
    }

    [Fact]
    public void A_phase_change_records_a_new_breach_even_with_the_same_gate_code()
    {
        // Visible basis, 500 ms drift charged at once. The pickup cap applies while accepted,
        // the drop cap only once waiting. Decision 1 (accepted) breaches the pickup cap.
        // Decision 2 confirms the booking without moving any ETA: the drop cap now applies and
        // is exceeded. The code is BUDGET both times, but the overrun is new, so it is recorded;
        // the same decision without the phase change is not.
        var policy = new CommitmentPolicy(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentBudgetBasis.CustomerVisible,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension is CommitmentDimension.PickupEtaTotalMs
                        or CommitmentDimension.DropEtaTotalMs
                        ? 400
                        : null,
                    dimension switch
                    {
                        CommitmentDimension.PickupEtaTotalMs => CommitmentPhase.Accepted,
                        CommitmentDimension.DropEtaTotalMs => CommitmentPhase.WaitingPickup,
                        _ => CommitmentPhase.AllActive,
                    })),
            new MaterialRevisionRule(1, null));
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var validator = new CommitmentDecisionValidator();
        var first = validator.Validate(
            NoOpUnderDrift(promiseAt: Early, reducedAt: Late, policy)
                with { ForcedReferenceVehicles = forced });
        Assert.True(first.IsValid, string.Join("; ", first.Witnesses.Select(w => w.Message)));
        Assert.Equal(
            [CommitmentFailureCodes.BudgetExceeded],
            Assert.Single(first.ForcedBreaches).WitnessCodes);
        var committed = first.ValidatedState!;

        var samePhase = validator.Validate(NextDecision(committed, Late, forced, policy));
        var confirmed = validator.Validate(
            NextDecision(
                committed,
                Late,
                forced,
                policy,
                run => run.ConfirmWaitingPickup(ApplicationTestData.Request().Id).Value!));

        Assert.True(samePhase.IsValid, string.Join("; ", samePhase.Witnesses.Select(w => w.Message)));
        Assert.Empty(samePhase.ForcedBreaches);
        Assert.Single(samePhase.ForcedExemptions);
        Assert.True(confirmed.IsValid, string.Join("; ", confirmed.Witnesses.Select(w => w.Message)));
        Assert.Empty(confirmed.Publications);
        Assert.Equal(
            [CommitmentFailureCodes.BudgetExceeded],
            Assert.Single(confirmed.ForcedBreaches).WitnessCodes);
        Assert.Equal(2, confirmed.ValidatedState!.Incidents.Breaches.Count);
    }

    [Fact]
    public void Collecting_every_witness_does_not_change_a_forced_reference_decision()
    {
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var failFast = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400))
            with { ForcedReferenceVehicles = forced };
        var collectAll = failFast with { CollectAllCommitmentWitnesses = true };

        var expected = new CommitmentDecisionValidator().Validate(failFast);
        var actual = new CommitmentDecisionValidator().Validate(collectAll);

        Assert.True(actual.IsValid, string.Join("; ", actual.Witnesses.Select(w => w.Message)));
        Assert.Equal(
            expected.ForcedBreaches.Select(value => (value.BreachId, string.Join(",", value.WitnessCodes))),
            actual.ForcedBreaches.Select(value => (value.BreachId, string.Join(",", value.WitnessCodes))));
        Assert.Equal(
            expected.Publications.Select(value => value.PublicationId),
            actual.Publications.Select(value => value.PublicationId));
    }

    [Fact]
    public void Without_forced_vehicles_the_validated_state_and_breaches_are_unchanged()
    {
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Early, Policy(slack: 400));

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid);
        Assert.Empty(result.ForcedBreaches);
        Assert.Same(context.CandidateState.Incidents, result.ValidatedState!.Incidents);
    }

    [Fact]
    public void No_worse_recovery_keeps_a_changed_route_that_keeps_the_late_riders_drop()
    {
        // The route a kept-route exemption refuses above (a waypoint after the drop) keeps the late
        // rider's drop at 1700 ms, so under no-worse recovery it is exempted, not rejected.
        var context = WithCandidateRoute(
                NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400)),
                AppendAfterDrop)
            with
            {
                ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
                ForcedNoWorse = true,
            };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        var breach = Assert.Single(result.ForcedBreaches);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, breach.Kind);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], breach.WitnessCodes);
        Assert.False(
            CommitmentBreachRecord.ProjectionsEqual(breach.ExogenousProjection, breach.SafetyProjection));
        Assert.True(Assert.Single(result.ForcedExemptions).NoWorse);
        Assert.Equal(
            new SimTime(1_700),
            Assert.Single(result.Publications).Entry.PublishedPromise.Projection.DropEta);
    }

    [Fact]
    public void No_worse_recovery_rejects_a_changed_route_that_delays_the_late_rider_further()
    {
        // A 300 ms stop before the drop moves the late rider from 1700 to 2000 ms: worse than the
        // kept route, so it stays rejected by the deadline, with no breach.
        var context = WithCandidateRoute(
                NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400)),
                DelayBeforeDrop)
            with
            {
                ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
                ForcedNoWorse = true,
            };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.False(result.IsValid);
        Assert.Empty(result.ForcedBreaches);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(result.Witnesses).Code);
    }

    [Fact]
    public void No_worse_recovery_never_exempts_a_rider_whose_kept_route_meets_the_deadline()
    {
        // No drift: the kept route drops at 1200 ms, inside 1200 + 100 ms. The changed route drops at
        // 1500 ms. The kept route overruns nothing, so there is nothing the changed route may match.
        var context = WithCandidateRoute(
                NoOpUnderDrift(promiseAt: Early, reducedAt: Early, Policy(slack: 100)),
                DelayBeforeDrop)
            with
            {
                ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
                ForcedNoWorse = true,
            };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.False(result.IsValid);
        Assert.Empty(result.ForcedBreaches);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(result.Witnesses).Code);
    }

    [Fact]
    public void No_worse_recovery_under_the_visible_basis_allows_no_more_than_the_kept_overrun()
    {
        // Visible basis, 400 ms cap, 500 ms drift: the kept route charges 500 ms. A changed route that
        // keeps the drop charges the same 500 ms and is exempted; one that adds 300 ms charges 800 ms
        // and stays rejected.
        var visible = new CommitmentPolicy(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentBudgetBasis.CustomerVisible,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.DropEtaTotalMs ? 400 : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null));
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var baseline = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, visible);
        var same = WithCandidateRoute(baseline, AppendAfterDrop)
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };
        var worse = WithCandidateRoute(baseline, DelayBeforeDrop)
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };

        var kept = new CommitmentDecisionValidator().Validate(same);
        var rejected = new CommitmentDecisionValidator().Validate(worse);

        Assert.True(kept.IsValid, string.Join("; ", kept.Witnesses.Select(w => w.Message)));
        var breach = Assert.Single(kept.ForcedBreaches);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, breach.Kind);
        Assert.Equal([CommitmentFailureCodes.BudgetExceeded], breach.WitnessCodes);
        Assert.Equal(500, breach.AttemptedBudgetAfter.DropEtaTotalMs);
        Assert.False(rejected.IsValid);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, Assert.Single(rejected.Witnesses).Code);
    }

    [Fact]
    public void No_worse_recovery_records_an_unchanged_route_as_a_kept_route_breach()
    {
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400))
            with
            {
                ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
                ForcedNoWorse = true,
            };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        Assert.Equal(CommitmentBreachKind.ForcedReference, Assert.Single(result.ForcedBreaches).Kind);
        Assert.False(Assert.Single(result.ForcedExemptions).NoWorse);
    }

    [Fact]
    public void No_worse_recovery_under_a_two_sided_deadline_compares_on_the_floor_side()
    {
        // Favourable drift: first promise 1700 ms, kept route 1200 ms, window 1700 +/- 400 ms, so the
        // kept route is too early. A changed route that keeps 1200 ms is no worse on the floor side
        // and is exempted; one that delays the drop to 1500 ms is back inside the window and needs
        // no exemption at all.
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var baseline = NoOpUnderDrift(promiseAt: Late, reducedAt: Early, Policy(slack: 400, twoSided: true));
        var same = WithCandidateRoute(baseline, AppendAfterDrop)
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };
        var inside = WithCandidateRoute(baseline, DelayBeforeDrop)
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };
        // 1250 ms: still below the 1300 ms floor, but later (less far out) than the kept 1200 ms.
        var better = WithCandidateRoute(baseline, route => DelayBeforeDropBy(route, 50))
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };

        var exempted = new CommitmentDecisionValidator().Validate(same);
        var normal = new CommitmentDecisionValidator().Validate(inside);
        var closer = new CommitmentDecisionValidator().Validate(better);

        Assert.True(exempted.IsValid, string.Join("; ", exempted.Witnesses.Select(w => w.Message)));
        var breach = Assert.Single(exempted.ForcedBreaches);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, breach.Kind);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], breach.WitnessCodes);
        Assert.True(closer.IsValid, string.Join("; ", closer.Witnesses.Select(w => w.Message)));
        Assert.Equal(
            new SimTime(1_250),
            Assert.Single(closer.Publications).Entry.PublishedPromise.Projection.DropEta);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, Assert.Single(closer.ForcedBreaches).Kind);
        Assert.True(normal.IsValid, string.Join("; ", normal.Witnesses.Select(w => w.Message)));
        Assert.Empty(normal.ForcedBreaches);
        Assert.Empty(normal.ForcedExemptions);
    }

    [Fact]
    public void Collecting_every_witness_does_not_change_a_no_worse_decision()
    {
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var failFast = WithCandidateRoute(
                NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400)),
                AppendAfterDrop)
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };
        var collectAll = failFast with { CollectAllCommitmentWitnesses = true };
        var worseFailFast = WithCandidateRoute(
                NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400)),
                DelayBeforeDrop)
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };

        var expected = new CommitmentDecisionValidator().Validate(failFast);
        var actual = new CommitmentDecisionValidator().Validate(collectAll);
        var worse = new CommitmentDecisionValidator().Validate(
            worseFailFast with { CollectAllCommitmentWitnesses = true });

        Assert.True(actual.IsValid, string.Join("; ", actual.Witnesses.Select(w => w.Message)));
        Assert.Equal(
            expected.ForcedBreaches.Select(value => (value.BreachId, value.Kind)),
            actual.ForcedBreaches.Select(value => (value.BreachId, value.Kind)));
        Assert.Equal(
            expected.Publications.Select(value => value.PublicationId),
            actual.Publications.Select(value => value.PublicationId));
        Assert.False(worse.IsValid);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(worse.Witnesses).Code);
    }

    [Fact]
    public void No_worse_recovery_applies_only_to_a_named_forced_vehicle()
    {
        var context = WithCandidateRoute(
                NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: 400)),
                AppendAfterDrop)
            with { ForcedNoWorse = true };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.False(result.IsValid);
        Assert.Empty(result.ForcedBreaches);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(result.Witnesses).Code);
    }

    private static CommitmentValidationContext WithCandidateRoute(
        CommitmentValidationContext context,
        Func<RoutePlan, RoutePlan> change)
    {
        var vehicle = context.CandidateState.Run.Vehicles[ApplicationTestData.VehicleId];
        var run = context.CandidateState.Run
            .UpdateVehicleRoute(ApplicationTestData.VehicleId, change(vehicle.Route)).Value!;
        return context with { CandidateState = context.CandidateState with { Run = run } };
    }

    /// <summary>A waypoint after the drop: the rider's drop ETA is unchanged.</summary>
    private static RoutePlan AppendAfterDrop(RoutePlan route) =>
        RoutePlan.Create(
            new PlanVersion(route.Version.Value + 1),
            route.ExecutedStopCount,
            route.FrozenPrefix,
            route.MutableSuffix.Append(
                new RouteStop(
                    new StopId("after-drop"),
                    ApplicationTestData.NodeZero,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0)))).Value!;

    /// <summary>A 300 ms waypoint at the pickup node just before the drop: the drop is 300 ms later.</summary>
    private static RoutePlan DelayBeforeDrop(RoutePlan route) => DelayBeforeDropBy(route, 300);

    /// <summary>A waypoint of <paramref name="milliseconds"/> at the pickup node just before the drop.</summary>
    private static RoutePlan DelayBeforeDropBy(RoutePlan route, long milliseconds)
    {
        var stops = route.MutableSuffix.ToList();
        var drop = stops.FindIndex(value => value.Kind == RouteStopKind.DropOff);
        stops.Insert(
            drop,
            new RouteStop(
                new StopId("before-drop"),
                ApplicationTestData.NodeOne,
                RouteStopKind.Waypoint,
                null,
                new Duration(milliseconds)));
        return RoutePlan.Create(
            new PlanVersion(route.Version.Value + 1),
            route.ExecutedStopCount,
            route.FrozenPrefix,
            stops).Value!;
    }

    private static CommitmentPolicy Policy(long? slack, bool twoSided = false) =>
        new(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: slack is long value ? new Duration(value) : null,
            dropEtaDeadlineTwoSided: twoSided);

    private static CommitmentValidationContext NoOpUnderDrift(
        long promiseAt,
        long reducedAt,
        CommitmentPolicy policy,
        long? revisedAt = null)
    {
        var request = ApplicationTestData.Request();
        var route = RoutePlan.Create(
            new PlanVersion(0),
            0,
            [],
            [
                new RouteStop(
                    new StopId("pickup"),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Pickup,
                    request.Id,
                    new Duration(0)),
                new RouteStop(
                    new StopId("drop"),
                    ApplicationTestData.NodeTwo,
                    RouteStopKind.DropOff,
                    request.Id,
                    new Duration(0)),
            ]).Value!;
        var vehicle = VehicleState.Create(
            ApplicationTestData.VehicleId,
            4,
            0,
            new NodePosition(ApplicationTestData.NodeZero),
            [],
            [],
            route,
            1).Value!;
        var run = RideBoundRun.Create(
            ApplicationTestData.RunId,
            ApplicationTestData.ScenarioId,
            new SimTime(Early));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(request.Id, vehicle.Id).Value!;
        run = run.AdvanceEpoch(1, new SimTime(Early)).Value!;
        var travel = ApplicationTestData.Travel();

        // The initial promise is the kept route projected at `promiseAt`.
        var initial = Project(run, vehicle.Id, route, travel, request.Id, promiseAt);
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "initial-publication",
            initial,
            1,
            new SimTime(Early),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;

        // An optional revision republishes the kept route projected at `revisedAt`.
        if (revisedAt is long revisionTime)
        {
            var revised = Project(run, vehicle.Id, route, travel, request.Id, revisionTime);
            ledger = ledger.AppendRevision(
                "revision-publication",
                request.Id,
                new PromiseVersion(1),
                revised,
                revised,
                new ThreeWayPromiseDelta(
                    CommitmentVector.Zero,
                    CommitmentVector.Zero,
                    CommitmentVector.Zero),
                CommitmentBudgetBasis.DecisionInduced,
                1,
                new SimTime(Early),
                "REPLAN",
                3).Ledger!;
            Assert.Equal(2, ledger.Histories[request.Id].Entries.Count);
        }

        var before = new OnlineState(
            run,
            travel,
            4,
            travel.SnapshotHash,
            ledger);

        // The exogenous projection is the same kept route projected at `reducedAt`.
        var reducedRun = run.AdvanceEpoch(2, new SimTime(Math.Max(Early, reducedAt))).Value!;
        var reduced = before with
        {
            Run = reducedRun,
            NextEventSequence = 5,
        };

        return new CommitmentValidationContext(
            before,
            reduced,
            reduced,
            new CommitmentPolicyCatalog([policy]),
            CommitmentValidatorFixtures.EmptyDistances.Instance,
            "test-scope",
            4);
    }

    /// <summary>
    /// The next decision after <paramref name="committed"/>: one epoch later at
    /// <paramref name="at"/>, keeping the same route.
    /// </summary>
    private static CommitmentValidationContext NextDecision(
        OnlineState committed,
        long at,
        IReadOnlySet<VehicleId> forced,
        CommitmentPolicy? policy = null,
        Func<RideBoundRun, RideBoundRun>? events = null)
    {
        var run = committed.Run.AdvanceEpoch(
            committed.Run.AppliedEpoch + 1,
            new SimTime(at)).Value!;
        var reduced = committed with
        {
            Run = events is null ? run : events(run),
            NextEventSequence = committed.NextEventSequence + 1,
        };

        return new CommitmentValidationContext(
            committed,
            reduced,
            reduced,
            new CommitmentPolicyCatalog([policy ?? Policy(slack: 400)]),
            CommitmentValidatorFixtures.EmptyDistances.Instance,
            $"test-scope-{at}",
            committed.NextEventSequence,
            ForcedReferenceVehicles: forced);
    }

    private static PromiseProjection Project(
        RideBoundRun run,
        VehicleId vehicleId,
        RoutePlan route,
        ITravelTimeLookup travel,
        RequestId requestId,
        long at)
    {
        var schedule = new RouteScheduleProjector().Project(
            run,
            run.Vehicles[vehicleId],
            route,
            travel,
            new SimTime(at)).Schedule!;
        return new PromiseProjector().Project(
            run,
            run.Vehicles[vehicleId],
            route,
            schedule,
            requestId).Value!;
    }
}
