using RideBound.Application.Events;
using RideBound.Application.State;
using RideBound.Domain.Common;
using RideBound.Domain.Incidents;

namespace RideBound.Application.Tests.State;

public sealed class IncidentStateTests
{
    [Fact]
    public void Open_derives_affected_riders_and_resolve_preserves_history()
    {
        var run = ApplicationTestData.InitialState().Run
            .AddRequest(ApplicationTestData.Request()).Value!
            .BootstrapVehicle(ApplicationTestData.Vehicle()).Value!
            .AcceptRequest(
                ApplicationTestData.RequestId,
                ApplicationTestData.VehicleId).Value!
            .AdvanceEpoch(1, new SimTime(1_000)).Value!;
        var state = new OnlineState(
            run,
            ApplicationTestData.Travel(),
            4,
            new string('a', 64),
            RideBound.Domain.Commitments.CommitmentLedger.Empty);
        var id = new IncidentId("incident-1");
        var opened = new EventReducer().Reduce(
            state,
            new InternalEventBatch(
                ApplicationTestData.RunId,
                ApplicationTestData.ScenarioId,
                2,
                new SimTime(1_100),
                [
                    new IncidentOpened(
                        4,
                        new SimTime(1_100),
                        id,
                        "ROAD_CLOSED",
                        [ApplicationTestData.VehicleId]),
                ]));

        Assert.True(opened.IsSuccess, opened.Witness?.Message);
        var incident = opened.ProposedState!.Incidents.Incidents[id];
        Assert.True(incident.IsOpen);
        Assert.Equal(
            ApplicationTestData.RequestId,
            Assert.Single(incident.AffectedRequestIds));

        var resolved = new EventReducer().Reduce(
            opened.ProposedState,
            new InternalEventBatch(
                ApplicationTestData.RunId,
                ApplicationTestData.ScenarioId,
                3,
                new SimTime(1_200),
                [new IncidentResolved(5, new SimTime(1_200), id)]));

        Assert.True(resolved.IsSuccess, resolved.Witness?.Message);
        var retained = resolved.ProposedState!.Incidents.Incidents[id];
        Assert.False(retained.IsOpen);
        Assert.Equal(4, retained.OpenedEventSequence);
        Assert.Equal(5, retained.ResolvedEventSequence);
    }

    [Fact]
    public void Unknown_vehicle_discards_the_whole_incident_batch()
    {
        var coordinator = new EventReductionCoordinator(
            ApplicationTestData.InitialState());
        _ = coordinator.Propose(ApplicationTestData.BootstrapBatch());
        _ = coordinator.ApplyDecisionAcknowledgement(1);
        var before = coordinator.CommittedState;
        var result = coordinator.Propose(
            new InternalEventBatch(
                ApplicationTestData.RunId,
                ApplicationTestData.ScenarioId,
                2,
                new SimTime(1_100),
                [
                    new IncidentOpened(
                        4,
                        new SimTime(1_100),
                        new IncidentId("incident-1"),
                        "ROAD_CLOSED",
                        [new VehicleId("unknown")]),
                ]));

        Assert.False(result.IsSuccess);
        Assert.Equal(
            IncidentFailureCodes.UnknownIncidentVehicle,
            result.Witness?.Code);
        Assert.Same(before, coordinator.CommittedState);
        Assert.Empty(coordinator.CommittedState.Incidents.Incidents);
    }
}
