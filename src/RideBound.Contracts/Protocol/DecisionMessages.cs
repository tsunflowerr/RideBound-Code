using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public enum DecisionType
{
    OfferProposed,
    RequestAccepted,
    RequestRejected,
    RequestDeferred,
    VehiclePlanUpdated,
    PromisePublished,
    CommitmentBreachDeclared,
}

public enum DecisionProductionStatus
{
    NotProduced,
    Produced,
}

public enum CertificateStatus
{
    NotProduced,
    Produced,
}

public enum SolverStatus
{
    NotRun,
    Completed,
    SafeFallback,
}

public sealed record CertificateShell(
    CertificateStatus Status,
    string ReasonCode,
    CommitmentCertificateBody? Body = null);

public sealed record SolverStatusShell(
    SolverStatus Status,
    JsonElement? ExecutionEvidence = null);

public sealed record DecisionPayload(
    DecisionProductionStatus Status,
    string ReasonCode,
    IReadOnlyList<JsonElement> Actions,
    CertificateShell Certificate,
    SolverStatusShell Solver,
    Sha256Hex StateBeforeHash,
    Sha256Hex StateAfterHash,
    Sha256Hex PreviousDecisionHash,
    Sha256Hex DecisionHash);

public static class DecisionTypeVocabulary
{
    private static readonly IReadOnlyDictionary<string, DecisionType> ByWireValue =
        Enum.GetValues<DecisionType>().ToDictionary(
            ToProtocolValue,
            value => value,
            StringComparer.Ordinal);

    public static bool TryParse(string? value, out DecisionType decisionType) =>
        ByWireValue.TryGetValue(value ?? string.Empty, out decisionType);

    public static string ToProtocolValue(DecisionType decisionType) =>
        decisionType switch
        {
            DecisionType.OfferProposed => "offerProposed",
            DecisionType.RequestAccepted => "requestAccepted",
            DecisionType.RequestRejected => "requestRejected",
            DecisionType.RequestDeferred => "requestDeferred",
            DecisionType.VehiclePlanUpdated => "vehiclePlanUpdated",
            DecisionType.PromisePublished => "promisePublished",
            DecisionType.CommitmentBreachDeclared =>
                "commitmentBreachDeclared",
            _ => throw new ArgumentOutOfRangeException(nameof(decisionType)),
        };
}

public static class DecisionReasonCodes
{
    public const string Accepted = "ACCEPTED";

    public const string NoFeasibleInsertion = "NO_FEASIBLE_INSERTION";

    public const string Capacity = "CAPACITY";

    public const string TimeWindow = "TIME_WINDOW";

    public const string MaxRideTime = "MAX_RIDE_TIME";

    public const string FrozenPrefix = "FROZEN_PREFIX";

    public const string CommitmentBudget = "COMMITMENT_BUDGET";

    public const string SolverTimeoutSafeFallback = "SOLVER_TIMEOUT_SAFE_FALLBACK";

    public const string IncidentOverride = "INCIDENT_OVERRIDE";

    public const string Wp1StructuralOnly = "WP1_STRUCTURAL_ONLY";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly(
        [
            Accepted,
            NoFeasibleInsertion,
            Capacity,
            TimeWindow,
            MaxRideTime,
            FrozenPrefix,
            CommitmentBudget,
            SolverTimeoutSafeFallback,
            IncidentOverride,
            Wp1StructuralOnly,
        ]);
}

public static class DecisionPayloadCodec
{
    private const string CandidatePortfolioSchemaId =
        "https://ridebound.local/schemas/wp13/v1/" +
        "runner-retained-candidate-portfolio-evidence.schema.json";

    public const string StructuralOnlyReasonCode =
        DecisionReasonCodes.Wp1StructuralOnly;

