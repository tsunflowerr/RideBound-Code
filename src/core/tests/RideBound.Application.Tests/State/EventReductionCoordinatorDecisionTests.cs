using RideBound.Application.Events;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;

namespace RideBound.Application.Tests.State;

public sealed class EventReductionCoordinatorDecisionTests
{
    [Fact]
    public void Decision_state_is_staged_and_committed_only_on_acknowledgement()
    {
        var run = RideBoundRun.Create(
            new RunIdentifier("run"),
            new ScenarioIdentifier("scenario"),
            new SimTime(0));
        var initial = OnlineState.Create(run, new string('a', 64));
        var coordinator = new EventReductionCoordinator(initial);
        var batch = new InternalEventBatch(
            run.Id,
            run.ScenarioId,
            1,
            new SimTime(1),
            [
                new RideBound.Application.Events.TravelTimesUpdated(
                    1,
                    new SimTime(1),
                    ApplicationTestData.Travel()),
                new RideBound.Application.Events.VehicleAdvanced(
                    2,
                    new SimTime(1),
                    ApplicationTestData.Vehicle(observedEpoch: 1)),
            ]);
        var reduced = coordinator.Propose(batch);
        Assert.True(reduced.IsSuccess, reduced.Witness?.Message);
        var proposedDecision = reduced.ProposedState! with
        {
            Run = reduced.ProposedState.Run,
        };

        var staged = coordinator.StageDecisionState(
            reduced.ProposedState,
            proposedDecision);

        Assert.True(staged.IsSuccess, staged.Witness?.Message);
        Assert.Same(initial, coordinator.CommittedState);
        Assert.Same(proposedDecision, coordinator.PendingState);
        var acknowledged = coordinator.ApplyDecisionAcknowledgement(1);
        Assert.True(acknowledged.IsSuccess, acknowledged.Witness?.Message);
        Assert.Same(proposedDecision, coordinator.CommittedState);
    }

    [Fact]
    public void Decision_cannot_change_epoch_or_travel_snapshot()
    {
        var run = RideBoundRun.Create(
            new RunIdentifier("run"),
            new ScenarioIdentifier("scenario"),
            new SimTime(0));
        var initial = OnlineState.Create(run, new string('a', 64));
        var coordinator = new EventReductionCoordinator(initial);
        var batch = new InternalEventBatch(
            run.Id,
            run.ScenarioId,
            1,
            new SimTime(1),
            [
                new RideBound.Application.Events.TravelTimesUpdated(
                    1,
                    new SimTime(1),
                    ApplicationTestData.Travel()),
                new RideBound.Application.Events.VehicleAdvanced(
                    2,
                    new SimTime(1),
                    ApplicationTestData.Vehicle(observedEpoch: 1)),
            ]);
        var reduced = coordinator.Propose(batch).ProposedState!;
        var invalid = reduced with { NextEventSequence = 99 };

        var staged = coordinator.StageDecisionState(reduced, invalid);

        Assert.False(staged.IsSuccess);
        Assert.Equal(
            EventReductionFailureCodes.DecisionStateMismatch,
            staged.Witness?.Code);
        Assert.Same(reduced, coordinator.PendingState);
    }
}
