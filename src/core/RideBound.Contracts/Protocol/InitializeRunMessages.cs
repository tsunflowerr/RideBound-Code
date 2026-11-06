using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public sealed record SourceUnitConversion(
    string Quantity,
    string SourceUnit,
    string CanonicalUnit,
    string RoundingRule);

public sealed record AdapterIdentity(
    string AdapterId,
    string AdapterVersion);

public sealed record SimulatorIdentity(
    string SimulatorId,
    string SimulatorVersion,
    SourceCommitSha UpstreamCommitSha);

public sealed record RunManifestIdentity(
    ProtocolVersion ProtocolVersion,
    long MasterSeed,
    string PolicyId,
    string PolicyVersion,
    Sha256Hex PolicyConfigurationHash,
    Sha256Hex ScenarioContentHash,
    Sha256Hex GraphSnapshotHash,
    Sha256Hex TravelTimeSnapshotHash,
    string CostUnitId,
    IReadOnlyList<SourceUnitConversion> SourceUnitConversions,
    CapabilitySelection CapabilitySelection,
    AdapterIdentity Adapter,
    SimulatorIdentity Simulator,
    SourceCommitSha CoreCommitSha,
    Sha256Hex BinarySha256);

public sealed record InitializeRunPayload(RunManifestIdentity Manifest);

public sealed record InitialStateIdentity(
    EpochId EpochId,
    EventSequence NextEventSequence,
    SimulationTimeMilliseconds SimTime,
    Sha256Hex StateHash);

public sealed record InitializedPayload(
    Sha256Hex ManifestHash,
    InitialStateIdentity InitialStateIdentity);

public static class InitializeRunPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "manifest",
        };

    public static ProtocolPayloadDecodeResult<InitializeRunPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(payload, "$.payload", Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<InitializeRunPayload>.Failure(objectError);
        }

        var manifestElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "manifest");

        if (!manifestElement.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<InitializeRunPayload>.Failure(
                manifestElement.Error!);
        }

        var manifest = RunManifestIdentityCodec.Decode(
            manifestElement.Value,
            "$.payload.manifest");

        if (!manifest.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<InitializeRunPayload>.Failure(
                manifest.Error!);
        }

        return ProtocolPayloadDecodeResult<InitializeRunPayload>.Success(
            new InitializeRunPayload(manifest.Value!));
    }

    public static byte[] Encode(InitializeRunPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("manifest");
                RunManifestIdentityCodec.Write(writer, payload.Manifest);
                writer.WriteEndObject();
            });
    }
}

