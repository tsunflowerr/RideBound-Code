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
    private static readonly FinalRun Run = FinalRuns.Get("FS-G2-SHRC71-CL.light-v1");

    /// <summary>
    /// A clean process runs the first batch and takes a checkpoint; another clean process restores it and runs the
    /// rest of the scenario. Its decisions equal the uninterrupted session's decisions after the first one.
    /// </summary>
    [Fact]
    public async Task Restored_clean_process_matches_genesis_suffix_decisions()
    {
        var scenario = FinalRuns.ScenarioLines(Run);
        var uninterrupted = await FinalRuns.SessionAsync(Run);
        Assert.Equal(0, uninterrupted.ExitCode);
        Assert.Equal(string.Empty, uninterrupted.StandardError);

        var firstBatch = IndexOf(scenario, "eventBatch");
        var checkpointRequest = scenario[firstBatch + 1];
        Assert.Equal("checkpoint", RunnerProcess.MessageType(checkpointRequest));
        var prefix = await RunnerProcess.RunAsync(
            Run.Arguments,
            FinalRuns.FixtureDirectory,
            [scenario[0], scenario[1], scenario[firstBatch], checkpointRequest, scenario[^1]]);
        Assert.Equal(0, prefix.ExitCode);
        Assert.Equal(string.Empty, prefix.StandardError);
        var checkpoint = Messages(prefix).Single(
            value => value.RootElement.GetProperty("messageType").GetString()
                == "checkpoint");
        var payload = checkpoint.RootElement.GetProperty("payload");
        var restore =
            "{\"schemaVersion\":\"1.0.0\",\"messageType\":\"restore\"," +
            $"\"runId\":{JsonSerializer.Serialize(checkpoint.RootElement.GetProperty("runId").GetString())}," +
            $"\"scenarioId\":{JsonSerializer.Serialize(checkpoint.RootElement.GetProperty("scenarioId").GetString())}," +
            $"\"payload\":{payload.GetRawText()}}}";
        var restored = await RunnerProcess.RunAsync(
            Run.Arguments,
            FinalRuns.FixtureDirectory,
            [scenario[0], scenario[1], restore, .. scenario.Skip(firstBatch + 2)]);

        Assert.Equal(0, restored.ExitCode);
        Assert.Equal(string.Empty, restored.StandardError);
        var uninterruptedSuffix = DecisionLines(uninterrupted).Skip(1).ToArray();
        var restoredSuffix = DecisionLines(restored);
        Assert.NotEmpty(restoredSuffix);
        Assert.Equal(uninterruptedSuffix, restoredSuffix);
        var restoreAck = Messages(restored).Single(
            value => value.RootElement.GetProperty("messageType").GetString()
                == "restore");
        Assert.Equal(
            payload.GetProperty("checkpointHash").GetString(),
            restoreAck.RootElement.GetProperty("payload")
                .GetProperty("checkpointHash").GetString());
    }

    /// <summary>
    /// ADR-089: the streamed checkpoint line equals, byte for byte, the line the envelope path
    /// builds from the same content (old hash, Encode, envelope, canonical NDJSON). Checked on every
    /// checkpoint of the session in both audit modes.
    /// </summary>
    [Fact]
    public async Task Streamed_checkpoint_line_equals_the_envelope_path_line()
    {
        foreach (var name in new[] { "FS-G2-SHRC71-CL.light-v1", "FS-G2-SHRC71-CL.full-v1" })
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

    private static int IndexOf(IReadOnlyList<string> lines, string messageType) =>
        Enumerable.Range(0, lines.Count)
            .First(index => RunnerProcess.MessageType(lines[index]) == messageType);

    private static string[] DecisionLines(RunnerProcessResult result) =>
        result.Lines()
            .Where(line => RunnerProcess.MessageType(line) == "decision")
            .ToArray();

    private static JsonDocument[] Messages(RunnerProcessResult result) =>
        result.Lines()
            .Select(line => JsonDocument.Parse(line))
            .ToArray();
}
