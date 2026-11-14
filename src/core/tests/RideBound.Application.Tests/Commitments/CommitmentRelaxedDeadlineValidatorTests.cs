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
/// The relaxed deadline (DL-HZ) and the drop budget (rule C) through the real validator and
/// ledger. One rider, pickup window [1000, 2000] ms, 100 ms per arc: at t = 1000 the kept
/// route picks up at 1100 ms and drops at 1200 ms, plus any wait it has before the drop. A
/// candidate changes the route by adding such waits.
/// </summary>
public sealed class CommitmentRelaxedDeadlineValidatorTests
{
    private const long Now = 1_000;
    private const long Budget = 250;

    // Eight tenths of the budget.
    private const long Step = 200;

    [Fact]
    public void Two_successive_changes_of_eight_tenths_of_the_budget_fail_rule_c()
    {
        var ruleC = Policy(dropBudget: Budget);
        var validator = new CommitmentDecisionValidator();

        // Decision 1 delays the rider by 0.8B against the kept route.
        var first = validator.Validate(
            WithCandidateRoute(Decision(ruleC), route => WaitBeforeDrop(route, "first", Step)));

        Assert.True(first.IsValid, string.Join("; ", first.Witnesses.Select(w => w.Message)));
        var entry = Assert.Single(first.Publications).Entry;
        Assert.Equal(1_400, entry.PublishedPromise.Projection.DropEta.Milliseconds);
        Assert.Equal(Step, entry.Deltas.DecisionInduced.DropEtaTotalMs);
        Assert.Equal(Step, entry.BudgetAfter.DropEtaTotalMs);

        // Decision 2 delays the same rider by another 0.8B against the new kept route.
        var second = validator.Validate(
            WithCandidateRoute(
                NextDecision(first.ValidatedState!, ruleC),
                route => WaitBeforeDrop(route, "second", Step)));

        // Rule C charges the ledger: 0.8B + 0.8B > B.
        Assert.False(second.IsValid);
        var witness = Assert.Single(second.Witnesses);
        Assert.Equal(CommitmentValidationStage.Budget, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.BudgetExceeded, witness.Code);
        Assert.Equal("drop_eta_total_ms", witness.Dimension);
        Assert.Equal(Budget, witness.Limit);
        Assert.Equal(Step, witness.Before);
        Assert.Equal(Step, witness.Delta);
        Assert.Equal(2 * Step, witness.After);
    }

    [Fact]
    public void Without_a_drop_limit_a_large_change_is_accepted()
    {
        // 750 ms keeps the ride inside the 1000 ms maximum ride time.
        var context = WithCandidateRoute(
            Decision(Policy()),
            route => WaitBeforeDrop(route, "added", 3 * Budget));

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.True(result.IsValid, string.Join("; ", result.Witnesses.Select(w => w.Message)));
        Assert.Equal(
            3 * Budget,
            Assert.Single(result.Publications).Entry.Deltas.DecisionInduced.DropEtaTotalMs);
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

    private static CommitmentPolicy Policy(
        long? dropBudget = null,
        long? deadlineSlack = null,
        bool relaxed = false) =>
        new(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    dimension == CommitmentDimension.DropEtaTotalMs ? dropBudget : null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: deadlineSlack is long slack ? new Duration(slack) : null,
            dropEtaDeadlineRelaxed: relaxed);

    /// <summary>
    /// The decision at <paramref name="reducedAt"/> on the kept route (pickup, drop), whose first
    /// promise was published at <see cref="Now"/>. The candidate is the kept route unless changed.
    /// </summary>
    private static CommitmentValidationContext Decision(
        CommitmentPolicy policy,
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

    private static RoutePlan Next(RoutePlan route, IReadOnlyList<RouteStop> stops) =>
        RoutePlan.Create(
            new PlanVersion(route.Version.Value + 1),
            route.ExecutedStopCount,
            route.FrozenPrefix,
            stops).Value!;
}
