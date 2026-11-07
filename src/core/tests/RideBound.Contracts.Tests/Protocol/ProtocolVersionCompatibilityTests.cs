using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Tests.Fixtures;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class ProtocolVersionCompatibilityTests
{
    [Fact]
    public void Patch_version_uses_same_canonical_semantics()
    {
        var fixture = FixtureLoader.ReadUtf8(
            "compatibility/valid-patch-version.json");

        var envelope = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        Assert.True(envelope.IsSuccess);
        Assert.Equal("1.0.7", envelope.Envelope?.SchemaVersion.ToString());

        var hello = HelloPayloadCodec.Decode(envelope.Envelope!.Payload);
        Assert.True(hello.IsSuccess);
        Assert.Equal("fixture-adapter", hello.Value?.AdapterId);
    }

    [Theory]
    [InlineData(
        "compatibility/invalid-unsupported-major.json",
        ProtocolEnvelopeErrorCode.UnsupportedSchemaMajor,
        ProtocolFailureDisposition.FailSession)]
    [InlineData(
        "compatibility/invalid-unsupported-minor.json",
        ProtocolEnvelopeErrorCode.UnsupportedSchemaMinor,
        ProtocolFailureDisposition.RejectMessage)]
    [InlineData(
        "compatibility/invalid-unknown-field.json",
        ProtocolEnvelopeErrorCode.UnknownField,
        ProtocolFailureDisposition.RejectMessage)]
    public void Compatibility_fixture_has_exact_error_and_disposition(
        string fixturePath,
        ProtocolEnvelopeErrorCode expectedCode,
        ProtocolFailureDisposition expectedDisposition)
    {
        var fixture = FixtureLoader.ReadUtf8(fixturePath);

        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.Error?.Code);
        Assert.Equal(expectedDisposition, result.Error?.Disposition);
    }

    [Fact]
    public void Unsupported_major_is_reported_before_unknown_future_fields()
    {
        var fixture = FixtureLoader.ReadUtf8(
            "compatibility/invalid-unsupported-major.json");

        var result = ProtocolEnvelopeCodec.Decode(Encoding.UTF8.GetBytes(fixture));

        Assert.Equal(
            ProtocolEnvelopeErrorCode.UnsupportedSchemaMajor,
            result.Error?.Code);
        Assert.Equal("schemaVersion", result.Error?.Field);
    }

    [Fact]
    public void Encoder_rejects_manually_constructed_unsupported_version()
    {
        var decoded = ProtocolEnvelopeCodec.Decode(
            """{"schemaVersion":"1.0.0","messageType":"hello","payload":{}}"""u8);
        Assert.True(ProtocolVersion.TryParse("2.0.0", out var unsupported));
        var invalid = decoded.Envelope! with { SchemaVersion = unsupported! };

        var exception = Assert.Throws<ArgumentException>(
            () => ProtocolEnvelopeCodec.Encode(invalid));

        Assert.Contains("2.0.0", exception.Message);
    }

    [Fact]
    public void Compatibility_matrix_matches_executable_policy()
    {
        using var document = JsonDocument.Parse(
            FixtureLoader.ReadSchemaUtf8("v1/compatibility-matrix.json"));
        var rows = document.RootElement.GetProperty("rows");

        Assert.Contains(
            rows.EnumerateArray(),
            row => row.GetProperty("behavior").GetString() == "failSession"
                && row.GetProperty("protocolCode").GetString()
                    == "UNSUPPORTED_SCHEMA_MAJOR");
        Assert.Contains(
            rows.EnumerateArray(),
            row => row.GetProperty("behavior").GetString() == "rejectMessage"
                && row.GetProperty("protocolCode").GetString()
                    == "UNSUPPORTED_SCHEMA_MINOR");
        Assert.Empty(
            document.RootElement
                .GetProperty("safeForwardMinorProfiles")
                .EnumerateArray());
    }
}
