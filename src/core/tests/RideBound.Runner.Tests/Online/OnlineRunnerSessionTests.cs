using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Online;

public sealed class OnlineRunnerSessionTests
{
    [Fact]
    public void Bootstrap_batch_produces_typed_B1_actions_and_waits_for_ack()
    {
        var session = CreateInitializedOnlineSession();
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");

        var result = session.Process(batch);

        Assert.Equal("decision", result.Response?.MessageType.Value);
        var decision = DecisionPayloadCodec.Decode(result.Response!.Payload);
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        Assert.Equal(DecisionProductionStatus.Produced, decision.Value!.Status);
        Assert.Equal(DecisionReasonCodes.Accepted, decision.Value.ReasonCode);
        Assert.Equal(CertificateStatus.NotProduced, decision.Value.Certificate.Status);
        Assert.Equal(SolverStatus.NotRun, decision.Value.Solver.Status);
        Assert.Contains(
            decision.Value.Actions,
            value => value.GetProperty("decisionType").GetString()
                == "requestAccepted");
        Assert.Contains(
            decision.Value.Actions,
            value => value.GetProperty("decisionType").GetString()
                == "vehiclePlanUpdated");
        Assert.Equal(
            RunnerSessionStatus.AwaitingDecisionApplied,
            session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(1, session.NextEventSequence);
    }

    [Fact]
    public void Exact_retry_is_byte_equivalent_and_next_epoch_requires_ack()
    {
        var session = CreateInitializedOnlineSession();
        var batch = ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var first = session.Process(batch);

        var retry = session.Process(batch);
        var premature = session.Process(CreateTimerBatch(2, 4, 1_100));

        Assert.Equal(
            CanonicalJson.Serialize(first.Response!),
            CanonicalJson.Serialize(retry.Response!));
        Assert.Equal("error", premature.Response?.MessageType.Value);
        Assert.Equal(
            "INVALID_SESSION_STATE",
            premature.Response?.Payload.GetProperty("code").GetString());
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Two_epoch_online_chain_commits_only_matching_decision_hash()
    {
        var session = CreateInitializedOnlineSession();
        var first = session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json"));
        var firstDecision = DecisionPayloadCodec.Decode(
            first.Response!.Payload).Value!;
        var wrongAck = session.Process(
            CreateDecisionApplied(
                epoch: 1,
                simTime: 1_000,
                new string('f', 64)));

        Assert.Equal("error", wrongAck.Response?.MessageType.Value);
        Assert.Equal(
            "HASH_MISMATCH",
            wrongAck.Response?.Payload.GetProperty("code").GetString());

        session = CreateInitializedOnlineSession();
        first = session.Process(
            ReadFixture("wp2/valid-bootstrap-event-batch.json"));
        firstDecision = DecisionPayloadCodec.Decode(
            first.Response!.Payload).Value!;
        var acknowledged = session.Process(
            CreateDecisionApplied(
                1,
                1_000,
                firstDecision.DecisionHash.Value));

        Assert.Null(acknowledged.Response);
        Assert.Equal(1, session.AppliedEpoch);
        Assert.Equal(4, session.NextEventSequence);
        Assert.Equal(firstDecision.DecisionHash, session.PreviousDecisionHash);

        var second = session.Process(CreateTimerBatch(2, 4, 1_100));
        var secondDecision = DecisionPayloadCodec.Decode(
            second.Response!.Payload);

        Assert.True(secondDecision.IsSuccess, secondDecision.Error?.Message);
        Assert.Equal(
            DecisionProductionStatus.Produced,
            secondDecision.Value!.Status);
        Assert.Equal(
            firstDecision.DecisionHash,
            secondDecision.Value.PreviousDecisionHash);
        Assert.NotEqual(
            firstDecision.DecisionHash,
            secondDecision.Value.DecisionHash);
        Assert.Equal(1, session.AppliedEpoch);
    }

    [Fact]
    public void Request_tamper_changes_online_decision_and_state_hash()
    {
        var originalSession = CreateInitializedOnlineSession();
        var tamperedSession = CreateInitializedOnlineSession();
        var originalBytes =
            ReadFixture("wp2/valid-bootstrap-event-batch.json");
        var tampered = JsonNode.Parse(originalBytes)!;
        tampered["payload"]!["events"]![1]!["payload"]!["request"]![
            "latestPickupMs"] = 1_050;

        var original = DecisionPayloadCodec.Decode(
            originalSession.Process(originalBytes).Response!.Payload).Value!;
        var changed = DecisionPayloadCodec.Decode(
            tamperedSession.Process(
                Encoding.UTF8.GetBytes(tampered.ToJsonString()))
                .Response!.Payload).Value!;

        Assert.NotEqual(original.DecisionHash, changed.DecisionHash);
        Assert.NotEqual(original.StateAfterHash, changed.StateAfterHash);
    }

    private static RunnerSession CreateInitializedOnlineSession()
    {
        var session = new RunnerSession(
            RunnerDefaults.CapabilityRequirements,
            RunnerExecutionMode.OnlineRollingCost);
        var hello = session.Process(ReadFixture("hello/valid-hello.json"));
        Assert.Equal("helloAck", hello.Response?.MessageType.Value);
        var initialize = JsonNode.Parse(
            ReadFixture("initialize/valid-initialize-run.json"))!;
        initialize["runId"] = "wp2-run-001";
        initialize["scenarioId"] = "wp2-two-epoch-small";
        initialize["payload"]!["manifest"]!["travelTimeSnapshotHash"] =
            new string('a', 64);
        var initialized = session.Process(
            Encoding.UTF8.GetBytes(initialize.ToJsonString()));
        Assert.Equal("initialized", initialized.Response?.MessageType.Value);
        return session;
    }

    private static byte[] CreateTimerBatch(
        long epoch,
        long eventSequence,
        long simTime) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"eventBatch",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{
                "events":[{
                  "eventSeq":{{eventSequence}},
                  "eventType":"timerTick",
                  "payload":{}
                }]
              }
            }
            """);

    private static byte[] CreateDecisionApplied(
        long epoch,
        long simTime,
        string decisionHash) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"decisionApplied",
              "runId":"wp2-run-001",
              "scenarioId":"wp2-two-epoch-small",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{"decisionHash":"{{decisionHash}}"}
            }
            """);

    private static byte[] ReadFixture(string path) =>
        Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path));
}
