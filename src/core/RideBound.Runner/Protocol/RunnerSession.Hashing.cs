using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.Online;

namespace RideBound.Runner.Protocol;

// The canonical bytes of a state and the inputs of the state and decision hashes.
public sealed partial class RunnerSession
{
    /// <summary>
    /// The canonicalInputState of decisionHash v1 as ordered byte ranges. The original
    /// CreateCanonicalInput built {"eventBatch", "stateIdentityBefore", "onlineStateBefore"}
    /// with Utf8JsonWriter, re-parsed both documents and canonicalized the whole object.
    /// Canonical JSON v1 is compositional (CanonicalJson.cs:78-176: an object is its members
    /// sorted by ordinal name, each value canonicalized on its own), so the same bytes are
    /// '{"eventBatch":' + canonical(batch envelope) + ',"onlineStateBefore":' +
    /// canonical(committed state) + ',"stateIdentityBefore":' + canonical(identity) + '}'.
    /// </summary>
    private IReadOnlyList<ReadOnlyMemory<byte>> CreateCanonicalInputParts(
        byte[] canonicalBatch,
        bool includeOnlineState)
    {
        var parts = new List<ReadOnlyMemory<byte>>
        {
            "{\"eventBatch\":"u8.ToArray(),
            canonicalBatch,
        };

        if (includeOnlineState)
        {
            parts.Add(",\"onlineStateBefore\":"u8.ToArray());

            foreach (var part in CanonicalOf(_onlineCoordinator!.CommittedState).Parts)
            {
                parts.Add(part);
            }
        }

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
    /// ADR-089: under the light audit a state is identified by its epoch, next event sequence
    /// and simulation time; the checkpoint hash is what binds a restored state's content.
    /// </summary>
    private static Sha256Hex? LightStateIdentityHash(OnlineState state) =>
        EpochId.TryCreate(state.Run.AppliedEpoch, out var epoch)
        && EventSequence.TryCreate(state.NextEventSequence, out var sequence)
        && SimulationTimeMilliseconds.TryCreate(state.Run.SimulationTime.Milliseconds, out var time)
            ? ProtocolHash.CalculateStateIdentityHash(epoch!, sequence!, time!)
            : null;

    /// <summary>
    /// stateAfterHash of the staged state: OnlineStateHash.v1 over cached canonical segments
    /// (exact), or under the light audit (ADR-089) the state identity hash the caller
    /// computed, without writing the state.
    /// </summary>
    private Sha256Hex HashStagedState(
        OnlineState state,
        Sha256Hex? lightStateHash,
        ref CanonicalSegments? canonical)
    {
        if (lightStateHash is not null)
        {
            return lightStateHash;
        }

        canonical = CanonicalOf(state);
        return OnlineStateCanonicalWriter.HashV1(canonical);
    }

    /// <summary>
    /// Canonical bytes of a state: reused when the state is the committed or staged object
    /// already hashed, otherwise built with the per-entity fragment cache.
    /// </summary>
    private CanonicalSegments CanonicalOf(OnlineState state)
    {
        if (_committedCanonical is { } committed && ReferenceEquals(committed.State, state))
        {
            return committed.Canonical;
        }

        if (_stagedCanonical is { } staged && ReferenceEquals(staged.State, state))
        {
            return staged.Canonical;
        }

        return OnlineStateCanonicalWriter.Write(state, _canonicalCache);
    }

    /// <summary>The canonical v1 bytes of one immutable state object.</summary>
    private sealed record CanonicalStateBytes(
        OnlineState State,
        CanonicalSegments Canonical);
}
