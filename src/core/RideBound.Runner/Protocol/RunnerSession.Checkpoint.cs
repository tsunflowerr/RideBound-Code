using System.Text.Json;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Runner.StateAudit;

namespace RideBound.Runner.Protocol;

// The checkpoint and restore messages.
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

        // ADR-089: the response is the canonical envelope line the envelope path wrote, built
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
                "checkpoint",
                [before, .. stateParts, after]));
    }

    private RunnerSessionResult ProcessRestore(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Initialized
            || _onlineCoordinator is null
            || _pending is not null
            || _appliedEpoch != 0
            || !IdentityMatches(envelope))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "restore requires a newly initialized online session with no pending decision.",
                envelope);
        }

        var decoded = CheckpointPayloadCodec.Decode(envelope.Payload);

        if (!decoded.IsSuccess)
        {
            return PayloadError(decoded.Error!, envelope);
        }

        var checkpoint = decoded.Value!;

        if (checkpoint.Content.ManifestHash != _manifestHash)
        {
            return Error(
                "MANIFEST_MUTATION",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint manifest hash differs from the initialized run.",
                envelope);
        }

        var restored = OnlineStateCheckpointCodec.Decode(
            checkpoint.Content.OnlineState,
            shadowAllocation: _shadowAllocator is not null);

        if (!restored.IsSuccess)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.RejectMessage,
                restored.Error!,
                envelope);
        }

        var state = restored.State!;

        if (_wp4Configuration.PlanningForecast is { } forecast && state.TravelTimes is not null)
        {
            state = state with { TravelTimes = state.TravelTimes.WithForecast(forecast) };
        }

        var restoredCanonical = CanonicalOf(state);
        var calculatedStateHash = _lightAudit
            ? LightStateIdentityHash(state)
            : OnlineStateCanonicalWriter.HashV1(restoredCanonical);

        if (state.Run.Id.Value != _identity!.RunId.Value
            || state.Run.ScenarioId.Value != _identity.ScenarioId.Value
            || state.Run.AppliedEpoch != checkpoint.Content.AppliedEpoch
            || state.NextEventSequence != checkpoint.Content.NextEventSequence
            || state.Run.SimulationTime.Milliseconds
                != checkpoint.Content.SimulationTimeMs
            || calculatedStateHash != checkpoint.Content.StateHash
            || !string.Equals(
                state.ExpectedInitialTravelTimeSnapshotHash,
                _identity.Manifest.TravelTimeSnapshotHash.Value,
                StringComparison.Ordinal))
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Checkpoint state identity does not match its content or initialized run.",
                envelope);
        }

        _onlineCoordinator = new EventReductionCoordinator(state);
        _committedCanonical = new CanonicalStateBytes(state, restoredCanonical);
        _stagedCanonical = null;
        _appliedEpoch = checkpoint.Content.AppliedEpoch;
        _nextEventSequence = checkpoint.Content.NextEventSequence;
        _simulationTimeMilliseconds = checkpoint.Content.SimulationTimeMs;
        _stateHash = checkpoint.Content.StateHash;
        _previousDecisionHash = checkpoint.Content.PreviousDecisionHash;
        _lastBatch = null;

        return new RunnerSessionResult(
            CreateEnvelope(
                "restore",
                RestoreAcknowledgedPayloadCodec.Encode(
                    new RestoreAcknowledgedPayload(
                        "restored",
                        checkpoint.CheckpointHash)),
                envelope.RunId,
                envelope.ScenarioId));
    }
}
