using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Contracts.Tests.Fixtures;
using RideBound.Runner.Protocol;
using RideBound.Runner.Tests.Final;

namespace RideBound.Runner.Tests.Protocol;

/// <summary>
/// The protocol rules of a Runner session (state edges, event ordering, retried batches, the decisionApplied
/// handshake), checked on the final experiment's configuration: the T1 session FS-G2-SHRC71-CL.light-v1 of
/// <see cref="FinalRuns"/>, run inside the test process. Its first event batch holds events 1-4 at epoch 1.
/// </summary>
public sealed class RunnerSessionTests
{
    private static readonly FinalRun Run = FinalRuns.Get("FS-G2-SHRC71-CL.light-v1");

    [Fact]
    public void Session_moves_new_to_negotiated_to_initialized()
    {
        var session = CreateSession();

        var hello = session.Process(Hello());
        var initialize = session.Process(InitializeRun());

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
    public void Event_batch_returns_a_produced_decision_and_awaits_its_acknowledgement()
    {
        var session = CreateInitializedSession();

        var result = session.Process(Batch(1));

        Assert.Equal("decision", result.Response?.MessageType.Value);
        var decision = DecisionPayloadCodec.Decode(result.Response!.Payload);
        Assert.True(decision.IsSuccess);
        Assert.Equal(DecisionProductionStatus.Produced, decision.Value?.Status);
        Assert.NotEmpty(decision.Value!.Actions);
        Assert.Equal(CertificateStatus.Produced, decision.Value.Certificate.Status);
        Assert.Equal(SolverStatus.Completed, decision.Value.Solver.Status);
        Assert.Equal(
            RecordedDecision(epoch: 1),
            Encoding.UTF8.GetString(CanonicalJson.Serialize(result.Response)));
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(1, session.NextEventSequence);
    }

    [Fact]
    public void Exact_duplicate_returns_cached_response_without_advancing_hash()
    {
        var session = CreateInitializedSession();
        var batch = Batch(1);
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
        var batch = Utf8(
            JsonNode.Parse(BatchLine(1))!.ToJsonString(
                new JsonSerializerOptions { WriteIndented = true }));
        var canonical = CanonicalJson.Canonicalize(batch);
        Assert.NotEqual(batch, canonical);
        var first = session.Process(batch);

        var retry = session.Process(canonical);

        Assert.Equal(
            CanonicalJson.Serialize(first.Response!),
            CanonicalJson.Serialize(retry.Response!));
        Assert.Equal(RunnerSessionStatus.AwaitingDecisionApplied, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    /// <summary>
    /// The published duplicate fixture, sent unchanged twice to a session initialized with the fixture's run and
    /// scenario identifiers. Its only event is a timer tick, and the first online state of a commitment session
    /// needs a travel snapshot, so both copies get the same recoverable rejection and the session does not move.
    /// </summary>
    [Fact]
    public void Published_duplicate_fixture_is_rejected_identically_twice_before_a_travel_snapshot()
    {
        var fixture = ReadFixture(
            "golden/required/09-duplicate-event-idempotent/input.json");
        string runId, scenarioId;

        using (var document = JsonDocument.Parse(fixture))
        {
            runId = document.RootElement.GetProperty("runId").GetString()!;
            scenarioId = document.RootElement.GetProperty("scenarioId").GetString()!;
        }

        var session = CreateSession();
        _ = session.Process(Hello());
        var initialize = JsonNode.Parse(InitializeRun())!;
        initialize["runId"] = runId;
        initialize["scenarioId"] = scenarioId;
        _ = session.Process(Utf8(initialize.ToJsonString()));
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);

        var first = session.Process(fixture);
        var second = session.Process(fixture);

        AssertError(first, "SCHEMA_VALIDATION_FAILED", "rejectMessage");
        Assert.Equal(
            "The first proposed online state requires a travel snapshot.",
            first.Response!.Payload.GetProperty("message").GetString());
        Assert.Equal(
            CanonicalJson.Serialize(first.Response!),
            CanonicalJson.Serialize(second.Response!));
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(1, session.NextEventSequence);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Same_duplicate_key_with_changed_payload_fails_session()
    {
        var session = CreateInitializedSession();
        _ = session.Process(Batch(1));

        var conflict = session.Process(
            ChangedBatch(1, batch => FirstArc(batch)["travelTimeMs"] = 3001));

        AssertError(conflict, "DUPLICATE_PAYLOAD_CONFLICT", "failSession");
        Assert.Equal(RunnerSessionStatus.Failed, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
    }

    [Fact]
    public void Same_duplicate_key_with_changed_simulation_time_fails_session()
    {
        var session = CreateInitializedSession();
        _ = session.Process(Batch(1));

        var conflict = session.Process(
            ChangedBatch(1, batch => batch["simTimeMs"] = batch["simTimeMs"]!.GetValue<long>() + 1));

        AssertError(conflict, "DUPLICATE_PAYLOAD_CONFLICT", "failSession");
        Assert.Equal(RunnerSessionStatus.Failed, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
        Assert.Equal(ProtocolHash.ZeroHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Event_gap_and_overlap_fail_without_sorting_or_buffering()
    {
        var gapSession = CreateInitializedSession();
        var gap = gapSession.Process(
            ChangedBatch(1, batch => Renumber(batch, firstSequence: 2)));
        AssertError(gap, "EVENT_SEQUENCE_GAP", "failSession");

        var overlapSession = CreateInitializedSession();
        _ = overlapSession.Process(Batch(1));
        var decision = DecisionPayloadCodec.Decode(
            overlapSession.Process(Batch(1))
                .Response!
                .Payload)
            .Value!;
        _ = overlapSession.Process(DecisionApplied(1, decision.DecisionHash));
        var overlap = overlapSession.Process(
            ChangedBatch(2, batch => Renumber(batch, firstSequence: 1)));
        AssertError(overlap, "EVENT_SEQUENCE_OVERLAP", "failSession");
    }

    [Fact]
    public void Epoch_gap_and_sequence_overflow_fail_deterministically()
    {
        var epochGapSession = CreateInitializedSession();
        var epochGap = epochGapSession.Process(
            ChangedBatch(1, batch => batch["epochId"] = 2));
        AssertError(epochGap, "EPOCH_GAP", "failSession");

        var overflowSession = CreateInitializedSession();
        var overflow = overflowSession.Process(
            SingleEventBatch(ProtocolLimits.MaxCanonicalInteger));
        AssertError(overflow, "EVENT_SEQUENCE_GAP", "failSession");
    }

    [Fact]
    public void Ordering_validator_rejects_the_last_integer_before_increment_overflow()
    {
        var bytes = SingleEventBatch(ProtocolLimits.MaxCanonicalInteger);
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
            session.Process(InitializeRun()),
            "INVALID_SESSION_STATE",
            "rejectMessage");
        AssertError(
            session.Process(Batch(1)),
            "INVALID_SESSION_STATE",
            "rejectMessage");
        Assert.Equal(RunnerSessionStatus.New, session.Status);

        _ = session.Process(Hello());
        AssertError(
            session.Process(Hello()),
            "INVALID_SESSION_STATE",
            "rejectMessage");
        Assert.Equal(RunnerSessionStatus.Negotiated, session.Status);
    }

    [Fact]
    public void Decision_applied_is_the_only_point_that_advances_epoch_and_hash()
    {
        var session = CreateInitializedSession();
        var response = session.Process(Batch(1));
        var decision = DecisionPayloadCodec.Decode(response.Response!.Payload).Value!;

        var applied = session.Process(DecisionApplied(1, decision.DecisionHash));

        Assert.Null(applied.Response);
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        Assert.Equal(1, session.AppliedEpoch);
        Assert.Equal(5, session.NextEventSequence);
        Assert.Equal(decision.DecisionHash, session.PreviousDecisionHash);
    }

    [Fact]
    public void Wrong_decision_applied_hash_fails_session_without_advancing()
    {
        var session = CreateInitializedSession();
        _ = session.Process(Batch(1));
        Sha256Hex.TryCreate(
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            out var wrongHash);

        var result = session.Process(DecisionApplied(1, wrongHash!));

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
            recoverable.Process(Hello())
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

        var repeated = session.Process(InitializeRun());

        AssertError(repeated, "INVALID_SESSION_STATE", "rejectMessage");
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        Assert.Equal(0, session.AppliedEpoch);
    }

    private static RunnerSession CreateInitializedSession()
    {
        var session = CreateSession();
        _ = session.Process(Hello());
        _ = session.Process(InitializeRun());
        Assert.Equal(RunnerSessionStatus.Initialized, session.Status);
        return session;
    }

    private static RunnerSession CreateSession() => FinalRuns.NewSession(Run);

    private static byte[] Hello() => Utf8(Scenario("hello").Single());

    private static byte[] InitializeRun() => Utf8(Scenario("initializeRun").Single());

    private static string BatchLine(int epoch) => Scenario("eventBatch")[epoch - 1];

    private static byte[] Batch(int epoch) => Utf8(BatchLine(epoch));

    private static byte[] ChangedBatch(int epoch, Action<JsonNode> change)
    {
        var batch = JsonNode.Parse(BatchLine(epoch))!;
        change(batch);
        return Utf8(batch.ToJsonString());
    }

    /// <summary>The first batch cut to its first event (the travel-time snapshot), numbered
    /// <paramref name="sequence"/>.</summary>
    private static byte[] SingleEventBatch(long sequence) =>
        ChangedBatch(
            1,
            batch =>
            {
                var events = batch["payload"]!["events"]!.AsArray();

                while (events.Count > 1)
                {
                    events.RemoveAt(events.Count - 1);
                }

                Renumber(batch, sequence);
            });

    private static void Renumber(JsonNode batch, long firstSequence)
    {
        var sequence = firstSequence;

        foreach (var item in batch["payload"]!["events"]!.AsArray())
        {
            item!["eventSeq"] = sequence++;
        }
    }

    private static JsonNode FirstArc(JsonNode batch) =>
        batch["payload"]!["events"]![0]!["payload"]!["snapshot"]!["arcs"]![0]!;

    /// <summary>The adapter's acknowledgement of the decision made for the batch of <paramref name="epoch"/>.</summary>
    private static byte[] DecisionApplied(int epoch, Sha256Hex decisionHash)
    {
        var batch = JsonNode.Parse(BatchLine(epoch))!;
        var applied = new JsonObject
        {
            ["schemaVersion"] = "1.0.0",
            ["messageType"] = "decisionApplied",
            ["runId"] = batch["runId"]!.GetValue<string>(),
            ["scenarioId"] = batch["scenarioId"]!.GetValue<string>(),
            ["epochId"] = batch["epochId"]!.GetValue<long>(),
            ["simTimeMs"] = batch["simTimeMs"]!.GetValue<long>(),
            ["payload"] = new JsonObject { ["decisionHash"] = decisionHash.Value },
        };
        return Utf8(applied.ToJsonString());
    }

    /// <summary>The decision line recorded for <paramref name="epoch"/> in the T1 golden (kept verbatim there).</summary>
    private static string RecordedDecision(int epoch) =>
        FinalRuns.RecordedLines(Run)
            .Where(line => line.StartsWith('{') && RunnerProcess.MessageType(line) == "decision")
            .ElementAt(epoch - 1);

    private static string[] Scenario(string messageType) =>
        FinalRuns.ScenarioLines(Run)
            .Where(line => RunnerProcess.MessageType(line) == messageType)
            .ToArray();

    private static byte[] Utf8(string json) => Encoding.UTF8.GetBytes(json);

    private static byte[] ReadFixture(string path) =>
        Encoding.UTF8.GetBytes(FixtureLoader.ReadUtf8(path));

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
