using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// Slack-proportional budgets (Elmi et al. 2026 Ordinary Control, wf14 formalisation) through the
/// real validator and ledger. One rider, max ride 1000 ms, 100 ms per arc: the first promise at
/// t = 1000 picks up at 1100 ms and drops at 1200 ms, so the first-promise slack is
/// 1100 + 1000 - 1200 = 900 ms and 35% of it is round(315.0) = 315 ms.
/// </summary>
public sealed class CommitmentSlackProportionalValidatorTests
{
    private const long Now = 1_000;
    private const long RiderBudget = 315;

    [Fact]
    public void Rule_c_with_a_rider_budget_charges_the_ledger_against_the_riders_own_limit()
    {
        var policy = Policy(budgetPermille: 350);
        var validator = new CommitmentDecisionValidator();

        var first = validator.Validate(WithCandidateRoute(Decision(policy), route => WaitBeforeDrop(route, "first", 200)));
        Assert.True(first.IsValid, string.Join("; ", first.Witnesses.Select(w => w.Message)));
        Assert.Equal(200, Assert.Single(first.Publications).Entry.BudgetAfter.DropEtaTotalMs);

        var second = validator.Validate(
            WithCandidateRoute(NextDecision(first.ValidatedState!, policy), route => WaitBeforeDrop(route, "second", 200)));

        Assert.False(second.IsValid);
        var witness = Assert.Single(second.Witnesses);
        Assert.Equal(CommitmentValidationStage.Budget, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
        Assert.Equal("drop_eta_total_ms", witness.Dimension);
        Assert.Equal(RiderBudget, witness.Limit);
        Assert.Equal(200, witness.Before);
        Assert.Equal(200, witness.Delta);
        Assert.Equal(400, witness.After);
    }

    [Theory]
    [InlineData(RiderBudget, true)]
    [InlineData(RiderBudget + 1, false)]
    public void A_rider_budget_passes_exactly_at_its_limit_and_fails_one_millisecond_past_it(long delay, bool expectValid)
    {
        var result = new CommitmentDecisionValidator().Validate(
            WithCandidateRoute(Decision(Policy(budgetPermille: 350)), route => WaitBeforeDrop(route, "added", delay)));

        Assert.Equal(expectValid, result.IsValid);

        if (!expectValid)
        {
            var witness = Assert.Single(result.Witnesses);
            Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
            Assert.Equal(RiderBudget, witness.Limit);
        }
    }

    [Theory]
    [InlineData(RiderBudget, true)]
    [InlineData(RiderBudget + 1, false)]
    public void A_relaxed_rider_deadline_passes_exactly_at_drop0_plus_the_rider_budget(long delay, bool expectValid)
    {
        var result = new CommitmentDecisionValidator().Validate(
            WithCandidateRoute(
                Decision(Policy(deadlinePermille: 350, relaxed: true)),
                route => WaitBeforeDrop(route, "added", delay)));

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
    public void Drift_past_a_relaxed_rider_deadline_keeps_the_kept_route_and_blocks_any_later_drop()
    {
        // The kept route drifts from 1200 to 1700 ms, past drop0 + 315 = 1515 ms.
        var policy = Policy(deadlinePermille: 350, relaxed: true);
        var kept = new CommitmentDecisionValidator().Validate(Decision(policy, reducedAt: Now + 500));
        var later = new CommitmentDecisionValidator().Validate(
            WithCandidateRoute(Decision(policy, reducedAt: Now + 500), route => WaitBeforeDrop(route, "added", 1)));

        Assert.True(kept.IsValid, string.Join("; ", kept.Witnesses.Select(w => w.Message)));
        Assert.Empty(kept.ForcedExemptions);
        Assert.False(later.IsValid);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(later.Witnesses).Code);
    }

    [Fact]
    public void A_strict_rider_deadline_rejects_drift_past_it_and_a_forced_vehicle_exempts_it()
    {
        var policy = Policy(deadlinePermille: 350);
        var plain = new CommitmentDecisionValidator().Validate(Decision(policy, reducedAt: Now + 500));
        var forced = new CommitmentDecisionValidator().Validate(
            Decision(policy, reducedAt: Now + 500) with
            {
                ForcedReferenceVehicles = new HashSet<VehicleId> { ApplicationTestData.VehicleId },
            });

        Assert.False(plain.IsValid);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, Assert.Single(plain.Witnesses).Code);
        Assert.True(forced.IsValid, string.Join("; ", forced.Witnesses.Select(w => w.Message)));
        Assert.NotEmpty(forced.ForcedExemptions);
    }