public static class RunManifestIdentityCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "protocolVersion",
            "masterSeed",
            "policyId",
            "policyVersion",
            "policyConfigurationHash",
            "scenarioContentHash",
            "graphSnapshotHash",
            "travelTimeSnapshotHash",
            "costUnitId",
            "sourceUnitConversions",
            "capabilitySelection",
            "adapter",
            "simulator",
            "coreCommitSha",
            "binarySha256",
        };

    private static readonly IReadOnlySet<string> ConversionFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "quantity",
            "sourceUnit",
            "canonicalUnit",
            "roundingRule",
        };

    private static readonly IReadOnlySet<string> AdapterFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "adapterId",
            "adapterVersion",
        };

    private static readonly IReadOnlySet<string> SimulatorFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "simulatorId",
            "simulatorVersion",
            "upstreamCommitSha",
        };

    public static ProtocolPayloadDecodeResult<RunManifestIdentity> Decode(
        JsonElement element,
        string path = "$.payload.manifest")
    {
        var objectError = ProtocolPayloadReader.ValidateObject(element, path, Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<RunManifestIdentity>.Failure(objectError);
        }

        var protocolVersionText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "protocolVersion");
        var masterSeed = ProtocolPayloadReader.ReadRequiredInteger(
            element,
            path,
            "masterSeed",
            minimum: 0);
        var policyId = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "policyId");
        var policyVersion = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "policyVersion");
        var policyConfigurationHashText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "policyConfigurationHash");
        var scenarioContentHashText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "scenarioContentHash");
        var graphSnapshotHashText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "graphSnapshotHash");
        var travelTimeSnapshotHashText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "travelTimeSnapshotHash");
        var costUnitId = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "costUnitId");
        var conversionsElement = ProtocolPayloadReader.ReadRequiredProperty(
            element,
            path,
            "sourceUnitConversions");
        var selectionElement = ProtocolPayloadReader.ReadRequiredProperty(
            element,
            path,
            "capabilitySelection");
        var adapterElement = ProtocolPayloadReader.ReadRequiredProperty(
            element,
            path,
            "adapter");
        var simulatorElement = ProtocolPayloadReader.ReadRequiredProperty(
            element,
            path,
            "simulator");
        var coreCommitText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "coreCommitSha");
        var binaryHashText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "binarySha256");

        var firstError = HelloPayloadCodec.FirstError(
            protocolVersionText.Error,
            masterSeed.Error,
            policyId.Error,
            policyVersion.Error,
            policyConfigurationHashText.Error,
            scenarioContentHashText.Error,
            graphSnapshotHashText.Error,
            travelTimeSnapshotHashText.Error,
            costUnitId.Error,
            conversionsElement.Error,
            selectionElement.Error,
            adapterElement.Error,
            simulatorElement.Error,
            coreCommitText.Error,
            binaryHashText.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<RunManifestIdentity>.Failure(firstError);
        }

        if (!ProtocolVersion.TryParse(protocolVersionText.Value, out var protocolVersion)
            || !ProtocolVersionCompatibility.Evaluate(protocolVersion!).IsCompatible)
        {
            return Invalid(
                ProtocolPayloadReader.Join(path, "protocolVersion"),
                $"Manifest protocol version '{protocolVersionText.Value}' is unsupported.");
        }

        if (!TryReadHash(
                policyConfigurationHashText.Value,
                ProtocolPayloadReader.Join(path, "policyConfigurationHash"),
                out var policyConfigurationHash,
                out var hashError)
            || !TryReadHash(
                scenarioContentHashText.Value,
                ProtocolPayloadReader.Join(path, "scenarioContentHash"),
                out var scenarioContentHash,
                out hashError)
            || !TryReadHash(
                graphSnapshotHashText.Value,
                ProtocolPayloadReader.Join(path, "graphSnapshotHash"),
                out var graphSnapshotHash,
                out hashError)
            || !TryReadHash(
                travelTimeSnapshotHashText.Value,
                ProtocolPayloadReader.Join(path, "travelTimeSnapshotHash"),
                out var travelTimeSnapshotHash,
                out hashError)
            || !TryReadHash(
                binaryHashText.Value,
                ProtocolPayloadReader.Join(path, "binarySha256"),
                out var binaryHash,
                out hashError))
        {
            return ProtocolPayloadDecodeResult<RunManifestIdentity>.Failure(hashError!);
        }

        if (!SourceCommitSha.TryCreate(coreCommitText.Value, out var coreCommit))
        {
            return Invalid(
                ProtocolPayloadReader.Join(path, "coreCommitSha"),
                "coreCommitSha must be 40 or 64 lowercase hexadecimal characters.");
        }

        var conversions = DecodeConversions(
            conversionsElement.Value,
            ProtocolPayloadReader.Join(path, "sourceUnitConversions"));

        if (!conversions.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<RunManifestIdentity>.Failure(
                conversions.Error!);
        }

        var selection = CapabilitySelectionCodec.Decode(
            selectionElement.Value,
            ProtocolPayloadReader.Join(path, "capabilitySelection"));

        if (!selection.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<RunManifestIdentity>.Failure(
                selection.Error!);
        }

        var adapter = DecodeAdapter(
            adapterElement.Value,
            ProtocolPayloadReader.Join(path, "adapter"));

        if (!adapter.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<RunManifestIdentity>.Failure(adapter.Error!);
        }

        var simulator = DecodeSimulator(
            simulatorElement.Value,
            ProtocolPayloadReader.Join(path, "simulator"));

        if (!simulator.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<RunManifestIdentity>.Failure(
                simulator.Error!);
        }

        return ProtocolPayloadDecodeResult<RunManifestIdentity>.Success(
            new RunManifestIdentity(
                protocolVersion!,
                masterSeed.Value,
                policyId.Value!,
                policyVersion.Value!,
                policyConfigurationHash!,
                scenarioContentHash!,
                graphSnapshotHash!,
                travelTimeSnapshotHash!,
                costUnitId.Value!,
                conversions.Value!,
                selection.Value!,
                adapter.Value!,
                simulator.Value!,
                coreCommit!,
                binaryHash!));
    }

    public static void Write(Utf8JsonWriter writer, RunManifestIdentity manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        writer.WriteStartObject();
        writer.WriteString("protocolVersion", manifest.ProtocolVersion.ToString());
        writer.WriteNumber("masterSeed", manifest.MasterSeed);
        writer.WriteString("policyId", manifest.PolicyId);
        writer.WriteString("policyVersion", manifest.PolicyVersion);
        writer.WriteString(
            "policyConfigurationHash",
            manifest.PolicyConfigurationHash.Value);
        writer.WriteString("scenarioContentHash", manifest.ScenarioContentHash.Value);
        writer.WriteString("graphSnapshotHash", manifest.GraphSnapshotHash.Value);
        writer.WriteString(
            "travelTimeSnapshotHash",
            manifest.TravelTimeSnapshotHash.Value);
        writer.WriteString("costUnitId", manifest.CostUnitId);
        writer.WritePropertyName("sourceUnitConversions");
        writer.WriteStartArray();

        foreach (var conversion in manifest.SourceUnitConversions.OrderBy(
                     conversion => conversion.Quantity,
                     StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("quantity", conversion.Quantity);
            writer.WriteString("sourceUnit", conversion.SourceUnit);
            writer.WriteString("canonicalUnit", conversion.CanonicalUnit);
            writer.WriteString("roundingRule", conversion.RoundingRule);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("capabilitySelection");
        CapabilitySelectionCodec.Write(writer, manifest.CapabilitySelection);
        writer.WritePropertyName("adapter");
        writer.WriteStartObject();
        writer.WriteString("adapterId", manifest.Adapter.AdapterId);
        writer.WriteString("adapterVersion", manifest.Adapter.AdapterVersion);
        writer.WriteEndObject();
        writer.WritePropertyName("simulator");
        writer.WriteStartObject();
        writer.WriteString("simulatorId", manifest.Simulator.SimulatorId);
        writer.WriteString("simulatorVersion", manifest.Simulator.SimulatorVersion);
        writer.WriteString(
            "upstreamCommitSha",
            manifest.Simulator.UpstreamCommitSha.Value);
        writer.WriteEndObject();
        writer.WriteString("coreCommitSha", manifest.CoreCommitSha.Value);
        writer.WriteString("binarySha256", manifest.BinarySha256.Value);
        writer.WriteEndObject();
    }

    private static ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>
        DecodeConversions(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidFieldType,
                    path,
                    $"Field '{path}' must be an array."));
        }

        var conversions = new List<SourceUnitConversion>();
        var quantities = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;

        foreach (var conversionElement in element.EnumerateArray())
        {
            var itemPath = $"{path}[{index}]";
            var objectError = ProtocolPayloadReader.ValidateObject(
                conversionElement,
                itemPath,
                ConversionFields);

            if (objectError is not null)
            {
                return ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>
                    .Failure(objectError);
            }

            var quantity = ProtocolPayloadReader.ReadRequiredString(
                conversionElement,
                itemPath,
                "quantity");
            var sourceUnit = ProtocolPayloadReader.ReadRequiredString(
                conversionElement,
                itemPath,
                "sourceUnit");
            var canonicalUnit = ProtocolPayloadReader.ReadRequiredString(
                conversionElement,
                itemPath,
                "canonicalUnit");
            var roundingRule = ProtocolPayloadReader.ReadRequiredString(
                conversionElement,
                itemPath,
                "roundingRule");
            var firstError = HelloPayloadCodec.FirstError(
                quantity.Error,
                sourceUnit.Error,
                canonicalUnit.Error,
                roundingRule.Error);

            if (firstError is not null)
            {
                return ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>
                    .Failure(firstError);
            }

            if (roundingRule.Value != "roundTiesToEven")
            {
                return ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>
                    .Failure(
                        new ProtocolPayloadError(
                            ProtocolPayloadErrorCode.InvalidValue,
                            ProtocolPayloadReader.Join(itemPath, "roundingRule"),
                            "Protocol v1 requires exact rounding rule 'roundTiesToEven'."));
            }

            if (!quantities.Add(quantity.Value!))
            {
                return ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>
                    .Failure(
                        new ProtocolPayloadError(
                            ProtocolPayloadErrorCode.InvalidValue,
                            ProtocolPayloadReader.Join(itemPath, "quantity"),
                            $"Quantity '{quantity.Value}' appears more than once."));
            }

            conversions.Add(
                new SourceUnitConversion(
                    quantity.Value!,
                    sourceUnit.Value!,
                    canonicalUnit.Value!,
                    roundingRule.Value!));
            index++;
        }

        if (conversions.Count == 0)
        {
            return ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>
                .Failure(
                    new ProtocolPayloadError(
                        ProtocolPayloadErrorCode.InvalidValue,
                        path,
                        "sourceUnitConversions must not be empty."));
        }

        conversions.Sort(
            static (left, right) =>
                StringComparer.Ordinal.Compare(left.Quantity, right.Quantity));

        return ProtocolPayloadDecodeResult<IReadOnlyList<SourceUnitConversion>>.Success(
            conversions);
    }

    private static ProtocolPayloadDecodeResult<AdapterIdentity> DecodeAdapter(
        JsonElement element,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            AdapterFields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<AdapterIdentity>.Failure(objectError);
        }

        var adapterId = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "adapterId");
        var adapterVersion = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "adapterVersion");
        var firstError = HelloPayloadCodec.FirstError(
            adapterId.Error,
            adapterVersion.Error);

        return firstError is not null
            ? ProtocolPayloadDecodeResult<AdapterIdentity>.Failure(firstError)
            : ProtocolPayloadDecodeResult<AdapterIdentity>.Success(
                new AdapterIdentity(adapterId.Value!, adapterVersion.Value!));
    }

    private static ProtocolPayloadDecodeResult<SimulatorIdentity> DecodeSimulator(
        JsonElement element,
        string path)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(
            element,
            path,
            SimulatorFields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<SimulatorIdentity>.Failure(objectError);
        }

        var simulatorId = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "simulatorId");
        var simulatorVersion = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "simulatorVersion");
        var upstreamCommitText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "upstreamCommitSha");
        var firstError = HelloPayloadCodec.FirstError(
            simulatorId.Error,
            simulatorVersion.Error,
            upstreamCommitText.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<SimulatorIdentity>.Failure(firstError);
        }

        if (!SourceCommitSha.TryCreate(
                upstreamCommitText.Value,
                out var upstreamCommit))
        {
            return ProtocolPayloadDecodeResult<SimulatorIdentity>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.InvalidValue,
                    ProtocolPayloadReader.Join(path, "upstreamCommitSha"),
                    "upstreamCommitSha must be 40 or 64 lowercase hexadecimal characters."));
        }

        return ProtocolPayloadDecodeResult<SimulatorIdentity>.Success(
            new SimulatorIdentity(
                simulatorId.Value!,
                simulatorVersion.Value!,
                upstreamCommit!));
    }

    private static bool TryReadHash(
        string? value,
        string field,
        out Sha256Hex? hash,
        out ProtocolPayloadError? error)
    {
        if (Sha256Hex.TryCreate(value, out hash))
        {
            error = null;
            return true;
        }

        error = new ProtocolPayloadError(
            ProtocolPayloadErrorCode.InvalidValue,
            field,
            $"Field '{field}' must be exactly 64 lowercase hexadecimal characters.");
        return false;
    }

    private static ProtocolPayloadDecodeResult<RunManifestIdentity> Invalid(
        string field,
        string message) =>
        HelloPayloadCodec.InvalidValue<RunManifestIdentity>(field, message);
}