    public const string CertificateNotAvailableReasonCode =
        "COMMITMENT_VALIDATOR_NOT_AVAILABLE";

    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "status",
            "reasonCode",
            "actions",
            "certificate",
            "solver",
            "stateBeforeHash",
            "stateAfterHash",
            "previousDecisionHash",
            "decisionHash",
        };

    private static readonly IReadOnlySet<string> CertificateFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "status",
            "reasonCode",
            "body",
        };

    private static readonly IReadOnlySet<string> ActionFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "decisionType",
            "payload",
        };

    private static readonly IReadOnlySet<string> SolverFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "status",
            "executionEvidence",
        };

    private static readonly IReadOnlySet<string> SolverEvidenceFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "evidenceVersion",
            "generation",
            "candidatePortfolio",
            "prunedCandidates",
            "selection",
        };

    private static readonly IReadOnlySet<string> CandidatePortfolioFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "portfolioVersion",
            "schemaId",
            "objectiveProfile",
            "generatedCandidateCount",
            "policyEligibleCandidateCount",
            "selectedCandidateIds",
            "selectionProblem",
            "candidates",
        };

    private static readonly IReadOnlySet<string> PortfolioProblemFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "vehicleIds",
            "requestIds",
            "objectiveLevels",
        };

    private static readonly IReadOnlySet<string> PortfolioObjectiveFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "levelIndex",
            "name",
            "sense",
            "aggregation",
        };

    private static readonly IReadOnlySet<string> PortfolioCandidateFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "candidateId",
            "vehicleId",
            "newRequestIds",
            "isNoOp",
            "scheduleStrategy",
            "relocatedWaitMs",
            "certifiedForwardSlackMs",
            "repairedIncumbentRequestId",
            "route",
            "schedule",
            "policyEligibility",
            "objectiveContributions",
        };

    // Solver evidence v1.3.0 (G2): a portfolio candidate may also name the waiting rider it moves.
    private static readonly IReadOnlySet<string> RelocationPortfolioCandidateFields =
        new HashSet<string>(PortfolioCandidateFields, StringComparer.Ordinal)
        {
            "relocatedRequestId",
            "relocationRole",
        };

    private static readonly IReadOnlySet<string> PortfolioRouteFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "planVersion",
            "executedStopCount",
            "frozenPrefix",
            "mutableSuffix",
        };

    private static readonly IReadOnlySet<string> PortfolioRouteStopFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "stopId",
            "nodeId",
            "kind",
            "requestId",
            "serviceDurationMs",
        };

    private static readonly IReadOnlySet<string> PortfolioScheduleFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "operationalCost",
            "stops",
        };

    private static readonly IReadOnlySet<string> PortfolioScheduledStopFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "stopId",
            "arrivalTimeMs",
            "serviceStartTimeMs",
            "departureTimeMs",
        };

    public static ProtocolPayloadDecodeResult<DecisionPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            "$.payload",
            Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(objectError);
        }

        var statusText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "status");
        var reasonCode = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "reasonCode");
        var actionsProperty = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "actions");
        var certificateProperty = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "certificate");
        var solverProperty = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "solver");
        var beforeHashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "stateBeforeHash");
        var afterHashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "stateAfterHash");
        var previousHashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "previousDecisionHash");
        var decisionHashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "decisionHash");
        var firstError = HelloPayloadCodec.FirstError(
            statusText.Error,
            reasonCode.Error,
            actionsProperty.Error,
            certificateProperty.Error,
            solverProperty.Error,
            beforeHashText.Error,
            afterHashText.Error,
            previousHashText.Error,
            decisionHashText.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(firstError);
        }

        if (!TryParseDecisionStatus(statusText.Value, out var status))
        {
            return Invalid("$.payload.status", "Unknown decision production status.");
        }

        if (actionsProperty.Value.ValueKind != JsonValueKind.Array)
        {
            return WrongType("$.payload.actions", "an array");
        }

        var actions = new List<JsonElement>();

        foreach (var action in actionsProperty.Value.EnumerateArray())
        {
            var actionPath = $"$.payload.actions[{actions.Count}]";
            var actionObjectError = ProtocolPayloadReader.ValidateObject(
                action,
                actionPath,
                ActionFields);

            if (actionObjectError is not null)
            {
                return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(
                    actionObjectError);
            }

            var decisionTypeText = ProtocolPayloadReader.ReadRequiredString(
                action,
                actionPath,
                "decisionType");
            var actionPayload = ProtocolPayloadReader.ReadRequiredProperty(
                action,
                actionPath,
                "payload");
            var actionError = HelloPayloadCodec.FirstError(
                decisionTypeText.Error,
                actionPayload.Error);

            if (actionError is not null)
            {
                return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(
                    actionError);
            }

            if (!DecisionTypeVocabulary.TryParse(
                    decisionTypeText.Value,
                    out var decisionType))
            {
                return Invalid(
                    $"{actionPath}.decisionType",
                    $"Decision type '{decisionTypeText.Value}' is unknown for protocol v1.");
            }

            if (actionPayload.Value.ValueKind != JsonValueKind.Object)
            {
                return WrongType($"{actionPath}.payload", "an object");
            }

            var payloadError = OnlineDecisionActionCodec.ValidatePayload(
                decisionType,
                actionPayload.Value,
                $"{actionPath}.payload");

            if (payloadError is not null)
            {
                return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(
                    payloadError);
            }

            actions.Add(action.Clone());
        }

        var certificate = DecodeCertificate(certificateProperty.Value);

        if (!certificate.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(
                certificate.Error!);
        }

        var solver = DecodeSolver(solverProperty.Value);

        if (!solver.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(solver.Error!);
        }

        if (!TryReadHash(beforeHashText.Value, out var beforeHash)
            || !TryReadHash(afterHashText.Value, out var afterHash)
            || !TryReadHash(previousHashText.Value, out var previousHash)
            || !TryReadHash(decisionHashText.Value, out var decisionHash))
        {
            return Invalid(
                "$.payload",
                "Decision hash fields must be 64 lowercase hexadecimal characters.");
        }

        if (!DecisionReasonCodes.All.Contains(
                reasonCode.Value!,
                StringComparer.Ordinal))
        {
            return Invalid(
                "$.payload.reasonCode",
                $"Decision reason code '{reasonCode.Value}' is unknown for protocol v1.");
        }

        var semanticError = ValidateDecisionSemantics(
            status,
            actions,
            certificate.Value!,
            solver.Value!,
            beforeHash!,
            afterHash!);

        if (semanticError is not null)
        {
            return ProtocolPayloadDecodeResult<DecisionPayload>.Failure(
                semanticError);
        }

        return ProtocolPayloadDecodeResult<DecisionPayload>.Success(
            new DecisionPayload(
                status,
                reasonCode.Value!,
                actions,
                certificate.Value!,
                solver.Value!,
                beforeHash!,
                afterHash!,
                previousHash!,
                decisionHash!));
    }

    public static byte[] Encode(DecisionPayload payload, bool hashProjection = false)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidatePayloadForEncoding(payload);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("status", ToProtocolValue(payload.Status));
                writer.WriteString("reasonCode", payload.ReasonCode);
                writer.WritePropertyName("actions");
                writer.WriteStartArray();

                foreach (var action in payload.Actions)
                {
                    ValidateActionForEncoding(action, payload);
                    action.WriteTo(writer);
                }

                writer.WriteEndArray();
                writer.WritePropertyName("certificate");
                writer.WriteStartObject();
                writer.WriteString(
                    "status",
                    ToProtocolValue(payload.Certificate.Status));
                writer.WriteString(
                    "reasonCode",
                    payload.Certificate.ReasonCode);

                if (payload.Certificate.Body is not null)
                {
                    writer.WritePropertyName("body");
                    CommitmentContractCodec.WriteCertificateBody(
                        writer,
                        payload.Certificate.Body);
                }
                writer.WriteEndObject();
                writer.WritePropertyName("solver");
                writer.WriteStartObject();
                writer.WriteString(
                    "status",
                    ToProtocolValue(payload.Solver.Status));

                if (payload.Solver.ExecutionEvidence is { } executionEvidence)
                {
                    writer.WritePropertyName("executionEvidence");
                    executionEvidence.WriteTo(writer);
                }
                writer.WriteEndObject();
                writer.WriteString(
                    "stateBeforeHash",
                    payload.StateBeforeHash.Value);
                writer.WriteString(
                    "stateAfterHash",
                    payload.StateAfterHash.Value);

                if (!hashProjection)
                {
                    writer.WriteString(
                        "previousDecisionHash",
                        payload.PreviousDecisionHash.Value);
                    writer.WriteString(
                        "decisionHash",
                        payload.DecisionHash.Value);
                }

                writer.WriteEndObject();
            });
    }

    private static void ValidateActionForEncoding(
        JsonElement action,
        DecisionPayload payload)
    {
        if (action.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "Every decision action must be an object.",
                nameof(payload));
        }

        var names = action.EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        var fields = names.ToHashSet(StringComparer.Ordinal);

        if (fields.Count != names.Length
            || !fields.SetEquals(ActionFields)
            || action.GetProperty("decisionType").ValueKind != JsonValueKind.String
            || !DecisionTypeVocabulary.TryParse(
                action.GetProperty("decisionType").GetString(),
                out var decisionType)
            || action.GetProperty("payload").ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "Every decision action must contain a known decisionType and object payload.",
                nameof(payload));
        }

        var error = OnlineDecisionActionCodec.ValidatePayload(
            decisionType,
            action.GetProperty("payload"),
            "$.payload.actions[].payload");

        if (error is not null)
        {
            throw new ArgumentException(error.Message, nameof(payload));
        }
    }

    private static void ValidatePayloadForEncoding(DecisionPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload.Actions);
        ArgumentNullException.ThrowIfNull(payload.Certificate);
        ArgumentNullException.ThrowIfNull(payload.Solver);

        if (payload.Solver.ExecutionEvidence is { } executionEvidence)
        {
            var evidenceError = ValidateSolverExecutionEvidence(executionEvidence);

            if (evidenceError is not null)
            {
                throw new ArgumentException(evidenceError.Message, nameof(payload));
            }
        }

        if (!DecisionReasonCodes.All.Contains(
                payload.ReasonCode,
                StringComparer.Ordinal)
            || !OpaqueIdentifier.IsValid(payload.Certificate.ReasonCode))
        {
            throw new ArgumentException(
                "Decision or certificate reason code is invalid.",
                nameof(payload));
        }

        foreach (var action in payload.Actions)
        {
            ValidateActionForEncoding(action, payload);
        }

        if (payload.Certificate.Status == CertificateStatus.Produced)
        {
            if (payload.Certificate.Body is null)
            {
                throw new ArgumentException(
                    "A produced certificate requires a body.",
                    nameof(payload));
            }

            // This validates certificate version, collections, counts and witnesses
            // before any bytes are emitted.
            _ = ProtocolPayloadReader.Write(
                writer => CommitmentContractCodec.WriteCertificateBody(
                    writer,
                    payload.Certificate.Body));
        }
        else if (payload.Certificate.Body is not null)
        {
            throw new ArgumentException(
                "A notProduced certificate cannot contain a body.",
                nameof(payload));
        }

        var semanticError = ValidateDecisionSemantics(
            payload.Status,
            payload.Actions,
            payload.Certificate,
            payload.Solver,
            payload.StateBeforeHash,
            payload.StateAfterHash);

        if (semanticError is not null)
        {
            throw new ArgumentException(semanticError.Message, nameof(payload));
        }
    }

    private static ProtocolPayloadError? ValidateDecisionSemantics(
        DecisionProductionStatus status,
        IReadOnlyList<JsonElement> actions,
        CertificateShell certificate,
        SolverStatusShell solver,
        Sha256Hex stateBeforeHash,
        Sha256Hex stateAfterHash)
    {
        if (status == DecisionProductionStatus.NotProduced
            && (actions.Count != 0
                || certificate.Status != CertificateStatus.NotProduced
                || solver.Status != SolverStatus.NotRun
                || solver.ExecutionEvidence is not null))
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                "$.payload",
                "A notProduced WP1 shell cannot contain actions, a certificate or a solver result.");
        }

        var publicationIds = actions
            .Where(
                action => string.Equals(
                    action.GetProperty("decisionType").GetString(),
                    "promisePublished",
                    StringComparison.Ordinal))
            .Select(
                action => action.GetProperty("payload")
                    .GetProperty("publicationId")
                    .GetString()!)
            .ToArray();

        if (certificate.Status != CertificateStatus.Produced)
        {
            return publicationIds.Length == 0
                ? null
                : new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    "$.payload.certificate",
                    "Promise publication actions require a produced certificate.");
        }

        var body = certificate.Body;

        if (body is null)
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.MissingRequiredField,
                "$.payload.certificate.body",
                "A produced certificate requires a body.");
        }

        if (body.InputStateHash != stateBeforeHash
            || body.ProposedStateHash != stateAfterHash)
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                "$.payload.certificate.body",
                "Certificate state hashes must match the containing decision state hashes.");
        }

        if (publicationIds.Distinct(StringComparer.Ordinal).Count()
                != publicationIds.Length
            || publicationIds.Length != body.PublicationIds.Count
            || !publicationIds.ToHashSet(StringComparer.Ordinal).SetEquals(
                body.PublicationIds))
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                "$.payload.certificate.body.publicationIds",
                "Certificate publication IDs must exactly match unique promisePublished actions.");
        }

        return null;
    }

    private static ProtocolPayloadDecodeResult<CertificateShell> DecodeCertificate(
        JsonElement element)
    {
        const string path = "$.payload.certificate";
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            CertificateFields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<CertificateShell>.Failure(objectError);
        }

        var statusText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "status");
        var reasonCode = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "reasonCode");
        var bodyProperty = element.TryGetProperty("body", out var bodyElement)
            ? ProtocolValueReadResult<JsonElement>.Success(bodyElement)
            : ProtocolValueReadResult<JsonElement>.Success(default);
        var error = HelloPayloadCodec.FirstError(statusText.Error, reasonCode.Error);

        if (error is not null)
        {
            return ProtocolPayloadDecodeResult<CertificateShell>.Failure(error);
        }

        if (!TryParseCertificateStatus(statusText.Value, out var status))
        {
            return ProtocolPayloadDecodeResult<CertificateShell>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    $"{path}.status",
                    "Unknown certificate status."));
        }

        CommitmentCertificateBody? body = null;

        if (status == CertificateStatus.Produced)
        {
            if (bodyProperty.Value.ValueKind != JsonValueKind.Object)
            {
                return ProtocolPayloadDecodeResult<CertificateShell>.Failure(
                    new ProtocolPayloadError(
                        ProtocolPayloadErrorCode.MissingRequiredField,
                        $"{path}.body",
                        "A produced certificate requires a body."));
            }

            var decodedBody = CommitmentContractCodec.ReadCertificateBody(
                bodyProperty.Value,
                $"{path}.body");

            if (!decodedBody.IsSuccess)
            {
                return ProtocolPayloadDecodeResult<CertificateShell>.Failure(
                    decodedBody.Error!);
            }

            body = decodedBody.Value;
        }
        else if (bodyProperty.Value.ValueKind != JsonValueKind.Undefined)
        {
            return ProtocolPayloadDecodeResult<CertificateShell>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    $"{path}.body",
                    "A notProduced certificate cannot contain a body."));
        }

        return ProtocolPayloadDecodeResult<CertificateShell>.Success(
            new CertificateShell(status, reasonCode.Value!, body));
    }

    private static ProtocolPayloadDecodeResult<SolverStatusShell> DecodeSolver(
        JsonElement element)
    {
        const string path = "$.payload.solver";
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            SolverFields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<SolverStatusShell>.Failure(objectError);
        }

        var statusText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "status");

        if (!statusText.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<SolverStatusShell>.Failure(
                statusText.Error!);
        }

        if (!TryParseSolverStatus(statusText.Value, out var status))
        {
            return ProtocolPayloadDecodeResult<SolverStatusShell>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    $"{path}.status",
                    "Unknown solver status."));
        }

        JsonElement? executionEvidence = null;

        if (element.TryGetProperty("executionEvidence", out var evidenceElement))
        {
            var evidenceError = ValidateSolverExecutionEvidence(evidenceElement);

            if (evidenceError is not null)
            {
                return ProtocolPayloadDecodeResult<SolverStatusShell>.Failure(
                    evidenceError);
            }

            executionEvidence = evidenceElement.Clone();
        }

        return ProtocolPayloadDecodeResult<SolverStatusShell>.Success(
            new SolverStatusShell(status, executionEvidence));
    }

    private static ProtocolPayloadError? ValidateSolverExecutionEvidence(
        JsonElement evidence)
    {
        const string path = "$.payload.solver.executionEvidence";
        var objectError = ProtocolPayloadReader.ValidateObject(
            evidence,
            path,
            SolverEvidenceFields);

        if (objectError is not null)
        {
            return objectError;
        }

        var version = ProtocolPayloadReader.ReadRequiredString(
            evidence,
            path,
            "evidenceVersion");
        var generation = ProtocolPayloadReader.ReadRequiredProperty(
            evidence,
            path,
            "generation");
        var pruned = ProtocolPayloadReader.ReadRequiredProperty(
            evidence,
            path,
            "prunedCandidates");
        var selection = ProtocolPayloadReader.ReadRequiredProperty(
            evidence,
            path,
            "selection");
        var error = HelloPayloadCodec.FirstError(
            version.Error,
            generation.Error,
            pruned.Error,
            selection.Error);

        if (error is not null)
        {
            return error;
        }

        var hasCandidatePortfolio = evidence.TryGetProperty(
            "candidatePortfolio",
            out var candidatePortfolio);

        if (version.Value is not ("1.0.0" or "1.1.0" or "1.2.0" or "1.3.0")
            || generation.Value.ValueKind != JsonValueKind.Object
            || pruned.Value.ValueKind != JsonValueKind.Array
            || selection.Value.ValueKind != JsonValueKind.Object)
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                path,
                "Solver execution evidence must use a supported 1.x version and the canonical generation/prune/selection shapes.");
        }

        if (version.Value == "1.2.0"
            && (!hasCandidatePortfolio
                || candidatePortfolio.ValueKind != JsonValueKind.Object))
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                $"{path}.candidatePortfolio",
                "Solver execution evidence v1.2.0 requires candidatePortfolio.");
        }

        if (version.Value is "1.0.0" or "1.1.0" && hasCandidatePortfolio)
        {
            return new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                $"{path}.candidatePortfolio",
                "candidatePortfolio is available only in solver evidence v1.2.0.");
        }

        // v1.3.0 (G2): v1.2.0 with relocation data; the portfolio is optional and validated when present.
        if (version.Value == "1.3.0")
        {
            return hasCandidatePortfolio
                ? ValidateCandidatePortfolio(
                    candidatePortfolio,
                    $"{path}.candidatePortfolio",
                    allowRelocation: true)
                : null;
        }

        return version.Value == "1.2.0"
            ? ValidateCandidatePortfolio(
                candidatePortfolio,
                $"{path}.candidatePortfolio")
            : null;
    }

    private static ProtocolPayloadError? ValidateCandidatePortfolio(
        JsonElement portfolio,
        string path,
        bool allowRelocation = false)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            portfolio,
            path,
            CandidatePortfolioFields);

        if (objectError is not null)
        {
            return objectError;
        }

        var version = ProtocolPayloadReader.ReadRequiredString(
            portfolio,
            path,
            "portfolioVersion");
        var schemaId = ProtocolPayloadReader.ReadRequiredString(
            portfolio,
            path,
            "schemaId",
            requireOpaqueValue: false);
        var objectiveProfile = ProtocolPayloadReader.ReadRequiredString(
            portfolio,
            path,
            "objectiveProfile");
        var generatedCount = ProtocolPayloadReader.ReadRequiredInteger(
            portfolio,
            path,
            "generatedCandidateCount",
            1);
        var eligibleCount = ProtocolPayloadReader.ReadRequiredInteger(
            portfolio,
            path,
            "policyEligibleCandidateCount",
            1);
        var selectedIds = ProtocolPayloadReader.ReadRequiredStringSet(
            portfolio,
            path,
            "selectedCandidateIds",
            allowEmpty: false);
        var problemProperty = ProtocolPayloadReader.ReadRequiredProperty(
            portfolio,
            path,
            "selectionProblem");
        var candidatesProperty = ProtocolPayloadReader.ReadRequiredProperty(
            portfolio,
            path,
            "candidates");
        var error = HelloPayloadCodec.FirstError(
            version.Error,
            schemaId.Error,
            objectiveProfile.Error,
            generatedCount.Error,
            eligibleCount.Error,
            selectedIds.Error,
            problemProperty.Error,
            candidatesProperty.Error);

        if (error is not null)
        {
            return error;
        }

        if (version.Value != "1.0.0"
            || schemaId.Value != CandidatePortfolioSchemaId
            || objectiveProfile.Value is not (
                "rollingCost"
                or "revisionPenalty"
                or "hardVector"
                or "softHardHybrid"))
        {
            return InvalidPortfolioValue(
                path,
                "Candidate portfolio identity or objective profile is unsupported.");
        }

        var problemError = ValidatePortfolioSelectionProblem(
            problemProperty.Value,
            $"{path}.selectionProblem",
            out var problem);

        if (problemError is not null)
        {
            return problemError;
        }

        if (candidatesProperty.Value.ValueKind != JsonValueKind.Array)
        {
            return InvalidPortfolioType(
                $"{path}.candidates",
                "an array");
        }

        var candidates = new List<PortfolioCandidateShape>();
        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        PortfolioCandidateShape? previous = null;
        var index = 0;

        foreach (var candidateElement in candidatesProperty.Value.EnumerateArray())
        {
            var candidateError = ValidatePortfolioCandidate(
                candidateElement,
                $"{path}.candidates[{index}]",
                problem!,
                out var candidate,
                allowRelocation);

            if (candidateError is not null)
            {
                return candidateError;
            }

            if (!candidateIds.Add(candidate!.CandidateId))
            {
                return InvalidPortfolioValue(
                    $"{path}.candidates[{index}].candidateId",
                    "Candidate IDs must be globally unique.");
            }

            if (previous is not null
                && (StringComparer.Ordinal.Compare(
                        previous.VehicleId,
                        candidate.VehicleId) > 0
                    || previous.VehicleId == candidate.VehicleId
                    && StringComparer.Ordinal.Compare(
                        previous.CandidateId,
                        candidate.CandidateId) >= 0))
            {
                return InvalidPortfolioValue(
                    $"{path}.candidates[{index}]",
                    "Candidates must be strictly ordered by vehicleId and candidateId.");
            }

            candidates.Add(candidate);
            previous = candidate;
            index++;
        }

        if (generatedCount.Value != candidates.Count
            || eligibleCount.Value != candidates.Count(value => value.IsEligible))
        {
            return InvalidPortfolioValue(
                path,
                "Candidate portfolio counts do not match the candidate records.");
        }

        var selectedInOrder = portfolio.GetProperty("selectedCandidateIds")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        var candidatesById = candidates.ToDictionary(
            value => value.CandidateId,
            StringComparer.Ordinal);

        if (selectedInOrder.Length != problem!.VehicleIds.Count)
        {
            return InvalidPortfolioValue(
                $"{path}.selectedCandidateIds",
                "Selection must contain exactly one candidate per vehicle.");
        }

        var selectedRequests = new HashSet<string>(StringComparer.Ordinal);

        for (var selectedIndex = 0;
             selectedIndex < selectedInOrder.Length;
             selectedIndex++)
        {
            if (!candidatesById.TryGetValue(
                    selectedInOrder[selectedIndex],
                    out var selectedCandidate)
                || !selectedCandidate.IsEligible
                || selectedCandidate.VehicleId
                    != problem.VehicleIds[selectedIndex]
                || selectedCandidate.RequestIds.Any(
                    value => !selectedRequests.Add(value)))
            {
                return InvalidPortfolioValue(
                    $"{path}.selectedCandidateIds[{selectedIndex}]",
                    "Selected IDs must be eligible, vehicle ordered and request-disjoint.");
            }
        }

        foreach (var vehicleId in problem.VehicleIds)
        {
            var vehicleCandidates = candidates
                .Where(value => value.VehicleId == vehicleId)
                .ToArray();

            if (vehicleCandidates.Length == 0
                || vehicleCandidates.Count(value => value.IsNoOp) != 1
                || vehicleCandidates.Count(
                    value => value.IsNoOp && value.IsEligible) != 1)
            {
                return InvalidPortfolioValue(
                    path,
                    "Every declared vehicle needs candidates and exactly one eligible no-op.");
            }
        }

        return null;
    }

    private static ProtocolPayloadError? ValidatePortfolioSelectionProblem(
        JsonElement problem,
        string path,
        out PortfolioSelectionShape? shape)
    {
        shape = null;
        var objectError = ProtocolPayloadReader.ValidateObject(
            problem,
            path,
            PortfolioProblemFields);

        if (objectError is not null)
        {
            return objectError;
        }

        var vehicleIds = ReadCanonicalStringSet(
            problem,
            path,
            "vehicleIds",
            allowEmpty: false);
        var requestIds = ReadCanonicalStringSet(
            problem,
            path,
            "requestIds",
            allowEmpty: true);
        var objectives = ProtocolPayloadReader.ReadRequiredProperty(
            problem,
            path,
            "objectiveLevels");
        var error = HelloPayloadCodec.FirstError(
            vehicleIds.Error,
            requestIds.Error,
            objectives.Error);

        if (error is not null)
        {
            return error;
        }

        if (objectives.Value.ValueKind != JsonValueKind.Array
            || objectives.Value.GetArrayLength() == 0)
        {
            return InvalidPortfolioType(
                $"{path}.objectiveLevels",
                "a non-empty array");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var objective in objectives.Value.EnumerateArray())
        {
            var objectivePath = $"{path}.objectiveLevels[{index}]";
            var objectiveError = ProtocolPayloadReader.ValidateObject(
                objective,
                objectivePath,
                PortfolioObjectiveFields);

            if (objectiveError is not null)
            {
                return objectiveError;
            }

            var levelIndex = ProtocolPayloadReader.ReadRequiredInteger(
                objective,
                objectivePath,
                "levelIndex",
                0);
            var name = ProtocolPayloadReader.ReadRequiredString(
                objective,
                objectivePath,
                "name");
            var sense = ProtocolPayloadReader.ReadRequiredString(
                objective,
                objectivePath,
                "sense");
            var aggregation = ProtocolPayloadReader.ReadRequiredString(
                objective,
                objectivePath,
                "aggregation");
            error = HelloPayloadCodec.FirstError(
                levelIndex.Error,
                name.Error,
                sense.Error,
                aggregation.Error);

            if (error is not null)
            {
                return error;
            }

            if (levelIndex.Value != index
                || !names.Add(name.Value!)
                || sense.Value is not ("minimize" or "maximize")
                || aggregation.Value is not ("sum" or "maximum"))
            {
                return InvalidPortfolioValue(
                    objectivePath,
                    "Objective levels must have contiguous indices, unique names and known semantics.");
            }

            index++;
        }

        shape = new PortfolioSelectionShape(
            Array.AsReadOnly(vehicleIds.Value!),
            requestIds.Value!.ToHashSet(StringComparer.Ordinal),
            index);
        return null;
    }

    private static ProtocolPayloadError? ValidatePortfolioCandidate(
        JsonElement candidate,
        string path,
        PortfolioSelectionShape problem,
        out PortfolioCandidateShape? shape,
        bool allowRelocation = false)
    {
        shape = null;
        var objectError = ProtocolPayloadReader.ValidateObject(
            candidate,
            path,
            allowRelocation ? RelocationPortfolioCandidateFields : PortfolioCandidateFields);

        if (objectError is not null)
        {
            return objectError;
        }

        if (allowRelocation)
        {
            var relocated = ProtocolPayloadReader.ReadOptionalString(candidate, path, "relocatedRequestId");
            var role = ProtocolPayloadReader.ReadOptionalString(candidate, path, "relocationRole");
            var relocationError = HelloPayloadCodec.FirstError(relocated.Error, role.Error);

            if (relocationError is not null)
            {
                return relocationError;
            }

            if ((relocated.Value is null) != (role.Value is null)
                || role.Value is not (null or "donor" or "receiver"))
            {
                return InvalidPortfolioValue(
                    path,
                    "A relocation candidate names both the moved rider and a donor or receiver role.");
            }
        }

        var candidateId = ProtocolPayloadReader.ReadRequiredString(
            candidate,
            path,
            "candidateId");
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            candidate,
            path,
            "vehicleId");
        var requestIds = ReadCanonicalStringSet(
            candidate,
            path,
            "newRequestIds",
            allowEmpty: true);
        var isNoOp = ReadRequiredBoolean(candidate, path, "isNoOp");
        var strategy = ProtocolPayloadReader.ReadRequiredString(
            candidate,
            path,
            "scheduleStrategy");
        var relocatedWait = ProtocolPayloadReader.ReadRequiredInteger(
            candidate,
            path,
            "relocatedWaitMs",
            0);
        var slack = ReadOptionalCanonicalInteger(
            candidate,
            path,
            "certifiedForwardSlackMs");
        var repaired = ProtocolPayloadReader.ReadOptionalString(
            candidate,
            path,
            "repairedIncumbentRequestId");
        var route = ProtocolPayloadReader.ReadRequiredProperty(
            candidate,
            path,
            "route");
        var schedule = ProtocolPayloadReader.ReadRequiredProperty(
            candidate,
            path,
            "schedule");
        var eligibility = ProtocolPayloadReader.ReadRequiredString(
            candidate,
            path,
            "policyEligibility");
        var error = HelloPayloadCodec.FirstError(
            candidateId.Error,
            vehicleId.Error,
            requestIds.Error,
            isNoOp.Error,
            strategy.Error,
            relocatedWait.Error,
            slack.Error,
            repaired.Error,
            route.Error,
            schedule.Error,
            eligibility.Error);

        if (error is not null)
        {
            return error;
        }

        if (!problem.VehicleIds.Contains(vehicleId.Value!)
            || eligibility.Value == "eligible"
            && requestIds.Value!.Any(
                value => !problem.RequestIds.Contains(value))
            || isNoOp.Value && requestIds.Value!.Length != 0
            || strategy.Value is not (
                "earliestFeasible" or "originHoldRelocatedWait")
            || eligibility.Value is not ("eligible" or "pruned"))
        {
            return InvalidPortfolioValue(
                path,
                "Candidate identity, request membership or enum value is invalid.");
        }

        var routeError = ValidatePortfolioRoute(
            route.Value,
            $"{path}.route",
            out var remainingStopIds);

        if (routeError is not null)
        {
            return routeError;
        }

        var scheduleError = ValidatePortfolioSchedule(
            schedule.Value,
            $"{path}.schedule",
            remainingStopIds!);

        if (scheduleError is not null)
        {
            return scheduleError;
        }

        var hasContributions = candidate.TryGetProperty(
            "objectiveContributions",
            out var contributions);
        var eligible = eligibility.Value == "eligible";

        if (eligible != hasContributions)
        {
            return InvalidPortfolioValue(
                $"{path}.objectiveContributions",
                "Eligible candidates require exact objectives; pruned candidates prohibit them.");
        }

        if (hasContributions)
        {
            if (contributions.ValueKind != JsonValueKind.Array
                || contributions.GetArrayLength() != problem.ObjectiveCount)
            {
                return InvalidPortfolioValue(
                    $"{path}.objectiveContributions",
                    "Objective contribution count must match objectiveLevels.");
            }

            var contributionIndex = 0;

            foreach (var contribution in contributions.EnumerateArray())
            {
                var contributionError = ValidateCanonicalInteger(
                    contribution,
                    $"{path}.objectiveContributions[{contributionIndex}]");

                if (contributionError is not null)
                {
                    return contributionError;
                }

                contributionIndex++;
            }
        }

        shape = new PortfolioCandidateShape(
            candidateId.Value!,
            vehicleId.Value!,
            isNoOp.Value,
            eligible,
            Array.AsReadOnly(requestIds.Value!));
        return null;
    }

    private static ProtocolPayloadError? ValidatePortfolioRoute(
        JsonElement route,
        string path,
        out IReadOnlyList<string>? remainingStopIds)
    {
        remainingStopIds = null;
        var objectError = ProtocolPayloadReader.ValidateObject(
            route,
            path,
            PortfolioRouteFields);

        if (objectError is not null)
        {
            return objectError;
        }

        var version = ProtocolPayloadReader.ReadRequiredInteger(
            route,
            path,
            "planVersion",
            0);
        var executed = ProtocolPayloadReader.ReadRequiredInteger(
            route,
            path,
            "executedStopCount",
            0);
        var frozen = ProtocolPayloadReader.ReadRequiredProperty(
            route,
            path,
            "frozenPrefix");
        var mutable = ProtocolPayloadReader.ReadRequiredProperty(
            route,
            path,
            "mutableSuffix");
        var error = HelloPayloadCodec.FirstError(
            version.Error,
            executed.Error,
            frozen.Error,
            mutable.Error);

        if (error is not null)
        {
            return error;
        }

        var stopIds = new HashSet<string>(StringComparer.Ordinal);
        var frozenError = ValidatePortfolioRouteStops(
            frozen.Value,
            $"{path}.frozenPrefix",
            stopIds,
            out var frozenIds);

        if (frozenError is not null)
        {
            return frozenError;
        }

        var mutableError = ValidatePortfolioRouteStops(
            mutable.Value,
            $"{path}.mutableSuffix",
            stopIds,
            out var mutableIds);

        if (mutableError is not null)
        {
            return mutableError;
        }

        if (executed.Value > frozenIds!.Count)
        {
            return InvalidPortfolioValue(
                $"{path}.executedStopCount",
                "Executed stop count must be within the frozen prefix.");
        }

        remainingStopIds = frozenIds
            .Skip((int)executed.Value)
            .Concat(mutableIds!)
            .ToArray();
        return null;
    }

    private static ProtocolPayloadError? ValidatePortfolioRouteStops(
        JsonElement stops,
        string path,
        ISet<string> allStopIds,
        out IReadOnlyList<string>? stopIds)
    {
        stopIds = null;

        if (stops.ValueKind != JsonValueKind.Array)
        {
            return InvalidPortfolioType(path, "an array");
        }

        var values = new List<string>();
        var index = 0;

        foreach (var stop in stops.EnumerateArray())
        {
            var stopPath = $"{path}[{index}]";
            var objectError = ProtocolPayloadReader.ValidateObject(
                stop,
                stopPath,
                PortfolioRouteStopFields);

            if (objectError is not null)
            {
                return objectError;
            }

            var stopId = ProtocolPayloadReader.ReadRequiredString(
                stop,
                stopPath,
                "stopId");
            var nodeId = ProtocolPayloadReader.ReadRequiredString(
                stop,
                stopPath,
                "nodeId");
            var kind = ProtocolPayloadReader.ReadRequiredString(
                stop,
                stopPath,
                "kind");
            var requestId = ProtocolPayloadReader.ReadOptionalString(
                stop,
                stopPath,
                "requestId");
            var duration = ProtocolPayloadReader.ReadRequiredInteger(
                stop,
                stopPath,
                "serviceDurationMs",
                0);
            var error = HelloPayloadCodec.FirstError(
                stopId.Error,
                nodeId.Error,
                kind.Error,
                requestId.Error,
                duration.Error);

            if (error is not null)
            {
                return error;
            }

            if (!allStopIds.Add(stopId.Value!)
                || kind.Value is not ("waypoint" or "pickup" or "dropOff")
                || kind.Value == "waypoint" && requestId.Value is not null
                || kind.Value != "waypoint" && requestId.Value is null)
            {
                return InvalidPortfolioValue(
                    stopPath,
                    "Route stop identity, kind or request binding is invalid.");
            }

            values.Add(stopId.Value!);
            index++;
        }

        stopIds = values;
        return null;
    }

    private static ProtocolPayloadError? ValidatePortfolioSchedule(
        JsonElement schedule,
        string path,
        IReadOnlyList<string> expectedStopIds)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            schedule,
            path,
            PortfolioScheduleFields);

        if (objectError is not null)
        {
            return objectError;
        }

        var cost = ProtocolPayloadReader.ReadRequiredInteger(
            schedule,
            path,
            "operationalCost",
            0);
        var stops = ProtocolPayloadReader.ReadRequiredProperty(
            schedule,
            path,
            "stops");
        var error = HelloPayloadCodec.FirstError(cost.Error, stops.Error);

        if (error is not null)
        {
            return error;
        }

        if (stops.Value.ValueKind != JsonValueKind.Array)
        {
            return InvalidPortfolioType($"{path}.stops", "an array");
        }

        var actualStopIds = new List<string>();
        long? previousDeparture = null;
        var index = 0;

        foreach (var stop in stops.Value.EnumerateArray())
        {
            var stopPath = $"{path}.stops[{index}]";
            var stopError = ProtocolPayloadReader.ValidateObject(
                stop,
                stopPath,
                PortfolioScheduledStopFields);

            if (stopError is not null)
            {
                return stopError;
            }

            var stopId = ProtocolPayloadReader.ReadRequiredString(
                stop,
                stopPath,
                "stopId");
            var arrival = ProtocolPayloadReader.ReadRequiredInteger(
                stop,
                stopPath,
                "arrivalTimeMs",
                0);
            var service = ProtocolPayloadReader.ReadRequiredInteger(
                stop,
                stopPath,
                "serviceStartTimeMs",
                0);
            var departure = ProtocolPayloadReader.ReadRequiredInteger(
                stop,
                stopPath,
                "departureTimeMs",
                0);
            error = HelloPayloadCodec.FirstError(
                stopId.Error,
                arrival.Error,
                service.Error,
                departure.Error);

            if (error is not null)
            {
                return error;
            }

            if (arrival.Value > service.Value
                || service.Value > departure.Value
                || previousDeparture > arrival.Value)
            {
                return InvalidPortfolioValue(
                    stopPath,
                    "Scheduled stop times must be ordered and non-overlapping.");
            }

            actualStopIds.Add(stopId.Value!);
            previousDeparture = departure.Value;
            index++;
        }

        return actualStopIds.SequenceEqual(expectedStopIds, StringComparer.Ordinal)
            ? null
            : InvalidPortfolioValue(
                $"{path}.stops",
                "Schedule stop IDs must exactly match the route remaining stops.");
    }

    private static ProtocolValueReadResult<string[]> ReadCanonicalStringSet(
        JsonElement element,
        string path,
        string field,
        bool allowEmpty)
    {
        var values = ProtocolPayloadReader.ReadRequiredStringSet(
            element,
            path,
            field,
            allowEmpty);

        if (!values.IsSuccess)
        {
            return values;
        }

        var raw = element.GetProperty(field)
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();

        return raw.SequenceEqual(values.Value!, StringComparer.Ordinal)
            ? values
            : ProtocolValueReadResult<string[]>.Failure(
                InvalidPortfolioValue(
                    $"{path}.{field}",
                    "Set values must use ordinal canonical order."));
    }

    private static ProtocolValueReadResult<bool> ReadRequiredBoolean(
        JsonElement element,
        string path,
        string field)
    {
        var property = ProtocolPayloadReader.ReadRequiredProperty(
            element,
            path,
            field);

        if (!property.IsSuccess)
        {
            return ProtocolValueReadResult<bool>.Failure(property.Error!);
        }

        return property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? ProtocolValueReadResult<bool>.Success(property.Value.GetBoolean())
            : ProtocolValueReadResult<bool>.Failure(
                InvalidPortfolioType($"{path}.{field}", "a boolean"));
    }

    private static ProtocolValueReadResult<long?> ReadOptionalCanonicalInteger(
        JsonElement element,
        string path,
        string field)
    {
        if (!element.TryGetProperty(field, out var property))
        {
            return ProtocolValueReadResult<long?>.Success(null);
        }

        var error = ValidateCanonicalInteger(property, $"{path}.{field}");
        return error is null
            ? ProtocolValueReadResult<long?>.Success(property.GetInt64())
            : ProtocolValueReadResult<long?>.Failure(error);
    }

    private static ProtocolPayloadError? ValidateCanonicalInteger(
        JsonElement element,
        string path)
    {
        var raw = element.GetRawText();

        return element.ValueKind == JsonValueKind.Number
            && element.TryGetInt64(out var value)
            && raw.AsSpan().IndexOfAny('.', 'e', 'E') < 0
            && raw != "-0"
            && value is >= 0 and <= ProtocolLimits.MaxCanonicalInteger
                ? null
                : new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.ValueOutOfRange,
                    path,
                    $"Field '{path}' is outside its canonical integer range.");
    }

    private static ProtocolPayloadError InvalidPortfolioValue(
        string path,
        string message) =>
        new(ProtocolPayloadErrorCode.InvalidValue, path, message);

    private static ProtocolPayloadError InvalidPortfolioType(
        string path,
        string expected) =>
        new(
            ProtocolPayloadErrorCode.InvalidFieldType,
            path,
            $"Field '{path}' must be {expected}.");

    private sealed record PortfolioSelectionShape(
        IReadOnlyList<string> VehicleIds,
        IReadOnlySet<string> RequestIds,
        int ObjectiveCount);

    private sealed record PortfolioCandidateShape(
        string CandidateId,
        string VehicleId,
        bool IsNoOp,
        bool IsEligible,
        IReadOnlyList<string> RequestIds);

    private static bool TryReadHash(string? value, out Sha256Hex? hash) =>
        Sha256Hex.TryCreate(value, out hash);

    private static bool TryParseDecisionStatus(
        string? value,
        out DecisionProductionStatus status)
    {
        status = value switch
        {
            "notProduced" => DecisionProductionStatus.NotProduced,
            "produced" => DecisionProductionStatus.Produced,
            _ => default,
        };

        return value is "notProduced" or "produced";
    }

    private static bool TryParseCertificateStatus(
        string? value,
        out CertificateStatus status)
    {
        status = value switch
        {
            "notProduced" => CertificateStatus.NotProduced,
            "produced" => CertificateStatus.Produced,
            _ => default,
        };

        return value is "notProduced" or "produced";
    }

    private static bool TryParseSolverStatus(
        string? value,
        out SolverStatus status)
    {
        status = value switch
        {
            "notRun" => SolverStatus.NotRun,
            "completed" => SolverStatus.Completed,
            "safeFallback" => SolverStatus.SafeFallback,
            _ => default,
        };

        return value is "notRun" or "completed" or "safeFallback";
    }

    private static string ToProtocolValue(DecisionProductionStatus status) =>
        status switch
        {
            DecisionProductionStatus.NotProduced => "notProduced",
            DecisionProductionStatus.Produced => "produced",
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static string ToProtocolValue(CertificateStatus status) =>
        status switch
        {
            CertificateStatus.NotProduced => "notProduced",
            CertificateStatus.Produced => "produced",
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static string ToProtocolValue(SolverStatus status) =>
        status switch
        {
            SolverStatus.NotRun => "notRun",
            SolverStatus.Completed => "completed",
            SolverStatus.SafeFallback => "safeFallback",
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    private static ProtocolPayloadDecodeResult<DecisionPayload> Invalid(
        string field,
        string message) =>
        ProtocolPayloadDecodeResult<DecisionPayload>.Failure(
            new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                field,
                message));

    private static ProtocolPayloadDecodeResult<DecisionPayload> WrongType(
        string field,
        string expected) =>
        ProtocolPayloadDecodeResult<DecisionPayload>.Failure(
            new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidFieldType,
                field,
                $"Field '{field}' must be {expected}."));
}
