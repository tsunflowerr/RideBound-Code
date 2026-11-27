using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Protocol;

// The canonical bytes of a state and the inputs of the state and decision hashes.
public sealed partial class RunnerSession
{
    /// <summary>
    /// The canonicalInputState of the light decision hash as ordered byte ranges: the canonical form of the
    /// object {eventBatch, stateIdentityBefore}, built from its parts without parsing
    /// them again. Canonical JSON v1 is compositional (CanonicalJson.WriteObject: an object is its members
    /// sorted by ordinal name, each value canonicalized on its own), so the bytes are '{"eventBatch":' +
    /// canonical(batch envelope) + ',"stateIdentityBefore":' + canonical(identity) + '}'.
    /// </summary>
    private IReadOnlyList<ReadOnlyMemory<byte>> CreateCanonicalInputParts(
        byte[] canonicalBatch)
    {
        var parts = new List<ReadOnlyMemory<byte>>
        {
            "{\"eventBatch\":"u8.ToArray(),
            canonicalBatch,
        };

        var identity = new CanonicalBuffer(160);
        identity.Raw(",\"stateIdentityBefore\":"u8);
        var item = new CanonicalObject(identity);
        item.Member("epochId"u8).Integer(_appliedEpoch);
        item.Member("nextEventSeq"u8).Integer(_nextEventSequence);
        item.Member("simTimeMs"u8).Integer(_simulationTimeMilliseconds);
        item.Member("stateHash"u8).String(_stateHash.Value);
        item.End();
        identity.Byte((byte)'}');
        parts.Add(identity.ToArray());
        return parts;
    }

    /// <summary>
    /// Canonical bytes of a state: reused when the state is the committed object
    /// already written, otherwise built with the per-entity fragment cache.
    /// </summary>
    private CanonicalSegments CanonicalOf(OnlineState state)
    {
        if (_committedCanonical is { } committed && ReferenceEquals(committed.State, state))
        {
            return committed.Canonical;
        }

        return OnlineStateCanonicalWriter.Write(state, _canonicalCache);
    }

    /// <summary>The canonical v1 bytes of one immutable state object.</summary>
    private sealed record CanonicalStateBytes(
        OnlineState State,
        CanonicalSegments Canonical);
}
