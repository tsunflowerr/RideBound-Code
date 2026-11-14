using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// The validator passes the rider's initial promise to the deadline rule. The kept route
/// drifts between the initial promise and the reduced state; the candidate is the kept
/// route or, where a test says so, the kept route with the drop delayed by 300 ms.
/// </summary>
public sealed class CommitmentDeadlineValidatorTests
{
    // Pickup window [1000, 2000] ms, 100 ms per arc: at t = 1000 the kept route
    // drops the rider at 1200 ms, at t = 1500 it drops at 1700 ms.
    private const long Early = 1_000;
    private const long Late = 1_500;

    [Fact]
    public void Without_a_deadline_the_same_drift_keeps_the_no_op()
    {
        var context = NoOpUnderDrift(promiseAt: Early, reducedAt: Late, Policy(slack: null));

        Assert.True(new CommitmentDecisionValidator().Validate(context).IsValid);
    }

    [Fact]
    public void The_deadline_is_measured_from_the_first_promise_not_the_latest_revision()
    {
        // First promise drops at 1200 ms, a later revision republished 1700 ms, the kept
        // route still drops at 1700 ms and the candidate at 2000 ms. Against the revision the
        // candidate moved 300 ms, inside the 400 ms slack; against the first promise it moved
        // 800 ms, past the slack, and it is later than the kept route.
        var context = WithCandidateRoute(
            NoOpUnderDrift(
                promiseAt: Early,
                reducedAt: Late,
                Policy(slack: 400),
                revisedAt: Late),
            DelayBeforeDrop);

        var result = new CommitmentDecisionValidator().Validate(context);

        Assert.False(result.IsValid);
        var witness = Assert.Single(result.Witnesses);
        Assert.Equal(CommitmentValidationStage.Lock, witness.Stage);
        Assert.Equal(CommitmentFailureCodes.DeadlineExceeded, witness.Code);
        Assert.Equal(
            "The candidate delays the drop ETA beyond the initial-promise deadline.",
            witness.Message);
        Assert.Equal("drop_eta_ms", witness.Dimension);
        Assert.Equal(CommitmentLockEvaluator.DeadlineCapRule, witness.Rule);
    }

    [Fact]
    public void A_revision_outside_the_slack_does_not_move_the_anchor()
    {
        // First promise drops at 1700 ms, a revision republished 1200 ms, the kept route
        // drops at 1700 ms again and the candidate at 2000 ms: 800 ms late against the
        // revision, but only 300 ms against the first promise, inside the 400 ms slack, so
        // the candidate is kept.
        var context = WithCandidateRoute(
            NoOpUnderDrift(
                promiseAt: Late,
                reducedAt: Late,
                Policy(slack: 400),
                revisedAt: Early),
            DelayBeforeDrop);

        Assert.True(new CommitmentDecisionValidator().Validate(context).IsValid);
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

    /// <summary>A 300 ms waypoint at the pickup node just before the drop: the drop is 300 ms later.</summary>
    private static RoutePlan DelayBeforeDrop(RoutePlan route)
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
                new Duration(300)));
        return RoutePlan.Create(
            new PlanVersion(route.Version.Value + 1),
            route.ExecutedStopCount,
            route.FrozenPrefix,
            stops).Value!;
    }

    private static CommitmentPolicy Policy(long? slack) =>
        new(
            ApplicationTestData.Request().CommitmentPolicyId,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            dropEtaDeadlineSlack: slack is long value ? new Duration(value) : null);

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
