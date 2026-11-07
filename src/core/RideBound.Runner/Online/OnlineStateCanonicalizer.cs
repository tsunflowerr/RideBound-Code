using RideBound.Application.State;
using RideBound.Contracts.Protocol;

namespace RideBound.Runner.Online;

/// <summary>
/// The canonical bytes and the OnlineStateHash.v1 of a whole online state, written by
/// OnlineStateCanonicalWriter without a fragment cache.
/// </summary>
public static class OnlineStateCanonicalizer
{
    public static byte[] Canonicalize(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var canonical = OnlineStateCanonicalWriter.Write(state, null);
        return canonical.ToArray();
    }

    public static Sha256Hex CalculateHash(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var canonical = OnlineStateCanonicalWriter.Write(state, null);
        return OnlineStateCanonicalWriter.HashV1(canonical);
    }
}
