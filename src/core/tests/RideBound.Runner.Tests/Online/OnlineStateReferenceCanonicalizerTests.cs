using System.Text;
using System.Text.Json;
using RideBound.Runner.Online;
using RideBound.Runner.Tests.Final;

namespace RideBound.Runner.Tests.Online;

/// <summary>
/// The Runner's direct canonical writer against the independent reference path, on real states: every checkpoint of
/// the final-configuration sessions in Final/FinalRuns (the same cached child-process sessions as the T1 golden test).
/// </summary>
public sealed class OnlineStateReferenceCanonicalizerTests
{
    public static TheoryData<string> Runs => FinalRuns.Names;

    [Theory]
    [MemberData(nameof(Runs))]
    public async Task Writer_and_reference_agree_on_every_final_checkpoint_state(string name)
    {
        var run = FinalRuns.Get(name);
        var session = await FinalRuns.SessionAsync(run);
        Assert.Equal(0, session.ExitCode);
        var checkpoints = session.Lines().Where(line => RunnerProcess.MessageType(line) == "checkpoint").ToArray();
        Assert.NotEmpty(checkpoints);
        var cache = new OnlineStateCanonicalCache();

        foreach (var line in checkpoints)
        {
            using var document = JsonDocument.Parse(line);
            var content = document.RootElement.GetProperty("payload").GetProperty("content");
            var onlineState = content.GetProperty("onlineState");
            var decoded = OnlineStateCheckpointCodec.Decode(onlineState, shadowAllocation: true);
            Assert.True(decoded.IsSuccess, decoded.Error);
            var state = decoded.State!;

            var reference = OnlineStateReferenceCanonicalizer.CanonicalizeReference(state);
            Assert.Equal(Encoding.UTF8.GetBytes(onlineState.GetRawText()), reference);
            Assert.Equal(reference, OnlineStateCanonicalWriter.Write(state, null).ToArray());

            // With a cache: first filled, then read back.
            Assert.Equal(reference, OnlineStateCanonicalWriter.Write(state, cache).ToArray());
            Assert.Equal(reference, OnlineStateCanonicalWriter.Write(state, cache).ToArray());

            var referenceHash = OnlineStateReferenceCanonicalizer.CalculateHashReference(state);
            Assert.Equal(referenceHash, OnlineStateCanonicalWriter.HashV1(OnlineStateCanonicalWriter.Write(state, null)));

            if (!run.Wp4.EndsWith("light-v1", StringComparison.Ordinal))
            {
                // full-v1: the checkpoint's stateHash is OnlineStateHash.v1 of this very state.
                Assert.Equal(content.GetProperty("stateHash").GetString(), referenceHash.Value);
            }
        }
    }
}
