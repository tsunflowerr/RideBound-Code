using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public sealed record CommitmentVectorContract(
    long PickupEtaTotalMs,
    long DropEtaTotalMs,
    long MaterialEtaRevisionCount,
    long VehicleSwitchCount,
    long PickupStopRelocationMm,
    long PickupStopSwitchCount,
    long DropStopRelocationMm,
    long DropStopSwitchCount,
    long IncumbentOrderInversionCount,
    long PrePickupInsertedStopCount);

public sealed record PromiseServiceTokenContract(
    string StopId,
    string? RequestId,
    RouteStopKind Kind);

public sealed record PromiseProjectionContract(
    string RequestId,
    string VehicleId,
    string PickupStopId,
    string PickupNodeId,
    string DropStopId,
    string DropNodeId,
    long PickupEtaMs,
    long DropEtaMs,
    IReadOnlyList<PromiseServiceTokenContract> ServiceOrder);

public sealed record PromisePublishedActionPayload(
    string PublicationId,
    long PromiseVersion,
    string ReasonCode,
    long SourceEventSequence,
    PromiseProjectionContract Promise,
    CommitmentVectorContract ExogenousDelta,
    CommitmentVectorContract DecisionDelta,
    CommitmentVectorContract VisibleDelta,
    CommitmentVectorContract BudgetBefore,
    CommitmentVectorContract BudgetAfter) : OnlineDecisionActionPayload;

public sealed record CommitmentBreachDeclaredActionPayload(
    string BreachId,
    string IncidentId,
    string RequestId,
    long SourceEventSequence,
    IReadOnlyList<string> WitnessCodes,
    CommitmentVectorContract BudgetBefore,
    CommitmentVectorContract AttemptedBudgetAfter)
    : OnlineDecisionActionPayload;

public sealed record CertificateWitnessContract(
    string Stage,
    string Code,
    string? VehicleId = null,
    string? RequestId = null,
    string? Dimension = null,
    string? Rule = null,
    long? Limit = null,
    long? Before = null,
    long? Delta = null,
    long? After = null);

public sealed record CommitmentCertificateBody(
    string CertificateVersion,
    string ValidatorVersion,
    bool NormalOperation,
    Sha256Hex InputStateHash,
    Sha256Hex ProposedStateHash,
    IReadOnlyList<string> PublicationIds,
    long PhysicalPlanCount,
    long PromiseCount,
    IReadOnlyList<CertificateWitnessContract> Witnesses);

internal static class CommitmentContractCodec
{
    public const string CurrentCertificateVersion = "1.0.0";

    private static readonly IReadOnlySet<string> CertificateFields = Fields(
        "certificateVersion",
        "validatorVersion",
        "normalOperation",
        "inputStateHash",
        "proposedStateHash",
        "publicationIds",
        "physicalPlanCount",
        "promiseCount",
        "witnesses");

    private static readonly IReadOnlySet<string> WitnessFields = Fields(
        "stage",
        "code",
        "vehicleId",
        "requestId",
        "dimension",
        "rule",
        "limit",
        "before",
        "delta",
        "after");

    private static readonly IReadOnlySet<string> VectorFields = Fields(
        "pickupEtaTotalMs",
        "dropEtaTotalMs",
        "materialEtaRevisionCount",
        "vehicleSwitchCount",
        "pickupStopRelocationMm",
        "pickupStopSwitchCount",
        "dropStopRelocationMm",
        "dropStopSwitchCount",
        "incumbentOrderInversionCount",
        "prePickupInsertedStopCount");

    private static readonly IReadOnlySet<string> PromiseFields = Fields(
        "requestId",
        "vehicleId",
        "pickupStopId",
        "pickupNodeId",
        "dropStopId",
        "dropNodeId",
        "pickupEtaMs",
        "dropEtaMs",
        "serviceOrder");

    private static readonly IReadOnlySet<string> TokenFields =
        Fields("stopId", "requestId", "kind");

