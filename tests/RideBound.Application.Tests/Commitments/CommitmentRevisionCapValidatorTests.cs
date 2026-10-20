using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// The per-decision revision cap (P-b) and the relaxed deadline (DL-HZ) through the real
/// validator and ledger. One rider, pickup window [1000, 2000] ms, 100 ms per arc: at
/// t = 1000 the kept route picks up at 1100 ms and drops at 1200 ms, plus any wait it has
/// before the drop. A candidate changes the route by adding or removing such waits.
/// </summary>
public sealed class CommitmentRevisionCapValidatorTests
{
    private const long Now = 1_000;
    private const long Cap = 250;

    // Eight tenths of the cap.
    private const long Step = 200;

    [Fact]
    public void Two_successive_changes_of_eight_tenths_of_the_cap_pass_the_cap_but_not_rule_c()
    {
        var perStep = Policy(revisionLimit: Cap);
        var ruleC = Policy(dropBudget: Cap);
        var validator = new CommitmentDecisionValidator();

        // Decision 1 delays the rider by 0.8b against the kept route under both policies.
        var perStepFirst = validator.Validate(
            WithCandidateRoute(Decision(perStep), route => WaitBeforeDrop(route, "first", Step)));
        var ruleCFirst = validator.Validate(
            WithCandidateRoute(Decision(ruleC), route => WaitBeforeDrop(route, "first", Step)));

        foreach (var first in new[] { perStepFirst, ruleCFirst })
        {
            Assert.True(first.IsValid, string.Join("; ", first.Witnesses.Select(w => w.Message)));
            var entry = Assert.Single(first.Publications).Entry;
            Assert.Equal(1_400, entry.PublishedPromise.Projection.DropEta.Milliseconds);
            Assert.Equal(Step, entry.Deltas.DecisionInduced.DropEtaTotalMs);
            Assert.Equal(Step, entry.BudgetAfter.DropEtaTotalMs);
        }

        // Decision 2 delays the same rider by another 0.8b against the new kept route.
        var perStepSecond = validator.Validate(
            WithCandidateRoute(
                NextDecision(perStepFirst.ValidatedState!, perStep),
                route => WaitBeforeDrop(route, "second", Step)));
        var ruleCSecond = validator.Validate(
            WithCandidateRoute(
                NextDecision(ruleCFirst.ValidatedState!, ruleC),
                route => WaitBeforeDrop(route, "second", Step)));

        // The cap has no memory: c = 0.8b passes again. The ledger still sums |c| = 1.6b.
        Assert.True(
            perStepSecond.IsValid,
            string.Join("; ", perStepSecond.Witnesses.Select(w => w.Message)));
        var second = Assert.Single(perStepSecond.Publications).Entry;
        Assert.Equal(1_600, second.PublishedPromise.Projection.DropEta.Milliseconds);
        Assert.Equal(Step, second.Deltas.DecisionInduced.DropEtaTotalMs);
        Assert.Equal(2 * Step, second.BudgetAfter.DropEtaTotalMs);

        // Rule C with B = b charges the ledger: 0.8b + 0.8b > b.
        Assert.False(ruleCSecond.IsValid);
        var witness = Assert.Single(ruleCSecond.Witnesses);
        Assert.Equal(CommitmentValidationStage.Budget, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
        Assert.Equal("drop_eta_total_ms", witness.Dimension);
        Assert.Equal(Cap, witness.Limit);
        Assert.Equal(Step, witness.Before);
        Assert.Equal(Step, witness.Delta);
        Assert.Equal(2 * Step, witness.After);
    }

    [Theory]
    [InlineData(Cap, true)]
    [InlineData(Cap + 1, false)]
    public void A_later_drop_passes_exactly_at_the_cap_and_fails_one_millisecond_past_it(
        long delay,
        bool expectValid)
    {
        var context = WithCandidateRoute(
            Decision(Policy(revisionLimit: Cap)),
            route => WaitBeforeDrop(route, "added", delay));

        var result = new CommitmentDecisionValidator().Validate(context);

        AssertCapOutcome(result, expectValid);

        if (expectValid)
        {
            Assert.Equal(
                delay,
                Assert.Single(result.Publications).Entry.Deltas.DecisionInduced.DropEtaTotalMs);
        }
    }

    [Theory]
    [InlineData(Cap, true)]
    [InlineData(Cap + 1, false)]
    public void An_earlier_drop_passes_exactly_at_the_cap_and_fails_one_millisecond_past_it(
        long earlier,
        bool expectValid)
    {
        // The kept route waits before the drop; the candidate removes the wait, so the
        // decision moves the drop ETA earlier: c = -earlier.
        var context = WithCandidateRoute(
            Decision(Policy(revisionLimit: Cap), keptWaitMs: earlier),
            route => RemoveStop(route, KeptWait));

        var result = new CommitmentDecisionValidator().Validate(context);

        AssertCapOutcome(result, expectValid);

        if (expectValid)
        {
            var entry = Assert.Single(result.Publications).Entry;
            Assert.Equal(1_200, entry.PublishedPromise.Projection.DropEta.Milliseconds);
            Assert.Equal(earlier, entry.Deltas.DecisionInduced.DropEtaTotalMs);
        }
    }

    [Fact]
    public void Drift_is_free_so_the_kept_route_passes_even_a_one_millisecond_cap()
    {
        // The kept route drifts from 1200 to 1700 ms. The rider is told 1700 ms, a visible
        // change of 500 ms, but the decision itself changed nothing.
        var context = Decision(Policy(revisionLimit: 1), reducedAt: Now + 500);

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        var entry = Assert.Single(result.Publications).Entry;
        Assert.Equal(1_700, entry.PublishedPromise.Projection.DropEta.Milliseconds);
        Assert.Equal(CommitmentVector.Zero, entry.Deltas.DecisionInduced);
        Assert.Equal(500, entry.Deltas.Visible.DropEtaTotalMs);
    }

    [Fact]
    public void Under_drift_the_cap_counts_only_the_change_the_decision_makes()
    {
        // After 500 ms of drift a candidate adds 200 ms: 700 ms from the previous promise,
        // 200 ms from the kept route, so it passes a 250 ms cap.
        var context = WithCandidateRoute(
            Decision(Policy(revisionLimit: Cap), reducedAt: Now + 500),
            route => WaitBeforeDrop(route, "added", Step));

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        var entry = Assert.Single(result.Publications).Entry;
        Assert.Equal(Step, entry.Deltas.DecisionInduced.DropEtaTotalMs);
        Assert.Equal(500 + Step, entry.Deltas.Visible.DropEtaTotalMs);
    }

    [Fact]
    public void Without_a_limit_a_change_beyond_the_cap_is_accepted()
    {
        // 750 ms keeps the ride inside the 1000 ms maximum ride time.
        var context = WithCandidateRoute(
            Decision(Policy()),
            route => WaitBeforeDrop(route, "added", 3 * Cap));

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        Assert.Equal(
            3 * Cap,
            Assert.Single(result.Publications).Entry.Deltas.DecisionInduced.DropEtaTotalMs);
    }

    [Fact]
    public void A_forced_reference_vehicle_gets_no_exemption_from_the_cap_on_its_kept_route()
    {
        // The cap cannot reject the kept route, so naming the vehicle forced changes nothing:
        // no exemption, no breach, the same publication as without recovery.
        var plain = Decision(Policy(revisionLimit: 1), reducedAt: Now + 500);
        var forced = plain with
        {
            ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
        };
        var noWorse = forced with { ForcedNoWorse = true };

        var expected = new CommitmentDecisionValidator().Validate(plain);

        foreach (var context in new[] { forced, noWorse })
        {
            var result = new CommitmentDecisionValidator().Validate(context);

            Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
            Assert.Empty(result.ForcedExemptions);
            Assert.Empty(result.ForcedBreaches);
            Assert.Empty(result.ValidatedState!.Incidents.Breaches);
            Assert.Equal(
                expected.Publications.Select(value => value.PublicationId),
                result.Publications.Select(value => value.PublicationId));
        }
    }

    [Fact]
    public void No_worse_recovery_never_exempts_the_cap_on_a_changed_route()
    {
        // The kept route waits 300 ms before the drop; the first promise dropped at 1500 ms and
        // 800 ms of drift carried the kept route to 2300 ms, past the 400 ms strict deadline.
        // Removing the wait drops at 2000 ms: still past the deadline but no later than the
        // kept route, so no-worse recovery exempts the deadline. Its own change is -300 ms,
        // which a 100 ms cap rejects; the cap is never exempted.
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var capped = WithCandidateRoute(
                Decision(
                    Policy(deadlineSlack: 400, revisionLimit: 100),
                    keptWaitMs: 300,
                    reducedAt: Now + 800),
                route => RemoveStop(route, KeptWait))
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };
        var uncapped = WithCandidateRoute(
                Decision(Policy(deadlineSlack: 400), keptWaitMs: 300, reducedAt: Now + 800),
                route => RemoveStop(route, KeptWait))
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };

