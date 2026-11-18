using RideBound.Application.State;

namespace RideBound.Runner.StateAudit;

/// <summary>
/// The canonical bytes of a whole online state, written by OnlineStateCanonicalWriter without a
/// fragment cache.
/// </summary>
public static class OnlineStateCanonicalizer
{
    public static byte[] Canonicalize(OnlineState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var canonical = OnlineStateCanonicalWriter.Write(state, null);
        return canonical.ToArray();
    }
}
