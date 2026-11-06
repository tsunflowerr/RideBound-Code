using System.Text.Json;

namespace RideBound.Contracts.Protocol;

public enum PositionModel
{
    NodeOnly,
    DirectedEdgeProgress,
}

public enum CapabilityId
{
    Cancellations,
    DynamicTravelTimes,
    ExactEventOrdering,
    NativeBaselineHooks,
    OldPlanProjection,
    StopRelocation,
    VehicleReassignment,
}

public enum CapabilitySelectionStatus
{
    Accepted,
    Downgraded,
}

public sealed record HelloPayload(
    string AdapterId,
    string AdapterVersion,
    IReadOnlyList<ProtocolVersion> SupportedSchemaVersions,
    PositionModel PositionModel,
    IReadOnlyList<CapabilityId> Capabilities,
    long MaxFleetSize,
    long MaxRequestCount);

public sealed record CapabilitySelection(
    CapabilitySelectionStatus Status,
    PositionModel PositionModel,
    IReadOnlyList<CapabilityId> Capabilities,
    long MaxFleetSize,
    long MaxRequestCount,
    string? DowngradePolicyId = null);

public sealed record HelloAckPayload(
    ProtocolVersion SelectedSchemaVersion,
    CapabilitySelection CapabilitySelection);

public static class CapabilityVocabulary
{
    private static readonly IReadOnlyDictionary<string, CapabilityId> CapabilitiesByName =
        new Dictionary<string, CapabilityId>(StringComparer.Ordinal)
        {
            ["cancellations"] = CapabilityId.Cancellations,
            ["dynamicTravelTimes"] = CapabilityId.DynamicTravelTimes,
            ["exactEventOrdering"] = CapabilityId.ExactEventOrdering,
            ["nativeBaselineHooks"] = CapabilityId.NativeBaselineHooks,
            ["oldPlanProjection"] = CapabilityId.OldPlanProjection,
            ["stopRelocation"] = CapabilityId.StopRelocation,
            ["vehicleReassignment"] = CapabilityId.VehicleReassignment,
        };

    public static bool TryParseCapability(string? value, out CapabilityId capability) =>
        CapabilitiesByName.TryGetValue(value ?? string.Empty, out capability);

    public static string ToProtocolValue(CapabilityId capability) =>
        capability switch
        {
            CapabilityId.Cancellations => "cancellations",
            CapabilityId.DynamicTravelTimes => "dynamicTravelTimes",
            CapabilityId.ExactEventOrdering => "exactEventOrdering",
            CapabilityId.NativeBaselineHooks => "nativeBaselineHooks",
            CapabilityId.OldPlanProjection => "oldPlanProjection",
            CapabilityId.StopRelocation => "stopRelocation",
            CapabilityId.VehicleReassignment => "vehicleReassignment",
            _ => throw new ArgumentOutOfRangeException(
                nameof(capability),
                capability,
                "Unknown capability value."),
        };

    public static bool TryParsePositionModel(string? value, out PositionModel positionModel)
    {
        switch (value)
        {
            case "nodeOnly":
                positionModel = PositionModel.NodeOnly;
                return true;
            case "directedEdgeProgress":
                positionModel = PositionModel.DirectedEdgeProgress;
                return true;
            default:
                positionModel = default;
                return false;
        }
    }

    public static string ToProtocolValue(PositionModel positionModel) =>
        positionModel switch
        {
            PositionModel.NodeOnly => "nodeOnly",
            PositionModel.DirectedEdgeProgress => "directedEdgeProgress",
            _ => throw new ArgumentOutOfRangeException(
                nameof(positionModel),
                positionModel,
                "Unknown position model."),
        };

    public static IReadOnlyList<CapabilityId> Normalize(
        IEnumerable<CapabilityId> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return capabilities
            .Distinct()
            .OrderBy(ToProtocolValue, StringComparer.Ordinal)
            .ToArray();
    }
}