        var rejected = new CommitmentDecisionValidator().Validate(capped);
        var exempted = new CommitmentDecisionValidator().Validate(uncapped);

        Assert.False(rejected.IsValid);
        Assert.Empty(rejected.ForcedBreaches);
        Assert.Contains(
            rejected.Witnesses,
            value => value.Code == CommitmentFailureCodes.RevisionLimitExceeded
                && value.Rule == CommitmentLockEvaluator.RevisionCapRule);
        Assert.True(exempted.IsValid, string.Join("; ", exempted.Witnesses.Select(w => w.Message)));
        var breach = Assert.Single(exempted.ForcedBreaches);
        Assert.Equal(CommitmentBreachKind.ForcedNoWorse, breach.Kind);
        Assert.Equal([CommitmentFailureCodes.DeadlineExceeded], breach.WitnessCodes);
    }

    [Fact]
    public void Collecting_every_witness_reports_the_cap_next_to_a_budget_overrun()
    {
        var context = WithCandidateRoute(
                Decision(Policy(revisionLimit: Cap, dropBudget: Cap)),
                route => WaitBeforeDrop(route, "added", Cap + 1))
            with { CollectAllCommitmentWitnesses = true };

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.False(result.IsValid);
        Assert.Equal(
            [CommitmentFailureCodes.RevisionLimitExceeded, CommitmentFailureCodes.BudgetExceeded],
            result.Witnesses.Select(value => value.Code));
    }

    [Fact]
    public void A_relaxed_deadline_keeps_the_no_op_under_drift_that_the_strict_deadline_rejects()
    {
        // First promise 1200 ms, kept route 1700 ms, slack 400 ms: past the deadline.
        var strict = Decision(Policy(deadlineSlack: 400), reducedAt: Now + 500);
        var relaxed = Decision(Policy(deadlineSlack: 400, relaxed: true), reducedAt: Now + 500);

        var rejected = new CommitmentDecisionValidator().Validate(strict);
        var kept = new CommitmentDecisionValidator().Validate(relaxed);

        Assert.False(rejected.IsValid);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(rejected.Witnesses).Code);
        Assert.True(kept.IsValid, string.Join("; ", kept.Witnesses.Select(w => w.Message)));
        Assert.Equal(
            1_700,
            Assert.Single(kept.Publications).Entry.PublishedPromise.Projection.DropEta.Milliseconds);
    }

    [Theory]
    [InlineData(0, 400, true)]     // no drift: exactly on the 1600 ms deadline
    [InlineData(0, 401, false)]    // no drift: 1 ms past it
    [InlineData(500, 0, true)]     // drift to 1700 ms: a changed route that keeps the drop
    [InlineData(500, 1, false)]    // drift to 1700 ms: 1 ms later than the kept route
    public void A_relaxed_deadline_bounds_a_changed_route_by_the_later_of_deadline_and_kept_route(
        long drift,
        long delay,
        bool expectValid)
    {
        var context = WithCandidateRoute(
            Decision(Policy(deadlineSlack: 400, relaxed: true), reducedAt: Now + drift),
            route => delay == 0 ? AppendAfterDrop(route) : WaitBeforeDrop(route, "added", delay));

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.Equal(expectValid, result.IsValid);

        if (!expectValid)
        {
            var witness = Assert.Single(result.Witnesses);
            Assert.Equal(CommitmentValidationStage.Lock, witness.Stage);
            Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, witness.Code);
            Assert.Equal(CommitmentLockEvaluator.DeadlineCapRule, witness.Rule);
        }
    }

    [Fact]
    public void A_relaxed_deadline_gives_a_forced_vehicle_nothing_to_exempt()
    {
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };
        var keptRoute = Decision(Policy(deadlineSlack: 400, relaxed: true), reducedAt: Now + 500)
            with { ForcedReferenceVehicles = forced };
        var changedRoute = WithCandidateRoute(
                Decision(Policy(deadlineSlack: 400, relaxed: true), reducedAt: Now + 500),
                AppendAfterDrop)
            with { ForcedReferenceVehicles = forced, ForcedNoWorse = true };

        foreach (var context in new[] { keptRoute, changedRoute })
        {
            var result = new CommitmentDecisionValidator().Validate(context);

            Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
            Assert.Empty(result.ForcedExemptions);
            Assert.Empty(result.ForcedBreaches);
        }
    }

    private const string KeptWait = "kept-wait";

    private static void AssertCapOutcome(CommitmentDecisionValidationResult result, bool expectValid)
    {
        Assert.Equal(expectValid, result.IsValid);

        if (expectValid)
        {
            return;
        }

        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.Lock, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.RevisionLimitExceeded, witness.Code);
        Assert.Equal("COMMITMENT_REVISION_LIMIT_EXCEEDED", witness.Code);
        Assert.Equal(
            "The candidate moves the drop ETA beyond the per-decision revision limit.",
            witness.Message);
        Assert.Equal(ApplicationTestData.VehicleId, witness.VehicleId);
        Assert.Equal(ApplicationTestData.RequestId, witness.RequestId);
        Assert.Equal("drop_eta_ms", witness.Dimension);
        Assert.Equal("revision_cap", witness.Rule);
    }

    private static CommitmentPolicy Policy(
        long? revisionLimit = null,
        long? dropBudget = null,
        long? deadlineSlack = null,
        bool relaxed = false) =>
        new(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.DropEtaTotalMs ? dropBudget : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: deadlineSlack is long slack ? new Duration(slack) : null,
            dropEtaDeadlineRelaxed: relaxed,
            dropEtaRevisionLimit: revisionLimit is long limit ? new Duration(limit) : null);

    /// <summary>
    /// The decision at <paramref name="reducedAt"/> on the kept route (pickup, optional wait of
    /// <paramref name="keptWaitMs"/> at the pickup node, drop), whose first promise was
    /// published at <see cref="Now"/>. The candidate is the kept route unless changed.
    /// </summary>
    private static CommitmentValidationContext Decision(
        CommitmentPolicy policy,
        long keptWaitMs = 0,
        long reducedAt = Now)
    {
        var request = ApplicationTestData.Request();
        var stops = new List<RouteStop>
        {
            new(
                new StopId("pickup"),
                ApplicationTestData.NodeOne,
                RouteStopKind.Pickup,
                request.Id,
                new Duration(0)),
            new(
                new StopId("drop"),
                ApplicationTestData.NodeTwo,
                RouteStopKind.DropOff,
                request.Id,
                new Duration(0)),
        };

        if (keptWaitMs > 0)
        {
            stops.Insert(
                1,
                new RouteStop(
                    new StopId(KeptWait),
                    ApplicationTestData.NodeOne,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(keptWaitMs)));
        }

        var route = RoutePlan.Create(new PlanVersion(0), 0, [], stops).Value!;
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
            new SimTime(Now));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(request.Id, vehicle.Id).Value!;
        run = run.AdvanceEpoch(1, new SimTime(Now)).Value!;
        var travel = ApplicationTestData.Travel();
        var schedule = new RouteScheduleProjector().Project(
            run,
            run.Vehicles[vehicle.Id],
            route,
            travel,
            new SimTime(Now)).Schedule!;
        var initial = new PromiseProjector().Project(
            run,
            run.Vehicles[vehicle.Id],
            route,
            schedule,
            request.Id).Value!;
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "initial-publication",
            initial,
            1,
            new SimTime(Now),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var before = new OnlineState(run, travel, 4, travel.SnapshotHash, ledger);
        var reduced = before with
        {
            Run = run.AdvanceEpoch(2, new SimTime(reducedAt)).Value!,
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

    /// <summary>The next decision after <paramref name="committed"/>, at the same time.</summary>
    private static CommitmentValidationContext NextDecision(
        OnlineState committed,
        CommitmentPolicy policy)
    {
        var reduced = committed with
        {
            Run = committed.Run.AdvanceEpoch(
                committed.Run.AppliedEpoch + 1,
                committed.Run.SimulationTime).Value!,
            NextEventSequence = committed.NextEventSequence + 1,
        };

        return new CommitmentValidationContext(
            committed,
            reduced,
            reduced,
            new CommitmentPolicyCatalog([policy]),
            CommitmentValidatorFixtures.EmptyDistances.Instance,
            "test-scope-next",
            committed.NextEventSequence);
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

    /// <summary>A wait of <paramref name="milliseconds"/> at the pickup node just before the drop.</summary>
    private static RoutePlan WaitBeforeDrop(RoutePlan route, string stopId, long milliseconds)
    {
        var stops = route.MutableSuffix.ToList();
        stops.Insert(
            stops.FindIndex(value => value.Kind == RouteStopKind.DropOff),
            new RouteStop(
                new StopId(stopId),
                ApplicationTestData.NodeOne,
                RouteStopKind.Waypoint,
                null,
                new Duration(milliseconds)));
        return Next(route, stops);
    }

    /// <summary>A waypoint after the drop: the rider's drop ETA is unchanged.</summary>
    private static RoutePlan AppendAfterDrop(RoutePlan route) =>
        Next(
            route,
            route.MutableSuffix.Append(
                new RouteStop(
                    new StopId("after-drop"),
                    ApplicationTestData.NodeZero,
                    RouteStopKind.Waypoint,
                    null,
                    new Duration(0))).ToList());

    private static RoutePlan RemoveStop(RoutePlan route, string stopId) =>
        Next(route, route.MutableSuffix.Where(value => value.StopId.Value != stopId).ToList());

    private static RoutePlan Next(RoutePlan route, IReadOnlyList<RouteStop> stops) =>
        RoutePlan.Create(
            new PlanVersion(route.Version.Value + 1),
            route.ExecutedStopCount,
            route.FrozenPrefix,
            stops).Value!;
}
