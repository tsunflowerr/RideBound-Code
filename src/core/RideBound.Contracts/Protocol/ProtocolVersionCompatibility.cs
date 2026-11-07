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
    ProtocolVersion SenderVersion)
{
    public bool IsCompatible => Status == ProtocolVersionCompatibilityStatus.Compatible;
}

public static class ProtocolVersionCompatibility
{
    /// <summary>
    /// A sender is compatible when it has the receiver's major and a minor no newer than the receiver's.
    /// A newer minor would need an explicit safe forward-minor profile, and protocol v1 declares none
    /// (schemas/v1/compatibility-matrix.json), so it is unsupported.
    /// </summary>
    public static ProtocolVersionCompatibilityResult Evaluate(
        ProtocolVersion senderVersion,
        ProtocolVersion? receiverVersion = null)
    {
        ArgumentNullException.ThrowIfNull(senderVersion);
        receiverVersion ??= ProtocolVersion.Current;

        if (senderVersion.Major != receiverVersion.Major)
        {
            return new ProtocolVersionCompatibilityResult(
                ProtocolVersionCompatibilityStatus.UnsupportedMajor,
                receiverVersion,
                senderVersion);
        }

        if (senderVersion.Minor > receiverVersion.Minor)
        {
            return new ProtocolVersionCompatibilityResult(
                ProtocolVersionCompatibilityStatus.UnsupportedMinor,
                receiverVersion,
                senderVersion);
        }

        return new ProtocolVersionCompatibilityResult(
            ProtocolVersionCompatibilityStatus.Compatible,
            receiverVersion,
            senderVersion);
    }
}
