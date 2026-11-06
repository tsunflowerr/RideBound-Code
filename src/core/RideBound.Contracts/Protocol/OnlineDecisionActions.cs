using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public abstract record OnlineDecisionActionPayload;

public sealed record RequestAcceptedActionPayload(
    string RequestId,
    string VehicleId,
    string CandidateId) : OnlineDecisionActionPayload;

public sealed record RequestOutcomeActionPayload(
    string RequestId,
    string ReasonCode) : OnlineDecisionActionPayload;

public sealed record VehiclePlanUpdatedActionPayload(
    string VehicleId,
    string CandidateId,
    RoutePlanContract Route) : OnlineDecisionActionPayload;

public sealed record OnlineDecisionAction(
    DecisionType DecisionType,
    OnlineDecisionActionPayload Payload);

public static class OnlineDecisionActionCodec
{
    private static readonly IReadOnlySet<string> AcceptedFields =
        Fields("requestId", "vehicleId", "candidateId");

    private static readonly IReadOnlySet<string> RequestOutcomeFields =
        Fields("requestId", "reasonCode");

    private static readonly IReadOnlySet<string> VehiclePlanFields =
        Fields("vehicleId", "candidateId", "route");

    private static readonly IReadOnlySet<string> PromisePublishedFields =
        Fields(
            "publicationId",
            "promiseVersion",
            "reasonCode",
            "sourceEventSeq",
            "promise",
            "exogenousDelta",
            "decisionDelta",
            "visibleDelta",
            "budgetBefore",
            "budgetAfter");

    private static readonly IReadOnlySet<string> BreachFields =
        Fields(
            "breachId",
            "incidentId",
            "requestId",
            "sourceEventSeq",
            "witnessCodes",
            "budgetBefore",
            "attemptedBudgetAfter");