public static class HelloPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "adapterId",
            "adapterVersion",
            "supportedSchemaVersions",
            "positionModel",
            "capabilities",
            "maxFleetSize",
            "maxRequestCount",
        };

    public static ProtocolPayloadDecodeResult<HelloPayload> Decode(JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(payload, "$.payload", Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<HelloPayload>.Failure(objectError);
        }

        var adapterId = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "adapterId");
        var adapterVersion = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "adapterVersion");
        var versionTexts = ProtocolPayloadReader.ReadRequiredStringSet(
            payload,
            "$.payload",
            "supportedSchemaVersions",
            allowEmpty: false);
        var positionText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "positionModel");
        var capabilityTexts = ProtocolPayloadReader.ReadRequiredStringSet(
            payload,
            "$.payload",
            "capabilities",
            allowEmpty: true);
        var maxFleetSize = ProtocolPayloadReader.ReadRequiredInteger(
            payload,
            "$.payload",
            "maxFleetSize",
            minimum: 1);
        var maxRequestCount = ProtocolPayloadReader.ReadRequiredInteger(
            payload,
            "$.payload",
            "maxRequestCount",
            minimum: 1);

        var firstError = FirstError(
            adapterId.Error,
            adapterVersion.Error,
            versionTexts.Error,
            positionText.Error,
            capabilityTexts.Error,
            maxFleetSize.Error,
            maxRequestCount.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<HelloPayload>.Failure(firstError);
        }

        var versions = new List<ProtocolVersion>();

        foreach (var versionText in versionTexts.Value!)
        {
            if (!ProtocolVersion.TryParse(versionText, out var version))
            {
                return InvalidValue<HelloPayload>(
                    "$.payload.supportedSchemaVersions",
                    $"Schema version '{versionText}' is not canonical MAJOR.MINOR.PATCH.");
            }

            versions.Add(version!);
        }

        versions.Sort(CompareVersions);

        if (!CapabilityVocabulary.TryParsePositionModel(
                positionText.Value,
                out var positionModel))
        {
            return InvalidValue<HelloPayload>(
                "$.payload.positionModel",
                $"Position model '{positionText.Value}' is unknown.");
        }

        var capabilities = new List<CapabilityId>();

        foreach (var capabilityText in capabilityTexts.Value!)
        {
            if (!CapabilityVocabulary.TryParseCapability(
                    capabilityText,
                    out var capability))
            {
                return InvalidValue<HelloPayload>(
                    "$.payload.capabilities",
                    $"Capability '{capabilityText}' is unknown for protocol v1.");
            }

            capabilities.Add(capability);
        }

        return ProtocolPayloadDecodeResult<HelloPayload>.Success(
            new HelloPayload(
                adapterId.Value!,
                adapterVersion.Value!,
                versions,
                positionModel,
                CapabilityVocabulary.Normalize(capabilities),
                maxFleetSize.Value,
                maxRequestCount.Value));
    }

    public static byte[] Encode(HelloPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("adapterId", payload.AdapterId);
                writer.WriteString("adapterVersion", payload.AdapterVersion);
                writer.WritePropertyName("supportedSchemaVersions");
                writer.WriteStartArray();

                foreach (var version in payload.SupportedSchemaVersions.OrderBy(
                             version => version,
                             ProtocolVersionComparer.Instance))
                {
                    writer.WriteStringValue(version.ToString());
                }

                writer.WriteEndArray();
                writer.WriteString(
                    "positionModel",
                    CapabilityVocabulary.ToProtocolValue(payload.PositionModel));
                WriteCapabilities(writer, payload.Capabilities);
                writer.WriteNumber("maxFleetSize", payload.MaxFleetSize);
                writer.WriteNumber("maxRequestCount", payload.MaxRequestCount);
                writer.WriteEndObject();
            });
    }

    internal static void WriteCapabilities(
        Utf8JsonWriter writer,
        IEnumerable<CapabilityId> capabilities)
    {
        writer.WritePropertyName("capabilities");
        writer.WriteStartArray();

        foreach (var capability in CapabilityVocabulary.Normalize(capabilities))
        {
            writer.WriteStringValue(CapabilityVocabulary.ToProtocolValue(capability));
        }

        writer.WriteEndArray();
    }

    internal static ProtocolPayloadError? FirstError(
        params ProtocolPayloadError?[] errors) =>
        errors.FirstOrDefault(error => error is not null);

    internal static ProtocolPayloadDecodeResult<T> InvalidValue<T>(
        string field,
        string message)
        where T : class
    {
        return ProtocolPayloadDecodeResult<T>.Failure(
            new ProtocolPayloadError(
                ProtocolPayloadErrorCode.InvalidValue,
                field,
                message));
    }

    private static int CompareVersions(ProtocolVersion left, ProtocolVersion right)
    {
        var major = left.Major.CompareTo(right.Major);

        if (major != 0)
        {
            return major;
        }

        var minor = left.Minor.CompareTo(right.Minor);
        return minor != 0 ? minor : left.Patch.CompareTo(right.Patch);
    }

    private sealed class ProtocolVersionComparer : IComparer<ProtocolVersion>
    {
        public static ProtocolVersionComparer Instance { get; } = new();

        public int Compare(ProtocolVersion? x, ProtocolVersion? y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);
            return CompareVersions(x, y);
        }
    }
}

