using RideBound.Contracts.Protocol;

namespace RideBound.Runner.Protocol;

public sealed record InitializedSessionIdentity(
    RunId RunId,
    ScenarioId ScenarioId,
    RunManifestIdentity Manifest);

public sealed record InitializeRunValidationContext(
    HelloPayload Hello,
    HelloAckPayload HelloAcknowledgement,
    RunId? ExpectedRunId = null,
    ScenarioId? ExpectedScenarioId = null,
    InitializedSessionIdentity? ActiveSession = null);

public enum InitializeRunValidationErrorCode
{
    InvalidSessionState,
    IdentityMismatch,
    SchemaValidationFailed,
}

public sealed record InitializeRunValidationError(
    InitializeRunValidationErrorCode Code,
    string ProtocolCode,
    string Field,
    string Message);

public sealed record InitializeRunValidationResult
{
    private InitializeRunValidationResult(
        InitializedSessionIdentity? identity,
        InitializeRunValidationError? error)
    {
        Identity = identity;
        Error = error;
    }

    public bool IsSuccess => Identity is not null;

    public InitializedSessionIdentity? Identity { get; }

    public InitializeRunValidationError? Error { get; }

    internal static InitializeRunValidationResult Success(
        InitializedSessionIdentity identity) =>
        new(identity, null);

    internal static InitializeRunValidationResult Failure(
        InitializeRunValidationError error) =>
        new(null, error);
}

public static class InitializeRunValidator
{
    public static InitializeRunValidationResult Validate(
        ProtocolEnvelope envelope,
        InitializeRunPayload payload,
        InitializeRunValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(context);

        if (context.ActiveSession is not null)
        {
            return Failure(
                InitializeRunValidationErrorCode.InvalidSessionState,
                "INVALID_SESSION_STATE",
                "$.messageType",
                "An active session cannot be initialized again.");
        }

        if (envelope.MessageType.Value != "initializeRun"
            || envelope.RunId is null
            || envelope.ScenarioId is null
            || envelope.EpochId is not null
            || envelope.SimTime is not null)
        {
            return Failure(
                InitializeRunValidationErrorCode.SchemaValidationFailed,
                "SCHEMA_VALIDATION_FAILED",
                "$",
                "initializeRun requires run/scenario identity and no epoch context.");
        }

        if (context.ExpectedRunId is not null
            && context.ExpectedRunId != envelope.RunId)
        {
            return IdentityMismatch("$.runId", "runId does not match expected run identity.");
        }

        if (context.ExpectedScenarioId is not null
            && context.ExpectedScenarioId != envelope.ScenarioId)
        {
            return IdentityMismatch(
                "$.scenarioId",
                "scenarioId does not match expected scenario identity.");
        }

        var manifest = payload.Manifest;

        if (envelope.SchemaVersion != context.HelloAcknowledgement.SelectedSchemaVersion
            || envelope.SchemaVersion != manifest.ProtocolVersion)
        {
            return IdentityMismatch(
                "$.payload.manifest.protocolVersion",
                "Envelope, hello acknowledgement and manifest schema versions differ.");
        }

        if (!context.Hello.SupportedSchemaVersions.Any(
                offered => ArePatchCompatible(
                    offered,
                    context.HelloAcknowledgement.SelectedSchemaVersion)))
        {
            return IdentityMismatch(
                "$.payload.manifest.protocolVersion",
                "Selected schema version was not offered by the client.");
        }

        if (!string.Equals(
                manifest.Adapter.AdapterId,
                context.Hello.AdapterId,
                StringComparison.Ordinal)
            || !string.Equals(
                manifest.Adapter.AdapterVersion,
                context.Hello.AdapterVersion,
                StringComparison.Ordinal))
        {
            return IdentityMismatch(
                "$.payload.manifest.adapter",
                "Manifest adapter identity differs from the hello identity.");
        }

        var acknowledgedSelection =
            context.HelloAcknowledgement.CapabilitySelection;

        if (acknowledgedSelection.PositionModel != context.Hello.PositionModel
            || acknowledgedSelection.MaxFleetSize != context.Hello.MaxFleetSize
            || acknowledgedSelection.MaxRequestCount != context.Hello.MaxRequestCount
            || acknowledgedSelection.Capabilities.Except(
                context.Hello.Capabilities).Any())
        {
            return IdentityMismatch(
                "$.payload.manifest.capabilitySelection",
                "helloAck selects capability values that the client did not offer.");
        }

        if (!SelectionsEqual(
                manifest.CapabilitySelection,
                acknowledgedSelection))
        {
            return IdentityMismatch(
                "$.payload.manifest.capabilitySelection",
                "Manifest capability selection differs from helloAck.");
        }

        return InitializeRunValidationResult.Success(
            new InitializedSessionIdentity(
                envelope.RunId,
                envelope.ScenarioId,
                manifest));
    }

    private static bool SelectionsEqual(
        CapabilitySelection left,
        CapabilitySelection right) =>
        left.Status == right.Status
        && left.PositionModel == right.PositionModel
        && left.MaxFleetSize == right.MaxFleetSize
        && left.MaxRequestCount == right.MaxRequestCount
        && string.Equals(
            left.DowngradePolicyId,
            right.DowngradePolicyId,
            StringComparison.Ordinal)
        && left.Capabilities
            .Select(CapabilityVocabulary.ToProtocolValue)
            .Order(StringComparer.Ordinal)
            .SequenceEqual(
                right.Capabilities
                    .Select(CapabilityVocabulary.ToProtocolValue)
                    .Order(StringComparer.Ordinal),
                StringComparer.Ordinal);

    private static bool ArePatchCompatible(
        ProtocolVersion offered,
        ProtocolVersion selected) =>
        offered.Major == selected.Major
        && offered.Minor == selected.Minor;

    private static InitializeRunValidationResult IdentityMismatch(
        string field,
        string message) =>
        Failure(
            InitializeRunValidationErrorCode.IdentityMismatch,
            "IDENTITY_MISMATCH",
            field,
            message);

    private static InitializeRunValidationResult Failure(
        InitializeRunValidationErrorCode code,
        string protocolCode,
        string field,
        string message) =>
        InitializeRunValidationResult.Failure(
            new InitializeRunValidationError(
                code,
                protocolCode,
                field,
                message));
}