    [Fact]
    public void Rider_budgets_decide_exactly_as_fixed_limits_of_the_same_value()
    {
        foreach (var delay in new long[] { 0, 50, 200, 314, 315, 316, 400, 900 })
        {
            foreach (var (proportional, fixedPolicy) in new[]
                     {
                         (Policy(budgetPermille: 350), Policy(dropBudget: RiderBudget)),
                         (Policy(deadlinePermille: 350, relaxed: true), Policy(deadlineSlack: RiderBudget, relaxed: true)),
                         (Policy(deadlinePermille: 350), Policy(deadlineSlack: RiderBudget)),
                     })
            {
                var a = new CommitmentDecisionValidator().Validate(
                    WithCandidateRoute(Decision(proportional), route => WaitBeforeDrop(route, "added", delay)));
                var b = new CommitmentDecisionValidator().Validate(
                    WithCandidateRoute(Decision(fixedPolicy), route => WaitBeforeDrop(route, "added", delay)));

                Assert.Equal(b.IsValid, a.IsValid);
                Assert.Equal(
                    b.Witnesses.Select(w => (w.Code, w.Rule, w.Limit, w.After)),
                    a.Witnesses.Select(w => (w.Code, w.Rule, w.Limit, w.After)));
            }
        }
    }

    [Fact]
    public void No_worse_recovery_under_a_rider_budget_allows_no_more_than_the_kept_overrun()
    {
        // Visible basis, 500 ms drift: the kept route charges 500 ms, past the rider's 315 ms. A
        // changed route that keeps the drop charges the same 500 ms and is exempted; one that adds
        // 300 ms charges 800 ms and stays rejected. A fixed 315 ms limit decides the same way.
        var forced = new HashSet<VehicleId> { ApplicationTestData.VehicleId };

        foreach (var (policy, isProportional) in new[]
                 {
                     (Policy(budgetPermille: 350, basis: CommitmentBudgetBasis.CustomerVisible), true),
                     (Policy(dropBudget: RiderBudget, basis: CommitmentBudgetBasis.CustomerVisible), false),
                 })
        {
            var baseline = Decision(policy, reducedAt: Now + 500);
            var kept = new CommitmentDecisionValidator().Validate(
                WithCandidateRoute(baseline, AppendAfterDrop)
                    with { ForcedReferenceVehicles = forced, ForcedNoWorse = true });
            var worse = new CommitmentDecisionValidator().Validate(
                WithCandidateRoute(baseline, route => WaitBeforeDrop(route, "added", 300))
                    with { ForcedReferenceVehicles = forced, ForcedNoWorse = true });

            Assert.True(kept.IsValid, $"{isProportional}: " + string.Join("; ", kept.Witnesses.Select(w => w.Message)));
            var breach = Assert.Single(kept.ForcedBreaches);
            Assert.Equal(CommitmentBreachKind.ForcedNoWorse, breach.Kind);
            Assert.Equal([CommitmentFailureCodes.BudgetExceeded], breach.WitnessCodes);
            Assert.Equal(500, breach.AttemptedBudgetAfter.DropEtaTotalMs);
            Assert.True(Assert.Single(kept.ForcedExemptions).NoWorse);
            Assert.False(worse.IsValid);
            Assert.Empty(worse.ForcedBreaches);
            var witness = Assert.Single(worse.Witnesses);
            Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
            Assert.Equal(RiderBudget, witness.Limit);
            Assert.Equal(800, witness.After);
        }
    }

