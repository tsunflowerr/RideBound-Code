using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class ProtocolEnvelopeTests
{
    [Fact]
    public void Decodes_valid_envelope_without_interpreting_payload()
    {
        var fixture = FixtureLoader.ReadUtf8("protocol/valid-event-batch-envelope.json");

        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        Assert.True(result.IsSuccess);
        var envelope = Assert.IsType<ProtocolEnvelope>(result.Envelope);
        Assert.Equal("1.0.0", envelope.SchemaVersion.ToString());
        Assert.Equal("eventBatch", envelope.MessageType.Value);
        Assert.Equal("Run-A", envelope.RunId?.Value);
        Assert.Equal("Scenario-X", envelope.ScenarioId?.Value);
        Assert.Equal(1, envelope.EpochId?.Value);
        Assert.Equal(500, envelope.SimTime?.Value);
        Assert.Equal(
            "futureEvent",
            envelope.Payload.GetProperty("events")[0].GetProperty("eventType").GetString());
    }

    [Fact]
    public void Round_trip_preserves_typed_identity_and_payload_semantics()
    {
        var fixture = FixtureLoader.ReadUtf8("protocol/valid-event-batch-envelope.json");
        var first = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        var encoded = ProtocolEnvelopeCodec.Encode(first.Envelope!);
        var second = ProtocolEnvelopeCodec.Decode(encoded);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Envelope!.SchemaVersion, second.Envelope!.SchemaVersion);
        Assert.Equal(first.Envelope.MessageType, second.Envelope.MessageType);
        Assert.Equal(first.Envelope.RunId, second.Envelope.RunId);
        Assert.Equal(first.Envelope.ScenarioId, second.Envelope.ScenarioId);
        Assert.Equal(first.Envelope.EpochId, second.Envelope.EpochId);
        Assert.Equal(first.Envelope.SimTime, second.Envelope.SimTime);
        Assert.True(JsonElement.DeepEquals(first.Envelope.Payload, second.Envelope.Payload));
    }

    [Fact]
    public void Unknown_message_type_is_versioned_validation_error()
    {
        var fixture = FixtureLoader.ReadUtf8("protocol/invalid-unknown-message-type.json");

        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolEnvelopeErrorCode.UnknownMessageType, result.Error?.Code);
        Assert.Equal("1.0.0", result.Error?.SchemaVersion?.ToString());
        Assert.Contains("futureMessage", result.Error?.Message);
    }

    [Fact]
    public void Event_sequence_is_rejected_from_batch_envelope()
    {
        var fixture = FixtureLoader.ReadUtf8("protocol/invalid-event-seq-in-envelope.json");

        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolEnvelopeErrorCode.UnknownField, result.Error?.Code);
        Assert.Equal("eventSeq", result.Error?.Field);
    }

    [Theory]
    [InlineData(
        """{"schemaVersion":"1.0","messageType":"hello","payload":{}}""",
        ProtocolEnvelopeErrorCode.InvalidSchemaVersion,
        "schemaVersion")]
    [InlineData(
        """{"schemaVersion":"1.0.0","messageType":"hello"}""",
        ProtocolEnvelopeErrorCode.MissingRequiredField,
        "payload")]
    [InlineData(
        """{"schemaVersion":"1.0.0","messageType":"eventBatch","runId":"r","scenarioId":"s","epochId":0,"simTimeMs":1,"payload":{}}""",
        ProtocolEnvelopeErrorCode.ValueOutOfRange,
        "epochId")]
    [InlineData(
        """{"schemaVersion":"1.0.0","messageType":"hello","runId":"r","payload":{}}""",
        ProtocolEnvelopeErrorCode.FieldNotAllowed,
        "runId")]
    [InlineData(
        """{"schemaVersion":"1.0.0","messageType":"eventBatch","runId":"r","scenarioId":"s","epochId":1,"simTimeMs":9007199254740992,"payload":{}}""",
        ProtocolEnvelopeErrorCode.ValueOutOfRange,
        "simTimeMs")]
    public void Invalid_envelope_has_specific_reason(
        string json,
        ProtocolEnvelopeErrorCode expectedCode,
        string expectedField)
    {
        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error?.Code);
        Assert.Equal(expectedField, result.Error?.Field);
    }

    [Fact]
    public void Opaque_identifiers_are_not_trimmed_or_case_folded()
    {
        const string json =
            """
            {
              "schemaVersion": "1.0.0",
              "messageType": "initializeRun",
              "runId": " Run-A ",
              "scenarioId": "Scenario-X",
              "payload": {}
            }
            """;

        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.True(result.IsSuccess);
        Assert.Equal(" Run-A ", result.Envelope?.RunId?.Value);
        Assert.Equal("Scenario-X", result.Envelope?.ScenarioId?.Value);
    }

    [Fact]
    public void Encoder_rejects_invalid_manually_constructed_envelope()
    {
        var decoded = ProtocolEnvelopeCodec.Decode(
            """{"schemaVersion":"1.0.0","messageType":"hello","payload":{}}"""u8);
        var invalid = decoded.Envelope! with { Payload = default };

        var exception = Assert.Throws<ArgumentException>(
            () => ProtocolEnvelopeCodec.Encode(invalid));

        Assert.Contains("payload", exception.Message);
    }
}
