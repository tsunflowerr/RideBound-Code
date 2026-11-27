using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Runner.StateAudit;
using RideBound.Runner.Tests.Final;

namespace RideBound.Runner.Tests.Online;

/// <summary>
/// The Runner's direct canonical writer against the independent reference path, on real states: the scenario of each
/// final-configuration run in Final/FinalRuns is replayed in this process, and at each checkpoint request the committed
/// state is written both ways and compared with the checkpoint that the child process of the T1 golden test printed at
/// that point. So each checkpoint holds exactly the session's whole committed state.
/// </summary>
public sealed class OnlineStateReferenceCanonicalizerTests
{
    public static TheoryData<string> Runs => FinalRuns.Names;

    [Theory]
    [MemberData(nameof(Runs))]
    public async Task Writer_and_reference_agree_on_every_final_checkpoint_state(string name)
    {
        var run = FinalRuns.Get(name);
        var recorded = await FinalRuns.SessionAsync(run);
        Assert.Equal(0, recorded.ExitCode);
        var checkpoints = recorded.Lines().Where(line => RunnerProcess.MessageType(line) == "checkpoint").ToArray();
        Assert.NotEmpty(checkpoints);
        var session = FinalRuns.NewSession(run);
        var cache = new OnlineStateCanonicalCache();
        var compared = 0;

        foreach (var line in FinalRuns.ScenarioLines(run))
        {
            var messageType = RunnerProcess.MessageType(line);

            if (messageType == "shutdown")
            {
                break;
            }

            if (messageType == "checkpoint")
            {
                var state = session.CommittedOnlineState!;
                using var document = JsonDocument.Parse(checkpoints[compared++]);
                var onlineState = document.RootElement.GetProperty("payload").GetProperty("content")
                    .GetProperty("onlineState");

                var reference = OnlineStateReferenceCanonicalizer.CanonicalizeReference(state);
                Assert.Equal(Encoding.UTF8.GetBytes(onlineState.GetRawText()), reference);
                Assert.Equal(reference, OnlineStateCanonicalWriter.Write(state, null).ToArray());

                // With a cache: first filled, then read back.
                Assert.Equal(reference, OnlineStateCanonicalWriter.Write(state, cache).ToArray());
                Assert.Equal(reference, OnlineStateCanonicalWriter.Write(state, cache).ToArray());
                continue;
            }

            var response = session.Process(Encoding.UTF8.GetBytes(line)).Response;

            if (messageType == "eventBatch")
            {
                Assert.Equal("decision", response?.MessageType.Value);
                Assert.Null(session.Process(DecisionApplied(line, response!.Payload)).Response);
            }
        }

        Assert.Equal(checkpoints.Length, compared);
    }

    /// <summary>The adapter's acknowledgement of the decision made for <paramref name="batchLine"/>, as RunnerProcess
    /// sends it.</summary>
    private static byte[] DecisionApplied(string batchLine, JsonElement decisionPayload)
    {
        var batch = JsonNode.Parse(batchLine)!;
        var applied = new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["messageType"] = "decisionApplied",
            ["runId"] = batch["runId"]!.GetValue<string>(),
            ["scenarioId"] = batch["scenarioId"]!.GetValue<string>(),
            ["epochId"] = batch["epochId"]!.GetValue<long>(),
            ["simTimeMs"] = batch["simTimeMs"]!.GetValue<long>(),
            ["payload"] = new JsonObject
            {
                ["decisionHash"] = decisionPayload.GetProperty("decisionHash").GetString(),
            },
        };
        return Encoding.UTF8.GetBytes(applied.ToJsonString());
    }
}
