using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Protocol;

namespace RideBound.Runner.Tests.Protocol;

public sealed class RunnerSessionTests
{
    [Fact]
    public void Session_moves_new_to_negotiated_to_initialized()
    {
        var session = CreateSession();

        var hello = session.Process(ReadFixture("hello/valid-hello.json"));
        var initialize = session.Process(
            ReadFixture("initialize/valid-initialize-run.json"));

        Assert.Equal("helloAck", hello.Response?.MessageType.Value);
        Assert.Equal("initialized", initialize.Response?.MessageType.Value);
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(1, session.NextEventSequence);
        var initialized = InitializedPayloadCodec.Decode(initialize.Response!.Payload);
        Assert.NotEqual(ProtocolHash.ZeroHash, initialized.Value?.ManifestHash);
        Assert.NotEqual(
            ProtocolHash.ZeroHash,
            initialized.Value?.InitialStateIdentity.StateHash);
    }

    [Fact]
    public void Structural_event_returns_explicit_not_produced_shell()
    {
        var session = CreateInitializedSession();

        var result = session.Process(CreateEventBatch(eventSequence: 1));

        Assert.Equal("decision", result.Response?.MessageType.Value);
        var decision = DecisionPayloadCodec.Decode(result.Response!.Payload);
        Assert.True(decision.IsSuccess);
        Assert.Equal(DecisionProductionStatus.NotProduced, decision.Value?.Status);
        Assert.Empty(decision.Value!.Actions);
        Assert.Equal(CertificateStatus.NotProduced, decision.Value.Certificate.Status);
        Assert.Equal(SolverStatus.NotRun, decision.Value.Solver.Status);
        Assert.Equal(
            DecisionPayloadCodec.StructuralOnlyReasonCode,
            decision.Value.ReasonCode);
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(1, session.NextEventSequence);
    }

    [Fact]
    public void Exact_duplicate_returns_cached_response_without_advancing_hash()
    {
        var session = CreateInitializedSession();
        var batch = CreateEventBatch(eventSequence: 1);
        var first = session.Process(batch);
        var hashBeforeRetry = session.PreviousDecisionHash;

        var retry = session.Process(batch);

        Assert.Equal(
            CanonicalJson.Serialize(first.Response!),
            CanonicalJson.Serialize(retry.Response!));
        Assert.Equal(hashBeforeRetry, session.PreviousDecisionHash);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
        Assert.Equal(1, RunnerSession.RetainedBatchResponseCount);
    }

