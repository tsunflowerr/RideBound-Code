using RideBound.Application.Commitments;
using RideBound.Application.Promises;
using RideBound.Application.Scheduling;
using RideBound.Application.State;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Requests;
using RideBound.Domain.Routes;
using RideBound.Domain.Runs;
using RideBound.Domain.Validation;
using RideBound.Domain.Vehicles;

namespace RideBound.Application.Tests.Commitments;

/// <summary>
/// Shared commitment-validator fixture. One accepted request whose drop stop is
/// pushed ten milliseconds later by the candidate, so the drop-ETA dimension is
/// the only one that can consume budget.
/// </summary>
internal static class CommitmentValidatorFixtures
{
    internal static Fixture OverBudget() => WithHardLimit(0);

    /// <summary>
    /// Same candidate, but the policy freezes the drop ETA inside a horizon that
    /// certainly covers this run and also gives it a zero budget. The candidate
    /// therefore breaks the lock layer and the budget layer at once, which is the
    /// case fail-fast can only ever report half of.
    /// </summary>
    internal static Fixture LockAndBudget() =>
        WithHardLimit(
            0,
            freezeHorizon: new Duration(3_600_000),
            freezeLocks: PromiseLock.DropEta);

    internal static Fixture WithHardLimit(
        long? hardLimit,
        Duration? freezeHorizon = null,
        PromiseLock freezeLocks = PromiseLock.None)
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
            new SimTime(1_000));
        run = run.AddRequest(request).Value!;
        run = run.BootstrapVehicle(vehicle).Value!;
        run = run.AcceptRequest(request.Id, vehicle.Id).Value!;
        run = run.AdvanceEpoch(1, new SimTime(1_000)).Value!;
        var travel = ApplicationTestData.Travel();
        var schedule = new RouteScheduleProjector().Project(
            run,
            run.Vehicles[vehicle.Id],
            route,
            travel,
            run.SimulationTime).Schedule!;
        var projection = new PromiseProjector().Project(
            run,
            run.Vehicles[vehicle.Id],
            route,
            schedule,
            request.Id).Value!;
        var ledger = CommitmentLedger.Empty.OpenInitial(
            "initial-publication",
            projection,
            1,
            new SimTime(1_000),
            "INITIAL_ACCEPTANCE",
            3).Ledger!;
        var before = new OnlineState(
            run,
            travel,
            4,
            travel.SnapshotHash,
            ledger);
        var reducedRun = run.AdvanceEpoch(2, new SimTime(1_000)).Value!;
        var reduced = before with
        {
            Run = reducedRun,
            NextEventSequence = 5,
        };
        var changedRoute = RoutePlan.Create(
            new PlanVersion(1),
            0,
            [],
            [
                new RouteStop(
                    route.MutableSuffix[0].StopId,
                    route.MutableSuffix[0].NodeId,
                    route.MutableSuffix[0].Kind,
                    route.MutableSuffix[0].RequestId,
                    new Duration(10)),
                route.MutableSuffix[1],
            ]).Value!;
        var changedRun = reducedRun.UpdateVehicleRoute(
            vehicle.Id,
            changedRoute).Value!;
        var candidate = reduced with { Run = changedRun };
        var policy = new CommitmentPolicy(
            request.CommitmentPolicyId,
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    hardLimit,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1, null),
            freezeHorizon,
            freezeLocks);

        return new Fixture(
            new CommitmentValidationContext(
                before,
                reduced,
                candidate,
                new CommitmentPolicyCatalog([policy]),
                EmptyDistances.Instance,
                "test-scope",
                4));
    }

    internal sealed record Fixture(CommitmentValidationContext Context);

    internal sealed class EmptyDistances : IStopDistanceLookup
    {
        public static EmptyDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