public static class InitializedPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "manifestHash",
            "initialStateIdentity",
        };

    private static readonly IReadOnlySet<string> StateFields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "epochId",
            "nextEventSeq",
            "simTimeMs",
            "stateHash",
        };

    public static ProtocolPayloadDecodeResult<InitializedPayload> Decode(
        JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(payload, "$.payload", Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<InitializedPayload>.Failure(objectError);
        }

        var manifestHashText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "manifestHash");
        var stateElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "initialStateIdentity");
        var firstError = HelloPayloadCodec.FirstError(
            manifestHashText.Error,
            stateElement.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<InitializedPayload>.Failure(firstError);
        }

        if (!Sha256Hex.TryCreate(manifestHashText.Value, out var manifestHash))
        {
            return HelloPayloadCodec.InvalidValue<InitializedPayload>(
                "$.payload.manifestHash",
                "manifestHash must be exactly 64 lowercase hexadecimal characters.");
        }

        var stateError = ProtocolPayloadReader.ValidateObject(
            stateElement.Value,
            "$.payload.initialStateIdentity",
            StateFields);

        if (stateError is not null)
        {
            return ProtocolPayloadDecodeResult<InitializedPayload>.Failure(stateError);
        }

        var epoch = ProtocolPayloadReader.ReadRequiredInteger(
            stateElement.Value,
            "$.payload.initialStateIdentity",
            "epochId",
            minimum: 0,
            maximum: 0);
        var nextEvent = ProtocolPayloadReader.ReadRequiredInteger(
            stateElement.Value,
            "$.payload.initialStateIdentity",
            "nextEventSeq",
            minimum: 1,
            maximum: 1);
        var simTime = ProtocolPayloadReader.ReadRequiredInteger(
            stateElement.Value,
            "$.payload.initialStateIdentity",
            "simTimeMs",
            minimum: 0);
        var stateHashText = ProtocolPayloadReader.ReadRequiredString(
            stateElement.Value,
            "$.payload.initialStateIdentity",
            "stateHash");
        firstError = HelloPayloadCodec.FirstError(
            epoch.Error,
            nextEvent.Error,
            simTime.Error,
            stateHashText.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<InitializedPayload>.Failure(firstError);
        }

        if (!Sha256Hex.TryCreate(stateHashText.Value, out var stateHash))
        {
            return HelloPayloadCodec.InvalidValue<InitializedPayload>(
                "$.payload.initialStateIdentity.stateHash",
                "stateHash must be exactly 64 lowercase hexadecimal characters.");
        }

        _ = EpochId.TryCreate(epoch.Value, out var epochId);
        _ = EventSequence.TryCreate(nextEvent.Value, out var nextEventSequence);
        _ = SimulationTimeMilliseconds.TryCreate(simTime.Value, out var simulationTime);

        return ProtocolPayloadDecodeResult<InitializedPayload>.Success(
            new InitializedPayload(
                manifestHash!,
                new InitialStateIdentity(
                    epochId,
                    nextEventSequence,
                    simulationTime,
                    stateHash!)));
    }

    public static byte[] Encode(InitializedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("manifestHash", payload.ManifestHash.Value);
                writer.WritePropertyName("initialStateIdentity");
                writer.WriteStartObject();
                writer.WriteNumber("epochId", payload.InitialStateIdentity.EpochId.Value);
                writer.WriteNumber(
                    "nextEventSeq",
                    payload.InitialStateIdentity.NextEventSequence.Value);
                writer.WriteNumber(
                    "simTimeMs",
                    payload.InitialStateIdentity.SimTime.Value);
                writer.WriteString(
                    "stateHash",
                    payload.InitialStateIdentity.StateHash.Value);
                writer.WriteEndObject();
                writer.WriteEndObject();
            });
    }
}