public static class CapabilitySelectionCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "status",
            "positionModel",
            "capabilities",
            "maxFleetSize",
            "maxRequestCount",
            "downgradePolicyId",
        };

    public static ProtocolPayloadDecodeResult<CapabilitySelection> Decode(
        JsonElement element,
        string path = "$.payload.capabilitySelection")
    {
        var objectError = ProtocolPayloadReader.ValidateObject(element, path, Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<CapabilitySelection>.Failure(objectError);
        }

        var statusText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "status");
        var positionText = ProtocolPayloadReader.ReadRequiredString(
            element,
            path,
            "positionModel");
        var capabilityTexts = ProtocolPayloadReader.ReadRequiredStringSet(
            element,
            path,
            "capabilities",
            allowEmpty: true);
        var maxFleetSize = ProtocolPayloadReader.ReadRequiredInteger(
            element,
            path,
            "maxFleetSize",
            minimum: 1);
        var maxRequestCount = ProtocolPayloadReader.ReadRequiredInteger(
            element,
            path,
            "maxRequestCount",
            minimum: 1);
        var downgradePolicyId = ProtocolPayloadReader.ReadOptionalString(
            element,
            path,
            "downgradePolicyId");

        var firstError = HelloPayloadCodec.FirstError(
            statusText.Error,
            positionText.Error,
            capabilityTexts.Error,
            maxFleetSize.Error,
            maxRequestCount.Error,
            downgradePolicyId.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<CapabilitySelection>.Failure(firstError);
        }

        var status = statusText.Value switch
        {
            "accepted" => CapabilitySelectionStatus.Accepted,
            "downgraded" => CapabilitySelectionStatus.Downgraded,
            _ => (CapabilitySelectionStatus?)null,
        };

        if (status is null)
        {
            return HelloPayloadCodec.InvalidValue<CapabilitySelection>(
                ProtocolPayloadReader.Join(path, "status"),
                $"Capability selection status '{statusText.Value}' is unknown.");
        }

        if (!CapabilityVocabulary.TryParsePositionModel(
                positionText.Value,
                out var positionModel))
        {
            return HelloPayloadCodec.InvalidValue<CapabilitySelection>(
                ProtocolPayloadReader.Join(path, "positionModel"),
                $"Position model '{positionText.Value}' is unknown.");
        }

        var capabilities = new List<CapabilityId>();

        foreach (var capabilityText in capabilityTexts.Value!)
        {
            if (!CapabilityVocabulary.TryParseCapability(
                    capabilityText,
                    out var capability))
            {
                return HelloPayloadCodec.InvalidValue<CapabilitySelection>(
                    ProtocolPayloadReader.Join(path, "capabilities"),
                    $"Capability '{capabilityText}' is unknown for protocol v1.");
            }

            capabilities.Add(capability);
        }

        if (status == CapabilitySelectionStatus.Accepted
            && downgradePolicyId.Value is not null)
        {
            return HelloPayloadCodec.InvalidValue<CapabilitySelection>(
                ProtocolPayloadReader.Join(path, "downgradePolicyId"),
                "An accepted selection cannot declare a downgrade policy.");
        }

        if (status == CapabilitySelectionStatus.Downgraded
            && downgradePolicyId.Value is null)
        {
            return ProtocolPayloadDecodeResult<CapabilitySelection>.Failure(
                new ProtocolPayloadError(
                    ProtocolPayloadErrorCode.MissingRequiredField,
                    ProtocolPayloadReader.Join(path, "downgradePolicyId"),
                    "A downgraded selection requires a named downgradePolicyId."));
        }

        return ProtocolPayloadDecodeResult<CapabilitySelection>.Success(
            new CapabilitySelection(
                status.Value,
                positionModel,
                CapabilityVocabulary.Normalize(capabilities),
                maxFleetSize.Value,
                maxRequestCount.Value,
                downgradePolicyId.Value));
    }

    public static void Write(Utf8JsonWriter writer, CapabilitySelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        writer.WriteStartObject();
        writer.WriteString(
            "status",
            selection.Status == CapabilitySelectionStatus.Accepted
                ? "accepted"
                : "downgraded");
        writer.WriteString(
            "positionModel",
            CapabilityVocabulary.ToProtocolValue(selection.PositionModel));
        HelloPayloadCodec.WriteCapabilities(writer, selection.Capabilities);
        writer.WriteNumber("maxFleetSize", selection.MaxFleetSize);
        writer.WriteNumber("maxRequestCount", selection.MaxRequestCount);

        if (selection.DowngradePolicyId is not null)
        {
            writer.WriteString("downgradePolicyId", selection.DowngradePolicyId);
        }

        writer.WriteEndObject();
    }
}

