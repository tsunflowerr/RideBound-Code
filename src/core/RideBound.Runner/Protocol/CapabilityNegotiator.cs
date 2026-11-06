using RideBound.Contracts.Protocol;

namespace RideBound.Runner.Protocol;

public sealed record CapabilityRequirementProfile(
    string RequiredPositionModel,
    IReadOnlyCollection<string> RequiredCapabilities,
    IReadOnlyCollection<string> OptionalCapabilities,
    long MinimumFleetSize,
    long MinimumRequestCount,
    CapabilityDowngradeProfile? Downgrade = null);

public sealed record CapabilityDowngradeProfile(
    string DowngradePolicyId,
    string RequiredPositionModel,
    IReadOnlyCollection<string> RequiredCapabilities,
    IReadOnlyCollection<string> OptionalCapabilities,
    long MinimumFleetSize,
    long MinimumRequestCount);

public enum CapabilityNegotiationErrorCode
{
    UnsupportedSchemaVersion,
    UnknownRequiredCapability,
    RequiredCapabilityMissing,
    InvalidRequirementProfile,
}

public sealed record CapabilityNegotiationError(
    CapabilityNegotiationErrorCode Code,
    string ProtocolCode,
    string Message,
    IReadOnlyList<string> Details);

public sealed record CapabilityNegotiationResult
{
    private CapabilityNegotiationResult(
        HelloAckPayload? acknowledgement,
        CapabilityNegotiationError? error)
    {
        Acknowledgement = acknowledgement;
        Error = error;
    }

    public bool IsSuccess => Acknowledgement is not null;

    public HelloAckPayload? Acknowledgement { get; }

    public CapabilityNegotiationError? Error { get; }

    internal static CapabilityNegotiationResult Success(
        HelloAckPayload acknowledgement) =>
        new(acknowledgement, null);

    internal static CapabilityNegotiationResult Failure(
        CapabilityNegotiationError error) =>
        new(null, error);
}

public static class CapabilityNegotiator
{
    public static CapabilityNegotiationResult Negotiate(
        HelloPayload hello,
        CapabilityRequirementProfile requirements)
    {
        ArgumentNullException.ThrowIfNull(hello);
        ArgumentNullException.ThrowIfNull(requirements);

        if (!hello.SupportedSchemaVersions.Any(
                version => ProtocolVersionCompatibility.Evaluate(version).IsCompatible))
        {
            return Failure(
                CapabilityNegotiationErrorCode.UnsupportedSchemaVersion,
                "UNSUPPORTED_SCHEMA_MINOR",
                "Client and runner do not share a supported schema version.",
                hello.SupportedSchemaVersions
                    .Select(version => version.ToString())
                    .Order(StringComparer.Ordinal));
        }

        var primary = EvaluateProfile(
            hello,
            requirements.RequiredPositionModel,
            requirements.RequiredCapabilities,
            requirements.OptionalCapabilities,
            requirements.MinimumFleetSize,
            requirements.MinimumRequestCount,
            CapabilitySelectionStatus.Accepted,
            downgradePolicyId: null);

        if (primary.IsSuccess)
        {
            return primary;
        }

        if (primary.Error?.Code is CapabilityNegotiationErrorCode.UnknownRequiredCapability
            or CapabilityNegotiationErrorCode.InvalidRequirementProfile)
        {
            return primary;
        }

        if (requirements.Downgrade is null)
        {
            return primary;
        }

        var downgrade = requirements.Downgrade;

        if (!OpaqueIdentifier.IsValid(downgrade.DowngradePolicyId))
        {
            return Failure(
                CapabilityNegotiationErrorCode.InvalidRequirementProfile,
                "SCHEMA_VALIDATION_FAILED",
                "downgradePolicyId must contain 1 to 128 valid UTF-8 bytes.",
                ["downgradePolicyId"]);
        }

        return EvaluateProfile(
            hello,
            downgrade.RequiredPositionModel,
            downgrade.RequiredCapabilities,
            downgrade.OptionalCapabilities,
            downgrade.MinimumFleetSize,
            downgrade.MinimumRequestCount,
            CapabilitySelectionStatus.Downgraded,
            downgrade.DowngradePolicyId);
    }

