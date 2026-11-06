using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Application.Commitments;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Domain.Commitments;
using RideBound.Domain.Common;
using RideBound.Domain.Validation;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed class CommitmentRunnerSessionTests
{
    [Fact]
    public void Bootstrap_publishes_initial_promise_and_produced_certificate_atomically()
    {
        var session = CreateInitializedSession();
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");

        var first = session.Process(batch);
        var retry = session.Process(batch);
        var decision = DecisionPayloadCodec.Decode(first.Response!.Payload);

        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(CertificateStatus.Produced, decision.Value!.Certificate.Status);
        Assert.NotNull(decision.Value.Certificate.Body);
        Assert.True(decision.Value.Certificate.Body!.NormalOperation);
        Assert.Equal(1, decision.Value.Certificate.Body.PromiseCount);
        Assert.Single(decision.Value.Certificate.Body.PublicationIds);
        Assert.Contains(
            decision.Value.Actions,
            value => value.GetProperty("decisionType").GetString()
                == "promisePublished");
        Assert.Equal(
            CanonicalJson.Serialize(first.Response),
            CanonicalJson.Serialize(retry.Response!));
        Assert.Empty(session.CommittedOnlineState!.Commitments.Histories);

        var wrongEpoch = session.Process(
            DecisionApplied(
                2,
                1_000,
                decision.Value.DecisionHash.Value));

        Assert.Equal("error", wrongEpoch.Response?.MessageType.Value);
        Assert.Empty(session.CommittedOnlineState.Commitments.Histories);
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);

        var applied = session.Process(
            DecisionApplied(
                1,
                1_000,
                decision.Value.DecisionHash.Value));

        Assert.Null(applied.Response);
        var history = Assert.Single(
            session.CommittedOnlineState.Commitments.Histories).Value;
        Assert.Single(history.Entries);
        Assert.Equal(1, history.Current.PublishedPromise.Version.Value);
    }

    [Fact]
    public void Restore_suffix_matches_genesis_replay_and_tamper_is_rejected()
    {
        var genesis = CreateInitializedSession();
        var first = DecisionPayloadCodec.Decode(
            genesis.Process(ReadFixture("wp2/valid-bootstrap-event-batch.json"))
                .Response!.Payload).Value!;
        _ = genesis.Process(DecisionApplied(1, 1_000, first.DecisionHash.Value));
        var checkpointResponse = genesis.Process(CheckpointRequest()).Response!;
        var checkpoint = CheckpointPayloadCodec.Decode(
            checkpointResponse.Payload);
        Assert.True(checkpoint.IsSuccess, checkpoint.Error?.Message);

        var uninterrupted = DecisionPayloadCodec.Decode(
            genesis.Process(TimerBatch()).Response!.Payload).Value!;
        var restored = CreateInitializedSession();
        var restoreResponse = restored.Process(
            RestoreRequest(checkpointResponse.Payload));

        Assert.Equal("restore", restoreResponse.Response?.MessageType.Value);
        Assert.Equal(1, restored.AppliedEpoch);
        Assert.Single(restored.CommittedOnlineState!.Commitments.Histories);
        var replayResponse = restored.Process(TimerBatch()).Response!;
        Assert.True(
            replayResponse.MessageType.Value == "decision",
            replayResponse.Payload.GetRawText());
        var replayDecode = DecisionPayloadCodec.Decode(replayResponse.Payload);
        Assert.True(replayDecode.IsSuccess, replayDecode.Error?.Message);
        var replayed = replayDecode.Value!;
        Assert.Equal(uninterrupted.DecisionHash, replayed.DecisionHash);
        Assert.Equal(uninterrupted.StateAfterHash, replayed.StateAfterHash);

        var tamperedSession = CreateInitializedSession();
        var tampered = JsonNode.Parse(checkpointResponse.Payload.GetRawText())!;
        tampered["content"]!["nextEventSeq"] = 999;
        var rejected = tamperedSession.Process(
            RestoreRequest(JsonDocument.Parse(tampered.ToJsonString()).RootElement));
        Assert.Equal("error", rejected.Response?.MessageType.Value);
        Assert.Equal(
            "SCHEMA_VALIDATION_FAILED",
            rejected.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(0, tamperedSession.AppliedEpoch);
    }

    [Fact]
    public void Checkpoint_is_forbidden_while_a_decision_is_pending()
    {
        var session = CreateInitializedSession();
        _ = session.Process(ReadFixture("wp2/valid-bootstrap-event-batch.json"));

        var rejected = session.Process(CheckpointRequest());

        Assert.Equal("error", rejected.Response?.MessageType.Value);
        Assert.Equal(
            "INVALID_SESSION_STATE",
            rejected.Response?.Payload.GetProperty("code").GetString());
        Assert.Empty(session.CommittedOnlineState!.Commitments.Histories);
    }

    private static RunnerSession CreateInitializedSession()
    {
        var policy = new CommitmentPolicy(
            "uniform-v1",
            CommitmentBudgetBasis.DecisionInduced,
            CommitmentDimensionVocabulary.Ordered.Select(
                dimension => new CommitmentDimensionLimit(
                    dimension,
                    null,
                    CommitmentPhase.AllActive)),
            new MaterialRevisionRule(1_000, 60_000));
        var session = new RunnerSession(
            RunnerDefaults.CapabilityRequirements,
            RunnerExecutionMode.OnlineCommitment,
            commitmentPolicies: new CommitmentPolicyCatalog([policy]),
            stopDistances: NoDistances.Instance,
            commitmentPolicyConfigurationHash: Hash(
                new string('1', 64)));
        Assert.Equal(
            "helloAck",
            session.Process(ReadFixture("hello/valid-hello.json"))
                .Response?.MessageType.Value);
        var initialize = JsonNode.Parse(
            ReadFixture("initialize/valid-initialize-run.json"))!;
        initialize["runId"] = "wp2-run-001";
        initialize["scenarioId"] = "wp2-two-epoch-small";
        initialize["payload"]!["manifest"]!["travelTimeSnapshotHash"] =
            new string('a', 64);
        Assert.Equal(
            "initialized",
            session.Process(
                Encoding.UTF8.GetBytes(initialize.ToJsonString()))
                .Response?.MessageType.Value);
        return session;
    }

    private static byte[] DecisionApplied(
        long epoch,
        long simTime,
        string hash) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"decisionApplied",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{"decisionHash":"{{hash}}"}
            }
            """);

    private static byte[] CheckpointRequest() =>
        Encoding.UTF8.GetBytes(
            """
            {
              "schemaVersion":"1.0.0",
              "messageType":"checkpoint",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "payload":{}
            }
            """);

    private static byte[] RestoreRequest(JsonElement payload) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"restore",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "payload":{{payload.GetRawText()}}
            }
            """);

    private static byte[] TimerBatch() =>
        Encoding.UTF8.GetBytes(
            """
            {
              "schemaVersion":"1.0.0",
              "messageType":"eventBatch",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":2,
              "simTimeMs":1100,
              "payload":{
                "events":[{
                  "eventSeq":4,
                  "eventType":"timerTick",
                  "payload":{}
                }]
              }
            }
            """);

    private static byte[] ReadFixture(string path) =>
        Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path));

    private static Sha256Hex Hash(string value)
    {
        Assert.True(Sha256Hex.TryCreate(value, out var hash));
        return hash!;
    }

    private sealed class NoDistances : IStopDistanceLookup
    {
        public static NoDistances Instance { get; } = new();

        public bool TryGetDistanceMillimeters(
            NodeId fromNodeId,
            NodeId toNodeId,
            out long distanceMillimeters)
        {
            distanceMillimeters = 0;
            return false;
        }
    }
}
