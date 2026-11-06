using System.Text;
using RideBound.Application.Events;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
using RideBound.Runner.Online;

namespace RideBound.Runner.Tests.Online;

public sealed class OnlineEventMapperTests
{
    [Fact]
    public void Bootstrap_fixture_maps_every_event_without_contract_leak()
    {
        var envelope = Decode("wp2/valid-bootstrap-event-batch.json");

        var result = new OnlineEventMapper().Map(envelope);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        Assert.Collection(
            result.Batch!.Events,
            value => Assert.IsType<TravelTimesUpdated>(value),
            value => Assert.IsType<RequestArrived>(value),
            value => Assert.IsType<VehicleAdvanced>(value));
        Assert.Equal("wp2-run-001", result.Batch.RunId.Value);
        Assert.Equal([1L, 2L, 3L], result.Batch.Events.Select(value => value.EventSequence));
    }

    [Fact]
    public void Incident_fixture_maps_to_typed_internal_event()
    {
        var envelope = Decode(
            "golden/required/08-incident-override/input.json");

        var result = new OnlineEventMapper().Map(envelope);

        Assert.True(result.IsSuccess, result.Witness?.Message);
        var opened = Assert.IsType<IncidentOpened>(
            Assert.Single(result.Batch!.Events));
        Assert.Equal("incident-08", opened.IncidentId.Value);
        Assert.Equal("ROAD_CLOSED", opened.ReasonCode);
        Assert.Equal(["v-08"], opened.VehicleIds.Select(value => value.Value));
    }

    [Fact]
    public void Invalid_typed_payload_is_reported_before_domain_mapping()
    {
        var envelope = Decode(
            "wp2/invalid-vehicle-missing-mutable-suffix.json");

        var result = new OnlineEventMapper().Map(envelope);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_EVENT_PAYLOAD", result.Witness?.Code);
        Assert.Equal(
            "$.payload.events[0].payload.vehicle.route.mutableSuffix",
            result.Witness?.Field);
    }

    [Fact]
    public void Published_two_epoch_fixtures_map_reduce_and_ack_atomically()
    {
        var mapper = new OnlineEventMapper();
        var first = mapper.Map(
            Decode("wp2/valid-bootstrap-event-batch.json"));
        Assert.True(first.IsSuccess, first.Witness?.Message);
        var coordinator = new EventReductionCoordinator(
            OnlineState.Create(
                RideBoundRun.Create(
                    first.Batch!.RunId,
                    first.Batch.ScenarioId,
                    new SimTime(0)),
                new string('a', 64)));

        var firstProposal = coordinator.Propose(first.Batch);
        Assert.True(firstProposal.IsSuccess, firstProposal.Witness?.Message);
        Assert.Empty(coordinator.CommittedState.Run.Vehicles);
        _ = coordinator.ApplyDecisionAcknowledgement(1);

        var second = mapper.Map(
            Decode("wp2/valid-second-epoch-event-batch.json"));
        Assert.True(second.IsSuccess, second.Witness?.Message);
        var secondProposal = coordinator.Propose(second.Batch!);

        Assert.True(secondProposal.IsSuccess, secondProposal.Witness?.Message);
        var vehicle = secondProposal.ProposedState!.Run.Vehicles[
            new VehicleId("v-1")];
        Assert.Equal(1, vehicle.Route.ExecutedStopCount);
        Assert.Empty(vehicle.Route.MutableSuffix);
        Assert.Equal(1, coordinator.CommittedState.Run.AppliedEpoch);
        _ = coordinator.ApplyDecisionAcknowledgement(2);
        Assert.Equal(2, coordinator.CommittedState.Run.AppliedEpoch);
        Assert.Equal(6, coordinator.CommittedState.NextEventSequence);
    }

    private static ProtocolEnvelope Decode(string path)
    {
        var result = ProtocolEnvelopeCodec.Decode(
            Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path)));
        Assert.True(result.IsSuccess);
        return result.Envelope!;
    }
}