    private static CapabilityNegotiationResult EvaluateProfile(
        HelloPayload hello,
        string requiredPositionModelText,
        IReadOnlyCollection<string> requiredCapabilityTexts,
        IReadOnlyCollection<string> optionalCapabilityTexts,
        long minimumFleetSize,
        long minimumRequestCount,
        CapabilitySelectionStatus status,
        string? downgradePolicyId)
    {
        if (requiredCapabilityTexts is null
            || optionalCapabilityTexts is null
            || minimumFleetSize < 1
            || minimumRequestCount < 1)
        {
            return Failure(
                CapabilityNegotiationErrorCode.InvalidRequirementProfile,
                "SCHEMA_VALIDATION_FAILED",
                "Capability requirements must declare positive scale and explicit sets.",
                ["requirements"]);
        }

        if (!CapabilityVocabulary.TryParsePositionModel(
                requiredPositionModelText,
                out var requiredPositionModel))
        {
            return Failure(
                CapabilityNegotiationErrorCode.UnknownRequiredCapability,
                "CAPABILITY_REQUIRED_MISSING",
                $"Required position model '{requiredPositionModelText}' is unknown.",
                [$"positionModel:{requiredPositionModelText}"]);
        }

        var required = ParseCapabilitySet(requiredCapabilityTexts, required: true);

        if (required.Error is not null)
        {
            return CapabilityNegotiationResult.Failure(required.Error);
        }

        var optional = ParseCapabilitySet(optionalCapabilityTexts, required: false);

        if (optional.Error is not null)
        {
            return CapabilityNegotiationResult.Failure(optional.Error);
        }

        var offered = hello.Capabilities.ToHashSet();
        var missing = required.Capabilities!
            .Where(capability => !offered.Contains(capability))
            .Select(CapabilityVocabulary.ToProtocolValue)
            .ToList();

        if (!SupportsPositionModel(hello.PositionModel, requiredPositionModel))
        {
            missing.Add(
                $"positionModel:{CapabilityVocabulary.ToProtocolValue(requiredPositionModel)}");
        }

        if (hello.MaxFleetSize < minimumFleetSize)
        {
            missing.Add($"maxFleetSize>={minimumFleetSize}");
        }

        if (hello.MaxRequestCount < minimumRequestCount)
        {
            missing.Add($"maxRequestCount>={minimumRequestCount}");
        }

        missing.Sort(StringComparer.Ordinal);

        if (missing.Count > 0)
        {
            return Failure(
                CapabilityNegotiationErrorCode.RequiredCapabilityMissing,
                "CAPABILITY_REQUIRED_MISSING",
                "Client does not provide every capability required by the selected policy.",
                missing);
        }

        var selected = required.Capabilities!
            .Concat(optional.Capabilities!.Where(offered.Contains));

        return CapabilityNegotiationResult.Success(
            new HelloAckPayload(
                ProtocolVersion.Current,
                new CapabilitySelection(
                    status,
                    hello.PositionModel,
                    CapabilityVocabulary.Normalize(selected),
                    hello.MaxFleetSize,
                    hello.MaxRequestCount,
                    downgradePolicyId)));
    }

    private static CapabilitySetParseResult ParseCapabilitySet(
        IEnumerable<string> values,
        bool required)
    {
        var parsed = new HashSet<CapabilityId>();

        foreach (var value in values)
        {
            if (!CapabilityVocabulary.TryParseCapability(value, out var capability))
            {
                return new CapabilitySetParseResult(
                    null,
                    new CapabilityNegotiationError(
                        required
                            ? CapabilityNegotiationErrorCode.UnknownRequiredCapability
                            : CapabilityNegotiationErrorCode.InvalidRequirementProfile,
                        required
                            ? "CAPABILITY_REQUIRED_MISSING"
                            : "SCHEMA_VALIDATION_FAILED",
                        $"Capability '{value}' is unknown for protocol v1.",
                        [value]));
            }

            parsed.Add(capability);
        }

        return new CapabilitySetParseResult(parsed, null);
    }

    private static bool SupportsPositionModel(
        PositionModel offered,
        PositionModel required) =>
        offered == required
        || offered == PositionModel.DirectedEdgeProgress
            && required == PositionModel.NodeOnly;

    private static CapabilityNegotiationResult Failure(
        CapabilityNegotiationErrorCode code,
        string protocolCode,
        string message,
        IEnumerable<string> details) =>
        CapabilityNegotiationResult.Failure(
            new CapabilityNegotiationError(
                code,
                protocolCode,
                message,
                details.Order(StringComparer.Ordinal).ToArray()));

    private sealed record CapabilitySetParseResult(
        IReadOnlySet<CapabilityId>? Capabilities,
        CapabilityNegotiationError? Error);
}
