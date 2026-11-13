using System.Text;
using System.Text.Json;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;

namespace RideBound.Contracts.Tests.Protocol;

public sealed class EventDecisionMessageTests
{
    [Fact]
    public void Event_batch_preserves_order_and_every_v1_event_type()
    {
        var eventTypes = Enum.GetValues<EventType>();
        var events = eventTypes.Select(
                (eventType, index) =>
                {
                    EventSequence.TryCreate(index + 1, out var sequence);
                    return new ProtocolEvent(
                        sequence,
                        eventType,
                        CreatePayload(eventType));
                })
            .ToArray();
        var encoded = EventBatchPayloadCodec.Encode(new EventBatchPayload(events));
        using var document = JsonDocument.Parse(encoded);

        var decoded = EventBatchPayloadCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess);
        Assert.Equal(eventTypes, decoded.Value!.Events.Select(value => value.EventType));
        Assert.Equal(
            Enumerable.Range(1, eventTypes.Length).Select(value => (long)value),
            decoded.Value.Events.Select(value => value.EventSequence.Value));
    }

    [Theory]
    [InlineData("""{"events":[]}""", ProtocolPayloadErrorCode.InvalidValue)]
    [InlineData(
        """{"events":[{"eventSeq":1,"eventType":"unknown","payload":{}}]}""",
        ProtocolPayloadErrorCode.InvalidValue)]
    [InlineData(
        """{"events":[{"eventSeq":1,"eventType":"timerTick","payload":[] }]}""",
        ProtocolPayloadErrorCode.InvalidFieldType)]
    [InlineData(
        """{"events":[{"eventSeq":1,"eventType":"timerTick","payload":{},"extra":1}]}""",
        ProtocolPayloadErrorCode.UnknownField)]
    public void Event_batch_rejects_invalid_structure(
        string json,
        ProtocolPayloadErrorCode expected)
    {
        using var document = JsonDocument.Parse(json);

        var result = EventBatchPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error?.Code);
    }

    [Fact]
    public void Wp1_decision_shell_cannot_claim_actions_or_validation()
    {
        var json = """
            {
              "status":"notProduced",
              "reasonCode":"WP1_STRUCTURAL_ONLY",
              "actions":[{
                "decisionType":"requestAccepted",
                "payload":{
                  "requestId":"r-1",
                  "vehicleId":"v-1",
                  "candidateId":"candidate-1"
                }
              }],
              "certificate":{
                "status":"notProduced",
                "reasonCode":"COMMITMENT_VALIDATOR_NOT_AVAILABLE"
              },
              "solver":{"status":"notRun"},
              "stateBeforeHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "stateAfterHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "previousDecisionHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "decisionHash":"0000000000000000000000000000000000000000000000000000000000000000"
            }
            """;
        using var document = JsonDocument.Parse(json);

        var result = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot contain actions", result.Error?.Message);
    }

    [Fact]
    public void Decision_actions_preserve_every_v1_type_in_order()
    {
        Sha256Hex.TryCreate(
            "0000000000000000000000000000000000000000000000000000000000000000",
            out var zero);
        var decisionTypes = Enum.GetValues<DecisionType>();
        var payload = new DecisionPayload(
            DecisionProductionStatus.Produced,
            DecisionReasonCodes.Accepted,
            decisionTypes.Select(CreateAction).ToArray(),
            new CertificateShell(
                CertificateStatus.Produced,
                "VALIDATED",
                Certificate(zero!) with
                {
                    PublicationIds = ["publication-1"],
                    PromiseCount = 1,
                }),
            new SolverStatusShell(SolverStatus.Completed),
            zero!,
            zero!,
            zero!,
            zero!);
        var encoded = DecisionPayloadCodec.Encode(payload);
        using var document = JsonDocument.Parse(encoded);

        var decoded = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess);
        Assert.Equal(
            decisionTypes,
            decoded.Value!.Actions.Select(
                value =>
                {
                    DecisionTypeVocabulary.TryParse(
                        value.GetProperty("decisionType").GetString(),
                        out var decisionType);
                    return decisionType;
                }));
    }

    [Fact]
    public void Solver_execution_evidence_is_kept_as_opaque_json()
    {
        // The Runner no longer writes solver execution evidence. A decision that has it decodes with the
        // evidence kept as it is, whatever its content, and encodes it back unchanged.
        const string evidence = """{"evidenceVersion":"9.9.9","anything":[1,{"nested":true}]}""";

        var decoded = DecodeSolverEvidence(evidence);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        Assert.Equal(evidence, decoded.Value!.Solver.ExecutionEvidence!.Value.GetRawText());
        Assert.Contains(
            "\"executionEvidence\":" + evidence,
            Encoding.UTF8.GetString(DecisionPayloadCodec.Encode(decoded.Value)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_decision_reason_is_rejected_as_contract_drift()
    {
        var json = """
            {
              "status":"notProduced",
              "reasonCode":"SOMETHING_NEW",
              "actions":[],
              "certificate":{
                "status":"notProduced",
                "reasonCode":"COMMITMENT_VALIDATOR_NOT_AVAILABLE"
              },
              "solver":{"status":"notRun"},
              "stateBeforeHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "stateAfterHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "previousDecisionHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "decisionHash":"0000000000000000000000000000000000000000000000000000000000000000"
            }
            """;
        using var document = JsonDocument.Parse(json);

        var result = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal("$.payload.reasonCode", result.Error?.Field);
    }

    [Fact]
    public void Online_decision_actions_reject_unknown_payload_fields()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "status":"produced",
              "reasonCode":"ACCEPTED",
              "actions":[{
                "decisionType":"requestAccepted",
                "payload":{
                  "requestId":"r-1",
                  "vehicleId":"v-1",
                  "candidateId":"candidate-1",
                  "extra":true
                }
              }],
              "certificate":{
                "status":"notProduced",
                "reasonCode":"COMMITMENT_VALIDATOR_NOT_AVAILABLE"
              },
              "solver":{"status":"notRun"},
              "stateBeforeHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "stateAfterHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "previousDecisionHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "decisionHash":"0000000000000000000000000000000000000000000000000000000000000000"
            }
            """);

        var result = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.UnknownField, result.Error?.Code);
    }

    [Fact]
    public void Error_taxonomy_requires_code_and_disposition_to_match()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "code":"EVENT_SEQUENCE_GAP",
              "disposition":"rejectMessage",
              "message":"wrong severity"
            }
            """);

        var result = ErrorPayloadCodec.Decode(document.RootElement);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProtocolPayloadErrorCode.InvalidValue, result.Error?.Code);
    }

    private static JsonElement CreateAction(DecisionType decisionType)
    {
        return decisionType switch
        {
            DecisionType.RequestAccepted => OnlineDecisionActionCodec.Encode(
                new OnlineDecisionAction(
                    decisionType,
                    new RequestAcceptedActionPayload(
                        "r-1",
                        "v-1",
                        "candidate-1"))),
            DecisionType.RequestRejected or DecisionType.RequestDeferred =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        decisionType,
                        new RequestOutcomeActionPayload(
                            "r-1",
                            "NO_FEASIBLE_INSERTION"))),
            DecisionType.VehiclePlanUpdated =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        decisionType,
                        new VehiclePlanUpdatedActionPayload(
                            "v-1",
                            "candidate-1",
                            new RoutePlanContract(0, 0, [], [])))),
            DecisionType.PromisePublished => OnlineDecisionActionCodec.Encode(
                new OnlineDecisionAction(
                    decisionType,
                    new PromisePublishedActionPayload(
                        "publication-1",
                        1,
                        "INITIAL_ACCEPTANCE",
                        1,
                        Promise(),
                        ZeroVector(),
                        ZeroVector(),
                        ZeroVector(),
                        ZeroVector(),
                        ZeroVector()))),
            DecisionType.CommitmentBreachDeclared =>
                OnlineDecisionActionCodec.Encode(
                    new OnlineDecisionAction(
                        decisionType,
                        new CommitmentBreachDeclaredActionPayload(
                            "breach-1",
                            "incident-1",
                            "r-1",
                            1,
                            ["pickup_eta_total_ms"],
                            ZeroVector(),
                            ZeroVector()))),
            _ => ParseObject(
                $$"""
                {
                  "decisionType":"{{DecisionTypeVocabulary.ToProtocolValue(decisionType)}}",
                  "payload":{}
                }
                """),
        };
    }

    [Fact]
    public void Produced_certificate_requires_body_and_body_changes_decision_hash()
    {
        var zeroText = new string('0', 64);
        Sha256Hex.TryCreate(zeroText, out var zero);
        using var missingBody = JsonDocument.Parse(
            $$"""
            {
              "status":"produced",
              "reasonCode":"ACCEPTED",
              "actions":[],
              "certificate":{"status":"produced","reasonCode":"VALIDATED"},
              "solver":{"status":"completed"},
              "stateBeforeHash":"{{zeroText}}",
              "stateAfterHash":"{{zeroText}}",
              "previousDecisionHash":"{{zeroText}}",
              "decisionHash":"{{zeroText}}"
            }
            """);

        var rejected = DecisionPayloadCodec.Decode(missingBody.RootElement);

        Assert.False(rejected.IsSuccess);
        Assert.Equal("$.payload.certificate.body", rejected.Error?.Field);

        var first = new DecisionPayload(
            DecisionProductionStatus.Produced,
            DecisionReasonCodes.Accepted,
            [],
            new CertificateShell(
                CertificateStatus.Produced,
                "VALIDATED",
                Certificate(zero!)),
            new SolverStatusShell(SolverStatus.Completed),
            zero!, zero!, zero!, zero!);
        var changed = first with
        {
            Certificate = first.Certificate with
            {
                Body = first.Certificate.Body! with { PromiseCount = 1 },
            },
        };

        Assert.False(
            CanonicalJson.Canonicalize(
                    DecisionPayloadCodec.Encode(first, hashProjection: true))
                .SequenceEqual(
                    CanonicalJson.Canonicalize(
                        DecisionPayloadCodec.Encode(
                            changed,
                            hashProjection: true))));
    }

    [Fact]
    public void Produced_certificate_is_bound_to_decision_states_and_publication_actions()
    {
        Sha256Hex.TryCreate(new string('0', 64), out var zero);
        Sha256Hex.TryCreate(new string('1', 64), out var other);
        var action = CreateAction(DecisionType.PromisePublished);
        var valid = new DecisionPayload(
            DecisionProductionStatus.Produced,
            DecisionReasonCodes.Accepted,
            [action],
            new CertificateShell(
                CertificateStatus.Produced,
                "VALIDATED",
                Certificate(zero!) with
                {
                    PublicationIds = ["publication-1"],
                    PromiseCount = 1,
                }),
            new SolverStatusShell(SolverStatus.NotRun),
            zero!, zero!, zero!, zero!);

        var encoded = DecisionPayloadCodec.Encode(valid);
        using var document = JsonDocument.Parse(encoded);
        Assert.True(DecisionPayloadCodec.Decode(document.RootElement).IsSuccess);

        var wrongState = valid with
        {
            Certificate = valid.Certificate with
            {
                Body = valid.Certificate.Body! with
                {
                    ProposedStateHash = other!,
                },
            },
        };
        var wrongPublication = valid with
        {
            Certificate = valid.Certificate with
            {
                Body = valid.Certificate.Body! with
                {
                    PublicationIds = ["publication-other"],
                },
            },
        };

        Assert.Throws<ArgumentException>(
            () => DecisionPayloadCodec.Encode(wrongState));
        Assert.Throws<ArgumentException>(
            () => DecisionPayloadCodec.Encode(wrongPublication));
    }

    [Fact]
    public void Certificate_normal_operation_and_witnesses_are_consistent()
    {
        Sha256Hex.TryCreate(new string('0', 64), out var zero);
        var nonNormal = Certificate(zero!) with
        {
            NormalOperation = false,
            Witnesses =
            [
                new CertificateWitnessContract(
                    "budget",
                    "COMMITMENT_BUDGET_EXCEEDED",
                    RequestId: "r-1",
                    Dimension: "pickup_eta_total_ms",
                    Limit: 10,
                    Before: 7,
                    Delta: 4,
                    After: 11),
            ],
        };
        var payload = new DecisionPayload(
            DecisionProductionStatus.Produced,
            DecisionReasonCodes.IncidentOverride,
            [],
            new CertificateShell(
                CertificateStatus.Produced,
                DecisionReasonCodes.IncidentOverride,
                nonNormal),
            new SolverStatusShell(SolverStatus.SafeFallback),
            zero!, zero!, zero!, zero!);

        var encoded = DecisionPayloadCodec.Encode(payload);
        using var document = JsonDocument.Parse(encoded);
        var decoded = DecisionPayloadCodec.Decode(document.RootElement);

        Assert.True(decoded.IsSuccess, decoded.Error?.Message);
        var witness = Assert.Single(
            decoded.Value!.Certificate.Body!.Witnesses);
        Assert.Equal(10, witness.Limit);
        Assert.Equal(11, witness.After);

        var inconsistent = payload with
        {
            Certificate = payload.Certificate with
            {
                Body = nonNormal with { NormalOperation = true },
            },
        };
        Assert.Throws<ArgumentException>(
            () => DecisionPayloadCodec.Encode(inconsistent));
    }

    private static CommitmentCertificateBody Certificate(Sha256Hex hash) =>
        new(
            "1.0.0",
            "commitment-validator-v1",
            true,
            hash,
            hash,
            [],
            1,
            0,
            []);

    private static CommitmentVectorContract ZeroVector() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static PromiseProjectionContract Promise() =>
        new(
            "r-1",
            "v-1",
            "pickup",
            "n-1",
            "drop",
            "n-2",
            1_000,
            2_000,
            [
                new PromiseServiceTokenContract(
                    "pickup",
                    "r-1",
                    RouteStopKind.Pickup),
                new PromiseServiceTokenContract(
                    "drop",
                    "r-1",
                    RouteStopKind.DropOff),
            ]);

    [Fact]
    public void Error_encoder_sanitizes_multiline_diagnostic()
    {
        var encoded = ErrorPayloadCodec.Encode(
            new ErrorPayload(
                "MALFORMED_JSON",
                ProtocolFailureDisposition.RejectMessage,
                "bad\r\nline"));

        Assert.DoesNotContain((byte)'\r', encoded);
        Assert.DoesNotContain((byte)'\n', encoded);
        using var document = JsonDocument.Parse(encoded);
        Assert.Equal(
            "bad  line",
            document.RootElement.GetProperty("message").GetString());
    }

    private static JsonElement ParseObject(string json)
    {
        using var document = JsonDocument.Parse(Encoding.UTF8.GetBytes(json));
        return document.RootElement.Clone();
    }

    private static ProtocolPayloadDecodeResult<DecisionPayload>
        DecodeSolverEvidence(string evidence)
    {
        using var document = JsonDocument.Parse(
            $$"""
            {
              "status":"produced",
              "reasonCode":"ACCEPTED",
              "actions":[],
              "certificate":{
                "status":"notProduced",
                "reasonCode":"COMMITMENT_VALIDATOR_NOT_AVAILABLE"
              },
              "solver":{
                "status":"completed",
                "executionEvidence":{{evidence}}
              },
              "stateBeforeHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "stateAfterHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "previousDecisionHash":"0000000000000000000000000000000000000000000000000000000000000000",
              "decisionHash":"0000000000000000000000000000000000000000000000000000000000000000"
            }
            """);
        return DecisionPayloadCodec.Decode(document.RootElement);
    }

    private static ProtocolEventPayload CreatePayload(EventType eventType)
    {
        var request = new RequestContract(
            "r-1",
            0,
            "n-1",
            "n-2",
            0,
            1000,
            1000,
            1,
            "standard",
            "uniform-v1");
        Sha256Hex.TryCreate(new string('a', 64), out var hash);

        return eventType switch
        {
            EventType.RequestArrived => new RequestArrivedEventPayload(request),
            EventType.BookingConfirmed
                or EventType.OfferDeclined
                or EventType.RequestCancelledBeforeAcceptance
                or EventType.RequestCancelledAfterAcceptance =>
                new RequestReferenceEventPayload("r-1"),
            EventType.VehicleAdvanced => new VehicleAdvancedEventPayload(
                new VehicleSnapshotContract(
                    "v-1",
                    4,
                    0,
                    new NodePositionContract("n-1"),
                    [],
                    [],
                    new RoutePlanContract(0, 0, [], []))),
            EventType.VehicleReachedStop => new VehicleReachedStopEventPayload(
                "v-1",
                "s-1",
                0,
                new NodePositionContract("n-1")),
            EventType.PassengerBoarded or EventType.PassengerAlighted =>
                new PassengerEventPayload("v-1", "r-1", 0),
            EventType.TravelTimesUpdated => new TravelTimesUpdatedEventPayload(
                new TravelTimeSnapshotContract(
                    1,
                    hash!,
                    [new TravelArcContract("n-1", "n-2", 1)])),
            EventType.TimerTick => TimerTickEventPayload.Instance,
            EventType.IncidentOpened => new IncidentOpenedEventPayload(
                "incident-1",
                "ROAD_CLOSED",
                ["v-1"]),
            EventType.IncidentResolved => new IncidentResolvedEventPayload(
                "incident-1"),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType)),
        };
    }
}
