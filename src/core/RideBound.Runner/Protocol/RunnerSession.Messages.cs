using System.Security.Cryptography;
using RideBound.Application.State;
using RideBound.Contracts.Protocol;
using RideBound.Contracts.Serialization;
using RideBound.Domain.Common;
using RideBound.Domain.Runs;
using RideBound.Runner.Online;

namespace RideBound.Runner.Protocol;

// The hello, initializeRun, eventBatch and decisionApplied messages.
public sealed partial class RunnerSession
{
    private RunnerSessionResult ProcessHello(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.New)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "hello is valid only for a new session.",
                envelope);
        }

        var payloadResult = HelloPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var negotiation = CapabilityNegotiator.Negotiate(
            payloadResult.Value!,
            _requirements);

        if (!negotiation.IsSuccess)
        {
            var error = negotiation.Error!;
            ProtocolErrorCodes.TryGetDisposition(
                error.ProtocolCode,
                out var disposition);
            return Error(
                error.ProtocolCode,
                disposition,
                error.Message,
                envelope);
        }

        _hello = payloadResult.Value;
        _helloAcknowledgement = negotiation.Acknowledgement;
        Status = RunnerSessionStatus.Negotiated;

        return new RunnerSessionResult(
            CreateEnvelope(
                "helloAck",
                HelloAckPayloadCodec.Encode(_helloAcknowledgement!)));
    }

    private RunnerSessionResult ProcessInitialize(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.Negotiated
            || _hello is null
            || _helloAcknowledgement is null)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "initializeRun requires a successful hello negotiation.",
                envelope);
        }

        var payloadResult = InitializeRunPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var validation = InitializeRunValidator.Validate(
            envelope,
            payloadResult.Value!,
            new InitializeRunValidationContext(
                _hello,
                _helloAcknowledgement,
                envelope.RunId,
                envelope.ScenarioId,
                _identity));

        if (!validation.IsSuccess)
        {
            return Error(
                validation.Error!.ProtocolCode,
                ProtocolFailureDisposition.RejectMessage,
                validation.Error.Message,
                envelope);
        }

        if (payloadResult.Value!.Manifest.PolicyConfigurationHash
            != _commitmentPolicyConfigurationHash)
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Manifest policyConfigurationHash does not match the " +
                "loaded commitment policy configuration.",
                envelope);
        }

        if (!StringComparer.Ordinal.Equals(
                payloadResult.Value.Manifest.PolicyId,
                _wp4Configuration.PolicyId)
            || !StringComparer.Ordinal.Equals(
                payloadResult.Value.Manifest.PolicyVersion,
                _wp4Configuration.PolicyVersion))
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "Manifest policyId/policyVersion does not match the loaded WP4 policy configuration.",
                envelope);
        }
        if (_useManifestSolverSeed
            && payloadResult.Value!.Manifest.MasterSeed > int.MaxValue)
        {
            return Error(
                "SCHEMA_VALIDATION_FAILED",
                ProtocolFailureDisposition.FailSession,
                "Manifest solver seed exceeds the deterministic Int32 range.",
                envelope);
        }

        EpochId.TryCreate(0, out var epochId);
        EventSequence.TryCreate(1, out var nextEventSequence);
        SimulationTimeMilliseconds.TryCreate(0, out var simTime);
        _manifestHash = ProtocolHash.CalculateManifestHash(
            payloadResult.Value!.Manifest);
        _stateHash = ProtocolHash.CalculateStateIdentityHash(
            epochId,
            nextEventSequence,
            simTime);
        _identity = validation.Identity;

        var run = RideBoundRun.Create(
            new RunIdentifier(_identity!.RunId.Value),
            new ScenarioIdentifier(_identity.ScenarioId.Value),
            new SimTime(0));
        _onlineCoordinator = new EventReductionCoordinator(
            OnlineState.Create(
                run,
                _identity.Manifest.TravelTimeSnapshotHash.Value),
            CreateEventReducer());
        _stagedCanonical = null;

        if (_lightAudit)
        {
            // ADR-089 light audit: the state hash stays the identity hash computed above.
            _committedCanonical = null;
        }
        else
        {
            var initialCanonical = CanonicalOf(_onlineCoordinator.CommittedState);
            _committedCanonical = new CanonicalStateBytes(
                _onlineCoordinator.CommittedState,
                initialCanonical);
            _stateHash = OnlineStateCanonicalWriter.HashV1(initialCanonical);
        }

        Status = RunnerSessionStatus.Initialized;

        var initialized = new InitializedPayload(
            _manifestHash,
            new InitialStateIdentity(
                epochId,
                nextEventSequence,
                simTime,
                _stateHash));

        return new RunnerSessionResult(
            CreateEnvelope(
                "initialized",
                InitializedPayloadCodec.Encode(initialized),
                envelope.RunId,
                envelope.ScenarioId));
    }

    private RunnerSessionResult ProcessEventBatch(ProtocolEnvelope envelope)
    {
        if (Status is not (
            RunnerSessionStatus.Initialized
            or RunnerSessionStatus.AwaitingDecisionApplied))
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "eventBatch requires an initialized session.",
                envelope);
        }

        if (!IdentityMatches(envelope))
        {
            return Error(
                "IDENTITY_MISMATCH",
                ProtocolFailureDisposition.RejectMessage,
                "Event context differs from initialized run identity.",
                envelope);
        }

        var payloadResult = EventBatchPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        var payload = payloadResult.Value!;
        var firstSequence = payload.Events[0].EventSequence.Value;
        var lastSequence = payload.Events[^1].EventSequence.Value;
        var key = new BatchKey(
            envelope.RunId!,
            envelope.ScenarioId!,
            envelope.EpochId!.Value.Value,
            firstSequence,
            lastSequence);
        var canonicalBatch = CanonicalJson.Canonicalize(
            ProtocolEnvelopeCodec.Encode(envelope));
        var canonicalBatchHash = Convert.ToHexStringLower(SHA256.HashData(canonicalBatch));

        if (_lastBatch is not null && _lastBatch.Key == key)
        {
            if (string.Equals(
                    _lastBatch.CanonicalBatchHash,
                    canonicalBatchHash,
                    StringComparison.Ordinal))
            {
                return new RunnerSessionResult(_lastBatch.Response);
            }

            return Error(
                "DUPLICATE_PAYLOAD_CONFLICT",
                ProtocolFailureDisposition.FailSession,
                "A retried batch key contains different canonical payload bytes.",
                envelope);
        }

        if (_lastBatch is not null
            && firstSequence <= _lastBatch.Key.LastEventSequence)
        {
            return Error(
                "EVENT_SEQUENCE_OVERLAP",
                ProtocolFailureDisposition.FailSession,
                "A partial event batch overlap is not a valid retry.",
                envelope);
        }

        if (Status == RunnerSessionStatus.AwaitingDecisionApplied)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "The previous decision must be acknowledged before another batch.",
                envelope);
        }

        var orderingError = EventBatchOrderingValidator.Validate(
            envelope,
            payload,
            new EventBatchOrderingState(
                _appliedEpoch,
                _nextEventSequence,
                _simulationTimeMilliseconds));

        if (orderingError is not null)
        {
            ProtocolErrorCodes.TryGetDisposition(
                orderingError.ProtocolCode,
                out var disposition);
            return Error(
                orderingError.ProtocolCode,
                disposition,
                orderingError.Message,
                envelope);
        }

        var nextSequence = checked(lastSequence + 1);
        var nextEpoch = envelope.EpochId!.Value.Value;
        var nextSimTime = envelope.SimTime!.Value.Value;
        EpochId.TryCreate(nextEpoch, out var nextEpochId);
        EventSequence.TryCreate(nextSequence, out var nextEventSequence);
        SimulationTimeMilliseconds.TryCreate(nextSimTime, out var nextSimulationTime);

        // ADR-089 light audit: the staged state is identified, not hashed.
        var lightStateAfterHash = _lightAudit
            ? ProtocolHash.CalculateStateIdentityHash(
                nextEpochId!,
                nextEventSequence!,
                nextSimulationTime!)
            : null;
        var online = BuildOnlineDecision(
            envelope,
            canonicalBatchHash,
            lightStateAfterHash);

        if (online.Error is not null)
        {
            return Error(
                online.Error.Code,
                online.Error.Disposition,
                online.Error.Message,
                envelope);
        }

        var stateAfterHash = online.StateAfterHash!;
        var shell = new DecisionPayload(
            DecisionProductionStatus.Produced,
            online.ReasonCode!,
            online.Actions!,
            online.Certificate!,
            online.Solver!,
            _stateHash,
            stateAfterHash,
            _previousDecisionHash,
            ProtocolHash.ZeroHash);
        var canonicalDecision = CanonicalJson.Canonicalize(
            DecisionPayloadCodec.Encode(shell, hashProjection: true));
        var decisionHash = _lightAudit
            ? ProtocolHash.CalculateDecisionHashLight(
                _previousDecisionHash,
                _manifestHash,
                _identity!.Manifest.PolicyVersion,
                CreateCanonicalInputParts(canonicalBatch, includeOnlineState: false),
                canonicalDecision)
            : ProtocolHash.CalculateDecisionHash(
                _previousDecisionHash,
                _manifestHash,
                _identity!.Manifest.PolicyVersion,
                CreateCanonicalInputParts(canonicalBatch, includeOnlineState: true),
                canonicalDecision);
        var decision = shell with { DecisionHash = decisionHash };
        var response = CreateEnvelope(
            "decision",
            DecisionPayloadCodec.Encode(decision),
            envelope.RunId,
            envelope.ScenarioId,
            envelope.EpochId,
            envelope.SimTime);

        _pending = new PendingDecision(
            nextEpoch,
            nextSequence,
            nextSimTime,
            stateAfterHash,
            decisionHash);
        _lastBatch = new CachedBatch(key, canonicalBatchHash, response);
        Status = RunnerSessionStatus.AwaitingDecisionApplied;

        return new RunnerSessionResult(response);
    }

    private RunnerSessionResult ProcessDecisionApplied(ProtocolEnvelope envelope)
    {
        if (Status != RunnerSessionStatus.AwaitingDecisionApplied
            || _pending is null)
        {
            return Error(
                "INVALID_SESSION_STATE",
                ProtocolFailureDisposition.RejectMessage,
                "decisionApplied requires a pending decision.",
                envelope);
        }

        if (!IdentityMatches(envelope)
            || envelope.EpochId!.Value.Value != _pending.Epoch
            || envelope.SimTime!.Value.Value != _pending.SimulationTimeMilliseconds)
        {
            return Error(
                "IDENTITY_MISMATCH",
                ProtocolFailureDisposition.RejectMessage,
                "decisionApplied context differs from the pending decision.",
                envelope);
        }

        var payloadResult = DecisionAppliedPayloadCodec.Decode(envelope.Payload);

        if (!payloadResult.IsSuccess)
        {
            return PayloadError(payloadResult.Error!, envelope);
        }

        if (payloadResult.Value!.DecisionHash != _pending.DecisionHash)
        {
            return Error(
                "HASH_MISMATCH",
                ProtocolFailureDisposition.FailSession,
                "decisionApplied hash differs from the pending decision hash.",
                envelope);
        }

        var acknowledged =
            _onlineCoordinator!.ApplyDecisionAcknowledgement(
                _pending.Epoch);

        if (!acknowledged.IsSuccess)
        {
            return Error(
                "INTERNAL_ERROR",
                ProtocolFailureDisposition.FailSession,
                acknowledged.Witness!.Message,
                envelope);
        }

        // The acknowledged state is the staged object itself
        // (EventReductionCoordinator.ApplyDecisionAcknowledgement), so its canonical
        // bytes are the ones hashed for stateAfterHash.
        _committedCanonical =
            _stagedCanonical is { } staged
                && ReferenceEquals(staged.State, _onlineCoordinator.CommittedState)
                ? staged
                : null;
        _stagedCanonical = null;

        _appliedEpoch = _pending.Epoch;
        _nextEventSequence = _pending.NextEventSequence;
        _simulationTimeMilliseconds = _pending.SimulationTimeMilliseconds;
        _stateHash = _pending.StateHash;
        _previousDecisionHash = _pending.DecisionHash;
        _pending = null;
        Status = RunnerSessionStatus.Initialized;
        return new RunnerSessionResult((ProtocolEnvelope?)null);
    }

    private sealed record BatchKey(
        RunId RunId,
        ScenarioId ScenarioId,
        long Epoch,
        long FirstEventSequence,
        long LastEventSequence);

    private sealed record CachedBatch(
        BatchKey Key,
        string CanonicalBatchHash,
        ProtocolEnvelope Response);

    private sealed record PendingDecision(
        long Epoch,
        long NextEventSequence,
        long SimulationTimeMilliseconds,
        Sha256Hex StateHash,
        Sha256Hex DecisionHash);
}
