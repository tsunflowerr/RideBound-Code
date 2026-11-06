using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class OnlineEventContractTests
{
    [Fact]
    public void Bootstrap_fixture_decodes_to_typed_payloads_and_round_trips_canonically()
    {
        var bytes = Encoding.UTF8.GetBytes(
            FixtureLoader.ReadUtf8("wp2/valid-bootstrap-event-batch.json"));
        var envelope = ProtocolEnvelopeCodec.Decode(bytes);
        Assert.True(envelope.IsSuccess);

        var decoded = EventBatchPayloadCodec.Decode(envelope.Envelope!.Payload);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.Collection(
            decoded.Value!.Events,
            value =>
            {
                var travel = Assert.IsType<TravelTimesUpdatedEventPayload>(
                    value.Payload);
                Assert.Equal(3, travel.Snapshot.Arcs.Count);
                Assert.Equal("n-0", travel.Snapshot.Arcs[0].FromNodeId);
            },
            value =>
            {
                var arrived = Assert.IsType<RequestArrivedEventPayload>(
                    value.Payload);
                Assert.Equal("r-1", arrived.Request.RequestId);
                Assert.Equal(1, arrived.Request.PartySize);
            },
            value =>
            {
                var advanced = Assert.IsType<VehicleAdvancedEventPayload>(
                    value.Payload);
                Assert.Equal("v-1", advanced.Vehicle.VehicleId);
                Assert.Single(advanced.Vehicle.Route.MutableSuffix);
            });

        var encoded = EventBatchPayloadCodec.Encode(decoded.Value);
        Assert.Equal(
            CanonicalJson.Canonicalize(
                Encoding.UTF8.GetBytes(envelope.Envelope.Payload.GetRawText())),
            CanonicalJson.Canonicalize(encoded));
    }

    [Fact]
    public void Second_epoch_fixture_preserves_route_event_order()
    {
        var payload = DecodeFixture("wp2/valid-second-epoch-event-batch.json");

        Assert.Equal([4L, 5L], payload.Events.Select(value => value.EventSequence.Value));
        Assert.IsType<VehicleReachedStopEventPayload>(payload.Events[0].Payload);
        Assert.IsType<TimerTickEventPayload>(payload.Events[1].Payload);
    }

    [Fact]
    public void Vehicle_snapshot_without_mutable_suffix_has_stable_schema_error()
    {
        var envelope = ProtocolEnvelopeCodec.Decode(
            Encoding.UTF8.GetBytes(
                FixtureLoader.ReadUtf8(
                    "wp2/invalid-vehicle-missing-mutable-suffix.json")));
        Assert.True(envelope.IsSuccess);

        var result = EventBatchPayloadCodec.Decode(envelope.Envelope!.Payload);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.MissingRequiredField, result.Error?.Code);
        Assert.Equal(
            "$.payload.events[0].payload.vehicle.route.mutableSuffix",
            result.Error?.Field);
    }

    [Theory]
    [InlineData(
        """
        {"events":[{"eventSeq":1,"eventType":"timerTick","payload":{"extra":1}}]}
        """,
        ProtocolPayloadErrorCode.UnknownField)]
    [InlineData(
        """
        {"events":[{"eventSeq":1,"eventType":"requestCancelledBeforeAcceptance","payload":{"requestId":null}}]}
        """,
        ProtocolPayloadErrorCode.InvalidFieldType)]
    [InlineData(
        """
        {"events":[{"eventSeq":1,"eventType":"passengerBoarded","payload":{"vehicleId":"v","requestId":"r","planVersion":1.5}}]}
        """,
        ProtocolPayloadErrorCode.ValueOutOfRange)]
    [InlineData(
        """
        {"events":[{"eventSeq":1,"eventType":"vehicleReachedStop","payload":{"vehicleId":"v","stopId":"s","planVersion":0,"position":{"kind":"edgeProgress","fromNodeId":"a","toNodeId":"b","edgeId":"e","progressPermille":5}}}]}
        """,
        ProtocolPayloadErrorCode.InvalidValue)]
    [InlineData(
        """
        {"events":[{"eventSeq":1,"eventType":"requestCancelledBeforeAcceptance","payload":{"requestId":"r","requestId":"r"}}]}
        """,
        ProtocolPayloadErrorCode.DuplicateField)]
    public void Typed_payloads_reject_unknown_null_fraction_wrong_union_and_duplicates(
        string json,
        ProtocolPayloadErrorCode expected)
    {
        using var document = JsonDocument.Parse(json);

        var result = EventBatchPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error?.Code);
    }

    [Fact]
    public void Travel_arcs_and_vehicle_sets_have_canonical_semantic_order()
    {
        Sha256Hex.TryCreate(new string('a', 64), out var hash);
        EventSequence.TryCreate(1, out var sequence);
        var payload = new EventBatchPayload(
            [
                new ProtocolEvent(
                    sequence,
                    EventType.TravelTimesUpdated,
                    new TravelTimesUpdatedEventPayload(
                        new TravelTimeSnapshotContract(
                            1,
                            hash!,
                            [
                                new TravelArcContract("z", "a", 2),
                                new TravelArcContract("a", "z", 1),
                            ]))),
            ]);

        var encoded = EventBatchPayloadCodec.Encode(payload);
        using var document = JsonDocument.Parse(encoded);
        var arcs = document.RootElement
            .GetProperty("events")[0]
            .GetProperty("payload")
            .GetProperty("snapshot")
            .GetProperty("arcs");

        Assert.Equal("a", arcs[0].GetProperty("fromNodeId").GetString());
        Assert.Equal("z", arcs[1].GetProperty("fromNodeId").GetString());
    }

    [Fact]
    public void Event_encoder_rejects_payload_type_mismatch()
    {
        EventSequence.TryCreate(1, out var sequence);
        var payload = new EventBatchPayload(
            [
                new ProtocolEvent(
                    sequence,
                    EventType.TimerTick,
                    new RequestReferenceEventPayload("r-1")),
            ]);

        var error = Assert.Throws<ArgumentException>(
            () => EventBatchPayloadCodec.Encode(payload));

        Assert.Contains("does not match", error.Message);
    }

    [Fact]
    public void Directed_edge_position_round_trips_without_node_downgrade()
    {
        EventSequence.TryCreate(1, out var sequence);
        var vehicle = new VehicleSnapshotContract(
            "v-edge",
            4,
            0,
            new EdgeProgressPositionContract(
                "n-0",
                "n-1",
                "edge-0-1",
                630),
            [],
            [],
            new RoutePlanContract(0, 0, [], []));
        var payload = new EventBatchPayload(
            [
                new ProtocolEvent(
                    sequence,
                    EventType.VehicleAdvanced,
                    new VehicleAdvancedEventPayload(vehicle)),
            ]);

        var encoded = EventBatchPayloadCodec.Encode(payload);
        using var document = JsonDocument.Parse(encoded);
        var decoded = EventBatchPayloadCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        var advanced = Assert.IsType<VehicleAdvancedEventPayload>(
            decoded.Value!.Events.Single().Payload);
        var edge = Assert.IsType<EdgeProgressPositionContract>(
            advanced.Vehicle.Position);
        Assert.Equal("edge-0-1", edge.EdgeId);
        Assert.Equal(630, edge.ProgressPermille);
    }

    private static EventBatchPayload DecodeFixture(string path)
    {
        var envelope = ProtocolEnvelopeCodec.Decode(
            Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path)));
        Assert.True(envelope.IsSuccess);
        var result = EventBatchPayloadCodec.Decode(envelope.Envelope!.Payload);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }
}