    public static JsonElement Encode(OnlineDecisionAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var bytes = ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString(
                    "decisionType",
                    DecisionTypeVocabulary.ToProtocolValue(
                        action.DecisionType));
                writer.WritePropertyName("payload");
                WritePayload(writer, action);
                writer.WriteEndObject();
            });
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    internal static ProtocolPayloadError? ValidatePayload(
        DecisionType decisionType,
        JsonElement payload,
        string path)
    {
        return decisionType switch
        {
            DecisionType.RequestAccepted =>
                ValidateAccepted(payload, path),
            DecisionType.RequestRejected or DecisionType.RequestDeferred =>
                ValidateRequestOutcome(payload, path),
            DecisionType.VehiclePlanUpdated =>
                ValidateVehiclePlan(payload, path),
            DecisionType.PromisePublished =>
                ValidatePromisePublished(payload, path),
            DecisionType.CommitmentBreachDeclared =>
                ValidateBreach(payload, path),
            DecisionType.OfferProposed =>
                ProtocolPayloadReader.ValidateObject(
                    payload,
                    path,
                    Fields()),
            _ => new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                path,
                "Decision action type is unsupported."),
        };
    }

    private static void WritePayload(
        Utf8JsonWriter writer,
        OnlineDecisionAction action)
    {
        switch (action.DecisionType, action.Payload)
        {
            case (
                DecisionType.RequestAccepted,
                RequestAcceptedActionPayload accepted):
                RequireIdentifier(accepted.RequestId, nameof(accepted.RequestId));
                RequireIdentifier(accepted.VehicleId, nameof(accepted.VehicleId));
                RequireIdentifier(
                    accepted.CandidateId,
                    nameof(accepted.CandidateId));
                writer.WriteStartObject();
                writer.WriteString("requestId", accepted.RequestId);
                writer.WriteString("vehicleId", accepted.VehicleId);
                writer.WriteString("candidateId", accepted.CandidateId);
                writer.WriteEndObject();
                break;
            case (
                DecisionType.RequestRejected or DecisionType.RequestDeferred,
                RequestOutcomeActionPayload outcome):
                RequireIdentifier(outcome.RequestId, nameof(outcome.RequestId));
                RequireIdentifier(outcome.ReasonCode, nameof(outcome.ReasonCode));
                writer.WriteStartObject();
                writer.WriteString("requestId", outcome.RequestId);
                writer.WriteString("reasonCode", outcome.ReasonCode);
                writer.WriteEndObject();
                break;
            case (
                DecisionType.VehiclePlanUpdated,
                VehiclePlanUpdatedActionPayload plan):
                RequireIdentifier(plan.VehicleId, nameof(plan.VehicleId));
                RequireIdentifier(plan.CandidateId, nameof(plan.CandidateId));
                writer.WriteStartObject();
                writer.WriteString("vehicleId", plan.VehicleId);
                writer.WriteString("candidateId", plan.CandidateId);
                writer.WritePropertyName("route");
                OnlineContractCodec.WriteRoutePlan(writer, plan.Route);
                writer.WriteEndObject();
                break;
            case (
                DecisionType.PromisePublished,
                PromisePublishedActionPayload promise):
                RequireIdentifier(promise.PublicationId, nameof(promise.PublicationId));
                RequireIdentifier(promise.ReasonCode, nameof(promise.ReasonCode));
                RequirePositive(promise.PromiseVersion, nameof(promise.PromiseVersion));
                RequirePositive(promise.SourceEventSequence, nameof(promise.SourceEventSequence));
                writer.WriteStartObject();
                writer.WriteString("publicationId", promise.PublicationId);
                writer.WriteNumber("promiseVersion", promise.PromiseVersion);
                writer.WriteString("reasonCode", promise.ReasonCode);
                writer.WriteNumber("sourceEventSeq", promise.SourceEventSequence);
                writer.WritePropertyName("promise");
                CommitmentContractCodec.WritePromise(writer, promise.Promise);
                WriteVector(writer, "exogenousDelta", promise.ExogenousDelta);
                WriteVector(writer, "decisionDelta", promise.DecisionDelta);
                WriteVector(writer, "visibleDelta", promise.VisibleDelta);
                WriteVector(writer, "budgetBefore", promise.BudgetBefore);
                WriteVector(writer, "budgetAfter", promise.BudgetAfter);
                writer.WriteEndObject();
                break;
            case (
                DecisionType.CommitmentBreachDeclared,
                CommitmentBreachDeclaredActionPayload breach):
                RequireIdentifier(breach.BreachId, nameof(breach.BreachId));
                RequireIdentifier(breach.IncidentId, nameof(breach.IncidentId));
                RequireIdentifier(breach.RequestId, nameof(breach.RequestId));
                RequirePositive(breach.SourceEventSequence, nameof(breach.SourceEventSequence));

                if (breach.WitnessCodes.Count == 0
                    || breach.WitnessCodes.Distinct(StringComparer.Ordinal).Count()
                        != breach.WitnessCodes.Count)
                {
                    throw new ArgumentException(
                        "A commitment breach requires unique witness codes.",
                        nameof(action));
                }

                writer.WriteStartObject();
                writer.WriteString("breachId", breach.BreachId);
                writer.WriteString("incidentId", breach.IncidentId);
                writer.WriteString("requestId", breach.RequestId);
                writer.WriteNumber("sourceEventSeq", breach.SourceEventSequence);
                writer.WritePropertyName("witnessCodes");
                writer.WriteStartArray();

                foreach (var value in breach.WitnessCodes.Order(StringComparer.Ordinal))
                {
                    RequireIdentifier(value, nameof(breach.WitnessCodes));
                    writer.WriteStringValue(value);
                }

                writer.WriteEndArray();
                WriteVector(writer, "budgetBefore", breach.BudgetBefore);
                WriteVector(
                    writer,
                    "attemptedBudgetAfter",
                    breach.AttemptedBudgetAfter);
                writer.WriteEndObject();
                break;
            default:
                throw new ArgumentException(
                    $"Payload '{action.Payload.GetType().Name}' does not match " +
                    $"online decision type '{action.DecisionType}'.",
                    nameof(action));
        }
    }

    private static ProtocolPayloadError? ValidateAccepted(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            AcceptedFields);
        var requestId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "requestId");
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "vehicleId");
        var candidateId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "candidateId");
        return HelloPayloadCodec.FirstError(
            objectError,
            requestId.Error,
            vehicleId.Error,
            candidateId.Error);
    }

    private static ProtocolPayloadError? ValidateRequestOutcome(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            RequestOutcomeFields);
        var requestId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "requestId");
        var reasonCode = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "reasonCode");
        return HelloPayloadCodec.FirstError(
            objectError,
            requestId.Error,
            reasonCode.Error);
    }

    private static ProtocolPayloadError? ValidateVehiclePlan(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            VehiclePlanFields);
        var vehicleId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "vehicleId");
        var candidateId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "candidateId");
        var routeElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            path,
            "route");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            vehicleId.Error,
            candidateId.Error,
            routeElement.Error);

        if (error is not null)
        {
            return error;
        }

        var route = OnlineContractCodec.ReadRoutePlan(
            routeElement.Value,
            $"{path}.route");
        return route.Error;
    }

    private static ProtocolPayloadError? ValidatePromisePublished(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            PromisePublishedFields);
        var publication = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "publicationId");
        var version = ProtocolPayloadReader.ReadRequiredInteger(
            payload,
            path,
            "promiseVersion",
            minimum: 1);
        var reason = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "reasonCode");
        var sequence = ProtocolPayloadReader.ReadRequiredInteger(
            payload,
            path,
            "sourceEventSeq",
            minimum: 1);
        var promise = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            path,
            "promise");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            publication.Error,
            version.Error,
            reason.Error,
            sequence.Error,
            promise.Error);

        if (error is not null)
        {
            return error;
        }

        error = CommitmentContractCodec.ValidatePromise(
            promise.Value,
            $"{path}.promise");

        if (error is not null)
        {
            return error;
        }

        foreach (var field in new[]
                 {
                     "exogenousDelta",
                     "decisionDelta",
                     "visibleDelta",
                     "budgetBefore",
                     "budgetAfter",
                 })
        {
            var vector = ProtocolPayloadReader.ReadRequiredProperty(
                payload,
                path,
                field);

            if (!vector.IsSuccess)
            {
                return vector.Error;
            }

            error = CommitmentContractCodec.ValidateVector(
                vector.Value,
                $"{path}.{field}");

            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static ProtocolPayloadError? ValidateBreach(
        JsonElement payload,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            payload,
            path,
            BreachFields);
        var breach = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "breachId");
        var incident = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "incidentId");
        var request = ProtocolPayloadReader.ReadRequiredString(
            payload,
            path,
            "requestId");
        var sequence = ProtocolPayloadReader.ReadRequiredInteger(
            payload,
            path,
            "sourceEventSeq",
            minimum: 1);
        var witnesses = ProtocolPayloadReader.ReadRequiredStringSet(
            payload,
            path,
            "witnessCodes",
            allowEmpty: false);
        var error = HelloPayloadCodec.FirstError(
            objectError,
            breach.Error,
            incident.Error,
            request.Error,
            sequence.Error,
            witnesses.Error);

        if (error is not null)
        {
            return error;
        }

        foreach (var field in new[] { "budgetBefore", "attemptedBudgetAfter" })
        {
            var vector = ProtocolPayloadReader.ReadRequiredProperty(
                payload,
                path,
                field);

            if (!vector.IsSuccess)
            {
                return vector.Error;
            }

            error = CommitmentContractCodec.ValidateVector(
                vector.Value,
                $"{path}.{field}");

            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static void WriteVector(
        Utf8JsonWriter writer,
        string propertyName,
        CommitmentVectorContract value)
    {
        writer.WritePropertyName(propertyName);
        CommitmentContractCodec.WriteVector(writer, value);
    }

    private static void RequirePositive(long value, string parameterName)
    {
        if (value is < 1 or > ProtocolLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static IReadOnlySet<string> Fields(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static void RequireIdentifier(string value, string parameterName)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            throw new ArgumentException(
                "Identifier must contain 1 to 128 valid UTF-8 bytes.",
                parameterName);
        }
    }
}
