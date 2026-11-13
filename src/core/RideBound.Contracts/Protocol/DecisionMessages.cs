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

        // The Runner no longer writes solver execution evidence; a decision that has it keeps it as opaque JSON.
        JsonElement? executionEvidence = element.TryGetProperty("executionEvidence", out var evidenceElement)
            ? evidenceElement.Clone()
            : null;

        return ProtocolPayloadDecodeResult<SolverStatusShell>.Success(
            new SolverStatusShell(status, executionEvidence));
    }

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
