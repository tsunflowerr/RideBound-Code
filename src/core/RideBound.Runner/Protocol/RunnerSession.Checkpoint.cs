using System.Text.Json;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Protocol;

// The checkpoint message.
public sealed partial class RunnerSession
{
    private RunnerSessionResult ProcessCheckpoint(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || _onlineCoordinator is null
            || _pending is not null
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "checkpoint requires an initialized online session with no pending decision.",
                envelope);
        }

        if (envelope.Payload.EnumerateObject().Any())
        {
            return Error(
                "UNKNOWN_FIELD",
                ProtocolFailureDisposition.RejectMessage,
                "checkpoint request payload must be empty.",
                envelope);
        }

        var committed = CanonicalOf(_onlineCoordinator.CommittedState);

        // The checkpoint re-derives the committed state's bytes without any cache: a stale
        // fragment (an entity mutated in place) fails the session instead of passing silently.
        if (!OnlineStateCanonicalWriter.Write(_onlineCoordinator.CommittedState, null)
                .ContentEquals(committed))
        {
            return Error(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                "Cached canonical state differs from its full recomputation.",
                envelope);
        }

        // The response is the canonical envelope line of the full checkpoint, built
        // around the cached canonical state instead of from copies of it. The shell is the same
        // checkpoint with an empty online state; its canonical line is split at that empty
        // object and the state's own canonical bytes are written there. Canonical JSON writes an
        // object member's value in place, so the line equals the canonical line of the full
        // envelope, and the hash streams the same canonical content.
        using var emptyState = JsonDocument.Parse("{}"u8.ToArray());
        var shell = new CheckpointContent(
            _manifestHash,
            _stateHash,
            _previousDecisionHash,
            _appliedEpoch,
            _nextEventSequence,
            _simulationTimeMilliseconds,
            emptyState.RootElement.Clone());
        var stateParts = committed.Parts
            .Select(part => (ReadOnlyMemory<byte>)part.AsMemory())
            .ToArray();
        var hash = CheckpointPayloadCodec.CalculateHash(shell, stateParts);
        var line = CanonicalJson.Serialize(
            CreateEnvelope(
                "checkpoint",
                CheckpointPayloadCodec.EncodeShell(hash, shell),
                envelope.RunId,
                envelope.ScenarioId));
        var (before, after) = CheckpointPayloadCodec.SplitAtEmptyOnlineState(line);

        return new RunnerSessionResult(
            new StreamedResponse(
                [before, .. stateParts, after]));
    }
}
