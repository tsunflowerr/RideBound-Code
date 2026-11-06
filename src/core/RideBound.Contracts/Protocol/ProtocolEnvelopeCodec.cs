using System.Buffers;
using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public static class ProtocolEnvelopeCodec
{
    private static readonly HashSet<string> EnvelopeFields =
        new(StringComparer.Ordinal)
        {
            "schemaVersion",
            "messageType",
            "runId",
            "scenarioId",
            "epochId",
            "simTimeMs",
            "payload",
        };

    private static readonly HashSet<string> NoContextMessageTypes =
        new(StringComparer.Ordinal)
        {
            "hello",
            "helloAck",
            "shutdown",
        };

    private static readonly HashSet<string> RunContextMessageTypes =
        new(StringComparer.Ordinal)
        {
            "initializeRun",
            "initialized",
            "checkpoint",
            "restore",
            "finalizeRun",
            "runSummary",
        };

    private static readonly HashSet<string> EpochContextMessageTypes =
        new(StringComparer.Ordinal)
        {
            "eventBatch",
            "decision",
            "decisionApplied",
        };

    public static ProtocolEnvelopeDecodeResult Decode(ReadOnlySpan<byte> utf8Json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                });
        }
        catch (JsonException exception)
        {
            return Failure(
                ProtocolEnvelopeErrorCode.MalformedJson,
                "$",
                $"Malformed JSON at byte {exception.BytePositionInLine}.");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    ProtocolEnvelopeErrorCode.RootMustBeObject,
                    "$",
                    "Protocol envelope must be a JSON object.");
            }

            var seenFields = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in root.EnumerateObject())
            {
                if (!seenFields.Add(property.Name))
                {
                    return Failure(
                        ProtocolEnvelopeErrorCode.DuplicateField,
                        property.Name,
                        $"Envelope field '{property.Name}' appears more than once.");
                }
            }

            var schemaTextResult = ReadRequiredString(root, "schemaVersion");

            if (schemaTextResult.Error is not null)
            {
                return ProtocolEnvelopeDecodeResult.Failure(schemaTextResult.Error);
            }

            if (!ProtocolVersion.TryParse(schemaTextResult.Value, out var schemaVersion))
            {
                return Failure(
                    ProtocolEnvelopeErrorCode.InvalidSchemaVersion,
                    "schemaVersion",
                    "schemaVersion must use exact MAJOR.MINOR.PATCH decimal form.");
            }

            var compatibility = ProtocolVersionCompatibility.Evaluate(schemaVersion!);

            if (compatibility.Status == ProtocolVersionCompatibilityStatus.UnsupportedMajor)
            {
                return Failure(
                    ProtocolEnvelopeErrorCode.UnsupportedSchemaMajor,
                    "schemaVersion",
                    $"Schema major '{schemaVersion!.Major}' is not supported by receiver " +
                    $"'{ProtocolVersion.Current}'.",
                    schemaVersion);
            }

            if (compatibility.Status == ProtocolVersionCompatibilityStatus.UnsupportedMinor)
            {
                return Failure(
                    ProtocolEnvelopeErrorCode.UnsupportedSchemaMinor,
                    "schemaVersion",
                    $"Schema minor '{schemaVersion!.Minor}' has no explicit safe compatibility " +
                    $"profile for receiver '{ProtocolVersion.Current}'.",
                    schemaVersion);
            }

            foreach (var property in root.EnumerateObject())
            {
                if (!EnvelopeFields.Contains(property.Name))
                {
                    return Failure(
                        ProtocolEnvelopeErrorCode.UnknownField,
                        property.Name,
                        $"Envelope field '{property.Name}' is not defined in protocol v1.",
                        schemaVersion);
                }
            }

            var messageTypeResult = ReadRequiredString(root, "messageType", schemaVersion);

            if (messageTypeResult.Error is not null)
            {
                return ProtocolEnvelopeDecodeResult.Failure(messageTypeResult.Error);
            }

            if (!ProtocolMessageType.TryParse(messageTypeResult.Value, out var messageType))
            {
                return Failure(
                    ProtocolEnvelopeErrorCode.UnknownMessageType,
                    "messageType",
                    $"Message type '{messageTypeResult.Value}' is unknown for schema " +
                    $"'{schemaVersion}'.",
                    schemaVersion);
            }

            if (!root.TryGetProperty("payload", out var payload))
            {
                return Failure(
                    ProtocolEnvelopeErrorCode.MissingRequiredField,
                    "payload",
                    "Required envelope field 'payload' is missing.",
                    schemaVersion);
            }

            if (payload.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    ProtocolEnvelopeErrorCode.InvalidFieldType,
                    "payload",
                    "Envelope field 'payload' must be a JSON object.",
                    schemaVersion);
            }

            var contextResult = ReadContext(root, messageType!, schemaVersion!);

            if (contextResult.Error is not null)
            {
                return ProtocolEnvelopeDecodeResult.Failure(contextResult.Error);
            }

            return ProtocolEnvelopeDecodeResult.Success(
                new ProtocolEnvelope(
                    schemaVersion!,
                    messageType!,
                    payload.Clone(),
                    contextResult.RunId,
                    contextResult.ScenarioId,
                    contextResult.EpochId,
                    contextResult.SimTime));
        }
    }

    public static byte[] Encode(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateForEncoding(envelope);

        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", envelope.SchemaVersion.ToString());
            writer.WriteString("messageType", envelope.MessageType.Value);

            if (envelope.RunId is not null)
            {
                writer.WriteString("runId", envelope.RunId.Value);
            }

            if (envelope.ScenarioId is not null)
            {
                writer.WriteString("scenarioId", envelope.ScenarioId.Value);
            }

            if (envelope.EpochId is not null)
            {
                writer.WriteNumber("epochId", envelope.EpochId.Value.Value);
            }

            if (envelope.SimTime is not null)
            {
                writer.WriteNumber("simTimeMs", envelope.SimTime.Value.Value);
            }

            writer.WritePropertyName("payload");
            envelope.Payload.WriteTo(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void ValidateForEncoding(ProtocolEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope.SchemaVersion);
        ArgumentNullException.ThrowIfNull(envelope.MessageType);

        if (!ProtocolVersionCompatibility.Evaluate(envelope.SchemaVersion).IsCompatible)
        {
            throw new ArgumentException(
                $"Schema version '{envelope.SchemaVersion}' is not supported.",
                nameof(envelope));
        }

        if (envelope.Payload.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException(
                "Protocol envelope payload must be a JSON object.",
                nameof(envelope));
        }

        var messageType = envelope.MessageType.Value;

        if (NoContextMessageTypes.Contains(messageType))
        {
            if (envelope.RunId is not null
                || envelope.ScenarioId is not null
                || envelope.EpochId is not null
                || envelope.SimTime is not null)
            {
                throw new ArgumentException(
                    $"Message type '{messageType}' cannot contain run or epoch context.",
                    nameof(envelope));
            }

            return;
        }

        if (RunContextMessageTypes.Contains(messageType))
        {
            if (envelope.RunId is null || envelope.ScenarioId is null)
            {
                throw new ArgumentException(
                    $"Message type '{messageType}' requires runId and scenarioId.",
                    nameof(envelope));
            }

            if (envelope.EpochId is not null || envelope.SimTime is not null)
            {
                throw new ArgumentException(
                    $"Message type '{messageType}' cannot contain epoch context.",
                    nameof(envelope));
            }

            return;
        }

        if (EpochContextMessageTypes.Contains(messageType)
            && (envelope.RunId is null
                || envelope.ScenarioId is null
                || envelope.EpochId is null
                || envelope.EpochId.Value.Value < 1
                || envelope.SimTime is null))
        {
            throw new ArgumentException(
                $"Message type '{messageType}' requires complete run and epoch context.",
                nameof(envelope));
        }
    }

    private static ContextReadResult ReadContext(
        JsonElement root,
        ProtocolMessageType messageType,
        ProtocolVersion schemaVersion)
    {
        if (NoContextMessageTypes.Contains(messageType.Value))
        {
            return RejectPresentContextFields(root, schemaVersion);
        }

        if (RunContextMessageTypes.Contains(messageType.Value))
        {
            return ReadRunContext(root, schemaVersion, requireEpochContext: false);
        }

        if (EpochContextMessageTypes.Contains(messageType.Value))
        {
            return ReadRunContext(root, schemaVersion, requireEpochContext: true);
        }

        return ReadOptionalErrorContext(root, schemaVersion);
    }

    private static ContextReadResult RejectPresentContextFields(
        JsonElement root,
        ProtocolVersion schemaVersion)
    {
        foreach (var field in new[] { "runId", "scenarioId", "epochId", "simTimeMs" })
        {
            if (root.TryGetProperty(field, out _))
            {
                return ContextReadResult.Failure(
                    new ProtocolEnvelopeError(
                        ProtocolEnvelopeErrorCode.FieldNotAllowed,
                        field,
                        $"Envelope field '{field}' is not allowed for this message type.",
                        schemaVersion));
            }
        }

        return ContextReadResult.Success();
    }

    private static ContextReadResult ReadRunContext(
        JsonElement root,
        ProtocolVersion schemaVersion,
        bool requireEpochContext)
    {
        var runIdResult = ReadRequiredIdentifier<RunId>(
            root,
            "runId",
            RunId.TryCreate,
            schemaVersion);

        if (runIdResult.Error is not null)
        {
            return ContextReadResult.Failure(runIdResult.Error);
        }

        var scenarioIdResult = ReadRequiredIdentifier<ScenarioId>(
            root,
            "scenarioId",
            ScenarioId.TryCreate,
            schemaVersion);

        if (scenarioIdResult.Error is not null)
        {
            return ContextReadResult.Failure(scenarioIdResult.Error);
        }

        if (!requireEpochContext)
        {
            foreach (var field in new[] { "epochId", "simTimeMs" })
            {
                if (root.TryGetProperty(field, out _))
                {
                    return ContextReadResult.Failure(
                        new ProtocolEnvelopeError(
                            ProtocolEnvelopeErrorCode.FieldNotAllowed,
                            field,
                            $"Envelope field '{field}' is not allowed for this message type.",
                            schemaVersion));
                }
            }

            return ContextReadResult.Success(runIdResult.Value, scenarioIdResult.Value);
        }

        var epochResult = ReadRequiredInteger(root, "epochId", minimum: 1, schemaVersion);

        if (epochResult.Error is not null)
        {
            return ContextReadResult.Failure(epochResult.Error);
        }

        var simTimeResult = ReadRequiredInteger(root, "simTimeMs", minimum: 0, schemaVersion);

        if (simTimeResult.Error is not null)
        {
            return ContextReadResult.Failure(simTimeResult.Error);
        }

        _ = EpochId.TryCreate(epochResult.Value, out var epochId);
        _ = SimulationTimeMilliseconds.TryCreate(simTimeResult.Value, out var simTime);

        return ContextReadResult.Success(
            runIdResult.Value,
            scenarioIdResult.Value,
            epochId,
            simTime);
    }

    private static ContextReadResult ReadOptionalErrorContext(
        JsonElement root,
        ProtocolVersion schemaVersion)
    {
        RunId? runId = null;
        ScenarioId? scenarioId = null;
        EpochId? epochId = null;
        SimulationTimeMilliseconds? simTime = null;

        if (root.TryGetProperty("runId", out var runElement))
        {
            var result = ReadIdentifier<RunId>(
                runElement,
                "runId",
                RunId.TryCreate,
                schemaVersion);

            if (result.Error is not null)
            {
                return ContextReadResult.Failure(result.Error);
            }

            runId = result.Value;
        }

        if (root.TryGetProperty("scenarioId", out var scenarioElement))
        {
            var result = ReadIdentifier<ScenarioId>(
                scenarioElement,
                "scenarioId",
                ScenarioId.TryCreate,
                schemaVersion);

            if (result.Error is not null)
            {
                return ContextReadResult.Failure(result.Error);
            }

            scenarioId = result.Value;
        }

        if (root.TryGetProperty("epochId", out var epochElement))
        {
            var result = ReadInteger(epochElement, "epochId", minimum: 1, schemaVersion);

            if (result.Error is not null)
            {
                return ContextReadResult.Failure(result.Error);
            }

            _ = EpochId.TryCreate(result.Value, out var parsedEpoch);
            epochId = parsedEpoch;
        }

        if (root.TryGetProperty("simTimeMs", out var simTimeElement))
        {
            var result = ReadInteger(simTimeElement, "simTimeMs", minimum: 0, schemaVersion);

            if (result.Error is not null)
            {
                return ContextReadResult.Failure(result.Error);
            }

            _ = SimulationTimeMilliseconds.TryCreate(result.Value, out var parsedSimTime);
            simTime = parsedSimTime;
        }

        return ContextReadResult.Success(runId, scenarioId, epochId, simTime);
    }

    private static StringReadResult ReadRequiredString(
        JsonElement root,
        string field,
        ProtocolVersion? schemaVersion = null)
    {
        if (!root.TryGetProperty(field, out var element))
        {
            return StringReadResult.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.MissingRequiredField,
                    field,
                    $"Required envelope field '{field}' is missing.",
                    schemaVersion));
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return StringReadResult.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.InvalidFieldType,
                    field,
                    $"Envelope field '{field}' must be a string.",
                    schemaVersion));
        }

        return StringReadResult.Success(element.GetString()!);
    }

    private static ValueReadResult<T> ReadRequiredIdentifier<T>(
        JsonElement root,
        string field,
        TryCreateIdentifier<T> factory,
        ProtocolVersion schemaVersion)
        where T : class
    {
        if (!root.TryGetProperty(field, out var element))
        {
            return ValueReadResult<T>.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.MissingRequiredField,
                    field,
                    $"Required envelope field '{field}' is missing.",
                    schemaVersion));
        }

        return ReadIdentifier(element, field, factory, schemaVersion);
    }

    private static ValueReadResult<T> ReadIdentifier<T>(
        JsonElement element,
        string field,
        TryCreateIdentifier<T> factory,
        ProtocolVersion schemaVersion)
        where T : class
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return ValueReadResult<T>.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.InvalidFieldType,
                    field,
                    $"Envelope field '{field}' must be a string.",
                    schemaVersion));
        }

        var text = element.GetString();

        if (!factory(text, out var identifier))
        {
            return ValueReadResult<T>.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.InvalidIdentifier,
                    field,
                    $"Envelope field '{field}' must contain 1 to 128 UTF-8 bytes.",
                    schemaVersion));
        }

        return ValueReadResult<T>.Success(identifier!);
    }

    private static IntegerReadResult ReadRequiredInteger(
        JsonElement root,
        string field,
        long minimum,
        ProtocolVersion schemaVersion)
    {
        if (!root.TryGetProperty(field, out var element))
        {
            return IntegerReadResult.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.MissingRequiredField,
                    field,
                    $"Required envelope field '{field}' is missing.",
                    schemaVersion));
        }

        return ReadInteger(element, field, minimum, schemaVersion);
    }

    private static IntegerReadResult ReadInteger(
        JsonElement element,
        string field,
        long minimum,
        ProtocolVersion schemaVersion)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            return IntegerReadResult.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.InvalidFieldType,
                    field,
                    $"Envelope field '{field}' must be an integer.",
                    schemaVersion));
        }

        var rawNumber = element.GetRawText();

        if (!element.TryGetInt64(out var value)
            || rawNumber.AsSpan().IndexOfAny('.', 'e', 'E') >= 0
            || rawNumber == "-0"
            || value < minimum
            || value > ProtocolLimits.MaxCanonicalInteger)
        {
            return IntegerReadResult.Failure(
                new ProtocolEnvelopeError(
                    ProtocolEnvelopeErrorCode.ValueOutOfRange,
                    field,
                    $"Envelope field '{field}' is outside its canonical integer range.",
                    schemaVersion));
        }

        return IntegerReadResult.Success(value);
    }

    private static ProtocolEnvelopeDecodeResult Failure(
        ProtocolEnvelopeErrorCode code,
        string field,
        string message,
        ProtocolVersion? schemaVersion = null)
    {
        return ProtocolEnvelopeDecodeResult.Failure(
            new ProtocolEnvelopeError(code, field, message, schemaVersion));
    }

    private delegate bool TryCreateIdentifier<T>(string? value, out T? identifier)
        where T : class;

    private sealed record ContextReadResult(
        RunId? RunId,
        ScenarioId? ScenarioId,
        EpochId? EpochId,
        SimulationTimeMilliseconds? SimTime,
        ProtocolEnvelopeError? Error)
    {
        public static ContextReadResult Success(
            RunId? runId = null,
            ScenarioId? scenarioId = null,
            EpochId? epochId = null,
            SimulationTimeMilliseconds? simTime = null)
        {
            return new ContextReadResult(runId, scenarioId, epochId, simTime, null);
        }

        public static ContextReadResult Failure(ProtocolEnvelopeError error)
        {
            return new ContextReadResult(null, null, null, null, error);
        }
    }

    private sealed record StringReadResult(string? Value, ProtocolEnvelopeError? Error)
    {
        public static StringReadResult Success(string value) => new(value, null);

        public static StringReadResult Failure(ProtocolEnvelopeError error) =>
            new(null, error);
    }

    private sealed record ValueReadResult<T>(T? Value, ProtocolEnvelopeError? Error)
        where T : class
    {
        public static ValueReadResult<T> Success(T value) => new(value, null);

        public static ValueReadResult<T> Failure(ProtocolEnvelopeError error) =>
            new(null, error);
    }

    private sealed record IntegerReadResult(long Value, ProtocolEnvelopeError? Error)
    {
        public static IntegerReadResult Success(long value) => new(value, null);

        public static IntegerReadResult Failure(ProtocolEnvelopeError error) =>
            new(default, error);
    }
}
