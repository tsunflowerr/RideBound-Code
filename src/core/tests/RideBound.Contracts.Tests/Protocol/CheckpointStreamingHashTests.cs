using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Contracts.Tests.Protocol;

/// <summary>
/// ADR-089: the streamed checkpoint hash and the shell split must reproduce the envelope path
/// exactly, for any split of the canonical state into parts.
/// </summary>
public sealed class CheckpointStreamingHashTests
{
    [Theory]
    [InlineData("""{"appliedEpoch":2,"commitmentLedger":[],"incidentLedger":{}}""")]
    [InlineData("""{"a":"plus+less<amp&quote'tick`","b":"é中😀","c":"esc\"\\\n\t\u0001","d":[1,-2,9007199254740991],"e":true,"f":false}""")]
    [InlineData("""{"onlineState":{},"nested":{"onlineState":[{}]},"x":"\"onlineState\":{}"}""")]
    public void Streamed_hash_equals_the_envelope_path_hash_for_every_split(string stateJson)
    {
        var canonicalState = CanonicalJson.Canonicalize(System.Text.Encoding.UTF8.GetBytes(stateJson));
        using var state = JsonDocument.Parse(canonicalState);
        var content = Content(state.RootElement.Clone());
        var expected = CheckpointPayloadCodec.CalculateHash(content);
        using var empty = JsonDocument.Parse("{}");
        var shell = content with { OnlineState = empty.RootElement.Clone() };
        var random = new Random(canonicalState.Length);

        for (var trial = 0; trial < 50; trial++)
        {
            var parts = RandomSplit(canonicalState, random);

            Assert.Equal(expected, CheckpointPayloadCodec.CalculateHash(shell, parts));
        }
    }

    [Fact]
    public void Shell_split_and_state_rebuild_the_canonical_payload()
    {
        var canonicalState = CanonicalJson.Canonicalize(
            """{"z":"é+","a":[{"onlineState":{}}]}"""u8);
        using var state = JsonDocument.Parse(canonicalState);
        var content = Content(state.RootElement.Clone());
        var hash = CheckpointPayloadCodec.CalculateHash(content);
        var expected = CanonicalJson.Canonicalize(
            CheckpointPayloadCodec.Encode(new CheckpointPayload("1.0.0", hash, content)));
        using var empty = JsonDocument.Parse("{}");
        var shell = content with { OnlineState = empty.RootElement.Clone() };

        var (before, after) = CheckpointPayloadCodec.SplitAtEmptyOnlineState(
            CanonicalJson.Canonicalize(CheckpointPayloadCodec.EncodeShell(hash, shell)));
        var rebuilt = before.ToArray().Concat(canonicalState).Concat(after.ToArray()).ToArray();

        Assert.Equal(expected, rebuilt);
    }

    [Fact]
    public void Shell_requires_an_empty_online_state()
    {
        using var state = JsonDocument.Parse("""{"a":1}""");

        Assert.Throws<ArgumentException>(
            () => CheckpointPayloadCodec.CalculateHash(Content(state.RootElement.Clone()), []));
    }

    private static CheckpointContent Content(JsonElement onlineState) =>
        new(
            ProtocolHash.ZeroHash,
            ProtocolHash.ZeroHash,
            ProtocolHash.ZeroHash,
            3,
            11,
            42_000,
            onlineState);

    private static ReadOnlyMemory<byte>[] RandomSplit(byte[] bytes, Random random)
    {
        var parts = new List<ReadOnlyMemory<byte>>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            var length = random.Next(0, Math.Min(7, bytes.Length - offset) + 1);
            parts.Add(bytes.AsMemory(offset, length));
            offset += length;
        }

        return [.. parts];
    }
}