    [Fact]
    public void Canonically_equal_duplicate_ignores_json_formatting()
    {
        var session = CreateInitializedSession();
        var batch = CreateEventBatch(eventSequence: 1);
        var first = session.Process(batch);

        var retry = session.Process(CanonicalJson.Canonicalize(batch));

        Assert.Equal(
            CanonicalJson.Serialize(first.Response!),
            CanonicalJson.Serialize(retry.Response!));
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Published_duplicate_fixture_is_runner_executable()
    {
        var session = CreateInitializedSession();
        var fixture = ReadFixture(
            "golden/required/09-duplicate-event-idempotent/input.json");

        var first = session.Process(fixture);
        var second = session.Process(fixture);

        Assert.Equal(
            CanonicalJson.Serialize(first.Response!),
            CanonicalJson.Serialize(second.Response!));
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
    }

    [Fact]
    public void Same_duplicate_key_with_changed_payload_fails_session()
    {
        var session = CreateInitializedSession();
        _ = session.Process(CreateEventBatch(eventSequence: 1));

        var conflict = session.Process(
            CreateEventBatch(eventSequence: 1, eventType: "incidentResolved"));

        AssertError(conflict, "DUPLICATE_PAYLOAD_CONFLICT", "failSession");
        Assert.Equal(RunnerSessionStatus.Failed, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
    }

    [Fact]
    public void Same_duplicate_key_with_changed_simulation_time_fails_session()
    {
        var session = CreateInitializedSession();
        _ = session.Process(CreateEventBatch(eventSequence: 1, simTime: 100));

        var conflict = session.Process(
            CreateEventBatch(eventSequence: 1, simTime: 200));

        AssertError(conflict, "DUPLICATE_PAYLOAD_CONFLICT", "failSession");
        Assert.Equal(RunnerSessionStatus.Failed, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Event_gap_and_overlap_fail_without_sorting_or_buffering()
    {
        var gapSession = CreateInitializedSession();
        var gap = gapSession.Process(CreateEventBatch(eventSequence: 2));
        AssertError(gap, "EVENT_SEQUENCE_GAP", "failSession");

        var overlapSession = CreateInitializedSession();
        _ = overlapSession.Process(CreateEventBatch(eventSequence: 1));
        var decision = DecisionPayloadCodec.Decode(
            overlapSession.Process(CreateEventBatch(eventSequence: 1))
                .Response!
                .Payload)
            .Value!;
        _ = overlapSession.Process(CreateDecisionApplied(decision.DecisionHash));
        var overlap = overlapSession.Process(
            CreateEventBatch(eventSequence: 1, epoch: 2, simTime: 200));
        AssertError(overlap, "EVENT_SEQUENCE_OVERLAP", "failSession");
    }

    [Fact]
    public void Epoch_gap_and_sequence_overflow_fail_deterministically()
    {
        var epochGapSession = CreateInitializedSession();
        var epochGap = epochGapSession.Process(
            CreateEventBatch(eventSequence: 1, epoch: 2));
        AssertError(epochGap, "EPOCH_GAP", "failSession");

        var overflowSession = CreateInitializedSession();
        var overflow = overflowSession.Process(
            CreateEventBatch(eventSequence: ProtocolLimits.MaxCanonicalInteger));
        AssertError(overflow, "EVENT_SEQUENCE_GAP", "failSession");
    }

    [Fact]
    public void Ordering_validator_rejects_the_last_integer_before_increment_overflow()
    {
        var bytes = CreateEventBatch(
            eventSequence: ProtocolLimits.MaxCanonicalInteger);
        var envelope = ProtocolEnvelopeCodec.Decode(bytes).Envelope!;
        var payload = EventBatchPayloadCodec.Decode(envelope.Payload).Value!;

        var error = EventBatchOrderingValidator.Validate(
            envelope,
            payload,
            new EventBatchOrderingState(
                PreviousAppliedEpoch: 0,
                NextEventSequence: ProtocolLimits.MaxCanonicalInteger,
                LastSimulationTimeMilliseconds: 0));

        Assert.Equal("EVENT_SEQUENCE_GAP", error?.ProtocolCode);
        Assert.Contains("exhausts", error?.Message);
    }

    [Fact]
    public void Forbidden_state_edges_are_recoverable_and_do_not_mutate_state()
    {
        var session = CreateSession();
        AssertError(
            session.Process(ReadFixture("initialize/valid-initialize-run.json")),
            "INVALID_SESSION_STATE",
            "rejectMessage");
        AssertError(
            session.Process(CreateEventBatch(eventSequence: 1)),
            "INVALID_SESSION_STATE",
            "rejectMessage");
        Assert.Equal(RunnerSessionStatus.New, session.Status);

        _ = session.Process(ReadFixture("hello/valid-hello.json"));
        AssertError(
            session.Process(ReadFixture("hello/valid-hello.json")),
            "INVALID_SESSION_STATE",
            "rejectMessage");
        Assert.Equal(RunnerSessionStatus.Negotiated, session.Status);
    }

    [Fact]
    public void Decision_applied_is_the_only_point_that_advances_epoch_and_hash()
    {
        var session = CreateInitializedSession();
        var response = session.Process(CreateEventBatch(eventSequence: 1));
        var decision = DecisionPayloadCodec.Decode(response.Response!.Payload).Value!;

        var applied = session.Process(CreateDecisionApplied(decision.DecisionHash));

        Assert.Null(applied.Response);
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        Assert.Equal(1, session.AppliedEpoch);
        Assert.Equal(2, session.NextEventSequence);
        Assert.Equal(decision.DecisionHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Wrong_decision_applied_hash_fails_session_without_advancing()
    {
        var session = CreateInitializedSession();
        _ = session.Process(CreateEventBatch(eventSequence: 1));
        Sha256Hex.TryCreate(
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            out var wrongHash);

        var result = session.Process(CreateDecisionApplied(wrongHash!));

        AssertError(result, "HASH_MISMATCH", "failSession");
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Unsupported_minor_is_recoverable_but_major_fails_session()
    {
        var recoverable = CreateSession();
        var minor = recoverable.Process(
            Encoding.UTF8.GetBytes(
                """{"schemaVersion":"1.1.0","messageType":"hello","payload":{}}"""));
        AssertError(minor, "UNSUPPORTED_SCHEMA_MINOR", "rejectMessage");
        Assert.Equal(RunnerSessionStatus.New, recoverable.Status);
        Assert.Equal(
            "helloAck",
            recoverable.Process(ReadFixture("hello/valid-hello.json"))
                .Response?
                .MessageType
                .Value);

        var fatal = CreateSession();
        var major = fatal.Process(
            Encoding.UTF8.GetBytes(
                """{"schemaVersion":"2.0.0","messageType":"hello","payload":{}}"""));
        AssertError(major, "UNSUPPORTED_SCHEMA_MAJOR", "failSession");
        Assert.Equal(RunnerSessionStatus.Failed, fatal.Status);
    }

    [Fact]
    public void Reinitialize_is_rejected_without_mutating_active_identity()
    {
        var session = CreateInitializedSession();

        var repeated = session.Process(
            ReadFixture("initialize/valid-initialize-run.json"));

        AssertError(repeated, "INVALID_SESSION_STATE", "rejectMessage");
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
    }

    private static RunnerSession CreateInitializedSession()
    {
        var session = CreateSession();
        _ = session.Process(ReadFixture("hello/valid-hello.json"));
        _ = session.Process(ReadFixture("initialize/valid-initialize-run.json"));
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        return session;
    }

    private static RunnerSession CreateSession() =>
        new(RunnerDefaults.CapabilityRequirements);

    private static byte[] ReadFixture(string path) =>
        Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path));

    private static byte[] CreateEventBatch(
        long eventSequence,
        string eventType = "timerTick",
        long epoch = 1,
        long simTime = 100) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"eventBatch",
              "runId":"run-001",
              "scenarioId":"manhattan-20260729-a",
              "epochId":{{epoch}},
              "simTimeMs":{{simTime}},
              "payload":{
                "events":[
                  {
                    "eventSeq":{{eventSequence}},
                    "eventType":"{{eventType}}",
                    "payload":{{(eventType == "timerTick" ? "{}" : """{"incidentId":"incident-1"}""")}}
                  }
                ]
              }
            }
            """);

    private static byte[] CreateDecisionApplied(Sha256Hex decisionHash) =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion":"1.0.0",
              "messageType":"decisionApplied",
              "runId":"run-001",
              "scenarioId":"manhattan-20260729-a",
              "epochId":1,
              "simTimeMs":100,
              "payload":{"decisionHash":"{{decisionHash.Value}}"}
            }
            """);

    private static void AssertError(
        RunnerSessionResult result,
        string code,
        string disposition)
    {
        Assert.Equal("error", result.Response?.MessageType.Value);
        var payload = result.Response!.Payload;
        Assert.Equal(code, payload.GetProperty("code").GetString());
        Assert.Equal(
            disposition,
            payload.GetProperty("disposition").GetString());
    }
}
