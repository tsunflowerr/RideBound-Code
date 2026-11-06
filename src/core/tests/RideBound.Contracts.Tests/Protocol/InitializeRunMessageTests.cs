using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class InitializeRunMessageTests
{
    [Fact]
    public void Initialize_fixture_contains_complete_reproducibility_identity()
    {
        var envelope = DecodeEnvelope("initialize/valid-initialize-run.json");

        var result = InitializeRunPayloadCodec.Decode(envelope.Payload);

        Assert.True(result.IsSuccess);
        var manifest = result.Value!.Manifest;
        Assert.Equal(20260729, manifest.MasterSeed);
        Assert.Equal("rolling-cost", manifest.PolicyId);
        Assert.Equal("fleetpy-ridebound", manifest.Adapter.AdapterId);
        Assert.Equal("fleetpy", manifest.Simulator.SimulatorId);
        Assert.Equal(
            ["distance", "time"],
            manifest.SourceUnitConversions.Select(value => value.Quantity));
        Assert.DoesNotContain(
            "runId",
            envelope.Payload.GetProperty("manifest")
                .EnumerateObject()
                .Select(property => property.Name));
        Assert.DoesNotContain(
            "scenarioId",
            envelope.Payload.GetProperty("manifest")
                .EnumerateObject()
                .Select(property => property.Name));
    }

    [Fact]
    public void Initialize_round_trip_preserves_manifest_identity()
    {
        var first = InitializeRunPayloadCodec.Decode(
            DecodeEnvelope("initialize/valid-initialize-run.json").Payload);

        using var document = JsonDocument.Parse(
            InitializeRunPayloadCodec.Encode(first.Value!));
        var second = InitializeRunPayloadCodec.Decode(document.RootElement);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Manifest.ProtocolVersion, second.Value!.Manifest.ProtocolVersion);
        Assert.Equal(first.Value.Manifest.BinarySha256, second.Value.Manifest.BinarySha256);
        Assert.Equal(
            first.Value.Manifest.SourceUnitConversions,
            second.Value.Manifest.SourceUnitConversions);
    }

    [Fact]
    public void Manifest_rejects_rounding_rule_that_changes_canonical_units()
    {
        var envelope = DecodeEnvelope("initialize/invalid-manifest-rounding.json");

        var result = InitializeRunPayloadCodec.Decode(envelope.Payload);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.InvalidValue, result.Error?.Code);
        Assert.EndsWith(".roundingRule", result.Error?.Field);
        Assert.Contains("roundTiesToEven", result.Error?.Message);
    }

    [Fact]
    public void Initialized_fixture_locks_initial_epoch_and_state_identity()
    {
        var envelope = DecodeEnvelope("initialize/valid-initialized.json");

        var result = InitializedPayloadCodec.Decode(envelope.Payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value?.InitialStateIdentity.EpochId.Value);
        Assert.Equal(1, result.Value?.InitialStateIdentity.NextEventSequence.Value);
        Assert.Equal(0, result.Value?.InitialStateIdentity.SimTime.Value);
    }

    [Fact]
    public void Initialized_identity_cannot_skip_initial_event_sequence()
    {
        var fixture = FixtureLoader.ReadUtf8("initialize/valid-initialized.json")
            .Replace(
                "\"nextEventSeq\": 1",
                "\"nextEventSeq\": 2",
                StringComparison.Ordinal);
        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        var result = InitializedPayloadCodec.Decode(envelope.Envelope!.Payload);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.ValueOutOfRange, result.Error?.Code);
        Assert.Equal(
            "$.payload.initialStateIdentity.nextEventSeq",
            result.Error?.Field);
    }

    [Fact]
    public void Manifest_unknown_field_is_rejected_instead_of_reading_environment_data()
    {
        var fixture = FixtureLoader.ReadUtf8("initialize/valid-initialize-run.json");
        using var source = JsonDocument.Parse(fixture);
        var manifestJson = source.RootElement.GetProperty("payload")
            .GetProperty("manifest")
            .GetRawText();
        var withHostName = manifestJson.Insert(
            manifestJson.LastIndexOf('}'),
            ",\"hostname\":\"developer-machine\"");
        using var document = JsonDocument.Parse($"{{\"manifest\":{withHostName}}}");

        var result = InitializeRunPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.UnknownField, result.Error?.Code);
        Assert.Equal("$.payload.manifest.hostname", result.Error?.Field);
    }

    private static ProtocolEnvelope DecodeEnvelope(string fixturePath)
    {
        var fixture = FixtureLoader.ReadUtf8(fixturePath);
        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));
        return Assert.IsType<ProtocolEnvelope>(result.Envelope);
    }
}
