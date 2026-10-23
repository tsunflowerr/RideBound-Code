using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
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

    private static CommitmentPolicy Policy(
        int? budgetPermille = null,
        int? deadlinePermille = null,
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
            dropEtaBudgetSlackPermille: budgetPermille,
            dropEtaDeadlineSlackPermille: deadlinePermille);

    /// <summary>
    /// The decision at <paramref name="reducedAt"/> on the kept route (pickup, drop), whose first
    /// promise was published at <see cref="Now"/>. Same fixture as CommitmentRevisionCapValidatorTests.
    /// </summary>
    private static CommitmentValidationContext Decision(CommitmentPolicy policy, long reducedAt = Now)
    {
        var request = ApplicationTestData.Request();
        var stops = new List<RouteStop>
        {
            new(new StopId("pickup"), ApplicationTestData.NodeOne, RouteStopKind.Pickup, request.Id, new Duration(0)),
            new(new StopId("drop"), ApplicationTestData.NodeTwo, RouteStopKind.DropOff, request.Id, new Duration(0)),
        };
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
        Assert.Equal(1_200, initial.DropEta.Milliseconds);
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
}