    [Fact]
    public void A_two_sided_rider_deadline_holds_the_floor_at_drop0_minus_the_rider_budget()
    {
        // A 500 ms dwell before the drop makes the first promise 1100/1700 ms, so the rider's slack is
        // 1100 + 1000 - 1700 = 400 ms and 35% of it is 140 ms: window 1560..1840 ms. Shortening the
        // dwell by 140 ms drops at 1560 ms (allowed); by 141 ms, at 1559 ms (too early). A one-sided
        // deadline allows both, and a fixed two-sided 140 ms slack decides the same way.
        foreach (var (cut, expectValid) in new[] { (1L, true), (140L, true), (141L, false), (500L, false) })
        {
            var proportional = new CommitmentDecisionValidator().Validate(
                WithCandidateRoute(
                    Decision(Policy(deadlinePermille: 350, twoSided: true), initialDwell: 500),
                    route => ShortenDwell(route, cut)));
            var fixedSlack = new CommitmentDecisionValidator().Validate(
                WithCandidateRoute(
                    Decision(Policy(deadlineSlack: 140, twoSided: true), initialDwell: 500),
                    route => ShortenDwell(route, cut)));
            var oneSided = new CommitmentDecisionValidator().Validate(
                WithCandidateRoute(
                    Decision(Policy(deadlinePermille: 350), initialDwell: 500),
                    route => ShortenDwell(route, cut)));

            Assert.True(expectValid == proportional.IsValid, $"cut {cut}: " + string.Join("; ", proportional.Witnesses.Select(w => $"{w.Code} {w.Rule} {w.Limit} {w.After} {w.Message}")));
            Assert.Equal(
                fixedSlack.Witnesses.Select(w => (w.Code, w.Rule, w.Limit, w.After)),
                proportional.Witnesses.Select(w => (w.Code, w.Rule, w.Limit, w.After)));
            Assert.Equal(fixedSlack.IsValid, proportional.IsValid);
            Assert.True(oneSided.IsValid, string.Join("; ", oneSided.Witnesses.Select(w => w.Message)));

            if (!expectValid)
            {
                var witness = Assert.Single(proportional.Witnesses);
                Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, witness.Code);
            }
        }
    }

    [Theory]
    [InlineData(RiderBudget, true)]
    [InlineData(RiderBudget + 1, false)]
    public void An_onboard_rider_keeps_the_budget_measured_on_the_first_promise(long delay, bool expectValid)
    {
        // Boarded at the promised 1100 ms; only the drop remains. The budget is still 35% of the
        // first-promise slack (315 ms), not re-measured from the onboard state.
        var result = new CommitmentDecisionValidator().Validate(
            WithCandidateRoute(
                Decision(Policy(budgetPermille: 350), reducedAt: 1_100, boardAt: 1_100),
                route => WaitBeforeDrop(route, "added", delay)));
        var fixedLimit = new CommitmentDecisionValidator().Validate(
            WithCandidateRoute(
                Decision(Policy(dropBudget: RiderBudget), reducedAt: 1_100, boardAt: 1_100),
                route => WaitBeforeDrop(route, "added", delay)));

        Assert.Equal(
            RequestLifecycle.Onboard,
            Decision(Policy(budgetPermille: 350), reducedAt: 1_100, boardAt: 1_100)
                .CandidateState.Run.Requests[ApplicationTestData.Request().Id].Lifecycle);
        Assert.Equal(expectValid, result.IsValid);
        Assert.Equal(fixedLimit.IsValid, result.IsValid);

        if (!expectValid)
        {
            var witness = Assert.Single(result.Witnesses);
            Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
            Assert.Equal(RiderBudget, witness.Limit);
        }
    }

    private static CommitmentPolicy Policy(
        int? budgetPermille = null,
        int? deadlinePermille = null,
        long? dropBudget = null,
        long? deadlineSlack = null,
        bool relaxed = false,
        bool twoSided = false,
        CommitmentBudgetBasis basis = CommitmentBudgetBasis.DecisionInduced) =>
        new(
            ApplicationTestData.Request().CommitmentPolicyId,
            basis,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.DropEtaTotalMs ? dropBudget : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: deadlineSlack is long slack ? new Duration(slack) : null,
            dropEtaDeadlineTwoSided: twoSided,
            dropEtaDeadlineRelaxed: relaxed,
            dropEtaBudgetSlackPermille: budgetPermille,
            dropEtaDeadlineSlackPermille: deadlinePermille);

    /// <summary>
    /// The decision at <paramref name="reducedAt"/> on the kept route (pickup, drop), whose first
    /// promise was published at <see cref="Now"/>. Same fixture as CommitmentRevisionCapValidatorTests.
    /// <paramref name="initialDwell"/> adds a waypoint dwell before the drop (in the first promise and
    /// the kept route); <paramref name="boardAt"/> boards the rider at that time before the decision.
    /// </summary>
    private static CommitmentValidationContext Decision(
        CommitmentPolicy policy,
        long reducedAt = Now,
        long initialDwell = 0,
        long? boardAt = null)
    {
        var request = ApplicationTestData.Request();
        var stops = new List<RouteStop>
        {
            new(new StopId("pickup"), ApplicationTestData.NodeOne, RouteStopKind.Pickup, request.Id, new Duration(0)),
            new(new StopId("drop"), ApplicationTestData.NodeTwo, RouteStopKind.DropOff, request.Id, new Duration(0)),
        };

        if (initialDwell > 0)
        {
            stops.Insert(
                1,
                new RouteStop(new StopId("dwell"), ApplicationTestData.NodeOne, RouteStopKind.Waypoint, null, new Duration(initialDwell)));
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
        var run = RideBoundRun.Create(ApplicationTestData.RunId, ApplicationTestData.ScenarioId, new SimTime(Now));
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
        var initial = new PromiseProjector().Project(run, run.Vehicles[vehicle.Id], route, schedule, request.Id).Value!;
        Assert.Equal(1_100, initial.PickupEta.Milliseconds);
        Assert.Equal(1_200 + initialDwell, initial.DropEta.Milliseconds);
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "initial-publication",
            initial,
            1,
            new SimTime(Now),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var before = new OnlineState(run, travel, 4, travel.SnapshotHash, ledger);
        var reducedRun = run;

        if (boardAt is long boarded)
        {
            reducedRun = reducedRun.ConfirmWaitingPickup(request.Id).Value!;
            reducedRun = reducedRun.ReachStop(
                vehicle.Id,
                new StopId("pickup"),
                new PlanVersion(0),
                new NodePosition(ApplicationTestData.NodeOne),
                2).Value!;
            reducedRun = reducedRun.Board(vehicle.Id, request.Id, new PlanVersion(0), new SimTime(boarded)).Value!;
        }

        var reduced = before with
        {
            Run = reducedRun.AdvanceEpoch(2, new SimTime(reducedAt)).Value!,
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

    private static CommitmentValidationContext NextDecision(OnlineState committed, CommitmentPolicy policy)
    {
        var reduced = committed with
        {
            Run = committed.Run.AdvanceEpoch(committed.Run.AppliedEpoch + 1, committed.Run.SimulationTime).Value!,
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
        var run = context.CandidateState.Run.UpdateVehicleRoute(ApplicationTestData.VehicleId, change(vehicle.Route)).Value!;
        return context with { CandidateState = context.CandidateState with { Run = run } };
    }

    private static RoutePlan WaitBeforeDrop(RoutePlan route, string stopId, long milliseconds)
    {
        var stops = route.MutableSuffix.ToList();
        stops.Insert(
            stops.FindIndex(value => value.Kind == RouteStopKind.DropOff),
            new RouteStop(new StopId(stopId), ApplicationTestData.NodeOne, RouteStopKind.Waypoint, null, new Duration(milliseconds)));
        return RoutePlan.Create(new PlanVersion(route.Version.Value + 1), route.ExecutedStopCount, route.FrozenPrefix, stops).Value!;
    }

    /// <summary>A changed route that keeps the drop time: a zero-dwell waypoint after the drop.</summary>
    private static RoutePlan AppendAfterDrop(RoutePlan route)
    {
        var stops = route.MutableSuffix.ToList();
        stops.Add(new RouteStop(new StopId("after"), ApplicationTestData.NodeZero, RouteStopKind.Waypoint, null, new Duration(0)));
        return RoutePlan.Create(new PlanVersion(route.Version.Value + 1), route.ExecutedStopCount, route.FrozenPrefix, stops).Value!;
    }

    private static RoutePlan ShortenDwell(RoutePlan route, long milliseconds)
    {
        var stops = route.MutableSuffix
            .Select(value => value.StopId == new StopId("dwell")
                ? new RouteStop(value.StopId, value.NodeId, value.Kind, value.RequestId, new Duration(value.ServiceDuration.Milliseconds - milliseconds))
                : value)
            .ToList();
        return RoutePlan.Create(new PlanVersion(route.Version.Value + 1), route.ExecutedStopCount, route.FrozenPrefix, stops).Value!;
    }
}
