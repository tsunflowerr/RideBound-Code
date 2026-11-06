using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class HelloMessageTests
{
    [Fact]
    public void Hello_fixture_decodes_and_normalizes_semantic_sets()
    {
        var envelope = DecodeEnvelope("hello/valid-hello.json");

        var result = HelloPayloadCodec.Decode(envelope.Payload);

        Assert.True(result.IsSuccess);
        var hello = result.Value!;
        Assert.Equal("fleetpy-ridebound", hello.AdapterId);
        Assert.Equal(PositionModel.DirectedEdgeProgress, hello.PositionModel);
        Assert.Equal(
            [
                CapabilityId.Cancellations,
                CapabilityId.DynamicTravelTimes,
                CapabilityId.ExactEventOrdering,
                CapabilityId.OldPlanProjection,
            ],
            hello.Capabilities);
    }

    [Fact]
    public void Hello_round_trip_preserves_contract_semantics()
    {
        var first = HelloPayloadCodec.Decode(
            DecodeEnvelope("hello/valid-hello.json").Payload);

        using var encodedDocument = JsonDocument.Parse(
            HelloPayloadCodec.Encode(first.Value!));
        var second = HelloPayloadCodec.Decode(encodedDocument.RootElement);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.AdapterId, second.Value!.AdapterId);
        Assert.Equal(first.Value.AdapterVersion, second.Value.AdapterVersion);
        Assert.Equal(first.Value.PositionModel, second.Value.PositionModel);
        Assert.Equal(first.Value.MaxFleetSize, second.Value.MaxFleetSize);
        Assert.Equal(first.Value.MaxRequestCount, second.Value.MaxRequestCount);
        Assert.Equal(first.Value!.Capabilities, second.Value!.Capabilities);
        Assert.Equal(
            first.Value.SupportedSchemaVersions,
            second.Value.SupportedSchemaVersions);
    }

    [Fact]
    public void Unknown_offered_capability_is_not_silently_defaulted()
    {
        var envelope = DecodeEnvelope("hello/invalid-unknown-capability.json");

        var result = HelloPayloadCodec.Decode(envelope.Payload);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.InvalidValue, result.Error?.Code);
        Assert.Equal("$.payload.capabilities", result.Error?.Field);
        Assert.Contains("teleportVehicles", result.Error?.Message);
    }

    [Theory]
    [InlineData(
        "hello/valid-hello-ack.json",
        CapabilitySelectionStatus.Accepted,
        null)]
    [InlineData(
        "hello/valid-hello-ack-downgraded.json",
        CapabilitySelectionStatus.Downgraded,
        "node-only-no-old-plan-v1")]
    public void Hello_ack_declares_normal_or_named_downgrade_selection(
        string fixturePath,
        CapabilitySelectionStatus expectedStatus,
        string? expectedDowngradePolicyId)
    {
        var envelope = DecodeEnvelope(fixturePath);

        var result = HelloAckPayloadCodec.Decode(envelope.Payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedStatus, result.Value?.CapabilitySelection.Status);
        Assert.Equal(
            expectedDowngradePolicyId,
            result.Value?.CapabilitySelection.DowngradePolicyId);
    }

    [Fact]
    public void Duplicate_capability_in_semantic_set_is_rejected()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "adapterId": "a",
              "adapterVersion": "1",
              "supportedSchemaVersions": ["1.0.0"],
              "positionModel": "nodeOnly",
              "capabilities": ["cancellations", "cancellations"],
              "maxFleetSize": 1,
              "maxRequestCount": 1
            }
            """);

        var result = HelloPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.InvalidValue, result.Error?.Code);
        Assert.Contains("duplicate", result.Error?.Message);
    }

    [Fact]
    public void Accepted_ack_cannot_hide_a_downgrade_policy()
    {
        var fixture = FixtureLoader.ReadUtf8("hello/valid-hello-ack-downgraded.json")
            .Replace("\"downgraded\"", "\"accepted\"", StringComparison.Ordinal);
        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        var result = HelloAckPayloadCodec.Decode(envelope.Envelope!.Payload);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.InvalidValue, result.Error?.Code);
        Assert.Equal(
            "$.payload.capabilitySelection.downgradePolicyId",
            result.Error?.Field);
    }

    private static ProtocolEnvelope DecodeEnvelope(string fixturePath)
    {
        var fixture = FixtureLoader.ReadUtf8(fixturePath);
        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        return Assert.IsType<ProtocolEnvelope>(result.Envelope);
    }
}
