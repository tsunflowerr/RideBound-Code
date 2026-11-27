using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Tests.Final;

namespace RideBound.Runner.Tests.Online;

/// <summary>
/// Checkpoints of the final experiment's configuration, through child Runner processes started with the final runs'
/// argument vector (the T1 runs FS-G2-SHRC71-CL of <see cref="FinalRuns"/>).
/// </summary>
public sealed class CommitmentCheckpointProcessTests
{
    /// <summary>
    /// The streamed checkpoint line equals, byte for byte, the line the envelope path
    /// builds from the same content (old hash, Encode, envelope, canonical NDJSON). Checked on every
    /// checkpoint of the session.
    /// </summary>
    [Fact]
    public async Task Streamed_checkpoint_line_equals_the_envelope_path_line()
    {
        foreach (var name in new[] { "FS-G2-SHRC71-CL.light-v1" })
        {
            var run = await FinalRuns.SessionAsync(FinalRuns.Get(name));
            Assert.Equal(0, run.ExitCode);
            var lines = run.Lines()
                .Where(value => RunnerProcess.MessageType(value) == "checkpoint")
                .ToArray();
            Assert.Equal(17, lines.Length);

            foreach (var line in lines)
            {
                var streamed = System.Text.Encoding.UTF8.GetBytes(line);
                var decoded = ProtocolEnvelopeCodec.Decode(streamed).Envelope!;
                var content = decoded.Payload.GetProperty("content");
                var archivedContent = new CheckpointContent(
                    Hash(content, "manifestHash"),
                    Hash(content, "stateHash"),
                    Hash(content, "previousDecisionHash"),
                    content.GetProperty("appliedEpoch").GetInt64(),
                    content.GetProperty("nextEventSeq").GetInt64(),
                    content.GetProperty("simTimeMs").GetInt64(),
                    content.GetProperty("onlineState").Clone());
                var archivedHash = CheckpointPayloadCodec.CalculateHash(archivedContent);
                using var archivedPayload = JsonDocument.Parse(
                    CheckpointPayloadCodec.Encode(
                        new CheckpointPayload("1.0.0", archivedHash, archivedContent)));
                var archivedLine = CanonicalJson.Serialize(
                    decoded with { Payload = archivedPayload.RootElement.Clone() });

                Assert.Equal(archivedHash.Value, decoded.Payload.GetProperty("checkpointHash").GetString());
                Assert.Equal(archivedLine, streamed);
            }
        }
    }

    private static Sha256Hex Hash(JsonElement content, string name)
    {
        Assert.True(Sha256Hex.TryCreate(content.GetProperty(name).GetString(), out var hash));
        return hash!;
    }
}
