using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class CheckpointMessageTests
{
    [Fact]
    public void Canonical_checkpoint_round_trips_and_tamper_is_detected()
    {
        using var state = JsonDocument.Parse(
            """{"appliedEpoch":2,"commitmentLedger":[],"incidentLedger":{}}""");
        var content = new CheckpointContent(
            ProtocolHash.ZeroHash,
            ProtocolHash.ZeroHash,
            ProtocolHash.ZeroHash,
            2,
            7,
            1_000,
            state.RootElement.Clone());
        var hash = CheckpointPayloadCodec.CalculateHash(content);
        var payload = new CheckpointPayload("1.0.0", hash, content);
        var encoded = CheckpointPayloadCodec.Encode(payload);
        using var document = JsonDocument.Parse(encoded);

        var decoded = CheckpointPayloadCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.Equal(hash, decoded.Value!.CheckpointHash);

        var tampered = JsonNode.Parse(encoded)!;
        tampered["content"]!["nextEventSeq"] = 8;
        using var tamperedDocument = JsonDocument.Parse(tampered.ToJsonString());
        var rejected = CheckpointPayloadCodec.Decode(tamperedDocument.RootElement);
        Assert.False(rejected.IsSuccess);
        Assert.Equal("$.payload.checkpointHash", rejected.Error?.Field);
    }

    [Fact]
    public void Unknown_checkpoint_version_is_rejected_before_restore()
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "checkpointVersion":"2.0.0",
              "checkpointHash":"{{new string('0', 64)}}",
              "content":{
                "manifestHash":"{{new string('0', 64)}}",
                "stateHash":"{{new string('0', 64)}}",
                "previousDecisionHash":"{{new string('0', 64)}}",
                "appliedEpoch":0,
                "nextEventSeq":1,
                "simTimeMs":0,
                "onlineState":{}
              }
            }
            """);

        var result = CheckpointPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal("$.payload.checkpointVersion", result.Error?.Field);
    }
}
