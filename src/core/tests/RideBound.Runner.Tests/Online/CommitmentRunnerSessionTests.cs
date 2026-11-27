using System.Text;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Protocol;
using RideBound.Runner.Tests.Final;

namespace RideBound.Runner.Tests.Online;

/// <summary>
/// The commitment rules of a Runner session: a promise is published with a produced certificate and enters the
/// commitment ledger only when its decision is acknowledged; no checkpoint is taken while a decision is pending. Checked on the final experiment's
/// configuration, the T1 session FS-G2-SHRC71-CL.light-v1 of <see cref="FinalRuns"/>, run inside the test process.
/// Its policy opens a promise when the booking is confirmed,
/// so the decision for epoch 1 accepts the first rider without a promise and the decision for epoch 2 publishes the
/// first one.
/// </summary>
public sealed class CommitmentRunnerSessionTests
{
    private static readonly FinalRun Run = FinalRuns.Get("FS-G2-SHRC71-CL.light-v1");

    [Fact]
    public void Initial_promise_publication_and_produced_certificate_commit_atomically_on_ack()
    {
        var session = CreateInitializedSession();
        Acknowledge(session, epoch: 1);
        Assert.Empty(session.CommittedOnlineState!.Commitments.Histories);
        var batch = Batch(2);

        var first = session.Process(batch);
        var retry = session.Process(batch);
        var decision = DecisionPayloadCodec.Decode(first.Response!.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(CertificateStatus.Produced, decision.Value!.Certificate.Status);
        Assert.NotNull(decision.Value.Certificate.Body);
        Assert.True(decision.Value.Certificate.Body!.NormalOperation);
        Assert.Equal(2, decision.Value.Certificate.Body.PromiseCount);
        Assert.Single(decision.Value.Certificate.Body.PublicationIds);
        Assert.Contains(
            decision.Value.Actions,
            value => value.GetProperty("decisionType").GetString()
                == "promisePublished");
        Assert.Equal(
            CanonicalJson.Serialize(first.Response),
            CanonicalJson.Serialize(retry.Response!));
        Assert.Empty(session.CommittedOnlineState.Commitments.Histories);

        var wrongEpoch = session.Process(
            DecisionApplied(2, decision.Value.DecisionHash, epochId: 3));

        Assert.Equal("error", wrongEpoch.Response?.MessageType.Value);
        Assert.Empty(session.CommittedOnlineState.Commitments.Histories);
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);

        var applied = session.Process(DecisionApplied(2, decision.Value.DecisionHash));

        Assert.Null(applied.Response);
        var history = Assert.Single(
            session.CommittedOnlineState.Commitments.Histories).Value;
        Assert.Single(history.Entries);
        Assert.Equal(1, history.Current.PublishedPromise.Version.Value);
    }

    [Fact]
    public void Checkpoint_is_forbidden_while_a_decision_is_pending()
    {
        var session = CreateInitializedSession();
        Acknowledge(session, epoch: 1);
        _ = session.Process(Batch(2));

        var rejected = session.Process(CheckpointRequest());

        Assert.Equal("error", rejected.Response?.MessageType.Value);
        Assert.Equal(
            "INVALID_SESSION_STATE",
            rejected.Response?.Payload.GetProperty("code").GetString());
        Assert.Empty(session.CommittedOnlineState!.Commitments.Histories);
    }

    private static RunnerSession CreateInitializedSession()
    {
        var session = FinalRuns.NewSession(Run);
        Assert.Equal(
            "helloAck",
            session.Process(Utf8(Scenario("hello").Single()))
                .Response?.MessageType.Value);
        Assert.Equal(
            "initialized",
            session.Process(Utf8(Scenario("initializeRun").Single()))
                .Response?.MessageType.Value);
        return session;
    }

    /// <summary>Sends the batch of <paramref name="epoch"/> and acknowledges its decision.</summary>
    private static void Acknowledge(RunnerSession session, int epoch)
    {
        var decision = DecisionPayloadCodec.Decode(
            session.Process(Batch(epoch)).Response!.Payload).Value!;
        Assert.Null(session.Process(DecisionApplied(epoch, decision.DecisionHash)).Response);
    }

    private static byte[] Batch(int epoch) => Utf8(Scenario("eventBatch")[epoch - 1]);

    /// <summary>The adapter's acknowledgement of the decision made for the batch of <paramref name="epoch"/>, sent
    /// with <paramref name="epochId"/> in place of that epoch when given.</summary>
    private static byte[] DecisionApplied(int epoch, Sha256Hex decisionHash, long? epochId = null)
    {
        var batch = JsonNode.Parse(Scenario("eventBatch")[epoch - 1])!;
        var applied = new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["messageType"] = "decisionApplied",
            ["runId"] = batch["runId"]!.GetValue<string>(),
            ["scenarioId"] = batch["scenarioId"]!.GetValue<string>(),
            ["epochId"] = epochId ?? batch["epochId"]!.GetValue<long>(),
            ["simTimeMs"] = batch["simTimeMs"]!.GetValue<long>(),
            ["payload"] = new JsonObject { ["decisionHash"] = decisionHash.Value },
        };
        return Utf8(applied.ToJsonString());
    }

    private static byte[] CheckpointRequest() => Utf8(Scenario("checkpoint")[0]);

    private static string[] Scenario(string messageType) =>
        FinalRuns.ScenarioLines(Run)
            .Where(line => RunnerProcess.MessageType(line) == messageType)
            .ToArray();

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);
}