public static class HelloAckPayloadCodec
{
    private static readonly IReadOnlySet<string> Fields =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "selectedSchemaVersion",
            "capabilitySelection",
        };

    public static ProtocolPayloadDecodeResult<HelloAckPayload> Decode(JsonElement payload)
    {
        var objectError = ProtocolPayloadReader.ValidateObject(payload, "$.payload", Fields);

        if (objectError is not null)
        {
            return ProtocolPayloadDecodeResult<HelloAckPayload>.Failure(objectError);
        }

        var versionText = ProtocolPayloadReader.ReadRequiredString(
            payload,
            "$.payload",
            "selectedSchemaVersion");
        var selectionElement = ProtocolPayloadReader.ReadRequiredProperty(
            payload,
            "$.payload",
            "capabilitySelection");

        var firstError = HelloPayloadCodec.FirstError(
            versionText.Error,
            selectionElement.Error);

        if (firstError is not null)
        {
            return ProtocolPayloadDecodeResult<HelloAckPayload>.Failure(firstError);
        }

        if (!ProtocolVersion.TryParse(versionText.Value, out var version)
            || !ProtocolVersionCompatibility.Evaluate(version!).IsCompatible)
        {
            return HelloPayloadCodec.InvalidValue<HelloAckPayload>(
                "$.payload.selectedSchemaVersion",
                $"Selected schema version '{versionText.Value}' is not supported.");
        }

        var selection = CapabilitySelectionCodec.Decode(selectionElement.Value);

        if (!selection.IsSuccess)
        {
            return ProtocolPayloadDecodeResult<HelloAckPayload>.Failure(selection.Error!);
        }

        return ProtocolPayloadDecodeResult<HelloAckPayload>.Success(
            new HelloAckPayload(version!, selection.Value!));
    }

    public static byte[] Encode(HelloAckPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return ProtocolPayloadReader.Write(
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString(
                    "selectedSchemaVersion",
                    payload.SelectedSchemaVersion.ToString());
                writer.WritePropertyName("capabilitySelection");
                CapabilitySelectionCodec.Write(writer, payload.CapabilitySelection);
                writer.WriteEndObject();
            });
    }
}
