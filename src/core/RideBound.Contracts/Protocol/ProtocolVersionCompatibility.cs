namespace RideBound.Contracts.Protocol;

public enum ProtocolVersionCompatibilityStatus
{
    Compatible,
    UnsupportedMajor,
    UnsupportedMinor,
}

public sealed record ProtocolVersionCompatibilityResult(
    ProtocolVersionCompatibilityStatus Status,
    ProtocolVersion ReceiverVersion,
    ProtocolVersion SenderVersion,
    bool UsesExplicitSafeForwardMinorProfile)
{
    public bool IsCompatible => Status == ProtocolVersionCompatibilityStatus.Compatible;
}

public enum ProtocolOptionalFieldReadBehavior
{
    Ignore,
    UseDeclaredDefault,
}

public sealed record ProtocolOptionalFieldBehavior(
    string MessageType,
    string FieldPath,
    ProtocolOptionalFieldReadBehavior ReadBehavior,
    bool IncludedInCanonicalProjection,
    bool IncludedInHashProjection);

public sealed record ProtocolForwardMinorProfile(
    int SenderMinor,
    IReadOnlyList<ProtocolOptionalFieldBehavior> OptionalFields);

public static class ProtocolVersionCompatibility
{
    public static ProtocolVersionCompatibilityResult Evaluate(
        ProtocolVersion senderVersion,
        ProtocolVersion? receiverVersion = null,
        ProtocolForwardMinorProfile? forwardMinorProfile = null)
    {
        ArgumentNullException.ThrowIfNull(senderVersion);
        receiverVersion ??= ProtocolVersion.Current;

        if (senderVersion.Major != receiverVersion.Major)
        {
            return new ProtocolVersionCompatibilityResult(
                ProtocolVersionCompatibilityStatus.UnsupportedMajor,
                receiverVersion,
                senderVersion,
                UsesExplicitSafeForwardMinorProfile: false);
        }

        if (senderVersion.Minor > receiverVersion.Minor
            && !IsExplicitSafeProfile(senderVersion, forwardMinorProfile))
        {
            return new ProtocolVersionCompatibilityResult(
                ProtocolVersionCompatibilityStatus.UnsupportedMinor,
                receiverVersion,
                senderVersion,
                UsesExplicitSafeForwardMinorProfile: false);
        }

        return new ProtocolVersionCompatibilityResult(
            ProtocolVersionCompatibilityStatus.Compatible,
            receiverVersion,
            senderVersion,
            senderVersion.Minor > receiverVersion.Minor);
    }

    private static bool IsExplicitSafeProfile(
        ProtocolVersion senderVersion,
        ProtocolForwardMinorProfile? profile)
    {
        if (profile is null
            || profile.SenderMinor != senderVersion.Minor
            || profile.OptionalFields is null
            || profile.OptionalFields.Count == 0)
        {
            return false;
        }

        return profile.OptionalFields.All(
            field => OpaqueIdentifier.IsValid(field.MessageType)
                && field.FieldPath.StartsWith("$.", StringComparison.Ordinal)
                && field.FieldPath.Length > 2);
    }
}