    public static void WriteCertificateBody(
        Utf8JsonWriter writer,
        CommitmentCertificateBody body)
    {
        ValidateCertificateForEncoding(body);
        writer.WriteStartObject();
        writer.WriteString("certificateVersion", body.CertificateVersion);
        writer.WriteString("validatorVersion", body.ValidatorVersion);
        writer.WriteBoolean("normalOperation", body.NormalOperation);
        writer.WriteString("inputStateHash", body.InputStateHash.Value);
        writer.WriteString("proposedStateHash", body.ProposedStateHash.Value);
        writer.WritePropertyName("publicationIds");
        writer.WriteStartArray();

        foreach (var value in body.PublicationIds)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
        writer.WriteNumber("physicalPlanCount", body.PhysicalPlanCount);
        writer.WriteNumber("promiseCount", body.PromiseCount);
        writer.WritePropertyName("witnesses");
        writer.WriteStartArray();

        foreach (var witness in body.Witnesses)
        {
            WriteWitness(writer, witness);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public static ProtocolValueReadResult<CommitmentCertificateBody>
        ReadCertificateBody(JsonElement element, string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            CertificateFields);
        var version = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "certificateVersion",
            requireOpaqueValue: false);
        var validator = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "validatorVersion");
        var normal = ReadRequiredBoolean(element, path, "normalOperation");
        var inputHash = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "inputStateHash",
            requireOpaqueValue: false);
        var proposedHash = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "proposedStateHash",
            requireOpaqueValue: false);
        var publications = ProtocolPayloadReader.ReadRequiredStringSet(
            element,
            path,
            "publicationIds",
            allowEmpty: true);
        var physicalCount = ProtocolPayloadReader.ReadRequiredInteger(
            element,
            path,
            "physicalPlanCount",
            minimum: 0);
        var promiseCount = ProtocolPayloadReader.ReadRequiredInteger(
            element,
            path,
            "promiseCount",
            minimum: 0);
        var witnessesProperty = ProtocolPayloadReader.ReadRequiredProperty(
            element,
            path,
            "witnesses");
        var error = HelloPayloadCodec.FirstError(
            objectError,
            version.Error,
            validator.Error,
            normal.Error,
            inputHash.Error,
            proposedHash.Error,
            publications.Error,
            physicalCount.Error,
            promiseCount.Error,
            witnessesProperty.Error);

        if (error is not null)
        {
            return ProtocolValueReadResult<CommitmentCertificateBody>.Failure(error);
        }

        if (!string.Equals(
                version.Value,
                CurrentCertificateVersion,
                StringComparison.Ordinal))
        {
            return Invalid<CommitmentCertificateBody>(
                $"{path}.certificateVersion",
                "Unknown commitment certificate version.");
        }

        if (!Sha256Hex.TryCreate(inputHash.Value, out var input)
            || !Sha256Hex.TryCreate(proposedHash.Value, out var proposed))
        {
            return Invalid<CommitmentCertificateBody>(
                path,
                "Certificate state hashes must be lowercase SHA-256 values.");
        }

        var witnesses = ReadWitnesses(
            witnessesProperty.Value,
            $"{path}.witnesses");

        if (!witnesses.IsSuccess)
        {
            return ProtocolValueReadResult<CommitmentCertificateBody>.Failure(
                witnesses.Error!);
        }

        var semanticError = ValidateCertificateSemantics(
            normal.Value,
            publications.Value!.Length,
            promiseCount.Value,
            witnesses.Value!.Count,
            path);

        if (semanticError is not null)
        {
            return ProtocolValueReadResult<CommitmentCertificateBody>.Failure(
                semanticError);
        }

        return ProtocolValueReadResult<CommitmentCertificateBody>.Success(
            new CommitmentCertificateBody(
                version.Value!,
                validator.Value!,
                normal.Value,
                input!,
                proposed!,
                publications.Value!,
                physicalCount.Value,
                promiseCount.Value,
                witnesses.Value!));
    }

    public static void WriteVector(
        Utf8JsonWriter writer,
        CommitmentVectorContract vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        var values = VectorValues(vector);

        if (values.Any(value => value is < 0 or > ProtocolLimits.MaxCanonicalInteger))
        {
            throw new ArgumentOutOfRangeException(nameof(vector));
        }

        writer.WriteStartObject();
        writer.WriteNumber("pickupEtaTotalMs", vector.PickupEtaTotalMs);
        writer.WriteNumber("dropEtaTotalMs", vector.DropEtaTotalMs);
        writer.WriteNumber("materialEtaRevisionCount", vector.MaterialEtaRevisionCount);
        writer.WriteNumber("vehicleSwitchCount", vector.VehicleSwitchCount);
        writer.WriteNumber("pickupStopRelocationMm", vector.PickupStopRelocationMm);
        writer.WriteNumber("pickupStopSwitchCount", vector.PickupStopSwitchCount);
        writer.WriteNumber("dropStopRelocationMm", vector.DropStopRelocationMm);
        writer.WriteNumber("dropStopSwitchCount", vector.DropStopSwitchCount);
        writer.WriteNumber("incumbentOrderInversionCount", vector.IncumbentOrderInversionCount);
        writer.WriteNumber("prePickupInsertedStopCount", vector.PrePickupInsertedStopCount);
        writer.WriteEndObject();
    }

    public static ProtocolPayloadError? ValidateVector(
        JsonElement element,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            VectorFields);

        if (objectError is not null)
        {
            return objectError;
        }

        foreach (var field in VectorFields.Order(StringComparer.Ordinal))
        {
            var value = ProtocolPayloadReader.ReadRequiredInteger(
                element,
                path,
                field,
                minimum: 0);

            if (!value.IsSuccess)
            {
                return value.Error;
            }
        }

        return null;
    }

    public static void WritePromise(
        Utf8JsonWriter writer,
        PromiseProjectionContract promise)
    {
        ArgumentNullException.ThrowIfNull(promise);
        RequireIdentifier(promise.RequestId, nameof(promise.RequestId));
        RequireIdentifier(promise.VehicleId, nameof(promise.VehicleId));
        RequireIdentifier(promise.PickupStopId, nameof(promise.PickupStopId));
        RequireIdentifier(promise.PickupNodeId, nameof(promise.PickupNodeId));
        RequireIdentifier(promise.DropStopId, nameof(promise.DropStopId));
        RequireIdentifier(promise.DropNodeId, nameof(promise.DropNodeId));
        RequireCanonical(promise.PickupEtaMs, nameof(promise.PickupEtaMs));
        RequireCanonical(promise.DropEtaMs, nameof(promise.DropEtaMs));
        writer.WriteStartObject();
        writer.WriteString("requestId", promise.RequestId);
        writer.WriteString("vehicleId", promise.VehicleId);
        writer.WriteString("pickupStopId", promise.PickupStopId);
        writer.WriteString("pickupNodeId", promise.PickupNodeId);
        writer.WriteString("dropStopId", promise.DropStopId);
        writer.WriteString("dropNodeId", promise.DropNodeId);
        writer.WriteNumber("pickupEtaMs", promise.PickupEtaMs);
        writer.WriteNumber("dropEtaMs", promise.DropEtaMs);
        writer.WritePropertyName("serviceOrder");
        writer.WriteStartArray();

        foreach (var token in promise.ServiceOrder)
        {
            RequireIdentifier(token.StopId, nameof(token.StopId));
            writer.WriteStartObject();
            writer.WriteString("stopId", token.StopId);

            if (token.RequestId is not null)
            {
                RequireIdentifier(token.RequestId, nameof(token.RequestId));
                writer.WriteString("requestId", token.RequestId);
            }

            writer.WriteString("kind", RouteStopKindVocabulary.ToProtocolValue(token.Kind));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public static ProtocolPayloadError? ValidatePromise(
        JsonElement element,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            PromiseFields);
        var request = ProtocolPayloadReader.ReadRequiredString(element, path, "requestId");
        var vehicle = ProtocolPayloadReader.ReadRequiredString(element, path, "vehicleId");
        var pickupStop = ProtocolPayloadReader.ReadRequiredString(element, path, "pickupStopId");
        var pickupNode = ProtocolPayloadReader.ReadRequiredString(element, path, "pickupNodeId");
        var dropStop = ProtocolPayloadReader.ReadRequiredString(element, path, "dropStopId");
        var dropNode = ProtocolPayloadReader.ReadRequiredString(element, path, "dropNodeId");
        var pickupEta = ProtocolPayloadReader.ReadRequiredInteger(element, path, "pickupEtaMs", minimum: 0);
        var dropEta = ProtocolPayloadReader.ReadRequiredInteger(element, path, "dropEtaMs", minimum: 0);
        var order = ProtocolPayloadReader.ReadRequiredProperty(element, path, "serviceOrder");
        var error = HelloPayloadCodec.FirstError(
            objectError, request.Error, vehicle.Error, pickupStop.Error,
            pickupNode.Error, dropStop.Error, dropNode.Error, pickupEta.Error,
            dropEta.Error, order.Error);

        if (error is not null)
        {
            return error;
        }

        if (dropEta.Value < pickupEta.Value
            || order.Value.ValueKind != JsonValueKind.Array)
        {
            return InvalidPayload(path, "Promise ETA/order values are invalid.");
        }

        var index = 0;

        foreach (var token in order.Value.EnumerateArray())
        {
            var tokenPath = $"{path}.serviceOrder[{index}]";
            var tokenObject = ProtocolPayloadReader.ValidateObject(
                token,
                tokenPath,
                TokenFields);
            var stop = ProtocolPayloadReader.ReadRequiredString(token, tokenPath, "stopId");
            var requestId = ProtocolPayloadReader.ReadOptionalString(token, tokenPath, "requestId");
            var kind = ProtocolPayloadReader.ReadRequiredString(
                token,
                tokenPath,
                "kind",
                requireOpaqueValue: false);
            error = HelloPayloadCodec.FirstError(
                tokenObject,
                stop.Error,
                requestId.Error,
                kind.Error);

            if (error is not null)
            {
                return error;
            }

            if (!RouteStopKindVocabulary.TryParse(kind.Value, out var parsed)
                || parsed == RouteStopKind.Waypoint && requestId.Value is not null
                || parsed != RouteStopKind.Waypoint && requestId.Value is null)
            {
                return InvalidPayload(tokenPath, "Promise service token is invalid.");
            }

            index++;
        }

        return index == 0
            ? InvalidPayload($"{path}.serviceOrder", "Promise service order cannot be empty.")
            : null;
    }

    private static ProtocolValueReadResult<IReadOnlyList<CertificateWitnessContract>>
        ReadWitnesses(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Failure<IReadOnlyList<CertificateWitnessContract>>(
                ProtocolPayloadErrorCode.InvalidFieldType,
                path,
                "Certificate witnesses must be an array.");
        }

        var values = new List<CertificateWitnessContract>();

        foreach (var item in element.EnumerateArray())
        {
            var itemPath = $"{path}[{values.Count}]";
            var objectError = ProtocolPayloadReader.ValidateObject(
                item,
                itemPath,
                WitnessFields);
            var stage = ProtocolPayloadReader.ReadRequiredString(item, itemPath, "stage");
            var code = ProtocolPayloadReader.ReadRequiredString(item, itemPath, "code");
            var vehicle = ProtocolPayloadReader.ReadOptionalString(item, itemPath, "vehicleId");
            var request = ProtocolPayloadReader.ReadOptionalString(item, itemPath, "requestId");
            var dimension = ProtocolPayloadReader.ReadOptionalString(item, itemPath, "dimension");
            var rule = ProtocolPayloadReader.ReadOptionalString(item, itemPath, "rule");
            var error = HelloPayloadCodec.FirstError(
                objectError, stage.Error, code.Error, vehicle.Error,
                request.Error, dimension.Error, rule.Error);

            if (error is not null)
            {
                return ProtocolValueReadResult<IReadOnlyList<CertificateWitnessContract>>.Failure(error);
            }

            var numbers = new Dictionary<string, long?>();

            foreach (var field in new[] { "limit", "before", "delta", "after" })
            {
                var number = ReadOptionalInteger(item, itemPath, field);

                if (!number.IsSuccess)
                {
                    return ProtocolValueReadResult<IReadOnlyList<CertificateWitnessContract>>.Failure(number.Error!);
                }

                numbers[field] = number.Value;
            }

            values.Add(
                new CertificateWitnessContract(
                    stage.Value!, code.Value!, vehicle.Value, request.Value,
                    dimension.Value, rule.Value, numbers["limit"],
                    numbers["before"], numbers["delta"], numbers["after"]));
        }

        return ProtocolValueReadResult<IReadOnlyList<CertificateWitnessContract>>.Success(values);
    }

    private static void WriteWitness(
        Utf8JsonWriter writer,
        CertificateWitnessContract witness)
    {
        RequireIdentifier(witness.Stage, nameof(witness.Stage));
        RequireIdentifier(witness.Code, nameof(witness.Code));
        writer.WriteStartObject();
        writer.WriteString("stage", witness.Stage);
        writer.WriteString("code", witness.Code);
        WriteOptionalString(writer, "vehicleId", witness.VehicleId);
        WriteOptionalString(writer, "requestId", witness.RequestId);
        WriteOptionalString(writer, "dimension", witness.Dimension);
        WriteOptionalString(writer, "rule", witness.Rule);
        WriteOptionalNumber(writer, "limit", witness.Limit);
        WriteOptionalNumber(writer, "before", witness.Before);
        WriteOptionalNumber(writer, "delta", witness.Delta);
        WriteOptionalNumber(writer, "after", witness.After);
        writer.WriteEndObject();
    }

    private static void ValidateCertificateForEncoding(
        CommitmentCertificateBody body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!string.Equals(
                body.CertificateVersion,
                CurrentCertificateVersion,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("Unknown commitment certificate version.", nameof(body));
        }

        RequireIdentifier(body.ValidatorVersion, nameof(body.ValidatorVersion));
        RequireCanonical(body.PhysicalPlanCount, nameof(body.PhysicalPlanCount));
        RequireCanonical(body.PromiseCount, nameof(body.PromiseCount));

        if (body.PublicationIds.Distinct(StringComparer.Ordinal).Count()
            != body.PublicationIds.Count)
        {
            throw new ArgumentException("Certificate publication IDs must be unique.", nameof(body));
        }

        var semanticError = ValidateCertificateSemantics(
            body.NormalOperation,
            body.PublicationIds.Count,
            body.PromiseCount,
            body.Witnesses.Count,
            "certificate");

        if (semanticError is not null)
        {
            throw new ArgumentException(semanticError.Message, nameof(body));
        }

        foreach (var value in body.PublicationIds)
        {
            RequireIdentifier(value, nameof(body.PublicationIds));
        }
    }

    private static ProtocolPayloadError? ValidateCertificateSemantics(
        bool normalOperation,
        int publicationCount,
        long promiseCount,
        int witnessCount,
        string path)
    {
        if (normalOperation != (witnessCount == 0))
        {
            return InvalidPayload(
                $"{path}.witnesses",
                "Normal-operation certificates require no witnesses; " +
                "non-normal certificates require at least one witness.");
        }

        return publicationCount <= promiseCount
            ? null
            : InvalidPayload(
                $"{path}.publicationIds",
                "Certificate publication count cannot exceed promise count.");
    }

    private static ProtocolValueReadResult<bool> ReadRequiredBoolean(
        JsonElement element,
        string path,
        string field)
    {
        var property = ProtocolPayloadReader.ReadRequiredProperty(element, path, field);

        if (!property.IsSuccess)
        {
            return ProtocolValueReadResult<bool>.Failure(property.Error!);
        }

        return property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? ProtocolValueReadResult<bool>.Success(property.Value.GetBoolean())
            : Failure<bool>(
                ProtocolPayloadErrorCode.InvalidFieldType,
                $"{path}.{field}",
                "Field must be a boolean.");
    }

    private static ProtocolValueReadResult<long?> ReadOptionalInteger(
        JsonElement element,
        string path,
        string field)
    {
        if (!element.TryGetProperty(field, out var property))
        {
            return ProtocolValueReadResult<long?>.Success(null);
        }

        if (property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt64(out var value)
            || value is < 0 or > ProtocolLimits.MaxCanonicalInteger)
        {
            return Failure<long?>(
                ProtocolPayloadErrorCode.InvalidFieldType,
                $"{path}.{field}",
                "Optional witness number must be a canonical non-negative integer.");
        }

        return ProtocolValueReadResult<long?>.Success(value);
    }

    private static long[] VectorValues(CommitmentVectorContract value) =>
    [
        value.PickupEtaTotalMs,
        value.DropEtaTotalMs,
        value.MaterialEtaRevisionCount,
        value.VehicleSwitchCount,
        value.PickupStopRelocationMm,
        value.PickupStopSwitchCount,
        value.DropStopRelocationMm,
        value.DropStopSwitchCount,
        value.IncumbentOrderInversionCount,
        value.PrePickupInsertedStopCount,
    ];

    private static void WriteOptionalString(
        Utf8JsonWriter writer,
        string field,
        string? value)
    {
        if (value is null)
        {
            return;
        }

        RequireIdentifier(value, field);
        writer.WriteString(field, value);
    }

    private static void WriteOptionalNumber(
        Utf8JsonWriter writer,
        string field,
        long? value)
    {
        if (value is null)
        {
            return;
        }

        RequireCanonical(value.Value, field);
        writer.WriteNumber(field, value.Value);
    }

    private static void RequireIdentifier(string value, string parameterName)
    {
        if (!OpaqueIdentifier.IsValid(value))
        {
            throw new ArgumentException(
                "Identifier must contain 1 to 128 valid UTF-8 bytes.",
                parameterName);
        }
    }

    private static void RequireCanonical(long value, string parameterName)
    {
        if (value is < 0 or > ProtocolLimits.MaxCanonicalInteger)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static ProtocolPayloadError InvalidPayload(string path, string message) =>
        new(ProtocolPayloadErrorCode.InvalidValue, path, message);

    private static ProtocolValueReadResult<T> Invalid<T>(string path, string message) =>
        ProtocolValueReadResult<T>.Failure(InvalidPayload(path, message));

    private static ProtocolValueReadResult<T> Failure<T>(
        ProtocolPayloadErrorCode code,
        string path,
        string message) =>
        ProtocolValueReadResult<T>.Failure(new ProtocolPayloadError(code, path, message));

    private static IReadOnlySet<string> Fields(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
